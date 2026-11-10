using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The game's art direction (docs/ART-LOOKS.md): the `reef_art` shader global. Look 6, felt tip and highlighter, is the
/// game's look; 0 is the old painted-photographic one, and 1–5 the studies (`--look=N` picks one, for verification).
/// </summary>
public static class ArtLook
{
    /// <summary>The drawn look: felt tip and highlighter (the game's until the Blue Hollow, `--showcase=0`).</summary>
    public const int Drawn = 6;
    /// <summary>The game's look: painted (docs/VISUAL-SHOWCASE.md), set at start by the reef and the title.</summary>
    public const int Painted = 0;

    /// <summary>The look in use.</summary>
    public static int Current { get; private set; } = Drawn;

    public static bool IsDrawn => Current == Drawn;

    public static void Set(int look)
    {
        Current = look;
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_art"), look);
        ApplyGlobals();
    }

    /// <summary>The drawn look's own globals: no caustics (the surface's moving filaments of light) on anything.</summary>
    public static void ApplyGlobals() => RenderingServer.GlobalShaderParameterSet(SN.Of("sun_caustic"), IsDrawn ? 0f : 1f);

    /// <summary>
    /// The drawn look's light: no ambient occlusion (flat fills, not photographic contact shadows), the fluorescent
    /// glow stronger and wider, a little more saturation for the creatures' vivid colours.
    /// </summary>
    public static void Apply(Godot.Environment env)
    {
        ApplyGlobals();
        if (!IsDrawn) return;
        env.SsaoEnabled = false;
        env.GlowIntensity = 0.95f;
        env.GlowBloom = 0.12f;
        env.GlowHdrThreshold = 0.85f;
        env.AdjustmentSaturation = 1.12f;
    }
}
