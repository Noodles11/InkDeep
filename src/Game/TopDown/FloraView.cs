using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The plants that answer light (PlaneFlora.cs), drawn as the real things (CLAUDE.md: real, never cartoon):
/// <list type="bullet">
/// <item>an anemone: its column on the floor and a crown of tentacles that spread out as it opens and fold in as it shuts
/// (a sun anemone warm coral, a shy one a dusky violet-brown);</item>
/// <item>a giant kelp: its holdfast and stipe up from the floor, the stalk it grows lying out along the water at her plane,
/// its blades along it;</item>
/// <item>a sunflower sponge: a barrel sponge, its rim glowing gold while it gives back the light it stored;</item>
/// <item>a night lily: a stem and its petals, spreading open and glowing a faint violet in the dark;</item>
/// <item>a fluorescent coral: a branching coral glowing green while it gives back the blue light it drank.</item>
/// </list>
/// A glow is drawn after the ink look too (its own light, never greyed), as a soft halo; a light room's ring of plants
/// has a faint ring on the floor while it is shut.
/// </summary>
public partial class FloraView : Node3D
{
    sealed class Plant
    {
        public PlaneFlora Flora = null!;
        public Node3D Root = null!;
        public readonly List<MeshInstance3D> Tentacles = new();
        public readonly List<MeshInstance3D> Blades = new();
        public MeshInstance3D? Stalk, Halo;
        public StandardMaterial3D? Glowing;
        public float Floor;
    }

    readonly List<Plant> _plants = new();
    readonly List<(LightRoom Room, MeshInstance3D Ring)> _rings = new();
    float _time;

    static StandardMaterial3D Body(Color c, float rough = 0.85f) => new() { AlbedoColor = c, Roughness = rough };

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

    static MeshInstance3D Mesh(Mesh mesh, Material m, Vector3 at) =>
        new() { Mesh = mesh, MaterialOverride = m, Position = at, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };

    public void Show(PlaneWorld world)
    {
        foreach (var p in _plants) p.Root.QueueFree();
        foreach (var (_, ring) in _rings) ring.QueueFree();
        _plants.Clear();
        _rings.Clear();
        foreach (var f in world.Flora) _plants.Add(Make(world, f));
        foreach (var room in world.LightRooms)
        {
            float floor = world.Map.HeightAt(room.Centre);
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = FloraTuning.Ring - 0.08f, OuterRadius = FloraTuning.Ring + 0.08f, Rings = 48, RingSegments = 6 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.95f, 1f, 0.25f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
                Position = new Vector3(room.Centre.X, floor + 0.05f, room.Centre.Y),
                Scale = new Vector3(1f, 0.2f, 1f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(ring);
            _rings.Add((room, ring));
        }
    }

    Plant Make(PlaneWorld world, PlaneFlora f)
    {
        float floor = world.Map.HeightAt(f.Position);
        var root = new Node3D { Position = new Vector3(f.Position.X, floor, f.Position.Y), Rotation = new Vector3(0f, f.Seed, 0f) };
        AddChild(root);
        var p = new Plant { Flora = f, Root = root, Floor = floor };
        switch (f.Kind)
        {
            case FloraKind.SunAnemone:
            case FloraKind.ShyAnemone:
            {
                bool sun = f.Kind == FloraKind.SunAnemone;
                var column = Body(sun ? new Color(0.85f, 0.45f, 0.38f) : new Color(0.38f, 0.26f, 0.32f));
                var tip = Body(sun ? new Color(1f, 0.62f, 0.5f) : new Color(0.55f, 0.38f, 0.55f), 0.6f);
                root.AddChild(Mesh(new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.42f, Height = 0.9f, RadialSegments = 14 }, column, new Vector3(0f, 0.45f, 0f)));
                int n = sun ? 22 : 16;
                for (int k = 0; k < n; k++)
                {
                    var t = Mesh(new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.045f, Height = 0.55f, RadialSegments = 5 }, tip, Vector3.Zero);
                    var arm = new Node3D { Position = new Vector3(0f, 0.9f, 0f), Rotation = new Vector3(0f, k * Mathf.Tau / n, 0f) };
                    t.Position = new Vector3(0f, 0.27f, 0f);
                    arm.AddChild(t);
                    root.AddChild(arm);
                    p.Tentacles.Add(t);
                }
                break;
            }
            case FloraKind.GiantKelp:
            {
                var kelp = Body(new Color(0.42f, 0.36f, 0.12f), 0.7f);
                float up = LevelMap.SwimBand - floor;
                root.AddChild(Mesh(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.1f, Height = up, RadialSegments = 6 }, kelp, new Vector3(0f, up * 0.5f, 0f)));
                root.AddChild(Mesh(new SphereMesh { Radius = 0.3f, Height = 0.3f }, kelp, new Vector3(0f, 0.08f, 0f)));
                root.Rotation = Vector3.Zero;
                p.Stalk = Mesh(new BoxMesh { Size = new Vector3(1f, 0.06f, 0.08f) }, kelp, new Vector3(0f, up, 0f));
                root.AddChild(p.Stalk);
                for (int k = 0; k < 10; k++)
                {
                    var b = Mesh(new BoxMesh { Size = new Vector3(0.55f, 0.02f, 0.22f) }, Body(new Color(0.5f, 0.45f, 0.15f), 0.6f), new Vector3(0f, up, 0f));
                    root.AddChild(b);
                    p.Blades.Add(b);
                }
                break;
            }
            case FloraKind.SunflowerSponge:
            {
                p.Glowing = Body(new Color(0.78f, 0.62f, 0.3f));
                p.Glowing.EmissionEnabled = true;
                p.Glowing.Emission = new Color(1f, 0.8f, 0.4f);
                p.Glowing.EmissionEnergyMultiplier = 0f;
                root.AddChild(Mesh(new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.4f, Height = 1.2f, RadialSegments = 16 }, p.Glowing, new Vector3(0f, 0.6f, 0f)));
                root.AddChild(Mesh(new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.3f, Height = 0.05f, RadialSegments = 16 }, Body(new Color(0.15f, 0.1f, 0.05f)), new Vector3(0f, 1.2f, 0f)));
                p.Halo = Halo(new Color(1f, 0.8f, 0.45f, 0.7f), 2.6f);
                break;
            }
            case FloraKind.NightLily:
            {
                var stem = Body(new Color(0.25f, 0.4f, 0.28f));
                root.AddChild(Mesh(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.05f, Height = 1f, RadialSegments = 6 }, stem, new Vector3(0f, 0.5f, 0f)));
                p.Glowing = Body(new Color(0.72f, 0.62f, 0.85f), 0.5f);
                p.Glowing.EmissionEnabled = true;
                p.Glowing.Emission = new Color(0.65f, 0.4f, 1f);
                for (int k = 0; k < 6; k++)
                {
                    var arm = new Node3D { Position = new Vector3(0f, 1f, 0f), Rotation = new Vector3(0f, k * Mathf.Tau / 6f, 0f) };
                    var petal = Mesh(new SphereMesh { Radius = 0.18f, Height = 0.08f, RadialSegments = 10, Rings = 4 }, p.Glowing, new Vector3(0f, 0.05f, 0.16f));
                    petal.Scale = new Vector3(0.7f, 1f, 1.6f);
                    arm.AddChild(petal);
                    root.AddChild(arm);
                    p.Tentacles.Add(petal);
                }
                p.Halo = Halo(new Color(0.65f, 0.45f, 1f, 0.6f), 1.6f);
                break;
            }
            default:
            {
                p.Glowing = Body(new Color(0.55f, 0.6f, 0.75f), 0.6f);
                p.Glowing.EmissionEnabled = true;
                p.Glowing.Emission = new Color(0.35f, 1f, 0.45f);
                p.Glowing.EmissionEnergyMultiplier = 0f;
                var rng = new RandomNumberGenerator { Seed = (ulong)(f.Seed * 1000f) };
                for (int k = 0; k < 6; k++)
                {
                    float h = rng.RandfRange(0.6f, 1.1f);
                    var branch = Mesh(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.07f, Height = h, RadialSegments = 6 }, p.Glowing, Vector3.Zero);
                    var arm = new Node3D { Rotation = new Vector3(rng.RandfRange(-0.6f, 0.6f), k * Mathf.Tau / 6f, rng.RandfRange(-0.6f, 0.6f)) };
                    branch.Position = new Vector3(0f, h * 0.5f, 0f);
                    arm.AddChild(branch);
                    root.AddChild(arm);
                }
                p.Halo = Halo(new Color(0.4f, 1f, 0.5f, 0.6f), 2.2f);
                break;
            }
        }
        if (p.Halo is not null) root.AddChild(p.Halo);
        return p;

        static MeshInstance3D Halo(Color c, float size)
        {
            var h = Mesh(new QuadMesh { Size = Vector2.One * size }, HaloMaterial(c), new Vector3(0f, 1.1f, 0f));
            h.Visible = false;
            return h;
        }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        foreach (var p in _plants)
        {
            var f = p.Flora;
            switch (f.Kind)
            {
                case FloraKind.SunAnemone:
                case FloraKind.ShyAnemone:
                    // Open: the tentacles spread out from the crown; shut: folded in over the mouth.
                    foreach (var t in p.Tentacles)
                    {
                        var arm = (Node3D)t.GetParent();
                        float sway = 0.08f * Mathf.Sin(_time * 1.6f + arm.Rotation.Y * 3f + f.Seed);
                        arm.Rotation = new Vector3(Mathf.Lerp(-0.25f, 1.15f, f.Open) + sway, arm.Rotation.Y, 0f);
                        arm.Scale = Vector3.One * Mathf.Lerp(0.55f, 1f, f.Open);
                    }
                    break;
                case FloraKind.GiantKelp:
                {
                    var way = new Vector3(f.Way.X, 0f, f.Way.Y);
                    float len = Mathf.Max(f.Length, 0.01f);
                    p.Stalk!.Visible = f.Length > 0.05f;
                    p.Stalk.Position = new Vector3(way.X * len * 0.5f, p.Stalk.Position.Y, way.Z * len * 0.5f);
                    p.Stalk.Rotation = new Vector3(0f, -Mathf.Atan2(way.Z, way.X), 0f);
                    p.Stalk.Scale = new Vector3(len, 1f, 1f);
                    for (int k = 0; k < p.Blades.Count; k++)
                    {
                        float at = (k + 0.5f) / p.Blades.Count * FloraTuning.KelpLength;
                        var b = p.Blades[k];
                        b.Visible = at < f.Length;
                        float side = k % 2 == 0 ? 1f : -1f;
                        b.Position = new Vector3(way.X * at - way.Z * 0.18f * side, p.Stalk.Position.Y, way.Z * at + way.X * 0.18f * side);
                        b.Rotation = new Vector3(0.2f * Mathf.Sin(_time * 1.3f + k), -Mathf.Atan2(way.Z, way.X) + 0.6f * side, 0f);
                    }
                    break;
                }
                case FloraKind.SunflowerSponge:
                {
                    float g = Mathf.Min(1f, f.Glow / 5f), fill = Mathf.Clamp(f.Charge / FloraTuning.SpongeFill, 0f, 1f);
                    p.Glowing!.EmissionEnergyMultiplier = 1.5f * g + 0.4f * fill;
                    p.Halo!.Visible = g > 0.02f;
                    p.Halo.Scale = Vector3.One * (0.6f + 0.4f * g);
                    break;
                }
                case FloraKind.NightLily:
                    foreach (var petal in p.Tentacles)
                    {
                        var arm = (Node3D)petal.GetParent();
                        arm.Rotation = new Vector3(Mathf.Lerp(-1.2f, -0.15f, f.Open), arm.Rotation.Y, 0f);
                    }
                    p.Glowing!.EmissionEnergyMultiplier = 1.2f * f.Open;
                    p.Halo!.Visible = f.Open > 0.05f;
                    p.Halo.Scale = Vector3.One * (0.5f + 0.5f * f.Open) * (0.9f + 0.1f * Mathf.Sin(_time * 2f + f.Seed));
                    break;
                default:
                    p.Glowing!.EmissionEnergyMultiplier = 2f * f.Glow;
                    p.Halo!.Visible = f.Glow > 0.03f;
                    p.Halo.Scale = Vector3.One * (0.5f + 0.5f * f.Glow);
                    break;
            }
        }
        foreach (var (room, ring) in _rings) ring.Visible = !room.Opened;
    }
}
