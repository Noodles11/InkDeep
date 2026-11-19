using System;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>The boss level's rising current (docs/proposals/CROSSOVER.md §4): first guesses, tuned in play.</summary>
public static class TideTuning
{
    /// <summary>
    /// Once the level's clock has run (DiveClock.Seconds) the current starts at Start m/s and grows by Rise every Step
    /// seconds, to Most: faster than she swims (Tuning.PlaneCruiseSpeed, 6) within about a minute.
    /// </summary>
    public const float Start = 1f, Rise = 0.5f, Step = 5f, Most = 9f;
    /// <summary>A swept creature dissolves this far past the arena's rim, and each adds this share of the boss's own light to its corruption, to at most FeedMost.</summary>
    public const float Mouth = 3f, Feed = 0.04f, FeedMost = 0.6f;
}

/// <summary>
/// The boss level's current, Spelunky's ghost in the game's own terms (docs/proposals/CROSSOVER.md §4): on a template
/// boss level, once its clock has run and till the boss is freed, the water flows toward the arena from everywhere
/// (down the level's own ways: the gradient of the distance through the water to the arena's middle), stronger every
/// few seconds. It carries everything not fixed to the rock: her, the free-swimming creatures, the reef's fish, shells,
/// orbs, loose pearls and freed light, ink clouds. Rooted things stay (Blightroots, Lashjaws, pots, bubble vaults,
/// charge stones, the shop's stands). Inside the arena it is still, so she can fight. A creature it brings to the arena's
/// mouth dissolves into ink there (none may be in the arena with the boss), and each adds to the boss's corruption.
/// </summary>
public sealed partial class PlaneWorld
{
    float[]? _toArena;
    bool _tideTold;
    float _fed;

    /// <summary>The creatures the current has dissolved at the arena's mouth.</summary>
    public int Dissolved { get; private set; }

    /// <summary>The current runs: a template boss level whose clock has run, its boss not yet freed.</summary>
    public bool TideActive => Map.Templated && Map.HasBoss && Boss is { Freed: false } && DiveClock.Open(Time);

    /// <summary>How fast the current runs now (m/s; 0 when it does not).</summary>
    public float TideStrength => !TideActive ? 0f
        : MathF.Min(TideTuning.Most, TideTuning.Start + TideTuning.Rise * MathF.Floor((Time - DiveClock.Seconds) / TideTuning.Step));

    /// <summary>The current's flow at a point (m/s): toward the arena along the water, none inside it.</summary>
    public Vector2 TideAt(Vector2 p)
    {
        float strength = TideStrength;
        if (strength <= 0f || Vector2.Distance(p, ArenaCenter) < ArenaRadius) return Vector2.Zero;
        _toArena ??= LevelValidator.Distances(Map, ArenaCenter);
        const float h = 0.75f;
        float D(Vector2 q)
        {
            float d = LevelValidator.DistanceAt(_toArena, q);
            return float.IsFinite(d) ? d : float.NaN;
        }
        float here = D(p);
        if (float.IsNaN(here)) return Vector2.Zero;
        // Downhill on the distance: each side's slope where there is water, else the side we have.
        float Slope(Vector2 axis)
        {
            float plus = D(p + axis * h), minus = D(p - axis * h);
            if (!float.IsNaN(plus) && !float.IsNaN(minus)) return (plus - minus) / (2f * h);
            if (!float.IsNaN(plus)) return (plus - here) / h;
            if (!float.IsNaN(minus)) return (here - minus) / h;
            return 0f;
        }
        var grad = new Vector2(Slope(Vector2.UnitX), Slope(Vector2.UnitY));
        if (grad.LengthSquared() < 1e-6f) return Vector2.Zero;
        return -Vector2.Normalize(grad) * strength;
    }

    void StepTide()
    {
        if (!TideActive) return;
        if (!_tideTold)
        {
            _tideTold = true;
            Events.Add(new PlaneEvent(PlaneEventType.TideRose, Player.Position, Vector2.Zero));
        }
        var p = Player;
        if (!Defeated)
        {
            var flow = TideAt(p.Position);
            if (flow != Vector2.Zero) Move(ref p.Position, ref flow, Radius, report: false);
        }
        float mouth = ArenaRadius + TideTuning.Mouth;
        bool AtMouth(Vector2 at) => Vector2.Distance(at, ArenaCenter) < mouth;

        foreach (var m in Murklings)
        {
            if (!m.Alive || m.Budding || m.Clinging) continue;
            Sweep(ref m.Position, m.Radius);
            if (AtMouth(m.Position))
            {
                m.Alive = false;
                Dissolve(m.Position);
            }
        }
        foreach (var mob in Mobs)
        {
            if (!mob.Alive) continue;
            Sweep(ref mob.Position, PufferlingTuning.CalmRadius);
            if (AtMouth(mob.Position))
            {
                mob.Hp = 0f;
                Dissolve(mob.Position);
            }
        }
        foreach (var t in Thumpsquirts)
        {
            if (!t.Alive) continue;
            Sweep(ref t.Position, 0.5f);
            if (AtMouth(t.Position))
            {
                t.Alive = false;
                Dissolve(t.Position);
            }
        }
        foreach (var b in Blotters)
        {
            if (!b.Alive) continue;
            Sweep(ref b.Position, 0.5f);
            if (AtMouth(b.Position))
            {
                b.Alive = false;
                Dissolve(b.Position);
            }
        }
        // The reef's fish ride it too, and keep out of the arena (they are not the corruption's: they feed nothing).
        foreach (var fish in ReefFish)
        {
            if (fish.Gone) continue;
            Drift(ref fish.Position);
            if (AtMouth(fish.Position)) fish.Gone = true;
        }
        foreach (var school in Schools) Drift(ref school.Position);
        foreach (var shell in Shells) Drift(ref shell.Position);
        foreach (var heart in Hearts) Drift(ref heart.Position);
        foreach (var core in Cores) Drift(ref core.Position);
        foreach (var pearl in Pearls)
            if (!pearl.Taken && !pearl.Absorbing && !pearl.Placed && !pearl.Deal) Drift(ref pearl.Position);
        foreach (var cloud in Clouds) Drift(ref cloud.Position);
    }

    /// <summary>A swimmer carried by the current, sliding along rock.</summary>
    void Sweep(ref Vector2 at, float radius)
    {
        var flow = TideAt(at);
        if (flow != Vector2.Zero) Move(ref at, ref flow, radius, report: false);
    }

    /// <summary>Something light carried by the current where there is water to drift into.</summary>
    void Drift(ref Vector2 at)
    {
        var d = TideAt(at) * Dt;
        if (d != Vector2.Zero && Map.IsOpen(at + d)) at += d;
    }

    /// <summary>A creature dissolves into ink at the arena's mouth: the boss's corruption grows by its share, to a limit.</summary>
    void Dissolve(Vector2 at)
    {
        Dissolved++;
        if (Boss is { } boss && _fed < TideTuning.FeedMost)
        {
            float share = MathF.Min(TideTuning.Feed, TideTuning.FeedMost - _fed);
            _fed += share;
            float more = boss.Spec.Hp * share;
            boss.MaxHp += more;
            boss.Hp += more;
        }
        Events.Add(new PlaneEvent(PlaneEventType.TideDissolved, at, Vector2.Zero));
    }
}
