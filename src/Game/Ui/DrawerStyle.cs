using Godot;

namespace OctoShoots.Game.Ui;

/// <summary>
/// The look every drawer of the game shares (the pause menu, the bay's mirror, the title's cards): a dark panel the
/// height of the screen, sliding in from the right with a gold rule down its left edge; a gold title, soft summary text,
/// warm row text, and the game's plain buttons.
/// </summary>
public static class DrawerStyle
{
    public static readonly Color Gold = new(1f, 0.82f, 0.45f);
    public static readonly Color Soft = new(0.75f, 0.85f, 0.88f);
    /// <summary>A row's own text (names, labels, values).</summary>
    public static readonly Color Text = new(1f, 0.93f, 0.85f);
    /// <summary>A tile on the panel (a slot, a stat, a portrait's back), its rim, and its hover.</summary>
    public static readonly Color Tile = new(0.07f, 0.13f, 0.16f, 0.95f);
    public static readonly Color TileRim = new(1f, 0.82f, 0.45f, 0.28f);
    public static readonly Color TileHover = new(0.1f, 0.18f, 0.21f, 0.95f);
    public const float SlideSeconds = 0.35f;

    /// <summary>The panel: dark, its left edge ruled in gold, rounded on the left.</summary>
    public static StyleBoxFlat Panel() => new()
    {
        BgColor = new Color(0.03f, 0.07f, 0.09f, 0.94f),
        BorderColor = new Color(Gold, 0.6f),
        BorderWidthLeft = 3,
        CornerRadiusTopLeft = 14, CornerRadiusBottomLeft = 14,
        ContentMarginLeft = 30, ContentMarginRight = 26, ContentMarginTop = 32, ContentMarginBottom = 24,
        ShadowColor = new Color(0f, 0f, 0f, 0.5f), ShadowSize = 18,
    };

    /// <summary>A tile on the panel (rim: gold when it is chosen).</summary>
    public static StyleBoxFlat TileBox(bool chosen = false, int margin = 14) => new()
    {
        BgColor = Tile,
        BorderColor = chosen ? new Color(Gold, 0.8f) : TileRim,
        BorderWidthBottom = chosen ? 2 : 1, BorderWidthTop = chosen ? 2 : 1, BorderWidthLeft = chosen ? 2 : 1, BorderWidthRight = chosen ? 2 : 1,
        CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        AntiAliasing = true,
    };

    public static Label Title(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 36);
        l.AddThemeColorOverride("font_color", Gold);
        return l;
    }

    public static Label Line(string text, Color color, int size = 17, bool wrap = false)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    /// <summary>Dark text on a gold chip (a "best", a "boss", the chosen tab).</summary>
    public static readonly Color OnGold = new(0.12f, 0.08f, 0.04f);

    /// <summary>A text field on the panel: a dark tile, gold when it has the focus.</summary>
    public static LineEdit Field(string placeholder, Font? font = null, int size = 17)
    {
        var f = new LineEdit { PlaceholderText = placeholder };
        if (font is not null) f.AddThemeFontOverride("font", font);
        f.AddThemeFontSizeOverride("font_size", size);
        f.AddThemeColorOverride("font_color", Text);
        f.AddThemeColorOverride("font_placeholder_color", new Color(Soft, 0.55f));
        f.AddThemeColorOverride("caret_color", Gold);
        f.AddThemeColorOverride("selection_color", new Color(Gold, 0.3f));
        var normal = TileBox(margin: 10);
        var focus = TileBox(chosen: true, margin: 10);
        f.AddThemeStyleboxOverride("normal", normal);
        f.AddThemeStyleboxOverride("focus", focus);
        return f;
    }

    /// <summary>A drawer's button: the game's own, as tall as the pause menu's.</summary>
    public static Button Button(string text, int size = 17)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0f, 40f) };
        b.AddThemeFontSizeOverride("font_size", size);
        return b;
    }
}
