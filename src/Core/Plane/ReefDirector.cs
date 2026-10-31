using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>The reef director's numbers (DESIGN-TOPDOWN §4.7); first guesses, tuned in play.</summary>
public static class ReefDirectorTuning
{
    /// <summary>The schedule is drawn this far ahead when the level is built.</summary>
    public const float Horizon = 20f * 60f;
}

public enum ReefEventKind { GeyserSpray }

/// <summary>Something the reef does on its own, at a set time in the level.</summary>
public sealed class ReefEvent
{
    public ReefEventKind Kind;
    /// <summary>Level time (s) it starts, and how long it lasts.</summary>
    public float Start, Duration;
    /// <summary>A geyser's spray: which vent (an index into the director's geysers).</summary>
    public int Geyser;

    public float End => Start + Duration;
}

/// <summary>
/// The reef director (DESIGN-TOPDOWN §4.7): the level acts on its own. At level build it draws a schedule of world
/// events from the run's "events" stream into a priority queue of (time, event); the sim pops them as level time
/// reaches them, whether Clementine is near or not. Deterministic: the same level always schedules the same events.
/// </summary>
public sealed class ReefDirector
{
    readonly PriorityQueue<ReefEvent, float> _queue = new();
    readonly List<ReefEvent> _active = new();

    public ReefDirector(LevelMap map)
    {
        Map = map;
        var rng = new RunStreams(SeedCode.FromValue(map.Seed)).Events(map.Id);
        ScheduleGeysers(rng.Stream("geysers"));
    }

    public LevelMap Map { get; }

    /// <summary>The level's bubble geysers' vents.</summary>
    public IReadOnlyList<Vector2> Geysers => _geysers;
    readonly List<Vector2> _geysers = new();
    readonly List<ReefEvent> _sprays = new();

    /// <summary>Every geyser's sprays for the level, in order of start.</summary>
    public IReadOnlyList<ReefEvent> GeyserSchedule => _sprays;

    /// <summary>Each vent sprays on its own clock: a first spray early on, then one every while after its last.</summary>
    void ScheduleGeysers(Rng rng)
    {
        _geysers.AddRange(Plane.Geysers.Of(Map));
        for (int g = 0; g < _geysers.Count; g++)
        {
            float t = rng.Range(GeyserTuning.FirstMin, GeyserTuning.FirstMax);
            while (t < ReefDirectorTuning.Horizon)
            {
                var e = new ReefEvent
                {
                    Kind = ReefEventKind.GeyserSpray,
                    Start = t,
                    Duration = GeyserTuning.Warn + rng.Range(GeyserTuning.SprayMin, GeyserTuning.SprayMax),
                    Geyser = g,
                };
                _queue.Enqueue(e, e.Start);
                _sprays.Add(e);
                t = e.End + rng.Range(GeyserTuning.GapMin, GeyserTuning.GapMax);
            }
        }
        _sprays.Sort((a, b) => a.Start.CompareTo(b.Start));
    }

    /// <summary>The events running now.</summary>
    public IReadOnlyList<ReefEvent> Active => _active;

    /// <summary>Adds an event to the schedule out of turn (verification: `--dbg-geyser`).</summary>
    public void Inject(ReefEvent e) => _queue.Enqueue(e, e.Start);

    /// <summary>Advances to level time <paramref name="time"/>: events whose time has come start, finished ones end.</summary>
    public void Step(float time, List<ReefEvent> started, List<ReefEvent> ended)
    {
        while (_queue.TryPeek(out var next, out float at) && at <= time)
        {
            _queue.Dequeue();
            _active.Add(next);
            started.Add(next);
        }
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].End > time) continue;
            ended.Add(_active[i]);
            _active.RemoveAt(i);
        }
    }

    /// <summary>The water's push at p from the geysers spraying now (m/s).</summary>
    public Vector2 FlowAt(Vector2 p, float time)
    {
        Vector2 flow = Vector2.Zero;
        foreach (var e in _active) flow += Plane.Geysers.PushAt(_geysers[e.Geyser], e, p, time);
        return flow;
    }
}
