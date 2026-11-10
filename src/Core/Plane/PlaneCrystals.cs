using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The reef's light crystals (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.5): small shards of coloured crystal set in the walls,
/// each its own colour. Her beam charges one (the more of its light reaches it, the faster: PlaneWorld.BeamLight); charged,
/// it glows on its own, full while it holds more than FadeSeconds of glow, then dimming as the last of it runs out, and
/// out. Its light is round it, as her glow is, fainter even at full charge, and harms nothing; it shows the murklings in
/// it as her glow does (CrystalSees). It is a light in the registry like any, so a murkling may go to it instead of her
/// and drink it down. A lure, and a lamp for the reef.
/// </summary>
public static class CrystalTuning
{
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

    /// <summary>The crystals' colours (linear RGB): cyan, magenta, amber, lime, violet, ice blue.</summary>
    public static readonly Vector3[] Colours =
    {
        new(0.25f, 0.95f, 1f), new(1f, 0.3f, 0.85f), new(1f, 0.72f, 0.2f), new(0.55f, 1f, 0.3f), new(0.65f, 0.4f, 1f), new(0.7f, 0.9f, 1f),
    };
}

/// <summary>A light crystal in a wall: where it is set (Face), the water before it its light shines from (Position), its colour, its charge.</summary>
public sealed class PlaneCrystal : ILightOwner
{
    public Vector2 Position, Face;
    /// <summary>The way out of the wall it faces.</summary>
    public Vector2 Out;
    public int Colour;
    /// <summary>Seconds of glow it holds (0: dark).</summary>
    public float Charge;
    public LightEmitter Light = null!;
    public float Seed;

    /// <summary>How bright it glows (0 dark, 1 full).</summary>
    public float Glow => Charge <= 0f ? 0f : MathF.Min(1f, Charge / CrystalTuning.FadeSeconds);

    float ILightOwner.Energy => Charge;

    /// <summary>A murkling drinking it drinks its glow away.</summary>
    float ILightOwner.Absorb(float amount)
    {
        float taken = MathF.Min(MathF.Max(amount, 0f), Charge);
        Charge -= taken;
        return taken;
    }
}

public sealed partial class PlaneWorld
{
    public List<PlaneCrystal> Crystals { get; } = new();

    /// <summary>The crystals set in the walls: at the swim plane's edge of the rock, apart, from the level's own dice.</summary>
    void PlaceCrystals()
    {
        var rng = new Rng(Map.Seed ^ 0xC2757A15UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Id.Lane << 40) ^ ((ulong)Map.Attempt << 50));
        for (int tries = 0; tries < 3000 && Crystals.Count < CrystalTuning.Count; tries++)
        {
            var p = new Vector2(rng.Range(6f, Map.Size - 6f), rng.Range(6f, Map.Size - 6f));
            if (!Map.IsOpen(p) || Map.InShaft(p, 4f) || Vector2.Distance(p, Map.Start.Position) < CrystalTuning.FromStart) continue;
            // A wall just beside it, at her plane: the crystal is set in its face.
            float a = rng.Range(0f, MathF.Tau);
            Vector2? into = null;
            for (int k = 0; k < 12 && into is null; k++)
            {
                var d = new Vector2(MathF.Cos(a + k * MathF.Tau / 12f), MathF.Sin(a + k * MathF.Tau / 12f));
                if (Map.HeightAt(p + d * 1.1f) > LevelMap.SwimBand + 0.4f) into = d;
            }
            if (into is not { } dir) continue;
            if (Crystals.Exists(c => Vector2.Distance(c.Position, p) < CrystalTuning.Apart)) continue;
            var crystal = new PlaneCrystal { Position = p, Face = p + dir * 0.75f, Out = -dir, Colour = rng.Int(CrystalTuning.Colours.Length), Seed = rng.NextFloat() };
            crystal.Light = Lights.Add(new LightEmitter
            {
                Owner = crystal, Position = p, Range = CrystalTuning.Reach, Intensity = 0f, Colour = CrystalTuning.Colours[crystal.Colour], Drinkable = true,
            });
            Crystals.Add(crystal);
        }
    }

    /// <summary>
    /// How much a lit crystal shows of a creature at this spot (0 none, 1 fully), as her glow shows what is near her: full
    /// within most of its drawn light (ShownReach), thinning at its edge, as bright as the crystal glows; not round rock.
    /// </summary>
    public float CrystalSees(Vector2 at, float radius)
    {
        float seen = 0f;
        foreach (var c in Crystals)
        {
            if (c.Glow <= seen) continue;
            float d = Vector2.Distance(at, c.Position) - radius;
            float inner = CrystalTuning.ShownReach * 0.75f, outer = CrystalTuning.ShownReach;
            if (d >= outer) continue;
            float t = Math.Clamp((d - inner) / (outer - inner), 0f, 1f);
            float s = c.Glow * (1f - t * t * (3f - 2f * t));
            if (s > seen && LineOfSight(c.Position, at)) seen = s;
        }
        return seen;
    }

    /// <summary>
    /// Whether light falls on a hidden creature's core (its middle), so the whole of it shows: within her glow's reach of
    /// her, under a beam of hers that gets to it (no body in the way), or in a lit crystal's light.
    /// </summary>
    public bool CoreLit(object kin, Vector2 at)
    {
        if (Aura.Shining && Vector2.Distance(at, Aura.Position) < Aura.Range && LineOfSight(Aura.Position, at)) return true;
        foreach (var (beam, _) in HerBeams)
            if (BeamLight(beam, at, 0f, 0.2f, kin) > 0f) return true;
        return CrystalSees(at, 0f) > 0.5f;
    }

    /// <summary>Her beams charge the crystals they reach (the more light, the faster); a charged one glows on, then fades out.</summary>
    void StepCrystals()
    {
        foreach (var c in Crystals)
        {
            float light = 0f;
            if (!Defeated)
                foreach (var (beam, worth) in HerBeams) light += worth * BeamLight(beam, c.Position, 0f, CrystalTuning.Radius, c);
            bool was = c.Charge > 0f;
            if (light > 0f) c.Charge = MathF.Min(CrystalTuning.MaxGlow, c.Charge + light * CrystalTuning.ChargeRate * Dt);
            else c.Charge = MathF.Max(0f, c.Charge - Dt);
            if (!was && c.Charge > 0f) Events.Add(new PlaneEvent(PlaneEventType.CrystalLit, c.Face, c.Out));
            c.Light.Intensity = CrystalTuning.Bright * c.Glow;
        }
    }
}
