using System;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The dive clock (docs/proposals/CROSSOVER.md §3): on a template level (LevelMap.Templated) she may dive anywhere once
/// she has spent <see cref="Seconds"/> on it, with nothing else to do first; there is no going back up.
/// </summary>
public static class DiveClock
{
    public const float Seconds = 30f;

    /// <summary>How far the clock has run, 0 → 1.</summary>
    public static float Progress(float time) => Math.Clamp(time / Seconds, 0f, 1f);

    /// <summary>Whether the dive is open after so long on the level.</summary>
    public static bool Open(float time) => time >= Seconds;
}

