using System;
using System.Collections.Generic;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Vector2 = System.Numerics.Vector2;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The ground the reef's decoration grows on (LevelView's flora, BiomePropView's things): a template level's square, or
/// an open level's chunk (in sea metres, so it grows the same on every visit and meets its neighbours'). Pure: any thread.
/// </summary>
internal sealed class DecorGround
{
    public required Func<Vector2, float> Height;
    /// <summary>No decoration within this margin of here (a shaft or a hole; a sealed wall).</summary>
    public required Func<Vector2, float, bool> Blocked;
    /// <summary>The places (and her start) things keep off: where, and how far.</summary>
    public required List<(Vector2 At, float Radius)> Places;
    public Vector2 Lo, Hi;
    /// <summary>Its own seed (each scatter mixes in its own).</summary>
    public uint Salt;
    public Biome Biome;

    public Vector2 Gradient(Vector2 p)
    {
        const float e = 0.5f;
        return new Vector2(Height(p + new Vector2(e, 0f)) - Height(p - new Vector2(e, 0f)), Height(p + new Vector2(0f, e)) - Height(p - new Vector2(0f, e))) / (2f * e);
    }

    /// <summary>The same ground, with its decoration kept only where keep holds.</summary>
    public DecorGround Keeping(Func<Vector2, bool> keep)
    {
        var blocked = Blocked;
        return new DecorGround { Height = Height, Blocked = (p, m) => !keep(p) || blocked(p, m), Places = Places, Lo = Lo, Hi = Hi, Salt = Salt, Biome = Biome };
    }

    public bool NearPlace(Vector2 p, float more) => Places.Exists(q => Vector2.Distance(q.At, p) < q.Radius + more);

    /// <summary>A level's square: inside its rim, off its shafts (and a boss level's rift), its POIs its places.</summary>
    public static DecorGround Of(LevelMap map) => new()
    {
        Height = map.HeightAt,
        Blocked = (p, m) => map.InShaft(p, m) || map.HasBoss && Vector2.Distance(p, map.Exit.Position) < map.Exit.Radius * 0.6f,
        Places = map.Pois.ConvertAll(q => (q.Position, q.Radius)),
        Lo = new Vector2(LevelMap.RimWidth + 1f),
        Hi = new Vector2(map.Size - LevelMap.RimWidth - 1f),
        Salt = unchecked((uint)(map.Seed ^ (map.Seed >> 32)) ^ (uint)(map.Level * 7919 + map.Depth * 104729 + map.Cycle * 15485863 + map.Id.Branch * 977)),
        Biome = map.Templated ? map.Biome : Biome.SunlitReef,
    };

    /// <summary>
    /// An open level's chunk: its own ground (a secret room's without its walls, and none on them, so nothing floats when
    /// they open), off its holes, its place its one place.
    /// </summary>
    public static DecorGround Of(OpenSea sea, OpenSea.Chunk c)
    {
        var open = c.Unsealed ?? c.Heights;
        float At(float[] h, int x, int y) =>
            h[Math.Clamp(y - (int)c.Origin.Y, 0, c.Rows - 1) * c.Stride + Math.Clamp(x - (int)c.Origin.X, 0, c.Stride - 1)];
        float At2(float[] h, int x, int y) =>
            c.Shown(h, Math.Clamp(y - (int)c.Origin.Y, 0, c.Rows - 1) * c.Stride + Math.Clamp(x - (int)c.Origin.X, 0, c.Stride - 1));
        float Height(Vector2 p)
        {
            int ix = (int)MathF.Floor(p.X), iy = (int)MathF.Floor(p.Y);
            float tx = p.X - ix, ty = p.Y - iy;
            float a = At2(open, ix, iy) + (At2(open, ix + 1, iy) - At2(open, ix, iy)) * tx;
            float b = At2(open, ix, iy + 1) + (At2(open, ix + 1, iy + 1) - At2(open, ix, iy + 1)) * tx;
            return a + (b - a) * ty;
        }
        bool Wall(Vector2 p)
        {
            if (c.Unsealed is null) return false;
            int ix = (int)MathF.Round(p.X), iy = (int)MathF.Round(p.Y);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (At(c.Heights, ix + dx, iy + dy) != At(open, ix + dx, iy + dy)) return true;
            return false;
        }
        var places = new List<(Vector2, float)>();
        if (c.Place is not null) places.Add((c.PlaceAt, 6f));
        return new DecorGround
        {
            Height = Height,
            Blocked = (p, m) => c.Holes.Exists(h => Vector2.Distance(h, p) < 3f + m) || Wall(p),
            Places = places,
            Lo = c.Min,
            Hi = c.Max,
            Salt = unchecked((uint)(sea.Seed ^ (sea.Seed >> 32))),
            Biome = sea.Biome,
        };
    }
}
