using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Game.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Game.Cubes;

/// <summary>
/// A cube's ground: its 32 m chunks (none where a chunk is all void) and its roofs, as surfaces, and the meshes made of
/// them: one a chunk, its ground and its cap together (one object to draw it, still culled chunk by chunk), and one for
/// the roofs. The meshes are made on the worker too (a mesh resource may be made off the main thread), so showing one
/// costs the main thread next to nothing.
/// </summary>
public sealed class CubeShape
{
    public CubeShape(List<LevelShape.Surface> surfaces, List<List<LevelShape.Surface>> pieces)
    {
        Surfaces = surfaces;
        Meshes = pieces.FindAll(p => p.Count > 0).ConvertAll(p => LevelView.ToMesh(Merge(p)));
    }

    /// <summary>One surface, one mesh.</summary>
    public CubeShape(LevelShape.Surface surface) : this(new List<LevelShape.Surface> { surface }, new List<List<LevelShape.Surface>> { new() { surface } }) { }

    /// <summary>Surfaces joined into one (all drawn alike: the ground's material).</summary>
    public static LevelShape.Surface Merge(List<LevelShape.Surface> surfaces)
    {
        if (surfaces.Count == 1) return surfaces[0];
        int verts = 0, indices = 0;
        foreach (var s in surfaces)
        {
            verts += s.Vertices.Length;
            indices += s.Indices.Length;
        }
        var v = new Vector3[verts];
        var n = new Vector3[verts];
        var c = new Color[verts];
        var i = new int[indices];
        int vo = 0, io = 0;
        foreach (var s in surfaces)
        {
            s.Vertices.CopyTo(v, vo);
            s.Normals.CopyTo(n, vo);
            s.Colors.CopyTo(c, vo);
            for (int k = 0; k < s.Indices.Length; k++) i[io + k] = s.Indices[k] + vo;
            vo += s.Vertices.Length;
            io += s.Indices.Length;
        }
        return new LevelShape.Surface(v, n, c, i);
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
        float Height(float x, float y) => Smooth(t, t.Heights, x, y);
        // (Kept down to just above the void's floor, its outline smooth: the layer below shows through the void.)
        float Kept(float x, float y) => Height(x, y) - (VoidHeight + 0.3f);
        var surfaces = new List<LevelShape.Surface>();
        var pieces = new List<List<LevelShape.Surface>>();
        for (int cy = 0; cy < h; cy += Chunk)
        for (int cx = 0; cx < w; cx += Chunk)
        {
            var piece = new List<LevelShape.Surface>();
            if (LevelView.SurfaceMesh(1 << 20, Height, x0 + cx, y0 + cy, x0 + cx + Chunk, y0 + cy + Chunk, null, Kept) is { } s) piece.Add(UnderAbove(t, s));
            // (The cutout's dotted cap inside its rock at the cut's height: LevelView.Cap.)
            if (LevelView.Cap(1 << 20, Height, x0 + cx, y0 + cy, x0 + cx + Chunk, y0 + cy + Chunk) is { } cap) piece.Add(cap);
            surfaces.AddRange(piece);
            pieces.Add(piece);
        }
        if (Roofs(t) is { } roofs)
        {
            surfaces.Add(roofs);
            pieces.Add(new List<LevelShape.Surface> { roofs });
        }
        return new CubeShape(surfaces, pieces);
    }

    /// <summary>
    /// A socket sealed (a secret's cracked seal, docs/DIVE.md §6.1), drawn as the rock it was before it was cut: its own
    /// piece of ground over the cells its cutting changed (and a few metres round, where it is the ground itself), laid
    /// over the cube's ground. Its heights never under the ground's (so the cut passage is wholly under it), its skin, rise
    /// and caps made as the ground's are (so where it meets the ground they are one), its caps a hair over the ground's (so
    /// over the passage's it is its own that show). Its height (the reef's metres), and its rock's heights by cell.
    /// </summary>
    public sealed record PlugShape(CubeShape Shape, Func<float, float, float> Height, Dictionary<(int X, int Y), float> Cells);

    /// <summary>How far round a socket's line its cut cells are its own (m); how far past them its piece of ground runs; its caps' lift.</summary>
    const float PlugReach = 8f, CapLift = 0.004f;
    const int PlugMargin = 4;

    public static PlugShape? Plug(CubeTerrain t, OctoShoots.Core.Gen.TopDown.Doorway socket)
    {
        if (t.Uncut is not { } uncut) return null;
        var into = System.Numerics.Vector2.Normalize(socket.Into);
        System.Numerics.Vector2 a = socket.At - into * 4f, b = socket.Back + into * 2f;
        var heights = (float[])t.Heights.Clone();
        var cells = new Dictionary<(int, int), float>();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int k = 0; k < uncut.Length; k++)
        {
            if (float.IsNaN(uncut[k]) || uncut[k] <= heights[k]) continue;
            int gx = t.X0 + k % t.Stride, gy = t.Y0 + k / t.Stride;
            if (OctoShoots.Core.Gen.TopDown.Geo.SegmentDistance(new System.Numerics.Vector2(gx, gy), a, b) > PlugReach) continue;
            heights[k] = uncut[k];
            cells[(gx, gy)] = uncut[k];
            minX = Math.Min(minX, gx);
            maxX = Math.Max(maxX, gx);
            minY = Math.Min(minY, gy);
            maxY = Math.Max(maxY, gy);
        }
        if (cells.Count == 0) return null;
        // (Never under the ground: the smoothing's overshoot would let the ground show through.)
        float Height(float x, float y) => Mathf.Max(Smooth(t, heights, x, y), Smooth(t, t.Heights, x, y));
        float Kept(float x, float y) => Height(x, y) - (VoidHeight + 0.3f);
        int x0 = Math.Max(minX - PlugMargin, t.X0 + 2), y0 = Math.Max(minY - PlugMargin, t.Y0 + 2);
        int x1 = Math.Min(maxX + PlugMargin, t.X0 + t.Stride - 3), y1 = Math.Min(maxY + PlugMargin, t.Y0 + t.Rows - 3);
        var surfaces = new List<LevelShape.Surface>();
        if (LevelView.SurfaceMesh(1 << 20, Height, x0, y0, x1, y1, null, Kept) is { } s) surfaces.Add(UnderAbove(t, s));
        if (LevelView.Cap(1 << 20, Height, x0, y0, x1, y1) is { } cap)
        {
            var lifted = (Vector3[])cap.Vertices.Clone();
            for (int k = 0; k < lifted.Length; k++) lifted[k].Y += CapLift;
            surfaces.Add(new LevelShape.Surface(lifted, cap.Normals, cap.Colors, cap.Indices));
        }
        if (surfaces.Count == 0) return null;
        return new PlugShape(new CubeShape(surfaces, new List<List<LevelShape.Surface>> { surfaces }), Height, cells);
    }

    /// <summary>
    /// Her walls rise on up (ReefStreamView.Rise) only where the layer above is rock (CUBE-WORLD-PLAN §4.3): under its
    /// caverns' open water they stop about where its floor lies, so the walls she sees rising are the reef above going
    /// on. (Each vertex's alpha is how far it rises; a cap's 0.5 is left alone, and none comes near it.)
    /// </summary>
    static LevelShape.Surface UnderAbove(CubeTerrain t, LevelShape.Surface s)
    {
        if (t.UpperOpen is null) return s;
        var colours = (Color[])s.Colors.Clone();
        for (int k = 0; k < colours.Length; k++)
        {
            if (Mathf.Abs(colours[k].A - 0.5f) < 0.1f) continue;
            float open = UpperOpen(t, s.Vertices[k].X, s.Vertices[k].Z);
            colours[k].A = Rising(Mathf.Lerp(1f, UnderOpenRise, open));
        }
        return new LevelShape.Surface(s.Vertices, s.Normals, colours, s.Indices);
    }

    /// <summary>How much of the full rise a wall keeps under the layer above's open water (to about that layer's floor).</summary>
    const float UnderOpenRise = (LayerDrop - RockLow) / ReefStreamView.Rise;

    /// <summary>A rise kept clear of a cap's alpha (0.4–0.6, topdown_ground's is_cap).</summary>
    static float Rising(float a) => a > 0.39f && a < 0.61f ? (a < 0.5f ? 0.38f : 0.62f) : a;

    static float UpperOpen(CubeTerrain t, float x, float y)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        float a = Mathf.Lerp(t.UpperOpenAt(ix, iy), t.UpperOpenAt(ix + 1, iy), fx);
        float b = Mathf.Lerp(t.UpperOpenAt(ix, iy + 1), t.UpperOpenAt(ix + 1, iy + 1), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>
    /// Its roofs (CUBE-WORLD-PLAN §4.4): rock over the throats of its roofed gates and its tunnels bored through rock, in
    /// one piece, its outline smooth (its canopy blurred). Inside it rises with the walls (so from above the wall runs on
    /// whole over the way through); at its edge it stays at RoofHeight, the lintel over each mouth. Made over its own
    /// squares only, its canopy read on past them as its apron holds it, so neighbours' roofs meet at full height.
    /// </summary>
    static LevelShape.Surface? Roofs(CubeTerrain t)
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
                if (CanopyNear(t, bx0 + i + dx, by0 + j + dy)) sum += k;
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
        float Top(float x, float y) => RoofHeight + 0.25f * Mathf.Sin(x * 0.9f + Mathf.Sin(y * 0.7f) * 1.5f) * Mathf.Sin(y * 0.8f + 1.3f);
        int ox0 = t.Cube.X * CubeTuning.Side, oy0 = t.Cube.Y * CubeTuning.Side, ox1 = ox0 + t.Cube.Wide * CubeTuning.Side, oy1 = oy0 + t.Cube.Tall * CubeTuning.Side;
        int sx0 = Math.Max(minX - 3, ox0), sy0 = Math.Max(minY - 3, oy0), sx1 = Math.Min(maxX + 3, ox1), sy1 = Math.Min(maxY + 3, oy1);
        if (sx1 <= sx0 || sy1 <= sy0) return null;
        if (LevelView.SurfaceMesh(1 << 20, Top, sx0, sy0, sx1, sy1, null, (x, y) => Cover(x, y) - 0.34f) is not { } slab) return null;
        // (Its rise: none at its edge, the lintel; all of it a little way in, as the walls round it rise.)
        var colors = new Color[slab.Colors.Length];
        for (int k = 0; k < colors.Length; k++)
        {
            float c = Cover(slab.Vertices[k].X, slab.Vertices[k].Z);
            float rise = Mathf.SmoothStep(0.36f, 0.5f, c) * Mathf.Lerp(1f, UnderOpenRise, UpperOpen(t, slab.Vertices[k].X, slab.Vertices[k].Z));
            colors[k] = slab.Colors[k] with { A = Rising(rise) };
        }
        return new LevelShape.Surface(slab.Vertices, slab.Normals, colors, slab.Indices);
    }

    /// <summary>Its canopy at a point, read on past its arrays' edge as their last sample.</summary>
    static bool CanopyNear(CubeTerrain t, int gx, int gy) =>
        t.CanopyAt(Math.Clamp(gx, t.X0, t.X0 + t.Stride - 1), Math.Clamp(gy, t.Y0, t.Y0 + t.Rows - 1));

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
        return new CubeShape(new LevelShape.Surface(verts, normals, colors, indices));
    }

    /// <summary>
    /// The cube's height at a global point, smoothed as a level's ground is (Catmull-Rom through its 1 m samples, never
    /// past the cell's own corners). Read by cell from the point's floor, so a point on a cube line is the sample itself
    /// in both cubes.
    /// </summary>
    static float Smooth(CubeTerrain t, float[] h, float x, float y)
    {
        int n = t.Stride;
        int ix = Math.Clamp(Mathf.FloorToInt(x), t.X0 + 1, t.X0 + n - 3), iy = Math.Clamp(Mathf.FloorToInt(y), t.Y0 + 1, t.Y0 + t.Rows - 3);
        float fx = x - ix, fy = y - iy;
        int k = (iy - t.Y0) * n + (ix - t.X0);
        float Row(int r) => LevelView.CatmullRom(h[r - 1], h[r], h[r + 1], h[r + 2], fx);
        float v = LevelView.CatmullRom(Row(k - n), Row(k), Row(k + n), Row(k + 2 * n), fy);
        float c00 = h[k], c10 = h[k + 1], c01 = h[k + n], c11 = h[k + n + 1];
        return Mathf.Clamp(v, Mathf.Min(Mathf.Min(c00, c10), Mathf.Min(c01, c11)), Mathf.Max(Mathf.Max(c00, c10), Mathf.Max(c01, c11)));
    }
}
