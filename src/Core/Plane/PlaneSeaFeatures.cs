using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// An open level's light features (phase 4): each chunk brings its own the first time it lies whole in the window, and
/// takes them away when it leaves it altogether; they come back with it as she left them (a shattered pane stays
/// shattered, a spent spring spent, an anemone's shells given).
/// </summary>
public sealed partial class PlaneWorld
{
    /// <summary>The chance a chunk holds one of its biome's light features (at half a level's count of it).</summary>
    public const float SeaFeature = 0.5f;

    /// <summary>The area features are placed in: an open level's chunk (AddSeaFeatures), else the whole level.</summary>
    (Vector2 Lo, Vector2 Hi)? _area;
    Rng? _areaRng;

    /// <summary>What features keep clear of: her start, or on an open level wherever she is.</summary>
    Vector2 FeatureStart => _area is null ? Map.Start.Position : Player.Position;

    /// <summary>A level's count of a feature, or a chunk's share of it (half, at least one).</summary>
    int AreaCount(int n) => _area is null || n <= 0 ? n : (n + 1) / 2;

    /// <summary>A chunk's share of a level's count (about SeaChunks to the window), its fraction rolled.</summary>
    static int AreaShare(int n, Rng rng)
    {
        float f = n / SeaChunks;
        return (int)f + (rng.NextFloat() < f - (int)f ? 1 : 0);
    }

    static void Shuffle(List<Vector2> list, Rng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>The chance a sealed chunk's seal takes only coloured light (its prism stones out before it).</summary>
    public const float SeaPrism = 0.4f;
    /// <summary>The prism stones' seals (an open level's seals come and go: their indices are set again from these).</summary>
    readonly Dictionary<PrismStone, SecretSeal> _prismSeal = new();

    /// <summary>A grid of spots through the area, <paramref name="edge"/> in from its sides (from the level's rim on a whole level).</summary>
    IEnumerable<Vector2> GridSpots(float edge, float step)
    {
        var (lo, hi) = _area is { } a
            ? (a.Lo + new Vector2(edge), a.Hi - new Vector2(edge))
            : (new Vector2(LevelMap.RimWidth + edge), new Vector2(Map.Size - LevelMap.RimWidth - edge));
        for (float y = lo.Y; y < hi.Y; y += step)
        for (float x = lo.X; x < hi.X; x += step)
            yield return new Vector2(x, y);
    }

    /// <summary>A chunk's features: its box (in the window plus <see cref="_shifted"/>, so it holds still), and the shift when it was parked.</summary>
    sealed class SeaFeatures
    {
        public readonly List<object> Items = new();
        public Vector2 Lo, Hi, At;
    }

    readonly Dictionary<Vector2, SeaFeatures> _seaLive = new(), _seaParked = new();
    /// <summary>How far the window has moved in all (a point's place in the window plus this holds still in the sea).</summary>
    Vector2 _shifted;

    /// <summary>
    /// A chunk's light features, now it lies whole in the window: those it had, back where they were; else, the first
    /// time, by its chance one of its biome's light features, its share of the level's charge stones, and perhaps a
    /// light spring (one to a window, about).
    /// </summary>
    void AddSeaFeatures(Vector2 min, Vector2 max, Vector2 inSea, Func<float, Vector2?> spot, IReadOnlyList<(Vector2 At, Vector2 Way, float Half)> seals)
    {
        if (_seaLive.ContainsKey(inSea)) return;
        if (_seaParked.Remove(inSea, out var back))
        {
            foreach (var o in back.Items)
            {
                var by = _shifted - back.At;
                MoveFeature(o, by);
                if (LightOf(o) is { } l)
                {
                    l.Position -= by;
                    Lights.Add(l);
                }
                Hold(o, true);
            }
            _seaLive[inSea] = back;
            Reindex();
            return;
        }
        var rng = new Rng(Map.Seed ^ 0x5EAF_EA7UL ^ ((ulong)(uint)(int)MathF.Round(inSea.X) << 32) ^ (uint)(int)MathF.Round(inSea.Y));
        var had = (OpticWalls.Count, Flora.Count, LightRooms.Count, Pockets.Count, Fauna.Count, ChargeStones.Count, Springs.Count);
        var more = (Vases.Count, Vaults.Count, Seals.Count, PrismStones.Count, Trio);
        if (rng.NextFloat() < 1f / SeaChunks && spot(SpringTuning.Clearance) is { } at)
        {
            float pool = SpringTuning.Pool * (1f + OctoShoots.Core.Items.LuckTuning.SpringPool(Map.Luck));
            Springs.Add(new LightSpring { Position = at, Pool = pool, PoolMax = pool });
        }
        var has = BiomeFeatures();
        var was = Features;
        _area = (min, max);
        _areaRng = rng;
        try
        {
            if (rng.NextFloat() < SeaFeature)
            {
                Features = has[rng.Int(has.Count)];
                if (Options.Optics && !Map.Rift) PlaceOpticWalls();
                if (Options.Flora && !Map.Rift) PlaceFlora();
                if (Options.Pockets && !Map.Rift) PlacePockets();
                if (Options.Fauna && !Map.Rift) PlaceFauna();
            }
            float n = ChargeStoneTuning.Count / SeaChunks;
            PlaceChargeStones(min + new Vector2(2f), max - new Vector2(2f), (int)n + (rng.NextFloat() < n - (int)n ? 1 : 0), 300, rng);
            PlaceChargeTrio();
            if (Options.Vases || Map.Biome == OctoShoots.Core.Run.Biome.SunkenRuins) PlaceVases();
            PlaceVaults();
            // A secret room's ways in: a sealed wall across each (its heights are the chunk's own), now and then coloured.
            var beds = new List<Vector2>();
            foreach (var (wayAt, way, half) in seals)
            {
                var rock = new WeakRock(wayAt - way * OpenSea.SealThick, MathF.Min(half + 0.5f, WeakRock.GateHalf + WeakRock.GateMore), -1, way, OpenSea.SealThick, SealKind.Secret);
                Seals.Add(new SecretSeal { Rock = rock, Facing = way, Emblem = rock.Center + way * rock.Thick });
                _rocks.Add(rock);
                if (ColourSeal(Seals.Count - 1, rng, beds, SeaPrism) is { } stones)
                    foreach (var s in stones) _prismSeal[s] = Seals[^1];
            }
        }
        finally
        {
            _area = null;
            _areaRng = null;
            Features = was;
        }
        var mine = new SeaFeatures { Lo = min + _shifted, Hi = max + _shifted };
        mine.Items.AddRange(OpticWalls.GetRange(had.Item1, OpticWalls.Count - had.Item1));
        mine.Items.AddRange(Flora.GetRange(had.Item2, Flora.Count - had.Item2));
        mine.Items.AddRange(LightRooms.GetRange(had.Item3, LightRooms.Count - had.Item3));
        mine.Items.AddRange(Pockets.GetRange(had.Item4, Pockets.Count - had.Item4));
        mine.Items.AddRange(Fauna.GetRange(had.Item5, Fauna.Count - had.Item5));
        mine.Items.AddRange(ChargeStones.GetRange(had.Item6, ChargeStones.Count - had.Item6));
        mine.Items.AddRange(Springs.GetRange(had.Item7, Springs.Count - had.Item7));
        mine.Items.AddRange(Vases.GetRange(more.Item1, Vases.Count - more.Item1));
        mine.Items.AddRange(Vaults.GetRange(more.Item2, Vaults.Count - more.Item2));
        mine.Items.AddRange(Seals.GetRange(more.Item3, Seals.Count - more.Item3));
        mine.Items.AddRange(PrismStones.GetRange(more.Item4, PrismStones.Count - more.Item4));
        if (Trio is { } trio && more.Item5 is null) mine.Items.Add(trio);
        _seaLive[inSea] = mine;
    }

    /// <summary>The window moved on: every chunk's features with it; those of a chunk now out of it altogether parked.</summary>
    void ShiftSeaFeatures(Vector2 by)
    {
        _shifted += by;
        // (Every wall in the sim's rocks is a chunk's: a seal's, a curtain's.)
        for (int i = 0; i < _rocks.Count; i++) _rocks[i] = _rocks[i] with { Center = _rocks[i].Center - by };
        foreach (var s in _seaLive.Values)
            foreach (var o in s.Items) MoveFeature(o, by);
        foreach (var (key, s) in new List<KeyValuePair<Vector2, SeaFeatures>>(_seaLive))
        {
            Vector2 lo = s.Lo - _shifted, hi = s.Hi - _shifted;
            if (hi.X > 0f && hi.Y > 0f && lo.X < Map.Size && lo.Y < Map.Size) continue;
            foreach (var o in s.Items)
            {
                Hold(o, false);
                if (LightOf(o) is { } l) Lights.Remove(l);
            }
            s.At = _shifted;
            _seaLive.Remove(key);
            _seaParked[key] = s;
        }
        Reindex();
    }

    /// <summary>Each prism stone's seal by its place in the seals (they come and go with their chunks).</summary>
    void Reindex()
    {
        foreach (var s in PrismStones)
            if (_prismSeal.TryGetValue(s, out var seal)) s.Seal = Seals.IndexOf(seal);
    }

    /// <summary>A feature into the sim's lists, or out of them.</summary>
    void Hold(object o, bool keep)
    {
        switch (o)
        {
            case OpticWall w: Toggle(OpticWalls, w, keep); break;
            case PlaneFlora f: Toggle(Flora, f, keep); break;
            case LightRoom r: Toggle(LightRooms, r, keep); break;
            case WaterPocket p: Toggle(Pockets, p, keep); break;
            case PlaneFauna f: Toggle(Fauna, f, keep); break;
            case ChargeStone c: Toggle(ChargeStones, c, keep); break;
            case LightSpring s: Toggle(Springs, s, keep); break;
            case PlaneVase v: Toggle(Vases, v, keep); break;
            case PlaneVault v: Toggle(Vaults, v, keep); break;
            case PrismStone s: Toggle(PrismStones, s, keep); break;
            case ChargeTrio t:
                if (keep) Trio = t;
                else if (Trio == t) Trio = null;
                break;
            case SecretSeal s:
                Toggle(Seals, s, keep);
                // (Its wall, or what of it stands once open, comes and goes with it.)
                foreach (var r in s.Open ? s.Rock.Remains() : new[] { s.Rock })
                    if (keep) _rocks.Add(r);
                    else _rocks.Remove(r);
                break;
        }
    }

    static void Toggle<T>(List<T> list, T o, bool keep)
    {
        if (keep) list.Add(o);
        else list.Remove(o);
    }

    static LightEmitter? LightOf(object o) => o switch
    {
        PlaneFlora f => f.Light,
        WaterPocket p => p.Light,
        PlaneFauna f => f.Light,
        ChargeStone c => c.Light,
        _ => null,
    };

    /// <summary>A feature's place in the window moved back by <paramref name="by"/> (its light is the registry's).</summary>
    static void MoveFeature(object o, Vector2 by)
    {
        switch (o)
        {
            case OpticWall w: w.Centre -= by; break;
            case PlaneFlora f: f.Position -= by; break;
            case LightRoom r: r.Centre -= by; break;
            case WaterPocket p: p.Centre -= by; break;
            case PlaneFauna f:
                f.Position -= by;
                f.Home -= by;
                f.Toward -= by;
                break;
            case ChargeStone c:
                c.Position -= by;
                c.Face -= by;
                break;
            case LightSpring s: s.Position -= by; break;
            case PlaneVase v: v.Position -= by; break;
            case PlaneVault v: v.Position -= by; break;
            case PrismStone s:
                s.Position -= by;
                s.Bed -= by;
                break;
            case ChargeTrio t: t.Middle -= by; break;
            case SecretSeal s:
                // (Its wall in the sim's rocks moves with them: only the seal's own copy here.)
                s.Rock = s.Rock with { Center = s.Rock.Center - by };
                s.Emblem -= by;
                break;
        }
    }
}
