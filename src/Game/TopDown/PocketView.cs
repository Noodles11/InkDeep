using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Local water (PlaneWaterPockets.cs): a brine pool a dark, glossy pool on the floor; a silt cloud soft brown billows,
/// thinning as it settles; vent shimmer a faint pale haze trembling; a plankton bloom a faint drift of specks that
/// glows cyan in light (its glow drawn after the ink look, its own light).
/// </summary>
public partial class PocketView : Node3D
{
    sealed class Shown
    {
        public WaterPocket Pocket = null!;
        public Node3D Root = null!;
        public readonly List<MeshInstance3D> Puffs = new();
        public StandardMaterial3D? Material;
    }

    readonly Dictionary<WaterPocket, Shown> _shown = new();
    readonly HashSet<WaterPocket> _seen = new();
    float _time;

    static GradientTexture2D Soft() => new()
    {
        Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
        Gradient = new Gradient { Offsets = new[] { 0f, 0.4f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f) } },
    };

    static StandardMaterial3D Puff(Color c, bool add) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = add ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        AlbedoColor = c,
        AlbedoTexture = Soft(),
        // (Drawn after the ink look: silt and haze over what lies under them, a bloom's light its own.)
        RenderPriority = 2,
    };

    public void Clear()
    {
        foreach (var s in _shown.Values) s.Root.QueueFree();
        _shown.Clear();
    }

    Shown Make(PlaneWorld world, WaterPocket w)
    {
        float floor = world.Map.HeightAt(w.Centre);
        var root = new Node3D { Position = new Vector3(w.Centre.X, 0f, w.Centre.Y) };
        AddChild(root);
        var s = new Shown { Pocket = w, Root = root };
        var rng = new RandomNumberGenerator { Seed = (ulong)(w.Seed * 1000f) + 1 };
        switch (w.Kind)
        {
            case PocketKind.Brine:
                s.Material = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.1f, 0.16f, 0.85f), Metallic = 0.4f, Roughness = 0.04f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
                root.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = w.Radius, BottomRadius = w.Radius, Height = 0.04f, RadialSegments = 40 },
                    MaterialOverride = s.Material, Position = new Vector3(0f, floor + 0.12f, 0f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                });
                break;
            default:
            {
                var (colour, add, count, size, y) = w.Kind switch
                {
                    PocketKind.Silt => (new Color(0.42f, 0.33f, 0.22f, 0.55f), false, 9, w.Radius * 0.9f, LevelMap.SwimBand - 0.3f),
                    PocketKind.Vent => (new Color(0.85f, 0.9f, 0.95f, 0.08f), true, 5, w.Radius * 0.8f, LevelMap.SwimBand - 0.6f),
                    _ => (new Color(0.4f, 0.95f, 1f, 0.5f), true, 7, w.Radius * 0.75f, LevelMap.SwimBand - 0.2f),
                };
                s.Material = Puff(colour, add);
                for (int k = 0; k < count; k++)
                {
                    float a = rng.RandfRange(0f, Mathf.Tau), d = Mathf.Sqrt(rng.Randf()) * w.Radius * 0.6f;
                    var q = new MeshInstance3D
                    {
                        Mesh = new QuadMesh { Size = Vector2.One * size * rng.RandfRange(0.7f, 1.2f) }, MaterialOverride = s.Material,
                        Position = new Vector3(Mathf.Cos(a) * d, y + rng.RandfRange(-0.2f, 0.2f), Mathf.Sin(a) * d), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    };
                    root.AddChild(q);
                    s.Puffs.Add(q);
                }
                break;
            }
        }
        return s;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        _seen.Clear();
        foreach (var w in world.Pockets)
        {
            _seen.Add(w);
            if (!_shown.TryGetValue(w, out var s)) _shown[w] = s = Make(world, w);
            switch (w.Kind)
            {
                case PocketKind.Silt:
                    s.Material!.AlbedoColor = s.Material.AlbedoColor with { A = 0.55f * w.Thick };
                    for (int k = 0; k < s.Puffs.Count; k++)
                        s.Puffs[k].Scale = Vector3.One * (1f + 0.25f * (1f - w.Thick) + 0.05f * Mathf.Sin(_time * 0.7f + k));
                    break;
                case PocketKind.Vent:
                    s.Material!.AlbedoColor = s.Material.AlbedoColor with { A = 0.06f + 0.05f * Mathf.Sin(_time * 5f + w.Seed) };
                    for (int k = 0; k < s.Puffs.Count; k++)
                        s.Puffs[k].Position = s.Puffs[k].Position with { Y = LevelMap.SwimBand - 0.6f + 0.15f * Mathf.Sin(_time * 3f + k * 1.7f) };
                    break;
                case PocketKind.Plankton:
                    s.Material!.AlbedoColor = s.Material.AlbedoColor with { A = 0.06f + 0.6f * w.Glow };
                    break;
            }
        }
        List<WaterPocket>? gone = null;
        foreach (var w in _shown.Keys)
            if (!_seen.Contains(w)) (gone ??= new()).Add(w);
        if (gone is null) return;
        foreach (var w in gone)
        {
            _shown[w].Root.QueueFree();
            _shown.Remove(w);
        }
    }
}
