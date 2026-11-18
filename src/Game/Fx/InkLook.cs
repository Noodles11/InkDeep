using System;
using System.Linq;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The coloured ink drawing (docs/ART-LOOKS.md, "Coloured ink"), the game's look over the painted reef: ink lines round
/// every edge, hatching in the shade, washes of colour grained on paper (topdown_post's ink_*). Out of her light the
/// drawing withers to faded blue ink on its blue-tinted paper; in her light its colours come back vivid. The cut rock is an
/// engraver's cross-section (topdown_ground's ink_cap). Each layer down is drawn on a deeper blue paper. `--ink=0` takes it
/// off (the painted look as it was).
/// </summary>
public static class InkLook
{
    /// <summary>Whether the reef is drawn in ink.</summary>
    public static readonly bool On = !OS.GetCmdlineUserArgs().Contains("--ink=0");

    /// <summary>The paper of each layer, from the warm-up layer's pale aqua down to the Abyss's near-black navy (sRGB).</summary>
    static readonly Color[] Papers =
    {
        new(0.82f, 0.90f, 0.94f),
        new(0.66f, 0.78f, 0.86f),
        new(0.50f, 0.62f, 0.74f),
        new(0.36f, 0.46f, 0.60f),
        new(0.24f, 0.31f, 0.44f),
        new(0.14f, 0.18f, 0.28f),
    };

    /// <summary>The paper a layer is drawn on (layers past the Abyss's on the Abyss's), as the post pass takes it (linear).</summary>
    public static Vector3 Paper(int layer)
    {
        var c = Papers[Math.Clamp(layer, 0, Papers.Length - 1)].SrgbToLinear();
        return new Vector3(c.R, c.G, c.B);
    }

    /// <summary>How much of its colour a layer's drawing keeps out of any light: the sunlit warm-up layer some, below it none.</summary>
    public static float Awake(int layer) => layer <= 0 ? 0.35f : 0f;

    /// <summary>The ink: a deep blue-black (linear).</summary>
    public static Vector3 Ink
    {
        get
        {
            var c = new Color(0.07f, 0.11f, 0.22f).SrgbToLinear();
            return new Vector3(c.R, c.G, c.B);
        }
    }

    /// <summary>A ground material drawn so: its cut an engraver's cross-section.</summary>
    public static void ApplyGround(ShaderMaterial ground) => ground.SetShaderParameter(SN.Of("ink_cap"), On);
}
