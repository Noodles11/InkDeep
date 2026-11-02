using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The open reef's ways through, on the floor (docs/RPG-PROPOSAL.md §2–3): a hand-drawn map's current station, a bright
/// swirl of streaming water where the ride starts; a region's cave mouths, black throats of the Leak.
/// </summary>
public partial class PortalView : Node3D
{
    static Shader? _shader;

    public void Show(LevelMap map)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        if (map.Station) Add(map.Exit.Position, ReefTuning.StationReach * 2.6f, dark: false);
        foreach (var mouth in map.Mouths) Add(mouth, ReefTuning.MouthReach * 2.6f, dark: true);
    }

    void Add(System.Numerics.Vector2 at, float size, bool dark)
    {
        _shader ??= GD.Load<Shader>("res://assets/shaders/reef_portal.gdshader");
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("dark", dark ? 1f : 0f);
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
}
