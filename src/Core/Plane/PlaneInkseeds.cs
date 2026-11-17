using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Inkseeds (docs/CORRUPTION.md): traps of ink lying in the reef's water, unseen in the dark. The first light of hers
/// on one wakes it; awake, it swells to twice its size over Swell seconds unless her beam is on it, and her beam held
/// on it Defuse seconds unbroken frees it. Swollen full, it bursts: a stain of darkness Reach metres round that no light
/// shows through, shrinking away over Stain seconds; everything within its reach thrown back (Knock m/s at its middle,
/// easing to nothing at its reach), and she alone hurt (Damage × (1 + Menace)). First guesses, tuned in play.
/// </summary>
public static class InkseedTuning
{
    /// <summary>Its radius asleep (a lesser murkling's); swollen full, twice it.</summary>
    public const float Radius = MurkSizeTuning.Least;
    /// <summary>Awake, it swells full over this long; her beam held on it this long unbroken frees it.</summary>
    public const float Swell = 3f, Defuse = 2f;
    /// <summary>Its burst: its reach, the knock at its middle (m/s), her tumble at most (s), her hurt at Menace 0.</summary>
    public const float Reach = 4f, Knock = 9f, Tumble = 0.35f, Damage = 12f;
    /// <summary>The stain it leaves: spread out to its reach over Spread seconds, gone after Stain.</summary>
    public const float Spread = 0.25f, Stain = 10f;
    /// <summary>A cube's seeds: Least + PerMenace × the layer's Menace, this far apart, from where she comes in, and from its ways (gates, wells, sockets).</summary>
    public const int Least = 1;
    public const float PerMenace = 2f, Spacing = 6f, FromEntry = 12f, FromWays = 6f;
    /// <summary>Farther than this from her a seed is still: the water does not carry it.</summary>
    public const float Still = 48f;
}

/// <summary>A trap of ink: where it is, whether awake and how far swollen, how long her beam has been on it.</summary>
public sealed class Inkseed
{
    public Vector2 Position;
    /// <summary>Woken by her light (for good).</summary>
    public bool Armed;
    /// <summary>How far swollen (0 asleep → 1: it bursts).</summary>
    public float Swell;
    /// <summary>How long her beam has been on it without a break.</summary>
    public float Held;
    /// <summary>Some light of hers falls on it this step (it shows); her beam is on it.</summary>
    public bool Lit, Beamed;
    /// <summary>The cube it lies in (kept with the cube's record when she leaves the layer).</summary>
    public OctoShoots.Core.Gen.Cubes.CubeId? Cube;
    public float Radius => InkseedTuning.Radius * (1f + Swell);
}

/// <summary>A burst seed's stain of darkness: where, and how long since.</summary>
public sealed class InkStain
{
    public Vector2 Position;
    public float Age;

    /// <summary>How far it reaches now: spreading out quickly, then shrinking away.</summary>
    public float Radius
    {
        get
        {
            float spread = Math.Clamp(Age / InkseedTuning.Spread, 0f, 1f);
            float left = Math.Clamp((InkseedTuning.Stain - Age) / (InkseedTuning.Stain - InkseedTuning.Spread), 0f, 1f);
            return InkseedTuning.Reach * (1f - (1f - spread) * (1f - spread)) * MathF.Sqrt(left);
        }
    }
}

public sealed partial class PlaneWorld
{
    public List<Inkseed> Inkseeds { get; } = new();
    public List<InkStain> Stains { get; } = new();

    /// <summary>Within a stain of darkness: no light of any kind shows or burns here.</summary>
    public bool Stained(Vector2 at)
    {
        foreach (var s in Stains)
            if (Vector2.DistanceSquared(s.Position, at) < s.Radius * s.Radius) return true;
        return false;
    }

    /// <summary>A seed laid in a cube (as the cube is claimed, or put back as she left it).</summary>
    public void PlaceInkseed(OctoShoots.Core.Gen.Cubes.CubeId cube, Vector2 at) =>
        Inkseeds.Add(new Inkseed { Position = at, Cube = cube });

    /// <summary>Where a cube's seeds still lie (none: all burst or freed).</summary>
    public List<Vector2> InkseedsLeft(OctoShoots.Core.Gen.Cubes.CubeId cube) =>
        Inkseeds.Where(s => s.Cube == cube).Select(s => s.Position).ToList();

    /// <summary>Whether any light of hers falls on this point now (her glow, her beam, her shots' light).</summary>
    bool HerLightOn(Vector2 at, float radius, object self, out bool beamed)
    {
        beamed = false;
        if (Defeated || Stained(at)) return false;
        foreach (var (beam, _) in HerBeams)
            if (BeamLight(beam, at, 0f, radius, self) > 0f)
            {
                beamed = true;
                return true;
            }
        if (LightQuery.Brightness(Aura, at, LineOfSight) > 0f) return true;
        foreach (var s in Shots)
            if (s.FromPlayer && !s.BossPearl && s.Life > 0f && Vector2.Distance(s.Position, at) - radius < ShotLightReach(s) && LineOfSight(s.Position, at)) return true;
        return false;
    }

    /// <summary>The seeds: woken by her light, swelling, freed by her beam held on them, or bursting; the stains fading.</summary>
    void StepInkseeds()
    {
        for (int i = Stains.Count - 1; i >= 0; i--)
        {
            Stains[i].Age += Dt;
            if (Stains[i].Age >= InkseedTuning.Stain) Stains.RemoveAt(i);
        }
        if (Inkseeds.Count == 0) return;
        var her = Player.Position;
        for (int i = Inkseeds.Count - 1; i >= 0; i--)
        {
            var s = Inkseeds[i];
            // (Far off, asleep: no light of hers reaches it.)
            if (!s.Armed && Vector2.DistanceSquared(s.Position, her) > 30f * 30f)
            {
                s.Lit = s.Beamed = false;
                continue;
            }
            s.Lit = HerLightOn(s.Position, s.Radius, s, out s.Beamed);
            if (!s.Armed)
            {
                if (!s.Lit) continue;
                s.Armed = true;
                Events.Add(new PlaneEvent(PlaneEventType.InkseedWoke, s.Position, Vector2.Zero, s.Radius));
            }
            if (s.Beamed)
            {
                // Her beam on it: it holds its size, and held long enough, it is freed.
                s.Held += Dt;
                if (s.Held < InkseedTuning.Defuse) continue;
                Inkseeds.RemoveAt(i);
                Events.Add(new PlaneEvent(PlaneEventType.InkseedFreed, s.Position, Vector2.Zero, s.Radius));
                continue;
            }
            s.Held = 0f;
            s.Swell += Dt / InkseedTuning.Swell;
            if (s.Swell < 1f) continue;
            Inkseeds.RemoveAt(i);
            BurstInkseed(s);
        }
    }

    /// <summary>Tests and verification: this seed bursts now.</summary>
    public void DebugBurstInkseed(Inkseed s)
    {
        Inkseeds.Remove(s);
        BurstInkseed(s);
    }

    /// <summary>Swollen full, it bursts: its stain, everything in its reach thrown back, and she hurt if she is in it.</summary>
    void BurstInkseed(Inkseed s)
    {
        var at = s.Position;
        const float reach = InkseedTuning.Reach;
        Stains.Add(new InkStain { Position = at });
        Events.Add(new PlaneEvent(PlaneEventType.InkseedBurst, at, Vector2.Zero, reach));
        var p = Player;
        float d = Vector2.Distance(p.Position, at);
        if (d < reach + Radius && !Defeated && !InHaven)
        {
            var away = d > 1e-3f ? (p.Position - at) / d : -p.Aim;
            float k = 1f - Math.Clamp(d / reach, 0f, 1f);
            p.Velocity = away * InkseedTuning.Knock * MathF.Max(k, 0.25f);
            p.TumbleTimer = InkseedTuning.Tumble * MathF.Max(k, 0.3f);
            p.TumbleDir = away;
            p.DashTimer = 0f;
            p.Sprinting = false;
            if (p.ShieldTimer > 0f) Events.Add(new PlaneEvent(PlaneEventType.ShieldBlocked, p.Position, away));
            else
            {
                // (Her dash does not save her: it is no blow to slip past, but the water itself.)
                float hurt = InkseedTuning.Damage * (1f + Menace);
                p.Hp -= hurt;
                Stats.DamageTaken += hurt;
                p.HurtTimer = PlaneCombatTuning.PlayerHurtGrace;
                LastHitSource = DamageSource.Inkseed;
                Events.Add(new PlaneEvent(PlaneEventType.PlayerHit, p.Position, away, hurt, DamageSource.Inkseed));
                SpendLastLight();
                if (p.Hp <= 0f)
                {
                    p.Hp = 0f;
                    p.Velocity = Vector2.Zero;
                    Defeated = true;
                    Events.Add(new PlaneEvent(PlaneEventType.PlayerDefeated, p.Position, Vector2.Zero, 0f, DamageSource.Inkseed));
                }
                Run.Hp = p.Hp;
            }
        }
        // The creatures are thrown back too (none is hurt).
        foreach (var m in Murklings)
            if (m.Alive && !m.Budding && !m.Clinging) Throw(at, m.Position, ref m.Knock);
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f) Throw(at, b.Position, ref b.Knock);
        foreach (var w in Inkwardens)
            if (w.Alive) Throw(at, w.Position, ref w.Knock);
        foreach (var t in Thumpsquirts)
            if (t.Alive && !t.Airborne) Throw(at, t.Position, ref t.Knock);
    }

    /// <summary>A burst at <paramref name="at"/> throws a creature there straight away from it, the harder the nearer.</summary>
    static void Throw(Vector2 at, Vector2 pos, ref Vector2 knock)
    {
        float d = Vector2.Distance(pos, at);
        if (d >= InkseedTuning.Reach) return;
        var dir = d > 1e-3f ? (pos - at) / d : Vector2.UnitX;
        knock += dir * InkseedTuning.Knock * (1f - d / InkseedTuning.Reach);
    }
}
