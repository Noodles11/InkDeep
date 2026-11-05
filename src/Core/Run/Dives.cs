using System;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Run;

/// <summary>A jelly's dives (docs/DIVE.md §1): each new one's seed and where it starts.</summary>
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
}
