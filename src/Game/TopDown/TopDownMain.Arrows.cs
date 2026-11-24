using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// What the emblems at the screen's edges show in the crossover (EdgeArrows, the cubes branch's: a ringed sign in its
/// place's colour that waits at the screen's edge and slides onto its place once that is on the screen, in place of the
/// minimap). The boss's arena while its boss waits and the shop are shown from the start; every other place (its caches,
/// clams, shrines, dens, light rooms, light springs) only once she has found it (been there); and none once it is done
/// (PlaneWorld.PoiDone: nothing left there; a light room opened; a spring spent with no energy of hers to lay).
/// </summary>
public partial class TopDownMain
{
    EdgeArrows _arrows = null!;

    static readonly Color SpringColour = new(1f, 0.93f, 0.7f), LightRoomColour = new(0.7f, 1f, 0.85f);

    /// <summary>How near (metres) counts as at a spring (its emblem fades: EdgeArrows).</summary>
    const float SpringReach = 3f;

    /// <summary>A place's sign on its emblem (the cubes' signs: "$" a shop, "*" a treasure cave, "o" a shell cache, "%" a crab's deal).</summary>
    static string Sign(PoiKind kind) => kind switch
    {
        PoiKind.Haven => "+",
        PoiKind.TreasureCave => "*",
        PoiKind.Shop => "$",
        PoiKind.ShellCache => "o",
        PoiKind.CrabDeal => "%",
        PoiKind.Shrine => "^",
        _ => "!",
    };

    IEnumerable<(System.Numerics.Vector2 At, Color Colour, string Sign, float Reach)> ArrowTargets()
    {
        for (int i = 0; i < _map.Pois.Count; i++)
        {
            var poi = _map.Pois[i];
            if (!_map.Shown(poi) || poi.Kind is PoiKind.Start or PoiKind.Relic or PoiKind.Secret or PoiKind.Hole) continue;
            // A boss's arena while its boss waits; no other way out (she dives anywhere).
            if (poi.Kind == PoiKind.Exit)
            {
                if (_map.HasBoss && _world.Boss is { Freed: false }) yield return (poi.Position, ReefKinds.Colour(ReefKind.Boss), "X", poi.Radius);
                continue;
            }
            bool found = poi.Kind == PoiKind.Shop || i < _world.Visited.Length && _world.Visited[i];
            if (!found || _world.PoiDone(i)) continue;
            yield return (poi.Position, LevelView.PoiColor(poi.Kind), Sign(poi.Kind), Mathf.Max(poi.Radius, SpringReach));
        }
        // Light rooms while they are shut ("~").
        foreach (var room in _world.LightRooms)
            if (room.Found && !room.Opened) yield return (room.Centre, LightRoomColour, "~", FloraTuning.Ring + 1f);
        // Her light springs: where she heals and grows ("+"), once she has found one (before, it lies faint on the floor).
        foreach (var sp in _world.Springs)
            if (sp.Found && (!sp.Spent || _world.Run.Energy > 0f)) yield return (sp.Position, SpringColour, "+", SpringReach);
    }

    /// <summary>The arrows each frame (the crossover's, in place of the minimap).</summary>
    void SyncArrows(float dt)
    {
        _arrows.Visible = OctoShoots.Core.Run.Crossover.On && !InLabyrinth;
        if (!_arrows.Visible) return;
        _arrows.SetTargets(ArrowTargets().ToList());
        _arrows.Sync(_camera.Camera, Mathf.Max(_hudRoot.Scale.X, 0.01f), dt, _bell.GlobalPosition);
    }
}
