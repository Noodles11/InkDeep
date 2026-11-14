using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// How much of a cube is in hand (CUBE-WORLD-PLAN §5.1), least first. Mask: its terrain only, not drawn (the layer
/// above, read to know where she may climb). Below: terrain and a simplified mesh (the layer under hers, seen through
/// the void). Preview: terrain and its full ground (the cubes round hers she sees but cannot reach). Preloaded: with its
/// stocking too, its creatures asleep. Active: the cube she is in.
/// </summary>
public enum CubeState : byte { Unloaded, Mask, Below, Preview, Preloaded, Active }

/// <summary>A piece of work on a cube: its terrain first, then what is built from it.</summary>
public enum CubeWork : byte { Terrain, PreviewMesh, Mesh, Stock }

/// <summary>A piece of work handed to the worker. Priority: lower first.</summary>
public readonly record struct CubeJob(long Id, CubeId Cube, CubeWork Work, int Priority);

/// <summary>What the stream tells the view: a cube's work done and kept (Added) or let go (Dropped).</summary>
public readonly record struct StreamChange(CubeId Cube, CubeWork Work, bool Added, object? Result);

/// <summary>Runs the stream's jobs off the main thread; each result comes back through <see cref="ReefStream.Complete"/> on it.</summary>
public interface ICubeWorker
{
    /// <summary>Begins a job. <paramref name="terrain"/> is the cube's terrain (null for the terrain job itself).</summary>
    void Start(CubeJob job, CubeTerrain? terrain);
}

/// <summary>Where she is and what she is about, each frame (what the stream wants follows from it).</summary>
public struct StreamFocus
{
    public Vector2 Position, Velocity;
    public int Layer;
    /// <summary>She may climb from this layer (it is the deepest she has reached): the layer above is kept as masks.</summary>
    public bool Climbable;
    /// <summary>A dive (or a climb) may come soon or is under way: the cube under (or over) her is readied for her to arrive in.</summary>
    public bool ReadyBelow, ReadyAbove;
    /// <summary>Edges the wake current holds shut (CUBE-WORLD-PLAN §8.2); null: none.</summary>
    public Func<EdgeId, bool>? Shut;
}

/// <summary>
/// The world-streaming manager (CUBE-WORLD-PLAN §5.2), core side, on the main thread: from where she is, which cubes
/// are wanted in which state; the work missing for them, handed to the worker most needed first (her cube, the cubes she
/// can reach, those she can see, the layer below, the layer above, the cube ahead) and a few at a time; finished work
/// kept or, if no longer wanted, thrown away; and what is no longer wanted let go only after it has been unwanted for
/// UnloadDelay and while nothing pins it. The view follows <see cref="Changes"/>.
/// </summary>
public sealed class ReefStream
{
    /// <summary>Seconds a cube's work is kept after it stops being wanted (so crossing a line back and forth costs nothing).</summary>
    public const float UnloadDelay = 3f;
    /// <summary>Jobs out with the worker at once.</summary>
    public const int MaxInFlight = 2;
    /// <summary>How near a gate she must be, heading for it, for the cube beyond its far side to be made early.</summary>
    public const float PredictReach = 20f;

    readonly CubeSpace _space;
    readonly ICubeWorker _worker;
    readonly Dictionary<CubeId, Entry> _cubes = new();
    readonly Dictionary<CubeId, CubeState> _wanted = new();
    readonly Dictionary<CubeId, int> _rank = new();
    readonly Dictionary<long, CubeJob> _inFlight = new();
    readonly ConcurrentDictionary<long, byte> _cancelled = new();
    readonly List<StreamChange> _changes = new();
    readonly List<(CubeJob Job, float Distance)> _todo = new();
    long _nextId;

    public ReefStream(CubeSpace space, ICubeWorker worker)
    {
        _space = space;
        _worker = worker;
    }

    public CubeSpace Space => _space;

    sealed class Entry
    {
        public CubeTerrain? Terrain;
        public readonly Dictionary<CubeWork, object?> Done = new();
        /// <summary>Per piece of work in hand, how long it has gone unneeded.</summary>
        public readonly Dictionary<CubeWork, float> Idle = new();
        public int Pins;
        public readonly List<string> Reasons = new();
    }

    /// <summary>What changed since the view last took them (it clears them).</summary>
    public List<StreamChange> Changes => _changes;

    /// <summary>The jobs out with the worker now.</summary>
    public IReadOnlyCollection<CubeJob> InFlight => _inFlight.Values;

    /// <summary>Whether a job has been given up (the worker may skip it; its result will be thrown away). Any thread.</summary>
    public bool Cancelled(long id) => _cancelled.ContainsKey(id);

    /// <summary>What each cube is wanted as, after the last update.</summary>
    public IReadOnlyDictionary<CubeId, CubeState> Wanted => _wanted;

    /// <summary>The cubes with any work in hand.</summary>
    public IEnumerable<CubeId> Held => _cubes.Keys;

    public CubeTerrain? Terrain(CubeId c) => _cubes.TryGetValue(c, out var e) ? e.Terrain : null;

    public object? Result(CubeId c, CubeWork work) => _cubes.TryGetValue(c, out var e) && e.Done.TryGetValue(work, out var r) ? r : null;

    public bool Has(CubeId c, CubeWork work) => _cubes.TryGetValue(c, out var e) && e.Done.ContainsKey(work);

    /// <summary>
    /// The state a cube is in: the most its work in hand allows, Active only for the cube wanted so (one wanted Active
    /// and not yet ready is Unloaded, or Preview, Mask: what it has).
    /// </summary>
    public CubeState State(CubeId c)
    {
        if (!_cubes.TryGetValue(c, out var e)) return CubeState.Unloaded;
        if (_wanted.TryGetValue(c, out var w) && Ready(e, w)) return w;
        var ready = CubeState.Unloaded;
        foreach (var s in Lesser)
            if (Ready(e, s)) ready = s;
        return ready;
    }

    static readonly CubeState[] Lesser = { CubeState.Mask, CubeState.Below, CubeState.Preview, CubeState.Preloaded };

    /// <summary>The work a state needs.</summary>
    public static IEnumerable<CubeWork> Needs(CubeState s)
    {
        if (s >= CubeState.Mask) yield return CubeWork.Terrain;
        if (s == CubeState.Below) yield return CubeWork.PreviewMesh;
        if (s >= CubeState.Preview) yield return CubeWork.Mesh;
        if (s >= CubeState.Preloaded) yield return CubeWork.Stock;
    }

    static bool Ready(Entry e, CubeState s)
    {
        foreach (var w in Needs(s))
            if (!e.Done.ContainsKey(w)) return false;
        return true;
    }

    /// <summary>
    /// A cube kept as it is, whatever is wanted, until the pin is let go (she, a creature awake in it, a shot crossing
    /// it, a battle, an animation). Pinning a cube with nothing in hand keeps nothing.
    /// </summary>
    public IDisposable Pin(CubeId c, string reason)
    {
        var e = Get(c);
        e.Pins++;
        e.Reasons.Add(reason);
        return new Unpin(this, c, reason);
    }

    /// <summary>What pins a cube (for the overlay).</summary>
    public IReadOnlyList<string> PinnedBy(CubeId c) => _cubes.TryGetValue(c, out var e) ? e.Reasons : Array.Empty<string>();

    sealed class Unpin : IDisposable
    {
        readonly ReefStream _stream;
        readonly CubeId _cube;
        readonly string _reason;
        bool _done;

        public Unpin(ReefStream stream, CubeId cube, string reason)
        {
            _stream = stream;
            _cube = cube;
            _reason = reason;
        }

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            if (!_stream._cubes.TryGetValue(_cube, out var e)) return;
            e.Pins--;
            e.Reasons.Remove(_reason);
        }
    }

    Entry Get(CubeId c)
    {
        if (!_cubes.TryGetValue(c, out var e)) _cubes[c] = e = new Entry();
        return e;
    }

    /// <summary>Each frame: what is wanted, what is let go, and the next jobs out.</summary>
    public void Update(in StreamFocus focus, float dt)
    {
        Want(focus);
        LetGo(dt);
        Dispatch(focus.Position);
    }

    /// <summary>A finished job's result (main thread). Kept if its cube still wants it, else thrown away.</summary>
    public void Complete(CubeJob job, object? result)
    {
        if (!_inFlight.Remove(job.Id)) return;
        bool cancelled = _cancelled.TryRemove(job.Id, out _);
        var want = _wanted.TryGetValue(job.Cube, out var w) ? w : CubeState.Unloaded;
        bool needed = Contains(Needs(want), job.Work);
        if (cancelled && !needed) return;
        if (!needed && !_cubes.ContainsKey(job.Cube)) return;
        if (job.Work == CubeWork.Terrain && result is not CubeTerrain) return;
        var e = Get(job.Cube);
        if (job.Work == CubeWork.Terrain) e.Terrain = (CubeTerrain)result!;
        else if (e.Terrain is null) return;
        e.Done[job.Work] = result;
        _changes.Add(new StreamChange(job.Cube, job.Work, true, result));
    }

    /// <summary>A job that failed: given up, to be tried again if still wanted.</summary>
    public void Fail(CubeJob job)
    {
        _inFlight.Remove(job.Id);
        _cancelled.TryRemove(job.Id, out _);
    }

    void Want(in StreamFocus f)
    {
        _wanted.Clear();
        _rank.Clear();
        var cell = CellCoord.Of(f.Position, f.Layer);
        var here = CubeId.Of(cell);
        // Rank: the order work is done in (lower first): her cube, those she can reach, those she sees, below, above, ahead.
        Mark(here, CubeState.Active, 0);
        foreach (var (edge, inside, across) in here.Outer())
        {
            var spec = _space.Edge(edge);
            var next = CubeId.Of(across);
            bool reach = spec.Open && !(f.Shut?.Invoke(edge) ?? false);
            Mark(next, reach ? CubeState.Preloaded : CubeState.Preview, reach ? 1 : 2);
            if (!reach) continue;
            // The cube past the square beyond, made early when she heads for a gate into it.
            foreach (var gate in spec.Gates)
            {
                var at = edge.Point(gate.Along, 0f);
                if (Vector2.Distance(at, f.Position) > PredictReach || f.Velocity.LengthSquared() < 0.25f) continue;
                if (Vector2.Dot(f.Velocity, at - f.Position) <= 0f) continue;
                var beyond = CubeId.Of(new CellCoord(2 * across.X - inside.X, 2 * across.Y - inside.Y, here.Z));
                if (beyond == next) beyond = CubeId.Of(new CellCoord(3 * across.X - 2 * inside.X, 3 * across.Y - 2 * inside.Y, here.Z));
                Mark(beyond, CubeState.Preview, 6);
            }
        }
        // The squares round hers (as far as the view reaches), their cubes; under them, and over them.
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            var near = new CellCoord(cell.X + dx, cell.Y + dy, cell.Z);
            Mark(CubeId.Of(near), CubeState.Preview, 2);
            // (A dive or a climb coming: the layer she will arrive on readied in full, the cube she lands in first.)
            Mark(CubeId.Of(near with { Z = cell.Z + 1 }), f.ReadyBelow ? CubeState.Preview : CubeState.Below, f.ReadyBelow ? 1 : 3);
            if (f.Climbable) Mark(CubeId.Of(near with { Z = cell.Z - 1 }), f.ReadyAbove ? CubeState.Preview : CubeState.Mask, f.ReadyAbove ? 1 : 4);
        }
        if (f.ReadyBelow) Mark(CubeId.Of(cell with { Z = cell.Z + 1 }), CubeState.Preloaded, 0);
        if (f.ReadyAbove && f.Climbable) Mark(CubeId.Of(cell with { Z = cell.Z - 1 }), CubeState.Preloaded, 0);
    }

    /// <summary>A cube wanted at least so (and its work at least this soon).</summary>
    void Mark(CubeId c, CubeState s, int rank)
    {
        if (!_space.Inside(c)) return;
        if (!_wanted.TryGetValue(c, out var had) || s > had) _wanted[c] = s;
        if (!_rank.TryGetValue(c, out var r) || rank < r) _rank[c] = rank;
    }

    /// <summary>What is no longer wanted, once it has been so for UnloadDelay and nothing pins it.</summary>
    void LetGo(float dt)
    {
        List<CubeId>? empty = null;
        foreach (var (c, e) in _cubes)
        {
            var want = _wanted.TryGetValue(c, out var w) ? w : CubeState.Unloaded;
            // Each piece of work by itself: a cube wanted one way on one side of a line and another on the other keeps both.
            // What is built on the terrain goes before the terrain.
            foreach (var work in LetGoOrder)
            {
                if (!e.Done.TryGetValue(work, out var result)) continue;
                if (Contains(Needs(want), work) || e.Pins > 0 || work == CubeWork.Terrain && e.Done.Count > 1)
                {
                    e.Idle[work] = Contains(Needs(want), work) ? 0f : e.Idle.GetValueOrDefault(work) + dt;
                    continue;
                }
                float idle = e.Idle.GetValueOrDefault(work) + dt;
                e.Idle[work] = idle;
                if (idle < UnloadDelay) continue;
                e.Done.Remove(work);
                e.Idle.Remove(work);
                if (work == CubeWork.Terrain) e.Terrain = null;
                _changes.Add(new StreamChange(c, work, false, result));
            }
            if (e.Done.Count == 0 && e.Pins == 0 && want == CubeState.Unloaded) (empty ??= new()).Add(c);
        }
        if (empty is not null)
            foreach (var c in empty) _cubes.Remove(c);
        // Jobs out for what is no longer wanted: given up.
        foreach (var job in _inFlight.Values)
        {
            var want = _wanted.TryGetValue(job.Cube, out var w) ? w : CubeState.Unloaded;
            if (!Contains(Needs(want), job.Work)) _cancelled.TryAdd(job.Id, 0);
            else _cancelled.TryRemove(job.Id, out _);
        }
    }

    static readonly CubeWork[] LetGoOrder = { CubeWork.Stock, CubeWork.Mesh, CubeWork.PreviewMesh, CubeWork.Terrain };

    /// <summary>The missing work of every wanted cube, most needed first, out to the worker while it has room.</summary>
    void Dispatch(Vector2 her)
    {
        if (_inFlight.Count >= MaxInFlight) return;
        _todo.Clear();
        foreach (var (c, want) in _wanted)
        {
            _cubes.TryGetValue(c, out var e);
            bool hasTerrain = e?.Terrain is not null;
            foreach (var work in Needs(want))
            {
                if (e is not null && e.Done.ContainsKey(work)) continue;
                // (What is built on the terrain waits for it.)
                if (work != CubeWork.Terrain && !hasTerrain) continue;
                if (Out(c, work)) continue;
                _todo.Add((new CubeJob(0, c, work, _rank[c] * 4 + (int)work), Vector2.Distance(c.Middle, her)));
            }
        }
        _todo.Sort((a, b) => a.Job.Priority != b.Job.Priority ? a.Job.Priority.CompareTo(b.Job.Priority) : a.Distance.CompareTo(b.Distance));
        foreach (var (job, _) in _todo)
        {
            if (_inFlight.Count >= MaxInFlight) break;
            var go = job with { Id = ++_nextId };
            _inFlight[go.Id] = go;
            _worker.Start(go, go.Work == CubeWork.Terrain ? null : _cubes[go.Cube].Terrain);
        }
    }

    bool Out(CubeId c, CubeWork work)
    {
        foreach (var job in _inFlight.Values)
            if (job.Cube == c && job.Work == work) return true;
        return false;
    }

    static bool Contains(IEnumerable<CubeWork> works, CubeWork work)
    {
        foreach (var w in works)
            if (w == work) return true;
        return false;
    }
}

/// <summary>
/// The stream's worker on the thread pool: a cube's terrain made here (<see cref="CubeTerrain.Make"/>), the rest by the
/// builder the game gives (meshes, stocking; null where none). Results wait in a queue until <see cref="Drain"/> hands
/// them to the stream on the main thread, a few a frame.
/// </summary>
public sealed class TaskCubeWorker : ICubeWorker
{
    readonly CubeSpace _space;
    readonly Func<CubeJob, CubeTerrain, object?>? _build;
    readonly ConcurrentQueue<(CubeJob Job, object? Result, bool Failed)> _done = new();

    /// <summary>Per kind of work: how many jobs ran, their total and longest time (ms), for the overlay.</summary>
    public ConcurrentDictionary<CubeWork, (int Count, double Total, double Most)> Timing { get; } = new();

    /// <summary>What went wrong in a job (each failed job is tried again while it is wanted): for the game to report.</summary>
    public ConcurrentQueue<string> Errors { get; } = new();

    readonly Func<CubeId, CubeTerrain>? _terrain;

    /// <param name="terrain">Where a cube's terrain comes from (a reef that keeps each cube's once made: CubeReef); by default made afresh.</param>
    public TaskCubeWorker(CubeSpace space, Func<CubeJob, CubeTerrain, object?>? build = null, Func<CubeId, CubeTerrain>? terrain = null)
    {
        _space = space;
        _build = build;
        _terrain = terrain;
    }

    /// <summary>Set once the stream exists (the worker asks it what has been given up).</summary>
    public ReefStream? Stream { get; set; }

    public void Start(CubeJob job, CubeTerrain? terrain) => System.Threading.Tasks.Task.Run(() =>
    {
        // (Given up before it began: handed back as failed, to be tried again should it be wanted again by then.)
        if (Stream?.Cancelled(job.Id) == true)
        {
            _done.Enqueue((job, null, true));
            return;
        }
        try
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            object? result = job.Work == CubeWork.Terrain ? _terrain?.Invoke(job.Cube) ?? CubeTerrain.Make(_space, job.Cube) : _build?.Invoke(job, terrain!);
            double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Timing.AddOrUpdate(job.Work, (1, ms, ms), (_, t) => (t.Count + 1, t.Total + ms, Math.Max(t.Most, ms)));
            _done.Enqueue((job, result, false));
        }
        catch (Exception e)
        {
            Errors.Enqueue($"{job.Work} of {job.Cube}: {e}");
            _done.Enqueue((job, null, true));
        }
    });

    /// <summary>Finished jobs handed to the stream (main thread), at most <paramref name="most"/>; how many were.</summary>
    public int Drain(ReefStream stream, int most = 4)
    {
        int n = 0;
        while (n < most && _done.TryDequeue(out var d))
        {
            if (d.Failed) stream.Fail(d.Job);
            else stream.Complete(d.Job, d.Result);
            n++;
        }
        return n;
    }
}
