using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Game.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Game.Cubes;

/// <summary>
/// A cube's ground: its 32 m chunks (none where a chunk is all void) and its arches, as surfaces and as the meshes made
/// of them. The meshes are made on the worker too (a mesh resource may be made off the main thread), so showing one costs
/// the main thread next to nothing.
/// </summary>
public sealed class CubeShape
{
    public CubeShape(List<LevelShape.Surface> surfaces)
    {
        Surfaces = surfaces;
        Meshes = surfaces.ConvertAll(LevelView.ToMesh);
    }

    public List<LevelShape.Surface> Surfaces { get; }
    public List<ArrayMesh> Meshes { get; }
}

/// <summary>
/// A cube's meshes worked out from its terrain (CUBE-WORLD-PLAN §5.5): plain arrays in global metres, safe on any thread.
/// The ground is the level's own surface builder over the cube's heights, smoothed with the two samples of apron either
/// side, so neighbours' chunks meet exactly; where it is void there is no ground at all, and the layer below shows.
/// </summary>
public static class CubeShapes
{
    const int Chunk = 32;

    public static CubeShape Ground(CubeTerrain t)
    {
        int x0 = t.Cube.X * CubeTuning.Side, y0 = t.Cube.Y * CubeTuning.Side;
        int w = t.Cube.Wide * CubeTuning.Side, h = t.Cube.Tall * CubeTuning.Side;
        float Height(float x, float y) => Smooth(t, x, y);
        // (Kept down to just above the void's floor, its outline smooth: the layer below shows through the void.)
        float Kept(float x, float y) => Height(x, y) - (VoidHeight + 0.3f);
        var surfaces = new List<LevelShape.Surface>();
        for (int cy = 0; cy < h; cy += Chunk)
        for (int cx = 0; cx < w; cx += Chunk)
            if (LevelView.SurfaceMesh(1 << 20, Height, x0 + cx, y0 + cy, x0 + cx + Chunk, y0 + cy + Chunk, null, Kept) is { } s) surfaces.Add(s);
        if (Arches(t) is { } arches) surfaces.Add(arches);
        return new CubeShape(surfaces);
    }

    /// <summary>Its arches: a slab of rock over the water they span, in one piece, its outline smooth (its canopy blurred).</summary>
    static LevelShape.Surface? Arches(CubeTerrain t)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        int n = t.Stride;
        for (int k = 0; k < t.Canopy.Length; k++)
        {
            if (!t.Canopy[k]) continue;
            int gx = t.X0 + k % n, gy = t.Y0 + k / n;
            minX = Math.Min(minX, gx);
            maxX = Math.Max(maxX, gx);
            minY = Math.Min(minY, gy);
            maxY = Math.Max(maxY, gy);
        }
        if (maxX < minX) return null;
        // Its cover: the canopy's cells blurred (a 5 × 5 tent), read between the grid's points; the slab is where it is over a third.
        int bx0 = minX - 4, by0 = minY - 4, bw = maxX - minX + 9, bh = maxY - minY + 9;
        var cover = new float[bw * bh];
        for (int j = 0; j < bh; j++)
        for (int i = 0; i < bw; i++)
        {
            float sum = 0f, weight = 0f;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                float k = (3 - Math.Abs(dx)) * (3 - Math.Abs(dy));
                weight += k;
                if (t.CanopyAt(bx0 + i + dx, by0 + j + dy)) sum += k;
            }
            cover[j * bw + i] = sum / weight;
        }
        float Cover(float x, float y)
        {
            float fx = Mathf.Clamp(x - bx0, 0f, bw - 1.001f), fy = Mathf.Clamp(y - by0, 0f, bh - 1.001f);
            int i = (int)fx, j = (int)fy;
            float u = fx - i, v = fy - j;
            float a = Mathf.Lerp(cover[j * bw + i], cover[j * bw + i + 1], u), b = Mathf.Lerp(cover[(j + 1) * bw + i], cover[(j + 1) * bw + i + 1], u);
            return Mathf.Lerp(a, b, v);
        }
        float Top(float x, float y) => 2.6f + 0.45f * Mathf.Sin(x * 0.9f + Mathf.Sin(y * 0.7f) * 1.5f) * Mathf.Sin(y * 0.8f + 1.3f);
        return LevelView.SurfaceMesh(1 << 20, Top, minX - 3, minY - 3, maxX + 3, maxY + 3, null, (x, y) => Cover(x, y) - 0.34f);
    }

    /// <summary>The layer below's simplified ground: one mesh on a 2 m grid, its void a flat floor deep under it (murk).</summary>
    public static CubeShape Preview(CubeTerrain t)
    {
        const int step = 2;
        int x0 = t.Cube.X * CubeTuning.Side, y0 = t.Cube.Y * CubeTuning.Side;
        int w = t.Cube.Wide * CubeTuning.Side / step + 1, rows = t.Cube.Tall * CubeTuning.Side / step + 1;
        var verts = new Vector3[w * rows];
        var normals = new Vector3[w * rows];
        var colors = new Color[w * rows];
        for (int j = 0; j < rows; j++)
        for (int i = 0; i < w; i++)
        {
            int gx = x0 + i * step, gy = y0 + j * step;
            float h = t.HeightAt(gx, gy);
            verts[j * w + i] = new Vector3(gx, h, gy);
            normals[j * w + i] = new Vector3(t.HeightAt(gx - step, gy) - t.HeightAt(gx + step, gy), 2f * step, t.HeightAt(gx, gy - step) - t.HeightAt(gx, gy + step)).Normalized();
            colors[j * w + i] = Colors.White;
        }
        var indices = new int[(w - 1) * (rows - 1) * 6];
        int o = 0;
        for (int j = 0; j + 1 < rows; j++)
        for (int i = 0; i + 1 < w; i++)
        {
            int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
            indices[o++] = a;
            indices[o++] = b;
            indices[o++] = c;
            indices[o++] = b;
            indices[o++] = d;
            indices[o++] = c;
        }
        return new CubeShape(new List<LevelShape.Surface> { new(verts, normals, colors, indices) });
    }

    /// <summary>
    /// The cube's height at a global point, smoothed as a level's ground is (Catmull-Rom through its 1 m samples, never
    /// past the cell's own corners). Read by cell from the point's floor, so a point on a cube line is the sample itself
    /// in both cubes.
    /// </summary>
    static float Smooth(CubeTerrain t, float x, float y)
    {
        int n = t.Stride;
        int ix = Math.Clamp(Mathf.FloorToInt(x), t.X0 + 1, t.X0 + n - 3), iy = Math.Clamp(Mathf.FloorToInt(y), t.Y0 + 1, t.Y0 + t.Rows - 3);
        float fx = x - ix, fy = y - iy;
        int k = (iy - t.Y0) * n + (ix - t.X0);
        var h = t.Heights;
        float Row(int r) => LevelView.CatmullRom(h[r - 1], h[r], h[r + 1], h[r + 2], fx);
        float v = LevelView.CatmullRom(Row(k - n), Row(k), Row(k + n), Row(k + 2 * n), fy);
        float c00 = h[k], c10 = h[k + 1], c01 = h[k + n], c11 = h[k + n + 1];
        return Mathf.Clamp(v, Mathf.Min(Mathf.Min(c00, c10), Mathf.Min(c01, c11)), Mathf.Max(Mathf.Max(c00, c10), Mathf.Max(c01, c11)));
    }
}
