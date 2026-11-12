using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light's rhythms (docs/LIGHT-AND-GRAPH.md §12, the light grammar's third words): what happens while she holds her
/// beam. They scale what her own beam and its rays burn (and so what the rays born of them carry).
/// <list type="bullet">
/// <item>Focus (Pearl Diver): held steady (turning under SteadyDeg a second), it builds over FocusTime to burn
///   × FocusBurn, its cone narrowing to FocusNarrow of its width; turning faster, it loses it LoseRate × as fast; let
///   go, it is gone.</item>
/// <item>Spin-up (Sardine Run): the longer it is held without a break, the stronger: × SpinBurn after SpinTime; let
///   go, it spins down over SpinDown.</item>
/// <item>Snap (Pistol Shrimp Claw): while her beam's axis ends on a creature, every SnapEvery a snap of light bursts
///   there, SnapReach wide, SnapShots shots' burn, pushing what it catches back.</item>
/// <item>Pulse (Glow Burst, an active): for its duration the beam beats every Beat, each beat × its value at its
///   peak.</item>
/// </list>
/// Named when they meet: Pearl Necklace (Focus + Fork: at full focus the tip opens into a ring of NecklaceRays rays) and
/// Depth Charge (Focus + Snap: at full focus the snap is DepthReach × as wide, DepthBurn × as strong).
/// </summary>
public static class RhythmTuning
{
    public const float FocusTime = 1.5f, FocusBurn = 3f, FocusNarrow = 0.45f, SteadyDeg = 30f, LoseRate = 4f;
    public const float SpinTime = 3f, SpinBurn = 1.8f, SpinDown = 0.6f;
    public const float SnapEvery = 0.8f, SnapReach = 2.5f, SnapShots = 1.5f;
    public const float Beat = 0.5f, BeatSharp = 6f;
    public const int NecklaceRays = 8;
    public const float NecklaceShare = 0.12f, NecklaceReach = 0.45f;
    public const float DepthReach = 2f, DepthBurn = 1.5f;
}

public sealed partial class PlaneWorld
{
    /// <summary>Her beam's focus (Pearl Diver), how far it has spun up (Sardine Run), both 0..1.</summary>
    public float Focus { get; private set; }
    public float SpinUp { get; private set; }
    /// <summary>Glow Burst's pulse: seconds left, its peak, and how far into its beat (0 on the beat).</summary>
    public float PulseLeft { get; private set; }
    float _pulsePeak = 2f, _pulseClock, _snapClock;
    Vector2 _focusAim = Vector2.UnitX;

    /// <summary>The pulse now: 0 between beats, 1 on one (sharp, fading).</summary>
    public float PulseBeat => PulseLeft > 0f ? MathF.Exp(-RhythmTuning.BeatSharp * (_pulseClock % RhythmTuning.Beat) / RhythmTuning.Beat * 2f) : 0f;

    /// <summary>What her rhythms make her own beam's rays burn now, × their worth.</summary>
    public float RhythmBurn =>
        (1f + (RhythmTuning.FocusBurn - 1f) * Focus) * (1f + (RhythmTuning.SpinBurn - 1f) * SpinUp) * (1f + (_pulsePeak - 1f) * PulseBeat);

    /// <summary>Her focus's narrowing of her beam's cone now (1: none).</summary>
    public float FocusWidth => 1f - (1f - RhythmTuning.FocusNarrow) * Focus;

    /// <summary>Her rhythms stepped (SyncLights, before her beam is shaped): held is whether her beam is on, aim its way.</summary>
    void StepRhythm(bool held, Vector2 aim)
    {
        var s = Run.Loadout;
        if (PulseLeft > 0f)
        {
            PulseLeft = MathF.Max(0f, PulseLeft - Dt);
            _pulseClock += Dt;
        }
        // Focus: steady builds it, a swing spends it, letting go loses it.
        if (s.Shot.Charge && held)
        {
            float turned = MathF.Acos(Math.Clamp(Vector2.Dot(_focusAim, aim), -1f, 1f)) * 180f / MathF.PI / MathF.Max(Dt, 1e-4f);
            // (String of Pearls: spun right up, a swing does not break it.)
            bool steady = turned <= RhythmTuning.SteadyDeg || (SpinUp >= 1f && s.Has("string_of_pearls"));
            float was = Focus;
            Focus = steady ? MathF.Min(1f, Focus + Dt / RhythmTuning.FocusTime) : MathF.Max(0f, Focus - RhythmTuning.LoseRate * Dt / RhythmTuning.FocusTime);
            if (was < 1f && Focus >= 1f) Events.Add(new PlaneEvent(PlaneEventType.ChargeFull, Player.Position, aim));
        }
        else Focus = 0f;
        _focusAim = aim;
        // Spin-up: held unbroken, it builds; let go, it spins down.
        if (s.Flags.Contains("minigun"))
            SpinUp = held ? MathF.Min(1f, SpinUp + Dt / RhythmTuning.SpinTime) : MathF.Max(0f, SpinUp - Dt / RhythmTuning.SpinDown);
        else SpinUp = 0f;
    }

    /// <summary>Glow Burst: her beam beats for so long, each beat × peak.</summary>
    void BeginPulse(float duration, float peak)
    {
        PulseLeft = duration;
        _pulsePeak = MathF.Max(peak, 1f);
        _pulseClock = 0f;
    }

    /// <summary>
    /// Pistol Shrimp Claw (after her light's step, her beam placed): while her beam's axis ends on a creature, a snap
    /// every SnapEvery there. Depth Charge at full focus: wider and stronger.
    /// </summary>
    void StepSnap()
    {
        if (!Run.Loadout.Flags.Contains("cavitation") || !BeamOn)
        {
            _snapClock = 0f;
            return;
        }
        var end = BeamStop(Beam);
        if (!OnABody(end))
        {
            _snapClock = MathF.Min(_snapClock, RhythmTuning.SnapEvery * 0.5f);
            return;
        }
        _snapClock += Dt;
        if (_snapClock < RhythmTuning.SnapEvery) return;
        _snapClock -= RhythmTuning.SnapEvery;
        bool depth = Run.Loadout.Has("depth_charge") && Focus >= 1f;
        float reach = RhythmTuning.SnapReach * (depth ? RhythmTuning.DepthReach : 1f);
        float burn = PlaneCombatTuning.ShotDamage * DamageScale * RhythmTuning.SnapShots * (depth ? RhythmTuning.DepthBurn : 1f);
        var at = end + Beam.Direction * 0.3f;
        Events.Add(new PlaneEvent(PlaneEventType.Cavitation, at, Beam.Direction, reach));
        LightBurst(at, reach, burn);
        // What it catches is pushed back, away from her.
        foreach (var m in Murklings)
            if (m.Alive && !m.Budding && !m.Clinging && Vector2.Distance(m.Position, at) < reach + m.Radius)
                Shove(ref m.Knock, m.Position - Beam.Position, m.Radius);
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f && Vector2.Distance(b.Position, at) < reach + BlotterTuning.Radius)
                Shove(ref b.Knock, b.Position - Beam.Position, BlotterTuning.Radius);
    }

    /// <summary>Whether a creature's body is at (or just past) a point on her beam's axis.</summary>
    bool OnABody(Vector2 at)
    {
        foreach (var (_, p, r) in _bodies)
            if (Vector2.Distance(p, at) < r + 0.4f) return true;
        return false;
    }
}
