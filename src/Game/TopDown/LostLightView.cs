using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her lost light (docs/RPG-PROPOSAL.md §5.1): where she dimmed, a cluster of small lights in her own glow, circling one
/// another over the floor and breathing, a light of its own on the reef round it; taken back, it is gone.
/// </summary>
public partial class LostLightView : Node3D
{
    const int Motes = 7;
    const float Ring = 0.45f, Above = 0.4f;
    readonly MeshInstance3D[] _motes = new MeshInstance3D[Motes];
    OmniLight3D _light = null!;
    StandardMaterial3D _material = null!;
    float _time;

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            EmissionEnergyMultiplier = 2f,
        };
        var mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f, RadialSegments = 12, Rings = 6 };
        for (int i = 0; i < Motes; i++)
        {
            _motes[i] = new MeshInstance3D { Mesh = mesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_motes[i]);
        }
        _light = new OmniLight3D { LightEnergy = 1.6f, OmniRange = 5f, ShadowEnabled = false, Position = new Vector3(0f, Above + 0.6f, 0f) };
        AddChild(_light);
        Visible = false;
    }

    public void Show(PlaneWorld world)
    {
        Visible = world.Lost is { Taken: false };
        if (world.Lost is { } lost) Position = new Vector3(lost.Position.X, LevelMap.SwimBand - Above, lost.Position.Y);
    }

    public void Sync(PlaneWorld world, float dt)
    {
        Visible = world.Lost is { Taken: false };
        if (!Visible) return;
        _time += dt;
        var glow = OctoShoots.Game.Fx.HerColor.Glow;
        _material.AlbedoColor = glow;
        _material.Emission = glow;
        _light.LightColor = glow;
        _light.LightEnergy = 1.3f + 0.5f * Mathf.Sin(_time * 2.2f);
        for (int i = 0; i < Motes; i++)
        {
            float a = _time * (0.9f + 0.15f * i) + i * Mathf.Tau / Motes;
            float r = Ring * (0.6f + 0.4f * Mathf.Sin(_time * 1.3f + i * 1.7f));
            _motes[i].Position = new Vector3(Mathf.Cos(a) * r, Above + 0.25f * Mathf.Sin(_time * 1.8f + i), Mathf.Sin(a) * r);
            _motes[i].Scale = Vector3.One * (0.8f + 0.35f * Mathf.Sin(_time * 3f + i * 2.1f));
        }
    }
}
