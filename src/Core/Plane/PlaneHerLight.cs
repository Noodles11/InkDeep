using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her searchlight (docs/ADVENTURE.md §8): her light is a lamp in her bell. What shines out through the bell all round her
/// is softened by it and burns nothing; what shines out of the bell's opening is a beam, intense, and it burns. The beam
/// leaves her bell the way its opening points: straight down at rest, and as she swims (tipped apex first, Tilt for her
/// pace, at most TiltMax) out behind her. Every corrupted creature some of whose body is inside the beam (BeamAngle either
/// side of its middle, a fan twice as tall as it is wide (BeamFlat), out to BeamReach, × √ her glow stat: Lantern Pearl
/// reaches farther), with nothing solid between,
/// slowly loses corruption, Burn a second (× her damage stat), and is freed when it has none left; a guarding Inkwarden
/// turns the burn aside as it turns her shots. Coming into the beam dazzles a creature: it stops dead for Dazzle seconds
/// (a throw it was winding up is let go), then carries on; it can be dazzled again once it has been out of the beam Rearm
/// seconds. Murklings, Inkwardens and Hearthguards are dazzled; the older rooted and leaping creatures only burn, and a
/// boss only burns where it lies open to the beam.
/// </summary>
public static class HerLightTuning
{
    public const float BeamAngle = 25f, BeamReach = 10f, Burn = 3f;
    /// <summary>The beam is a fan, taller than it is wide: up and down count this much of an angle off its middle.</summary>
    public const float BeamFlat = 0.5f;
    public const float Dazzle = 0.8f, Rearm = 2f;
    /// <summary>Her tilt swimming: Tilt × √(her speed ÷ her cruise) radians off upright, at most TiltMax, eased in at TiltEase a second.</summary>
    public const float Tilt = 1.22f, TiltMax = 1.38f, TiltEase = 6f;
}

public sealed partial class PlaneWorld
{
    /// <summary>A creature's moments in her beam: in it now, dazzled for so long still, out of it for so long.</summary>
    sealed class Glare
    {
        public bool Lit;
        public float Frozen, Dark = HerLightTuning.Rearm;
        public long Seen;
    }

    readonly Dictionary<object, Glare> _glare = new();

    /// <summary>Tests: false turns her beam's burn and dazzle off, to watch a creature or a shot on its own.</summary>
    public bool LightBurns { get; set; } = true;

    /// <summary>How far her beam burns now (metres from her bell).</summary>
    public float BeamReach => HerLightTuning.BeamReach * MathF.Sqrt(MathF.Max(Run.Loadout.Stats[Stat.Glow], 0.1f));

    /// <summary>How far her bell is tipped off upright (radians), and the way along her plane its opening leans (away from her swim).</summary>
    public float BeamTilt { get; private set; }
    public Vector2 BeamWay { get; private set; } = Vector2.UnitY;

    /// <summary>The way her beam shines (x east, y up, z south; unit): down at rest, out behind her as she swims.</summary>
    public Vector3 BeamDirection => new(BeamWay.X * MathF.Sin(BeamTilt), -MathF.Cos(BeamTilt), BeamWay.Y * MathF.Sin(BeamTilt));

    /// <summary>Whether this creature is in her beam now (burning, unless warded).</summary>
    public bool InHerLight(object creature) => _glare.TryGetValue(creature, out var g) && g.Lit;

    /// <summary>Whether this creature is dazzled now (it holds still).</summary>
    public bool Dazzled(object creature) => _glare.TryGetValue(creature, out var g) && g.Frozen > 0f;

    /// <summary>Her bell tips as she swims, and its opening (her beam) with it.</summary>
    void StepBeam()
    {
        var v = Player.Velocity;
        float speed = v.Length();
        float pace = Math.Clamp(speed / MathF.Max(CruiseSpeed, 0.1f), 0f, 1.6f);
        float tilt = speed > 0.05f ? MathF.Min(MathF.Sqrt(pace) * HerLightTuning.Tilt, HerLightTuning.TiltMax) : 0f;
        BeamTilt += (tilt - BeamTilt) * (1f - MathF.Exp(-HerLightTuning.TiltEase * Dt));
        if (speed > 0.3f)
        {
            var way = Vector2.Lerp(BeamWay, -v / speed, 1f - MathF.Exp(-HerLightTuning.TiltEase * 2f * Dt));
            if (way.LengthSquared() > 1e-6f) BeamWay = Vector2.Normalize(way);
        }
    }

    /// <summary>Her beam on the corrupted in it: each one burns and, coming into it, is dazzled.</summary>
    void StepHerLight()
    {
        StepBeam();
        if (!LightBurns)
        {
            _glare.Clear();
            return;
        }
        float burn = HerLightTuning.Burn * DamageScale * Dt;
        for (int i = 0; i < Murklings.Count; i++)
        {
            var m = Murklings[i];
            if (!m.Alive || m.Budding || m.Clinging || !Touch(m, m.Position, 0f, m.Radius) || Guarded(m.Position)) continue;
            if (!m.Corruption.Sear(burn)) continue;
            Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, m.Position, Vector2.Zero, burn));
            BurstMurkling(m, ember: true);
        }
        for (int i = 0; i < Inkwardens.Count; i++)
        {
            var w = Inkwardens[i];
            if (!w.Alive || !Touch(w, w.Position, 0f, WardenTuning.Radius) || !w.Corruption.Sear(burn)) continue;
            Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, w.Position, Vector2.Zero, burn));
            FreeWarden(w);
        }
        foreach (var f in Flounders)
        {
            // A Hearthguard lies on its bed under her plane: the beam finds it as she passes over it.
            if (!f.Corrupted || f.Freed || !Touch(f, f.Position, FlounderTuning.Bed(Map, f.Home) - LevelMap.SwimBand, FlounderTuning.Radius) || Guarded(f.Position)
                || !f.Corruption.Sear(burn)) continue;
            Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, FlounderSeen(f), Vector2.Zero, burn));
            FreeFlounder(f);
        }
        if (Corruption is { } field)
        {
            for (int i = 0; i < Blotters.Count; i++)
            {
                var b = Blotters[i];
                if (!b.Alive || b.Rise > 0f || !Touch(b, b.Position, 0f, BlotterTuning.Radius, dazzles: false) || Guarded(b.Position) || !b.Corruption.Sear(burn)) continue;
                Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, b.Position, Vector2.Zero, burn));
                BurstBlotter(b, field);
            }
            for (int i = 0; i < Thumpsquirts.Count; i++)
            {
                var t = Thumpsquirts[i];
                if (!t.Alive || t.Airborne || !Touch(t, t.Position, 0f, ThumpTuning.Radius, dazzles: false) || Guarded(t.Position) || !t.Corruption.Sear(burn)) continue;
                Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, t.Position, Vector2.Zero, burn));
                BurstThump(t);
            }
            foreach (var l in Lashjaws)
            {
                if (!l.Alive || !Touch(l, l.Position, 0f, LashTuning.Head, dazzles: false) || Guarded(l.Position) || !l.Corruption.Sear(burn)) continue;
                if (l.Phase == 2) Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, l.Position, Vector2.Zero, burn));
                BreakLashjaw(l);
            }
            // An emptied Blightroot bursts on its own next step (its sac stands high on its pole).
            foreach (var root in Blightroots)
                if (root.Alive && Touch(root, root.Position, 1.9f, BlightTuning.SacRadius, dazzles: false) && !Guarded(root.Position)) root.Corruption.Sear(burn);
        }
        // A boss burns only where it lies open to the beam (its pearl, or Drippus Prime's whole body), and is never dazzled.
        if (Boss is { Stage: BossStage.Fight, Open: true } boss && Touch(boss, boss.LightAt, 0f, boss.LightReach, dazzles: false))
            DamageBoss(boss, burn, Vector2.Zero, quiet: true);
        // Creatures gone (freed, or the level left) are forgotten.
        if (Tick % 60 == 0)
        {
            var gone = new List<object>();
            foreach (var (kin, g) in _glare)
                if (Tick - g.Seen > 60) gone.Add(kin);
            foreach (var kin in gone) _glare.Remove(kin);
        }
    }

    /// <summary>
    /// Whether her beam reaches this creature (on her plane at <paramref name="at"/>, <paramref name="height"/> metres above
    /// it, this radius): some of its body inside the beam's cone and its reach, in sight of her. Dazzles it as it comes in.
    /// </summary>
    bool Touch(object kin, Vector2 at, float height, float radius, bool dazzles = true)
    {
        var her = Player.Position;
        bool lit = !Defeated && InBeam(at, height, radius) && LineOfSight(her, at);
        if (!_glare.TryGetValue(kin, out var g)) _glare[kin] = g = new Glare();
        g.Seen = Tick;
        g.Lit = lit;
        g.Frozen = MathF.Max(0f, g.Frozen - Dt);
        if (!lit)
        {
            g.Dark += Dt;
            return false;
        }
        if (dazzles && g.Dark >= HerLightTuning.Rearm)
        {
            g.Frozen = HerLightTuning.Dazzle;
            Events.Add(new PlaneEvent(PlaneEventType.Dazzled, at, SafeNormalize(at - her), radius));
        }
        g.Dark = 0f;
        return true;
    }

    /// <summary>Whether some of a body (on her plane at at, height above it, this radius) lies inside her beam's cone and reach.</summary>
    public bool InBeam(Vector2 at, float height, float radius)
    {
        var her = Player.Position;
        var to = new Vector3(at.X - her.X, height, at.Y - her.Y);
        float d = to.Length();
        if (d <= radius) return true;
        if (d - radius > BeamReach) return false;
        // The fan: up and down count for less of an angle than across.
        var flat = new Vector3(1f, HerLightTuning.BeamFlat, 1f);
        var way = Vector3.Normalize(BeamDirection * flat);
        float off = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(to * flat), way), -1f, 1f));
        return off <= HerLightTuning.BeamAngle * MathUtil.Deg2Rad + MathF.Asin(MathF.Min(radius / d, 1f));
    }

    /// <summary>An Inkwarden guards this spot: her beam's burn is turned aside there (silently, as it is steady).</summary>
    bool Guarded(Vector2 at)
    {
        foreach (var w in Inkwardens)
            if (w.Alive && Vector2.Distance(w.Position, at) <= WardenTuning.Reach) return true;
        return false;
    }
}
