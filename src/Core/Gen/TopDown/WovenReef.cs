using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>What a woven reef is built of (docs/proposals/REEF-REDO-PROPOSAL.md §3).</summary>
public enum PieceKind
{
    /// <summary>A coral pinnacle rising from the reef bank through her plane: a column she swims round.</summary>
    Pinnacle,
    /// <summary>Staghorn: forking branches in a thicket, under her plane (or now and then through it).</summary>
    Staghorn,
    /// <summary>Table coral: a plate on a stalk. Above her plane she swims under it; below it, over it.</summary>
    Table,
    /// <summary>An arch over a lane: two feet in the banks, its span above her plane.</summary>
    Arch,
    /// <summary>A standing ring: its hole at her plane, a way through the thicket.</summary>
    Ring,
    /// <summary>A brain coral or boulder head on the bank or the lane's floor.</summary>
    Head,
    /// <summary>A sea fan, broadside to the current: soft, it bends in her wake.</summary>
    Fan,
    /// <summary>A cluster of sponge tubes.</summary>
    Sponge,
    /// <summary>A tuft of soft coral: soft, it bends in her wake.</summary>
    Soft,
}

/// <summary>
/// One piece of a woven reef: where it stands (its foot on the floor at <see cref="Base"/>), how high it reaches
/// (<see cref="Top"/>, both from her plane), its size (its radius, or an arch's half span), its yaw and its own seed.
/// </summary>
public readonly record struct ReefPiece(PieceKind Kind, Vector2 At, float Base, float Top, float Size, float Yaw, uint Seed);

/// <summary>
/// The woven reef (docs/proposals/REEF-REDO-PROPOSAL.md, the look prototype): the level's walls give way to a reef of coral
/// pieces over open water. The lanes (the water the level's cave already had) keep their deep floor; where the rock
/// stood, a reef bank rises under her plane and the coral grows on it: pinnacles, arches and rings through her plane,
/// thickets, tables, heads, fans and sponges under it. Only what crosses her plane blocks her (<see cref="LevelMap.Blocks"/>),
/// and none of it stands in a lane, so all she could reach she still reaches. The caves keep their rock.
/// </summary>
public static class WovenReef
{
    /// <summary>The bank's top under her plane at Menace 0 and 1 (metres).</summary>
    public const float BankShallow = -2.6f, BankDeep = -9f;
    /// <summary>How far from a lane a piece that crosses her plane may stand (its edge, metres), and how far round a cave its rock is kept.</summary>
    const float LaneClear = 1.2f, CaveKeep = 7f;
    /// <summary>The bank's rise from the lane's floor, over this many metres out from the lane.</summary>
    const float BankRise = 6f;

    public static LevelMap Apply(LevelMap map)
    {
        int n = map.Samples;
        var rng = new Rng(map.Seed ^ 0x3E7E5UL ^ ((ulong)map.Level << 22) ^ ((ulong)map.Depth << 30) ^ ((ulong)map.Attempt << 46));

        // What stays as it is: the caves (their rock and their roofs), the shaft and the springs.
        var keep = new bool[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            if (!float.IsNaN(map.LidAt(x, y)) || !float.IsNaN(map.RoofAt(x, y))) Mark(keep, n, new Vector2(x, y), CaveKeep);
        foreach (var cave in map.Caves)
            foreach (var (c, r) in cave.Chambers) Mark(keep, n, c, r + CaveKeep);
        foreach (var door in map.Mouths) Mark(keep, n, door.At, CaveKeep);
        if (map.Way is { } way) Mark(keep, n, way.At, CaveKeep);

        // The lanes: the water the level had. How far each point lies from one (0 in it).
        var lane = new bool[n * n];
        int rim = (int)LevelMap.RimWidth;
        for (int i = 0; i < n * n; i++) lane[i] = map.Heights[i] <= LevelMap.BlockHeight;
        var fromLane = Chamfer(lane, n);
        var inRim = new Func<int, int, bool>((x, y) => x < rim || y < rim || x > n - 1 - rim || y > n - 1 - rim);

        // The bank: where the rock stood, a reef bank under her plane, rising from the lane's floor.
        float menace = Math.Clamp(map.Menace, 0f, 1f);
        float bank = BankShallow + (BankDeep - BankShallow) * menace;
        uint noise = (uint)(map.Seed ^ 0xBA4CUL);
        var laneFloor = LaneFloor(map, lane, n);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            if (lane[i] || keep[i] || inRim(x, y)) continue;
            float t = MathF.Min(fromLane[i] / BankRise, 1f);
            t = t * t * (3f - 2f * t);
            float top = bank + 1.4f * TerrainNoise.Fbm(x / 9f, y / 9f, noise, 2);
            map.Heights[i] = MathF.Min(laneFloor[i] + (top - laneFloor[i]) * t, -1.6f);
        }

        // The pieces, on a jittered grid so they never clump: crossing her plane only in the banks, away from the lanes.
        var pieces = map.Pieces;
        pieces.Clear();
        bool Bank(Vector2 p, float clear)
        {
            int x = (int)MathF.Round(p.X), y = (int)MathF.Round(p.Y);
            if (x < rim + 1 || y < rim + 1 || x > n - 2 - rim || y > n - 2 - rim) return false;
            int i = y * n + x;
            return !lane[i] && !keep[i] && fromLane[i] >= clear;
        }
        bool InWater(Vector2 p)
        {
            int x = (int)MathF.Round(p.X), y = (int)MathF.Round(p.Y);
            if (x < rim + 1 || y < rim + 1 || x > n - 2 - rim || y > n - 2 - rim) return false;
            int i = y * n + x;
            return !keep[i] && !map.Shaft.Contains(p, 3f);
        }
        bool Near(Vector2 p, float r) => Vector2.Distance(p, map.Start.Position) < r || Vector2.Distance(p, map.Exit.Position) < map.Exit.Radius + r;
        float Floor(Vector2 p) => map.HeightAt(p);
        uint Seed() => (uint)(rng.NextU64() >> 32);

        // The blocking pieces first, from the widest gaps down: pinnacles, rings and staghorn crowns, apart enough to
        // leave gaps to slip through.
        var blocking = new List<(Vector2 At, float R)>();
        bool Apart(Vector2 p, float r, float gap)
        {
            foreach (var (q, s) in blocking)
                if (Vector2.Distance(p, q) < r + s + gap) return false;
            return true;
        }
        for (int k = 0; k < 2600; k++)
        {
            var p = new Vector2(rng.Range(rim, n - 1 - rim), rng.Range(rim, n - 1 - rim));
            float roll = rng.NextFloat();
            if (roll < 0.62f)
            {
                float r = rng.Range(1.4f, 3.6f);
                if (!Bank(p, LaneClear + r) || Near(p, 8f) || !Apart(p, r, rng.Range(2.2f, 4.5f))) continue;
                pieces.Add(new ReefPiece(PieceKind.Pinnacle, p, Floor(p), rng.Range(1.8f, 6.5f), r, rng.Range(0f, MathF.Tau), Seed()));
                blocking.Add((p, r * 0.92f));
                // Life crowds round its foot: thickets, fans, sponges and soft coral, woven into the bank.
                int crowd = 3 + rng.Int(5);
                for (int c = 0; c < crowd; c++)
                {
                    float a = rng.Range(0f, MathF.Tau), d = r * rng.Range(0.9f, 1.7f);
                    var q = p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                    float f = Floor(q);
                    if (-1.4f - f < 0.8f) continue;
                    float pick = rng.NextFloat();
                    var kind = pick < 0.4f ? PieceKind.Staghorn : pick < 0.6f ? PieceKind.Fan : pick < 0.8f ? PieceKind.Sponge : PieceKind.Soft;
                    float size = kind == PieceKind.Staghorn ? rng.Range(0.9f, 1.8f) : rng.Range(0.5f, 1.2f);
                    pieces.Add(new ReefPiece(kind, q, f, MathF.Min(f + size * rng.Range(1.2f, 2.2f), -1.4f), size, rng.Range(0f, MathF.Tau), Seed()));
                }
            }
            else if (roll < 0.74f)
            {
                // A ring standing across the thicket, its hole at her plane.
                float r = rng.Range(2.2f, 3.4f);
                float yaw = rng.Range(0f, MathF.PI);
                var axis = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
                var a = p + axis * r;
                var b = p - axis * r;
                if (!Bank(a, LaneClear + 0.8f) || !Bank(b, LaneClear + 0.8f) || Near(p, 10f) || !Apart(p, r + 0.8f, 2.5f)) continue;
                pieces.Add(new ReefPiece(PieceKind.Ring, p, Floor(p), 0.7f + r, r, yaw, Seed()));
                blocking.Add((a, 0.75f));
                blocking.Add((b, 0.75f));
            }
            else
            {
                // A staghorn thicket whose crown breaks her plane.
                float r = rng.Range(1.8f, 3.2f);
                if (!Bank(p, LaneClear + r) || Near(p, 8f) || !Apart(p, r, rng.Range(2.4f, 4.5f))) continue;
                pieces.Add(new ReefPiece(PieceKind.Staghorn, p, Floor(p), rng.Range(1.2f, 2.4f), r, rng.Range(0f, MathF.Tau), Seed()));
                blocking.Add((p, r * 0.8f));
            }
        }

        // The rim: a wall of tall pinnacles shoulder to shoulder all round, on a bank (no blank wall of rock).
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            if (!inRim(x, y) || keep[i]) continue;
            float e = MathF.Min(MathF.Min(x, y), MathF.Min(n - 1 - x, n - 1 - y));
            map.Heights[i] = MathF.Min(bank + 0.8f * TerrainNoise.Fbm(x / 7f, y / 7f, noise + 3u, 2), -1.6f) + (rim - e) * 0.15f;
        }
        for (int side = 0; side < 4; side++)
            for (float t = rim * 0.5f; t < n - 1 - rim * 0.5f; t += rng.Range(3.2f, 4.6f))
            {
                float inset = rng.Range(2.5f, rim - 0.5f);
                var p = side switch
                {
                    0 => new Vector2(t, inset),
                    1 => new Vector2(n - 1 - inset, t),
                    2 => new Vector2(t, n - 1 - inset),
                    _ => new Vector2(inset, t),
                };
                float r = rng.Range(2.6f, 4.2f);
                pieces.Add(new ReefPiece(PieceKind.Pinnacle, p, Floor(p), rng.Range(3f, 9f), r, rng.Range(0f, MathF.Tau), Seed()));
                blocking.Add((p, r));
            }
        // And behind them, nothing lets her out: the band itself blocks.
        for (int side = 0; side < 4; side++)
            for (float t = 0f; t <= n - 1; t += 3f)
            {
                var p = side switch
                {
                    0 => new Vector2(t, 1.5f),
                    1 => new Vector2(n - 2.5f, t),
                    2 => new Vector2(t, n - 2.5f),
                    _ => new Vector2(1.5f, t),
                };
                blocking.Add((p, 3f));
            }

        // Arches over the lanes: two feet in the banks either side, the span high over her plane.
        for (int k = 0, arches = 0; k < 900 && arches < 6; k++)
        {
            var p = new Vector2(rng.Range(rim, n - 1 - rim), rng.Range(rim, n - 1 - rim));
            int pi = (int)MathF.Round(p.Y) * n + (int)MathF.Round(p.X);
            if (!lane[pi] || keep[pi] || Near(p, 12f)) continue;
            float yaw = rng.Range(0f, MathF.PI);
            var axis = new Vector2(MathF.Cos(yaw), MathF.Sin(yaw));
            float best = -1f;
            for (float half = 4f; half <= 9f; half += 0.5f)
                if (Bank(p + axis * half, LaneClear + 0.6f) && Bank(p - axis * half, LaneClear + 0.6f))
                {
                    best = half;
                    break;
                }
            if (best < 0f || !Apart(p + axis * best, 1f, 2f) || !Apart(p - axis * best, 1f, 2f)) continue;
            pieces.Add(new ReefPiece(PieceKind.Arch, p, MathF.Min(Floor(p + axis * best), Floor(p - axis * best)), rng.Range(3.2f, 5f), best, yaw, Seed()));
            blocking.Add((p + axis * best, 1f));
            blocking.Add((p - axis * best, 1f));
            arches++;
        }

        // Tables: their stalks in the banks; their plates above her plane (to swim under) or below it (to swim over).
        for (int k = 0; k < 320; k++)
        {
            var p = new Vector2(rng.Range(rim, n - 1 - rim), rng.Range(rim, n - 1 - rim));
            bool high = rng.NextFloat() < 0.25f;
            float r = high ? rng.Range(1.8f, 3.4f) : rng.Range(1.5f, 3.5f);
            if (high)
            {
                if (!Bank(p, LaneClear + 0.8f) || Near(p, 10f) || !Apart(p, 0.8f, 1.2f)) continue;
                pieces.Add(new ReefPiece(PieceKind.Table, p, Floor(p), rng.Range(2.6f, 4.2f), r, rng.Range(0f, MathF.Tau), Seed()));
                blocking.Add((p, 0.75f));
            }
            else
            {
                // Low plates only on the banks (in a lane, seen through its deep water, they would read as pale pads).
                if (!InWater(p) || Floor(p) > -3.5f || !Bank(p, 0.5f) || rng.NextFloat() < 0.5f) continue;
                pieces.Add(new ReefPiece(PieceKind.Table, p, Floor(p), MathF.Min(Floor(p) + rng.Range(1.5f, 3f), -1.4f), r, rng.Range(0f, MathF.Tau), Seed()));
            }
        }

        // Under her plane, everywhere: thickets, heads, sponges, fans and soft coral, thicker on the banks.
        for (int k = 0; k < 5200; k++)
        {
            var p = new Vector2(rng.Range(rim, n - 1 - rim), rng.Range(rim, n - 1 - rim));
            if (!InWater(p)) continue;
            int i = (int)MathF.Round(p.Y) * n + (int)MathF.Round(p.X);
            float floor = Floor(p);
            // In the lanes sparse (their floor stays open to read the depth), on the banks thick.
            if (lane[i] && rng.NextFloat() < 0.7f) continue;
            float room = -1.3f - floor;
            if (room < 0.8f) continue;
            float roll = rng.NextFloat();
            PieceKind kind = roll < 0.34f ? PieceKind.Staghorn : roll < 0.5f ? PieceKind.Head : roll < 0.64f ? PieceKind.Sponge : roll < 0.82f ? PieceKind.Fan : PieceKind.Soft;
            float size = kind switch
            {
                PieceKind.Staghorn => rng.Range(1f, 2.6f),
                PieceKind.Head => rng.Range(0.7f, 2f),
                PieceKind.Sponge => rng.Range(0.4f, 0.9f),
                PieceKind.Fan => rng.Range(0.8f, 1.8f),
                _ => rng.Range(0.4f, 0.9f),
            };
            float tall = kind switch
            {
                PieceKind.Staghorn => size * rng.Range(0.8f, 1.4f),
                PieceKind.Head => size * 0.8f,
                PieceKind.Sponge => size * rng.Range(2f, 4f),
                PieceKind.Fan => size * rng.Range(1.4f, 2f),
                _ => size * rng.Range(1.2f, 2f),
            };
            float top = MathF.Min(floor + tall, -1.3f);
            pieces.Add(new ReefPiece(kind, p, floor, top, size, rng.Range(0f, MathF.Tau), Seed()));
        }

        // What grew on the walls (their coral and the like) goes with them; the arches and roofs of the old walls too.
        map.Decor.RemoveAll(d => d.Kind is DecorKind.CliffCoral or DecorKind.EncrustingCoral || d.Height > LevelMap.BlockHeight && !Kept(d.Position));
        map.Canopies.RemoveAll(c => c.Kind != CanopyKind.CaveRoof && !Kept(c.Center));
        map.SetBlocks(blocking);
        return map;

        bool Kept(Vector2 p)
        {
            int x = Math.Clamp((int)MathF.Round(p.X), 0, n - 1), y = Math.Clamp((int)MathF.Round(p.Y), 0, n - 1);
            return keep[y * n + x];
        }
    }

    /// <summary>Each point's lane floor: its own height in a lane, else the nearest lane's (the bank rises from it).</summary>
    static float[] LaneFloor(LevelMap map, bool[] lane, int n)
    {
        var floor = new float[n * n];
        var from = new int[n * n];
        var queue = new Queue<int>();
        for (int i = 0; i < n * n; i++)
        {
            from[i] = lane[i] ? i : -1;
            if (lane[i]) queue.Enqueue(i);
        }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % n, y = i / n;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                int j = ny * n + nx;
                if (from[j] >= 0) continue;
                from[j] = from[i];
                queue.Enqueue(j);
            }
        }
        for (int i = 0; i < n * n; i++) floor[i] = from[i] >= 0 ? MathF.Min(map.Heights[from[i]], -1.6f) : -4f;
        return floor;
    }

    static void Mark(bool[] keep, int n, Vector2 c, float r)
    {
        int x0 = Math.Max(0, (int)(c.X - r)), x1 = Math.Min(n - 1, (int)(c.X + r) + 1);
        int y0 = Math.Max(0, (int)(c.Y - r)), y1 = Math.Min(n - 1, (int)(c.Y + r) + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (Vector2.Distance(new Vector2(x, y), c) <= r) keep[y * n + x] = true;
    }

    static float[] Chamfer(bool[] source, int n)
    {
        var d = new float[n * n];
        for (int i = 0; i < d.Length; i++) d[i] = source[i] ? 0f : 1e9f;
        const float D = 1.41421356f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            int i = y * n + x;
            if (x > 0) d[i] = MathF.Min(d[i], d[i - 1] + 1f);
            if (y > 0) d[i] = MathF.Min(d[i], d[i - n] + 1f);
            if (x > 0 && y > 0) d[i] = MathF.Min(d[i], d[i - n - 1] + D);
            if (x < n - 1 && y > 0) d[i] = MathF.Min(d[i], d[i - n + 1] + D);
        }
        for (int y = n - 1; y >= 0; y--)
        for (int x = n - 1; x >= 0; x--)
        {
            int i = y * n + x;
            if (x < n - 1) d[i] = MathF.Min(d[i], d[i + 1] + 1f);
            if (y < n - 1) d[i] = MathF.Min(d[i], d[i + n] + 1f);
            if (x < n - 1 && y < n - 1) d[i] = MathF.Min(d[i], d[i + n + 1] + D);
            if (x > 0 && y < n - 1) d[i] = MathF.Min(d[i], d[i + n - 1] + D);
        }
        return d;
    }
}
