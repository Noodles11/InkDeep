using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>What a reef of the dive's graph looks like on a hole's rim and the chart (docs/LIGHT-AND-GRAPH.md §6.3).</summary>
public static class ReefKinds
{
    public static Color Colour(ReefKind kind) => kind switch
    {
        ReefKind.Haven => new Color(0.55f, 1f, 0.72f),       // Lantern Bay's calm green
        ReefKind.Boss => new Color(0.78f, 0.42f, 1f),        // the Leak's violet: a neighbour held by it
        ReefKind.Treasure => LevelView.PoiColor(PoiKind.TreasureCave),
        ReefKind.Den => LevelView.PoiColor(PoiKind.CurseDen),
        _ => new Color(0.72f, 0.88f, 1f),                    // a plain reef: pale water
    };

    /// <summary>What lies down a hole, as the dive prompt says it.</summary>
    public static string Below(ReefKind kind) => kind switch
    {
        ReefKind.Haven => "Lantern Bay below",
        ReefKind.Boss => "the depth's boss below",
        ReefKind.Treasure => "a clam room below",
        ReefKind.Den => "a curse den below",
        _ => "a reef below",
    };

    /// <summary>The chart's name for a kind.</summary>
    public static string Name(ReefKind kind) => kind switch
    {
        ReefKind.Haven => "Haven",
        ReefKind.Boss => "Boss",
        ReefKind.Treasure => "Clam room",
        ReefKind.Den => "Curse den",
        _ => "Reef",
    };
}

/// <summary>
/// The rims of a reef's holes (docs/LIGHT-AND-GRAPH.md §6.3): a ring of light on each hole's floor and a glow
/// down in it, in the colour of the reef it leads to (ReefKinds). None while a hole lies shut under its leak's sand;
/// once its leak is sealed and it opens, its rim lights and breathes. A boss arena's Crack has its own light and gets none.
/// </summary>
public partial class HoleView : Node3D
{
    const float Lift = 0.35f, OpenEnergy = 1.1f;

    readonly List<(MeshInstance3D Ring, OmniLight3D Glow, StandardMaterial3D Material)> _rims = new();
    LevelMap? _map;
    float _time;
    /// <summary>How open each hole is shown (0 shut → 1 open), eased.</summary>
    readonly List<float> _open = new();

    void Build(LevelMap map)
    {
        foreach (var (ring, glow, _) in _rims)
        {
            ring.QueueFree();
            glow.QueueFree();
        }
        _rims.Clear();
        _open.Clear();
        _map = map;
        foreach (var hole in map.Holes)
        {
            var sh = hole.Shaft;
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = ReefKinds.Colour(hole.Kind),
            };
            var ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = sh.HalfWidth + 0.2f, OuterRadius = sh.HalfWidth + 0.55f, Rings = 48, RingSegments = 6 },
                MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = new Vector3(1f, 0.25f, 1f),
            };
            // Over the floor at the rim, the lowest of a few points round it (so the ring never sinks into the slope).
            float floor = float.MaxValue;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.Tau / 8f;
                floor = Mathf.Min(floor, map.HeightAt(sh.Center + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (sh.HalfWidth + 0.4f)));
            }
            ring.Position = new Vector3(sh.Center.X, floor + Lift, sh.Center.Y);
            var glow = new OmniLight3D
            {
                Position = new Vector3(sh.Center.X, floor + 1.6f, sh.Center.Y), OmniRange = 7f, OmniAttenuation = 1.3f,
                ShadowEnabled = false, LightColor = ReefKinds.Colour(hole.Kind), LightSpecular = 0.2f,
            };
            // (The Crack's fissure: its own light.)
            bool crack = sh.HalfLength > sh.HalfWidth + 0.5f;
            ring.Visible = glow.Visible = !crack;
            AddChild(ring);
            AddChild(glow);
            _rims.Add((ring, glow, material));
            _open.Add(-1f);
        }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        if (!ReferenceEquals(world.Map, _map)) Build(world.Map);
        _time += dt;
        float breathe = 0.85f + 0.15f * Mathf.Sin(_time * 2.2f);
        for (int i = 0; i < _rims.Count; i++)
        {
            var (ring, glow, material) = _rims[i];
            float want = world.HoleOpen(i) ? 1f : 0f;
            // (As it was, on arriving; then eased.)
            _open[i] = _open[i] < 0f ? want : Mathf.MoveToward(_open[i], want, dt * 0.8f);
            var colour = ReefKinds.Colour(world.Map.Holes[i].Kind);
            material.AlbedoColor = new Color(colour, 0.8f * breathe * _open[i]);
            glow.LightEnergy = OpenEnergy * breathe * _open[i];
            bool crack = world.IsCrack(i);
            ring.Visible = glow.Visible = !crack && _open[i] > 0.01f;
        }
    }
}
