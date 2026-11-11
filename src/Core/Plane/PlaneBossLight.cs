using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Bosses;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light on a boss (docs/LIGHT-AND-GRAPH.md §5.2). Her shots burn its pearl while it is open, as ever. Her
/// beams burn it too, and each folk boss asks one thing more of the beam:
/// - Slam and Sigh (Tull): **hold it steady.** The beam burns her pearl only once it has stayed on it HoldSeconds.
/// - Cleaning dance (Kip): **from the side.** The beam burns her pearl only meeting it within 45° of square-on to her
///   flank (|cos| of its way against her facing at most FlankCos).
/// - The hidden one (Fennel): **find him.** Any beam falling on him in his kelp finds him (as a shot does).
/// - The treasure (Ottilie): **a brighter light.** Two lamp-shells in her hoard (light emitters, LampDim faint). Lit by
///   the beam, one holds her light LampGlow seconds, LampBright bright (brighter than Clementine), and Ottilie turns to
///   admire it (LampTurn rad/s), carrying her pearl round with her: her body no longer shields it from Clementine.
/// - Boil heart (the Pale Dragonet): **the beam's heart.** Her sacs burn only under the middle of her own beam (within
///   CoreShare of its half-angle), not its edge, nor Rear Fin's or Lighthouse's.
/// </summary>
public static class BossLightTuning
{
    public const float HoldSeconds = 0.5f, FlankCos = 0.7071f, CoreShare = 0.5f;
    public const float LampGlow = 3f, LampBright = 2f, LampDim = 0.15f, LampReach = 6f, LampTurn = 1.2f, LampOut = 0.7f;
    /// <summary>How near the beam's cone must come to a lamp-shell to light it.</summary>
    public const float LampRadius = 0.6f;
}

/// <summary>A lamp-shell in Ottilie's hoard: where it lies, how long it holds her light still, its light.</summary>
public sealed class BossLamp
{
    public Vector2 Position;
    public float Glow;
    public LightEmitter Light = null!;
    public bool Lit => Glow > 0f;
}

public sealed partial class PlaneBoss
{
    /// <summary>Tull: how long a beam has stayed on her pearl without leaving it.</summary>
    public float BeamHold;
    /// <summary>Ottilie's lamp-shells (none for the others).</summary>
    public readonly List<BossLamp> Lamps = new();
    /// <summary>The lamp-shell she is admiring now (null: none glows).</summary>
    public BossLamp? Admiring;
}

public sealed partial class PlaneWorld
{
    /// <summary>Ottilie's two lamp-shells, either side behind her as she lies (round her hoard).</summary>
    void PlaceLamps(PlaneBoss boss)
    {
        if (boss.Spec.Concept != BossConcept.TheTreasure) return;
        float r = ArenaRadius * BossLightTuning.LampOut;
        foreach (float turn in new[] { 2.35f, -2.35f })
        {
            var at = ArenaCenter + Rotate(boss.Facing, turn) * r;
            for (int k = 0; k < 6 && !Clear(at, 0.6f); k++) at = Vector2.Lerp(at, ArenaCenter, 0.2f);
            var light = Lights.Add(new LightEmitter
            {
                Owner = new LightPool(float.MaxValue), Position = at, Range = BossLightTuning.LampReach, Intensity = BossLightTuning.LampDim, Drinkable = false,
            });
            boss.Lamps.Add(new BossLamp { Position = at, Light = light });
        }
    }

    /// <summary>Her light on the boss this step: the hider found, the lamps lit, the sacs and the pearl burned by its rules.</summary>
    void StepBossLight(PlaneBoss boss, float burn)
    {
        StepLamps(boss);
        if (boss.Aloft)
        {
            boss.BeamHold = 0f;
            return;
        }
        if (boss.Hidden)
        {
            foreach (var (beam, _) in HerBeams)
                if (BeamReaches(beam, boss.Position, 0f, boss.Radius))
                {
                    Find(boss);
                    break;
                }
            if (boss.Hidden) return;
        }
        bool core = boss.Spec.Concept == BossConcept.BoilHeart;
        foreach (var sac in boss.Sacs)
        {
            if (!sac.Alive) continue;
            var at = boss.SacAt(sac);
            float reach = PlaneBossTuning.SacRadius * (0.6f + sac.Site.Size);
            float n = 0f;
            foreach (var (beam, worth) in HerBeams)
            {
                if (core && (beam != Beam || !InCore(beam, at))) continue;
                n += worth * BeamLight(beam, at, 0f, reach, boss);
            }
            if (n <= 0f) continue;
            BeamBurns = true;
            DamageSac(boss, sac, burn * n, SafeNormalize(at - Player.Position), quiet: true);
        }
        float shots = boss.Open ? Touch(boss, boss.LightAt, 0f, boss.LightReach, dazzles: false, beams: false) : 0f;
        float beams = BeamOnPearl(boss);
        if (shots + beams > 0) DamageBoss(boss, burn * (shots + beams), Vector2.Zero, quiet: true);
    }

    /// <summary>Whether a point lies within the heart of this beam (CoreShare of its half-angle round its axis).</summary>
    static bool InCore(LightEmitter beam, Vector2 at)
    {
        var to = at - beam.Position;
        float d = to.Length();
        if (d < 1e-3f) return true;
        float angle = MathF.Acos(Math.Clamp(Vector2.Dot(to / d, beam.Direction), -1f, 1f));
        return angle <= beam.HalfAngle * BossLightTuning.CoreShare;
    }

    /// <summary>How many shots' worth her beams burn the boss's pearl this step (as much as their light reaches it), by its rule (0 while it is shut).</summary>
    float BeamOnPearl(PlaneBoss boss)
    {
        if (!boss.Open)
        {
            boss.BeamHold = 0f;
            return 0f;
        }
        float n = 0f;
        foreach (var (beam, worth) in HerBeams)
        {
            float light = BeamLight(beam, boss.LightAt, 0f, boss.LightReach, boss);
            if (light <= 0f) continue;
            // A shielded pearl held behind the body: the body stops a beam coming through it (all but in the window), as
            // it stops a shot.
            if (boss.Spec.Shielded && boss.Act != BossAct.Window
                && Vector2.Dot(SafeNormalize(boss.LightAt - beam.Position), SafeNormalize(boss.LightAt - boss.Position)) > 0.3f) continue;
            if (boss.Spec.Concept == BossConcept.CleaningDance)
            {
                var way = SafeNormalize(boss.LightAt - beam.Position);
                if (MathF.Abs(Vector2.Dot(way, boss.Facing)) > BossLightTuning.FlankCos) continue;
            }
            n += worth * light;
        }
        if (boss.Spec.Concept == BossConcept.SlamAndSigh)
        {
            boss.BeamHold = n > 0f ? boss.BeamHold + Dt : 0f;
            if (boss.BeamHold < BossLightTuning.HoldSeconds) return 0f;
        }
        if (n > 0f) BeamBurns = true;
        return n;
    }

    /// <summary>Ottilie's lamp-shells: her beam lights one; lit, it holds her light a while, and she turns to admire the brightest.</summary>
    void StepLamps(PlaneBoss boss)
    {
        if (boss.Lamps.Count == 0) return;
        BossLamp? brightest = null;
        foreach (var lamp in boss.Lamps)
        {
            if (!Defeated && BeamReaches(Beam, lamp.Position, 0f, BossLightTuning.LampRadius))
            {
                if (!lamp.Lit) Events.Add(new PlaneEvent(PlaneEventType.LampLit, lamp.Position, Vector2.Zero));
                lamp.Glow = BossLightTuning.LampGlow;
            }
            else lamp.Glow = MathF.Max(0f, lamp.Glow - Dt);
            lamp.Light.Intensity = lamp.Lit && !boss.Freed ? BossLightTuning.LampBright : BossLightTuning.LampDim;
            if (lamp.Lit && (brightest is null || lamp.Glow > brightest.Glow)) brightest = lamp;
        }
        boss.Admiring = boss.Stage == BossStage.Fight ? brightest : null;
    }
}
