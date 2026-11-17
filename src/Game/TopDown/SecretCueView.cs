using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The arrows carved into the floor at the walls' feet round a secret's mouth (docs/DIVE.md §6.1; CubeStock.Cues): each a
/// flat stone carving pointing to the mouth, wholly hidden until her beam falls on it (secret_cue.gdshader reads her
/// beam's light, reef_show.gdshaderinc), then glowing a pale moonlit blue in the groove.
/// </summary>
public partial class SecretCueView : Node3D
{
    /// <summary>A carving's size (m a side).</summary>
    const float Size = 1.5f;

    PlaneMesh? _mesh;
    ShaderMaterial? _material;

    public void Show(IReadOnlyList<(System.Numerics.Vector2 At, System.Numerics.Vector2 Way)> cues, Func<System.Numerics.Vector2, float> height)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _mesh ??= new PlaneMesh { Size = new Vector2(Size, Size) };
        _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/secret_cue.gdshader") };
        foreach (var (at, way) in cues)
        {
            AddChild(new MeshInstance3D
            {
                Mesh = _mesh,
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // (Laid on the floor, its +Z the way it points.)
                Position = new Vector3(at.X, height(at) + 0.04f, at.Y),
                Rotation = new Vector3(0f, Mathf.Atan2(way.X, way.Y), 0f),
            });
        }
    }
}
