using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A murkling's size is its corruption (docs/CORRUPTION.md, Murklings). Each one budded from the ink has a full
/// size of its own, between Least and Most (skewed small: Skew), and corruption to match: its meter is a plain
/// murkling's × its ink round its core against a plain one's. As her light takes its corruption it shrinks toward its
/// glowing core (Core, the size of the core it leaves), and it is freed the moment it is no bigger than its core: its
/// meter empty. Out of every light of hers for Grace seconds (and unhit as long), it grows back toward its full size,
/// Regrow of its meter a second. Its full size is its grade, and its grade its loot:
/// <list type="bullet">
/// <item>lesser (full size under LesserBelow): no shells;</item>
/// <item>regular: RegularShellsMin..Max shells;</item>
/// <item>elite (full size over EliteAbove): EliteShellsMin..Max shells.</item>
/// </list>
/// Every one leaves its core of light (healing her) as before. A prime, a turned fish and Drippus Prime's brood keep
/// their own sizes and loot.
/// </summary>
public static class MurkSizeTuning
{
    /// <summary>Its glowing core's radius (the size of the core of light it leaves, CoreView's new core).</summary>
    public const float Core = 0.275f;
    public const float Least = 0.36f, Most = 0.68f, Skew = 1.4f;
    public const float LesserBelow = 0.42f, EliteAbove = 0.6f;
    public const float Grace = 1f, Regrow = 0.12f;
    public const int RegularShellsMin = 1, RegularShellsMax = 2, EliteShellsMin = 5, EliteShellsMax = 10;

    /// <summary>A full size from its seed (0..100): even, steady, and drawing nothing from the level's dice.</summary>
    public static float FullOf(float seed)
    {
        float h = MathF.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        float t = h - MathF.Floor(h);
        return Least + (Most - Least) * MathF.Pow(t, Skew);
    }

    public static MurkGrade GradeOf(float full) => full < LesserBelow ? MurkGrade.Lesser : full > EliteAbove ? MurkGrade.Elite : MurkGrade.Regular;

    /// <summary>Its meter against a plain murkling's: its ink round its core against a plain one's.</summary>
    public static float MeterScale(float full) => (full - Core) / (BlightTuning.MurkRadius - Core);
}

/// <summary>A sized murkling's grade, by its full size: what it leaves when freed.</summary>
public enum MurkGrade { Regular, Lesser, Elite }

public sealed partial class PlaneWorld
{
    /// <summary>A murkling budded from the ink is given its full size (× growth: the Menace's, in the adventure), its grade and its meter (Bud).</summary>
    static void Size(Murkling m, float menaceMeter, float growth = 1f)
    {
        m.Full = MurkSizeTuning.FullOf(m.Seed) * growth;
        m.Grade = MurkSizeTuning.GradeOf(m.Full);
        m.Corruption.Max = m.Corruption.Value = menaceMeter * MurkSizeTuning.MeterScale(m.Full);
        m.Radius = m.Body = m.Full;
    }

    /// <summary>How long a creature has been out of every light of hers (long, if her light never found it).</summary>
    float DarkFor(object kin) => _glare.TryGetValue(kin, out var g) ? g.Dark : float.MaxValue;

    /// <summary>
    /// The sized murklings (after her light's step): grown back in the dark once Grace has passed unlit and unhit, and
    /// each as big as its corruption: its core, and its share of the rest of its full size.
    /// </summary>
    void StepMurkSizes()
    {
        foreach (var m in Murklings)
        {
            if (!m.Alive || !m.Sized) continue;
            var meter = m.Corruption;
            if (!m.Budding && meter.Value < meter.Max && DarkFor(m) >= MurkSizeTuning.Grace && meter.Since >= MurkSizeTuning.Grace)
                meter.Value = MathF.Min(meter.Max, meter.Value + MurkSizeTuning.Regrow * meter.Max * Dt);
            m.Radius = m.Body = MurkSizeTuning.Core + (m.Full - MurkSizeTuning.Core) * meter.Fraction;
        }
    }

    /// <summary>The shells a sized murkling leaves by its grade (none for a lesser one).</summary>
    void ShellsByGrade(Murkling m)
    {
        int shells = m.Grade switch
        {
            MurkGrade.Lesser => 0,
            MurkGrade.Elite => MurkSizeTuning.EliteShellsMin + _drops.Int(MurkSizeTuning.EliteShellsMax - MurkSizeTuning.EliteShellsMin + 1),
            _ => MurkSizeTuning.RegularShellsMin + _drops.Int(MurkSizeTuning.RegularShellsMax - MurkSizeTuning.RegularShellsMin + 1),
        };
        for (int i = 0; i < shells; i++) Shells.Add(new PlaneShell { Position = m.Position, Velocity = Fling(2f, shells > 2 ? 5f : 4f) });
    }
}
