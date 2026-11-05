using System;
using System.Globalization;
using System.Linq;
using Godot;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Settings;
using OctoShoots.Game.Ui;

namespace OctoShoots.Game.Title;

/// <summary>
/// The jellies of this machine (docs/RPG-PROPOSAL.md §8, §11), each a save slot, at most JellyNames.MaxJellies, side by
/// side in the title's sea: the title draws each jelly as herself, floating where her slot is (TitleMain.SyncSlots,
/// a BellView wearing her look), and this lays out what goes with them — a card under each with her details, and over an
/// empty slot a round, see-through (+) button. The slot chosen lights up (its jelly shines; the others are switched off
/// like lamps) and its card holds Dive (her dive, continued or begun, docs/DIVE.md) and Delete (asked twice). The (+) makes a new jelly: one
/// swims into that slot wearing the form under her as it is set — her name (unique here), the shape of her gonads, and
/// her glow, arm and tentacle-tip colours — and Dive wakes her. With every slot taken, one must be deleted first.
/// </summary>
public partial class JellyCard : Control
{
    readonly Action<int> _play;
    readonly Action _close;
    const float CardWidth = 300f, JellyBox = 210f;

    sealed class Column
    {
        public Button Hit = null!;
        public PanelContainer Card = null!;
        public VBoxContainer Box = null!;
        public Control? Buttons;
        public Button Dive = null!, Delete = null!;
        public Vector2 At;
    }

    readonly Column[] _columns = new Column[JellyNames.MaxJellies];
    Label _full = null!;
    Button _back = null!;
    VBoxContainer _form = null!;
    LineEdit _name = null!;
    LookForm _look = null!;
    Label _problem = null!;
    /// <summary>The slot whose Delete was pressed once (−1: none): pressed again, she is gone.</summary>
    int _deleting = -1;

    /// <summary>The slot chosen (lit), and the slot a new jelly is being made in (−1: none).</summary>
    public int Selected { get; private set; }
    public int Making { get; private set; } = -1;

    /// <summary>The look of the jelly being made, as the form has it.</summary>
    public JellyLook MakingLook => _look.Look;

    public JellyCard(Action<int> play, Action close)
    {
        _play = play;
        _close = close;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var head = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        head.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        head.Position = new Vector2(-400f, 34f);
        head.Size = new Vector2(800f, 0f);
        AddChild(head);
        var title = DrawerStyle.Title("Your jellies");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        head.AddChild(title);
        var line = DrawerStyle.Line($"Each jelly is a save of her own. Up to {JellyNames.MaxJellies} live on this machine.", DrawerStyle.Soft);
        line.HorizontalAlignment = HorizontalAlignment.Center;
        head.AddChild(line);
        _full = DrawerStyle.Line("Every slot is taken: delete a jelly to make room for a new one.", DrawerStyle.Gold, 15);
        _full.HorizontalAlignment = HorizontalAlignment.Center;
        head.AddChild(_full);

        _back = DrawerStyle.Button(BackText);
        _back.CustomMinimumSize = new Vector2(240f, 40f);
        _back.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
        _back.Position = new Vector2(-120f, -70f);
        _back.Pressed += () =>
        {
            if (!Back()) _close();
        };
        AddChild(_back);

        // The form a new jelly is made with: under her, in her slot's card.
        _form = new VBoxContainer();
        _form.AddThemeConstantOverride("separation", 8);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 8);
        _form.AddChild(grid);
        grid.AddChild(RowLabel("Name"));
        _name = DrawerStyle.Field("Her name", size: 17);
        _name.MaxLength = JellyNames.MaxLength;
        _name.CustomMinimumSize = new Vector2(170f, 0f);
        _name.TextChanged += _ => _problem.Text = "";
        grid.AddChild(_name);
        _look = new LookForm(grid, RowLabel, new JellyLook());
        _problem = DrawerStyle.Line("", DrawerStyle.Gold, 14);
        _form.AddChild(_problem);
        var go = new HBoxContainer();
        go.AddThemeConstantOverride("separation", 8);
        _form.AddChild(go);
        var dive = DrawerStyle.Button("Dive");
        dive.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        dive.Pressed += Make;
        go.AddChild(dive);
        var cancel = DrawerStyle.Button("Back");
        cancel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cancel.Pressed += () => Back();
        go.AddChild(cancel);

        for (int i = 0; i < _columns.Length; i++) _columns[i] = MakeColumn(i);
        int chosen = GameSave.Current.Chosen;
        Selected = chosen >= 0 && chosen < GameSave.Current.Jellies.Count ? chosen : 0;
        Rebuild();
    }

    static string BackText => OctoShoots.Game.Controls.InputSetup.UsingPad
        ? $"Back  ({OctoShoots.Game.Controls.InputSetup.Hint(OctoShoots.Game.Controls.InputSetup.MenuBack, true)})"
        : "Back  (Esc)";

    Column MakeColumn(int slot)
    {
        var c = new Column();
        // Over her: pointing at her (or the (+) of an empty slot) chooses her slot.
        c.Hit = new Button { FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(JellyBox, JellyBox), Size = new Vector2(JellyBox, JellyBox), MouseDefaultCursorShape = CursorShape.PointingHand };
        c.Hit.Pressed += () => HitPressed(slot);
        c.Hit.MouseEntered += () => Select(slot);
        c.Hit.FocusEntered += () => Select(slot);
        AddChild(c.Hit);
        c.Card = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0f), Size = new Vector2(CardWidth, 0f) };
        c.Card.MouseEntered += () => Select(slot);
        c.Card.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) Select(slot);
        };
        AddChild(c.Card);
        c.Box = new VBoxContainer();
        c.Box.AddThemeConstantOverride("separation", 4);
        c.Card.AddChild(c.Box);
        return c;
    }

    /// <summary>Where a slot's jelly is on the page: her box over her, her card under her.</summary>
    /// <summary>
    /// The form is out of the tree whenever no jelly is being made (Rebuild takes it out of its slot's card): freed with
    /// the card, or it and all it holds (its pickers' popups are windows of their own) would be left behind.
    /// </summary>
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete && IsInstanceValid(_form) && _form.GetParent() is null) _form.Free();
    }

    public void Place(int slot, Vector2 at)
    {
        var c = _columns[slot];
        c.At = at;
        c.Hit.Position = at - new Vector2(JellyBox * 0.5f, JellyBox * 0.6f);
        c.Card.Position = new Vector2(at.X - CardWidth * 0.5f, at.Y + JellyBox * 0.45f);
        c.Card.Size = new Vector2(CardWidth, 0f);
    }

    /// <summary>The cards afresh (after a jelly is made or deleted): a jelly's details, the form, or an empty slot.</summary>
    void Rebuild()
    {
        var save = GameSave.Current;
        for (int i = 0; i < _columns.Length; i++)
        {
            var c = _columns[i];
            if (_form.GetParent() == c.Box) c.Box.RemoveChild(_form);
            foreach (var child in c.Box.GetChildren())
            {
                c.Box.RemoveChild(child);
                child.QueueFree();
            }
            bool taken = i < save.Jellies.Count;
            StyleHit(c.Hit, empty: !taken && Making != i);
            if (Making == i)
            {
                c.Box.AddChild(Centred(DrawerStyle.Line("A new jelly of the bloom", DrawerStyle.Gold, 18)));
                c.Box.AddChild(_form);
                c.Buttons = null;
                continue;
            }
            if (!taken)
            {
                c.Box.AddChild(Centred(DrawerStyle.Line("Empty slot", DrawerStyle.Soft, 19)));
                c.Box.AddChild(Centred(DrawerStyle.Line("Press + for a new jelly", DrawerStyle.Soft, 14)));
                c.Buttons = null;
                continue;
            }
            var j = save.Jellies[i];
            c.Box.AddChild(Centred(DrawerStyle.Line(j.Name, DrawerStyle.Text, 22)));
            if (i == save.Chosen) c.Box.AddChild(Centred(DrawerStyle.Line("played last", DrawerStyle.Gold, 13)));
            c.Box.AddChild(Centred(DrawerStyle.Line($"Level {j.Level}", DrawerStyle.Soft, 15)));
            c.Box.AddChild(Centred(DrawerStyle.Line($"{j.Shells} shells · {j.Passives.Count + j.Actives.Count} pearls banked", DrawerStyle.Soft, 15)));
            // Where her dive stands (docs/DIVE.md §1), or that the next one is new.
            c.Box.AddChild(Centred(DrawerStyle.Line(j.Dive is { } dive ? $"Diving · {dive.Where}" : $"Dive {j.Dives + 1} to begin", DrawerStyle.Soft, 15)));
            c.Box.AddChild(Centred(DrawerStyle.Line(Played(j), DrawerStyle.Soft, 13)));
            var buttons = new VBoxContainer();
            buttons.AddThemeConstantOverride("separation", 4);
            c.Box.AddChild(buttons);
            buttons.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 4f) });
            int slot = i;
            c.Dive = DrawerStyle.Button("Dive");
            c.Dive.Pressed += () => _play(slot);
            c.Dive.FocusEntered += () => Select(slot);
            buttons.AddChild(c.Dive);
            c.Delete = DrawerStyle.Button("Delete", 14);
            c.Delete.CustomMinimumSize = new Vector2(0f, 32f);
            c.Delete.Pressed += () => Delete(slot, c.Delete);
            c.Delete.FocusEntered += () => Select(slot);
            var delete = c.Delete;
            delete.FocusExited += () =>
            {
                if (_deleting != slot) return;
                _deleting = -1;
                delete.Text = "Delete";
            };
            buttons.AddChild(c.Delete);
            c.Buttons = buttons;
        }
        _full.Visible = save.Jellies.Count >= JellyNames.MaxJellies && Making < 0;
        ShowSelected();
    }

    /// <summary>A slot's box over her: invisible over a jelly; over an empty slot a round, see-through (+).</summary>
    static void StyleHit(Button hit, bool empty)
    {
        hit.Text = empty ? "+" : "";
        hit.AddThemeFontSizeOverride("font_size", 64);
        hit.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.75f));
        hit.AddThemeColorOverride("font_hover_color", Colors.White);
        hit.AddThemeColorOverride("font_focus_color", Colors.White);
        StyleBox Round(float fill, float rim) => empty
            ? new StyleBoxFlat
            {
                BgColor = new Color(0.75f, 0.92f, 1f, fill), BorderColor = new Color(1f, 1f, 1f, rim),
                BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
                CornerRadiusBottomLeft = 999, CornerRadiusBottomRight = 999, CornerRadiusTopLeft = 999, CornerRadiusTopRight = 999,
                ExpandMarginLeft = -40f, ExpandMarginRight = -40f, ExpandMarginTop = -40f, ExpandMarginBottom = -40f,
                AntiAliasing = true,
            }
            : new StyleBoxEmpty();
        hit.AddThemeStyleboxOverride("normal", Round(0.1f, 0.35f));
        hit.AddThemeStyleboxOverride("hover", Round(0.18f, 0.6f));
        hit.AddThemeStyleboxOverride("pressed", Round(0.25f, 0.7f));
        hit.AddThemeStyleboxOverride("focus", Round(0.18f, 0.85f));
    }

    /// <summary>A slot chosen: its card ruled in gold, its buttons shown (the others' hidden); its jelly lights up.</summary>
    public void Select(int slot)
    {
        if (Making >= 0 && slot != Making) return;
        Selected = slot;
        ShowSelected();
    }

    void ShowSelected()
    {
        for (int i = 0; i < _columns.Length; i++)
        {
            var c = _columns[i];
            c.Card.AddThemeStyleboxOverride("panel", DrawerStyle.TileBox(chosen: i == Selected, margin: 14));
            if (c.Buttons is not null) c.Buttons.Visible = i == Selected;
            c.Card.Modulate = new Color(1f, 1f, 1f, i == Selected || Making == i ? 1f : 0.7f);
        }
    }

    void HitPressed(int slot)
    {
        Select(slot);
        if (Making >= 0) return;
        if (slot < GameSave.Current.Jellies.Count) _columns[slot].Dive.GrabFocus();
        else if (GameSave.Current.Jellies.Count < JellyNames.MaxJellies) StartMaking(slot);
    }

    /// <summary>The (+): a new jelly in the first empty slot, wearing the form as it is set.</summary>
    void StartMaking(int slot)
    {
        Making = Math.Min(slot, GameSave.Current.Jellies.Count);
        Selected = Making;
        _name.Text = "";
        _problem.Text = "";
        _look.Set(new JellyLook(), quiet: true);
        Rebuild();
        Callable.From(() => _name.GrabFocus()).CallDeferred();
    }

    /// <summary>Esc, B or Back: out of the form back to the slots (true), or nothing to do here (false: it closes).</summary>
    public bool Back()
    {
        if (_look.Picking) return true;
        if (Making < 0) return false;
        int was = Making;
        Making = -1;
        Rebuild();
        Callable.From(() => _columns[was].Hit.GrabFocus()).CallDeferred();
        return true;
    }

    /// <summary>Review: a new jelly being made in the first empty slot, the form set to a look.</summary>
    public void SetForm(JellyLook look)
    {
        if (Making < 0 && GameSave.Current.Jellies.Count < JellyNames.MaxJellies) StartMaking(GameSave.Current.Jellies.Count);
        _look.Set(look);
    }

    /// <summary>Pressed once, Delete asks; pressed again, the jelly and all she kept are gone from this machine.</summary>
    void Delete(int index, Button button)
    {
        if (_deleting != index)
        {
            _deleting = index;
            button.Text = $"Sure? Delete {GameSave.Current.Jellies[index].Name}";
            return;
        }
        _deleting = -1;
        var save = GameSave.Current;
        save.Jellies.RemoveAt(index);
        if (save.Chosen == index) save.Chosen = -1;
        else if (save.Chosen > index) save.Chosen--;
        // The last played is the one played most recently of those left.
        if (save.Chosen < 0 && save.Jellies.Count > 0)
            save.Chosen = Enumerable.Range(0, save.Jellies.Count).OrderByDescending(i => save.Jellies[i].Played, StringComparer.Ordinal).First();
        GameSave.Write();
        Selected = Math.Clamp(index, 0, Math.Max(save.Jellies.Count - 1, 0));
        Rebuild();
        Callable.From(FocusFirst).CallDeferred();
    }

    /// <summary>Makes the jelly as the form has her, saves her, and wakes her in the bay.</summary>
    void Make()
    {
        var save = GameSave.Current;
        if (save.Jellies.Count >= JellyNames.MaxJellies)
        {
            _problem.Text = "Every slot is taken";
            return;
        }
        if (JellyNames.Problem(_name.Text, save.Jellies) is { } problem)
        {
            _problem.Text = problem;
            _name.GrabFocus();
            return;
        }
        var jelly = new Jelly
        {
            Name = _name.Text.Trim(),
            Look = _look.Look,
            Seed = SeedCode.NewRandom().ToString(),
            Made = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
        };
        save.Jellies.Add(jelly);
        save.Chosen = save.Jellies.Count - 1;
        Making = -1;
        GameSave.Write();
        _play(save.Chosen);
    }

    /// <summary>The first thing to take the focus: the chosen jelly's Dive, else her slot's box.</summary>
    public void FocusFirst()
    {
        if (Making >= 0)
        {
            _name.GrabFocus();
            return;
        }
        var c = _columns[Selected];
        if (Selected < GameSave.Current.Jellies.Count) c.Dive.GrabFocus();
        else c.Hit.GrabFocus();
    }

    static string Played(Jelly j) =>
        DateTime.TryParse(j.Played, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? $"played {at:d MMM, HH:mm}" : "not played yet";

    /// <summary>A line centred in its card (short enough not to wrap: a wrapping label in a narrow card would stand very tall).</summary>
    static Control Centred(Label l)
    {
        l.HorizontalAlignment = HorizontalAlignment.Center;
        l.ClipText = true;
        return l;
    }

    static Control RowLabel(string text) => DrawerStyle.Line(text, DrawerStyle.Text, 15);
}
