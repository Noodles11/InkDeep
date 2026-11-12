using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A corruption ring round each corrupted creature (docs/CORRUPTION.md, "Corruption rings"): how much of its corruption is
/// left, in the corruption's own look and the same language as her own meter on the minimap's bezel — dark glossy ink
/// over a faint gold track, filling clockwise from 12 o'clock, a violet sheen on its lip. As the creature is cleansed
/// the ink draws back round the ring and thins, so a nearly freed creature wears only a short, fine sliver. No green or
/// red: ink is corruption, gold is her light. Drawn only while the creature can be seen: a murkling only where her light
/// (or a charge stone's, or a sealed leak's wave) shows it (BlightView.Shown), the others while they are on the screen.
/// Drawn on the screen over the reef, under the HUD.
/// </summary>
public partial class HealthBars : Control
{
    /// <summary>The ring's gap outside the creature's edge (px), its ink's width full and nearly empty (px, at 1080 lines).</summary>
    const float Gap = 5f, WidthFull = 3.2f, WidthLow = 1.4f;
    /// <summary>A ring never drawn smaller than this radius (px), so a small creature's still reads.</summary>
    const float Smallest = 12f;
    static readonly Color Track = new(1f, 0.86f, 0.55f, 0.22f), Ink = new(0.03f, 0.02f, 0.06f, 0.92f);
    static readonly Color Sheen = new(0.62f, 0.45f, 0.95f, 0.75f), Edge = new(0.85f, 0.8f, 1f, 0.28f);

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

    public override void _Draw()
    {
        if (_world is not { } w || _camera is not { } cam || _blight is not { } blight) return;
        float scale = Mathf.Clamp(GetViewportRect().Size.Y / 1080f, 0.7f, 2f);
        // (Murklings wear no ring: a budded one shows its corruption as its size, PlaneMurkSize.cs.)
        foreach (var r in w.Blightroots)
            if (r.Alive) Ring(cam, r.Position, BlightTuning.SacRadius, r.Corruption, scale, 1.4f);
        foreach (var b in w.Blotters)
            if (b.Alive) Ring(cam, b.Position, BlotterTuning.Radius, b.Corruption, scale);
        foreach (var t in w.Thumpsquirts)
            if (t.Alive) Ring(cam, t.Position, ThumpTuning.Radius, t.Corruption, scale);
        foreach (var i in w.Inkwardens)
            if (i.Alive) Ring(cam, i.Position, WardenTuning.Radius, i.Corruption, scale);
        foreach (var l in w.Lashjaws)
            if (l.Alive) Ring(cam, l.Position, 0.5f, l.Corruption, scale);
        foreach (var f in w.Flounders)
            if (f.Corrupted && !f.Freed && !f.Gone) Ring(cam, f.Position, FlounderTuning.Radius, f.Corruption, scale, -0.6f);
    }

    /// <summary>One creature's ring, round it on the screen (<paramref name="lift"/>: how far above her plane it is, m).</summary>
    void Ring(Camera3D cam, System.Numerics.Vector2 at, float radius, CorruptionMeter meter, float scale, float lift = 0f)
    {
        float left = Mathf.Clamp(meter.Fraction, 0f, 1f);
        // (Emptied: it is being freed, no ring.)
        if (left <= 0f) return;
        var centre = new Vector3(at.X, LevelMap.SwimBand + lift, at.Y);
        if (cam.IsPositionBehind(centre)) return;
        var p = cam.UnprojectPosition(centre);
        var size = GetViewportRect().Size;
        if (p.X < -80f || p.Y < -80f || p.X > size.X + 80f || p.Y > size.Y + 80f) return;
        // Its size on the screen: its radius across, seen from here, and the ring just outside it.
        float across = (cam.UnprojectPosition(centre + cam.GlobalBasis.X * radius) - p).Length();
        float r = Mathf.Max(across + Gap * scale, Smallest * scale);
        float width = Mathf.Lerp(WidthLow, WidthFull, left) * scale;
        int points = Mathf.Clamp((int)(r * 0.8f), 24, 72);
        // The track: the whole ring, faint gold (her light's), with a pale edge so the ink reads on the dark water.
        DrawArc(p, r, 0f, Mathf.Tau, points, Track, Mathf.Max(1f, 1.2f * scale), true);
        // The ink: from 12 o'clock, clockwise (on the screen, angles grow clockwise), as long as the meter is full.
        float from = -Mathf.Pi * 0.5f, to = from + Mathf.Tau * left;
        int n = Mathf.Max(4, (int)(points * left));
        DrawArc(p, r, from, to, n, Edge, width + 1.6f * scale, true);
        DrawArc(p, r, from, to, n, Ink, width, true);
        // The violet sheen on its lip, as on her own meter.
        var lip = p + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * r;
        DrawCircle(lip, width * 0.75f, Sheen);
    }
}
