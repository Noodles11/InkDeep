using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her great lights (docs/LIGHT-AND-GRAPH.md §16, the light grammar's seventh words).
/// <list type="bullet">
/// <item>Lance (Sunbeam): her beam LanceReach × as long, LanceWidth × as wide, its carry full (MostCarry).</item>
/// <item>Orbit (Nautilus Spiral): a fainter beam turning round her at OrbitTurn rad/s, always (OrbitIntensity as
///   bright, OrbitReach × her beam's reach, as wide, OrbitShots of a shot's burn).</item>
/// <item>Lighthouse (Lance + Orbit): the turning beam is a full Lance (her beam's reach, width, carry and worth).</item>
/// <item>Neon Rave (three glow pearls): her light is each of the seven colours in turn, NeonEvery seconds each.</item>
/// <item>Coral Reef (three coral pearls): her light lingers (Linger), and standing in a pool of it mends her
///   ReefMend HP a second.</item>
/// <item>Shark Mode (three predator pearls): her beam SharkNarrow × as wide, burning SharkHeat ×; what it frees sends
///   her into the Frenzy: her beam burns FrenzyBurn × for FrenzyTime seconds.</item>
/// <item>Pirate (three galleon pearls): gilded light: shells her beams reach fly to her; what it frees drops
///   GildShells more.</item>
/// <item>Black Sun (a Crab Deal pearl): the shadow a creature casts down her beam burns what stands in it, as hard
///   as the light would have.</item>
/// </list>
/// </summary>
public static class GreatLightTuning
{
    public const float LanceReach = 1.6f, LanceWidth = 0.5f;
    public const float OrbitIntensity = 0.5f, OrbitReach = 0.7f, OrbitTurn = 0.6f, OrbitShots = 3f;
    public const float NeonEvery = 0.8f;
    public const float ReefMend = 3f;
    public const float SharkNarrow = 0.75f, SharkHeat = 1.3f, FrenzyBurn = 1.5f, FrenzyTime = 3f;
    public const int GildShells = 2;

    /// <summary>Neon Rave's colours in the order it turns through them.</summary>
    public static readonly LightHue[] Neon =
    {
        LightHue.Red, LightHue.Yellow, LightHue.Green, LightHue.Cyan, LightHue.Blue, LightHue.Magenta, LightHue.White,
    };
}

public sealed partial class PlaneWorld
{
    /// <summary>The Frenzy (Shark Mode): seconds left.</summary>
    public float FrenzyLeft { get; private set; }

    /// <summary>Whether she is standing in her own afterlight with Coral Reef (mending).</summary>
    public bool Mending { get; private set; }

    /// <summary>Her great lights' burn on her own beam's rays: Shark Mode's heat and its Frenzy.</summary>
    float GreatBurn =>
        (Run.Loadout.Has("shark_mode") ? GreatLightTuning.SharkHeat : 1f) * (FrenzyLeft > 0f ? GreatLightTuning.FrenzyBurn : 1f);

    /// <summary>Lighthouse: her turning beam a full Lance.</summary>
    bool Lighthouse => Run.Loadout.Has("lighthouse");

    /// <summary>Neon Rave's colour now (none without it).</summary>
    public LightHue NeonHue => Run.Loadout.Has("neon_rave")
        ? GreatLightTuning.Neon[(int)(Time / GreatLightTuning.NeonEvery) % GreatLightTuning.Neon.Length]
        : LightHue.None;

    /// <summary>Her great lights stepped (SyncLights, before her beam is shaped): the Frenzy runs down; Coral Reef mends.</summary>
    void StepGreatLights()
    {
        FrenzyLeft = MathF.Max(0f, FrenzyLeft - Dt);
        Mending = false;
        if (!Run.Loadout.Has("coral_reef") || Defeated || Pools.Count == 0) return;
        var p = Player;
        foreach (var pool in Pools)
        {
            if (Vector2.Distance(pool.At, p.Position) > OpenPathTuning.PoolReach + Radius) continue;
            Mending = true;
            if (p.Hp < Run.MaxHp)
            {
                p.Hp = MathF.Min(Run.MaxHp, p.Hp + GreatLightTuning.ReefMend * Dt);
                Run.Hp = p.Hp;
            }
            return;
        }
    }

    /// <summary>Her turning beam (Orbit, Lighthouse) placed (SyncLights, her beam shaped).</summary>
    void PlaceOrbit(LightEmitter beam, bool orbit)
    {
        var sweep = SweepBeam;
        if (orbit) _sweepAngle = (_sweepAngle + GreatLightTuning.OrbitTurn * Dt) % MathF.Tau;
        sweep.Position = beam.Position;
        sweep.Direction = new Vector2(MathF.Cos(_sweepAngle), MathF.Sin(_sweepAngle));
        bool full = Lighthouse;
        sweep.Range = beam.Range * (full ? 1f : GreatLightTuning.OrbitReach);
        sweep.HalfAngle = beam.HalfAngle;
        sweep.Intensity = LightTuning.BeamIntensity * (full ? 1f : GreatLightTuning.OrbitIntensity);
        sweep.Carry = full ? beam.Carry : 0f;
        sweep.Active = orbit && !Defeated;
    }

    /// <summary>What her turning beam is worth (shots): Orbit's, or Lighthouse's full Lance.</summary>
    float OrbitWorth => Lighthouse ? HerLightTuning.BeamShots : GreatLightTuning.OrbitShots;

    /// <summary>A creature her light freed (FreedInColour): Shark Mode's Frenzy; Pirate's gilded drop.</summary>
    void FreedGreat(Vector2 at)
    {
        var s = Run.Loadout;
        if (s.Has("shark_mode"))
        {
            if (FrenzyLeft <= 0f) Events.Add(new PlaneEvent(PlaneEventType.Frenzy, Player.Position, Vector2.Zero));
            FrenzyLeft = GreatLightTuning.FrenzyTime;
        }
        if (s.Has("pirate"))
            for (int i = 0; i < GreatLightTuning.GildShells; i++) Shells.Add(new PlaneShell { Position = at, Velocity = Fling(2f, 4f) });
    }

    /// <summary>Pirate: whether her gilded light reaches a shell here (it flies to her).</summary>
    bool Gilded(Vector2 at)
    {
        if (!Run.Loadout.Has("pirate") || Defeated) return false;
        foreach (var (beam, _) in HerBeams)
            if (BeamReaches(beam, at, 0f, 0.15f)) return true;
        return false;
    }

    /// <summary>
    /// Black Sun: how much of a beam's light the shadow of a body in its way burns a creature with (0: not in a
    /// shadow, or no Black Sun): as much as the light would have.
    /// </summary>
    float ShadeLight(LightEmitter b, Vector2 at, float height, float radius, object? self)
    {
        if (!Has("blackSun") || !BeamReaches(b, at, height, radius) || !BodyInTheWay(b.Position, at, radius, self)) return 0f;
        return OpenLight(b, at, radius);
    }
}
