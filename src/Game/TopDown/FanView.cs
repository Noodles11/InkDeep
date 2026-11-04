using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's Tide Fan (docs/QUESTS-PROPOSAL.md §2.2): a great old sea fan on its rock in the commune, leaning into the
/// current (sea_fan.gdshader), with a ribbon of kelp drifting from it for each request tied there — its knot holding its
/// token (the pearl in its colour, or gold for shells), glowing softly in the bay's evening — and, for each request she
/// carries done, a gold ribbon glinting at its top, waiting to be untied.
/// </summary>
public partial class FanView : Node3D
{
    /// <summary>A ribbon on the fan: its kind's colour, its token's colour, and done (a carried request to untie).</summary>
    public readonly record struct Ribbon(Color Kind, Color Token, bool Done);

    const float FanWidth = 3.6f, FanHeight = 3.4f, RibbonLength = 1.6f, RibbonWidth = 0.16f;
    MeshInstance3D? _fan;
    ImmediateMesh _strips = new();
    readonly List<MeshInstance3D> _tokens = new();
    readonly List<Ribbon> _ribbons = new();
    StandardMaterial3D _stripMaterial = null!;
    Vector3 _at;
    float _time;

    /// <summary>The bay's fan (none elsewhere).</summary>
    public void Show(LevelMap map)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        // (Its ribbons are tied again for the new place: none drawn from the last one's.)
        _tokens.Clear();
        _ribbons.Clear();
        _fan = null;
        if (map.Fan is not { } at) return;
        float floor = map.HeightAt(at);
        _at = new Vector3(at.X, floor, at.Y);
        // Its rock: a low dark boulder.
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.9f, Height = 1f, RadialSegments = 18, Rings = 8 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.2f, 0.24f), Roughness = 0.95f },
            Position = _at + new Vector3(0f, 0.25f, 0f),
            Scale = new Vector3(1.3f, 1f, 1f),
        });
        // The fan, upright on it, facing the camera and leaning back into the current.
        var fan = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(FanWidth, FanHeight) },
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/sea_fan.gdshader") },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        fan.Transform = new Transform3D(new Basis(Vector3.Right, -0.42f), _at + new Vector3(0f, 0.55f + FanHeight * 0.5f * Mathf.Cos(0.42f), -FanHeight * 0.5f * Mathf.Sin(0.42f)));
        AddChild(fan);
        _fan = fan;
        _stripMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 0.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _strips = new ImmediateMesh();
        AddChild(new MeshInstance3D { Mesh = _strips, MaterialOverride = _stripMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 4f });
    }

    /// <summary>The ribbons to show now: the fan's, then the carried requests done.</summary>
    public void SetRibbons(IReadOnlyList<Ribbon> ribbons)
    {
        _ribbons.Clear();
        _ribbons.AddRange(ribbons);
        if (_fan is null) return;
        while (_tokens.Count < _ribbons.Count)
        {
            var token = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.13f, Height = 0.26f }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            token.MaterialOverride = new StandardMaterial3D { Roughness = 0.15f, Metallic = 0.3f, EmissionEnabled = true, EmissionEnergyMultiplier = 1.2f, RimEnabled = true };
            AddChild(token);
            _tokens.Add(token);
        }
        for (int i = 0; i < _tokens.Count; i++)
        {
            _tokens[i].Visible = i < _ribbons.Count;
            if (i >= _ribbons.Count) continue;
            var m = (StandardMaterial3D)_tokens[i].MaterialOverride;
            m.AlbedoColor = _ribbons[i].Token;
            m.Emission = _ribbons[i].Token;
        }
    }

    /// <summary>Where a ribbon's knot is tied on the fan: the fan's three across its middle, the done ones along its top.</summary>
    Vector3 KnotAt(int i, bool done)
    {
        float across = done ? (i % 3 - 1) * 0.5f : (i % 3 - 1) * 1.05f;
        float up = done ? 3.2f : 2.1f;
        return _at + new Vector3(across, up * Mathf.Cos(0.42f) + 0.55f, -up * Mathf.Sin(0.42f) + 0.05f);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_fan is null) return;
        _strips.ClearSurfaces();
        if (_ribbons.Count == 0) return;
        _strips.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        int fanIndex = 0, doneIndex = 0;
        for (int i = 0; i < _ribbons.Count; i++)
        {
            var r = _ribbons[i];
            var knot = KnotAt(r.Done ? doneIndex++ : fanIndex++, r.Done);
            _tokens[i].Position = knot + new Vector3(0f, 0f, 0.08f);
            float glint = r.Done ? 0.6f + 0.4f * Mathf.Sin(_time * 4f + i) : 0.85f;
            ((StandardMaterial3D)_tokens[i].MaterialOverride).EmissionEnergyMultiplier = r.Done ? 1.6f * glint + 0.4f : 1.2f;
            // The blade drifts out from its knot with the current, waving.
            const int steps = 10;
            Vector3 prevL = default, prevR = default;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                var p = knot + new Vector3(0.55f * t + Mathf.Sin(_time * 1.3f + t * 4f + i) * 0.12f * t, -RibbonLength * 0.7f * t, 0.25f * t + Mathf.Cos(_time * 1.1f + t * 3f + i) * 0.1f * t);
                float half = RibbonWidth * (1f - 0.6f * t);
                var l = p + new Vector3(-half, 0f, 0f);
                var rr = p + new Vector3(half, 0f, 0f);
                var kelp = r.Done ? new Color(1f, 0.8f, 0.35f, 0.95f) : new Color(0.26f, 0.48f, 0.22f, 0.9f).Lerp(r.Kind, 0.35f * (1f - t));
                if (s > 0)
                {
                    Vertex(prevL, kelp);
                    Vertex(prevR, kelp);
                    Vertex(l, kelp);
                    Vertex(prevR, kelp);
                    Vertex(rr, kelp);
                    Vertex(l, kelp);
                }
                prevL = l;
                prevR = rr;
            }
        }
        _strips.SurfaceEnd();
    }

    void Vertex(Vector3 at, Color c)
    {
        _strips.SurfaceSetColor(c);
        _strips.SurfaceSetNormal(Vector3.Back);
        _strips.SurfaceAddVertex(at);
    }
}
