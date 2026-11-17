using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>A secret's cracked seal (docs/DIVE.md §6.1; PlaneCrackSeals.cs). Numbers are first guesses, tuned in play.</summary>
public static class CrackSealTuning
{
    /// <summary>How far into the passage its face stands, and how thick it is (m).</summary>
    public const float Inset = 0.45f, Thick = 0.35f;
    /// <summary>How far past the passage's sides it runs into the rock either side (m).</summary>
    public const float Overlap = 0.4f;
    /// <summary>Within this of its middle across (m) her dash finds the crack; this near its face she has reached it.</summary>
    public const float Crack = 0.75f, Touch = 0.3f;
}

/// <summary>
/// A secret's way in, sealed (docs/DIVE.md §6.1): a continuous wall of the reef's rock across its passage, just inside
/// its mouth, a vertical crack down its middle glowing faintly from inside. It stands as rock (WeakRock: it stops her,
/// the creatures and her shots). Only her dash driven into its crack gets through: then it crumbles for good.
/// </summary>
public sealed class CrackSeal
{
    public Doorway Mouth;
    public WeakRock Rock;
    public bool Open;
}

public sealed partial class PlaneWorld
{
    public List<CrackSeal> CrackSeals { get; } = new();

    /// <summary>A secret's mouth (on this world's map) sealed with a cracked wall.</summary>
    public CrackSeal PlaceCrackSeal(Doorway mouth)
    {
        var into = Vector2.Normalize(mouth.Into);
        var rock = new WeakRock(mouth.At + into * (CrackSealTuning.Inset + CrackSealTuning.Thick), Doorway.HalfWidth + CrackSealTuning.Overlap, -1,
            Facing: -into, Thick: CrackSealTuning.Thick, Kind: SealKind.Secret);
        var seal = new CrackSeal { Mouth = mouth, Rock = rock };
        CrackSeals.Add(seal);
        _rocks.Add(rock);
        return seal;
    }

    /// <summary>Her dash driven into a seal's crack (heading in, at its middle): it crumbles for good.</summary>
    void StepCrackSeals()
    {
        if (CrackSeals.Count == 0 || Defeated) return;
        var p = Player;
        if (!p.IsDashing) return;
        foreach (var seal in CrackSeals)
        {
            if (seal.Open || !seal.Rock.Contains(p.Position, Radius + CrackSealTuning.Touch)) continue;
            var into = -seal.Rock.Facing;
            float across = Vector2.Dot(p.Position - seal.Rock.Center, seal.Rock.Across);
            var heading = p.Velocity.LengthSquared() > 1e-4f ? Vector2.Normalize(p.Velocity) : p.LastMoveDir;
            if (MathF.Abs(across) > CrackSealTuning.Crack || Vector2.Dot(heading, into) < 0.3f) continue;
            seal.Open = true;
            _rocks.Remove(seal.Rock);
            Events.Add(new PlaneEvent(PlaneEventType.CrackSealCrumbled, seal.Rock.Center, into, seal.Rock.Radius));
        }
    }
}
