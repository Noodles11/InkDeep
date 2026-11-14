using System;
using System.Collections.Generic;
using System.Numerics;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// What is remembered of a claimed cube (CUBE-WORLD-PLAN §10): its claim number on its layer and what she has seen of
/// it (fog of war, a bit a metre of its squares). Its terrain is never kept: it is made again from the seed and this is
/// laid over it, so a cube she comes back to is as she left it.
/// </summary>
public sealed class CubeRecord
{
    public CubeRecord(CubeId cube, int claim)
    {
        Cube = cube;
        Claim = claim;
        Seen = new ulong[cube.Wide * cube.Tall * Side * Side / 64];
    }

    public CubeId Cube { get; }
    /// <summary>The how-manyth cube of its layer she claimed (from 1).</summary>
    public int Claim { get; }
    /// <summary>Seen, a bit a metre over its squares (row by row across the cube).</summary>
    public ulong[] Seen { get; }
    /// <summary>
    /// Its stock as she last left the layer (CubeStock: the murklings still living, where they were); null while she has
    /// not left it since claiming it. A cube she arrived in by a dive or a climb has none.
    /// </summary>
    public List<StockLeft>? Left { get; set; }

    int Bit(int gx, int gy)
    {
        int x = gx - Cube.X * Side, y = gy - Cube.Y * Side;
        return x < 0 || y < 0 || x >= Cube.Wide * Side || y >= Cube.Tall * Side ? -1 : y * Cube.Wide * Side + x;
    }

    public bool WasSeen(int gx, int gy) => Bit(gx, gy) is int b and >= 0 && (Seen[b >> 6] & (1UL << (b & 63))) != 0;

    public void See(int gx, int gy)
    {
        if (Bit(gx, gy) is int b and >= 0) Seen[b >> 6] |= 1UL << (b & 63);
    }

    public int SeenCount
    {
        get
        {
            int n = 0;
            foreach (var w in Seen) n += System.Numerics.BitOperations.PopCount(w);
            return n;
        }
    }
}

/// <summary>What happened as she moved this tick (CUBE-WORLD-PLAN §6, §8.2).</summary>
public enum Crossing : byte
{
    /// <summary>She stayed in her cube.</summary>
    None,
    /// <summary>Into a cube she had claimed already.</summary>
    Back,
    /// <summary>Into a cube she had never been in: claimed now.</summary>
    Claimed,
    /// <summary>Against an edge the wake current holds: she is kept on her side.</summary>
    Held,
}

/// <summary>
/// One layer's records (CUBE-WORLD-PLAN §6, §8.2, §10): the cubes she has claimed (the first time she enters each), the
/// edges the wake current holds (each time she crosses an edge, the one she crossed before it is shut, for as long as
/// she stays on the layer), and what she has seen.
/// </summary>
public sealed class LayerRecords
{
    public LayerRecords(int layer) => Layer = layer;

    public int Layer { get; }
    readonly Dictionary<CubeId, CubeRecord> _claimed = new();
    readonly HashSet<EdgeId> _shut = new();

    public IReadOnlyDictionary<CubeId, CubeRecord> Claimed => _claimed;
    public IReadOnlyCollection<EdgeId> Shut => _shut;
    /// <summary>The edge she crossed last (null: none yet on this layer).</summary>
    public EdgeId? Last { get; private set; }
    public int Claims => _claimed.Count;

    public bool IsClaimed(CubeId cube) => _claimed.ContainsKey(cube);
    public bool IsShut(EdgeId edge) => _shut.Contains(edge);

    /// <summary>She arrives on the layer at a point (from the start, or a dive or climb): the cube there claimed.</summary>
    public Crossing Arrive(Vector2 at)
    {
        var cube = CubeId.Of(at, Layer);
        if (_claimed.ContainsKey(cube)) return Crossing.Back;
        _claimed[cube] = new CubeRecord(cube, _claimed.Count + 1);
        return Crossing.Claimed;
    }

    /// <summary>
    /// She moved from <paramref name="from"/> to <paramref name="to"/> (one tick, under a metre): into another cube across
    /// an edge (claiming it the first time), or held against a shut edge, in which case <paramref name="to"/> is put back
    /// to <paramref name="from"/>.
    /// </summary>
    public Crossing Move(Vector2 from, ref Vector2 to)
    {
        var a = CellCoord.Of(from, Layer);
        var b = CellCoord.Of(to, Layer);
        if (CubeId.Of(a) == CubeId.Of(b)) return Crossing.None;
        // (Across one edge of a square: a corner's rock keeps her from crossing two at once.)
        var edge = b.X != a.X
            ? new EdgeId(Math.Min(a.X, b.X), a.Y, Layer, EdgeAxis.X)
            : new EdgeId(a.X, Math.Min(a.Y, b.Y), Layer, EdgeAxis.Y);
        if (_shut.Contains(edge))
        {
            to = from;
            return Crossing.Held;
        }
        if (Last is { } last && last != edge) _shut.Add(last);
        Last = edge;
        var cube = CubeId.Of(b);
        if (_claimed.ContainsKey(cube)) return Crossing.Back;
        _claimed[cube] = new CubeRecord(cube, _claimed.Count + 1);
        return Crossing.Claimed;
    }

    /// <summary>
    /// What she sees round a point (a disc of <paramref name="radius"/>) marked seen: all of it in claimed cubes; in a
    /// cube not claimed yet nothing (it stays unseen until she enters it, so nothing she has looked at can change).
    /// </summary>
    public void Reveal(Vector2 at, float radius)
    {
        int r = (int)MathF.Ceiling(radius);
        int cx = (int)MathF.Round(at.X), cy = (int)MathF.Round(at.Y);
        CubeRecord? record = null;
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            if ((x - at.X) * (x - at.X) + (y - at.Y) * (y - at.Y) > radius * radius) continue;
            var cube = CubeId.Of(new CellCoord(FloorDiv(x, Side), FloorDiv(y, Side), Layer));
            if (record?.Cube != cube && !_claimed.TryGetValue(cube, out record)) continue;
            record!.See(x, y);
        }
    }

    public bool WasSeen(Vector2 p) =>
        _claimed.TryGetValue(CubeId.Of(p, Layer), out var r) && r.WasSeen((int)MathF.Round(p.X), (int)MathF.Round(p.Y));

    /// <summary>She leaves the layer: its shut edges open again (the wake current holds only while she is on it).</summary>
    public void Leave()
    {
        _shut.Clear();
        Last = null;
    }

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}

/// <summary>Every layer's records she keeps: her layer's and the one above (CUBE-WORLD-PLAN §10).</summary>
public sealed class ReefRecords
{
    readonly Dictionary<int, LayerRecords> _layers = new();

    public LayerRecords Of(int layer)
    {
        if (!_layers.TryGetValue(layer, out var r)) _layers[layer] = r = new LayerRecords(layer);
        return r;
    }

    public IEnumerable<int> Layers => _layers.Keys;

    /// <summary>She has changed layer, from <paramref name="from"/> to <paramref name="to"/>: the layer left opens its shut edges; records two layers above her deepest are let go (she can never return there).</summary>
    public void Changed(int from, int to, int deepest)
    {
        Of(from).Leave();
        var gone = new List<int>();
        foreach (var z in _layers.Keys)
            if (z < deepest - 1) gone.Add(z);
        foreach (var z in gone) _layers.Remove(z);
        Of(to);
    }
}

/// <summary>
/// The cube world's two currents (CUBE-WORLD-PLAN §8): the frontier drift in the cube she is in, leaning her gently
/// toward the nearest gate into a cube she has not claimed (else toward the nearest way down), along the water; and the
/// wake current, pouring out of every shut edge's throats on her side, too strong to swim against.
/// </summary>
public sealed class CubeCurrents
{
    /// <summary>The drift's speed (a seventh of her cruise: felt when she idles, nothing to swim against).</summary>
    public const float Drift = 0.6f;
    /// <summary>The wake current's push at a shut edge's line, falling off over WakeReach into her cube.</summary>
    public const float Wake = 9f, WakeReach = ThroatReach;

    /// <summary>The cube the drift was worked out for, and the claims it knew of.</summary>
    public CubeId? Cube { get; private set; }
    int _claimsFor = -1, _shutFor = -1;
    int[]? _steps;
    CubeTerrain? _terrain;
    /// <summary>Where the drift leads in the cube (gate mouths into unclaimed cubes, or its ways down).</summary>
    public List<Vector2> Targets { get; } = new();

    const int Far = int.MaxValue;

    /// <summary>The drift's field for her cube, worked out again when she changes cube or what she has claimed or shut changes.</summary>
    public void Prepare(CubeTerrain terrain, CubeSpace space, LayerRecords records)
    {
        if (Cube == terrain.Cube && _claimsFor == records.Claims && _shutFor == records.Shut.Count && ReferenceEquals(_terrain, terrain)) return;
        Cube = terrain.Cube;
        _terrain = terrain;
        _claimsFor = records.Claims;
        _shutFor = records.Shut.Count;
        Targets.Clear();
        foreach (var (edge, gate, mouth) in terrain.Gates)
        {
            var across = CubeId.Of(edge.Low) == terrain.Cube ? CubeId.Of(edge.High) : CubeId.Of(edge.Low);
            if (!records.IsClaimed(across) && !records.IsShut(edge) && space.Inside(across)) Targets.Add(edge.Point(gate.Along, 0f));
        }
        if (Targets.Count == 0)
            foreach (var well in terrain.Down) Targets.Add(well.Center);
        // Steps over the water to the nearest target (4-way on the 1 m grid, through water only).
        int w = terrain.Stride, h = terrain.Rows;
        _steps = new int[w * h];
        Array.Fill(_steps, Far);
        var todo = new Queue<int>();
        foreach (var t in Targets)
        {
            int x = (int)MathF.Round(t.X) - terrain.X0, y = (int)MathF.Round(t.Y) - terrain.Y0;
            if (x < 0 || y < 0 || x >= w || y >= h || terrain.Cells[y * w + x] == CubeCell.Rock) continue;
            _steps[y * w + x] = 0;
            todo.Enqueue(y * w + x);
        }
        while (todo.Count > 0)
        {
            int k = todo.Dequeue(), x = k % w, y = k / w;
            foreach (var (dx, dy) in Four)
            {
                int a = x + dx, b = y + dy;
                if (a < 0 || b < 0 || a >= w || b >= h) continue;
                int n = b * w + a;
                if (_steps[n] != Far || terrain.Cells[n] == CubeCell.Rock) continue;
                _steps[n] = _steps[k] + 1;
                todo.Enqueue(n);
            }
        }
    }

    static readonly (int, int)[] Four = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>How many metres over the water a point of her cube is from the nearest of the drift's targets (−1: none, or not her cube).</summary>
    public int StepsAt(Vector2 p)
    {
        if (_terrain is not { } t || _steps is null) return -1;
        int x = (int)MathF.Round(p.X) - t.X0, y = (int)MathF.Round(p.Y) - t.Y0;
        if (x < 0 || y < 0 || x >= t.Stride || y >= t.Rows) return -1;
        int s = _steps[y * t.Stride + x];
        return s == Far ? -1 : s;
    }

    /// <summary>The drift at a point of her cube (a velocity, metres a second): down the steps toward the nearest target.</summary>
    public Vector2 DriftAt(Vector2 p)
    {
        if (_terrain is not { } t || _steps is null) return Vector2.Zero;
        int x = (int)MathF.Round(p.X) - t.X0, y = (int)MathF.Round(p.Y) - t.Y0, w = t.Stride, h = t.Rows;
        if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1) return Vector2.Zero;
        int here = _steps[y * w + x];
        if (here == Far || here == 0) return Vector2.Zero;
        // Toward the lower of the neighbours, weighed by how much lower (a smooth way round corners).
        var way = Vector2.Zero;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            int s = _steps[(y + dy) * w + x + dx];
            if (s == Far || s >= here) continue;
            way += new Vector2(dx, dy) / MathF.Sqrt(dx * dx + dy * dy) * (here - s);
        }
        return way.LengthSquared() > 1e-6f ? Vector2.Normalize(way) * Drift : Vector2.Zero;
    }

    /// <summary>The wake current at a point of her cube: out of each shut edge's throats on her side, strongest at the line.</summary>
    public static Vector2 WakeAt(Vector2 p, CubeId cube, CubeSpace space, LayerRecords records)
    {
        var push = Vector2.Zero;
        foreach (var (edge, inside, _) in cube.Outer())
        {
            if (!records.IsShut(edge)) continue;
            var spec = space.Edge(edge);
            float t = edge.Across(p), into = edge.High == inside ? 1f : -1f;
            // (Her side of it: into her cube.)
            if (t * into < -0.5f || MathF.Abs(t) > WakeReach) continue;
            float along = edge.Along(p);
            foreach (var gate in spec.Gates)
                if (MathF.Abs(along - gate.Along) < gate.Width * 0.75f + 2f)
                {
                    var outward = edge.Axis == EdgeAxis.X ? new Vector2(into, 0f) : new Vector2(0f, into);
                    push += outward * Wake * (1f - MathF.Abs(t) / WakeReach);
                }
        }
        return push;
    }
}
