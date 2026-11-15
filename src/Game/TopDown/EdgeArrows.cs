using System.Collections.Generic;
using Godot;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The places round her, shown as small emblems (in place of a minimap): a dark disc ringed in its place's colour with
/// its sign inked in the middle ("!" a leak cave, "+" a haven, "?" a secret...). An emblem stands on the place's
/// entrance while that is on the screen, and stays there; while it is off the screen, the emblem waits at the screen's
/// edge on the line from the middle toward it, a small pointer on its ring turned to it. As she swims toward a place
/// its emblem slides along the edge and then on in to its entrance, one motion. Changing layer (or riding into a
/// place), the emblems slide out past the edges as she goes, and the new place's slide back in from outside once she
/// has settled.
/// </summary>
public partial class EdgeArrows : Control
{
    /// <summary>
    /// How far in from the screen's edges an emblem waits, its radius, how far its pointer stands out, and how long they
    /// take to slide back in (after a moment's wait).
    /// </summary>
    const float Inset = 34f, Radius = 16f, Pointer = 8f, SlideIn = 0.55f, SettleWait = 0.35f;

    /// <summary>
    /// An emblem's size against Radius: smaller waiting at the edge, bigger on its place; and how long it takes to grow
    /// or shrink between them (seconds, eased in and out).
    /// </summary>
    const float EdgeSize = 2f / 3f, OnSize = 4f / 3f, Grow = 0.3f;

    /// <summary>Each place's way from its edge size (0) to its size on it (1), by where it is.</summary>
    readonly Dictionary<System.Numerics.Vector2, float> _grown = new();

    /// <summary>
    /// She is at a place once within this many of a full-size emblem's radii of it on the screen: its emblem fades away
    /// (over Fade seconds), so it never covers what lies there (a shop's wares), and back as she leaves.
    /// </summary>
    const float Near = 3f, Fade = 0.25f;
    readonly Dictionary<System.Numerics.Vector2, float> _shownBy = new();

    /// <summary>Two places nearer than this (metres) share one emblem (the first: a request's mark over a mouth's own).</summary>
    const float Same = 1.5f;

    static readonly Color Disc = new(0.03f, 0.045f, 0.07f, 0.62f), Ink = new(0.03f, 0.045f, 0.07f, 0.9f);

    readonly List<(System.Numerics.Vector2 At, Color Colour, string Sign)> _targets = new();
    readonly List<(Vector2 At, Vector2 Way, Color Colour, string Sign, bool Off, float Size, float Alpha)> _shown = new();
    /// <summary>0: in place; 1: slid out past the edges.</summary>
    float _out;
    /// <summary>Since she settled (−1: not sliding in).</summary>
    float _settled = -1f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>The places to show (world metres on her plane, each one's colour and sign).</summary>
    public void SetTargets(IEnumerable<(System.Numerics.Vector2 At, Color Colour, string Sign)> targets)
    {
        _targets.Clear();
        foreach (var t in targets)
        {
            bool dup = false;
            foreach (var u in _targets)
                if (System.Numerics.Vector2.Distance(u.At, t.At) < Same) { dup = true; break; }
            if (!dup) _targets.Add(t);
        }
    }

    /// <summary>Leaving (0 → 1 as she goes): the emblems slide out past the edges.</summary>
    public void DiveOut(float k)
    {
        _out = Mathf.Clamp(k, 0f, 1f);
        _settled = -1f;
    }

    /// <summary>Settled where she went (its places set): after a moment the emblems slide in from outside.</summary>
    public void Arrive()
    {
        _out = 1f;
        _settled = 0f;
    }

    /// <summary>Each frame: where each emblem stands, by the camera (the screen's own scale is <paramref name="scale"/>).</summary>
    public void Sync(Camera3D camera, float scale, float dt, Vector3? her = null)
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
        Vector2? herAt = her is { } h && !camera.IsPositionBehind(h) ? Fisheye.Shown(camera.UnprojectPosition(h), view) / scale : null;
        foreach (var (at, colour, sign) in _targets)
        {
            var world = new Vector3(at.X, 0.7f, at.Y);
            if (camera.IsPositionBehind(world)) continue;
            var screen = Fisheye.Shown(camera.UnprojectPosition(world), view) / scale;
            var way = screen - middle;
            if (way.LengthSquared() < 1f) way = Vector2.Down;
            way = way.Normalized();
            bool off = screen.X < lo.X || screen.Y < lo.Y || screen.X > hi.X || screen.Y > hi.Y;
            var place = screen;
            if (off)
            {
                // Where the line from the middle toward it meets the inset frame.
                var d = screen - middle;
                float tx = d.X > 0f ? (hi.X - middle.X) / d.X : d.X < 0f ? (lo.X - middle.X) / d.X : float.MaxValue;
                float ty = d.Y > 0f ? (hi.Y - middle.Y) / d.Y : d.Y < 0f ? (lo.Y - middle.Y) / d.Y : float.MaxValue;
                place = middle + d * Mathf.Min(1f, Mathf.Min(tx, ty));
            }
            // Sliding: out along its way, past the screen's edge.
            // (One on the screen goes out the further: from where it stands on to the edge along its way.)
            float toEdge = 0f;
            if (!off)
            {
                float ex = way.X > 0f ? (hi.X - place.X) / way.X : way.X < 0f ? (lo.X - place.X) / way.X : float.MaxValue;
                float ey = way.Y > 0f ? (hi.Y - place.Y) / way.Y : way.Y < 0f ? (lo.Y - place.Y) / way.Y : float.MaxValue;
                toEdge = Mathf.Max(0f, Mathf.Min(ex, ey));
            }
            place += way * _out * (toEdge + Inset + Radius * 2f + Pointer + 8f);
            // Growing as its place comes on the screen, shrinking as it leaves.
            _grown.TryGetValue(at, out float grown);
            grown = Mathf.MoveToward(grown, off ? 0f : 1f, dt / Grow);
            _grown[at] = grown;
            float grownSize = Mathf.Lerp(EdgeSize, OnSize, Mathf.SmoothStep(0f, 1f, grown));
            // At it: gone (eased).
            bool at_ = !off && herAt is { } me && me.DistanceTo(screen) < Near * Radius * OnSize;
            _shownBy.TryGetValue(at, out float alpha);
            if (!_shownBy.ContainsKey(at)) alpha = 1f;
            alpha = Mathf.MoveToward(alpha, at_ ? 0f : 1f, dt / Fade);
            _shownBy[at] = alpha;
            if (alpha > 0.001f) _shown.Add((place, way, colour, sign, off, grownSize, Mathf.SmoothStep(0f, 1f, alpha)));
        }
        // (Places gone forgotten.)
        if (_grown.Count > _targets.Count)
            foreach (var key in new List<System.Numerics.Vector2>(_grown.Keys))
                if (!_targets.Exists(t => t.At == key)) { _grown.Remove(key); _shownBy.Remove(key); }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        const int fontSize = 22;
        foreach (var (place, way, tint, sign, off, size, alpha) in _shown)
        {
            // (Faded as she reaches it.)
            var colour = tint with { A = tint.A * alpha };
            var disc = Disc with { A = Disc.A * alpha };
            var ink = Ink with { A = Ink.A * alpha };
            // (Drawn about its middle at its size.)
            DrawSetTransform(place, 0f, new Vector2(size, size));
            var at = Vector2.Zero;
            // Off the screen: a small pointer on its ring, turned to it.
            if (off)
            {
                var side = new Vector2(-way.Y, way.X);
                var tip = at + way * (Radius + Pointer);
                var points = new[] { tip, at + way * (Radius - 1f) + side * 7f, at + way * (Radius - 1f) - side * 7f };
                DrawColoredPolygon(points, colour);
                DrawPolyline(new[] { points[0], points[1], points[2], points[0] }, ink, 1.5f, true);
            }
            DrawCircle(at, Radius, disc);
            DrawArc(at, Radius + 1.5f, 0f, Mathf.Tau, 40, ink, 1.8f, true);
            DrawArc(at, Radius - 0.6f, 0f, Mathf.Tau, 40, colour, 2.8f, true);
            // Its sign, inked in its colour.
            if (sign == ".") DrawCircle(at, 4.8f, colour);
            else
            {
                float ascent = font.GetAscent(fontSize), descent = font.GetDescent(fontSize);
                var pos = new Vector2(at.X - Radius, at.Y + (ascent - descent) * 0.5f - 0.5f);
                DrawStringOutline(font, pos, sign, HorizontalAlignment.Center, Radius * 2f, fontSize, 3, Ink);
                DrawString(font, pos, sign, HorizontalAlignment.Center, Radius * 2f, fontSize, colour);
            }
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
