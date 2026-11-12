using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The paths her light takes on rock (docs/LIGHT-AND-GRAPH.md §11, the light grammar's second words). Where a ray of
/// hers lands on rock, a path pearl gives it new rays born there: real rays of hers that burn, reveal, carry her colours
/// and strike seals. Light is conserved: each passes on less than arrived there.
/// <list type="bullet">
/// <item>Bounce (Mirror Scale): it reflects off the rock, Bounce of its light and reach; a copy, one more bounce.</item>
/// <item>Fork (Mitosis): two short rays fork off at ±ForkDeg, Fork of its light each.</item>
/// <item>Return (Boomerang Shrimp): it comes back along its own way, Return of its light: what stands in it is lit from
///   both sides.</item>
/// <item>Ghost (Ghost Jelly): it passes through a wall up to GhostThick thick, Ghost of its light beyond. It changes
///   what counts as rock: a wall it passes through is not landed on.</item>
/// </list>
/// A born ray may take the other path words where it lands, never its own again (Bounce as many times as she has
/// copies), and a Return's ray takes none. At most MostBorn rays at once.
/// </summary>
public static class PathTuning
{
    public const float Bounce = 0.6f;
    public const float ForkDeg = 35f, Fork = 0.3f, ForkReach = 0.4f, ForkNarrow = 0.6f;
    public const float Return = 0.5f;
    public const float GhostThick = 3f, Ghost = 0.5f;
    public const int MostBorn = 24;
    /// <summary>How far off the rock a born ray starts (so it starts in the open).</summary>
    public const float Off = 0.15f;
    /// <summary>The least reach worth a born ray.</summary>
    public const float Shortest = 0.8f;
}

/// <summary>The path words (one bit each, for what a ray's line has taken).</summary>
[Flags]
public enum PathWord { None = 0, Bounce = 1, Fork = 2, Return = 4, Ghost = 8 }

/// <summary>A ray of hers born where her light landed on rock: by which word, where (on the rock), worth and colour.</summary>
public sealed class BornRay
{
    public required LightEmitter Emitter { get; init; }
    public PathWord Word;
    public Vector2 At;
    public float Worth;
    public LightHue Hue;
    /// <summary>The words its line has taken (it takes none of them again), and its bounces so far.</summary>
    public PathWord Taken;
    public int Bounces;
    /// <summary>Ghost: where it went into the rock (it came out at its emitter's position).</summary>
    public Vector2 Into;
}

public sealed partial class PlaneWorld
{
    readonly List<BornRay> _born = new();
    int _bornCount;

    /// <summary>Her rays born on rock this step (Bounce, Fork, Return, Ghost), in order.</summary>
    public IReadOnlyList<BornRay> Born => _born.GetRange(0, _bornCount);

    /// <summary>Whether a beam of hers is one born on rock (and which).</summary>
    public BornRay? BornOf(LightEmitter beam)
    {
        for (int i = 0; i < _bornCount; i++)
            if (_born[i].Emitter == beam) return _born[i];
        return null;
    }

    /// <summary>How far a line goes from a point before rock (a sealed wall, a pot) stops it, found to a few centimetres.</summary>
    float RockReach(Vector2 from, Vector2 dir, float range, out bool hit)
    {
        hit = false;
        const float step = 0.3f;
        float open = 0f;
        for (float t = step; t < range; t += step)
        {
            if (!Stops(from + dir * t))
            {
                open = t;
                continue;
            }
            float lo = open, hi = t;
            for (int k = 0; k < 5; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Stops(from + dir * mid)) hi = mid;
                else lo = mid;
            }
            hit = true;
            return lo;
        }
        return range;
    }

    /// <summary>
    /// Her rays born on rock (SyncLights, once her beams are placed): each ray of hers that lands on rock takes her path
    /// words there, and the rays born take the others where they land, in turn, up to MostBorn.
    /// </summary>
    void SyncPaths()
    {
        _bornCount = 0;
        var s = Run.Loadout.Shot;
        int bounces = s.Bounces;
        var words = (bounces > 0 ? PathWord.Bounce : 0) | (s.Split ? PathWord.Fork : 0) | (s.Boomerang ? PathWord.Return : 0) | (s.Spectral ? PathWord.Ghost : 0);
        if (words != PathWord.None && !Defeated)
        {
            // Her own rays first (as HerBeams gives them), then each born ray in the order it was born.
            var parents = new List<(LightEmitter Beam, float Worth, LightHue Hue, PathWord Taken, int Bounces)>();
            float tick = BeamTick;
            foreach (var (beam, shots) in HerBeams)
                if (beam.Shining && BornOf(beam) is null) parents.Add((beam, shots / tick, RayHue(beam), PathWord.None, 0));
            for (int p = 0; p < parents.Count && _bornCount < PathTuning.MostBorn; p++)
            {
                var (beam, worth, hue, taken, bounced) = parents[p];
                float len = RockReach(beam.Position, beam.Direction, beam.Range, out bool hit);
                if (!hit) continue;
                var at = beam.Position + beam.Direction * len;
                // Its light where it lands, against what it had at her bell (light is conserved).
                float left = worth * BeamFalloff(beam, len) / LightTuning.NearLight;
                var landed = hue | HueAlong(beam.Position, at);
                // Ghost: through a wall thin enough (the reef's rock, not a seal's nor a pot), and on beyond.
                if ((words & PathWord.Ghost) != 0 && (taken & PathWord.Ghost) == 0 && !_rocks.Exists(r => r.Contains(at + beam.Direction * 0.1f))
                    && VaseAt(at + beam.Direction * 0.1f, 0.05f) is null)
                {
                    float through = -1f;
                    for (float t = 0.1f; t <= PathTuning.GhostThick; t += 0.1f)
                        if (!Stops(at + beam.Direction * t))
                        {
                            through = t;
                            break;
                        }
                    if (through > 0f)
                    {
                        var exit = at + beam.Direction * (through + PathTuning.Off);
                        float reach = beam.Range - len - through;
                        if (reach >= PathTuning.Shortest)
                        {
                            var g = Bear(PathWord.Ghost, at, exit, beam.Direction, reach, beam.HalfAngle, left * PathTuning.Ghost, landed, taken | PathWord.Ghost, bounced);
                            g.Into = at;
                            parents.Add((g.Emitter, g.Worth, g.Hue, g.Taken, g.Bounces));
                        }
                        continue;
                    }
                }
                var back = at - beam.Direction * PathTuning.Off;
                // The way off the rock, as a mirror would send it (straight back off a seal or a pot).
                var n = Map.Gradient(at + beam.Direction * 0.2f);
                n = n.LengthSquared() > 1e-6f ? Vector2.Normalize(n) : beam.Direction;
                var off = SafeNormalize(beam.Direction - 2f * Vector2.Dot(beam.Direction, n) * n);
                if (off == Vector2.Zero) off = -beam.Direction;
                if ((words & PathWord.Bounce) != 0 && bounced < bounces && _bornCount < PathTuning.MostBorn)
                {
                    float reach = beam.Range * PathTuning.Bounce;
                    var b = Bear(PathWord.Bounce, at, back, off, reach, beam.HalfAngle, left * PathTuning.Bounce, landed, taken | PathWord.Bounce, bounced + 1);
                    // (Another bounce off it while she has copies left: Bounce is the one word a line takes again.)
                    if (b.Bounces < bounces) b.Taken &= ~PathWord.Bounce;
                    parents.Add((b.Emitter, b.Worth, b.Hue, b.Taken, b.Bounces));
                }
                if ((words & PathWord.Fork) != 0 && (taken & PathWord.Fork) == 0)
                    for (int side = -1; side <= 1 && _bornCount < PathTuning.MostBorn; side += 2)
                    {
                        // Forked off the rock: the two arms of a Y, either side of the way a mirror would send it.
                        var way = Rotate(off, side * PathTuning.ForkDeg * MathF.PI / 180f);
                        var f = Bear(PathWord.Fork, at, back, way, beam.Range * PathTuning.ForkReach, beam.HalfAngle * PathTuning.ForkNarrow,
                            left * PathTuning.Fork, landed, taken | PathWord.Fork, bounced);
                        parents.Add((f.Emitter, f.Worth, f.Hue, f.Taken, f.Bounces));
                    }
                if ((words & PathWord.Return) != 0 && (taken & PathWord.Return) == 0 && _bornCount < PathTuning.MostBorn && len >= PathTuning.Shortest)
                    // Back along its own way to where it left, lighting what stands in it from the other side; it lands nowhere.
                    Bear(PathWord.Return, at, back, -beam.Direction, len - PathTuning.Off, beam.HalfAngle, left * PathTuning.Return, landed,
                        PathWord.Bounce | PathWord.Fork | PathWord.Return | PathWord.Ghost, bounces);
            }
        }
        // Pearl Necklace (Focus + Fork): at full focus her own beam's tip opens into a ring of rays.
        if (Focus >= 1f && Run.Loadout.Has("pearl_necklace") && Beam.Shining && !Defeated)
        {
            float len = RockReach(Beam.Position, Beam.Direction, Beam.Range, out _);
            var tip = Beam.Position + Beam.Direction * MathF.Max(len - PathTuning.Off, 0f);
            float left = _beamWorth * RhythmBurn * BeamFalloff(Beam, len) / LightTuning.NearLight;
            for (int k = 0; k < RhythmTuning.NecklaceRays && _bornCount < PathTuning.MostBorn; k++)
                Bear(PathWord.Fork, tip, tip, Rotate(Beam.Direction, k * MathF.Tau / RhythmTuning.NecklaceRays + MathF.PI / RhythmTuning.NecklaceRays),
                    Beam.Range * RhythmTuning.NecklaceReach, Beam.HalfAngle * PathTuning.ForkNarrow * 1.4f, left * RhythmTuning.NecklaceShare,
                    RayHue(Beam), PathWord.Bounce | PathWord.Fork | PathWord.Return | PathWord.Ghost, int.MaxValue);
        }
        for (int i = _bornCount; i < _born.Count; i++) _born[i].Emitter.Active = false;
    }

    /// <summary>The next born ray, set out (its emitter made, registered as hers and dark to light-seekers, when first needed).</summary>
    BornRay Bear(PathWord word, Vector2 at, Vector2 from, Vector2 dir, float reach, float half, float worth, LightHue hue, PathWord taken, int bounces)
    {
        if (_bornCount == _born.Count)
            _born.Add(new BornRay
            {
                Emitter = Lights.Add(new LightEmitter
                {
                    Owner = Her, Shape = LightShape.Cone, Intensity = LightTuning.BeamIntensity, Active = false, Drinkable = false,
                }),
            });
        var r = _born[_bornCount++];
        r.Word = word;
        r.At = at;
        r.Worth = worth;
        r.Hue = hue;
        r.Taken = taken;
        r.Bounces = bounces;
        r.Into = default;
        var e = r.Emitter;
        e.Position = from;
        e.Direction = dir;
        e.Range = MathF.Max(reach, 0.1f);
        e.HalfAngle = half;
        e.Intensity = LightTuning.BeamIntensity * Math.Clamp(worth / HerLightTuning.BeamShots, 0.15f, 1f);
        e.Active = true;
        return r;
    }
}
