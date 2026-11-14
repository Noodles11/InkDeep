using System;
using System.Numerics;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>Why she may not dive or climb where she is (None: she may).</summary>
public enum LayerBlock : byte
{
    None,
    /// <summary>No way through here: not void over open water (CUBE-WORLD-PLAN §3).</summary>
    NoWay,
    /// <summary>The 30 s since her last change of layer have not run out.</summary>
    Cooldown,
    /// <summary>Too near where she arrived (no diving straight on through a well under the one she came by).</summary>
    TooNear,
    /// <summary>Climbing: she has climbed already (only one layer back above her deepest).</summary>
    NotDeepest,
    /// <summary>The reef's top or bottom.</summary>
    Edge,
    /// <summary>The cubes on either side are not in hand yet.</summary>
    NotReady,
}

/// <summary>
/// Diving and climbing between layers (CUBE-WORLD-PLAN §7): where she may (§3's rule: void here over open water there,
/// for her whole body), the cooldown after any change of layer, no diving within half a square of where she arrived
/// (less in a cube too small for that), and climbing only from the deepest layer she has reached.
/// </summary>
public sealed class CubeDiving
{
    /// <summary>Seconds after a change of layer before the next, either way.</summary>
    public const float Cooldown = 30f;
    /// <summary>How far from where she arrived she must be to dive again (half a square), or this share of the farthest way down in the cube she arrived in, if less.</summary>
    public const float ChainReach = Side * 0.5f, ChainShare = 0.8f;

    public CubeDiving(int layers, float cooldown = Cooldown)
    {
        Layers = layers;
        CooldownSeconds = cooldown;
        Since = cooldown;
    }

    public int Layers { get; }
    public float CooldownSeconds { get; }
    public int Layer { get; private set; }
    public int Deepest { get; private set; }
    /// <summary>Seconds since her last change of layer.</summary>
    public float Since { get; private set; }
    public float CooldownLeft => MathF.Max(0f, CooldownSeconds - Since);
    /// <summary>Where she arrived on this layer (null on the first), and how far from it she must be to dive.</summary>
    public Vector2? Arrived { get; private set; }
    public float ChainLimit { get; private set; }

    /// <summary>She may climb from this layer at all (it is the deepest she has reached, and not the top).</summary>
    public bool Climbable => Layer == Deepest && Layer > 0;

    public void Tick(float dt) => Since += dt;

    /// <summary>Whether she may dive at <paramref name="p"/>, and if not why.</summary>
    public LayerBlock CanDive(Vector2 p, float radius, Func<CubeId, CubeTerrain?> terrain)
    {
        if (Layer + 1 >= Layers) return LayerBlock.Edge;
        if (terrain(CubeId.Of(p, Layer)) is not { } here || terrain(CubeId.Of(p, Layer + 1)) is not { } below) return LayerBlock.NotReady;
        if (!CubeTerrain.CanDive(here, below, p, radius)) return LayerBlock.NoWay;
        if (Arrived is { } at && Vector2.Distance(p, at) < ChainLimit) return LayerBlock.TooNear;
        return CooldownLeft > 0f ? LayerBlock.Cooldown : LayerBlock.None;
    }

    /// <summary>Whether she may climb at <paramref name="p"/>, and if not why.</summary>
    public LayerBlock CanClimb(Vector2 p, float radius, Func<CubeId, CubeTerrain?> terrain)
    {
        if (Layer == 0) return LayerBlock.Edge;
        if (!Climbable) return LayerBlock.NotDeepest;
        if (terrain(CubeId.Of(p, Layer - 1)) is not { } above || terrain(CubeId.Of(p, Layer)) is not { } here) return LayerBlock.NotReady;
        if (!CubeTerrain.CanDive(above, here, p, radius)) return LayerBlock.NoWay;
        return CooldownLeft > 0f ? LayerBlock.Cooldown : LayerBlock.None;
    }

    /// <summary>She has dived and arrived at <paramref name="p"/> in <paramref name="arrival"/> (its wells set how far she must go to dive again).</summary>
    public void Dived(Vector2 p, CubeTerrain arrival)
    {
        Layer++;
        Deepest = Math.Max(Deepest, Layer);
        Arrive(p, arrival);
    }

    /// <summary>A dive taken up from a save: on that layer, that deep, free to dive or climb (she waited in a haven).</summary>
    public void Restore(int layer, int deepest)
    {
        Layer = layer;
        Deepest = Math.Max(deepest, layer);
        Since = CooldownSeconds;
        Arrived = null;
        ChainLimit = 0f;
    }

    /// <summary>She has climbed and arrived at <paramref name="p"/>.</summary>
    public void Climbed(Vector2 p, CubeTerrain arrival)
    {
        Layer--;
        Arrive(p, arrival);
    }

    void Arrive(Vector2 p, CubeTerrain arrival)
    {
        Since = 0f;
        Arrived = p;
        // (A cube whose ways down all lie near where she came in: the reach shrinks so that she is never kept from them.)
        float farthest = 0f;
        foreach (var well in arrival.Down) farthest = MathF.Max(farthest, Vector2.Distance(p, well.Center));
        ChainLimit = arrival.Down.Count == 0 ? ChainReach : MathF.Min(ChainReach, ChainShare * farthest);
    }
}
