using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// docs/PEARLS.md's tables are generated from the catalogue and the game's own captions: this test renders them and
/// fails when the page has fallen behind. Run with OCTO_WRITE_DOCS=1 to write them into the page (between its
/// "generated" markers; the prose round them is the page's own).
/// </summary>
public class PearlsPageTests
{
    const string Begin = "<!-- generated: PearlsPageTests (OCTO_WRITE_DOCS=1 dotnet test regenerates it) -->";
    const string End = "<!-- end of generated -->";

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static string Page()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var page = Path.Combine(dir.FullName, "docs", "PEARLS.md");
            if (File.Exists(page)) return page;
        }
        throw new FileNotFoundException("docs/PEARLS.md");
    }

    static string Stars(int q) => new('★', Math.Max(q, 1));

    static string Cell(string s) => s.Replace("|", "\\|");

    static string Effect(EffectBundle b) => Cell(string.Join("; ", ItemCaption.Describe(b)));

    static string Stacks(ItemDef item) =>
        item.Unique ? "Once" : item.Stack is { } st && ItemCaption.Describe(st).Count > 0 ? $"Yes (each copy: {Effect(st)})" : "Yes";

    static string Found(ItemDef item) =>
        PlaneRun.StartingPearls.Contains(item.Id) ? "every run's start" : string.Join(", ", item.Pools.Select(PearlWords.Pool));

    static string Name(string id) => Catalog.Value.TryGet(id, out var item) ? item.Name : id;

    static readonly (PearlKind Kind, string Heading, string Line)[] Kinds =
    {
        (PearlKind.Shape, "Shapes", "How her light leaves her."),
        (PearlKind.Path, "Paths", "What a ray of hers does where it lands, on rock or on a body, or along the way."),
        (PearlKind.Colour, "Colours", "What her light does to what it lights; two or three mix (docs/LIGHT-AND-GRAPH.md §10)."),
        (PearlKind.Rhythm, "Rhythms", "How her beam changes the longer it is held."),
        (PearlKind.Echo, "Echoes", "What the light a creature had drunk does when her light frees it."),
        (PearlKind.Glow, "Glow", "Her glow's own voice: its reach, and the fireflies round her."),
        (PearlKind.Stat, "Stats and others", "The plain adverbs: her numbers, her pickups, her maps."),
        (PearlKind.Active, "Active pearls", "Used with their button, then they recharge. She carries three."),
    };

    /// <summary>The generated part of docs/PEARLS.md.</summary>
    public static string Render()
    {
        var c = Catalog.Value;
        var inPlay = new HashSet<string>(PlaneRun.PortedPearls);
        var sb = new StringBuilder();
        sb.AppendLine(Begin);
        sb.AppendLine();
        var played = c.Items.Where(i => inPlay.Contains(i.Id)).ToList();
        var designed = c.Items.Where(i => !inPlay.Contains(i.Id) && PearlWords.Kind(i) != PearlKind.Crab).ToList();
        var crab = c.Items.Where(i => PearlWords.Kind(i) == PearlKind.Crab).ToList();
        sb.AppendLine("| Kind | In play | Designed, not yet in play |");
        sb.AppendLine("|---|---|---|");
        foreach (var (kind, heading, _) in Kinds)
            sb.AppendLine($"| {heading} | {played.Count(i => PearlWords.Kind(i) == kind)} | {designed.Count(i => PearlWords.Kind(i) == kind)} |");
        sb.AppendLine($"| The crab's | {crab.Count} | |");
        sb.AppendLine($"| **All** | **{played.Count + crab.Count}** | **{designed.Count}** |");
        foreach (var (kind, heading, line) in Kinds)
        {
            var these = played.Where(i => PearlWords.Kind(i) == kind).ToList();
            if (these.Count == 0) continue;
            sb.AppendLine();
            sb.AppendLine($"### {heading}");
            sb.AppendLine();
            sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine("| Pearl | Tagline | What it does | Stacks | Quality | Found in | Unlocked by |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var i in these)
                sb.AppendLine($"| **{i.Name}** | *{Cell(i.Tagline)}* | {Effect(i)} | {Stacks(i)} | {Stars(i.Quality)} | {Found(i)} | {i.Unlock ?? ""} |");
        }
        sb.AppendLine();
        sb.AppendLine("### The crab's pearls");
        sb.AppendLine();
        sb.AppendLine("Found only in a Crab Deal (docs/ADVENTURE.md §7), at the price of a quarter of her max HP.");
        sb.AppendLine();
        sb.AppendLine("| Pearl | Tagline | What it does |");
        sb.AppendLine("|---|---|---|");
        foreach (var i in crab) sb.AppendLine($"| **{i.Name}** | *{Cell(i.Tagline)}* | {Effect(i)} |");
        sb.AppendLine();
        sb.AppendLine("### Constellations");
        sb.AppendLine();
        sb.AppendLine("What pearls make together, named (each announces itself: \"Synergy!\" with its name, a chime and a ring of her light).");
        sb.AppendLine();
        sb.AppendLine("| Constellation | Needs | What happens |");
        sb.AppendLine("|---|---|---|");
        foreach (var y in c.Synergies)
        {
            string needs = string.Join(" + ", y.Requires.Select(Name));
            if (y.RequiresAny.Count > 0) needs += $" + any {y.AnyCount} of {string.Join(", ", y.RequiresAny.Select(Name))}";
            sb.AppendLine($"| **{y.Name}** | {needs} | {Cell(y.Description)} |");
        }
        sb.AppendLine();
        sb.AppendLine("### Lantern forms");
        sb.AppendLine();
        sb.AppendLine("Three pearls of one tag re-theme her whole light (docs/LIGHT-AND-GRAPH.md §16).");
        sb.AppendLine();
        sb.AppendLine("| Form | Pearls with the tag | What happens | Its numbers |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var t in c.Transformations)
        {
            var with = c.Items.Where(i => i.Tags.Contains(t.Tag) && inPlay.Contains(i.Id)).Select(i => i.Name);
            // (Its numbers beside it: its stats and flags; a form that is only a shape says it all in its line.)
            string numbers = t.Stats.Count + t.Flags.Count + t.Effects.Count > 0 ? Effect(t) : "";
            sb.AppendLine($"| **{t.Name}** | {t.Count} of: {string.Join(", ", with)} | {Cell(t.Description)} | {numbers} |");
        }
        sb.AppendLine();
        sb.AppendLine("### Designed, not yet in play");
        sb.AppendLine();
        sb.AppendLine("In the catalogue (their numbers load and are tested), but not yet offered on the reef.");
        sb.AppendLine();
        sb.AppendLine("| Pearl | Kind | Tagline | What it does | Quality |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var i in designed)
            sb.AppendLine($"| **{i.Name}** | {PearlWords.Name(PearlWords.Kind(i))} | *{Cell(i.Tagline)}* | {Effect(i)} | {Stars(i.Quality)} |");
        sb.AppendLine();
        sb.Append(End);
        return sb.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public void The_pearls_page_is_generated_from_the_catalogue()
    {
        var path = Page();
        var page = File.ReadAllText(path).Replace("\r\n", "\n");
        int a = page.IndexOf(Begin, StringComparison.Ordinal), b = page.IndexOf(End, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a, "docs/PEARLS.md has its generated markers");
        var now = page[..a] + Render() + page[(b + End.Length)..];
        if (Environment.GetEnvironmentVariable("OCTO_WRITE_DOCS") == "1")
        {
            File.WriteAllText(path, now);
            return;
        }
        Assert.Equal(now, page);
    }

    [Fact]
    public void Every_pearl_in_play_has_a_kind_and_the_kinds_read_the_grammar()
    {
        var c = Catalog.Value;
        PearlKind Of(string id) => c.TryGet(id, out var item) ? PearlWords.Kind(item) : throw new KeyNotFoundException(id);
        Assert.Equal(PearlKind.Shape, Of("sunbeam"));
        Assert.Equal(PearlKind.Shape, Of("triple_tentacle"));
        Assert.Equal(PearlKind.Path, Of("mirror_scale"));
        Assert.Equal(PearlKind.Path, Of("bubble_coral"));
        Assert.Equal(PearlKind.Colour, Of("glowing_moss"));
        Assert.Equal(PearlKind.Rhythm, Of("pearl_diver"));
        Assert.Equal(PearlKind.Echo, Of("lamprey_mouth"));
        Assert.Equal(PearlKind.Glow, Of("firefly_squid"));
        Assert.Equal(PearlKind.Stat, Of("shark_tooth"));
        Assert.Equal(PearlKind.Active, Of("glow_burst"));
        Assert.Equal(PearlKind.Crab, Of("black_sun"));
    }
}
