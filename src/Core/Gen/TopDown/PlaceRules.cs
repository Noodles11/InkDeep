using System;
using System.Collections.Generic;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>A place's sort (docs/LIGHT-AND-GRAPH-PROPOSAL.md §7): a spot on the reef, or a way into a sub-level.</summary>
public enum PlaceSort { Spot, SubLevel }

/// <summary>
/// One rule for each kind of place in a reef (docs/LIGHT-AND-GRAPH-PROPOSAL.md §7): its sort, the first row of a depth it
/// may stand in, how often a reef holds one (the chance its plan rolls; 0: it comes another way, named in Comes), and how
/// far it keeps from any other place (two caves, or two holes, keep further: CaveApart, HoleApart). Every place keeps
/// FromStart from the start and FromExit from the exit. LevelPlan rolls them; CaveLayout spaces them; LevelValidator
/// holds a level to them.
/// </summary>
public static class PlaceRules
{
    public readonly record struct Rule(PoiKind Kind, PlaceSort Sort, int FromRow, float Chance, float Apart, string Comes = "");

    /// <summary>How far every place keeps from the start and the exit, from any other, and two caves or two holes from each other.</summary>
    public const float FromStart = 30f, FromExit = 30f, Apart = 17f, CaveApart = 20f, HoleApart = 28f;
    /// <summary>The early place (the one she meets first) stands within this far of the start.</summary>
    public const float EarlyMax = 38f;
    /// <summary>Past the first depth, this often a depth hides one Crab Deal, on one reef of a row short of its boss.</summary>
    public const float CrabDepth = 0.6f;

    public static readonly Rule Secret = new(PoiKind.Secret, PlaceSort.SubLevel, 1, 0.4f, Apart);
    public static readonly Rule Shop = new(PoiKind.Shop, PlaceSort.Spot, 2, 0.45f, Apart);
    public static readonly Rule ShellCache = new(PoiKind.ShellCache, PlaceSort.Spot, 2, 0.6f, Apart);
    public static readonly Rule CrabDeal = new(PoiKind.CrabDeal, PlaceSort.Spot, 2, 0f, Apart, "one reef a depth, CrabDepth of depths past the first");
    public static readonly Rule TreasureCave = new(PoiKind.TreasureCave, PlaceSort.Spot, 1, 0f, Apart, "the reef's kind (Run.ReefPlaces.Treasure)");
    public static readonly Rule LeakCave = new(PoiKind.LeakCave, PlaceSort.SubLevel, 2, 0f, Apart, "the reef's kind (Run.ReefPlaces.Den)");
    public static readonly Rule Hole = new(PoiKind.Hole, PlaceSort.Spot, 1, 0f, Apart, "one for each of the reef's exits past the first");

    public static readonly IReadOnlyList<Rule> All = new[] { Secret, Shop, ShellCache, CrabDeal, TreasureCave, LeakCave, Hole };

    public static Rule Of(PoiKind kind)
    {
        foreach (var rule in All)
            if (rule.Kind == kind) return rule;
        return new Rule(kind, PlaceSort.Spot, 1, 0f, Apart);
    }

    /// <summary>Whether a reef of this row may hold this kind of place, and its plan's roll for one (the roll drawn only where it may).</summary>
    public static bool Rolls(PoiKind kind, int row, Rng rng) => row >= Of(kind).FromRow && rng.NextFloat() < Of(kind).Chance;

    /// <summary>How far apart two places keep.</summary>
    public static float Between(PoiKind a, PoiKind b) =>
        a == PoiKind.Hole && b == PoiKind.Hole ? HoleApart
        : Poi.IsCaveHosted(a) && Poi.IsCaveHosted(b) ? Math.Max(CaveApart, Math.Max(Of(a).Apart, Of(b).Apart))
        : Math.Max(Of(a).Apart, Of(b).Apart);
}
