using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Light emitters (docs/LIGHT-AND-GRAPH-PROPOSAL.md §1): everything that shines in the sim, in one registry that every
/// rule asking "what is lit here, and how brightly" asks. A point shines all round it; a cone shines along Direction
/// within HalfAngle. Brightness at a point is Intensity × (1 − d / Range)^Falloff, × a cone's angle falloff (full
/// within ConeSoft of its half-angle, easing to nothing at its edge), and nothing with rock between.
/// </summary>
public static class LightTuning
{
    public const float Falloff = 1.6f, ConeSoft = 0.7f;
    /// <summary>Her aura: a point on her, always on (reach × √ her glow stat).</summary>
    public const float AuraReach = 5.5f, AuraIntensity = 1f;
    /// <summary>Her beam: a cone along her aim.</summary>
    public const float BeamReach = 16f, BeamHalfAngleDeg = 22f, BeamIntensity = 1.2f;
    /// <summary>A light-seeker's scan: emitters within Scan metres, every ScanEvery seconds, none dimmer than Threshold where it is.</summary>
    public const float Scan = 20f, ScanEvery = 0.2f, Threshold = 0.04f;
}

public enum LightShape { Point, Cone }

/// <summary>
/// What an emitter's light belongs to, and what pays when something drinks it: her (her HP), a shot (its glow), a
/// plant (it withers), a decoy (it goes out). Several emitters may share one owner and so one pool.
/// </summary>
public interface ILightOwner
{
    /// <summary>Light left to drink (0: spent).</summary>
    float Energy { get; }

    /// <summary>Drink up to <paramref name="amount"/>; returns what was taken.</summary>
    float Absorb(float amount);
}

/// <summary>A plain pool of light (a glowing plant, a decoy): drunk down to nothing, it is spent.</summary>
public sealed class LightPool : ILightOwner
{
    public float Energy { get; set; }

    public LightPool(float energy) => Energy = energy;

    public float Absorb(float amount)
    {
        float taken = MathF.Min(MathF.Max(amount, 0f), Energy);
        Energy -= taken;
        return taken;
    }
}

public sealed class LightEmitter
{
    /// <summary>Stable while it is registered (LightRegistry.Add gives it; 0 when not registered).</summary>
    public int Id { get; internal set; }
    public required ILightOwner Owner { get; init; }
    public LightShape Shape { get; init; } = LightShape.Point;
    public Vector2 Position;
    /// <summary>A cone's unit direction (unused by a point).</summary>
    public Vector2 Direction = Vector2.UnitX;
    public float HalfAngle = LightTuning.BeamHalfAngleDeg * MathF.PI / 180f;
    public float Intensity = 1f, Range = 1f;
    /// <summary>Warm gold for hers; others by owner (for the view).</summary>
    public Vector3 Colour = new(1f, 0.82f, 0.45f);
    /// <summary>False: registered, but lighting nothing (her beam while Fire is up).</summary>
    public bool Active = true;
    /// <summary>Whether a light-seeker may feed on it.</summary>
    public bool Drinkable = true;

    /// <summary>Lighting anything now: active, with light left in its owner.</summary>
    public bool Shining => Active && Intensity > 0f && Range > 0f && Owner.Energy > 0f;
}

/// <summary>Every emitter in a world. Ids are never reused within it.</summary>
public sealed class LightRegistry
{
    readonly List<LightEmitter> _all = new();
    int _next = 1;

    public IReadOnlyList<LightEmitter> All => _all;

    public LightEmitter Add(LightEmitter e)
    {
        if (e.Id != 0 && _all.Contains(e)) return e;
        e.Id = _next++;
        _all.Add(e);
        return e;
    }

    public bool Remove(LightEmitter e)
    {
        if (!_all.Remove(e)) return false;
        e.Id = 0;
        return true;
    }

    public LightEmitter? Get(int id)
    {
        foreach (var e in _all)
            if (e.Id == id) return e;
        return null;
    }
}

/// <summary>The one way to ask how bright a light is somewhere, and which light is brightest.</summary>
public static class LightQuery
{
    /// <summary>How bright this emitter is at <paramref name="at"/>, rock aside (0: not shining, out of reach, outside a cone).</summary>
    public static float Brightness(LightEmitter e, Vector2 at)
    {
        if (!e.Shining) return 0f;
        var to = at - e.Position;
        float d = to.Length();
        if (d >= e.Range) return 0f;
        float b = e.Intensity * MathF.Pow(1f - d / e.Range, LightTuning.Falloff);
        if (e.Shape == LightShape.Cone && d > 1e-4f)
        {
            float angle = MathF.Acos(Math.Clamp(Vector2.Dot(to / d, e.Direction), -1f, 1f));
            if (angle >= e.HalfAngle) return 0f;
            float soft = e.HalfAngle * LightTuning.ConeSoft;
            if (angle > soft)
            {
                float t = (angle - soft) / (e.HalfAngle - soft);
                b *= 1f - t * t * (3f - 2f * t);
            }
        }
        return b;
    }

    /// <summary>As Brightness, and nothing when <paramref name="sight"/> says rock stands between the emitter and the point.</summary>
    public static float Brightness(LightEmitter e, Vector2 at, Func<Vector2, Vector2, bool>? sight)
    {
        float b = Brightness(e, at);
        return b > 0f && sight is not null && !sight(e.Position, at) ? 0f : b;
    }

    /// <summary>
    /// The brightest drinkable emitter as seen from <paramref name="at"/>: within <paramref name="scan"/> metres, at least
    /// <paramref name="threshold"/> bright there, in sight (null: none). Ties go to the lower id, so the pick is steady.
    /// </summary>
    public static LightEmitter? Brightest(IEnumerable<LightEmitter> emitters, Vector2 at, float scan, float threshold,
        Func<Vector2, Vector2, bool>? sight)
    {
        LightEmitter? best = null;
        float bestB = threshold;
        foreach (var e in emitters)
        {
            if (!e.Drinkable || !e.Shining) continue;
            if (Vector2.DistanceSquared(e.Position, at) > scan * scan) continue;
            float b = Brightness(e, at);
            if (b < bestB || (b == bestB && best is not null && e.Id > best.Id)) continue;
            if (sight is not null && !sight(e.Position, at)) continue;
            best = e;
            bestB = b;
        }
        return best;
    }

    /// <summary>All the light falling on <paramref name="at"/> (in sight), summed.</summary>
    public static float Total(IEnumerable<LightEmitter> emitters, Vector2 at, Func<Vector2, Vector2, bool>? sight)
    {
        float sum = 0f;
        foreach (var e in emitters) sum += Brightness(e, at, sight);
        return sum;
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>Her light: her HP is the one pool her aura and her beam are drunk from.</summary>
    sealed class HerLightOwner : ILightOwner
    {
        readonly PlaneWorld _w;
        public HerLightOwner(PlaneWorld w) => _w = w;
        public float Energy => _w.Defeated ? 0f : MathF.Max(_w.Player.Hp, 0f);

        public float Absorb(float amount)
        {
            float before = _w.Player.Hp;
            _w.Drain(amount, DamageSource.Murkling);
            return MathF.Max(before - _w.Player.Hp, 0f);
        }
    }

    /// <summary>A shot's light: drunk, it dims, and goes out when none is left.</summary>
    sealed class ShotLightOwner : ILightOwner
    {
        readonly PlaneShot _s;
        public ShotLightOwner(PlaneShot s) => _s = s;
        public float Energy => _s.Life > 0f ? _s.Dim : 0f;

        public float Absorb(float amount)
        {
            float taken = MathF.Min(MathF.Max(amount, 0f), _s.Dim);
            _s.Dim -= taken;
            if (_s.Dim <= 0f) _s.Life = 0f;
            return taken;
        }
    }

    readonly Dictionary<PlaneShot, LightEmitter> _shotLights = new();
    ILightOwner? _her;
    LightEmitter? _aura, _beam;

    /// <summary>Every light emitter in the world (docs/LIGHT-AND-GRAPH-PROPOSAL.md §1).</summary>
    public LightRegistry Lights { get; } = new();

    /// <summary>What pays for her light: her HP.</summary>
    public ILightOwner Her => _her ??= new HerLightOwner(this);

    /// <summary>Her aura: a point on her, always on while she has light.</summary>
    public LightEmitter Aura
    {
        get
        {
            if (_aura is null)
            {
                _aura = Lights.Add(new LightEmitter { Owner = Her, Intensity = LightTuning.AuraIntensity });
                PlaceAura();
            }
            return _aura;
        }
    }

    /// <summary>Her beam: a cone from her along her aim, shining while Beam is held.</summary>
    public LightEmitter Beam => _beam ??= Lights.Add(new LightEmitter
    {
        Owner = Her, Shape = LightShape.Cone, Intensity = LightTuning.BeamIntensity, Range = LightTuning.BeamReach, Active = false,
    });

    /// <summary>Her beam is shining now.</summary>
    public bool BeamOn => Beam.Shining;

    /// <summary>Where her beam's axis first meets rock (or its full reach): what it strikes, and where the view ends it.</summary>
    public Vector2 BeamEnd { get; private set; }

    /// <summary>
    /// Whether her beam reaches a body (on her plane at <paramref name="at"/>, <paramref name="height"/> metres off it, this
    /// radius): some of it within the beam's reach and cone, in sight of her.
    /// </summary>
    public bool BeamReaches(Vector2 at, float height, float radius)
    {
        var b = Beam;
        if (!b.Shining) return false;
        var to = at - b.Position;
        float flat = to.Length();
        float d = MathF.Sqrt(flat * flat + height * height);
        if (d - radius > b.Range) return false;
        if (flat > radius)
        {
            float angle = MathF.Acos(Math.Clamp(Vector2.Dot(to / flat, b.Direction), -1f, 1f));
            float widen = MathF.Asin(MathF.Min(radius / flat, 1f));
            if (angle - widen > b.HalfAngle) return false;
        }
        return LineOfSight(b.Position, at);
    }

    /// <summary>The emitter for this shot of hers, if it is shining.</summary>
    public LightEmitter? LightOf(PlaneShot shot) => _shotLights.TryGetValue(shot, out var e) ? e : null;

    /// <summary>How bright this emitter is at a point, rock between them blocking it.</summary>
    public float BrightnessAt(LightEmitter e, Vector2 at) => LightQuery.Brightness(e, at, LineOfSight);

    /// <summary>All the light falling on a point, rock blocking it.</summary>
    public float LightAt(Vector2 at) => LightQuery.Total(Lights.All, at, LineOfSight);

    /// <summary>The brightest drinkable light a light-seeker at <paramref name="at"/> can see (null: none).</summary>
    public LightEmitter? BrightestLight(Vector2 at, float scan = LightTuning.Scan) =>
        LightQuery.Brightest(Lights.All, at, scan, LightTuning.Threshold, LineOfSight);

    /// <summary>A light-seeker drinks this much of an emitter: its owner pays (returns what it took).</summary>
    public float DrinkLight(LightEmitter e, float amount) => e.Shining ? e.Owner.Absorb(amount) : 0f;

    void PlaceAura()
    {
        var a = _aura!;
        a.Position = Player.Position;
        a.Range = LightTuning.AuraReach * MathF.Sqrt(MathF.Max(Run.Loadout.Stats[Stat.Glow], 0.1f));
        a.Active = !Defeated;
    }

    /// <summary>
    /// The registry kept in step with the world: her aura on her, her beam along her aim while it is held, an emitter for
    /// each shot of hers that shines.
    /// </summary>
    void SyncLights(in PlaneInput input)
    {
        _ = Aura;
        PlaceAura();
        var beam = Beam;
        beam.Position = Player.Position;
        var aim = input.Aim.LengthSquared() > 1e-6f ? input.Aim : Player.Aim;
        if (aim.LengthSquared() > 1e-6f) beam.Direction = Vector2.Normalize(aim);
        beam.Range = LightTuning.BeamReach * MathF.Sqrt(MathF.Max(Run.Loadout.Stats[Stat.Glow], 0.1f));
        beam.Active = input.Beam && !Defeated;
        BeamEnd = beam.Active ? FirstBlocked(beam.Position, beam.Position + beam.Direction * beam.Range, 0.1f) : beam.Position;
        foreach (var s in Shots)
        {
            if (!s.FromPlayer || s.BossPearl || s.Life <= 0f) continue;
            if (!_shotLights.TryGetValue(s, out var e))
                _shotLights[s] = e = Lights.Add(new LightEmitter { Owner = new ShotLightOwner(s) });
            e.Position = s.Position;
            e.Range = ShotLightReach(s);
            e.Intensity = MathF.Min(0.4f + 0.06f * s.Bubbles, 1.3f) * s.Dim;
        }
        if (_shotLights.Count == 0) return;
        List<PlaneShot>? gone = null;
        foreach (var (s, e) in _shotLights)
            if (s.Life <= 0f || !Shots.Contains(s))
                (gone ??= new()).Add(s);
        if (gone is null) return;
        foreach (var s in gone)
        {
            Lights.Remove(_shotLights[s]);
            _shotLights.Remove(s);
        }
    }
}
