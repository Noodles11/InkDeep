using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The reef's animals that answer light (PlaneFauna.cs), drawn as the real things (CLAUDE.md: real, never cartoon):
/// <list type="bullet">
/// <item>glass shrimp: a cluster of small clear bodies, barely there, each turned along its way;</item>
/// <item>a lantern shoal: small dark fish wheeling round their centre, each with a blue photophore along its flank;</item>
/// <item>a cuttlefish: a flat mantle in its fin skirt and its arms gathered ahead, grey; lit, its skin takes the colour
/// of the light on it and glows it back;</item>
/// <item>a mantis shrimp: a dark burrow in the floor; lit, its segmented body comes up out of it and its raptorial claw
/// snaps out at what it strikes;</item>
/// <item>a firefly squid: a slender mantle, glowing and flashing blue in the dark.</item>
/// </list>
/// A glow is drawn after the ink look (its own light, never greyed), as a soft halo.
/// And the murklings' corrupted traits (PlaneInkTraits.cs), shown where the murkling itself shows: a chitin-mirror's glassy
/// half-shell turning round it, a refractor's crystal skin, a band-ward's thin rim in the band it lets through, a
/// light-eater's grey fringe.
/// </summary>
public partial class FaunaView : Node3D
{
    sealed class Shown
    {
        public PlaneFauna Fauna = null!;
        public Node3D Root = null!;
        public readonly List<MeshInstance3D> Parts = new();
        public MeshInstance3D? Halo, Claw;
        public StandardMaterial3D? Skin;
        public float Floor;
    }

    readonly Dictionary<PlaneFauna, Shown> _shown = new();
    readonly HashSet<PlaneFauna> _seen = new();
    readonly Dictionary<Murkling, Node3D> _traits = new();
    readonly HashSet<Murkling> _traitSeen = new();
    float _time;

    const float Swim = LevelMap.SwimBand - 0.25f;

    static StandardMaterial3D Body(Color c, float rough = 0.7f) => new() { AlbedoColor = c, Roughness = rough };

    static StandardMaterial3D Clear(Color c) => new()
    {
        AlbedoColor = c, Roughness = 0.1f, Metallic = 0.2f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    static StandardMaterial3D HaloMaterial(Color c) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        AlbedoColor = c,
        AlbedoTexture = new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
            Gradient = new Gradient { Offsets = new[] { 0f, 0.3f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.3f), new Color(1f, 1f, 1f, 0f) } },
        },
        RenderPriority = 3,
    };

    MeshInstance3D Part(Shown s, Mesh mesh, Material m, Vector3 at, Vector3? scale = null)
    {
        var q = new MeshInstance3D { Mesh = mesh, MaterialOverride = m, Position = at, Scale = scale ?? Vector3.One, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        s.Root.AddChild(q);
        s.Parts.Add(q);
        return q;
    }

    public void Clear()
    {
        foreach (var s in _shown.Values) s.Root.QueueFree();
        _shown.Clear();
        foreach (var t in _traits.Values) t.QueueFree();
        _traits.Clear();
    }

    Shown Make(PlaneWorld world, PlaneFauna f)
    {
        var root = new Node3D();
        AddChild(root);
        var s = new Shown { Fauna = f, Root = root, Floor = world.Map.HeightAt(f.Home) };
        var rng = new RandomNumberGenerator { Seed = (ulong)(f.Seed * 1000f) + 7 };
        switch (f.Kind)
        {
            case FaunaKind.GlassShrimp:
            {
                var m = Clear(new Color(0.85f, 0.95f, 1f, 0.35f));
                for (int k = 0; k < 6; k++)
                    Part(s, new CapsuleMesh { Radius = 0.035f, Height = 0.22f }, m,
                        new Vector3(rng.RandfRange(-0.4f, 0.4f), rng.RandfRange(-0.1f, 0.1f), rng.RandfRange(-0.4f, 0.4f))).RotationDegrees = new Vector3(0f, 0f, 90f);
                break;
            }
            case FaunaKind.LanternShoal:
            {
                var body = Body(new Color(0.1f, 0.12f, 0.18f), 0.4f);
                var spot = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.45f, 0.75f, 1f), RenderPriority = 3, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
                for (int k = 0; k < 7; k++)
                {
                    var fish = Part(s, new CapsuleMesh { Radius = 0.04f, Height = 0.2f }, body, Vector3.Zero);
                    fish.RotationDegrees = new Vector3(0f, 0f, 90f);
                    var dot = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.018f, Height = 0.036f }, MaterialOverride = spot, Position = new Vector3(0f, 0.02f, 0.035f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                    fish.AddChild(dot);
                }
                s.Halo = Part(s, new QuadMesh { Size = Vector2.One * 2.4f }, HaloMaterial(new Color(0.35f, 0.6f, 1f, 0.35f)), Vector3.Zero);
                break;
            }
            case FaunaKind.Cuttlefish:
            {
                s.Skin = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.5f, 0.52f), Roughness = 0.6f, EmissionEnabled = true, Emission = Colors.Black };
                Part(s, new SphereMesh { Radius = 0.5f, Height = 1f }, s.Skin, Vector3.Zero, new Vector3(0.55f, 0.18f, 0.3f));
                // Its fin skirt, the mantle's full length, and its arms gathered ahead (a short cone).
                Part(s, new SphereMesh { Radius = 0.5f, Height = 1f }, Clear(new Color(0.6f, 0.6f, 0.62f, 0.35f)), new Vector3(0f, -0.02f, 0f), new Vector3(0.58f, 0.04f, 0.42f));
                Part(s, new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.08f, Height = 0.22f }, s.Skin, new Vector3(0.36f, 0f, 0f)).RotationDegrees = new Vector3(0f, 0f, -90f);
                s.Halo = Part(s, new QuadMesh { Size = Vector2.One * 2f }, HaloMaterial(new Color(1f, 1f, 1f, 0f)), Vector3.Zero);
                break;
            }
            case FaunaKind.MantisShrimp:
            {
                root.Position = new Vector3(f.Home.X, s.Floor, f.Home.Y);
                Part(s, new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 0.02f }, Body(new Color(0.03f, 0.03f, 0.04f)), new Vector3(0f, 0.02f, 0f));
                var shell = Body(new Color(0.32f, 0.5f, 0.38f), 0.45f);
                for (int k = 0; k < 4; k++)
                    Part(s, new CapsuleMesh { Radius = 0.08f - k * 0.01f, Height = 0.2f }, shell, new Vector3(0f, -0.3f + k * 0.13f, 0f));
                s.Claw = Part(s, new CapsuleMesh { Radius = 0.03f, Height = 0.35f }, Body(new Color(0.55f, 0.35f, 0.3f), 0.4f), Vector3.Zero);
                break;
            }
            default:
            {
                Part(s, new CapsuleMesh { Radius = 0.05f, Height = 0.3f }, Clear(new Color(0.35f, 0.45f, 0.7f, 0.6f)), Vector3.Zero).RotationDegrees = new Vector3(0f, 0f, 90f);
                s.Halo = Part(s, new QuadMesh { Size = Vector2.One * 1.6f }, HaloMaterial(new Color(0.3f, 0.55f, 1f, 0f)), Vector3.Zero);
                break;
            }
        }
        return s;
    }

    public void Sync(PlaneWorld world, BlightView blight, float dt)
    {
        _time += dt;
        _seen.Clear();
        foreach (var f in world.Fauna)
        {
            _seen.Add(f);
            if (!_shown.TryGetValue(f, out var s)) _shown[f] = s = Make(world, f);
            float heading = f.Velocity.LengthSquared() > 0.01f ? Mathf.Atan2(-f.Velocity.Y, f.Velocity.X) : s.Root.Rotation.Y;
            if (f.Kind != FaunaKind.MantisShrimp)
            {
                s.Root.Position = new Vector3(f.Position.X, Swim, f.Position.Y);
                s.Root.Rotation = new Vector3(0f, Mathf.LerpAngle(s.Root.Rotation.Y, heading, Mathf.Min(1f, 6f * dt)), 0f);
            }
            float glow = f.Light is { Active: true } l ? Mathf.Min(l.Intensity / 0.5f, 1.2f) : 0f;
            switch (f.Kind)
            {
                case FaunaKind.GlassShrimp:
                    for (int k = 0; k < s.Parts.Count; k++)
                        s.Parts[k].Position = s.Parts[k].Position with { Y = 0.06f * Mathf.Sin(_time * 3f + k * 1.3f) };
                    break;
                case FaunaKind.LanternShoal:
                    for (int k = 0; k < 7; k++)
                    {
                        float a = _time * (0.9f + 0.1f * k) + k * Mathf.Tau / 7f;
                        var fish = s.Parts[k];
                        fish.Position = new Vector3(Mathf.Cos(a) * 0.6f, 0.08f * Mathf.Sin(a * 2f), Mathf.Sin(a) * 0.6f);
                        fish.Rotation = new Vector3(0f, -a, Mathf.Pi / 2f);
                    }
                    break;
                case FaunaKind.Cuttlefish:
                {
                    var c = new Color(f.Colour.X, f.Colour.Y, f.Colour.Z);
                    s.Skin!.AlbedoColor = new Color(0.5f, 0.5f, 0.52f).Lerp(c * 0.8f, f.Glow);
                    s.Skin.Emission = c * f.Glow * 0.8f;
                    ((StandardMaterial3D)s.Halo!.MaterialOverride).AlbedoColor = new Color(c.R, c.G, c.B, 0.35f * f.Glow);
                    break;
                }
                case FaunaKind.MantisShrimp:
                {
                    // Up out of its burrow as it comes out; its claw snapping out at what it struck.
                    for (int k = 1; k < 5; k++)
                        s.Parts[k].Position = s.Parts[k].Position with { Y = -0.3f + (k - 1) * 0.13f + 0.45f * f.Out };
                    float snap = Mathf.Clamp(1f - f.Struck / 0.2f, 0f, 1f);
                    var to = new Vector3(f.Toward.X - f.Home.X, 0f, f.Toward.Y - f.Home.Y);
                    to = to.LengthSquared() > 1e-4f ? to.Normalized() : Vector3.Right;
                    s.Claw!.Visible = f.Out > 0.5f;
                    s.Claw.Position = new Vector3(0f, 0.35f * f.Out, 0f) + to * (0.15f + snap * (FaunaTuning.MantisReach - 0.2f));
                    s.Claw.LookAtFromPosition(s.Root.GlobalPosition + s.Claw.Position, s.Root.GlobalPosition + s.Claw.Position + to + Vector3.Up * 0.001f, Vector3.Up);
                    s.Claw.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
                    break;
                }
                case FaunaKind.FireflySquid:
                    ((StandardMaterial3D)s.Halo!.MaterialOverride).AlbedoColor = new Color(0.3f, 0.55f, 1f, 0.6f * glow);
                    break;
            }
            if (f.Kind == FaunaKind.LanternShoal) ((StandardMaterial3D)s.Halo!.MaterialOverride).AlbedoColor = new Color(0.35f, 0.6f, 1f, 0.3f * glow);
        }
        List<PlaneFauna>? gone = null;
        foreach (var f in _shown.Keys)
            if (!_seen.Contains(f)) (gone ??= new()).Add(f);
        if (gone is not null)
            foreach (var f in gone)
            {
                _shown[f].Root.QueueFree();
                _shown.Remove(f);
            }
        SyncTraits(world, blight);
    }

    void SyncTraits(PlaneWorld world, BlightView blight)
    {
        _traitSeen.Clear();
        foreach (var m in world.Murklings)
        {
            if (m.Trait == MurkTrait.None || !m.Alive) continue;
            _traitSeen.Add(m);
            if (!_traits.TryGetValue(m, out var v)) _traits[m] = v = MakeTrait(m);
            v.Visible = blight.Shown(m) && !m.Budding;
            v.Position = new Vector3(m.Position.X, LevelMap.SwimBand, m.Position.Y);
            float r = m.Radius;
            v.Scale = Vector3.One * r;
            if (m.Trait == MurkTrait.ChitinMirror) v.Rotation = new Vector3(0f, -m.Shell, 0f);
        }
        List<Murkling>? gone = null;
        foreach (var m in _traits.Keys)
            if (!_traitSeen.Contains(m)) (gone ??= new()).Add(m);
        if (gone is null) return;
        foreach (var m in gone)
        {
            _traits[m].QueueFree();
            _traits.Remove(m);
        }
    }

    Node3D MakeTrait(Murkling m)
    {
        // (Unit-sized, scaled by its radius each frame.)
        var (mesh, mat) = m.Trait switch
        {
            // A glassy half-shell (a hemisphere laid on its side, its dome toward the shell's facing).
            MurkTrait.ChitinMirror => ((Mesh)new SphereMesh { Radius = 1.08f, Height = 1.08f, IsHemisphere = true, RadialSegments = 24, Rings = 8 },
                new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.8f, 0.85f, 0.45f), Metallic = 0.9f, Roughness = 0.05f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled }),
            // A crystal skin over its whole body.
            MurkTrait.Refractor => (new SphereMesh { Radius = 1.15f, Height = 2.3f },
                new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.85f, 0.9f, 0.18f), Roughness = 0.02f, Metallic = 0.3f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha }),
            // A thin rim in the band it lets through.
            MurkTrait.BandWard => (new TorusMesh { InnerRadius = 1.05f, OuterRadius = 1.15f },
                new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = m.Ward switch { LightHue.Red => new Color(0.9f, 0.25f, 0.2f, 0.7f), LightHue.Green => new Color(0.3f, 0.9f, 0.4f, 0.7f), _ => new Color(0.3f, 0.5f, 1f, 0.7f) }, Transparency = BaseMaterial3D.TransparencyEnum.Alpha }),
            // A grey, ashen fringe.
            _ => (new TorusMesh { InnerRadius = 1f, OuterRadius = 1.35f, RingSegments = 6 },
                new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.35f, 0.35f, 0.5f), Roughness = 1f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha }),
        };
        var holder = new Node3D();
        holder.AddChild(new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = m.Trait == MurkTrait.ChitinMirror ? new Vector3(0f, 0f, -90f) : Vector3.Zero,
        });
        AddChild(holder);
        return holder;
    }
}
