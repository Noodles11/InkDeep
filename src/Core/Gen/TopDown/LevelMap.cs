using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using OctoShoots.Core.Creatures;

namespace OctoShoots.Core.Gen.TopDown;

// Map coordinates: X runs east, Y runs south (world X and world Z). North (−Y) is up on screen.

/// <summary>
/// A named place on the level (DESIGN-TOPDOWN §4.1 stage 1). Exit: the way down — a blue hole, or on a boss level the
/// boss arena with the Crack across its floor. ShellCache: a guarded clearing of shells.
/// </summary>
public enum PoiKind { Start, ShellCache, Exit, Shop, Secret, CurseDen, TreasureCave, CrabDeal }

public sealed class Poi
{
    public PoiKind Kind;
    public Vector2 Position;
    /// <summary>Radius of the open ground (or cave) the place occupies.</summary>
    public float Radius;
    /// <summary>The one optional POI allowed 20–35 m from the start.</summary>
    public bool Early;
    /// <summary>Index into <see cref="LevelMap.Caves"/> for cave-hosted places, else −1.</summary>
    public int Cave = -1;

    /// <summary>Special rooms live in caves (DESIGN-TOPDOWN §4.2).</summary>
    public static bool IsCaveHosted(PoiKind kind) => kind is PoiKind.Shop or PoiKind.Secret or PoiKind.CurseDen or PoiKind.TreasureCave or PoiKind.CrabDeal;
}

/// <summary>Main: a route canyon start→rift. Spur: to a place. Pass: a narrow cut. Side: a labyrinth canyon (dead end or loop).</summary>
public enum CorridorKind { Main, Spur, Pass, Side }

/// <summary>A swimmable channel pinned at h = 0: a polyline with a width.</summary>
public sealed class Corridor
{
    public CorridorKind Kind;
    public List<Vector2> Points = new();
    public float Width;
    /// <summary>For spurs: the POI it connects.</summary>
    public int Poi = -1;
    /// <summary>A shortcut (a pass): a tunnel under the rock, roofed by <see cref="LevelMap.Roof"/>.</summary>
    public bool Roofed;
    /// <summary>While the level is built: the rock a pass bored away (grid index → height before), to fill it back if it gets no roof.</summary>
    internal Dictionary<int, float>? Bored;

    public float HalfWidth => Width * 0.5f;

    public float Length
    {
        get
        {
            float l = 0f;
            for (int i = 1; i < Points.Count; i++) l += Vector2.Distance(Points[i - 1], Points[i]);
            return l;
        }
    }

    /// <summary>Distance from p to the centre line.</summary>
    public float DistanceToCentre(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 1; i < Points.Count; i++) best = MathF.Min(best, Geo.SegmentDistance(p, Points[i - 1], Points[i]));
        return best;
    }

    public bool Contains(Vector2 p) => DistanceToCentre(p) <= HalfWidth;
}

/// <summary>An open crossing where two corridors meet (~14 m).</summary>
public readonly record struct Plaza(Vector2 Center, float Radius);

/// <summary>A chamber of the cave: on the main chain (start to way down), or a dead end (a branch with one tunnel).</summary>
public readonly record struct CaveChamber(Vector2 At, float Radius, bool Main, bool DeadEnd);

/// <summary>
/// A nave (DESIGN-TOPDOWN §4.9): a chamber off the main route (a side branch or a dead end), each holding something —
/// the place in it or opening off it (<paramref name="Poi"/>, an index into the map's places), or else, at -1, a den of
/// corrupted creatures whose freeing pays out (PlaneDens).
/// </summary>
public readonly record struct Nave(Vector2 At, float Radius, int Poi)
{
    public bool Den => Poi < 0;
}


/// <summary>
/// The shaft down to the next level (DESIGN-TOPDOWN §4.6): an ellipse in the exit's floor (a disc for a blue hole, the
/// Crack's fissure in a boss arena) where the floor gives way and the level below shows through.
/// </summary>
public readonly record struct Shaft(Vector2 Center, Vector2 Axis, float HalfLength, float HalfWidth)
{
    /// <summary>True inside the ellipse grown by <paramref name="margin"/> metres.</summary>
    public bool Contains(Vector2 p, float margin = 0f)
    {
        Vector2 d = p - Center;
        float a = Vector2.Dot(d, Axis) / (HalfLength + margin), b = Vector2.Dot(d, Geo.Perp(Axis)) / (HalfWidth + margin);
        return a * a + b * b <= 1f;
    }
}

/// <summary>
/// Pocket: one chamber, the dead end of its spur (the shop, a secret, a clam room). Tunnel (the curse den): a long,
/// narrow, winding dead end, its chambers the points along it (each its half-width), the last its end chamber.
/// </summary>
public enum CaveKind { Pocket, Tunnel }

/// <summary>A cave hollowed out inside a mountain: floor under the swim level, roofed by the mountain (<see cref="LevelMap.Lid"/>), opening through its mouth.</summary>
public sealed class CaveSite
{
    public CaveKind Kind;
    public int Poi;
    public List<(Vector2 Center, float Radius)> Chambers = new();
    /// <summary>Where the spur enters the cave.</summary>
    public Vector2 Mouth;
    /// <summary>Unit direction from the cave out through its mouth.</summary>
    public Vector2 Facing;
}

public enum CanopyKind { Arch, CaveRoof, Overhang, TunnelRoof }

/// <summary>
/// Something solid overhead, on the canopy layer above the swim band (DESIGN-TOPDOWN §3): it fades while
/// Clementine is underneath. Footprint is a disc (Radius > 0) or an oriented rectangle.
/// </summary>
public sealed class Canopy
{
    public CanopyKind Kind;
    public Vector2 Center;
    /// <summary>Unit direction of the rectangle's length (for an arch: from one footing to the other).</summary>
    public Vector2 Axis = Vector2.UnitX;
    public float HalfLength;
    public float HalfWidth;
    /// <summary>Disc footprint radius, or 0 for a rectangle.</summary>
    public float Radius;
    public float Bottom;
    public float Top;
    /// <summary>Arches: the corridor they span; a tunnel's roof: the corridor it roofs (all its pieces fade as one).</summary>
    public int Corridor = -1;

    public Vector2 EndA => Center - Axis * HalfLength;
    public Vector2 EndB => Center + Axis * HalfLength;

    public bool Contains(Vector2 p)
    {
        Vector2 d = p - Center;
        if (Radius > 0f) return d.LengthSquared() <= Radius * Radius;
        float along = Vector2.Dot(d, Axis);
        float across = Vector2.Dot(d, new Vector2(-Axis.Y, Axis.X));
        return MathF.Abs(along) <= HalfLength && MathF.Abs(across) <= HalfWidth;
    }
}

/// <summary>What a sealed wall hides, and so what is carved on it: a secret room or a way on (an X), the boss (a sword).</summary>
public enum SealKind { Secret, Path, Boss }

/// <summary>
/// A sealed wall (DESIGN-TOPDOWN §4.2): a slab of reef wall across a passage — a secret room's way in, a tunnel, the
/// way into the boss's arena — Thick metres either side of Center along Facing (out toward the side it is met from),
/// Radius metres either side across it. It blocks swimming and shots until her light fills its carving
/// (PlaneSeals); then it slides down into the sand. The terrain it stands as is LevelMap.Sealed. (Thick 0: a disc of
/// Radius, as older seals were.)
/// </summary>
public readonly record struct WeakRock(Vector2 Center, float Radius, int Cave, Vector2 Facing = default, float Thick = 0f, SealKind Kind = SealKind.Secret, bool Fake = false)
{
    /// <summary>Whether a point (padded by a body's radius) is inside the wall.</summary>
    public bool Contains(Vector2 p, float pad = 0f)
    {
        if (Thick <= 0f) return Vector2.Distance(p, Center) < Radius + pad;
        var d = p - Center;
        float along = Vector2.Dot(d, Facing), across = d.X * -Facing.Y + d.Y * Facing.X;
        return MathF.Abs(along) < Thick + pad && MathF.Abs(across) < Radius + pad;
    }

    /// <summary>
    /// A wall longer than this (Radius over GateHalf + GateMore) opens only a gate in its middle, GateHalf metres either
    /// side; the rest of it stays standing.
    /// </summary>
    public const float GateHalf = 3.2f, GateMore = 1.5f;

    /// <summary>Its gate's half width (it opens only there), or its whole width when it is short.</summary>
    public float Gate => Thick > 0f && Radius > GateHalf + GateMore ? GateHalf : Radius;

    /// <summary>Across it, the unit way (its Radius runs either side along this).</summary>
    public Vector2 Across => new(-Facing.Y, Facing.X);

    /// <summary>What of it still stands once it has opened: the wall either side of its gate (none when it opens whole).</summary>
    public IEnumerable<WeakRock> Remains()
    {
        if (Gate >= Radius) yield break;
        float half = (Radius - Gate) * 0.5f, mid = Gate + half;
        yield return this with { Center = Center + Across * mid, Radius = half };
        yield return this with { Center = Center - Across * mid, Radius = half };
    }

    /// <summary>Into the wall from a point by it (its face's inward normal).</summary>
    public Vector2 Inward(Vector2 p)
    {
        if (Thick > 0f) return Vector2.Dot(p - Center, Facing) >= 0f ? -Facing : Facing;
        var d = Center - p;
        return d.LengthSquared() > 1e-8f ? Vector2.Normalize(d) : Vector2.UnitX;
    }
}

public enum DecorKind
{
    SandChannel, GrassMeadow, Boulder, Bommie,
    SeaRod, SeaFan, TubeSponge,
    CliffCoral, EncrustingCoral, CrownGarden,
    BiolumAccent,
}

/// <summary>Cosmetic reef dressing, chosen by height band (DESIGN-TOPDOWN §4.1 stage 5). Never affects play.</summary>
public readonly record struct DecorSpot(DecorKind Kind, Vector2 Position, float Height, float Scale, float Yaw);

public readonly record struct SealedPocket(Vector2 Position, float Radius);

/// <summary>
/// A cave's opening in a wall (docs/RPG-PROPOSAL.md §2.3): a passage cut into the rock where it stands tall at a
/// chamber's edge, roofed by the rock that stood there (LevelMap.Roof). At is its threshold, under the arch of its
/// face; Into, the way into the rock; its back lies Depth in, in the dark. A notch leads up to it through the wall's
/// foot from the water.
/// </summary>
public readonly record struct Doorway(Vector2 At, Vector2 Into)
{
    /// <summary>How deep the passage runs into the rock, and its open half-width (metres).</summary>
    public const float Depth = 4.5f, HalfWidth = 1.7f;
    /// <summary>The arch's crown over the swim level, and the rock the face must stand over the swim level where it is cut.</summary>
    public const float ArchTop = 2.8f, FaceHeight = 3.4f;

    /// <summary>The way across it (Into turned a quarter).</summary>
    public Vector2 Across => new(-Into.Y, Into.X);
    public Vector2 Back => At + Into * Depth;
    /// <summary>Where one comes through it: just short of its back, in the dark.</summary>
    public Vector2 Inside => Back - Into * 0.8f;

    /// <summary>How far in a point lies (from the threshold, along Into) and how far off its middle.</summary>
    public (float In, float Off) Measure(Vector2 p)
    {
        var d = p - At;
        return (Vector2.Dot(d, Into), MathF.Abs(Vector2.Dot(d, Across)));
    }

    /// <summary>Whether a point lies in the passage, at least this far in from its threshold.</summary>
    public bool Holds(Vector2 p, float past = 0f)
    {
        var (inward, off) = Measure(p);
        return inward >= past && inward <= Depth + 0.5f && off <= HalfWidth + 0.3f;
    }
}

/// <summary>
/// A kelp bed (DESIGN-TOPDOWN §4.1 stage 5): a dense patch of tall seaweed rooted on the open floor, its strands' roots
/// and lengths (metres). Cosmetic in the sim (it neither blocks nor slows); the view sways it in the water everything
/// stirs, and it can hide what swims in it.
/// </summary>
public sealed class KelpBed
{
    public Vector2 Center;
    public float Radius;
    public List<Vector2> Roots = new();
    public List<float> Lengths = new();
}

public readonly record struct BuriedCoins(Vector2 Position, int Amount);

/// <summary>
/// One of the reef's biome zones (docs/REEF-WORLD.md §1): its box, its name and title, the depth whose look it takes
/// (ReefLook), its Menace and its area level (its caves' least level; on the open reef, its creatures').
/// </summary>
public sealed record ReefZone(string Name, string Title, int Look, Vector2 Min, Vector2 Max, float Menace, int Level)
{
    /// <summary>How far outside its box a point lies (0 inside).</summary>
    public float Outside(Vector2 p)
    {
        float dx = MathF.Max(MathF.Max(Min.X - p.X, p.X - Max.X), 0f), dy = MathF.Max(MathF.Max(Min.Y - p.Y, p.Y - Max.Y), 0f);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>How far inside its box a point lies, to the nearest edge (0 on it or outside).</summary>
    public float Inside(Vector2 p) =>
        MathF.Max(MathF.Min(MathF.Min(p.X - Min.X, Max.X - p.X), MathF.Min(p.Y - Min.Y, Max.Y - p.Y)), 0f);
}

public enum SpawnRole { Den, Ambush, Patrol, Guardian }

/// <summary>DESIGN-2D §12.1 movement classes.</summary>
public enum MoveClass { Swimmer, Walker, Clinger, Burrower }

/// <summary>One row of the level's spawn table (DESIGN-TOPDOWN §4.1 stage 6).</summary>
public sealed class SpawnEntry
{
    public SpawnRole Role;
    public EnemyKind Kind;
    public MoveClass Class;
    public int Count;
    public Vector2 Position;
    /// <summary>Patrols: the corridor stretch they loop along.</summary>
    public List<Vector2>? Path;
    /// <summary>Guardians and nests: the POI they belong to.</summary>
    public int Poi = -1;
    /// <summary>Secrets are trap-heavy (mimics).</summary>
    public bool Trap;
}

/// <summary>
/// One generated level (DESIGN-TOPDOWN §4): a square heightfield on a 1 m grid (140 m a side for a seeded level or a
/// leak cave, larger for a hand-drawn map that asks for it) plus everything the generator decided. Produced only by
/// <see cref="TopDownGenerator"/>; the game reads it and never generates or scatters.
/// </summary>
public sealed class LevelMap
{
    /// <summary>A seeded level's square (and a leak cave's, and a hand-drawn map's unless it says otherwise), metres a side.</summary>
    public const int Seeded = 140;

    public LevelMap(int size = Seeded)
    {
        Size = size;
        Heights = new float[Samples * Samples];
        Lid = NewLid(Samples);
        Roof = NewLid(Samples);
        Sealed = NewLid(Samples);
        SealedOpen = NewLid(Samples);
    }

    /// <summary>Its square, metres a side, and its grid points a side.</summary>
    public int Size { get; }
    public int Samples => Size + 1;

    /// <summary>Where the bell is drawn: floating just above the swim level (0), which every height is measured from.</summary>
    public const float SwimBand = 0.7f;

    /// <summary>The swim level: terrain above it blocks; anything at or below it (seabed, deeps) is open water to float over.</summary>
    public const float BlockHeight = 0f;

    /// <summary>Width of the impassable reef rim around the square.</summary>
    public const float RimWidth = 8f;

    /// <summary>How far below this level the next one lies (swim plane to swim plane, metres).</summary>
    public const float LevelDrop = 8f;
    /// <summary>The bottom of the shaft: the next level's seabed.</summary>
    public const float ShaftBottom = -LevelDrop - 1f;

    public ulong Seed;
    public LevelId Id;
    public int Cycle => Id.Cycle;
    public int Depth => Id.Depth;
    public int Level => Id.Level;
    public int Attempt;
    /// <summary>The exit is an arena round the Crack: the boss stands in it, and the Crack opens when she is freed.</summary>
    public bool HasBoss;
    /// <summary>The tutorial's open rift: the arena holds no boss, and the Crack stands open from the start.</summary>
    public bool Rift;
    public float Menace;
    /// <summary>
    /// A haven's water on a hand-drawn map (HandLayout.Haven: Lantern Bay, in a corner of the reef), the box it fills:
    /// nothing harms her in it, and coming into it heals her whole. Null where there is none.
    /// </summary>
    public (Vector2 Min, Vector2 Max)? Haven;
    /// <summary>Whether a point lies in the haven's water (or within <paramref name="margin"/> of it).</summary>
    public bool InHaven(Vector2 p, float margin = 0f) =>
        Haven is { } h && p.X >= h.Min.X - margin && p.Y >= h.Min.Y - margin && p.X <= h.Max.X + margin && p.Y <= h.Max.Y + margin;
    /// <summary>How far outside the haven's water a point lies (0 inside it, ∞ where there is none).</summary>
    public float OutOfHaven(Vector2 p)
    {
        if (Haven is not { } h) return float.PositiveInfinity;
        float dx = MathF.Max(MathF.Max(h.Min.X - p.X, p.X - h.Max.X), 0f), dy = MathF.Max(MathF.Max(h.Min.Y - p.Y, p.Y - h.Max.Y), 0f);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
    /// <summary>A hand-drawn map's name (HandLayout.Title), or empty for a seeded level.</summary>
    public string Title = "";
    /// <summary>A hand-drawn map (the reef): it holds no leaks and no main current of its own.</summary>
    public bool Station;
    /// <summary>A hand-drawn map whose exit is a current station, where the ride to another map starts (HandLayout.Station).</summary>
    public bool CurrentStation;
    /// <summary>The depth whose look it takes, when not its own depth's (HandLayout.Look).</summary>
    public int? LookDepth;
    /// <summary>The reef's biome zones (docs/REEF-WORLD.md §1, HandLayout.Zones); none on a seeded level or a cave.</summary>
    public List<ReefZone> Zones = new();

    /// <summary>The zone a point lies in (its box's; the nearest box's, out past every one), or -1 where there are none.</summary>
    public int ZoneAt(Vector2 p)
    {
        int best = -1;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < Zones.Count; i++)
        {
            float d = Zones[i].Outside(p);
            if (d < nearest)
            {
                nearest = d;
                best = i;
            }
        }
        return best;
    }
    /// <summary>
    /// A leak cave (docs/RPG-PROPOSAL.md §2.3): one leak, at the dead end of its main path where a way down would be;
    /// no shaft; its way out (Way) in its start chamber's wall.
    /// </summary>
    public bool LeakCave;
    /// <summary>A leak cave's level (what its leak is worth, how strong its creatures are); 0 elsewhere.</summary>
    public int CaveLevel;
    /// <summary>A region's cave mouths, openings in its walls: swimming into one takes her into its leak cave.</summary>
    public List<Doorway> Mouths = new();
    /// <summary>A leak cave's way out, the opening she came in by (in its start chamber's wall); null elsewhere.</summary>
    public Doorway? Way;
    /// <summary>The bay's mirror (HandLayout.Mirror): swimming onto it, she changes her look. Null elsewhere.</summary>
    public Vector2? Mirror;
    /// <summary>The mirror's pool (metres across its middle to its rim).</summary>
    public const float MirrorRadius = 1.7f;
    /// <summary>The bay's Tide Fan (HandLayout.Fan): where it stands, at the foot of its rock. Null elsewhere.</summary>
    public Vector2? Fan;
    /// <summary>The floor kept bare round the fan's rock (metres).</summary>
    public const float FanClear = 3.5f;
    public ulong EntryStampSeed, ExitStampSeed;
    public bool EntryFromArena;
    /// <summary>Where the floor gives way to the level below.</summary>
    public Shaft Shaft;
    /// <summary>The adventure's abyss reef (AbyssReef): the open floor dropped away outside the caves and tunnels.</summary>
    public bool Abyss;
    /// <summary>Her luck as the level was made (LuckTuning): it may bring a light spring (LightSprings).</summary>
    public float Luck;
    /// <summary>An abyss reef's shelves (AbyssReef): ledges just under the plane, jutting from the walls, where anemones grow.</summary>
    public List<Vector2> Shelves = new();

    /// <summary>Heights in metres at the grid points (Samples × Samples), row-major (index = y * Samples + x).</summary>
    public float[] Heights;

    /// <summary>
    /// The mountain over each cave as it stood before the chambers were hollowed out (NaN elsewhere): the cave's roof,
    /// drawn as intact rock until Clementine swims inside.
    /// </summary>
    public float[] Lid;

    static float[] NewLid(int samples)
    {
        var lid = new float[samples * samples];
        Array.Fill(lid, float.NaN);
        return lid;
    }

    /// <summary>The cave roof's height at a grid point, or NaN where no cave is.</summary>
    public float LidAt(int x, int y) => Lid[y * Samples + x];

    /// <summary>
    /// The rock over the shortcut tunnels (NaN elsewhere): over a pass, the ridge it was cut through as it stood; over a
    /// route shortcut, the rock either side carried on over it. Drawn as rock until Clementine is near.
    /// </summary>
    public float[] Roof;

    public float RoofAt(int x, int y) => Roof[y * Samples + x];

    public List<Poi> Pois = new();
    public List<Corridor> Corridors = new();
    public List<Plaza> Plazas = new();
    /// <summary>The main current's course, once worked out (MainCurrent.Course).</summary>
    internal List<Vector2>? MainCourse;
    /// <summary>The cave's open chambers, with their roles (the level templates' anchors read them).</summary>
    public List<CaveChamber> Chambers = new();
    /// <summary>The chambers off the main route, each with its place or its den.</summary>
    public List<Nave> Naves = new();
    /// <summary>The level's template (null: the general rules alone, as in the tutorial).</summary>
    public LevelTemplate? Template;
    public List<CaveSite> Caves = new();
    public List<Canopy> Canopies = new();
    public List<WeakRock> WeakRocks = new();
    /// <summary>The terrain the sealed walls stand as (NaN where no wall stands): the walls raised as if their slab of passage were rock.</summary>
    public float[] Sealed;
    /// <summary>The terrain a long sealed wall leaves standing once its gate has opened (NaN where none stands).</summary>
    public float[] SealedOpen;
    public float SealedOpenAt(int x, int y) => SealedOpen[y * Samples + x];
    public float SealedAt(int x, int y) => Sealed[y * Samples + x];
    public List<DecorSpot> Decor = new();
    /// <summary>Kelp beds: dense patches of tall seaweed.</summary>
    public List<KelpBed> Kelp = new();
    public List<SealedPocket> Pockets = new();
    public List<BuriedCoins> Coins = new();
    public List<SpawnEntry> Spawns = new();

    public Poi Start => Pois.Find(p => p.Kind == PoiKind.Start)!;
    public Poi Exit => Pois.Find(p => p.Kind == PoiKind.Exit)!;

    public float this[int x, int y]
    {
        get => Heights[y * Samples + x];
        set => Heights[y * Samples + x] = value;
    }

    /// <summary>Bilinear height at a map position (clamped to the square).</summary>
    public float[] CopyHeights() => (float[])Heights.Clone();

    public void SetHeights(float[] heights) => Array.Copy(heights, Heights, Heights.Length);

    public float HeightAt(Vector2 p)
    {
        float x = Math.Clamp(p.X, 0f, Size - 0.0001f), y = Math.Clamp(p.Y, 0f, Size - 0.0001f);
        int ix = (int)x, iy = (int)y;
        float tx = x - ix, ty = y - iy;
        float a = MathUtil.Lerp(this[ix, iy], this[ix + 1, iy], tx);
        float b = MathUtil.Lerp(this[ix, iy + 1], this[ix + 1, iy + 1], tx);
        return MathUtil.Lerp(a, b, ty);
    }

    /// <summary>Uphill direction of the terrain (not normalised).</summary>
    public Vector2 Gradient(Vector2 p)
    {
        const float e = 0.5f;
        return new Vector2(
            HeightAt(p + new Vector2(e, 0f)) - HeightAt(p - new Vector2(e, 0f)),
            HeightAt(p + new Vector2(0f, e)) - HeightAt(p - new Vector2(0f, e))) / (2f * e);
    }

    /// <summary>True when the swim band is open at p (terrain low enough; weak rock not counted).</summary>
    public bool IsOpen(Vector2 p) => p.X >= 0f && p.Y >= 0f && p.X <= Size && p.Y <= Size && HeightAt(p) <= BlockHeight;

    /// <summary>Index of the canopy overhead at p, or −1.</summary>
    public int CanopyAt(Vector2 p)
    {
        for (int i = 0; i < Canopies.Count; i++)
            if (Canopies[i].Contains(p)) return i;
        return -1;
    }

    public bool InsideAnyCorridor(Vector2 p)
    {
        foreach (var c in Corridors)
            if (c.Contains(p)) return true;
        return false;
    }

    /// <summary>Every byte of the map, for determinism checks.</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        void V(Vector2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }
        w.Write(2); // format version
        w.Write(Seed);
        w.Write(Cycle);
        w.Write(Depth);
        w.Write(Level);
        w.Write(Attempt);
        w.Write(HasBoss);
        w.Write(Rift);
        w.Write(Menace);
        w.Write(EntryStampSeed);
        w.Write(ExitStampSeed);
        V(Shaft.Center);
        V(Shaft.Axis);
        w.Write(Shaft.HalfLength);
        w.Write(Shaft.HalfWidth);
        foreach (float h in Heights) w.Write(h);
        foreach (float h in Lid) w.Write(h);
        foreach (float h in Sealed) w.Write(h);
        foreach (float h in SealedOpen) w.Write(h);
        w.Write(Pois.Count);
        foreach (var p in Pois)
        {
            w.Write((int)p.Kind);
            V(p.Position);
            w.Write(p.Radius);
            w.Write(p.Early);
            w.Write(p.Cave);
        }
        w.Write(Corridors.Count);
        foreach (var c in Corridors)
        {
            w.Write((int)c.Kind);
            w.Write(c.Width);
            w.Write(c.Poi);
            w.Write(c.Points.Count);
            foreach (var p in c.Points) V(p);
        }
        w.Write(Plazas.Count);
        foreach (var p in Plazas)
        {
            V(p.Center);
            w.Write(p.Radius);
        }
        w.Write(Caves.Count);
        foreach (var c in Caves)
        {
            w.Write((int)c.Kind);
            w.Write(c.Poi);
            V(c.Mouth);
            V(c.Facing);
            w.Write(c.Chambers.Count);
            foreach (var (center, radius) in c.Chambers)
            {
                V(center);
                w.Write(radius);
            }
        }
        w.Write(Canopies.Count);
        foreach (var c in Canopies)
        {
            w.Write((int)c.Kind);
            V(c.Center);
            V(c.Axis);
            w.Write(c.HalfLength);
            w.Write(c.HalfWidth);
            w.Write(c.Radius);
            w.Write(c.Bottom);
            w.Write(c.Top);
            w.Write(c.Corridor);
        }
        w.Write(WeakRocks.Count);
        foreach (var r in WeakRocks)
        {
            V(r.Center);
            w.Write(r.Radius);
            w.Write(r.Cave);
            V(r.Facing);
            w.Write(r.Thick);
            w.Write((int)r.Kind);
        }
        w.Write(Decor.Count);
        foreach (var d in Decor)
        {
            w.Write((int)d.Kind);
            V(d.Position);
            w.Write(d.Height);
            w.Write(d.Scale);
            w.Write(d.Yaw);
        }
        w.Write(Kelp.Count);
        foreach (var k in Kelp)
        {
            V(k.Center);
            w.Write(k.Radius);
            w.Write(k.Roots.Count);
            for (int i = 0; i < k.Roots.Count; i++)
            {
                V(k.Roots[i]);
                w.Write(k.Lengths[i]);
            }
        }
        w.Write(Pockets.Count);
        foreach (var p in Pockets)
        {
            V(p.Position);
            w.Write(p.Radius);
        }
        w.Write(Coins.Count);
        foreach (var c in Coins)
        {
            V(c.Position);
            w.Write(c.Amount);
        }
        w.Write(Spawns.Count);
        foreach (var s in Spawns)
        {
            w.Write((int)s.Role);
            w.Write((int)s.Kind);
            w.Write((int)s.Class);
            w.Write(s.Count);
            V(s.Position);
            w.Write(s.Poi);
            w.Write(s.Trap);
            w.Write(s.Path?.Count ?? 0);
            if (s.Path is not null) foreach (var p in s.Path) V(p);
        }
        w.Flush();
        return stream.ToArray();
    }
}

/// <summary>Small 2D geometry helpers for the generator.</summary>
public static class Geo
{
    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        float t = len2 > 1e-9f ? Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Intersection point of segments ab and cd, if they cross.</summary>
    public static bool SegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
    {
        at = default;
        Vector2 r = b - a, s = d - c;
        float denom = r.X * s.Y - r.Y * s.X;
        if (MathF.Abs(denom) < 1e-9f) return false;
        Vector2 ca = c - a;
        float t = (ca.X * s.Y - ca.Y * s.X) / denom;
        float u = (ca.X * r.Y - ca.Y * r.X) / denom;
        if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
        at = a + r * t;
        return true;
    }

    public static Vector2 Perp(Vector2 v) => new(-v.Y, v.X);

    public static Vector2 Normalize(Vector2 v, Vector2 fallback)
    {
        float l = v.Length();
        return l > 1e-6f ? v / l : fallback;
    }
}
