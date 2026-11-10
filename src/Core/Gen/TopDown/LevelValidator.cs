using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The rules a generated level must keep (DESIGN-TOPDOWN §4.1, §4.5), plus the swim-band flood fill they are
/// checked with. A level that breaks any of them is regenerated from the next attempt's sub-streams.
/// </summary>
public static class LevelValidator
{
    public const float MinExitGeodesic = 55f;
    /// <summary>The longest spur from the network to a place.</summary>
    public const float MaxSpur = 34f;
    /// <summary>How much nearer than placed a further hole may end up, sunk to its chamber's middle.</summary>
    const float HoleGive = 10f;

    /// <summary>Null when the level keeps every rule; otherwise the first one it breaks.</summary>
    public static string? Validate(LevelMap map)
    {
        int Count(PoiKind k) => map.Pois.Count(p => p.Kind == k);
        if (Count(PoiKind.Start) != 1 || Count(PoiKind.Exit) != 1) return "missing start or exit";
        if (Count(PoiKind.TreasureCave) > 2 || Count(PoiKind.Shop) > 1 || Count(PoiKind.Secret) > 1 || Count(PoiKind.CurseDen) > 1 || Count(PoiKind.LeakCave) > 1
            || Count(PoiKind.ShellCache) > 1) return "optional POI counts out of range";

        // Scatter rules.
        var start = map.Start;
        var exit = map.Exit;
        var others = map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit or PoiKind.Hole)).ToList();
        // A reef's further holes (each sunk to its chamber's middle, so a little nearer than placed): away from the start,
        // the exit, each other and the places.
        var holes = map.Pois.Where(p => p.Kind == PoiKind.Hole).ToList();
        for (int i = 0; i < holes.Count; i++)
        {
            if (Vector2.Distance(holes[i].Position, map.Start.Position) < CaveLayout.FromStart - HoleGive) return "a hole down too close to the start";
            if (Vector2.Distance(holes[i].Position, map.Exit.Position) < CaveLayout.FromExit - HoleGive) return "a hole down too close to the exit";
            for (int j = i + 1; j < holes.Count; j++)
                if (Vector2.Distance(holes[i].Position, holes[j].Position) < PlaceRules.HoleApart - HoleGive) return "two holes down too close";
            if (others.Any(o => Vector2.Distance(o.Position, holes[i].Position) < CaveLayout.Spacing - HoleGive)) return "a place too close to a hole down";
        }
        if (others.Count + (map.HasBoss ? 1 : 0) > LevelStocking.MaxPlaces) return $"{others.Count} places (want at most {LevelStocking.MaxPlaces})";
        for (int i = 0; i < others.Count; i++)
        {
            if (Vector2.Distance(others[i].Position, exit.Position) < CaveLayout.FromExit) return $"{others[i].Kind} inside the exit's exclusion zone";
            if (Vector2.Distance(others[i].Position, start.Position) < CaveLayout.FromStart) return $"{others[i].Kind} too close to the start";
            for (int j = i + 1; j < others.Count; j++)
                if (Vector2.Distance(others[i].Position, others[j].Position) < CaveLayout.Spacing) return $"{others[i].Kind} and {others[j].Kind} closer than {CaveLayout.Spacing:0} m";
        }
        // One place sits early, unless the shell cache (which keeps to the middle of the way) is the level's only place.
        var early = others.Where(p => p.Early).ToList();
        if (others.Any(p => p.Kind != PoiKind.ShellCache) && (early.Count != 1 || Vector2.Distance(early[0].Position, start.Position) > CaveLayout.EarlyMax)) return $"{early.Count} early POIs (want exactly one, {CaveLayout.FromStart:0}–{CaveLayout.EarlyMax:0} m from the start)";

        // Tunnels: a room's spur stays short, a pass narrow, and nothing on a tunnel's middle above the swim level.
        foreach (var spur in map.Corridors.Where(c => c.Kind == CorridorKind.Spur))
            if (spur.Length > MaxSpur) return $"a spur of {spur.Length:0.0} m (want ≤ {MaxSpur:0} m)";
        if (map.Corridors.Any(c => c.Kind == CorridorKind.Pass && c.Width is < 3f or > 5f)) return "a pass width outside 3–5 m";
        string? flat = CheckCorridorFloors(map);
        if (flat is not null) return flat;

        // Topography: the shaft is the only deep water (the deepest point of the level lies in it).
        if (!map.LeakCave && !map.InShaft(DeepestPoint(map), 1f)) return "the shaft is not the deepest point";
        float peak = InteriorPeak(map);
        if (peak is < 3f or > 14f) return $"highest peak {peak:0.0} m (want 3–14 m)";

        // Arches across corridors.
        var arches = map.Canopies.Where(c => c.Kind == CanopyKind.Arch).ToList();
        if (arches.Count > TopDownGenerator.ArchesCap) return $"{arches.Count} arches (want at most {TopDownGenerator.ArchesCap})";
        foreach (var arch in arches)
        {
            float span = arch.HalfLength * 2f;
            if (span < TopDownGenerator.ArchSpanMin || span > TopDownGenerator.ArchSpanMax) return $"an arch spanning {span:0.0} m (want {TopDownGenerator.ArchSpanMin:0}–{TopDownGenerator.ArchSpanMax:0} m)";
            if (!ArchCrossesCorridor(map, arch)) return "an arch does not span its corridor";
        }

        // Caves: apart, and opening onto a corridor or open water.
        var caves = map.Caves;
        for (int i = 0; i < caves.Count; i++)
        {
            if (!CaveMouthOpens(map, caves[i])) return $"the {map.Pois[caves[i].Poi].Kind} cave's mouth faces rock";
            for (int j = i + 1; j < caves.Count; j++)
                if (Vector2.Distance(map.Pois[caves[i].Poi].Position, map.Pois[caves[j].Poi].Position) < 25f) return "two caves closer than 25 m";
        }

        // Loot under the floor.
        if (map.Pockets.Count < TopDownGenerator.PocketsMin || map.Pockets.Count > TopDownGenerator.PocketsMax)
            return $"{map.Pockets.Count} sealed pockets (want {TopDownGenerator.PocketsMin}–{TopDownGenerator.PocketsMax})";
        if (map.Coins.Count < TopDownGenerator.CoinsMin || map.Coins.Count > TopDownGenerator.CoinsMax)
            return $"{map.Coins.Count} buried coins (want {TopDownGenerator.CoinsMin}–{TopDownGenerator.CoinsMax})";

        // Reachability and the exit's distance.
        var dist = Distances(map, start.Position);
        // (A leak cave's place is in the rock behind its mouth: the water out of its mouth must be reached.)
        foreach (var poi in map.Pois)
            if (poi.Kind != PoiKind.LeakCave && float.IsPositiveInfinity(DistanceAt(dist, poi.Position))) return $"the {poi.Kind} cannot be reached from the start";
        foreach (var mouth in map.Mouths)
            if (!Enumerable.Range(6, 19).Select(k => mouth.At - mouth.Into * (k * 0.5f)).Any(q => map.IsOpen(q) && !float.IsPositiveInfinity(DistanceAt(dist, q))))
                return "a leak cave's mouth cannot be reached from the start";
        // The playable area: the water she can reach.
        int area = WaterArea(map);
        float lo = AreaMin * map.AreaScale, hi = AreaMax * map.AreaScale, least = MinExitGeodesic * MathF.Sqrt(map.AreaScale);
        if (area < (int)lo || area > (int)hi) return $"{area} m² of water to swim (want {lo:0}–{hi:0} m²)";
        float geodesic = DistanceAt(dist, exit.Position);
        if (geodesic < least) return $"the exit is only {geodesic:0} m from the start (want ≥ {least:0} m)";
        return null;
    }

    /// <summary>Water under a path is at least this deep.</summary>
    public const float PathClearance = 1f;

    /// <summary>How far either side of a tunnel's centre line its floor is checked (its walls are craggy past that).</summary>
    public const float PathCore = 0.6f;

    /// <summary>Samples along every tunnel's middle: the seabed lies at least <see cref="PathClearance"/> below the swim level.</summary>
    public static string? CheckCorridorFloors(LevelMap map)
    {
        foreach (var c in map.Corridors)
        {
            for (int i = 0; i < c.Points.Count; i++)
            {
                Vector2 tangent = Geo.Normalize(c.Points[Math.Min(i + 1, c.Points.Count - 1)] - c.Points[Math.Max(i - 1, 0)], Vector2.UnitX);
                Vector2 nrm = Geo.Perp(tangent);
                for (float k = -1f; k <= 1.01f; k += 0.5f)
                {
                    Vector2 p = c.Points[i] + nrm * (k * MathF.Min(PathCore, c.HalfWidth));
                    if (!InSquare(map, p)) continue;
                    float h = map.HeightAt(p);
                    if (h > -PathClearance) return $"a {c.Kind} corridor rises to {h:0.00} m at ({p.X:0.0}, {p.Y:0.0})";
                }
            }
        }
        return null;
    }

    /// <summary>The playable area rule (docs/CAVEGEN.md): reachable water between these (m²).</summary>
    public const float AreaMin = CaveLayout.AreaMin, AreaMax = CaveLayout.AreaMax;

    /// <summary>The playable area: grid points under the swim level joined (4-way) to the start's, in m².</summary>
    public static int WaterArea(LevelMap map)
    {
        int S = map.Samples;
        var (sx, sy) = ((int)MathF.Round(map.Start.Position.X), (int)MathF.Round(map.Start.Position.Y));
        var seen = new bool[S * S];
        var stack = new Stack<int>();
        if (map[sx, sy] > LevelMap.BlockHeight) return 0;
        stack.Push(sy * S + sx);
        seen[sy * S + sx] = true;
        int area = 0;
        while (stack.Count > 0)
        {
            int i = stack.Pop(), x = i % S, y = i / S;
            area++;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= S || ny >= S) continue;
                int j = ny * S + nx;
                if (seen[j] || map[nx, ny] > LevelMap.BlockHeight) continue;
                seen[j] = true;
                stack.Push(j);
            }
        }
        return area;
    }

    static bool InSquare(LevelMap map, Vector2 p) => p.X >= 0f && p.Y >= 0f && p.X <= map.Size && p.Y <= map.Size;

    /// <summary>The lowest grid point inside the rim.</summary>
    public static Vector2 DeepestPoint(LevelMap map)
    {
        float best = float.MaxValue;
        Vector2 at = default;
        int margin = (int)MathF.Ceiling(LevelMap.RimWidth) + 1;
        for (int y = margin; y <= map.Size - margin; y++)
        for (int x = margin; x <= map.Size - margin; x++)
            if (map[x, y] < best)
            {
                best = map[x, y];
                at = new Vector2(x, y);
            }
        return at;
    }

    /// <summary>The highest ground inside the rim.</summary>
    public static float InteriorPeak(LevelMap map)
    {
        float best = float.MinValue;
        int margin = (int)MathF.Ceiling(LevelMap.RimWidth) + 1;
        for (int y = margin; y <= map.Size - margin; y++)
        for (int x = margin; x <= map.Size - margin; x++)
            best = MathF.Max(best, map[x, y]);
        return best;
    }

    /// <summary>The span between the arch's footings crosses the centre line of its corridor.</summary>
    public static bool ArchCrossesCorridor(LevelMap map, Canopy arch)
    {
        if (arch.Corridor < 0 || arch.Corridor >= map.Corridors.Count) return false;
        var line = map.Corridors[arch.Corridor].Points;
        for (int i = 1; i < line.Count; i++)
            if (Geo.SegmentIntersection(arch.EndA, arch.EndB, line[i - 1], line[i], out _)) return true;
        return false;
    }

    /// <summary>Stepping out of the mouth, Clementine reaches a corridor or open water within a few metres.</summary>
    public static bool CaveMouthOpens(LevelMap map, CaveSite cave)
    {
        for (float s = 0.5f; s <= 6f; s += 0.5f)
        {
            Vector2 p = cave.Mouth + cave.Facing * s;
            if (!map.IsOpen(p)) return false;
            bool inCave = cave.Chambers.Any(c => Vector2.Distance(c.Center, p) < c.Radius);
            if (!inCave && (map.InsideAnyCorridor(p) || s >= 3f)) return true;
        }
        return false;
    }

    // ───────────────────────── flood fill ─────────────────────────

    /// <summary>The cell a position falls in, on a square of <paramref name="n"/> cells a side.</summary>
    public static (int X, int Y) Cell(Vector2 p, int n = LevelMap.Seeded) => (Math.Clamp((int)p.X, 0, n - 1), Math.Clamp((int)p.Y, 0, n - 1));

    /// <summary>A field of distances' cells a side.</summary>
    static int Side(float[] field) => (int)MathF.Round(MathF.Sqrt(field.Length));

    /// <summary>Half the bell's width (~0.8 m across): the clearance a swimmable cell must have all round.</summary>
    public const float BodyRadius = 0.4f;

    /// <summary>A 1 m cell is swimmable when the bell fits at its centre: the ground under it and all round it lies below the swim band.</summary>
    public static bool CellOpen(LevelMap map, int x, int y, float clearance = BodyRadius)
    {
        var c = new Vector2(x + 0.5f, y + 0.5f);
        if (!map.IsOpen(c)) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            if (!map.IsOpen(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * clearance)) return false;
        }
        return true;
    }

    /// <summary>
    /// Swimming distance from a point to every 1 m cell (∞ where unreachable). Weak rock is not counted as a wall, unless
    /// <paramref name="blocked"/> says so.
    /// </summary>
    public static float[] Distances(LevelMap map, Vector2 from, float clearance = BodyRadius, Func<Vector2, bool>? blocked = null)
    {
        int N = map.Size;
        var open = OpenCells(map, clearance, blocked);
        var dist = new float[N * N];
        Array.Fill(dist, float.PositiveInfinity);
        var (sx, sy) = Cell(from, N);
        var queue = new PriorityQueue<int, float>();
        dist[sy * N + sx] = 0f;
        queue.Enqueue(sy * N + sx, 0f);
        while (queue.TryDequeue(out int i, out float d))
        {
            if (d > dist[i]) continue;
            int x = i % N, y = i / N;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N || !open[ny * N + nx]) continue;
                // No squeezing diagonally between two walls.
                if (dx != 0 && dy != 0 && (!open[y * N + nx] || !open[ny * N + x])) continue;
                float nd = d + (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                int j = ny * N + nx;
                if (nd < dist[j])
                {
                    dist[j] = nd;
                    queue.Enqueue(j, nd);
                }
            }
        }
        return dist;
    }

    /// <summary>Which 1 m cells are swimmable (CellOpen), as <see cref="Distances"/> sees them.</summary>
    public static bool[] OpenCells(LevelMap map, float clearance = BodyRadius, Func<Vector2, bool>? blocked = null)
    {
        int N = map.Size;
        var open = new bool[N * N];
        // (Every cell on its own: the rows at once, unless the caller's test must be asked in turn.)
        void Row(int y)
        {
            for (int x = 0; x < N; x++)
                open[y * N + x] = CellOpen(map, x, y, clearance) && blocked?.Invoke(new Vector2(x + 0.5f, y + 0.5f)) != true;
        }
        if (blocked is null) System.Threading.Tasks.Parallel.For(0, N, Row);
        else for (int y = 0; y < N; y++) Row(y);
        return open;
    }

    /// <summary>
    /// The swimming distance from one point to another exactly as <see cref="Distances"/> measures it, but searched only
    /// while it is under <paramref name="limit"/>: ∞ if it is not (unreachable, or at least that far round). The cells
    /// are <see cref="OpenCells"/>' and <paramref name="scratch"/> a field of the map's cells all ∞, left so.
    /// </summary>
    public static float DistanceWithin(bool[] open, Vector2 from, Vector2 to, float limit, float[] scratch)
    {
        int N = Side(scratch);
        var (sx, sy) = Cell(from, N);
        var (tx, ty) = Cell(to, N);
        int target = ty * N + tx;
        var touched = new List<int>();
        var queue = new PriorityQueue<int, float>();
        scratch[sy * N + sx] = 0f;
        touched.Add(sy * N + sx);
        queue.Enqueue(sy * N + sx, 0f);
        float found = float.PositiveInfinity;
        // In the order Distances settles them: a cell is final once taken off the queue, and nothing after is nearer.
        while (queue.TryDequeue(out int i, out float d))
        {
            if (d > scratch[i]) continue;
            if (d >= limit) break;
            if (i == target)
            {
                found = d;
                break;
            }
            int x = i % N, y = i / N;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N || !open[ny * N + nx]) continue;
                if (dx != 0 && dy != 0 && (!open[y * N + nx] || !open[ny * N + x])) continue;
                float nd = d + (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                int j = ny * N + nx;
                if (nd < scratch[j])
                {
                    if (float.IsPositiveInfinity(scratch[j])) touched.Add(j);
                    scratch[j] = nd;
                    queue.Enqueue(j, nd);
                }
            }
        }
        foreach (int i in touched) scratch[i] = float.PositiveInfinity;
        return found;
    }

    public static float DistanceAt(float[] dist, Vector2 p)
    {
        int N = Side(dist);
        var (x, y) = Cell(p, N);
        return dist[y * N + x];
    }

    /// <summary>A shortest swimming route as cell centres (empty if unreachable).</summary>
    public static List<Vector2> ShortestPath(LevelMap map, Vector2 from, Vector2 to, float clearance = BodyRadius) =>
        Walk(Distances(map, to, clearance), from);

    /// <summary>The route down a field of distances to where it was measured from, as cell centres (empty if unreachable).</summary>
    public static List<Vector2> Walk(float[] dist, Vector2 from)
    {
        int N = Side(dist);
        var path = new List<Vector2>();
        var (x, y) = Cell(from, N);
        if (float.IsPositiveInfinity(dist[y * N + x])) return path;
        for (int guard = 0; guard < N * N; guard++)
        {
            path.Add(new Vector2(x + 0.5f, y + 0.5f));
            float here = dist[y * N + x];
            if (here <= 0f) break;
            int bx = x, by = y;
            float best = here;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                if (dist[ny * N + nx] < best)
                {
                    best = dist[ny * N + nx];
                    bx = nx;
                    by = ny;
                }
            }
            if (bx == x && by == y) break;
            x = bx;
            y = by;
        }
        return path;
    }
}
