using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>The adventure's numbers (docs/ADVENTURE.md): first guesses, tuned in play.</summary>
public static class AdventureTuning
{
    /// <summary>
    /// The ink dots lie on an even grid this far apart, each this big, over a seeded level's square (the leaks' levels
    /// and caves; the open reef's big map has none); at most MaxDots on a level.
    /// </summary>
    public const float Spacing = 0.8f, DotRadius = 0.135f;
    public const int Cells = (int)(LevelMap.Seeded / Spacing), MaxDots = 9000;
    /// <summary>A dot her light has worn down (but not cleared) fills back at this rate (strength a second) once out of it.</summary>
    public const float Regrow = 0.6f;
    /// <summary>Her shots' pops and the Starburst take this many times their ground strength off a dot.</summary>
    public const float PopBite = 1.6f, BurstBite = 2f;
    /// <summary>
    /// The leaks: LevelLeaks a level, in the middle of its chambers, LeakSpacing apart and LeakFromStart from her start
    /// (closer if the level has no room). Sealing LeaksToClear of them opens the way on and wakes the main current.
    /// </summary>
    public const int LevelLeaks = 5, LeaksToClear = 3;
    public const float LeakSpacing = 30f, LeakFromStart = 24f;
    /// <summary>No creature comes nearer a leak's disc than LeakClear (it does not push: she must come in among its arms).</summary>
    public const float LeakClear = 2.5f;
    /// <summary>
    /// A leak is a brittle star (docs/ADVENTURE.md §1): a disc LeakDisc across and five arms, each LeakArmBase +
    /// LeakArmPerMenace × Menace long (Menace up to 1), tapering from LeakArmRoot to LeakArmTip (half widths).
    /// </summary>
    public const int LeakArms = 5;
    public const float LeakDisc = 0.45f, LeakArmBase = 2.5f, LeakArmPerMenace = 3.5f, LeakArmRoot = 0.14f, LeakArmTip = 0.04f;
    /// <summary>
    /// Sealing: her inner light does it, no shots, while she stays within LeakSealReach of its disc. It staples its arms
    /// shut one after another, from the disc out, a staple every StapleSpacing of an arm (at least MinStaples an arm),
    /// one every StapleSeconds; the last staple seals it. LeakHold seconds after she leaves it heals, a staple snapping
    /// every StapleDrain. Each sealed is worth LeakXp, and throws LeakShells shells out of it.
    /// </summary>
    public const int LeakXp = 25, LeakShells = 5, MinStaples = 3;
    public const float StapleSpacing = 0.6f, StapleSeconds = 0.5f, StapleDrain = 0.4f, LeakSealReach = 6f, LeakHold = 1.5f;
    /// <summary>
    /// Its arms float and wiggle: each joint bends up to LeakWiggle (radians, more toward the tip) on a slow wave
    /// (LeakWave radians a second, LeakWaveJoint further along each joint). A stapled stretch cannot move. Touching an
    /// unstapled stretch stings her for LeakSting and throws her off at LeakKnock; a shot that meets one pops on it and
    /// bends the arm away by LeakKick (radians a second), sprung back (LeakSpring, LeakDamp).
    /// </summary>
    public const float LeakWiggle = 0.13f, LeakWave = 0.7f, LeakWaveJoint = 0.9f, LeakEase = 1.5f;
    public const float LeakSting = 6f, LeakKnock = 7f, LeakKick = 2.4f, LeakSpring = 6f, LeakDamp = 3.5f;

    /// <summary>A leak's arms' length at this Menace.</summary>
    public static float LeakArmLength(float menace) => LeakArmBase + LeakArmPerMenace * Math.Clamp(menace, 0f, 1f);

    /// <summary>Staples on each arm of this length.</summary>
    public static int StaplesPerArm(float length) => Math.Max(MinStaples, (int)MathF.Round(length / StapleSpacing));

    /// <summary>Her first coming near rouses the fish within RouseReach of it; each arm stapled shut makes it spit LeakSpit drops at LeakSpitSpeed, LeakSpitHit of a throw's damage each.</summary>
    public const float RouseReach = 14f, LeakSpitSpeed = 5f, LeakSpitHit = 0.6f;
    public const int LeakSpit = 8;
    /// <summary>Sealed, its ink dissolves outward from it at this speed (m/s).</summary>
    public const float DissolveSpeed = 12f;
    /// <summary>Ink cleared from an open leak's water stays gone this long, then comes back one cell out from the leak every ReturnEvery ticks.</summary>
    public const float ReturnHold = 1f;
    public const int ReturnEvery = 12;
    /// <summary>
    /// The longer a level's leaks have been pouring, the deeper it is: it starts with StartInk of the water the ink can
    /// reach already inked (15% at Depth 1, 95% at Depth 6), grown out of its leaks' mouths, its edge ragged by PuddleRagged.
    /// </summary>
    public const float StartShare = 0.15f, StartPerMenace = 0.8f, StartMax = 0.95f, PuddleRagged = 0.45f;

    /// <summary>The share of a level's water that starts inked at this Menace.</summary>
    public static float StartInk(float menace) => MathF.Min(StartShare + StartPerMenace * menace, StartMax);

    /// <summary>
    /// A fish's gauge: it fills FishFill a second with a dot within FishSense, FishEat for each dot it eats, and drains
    /// FishDrain a second over clean water. Full, the fish turns.
    /// </summary>
    public const float FishSense = 1f, FishFill = 0.12f, FishEat = 0.2f, FishDrain = 0.05f;
    /// <summary>A spiked murkling's throw striking a fish fills its gauge (and those in the splash) this much.</summary>
    public const float ThrowFill = 0.6f;
    /// <summary>A pufferfish's gauge: PufferFill a second with a dot within PufferSense (and FishEat a dot it eats); full, it is an Inkwarden. PufferMin–Max a level.</summary>
    public const float PufferSense = 1.4f, PufferFill = 0.06f, PufferDrain = 0.03f;
    public const int PufferMin = 12, PufferMax = 18;
    /// <summary>Fish: this many times the depth's count of anemones, tangs and schools; schools of SchoolMin–SchoolMax.</summary>
    public const float FishMore = 3f;
    public const int SchoolMin = 12, SchoolMax = 24;

    /// <summary>
    /// Each fish kind has its own depth: there it lives in its full numbers, above it not at all, and below it at
    /// EarlierRate of them (the deeper depths keep the earlier fish, fewer). The Shallows' fish (the reef fish and the
    /// pufferfish) count the Kelp Jungle, where runs start, as their own too: BaseHome.
    /// </summary>
    public const float EarlierRate = 0.6f;
    public const int BaseHome = 2;

    /// <summary>How many of a fish whose own depth is <paramref name="home"/> live at <paramref name="depth"/>, as a share of its full numbers.</summary>
    public static float Rate(int home, int depth) => depth < home ? 0f : depth == home ? 1f : EarlierRate;

    /// <summary>Spiked murkling (a blue tang): keeps KeepMin–KeepMax metres from her at SpikedSpeed; a touch hurts and knocks her off at SpikedKnock.</summary>
    public const float KeepMin = 6f, KeepMax = 9f, SpikedSpeed = 3.4f, SpikedKnock = 7f, SpikedRadius = 0.38f, SpikedSense = 16f;
    /// <summary>Tadpole murkling (a clownfish): this small, chasing at TadpoleSpeed times her cruise.</summary>
    public const float TadpoleRadius = 0.22f, TadpoleSpeed = 1.1f, TadpoleSense = 16f;
    /// <summary>Shoal murkling (a chromis): very fast, on a ring ShoalRing (+ a layer) round her, circling her at ShoalOrbit radians a second.</summary>
    public const float ShoalSpeed = 9f, ShoalRing = 1.8f, ShoalLayer = 0.4f, ShoalRadius = 0.2f, ShoalSense = 14f, ShoalOrbit = 1.1f;

    /// <summary>
    /// Her oral arm: ArmBase metres long at ArmHp max HP, in proportion for more or less (between ArmMin and ArmMax).
    /// trails her along the way she swam (her last ArmMemory seconds of it, so it gathers in when she stops). A
    /// corrupted creature it touches (within ArmWidth of it) takes ArmShare of a shot's damage, once every ArmEvery.
    /// </summary>
    public const float ArmBase = 3.2f, ArmHp = 100f, ArmMin = 1.5f, ArmMax = 6.5f, ArmMemory = 1.5f, ArmWidth = 0.14f, ArmShare = 0.3f, ArmEvery = 0.4f;

    /// <summary>Her oral arm's length after so many skill points spent.</summary>
    public static float ArmLength(float maxHp) => System.Math.Clamp(ArmBase * maxHp / ArmHp, ArmMin, ArmMax);
}

/// <summary>
/// The ink in the water (docs/ADVENTURE.md §2): round dots on an even grid at her swim plane, each with its strength
/// (1 whole, worn down by her light; gone at 0). Cells in rock and in safe places never hold one; Links says which of a
/// cell's four neighbours the ink may divide into (no rock between).
/// </summary>
public sealed class InkDots
{
    public const int N = AdventureTuning.Cells;
    const float S = AdventureTuning.Spacing;

    /// <summary>Strength per cell (0: no dot), row-major.</summary>
    public readonly float[] Ink = new float[N * N];
    /// <summary>Where a dot may lie, and which neighbours it reaches (bits: east, south, west, north).</summary>
    public readonly bool[] Open = new bool[N * N];
    public readonly byte[] Links = new byte[N * N];
    /// <summary>The dots, in the order they came (each one's place in it in Slot).</summary>
    public readonly List<int> Live = new();
    readonly int[] _slot = new int[N * N];
    /// <summary>The step each cell was last in her light (it does not regrow then).</summary>
    readonly long[] _lit = new long[N * N];
    /// <summary>
    /// The water the ink holds: each cell's leak (−1: none), the cell it reached this one from (−1: the leak's own), and how
    /// far it lies from the leak through the water (m). What is cleared there comes back along that way while the leak is open.
    /// </summary>
    public readonly int[] Source = new int[N * N], Parent = new int[N * N];
    public readonly float[] Far = new float[N * N];
    public readonly List<int> Claimed = new();
    /// <summary>The step each cell lost its dot; this step (kept up by the world); a count of every change, for the view.</summary>
    public readonly long[] GoneAt = new long[N * N];
    public long Now;
    public long Version;

    public static readonly (int Dx, int Dy)[] Steps = { (1, 0), (0, 1), (-1, 0), (0, -1) };

    public int Count => Live.Count;

    public static Vector2 Centre(int i) => new((i % N + 0.5f) * S, (i / N + 0.5f) * S);

    public static int CellOf(Vector2 p)
    {
        int x = (int)MathF.Floor(p.X / S), y = (int)MathF.Floor(p.Y / S);
        return x < 0 || y < 0 || x >= N || y >= N ? -1 : y * N + x;
    }

    public bool Has(int i) => i >= 0 && Ink[i] > 0f;

    public static InkDots ForMap(LevelMap map)
    {
        var dots = new InkDots();
        Array.Fill(dots.Source, -1);
        Array.Fill(dots.Parent, -1);
        var safes = CorruptionField.SafeOf(map).ToList();
        for (int i = 0; i < N * N; i++)
        {
            var c = Centre(i);
            if (!map.IsOpen(c)) continue;
            bool clear = true;
            for (int k = 0; k < 4 && clear; k++)
                clear = map.IsOpen(c + new Vector2(Steps[k].Dx, Steps[k].Dy) * 0.3f);
            if (!clear || safes.Any(s => Vector2.Distance(c, s.Centre) < s.Radius + 2f)) continue;
            dots.Open[i] = true;
        }
        for (int i = 0; i < N * N; i++)
        {
            if (!dots.Open[i]) continue;
            int x = i % N, y = i / N;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Steps[k].Dx, ny = y + Steps[k].Dy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N || !dots.Open[ny * N + nx]) continue;
                if (map.IsOpen((Centre(i) + Centre(ny * N + nx)) * 0.5f)) dots.Links[i] |= (byte)(1 << k);
            }
        }
        return dots;
    }

    /// <summary>The neighbour of a cell one step along k (−1 when the ink cannot reach it).</summary>
    public int Neighbour(int i, int k) => (Links[i] & (1 << k)) == 0 ? -1 : i + Steps[k].Dx + Steps[k].Dy * N;

    public bool Add(int i)
    {
        if (i < 0 || !Open[i] || Ink[i] > 0f || Live.Count >= AdventureTuning.MaxDots) return false;
        Ink[i] = 1f;
        Version++;
        _slot[i] = Live.Count;
        Live.Add(i);
        return true;
    }

    public void Remove(int i)
    {
        if (i < 0 || Ink[i] <= 0f) return;
        Ink[i] = 0f;
        GoneAt[i] = Now;
        Version++;
        int at = _slot[i], last = Live[^1];
        Live[at] = last;
        _slot[last] = at;
        Live.RemoveAt(Live.Count - 1);
    }

    /// <summary>
    /// Light on the dots round a point: strength off each (full at the centre, a third at the rim). Those it empties are
    /// gone and listed in <paramref name="cleared"/>; the rest do not regrow this step.
    /// </summary>
    public void Light(Vector2 at, float radius, float strength, long tick, List<Vector2> cleared)
    {
        Span(at, radius, out int x0, out int y0, out int x1, out int y1);
        float r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            if (Ink[i] <= 0f) continue;
            float d2 = Vector2.DistanceSquared(Centre(i), at);
            if (d2 > r2) continue;
            float k = 1f - d2 / r2;
            Ink[i] -= strength * (0.35f + 0.65f * k);
            _lit[i] = tick;
            if (Ink[i] > 0f) continue;
            Ink[i] = 1f;
            Remove(i);
            cleared.Add(Centre(i));
        }
    }

    /// <summary>Worn dots out of her light fill back.</summary>
    public void Regrow(long tick, float step)
    {
        foreach (int i in Live)
            if (Ink[i] < 1f && _lit[i] != tick) Ink[i] = MathF.Min(1f, Ink[i] + step);
    }

    /// <summary>The cells a disc covers (clamped to the grid).</summary>
    public static void Span(Vector2 at, float radius, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Math.Max(0, (int)((at.X - radius) / S));
        y0 = Math.Max(0, (int)((at.Y - radius) / S));
        x1 = Math.Min(N - 1, (int)((at.X + radius) / S));
        y1 = Math.Min(N - 1, (int)((at.Y + radius) / S));
    }

    /// <summary>The cell of open water nearest a point (−1: none near).</summary>
    public int Nearest(Vector2 at)
    {
        int best = -1;
        float bestD = float.MaxValue;
        Span(at, 3f, out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            float d = Vector2.DistanceSquared(Centre(i), at);
            if (!Open[i] || d >= bestD) continue;
            bestD = d;
            best = i;
        }
        return best;
    }

    /// <summary>The nearest dot within a reach of a point (−1: none).</summary>
    public int Nearest(Vector2 at, float reach)
    {
        Span(at, reach, out int x0, out int y0, out int x1, out int y1);
        int best = -1;
        float bestD = reach * reach;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * N + x;
            if (Ink[i] <= 0f) continue;
            float d2 = Vector2.DistanceSquared(Centre(i), at);
            if (d2 > bestD) continue;
            bestD = d2;
            best = i;
        }
        return best;
    }
}

/// <summary>
/// A leak (docs/ADVENTURE.md §1): a brittle star of ink floating at her plane, its disc where the ink comes through, its
/// five arms adrift round it. The only thing that heals: her light staples its arms still one after another, and left
/// alone it pulls the staples out again.
/// </summary>
public sealed class PlaneLeak
{
    public Vector2 Position;
    /// <summary>The seconds of her light it has taken, and those it needs (a staple every StapleSeconds).</summary>
    public float Light, Needed;
    /// <summary>Its arms, and the staples each takes.</summary>
    public LeakArm[] Arms = Array.Empty<LeakArm>();
    public int StaplesPerArm;
    /// <summary>An Inkwarden guards it this moment.</summary>
    public bool Warded;
    public bool Sealed;
    /// <summary>Shut by the boss as it rose, not by her.</summary>
    public bool Drunk;
    /// <summary>She has come near it (it roused the fish round it).</summary>
    public bool Roused;
    /// <summary>Since she last lit it (it heals once LeakHold has passed).</summary>
    public float Since;
    /// <summary>Sealed: how far out from it the ink has dissolved (negative: none under way).</summary>
    public float Wave = -1f;
    /// <summary>How full of her light it is, 0..1.</summary>
    public float Glow => Needed <= 0f ? 1f : Light / Needed;
    public int StapleCount => Arms.Length * StaplesPerArm;
    /// <summary>The staples in now: all of them once sealed.</summary>
    public int Staples => Sealed ? StapleCount : Math.Min((int)(Light / AdventureTuning.StapleSeconds + 1e-4f), StapleCount);
    /// <summary>How many of an arm's staples are in (its arms are stapled one after another, from the disc out).</summary>
    public int StapledOn(int arm) => Math.Clamp(Staples - arm * StaplesPerArm, 0, StaplesPerArm);
    /// <summary>Its arms stapled shut all along.</summary>
    public int ArmsShut => Staples / Math.Max(StaplesPerArm, 1);
}

/// <summary>
/// One arm of a leak: its way out from the disc, its joints' bends (one a staple's stretch; a stapled one holds still),
/// where its joints lie now (the disc's rim, then each joint out to the tip), and how a shot has bent it.
/// </summary>
public sealed class LeakArm
{
    public float Angle, Seed, Length;
    public float[] Bends = Array.Empty<float>();
    public Vector2[] Points = Array.Empty<Vector2>();
    public float Kick, KickSpeed;

    /// <summary>Its half width this far along it (0 at the disc, 1 at the tip).</summary>
    public static float HalfWidth(float along) => AdventureTuning.LeakArmRoot + (AdventureTuning.LeakArmTip - AdventureTuning.LeakArmRoot) * along;
}

public sealed partial class PlaneWorld
{
    /// <summary>The ink dots in the water (the adventure; null otherwise).</summary>
    public InkDots? Dots { get; private set; }
    public List<PlaneLeak> Leaks { get; } = new();
    public int LeaksOpen => Leaks.Count(v => !v.Sealed);
    /// <summary>How many more leaks she must seal before the way on opens.</summary>
    public int LeaksLeft => Math.Max(Math.Min(AdventureTuning.LeaksToClear, Leaks.Count) - (Leaks.Count - LeaksOpen), 0);

    Rng _inkRng = null!;
    readonly List<Vector2> _cleared = new();

    /// <summary>The adventure's level: the dots' grid, the leaks, the way down shut until they are sealed (and its boss freed).</summary>
    void PlaceAdventure()
    {
        _inkRng = new Rng(Map.Seed ^ 0x1DC0757UL ^ ((ulong)Map.Level << 21) ^ ((ulong)Map.Depth << 29) ^ ((ulong)Map.Attempt << 45));
        Dots = InkDots.ForMap(Map);
        PlaceLeaks();
        PlaceCurrents();
        PlaceInk(Dots);
        if (Map.LeakCave) PlaceCaveMurklings();
        ExitOpen = Leaks.Count == 0 && Boss is null;
    }

    /// <summary>
    /// The leaks, in the middle of the chambers: failing those, on the side routes; failing those, in any open water; and
    /// only as a last resort on the main current. Far apart if the level allows it, closer if it must.
    /// </summary>
    void PlaceLeaks()
    {
        // A hand-drawn map holds none of its own; a leak cave holds one, at the dead end where a way down would be.
        if (Map.Station) return;
        if (Map.LeakCave)
        {
            // (A floor above its deepest holds a hole down there instead.)
            if (!Map.HoleDown) Leaks.Add(NewLeak(Map.Exit.Position));
            return;
        }
        // Where she can swim with the boss's walls shut: they wait for the leaks, so none may lie behind them.
        var bossWalls = _rocks.Where(r => r.Kind == SealKind.Boss).ToList();
        var reach = LevelValidator.Distances(Map, Map.Start.Position, blocked: q => bossWalls.Exists(r => r.Contains(q)));
        var safes = SafePlaces().ToList();
        var chambers = Map.Plazas.Select(z => z.Center).ToList();
        var sides = new List<Vector2>();
        foreach (var c in Map.Corridors.Where(c => c.Kind == CorridorKind.Side))
            for (int i = 0; i < c.Points.Count; i += 3) sides.Add(c.Points[i]);
        var anywhere = new List<Vector2>();
        for (int y = 2; y < Map.Size; y += 3)
            for (int x = 2; x < Map.Size; x += 3) anywhere.Add(new Vector2(x, y));
        foreach (var spots in new[] { chambers, sides, anywhere })
        {
            Shuffle(spots, _inkRng);
            PlaceLeaks(spots, reach, safes, false);
        }
        PlaceLeaks(anywhere, reach, safes, true);
    }

    /// <summary>A leak here: its arms the length its Menace gives, each its own way round (a fifth of a turn apart) and its own wave.</summary>
    PlaneLeak NewLeak(Vector2 at)
    {
        float length = AdventureTuning.LeakArmLength(Menace);
        int per = AdventureTuning.StaplesPerArm(length);
        var v = new PlaneLeak { Position = at, StaplesPerArm = per, Needed = AdventureTuning.StapleSeconds * per * AdventureTuning.LeakArms };
        float turn = _inkRng.Range(0f, MathF.Tau);
        v.Arms = new LeakArm[AdventureTuning.LeakArms];
        for (int i = 0; i < v.Arms.Length; i++)
            v.Arms[i] = new LeakArm
            {
                Angle = turn + i * MathF.Tau / AdventureTuning.LeakArms + _inkRng.Range(-0.12f, 0.12f), Seed = _inkRng.Range(0f, 100f),
                Length = length, Bends = new float[per], Points = new Vector2[per + 1],
            };
        LayArms(v);
        return v;
    }

    /// <summary>Opens leaks at these spots, until the level has its number of them.</summary>
    void PlaceLeaks(List<Vector2> spots, float[] reach, List<(Vector2 Centre, float Radius)> safes, bool lastResort)
    {
        int want = AdventureTuning.LevelLeaks;
        for (float spacing = AdventureTuning.LeakSpacing; spacing > 3.5f && Leaks.Count < want; spacing *= 0.75f)
            foreach (var at in spots)
            {
                if (Leaks.Count >= want) break;
                if (!Clear(at, 1f) || float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, at))) continue;
                if (Vector2.Distance(at, Map.Start.Position) < (lastResort ? MathF.Min(AdventureTuning.LeakFromStart, spacing) : AdventureTuning.LeakFromStart)) continue;
                if (Vector2.Distance(at, Map.Exit.Position) < Map.Exit.Radius + 6f || Map.Shaft.Contains(at, 4f)) continue;
                if (safes.Any(s => Vector2.Distance(at, s.Centre) < s.Radius + 4f)) continue;
                if (Leaks.Any(v => Vector2.Distance(v.Position, at) < spacing)) continue;
                if (!lastResort && MainCurrent.Touches(Map, at, 0f)) continue;
                Leaks.Add(NewLeak(at));
            }
    }

    /// <summary>
    /// The ink a level starts with (AdventureTuning.StartInk of its water): grown out of every leak at once, neighbour to
    /// neighbour through open water only, the nearest first, its edge ragged (each step stretched by up to PuddleRagged at
    /// random). It has had years: it has got through the sealed walls and behind them. Each cell belongs to the leak that
    /// reached it, and keeps the way it came.
    /// </summary>
    void PlaceInk(InkDots dots)
    {
        var reach = new PriorityQueue<int, float>();
        var seen = new HashSet<int>();
        var water = new List<int>();
        for (int k = 0; k < Leaks.Count; k++)
        {
            int cell = dots.Nearest(Leaks[k].Position);
            if (cell < 0 || !seen.Add(cell)) continue;
            dots.Source[cell] = k;
            reach.Enqueue(cell, 0f);
        }
        while (reach.TryDequeue(out int cell, out float far))
        {
            water.Add(cell);
            dots.Far[cell] = far * AdventureTuning.Spacing;
            for (int k = 0; k < 4; k++)
            {
                int next = dots.Neighbour(cell, k);
                if (next < 0 || !seen.Add(next)) continue;
                dots.Source[next] = dots.Source[cell];
                dots.Parent[next] = cell;
                reach.Enqueue(next, far + 1f + AdventureTuning.PuddleRagged * _inkRng.NextFloat());
            }
        }
        float share = Map.LeakCave ? MathF.Max(ReefTuning.CaveInk, AdventureTuning.StartInk(Menace)) : AdventureTuning.StartInk(Menace);
        int size = Math.Min((int)MathF.Round(share * dots.Open.Count(o => o)), water.Count);
        for (int i = 0; i < size; i++)
        {
            dots.Add(water[i]);
            dots.Claimed.Add(water[i]);
        }
        // What lies past the ink's edge is no one's.
        for (int i = size; i < water.Count; i++) dots.Source[water[i]] = -1;
    }

    /// <summary>The adventure's turn, after the war's: leaks pour, the ink spreads and trails, fish and anemones fill.</summary>
    void StepAdventure()
    {
        StepArm();
        if (Dots is not { } dots) return;
        dots.Now = Tick;
        StepLeaks(dots);
        StepReturn(dots);
        StepGauges(dots);
        StepFlounders(dots);
        StepCaveMurklings(dots);
        dots.Regrow(Tick, AdventureTuning.Regrow * Dt);
    }

    // ───────────────────────── the leaks ─────────────────────────

    void StepLeaks(InkDots dots)
    {
        for (int k = 0; k < Leaks.Count; k++)
        {
            var v = Leaks[k];
            if (v.Wave >= 0f) Dissolve(dots, v, k);
            StepArms(v);
            if (v.Sealed) continue;
            // Near it, her inner light works on it on its own (an Inkwarden in reach turns it aside, and a great cave's
            // boss lying over it until she is freed).
            if (Tick % 15 == 0) v.Warded = Ward(v, v.Position, 1f) <= 0f;
            if (Boss is { Freed: false }) v.Warded = true;
            int staples = v.Staples, shut = v.ArmsShut;
            if (!Defeated && !v.Warded && Vector2.Distance(Player.Position, v.Position) <= AdventureTuning.LeakSealReach)
            {
                if (!v.Roused) Rouse(v);
                v.Since = 0f;
                v.Light += Dt;
                if (v.Light >= v.Needed)
                {
                    SealLeak(v);
                    continue;
                }
                if (v.Staples <= staples) continue;
                // A staple in, where it lands; an arm stapled shut all along spits.
                Events.Add(new PlaneEvent(PlaneEventType.LeakTied, StapleAt(v, v.Staples - 1), Vector2.UnitY, v.Staples));
                if (v.ArmsShut > shut) Spit(v);
                continue;
            }
            if (v.Light <= 0f) continue;
            // Left alone it heals, its staples snapping out the way they went in, last first.
            v.Since += Dt;
            if (v.Since < AdventureTuning.LeakHold) continue;
            v.Light = MathF.Max(0f, v.Light - AdventureTuning.StapleSeconds / AdventureTuning.StapleDrain * Dt);
            if (v.Staples < staples) Events.Add(new PlaneEvent(PlaneEventType.LeakUntied, StapleAt(v, staples - 1), Vector2.UnitY, v.Staples));
        }
        StepArmContact();
    }

    /// <summary>Where a leak's staple (by its place in order) sits: the middle of its arm's stretch.</summary>
    public static Vector2 StapleAt(PlaneLeak v, int staple)
    {
        int per = Math.Max(v.StaplesPerArm, 1);
        var arm = v.Arms[Math.Clamp(staple / per, 0, v.Arms.Length - 1)];
        int j = staple % per;
        return (arm.Points[j] + arm.Points[j + 1]) * 0.5f;
    }

    /// <summary>
    /// Its arms float and wiggle: each joint not stapled eases toward its wave (and the bend a shot gave it); a stapled
    /// joint holds where it was stapled. Then the joints are laid out from the disc's rim.
    /// </summary>
    void StepArms(PlaneLeak v)
    {
        for (int i = 0; i < v.Arms.Length; i++)
        {
            var arm = v.Arms[i];
            arm.KickSpeed += (-AdventureTuning.LeakSpring * arm.Kick - AdventureTuning.LeakDamp * arm.KickSpeed) * Dt;
            arm.Kick += arm.KickSpeed * Dt;
            int stapled = v.StapledOn(i), n = arm.Bends.Length;
            for (int j = stapled; j < n; j++)
            {
                float along = (j + 0.5f) / n;
                float wave = AdventureTuning.LeakWiggle * (0.4f + 0.6f * along) * MathF.Sin(Time * AdventureTuning.LeakWave + arm.Seed + j * AdventureTuning.LeakWaveJoint);
                float target = wave + arm.Kick / Math.Max(n - stapled, 1);
                arm.Bends[j] = MathUtil.MoveToward(arm.Bends[j], target, AdventureTuning.LeakEase * Dt + MathF.Abs(arm.KickSpeed) * Dt);
            }
        }
        LayArms(v);
    }

    /// <summary>Its arms' joints, from the disc's rim out along each arm's way, bending at each.</summary>
    static void LayArms(PlaneLeak v)
    {
        foreach (var arm in v.Arms)
        {
            int n = arm.Bends.Length;
            float seg = arm.Length / Math.Max(n, 1), heading = arm.Angle;
            var p = v.Position + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * AdventureTuning.LeakDisc;
            arm.Points[0] = p;
            for (int j = 0; j < n; j++)
            {
                heading += arm.Bends[j];
                p += new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * seg;
                arm.Points[j + 1] = p;
            }
        }
    }

    /// <summary>
    /// The arms against her and her shots: a stretch not stapled stings her if she touches it and throws her off; a shot
    /// that meets one pops on it and bends it away (a stapled stretch only stops the shot).
    /// </summary>
    void StepArmContact()
    {
        var p = Player;
        foreach (var v in Leaks)
        {
            if (v.Sealed) continue;
            for (int i = 0; i < v.Arms.Length; i++)
            {
                var arm = v.Arms[i];
                int stapled = v.StapledOn(i), n = arm.Bends.Length;
                for (int j = 0; j < n; j++)
                {
                    Vector2 a = arm.Points[j], b = arm.Points[j + 1];
                    float half = LeakArm.HalfWidth((j + 0.5f) / n);
                    if (j >= stapled && !Defeated)
                    {
                        var near = ClosestOn(a, b, p.Position);
                        if (Vector2.Distance(near, p.Position) < half + Radius)
                        {
                            var away = SafeNormalize(p.Position - near);
                            if (away == Vector2.Zero) away = SafeNormalize(p.Position - v.Position);
                            if (HurtPlayer(AdventureTuning.LeakSting, away, DamageSource.Leak)) p.Velocity += away * AdventureTuning.LeakKnock;
                        }
                    }
                    foreach (var shot in Shots)
                    {
                        if (!shot.FromPlayer || shot.Life <= 0f || shot.BossPearl) continue;
                        Vector2 from = float.IsNaN(shot.From.X) ? shot.Position : shot.From;
                        if (SegmentGap(from, shot.Position, a, b) > half + shot.Radius) continue;
                        var hit = ClosestOn(a, b, shot.Position);
                        Pop(shot, hit - shot.Position);
                        Events.Add(new PlaneEvent(PlaneEventType.LeakArmStruck, hit, SafeNormalize(shot.Velocity), j >= stapled ? 1f : 0f));
                        if (j < stapled) continue;
                        // Bent away from the shot: the way round the arm's root that the shot pushes it.
                        var root = arm.Points[0];
                        float turn = MathF.Sign(Cross(hit - root, shot.Velocity));
                        arm.KickSpeed += turn * AdventureTuning.LeakKick;
                    }
                }
            }
        }
    }

    static Vector2 ClosestOn(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        float t = l2 > 1e-9f ? Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f) : 0f;
        return a + ab * t;
    }

    static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>The least distance between two segments.</summary>
    static float SegmentGap(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1), d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
        if ((d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f)) return 0f;
        return MathF.Min(MathF.Min(Vector2.Distance(ClosestOn(q1, q2, p1), p1), Vector2.Distance(ClosestOn(q1, q2, p2), p2)),
            MathF.Min(Vector2.Distance(ClosestOn(p1, p2, q1), q1), Vector2.Distance(ClosestOn(p1, p2, q2), q2)));
    }

    /// <summary>Sealed: its ink goes, from the leak outward.</summary>
    void Dissolve(InkDots dots, PlaneLeak v, int index)
    {
        v.Wave += AdventureTuning.DissolveSpeed * Dt;
        bool left = false;
        foreach (int i in dots.Claimed)
        {
            if (dots.Source[i] != index || dots.Ink[i] <= 0f) continue;
            if (dots.Far[i] > v.Wave)
            {
                left = true;
                continue;
            }
            dots.Remove(i);
            Events.Add(new PlaneEvent(PlaneEventType.DotDecayed, InkDots.Centre(i), Vector2.Zero));
        }
        if (!left) v.Wave = -1f;
    }

    /// <summary>Ink cleared while its leak is open comes back, out from the leak along the way it first came.</summary>
    void StepReturn(InkDots dots)
    {
        if (Tick % AdventureTuning.ReturnEvery != 0) return;
        long held = (long)(AdventureTuning.ReturnHold * TickRate);
        _back.Clear();
        foreach (int i in dots.Claimed)
        {
            if (dots.Ink[i] > 0f || Tick - dots.GoneAt[i] < held || Leaks[dots.Source[i]].Sealed) continue;
            int from = dots.Parent[i];
            if (from < 0 || dots.Ink[from] > 0f) _back.Add(i);
        }
        foreach (int i in _back) dots.Add(i);
    }

    readonly List<int> _back = new();

    /// <summary>She comes near: it lashes out at the water round it, and the fish there turn.</summary>
    void Rouse(PlaneLeak v)
    {
        v.Roused = true;
        foreach (var f in ReefFish)
            if (f.Huntable && Vector2.Distance(f.Position, v.Position) <= AdventureTuning.RouseReach) f.Ink = 1f;
    }

    /// <summary>A tie hurts it: it spits a ring of ink.</summary>
    void Spit(PlaneLeak v)
    {
        for (int i = 0; i < AdventureTuning.LeakSpit; i++)
        {
            float a = i * MathF.Tau / AdventureTuning.LeakSpit;
            var way = new Vector2(MathF.Cos(a), MathF.Sin(a));
            MurkShots.Add(new MurkShot { Position = v.Position + way, From = v.Position, Velocity = way * AdventureTuning.LeakSpitSpeed, Damage = BlightTuning.ThrowDamage * AdventureTuning.LeakSpitHit });
        }
    }

    /// <summary>A leak sealed: by her, for her reward; or, drunk dry by the boss as it rises, for none.</summary>
    void SealLeak(PlaneLeak v, bool drunk = false)
    {
        bool wakes = LeaksLeft > 0;
        v.Sealed = true;
        v.Drunk = drunk;
        v.Light = v.Needed;
        if (!drunk) v.Wave = 0f;
        Events.Add(new PlaneEvent(drunk ? PlaneEventType.LeakDrunk : PlaneEventType.LeakSealed, v.Position, Vector2.UnitY, LeaksLeft));
        if (wakes && LeaksLeft == 0 && Currents.Count > 0) Events.Add(new PlaneEvent(PlaneEventType.MainCurrentWoke, v.Position, Vector2.UnitY));
        if (Map.LeakCave) DissolveAsh(v.Position);
        if (drunk) return;
        // Her reward: shells burst out of it.
        for (int i = 0; i < AdventureTuning.LeakShells; i++)
        {
            float a = _drops.Range(0f, MathF.Tau);
            Shells.Add(new PlaneShell { Position = v.Position, Velocity = new Vector2(MathF.Cos(a), MathF.Sin(a)) * _drops.Range(3f, 6f) });
        }
        // A boss level's way down waits for its boss too (PlaneBoss's Release opens it once both are done).
        if (LeaksLeft > 0 || ExitOpen || Boss is { Freed: false }) return;
        ExitOpen = true;
        Events.Add(new PlaneEvent(PlaneEventType.ExitOpened, Map.Exit.Position, Vector2.Zero));
    }

    /// <summary>Verification: this leak (or the first open one) sealed at once.</summary>
    public void DebugSealLeak(PlaneLeak? which = null)
    {
        if ((which ?? Leaks.FirstOrDefault(v => !v.Sealed)) is { } v) SealLeak(v);
    }

    /// <summary>Verification and tests: a dot at a point (false where none may lie or one already does).</summary>
    public bool DebugDot(Vector2 at) => Dots?.Add(InkDots.CellOf(at)) ?? false;

    // ───────────────────────── the ink ─────────────────────────

    /// <summary>A shot of her light flew from a to b: every dot it passed through is cleared.</summary>
    void ClearAlong(Vector2 a, Vector2 b, float radius)
    {
        var dots = Dots!;
        float reach = radius + AdventureTuning.DotRadius;
        InkDots.Span((a + b) * 0.5f, Vector2.Distance(a, b) * 0.5f + reach, out int x0, out int y0, out int x1, out int y1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * InkDots.N + x;
            if (dots.Ink[i] <= 0f || Geo.SegmentDistance(InkDots.Centre(i), a, b) > reach) continue;
            dots.Remove(i);
            Events.Add(new PlaneEvent(PlaneEventType.DotCleared, InkDots.Centre(i), Vector2.Zero));
        }
    }

    /// <summary>Her light on the dots: where she swims, where her shots pop (from the war's turn), a Starburst's burst.</summary>
    void LightDots(Vector2 at, float radius, float strength)
    {
        if (Dots is not { } dots) return;
        _cleared.Clear();
        dots.Light(at, radius, strength, Tick, _cleared);
        foreach (var c in _cleared) Events.Add(new PlaneEvent(PlaneEventType.DotCleared, c, Vector2.Zero));
    }

    // ───────────────────────── fish and anemones ─────────────────────────

    /// <summary>
    /// Healthy fish eat the dots they touch and fill over ink (draining over clean water); full, a fish turns (a school
    /// all together). A pufferfish fills the same way; full, it is an Inkwarden. (Anemones are only the reef's.)
    /// </summary>
    void StepGauges(InkDots dots)
    {
        foreach (var f in ReefFish)
        {
            if (f.Corrupted || f.Turning > 0f) continue;
            float r = f.Kind switch { ReefFishKind.Chromis => ReefFishTuning.ChromisRadius, ReefFishKind.Clownfish => ReefFishTuning.ClownRadius, _ => ReefFishTuning.TangRadius };
            int near = dots.Nearest(f.Position, AdventureTuning.FishSense);
            if (near >= 0 && Vector2.Distance(InkDots.Centre(near), f.Position) < r + AdventureTuning.DotRadius + 0.1f)
            {
                // Eaten: the dot is gone, its ink in the fish (a freed fish eats it harmlessly).
                dots.Remove(near);
                Events.Add(new PlaneEvent(PlaneEventType.DotEaten, InkDots.Centre(near), Vector2.Zero));
                if (!f.Freed) f.Ink += AdventureTuning.FishEat;
            }
            if (f.Freed) continue;
            if (f.Ink < 1f) f.Ink = Math.Clamp(f.Ink + (near >= 0 ? AdventureTuning.FishFill : -AdventureTuning.FishDrain) * Dt, 0f, 1f);
            if (f.Ink < 1f) continue;
            if (f.Kind == ReefFishKind.Chromis)
            {
                foreach (var o in ReefFish)
                    if (o.Kind == ReefFishKind.Chromis && o.Host == f.Host && o.Huntable) Turn(o);
            }
            else Turn(f);
        }
        // Pufferfish: they eat the dots they touch and fill over ink; full, one is an Inkwarden (the spiky ring).
        foreach (var f in Fish)
        {
            if (f.Freed || f.Gone) continue;
            int near = dots.Nearest(f.Position, AdventureTuning.PufferSense);
            if (near >= 0 && Vector2.Distance(InkDots.Centre(near), f.Position) < PufferlingTuning.CalmRadius + AdventureTuning.DotRadius + 0.1f)
            {
                dots.Remove(near);
                Events.Add(new PlaneEvent(PlaneEventType.DotEaten, InkDots.Centre(near), Vector2.Zero));
                f.Ink += AdventureTuning.FishEat;
            }
            if (f.Ink < 1f) f.Ink = Math.Clamp(f.Ink + (near >= 0 ? AdventureTuning.PufferFill : -AdventureTuning.PufferDrain) * Dt, 0f, 1f);
            if (f.Ink < 1f) continue;
            f.Gone = true;
            var w = NewWarden(f.Position, roams: false);
            w.Puffer = true;
            w.Home = f.Position;
            w.Target = f.Position;
            w.Spin = f.Heading;
            Inkwardens.Add(w);
            Events.Add(new PlaneEvent(PlaneEventType.FishTurned, f.Position, Vector2.Zero, WardenTuning.Radius, DamageSource.None));
        }

        void Turn(PlaneReefFish f)
        {
            f.Ink = 1f;
            f.Turning = MurkFishTuning.TurnSeconds;
        }
    }

    /// <summary>A spiked murkling's throw strikes a fish: its ink fills the gauges of the fish in its splash.</summary>
    void InkFish(MurkShot s, PlaneReefFish struck)
    {
        foreach (var f in ReefFish)
        {
            if (!f.Huntable || (f != struck && Vector2.Distance(f.Position, s.Position) > s.Splash)) continue;
            f.Ink = MathF.Min(1f, f.Ink + AdventureTuning.ThrowFill);
        }
    }

    // ───────────────────────── the three murklings ─────────────────────────

    /// <summary>A fish-born murkling of the adventure, by the fish it was.</summary>
    void StepAdventureMurkling(Murkling m)
    {
        switch (m.Fish!.Kind)
        {
            case ReefFishKind.BlueTang: StepSpiked(m); break;
            case ReefFishKind.Clownfish: StepTadpole(m); break;
            default: StepShoal(m); break;
        }
    }

    Vector2 Wobble(Murkling m, float amount) => new Vector2(MathF.Sin(Time * 2.3f + m.Seed), MathF.Cos(Time * 1.9f + m.Seed * 2f)) * amount;

    /// <summary>
    /// Spiked murkling: keeps KeepMin–KeepMax from her, circling, and throws at her (wound up as a murkling's throw);
    /// never clings. Touching it hurts and knocks her off.
    /// </summary>
    void StepSpiked(Murkling m)
    {
        var p = Player;
        var to = p.Position - m.Position;
        float d = to.Length();
        var dir = SafeNormalize(to);
        bool sees = d < AdventureTuning.SpikedSense;
        Vector2 wish = Wobble(m, 0.3f);
        if (sees && !NavLine(m.Position, p.Position))
            // Rock between: round it toward her, to where it can see her again.
            wish += WayToHer(m.Position);
        else if (sees)
        {
            float keep = d > AdventureTuning.KeepMax ? 1f : d < AdventureTuning.KeepMin ? -1f : 0f;
            float spin = m.Seed % 2f < 1f ? 1f : -1f;
            wish += dir * keep + new Vector2(-dir.Y, dir.X) * spin * 0.7f;
        }
        float pace = AdventureTuning.SpikedSpeed * (m.Windup > 0f ? BlightTuning.WindupSlow : 1f) * (sees ? 1f : 0.4f);
        m.Target = sees ? p.Position : m.Position;
        m.Velocity = MoveToward(m.Velocity, SafeNormalize(wish) * pace, 6f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Body, report: false);

        if (Vector2.Distance(m.Position, p.Position) < m.Radius + Radius)
        {
            if (HurtPlayer(BlightTuning.ContactDamage, dir, DamageSource.Murkling)) p.Velocity += dir * AdventureTuning.SpikedKnock;
            m.Velocity -= dir * AdventureTuning.SpikedKnock * 0.5f;
        }
        m.ThrowAge += Dt;
        if (m.Windup > 0f) StepWindup(m);
        else
        {
            m.ThrowTimer -= Dt;
            if (sees && d < m.ThrowRange && m.ThrowTimer <= 0f && LineOfSight(m.Position, p.Position)) BeginThrow(m, null);
        }
    }

    /// <summary>Tadpole murkling: only chases her, a little faster than she cruises; clings on touch and drains her.</summary>
    void StepTadpole(Murkling m)
    {
        var p = Player;
        float speed = Tuning.PlaneCruiseSpeed * AdventureTuning.TadpoleSpeed;
        float d = Vector2.Distance(m.Position, p.Position);
        bool chase = d < AdventureTuning.TadpoleSense;
        Vector2 wish = (chase ? WayToHer(m.Position) * speed : Vector2.Zero) + Wobble(m, chase ? 0.5f : 0.6f);
        if (wish.Length() > speed) wish = Vector2.Normalize(wish) * speed;
        m.Target = chase ? p.Position : m.Position;
        m.Velocity = MoveToward(m.Velocity, wish, 14f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Radius, report: false);
        if (d < m.Radius + Radius && p.DashInvulnerableTimer <= 0f && p.ShieldTimer <= 0f && LesserClingers < MurkFishTuning.MaxClinging)
        {
            m.Clinging = true;
            m.ClingOffset = SafeNormalize(m.Position - p.Position) * (Radius * 0.85f);
            Events.Add(new PlaneEvent(PlaneEventType.MurklingCling, m.Position, Vector2.Zero));
        }
    }

    long _shoalTick = -1;
    readonly Dictionary<int, List<Murkling>> _shoals = new();
    /// <summary>Per swarm, how many places its ring has: it never closes up as fish are freed, each keeping to its own.</summary>
    readonly Dictionary<int, int> _shoalPlaces = new();

    /// <summary>
    /// Shoal murkling: very fast; near her, its school stands on a ring round her, most of it on her aim's side, so it
    /// takes her shots. Each holds its own place on the ring until it is freed: a freed one leaves a gap, and any fish that
    /// turns later takes the first gap. Harmless otherwise. Away from her, the school keeps together.
    /// </summary>
    void StepShoal(Murkling m)
    {
        var p = Player;
        if (_shoalTick != Tick)
        {
            _shoalTick = Tick;
            foreach (var list in _shoals.Values) list.Clear();
            foreach (var o in Murklings)
            {
                if (!o.Alive || o.Fish is not { Kind: ReefFishKind.Chromis }) continue;
                if (!_shoals.TryGetValue(o.Swarm, out var l)) _shoals[o.Swarm] = l = new List<Murkling>();
                l.Add(o);
            }
            foreach (var (swarm, list) in _shoals)
                foreach (var o in list)
                {
                    if (o.ShoalSlot >= 0) continue;
                    int free = 0;
                    while (list.Exists(q => q.ShoalSlot == free)) free++;
                    o.ShoalSlot = free;
                    _shoalPlaces[swarm] = Math.Max(_shoalPlaces.GetValueOrDefault(swarm), free + 1);
                }
        }
        var school = _shoals[m.Swarm];
        Vector2 centre = Vector2.Zero;
        foreach (var o in school) centre += o.Position;
        centre /= school.Count;
        Vector2 wish;
        if (Vector2.Distance(centre, p.Position) < AdventureTuning.ShoalSense)
        {
            // Its place on the ring: evenly round her, the ring circling her (each school its own way, each layer a little
            // faster than the one inside it) — never tied to her aim, so she can time her shots through the gaps.
            int k = m.ShoalSlot, n = _shoalPlaces[m.Swarm], layer = k % 3;
            float spin = (m.Swarm & 1) == 0 ? 1f : -1f;
            float a = MathF.Tau * k / n + spin * AdventureTuning.ShoalOrbit * (1f + 0.25f * layer) * Time;
            float r = Radius + AdventureTuning.ShoalRing + AdventureTuning.ShoalLayer * layer;
            var slot = p.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            // (Rock between it and her: round the rock to her first.)
            wish = NavLine(m.Position, p.Position) ? (slot - m.Position) * 8f : WayToHer(m.Position) * AdventureTuning.ShoalSpeed;
            m.Target = p.Position;
        }
        else
        {
            Vector2 apart = Vector2.Zero;
            foreach (var o in school)
            {
                if (o == m) continue;
                Vector2 dd = m.Position - o.Position;
                float l = dd.Length();
                if (l < MurkFishTuning.Spacing) apart += (l > 1e-3f ? dd / l : new Vector2(MathF.Cos(m.Seed), MathF.Sin(m.Seed))) * (1f - l / MurkFishTuning.Spacing);
            }
            wish = (centre - m.Position) * 1.2f + apart * 4f + Wobble(m, 0.6f);
            m.Target = centre;
        }
        if (wish.Length() > AdventureTuning.ShoalSpeed) wish = Vector2.Normalize(wish) * AdventureTuning.ShoalSpeed;
        m.Velocity = MoveToward(m.Velocity, wish, 40f * Dt);
        Move(ref m.Position, ref m.Velocity, m.Body, report: false);
    }

    // ───────────────────────── her oral arm ─────────────────────────

    /// <summary>Where she has been lately (newest last) and when: the way her arm trails.</summary>
    readonly List<(Vector2 At, float Time)> _trail = new();
    readonly List<Vector2> _arm = new();

    /// <summary>Her oral arm's length now: her max HP, as a length.</summary>
    public float ArmLength => AdventureTuning.ArmLength(Run.MaxHp);

    /// <summary>Her oral arm as the sim has it: from her, back along the way she swam, as far as it reaches.</summary>
    public IReadOnlyList<Vector2> Arm => _arm;

    /// <summary>Her oral arm trails her; a corrupted creature it brushes takes a little of her light.</summary>
    void StepArm()
    {
        var p = Player;
        if (_trail.Count == 0 || Vector2.Distance(_trail[^1].At, p.Position) > 0.1f) _trail.Add((p.Position, Time));
        while (_trail.Count > 0 && Time - _trail[0].Time > AdventureTuning.ArmMemory) _trail.RemoveAt(0);
        _arm.Clear();
        _arm.Add(p.Position);
        float left = ArmLength;
        for (int i = _trail.Count - 1; i >= 0 && left > 0f; i--)
        {
            var from = _arm[^1];
            float d = Vector2.Distance(from, _trail[i].At);
            if (d < 1e-4f) continue;
            if (d >= left)
            {
                _arm.Add(from + (_trail[i].At - from) * (left / d));
                break;
            }
            _arm.Add(_trail[i].At);
            left -= d;
        }
        if (_arm.Count < 2 || Defeated) return;

        float light = PlaneCombatTuning.ShotDamage * DamageScale * AdventureTuning.ArmShare;
        foreach (var m in Murklings)
        {
            m.ArmCooldown -= Dt;
            if (!m.Alive || m.Budding || m.Clinging || m.ArmCooldown > 0f || ArmDistance(m.Position) > m.Radius + AdventureTuning.ArmWidth) continue;
            m.ArmCooldown = AdventureTuning.ArmEvery;
            float dealt = Ward(m, m.Position, light);
            bool emptied = dealt > 0f && m.Corruption.Hit(dealt);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, SafeNormalize(m.Position - p.Position), dealt));
            if (emptied) BurstMurkling(m, ember: true);
        }
        foreach (var w in Inkwardens)
        {
            w.ArmCooldown -= Dt;
            if (!w.Alive || w.ArmCooldown > 0f || ArmDistance(w.Position) > WardenTuning.Radius + AdventureTuning.ArmWidth) continue;
            w.ArmCooldown = AdventureTuning.ArmEvery;
            bool emptied = w.Corruption.Hit(light);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, w.Position, SafeNormalize(w.Position - p.Position), light));
            if (emptied) FreeWarden(w);
        }
        foreach (var f in Flounders)
        {
            if (!f.Corrupted || f.ArmCooldown > 0f || ArmDistance(f.Position) > FlounderTuning.Radius + AdventureTuning.ArmWidth) continue;
            f.ArmCooldown = AdventureTuning.ArmEvery;
            HitFlounder(f, Ward(f, f.Position, light), SafeNormalize(f.Position - p.Position));
        }
    }

    /// <summary>How far a point is from her oral arm.</summary>
    float ArmDistance(Vector2 at)
    {
        float best = float.MaxValue;
        for (int i = 1; i < _arm.Count; i++) best = MathF.Min(best, Geo.SegmentDistance(at, _arm[i - 1], _arm[i]));
        if (NerveAt is { } nerve) best = MathF.Min(best, Vector2.Distance(at, nerve));
        return best;
    }

    /// <summary>Tests: her trail laid out behind her (oldest first), as if she had just swum it.</summary>
    public void DebugTrail(IEnumerable<Vector2> path)
    {
        _trail.Clear();
        foreach (var at in path) _trail.Add((at, Time));
    }
}
