using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.Fx;

/// <summary>Her light's colours through prism stones (LightHue), as drawn: the stones, the light out of them, the seals' X.</summary>
public static class HueColor
{
    public static Color Of(LightHue hue) => hue switch
    {
        LightHue.Red => new Color(1f, 0.08f, 0.05f),
        LightHue.Green => new Color(0.12f, 1f, 0.15f),
        LightHue.Blue => new Color(0.1f, 0.3f, 1f),
        LightHue.Yellow => new Color(1f, 0.9f, 0.25f),
        LightHue.Magenta => new Color(1f, 0.28f, 0.9f),
        LightHue.Cyan => new Color(0.25f, 0.95f, 1f),
        LightHue.White => new Color(1f, 1f, 1f),
        _ => new Color(1f, 0.82f, 0.45f),
    };
}
