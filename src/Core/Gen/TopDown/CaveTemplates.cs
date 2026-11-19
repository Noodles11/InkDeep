using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>What a mob mark asks for (docs/proposals/CROSSOVER.md §2): the level's roster fills it with a creature.</summary>
public enum MobTag { Lone, Pack, Thrower, Ambusher, Guard }

/// <summary>
/// The crossover's cell templates (docs/proposals/CROSSOVER.md §2): hand-drawn ASCII cells of <see cref="Cell"/> metres
/// a side, one grid point to a character, every one of them also used turned and mirrored. A cell opens on a side
/// only through its socket (<see cref="SocketFrom"/>–<see cref="SocketTo"/> along that side), so any two cells that
/// share an open side line up; every other point on its edge is rock.
/// <para>
/// The legend: <c>#</c> rock, <c>.</c> water, <c>c</c> a chunk slot (a 5 × 5 block, filled from <see cref="Chunks"/>),
/// mob marks <c>m</c> lone, <c>p</c> pack, <c>t</c> thrower, <c>a</c> ambusher, <c>g</c> guard (<see cref="MobTag"/>),
/// <c>*</c> a POI mark (a spot where a place can stand), and in a room cell <c>R</c>, the middle of the room behind its
/// spur (a place that lives in a cave: a treasure clam, a Crab Deal). Marks are water.
/// </para>
/// </summary>
public static class CaveTemplates
{
    /// <summary>A cell's side (metres, grid points), and its socket: the open stretch of an open side.</summary>
    public const int Cell = 18, SocketFrom = 6, SocketTo = 11;
    /// <summary>A chunk slot's side.</summary>
    public const int Chunk = 5;

    /// <summary>Openings, as a mask: north (−y), east (+x), south (+y), west (−x).</summary>
    public const int N = 1, E = 2, S = 4, W = 8;

    /// <summary>One cell as drawn, turned and mirrored into one of its eight ways.</summary>
    public sealed class Variant
    {
        public string Name = "";
        public char[,] Grid = new char[Cell, Cell];
        public int Openings;
        public bool Room;
        /// <summary>Its chunk slots' top-left corners.</summary>
        public List<(int X, int Y)> Slots = new();
        public char this[int x, int y] => Grid[x, y];
    }

    /// <summary>Whether a character of a cell is water (anything but rock).</summary>
    public static bool Water(char c) => c != '#';

    public static MobTag? TagOf(char c) => c switch
    {
        'm' => MobTag.Lone,
        'p' => MobTag.Pack,
        't' => MobTag.Thrower,
        'a' => MobTag.Ambusher,
        'g' => MobTag.Guard,
        _ => null,
    };

    /// <summary>The Sunlit Reef's cells, as drawn (each in one way; the generator uses all eight).</summary>
    public static readonly (string Name, string[] Rows)[] SunlitReef =
    {
        ("cross plaza", new[]
        {
            "######......######",
            "#####........#####",
            "###............###",
            "##..............##",
            "##...m..........##",
            "#.......ccccc....#",
            "........ccccc.....",
            "........ccccc..*..",
            "........ccccc.....",
            "........ccccc.....",
            "...p..............",
            "..................",
            "#................#",
            "##......t.......##",
            "##..............##",
            "###............###",
            "#####........#####",
            "######......######",
        }),
        ("cross pillars", new[]
        {
            "######......######",
            "####..........####",
            "##..............##",
            "##..##......##..##",
            "#..###..m...###..#",
            "#...#........#...#",
            "..................",
            "......p....*......",
            "........##........",
            "........##........",
            "..................",
            ".......t..........",
            "#...#........#...#",
            "#..###......###..#",
            "##..##......##..##",
            "##..............##",
            "####..........####",
            "######......######",
        }),
        ("bay", new[]
        {
            "##################",
            "######.....#######",
            "####.........#####",
            "###....m.......###",
            "##..............##",
            "#......ccccc.....#",
            ".......ccccc......",
            ".......ccccc......",
            ".......ccccc...*..",
            ".......ccccc......",
            "..................",
            "...........p......",
            "#................#",
            "##....a.......####",
            "####.........#####",
            "#####........#####",
            "######......######",
            "######......######",
        }),
        ("fork", new[]
        {
            "##################",
            "##################",
            "###.....####...###",
            "##...m...##.....##",
            "#.........#..*...#",
            "#................#",
            "..................",
            "........##........",
            "......#####.......",
            "......######......",
            ".......####.......",
            "..................",
            "##.......p......##",
            "####..........####",
            "#####........#####",
            "#####.t......#####",
            "######......######",
            "######......######",
        }),
        ("bend", new[]
        {
            "##################",
            "##################",
            "#######...########",
            "####.......#######",
            "###.....m....#####",
            "##.............###",
            "...............###",
            "......ccccc.....##",
            "......ccccc..*..##",
            "......ccccc.....##",
            "......ccccc....###",
            "......ccccc....###",
            "#..............###",
            "##....p.......####",
            "###..........#####",
            "#####........#####",
            "######......######",
            "######......######",
        }),
        ("lobes", new[]
        {
            "##################",
            "#########....#####",
            "#######.........##",
            "######....m.....##",
            "####......##....##",
            "##.......####..*##",
            "..........###...##",
            "..............####",
            "....t........#####",
            "............######",
            "...........#######",
            "..........########",
            "#.........########",
            "##........########",
            "###.......p...####",
            "####............##",
            "######......######",
            "######......######",
        }),
        ("cavern", new[]
        {
            "##################",
            "#######....#######",
            "####..........####",
            "##.....m........##",
            "#.......ccccc....#",
            "#.......ccccc....#",
            "........ccccc.....",
            "........ccccc.....",
            "...p....ccccc..*..",
            "..................",
            "..................",
            "........t.........",
            "#................#",
            "##..............##",
            "####....a.....####",
            "######......######",
            "##################",
            "##################",
        }),
        ("snake", new[]
        {
            "##################",
            "##################",
            "##########....####",
            "######.........###",
            "####......m.....##",
            "###....#####....##",
            "......#######.....",
            ".....########.....",
            "....#########..*..",
            "....#######.......",
            "..........##......",
            "...p..............",
            "##..............##",
            "###.....t....#####",
            "#####......#######",
            "##################",
            "##################",
            "##################",
        }),
        ("grotto", new[]
        {
            "##################",
            "##################",
            "######.....#######",
            "####.........#####",
            "###....*.......###",
            "##.............###",
            "##....ccccc.....##",
            "#.....ccccc.....##",
            "#.....ccccc..g...#",
            "#.....ccccc......#",
            "##....ccccc.....##",
            "##..............##",
            "###....a.......###",
            "####..........####",
            "#####........#####",
            "######......######",
            "######......######",
            "######......######",
        }),
        ("pocket", new[]
        {
            "##################",
            "##################",
            "##################",
            "########...#######",
            "######.......#####",
            "#####....*....####",
            "#####.........####",
            "######...g....####",
            "#######......#####",
            "########.....#####",
            "#######......#####",
            "######.......#####",
            "######...m...#####",
            "#####.........####",
            "#####.........####",
            "######......######",
            "######......######",
            "######......######",
        }),
    };

    /// <summary>
    /// The room cells (a dead end holding a place in its own cave): rock round a short spur from the socket to the room's
    /// mouth, the room's middle at <c>R</c> (the generator hollows the room out once the rock stands).
    /// </summary>
    public static readonly (string Name, string[] Rows)[] Rooms =
    {
        ("room", new[]
        {
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            "########R#########",
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            "#######....#######",
            "######......######",
            "######......######",
            "######......######",
        }),
    };

    /// <summary>The chunks a slot is filled from (each also turned and mirrored): rock to swim round, now and then a mark.</summary>
    public static readonly string[][] Chunks =
    {
        new[] { ".....", ".....", ".....", ".....", "....." },
        new[] { ".....", ".##..", ".###.", "..##.", "....." },
        new[] { ".....", "..#..", ".###.", "..#..", "....." },
        new[] { "##...", "#....", ".....", "....#", "...##" },
        new[] { ".....", "..m..", ".....", ".#...", "....." },
        new[] { ".....", ".##..", ".##*.", ".....", "....." },
        new[] { ".....", ".#.#.", ".....", ".#.#.", "....." },
        new[] { "...#.", "..##.", ".....", ".##..", "....." },
    };

    static char[,] Parse(string[] rows, int size)
    {
        if (rows.Length != size) throw new InvalidOperationException($"a template of {rows.Length} rows (want {size})");
        var g = new char[size, size];
        for (int y = 0; y < size; y++)
        {
            if (rows[y].Length != size) throw new InvalidOperationException($"a template row of {rows[y].Length} characters (want {size})");
            for (int x = 0; x < size; x++) g[x, y] = rows[y][x];
        }
        return g;
    }

    /// <summary>One of the eight ways of a square grid: turned <paramref name="turns"/> quarter turns, mirrored first if asked.</summary>
    public static char[,] Transform(char[,] g, int way)
    {
        int n = g.GetLength(0);
        var r = new char[n, n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            var (tx, ty) = Way(x, y, n, way);
            r[tx, ty] = g[x, y];
        }
        return r;
    }

    /// <summary>Where a point of an n-square goes in one of its eight ways (0–3 turns, 4–7 mirrored then turned).</summary>
    public static (int X, int Y) Way(int x, int y, int n, int way)
    {
        if (way >= 4) x = n - 1 - x;
        for (int k = 0; k < (way & 3); k++) (x, y) = (n - 1 - y, x);
        return (x, y);
    }

    /// <summary>A cell's openings as drawn, or why it breaks the socket rule.</summary>
    public static (int Openings, string? Why) OpeningsOf(char[,] g)
    {
        int open = 0;
        foreach (var (side, bit) in new[] { (0, N), (1, E), (2, S), (3, W) })
        {
            int water = 0, socket = 0;
            for (int k = 0; k < Cell; k++)
            {
                var (x, y) = side switch { 0 => (k, 0), 1 => (Cell - 1, k), 2 => (k, Cell - 1), _ => (0, k) };
                bool w = Water(g[x, y]);
                bool inSocket = k >= SocketFrom && k <= SocketTo;
                if (w && !inSocket) return (0, $"water on its edge outside the socket ({x}, {y})");
                if (w) water++;
                if (inSocket) socket++;
            }
            if (water == 0) continue;
            if (water != socket) return (0, "a socket only partly open");
            open |= bit;
        }
        return (open, null);
    }

    static List<Variant>? _sunlit, _rooms;
    static List<char[,]>? _chunks;

    /// <summary>Every way of every cell (the same way twice kept once).</summary>
    public static IReadOnlyList<Variant> SunlitVariants => _sunlit ??= Variants(SunlitReef, false);
    public static IReadOnlyList<Variant> RoomVariants => _rooms ??= Variants(Rooms, true);
    public static IReadOnlyList<char[,]> ChunkVariants => _chunks ??= Chunks
        .SelectMany(c => Enumerable.Range(0, 8).Select(w => Transform(Parse(c, Chunk), w)))
        .ToList();

    static List<Variant> Variants((string Name, string[] Rows)[] cells, bool room)
    {
        var list = new List<Variant>();
        var seen = new HashSet<string>();
        foreach (var (name, rows) in cells)
        {
            var g = Parse(rows, Cell);
            for (int way = 0; way < 8; way++)
            {
                var t = Transform(g, way);
                string key = new(Enumerable.Range(0, Cell * Cell).Select(i => t[i % Cell, i / Cell]).ToArray());
                if (!seen.Add(key)) continue;
                var (open, why) = OpeningsOf(t);
                if (why is not null) throw new InvalidOperationException($"The cave template \"{name}\" breaks the socket rule: {why}");
                var v = new Variant { Name = name, Grid = t, Openings = open, Room = room };
                FindSlots(v);
                list.Add(v);
            }
        }
        return list;
    }

    /// <summary>Its chunk slots: each 5 × 5 block of <c>c</c>, by its top-left corner.</summary>
    static void FindSlots(Variant v)
    {
        var taken = new bool[Cell, Cell];
        for (int y = 0; y < Cell; y++)
        for (int x = 0; x < Cell; x++)
        {
            if (v[x, y] != 'c' || taken[x, y]) continue;
            for (int j = 0; j < Chunk; j++)
            for (int i = 0; i < Chunk; i++)
            {
                if (x + i >= Cell || y + j >= Cell || v[x + i, y + j] != 'c')
                    throw new InvalidOperationException($"The cave template \"{v.Name}\" has a chunk slot that is not a 5 × 5 block at ({x}, {y})");
                taken[x + i, y + j] = true;
            }
            v.Slots.Add((x, y));
        }
    }
}
