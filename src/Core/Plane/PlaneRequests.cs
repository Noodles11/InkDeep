using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Items;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>The Tide Fan's numbers (docs/QUESTS-PROPOSAL.md): first guesses, tuned in play.</summary>
public static class RequestTuning
{
    /// <summary>Ribbons on the fan for each region, and the requests she may carry at once.</summary>
    public const int Places = 3, Carry = 2;
    /// <summary>A request pays ShellsBase + ShellsPerLevel × its level shells; a request whose token is shells, ShellToken times that.</summary>
    public const int ShellsBase = 15, ShellsPerLevel = 10, ShellToken = 3;
    /// <summary>The share of requests whose token is a pearl (the rest offer shells).</summary>
    public const float PearlShare = 0.75f;
    /// <summary>Wash the walls asks for AshBase + AshPerLevel × its level patches.</summary>
    public const int AshBase = 120, AshPerLevel = 30;
    /// <summary>How near the fan's foot she must swim to be at it (metres).</summary>
    public const float FanReach = 2.4f;

    /// <summary>A request's experience on untying: as much again as a leak of its level (on top of the leaks it sends her to).</summary>
    public static int Xp(int level) => ReefTuning.LeakXp(level);

    /// <summary>The quality of pearl a request of this level offers (lowest, highest).</summary>
    public static (int Low, int High) Quality(int level) => level <= 1 ? (1, 2) : level <= 3 ? (2, 3) : (3, 4);
}

/// <summary>
/// The Tide Fan's requests (docs/QUESTS-PROPOSAL.md): the ribbons tied for each region from her jelly's own seed, taking
/// them (two at most), letting them go, their progress from what she does out in the region's caves, and untying them
/// when done. The game raises the progress from the sim's events; everything else lives on her jelly.
/// </summary>
public static class Requests
{
    public const string Seal = "seal", Prime = "prime", Den = "den", Ash = "ash";

    /// <summary>Who ties a request of each kind, and what they say (docs/QUESTS-PROPOSAL.md §3.5).</summary>
    public static readonly Dictionary<string, (string Who, string Line)[]> Posters = new()
    {
        [Seal] = new[]
        {
            ("The clownfish of the glade", "Our anemone went grey the night the {mouth} mouth opened. Close it, and it might come back."),
            ("A pufferfish mother", "My little ones keep swimming toward the {mouth} mouth. It tastes warm, they say. Please shut it."),
            ("The sea hare", "Something sweet and wrong comes up out of the {mouth} mouth. The weed won't grow round it any more."),
        },
        [Prime] = new[]
        {
            ("An old blue tang", "A great dark drop swallowed my whole school in the caves. They're still in there. I can feel them."),
            ("A chromis who swam home alone", "There were thirty of us when the dark came down. Find the big one that took the rest."),
            ("The sea hare", "The caves have a hungry thing in them, all one drop of ink. Whatever it swallowed, let it out."),
        },
        [Den] = new[]
        {
            ("The moray who never leaves her crack", "There's a tunnel behind the {mouth} mouth where the dark sits like ink in a shell. Something of ours fell in."),
            ("A hermit crab between shells", "I dropped the best shell I ever had down a dark tunnel past the {mouth} mouth. I'd go myself, but the dark."),
            ("An old blue tang", "My grandmother said never swim the long tunnel past the {mouth} mouth. Someone should, though. For her."),
        },
        [Ash] = new[]
        {
            ("The cleaner shrimp", "We'd scrub those walls ourselves if the Leak would let us near. Clear the grey off for us?"),
            ("A young octopus", "I used to read the walls down there, all the colours. Now they're grey. Wash them back?"),
            ("The clownfish of the glade", "The grey on the cave walls is the Leak's breath, my mother says. It comes off in light."),
        },
    };

    /// <summary>A request's poster: who tied it, and their line (its mouth named, "the east").</summary>
    public static (string Who, string Line) PosterOf(Request r, IReadOnlyList<string> mouths)
    {
        var all = Posters.TryGetValue(r.Kind, out var p) ? p : Posters[Seal];
        var (who, line) = all[Math.Clamp(r.Poster, 0, all.Length - 1)];
        return (who, line.Replace("{mouth}", MouthName(r.Mouth, mouths)));
    }

    static string MouthName(int mouth, IReadOnlyList<string> mouths) =>
        mouth >= 0 && mouth < mouths.Count && mouths[mouth].Length > 0 ? mouths[mouth] : mouth >= 0 ? $"{mouth + 1}." : "";

    /// <summary>What it asks, plainly, with numbers (the caption under the poster's line).</summary>
    public static string Caption(Request r, string regionTitle, IReadOnlyList<string> mouths) => r.Kind switch
    {
        Seal => $"Seal the leak behind the {MouthName(r.Mouth, mouths)} mouth (level {r.Level})",
        Prime => $"Free a murkling prime in a {regionTitle} cave (level {r.Level})",
        Den => $"Reach the end of the curse den behind the {MouthName(r.Mouth, mouths)} mouth, and swim back out (level {r.Level})",
        _ => $"Clear {r.Goal} patches of ash in {regionTitle} caves ({r.Progress} / {r.Goal})",
    };

    /// <summary>The request in a few words, for the HUD's ribbons.</summary>
    public static string Short(Request r, IReadOnlyList<string> mouths) => r.Done ? "Done · back to the Tide Fan" : r.Kind switch
    {
        Seal => $"Seal the {MouthName(r.Mouth, mouths)} mouth's leak",
        Prime => "Free a murkling prime",
        Den => $"Brave the {MouthName(r.Mouth, mouths)} mouth's curse den",
        _ => $"Wash the walls · {r.Progress} / {r.Goal}",
    };

    /// <summary>The fan's places for a region (made the first time, three of them).</summary>
    public static List<Request?> Places(Jelly jelly, string region)
    {
        if (!jelly.Fan.TryGetValue(region, out var places)) jelly.Fan[region] = places = new List<Request?>();
        while (places.Count < RequestTuning.Places) places.Add(null);
        return places;
    }

    /// <summary>
    /// Ties a new ribbon in every empty place on the region's fan (she has come home): its kind (a seal request always
    /// among them; otherwise no kind twice where it can help it), its mouth, its level, its poster and what it pays, all
    /// from her seed and the count of ribbons tied for the region.
    /// </summary>
    /// <param name="level">The region's cave level now.</param>
    /// <param name="mouths">How many cave mouths the region has.</param>
    /// <param name="denBehind">Whether the cave behind a mouth holds a curse den now.</param>
    /// <param name="canOffer">Whether a pearl may be offered to her (unlocked, not held).</param>
    public static void Fill(Jelly jelly, string region, int level, int mouths, Func<int, bool> denBehind, Func<string, bool> canOffer, ItemCatalog? catalog)
    {
        var places = Places(jelly, region);
        for (int i = 0; i < places.Count; i++)
        {
            if (places[i] is not null) continue;
            int number = jelly.Tied.TryGetValue(region, out int n) ? n : 0;
            jelly.Tied[region] = number + 1;
            places[i] = Tie(jelly, region, number, level, mouths, denBehind, canOffer, catalog);
        }
    }

    static Request Tie(Jelly jelly, string region, int number, int level, int mouths, Func<int, bool> denBehind, Func<string, bool> canOffer, ItemCatalog? catalog)
    {
        var rng = new Rng(Hash(jelly.Seed) ^ Hash(region) * 0x9E3779B97F4A7C15UL ^ (ulong)(number + 1) * 0xBF58476D1CE4E5B9UL);
        var others = Others(jelly, region).ToList();
        // A seal request is always on the fan (the way to reopen mouths, and the leaks' experience); else a kind not on
        // the fan or carried yet, where there is one.
        string kind;
        if (!Places(jelly, region).Any(o => o?.Kind == Seal)) kind = Seal;
        else
        {
            var fresh = new[] { Seal, Prime, Den, Ash }.Where(k => !others.Any(o => o.Kind == k)).ToList();
            var pool = fresh.Count > 0 ? fresh : new List<string> { Seal, Prime, Den, Ash };
            kind = pool[rng.Int(pool.Count)];
        }
        int mouth = -1;
        if (kind is Seal or Den && mouths > 0)
        {
            // A mouth no other request names, where it can be (a den only where its cave holds one).
            var open = Enumerable.Range(0, mouths).Where(m => kind == Seal || denBehind(m)).ToList();
            var unnamed = open.Where(m => !others.Any(o => o.Kind == kind && o.Mouth == m)).ToList();
            var pick = unnamed.Count > 0 ? unnamed : open;
            if (pick.Count == 0) kind = Prime;
            else mouth = pick[rng.Int(pick.Count)];
        }
        var r = new Request
        {
            Kind = kind, Region = region, Mouth = kind is Seal or Den ? mouth : -1, Level = Math.Max(level, 1), Number = number,
            Poster = rng.Int(Posters[kind].Length),
        };
        r.Goal = kind == Ash ? RequestTuning.AshBase + RequestTuning.AshPerLevel * r.Level : 1;
        r.Xp = RequestTuning.Xp(r.Level);
        r.Shells = RequestTuning.ShellsBase + RequestTuning.ShellsPerLevel * r.Level;
        if (rng.NextFloat() < RequestTuning.PearlShare) r.Pearl = PickPearl(jelly, region, r.Level, canOffer, catalog, rng);
        if (r.Pearl is null) r.Shells *= RequestTuning.ShellToken;
        return r;
    }

    /// <summary>
    /// A passive pearl of the treasure pool she could be offered and has not absorbed, of the quality its level asks
    /// (any quality, failing that), and offered by no other ribbon or carried request.
    /// </summary>
    static string? PickPearl(Jelly jelly, string region, int level, Func<string, bool> canOffer, ItemCatalog? catalog, Rng rng)
    {
        if (catalog is null) return null;
        var taken = new HashSet<string>(jelly.Fan.Values.SelectMany(p => p).Concat(jelly.Carried).Where(o => o?.Pearl is not null).Select(o => o!.Pearl!));
        var all = PlaneRun.PortedPearls
            .Where(id => catalog.TryGet(id, out var it) && it.Kind == ItemKind.Passive && it.Pools.Contains("treasure")
                         && canOffer(id) && !jelly.Passives.Contains(id) && !taken.Contains(id))
            .ToList();
        var (low, high) = RequestTuning.Quality(level);
        var band = all.Where(id => catalog.TryGet(id, out var it) && it.Quality >= low && it.Quality <= high).ToList();
        var pick = band.Count > 0 ? band : all;
        return pick.Count == 0 ? null : pick[rng.Int(pick.Count)];
    }

    /// <summary>The other requests of a region: on its fan and carried.</summary>
    static IEnumerable<Request> Others(Jelly jelly, string region) =>
        Places(jelly, region).Where(r => r is not null).Select(r => r!).Concat(jelly.Carried.Where(r => r.Region == region));

    /// <summary>
    /// She takes the ribbon in a place (if she carries fewer than two): it is hers to do. A seal request calls its
    /// mouth's leak out: shut since she sealed it, it opens again now on a fresh cave.
    /// </summary>
    public static bool Take(Jelly jelly, string region, int place)
    {
        var places = Places(jelly, region);
        if (jelly.Carried.Count >= RequestTuning.Carry || place < 0 || place >= places.Count || places[place] is not { } r) return false;
        places[place] = null;
        jelly.Carried.Add(r);
        if (r.Kind == Seal && jelly.Shut.TryGetValue(region, out var shut)) shut.Remove(r.Mouth);
        return true;
    }

    /// <summary>She lets a carried request go: its ribbon goes back on the fan (if there is a place for it), for nothing.</summary>
    public static void LetGo(Jelly jelly, int carried)
    {
        if (carried < 0 || carried >= jelly.Carried.Count) return;
        var r = jelly.Carried[carried];
        jelly.Carried.RemoveAt(carried);
        var places = Places(jelly, r.Region);
        int free = places.IndexOf(null);
        if (free >= 0) places[free] = r;
    }

    /// <summary>
    /// Something she did out in a region: a leak sealed behind a mouth, a prime freed, a curse den cleared behind a
    /// mouth, ash washed off (how many patches). The requests it answers go forward; those it finishes are returned.
    /// </summary>
    public static List<Request> Note(Jelly jelly, string kind, string region, int mouth = -1, int amount = 1)
    {
        var done = new List<Request>();
        foreach (var r in jelly.Carried)
        {
            if (r.Done || r.Kind != kind || r.Region != region) continue;
            if (kind is Seal or Den && r.Mouth != mouth) continue;
            r.Progress = Math.Min(r.Goal, r.Progress + amount);
            if (r.Done) done.Add(r);
        }
        return done;
    }

    /// <summary>A carried request done, untied at the fan: it is gone, and what it pays is hers (the game pays it).</summary>
    public static Request? Untie(Jelly jelly, int carried)
    {
        if (carried < 0 || carried >= jelly.Carried.Count || !jelly.Carried[carried].Done) return null;
        var r = jelly.Carried[carried];
        jelly.Carried.RemoveAt(carried);
        return r;
    }

    /// <summary>A string's FNV-1a hash (stable from run to run, unlike string.GetHashCode).</summary>
    static ulong Hash(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in s)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        return h;
    }
}
