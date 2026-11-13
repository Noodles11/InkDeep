using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Run;

/// <summary>What a reef of the dive's graph is (docs/LIGHT-AND-GRAPH.md §6.2): its hole's rim shows it.</summary>
public enum ReefKind
{
    /// <summary>An ordinary seeded reef.</summary>
    Reef,
    /// <summary>Lantern Bay's water: calm, heals and banks; its map hand-drawn.</summary>
    Haven,
    /// <summary>The bottom of a depth, one reef wide: its boss's arena round the Crack.</summary>
    Boss,
    /// <summary>A reef holding a treasure room (a clam room).</summary>
    Treasure,
    /// <summary>A reef holding a curse den (a leak cave off its way).</summary>
    Den,
}

/// <summary>The shape of the dive's graph (docs/LIGHT-AND-GRAPH.md §6.1). All of it tunable.</summary>
public static class DiveTuning
{
    /// <summary>Reefs across a row (a depth's first row of the whole dive is one wide: where every dive begins).</summary>
    public const int MinLanes = 2, MaxLanes = 3;
    /// <summary>Holes out of a reef (fewer only where the row below is narrower, or is the boss's).</summary>
    public const int MinExits = 2, MaxExits = 3;
    /// <summary>A haven's holes: Lantern Bay's map has this many (data/maps/haven.json).</summary>
    public const int HavenExits = 3;
    /// <summary>How often a reef has one exit more, a shortcut past the row below to the one after.</summary>
    public const float Shortcut = 0.15f;
    /// <summary>
    /// How often a reef from a depth's second row to the one before its boss's is a haven by chance (never straight after
    /// one); past those, a haven goes where most of the reefs that could reach none would reach it, until from every reef
    /// of a depth's first two rows a haven of the depth can be reached. A haven is a choice, never a toll.
    /// </summary>
    public const float HavenChance = 0.15f;
}

/// <summary>
/// The rules for the places that make a reef's kind (docs/LIGHT-AND-GRAPH.md §7): where in a depth each may
/// stand (its rows), how often, and how far from another of its kind (never straight after one on a route). The spots
/// within a reef (a secret, a shop, a shell cache, a Crab Deal) are rolled by its plan (LevelPlan, LevelStocking).
/// </summary>
public static class ReefPlaces
{
    public readonly record struct Rule(ReefKind Kind, int FromRow, int ToRow, float Chance, int AtLeastPerDepth);

    public static readonly Rule Treasure = new(ReefKind.Treasure, 1, 4, 0.3f, 1);
    public static readonly Rule Den = new(ReefKind.Den, 2, 4, 0.2f, 0);
    public static readonly Rule[] All = { Treasure, Den };
}

/// <summary>One reef of the dive's graph: which it is, what it is, where it sits across its row (0–1), its holes' reefs.</summary>
public sealed class DiveNode
{
    public LevelId Id;
    public ReefKind Kind;
    public float X;
    public readonly List<LevelId> Exits = new();
    public readonly List<LevelId> From = new();
    public int Row => Id.Level;
}

/// <summary>
/// The dive as a layered graph of reefs (docs/LIGHT-AND-GRAPH.md §6), from the dive's seed alone. The tutorial
/// is two reefs one wide; below it each depth has four rows of 2–4 reefs (the very first row of the dive one wide), each
/// reef 2–4 holes down into the row below (now and then a shortcut into the row after), and ends in its boss's reef,
/// one wide, whose holes lead to every reef of the next depth's first row. Past the last depth the loop begins again
/// below the tutorial. The generator keeps: every reef reached from above; every reef leading on to its depth's boss;
/// no two holes of a reef to the same reef; from every reef of a depth's first two rows a haven of the depth can be
/// reached; no row (two wide or more) all havens. Built a depth at a time
/// as it is asked for (each depth's havens hang on the routes above it), and kept per seed.
/// </summary>
public sealed class DiveGraph
{
    /// <summary>Rows in a depth: four of reefs and its boss's (the tutorial: two).</summary>
    public static int RowsOf(int depth) => depth == LevelPlan.Tutorial ? LevelStocking.TutorialLevels : LevelStocking.LevelsPerDepth;

    static readonly Dictionary<ulong, DiveGraph> Graphs = new();

    public static DiveGraph Of(SeedCode seed)
    {
        lock (Graphs)
        {
            if (!Graphs.TryGetValue(seed.Value, out var graph)) Graphs[seed.Value] = graph = new DiveGraph(seed);
            return graph;
        }
    }

    readonly SeedCode _seed;
    readonly RunStreams _streams;
    readonly Dictionary<(int Cycle, int Depth, int Row, int Lane), DiveNode> _nodes = new();
    readonly Dictionary<(int Cycle, int Depth, int Row), List<DiveNode>> _rows = new();
    /// <summary>The last depth built (cycle, depth), in dive order.</summary>
    (int Cycle, int Depth) _built;

    DiveGraph(SeedCode seed)
    {
        _seed = seed;
        _streams = new RunStreams(seed);
        BuildTutorial();
    }

    /// <summary>The reef at this place of the graph (its lane past the row's last: the row's last).</summary>
    public DiveNode Node(LevelId id)
    {
        var row = Row(id.Cycle, id.Depth, id.Level);
        return row[Math.Clamp(id.Lane, 0, row.Count - 1)];
    }

    /// <summary>The reef's id as the graph has it (a haven's marked).</summary>
    public LevelId Resolve(LevelId id) => Node(id).Id;

    /// <summary>A row of a depth, left to right.</summary>
    public IReadOnlyList<DiveNode> Row(int cycle, int depth, int row)
    {
        lock (this)
        {
            if (depth == LevelPlan.Tutorial) cycle = 1;
            row = Math.Clamp(row, 1, RowsOf(depth));
            Ensure(cycle, depth);
            return _rows[(cycle, depth, row)];
        }
    }

    /// <summary>Where the reef's holes lead, in its order (left to right across the rows below).</summary>
    public IReadOnlyList<LevelId> ExitsOf(LevelId id)
    {
        var node = Node(id);
        // (The boss's reef: the next depth's first row, built as it is asked for.)
        if (node.Exits.Count == 0) Row(Next(id.Cycle, id.Depth).Cycle, Next(id.Cycle, id.Depth).Depth, 1);
        return node.Exits;
    }

    /// <summary>A reef's kind by its id.</summary>
    public ReefKind KindOf(LevelId id) => Node(id).Kind;

    static (int Cycle, int Depth) Next(int cycle, int depth) =>
        depth < LevelPlan.Depths ? (cycle, depth + 1) : (cycle + 1, LevelPlan.Tutorial + 1);

    static int Order(int cycle, int depth) => depth == LevelPlan.Tutorial ? 0 : (cycle - 1) * (LevelPlan.Depths - LevelPlan.Tutorial) + depth - LevelPlan.Tutorial;

    void Ensure(int cycle, int depth)
    {
        while (Order(_built.Cycle, _built.Depth) < Order(cycle, depth))
        {
            var next = Next(_built.Cycle, _built.Depth);
            BuildDepth(next.Cycle, next.Depth);
            _built = next;
        }
    }

    DiveNode Add(int cycle, int depth, int row, int lane, int lanes)
    {
        var node = new DiveNode { Id = new LevelId(cycle, depth, row) { Lane = lane }, X = (lane + 0.5f) / lanes };
        _nodes[(cycle, depth, row, lane)] = node;
        if (!_rows.TryGetValue((cycle, depth, row), out var list)) _rows[(cycle, depth, row)] = list = new List<DiveNode>();
        list.Add(node);
        return node;
    }

    static void Link(DiveNode from, DiveNode to)
    {
        from.Exits.Add(to.Id);
        to.From.Add(from.Id);
    }

    /// <summary>The tutorial: two reefs, one wide; its rift's hole leads to where every dive begins.</summary>
    void BuildTutorial()
    {
        int t = LevelPlan.Tutorial;
        DiveNode? above = null;
        for (int row = 1; row <= RowsOf(t); row++)
        {
            var node = Add(1, t, row, 0, 1);
            if (above is not null) Link(above, node);
            above = node;
        }
        _built = (1, t);
    }

    /// <summary>One depth, below the one built last (whose boss's holes it joins); retried on a fresh stream if any rule breaks.</summary>
    void BuildDepth(int cycle, int depth)
    {
        int rows = RowsOf(depth);
        var above = Above(cycle, depth);
        for (int attempt = 0; ; attempt++)
        {
            var rng = _streams.Plan(new LevelId(cycle, depth, 0) { Lane = 100 + attempt });
            // (A failed attempt leaves nothing behind.)
            foreach (var n in above) n.Exits.Clear();
            for (int row = 1; row <= rows; row++)
            {
                if (!_rows.TryGetValue((cycle, depth, row), out var old)) continue;
                foreach (var n in old) _nodes.Remove((cycle, depth, row, n.Id.Lane));
                _rows.Remove((cycle, depth, row));
            }
            if (TryDepth(rng, cycle, depth, above) || attempt >= 40) return;
        }
    }

    /// <summary>The reefs whose holes lead into this depth's first row: the boss's reef above (or the tutorial's rift).</summary>
    List<DiveNode> Above(int cycle, int depth)
    {
        var prev = Order(cycle, depth) == 1 ? (1, LevelPlan.Tutorial) : PrevOf(cycle, depth);
        return _rows[(prev.Item1, prev.Item2, RowsOf(prev.Item2))];
    }

    static (int, int) PrevOf(int cycle, int depth) =>
        depth > LevelPlan.Tutorial + 1 ? (cycle, depth - 1) : (cycle - 1, LevelPlan.Depths);

    bool TryDepth(Rng rng, int cycle, int depth, List<DiveNode> above)
    {
        int rows = RowsOf(depth);
        bool first = Order(cycle, depth) == 1;
        var layers = new List<List<DiveNode>>();
        for (int row = 1; row <= rows; row++)
        {
            int lanes = row == rows || row == 1 && first ? 1 : DiveTuning.MinLanes + rng.Int(DiveTuning.MaxLanes - DiveTuning.MinLanes + 1);
            var layer = new List<DiveNode>();
            for (int lane = 0; lane < lanes; lane++) layer.Add(Add(cycle, depth, row, lane, lanes));
            layers.Add(layer);
        }
        // Into the first row: the reef above leads to every reef of it.
        foreach (var from in above)
            foreach (var to in layers[0]) Link(from, to);
        for (int row = 1; row < rows; row++)
        {
            var layer = layers[row - 1];
            var below = layers[row];
            foreach (var node in layer)
            {
                int most = Math.Min(DiveTuning.MaxExits, below.Count);
                int least = Math.Min(DiveTuning.MinExits, most);
                int count = least + rng.Int(most - least + 1);
                foreach (var to in Nearest(below, node.X).Take(count)) Link(node, to);
            }
            // Every reef of the row below reached: from the nearest reef above with a hole to spare.
            foreach (var to in below)
            {
                if (to.From.Count > 0) continue;
                var from = Nearest(layer, to.X).FirstOrDefault(n => n.Exits.Count < DiveTuning.MaxExits);
                if (from is null) return false;
                Link(from, to);
            }
            // Shortcuts: past the row below to the one after (never to the boss's).
            if (row + 2 < rows)
                foreach (var node in layer)
                {
                    if (node.Exits.Count >= DiveTuning.MaxExits || rng.NextFloat() >= DiveTuning.Shortcut) continue;
                    Link(node, Nearest(layers[row + 1], node.X).First());
                }
        }
        // The kinds: the boss's, the havens (a few by chance, then wherever a route would still miss one), the places.
        foreach (var node in layers.SelectMany(l => l)) node.Kind = node.Row == rows ? ReefKind.Boss : ReefKind.Reef;
        var band = layers.Skip(HavenFrom - 1).Take(rows - HavenFrom).SelectMany(l => l).ToList();
        foreach (var node in band)
            if (rng.NextFloat() < DiveTuning.HavenChance && !node.From.Any(f => NodeOf(f).Kind == ReefKind.Haven)) MakeHaven(node);
        for (int guard = 0; guard < 64 && Unserved(layers, rows) is { Count: > 0 } lacking; guard++)
        {
            // The reef most of those that reach no haven can reach (ties by chance): not one that would leave its row all
            // havens, nor (if it can be helped) straight after one.
            bool Spares(DiveNode n) => layers[n.Row - 1].Count(x => x.Kind != ReefKind.Haven) > 1;
            var counts = band.Where(n => n.Kind != ReefKind.Haven && Spares(n)).ToDictionary(n => n, n => lacking.Count(l => Reaches(l, n)));
            var on = counts.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
            var calm = on.Where(n => !n.From.Any(f => NodeOf(f).Kind == ReefKind.Haven)).ToList();
            if (calm.Count > 0) on = calm;
            if (on.Count == 0) return false;
            int most = on.Max(n => counts[n]);
            var best = on.Where(n => counts[n] == most).ToList();
            MakeHaven(best[rng.Int(best.Count)]);
        }
        foreach (var node in layers.SelectMany(l => l))
        {
            if (node.Kind != ReefKind.Reef) continue;
            float sum = 0f, place = rng.NextFloat();
            foreach (var rule in ReefPlaces.All)
            {
                if (node.Row < rule.FromRow || node.Row > rule.ToRow) continue;
                sum += rule.Chance;
                if (place >= sum) continue;
                if (!node.From.Any(f => NodeOf(f).Kind == rule.Kind)) node.Kind = rule.Kind;
                break;
            }
        }
        // A haven's map has HavenExits holes: its farthest exits go, while what they led to has another way in.
        foreach (var node in layers.SelectMany(l => l).Where(n => n.Kind == ReefKind.Haven))
        {
            var spare = node.Exits.OrderByDescending(e => MathF.Abs(NodeOf(e).X - node.X)).ToList();
            foreach (var e in spare)
            {
                if (node.Exits.Count <= DiveTuning.HavenExits) break;
                var to = NodeOf(e);
                if (to.From.Count < 2) continue;
                node.Exits.Remove(e);
                to.From.Remove(node.Id);
            }
            if (node.Exits.Count > DiveTuning.HavenExits) return false;
        }
        // A choice in every row: never a whole row of havens (where it is two wide or more).
        if (layers.Any(l => l.Count > 1 && l.All(n => n.Kind == ReefKind.Haven))) return false;
        if (Unserved(layers, rows).Count > 0) return false;
        // A depth's place rules that ask for at least so many.
        foreach (var rule in ReefPlaces.All)
        {
            var spots = layers.Skip(rule.FromRow - 1).Take(rule.ToRow - rule.FromRow + 1).SelectMany(l => l).ToList();
            int have = spots.Count(n => n.Kind == rule.Kind);
            var free = spots.Where(n => n.Kind == ReefKind.Reef && !NextTo(n, rule.Kind)).ToList();
            while (have < rule.AtLeastPerDepth && free.Count > 0)
            {
                var pick = free[rng.Int(free.Count)];
                pick.Kind = rule.Kind;
                free.RemoveAll(n => n == pick || NextTo(n, rule.Kind));
                have++;
            }
        }
        return true;
    }

    DiveNode NodeOf(LevelId id) => _nodes[(id.Depth == LevelPlan.Tutorial ? 1 : id.Cycle, id.Depth, id.Level, id.Lane)];

    /// <summary>Whether a reef has one of this kind straight above or below it on a route.</summary>
    bool NextTo(DiveNode node, ReefKind kind) =>
        node.From.Concat(node.Exits).Any(id => _nodes.TryGetValue((id.Cycle, id.Depth, id.Level, id.Lane), out var n) && n.Kind == kind);

    /// <summary>The first row a haven may be in (the row after the boss's reef is a reef: she comes out of a fight).</summary>
    const int HavenFrom = 2;

    /// <summary>A haven: its kind, its id marked (and what leads into it and out of it told).</summary>
    void MakeHaven(DiveNode node)
    {
        var was = node.Id;
        node.Kind = ReefKind.Haven;
        node.Id = was with { Haven = true };
        foreach (var f in node.From)
        {
            var p = NodeOf(f);
            for (int i = 0; i < p.Exits.Count; i++)
                if (p.Exits[i] == was) p.Exits[i] = node.Id;
        }
        foreach (var e in node.Exits)
        {
            var to = NodeOf(e);
            for (int i = 0; i < to.From.Count; i++)
                if (to.From[i] == was) to.From[i] = node.Id;
        }
    }

    /// <summary>The reefs of a depth's first two rows from which no haven of the depth can be reached.</summary>
    List<DiveNode> Unserved(List<List<DiveNode>> layers, int rows) =>
        layers.Take(2).SelectMany(l => l).Where(n => !layers.Skip(HavenFrom - 1).Take(rows - HavenFrom).SelectMany(l => l)
            .Any(h => h.Kind == ReefKind.Haven && (h == n || Reaches(n, h)))).ToList();

    /// <summary>Whether a route of the depth leads from one reef down to another.</summary>
    bool Reaches(DiveNode from, DiveNode to)
    {
        if (to.Row <= from.Row) return to == from;
        var seen = new HashSet<DiveNode>();
        var stack = new Stack<DiveNode>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var e in n.Exits)
            {
                if (e.Depth != from.Id.Depth || e.Cycle != from.Id.Cycle) continue;
                var x = NodeOf(e);
                if (x == to) return true;
                if (x.Row < to.Row && seen.Add(x)) stack.Push(x);
            }
        }
        return false;
    }

    static IEnumerable<DiveNode> Nearest(List<DiveNode> row, float x) =>
        row.OrderBy(n => MathF.Abs(n.X - x)).ThenBy(n => n.Id.Lane);
}
