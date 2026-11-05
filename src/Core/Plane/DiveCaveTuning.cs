namespace OctoShoots.Core.Plane;

/// <summary>A dive's leak caves (docs/DIVE.md §6).</summary>
public static class DiveCaveTuning
{
    /// <summary>How fast the Leak's water drinks her light while its leak is open (HP a second): about a minute of her at the start.</summary>
    public const float Sting = 1.6f;
    /// <summary>A cave's Menace over its level's.</summary>
    public const float Menace = 0.1f;
    /// <summary>Its share of a level's water: half.</summary>
    public const float Area = 0.5f;
}
