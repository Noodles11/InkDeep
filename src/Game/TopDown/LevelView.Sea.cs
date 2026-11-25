using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// An open level's ground (OpenSea): no ground of its own, only the sea's chunks, each one's mesh made off the main thread
/// (<see cref="SeaSurface"/>) and shown when it arrives (<see cref="ShowSeaChunk"/>), all under one node that sits where
/// the sea's window puts them (<see cref="SetSeaOrigin"/>).
/// </summary>
public partial class LevelView
{
    Node3D? _seaRoot;
    readonly HashSet<(int X, int Y)> _seaShown = new();

    /// <summary>A chunk's ground, in its own coordinates (its corner at 0): its padding read for the spline and the normals at its edge, so neighbouring chunks meet without a seam. Pure: any thread.</summary>
    internal static LevelShape.Surface? SeaSurface(OpenSea.Chunk c)
    {
        // (The sampler clamps both axes to one size: the longer side's, each axis clamped again to its own by Grid.)
        int size = System.Math.Max(c.Stride, c.Rows) - 1;
        float Grid(int i, int j) => c.Heights[System.Math.Clamp(j, 0, c.Rows - 1) * c.Stride + System.Math.Clamp(i, 0, c.Stride - 1)];
        return SurfaceMesh(size, (x, y) => SmoothSample(Grid, x + OpenSea.Pad, y + OpenSea.Pad, size), 0, 0, (int)(c.Max.X - c.Min.X), (int)(c.Max.Y - c.Min.Y), null);
    }

    void ShowSea(LevelShape shape)
    {
        _seaRoot = new Node3D();
        AddChild(_seaRoot);
        _seaShown.Clear();
        SetSeaOrigin(shape.SeaOrigin);
        foreach (var (chunk, ground) in shape.SeaChunks) ShowSeaChunk(chunk, ground);
    }

    public bool HasSeaChunk((int X, int Y) k) => _seaShown.Contains(k);

    /// <summary>A chunk of the sea shown (once).</summary>
    public void ShowSeaChunk(OpenSea.Chunk c, LevelShape.Surface? ground)
    {
        if (_seaRoot is null || !_seaShown.Add(c.Key) || ground is null) return;
        _seaRoot.AddChild(new MeshInstance3D { Mesh = ToMesh(ground), MaterialOverride = _ground, Position = new Vector3(c.Min.X, 0f, c.Min.Y) });
    }

    /// <summary>Where the sea's window lies (its corner, sea metres): the chunks are drawn in its coordinates.</summary>
    public void SetSeaOrigin(System.Numerics.Vector2 w)
    {
        if (_seaRoot is not null) _seaRoot.Position = new Vector3(-w.X, 0f, -w.Y);
    }
}
