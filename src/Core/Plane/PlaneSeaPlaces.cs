using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// An open level's places (OpenSea.PlaceRules, phase 2): each comes into the sim with its chunk (AddSeaPlace), set up as
/// it would be on any level, only one at a time; and everything of them moves with the window (ShiftWindow).
/// </summary>
public sealed partial class PlaneWorld
{
    /// <summary>
    /// A chunk's place, come into the window: a place on the map (its index), and what it holds: a clam with its pearl or
    /// murkling, a cache's shells, a shop's or a shrine's stands, a crab's deal, a relic (or a pearl if she has it). A
    /// hole is the map's own (OpenSea.Fill). Seeded by where it lies in the sea, so the same chunk holds the same.
    /// </summary>
    public int AddSeaPlace(PoiKind kind, Vector2 at, Vector2 inSea)
    {
        int pi = Map.Pois.Count;
        var poi = new Poi { Kind = kind, Position = at, Radius = SeaPlaceRadius };
        Map.Pois.Add(poi);
        Visited = Grown(Visited);
        Revealed = Grown(Revealed);
        var rng = new Rng(Map.Seed ^ 0x5EA9_1ACEUL ^ ((ulong)(uint)(int)MathF.Round(inSea.X) << 32) ^ (uint)(int)MathF.Round(inSea.Y));
        switch (kind)
        {
            case PoiKind.TreasureCave:
                PlaceClam(pi, rng, rng);
                break;
            case PoiKind.ShellCache:
                PlaceCache(poi);
                break;
            case PoiKind.Shop:
                ShopAt(poi, Vector2.UnitY, rng);
                break;
            case PoiKind.Shrine:
                ShrineAt(poi, rng);
                break;
            case PoiKind.CrabDeal:
                CrabAt ??= at - Vector2.UnitY * CrabTuning.Back;
                DealAt(poi, Vector2.UnitY, rng);
                break;
            case PoiKind.Relic:
                RelicAt(poi, rng);
                break;
        }
        return pi;
    }

    /// <summary>How far round its middle an open level's place reaches (she has found it within this, and one more metre).</summary>
    public const float SeaPlaceRadius = 6f;

    static bool[] Grown(bool[] a)
    {
        var b = new bool[a.Length + 1];
        Array.Copy(a, b, a.Length);
        return b;
    }

    /// <summary>
    /// An open level's window moved on by <paramref name="by"/> (its map refilled for its new place, OpenSea.Fill): she and
    /// everything on the level keep their place in the sea, so their place in the window moves back by as much; her shots
    /// in flight are let go.
    /// </summary>
    public void ShiftWindow(Vector2 by)
    {
        Player.Position -= by;
        Player.PrevPosition -= by;
        Shots.Clear();
        Clouds.Clear();
        foreach (var poi in Map.Pois) poi.Position -= by;
        foreach (var c in Clams) c.Position -= by;
        foreach (var q in Pearls) q.Position -= by;
        foreach (var s in Shells) s.Position -= by;
        foreach (var h in Hearts) h.Position -= by;
        foreach (var s in Stands) s.Position -= by;
        foreach (var r in Relics) r.Position -= by;
        if (CrabAt is { } crab) CrabAt = crab - by;
    }
}
