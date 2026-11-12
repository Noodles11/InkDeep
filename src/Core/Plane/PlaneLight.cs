using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Light emitters (docs/LIGHT-AND-GRAPH.md §1): everything that shines in the sim, in one registry that every
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
    /// <summary>
    /// How much of her beam's light reaches a thing (shots' worth for one shot's, PlaneWorld.BeamLight): NearLight at her
    /// bell, falling to FarLight at the end of its reach (by (1 − d / reach)^1.4).
    /// </summary>
    public const float NearLight = 2f, FarLight = 0.25f;
    /// <summary>
    /// Her stats on the beam (docs/proposals/BEAM-PEARLS-PLAN.md, step 2): her fire rate is its tick rate (every beam's worth × her
    /// fire rate against her base); her shot speed its carry (FarLight × her shot speed against her base, between
    /// LeastCarry and MostCarry); her knockback its light pressure (creatures in it pushed away from her at Pressure m/s
    /// for each × of knockback past her own, at full light, less as it falls off).
    /// </summary>
    public const float LeastCarry = 0.05f, MostCarry = 1.5f, Pressure = 1.2f;
    /// <summary>
    /// Her pearls on the beam (docs/LIGHT-AND-GRAPH.md §5.1): her range stat lengthens it and her shot size
    /// widens it (each × its square root; at most MaxHalfAngleDeg); Sunbeam makes it a searchlight (SunbeamReach × as
    /// long, SunbeamHalfAngleDeg wide). Rear Fin adds a beam behind her (RearIntensity, RearReach × as long), Lighthouse
    /// one turning round her at SweepTurn rad/s (SweepIntensity, SweepReach × as long); each burns as one shot. The
    /// Anglerfish Lure makes a shot of hers left hanging LureBright × as bright.
    /// </summary>
    public const float MaxHalfAngleDeg = 35f, SunbeamReach = 1.5f, SunbeamHalfAngleDeg = 15f;
    public const float RearIntensity = 0.6f, RearReach = 0.75f, SweepIntensity = 0.5f, SweepReach = 0.7f, SweepTurn = 0.6f;
    public const float LureBright = 2.5f;
    /// <summary>
    /// Her beam split into rays (docs/proposals/BEAM-PEARLS-PLAN.md, step 1), by her merged shot shape: Multishot rays fanned
    /// across her aim, FanStep × the shape's spread apart (a cone, Hammerhead's, ConeStep° apart and ConeReach × as
    /// long), each as narrow as 1/√ of their count (no narrower than RayNarrowest) and worth a volley's share of her burn
    /// (PlaneCombatTuning.VolleyShot × the shape's damage); a wave (Double Helix) weaves them across her aim, ±WaveDeg
    /// at WaveTurn rad/s, out of step. Kraken Form adds KrakenRays − 1 short rays all round her (KrakenReach × as long,
    /// KrakenNarrow × as wide), each worth KrakenWorth of her beam.
    /// </summary>
    public const float FanStep = 1.5f, ConeStep = 15f, ConeReach = 0.85f, RayNarrowest = 0.4f, WaveDeg = 8f, WaveTurn = 3f;
    public const int KrakenRays = 8;
    public const float KrakenReach = 0.45f, KrakenNarrow = 0.5f, KrakenWorth = 0.15f;
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
    /// <summary>Flare (Starfish Arm): a cone widening along its length, this much as wide again at its reach (0: a plain cone).</summary>
    public float Flare;

    /// <summary>A cone's half-angle this far along it (wider toward its reach when it flares).</summary>
    public float HalfAt(float distance) => Flare <= 0f ? HalfAngle : HalfAngle * (1f + Flare * Math.Clamp(distance / MathF.Max(Range, 0.1f), 0f, 1f));
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
            float half = e.HalfAt(d);
            if (angle >= half) return 0f;
            float soft = half * LightTuning.ConeSoft;
            if (angle > soft)
            {
                float t = (angle - soft) / (half - soft);
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
        Func<Vector2, Vector2, bool>? sight, Func<LightEmitter, bool>? skip = null)
    {
        LightEmitter? best = null;
        float bestB = threshold;
        foreach (var e in emitters)
        {
            if (!e.Drinkable || !e.Shining || skip?.Invoke(e) == true) continue;
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
    LightEmitter? _aura, _beam, _rear, _sweep;
    float _sweepAngle;
    /// <summary>Her beam's other rays (a split beam's, Kraken Form's), made as they are first needed, and what each is worth.</summary>
    readonly List<LightEmitter> _rays = new();
    readonly List<float> _rayWorth = new();
    float _beamWorth = HerLightTuning.BeamShots;
    float _wavePhase;

    /// <summary>Every light emitter in the world (docs/LIGHT-AND-GRAPH.md §1).</summary>
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

    /// <summary>Rear Fin's beam behind her, and Lighthouse's turning round her (registered, dark until their pearls shine them).</summary>
    public LightEmitter RearBeam => _rear ??= Lights.Add(new LightEmitter
    {
        Owner = Her, Shape = LightShape.Cone, Intensity = LightTuning.BeamIntensity * LightTuning.RearIntensity, Active = false,
    });
    public LightEmitter SweepBeam => _sweep ??= Lights.Add(new LightEmitter
    {
        Owner = Her, Shape = LightShape.Cone, Intensity = LightTuning.BeamIntensity * LightTuning.SweepIntensity, Active = false,
    });

    /// <summary>
    /// Her beams and how many plain shots' burn each is worth: her own (BeamShots, or its share of them when it is split),
    /// its other rays, Rear Fin's and Lighthouse's (one each).
    /// </summary>
    public IEnumerable<(LightEmitter Beam, float Shots)> HerBeams
    {
        get
        {
            // (Her fire rate is its tick rate: each burns, seals and charges the faster.)
            float tick = BeamTick;
            // (Her own beam's rays by her rhythms: focus, spin-up, pulse. PlaneRhythm.cs.)
            float rhythm = RhythmBurn;
            yield return (Beam, _beamWorth * tick * rhythm);
            for (int i = 0; i < _rays.Count; i++) yield return (_rays[i], _rayWorth[i] * tick * rhythm);
            yield return (RearBeam, tick);
            yield return (SweepBeam, tick);
            // The rays born where her light lands on rock (PlanePaths.cs).
            for (int i = 0; i < _bornCount; i++) yield return (_born[i].Emitter, _born[i].Worth * tick);
        }
    }

    /// <summary>Her beam and its other rays (the ones along her aim and round her, not Rear Fin's nor Lighthouse's).</summary>
    public IEnumerable<LightEmitter> BeamRays
    {
        get
        {
            yield return Beam;
            foreach (var r in _rays) yield return r;
        }
    }

    /// <summary>The way she aims her beam (its rays fan round it), and how far either side of it the fan reaches (radians).</summary>
    public Vector2 BeamAim { get; private set; } = Vector2.UnitX;
    public float BeamSpread { get; private set; }

    /// <summary>
    /// The rays her beam splits into by her shot shape (BeamSplit's numbers): each one's angle off her aim, its width and
    /// reach against the beam's, and its worth against BeamShots. One ray at 0 unsplit; its first is her own beam (the
    /// middle one, or the first of an even fan).
    /// </summary>
    public static List<(float Offset, float Width, float Reach, float Worth)> RayLayout(ShotSpec shape, float time)
    {
        var rays = new List<(float, float, float, float)>();
        int n = Math.Max(1, shape.Multishot);
        bool cone = shape.Pattern == ShotPattern.Cone;
        float step = (cone ? LightTuning.ConeStep : shape.SpreadDeg * LightTuning.FanStep) * MathF.PI / 180f;
        float width = n == 1 ? 1f : MathF.Max(LightTuning.RayNarrowest, 1f / MathF.Sqrt(n));
        float reach = cone && n > 1 ? LightTuning.ConeReach : 1f;
        float worth = n == 1 ? shape.DamageMult : PlaneCombatTuning.VolleyShot(n) * shape.DamageMult;
        float wave = shape.Wave ? LightTuning.WaveDeg * MathF.PI / 180f * MathF.Sin(time * LightTuning.WaveTurn) : 0f;
        // Her own first: the middle (or the first of an even fan), then the rest out from it.
        var order = Enumerable.Range(0, n).OrderBy(i => MathF.Abs(i - (n - 1) / 2f)).ThenBy(i => i).ToList();
        foreach (int i in order)
        {
            float offset = (i - (n - 1) / 2f) * step;
            // A wave weaves them across her aim, each the other way from the last.
            if (shape.Wave) offset += i % 2 == 0 ? wave : -wave;
            rays.Add((offset, width, reach, worth));
        }
        if (n == 2 && shape.Wave)
        {
            // Two strands crossing: one at +wave, one at −wave (not side by side).
            rays[0] = (wave, width, reach, worth);
            rays[1] = (-wave, width, reach, worth);
        }
        if (shape.Pattern == ShotPattern.Kraken8)
            for (int k = 1; k < LightTuning.KrakenRays; k++)
                rays.Add((k * MathF.Tau / LightTuning.KrakenRays, LightTuning.KrakenNarrow, LightTuning.KrakenReach, LightTuning.KrakenWorth));
        return rays;
    }

    /// <summary>Where her beam's axis first meets rock (or its full reach): what it strikes, and where the view ends it.</summary>
    public Vector2 BeamEnd { get; private set; }

    /// <summary>
    /// Whether her beam reaches a body (on her plane at <paramref name="at"/>, <paramref name="height"/> metres off it, this
    /// radius): some of it within the beam's reach and cone, in sight of her.
    /// </summary>
    public bool BeamReaches(Vector2 at, float height, float radius) => BeamReaches(Beam, at, height, radius);

    /// <summary>As BeamReaches, for any of her beams.</summary>
    public bool BeamReaches(LightEmitter b, Vector2 at, float height, float radius)
    {
        if (!b.Shining) return false;
        var to = at - b.Position;
        float flat = to.Length();
        float d = MathF.Sqrt(flat * flat + height * height);
        if (d - radius > b.Range) return false;
        if (flat > radius)
        {
            float angle = MathF.Acos(Math.Clamp(Vector2.Dot(to / flat, b.Direction), -1f, 1f));
            float widen = MathF.Asin(MathF.Min(radius / flat, 1f));
            if (angle - widen > b.HalfAt(flat)) return false;
        }
        return LineOfSight(b.Position, at);
    }

    /// <summary>
    /// How much of a beam's light reaches a point (docs/LIGHT-AND-GRAPH.md §2.3), as shots' worth for one
    /// shot's: nothing outside its cone or past rock, nor past a creature's body in the way (bodies block it as rock
    /// does, unless her shots pierce: Swordfish Bill); else FarLight at the end of its reach rising to NearLight at her
    /// bell, a little less toward the cone's edge. Damage, sealing and seals' wear all go by it.
    /// </summary>
    public float BeamLight(LightEmitter b, Vector2 at, float height, float radius, object? self = null)
    {
        if (!BeamReaches(b, at, height, radius)) return 0f;
        if (BodyInTheWay(b.Position, at, radius, self)) return 0f;
        var to = at - b.Position;
        float flat = to.Length();
        float cone = 1f;
        if (flat > radius + 0.5f)
        {
            float angle = MathF.Acos(Math.Clamp(Vector2.Dot(to / flat, b.Direction), -1f, 1f));
            cone = 1f - 0.4f * Math.Clamp((angle / MathF.Max(b.HalfAt(flat), 1e-3f) - 0.6f) / 0.4f, 0f, 1f);
        }
        return BeamFalloff(b, MathF.Max(0f, flat - radius)) * cone;
    }

    /// <summary>
    /// The beam's light at a distance from her along it: NearLight at her bell to its carry at its reach (FarLight, raised
    /// or lowered by her shot speed).
    /// </summary>
    public float BeamFalloff(LightEmitter b, float distance)
    {
        float k = 1f - Math.Clamp(distance / MathF.Max(b.Range, 0.1f), 0f, 1f);
        float far = BeamCarry;
        return far + (LightTuning.NearLight - far) * MathF.Pow(k, 1.4f);
    }

    /// <summary>Her beam's tick rate: her fire rate against her base (1 with no pearls).</summary>
    public float BeamTick => MathF.Max(FireScale, 0.05f);

    /// <summary>Her beam's carry: the light it keeps at the end of its reach (FarLight × her shot speed against her base).</summary>
    public float BeamCarry => Math.Clamp(LightTuning.FarLight * ShotSpeedScale, LightTuning.LeastCarry, LightTuning.MostCarry);

    /// <summary>
    /// Her beam's light pressure on a creature (m/s, away from her): Pressure for each × of her knockback past her own,
    /// as much as her beams' light reaches it (a share of NearLight). None with no knockback pearl.
    /// </summary>
    public Vector2 LightPressure(object kin, Vector2 at, float radius)
    {
        float past = Run.Loadout.Stats[Stat.Knockback] - 1f;
        if (past <= 0f || Defeated) return Vector2.Zero;
        var push = Vector2.Zero;
        foreach (var (beam, _) in HerBeams)
        {
            float light = BeamLight(beam, at, 0f, radius, kin);
            if (light <= 0f) continue;
            push += SafeNormalize(at - beam.Position) * (LightTuning.Pressure * past * MathF.Min(light / LightTuning.NearLight, 1f));
        }
        return push;
    }

    /// <summary>The creatures' bodies that stand in her light's way this step (where they are, how big).</summary>
    readonly List<(object Kin, Vector2 At, float R)> _bodies = new();

    /// <summary>The bodies in her light's way, gathered once a step (none while her shots pierce).</summary>
    void GatherBodies()
    {
        _bodies.Clear();
        if (Run.Loadout.Shot.Pierce) return;
        foreach (var m in Murklings)
            if (m.Alive && !m.Budding && !m.Clinging) _bodies.Add((m, m.Position, m.Body));
        foreach (var w in Inkwardens)
            if (w.Alive) _bodies.Add((w, w.Position, WardenTuning.Radius));
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f) _bodies.Add((b, b.Position, BlotterTuning.Radius));
        foreach (var t in Thumpsquirts)
            if (t.Alive) _bodies.Add((t, t.Position, ThumpTuning.Radius));
        foreach (var l in Lashjaws)
            if (l.Alive) _bodies.Add((l, l.Position, LashTuning.Head));
        if (Boss is { Stage: BossStage.Fight, Aloft: false, Hidden: false } boss) _bodies.Add((boss, boss.Position, boss.Radius * 0.8f));
    }

    /// <summary>Whether a creature's body stands between her light at <paramref name="from"/> and a point (not the point's own).</summary>
    bool BodyInTheWay(Vector2 from, Vector2 at, float radius, object? self)
    {
        if (_bodies.Count == 0) return false;
        var line = at - from;
        float len = line.Length();
        if (len < 1e-3f) return false;
        var dir = line / len;
        foreach (var (kin, p, r) in _bodies)
        {
            if (ReferenceEquals(kin, self)) continue;
            float along = Vector2.Dot(p - from, dir);
            // In front of her, short of the point's own body.
            if (along <= r || along >= len - radius) continue;
            float across = MathF.Abs((p.X - from.X) * dir.Y - (p.Y - from.Y) * dir.X);
            if (across < r * 0.85f) return true;
        }
        return false;
    }

    /// <summary>Whether any beam of hers reaches this creature (none past a body in the way): it shows in it.</summary>
    public bool BeamSees(object kin, Vector2 at, float radius)
    {
        foreach (var (beam, _) in HerBeams)
            if (BeamLight(beam, at, 0f, radius, kin) > 0f) return true;
        // (Her pools of light too: Linger; and her fireflies.)
        return (Pools.Count > 0 && PoolSees(at, radius)) || (Fireflies.Count > 0 && FireflySees(at, radius));
    }

    /// <summary>
    /// Where a beam of hers stops along its axis: at rock, or short of the first creature's body across it (the near side
    /// of its middle), unless her light pierces. Its fan is drawn to here.
    /// </summary>
    public Vector2 BeamStop(LightEmitter b)
    {
        var from = b.Position;
        var end = b == Beam ? BeamEnd : StopOf(b);
        float len = Vector2.Distance(from, end);
        foreach (var (_, p, r) in _bodies)
        {
            float along = Vector2.Dot(p - from, b.Direction);
            if (along <= 0f || along - r * 0.4f >= len) continue;
            float across = MathF.Abs((p.X - from.X) * b.Direction.Y - (p.Y - from.Y) * b.Direction.X);
            if (across < r * 0.85f) len = MathF.Max(0.5f, along - r * 0.4f);
        }
        return from + b.Direction * len;
    }

    /// <summary>
    /// How far one line of her light gets from a point along a way before something stops it (its full reach if nothing):
    /// rock, a sealed wall or a pot (found to a few centimetres), or a creature's body across it (unless her light
    /// pierces). Her beam is drawn as a fan of such lines, so its end follows the edge of whatever it falls on, and what
    /// is not in the way of a line lets it on past (BeamView). <paramref name="hit"/>: something stopped it.
    /// </summary>
    public float LineReach(Vector2 from, Vector2 dir, float range, out bool hit)
    {
        hit = false;
        float len = range;
        // Rock, coarse then narrowed down.
        const float step = 0.35f;
        float open = 0f;
        for (float t = step; t < range; t += step)
        {
            if (Stops(from + dir * t))
            {
                float lo = open, hi = t;
                for (int k = 0; k < 5; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Stops(from + dir * mid)) hi = mid;
                    else lo = mid;
                }
                len = lo;
                hit = true;
                break;
            }
            open = t;
        }
        // The nearest body across it, where the line meets its rim.
        foreach (var (_, p, r) in _bodies)
        {
            float rr = r * 0.85f;
            var to = p - from;
            float along = Vector2.Dot(to, dir);
            if (along <= 0f) continue;
            float across2 = to.LengthSquared() - along * along;
            if (across2 >= rr * rr) continue;
            float meet = along - MathF.Sqrt(rr * rr - across2);
            if (meet > 0.3f && meet < len)
            {
                len = meet;
                hit = true;
            }
        }
        return len;
    }

    /// <summary>The creatures' bodies standing in her light's way this step (where, how big), for drawing their shadows in her beam.</summary>
    public IReadOnlyList<(object Kin, Vector2 At, float R)> LightBodies => _bodies;

    /// <summary>Whether her light is stopped at a point of her plane (rock, a sealed wall's rock, a pot).</summary>
    public bool StopsLight(Vector2 q) => Stops(q);

    bool Stops(Vector2 q) => !Map.IsOpen(q) || _rocks.Exists(r => r.Contains(q)) || VaseAt(q, 0.05f) is not null;

    /// <summary>Her beam's other ray at this index, made (registered with her as its owner, dark) when first needed.</summary>
    LightEmitter Ray(int index)
    {
        while (_rays.Count <= index)
        {
            _rays.Add(Lights.Add(new LightEmitter { Owner = Her, Shape = LightShape.Cone, Intensity = LightTuning.BeamIntensity, Active = false }));
            _rayWorth.Add(0f);
        }
        return _rays[index];
    }

    /// <summary>Where a beam of hers first meets rock along its axis (its full reach if nothing).</summary>
    public Vector2 StopOf(LightEmitter beam) => FirstBlocked(beam.Position, beam.Position + beam.Direction * beam.Range, 0.1f);

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
        a.Active = !Defeated && !GlowOff;
    }

    /// <summary>
    /// Her glow put out (docs/LIGHT-AND-GRAPH.md §2.4): no aura round her. Dark, and her beam not shining, she
    /// is hidden (Hidden): the Leak's creatures lose her, as in her Ink Cloud. But she sees little more than they do, and
    /// her inner light seals no leak.
    /// </summary>
    public bool GlowOff;

    /// <summary>Her glow out and her beam dark: no light of hers for anything to find her by.</summary>
    public bool Hidden => GlowOff && !BeamOn && !Defeated;

    /// <summary>
    /// The registry kept in step with the world: her aura on her, her beam along her aim while it is held, an emitter for
    /// each shot of hers that shines.
    /// </summary>
    void SyncLights(in PlaneInput input)
    {
        _ = Aura;
        PlaceAura();
        // Her fireflies round her (PlaneFireflies.cs).
        SyncFireflies();
        var beam = Beam;
        var shape = Run.Loadout.Shot;
        beam.Position = Player.Position;
        var aim = input.Aim.LengthSquared() > 1e-6f ? input.Aim : Player.Aim;
        if (aim.LengthSquared() > 1e-6f) beam.Direction = Vector2.Normalize(aim);
        beam.Range = LightTuning.BeamReach * MathF.Sqrt(MathF.Max(Run.Loadout.Stats[Stat.Glow], 0.1f)) * MathF.Sqrt(MathF.Max(RangeScale, 0.1f))
            * (shape.Laser ? LightTuning.SunbeamReach : 1f);
        // Her rhythms (PlaneRhythm.cs): focus narrows her beam.
        StepRhythm(input.Beam && !Defeated, beam.Direction);
        float halfDeg = shape.Laser ? LightTuning.SunbeamHalfAngleDeg
            : MathF.Min(LightTuning.BeamHalfAngleDeg * MathF.Sqrt(MathF.Max(Run.Loadout.Stats[Stat.ShotSize], 0.1f)), LightTuning.MaxHalfAngleDeg);
        float half = halfDeg * MathF.PI / 180f * MathF.Sqrt(MathF.Max(shape.SizeMult, 0.1f)) * FocusWidth;
        float range = beam.Range;
        beam.Active = input.Beam && !Defeated;
        // Split into its rays (her shot shape: Triple Tentacle, Hammerhead, Double Helix, Plankton Swarm, Kraken Form).
        BeamAim = beam.Direction;
        if (shape.Wave) _wavePhase += Dt;
        var layout = RayLayout(shape, _wavePhase);
        float spread = 0f;
        for (int i = 0; i < layout.Count; i++)
        {
            var (offset, width, reach, worth) = layout[i];
            var ray = i == 0 ? beam : Ray(i - 1);
            ray.Position = beam.Position;
            ray.Direction = Rotate(BeamAim, offset);
            ray.HalfAngle = half * width;
            ray.Range = range * reach;
            if (i == 0) _beamWorth = HerLightTuning.BeamShots * worth;
            else
            {
                ray.Active = beam.Active;
                _rayWorth[i - 1] = HerLightTuning.BeamShots * worth;
            }
            if (reach >= 1f) spread = MathF.Max(spread, MathF.Abs(offset) + half * width);
        }
        for (int k = layout.Count - 1; k < _rays.Count; k++) _rays[k].Active = false;
        // Lean and Flare (PlaneOpenPaths.cs): her rays turned toward the nearest creature, widening along their length.
        LeanRays(layout.Count);
        BeamSpread = spread;
        // Her colours dealt across the rays (the fan first, the Kraken's rays round her after it).
        DealHues(layout, shape.Pattern == ShotPattern.Kraken8 ? layout.Count - (LightTuning.KrakenRays - 1) : layout.Count);
        // Rear Fin: a fainter beam out of her back while her beam shines.
        var rear = RearBeam;
        rear.Position = beam.Position;
        rear.Direction = -beam.Direction;
        rear.Range = beam.Range * LightTuning.RearReach;
        rear.HalfAngle = beam.HalfAngle;
        rear.Active = beam.Active && shape.Rear;
        // Lighthouse: a fainter beam turning slowly round her, always.
        var sweep = SweepBeam;
        bool lighthouse = Run.Loadout.Has("lighthouse");
        if (lighthouse) _sweepAngle = (_sweepAngle + LightTuning.SweepTurn * Dt) % MathF.Tau;
        sweep.Position = beam.Position;
        sweep.Direction = new Vector2(MathF.Cos(_sweepAngle), MathF.Sin(_sweepAngle));
        sweep.Range = beam.Range * LightTuning.SweepReach;
        sweep.HalfAngle = beam.HalfAngle;
        sweep.Active = lighthouse && !Defeated;
        BeamEnd = beam.Active ? FirstBlocked(beam.Position, beam.Position + beam.Direction * beam.Range, 0.1f) : beam.Position;
        // Her rays born where her light lands on rock (Bounce, Fork, Return, Ghost).
        SyncPaths();
        // The Anglerfish Lure: a shot of hers left hanging is a decoy, brighter than her.
        bool lure = Run.Loadout.Count("anglerfish_lure") > 0;
        foreach (var s in Shots)
        {
            if (!s.FromPlayer || s.BossPearl || s.Life <= 0f) continue;
            if (!_shotLights.TryGetValue(s, out var e))
                _shotLights[s] = e = Lights.Add(new LightEmitter { Owner = new ShotLightOwner(s) });
            e.Position = s.Position;
            e.Range = ShotLightReach(s);
            e.Intensity = MathF.Min(0.4f + 0.06f * s.Bubbles, 1.3f) * s.Dim * (lure && s.Rest > 0f ? LightTuning.LureBright : 1f);
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
        // Compacted, so its order is the order it was filled in (as a level put back from a save fills it): a dictionary
        // reuses the slots of what it lost, and a saved level must carry on exactly.
        var kept = _shotLights.ToList();
        _shotLights.Clear();
        foreach (var (s, e) in kept) _shotLights[s] = e;
    }
}
