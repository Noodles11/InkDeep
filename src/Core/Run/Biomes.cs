using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Creatures;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Run;

/// <summary>The crossover's biomes (docs/proposals/CROSSOVER.md §8): one a depth, two at a branch.</summary>
public enum Biome { SunlitReef, KelpForest, Shipwreck, SunkenRuins }

/// <summary>A biome's creatures, by the mob mark's tag they fill (MobTag).</summary>
public sealed record Roster(EnemyKind[] Lone, EnemyKind[] Pack, EnemyKind[] Thrower, EnemyKind[] Ambusher, EnemyKind[] Guard);

/// <summary>
/// The crossover's route (docs/proposals/CROSSOVER.md §8): which biome a level is, where each level leads, and which
/// boss waits in a biome's arena. Depth 1 is the Sunlit Reef; its boss's arena opens two vortices, to the Kelp Forest
/// (branch 0) or the Shipwreck Graveyard (branch 1), and the branch she took holds for the whole depth
/// (<see cref="LevelId.Branch"/>); both lead on to the Sunken Ruins. Deeper depths are the Ruins until their own biomes
/// are built (step 8).
/// </summary>
public static class Biomes
{
    /// <summary>A biome's name as she reads it.</summary>
    public static string Name(Biome biome) => biome switch
    {
        Biome.KelpForest => "Kelp Forest",
        Biome.Shipwreck => "Shipwreck Graveyard",
        Biome.SunkenRuins => "Sunken Ruins",
        _ => "Sunlit Reef",
    };

    /// <summary>A level's biome: by its depth as the player counts them, and at a branch the way she chose.</summary>
    public static Biome Of(LevelId id) => id.IsTutorial ? Biome.SunlitReef : id.ShownDepth switch
    {
        <= 1 => Biome.SunlitReef,
        2 => id.Branch == 1 ? Biome.Shipwreck : Biome.KelpForest,
        _ => Biome.SunkenRuins,
    };

    /// <summary>How many ways a depth's boss arena opens down (2 at a branch: the first depth's, to depth 2a or 2b).</summary>
    public static int WaysBelow(int shownDepth) => shownDepth == 1 ? 2 : 1;

    /// <summary>
    /// Where a crossover level leads, in the order of its holes left to right: an ordinary level to the next row of its
    /// depth (the graph's first exit, her branch kept); a boss level to the next depth's first level, once a way
    /// (branch 0), or at a branch one way for each biome below it.
    /// </summary>
    public static IReadOnlyList<LevelId> Exits(SeedCode seed, LevelId id)
    {
        var graph = DiveGraph.Of(seed);
        var first = graph.ExitsOf(id).OrderBy(e => graph.Node(e).X).ThenBy(e => e.Lane).First();
        if (graph.KindOf(id) != ReefKind.Boss) return new[] { first with { Branch = id.Branch } };
        return Enumerable.Range(0, WaysBelow(id.ShownDepth)).Select(b => first with { Branch = b }).ToArray();
    }

    /// <summary>The neighbour the Leak made a biome's boss (FolkBosses), or null for the depth's own (Queen Clam, Drippus Prime).</summary>
    public static string? BossFolk(Biome biome) => biome switch
    {
        Biome.KelpForest => "tull",
        Biome.Shipwreck => "kip",
        Biome.SunkenRuins => "ottilie",
        _ => null,
    };

    /// <summary>
    /// A biome's roster (CROSSOVER.md §9): the reef's creatures each mob mark's tag (MobTag) takes there, the real
    /// animals of the place. The Sunlit Reef's are the common reef's; the Kelp Forest's barracuda hunt the kelp and its
    /// urchin beds lie in wait; the Shipwreck's morays and crabs keep its holds; the Ruins' morays hide in its walls.
    /// </summary>
    public static Roster Roster(Biome biome) => biome switch
    {
        Biome.KelpForest => new Roster(
            Lone: new[] { EnemyKind.Barracuda },
            Pack: new[] { EnemyKind.MoonJelly, EnemyKind.SpanishDancer },
            Thrower: new[] { EnemyKind.Pufferling },
            Ambusher: new[] { EnemyKind.SeaUrchin },
            Guard: new[] { EnemyKind.Barracuda, EnemyKind.Crabby }),
        Biome.Shipwreck => new Roster(
            Lone: new[] { EnemyKind.Barracuda },
            Pack: new[] { EnemyKind.Pufferling, EnemyKind.MoonJelly },
            Thrower: new[] { EnemyKind.Pufferling },
            Ambusher: new[] { EnemyKind.Moray, EnemyKind.Crabby },
            Guard: new[] { EnemyKind.Crabby, EnemyKind.Barracuda }),
        Biome.SunkenRuins => new Roster(
            Lone: new[] { EnemyKind.Barracuda, EnemyKind.SpanishDancer },
            Pack: new[] { EnemyKind.MoonJelly },
            Thrower: new[] { EnemyKind.Pufferling },
            Ambusher: new[] { EnemyKind.Moray },
            Guard: new[] { EnemyKind.Barracuda, EnemyKind.Crabby }),
        _ => new Roster(
            Lone: Reef, Pack: Reef, Thrower: new[] { EnemyKind.Pufferling },
            Ambusher: new[] { EnemyKind.SeaUrchin, EnemyKind.Moray }, Guard: Reef),
    };

    static readonly EnemyKind[] Reef = { EnemyKind.SpanishDancer, EnemyKind.Pufferling, EnemyKind.Barracuda, EnemyKind.MoonJelly };

    /// <summary>Which treasure's biome a treasure's record names (data/treasures.json's "biome").</summary>
    public static Biome? Parse(string name) =>
        Enum.GetValues<Biome>().Cast<Biome?>().FirstOrDefault(b => string.Equals(Name(b!.Value), name, StringComparison.OrdinalIgnoreCase));
}
