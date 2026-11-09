using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// How a depth looks (THEME-BIBLE §6.2 light budget, §6.3 palette, §7 the depths): its water, seabed and reef colours,
/// its sun, how sharp its caustics and how strong its god rays, and its look's Menace (0 in the Shallows, about 0.6 at
/// most), which thickens the murk, darkens the vignette and drains the colour. No depth is much darker than the Coral
/// Carnival: the dark grows with the descent, but every level stays readable. One look drives every reef shader through the
/// reef_* shader globals, the environment and the sun, so the descent is felt everywhere at once.
/// Warmth belongs to Clementine and to healthy life: the sun is a pale gold, never her tangerine.
/// </summary>
public sealed record ReefLook
{
    public required string Name { get; init; }
    /// <summary>Water near the swim level, and the deep water below it; murk is the deepest, light-starved colour.</summary>
    public required Color WaterShallow { get; init; }
    public required Color WaterDeep { get; init; }
    public required Color Murk { get; init; }
    public required Color Sand { get; init; }
    /// <summary>The reef's bare rock, and the life encrusting it: coral, a second coral, algae turf, sponges, a pale butter.</summary>
    public required Color Rock { get; init; }
    public required Color Coral { get; init; }
    public required Color Coral2 { get; init; }
    public required Color Algae { get; init; }
    public required Color Sponge { get; init; }
    public required Color Butter { get; init; }
    public required Color Sun { get; init; }
    public required float SunEnergy { get; init; }
    public required Color Ambient { get; init; }
    public required float AmbientEnergy { get; init; }
    /// <summary>Caustic and god-ray strength (0 none).</summary>
    public required float Caustics { get; init; }
    public required float Rays { get; init; }
    public required float Menace { get; init; }
    public required float Saturation { get; init; }
    public required float Vignette { get; init; }
    /// <summary>How thick the veil over the deeps is (0 crystal, 1 murk).</summary>
    public required float Fog { get; init; }
    /// <summary>
    /// Her lantern against the depth (DESIGN-TOPDOWN §5.1 player glow): how strong and far her light carries, 1 in the
    /// sunlit Shallows (supplemental), growing as the sun fails until she is the lantern of the Trench and the Abyss.
    /// </summary>
    public float Lantern { get; init; } = 1f;
    /// <summary>Contrast after tone mapping (1 neutral): the Shallows want punchy light and shade.</summary>
    public float Contrast { get; init; } = 1f;
    /// <summary>A cave's: no light in it but hers, her shots' and its few motes of fluorescent plankton (GlowPlankton).</summary>
    public bool Dark { get; init; }
    /// <summary>The flora's palette: low and high ends for each of two variants per species group.</summary>
    public required Color FanA { get; init; }
    public required Color FanB { get; init; }
    public required Color SoftCoral { get; init; }
    public required Color HardCoral { get; init; }
    public required Color Grass { get; init; }

    static Color C(string hex) => Color.FromHtml(hex);

    /// <summary>Partway from one depth's look to another's (the dive through the Crack crosses from one to the next).</summary>
    public static ReefLook Blend(ReefLook a, ReefLook b, float t)
    {
        Color L(Color x, Color y) => x.Lerp(y, t);
        float F(float x, float y) => Mathf.Lerp(x, y, t);
        return new ReefLook
        {
            Name = t < 0.5f ? a.Name : b.Name,
            WaterShallow = L(a.WaterShallow, b.WaterShallow), WaterDeep = L(a.WaterDeep, b.WaterDeep), Murk = L(a.Murk, b.Murk),
            Sand = L(a.Sand, b.Sand), Rock = L(a.Rock, b.Rock), Coral = L(a.Coral, b.Coral), Coral2 = L(a.Coral2, b.Coral2),
            Algae = L(a.Algae, b.Algae), Sponge = L(a.Sponge, b.Sponge), Butter = L(a.Butter, b.Butter),
            Sun = L(a.Sun, b.Sun), SunEnergy = F(a.SunEnergy, b.SunEnergy), Ambient = L(a.Ambient, b.Ambient), AmbientEnergy = F(a.AmbientEnergy, b.AmbientEnergy),
            Caustics = F(a.Caustics, b.Caustics), Rays = F(a.Rays, b.Rays), Menace = F(a.Menace, b.Menace), Saturation = F(a.Saturation, b.Saturation),
            Vignette = F(a.Vignette, b.Vignette), Fog = F(a.Fog, b.Fog), Contrast = F(a.Contrast, b.Contrast), Lantern = F(a.Lantern, b.Lantern),
            Dark = t < 0.5f ? a.Dark : b.Dark,
            FanA = L(a.FanA, b.FanA), FanB = L(a.FanB, b.FanB), SoftCoral = L(a.SoftCoral, b.SoftCoral), HardCoral = L(a.HardCoral, b.HardCoral), Grass = L(a.Grass, b.Grass),
        };
    }

    /// <summary>The seven depths (THEME-BIBLE §6.3). Depth 1 is tuned; the rest follow the palette table, to be tuned as they are built.</summary>
    public static ReefLook For(int depth) => depth switch
    {
        <= 1 => OpenShallows,
        2 => Kelp,
        3 => Galleon,
        4 => Carnival,
        5 => Trench,
        6 => Abyss,
        _ => Tank,
    };

    /// <summary>Depth 1 — Sunlit Shallows · The Bright Ache: saturated and hopeful, turquoise over sand, pink coral, sharp caustics.</summary>
    public static readonly ReefLook Shallows = new()
    {
        Name = "Sunlit Shallows",
        WaterShallow = C("#7FDCD2"), WaterDeep = C("#3FBFBE"), Murk = C("#17707A"),
        Sand = C("#E3D2A0"), Rock = C("#C9C2AC"),
        Coral = C("#F089A8"), Coral2 = C("#B79BE0"), Algae = C("#A9C46B"), Sponge = C("#5EC7BC"), Butter = C("#F3DE8C"),
        Sun = C("#FFF8EC"), SunEnergy = 0.78f, Ambient = C("#3FA6C0"), AmbientEnergy = 0.13f,
        Caustics = 1f, Rays = 1f, Menace = 0f, Saturation = 1.3f, Vignette = 0.36f, Fog = 0.3f, Contrast = 1.22f,
        FanA = C("#B48CDC"), FanB = C("#F28BB0"), SoftCoral = C("#F6A5C0"), HardCoral = C("#F1DDA2"), Grass = C("#8FC85A"),
    };

    /// <summary>
    /// The Sunlit Shallows as she swims them (Depth 1 and the tutorial, and the open reef's zone): the Shallows' own
    /// palette, dimmed so the wide sand never washes out: a deeper turquoise, a toned-down sand and a softer sun, a little brighter than the
    /// bay's evening, so the characters stay the brightest things in it.
    /// </summary>
    public static readonly ReefLook OpenShallows = Shallows with
    {
        WaterShallow = C("#4AA3AE"), WaterDeep = C("#25839A"), Murk = C("#0F4D5E"),
        Sand = C("#BDAE84"), Rock = C("#A9A595"),
        SunEnergy = 0.4f, AmbientEnergy = 0.1f, Caustics = 0.7f, Rays = 0.7f, Fog = 0.24f, Vignette = 0.42f, Contrast = 1.2f,
    };

    /// <summary>
    /// Lantern Bay (docs/RPG-PROPOSAL.md §3): the Sunlit Shallows themselves at the soft end of a summer day, safe and
    /// joyful. The same turquoise over sand, pink coral and saturated life, the same caustics and shafts, all of it
    /// gentler and dimmed, the sun low and pale gold; so Clementine's tangerine is the brightest thing in it, her light
    /// pooling round her wherever she swims. No menace: the dusk here is warm, never gloomy.
    /// </summary>
    public static readonly ReefLook Bay = Shallows with
    {
        Name = "Lantern Bay",
        WaterShallow = C("#3E90AA"), WaterDeep = C("#1F6E90"), Murk = C("#0C3A56"),
        Sand = C("#B0A27C"), Rock = C("#96948A"),
        Sun = C("#FFEACB"), SunEnergy = 0.4f, Ambient = C("#2E86AA"), AmbientEnergy = 0.1f,
        Caustics = 0.8f, Rays = 0.6f, Menace = 0f, Saturation = 1.3f, Vignette = 0.42f, Fog = 0.2f, Contrast = 1.22f,
        Lantern = 1.25f,
    };

    public static readonly ReefLook Kelp = Shallows with
    {
        Name = "Kelp Jungle",
        WaterShallow = C("#8FBF6F"), WaterDeep = C("#2E7D5B"), Murk = C("#173F31"),
        Sand = C("#B9B07E"), Rock = C("#5C6E46"), Coral = C("#C88AA0"), Coral2 = C("#8C8FBF"), Algae = C("#7FA84A"),
        Sun = C("#E8D878"), SunEnergy = 0.95f, Ambient = C("#7FB88A"), AmbientEnergy = 0.7f,
        Caustics = 0.6f, Rays = 0.75f, Menace = 0.15f, Saturation = 1f, Vignette = 0.4f, Fog = 0.45f, Lantern = 1.1f,
    };

    public static readonly ReefLook Galleon = Shallows with
    {
        Name = "Sunken Galleon",
        WaterShallow = C("#6B7A84"), WaterDeep = C("#4A5A66"), Murk = C("#1F272E"),
        Sand = C("#9C8C6A"), Rock = C("#7A5230"), Coral = C("#B07A6A"), Coral2 = C("#8A4B2E"), Algae = C("#6E7A4A"),
        Butter = C("#D9A441"), Sun = C("#F2C14E"), SunEnergy = 0.7f, Ambient = C("#6A7680"), AmbientEnergy = 0.5f,
        Caustics = 0.3f, Rays = 0.5f, Menace = 0.35f, Saturation = 0.9f, Vignette = 0.5f, Fog = 0.55f, Lantern = 1.35f,
    };

    public static readonly ReefLook Carnival = Shallows with
    {
        Name = "Coral Carnival",
        WaterShallow = C("#5A3E8A"), WaterDeep = C("#3A2A5E"), Murk = C("#1A1230"),
        Sand = C("#6E5A8A"), Rock = C("#4A3A6A"), Coral = C("#B44FD0"), Coral2 = C("#FF4FA3"), Sponge = C("#2FF3E0"), Butter = C("#9B5CFF"),
        Sun = C("#C9A8FF"), SunEnergy = 0.35f, Ambient = C("#5A4A8A"), AmbientEnergy = 0.45f,
        Caustics = 0f, Rays = 0f, Menace = 0.5f, Saturation = 1.15f, Vignette = 0.6f, Fog = 0.6f, Lantern = 1.5f,
    };

    /// <summary>
    /// The deepest two stay readable: no darker than a little past the Coral Carnival (Depth 3). The Trench is about as
    /// light as the Carnival in its own cold blue; the Abyss a shade darker in indigo, its life glowing neon.
    /// </summary>
    public static readonly ReefLook Trench = Shallows with
    {
        Name = "Twilight Trench",
        WaterShallow = C("#304878"), WaterDeep = C("#1F3460"), Murk = C("#0D1830"),
        Sand = C("#4E5A74"), Rock = C("#3A4866"), Coral = C("#4F7099"), Coral2 = C("#3E5C86"), Algae = C("#3A6070"), Sponge = C("#7FE7FF"),
        Sun = C("#7FA8D8"), SunEnergy = 0.32f, Ambient = C("#3A5080"), AmbientEnergy = 0.44f,
        Caustics = 0f, Rays = 0f, Menace = 0.55f, Saturation = 0.85f, Vignette = 0.62f, Fog = 0.62f, Lantern = 1.85f,
    };

    public static readonly ReefLook Abyss = Trench with
    {
        Name = "The Abyss",
        WaterShallow = C("#34407A"), WaterDeep = C("#222A55"), Murk = C("#10132C"),
        Rock = C("#30364F"), Sand = C("#44486A"), Coral = C("#FF3FA4"), Sponge = C("#2FF3E0"), Butter = C("#FFD25E"),
        Sun = C("#8C90D8"), SunEnergy = 0.26f, Ambient = C("#343C70"), AmbientEnergy = 0.4f,
        Menace = 0.6f, Saturation = 0.8f, Vignette = 0.66f, Fog = 0.66f, Lantern = 2f,
    };

    /// <summary>
    /// A cave (a leak cave, docs/ADVENTURE.md §8): no sun reaches it and nothing of its reef glows on its own. Never black:
    /// a cool dim shade lets its rock and what swims in it show, and the rest is her light (the Abyss's lantern), her
    /// shots' and its fluorescent plankton. Its reef's colours are the Abyss's, dulled, so what her light finds there is
    /// stone and dim life, not neon.
    /// </summary>
    public static readonly ReefLook Cave = Abyss with
    {
        Name = "Cave",
        WaterShallow = C("#1C2340"), WaterDeep = C("#151A33"), Murk = C("#0D1124"),
        Rock = C("#3C4054"), Sand = C("#4C4F66"), Coral = C("#7A3A62"), Coral2 = C("#3E4C70"), Algae = C("#2E4A4C"), Sponge = C("#2A6A66"), Butter = C("#8A7440"),
        Sun = C("#000000"), SunEnergy = 0f, Ambient = C("#5A68A0"), AmbientEnergy = 0.6f,
        Caustics = 0f, Rays = 0f, Menace = 0.6f, Saturation = 0.85f, Vignette = 0.35f, Fog = 0.35f, Lantern = 2f,
        FanA = C("#4A3A6A"), FanB = C("#6A3A5A"), SoftCoral = C("#6A4A64"), HardCoral = C("#6E6450"), Grass = C("#2E4A3A"),
        Dark = true,
    };

    public static readonly ReefLook Tank = Shallows with
    {
        Name = "The Tank",
        WaterShallow = C("#F4F8FF"), WaterDeep = C("#DCE6F5"), Murk = C("#9AA8C0"),
        Sand = C("#3A7BFF"), Rock = C("#C8D0E0"), Coral = C("#FF6BCB"), Coral2 = C("#7CFC4D"), Algae = C("#7CFC4D"), Butter = C("#FF4D5E"),
        Sun = C("#F4F8FF"), SunEnergy = 1.4f, Ambient = C("#F4F8FF"), AmbientEnergy = 1.2f,
        Caustics = 0f, Rays = 0f, Menace = 1f, Saturation = 1.2f, Vignette = 0.1f, Fog = 0.05f, Lantern = 0.6f,
    };

    /// <summary>Sets the reef globals the shaders read (colours converted to linear).</summary>
    public void Apply()
    {
        static void V(string name, Color c)
        {
            var l = c.SrgbToLinear();
            RenderingServer.GlobalShaderParameterSet(name, new Vector3(l.R, l.G, l.B));
        }
        V("reef_water_shallow", WaterShallow);
        V("reef_water_deep", WaterDeep);
        V("reef_murk", Murk);
        V("reef_sand", Sand);
        V("reef_rock", Rock);
        V("reef_coral", Coral);
        V("reef_coral2", Coral2);
        V("reef_algae", Algae);
        V("reef_sponge", Sponge);
        V("reef_butter", Butter);
        V("reef_sun", Sun);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_caustics"), Caustics);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_rays"), Rays);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_menace"), Menace);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_saturation"), Saturation);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_vignette"), Vignette);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_fog"), Fog);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_dark"), Dark ? 1f : 0f);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_lantern"), Lantern);
        SetDrop(0f);
    }

    /// <summary>The level's square (metres a side) for the shaders: its rim, and what its sun map covers.</summary>
    public static void SetExtent(int size) =>
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_extent"), new Vector2(size, size));

    /// <summary>
    /// The dive: the world's swim plane is <paramref name="drop"/> metres down (sinking toward the level below), and the
    /// sea surface sinks with it, so the water reads the same from one level to the next.
    /// </summary>
    public void SetDrop(float drop)
    {
        // The sea surface a little above the highest reef tops (they reach about 14 m), for the caustics and the rays.
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("sea_surface_y"), 18f - drop);
    }

    /// <summary>The water around her: ambient light, background, and glow, for this depth.</summary>
    public void ApplyTo(Godot.Environment env)
    {
        env.BackgroundColor = Murk;
        env.AmbientLightColor = Ambient;
        env.AmbientLightEnergy = AmbientEnergy;
        env.FogLightColor = WaterDeep;
        env.FogDensity = 0.0015f + 0.006f * Menace;
        env.AdjustmentEnabled = true;
        env.AdjustmentContrast = Contrast;
        env.AdjustmentSaturation = OctoShoots.Game.Fx.ArtLook.IsDrawn ? 1.12f : 1f;
        env.AdjustmentBrightness = 1f;
    }
}
