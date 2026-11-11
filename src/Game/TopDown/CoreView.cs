using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Freed murklings' cores (docs/LIGHT-AND-GRAPH.md §4.2; PlaneWorld.Cores): each a soft ball of her light where
/// it was freed (light_core.gdshader), swelling and thinning out over its second.
/// </summary>
public partial class CoreView : Node3D
{
    /// <summary>Its size as it appears and as it fades (metres across), and how high over her plane it hangs.</summary>
    const float SizeNew = 0.55f, SizeGone = 1.5f, Lift = 0.25f;

    readonly List<MeshInstance3D> _pool = new();
    SphereMesh _mesh = null!;
    ShaderMaterial _material = null!;

    public override void _Ready()
    {
        _mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 8 };
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/light_core.gdshader") };
    }

    public void Sync(PlaneWorld world)
    {
        var cores = world.Cores;
        while (_pool.Count < cores.Count)
        {
            var view = new MeshInstance3D { Mesh = _mesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(view);
            _pool.Add(view);
        }
        _material.SetShaderParameter("tint", OctoShoots.Game.Fx.HerColor.Light);
        for (int i = 0; i < _pool.Count; i++)
        {
            var view = _pool[i];
            view.Visible = i < cores.Count;
            if (!view.Visible) continue;
            var core = cores[i];
            float k = core.Fade;
            float size = Mathf.Lerp(SizeNew, SizeGone, Mathf.Sqrt(k)) * (core.Light >= CoreTuning.PrimeLight ? 1.8f : 1f);
            view.Position = new Vector3(core.Position.X, LevelMap.SwimBand + Lift, core.Position.Y);
            view.Scale = Vector3.One * size;
            view.SetInstanceShaderParameter("fade", k);
        }
    }
}
