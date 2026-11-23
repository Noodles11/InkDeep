using System.Collections.Generic;
using Godot;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The places round her, shown as small coloured arrows at the edges of the screen (in place of a minimap): each place
/// off the screen has an arrow on the edge between the screen's middle and it, pointing at it, in its place's colour;
/// one on the screen has none. Changing layer (or riding into a place), the arrows slide out past the edges as she goes,
/// and the new place's slide back in from outside once she has settled.
/// </summary>
public partial class EdgeArrows : Control
{
    /// <summary>How far in from the screen's edges the arrows stand, how long one is, and how long they take to slide back in (after a moment's wait).</summary>
    const float Inset = 30f, Length = 19f, SlideIn = 0.55f, SettleWait = 0.35f;

    readonly List<(System.Numerics.Vector2 At, Color Colour)> _targets = new();
    readonly List<(Vector2 At, Vector2 Way, Color Colour)> _shown = new();
    /// <summary>0: in place; 1: slid out past the edges.</summary>
    float _out;
    /// <summary>Since she settled (−1: not sliding in).</summary>
    float _settled = -1f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>The places to point at (world metres on her plane, and each one's colour).</summary>
    public void SetTargets(IEnumerable<(System.Numerics.Vector2 At, Color Colour)> targets)
    {
        _targets.Clear();
        _targets.AddRange(targets);
    }

    /// <summary>Leaving (0 → 1 as she goes): the arrows slide out past the edges.</summary>
    public void DiveOut(float k)
    {
        _out = Mathf.Clamp(k, 0f, 1f);
        _settled = -1f;
    }

    /// <summary>Settled where she went (its places set): after a moment the arrows slide in from outside.</summary>
    public void Arrive()
    {
        _out = 1f;
        _settled = 0f;
    }

    /// <summary>Each frame: where each arrow stands, by the camera (the screen's own scale is <paramref name="scale"/>).</summary>
    public void Sync(Camera3D camera, float scale, float dt)
    {
        if (_settled >= 0f)
        {
            _settled += dt;
            float k = Mathf.Clamp((_settled - SettleWait) / SlideIn, 0f, 1f);
            _out = 1f - Mathf.SmoothStep(0f, 1f, k);
            if (k >= 1f) _settled = -1f;
        }
        _shown.Clear();
        var size = Size;
        var view = GetViewportRect().Size;
        var middle = size * 0.5f;
        var lo = new Vector2(Inset, Inset);
        var hi = size - lo;
        foreach (var (at, colour) in _targets)
        {
            var world = new Vector3(at.X, 0.7f, at.Y);
            if (camera.IsPositionBehind(world)) continue;
            var screen = Fisheye.Shown(camera.UnprojectPosition(world), view) / scale;
            // On the screen: no arrow.
            if (screen.X > lo.X && screen.Y > lo.Y && screen.X < hi.X && screen.Y < hi.Y) continue;
            var way = screen - middle;
            if (way.LengthSquared() < 1f) continue;
            way = way.Normalized();
            // Where the line from the middle toward it meets the inset frame.
            float tx = way.X > 0f ? (hi.X - middle.X) / way.X : way.X < 0f ? (lo.X - middle.X) / way.X : float.MaxValue;
            float ty = way.Y > 0f ? (hi.Y - middle.Y) / way.Y : way.Y < 0f ? (lo.Y - middle.Y) / way.Y : float.MaxValue;
            var edge = middle + way * Mathf.Min(tx, ty);
            // Sliding: out along its way, past the screen's edge.
            edge += way * _out * (Inset + Length * 2f + 8f);
            _shown.Add((edge, way, colour));
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var (at, way, colour) in _shown)
        {
            var side = new Vector2(-way.Y, way.X);
            var tip = at + way * Length * 0.5f;
            var back = at - way * Length * 0.5f;
            var points = new[] { tip, back + side * Length * 0.45f, back + way * Length * 0.18f, back - side * Length * 0.45f };
            var outline = new[] { tip, back + side * Length * 0.45f, back + way * Length * 0.18f, back - side * Length * 0.45f, tip };
            DrawColoredPolygon(points, colour);
            DrawPolyline(outline, new Color(0.04f, 0.06f, 0.09f, 0.85f), 2f, true);
        }
    }
}
