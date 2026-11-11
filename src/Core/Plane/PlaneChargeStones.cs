using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The reef's charge stones (docs/LIGHT-AND-GRAPH.md §2.5): small clusters of coloured crystal shards set in the walls,
/// each its own colour. Her beam charges one (the more of its light reaches it, the faster: PlaneWorld.BeamLight); charged,
/// it glows on its own, full while it holds more than FadeSeconds of glow, then dimming as the last of it runs out, and
/// out. Its light is round it, as her glow is, fainter even at full charge, and harms nothing; it shows the murklings in
/// it as her glow does (ChargeStoneSees). It is a light in the registry like any, so a murkling may go to it instead of her
/// and drink it down. A lure, and a lamp for the reef. Some levels hold a trio of them round a nook, alike in colour
/// with three dark shards on the sand between them: all three glowing at once, a secret cache of shells bursts up from
/// the sand there, once (ChargeTrio).
/// </summary>
public static class ChargeStoneTuning
{
    /// <summary>The chance a level holds a trio; how far its stones are from its middle at most; the shells its cache holds.</summary>
    public const float TrioChance = 0.7f, TrioReach = 6.5f;
    public const int TrioShells = 12;
    /// <summary>Seconds of glow a full charge holds; seconds of glow a second of her beam gives at one shot's light.</summary>
    public const float MaxGlow = 14f, ChargeRate = 5f;
    /// <summary>It is full bright while it holds more than FadeSeconds of glow, dimming to nothing over them.</summary>
    public const float FadeSeconds = 5f;
    /// <summary>Its light at full: fainter than her aura (1), as far as a murkling may see it; drawn round it to ShownReach.</summary>
    public const float Bright = 0.5f, Reach = 7f, ShownReach = 4.5f;
    /// <summary>How many to a reef, how far apart, how far from her start.</summary>
    public const int Count = 16;
    public const float Apart = 9f, FromStart = 8f;
    /// <summary>The reach of her beam's light that charges it (round the point in the water before its face).</summary>
    public const float Radius = 0.6f;

    /// <summary>The charge stones' colours (linear RGB): cyan, magenta, amber, lime, violet, ice blue.</summary>
    public static readonly Vector3[] Colours =
    {
        new(0.25f, 0.95f, 1f), new(1f, 0.3f, 0.85f), new(1f, 0.72f, 0.2f), new(0.55f, 1f, 0.3f), new(0.65f, 0.4f, 1f), new(0.7f, 0.9f, 1f),
    };
}

/// <summary>A charge stone in a wall: where it is set (Face), the water before it its light shines from (Position), its colour, its charge.</summary>
public sealed class ChargeStone : ILightOwner
{
    public Vector2 Position, Face;
    /// <summary>The way out of the wall it faces.</summary>
    public Vector2 Out;
    public int Colour;
    /// <summary>Seconds of glow it holds (0: dark).</summary>
    public float Charge;
    public LightEmitter Light = null!;
    public float Seed;
    /// <summary>One of the level's trio (its place in it, 0..2), or not (-1).</summary>
    public int Trio = -1;

    /// <summary>How bright it glows (0 dark, 1 full).</summary>
    public float Glow => Charge <= 0f ? 0f : MathF.Min(1f, Charge / ChargeStoneTuning.FadeSeconds);

    float ILightOwner.Energy => Charge;

    /// <summary>A murkling drinking it drinks its glow away.</summary>
    float ILightOwner.Absorb(float amount)
    {
        float taken = MathF.Min(MathF.Max(amount, 0f), Charge);
        Charge -= taken;
        return taken;
    }
}

/// <summary>A level's trio of charge stones: the middle of the nook between them (its cache bursts up there), and whether it has.</summary>
public sealed class ChargeTrio
{
    public Vector2 Middle;
    public bool Opened;
}

public sealed partial class PlaneWorld
{
    public List<ChargeStone> ChargeStones { get; } = new();

    /// <summary>The level's trio of charge stones, if it has one.</summary>
    public ChargeTrio? Trio { get; private set; }

    /// <summary>
    /// A trio: an open nook away from her start with rock round it three ways, far apart in angle; a charge stone in each
    /// of the three, alike in colour.
    /// </summary>
    void PlaceChargeTrio()
    {
        var rng = new Rng(Map.Seed ^ 0x7121057EUL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Id.Lane << 40) ^ ((ulong)Map.Attempt << 50));
        if (Map.Labyrinth || rng.NextFloat() >= ChargeStoneTuning.TrioChance) return;
        var walls = new List<(float Angle, Vector2 Dir, float At)>();
        for (int tries = 0; tries < 800; tries++)
        {
            var p = new Vector2(rng.Range(8f, Map.Size - 8f), rng.Range(8f, Map.Size - 8f));
            if (!Clear(p, 2f) || Map.InShaft(p, 5f) || Vector2.Distance(p, Map.Start.Position) < 12f) continue;
            walls.Clear();
            for (int k = 0; k < 24; k++)
            {
                float a = k * MathF.Tau / 24f;
                var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                for (float t = 2f; t <= ChargeStoneTuning.TrioReach; t += 0.25f)
                {
                    var q = p + d * t;
                    if (Map.HeightAt(q) <= LevelMap.SwimBand + 0.4f && Map.IsOpen(q)) continue;
                    // The water just before the wall must be open, and in sight of the middle.
                    var before = p + d * (t - 0.75f);
                    if (Map.IsOpen(before) && LineOfSight(p, before) && Map.HeightAt(p + d * (t + 0.35f)) > LevelMap.SwimBand + 0.4f) walls.Add((a, d, t));
                    break;
                }
            }
            if (walls.Count < 3) continue;
            // Three far apart in angle: the first, then each next the farthest from those chosen.
            var chosen = new List<(float Angle, Vector2 Dir, float At)> { walls[rng.Int(walls.Count)] };
            while (chosen.Count < 3)
            {
                (float Angle, Vector2 Dir, float At) best = default;
                float bestGap = -1f;
                foreach (var w in walls)
                {
                    float gap = float.MaxValue;
                    foreach (var c in chosen) gap = MathF.Min(gap, MathF.Abs(MathF.IEEERemainder(w.Angle - c.Angle, MathF.Tau)));
                    if (gap > bestGap)
                    {
                        bestGap = gap;
                        best = w;
                    }
                }
                if (bestGap < MathF.PI * 0.45f) break;
                chosen.Add(best);
            }
            if (chosen.Count < 3) continue;
            int colour = rng.Int(ChargeStoneTuning.Colours.Length);
            for (int i = 0; i < 3; i++)
            {
                var (_, d, at) = chosen[i];
                var stone = new ChargeStone { Position = p + d * (at - 0.75f), Face = p + d * at, Out = -d, Colour = colour, Seed = rng.NextFloat(), Trio = i };
                stone.Light = Lights.Add(new LightEmitter
                {
                    Owner = stone, Position = stone.Position, Range = ChargeStoneTuning.Reach, Intensity = 0f, Colour = ChargeStoneTuning.Colours[colour], Drinkable = true,
                });
                ChargeStones.Add(stone);
            }
            Trio = new ChargeTrio { Middle = p };
            return;
        }
    }

    /// <summary>The charge stones set in the walls: at the swim plane's edge of the rock, apart, from the level's own dice.</summary>
    void PlaceChargeStones()
    {
        PlaceChargeTrio();
        var rng = new Rng(Map.Seed ^ 0xC2757A15UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Id.Lane << 40) ^ ((ulong)Map.Attempt << 50));
        for (int tries = 0; tries < 3000 && ChargeStones.Count < ChargeStoneTuning.Count; tries++)
        {
            var p = new Vector2(rng.Range(6f, Map.Size - 6f), rng.Range(6f, Map.Size - 6f));
            if (!Map.IsOpen(p) || Map.InShaft(p, 4f) || Vector2.Distance(p, Map.Start.Position) < ChargeStoneTuning.FromStart) continue;
            // A wall just beside it, at her plane: the charge stone is set in its face.
            float a = rng.Range(0f, MathF.Tau);
            Vector2? into = null;
            for (int k = 0; k < 12 && into is null; k++)
            {
                var d = new Vector2(MathF.Cos(a + k * MathF.Tau / 12f), MathF.Sin(a + k * MathF.Tau / 12f));
                if (Map.HeightAt(p + d * 1.1f) > LevelMap.SwimBand + 0.4f) into = d;
            }
            if (into is not { } dir) continue;
            if (ChargeStones.Exists(c => Vector2.Distance(c.Position, p) < ChargeStoneTuning.Apart)) continue;
            var crystal = new ChargeStone { Position = p, Face = p + dir * 0.75f, Out = -dir, Colour = rng.Int(ChargeStoneTuning.Colours.Length), Seed = rng.NextFloat() };
            crystal.Light = Lights.Add(new LightEmitter
            {
                Owner = crystal, Position = p, Range = ChargeStoneTuning.Reach, Intensity = 0f, Colour = ChargeStoneTuning.Colours[crystal.Colour], Drinkable = true,
            });
            ChargeStones.Add(crystal);
        }
    }

    /// <summary>
    /// How much a lit charge stone shows of a creature at this spot (0 none, 1 fully), as her glow shows what is near her: full
    /// within most of its drawn light (ShownReach), thinning at its edge, as bright as the charge stone glows; not round rock.
    /// </summary>
    public float ChargeStoneSees(Vector2 at, float radius)
    {
        float seen = 0f;
        foreach (var c in ChargeStones)
        {
            if (c.Glow <= seen) continue;
            float d = Vector2.Distance(at, c.Position) - radius;
            float inner = ChargeStoneTuning.ShownReach * 0.75f, outer = ChargeStoneTuning.ShownReach;
            if (d >= outer) continue;
            float t = Math.Clamp((d - inner) / (outer - inner), 0f, 1f);
            float s = c.Glow * (1f - t * t * (3f - 2f * t));
            if (s > seen && LineOfSight(c.Position, at)) seen = s;
        }
        return seen;
    }

    /// <summary>
    /// Whether light falls on a hidden creature's core (its middle), so the whole of it shows: within her glow's reach of
    /// her, under a beam of hers that gets to it (no body in the way), or in a lit charge stone's light.
    /// </summary>
    public bool CoreLit(object kin, Vector2 at)
    {
        if (Aura.Shining && Vector2.Distance(at, Aura.Position) < Aura.Range && LineOfSight(Aura.Position, at)) return true;
        foreach (var (beam, _) in HerBeams)
            if (BeamLight(beam, at, 0f, 0.2f, kin) > 0f) return true;
        return ChargeStoneSees(at, 0f) > 0.5f;
    }

    /// <summary>Her beams charge the charge stones they reach (the more light, the faster); a charged one glows on, then fades out.</summary>
    void StepChargeStones()
    {
        foreach (var c in ChargeStones)
        {
            float light = 0f;
            if (!Defeated)
                foreach (var (beam, worth) in HerBeams) light += worth * BeamLight(beam, c.Position, 0f, ChargeStoneTuning.Radius, c);
            bool was = c.Charge > 0f;
            if (light > 0f) c.Charge = MathF.Min(ChargeStoneTuning.MaxGlow, c.Charge + light * ChargeStoneTuning.ChargeRate * Dt);
            else c.Charge = MathF.Max(0f, c.Charge - Dt);
            if (!was && c.Charge > 0f) Events.Add(new PlaneEvent(PlaneEventType.ChargeStoneLit, c.Face, c.Out));
            c.Light.Intensity = ChargeStoneTuning.Bright * c.Glow;
        }
        // The trio all glowing at once: its cache bursts up from the sand between them, once.
        if (Trio is { Opened: false } trio && ChargeStones.FindAll(c => c.Trio >= 0) is { Count: 3 } three && three.TrueForAll(c => c.Charge > 0f))
        {
            trio.Opened = true;
            for (int i = 0; i < ChargeStoneTuning.TrioShells; i++) Shells.Add(new PlaneShell { Position = trio.Middle, Velocity = Fling(2f, 5f) });
            Events.Add(new PlaneEvent(PlaneEventType.TrioOpened, trio.Middle, Vector2.Zero, ChargeStoneTuning.TrioShells));
        }
    }
}
