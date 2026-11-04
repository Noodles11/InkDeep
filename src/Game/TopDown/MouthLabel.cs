using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A cave mouth's name over it as she nears it (docs/REEF-WORLD.md §3): the cave's name, and under it its level and
/// floors (or that it is sealed), fading in while she is near and out once she swims away. Placed on the screen by the
/// caller each frame, in the window's own pixels.
/// </summary>
public partial class MouthLabel : Control
{
    Label _name = null!, _facts = null!;
    VBoxContainer _box = null!;
    float _shown;
    bool _wanted;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _box.AddThemeConstantOverride("separation", 0);
        AddChild(_box);
        _name = Text(22, new Color(1f, 0.93f, 0.82f));
        _facts = Text(15, new Color(0.78f, 0.86f, 0.9f));
        _box.AddChild(_name);
        _box.AddChild(_facts);
        Modulate = new Color(1f, 1f, 1f, 0f);
    }

    static Label Text(int size, Color color)
    {
        var l = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.05f, 0.9f));
        l.AddThemeConstantOverride("outline_size", 6);
        return l;
    }

    /// <summary>Over this point of the screen (its foot), this cave.</summary>
    public void Show(string name, string facts, Vector2 at)
    {
        _wanted = true;
        _name.Text = name;
        _facts.Text = facts;
        var size = _box.GetCombinedMinimumSize();
        _box.Size = size;
        _box.Position = at - new Vector2(size.X * 0.5f, size.Y);
    }

    public new void Hide() => _wanted = false;

    public override void _Process(double delta)
    {
        _shown = Mathf.MoveToward(_shown, _wanted ? 1f : 0f, (float)delta / 0.25f);
        Modulate = new Color(1f, 1f, 1f, _shown);
    }
}
