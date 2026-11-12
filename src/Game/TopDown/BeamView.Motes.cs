using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her coloured light carries its colour's own motes in the water it lights (docs/LIGHT-AND-GRAPH.md §10), borne out
/// along the beam by its current: ember sparks sprayed off her like a hot hearth's, frost's tiny snowflakes tumbling in a
/// pale breath of cold, bloom's drifting moss spores and now and then a little leaf. A mixed beam carries every colour's
/// at once. Cosmetic only: each is born at a random point of the cone, short of the rock its line meets.
/// </summary>
public partial class BeamView
{
    FxParticles _glowMotes = null!, _flakes = null!, _leaves = null!;

    /// <summary>Each colour's motes a second, in her own beam at its full (other rays fewer, by their brightness).</summary>
    const float SparkRate = 70f, FlakeRate = 30f, BreathRate = 7f, SporeRate = 46f, LeafRate = 2.5f;

    void ReadyMotes()
    {
        _glowMotes = new FxParticles(1200, additive: true);
        _flakes = new FxParticles(400, additive: true, Snowflake());
        _leaves = new FxParticles(80, additive: false, Leaf());
        AddChild(_glowMotes);
        AddChild(_flakes);
        AddChild(_leaves);
    }

    /// <summary>One ray's motes this frame: <paramref name="share"/> how strongly it shines (0..1+).</summary>
    void EmitMotes(PlaneWorld world, LightEmitter beam, LightHue hue, float range, float share, float height, float dt)
    {
        if (hue == LightHue.None || share <= 0.01f) return;
        bool ember = (hue & LightHue.Red) != 0, bloom = (hue & LightHue.Green) != 0, frost = (hue & LightHue.Blue) != 0;
        if (ember)
            for (int n = Count(SparkRate * share, dt); n > 0; n--)
            {
                // Sprayed off her like sparks off a hot hearth: fast out along the beam, rising and cooling as they go.
                if (!Within(world, beam, range, 0.25f, 0.55f, height, out var at, out var way)) continue;
                bool big = _glowMotes.RandRange(0f, 1f) < 0.12f;
                var v = way * _glowMotes.RandRange(3f, 6.5f) * (big ? 0.5f : 1f) + Vector3.Up * _glowMotes.RandRange(0.2f, 1f) + _glowMotes.RandDir() * 0.7f;
                _glowMotes.Emit(at, v, _glowMotes.RandRange(0.35f, 0.7f) * (big ? 1.6f : 1f), _glowMotes.RandRange(0.055f, 0.085f) * (big ? 1.6f : 1f), 0.015f,
                    new Color(2.4f, 1.25f, 0.4f, 1f), new Color(1f, 0.18f, 0.04f, 0f), drag: big ? 1.4f : 2.2f, buoyancy: 1.4f);
            }
        if (frost)
        {
            for (int n = Count(FlakeRate * share, dt); n > 0; n--)
            {
                // Tiny snowflakes in an icy breath, tumbling out along it, sinking a little.
                if (!Within(world, beam, range, 0.25f, 0.9f, height, out var at, out var way)) continue;
                var v = way * _flakes.RandRange(1.2f, 2.6f) + _flakes.RandDir() * 0.25f;
                _flakes.Emit(at, v, _flakes.RandRange(0.9f, 1.5f), _flakes.RandRange(0.12f, 0.17f), 0.07f,
                    new Color(0.85f, 0.95f, 1.25f, 0.9f), new Color(0.6f, 0.8f, 1f, 0f), drag: 1.2f, buoyancy: -0.15f, spin: _flakes.RandRange(-3f, 3f));
            }
            for (int n = Count(BreathRate * share, dt); n > 0; n--)
            {
                // The breath itself: a pale cold mist welling out from her, spreading as it goes.
                if (!Within(world, beam, range, 0.2f, 0.45f, height, out var at, out var way)) continue;
                _glowMotes.Emit(at, way * _glowMotes.RandRange(1.2f, 2f), _glowMotes.RandRange(0.8f, 1.2f), 0.3f, 0.95f,
                    new Color(0.55f, 0.75f, 0.95f, 0.11f), new Color(0.55f, 0.75f, 0.95f, 0f), drag: 1f);
            }
        }
        if (bloom)
        {
            for (int n = Count(SporeRate * share, dt); n > 0; n--)
            {
                // Moss spores: tiny green-gold specks drifting slowly along it, wandering, rising a little.
                if (!Within(world, beam, range, 0.25f, 0.95f, height, out var at, out var way)) continue;
                var v = way * _glowMotes.RandRange(0.4f, 1.1f) + _glowMotes.RandDir() * 0.35f;
                _glowMotes.Emit(at, v, _glowMotes.RandRange(1.4f, 2.4f), _glowMotes.RandRange(0.035f, 0.055f), 0.018f,
                    new Color(0.8f, 1.15f, 0.45f, 0.95f), new Color(0.45f, 0.9f, 0.3f, 0f), drag: 0.8f, buoyancy: 0.12f);
            }
            for (int n = Count(LeafRate * share, dt); n > 0; n--)
            {
                // Now and then a little leaf, tumbling as the current carries it.
                if (!Within(world, beam, range, 0.25f, 0.8f, height, out var at, out var way)) continue;
                var v = way * _leaves.RandRange(1f, 1.8f) + Vector3.Up * 0.2f + _leaves.RandDir() * 0.2f;
                _leaves.Emit(at, v, _leaves.RandRange(1.6f, 2.2f), _leaves.RandRange(0.19f, 0.25f), 0.15f,
                    new Color(0.55f, 0.95f, 0.38f, 0.95f), new Color(0.4f, 0.8f, 0.3f, 0f), drag: 1f, buoyancy: -0.1f, spin: _leaves.RandRange(-4f, 4f));
            }
        }
    }

    void TickMotes(float dt)
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is null) return;
        _glowMotes.Tick(dt, camera.GlobalBasis);
        _flakes.Tick(dt, camera.GlobalBasis);
        _leaves.Tick(dt, camera.GlobalBasis);
    }

    int Count(float rate, float dt)
    {
        float n = rate * dt;
        return (int)n + (_glowMotes.RandRange(0f, 1f) < n % 1f ? 1 : 0);
    }

    /// <summary>
    /// A random point of a ray's cone, between <paramref name="near"/> and <paramref name="far"/> of its reach and short
    /// of the rock its line meets, and the way its light runs there.
    /// </summary>
    bool Within(PlaneWorld world, LightEmitter beam, float range, float near, float far, float height, out Vector3 at, out Vector3 way)
    {
        float half = beam.HalfAngle * (1f + beam.Flare);
        float a = Mathf.Atan2(beam.Direction.Y, beam.Direction.X) + _glowMotes.RandRange(-1f, 1f) * half * 0.9f;
        var d = new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float s = range * _glowMotes.RandRange(near, far);
        float open = world.LineReach(beam.Position, d, range, out _);
        way = new Vector3(d.X, 0f, d.Y);
        at = default;
        if (open < 0.4f) return false;
        s = Mathf.Min(s, open - 0.15f);
        float up = _glowMotes.RandRange(-1f, 1f) * Mathf.Min(0.35f, s * Mathf.Tan(half));
        at = new Vector3(beam.Position.X + d.X * s, height + up, beam.Position.Y + d.Y * s);
        return true;
    }

    static Texture2D? _snowflake, _leaf;

    /// <summary>A six-armed snowflake, each arm with two pairs of little branches, soft at its edges.</summary>
    static Texture2D Snowflake()
    {
        if (_snowflake is not null) return _snowflake;
        const int size = 64;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.One;
                float d = 9f;
                for (int k = 0; k < 6; k++)
                {
                    var arm = Vector2.FromAngle(k * Mathf.Tau / 6f);
                    d = Mathf.Min(d, Segment(p, Vector2.Zero, arm * 0.9f));
                    foreach (float at in new[] { 0.45f, 0.68f })
                    {
                        var root = arm * at;
                        float len = 0.32f * (1.05f - at);
                        d = Mathf.Min(d, Segment(p, root, root + arm.Rotated(0.9f) * len));
                        d = Mathf.Min(d, Segment(p, root, root + arm.Rotated(-0.9f) * len));
                    }
                }
                float a = 1f - Mathf.SmoothStep(0.035f, 0.075f, d);
                a = Mathf.Max(a, 0.9f * (1f - Mathf.SmoothStep(0f, 0.16f, p.Length())));
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        return _snowflake = ImageTexture.CreateFromImage(img);
    }

    /// <summary>A small leaf: an almond with a pale midrib and a short stalk.</summary>
    static Texture2D Leaf()
    {
        if (_leaf is not null) return _leaf;
        const int size = 64;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) / size * 2f - Vector2.One;
                // (Along it: x from −0.8 to 0.8; its width swelling to the middle and to points at both ends.)
                float t = Mathf.Clamp((p.X + 0.8f) / 1.6f, 0f, 1f);
                float width = 0.42f * Mathf.Sin(Mathf.Pi * t) * (1f - 0.25f * t);
                float inside = 1f - Mathf.SmoothStep(width - 0.05f, width + 0.02f, Mathf.Abs(p.Y));
                if (p.X < -0.8f || p.X > 0.8f) inside = 0f;
                float stalk = (1f - Mathf.SmoothStep(0.02f, 0.05f, Mathf.Abs(p.Y))) * (p.X < -0.75f && p.X > -0.98f ? 1f : 0f);
                float rib = (1f - Mathf.SmoothStep(0.015f, 0.04f, Mathf.Abs(p.Y))) * inside;
                float shade = 0.75f + 0.25f * (1f - Mathf.Abs(p.Y) / Mathf.Max(width, 0.01f));
                var c = new Color(shade, shade, shade).Lerp(new Color(1.4f, 1.4f, 1.2f), rib * 0.6f);
                img.SetPixel(x, y, new Color(Mathf.Min(c.R, 1f), Mathf.Min(c.G, 1f), Mathf.Min(c.B, 1f), Mathf.Max(inside, stalk)));
            }
        return _leaf = ImageTexture.CreateFromImage(img);
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return (p - (a + ab * t)).Length();
    }
}
