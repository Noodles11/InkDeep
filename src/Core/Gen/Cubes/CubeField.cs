using System;
using System.Numerics;
using OctoShoots.Core.Gen;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// The cube world's rock as a body meets it (CUBE-WORLD-PLAN §5.3): a window of one layer's squares round the one she
/// is in, each read from the cube it belongs to, in global metres. A point is read from the cube its grid cell starts in
/// (neighbours agree on the samples they share, so which one holds a line's points makes no difference). Anything
/// outside the window, or in a cube not made, reads as rock.
/// </summary>
public sealed class CubeField : IReefField
{
    /// <summary>The height of rock read where there is no cube.</summary>
    public const float Missing = 2f;

    readonly Func<CubeId, CubeTerrain?> _terrain;
    readonly int _reach, _span;
    readonly CubeTerrain?[] _slots;
    bool _filled;

    /// <param name="terrain">A layer's cubes (null: not made, or outside the reef).</param>
    /// <param name="reach">The window's squares either side of its middle one (1: the 3 × 3 round her, as wide as the view).</param>
    public CubeField(Func<CubeId, CubeTerrain?> terrain, CellCoord center, int reach = 1)
    {
        _terrain = terrain;
        _reach = reach;
        _span = 2 * reach + 1;
        _slots = new CubeTerrain?[_span * _span];
        Recenter(center);
    }

    /// <summary>The window's middle square.</summary>
    public CellCoord Center { get; private set; }

    /// <summary>How many times the window has moved (for tests and the overlay).</summary>
    public int Recentres { get; private set; }

    /// <summary>The window moved to have <paramref name="center"/> in its middle (nothing done if it already has).</summary>
    public void Recenter(CellCoord center)
    {
        if (_filled && center == Center) return;
        Center = center;
        for (int dy = -_reach; dy <= _reach; dy++)
        for (int dx = -_reach; dx <= _reach; dx++)
            _slots[(dy + _reach) * _span + dx + _reach] = _terrain(CubeId.Of(new CellCoord(center.X + dx, center.Y + dy, center.Z)));
        if (_filled) Recentres++;
        _filled = true;
    }

    /// <summary>The window's cubes read again (terrain that has come or gone since); true when any changed.</summary>
    public bool Refresh()
    {
        bool changed = false;
        for (int dy = -_reach; dy <= _reach; dy++)
        for (int dx = -_reach; dx <= _reach; dx++)
        {
            int k = (dy + _reach) * _span + dx + _reach;
            var now = _terrain(CubeId.Of(new CellCoord(Center.X + dx, Center.Y + dy, Center.Z)));
            if (ReferenceEquals(now, _slots[k])) continue;
            _slots[k] = now;
            changed = true;
        }
        return changed;
    }

    /// <summary>The cube holding a grid cell (by its first corner), or null outside the window.</summary>
    CubeTerrain? CubeOf(int gx, int gy)
    {
        int i = FloorDiv(gx, Side) - Center.X + _reach, j = FloorDiv(gy, Side) - Center.Y + _reach;
        return i < 0 || j < 0 || i >= _span || j >= _span ? null : _slots[j * _span + i];
    }

    public float HeightAt(Vector2 p)
    {
        int ix = (int)MathF.Floor(p.X), iy = (int)MathF.Floor(p.Y);
        if (CubeOf(ix, iy) is not { } t) return Missing;
        float tx = p.X - ix, ty = p.Y - iy;
        int k = t.Index(ix, iy), n = t.Stride;
        float a = MathUtil.Lerp(t.Heights[k], t.Heights[k + 1], tx);
        float b = MathUtil.Lerp(t.Heights[k + n], t.Heights[k + n + 1], tx);
        return MathUtil.Lerp(a, b, ty);
    }

    public bool IsOpen(Vector2 p) => HeightAt(p) <= 0f;

    public Vector2 Gradient(Vector2 p)
    {
        const float e = 0.5f;
        return new Vector2(
            HeightAt(p + new Vector2(e, 0f)) - HeightAt(p - new Vector2(e, 0f)),
            HeightAt(p + new Vector2(0f, e)) - HeightAt(p - new Vector2(0f, e))) / (2f * e);
    }

    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
