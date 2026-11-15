using System;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The depth's danger (docs/DIVE.md, "The danger clock"): each layer of the cube reef starts a clock, unseen, the first
/// time she comes onto it, running while she is on it until its boss is freed. Its Minutes are cut into Steps equal
/// shares: in the first, murklings come as the Menace alone has them; each share after, they come faster and more of
/// them stay round her (RateStep and CapStep more a step), to the most at the last. Freed of its boss, the layer is back
/// to the first step for good, to be explored in peace. The HUD shows only the step, as exclamation marks (none at
/// first, four at the most).
/// </summary>
public static class DangerTuning
{
    /// <summary>How long the clock runs to the most danger (minutes), and the equal shares it is cut into.</summary>
    public static float Minutes { get; set; } = 10f;
    public const int Steps = 5;
    /// <summary>Each step past the first: murklings come this much more often, and this many more may stay round her (shares of the Menace's own).</summary>
    public const float RateStep = 0.3f, CapStep = 0.25f;
    /// <summary>A spot this dark (her light and every other added up there) is dark enough for a murkling to come out of.</summary>
    public const float Dark = 0.05f;

    /// <summary>The step the clock is at after this many seconds (0 to Steps − 1).</summary>
    public static int StepAt(float seconds) => Math.Clamp((int)(seconds / (Minutes * 60f / Steps)), 0, Steps - 1);
}

public sealed partial class PlaneWorld
{
    /// <summary>The danger's step on this layer (DangerTuning; set by the game from its clock): 0 calm, Steps − 1 the most.</summary>
    public int Danger { get; set; }

    float _darkBud = MurkRosterTuning.BudEvery * 0.5f;

    /// <summary>
    /// On the cube reef, murklings come out of any dark spot near her (BudNear–BudFar, no ink needed), one every BudEvery s
    /// ÷ (1 + BudPerMenace × Menace), while fewer than Cap + CapPerMenace × Menace are round her; both more by the
    /// danger's step. None in a haven, nor in the boss's arena.
    /// </summary>
    void StepDarkMurklings()
    {
        if (!Options.Reef || Defeated || InHaven) return;
        float rate = 1f + DangerTuning.RateStep * Danger;
        _darkBud -= Dt * rate;
        if (_darkBud > 0f) return;
        _darkBud = MurkRosterTuning.BudEvery / (1f + MurkRosterTuning.BudPerMenace * Menace);
        int cap = (int)MathF.Round((MurkRosterTuning.Cap + MurkRosterTuning.CapPerMenace * Menace) * (1f + DangerTuning.CapStep * Danger));
        var her = Player.Position;
        if (Murklings.Count(m => m.Alive && Vector2.Distance(m.Position, her) < MurkRosterTuning.CapReach) >= cap) return;
        for (int tries = 0; tries < 16; tries++)
        {
            float a = _blightRng.Range(0f, MathF.Tau), d = _blightRng.Range(MurkRosterTuning.BudNear, MurkRosterTuning.BudFar);
            var at = her + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (!Clear(at, BlightTuning.MurkRadius * MurkGrowth) || Map.InShaft(at, 2f) || Map.InHaven(at)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 2f) continue;
            if (LightAt(at) > DangerTuning.Dark) continue;
            BudRoster(at, prime: false, born: false);
            return;
        }
    }
}
