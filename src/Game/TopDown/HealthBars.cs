using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A thin bar over each corrupted creature (docs/CORRUPTION.md, "Corruption meters"): how much of its corruption is left,
/// green while most is, yellow at about half, red when little is. Drawn only while the creature can be seen: a murkling
/// only where her light (or a charge stone's, or a sealed leak's wave) shows it (BlightView.Shown), the others while
/// they are on the screen. Drawn on the screen over the reef, under the HUD.
/// </summary>
public partial class HealthBars : Control
{
    /// <summary>A bar's height (px), and its width per metre of the creature's radius, at least and at most (px, at 1080 lines).</summary>
    const float Height = 4f, PerMetre = 46f, Narrowest = 34f, Widest = 72f;
    /// <summary>The share of its meter left at which a bar turns yellow, and red.</summary>
    const float Yellow = 0.6f, Red = 0.3f;
    static readonly Color Green = new(0.36f, 0.9f, 0.42f), Amber = new(1f, 0.84f, 0.25f), Low = new(1f, 0.3f, 0.25f);
    static readonly Color Back = new(0.02f, 0.02f, 0.05f, 0.7f);

    PlaneWorld? _world;
    Camera3D? _camera;
    BlightView? _blight;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public void Sync(PlaneWorld world, Camera3D camera, BlightView blight)
    {
        _world = world;
        _camera = camera;
        _blight = blight;
        QueueRedraw();
    }

    /// <summary>The bar's colour for this share of its meter left.</summary>
    public static Color ColourOf(float left) => left > Yellow ? Green : left > Red ? Amber : Low;

    public override void _Draw()
    {
        if (_world is not { } w || _camera is not { } cam || _blight is not { } blight) return;
        float scale = Mathf.Clamp(GetViewportRect().Size.Y / 1080f, 0.7f, 2f);
        foreach (var m in w.Murklings)
            if (m.Alive && blight.Shown(m)) Bar(cam, m.Position, m.Radius, m.Corruption, scale);
        foreach (var r in w.Blightroots)
            if (r.Alive) Bar(cam, r.Position, BlightTuning.SacRadius, r.Corruption, scale, 1.4f);
        foreach (var b in w.Blotters)
            if (b.Alive) Bar(cam, b.Position, BlotterTuning.Radius, b.Corruption, scale);
        foreach (var t in w.Thumpsquirts)
            if (t.Alive) Bar(cam, t.Position, ThumpTuning.Radius, t.Corruption, scale);
        foreach (var i in w.Inkwardens)
            if (i.Alive) Bar(cam, i.Position, WardenTuning.Radius, i.Corruption, scale);
        foreach (var l in w.Lashjaws)
            if (l.Alive) Bar(cam, l.Position, 0.5f, l.Corruption, scale);
        foreach (var f in w.Flounders)
            if (f.Corrupted && !f.Freed && !f.Gone) Bar(cam, f.Position, FlounderTuning.Radius, f.Corruption, scale, -0.6f);
    }

    /// <summary>One creature's bar, just above it on the screen (<paramref name="lift"/>: how far above her plane it is, m).</summary>
    void Bar(Camera3D cam, System.Numerics.Vector2 at, float radius, CorruptionMeter meter, float scale, float lift = 0f)
    {
        var centre = new Vector3(at.X, LevelMap.SwimBand + lift, at.Y);
        if (cam.IsPositionBehind(centre)) return;
        var p = cam.UnprojectPosition(centre);
        // Its size on the screen: its radius across, seen from here.
        float across = (cam.UnprojectPosition(centre + cam.GlobalBasis.X * radius) - p).Length();
        var size = GetViewportRect().Size;
        if (p.X < -80f || p.Y < -80f || p.X > size.X + 80f || p.Y > size.Y + 80f) return;
        float width = Mathf.Clamp(radius * PerMetre, Narrowest, Widest) * scale;
        float height = Mathf.Max(2f, Height * scale);
        var top = new Vector2(p.X - width * 0.5f, p.Y - across - 10f * scale - height);
        float left = Mathf.Clamp(meter.Fraction, 0f, 1f);
        // (Emptied: it is being freed, no bar.)
        if (left <= 0f) return;
        DrawRect(new Rect2(top - Vector2.One, new Vector2(width + 2f, height + 2f)), Back);
        DrawRect(new Rect2(top, new Vector2(width * left, height)), ColourOf(left));
    }
}
