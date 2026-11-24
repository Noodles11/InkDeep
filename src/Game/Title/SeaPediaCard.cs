using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;

using OctoShoots.Game.Ui;

namespace OctoShoots.Game.Title;

/// <summary>
/// The glossary (proposal §4.4): the Pearls tab and a tab for each of the codex's sections (Codex.cs: places,
/// creatures, bosses, reef life, plants, waters and stones, biomes), each a grid of tiles with a detail pane for the
/// selected one: what it is, what it does, and (a pearl, a creature with one) its record. A codex entry stays locked
/// ("?") until she first finds it in play (Profile.Discovered).
/// </summary>
public partial class SeaPediaCard : PanelContainer
{
    readonly Profile _profile;
    readonly ItemCatalog? _catalog;
    readonly Action _close;
    GridContainer _grid = null!;
    VBoxContainer _detail = null!;
    Button _pearlTab = null!;
    readonly Dictionary<CodexSection, Button> _sectionTabs = new();
    readonly List<ShaderMaterial> _spinning = new();
    float _time;

    public SeaPediaCard(Profile profile, ItemCatalog? catalog, Action close)
    {
        _profile = profile;
        _catalog = catalog;
        _close = close;
    }

    /// <summary>
    /// Every pearl in play (the reef's and the crab's), by its kind in her light's grammar (PearlWords: shapes, paths,
    /// colours, rhythms, echoes, glow, stats, actives, the crab's), in the catalogue's order within each.
    /// </summary>
    public static IReadOnlyList<string> PearlsInPlay(ItemCatalog? catalog)
    {
        var all = PlaneRun.PortedPearls.Concat(PlaneRun.CrabPearls).ToList();
        if (catalog is null) return all;
        return all.Select((id, i) => (id, i, kind: catalog.TryGet(id, out var item) ? PearlWords.Kind(item) : PearlKind.Stat))
            .OrderBy(x => x.kind).ThenBy(x => x.i).Select(x => x.id).ToList();
    }

    public static int PearlsFound(Profile p) => PlaneRun.PortedPearls.Concat(PlaneRun.CrabPearls).Count(id => p.SeenItems.Contains(id));
    /// <summary>Codex entries found (all of them, or one section's).</summary>
    public static int EntriesFound(Profile p, CodexSection? section = null) =>
        Codex.All.Count(e => (section is null || e.Section == section) && PlaneProfileRecorder.Knows(p, e.Id));

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", DrawerStyle.Panel());

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);
        var title = DrawerStyle.Title("Sea-pedia");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var close = DrawerStyle.Button(TitleStyle.CloseText, 15);
        close.Pressed += _close;
        head.AddChild(close);

        var tabs = new HFlowContainer();
        tabs.AddThemeConstantOverride("h_separation", 10);
        tabs.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(tabs);
        _pearlTab = DrawerStyle.Button($"Pearls  {PearlsFound(_profile)} / {PlaneRun.PortedPearls.Length + PlaneRun.CrabPearls.Length}", 15);
        _pearlTab.Pressed += () => ShowTab(null);
        tabs.AddChild(_pearlTab);
        foreach (var section in Enum.GetValues<CodexSection>())
        {
            var tab = DrawerStyle.Button($"{Codex.Name(section)}  {EntriesFound(_profile, section)} / {Codex.In(section).Count()}", 15);
            tab.Pressed += () => ShowTab(section);
            tabs.AddChild(tab);
            _sectionTabs[section] = tab;
        }

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 28);
        box.AddChild(body);
        var gridScroll = new ScrollContainer { CustomMinimumSize = new Vector2(556f, 520f), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(gridScroll);
        _grid = new GridContainer { Columns = 5 };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        gridScroll.AddChild(_grid);
        body.AddChild(new VSeparator());
        _detail = new VBoxContainer { CustomMinimumSize = new Vector2(380f, 0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _detail.ClipContents = true;
        _detail.AddThemeConstantOverride("separation", 10);
        body.AddChild(_detail);

        ShowTab(null);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        foreach (var m in _spinning) m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("spin"), _time * 0.6f);
    }

    /// <summary>Opens on the Creatures tab.</summary>
    public void ShowCreatures() => ShowTab(CodexSection.Creatures);

    /// <summary>A tab: the pearls (null), or a codex section.</summary>
    void ShowTab(CodexSection? section)
    {
        bool pearls = section is null;
        Restyle(_pearlTab, pearls);
        foreach (var (s, tab) in _sectionTabs) Restyle(tab, s == section);
        foreach (var child in _grid.GetChildren()) child.QueueFree();
        _spinning.Clear();
        Button? first = null;
        if (pearls)
        {
            foreach (string id in PearlsInPlay(_catalog))
            {
                var tile = PearlTile(id);
                _grid.AddChild(tile);
                first ??= tile;
            }
        }
        else
        {
            foreach (var entry in Codex.In(section!.Value))
            {
                var tile = EntryTile(entry);
                _grid.AddChild(tile);
                first ??= tile;
            }
        }
        first?.CallDeferred(Control.MethodName.GrabFocus);
        first?.EmitSignal(BaseButton.SignalName.Pressed);
    }

    static void Restyle(Button tab, bool active)
    {
        var fill = active ? DrawerStyle.Gold : new Color(1f, 1f, 1f, 0f);
        var box = TitleStyle.Box(fill, DrawerStyle.Gold, 2, 999);
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = box.ContentMarginBottom = 7;
        tab.AddThemeStyleboxOverride("normal", box);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            tab.AddThemeColorOverride(state, active ? DrawerStyle.OnGold : DrawerStyle.Text);
    }

    enum Known { Unknown, Seen, Absorbed }

    Known PearlState(string id) =>
        _profile.Pearls.TryGetValue(id, out var r) && r.Absorbed > 0 ? Known.Absorbed
        : _profile.SeenItems.Contains(id) ? Known.Seen : Known.Unknown;

    /// <summary>A flat button holding a pearl icon (or a creature portrait) and its name.</summary>
    static Button Tile(Control art, string caption, bool known)
    {
        var tile = new Button { CustomMinimumSize = new Vector2(100f, 124f), FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        var normal = TitleStyle.Box(DrawerStyle.Tile, DrawerStyle.TileRim, 2, 14);
        tile.AddThemeStyleboxOverride("normal", normal);
        tile.AddThemeStyleboxOverride("hover", TitleStyle.Box(DrawerStyle.TileHover, DrawerStyle.Gold, 2, 14));
        tile.AddThemeStyleboxOverride("pressed", TitleStyle.Box(DrawerStyle.TileHover, DrawerStyle.Gold, 3, 14));
        tile.AddThemeStyleboxOverride("focus", TitleStyle.Box(new Color(0, 0, 0, 0), DrawerStyle.Gold, 3, 14));
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.AddThemeConstantOverride("separation", 4);
        tile.AddChild(col);
        var holder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        holder.AddChild(art);
        col.AddChild(holder);
        var name = TitleStyle.Text(known ? caption : "?", 13, known ? DrawerStyle.Text : DrawerStyle.Soft, TitleStyle.BodyBold, HorizontalAlignment.Center);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.CustomMinimumSize = new Vector2(92f, 0f);
        col.AddChild(name);
        return tile;
    }

    ColorRect PearlIcon(ItemDef? item, Known state, float size)
    {
        var mat = OctoShoots.Game.Fx.PearlMaterials.Flat(item?.Pearl);
        mat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("state"), state switch { Known.Absorbed => 0, Known.Seen => 1, _ => 2 });
        _spinning.Add(mat);
        return new ColorRect { CustomMinimumSize = new Vector2(size, size), Material = mat, MouseFilter = MouseFilterEnum.Ignore };
    }

    Button PearlTile(string id)
    {
        ItemDef? item = null;
        _catalog?.TryGet(id, out item);
        var state = PearlState(id);
        var tile = Tile(PearlIcon(item, state, 64f), item?.Name ?? id, state != Known.Unknown);
        tile.Pressed += () => ShowPearl(id, item, state);
        tile.FocusEntered += () => ShowPearl(id, item, state);
        return tile;
    }

    /// <summary>A codex entry's art: a drawn portrait where there is one (the Pufferling, The Lid), else its glyph.</summary>
    static Control EntryArt(CodexEntry entry, bool known, float size) =>
        Codex.RecordOf(entry.Id) is { } record
            ? new Portrait(record, known) { CustomMinimumSize = new Vector2(size, size) }
            : new Glyph(entry, known) { CustomMinimumSize = new Vector2(size, size) };

    Button EntryTile(CodexEntry entry)
    {
        bool known = PlaneProfileRecorder.Knows(_profile, entry.Id);
        var tile = Tile(EntryArt(entry, known, 70f), entry.Name, known);
        tile.Pressed += () => ShowEntry(entry, known);
        tile.FocusEntered += () => ShowEntry(entry, known);
        return tile;
    }

    void ClearDetail()
    {
        foreach (var child in _detail.GetChildren()) child.QueueFree();
    }

    void ShowPearl(string id, ItemDef? item, Known state)
    {
        ClearDetail();
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        _detail.AddChild(top);
        top.AddChild(PearlIcon(item, state, 120f));
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        top.AddChild(names);
        if (state == Known.Unknown || item is null)
        {
            names.AddChild(TitleStyle.Text("Not found yet", 28, DrawerStyle.Text, TitleStyle.Display));
            names.AddChild(Wrapped(PlaneRun.CrabPearls.Contains(id) ? "Only the crab trades it, in a Crab Deal." : "Find it in a clam room, a shop, or on a freed boss.", 15, DrawerStyle.Soft, width: 230f));
            return;
        }
        names.AddChild(TitleStyle.Text(item.Name, 30, DrawerStyle.Text, TitleStyle.Display));
        names.AddChild(Wrapped(item.Tagline, 16, DrawerStyle.Soft, width: 230f));
        // Its kind in her light's grammar (PearlWords).
        names.AddChild(Chip(PearlWords.Name(PearlWords.Kind(item)).ToUpperInvariant(), DrawerStyle.Gold, DrawerStyle.OnGold));
        if (state == Known.Seen) names.AddChild(Chip("SEEN, NOT TAKEN", DrawerStyle.TileHover, DrawerStyle.Soft));
        foreach (string line in ItemCaption.Describe(item)) _detail.AddChild(Wrapped("+ " + line, 16, DrawerStyle.Text, TitleStyle.BodyBold));
        _detail.AddChild(Wrapped("Found in: " + (PlaneRun.StartingPearls.Contains(id) ? "every run's start" : string.Join(", ", item.Pools.Select(PearlWords.Pool))), 14, DrawerStyle.Soft));
        _detail.AddChild(new HSeparator());
        var r = _profile.Pearls.GetValueOrDefault(id) ?? new PearlRecord();
        _detail.AddChild(Record(("Absorbed", r.Absorbed.ToString("N0")), ("Runs it was in", r.Runs.ToString("N0")),
            ("Runs lost holding it", r.RunsLost.ToString("N0")), ("Levels cleared with it", r.RoomsCleared.ToString("N0"))));
    }

    void ShowEntry(CodexEntry entry, bool known)
    {
        ClearDetail();
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        _detail.AddChild(top);
        top.AddChild(EntryArt(entry, known, 120f));
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        top.AddChild(names);
        if (!known)
        {
            names.AddChild(TitleStyle.Text("Not found yet", 28, DrawerStyle.Text, TitleStyle.Display));
            names.AddChild(Wrapped(Codex.Hint(entry.Section), 15, DrawerStyle.Soft, width: 230f));
            return;
        }
        names.AddChild(TitleStyle.Text(entry.Name, 30, DrawerStyle.Text, TitleStyle.Display));
        names.AddChild(Chip(Codex.Name(entry.Section).ToUpperInvariant(), DrawerStyle.Gold, DrawerStyle.OnGold));
        _detail.AddChild(Wrapped("\u201c" + entry.Line + "\u201d", 17, DrawerStyle.Soft));
        _detail.AddChild(Wrapped(entry.Notes, 15, DrawerStyle.Text));
        if (Survey.IsSubject(entry.Id))
        {
            // The spectral survey (Survey.cs): its facets lit, and its lore once whole.
            _detail.AddChild(new HSeparator());
            int done = Survey.Done(_profile.Surveyed, entry.Id);
            _detail.AddChild(Wrapped($"Spectral survey  {done} / {Survey.Facets.Length}", 16, DrawerStyle.Text, TitleStyle.BodyBold));
            _detail.AddChild(Wrapped(string.Join("   ", Survey.Facets.Select(f =>
                (_profile.Surveyed.Contains(Survey.Mark(entry.Id, f)) ? "✓ " : "· ") + Survey.FacetName(f))), 14, DrawerStyle.Soft));
            if (done == Survey.Facets.Length) _detail.AddChild(Wrapped(Survey.Lore(entry.Id), 15, DrawerStyle.Gold, TitleStyle.BodyBold));
        }
        if (Codex.RecordOf(entry.Id) is not { } id) return;
        _detail.AddChild(new HSeparator());
        bool boss = entry.Section == CodexSection.Bosses;
        var r = _profile.Creatures.GetValueOrDefault(id) ?? new CreatureRecord();
        var cells = new List<(string, string)>
        {
            (boss ? "Freed" : "Defeated", r.Defeated.ToString("N0")),
            ("Defeated you", r.DefeatedYou.ToString("N0")),
            ("Encounters", r.Encounters.ToString("N0")),
        };
        if (boss) cells.Add(("Best time", r.BestSeconds > 0 ? TitleStyle.Clock(r.BestSeconds) : "\u2014"));
        _detail.AddChild(Record(cells.ToArray()));
    }

    static Label Wrapped(string text, int size, Color color, Font? font = null, float width = 380f)
    {
        var l = TitleStyle.Text(text, size, color, font);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0f);
        return l;
    }

    static Control Chip(string text, Color fill, Color ink)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        var box = TitleStyle.Box(fill, fill, 0, 999);
        box.ContentMarginLeft = box.ContentMarginRight = 10;
        box.ContentMarginTop = box.ContentMarginBottom = 2;
        p.AddThemeStyleboxOverride("panel", box);
        p.AddChild(TitleStyle.Text(text, 12, ink, TitleStyle.BodyBold));
        return p;
    }

    /// <summary>A row of record cells: a big number over a small label.</summary>
    static Control Record(params (string Label, string Value)[] cells)
    {
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 26);
        grid.AddThemeConstantOverride("v_separation", 12);
        foreach (var (label, value) in cells)
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            cell.AddThemeConstantOverride("separation", 0);
            cell.AddChild(TitleStyle.Text(value, 32, DrawerStyle.Text, TitleStyle.Display));
            cell.AddChild(TitleStyle.Text(label, 14, DrawerStyle.Soft));
            grid.AddChild(cell);
        }
        return grid;
    }

    /// <summary>
    /// A codex entry's glyph: a disc in its section's colour with the entry's first letter (a dark disc and "?" while it
    /// is locked).
    /// </summary>
    public partial class Glyph : Control
    {
        readonly CodexEntry _entry;
        readonly bool _known;

        public Glyph(CodexEntry entry, bool known)
        {
            _entry = entry;
            _known = known;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        static Color Of(CodexSection s) => s switch
        {
            CodexSection.Places => new Color(0.95f, 0.8f, 0.45f),
            CodexSection.Creatures => new Color(0.32f, 0.3f, 0.38f),
            CodexSection.Bosses => new Color(0.5f, 0.25f, 0.45f),
            CodexSection.ReefLife => new Color(0.4f, 0.75f, 0.85f),
            CodexSection.Plants => new Color(0.45f, 0.8f, 0.5f),
            CodexSection.Waters => new Color(0.55f, 0.65f, 0.9f),
            CodexSection.Keystones => new Color(0.8f, 0.68f, 0.45f),
            _ => new Color(0.3f, 0.55f, 0.75f),
        };

        public override void _Draw()
        {
            var c = Size * 0.5f;
            float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
            DrawCircle(c, r, _known ? Of(_entry.Section) : new Color(0.2f, 0.22f, 0.26f));
            DrawArc(c, r * 0.92f, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, _known ? 0.35f : 0.12f), 2f, true);
            string text = _known ? _entry.Name[..1].ToUpperInvariant() : "?";
            var font = TitleStyle.Display;
            int size = (int)(r * 1.1f);
            var at = c + new Vector2(-font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X * 0.5f, size * 0.36f);
            DrawString(font, at, text, HorizontalAlignment.Left, -1, size, _known ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.75f, 0.75f, 0.8f));
        }
    }

    /// <summary>A creature's portrait, drawn: the Pufferling as a red, lime-spotted ball of spines, Queen Clam as her crowned shell.</summary>
    public partial class Portrait : Control
    {
        readonly string _id;
        readonly bool _known;

        public Portrait(string id, bool known)
        {
            _id = id;
            _known = known;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            var c = Size * 0.5f;
            float r = Mathf.Min(Size.X, Size.Y) * 0.5f;
            var dark = new Color(0.12f, 0.15f, 0.18f);
            DrawCircle(c, r, _known ? new Color(0.86f, 0.95f, 0.93f) : new Color(0.8f, 0.78f, 0.78f));
            if (_id == PlaneProfileRecorder.MobId)
            {
                // Blown up: a red ball, spines all round, lime spots, a tail fin with its price tag.
                var body = _known ? new Color(0.95f, 0.25f, 0.3f) : dark;
                var at = c + new Vector2(-r * 0.05f, r * 0.04f);
                float ball = r * 0.5f;
                for (int i = 0; i < 18; i++)
                {
                    float a = i * Mathf.Tau / 18f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    DrawLine(at + dir * ball * 0.9f, at + dir * ball * 1.32f, _known ? new Color(0.55f, 0.38f, 0.3f) : dark, r * 0.04f, true);
                }
                var tail = new[] { at + new Vector2(ball * 0.92f, 0f), at + new Vector2(ball * 1.35f, -ball * 0.38f), at + new Vector2(ball * 1.35f, ball * 0.38f) };
                DrawColoredPolygon(tail, _known ? new Color(0.45f, 1f, 0.25f) : dark);
                DrawCircle(at, ball, body);
                if (!_known) return;
                foreach (var (x, y) in new[] { (-0.45f, -0.3f), (0f, -0.55f), (0.4f, -0.25f), (-0.15f, 0.2f), (0.3f, 0.35f), (-0.5f, 0.45f), (0.55f, 0.05f) })
                    DrawCircle(at + new Vector2(x, y) * ball, ball * 0.11f, new Color(0.5f, 1f, 0.25f));
                DrawCircle(at + new Vector2(-ball * 0.45f, -ball * 0.05f), ball * 0.2f, new Color(0.45f, 1f, 0.2f));
                DrawCircle(at + new Vector2(-ball * 0.48f, -ball * 0.05f), ball * 0.11f, new Color(0.05f, 0.04f, 0.04f));
                DrawLine(at + new Vector2(-ball * 0.7f, -ball * 0.36f), at + new Vector2(-ball * 0.25f, -ball * 0.28f), new Color(0.3f, 0.02f, 0.05f), r * 0.045f, true);
                // The price tag on the tail: a cream card with a pink edge.
                var tag = new Rect2(at + new Vector2(ball * 1.2f, ball * 0.35f), new Vector2(r * 0.2f, r * 0.13f));
                DrawRect(tag, new Color(1f, 0.42f, 0.8f));
                DrawRect(tag.Grow(-r * 0.02f), new Color(0.98f, 0.95f, 0.86f));
            }
            else
            {
                // A shell fan, hinge down, ribbed, with a pearl crown on the hinge.
                var shell = _known ? new Color(0.25f, 0.62f, 0.6f) : dark;
                var hinge = c + new Vector2(0f, r * 0.45f);
                var pts = new List<Vector2> { hinge };
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.Pi * (1.08f + 0.84f * i / 16f);
                    float bump = 1f + 0.05f * Mathf.Cos(i * Mathf.Pi);
                    pts.Add(hinge + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.8f * bump);
                }
                DrawColoredPolygon(pts.ToArray(), shell);
                if (!_known) return;
                for (int i = 1; i < 8; i++)
                {
                    float a = Mathf.Pi * (1.08f + 0.84f * i / 8f);
                    DrawLine(hinge, hinge + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 0.78f, new Color(0.13f, 0.4f, 0.44f), 2f, true);
                }
                for (int i = -2; i <= 2; i++)
                    DrawCircle(hinge + new Vector2(i * r * 0.14f, r * 0.06f - (i == 0 ? r * 0.04f : 0f)), r * (i == 0 ? 0.1f : 0.075f), new Color(1f, 0.97f, 0.92f));
                DrawCircle(c + new Vector2(-r * 0.16f, -r * 0.05f), r * 0.07f, Colors.White);
                DrawCircle(c + new Vector2(r * 0.16f, -r * 0.05f), r * 0.07f, Colors.White);
                DrawCircle(c + new Vector2(-r * 0.15f, -r * 0.04f), r * 0.035f, Colors.Black);
                DrawCircle(c + new Vector2(r * 0.17f, -r * 0.04f), r * 0.035f, Colors.Black);
            }
        }
    }
}
