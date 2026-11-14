using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Bubble vaults (PlaneVaults.cs) in the water: each a fragile shimmering bubble (bubble_vault.gdshader) floating and
/// bobbing as a pearl does, a faint light inside it. As her light fills one its outline and glow brighten and its light
/// grows, until it pops (TopDownMain shows the pop: a ripple of hot air, a flash and its sound).
/// </summary>
public partial class VaultView : Node3D
{
    static Shader? _shader;
    readonly List<(MeshInstance3D Mesh, OmniLight3D Light)> _shown = new();
    float _time;

    /// <summary>A vault's bubble (its stand in a shop wears one too).</summary>
    public static MeshInstance3D Bubble(float seed)
    {
        _shader ??= GD.Load<Shader>("res://assets/shaders/bubble_vault.gdshader");
        var mesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = VaultTuning.Radius, Height = VaultTuning.Radius * 2f, RadialSegments = 32, Rings = 16 },
            MaterialOverride = new ShaderMaterial { Shader = _shader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        mesh.SetInstanceShaderParameter("seed", seed);
        return mesh;
    }

    public void Show(PlaneWorld world)
    {
        foreach (var (mesh, light) in _shown)
        {
            mesh.QueueFree();
            light.QueueFree();
        }
        _shown.Clear();
        Sync(world, 0f);
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        while (_shown.Count < world.Vaults.Count)
        {
            var v = world.Vaults[_shown.Count];
            var mesh = Bubble(v.Seed);
            var light = new OmniLight3D { LightColor = new Color(1f, 0.95f, 0.88f), OmniRange = 2.6f, ShadowEnabled = false };
            AddChild(mesh);
            AddChild(light);
            _shown.Add((mesh, light));
        }
        for (int i = 0; i < _shown.Count; i++)
        {
            var (mesh, light) = _shown[i];
            if (i >= world.Vaults.Count || world.Vaults[i].Popped)
            {
                mesh.Visible = light.Visible = false;
                continue;
            }
            var v = world.Vaults[i];
            // Floating as a pearl does, a slow bob, and breathing a little faster the fuller it is.
            float bob = 0.16f * Mathf.Sin(_time * 1.3f + v.Seed);
            var at = new Vector3(v.Position.X, LevelMap.SwimBand + 0.15f + bob, v.Position.Y);
            mesh.Position = at;
            mesh.Scale = Vector3.One * (1f + 0.03f * Mathf.Sin(_time * (2.2f + 4f * v.Fill) + v.Seed) + 0.08f * v.Fill);
            mesh.SetInstanceShaderParameter("charge", v.Fill);
            light.Position = at;
            light.LightEnergy = 0.12f + 1.6f * v.Fill * v.Fill;
            mesh.Visible = light.Visible = true;
        }
    }
}
