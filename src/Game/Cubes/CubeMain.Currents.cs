using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.Cubes;

/// <summary>
/// The cube world's records and currents in the scene (CUBE-WORLD-PLAN §6, §8, §10): each tick she claims the cubes she
/// enters, is held at shut edges, and the frontier drift and the wake current move her; what she sees is marked; both
/// currents are drawn as streaks of brighter water (cube_current.gdshader), the drift faint round her, the wake current
/// plain at each shut throat of her cube.
/// </summary>
public partial class CubeMain
{
    readonly ReefRecords _records = new();
    readonly CubeCurrents _currents = new();
    LayerRecords Here => _records.Of(_layer);
    /// <summary>What her last tick did at a cube line (the overlay shows it; a hold shows the prompt).</summary>
    Crossing _crossed;
    float _heldShown;
    int _revealTick;

    MeshInstance3D _driftStreaks = null!;
    readonly List<MeshInstance3D> _wakeStreaks = new();
    Shader _currentShader = null!;

    void BuildCurrents(System.Numerics.Vector2 start)
    {
        Here.Arrive(start);
        _currentShader = GD.Load<Shader>("res://assets/shaders/cube_current.gdshader");
        _driftStreaks = Streaks(new Vector2(26f, 26f));
    }

    MeshInstance3D Streaks(Vector2 size)
    {
        var m = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = size },
            MaterialOverride = new ShaderMaterial { Shader = _currentShader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(m);
        return m;
    }

    /// <summary>After her tick: the currents move her, and a cube line crossed claims a cube or holds her at a shut edge.</summary>
    void StepCurrents()
    {
        var p = _world.Player;
        var cube = CubeId.Of(p.Position, _layer);
        if (_stream.Terrain(cube) is { } here)
        {
            _currents.Prepare(here, _space, Here);
            var flow = _currents.DriftAt(p.Position) + CubeCurrents.WakeAt(p.Position, cube, _space, Here);
            if (flow != System.Numerics.Vector2.Zero) _world.Move(ref p.Position, ref flow, _world.Radius, report: false);
        }
        var to = p.Position;
        _crossed = Here.Move(p.PrevPosition, ref to);
        if (_crossed == Crossing.Held)
        {
            p.Position = to;
            p.Velocity = System.Numerics.Vector2.Zero;
            _heldShown = 2f;
        }
        if (++_revealTick % 6 == 0) Here.Reveal(p.Position, 12f);
    }

    /// <summary>Each frame: the streaks where the currents run.</summary>
    void DrawCurrents(float dt, Vector3 her)
    {
        _heldShown = Mathf.Max(0f, _heldShown - dt);
        var p = _world.Player.Position;
        var drift = _currents.DriftAt(p);
        _driftStreaks.Visible = drift != System.Numerics.Vector2.Zero && _move == Move.None;
        _driftStreaks.Position = new Vector3(her.X, LevelMap.SwimBand + 0.25f, her.Z);
        _driftStreaks.SetInstanceShaderParameter("flow", new Vector2(drift.X, drift.Y));
        _driftStreaks.SetInstanceShaderParameter("strength", 0.6f);
        // The wake current at each throat of her cube's shut edges, on her side.
        var cube = CubeId.Of(p, _layer);
        int n = 0;
        if (_move == Move.None)
            foreach (var (edge, inside, _) in cube.Outer())
            {
                if (!Here.IsShut(edge)) continue;
                float into = edge.High == inside ? 1f : -1f;
                foreach (var gate in _space.Edge(edge).Gates)
                {
                    if (n == _wakeStreaks.Count) _wakeStreaks.Add(Streaks(Vector2.One));
                    var s = _wakeStreaks[n++];
                    var at = edge.Point(gate.Along, into * CubeCurrents.WakeReach * 0.5f);
                    bool acrossX = edge.Axis == EdgeAxis.X;
                    ((PlaneMesh)s.Mesh).Size = acrossX ? new Vector2(CubeCurrents.WakeReach, gate.Width + 4f) : new Vector2(gate.Width + 4f, CubeCurrents.WakeReach);
                    s.Position = new Vector3(at.X, LevelMap.SwimBand + 0.25f, at.Y);
                    s.SetInstanceShaderParameter("flow", acrossX ? new Vector2(into * CubeCurrents.Wake, 0f) : new Vector2(0f, into * CubeCurrents.Wake));
                    s.SetInstanceShaderParameter("strength", 1f);
                    s.Visible = true;
                }
            }
        for (int i = n; i < _wakeStreaks.Count; i++) _wakeStreaks[i].Visible = false;
    }
}
