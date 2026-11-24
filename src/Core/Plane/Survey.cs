using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The spectral survey (docs/proposals/LIGHT-WORLD.md §9.4, phase 6): every plant and animal that answers light (§6–§7)
/// has an entry that fills in as she lights it: by each of her colours (ember, bloom, frost, her plain white), watched in
/// the dark, and lit with a pulse (PlaneCodex.cs finds them; the profile keeps them, Profile.Surveyed, as
/// "flora:NightLily/frost"). A complete entry gives its line of lore in the Sea-pedia; a depth's biome surveyed whole raises
/// that depth's Atlantean keystone piece (a codex entry, "keystone:1".."keystone:4").
/// </summary>
public static class Survey
{
    /// <summary>The facets of an entry, in the Sea-pedia's order.</summary>
    public static readonly string[] Facets = { "ember", "bloom", "frost", "white", "dark", "pulse" };

    /// <summary>The light of hers a facet needs on it, and the share of a band against her plain light's that makes her light that colour.</summary>
    public const float Lit = 0.15f, Tint = 1.15f;

    public static string FacetName(string facet) => facet switch
    {
        "ember" => "Ember light",
        "bloom" => "Bloom light",
        "frost" => "Frost light",
        "white" => "White light",
        "dark" => "In the dark",
        _ => "A pulse",
    };

    public static string Mark(string subject, string facet) => subject + "/" + facet;

    /// <summary>The codex ids the survey covers: the plants and the animals that answer light.</summary>
    public static readonly IReadOnlyList<string> Subjects =
        Codex.All.Select(e => e.Id).Where(id => id.StartsWith("flora:") || id.StartsWith("fauna:")).ToList();

    public static bool IsSubject(string id) => Subjects.Contains(id);

    public static int Done(ICollection<string> surveyed, string subject) => Facets.Count(f => surveyed.Contains(Mark(subject, f)));

    public static bool Complete(ICollection<string> surveyed, string subject) => Done(surveyed, subject) == Facets.Length;

    /// <summary>A biome's subjects: its light room's plant, its loose plants, its animals.</summary>
    public static IEnumerable<string> Of(Biome biome)
    {
        var (room, _, loose) = FloraTuning.For(biome);
        return new[] { room }.Concat(loose).Distinct().Select(k => "flora:" + k)
            .Concat(FaunaTuning.For(biome).Select(f => "fauna:" + f.Kind).Distinct());
    }

    public static bool BiomeComplete(ICollection<string> surveyed, Biome biome) => Of(biome).All(s => Complete(surveyed, s));

    /// <summary>The four keystone pieces (CROSSOVER.md §5.2): one to each depth above Atlantis, raised by surveying either of its biomes whole.</summary>
    public static readonly IReadOnlyList<(int Piece, Biome[] Biomes)> Keystones = new[]
    {
        (1, new[] { Biome.SunlitReef }),
        (2, new[] { Biome.KelpForest, Biome.Shipwreck }),
        (3, new[] { Biome.SunkenRuins }),
        (4, new[] { Biome.AlienWreckage, Biome.DrownedTemple }),
    };

    public static string Keystone(int piece) => "keystone:" + piece;

    /// <summary>The keystone pieces the survey has raised.</summary>
    public static IEnumerable<string> KeystonesRaised(ICollection<string> surveyed) =>
        Keystones.Where(k => k.Biomes.Any(b => BiomeComplete(surveyed, b))).Select(k => Keystone(k.Piece));

    /// <summary>A complete entry's line of lore (its reward).</summary>
    public static string Lore(string subject) => subject switch
    {
        "flora:SunAnemone" => "The Folk planted sun anemones by their doors: a house whose anemones stayed shut had nobody home to bring the light.",
        "flora:ShyAnemone" => "A shy anemone opens only to the dark. The old divers said it is listening for something the light drowns out.",
        "flora:GiantKelp" => "Kelp grows toward green. Before the Leak, whole forests leaned the same way, toward the Palace's lamps.",
        "flora:SunflowerSponge" => "A sponge keeps light the way a jar keeps water. The Ruins' builders lit their halls with them through the long nights.",
        "flora:NightLily" => "A night lily gives its light only to one who comes in the dark. Nobody has ever seen one open in a lit room.",
        "flora:FluorCoral" => "Fluorescent coral turns cold blue into warm green: it was the Atlanteans' first lamp, and their last.",
        "fauna:GlassShrimp" => "Glass shrimp scatter from light and gather in the dark, and always come back to the same stone.",
        "fauna:LanternShoal" => "A lantern shoal follows a slow light. Fishers once led them home through the kelp like a string of lamps.",
        "fauna:Cuttlefish" => "A cuttlefish wears the light that falls on it. In the Leak's dark it wears nothing, and waits.",
        "fauna:MantisShrimp" => "A mantis shrimp sees colours nobody else has names for, and strikes at all of them.",
        "fauna:FireflySquid" => "A firefly squid answers its own beat. Flash it back, and it will show you where it has been.",
        _ => "",
    };
}
