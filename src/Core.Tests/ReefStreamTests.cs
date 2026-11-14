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

    static (ReefStream Stream, HandWorker Worker, CubeSpace Space) Make(int footprint = 5, int layers = 3)
    {
        var space = new CubeSpace(Seed, footprint, layers);
        var worker = new HandWorker(space);
        return (new ReefStream(space, worker), worker, space);
    }

    static Vector2 Middle(int x, int y) => new((x + 0.5f) * Side, (y + 0.5f) * Side);

    static StreamFocus At(Vector2 p, int layer = 0, bool climbable = false) => new() { Position = p, Layer = layer, Climbable = climbable };

    /// <summary>Frames until the stream has nothing more to do (every job finished as soon as it is out).</summary>
    static void Settle(ReefStream stream, HandWorker worker, StreamFocus focus)
    {
        for (int k = 0; k < 400; k++)
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
        var here = new CubeCoord(2, 2, 0);
        Settle(stream, worker, At(Middle(2, 2)));
        Assert.Equal(CubeState.Active, stream.State(here));
        foreach (var (edge, next) in new[] { (here.West, new CubeCoord(1, 2, 0)), (here.East, new CubeCoord(3, 2, 0)), (here.North, new CubeCoord(2, 1, 0)), (here.South, new CubeCoord(2, 3, 0)) })
            Assert.Equal(space.Edge(edge).Open ? CubeState.Preloaded : CubeState.Preview, stream.State(next));
        foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            Assert.Equal(CubeState.Preview, stream.State(new CubeCoord(2 + dx, 2 + dy, 0)));
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            Assert.Equal(CubeState.Preview, stream.State(new CubeCoord(2 + dx, 2 + dy, 1)));
        // Nothing beyond: no layer below that, none above, nothing two cubes off.
        Assert.Equal(18, stream.Held.Count());
        Assert.All(stream.Held, c => Assert.Equal(stream.Wanted[c], stream.State(c)));
        Assert.Equal(CubeState.Unloaded, stream.State(new CubeCoord(0, 0, 0)));
        Assert.Equal(stream.Terrain(here)!.Cells, CubeTerrain.Make(space, here).Cells);
    }

    [Fact]
    public void Her_cube_comes_first_terrain_before_what_is_built_on_it_a_few_jobs_at_a_time()
    {
        var (stream, worker, _) = Make();
        var here = new CubeCoord(2, 2, 0);
        Settle(stream, worker, At(Middle(2, 2)));
        var jobs = worker.Started;
        Assert.Equal(new CubeJob(jobs[0].Id, here, CubeWork.Terrain, jobs[0].Priority), jobs[0]);
        int lastOfHers = jobs.FindLastIndex(j => j.Cube == here);
        int firstBelow = jobs.FindIndex(j => j.Cube.Z == 1);
        Assert.True(lastOfHers < firstBelow, $"her cube's last job at {lastOfHers}, the first below at {firstBelow}");
        for (int i = 0; i < jobs.Count; i++)
            if (jobs[i].Work != CubeWork.Terrain)
                Assert.Contains(jobs.Take(i), j => j.Cube == jobs[i].Cube && j.Work == CubeWork.Terrain);
        Assert.InRange(worker.MostOut, 1, ReefStream.MaxInFlight);
        // Each piece of work done once.
        Assert.Equal(jobs.Count, jobs.Select(j => (j.Cube, j.Work)).Distinct().Count());
    }

    [Fact]
    public void Crossing_a_line_back_and_forth_lets_nothing_go()
    {
        var (stream, worker, _) = Make();
        var west = new Vector2(2 * Side + 60f, 2.5f * Side);
        var east = new Vector2(3 * Side + 4f, 2.5f * Side);
        Settle(stream, worker, At(west));
        Settle(stream, worker, At(east));
        stream.Changes.Clear();
        for (int k = 0; k < 20; k++) Run(stream, worker, At(k % 2 == 0 ? west : east), 1f);
        Assert.DoesNotContain(stream.Changes, c => !c.Added);
    }

    [Fact]
    public void What_she_leaves_behind_is_let_go_only_after_the_delay()
    {
        var (stream, worker, _) = Make();
        Settle(stream, worker, At(Middle(2, 2)));
        stream.Changes.Clear();
        var behind = new CubeCoord(1, 2, 0);
        Assert.True(stream.Has(behind, CubeWork.Terrain));
        Run(stream, worker, At(Middle(3, 2)), ReefStream.UnloadDelay - 0.2f);
        Assert.DoesNotContain(stream.Changes, c => !c.Added);
        Assert.True(stream.Has(behind, CubeWork.Terrain));
        Run(stream, worker, At(Middle(3, 2)), 0.4f);
        Assert.Contains(stream.Changes, c => !c.Added && c.Cube == behind && c.Work == CubeWork.Terrain);
        Assert.DoesNotContain(behind, stream.Held);
        Assert.DoesNotContain(new CubeCoord(1, 1, 1), stream.Held);
        // What is built on the terrain goes before it.
        var gone = stream.Changes.Where(c => !c.Added && c.Cube == behind).Select(c => c.Work).ToList();
        Assert.Equal(CubeWork.Terrain, gone[^1]);
    }

    [Fact]
    public void A_pinned_cube_is_kept_until_it_is_let_go()
    {
        var (stream, worker, _) = Make();
        Settle(stream, worker, At(Middle(2, 2)));
        var behind = new CubeCoord(1, 2, 0);
        var pin = stream.Pin(behind, "a creature awake");
        Run(stream, worker, At(Middle(3, 2)), 5f);
        Assert.Contains(behind, stream.Held);
        Assert.Equal(new[] { "a creature awake" }, stream.PinnedBy(behind));
        Assert.NotEqual(CubeState.Unloaded, stream.State(behind));
        pin.Dispose();
        Run(stream, worker, At(Middle(3, 2)), ReefStream.UnloadDelay + 0.2f);
        Assert.DoesNotContain(behind, stream.Held);
    }

    [Fact]
    public void Work_no_longer_wanted_is_given_up_and_its_result_thrown_away()
    {
        var (stream, worker, _) = Make();
        stream.Update(At(Middle(2, 2)), Frame);
        var out1 = worker.Waiting.ToList();
        Assert.Contains(out1, j => j.Cube == new CubeCoord(2, 2, 0));
        // She is far off before it comes back.
        stream.Update(At(Middle(0, 0)), Frame);
        var far = out1.Where(j => Math.Max(Math.Abs(j.Cube.X), Math.Abs(j.Cube.Y)) >= 2).ToList();
        Assert.NotEmpty(far);
        Assert.All(far, j => Assert.True(stream.Cancelled(j.Id)));
        stream.Changes.Clear();
        worker.FinishAll(stream);
        Assert.DoesNotContain(stream.Changes, c => far.Any(j => j.Cube == c.Cube));
        Assert.DoesNotContain(new CubeCoord(2, 2, 0), stream.Held);
    }

    [Fact]
    public void Diving_readies_the_cube_below_and_the_layer_left_is_kept_as_masks_while_she_may_climb_back()
    {
        var (stream, worker, _) = Make();
        var focus = At(Middle(2, 2));
        focus.ReadyBelow = true;
        Settle(stream, worker, focus);
        Assert.Equal(CubeState.Preloaded, stream.State(new CubeCoord(2, 2, 1)));

        // Down a layer, the one she left above to climb back to.
        Run(stream, worker, At(Middle(2, 2), 1, climbable: true), ReefStream.UnloadDelay + 0.2f);
        Assert.Equal(CubeState.Active, stream.State(new CubeCoord(2, 2, 1)));
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            var above = new CubeCoord(2 + dx, 2 + dy, 0);
            Assert.Equal(CubeState.Mask, stream.State(above));
            Assert.False(stream.Has(above, CubeWork.Mesh) || stream.Has(above, CubeWork.PreviewMesh));
        }
        // Deeper than it now (she may no longer climb there): the layer above let go.
        Run(stream, worker, At(Middle(2, 2), 1, climbable: false), ReefStream.UnloadDelay + 0.2f);
        Assert.DoesNotContain(stream.Held, c => c.Z == 0);
    }

    [Fact]
    public void Across_a_shut_edge_her_neighbour_is_only_seen()
    {
        var (stream, worker, space) = Make();
        var here = new CubeCoord(2, 2, 0);
        var (edge, next) = new[] { (here.West, new CubeCoord(1, 2, 0)), (here.East, new CubeCoord(3, 2, 0)), (here.North, new CubeCoord(2, 1, 0)), (here.South, new CubeCoord(2, 3, 0)) }
            .First(e => space.Edge(e.Item1).Open);
        var focus = At(Middle(2, 2));
        Settle(stream, worker, focus);
        Assert.Equal(CubeState.Preloaded, stream.State(next));
        focus.Shut = e => e == edge;
        Run(stream, worker, focus, ReefStream.UnloadDelay + 0.2f);
        Assert.Equal(CubeState.Preview, stream.Wanted[next]);
        Assert.Equal(CubeState.Preview, stream.State(next));
    }

    [Fact]
    public void Heading_for_a_gate_makes_the_cube_beyond_it_early()
    {
        var (stream, worker, space) = Make();
        var here = new CubeCoord(2, 2, 0);
        var (edge, next) = new[] { (here.West, new CubeCoord(1, 2, 0)), (here.East, new CubeCoord(3, 2, 0)), (here.North, new CubeCoord(2, 1, 0)), (here.South, new CubeCoord(2, 3, 0)) }
            .First(e => space.Edge(e.Item1).Open);
        var gate = edge.Point(space.Edge(edge).Gates[0].Along, 0f);
        var beyond = new CubeCoord(2 * next.X - here.X, 2 * next.Y - here.Y, 0);
        var inside = gate + Vector2.Normalize(Middle(2, 2) - gate) * 12f;
        var focus = At(inside);
        stream.Update(focus, Frame);
        Assert.False(stream.Wanted.ContainsKey(beyond));
        focus.Velocity = Vector2.Normalize(gate - inside) * 6f;
        stream.Update(focus, Frame);
        Assert.Equal(CubeState.Preview, stream.Wanted[beyond]);
        // Swimming away from it: not.
        focus.Velocity = -focus.Velocity;
        stream.Update(focus, Frame);
        Assert.False(stream.Wanted.ContainsKey(beyond));
    }

    [Fact]
    public void Nothing_is_asked_for_outside_the_reef()
    {
        var (stream, worker, space) = Make(footprint: 3, layers: 2);
        Settle(stream, worker, At(Middle(0, 0), 1, climbable: true));
        Assert.All(worker.Started, j => Assert.True(space.Inside(j.Cube), $"{j.Cube}"));
        Assert.DoesNotContain(worker.Started, j => j.Cube.Z == 2);
    }

    [Fact]
    public void The_thread_pool_worker_makes_the_same_terrain_and_hands_it_over_on_the_main_thread()
    {
        var space = new CubeSpace(Seed, 5, 3);
        var worker = new TaskCubeWorker(space, (job, terrain) => $"{job.Work} of {terrain.Coord}");
        var stream = new ReefStream(space, worker);
        worker.Stream = stream;
        var focus = At(Middle(2, 2));
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 20)
        {
            stream.Update(focus, Frame);
            worker.Drain(stream);
            if (stream.InFlight.Count == 0 && stream.Held.All(c => stream.State(c) == stream.Wanted[c]) && stream.Held.Count() == 18) break;
            Thread.Sleep(1);
        }
        Assert.Equal(18, stream.Held.Count());
        var here = new CubeCoord(2, 2, 0);
        Assert.Equal(CubeState.Active, stream.State(here));
        Assert.Equal(CubeTerrain.Make(space, here).Heights, stream.Terrain(here)!.Heights);
        Assert.Equal("Mesh of (2, 2, 0)", stream.Result(here, CubeWork.Mesh));
    }
}
