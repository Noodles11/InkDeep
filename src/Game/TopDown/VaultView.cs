using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Bubble vaults on screen (docs/proposals/CROSSOVER.md §5.1): a big air bubble caught against the rock, silvery and
/// wobbling. What it holds shows inside it only in her light (PlaneWorld.Seen): its shells, a healing orb, a pearl.
/// Her beam fills it with her warm light till it bursts: it swells, flashes and is gone, and what it held tumbles out
/// (the sim's own shells, orbs and pearl, drawn by the combat view).
/// </summary>
public partial class VaultView : Node3D
{
    sealed class Vault
    {
        public PlaneVault Sim = null!;
        public Node3D Root = null!;
        public MeshInstance3D Bubble = null!;
        public Node3D Inside = null!;
        public float Seen, Phase;
    }

    sealed class Pop
    {
        public MeshInstance3D Mesh = null!;
        public float Age, Radius;
    }

    readonly List<Vault> _vaults = new();
    readonly List<Pop> _pops = new();
    readonly SphereMesh _sphere = new() { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16 };
    ShaderMaterial _bubble = null!;
    StandardMaterial3D _pearl = null!, _orb = null!;
    float _time;

    public override void _Ready()
    {
        _bubble = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader") };
        _pearl = new StandardMaterial3D { AlbedoColor = new Color(0.96f, 0.94f, 0.9f), Roughness = 0.15f, Metallic = 0.1f, ClearcoatEnabled = true };
        var her = HerColor.Light;
        _orb = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = her, EmissionEnabled = true, Emission = her, EmissionEnergyMultiplier = 2.5f };
    }

    /// <summary>A new level's vaults.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var v in _vaults) v.Root.QueueFree();
        foreach (var p in _pops) p.Mesh.QueueFree();
        _vaults.Clear();
        _pops.Clear();
        foreach (var sim in world.Vaults)
        {
            var root = new Node3D { Position = new Vector3(sim.Position.X, LevelMap.SwimBand, sim.Position.Y) };
            var bubble = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _bubble, Scale = Vector3.One * sim.Radius, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            float seed = (float)GD.RandRange(0.0, 10.0);
            bubble.SetInstanceShaderParameter(SN.Of("rainbow"), 0.55f);
            bubble.SetInstanceShaderParameter(SN.Of("wobble"), 0.5f);
            bubble.SetInstanceShaderParameter(SN.Of("seed"), seed);
            bubble.SetInstanceShaderParameter(SN.Of("hole"), new Vector4(0f, 1f, 0f, 0f));
            root.AddChild(bubble);
            var inside = Contents(sim);
            root.AddChild(inside);
            AddChild(root);
            _vaults.Add(new Vault { Sim = sim, Root = root, Bubble = bubble, Inside = inside, Phase = seed });
        }
    }

    /// <summary>What a vault holds, small inside it: its shells in a little heap, an orb's light, a pearl.</summary>
    Node3D Contents(PlaneVault sim)
    {
        var node = new Node3D { Visible = false };
        int n = sim.Shells.Count + sim.Orbs + (sim.PearlId is null ? 0 : 1);
        int i = 0;
        Vector3 Spot() => n <= 1 ? Vector3.Zero : new Vector3(Mathf.Cos(i * Mathf.Tau / n), 0f, Mathf.Sin(i * Mathf.Tau / n)) * sim.Radius * 0.42f + Vector3.Down * 0.1f;
        foreach (var kind in sim.Shells)
        {
            float size = CombatView.ShellSize * CombatView.ShellScale(kind) * 0.85f;
            var shell = new MeshInstance3D { Mesh = NautilusShell.Mesh, MaterialOverride = NautilusShell.Material, Position = Spot(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            shell.Basis = Basis.FromEuler(new Vector3((float)GD.RandRange(0, Mathf.Tau), (float)GD.RandRange(0, Mathf.Tau), 0f)).Scaled(Vector3.One * size);
            node.AddChild(shell);
            i++;
        }
        for (int k = 0; k < sim.Orbs; k++, i++)
            node.AddChild(new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, Position = Spot(), Scale = Vector3.One * 0.18f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (sim.PearlId is not null)
            node.AddChild(new MeshInstance3D { Mesh = _sphere, MaterialOverride = _pearl, Position = Spot(), Scale = Vector3.One * 0.22f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return node;
    }

    /// <summary>A vault bursts: it swells and flashes with her light, and is gone.</summary>
    public void Burst(Vector2 at, float radius)
    {
        var mesh = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _bubble, Position = new Vector3(at.X, LevelMap.SwimBand, at.Y), Scale = Vector3.One * radius, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        mesh.SetInstanceShaderParameter(SN.Of("lantern"), 1f);
        mesh.SetInstanceShaderParameter(SN.Of("warmth"), 1f);
        mesh.SetInstanceShaderParameter(SN.Of("hole"), new Vector4(0f, 1f, 0f, 0f));
        AddChild(mesh);
        _pops.Add(new Pop { Mesh = mesh, Radius = radius });
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        foreach (var v in _vaults)
        {
            if (v.Sim.Burst)
            {
                v.Root.Visible = false;
                continue;
            }
            // It breathes and sways a little, pressed to its rock.
            float sway = Mathf.Sin(_time * 1.3f + v.Phase);
            var wall = new Vector3(v.Sim.Wall.X, 0f, v.Sim.Wall.Y);
            v.Root.Position = new Vector3(v.Sim.Position.X, LevelMap.SwimBand + 0.05f * sway, v.Sim.Position.Y) - wall * 0.04f * sway;
            float fill = Mathf.Clamp(v.Sim.Light / VaultTuning.BurstLight, 0f, 1f);
            v.Bubble.Scale = new Vector3(1f + 0.04f * sway, 0.85f, 1f - 0.04f * sway) * v.Sim.Radius * (1f + 0.12f * fill);
            // Her light filling it warms it from within.
            v.Bubble.SetInstanceShaderParameter(SN.Of("lantern"), 0.15f + 0.85f * fill);
            v.Bubble.SetInstanceShaderParameter(SN.Of("warmth"), fill);
            v.Bubble.SetInstanceShaderParameter(SN.Of("wobble"), 0.5f + 1.2f * fill);
            // What it holds shows in her light, fading in and out.
            v.Seen = Mathf.MoveToward(v.Seen, world.Seen(v.Sim) ? 1f : 0f, dt * 3f);
            v.Inside.Visible = v.Seen > 0.05f;
            v.Inside.Scale = Vector3.One * Mathf.Lerp(0.6f, 1f, v.Seen);
            v.Inside.RotateY(dt * 0.4f);
        }
        for (int i = _pops.Count - 1; i >= 0; i--)
        {
            var p = _pops[i];
            p.Age += dt;
            float k = p.Age / 0.3f;
            if (k >= 1f)
            {
                p.Mesh.QueueFree();
                _pops.RemoveAt(i);
                continue;
            }
            p.Mesh.Scale = Vector3.One * p.Radius * (1f + 0.6f * k);
            p.Mesh.SetInstanceShaderParameter(SN.Of("fade"), 1f - k);
        }
    }
}
