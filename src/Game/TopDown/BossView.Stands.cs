using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A hider's stands (Fennel's, docs/FOLK.md §6.2): clumps of kelp round the arena, swaying. Hidden in one
/// he hangs half seen among the stalks, and the Leak gives him away: his drip rises out of it. From his second phase
/// two other stands drip too (his decoys).
/// </summary>
public partial class BossView
{
    readonly List<(Node3D Pivot, float Phase)> _stalks = new();
    readonly List<Node3D> _standRoots = new();
    float _hide, _decoyDrip;

    void BuildStands(PlaneBoss? boss)
    {
        foreach (var root in _standRoots) root.QueueFree();
        _standRoots.Clear();
        _stalks.Clear();
        _hide = 0f;
        if (boss is null || boss.Spec.Move != BossMove.Hide) return;
        var kelp = new StandardMaterial3D { AlbedoColor = new Color(0.26f, 0.36f, 0.16f), Roughness = 0.85f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var rng = new RandomNumberGenerator { Seed = boss.Spec.Seed };
        foreach (var at in boss.Stands)
        {
            var root = new Node3D { Position = new Vector3(at.X, LevelMap.SwimBand - 1.2f, at.Y) };
            AddChild(root);
            _standRoots.Add(root);
            for (int i = 0; i < 11; i++)
            {
                float a = rng.RandfRange(0f, Mathf.Tau), r = Mathf.Sqrt(rng.Randf()) * 1.7f;
                var pivot = new Node3D { Position = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r) };
                root.AddChild(pivot);
                float height = rng.RandfRange(3.2f, 5f), bend = rng.RandfRange(-0.6f, 0.6f);
                var path = BossMeshes.Path(t => new Vector3(bend * t * t, t * height, 0.3f * Mathf.Sin(t * 3f + a)), 10);
                pivot.AddChild(new MeshInstance3D { Mesh = BossMeshes.Tube(path, t => 0.11f * (1f - 0.5f * t) + 0.02f, 5, null, _ => 0.45f), MaterialOverride = kelp });
                _stalks.Add((pivot, rng.RandfRange(0f, 10f)));
            }
        }
    }

    /// <summary>The stands sway; hidden, he fades into his; his decoys drip.</summary>
    void SyncStands(PlaneBoss boss, float dt)
    {
        if (_standRoots.Count == 0) return;
        foreach (var (pivot, phase) in _stalks)
            pivot.Rotation = new Vector3(0.12f * Mathf.Sin(_time * 0.7f + phase), 0f, 0.1f * Mathf.Cos(_time * 0.55f + phase * 1.3f));
        _hide = Mathf.MoveToward(_hide, boss.Hidden && !boss.Freed ? 1f : 0f, dt * 2f);
        if (boss.Freed || boss.Decoys.Count == 0) return;
        _decoyDrip += dt * 6f * boss.Decoys.Count;
        while (_decoyDrip >= 1f)
        {
            _decoyDrip -= 1f;
            var at = boss.Stands[boss.Decoys[(int)_drips.RandRange(0f, boss.Decoys.Count - 0.01f)]];
            var from = new Vector3(at.X, LevelMap.SwimBand + 0.3f, at.Y) + _drips.RandDir() * 0.8f;
            var c0 = new Color(0.03f, 0.015f, 0.05f, 0.9f);
            _drips.Emit(from, new Vector3(_drips.RandRange(-0.15f, 0.15f), _drips.RandRange(0.3f, 0.8f), _drips.RandRange(-0.15f, 0.15f)), _drips.RandRange(0.9f, 1.6f),
                _drips.RandRange(0.05f, 0.1f), 0.02f, c0, c0 with { A = 0f }, drag: 0.8f, buoyancy: 0.4f);
        }
    }
}
