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
    const float Wall = 1f, WallWander = 2.2f, WallCore = 1f;
    /// <summary>
    /// How far a wall's face swells and shrinks over a few tens of metres besides (so it is never a straight line along
    /// the chunks' edge, but reef), and how much a way through it widens past its socket (never past a seal's reach).
    /// </summary>
    const float WallLump = 5f, GapWiden = 1.4f;
    /// <summary>The erosion's two octaves (as a template level's; its walls are not pushed back: between chunks they are the ways shut).</summary>
    const float Erode = 1.5f, ErodeFine = 0.6f;
    /// <summary>How far at most the erosion grows rock into a template's water (metres).</summary>
    const float ErodeIn = 0.9f;
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
    public bool Linked((int X, int Y) a, (int X, int Y) b) => FillOf(a) != Filler.Full && FillOf(b) != Filler.Full && Joined(a, b);

    /// <summary>Whether two neighbouring chunks are joined by the tree or by chance (before any chunk is filled whole).</summary>
    bool Joined((int X, int Y) a, (int X, int Y) b)
    {
        if (Parent(a) == b || Parent(b) == a) return true;
        var (lo, hi) = a.CompareTo(b) < 0 ? (a, b) : (b, a);
        return Unit(Mix(Seed, (ulong)(uint)lo.X, (ulong)(uint)lo.Y, (ulong)(uint)hi.X, (ulong)(uint)hi.Y)) < ChunkLink;
    }

    /// <summary>How much of a chunk is solid rock over its template: none, a quarter (half a cell), a half (a cell), or all of it.</summary>
    public enum Filler { None, Quarter, Half, Full }

    /// <summary>
    /// The share of chunks filled whole (only one no other chunk takes as its parent: it is joined to none, so nothing
    /// lies past it), by half and by a quarter (their ways kept open to their middle through it).
    /// </summary>
    public const float FullChance = 0.15f, HalfChance = 0.15f, QuarterChance = 0.15f;

    readonly ConcurrentDictionary<(int X, int Y), (Filler Kind, int Cell)> _fills = new();

    /// <summary>A chunk's filler (never her first chunk or those round it, the lair, or a chunk holding a place).</summary>
    public Filler FillOf((int X, int Y) key) => Fill(key).Kind;

    /// <summary>
    /// A chunk's filler and the cell it fills: a half fills a cell with one way out at most (its rock opened from it to
    /// the chunk's middle), a quarter the far half of a cell with no way out at its end.
    /// </summary>
    (Filler Kind, int Cell) Fill((int X, int Y) key) => _fills.GetOrAdd(key, k =>
    {
        var none = (Filler.None, 0);
        if (Near(k) || k == Lair || PlaceOf(k) is not null) return none;
        float u = Unit(Mix(Seed, (ulong)(uint)k.X, (ulong)(uint)k.Y, 0xF111));
        if (u < FullChance) return Neighbours(k).Any(n => Parent(n) == k) ? none : (Filler.Full, 0);
        if (u >= FullChance + HalfChance + QuarterChance) return none;
        var cells = CellsOf(k);
        bool Out((int X, int Y) cell, int dx, int dy)
        {
            var n = ChunkOfCell((cell.X + dx, cell.Y + dy));
            return n != k && Joined(k, n);
        }
        int first = (int)(Mix(Seed, (ulong)(uint)k.X, (ulong)(uint)k.Y, 0xF112) & 1);
        for (int t = 0; t < 2; t++)
        {
            int c = (first + t) % 2;
            var cell = cells[c];
            if (u < FullChance + HalfChance)
            {
                if (Sides.Count(s => Out(cell, s.DX, s.DY)) <= 1) return (Filler.Half, c);
            }
            else
            {
                // (Its end: the side away from its other cell.)
                var (dx, dy) = Lying(k) ? (c == 0 ? -1 : 1, 0) : (0, c == 0 ? -1 : 1);
                if (!Out(cell, dx, dy)) return (Filler.Quarter, c);
            }
        }
        return none;
    });

    /// <summary>A filler's rock: its chunk, whether it fills its second cell, its middle, and its ways to its middle.</summary>
    sealed record FillShape(Filler Kind, Vector2 Min, bool Lying, bool End, Vector2 Hub, (Vector2 A, Vector2 B, float Bend)[] Ways);

    readonly ConcurrentDictionary<(int X, int Y), FillShape> _shapes = new();

    FillShape ShapeOf((int X, int Y) key) => _shapes.GetOrAdd(key, k =>
    {
        var (kind, cell) = Fill(k);
        var (min, max) = RectOf(k);
        var hub = (min + max) * 0.5f;
        // (Each of its filled cell's ways bowing in to that cell's middle, and on from there straight to the chunk's.)
        var ways = new List<(Vector2, Vector2, float)>();
        if (kind is Filler.Half or Filler.Quarter)
        {
            var filled = CellsOf(k)[cell];
            var middle = new Vector2(filled.X * Cell + Cell * 0.5f, filled.Y * Cell + Cell * 0.5f);
            foreach (var s in SocketsOf(k))
                if (CellOf(s.At - s.Way * 0.5f) == filled)
                    ways.Add((s.At + s.Way * 2f, middle, FillBend * CaveLayout.Fbm(s.At.X * 0.37f, s.At.Y * 0.37f, _noise + 97)));
            if (ways.Count > 0) ways.Add((middle, hub, 0f));
        }
        return new FillShape(kind, min, Lying(k), cell == 1, hub, ways.ToArray());
    });

    /// <summary>How far a filler's rock edge wanders, its ways' half width (and wander), and the room kept at its middle.</summary>
    const float FillWobble = 4f, FillWay = 2.8f, FillWayWobble = 1f, FillHub = 3.5f;

    /// <summary>Whether a point of a filler chunk is water: its template's, but where its rock stands; always on its ways to its middle.</summary>
    bool FillWater(FillShape f, Vector2 p, bool water)
    {
        if (f.Kind == Filler.Full) return false;
        var q = p - f.Min;
        float along = f.Lying ? q.X : q.Y;
        if (f.End) along = 2 * Cell - along;
        // (Its other cell is as its template draws it; in its filled cell, the ways run on to its middle through the rock.)
        if (along >= Cell) return water;
        float n = CaveLayout.Fbm(p.X * 0.18f, p.Y * 0.18f, _noise + 61);
        if (Vector2.Distance(p, f.Hub) < FillHub + n) return true;
        foreach (var (a, b, bend) in f.Ways)
            if (WayDistance(p, a, b, bend) < FillWay + FillWayWobble * n) return true;
        float edge = CaveLayout.Fbm(p.X * 0.07f, p.Y * 0.07f, _noise + 63);
        bool rock = f.Kind == Filler.Half ? along < Cell - FillWobble * 0.5f * (1f + edge) : along < Cell * 0.5f + 1.5f + FillWobble * 0.5f * edge;
        return water && !rock;
    }

    /// <summary>How far at most a chunk filled whole bulges into its neighbours' water (so it is a rock, not a box).</summary>
    const float FullBulge = 7f;

    /// <summary>Whether a point near its cell's edge is under the bulge of a neighbouring chunk filled whole.</summary>
    bool Bulge((int X, int Y) cell, int lx, int ly, int x, int y)
    {
        int dxs = lx < FullBulge ? -1 : lx >= Cell - FullBulge ? 1 : 0, dys = ly < FullBulge ? -1 : ly >= Cell - FullBulge ? 1 : 0;
        if (dxs == 0 && dys == 0) return false;
        var own = ChunkOfCell(cell);
        var p = new Vector2(x, y);
        for (int dy = Math.Min(dys, 0); dy <= Math.Max(dys, 0); dy++)
        for (int dx = Math.Min(dxs, 0); dx <= Math.Max(dxs, 0); dx++)
        {
            var n = ChunkOfCell((cell.X + dx, cell.Y + dy));
            if (n == own || FillOf(n) != Filler.Full) continue;
            var (min, max) = RectOf(n);
            float d = Vector2.Distance(p, Vector2.Clamp(p, min, max));
            if (d < FullBulge * MathUtil.Clamp01(0.4f + 1.1f * CaveLayout.Fbm(x * 0.08f, y * 0.08f, _noise + 91))) return true;
        }
        return false;
    }

    /// <summary>How far a filler's way bows out of a straight line between its ends (metres at most).</summary>
    const float FillBend = 4f;

    /// <summary>A point's distance from a way bowed into an arc (by bend, at its middle; its ends where they are).</summary>
    static float WayDistance(Vector2 p, Vector2 a, Vector2 b, float bend)
    {
        var ab = b - a;
        var across = Vector2.Normalize(new Vector2(-ab.Y, ab.X));
        // (Near enough for an arc this shallow: its point abreast of p along the chord, pushed out by the bow.)
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return Vector2.Distance(p, a + ab * t + across * bend * MathF.Sin(MathF.PI * t));
    }

    /// <summary>
    /// The rock where four cells meet (their templates' corners and the walls' ends: a small square on the chunks' edges)
    /// is kept only this often, and only left out where no chunk round it is a filler or a secret room and every two of them
    /// side by side are joined (so no way opens that the sea has not): out to this far from the corner, give or take.
    /// </summary>
    public const float PillarKeep = 0.15f;
    const float PillarClear = 6.5f, PillarWobble = 1.5f;

    readonly ConcurrentDictionary<(int X, int Y), bool> _dropped = new();

    /// <summary>Whether the rock at a corner of the cells' grid (cell (X, Y)'s top left) is left out.</summary>
    bool Dropped((int X, int Y) corner) => _dropped.GetOrAdd(corner, c =>
    {
        if (Unit(Mix(Seed, (ulong)(uint)c.X, (ulong)(uint)c.Y, 0xC0E)) < PillarKeep) return false;
        var keys = new[] { (c.X - 1, c.Y - 1), (c.X, c.Y - 1), (c.X - 1, c.Y), (c.X, c.Y) }.Select(ChunkOfCell).Distinct().ToList();
        foreach (var a in keys)
        {
            if (FillOf(a) != Filler.None || Secret(a)) return false;
            foreach (var b in keys)
                if (a.CompareTo(b) < 0 && Neighbours(a).Contains(b) && !Linked(a, b)) return false;
        }
        return true;
    });

    /// <summary>Whether a point (sea metres) is in the clearing round a corner whose rock is left out.</summary>
    bool Cleared(float x, float y)
    {
        int cx = (int)MathF.Round(x / Cell), cy = (int)MathF.Round(y / Cell);
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx * Cell, cy * Cell));
        return d < PillarClear + PillarWobble && Dropped((cx, cy)) && d < PillarClear + PillarWobble * CaveLayout.Fbm(x * 0.15f, y * 0.15f, _noise + 81);
    }

    /// <summary>
    /// How far the cells' templates are warped where they are read (metres at most, and the warp's scale): the sea's whole
    /// mosaic of them bent smoothly and alike from any chunk, so its rock's outline is the reef's own rather than the
    /// templates' squares and bevels; not round the middle of a cell's side (where a way's gap in the walls stands).
    /// </summary>
    const float WarpReach = 2.5f, WarpScale = 0.06f;

    Vector2 Warp(int x, int y)
    {
        // (How far from the nearest middle of a cell's side: the same lattice from any chunk.)
        float mx = Mod(x, Cell), my = Mod(y, Cell), half = Cell * 0.5f;
        float way = MathF.Min(MathF.Sqrt(Square(mx - half) + Square(MathF.Min(my, Cell - my))), MathF.Sqrt(Square(my - half) + Square(MathF.Min(mx, Cell - mx))));
        float a = WarpReach * Smooth(3f, 6.5f, way);
        if (a <= 0f) return Vector2.Zero;
        return new Vector2(CaveLayout.Fbm(x * WarpScale, y * WarpScale, _noise + 51), CaveLayout.Fbm(x * WarpScale, y * WarpScale, _noise + 53)) * a;
    }

    /// <summary>
    /// Rock this small (points, and metres across at most) standing alone in the water is left out but for this share of
    /// it (by its own seed): no scattered squares, only now and then a lone rock.
    /// </summary>
    const int Islet = 30, IsletSpan = 8;
    const float IsletKeep = 0.15f;

    /// <summary>Drops the small lone rocks (never a wall's, nor one near the edge of what was read: it may be larger past it).</summary>
    void DropIslets(bool[] open, int w, int h, int x0, int y0)
    {
        var seen = new bool[open.Length];
        var part = new List<int>();
        var stack = new Stack<int>();
        for (int k0 = 0; k0 < open.Length; k0++)
        {
            if (open[k0] || seen[k0]) continue;
            part.Clear();
            seen[k0] = true;
            stack.Push(k0);
            int iMin = int.MaxValue, iMax = int.MinValue, jMin = int.MaxValue, jMax = int.MinValue;
            while (stack.Count > 0)
            {
                int k = stack.Pop(), i = k % w, j = k / w;
                part.Add(k);
                iMin = Math.Min(iMin, i); iMax = Math.Max(iMax, i); jMin = Math.Min(jMin, j); jMax = Math.Max(jMax, j);
                if (i > 0 && !open[k - 1] && !seen[k - 1]) { seen[k - 1] = true; stack.Push(k - 1); }
                if (i < w - 1 && !open[k + 1] && !seen[k + 1]) { seen[k + 1] = true; stack.Push(k + 1); }
                if (j > 0 && !open[k - w] && !seen[k - w]) { seen[k - w] = true; stack.Push(k - w); }
                if (j < h - 1 && !open[k + w] && !seen[k + w]) { seen[k + w] = true; stack.Push(k + w); }
            }
            if (part.Count > Islet || iMax - iMin >= IsletSpan || jMax - jMin >= IsletSpan || iMin < 2 || jMin < 2 || iMax >= w - 2 || jMax >= h - 2) continue;
            // (Its seed from its first point, in the sea's own coordinates: the same from any chunk reading it.)
            int x = x0 + k0 % w, y = y0 + k0 / w;
            if (Unit(Mix(Seed, (ulong)(uint)x, (ulong)(uint)y, 0x15E7)) < IsletKeep) continue;
            if (part.Exists(k => InWall((FloorDiv(x0 + k % w), FloorDiv(y0 + k / w)), Mod(x0 + k % w, Cell), Mod(y0 + k / w, Cell), WallCore))) continue;
            foreach (int k in part) open[k] = true;
        }
    }

    /// <summary>
    /// The rock rising on past its foot into mountains: up to MountainPeak higher, from MountainFoot to MountainReach metres
    /// in from the water (less than Context: the same from any chunk), ridged.
    /// </summary>
    const float MountainPeak = 4.5f, MountainFoot = 2f, MountainReach = 10f;

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
        /// <summary>Its arches and its room's roof (sea metres): drawn with it, and copied into the window (the sun's shade).</summary>
        public readonly List<Canopy> Canopies = new();
        /// <summary>Its room's roof: the rock's height over it (as Heights), NaN where there is none; null without one.</summary>
        public float[]? Lid;

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
        var across = new Dictionary<((int, int), (int, int)), bool>();
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int x = x0 + i, y = y0 + j;
            var cell = (FloorDiv(x), FloorDiv(y));
            int lx = Mod(x, Cell), ly = Mod(y, Cell);
            var warp = Warp(x, y);
            int sx = x + (int)MathF.Round(warp.X), sy = y + (int)MathF.Round(warp.Y);
            var from = (FloorDiv(sx), FloorDiv(sy));
            // (Read across into a chunk not joined to its own, it is rock: their wall's side, never water drawn in.)
            if (!across.TryGetValue((cell, from), out bool readable))
            {
                var mine = ChunkOfCell(cell);
                var theirs = ChunkOfCell(from);
                across[(cell, from)] = readable = theirs == mine || Linked(mine, theirs);
            }
            char c = readable ? CellGrid(from).Grid[Mod(sx, Cell), Mod(sy, Cell)] : '#';
            bool water = CaveTemplates.Water(c) && c != 'R' || Cleared(x, y);
            var shape = ShapeOf(ChunkOfCell(cell));
            raw[j * w + i] = (shape.Kind == Filler.None ? water : FillWater(shape, new Vector2(x, y) + warp, water)) && !Bulge(cell, lx, ly, x, y);
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
            float erode = CaveLayout.Fbm(x * 0.13f, y * 0.13f, _noise) * Erode + CaveLayout.Fbm(x * 0.41f, y * 0.41f, _noise + 31) * ErodeFine;
            // (The rock grows only a little into the template's water: never across one of its narrow ways.)
            if (raw[k]) erode = MathF.Min(erode, ErodeIn);
            open[k] = sd + erode < 0f;
        }
        // The chunk's walls, after the erosion so it never eats them: a band along every side it shares with another
        // chunk but the joined ones' sockets, its face wandering with its own noise, swelling and shrinking more broadly
        // too (reef, not a line along the edge); what it walls is kept (its rock rises to the reef's top at once).
        var walled = new bool[n];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int x = x0 + i, y = y0 + j;
            // (Over the template's water its swell is held to WallOverWater: never shutting a way inside the chunk.)
            float wall = raw[j * w + i] ? Math.Clamp(Wall + WallWander * CaveLayout.Fbm(x * 0.11f, y * 0.11f, _noise + 17) + 1.5f * CaveLayout.Fbm(x * 0.06f, y * 0.06f, _noise + 23), WallCore, WallOverWater) : WallThick(x, y);
            if (InWall((FloorDiv(x), FloorDiv(y)), Mod(x, Cell), Mod(y, Cell), wall))
            {
                walled[j * w + i] = true;
                open[j * w + i] = false;
            }
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
        DropIslets(open, w, h, x0, y0);
        // (How far in from the water the rock lies, before the lair's pool: its mountains, the same from any chunk.)
        var inland = Chamfer(open, w, h, true);
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
            // (A wall between chunks, thin as it may be, stands at the reef's top: never a low strip along their edge.)
            float hgt = floor + (top - floor) * Smooth(-0.8f, walled[k] ? 1.4f : 3f, s);
            if (!open[k]) hgt += MountainPeak * Smooth(MountainFoot, MountainReach, inland[k] - 0.5f) * (0.55f + 0.45f * (1f - MathF.Abs(CaveLayout.Fbm(x * 0.08f, y * 0.08f, _noise + 71))));
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
        PlaceRoof(chunk, key, toRock, x0, y0, w);
        PlaceArches(chunk, key);
        PlaceKelp(chunk, key, toRock, x0, y0, w);
        var names = CellsOf(key).Select(c => CellGrid(c).Name);
        chunk.Template = string.Join("+", names);
        chunk.Ways = Neighbours(key).Count(o => Linked(key, o));
        return chunk;
    }

    /// <summary>The share of the chunks holding a clam, a cache or a relic whose room is roofed (a secret room's always is); its roof's reach past the water round its place, its least and most reach, and how much it domes up over the rock's top.</summary>
    public const float RoofChance = 0.5f, RoofPast = 2.5f, RoofMin = 4f, RoofMax = 9f, RoofDome = 1.5f;

    /// <summary>
    /// A treasure's room roofed, as a template level's caves are: the reef's rock carried on over it as a low dome (its
    /// own top's height, rising a little to the middle), out to a little past the water round its place, and never past
    /// its own chunk (its roof is drawn with it). Its canopy fades it while she is under it (LevelView).
    /// </summary>
    void PlaceRoof(Chunk chunk, (int X, int Y) key, float[] toRock, int x0, int y0, int w)
    {
        if (chunk.Place is not (PoiKind.TreasureCave or PoiKind.ShellCache or PoiKind.Relic)) return;
        if (!Secret(key) && Unit(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x400F)) >= RoofChance) return;
        var at = chunk.PlaceAt;
        float room = toRock[((int)at.Y - y0) * w + (int)at.X - x0];
        float edge = MathF.Min(MathF.Min(at.X - chunk.Min.X, chunk.Max.X - at.X), MathF.Min(at.Y - chunk.Min.Y, chunk.Max.Y - at.Y));
        float r = MathF.Min(Math.Clamp(room + RoofPast, RoofMin, RoofMax), edge - 1.5f);
        if (r < RoofMin) return;
        var ground = chunk.Unsealed ?? chunk.Heights;
        var lid = new float[chunk.Heights.Length];
        Array.Fill(lid, float.NaN);
        float most = 0f;
        for (int j = 0; j < chunk.Rows; j++)
        for (int i = 0; i < chunk.Stride; i++)
        {
            var p = chunk.Origin + new Vector2(i, j);
            float d = Vector2.Distance(p, at);
            if (d > r) continue;
            float top = Top + 1.2f * CaveLayout.Fbm(p.X * 0.05f, p.Y * 0.05f, _noise + 9) + RoofDome * (1f - Square(d / r));
            int k = j * chunk.Stride + i;
            lid[k] = MathF.Max(ground[k], top);
            most = MathF.Max(most, lid[k]);
        }
        chunk.Lid = lid;
        chunk.Canopies.Add(new Canopy { Kind = CanopyKind.CaveRoof, Center = at, Radius = r, Bottom = LevelMap.SwimBand + 1.2f, Top = most });
    }

    /// <summary>The share of chunks with an arch; its span's least and most (footing to footing), and the rock's least height at a footing.</summary>
    public const float ArchChance = 0.35f, ArchSpanMin = 6f, ArchSpanMax = 14f, ArchFooting = 2.2f;

    /// <summary>
    /// Now and then an arch, as a template level's: a span of rock across a passage, springing from the rock tops either
    /// side of it, footings and all inside its chunk (it is drawn with it), clear of its holes and its place.
    /// </summary>
    void PlaceArches(Chunk chunk, (int X, int Y) key)
    {
        var rng = new Rng(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0xA4C4));
        if (rng.NextFloat() >= ArchChance) return;
        var ground = chunk.Unsealed ?? chunk.Heights;
        float H(Vector2 p) => ground[Math.Clamp((int)MathF.Round(p.Y - chunk.Origin.Y), 0, chunk.Rows - 1) * chunk.Stride + Math.Clamp((int)MathF.Round(p.X - chunk.Origin.X), 0, chunk.Stride - 1)];
        bool Inside(Vector2 p) => p.X > chunk.Min.X + 1f && p.Y > chunk.Min.Y + 1f && p.X < chunk.Max.X - 1f && p.Y < chunk.Max.Y - 1f;
        var keep = new List<(Vector2 At, float Radius)>();
        foreach (var hole in chunk.Holes) keep.Add((hole, 9f));
        if (chunk.Place is not null) keep.Add((chunk.PlaceAt, 6f));
        foreach (var c in chunk.Canopies) keep.Add((c.Center, c.Radius + 3f));
        var found = new List<(Vector2 A, Vector2 B)>();
        for (float y = chunk.Min.Y + 3f; y < chunk.Max.Y - 3f; y += 2f)
        for (float x = chunk.Min.X + 3f; x < chunk.Max.X - 3f; x += 2f)
        {
            var p = new Vector2(x, y);
            if (H(p) > -0.3f || keep.Exists(q => Vector2.Distance(q.At, p) < q.Radius)) continue;
            for (int d = 0; d < 8; d++)
            {
                var dir = new Vector2(MathF.Cos(d * MathF.PI / 8f), MathF.Sin(d * MathF.PI / 8f));
                float Reach(Vector2 way)
                {
                    for (float t = 1f; t <= ArchSpanMax; t += 0.5f)
                        if (H(p + way * t) >= ArchFooting) return t + 0.5f;
                    return -1f;
                }
                float sa = Reach(dir), sb = Reach(-dir);
                if (sa < 0f || sb < 0f || sa + sb < ArchSpanMin || sa + sb > ArchSpanMax) continue;
                var a = p + dir * sa;
                var b = p - dir * sb;
                if (Inside(a) && Inside(b) && H(a) >= ArchFooting && H(b) >= ArchFooting) found.Add((a, b));
            }
        }
        if (found.Count == 0) return;
        var (fa, fb) = found[rng.Int(found.Count)];
        float bottom = LevelMap.SwimBand + 1.6f;
        chunk.Canopies.Add(new Canopy
        {
            Kind = CanopyKind.Arch,
            Center = (fa + fb) * 0.5f,
            Axis = Vector2.Normalize(fb - fa),
            HalfLength = Vector2.Distance(fa, fb) * 0.5f,
            HalfWidth = rng.Range(1.8f, 3f),
            Bottom = bottom,
            Top = bottom + rng.Range(2f, 3.5f),
        });
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
    /// <summary>A wall's thickness from its chunk's edge at a point (sea metres): its own wander and its broad swell.</summary>
    /// <summary>How thick at most a wall stands over a template's water (metres from the edge).</summary>
    const float WallOverWater = 3.5f;

    float WallThick(int x, int y) =>
        MathF.Max(WallCore, Wall + WallWander * CaveLayout.Fbm(x * 0.11f, y * 0.11f, _noise + 17) + WallLump * (0.35f + 0.65f * MathUtil.Clamp01(0.5f + 1.2f * CaveLayout.Fbm(x * 0.06f, y * 0.06f, _noise + 23))));

    bool InWall((int X, int Y) cell, int lx, int ly, float wall)
    {
        if (lx >= wall && lx < Cell - wall && ly >= wall && ly < Cell - wall) return false;
        var key = ChunkOfCell(cell);
        if (Cleared(cell.X * Cell + lx, cell.Y * Cell + ly)) return false;
        // (A way's sides waver too, widening it a little past its socket here and there.)
        float widen = GapWiden * MathUtil.Clamp01(0.5f + CaveLayout.Fbm((cell.X * Cell + lx) * 0.19f, (cell.Y * Cell + ly) * 0.19f, _noise + 29));
        bool Shut(int dx, int dy, int along)
        {
            var n = ChunkOfCell((cell.X + dx, cell.Y + dy));
            return n != key && (along < CaveTemplates.SocketFrom - 1 - widen || along > CaveTemplates.SocketTo + 1 + widen || !Linked(key, n));
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
        map.Canopies.Clear();
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
            // (Its arches and roof, those whose middle is in the window, in its coordinates.)
            if (chunk is not null)
                foreach (var c in chunk.Canopies)
                    if (c.Center.X - ox is > 0f && c.Center.Y - oy is > 0f && c.Center.X - ox < n - 1 && c.Center.Y - oy < n - 1)
                        map.Canopies.Add(new Canopy { Kind = c.Kind, Center = c.Center - origin, Axis = c.Axis, HalfLength = c.HalfLength, HalfWidth = c.HalfWidth, Radius = c.Radius, Bottom = c.Bottom, Top = c.Top });
            if (only is { } o && o != key) continue;
            var (min, max) = RectOf(key);
            int x0 = Math.Max((int)min.X, ox), x1 = Math.Min((int)max.X, ox + n), y0 = Math.Max((int)min.Y, oy), y1 = Math.Min((int)max.Y, oy + n);
            for (int y = y0; y < y1; y++)
            {
                int row = (y - oy) * n - ox;
                if (chunk is null)
                {
                    for (int x = x0; x < x1; x++) map.Heights[row + x] = Unbuilt;
                    Array.Fill(map.Lid, float.NaN, row + x0, x1 - x0);
                    missing += x1 - x0;
                    continue;
                }
                int src = (y - (int)chunk.Origin.Y) * chunk.Stride - (int)chunk.Origin.X;
                Array.Copy(chunk.Heights, src + x0, map.Heights, row + x0, x1 - x0);
                if (chunk.Lid is { } lid) Array.Copy(lid, src + x0, map.Lid, row + x0, x1 - x0);
                else Array.Fill(map.Lid, float.NaN, row + x0, x1 - x0);
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
        // (Exact Euclidean distances, Felzenszwalb and Huttenlocher's two passes: round about a corner, never the
        // octagons of a chamfer's, whose flat facets showed as straight bevels on the reef.)
        const float Inf = 1e12f;
        int n = Math.Max(w, h);
        var f = new float[n];
        var line = new float[n];
        var v = new int[n];
        var z = new float[n + 1];
        var d = new float[w * h];
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++) f[j] = set[j * w + i] == to ? 0f : Inf;
            Squared(f, h, line, v, z);
            for (int j = 0; j < h; j++) d[j * w + i] = line[j];
        }
        for (int j = 0; j < h; j++)
        {
            Array.Copy(d, j * w, f, 0, w);
            Squared(f, w, line, v, z);
            for (int i = 0; i < w; i++) d[j * w + i] = MathF.Sqrt(line[i]);
        }
        return d;
    }

    static float Meet(float[] f, int q, int p) => (f[q] + q * q - (f[p] + p * p)) / (2f * (q - p));

    /// <summary>The squared distance transform of a sampled function along a line (the lower envelope of its parabolas).</summary>
    static void Squared(float[] f, int n, float[] d, int[] v, float[] z)
    {
        int k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            float s = Meet(f, q, v[k]);
            while (s <= z[k]) s = Meet(f, q, v[--k]);
            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            float dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }
}
