using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The open reef's ways through (docs/RPG-PROPOSAL.md §2–3): a hand-drawn map's current station, a bright swirl of
/// streaming water on the floor where the ride starts; and the dark in each cave opening (its rock is LevelView's). A
/// region's open mouth is the Leak's own, black, its slow ink stirring at the back (none where a mouth is shut, its cave
/// sealed: its plug of rock closes it); a leak cave's way out shows the water outside shining in at its far end.
/// </summary>
public partial class PortalView : Node3D
{
    static Shader? _shader, _throat;

    public void Show(LevelMap map, IReadOnlySet<int>? shut = null)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        if (map.Station) Add(map.Exit.Position, ReefTuning.StationReach * 2.6f);
        for (int i = 0; i < map.Mouths.Count; i++)
            if (shut?.Contains(i) != true) AddThroat(map.Mouths[i], wayOut: false);
        if (map.Way is { } way) AddThroat(way, wayOut: true);
    }

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
    void AddThroat(Doorway door, bool wayOut)
    {
        _throat ??= GD.Load<Shader>("res://assets/shaders/cave_throat.gdshader");
        const float floor = -1.3f;
        float height = Doorway.ArchTop + 0.1f - floor;
        var material = new ShaderMaterial { Shader = _throat };
        material.SetShaderParameter("half_width", LevelView.DoorArchHalf);
        material.SetShaderParameter("spring", LevelView.DoorSpring - floor);
        material.SetShaderParameter("height", height);
        material.SetShaderParameter("way_out", wayOut ? 1f : 0f);
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
