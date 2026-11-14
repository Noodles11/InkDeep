using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
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
/// conditions. Each layer's open edges hold a spanning tree of its cubes, so all of a layer joins up; the footprint's
/// rim is closed. Thread safe; the only thing kept is each layer's tree.
/// </summary>
public sealed class CubeSpace
{
    public CubeSpace(ulong seed, int footprint = CubeTuning.Footprint, int layers = CubeTuning.Layers)
    {
        Seed = seed;
        Footprint = footprint;
        Layers = layers;
    }

    public ulong Seed { get; }
    public int Footprint { get; }
    public int Layers { get; }

    public bool Inside(CubeCoord c) => c.X >= 0 && c.Y >= 0 && c.X < Footprint && c.Y < Footprint && c.Z >= 0 && c.Z < Layers;

    public uint NoiseSeed(int use) => ReefHash.NoiseSeed(Seed, use);

    public EdgeSpec Edge(EdgeId id)
    {
        var rng = ReefHash.Rng(Seed, ReefHash.Edge, id.X, id.Y, id.Z, (int)id.Axis);
        // (Every draw made whatever the edge turns out to be, so each one means the same on every edge.)
        float extra = rng.NextFloat(), two = rng.NextFloat();
        float w1 = rng.Range(GateMinWidth, GateMaxWidth), w2 = rng.Range(GateMinWidth, GateMaxWidth);
        float a1 = rng.NextFloat(), a2 = rng.NextFloat();
        uint wall = (uint)(rng.NextU64() >> 32);
        bool inside = Inside(id.Low) && Inside(id.High);
        bool open = inside && (TreeOf(id.Z).Contains(id) || extra < EdgeOpenExtra);
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

    public CornerSpec Corner(CornerId id) =>
        new(id, ReefHash.Rng(Seed, ReefHash.Corner, id.X, id.Y, id.Z).Range(CornerMin, CornerMax));

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

    readonly ConcurrentDictionary<int, HashSet<EdgeId>> _trees = new();

    /// <summary>A layer's spanning tree of its cubes (Kruskal over its edges in a seeded order).</summary>
    HashSet<EdgeId> TreeOf(int z) => _trees.GetOrAdd(z, layer =>
    {
        var edges = new List<EdgeId>();
        for (int y = 0; y < Footprint; y++)
        for (int x = 0; x < Footprint; x++)
        {
            if (x + 1 < Footprint) edges.Add(new EdgeId(x, y, layer, EdgeAxis.X));
            if (y + 1 < Footprint) edges.Add(new EdgeId(x, y, layer, EdgeAxis.Y));
        }
        var rng = ReefHash.Rng(Seed, ReefHash.Layer, layer);
        for (int i = edges.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (edges[i], edges[j]) = (edges[j], edges[i]);
        }
        var parent = new int[Footprint * Footprint];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i)
        {
            while (parent[i] != i) i = parent[i] = parent[parent[i]];
            return i;
        }
        var tree = new HashSet<EdgeId>();
        foreach (var e in edges)
        {
            int a = Find(e.Y * Footprint + e.X), b = Find(e.High.Y * Footprint + e.High.X);
            if (a == b) continue;
            parent[a] = b;
            tree.Add(e);
        }
        return tree;
    });
}
