using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Ottilie's lamp-shells (docs/LIGHT-AND-GRAPH.md §5.2; PlaneBoss.Lamps): mother-of-pearl shells in her hoard,
/// lying low in the water. Dark, they hold a faint pearly sheen; lit by her beam, they hold her light a while, glowing
/// in her colour (a soft light round them, never brighter than her own), and fade as it leaves them.
/// </summary>
public partial class LampView : Node3D
{
    const float Low = 0.7f, DimEnergy = 0.08f, LitEnergy = 0.9f, LitRange = 4.5f;

    readonly List<(MeshInstance3D Shell, OmniLight3D Light, StandardMaterial3D Material)> _lamps = new();
    float _time;

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        var lamps = world.Boss?.Lamps;
        int count = lamps?.Count ?? 0;
        while (_lamps.Count < count)
        {
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.82f, 0.8f, 0.78f), Roughness = 0.25f, Metallic = 0.1f, EmissionEnabled = true,
                Rim = 0.6f, RimEnabled = true, RimTint = 0.4f,
            };
            var shell = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 20, Rings = 8 },
                MaterialOverride = material, Scale = new Vector3(0.75f, 0.28f, 0.62f),
            };
            var light = new OmniLight3D { OmniRange = LitRange, OmniAttenuation = 1.4f, ShadowEnabled = false, LightSpecular = 0.4f };
            AddChild(shell);
            AddChild(light);
            _lamps.Add((shell, light, material));
        }
        var color = OctoShoots.Game.Fx.HerColor.Light;
        for (int i = 0; i < _lamps.Count; i++)
        {
            var (shell, light, material) = _lamps[i];
            bool shown = i < count;
            shell.Visible = light.Visible = shown;
            if (!shown) continue;
            var lamp = lamps![i];
            float glow = Mathf.Clamp(lamp.Glow / BossLightTuning.LampGlow, 0f, 1f);
            // A last flicker as it lets her light go.
            if (glow is > 0f and < 0.25f) glow *= 0.75f + 0.25f * Mathf.Sin(_time * 30f);
            var at = new Vector3(lamp.Position.X, LevelMap.SwimBand - Low, lamp.Position.Y);
            shell.Position = at;
            light.Position = at + Vector3.Up * 0.4f;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 0.05f + 1.6f * glow;
            light.LightColor = color;
            light.LightEnergy = Mathf.Lerp(DimEnergy, LitEnergy, glow);
        }
    }
}
