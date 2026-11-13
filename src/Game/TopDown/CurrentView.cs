using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The currents on screen (DESIGN-TOPDOWN §4.7): the moving water seen as a shimmer, like the air over a hot road seen
/// from afar. The level's currents are baked into a map over her plane (how far along its course each point lies, how far
/// off to its side, how strongly it shows, whose it is), and the post pass bends the finished picture by it, eddies of
/// denser water rolling down each course with the flow and boiling as they go (topdown_post.gdshader). No lines, no
/// edges, no colour, and no change to the water's brightness: patchy, softer past the channel, fading out at its sides and
/// ends. It wakes and dies with the current's strength.
/// </summary>
public partial class CurrentView : Node3D
{
    /// <summary>How far past the current's edge it is seen, and how far ahead of the water's own pace it rolls.</summary>
    const float Past = 5f, Pace = 1.6f;
    /// <summary>The map's cells a metre, and the most currents it holds (the post pass's array).</summary>
    const int PerMetre = 2, Most = 6;

    readonly Vector2[] _flows = new Vector2[Most];
    /// <summary>How far each current's water has rolled along its course (metres).</summary>
    readonly float[] _rolled = new float[Most];
    PlaneWorld? _world;
    ImageTexture? _map;

    /// <summary>A new level: nothing to show until it is baked.</summary>
    public void Clear(CameraRig camera)
    {
        _world = null;
        camera.SetCurrents(_map, _flows, 0);
    }

    public void Sync(PlaneWorld world, float dt, CameraRig camera)
    {
        if (world != _world) Bake(world);
        int count = Math.Min(world.Currents.Count, Most);
        for (int i = 0; i < count; i++)
        {
            var c = world.Currents[i];
            _rolled[i] += c.Speed * c.Strength * Pace * dt;
            _flows[i] = new Vector2(_rolled[i], c.Strength);
        }
        camera.SetCurrents(_map, _flows, count, world.Map.Size);
    }

    /// <summary>Each cell: how far along its current (m), how far to its side (m), how strongly it shows, and which current.</summary>
    void Bake(PlaneWorld world)
    {
        _world = world;
        Array.Clear(_rolled);
        int n = world.Map.Size * PerMetre;
        var cells = new float[n * n * 4];
        var nearest = new float[n * n];
        var own = new float[n * n * 3];
        for (int id = 0; id < Math.Min(world.Currents.Count, Most); id++)
        {
            var c = world.Currents[id];
            float half = Mathf.Max(c.HalfStart, c.HalfEnd), reach = half + Past, channel = half / reach;
            Array.Fill(nearest, float.MaxValue);
            Array.Clear(own);
            float s = 0f;
            for (int i = 0; i + 1 < c.Course.Count; i++)
            {
                System.Numerics.Vector2 a = c.Course[i], b = c.Course[i + 1], ab = b - a;
                float len = ab.Length();
                if (len < 1e-5f) continue;
                int x0 = Cell(Mathf.Min(a.X, b.X) - reach, n), x1 = Cell(Mathf.Max(a.X, b.X) + reach, n);
                int y0 = Cell(Mathf.Min(a.Y, b.Y) - reach, n), y1 = Cell(Mathf.Max(a.Y, b.Y) + reach, n);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new System.Numerics.Vector2((x + 0.5f) / PerMetre, (y + 0.5f) / PerMetre);
                    float t = Mathf.Clamp(System.Numerics.Vector2.Dot(p - a, ab) / (len * len), 0f, 1f);
                    var off = p - (a + ab * t);
                    float d = off.Length();
                    int k = y * n + x;
                    if (d >= reach || d >= nearest[k]) continue;
                    nearest[k] = d;
                    float along = s + len * t, across = d / reach;
                    float ends = Mathf.SmoothStep(0f, 5f, along) * (1f - Mathf.SmoothStep(c.Length - 5f, c.Length, along));
                    float sides = 1f - Mathf.SmoothStep(0.45f, 1f, across);
                    float walls = Mathf.Lerp(0.45f, 1f, 1f - Mathf.SmoothStep(channel - 0.08f, channel + 0.12f, across));
                    own[k * 3] = along;
                    own[k * 3 + 1] = (ab.X * off.Y - ab.Y * off.X) < 0f ? -d : d;
                    own[k * 3 + 2] = ends * sides * walls;
                }
                s += len;
            }
            // Where currents cross, the stronger shows.
            for (int k = 0; k < n * n; k++)
            {
                if (own[k * 3 + 2] <= cells[k * 4 + 2]) continue;
                cells[k * 4] = own[k * 3];
                cells[k * 4 + 1] = own[k * 3 + 1];
                cells[k * 4 + 2] = own[k * 3 + 2];
                cells[k * 4 + 3] = id;
            }
        }
        var bytes = new byte[cells.Length * sizeof(float)];
        Buffer.BlockCopy(cells, 0, bytes, 0, bytes.Length);
        _map = ImageTexture.CreateFromImage(Image.CreateFromData(n, n, false, Image.Format.Rgbaf, bytes));
    }

    static int Cell(float metres, int n) => Mathf.Clamp((int)(metres * PerMetre), 0, n - 1);
}
