using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>The Sea-pedia's sections beside the pearls (docs/GAME.md: the codex).</summary>
public enum CodexSection { Places, Creatures, Bosses, ReefLife, Plants, Waters, Biomes, Keystones }

/// <summary>A Sea-pedia entry: locked until she first finds it in play (PlaneWorld.Discoveries).</summary>
public sealed record CodexEntry(string Id, CodexSection Section, string Name, string Line, string Notes);

/// <summary>
/// The codex (the Sea-pedia beside the pearls): every place, creature, boss, reef animal, plant, water and stone, and
/// biome she can find, each unlocked the first time she enters it, faces it, sees it or reaches it (PlaneCodex.cs), kept
/// in the profile (Profile.Discovered). Ids are "kind:name" (place:Shop, murk:thrower, boss:TheLid, flora:SunAnemone…).
/// </summary>
public static class Codex
{
    static CodexEntry E(string id, CodexSection s, string name, string line, string notes) => new(id, s, name, line, notes);

    public static readonly IReadOnlyList<CodexEntry> All = new List<CodexEntry>
    {
        // ── Places ──
        E("place:ShellCache", CodexSection.Places, "Shell cache", "Somebody hid these. Somebody forgot.",
            "A heap of shells in the open water. Swim through it to gather them; shells buy pearls in a shop."),
        E("place:Shop", CodexSection.Places, "Shop", "Everything has a price, even down here.",
            "Pearls laid out on stands, each with its price in shells. Touch one you can afford to take it."),
        E("place:Secret", CodexSection.Places, "Secret", "The reef keeps a few rooms to itself.",
            "A room behind a sealed wall. Find the weak rock and break it to get in; what waits inside is worth the trouble."),
        E("place:CurseDen", CodexSection.Places, "Curse den", "Power, at a cost.",
            "A den that offers a gift with a curse attached. Take it only if you can live with the price."),
        E("place:TreasureCave", CodexSection.Places, "Treasure clam", "A clam that has been keeping something.",
            "A cave off the path where a giant clam holds a pearl. Its pearl is yours to take."),
        E("place:CrabDeal", CodexSection.Places, "Crab Deal", "The crab always wins. Usually.",
            "A crab that trades its own pearls, found nowhere else, for a cut of you."),
        E("place:LeakCave", CodexSection.Places, "Leak cave", "Where the ink comes in.",
            "A cave around a leak of the corruption. Seal the leak with your light."),
        E("place:Haven", CodexSection.Places, "Haven", "Nothing harms you here.",
            "Safe water with the folk of the reef: rest, trade, and take their requests."),
        E("place:Shrine", CodexSection.Places, "Light shrine", "An old place of light, still listening.",
            "A shrine that gives one of its finest pearls to one who brings it light."),
        E("place:Relic", CodexSection.Places, "Hidden treasure", "Every depth hides one.",
            "One treasure to each depth, hidden on one of its levels, as far from the way through as the reef allows."),
        E("place:Spring", CodexSection.Places, "Light spring", "Warm water, rising.",
            "A spring of light. Bathing in it heals you, and while you bathe you can grow your light with the energy you gathered."),
        E("place:LightRoom", CodexSection.Places, "Light room", "A ring of plants, waiting.",
            "A ring of one kind of plant round a prize. Wake every plant at once (each answers light its own way) and the room opens."),
        E("place:Vault", CodexSection.Places, "Bubble vault", "Air, and something in it.",
            "A bubble hanging against the rock with shells inside (now and then a pearl). Your light or your shots burst it."),

        // ── Creatures (the corruption's) ──
        E("murk:basic", CodexSection.Creatures, "Murkling", "A wobbling orb of darkness with a stolen ember in it.",
            "It holds still in the dark and looks for light. Seeing one, it swims to the brightest and drinks it; on you, it clings and drains. Your light burns it free."),
        E("murk:thrower", CodexSection.Creatures, "Thrower", "A murkling that learned to share.",
            "A murkling that throws balls of ink at you from range. Its throws drain light; dodge them."),
        E("murk:hunter", CodexSection.Creatures, "Hunter", "It goes after the reef's own.",
            "A murkling that hunts the reef's fish while you are away. What it hits turns into a lesser murkling."),
        E("murk:shoal", CodexSection.Creatures, "Shoal murklings", "Small, quick, many.",
            "A school of small murklings that circles you together. Each is weak; all of them are not."),
        E("murk:prime", CodexSection.Creatures, "Murkling prime", "Five times the murkling.",
            "A rare, huge murkling: every stat scaled up, its throws bigger, its reach longer. Freed, it lets out what it swallowed."),
        E("murk:lesser", CodexSection.Creatures, "Lesser murkling", "A fish, under the ink.",
            "A reef fish turned by a murkling's throw. It chases you a little faster and clings. Freed, it is a fish again."),
        E("trait:ChitinMirror", CodexSection.Creatures, "Chitin-mirror", "Its shell is glass now.",
            "A murkling with a glassy half-shell turning round it. Shots off the shell glance away harmless, and it turns half your beam. Hit its soft side."),
        E("trait:Refractor", CodexSection.Creatures, "Refractor", "Light goes round it.",
            "A murkling in a crystal skin. A shot at its middle bends past it; one at its edge burns harder. Shine at its edge."),
        E("trait:BandWard", CodexSection.Creatures, "Band-ward", "One colour passes it by.",
            "Ink that lets one band of light through untouched: the rim's colour. Only your other colours hurt it."),
        E("trait:LightEater", CodexSection.Creatures, "Light-eater", "It grows on a weak light.",
            "A murkling that feeds on weak light (a far or wide beam) and burns only in a strong one. Focus it."),
        E("mob:blotter", CodexSection.Creatures, "Blotter", "It paints the reef black.",
            "An ink creature that throws bombs that stain the floor where they land."),
        E("mob:blightroot", CodexSection.Creatures, "Blightroot", "Where the murklings come from.",
            "A root of the corruption. Murklings bud round it; burn it out and its reach clears."),
        E("mob:thumpsquirt", CodexSection.Creatures, "Thumpsquirt", "Thump. Squirt.",
            "An ink creature that thumps the floor and squirts ink."),
        E("mob:inkwarden", CodexSection.Creatures, "Inkwarden", "It guards its kin.",
            "While it stands, the corrupted near it take none of your light. Free it first."),
        E("mob:lashjaw", CodexSection.Creatures, "Lashjaw", "All mouth.",
            "An ink creature that lashes out with its jaws. It fights in phases."),
        E("mob:hearthguard", CodexSection.Creatures, "Hearthguard", "A flounder, corrupted, lying in wait.",
            "A flounder turned by the ink. It lies on the shelf and flings ink up at you when you come within reach."),
        E("mob:pufferling", CodexSection.Creatures, "Pufferling", "Round, spotty and easily offended.",
            "Notices you within 10 m and drifts closer. Within 8 m it blows up into a spiny ball and fires needles in a ring, then backs off and shrinks: your window. Its spines sting on touch."),

        // ── Bosses (each holds a corrupted pearl: light it) ──
        E("boss:TheLid", CodexSection.Bosses, "The Lid", "When does it open?",
            "A great bivalve. Its pearl shows only while its lid lifts on its cycle; ring and wall volleys between. Queen Clam is one of these."),
        E("boss:SnappingGate", CodexSection.Bosses, "Snapping Gate", "Get in close, but not too long.",
            "A bivalve that opens in short moments and snaps at whoever lingers. A helix of shots."),
        E("boss:TurningStar", CodexSection.Bosses, "Turning Star", "Stay with the open arc.",
            "A sea star or urchin whose pearl travels round under its skin. A ring of spines with a turning gap."),
        E("boss:SlamAndSigh", CodexSection.Bosses, "Slam and Sigh", "Dodge the slam, punish the rest.",
            "A sea star that slams and then rests, its pearl open while it recovers. An ink pulse ring."),
        E("boss:PulseBell", CodexSection.Bosses, "Pulse Bell", "Hit the heart between pulses.",
            "A great jelly whose bell opens as it contracts. A ring on every pulse."),
        E("boss:TrailingCurtain", CodexSection.Bosses, "Trailing Curtain", "Find the gap in the curtain.",
            "A siphonophore circling at range, its pearl moving along the chain, a wall of pearls hanging from its tentacles."),
        E("boss:TheSpreader", CodexSection.Bosses, "The Spreader", "It paints the floor; when it rears, strike.",
            "A sea hare lobbing arc bombs that stain where they land. It rears after a volley: strike then."),
        E("boss:ShyShell", CodexSection.Bosses, "Shy Shell", "Don't overdo it.",
            "A snail that comes out of its shell; hit it too hard and it withdraws early. A homing pearl."),
        E("boss:BoilHeart", CodexSection.Bosses, "Boil Heart", "Burst the boils to reach the heart.",
            "Its pearl hides under ink sacs. Burst them one by one and the pearl surfaces."),
        E("boss:TheBrood", CodexSection.Bosses, "Drippus Prime", "The murklings' mother.",
            "A vast murkling that births its brood out of its own body. They pop when it is freed."),
        E("boss:CleaningDance", CodexSection.Bosses, "Cleaning Dance", "It moves to its own rhythm.",
            "A boss that dances its round. Learn the steps and find its pearl between them."),
        E("boss:TheHiddenOne", CodexSection.Bosses, "The Hidden One", "You will not see it coming.",
            "A boss that hides in its arena. Find it before you can find its pearl."),
        E("boss:TheTreasure", CodexSection.Bosses, "The Treasure", "Something precious went bad.",
            "A boss made of treasure. Its pearl is the prize at its heart."),

        // ── Reef life ──
        E("fish:Clownfish", CodexSection.ReefLife, "Clownfish", "Home is an anemone.",
            "A pair or trio to an anemone, darting out and back. Come close and they tuck in."),
        E("fish:BlueTang", CodexSection.ReefLife, "Blue tang", "Grazing, always grazing.",
            "Hops from spot to spot about its home, grazing a while at each. Startled, it veers off."),
        E("fish:Chromis", CodexSection.ReefLife, "Chromis", "A school is a single thought.",
            "Small fish in a big school. Swim into a school and it may form an arrow pointing to a secret."),
        E("fish:Flounder", CodexSection.ReefLife, "Flounder", "Flat, and patient.",
            "A flatfish lying on the shelf. The ink can turn it into a Hearthguard."),
        E("fauna:GlassShrimp", CodexSection.ReefLife, "Glass shrimp", "Barely there.",
            "Flee from bright light and gather home again in the dark."),
        E("fauna:LanternShoal", CodexSection.ReefLife, "Lantern fish", "A living light.",
            "A shoal with blue photophores. A steady light with blue in it draws them to follow you; a fast pulse scatters them."),
        E("fauna:Cuttlefish", CodexSection.ReefLife, "Cuttlefish", "It wears the light it is given.",
            "Grey in the dark; lit, its skin takes the colour of the light on it and glows it back, a light the murklings go to."),
        E("fauna:MantisShrimp", CodexSection.ReefLife, "Mantis shrimp", "The fastest strike in the sea.",
            "Hides in its burrow in the dark. Lit, it comes out and strikes whatever comes close: you, or a murkling."),
        E("fauna:FireflySquid", CodexSection.ReefLife, "Firefly squid", "It flashes to a beat.",
            "Glows blue in the dark, flashing at its own beat. Pulse your light at that beat and it leads you to a secret."),

        // ── Plants ──
        E("flora:SunAnemone", CodexSection.Plants, "Sun anemone", "It opens to the light.",
            "Opens its crown in light and closes a while after. What it hid comes out the first time."),
        E("flora:ShyAnemone", CodexSection.Plants, "Shy anemone", "Too bright, and it shuts.",
            "Snaps shut in strong light and opens slowly in the dark. Take what it holds while it is open."),
        E("flora:GiantKelp", CodexSection.Plants, "Giant kelp", "It grows toward you.",
            "Grows a stalk while light is held on it (green the fastest) and withers in the dark. Nothing sees through it."),
        E("flora:SunflowerSponge", CodexSection.Plants, "Sunflower sponge", "It keeps the light for later.",
            "Stores light held on it, then glows for a long while: a lamp the murklings go to."),
        E("flora:NightLily", CodexSection.Plants, "Night lily", "Only in the dark.",
            "Opens and glows a faint violet only in the dark. Its gift is a little light."),
        E("flora:FluorCoral", CodexSection.Plants, "Fluorescent coral", "Blue in, green out.",
            "Drinks high-frequency light (blue the most) and gives it back as a green glow."),

        // ── Waters and stones ──
        E("water:Brine", CodexSection.Waters, "Brine pool", "A lake under the sea.",
            "Dense, glossy water on the floor. Nothing is seen across it."),
        E("water:Silt", CodexSection.Waters, "Silt", "Kicked up, it hides.",
            "Clouds of silt that hide what lies across them while thick. Your dash kicks it up in the Shipwreck and the Ruins."),
        E("water:Vent", CodexSection.Waters, "Vent shimmer", "Hot water trembles.",
            "Water from a vent, its index trembling: your shots wobble crossing it."),
        E("water:Plankton", CodexSection.Waters, "Plankton bloom", "Light it, and it lights back.",
            "Glows cyan when lit, fading after: a decoy the murklings go to."),
        E("wall:Mirror", CodexSection.Waters, "Mirror plate", "Bright metal, turning.",
            "A plate your beam bounces off. Push it to turn it."),
        E("wall:Lightglass", CodexSection.Waters, "Lightglass", "Only its colour passes.",
            "A pane that lets one colour through and stops the rest. Its own colour held on it long enough shatters it."),
        E("wall:SunStone", CodexSection.Waters, "Sun-stone", "Light makes it solid.",
            "Stands only while lit, and a moment after. Light it to make a wall."),
        E("wall:ShadowStone", CodexSection.Waters, "Shadow-stone", "Light makes it vanish.",
            "Stands only in the dark. Keep your light on it to pass."),
        E("wall:Halocline", CodexSection.Waters, "Halocline", "Where salt water meets salt water.",
            "A layer of denser water. Your light bends crossing it, and glances back off its dense side at a low angle."),

        // ── Biomes ──
        E("biome:SunlitReef", CodexSection.Biomes, "Sunlit Reef", "Where the light still reaches.",
            "Clear water, coral and fish: the gentlest depth, and the lesson before the rest."),
        E("biome:KelpForest", CodexSection.Biomes, "Kelp Forest", "Green, and growing.",
            "Green water among tall kelp. Its rock keeps a little of the light it drinks."),
        E("biome:Shipwreck", CodexSection.Biomes, "Shipwreck", "Iron and silt.",
            "Silty water in a wrecked hull. The iron throws your shots back."),
        E("biome:SunkenRuins", CodexSection.Biomes, "Sunken Ruins", "Gilt and dust.",
            "Yellow, dusty water in old stone. Dust drinks blue light; the walls keep a glow."),
        E("biome:AlienWreckage", CodexSection.Biomes, "Alien Wreckage", "Not from here.",
            "Cold clear water in a crystal lattice. Thin crystal walls let light through, bent."),
        E("biome:DrownedTemple", CodexSection.Biomes, "Drowned Temple", "Heavy water, old gods.",
            "Dense brine in a sunken temple: haloclines, brine pools and stones that answer light."),
        E("biome:Atlantis", CodexSection.Biomes, "Atlantis", "The bottom of everything.",
            "Deep clear water, the last depth, with every trick of light the reef knows."),

        // ── Keystones (raised by the spectral survey: Survey.cs) ──
        E("keystone:1", CodexSection.Keystones, "Keystone of the Reef", "A piece of the stone that pinned the old catfish.",
            "Raised by surveying the Sunlit Reef whole: every plant and animal of it lit by each colour, in the dark and with a pulse. One of four pieces of the Atlantean keystone."),
        E("keystone:2", CodexSection.Keystones, "Keystone of the Middle Water", "Green-stained, or rust-stained.",
            "Raised by surveying the Kelp Forest or the Shipwreck whole. One of four pieces of the Atlantean keystone."),
        E("keystone:3", CodexSection.Keystones, "Keystone of the Ruins", "Gilt worn off by hands.",
            "Raised by surveying the Sunken Ruins whole. One of four pieces of the Atlantean keystone."),
        E("keystone:4", CodexSection.Keystones, "Keystone of the Deep", "Cold, and faintly glowing.",
            "Raised by surveying the Alien Wreckage or the Drowned Temple whole. One of four pieces of the Atlantean keystone: together they open the way to Atlantis's sealed heart."),
    };

    static readonly Dictionary<string, CodexEntry> ById = All.ToDictionary(e => e.Id);

    public static bool Has(string id) => ById.ContainsKey(id);

    public static CodexEntry? Get(string id) => ById.GetValueOrDefault(id);

    public static IEnumerable<CodexEntry> In(CodexSection section) => All.Where(e => e.Section == section);

    /// <summary>A section's name as its tab reads.</summary>
    public static string Name(CodexSection s) => s switch
    {
        CodexSection.ReefLife => "Reef life",
        CodexSection.Waters => "Waters & stones",
        _ => s.ToString(),
    };

    /// <summary>Where a locked entry of a section is found (the hint the Sea-pedia shows).</summary>
    public static string Hint(CodexSection s) => s switch
    {
        CodexSection.Places => "Find it somewhere in the reef and swim in.",
        CodexSection.Creatures => "Lurks somewhere in the dark.",
        CodexSection.Bosses => "Waits in an arena, deeper down.",
        CodexSection.ReefLife => "Lives somewhere in the reef.",
        CodexSection.Plants => "Grows somewhere in the reef.",
        CodexSection.Waters => "Lies somewhere in the deep.",
        CodexSection.Keystones => "Survey a biome's plants and animals whole to raise it.",
        _ => "Dive deeper to reach it.",
    };

    // The ids the world reports (PlaneCodex.cs).
    public static string Place(PoiKind k) => "place:" + k;
    public static string Biome(Biome b) => "biome:" + b;
    public static string Boss(BossConcept c) => "boss:" + c;
    public static string Murk(Murkling m) =>
        m.Prime ? "murk:prime" : m.Lesser ? "murk:lesser" : m.Kind switch
        {
            MurkKind.Thrower => "murk:thrower",
            MurkKind.Hunter => "murk:hunter",
            MurkKind.Shoal => "murk:shoal",
            _ => "murk:basic",
        };

    /// <summary>The Sea-pedia's old records (before the codex): what they unlock now.</summary>
    public static string? Legacy(string seen) => seen switch
    {
        PlaneProfileRecorder.MobId => "mob:pufferling",
        PlaneProfileRecorder.QueenClamId => "boss:TheLid",
        _ => null,
    };

    /// <summary>An entry's record in the profile's creature records, where it has one.</summary>
    public static string? RecordOf(string id) => id switch
    {
        "mob:pufferling" => PlaneProfileRecorder.MobId,
        "boss:TheLid" => PlaneProfileRecorder.QueenClamId,
        _ => null,
    };
}
