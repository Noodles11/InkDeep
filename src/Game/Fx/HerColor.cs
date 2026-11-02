using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Clementine's colour, from one place: res://data/clementine_color.cfg (or the F1 debug menu, which can save back to it).
/// Her body (the bell, the tentacles) is the main colour; her light (bubbles, shots, their halos and trails, the
/// active pearl's charge) is derived from it — lighter and a touch warmer, the way gold sits beside tangerine — unless
/// the file sets it too. Her glow in the water and the warm pool on the floor are derived as well.
/// Published as global shader parameters (her_body, her_light, her_warm in project.godot), so every shader follows at
/// once; views that colour lights or nodes themselves read the properties here (and <see cref="Changed"/>).
/// </summary>
public static class HerColor
{
    public const string File = "res://data/clementine_color.cfg";
    const string Section = "clementine";

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
    /// A jelly's look (docs/RPG-PROPOSAL.md §8), applied at once: her bell and arms, her glow, her tips and her gonads'
    /// shape. Load (forced) goes back to Clementine's own.
    /// </summary>
    public static void SetLook(OctoShoots.Core.Saves.JellyLook look)
    {
        if (Color.HtmlIsValid(look.Arms)) Body = Color.FromHtml(look.Arms);
        LightOverride = Color.HtmlIsValid(look.Glow) ? Color.FromHtml(look.Glow) : null;
        Tips = Color.HtmlIsValid(look.Tips) ? Color.FromHtml(look.Tips) : null;
        Gonads = Math.Max(0, Array.IndexOf(OctoShoots.Core.Saves.JellyNames.Symbols, look.Gonads));
        Publish();
    }

    static bool _loaded;

    /// <summary>Reads the file (once, unless forced) and publishes the colours.</summary>
    public static void Load(bool force = false)
    {
        if (_loaded && !force) return;
        _loaded = true;
        Tips = null;
        Gonads = 0;
        var cfg = new ConfigFile();
        if (cfg.Load(File) == Error.Ok)
        {
            string body = (string)cfg.GetValue(Section, "body", DefaultBody.ToHtml(false));
            string light = (string)cfg.GetValue(Section, "light", "");
            if (Color.HtmlIsValid(body)) Body = Color.FromHtml(body);
            LightOverride = Color.HtmlIsValid(light) ? Color.FromHtml(light) : null;
        }
        Publish();
    }

    /// <summary>A new colour (the debug menu): applied at once; null light derives it from the body.</summary>
    public static void Set(Color body, Color? light)
    {
        Body = body;
        LightOverride = light;
        Publish();
    }

    /// <summary>Writes the current colours back to the file, so they stick (works where the project is writable).</summary>
    public static Error Save()
    {
        var cfg = new ConfigFile();
        cfg.Load(File);
        cfg.SetValue(Section, "body", "#" + Body.ToHtml(false).ToUpperInvariant());
        cfg.SetValue(Section, "light", LightOverride is { } l ? "#" + l.ToHtml(false).ToUpperInvariant() : "");
        return cfg.Save(File);
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
