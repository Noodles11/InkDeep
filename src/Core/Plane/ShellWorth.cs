using System;

namespace OctoShoots.Core.Plane;

/// <summary>The shells she collects, by worth (docs/proposals/CROSSOVER.md §5.1): the rarer, the more.</summary>
public enum ShellKind { Cowrie, Scallop, Conch, Nautilus }

/// <summary>
/// What each shell is worth, and which one a find holds (CROSSOVER.md §5.1). The rarer shells lie deeper and better
/// hidden: a find's richness (0 a freed creature's, 1 a pot's, 2 a cache's, 3 a bubble vault's) and the depth both
/// tip the roll toward them. Only a template level (LevelMap.Templated) rolls worths; elsewhere every shell is a
/// cowrie worth one, as the dive's always were.
/// </summary>
public static class ShellWorth
{
    public static int Of(ShellKind kind) => kind switch
    {
        ShellKind.Cowrie => 1,
        ShellKind.Scallop => 3,
        ShellKind.Conch => 8,
        _ => 25,
    };

    /// <summary>The shell a counted worth was (a lucky shell counts one more than its kind's worth).</summary>
    public static ShellKind KindOf(int value) =>
        value >= 25 ? ShellKind.Nautilus : value >= 8 ? ShellKind.Conch : value >= 3 ? ShellKind.Scallop : ShellKind.Cowrie;

    /// <summary>The kinds of find, from poorest to richest.</summary>
    public const int Freed = 0, Pot = 1, Cache = 2, Vault = 3;

    /// <summary>
    /// One shell of a find: the chance of each rarer kind grows with the find's richness and with the depth (0 at the
    /// first depth, 1 at the last: LevelPlan's menace).
    /// </summary>
    public static ShellKind Roll(Rng rng, int richness, float menace)
    {
        float deep = Math.Clamp(menace, 0f, 1.5f);
        float nautilus = 0.004f * richness * richness * (1f + deep);
        float conch = 0.03f * richness * (1f + deep);
        float scallop = 0.12f + 0.08f * richness + 0.06f * deep;
        float r = rng.NextFloat();
        if (r < nautilus) return ShellKind.Nautilus;
        if (r < nautilus + conch) return ShellKind.Conch;
        if (r < nautilus + conch + scallop) return ShellKind.Scallop;
        return ShellKind.Cowrie;
    }
}
