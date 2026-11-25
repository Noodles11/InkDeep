using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Local water (docs/proposals/LIGHT-WORLD.md §3.2, phase 4): pockets that break a biome's rule, round discs of it in the
/// open water (the halocline, a layer her light bends crossing, is an optic wall: PlaneOpticWalls.cs).
/// <list type="bullet">
/// <item>a brine pool (the Temple): its hard surface hides what lies across it: a creature cannot see a light through it;</item>
/// <item>a silt cloud (the Shipwreck, the Ruins): her dash kicks one up where she is, and some lie on the floor already; it
/// smears light, so no creature sees a light through it (nor her light anything past it), settling over SiltLife seconds;</item>
/// <item>vent shimmer (the Alien Wreckage, Atlantis): hot water whose index trembles: her shots crossing it wobble off their way;</item>
/// <item>a plankton bloom (the Reef, the Kelp): lit, its plankton glow back, a light of its own (the murklings come to it, a
/// decoy), fading a while after.</item>
/// </list>
/// </summary>
public enum PocketKind { Brine, Silt, Vent, Plankton }

public sealed class WaterPocket : ILightOwner
{
    public PocketKind Kind;
    public Vector2 Centre;
    public float Radius;
    /// <summary>A silt cloud's life left (seconds; the floor's own never settle: +∞), a bloom's glow (0..1).</summary>
    public float Life = float.PositiveInfinity, Glow;
    public float Seed;
    public LightEmitter? Light;

    public float Energy => Glow;
    public float Absorb(float amount)
    {
        float taken = MathF.Min(MathF.Max(amount, 0f), Glow);
        Glow -= taken * 0.5f;
        return taken;
    }

    /// <summary>How thick it is now (0..1): a silt cloud thins as it settles.</summary>
    public float Thick => Kind == PocketKind.Silt ? Math.Clamp(Life / 2f, 0f, 1f) : 1f;

    /// <summary>Whether the line from a to b passes through it (its middle, by its radius).</summary>
    public bool Crosses(Vector2 a, Vector2 b, float shrink = 0.8f)
    {
        var d = b - a;
        float len2 = d.LengthSquared();
        float t = len2 < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(Centre - a, d) / len2, 0f, 1f);
        // (Not when either end is inside: a creature in the cloud sees what is round it.)
        float r = Radius * shrink;
        if (Vector2.DistanceSquared(a, Centre) < r * r || Vector2.DistanceSquared(b, Centre) < r * r) return false;
        return Vector2.DistanceSquared(a + d * t, Centre) < r * r;
    }
}

/// <summary>The local water's numbers (LIGHT-WORLD.md §3.2). (tune)</summary>
public static class PocketTuning
{
    /// <summary>A pocket's radius (least to most).</summary>
    public const float RadiusMin = 2.5f, RadiusMax = 4f;
    /// <summary>A silt cloud her dash kicks up: its radius and life (seconds), and its thickness at which it hides (0..1).</summary>
    public const float DashSilt = 2.6f, SiltLife = 6f, SiltHides = 0.35f;
    /// <summary>Vent shimmer: how hard a shot wobbles crossing it (radians a second, either way).</summary>
    public const float Wobble = 2.5f;
    /// <summary>A plankton bloom: the light that wakes it, its glow's light and how fast it fades (/s).</summary>
    public const float BloomSense = 0.3f, BloomLight = 0.7f, BloomFade = 0.3f;
    /// <summary>How far from her start pockets lie, and from each other.</summary>
    public const float FromStart = 9f, Apart = 9f;

    /// <summary>A biome's pockets in a level: (kind, how many).</summary>
    public static (PocketKind Kind, int Count)[] For(Biome biome) => biome switch
    {
        Biome.SunlitReef => new[] { (PocketKind.Plankton, 3) },
        Biome.KelpForest => new[] { (PocketKind.Plankton, 3) },
        Biome.Shipwreck => new[] { (PocketKind.Silt, 3) },
        Biome.SunkenRuins => new[] { (PocketKind.Silt, 2) },
        Biome.AlienWreckage => new[] { (PocketKind.Vent, 3) },
        Biome.DrownedTemple => new[] { (PocketKind.Brine, 3) },
        Biome.Atlantis => new[] { (PocketKind.Vent, 2), (PocketKind.Brine, 1) },
        _ => Array.Empty<(PocketKind, int)>(),
    };

    /// <summary>Her dash kicks up silt here.</summary>
    public static bool DashSilts(Biome biome) => biome is Biome.Shipwreck or Biome.SunkenRuins;
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's local water.</summary>
    public List<WaterPocket> Pockets { get; } = new();

    Rng? _pocketRng;
    Rng PocketRng => _pocketRng ??= new Rng(Map.Seed ^ 0x90C4E7UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));

    /// <summary>The level's pockets (PlaneOptions.Pockets), in its open water by its own seed.</summary>
    void PlacePockets()
    {
        var rng = _areaRng ?? PocketRng;
        var spots = new List<Vector2>();
        foreach (var p in GridSpots(4f, 3f))
            if (Clear(p, 2f) && !Map.InShaft(p) && Vector2.Distance(p, FeatureStart) >= PocketTuning.FromStart) spots.Add(p);
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        int next = 0;
        if (!HasFeature(LightFeature.Pockets)) return;
        foreach (var (kind, count) in PocketTuning.For(Map.Biome))
            for (int k = 0; k < AreaCount(count) && next < spots.Count; next++)
            {
                var p = spots[next];
                if (Pockets.Exists(o => Vector2.Distance(o.Centre, p) < PocketTuning.Apart) || !TidySpot(p)) continue;
                AddPocket(kind, p, rng.Range(PocketTuning.RadiusMin, PocketTuning.RadiusMax));
                k++;
            }
    }

    /// <summary>A pocket of local water (its light registered, a plankton bloom's).</summary>
    public WaterPocket AddPocket(PocketKind kind, Vector2 at, float radius, float life = float.PositiveInfinity)
    {
        var w = new WaterPocket { Kind = kind, Centre = at, Radius = radius, Life = life, Seed = PocketRng.Range(0f, 100f) };
        if (kind == PocketKind.Plankton)
            w.Light = Lights.Add(new LightEmitter { Owner = w, Position = at, Range = radius + 2.5f, Active = false, Colour = new Vector3(0.45f, 0.95f, 1f) });
        Pockets.Add(w);
        return w;
    }

    /// <summary>
    /// The pockets' step: a silt cloud settles (gone when it has); her dash kicks one up (where silt lies); a bloom glows
    /// in the light on it and fades after; vent shimmer wobbles her shots crossing it.
    /// </summary>
    void StepPockets()
    {
        if (Options.Pockets && Map.Templated && PocketTuning.DashSilts(Map.Biome))
            foreach (var e in Events)
                if (e.Type == PlaneEventType.DashStarted)
                {
                    AddPocket(PocketKind.Silt, Player.Position, PocketTuning.DashSilt, PocketTuning.SiltLife);
                    _siltRaised = true;
                }
        if (_siltRaised)
        {
            Events.Add(new PlaneEvent(PlaneEventType.SiltRaised, Player.Position, Vector2.Zero, PocketTuning.DashSilt));
            _siltRaised = false;
        }
        for (int i = Pockets.Count - 1; i >= 0; i--)
        {
            var w = Pockets[i];
            switch (w.Kind)
            {
                case PocketKind.Silt:
                    w.Life -= Dt;
                    if (w.Life <= 0f) Pockets.RemoveAt(i);
                    break;
                case PocketKind.Plankton:
                {
                    float lit = 0f;
                    foreach (var e in Lights.All)
                        if (e.Owner != w) lit = MathF.Max(lit, LightQuery.Brightness(e, w.Centre, LineOfSight));
                    bool was = w.Glow > 0.5f;
                    w.Glow = lit >= PocketTuning.BloomSense ? MathF.Min(1f, w.Glow + 2f * Dt) : MathF.Max(0f, w.Glow - PocketTuning.BloomFade * Dt);
                    if (!was && w.Glow > 0.5f) Events.Add(new PlaneEvent(PlaneEventType.BloomLit, w.Centre, Vector2.Zero, w.Radius));
                    w.Light!.Active = w.Glow > 0.02f;
                    w.Light.Intensity = PocketTuning.BloomLight * w.Glow;
                    break;
                }
                case PocketKind.Vent:
                    foreach (var s in Shots)
                    {
                        if (!s.FromPlayer || s.Life <= 0f || Vector2.DistanceSquared(s.Position, w.Centre) > w.Radius * w.Radius) continue;
                        float a = PocketRng.Range(-1f, 1f) * PocketTuning.Wobble * Dt;
                        var v = s.Velocity;
                        s.Velocity = new Vector2(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));
                    }
                    break;
            }
        }
    }

    bool _siltRaised;

    /// <summary>Whether local water hides b from a: a brine pool's surface, or silt still thick enough, lies between them.</summary>
    bool PocketHides(Vector2 a, Vector2 b)
    {
        foreach (var w in Pockets)
        {
            if (w.Kind == PocketKind.Brine || w.Kind == PocketKind.Silt && w.Thick >= PocketTuning.SiltHides)
                if (w.Crosses(a, b)) return true;
        }
        return false;
    }
}
