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
        _last = null;
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
            _last = null;
        }
        return changed;
    }

    /// <summary>The cube holding a grid cell (by its first corner), or null outside the window.</summary>
    CubeTerrain? CubeOf(int gx, int gy)
    {
        int i = FloorDiv(gx, Side) - Center.X + _reach, j = FloorDiv(gy, Side) - Center.Y + _reach;
        return i < 0 || j < 0 || i >= _span || j >= _span ? null : _slots[j * _span + i];
    }

    /// <summary>
    /// The cube the last read fell in, and its own squares' extent (global metres): reads come in clusters (a body's nine
    /// round one point), so most need no look-up at all. Which cube holds a cell never changes with the window: it is the
    /// cube of the cell's square, whichever window slot gave it.
    /// </summary>
    CubeTerrain? _last;
    int _lastX0, _lastY0, _lastX1, _lastY1;

    public float HeightAt(Vector2 p)
    {
        int ix = (int)MathF.Floor(p.X), iy = (int)MathF.Floor(p.Y);
        CubeTerrain t;
        if (_last is { } last && ix >= _lastX0 && iy >= _lastY0 && ix < _lastX1 && iy < _lastY1) t = last;
        else
        {
            if (CubeOf(ix, iy) is not { } found) return Missing;
            t = found;
            _last = t;
            _lastX0 = t.Cube.X * Side;
            _lastY0 = t.Cube.Y * Side;
            _lastX1 = _lastX0 + t.Cube.Wide * Side;
            _lastY1 = _lastY0 + t.Cube.Tall * Side;
        }
        float tx = p.X - ix, ty = p.Y - iy;
        int n = t.Stride, k = (iy - t.Y0) * n + (ix - t.X0);
        var h = t.Heights;
        float a = h[k] + (h[k + 1] - h[k]) * tx;
        float b = h[k + n] + (h[k + n + 1] - h[k + n]) * tx;
        return a + (b - a) * ty;
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
