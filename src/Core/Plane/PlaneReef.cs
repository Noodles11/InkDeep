using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>The open reef's numbers (docs/RPG-PROPOSAL.md): first guesses, tuned in play.</summary>
public static class ReefTuning
{
    /// <summary>How near a current station's middle she must swim to be taken through it (metres).</summary>
    public const float StationReach = 3.5f;
    /// <summary>How far into a cave's opening (past its threshold, Doorway) she must swim to be taken through it.</summary>
    public const float MouthIn = 1.4f;
    /// <summary>How near the bay mirror's middle she must swim for it to show her (metres: well inside its pool).</summary>
    public const float MirrorReach = 1.3f;
    /// <summary>A leak cave's mouth takes her back out only once she has been this far into the cave.</summary>
    public const float LeaveAfter = 10f;
    /// <summary>A leak cave is the Leak's own: at least this share of its water starts inked.</summary>
    public const float CaveInk = 0.9f;
    /// <summary>
    /// A leak cave's murklings (no fish live in it, so nothing turns): CaveMurklings + CaveMurklingsPerMenace × Menace wait
    /// along its path at the start, CaveMurkSpacing apart and at least CaveMurkFromStart from its mouth, and a prime waits
    /// CavePrimeBeforeLeak short of the leak.
    /// </summary>
    public const int CaveMurklings = 4;
    public const float CaveMurklingsPerMenace = 3f, CaveMurkSpacing = 9f, CaveMurkFromStart = 14f, CavePrimeBeforeLeak = 9f;
    /// <summary>
    /// While its leak is open, one buds out of its ink every CaveBudEvery s ÷ (1 + 0.6 × Menace), CaveBudNear–CaveBudFar
    /// from her, while fewer than CaveCap + CaveCapPerMenace × Menace are within CaveCapReach of her; now and then a prime
    /// (BlightTuning's chance, at most PrimePerLevel a cave, the waiting one counted). Sealed, it buds no more.
    /// </summary>
    public const float CaveBudEvery = 10f, CaveBudNear = 8f, CaveBudFar = 18f, CaveCapPerMenace = 2f, CaveCapReach = 24f;
    public const int CaveCap = 4;
    /// <summary>A minor leak is worth LeakXpBase × its cave's level^LeakXpPower experience.</summary>
    public const float LeakXpBase = 25f, LeakXpPower = 0.8f;
    /// <summary>A cave mouth's danger is 0 to MostDanger; each step adds MenacePerDanger to its cave's Menace.</summary>
    public const int MostDanger = 2;
    public const float MenacePerDanger = 0.1f;
    /// <summary>Each level past the first adds MeterPerLevel to a creature's corruption meter, and HarmPerLevel to the harm it does (docs/REEF-WORLD.md §4).</summary>
    public const float MeterPerLevel = 0.25f, HarmPerLevel = 0.12f;

    /// <summary>
    /// The reef's murkling packs in a zone of this look (docs/REEF-WORLD.md §4): how many, how many murklings in each,
    /// and the share led by a prime.
    /// </summary>
    public static (int Packs, int Least, int Most, float PrimeShare) Packs(int look) => look switch
    {
        <= 1 => (4, 1, 2, 0f),
        2 => (8, 2, 3, 0f),
        3 => (10, 3, 4, 0.1f),
        _ => (12, 4, 5, 0.3f),
    };
    /// <summary>No pack within this of a cave's mouth or the haven (twice it of her start).</summary>
    public const float PackClear = 14f;
    /// <summary>
    /// On the zoned reef only what is within this of her lives (its fish and murklings); the rest hold still where they
    /// are until she comes near, as a Diablo map's far monsters wait (and the 840 m reef stays cheap to run).
    /// </summary>
    public const float Awake = 60f;
    /// <summary>A sealed cave's mouth stays shut this long of her play time (s); then the Leak seeps back and it opens on a fresh cave.</summary>
    public const double Recorrupt = 15 * 60;

    public static int LeakXp(int caveLevel) => (int)MathF.Round(LeakXpBase * MathF.Pow(Math.Max(caveLevel, 1), LeakXpPower));

    /// <summary>A pearl's worth in silver shells at the shell shop: PearlWorthBase + PearlWorthPerQuality × its quality.</summary>
    public const int PearlWorthBase = 20, PearlWorthPerQuality = 20;
    public static int PearlWorth(int quality) => PearlWorthBase + PearlWorthPerQuality * Math.Max(quality, 0);

    /// <summary>How near her lost light she must swim to take it back (metres).</summary>
    public const float LostReach = 1.4f;
}

/// <summary>
/// Her lost light (docs/RPG-PROPOSAL.md §5.1): where she dimmed, everything death took from her — her passive pearls and
/// the shells on her — glowing in one cluster. She takes it all back by swimming into it; dimming again first, it is gone.
/// </summary>
public sealed class PlaneLostLight
{
    public Vector2 Position;
    public List<string> Pearls = new();
    public int Shells;
    public bool Taken;
}

/// <summary>
/// The open reef's places (docs/RPG-PROPOSAL.md §2): the regions (each a hand-drawn map; the first holds Lantern Bay in
/// its corner, her home water), and behind each region's cave mouths a leak cave, seeded from her own world seed.
/// Sealing a cave shuts its mouth; after ReefTuning.Recorrupt of her play time the Leak seeps back and the mouth opens
/// again on a fresh cave (mouth i holds caves i, i + mouths, i + 2·mouths…), so a region never runs out.
/// </summary>
public static class OpenReef
{
    /// <summary>The regions' maps in their order (a region's number is its place here, from 1).</summary>
    public static readonly string[] Regions = { "reef" };

    /// <summary>The region she wakes in: the one Lantern Bay lies in.</summary>
    public static string FirstRegion => Regions[0];

    public static int RegionNumber(string region) => Array.IndexOf(Regions, region) + 1;

    /// <summary>The cave behind a region's mouth now.</summary>
    public static int CaveBehind(Jelly jelly, string region, int mouth)
    {
        var caves = CavesOf(jelly, region);
        while (caves.Count <= mouth) caves.Add(caves.Count);
        return caves[mouth];
    }

    /// <summary>
    /// She sealed the cave behind this mouth: the mouth shuts (from her play time now), a fresh cave waits behind it for
    /// when it opens again, and the region counts one more.
    /// </summary>
    public static void Sealed(Jelly jelly, string region, int mouth, int mouths)
    {
        int now = CaveBehind(jelly, region, mouth);
        CavesOf(jelly, region)[mouth] = now + Math.Max(mouths, 1);
        jelly.Sealed[region] = SealedIn(jelly, region) + 1;
        if (!jelly.Shut.TryGetValue(region, out var shut)) jelly.Shut[region] = shut = new Dictionary<int, double>();
        shut[mouth] = jelly.PlaySeconds;
    }

    /// <summary>The region's mouths shut now (the cave behind each sealed, its corruption not yet back); those whose time is up open again.</summary>
    public static HashSet<int> ShutMouths(Jelly jelly, string region)
    {
        var closed = new HashSet<int>();
        if (!jelly.Shut.TryGetValue(region, out var shut)) return closed;
        foreach (var (mouth, at) in shut.ToList())
            if (jelly.PlaySeconds - at >= ReefTuning.Recorrupt) shut.Remove(mouth);
            else closed.Add(mouth);
        return closed;
    }

    public static int SealedIn(Jelly jelly, string region) => jelly.Sealed.TryGetValue(region, out int n) ? n : 0;

    /// <summary>The zone a region's mouth lies in (by its drawn point), if the region has zones.</summary>
    public static HandZone? ZoneOf(HandLayout hand, int mouth)
    {
        if (mouth < 0 || mouth >= hand.Mouths.Count) return null;
        var at = hand.Mouths[mouth];
        return hand.Zones.FirstOrDefault(z => at.X >= z.Box.X0 && at.X < z.Box.X1 && at.Y >= z.Box.Y0 && at.Y < z.Box.Y1);
    }

    /// <summary>
    /// The level of the cave behind a mouth (docs/REEF-WORLD.md §3): its zone's area level (the region's tier, without
    /// zones) plus the mouth's danger, fixed as an area's is. Without a mouth (-1), the region's least.
    /// </summary>
    public static int CaveLevel(HandLayout hand, int mouth) =>
        GreatOf(hand, mouth) is { } great ? great.Level
        : mouth < 0 || mouth >= hand.Mouths.Count
            ? hand.Zones.Count > 0 ? hand.Zones.Min(z => z.Level) : hand.Tier
            : (ZoneOf(hand, mouth)?.Level ?? hand.Tier) + Math.Clamp(hand.Mouths[mouth].Danger, 0, ReefTuning.MostDanger);

    /// <summary>How many floors the cave behind a mouth goes down (docs/REEF-WORLD.md §3): one more than its danger (a great cave's, its neighbour's).</summary>
    public static int CaveFloors(HandLayout hand, int mouth) =>
        GreatOf(hand, mouth) is { } great ? great.Floors
        : 1 + (mouth >= 0 && mouth < hand.Mouths.Count ? Math.Clamp(hand.Mouths[mouth].Danger, 0, ReefTuning.MostDanger) : 0);

    /// <summary>
    /// The neighbour waiting at the bottom of the great cave behind a mouth (docs/NPC-BOSSES-PROPOSAL.md), or null for a
    /// minor one.
    /// </summary>
    public static Neighbour? GreatOf(HandLayout hand, int mouth) =>
        mouth >= 0 && mouth < hand.Mouths.Count ? Folk.Get(hand.Mouths[mouth].Folk) : null;

    /// <summary>The region's minor mouths, the Tide Fan's to name: every one before the great caves', which come last.</summary>
    public static int MinorMouths(HandLayout hand) => hand.Mouths.TakeWhile(m => m.Folk is null).Count();

    /// <summary>
    /// A great cave is the same for every jelly (grown from the region's own seed, its own number past every minor
    /// cave's), and opens again never once its leak is sealed.
    /// </summary>
    public const int GreatCaves = 100000;

    public static int GreatCave(Neighbour n) => GreatCaves + Array.IndexOf(Folk.All, n);

    /// <summary>She sealed a great cave's leak: it is closed for good (its mouth shut forever).</summary>
    public static void SealedGreat(Jelly jelly, Neighbour n) => jelly.GreatLeaks.Add(n.Id);

    /// <summary>The region's mouths shut now: the minor ones sealed and not yet corrupted again, and the great ones closed for good.</summary>
    public static HashSet<int> ShutMouths(Jelly jelly, string region, HandLayout hand)
    {
        var shut = ShutMouths(jelly, region);
        for (int i = 0; i < hand.Mouths.Count; i++)
            if (GreatOf(hand, i) is { } n && jelly.GreatLeaks.Contains(n.Id)) shut.Add(i);
        return shut;
    }

    /// <summary>
    /// A cave's name (docs/REEF-WORLD.md §3), from its zone's own words, her world's seed and the cave's number: "The
    /// Weeping Grotto" in the Shallows, "The Flooded Hold" in the Galleon.
    /// </summary>
    public static string CaveName(string zone, string seed, int cave)
    {
        var (kinds, places) = CaveWords.TryGetValue(zone, out var words) ? words : CaveWords["shallows"];
        ulong h = 1469598103934665603UL;
        foreach (char c in $"{seed}:{zone}:{cave}") h = (h ^ c) * 1099511628211UL;
        return $"The {kinds[(int)(h % (ulong)kinds.Length)]} {places[(int)(h / 7 % (ulong)places.Length)]}";
    }

    /// <summary>Each zone's words for its caves: what kind of place, and what place.</summary>
    static readonly Dictionary<string, (string[] Kinds, string[] Places)> CaveWords = new()
    {
        ["shallows"] = (new[] { "Weeping", "Sunken", "Quiet", "Drowned", "Pale", "Still" }, new[] { "Grotto", "Burrow", "Cove", "Den", "Pocket", "Hollow" }),
        ["kelp"] = (new[] { "Tangled", "Hushed", "Knotted", "Shaded", "Choking", "Green" }, new[] { "Thicket", "Bower", "Warren", "Hollow", "Tangle", "Root-cave" }),
        ["galleon"] = (new[] { "Flooded", "Gilded", "Silent", "Broken", "Rotten", "Lost" }, new[] { "Hold", "Bilge", "Brig", "Magazine", "Galley", "Cabin" }),
        ["carnival"] = (new[] { "Grinning", "Painted", "Glittering", "Endless", "Laughing", "Hushed" }, new[] { "Hall", "Funhouse", "Tent", "Maze", "Stage", "Arcade" }),
    };

    /// <summary>The Menace of the cave behind a mouth: its zone's (the region's, without zones), and more for its danger.</summary>
    public static float CaveMenace(HandLayout hand, int mouth) =>
        (ZoneOf(hand, mouth)?.Menace ?? hand.Menace) + ReefTuning.MenacePerDanger * (GreatOf(hand, mouth) is { } great ? great.Floors - 1
            : mouth >= 0 && mouth < hand.Mouths.Count ? hand.Mouths[mouth].Danger : 0);

    /// <summary>A cave's own level id (apart from every run level and hand-drawn map), from its region, depth and number.</summary>
    public static LevelId CaveId(string region, int depth, int cave) => new(1000 + RegionNumber(region), depth, cave + 1);

    /// <summary>
    /// A place's key, for where her lost light lies: a region, or a region's cave by its number (a cave sealed since is
    /// gone, and the light with it).
    /// </summary>
    public static string PlaceKey(string kind, string region, int cave = -1, int floor = 1) =>
        kind == "cave" ? floor > 1 ? $"cave:{region}:{cave}:{floor}" : $"cave:{region}:{cave}" : $"region:{region}";

    static List<int> CavesOf(Jelly jelly, string region) =>
        jelly.Caves.TryGetValue(region, out var list) ? list : jelly.Caves[region] = new List<int>();
}

public sealed partial class PlaneWorld
{
    /// <summary>She is in a hand-drawn map's current station, where the ride starts.</summary>
    public bool AtStation => Map.CurrentStation && !Defeated && Vector2.Distance(Player.Position, Map.Exit.Position) < ReefTuning.StationReach;

    /// <summary>She is in a haven's water (Lantern Bay): nothing harms her there.</summary>
    public bool InHaven => Map.InHaven(Player.Position);

    bool _wasInHaven;

    /// <summary>She was set down somewhere new (not swum there): in the haven or out of it, nothing is news.</summary>
    public void SettleHaven() => _wasInHaven = InHaven;

    /// <summary>
    /// The haven's edge: swimming back into its water she is healed whole (HavenEntered), and leaving it she is out on the
    /// open reef again (HavenLeft).
    /// </summary>
    void StepHaven()
    {
        if (Map.Haven is null || Defeated) return;
        bool inside = InHaven;
        if (inside == _wasInHaven) return;
        _wasInHaven = inside;
        if (inside)
        {
            Run.Hp = Player.Hp = Run.MaxHp;
            Events.Add(new PlaneEvent(PlaneEventType.HavenEntered, Player.Position, Vector2.Zero));
        }
        else Events.Add(new PlaneEvent(PlaneEventType.HavenLeft, Player.Position, Vector2.Zero));
    }

    /// <summary>She is over the bay's mirror, where she changes her look (docs/RPG-PROPOSAL.md §8).</summary>
    public bool AtMirror => Map.Mirror is { } mirror && !Defeated && Vector2.Distance(Player.Position, mirror) < ReefTuning.MirrorReach;

    /// <summary>
    /// The region's mouths shut now (their caves sealed, not yet corrupted again): each opening stands closed with rock
    /// (Plugged), no way in (the game sets them from her jelly).
    /// </summary>
    public HashSet<int> ShutMouths { get; set; } = new();

    /// <summary>The region's open cave mouth she has swum into (its index), or −1.</summary>
    public int AtMouth()
    {
        if (Defeated) return -1;
        for (int i = 0; i < Map.Mouths.Count; i++)
            if (!ShutMouths.Contains(i) && Map.Mouths[i].Holds(Player.Position, ReefTuning.MouthIn)) return i;
        return -1;
    }

    /// <summary>
    /// The shut mouth whose rock a circle at p runs into (it closes the opening at its threshold): the way into it (the
    /// wall's uphill), or null.
    /// </summary>
    Vector2? Plug(Vector2 p, float radius)
    {
        foreach (int i in ShutMouths)
        {
            if (i >= Map.Mouths.Count) continue;
            var door = Map.Mouths[i];
            var (inward, off) = door.Measure(p);
            if (inward + radius > 0.1f && inward < Doorway.Depth + 1f && off < Doorway.HalfWidth + 1.8f) return door.Into;
        }
        return null;
    }

    bool _wentIn;

    /// <summary>Back at a leak cave's way out (at its threshold or in it), having been out into the cave.</summary>
    public bool AtCaveMouth
    {
        get
        {
            if (!Map.LeakCave || Defeated || Map.Way is not { } way) return false;
            if (Vector2.Distance(Player.Position, way.At) > ReefTuning.LeaveAfter) _wentIn = true;
            return _wentIn && way.Holds(Player.Position, -1f);
        }
    }

    /// <summary>
    /// Into a leak cave's way out with its leak sealed: she leaves the cave. Till then its current (PlaneCurrent.Holds)
    /// pushes her back in.
    /// </summary>
    public bool LeavingCave => AtCaveMouth && !CaveLeakOpen && Map.Way is { } way && way.Holds(Player.Position, ReefTuning.MouthIn);

    /// <summary>
    /// A leak cave's leak is open: on its deepest floor, till she seals it; on a floor above, always (it lies below, and
    /// she leaves a sealed cave by the upwelling where it was).
    /// </summary>
    public bool CaveLeakOpen => Map.LeakCave && (LeaksOpen > 0 || Map.HoleDown);

    /// <summary>In her Ink Cloud (the active): the Leak's creatures have lost her.</summary>
    public bool Unseen => Player.CloakTimer > 0f;

    /// <summary>On the zoned reef, too far from her to live just now (ReefTuning.Awake): it holds still where it is.</summary>
    bool Asleep(Vector2 at) => Map.Zones.Count > 0 && Vector2.DistanceSquared(at, Player.Position) > ReefTuning.Awake * ReefTuning.Awake;

    float _caveBud;

    /// <summary>The school a freed prime of a leak cave swallowed: chromis, healthy, bursting out of it.</summary>
    public const int SwallowedSchool = 18;

    /// <summary>
    /// A freed prime lets its swallowed school out: healthy chromis burst from where it was, scatter, and stream away
    /// toward the cave's way out, going home (gone once far off). Returns how many.
    /// </summary>
    int ReleaseSchool(Vector2 at)
    {
        int id = ReefFish.Count == 0 ? 0 : ReefFish.Max(f => f.Id) + 1;
        var home = SafeNormalize((Map.Way?.At ?? Map.Start.Position) - at);
        if (home == Vector2.Zero) home = -Vector2.UnitY;
        for (int i = 0; i < SwallowedSchool; i++)
        {
            float a = _blightRng.Range(0f, MathF.Tau), r = MathF.Sqrt(_blightRng.NextFloat()) * 1.2f;
            var from = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            if (!Clear(from, ReefFishTuning.ChromisRadius)) from = at;
            var dir = Rotate(home, _blightRng.Range(-0.5f, 0.5f));
            ReefFish.Add(new PlaneReefFish
            {
                Kind = ReefFishKind.Chromis, Id = id++, Position = from, Home = from, Heading = MathF.Atan2(dir.Y, dir.X), Freed = true, Leaving = dir,
                Velocity = new Vector2(MathF.Cos(a), MathF.Sin(a)) * _blightRng.Range(3f, 6f), Rng = new Rng(_blightRng.NextU64()),
            });
        }
        return SwallowedSchool;
    }

    /// <summary>The Tide Fan's requests: ash patches her light or shots have cleared on this level (not the sealed leak's wave).</summary>
    public int AshWashed { get; private set; }

    /// <summary>Experience given (an untied request's), paid with the step's own: shown and levelled as a leak's is.</summary>
    public void GiveXp(int xp) => _givenXp += Math.Max(xp, 0);
    int _givenXp;

    /// <summary>She is at the bay's Tide Fan (its foot), where requests are taken and untied.</summary>
    public bool AtFan => Map.Fan is { } fan && !Defeated && Vector2.Distance(Player.Position, fan) < RequestTuning.FanReach;

    /// <summary>
    /// The reef's packs (docs/REEF-WORLD.md §4): murklings roaming its zones' chambers in packs, more and bigger the
    /// farther the zone from the bay (ReefTuning.Packs), now and then led by a prime; none in the haven, by her start or
    /// at a cave's mouth.
    /// </summary>
    void PlaceReefPacks()
    {
        var sites = Map.Plazas.Select(z => z.Center).ToList();
        Shuffle(sites, _blightRng);
        foreach (var zone in Map.Zones)
        {
            var (packs, least, most, primeShare) = ReefTuning.Packs(zone.Look);
            int placed = 0;
            foreach (var site in sites)
            {
                if (placed >= packs) break;
                if (Map.ZoneAt(site) != Map.Zones.IndexOf(zone) || Map.InHaven(site, ReefTuning.PackClear) || Vector2.Distance(site, Map.Start.Position) < ReefTuning.PackClear * 2f
                    || Map.Mouths.Any(m => Vector2.Distance(m.At, site) < ReefTuning.PackClear) || !Clear(site, BlightTuning.PrimeBody)) continue;
                placed++;
                bool prime = _blightRng.NextFloat() < primeShare;
                if (prime) Bud(site, -1, prime: true, drink: false, born: true);
                int n = least + _blightRng.Int(most - least + 1);
                for (int k = 0; k < n; k++)
                {
                    float a = _blightRng.Range(0f, MathF.Tau);
                    var at = site + new Vector2(MathF.Cos(a), MathF.Sin(a)) * _blightRng.Range(prime ? 3.5f : 1.5f, prime ? 6f : 4f);
                    if (Clear(at, BlightTuning.MurkRadius)) Bud(at, -1, drink: false, born: true);
                }
            }
        }
    }

    /// <summary>A leak cave's murklings at its start: a few along its path, and a prime short of its leak.</summary>
    void PlaceCaveMurklings()
    {
        var path = LevelValidator.ShortestPath(Map, Map.Start.Position, Map.Exit.Position, clearance: 1f);
        if (path.Count == 0) return;
        // The leak, on the deepest floor, is guarded (a floor above holds only its hole down; a great cave's by its boss).
        var guard = Map.HoleDown || Map.HasBoss ? default : path.LastOrDefault(q => Vector2.Distance(q, Map.Exit.Position) >= ReefTuning.CavePrimeBeforeLeak && Clear(q, BlightTuning.PrimeBody));
        if (guard != default) Bud(guard, -1, prime: true, drink: false, born: true);
        int want = ReefTuning.CaveMurklings + (int)MathF.Round(ReefTuning.CaveMurklingsPerMenace * Menace);
        var spots = path.Where(q => Vector2.Distance(q, Map.Start.Position) >= ReefTuning.CaveMurkFromStart).ToList();
        Shuffle(spots, _blightRng);
        int placed = 0;
        foreach (var on in spots)
        {
            if (placed >= want) break;
            // A little off the path, where there is room.
            var at = on + new Vector2(_blightRng.Range(-2f, 2f), _blightRng.Range(-2f, 2f));
            if (!Clear(at, BlightTuning.MurkRadius)) at = on;
            if (!Clear(at, BlightTuning.MurkRadius) || Murklings.Any(m => Vector2.Distance(m.Position, at) < ReefTuning.CaveMurkSpacing)) continue;
            Bud(at, -1, drink: false, born: true);
            placed++;
        }
        _caveBud = ReefTuning.CaveBudEvery * 0.5f;
    }

    /// <summary>While its leak is open, a leak cave buds murklings out of its ink near her (and now and then a prime).</summary>
    void StepCaveMurklings(InkDots dots)
    {
        // (A great cave's boss fight is hers and the boss's alone.)
        if (!CaveLeakOpen || Defeated || BossBattle) return;
        _caveBud -= Dt;
        if (_caveBud > 0f) return;
        _caveBud = ReefTuning.CaveBudEvery / (1f + 0.6f * Menace);
        int cap = ReefTuning.CaveCap + (int)MathF.Round(ReefTuning.CaveCapPerMenace * Menace);
        var her = Player.Position;
        if (Murklings.Count(m => m.Alive && Vector2.Distance(m.Position, her) < ReefTuning.CaveCapReach) >= cap || dots.Live.Count == 0) return;
        for (int tries = 0; tries < 16; tries++)
        {
            var at = InkDots.Centre(dots.Live[_blightRng.Int(dots.Live.Count)]);
            float d = Vector2.Distance(at, her);
            if (d < ReefTuning.CaveBudNear || d > ReefTuning.CaveBudFar || !Clear(at, BlightTuning.MurkRadius)) continue;
            bool prime = _primesBorn < BlightTuning.PrimePerLevel && !Murklings.Any(x => x.Alive && x.Prime)
                && _blightRng.NextFloat() < BlightTuning.PrimeChance + BlightTuning.PrimeChancePerMenace * Menace
                && d > BlightTuning.PrimeClearance && Clear(at, BlightTuning.PrimeBody);
            Bud(at, -1, prime, drink: false);
            // It is made of the cave's ink: the dots round it go into it (a prime's twelve murklings' worth).
            float r = BlightTuning.GatherRadius * (prime ? MathF.Sqrt(BlightTuning.PrimeInk / BlightTuning.Ink) : 1f);
            foreach (int cell in dots.Live.Where(c => Vector2.Distance(InkDots.Centre(c), at) < r).ToList()) dots.Remove(cell);
            return;
        }
    }

    /// <summary>Her lost light, if it lies in this place (the game puts it here from her jelly).</summary>
    public PlaneLostLight? Lost { get; set; }

    /// <summary>She swims into her lost light: her pearls are hers again (absorbed at once), and her shells.</summary>
    void StepLostLight()
    {
        if (Lost is not { Taken: false } lost || Defeated || Vector2.Distance(Player.Position, lost.Position) > ReefTuning.LostReach) return;
        lost.Taken = true;
        foreach (string id in lost.Pearls) Run.Add(id);
        Run.Shells += lost.Shells;
        Player.Hp = MathF.Min(Player.Hp, Run.MaxHp);
        Events.Add(new PlaneEvent(PlaneEventType.LostLightTaken, lost.Position, Vector2.Zero, lost.Pearls.Count));
    }

    /// <summary>The depth whose life fills the water: the open reef's places have their region's fish whatever its depth.</summary>
    int LifeDepth => Map.LeakCave || Map.Station ? Math.Max(Map.Depth, AdventureTuning.BaseHome) : Map.Depth;
}
