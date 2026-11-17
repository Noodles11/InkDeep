namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light bubbles (docs/LIGHT-AND-GRAPH.md §2.3; PlaneCombat's ThrowLightBubble), her attack while her beam is her lamp:
/// a clear sphere of white light, the water burning round it, thrown along her aim while fire is held. Each number is a
/// stat of hers too (her shot speed, shot size, damage, fire rate and range scale it), for pearls to alter later.
/// </summary>
public static class LightBubbleTuning
{
    /// <summary>How fast one flies (m/s), steady all its way.</summary>
    public const float Speed = 20f;
    /// <summary>Its radius: a lesser murkling's (m).</summary>
    public const float Radius = MurkSizeTuning.Least;
    /// <summary>What it takes off a creature's corruption when it strikes (a murkling holds 36).</summary>
    public const float Damage = 5f;
    /// <summary>How many she throws a second while fire is held.</summary>
    public const float Rate = 4f;
    /// <summary>How far one flies before it pops on its own (m).</summary>
    public const float Range = 18f;
}
