using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The plants that answer light (docs/proposals/LIGHT-WORLD.md §6, phase 3). Each has a sense (what light it answers,
/// how strong) and verbs (what it does lit, and dark):
/// <list type="bullet">
/// <item>a sun anemone opens its crown in light and closes a while after it has gone (what it hid comes out the first time);</item>
/// <item>a shy anemone snaps shut in strong light and opens slowly in the dark (what it holds she takes only while it is open);</item>
/// <item>a giant kelp grows a stalk while light is held on it (green the fastest), withers back in the dark, and stands in
/// the way of sight: a creature cannot see a light through it;</item>
/// <item>a sunflower sponge stores light held on it and then glows for a long while, a lamp the murklings go to;</item>
/// <item>a night lily opens and glows a faint violet only in the dark (what it gives she takes while it is open);</item>
/// <item>a fluorescent coral drinks high-frequency light (blue the most) and gives it back lower, a green glow (fluorescence).</item>
/// </list>
/// A plant reads the light of the level as it falls on it (every light but the plants' own), and her colours as her light's
/// spectrum carries them.
/// </summary>
public enum FloraKind { SunAnemone, ShyAnemone, GiantKelp, SunflowerSponge, NightLily, FluorCoral }

public sealed class PlaneFlora : ILightOwner
{
    public FloraKind Kind;
    public Vector2 Position;
    /// <summary>A kelp's way of growing (unit).</summary>
    public Vector2 Way = Vector2.UnitX;
    /// <summary>How open it is (anemones, lilies: 0 shut, 1 open).</summary>
    public float Open;
    /// <summary>A kelp's stalk (metres); a sponge's stored light; a coral's and a sponge's glow (0..1, a sponge's in seconds).</summary>
    public float Length, Charge, Glow;
    /// <summary>The light on it this step, and how long a sun anemone stays open yet.</summary>
    public float Lit, Hold;
    /// <summary>Shells it hides or holds (taken once), and the light speck energy it gives (a lily).</summary>
    public int Shells;
    public float Gift;
    /// <summary>Its light room (−1: none).</summary>
    public int Room = -1;
    public float Seed;
    public LightEmitter? Light;

    public float Energy => Light is { Active: true } ? 1f : 0f;
    public float Absorb(float amount) => MathF.Min(MathF.Max(amount, 0f), 0.05f);

    /// <summary>Its stalk's tip (a kelp).</summary>
    public Vector2 Tip => Position + Way * Length;

    /// <summary>Awake as its light room asks: an anemone or a lily open, a kelp grown, a sponge or a coral glowing.</summary>
    public bool Awake => Kind switch
    {
        FloraKind.GiantKelp => Length >= FloraTuning.KelpLength * 0.8f,
        FloraKind.SunflowerSponge => Glow > 0f,
        FloraKind.FluorCoral => Glow >= 0.6f,
        _ => Open >= 0.9f,
    };
}

/// <summary>
/// A light room (LIGHT-WORLD.md §9.2): a ring of one biome's plants round a prize, that opens once they are all awake at
/// once. Atlantis's is an orrery (phase 6): its ring of corals round a quartz heart, and turnable orichalcum mirrors
/// outside the ring; it opens only while the corals glow and the heart is lit by her light thrown off its own mirrors.
/// </summary>
public sealed class LightRoom
{
    public Vector2 Centre;
    public readonly List<PlaneFlora> Plants = new();
    public bool Opened;
    /// <summary>An orrery: its mirrors, and its heart's light (0..1: full lights it).</summary>
    public bool Orrery;
    public readonly List<OpticWall> Mirrors = new();
    public float Heart;
}

/// <summary>The flora's numbers (LIGHT-WORLD.md §6). (tune)</summary>
public static class FloraTuning
{
    /// <summary>The least light a sun anemone answers, how long it stays open after, and how fast it opens and closes (/s).</summary>
    public const float SunSense = 0.3f, SunHold = 5f, SunOpens = 2f, SunCloses = 0.35f;
    /// <summary>The light that shuts a shy anemone, how fast it snaps shut and opens again (/s).</summary>
    public const float ShySense = 0.5f, ShyShuts = 6f, ShyOpens = 0.4f;
    /// <summary>A kelp's stalk at its longest (m), how fast it grows at full light (m/s, × her green share against her plain light's) and withers.</summary>
    public const float KelpLength = 7f, KelpGrow = 1.6f, KelpWither = KelpLength / 20f;
    /// <summary>A sponge: light held on it to fill it (light × seconds), how long it glows, its light's brightness and reach.</summary>
    public const float SpongeFill = 3f, SpongeGlow = 30f, SpongeLight = 0.8f, SpongeReach = 6f;
    /// <summary>A lily: the most light it opens in, how fast it opens and shuts (/s), its light, and the energy it gives.</summary>
    public const float LilyDark = 0.08f, LilyOpens = 0.25f, LilyShuts = 3f, LilyLight = 0.3f, LilyReach = 3f, LilyGift = 0.6f;
    /// <summary>A coral: how much of the light it drinks glows back (× its blue share against her plain light's), how fast the glow fades (/s), its light.</summary>
    public const float CoralFrom = 0.2f, CoralGain = 1.2f, CoralFade = 0.25f, CoralLight = 0.6f, CoralReach = 4f;
    /// <summary>She takes what a plant holds within this of it.</summary>
    public const float Touch = 1.3f;
    /// <summary>Light rooms: how many a level (1 to Rooms), the open water round one, how far from her start and from each other, the ring's radius.</summary>
    public const int Rooms = 2;
    public const float RoomClear = 4.6f, RoomFromStart = 16f, RoomApart = 22f, Ring = 3.2f;
    /// <summary>A room's prize: shells, and light specks' energy.</summary>
    public const int RoomShells = 6;
    public const float RoomSpecks = 3f;
    /// <summary>Loose plants on a level, and the shells an anemone hides.</summary>
    public const int Loose = 6, AnemoneShells = 3;
    /// <summary>An orrery (Atlantis's light room): its mirrors, how far out they stand, the mirror light its heart answers, how fast the heart fills and fades (/s), and its prize's shells.</summary>
    public const int OrreryMirrors = 2, OrreryShells = 12;
    public const float OrreryOut = 4.1f, HeartSense = 0.15f, HeartFill = 1.2f, HeartFade = 0.6f;

    /// <summary>A biome's light rooms are orreries (Atlantis).</summary>
    public static bool Orrery(Biome biome) => biome == Biome.Atlantis;

    /// <summary>A biome's light room plant and how many ring it, and its loose plants.</summary>
    public static (FloraKind Room, int Count, FloraKind[] Loose) For(Biome biome) => biome switch
    {
        Biome.KelpForest => (FloraKind.GiantKelp, 3, new[] { FloraKind.GiantKelp, FloraKind.ShyAnemone }),
        Biome.Shipwreck => (FloraKind.ShyAnemone, 4, new[] { FloraKind.ShyAnemone, FloraKind.SunflowerSponge }),
        Biome.SunkenRuins => (FloraKind.SunflowerSponge, 3, new[] { FloraKind.SunflowerSponge, FloraKind.SunAnemone }),
        Biome.AlienWreckage => (FloraKind.FluorCoral, 4, new[] { FloraKind.FluorCoral }),
        Biome.DrownedTemple => (FloraKind.NightLily, 5, new[] { FloraKind.NightLily, FloraKind.SunflowerSponge }),
        Biome.Atlantis => (FloraKind.FluorCoral, 4, new[] { FloraKind.FluorCoral, FloraKind.SunflowerSponge }),
        _ => (FloraKind.SunAnemone, 5, new[] { FloraKind.SunAnemone, FloraKind.ShyAnemone }),
    };
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's plants that answer light, and its light rooms.</summary>
    public List<PlaneFlora> Flora { get; } = new();
    public List<LightRoom> LightRooms { get; } = new();

    /// <summary>The light on a point from every light but the plants' own (a plant does not wake its neighbours).</summary>
    float LightOnPlant(Vector2 at)
    {
        float sum = 0f;
        foreach (var e in Lights.All)
            if (e.Owner is not PlaneFlora) sum += LightQuery.Brightness(e, at, LineOfSight);
        return sum;
    }

    /// <summary>A band's share of her light against her plain light's (1 for her plain light).</summary>
    float BandShare(LightHue band)
    {
        var s = HerLightPhysics.Spectrum;
        if (s.Total <= 1e-4f) return 1f;
        var h = Spectrum.Hers;
        return band switch
        {
            LightHue.Green => s.G / s.Total / (h.G / h.Total),
            LightHue.Blue => s.B / s.Total / (h.B / h.Total),
            _ => s.R / s.Total / (h.R / h.Total),
        };
    }

    /// <summary>The level's light rooms and loose plants (PlaneOptions.Flora), placed by its own seed in its open water.</summary>
    void PlaceFlora()
    {
        var rng = new Rng(Map.Seed ^ 0xF10AAUL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var (roomKind, ring, loose) = FloraTuning.For(Map.Biome);
        bool Fits(Vector2 p, float r)
        {
            if (!Clear(p, r) || Map.InShaft(p)) return false;
            foreach (var poi in Map.Pois)
                if (poi.Kind != PoiKind.Start && Vector2.Distance(p, poi.Position) < poi.Radius + r + 2f) return false;
            return !Springs.Exists(s => Vector2.Distance(s.Position, p) < r + SpringTuning.Radius + 1f);
        }
        var spots = new List<Vector2>();
        for (float y = LevelMap.RimWidth + 4f; y < Map.Size - LevelMap.RimWidth - 4f; y += 3f)
        for (float x = LevelMap.RimWidth + 4f; x < Map.Size - LevelMap.RimWidth - 4f; x += 3f)
            spots.Add(new Vector2(x, y));
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        int rooms = HasFeature(LightFeature.Rooms) ? 1 + rng.Int(FloraTuning.Rooms) : 0;
        foreach (var p in spots)
        {
            if (LightRooms.Count >= rooms) break;
            if (Vector2.Distance(p, Map.Start.Position) < FloraTuning.RoomFromStart || !Fits(p, FloraTuning.RoomClear) || InQuietWater(p)) continue;
            if (LightRooms.Exists(r => Vector2.Distance(r.Centre, p) < FloraTuning.RoomApart)) continue;
            bool orrery = FloraTuning.Orrery(Map.Biome);
            if (orrery && OpticWalls.Exists(w => w.Distance(p) < FloraTuning.RoomClear + 1f)) continue;
            var room = new LightRoom { Centre = p, Orrery = orrery };
            float turn = rng.Range(0f, MathF.Tau);
            for (int k = 0; k < ring; k++)
            {
                float a = turn + k * MathF.Tau / ring;
                var way = new Vector2(MathF.Cos(a), MathF.Sin(a));
                // (A kelp roots on the ring and grows round it; the rest stand on it.)
                var f = AddFlora(roomKind, p + way * FloraTuning.Ring, rng);
                f.Way = new Vector2(-way.Y, way.X);
                f.Room = LightRooms.Count;
                f.Shells = 0;
                room.Plants.Add(f);
            }
            // An orrery's mirrors, between the ring's plants, each turned its own way (she turns them by pushing an end).
            for (int k = 0; orrery && k < FloraTuning.OrreryMirrors; k++)
            {
                float a = turn + (k * ring / (float)FloraTuning.OrreryMirrors + 0.5f) * MathF.Tau / ring;
                var plate = new OpticWall
                {
                    Kind = OpticKind.Mirror, Centre = p + new Vector2(MathF.Cos(a), MathF.Sin(a)) * FloraTuning.OrreryOut,
                    Angle = rng.Range(0f, MathF.PI), HalfLength = OpticWallTuning.MirrorHalf, HalfThick = OpticWallTuning.MirrorThick,
                    Seed = rng.Range(0f, 100f),
                };
                OpticWalls.Add(plate);
                room.Mirrors.Add(plate);
            }
            LightRooms.Add(room);
        }
        int placed = 0;
        foreach (var p in spots)
        {
            if (placed >= FloraTuning.Loose || !HasFeature(LightFeature.Plants)) break;
            if (Vector2.Distance(p, Map.Start.Position) < 8f || !Fits(p, 1.6f) || !TidySpot(p)) continue;
            if (LightRooms.Exists(r => Vector2.Distance(r.Centre, p) < FloraTuning.RoomClear + 3f) || Flora.Exists(f => Vector2.Distance(f.Position, p) < 9f)) continue;
            var f = AddFlora(loose[rng.Int(loose.Length)], p, rng);
            float a = rng.Range(0f, MathF.Tau);
            f.Way = new Vector2(MathF.Cos(a), MathF.Sin(a));
            placed++;
        }
    }

    /// <summary>A plant of a kind where it is asked (the level's own placing, and verification).</summary>
    public PlaneFlora AddFlora(FloraKind kind, Vector2 at, Rng? rng = null)
    {
        rng ??= new Rng(0xF10AUL + (ulong)Flora.Count);
        var f = new PlaneFlora { Kind = kind, Position = at, Seed = rng.Range(0f, 100f) };
        if (kind is FloraKind.SunAnemone or FloraKind.ShyAnemone) f.Shells = FloraTuning.AnemoneShells;
        if (kind == FloraKind.ShyAnemone) f.Open = 1f;
        if (kind == FloraKind.NightLily) f.Gift = FloraTuning.LilyGift;
        if (kind is FloraKind.SunflowerSponge or FloraKind.NightLily or FloraKind.FluorCoral)
            f.Light = Lights.Add(new LightEmitter
            {
                Owner = f, Position = at, Active = false,
                Range = kind switch { FloraKind.SunflowerSponge => FloraTuning.SpongeReach, FloraKind.NightLily => FloraTuning.LilyReach, _ => FloraTuning.CoralReach },
                Colour = kind switch { FloraKind.SunflowerSponge => new Vector3(1f, 0.85f, 0.45f), FloraKind.NightLily => new Vector3(0.7f, 0.45f, 1f), _ => new Vector3(0.4f, 1f, 0.5f) },
            });
        Flora.Add(f);
        return f;
    }

    /// <summary>The plants' step: each reads the light on it and answers; she takes what an open one holds; a room whose plants are all awake opens.</summary>
    void StepFlora()
    {
        if (Flora.Count == 0) return;
        var her = Player.Position;
        foreach (var f in Flora)
        {
            float lit = f.Lit = LightOnPlant(f.Kind == FloraKind.GiantKelp ? f.Position + f.Way * MathF.Min(f.Length, 1f) : f.Position);
            switch (f.Kind)
            {
                case FloraKind.SunAnemone:
                    if (lit >= FloraTuning.SunSense) f.Hold = FloraTuning.SunHold;
                    else f.Hold = MathF.Max(0f, f.Hold - Dt);
                    f.Open = f.Hold > 0f ? MathF.Min(1f, f.Open + FloraTuning.SunOpens * Dt) : MathF.Max(0f, f.Open - FloraTuning.SunCloses * Dt);
                    // What it hid comes out the first time it opens.
                    if (f.Open >= 0.9f && f.Shells > 0) GiveShells(f);
                    break;
                case FloraKind.ShyAnemone:
                    f.Open = lit >= FloraTuning.ShySense ? MathF.Max(0f, f.Open - FloraTuning.ShyShuts * Dt) : MathF.Min(1f, f.Open + FloraTuning.ShyOpens * Dt);
                    if (f.Open >= 0.9f && f.Shells > 0 && Vector2.Distance(her, f.Position) < FloraTuning.Touch + Radius) GiveShells(f);
                    break;
                case FloraKind.GiantKelp:
                    if (lit >= FloraTuning.SunSense * 0.5f) f.Length = MathF.Min(FloraTuning.KelpLength, f.Length + FloraTuning.KelpGrow * MathF.Min(lit, 1.5f) * BandShare(LightHue.Green) * Dt);
                    else f.Length = MathF.Max(0f, f.Length - FloraTuning.KelpWither * Dt);
                    // (It grows only through open water.)
                    while (f.Length > 0.1f && !Map.IsOpen(f.Tip)) f.Length -= 0.1f;
                    break;
                case FloraKind.SunflowerSponge:
                    if (f.Glow > 0f) f.Glow = MathF.Max(0f, f.Glow - Dt);
                    else
                    {
                        f.Charge = lit > 0.05f ? f.Charge + lit * Dt : MathF.Max(0f, f.Charge - 0.2f * Dt);
                        if (f.Charge >= FloraTuning.SpongeFill)
                        {
                            f.Charge = 0f;
                            f.Glow = FloraTuning.SpongeGlow;
                            Events.Add(new PlaneEvent(PlaneEventType.FloraWoke, f.Position, Vector2.Zero, (int)f.Kind));
                        }
                    }
                    f.Light!.Active = f.Glow > 0f;
                    f.Light.Intensity = FloraTuning.SpongeLight * MathF.Min(1f, f.Glow / 5f);
                    break;
                case FloraKind.NightLily:
                    f.Open = lit <= FloraTuning.LilyDark ? MathF.Min(1f, f.Open + FloraTuning.LilyOpens * Dt) : MathF.Max(0f, f.Open - FloraTuning.LilyShuts * Dt);
                    f.Light!.Active = f.Open > 0.05f;
                    f.Light.Intensity = FloraTuning.LilyLight * f.Open;
                    if (f.Open >= 0.9f && f.Gift > 0f && Vector2.Distance(her, f.Position) < FloraTuning.Touch + Radius)
                    {
                        // (Its gift: a little light, as a speck's.)
                        Run.Energy += f.Gift;
                        Events.Add(new PlaneEvent(PlaneEventType.SpeckGathered, f.Position, Vector2.Zero, f.Gift));
                        f.Gift = 0f;
                    }
                    break;
                case FloraKind.FluorCoral:
                    // Fluorescence: the light it drinks, the more of it high-frequency (blue), glows back lower (green).
                    float drink = lit * BandShare(LightHue.Blue);
                    float want = Math.Clamp((drink - FloraTuning.CoralFrom) * FloraTuning.CoralGain, 0f, 1f);
                    f.Glow = want > f.Glow ? MathF.Min(want, f.Glow + 3f * Dt) : MathF.Max(want, f.Glow - FloraTuning.CoralFade * Dt);
                    f.Light!.Active = f.Glow > 0.02f;
                    f.Light.Intensity = FloraTuning.CoralLight * f.Glow;
                    break;
            }
        }
        foreach (var room in LightRooms)
        {
            if (room.Opened) continue;
            if (room.Orrery)
            {
                float thrown = OrreryLight(room);
                room.Heart = thrown >= FloraTuning.HeartSense ? MathF.Min(1f, room.Heart + FloraTuning.HeartFill * Dt) : MathF.Max(0f, room.Heart - FloraTuning.HeartFade * Dt);
                if (room.Heart < 1f) continue;
            }
            if (room.Plants.Count == 0 || !room.Plants.TrueForAll(p => p.Awake)) continue;
            room.Opened = true;
            Events.Add(new PlaneEvent(PlaneEventType.LightRoomOpened, room.Centre, Vector2.Zero, room.Plants.Count));
            for (int i = 0; i < (room.Orrery ? FloraTuning.OrreryShells : FloraTuning.RoomShells); i++) Shells.Add(FoundShell(room.Centre, ShellWorth.Pot, Fling(2f, 4.5f)));
            if (Options.Specks) DropSpecks(room.Centre, FloraTuning.RoomSpecks);
        }
    }

    /// <summary>An orrery's heart's light: her rays born on its own mirrors (thrown off them), as they fall on it.</summary>
    float OrreryLight(LightRoom room)
    {
        float sum = 0f;
        foreach (var b in Born)
            if (b.Word == PathWord.Mirror && room.Mirrors.Exists(m => m.Distance(b.At) < 0.4f))
                sum += LightQuery.Brightness(b.Emitter, room.Centre, LineOfSight);
        return sum;
    }

    void GiveShells(PlaneFlora f)
    {
        for (int i = 0; i < f.Shells; i++) Shells.Add(FoundShell(f.Position, ShellWorth.Pot, Fling(1.5f, 3f)));
        f.Shells = 0;
        Events.Add(new PlaneEvent(PlaneEventType.FloraGave, f.Position, Vector2.Zero, (int)f.Kind));
    }

    /// <summary>Whether a grown kelp's stalk crosses the line from a to b (it stands in the way of sight).</summary>
    bool KelpBlocks(Vector2 a, Vector2 b)
    {
        foreach (var f in Flora)
        {
            if (f.Kind != FloraKind.GiantKelp || f.Length < 1f) continue;
            if (SegmentsCross(a, b, f.Position, f.Tip)) return true;
        }
        return false;
    }

    static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        var r = p2 - p1;
        var s = q2 - q1;
        float d = Cross(r, s);
        if (MathF.Abs(d) < 1e-6f) return false;
        float t = Cross(q1 - p1, s) / d, u = Cross(q1 - p1, r) / d;
        return t > 0f && t < 1f && u > 0f && u < 1f;
    }
}
