using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Ui;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A neighbour's service in the bay (docs/FOLK.md §3), as the Tide Fan's and the mirror's drawers are:
/// at her home it slides in from the right, the camera drawn in and the sea held. Her service's name, a line of what it
/// does and how things stand, and its actions (Fennel's Brood Bed: leave the shells she carries, take them back).
/// </summary>
public partial class FolkMenu : Control
{
    /// <summary>One thing the service does: its button, and whether it can be done now.</summary>
    public readonly record struct Choice(string Text, bool Enabled, Action Do);

    public event Action? Closed;

    const float Width = 520f;
    PanelContainer _panel = null!;
    Label _title = null!, _summary = null!;
    VBoxContainer _actions = null!;
    Button _close = null!;
    float _open;
    bool _opening;

    public bool IsOpen => _opening;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0f) };
        _panel.AddThemeStyleboxOverride("panel", DrawerStyle.Panel());
        AddChild(_panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(box);
        _title = DrawerStyle.Title("");
        box.AddChild(_title);
        _summary = DrawerStyle.Line("", DrawerStyle.Soft, wrap: true);
        box.AddChild(_summary);
        _actions = new VBoxContainer();
        _actions.AddThemeConstantOverride("separation", 10);
        box.AddChild(_actions);
        box.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        box.AddChild(new HSeparator());
        _close = DrawerStyle.Button("");
        _close.Pressed += () => Closed?.Invoke();
        box.AddChild(_close);
        Place();
    }

    /// <summary>Opens on a service: its name, how it stands, what it does, and the way out ("Leave Fennel").</summary>
    public void Open(string title, string summary, IReadOnlyList<Choice> actions, string leave)
    {
        Fill(title, summary, actions);
        _close.Text = InputSetup.UsingPad ? $"{leave}  ({InputSetup.Hint(InputSetup.MenuBack, true)})" : $"{leave}  (Esc)";
        _opening = true;
        Visible = true;
        Callable.From(FocusFirst).CallDeferred();
    }

    /// <summary>Afresh (after one of its actions).</summary>
    public void Fill(string title, string summary, IReadOnlyList<Choice> actions)
    {
        _title.Text = title;
        _summary.Text = summary;
        foreach (var child in _actions.GetChildren())
        {
            _actions.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var a in actions)
        {
            var button = DrawerStyle.Button(a.Text);
            button.Disabled = !a.Enabled;
            var act = a.Do;
            button.Pressed += () => act();
            _actions.AddChild(button);
        }
    }

    void FocusFirst()
    {
        if (!InputSetup.UsingPad) return;
        foreach (var child in _actions.GetChildren())
            if (child is Button { Disabled: false } button)
            {
                button.GrabFocus();
                return;
            }
        _close.GrabFocus();
    }

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
        _open = Mathf.MoveToward(_open, _opening ? 1f : 0f, (float)delta / DrawerStyle.SlideSeconds);
        Visible = _opening || _open > 0f;
        Place();
        if (_opening && InputSetup.UsingPad && GetViewport().GuiGetFocusOwner() is null) FocusFirst();
    }

    void Place()
    {
        var size = GetViewportRect().Size;
        float e = 1f - Mathf.Pow(1f - _open, 3f);
        _panel.Position = new Vector2(size.X - Width * e, 0f);
        _panel.Size = new Vector2(Width, size.Y);
    }
}
