using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Crab Deals on screen (docs/ADVENTURE.md §7): the old crab waiting behind its two pearls, tapping its one great claw,
/// rising as she stays at a pearl, snapping the claw shut on her arms when the pearl is absorbed (the severed tips drifting
/// off and fading), then backing into its burrow. Outside, the fake wall's clues: a thread of bubbles from a seam and a
/// faint clicking. A ring fills round the pearl as she holds; Severed Nerve's strand circles her.
/// </summary>
public partial class CrabView : Node3D
{
    static readonly Color Shell = new(0.62f, 0.22f, 0.14f), Pale = new(0.86f, 0.62f, 0.42f);
    const float Size = 1.5f, ClickEvery = 1.1f, ClueReach = 14f;

    Node3D? _crab;
    Node3D _jaw = null!;
    MeshInstance3D _ring = null!, _nerve = null!;
    ShaderMaterial _ringMat = null!;
    FxParticles _bubbles = null!;
    float _time, _snap = 10f, _gone, _click, _bubble, _rise;
    bool _leaving;
    Vector3 _home;

    sealed class Tip
    {
        public MeshInstance3D Mesh = null!;
        public Vector3 Velocity;
        public float Age;
    }

    readonly List<Tip> _tips = new();

    /// <summary>The fake wall's faint clicking, heard near it.</summary>
    public System.Action? Click;

    public override void _Ready()
    {
        _bubbles = new FxParticles(80, additive: false);
        AddChild(_bubbles);
        _ringMat = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform float fill = 0.0;
void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float a = fract(atan(p.x, -p.y) / 6.28318 + 1.0);
	float band = smoothstep(0.76, 0.8, r) * (1.0 - smoothstep(0.92, 0.96, r));
	ALBEDO = vec3(1.0, 0.55, 0.45);
	ALPHA = band * (a < fill ? 0.95 : 0.18);
}",
            },
        };
        _ring = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 1.5f }, MaterialOverride = _ringMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ring);
        _nerve = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f, RadialSegments = 10, Rings = 6 },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.82f, 0.62f) },
            Visible = false,
        };
        AddChild(_nerve);
    }

    static StandardMaterial3D Skin(Color c) => new() { AlbedoColor = c, Roughness = 0.55f };

    static MeshInstance3D Lump(Node3D parent, Color c, Vector3 at, Vector3 scale, Vector3 turn = default)
    {
        var m = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 18, Rings = 10 },
            MaterialOverride = Skin(c), Position = at, Scale = scale, RotationDegrees = turn,
        };
        parent.AddChild(m);
        return m;
    }

    /// <summary>A new level: its crab, if it has a deal room.</summary>
    public void Show(PlaneWorld world)
    {
        _crab?.QueueFree();
        _crab = null;
        foreach (var t in _tips) t.Mesh.QueueFree();
        _tips.Clear();
        _bubbles.Clear();
        _leaving = false;
        _gone = _rise = 0f;
        _snap = 10f;
        if (world.CrabAt is not { } at) return;
        _home = new Vector3(at.X, LevelMap.SwimBand - 0.25f, at.Y);
        var crab = _crab = new Node3D { Position = _home, Scale = Vector3.One * Size };
        crab.Rotation = new Vector3(0f, Mathf.Atan2(world.CrabFacing.X, world.CrabFacing.Y), 0f);
        AddChild(crab);
        Lump(crab, Shell, Vector3.Zero, new Vector3(1f, 0.5f, 0.75f));
        Lump(crab, Pale, new Vector3(0f, -0.2f, 0.1f), new Vector3(0.82f, 0.32f, 0.6f));
        foreach (float s in new[] { -1f, 1f })
        {
            Lump(crab, Shell, new Vector3(s * 0.3f, 0.48f, 0.55f), new Vector3(0.05f, 0.2f, 0.05f));
            Lump(crab, new Color(0.04f, 0.03f, 0.03f), new Vector3(s * 0.3f, 0.68f, 0.56f), Vector3.One * 0.09f);
            for (int k = 0; k < 3; k++)
                Lump(crab, Shell, new Vector3(s * (1.05f + 0.06f * k), -0.2f, 0.3f - k * 0.42f), new Vector3(0.48f, 0.07f, 0.08f), new Vector3(0f, s * (k - 1f) * 18f, s * -24f));
        }
        // Its one great claw, held forward on the right: the fixed finger, and the jaw that snaps.
        var arm = new Node3D { Position = new Vector3(0.9f, 0.12f, 0.6f) };
        crab.AddChild(arm);
        Lump(arm, Shell, new Vector3(0f, 0f, 0.1f), new Vector3(0.28f, 0.2f, 0.38f));
        Lump(arm, Pale, new Vector3(0f, -0.09f, 0.72f), new Vector3(0.14f, 0.08f, 0.5f));
        _jaw = new Node3D { Position = new Vector3(0f, 0.08f, 0.25f) };
        arm.AddChild(_jaw);
        Lump(_jaw, Shell, new Vector3(0f, 0.05f, 0.48f), new Vector3(0.17f, 0.1f, 0.52f));
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        Clues(world, dt);
        SyncRing(world);
        if (world.NerveAt is { } nerve)
        {
            _nerve.Visible = true;
            _nerve.Position = new Vector3(nerve.X, LevelMap.SwimBand, nerve.Y);
        }
        else _nerve.Visible = false;
        for (int i = _tips.Count - 1; i >= 0; i--)
        {
            var t = _tips[i];
            t.Age += dt;
            t.Mesh.Position += t.Velocity * dt;
            t.Velocity *= Mathf.Exp(-1.2f * dt);
            t.Mesh.Scale = Vector3.One * Mathf.Max(1f - t.Age / 1.8f, 0.01f);
            if (t.Age < 1.8f) continue;
            t.Mesh.QueueFree();
            _tips.RemoveAt(i);
        }
        if (_crab is null) return;
        bool holding = world.Stands.Exists(s => s.Kind == StandKind.Deal && s.Hold > 0f);
        bool ritual = world.Pearls.Exists(q => q.Absorbing);
        _rise = Mathf.MoveToward(_rise, holding || ritual ? 1f : 0f, dt * 1.6f);
        _snap += dt;
        // Tapping while it waits, wide open as it rises, shut hard on the cut.
        float open = Mathf.Lerp(0.14f + 0.14f * Mathf.Max(Mathf.Sin(_time * 3f), 0f), 0.75f, _rise) * Mathf.Clamp((_snap - 0.08f) / 0.5f, 0f, 1f);
        _jaw.Rotation = new Vector3(-open, 0f, 0f);
        float back = 0f;
        if (_leaving)
        {
            _gone += dt / 1.6f;
            back = Mathf.Clamp(_gone - 0.4f, 0f, 1f);
            if (back >= 1f) _crab.Visible = false;
        }
        _crab.Position = _home + Vector3.Up * (0.45f * _rise + 0.03f * Mathf.Sin(_time * 1.4f)) - Vector3.Up * (back * 1.6f);
        _crab.Scale = Vector3.One * Size * (1f - 0.6f * back);
    }

    void SyncRing(PlaneWorld world)
    {
        var held = world.Stands.Find(s => s.Kind == StandKind.Deal && s.Hold > 0f && !s.Sold);
        _ring.Visible = held is not null;
        if (held is null) return;
        _ring.Position = new Vector3(held.Position.X, LevelMap.SwimBand + 0.1f, held.Position.Y);
        _ringMat.SetShaderParameter(SN.Of("fill"), held.Hold / CrabTuning.HoldSeconds);
    }

    /// <summary>The fake wall gives itself away only to those who look: a thread of bubbles, a faint click.</summary>
    void Clues(PlaneWorld world, float dt)
    {
        foreach (var seal in world.Seals)
        {
            if (!seal.Rock.Fake || seal.Open) continue;
            float d = System.Numerics.Vector2.Distance(seal.Rock.Center, world.Player.Position);
            if (d > ClueReach) continue;
            _bubble -= dt;
            if (_bubble <= 0f)
            {
                _bubble = 0.3f;
                var at = seal.Rock.Center + seal.Facing * (seal.Rock.Thick + 0.1f);
                _bubbles.Emit(new Vector3(at.X, LevelMap.SwimBand, at.Y), new Vector3(0f, 0.8f, 0f), 1.6f, 0.05f, 0.09f, new Color(0.9f, 0.97f, 1f, 0.7f), new Color(0.9f, 0.97f, 1f, 0f));
            }
            _click -= dt;
            if (_click > 0f) continue;
            _click = ClickEvery;
            Click?.Invoke();
        }
        if (GetViewport().GetCamera3D() is { } eye) _bubbles.Tick(dt, eye.GlobalBasis);
    }

    public void OnEvent(PlaneWorld world, in PlaneEvent e)
    {
        if (e.Type != PlaneEventType.ClawCut) return;
        _snap = 0f;
        _leaving = true;
        // The severed tips, one from each arm, drifting off behind her.
        var rng = new RandomNumberGenerator();
        for (int i = 0; i < 3; i++)
        {
            var off = new Vector3(rng.RandfRange(-0.8f, 0.8f), 0f, rng.RandfRange(0.6f, 1.6f));
            var tip = new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.05f, Height = 1f },
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.86f, 0.72f) },
                Position = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y) + off,
                Rotation = new Vector3(Mathf.Pi * 0.5f, rng.RandfRange(0f, Mathf.Tau), 0f),
            };
            AddChild(tip);
            _tips.Add(new Tip { Mesh = tip, Velocity = off * 0.8f });
        }
    }
}
