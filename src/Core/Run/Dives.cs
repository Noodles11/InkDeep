using System;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Run;

/// <summary>A jelly's dives (docs/DIVE.md): each new one's seed and where it starts (the way down: DiveGraph).</summary>
public static class Dives
{
    /// <summary>A new dive's seed: from her own seed and how many dives she has ended, so each is a different reef.</summary>
    public static SeedCode SeedOf(Jelly jelly)
    {
        ulong h = 1469598103934665603UL;
        foreach (char c in jelly.Seed) h = (h ^ c) * 1099511628211UL;
        h = (h ^ (ulong)(jelly.Dives + 1)) * 1099511628211UL;
        h ^= h >> 29;
        return SeedCode.FromValue(h);
    }

    /// <summary>Where a new dive starts: the tutorial until she has passed it, then Depth 1.</summary>
    public static LevelId StartOf(Jelly jelly) => jelly.Tutored ? LevelId.FirstRun : LevelId.First;

    /// <summary>
    /// The neighbour waiting as a depth's boss (docs/DIVE.md §5): Depth 1 Grandmother Tull, then Kip, Fennel, Ottilie and
    /// Bubbles, each until she is freed (then the depth's boss is the Leak's own again, as is Depth 6's). Null: none.
    /// </summary>
    public static string? FolkBossOf(LevelId id, Jelly jelly)
    {
        int depth = id.ShownDepth;
        if (id.Haven || id.IsTutorial || depth < 1 || depth > OctoShoots.Core.Plane.Folk.All.Length) return null;
        string folk = OctoShoots.Core.Plane.Folk.All[depth - 1].Id;
        return jelly.Freed.Contains(folk) ? null : folk;
    }
}
