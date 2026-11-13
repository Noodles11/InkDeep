using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>The level's two kinds of current (DESIGN-TOPDOWN §4.7); first guesses, tuned in play.</summary>
public static class CurrentTuning
{
    /// <summary>The main current: its flow (m/s; her cruise is 6), its half width, and how long it takes to wake.</summary>
    public const float MainSpeed = 3.5f, MainHalfWidth = 4f, MainRise = 3f;
    /// <summary>What keeps her in it round a bend: the pull back to its middle at its edge, as a share of its flow.</summary>
    public const float MainHold = 0.35f;
    /// <summary>The flow fades to nothing this far past a current's sides, and over this last stretch of its course (m).</summary>
    public const float Feather = 2f, EndFade = 5f;
    /// <summary>The clearances a course is sought with, widest first: down the middle of the water wherever it can be.</summary>
    public static readonly float[] Clearances = { 3f, 2f, 1f, 0.5f };
    /// <summary>
    /// A leak cave's way out, held: water pouring in through it from its back, faster than she swims (her cruise is 6),
    /// running on this far into the cave; once its leak is sealed it dies away over HoldFade s.
    /// </summary>
    public const float HoldSpeed = 11f, HoldOut = 6f, HoldFade = 1.5f;
    /// <summary>A solved labyrinth's way home: its flow (m/s) and half width (m, within its 4 m passages).</summary>
    public const float GuideSpeed = 8f, GuideHalfWidth = 1.8f;
    /// <summary>How far ahead along its middle line a way home pulls (m), and how far off the rock it tries to keep that line (m, the first that finds a way).</summary>
    public const float GuideAhead = 1.2f;
    /// <summary>How long a way home that stopped carrying her pushes her aside instead (s).</summary>
    public const float GuideUnstick = 0.25f;
    public static readonly float[] GuideClearances = { 1.4f, 1.1f, 0.8f };
}

/// <summary>The main current: water running along a course, full strength in its middle and fading past its sides.</summary>
public sealed class PlaneCurrent
{
    public List<Vector2> Course = new();
    /// <summary>Its course's length (m), and the box its water lies in.</summary>
    public float Length;
    public Vector2 Min, Max;
    /// <summary>Its flow at the start of its course (m/s), and its half width at either end (m).</summary>
    public float Speed, HalfStart, HalfEnd;
    /// <summary>Its strength now, 0..1.</summary>
    public float Strength;
    /// <summary>A leak cave's way out held shut: it runs while the cave's leak is open, not once it is sealed.</summary>
    public bool Holds;
    /// <summary>A labyrinth's way home: it runs from its heart to its way out once its reward is taken (LabyrinthSolved).</summary>
    public bool Guides;
    /// <summary>
    /// A way home's line down the middle of its passages (well clear of their rock), and how far along it what rides it
    /// has come (it only goes on: never jumps across a wall to a later stretch of the way).
    /// </summary>
    public List<Vector2>? Middle;
    public int Progress;
    /// <summary>Its half width this far along its course.</summary>
    public float HalfAt(float along) => HalfStart + (HalfEnd - HalfStart) * MathUtil.Clamp01(along / MathF.Max(Length, 1f));

    /// <summary>Lays it along a course.</summary>
    public void Lay(List<Vector2> course)
    {
        Course = course;
        Length = 0f;
        for (int i = 1; i < course.Count; i++) Length += Vector2.Distance(course[i - 1], course[i]);
        float reach = MathF.Max(HalfStart, HalfEnd) + CurrentTuning.Feather;
        Min = course.Aggregate(new Vector2(float.MaxValue), Vector2.Min) - new Vector2(reach);
        Max = course.Aggregate(new Vector2(float.MinValue), Vector2.Max) + new Vector2(reach);
    }

    /// <summary>The water's flow at p (m/s).</summary>
    public Vector2 At(Vector2 p)
    {
        if (Strength <= 0f || Course.Count < 2 || p.X < Min.X || p.Y < Min.Y || p.X > Max.X || p.Y > Max.Y) return Vector2.Zero;
        float dist = float.MaxValue, along = 0f, s = 0f;
        Vector2 dir = Vector2.UnitX, nearest = p;
        for (int i = 0; i + 1 < Course.Count; i++)
        {
            Vector2 a = Course[i], b = Course[i + 1], ab = b - a;
            float len = ab.Length();
            if (len < 1e-5f) continue;
            float t = MathUtil.Clamp01(Vector2.Dot(p - a, ab) / (len * len));
            var q = a + ab * t;
            float d = Vector2.Distance(p, q);
            if (d < dist)
            {
                dist = d;
                along = s + len * t;
                dir = ab / len;
                nearest = q;
            }
            s += len;
        }
        float half = HalfAt(along);
        float k = (1f - MathUtil.Clamp01((dist - half) / CurrentTuning.Feather)) * MathUtil.Clamp01((Length - along) / CurrentTuning.EndFade) * Strength;
        if (k <= 0f) return Vector2.Zero;
        // A way home through a maze's turns: it flows toward a point a little further along its course, so what rides it
        // is steered round each bend rather than pressed into the rock outside it.
        if (Guides && Middle is { Count: >= 2 } middle) return Pursue(middle, p) * (Speed * k);
        // It holds what rides it: a pull back to its middle, so it stays in as the course bends.
        var hold = dist > 1e-4f ? (nearest - p) / dist * (MathUtil.Clamp01(dist / half) * CurrentTuning.MainHold) : Vector2.Zero;
        return (dir + hold) * (Speed * k);
    }

    /// <summary>
    /// A way home's flow at p: toward the point GuideAhead further along its middle line than the nearest point of it to
    /// p (looked for only a little behind and ahead of how far she has come), so it carries her down the middle of each
    /// passage and round each turn, never into the rock.
    /// </summary>
    Vector2 Pursue(List<Vector2> line, Vector2 p)
    {
        int from = Math.Max(0, Progress - 2), to = Math.Min(line.Count - 2, Progress + 6);
        float best = float.MaxValue;
        int seg = Progress;
        float segT = 0f;
        for (int i = from; i <= to; i++)
        {
            var a = line[i];
            var ab = line[i + 1] - a;
            float len2 = ab.LengthSquared();
            float t = len2 > 1e-8f ? MathUtil.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            float d = Vector2.DistanceSquared(p, a + ab * t);
            if (d < best)
            {
                best = d;
                seg = i;
                segT = t;
            }
        }
        Progress = Math.Max(Progress, seg);
        // From the nearest point, GuideAhead on along the line.
        float left = CurrentTuning.GuideAhead;
        var at = Vector2.Lerp(line[seg], line[seg + 1], segT);
        int i2 = seg;
        while (left > 0f && i2 + 1 < line.Count)
        {
            float rest = Vector2.Distance(at, line[i2 + 1]);
            if (rest >= left)
            {
                at += (line[i2 + 1] - at) / MathF.Max(rest, 1e-5f) * left;
                left = 0f;
                break;
            }
            left -= rest;
            at = line[++i2];
        }
        var way = at - p;
        return way.LengthSquared() > 1e-6f ? Vector2.Normalize(way) : Vector2.Zero;
    }
}

/// <summary>The main current's course, worked out from the map alone (the springs keep off it, the world lays it).</summary>
public static class MainCurrent
{
    /// <summary>
    /// From her start to the way down; a boss level's, to its arena's edge (it does not blow through the fight).
    /// </summary>
    public static List<Vector2> Course(LevelMap map)
    {
        if (map.MainCourse is { } known) return known;
        var course = Route(map, map.Exit.Position, map.Start.Position, new Dictionary<(Vector2, float), float[]>());
        if (map.HasBoss)
        {
            int inside = course.FindIndex(q => Vector2.Distance(q, map.Exit.Position) < map.Exit.Radius);
            if (inside >= 2) course.RemoveRange(inside, course.Count - inside);
        }
        return map.MainCourse = course;
    }

    /// <summary>Whether a circle at p touches the main current, felt anywhere.</summary>
    public static bool Touches(LevelMap map, Vector2 p, float radius)
    {
        var course = Course(map);
        float clear = CurrentTuning.MainHalfWidth + CurrentTuning.Feather + radius;
        for (int i = 0; i + 1 < course.Count; i++)
            if (Geo.SegmentDistance(p, course[i], course[i + 1]) < clear) return true;
        return false;
    }

    /// <summary>
    /// A smooth course from a point to a target, as far down the middle of the water as it will go (empty if she cannot get
    /// there). It may run through the boss's walls (she brings those down), never through a secret room's seal.
    /// </summary>
    public static List<Vector2> Route(LevelMap map, Vector2 to, Vector2 from, Dictionary<(Vector2, float), float[]> fields)
    {
        var shut = map.WeakRocks.Where(r => r.Kind != SealKind.Boss).ToList();
        foreach (float clearance in CurrentTuning.Clearances)
        {
            if (!fields.TryGetValue((to, clearance), out var field))
                fields[(to, clearance)] = field = LevelValidator.Distances(map, to, clearance, q => shut.Exists(r => r.Contains(q)));
            var cells = LevelValidator.Walk(field, from);
            if (cells.Count >= 2) return Smooth(cells);
        }
        return new List<Vector2>();
    }

    /// <summary>A route of cell centres eased into a curve: every other cell, its corners rounded off.</summary>
    static List<Vector2> Smooth(List<Vector2> cells)
    {
        var points = new List<Vector2>();
        for (int i = 0; i < cells.Count; i += 2) points.Add(cells[i]);
        if (points[^1] != cells[^1]) points.Add(cells[^1]);
        for (int pass = 0; pass < 5; pass++)
        {
            var before = points.ToArray();
            for (int i = 1; i + 1 < before.Length; i++) points[i] = (before[i - 1] + before[i] * 2f + before[i + 1]) * 0.25f;
        }
        return points;
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's standing currents: the main one (adventure levels), or a leak cave's held way out.</summary>
    public List<PlaneCurrent> Currents { get; } = new();

    /// <summary>The water's flow at p (m/s): the currents', if asked (a leak does not push).</summary>
    public Vector2 CurrentAt(Vector2 p, bool main = true)
    {
        Vector2 flow = Vector2.Zero;
        if (main)
            foreach (var c in Currents) flow += c.At(p);
        return flow;
    }

    /// <summary>
    /// A labyrinth's way home, asleep till its reward is taken; in a leak cave, the current that holds its way out while
    /// its leak is open. (A reef's main current, waking as its leaks were sealed, is no more: docs/DIVE.md.)
    /// </summary>
    void PlaceCurrents()
    {
        if (Map.Labyrinth)
        {
            PlaceGuide();
            return;
        }
        // A leak cave has no main current: she swims back out herself, whenever she likes (docs/DIVE.md §6). (A cave that
        // keeps her in till its leak is sealed has water pouring in through its way out, from its back out into the cave.)
        if (Map.Way is { } way && !Options.DiveCave)
        {
            var hold = new PlaneCurrent { Speed = CurrentTuning.HoldSpeed, HalfStart = Doorway.HalfWidth + 0.3f, HalfEnd = Doorway.HalfWidth + 1f, Strength = 1f, Holds = true };
            hold.Lay(new List<Vector2> { way.Back + way.Into * 0.5f, way.At, way.At - way.Into * CurrentTuning.HoldOut });
            Currents.Add(hold);
        }
    }

    /// <summary>Its reward taken (every shell and pearl at its heart gathered): a labyrinth is solved, and its way home runs.</summary>
    public bool LabyrinthSolved => Map.Labyrinth && Shells.Count == 0 && Pearls.Count == 0;

    /// <summary>
    /// A labyrinth's way home (docs/DIVE.md §6.1): a current from its heart along its passages to its way out and through
    /// it, asleep till it is solved; then it carries her out (and its mouth takes her back to the reef).
    /// </summary>
    /// <summary>A way home's middle line: the water's shortest way from the heart to the way out kept as far off the rock as the maze allows, then on through the way out.</summary>
    List<Vector2>? MiddleOf(Vector2 heart, Doorway way)
    {
        foreach (float clearance in CurrentTuning.GuideClearances)
        {
            var path = LevelValidator.ShortestPath(Map, heart, way.At, clearance);
            if (path.Count < 2) continue;
            path.Add(way.Back);
            return path;
        }
        return null;
    }

    void PlaceGuide()
    {
        if (Map.Way is not { } way) return;
        var heart = Map.Pois.FirstOrDefault(p => p.Kind == PoiKind.Secret)?.Position ?? Map.Exit.Position;
        // (Down the middle of its passages, smooth: as the main current was laid.)
        var path = MainCurrent.Route(Map, way.At, heart, new Dictionary<(Vector2, float), float[]>());
        if (path.Count < 2) return;
        path.Add(way.Back);
        var guide = new PlaneCurrent
        {
            Speed = CurrentTuning.GuideSpeed, HalfStart = CurrentTuning.GuideHalfWidth, HalfEnd = CurrentTuning.GuideHalfWidth, Guides = true,
            Middle = MiddleOf(heart, way),
        };
        guide.Lay(path);
        Currents.Add(guide);
    }

    /// <summary>
    /// The currents' turn: the main one wakes once enough leaks are sealed. What it carries: Clementine, sliding along
    /// rock, every shot and ink cloud. The creatures live in this water and hold their ground in it, and shells, hearts
    /// and pearls stay where they lie (a current that never stops would sweep them all to the way down). No creature
    /// comes onto an open leak's disc.
    /// </summary>
    void StepCurrents()
    {
        if (Currents.Count == 0 && Leaks.Count == 0) return;
        foreach (var c in Currents)
            c.Strength = c.Holds ? MathUtil.MoveToward(c.Strength, CaveLeakOpen ? 1f : 0f, Dt / CurrentTuning.HoldFade)
                : c.Guides ? MathUtil.MoveToward(c.Strength, LabyrinthSolved ? 1f : 0f, Dt / CurrentTuning.MainRise)
                : c.Strength;
        var p = Player;
        Vector2 flow = CurrentAt(p.Position);
        // A way home that has stopped carrying her (pressed against a nub of rock in its passage) turns its push aside
        // for a moment, the way there is room, so she slides round it.
        if (_guideStuck > 0f && flow != Vector2.Zero)
        {
            var side = new Vector2(-flow.Y, flow.X);
            if (!Clear(p.Position + Vector2.Normalize(side) * 0.3f, Radius)) side = -side;
            flow = side;
            _guideStuck -= Dt;
        }
        // (Stuck: no further on than a tick ago, for all the current's push.)
        if (LabyrinthSolved && _guideStuck <= 0f && flow.LengthSquared() > 1f && Vector2.DistanceSquared(_guideLast, p.Position) < 1e-4f) _guideStuck = CurrentTuning.GuideUnstick;
        _guideLast = p.Position;
        if (flow != Vector2.Zero) Move(ref p.Position, ref flow, Radius, report: false);
        foreach (var shot in Shots)
        {
            Vector2 d = CurrentAt(shot.Position) * Dt;
            if (d == Vector2.Zero || !Map.IsOpen(shot.Position + d)) continue;
            shot.Position += d;
            shot.Line += d;
        }
        foreach (var cloud in Clouds)
        {
            Vector2 d = CurrentAt(cloud.Position) * Dt;
            if (d != Vector2.Zero && Map.IsOpen(cloud.Position + d)) cloud.Position += d;
        }
        if (Leaks.Exists(v => !v.Sealed)) KeepFromLeaks();
    }

    /// <summary>How much longer a way home pushes her aside, round what it pressed her against (s).</summary>
    float _guideStuck;
    Vector2 _guideLast;

    /// <summary>No creature gets onto a leak: none comes nearer its disc than LeakClear.</summary>
    void KeepFromLeaks()
    {
        foreach (var m in Murklings)
            if (m.Alive && !m.Clinging) Shove(ref m.Position);
        foreach (var w in Inkwardens)
            if (w.Alive) Shove(ref w.Position);
        foreach (var mob in Mobs)
            if (mob.Alive) Shove(ref mob.Position);
        foreach (var f in Fish) Shove(ref f.Position);
        foreach (var f in ReefFish) Shove(ref f.Position);
        foreach (var school in Schools) Shove(ref school.Position);
    }

    void Shove(ref Vector2 at)
    {
        foreach (var v in Leaks)
        {
            if (v.Sealed || v.Hidden) continue;
            var off = at - v.Position;
            float d = off.Length();
            if (d >= AdventureTuning.LeakClear) continue;
            var away = d > 1e-3f ? off / d : Vector2.UnitY;
            var to = v.Position + away * AdventureTuning.LeakClear;
            if (Map.IsOpen(to)) at = to;
        }
    }
}
