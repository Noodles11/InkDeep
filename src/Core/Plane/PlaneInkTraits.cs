using System;
using System.Numerics;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Corrupted traits (docs/proposals/LIGHT-WORLD.md §8, phase 5): the Leak's murklings taking on the same physics as
/// the reef, so each fight has a reason.
/// <list type="bullet">
/// <item>chitin-mirror (Shipwreck, Ruins): a glassy half-shell turning slowly round it; a shot meeting the shell glances
/// off it, harmless, and the shell turns half her beam away: hit its soft side;</item>
/// <item>refractor (Temple, Atlantis): a body of high index; a shot at its middle is bent off past it, harmless, one at its
/// edge burns the harder, and her beam bends half away: shine at its edge;</item>
/// <item>band-ward (Alien, Atlantis): ink that lets one band through untouched: her light hurts it only by her other
/// colours (its band read from what it shrugs off);</item>
/// <item>light-eater (Kelp, Ruins): it feeds on weak light (her beam on it under EaterWeak) and burns only in strong: focus it.</item>
/// </list>
/// </summary>
public enum MurkTrait { None, ChitinMirror, Refractor, BandWard, LightEater }

/// <summary>The traits' numbers (LIGHT-WORLD.md §8). (tune)</summary>
public static class InkTraitTuning
{
    /// <summary>The share of a level's budded murklings that carry its biome's trait.</summary>
    public const float Share = 0.35f;
    /// <summary>Chitin-mirror: its shell's turn (rad/s), what a glancing shot keeps, the beam it lets in.</summary>
    public const float ShellTurn = 0.8f, ShellKeep = 0.85f, ShellBeam = 0.5f;
    /// <summary>Refractor: the middle share of its radius that bends a shot away, how far it bends it (rad), an edge hit's burn, the beam it lets in.</summary>
    public const float Core = 0.5f, Bend = 0.6f, Edge = 1.3f, BentBeam = 0.5f;
    /// <summary>Band-ward: what it takes is (1 − its band's share of her light) × Slope − Offset, between Least and 1.</summary>
    public const float WardSlope = 1.5f, WardOffset = 0.5f, WardLeast = 0.1f;
    /// <summary>Light-eater: her light on it under this feeds it (this share of what it would have burnt, back on its meter).</summary>
    public const float EaterWeak = 0.45f, EaterFeeds = 0.5f;

    /// <summary>A biome's traits (none in the Sunlit Reef: it is the lesson before them).</summary>
    public static MurkTrait[] For(Biome biome) => biome switch
    {
        Biome.KelpForest => new[] { MurkTrait.LightEater },
        Biome.Shipwreck => new[] { MurkTrait.ChitinMirror },
        Biome.SunkenRuins => new[] { MurkTrait.ChitinMirror, MurkTrait.LightEater },
        Biome.AlienWreckage => new[] { MurkTrait.BandWard },
        Biome.DrownedTemple => new[] { MurkTrait.Refractor },
        Biome.Atlantis => new[] { MurkTrait.Refractor, MurkTrait.BandWard },
        _ => Array.Empty<MurkTrait>(),
    };
}

public sealed partial class PlaneWorld
{
    static readonly LightHue[] Bands = { LightHue.Red, LightHue.Green, LightHue.Blue };

    /// <summary>A budded murkling's trait, if this biome gives one (with optics, from its own seed).</summary>
    void GiveTrait(Murkling m)
    {
        if (!Options.Optics || !Map.Templated) return;
        var traits = InkTraitTuning.For(Map.Biome);
        float roll = m.Seed * 0.381966f % 1f;
        if (traits.Length == 0 || roll >= InkTraitTuning.Share) return;
        m.Trait = traits[(int)(m.Seed * 7.13f) % traits.Length];
        m.Ward = Bands[(int)(m.Seed * 3.71f) % 3];
        m.Shell = m.Seed;
    }

    /// <summary>A trait's own step: a chitin-mirror's shell turning.</summary>
    void StepTraits()
    {
        foreach (var m in Murklings)
            if (m.Trait == MurkTrait.ChitinMirror) m.Shell += InkTraitTuning.ShellTurn * Dt;
    }

    /// <summary>Her light's share in a band (0..1 of its total).</summary>
    float BandOf(LightHue band)
    {
        var s = HerLightPhysics.Spectrum;
        if (s.Total <= 1e-4f) return 1f / 3f;
        return (band == LightHue.Red ? s.R : band == LightHue.Green ? s.G : s.B) / s.Total;
    }

    /// <summary>What a band-ward takes of her light: by her other colours.</summary>
    float WardTakes(Murkling m) =>
        Math.Clamp((1f - BandOf(m.Ward)) * InkTraitTuning.WardSlope - InkTraitTuning.WardOffset, InkTraitTuning.WardLeast, 1f);

    /// <summary>
    /// One of her shots meets a murkling with a trait: true if it was turned away harmless (glanced off a chitin shell, bent
    /// past a refractor's middle; it flies on); otherwise <paramref name="light"/> is what the trait lets it do.
    /// </summary>
    bool TraitMeets(PlaneShot shot, Murkling m, ref float light)
    {
        switch (m.Trait)
        {
            case MurkTrait.ChitinMirror:
            {
                var face = SafeNormalize(shot.Position - m.Position);
                if (Vector2.Dot(face, new Vector2(MathF.Cos(m.Shell), MathF.Sin(m.Shell))) <= 0f) return false;
                var v = shot.Velocity;
                if (Vector2.Dot(v, face) < 0f) shot.Velocity = v - 2f * Vector2.Dot(v, face) * face;
                shot.Damage *= InkTraitTuning.ShellKeep;
                shot.Struck = m;
                Events.Add(new PlaneEvent(PlaneEventType.ShotBounced, shot.Position, SafeNormalize(shot.Velocity)));
                return true;
            }
            case MurkTrait.Refractor:
            {
                var way = SafeNormalize(shot.Velocity);
                var to = m.Position - shot.Position;
                float off = way.X * to.Y - way.Y * to.X;
                if (MathF.Abs(off) > InkTraitTuning.Core * m.Radius)
                {
                    light *= InkTraitTuning.Edge;
                    return false;
                }
                // Bent away from its middle, to the side it came in on.
                float a = off >= 0f ? -InkTraitTuning.Bend : InkTraitTuning.Bend;
                var v = shot.Velocity;
                shot.Velocity = new Vector2(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));
                shot.Struck = m;
                Events.Add(new PlaneEvent(PlaneEventType.ShotRefracted, shot.Position, SafeNormalize(shot.Velocity), m.Radius));
                return true;
            }
            case MurkTrait.BandWard:
                light *= WardTakes(m);
                return false;
            default:
                return false;
        }
    }

    /// <summary>Her beam's burn on a murkling as its trait lets it (a light-eater fed by a weak light takes none and grows).</summary>
    float TraitBurn(Murkling m, float burn)
    {
        switch (m.Trait)
        {
            case MurkTrait.ChitinMirror: return burn * InkTraitTuning.ShellBeam;
            case MurkTrait.Refractor: return burn * InkTraitTuning.BentBeam;
            case MurkTrait.BandWard: return burn * WardTakes(m);
            case MurkTrait.LightEater:
                if (HerLightOn(m.Position) >= InkTraitTuning.EaterWeak) return burn;
                m.Corruption.Value = MathF.Min(m.Corruption.Max, m.Corruption.Value + burn * InkTraitTuning.EaterFeeds);
                return 0f;
            default: return burn;
        }
    }
}
