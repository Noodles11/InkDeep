using System;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The level's danger (docs/GAME.md §14, brought over from the cube reef's danger clock): on a crossover level the
/// corruption has no leaks to bud from, so its murklings come out of the dark instead, more the deeper she is (the
/// Menace) and more the longer she stays. Each level's clock starts as she arrives and its Seconds are cut into Steps
/// equal shares: in the first, murklings come as the Menace alone has them; each share after, they come faster and more
/// of them stay round her (RateStep and CapStep more a step), to the most at the last. A boss level's clock stops once
/// its boss is freed. The HUD shows the step as exclamation marks (none at first, four at the most).
/// </summary>
public static class DangerTuning
{
    /// <summary>How long a level's clock runs to the most danger (seconds), and the equal shares it is cut into.</summary>
    public const float Seconds = 150f;
    public const int Steps = 5;
    /// <summary>Each step past the first: murklings come this much more often, and this many more may stay round her (shares of the Menace's own).</summary>
    public const float RateStep = 0.35f, CapStep = 0.3f;
    /// <summary>A spot this dark (her light and every other added up there) is dark enough for a murkling to come out of.</summary>
    public const float Dark = 0.05f;

    /// <summary>The step the clock is at after this many seconds (0 to Steps − 1).</summary>
    public static int StepAt(float seconds) => Math.Clamp((int)(seconds / (Seconds / Steps)), 0, Steps - 1);
}

public sealed partial class PlaneWorld
{
    /// <summary>Whether this level's murklings come out of the dark (a crossover level: no leaks, no ink to bud from).</summary>
    bool DarkMurklings => Options.Adventure && Options.InkMurklings && Map.Templated && !Map.Rift && Map.Haven is null;

    /// <summary>The level's danger step (DangerTuning): 0 calm, Steps − 1 the most; 0 again once a boss level's boss is freed.</summary>
    public int Danger => !DarkMurklings || Boss is { Freed: true } ? 0 : DangerTuning.StepAt(Time);

    float _darkBud = MurkRosterTuning.BudEvery * 0.5f;

    /// <summary>The level's first murklings, in the dark away from where she comes in: as many as an inked reef would start with.</summary>
    void PlaceDarkMurklings()
    {
        if (!DarkMurklings) return;
        int want = MurkRosterTuning.Start + (int)MathF.Round(MurkRosterTuning.StartPerMenace * Menace);
        for (int tries = 0, placed = 0; tries < 400 && placed < want; tries++)
        {
            var at = new Vector2(_blightRng.Range(0f, Map.Size), _blightRng.Range(0f, Map.Size));
            if (!Map.IsOpen(at) || Vector2.Distance(at, Map.Start.Position) < MurkRosterTuning.FromStart) continue;
            if (!Clear(at, BlightTuning.MurkRadius * MurkGrowth) || Map.InShaft(at, 2f)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 4f) continue;
            if (Murklings.Any(m => Vector2.Distance(m.Position, at) < MurkRosterTuning.Spacing)) continue;
            BudRoster(at, prime: false, born: true);
            placed++;
        }
    }

    /// <summary>
    /// Murklings come out of any dark spot near her (BudNear–BudFar, no ink needed), one every BudEvery s ÷ (1 +
    /// BudPerMenace × Menace), while fewer than Cap + CapPerMenace × Menace are round her; both more by the danger's step.
    /// None during a boss's fight, nor in its arena.
    /// </summary>
    void StepDarkMurklings()
    {
        if (!DarkMurklings || Defeated || BossBattle || InHaven) return;
        _darkBud -= Dt * (1f + DangerTuning.RateStep * Danger);
        if (_darkBud > 0f) return;
        _darkBud = MurkRosterTuning.BudEvery / (1f + MurkRosterTuning.BudPerMenace * Menace);
        int cap = (int)MathF.Round((MurkRosterTuning.Cap + MurkRosterTuning.CapPerMenace * Menace) * (1f + DangerTuning.CapStep * Danger));
        var her = Player.Position;
        if (Murklings.Count(m => m.Alive && Vector2.Distance(m.Position, her) < MurkRosterTuning.CapReach) >= cap) return;
        for (int tries = 0; tries < 16; tries++)
        {
            float a = _blightRng.Range(0f, MathF.Tau), d = _blightRng.Range(MurkRosterTuning.BudNear, MurkRosterTuning.BudFar);
            var at = her + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (!Clear(at, BlightTuning.MurkRadius * MurkGrowth) || Map.InShaft(at, 2f)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 2f) continue;
            if (LightAt(at) > DangerTuning.Dark) continue;
            bool prime = _primesBorn < BlightTuning.PrimePerLevel && !Murklings.Any(x => x.Alive && x.Prime)
                && _blightRng.NextFloat() < BlightTuning.PrimeChance + BlightTuning.PrimeChancePerMenace * Menace
                && d > BlightTuning.PrimeClearance && Clear(at, BlightTuning.PrimeBody);
            BudRoster(at, prime, born: false);
            return;
        }
    }
}
