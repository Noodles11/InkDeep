using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A wall made for her light (LIGHT-WORLD.md §4.2): a mirror plate, a pane of lightglass; (phase 4) sun-stone, there only
/// while it is lit; shadow-stone, there only in the dark; and a halocline, a layer of denser water her light bends
/// crossing (and is thrown back from at a grazing angle), which nothing else feels.
/// </summary>
public enum OpticKind { Mirror, Lightglass, SunStone, ShadowStone, Halocline }

/// <summary>
/// A mirror plate or a lightglass pane standing in the water: a thin slab (Centre, its long way at Angle, HalfLength by
/// HalfThick). Her body and the creatures cannot pass it. A mirror throws her light back whole and turns when she pushes
/// its end; lightglass lets through only its own band of her light, and rung long enough by it, it shatters.
/// </summary>
public sealed class OpticWall
{
    public OpticKind Kind;
    public Vector2 Centre;
    public float Angle, HalfLength, HalfThick;
    /// <summary>Lightglass: the one band it passes (red, green or blue).</summary>
    public LightHue Band;
    /// <summary>Lightglass: how much of its band it has taken (shatters at OpticWallTuning.Shatter); a mirror: how fast it turns (rad/s).</summary>
    public float Charge, Spin;
    public bool Shattered;
    public float Seed;
    /// <summary>A stone: how much it stands now (0 gone .. 1 solid), the light on it, and how long it holds as it is.</summary>
    public float Solid = 1f, Lit, Hold;

    /// <summary>It stands now: a stone while solid enough, the rest always (unless shattered).</summary>
    public bool Stands => !Shattered && (Kind is not (OpticKind.SunStone or OpticKind.ShadowStone) || Solid >= 0.5f);

    /// <summary>Bodies cannot pass it (a halocline is water: only her light feels it).</summary>
    public bool BlocksBodies => Kind != OpticKind.Halocline;

    public Vector2 Axis => new(MathF.Cos(Angle), MathF.Sin(Angle));
    /// <summary>Its face's normal (either side: the other is its negative).</summary>
    public Vector2 Normal => new(-MathF.Sin(Angle), MathF.Cos(Angle));

    /// <summary>How far a point is outside the slab (negative inside).</summary>
    public float Distance(Vector2 p)
    {
        var d = p - Centre;
        float along = Math.Clamp(Vector2.Dot(d, Axis), -HalfLength, HalfLength);
        return Vector2.Distance(p, Centre + Axis * along) - HalfThick;
    }

    public bool Contains(Vector2 p, float radius = 0f) => Stands && Distance(p) < radius;

    /// <summary>Its end points.</summary>
    public (Vector2 A, Vector2 B) Ends => (Centre - Axis * HalfLength, Centre + Axis * HalfLength);
}

/// <summary>Mirror plates and lightglass (LIGHT-WORLD.md §4.2, phase 2). (tune)</summary>
public static class OpticWallTuning
{
    /// <summary>A mirror plate's half length and half thickness; lightglass's half thickness.</summary>
    public const float MirrorHalf = 1.3f, MirrorThick = 0.2f, GlassThick = 0.22f;
    /// <summary>What a mirror plate throws back of her light, and a shot's power.</summary>
    public const float MirrorKeep = 0.95f;
    /// <summary>How wide a passage lightglass is set across (metres), and how far from her start anything is placed.</summary>
    public const float GlassWidthMin = 2.5f, GlassWidthMax = 7f, StartClear = 9f, Spacing = 11f;
    /// <summary>How much of its band a pane takes before it shatters (her full beam of its colour: about Shatter / 2 seconds), and how fast it loses it unlit.</summary>
    public const float Shatter = 1.2f, Settle = 0.1f, BeamCharge = 3f, ShotCharge = 0.3f;
    /// <summary>A mirror plate turns as she pushes its end (rad/s per m/s of her push across it at a metre out), and its water's drag on the turn.</summary>
    public const float Push = 1.1f, TurnDrag = 3f;
    /// <summary>The least share of her light a wall's meeting must give a born ray (the biome's own rock).</summary>
    public const float LeastShare = 0.12f;
    /// <summary>Most wall glows her beam keeps at once.</summary>
    public const int MostGlows = 16;
    /// <summary>The shells a shattered pane leaves.</summary>
    public const int ShatterShells = 2;

    /// <summary>A stone's half thickness, the light it answers, how long it holds (sun-stone lit, shadow-stone gone) after the light leaves, how fast it stands and fades (/s).</summary>
    public const float StoneThick = 0.3f, StoneSense = 0.25f, SunHold = 1.5f, ShadowHold = 1.2f, StoneRise = 3f, StoneFade = 4f;
    /// <summary>A halocline: its half length and half thickness, how much denser (in index) its water is, and what of her light goes on through it.</summary>
    public const float HaloHalf = 4.5f, HaloThick = 0.15f, HaloIndex = 0.12f, HaloKeep = 0.9f;

    /// <summary>A biome's sun-stones, shadow-stones and haloclines in a level (LIGHT-WORLD.md §3.2, §4.2).</summary>
    public static (int Sun, int Shadow, int Halo) Stones(Biome biome) => biome switch
    {
        Biome.SunkenRuins => (1, 2, 0),
        Biome.DrownedTemple => (1, 1, 2),
        Biome.Atlantis => (1, 1, 2),
        _ => (0, 0, 0),
    };

    /// <summary>A biome's mirror plates and lightglass panes in a level.</summary>
    public static (int Mirrors, int Panes) For(Biome biome) => biome switch
    {
        Biome.Shipwreck => (3, 0),
        Biome.DrownedTemple => (4, 1),
        Biome.AlienWreckage => (1, 4),
        Biome.Atlantis => (4, 2),
        _ => (0, 0),
    };
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's mirror plates and lightglass panes.</summary>
    public List<OpticWall> OpticWalls { get; } = new();

    /// <summary>The standing optic wall at a point (within radius), if any (<paramref name="bodies"/>: only those bodies cannot pass).</summary>
    public OpticWall? OpticAt(Vector2 p, float radius = 0f, bool bodies = false)
    {
        for (int i = 0; i < OpticWalls.Count; i++)
            if (OpticWalls[i].Contains(p, radius) && (!bodies || OpticWalls[i].BlocksBodies)) return OpticWalls[i];
        return null;
    }

    /// <summary>
    /// The first optic wall a step from a to b crosses (a fast shot steps over a thin slab between samples), and the
    /// point on the step just before it.
    /// </summary>
    public OpticWall? OpticCrossed(Vector2 a, Vector2 b, out Vector2 before)
    {
        before = b;
        OpticWall? first = null;
        float best = float.MaxValue;
        var d = b - a;
        float len = d.Length();
        if (len < 1e-6f) return null;
        foreach (var w in OpticWalls)
        {
            if (!w.Stands) continue;
            var n = w.Normal;
            float s0 = Vector2.Dot(a - w.Centre, n), s1 = Vector2.Dot(b - w.Centre, n);
            float t;
            if (MathF.Abs(s0) <= w.HalfThick) t = 0f;
            else if (MathF.Abs(s1) > w.HalfThick && MathF.Sign(s0) == MathF.Sign(s1)) continue;
            else t = Math.Clamp((MathF.Abs(s0) - w.HalfThick) / MathF.Max(MathF.Abs(s0 - s1), 1e-6f), 0f, 1f);
            var at = a + d * t;
            if (MathF.Abs(Vector2.Dot(at - w.Centre, w.Axis)) > w.HalfLength || t >= best) continue;
            best = t;
            first = w;
            before = a + d * MathF.Max(0f, t - 0.05f / len);
        }
        return first;
    }

    /// <summary>The share of a light a pane lets through: its band's share of the light's energy.</summary>
    public static float PassShare(OpticWall pane, Spectrum light)
    {
        float band = pane.Band switch { LightHue.Red => light.R, LightHue.Green => light.G, _ => light.B };
        return light.Total <= 1e-6f ? 0f : band / light.Total;
    }

    /// <summary>
    /// Mirror plates in open water and lightglass panes across passages (a biome's counts, OpticWallTuning.For), placed by
    /// the level's own seed, clear of her start, the level's places and each other.
    /// </summary>
    void PlaceOpticWalls()
    {
        var (mirrors, panes) = OpticWallTuning.For(Map.Biome);
        var (sunStones, shadowStones, haloclines) = OpticWallTuning.Stones(Map.Biome);
        if (!HasFeature(LightFeature.Mirrors)) mirrors = 0;
        if (!HasFeature(LightFeature.Panes)) panes = 0;
        if (!HasFeature(LightFeature.Stones)) sunStones = shadowStones = 0;
        if (!HasFeature(LightFeature.Haloclines)) haloclines = 0;
        if (mirrors + panes + sunStones + shadowStones + haloclines == 0) return;
        var rng = new Rng(Map.Seed ^ 0x0971C5UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var spots = new List<Vector2>();
        for (float y = LevelMap.RimWidth + 3f; y < Map.Size - LevelMap.RimWidth - 3f; y += 2.5f)
        for (float x = LevelMap.RimWidth + 3f; x < Map.Size - LevelMap.RimWidth - 3f; x += 2.5f)
        {
            var p = new Vector2(x, y);
            if (Fits(p, 1.6f)) spots.Add(p);
        }
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        LightHue[] bands = { LightHue.Red, LightHue.Green, LightHue.Blue };
        foreach (var p in spots)
        {
            if (panes <= 0 && mirrors <= 0 && sunStones <= 0 && shadowStones <= 0 && haloclines <= 0) break;
            if (OpticWalls.Exists(w => Vector2.Distance(w.Centre, p) < OpticWallTuning.Spacing) || InQuietWater(p)) continue;
            // Stones across passages (phase 4): sun-stone, there only while lit; shadow-stone, there only in the dark.
            if ((sunStones > 0 || shadowStones > 0) && Across(p) is { } stone && !InQuietWater(stone.Centre))
            {
                bool sun = sunStones > 0 && (shadowStones <= 0 || rng.Int(2) == 0);
                stone.Kind = sun ? OpticKind.SunStone : OpticKind.ShadowStone;
                stone.HalfThick = OpticWallTuning.StoneThick;
                stone.Solid = sun ? 0f : 1f;
                stone.Seed = rng.Range(0f, 100f);
                OpticWalls.Add(stone);
                if (sun) sunStones--;
                else shadowStones--;
                continue;
            }
            // A halocline lying across open water (phase 4).
            if (haloclines > 0 && Fits(p, OpticWallTuning.HaloHalf * 0.6f))
            {
                OpticWalls.Add(new OpticWall
                {
                    Kind = OpticKind.Halocline, Centre = p, Angle = rng.Range(0f, MathF.PI), HalfLength = OpticWallTuning.HaloHalf,
                    HalfThick = OpticWallTuning.HaloThick, Seed = rng.Range(0f, 100f),
                });
                haloclines--;
                continue;
            }
            if (panes > 0 && Across(p) is { } pane && !InQuietWater(pane.Centre))
            {
                pane.Band = bands[rng.Int(3)];
                pane.Seed = rng.Range(0f, 100f);
                OpticWalls.Add(pane);
                panes--;
                continue;
            }
            if (mirrors <= 0 || !Fits(p, 2.4f) || !TidySpot(p)) continue;
            var plate = new OpticWall
            {
                Kind = OpticKind.Mirror, Centre = p, Angle = rng.Range(0f, MathF.PI), HalfLength = OpticWallTuning.MirrorHalf,
                HalfThick = OpticWallTuning.MirrorThick, Seed = rng.Range(0f, 100f),
            };
            OpticWalls.Add(plate);
            mirrors--;
        }

        bool Fits(Vector2 p, float radius)
        {
            if (!Clear(p, radius) || Map.InShaft(p)) return false;
            if (Vector2.Distance(p, Map.Start.Position) < OpticWallTuning.StartClear) return false;
            foreach (var poi in Map.Pois)
                if (poi.Kind != PoiKind.Start && Vector2.Distance(p, poi.Position) < poi.Radius + 3f) return false;
            foreach (var v in Vases)
                if (Vector2.Distance(v.Position, p) < radius + v.Radius + 0.5f) return false;
            return true;
        }

        // A pane across the passage through p: the narrowest way across it of eight, if it is a passage's width.
        OpticWall? Across(Vector2 p)
        {
            float best = float.MaxValue;
            Vector2 way = Vector2.Zero, mid = p;
            for (int k = 0; k < 8; k++)
            {
                var d = new Vector2(MathF.Cos(k * MathF.PI / 8f), MathF.Sin(k * MathF.PI / 8f));
                float a = LineReach(p, d, OpticWallTuning.GlassWidthMax, out bool ha), b = LineReach(p, -d, OpticWallTuning.GlassWidthMax, out bool hb);
                if (!ha || !hb || a + b >= best) continue;
                best = a + b;
                way = d;
                mid = p + d * ((a - b) * 0.5f);
            }
            if (best < OpticWallTuning.GlassWidthMin || best > OpticWallTuning.GlassWidthMax) return null;
            return new OpticWall
            {
                Kind = OpticKind.Lightglass, Centre = mid, Angle = MathF.Atan2(way.Y, way.X),
                // (Into the rock at either end a little, so nothing slips round it.)
                HalfLength = best * 0.5f + 0.35f, HalfThick = OpticWallTuning.GlassThick,
            };
        }
    }

    /// <summary>
    /// The optic walls' own step: a mirror plate turns as she pushes against its end (stopping where it would cut into
    /// rock); a pane loses the light it took while unlit, and shatters once its band has rung it long enough.
    /// </summary>
    void StepOpticWalls()
    {
        foreach (var w in OpticWalls)
        {
            if (w.Shattered || w.Kind == OpticKind.Halocline) continue;
            if (w.Kind is OpticKind.SunStone or OpticKind.ShadowStone)
            {
                StepStone(w);
                continue;
            }
            if (w.Kind == OpticKind.Mirror)
            {
                var p = Player;
                if (w.Distance(p.Position) < Radius + 0.2f)
                {
                    // (Her push at her lever arm from its pivot turns it: the torque r × v, in the plane.)
                    var r = p.Position - w.Centre;
                    w.Spin += (r.X * p.Velocity.Y - r.Y * p.Velocity.X) * OpticWallTuning.Push * Dt * 6f;
                }
                w.Spin /= 1f + OpticWallTuning.TurnDrag * Dt;
                if (MathF.Abs(w.Spin) < 1e-4f) continue;
                float was = w.Angle;
                w.Angle += w.Spin * Dt;
                var (a, b) = w.Ends;
                if (!Map.IsOpen(a) || !Map.IsOpen(b) || w.Contains(p.Position, Radius * 0.6f))
                {
                    w.Angle = was;
                    w.Spin = 0f;
                }
                continue;
            }
            w.Charge = MathF.Max(0f, w.Charge - OpticWallTuning.Settle * Dt);
            if (w.Charge < OpticWallTuning.Shatter) continue;
            w.Shattered = true;
            Events.Add(new PlaneEvent(PlaneEventType.GlassShattered, w.Centre, w.Axis, w.HalfLength));
            for (int i = 0; i < OpticWallTuning.ShatterShells; i++) Shells.Add(FoundShell(w.Centre, ShellWorth.Pot, Fling(2f, 4f)));
        }
    }

    /// <summary>
    /// A stone's step: the light on its faces (either side, along it); sun-stone hardens while lit and fades a while after,
    /// shadow-stone fades while lit and stands again a while after. Never solid over her (she is not shut in it).
    /// </summary>
    void StepStone(OpticWall w)
    {
        float lit = 0f;
        foreach (float side in new[] { -1f, 1f })
            for (int k = -1; k <= 1; k++)
                lit = MathF.Max(lit, LightAt(w.Centre + w.Axis * (k * w.HalfLength * 0.6f) + w.Normal * side * (w.HalfThick + 0.35f)));
        w.Lit = lit;
        bool sun = w.Kind == OpticKind.SunStone;
        bool lightOn = lit >= OpticWallTuning.StoneSense;
        if (lightOn) w.Hold = sun ? OpticWallTuning.SunHold : OpticWallTuning.ShadowHold;
        else w.Hold = MathF.Max(0f, w.Hold - Dt);
        bool want = sun ? w.Hold > 0f : w.Hold <= 0f;
        float was = w.Solid;
        w.Solid = want ? MathF.Min(1f, w.Solid + OpticWallTuning.StoneRise * Dt) : MathF.Max(0f, w.Solid - OpticWallTuning.StoneFade * Dt);
        if (w.Solid >= 0.5f && was < 0.5f && w.Distance(Player.Position) < Radius) w.Solid = 0.49f;
        if ((was < 0.5f) != (w.Solid < 0.5f))
            Events.Add(new PlaneEvent(w.Solid >= 0.5f ? PlaneEventType.StoneStood : PlaneEventType.StoneFaded, w.Centre, w.Axis, (int)w.Kind));
    }

    /// <summary>A halocline's way for her light: bent crossing it (Snell), or thrown back when it cannot leave the denser water (total internal reflection). Its dense side is along its normal.</summary>
    Vector2 HaloWay(Vector2 dir, OpticWall w, out bool reflected)
    {
        var n = w.Normal;
        bool entering = Vector2.Dot(dir, n) > 0f;
        float dense = Water.Index + OpticWallTuning.HaloIndex;
        var bent = Optics.Refract(dir, n, entering ? Water.Index : dense, entering ? dense : Water.Index);
        reflected = bent is null;
        return bent ?? Optics.Reflect(dir, n);
    }

    /// <summary>
    /// One of her shots meets an optic wall: off a mirror it bounces whole; at a pane it passes on through (its band's share of
    /// the time, the pane ringing with it) or is stopped there.
    /// </summary>
    void ShotMeetsOptic(PlaneShot shot, Vector2 dir, OpticWall w)
    {
        var n = w.Normal;
        // A stone drinks it (and is lit by it a moment: its light is on it).
        if (w.Kind is OpticKind.SunStone or OpticKind.ShadowStone)
        {
            w.Hold = w.Kind == OpticKind.SunStone ? OpticWallTuning.SunHold : OpticWallTuning.ShadowHold;
            Pop(shot, dir);
            return;
        }
        // A halocline bends it on (or throws it back at a grazing angle).
        if (w.Kind == OpticKind.Halocline)
        {
            var way = HaloWay(dir, w, out bool back);
            float side = Vector2.Dot(back ? -dir : dir, n) >= 0f ? 1f : -1f;
            shot.Line = shot.Position = shot.Line + way * 0.05f + n * side * (w.HalfThick * 2f + 0.1f);
            shot.Velocity = way * shot.Velocity.Length();
            Events.Add(new PlaneEvent(back ? PlaneEventType.ShotBounced : PlaneEventType.ShotRefracted, shot.Line, way, w.HalfThick));
            return;
        }
        if (w.Kind == OpticKind.Mirror)
        {
            var off = Optics.Reflect(dir, n);
            shot.Velocity = off * shot.Velocity.Length();
            shot.Damage *= OpticWallTuning.MirrorKeep;
            Events.Add(new PlaneEvent(PlaneEventType.ShotBounced, shot.Line, off, shot.Radius));
            return;
        }
        float pass = PassShare(w, HerLightPhysics.Spectrum);
        w.Charge += pass * OpticWallTuning.ShotCharge;
        if (_bubbleRng.NextFloat() < pass)
        {
            // Through the pane, on along its way.
            float side = Vector2.Dot(dir, n) >= 0f ? 1f : -1f;
            float into = MathF.Max(0f, w.Distance(shot.Line) + w.HalfThick);
            var beyond = shot.Line + dir * ((into + w.HalfThick) / MathF.Max(MathF.Abs(Vector2.Dot(dir, n)), 0.2f) + 0.15f) + n * side * 0.02f;
            if (OpticAt(beyond) is null && Map.IsOpen(beyond))
            {
                shot.Line = shot.Position = beyond;
                shot.Damage *= MathF.Max(0.5f, pass);
                Events.Add(new PlaneEvent(PlaneEventType.ShotRefracted, beyond, dir, w.HalfThick * 2f));
                return;
            }
        }
        Pop(shot, dir);
    }

    /// <summary>
    /// Her beam (or a ray of hers) landing on an optic wall or the biome's rock (LIGHT-WORLD.md §5, phase 2): the light is
    /// divided as it meets it. Off a mirror plate a ray goes on whole; through a pane, its band's share of it, coloured
    /// the pane's colour; on the biome's rock a mirror's share bounces, a crystal's share goes in, bends and comes out of a
    /// thin wall, and the share it stores glows. True: an optic wall took it (her own path words do not apply there).
    /// </summary>
    bool OpticLanding(LightEmitter beam, Vector2 at, float len, float left, LightHue hue, PathWord taken, int bounced,
        List<(LightEmitter Beam, float Worth, LightHue Hue, PathWord Taken, int Bounces)> parents)
    {
        float rest = beam.Range - len;
        var back = at - beam.Direction * PathTuning.Off;
        if (OpticAt(at + beam.Direction * 0.1f) is { } w)
        {
            if (rest < PathTuning.Shortest || _bornCount >= PathTuning.MostBorn) return true;
            // A stone stops it (its light is on its face: StepStone).
            if (w.Kind is OpticKind.SunStone or OpticKind.ShadowStone) return true;
            // A halocline: on, bent (or thrown back), with what it carried.
            if (w.Kind == OpticKind.Halocline)
            {
                var way = HaloWay(beam.Direction, w, out bool thrown);
                float side = Vector2.Dot(thrown ? -beam.Direction : beam.Direction, w.Normal) >= 0f ? 1f : -1f;
                var from = at + w.Normal * side * (w.HalfThick * 2f + 0.15f);
                if (Stops(from)) return true;
                var h = Bear(thrown ? PathWord.Mirror : PathWord.Refract, at, from, way, rest, beam.HalfAngle, left * OpticWallTuning.HaloKeep, hue, taken, bounced);
                h.Into = at;
                parents.Add((h.Emitter, h.Worth, h.Hue, h.Taken, h.Bounces));
                return true;
            }
            if (w.Kind == OpticKind.Mirror)
            {
                var r = Bear(PathWord.Mirror, at, back, Optics.Reflect(beam.Direction, w.Normal), rest, beam.HalfAngle,
                    left * OpticWallTuning.MirrorKeep, hue, taken, bounced);
                parents.Add((r.Emitter, r.Worth, r.Hue, r.Taken, r.Bounces));
                return true;
            }
            float pass = PassShare(w, HerLightPhysics.Spectrum);
            w.Charge += pass * Math.Clamp(left / HerLightTuning.BeamShots, 0f, 1f) * OpticWallTuning.BeamCharge * Dt;
            float cos = MathF.Max(MathF.Abs(Vector2.Dot(beam.Direction, w.Normal)), 0.2f);
            var exit = at + beam.Direction * (w.HalfThick * 2f / cos + PathTuning.Off + 0.1f);
            if (pass * left > 0.05f && !Stops(exit))
            {
                var g = Bear(PathWord.Refract, at, exit, beam.Direction, rest, beam.HalfAngle, left * pass, w.Band, taken, bounced);
                g.Into = at;
                parents.Add((g.Emitter, g.Worth, g.Hue, g.Taken, g.Bounces));
            }
            return true;
        }
        // The biome's own rock (not a seal's, nor a pot).
        if (_rocks.Exists(r => r.Contains(at + beam.Direction * 0.1f)) || VaseAt(at + beam.Direction * 0.1f, 0.05f) is not null) return false;
        var rock = Rock;
        var gr = Map.Gradient(at + beam.Direction * 0.2f);
        var face = gr.LengthSquared() > 1e-6f ? -Vector2.Normalize(gr) : -beam.Direction;
        var meet = Optics.Meet(HerLightPhysics.Spectrum, rock, Vector2.Dot(beam.Direction, face), Water.Index);
        float share = left / MathF.Max(HerLightTuning.BeamShots, 1e-3f);
        if (rock.Stores > 0f) StoreBeamLight(at - beam.Direction * 0.3f, rock.Stores * Math.Clamp(share, 0f, 1f) * (meet.Drunk + meet.Scattered));
        if (rest < PathTuning.Shortest) return false;
        if ((taken & PathWord.Mirror) == 0 && meet.Mirrored >= OpticWallTuning.LeastShare && _bornCount < PathTuning.MostBorn)
        {
            var off = Optics.Reflect(beam.Direction, face);
            if (Vector2.Dot(off, face) > 0f)
            {
                var r = Bear(PathWord.Mirror, at, back, off, rest * MathF.Sqrt(meet.Mirrored), beam.HalfAngle, left * meet.Mirrored, hue, taken | PathWord.Mirror, bounced);
                parents.Add((r.Emitter, r.Worth, r.Hue, r.Taken, r.Bounces));
            }
        }
        if ((taken & PathWord.Refract) == 0 && rock.Through > 0f && meet.Entered >= OpticWallTuning.LeastShare && _bornCount < PathTuning.MostBorn
            && Optics.Refract(beam.Direction, face, Water.Index, rock.Index) is { } inside)
        {
            for (float d = 0.1f; d <= rock.Through; d += 0.1f)
            {
                var q = at + inside * d;
                if (Stops(q)) continue;
                var g2 = Map.Gradient(q - inside * 0.1f);
                var exitFace = g2.LengthSquared() > 1e-6f ? -Vector2.Normalize(g2) : inside;
                if (Optics.Refract(inside, exitFace, rock.Index, Water.Index) is { } outside && rest - d >= PathTuning.Shortest)
                {
                    var g = Bear(PathWord.Refract, at, q + outside * PathTuning.Off, outside, rest - d, beam.HalfAngle,
                        left * meet.Entered * MathF.Exp(-OpticsTuning.InsideDrink * d), hue, taken | PathWord.Refract, bounced);
                    g.Into = at;
                    parents.Add((g.Emitter, g.Worth, g.Hue, g.Taken, g.Bounces));
                }
                break;
            }
        }
        return false;
    }

    /// <summary>Her beam on a wall that stores light: the nearest glow there kept full, or a new one (at most MostGlows).</summary>
    void StoreBeamLight(Vector2 at, float strength)
    {
        if (strength <= 0.05f) return;
        foreach (var g in WallGlows)
            if (Vector2.DistanceSquared(g.At, at) < 1.4f * 1.4f)
            {
                float most = OpticsTuning.GlowSeconds * MathF.Min(strength, 1.3f);
                if (most > g.Most) g.Most = most;
                g.Life = MathF.Min(g.Most, g.Life + 2f * Dt * g.Most);
                return;
            }
        if (WallGlows.Count < OpticWallTuning.MostGlows) AddWallGlow(at, MathF.Min(strength, 1.3f) * 0.5f);
    }
}
