using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The crossover's template caves (docs/proposals/CROSSOVER.md §2): a grid of cells, a Spelunky-style walk from one
/// side to the other with side cells off it and the odd loop, every used cell filled with a template made for its
/// openings (<see cref="CaveTemplates"/>), its chunk slots filled at random, and the whole eroded with noise so the seams
/// between cells disappear. The result is a <see cref="CaveLayout"/> like a grown one: chambers (one a cell), tunnels
/// (between joined cells), places (rooms in room cells, open places on POI marks), and the open water; so the rest of
/// the generator (the rock, the features, the decoration) builds on it unchanged.
/// </summary>
public sealed partial class CaveLayout
{
    /// <summary>Its mob marks (where the roster puts a creature) and its POI marks not taken by a place, all in open water.</summary>
    public readonly List<(Vector2 At, MobTag Tag)> MobMarks = new();
    public readonly List<Vector2> PoiMarks = new();
    /// <summary>Its bubble vaults' marks (CROSSOVER.md §5.1), in open water by a wall.</summary>
    public readonly List<Vector2> VaultMarks = new();

    /// <summary>The grid: Cells × Cells cells of CaveTemplates.Cell metres, centred in the level's square.</summary>
    public const int Cells = 5;
    public static int GridOrigin => (LevelMap.Seeded - Cells * CaveTemplates.Cell) / 2;
    /// <summary>The walk goes down a row this often at each step (else on along the row, till its end).</summary>
    const float DownChance = 0.3f;
    /// <summary>Two joined cells that are not yet joined are joined this often (a loop).</summary>
    const float TemplateLoop = 0.2f;
    /// <summary>Water a cell holds, about (to choose how many side cells the level needs).</summary>
    const float CellWater = 185f;
    /// <summary>Tries at a layout within the area's band before the attempt is given up.</summary>
    const int TemplateTries = 40;
    /// <summary>The erosion (metres): two octaves of noise on the signed distance to the rock.</summary>
    const float Erode = 1.5f, ErodeFine = 0.6f;

    /// <summary>The boss's arena (CROSSOVER.md §4): a round cave this wide in a 2 × 2 block of cells, its doorways this wide.</summary>
    public const float ArenaRadius = 15f, ArenaDoor = 6f;

    /// <summary>
    /// One template cave for the level, or why none could be made. <paramref name="kinds"/> are the plan's places:
    /// those that live in a cave take a room cell, the rest a POI mark; one that finds no room or mark is left out.
    /// <paramref name="boss"/>: a boss level, whose walk ends in the boss's arena (its exit), a 2 × 2 block of cells.
    /// </summary>
    public static (CaveLayout? Layout, string? Why) FromTemplates(Rng rng, IReadOnlyList<PoiKind> kinds, Func<PoiKind, float> radius, bool boss = false, OctoShoots.Core.Run.Biome biome = OctoShoots.Core.Run.Biome.SunlitReef)
    {
        string why = "no layout";
        for (int t = 0; t < TemplateTries; t++)
        {
            var (g, w) = TryTemplates(rng, kinds, radius, boss, biome);
            if (g is not null) return (g, null);
            why = w!;
        }
        return (null, why);
    }

    enum Use { None, Path, Side, Room, Arena }

    static (CaveLayout? Layout, string? Why) TryTemplates(Rng rng, IReadOnlyList<PoiKind> kinds, Func<PoiKind, float> radius, bool boss, OctoShoots.Core.Run.Biome biome)
    {
        const int C = Cells, CS = CaveTemplates.Cell;
        var use = new Use[C * C];
        var depth = new int[C * C];
        var links = new HashSet<(int A, int B)>();
        void Link(int a, int b) => links.Add(a < b ? (a, b) : (b, a));
        bool Linked(int a, int b) => links.Contains(a < b ? (a, b) : (b, a));
        IEnumerable<int> Around(int i)
        {
            int x = i % C, y = i / C;
            if (y > 0) yield return i - C;
            if (x < C - 1) yield return i + 1;
            if (y < C - 1) yield return i + C;
            if (x > 0) yield return i - 1;
        }

        // 1. The path: from a cell of the first row, along the row and down, to the last row.
        var path = new List<int>();
        int cx = rng.Int(C), cy = 0, dir = rng.NextFloat() < 0.5f ? -1 : 1;
        path.Add(cy * C + cx);
        while (true)
        {
            int prev = cy * C + cx;
            bool down = rng.NextFloat() < DownChance || cx + dir < 0 || cx + dir >= C;
            if (down)
            {
                if (cy == C - 1) break;
                cy++;
                dir = rng.NextFloat() < 0.5f ? -1 : 1;
            }
            else cx += dir;
            int cur = cy * C + cx;
            Link(prev, cur);
            path.Add(cur);
        }
        foreach (int i in path) use[i] = Use.Path;
        int start = path[0], exit = path[^1];

        // 1b. A boss level's arena: the 2 × 2 block of the last two rows round where the walk ends. The walk stops where it
        // first enters the block (the cells it would have gone on through are left out), and the block is one room.
        var arena = new List<int>();
        if (boss)
        {
            int ex = exit % C;
            int x0 = ex == 0 ? 0 : ex == C - 1 ? C - 2 : ex - rng.Int(2);
            arena.AddRange(new[] { (C - 2) * C + x0, (C - 2) * C + x0 + 1, (C - 1) * C + x0, (C - 1) * C + x0 + 1 });
            int enter = path.FindIndex(arena.Contains);
            for (int k = enter + 1; k < path.Count; k++)
                if (!arena.Contains(path[k])) use[path[k]] = Use.None;
            links.RemoveWhere(l => use[l.A] == Use.None || use[l.B] == Use.None);
            // (Within the block it is one room: no links between its own cells.)
            links.RemoveWhere(l => arena.Contains(l.A) && arena.Contains(l.B));
            path = path.Take(enter + 1).ToList();
            foreach (int i in arena) use[i] = Use.Arena;
            exit = path[^1];
        }

        // 2. Side cells, up to two deep, until the level holds about its water.
        float goal = Area * rng.Range(0.92f, 1.04f);
        for (int misses = 0; use.Count(u => u is Use.Path or Use.Side or Use.Arena) * CellWater < goal && misses < 200;)
        {
            bool offPath = rng.NextFloat() < RouteParent;
            var parents = Enumerable.Range(0, C * C).Where(i => (use[i] == Use.Path && !arena.Contains(i)) || (!offPath && use[i] == Use.Side && depth[i] < BranchDepth)).ToList();
            int parent = parents[rng.Int(parents.Count)];
            var free = Around(parent).Where(n => use[n] == Use.None).ToList();
            if (free.Count == 0)
            {
                misses++;
                continue;
            }
            int cell = free[rng.Int(free.Count)];
            use[cell] = Use.Side;
            depth[cell] = depth[parent] + 1;
            Link(parent, cell);
        }

        // 3. The orientation: the walk turned or mirrored, so the level runs any way across the square.
        int way = rng.Int(8);
        (int X, int Y) At(int i) => CaveTemplates.Way(i % C, i / C, C, way);
        Vector2 Middle(int i) => new(GridOrigin + At(i).X * CS + CS / 2f, GridOrigin + At(i).Y * CS + CS / 2f);
        // The far end: the arena's middle on a boss level, else the last cell of the walk.
        Vector2 arenaAt = arena.Count > 0 ? arena.Aggregate(Vector2.Zero, (s, i) => s + Middle(i)) / arena.Count : Vector2.Zero;
        Vector2 FarEnd() => boss ? arenaAt : Middle(exit);

        // 4. Rooms: a cell of rock off the cave holding a place in its own cave, apart from the start, the exit and each other.
        var rooms = new List<(int Cell, int Parent, PoiKind Kind)>();
        foreach (var kind in kinds.Where(Poi.IsCaveHosted))
        {
            var options = new List<(int Cell, int Parent)>();
            for (int i = 0; i < C * C; i++)
            {
                if (use[i] != Use.None || Around(i).Any(n => use[n] == Use.Room)) continue;
                if (Vector2.Distance(Middle(i), Middle(start)) < PlaceRules.FromStart + 6f || Vector2.Distance(Middle(i), FarEnd()) < PlaceRules.FromExit + 6f + (boss ? ArenaRadius : 0f)) continue;
                if (rooms.Any(r => Vector2.Distance(Middle(r.Cell), Middle(i)) < 25f)) continue;
                foreach (int n in Around(i))
                    if (use[n] is Use.Path or Use.Side) options.Add((i, n));
            }
            if (options.Count == 0) continue;
            var (cell, parent) = options[rng.Int(options.Count)];
            use[cell] = Use.Room;
            Link(parent, cell);
            rooms.Add((cell, parent, kind));
        }

        // 5. Loops: neighbours not yet joined, joined now and then.
        for (int i = 0; i < C * C; i++)
        foreach (int n in Around(i))
            if (n > i && use[i] is Use.Path or Use.Side && use[n] is Use.Path or Use.Side && !Linked(i, n) && rng.NextFloat() < TemplateLoop)
                Link(i, n);

        // 6. Each used cell's template, by its openings (as turned), and its chunks.
        int OpeningsOf(int i)
        {
            int open = 0;
            var (x, y) = At(i);
            foreach (var (a, b) in links)
            {
                if (a != i && b != i) continue;
                var (ox, oy) = At(a == i ? b : a);
                open |= (ox - x, oy - y) switch
                {
                    (0, -1) => CaveTemplates.N,
                    (1, 0) => CaveTemplates.E,
                    (0, 1) => CaveTemplates.S,
                    _ => CaveTemplates.W,
                };
            }
            return open;
        }

        var g = new CaveLayout();
        int NN = g.N;
        var raw = new bool[NN * NN];
        var roomAt = new Dictionary<int, Vector2>();
        var mobs = new List<(Vector2 At, MobTag Tag)>();
        var spots = new List<Vector2>();
        var vaults = new List<Vector2>();
        for (int i = 0; i < C * C; i++)
        {
            if (use[i] is Use.None or Use.Arena) continue;
            int opens = OpeningsOf(i);
            // (Its biome's cells, CROSSOVER.md §8; any way they lack, the Sunlit Reef's.)
            var pool = (use[i] == Use.Room ? CaveTemplates.RoomVariants : CaveTemplates.VariantsOf(biome)).Where(v => v.Openings == opens).ToList();
            if (pool.Count == 0 && use[i] != Use.Room) pool = CaveTemplates.SunlitVariants.Where(v => v.Openings == opens).ToList();
            if (pool.Count == 0) return (null, $"no cave template opens {opens}");
            var v = pool[rng.Int(pool.Count)];
            var cell = (char[,])v.Grid.Clone();
            foreach (var (sx, sy) in v.Slots)
            {
                var chunks = CaveTemplates.ChunksOf(biome);
                var chunk = chunks[rng.Int(chunks.Count)];
                for (int j = 0; j < CaveTemplates.Chunk; j++)
                for (int k = 0; k < CaveTemplates.Chunk; k++)
                    cell[sx + k, sy + j] = chunk[k, j];
            }
            int ox = GridOrigin + At(i).X * CS, oy = GridOrigin + At(i).Y * CS;
            for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                char c = cell[x, y];
                var p = new Vector2(ox + x, oy + y);
                raw[(oy + y) * NN + ox + x] = CaveTemplates.Water(c) && c != 'R';
                if (CaveTemplates.TagOf(c) is { } tag) mobs.Add((p, tag));
                else if (c == '*') spots.Add(p);
                else if (c == 'v') vaults.Add(p);
                else if (c == 'R') roomAt[i] = p;
            }
        }

        // 6b. The arena: a round cave in its block, a doorway out to each cell joined to it, the wall wavering a little.
        if (boss)
        {
            for (int y = 0; y < NN; y++)
            for (int x = 0; x < NN; x++)
            {
                var p = new Vector2(x, y) - arenaAt;
                float a = MathF.Atan2(p.Y, p.X);
                if (p.Length() < ArenaRadius * (1f + 0.04f * MathF.Sin(3f * a + 1.3f) + 0.03f * MathF.Sin(5f * a))) raw[y * NN + x] = true;
            }
            foreach (var (a, b) in links)
            {
                int inside = arena.Contains(a) ? a : arena.Contains(b) ? b : -1;
                if (inside < 0) continue;
                int outside = inside == a ? b : a;
                var door = (Middle(inside) + Middle(outside)) * 0.5f;
                var inward = Vector2.Normalize(arenaAt - door);
                var across = new Vector2(-inward.Y, inward.X);
                for (float t = -0.5f; t <= ArenaRadius; t += 0.5f)
                for (float s = -ArenaDoor / 2f; s <= ArenaDoor / 2f; s += 0.5f)
                {
                    var q = door + inward * t + across * s;
                    int qx = (int)MathF.Round(q.X), qy = (int)MathF.Round(q.Y);
                    if (qx >= 0 && qy >= 0 && qx < NN && qy < NN) raw[qy * NN + qx] = true;
                }
            }
        }

        // 7. Erosion: the signed distance to the rock, roughened by noise, smoothed twice; the rim kept rock.
        uint seed = (uint)(rng.NextU64() >> 34);
        var toRock = Chamfer(raw, false);
        var toWater = Chamfer(raw, true);
        var open = new bool[NN * NN];
        for (int y = 0; y < NN; y++)
        for (int x = 0; x < NN; x++)
        {
            int i = y * NN + x;
            float sd = raw[i] ? 0.5f - toRock[i] : toWater[i] - 0.5f;
            float v = sd + Fbm(x * 0.13f, y * 0.13f, seed) * Erode + Fbm(x * 0.41f, y * 0.41f, seed + 31) * ErodeFine;
            float edge = MathF.Min(MathF.Min(x, y), MathF.Min(g.Size - x, g.Size - y));
            if (edge < Rim + 1f) v = MathF.Max(v, Rim + 1f - edge);
            open[i] = v < 0f;
        }
        for (int pass = 0; pass < 2; pass++)
        {
            var next = new bool[NN * NN];
            for (int y = 1; y < NN - 1; y++)
            for (int x = 1; x < NN - 1; x++)
            {
                int n = 0;
                for (int j = -1; j <= 1; j++)
                for (int k = -1; k <= 1; k++)
                    if (open[(y + j) * NN + x + k]) n++;
                next[y * NN + x] = n >= 5;
            }
            open = next;
        }

        // 8. Each cell's anchor: its most open point (the middle of its chamber).
        var clear = Chamfer(open, false);
        var anchor = new Dictionary<int, (Vector2 At, float Clear)>();
        for (int i = 0; i < C * C; i++)
        {
            if (use[i] is not (Use.Path or Use.Side)) continue;
            int ox = GridOrigin + At(i).X * CS, oy = GridOrigin + At(i).Y * CS;
            float best = 0f, near = float.MaxValue;
            Vector2 at = Middle(i);
            for (int y = oy; y < oy + CS; y++)
            for (int x = ox; x < ox + CS; x++)
            {
                int k = y * NN + x;
                if (!open[k]) continue;
                float d = Vector2.Distance(new Vector2(x, y), Middle(i));
                if (clear[k] > best + 0.01f || (MathF.Abs(clear[k] - best) <= 0.01f && d < near))
                {
                    best = clear[k];
                    near = d;
                    at = new Vector2(x, y);
                }
            }
            if (best < 2f) return (null, "a cave cell eroded shut");
            anchor[i] = (at, best);
        }
        if (boss && !open[g.Index(arenaAt)]) return (null, "the arena eroded shut");

        // What joins the start.
        g.Open = open;
        var keep = new bool[NN * NN];
        int area = 0;
        {
            var stack = new Stack<int>();
            int si = g.Index(anchor[start].At);
            stack.Push(si);
            keep[si] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % NN, y = i / NN;
                area++;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= NN || ny >= NN) continue;
                    int j = ny * NN + nx;
                    if (open[j] && !keep[j])
                    {
                        keep[j] = true;
                        stack.Push(j);
                    }
                }
            }
        }
        g.Open = keep;
        g.OpenArea = area;
        if (anchor.Values.Any(a => !keep[g.Index(a.At)])) return (null, "a cave cell cut off by the erosion");
        if (boss && !keep[g.Index(arenaAt)]) return (null, "the arena cut off by the erosion");
        if (area < g.Lo || area > g.Hi) return (null, $"cave water {area} m² (want {g.Lo:0}–{g.Hi:0})");

        // 9. The chambers and the tunnels between them.
        var chamberOf = new Dictionary<int, Chamber>();
        foreach (var (i, a) in anchor)
        {
            var role = i == start ? Role.Start : i == exit && !boss ? Role.Exit : use[i] == Use.Path ? Role.Main : Role.Branch;
            var chamber = new Chamber { At = a.At, R = Math.Clamp(a.Clear, 3.5f, 9f), Role = role, Depth = depth[i] };
            chamberOf[i] = chamber;
            g.Chambers.Add(chamber);
        }
        g.Start = chamberOf[start];
        if (boss)
        {
            // The arena: one chamber for its whole block, the exit.
            var hall = new Chamber { At = arenaAt, R = ArenaRadius, Role = Role.Exit };
            g.Chambers.Add(hall);
            foreach (int i in arena) chamberOf[i] = hall;
            g.Exit = hall;
        }
        else g.Exit = chamberOf[exit];
        var clearKept = Chamfer(keep, false);
        foreach (var (a, b) in links)
        {
            if (use[a] == Use.Room || use[b] == Use.Room) continue;
            var line = g.WaterPath(clearKept, chamberOf[a].At, chamberOf[b].At);
            if (line is null) return (null, "two joined cells not joined by water");
            chamberOf[a].Links.Add(chamberOf[b]);
            chamberOf[b].Links.Add(chamberOf[a]);
            bool main = (use[a] is Use.Path or Use.Arena) && (use[b] is Use.Path or Use.Arena);
            g.Tunnels.Add(new Tunnel { A = chamberOf[a], B = chamberOf[b], Kind = main ? CorridorKind.Main : CorridorKind.Side, Points = line.Value.Points, Width = Math.Clamp(2f * line.Value.Least, 4.2f, 9f) });
        }

        // 10. The places: each room behind its spur, then the open places on POI marks.
        foreach (var (cell, parent, kind) in rooms)
        {
            float r = radius(kind);
            var (px, py) = At(parent);
            var (rx, ry) = At(cell);
            var facing = new Vector2(px - rx, py - ry);
            var at = roomAt[cell];
            var mouth = at + facing * r;
            var outside = mouth + facing * 1.5f;
            if (!keep[g.Index(outside)]) return (null, "a room's spur cut off by the erosion");
            if (Vector2.Distance(at, g.Start.At) < PlaceRules.FromStart || Vector2.Distance(at, g.Exit.At) < PlaceRules.FromExit) return (null, "a room too close to the start or the exit");
            var line = g.WaterPath(clearKept, chamberOf[parent].At, outside);
            if (line is null) return (null, "a room's spur not joined by water");
            var points = line.Value.Points;
            points.Add(mouth);
            var tunnel = new Tunnel { A = chamberOf[parent], B = new Chamber { At = at, R = r, Role = Role.Branch, Room = true, Keep = RoomKeep(kind), Depth = depth[parent] + 1 }, Kind = CorridorKind.Spur, Points = points, Width = Math.Clamp(2f * line.Value.Least, 4.2f, 6f) };
            if (SpurLength(points) > LevelValidator.MaxSpur - 1f) return (null, "a room's spur too long");
            g.Chambers.Add(tunnel.B);
            g.Tunnels.Add(tunnel);
            g.Places.Add(new Place { Kind = kind, At = at, Radius = r, Spur = tunnel, Facing = facing });
        }
        var marks = spots.Where(p => keep[g.Index(p)]).ToList();
        foreach (var kind in kinds.Where(k => !Poi.IsCaveHosted(k) && k != PoiKind.Hole))
        {
            var options = marks.Where(p =>
                Vector2.Distance(p, g.Start.At) >= PlaceRules.FromStart && Vector2.Distance(p, g.Exit.At) >= PlaceRules.FromExit
                && g.Places.All(o => Vector2.Distance(o.At, p) >= PlaceRules.Between(o.Kind, kind))
                && (kind != PoiKind.ShellCache || Along(p, g.Start.At, g.Exit.At) is >= 0.3f and <= 0.7f)).ToList();
            if (options.Count == 0) continue;
            // A relic hides (CROSSOVER.md §5.2): the mark farthest from the walk's own cells.
            var walk = g.Chambers.Where(c => !c.Room && c.Role is Role.Start or Role.Main or Role.Exit).Select(c => c.At).ToList();
            var at = kind == PoiKind.Relic && walk.Count > 0
                ? options.OrderByDescending(p => walk.Min(w => Vector2.Distance(w, p))).First()
                : options[rng.Int(options.Count)];
            marks.Remove(at);
            g.Places.Add(new Place { Kind = kind, At = at, Radius = radius(kind) });
        }
        // The early place: the one nearest the start, where it is near enough.
        if (g.Places.OrderBy(p => Vector2.Distance(p.At, g.Start.At)).FirstOrDefault() is { } early && Vector2.Distance(early.At, g.Start.At) <= EarlyMax)
            early.Early = true;

        g.MobMarks.AddRange(mobs.Where(m => keep[g.Index(m.At)]));
        g.PoiMarks.AddRange(marks);
        g.VaultMarks.AddRange(vaults.Where(p => keep[g.Index(p)]));
        g.PathToExit = g.Geodesic(g.Start.At)[g.Index(g.Exit.At)];
        if (!float.IsFinite(g.PathToExit) || g.PathToExit < MinPath) return (null, $"the far side is {g.PathToExit:0} m from the start along the water");
        return (g, null);
    }

    /// <summary>How far along the start → exit a point lies (0 at the start, 1 at the exit).</summary>
    static float Along(Vector2 p, Vector2 start, Vector2 exit)
    {
        var axis = exit - start;
        return Vector2.Dot(p - start, axis) / MathF.Max(axis.LengthSquared(), 1f);
    }

    static float SpurLength(List<Vector2> points)
    {
        float l = 0f;
        for (int i = 1; i < points.Count; i++) l += Vector2.Distance(points[i - 1], points[i]);
        return l;
    }

    /// <summary>
    /// The way through the water between two points, keeping to the middle of it (Dijkstra, the cost of a step growing
    /// near the rock), sampled every ~2 m, and the least room either side of it; null where the water does not join them.
    /// </summary>
    (List<Vector2> Points, float Least)? WaterPath(float[] clear, Vector2 from, Vector2 to)
    {
        int s = Index(from), t = Index(to);
        if (!Open[s] || !Open[t]) return null;
        var cost = new float[N * N];
        var prev = new int[N * N];
        Array.Fill(cost, float.PositiveInfinity);
        cost[s] = 0f;
        var queue = new PriorityQueue<int, float>();
        queue.Enqueue(s, 0f);
        while (queue.TryDequeue(out int i, out float d))
        {
            if (d > cost[i]) continue;
            if (i == t) break;
            int x = i % N, y = i / N;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                int j = ny * N + nx;
                if (!Open[j]) continue;
                if (dx != 0 && dy != 0 && (!Open[y * N + nx] || !Open[ny * N + x])) continue;
                float step = (dx != 0 && dy != 0 ? 1.41421356f : 1f) * (1f + 4f / (clear[j] + 0.5f));
                if (d + step < cost[j])
                {
                    cost[j] = d + step;
                    prev[j] = i;
                    queue.Enqueue(j, cost[j]);
                }
            }
        }
        if (float.IsPositiveInfinity(cost[t])) return null;
        var cells = new List<int>();
        for (int i = t; i != s; i = prev[i]) cells.Add(i);
        cells.Add(s);
        cells.Reverse();
        float least = cells.Min(i => clear[i]);
        var points = new List<Vector2>();
        for (int k = 0; k < cells.Count; k += 2) points.Add(new Vector2(cells[k] % N, cells[k] / N));
        if ((cells.Count - 1) % 2 != 0) points.Add(new Vector2(t % N, t / N));
        if (points.Count < 2) points.Add(new Vector2(t % N, t / N));
        return (points, least);
    }
}
