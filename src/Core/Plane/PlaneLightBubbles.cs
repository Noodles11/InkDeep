using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light bubbles (docs/LIGHT-AND-GRAPH.md §2.3; PlaneCombat's ThrowLightBubble), her attack while her beam is her lamp:
/// a clear sphere of her light, the water burning round it, thrown along her aim while fire is held. Each number is a
/// stat of hers too (her shot speed, shot size, damage, fire rate and range scale it), for pearls to alter later.
/// </summary>
public static class LightBubbleTuning
{
    /// <summary>How fast one flies (m/s), steady all its way.</summary>
    public const float Speed = 20f;
    /// <summary>Its radius: a third of a lesser murkling's (m).</summary>
    public const float Radius = MurkSizeTuning.Least / 3f;
    /// <summary>What it takes off a creature's corruption when it strikes (a murkling holds 36).</summary>
    public const float Damage = 5f;
    /// <summary>How many she throws a second while fire is held.</summary>
    public const float Rate = 4f;
    /// <summary>How far one flies before it pops on its own (m).</summary>
    public const float Range = 18f;
    /// <summary>Out of her beam's light, it strikes for this share of its damage (its light fainter).</summary>
    public const float DarkShare = 0.5f;
    /// <summary>
    /// Its push on what it strikes (m/s, before easing off at KnockTuning.Decay): Knock × √(the damage dealt over the
    /// health the creature has left, over its mass), within Least–Most of Knock. Its mass: (its radius over a lesser
    /// murkling's)². So a fresh creature is nudged, one nearly freed thrown back, a big one less than a small.
    /// </summary>
    public const float Knock = 8f, KnockLeast = 0.15f, KnockMost = 1.3f;
}

public sealed partial class PlaneWorld
{
    /// <summary>
    /// A shot of hers has struck a creature of this radius for <paramref name="dealt"/>, leaving it <paramref name="left"/>:
    /// a light bubble pushes it back by that ratio (LightBubbleTuning.Knock), with a wave of hot water where it is struck
    /// (BubbleKnock); any other shot shoves it as shots do (Shove).
    /// </summary>
    void ShotShove(PlaneShot shot, ref Vector2 knock, Vector2 at, float radius, float dealt, float left)
    {
        if (!shot.Straight)
        {
            Shove(ref knock, shot.Velocity, radius);
            return;
        }
        if (dealt <= 0f || shot.Velocity.LengthSquared() < 1e-8f) return;
        float mass = MathF.Pow(MathF.Max(radius, 0.05f) / MurkSizeTuning.Least, 2f);
        float ratio = dealt / MathF.Max(left, 0.5f) / mass;
        float speed = LightBubbleTuning.Knock * Math.Clamp(MathF.Sqrt(ratio), LightBubbleTuning.KnockLeast, LightBubbleTuning.KnockMost)
            * Run.Loadout.Stats[OctoShoots.Core.Items.Stat.Knockback];
        var dir = Vector2.Normalize(shot.Velocity);
        knock += dir * speed;
        float most = LightBubbleTuning.Knock * LightBubbleTuning.KnockMost * KnockTuning.Stack;
        if (knock.LengthSquared() > most * most) knock = Vector2.Normalize(knock) * most;
        Events.Add(new PlaneEvent(PlaneEventType.BubbleKnock, at, dir, speed));
    }

    /// <summary>Whether a light bubble is in her beam's light now (else it strikes for DarkShare, its light fainter).</summary>
    bool InHerBeam(PlaneShot shot)
    {
        if (!BeamOn || Defeated || Stains.Count > 0 && Stained(shot.Position)) return false;
        foreach (var (beam, _) in HerBeams)
            if (BeamLight(beam, shot.Position, 0f, shot.Radius, shot) > 0f) return true;
        return false;
    }
}
