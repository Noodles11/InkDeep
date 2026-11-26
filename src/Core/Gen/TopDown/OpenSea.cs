using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// An open level (docs/GAME.md §5, Open levels): an endless sea of 2 × 1 chunks laid in a herringbone over a grid of
/// CaveTemplates.Cell cells, every chunk made only when asked for (<see cref="Build"/>, safe on any thread) and always the
/// same from the same seed, so nothing of the level is made before she nears it.
/// <para>
/// The pattern: with d = x − y + shift (mod 4) for a cell (x, y), a cell at d 0 is a lying chunk's left half (its right
/// half at d 1), at d 3 a standing chunk's top (its foot at d 2). A chunk's two cells are always joined. Two neighbouring
/// chunks are joined (the socket in the middle of every side they share opened through their walls) when either takes the other as its parent (each chunk but the one
/// holding cell (0, 0) has one: a neighbour nearer that cell, so every chunk reaches it and has a way out), or now and
/// then besides (ChunkLink). Each cell's template is one made for its openings (CaveTemplates, turned and mirrored to
/// fit; the biome's own, else the Sunlit Reef's), its chunk slots filled from the cell's own seed; its look comes from
/// the biome. Built, a chunk's water is eroded and raised into rock exactly as wherever it is asked from: what
/// it reads round it (Context) is more than any distance it uses, so neighbouring chunks meet without a seam.
/// </para>
/// </summary>
public sealed class OpenSea
{
    public const int Cell = CaveTemplates.Cell;
    /// <summary>Two neighbouring chunks not joined by the tree are joined this often.</summary>
    public const float ChunkLink = 0.55f;
    /// <summary>
    /// How often each kind of place lies in the sea: at most one in AtMostOnceIn chunks and at least one in AtLeastOnceIn.
    /// The sea is cut into regions of about twice AtLeastOnceIn chunks for each kind (Region), and each region rolls how many it
    /// holds, from as many as keep that "at least" to as many as the "at most" allows, each in a chunk of its own and
    /// never two side by side. A chunk holds one place at most: where two kinds want one, the kind higher in this list
    /// has it. Her first chunk and those round it hold none.
    /// </summary>
    public static readonly IReadOnlyList<PlaceRule> PlaceRules = new PlaceRule[]
    {
        new(PoiKind.Hole, 6, 12),
        new(PoiKind.Shop, 5, 10),
        new(PoiKind.TreasureCave, 4, 8),
        new(PoiKind.ShellCache, 6, 12),
        new(PoiKind.Shrine, 8, 16),
        new(PoiKind.CrabDeal, 10, 20),
        new(PoiKind.Relic, 25, 50),
    };

    /// <summary>One kind of place's chances (see PlaceRules).</summary>
    public sealed record PlaceRule(PoiKind Kind, int AtMostOnceIn, int AtLeastOnceIn);

    readonly ConcurrentDictionary<(int Rule, int X, int Y), HashSet<(int X, int Y)>> _chosen = new();

    /// <summary>The place a chunk holds (by the rules), or none.</summary>
    public PoiKind? PlaceOf((int X, int Y) key)
    {
        if (Near(key) || key == Lair) return null;
        for (int r = 0; r < PlaceRules.Count; r++)
            if (Chosen(r, key).Contains(key)) return PlaceRules[r].Kind;
        return null;
    }

    /// <summary>Her first chunk and those touching it.</summary>
    bool Near((int X, int Y) key)
    {
        var home = ChunkOfCell((0, 0));
        return key == home || Neighbours(home).Contains(key);
    }

    /// <summary>A rule's region (a block of cells holding about twice AtLeastOnceIn chunks), by a chunk's first cell.</summary>
    static (int W, int H) Region(PlaceRule rule)
    {
        int cells = 4 * rule.AtLeastOnceIn;
        int w = (int)MathF.Ceiling(MathF.Sqrt(cells));
        return (w, (int)MathF.Ceiling(cells / (float)w));
    }

    /// <summary>The chunks a rule has chosen in the region holding a chunk (none taken by a rule above it, none side by side).</summary>
    HashSet<(int X, int Y)> Chosen(int r, (int X, int Y) key)
    {
        var rule = PlaceRules[r];
        var (w, h) = Region(rule);
        int bx = (int)MathF.Floor(key.X / (float)w), by = (int)MathF.Floor(key.Y / (float)h);
        return _chosen.GetOrAdd((r, bx, by), _ =>
        {
            var inside = new List<(int X, int Y)>();
            for (int y = by * h; y < (by + 1) * h; y++)
            for (int x = bx * w; x < (bx + 1) * w; x++)
                if (ChunkOfCell((x, y)) == (x, y)) inside.Add((x, y));
            var rng = new Rng(Mix(Seed, (ulong)r, (ulong)(uint)bx, (ulong)(uint)by, 0x9A1CE));
            int least = Math.Max(1, (int)MathF.Round(inside.Count / (float)rule.AtLeastOnceIn));
            int most = Math.Max(least, inside.Count / rule.AtMostOnceIn);
            int want = least + rng.Int(most - least + 1);
            for (int i = inside.Count - 1; i > 0; i--)
            {
                int j = rng.Int(i + 1);
                (inside[i], inside[j]) = (inside[j], inside[i]);
            }
            var chosen = new HashSet<(int X, int Y)>();
            foreach (var c in inside)
            {
                if (chosen.Count >= want) break;
                if (Near(c) || Neighbours(c).Any(chosen.Contains)) continue;
                // (Nor one touching a later region, row by row: of two across a border, one never holds it.)
                if (Neighbours(c).Any(n =>
                    {
                        int nx = (int)MathF.Floor(n.X / (float)w), ny = (int)MathF.Floor(n.Y / (float)h);
                        return ny > by || ny == by && nx > bx;
                    })) continue;
                bool taken = false;
                for (int above = 0; above < r && !taken; above++) taken = Chosen(above, c).Contains(c);
                if (!taken) chosen.Add(c);
            }
            return chosen;
        });
    }
    /// <summary>Heights kept past a chunk's edge (its mesh's spline and normals), and water read past that (its distances).</summary>
    public const int Pad = 3, Context = 12;
    /// <summary>The window's height where no chunk is built yet: just over her plane (it stops her, and casts next to no shade).</summary>
    public const float Unbuilt = 0.3f;
    /// <summary>How thick a chunk's wall is along each side it shares with another (so two meet as a wall twice as thick), how much it wanders (its face's noise), and how much of it the erosion never opens.</summary>
    const float Wall = 2f, WallWander = 1.8f, WallCore = 1f;
    /// <summary>The erosion's two octaves (as a template level's; its walls are not pushed back: between chunks they are the ways shut).</summary>
    const float Erode = 1.5f, ErodeFine = 0.6f;
    /// <summary>The floor, and the rock's top (each give or take its noise).</summary>
    const float Floor = -1f, Top = 3f;
    /// <summary>A hole: its opening, its slope, its sag at the rim, its lip; how far its middle keeps from the chunk's edge (its slope then ends Pad inside it: a neighbour's padding never holds it), and how much room it needs.</summary>
    /// <summary>How much room a place needs round its POI mark (metres to the rock).</summary>
    const float PlaceRoom = 3f;
    /// <summary>How far round the lair's hole the water is opened (metres).</summary>
    const float LairPool = 5f;
    const float HoleOpening = 2.4f, HoleSlope = 3f, HoleSag = 1.2f, HoleLip = 1.4f, HoleKeep = 9f, HoleRoom = 3.5f;

    public readonly ulong Seed;
    public readonly OctoShoots.Core.Run.Biome Biome;
    readonly int _shift;
    readonly uint _noise;
    readonly ConcurrentDictionary<(int X, int Y), char[,]> _cells = new();
    readonly ConcurrentDictionary<(int X, int Y), (int X, int Y)?> _parents = new();

    public OpenSea(ulong seed, OctoShoots.Core.Run.Biome biome)
    {
        Seed = seed;
        Biome = biome;
        _shift = (int)(Mix(seed, 0x5EA) % 4);
        _noise = (uint)(Mix(seed, 0xE40DE) >> 40);
        float a = Mix(seed, 0x1A12) % 3600 / 3600f * MathF.Tau;
        Lair = ChunkOfCell(((int)MathF.Round(MathF.Cos(a) * LairCells), (int)MathF.Round(MathF.Sin(a) * LairCells)));
    }

    /// <summary>
    /// The level's one way to its boss (the depth's arena, met alone): a hole down in this chunk, LairCells from her
    /// start in a direction of the sea's own. It holds no other place.
    /// </summary>
    public readonly (int X, int Y) Lair;
    /// <summary>How far the lair lies from her first cell (cells).</summary>
    const int LairCells = 5;

    // ───────────────────────── the pattern ─────────────────────────

    static int Mod(int a, int m) => (a % m + m) % m;

    /// <summary>The cell a point lies in.</summary>
    public static (int X, int Y) CellOf(Vector2 p) => ((int)MathF.Floor(p.X / Cell), (int)MathF.Floor(p.Y / Cell));

    /// <summary>The chunk a cell belongs to (by its first cell: a lying chunk's left, a standing one's top).</summary>
    public (int X, int Y) ChunkOfCell((int X, int Y) c) => Mod(c.X - c.Y + _shift, 4) switch
    {
        1 => (c.X - 1, c.Y),
        2 => (c.X, c.Y - 1),
        _ => c,
    };

    public (int X, int Y) ChunkAt(Vector2 p) => ChunkOfCell(CellOf(p));

    /// <summary>Whether a chunk lies (two cells side by side) or stands (one over the other).</summary>
    public bool Lying((int X, int Y) key) => Mod(key.X - key.Y + _shift, 4) == 0;

    public (int X, int Y)[] CellsOf((int X, int Y) key) =>
        Lying(key) ? new[] { key, (key.X + 1, key.Y) } : new[] { key, (key.X, key.Y + 1) };

    /// <summary>A chunk's square in the sea (metres).</summary>
    public (Vector2 Min, Vector2 Max) RectOf((int X, int Y) key)
    {
        var min = new Vector2(key.X * Cell, key.Y * Cell);
        return (min, min + (Lying(key) ? new Vector2(2 * Cell, Cell) : new Vector2(Cell, 2 * Cell)));
    }

    static readonly (int DX, int DY, int Bit)[] Sides = { (0, -1, CaveTemplates.N), (1, 0, CaveTemplates.E), (0, 1, CaveTemplates.S), (-1, 0, CaveTemplates.W) };

    /// <summary>The chunks touching a chunk along a side, in a fixed order.</summary>
    public List<(int X, int Y)> Neighbours((int X, int Y) key)
    {
        var cells = CellsOf(key);
        var list = new List<(int X, int Y)>();
        foreach (var c in cells)
        foreach (var (dx, dy, _) in Sides)
        {
            var n = (c.X + dx, c.Y + dy);
            if (cells.Contains(n)) continue;
            var k = ChunkOfCell(n);
            if (!list.Contains(k)) list.Add(k);
        }
        list.Sort();
        return list;
    }

    static int Dist((int X, int Y) key, (int X, int Y)[] cells) => cells.Min(c => Math.Abs(c.X) + Math.Abs(c.Y));

    /// <summary>A chunk's parent: a neighbour nearer cell (0, 0), chosen by its seed (none for the chunk holding it).</summary>
    public (int X, int Y)? Parent((int X, int Y) key) => _parents.GetOrAdd(key, k =>
    {
        int d = Dist(k, CellsOf(k));
        if (d == 0) return null;
        var nearer = Neighbours(k).Where(n => Dist(n, CellsOf(n)) < d).ToList();
        return nearer[(int)(Mix(Seed, (ulong)(uint)k.X, (ulong)(uint)k.Y, 0x9A2E) % (ulong)nearer.Count)];
    });

    /// <summary>Whether two neighbouring chunks are joined (the same asked from either).</summary>
    public bool Linked((int X, int Y) a, (int X, int Y) b)
    {
        if (Parent(a) == b || Parent(b) == a) return true;
        var (lo, hi) = a.CompareTo(b) < 0 ? (a, b) : (b, a);
        return Unit(Mix(Seed, (ulong)(uint)lo.X, (ulong)(uint)lo.Y, (ulong)(uint)hi.X, (ulong)(uint)hi.Y)) < ChunkLink;
    }

    /// <summary>A cell's openings (CaveTemplates.N/E/S/W): to its chunk's other cell, and to every joined neighbour.</summary>
    public int Openings((int X, int Y) cell)
    {
        var key = ChunkOfCell(cell);
        int open = 0;
        foreach (var (dx, dy, bit) in Sides)
        {
            var n = ChunkOfCell((cell.X + dx, cell.Y + dy));
            if (n == key || Linked(key, n)) open |= bit;
        }
        return open;
    }

    /// <summary>A cell's template, its chunk slots filled (from the cell's own seed), and its name.</summary>
    (char[,] Grid, string Name) CellGrid((int X, int Y) cell)
    {
        string name = "";
        var grid = _cells.GetOrAdd(cell, c =>
        {
            int open = Openings(c);
            var rng = new Rng(Mix(Seed, (ulong)(uint)c.X, (ulong)(uint)c.Y, 0xCE11));
            var pool = CaveTemplates.VariantsOf(Biome).Where(v => v.Openings == open).ToList();
            if (pool.Count == 0) pool = CaveTemplates.SunlitVariants.Where(v => v.Openings == open).ToList();
            var v = pool[rng.Int(pool.Count)];
            var g = (char[,])v.Grid.Clone();
            var chunks = CaveTemplates.ChunksOf(Biome);
            foreach (var (sx, sy) in v.Slots)
            {
                var chunk = chunks[rng.Int(chunks.Count)];
                for (int j = 0; j < CaveTemplates.Chunk; j++)
                for (int k = 0; k < CaveTemplates.Chunk; k++)
                    g[sx + k, sy + j] = chunk[k, j];
            }
            _names[c] = v.Name;
            return g;
        });
        _names.TryGetValue(cell, out name!);
        return (grid, name ?? "");
    }

    readonly ConcurrentDictionary<(int, int), string> _names = new();

    // ───────────────────────── a chunk ─────────────────────────

    /// <summary>One chunk as built: its heights (a metre apart, Pad past its edge), its holes down, its template.</summary>
    public sealed class Chunk
    {
        public (int X, int Y) Key;
        public Vector2 Min, Max;
        /// <summary>Its heights: Stride a row, the first sample at Origin (Min − Pad).</summary>
        public float[] Heights = Array.Empty<float>();
        public int Stride, Rows;
        public Vector2 Origin;
        public readonly List<Vector2> Holes = new();
        /// <summary>The place it holds (PlaceOf; a hole is in Holes as well), and where: on one of its cells' POI marks in open water, else where its water is widest.</summary>
        public PoiKind? Place;
        public Vector2 PlaceAt;
        /// <summary>The lair's hole (the way to the boss), if this chunk is the lair (it is in Holes as well).</summary>
        public Vector2? Lair;
        /// <summary>Its template: its cells' templates by name, and how many neighbours it is joined to.</summary>
        public string Template = "";
        public int Ways;
        /// <summary>
        /// Its ways out (sockets): on the edge it shares with a joined chunk, the middle of the way (sea metres), the way
        /// out of it, the way's half width, and whether it is this chunk's to fill (each way is one chunk's: a kelp curtain).
        /// </summary>
        public readonly List<(Vector2 At, Vector2 Way, float Half, bool Own)> Sockets = new();
        /// <summary>A secret room (OpenSea.SecretChance of the chunks holding a treasure): its ways out sealed (a wall of its own heights; Unsealed, its heights without it).</summary>
        public bool Sealed => Unsealed is not null;
        public float[]? Unsealed;
        /// <summary>Its kelp beds (sea metres).</summary>
        public readonly List<KelpBed> Kelp = new();

        /// <summary>Its seal opened: its heights without the wall (the sea's window is filled again from them).</summary>
        public void Unseal()
        {
            if (Unsealed is null) return;
            Heights = Unsealed;
            Unsealed = null;
        }

        public bool Contains(Vector2 p) => p.X >= Min.X && p.Y >= Min.Y && p.X < Max.X && p.Y < Max.Y;

        /// <summary>The height at a sample (sea metres, whole), clamped to what it holds.</summary>
        public float At(int x, int y)
        {
            int i = Math.Clamp(x - (int)Origin.X, 0, Stride - 1), j = Math.Clamp(y - (int)Origin.Y, 0, Rows - 1);
            return Heights[j * Stride + i];
        }
    }

    /// <summary>Builds one chunk. Pure: safe on any thread; the same chunk every time.</summary>
    public Chunk Build((int X, int Y) key)
    {
        var (min, max) = RectOf(key);
        int m = Pad + Context;
        int x0 = (int)min.X - m, y0 = (int)min.Y - m, w = (int)(max.X - min.X) + 2 * m + 1, h = (int)(max.Y - min.Y) + 2 * m + 1;
        int n = w * h;
        // The cells' water, as their templates draw it.
        var raw = new bool[n];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int x = x0 + i, y = y0 + j;
            var (grid, _) = CellGrid((FloorDiv(x), FloorDiv(y)));
            int lx = Mod(x, Cell), ly = Mod(y, Cell);
            char c = grid[lx, ly];
            raw[j * w + i] = CaveTemplates.Water(c) && c != 'R';
        }
        // Eroded with noise in the sea's own coordinates, then smoothed twice.
                var inRock = Chamfer(raw, w, h, false);
        var outRock = Chamfer(raw, w, h, true);
        var open = new bool[n];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int k = j * w + i, x = x0 + i, y = y0 + j;
            float sd = raw[k] ? 0.5f - inRock[k] : outRock[k] - 0.5f;
            open[k] = sd + CaveLayout.Fbm(x * 0.13f, y * 0.13f, _noise) * Erode + CaveLayout.Fbm(x * 0.41f, y * 0.41f, _noise + 31) * ErodeFine < 0f;
        }
        // The chunk's walls, after the erosion so it never eats them: a band along every side it shares with another
        // chunk but the joined ones' sockets, its face wandering with its own noise.
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int x = x0 + i, y = y0 + j;
            float wall = MathF.Max(WallCore, Wall + WallWander * CaveLayout.Fbm(x * 0.11f, y * 0.11f, _noise + 17));
            if (open[j * w + i] && InWall((FloorDiv(x), FloorDiv(y)), Mod(x, Cell), Mod(y, Cell), wall)) open[j * w + i] = false;
        }
        for (int pass = 0; pass < 2; pass++)
        {
            var next = (bool[])open.Clone();
            for (int j = 1; j < h - 1; j++)
            for (int i = 1; i < w - 1; i++)
            {
                int c = 0;
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                    if (open[(j + dj) * w + i + di]) c++;
                next[j * w + i] = c >= 5;
            }
            open = next;
        }
        // (The middle of a wall stays rock whatever the erosion did: a way between chunks opens only where they are joined.)
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int x = x0 + i, y = y0 + j;
            if (open[j * w + i] && InWall((FloorDiv(x), FloorDiv(y)), Mod(x, Cell), Mod(y, Cell), WallCore)) open[j * w + i] = false;
        }
        // Raised: the floor, rising round into the rock over a few metres.
        var toRock = Chamfer(open, w, h, false);
        // The lair: its hole where the water is widest, the water opened round it so it always has the room.
        Vector2? lair = null;
        if (key == Lair)
        {
            int at = -1;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var p = new Vector2(x0 + i, y0 + j);
                if (p.X < min.X + HoleKeep || p.Y < min.Y + HoleKeep || p.X > max.X - HoleKeep || p.Y > max.Y - HoleKeep) continue;
                if (open[j * w + i] && (at < 0 || toRock[j * w + i] > toRock[at])) at = j * w + i;
            }
            if (at >= 0)
            {
                int ci = at % w, cj = at / w;
                for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    if ((i - ci) * (i - ci) + (j - cj) * (j - cj) <= LairPool * LairPool && !InWall((FloorDiv(x0 + i), FloorDiv(y0 + j)), Mod(x0 + i, Cell), Mod(y0 + j, Cell), WallCore))
                        open[j * w + i] = true;
                toRock = Chamfer(open, w, h, false);
                lair = new Vector2(x0 + ci, y0 + cj);
            }
        }
        var toOpen = Chamfer(open, w, h, true);
        var chunk = new Chunk { Key = key, Min = min, Max = max, Origin = min - new Vector2(Pad), Stride = (int)(max.X - min.X) + 2 * Pad + 1, Rows = (int)(max.Y - min.Y) + 2 * Pad + 1 };
        chunk.Heights = new float[chunk.Stride * chunk.Rows];
        for (int j = 0; j < chunk.Rows; j++)
        for (int i = 0; i < chunk.Stride; i++)
        {
            int gi = i + Context, gj = j + Context, k = gj * w + gi;
            float x = x0 + gi, y = y0 + gj;
            float s = open[k] ? 0.5f - toRock[k] : toOpen[k] - 0.5f;
            float floor = Floor + 0.12f * CaveLayout.Fbm(x * 0.21f, y * 0.21f, _noise + 5);
            float top = Top + 1.2f * CaveLayout.Fbm(x * 0.05f, y * 0.05f, _noise + 9);
            float hgt = floor + (top - floor) * Smooth(-0.8f, 3f, s);
            // (Rock always stands over her plane and water always lies under it, however thin: a wall is a wall.)
            chunk.Heights[j * chunk.Stride + i] = open[k] ? MathF.Min(hgt, -0.3f) : MathF.Max(hgt, 0.3f);
        }
        // Its place, if it holds one: a hole down where its water is widest, well inside it (so no neighbour's heights
        // change); anything else on one of its POI marks with room round it, else where its water is widest.
        chunk.Place = PlaceOf(key);
        if (chunk.Place is { } kind && kind != PoiKind.Hole)
        {
            var marks = new List<Vector2>();
            Vector2 widest = (min + max) * 0.5f;
            float room = -1f;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var p = new Vector2(x0 + i, y0 + j);
                if (!chunk.Contains(p) || p.X < min.X + 4f || p.Y < min.Y + 4f || p.X > max.X - 4f || p.Y > max.Y - 4f) continue;
                int k = j * w + i;
                if (!open[k]) continue;
                if (toRock[k] > room + 0.01f)
                {
                    room = toRock[k];
                    widest = p;
                }
                if (toRock[k] >= PlaceRoom && CellGrid((FloorDiv((int)p.X), FloorDiv((int)p.Y))).Grid[Mod((int)p.X, Cell), Mod((int)p.Y, Cell)] == '*') marks.Add(p);
            }
            chunk.PlaceAt = marks.Count > 0 ? marks[(int)(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x91AC) % (ulong)marks.Count)] : widest;
        }
        if (chunk.Place == PoiKind.Hole)
        {
            float best = HoleRoom;
            Vector2? at = null;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var p = new Vector2(x0 + i, y0 + j);
                if (p.X < min.X + HoleKeep || p.Y < min.Y + HoleKeep || p.X > max.X - HoleKeep || p.Y > max.Y - HoleKeep) continue;
                int k = j * w + i;
                if (open[k] && toRock[k] > best + 0.01f)
                {
                    best = toRock[k];
                    at = p;
                }
            }
            if (at is { } c)
            {
                DigHole(chunk, c, new Rng(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x401F)));
                chunk.PlaceAt = c;
            }
            else chunk.Place = null;
        }
        if (lair is { } l)
        {
            DigHole(chunk, l, new Rng(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x1A13)));
            chunk.Lair = l;
        }
        // Its ways out; and the walls of any secret room (its own, or a neighbour's standing in its padding), so all meet.
        chunk.Sockets.AddRange(SocketsOf(key));
        var near = new HashSet<(int X, int Y)>();
        for (int cy = FloorDiv((int)chunk.Origin.Y); cy <= FloorDiv((int)chunk.Origin.Y + chunk.Rows); cy++)
        for (int cx = FloorDiv((int)chunk.Origin.X); cx <= FloorDiv((int)chunk.Origin.X + chunk.Stride); cx++)
            near.Add(ChunkOfCell((cx, cy)));
        near.Remove(key);
        foreach (var k in near)
            if (Secret(k)) RaiseSeals(chunk, k);
        if (Secret(key))
        {
            chunk.Unsealed = (float[])chunk.Heights.Clone();
            RaiseSeals(chunk, key);
        }
        PlaceKelp(chunk, key, toRock, x0, y0, w);
        var names = CellsOf(key).Select(c => CellGrid(c).Name);
        chunk.Template = string.Join("+", names);
        chunk.Ways = Neighbours(key).Count(o => Linked(key, o));
        return chunk;
    }

    /// <summary>A sealed way: its wall's half thickness (it stands inside the dead end, from its edge in), how far past the way it reaches either side, and its top.</summary>
    public const float SealThick = 1.2f, SealPast = 1.5f, SealTop = 2f;
    /// <summary>The share of the chunks holding a treasure that are secret rooms.</summary>
    public const float SecretChance = 0.3f;

    /// <summary>A secret room: a chunk holding a treasure (a clam, a cache, a relic), by SecretChance; never the lair or her first.</summary>
    public bool Secret((int X, int Y) key) =>
        PlaceOf(key) is PoiKind.TreasureCave or PoiKind.ShellCache or PoiKind.Relic && key != Lair && key != ChunkOfCell((0, 0))
        && Unit(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x5EA1)) < SecretChance;

    /// <summary>A chunk's ways out: the middle of each on the edge it shares with a joined chunk, the way out, its half width, and whether it is this chunk's.</summary>
    public List<(Vector2 At, Vector2 Way, float Half, bool Own)> SocketsOf((int X, int Y) key)
    {
        var ways = new List<(Vector2, Vector2, float, bool)>();
        float mid = (CaveTemplates.SocketFrom + CaveTemplates.SocketTo + 1) * 0.5f, half = (CaveTemplates.SocketTo - CaveTemplates.SocketFrom + 1) * 0.5f + 1f;
        foreach (var c in CellsOf(key))
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nb = ChunkOfCell((c.X + dx, c.Y + dy));
                if (nb == key || !Linked(key, nb)) continue;
                var at = dx != 0 ? new Vector2((c.X + (dx > 0 ? 1 : 0)) * Cell, c.Y * Cell + mid) : new Vector2(c.X * Cell + mid, (c.Y + (dy > 0 ? 1 : 0)) * Cell);
                ways.Add((at, new Vector2(dx, dy), half, nb.X > key.X || nb.X == key.X && nb.Y > key.Y));
            }
        return ways;
    }

    /// <summary>A secret room opened: its walls gone from another chunk's padding too (the room's own heights there).</summary>
    public void UnsealAround(Chunk room, Chunk other)
    {
        var (min, max) = RectOf(room.Key);
        for (int j = 0; j < other.Rows; j++)
        for (int i = 0; i < other.Stride; i++)
        {
            var p = other.Origin + new Vector2(i, j);
            if (p.X < min.X || p.Y < min.Y || p.X >= max.X || p.Y >= max.Y) continue;
            float h = room.At((int)p.X, (int)p.Y);
            other.Heights[j * other.Stride + i] = h;
            if (other.Unsealed is { } u) u[j * other.Stride + i] = h;
        }
    }

    /// <summary>A secret room's walls across its ways (inside it, from its edge in) raised in a chunk's heights, padding and all.</summary>
    void RaiseSeals(Chunk chunk, (int X, int Y) room)
    {
        var (min, max) = RectOf(room);
        foreach (var (at, way, half, _) in SocketsOf(room))
            for (int j = 0; j < chunk.Rows; j++)
            for (int i = 0; i < chunk.Stride; i++)
            {
                var p = chunk.Origin + new Vector2(i, j);
                if (p.X < min.X || p.Y < min.Y || p.X >= max.X || p.Y >= max.Y) continue;
                var d = p - at;
                float inward = -Vector2.Dot(d, way), across = MathF.Abs(d.X * -way.Y + d.Y * way.X);
                if (inward >= -0.5f && inward <= SealThick * 2f && across <= half + SealPast)
                    chunk.Heights[j * chunk.Stride + i] = MathF.Max(chunk.Heights[j * chunk.Stride + i], SealTop);
            }
    }

    /// <summary>
    /// A chunk's kelp beds, as a template level's (TopDownGenerator.PlaceKelpIn): by its chance (the Kelp Forest's more
    /// and thicker), in open water away from its holes and its place, rooted on the floor.
    /// </summary>
    void PlaceKelp(Chunk chunk, (int X, int Y) key, float[] toRock, int x0, int y0, int w)
    {
        var rng = new Rng(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x6E1F));
        bool forest = Biome == OctoShoots.Core.Run.Biome.KelpForest;
        int beds = (rng.NextFloat() < (forest ? 0.55f : 0.25f) ? 1 : 0) + (forest && rng.NextFloat() < 0.2f ? 1 : 0);
        uint seed = (uint)(rng.NextU64() >> 32);
        for (int tried = 0; tried < 60 && chunk.Kelp.Count < beds; tried++)
        {
            var c = new Vector2(rng.Range(chunk.Min.X + 4f, chunk.Max.X - 4f), rng.Range(chunk.Min.Y + 4f, chunk.Max.Y - 4f));
            int ci = ((int)MathF.Round(c.Y) - y0) * w + (int)MathF.Round(c.X) - x0;
            if (toRock[ci] < TopDownGenerator.KelpClear || chunk.At((int)MathF.Round(c.X), (int)MathF.Round(c.Y)) > LevelMap.BlockHeight - 0.5f) continue;
            float r = rng.Range(TopDownGenerator.KelpRadiusMin, TopDownGenerator.KelpRadiusMax);
            if (chunk.Holes.Exists(h => Vector2.Distance(h, c) < r + 6f) || chunk.Place is not null && Vector2.Distance(chunk.PlaceAt, c) < r + 8f) continue;
            if (chunk.Kelp.Exists(k => Vector2.Distance(k.Center, c) < k.Radius + r + TopDownGenerator.KelpApart)) continue;
            var bed = new KelpBed { Center = c, Radius = r };
            for (float y = -r; y <= r; y += TopDownGenerator.KelpSpacing)
            for (float x = -r; x <= r; x += TopDownGenerator.KelpSpacing)
            {
                var p = c + new Vector2(x + rng.Range(-0.45f, 0.45f) * TopDownGenerator.KelpSpacing, y + rng.Range(-0.45f, 0.45f) * TopDownGenerator.KelpSpacing);
                float edge = r * (0.8f + 0.3f * TerrainNoise.Fbm(p.X * 0.22f, p.Y * 0.22f, seed, 2));
                float d = Vector2.Distance(p, c) / MathF.Max(edge, 0.5f);
                if (d > 1f || rng.NextFloat() > 1f - 0.65f * d * d) continue;
                if (!chunk.Contains(p) || chunk.At((int)MathF.Round(p.X), (int)MathF.Round(p.Y)) > LevelMap.BlockHeight - 0.3f) continue;
                bed.Roots.Add(p);
                bed.Lengths.Add(rng.Range(TopDownGenerator.KelpLengthMin, TopDownGenerator.KelpLengthMax));
            }
            if (bed.Roots.Count >= TopDownGenerator.KelpLeast) chunk.Kelp.Add(bed);
        }
    }

    /// <summary>
    /// Whether a point of a cell (lx, ly within it) is in its chunk's wall: <paramref name="wall"/> metres along every side it shares with
    /// another chunk, but for the socket in the middle of a side where the two are joined (the way between them).
    /// </summary>
    bool InWall((int X, int Y) cell, int lx, int ly, float wall)
    {
        var key = ChunkOfCell(cell);
        bool Shut(int dx, int dy, int along)
        {
            var n = ChunkOfCell((cell.X + dx, cell.Y + dy));
            return n != key && (along < CaveTemplates.SocketFrom - 1 || along > CaveTemplates.SocketTo + 1 || !Linked(key, n));
        }
        return lx < wall && Shut(-1, 0, ly) || lx >= Cell - wall && Shut(1, 0, ly) || ly < wall && Shut(0, -1, lx) || ly >= Cell - wall && Shut(0, 1, lx);
    }

    static int FloorDiv(int v) => v >= 0 ? v / Cell : -((-v + Cell - 1) / Cell);

    /// <summary>A hole down (as a template level's, TopDownGenerator.OpenHoles): the floor sags into it, its rim wavering, its lip rounding over into the drop.</summary>
    static void DigHole(Chunk chunk, Vector2 c, Rng rng)
    {
        float p1 = rng.Range(0f, MathF.Tau), p2 = rng.Range(0f, MathF.Tau), stretch = rng.Range(0.08f, 0.16f);
        float Rim(float a) => HoleOpening * (1f + stretch * MathF.Sin(2f * a + p1) + 0.07f * MathF.Sin(3f * a + p2));
        for (int j = 0; j < chunk.Rows; j++)
        for (int i = 0; i < chunk.Stride; i++)
        {
            int k = j * chunk.Stride + i;
            float hgt = chunk.Heights[k];
            if (hgt > 0f) continue;
            var d = chunk.Origin + new Vector2(i, j) - c;
            float beyond = d.Length() - Rim(MathF.Atan2(d.Y, d.X));
            if (beyond > HoleSlope) continue;
            float sag = hgt - HoleSag * Square(1f - Smooth(0f, HoleSlope, beyond));
            float drop = 1f - Smooth(-HoleLip, 0.4f, beyond);
            chunk.Heights[k] = MathF.Min(hgt, sag + (LevelMap.ShaftBottom + 0.8f - sag) * drop);
        }
        chunk.Holes.Add(c);
    }

    // ───────────────────────── a window of the sea for the sim ─────────────────────────

    /// <summary>
    /// The sea as a level's map round a point (its window: <paramref name="origin"/> its corner, in whole cells): heights
    /// from the chunks built (Unbuilt where none is yet), and their holes, copied a chunk at a time. With
    /// <paramref name="only"/>, only that chunk's heights are copied (it has just been built); the holes are always
    /// taken afresh. Returns how many samples had no chunk yet.
    /// </summary>
    public int Fill(LevelMap map, Vector2 origin, Func<(int X, int Y), Chunk?> chunkOf, (int X, int Y)? only = null)
    {
        int missing = 0, n = map.Samples, ox = (int)origin.X, oy = (int)origin.Y;
        map.Holes.Clear();
        map.Kelp.Clear();
        var keys = new HashSet<(int X, int Y)>();
        for (int cy = FloorDiv(oy); cy <= FloorDiv(oy + n - 1); cy++)
        for (int cx = FloorDiv(ox); cx <= FloorDiv(ox + n - 1); cx++)
            keys.Add(ChunkOfCell((cx, cy)));
        foreach (var key in keys)
        {
            var chunk = chunkOf(key);
            if (chunk is not null)
                foreach (var hole in chunk.Holes)
                    if (hole.X - ox is > 4f and < LevelMap.Seeded - 4f && hole.Y - oy is > 4f and < LevelMap.Seeded - 4f)
                        map.Holes.Add(new MapHole { Shaft = new Shaft(hole - origin, Vector2.UnitX, 3f, 3f), Kind = hole == chunk.Lair ? OctoShoots.Core.Run.ReefKind.Boss : default });
            // (Its kelp beds, those wholly in the window, in its coordinates.)
            if (chunk is not null)
                foreach (var bed in chunk.Kelp)
                    if (bed.Center.X - bed.Radius - ox > 0f && bed.Center.Y - bed.Radius - oy > 0f && bed.Center.X + bed.Radius - ox < n - 1 && bed.Center.Y + bed.Radius - oy < n - 1)
                        map.Kelp.Add(new KelpBed { Center = bed.Center - origin, Radius = bed.Radius, Roots = bed.Roots.Select(q => q - origin).ToList(), Lengths = bed.Lengths });
            if (only is { } o && o != key) continue;
            var (min, max) = RectOf(key);
            int x0 = Math.Max((int)min.X, ox), x1 = Math.Min((int)max.X, ox + n), y0 = Math.Max((int)min.Y, oy), y1 = Math.Min((int)max.Y, oy + n);
            for (int y = y0; y < y1; y++)
            {
                int row = (y - oy) * n - ox;
                if (chunk is null)
                {
                    for (int x = x0; x < x1; x++) map.Heights[row + x] = Unbuilt;
                    missing += x1 - x0;
                    continue;
                }
                int src = (y - (int)chunk.Origin.Y) * chunk.Stride - (int)chunk.Origin.X;
                Array.Copy(chunk.Heights, src + x0, map.Heights, row + x0, x1 - x0);
            }
        }
        map.Shaft = map.Holes.Count > 0 ? map.Holes[0].Shaft : new Shaft(new Vector2(-100f, -100f), Vector2.UnitX, 0.01f, 0.01f);
        return missing;
    }

    // ───────────────────────── helpers ─────────────────────────

    static float Smooth(float a, float b, float x) => MathUtil.Smoothstep(MathUtil.Clamp01((x - a) / (b - a)));
    static float Square(float x) => x * x;
    static float Unit(ulong h) => (h >> 40) * (1f / 16777216f);

    static ulong Mix(ulong seed, params ulong[] parts)
    {
        ulong h = seed ^ 0x9E3779B97F4A7C15UL;
        foreach (var p in parts)
        {
            h ^= p + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2);
            h ^= h >> 31;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 29;
        }
        return h;
    }

    /// <summary>Distances (3–4 chamfer, in metres) to the nearest cell that is (or is not, <paramref name="to"/> false) set.</summary>
    static float[] Chamfer(bool[] set, int w, int h, bool to)
    {
        var d = new float[w * h];
        for (int k = 0; k < d.Length; k++) d[k] = set[k] == to ? 0f : 1e6f;
        const float A = 1f, B = 1.41421356f;
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int k = j * w + i;
            float v = d[k];
            if (i > 0) v = MathF.Min(v, d[k - 1] + A);
            if (j > 0)
            {
                v = MathF.Min(v, d[k - w] + A);
                if (i > 0) v = MathF.Min(v, d[k - w - 1] + B);
                if (i < w - 1) v = MathF.Min(v, d[k - w + 1] + B);
            }
            d[k] = v;
        }
        for (int j = h - 1; j >= 0; j--)
        for (int i = w - 1; i >= 0; i--)
        {
            int k = j * w + i;
            float v = d[k];
            if (i < w - 1) v = MathF.Min(v, d[k + 1] + A);
            if (j < h - 1)
            {
                v = MathF.Min(v, d[k + w] + A);
                if (i < w - 1) v = MathF.Min(v, d[k + w + 1] + B);
                if (i > 0) v = MathF.Min(v, d[k + w - 1] + B);
            }
            d[k] = v;
        }
        return d;
    }
}
