using System.Collections.Generic;

namespace OctoShoots.Core.Saves;

/// <summary>The whole save: one profile plus at most one suspended run (2D §13.1).</summary>
public sealed class SaveFile
{
    public int Version { get; set; } = SaveCodec.CurrentVersion;
    public Profile Profile { get; set; } = new();
    public SuspendedRun? Run { get; set; }
    /// <summary>The jellies made on this machine (docs/RPG-PROPOSAL.md §8, §11: local profiles), each her own save.</summary>
    public List<Jelly> Jellies { get; set; } = new();
    /// <summary>Which of them was played last (an index into Jellies; −1: none yet).</summary>
    public int Chosen { get; set; } = -1;

    [System.Text.Json.Serialization.JsonIgnore]
    public Jelly? Current => Chosen >= 0 && Chosen < Jellies.Count ? Jellies[Chosen] : null;
}

/// <summary>
/// One jelly of the Clementine bloom, as the player made her (docs/RPG-PROPOSAL.md): her name and look, and everything
/// she keeps between trips out of Lantern Bay. What she carries on a trip that death can take (her passive pearls and
/// the shells on her) is kept here too, between trips.
/// </summary>
public sealed class Jelly
{
    public string Name { get; set; } = "";
    public JellyLook Look { get; set; } = new();
    /// <summary>Her level and the experience toward the next; only sealing leaks gives experience.</summary>
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    /// <summary>Skill points not yet spent, and the points spent (by stat name): the only way her stats change.</summary>
    public int Points { get; set; }
    public Dictionary<string, int> Skills { get; set; } = new();
    /// <summary>Silver shells on her, and those she has left with Fennel in the bay (BroodBed: never lost).</summary>
    public int Shells { get; set; }
    public int Banked { get; set; }
    /// <summary>The passive pearls absorbed into her bell (lost as lost light when she dims).</summary>
    public List<string> Passives { get; set; } = new();
    /// <summary>Every active pearl she owns (never lost), and which sit in her three slots (null: an empty slot).</summary>
    public List<string> Actives { get; set; } = new();
    public List<string?> Slots { get; set; } = new() { null, null, null };
    /// <summary>The great leaks sealed for good, and the named creatures freed (now living in the bay), by id.</summary>
    public HashSet<string> GreatLeaks { get; set; } = new();
    public HashSet<string> Freed { get; set; } = new();
    /// <summary>Her world's seed code: every leak cave is grown from it.</summary>
    public string Seed { get; set; } = "";
    /// <summary>Per region: the cave behind each of its mouths now, and how many minor leaks she has sealed there (OpenReef).</summary>
    public Dictionary<string, List<int>> Caves { get; set; } = new();
    public Dictionary<string, int> Sealed { get; set; } = new();
    /// <summary>
    /// Per region: its mouths shut since she sealed the cave behind them, each with her play time then (PlaySeconds).
    /// The Leak seeps back in after ReefTuning.Recorrupt of play, and the mouth opens again on a fresh cave.
    /// </summary>
    public Dictionary<string, Dictionary<int, double>> Shut { get; set; } = new();
    /// <summary>Per region: what she has charted of it (its map's fog) and the places in it she has been to.</summary>
    public Dictionary<string, RegionChart> Charts { get; set; } = new();
    /// <summary>
    /// The Tide Fan's requests (docs/QUESTS-PROPOSAL.md): per region, the ribbons tied on it (a place each, null where
    /// one was taken), the requests she carries, and how many ribbons have ever been tied for each region.
    /// </summary>
    public Dictionary<string, List<Request?>> Fan { get; set; } = new();
    public List<Request> Carried { get; set; } = new();
    public Dictionary<string, int> Tied { get; set; } = new();
    /// <summary>Her lost light, where she last dimmed (null: none, or she took it back).</summary>
    public LostLight? Lost { get; set; }
    /// <summary>When she was made and last played, as ISO 8601 local times.</summary>
    public string Made { get; set; } = "";
    public string Played { get; set; } = "";
    /// <summary>Her time played on the open reef (seconds): the clock the shut mouths reopen by.</summary>
    public double PlaySeconds { get; set; }
}

/// <summary>
/// One request off the Tide Fan (docs/QUESTS-PROPOSAL.md §3): what it asks (Kind: seal, prime, den or ash), in which
/// region and (seal, den) behind which mouth, its level, who tied it (an index into its kind's posters), what it pays
/// (a passive pearl or none, silver shells, experience), and how far along it is.
/// </summary>
public sealed class Request
{
    public string Kind { get; set; } = "";
    public string Region { get; set; } = "";
    public int Mouth { get; set; } = -1;
    public int Level { get; set; } = 1;
    public int Poster { get; set; }
    public string? Pearl { get; set; }
    public int Shells { get; set; }
    public int Xp { get; set; }
    public int Goal { get; set; } = 1;
    public int Progress { get; set; }
    /// <summary>Its number among the ribbons ever tied for its region.</summary>
    public int Number { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Done => Progress >= Goal;
}

/// <summary>A region's map as she has charted it: its fog (a byte a grid point, base64) and the places she has visited.</summary>
public sealed class RegionChart
{
    public string Fog { get; set; } = "";
    public List<int> Visited { get; set; } = new();
}

/// <summary>
/// What death took from a jelly (docs/RPG-PROPOSAL.md §5.1): her passive pearls and the shells on her, left glowing where
/// she dimmed — the place (OpenReef.PlaceKey) and the spot in it.
/// </summary>
public sealed class LostLight
{
    public string Place { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public List<string> Pearls { get; set; } = new();
    public int Shells { get; set; }
}

/// <summary>How she looks (docs/RPG-PROPOSAL.md §8): every colour as #RRGGBB, any colour the player likes.</summary>
public sealed class JellyLook
{
    /// <summary>The shape her four gonads take (JellyNames.Symbols).</summary>
    public string Gonads { get; set; } = "horseshoe";
    /// <summary>Her glow: her light, her rim, her shots. Clementine's own gold by default.</summary>
    public string Glow { get; set; } = "#FFC46B";
    /// <summary>Her bell and oral arms.</summary>
    public string Arms { get; set; } = "#FF8C42";
    /// <summary>The tips of her eight tentacles.</summary>
    public string Tips { get; set; } = "#FFD97A";
}

/// <summary>The rules for a jelly's name and the choices for her look.</summary>
public static class JellyNames
{
    public const int MaxLength = 16;
    /// <summary>How many jellies (save slots) a machine keeps: one must go before a fourth is made.</summary>
    public const int MaxJellies = 3;
    public static readonly string[] Symbols = { "horseshoe", "ring", "clover", "crescent", "star" };

    /// <summary>Why a name cannot be taken (empty, too long, odd characters, or another jelly's already), or null.</summary>
    public static string? Problem(string name, IEnumerable<Jelly> others)
    {
        string n = name.Trim();
        if (n.Length == 0) return "She needs a name";
        if (n.Length > MaxLength) return $"At most {MaxLength} letters";
        foreach (char c in n)
            if (!char.IsLetterOrDigit(c) && c is not (' ' or '-' or '\'' or '_')) return "Letters, digits, spaces, - ' and _ only";
        foreach (var o in others)
            if (string.Equals(o.Name.Trim(), n, System.StringComparison.OrdinalIgnoreCase)) return "Another jelly has that name";
        return null;
    }

    /// <summary>Whether a colour is written as the look keeps it (#RRGGBB).</summary>
    public static bool IsColour(string s) =>
        s.Length == 7 && s[0] == '#' && System.Linq.Enumerable.All(s.Substring(1), System.Uri.IsHexDigit);
}

/// <summary>Meta progress: achievements (which unlock pearls), the Sea-pedia's records and the statistics.</summary>
public sealed class Profile
{
    public HashSet<string> Achievements { get; set; } = new();
    /// <summary>Pearls she has seen offered (in a room or a shop) or taken.</summary>
    public HashSet<string> SeenItems { get; set; } = new();
    /// <summary>Creatures and bosses she has met.</summary>
    public HashSet<string> SeenCreatures { get; set; } = new();
    public HashSet<string> SeenBosses { get; set; } = new();
    public ProfileStats Stats { get; set; } = new();
    /// <summary>Per pearl id: how it has done.</summary>
    public Dictionary<string, PearlRecord> Pearls { get; set; } = new();
    /// <summary>Per creature or boss id: how it has done against her.</summary>
    public Dictionary<string, CreatureRecord> Creatures { get; set; } = new();
    /// <summary>
    /// She has dived out of the tutorial (Depth 0) once: from now on runs start in Depth 1, unless the settings ask to
    /// replay it.
    /// </summary>
    public bool TutorialPassed { get; set; }
    /// <summary>The last few seeds she played, newest first.</summary>
    public List<string> RecentSeeds { get; set; } = new();

    /// <summary>Grants an achievement unless the run used a custom seed (Isaac rule). Returns true when new.</summary>
    public bool Award(string achievement, bool customSeed) => !customSeed && Achievements.Add(achievement);

    public PearlRecord Pearl(string id) => Pearls.TryGetValue(id, out var r) ? r : Pearls[id] = new PearlRecord();

    public CreatureRecord Creature(string id) => Creatures.TryGetValue(id, out var r) ? r : Creatures[id] = new CreatureRecord();
}

/// <summary>Totals and records over every run (seeded runs included; SeededRuns counts them).</summary>
public sealed class ProfileStats
{
    public int Runs { get; set; }
    public int SeededRuns { get; set; }
    public int Wins { get; set; }
    public int Deaths { get; set; }
    public int Abandoned { get; set; }
    public int Kills { get; set; }
    public int RoomsCleared { get; set; }
    public int BossesFreed { get; set; }
    public long BubblesThrown { get; set; }
    public int FullBubbles { get; set; }
    public int BiggestVolley { get; set; }
    public double DamageDealt { get; set; }
    public double DamageTaken { get; set; }
    public long ShellsCollected { get; set; }
    public long ShellsSpent { get; set; }
    public int PearlsAbsorbed { get; set; }
    public int UntouchableRooms { get; set; }
    public double PlaySeconds { get; set; }
    public double LongestRunSeconds { get; set; }
    /// <summary>0 until a room has been cleared.</summary>
    public double FastestRoomSeconds { get; set; }
    public int MostShellsInRoom { get; set; }
    /// <summary>The furthest she has got: depth, then room within it.</summary>
    public int BestDepth { get; set; }
    public int BestRoom { get; set; }
    /// <summary>What ended each run, by source id (see the sim's damage sources).</summary>
    public Dictionary<string, int> DeathsByCause { get; set; } = new();
}

public sealed class PearlRecord
{
    public int Absorbed { get; set; }
    /// <summary>Runs in which she held it at some point.</summary>
    public int Runs { get; set; }
    /// <summary>Runs that ended in her death while she held it.</summary>
    public int RunsLost { get; set; }
    public int RoomsCleared { get; set; }
}

public sealed class CreatureRecord
{
    /// <summary>Times one noticed her (bosses: times she entered the arena).</summary>
    public int Encounters { get; set; }
    /// <summary>Times she beat one (bosses: times freed).</summary>
    public int Defeated { get; set; }
    /// <summary>Runs one of them ended.</summary>
    public int DefeatedYou { get; set; }
    /// <summary>Bosses: the fastest time from landing to freed (0 until freed once).</summary>
    public double BestSeconds { get; set; }
}

/// <summary>
/// A run saved at the start of a level (2D §13.1): continuing regenerates that level from the seed (a level needs no
/// other level generated first) and puts her back at its start with what she carried in.
/// </summary>
public sealed class SuspendedRun
{
    public string Seed { get; set; } = "";
    public bool CustomSeed { get; set; }
    public int Cycle { get; set; } = 1;
    public int Depth { get; set; } = 1;
    public int Level { get; set; } = 1;

    /// <summary>Where the run stands, as the HUD names it.</summary>
    public string Where => new OctoShoots.Core.Gen.TopDown.LevelId(Cycle, Depth, Level).ToString();
    public List<string> Items { get; set; } = new();
    public float Hp { get; set; }
    /// <summary>The Crab Deals she has struck.</summary>
    public int ArmCuts { get; set; }
    public int Shells { get; set; }
    /// <summary>How charged her active pearl was (0–1).</summary>
    public float ActiveCharge { get; set; } = 1f;
    /// <summary>Her experience: toward the next level, her level, unspent skill points, and the points spent (by stat name).</summary>
    public int Xp { get; set; }
    public int XpLevel { get; set; } = 1;
    public int Points { get; set; }
    public Dictionary<string, int> Skills { get; set; } = new();
    /// <summary>Her luck as the level was made: it is made again with it (her luck can bring a light spring).</summary>
    public float Luck { get; set; }
    /// <summary>Play time of the run so far (the HUD clock).</summary>
    public double Elapsed { get; set; }
    /// <summary>The run's totals so far, for the death splash.</summary>
    public int Foes { get; set; }
    public int ShellsCollected { get; set; }
    /// <summary>When it was saved, as an ISO 8601 local time.</summary>
    public string SavedAt { get; set; } = "";

    /// <summary>
    /// The level as she left it, mid-play (null: she left it at its start): continuing resumes that very moment. The
    /// fields above stay as the level's start, to fall back on if the moment cannot be restored.
    /// </summary>
    public LevelState? State { get; set; }
}

/// <summary>A level in play, exactly (<see cref="Snapshot"/>), with what the game keeps beside it.</summary>
public sealed class LevelState
{
    /// <summary>The level's state (the Snapshot's bytes, base64).</summary>
    public string World { get; set; } = "";
    /// <summary>The map's fog: what she has seen of the level (base64), and the places she has visited.</summary>
    public string Fog { get; set; } = "";
    public List<int> Visited { get; set; } = new();
    /// <summary>Time on the level so far.</summary>
    public double LevelTime { get; set; }
    /// <summary>What she carried, her HP and shells then (for the save card).</summary>
    public List<string> Items { get; set; } = new();
    public float Hp { get; set; }
    public int Shells { get; set; }
}

/// <summary>
/// Achievement ids. The first five are the plane's (data/achievements.json; PlaneAchievements earns them); the rest
/// are still named by the unlocks of pearls not on the plane yet, for bosses still to come.
/// </summary>
public static class Achievements
{
    public const string BigBubbleEnergy = "big_bubble_energy";
    public const string BubbleBath = "bubble_bath";
    public const string ShuckedInFifteen = "shucked_in_fifteen";
    public const string Untouchable = "untouchable";
    public const string HermitHoarder = "hermit_hoarder";

    public const string Jesters = "jesters";
    public const string Siphonophore = "siphonophore";
    public const string HollowMaw = "hollow_maw";
}
