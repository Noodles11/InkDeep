using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A neighbour talking (docs/FOLK.md): a speech bubble low on the screen while Clementine is near her home,
/// her portrait (her drawing, freed: BossIntroArt) beside her name and one line. Clementine never answers in words.
/// Each time she swims up, the next line.
/// </summary>
public partial class FolkTalk : Control
{
    PanelContainer _panel = null!;
    Label _name = null!, _line = null!;
    FolkPortrait _portrait = null!;
    float _shown;
    string? _who;
    readonly System.Collections.Generic.Dictionary<string, int> _visits = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.07f, 0.1f, 0.82f), BorderColor = new Color(1f, 0.82f, 0.55f, 0.55f),
            ContentMarginLeft = 14, ContentMarginRight = 22, ContentMarginTop = 10, ContentMarginBottom = 12,
        };
        box.SetBorderWidthAll(2);
        box.SetCornerRadiusAll(18);
        _panel.AddThemeStyleboxOverride("panel", box);
        AddChild(_panel);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(row);
        _portrait = new FolkPortrait { CustomMinimumSize = new Vector2(120f, 110f), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(_portrait);
        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(text);
        _name = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _name.AddThemeFontSizeOverride("font_size", 20);
        _name.AddThemeColorOverride("font_color", new Color(1f, 0.86f, 0.62f));
        text.AddChild(_name);
        _line = new Label { MouseFilter = MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520f, 0f) };
        _line.AddThemeFontSizeOverride("font_size", 17);
        _line.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 0.96f));
        text.AddChild(_line);
        Modulate = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>A frame: the neighbour she is near (null: none), the bubble fading in and out.</summary>
    public void Sync((Neighbour Folk, BossSpec Spec)? near, bool late, float dt)
    {
        if (near is { } n && n.Folk.Id != _who)
        {
            // She swam up to her: the next of her lines.
            _who = n.Folk.Id;
            int visit = _visits.TryGetValue(_who, out int v) ? v : 0;
            _visits[_who] = visit + 1;
            _name.Text = n.Folk.Name;
            _line.Text = Folk.Line(n.Folk, visit, late);
            _portrait.Spec = n.Spec;
        }
        else if (near is null) _who = null;
        _shown = Mathf.MoveToward(_shown, near is null ? 0f : 1f, dt / 0.35f);
        Modulate = new Color(1f, 1f, 1f, _shown);
        Visible = _shown > 0.001f;
        var view = Size;
        var size = _panel.GetCombinedMinimumSize();
        _panel.Size = size;
        _panel.Position = new Vector2((view.X - size.X) * 0.5f, view.Y - size.Y - 140f + 12f * (1f - _shown));
    }
}
