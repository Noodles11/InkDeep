using System;
using System.Globalization;
using Godot;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.Title;

/// <summary>
/// A jelly's look as a form (docs/GAME.md §9): the shape of her gonads, a row of a two-column grid. Her colours are not
/// picked: every jelly's follow her light (HerColor.FollowLight), as her beam and her shots do. The title's jelly card
/// makes a new jelly with it; the bay's mirror changes one's look with it.
/// </summary>
public sealed class LookForm
{
    readonly OptionButton _gonads;

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
        Set(look, quiet: true);
    }

    /// <summary>The first row, to take the focus.</summary>
    public Control First => _gonads;

    /// <summary>Its popup is open (its own keys close it).</summary>
    public bool Picking => _gonads.GetPopup().Visible;

    /// <summary>The form's look (the gonads by name; no colours: they are her light's).</summary>
    public JellyLook Look => new()
    {
        Gonads = JellyNames.Symbols[Math.Clamp(_gonads.Selected, 0, JellyNames.Symbols.Length - 1)],
    };

    /// <summary>The form set to a look.</summary>
    public void Set(JellyLook look, bool quiet = false)
    {
        _gonads.Select(Math.Max(0, Array.IndexOf(JellyNames.Symbols, look.Gonads)));
        if (!quiet) Changed?.Invoke(Look);
    }

    public static Color Colour(string hex, Color fallback) => Color.HtmlIsValid(hex) ? Color.FromHtml(hex) : fallback;
}
