using System;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Run;

/// <summary>How often a dive's havens come (docs/DIVE.md §4).</summary>
public static class HavenTuning
{
    /// <summary>After this many levels without a haven one may come, this often; after Surely levels, always.</summary>
    public const int Maybe = 2, Surely = 3;
    public const float Chance = 0.35f;
}

/// <summary>A jelly's dives (docs/DIVE.md): each new one's seed, where it starts, and the way down through it.</summary>
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

    /// <summary>
    /// What her hole leads to: from a haven, the level it stands before; from a level, the next level, or a haven before
    /// it when one comes (never in the tutorial, nor straight after a haven).
    /// </summary>
    public static LevelId NextOf(SeedCode seed, LevelId id)
    {
        if (id.Haven) return id with { Haven = false };
        var next = LevelPlan.NextOf(seed, id);
        return HavenBefore(seed, next) ? next with { Haven = true } : next;
    }

    /// <summary>
    /// Whether a haven comes before this level: counting the levels since the last haven from the top of the dive (Depth
    /// 1, Level 1), after Maybe of them a haven comes Chance of the time (rolled from the seed and the level), after Surely
    /// always.
    /// </summary>
    public static bool HavenBefore(SeedCode seed, LevelId level)
    {
        if (level.IsTutorial || level == LevelId.FirstRun) return false;
        int since = 0;
        var at = LevelId.FirstRun;
        // (A dive runs its depths in order, cycle after cycle: walk them to this level.)
        for (int guard = 0; guard < 10_000; guard++)
        {
            since++;
            var next = LevelPlan.NextOf(seed, at);
            bool haven = since >= HavenTuning.Surely || (since >= HavenTuning.Maybe && Roll(seed, next) < HavenTuning.Chance);
            if (next == level) return haven;
            if (haven) since = 0;
            at = next;
        }
        return false;
    }

    static float Roll(SeedCode seed, LevelId level)
    {
        ulong h = seed.Value * 0x9E3779B97F4A7C15UL ^ ((ulong)level.Cycle << 40) ^ ((ulong)level.Depth << 20) ^ (ulong)level.Level ^ 0xAB1E5EEDUL;
        h ^= h >> 31;
        h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 27;
        return (h >> 40) / (float)(1UL << 24);
    }
}
