using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>What a point of a cube is: rock, water over a shelf, or water with nothing under it (void).</summary>
public enum CubeCell : byte { Rock, Shelf, Void }

/// <summary>
/// One cube's reef (CUBE-WORLD-PLAN §3–§4): a heightfield on the 1 m grid of its two squares plus an Apron of its
/// neighbours', each point rock, shelf or void, and the arches over its water. Made by <see cref="Make"/> from the seed
/// and its id alone.
/// </summary>
public sealed class CubeTerrain
{
    public required CubeId Cube { get; init; }
    /// <summary>Its arrays' samples across and down (its size + 1 and the apron either side).</summary>
    public required int Stride { get; init; }
    public required int Rows { get; init; }
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
    /// <summary>
    /// Its sockets (CUBE-WORLD-PLAN §9): up to two alcoves cut into its walls as a cave's opening is (TopDownGenerator's
    /// doorway), well inside it and away from its gates and wells, where a place granted to it opens its mouth.
    /// </summary>
    public IReadOnlyList<Doorway> Sockets { get; init; } = Array.Empty<Doorway>();
    /// <summary>The rock over its sockets' passages (NaN elsewhere; null with none), as a level's Roof.</summary>
    public float[]? Roof { get; init; }
    public float RoofAt(int gx, int gy) => Roof is { } r && Has(gx, gy) ? r[Index(gx, gy)] : float.NaN;

    public bool Has(int gx, int gy) => gx >= X0 && gy >= Y0 && gx < X0 + Stride && gy < Y0 + Rows;
    public int Index(int gx, int gy) => (gy - Y0) * Stride + (gx - X0);
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

    /// <summary>The shelf noise's band over which a shelf's floor drops into the void (so its edge is a slope, not a step).</summary>
    const float ShelfEdge = 0.05f;

    static float Smooth(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Points of the rock's rim pass (lower rock beside water) reach this far from the water.</summary>
    const int Lip = 1;

    // Noise fields, one seed each (every cube reads the same at the same global point).
    const int NoiseThroat = 1, NoiseChamber = 2, NoiseTunnel = 3, NoiseShelf = 4, NoiseTop = 5, NoiseFloor = 6, NoiseBays = 7;

    /// <summary>A socket's opening lies at least this far inside its cube, this far from a gate's mouth and from a well, and sockets this far apart.</summary>
    public const float SocketInside = 12f, SocketFromGate = 14f, SocketFromWell = 9f, SocketApart = 20f;

    /// <summary>
    /// Up to two sockets cut into the cube's walls (heights and cells changed in place): the classic level's doorway carved
    /// on a scratch map of the cube, from spots of shelf water deep inside it. Pure: the cube's own rng, after all else.
    /// </summary>
    static List<Doorway> CutSockets(ulong seed, CubeId c, CubeCell[] cells, float[] heights, int nw, int nh, int x0, int y0,
        List<(EdgeId, Gate, Vector2)> gates, List<Well> down, List<Well> arrivals, Rng rng, out float[]? roof)
    {
        var sockets = new List<Doorway>();
        roof = null;
        var map = new LevelMap(Math.Max(nw, nh) - 1) { Seed = seed };
        Array.Fill(map.Heights, RockLow + RockRim);
        for (int j = 0; j < nh; j++)
        for (int i = 0; i < nw; i++) map[i, j] = heights[j * nw + i];
        var off = new Vector2(x0, y0);
        var lo = c.Origin - off;
        var hi = lo + c.Size;
        bool Deep(Vector2 p, float inside) => p.X >= lo.X + inside && p.Y >= lo.Y + inside && p.X <= hi.X - inside && p.Y <= hi.Y - inside;
        bool Clear(Vector2 local)
        {
            var g = local + off;
            foreach (var (_, _, mouth) in gates) if (Vector2.Distance(g, mouth) < SocketFromGate) return false;
            foreach (var w in down) if (Vector2.Distance(g, w.Center) < SocketFromWell + w.Radius) return false;
            foreach (var w in arrivals) if (Vector2.Distance(g, w.Center) < SocketFromWell + w.Radius) return false;
            foreach (var s in sockets) if (Vector2.Distance(g, s.At) < SocketApart) return false;
            return true;
        }
        for (int tries = 0; tries < 60 && sockets.Count < 2; tries++)
        {
            var from = new Vector2(rng.Range(lo.X + SocketInside + 6f, hi.X - SocketInside - 6f), rng.Range(lo.Y + SocketInside + 6f, hi.Y - SocketInside - 6f));
            int fi = (int)MathF.Round(from.X), fj = (int)MathF.Round(from.Y);
            if (cells[fj * nw + fi] != CubeCell.Shelf || !map.IsOpen(from) || !Clear(from)) continue;
            float a = rng.Range(0f, MathF.Tau);
            if (TopDownGenerator.FindDoorway(map, from, 10f, new Vector2(MathF.Cos(a), MathF.Sin(a)), -1.01f) is not { } found) continue;
            if (!Deep(found.Door.At, SocketInside) || !Deep(found.Door.Back, SocketInside) || !Clear(found.Door.At)) continue;
            if (TopDownGenerator.CutDoorway(map, from, 10f, new Vector2(MathF.Cos(a), MathF.Sin(a)), -1.01f) is not { } door) continue;
            sockets.Add(new Doorway(door.At + off, door.Into));
        }
        if (sockets.Count == 0) return sockets;
        roof = new float[nw * nh];
        for (int j = 0; j < nh; j++)
        for (int i = 0; i < nw; i++)
        {
            int k = j * nw + i;
            float now = map[i, j];
            roof[k] = map.RoofAt(i, j);
            if (now == heights[k]) continue;
            heights[k] = now;
            if (now <= 0f && cells[k] == CubeCell.Rock) cells[k] = CubeCell.Shelf;
        }
        return sockets;
    }

    public static CubeTerrain Make(CubeSpace space, CubeId c)
    {
        // Its size (metres), and the grid it is worked on: its own samples, the apron and the rim pass's margin round them.
        int w = c.Wide * Side, h = c.Tall * Side;
        int m = Apron + Lip, gw = w + 1 + 2 * m, gh = h + 1 + 2 * m;
        int gx0 = c.X * Side - m, gy0 = c.Y * Side - m;
        var origin = c.Origin;
        var outer = c.Outer();
        var edges = Array.ConvertAll(outer, o => space.Edge(o.Edge));
        // Into the cube from each edge: it lies on the edge's high side or its low side.
        var inward = Array.ConvertAll(outer, o => o.Edge.High == o.Inside ? 1f : -1f);
        var corners = Array.ConvertAll(c.Corners(), space.Corner);
        var down = new List<Well>(space.Floor(c.First.Under).Wells);
        down.AddRange(space.Floor(c.Second.Under).Wells);
        var arrivals = new List<Well>(space.Floor(c.First.Over).Wells);
        arrivals.AddRange(space.Floor(c.Second.Over).Wells);
        var rng = ReefHash.Rng(space.Seed, ReefHash.Cube, c.X, c.Y, c.Z, c.OnEnd ? 1 : 0);
        uint throatSeed = space.NoiseSeed(NoiseThroat), chamberSeed = space.NoiseSeed(NoiseChamber), tunnelSeed = space.NoiseSeed(NoiseTunnel);
        uint shelfSeed = space.NoiseSeed(NoiseShelf), topSeed = space.NoiseSeed(NoiseTop), floorSeed = space.NoiseSeed(NoiseFloor);

        // 1. The plan inside it: its gates' mouths, its wells, its chambers (local metres).
        var gates = new List<(EdgeId, Gate, Vector2)>();
        var nodes = new List<Vector2>();
        var wellNode = new List<bool>();
        var chambers = new List<(Vector2 At, float Radius, float Least)>();
        for (int e = 0; e < edges.Length; e++)
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
        foreach (var well in Concat(down, arrivals))
        {
            nodes.Add(well.Center - origin);
            wellNode.Add(true);
            chambers.Add((well.Center - origin, well.Radius + 4f, well.Radius + 1.5f));
        }
        // Pockets hugging its edges (so a closed edge is often a thin fin of rock with water both sides, not a band).
        for (int e = 0; e < edges.Length; e++)
        {
            float along = rng.Range(16f, Side - 16f), inset = rng.Range(9f, 12f), radius = rng.Range(5f, 8f);
            if (rng.NextFloat() < 0.4f) continue;
            var at = edges[e].Id.Point(along, inward[e] * inset) - origin;
            if (nodes.Exists(n => Vector2.Distance(n, at) < 11f)) continue;
            nodes.Add(at);
            wellNode.Add(false);
            chambers.Add((at, radius, 0f));
        }
        int extra = 2 + rng.Int(4);
        for (int k = 0; k < extra; k++)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                var at = new Vector2(rng.Range(13f, w - 13f), rng.Range(13f, h - 13f));
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
            tunnels.Add(Tunnel(nodes[ba], nodes[bb], rng, wellNode[ba] || wellNode[bb], w, h));
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
            tunnels.Add(Tunnel(nodes[ba], nodes[bb], rng, wellNode[ba] || wellNode[bb], w, h));
        }
        // Arches over a tunnel or two (none by a well: nothing hides a way down or a way in).
        var arches = new List<(Vector2[] Points, float Half)>();
        foreach (var t in tunnels)
        {
            if (t.Wells || arches.Count >= 2 || Vector2.Distance(t.Points[0], t.Points[^1]) < 14f) continue;
            if (rng.NextFloat() < 0.45f) arches.Add((t.Points[4..8], t.Half + 1.2f));
        }

        // 3. Carved into its own square: water where a chamber or tunnel is (their rims wobbling by global noise).
        int ow = w + 1, oh = h + 1;
        var water = new bool[ow * oh];
        var arch = new bool[ow * oh];
        foreach (var (at, radius, least) in chambers)
            Stamp(at, radius * 1.45f, (x, y) =>
            {
                float wob = 1f + 0.3f * TerrainNoise.Fbm((origin.X + x) * 0.07f, (origin.Y + y) * 0.07f, chamberSeed, 2);
                return Vector2.Distance(new Vector2(x, y), at) < MathF.Max(least, radius * wob);
            }, water, ow, oh);
        foreach (var t in tunnels)
            StampLine(t.Points, t.Half + 1.5f, (x, y, d) =>
                d < t.Half + 1.1f * TerrainNoise.Fbm((origin.X + x) * 0.1f, (origin.Y + y) * 0.1f, tunnelSeed, 2), water, ow, oh);
        Bays(water, origin, space.NoiseSeed(NoiseBays), ow, oh);
        foreach (var a in arches) StampLine(a.Points, a.Half, (x, y, d) => d < a.Half, arch, ow, oh);

        // 4. Every point: what the edges and corners decide (shared with the neighbours), else what was carved.
        var cell = new CubeCell[gw * gh];
        var shelf = new float[gw * gh];
        var top = new float[gw * gh];
        var canopy = new bool[gw * gh];
        for (int j = 0; j < gh; j++)
        for (int i = 0; i < gw; i++)
        {
            int u = i - m, v = j - m, k = j * gw + i;
            var p = new Vector2(gx0 + i, gy0 + j);
            top[k] = RockLow + RockRise * Math.Clamp(TerrainNoise.Fbm(p.X * 0.06f, p.Y * 0.06f, topSeed, 3) * 0.5f + 0.5f, 0f, 1f);
            var shared = Shared(p, edges, corners, throatSeed);
            bool ownPoint = u >= 0 && v >= 0 && u <= w && v <= h;
            bool wet = shared ?? (ownPoint && water[v * ow + u]);
            if (!wet)
            {
                cell[k] = CubeCell.Rock;
                continue;
            }
            var local = new Vector2(u, v);
            bool byWell = false, inWell = false;
            // How far a point is from being a well's void (0 inside it, 1 a little way out): its floor sinks smoothly into it.
            float rise = 1f;
            if (ownPoint && shared is null)
                foreach (var well in down)
                {
                    float reach = well.Radius * (1f + 0.3f * MathF.Max(0f, TerrainNoise.Perlin(p.X * 0.15f, p.Y * 0.15f, floorSeed + 9u)));
                    float d = Vector2.Distance(local, well.Center - origin);
                    if (d < reach) inWell = true;
                    rise = MathF.Min(rise, Smooth(reach, reach + 2.5f, d));
                }
            foreach (var well in Concat(down, arrivals))
                if (Vector2.Distance(local, well.Center - origin) < well.Radius + 2f) byWell = true;
            canopy[k] = ownPoint && shared is null && arch[v * ow + u] && !byWell;
            float shelfNoise = TerrainNoise.Fbm(p.X * 0.035f, p.Y * 0.035f, shelfSeed, 2);
            if (inWell) cell[k] = CubeCell.Void;
            else if (canopy[k]) cell[k] = CubeCell.Shelf;
            else cell[k] = shelfNoise > ShelfCut ? CubeCell.Shelf : CubeCell.Void;
            // The floor's height follows the shelf noise's contour smoothly (no 1 m steps along a shelf's edge), and sinks into
            // a well; under an arch it is a shelf.
            shelf[k] = inWell ? 0f : canopy[k] ? rise : MathF.Min(rise, Smooth(ShelfCut - ShelfEdge, ShelfCut + ShelfEdge, shelfNoise));
        }

        // 5. Heights, cropped to the apron: the rock lower at its rim, a shelf's floor a little uneven, the void deep.
        int nw = w + 1 + 2 * Apron, nh = h + 1 + 2 * Apron;
        var outCells = new CubeCell[nw * nh];
        var outHeights = new float[nw * nh];
        var outCanopy = new bool[nw * nh];
        for (int j = 0; j < nh; j++)
        for (int i = 0; i < nw; i++)
        {
            int si = i + Lip, sj = j + Lip, k = sj * gw + si, o = j * nw + i;
            var kind = cell[k];
            outCells[o] = kind;
            outCanopy[o] = canopy[k];
            float gx = gx0 + si, gy = gy0 + sj;
            outHeights[o] = kind switch
            {
                CubeCell.Void or CubeCell.Shelf => VoidHeight + (ShelfHeight + 0.3f * TerrainNoise.Perlin(gx * 0.18f, gy * 0.18f, floorSeed) - VoidHeight) * shelf[k],
                _ => Rim(cell, top, gw, gh, si, sj),
            };
        }

        // 6. Its sockets, cut into its walls.
        var sockets = CutSockets(space.Seed, c, outCells, outHeights, nw, nh, c.X * Side - Apron, c.Y * Side - Apron, gates, down, arrivals, rng, out var roof);

        return new CubeTerrain
        {
            Cube = c,
            Sockets = sockets,
            Roof = roof,
            Stride = nw,
            Rows = nh,
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
    static void Bays(bool[] water, Vector2 origin, uint seed, int ow, int oh)
    {
        const int BayReach = 5;
        var near = new int[ow * oh];
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
            int x = k % ow, y = k / ow;
            foreach (var (dx, dy) in Steps)
            {
                int a = x + dx, b = y + dy;
                if (a < 0 || b < 0 || a >= ow || b >= oh || near[b * ow + a] <= near[k] + 1) continue;
                near[b * ow + a] = near[k] + 1;
                todo.Enqueue(b * ow + a);
            }
        }
        for (int k = 0; k < near.Length; k++)
        {
            if (near[k] == 0 || near[k] > BayReach) continue;
            float n = TerrainNoise.Fbm((origin.X + k % ow) * 0.09f, (origin.Y + k / ow) * 0.09f, seed, 2);
            if (n > 0.08f + 0.07f * near[k]) water[k] = true;
        }
    }

    static readonly (int, int)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>A rock point's height: its own, lowered where water is within Lip of it.</summary>
    static float Rim(CubeCell[] cell, float[] top, int gw, int gh, int i, int j)
    {
        int near = Lip + 1;
        for (int y = j - Lip; y <= j + Lip; y++)
        for (int x = i - Lip; x <= i + Lip; x++)
        {
            if (x < 0 || y < 0 || x >= gw || y >= gh || cell[y * gw + x] == CubeCell.Rock) continue;
            near = Math.Min(near, Math.Max(Math.Abs(x - i), Math.Abs(y - j)));
        }
        float h = top[j * gw + i];
        return near == 1 ? MathF.Min(h, RockRim) : h;
    }

    /// <summary>A tunnel from a to b: a curve bowed to one side, kept inside the cube's middle, 11 points.</summary>
    static (Vector2[] Points, float Half, bool Wells) Tunnel(Vector2 a, Vector2 b, Rng rng, bool wells, int w, int h)
    {
        var along = b - a;
        float len = along.Length();
        var side = len > 0f ? new Vector2(-along.Y, along.X) / len : Vector2.Zero;
        var bend = (a + b) * 0.5f + side * len * rng.Range(-0.3f, 0.3f);
        bend = Vector2.Clamp(bend, new Vector2(12f), new Vector2(w - 12f, h - 12f));
        float half = rng.Range(2.8f, 4.4f);
        var points = new Vector2[11];
        for (int k = 0; k <= 10; k++)
        {
            float t = k / 10f;
            points[k] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * bend + t * t * b;
        }
        return (points, half, wells);
    }

    static void Stamp(Vector2 at, float reach, Func<int, int, bool> inside, bool[] into, int ow, int oh)
    {
        int x0 = Math.Max(0, (int)(at.X - reach)), x1 = Math.Min(ow - 1, (int)MathF.Ceiling(at.X + reach));
        int y0 = Math.Max(0, (int)(at.Y - reach)), y1 = Math.Min(oh - 1, (int)MathF.Ceiling(at.Y + reach));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (!into[y * ow + x] && inside(x, y)) into[y * ow + x] = true;
    }

    static void StampLine(Vector2[] points, float reach, Func<int, int, float, bool> inside, bool[] into, int ow, int oh)
    {
        var lo = points[0];
        var hi = points[0];
        foreach (var q in points)
        {
            lo = Vector2.Min(lo, q);
            hi = Vector2.Max(hi, q);
        }
        int x0 = Math.Max(0, (int)(lo.X - reach)), x1 = Math.Min(ow - 1, (int)MathF.Ceiling(hi.X + reach));
        int y0 = Math.Max(0, (int)(lo.Y - reach)), y1 = Math.Min(oh - 1, (int)MathF.Ceiling(hi.Y + reach));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (into[y * ow + x]) continue;
            var q = new Vector2(x, y);
            float d = float.MaxValue;
            for (int k = 0; k + 1 < points.Length; k++) d = MathF.Min(d, SegmentDistance(q, points[k], points[k + 1]));
            if (d <= reach && inside(x, y, d)) into[y * ow + x] = true;
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
