using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Plane;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// How a claimed cube is stocked with murklings (CUBE-WORLD-PLAN §9, §15.2): Least + PerMenace × the layer's Menace of
/// the roster's murklings, Spacing apart, none within FromEntry of where she came in; now and then a prime (from the
/// layer below the warm-up's first, never more than one a cube). Murklings farther than SleepReach from her are asleep.
/// First guesses, tuned in play.
/// </summary>
public static class CubeStockTuning
{
    public const int Least = 3;
    public const float PerMenace = 4f, Spacing = 9f, FromEntry = 18f, SleepReach = 48f;
    /// <summary>Tries at a spot per murkling wanted.</summary>
    public const int Tries = 24;
}

/// <summary>One of a cube's stock: its number in the cube, where it buds, whether a prime, and its seed (which sets its kind).</summary>
public readonly record struct StockSpot(int Spawn, Vector2 At, bool Prime, float Seed);

/// <summary>What is left of one of a cube's stock as she leaves the layer: where its living murklings are (a shoal's whole school).</summary>
public sealed record StockLeft(int Spawn, float Seed, bool Prime, bool Shoal, Vector2[] At);

public static class CubeStock
{
    /// <summary>
    /// Where a cube's stock buds, the moment it is claimed (its <paramref name="claim"/>-th on the layer), she coming in at
    /// <paramref name="entry"/>: from the seed, the cube and the claim alone, so the same reef swum the same way is
    /// stocked alike. <paramref name="clear"/>: whether a body of that radius fits at a point.
    /// </summary>
    public static List<StockSpot> Spots(ulong seed, CubeId cube, int claim, float menace, Vector2 entry, Func<Vector2, float, bool> clear)
    {
        var spots = new List<StockSpot>();
        if (cube.Z == 0) return spots;
        var rng = new Rng(ReefHash.Of(seed, ReefHash.Cube, cube.X, cube.Y, cube.Z, claim * 2 + (cube.OnEnd ? 1 : 0)) ^ 0x5701CUL);
        int want = CubeStockTuning.Least + (int)MathF.Round(CubeStockTuning.PerMenace * menace);
        bool primed = false;
        for (int tries = 0; tries < want * CubeStockTuning.Tries && spots.Count < want; tries++)
        {
            var at = cube.Origin + new Vector2(rng.Range(4f, cube.Size.X - 4f), rng.Range(4f, cube.Size.Y - 4f));
            if (Vector2.Distance(at, entry) < CubeStockTuning.FromEntry || !clear(at, BlightTuning.MurkRadius * 1.8f)) continue;
            bool near = false;
            foreach (var s in spots) near |= Vector2.Distance(s.At, at) < CubeStockTuning.Spacing;
            if (near) continue;
            bool prime = !primed && cube.Z >= 2 && rng.NextFloat() < BlightTuning.PrimeChance + BlightTuning.PrimeChancePerMenace * menace
                && clear(at, BlightTuning.PrimeBody);
            primed |= prime;
            spots.Add(new StockSpot(spots.Count, at, prime, rng.Range(0f, 100f)));
        }
        return spots;
    }
}
