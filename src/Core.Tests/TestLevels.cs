using System;
using System.Linq;
using System.Collections.Concurrent;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Tests;

/// <summary>Levels for the sim tests, picked by what their plan holds (plans are cheap; levels are generated once).</summary>
static class TestLevels
{
    public static readonly SeedCode Seed = SeedCode.Parse("KELP7Q2Z");
    static readonly ConcurrentDictionary<LevelId, LevelMap> Maps = new();

    public static LevelMap Get(LevelId id) => Maps.GetOrAdd(id, i => TopDownGenerator.Generate(new RunStreams(Seed), i));

    /// <summary>A level on the general rules alone (the tutorial's, or the default template's): what the mechanics tests assume.</summary>
    public static bool General(LevelPlan p) => p.Template is null || p.Template.Id == "default";

    /// <summary>The seed's seeded reefs, from the tutorial on, row by row and lane by lane (Run.DiveGraph; no havens).</summary>
    public static System.Collections.Generic.IEnumerable<LevelId> Reefs()
    {
        var graph = DiveGraph.Of(Seed);
        for (int row = 1; row <= DiveGraph.RowsOf(LevelPlan.Tutorial); row++) yield return graph.Row(1, LevelPlan.Tutorial, row)[0].Id;
        for (int cycle = 1; cycle <= 2; cycle++)
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        for (int row = 1; row <= DiveGraph.RowsOf(depth); row++)
            foreach (var node in graph.Row(cycle, depth, row))
                if (node.Kind != ReefKind.Haven) yield return node.Id;
    }

    /// <summary>The first reef of the seed's whose plan matches (from the tutorial on).</summary>
    public static LevelId Find(Func<LevelPlan, bool> match)
    {
        foreach (var id in Reefs().Take(120))
            if (match(LevelPlan.For(Seed, id))) return id;
        throw new InvalidOperationException("no such reef in the seed's first 120");
    }

    /// <summary>Every reef of the seed's first 120 whose plan matches, in order.</summary>
    public static System.Collections.Generic.IEnumerable<LevelId> All(Func<LevelPlan, bool> match)
    {
        foreach (var id in Reefs().Take(120))
            if (match(LevelPlan.For(Seed, id))) yield return id;
    }

    /// <summary>A level with a shop.</summary>
    /// <summary>A haven (docs/DIVE.md §4), which holds the shop: the k-th of a few made from the seed.</summary>
    public static LevelMap Haven(int k = 0) => Havens.GetOrAdd(k, i =>
        TopDownGenerator.GenerateHaven(Seed, new LevelId(701, LevelPlan.Tutorial + 1 + i % 4, 1 + i), System.Numerics.Vector2.UnitX));

    static readonly ConcurrentDictionary<int, LevelMap> Havens = new();

    public static LevelMap WithShop() => Haven();

    /// <summary>A level with exactly one treasure room, and the level after it.</summary>
    public static LevelMap OneTreasure() => Get(Find(p => p.Treasures == 1 && !p.HasBoss));

    public static LevelMap After(LevelMap map) => Get(DiveGraph.Of(Seed).ExitsOf(map.Id)[0]);
}
