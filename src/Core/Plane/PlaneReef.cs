using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>The open reef's numbers (docs/archive/RPG-PROPOSAL.md): first guesses, tuned in play.</summary>
public static class ReefTuning
{
    /// <summary>How far into a cave's opening (past its threshold, Doorway) she must swim to be taken through it.</summary>
    public const float MouthIn = 1.4f;
    /// <summary>How near the bay mirror's middle she must swim for it to show her (metres: well inside its pool).</summary>
    public const float MirrorReach = 1.3f;
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
    public static int LeakXp(int caveLevel) => (int)MathF.Round(LeakXpBase * MathF.Pow(Math.Max(caveLevel, 1), LeakXpPower));

    /// <summary>A pearl's worth in silver shells at the shell shop: PearlWorthBase + PearlWorthPerQuality × its quality.</summary>
    public const int PearlWorthBase = 20, PearlWorthPerQuality = 20;
    public static int PearlWorth(int quality) => PearlWorthBase + PearlWorthPerQuality * Math.Max(quality, 0);

}

public sealed partial class PlaneWorld
{
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

    /// <summary>She is over the bay's mirror, where she changes her look (docs/GAME.md §9).</summary>
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

    /// <summary>
    /// A leak cave's leak is open: on its deepest floor, till she seals it; on a floor above, always (it lies below, and
    /// she leaves a sealed cave by the upwelling where it was).
    /// </summary>
    public bool CaveLeakOpen => Map.LeakCave && (LeaksOpen > 0 || Map.HoleDown);

    /// <summary>In her Ink Cloud (the active): the Leak's creatures have lost her.</summary>
    public bool Unseen => Player.CloakTimer > 0f || Hidden;

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
            BudRoster(at, prime: false, born: true);
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
            BudRoster(at, prime, born: false);
            // It is made of the cave's ink: the dots round it go into it (a prime's twelve murklings' worth).
            float r = BlightTuning.GatherRadius * (prime ? MathF.Sqrt(BlightTuning.PrimeInk / BlightTuning.Ink) : 1f);
            foreach (int cell in dots.Live.Where(c => Vector2.Distance(InkDots.Centre(c), at) < r).ToList()) dots.Remove(cell);
            return;
        }
    }

    /// <summary>The depth whose life fills the water: the open reef's places have their region's fish whatever its depth.</summary>
    int LifeDepth => Map.LeakCave || Map.Station ? Math.Max(Map.Depth, AdventureTuning.BaseHome) : Map.Depth;
}
