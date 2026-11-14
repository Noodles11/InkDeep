using System;
using System.Numerics;

namespace OctoShoots.Core.Gen;

/// <summary>
/// The rock a body swims among, in global metres (docs/proposals/CUBE-WORLD-PLAN.md §5.3): a classic level's one map, or
/// the cube world's loaded cubes. Terrain above the swim level (height 0) blocks; anything at or below it is water.
/// </summary>
public interface IReefField
{
    /// <summary>The terrain's height at a point (bilinear between the grid's samples).</summary>
    float HeightAt(Vector2 p);
    /// <summary>Whether the swim band is open at a point.</summary>
    bool IsOpen(Vector2 p);
    /// <summary>The terrain's uphill direction (not normalised).</summary>
    Vector2 Gradient(Vector2 p);
}

/// <summary>Collision against an <see cref="IReefField"/>'s rock, shared by every world that swims in one.</summary>
public static class ReefCollision
{
    /// <summary>True when a circle of the given radius at p sits wholly in the field's open water (its middle and 8 rim points).</summary>
    public static bool Clear(IReefField field, Vector2 p, float radius)
    {
        if (!field.IsOpen(p)) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8f;
            if (!field.IsOpen(p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius)) return false;
        }
        return true;
    }

    /// <summary>
    /// Toward the rock at p: where the rim of the circle meets high ground (a trench's downhill slope beside it must not
    /// count), else uphill.
    /// </summary>
    public static Vector2 WallNormal(IReefField field, Vector2 p, float radius)
    {
        Vector2 blocked = Vector2.Zero;
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.Tau / 16f;
            Vector2 d = new(MathF.Cos(a), MathF.Sin(a));
            if (!field.IsOpen(p + d * radius)) blocked += d;
        }
        return blocked.LengthSquared() > 1e-6f ? Normalize(blocked) : Normalize(field.Gradient(p));
    }

    static Vector2 Normalize(Vector2 v)
    {
        float l = v.Length();
        return l > 1e-6f ? v / l : Vector2.Zero;
    }
}
