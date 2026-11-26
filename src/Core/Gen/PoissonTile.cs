using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Gen;

/// <summary>
/// Poisson disk points (Bridson's algorithm) on a square tile that wraps round on itself, so tiles laid side by side
/// keep every point at least its spacing from every other, with no seam. One tile per spacing, made once (always the
/// same) and shared: the reef's decoration reads it wherever it grows, a level's square or an open level's chunk alike.
/// </summary>
public static class PoissonTile
{
    /// <summary>The tile's side (metres).</summary>
    public const float Size = 64f;

    static readonly ConcurrentDictionary<float, Vector2[]> Tiles = new();

    /// <summary>The tile's points for a spacing, in [0, Size).</summary>
    public static Vector2[] Of(float spacing) => Tiles.GetOrAdd(spacing, Make);

    /// <summary>Points to the square metre at a spacing.</summary>
    public static float Density(float spacing) => Of(spacing).Length / (Size * Size);

    /// <summary>The points in a box (any coordinates: the tile repeats).</summary>
    public static IEnumerable<Vector2> In(float spacing, Vector2 lo, Vector2 hi)
    {
        var points = Of(spacing);
        for (int ty = (int)MathF.Floor(lo.Y / Size); ty * Size < hi.Y; ty++)
        for (int tx = (int)MathF.Floor(lo.X / Size); tx * Size < hi.X; tx++)
            foreach (var q in points)
            {
                var p = q + new Vector2(tx, ty) * Size;
                if (p.X >= lo.X && p.Y >= lo.Y && p.X < hi.X && p.Y < hi.Y) yield return p;
            }
    }

    static float Wrap(float v)
    {
        v -= Size * MathF.Floor(v / Size);
        return v >= Size ? 0f : v;
    }

    static Vector2[] Make(float spacing)
    {
        var rng = new Rng(0x9015_5015_D15CUL ^ (ulong)BitConverter.SingleToInt32Bits(spacing));
        // (A whole number of cells across the tile, each small enough to hold one point at most.)
        int n = (int)MathF.Ceiling(Size * MathF.Sqrt(2f) / spacing);
        float cell = Size / n;
        var grid = new int[n * n];
        Array.Fill(grid, -1);
        var points = new List<Vector2>();
        var active = new List<int>();
        int Cell(float v) => Math.Min((int)(v / cell), n - 1);
        void Put(Vector2 p)
        {
            grid[Cell(p.Y) * n + Cell(p.X)] = points.Count;
            active.Add(points.Count);
            points.Add(p);
        }
        bool Free(Vector2 p)
        {
            int cx = Cell(p.X), cy = Cell(p.Y), reach = (int)MathF.Ceiling(spacing / cell);
            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int gx = ((cx + dx) % n + n) % n, gy = ((cy + dy) % n + n) % n;
                int i = grid[gy * n + gx];
                if (i < 0) continue;
                var d = points[i] - p;
                d.X -= Size * MathF.Round(d.X / Size);
                d.Y -= Size * MathF.Round(d.Y / Size);
                if (d.LengthSquared() < spacing * spacing) return false;
            }
            return true;
        }
        Put(new Vector2(rng.NextFloat() * Size, rng.NextFloat() * Size));
        while (active.Count > 0)
        {
            int a = rng.Int(active.Count);
            var c = points[active[a]];
            bool found = false;
            for (int k = 0; k < 30 && !found; k++)
            {
                float angle = rng.NextFloat() * MathF.Tau, d = spacing * (1f + rng.NextFloat());
                var p = new Vector2(Wrap(c.X + MathF.Cos(angle) * d), Wrap(c.Y + MathF.Sin(angle) * d));
                if (!Free(p)) continue;
                Put(p);
                found = true;
            }
            if (found) continue;
            active[a] = active[^1];
            active.RemoveAt(active.Count - 1);
        }
        return points.ToArray();
    }
}
