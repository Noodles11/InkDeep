using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The level's mirror plates and lightglass panes (PlaneOpticWalls.cs, docs/proposals/LIGHT-WORLD.md §4.2): a plate a
/// slab of polished metal standing in the water, turning as she pushes it; a pane a sheet of tinted glass in its own
/// colour, brightening as her light of its colour rings it, gone when it shatters.
/// </summary>
public partial class OpticWallView : Node3D
{
    const float Height = 2.4f;
    readonly List<(OpticWall Wall, MeshInstance3D Mesh, StandardMaterial3D Material)> _walls = new();

    public void Show(PlaneWorld world)
    {
        foreach (var (_, mesh, _) in _walls) mesh.QueueFree();
        _walls.Clear();
        foreach (var w in world.OpticWalls)
        {
            var m = new StandardMaterial3D();
            float height = Height;
            if (w.Kind is OpticKind.SunStone or OpticKind.ShadowStone)
            {
                // Sun-stone: warm sandstone that hardens in light (a faint warmth while it stands); shadow-stone: dark slate.
                bool sun = w.Kind == OpticKind.SunStone;
                m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                m.AlbedoColor = sun ? new Color(0.86f, 0.7f, 0.42f) : new Color(0.16f, 0.15f, 0.2f);
                m.Roughness = 0.9f;
                if (sun)
                {
                    m.EmissionEnabled = true;
                    m.Emission = new Color(1f, 0.78f, 0.4f);
                }
            }
            else if (w.Kind == OpticKind.Halocline)
            {
                // A layer of denser water: a faint shimmering sheet at her plane.
                m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                m.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                m.AlbedoColor = new Color(0.75f, 0.92f, 1f, 0.22f);
                height = 0.04f;
            }
            else if (w.Kind == OpticKind.Mirror)
            {
                m.AlbedoColor = new Color(0.78f, 0.8f, 0.82f);
                m.Metallic = 1f;
                m.Roughness = 0.08f;
                // (A faint silver sheen, so it reads from above in the dark: never near her light.)
                m.EmissionEnabled = true;
                m.Emission = new Color(0.72f, 0.78f, 0.84f);
                m.EmissionEnergyMultiplier = 0.35f;
            }
            else
            {
                var tint = Tint(w.Band);
                m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                m.AlbedoColor = new Color(tint.R, tint.G, tint.B, 0.38f);
                m.Roughness = 0.05f;
                m.Metallic = 0.2f;
                m.EmissionEnabled = true;
                m.Emission = tint;
                m.EmissionEnergyMultiplier = 0.35f;
            }
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(w.HalfLength * 2f, height, w.HalfThick * (w.Kind == OpticKind.Halocline ? 4f : 2f)) },
                MaterialOverride = m,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mesh);
            _walls.Add((w, mesh, m));
            Place(w, mesh);
        }
    }

    /// <summary>A pane's colour: its band's (an accent, never as bright as her).</summary>
    static Color Tint(LightHue band) => band switch
    {
        LightHue.Red => new Color(0.95f, 0.32f, 0.25f),
        LightHue.Green => new Color(0.35f, 0.92f, 0.45f),
        _ => new Color(0.3f, 0.55f, 1f),
    };

    static void Place(OpticWall w, MeshInstance3D mesh)
    {
        mesh.Position = new Vector3(w.Centre.X, LevelMap.SwimBand, w.Centre.Y);
        // (Its long way along the world's (cos, sin) in x–z: turned by −Angle about up.)
        mesh.Rotation = new Vector3(0f, -w.Angle, 0f);
    }

    public void Sync()
    {
        foreach (var (w, mesh, m) in _walls)
        {
            mesh.Visible = !w.Shattered;
            if (w.Shattered) continue;
            Place(w, mesh);
            if (w.Kind == OpticKind.Lightglass)
            {
                float rung = Mathf.Clamp(w.Charge / OpticWallTuning.Shatter, 0f, 1f);
                m.EmissionEnergyMultiplier = 0.35f + 1.1f * rung * rung;
            }
            else if (w.Kind is OpticKind.SunStone or OpticKind.ShadowStone)
            {
                // (Gone, a faint ghost of it stays, so its place still reads.)
                m.AlbedoColor = m.AlbedoColor with { A = 0.12f + 0.88f * w.Solid };
                if (w.Kind == OpticKind.SunStone) m.EmissionEnergyMultiplier = 0.5f * w.Solid;
            }
            else if (w.Kind == OpticKind.Halocline)
                m.AlbedoColor = m.AlbedoColor with { A = 0.16f + 0.08f * Mathf.Sin((float)Time.GetTicksMsec() * 0.0015f + w.Seed) };
        }
    }
}
