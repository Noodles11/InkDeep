using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The adventure's murklings (docs/ADVENTURE.md §4): on every reef, at every depth, the only creatures are murklings
/// budded out of the leaks' ink (no fish turns). Four kinds: basic (seeks her light, clings and drinks), thrower (keeps a
/// throw's distance and throws ink), shoal (a whole school buds at once and rings her) and prime (big, rams, breaks
/// into murklings). The Menace makes them bigger and more of them. First guesses, tuned in play.
/// </summary>
public static class MurkRosterTuning
{
    /// <summary>A budding murkling's kind: this share basic, this thrower, the rest a shoal (a prime is its own roll).</summary>
    public const float BasicShare = 0.5f, ThrowerShare = 0.3f;
    /// <summary>A budded murkling's full size × (1 + SizePerMenace × Menace), at most × MostGrowth: bigger, and its corruption with it.</summary>
    public const float SizePerMenace = 0.5f, MostGrowth = 1.8f;
    /// <summary>A shoal's school: ShoalLeast + ShoalPerMenace × Menace murklings, each with this share of a murkling's meter.</summary>
    public const int ShoalLeast = 5;
    public const float ShoalPerMenace = 4f, ShoalMeter = 0.3f;
    /// <summary>
    /// A reef's murklings at its start: Start + StartPerMenace × Menace, in its ink, Spacing apart and at least FromStart
    /// from where she comes in.
    /// </summary>
    public const int Start = 4;
    public const float StartPerMenace = 8f, Spacing = 9f, FromStart = 18f;
    /// <summary>
    /// While a leak of the reef is open, one buds out of its ink every BudEvery s ÷ (1 + BudPerMenace × Menace),
    /// BudNear–BudFar from her, while fewer than Cap + CapPerMenace × Menace are within CapReach of her.
    /// </summary>
    public const float BudEvery = 9f, BudPerMenace = 1f, BudNear = 9f, BudFar = 20f, CapPerMenace = 5f, CapReach = 26f;
    public const int Cap = 4;
}

public sealed partial class PlaneWorld
{
    /// <summary>Whether this reef buds the adventure's murklings out of its ink (not a haven, a station or a labyrinth).</summary>
    bool InkMurklings => Options.Adventure && Options.InkMurklings && !Map.LeakCave && !Map.Station && !Map.Labyrinth && Map.Haven is null;

    /// <summary>How much bigger a budded murkling grows with the Menace (the adventure's; 1 elsewhere).</summary>
    float MurkGrowth => Options.Adventure && Options.InkMurklings ? MathF.Min(1f + MurkRosterTuning.SizePerMenace * Menace, MurkRosterTuning.MostGrowth) : 1f;

    /// <summary>The next shoal's school (apart from the reef fish's schools).</summary>
    int _nextShoal = 1 << 20;

    /// <summary>The adventure's kind for a budding murkling, from its own seed (on every depth alike).</summary>
    MurkKind RosterKind(float seed)
    {
        float roll = seed * 0.618034f % 1f;
        return roll < MurkRosterTuning.BasicShare ? MurkKind.Basic
            : roll < MurkRosterTuning.BasicShare + MurkRosterTuning.ThrowerShare ? MurkKind.Thrower : MurkKind.Shoal;
    }

    /// <summary>A shoal murkling, small and quick, one of a school (Swarm).</summary>
    void MakeShoal(Murkling m, int school, float meter)
    {
        m.Kind = MurkKind.Shoal;
        m.Swarm = school;
        m.Full = 0f;
        m.Radius = m.Body = AdventureTuning.ShoalRadius;
        m.Corruption.Max = m.Corruption.Value = meter * MurkRosterTuning.ShoalMeter;
    }

    /// <summary>
    /// A murkling of the roster buds here out of the ink (a prime if asked): a shoal buds its whole school round the spot.
    /// </summary>
    void BudRoster(Vector2 at, bool prime, bool born)
    {
        if (Bud(at, -1, prime, drink: false, born: born) is not { } m || m.Kind != MurkKind.Shoal) return;
        int more = MurkRosterTuning.ShoalLeast + (int)MathF.Round(MurkRosterTuning.ShoalPerMenace * Menace) - 1;
        float meter = m.Corruption.Max / MurkRosterTuning.ShoalMeter;
        for (int i = 0; i < more; i++)
        {
            float a = _blightRng.Range(0f, MathF.Tau), r = 0.5f + 0.7f * MathF.Sqrt(_blightRng.NextFloat());
            var p = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            if (!Clear(p, AdventureTuning.ShoalRadius)) p = at;
            if (Bud(p, -1, drink: false, born: born) is { } o) MakeShoal(o, m.Swarm, meter);
        }
    }

    /// <summary>A reef's murklings at its start: scattered through its ink, away from where she comes in and from the boss's arena.</summary>
    void PlaceReefMurklings(InkDots dots)
    {
        if (!InkMurklings || dots.Live.Count == 0) return;
        int want = MurkRosterTuning.Start + (int)MathF.Round(MurkRosterTuning.StartPerMenace * Menace);
        var cells = dots.Live.ToList();
        Shuffle(cells, _blightRng);
        int placed = 0;
        foreach (int cell in cells)
        {
            if (placed >= want) break;
            var at = InkDots.Centre(cell);
            if (Vector2.Distance(at, Map.Start.Position) < MurkRosterTuning.FromStart || !Clear(at, BlightTuning.MurkRadius * MurkGrowth)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 4f) continue;
            if (Map.InShaft(at, 2f) || Murklings.Any(m => Vector2.Distance(m.Position, at) < MurkRosterTuning.Spacing)) continue;
            bool prime = _primesBorn < BlightTuning.PrimePerLevel
                && _blightRng.NextFloat() < BlightTuning.PrimeChance + BlightTuning.PrimeChancePerMenace * Menace && Clear(at, BlightTuning.PrimeBody);
            BudRoster(at, prime, born: true);
            placed++;
        }
        _reefBud = MurkRosterTuning.BudEvery * 0.5f;
    }

    float _reefBud;

    /// <summary>While a leak of the reef is open, murklings bud out of its ink near her (and now and then a prime).</summary>
    void StepReefMurklings(InkDots dots)
    {
        if (!InkMurklings || LeaksOpen == 0 || Defeated || BossBattle || InHaven) return;
        _reefBud -= Dt;
        if (_reefBud > 0f) return;
        _reefBud = MurkRosterTuning.BudEvery / (1f + MurkRosterTuning.BudPerMenace * Menace);
        int cap = MurkRosterTuning.Cap + (int)MathF.Round(MurkRosterTuning.CapPerMenace * Menace);
        var her = Player.Position;
        if (Murklings.Count(m => m.Alive && Vector2.Distance(m.Position, her) < MurkRosterTuning.CapReach) >= cap || dots.Live.Count == 0) return;
        for (int tries = 0; tries < 16; tries++)
        {
            var at = InkDots.Centre(dots.Live[_blightRng.Int(dots.Live.Count)]);
            float d = Vector2.Distance(at, her);
            if (d < MurkRosterTuning.BudNear || d > MurkRosterTuning.BudFar || !Clear(at, BlightTuning.MurkRadius * MurkGrowth)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 2f) continue;
            bool prime = _primesBorn < BlightTuning.PrimePerLevel && !Murklings.Any(x => x.Alive && x.Prime)
                && _blightRng.NextFloat() < BlightTuning.PrimeChance + BlightTuning.PrimeChancePerMenace * Menace
                && d > BlightTuning.PrimeClearance && Clear(at, BlightTuning.PrimeBody);
            BudRoster(at, prime, born: false);
            // It is made of the reef's ink: the dots round it go into it.
            float r = BlightTuning.GatherRadius * (prime ? MathF.Sqrt(BlightTuning.PrimeInk / BlightTuning.Ink) : 1f);
            foreach (int cell in dots.Live.Where(c => Vector2.Distance(InkDots.Centre(c), at) < r).ToList()) dots.Remove(cell);
            return;
        }
    }
}
