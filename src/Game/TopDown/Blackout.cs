using Godot;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The dark she swims into through a cave opening and out of on the far side: the screen goes black, and while the place
/// beyond is made her loader (GonadLoader) turns in its middle, nothing else.
/// </summary>
public partial class Blackout : Control
{
    ColorRect _black = null!;
    GonadLoader _loader = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        _black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        _black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_black);
        _loader = new GonadLoader { Centered = true };
        AddChild(_loader);
    }

    /// <summary>How dark the screen is (0 clear, 1 black), and whether the loader shows (only once it is black).</summary>
    public void Set(float dark, bool loading)
    {
        Visible = dark > 0.001f;
        _black.Color = new Color(0f, 0f, 0f, Mathf.Clamp(dark, 0f, 1f));
        float shown = loading ? Mathf.Clamp((dark - 0.9f) * 10f, 0f, 1f) : 0f;
        _loader.Visible = shown > 0f;
        _loader.Modulate = new Color(1f, 1f, 1f, shown * 0.8f);
    }
}
