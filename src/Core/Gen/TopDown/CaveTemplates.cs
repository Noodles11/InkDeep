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
/// <c>*</c> a POI mark (a spot where a place can stand), <c>v</c> a bubble vault's mark (by a wall: the vault hangs in
/// the rock's lee), and in a room cell <c>R</c>, the middle of the room behind its
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
            "###v...........###",
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
            "##....a......v####",
            "####.........#####",
            "#####........#####",
            "######......######",
            "######......######",
        }),
        ("fork", new[]
        {
            "##################",
            "##################",
            "###....v####...###",
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
            "####......v#######",
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
            "...........v######",
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
            "##v.............##",
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
            "##.............v##",
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
            "####v........#####",
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
            "#####........v####",
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

    /// <summary>The Kelp Forest's cells (CROSSOVER.md §8): wide open water among a few rounded rocks, for its dense kelp beds to stand in.</summary>
    public static readonly (string Name, string[] Rows)[] KelpForest =
    {
        ("kelp glade", new[]
        {
            "######......######",
            "####..........####",
            "##............####",
            "##.....m......####",
            "#...........##...#",
            "#....ccccc..##...#",
            ".....ccccc........",
            ".....ccccc....*...",
            ".....ccccc........",
            ".....ccccc........",
            "..................",
            "...p.........v#...",
            "#............#####",
            "#.......t.......##",
            "##..............##",
            "##......a.......##",
            "####..........####",
            "######......######",
        }),
        ("kelp aisle", new[]
        {
            "##################",
            "####...######...##",
            "##.......##......#",
            "#....m...........#",
            "#.....ccccc......#",
            "#.....ccccc..*...#",
            "......ccccc.......",
            "......ccccc.......",
            "......ccccc.......",
            "........p.........",
            "......##..........",
            ".....####....t....",
            "##...###........##",
            "######.........###",
            "######..a....#####",
            "######......######",
            "######......######",
            "######......######",
        }),
        ("kelp lane", new[]
        {
            "##################",
            "##################",
            "###....#####...###",
            "#.....m..##......#",
            "#................#",
            "#................#",
            "....ccccc.....*...",
            "....ccccc.........",
            "....ccccc....p....",
            "....ccccc.........",
            "....ccccc.........",
            "..................",
            "#.......a........#",
            "#####.........v###",
            "#####.....########",
            "##################",
            "##################",
            "##################",
        }),
        ("kelp hollow", new[]
        {
            "##################",
            "##################",
            "######....########",
            "####........######",
            "###...m.......####",
            "##.............###",
            "......ccccc.....##",
            "......ccccc..*..##",
            "......ccccc.....##",
            "......ccccc......#",
            "......ccccc......#",
            "..........p......#",
            "##..............##",
            "###....a.......###",
            "#####........v####",
            "######......######",
            "######......######",
            "######......######",
        }),
        ("kelp nook", new[]
        {
            "##################",
            "##################",
            "##################",
            "#######.....######",
            "#####.........####",
            "####...*.......###",
            "###.....g......###",
            "###.....ccccc..###",
            "###.v...ccccc..###",
            "####....ccccc..###",
            "####....ccccc.####",
            "#####...ccccc.####",
            "#####...m.....####",
            "######.......#####",
            "######......######",
            "######......######",
            "######......######",
            "######......######",
        }),
    };

    /// <summary>The Shipwreck Graveyard's cells: square holds and decks, a beam or a crate stack in them, a keel down a long hull, its ribs along the walls. (Every passage at least 4 m wide: narrower ones close up under the reef's walls and its corridors.)</summary>
    public static readonly (string Name, string[] Rows)[] Shipwreck =
    {
        ("hold cross", new[]
        {
            "######......######",
            "######......######",
            "##..............##",
            "##.m............##",
            "##..........*...##",
            "##..............##",
            "..................",
            ".......###........",
            "......v###........",
            ".......###........",
            "..................",
            "..................",
            "##..............##",
            "##.t........p...##",
            "##..............##",
            "##..............##",
            "######......######",
            "######......######",
        }),
        ("keel", new[]
        {
            "##################",
            "##################",
            "##################",
            "####....##....####",
            "#................#",
            "#....m.....*.....#",
            "..................",
            "..................",
            "....##########....",
            "....##########....",
            "......v...........",
            "...............t..",
            "#....a....p......#",
            "#................#",
            "####....##....####",
            "##################",
            "##################",
            "##################",
        }),
        ("broken deck", new[]
        {
            "##################",
            "##################",
            "##.............###",
            "##.............###",
            "##..m......*...###",
            "##.............###",
            "........v......###",
            ".........#########",
            ".........#########",
            "...ccccc.......###",
            "...ccccc.......###",
            "...ccccc.......###",
            "##.ccccc....p..###",
            "##.ccccc.a.....###",
            "##.............###",
            "######......######",
            "######......######",
            "######......######",
        }),
        ("cargo bay", new[]
        {
            "##################",
            "##################",
            "##################",
            "#######....#######",
            "#######.m..#######",
            "#######....#######",
            "..................",
            "..................",
            "...t.........*....",
            "........##........",
            ".......v##........",
            "........##........",
            "##..............##",
            "##..p.........a.##",
            "##..............##",
            "##..............##",
            "######......######",
            "######......######",
        }),
        ("captains cabin", new[]
        {
            "##################",
            "##################",
            "##################",
            "####....##....####",
            "####.*..##....####",
            "####....##....####",
            "####....##....####",
            "####....##v...####",
            "####....##....####",
            "####..........####",
            "####.......g..####",
            "####.m........####",
            "####..........####",
            "####..........####",
            "######......######",
            "######......######",
            "######......######",
            "######......######",
        }),
    };

    /// <summary>The Sunken Ruins' cells: a forum and an atrium round their middle, a colonnade, a temple's cella over its steps, a shrine's niche, laid out square and even (passages at least 4 m wide).</summary>
    public static readonly (string Name, string[] Rows)[] SunkenRuins =
    {
        ("forum", new[]
        {
            "######......######",
            "######......######",
            "####..........####",
            "####..........####",
            "##..............##",
            "##..m.......*...##",
            "..................",
            "..................",
            ".......v##........",
            "........##........",
            "..................",
            "..................",
            "##..a.......p...##",
            "##......t.......##",
            "####..........####",
            "####..........####",
            "######......######",
            "######......######",
        }),
        ("colonnade", new[]
        {
            "##################",
            "##################",
            "##################",
            "##################",
            "#####....##....###",
            "#####....##....###",
            "......m....v*.....",
            "..................",
            "..................",
            "..................",
            "............p.....",
            "......t...........",
            "#####....##....###",
            "#####....##....###",
            "##################",
            "##################",
            "##################",
            "##################",
        }),
        ("atrium", new[]
        {
            "##################",
            "##################",
            "##################",
            "###............###",
            "###.m.......a..###",
            "###............###",
            "...............###",
            ".......####....###",
            "......v####....###",
            ".......####....###",
            ".......####....###",
            "...............###",
            "###.*.......p..###",
            "###............###",
            "###............###",
            "######......######",
            "######......######",
            "######......######",
        }),
        ("temple steps", new[]
        {
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            "##################",
            ".......v..........",
            "....m...*....t....",
            "..................",
            "..................",
            "..................",
            "..................",
            "##.......p......##",
            "##.a............##",
            "##..............##",
            "##..............##",
            "######......######",
            "######......######",
        }),
        ("shrine niche", new[]
        {
            "##################",
            "##################",
            "##################",
            "#####........#####",
            "#####...*....#####",
            "#####........#####",
            "#####...g....#####",
            "#######....#######",
            "#######v...#######",
            "#####........#####",
            "#####........#####",
            "#####...m....#####",
            "#####........#####",
            "#####........#####",
            "######......######",
            "######......######",
            "######......######",
            "######......######",
        }),
    };

    /// <summary>The Kelp Forest's chunks: open water, now and then a boulder or a mark.</summary>
    public static readonly string[][] KelpChunks =
    {
        new[] { ".....", ".....", ".....", ".....", "....." },
        new[] { ".....", "..#..", ".....", ".....", "....." },
        new[] { ".....", ".....", "...##", "...##", "....." },
        new[] { ".....", ".m...", ".....", "...#.", "....." },
        new[] { ".....", ".##..", ".##..", ".....", "....." },
    };

    /// <summary>The Shipwreck's chunks: crates and beams lying in the hold, a mark among them.</summary>
    public static readonly string[][] WreckChunks =
    {
        new[] { ".....", ".....", ".....", ".....", "....." },
        new[] { ".....", ".##..", ".##..", ".....", "....." },
        new[] { ".....", "..###", "..###", ".....", "....." },
        new[] { "##...", "##...", ".....", "...*.", "....." },
        new[] { ".....", ".###.", ".###.", ".....", "....." },
    };

    /// <summary>The Ruins' chunks: a column or two, a fallen block, a mark among them.</summary>
    public static readonly string[][] RuinsChunks =
    {
        new[] { ".....", ".##..", ".##..", ".....", "....." },
        new[] { ".....", ".....", "..##.", "..##.", "....." },
        new[] { ".....", "..*..", ".....", ".....", "....." },
        new[] { ".....", ".....", ".....", ".....", "....." },
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
    static readonly Dictionary<OctoShoots.Core.Run.Biome, List<Variant>> _byBiome = new();
    static readonly Dictionary<OctoShoots.Core.Run.Biome, List<char[,]>> _chunksByBiome = new();

    /// <summary>A biome's cells, every way of each (the Sunlit Reef's own for the Sunlit Reef).</summary>
    public static IReadOnlyList<Variant> VariantsOf(OctoShoots.Core.Run.Biome biome)
    {
        if (biome == OctoShoots.Core.Run.Biome.SunlitReef) return SunlitVariants;
        lock (_byBiome)
        {
            if (!_byBiome.TryGetValue(biome, out var list))
                _byBiome[biome] = list = Variants(biome switch
                {
                    OctoShoots.Core.Run.Biome.KelpForest => KelpForest,
                    OctoShoots.Core.Run.Biome.Shipwreck => Shipwreck,
                    _ => SunkenRuins,
                }, false);
            return list;
        }
    }

    /// <summary>A biome's chunks, every way of each (the common ones for the Sunlit Reef).</summary>
    public static IReadOnlyList<char[,]> ChunksOf(OctoShoots.Core.Run.Biome biome)
    {
        if (biome == OctoShoots.Core.Run.Biome.SunlitReef) return ChunkVariants;
        lock (_chunksByBiome)
        {
            if (!_chunksByBiome.TryGetValue(biome, out var list))
                _chunksByBiome[biome] = list = (biome switch
                {
                    OctoShoots.Core.Run.Biome.KelpForest => KelpChunks,
                    OctoShoots.Core.Run.Biome.Shipwreck => WreckChunks,
                    _ => RuinsChunks,
                }).SelectMany(c => Enumerable.Range(0, 8).Select(w => Transform(Parse(c, Chunk), w))).ToList();
            return list;
        }
    }

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
