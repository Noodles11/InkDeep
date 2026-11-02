using System;
using System.Globalization;
using System.Linq;
using Godot;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Settings;

namespace OctoShoots.Game.Title;

/// <summary>
/// The jellies of this machine (docs/RPG-PROPOSAL.md §8, §11): pick one to wake in Lantern Bay, or make a new one —
/// her name (unique here), the shape of her gonads, and her glow, arm and tentacle-tip colours, any colour at all. The
/// title frames her beside the card (as the pause menu does in the game) and wears what it shows: the form as it is set,
/// or a saved jelly while she is pointed at.
/// </summary>
public partial class JellyCard : PanelContainer
{
    readonly Action<int> _play;
    readonly Action _close;
    VBoxContainer _box = null!;
    LineEdit _name = null!;
    OptionButton _gonads = null!;
    ColorPickerButton _glow = null!, _arms = null!, _tips = null!;
    Label _problem = null!;

    /// <summary>The look to show on her now (the title applies it live).</summary>
    public Action<JellyLook>? Preview;

    public JellyCard(Action<int> play, Action close)
    {
        _play = play;
        _close = close;
    }

    public override void _Ready()
    {
        var style = TitleStyle.Box(TitleStyle.Pearl, TitleStyle.Coral, 3, 18, 28);
        style.ShadowColor = new Color(0.04f, 0.1f, 0.12f, 0.45f);
        style.ShadowSize = 24;
        style.ShadowOffset = new Vector2(0f, 10f);
        AddThemeStyleboxOverride("panel", style);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(_box);
        Build();
    }

    void Build()
    {
        var save = GameSave.Current;
        var head = new HBoxContainer();
        _box.AddChild(head);
        var title = TitleStyle.Text("Lantern Bay", 40, TitleStyle.Ink, TitleStyle.Display);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = TitleStyle.Pill(TitleStyle.CloseText, primary: false, size: 15);
        close.Pressed += _close;
        head.AddChild(close);

        if (save.Jellies.Count > 0)
        {
            _box.AddChild(Heading("Your jellies"));
            // The last one played first.
            var order = Enumerable.Range(0, save.Jellies.Count).OrderByDescending(i => i == save.Chosen).ThenByDescending(i => save.Jellies[i].Played).ToList();
            foreach (int i in order)
            {
                var j = save.Jellies[i];
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 14);
                var swatch = new ColorRect { Color = Colour(j.Look.Glow, Colors.Gold), CustomMinimumSize = new Vector2(22f, 22f), SizeFlagsVertical = SizeFlags.ShrinkCenter };
                row.AddChild(swatch);
                var play = TitleStyle.Pill(j.Name, size: 18);
                int index = i;
                play.Pressed += () => _play(index);
                play.MouseEntered += () => Preview?.Invoke(j.Look);
                play.FocusEntered += () => Preview?.Invoke(j.Look);
                play.MouseExited += Changed;
                play.FocusExited += Changed;
                row.AddChild(play);
                string sealedCount = j.Sealed.Values.Sum().ToString(CultureInfo.InvariantCulture);
                row.AddChild(TitleStyle.Text($"Level {j.Level} · {j.Shells} shells · {sealedCount} leaks sealed", 16, TitleStyle.InkSoft));
                _box.AddChild(row);
            }
        }

        _box.AddChild(Heading("A new jelly of the bloom"));
        var form = new GridContainer { Columns = 2 };
        form.AddThemeConstantOverride("h_separation", 18);
        form.AddThemeConstantOverride("v_separation", 10);
        _box.AddChild(form);

        form.AddChild(Label("Name"));
        _name = new LineEdit { PlaceholderText = "Her name", MaxLength = JellyNames.MaxLength, CustomMinimumSize = new Vector2(320f, 0f) };
        _name.AddThemeFontSizeOverride("font_size", 18);
        _name.TextChanged += _ => _problem.Text = "";
        form.AddChild(_name);

        form.AddChild(Label("Gonads"));
        _gonads = new OptionButton();
        foreach (string symbol in JellyNames.Symbols) _gonads.AddItem(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(symbol));
        _gonads.ItemSelected += _ => Changed();
        form.AddChild(_gonads);

        var look = new JellyLook();
        form.AddChild(Label("Glow"));
        form.AddChild(_glow = Picker(look.Glow));
        form.AddChild(Label("Oral arms"));
        form.AddChild(_arms = Picker(look.Arms));
        form.AddChild(Label("Tentacle tips"));
        form.AddChild(_tips = Picker(look.Tips));

        var go = new HBoxContainer();
        go.AddThemeConstantOverride("separation", 14);
        var begin = TitleStyle.Pill("Wake in the bay", size: 18);
        begin.Pressed += Make;
        go.AddChild(begin);
        _problem = TitleStyle.Text("", 15, TitleStyle.Coral, TitleStyle.BodyBold);
        go.AddChild(_problem);
        _box.AddChild(go);
        foreach (var picker in new[] { _glow, _arms, _tips }) picker.ColorChanged += _ => Changed();
        Callable.From(Changed).CallDeferred();
    }

    /// <summary>Review: the form set to a look (colours as #RRGGBB, the gonads by name).</summary>
    public void SetForm(JellyLook look)
    {
        _gonads.Select(Math.Max(0, Array.IndexOf(JellyNames.Symbols, look.Gonads)));
        _glow.Color = Colour(look.Glow, _glow.Color);
        _arms.Color = Colour(look.Arms, _arms.Color);
        _tips.Color = Colour(look.Tips, _tips.Color);
        Changed();
    }

    /// <summary>The form's look, shown on her.</summary>
    void Changed() => Preview?.Invoke(FormLook());

    JellyLook FormLook() => new()
    {
        Gonads = JellyNames.Symbols[Math.Clamp(_gonads.Selected, 0, JellyNames.Symbols.Length - 1)],
        Glow = Hex(_glow.Color),
        Arms = Hex(_arms.Color),
        Tips = Hex(_tips.Color),
    };

    /// <summary>Makes the jelly as the form has her, saves her, and wakes her in the bay.</summary>
    void Make()
    {
        var save = GameSave.Current;
        if (JellyNames.Problem(_name.Text, save.Jellies) is { } problem)
        {
            _problem.Text = problem;
            _name.GrabFocus();
            return;
        }
        var jelly = new Jelly
        {
            Name = _name.Text.Trim(),
            Look = FormLook(),
            Seed = SeedCode.NewRandom().ToString(),
            Made = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
        };
        save.Jellies.Add(jelly);
        save.Chosen = save.Jellies.Count - 1;
        GameSave.Write();
        _play(save.Chosen);
    }

    /// <summary>The first thing to take the focus: the name of a new jelly when there are none, else the last played.</summary>
    public void FocusFirst()
    {
        if (GameSave.Current.Jellies.Count == 0) _name.GrabFocus();
        else _box.GetChildren().OfType<HBoxContainer>().Skip(1).FirstOrDefault()?.GetChildren().OfType<Button>().FirstOrDefault()?.GrabFocus();
    }

    static ColorPickerButton Picker(string hex) =>
        new() { Color = Colour(hex, Colors.White), EditAlpha = false, CustomMinimumSize = new Vector2(120f, 34f) };

    static Color Colour(string hex, Color fallback) => Color.HtmlIsValid(hex) ? Color.FromHtml(hex) : fallback;

    static string Hex(Color c) => "#" + c.ToHtml(false).ToUpperInvariant();

    static Label Heading(string text) => TitleStyle.Text(text.ToUpperInvariant(), 13, TitleStyle.Coral, TitleStyle.BodyBold);

    static Label Label(string text) => TitleStyle.Text(text, 17, TitleStyle.Ink, TitleStyle.BodyBold);
}
