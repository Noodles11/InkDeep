using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The light specks (PlaneSpecks.cs): each a small mote of warm light drawn after the ink look (its own light, never
/// greyed), a soft halo round a bright core, twinkling and bobbing, faint but seen from across the level. The ones she
/// gathers flash; the ones a murkling drinks dim with what is left of them.
/// </summary>
public partial class SpeckView : Node3D
{
    const float Halo = 1.1f, Core = 0.28f;
    readonly Dictionary<LightSpeck, (MeshInstance3D Halo, MeshInstance3D Core)> _shown = new();
    readonly HashSet<LightSpeck> _seen = new();
    StandardMaterial3D? _haloMat, _coreMat;
    float _time;

    static StandardMaterial3D Glow(Color colour, float softness) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        AlbedoColor = colour,
        AlbedoTexture = new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
            Gradient = new Gradient { Offsets = new[] { 0f, softness, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f) } },
        },
        // (Drawn after the post pass's ink look: its own light, never greyed.)
        RenderPriority = 3,
        NoDepthTest = false,
    };

    public void Clear()
    {
        foreach (var (h, c) in _shown.Values)
        {
            h.QueueFree();
            c.QueueFree();
        }
        _shown.Clear();
    }

    public void Sync(PlaneWorld world, float dt)
    {
        _time += dt;
        _haloMat ??= Glow(new Color(1f, 0.82f, 0.5f, 0.55f), 0.18f);
        _coreMat ??= Glow(new Color(1f, 0.97f, 0.88f, 1f), 0.5f);
        _seen.Clear();
        foreach (var s in world.Specks)
        {
            _seen.Add(s);
            if (!_shown.TryGetValue(s, out var view))
            {
                var halo = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * Halo }, MaterialOverride = _haloMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                var core = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * Core }, MaterialOverride = _coreMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(halo);
                AddChild(core);
                _shown[s] = view = (halo, core);
            }
            float tw = 0.8f + 0.2f * Mathf.Sin(_time * 3.3f + s.Seed * 7f) + 0.1f * Mathf.Sin(_time * 7.9f + s.Seed);
            float worth = Mathf.Clamp(0.55f + 0.45f * s.Value, 0.3f, 1.3f);
            var at = new Vector3(s.Position.X, LevelMap.SwimBand + 0.15f + 0.12f * Mathf.Sin(_time * 1.7f + s.Seed), s.Position.Y);
            view.Halo.Position = at;
            view.Core.Position = at + Vector3.Up * 0.02f;
            view.Halo.Scale = Vector3.One * worth * tw;
            view.Core.Scale = Vector3.One * worth * (0.9f + 0.1f * tw);
        }
        List<LightSpeck>? gone = null;
        foreach (var s in _shown.Keys)
            if (!_seen.Contains(s)) (gone ??= new()).Add(s);
        if (gone is null) return;
        foreach (var s in gone)
        {
            var (h, c) = _shown[s];
            h.QueueFree();
            c.QueueFree();
            _shown.Remove(s);
        }
    }
}
