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
    /// <summary>How long a sealed leak's push takes to die.</summary>
    public const float LeakFall = 1.5f;
    /// <summary>The flow fades to nothing this far past a current's sides, and over this last stretch of its course (m).</summary>
    public const float Feather = 2f, EndFade = 5f;
    /// <summary>The clearances a course is sought with, widest first: down the middle of the water wherever it can be.</summary>
    public static readonly float[] Clearances = { 3f, 2f, 1f, 0.5f };
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
        // It holds what rides it: a pull back to its middle, so it stays in as the course bends.
        var hold = dist > 1e-4f ? (nearest - p) / dist * (MathUtil.Clamp01(dist / half) * CurrentTuning.MainHold) : Vector2.Zero;
        return (dir + hold) * (Speed * k);
    }
}

/// <summary>The main current's course, worked out from the map alone (the springs keep off it, the world lays it).</summary>
public static class MainCurrent
{
    /// <summary>
    /// From her start to the way down; a boss level's, to its arena's edge (it does not blow through the fight); a leak
    /// cave's, from its leak back to its mouth.
    /// </summary>
    public static List<Vector2> Course(LevelMap map)
    {
        if (map.MainCourse is { } known) return known;
        // A leak cave's runs the other way: from its sealed leak back out to the mouth.
        var course = map.LeakCave ? Route(map, map.Start.Position, map.Exit.Position, new Dictionary<(Vector2, float), float[]>())
            : Route(map, map.Exit.Position, map.Start.Position, new Dictionary<(Vector2, float), float[]>());
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
    /// <summary>The level's standing current, the main one (adventure levels only).</summary>
    public List<PlaneCurrent> Currents { get; } = new();

    /// <summary>The water's flow at p (m/s): the leaks' push, harder the nearer, and the main current's too if asked.</summary>
    public Vector2 CurrentAt(Vector2 p, bool main = true)
    {
        Vector2 flow = Vector2.Zero;
        if (main)
            foreach (var c in Currents) flow += c.At(p);
        float push = AdventureTuning.LeakPush + AdventureTuning.LeakPushPerMenace * Menace;
        foreach (var v in Leaks)
        {
            var off = p - v.Position;
            float d = off.Length();
            if (v.Push <= 0f || d >= AdventureTuning.LeakReach) continue;
            float k = 1f - d / AdventureTuning.LeakReach;
            flow += (d < 1e-3f ? Vector2.UnitY : off / d) * (push * v.Push * k * k);
        }
        return flow;
    }

    /// <summary>Lays the main current (MainCurrent.Course), asleep until enough leaks are sealed.</summary>
    void PlaceCurrents()
    {
        // A region's water has no main current of its own: it would carry her to its station and home.
        if (Map.Station) return;
        var course = MainCurrent.Course(Map);
        if (course.Count < 2) return;
        var main = new PlaneCurrent { Speed = CurrentTuning.MainSpeed, HalfStart = CurrentTuning.MainHalfWidth, HalfEnd = CurrentTuning.MainHalfWidth, Strength = LeaksLeft == 0 ? 1f : 0f };
        main.Lay(course);
        Currents.Add(main);
    }

    /// <summary>
    /// The currents' turn: the main one wakes once enough leaks are sealed, and a leak's push dies with it. What they
    /// carry: Clementine, sliding along rock, every shot and ink cloud. The creatures live in this water and hold their
    /// ground in the main current (a leak's push turns them all the same), and shells, hearts and pearls stay where they lie (a current that never stops would sweep them all
    /// to the way down).
    /// </summary>
    void StepCurrents()
    {
        if (Currents.Count == 0 && Leaks.Count == 0) return;
        foreach (var c in Currents) c.Strength = MathUtil.MoveToward(c.Strength, LeaksLeft == 0 ? 1f : 0f, Dt / CurrentTuning.MainRise);
        var p = Player;
        Vector2 flow = CurrentAt(p.Position);
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

    /// <summary>No creature gets under a leak: the push turns them, and none comes nearer than LeakClear.</summary>
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
            if (v.Sealed) continue;
            var off = at - v.Position;
            float d = off.Length();
            if (d >= AdventureTuning.LeakReach) continue;
            var away = d > 1e-3f ? off / d : Vector2.UnitY;
            float k = 1f - d / AdventureTuning.LeakReach;
            var to = at + away * ((AdventureTuning.LeakPush + AdventureTuning.LeakPushPerMenace * Menace) * k * k * Dt);
            if (d < AdventureTuning.LeakClear) to = v.Position + away * AdventureTuning.LeakClear;
            if (Map.IsOpen(to)) at = to;
        }
    }
}
