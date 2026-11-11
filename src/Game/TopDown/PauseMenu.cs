using System.Linq;
using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Controls;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The pause menu (Esc or Start; DESIGN-TOPDOWN §9): a drawer that slides in from the right, over the reef, while the camera
/// closes in on Clementine at the left and the sea is held. Four tabs, changed with LB and RB or the mouse: the level-up
/// (the modifiers a skill point can go to, each with her value now, what a point adds and how many it holds), her stats
/// as they stand (PlaneStatSheet: each against her bare base, better in green, worse in red), every pearl she has absorbed
/// with its name, tagline and exact effects (the item caption lines), and the achievements. Under them resume, restart
/// the run (a new random seed, level 1, no pearls, full HP), save and quit. It opens on the level-up
/// while a point waits.
/// </summary>
public partial class PauseMenu : Control
{
    public event Action? ResumePressed;
    public event Action? RestartPressed;
    public event Action? QuitPressed;
    /// <summary>A skill point was put on a modifier.</summary>
    public event Action<Stat>? SkillChosen;
    /// <summary>The open reef, in Lantern Bay: the next pearl into a slot, and a pearl sold at the shell shop.</summary>
    /// <summary>Whether the run can be started again from here (not a jelly's dive).</summary>
    public bool Restartable { get; set; } = true;
    public event Action<string>? PearlSold;
    /// <summary>The open reef's requests: one she carries let go (back to the Tide Fan), and the HUD's ribbons shown or hidden.</summary>
    public event Action<int>? RequestLetGo;
    public event Action<bool>? RibbonsToggled;


    const float Width = 640f, SlideSeconds = OctoShoots.Game.Ui.DrawerStyle.SlideSeconds;
    /// <summary>The right stick scrolls the list (pixels a second, pushed all the way).</summary>
    const float ScrollSpeed = 900f;
    const int LevelTab = 0, StatTab = 1, RequestTab = 4, ChartTab = 5, Tabs = 6;

    static readonly Color Gold = OctoShoots.Game.Ui.DrawerStyle.Gold;
    static readonly Color Soft = OctoShoots.Game.Ui.DrawerStyle.Soft;

    /// <summary>The stat sheet's name for each modifier (its values are read from there).</summary>
    static readonly Dictionary<Stat, string> SheetName = new()
    {
        [Stat.MaxHp] = "Max HP", [Stat.Damage] = "Damage per shot", [Stat.FireRate] = "Beam tick rate", [Stat.ShotSpeed] = "Beam carry",
        [Stat.Range] = "Range", [Stat.ShotSize] = "Shot size", [Stat.Speed] = "Swim speed", [Stat.DashCooldown] = "Dash cooldown", [Stat.Glow] = "Glow",
    };

    PanelContainer _panel = null!;
    ScrollContainer _scroll = null!;
    VBoxContainer _levelUp = null!, _rows = null!, _pearls = null!, _achievements = null!, _requests = null!;
    /// <summary>The dive's chart (docs/LIGHT-AND-GRAPH.md §6.3).</summary>
    ChartView _chart = null!;
    GridContainer _stats = null!;
    readonly Button[] _tabs = new Button[Tabs];
    Label _summary = null!, _hint = null!, _tabLeft = null!, _tabRight = null!;
    Button _resume = null!, _restart = null!;
    int _tab = StatTab, _points;
    float _open;
    bool _opening;

    /// <summary>Open, or sliding in.</summary>
    public bool IsOpen => _opening;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0f) };
        _panel.AddThemeStyleboxOverride("panel", OctoShoots.Game.Ui.DrawerStyle.Panel());
        AddChild(_panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(box);

        var title = new Label { Text = "Paused" };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", Gold);
        box.AddChild(title);

        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeColorOverride("font_color", Soft);
        box.AddChild(_summary);

        // The tabs, a controller's shoulder buttons either side (they take no focus: LB and RB change them).
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        box.AddChild(tabs);
        _tabLeft = TabHint();
        tabs.AddChild(_tabLeft);
        string[] names = { "Level up", "Stats", "Pearls", "Achievements", "Requests", "Chart" };
        for (int i = 0; i < Tabs; i++)
        {
            int tab = i;
            _tabs[i] = new Button { Text = names[i], ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0f, 34f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _tabs[i].Pressed += () => ShowList(tab);
            tabs.AddChild(_tabs[i]);
        }
        _tabRight = TabHint();
        tabs.AddChild(_tabRight);

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        box.AddChild(_scroll);
        var lists = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scroll.AddChild(lists);
        _levelUp = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _levelUp.AddThemeConstantOverride("separation", 10);
        lists.AddChild(_levelUp);
        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hint.AddThemeFontSizeOverride("font_size", 17);
        _hint.AddThemeColorOverride("font_color", Soft);
        _levelUp.AddChild(_hint);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 6);
        _levelUp.AddChild(_rows);
        // Her stats: two columns of name and value.
        _stats = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _stats.AddThemeConstantOverride("h_separation", 18);
        _stats.AddThemeConstantOverride("v_separation", 8);
        lists.AddChild(_stats);
        _pearls = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _pearls.AddThemeConstantOverride("separation", 14);
        lists.AddChild(_pearls);
        _achievements = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _achievements.AddThemeConstantOverride("separation", 14);
        lists.AddChild(_achievements);
        _requests = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
        _requests.AddThemeConstantOverride("separation", 12);
        lists.AddChild(_requests);
        _chart = new ChartView { Visible = false };
        lists.AddChild(_chart);

        box.AddChild(new HSeparator());
        _resume = new Button { CustomMinimumSize = new Vector2(0f, 40f) };
        _resume.Pressed += () => ResumePressed?.Invoke();
        box.AddChild(_resume);
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        box.AddChild(buttons);
        var restart = _restart = new Button { Text = "Restart run", CustomMinimumSize = new Vector2(0f, 40f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        restart.Pressed += () => RestartPressed?.Invoke();
        buttons.AddChild(restart);
        var quit = new Button { Text = "Save & quit to title", CustomMinimumSize = new Vector2(0f, 40f), SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "You'll resume right where you are." };
        quit.Pressed += () => QuitPressed?.Invoke();
        buttons.AddChild(quit);
        Place();
    }

    static Label TabHint()
    {
        var hint = new Label { VerticalAlignment = VerticalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", Gold);
        return hint;
    }

    /// <summary>Verification: opens on a tab.</summary>
    public void ShowTab(int tab) => ShowList(Math.Clamp(tab, 0, Tabs - 1));

    /// <summary>0 the level-up, 1 her stats, 2 the absorbed pearls, 3 the achievements, 4 the requests she carries, 5 the chart.</summary>
    void ShowList(int tab)
    {
        _tab = tab;
        _levelUp.Visible = tab == LevelTab;
        _stats.Visible = tab == StatTab;
        _pearls.Visible = tab == 2;
        _achievements.Visible = tab == 3;
        _requests.Visible = tab == RequestTab;
        _chart.Visible = tab == ChartTab;
        for (int i = 0; i < Tabs; i++) _tabs[i].ButtonPressed = tab == i;
        _scroll.ScrollVertical = 0;
        if (_opening && InputSetup.UsingPad) FocusFirst();
    }

    /// <summary>Takes a list's rows away at once (a row only queued to go could still be given the focus).</summary>
    static void Empty(Node list)
    {
        foreach (var child in list.GetChildren())
        {
            list.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>The level-up tab: the points to spend and every modifier; with no point to spend they only show.</summary>
    void FillSkills(PlaneRun run)
    {
        _points = run.Points;
        _tabs[LevelTab].Text = run.Points > 0 ? $"Level up  {run.Points}" : "Level up";
        _hint.Text = run.Points switch
        {
            0 => $"Level {run.XpLevel}. No skill point to spend: each level brings one.",
            1 => $"Level {run.XpLevel}! Choose a modifier to strengthen: 1 skill point.",
            _ => $"Level {run.XpLevel}! Choose a modifier to strengthen: {run.Points} skill points.",
        };
        Empty(_rows);
        var sheet = PlaneStatSheet.Of(run).ToDictionary(l => l.Name, l => l.Value);
        foreach (var skill in Skills.All(run.Tuning))
        {
            int held = run.Skills.GetValueOrDefault(skill.Stat);
            var button = new Button { CustomMinimumSize = new Vector2(0f, 52f), Disabled = run.Points <= 0, FocusMode = run.Points > 0 ? FocusModeEnum.All : FocusModeEnum.None };
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 14f;
            row.OffsetRight = -14f;
            row.AddThemeConstantOverride("separation", 10);
            button.AddChild(row);
            var name = new Label { Text = skill.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            name.AddThemeFontSizeOverride("font_size", 19);
            row.AddChild(name);
            if (held > 0)
            {
                var dots = new Label { Text = new string('●', Math.Min(held, 8)) + (held > 8 ? $"+{held - 8}" : ""), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                dots.AddThemeFontSizeOverride("font_size", 12);
                dots.AddThemeColorOverride("font_color", Gold);
                row.AddChild(dots);
            }
            var value = new Label { Text = sheet.GetValueOrDefault(SheetName[skill.Stat], ""), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            value.AddThemeFontSizeOverride("font_size", 17);
            value.AddThemeColorOverride("font_color", Soft);
            row.AddChild(value);
            var gain = new Label { Text = skill.Gain, CustomMinimumSize = new Vector2(52f, 0f), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            gain.AddThemeFontSizeOverride("font_size", 17);
            gain.AddThemeColorOverride("font_color", Gold);
            row.AddChild(gain);
            var stat = skill.Stat;
            button.Pressed += () => SkillChosen?.Invoke(stat);
            _rows.AddChild(button);
        }
    }

    /// <summary>A point was spent: the tab and her stats show it, the focus stays on that row (or moves to Resume with none left).</summary>
    public void Refill(PlaneRun run)
    {
        int at = GetViewport().GuiGetFocusOwner() is Control focus && focus.GetParent() == _rows ? focus.GetIndex() : -1;
        FillSkills(run);
        FillStats(run);
        if (at < 0) return;
        if (run.Points > 0 && at < _rows.GetChildCount()) _rows.GetChild<Control>(at).GrabFocus();
        else _resume.GrabFocus();
    }

    /// <summary>Her stats now: each value, and how far it is from her bare base (better green, worse red).</summary>
    void FillStats(PlaneRun run)
    {
        Empty(_stats);
        foreach (var line in PlaneStatSheet.Of(run))
        {
            var name = new Label { Text = line.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            name.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
            _stats.AddChild(name);
            bool better = line.LowerIsBetter ? line.Change < 0f : line.Change > 0f;
            string change = MathF.Abs(line.Change) < 0.005f ? "" : $"  ({(line.Change > 0f ? "+" : "−")}{MathF.Abs(line.Change) * 100f:0}%)";
            // Counts (luck, shots per throw, mending) change by a number, not a share.
            if (line.Name is "Luck" or "Shots per throw" or "Mending" or "Critical hits") change = "";
            var value = new Label { Text = line.Value + change, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            value.AddThemeColorOverride("font_color", MathF.Abs(line.Change) < 0.005f ? new Color(1f, 0.95f, 0.88f) : better ? new Color(0.55f, 0.95f, 0.65f) : new Color(1f, 0.55f, 0.5f));
            _stats.AddChild(value);
        }
    }

    /// <summary>
    /// Every achievement, earned or not: an earned one shows the pearl it unlocked and its line; an unearned one a dark
    /// pearl silhouette, its title and how to earn it.
    /// </summary>
    void FillAchievements(AchievementCatalog? data, ISet<string> earned, ItemCatalog? catalog)
    {
        Empty(_achievements);
        if (data is null) return;
        int got = data.All.Count(a => earned.Contains(a.Id));
        var count = new Label { Text = $"{got} of {data.All.Count} earned" };
        count.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
        _achievements.AddChild(count);
        foreach (var a in data.All)
        {
            bool has = earned.Contains(a.Id);
            ItemDef? pearl = catalog is not null && catalog.TryGet(a.Pearl, out var p) ? p : null;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            row.AddChild(new PearlDot(has ? pearl?.Pearl : null, dark: !has));
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 2);
            row.AddChild(text);
            var title = new Label { Text = a.Title };
            title.AddThemeFontSizeOverride("font_size", 19);
            title.AddThemeColorOverride("font_color", has ? new Color(1f, 0.93f, 0.85f) : new Color(0.62f, 0.68f, 0.7f));
            text.AddChild(title);
            var line = new Label { Text = has ? $"\u201c{a.Line}\u201d" : a.Hint, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            line.AddThemeColorOverride("font_color", has ? new Color(0.82f, 0.95f, 0.88f) : new Color(0.55f, 0.62f, 0.65f));
            text.AddChild(line);
            var unlock = new Label { Text = has && pearl is not null ? $"Unlocked {pearl.Name}" : "Unlocks a pearl" };
            unlock.AddThemeColorOverride("font_color", has ? new Color(0.44f, 0.89f, 0.76f) : new Color(0.45f, 0.52f, 0.55f));
            text.AddChild(unlock);
            _achievements.AddChild(row);
        }
    }

    /// <summary>Fills the menu from the run (and the profile's achievements) and shows it.</summary>
    public void Open(PlaneRun run, ItemCatalog? catalog, string seed, string where, float hp, AchievementCatalog? achievements = null)
    {
        ShowHints();
        FillSkills(run);
        FillAchievements(achievements, run.Unlocked, catalog);
        FillStats(run);
        _summary.Text = $"Seed {seed} · {where} · {Mathf.RoundToInt(Mathf.Clamp(1f - hp / Mathf.Max(run.MaxHp, 1f), 0f, 1f) * 100f)}% corrupted · {run.Items.Count} pearl{(run.Items.Count == 1 ? "" : "s")}";
        FillPearls(run, catalog);
        // A jelly's dive has no restart: it goes on till she dims (docs/DIVE.md).
        _restart.Visible = Restartable;
        _opening = true;
        Visible = true;
        ShowList(run.Points > 0 ? LevelTab : StatTab);
    }

    /// <summary>
    /// The Requests tab (docs/archive/QUESTS-PROPOSAL.md §6): the requests she carries off the Tide Fan, each its poster's line,
    /// what it asks and how far along, what it pays, and Let go; and whether the HUD shows them as ribbons. On a run,
    /// only a line saying there are none.
    /// </summary>
    /// <summary>The Chart tab: the dive's graph round where she is (ChartView); hidden off a dive's graph (the open reef).</summary>
    public void FillChart(OctoShoots.Core.Run.DiveGraph? graph, OctoShoots.Core.Gen.TopDown.LevelId at, IEnumerable<OctoShoots.Core.Gen.TopDown.LevelId> visited, int sight)
    {
        _tabs[ChartTab].Visible = graph is not null;
        if (graph is not null) _chart.Show(graph, at, visited, sight);
    }

    public void FillRequests(IReadOnlyList<FanMenu.Entry> carried, bool ribbons, bool reef)
    {
        Empty(_requests);
        _tabs[RequestTab].Text = carried.Count > 0 ? $"Requests  {carried.Count}" : "Requests";
        if (!reef)
        {
            _requests.AddChild(Note("Requests are tied to the Tide Fan in Lantern Bay, on the open reef."));
            return;
        }
        var toggle = new Button { Text = $"Ribbons on the HUD: {(ribbons ? "On" : "Off")}", CustomMinimumSize = new Vector2(0f, 38f), ToggleMode = true, ButtonPressed = ribbons };
        toggle.Toggled += on =>
        {
            toggle.Text = $"Ribbons on the HUD: {(on ? "On" : "Off")}";
            RibbonsToggled?.Invoke(on);
        };
        _requests.AddChild(toggle);
        if (carried.Count == 0) _requests.AddChild(Note("You carry none. Take a ribbon off the Tide Fan in the bay's commune."));
        for (int i = 0; i < carried.Count; i++)
        {
            var e = carried[i];
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 3);
            var who = new Label { Text = e.Who };
            who.AddThemeColorOverride("font_color", Gold);
            col.AddChild(who);
            var line = new Label { Text = $"“{e.Line}”", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            line.AddThemeColorOverride("font_color", new Color(1f, 0.93f, 0.85f));
            col.AddChild(line);
            col.AddChild(Note(e.Caption));
            if (e.Pearl is { } pearl) col.AddChild(Row(pearl));
            var pays = new Label { Text = $"{e.Shells} silver shells · {e.Xp} experience" };
            pays.AddThemeColorOverride("font_color", Gold);
            col.AddChild(pays);
            int index = i;
            var go = new Button { Text = e.Enabled ? "Done · untie it at the Tide Fan" : "Let go", Disabled = e.Enabled, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            go.Pressed += () => RequestLetGo?.Invoke(index);
            col.AddChild(go);
            _requests.AddChild(col);
            _requests.AddChild(new HSeparator());
        }
    }

    /// <summary>The open reef: whether pearls can be slotted and sold here (in Lantern Bay), and whether the shell shop is open to buy them (its collector, Ottilie, home).</summary>
    public bool Trade { get; set; }
    public bool Sells { get; set; } = true;

    /// <summary>
    /// The pearls tab. On a run: every pearl she has absorbed, newest first. On the open reef: her three active slots (each
    /// with its key), the actives she keeps in her stash, and the passives in her bell; in Lantern Bay a slot can be
    /// changed and a pearl sold to the shell shop for silver shells (an active its worth, a passive half).
    /// </summary>
    public void FillPearls(PlaneRun run, ItemCatalog? catalog)
    {
        Empty(_pearls);
        if (catalog is null) return;
        // Ottilie buys pearls in a haven once she is home (docs/DIVE.md §4); elsewhere they are only listed.
        if (Trade) _pearls.AddChild(Note(Sells ? $"{run.Shells} silver shells. The shell shop buys any pearl: an active for its worth, a passive for half."
            : "The market's cave stands dark: its collector is not home yet."));
        _pearls.AddChild(Heading("In her bell"));
        if (run.Items.Count == 0) _pearls.AddChild(Note("None yet. Shops, secret places and bosses hold them."));
        foreach (string id in Enumerable.Reverse(run.Items).Distinct())
            if (catalog.TryGet(id, out var item)) _pearls.AddChild(Trade ? WithButton(Row(item), SellButton(run, id)) : Row(item));
    }

    Button SellButton(PlaneRun run, string id)
    {
        var sell = new Button { Text = $"Sell · {run.SellPrice(id)}", Disabled = !Trade || !Sells, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        sell.Pressed += () => PearlSold?.Invoke(id);
        return sell;
    }

    static Control WithButton(Control row, Button button)
    {
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        line.AddChild(row);
        line.AddChild(button);
        return line;
    }

    static Label Heading(string text)
    {
        var l = new Label { Text = text.ToUpperInvariant() };
        l.AddThemeFontSizeOverride("font_size", 14);
        l.AddThemeColorOverride("font_color", Gold);
        return l;
    }

    static Label Note(string text)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.72f));
        return l;
    }

    /// <summary>Slides away.</summary>
    public void Close()
    {
        _opening = false;
        GetViewport()?.GuiReleaseFocus();
    }

    /// <summary>Gone at once (a new level, a new run).</summary>
    public void Shut()
    {
        Close();
        _open = 0f;
        Visible = false;
    }

    /// <summary>A controller in hand: the focus on the first modifier while a point waits on the level-up tab, else on Resume, so the D-pad and A work the menu.</summary>
    public void FocusFirst()
    {
        if (_tab == LevelTab && _points > 0 && _rows.GetChildCount() > 0) _rows.GetChild<Control>(0).GrabFocus();
        else _resume.GrabFocus();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _open = Mathf.MoveToward(_open, _opening ? 1f : 0f, dt / SlideSeconds);
        Visible = _opening || _open > 0f;
        Place();
        if (!_opening) return;
        if (Input.IsActionJustPressed(InputSetup.MenuTabLeft)) ShowList((_tab + Tabs - 1) % Tabs);
        else if (Input.IsActionJustPressed(InputSetup.MenuTabRight)) ShowList((_tab + 1) % Tabs);
        if (!InputSetup.UsingPad) return;
        float scroll = Input.GetAxis(InputSetup.AimUp, InputSetup.AimDown);
        if (Mathf.Abs(scroll) > 0.2f) _scroll.ScrollVertical += Mathf.RoundToInt(scroll * ScrollSpeed * dt);
        // On a controller something always holds the focus, or nothing it presses reaches the menu.
        if (GetViewport().GuiGetFocusOwner() is null) FocusFirst();
    }

    void Place()
    {
        var size = GetViewportRect().Size;
        float e = 1f - Mathf.Pow(1f - _open, 3f);
        _panel.Position = new Vector2(size.X - Width * e, 0f);
        _panel.Size = new Vector2(Width, size.Y);
    }

    /// <summary>Resume's hint in the device's own buttons: Esc on the keyboard, the pause and back buttons on a controller.</summary>
    public void ShowHints()
    {
        if (_resume is null) return;
        _resume.Text = InputSetup.UsingPad
            ? $"Resume  ({InputSetup.Hint(InputSetup.Pause, true)} · {InputSetup.Hint(InputSetup.MenuBack, true)})"
            : "Resume  (Esc)";
        _tabLeft.Text = InputSetup.Hint(InputSetup.MenuTabLeft, true);
        _tabRight.Text = InputSetup.Hint(InputSetup.MenuTabRight, true);
        _tabLeft.Visible = _tabRight.Visible = InputSetup.UsingPad;
    }

    /// <summary>A pearl as the menu lists it: the pearl in its colours, its name, its tagline, its effects (the HUD's PearlBanner shows the same).</summary>
    internal static Control Row(ItemDef item)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        row.AddChild(new PearlDot(item.Pearl));
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);

        var name = new Label { Text = item.Name };
        name.AddThemeFontSizeOverride("font_size", 19);
        name.AddThemeColorOverride("font_color", new Color(1f, 0.93f, 0.85f));
        text.AddChild(name);
        if (!string.IsNullOrEmpty(item.Tagline))
        {
            var tagline = new Label { Text = item.Tagline };
            tagline.AddThemeColorOverride("font_color", new Color(0.62f, 0.72f, 0.75f));
            text.AddChild(tagline);
        }
        foreach (string line in ItemCaption.Describe(item))
        {
            var effect = new Label { Text = "• " + line, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            effect.AddThemeColorOverride("font_color", new Color(0.82f, 0.95f, 0.88f));
            text.AddChild(effect);
        }
        return row;
    }

    /// <summary>The pearl in its own colours with its engraving, as in the world.</summary>
    partial class PearlDot : Control
    {
        /// <param name="dark">A pearl not yet unlocked: a dark silhouette.</param>
        public PearlDot(PearlLook? look, bool dark = false)
        {
            CustomMinimumSize = new Vector2(44f, 44f);
            MouseFilter = MouseFilterEnum.Ignore;
            SizeFlagsVertical = SizeFlags.ShrinkBegin;
            var material = OctoShoots.Game.Fx.PearlMaterials.Flat(look);
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("state"), dark ? 2 : 0);
            var face = new ColorRect { Material = material, MouseFilter = MouseFilterEnum.Ignore };
            face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(face);
        }
    }
}
