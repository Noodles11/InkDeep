using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A reef's holes down (docs/LIGHT-AND-GRAPH-PROPOSAL.md §6.3): each leads to its own reef of the dive's graph. The
/// reefs below are made off the main thread one at a time, the hole nearest her first; the one under the hole she is
/// nearest shows through it, and she dives into whichever she is over.
/// </summary>
public partial class TopDownMain
{
    /// <summary>The reefs below this one's holes, as they are made (by where they are).</summary>
    readonly Dictionary<LevelId, Task<LevelShape>> _holeShapes = new();
    /// <summary>The hole the level below is shown under (−1: none yet), and the one she is diving into.</summary>
    int _belowHole = -1, _diveHole = -1;
    LevelId _diveTo;
    /// <summary>The reefs of the dive's graph she has been on (the chart's), without their haven marks.</summary>
    readonly HashSet<LevelId> _reefs = new();
    /// <summary>Reefs below that could not be made (told once).</summary>
    readonly HashSet<LevelId> _holeFailed = new();

    /// <summary>She switches the shown level below to another hole only once she is this much nearer to it.</summary>
    const float HoleSwitch = 6f;

    /// <summary>A new reef: its holes' reefs to make, the nearest first.</summary>
    void PrepareHoles()
    {
        _holeShapes.Clear();
        _belowHole = _diveHole = -1;
        StartNextHoleShape();
    }

    System.Numerics.Vector2 HoleCenter(int hole) =>
        hole >= 0 && hole < _map.Holes.Count ? _map.Holes[hole].Shaft.Center : _map.Exit.Position;

    /// <summary>Where a hole leads (a map without its list of holes: the graph's first exit).</summary>
    LevelId DestinationOf(int hole) =>
        hole >= 0 && hole < _map.Holes.Count && _map.Holes[hole].To != default ? _map.Holes[hole].To : LevelPlan.NextOf(_seed, _id);

    int HoleCount => System.Math.Max(1, _map.Holes.Count);

    /// <summary>The hole nearest her (by its middle).</summary>
    int NearestHole()
    {
        int best = 0;
        float near = float.MaxValue;
        for (int i = 0; i < HoleCount; i++)
        {
            float d = System.Numerics.Vector2.Distance(_world.Player.Position, HoleCenter(i));
            if (d < near)
            {
                near = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>One reef below at a time, the hole nearest her first.</summary>
    void StartNextHoleShape()
    {
        if (_holeShapes.Values.Any(t => !t.IsCompleted)) return;
        var her = _world?.Player.Position ?? _map.Start.Position;
        foreach (int i in Enumerable.Range(0, HoleCount).OrderBy(i => System.Numerics.Vector2.Distance(her, HoleCenter(i))))
        {
            var to = DestinationOf(i);
            if (_holeShapes.ContainsKey(to)) continue;
            _holeShapes[to] = Shape(to, background: true);
            return;
        }
    }

    /// <summary>
    /// Each frame: the next reef below put in hand, and the level below shown under the hole she is nearest (the one
    /// she dives into, once she does), once it is made.
    /// </summary>
    void StepHoles()
    {
        if (InDiveCave) return;
        StartNextHoleShape();
        int want = _diveHole >= 0 ? _diveHole : NearestHole();
        if (want == _belowHole) return;
        if (_belowHole >= 0 && _diveHole < 0
            && System.Numerics.Vector2.Distance(_world.Player.Position, HoleCenter(want)) + HoleSwitch > System.Numerics.Vector2.Distance(_world.Player.Position, HoleCenter(_belowHole)))
            return;
        if (!_holeShapes.TryGetValue(DestinationOf(want), out var task) || !task.IsCompleted) return;
        if (task.IsFaulted)
        {
            if (_holeFailed.Add(DestinationOf(want))) GD.PushError(task.Exception?.ToString());
            return;
        }
        DropBelowView();
        ShowBelow(task.Result, want);
    }
}
