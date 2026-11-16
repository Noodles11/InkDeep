using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>One plant or sessile animal on the reef.</summary>
public readonly record struct FloraItem(FloraKind Kind, Transform3D Transform, Color Tint);

/// <summary>
/// Draws the reef's flora: one MultiMesh per species per 64 m tile (few objects, each holding many), each fading out
/// beyond the mist, so only the nearby reef costs anything; none casts a shadow. Sway, lattice, grooves and polyps are done in flora.gdshader.
/// </summary>
public partial class FloraViews : Node3D
{
    const float Tile = 64f;

    sealed record Look(float Sway, float SwaySpeed, float Lattice, float Meander, float Polyps, float Rough, float Glow, float Backlight, float Range);

    static readonly Dictionary<FloraKind, Look> Looks = new()
    {
        [FloraKind.TurtleGrass] = new(0.09f, 1.3f, 0f, 0f, 0f, 0.6f, 0.2f, 0.6f, 30f),
        [FloraKind.Halimeda] = new(0.03f, 1.1f, 0f, 0f, 0f, 0.6f, 0.05f, 0.2f, 28f),
        [FloraKind.Staghorn] = new(0f, 0f, 0f, 0f, 0.8f, 0.75f, 0.06f, 0f, 55f),
        [FloraKind.Elkhorn] = new(0f, 0f, 0f, 0f, 0.6f, 0.7f, 0.06f, 0f, 55f),
        [FloraKind.BrainCoral] = new(0f, 0f, 0f, 1f, 0f, 0.8f, 0.05f, 0f, 55f),
        [FloraKind.SeaFan] = new(0.06f, 0.9f, 1f, 0f, 0f, 0.65f, 0.08f, 0.45f, 50f),
        [FloraKind.SeaRod] = new(0.07f, 1.0f, 0f, 0f, 0.5f, 0.85f, 0.06f, 0.15f, 45f),
        [FloraKind.TubeSponge] = new(0f, 0f, 0f, 0f, 0f, 0.8f, 0.08f, 0f, 50f),
        [FloraKind.BarrelSponge] = new(0f, 0f, 0f, 0f, 0f, 0.9f, 0.05f, 0f, 60f),
    };

    readonly Dictionary<FloraKind, (Mesh Mesh, ShaderMaterial Material)> _species = new();

    /// <summary>Each species' material (a level sets its frame and the dive's portal on them).</summary>
    public IEnumerable<ShaderMaterial> Materials => _species.Values.Select(s => s.Material);

    /// <summary>Draw at any distance (the top-down camera sees the whole view at once).</summary>
    public bool Unlimited { get; set; }

    /// <summary>A level's flora is drawn out to this far from the camera (the camera hangs ~55 m off; the screen's corners are within ~75 m).</summary>
    const float LevelRange = 85f;

    /// <summary>Repaints a species in two variants (low and high ends each), keeping its mesh's light and dark structure.</summary>
    public void SetPalette(FloraKind kind, Color lowA, Color highA, Color lowB, Color highB, float sway = -1f)
    {
        if (!_species.TryGetValue(kind, out var s)) return;
        var m = s.Material;
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("recolor"), 1f);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("low_a"), lowA);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("high_a"), highA);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("low_b"), lowB);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("high_b"), highB);
        if (sway >= 0f) m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("sway"), sway);
    }

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/flora.gdshader");
        foreach (var (kind, look) in Looks)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("sway"), look.Sway);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("sway_speed"), look.SwaySpeed);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lattice"), look.Lattice);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("meander"), look.Meander);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("polyps"), look.Polyps);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("rough"), look.Rough);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("glow"), look.Glow);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("backlight"), look.Backlight);
            _species[kind] = (FloraMeshes.Build(kind), material);
        }
    }

    /// <summary>Each tile's plants of each species, and the node drawing them.</summary>
    readonly Dictionary<(FloraKind Kind, int X, int Z), (List<FloraItem> Items, MultiMeshInstance3D? Node)> _tiles = new();

    public void Clear()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _tiles.Clear();
    }

    public void Show(IReadOnlyList<FloraItem> items)
    {
        Clear();
        Add(items);
        GD.Print($"Flora: {items.Count} plants and animals ({string.Join(", ", items.GroupBy(i => i.Kind).Select(g => $"{g.Count()} {g.Key}"))})");
    }

    /// <summary>More plants drawn (a cube reef's cube laid): only the tiles they fall in made again.</summary>
    public void Add(IEnumerable<FloraItem> items)
    {
        var touched = new HashSet<(FloraKind, int, int)>();
        foreach (var item in items)
        {
            var key = (item.Kind, Mathf.FloorToInt(item.Transform.Origin.X / Tile), Mathf.FloorToInt(item.Transform.Origin.Z / Tile));
            if (!_tiles.TryGetValue(key, out var tile)) _tiles[key] = tile = (new List<FloraItem>(), null);
            tile.Items.Add(item);
            touched.Add(key);
        }
        foreach (var key in touched) MakeTile(key);
    }

    void MakeTile((FloraKind Kind, int X, int Z) key)
    {
        var (list, old) = _tiles[key];
        old?.QueueFree();
        var (mesh, material) = _species[key.Kind];
        var look = Looks[key.Kind];
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = mesh,
            InstanceCount = list.Count,
        };
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var item in list)
        {
            minY = Mathf.Min(minY, item.Transform.Origin.Y);
            maxY = Mathf.Max(maxY, item.Transform.Origin.Y);
        }
        // The node sits at the tile's middle (visibility ranges are measured from it); instances are relative to it.
        var centre = new Vector3((key.X + 0.5f) * Tile, (minY + maxY) * 0.5f, (key.Z + 0.5f) * Tile);
        for (int i = 0; i < list.Count; i++)
        {
            multimesh.SetInstanceTransform(i, list[i].Transform.Translated(-centre));
            multimesh.SetInstanceCustomData(i, list[i].Tint);
        }
        float half = (maxY - minY) * 0.5f + 3f;
        var node = new MultiMeshInstance3D
        {
            Position = centre,
            Multimesh = multimesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-Tile * 0.5f - 3f, -half, -Tile * 0.5f - 3f), new Vector3(Tile + 6f, half * 2f, Tile + 6f)),
            // (A level's own: out to past what the camera can see, ~70 m off, so no tile far beyond the screen is drawn.)
            VisibilityRangeEnd = Unlimited ? LevelRange + Tile * 0.5f : look.Range + Tile * 0.5f,
            VisibilityRangeEndMargin = 8f,
            VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
        };
        AddChild(node);
        _tiles[key] = (list, node);
    }
}
