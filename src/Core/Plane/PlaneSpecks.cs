using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A light speck (docs/GAME.md): a mote of light worth some energy (Value), lying on the level or dropped by a creature
/// her light frees. It glows faintly, a light like any other (an emitter in the registry): it shows the murklings near
/// it, and they come to drink it. She gathers it by touching it (it drifts to her when she is near), and its energy is
/// hers to grow with (PlaneGrowth.cs). A murkling that drinks it keeps what it drank, grows stronger by it, and lets it
/// all go again when her light frees it.
/// </summary>
public sealed class LightSpeck : ILightOwner
{
    public Vector2 Position, Velocity;
    public float Value, Seed;
    public bool Gone;
    public LightEmitter Light = null!;

    public float Energy => Gone ? 0f : Value;

    /// <summary>Drunk (by a murkling feeding on it): it loses what is drunk, and is gone when none is left.</summary>
    public float Absorb(float amount)
    {
        if (Gone) return 0f;
        float taken = MathF.Min(MathF.Max(amount, 0f), Value);
        Value -= taken;
        if (Value <= 1e-4f) Gone = true;
        return taken;
    }
}

/// <summary>Light specks (docs/GAME.md). (tune)</summary>
public static class SpeckTuning
{
    /// <summary>How many lie on a level (Scatter + PerDepth a depth below the first), and what each is worth (Least to Most).</summary>
    public const int Scatter = 10, PerDepth = 2;
    public const float Least = 0.5f, Most = 1.5f;
    /// <summary>How far from her start they lie, and the open water round each.</summary>
    public const float FromStart = 6f, Clearance = 0.5f;
    /// <summary>A freed creature's chance of a speck, and what it is worth (Least to Most × DropWorth); a freed boss's specks.</summary>
    public const float DropChance = 0.7f, DropWorth = 0.8f;
    public const int BossSpecks = 6;
    /// <summary>Its light: faint, a small reach (it shows only the murklings near it), and how bright to the eye far off (the view).</summary>
    public const float Intensity = 0.55f, Reach = 4f;
    /// <summary>She gathers it within Gather m of her body; within Draw m it drifts to her at DrawSpeed m/s.</summary>
    public const float Gather = 0.45f, Draw = 2.6f, DrawSpeed = 7f;
    /// <summary>A dropped speck's fling (m/s) and the water's drag on it (/s).</summary>
    public const float Fling = 3.5f, Drag = 3f;
    /// <summary>A murkling for each unit of energy it drinks: its corruption (× 1 + Hardier), its size (× 1 + Bigger, to MostBigger) and its drain (× 1 + Hungrier).</summary>
    public const float Hardier = 0.3f, Bigger = 0.07f, MostBigger = 1.6f, Hungrier = 0.15f;
    /// <summary>A murkling drinks a speck this many times as fast as it drains her.</summary>
    public const float DrinkRate = 1f;
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's light specks, lying or dropped (gathered and drunk ones leave).</summary>
    public List<LightSpeck> Specks { get; } = new();

    Rng? _speckRng;
    Rng SpeckRng => _speckRng ??= new Rng(Map.Seed ^ 0x5BECC5UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));

    /// <summary>The level's specks laid at its start (PlaneOptions.Specks): scattered through its open water, away from her start.</summary>
    void PlaceSpecks()
    {
        var rng = SpeckRng;
        int want = SpeckTuning.Scatter + SpeckTuning.PerDepth * Math.Max(0, Map.Depth - LevelPlan.Tutorial - 1);
        // (Tidy: fewer specks, each worth the more, out of the quiet water.)
        float worth = 1f;
        if (Options.Tidy)
        {
            int fewer = Math.Max(1, (int)MathF.Round(want * TidyTuning.SpeckShare));
            worth = want / (float)fewer;
            want = fewer;
        }
        var spots = new List<Vector2>();
        for (float y = LevelMap.RimWidth + 2f; y < Map.Size - LevelMap.RimWidth - 2f; y += 3f)
        for (float x = LevelMap.RimWidth + 2f; x < Map.Size - LevelMap.RimWidth - 2f; x += 3f)
        {
            var p = new Vector2(x, y);
            if (Clear(p, SpeckTuning.Clearance) && !Map.InShaft(p) && Vector2.Distance(p, Map.Start.Position) >= SpeckTuning.FromStart && !InQuietWater(p)) spots.Add(p);
        }
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        for (int i = 0; i < Math.Min(want, spots.Count); i++)
        {
            var jitter = new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 1f));
            var at = Clear(spots[i] + jitter, SpeckTuning.Clearance) && !InQuietWater(spots[i] + jitter) ? spots[i] + jitter : spots[i];
            AddSpeck(at, rng.Range(SpeckTuning.Least, SpeckTuning.Most) * worth, Vector2.Zero);
        }
    }

    /// <summary>A speck on the level (its light registered: the murklings see it).</summary>
    public LightSpeck AddSpeck(Vector2 at, float value, Vector2 velocity)
    {
        var s = new LightSpeck { Position = at, Velocity = velocity, Value = value, Seed = SpeckRng.Range(0f, 100f) };
        s.Light = Lights.Add(new LightEmitter
        {
            Owner = s, Position = at, Colour = new Vector3(1f, 0.92f, 0.7f),
            Range = SpeckTuning.Reach * (Options.Tidy ? TidyTuning.SpeckReach : 1f),
            Intensity = SpeckTuning.Intensity * (Options.Tidy ? TidyTuning.SpeckLight : 1f),
        });
        Specks.Add(s);
        return s;
    }

    /// <summary>Specks thrown out from where something was freed (worth <paramref name="total"/> in all, in specks of about one).</summary>
    void DropSpecks(Vector2 at, float total)
    {
        if (!Options.Specks || total <= 0.01f) return;
        var rng = SpeckRng;
        int n = Math.Max(1, (int)MathF.Round(total));
        for (int i = 0; i < n; i++)
        {
            float a = rng.Range(0f, MathF.Tau);
            var way = new Vector2(MathF.Cos(a), MathF.Sin(a));
            AddSpeck(Clear(at, 0.3f) ? at : at + way * 0.5f, total / n, way * SpeckTuning.Fling * rng.Range(0.5f, 1f));
        }
        Events.Add(new PlaneEvent(PlaneEventType.SpecksDropped, at, Vector2.Zero, total));
    }

    /// <summary>
    /// The specks' step: drifting (a dropped one slows), drawn to her when she is near and gathered when she touches one
    /// (her energy grows by its worth); the drunk ones gone; and a speck for what her light freed this step (a murkling's
    /// with what it had drunk; a boss's, BossSpecks).
    /// </summary>
    void StepSpecks()
    {
        if (!Options.Specks) return;
        // (Gathered first, dropped after: dropping tells of it among the very events walked.)
        List<(Vector2 At, float Worth)>? drops = null;
        foreach (var e in Events)
        {
            if (e.Type == PlaneEventType.MobDefeated && SpeckRng.NextFloat() < SpeckTuning.DropChance)
                (drops ??= new()).Add((e.Position, SpeckRng.Range(SpeckTuning.Least, SpeckTuning.Most) * SpeckTuning.DropWorth));
            else if (e.Type == PlaneEventType.BossFreed) (drops ??= new()).Add((e.Position, SpeckTuning.BossSpecks));
        }
        if (drops is not null)
            foreach (var (at, worth) in drops) DropSpecks(at, worth);
        var her = Player.Position;
        for (int i = Specks.Count - 1; i >= 0; i--)
        {
            var s = Specks[i];
            if (!s.Gone && !Defeated)
            {
                float d = Vector2.Distance(s.Position, her);
                if (d <= Radius + SpeckTuning.Gather)
                {
                    Run.Energy += s.Value;
                    s.Gone = true;
                    Events.Add(new PlaneEvent(PlaneEventType.SpeckGathered, s.Position, Vector2.Zero, s.Value));
                }
                else if (d <= SpeckTuning.Draw) s.Velocity = Vector2.Normalize(her - s.Position) * SpeckTuning.DrawSpeed;
                else s.Velocity /= 1f + SpeckTuning.Drag * Dt;
                if (!s.Gone && s.Velocity.LengthSquared() > 1e-6f)
                {
                    var next = s.Position + s.Velocity * Dt;
                    if (Map.IsOpen(next)) s.Position = next;
                    else s.Velocity = Vector2.Zero;
                }
                s.Light.Position = s.Position;
                s.Light.Intensity = SpeckTuning.Intensity * MathF.Min(1f, 0.5f + 0.5f * s.Value);
            }
            if (!s.Gone) continue;
            Lights.Remove(s.Light);
            Specks.RemoveAt(i);
        }
    }

    /// <summary>How much a speck's own faint light shows something at <paramref name="at"/> (0..1): near one, in its sight.</summary>
    public float SpeckSees(Vector2 at, float radius)
    {
        float best = 0f;
        foreach (var s in Specks)
        {
            if (s.Gone) continue;
            float d = Vector2.Distance(s.Position, at) - radius;
            if (d >= SpeckTuning.Reach) continue;
            float k = 1f - MathF.Max(d, 0f) / SpeckTuning.Reach;
            if (k > best && LineOfSight(s.Position, at)) best = k;
        }
        return best;
    }

    /// <summary>A murkling drank a speck's light (<paramref name="got"/> of its energy): it keeps it, and grows stronger by it.</summary>
    void MurklingAte(Murkling m, float got)
    {
        if (got <= 0f) return;
        m.Specks += got;
        float hardier = 1f + SpeckTuning.Hardier * got;
        m.Corruption.Max *= hardier;
        m.Corruption.Value *= hardier;
        float bigger = MathF.Min(1f + SpeckTuning.Bigger * got, SpeckTuning.MostBigger * BlightTuning.MurkRadius / MathF.Max(m.Radius, 0.01f));
        m.Radius *= bigger;
        m.Body *= bigger;
        m.ClingDrain *= 1f + SpeckTuning.Hungrier * got;
        Events.Add(new PlaneEvent(PlaneEventType.SpeckEaten, m.Position, Vector2.Zero, got));
    }
}
