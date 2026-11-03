using System;
using System.Globalization;
using System.Linq;
using Godot;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.Title;

/// <summary>
/// A jelly's look as a form (docs/RPG-PROPOSAL.md §8): the shape of her gonads and her glow, arm and tentacle-tip
/// colours, laid as rows of a two-column grid. The title's jelly card makes a new jelly with it; the bay's mirror changes
/// one's look with it.
/// </summary>
public sealed class LookForm
{
    readonly OptionButton _gonads;
    readonly ColorPickerButton _glow, _arms, _tips;

    /// <summary>The form changed: its look now.</summary>
    public event Action<JellyLook>? Changed;

    /// <param name="label">Each row's label, in the style of what holds the form.</param>
    public LookForm(GridContainer grid, Func<string, Control> label, JellyLook look)
    {
        grid.AddChild(label("Gonads"));
        _gonads = new OptionButton();
        foreach (string symbol in JellyNames.Symbols) _gonads.AddItem(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(symbol));
        _gonads.ItemSelected += _ => Changed?.Invoke(Look);
        grid.AddChild(_gonads);
        grid.AddChild(label("Glow"));
        grid.AddChild(_glow = Picker(look.Glow));
        grid.AddChild(label("Oral arms"));
        grid.AddChild(_arms = Picker(look.Arms));
        grid.AddChild(label("Tentacle tips"));
        grid.AddChild(_tips = Picker(look.Tips));
        foreach (var picker in Pickers) picker.ColorChanged += _ => Changed?.Invoke(Look);
        Set(look, quiet: true);
    }

    ColorPickerButton[] Pickers => new[] { _glow, _arms, _tips };

    /// <summary>The first row, to take the focus.</summary>
    public Control First => _gonads;

    /// <summary>A colour picker's popup is open (its own keys close it).</summary>
    public bool Picking => Pickers.Any(p => p.GetPopup().Visible) || _gonads.GetPopup().Visible;

    /// <summary>The form's look (colours as #RRGGBB, the gonads by name).</summary>
    public JellyLook Look => new()
    {
        Gonads = JellyNames.Symbols[Math.Clamp(_gonads.Selected, 0, JellyNames.Symbols.Length - 1)],
        Glow = Hex(_glow.Color),
        Arms = Hex(_arms.Color),
        Tips = Hex(_tips.Color),
    };

    /// <summary>The form set to a look.</summary>
    public void Set(JellyLook look, bool quiet = false)
    {
        _gonads.Select(Math.Max(0, Array.IndexOf(JellyNames.Symbols, look.Gonads)));
        _glow.Color = Colour(look.Glow, _glow.Color);
        _arms.Color = Colour(look.Arms, _arms.Color);
        _tips.Color = Colour(look.Tips, _tips.Color);
        if (!quiet) Changed?.Invoke(Look);
    }

    static ColorPickerButton Picker(string hex) =>
        new() { Color = Colour(hex, Colors.White), EditAlpha = false, CustomMinimumSize = new Vector2(120f, 34f) };

    public static Color Colour(string hex, Color fallback) => Color.HtmlIsValid(hex) ? Color.FromHtml(hex) : fallback;

    static string Hex(Color c) => "#" + c.ToHtml(false).ToUpperInvariant();
}
