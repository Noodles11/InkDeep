using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>The colours and names of the places a dive holds, on the chart and the HUD (docs/LIGHT-AND-GRAPH.md §6.3).</summary>
public static class ReefKinds
{
    public static Color Colour(ReefKind kind) => kind switch
    {
        ReefKind.Haven => new Color(0.55f, 1f, 0.72f),       // Lantern Bay's calm green
        ReefKind.Boss => new Color(0.78f, 0.42f, 1f),        // the Leak's violet: a neighbour held by it
        ReefKind.Treasure => LevelView.PoiColor(PoiKind.TreasureCave),
        ReefKind.Den => LevelView.PoiColor(PoiKind.CurseDen),
        _ => new Color(0.72f, 0.88f, 1f),                    // a plain reef: pale water
    };

    /// <summary>What lies down a hole, as the dive prompt says it.</summary>
    public static string Below(ReefKind kind) => kind switch
    {
        ReefKind.Haven => "Lantern Bay below",
        ReefKind.Boss => "the depth's boss below",
        ReefKind.Treasure => "a clam room below",
        ReefKind.Den => "a curse den below",
        _ => "a reef below",
    };

    /// <summary>The chart's name for a kind.</summary>
    public static string Name(ReefKind kind) => kind switch
    {
        ReefKind.Haven => "Haven",
        ReefKind.Boss => "Boss",
        ReefKind.Treasure => "Clam room",
        ReefKind.Den => "Curse den",
        _ => "Reef",
    };
}
