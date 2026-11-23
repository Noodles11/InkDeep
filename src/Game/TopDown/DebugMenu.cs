using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// F1, any time in a run: the debug menu, a drawer sliding in from the right as the pause menu does (DrawerStyle), all of
/// it one scroll (the mouse wheel, the right stick). The game stands still under it. Jump to any level of the seed
/// (cycle, depth, level; a depth ends at its boss level), switch the floor fog off (to see the floor clear), set
/// Clementine's light by hand (its spectrum, power, coherence, width and pulse: PlaneOptics' LightSettings), try her
/// colour (HerColor: live, and savable to its file), and switch any pearl on or off. Everything applies at once.
/// </summary>
public partial class DebugMenu : Control
{
    /// <summary>Go to this level, keeping what she carries; true: arrive at full HP.</summary>
    public event Action<LevelId, bool>? GoTo;
    /// <summary>A pearl switched on (true) or off.</summary>
    public event Action<string, bool>? PearlToggled;
    /// <summary>Every pearl on the plane on (true), or every pearl off.</summary>
    public event Action<bool>? AllPearls;
    public event Action? Closed;
    /// <summary>The floor fog switched on (true) or off.</summary>
    public event Action<bool>? FloorFogToggled;
    /// <summary>The camera-zoom slider moved (for this session only).</summary>
    public event Action<float>? ZoomChanged;
    /// <summary>Her light set by hand (its settings), or given back to her pearls (null).</summary>
    public event Action<LightSettings?>? LightChanged;

    const float Width = 660f, SlideSeconds = OctoShoots.Game.Ui.DrawerStyle.SlideSeconds, ScrollSpeed = 900f;
    PanelContainer _panel = null!;
    ColorRect _shade = null!;
    ScrollContainer _scroll = null!;
    float _open;
    bool _opening;

    /// <summary>Open, or sliding in.</summary>
    public bool IsOpen => _opening;

    PlaneWorld? _world;
    LightSettings? _manual;
    CheckBox _byHand = null!;
    HSlider _red = null!, _green = null!, _blue = null!, _power = null!, _coherence = null!, _width = null!, _pulse = null!;
    ColorRect _swatch = null!;
    Label _readout = null!;
    bool _filling;

    static readonly Color Ink = new(0.82f, 0.9f, 0.92f);
    static readonly Color Soft = new(0.55f, 0.65f, 0.68f);
    static readonly Color Head = new(1f, 0.85f, 0.7f);

    ItemCatalog? _catalog;
    SeedCode _seed;
    SpinBox _cycle = null!, _depth = null!, _level = null!;
    Label _bossNote = null!, _where = null!;
    CheckBox _fullHp = null!, _floorFog = null!;
    VBoxContainer _pearlList = null!;
    readonly Dictionary<string, CheckBox> _pearls = new();
    ColorPickerButton _body = null!, _light = null!;
    CheckBox _autoLight = null!;
    Label _colourNote = null!;

    /// <summary>A few colours to try at a click.</summary>
    static readonly (string Name, string Html)[] Presets =
    {
        ("Tangerine", "#FF8C42"), ("Coral", "#FF6F61"), ("Gold", "#FFC14D"), ("Rose", "#FF6FA8"),
        ("Violet", "#B07CFF"), ("Cyan", "#4FD6FF"), ("Mint", "#5FE3B0"), ("Lime", "#B6F05A"),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        // (The world dimmed behind the drawer as it slides in.)
        _shade = new ColorRect { Color = new Color(0.01f, 0.03f, 0.05f, 0f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f };
        AddChild(_shade);
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0f) };
        _panel.AddThemeStyleboxOverride("panel", OctoShoots.Game.Ui.DrawerStyle.Panel());
        AddChild(_panel);
        var frame = new VBoxContainer();
        frame.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(frame);

        var head = new HBoxContainer();
        frame.AddChild(head);
        var title = OctoShoots.Game.Ui.DrawerStyle.Title("Debug");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = OctoShoots.Game.Ui.DrawerStyle.Button("Close  (F1)");
        close.Pressed += () => Closed?.Invoke();
        head.AddChild(close);
        frame.AddChild(Caption("The game is paused. Debug runs are not saved or counted in your statistics.", 13, Soft));

        // All the rest one scroll (the wheel; the right stick on a controller).
        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        frame.AddChild(_scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 10);
        _scroll.AddChild(box);

        // Level: cycle, depth, level (up to that depth's boss level).
        box.AddChild(new HSeparator());
        box.AddChild(Caption("LEVEL", 13, Head));
        _where = Caption("", 14, Soft);
        box.AddChild(_where);
        var pick = new HBoxContainer();
        pick.AddThemeConstantOverride("separation", 12);
        box.AddChild(pick);
        _cycle = Spin(pick, "Cycle", 1, 9);
        _depth = Spin(pick, "Depth", 1, LevelPlan.Depths);
        _level = Spin(pick, "Level", 1, 5);
        _cycle.ValueChanged += _ => ClampLevel();
        _depth.ValueChanged += _ => ClampLevel();
        _level.ValueChanged += _ => ClampLevel();
        _bossNote = Inline("", 14, Soft);
        pick.AddChild(_bossNote);

        var go = new HBoxContainer();
        go.AddThemeConstantOverride("separation", 16);
        box.AddChild(go);
        var goButton = new Button { Text = "Go to level", CustomMinimumSize = new Vector2(150f, 34f) };
        goButton.Pressed += () => GoTo?.Invoke(Selected, _fullHp.ButtonPressed);
        go.AddChild(goButton);
        var first = new Button { Text = "First level", CustomMinimumSize = new Vector2(120f, 34f) };
        first.Pressed += () => GoTo?.Invoke(LevelId.First, _fullHp.ButtonPressed);
        go.AddChild(first);
        _fullHp = new CheckBox { Text = "Arrive cleansed", ButtonPressed = true };
        _fullHp.AddThemeColorOverride("font_color", Ink);
        go.AddChild(_fullHp);

        // The view: the floor's fog and depth blur, off to see the floor clear.
        box.AddChild(new HSeparator());
        box.AddChild(Caption("VIEW", 13, Head));
        _floorFog = new CheckBox { Text = "Floor fog (the murk and blur below her plane)", ButtonPressed = true };
        _floorFog.AddThemeColorOverride("font_color", Ink);
        _floorFog.Toggled += on => FloorFogToggled?.Invoke(on);
        box.AddChild(_floorFog);
        var zoomRow = new HBoxContainer();
        zoomRow.AddThemeConstantOverride("separation", 16);
        zoomRow.AddChild(Inline("Camera zoom", 13, Ink));
        zoomRow.AddChild(OctoShoots.Game.Title.SettingsCard.ZoomSlider(CameraRig.DefaultZoom, v => ZoomChanged?.Invoke(v), Ink));
        box.AddChild(zoomRow);

        // Clementine's light by hand: each of its physical characteristics a gauge (PlaneOptics' LightSettings).
        BuildLight(box);

        // Clementine's colour: her body, and her light (derived from it unless set).
        box.AddChild(new HSeparator());
        box.AddChild(Caption("CLEMENTINE'S COLOUR", 13, Head));
        var colours = new HBoxContainer();
        colours.AddThemeConstantOverride("separation", 10);
        box.AddChild(colours);
        colours.AddChild(Inline("Body", 15, Ink));
        _body = new ColorPickerButton { CustomMinimumSize = new Vector2(70f, 30f), EditAlpha = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _body.ColorChanged += _ => ApplyColour();
        colours.AddChild(_body);
        colours.AddChild(Inline("   Light", 15, Ink));
        _light = new ColorPickerButton { CustomMinimumSize = new Vector2(70f, 30f), EditAlpha = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _light.ColorChanged += _ =>
        {
            _autoLight.SetPressedNoSignal(false);
            ApplyColour();
        };
        colours.AddChild(_light);
        _autoLight = new CheckBox { Text = "from body" };
        _autoLight.Toggled += _ => ApplyColour();
        colours.AddChild(_autoLight);
        var save = new Button { Text = "Save to file" };
        save.Pressed += () =>
        {
            var err = HerColor.Save();
            _colourNote.Text = err == Error.Ok ? $"Saved to {HerColor.File}" : $"Could not save ({err})";
        };
        colours.AddChild(save);
        var presets = new HFlowContainer();
        presets.AddThemeConstantOverride("h_separation", 6);
        box.AddChild(presets);
        foreach (var (name, html) in Presets)
        {
            var c = Color.FromHtml(html);
            var swatch = new Button { Text = name, CustomMinimumSize = new Vector2(0f, 28f) };
            swatch.AddThemeColorOverride("font_color", c);
            swatch.AddThemeColorOverride("font_hover_color", c.Lightened(0.3f));
            swatch.Pressed += () =>
            {
                _body.Color = c;
                _autoLight.ButtonPressed = true;
                ApplyColour();
            };
            presets.AddChild(swatch);
        }
        _colourNote = Caption($"Applies at once. \"Save to file\" keeps it ({HerColor.File}).", 13, Soft);
        box.AddChild(_colourNote);

        // Pearls: the ones on the plane first, then the rest of the catalog (not ported yet; they may do nothing).
        box.AddChild(new HSeparator());
        var pearlHead = new HBoxContainer();
        pearlHead.AddThemeConstantOverride("separation", 10);
        box.AddChild(pearlHead);
        var pearlTitle = Caption("PEARLS", 13, Head);
        pearlTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pearlHead.AddChild(pearlTitle);
        var all = new Button { Text = "All on the plane" };
        all.Pressed += () => AllPearls?.Invoke(true);
        pearlHead.AddChild(all);
        var none = new Button { Text = "None" };
        none.Pressed += () => AllPearls?.Invoke(false);
        pearlHead.AddChild(none);
        box.AddChild(Caption("Switched pearls apply at once. Holding several active pearls, the last one switched on is used.", 13, Soft));

        _pearlList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pearlList.AddThemeConstantOverride("separation", 4);
        box.AddChild(_pearlList);
    }

    /// <summary>
    /// The light section: "Set by hand", and a gauge for each characteristic of her light (its red, green and blue
    /// bands, its power, coherence, beam width and pulse), its colour as a swatch, and a read-out of what it is in this
    /// water (its wavelength and frequency, reach, punch, halo). Moving a gauge sets it by hand; "From her pearls"
    /// starts the gauges again from what her pearls make of it.
    /// </summary>
    void BuildLight(VBoxContainer box)
    {
        box.AddChild(new HSeparator());
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        box.AddChild(head);
        var t = Caption("CLEMENTINE'S LIGHT", 13, Head);
        t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(t);
        _byHand = new CheckBox { Text = "Set by hand" };
        _byHand.AddThemeColorOverride("font_color", Ink);
        _byHand.Toggled += on =>
        {
            if (_filling) return;
            _manual = on ? Read() : null;
            LightChanged?.Invoke(_manual);
            ShowLight();
        };
        head.AddChild(_byHand);
        var fromPearls = new Button { Text = "From her pearls" };
        fromPearls.Pressed += () =>
        {
            if (_world is null) return;
            Fill(_world.PearlLight());
            if (_manual is not null)
            {
                _manual = Read();
                LightChanged?.Invoke(_manual);
            }
            ShowLight();
        };
        head.AddChild(fromPearls);
        box.AddChild(Caption("Its colour from its three bands' mix (its total kept: light is conserved); its reach, punch and halo follow from it and the water.", 13, Soft));

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        box.AddChild(top);
        _swatch = new ColorRect { CustomMinimumSize = new Vector2(46f, 46f), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        top.AddChild(_swatch);
        _readout = Caption("", 14, Ink);
        _readout.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(_readout);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 4);
        box.AddChild(grid);
        _red = Gauge(grid, "Red  640 nm", 0, LightSettings.BandMost, 0.01, v => $"{v:0.00}", new Color(1f, 0.45f, 0.4f));
        _green = Gauge(grid, "Green  530 nm", 0, LightSettings.BandMost, 0.01, v => $"{v:0.00}", new Color(0.5f, 1f, 0.55f));
        _blue = Gauge(grid, "Blue  465 nm", 0, LightSettings.BandMost, 0.01, v => $"{v:0.00}", new Color(0.5f, 0.7f, 1f));
        _power = Gauge(grid, "Power", LightSettings.PowerLeast, LightSettings.PowerMost, 0.05, v => $"x {v:0.00}", Ink);
        _coherence = Gauge(grid, "Coherence", 0, 1, 0.01, v => $"{v:0.00}", Ink);
        _width = Gauge(grid, "Beam width", LightSettings.WidthLeast, LightSettings.WidthMost, 0.05, v => $"x {v:0.00}", Ink);
        _pulse = Gauge(grid, "Pulse", 0, LightSettings.PulseMost, 0.1, v => v <= 0.001 ? "steady" : $"{v:0.0} Hz", Ink);
    }

    /// <summary>A gauge: its name, a slider, its value; moved, her light is set by hand.</summary>
    HSlider Gauge(GridContainer grid, string name, double min, double max, double step, Func<double, string> show, Color colour)
    {
        var n = Inline(name, 15, colour);
        n.CustomMinimumSize = new Vector2(130f, 0f);
        grid.AddChild(n);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(300f, 24f) };
        grid.AddChild(s);
        var v = Inline(show(min), 15, Ink);
        v.CustomMinimumSize = new Vector2(80f, 0f);
        grid.AddChild(v);
        s.ValueChanged += value =>
        {
            v.Text = show(value);
            if (_filling) return;
            _manual = Read();
            _byHand.SetPressedNoSignal(true);
            LightChanged?.Invoke(_manual);
            ShowLight();
        };
        return s;
    }

    LightSettings Read() => new()
    {
        Spectrum = new Spectrum((float)_red.Value, (float)_green.Value, (float)_blue.Value),
        Power = (float)_power.Value, Coherence = (float)_coherence.Value, Width = (float)_width.Value, PulseHz = (float)_pulse.Value,
    };

    void Fill(LightSettings s)
    {
        _filling = true;
        _red.Value = s.Spectrum.R;
        _green.Value = s.Spectrum.G;
        _blue.Value = s.Spectrum.B;
        _power.Value = s.Power;
        _coherence.Value = s.Coherence;
        _width.Value = s.Width;
        _pulse.Value = s.PulseHz;
        _filling = false;
    }

    /// <summary>The swatch and the read-out: the light the gauges make (or her pearls'), in this water.</summary>
    void ShowLight()
    {
        if (_world is null) return;
        var l = _world.Preview(_manual ?? Read());
        var c = l.Colour;
        _swatch.Color = new Color(c.X, c.Y, c.Z);
        _readout.Text = $"{l.Wavelength:0} nm · {l.Frequency:0} THz{(_manual is null ? "  (her pearls')" : "  (by hand)")}\n"
            + $"In {_world.Water.Name}: reach x {l.Reach:0.00} · punch x {l.Punch:0.00} · halo x {l.Halo:0.00}";
    }

    /// <summary>The pickers into HerColor (the light follows the body while "from body" is on).</summary>
    void ApplyColour()
    {
        HerColor.Set(_body.Color, _autoLight.ButtonPressed ? null : _light.Color);
        if (_autoLight.ButtonPressed) _light.Color = HerColor.Light;
    }

    /// <summary>The pickers show HerColor as it is now.</summary>
    void ShowColour()
    {
        _body.Color = HerColor.Body;
        _light.Color = HerColor.Light;
        _autoLight.SetPressedNoSignal(HerColor.LightOverride is null);
    }

    LevelId Selected => new((int)_cycle.Value, (int)_depth.Value, (int)_level.Value);

    /// <summary>Fills the menu from the run and slides it in.</summary>
    public void Open(PlaneRun run, ItemCatalog? catalog, SeedCode seed, LevelId current, bool floorFog, PlaneWorld world)
    {
        _world = world;
        _manual = world.ManualLight;
        _filling = true;
        _byHand.ButtonPressed = _manual is not null;
        _filling = false;
        Fill(_manual ?? world.PearlLight());
        ShowLight();
        _floorFog.SetPressedNoSignal(floorFog);
        _seed = seed;
        if (!ReferenceEquals(catalog, _catalog) || _pearls.Count == 0)
        {
            _catalog = catalog;
            BuildPearls();
        }
        _where.Text = $"Seed {seed} · now on {current}";
        _cycle.SetValueNoSignal(current.Cycle);
        _depth.SetValueNoSignal(current.Depth);
        _level.SetValueNoSignal(current.Level);
        ClampLevel();
        Refresh(run);
        ShowColour();
        _opening = true;
        Visible = true;
    }

    /// <summary>Slides away.</summary>
    public void Close()
    {
        _opening = false;
        GetViewport()?.GuiReleaseFocus();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _open = Mathf.MoveToward(_open, _opening ? 1f : 0f, dt / SlideSeconds);
        Visible = _opening || _open > 0f;
        var size = GetViewportRect().Size;
        float e = 1f - Mathf.Pow(1f - _open, 3f);
        _panel.Position = new Vector2(size.X - Width * e, 0f);
        _panel.Size = new Vector2(Width, size.Y);
        _shade.Color = new Color(0.01f, 0.03f, 0.05f, 0.45f * e);
        if (!_opening || !InputSetup.UsingPad) return;
        // (The right stick scrolls it.)
        float scroll = Input.GetAxis(InputSetup.AimUp, InputSetup.AimDown);
        if (Mathf.Abs(scroll) > 0.2f) _scroll.ScrollVertical += Mathf.RoundToInt(scroll * ScrollSpeed * dt);
    }

    /// <summary>The pearl boxes follow what she carries.</summary>
    public void Refresh(PlaneRun run)
    {
        foreach (var (id, check) in _pearls) check.SetPressedNoSignal(run.Items.Contains(id));
    }

    /// <summary>A depth's levels run up to its boss level (4 or 5, from the seed).</summary>
    void ClampLevel()
    {
        int boss = LevelPlan.BossLevel(_seed, (int)_cycle.Value, (int)_depth.Value);
        _level.MaxValue = boss;
        if (_level.Value > boss) _level.SetValueNoSignal(boss);
        _bossNote.Text = (int)_level.Value == boss ? $"boss level (of {boss})" : $"boss on level {boss}";
    }

    void BuildPearls()
    {
        foreach (var child in _pearlList.GetChildren()) child.QueueFree();
        _pearls.Clear();
        if (_catalog is null)
        {
            _pearlList.AddChild(Caption("No item catalog loaded.", 14, Soft));
            return;
        }
        var ported = PlaneRun.PortedPearls.Where(_catalog.Contains).Select(id => _catalog[id]).ToList();
        var rest = _catalog.Items.Where(i => !PlaneRun.PortedPearls.Contains(i.Id)).OrderBy(i => i.Name).ToList();
        AddSection("On the plane", ported);
        if (rest.Count > 0) AddSection("Not on the plane yet (may have no effect)", rest);
    }

    void AddSection(string name, List<ItemDef> items)
    {
        _pearlList.AddChild(Caption(name, 14, Head));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 2);
        _pearlList.AddChild(grid);
        foreach (var item in items)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(280f, 0f) };
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new Dot(item.Pearl));
            string id = item.Id;
            var check = new CheckBox { Text = item.Name + (item.Active is not null ? "  (active)" : ""), TooltipText = item.Tagline };
            check.AddThemeColorOverride("font_color", Ink);
            check.Toggled += on => PearlToggled?.Invoke(id, on);
            row.AddChild(check);
            grid.AddChild(row);
            _pearls[id] = check;
        }
    }

    static SpinBox Spin(HBoxContainer row, string name, int min, int max)
    {
        row.AddChild(Inline(name, 15, Ink));
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = min, CustomMinimumSize = new Vector2(80f, 0f) };
        row.AddChild(spin);
        return spin;
    }

    /// <summary>A label in a row: one line, its own width (a wrapping label in a row squeezes to a letter a line).</summary>
    static Label Inline(string text, int size, Color color)
    {
        var label = Caption(text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    static Label Caption(string text, int size, Color color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>The pearl's colours, small.</summary>
    partial class Dot : Control
    {
        readonly PearlLook? _look;

        public Dot(PearlLook? look)
        {
            _look = look;
            CustomMinimumSize = new Vector2(18f, 18f);
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            Color a = _look is null ? Colors.White : new Color(_look.Colors[0]);
            Color b = _look is null ? Colors.LightBlue : new Color(_look.Colors[^1]);
            var c = new Vector2(9f, 9f);
            DrawCircle(c, 8f, new Color(0f, 0f, 0f, 0.6f));
            DrawCircle(c, 7f, b);
            DrawCircle(c - new Vector2(1.5f, 1.5f), 4.5f, a);
        }
    }
}
