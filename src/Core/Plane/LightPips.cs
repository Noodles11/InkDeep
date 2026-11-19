using System;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light as pips (docs/proposals/CROSSOVER.md §6): with PlaneRun.Pips, her health counts in half pips of
/// <see cref="Half"/> each (6 pips at the 100 she starts with; more as pearls raise it). A hit takes whole half pips
/// (one to four, by how hard it was), so hits read and count; a drain (a murkling clinging) still drinks steadily,
/// and her pips show it rounded up. A healing orb gives back a whole pip.
/// </summary>
public static class LightPips
{
    /// <summary>What half a pip is worth on her health.</summary>
    public const float Half = 100f / 12f;
    /// <summary>A hit's damage (after the menace) per half pip it takes, and the most halves one hit takes.</summary>
    public const float HitStep = 12f;
    public const int HitMost = 4;
    /// <summary>A healing orb's halves (a whole pip), the shop's top-up's (two pips), and what the light shrine asks (two pips).</summary>
    public const int OrbHalves = 2, ShopHalves = 4, ShrineHalves = 4;

    /// <summary>Her half pips now: a part-drunk half counts as whole.</summary>
    public static int Halves(float hp) => Math.Max(0, (int)MathF.Ceiling(hp / Half - 1e-3f));

    /// <summary>How many half pips her health holds at most.</summary>
    public static int MaxHalves(float maxHp) => Math.Max(2, (int)MathF.Round(maxHp / Half));

    /// <summary>The half pips a hit of this damage takes.</summary>
    public static int HitHalves(float damage) => Math.Clamp((int)MathF.Round(damage / HitStep), 1, HitMost);

    /// <summary>Her health with so many half pips added (taken, if negative), snapped to whole halves, within 0 and her most.</summary>
    public static float Add(float hp, int halves, float maxHp) => Math.Clamp((Halves(hp) + halves) * Half, 0f, maxHp);
}
