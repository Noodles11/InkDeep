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

    /// <summary>How many carved arrows point to a secret's mouth at most, how far from it, how far apart, how near a wall's foot.</summary>
    public const int CueMost = 3;
    public const float CueNear = 7f, CueFar = 26f, CueApart = 7f, CueWall = 1.7f;

    /// <summary>
    /// The arrows carved into the floor at the walls' feet round a secret's mouth (docs/DIVE.md §6.1), each pointing to
    /// it, seen only in her beam: up to CueMost, CueNear to CueFar from it, apart, each in sight of the water before it.
    /// <paramref name="height"/>: the floor's height at a point (rock above naught); <paramref name="sight"/>: whether one
    /// point sees another. From the seed and the mouth alone. Each: where it lies and the way it points.
    /// </summary>
    public static List<(Vector2 At, Vector2 Way)> Cues(ulong seed, OctoShoots.Core.Gen.TopDown.Doorway mouth, Func<Vector2, float> height, Func<Vector2, Vector2, bool> sight)
    {
        var cues = new List<(Vector2, Vector2)>();
        var before = mouth.At - mouth.Into * 2f;
        var rng = new Rng(seed ^ ((ulong)(uint)(int)MathF.Round(mouth.At.X * 4f) << 32) ^ (uint)(int)MathF.Round(mouth.At.Y * 4f) ^ 0xC0E5UL);
        for (int tries = 0; tries < 160 && cues.Count < CueMost; tries++)
        {
            float a = rng.Range(0f, MathF.Tau), r = rng.Range(CueNear, CueFar);
            var at = before + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            float h = height(at);
            if (h > -0.5f || h < -6f) continue;
            // (At a wall's foot: rock close by one way, open floor round it the others.)
            int rock = 0;
            for (int k = 0; k < 8; k++)
            {
                float b = k * MathF.Tau / 8f;
                float hk = height(at + new Vector2(MathF.Cos(b), MathF.Sin(b)) * CueWall);
                if (hk > 0.2f) rock++;
                else if (hk < -6f) rock = 99;
            }
            if (rock is 0 or > 3) continue;
            bool near = false;
            foreach (var (c, _) in cues) near |= Vector2.Distance(c, at) < CueApart;
            if (near || !sight(at, before)) continue;
            cues.Add((at, Vector2.Normalize(before - at)));
        }
        return cues;
    }

    /// <summary>
    /// Where a cube's stone lantern stands, if it has one (docs/LIGHT-AND-GRAPH.md §2.7): LanternTuning.Chance of cubes,
    /// none in a boss's arena; on its open floor (shelf, not under a roof), clear of rock, inside the cube and away from
    /// its ways (its gates' mouths, its wells, its sockets' passages, its spires). From the seed and the cube alone.
    /// </summary>
    public static Vector2? Lantern(ulong seed, CubeTerrain terrain)
    {
        var cube = terrain.Cube;
        if (terrain.Arena) return null;
        var rng = new Rng(ReefHash.Of(seed, ReefHash.Cube, cube.X, cube.Y, cube.Z, 0x7A + (cube.OnEnd ? 1 : 0)) ^ 0x70A0UL);
        if (rng.NextFloat() >= LanternTuning.Chance) return null;
        var ways = new List<Vector2>();
        foreach (var (_, _, mouth) in terrain.Gates) ways.Add(mouth);
        foreach (var w in terrain.Down) ways.Add(w.Center);
        foreach (var w in terrain.Arrivals) ways.Add(w.Center);
        foreach (var s in terrain.Sockets)
        {
            ways.Add(s.At);
            ways.Add(s.At + s.Into * OctoShoots.Core.Gen.TopDown.Doorway.Depth);
        }
        bool Floor(Vector2 p)
        {
            int r = (int)MathF.Ceiling(LanternTuning.Clear);
            int cx = (int)MathF.Round(p.X), cy = (int)MathF.Round(p.Y);
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if ((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) > LanternTuning.Clear * LanternTuning.Clear) continue;
                if (terrain.CellAt(x, y) != CubeCell.Shelf || terrain.CanopyAt(x, y) || terrain.HeightAt(x, y) > -0.5f) return false;
            }
            return true;
        }
        for (int tries = 0; tries < 60; tries++)
        {
            var at = cube.Origin + new Vector2(rng.Range(LanternTuning.Inside, cube.Size.X - LanternTuning.Inside), rng.Range(LanternTuning.Inside, cube.Size.Y - LanternTuning.Inside));
            bool near = false;
            foreach (var w in ways) near |= Vector2.Distance(w, at) < LanternTuning.FromWays;
            foreach (var s in terrain.Spires) near |= Vector2.Distance(new Vector2(s.X, s.Y), at) < s.Z + LanternTuning.Clear + 1f;
            if (!near && Floor(at)) return at;
        }
        return null;
    }

    /// <summary>
    /// Where a claimed cube's inkseeds lie (docs/CORRUPTION.md): InkseedTuning.Least + PerMenace × Menace of them, in its
    /// open water, apart from each other, from where she came in and from its ways (its gates' mouths, its wells, its
    /// sockets' passages); from the seed, the cube and the claim alone. The warm-up layer has them too.
    /// </summary>
    public static List<Vector2> Inkseeds(ulong seed, CubeId cube, int claim, float menace, Vector2 entry, CubeTerrain terrain, Func<Vector2, float, bool> clear)
    {
        var seeds = new List<Vector2>();
        var rng = new Rng(ReefHash.Of(seed, ReefHash.Cube, cube.X, cube.Y, cube.Z, claim * 2 + (cube.OnEnd ? 1 : 0)) ^ 0x1A55EEDUL);
        int want = InkseedTuning.Least + (int)MathF.Round(InkseedTuning.PerMenace * menace);
        var ways = new List<Vector2>();
        foreach (var (_, _, mouth) in terrain.Gates) ways.Add(mouth);
        foreach (var w in terrain.Down) ways.Add(w.Center);
        foreach (var w in terrain.Arrivals) ways.Add(w.Center);
        foreach (var s in terrain.Sockets)
        {
            ways.Add(s.At);
            ways.Add(s.At + s.Into * OctoShoots.Core.Gen.TopDown.Doorway.Depth);
        }
        for (int tries = 0; tries < want * CubeStockTuning.Tries && seeds.Count < want; tries++)
        {
            var at = cube.Origin + new Vector2(rng.Range(3f, cube.Size.X - 3f), rng.Range(3f, cube.Size.Y - 3f));
            if (Vector2.Distance(at, entry) < InkseedTuning.FromEntry || !clear(at, InkseedTuning.Radius * 2.5f)) continue;
            bool near = false;
            foreach (var w in ways) near |= Vector2.Distance(w, at) < InkseedTuning.FromWays;
            foreach (var s in seeds) near |= Vector2.Distance(s, at) < InkseedTuning.Spacing;
            if (!near) seeds.Add(at);
        }
        return seeds;
    }
}
