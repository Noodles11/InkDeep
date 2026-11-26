using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// An area template: a small room drawn by hand ('#' rock, '.' water, '*' where its place stands, in water), a metre a
/// character, pressed into an open level's reef round a place (OpenSea.PlaceRules) and blended into the rock round it.
/// The chunk's ways are carved on to its place, so a template needs no openings of its own.
/// </summary>
public sealed class AreaTemplate
{
    public readonly string Name;
    public readonly int Size;
    /// <summary>Its place's mark, from its middle (metres).</summary>
    public readonly Vector2 Mark;
    readonly float[] _sdf;

    public AreaTemplate(string name, params string[] rows)
    {
        Name = name;
        Size = rows.Length;
        var water = new bool[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            char c = rows[y][x];
            water[y * Size + x] = c != '#';
            if (c == '*') Mark = new Vector2(x, y) - new Vector2((Size - 1) * 0.5f);
        }
        // (Signed: below 0 in its water, the metres to its rock; above 0 in its rock, the metres to its water.)
        var toRock = OpenSea.Distances(water, Size, Size, false);
        var toWater = OpenSea.Distances(water, Size, Size, true);
        _sdf = new float[Size * Size];
        for (int k = 0; k < _sdf.Length; k++) _sdf[k] = water[k] ? 0.5f - toRock[k] : toWater[k] - 0.5f;
    }

    /// <summary>Its signed distance at a point from its middle (metres, turned a quarter Turn times), rock past its square.</summary>
    public float At(Vector2 p, int turn)
    {
        for (int t = 0; t < (turn & 3); t++) p = new Vector2(p.Y, -p.X);
        float fx = p.X + (Size - 1) * 0.5f, fy = p.Y + (Size - 1) * 0.5f;
        if (fx < 0f || fy < 0f || fx > Size - 1 || fy > Size - 1) return 3f;
        int x = Math.Min((int)fx, Size - 2), y = Math.Min((int)fy, Size - 2);
        float u = fx - x, v = fy - y;
        float a = _sdf[y * Size + x], b = _sdf[y * Size + x + 1], c = _sdf[(y + 1) * Size + x], d = _sdf[(y + 1) * Size + x + 1];
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    /// <summary>Its place's mark turned as At turns it.</summary>
    public Vector2 MarkTurned(int turn)
    {
        var m = Mark;
        // (At turns the point back by the turn: the mark goes the other way.)
        for (int t = 0; t < (turn & 3); t++) m = new Vector2(-m.Y, m.X);
        return m;
    }
}

/// <summary>The area templates by name (PlaceRule.Templates).</summary>
public static class AreaTemplates
{
    public static readonly IReadOnlyDictionary<string, AreaTemplate> ByName = new AreaTemplate[]
    {
        // A hall held up by four pillars (a shrine).
        new("hall",
            "#############",
            "#####...#####",
            "###.......###",
            "##.........##",
            "##..#...#..##",
            "#...........#",
            "#.....*.....#",
            "#...........#",
            "##..#...#..##",
            "##.........##",
            "###.......###",
            "#####...#####",
            "#############"),
        // A grotto with an alcove off it (a shop, a crab's deal).
        new("grotto",
            "#############",
            "########...##",
            "######......#",
            "####........#",
            "###......####",
            "##.......####",
            "##....*...###",
            "##.........##",
            "###........##",
            "####.......##",
            "#####....####",
            "#############",
            "#############"),
        // A round chamber ringed by rocks (a clam's).
        new("chamber",
            "#############",
            "####.....####",
            "##.........##",
            "##..#...#..##",
            "#...........#",
            "#.#.......#.#",
            "#.....*.....#",
            "#.#.......#.#",
            "#...........#",
            "##..#...#..##",
            "##.........##",
            "####.....####",
            "#############"),
        // A crypt: a narrow chamber with niches either side (a relic's).
        new("crypt",
            "#############",
            "#####...#####",
            "###.......###",
            "###.##.##.###",
            "##.........##",
            "###.##.##.###",
            "##....*....##",
            "###.##.##.###",
            "##.........##",
            "###.##.##.###",
            "###.......###",
            "#####...#####",
            "#############"),
    }.ToDictionary(t => t.Name);
}
