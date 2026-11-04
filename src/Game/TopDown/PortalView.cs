using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The open reef's ways through (docs/RPG-PROPOSAL.md §2–3): a hand-drawn map's current station, a bright swirl of
/// streaming water on the floor where the ride starts; and the plain dark at the back of each cave opening (its rock is
/// LevelView's), none where a region's mouth is shut, its cave sealed (its plug of rock closes it).
/// </summary>
public partial class PortalView : Node3D
{
    static Shader? _shader, _throat, _ink;

    /// <param name="dangers">Each mouth's danger (docs/REEF-WORLD.md §3): a dangerous one weeps the Leak's ink down its
    /// face and onto the floor before it, the more the more dangerous.</param>
    public void Show(LevelMap map, IReadOnlySet<int>? shut = null, IReadOnlyList<int>? dangers = null)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        if (map.Station) Add(map.Exit.Position, ReefTuning.StationReach * 2.6f);
        for (int i = 0; i < map.Mouths.Count; i++)
        {
            if (shut?.Contains(i) == true) continue;
            AddThroat(map.Mouths[i]);
            if (dangers is not null && i < dangers.Count && dangers[i] > 0) AddInk(map, map.Mouths[i], dangers[i] / (float)ReefTuning.MostDanger, i);
        }
        if (map.Way is { } way) AddThroat(way);
    }

    /// <summary>A mouth's weeping ink: runs down its face, and a stain pooled on the floor before its threshold.</summary>
    void AddInk(LevelMap map, Doorway door, float amount, int index)
    {
        _ink ??= GD.Load<Shader>("res://assets/shaders/mouth_ink.gdshader");
        var side = new Vector3(door.Across.X, 0f, door.Across.Y);
        var outward = new Vector3(-door.Into.X, 0f, -door.Into.Y);
        float floor = map.HeightAt(door.At - door.Into * 0.6f);
        float width = 2f * (Doorway.HalfWidth + 1.6f), top = Doorway.FaceHeight + 0.4f;
        var face = door.At - door.Into * 0.12f;
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width, top - floor) },
            MaterialOverride = InkMaterial(amount, 1f, index),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transform = new Transform3D(new Basis(side, Vector3.Up, outward), new Vector3(face.X, (floor + top) * 0.5f, face.Y)),
        });
        const float pool = 4.5f;
        var stain = door.At - door.Into * (pool * 0.5f + 0.2f);
        var into = -outward;
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width + 1.2f, pool) },
            MaterialOverride = InkMaterial(amount, 0f, index),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transform = new Transform3D(new Basis(side, into, side.Cross(into)), new Vector3(stain.X, map.HeightAt(stain) + 0.05f, stain.Y)),
        });
    }

    static ShaderMaterial InkMaterial(float amount, float standing, int index)
    {
        var material = new ShaderMaterial { Shader = _ink };
        material.SetShaderParameter("amount", amount);
        material.SetShaderParameter("standing", standing);
        material.SetShaderParameter("seed", index * 3.7f);
        return material;
    }

    /// <summary>A sealed leak's upwelling (docs/REEF-WORLD.md §3): the station's bright swirl where the leak was.</summary>
    public void Upwell(System.Numerics.Vector2 at) => Add(at, ReefTuning.StationReach * 2.2f);

    void Add(System.Numerics.Vector2 at, float size)
    {
        _shader ??= GD.Load<Shader>("res://assets/shaders/reef_portal.gdshader");
        var material = new ShaderMaterial { Shader = _shader };
        var quad = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(size, size) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Just over the seabed, under her.
            Position = new Vector3(at.X, -0.6f, at.Y),
        };
        AddChild(quad);
    }

    /// <summary>
    /// An opening's far end: a sheet across the back of its passage in its arch's shape (the passage itself darkens into
    /// it in the ground's own shader).
    /// </summary>
    void AddThroat(Doorway door)
    {
        _throat ??= GD.Load<Shader>("res://assets/shaders/cave_throat.gdshader");
        const float floor = -1.3f;
        float height = Doorway.ArchTop + 0.1f - floor;
        var material = new ShaderMaterial { Shader = _throat };
        material.SetShaderParameter("half_width", LevelView.DoorArchHalf);
        material.SetShaderParameter("spring", LevelView.DoorSpring - floor);
        material.SetShaderParameter("height", height);
        var at = door.At + door.Into * (Doorway.Depth - 0.4f);
        // The sheet faces out of the opening (its +Z), standing on the passage floor.
        var outward = new Vector3(-door.Into.X, 0f, -door.Into.Y);
        var side = new Vector3(door.Across.X, 0f, door.Across.Y);
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2f * LevelView.DoorArchHalf, height) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transform = new Transform3D(new Basis(side, Vector3.Up, outward), new Vector3(at.X, floor + height * 0.5f, at.Y)),
        });
    }
}
