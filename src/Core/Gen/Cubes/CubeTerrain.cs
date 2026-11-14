using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>What a point of a cube is: rock, water over a shelf, or water with nothing under it (void).</summary>
public enum CubeCell : byte { Rock, Shelf, Void }

/// <summary>
/// One cube's reef (CUBE-WORLD-PLAN §3–§4): a heightfield on the 1 m grid of its own square plus an Apron of its
/// neighbours', each point rock, shelf or void, and the arches over its water. Made by <see cref="Make"/> from the seed
/// and its coordinates alone.
/// </summary>
public sealed class CubeTerrain
{
    /// <summary>Its arrays' samples a side (its square's Side + 1 and the apron either side).</summary>
    public const int N = Side + 1 + 2 * Apron;

    public required CubeCoord Coord { get; init; }
    /// <summary>The global grid point of its arrays' first sample.</summary>
    public required int X0 { get; init; }
    public required int Y0 { get; init; }
    public required CubeCell[] Cells { get; init; }
    public required float[] Heights { get; init; }
    public required bool[] Canopy { get; init; }
    /// <summary>Its gates (edge, gate, and where its throat meets the cube's own water, global).</summary>
    public required IReadOnlyList<(EdgeId Edge, Gate Gate, Vector2 Mouth)> Gates { get; init; }
    /// <summary>The wells down out of it (its floor's), and those into it from above (the floor over it).</summary>
    public required IReadOnlyList<Well> Down { get; init; }
    public required IReadOnlyList<Well> Arrivals { get; init; }
    /// <summary>The tunnels carved through it (global points), for views and tests.</summary>
    public required IReadOnlyList<Vector2[]> Tunnels { get; init; }

    public bool Has(int gx, int gy) => gx >= X0 && gy >= Y0 && gx < X0 + N && gy < Y0 + N;
    public int Index(int gx, int gy) => (gy - Y0) * N + (gx - X0);
    public CubeCell CellAt(int gx, int gy) => Has(gx, gy) ? Cells[Index(gx, gy)] : CubeCell.Rock;
    public float HeightAt(int gx, int gy) => Has(gx, gy) ? Heights[Index(gx, gy)] : 0f;
    public bool CanopyAt(int gx, int gy) => Has(gx, gy) && Canopy[Index(gx, gy)];

    /// <summary>
    /// Whether she may dive at <paramref name="p"/> (CUBE-WORLD-PLAN §3): within <paramref name="radius"/> of it the
    /// upper cube is all void and the lower all open water with no arch over it. Climbing reads the same, from below.
    /// </summary>
    public static bool CanDive(CubeTerrain upper, CubeTerrain lower, Vector2 p, float radius)
    {
        int r = (int)MathF.Ceiling(radius);
        int cx = (int)MathF.Round(p.X), cy = (int)MathF.Round(p.Y);
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            if ((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y) > radius * radius) continue;
            if (upper.CellAt(x, y) != CubeCell.Void) return false;
            if (lower.CellAt(x, y) == CubeCell.Rock || lower.CanopyAt(x, y)) return false;
        }
        return true;
    }

    /// <summary>Points of the rock's rim pass (lower rock beside water) reach this far from the water.</summary>
    const int Lip = 1;

    // Noise fields, one seed each (every cube reads the same at the same global point).
    const int NoiseThroat = 1, NoiseChamber = 2, NoiseTunnel = 3, NoiseShelf = 4, NoiseTop = 5, NoiseFloor = 6, NoiseBays = 7;

    public static CubeTerrain Make(CubeSpace space, CubeCoord c)
    {
        int m = Apron + Lip, g = Side + 1 + 2 * m;
        int gx0 = c.X * Side - m, gy0 = c.Y * Side - m;
        var origin = c.Origin;
        var edges = new[] { space.Edge(c.West), space.Edge(c.East), space.Edge(c.North), space.Edge(c.South) };
        // Into the cube from each edge: west and north edges have it as their high side.
        var inward = new[] { 1f, -1f, 1f, -1f };
        var corners = new[] { space.Corner(c.Corner(0, 0)), space.Corner(c.Corner(1, 0)), space.Corner(c.Corner(0, 1)), space.Corner(c.Corner(1, 1)) };
        var down = space.Floor(c.Under).Wells;
        var arrivals = space.Floor(c.Over).Wells;
        var rng = ReefHash.Rng(space.Seed, ReefHash.Cube, c.X, c.Y, c.Z);
        uint throatSeed = space.NoiseSeed(NoiseThroat), chamberSeed = space.NoiseSeed(NoiseChamber), tunnelSeed = space.NoiseSeed(NoiseTunnel);
        uint shelfSeed = space.NoiseSeed(NoiseShelf), topSeed = space.NoiseSeed(NoiseTop), floorSeed = space.NoiseSeed(NoiseFloor);

        // 1. The plan inside it: its gates' mouths, its wells, its chambers (local metres).
        var gates = new List<(EdgeId, Gate, Vector2)>();
        var nodes = new List<Vector2>();
        var wellNode = new List<bool>();
        var chambers = new List<(Vector2 At, float Radius, float Least)>();
        for (int e = 0; e < 4; e++)
        {
            if (!edges[e].Open) continue;
            foreach (var gate in edges[e].Gates)
            {
                var mouth = edges[e].Id.Point(gate.Along, inward[e] * ThroatReach);
                gates.Add((edges[e].Id, gate, mouth));
                nodes.Add(mouth - origin);
                wellNode.Add(false);
            }
        }
        foreach (var w in Concat(down, arrivals))
        {
            nodes.Add(w.Center - origin);
            wellNode.Add(true);
            chambers.Add((w.Center - origin, w.Radius + 4f, w.Radius + 1.5f));
        }
        // Pockets hugging its edges (so a closed edge is often a thin fin of rock with water both sides, not a band).
        for (int e = 0; e < 4; e++)
        {
            float along = rng.Range(16f, Side - 16f), inset = rng.Range(9f, 12f), radius = rng.Range(5f, 8f);
            if (rng.NextFloat() < 0.4f) continue;
            var at = edges[e].Id.Point(along, inward[e] * inset) - origin;
            if (nodes.Exists(n => Vector2.Distance(n, at) < 11f)) continue;
            nodes.Add(at);
            wellNode.Add(false);
            chambers.Add((at, radius, 0f));
        }
        int extra = 1 + rng.Int(3);
        for (int k = 0; k < extra; k++)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                var at = new Vector2(rng.Range(13f, Side - 13f), rng.Range(13f, Side - 13f));
                if (tries < 11 && nodes.Exists(n => Vector2.Distance(n, at) < 12f)) continue;
                nodes.Add(at);
                wellNode.Add(false);
                chambers.Add((at, rng.Range(7f, 12f), 0f));
                break;
            }
        }

        // 2. Joined: a spanning tree of the shortest ways (Prim), and a loop or two.
        var tunnels = new List<(Vector2[] Points, float Half, bool Wells)>();
        var joined = new HashSet<(int, int)>();
        var inTree = new bool[nodes.Count];
        inTree[0] = true;
        for (int added = 1; added < nodes.Count; added++)
        {
            int ba = -1, bb = -1;
            float best = float.MaxValue;
            for (int a = 0; a < nodes.Count; a++)
            {
                if (!inTree[a]) continue;
                for (int b = 0; b < nodes.Count; b++)
                {
                    if (inTree[b]) continue;
                    float d = Vector2.DistanceSquared(nodes[a], nodes[b]);
                    if (d < best)
                    {
                        best = d;
                        ba = a;
                        bb = b;
                    }
                }
            }
            inTree[bb] = true;
            joined.Add((Math.Min(ba, bb), Math.Max(ba, bb)));
            tunnels.Add(Tunnel(nodes[ba], nodes[bb], rng, wellNode[ba] || wellNode[bb]));
        }
        int loops = nodes.Count >= 4 ? 1 + (rng.NextFloat() < 0.5f ? 1 : 0) : 0;
        for (int l = 0; l < loops; l++)
        {
            int ba = -1, bb = -1;
            float best = float.MaxValue;
            for (int a = 0; a < nodes.Count; a++)
            for (int b = a + 1; b < nodes.Count; b++)
            {
                if (joined.Contains((a, b))) continue;
                float d = Vector2.Distance(nodes[a], nodes[b]) * rng.Range(1f, 1.6f);
                if (d < 16f || d >= best) continue;
                best = d;
                ba = a;
                bb = b;
            }
            if (ba < 0) break;
            joined.Add((ba, bb));
            tunnels.Add(Tunnel(nodes[ba], nodes[bb], rng, wellNode[ba] || wellNode[bb]));
        }
        // Arches over a tunnel or two (none by a well: nothing hides a way down or a way in).
        var arches = new List<(Vector2[] Points, float Half)>();
        foreach (var t in tunnels)
        {
            if (t.Wells || arches.Count >= 2 || Vector2.Distance(t.Points[0], t.Points[^1]) < 14f) continue;
            if (rng.NextFloat() < 0.45f) arches.Add((t.Points[4..8], t.Half + 1.2f));
        }

        // 3. Carved into its own square: water where a chamber or tunnel is (their rims wobbling by global noise).
        int own = Side + 1;
        var water = new bool[own * own];
        var arch = new bool[own * own];
        foreach (var (at, radius, least) in chambers)
            Stamp(at, radius * 1.45f, (x, y) =>
            {
                float wob = 1f + 0.3f * TerrainNoise.Fbm((origin.X + x) * 0.07f, (origin.Y + y) * 0.07f, chamberSeed, 2);
                return Vector2.Distance(new Vector2(x, y), at) < MathF.Max(least, radius * wob);
            }, water);
        foreach (var t in tunnels)
            StampLine(t.Points, t.Half + 1.5f, (x, y, d) =>
                d < t.Half + 1.1f * TerrainNoise.Fbm((origin.X + x) * 0.1f, (origin.Y + y) * 0.1f, tunnelSeed, 2), water);
        Bays(water, origin, space.NoiseSeed(NoiseBays));
        foreach (var a in arches) StampLine(a.Points, a.Half, (x, y, d) => d < a.Half, arch);

        // 4. Every point: what the edges and corners decide (shared with the neighbours), else what was carved.
        var cell = new CubeCell[g * g];
        var top = new float[g * g];
        var canopy = new bool[g * g];
        for (int j = 0; j < g; j++)
        for (int i = 0; i < g; i++)
        {
            int u = i - m, v = j - m, k = j * g + i;
            var p = new Vector2(gx0 + i, gy0 + j);
            top[k] = RockLow + RockRise * Math.Clamp(TerrainNoise.Fbm(p.X * 0.06f, p.Y * 0.06f, topSeed, 3) * 0.5f + 0.5f, 0f, 1f);
            var shared = Shared(p, edges, corners, throatSeed);
            bool ownPoint = u >= 0 && v >= 0 && u <= Side && v <= Side;
            bool wet = shared ?? (ownPoint && water[v * own + u]);
            if (!wet)
            {
                cell[k] = CubeCell.Rock;
                continue;
            }
            var local = new Vector2(u, v);
            bool byWell = false, inWell = false;
            if (ownPoint && shared is null)
                foreach (var w in down)
                    if (Vector2.Distance(local, w.Center - origin) < w.Radius * (1f + 0.3f * MathF.Max(0f, TerrainNoise.Perlin(p.X * 0.15f, p.Y * 0.15f, floorSeed + 9u))))
                        inWell = true;
            foreach (var w in Concat(down, arrivals))
                if (Vector2.Distance(local, w.Center - origin) < w.Radius + 2f) byWell = true;
            canopy[k] = ownPoint && shared is null && arch[v * own + u] && !byWell;
            if (inWell) cell[k] = CubeCell.Void;
            else if (canopy[k]) cell[k] = CubeCell.Shelf;
            else cell[k] = TerrainNoise.Fbm(p.X * 0.035f, p.Y * 0.035f, shelfSeed, 2) > ShelfCut ? CubeCell.Shelf : CubeCell.Void;
        }

        // 5. Heights, cropped to the apron: the rock lower at its rim, a shelf's floor a little uneven, the void deep.
        var outCells = new CubeCell[N * N];
        var outHeights = new float[N * N];
        var outCanopy = new bool[N * N];
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            int si = i + Lip, sj = j + Lip, k = sj * g + si, o = j * N + i;
            var kind = cell[k];
            outCells[o] = kind;
            outCanopy[o] = canopy[k];
            float gx = gx0 + si, gy = gy0 + sj;
            outHeights[o] = kind switch
            {
                CubeCell.Void => VoidHeight,
                CubeCell.Shelf => ShelfHeight + 0.3f * TerrainNoise.Perlin(gx * 0.18f, gy * 0.18f, floorSeed),
                _ => Rim(cell, top, g, si, sj),
            };
        }

        return new CubeTerrain
        {
            Coord = c,
            X0 = c.X * Side - Apron,
            Y0 = c.Y * Side - Apron,
            Cells = outCells,
            Heights = outHeights,
            Canopy = outCanopy,
            Gates = gates,
            Down = down,
            Arrivals = arrivals,
            Tunnels = tunnels.ConvertAll(t => Array.ConvertAll(t.Points, q => q + origin)),
        };
    }

    /// <summary>
    /// What a point's edges and corners make it, the same for every cube that holds it: a gate's throat (shelf), the
    /// edge's wall or a corner's rock; null where the cube's own carving decides.
    /// </summary>
    static bool? Shared(Vector2 p, EdgeSpec[] edges, CornerSpec[] corners, uint throatSeed)
    {
        foreach (var e in edges)
        {
            float t = e.Id.Across(p);
            if (!e.Open || MathF.Abs(t) > ThroatReach) continue;
            float along = e.Id.Along(p);
            // (Swaying from side to side as it goes through, and flaring where it opens into each cube.)
            float sway = 2.2f * TerrainNoise.Perlin(t * 0.11f, 7.3f, e.WallSeed + 5u);
            float flare = 1f + 0.5f * (t / ThroatReach) * (t / ThroatReach);
            foreach (var gate in e.Gates)
            {
                float half = gate.Width * 0.5f * flare + 0.9f * TerrainNoise.Perlin(p.X * 0.15f, p.Y * 0.15f, throatSeed);
                if (MathF.Abs(along - gate.Along - sway) < half) return true;
            }
        }
        foreach (var e in edges)
        {
            float t = e.Id.Across(p);
            if (MathF.Abs(t) > WallMax) continue;
            float s = e.Id.AlongGlobal(p);
            float shift = WallShift * Math.Clamp(TerrainNoise.Perlin(s * 0.05f, 0.5f, e.WallSeed), -1f, 1f);
            float swell = WallSwell * Math.Clamp(TerrainNoise.Perlin(s * 0.09f, 3.7f, e.WallSeed + 1u) * 0.5f + 0.5f, 0f, 1f);
            if (MathF.Abs(t - shift) < WallCore + MathF.Abs(shift) + swell) return false;
        }
        foreach (var k in corners)
            if (Vector2.Distance(p, k.Id.Point) < k.Radius) return false;
        return null;
    }

    /// <summary>
    /// Bays bitten into the rock round the carved water: rock within BayReach of it gives way where the bay noise is
    /// high, so the walls wander instead of tracing the chambers and tunnels.
    /// </summary>
    static void Bays(bool[] water, Vector2 origin, uint seed)
    {
        const int BayReach = 5;
        int own = Side + 1;
        var near = new int[own * own];
        var todo = new Queue<int>();
        for (int k = 0; k < near.Length; k++)
        {
            near[k] = water[k] ? 0 : int.MaxValue;
            if (water[k]) todo.Enqueue(k);
        }
        while (todo.Count > 0)
        {
            int k = todo.Dequeue();
            if (near[k] >= BayReach) continue;
            int x = k % own, y = k / own;
            foreach (var (dx, dy) in Steps)
            {
                int a = x + dx, b = y + dy;
                if (a < 0 || b < 0 || a >= own || b >= own || near[b * own + a] <= near[k] + 1) continue;
                near[b * own + a] = near[k] + 1;
                todo.Enqueue(b * own + a);
            }
        }
        for (int k = 0; k < near.Length; k++)
        {
            if (near[k] == 0 || near[k] > BayReach) continue;
            float n = TerrainNoise.Fbm((origin.X + k % own) * 0.09f, (origin.Y + k / own) * 0.09f, seed, 2);
            if (n > 0.08f + 0.07f * near[k]) water[k] = true;
        }
    }

    static readonly (int, int)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>A rock point's height: its own, lowered where water is within Lip of it.</summary>
    static float Rim(CubeCell[] cell, float[] top, int g, int i, int j)
    {
        int near = Lip + 1;
        for (int y = j - Lip; y <= j + Lip; y++)
        for (int x = i - Lip; x <= i + Lip; x++)
        {
            if (x < 0 || y < 0 || x >= g || y >= g || cell[y * g + x] == CubeCell.Rock) continue;
            near = Math.Min(near, Math.Max(Math.Abs(x - i), Math.Abs(y - j)));
        }
        float h = top[j * g + i];
        return near == 1 ? MathF.Min(h, RockRim) : h;
    }

    /// <summary>A tunnel from a to b: a curve bowed to one side, kept inside the cube's middle, 11 points.</summary>
    static (Vector2[] Points, float Half, bool Wells) Tunnel(Vector2 a, Vector2 b, Rng rng, bool wells)
    {
        var along = b - a;
        float len = along.Length();
        var side = len > 0f ? new Vector2(-along.Y, along.X) / len : Vector2.Zero;
        var bend = (a + b) * 0.5f + side * len * rng.Range(-0.3f, 0.3f);
        bend = Vector2.Clamp(bend, new Vector2(12f), new Vector2(Side - 12f));
        float half = rng.Range(2.8f, 4.4f);
        var points = new Vector2[11];
        for (int k = 0; k <= 10; k++)
        {
            float t = k / 10f;
            points[k] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * bend + t * t * b;
        }
        return (points, half, wells);
    }

    static void Stamp(Vector2 at, float reach, Func<int, int, bool> inside, bool[] into)
    {
        int own = Side + 1;
        int x0 = Math.Max(0, (int)(at.X - reach)), x1 = Math.Min(Side, (int)MathF.Ceiling(at.X + reach));
        int y0 = Math.Max(0, (int)(at.Y - reach)), y1 = Math.Min(Side, (int)MathF.Ceiling(at.Y + reach));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (!into[y * own + x] && inside(x, y)) into[y * own + x] = true;
    }

    static void StampLine(Vector2[] points, float reach, Func<int, int, float, bool> inside, bool[] into)
    {
        int own = Side + 1;
        var lo = points[0];
        var hi = points[0];
        foreach (var q in points)
        {
            lo = Vector2.Min(lo, q);
            hi = Vector2.Max(hi, q);
        }
        int x0 = Math.Max(0, (int)(lo.X - reach)), x1 = Math.Min(Side, (int)MathF.Ceiling(hi.X + reach));
        int y0 = Math.Max(0, (int)(lo.Y - reach)), y1 = Math.Min(Side, (int)MathF.Ceiling(hi.Y + reach));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (into[y * own + x]) continue;
            var q = new Vector2(x, y);
            float d = float.MaxValue;
            for (int k = 0; k + 1 < points.Length; k++) d = MathF.Min(d, SegmentDistance(q, points[k], points[k + 1]));
            if (d <= reach && inside(x, y, d)) into[y * own + x] = true;
        }
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float l = ab.LengthSquared();
        float t = l > 0f ? Math.Clamp(Vector2.Dot(p - a, ab) / l, 0f, 1f) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    static IEnumerable<Well> Concat(IReadOnlyList<Well> a, IReadOnlyList<Well> b)
    {
        foreach (var w in a) yield return w;
        foreach (var w in b) yield return w;
    }
}
