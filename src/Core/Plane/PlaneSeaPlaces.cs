using System;
using System.Collections.Generic;
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

    /// <summary>
    /// A chunk's life, once it lies whole in the window (its heights there): the reef's own fish, each kind by its chance
    /// (SeaLife), at open spots of it apart from its place and from her. Seeded by where it lies in the sea.
    /// </summary>
    /// <param name="ways">Its ways out (in the window): where, which way out, the way's half width, whether it is this chunk's (a curtain's).</param>
    /// <param name="sealed">It is a secret room: its ways sealed.</param>
    public void AddSeaLife(Vector2 min, Vector2 max, Vector2 inSea, Vector2? place,
        IReadOnlyList<(Vector2 At, Vector2 Way, float Half, bool Own)>? ways = null, bool @sealed = false)
    {
        ways ??= Array.Empty<(Vector2, Vector2, float, bool)>();
        var rng = new Rng(Map.Seed ^ 0x11FE_5EAUL ^ ((ulong)(uint)(int)MathF.Round(inSea.X) << 32) ^ (uint)(int)MathF.Round(inSea.Y));
        var taken = new System.Collections.Generic.List<Vector2>();
        if (place is { } pl) taken.Add(pl);
        Vector2? Spot(float room, bool byRock)
        {
            for (int t = 0; t < 24; t++)
            {
                var q = new Vector2(rng.Range(min.X + 2f, max.X - 2f), rng.Range(min.Y + 2f, max.Y - 2f));
                if (!Clear(q, room) || byRock && Clear(q, room + 2.5f) || Map.InShaft(q, 3f)) continue;
                if (Vector2.Distance(q, Player.Position) < SeaLifeClear || taken.Exists(o => Vector2.Distance(o, q) < 8f)) continue;
                taken.Add(q);
                return q;
            }
            return null;
        }
        // Its light features first (they keep their own dice: phase 4, PlaneSeaFeatures.cs).
        AddSeaFeatures(min, max, inSea, r => Spot(r, false), @sealed ? ways.Select(w => (w.At, w.Way, w.Half)).ToList() : new List<(Vector2, Vector2, float)>());
        // The Kelp Forest's curtains: across some of its ways (each way is one chunk's), never a secret room's.
        if (HasHazards(OctoShoots.Core.Run.Biome.KelpForest) && !@sealed)
            foreach (var (at, way, half, own) in ways)
                if (own && rng.NextFloat() < SeaCurtain && !Curtains.Exists(c => Vector2.Distance(c.Rock.Center, at) < 1f))
                {
                    var rock = new WeakRock(at, half + HazardTuning.CurtainPast, -1, way, HazardTuning.CurtainThick, SealKind.Path);
                    Curtains.Add(new PlaneKelpCurtain { Rock = rock });
                    _rocks.Add(rock);
                }
        if (rng.NextFloat() < SeaAnemone && Spot(ReefFishTuning.ClownRange, true) is { } a) AddAnemone(a, rng);
        if (rng.NextFloat() < SeaTangs && Spot(ReefFishTuning.TangRadius + 0.6f, false) is { } t) AddTangs(t, 1 + rng.Int(2), rng);
        // (A school the adventure's size: a template level's few big ones would crowd a sea of chunks.)
        if (rng.NextFloat() < SeaSchool && Spot(2f, false) is { } s) AddSchool(s, AdventureTuning.SchoolMin + rng.Int(AdventureTuning.SchoolMax - AdventureTuning.SchoolMin + 1), rng);
        if (rng.NextFloat() < SeaPuffers && Spot(PufferlingTuning.CalmRadius + 0.5f, false) is { } f) AddPufferlings(f, 1 + rng.Int(3), rng);
        // Its biome's traps (the Ruins' darts, the Shipwreck's cannons), as many to the window as a level holds.
        bool ruins = HasHazards(OctoShoots.Core.Run.Biome.SunkenRuins), wreck = HasHazards(OctoShoots.Core.Run.Biome.Shipwreck);
        if ((ruins || wreck) && rng.NextFloat() < (ruins ? HazardTuning.DartTraps : HazardTuning.Cannons) / SeaChunks)
            for (int k = 0; k < 40 && !TryTrap(new Vector2(rng.Range(min.X, max.X), rng.Range(min.Y, max.Y)), Player.Position, rng, wreck); k++) { }
        // Its light specks, the first time only (those she gathers never come back), as many to the window as a level holds.
        if (!Options.Specks || !_specked.Add(inSea)) return;
        var (want, worth) = SpeckCount();
        float n = want / SeaChunks;
        int count = (int)n + (rng.NextFloat() < n - (int)n ? 1 : 0);
        for (int i = 0; i < count && Spot(SpeckTuning.Clearance, false) is { } at; i++) AddSpeck(at, rng.Range(SpeckTuning.Least, SpeckTuning.Most) * worth, Vector2.Zero);
    }

    /// <summary>The chance a chunk's way holds a kelp curtain (the Kelp Forest).</summary>
    public const float SeaCurtain = 0.2f;
    /// <summary>About how many chunks' life lies in the window at once (each chunk brings its share of a level's traps and specks).</summary>
    const float SeaChunks = 12f;
    /// <summary>The chunks (by where they lie in the sea) whose specks have come.</summary>
    readonly System.Collections.Generic.HashSet<Vector2> _specked = new();

    /// <summary>
    /// How often a chunk holds each kind of the reef's fish (an anemone with its clownfish, tangs, a chromis school,
    /// pufferlings): about as many to the window as a template level holds.
    /// </summary>
    public const float SeaAnemone = 0.5f, SeaTangs = 0.5f, SeaSchool = 0.2f, SeaPuffers = 0.35f;
    /// <summary>No fish comes in this near her (metres).</summary>
    const float SeaLifeClear = 10f;

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
        foreach (var f in Fish) Moved(f, by);
        foreach (var f in ReefFish) Moved(f, by);
        foreach (var a in Anemones) a.Position -= by;
        foreach (var s in Schools)
        {
            s.Position -= by;
            s.Home -= by;
            s.Target -= by;
        }
        foreach (var m in Murklings)
        {
            m.Position -= by;
            m.Target -= by;
            if (m.Home is { } h) m.Home = h - by;
        }
        foreach (var s in MurkShots)
        {
            s.Position -= by;
            s.From -= by;
        }
        foreach (var e in Lights.All) e.Position -= by;
        foreach (var s in Specks) s.Position -= by;
        foreach (var t in DartTraps) t.Position -= by;
        foreach (var c in Curtains) c.Rock = c.Rock with { Center = c.Rock.Center - by };
        Darts.Clear();
        Debris.Clear();
        for (int i = 0; i < _trail.Count; i++) _trail[i] = (_trail[i].At - by, _trail[i].Time);
        ShiftSeaFeatures(by);
        DropOutside();
    }

    /// <summary>
    /// The fish beyond the window let go (each with its anemone's or school's), the rest re-pointed at theirs: what lies
    /// out of the window is rock to the sim, and a chunk's fish come back with it (AddSeaLife).
    /// </summary>
    void DropOutside()
    {
        bool Out(Vector2 p) => p.X < 0f || p.Y < 0f || p.X > Map.Size || p.Y > Map.Size;
        Fish.RemoveAll(f => Out(f.Position));
        Murklings.RemoveAll(m => !m.Clinging && Out(m.Position));
        DartTraps.RemoveAll(t => Out(t.Position));
        foreach (var c in Curtains.Where(c => Out(c.Rock.Center)).ToList())
        {
            _rocks.Remove(c.Rock);
            Curtains.Remove(c);
        }
        foreach (var s in Specks.Where(s => Out(s.Position)).ToList())
        {
            Lights.Remove(s.Light);
            Specks.Remove(s);
        }
        var anemones = Anemones.Select(a => Out(a.Position) ? -1 : 0).ToArray();
        var schools = Schools.Select(sc => Out(sc.Home) ? -1 : 0).ToArray();
        int n = 0;
        for (int i = 0; i < anemones.Length; i++) if (anemones[i] == 0) anemones[i] = n++;
        n = 0;
        for (int i = 0; i < schools.Length; i++) if (schools[i] == 0) schools[i] = n++;
        ReefFish.RemoveAll(f => f.Kind switch
        {
            ReefFishKind.Clownfish => anemones[f.Host] < 0,
            ReefFishKind.Chromis => schools[f.Host] < 0,
            _ => Out(f.Position),
        });
        foreach (var f in ReefFish)
            f.Host = f.Kind switch { ReefFishKind.Clownfish => anemones[f.Host], ReefFishKind.Chromis => schools[f.Host], _ => f.Host };
        for (int i = anemones.Length - 1; i >= 0; i--) if (anemones[i] < 0) Anemones.RemoveAt(i);
        for (int i = schools.Length - 1; i >= 0; i--) if (schools[i] < 0) Schools.RemoveAt(i);
    }

    static void Moved(PlaneSwimmer f, Vector2 by)
    {
        f.Position -= by;
        f.Home -= by;
        f.Target -= by;
    }
}
