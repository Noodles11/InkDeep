using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's mirror (docs/RPG-PROPOSAL.md §8): a still pool of polished nacre on the floor in a low pearl rim. Its face
/// shows the water above as a pale sheen, and her as she swims over it: her glow mirrored under her, rings spreading from it
/// (mirror_pool.gdshader). Swimming onto it opens the mirror's drawer (MirrorMenu).
/// </summary>
public partial class MirrorView : Node3D
{
    ShaderMaterial? _face;
    float _near;

    public void Show(LevelMap map)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _face = null;
        Visible = map.Mirror is not null;
        if (map.Mirror is not { } at) return;
        float r = LevelMap.MirrorRadius;
        Position = new Vector3(at.X, map.HeightAt(at) + 0.06f, at.Y);
        _face = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/mirror_pool.gdshader") };
        _face.SetShaderParameter("radius", r);
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(r * 2f, r * 2f) },
            MaterialOverride = _face,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        // The rim: a low lip of pearl round it, worn smooth.
        var rim = new StandardMaterial3D
        {
            AlbedoColor = new Color("#E9DCD8"),
            Roughness = 0.3f,
            Metallic = 0.15f,
            RimEnabled = true,
            Rim = 0.6f,
            RimTint = 0.7f,
            ClearcoatEnabled = true,
            Clearcoat = 0.8f,
        };
        AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = r - 0.12f, OuterRadius = r + 0.16f, Rings = 48, RingSegments = 10 },
            MaterialOverride = rim,
            Scale = new Vector3(1f, 0.45f, 1f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Her over it: her glow shows in it as she comes near.</summary>
    public void Sync(PlaneWorld world, float dt)
    {
        if (_face is null || world.Map.Mirror is not { } at) return;
        float d = System.Numerics.Vector2.Distance(world.Player.Position, at);
        float target = 1f - Mathf.SmoothStep(LevelMap.MirrorRadius * 0.6f, LevelMap.MirrorRadius + 1.5f, d);
        _near = Mathf.MoveToward(_near, target, dt * 3f);
        _face.SetShaderParameter("near", _near);
    }
}
