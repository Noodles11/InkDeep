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
/// chunks are joined (meeting in open water, their one way on a side they share: WaySide) when either takes the other as
/// its parent (each chunk but the one holding cell (0, 0) has one: a neighbour nearer that cell, so every chunk reaches
/// it and has a way out), or besides where their way lies in open water (ChunkLink). The sea is open but for its own
/// reefs (ReefAt), worn by it so the biggest stand (Wear), the fillers' rock and the walls between chunks not joined;
/// each chunk's passages (Plan) cut through any of it, so its ways always meet (RockAt: a function of the sea's own
/// coordinates, so a chunk is the same from wherever it is asked and neighbours meet without a seam). Its places come
/// by rules (PlaceRules), some in rooms drawn by hand (AreaTemplates).
/// </para>
/// </summary>
public sealed class OpenSea
{
    public const int Cell = CaveTemplates.Cell;
    /// <summary>Two neighbouring chunks not joined by the tree are joined this often where their way lies in open water (less as it lies in a reef).</summary>
    public const float ChunkLink = 1f;
    /// <summary>
    /// Where and how often each kind of place lies in the sea (docs/GAME.md, Open levels): about one chunk in OnceIn holds
    /// one, none Apart or nearer to another of its kind (1: never side by side; more: no two whose first cells are that
    /// few cells apart either way), none within From cells of her start (and none in her first chunk or those round it);
    /// with Templates, about TemplateShare of them stand in a room drawn by hand (AreaTemplates), the rest in the reef's
    /// own water. Each chunk rolls for each kind (Share, from OnceIn and Apart) and holds it where none of those round it
    /// that rolled for it rolled a higher priority: the choice is the same from any chunk asked, nothing of the sea made
    /// first. A chunk holds one place at most: the kind higher in this list has it.
    /// </summary>
    public static readonly IReadOnlyList<PlaceRule> PlaceRules = new PlaceRule[]
    {
        new(PoiKind.Hole, 9, 1, 3),
        new(PoiKind.Shop, 8, 1, 3, new[] { "grotto" }, 0.6f),
        new(PoiKind.TreasureCave, 8, 1, 3, new[] { "chamber" }, 0.5f),
        new(PoiKind.ShellCache, 9, 1, 3),
        new(PoiKind.Shrine, 12, 1, 4, new[] { "hall" }, 0.7f),
        new(PoiKind.CrabDeal, 15, 1, 4, new[] { "grotto" }, 0.4f),
        new(PoiKind.Relic, 35, 2, 6, new[] { "crypt" }, 0.8f),
    };

    /// <summary>One kind of place's rule (see PlaceRules).</summary>
    public sealed record PlaceRule(PoiKind Kind, int OnceIn, int Apart, int From, string[]? Templates = null, float TemplateShare = 0f)
    {
        /// <summary>
        /// The share of chunks rolling for it, so that about one in OnceIn holds it: each wins against those within Apart
        /// of it that roll too (about one in 1/share + their number), and the kinds above it have taken some (taken).
        /// </summary>
        public float Share(float taken)
        {
            float round = Apart <= 1 ? 6f : (2 * Apart + 1) * (2 * Apart + 1) * 0.5f - 1f;
            float onceIn = OnceIn * (1f - taken);
            return onceIn > round + 1f ? MathF.Min(1f, 1f / (onceIn - round)) : 1f;
        }
    }

    /// <summary>Each rule's share (PlaceRule.Share), the kinds above it taking theirs first.</summary>
    static readonly float[] Shares = PlaceRules.Select((r, i) => r.Share(PlaceRules.Take(i).Sum(a => 1f / a.OnceIn))).ToArray();

    readonly ConcurrentDictionary<(int X, int Y), PoiKind?> _places = new();

    /// <summary>The place a chunk holds (by the rules), or none.</summary>
    public PoiKind? PlaceOf((int X, int Y) key) => _places.TryGetValue(key, out var hit) ? hit : _places.GetOrAdd(key, k =>
    {
        for (int r = 0; r < PlaceRules.Count; r++)
            if (Wins(r, k)) return PlaceRules[r].Kind;
        return null;
    });

    float Roll(int r, (int X, int Y) k, ulong salt) => Unit(Mix(Seed, (ulong)r, (ulong)(uint)k.X, (ulong)(uint)k.Y, salt));

    /// <summary>Whether a chunk rolls for a rule's kind (it holds it if none round it rolls higher).</summary>
    bool Rolls(int r, (int X, int Y) k)
    {
        var rule = PlaceRules[r];
        return ChunkOfCell(k) == k && !Near(k) && k != Lair && Dist(k, CellsOf(k)) >= rule.From && Roll(r, k, 0x9A1CE) < Shares[r];
    }

    bool Wins(int r, (int X, int Y) k)
    {
        if (!Rolls(r, k)) return false;
        var rule = PlaceRules[r];
        float mine = Roll(r, k, 0x9A1CF);
        foreach (var o in rule.Apart <= 1 ? Neighbours(k) : Box(k, rule.Apart))
            if (o != k && Rolls(r, o) && Roll(r, o, 0x9A1CF) > mine) return false;
        return true;
    }

    /// <summary>The chunks whose first cell is within apart cells of a chunk's either way.</summary>
    IEnumerable<(int X, int Y)> Box((int X, int Y) k, int apart)
    {
        for (int y = k.Y - apart; y <= k.Y + apart; y++)
        for (int x = k.X - apart; x <= k.X + apart; x++)
            if (ChunkOfCell((x, y)) == (x, y)) yield return (x, y);
    }

    /// <summary>Her first chunk and those touching it.</summary>
    bool Near((int X, int Y) key)
    {
        var home = ChunkOfCell((0, 0));
        return key == home || Neighbours(home).Contains(key);
    }

    /// <summary>Heights kept past a chunk's edge (its mesh's spline and normals).</summary>
    public const int Pad = 3;
    /// <summary>The window's height where no chunk is built yet: just over her plane (it stops her, and casts next to no shade).</summary>
    public const float Unbuilt = 0.3f;
    /// <summary>The middle of a wall between chunks not joined (always rock), and how far past it no water lies (metres).</summary>
    const float WallCore = 1f, WallClear = 1.5f;
    /// <summary>The floor, and the rock's top (each give or take its noise).</summary>
    const float Floor = -1f, Top = 3f;
    /// <summary>A hole: its opening, its slope, its sag at the rim, its lip; how far its middle keeps from the chunk's edge (its slope then ends Pad inside it: a neighbour's padding never holds it), and how much room it needs.</summary>
    const float HoleOpening = 2.4f, HoleSlope = 3f, HoleSag = 1.2f, HoleLip = 1.4f, HoleKeep = 9f, HoleRoom = 3.5f;

    public readonly ulong Seed;
    public readonly OctoShoots.Core.Run.Biome Biome;
    readonly int _shift;
    readonly uint _noise;
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
    public (int X, int Y)? Parent((int X, int Y) key) => _parents.TryGetValue(key, out var hit) ? hit : _parents.GetOrAdd(key, k =>
    {
        int d = Dist(k, CellsOf(k));
        if (d == 0) return null;
        // (Of the neighbours nearer it, the one whose way lies in the most open water, give or take: the tree runs
        // through the open sea, round the reefs.)
        var nearer = Neighbours(k).Where(n => Dist(n, CellsOf(n)) < d).ToList();
        return nearer.MinBy(n => WayReef(k, n) + 0.15f * Unit(Mix(Seed, (ulong)(uint)k.X, (ulong)(uint)k.Y, (ulong)(uint)n.X, (ulong)(uint)n.Y, 0x9A2E)));
    });

    /// <summary>Whether two neighbouring chunks are joined (the same asked from either).</summary>
    public bool Linked((int X, int Y) a, (int X, int Y) b) => FillOf(a) != Filler.Full && FillOf(b) != Filler.Full && Joined(a, b);

    /// <summary>Whether two neighbouring chunks are joined by the tree or by chance (before any chunk is filled whole).</summary>
    bool Joined((int X, int Y) a, (int X, int Y) b)
    {
        if (Parent(a) == b || Parent(b) == a) return true;
        var (lo, hi) = a.CompareTo(b) < 0 ? (a, b) : (b, a);
        // (Now and then besides: mostly where their way lies in open water, seldom through a reef.)
        return Unit(Mix(Seed, (ulong)(uint)lo.X, (ulong)(uint)lo.Y, (ulong)(uint)hi.X, (ulong)(uint)hi.Y)) < ChunkLink * (1f - Smooth(ReefLevel - 0.15f, ReefLevel + 0.15f, WayReef(lo, hi)));
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
    (Filler Kind, int Cell) Fill((int X, int Y) key) => _fills.TryGetValue(key, out var hit) ? hit : _fills.GetOrAdd(key, k =>
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

    /// <summary>
    /// A chunk's skeleton (docs/GAME.md, Open levels): its passages, which cut through any rock in their way, so every way
    /// of it always meets the others. From each of its ways out a passage runs straight in and on to the nearest of a few
    /// points scattered through it, and those are joined by the shortest tree through them (now and then a loop); every
    /// passage bowed and meandering. Its place stands at the roomiest of those points (or its area template's mark), a
    /// hole and the lair at its middle; no reef stands over them (Clear). A filler's rock (Solid): all of it, a half's
    /// filled cell, a quarter's far half of it.
    /// </summary>
    sealed record Plan(Way[] Ways, Stamp? Stamp, Vector2? Mark, Vector2[] Clear, (Vector2 Lo, Vector2 Hi)? Solid, Vector2 Lo, Vector2 Hi);
    readonly record struct Way(Vector2 A, Vector2 B, float Bend, float R0, float R1, Vector2 Lo, Vector2 Hi);
    sealed record Stamp(AreaTemplate Template, Vector2 At, int Turn);

    /// <summary>A passage's half width (least, most), and how far a passage bows (a share of its length).</summary>
    const float WayMin = 1.7f, WayMax = 2.5f, WayBend = 0.3f;
    /// <summary>How near two of its points may be, how far they keep from its edge, and the room kept clear round a place or a hole.</summary>
    const float NodeApart = 6.5f, NodeKeep = 5f, PlaceCave = 4.5f;
    /// <summary>How far round its chunk a skeleton reaches (its passages past the ways, a filler's rock bulging), and how broadly passages blend.</summary>
    const float Reach = 7f, Blend = 1.5f;
    /// <summary>The rock's face: its broad roughness and its scallops (metres).</summary>
    const float Rough = 1f, Scallop = 0.9f;
    const float Far = 1e6f;

    readonly ConcurrentDictionary<(int X, int Y), Plan> _plans = new();

    Plan PlanOf((int X, int Y) key) => _plans.TryGetValue(key, out var hit) ? hit : _plans.GetOrAdd(key, MakePlan);

    Plan MakePlan((int X, int Y) k)
    {
        var (min, max) = RectOf(k);
        var (kind, filled) = Fill(k);
        var lo = min - new Vector2(Reach);
        var hi = max + new Vector2(Reach);
        if (kind == Filler.Full) return new Plan(Array.Empty<Way>(), null, null, Array.Empty<Vector2>(), (min, max), lo, hi);
        var rng = new Rng(Mix(Seed, (ulong)(uint)k.X, (ulong)(uint)k.Y, 0x5CE1));
        var hub = (min + max) * 0.5f;
        var place = PlaceOf(k);
        bool hole = place == PoiKind.Hole || k == Lair;
        var cells = CellsOf(k);
        var ways = new List<Way>();
        Vector2? mark = null;
        Stamp? stamp = null;
        void AddWay(Vector2 a, Vector2 b, float bend, float r0, float r1)
        {
            var m = new Vector2(MathF.Abs(bend) + Meander + MathF.Max(r0, r1) + Rough + Scallop + Blend);
            ways.Add(new Way(a, b, bend, r0, r1, Vector2.Min(a, b) - m, Vector2.Max(a, b) + m));
        }
        float Bend(Vector2 a, Vector2 b, float most) => Vector2.Distance(a, b) * most * rng.Range(-1f, 1f);
        // (A filler's rock: its filled cell (a half), the far half of it (a quarter).)
        (Vector2 Lo, Vector2 Hi)? solid = null;
        if (kind is Filler.Half or Filler.Quarter)
        {
            var fc = cells[filled];
            var c0 = new Vector2(fc.X * Cell, fc.Y * Cell);
            var c1 = c0 + new Vector2(Cell);
            if (kind == Filler.Quarter)
            {
                // (The half away from its other cell.)
                var other = cells[1 - filled];
                if (other.X < fc.X) c0.X += Cell * 0.5f;
                else if (other.X > fc.X) c1.X -= Cell * 0.5f;
                else if (other.Y < fc.Y) c0.Y += Cell * 0.5f;
                else c1.Y -= Cell * 0.5f;
            }
            solid = (c0, c1);
        }
        // (A secret room is a grotto in a block of rock of its own: its ways and the pool round its place carved in it.)
        if (Secret(k)) solid = (min, max);
        bool Allowed(Vector2 p) => solid is not { } so || so == (min, max) || p.X < so.Lo.X || p.Y < so.Lo.Y || p.X > so.Hi.X || p.Y > so.Hi.Y;
        // Its points: where its ways meet (its middle for a hole or the lair, else where the sea's reefs are lowest of a
        // few places in it, so its passages run through open water), and for a place one more, away from that one.
        var spots = Enumerable.Range(0, 12).Select(_ => new Vector2(rng.Range(min.X + NodeKeep, max.X - NodeKeep), rng.Range(min.Y + NodeKeep, max.Y - NodeKeep))).Where(Allowed).OrderBy(ReefAt).ToList();
        var nodes = new List<Vector2> { hole || spots.Count == 0 ? (Allowed(hub) ? hub : Vector2.Lerp(hub, new Vector2(cells[1 - filled].X * Cell + Cell * 0.5f, cells[1 - filled].Y * Cell + Cell * 0.5f), 0.6f)) : spots[0] };
        if (place is not null && !hole && spots.FirstOrDefault(o => Vector2.Distance(o, nodes[0]) >= NodeApart) is var second && second != default) nodes.Add(second);
        // (Its place at the other point, if it found room for one, but a hole's, at its middle.)
        int placeNode = -1;
        if (place is not null && !hole)
        {
            placeNode = nodes.Count - 1;
            mark = nodes[placeNode];
            var rule = PlaceRules.First(r => r.Kind == place);
            if (rule.Templates is { Length: > 0 } names && rng.NextFloat() < rule.TemplateShare)
            {
                var t = AreaTemplates.ByName[names[rng.Int(names.Length)]];
                int turn = rng.Int(4);
                stamp = new Stamp(t, mark.Value, turn);
                mark = mark.Value + t.MarkTurned(turn);
            }
        }
        // Its passages: the shortest tree through its points, now and then one more (a loop), and each way out straight
        // in through its wall (never along the wall beside its gap) and on to the nearest point.
        var joinedTo = new List<int> { 0 };
        var rest = Enumerable.Range(1, nodes.Count - 1).ToList();
        while (rest.Count > 0)
        {
            var (a, b) = joinedTo.SelectMany(i => rest.Select(j => (i, j))).MinBy(e => Vector2.Distance(nodes[e.i], nodes[e.j]));
            AddWay(nodes[a], nodes[b], Bend(nodes[a], nodes[b], WayBend), rng.Range(WayMin, WayMax), rng.Range(WayMin, WayMax));
            joinedTo.Add(b);
            rest.Remove(b);
        }
        foreach (var s in SocketsOf(k))
        {
            var from = s.At + s.Way * 2f;
            var inside = s.At - s.Way * WayGap;
            var to = nodes[0];
            float r = rng.Range(WayMin, WayMax);
            AddWay(from, inside, 0f, r, r);
            AddWay(inside, to, Bend(inside, to, WayBend * 0.8f), r, r + rng.Range(0f, 0.6f));
        }
        var clear = nodes.Where((_, i) => i == placeNode || hole && i == 0).ToArray();
        return new Plan(ways.ToArray(), stamp, mark, clear, solid, lo, hi);
    }

    /// <summary>The skeletons reaching into a window of the sea (lo to hi, metres).</summary>
    List<Plan> PlansNear(Vector2 lo, Vector2 hi)
    {
        var keys = new HashSet<(int X, int Y)>();
        for (int cy = FloorDiv((int)MathF.Floor(lo.Y - Reach)); cy <= FloorDiv((int)MathF.Ceiling(hi.Y + Reach)); cy++)
        for (int cx = FloorDiv((int)MathF.Floor(lo.X - Reach)); cx <= FloorDiv((int)MathF.Ceiling(hi.X + Reach)); cx++)
            keys.Add(ChunkOfCell((cx, cy)));
        // (In one order, whichever chunk asks: the blends between them are not quite the same taken in another.)
        return keys.OrderBy(k => k).Select(PlanOf).Where(p => p.Lo.X < hi.X && p.Lo.Y < hi.Y && p.Hi.X > lo.X && p.Hi.Y > lo.Y).ToList();
    }

    /// <summary>
    /// The rock at a point (sea metres), before the sea wears it and the walls between chunks go in: below 0 in water
    /// (about the metres to the rock), above 0 in rock (about the metres to the water). Open sea but for the sea's own
    /// reefs (broad, lying anywhere, never over a place or a hole) and the fillers' rock (bulging lumpily past its
    /// square), the skeletons' passages cut through them; warped and roughened in the sea's own coordinates (but not by a
    /// closed wall, which it would only run against); a place's template pressed in round it. The same from any chunk.
    /// </summary>
    float RockAt(int x, int y, List<Plan> plans, out bool walled, out float closed, out float passage)
    {
        var p = new Vector2(x, y);
        closed = ClosedAt(x, y);
        walled = closed < WallCore;
        float fade = Smooth(1.5f, 5f, closed);
        var q = fade > 0f ? p + Warp(x, y) * fade : p;
        float ways = Far, clear = Far, solid = -Far;
        Stamp? stamp = null;
        float rough = fade * (Rough * CaveLayout.Fbm(q.X * 0.11f, q.Y * 0.11f, _noise + 3) + Scallop * (0.3f - MathF.Abs(CaveLayout.Fbm(q.X * 0.29f, q.Y * 0.29f, _noise + 7))));
        foreach (var plan in plans)
        {
            if (q.X < plan.Lo.X || q.Y < plan.Lo.Y || q.X > plan.Hi.X || q.Y > plan.Hi.Y) continue;
            foreach (var w in plan.Ways)
            {
                if (q.X < w.Lo.X || q.Y < w.Lo.Y || q.X > w.Hi.X || q.Y > w.Hi.Y) continue;
                float d = WayDistance(q, w.A, w.B, w.Bend, out float t);
                ways = SMin(ways, d - (w.R0 + (w.R1 - w.R0) * t), Blend);
            }
            foreach (var c in plan.Clear) clear = MathF.Min(clear, Vector2.Distance(q, c));
            if (plan.Solid is { } so)
            {
                // (Its rock: how far inside its square, and a lumpy bulge out past it.)
                var o = Vector2.Max(so.Lo - p, p - so.Hi);
                float box = o.X > 0f || o.Y > 0f ? Vector2.Max(o, Vector2.Zero).Length() : MathF.Max(o.X, o.Y);
                solid = MathF.Max(solid, SolidBulge * MathUtil.Clamp01(0.5f + CaveLayout.Fbm(x * 0.08f, y * 0.08f, _noise + 91)) + 1f - box);
            }
            if (plan.Stamp is { } st && Vector2.DistanceSquared(q, st.At) < 49f) stamp = st;
        }
        // (The sea's own reefs, broad and lying anywhere, whatever the chunks: never over a place or a hole.)
        float s = ReefDepth * (ReefAt(q) - ReefLevel) + ReefRough * CaveLayout.Fbm(q.X * 0.08f, q.Y * 0.08f, _noise + 103) + rough;
        s = SMin(s, clear - PlaceCave - 1f + rough, Blend);
        s = MathF.Max(s, solid + rough * 0.5f);
        if (stamp is { } at)
        {
            float m = 1f - Smooth(4.5f, 6.5f, Vector2.Distance(q, at.At));
            s += (at.Template.At(q - at.At, at.Turn) + 0.3f * rough - s) * m;
        }
        passage = ways + rough;
        return SMin(s, passage, Blend);
    }

    /// <summary>The sea's own reefs: how steeply they rise (metres a unit of their noise), its scale, the share of it under them, and their rough edge.</summary>
    const float ReefDepth = 35f, ReefScale = 0.022f, ReefLevel = 0.15f, ReefRough = 1.5f;
    /// <summary>How far a filler's rock bulges past its square at most (metres).</summary>
    const float SolidBulge = 4f;

    /// <summary>A wall between chunks not joined (or round a secret room), put back once the sea has worn the rest: how thick from where they meet, lumpy.</summary>
    float WallReef(int x, int y) => WallClear + WallSwell * MathUtil.Clamp01(0.5f + 1.3f * CaveLayout.Fbm(x * 0.06f, y * 0.06f, _noise + 107));

    /// <summary>How much a wall between chunks swells past WallClear (metres at most, either side).</summary>
    const float WallSwell = 3f;

    /// <summary>
    /// How far a point of a cell (fx, fy within it) is from the closed part of its sides shared with another chunk (all
    /// of a side not joined; a joined side's but its way).
    /// </summary>
    float Closed((int X, int Y) cell, float fx, float fy)
    {
        var (other, joined, shut, way) = SidesOf(cell);
        float closed = Far;
        float[]? mids = way != 0 ? MidsOf(cell) : null;
        void Side(int bit, int index, float perp, float along)
        {
            // (Two chunks joined meet in open water; but a secret room's side, which is shut but for its way.)
            if ((other & bit) == 0 || (joined & bit) != 0 && (shut & bit) == 0) return;
            float a = Math.Clamp(along, 0f, Cell), da = MathF.Abs(along - a);
            if ((way & bit) != 0 && MathF.Abs(a - mids![index]) < WayGap) da = WayGap - MathF.Abs(a - mids[index]);
            closed = MathF.Min(closed, MathF.Sqrt(perp * perp + da * da));
        }
        Side(CaveTemplates.N, 0, MathF.Abs(fy), fx);
        Side(CaveTemplates.E, 1, MathF.Abs(Cell - fx), fy);
        Side(CaveTemplates.S, 2, MathF.Abs(Cell - fy), fx);
        Side(CaveTemplates.W, 3, MathF.Abs(fx), fy);
        return closed;
    }

    /// <summary>How far a point (sea metres) is from the nearest closed wall between chunks (its cell's and those round it: so a wall's end is round), ClosedFar at most.</summary>
    const float ClosedFar = 8f;

    float ClosedAt(int x, int y)
    {
        int cx = FloorDiv(x), cy = FloorDiv(y);
        float fx = x + 0.5f - cx * Cell, fy = y + 0.5f - cy * Cell;
        // (Nothing reads it past ClosedFar: a cell round it further off than that is not asked.)
        int x0 = fx < ClosedFar ? -1 : 0, x1 = Cell - fx < ClosedFar ? 1 : 0, y0 = fy < ClosedFar ? -1 : 0, y1 = Cell - fy < ClosedFar ? 1 : 0;
        float best = ClosedFar;
        for (int dy = y0; dy <= y1; dy++)
        for (int dx = x0; dx <= x1; dx++)
            best = MathF.Min(best, Closed((cx + dx, cy + dy), fx - dx * Cell, fy - dy * Cell));
        return best;
    }

    /// <summary>A smooth minimum (the two blended over k metres where they meet, as water rounds a joint).</summary>
    static float SMin(float a, float b, float k)
    {
        float h = MathUtil.Clamp01(0.5f + 0.5f * (b - a) / k);
        return b + (a - b) * h - k * h * (1f - h);
    }

    /// <summary>
    /// A point's distance from a passage bowed into an arc (by bend, at its middle) and meandering about it (Meander, in
    /// the sea's own noise along it; neither at its ends, so passages meet where they are), and how far along it.
    /// </summary>
    float WayDistance(Vector2 p, Vector2 a, Vector2 b, float bend, out float t)
    {
        var ab = b - a;
        float len2 = MathF.Max(ab.LengthSquared(), 1e-6f), len = MathF.Sqrt(len2);
        var across = new Vector2(-ab.Y, ab.X) / len;
        // (Near enough for a bend this gentle: its point abreast of p along the chord, pushed out by the bow and the meander.)
        t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
        float envelope = MathF.Sin(MathF.PI * t);
        float meander = Meander * CaveLayout.Fbm(t * len * 0.13f + a.X * 0.37f, a.Y * 0.37f + b.X * 0.11f, _noise + 17);
        return Vector2.Distance(p, a + ab * t + across * (bend + meander) * envelope);
    }

    /// <summary>How far a passage meanders about its bow (metres at most).</summary>
    const float Meander = 2.2f;

    /// <summary>How far a skeleton is warped where it is read (metres at most, two octaves, and their scales).</summary>
    const float WarpReach = 3.5f, WarpScale = 0.045f, WarpFine = 1.5f, WarpFineScale = 0.12f;

    Vector2 Warp(int x, int y) =>
        new Vector2(CaveLayout.Fbm(x * WarpScale, y * WarpScale, _noise + 51), CaveLayout.Fbm(x * WarpScale, y * WarpScale, _noise + 53)) * WarpReach
        + new Vector2(CaveLayout.Fbm(x * WarpFineScale, y * WarpFineScale, _noise + 55), CaveLayout.Fbm(x * WarpFineScale, y * WarpFineScale, _noise + 57)) * WarpFine;

    /// <summary>
    /// How much the sea wears the reef away (metres at most, an opening's reach: a reef must stand that far in from the
    /// water somewhere to stay), how far past that it is read (so it is the same from any chunk), and the scale of its
    /// wander: heavy over most of the sea, light here and there (where a small reef may stand).
    /// </summary>
    const float WearMost = 4.5f, WearCap = 2.5f, WearScale = 0.035f;
    const int Open = 8;

    float Wear(int x, int y) =>
        WearMost * Smooth(-0.45f, 0.2f, CaveLayout.Fbm(x * WearScale, y * WearScale, _noise + 91));

    /// <summary>
    /// The rock rising on past its foot into mountains: up to MountainPeak higher, from MountainFoot to MountainReach metres
    /// in from the water, ridged.
    /// </summary>
    const float MountainPeak = 4.5f, MountainFoot = 2f, MountainReach = 10f;

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
        var chunk = new Chunk { Key = key, Min = min, Max = max, Origin = min - new Vector2(Pad), Stride = (int)(max.X - min.X) + 2 * Pad + 1, Rows = (int)(max.Y - min.Y) + 2 * Pad + 1 };
        int w = chunk.Stride, h = chunk.Rows, n = w * h, x0 = (int)chunk.Origin.X, y0 = (int)chunk.Origin.Y;
        // The rock round every point, from the skeletons reaching it (its own and its neighbours'), read Open past its
        // padding: what the sea wears away of it is judged by how big a reef it is.
        int bw = w + 2 * Open, bh = h + 2 * Open, bn = bw * bh, bx = x0 - Open, by = y0 - Open;
        var plans = PlansNear(new Vector2(bx, by) - new Vector2(WarpReach + WarpFine), new Vector2(bx + bw, by + bh) + new Vector2(WarpReach + WarpFine));
        var all = new float[bn];
        var wear = new float[bn];
        var walls = new bool[bn];
        var core = new bool[bn];
        var closedAt = new float[bn];
        var passages = new float[bn];
        for (int j = 0; j < bh; j++)
        for (int i = 0; i < bw; i++)
        {
            int k = j * bw + i, x = bx + i, y = by + j;
            all[k] = RockAt(x, y, plans, out walls[k], out closedAt[k], out passages[k]);
            wear[k] = Wear(x, y);
            core[k] = all[k] >= wear[k];
        }
        // Worn by the sea: rock stands only within its wear of rock that far in from the water (an opening: a reef too
        // small for it is gone, a bigger one keeps its shape, rounded), so the biggest reefs stand, fewer of the middling,
        // now and then a small one (where the sea's wear is light).
        var toCore = Distances(core, bw, bh, true);
        var s = new float[n];
        var walled = new bool[n];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int k = (j + Open) * bw + i + Open;
            float d = MathF.Min(toCore[k], WearMost + WearCap);
            s[j * w + i] = d > 0f ? MathF.Min(all[k], wear[k] - d) : all[k];
            // (Then the walls between chunks not joined: never water near one, its middle always rock; but the chunk's
            // passages run on through a wall's swell, so it never shuts one.)
            s[j * w + i] = MathF.Min(MathF.Max(s[j * w + i], WallReef(x0 + i, y0 + j) - closedAt[k]), MathF.Max(passages[k], WallClear - closedAt[k]));
            walled[j * w + i] = walls[k];
            if (walls[k]) s[j * w + i] = MathF.Max(s[j * w + i], 0.6f);
        }
        // The lair: its hole where the water is deepest (its pool at the chunk's middle).
        Vector2? lair = null;
        if (key == Lair)
        {
            int at = -1;
            for (int k = 0; k < n; k++)
            {
                var p = new Vector2(x0 + k % w, y0 + k / w);
                if (p.X < min.X + HoleKeep || p.Y < min.Y + HoleKeep || p.X > max.X - HoleKeep || p.Y > max.Y - HoleKeep) continue;
                if (at < 0 || s[k] < s[at]) at = k;
            }
            if (at >= 0) lair = new Vector2(x0 + at % w, y0 + at / w);
        }
        // Raised: the floor, rising round into the rock over a few metres, and on into mountains.
        chunk.Heights = new float[n];
        var toRock = new float[n];
        for (int k = 0; k < n; k++)
        {
            float x = x0 + k % w, y = y0 + k / w;
            bool open = s[k] < 0f;
            toRock[k] = MathF.Max(0f, -s[k]);
            float floor = Floor + 0.12f * CaveLayout.Fbm(x * 0.21f, y * 0.21f, _noise + 5);
            float top = Top + 1.2f * CaveLayout.Fbm(x * 0.05f, y * 0.05f, _noise + 9);
            // (A wall between chunks, thin as it may be, stands at the reef's top: never a low strip along their edge.)
            float hgt = floor + (top - floor) * Smooth(-0.8f, walled[k] ? 1.4f : 3f, s[k]);
            if (!open) hgt += MountainPeak * Smooth(MountainFoot, MountainReach, s[k]) * (0.55f + 0.45f * (1f - MathF.Abs(CaveLayout.Fbm(x * 0.08f, y * 0.08f, _noise + 71))));
            // (Rock always stands over her plane and water always lies under it, however thin: a wall is a wall.)
            chunk.Heights[k] = open ? MathF.Min(hgt, -0.3f) : MathF.Max(hgt, 0.3f);
        }
        // Its place, if it holds one: a hole down where its water is deepest, well inside it (so no neighbour's heights
        // change); anything else in its cave (or its template's room), else where its water is deepest.
        chunk.Place = PlaceOf(key);
        var plan = PlanOf(key);
        if (chunk.Place is { } kind && kind != PoiKind.Hole)
        {
            Vector2 widest = (min + max) * 0.5f;
            float room = -1f;
            for (int k = 0; k < n; k++)
            {
                var p = new Vector2(x0 + k % w, y0 + k / w);
                if (p.X < min.X + 4f || p.Y < min.Y + 4f || p.X > max.X - 4f || p.Y > max.Y - 4f) continue;
                if (toRock[k] > room + 0.01f)
                {
                    room = toRock[k];
                    widest = p;
                }
            }
            var mark = plan.Mark is { } m ? new Vector2(MathF.Round(m.X), MathF.Round(m.Y)) : widest;
            chunk.PlaceAt = chunk.Contains(mark) && toRock[((int)mark.Y - y0) * w + (int)mark.X - x0] >= 1.5f ? mark : widest;
        }
        if (chunk.Place == PoiKind.Hole)
        {
            float best = HoleRoom;
            Vector2? at = null;
            for (int k = 0; k < n; k++)
            {
                var p = new Vector2(x0 + k % w, y0 + k / w);
                if (p.X < min.X + HoleKeep || p.Y < min.Y + HoleKeep || p.X > max.X - HoleKeep || p.Y > max.Y - HoleKeep) continue;
                if (toRock[k] > best + 0.01f)
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
        if (lair is { } lh)
        {
            DigHole(chunk, lh, new Rng(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x1A13)));
            chunk.Lair = lh;
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
        var fill = FillOf(key);
        chunk.Template = plan.Stamp?.Template.Name ?? (fill != Filler.None ? fill.ToString().ToLowerInvariant() : "caves");
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
            lid[k] = MathF.Max(MathF.Max(ground[k], chunk.Heights[k]), top);
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
    /// <summary>How much further a seal's plug reaches in here and there, and how much it rises to its middle.</summary>
    const float SealLump = 2.5f, SealDome = 1f;
    /// <summary>The share of the chunks holding a treasure that are secret rooms.</summary>
    public const float SecretChance = 0.3f;

    /// <summary>A secret room: a chunk holding a treasure (a clam, a cache, a relic), by SecretChance; never the lair or her first.</summary>
    public bool Secret((int X, int Y) key) =>
        PlaceOf(key) is PoiKind.TreasureCave or PoiKind.ShellCache or PoiKind.Relic && key != Lair && key != ChunkOfCell((0, 0))
        && Unit(Mix(Seed, (ulong)(uint)key.X, (ulong)(uint)key.Y, 0x5EA1)) < SecretChance;

    /// <summary>A chunk's ways out: the middle of each on the edge it shares with a joined chunk, the way out, its half width, and whether it is this chunk's.</summary>
    public List<(Vector2 At, Vector2 Way, float Half, bool Own)> SocketsOf((int X, int Y) key)
    {
        const float half = WayGap + 1f;
        var ways = new List<(Vector2, Vector2, float, bool)>();
        foreach (var c in CellsOf(key))
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nb = ChunkOfCell((c.X + dx, c.Y + dy));
                if (nb == key || !Linked(key, nb) || !IsWaySide(c, dx, dy)) continue;
                float mid = SideMid(c, dx, dy);
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
                if (inward < -0.5f) continue;
                // (A plug of rock, not a slab: rounded at its ends and lumpy on its face, at least as wide as the way.)
                float reach = SealThick * 2f + SealLump * MathUtil.Clamp01(0.5f + CaveLayout.Fbm(p.X * 0.3f, p.Y * 0.3f, _noise + 111));
                float off = MathF.Sqrt(Square(MathF.Max(0f, across - half - SealPast + SealThick * 2f)) + Square(MathF.Max(0f, inward)));
                if (off <= reach)
                    chunk.Heights[j * chunk.Stride + i] = MathF.Max(chunk.Heights[j * chunk.Stride + i], SealTop + SealDome * (1f - off / reach));
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
    /// Where along a side between two cells its way lies (metres from the side's start, the same from either cell): anywhere
    /// leaving WayGap and a little more to its ends, so the ways between chunks never line up in rows.
    /// </summary>
    float SideMid((int X, int Y) cell, int dx, int dy)
    {
        var (ex, ey, axis) = dx != 0 ? (Math.Min(cell.X, cell.X + dx), cell.Y, 0UL) : (cell.X, Math.Min(cell.Y, cell.Y + dy), 1UL);
        // (Where the sea's reefs are lowest along it, of a few places: so its passage runs through open water.)
        float lo = WayGap + 1.5f, best = lo, least = Far;
        ulong h = Mix(Seed, (ulong)(uint)ex, (ulong)(uint)ey, axis, 0x50C);
        for (int i = 0; i < 7; i++)
        {
            float along = lo + (Cell - 2f * lo) * (i + Unit(h >> (i * 3)) * 0.8f) / 7f;
            var at = axis == 0 ? new Vector2((ex + 1) * Cell, ey * Cell + along) : new Vector2(ex * Cell + along, (ey + 1) * Cell);
            float reef = ReefAt(at);
            if (reef < least)
            {
                least = reef;
                best = along;
            }
        }
        return best;
    }

    readonly ConcurrentDictionary<((int X, int Y) A, (int X, int Y) B), ((int X, int Y) Cell, int DX, int DY)> _waySides = new();

    /// <summary>
    /// The one side two joined chunks' way passes (of the sides they share, the one whose way lies where the sea's reefs
    /// are lowest), as a cell of the first and the way out of it: one way to a pair, so few passages cut the reefs.
    /// </summary>
    ((int X, int Y) Cell, int DX, int DY) WaySide((int X, int Y) a, (int X, int Y) b) => _waySides.TryGetValue((a, b), out var hit) ? hit : _waySides.GetOrAdd((a, b), pair =>
    {
        var best = (pair.A, 0, 0);
        float least = Far;
        foreach (var c in CellsOf(pair.A))
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (ChunkOfCell((c.X + dx, c.Y + dy)) != pair.B) continue;
                float mid = SideMid(c, dx, dy);
                var at = dx != 0 ? new Vector2((c.X + (dx > 0 ? 1 : 0)) * Cell, c.Y * Cell + mid) : new Vector2(c.X * Cell + mid, (c.Y + (dy > 0 ? 1 : 0)) * Cell);
                float reef = ReefAt(at);
                if (reef < least)
                {
                    least = reef;
                    best = (c, dx, dy);
                }
            }
        return best;
    });

    /// <summary>The sea's reefs' noise where two neighbouring chunks' way would lie.</summary>
    float WayReef((int X, int Y) a, (int X, int Y) b)
    {
        var (lo, hi) = a.CompareTo(b) < 0 ? (a, b) : (b, a);
        var (c, dx, dy) = WaySide(lo, hi);
        float mid = SideMid(c, dx, dy);
        return ReefAt(dx != 0 ? new Vector2((c.X + (dx > 0 ? 1 : 0)) * Cell, c.Y * Cell + mid) : new Vector2(c.X * Cell + mid, (c.Y + (dy > 0 ? 1 : 0)) * Cell));
    }

    /// <summary>Whether a side of a cell (toward dx, dy) is the one its chunk and the chunk past it pass by (WaySide).</summary>
    bool IsWaySide((int X, int Y) cell, int dx, int dy)
    {
        var a = ChunkOfCell(cell);
        var b = ChunkOfCell((cell.X + dx, cell.Y + dy));
        if (a.CompareTo(b) > 0) return WaySide(b, a) == ((cell.X + dx, cell.Y + dy), -dx, -dy);
        return WaySide(a, b) == (cell, dx, dy);
    }

    /// <summary>The sea's reefs' broad noise at a point (above ReefLevel, a reef stands).</summary>
    float ReefAt(Vector2 p) => CaveLayout.Fbm(p.X * ReefScale, p.Y * ReefScale, _noise + 101);

    /// <summary>How far a way's gap in a wall reaches either side of its middle (metres).</summary>
    const float WayGap = 4f;

    readonly ConcurrentDictionary<(int X, int Y), float[]> _mids = new();

    /// <summary>A cell's ways' middles along its sides (as Sides: N, E, S, W).</summary>
    float[] MidsOf((int X, int Y) cell) => _mids.TryGetValue(cell, out var hit) ? hit : _mids.GetOrAdd(cell, c => Sides.Select(side => SideMid(c, side.DX, side.DY)).ToArray());

    readonly ConcurrentDictionary<(int X, int Y), (int Other, int Joined, int Shut, int Way)> _sides = new();

    /// <summary>A cell's sides (CaveTemplates.N/E/S/W bits) shared with another chunk, those of them joined to it, and those round a secret room.</summary>
    (int Other, int Joined, int Shut, int Way) SidesOf((int X, int Y) cell) => _sides.TryGetValue(cell, out var hit) ? hit : _sides.GetOrAdd(cell, c =>
    {
        var key = ChunkOfCell(c);
        int other = 0, joined = 0, shut = 0, way = 0;
        foreach (var (dx, dy, bit) in Sides)
        {
            var n = ChunkOfCell((c.X + dx, c.Y + dy));
            if (n == key) continue;
            other |= bit;
            if (!Linked(key, n)) continue;
            joined |= bit;
            if (IsWaySide(c, dx, dy)) way |= bit;
            // (A secret room is walled but for its ways, which its seals shut.)
            if (Secret(key) || Secret(n)) shut |= bit;
        }
        return (other, joined, shut, way);
    });

    static int FloorDiv(int v) => FloorDiv(v, Cell);

    static int FloorDiv(int v, int by) => v >= 0 ? v / by : -((-v + by - 1) / by);

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

    /// <summary>Exact distances (metres) to the nearest sample that is (or is not, <paramref name="to"/> false) set.</summary>
    internal static float[] Distances(bool[] set, int w, int h, bool to)
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
