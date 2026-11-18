using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The secrets' cracked seals (docs/DIVE.md §6.1; PlaneCrackSeals.cs). The seal's rock is the reef's own, drawn with its
/// ground (CubeShapes.Plug: the passage as the rock it was, flush with the wall); here, the jagged vertical crack down
/// its face, laid on that rock from the floor up to just under the cut, glowing faintly from inside (its light breathing
/// slowly). Her dash into the crack crumbles it: chunks tumble out and sink into the sand, fading, and the way is open.
/// </summary>
public partial class CrackSealView : Node3D
{
    static readonly Color Stone = new(0.52f, 0.5f, 0.46f), StoneDark = new(0.4f, 0.38f, 0.35f);
    static readonly Color Glow = new(0.75f, 0.92f, 1f);
    /// <summary>How long its chunks take to sink away (s).</summary>
    const float Crumble = 1.6f;
    /// <summary>
    /// Where its crack starts up its face (the rock's own height, m: a little under her plane), and how high it runs up the
    /// risen wall (m over it, drawn).
    /// </summary>
    const float CrackFoot = OctoShoots.Core.Gen.TopDown.LevelMap.SwimBand - 0.45f, CrackHigh = 5f;
    /// <summary>The walls' rise as topdown_ground draws it (its rise_from, rise_to).</summary>
    const float RiseFrom = 1.4f, RiseTo = 3.2f;
    /// <summary>How many segments its crack has, and how far off the rock's skin it is laid (m).</summary>
    const int CrackSteps = 7;
    const float CrackOff = 0.04f;

    sealed class Look
    {
        public required MeshInstance3D Node;
        public required ShaderMaterial Crack;
        public required Vector3 Middle;
        public required Vector3 Out;
        public float Seed;
    }

    sealed class Chunk
    {
        public required MeshInstance3D Node;
        public Vector3 Velocity, Spin;
        public float Age;
    }

    readonly Dictionary<CrackSeal, Look> _shown = new();
    readonly List<Chunk> _chunks = new();
    float _lift, _rise;
    static Shader? _shader;
    /// <summary>The cutout as it stands (level_frame's see_through: her xz, radius, amount; its plane).</summary>
    Vector4 _seeThrough;
    float _seePlane;

    /// <summary>The cutout (as PortalView's): the crack is cut where it takes the wall the crack is in.</summary>
    public void SeeThrough(Vector3 her, float radius, float amount, float plane)
    {
        _seeThrough = new Vector4(her.X, her.Z, radius, amount);
        _seePlane = plane;
        foreach (var look in _shown.Values) See(look.Crack);
    }

    void See(ShaderMaterial m)
    {
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_through"), _seeThrough);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_through_y"), _seePlane);
    }

    /// <summary>The rock's own height drawn, risen as the walls are.</summary>
    float Drawn(float h)
    {
        float t = Mathf.Clamp((h - RiseFrom) / (RiseTo - RiseFrom), 0f, 1f);
        return h + _rise * t * t * (3f - 2f * t) + _lift;
    }

    public void Clear()
    {
        foreach (var look in _shown.Values) look.Node.QueueFree();
        _shown.Clear();
        foreach (var c in _chunks) c.Node.QueueFree();
        _chunks.Clear();
    }

    /// <param name="surface">The seal's rock's own height under a point of her plane, null where it has none drawn.</param>
    /// <param name="lift">How far up her layer's ground is drawn; <paramref name="rise"/>, how far its walls rise (ReefStreamView).</param>
    public void Sync(PlaneWorld world, float time, float dt, Func<CrackSeal, System.Numerics.Vector2, float?> surface, float lift, float rise)
    {
        _lift = lift;
        _rise = rise;
        var seen = new HashSet<CrackSeal>();
        foreach (var seal in world.CrackSeals)
        {
            seen.Add(seal);
            if (!_shown.TryGetValue(seal, out var look))
            {
                if (seal.Open) continue;
                float Floor(System.Numerics.Vector2 p) => surface(seal, p) ?? world.Map.HeightAt(p);
                if (Make(seal, Floor) is not { } made) continue;
                _shown[seal] = look = made;
            }
            if (seal.Open)
            {
                Break(look);
                _shown.Remove(seal);
                continue;
            }
            // Its crack breathes faintly, as a light behind rock does.
            look.Crack.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("energy"), 0.55f + 0.25f * Mathf.Sin(time * 1.3f + look.Seed) + 0.08f * Mathf.Sin(time * 4.1f + look.Seed * 2f));
        }
        var gone = new List<CrackSeal>();
        foreach (var s in _shown.Keys) if (!seen.Contains(s)) gone.Add(s);
        foreach (var s in gone)
        {
            _shown[s].Node.QueueFree();
            _shown.Remove(s);
        }
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            var c = _chunks[i];
            c.Age += dt;
            c.Velocity += Vector3.Down * 6f * dt;
            c.Velocity *= Mathf.Exp(-1.8f * dt);
            c.Node.Position += c.Velocity * dt;
            c.Node.Rotation += c.Spin * dt;
            float k = c.Age / Crumble;
            c.Node.Scale = Vector3.One * Mathf.Max(0.01f, 1f - k * k);
            if (c.Age < Crumble) continue;
            c.Node.QueueFree();
            _chunks.RemoveAt(i);
        }
    }

    /// <summary>
    /// The crack: a jagged line up the middle of the seal's face, from a little under her plane up the risen wall, laid on
    /// the rock's skin (a hair off it, toward the water).
    /// </summary>
    Look? Make(CrackSeal seal, Func<System.Numerics.Vector2, float> height)
    {
        var mouth = seal.Mouth;
        var into = System.Numerics.Vector2.Normalize(mouth.Into);
        var across = new System.Numerics.Vector2(-into.Y, into.X);
        // Its rock's rise along its middle, finely (as drawn): from the water before it to the crack's top.
        float foot = Drawn(CrackFoot), top = foot + CrackHigh;
        var line = new List<(float S, float H)>();
        for (float s = -6f; s <= OctoShoots.Core.Gen.TopDown.Doorway.Depth; s += 0.01f)
        {
            float h = Drawn(height(mouth.At + into * s));
            line.Add((s, h));
            if (h >= top) break;
        }
        if (line.Count < 2 || line[^1].H < top || line[0].H >= foot) return null;
        float S(float h)
        {
            for (int i = line.Count - 1; i > 0; i--)
                if (line[i - 1].H < h) return line[i].S;
            return line[0].S;
        }
        var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.Abs(mouth.At.X * 131f + mouth.At.Y * 17f) + 1f) };
        var cb = new OctoShoots.Game.Fx.MeshBuilder();
        var outward = new Vector3(-into.X, 0f, -into.Y);
        Vector3 At(float s, float off, float w, float y)
        {
            var p = mouth.At + into * s + across * (off + w);
            return new Vector3(p.X, y, p.Y) + outward * CrackOff;
        }
        float x = 0f, y0 = foot, s0 = S(foot), w0 = 0.04f;
        for (int k = 1; k <= CrackSteps; k++)
        {
            float y1 = Mathf.Lerp(foot, top, k / (float)CrackSteps), s1 = S(y1);
            float x1 = Mathf.Clamp(x + rng.RandfRange(-0.14f, 0.14f), -0.2f, 0.2f);
            float w1 = 0.035f + 0.025f * rng.Randf();
            cb.Quad(cb.Add(At(s0, x, -w0, y0), Vector3.Up, Glow), cb.Add(At(s0, x, w0, y0), Vector3.Up, Glow),
                cb.Add(At(s1, x1, w1, y1), Vector3.Up, Glow), cb.Add(At(s1, x1, -w1, y1), Vector3.Up, Glow));
            x = x1;
            y0 = y1;
            s0 = s1;
            w0 = w1;
        }
        _shader ??= GD.Load<Shader>("res://assets/shaders/crack_seal.gdshader");
        var crack = new ShaderMaterial { Shader = _shader };
        crack.SetShaderParameter("glow", Glow);
        See(crack);
        var node = new MeshInstance3D { Mesh = cb.Build(), MaterialOverride = crack, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node);
        float ym = foot + 1.2f;
        return new Look { Node = node, Crack = crack, Middle = At(S(ym), 0f, 0f, ym), Out = outward, Seed = rng.Randf() * 6f };
    }

    /// <summary>It crumbles: the crack gone, chunks of its rock tumbling out toward the water and sinking into the sand.</summary>
    void Break(Look look)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(look.Seed * 1000f) + 7 };
        var side = new Vector3(look.Out.Z, 0f, -look.Out.X);
        for (int i = 0; i < 16; i++)
        {
            float size = rng.RandfRange(0.25f, 0.6f);
            var node = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(size, size * rng.RandfRange(0.6f, 1.2f), size * rng.RandfRange(0.5f, 1f)) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = StoneDark.Lerp(Stone, rng.Randf()), Roughness = 1f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = look.Middle + side * rng.RandfRange(-1.6f, 1.6f) + Vector3.Up * rng.RandfRange(-0.8f, 1.2f) - look.Out * rng.RandfRange(0.1f, 0.6f),
            };
            AddChild(node);
            _chunks.Add(new Chunk
            {
                Node = node,
                Velocity = look.Out * rng.RandfRange(0.8f, 3f) + side * rng.RandfRange(-1.2f, 1.2f) + Vector3.Up * rng.RandfRange(0f, 1.5f),
                Spin = new Vector3(rng.RandfRange(-4f, 4f), rng.RandfRange(-4f, 4f), rng.RandfRange(-4f, 4f)),
            });
        }
        look.Node.QueueFree();
    }
}
