using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>What a point of a cube is: rock, water over a shelf, or water with nothing under it (void).</summary>
public enum CubeCell : byte { Rock, Shelf, Void }

/// <summary>
/// A cube's character (CUBE-WORLD-PLAN §4.2, kinds): the reef's caverns as they come (Plain); a great open hall carved
/// in its middle (Hall); its open water stood with rock spires (Spires); its shelves falling away into the void far more
/// than elsewhere (Drop). Only its own carving changes: its edges stay as its neighbours have them.
/// </summary>
public enum CubeKind : byte { Plain, Hall, Spires, Drop }

/// <summary>
/// One cube's reef (CUBE-WORLD-PLAN §3–§4): a heightfield on the 1 m grid of its two squares plus an Apron of its
/// neighbours', each point rock, shelf or void, and the roofs over its water. Made by <see cref="Make"/> from the seed
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
    /// <summary>
    /// Where rock roofs its water over (CUBE-WORLD-PLAN §4.4): the throats of its roofed gates through their walls and
    /// its tunnels bored through rock, and the rock round them (so their roof runs on into the walls).
    /// </summary>
    public required bool[] Canopy { get; init; }
    /// <summary>How open the layer above is over each sample (0 rock, 1 its caverns' water): her walls rise on up only under rock.</summary>
    public float[]? UpperOpen { get; init; }
    public float UpperOpenAt(int gx, int gy) => UpperOpen is { } o && Has(gx, gy) ? o[Index(gx, gy)] : 0f;
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
    /// <summary>The layer's boss arena (its basin in its middle).</summary>
    public bool Arena { get; init; }
    /// <summary>Its character (Plain, Hall, Spires, Drop).</summary>
    public CubeKind Kind { get; init; }
    /// <summary>Its spires, if it has them (global xy, radius z).</summary>
    public IReadOnlyList<Vector3> Spires { get; init; } = Array.Empty<Vector3>();
    /// <summary>The rock over its sockets' passages (NaN elsewhere; null with none), as a level's Roof.</summary>
    public float[]? Roof { get; init; }
    public float RoofAt(int gx, int gy) => Roof is { } r && Has(gx, gy) ? r[Index(gx, gy)] : float.NaN;
    /// <summary>
    /// The heights its sockets' cutting changed, as they stood before (NaN elsewhere; null with none): a secret's cracked
    /// seal draws its socket as the rock it was (CubeShapes.Plug).
    /// </summary>
    public float[]? Uncut { get; init; }
    public float UncutAt(int gx, int gy) => Uncut is { } u && Has(gx, gy) ? u[Index(gx, gy)] : float.NaN;

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
    const float ShelfEdge = 0.14f;
    /// <summary>How much more of the water over the layer below's caverns is void than the shelf noise alone would make.</summary>
    const float VoidMore = 0.12f;

    static float Smooth(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Points of the rock's rim pass (lower rock beside water) reach this far from the water.</summary>
    const int Lip = 1;

    // Noise fields, one seed each (every cube reads the same at the same global point).
    const int NoiseThroat = 1, NoiseChamber = 2, NoiseTunnel = 3, NoiseShelf = 4, NoiseTop = 5, NoiseFloor = 6, NoiseBays = 7, NoiseArch = 8;
    /// <summary>The stacked cavern field (CubeSpace.Strata).</summary>
    const int NoiseCavern = 8;

    /// <summary>A socket's opening lies at least this far inside its cube, this far from a gate's mouth and from a well, and sockets this far apart.</summary>
    public const float SocketInside = 12f, SocketFromGate = 14f, SocketFromWell = 9f, SocketApart = 20f;

    /// <summary>
    /// Up to two sockets cut into the cube's walls (heights and cells changed in place): the classic level's doorway carved
    /// on a scratch map of the cube, from spots of shelf water deep inside it. Pure: the cube's own rng, after all else.
    /// </summary>
    static List<Doorway> CutSockets(ulong seed, CubeId c, CubeCell[] cells, float[] heights, int nw, int nh, int x0, int y0,
        List<(EdgeId, Gate, Vector2)> gates, List<Well> down, List<Well> arrivals, bool[] roofed, Rng rng, out float[]? roof, out float[]? uncut)
    {
        var sockets = new List<Doorway>();
        roof = null;
        uncut = null;
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
        // (Tried from spots of open shelf water deep inside it, not under a roof: every other metre of it.)
        var spots = new List<Vector2>();
        for (int fj = (int)MathF.Ceiling(lo.Y + SocketInside + 6f); fj <= hi.Y - SocketInside - 6f; fj += 2)
        for (int fi = (int)MathF.Ceiling(lo.X + SocketInside + 6f); fi <= hi.X - SocketInside - 6f; fi += 2)
            if (cells[fj * nw + fi] == CubeCell.Shelf && !roofed[fj * nw + fi]) spots.Add(new Vector2(fi, fj));
        for (int tries = 0; tries < 100 && sockets.Count < 2 && spots.Count > 0; tries++)
        {
            var from = spots[rng.Int(spots.Count)];
            if (!map.IsOpen(from) || !Clear(from)) continue;
            float a = rng.Range(0f, MathF.Tau);
            if (TopDownGenerator.FindDoorway(map, from, 10f, new Vector2(MathF.Cos(a), MathF.Sin(a)), -1.01f) is not { } found) continue;
            if (!Deep(found.Door.At, SocketInside) || !Deep(found.Door.Back, SocketInside) || !Clear(found.Door.At)) continue;
            if (TopDownGenerator.CutDoorway(map, from, 10f, new Vector2(MathF.Cos(a), MathF.Sin(a)), -1.01f) is not { } door) continue;
            sockets.Add(new Doorway(door.At + off, door.Into));
        }
        if (sockets.Count == 0) return sockets;
        roof = new float[nw * nh];
        uncut = new float[nw * nh];
        Array.Fill(uncut, float.NaN);
        for (int j = 0; j < nh; j++)
        for (int i = 0; i < nw; i++)
        {
            int k = j * nw + i;
            float now = map[i, j];
            roof[k] = map.RoofAt(i, j);
            if (now == heights[k]) continue;
            uncut[k] = heights[k];
            heights[k] = now;
            if (now <= 0f && cells[k] == CubeCell.Rock) cells[k] = CubeCell.Shelf;
        }
        return sockets;
    }

    /// <summary>A boss arena's basin (CUBE-WORLD-PLAN §9.1): its radius, and the width of the way cut to it from each gate.</summary>
    public const float ArenaRadius = 24f, ArenaWay = 5.6f;

    /// <summary>How likely each kind is (the rest Plain).</summary>
    public const float HallShare = 0.2f, SpiresShare = 0.2f, DropShare = 0.2f;
    /// <summary>A hall's radius; spires' radii, the water kept round each and their lattice; how much more of a drop's water is void.</summary>
    public const float HallMin = 13f, HallMax = 18f, SpireMin = 1.3f, SpireMax = 2.3f, SpireClear = 3.5f, SpireStep = 6f, DropMore = 0.3f;

    /// <summary>A cube's kind, from the seed and the cube alone (a boss's arena is always Plain).</summary>
    public static CubeKind KindOf(ulong seed, CubeId c, bool arena = false)
    {
        if (arena) return CubeKind.Plain;
        float r = (ReefHash.Of(seed, ReefHash.Cube, c.X, c.Y, c.Z, 0x4B1D + (c.OnEnd ? 1 : 0)) >> 40) / (float)(1UL << 24);
        return r < HallShare ? CubeKind.Hall
            : r < HallShare + SpiresShare ? CubeKind.Spires
            : r < HallShare + SpiresShare + DropShare ? CubeKind.Drop
            : CubeKind.Plain;
    }

    /// <summary>
    /// A hall: a great wobbling basin of open water in the cube's middle (local metres), joined to its nearest water by
    /// a wide way, so it is never cut off.
    /// </summary>
    static void CarveHall(bool[] water, int ow, int oh, Vector2 size, Vector2 origin, Rng rng, uint seed)
    {
        float r = rng.Range(HallMin, HallMax);
        var at = size * 0.5f + new Vector2(rng.Range(-1f, 1f) * MathF.Max(0f, size.X * 0.5f - r - 16f), rng.Range(-1f, 1f) * MathF.Max(0f, size.Y * 0.5f - r - 16f));
        // (The nearest water outside it, before it is carved, and a way to it.)
        var best = at;
        float bestD = float.MaxValue;
        for (int v = 0; v < oh; v += 2)
        for (int u = 0; u < ow; u += 2)
        {
            if (!water[v * ow + u]) continue;
            float d = Vector2.Distance(new Vector2(u, v), at);
            if (d > r * 0.8f && d < bestD)
            {
                bestD = d;
                best = new Vector2(u, v);
            }
        }
        Stamp(at, r * 1.35f, (x, y) =>
        {
            float wob = 1f + 0.22f * TerrainNoise.Fbm((origin.X + x) * 0.08f, (origin.Y + y) * 0.08f, seed + 17u, 2);
            return Vector2.Distance(new Vector2(x, y), at) < r * wob;
        }, water, ow, oh);
        if (bestD < float.MaxValue) StampLine(new[] { at, best }, 6f, (x, y, d) => d < 2.6f, water, ow, oh);
    }

    /// <summary>
    /// Spires: rock pillars stood in the cube's wider open water on a jittered lattice, each with SpireClear of water all
    /// round it (so no way is ever shut), far from its gates' mouths and wells (local metres). Returns them (local xy, radius z).
    /// </summary>
    static List<Vector3> StandSpires(bool[] water, int ow, int oh, List<Vector2> nodes, Rng rng)
    {
        var spires = new List<Vector3>();
        var toDry = Chamfer(water, ow, oh, false);
        for (float y = SpireStep; y < oh - SpireStep; y += SpireStep)
        for (float x = SpireStep; x < ow - SpireStep; x += SpireStep)
        {
            var p = new Vector2(x + rng.Range(-0.4f, 0.4f) * SpireStep, y + rng.Range(-0.4f, 0.4f) * SpireStep);
            float r = rng.Range(SpireMin, SpireMax);
            // (Not every spot: a forest, not a grid.)
            bool skip = rng.NextFloat() < 0.35f;
            int u = (int)MathF.Round(p.X), v = (int)MathF.Round(p.Y);
            if (skip || u < 0 || v < 0 || u >= ow || v >= oh || toDry[v * ow + u] < r + SpireClear + 2.5f) continue;
            if (p.X < 14f || p.Y < 14f || p.X > ow - 15f || p.Y > oh - 15f) continue;
            bool clear = true;
            foreach (var n in nodes) if (Vector2.Distance(n, p) < r + 9f) clear = false;
            foreach (var s in spires) if (Vector2.Distance(new Vector2(s.X, s.Y), p) < r + s.Z + SpireClear + 1f) clear = false;
            if (clear) spires.Add(new Vector3(p.X, p.Y, r));
        }
        return spires;
    }

    /// <summary>A disc of the grid set false.</summary>
    static void Clear(bool[] into, int ow, int oh, Vector2 at, float radius)
    {
        int x0 = Math.Max(0, (int)(at.X - radius - 1f)), x1 = Math.Min(ow - 1, (int)MathF.Ceiling(at.X + radius + 1f));
        int y0 = Math.Max(0, (int)(at.Y - radius - 1f)), y1 = Math.Min(oh - 1, (int)MathF.Ceiling(at.Y + radius + 1f));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (Vector2.Distance(new Vector2(x, y), at) < radius) into[y * ow + x] = false;
    }

    /// <param name="arena">The layer's boss arena: a basin opened in its middle, a way cut to it from every gate, no sockets.</param>
    public static CubeTerrain Make(CubeSpace space, CubeId c, bool arena = false)
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
        uint shelfSeed = space.NoiseSeed(NoiseShelf), topSeed = space.NoiseSeed(NoiseTop), floorSeed = space.NoiseSeed(NoiseFloor), archSeed = space.NoiseSeed(NoiseArch);

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
        // (Its open water is the reef's caverns (step 3); the plan only joins its gates and wells through them.)

        // The stacked fields on the worked grid: the caverns of its own layer, the one below and the one above.
        var here = StrataGrid(space, c.Z, gx0, gy0, gw, gh);
        var under = c.Z + 1 >= space.Layers ? Filled(gw * gh, 1f) : StrataGrid(space, c.Z + 1, gx0, gy0, gw, gh, 4);
        var over = c.Z == 0 ? Filled(gw * gh, -1f) : StrataGrid(space, c.Z - 1, gx0, gy0, gw, gh, 4);

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
        // Which of them run under a roof where they bore through rock (CUBE-WORLD-PLAN §4.4).
        var roofedTunnel = tunnels.ConvertAll(_ => rng.NextFloat() < RoofedTunnels);

        // 3. Carved into its own square: water in the reef's caverns, its wells' chambers and its tunnels (their rims
        // wobbling by global noise); a tunnel bored through rock under a roof.
        int ow = w + 1, oh = h + 1;
        var water = new bool[ow * oh];
        var bore = new bool[ow * oh];
        for (int v = 0; v < oh; v++)
        for (int u = 0; u < ow; u++)
            if (here[(v + m) * gw + u + m] > CavernCut) water[v * ow + u] = true;
        foreach (var (at, radius, least) in chambers)
            Stamp(at, radius * 1.45f, (x, y) =>
            {
                float wob = 1f + 0.3f * TerrainNoise.Fbm((origin.X + x) * 0.07f, (origin.Y + y) * 0.07f, chamberSeed, 2);
                return Vector2.Distance(new Vector2(x, y), at) < MathF.Max(least, radius * wob);
            }, water, ow, oh);
        for (int n = 0; n < tunnels.Count; n++)
        {
            var t = tunnels[n];
            StampLine(t.Points, t.Half + 1.5f, (x, y, d) =>
                d < t.Half + 1.1f * TerrainNoise.Fbm((origin.X + x) * 0.1f, (origin.Y + y) * 0.1f, tunnelSeed, 2), roofedTunnel[n] && !t.Wells ? bore : water, ow, oh);
        }
        // Caverns not joined to its gates and wells: the small filled, the rest bored through to the nearest that is.
        var blocked = new bool[ow * oh];
        foreach (var e in edges)
        {
            bool isX = e.Id.Axis == EdgeAxis.X;
            float line = e.Id.Line - (isX ? origin.X : origin.Y);
            int lo = Math.Max(0, (int)MathF.Ceiling(line - WallMax - 1f)), hi = Math.Min((isX ? ow : oh) - 1, (int)MathF.Floor(line + WallMax + 1f));
            for (int a = lo; a <= hi; a++)
            for (int b = 0; b < (isX ? oh : ow); b++) blocked[isX ? b * ow + a : a * ow + b] = true;
        }
        var roofOwn = new bool[ow * oh];
        bool Roofable(int u, int v) => here[(v + m) * gw + u + m] < CavernCut - 0.06f && over[(v + m) * gw + u + m] < CavernCut;
        void Merge()
        {
            for (int k = 0; k < bore.Length; k++)
            {
                if (!bore[k]) continue;
                bore[k] = false;
                if (water[k]) continue;
                water[k] = true;
                if (Roofable(k % ow, k / ow)) roofOwn[k] = true;
            }
        }
        Merge();
        Join(water, bore, blocked, nodes, ow, oh, rng, tunnelSeed, origin);
        Merge();
        Bays(water, roofOwn, origin, space.NoiseSeed(NoiseBays), ow, oh);
        // Its kind: a hall carved, or spires stood (from a rng of its own, so the rest of its plan is as it was).
        var cubeKind = KindOf(space.Seed, c, arena);
        var kindRng = ReefHash.Rng(space.Seed, ReefHash.Cube, c.X, c.Y, c.Z, 0x4B1E + (c.OnEnd ? 1 : 0));
        if (cubeKind == CubeKind.Hall) CarveHall(water, ow, oh, c.Size, origin, kindRng, chamberSeed);
        var spires = cubeKind == CubeKind.Spires ? StandSpires(water, ow, oh, nodes, kindRng) : new List<Vector3>();
        // (A drop falls away across one of its two squares, the far one or the near, its other kept as it was.)
        bool dropFar = kindRng.NextFloat() < 0.5f;
        bool across = c.Wide == 2;
        // (Over a spire and round it nothing is roofed.)
        foreach (var s in spires) Clear(roofOwn, ow, oh, new Vector2(s.X, s.Y), s.Z + 2f);
        // A boss arena's basin and the ways to it from its gates, carved as its own water is.
        if (arena)
        {
            var middle = c.Middle - origin;
            float r = MathF.Min(ArenaRadius, MathF.Min(c.Size.X, c.Size.Y) * 0.5f - 6f);
            Stamp(middle, r + 1f, (x, y) => Vector2.Distance(new Vector2(x, y), middle) < r, water, ow, oh);
            foreach (var (_, _, mouth) in gates)
                StampLine(new[] { mouth - origin, middle }, ArenaWay, (x, y, d) => d < ArenaWay * 0.5f, water, ow, oh);
        }
        // (A boss's basin is open to the sky; a tunnel's roof runs on two metres into the rock round it.)
        if (arena) Array.Clear(roofOwn);
        var roofed = (bool[])roofOwn.Clone();
        for (int v = 0; v < oh; v++)
        for (int u = 0; u < ow; u++)
        {
            if (!roofOwn[v * ow + u]) continue;
            for (int dv = -2; dv <= 2; dv++)
            for (int du = -2; du <= 2; du++)
            {
                int a = u + du, b = v + dv;
                if (a >= 0 && b >= 0 && a < ow && b < oh && !water[b * ow + a]) roofed[b * ow + a] = true;
            }
        }
        // Its gates' roofs: whether each is roofed, the same from both its cubes.
        var gateRoofs = new List<(EdgeId Edge, Gate Gate)>();
        foreach (var (edge, gate, _) in gates)
        {
            int index = 0;
            foreach (var e in edges)
                if (e.Id == edge)
                    for (int g = 0; g < e.Gates.Count; g++)
                        if (e.Gates[g] == gate) index = g;
            ulong h2 = ReefHash.Of(space.Seed, ReefHash.Edge, edge.X, edge.Y, edge.Z, 16 + (int)edge.Axis * 4 + index);
            if ((h2 >> 40) / (float)(1UL << 24) < RoofedGates) gateRoofs.Add((edge, gate));
        }
        bool UnderGateRoof(Vector2 p, float wall)
        {
            if (wall <= 0.3f) return false;
            foreach (var (edge, gate) in gateRoofs)
                if (MathF.Abs(edge.Across(p)) <= ThroatReach + ThroatPast && MathF.Abs(edge.Along(p) - gate.Along) <= gate.Width * 0.75f + 3.5f) return true;
            return false;
        }

        // 4. Every point's signed distance to the rock's face (CUBE-WORLD-PLAN §4.2; positive in rock): near its outer
        // edges what the edges and corners decide (shared with the neighbours, so its apron is theirs too), further in
        // that and its own carving, blurred so the face runs smoothly between the grid's points.
        var own = OwnField(water, ow, oh, gw, gh, m);
        var (allEdges, allCorners) = SharedNear(space, c.Z, gx0, gy0, gw, gh);
        // (The rock's top relief and the shelf noise are slow: read on a 4 m grid and between its points.)
        var topNoise = Coarse((x, y) => TerrainNoise.Fbm(x * 0.06f, y * 0.06f, topSeed, 3), gx0, gy0, gw, gh);
        var shelfNoises = Coarse((x, y) => TerrainNoise.Fbm(x * 0.035f, y * 0.035f, shelfSeed, 2), gx0, gy0, gw, gh);
        var field = new float[gw * gh];
        var cell = new CubeCell[gw * gh];
        var shelf = new float[gw * gh];
        var top = new float[gw * gh];
        var canopy = new bool[gw * gh];
        for (int j = 0; j < gh; j++)
        for (int i = 0; i < gw; i++)
        {
            int u = i - m, v = j - m, k = j * gw + i;
            var p = new Vector2(gx0 + i, gy0 + j);
            var (wall, throat) = SharedField(gx0 + i, gy0 + j, allEdges, allCorners, throatSeed);
            float near = float.MaxValue;
            foreach (var e in edges) near = MathF.Min(near, MathF.Abs(e.Id.Across(p)));
            float line = MathF.Min(wall, throat);
            float carved = MathF.Min(MathF.Max(wall, own[k]), throat);
            float s = Lerp(line, carved, Smooth(Apron, Apron + BlendWidth, near));
            // (A spire's rock laid on the field itself, so the blur never wears it away: its face round, its foot sloping.)
            foreach (var sp in spires)
                s = MathF.Max(s, sp.Z - Vector2.Distance(new Vector2(u, v), new Vector2(sp.X, sp.Y)));
            field[k] = s;
            var shared = throat < 0f ? true : wall > 0f ? false : (bool?)null;
            bool ownPoint = u >= 0 && v >= 0 && u <= w && v <= h;
            bool wet = s <= 0f;
            bool underRoof = UnderGateRoof(p, wall) || ownPoint && roofed[v * ow + u];
            if (!wet)
            {
                cell[k] = CubeCell.Rock;
                top[k] = RockLow + RockRise * Math.Clamp(topNoise[k] * 0.5f + 0.5f, 0f, 1f);
                canopy[k] = underRoof;
                continue;
            }
            var local = new Vector2(u, v);
            bool byWell = false, inWell = false;
            // How far a point is from being a well's void (0 inside it, 1 a little way out): its floor sinks smoothly into it.
            float rise = 1f;
            if (ownPoint && shared is null)
                foreach (var well in down)
                {
                    float d = Vector2.Distance(local, well.Center - origin);
                    // (Past its widest reach and the slope round it, nothing to work out.)
                    if (d >= well.Radius * 1.3f + 2.5f) continue;
                    float reach = well.Radius * (1f + 0.3f * MathF.Max(0f, TerrainNoise.Perlin(p.X * 0.15f, p.Y * 0.15f, floorSeed + 9u)));
                    if (d < reach) inWell = true;
                    rise = MathF.Min(rise, Smooth(reach, reach + 2.5f, d));
                }
            foreach (var well in Concat(down, arrivals))
                if (Vector2.Distance(local, well.Center - origin) < well.Radius + 2f) byWell = true;
            canopy[k] = underRoof && !byWell;
            // Void only where the shelf noise opens it over the layer below's caverns (CUBE-WORLD-PLAN §4.3), so what she
            // sees down through it is that layer's open water.
            float shelfNoise = shelfNoises[k];
            // (A drop's void more, blended in from its edges as its own carving is.)
            float more = 0f;
            if (cubeKind == CubeKind.Drop)
            {
                float along = (across ? u : v) - (across ? w : h) * 0.5f;
                more = DropMore * Smooth(Apron, Apron + BlendWidth, near) * Smooth(-4f, 10f, dropFar ? along : -along);
            }
            float open = MathF.Min(ShelfCut + VoidMore + more - shelfNoise, under[k] - CavernCut + 0.02f);
            // A natural arch: rock grown across over her where a band of its noise crosses water near the walls (the whole
            // way over a narrow way; over a wide one, a shelf of it reaching out from the wall). Only over a shelf's floor.
            if (!canopy[k] && !inWell && !byWell && open <= 0f && ownPoint && shared is null && -s < ArchReach && Arch(p, archSeed))
                canopy[k] = true;
            if (inWell) cell[k] = CubeCell.Void;
            else if (canopy[k]) cell[k] = CubeCell.Shelf;
            else cell[k] = open > 0f ? CubeCell.Void : CubeCell.Shelf;
            // The floor's height follows that contour smoothly (no 1 m steps along a shelf's edge), and sinks into a well;
            // under a roof it is a shelf.
            shelf[k] = inWell ? 0f : canopy[k] ? rise : MathF.Min(rise, Smooth(-ShelfEdge, ShelfEdge, -open));
        }

        // 5. Heights, cropped to the apron, laid on the field: the rock rising from its face over RockRamp to its own
        // height, the floor falling from it to a shelf's (a little uneven) or the void's depth, the deeper the further.
        int nw = w + 1 + 2 * Apron, nh = h + 1 + 2 * Apron;
        var outCells = new CubeCell[nw * nh];
        var outHeights = new float[nw * nh];
        var outCanopy = new bool[nw * nh];
        var outOpen = new float[nw * nh];
        for (int j = 0; j < nh; j++)
        for (int i = 0; i < nw; i++)
        {
            int si = i + Lip, sj = j + Lip, k = sj * gw + si, o = j * nw + i;
            var kind = cell[k];
            outCells[o] = kind;
            outCanopy[o] = canopy[k];
            outOpen[o] = c.Z == 0 ? 0f : Smooth(CavernCut - 0.05f, CavernCut + 0.05f, over[k]);
            float gx = gx0 + si, gy = gy0 + sj;
            float s = field[k];
            if (kind == CubeCell.Rock) outHeights[o] = top[k] * Smooth(0f, RockRamp, s);
            else
            {
                // (No relief worked out where the floor is the void's.)
                float floor = shelf[k] <= 0f ? VoidHeight : VoidHeight + (ShelfHeight + Relief(gx, gy, floorSeed, c.Z) - VoidHeight) * shelf[k];
                outHeights[o] = floor * Smooth(0f, FloorRamp + FloorRampPerMetre * -floor, -s);
            }
        }

        // 6. A boss arena's basin; else its sockets, cut into its walls.
        float[]? roof = null, uncut = null;
        var sockets = new List<Doorway>();
        if (!arena) sockets = CutSockets(space.Seed, c, outCells, outHeights, nw, nh, c.X * Side - Apron, c.Y * Side - Apron, gates, down, arrivals, outCanopy, rng, out roof, out uncut);

        return new CubeTerrain
        {
            Cube = c,
            Sockets = sockets,
            Arena = arena,
            Kind = cubeKind,
            Spires = spires.ConvertAll(s => new Vector3(s.X + origin.X, s.Y + origin.Y, s.Z)),
            Roof = roof,
            Uncut = uncut,
            Stride = nw,
            Rows = nh,
            X0 = c.X * Side - Apron,
            Y0 = c.Y * Side - Apron,
            Cells = outCells,
            Heights = outHeights,
            Canopy = outCanopy,
            UpperOpen = outOpen,
            Gates = gates,
            Down = down,
            Arrivals = arrivals,
            Tunnels = tunnels.ConvertAll(t => Array.ConvertAll(t.Points, q => q + origin)),
        };
    }

    /// <summary>
    /// Whether a natural arch's band runs over a point: thin bands along the contours of a slow noise, about ArchWidth
    /// across, and only where a second noise lets them (so about half of them grow).
    /// </summary>
    static bool Arch(Vector2 p, uint seed)
    {
        float n = TerrainNoise.Perlin(p.X * ArchScale, p.Y * ArchScale, seed);
        if (MathF.Abs(n) > ArchBand) return false;
        return TerrainNoise.Perlin(p.X * 0.02f, p.Y * 0.02f, seed + 5u) > 0f;
    }

    /// <summary>
    /// A natural arch's noise scale, the half-width of its band in that noise (about 4–5 m of rock across), and how far
    /// out over the water from a wall it reaches.
    /// </summary>
    const float ArchScale = 0.03f, ArchBand = 0.05f, ArchReach = 5f;

    /// <summary>
    /// A shelf floor's relief (metres over or under ShelfHeight): never flat, yet not bumpy. A broad, slow noise is
    /// stepped into terraces: each step's top all but level, falling away smoothly to the next over most of a step (so
    /// a flat stretch's edges are lowered gradually into slopes), the steps a couple of metres to a few across. The
    /// warm-up layer's half as deep (broader, gentler sand). Always well under her plane.
    /// </summary>
    public static float Relief(float x, float y, uint seed, int layer)
    {
        float n = TerrainNoise.Perlin(x * ReliefScale, y * ReliefScale, seed + 31u);
        float steps = (n * 0.5f + 0.5f) * ReliefSteps;
        float whole = MathF.Floor(steps), part = steps - whole;
        // (Level for its first ReliefFlat of a step, then easing down to the next: a plateau with a soft edge.)
        float edge = Smooth(ReliefFlat, 1f, part);
        float terraced = (whole + edge) / ReliefSteps;
        float depth = layer == 0 ? 0.5f : 1f;
        return (ReliefTop - ReliefDepth * (1f - terraced)) * depth;
    }

    /// <summary>
    /// The relief's noise scale (per metre), its steps across the noise's range, the share of a step kept level, its top
    /// over ShelfHeight and its whole depth.
    /// </summary>
    const float ReliefScale = 0.16f, ReliefSteps = 5f, ReliefFlat = 0.35f, ReliefTop = 0.35f, ReliefDepth = 1.8f;

    /// <summary>How far in from its outer edges a cube's own carving blends in (past its apron), and the rock's and floor's ramps from the face.</summary>
    const float BlendWidth = 3f, RockRamp = 5f, FloorRamp = 0.8f, FloorRampPerMetre = 0.3f;
    /// <summary>A throat runs on this far past its mouth, closing as it goes, into the cube's own water there.</summary>
    const float ThroatPast = 2f;
    /// <summary>The cube's own water widened by this much (metres), so the blur never pinches a narrow way shut.</summary>
    const float Widen = 0.6f;

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Every edge and corner of the layer whose wall or throat can reach the worked grid: one field for the whole layer,
    /// so where two cubes both hold a point they work out the same. (Between the two squares of one cube there is none.)
    /// </summary>
    /// <summary>
    /// An edge near the worked grid, with its wall's wander worked out once a metre along it (the grid's points lie on whole
    /// metres along and across every line): its line's shift and each face's swell, and its throats' sway across it.
    /// </summary>
    sealed class NearEdge
    {
        public required EdgeSpec Spec;
        public required int S0;
        /// <summary>Its line, where it starts along it, and whether it runs north-south (an X edge).</summary>
        public required float Line, Start;
        public required bool IsX;
        public required float[] Shift, SwellLow, SwellHigh, Sway;
    }

    static (NearEdge[] Edges, CornerSpec[] Corners) SharedNear(CubeSpace space, int z, int gx0, int gy0, int gw, int gh)
    {
        int reach = (int)MathF.Ceiling(WallMax + FieldReach);
        int x0 = FloorDiv(gx0 - reach, Side) - 1, x1 = FloorDiv(gx0 + gw + reach, Side) + 1;
        int y0 = FloorDiv(gy0 - reach, Side) - 1, y1 = FloorDiv(gy0 + gh + reach, Side) + 1;
        var edges = new List<NearEdge>();
        var corners = new List<CornerSpec>();
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            foreach (var axis in new[] { EdgeAxis.X, EdgeAxis.Y })
            {
                var id = new EdgeId(x, y, z, axis);
                if (CubeId.Of(id.Low) == CubeId.Of(id.High)) continue;
                // (Only one whose line passes near the grid.)
                float line = id.Line, lo = axis == EdgeAxis.X ? gx0 : gy0, hi = lo + (axis == EdgeAxis.X ? gw : gh);
                float start = id.Start, alo = axis == EdgeAxis.X ? gy0 : gx0, ahi = alo + (axis == EdgeAxis.X ? gh : gw);
                if (line < lo - reach || line > hi + reach || start + Side < alo - reach || start > ahi + reach) continue;
                var spec = space.Edge(id);
                int s0 = (int)alo, n = (int)(ahi - alo) + 1, swayN = 2 * ((int)ThroatReach + (int)ThroatPast) + 1;
                var near = new NearEdge { Spec = spec, S0 = s0, Line = id.Line, Start = id.Start, IsX = axis == EdgeAxis.X, Shift = new float[n], SwellLow = new float[n], SwellHigh = new float[n], Sway = new float[swayN] };
                for (int q = 0; q < n; q++)
                {
                    float g = s0 + q;
                    // (Read at the layer's depth: the wall's seed is its line's, so it wanders on slowly down the layers.)
                    near.Shift[q] = WallShift * Math.Clamp(TerrainNoise.Perlin(g * 0.05f, 0.5f + z * WallDepth, spec.WallSeed), -1f, 1f);
                    // (Each face swells on its own, so the wall's two faces wander apart.)
                    near.SwellLow[q] = WallSwell * Math.Clamp(TerrainNoise.Fbm(g * 0.13f, 3.7f + z * WallDepth, spec.WallSeed + 1u, 2) * 0.6f + 0.5f, 0f, 1f);
                    near.SwellHigh[q] = WallSwell * Math.Clamp(TerrainNoise.Fbm(g * 0.13f, 11.3f + z * WallDepth, spec.WallSeed + 1u, 2) * 0.6f + 0.5f, 0f, 1f);
                }
                if (spec.Open)
                    for (int q = 0; q < swayN; q++) near.Sway[q] = 2.2f * TerrainNoise.Perlin((q - swayN / 2) * 0.11f, 7.3f, spec.WallSeed + 5u);
                edges.Add(near);
            }
            var corner = new CornerId(x, y, z);
            if (corner.Point.X >= gx0 - CornerMax - 1 && corner.Point.X <= gx0 + gw + CornerMax + 1 && corner.Point.Y >= gy0 - CornerMax - 1 && corner.Point.Y <= gy0 + gh + CornerMax + 1)
                corners.Add(space.Corner(corner));
        }
        return (edges.ToArray(), corners.ToArray());
    }

    static float[] Filled(int n, float v)
    {
        var a = new float[n];
        Array.Fill(a, v);
        return a;
    }

    /// <summary>
    /// The cavern field of layer <paramref name="z"/> on the worked grid: read every <paramref name="Step"/> metres on the
    /// global grid and blended between (its features are tens of metres; the layers either side, read coarser, only soften), so every cube that holds a point reads the same.
    /// </summary>
    static float[] StrataGrid(CubeSpace space, int z, int gx0, int gy0, int gw, int gh, int Step = 2)
    {
        int cx0 = FloorDiv(gx0, Step) * Step, cy0 = FloorDiv(gy0, Step) * Step;
        int cw = (gx0 + gw - cx0) / Step + 2, ch = (gy0 + gh - cy0) / Step + 2;
        var coarse = new float[cw * ch];
        var slice = space.StrataKeys(NoiseCavern, z);
        for (int j = 0; j < ch; j++)
        for (int i = 0; i < cw; i++) coarse[j * cw + i] = slice.At(cx0 + i * Step, cy0 + j * Step, CavernScale);
        var f = new float[gw * gh];
        for (int j = 0; j < gh; j++)
        {
            int ry = gy0 + j - cy0, cj = ry / Step;
            float v = (ry - cj * Step) / (float)Step;
            for (int i = 0; i < gw; i++)
            {
                int rx = gx0 + i - cx0, ci = rx / Step;
                float u = (rx - ci * Step) / (float)Step;
                int k = cj * cw + ci;
                float a = coarse[k] + (coarse[k + 1] - coarse[k]) * u, b = coarse[k + cw] + (coarse[k + cw + 1] - coarse[k + cw]) * u;
                f[j * gw + i] = a + (b - a) * v;
            }
        }
        return f;
    }

    /// <summary>
    /// A slow field over the worked grid, read every CoarseStep metres (on the global grid, so neighbours agree) and
    /// bilinearly between.
    /// </summary>
    static float[] Coarse(Func<float, float, float> f, int gx0, int gy0, int gw, int gh)
    {
        const int Step = CoarseStep;
        int cx0 = FloorDiv(gx0, Step) * Step, cy0 = FloorDiv(gy0, Step) * Step;
        int cw = (gx0 + gw - cx0) / Step + 2, ch = (gy0 + gh - cy0) / Step + 2;
        var coarse = new float[cw * ch];
        for (int j = 0; j < ch; j++)
        for (int i = 0; i < cw; i++) coarse[j * cw + i] = f(cx0 + i * Step, cy0 + j * Step);
        var o = new float[gw * gh];
        for (int j = 0; j < gh; j++)
        {
            int ry = gy0 + j - cy0, cj = ry / Step;
            float v = (ry - cj * Step) / (float)Step;
            for (int i = 0; i < gw; i++)
            {
                int rx = gx0 + i - cx0, ci = rx / Step;
                float u = (rx - ci * Step) / (float)Step;
                int k = cj * cw + ci;
                float a = coarse[k] + (coarse[k + 1] - coarse[k]) * u, b = coarse[k + cw] + (coarse[k + cw + 1] - coarse[k + cw]) * u;
                o[j * gw + i] = a + (b - a) * v;
            }
        }
        return o;
    }

    const int CoarseStep = 4;

    /// <summary>How far a wall's wander moves on (in its noise) from one layer to the next.</summary>
    const float WallDepth = 0.12f;

    /// <summary>
    /// Its caverns joined up: each patch of water (off its walls' band) not joined to its gates and wells is filled if
    /// small, else bored through to the nearest water that is (into <paramref name="bore"/>, roofed where through rock).
    /// </summary>
    static void Join(bool[] water, bool[] bore, bool[] blocked, List<Vector2> nodes, int ow, int oh, Rng rng, uint tunnelSeed, Vector2 origin)
    {
        const int Least = 30;
        var label = new int[ow * oh];
        Array.Fill(label, -1);
        var sizes = new List<int>();
        var todo = new Queue<int>();
        for (int s = 0; s < label.Length; s++)
        {
            if (!water[s] || blocked[s] || label[s] >= 0) continue;
            int id = sizes.Count, size = 0;
            label[s] = id;
            todo.Enqueue(s);
            while (todo.Count > 0)
            {
                int k = todo.Dequeue();
                size++;
                int x = k % ow, y = k / ow;
                foreach (var (dx, dy) in Steps)
                {
                    int a = x + dx, b = y + dy;
                    if (a < 0 || b < 0 || a >= ow || b >= oh) continue;
                    int q = b * ow + a;
                    if (!water[q] || blocked[q] || label[q] >= 0) continue;
                    label[q] = id;
                    todo.Enqueue(q);
                }
            }
            sizes.Add(size);
        }
        var main = new bool[sizes.Count];
        foreach (var n in nodes)
        {
            int x = Math.Clamp((int)MathF.Round(n.X), 0, ow - 1), y = Math.Clamp((int)MathF.Round(n.Y), 0, oh - 1);
            if (label[y * ow + x] >= 0) main[label[y * ow + x]] = true;
        }
        // Every point's nearest joined water (breadth first from all of it, off the walls' band).
        var from = new int[ow * oh];
        var dist = new int[ow * oh];
        Array.Fill(from, -1);
        for (int k = 0; k < label.Length; k++)
            if (label[k] >= 0 && main[label[k]])
            {
                from[k] = k;
                todo.Enqueue(k);
            }
        while (todo.Count > 0)
        {
            int k = todo.Dequeue();
            int x = k % ow, y = k / ow;
            foreach (var (dx, dy) in Steps)
            {
                int a = x + dx, b = y + dy;
                if (a < 0 || b < 0 || a >= ow || b >= oh) continue;
                int q = b * ow + a;
                if (blocked[q] || from[q] >= 0) continue;
                from[q] = from[k];
                dist[q] = dist[k] + 1;
                todo.Enqueue(q);
            }
        }
        var best = new int[sizes.Count];
        Array.Fill(best, -1);
        for (int k = 0; k < label.Length; k++)
        {
            int id = label[k];
            if (id < 0 || main[id] || from[k] < 0) continue;
            if (best[id] < 0 || dist[k] < dist[best[id]]) best[id] = k;
        }
        for (int id = 0; id < sizes.Count; id++)
        {
            if (main[id]) continue;
            if (sizes[id] < Least || best[id] < 0)
            {
                for (int k = 0; k < label.Length; k++)
                    if (label[k] == id) water[k] = false;
                continue;
            }
            int a = best[id], b = from[a];
            var p0 = new Vector2(a % ow, a / ow);
            var p1 = new Vector2(b % ow, b / ow);
            float half = rng.Range(2.6f, 3.4f);
            StampLine(new[] { p0, Vector2.Lerp(p0, p1, 0.5f), p1 }, half + 1.5f, (x, y, d) =>
                d < half + 0.9f * TerrainNoise.Fbm((origin.X + x) * 0.1f, (origin.Y + y) * 0.1f, tunnelSeed, 2), bore, ow, oh);
        }
    }

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    /// <summary>
    /// What the edges and corners decide at a point, as signed distances (the same field for every cube): the shared
    /// walls' and corners' rock (positive inside), and the gates' throats' water (negative inside, closing over their last
    /// metres). An edge's wall and throat hold along its own stretch of line (and a little past its ends).
    /// </summary>
    static (float Wall, float Throat) SharedField(int gx, int gy, NearEdge[] edges, CornerSpec[] corners, uint throatSeed)
    {
        var p = new Vector2(gx, gy);
        float wall = -FieldReach, throat = 1e6f;
        const float Reach = WallMax + FieldReach;
        for (int n = 0; n < edges.Length; n++)
        {
            var near = edges[n];
            float t = (near.IsX ? gx : gy) - near.Line;
            if (t > Reach || t < -Reach) continue;
            float along = (near.IsX ? gy : gx) - near.Start;
            if (along < -Reach || along > Side + Reach) continue;
            var e = near.Spec;
            int q = (near.IsX ? gy : gx) - near.S0;
            float shift = near.Shift[q];
            float swell = t < shift ? near.SwellLow[q] : near.SwellHigh[q];
            // (Past its own stretch of line it tapers away, so no wall ever ends in a cut.)
            float past = MathF.Max(0f, MathF.Max(-along, along - Side));
            wall = MathF.Max(wall, WallCore + MathF.Abs(shift) + swell - MathF.Abs(t - shift) - past);
            if (!e.Open || MathF.Abs(t) > ThroatReach + ThroatPast) continue;
            // (Swaying from side to side as it goes through, and flaring where it opens into each cube.)
            float sway = near.Sway[(int)MathF.Round(t) + near.Sway.Length / 2];
            float flare = 1f + 0.5f * (t / ThroatReach) * (t / ThroatReach);
            float closing = 6f * MathF.Max(0f, MathF.Abs(t) - ThroatReach);
            float wobble = float.NaN;
            foreach (var gate in e.Gates)
            {
                float off = MathF.Abs(along - gate.Along - sway);
                // (Far from the gate it is rock whatever the wobble: the noise only where it may matter.)
                if (off > gate.Width * flare + 2f) continue;
                if (float.IsNaN(wobble)) wobble = 0.9f * TerrainNoise.Perlin(p.X * 0.15f, p.Y * 0.15f, throatSeed);
                throat = MathF.Min(throat, off - (gate.Width * 0.5f * flare + wobble) + closing);
            }
        }
        foreach (var k in corners) wall = MathF.Max(wall, k.Radius - Vector2.Distance(p, k.Id.Point));
        return (wall, throat);
    }

    /// <summary>
    /// Bays bitten into the rock round the carved water open to the sky (none off a roofed tunnel: no skylights in its
    /// roof): rock within BayReach of it gives way where the bay noise is high, so the walls wander instead of tracing
    /// the caverns and chambers.
    /// </summary>
    static void Bays(bool[] water, bool[] roofed, Vector2 origin, uint seed, int ow, int oh)
    {
        const int BayReach = 5;
        var near = new int[ow * oh];
        var todo = new Queue<int>();
        for (int k = 0; k < near.Length; k++)
        {
            bool from = water[k] && !roofed[k];
            near[k] = from ? 0 : water[k] ? -1 : int.MaxValue;
            if (from) todo.Enqueue(k);
        }
        while (todo.Count > 0)
        {
            int k = todo.Dequeue();
            if (near[k] >= BayReach) continue;
            int x = k % ow, y = k / ow;
            foreach (var (dx, dy) in Steps)
            {
                int a = x + dx, b = y + dy;
                if (a < 0 || b < 0 || a >= ow || b >= oh || near[b * ow + a] < 0 || near[b * ow + a] <= near[k] + 1) continue;
                near[b * ow + a] = near[k] + 1;
                todo.Enqueue(b * ow + a);
            }
        }
        for (int k = 0; k < near.Length; k++)
        {
            if (near[k] <= 0 || near[k] > BayReach) continue;
            float n = TerrainNoise.Fbm((origin.X + k % ow) * 0.09f, (origin.Y + k / ow) * 0.09f, seed, 2);
            if (n > 0.08f + 0.07f * near[k]) water[k] = true;
        }
    }

    static readonly (int, int)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>Distances on the grid to the nearest point that is <paramref name="of"/> (8-way chamfer, 1 and √2).</summary>
    static float[] Chamfer(bool[] wet, int gw, int gh, bool of)
    {
        const float Diagonal = 1.41421356f;
        var d = new float[gw * gh];
        for (int k = 0; k < d.Length; k++) d[k] = wet[k] == of ? 0f : 1e6f;
        for (int j = 0; j < gh; j++)
        for (int i = 0; i < gw; i++)
        {
            int k = j * gw + i;
            float v = d[k];
            if (i > 0) v = MathF.Min(v, d[k - 1] + 1f);
            if (j > 0)
            {
                v = MathF.Min(v, d[k - gw] + 1f);
                if (i > 0) v = MathF.Min(v, d[k - gw - 1] + Diagonal);
                if (i < gw - 1) v = MathF.Min(v, d[k - gw + 1] + Diagonal);
            }
            d[k] = v;
        }
        for (int j = gh - 1; j >= 0; j--)
        for (int i = gw - 1; i >= 0; i--)
        {
            int k = j * gw + i;
            float v = d[k];
            if (i < gw - 1) v = MathF.Min(v, d[k + 1] + 1f);
            if (j < gh - 1)
            {
                v = MathF.Min(v, d[k + gw] + 1f);
                if (i < gw - 1) v = MathF.Min(v, d[k + gw + 1] + Diagonal);
                if (i > 0) v = MathF.Min(v, d[k + gw - 1] + Diagonal);
            }
            d[k] = v;
        }
        return d;
    }

    /// <summary>How far a face is sought (the field is clamped there), and the blur's passes.</summary>
    const int FieldReach = 6, BlurPasses = 2;

    /// <summary>
    /// The cube's own carving as a signed distance on the worked grid (positive in rock, metres to the nearest point of the
    /// other kind less half a metre, clamped to FieldReach), blurred so its face is a smooth line, not the grid's steps.
    /// Outside its own square it is rock.
    /// </summary>
    static float[] OwnField(bool[] water, int ow, int oh, int gw, int gh, int m)
    {
        var wet = new bool[gw * gh];
        for (int j = 0; j < gh; j++)
        for (int i = 0; i < gw; i++)
        {
            int u = i - m, v = j - m;
            wet[j * gw + i] = u >= 0 && v >= 0 && u < ow && v < oh && water[v * ow + u];
        }
        // Each point's distance to the nearest point of the other kind (two passes of an 8-way chamfer), less half a metre.
        var toWet = Chamfer(wet, gw, gh, true);
        var toDry = Chamfer(wet, gw, gh, false);
        var f = new float[gw * gh];
        for (int k = 0; k < f.Length; k++)
            f[k] = wet[k] ? -MathF.Min(toDry[k] - 0.5f, FieldReach) - Widen : MathF.Min(toWet[k] - 0.5f, FieldReach) - Widen;
        var tmp = new float[gw * gh];
        ReadOnlySpan<float> kernel = stackalloc float[] { 1f / 16f, 4f / 16f, 6f / 16f, 4f / 16f, 1f / 16f };
        for (int pass = 0; pass < BlurPasses; pass++)
        {
            for (int j = 0; j < gh; j++)
            for (int i = 0; i < gw; i++)
            {
                float sum = 0f;
                for (int q = -2; q <= 2; q++) sum += kernel[q + 2] * f[j * gw + Math.Clamp(i + q, 0, gw - 1)];
                tmp[j * gw + i] = sum;
            }
            for (int j = 0; j < gh; j++)
            for (int i = 0; i < gw; i++)
            {
                float sum = 0f;
                for (int q = -2; q <= 2; q++) sum += kernel[q + 2] * tmp[Math.Clamp(j + q, 0, gh - 1) * gw + i];
                f[j * gw + i] = sum;
            }
        }
        return f;
    }

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
