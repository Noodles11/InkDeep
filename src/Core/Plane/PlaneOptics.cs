using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A light's energy in three bands (docs/proposals/LIGHT-WORLD.md §2): red ≈ 640 nm, green ≈ 530 nm, blue ≈ 465 nm. From
/// it come the light's colour, its dominant wavelength and frequency, and its photon energy (what it burns per unit of light).
/// </summary>
public readonly record struct Spectrum(float R, float G, float B)
{
    /// <summary>The bands' wavelengths (nm).</summary>
    public const float RedNm = 640f, GreenNm = 530f, BlueNm = 465f;
    /// <summary>c in nm·THz (f = c / λ), and h·c in eV·nm (E = h·c / λ).</summary>
    public const float LightSpeed = 299792.458f, PlanckEvNm = 1239.84f;

    /// <summary>Her own warm gold light, unshifted.</summary>
    public static readonly Spectrum Hers = new(1f, 0.82f, 0.45f);

    public float Total => R + G + B;
    public Spectrum Scale(float k) => new(R * k, G * k, B * k);
    public static Spectrum operator *(Spectrum a, Spectrum b) => new(a.R * b.R, a.G * b.G, a.B * b.B);
    public static Spectrum operator +(Spectrum a, Spectrum b) => new(a.R + b.R, a.G + b.G, a.B + b.B);

    /// <summary>Its colour: the bands scaled so the strongest is 1 (black when dark).</summary>
    public Vector3 Colour
    {
        get
        {
            float most = MathF.Max(R, MathF.Max(G, B));
            return most <= 1e-6f ? Vector3.Zero : new Vector3(R, G, B) / most;
        }
    }

    /// <summary>Its energy-weighted wavelength (nm): what a creature, plant or lock tuned to a frequency reads.</summary>
    public float Wavelength => Total <= 1e-6f ? GreenNm : (R * RedNm + G * GreenNm + B * BlueNm) / Total;

    /// <summary>Its frequency (THz), c / λ.</summary>
    public float Frequency => LightSpeed / Wavelength;

    /// <summary>Its mean photon energy (eV): each band's h·c / λ, weighted by its energy.</summary>
    public float PhotonEnergy => Total <= 1e-6f ? 0f
        : (R * PlanckEvNm / RedNm + G * PlanckEvNm / GreenNm + B * PlanckEvNm / BlueNm) / Total;

    public static Spectrum Exp(Spectrum k, float d) => new(MathF.Exp(-k.R * d), MathF.Exp(-k.G * d), MathF.Exp(-k.B * d));
}

/// <summary>
/// A biome's water as light meets it (LIGHT-WORLD.md §3): how fast it drinks each band (per metre), how much it scatters
/// (per metre: light leaving the beam as a glow round it), its refractive index and its density (against the reef's).
/// </summary>
public sealed record WaterMedium(string Name, Spectrum Absorb, float Scatter, float Index, float Density);

/// <summary>
/// A wall's material as light meets it (LIGHT-WORLD.md §4): how much of each band it gives back at all (Albedo), how much of
/// that is a clean mirror bounce (Mirror), how much of the rest enters it (Into) and bends by its Index, the thickest wall
/// light crosses (Through, metres), and how much of what it drinks it keeps and gives out as a glow (Stores).
/// </summary>
public sealed record RockMaterial(string Name, Spectrum Albedo, float Mirror, float Into, float Index, float Through, float Stores);

/// <summary>How light meeting a wall shares itself out (each 0..1, adding to 1): mirrored, scattered back, gone in, drunk.</summary>
public readonly record struct Meeting(float Mirrored, float Scattered, float Entered, float Drunk);

/// <summary>
/// Her light's physical properties this step (LIGHT-WORLD.md §2), and what this water does to them. Power, Width and
/// Pulse are her manual light's (LightSettings; 1 otherwise): her light's amount, her beam's width, and its pulse's
/// multiplier this step.
/// </summary>
public readonly record struct LightPhysics(Spectrum Spectrum, float Coherence, float Reach, float Punch, float Halo, float Power = 1f, float Width = 1f, float Pulse = 1f)
{
    public float Wavelength => Spectrum.Wavelength;
    public float Frequency => Spectrum.Frequency;
    public Vector3 Colour => Spectrum.Colour;

    /// <summary>Nothing changed: her plain light in the reference water (the game without optics).</summary>
    public static readonly LightPhysics Neutral = new(Spectrum.Hers, OpticsTuning.Coherence, 1f, 1f, 1f);
}

/// <summary>
/// Her light set by hand (the F1 menu's gauges): its spectrum (the bands' mix; the total is kept, light is conserved), its
/// power (× her light's amount: her beam's burn and her shots' damage), its coherence (0..1), her beam's width (× its
/// half-angle) and its pulse (Hz: her light swelling and ebbing, 0 steady). Set, it takes the place of what her pearls
/// make of her light, in any level (the water's effect on it only with optics).
/// </summary>
public sealed class LightSettings
{
    public Spectrum Spectrum = Spectrum.Hers;
    public float Power = 1f, Coherence = OpticsTuning.Coherence, Width = 1f, PulseHz;

    /// <summary>Her light's pulse swing: ± this share of it at the pulse's peak and trough.</summary>
    public const float PulseDepth = 0.5f;
    public const float PowerLeast = 0.25f, PowerMost = 3f, WidthLeast = 0.3f, WidthMost = 2.5f, PulseMost = 8f, BandMost = 2f;
}

/// <summary>Phase 1's values (LIGHT-WORLD.md §14). (tune)</summary>
public static class OpticsTuning
{
    /// <summary>How much of her power one copy of a colour word moves into its band.</summary>
    public const float ColourShift = 0.6f;
    /// <summary>The share of her light left where her reach ends; her reach in a water against the reference's, clamped.</summary>
    public const float ReachThreshold = 0.35f, ReachLeast = 0.55f, ReachMost = 1.5f;
    /// <summary>A plain beam's coherence, the Lance's, and what her focus rhythm adds at its full.</summary>
    public const float Coherence = 0.15f, LanceCoherence = 0.9f, FocusCoherence = 0.4f;
    /// <summary>Her punch (damage at mid reach) against the reference's, clamped.</summary>
    public const float PunchLeast = 0.6f, PunchMost = 1.4f;
    /// <summary>The least share of its power a shot keeps after the water (against the reference water's).</summary>
    public const float ShotFloor = 0.25f;
    /// <summary>A bounced shot keeps this much of its power (at least BounceFloor).</summary>
    public const float MirrorKeep = 0.85f, BounceFloor = 0.35f;
    /// <summary>A stored wall glow: its seconds at full store, its brightness × the stored share, and its reach (m).</summary>
    public const float GlowSeconds = 3f, GlowLight = 0.9f, GlowReach = 3f;
    /// <summary>What a material inside drinks per metre a shot crosses it.</summary>
    public const float InsideDrink = 0.15f;
}

/// <summary>The light pearls' numbers (LIGHT-WORLD.md §10, phase 5). (tune)</summary>
public static class PearlOpticsTuning
{
    /// <summary>How much of her light Abyssal Blue or the Ember Coal pushes into its band (× her total), and what each does to her punch and reach.</summary>
    public const float Shift = 1.2f, UvPunch = 1.25f, InfraReach = 0.7f, InfraPunch = 0.75f;
    /// <summary>Siltwalker: the water it works in (its scatter per metre at least), her reach and halo there.</summary>
    public const float Silty = 0.015f, SiltReach = 1.2f, SiltHalo = 0.5f;
    /// <summary>Lens of the Deep: a shot's damage grows by this share a metre it flies, up to LensMost.</summary>
    public const float LensPerMetre = 0.06f, LensMost = 1.6f;
    public static float Lens(float metres) => MathF.Min(LensMost, 1f + LensPerMetre * metres);
}

/// <summary>Each biome's water and rock (LIGHT-WORLD.md §3.1, §4.1).</summary>
public static class BiomeOptics
{
    /// <summary>The reference water: the Sunlit Reef's (her reach and punch are measured against it).</summary>
    public static readonly WaterMedium Reference = new("clear reef water", new Spectrum(0.045f, 0.020f, 0.016f), 0.012f, 1.333f, 1f);

    public static WaterMedium Water(Biome biome) => biome switch
    {
        Biome.KelpForest => new("green kelp water", new Spectrum(0.050f, 0.014f, 0.040f), 0.012f, 1.334f, 1.01f),
        Biome.Shipwreck => new("silty wreck water", new Spectrum(0.040f, 0.026f, 0.024f), 0.040f, 1.335f, 1.02f),
        Biome.SunkenRuins => new("yellow dust water", new Spectrum(0.022f, 0.020f, 0.075f), 0.016f, 1.334f, 1.02f),
        Biome.AlienWreckage => new("cold clear water", new Spectrum(0.100f, 0.034f, 0.012f), 0.004f, 1.333f, 1f),
        Biome.DrownedTemple => new("dense brine", new Spectrum(0.040f, 0.026f, 0.024f), 0.018f, 1.380f, 1.2f),
        Biome.Atlantis => new("deep clear water", new Spectrum(0.070f, 0.030f, 0.016f), 0.008f, 1.345f, 1.1f),
        _ => Reference,
    };

    public static RockMaterial Rock(Biome biome) => biome switch
    {
        Biome.KelpForest => new("kelp holdfast rock", new Spectrum(0.25f, 0.55f, 0.20f), 0.08f, 0f, 1.5f, 0f, 0.30f),
        Biome.Shipwreck => new("iron hull", new Spectrum(0.60f, 0.50f, 0.40f), 0.65f, 0f, 1.5f, 0f, 0f),
        Biome.SunkenRuins => new("gilt sandstone", new Spectrum(0.70f, 0.60f, 0.35f), 0.20f, 0f, 1.5f, 0f, 0.55f),
        Biome.AlienWreckage => new("crystal lattice", new Spectrum(0.30f, 0.55f, 0.85f), 0.15f, 0.70f, 1.9f, 3f, 0.10f),
        Biome.DrownedTemple => new("black glass", new Spectrum(0.35f, 0.35f, 0.40f), 0.50f, 0.10f, 1.5f, 1f, 0.20f),
        Biome.Atlantis => new("orichalcum and quartz", new Spectrum(0.85f, 0.65f, 0.45f), 0.80f, 0.25f, 1.6f, 2f, 0.25f),
        _ => new("living coral", new Spectrum(0.55f, 0.50f, 0.45f), 0.15f, 0f, 1.5f, 0f, 0.15f),
    };
}

/// <summary>The rules light follows (LIGHT-WORLD.md §2, §5): through water, off and into walls.</summary>
public static class Optics
{
    /// <summary>What a water takes from each band per metre: its absorption, and the share of its scattering a beam this coherent loses.</summary>
    public static Spectrum Extinction(WaterMedium w, float coherence)
    {
        float s = w.Scatter * (1f - 0.5f * Math.Clamp(coherence, 0f, 1f));
        return new Spectrum(w.Absorb.R + s, w.Absorb.G + s, w.Absorb.B + s);
    }

    /// <summary>What of a light is left after d metres of a water (Beer–Lambert, per band).</summary>
    public static Spectrum Transmitted(Spectrum light, WaterMedium w, float coherence, float d) =>
        light * Spectrum.Exp(Extinction(w, coherence), MathF.Max(d, 0f));

    /// <summary>The share of a light's energy left after d metres.</summary>
    public static float Kept(Spectrum light, WaterMedium w, float coherence, float d) =>
        light.Total <= 1e-6f ? 0f : Transmitted(light, w, coherence, d).Total / light.Total;

    /// <summary>How far a light gets before the share left falls to <paramref name="threshold"/> (metres; bisection, to 400 m).</summary>
    public static float Distance(Spectrum light, WaterMedium w, float coherence, float threshold = OpticsTuning.ReachThreshold)
    {
        float lo = 0f, hi = 400f;
        if (Kept(light, w, coherence, hi) > threshold) return hi;
        for (int i = 0; i < 40; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (Kept(light, w, coherence, mid) > threshold) lo = mid;
            else hi = mid;
        }
        return 0.5f * (lo + hi);
    }

    /// <summary>A light's photon energy against her plain light's: blue burns harder per unit of light, red less.</summary>
    public static float Potency(Spectrum light) => light.PhotonEnergy / Spectrum.Hers.PhotonEnergy;

    /// <summary>A light's reach in a water against her plain light's in the reference water (clamped, LIGHT-WORLD.md §2.3).</summary>
    public static float ReachScale(Spectrum light, WaterMedium w, float coherence) =>
        Math.Clamp(Distance(light, w, coherence) / Distance(Spectrum.Hers, BiomeOptics.Reference, OpticsTuning.Coherence),
            OpticsTuning.ReachLeast, OpticsTuning.ReachMost);

    /// <summary>
    /// What a light delivers d metres out against her plain light in the reference water at the same distance: its potency
    /// × the share the water left it, over the reference's share.
    /// </summary>
    public static float PunchAt(Spectrum light, WaterMedium w, float coherence, float d)
    {
        float reference = Kept(Spectrum.Hers, BiomeOptics.Reference, OpticsTuning.Coherence, d);
        return reference <= 1e-6f ? 0f : Potency(light) * Kept(light, w, coherence, d) / reference;
    }

    /// <summary>How much a water makes a light glow round its path, against the reference's (its scattering, less for a coherent light).</summary>
    public static float Halo(WaterMedium w, float coherence) =>
        w.Scatter * (1f - 0.5f * coherence) / (BiomeOptics.Reference.Scatter * (1f - 0.5f * OpticsTuning.Coherence));

    /// <summary>A direction reflected off a surface (the normal either side).</summary>
    public static Vector2 Reflect(Vector2 d, Vector2 n) => d - 2f * Vector2.Dot(d, n) * n;

    /// <summary>
    /// A direction bent crossing from index n1 into n2 (Snell's law); <paramref name="n"/> is the surface's normal on the side
    /// the light comes from (against d). Null: it cannot cross (total internal reflection).
    /// </summary>
    public static Vector2? Refract(Vector2 d, Vector2 n, float n1, float n2)
    {
        if (Vector2.Dot(d, n) > 0f) n = -n;
        float cosi = -Vector2.Dot(n, d), eta = n1 / n2;
        float k = 1f - eta * eta * (1f - cosi * cosi);
        if (k < 0f) return null;
        return Vector2.Normalize(eta * d + (eta * cosi - MathF.Sqrt(k)) * n);
    }

    /// <summary>
    /// How a light meeting a material shares itself out (LIGHT-WORLD.md §5): the albedo share given back (more of it a
    /// mirror at grazing angles, Schlick's Fresnel), of the rest the Into share entering it, and the remainder drunk.
    /// cos: the cosine between the light's way and the surface's normal.
    /// </summary>
    public static Meeting Meet(Spectrum light, RockMaterial m, float cos, float waterIndex = 1.333f)
    {
        float total = MathF.Max(light.Total, 1e-6f);
        float back = (light * m.Albedo).Total / total;
        float r0 = (m.Index - waterIndex) / (m.Index + waterIndex);
        r0 *= r0;
        float grazing = MathF.Pow(1f - Math.Clamp(MathF.Abs(cos), 0f, 1f), 5f);
        float mirror = Math.Clamp(m.Mirror + (1f - m.Mirror) * (r0 + (1f - r0) * grazing) * 0.5f, 0f, 1f);
        float mirrored = back * mirror, scattered = back * (1f - mirror);
        float entered = (1f - back) * m.Into;
        return new Meeting(mirrored, scattered, entered, MathF.Max(0f, 1f - mirrored - scattered - entered));
    }
}

/// <summary>A wall's stored light (LIGHT-WORLD.md §5): it glows where a shot was drunk, a light the murklings see, fading.</summary>
public sealed class WallGlow : ILightOwner
{
    public Vector2 At;
    public float Life, Most;
    public LightEmitter Light = null!;
    public float Glow => Most <= 0f ? 0f : Math.Clamp(Life / Most, 0f, 1f);
    public float Energy => Life > 0f ? Glow : 0f;

    public float Absorb(float amount)
    {
        float taken = MathF.Min(MathF.Max(amount, 0f), Glow) * Most;
        Life -= taken;
        return taken / MathF.Max(Most, 1e-6f);
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>This level's water and rock (the reef's in a level that is not a biome's).</summary>
    public WaterMedium Water => _water ??= Options.Optics && Map.Templated ? BiomeOptics.Water(Map.Biome) : BiomeOptics.Reference;
    public RockMaterial Rock => _rockMaterial ??= BiomeOptics.Rock(Options.Optics && Map.Templated ? Map.Biome : Biome.SunlitReef);
    WaterMedium? _water;
    RockMaterial? _rockMaterial;

    /// <summary>Her light's physics this step (LIGHT-WORLD.md §2): neutral without optics.</summary>
    public LightPhysics HerLightPhysics { get; private set; } = LightPhysics.Neutral;

    /// <summary>
    /// Her light's spectrum now, with or without optics: set by hand, or her colour words' (SpectrumOf). Every jelly's
    /// colours follow it (the view's HerColor.FollowLight), as her beam and her shots do.
    /// </summary>
    public Spectrum HerSpectrum => Options.Optics || LightSet ? HerLightPhysics.Spectrum
        : SpectrumOf(Run.Loadout.Shot.Ember, Run.Loadout.Shot.Bloom, Run.Loadout.Shot.Frost);

    /// <summary>Her light set (by hand in the F1 menu, or grown with her energy: PlaneGrowth.cs), not only her pearls'.</summary>
    public bool LightSet => ManualLight is not null || GrowthTuning.LightGrown(Run);

    /// <summary>Her light's settings this step: the F1 gauges' or her pearls', her growth laid over them; null if neither is set.</summary>
    LightSettings? SetLight()
    {
        if (!GrowthTuning.LightGrown(Run)) return ManualLight;
        return GrowthTuning.Grow(ManualLight ?? PearlLight(), Run);
    }

    /// <summary>Her light set by hand (the F1 menu's gauges), or null: her pearls make it.</summary>
    public LightSettings? ManualLight { get; set; }

    /// <summary>A spectrum with a band pushed in, its total kept (light is conserved).</summary>
    static Spectrum Shifted(Spectrum s, Spectrum push)
    {
        var t = s + push.Scale(s.Total);
        return t.Scale(s.Total / t.Total);
    }

    /// <summary>A manual light's spectrum with her light's total (its mix only: light is conserved).</summary>
    static Spectrum Conserved(Spectrum s) => s.Total <= 1e-4f ? Spectrum.Hers : s.Scale(Spectrum.Hers.Total / s.Total);

    /// <summary>Walls glowing with light they stored.</summary>
    public List<WallGlow> WallGlows { get; } = new();

    /// <summary>
    /// Her spectrum: her warm gold, each copy of a colour word moving ColourShift of her power into its band, the total kept
    /// (light is conserved: a colour is not more light).
    /// </summary>
    public static Spectrum SpectrumOf(int ember, int bloom, int frost)
    {
        var s = Spectrum.Hers;
        float total = s.Total;
        var shifted = s + new Spectrum(ember, bloom, frost).Scale(OpticsTuning.ColourShift);
        return shifted.Scale(total / shifted.Total);
    }

    /// <summary>Her light's properties, and what this water does to them: reach, punch at mid reach, halo.</summary>
    void StepOptics()
    {
        var manual = SetLight();
        HerPulseHz = manual?.PulseHz ?? 0f;
        if (!Options.Optics && manual is null)
        {
            HerLightPhysics = LightPhysics.Neutral;
            return;
        }
        var shot = Run.Loadout.Shot;
        var flags = Run.Loadout.Flags;
        var spectrum = manual is not null ? Conserved(manual.Spectrum) : SpectrumOf(shot.Ember, shot.Bloom, shot.Frost);
        // The light pearls (LIGHT-WORLD.md §10): Abyssal Blue pushes her light toward near-UV, the Ember Coal toward infrared.
        if (flags.Contains("nearUv")) spectrum = Shifted(spectrum, new Spectrum(0f, 0f, PearlOpticsTuning.Shift));
        if (flags.Contains("infrared")) spectrum = Shifted(spectrum, new Spectrum(PearlOpticsTuning.Shift, 0f, 0f));
        float coherence = Math.Clamp((manual is not null ? manual.Coherence : shot.Laser ? OpticsTuning.LanceCoherence : OpticsTuning.Coherence)
            + OpticsTuning.FocusCoherence * Focus, 0f, 1f);
        float reach = Optics.ReachScale(spectrum, Water, coherence);
        float punch = Math.Clamp(Optics.PunchAt(spectrum, Water, coherence, LightTuning.BeamReach * 0.5f), OpticsTuning.PunchLeast, OpticsTuning.PunchMost);
        float power = 1f, width = 1f, pulse = 1f;
        if (manual is not null)
        {
            power = Math.Clamp(manual.Power, LightSettings.PowerLeast, LightSettings.PowerMost);
            width = Math.Clamp(manual.Width, LightSettings.WidthLeast, LightSettings.WidthMost);
            if (manual.PulseHz > 0f) pulse = 1f + LightSettings.PulseDepth * MathF.Sin(Time * manual.PulseHz * MathF.Tau);
        }
        float halo = Optics.Halo(Water, coherence);
        if (flags.Contains("nearUv")) punch *= PearlOpticsTuning.UvPunch;
        if (flags.Contains("infrared"))
        {
            reach *= PearlOpticsTuning.InfraReach;
            punch *= PearlOpticsTuning.InfraPunch;
        }
        // Siltwalker: in scattering water her light keeps itself together.
        if (flags.Contains("siltwalker") && Water.Scatter >= PearlOpticsTuning.Silty)
        {
            reach *= PearlOpticsTuning.SiltReach;
            halo *= PearlOpticsTuning.SiltHalo;
        }
        HerLightPhysics = new LightPhysics(spectrum, coherence, reach, punch * power * pulse, halo, power, width, pulse);
        StepWallGlows();
    }

    void StepWallGlows()
    {
        for (int i = WallGlows.Count - 1; i >= 0; i--)
        {
            var g = WallGlows[i];
            g.Life -= Dt;
            g.Light.Intensity = OpticsTuning.GlowLight * g.Glow;
            if (g.Life > 0f) continue;
            Lights.Remove(g.Light);
            WallGlows.RemoveAt(i);
        }
    }

    /// <summary>A shot's share of its power left after d metres of this water, against the reference water's (floored).</summary>
    float ShotKept(float d)
    {
        var l = HerLightPhysics;
        float here = Optics.Kept(l.Spectrum, Water, l.Coherence, d), reference = Optics.Kept(Spectrum.Hers, BiomeOptics.Reference, OpticsTuning.Coherence, d);
        return MathF.Max(OpticsTuning.ShotFloor, reference <= 1e-6f ? 1f : here / reference);
    }

    /// <summary>A shot flew from <paramref name="before"/> to <paramref name="after"/> metres: the water takes its share of its power.</summary>
    void ShotThroughWater(PlaneShot shot, float before, float after)
    {
        if (!Options.Optics || after <= before) return;
        shot.Damage *= ShotKept(after) / ShotKept(before);
        // Lens of the Deep: her shots focus as they fly, harder the farther they go.
        if (Run.Loadout.Flags.Contains("deepLens")) shot.Damage *= PearlOpticsTuning.Lens(after) / PearlOpticsTuning.Lens(before);
    }

    /// <summary>
    /// One of her gun's shots meets the level's rock (LIGHT-WORLD.md §5): by the material's shares it bounces on (a mirror),
    /// passes through a thin wall bent (crystal, quartz), or is drunk there, the wall glowing by what it stored. True: it
    /// was dealt with (it carries on, or it is gone).
    /// </summary>
    /// <summary>
    /// A volley's shots take her light as her beam has it: her colours (drawn in them, acting on what they hit), and, with
    /// optics or her light set by hand, its potency (blue burns harder, red less), its power and pulse (their damage and
    /// light), its width (their size, as her beam's width) and its reach in this water (their range), and the water's
    /// density (slower in brine).
    /// </summary>
    void ShapeOpticsVolley(int first)
    {
        // (Her colours as her beam has them now: her colour words mixed, or Neon Rave's one at a time.)
        var all = LightHue.None;
        foreach (var h in Primaries()) all |= h;
        if (NeonHue != LightHue.None) all = NeonHue;
        for (int i = first; i < Shots.Count; i++) Shots[i].Light = all;
        if (!Options.Optics && !LightSet) return;
        var l = HerLightPhysics;
        float potency = Optics.Potency(l.Spectrum) * l.Power * l.Pulse, slow = 1f / MathF.Sqrt(MathF.Max(Water.Density, 0.5f));
        for (int i = first; i < Shots.Count; i++)
        {
            var s = Shots[i];
            s.Damage *= potency;
            s.Velocity *= slow;
            s.Speed0 *= slow;
            s.Radius *= l.Width;
            s.BaseRadius *= l.Width;
            if (s.Range > 0f) s.Range *= l.Reach;
        }
    }

    /// <summary>The share of a shot's colour that marks what it hits: frost's chill a hit adds (of a full standstill).</summary>
    public const float ShotChill = 0.35f;

    /// <summary>
    /// Her shot's colours on what it hits (as her beam's act, PlaneColour.ApplyColour, but at once): ember leaves it burning
    /// on, frost chills it a step toward a standstill, bloom and sunfire mark it for when it is freed, lull makes it lose
    /// her, and ember on what frost has stilled shatters it.
    /// </summary>
    void ShotColour(object on, LightHue hue, Vector2 at)
    {
        if (hue == LightHue.None) return;
        if (!_glare.TryGetValue(on, out var g)) _glare[on] = g = new Glare();
        var s = Run.Loadout.Shot;
        bool red = (hue & LightHue.Red) != 0, green = (hue & LightHue.Green) != 0, blue = (hue & LightHue.Blue) != 0;
        if (red)
        {
            g.Afterglow = MathF.Max(g.Afterglow, ColourTuning.Ember(s.Ember));
            g.Ember = true;
        }
        if (blue)
        {
            g.Chill = MathF.Min(1f, g.Chill + ShotChill / MathF.Max(ColourTuning.Frost(s.Frost) / ColourTuning.FrostTime, 0.2f));
            if (g.Chill >= 1f) g.Frozen = MathF.Max(g.Frozen, ColourTuning.Mark);
        }
        if (green) g.Bloom = ColourTuning.Mark;
        if (red && green) g.Sun = ColourTuning.Mark;
        if (green && blue) g.Charm = MathF.Max(g.Charm, ColourTuning.LullTime);
        if (red && blue && g.Chill >= 1f)
        {
            g.Chill = 0f;
            g.Frozen = 0f;
            _colourBursts.Add((at, true));
        }
    }

    bool ShotMeetsRock(PlaneShot shot, Vector2 dir, Vector2 wallAt)
    {
        if (!Options.Optics || !shot.Gun || shot.BouncesLeft > 0) return false;
        var g = Map.Gradient(wallAt);
        // (Uphill is into the wall: its face looks the other way.)
        var face = g.LengthSquared() > 1e-6f ? -Vector2.Normalize(g) : -dir;
        var rock = Rock;
        var meet = Optics.Meet(HerLightPhysics.Spectrum, rock, Vector2.Dot(dir, face), Water.Index);
        float roll = _bubbleRng.NextFloat();
        if (roll < meet.Mirrored)
        {
            var out1 = Optics.Reflect(dir, face);
            if (Vector2.Dot(out1, face) <= 0f) out1 = -dir;
            shot.Velocity = out1 * shot.Velocity.Length();
            shot.Damage *= MathF.Max(OpticsTuning.MirrorKeep, OpticsTuning.BounceFloor);
            Events.Add(new PlaneEvent(PlaneEventType.ShotBounced, shot.Line, out1, shot.Radius));
            return true;
        }
        if (roll < meet.Mirrored + meet.Entered && rock.Through > 0f && Through(shot, dir, face, rock, wallAt)) return true;
        // Drunk: it goes out here; the share the wall stores glows on.
        Pop(shot, dir);
        if (rock.Stores > 0f) AddWallGlow(shot.Line, rock.Stores * MathF.Min(ShotLight(shot) + 0.3f, 1.3f));
        return true;
    }

    /// <summary>Into a thin wall, bent; out of its far side bent back, if it is thin enough and the light can leave. False: drunk inside.</summary>
    bool Through(PlaneShot shot, Vector2 dir, Vector2 face, RockMaterial rock, Vector2 at)
    {
        if (Optics.Refract(dir, face, Water.Index, rock.Index) is not { } inside) return false;
        for (float d = 0.1f; d <= rock.Through; d += 0.1f)
        {
            var q = at + inside * d;
            if (!Map.IsOpen(q) || _rocks.Exists(r => r.Contains(q))) continue;
            // Out of the far side: bent back by its face there.
            var g = Map.Gradient(q - inside * 0.1f);
            var exitFace = g.LengthSquared() > 1e-6f ? -Vector2.Normalize(g) : inside;
            if (Optics.Refract(inside, exitFace, rock.Index, Water.Index) is not { } outside) return false;
            shot.Line = shot.Position = shot.From = q;
            shot.Velocity = outside * shot.Velocity.Length();
            shot.Damage *= MathF.Exp(-OpticsTuning.InsideDrink * d);
            shot.Traveled += d;
            Events.Add(new PlaneEvent(PlaneEventType.ShotRefracted, q, outside, d));
            return true;
        }
        return false;
    }

    /// <summary>A wall glowing with stored light: a light the murklings see, fading over GlowSeconds × its strength.</summary>
    public void AddWallGlow(Vector2 at, float strength)
    {
        if (strength <= 0.02f) return;
        var g = new WallGlow { At = at, Most = OpticsTuning.GlowSeconds * strength * (Options.Tidy ? TidyTuning.GlowShare : 1f) };
        g.Life = g.Most;
        g.Light = Lights.Add(new LightEmitter
        {
            Owner = g, Position = at, Range = OpticsTuning.GlowReach * MathF.Sqrt(strength),
            Intensity = OpticsTuning.GlowLight, Colour = new Vector3(1f, 0.86f, 0.6f),
        });
        WallGlows.Add(g);
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>Her light as her pearls make it, as a manual light's settings (the F1 menu's gauges start from it).</summary>
    public LightSettings PearlLight()
    {
        var shot = Run.Loadout.Shot;
        return new LightSettings
        {
            Spectrum = SpectrumOf(shot.Ember, shot.Bloom, shot.Frost),
            Coherence = shot.Laser ? OpticsTuning.LanceCoherence : OpticsTuning.Coherence,
        };
    }

    /// <summary>What a light would be in this water (its pulse steady, no focus): for the F1 menu's read-out while the game stands still.</summary>
    public LightPhysics Preview(LightSettings s)
    {
        var spectrum = Conserved(s.Spectrum);
        float coherence = Math.Clamp(s.Coherence, 0f, 1f);
        float power = Math.Clamp(s.Power, LightSettings.PowerLeast, LightSettings.PowerMost);
        float punch = Math.Clamp(Optics.PunchAt(spectrum, Water, coherence, LightTuning.BeamReach * 0.5f), OpticsTuning.PunchLeast, OpticsTuning.PunchMost);
        return new LightPhysics(spectrum, coherence, Optics.ReachScale(spectrum, Water, coherence), punch * power, Optics.Halo(Water, coherence),
            power, Math.Clamp(s.Width, LightSettings.WidthLeast, LightSettings.WidthMost));
    }
}
