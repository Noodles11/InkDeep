using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// What the arrows at the screen's edges point at in the crossover (EdgeArrows, in place of the minimap): the level's
/// places in their colours (its rooms, caches, shrines), its light springs (where she heals and grows) while they hold
/// healing or she has energy to lay, and the boss's arena while its boss waits.
/// </summary>
public partial class TopDownMain
{
    EdgeArrows _arrows = null!;

    static readonly Color SpringColour = new(1f, 0.93f, 0.7f);

    IEnumerable<(System.Numerics.Vector2 At, Color Colour)> ArrowTargets()
    {
        foreach (var poi in _map.Pois)
        {
            if (!_map.Shown(poi) || poi.Kind is PoiKind.Start or PoiKind.Relic or PoiKind.Secret or PoiKind.Hole) continue;
            // A boss's arena while its boss waits; no other way out (she dives anywhere).
            if (poi.Kind == PoiKind.Exit)
            {
                if (_map.HasBoss && _world.Boss is { Freed: false }) yield return (poi.Position, ReefKinds.Colour(ReefKind.Boss));
                continue;
            }
            yield return (poi.Position, LevelView.PoiColor(poi.Kind));
        }
        foreach (var sp in _world.Springs)
            if (!sp.Spent || _world.Run.Energy > 0f) yield return (sp.Position, SpringColour);
    }

    /// <summary>The arrows each frame (the crossover's, in place of the minimap).</summary>
    void SyncArrows(float dt)
    {
        _arrows.Visible = OctoShoots.Core.Run.Crossover.On && !InLabyrinth;
        if (!_arrows.Visible) return;
        _arrows.SetTargets(ArrowTargets().ToList());
        _arrows.Sync(_camera.Camera, Mathf.Max(_hudRoot.Scale.X, 0.01f), dt);
    }
}
