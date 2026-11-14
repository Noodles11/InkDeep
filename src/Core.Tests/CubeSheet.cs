using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using OctoShoots.Core.Gen.Cubes;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Tests;

/// <summary>
/// A picture of a block of cubes for judging the terrain by eye (CUBE-WORLD-PLAN §14.2 step 3): each layer seen from
/// above, the layers left to right. Rock shaded by its height; shelves sand; through the void, the layer below, dimmed
/// (and murk under the last). Arches hatched; wells down ringed orange, ways in from above green.
/// </summary>
public static class CubeSheet
{
    const int Scale = 3, Gap = 24;

    public static void Write(string path, Dictionary<CubeCoord, CubeTerrain> cubes, int footprint, int layers, bool lines)
    {
        int span = footprint * Side + 1, side = span * Scale;
        int w = layers * side + (layers - 1) * Gap, h = side;
        var px = new byte[w * h * 3];
        for (int i = 0; i < px.Length; i++) px[i] = 18;
        for (int z = 0; z < layers; z++)
        {
            int ox = z * (side + Gap);
            for (int gy = 0; gy < span; gy++)
            for (int gx = 0; gx < span; gx++)
            {
                var t = Owner(cubes, footprint, gx, gy, z);
                var below = z + 1 < layers ? Owner(cubes, footprint, gx, gy, z + 1) : null;
                var (r, g, b) = Colour(t, below, gx, gy);
                for (int sy = 0; sy < Scale; sy++)
                for (int sx = 0; sx < Scale; sx++)
                {
                    float cr = r, cg = g, cb = b;
                    if (t.CanopyAt(gx, gy) && ((gx * Scale + sx + gy * Scale + sy) % 4 == 0)) (cr, cg, cb) = (0.85f, 0.75f, 0.55f);
                    if (lines && (gx % Side == 0 || gy % Side == 0) && ((gx + gy) % 4 < 2)) (cr, cg, cb) = (cr * 0.5f + 0.5f, cg * 0.5f + 0.5f, cb * 0.5f + 0.5f);
                    Put(px, w, ox + gx * Scale + sx, gy * Scale + sy, cr, cg, cb);
                }
            }
            foreach (var t in cubes.Values)
            {
                if (t.Coord.Z != z) continue;
                foreach (var well in t.Down) Ring(px, w, ox, well.Center.X, well.Center.Y, well.Radius, (1f, 0.55f, 0.15f));
                foreach (var well in t.Arrivals) Ring(px, w, ox, well.Center.X, well.Center.Y, well.Radius, (0.35f, 0.95f, 0.45f));
            }
        }
        Png(path, w, h, px);
    }

    static CubeTerrain Owner(Dictionary<CubeCoord, CubeTerrain> cubes, int footprint, int gx, int gy, int z) =>
        cubes[new CubeCoord(Math.Min(gx / Side, footprint - 1), Math.Min(gy / Side, footprint - 1), z)];

    static (float, float, float) Colour(CubeTerrain t, CubeTerrain? below, int gx, int gy)
    {
        switch (t.CellAt(gx, gy))
        {
            case CubeCell.Rock:
            {
                float hgt = t.HeightAt(gx, gy);
                float dx = t.HeightAt(gx + 1, gy) - t.HeightAt(gx - 1, gy), dy = t.HeightAt(gx, gy + 1) - t.HeightAt(gx, gy - 1);
                float shade = Math.Clamp(0.75f + 0.25f * (-dx - dy) + 0.06f * hgt, 0.35f, 1.15f);
                return (0.46f * shade, 0.38f * shade, 0.31f * shade);
            }
            case CubeCell.Shelf:
            {
                float n = 0.04f * t.HeightAt(gx, gy);
                return (0.62f + n, 0.66f + n, 0.55f + n);
            }
            default:
                if (below is null) return (0.04f, 0.06f, 0.11f);
                return below.CellAt(gx, gy) switch
                {
                    CubeCell.Rock => (0.16f, 0.17f, 0.23f),
                    CubeCell.Shelf => (0.22f, 0.32f, 0.38f),
                    _ => (0.06f, 0.09f, 0.16f),
                };
        }
    }

    static void Ring(byte[] px, int w, int ox, float cx, float cy, float radius, (float, float, float) c)
    {
        for (int k = 0; k < 360; k++)
        {
            float a = k * MathF.PI / 180f;
            int x = (int)((cx + MathF.Cos(a) * radius) * Scale), y = (int)((cy + MathF.Sin(a) * radius) * Scale);
            for (int d = 0; d < 2; d++) Put(px, w, ox + x + d, y, c.Item1, c.Item2, c.Item3);
        }
    }

    static void Put(byte[] px, int w, int x, int y, float r, float g, float b)
    {
        int h = px.Length / 3 / w;
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = (y * w + x) * 3;
        px[i] = (byte)Math.Clamp(r * 255f, 0f, 255f);
        px[i + 1] = (byte)Math.Clamp(g * 255f, 0f, 255f);
        px[i + 2] = (byte)Math.Clamp(b * 255f, 0f, 255f);
    }

    static void Png(string path, int w, int h, byte[] rgb)
    {
        var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0);
                z.Write(rgb, y * w * 3, w * 3);
            }
        using var f = File.Create(path);
        f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        Big(ihdr, 0, w);
        Big(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 2;
        Chunk(f, "IHDR", ihdr);
        Chunk(f, "IDAT", raw.ToArray());
        Chunk(f, "IEND", Array.Empty<byte>());
    }

    static void Chunk(Stream f, string type, byte[] data)
    {
        var len = new byte[4];
        Big(len, 0, data.Length);
        f.Write(len);
        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
        Buffer.BlockCopy(data, 0, td, 4, data.Length);
        f.Write(td);
        var crc = new byte[4];
        Big(crc, 0, (int)Crc(td));
        f.Write(crc);
    }

    static void Big(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24);
        b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8);
        b[at + 3] = (byte)v;
    }

    static uint Crc(byte[] data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte d in data)
        {
            c ^= d;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFFu;
    }
}
