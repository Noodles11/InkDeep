using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The stone lanterns (docs/LIGHT-AND-GRAPH.md §2.7; PlaneLanterns.cs): a tōrō on the floor (a broad foot, a post, a
/// ledge, the fire box with its windows, a wide six-sided roof and its jewel), in its layer's reef colours (its rock, its
/// roof the coral's, grown with the algae's turf, its jewel the second coral's), so it reads against the reef as the
/// reef's own (the post pass withers it with the reef out of light). Her beam on it kindles a flame in its fire box,
/// growing as she holds it; lit, the flame burns on, flickering, giving a light as strong as hers (BellView's bulb), and
/// its pool of light (the post pass's lantern_pools) shows the reef round it.
/// </summary>
public partial class LanternView : Node3D
{
    /// <summary>Its flame and light: her own light's colour (HerColor.Light), as her beam kindled it.</summary>
    static Color Flame => OctoShoots.Game.Fx.HerColor.Light;
    /// <summary>How much larger than life it is drawn.</summary>
    const float DrawnScale = 1.6f;

    sealed class Look
    {
        public required Node3D Root;
        public required MeshInstance3D Fire;
        public required StandardMaterial3D FireMaterial;
        public required OmniLight3D Light;
        public required MeshInstance3D Halo;
        public float Seed;
    }

    readonly Dictionary<StoneLantern, Look> _shown = new();
    /// <summary>Its stone made in each layer's colours (by its look's name).</summary>
    readonly Dictionary<string, ArrayMesh> _stones = new();
    StandardMaterial3D? _stoneMaterial;
    SphereMesh? _flame;
    /// <summary>Its glow in the water once lit (bubble_halo.gdshader), about as wide and bright as hers looks round her.</summary>
    const float HaloRadius = 1.8f, HaloStrength = 0.7f;
    readonly QuadMesh _haloQuad = new() { Size = new Vector2(2f, 2f) };
    ShaderMaterial? _haloMaterial;

    public void Clear()
    {
        foreach (var look in _shown.Values)
        {
            look.Root.QueueFree();
            look.Light.QueueFree();
            look.Halo.QueueFree();
        }
        _shown.Clear();
    }

    /// <summary>Each frame: a lantern's look made as it comes, its flame as it is kindled or burns; those gone let go.</summary>
    public void Sync(PlaneWorld world, float time)
    {
        var reef = ReefLook.For(world.Map.Depth);
        if (!_stones.TryGetValue(reef.Name, out var stone)) _stones[reef.Name] = stone = BuildStone(reef);
        _stoneMaterial ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, AlbedoColor = Colors.White, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _flame ??= new SphereMesh { Radius = 0.09f, Height = 0.2f, RadialSegments = 10, Rings = 6 };
        var seen = new HashSet<StoneLantern>();
        foreach (var l in world.Lanterns)
        {
            seen.Add(l);
            if (!_shown.TryGetValue(l, out var look)) _shown[l] = look = Make(l, world.Map.HeightAt(l.Position), stone);
            // (Kindling, the flame grows with her beam held on it; lit, it burns on, flickering.)
            float k = l.Lit ? 1f : Mathf.Clamp(l.Held / LanternTuning.Kindle, 0f, 1f);
            look.Fire.Visible = look.Light.Visible = look.Halo.Visible = k > 0.01f;
            if (!look.Fire.Visible) continue;
            float flicker = 0.82f + 0.1f * Mathf.Sin(time * 11f + look.Seed) + 0.08f * Mathf.Sin(time * 23f + look.Seed * 2.3f);
            look.Fire.Scale = Vector3.One * (0.35f + 0.65f * k) * (l.Lit ? flicker : 1f);
            look.FireMaterial.EmissionEnergyMultiplier = (l.Lit ? 1.6f : 0.8f + 0.8f * k) * flicker;
            // Its light as strong as hers, kindled up to it as it catches.
            // (In her light's colour as it is now.)
            var flame = Flame;
            look.FireMaterial.AlbedoColor = look.FireMaterial.Emission = look.Light.LightColor = flame;
            look.Halo.SetInstanceShaderParameter(SN.Of("color"), new Vector3(flame.R, flame.G, flame.B));
            look.Light.LightEnergy = BellView.BulbEnergy * k * k * (0.9f + 0.1f * flicker);
            look.Halo.SetInstanceShaderParameter(SN.Of("strength"), HaloStrength * k * k * (0.85f + 0.15f * flicker));
        }
        var gone = new List<StoneLantern>();
        foreach (var l in _shown.Keys) if (!seen.Contains(l)) gone.Add(l);
        foreach (var l in gone)
        {
            _shown[l].Root.QueueFree();
            _shown[l].Light.QueueFree();
            _shown[l].Halo.QueueFree();
            _shown.Remove(l);
        }
    }

    Look Make(StoneLantern l, float floor, ArrayMesh stone)
    {

        // (Seen from 30 m up it is drawn larger than life, as the reef's life is, so it reads.)
        var root = new Node3D { Position = new Vector3(l.Position.X, floor, l.Position.Y), Rotation = new Vector3(0f, (l.Position.X * 1.7f + l.Position.Y * 0.9f) % Mathf.Tau, 0f), Scale = Vector3.One * DrawnScale };
        root.AddChild(new MeshInstance3D { Mesh = stone, MaterialOverride = _stoneMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var fireMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Flame,
            EmissionEnabled = true,
            Emission = Flame,
            EmissionEnergyMultiplier = 3f,
        };
        var fire = new MeshInstance3D { Mesh = _flame, MaterialOverride = fireMaterial, Position = new Vector3(0f, 0.72f, 0f), Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(fire);
        AddChild(root);
        // (Its light out of the lantern's scale: reaching as far as hers, falling off as hers does.)
        var light = new OmniLight3D
        {
            LightColor = Flame, LightEnergy = 0f, OmniRange = BellView.BulbRange, OmniAttenuation = BellView.Falloff, ShadowEnabled = false,
            Visible = false, Position = new Vector3(l.Position.X, floor + BellView.LightHeight * DrawnScale, l.Position.Y),
        };
        AddChild(light);
        _haloMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/bubble_halo.gdshader") };
        var halo = new MeshInstance3D
        {
            Mesh = _haloQuad, MaterialOverride = _haloMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false,
            Scale = Vector3.One * HaloRadius, Position = new Vector3(l.Position.X, floor + 0.72f * DrawnScale, l.Position.Y),
        };
        halo.SetInstanceShaderParameter(SN.Of("color"), new Vector3(Flame.R, Flame.G, Flame.B));
        AddChild(halo);
        return new Look { Root = root, Fire = fire, FireMaterial = fireMaterial, Light = light, Halo = halo, Seed = l.Position.X * 0.37f + l.Position.Y * 0.71f };
    }

    /// <summary>A six-sided prism from <paramref name="y0"/> to <paramref name="y1"/>, its top and bottom capped.</summary>
    static void Prism(MeshBuilder b, float y0, float y1, float r0, float r1, Color side, Color top, int sides = 6)
    {
        b.Tube(new[] { new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f) }, s => s == 0 ? r0 : r1, sides, s => side);
        b.Disc(new Vector3(0f, y1, 0f), Vector3.Up, r1, top, sides);
        b.Disc(new Vector3(0f, y0, 0f), Vector3.Down, r0, side, sides);
    }

    /// <summary>The lantern's stone, a little over a metre high (its fire box at 0.72 m), in a layer's reef colours.</summary>
    static ArrayMesh BuildStone(ReefLook reef)
    {
        Color Stone = reef.Rock, StoneDark = reef.Rock.Darkened(0.35f), Moss = reef.Algae, Roof = reef.Coral, Jewel = reef.Coral2;
        var b = new MeshBuilder();
        Prism(b, 0f, 0.1f, 0.36f, 0.33f, StoneDark, Stone.Lerp(Moss, 0.4f));
        Prism(b, 0.1f, 0.18f, 0.24f, 0.2f, StoneDark, Stone);
        b.Tube(new[] { new Vector3(0f, 0.18f, 0f), new Vector3(0f, 0.4f, 0f), new Vector3(0f, 0.56f, 0f) }, s => 0.09f + 0.01f * (2 - s), 8, s => Stone);
        Prism(b, 0.56f, 0.62f, 0.2f, 0.27f, StoneDark, Stone);
        // The fire box: its six posts, open between (the flame shows through its windows).
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.Tau / 6f;
            var at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.17f;
            b.Tube(new[] { at + Vector3.Up * 0.62f, at + Vector3.Up * 0.84f }, s => 0.035f, 4, s => Stone);
        }
        Prism(b, 0.84f, 0.88f, 0.21f, 0.23f, StoneDark, Stone);
        // The roof: wide, six-sided, its eaves turned up a little, moss on its slopes.
        b.Cone(new Vector3(0f, 0.88f, 0f), new Vector3(0f, 1.1f, 0f), 0.44f, Roof, Roof.Lerp(Moss, 0.35f), 6);
        b.Disc(new Vector3(0f, 0.88f, 0f), Vector3.Down, 0.44f, Roof.Darkened(0.4f), 6);
        b.Sphere(new Vector3(0f, 1.15f, 0f), 0.06f, Jewel, 6);
        return b.Build();
    }
}
