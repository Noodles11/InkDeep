using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Her light's colours from her pearls (docs/LIGHT-AND-GRAPH.md §10), as drawn: tints of her light, never neon. Ember a
/// warm red-gold, bloom a soft sea-green, frost a pale blue-white, and their mixes; none (her own light) her usual gold.
/// (HueColor is the prism stones' purer colours.)
/// </summary>
public static class LightColour
{
    public static Color Of(LightHue hue) => hue switch
    {
        LightHue.Red => new Color(1f, 0.42f, 0.16f),
        LightHue.Green => new Color(0.42f, 1f, 0.58f),
        LightHue.Blue => new Color(0.55f, 0.8f, 1f),
        LightHue.Yellow => new Color(1f, 0.86f, 0.32f),
        LightHue.Magenta => new Color(1f, 0.42f, 0.82f),
        LightHue.Cyan => new Color(0.42f, 0.98f, 1f),
        LightHue.White => new Color(1f, 1f, 1f),
        _ => HerColor.Light,
    };

    /// <summary>Her light as drawn in the game's look, tinted by a colour of hers (none: her nearly white showcase light).</summary>
    public static Color Beam(LightHue hue, bool showcase)
    {
        var plain = showcase ? HerColor.Light.Lerp(new Color(0.9f, 0.96f, 1f), 0.65f) : HerColor.Light;
        if (hue == LightHue.None) return plain;
        // (Deepened in the water's haze, so the colour holds against the bright sand.)
        var c = Of(hue);
        float l = Mathf.Max(c.R, Mathf.Max(c.G, c.B));
        return hue == LightHue.White ? plain.Lerp(Colors.White, 0.7f) : new Color(c.R / l, c.G / l, c.B / l).Lerp(new Color(Mathf.Pow(c.R / l, 2.2f), Mathf.Pow(c.G / l, 2.2f), Mathf.Pow(c.B / l, 2.2f)), 0.6f);
    }
}
