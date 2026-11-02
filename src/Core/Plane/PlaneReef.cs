using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>The open reef's numbers (docs/RPG-PROPOSAL.md): first guesses, tuned in play.</summary>
public static class ReefTuning
{
    /// <summary>How near a current station's middle, or a cave mouth, she must swim to be taken through it (metres).</summary>
    public const float StationReach = 3.5f, MouthReach = 3f;
    /// <summary>A leak cave's mouth takes her back out only once she has been this far into the cave.</summary>
    public const float LeaveAfter = 10f;
    /// <summary>A leak cave is the Leak's own: at least this share of its water starts inked.</summary>
    public const float CaveInk = 0.9f;
    /// <summary>A minor leak is worth LeakXpBase × its cave's level^LeakXpPower experience.</summary>
    public const float LeakXpBase = 25f, LeakXpPower = 0.8f;
    /// <summary>A region's caves go up a level every CavesPerLevel sealed in it.</summary>
    public const int CavesPerLevel = 3;

    public static int LeakXp(int caveLevel) => (int)MathF.Round(LeakXpBase * MathF.Pow(Math.Max(caveLevel, 1), LeakXpPower));
}

/// <summary>
/// The open reef's places (docs/RPG-PROPOSAL.md §2): Lantern Bay, the regions (each a hand-drawn map), and behind each
/// region's cave mouths a leak cave, seeded from her own world seed. Each mouth always leads to a cave: sealing it puts a
/// fresh one behind the same mouth (mouth i holds caves i, i + mouths, i + 2·mouths…), so a region never runs out.
/// </summary>
public static class OpenReef
{
    /// <summary>The bay's map, and the regions' maps in their order (a region's number is its place here, from 1).</summary>
    public const string Bay = "lantern_bay";
    public static readonly string[] Regions = { "shallows" };

    /// <summary>The region a current station of the bay leads to first.</summary>
    public static string FirstRegion => Regions[0];

    public static int RegionNumber(string region) => Array.IndexOf(Regions, region) + 1;

    /// <summary>The cave behind a region's mouth now.</summary>
    public static int CaveBehind(Jelly jelly, string region, int mouth)
    {
        var caves = CavesOf(jelly, region);
        while (caves.Count <= mouth) caves.Add(caves.Count);
        return caves[mouth];
    }

    /// <summary>She sealed the cave behind this mouth: a fresh one waits there now, and the region counts one more.</summary>
    public static void Sealed(Jelly jelly, string region, int mouth, int mouths)
    {
        int now = CaveBehind(jelly, region, mouth);
        CavesOf(jelly, region)[mouth] = now + Math.Max(mouths, 1);
        jelly.Sealed[region] = SealedIn(jelly, region) + 1;
    }

    public static int SealedIn(Jelly jelly, string region) => jelly.Sealed.TryGetValue(region, out int n) ? n : 0;

    /// <summary>The level of the region's caves now: its tier, one more every few sealed in it.</summary>
    public static int CaveLevel(Jelly jelly, string region, int tier) => tier + SealedIn(jelly, region) / ReefTuning.CavesPerLevel;

    /// <summary>A cave's own level id (apart from every run level and hand-drawn map), from its region, depth and number.</summary>
    public static LevelId CaveId(string region, int depth, int cave) => new(1000 + RegionNumber(region), depth, cave + 1);

    static List<int> CavesOf(Jelly jelly, string region) =>
        jelly.Caves.TryGetValue(region, out var list) ? list : jelly.Caves[region] = new List<int>();
}

public sealed partial class PlaneWorld
{
    /// <summary>She is in a hand-drawn map's current station (the bay's, a region's), where the ride starts.</summary>
    public bool AtStation => Map.Station && !Defeated && Vector2.Distance(Player.Position, Map.Exit.Position) < ReefTuning.StationReach;

    /// <summary>The region's cave mouth she is in (its index), or −1.</summary>
    public int AtMouth()
    {
        if (Defeated) return -1;
        for (int i = 0; i < Map.Mouths.Count; i++)
            if (Vector2.Distance(Player.Position, Map.Mouths[i]) < ReefTuning.MouthReach) return i;
        return -1;
    }

    bool _wentIn;

    /// <summary>Back at a leak cave's mouth, having been into it: she leaves the cave.</summary>
    public bool LeavingCave
    {
        get
        {
            if (!Map.LeakCave || Defeated) return false;
            float d = Vector2.Distance(Player.Position, Map.Start.Position);
            if (d > ReefTuning.LeaveAfter) _wentIn = true;
            return _wentIn && d < ReefTuning.MouthReach + 1f;
        }
    }

    /// <summary>The depth whose life fills the water: the open reef's places have their region's fish whatever its depth.</summary>
    int LifeDepth => Map.LeakCave || Map.Station ? Math.Max(Map.Depth, AdventureTuning.BaseHome) : Map.Depth;
}
