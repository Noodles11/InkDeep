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
    void AddSeaFeatures(Vector2 min, Vector2 max, Vector2 inSea, Func<float, Vector2?> spot)
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
            return;
        }
        var rng = new Rng(Map.Seed ^ 0x5EAF_EA7UL ^ ((ulong)(uint)(int)MathF.Round(inSea.X) << 32) ^ (uint)(int)MathF.Round(inSea.Y));
        var had = (OpticWalls.Count, Flora.Count, LightRooms.Count, Pockets.Count, Fauna.Count, ChargeStones.Count, Springs.Count);
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
        _seaLive[inSea] = mine;
    }

    /// <summary>The window moved on: every chunk's features with it; those of a chunk now out of it altogether parked.</summary>
    void ShiftSeaFeatures(Vector2 by)
    {
        _shifted += by;
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
        }
    }
}
