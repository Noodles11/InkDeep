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

/// <summary>A dart trap set in a wall (the Ruins): where, which way it shoots, its windup and its rest.</summary>
public sealed class PlaneDartTrap
{
    public Vector2 Position, Facing;
    public float Windup, Rest;
}

/// <summary>A dart in flight.</summary>
public sealed class PlaneDart
{
    public Vector2 Position, Velocity;
    public float Life = 1.5f;
}

public sealed partial class PlaneWorld
{
    public List<PlaneDebris> Debris { get; } = new();
    public List<PlaneDartTrap> DartTraps { get; } = new();
    public List<PlaneDart> Darts { get; } = new();

    float _debrisIn = HazardTuning.DebrisEveryMax;
    Rng? _hazardRng;
    Rng HazardRng => _hazardRng ??= new Rng(Map.Seed ^ 0x4A2A6DUL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 48) ^ ((ulong)Map.Id.Branch << 56));

    bool HasHazards(Biome biome) => Map.Templated && Map.Biome == biome && !Map.Rift;

    /// <summary>She is in a kelp bed's tangle (the Kelp Forest): she swims slower.</summary>
    public bool Tangled => HasHazards(Biome.KelpForest)
        && Map.Kelp.Exists(b => Vector2.Distance(b.Center, Player.Position) < b.Radius * HazardTuning.TangleReach);

    /// <summary>The level's dart traps (the Ruins): set in walls, each facing a stretch of open water it can see down.</summary>
    void PlaceHazards()
    {
        if (!HasHazards(Biome.SunkenRuins)) return;
        var rng = HazardRng;
        for (int tries = 0; tries < 600 && DartTraps.Count < HazardTuning.DartTraps; tries++)
        {
            var p = new Vector2(rng.Range(0f, Map.Size), rng.Range(0f, Map.Size));
            if (!Map.IsOpen(p) || Vector2.Distance(p, Map.Start.Position) < 10f) continue;
            if (Map.HasBoss && Vector2.Distance(p, ArenaCenter) < ArenaRadius + 3f) continue;
            if (DartTraps.Exists(t => Vector2.Distance(t.Position, p) < HazardTuning.DartApart)) continue;
            float a = rng.Int(8) * MathF.Tau / 8f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            // Its back to the rock, a clear run of water before it.
            if (Map.IsOpen(p - d * 1.2f) || LineReach(p, d, HazardTuning.DartReach, out _) < 6f) continue;
            DartTraps.Add(new PlaneDartTrap { Position = p, Facing = d, Rest = rng.Range(0f, 1f) });
        }
    }

    void StepHazards()
    {
        StepDebris();
        StepDarts();
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
                Darts.Add(new PlaneDart { Position = t.Position + t.Facing * 0.4f, Velocity = t.Facing * HazardTuning.DartSpeed });
                Events.Add(new PlaneEvent(PlaneEventType.DartFired, t.Position, t.Facing));
                t.Rest = HazardTuning.DartRest;
                continue;
            }
            if (t.Rest > 0f || Defeated) continue;
            var rel = Player.Position - t.Position;
            float along = Vector2.Dot(rel, t.Facing), across = MathF.Abs(rel.X * t.Facing.Y - rel.Y * t.Facing.X);
            if (along < 0.5f || along > HazardTuning.DartReach || across > HazardTuning.DartLine) continue;
            if (!LineOfSight(t.Position + t.Facing * 0.4f, Player.Position)) continue;
            t.Windup = HazardTuning.DartWindup;
            Events.Add(new PlaneEvent(PlaneEventType.DartArmed, t.Position, t.Facing));
        }
        for (int i = Darts.Count - 1; i >= 0; i--)
        {
            var d = Darts[i];
            d.Position += d.Velocity * Dt;
            d.Life -= Dt;
            if (Vector2.Distance(d.Position, Player.Position) < HazardTuning.DartRadius + Radius)
            {
                HurtPlayer(HazardTuning.DartDamage, SafeNormalize(d.Velocity), DamageSource.Dart, d.Position);
                Darts.RemoveAt(i);
                continue;
            }
            if (d.Life <= 0f || !Map.IsOpen(d.Position)) Darts.RemoveAt(i);
        }
    }
}
