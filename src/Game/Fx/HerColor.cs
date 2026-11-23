using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Clementine's colour, from one place: her light. The player picks no colour; every jelly's colours follow her light's
/// characteristics, as her beam and her shots do (PlaneOptics' spectrum: her colour words, or her light set by hand in
/// the F1 menu). Her light (bubbles, shots, their halos and trails, the active pearl's charge) is the spectrum's
/// colour; her body (the bell, the tentacles) a deeper, richer shade of its hue, the way tangerine sits beside gold;
/// her tentacle tips a paler one; her glow in the water and the warm pool on the floor are derived as well
/// (<see cref="FollowLight"/>). Her plain warm gold light gives the tangerine she always had.
/// Published as global shader parameters (her_body, her_light, her_warm in project.godot), so every shader follows at
/// once; views that colour lights or nodes themselves read the properties here (and <see cref="Changed"/>).
/// </summary>
public static class HerColor
{
    /// <summary>The default: tangerine (THEME-BIBLE §6.1), and gold for her light.</summary>
    public static readonly Color DefaultBody = Color.FromHtml("#FF8C42");

    public static Color Body { get; private set; } = DefaultBody;
    /// <summary>Set in the file or menu; null: derived from the body.</summary>
    public static Color? LightOverride { get; private set; }

    /// <summary>Her light: bubbles, shots, halos, trails.</summary>
    public static Color Light => LightOverride ?? Shift(Body, hue: 0.041f, sat: 0.78f);
    /// <summary>The colour of her glow in the water (the light she casts): from her light when it is set, else her body.</summary>
    public static Color Glow => Shift(LightOverride ?? Body, hue: 0.004f, sat: 0.65f);
    /// <summary>The tint her glow lays on the floor under her: a deeper, richer shade of the same.</summary>
    public static Color Warm => Shift(LightOverride ?? Body, hue: -0.008f, sat: 1.1f);

    public static event Action? Changed;

    /// <summary>A jelly's tentacle tips (null: they twinkle in random hues, as Clementine's), and her gonads' shape (JellyNames.Symbols).</summary>
    public static Color? Tips { get; private set; }
    public static int Gonads { get; private set; }

    /// <summary>
    /// A jelly's look (docs/GAME.md §9), applied at once: only her gonads' shape now (her colours are her light's: a
    /// look's old colours are not worn).
    /// </summary>
    public static void SetLook(OctoShoots.Core.Saves.JellyLook look)
    {
        Gonads = Math.Max(0, Array.IndexOf(OctoShoots.Core.Saves.JellyNames.Symbols, look.Gonads));
        Publish();
    }

    /// <summary>A jelly's colours (the body, her light, the glow she casts, her tips), without wearing them: her plain light's, as every jelly's.</summary>
    public static (Color Body, Color Light, Color Glow, Color Tips) Of(OctoShoots.Core.Saves.JellyLook look)
    {
        var (body, light, tips) = FromLight(PlainLight);
        return (body, light, Shift(light, hue: 0.004f, sat: 0.65f), tips);
    }

    /// <summary>Her plain warm gold light's colour (PlaneOptics' Spectrum.Hers, its strongest band 1).</summary>
    public static readonly Color PlainLight = new(1f, 0.82f, 0.45f);

    /// <summary>A light's colour (its strongest band 1) and the jelly it makes: her body a deeper, richer shade of its hue, her light itself, her tips a paler one.</summary>
    public static (Color Body, Color Light, Color Tips) FromLight(Color light)
    {
        float h = light.H, s = light.S;
        var body = Color.FromHsv(Mathf.PosMod(h - 0.041f, 1f), Mathf.Clamp(s / 0.78f, 0f, 1f), 1f);
        var lit = Color.FromHsv(h, s, Mathf.Max(light.V, 0.9f));
        var tips = Color.FromHsv(h, s * 0.6f, 1f);
        return (body, lit, tips);
    }

    static Color _following = new(-1f, -1f, -1f);

    /// <summary>
    /// Her colours follow her light (each frame in play, PlaneWorld.HerSpectrum's colour): body, light, glow, warm pool and
    /// tips; published only when it has changed enough to see.
    /// </summary>
    public static void FollowLight(Color light)
    {
        if (Mathf.Abs(light.R - _following.R) + Mathf.Abs(light.G - _following.G) + Mathf.Abs(light.B - _following.B) < 0.004f) return;
        _following = light;
        var (body, lit, tips) = FromLight(light);
        Body = body;
        LightOverride = lit;
        Tips = tips;
        Publish();
    }

    static bool _loaded;

    /// <summary>Her plain light's colours (once, unless forced), till play's light takes them over.</summary>
    public static void Load(bool force = false)
    {
        if (_loaded && !force) return;
        _loaded = true;
        Gonads = 0;
        _following = new Color(-1f, -1f, -1f);
        FollowLight(PlainLight);
    }

    static void Publish()
    {
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_body"), AsVector(Body));
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_light"), AsVector(Light));
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_warm"), AsVector(Warm));
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_tips"), AsVector(Tips ?? Colors.White));
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_tips_set"), Tips is null ? 0f : 1f);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_gonads"), Gonads);
        Changed?.Invoke();
    }

    static Vector3 AsVector(Color c) => new(c.R, c.G, c.B);

    /// <summary>The body's hue turned a little, its saturation scaled, at full brightness.</summary>
    static Color Shift(Color c, float hue, float sat) =>
        Color.FromHsv(Mathf.PosMod(c.H + hue, 1f), Mathf.Clamp(c.S * sat, 0f, 1f), Mathf.Max(c.V, 0.9f));
}
