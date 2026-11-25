using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The biomes' hazards (docs/proposals/CROSSOVER.md §8), one a biome so far: the Kelp Forest's tangles (inside a kelp bed
/// she swims at <see cref="Tangle"/> of her pace), the Shipwreck Graveyard's falling debris (a timber falls from the
/// wreck above where she is heading: its shadow shows for <see cref="DebrisWarn"/> s, then it lands, hurting her if she is
/// under it), the Sunken Ruins' dart traps (a trap set in a wall fires a dart down its line a moment after she crosses
/// it, then rests). None of them is the corruption's: they are the old places' own, and her light does nothing to them.
/// None is in a boss's arena.
/// </summary>
public static class HazardTuning
{
    /// <summary>Her pace in a kelp bed (Kelp Forest), within this share of its radius.</summary>
    public const float Tangle = 0.6f, TangleReach = 0.85f;
    /// <summary>Debris: how often one falls (seconds, between), how long its shadow warns, how wide it lands, how hard it hits, how far ahead of her it aims (her velocity × Lead) and how far it may stray.</summary>
    public const float DebrisEveryMin = 3.5f, DebrisEveryMax = 6.5f, DebrisWarn = 1.3f, DebrisRadius = 1.2f, DebrisDamage = 14f, DebrisLead = 1f, DebrisStray = 3f;
    /// <summary>Dart traps: how many a level, how far apart, how far a trap sees down its line and how near it she must be, its windup, its dart's speed, its rest, the dart's hit and size.</summary>
    public const int DartTraps = 6;
    public const float DartApart = 8f, DartReach = 11f, DartLine = 0.9f, DartWindup = 0.35f, DartSpeed = 12f, DartRest = 2.5f, DartDamage = 10f, DartRadius = 0.3f;
    /// <summary>
    /// The Shipwreck's rusted cannons: how many a level; their line and reach; a slower, longer windup and a heavy ball
    /// (its speed, size, its hit on her and on a creature); a long rest. Anything that swims across its line sets it off,
    /// her or a creature, and the ball hits whatever it meets first.
    /// </summary>
    public const int Cannons = 4;
    public const float CannonReach = 14f, CannonLine = 1.2f, CannonWindup = 0.6f, BallSpeed = 7f, BallRadius = 0.45f, BallDamage = 16f, BallMobDamage = 40f, CannonRest = 4f;
    /// <summary>
    /// The Kelp Forest's kelp curtains across its side passages: how many a level; how long her beam must hold on one to
    /// part it, how long it stays parted, and how thick and how far past the passage's sides it stands.
    /// </summary>
    public const int Curtains = 2;
    public const float CurtainBurn = 0.6f, CurtainOpen = 3.5f, CurtainThick = 0.8f, CurtainPast = 0.6f;
}

/// <summary>A falling timber (the Shipwreck): where it lands, and how long till it does (then how long it has lain).</summary>
public sealed class PlaneDebris
{
    public Vector2 Position;
    public float Timer = HazardTuning.DebrisWarn, Lain;
    public bool Landed;
    /// <summary>Which way its timber lies once it has landed.</summary>
    public float Yaw;
}

/// <summary>A dart trap set in a wall (the Ruins), or a rusted cannon (the Shipwreck): where, which way it shoots, its windup and its rest.</summary>
public sealed class PlaneDartTrap
{
    public Vector2 Position, Facing;
    public float Windup, Rest;
    public bool Cannon;
}

/// <summary>A dart in flight, or a cannon's ball.</summary>
public sealed class PlaneDart
{
    public Vector2 Position, Velocity;
    public float Life = 1.5f;
    public bool Ball;
}

/// <summary>A kelp curtain across a passage (the Kelp Forest): its wall, her beam's light on it so far, how long it stays parted.</summary>
public sealed class PlaneKelpCurtain
{
    public WeakRock Rock;
    public float Light, OpenFor;
    public bool Open => OpenFor > 0f;
}

public sealed partial class PlaneWorld
{
    public List<PlaneDebris> Debris { get; } = new();
    public List<PlaneDartTrap> DartTraps { get; } = new();
    public List<PlaneDart> Darts { get; } = new();
    public List<PlaneKelpCurtain> Curtains { get; } = new();

    float _debrisIn = HazardTuning.DebrisEveryMax;
    Rng? _hazardRng;
    Rng HazardRng => _hazardRng ??= new Rng(Map.Seed ^ 0x4A2A6DUL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 48) ^ ((ulong)Map.Id.Branch << 56));

    bool HasHazards(Biome biome) => Map.Templated && Map.Biome == biome && !Map.Rift;

    /// <summary>She is in a kelp bed's tangle (the Kelp Forest): she swims slower.</summary>
    public bool Tangled => HasHazards(Biome.KelpForest)
        && Map.Kelp.Exists(b => Vector2.Distance(b.Center, Player.Position) < b.Radius * HazardTuning.TangleReach);

    /// <summary>
    /// The level's traps: the Ruins' dart traps and the Shipwreck's cannons, set against walls, each facing a stretch of
    /// open water it can see down; the Kelp Forest's kelp curtains across side passages.
    /// </summary>
    void PlaceHazards()
    {
        PlaceCurtains();
        bool ruins = HasHazards(Biome.SunkenRuins), wreck = HasHazards(Biome.Shipwreck);
        if (!ruins && !wreck) return;
        int want = ruins ? HazardTuning.DartTraps : HazardTuning.Cannons;
        var rng = HazardRng;
        for (int tries = 0; tries < 600 && DartTraps.Count < want; tries++)
            TryTrap(new Vector2(rng.Range(0f, Map.Size), rng.Range(0f, Map.Size)), Map.Start.Position, rng, wreck);
    }

    /// <summary>A trap at a spot, if it may stand there: open water away from <paramref name="clear"/> and the others, its back to the rock, a clear run of water before it.</summary>
    bool TryTrap(Vector2 p, Vector2 clear, Rng rng, bool wreck)
    {
        if (!Map.IsOpen(p) || Vector2.Distance(p, clear) < 10f) return false;
        if (Map.HasBoss && Vector2.Distance(p, ArenaCenter) < ArenaRadius + 3f) return false;
        if (DartTraps.Exists(t => Vector2.Distance(t.Position, p) < HazardTuning.DartApart)) return false;
        float a = rng.Int(8) * MathF.Tau / 8f;
        var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
        if (Map.IsOpen(p - d * 1.2f) || LineReach(p, d, HazardTuning.DartReach, out _) < 6f) return false;
        DartTraps.Add(new PlaneDartTrap { Position = p, Facing = d, Rest = rng.Range(0f, 1f), Cannon = wreck });
        return true;
    }

    /// <summary>The Kelp Forest's curtains: across the middle of a side passage or two, far from the start and the arena.</summary>
    void PlaceCurtains()
    {
        if (!HasHazards(Biome.KelpForest)) return;
        var rng = HazardRng;
        var sides = Map.Corridors.Where(c => c.Kind == CorridorKind.Side && c.Points.Count >= 6).ToList();
        while (Curtains.Count < HazardTuning.Curtains && sides.Count > 0)
        {
            var c = sides[rng.Int(sides.Count)];
            sides.Remove(c);
            int k = c.Points.Count / 2;
            var p = c.Points[k];
            if (Vector2.Distance(p, Map.Start.Position) < 12f || Map.HasBoss && Vector2.Distance(p, ArenaCenter) < ArenaRadius + 6f) continue;
            if (Curtains.Exists(x => Vector2.Distance(x.Rock.Center, p) < 10f)) continue;
            var along = SafeNormalize(c.Points[k + 1] - c.Points[k - 1]);
            var rock = new WeakRock(p, c.HalfWidth + HazardTuning.CurtainPast, -1, along, HazardTuning.CurtainThick, SealKind.Path);
            Curtains.Add(new PlaneKelpCurtain { Rock = rock });
            _rocks.Add(rock);
        }
    }

    /// <summary>A kelp curtain her beam holds on long enough parts (it no longer stops her or her light) for a while, then closes behind her.</summary>
    void StepCurtains()
    {
        foreach (var c in Curtains)
        {
            if (c.Open)
            {
                c.OpenFor -= Dt;
                // (It closes only once nothing stands in it.)
                if (c.OpenFor <= 0f && c.Rock.Contains(Player.Position, Radius)) c.OpenFor = 0.1f;
                if (c.OpenFor > 0f) continue;
                c.Light = 0f;
                _rocks.Add(c.Rock);
                Events.Add(new PlaneEvent(PlaneEventType.CurtainClosed, c.Rock.Center, c.Rock.Facing));
                continue;
            }
            bool lit = BeamOn && c.Rock.Contains(BeamEnd, 0.4f);
            c.Light = lit ? c.Light + Dt : MathF.Max(0f, c.Light - Dt * 0.5f);
            if (c.Light < HazardTuning.CurtainBurn) continue;
            c.OpenFor = HazardTuning.CurtainOpen;
            _rocks.Remove(c.Rock);
            Events.Add(new PlaneEvent(PlaneEventType.CurtainParted, c.Rock.Center, c.Rock.Facing));
        }
    }

    void StepHazards()
    {
        StepDebris();
        StepDarts();
        StepCurtains();
    }

    /// <summary>The Shipwreck's debris: now and then a timber falls where she is heading; under it, she is hurt.</summary>
    void StepDebris()
    {
        for (int i = Debris.Count - 1; i >= 0; i--)
        {
            var d = Debris[i];
            if (d.Landed)
            {
                d.Lain += Dt;
                if (d.Lain > 4f) Debris.RemoveAt(i);
                continue;
            }
            d.Timer -= Dt;
            if (d.Timer > 0f) continue;
            d.Landed = true;
            Events.Add(new PlaneEvent(PlaneEventType.DebrisLanded, d.Position, Vector2.Zero, HazardTuning.DebrisRadius));
            if (Vector2.Distance(Player.Position, d.Position) < HazardTuning.DebrisRadius + Radius)
                HurtPlayer(HazardTuning.DebrisDamage, SafeNormalize(Player.Position - d.Position), DamageSource.Debris, d.Position);
        }
        if (!HasHazards(Biome.Shipwreck) || Defeated || BossBattle) return;
        _debrisIn -= Dt;
        if (_debrisIn > 0f) return;
        var rng = HazardRng;
        _debrisIn = rng.Range(HazardTuning.DebrisEveryMin, HazardTuning.DebrisEveryMax);
        for (int tries = 0; tries < 12; tries++)
        {
            float a = rng.Range(0f, MathF.Tau), r = rng.Range(0f, HazardTuning.DebrisStray);
            var at = Player.Position + Player.Velocity * HazardTuning.DebrisLead + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            if (!Clear(at, 0.6f)) continue;
            if (Map.HasBoss && Vector2.Distance(at, ArenaCenter) < ArenaRadius + 2f) continue;
            Debris.Add(new PlaneDebris { Position = at, Yaw = rng.Range(0f, MathF.PI) });
            Events.Add(new PlaneEvent(PlaneEventType.DebrisWarned, at, Vector2.Zero, HazardTuning.DebrisRadius));
            return;
        }
    }

    /// <summary>The Ruins' dart traps: one she crosses the line of winds up and fires; its dart flies till it hits her or the rock.</summary>
    void StepDarts()
    {
        foreach (var t in DartTraps)
        {
            t.Rest -= Dt;
            if (t.Windup > 0f)
            {
                t.Windup -= Dt;
                if (t.Windup > 0f) continue;
                Darts.Add(t.Cannon
                    ? new PlaneDart { Position = t.Position + t.Facing * 0.8f, Velocity = t.Facing * HazardTuning.BallSpeed, Life = 2.6f, Ball = true }
                    : new PlaneDart { Position = t.Position + t.Facing * 0.4f, Velocity = t.Facing * HazardTuning.DartSpeed });
                Events.Add(new PlaneEvent(PlaneEventType.DartFired, t.Position, t.Facing, t.Cannon ? 1f : 0f));
                t.Rest = t.Cannon ? HazardTuning.CannonRest : HazardTuning.DartRest;
                continue;
            }
            if (t.Rest > 0f || Defeated) continue;
            float reach = t.Cannon ? HazardTuning.CannonReach : HazardTuning.DartReach, line = t.Cannon ? HazardTuning.CannonLine : HazardTuning.DartLine;
            bool Crosses(Vector2 who)
            {
                var rel = who - t.Position;
                float along = Vector2.Dot(rel, t.Facing), across = MathF.Abs(rel.X * t.Facing.Y - rel.Y * t.Facing.X);
                return along >= 0.5f && along <= reach && across <= line && LineOfSight(t.Position + t.Facing * 0.4f, who);
            }
            // (A cannon goes off for anything that swims across its muzzle: her, or a creature.)
            if (!Crosses(Player.Position) && !(t.Cannon && Mobs.Exists(m => m.Alive && Crosses(m.Position)))) continue;
            t.Windup = t.Cannon ? HazardTuning.CannonWindup : HazardTuning.DartWindup;
            Events.Add(new PlaneEvent(PlaneEventType.DartArmed, t.Position, t.Facing, t.Cannon ? 1f : 0f));
        }
        for (int i = Darts.Count - 1; i >= 0; i--)
        {
            var d = Darts[i];
            d.Position += d.Velocity * Dt;
            d.Life -= Dt;
            float size = d.Ball ? HazardTuning.BallRadius : HazardTuning.DartRadius;
            if (Vector2.Distance(d.Position, Player.Position) < size + Radius)
            {
                HurtPlayer(d.Ball ? HazardTuning.BallDamage : HazardTuning.DartDamage, SafeNormalize(d.Velocity), DamageSource.Dart, d.Position);
                Darts.RemoveAt(i);
                continue;
            }
            // (A cannon's ball strikes a creature in its way too.)
            if (d.Ball && Mobs.FirstOrDefault(m => m.Alive && Vector2.Distance(m.Position, d.Position) < size + m.Radius) is { } hit)
            {
                DamageMob(hit, HazardTuning.BallMobDamage, SafeNormalize(d.Velocity));
                Events.Add(new PlaneEvent(PlaneEventType.DebrisLanded, d.Position, Vector2.Zero, size));
                Darts.RemoveAt(i);
                continue;
            }
            if (d.Life <= 0f || !Map.IsOpen(d.Position) || _rocks.Exists(r => r.Contains(d.Position)))
            {
                if (d.Ball) Events.Add(new PlaneEvent(PlaneEventType.DebrisLanded, d.Position, Vector2.Zero, size));
                Darts.RemoveAt(i);
            }
        }
    }
}
