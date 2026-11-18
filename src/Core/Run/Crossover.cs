namespace OctoShoots.Core.Run;

/// <summary>
/// The crossover (docs/proposals/CROSSOVER.md): the room-and-template roguelite the `inkdeep-crossover` branch is
/// building out of the dive. While it is being built, the systems it cuts are shelved rather than deleted
/// (CROSSOVER.md §11): havens, leak caves and curse dens, requests and chains, skill points, the chart.
/// <para>
/// The game runs with <see cref="On"/>. The sim's own rules take the switch as a parameter instead
/// (<c>LevelPlan.For</c>, <c>TopDownGenerator.Generate</c>: <c>crossover</c>), defaulting to the dive's rules, so
/// the existing tests keep testing the dive and the crossover's tests ask for it by name.
/// </para>
/// </summary>
public static class Crossover
{
    /// <summary>The game plays the crossover's rules.</summary>
    public static bool On => true;
}
