using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The wakes of everything that swims (docs/VISUAL-SHOWCASE.md): as a motorboat seen from above, a moving body leaves a V
/// of disturbed water behind it, drawn as the game's moving water is (hot air over a road: the picture bent, no lines, no
/// colour). Each wake is the body's own last Period seconds: from each point of its way since, an arm out to either side
/// as far as the water has spread from it (the Kelvin wake's 19.5°), fading as it ages; so the faster it swims, the
/// longer its arms, and a body that stops leaves nothing within a moment. Clementine, every creature, the reef's fish,
/// her shells in flight. Baked each frame into a map round her (Span metres, PerMetre cells a metre: each cell the way
/// the water is pushed there, and how hard), which the post pass bends the picture by (topdown_post's wake_map).
/// </summary>
public sealed class WakeField
{
    /// <summary>How long a body's way shows behind it (s), how far the water spreads to each side for each metre behind (the Kelvin wake's tan 19.5°), and how wide an arm is (m).</summary>
    public const float Period = 0.55f, Spread = 0.36f, Width = 0.55f;
    /// <summary>Slower than this (m/s) a body leaves no wake; the speed (m/s) at which its wake is at its strongest.</summary>
    public const float Least = 0.6f, Full = 6f;
    /// <summary>The map round her: metres a side and cells a metre.</summary>
    public const float Span = 64f;
    const int PerMetre = 3, N = (int)(Span * PerMetre);
    /// <summary>The most bodies drawn (the nearest her).</summary>
    const int Most = 64;

    sealed class Trail
    {
        public readonly List<(System.Numerics.Vector2 At, float Time)> Way = new();
        public float Radius;
        public int Seen;
    }

    readonly Dictionary<object, Trail> _trails = new();
    readonly float[] _push = new float[N * N * 2];
    readonly byte[] _bytes = new byte[N * N * 2];
    Image? _image;
    float _time;
    int _frame;

    public ImageTexture? Texture { get; private set; }
    /// <summary>The map's corner (world x, z).</summary>
    public Vector2 Origin { get; private set; }

    public void Clear()
    {
        _trails.Clear();
        Array.Clear(_push);
    }

    /// <summary>One frame: every body's way kept, and the wakes baked round <paramref name="her"/>.</summary>
    public void Step(PlaneWorld world, float dt, System.Numerics.Vector2 her)
    {
        _time += dt;
        _frame++;
        var bodies = new List<(object Key, System.Numerics.Vector2 At, float Radius)>(128);
        bodies.Add((world.Player, world.Player.Position, world.Radius));
        foreach (var m in world.Murklings) if (m.Alive && !m.Budding) bodies.Add((m, m.Position, m.Radius));
        foreach (var m in world.Mobs) if (m.Alive) bodies.Add((m, m.Position, 0.5f));
        foreach (var f in world.Fish) bodies.Add((f, f.Position, 0.35f));
        foreach (var f in world.ReefFish) bodies.Add((f, f.Position, 0.18f));
        foreach (var s in world.Shells) bodies.Add((s, s.Position, 0.12f));
        foreach (var w in world.Inkwardens) if (w.Alive) bodies.Add((w, w.Position, 0.6f));
        foreach (var t in world.Thumpsquirts) bodies.Add((t, t.Position, 0.6f));
        if (world.Boss is { } boss) bodies.Add((boss, boss.Position, boss.Radius));
        // (The nearest her only, should there be many.)
        if (bodies.Count > Most) bodies.Sort((a, b) => System.Numerics.Vector2.DistanceSquared(a.At, her).CompareTo(System.Numerics.Vector2.DistanceSquared(b.At, her)));
        for (int i = 0; i < bodies.Count && i < Most; i++)
        {
            var (key, at, radius) = bodies[i];
            if (!_trails.TryGetValue(key, out var trail)) _trails[key] = trail = new Trail();
            trail.Radius = radius;
            trail.Seen = _frame;
            // (A body that jumped, a teleport or a change of layer, starts its way again.)
            if (trail.Way.Count > 0 && System.Numerics.Vector2.Distance(trail.Way[^1].At, at) > 4f) trail.Way.Clear();
            trail.Way.Add((at, _time));
            int old = 0;
            while (old < trail.Way.Count - 1 && _time - trail.Way[old + 1].Time > Period) old++;
            if (old > 0) trail.Way.RemoveRange(0, old);
        }
        if (_frame % 30 == 0)
        {
            var gone = new List<object>();
            foreach (var (key, trail) in _trails) if (_frame - trail.Seen > 2) gone.Add(key);
            foreach (var key in gone) _trails.Remove(key);
        }
        Bake(her);
    }

    void Bake(System.Numerics.Vector2 her)
    {
        // (The map's corner on its own cells, so the wakes never swim as she moves.)
        var origin = new System.Numerics.Vector2(MathF.Floor((her.X - Span / 2f) * PerMetre) / PerMetre, MathF.Floor((her.Y - Span / 2f) * PerMetre) / PerMetre);
        Origin = new Vector2(origin.X, origin.Y);
        Array.Clear(_push);
        foreach (var trail in _trails.Values)
        {
            if (trail.Seen != _frame || trail.Way.Count < 2) continue;
            var way = trail.Way;
            var head = way[^1];
            // Its speed now (over the last tenth of a second or so).
            int back = way.Count - 1;
            while (back > 0 && head.Time - way[back].Time < 0.1f) back--;
            float span = head.Time - way[back].Time;
            float speed = span > 1e-4f ? System.Numerics.Vector2.Distance(head.At, way[back].At) / span : 0f;
            if (speed < Least) continue;
            float strength = Math.Clamp((speed - Least) / (Full - Least), 0.25f, 1f);
            // Walking back along its way: each point's arm out to each side as far as the water has spread from it.
            float behind = 0f;
            System.Numerics.Vector2? leftWas = null, rightWas = null;
            float fadeWas = 1f;
            for (int k = way.Count - 1; k > 0; k--)
            {
                var a = way[k].At;
                var b = way[k - 1].At;
                var step = a - b;
                float len = step.Length();
                if (len < 1e-4f) continue;
                behind += len;
                var dir = step / len;
                var side = new System.Numerics.Vector2(-dir.Y, dir.X);
                float age = head.Time - way[k - 1].Time;
                float fade = (1f - age / Period) * strength;
                if (fade <= 0f) break;
                float out_ = trail.Radius * 0.8f + behind * Spread;
                var left = b + side * out_;
                var right = b - side * out_;
                if (leftWas is { } l0 && rightWas is { } r0)
                {
                    Stamp(l0 - origin, left - origin, side, fadeWas, fade);
                    Stamp(r0 - origin, right - origin, -side, fadeWas, fade);
                }
                else
                {
                    // (From its sides at its tail.)
                    Stamp(a + side * trail.Radius * 0.8f - origin, left - origin, side, fade, fade);
                    Stamp(a - side * trail.Radius * 0.8f - origin, right - origin, -side, fade, fade);
                }
                leftWas = left;
                rightWas = right;
                fadeWas = fade;
            }
        }
        for (int i = 0; i < N * N; i++)
        {
            _bytes[i * 2] = (byte)Math.Clamp(128f + _push[i * 2] * 127f, 0f, 255f);
            _bytes[i * 2 + 1] = (byte)Math.Clamp(128f + _push[i * 2 + 1] * 127f, 0f, 255f);
        }
        if (_image is null)
        {
            _image = Image.CreateFromData(N, N, false, Image.Format.Rg8, _bytes);
            Texture = ImageTexture.CreateFromImage(_image);
        }
        else
        {
            _image.SetData(N, N, false, Image.Format.Rg8, _bytes);
            Texture!.Update(_image);
        }
    }

    /// <summary>One piece of an arm (map metres), the water pushed along <paramref name="push"/>, as hard as its ends' strengths.</summary>
    void Stamp(System.Numerics.Vector2 p0, System.Numerics.Vector2 p1, System.Numerics.Vector2 push, float s0, float s1)
    {
        var ab = p1 - p0;
        float len2 = ab.LengthSquared();
        int x0 = Math.Max(0, (int)((MathF.Min(p0.X, p1.X) - Width) * PerMetre)), x1 = Math.Min(N - 1, (int)((MathF.Max(p0.X, p1.X) + Width) * PerMetre));
        int y0 = Math.Max(0, (int)((MathF.Min(p0.Y, p1.Y) - Width) * PerMetre)), y1 = Math.Min(N - 1, (int)((MathF.Max(p0.Y, p1.Y) + Width) * PerMetre));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var p = new System.Numerics.Vector2((x + 0.5f) / PerMetre, (y + 0.5f) / PerMetre);
            float t = len2 > 1e-8f ? Math.Clamp(System.Numerics.Vector2.Dot(p - p0, ab) / len2, 0f, 1f) : 0f;
            float d = System.Numerics.Vector2.Distance(p, p0 + ab * t);
            if (d >= Width) continue;
            float k = 1f - d / Width;
            float w = k * k * (s0 + (s1 - s0) * t);
            int i = (y * N + x) * 2;
            // (The strongest push a cell is given, not their sum: arms crossing do not pile up.)
            float px = push.X * w, py = push.Y * w;
            if (px * px + py * py > _push[i] * _push[i] + _push[i + 1] * _push[i + 1])
            {
                _push[i] = px;
                _push[i + 1] = py;
            }
        }
    }
}
