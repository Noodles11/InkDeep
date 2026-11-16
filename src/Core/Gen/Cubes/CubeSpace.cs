using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>A way through an edge: its middle, metres along the edge from its start, and its width.</summary>
public readonly record struct Gate(float Along, float Width);

/// <summary>
/// An edge's boundary conditions (CUBE-WORLD-PLAN §4.1): open with one or two gates (each kept GateMargin off both
/// corners), or closed; and the seeds of its shared wall's shape.
/// </summary>
public sealed class EdgeSpec
{
    public required EdgeId Id { get; init; }
    public required bool Open { get; init; }
    public required IReadOnlyList<Gate> Gates { get; init; }
    public required uint WallSeed { get; init; }
}

/// <summary>A well: a disc where the cube above is void and the cube below open water, a sure way down.</summary>
public readonly record struct Well(Vector2 Center, float Radius);

/// <summary>A floor face's boundary conditions: its wells (none where either cube is outside the reef).</summary>
public sealed class FloorSpec
{
    public required FloorId Id { get; init; }
    public required IReadOnlyList<Well> Wells { get; init; }
}

/// <summary>A corner's rock: everything within Radius of it.</summary>
public readonly record struct CornerSpec(CornerId Id, float Radius);

/// <summary>
/// The cube world's plan (CUBE-WORLD-PLAN §4.1): from the seed alone, every edge's, corner's and floor's boundary
/// conditions, each from the seed and the id of the thing two or three cubes share, so any cube can be made by itself, in
/// any order, and always matches its neighbours and the cubes over and under it. The reef runs on every way; a run reads
/// it in a window of Window squares a side, outside which no cube is made and every edge is closed. Thread safe; it keeps
/// only the edges it has worked out.
/// </summary>
public sealed class CubeSpace
{
    public CubeSpace(ulong seed, int window = CubeTuning.Window, int layers = CubeTuning.Layers)
    {
        Seed = seed;
        Window = window;
        Layers = layers;
    }

    public ulong Seed { get; }
    public int Window { get; }
    public int Layers { get; }

    /// <summary>The square in the window's middle (where a run begins).</summary>
    public CellCoord Middle(int z) => new(Window / 2, Window / 2, z);

    /// <summary>A cube is in the window when both its squares are.</summary>
    public bool Inside(CubeId c) => InWindow(c.First) && InWindow(c.Second);

    /// <summary>A square is the reef's when its cube is in the window (a square of the window whose cube sticks out is rock).</summary>
    public bool Inside(CellCoord c) => Inside(CubeId.Of(c));

    bool InWindow(CellCoord c) => c.Z >= 0 && c.Z < Layers && c.X >= 0 && c.Y >= 0 && c.X < Window && c.Y < Window;

    /// <summary>Every cube of the window on layer <paramref name="z"/> (for tests and tools: the game makes only those she comes near).</summary>
    public IEnumerable<CubeId> CubesOf(int z) => CubesNear(new CellCoord(Window / 2, Window / 2, z), Window);

    /// <summary>The cubes of layer <paramref name="z"/> with a square within <paramref name="reach"/> squares of <paramref name="center"/> (inside the window).</summary>
    public IEnumerable<CubeId> CubesNear(CellCoord center, int reach)
    {
        var seen = new HashSet<CubeId>();
        for (int y = center.Y - reach; y <= center.Y + reach; y++)
        for (int x = center.X - reach; x <= center.X + reach; x++)
        {
            var c = CubeId.Of(new CellCoord(x, y, center.Z));
            if (Inside(c) && seen.Add(c)) yield return c;
        }
    }

    public uint NoiseSeed(int use) => ReefHash.NoiseSeed(Seed, use);

    /// <summary>The edges worked out so far (each is a function of the seed and its id alone, read many times a frame).</summary>
    readonly ConcurrentDictionary<EdgeId, EdgeSpec> _edges = new();

    public EdgeSpec Edge(EdgeId id) => _edges.GetOrAdd(id, MakeEdge);

    EdgeSpec MakeEdge(EdgeId id)
    {
        var rng = ReefHash.Rng(Seed, ReefHash.Edge, id.X, id.Y, id.Z, (int)id.Axis);
        // (Every draw made whatever the edge turns out to be, so each one means the same on every edge.)
        float extra = rng.NextFloat(), two = rng.NextFloat();
        float w1 = rng.Range(GateMinWidth, GateMaxWidth), w2 = rng.Range(GateMinWidth, GateMaxWidth);
        float a1 = rng.NextFloat(), a2 = rng.NextFloat();
        _ = rng.NextU64();
        // (Its wall's shape is its line's at every depth, read at the layer's depth: CubeTerrain.SharedNear.)
        uint wall = (uint)(ReefHash.Of(Seed, ReefHash.Edge, id.X, id.Y, -1, (int)id.Axis) >> 32);
        // (Between the two squares of one cube there is no edge to speak of: its water runs across.)
        bool inside = Inside(CubeId.Of(id.Low)) && Inside(CubeId.Of(id.High)) && CubeId.Of(id.Low) != CubeId.Of(id.High);
        // Open by its own chance; or, should a cube beside it be left with none, the one of that cube's edges likeliest to be.
        bool open = inside && (extra < EdgeOpen || Forced(CubeId.Of(id.Low)) == id || Forced(CubeId.Of(id.High)) == id);
        var gates = new List<Gate>();
        if (open)
        {
            float lo = GateMargin, hi = Side - GateMargin;
            if (two < TwoGates)
            {
                float mid = Side * 0.5f;
                gates.Add(new Gate(Between(lo + w1 / 2f, mid - 5f - w1 / 2f, a1), w1));
                gates.Add(new Gate(Between(mid + 5f + w2 / 2f, hi - w2 / 2f, a2), w2));
            }
            else gates.Add(new Gate(Between(lo + w1 / 2f, hi - w1 / 2f, a1), w1));
        }
        return new EdgeSpec { Id = id, Open = open, Gates = gates, WallSeed = wall };
    }

    /// <summary>A corner's rock: its size wandering slowly with depth, so a corner stands on down the layers as one pillar.</summary>
    public CornerSpec Corner(CornerId id)
    {
        uint seed = (uint)(ReefHash.Of(Seed, ReefHash.Corner, id.X, id.Y, -1) >> 32);
        float t = Math.Clamp(TerrainNoise.Perlin(id.Z * 0.17f, 0.5f, seed) * 0.7f + 0.5f, 0f, 1f);
        return new(id, CornerMin + (CornerMax - CornerMin) * t);
    }

    /// <summary>
    /// A stacked depth field at a global point of layer <paramref name="z"/> (CUBE-WORLD-PLAN §4.3): a noise map every
    /// StrataLayers layers, each layer a smooth blend of the four round it (a cubic B-spline down the stack, kept to the
    /// maps' own spread), so layers next to each other read nearly the same and the field changes on down the reef.
    /// About −1 to 1.
    /// </summary>
    public float Strata(int use, float x, float y, int z, float scale, int octaves = 2) => StrataKeys(use, z).At(x, y, scale, octaves);

    /// <summary>The four stacked maps a layer reads and its weights on them (for reading many points of one layer).</summary>
    public StrataSlice StrataKeys(int use, int z)
    {
        float depth = Math.Max(z, 0) / (float)StrataLayers;
        int k = (int)MathF.Floor(depth);
        float u = depth - k, u2 = u * u, u3 = u2 * u;
        float w0 = (1f - u) * (1f - u) * (1f - u) / 6f, w1 = (3f * u3 - 6f * u2 + 4f) / 6f, w2 = (-3f * u3 + 3f * u2 + 3f * u + 1f) / 6f, w3 = u3 / 6f;
        float norm = 1f / MathF.Sqrt(w0 * w0 + w1 * w1 + w2 * w2 + w3 * w3);
        return new StrataSlice(NoiseSeed(use * 64 + k), NoiseSeed(use * 64 + k + 1), NoiseSeed(use * 64 + k + 2), NoiseSeed(use * 64 + k + 3),
            w0 * norm, w1 * norm, w2 * norm, w3 * norm);
    }

    /// <summary>
    /// A floor's wells: one or two, in opposite quadrants of the cube, the pair of quadrants by the floor's parity (so the
    /// wells she arrives by and those she may leave by are never in the same quadrant, nor nearer than 2 · WellNear).
    /// </summary>
    public FloorSpec Floor(FloorId id)
    {
        if (!Inside(id.Upper) || !Inside(id.Lower)) return new FloorSpec { Id = id, Wells = Array.Empty<Well>() };
        var rng = ReefHash.Rng(Seed, ReefHash.Floor, id.X, id.Y, id.Z);
        var middle = id.Upper.Origin + new Vector2(Side * 0.5f);
        var quadrants = (id.Z & 1) == 0
            ? new[] { new Vector2(1f, -1f), new Vector2(-1f, 1f) }
            : new[] { new Vector2(-1f, -1f), new Vector2(1f, 1f) };
        var wells = new List<Well>();
        bool second = rng.NextFloat() < SecondWell;
        int first = rng.Int(2);
        for (int k = 0; k < 2; k++)
        {
            var q = quadrants[(first + k) % 2];
            var at = middle + q * new Vector2(rng.Range(WellNear, WellFar), rng.Range(WellNear, WellFar));
            float radius = rng.Range(WellMin, WellMax);
            if (k == 0 || second) wells.Add(new Well(at, radius));
        }
        return new FloorSpec { Id = id, Wells = wells };
    }

    static float Between(float lo, float hi, float t) => lo + (hi - lo) * t;

    /// <summary>An edge's own chance of being open (Edge's first draw).</summary>
    float Chance(EdgeId id) => ReefHash.Rng(Seed, ReefHash.Edge, id.X, id.Y, id.Z, (int)id.Axis).NextFloat();

    /// <summary>
    /// Should none of a cube's edges (to cubes in the window) be open by its own chance, the one nearest to being: so no
    /// cube is ever shut in. Null: it has one open already, or none to open.
    /// </summary>
    EdgeId? Forced(CubeId cube)
    {
        if (!Inside(cube)) return null;
        EdgeId? best = null;
        float least = float.MaxValue;
        foreach (var (edge, _, across) in cube.Outer())
        {
            if (!Inside(CubeId.Of(across))) continue;
            float chance = Chance(edge);
            if (chance < EdgeOpen) return null;
            if (chance < least)
            {
                least = chance;
                best = edge;
            }
        }
        return best;
    }
}

/// <summary>A layer's slice of a stacked field: its four maps' seeds and its weights on them (their squares summing to 1).</summary>
public readonly record struct StrataSlice(uint S0, uint S1, uint S2, uint S3, float W0, float W1, float W2, float W3)
{
    public float At(float x, float y, float scale, int octaves = 2)
    {
        x *= scale;
        y *= scale;
        float v = W0 * TerrainNoise.Fbm(x, y, S0, octaves) + W1 * TerrainNoise.Fbm(x, y, S1, octaves) + W2 * TerrainNoise.Fbm(x, y, S2, octaves);
        return W3 > 0f ? v + W3 * TerrainNoise.Fbm(x, y, S3, octaves) : v;
    }
}
