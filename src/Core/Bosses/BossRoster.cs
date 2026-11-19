namespace OctoShoots.Core.Bosses;

/// <summary>
/// The bosses met, hardcoded one per depth (docs/BOSS-GENERATOR.md §1: the generator is parked until the hand-made
/// roster grows). For now every depth's boss is Drippus Prime (docs/CORRUPTION.md), tougher the deeper it is met and on
/// later loops. Queen Clam stays the generator's reference roll.
/// </summary>
public static class BossRoster
{
    /// <summary>Health grows by this share at Menace 1 (Depth 6 of the first loop; 0 at Depth 1). Its blows grow as all that hurts her does (PlaneCombatTuning.HurtPerMenace).</summary>
    public const float HpGrowth = 1.5f;

    /// <summary>
    /// The boss of this loop's depth (as the player numbers them, 1–6). <paramref name="crossover"/>: the crossover's
    /// roster (docs/proposals/CROSSOVER.md §8), the Sunlit Reef's boss Queen Clam; the deeper ones Drippus Prime until
    /// their own bosses move in.
    /// </summary>
    public static BossSpec For(ulong runSeed, int cycle, int depth, bool crossover = false, OctoShoots.Core.Run.Biome biome = OctoShoots.Core.Run.Biome.SunlitReef)
    {
        // (The crossover's deeper biomes: the neighbour the Leak made their boss, Run.Biomes.BossFolk, at its depth's
        // Menace as every boss of the roster. Freed, she is the crossover's reward and nobody's neighbour: no home to go to.)
        float menace = OctoShoots.Core.Gen.TopDown.LevelPlan.MenaceOf(new OctoShoots.Core.Gen.TopDown.LevelId(cycle, depth + OctoShoots.Core.Gen.TopDown.LevelPlan.Tutorial, 1));
        var spec = crossover && OctoShoots.Core.Run.Biomes.BossFolk(biome) is { } folk && FolkBosses.For(folk, 1, menace) is { } made ? made
            : crossover && depth == 1 ? BossGenerator.QueenClam(runSeed, cycle, depth) : BossGenerator.DrippusPrime(runSeed, cycle, depth);
        if (crossover) spec.Folk = null;
        spec.Hp *= 1f + HpGrowth * spec.Menace;
        return spec;
    }
}
