using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using OctoShoots.Core.Gen.Cubes;
using Xunit;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Tests;

/// <summary>The world-streaming manager (CUBE-WORLD-PLAN §5.2, §14.2 step 5), driven by a worker the test runs by hand.</summary>
public class ReefStreamTests
{
    const ulong Seed = 0x4B454C50UL;
    const float Frame = 1f / 60f;

    /// <summary>Holds the jobs it is given until the test finishes them (terrain made for real, the rest a token).</summary>
    sealed class HandWorker : ICubeWorker
    {
        readonly CubeSpace _space;
        public readonly List<CubeJob> Started = new();
        public readonly List<CubeJob> Waiting = new();
        public int MostOut;

        public HandWorker(CubeSpace space) => _space = space;

        public void Start(CubeJob job, CubeTerrain? terrain)
        {
            if (job.Work != CubeWork.Terrain) Assert.NotNull(terrain);
            Started.Add(job);
            Waiting.Add(job);
            MostOut = Math.Max(MostOut, Waiting.Count);
        }

        public void FinishAll(ReefStream stream)
        {
            var now = Waiting.ToList();
            Waiting.Clear();
            foreach (var job in now) stream.Complete(job, job.Work == CubeWork.Terrain ? CubeTerrain.Make(_space, job.Cube) : $"{job.Work} {job.Cube}");
        }
    }

    static (ReefStream Stream, HandWorker Worker, CubeSpace Space) Make(int footprint = 7, int layers = 3)
    {
        var space = new CubeSpace(Seed, footprint, layers);
        var worker = new HandWorker(space);
        return (new ReefStream(space, worker), worker, space);
    }

    /// <summary>The middle of a square.</summary>
    static Vector2 Middle(CellCoord c) => c.Origin + new Vector2(Side * 0.5f);

    /// <summary>The square of the reef nearest the footprint's middle (whose 3 × 3 round it is all reef), its cube.</summary>
    static CellCoord Centre(CubeSpace space) => new(space.Footprint / 2, space.Footprint / 2, 0);

    static StreamFocus At(Vector2 p, int layer = 0, bool climbable = false) => new() { Position = p, Layer = layer, Climbable = climbable };

    /// <summary>Frames until the stream has nothing more to do (every job finished as soon as it is out).</summary>
    static void Settle(ReefStream stream, HandWorker worker, StreamFocus focus)
    {
        for (int k = 0; k < 600; k++)
        {
            stream.Update(focus, Frame);
            if (worker.Waiting.Count == 0) return;
            worker.FinishAll(stream);
        }
        throw new Exception("never settled");
    }

    static void Run(ReefStream stream, HandWorker worker, StreamFocus focus, float seconds)
    {
        for (float t = 0f; t < seconds; t += Frame)
        {
            stream.Update(focus, Frame);
            worker.FinishAll(stream);
        }
    }

    [Fact]
    public void At_rest_she_has_her_cube_active_what_she_can_reach_preloaded_and_what_she_can_see_previewed()
    {
        var (stream, worker, space) = Make();
        var cell = Centre(space);
        var here = CubeId.Of(cell);
        Settle(stream, worker, At(Middle(cell)));
        Assert.Equal(CubeState.Active, stream.State(here));
        // Across its six outer edges: what an open edge leads to she can swim into.
        var reach = here.Outer().GroupBy(o => CubeId.Of(o.Across)).ToDictionary(g => g.Key, g => g.Any(o => space.Edge(o.Edge).Open));
        foreach (var (next, open) in reach)
            if (space.Inside(next)) Assert.Equal(open ? CubeState.Preloaded : CubeState.Preview, stream.State(next));
        // The cubes of the squares round hers (the view), and under them.
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            var near = CubeId.Of(new CellCoord(cell.X + dx, cell.Y + dy, 0));
            if (near != here && !reach.ContainsKey(near)) Assert.Equal(CubeState.Preview, stream.State(near));
            Assert.Equal(CubeState.Below, stream.State(CubeId.Of(new CellCoord(cell.X + dx, cell.Y + dy, 1))));
        }
        Assert.Equal(stream.Wanted.Count, stream.Held.Count());
        Assert.All(stream.Held, c => Assert.Equal(stream.Wanted[c], stream.State(c)));
        Assert.DoesNotContain(stream.Held, c => c.Z == 2);
        Assert.Equal(stream.Terrain(here)!.Cells, CubeTerrain.Make(space, here).Cells);
    }

    [Fact]
    public void Her_cube_comes_first_terrain_before_what_is_built_on_it_a_few_jobs_at_a_time()
    {
        var (stream, worker, space) = Make();
        var here = CubeId.Of(Centre(space));
        Settle(stream, worker, At(Middle(Centre(space))));
        var jobs = worker.Started;
        Assert.Equal(here, jobs[0].Cube);
        Assert.Equal(CubeWork.Terrain, jobs[0].Work);
        int lastOfHers = jobs.FindLastIndex(j => j.Cube == here);
        int firstBelow = jobs.FindIndex(j => j.Cube.Z == 1);
        Assert.True(lastOfHers < firstBelow, $"her cube's last job at {lastOfHers}, the first below at {firstBelow}");
        for (int i = 0; i < jobs.Count; i++)
            if (jobs[i].Work != CubeWork.Terrain)
                Assert.Contains(jobs.Take(i), j => j.Cube == jobs[i].Cube && j.Work == CubeWork.Terrain);
        Assert.InRange(worker.MostOut, 1, ReefStream.MaxInFlight);
        Assert.Equal(jobs.Count, jobs.Select(j => (j.Cube, j.Work)).Distinct().Count());
    }

    [Fact]
    public void Crossing_a_line_back_and_forth_lets_nothing_go()
    {
        var (stream, worker, space) = Make();
        var here = CubeId.Of(Centre(space));
        var (edge, _, _) = here.Outer().First(o => space.Inside(CubeId.Of(o.Across)));
        var one = edge.Point(Side * 0.5f, -4f);
        var two = edge.Point(Side * 0.5f, 4f);
        Settle(stream, worker, At(one));
        Settle(stream, worker, At(two));
        stream.Changes.Clear();
        for (int k = 0; k < 20; k++) Run(stream, worker, At(k % 2 == 0 ? one : two), 1f);
        Assert.DoesNotContain(stream.Changes, c => !c.Added);
    }

    /// <summary>Where she goes two squares off, and a cube she held at first that is not wanted there.</summary>
    static (Vector2 There, CubeId Behind) Leave(ReefStream stream, HandWorker worker, CubeSpace space)
    {
        var from = Centre(space);
        Settle(stream, worker, At(Middle(from)));
        var held = stream.Held.Where(c => c.Z == 0).ToHashSet();
        var to = new CellCoord(from.X + 2, from.Y, 0);
        stream.Update(At(Middle(to)), Frame);
        worker.FinishAll(stream);
        var behind = held.First(c => !stream.Wanted.ContainsKey(c));
        return (Middle(to), behind);
    }

    [Fact]
    public void What_she_leaves_behind_is_let_go_only_after_the_delay()
    {
        var (stream, worker, space) = Make();
        var (there, behind) = Leave(stream, worker, space);
        stream.Changes.Clear();
        Run(stream, worker, At(there), ReefStream.UnloadDelay - 0.2f);
        Assert.DoesNotContain(stream.Changes, c => !c.Added && c.Cube == behind);
        Assert.True(stream.Has(behind, CubeWork.Terrain));
        Run(stream, worker, At(there), 0.4f);
        Assert.Contains(stream.Changes, c => !c.Added && c.Cube == behind && c.Work == CubeWork.Terrain);
        Assert.DoesNotContain(behind, stream.Held);
        // What is built on the terrain goes before it.
        var gone = stream.Changes.Where(c => !c.Added && c.Cube == behind).Select(c => c.Work).ToList();
        Assert.Equal(CubeWork.Terrain, gone[^1]);
    }

    [Fact]
    public void A_pinned_cube_is_kept_until_it_is_let_go()
    {
        var (stream, worker, space) = Make();
        var (there, behind) = Leave(stream, worker, space);
        var pin = stream.Pin(behind, "a creature awake");
        Run(stream, worker, At(there), 5f);
        Assert.Contains(behind, stream.Held);
        Assert.Equal(new[] { "a creature awake" }, stream.PinnedBy(behind));
        Assert.NotEqual(CubeState.Unloaded, stream.State(behind));
        pin.Dispose();
        Run(stream, worker, At(there), ReefStream.UnloadDelay + 0.2f);
        Assert.DoesNotContain(behind, stream.Held);
    }

    [Fact]
    public void Work_no_longer_wanted_is_given_up_and_its_result_thrown_away()
    {
        var (stream, worker, space) = Make();
        var start = Centre(space);
        stream.Update(At(Middle(start)), Frame);
        var out1 = worker.Waiting.ToList();
        Assert.Contains(out1, j => j.Cube == CubeId.Of(start));
        // She is far off before it comes back.
        var far = space.CubesOf(0).OrderByDescending(c => Vector2.Distance(c.Middle, Middle(start))).First();
        stream.Update(At(Middle(far.First)), Frame);
        var gone = out1.Where(j => !stream.Wanted.ContainsKey(j.Cube)).ToList();
        Assert.NotEmpty(gone);
        Assert.All(gone, j => Assert.True(stream.Cancelled(j.Id)));
        stream.Changes.Clear();
        worker.FinishAll(stream);
        Assert.DoesNotContain(stream.Changes, c => gone.Any(j => j.Cube == c.Cube));
        Assert.DoesNotContain(CubeId.Of(start), stream.Held);
    }

    [Fact]
    public void Diving_readies_the_layer_below_and_the_layer_left_is_kept_as_masks_while_she_may_climb_back()
    {
        var (stream, worker, space) = Make();
        var cell = Centre(space);
        var focus = At(Middle(cell));
        focus.ReadyBelow = true;
        Settle(stream, worker, focus);
        Assert.Equal(CubeState.Preloaded, stream.State(CubeId.Of(cell with { Z = 1 })));
        // (The rest of the layer she will land on is readied in full too: its ground, not the simplified one.)
        Assert.True(stream.State(CubeId.Of(new CellCoord(cell.X - 1, cell.Y - 1, 1))) >= CubeState.Preview);

        // Down a layer, the one she left above to climb back to.
        Run(stream, worker, At(Middle(cell), 1, climbable: true), ReefStream.UnloadDelay + 0.2f);
        Assert.Equal(CubeState.Active, stream.State(CubeId.Of(cell with { Z = 1 })));
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            var above = CubeId.Of(new CellCoord(cell.X + dx, cell.Y + dy, 0));
            Assert.Equal(CubeState.Mask, stream.State(above));
            Assert.False(stream.Has(above, CubeWork.Mesh) || stream.Has(above, CubeWork.PreviewMesh));
        }
        // Deeper than it now (she may no longer climb there): the layer above let go.
        Run(stream, worker, At(Middle(cell), 1, climbable: false), ReefStream.UnloadDelay + 0.2f);
        Assert.DoesNotContain(stream.Held, c => c.Z == 0);
    }

    [Fact]
    public void Across_a_shut_edge_her_neighbour_is_only_seen()
    {
        var (stream, worker, space) = Make();
        var cell = Centre(space);
        var here = CubeId.Of(cell);
        var next = here.Outer().Select(o => CubeId.Of(o.Across)).First(n => here.Outer().Any(o => CubeId.Of(o.Across) == n && space.Edge(o.Edge).Open));
        var between = here.Outer().Where(o => CubeId.Of(o.Across) == next).Select(o => o.Edge).ToHashSet();
        var focus = At(Middle(cell));
        Settle(stream, worker, focus);
        Assert.Equal(CubeState.Preloaded, stream.State(next));
        focus.Shut = between.Contains;
        Run(stream, worker, focus, ReefStream.UnloadDelay + 0.2f);
        Assert.Equal(CubeState.Preview, stream.Wanted[next]);
        Assert.Equal(CubeState.Preview, stream.State(next));
    }

    [Fact]
    public void Heading_for_a_gate_makes_the_cube_beyond_it_early()
    {
        var (stream, worker, space) = Make(footprint: 9);
        var cell = Centre(space);
        var here = CubeId.Of(cell);
        var (edge, inside, across) = here.Outer().First(o => space.Edge(o.Edge).Open);
        var gate = edge.Point(space.Edge(edge).Gates[0].Along, 0f);
        var next = CubeId.Of(across);
        var beyond = CubeId.Of(new CellCoord(2 * across.X - inside.X, 2 * across.Y - inside.Y, 0));
        if (beyond == next) beyond = CubeId.Of(new CellCoord(3 * across.X - 2 * inside.X, 3 * across.Y - 2 * inside.Y, 0));
        var from = gate + Vector2.Normalize(Middle(inside) - gate) * 12f;
        var focus = At(from);
        stream.Update(focus, Frame);
        bool seenAnyway = stream.Wanted.ContainsKey(beyond);
        focus.Velocity = Vector2.Normalize(gate - from) * 6f;
        stream.Update(focus, Frame);
        Assert.True(stream.Wanted.ContainsKey(beyond));
        if (seenAnyway) return;
        // Swimming away from it: not.
        focus.Velocity = -focus.Velocity;
        stream.Update(focus, Frame);
        Assert.False(stream.Wanted.ContainsKey(beyond));
    }

    [Fact]
    public void Nothing_is_asked_for_outside_the_reef()
    {
        var (stream, worker, space) = Make(footprint: 4, layers: 2);
        Settle(stream, worker, At(Middle(space.CubesOf(1).First().First), 1, climbable: true));
        Assert.All(worker.Started, j => Assert.True(space.Inside(j.Cube), $"{j.Cube}"));
        Assert.DoesNotContain(worker.Started, j => j.Cube.Z == 2);
    }

    [Fact]
    public void The_thread_pool_worker_makes_the_same_terrain_and_hands_it_over_on_the_main_thread()
    {
        var space = new CubeSpace(Seed, 7, 3);
        var worker = new TaskCubeWorker(space, (job, terrain) => $"{job.Work} of {terrain.Cube}");
        var stream = new ReefStream(space, worker);
        worker.Stream = stream;
        var cell = Centre(space);
        var focus = At(Middle(cell));
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 20)
        {
            stream.Update(focus, Frame);
            worker.Drain(stream);
            if (stream.InFlight.Count == 0 && stream.Held.Count() == stream.Wanted.Count && stream.Held.All(c => stream.State(c) == stream.Wanted[c])) break;
            Thread.Sleep(1);
        }
        Assert.Equal(stream.Wanted.Count, stream.Held.Count());
        var here = CubeId.Of(cell);
        Assert.Equal(CubeState.Active, stream.State(here));
        Assert.Equal(CubeTerrain.Make(space, here).Heights, stream.Terrain(here)!.Heights);
        Assert.Equal($"Mesh of {here}", stream.Result(here, CubeWork.Mesh));
    }
}
