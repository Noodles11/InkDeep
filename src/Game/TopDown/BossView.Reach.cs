using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Ottilie's reach and ink blooms (docs/NPC-BOSSES-PROPOSAL.md §7.2): a reach's lines lit on the floor through its
/// warning (her suckers' pale light, brightening as it nears), and the clouds of the Leak's ink she blooms into the
/// water, dark and light-drinking, thinning away as they age.
/// </summary>
public partial class BossView
{
    readonly List<MeshInstance3D> _reachLines = new();
    readonly List<MeshInstance3D> _blooms = new();
    StandardMaterial3D? _reachMat;
    ShaderMaterial? _bloomMat;

    void SyncReach(PlaneBoss boss)
    {
        _reachMat ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(1f, 0.72f, 0.55f, 0.6f),
        };
        while (_reachLines.Count < boss.ReachDirs.Count)
        {
            var line = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(1f, 1f) }, MaterialOverride = _reachMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(line);
            _reachLines.Add(line);
        }
        float pulse = 0.16f + 0.12f * Mathf.Sin(_time * 14f);
        for (int i = 0; i < _reachLines.Count; i++)
        {
            var line = _reachLines[i];
            line.Visible = i < boss.ReachDirs.Count && !boss.Freed;
            if (!line.Visible) continue;
            var dir = boss.ReachDirs[i];
            var from = boss.Position + dir * boss.Radius;
            var mid = from + dir * (PlaneBossTuning.ReachLength * 0.5f);
            line.Position = new Vector3(mid.X, LevelMap.SwimBand - 0.5f, mid.Y);
            line.Rotation = new Vector3(0f, -Mathf.Atan2(dir.Y, dir.X), 0f);
            line.Scale = new Vector3(PlaneBossTuning.ReachLength, 1f, PlaneBossTuning.ReachWidth * 1.2f);
        }
        _reachMat.AlbedoColor = new Color(1f, 0.72f, 0.55f, pulse);

        _bloomMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/corruption_orb.gdshader") };
        while (_blooms.Count < boss.Blooms.Count)
        {
            var cloud = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 24, Rings = 12 }, MaterialOverride = _bloomMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            cloud.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.02f);
            cloud.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 0.4f);
            AddChild(cloud);
            _blooms.Add(cloud);
        }
        for (int i = 0; i < _blooms.Count; i++)
        {
            var cloud = _blooms[i];
            cloud.Visible = i < boss.Blooms.Count;
            if (!cloud.Visible) continue;
            var b = boss.Blooms[i];
            // It billows out fast, hangs, and thins away at the end of its life.
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(b.Age / 0.6f, 0f, 1f));
            float fade = 1f - Mathf.SmoothStep(PlaneBossTuning.BloomLife - 1.2f, PlaneBossTuning.BloomLife, b.Age);
            cloud.Position = new Vector3(b.Position.X, LevelMap.SwimBand, b.Position.Y);
            cloud.Scale = new Vector3(b.Radius, b.Radius * 0.45f, b.Radius) * (0.4f + 0.6f * grow);
            cloud.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 0.85f * fade);
            cloud.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), i * 2.1f + b.Age * 0.3f);
        }
    }
}
