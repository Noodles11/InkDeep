using System;
using System.Numerics;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// The cube world's fixed numbers (docs/proposals/CUBE-WORLD-PLAN.md §2–§4). Distances are metres on the 1 m grid.
/// </summary>
public static class CubeTuning
{
    /// <summary>A cube's side: every cube is Side × Side and one layer thick.</summary>
    public const int Side = 64;
    /// <summary>A layer's cubes a side (the reef's footprint), and how many layers it goes down.</summary>
    public const int Footprint = 9, Layers = 36;
    /// <summary>The samples of its neighbours a cube's arrays carry round it.</summary>
    public const int Apron = 1;

    /// <summary>How far either side of an edge a gate's throat is written from the edge alone.</summary>
    public const float ThroatReach = 10f;
    /// <summary>
    /// An edge's shared wall: it always covers WallCore either side of the line (so a cube's apron, and the rim pass
    /// round it, are made from the edge alone), its line wandering up to WallShift and swelling by up to WallSwell.
    /// </summary>
    public const float WallCore = 3.05f, WallShift = 1f, WallSwell = 1.5f;
    public const float WallMax = WallCore + 2f * WallShift + WallSwell;
    /// <summary>A gate's edge is never nearer a corner than this; its width.</summary>
    public const float GateMargin = 13f, GateMinWidth = 6f, GateMaxWidth = 13f;
    /// <summary>Edges open beyond each layer's spanning tree, and edges with two gates.</summary>
    public const float EdgeOpenExtra = 0.35f, TwoGates = 0.35f;
    /// <summary>The rock round a corner.</summary>
    public const float CornerMin = 8f, CornerMax = 10f;
    /// <summary>A well's radius; how far it lies from the cube's middle along each axis (into its quadrant); a second well's chance.</summary>
    public const float WellMin = 5f, WellMax = 7f, WellNear = 9f, WellFar = 15f, SecondWell = 0.5f;

    /// <summary>Heights: a shelf's floor, and the void (water with nothing under it, the layer below showing through).</summary>
    public const float ShelfHeight = -1.1f, VoidHeight = -8f;
    /// <summary>The share of open water that is shelf rather than void, set by the shelf noise's cut.</summary>
    public const float ShelfCut = 0.1f;
}

/// <summary>A cube: X east, Y south (the map's axes), Z the layer (0 at the top, growing downward).</summary>
public readonly record struct CubeCoord(int X, int Y, int Z)
{
    /// <summary>Its north-west corner, in global metres.</summary>
    public Vector2 Origin => new(X * CubeTuning.Side, Y * CubeTuning.Side);

    /// <summary>The cube of layer <paramref name="z"/> a global point lies in.</summary>
    public static CubeCoord Of(Vector2 p, int z) =>
        new((int)MathF.Floor(p.X / CubeTuning.Side), (int)MathF.Floor(p.Y / CubeTuning.Side), z);

    public EdgeId West => new(X - 1, Y, Z, EdgeAxis.X);
    public EdgeId East => new(X, Y, Z, EdgeAxis.X);
    public EdgeId North => new(X, Y - 1, Z, EdgeAxis.Y);
    public EdgeId South => new(X, Y, Z, EdgeAxis.Y);
    /// <summary>The floor face under it, and the one over it.</summary>
    public FloorId Under => new(X, Y, Z);
    public FloorId Over => new(X, Y, Z - 1);
    /// <summary>Its corner at (dx, dy) of (0 or 1, 0 or 1).</summary>
    public CornerId Corner(int dx, int dy) => new(X + dx, Y + dy, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
}

/// <summary>Which way an edge's line runs across: X, a line of constant x (between east-west neighbours); Y, of constant y.</summary>
public enum EdgeAxis : byte { X, Y }

/// <summary>
/// The face two horizontal neighbours share, named from its lower-coordinate cube (X, Y), so both name it the same.
/// Along: metres along it from its start; across: signed metres from its line, positive into the higher cube.
/// </summary>
public readonly record struct EdgeId(int X, int Y, int Z, EdgeAxis Axis)
{
    public float Line => Axis == EdgeAxis.X ? (X + 1) * CubeTuning.Side : (Y + 1) * CubeTuning.Side;
    public float Start => Axis == EdgeAxis.X ? Y * CubeTuning.Side : X * CubeTuning.Side;
    public CubeCoord Low => new(X, Y, Z);
    public CubeCoord High => Axis == EdgeAxis.X ? new(X + 1, Y, Z) : new(X, Y + 1, Z);
    public float Across(Vector2 g) => Axis == EdgeAxis.X ? g.X - Line : g.Y - Line;
    public float Along(Vector2 g) => Axis == EdgeAxis.X ? g.Y - Start : g.X - Start;
    /// <summary>The global coordinate along its line (for noise every cube reads the same).</summary>
    public float AlongGlobal(Vector2 g) => Axis == EdgeAxis.X ? g.Y : g.X;
    public Vector2 Point(float along, float across) =>
        Axis == EdgeAxis.X ? new(Line + across, Start + along) : new(Start + along, Line + across);
}

/// <summary>The vertical line four cubes of a layer meet on, at global (X·Side, Y·Side).</summary>
public readonly record struct CornerId(int X, int Y, int Z)
{
    public Vector2 Point => new(X * CubeTuning.Side, Y * CubeTuning.Side);
}

/// <summary>The face between cube (X, Y, Z) and the one under it, (X, Y, Z + 1).</summary>
public readonly record struct FloorId(int X, int Y, int Z)
{
    public CubeCoord Upper => new(X, Y, Z);
    public CubeCoord Lower => new(X, Y, Z + 1);
}

/// <summary>
/// Hashes of the world seed and an id: every choice of the cube world is one of these, so it never depends on what was
/// made before it, nor on which thread.
/// </summary>
public static class ReefHash
{
    public const ulong Edge = 1, Corner = 2, Floor = 3, Cube = 4, Layer = 5, Noise = 6;

    public static ulong Of(ulong seed, ulong salt, int a, int b = 0, int c = 0, int d = 0)
    {
        ulong h = Step(seed ^ (salt * 0x9E3779B97F4A7C15UL));
        h = Step(h ^ (uint)a);
        h = Step(h ^ ((ulong)(uint)b << 21));
        h = Step(h ^ ((ulong)(uint)c << 42));
        return Step(h ^ (uint)d);
    }

    public static Rng Rng(ulong seed, ulong salt, int a, int b = 0, int c = 0, int d = 0) => new(Of(seed, salt, a, b, c, d));

    /// <summary>A noise field's seed: one per use, the same for every cube.</summary>
    public static uint NoiseSeed(ulong seed, int use) => (uint)(Of(seed, Noise, use) >> 32);

    static ulong Step(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
