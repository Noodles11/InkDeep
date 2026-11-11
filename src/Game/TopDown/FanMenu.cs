using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Ui;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The Tide Fan's drawer (docs/archive/QUESTS-PROPOSAL.md §2.3), as the pause menu's and the mirror's are: at the fan's foot it
/// slides in from the right, the camera drawn in and the sea held. It lists the ribbons tied on the fan (each its
/// poster's line, what it asks, what it pays, and Take) and the requests she carries (Untie once done).
/// </summary>
public partial class FanMenu : Control
{
    /// <summary>One request as the drawer lists it.</summary>
    public readonly record struct Entry(string Who, string Line, string Caption, ItemDef? Pearl, int Shells, int Xp, Color Kind, string Action, bool Enabled);

    /// <summary>Take the ribbon in a place (false), or untie a carried request (true), by its index.</summary>
    public event Action<bool, int>? Acted;
    public event Action? Closed;

    const float Width = 600f;
    PanelContainer _panel = null!;
    VBoxContainer _list = null!;
    Label _summary = null!;
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
        box.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(box);
        box.AddChild(DrawerStyle.Title("The Tide Fan"));
        _summary = DrawerStyle.Line("", DrawerStyle.Soft, wrap: true);
        box.AddChild(_summary);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        box.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_list);
        box.AddChild(new HSeparator());
        _close = DrawerStyle.Button("");
        _close.Pressed += () => Closed?.Invoke();
        box.AddChild(_close);
        Place();
    }

    /// <summary>Shows the fan's ribbons and her carried requests.</summary>
    public void Open(IReadOnlyList<Entry> fan, IReadOnlyList<Entry> carried, int carry)
    {
        Fill(fan, carried, carry);
        _close.Text = InputSetup.UsingPad ? $"Leave the fan  ({InputSetup.Hint(InputSetup.MenuBack, true)})" : "Leave the fan  (Esc)";
        _opening = true;
        Visible = true;
        Callable.From(FocusFirst).CallDeferred();
    }

    /// <summary>The lists afresh (a ribbon taken or untied).</summary>
    public void Fill(IReadOnlyList<Entry> fan, IReadOnlyList<Entry> carried, int carry)
    {
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        _summary.Text = $"The bay ties its troubles here, a ribbon of kelp for each. You carry {carried.Count} of {carry}.";
        if (carried.Count > 0)
        {
            _list.AddChild(Heading("You carry"));
            for (int i = 0; i < carried.Count; i++) _list.AddChild(Row(carried[i], true, i));
        }
        _list.AddChild(Heading("On the fan"));
        if (fan.Count == 0) _list.AddChild(DrawerStyle.Line("No ribbons now. New troubles drift in when you come home.", DrawerStyle.Soft, 15, wrap: true));
        for (int i = 0; i < fan.Count; i++) _list.AddChild(Row(fan[i], false, i));
    }

    Control Row(Entry e, bool carried, int index)
    {
        var tile = new PanelContainer();
        var box = DrawerStyle.TileBox(chosen: e.Action == "Untie" && e.Enabled, margin: 14);
        box.BorderColor = e.Kind with { A = 0.7f };
        box.BorderWidthLeft = 4;
        tile.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        tile.AddChild(col);
        col.AddChild(DrawerStyle.Line(e.Who, DrawerStyle.Gold, 15));
        col.AddChild(DrawerStyle.Line($"“{e.Line}”", DrawerStyle.Text, 16, wrap: true));
        col.AddChild(DrawerStyle.Line(e.Caption, DrawerStyle.Soft, 15, wrap: true));
        if (e.Pearl is { } pearl) col.AddChild(PauseMenu.Row(pearl));
        col.AddChild(DrawerStyle.Line($"{e.Shells} silver shells · {e.Xp} experience", DrawerStyle.Gold, 15));
        if (e.Action.Length > 0)
        {
            var act = DrawerStyle.Button(e.Action);
            act.Disabled = !e.Enabled;
            act.Pressed += () => Acted?.Invoke(carried, index);
            col.AddChild(act);
        }
        return tile;
    }

    static Label Heading(string text) => DrawerStyle.Line(text.ToUpperInvariant(), DrawerStyle.Gold, 14);

    void FocusFirst()
    {
        if (!InputSetup.UsingPad) return;
        foreach (var b in FindChildren("*", "Button", true, false))
            if (b is Button { Disabled: false } button && button != _close && button.IsVisibleInTree())
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
