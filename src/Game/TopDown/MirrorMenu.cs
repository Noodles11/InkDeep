using System;
using Godot;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Title;
using OctoShoots.Game.Ui;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's mirror (docs/GAME.md §9): over its pool, a drawer slides in from the right as the pause menu's
/// does, the camera closing in on her at the left and the sea held. In it her look as a form (LookForm): her gonads'
/// shape and her glow, arm and tip colours, shown on her as they change. Her name stays as it is. Keep this look keeps
/// it on her jelly; As she was (or Esc, or B) puts back the look she came with.
/// </summary>
public partial class MirrorMenu : Control
{
    /// <summary>The look shown on her as it changes, the look kept, and the look she came with put back.</summary>
    public event Action<JellyLook>? Preview, Kept;
    public event Action? Reverted;

    const float Width = 520f, SlideSeconds = DrawerStyle.SlideSeconds;
    static readonly Color Gold = DrawerStyle.Gold;
    static readonly Color Soft = DrawerStyle.Soft;

    PanelContainer _panel = null!;
    Label _summary = null!;
    Button _keep = null!, _back = null!;
    LookForm _form = null!;
    float _open;
    bool _opening;

    /// <summary>Open, or sliding in.</summary>
    public bool IsOpen => _opening;

    /// <summary>
    /// A picker's own popup is open, or was a moment ago: Esc and B close it, not the mirror (the press that closes it is
    /// read after it has gone).
    /// </summary>
    public bool Picking => _opening && (_form.Picking || Time.GetTicksMsec() - _pickedAt < PickGrace);

    const ulong PickGrace = 250;
    ulong _pickedAt;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0f) };
        _panel.AddThemeStyleboxOverride("panel", DrawerStyle.Panel());
        AddChild(_panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        _panel.AddChild(box);

        var title = new Label { Text = "The mirror" };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", Gold);
        box.AddChild(title);
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", Soft);
        box.AddChild(_summary);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 12);
        box.AddChild(grid);
        _form = new LookForm(grid, RowLabel, new JellyLook());
        _form.Changed += look => Preview?.Invoke(look);

        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        box.AddChild(new HSeparator());
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        box.AddChild(buttons);
        _keep = new Button { Text = "Keep this look", CustomMinimumSize = new Vector2(0f, 44f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _keep.Pressed += () => Kept?.Invoke(_form.Look);
        buttons.AddChild(_keep);
        _back = new Button { CustomMinimumSize = new Vector2(0f, 44f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _back.Pressed += () => Reverted?.Invoke();
        buttons.AddChild(_back);
        Place();
    }

    static Control RowLabel(string text)
    {
        var l = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 19);
        l.AddThemeColorOverride("font_color", new Color(1f, 0.93f, 0.85f));
        return l;
    }

    /// <summary>Shows her look as she has it, under her name (which the mirror does not change).</summary>
    public void Open(string name, JellyLook look)
    {
        _summary.Text = $"{name}, as the nacre shows her. All but her name is hers to change.";
        _back.Text = InputSetup.UsingPad ? $"As she was  ({InputSetup.Hint(InputSetup.MenuBack, true)})" : "As she was  (Esc)";
        _form.Set(look, quiet: true);
        _opening = true;
        Visible = true;
        if (InputSetup.UsingPad) _form.First.GrabFocus();
    }

    /// <summary>Verification: the form set to a look, shown on her as if chosen.</summary>
    public void SetForm(JellyLook look) => _form.Set(look);

    /// <summary>Slides away.</summary>
    public void Close()
    {
        _opening = false;
        GetViewport()?.GuiReleaseFocus();
    }

    /// <summary>Gone at once.</summary>
    public void Shut()
    {
        Close();
        _open = 0f;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        _open = Mathf.MoveToward(_open, _opening ? 1f : 0f, (float)delta / SlideSeconds);
        Visible = _opening || _open > 0f;
        Place();
        if (_opening && _form.Picking) _pickedAt = Time.GetTicksMsec();
        // On a controller something always holds the focus, or nothing it presses reaches the drawer.
        if (_opening && InputSetup.UsingPad && GetViewport().GuiGetFocusOwner() is null) _form.First.GrabFocus();
    }

    void Place()
    {
        var size = GetViewportRect().Size;
        float e = 1f - Mathf.Pow(1f - _open, 3f);
        _panel.Position = new Vector2(size.X - Width * e, 0f);
        _panel.Size = new Vector2(Width, size.Y);
    }
}
