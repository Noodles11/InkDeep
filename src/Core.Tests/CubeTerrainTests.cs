using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OctoShoots.Core.Gen.Cubes;
using Xunit;
using Xunit.Abstractions;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Tests;

/// <summary>The cube world's terrain (docs/proposals/CUBE-WORLD-PLAN.md §4, §12): the prototype's 3 × 3 × 3 block.</summary>
public class CubeTerrainTests
{
    readonly ITestOutputHelper _out;
    public CubeTerrainTests(ITestOutputHelper output) => _out = output;

    public static readonly ulong[] SeedValues = { 0x4B454C50UL, 7UL, 0xC0FFEEUL, 123456789UL, 0xDEADBEEFUL, 42UL };
    public static IEnumerable<object[]> Seeds => SeedValues.Select(s => new object[] { s });

    const int F = 3, L = 3;

    static IEnumerable<CubeCoord> Block()
    {
        for (int z = 0; z < L; z++)
        for (int y = 0; y < F; y++)
        for (int x = 0; x < F; x++)
            yield return new CubeCoord(x, y, z);
    }

    static Dictionary<CubeCoord, CubeTerrain> MakeAll(CubeSpace space) => Block().ToDictionary(c => c, c => CubeTerrain.Make(space, c));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_cube_is_the_same_made_twice_in_any_order_on_any_thread(ulong seed)
    {
        var first = MakeAll(new CubeSpace(seed, F, L));
        var space = new CubeSpace(seed, F, L);
        var again = new CubeTerrain[F * F * L];
        var order = Block().Reverse().ToArray();
        Parallel.For(0, order.Length, k => again[k] = CubeTerrain.Make(space, order[k]));
        foreach (var b in again)
        {
            var a = first[b.Coord];
            Assert.Equal(a.Cells, b.Cells);
            Assert.Equal(a.Heights, b.Heights);
            Assert.Equal(a.Canopy, b.Canopy);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Neighbours_agree_on_every_sample_they_both_hold(ulong seed)
    {
        var all = MakeAll(new CubeSpace(seed, F, L));
        foreach (var (c, a) in all)
        foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
        {
            if (!all.TryGetValue(c with { X = c.X + dx, Y = c.Y + dy }, out var b)) continue;
            int shared = 0;
            for (int gy = Math.Max(a.Y0, b.Y0); gy < Math.Min(a.Y0, b.Y0) + CubeTerrain.N; gy++)
            for (int gx = Math.Max(a.X0, b.X0); gx < Math.Min(a.X0, b.X0) + CubeTerrain.N; gx++)
            {
                if (!a.Has(gx, gy) || !b.Has(gx, gy)) continue;
                shared++;
                Assert.True(a.CellAt(gx, gy) == b.CellAt(gx, gy), $"{c}/{b.Coord} at ({gx}, {gy}): {a.CellAt(gx, gy)} vs {b.CellAt(gx, gy)}");
                Assert.Equal(a.HeightAt(gx, gy), b.HeightAt(gx, gy));
                Assert.Equal(a.CanopyAt(gx, gy), b.CanopyAt(gx, gy));
            }
            Assert.True(shared > 0);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Gates_keep_off_the_corners_and_corners_are_rock(ulong seed)
    {
        foreach (var t in MakeAll(new CubeSpace(seed, F, L)).Values)
        {
            foreach (var (_, gate, _) in t.Gates)
            {
                Assert.True(gate.Along - gate.Width / 2f >= GateMargin - 0.01f);
                Assert.True(gate.Along + gate.Width / 2f <= Side - GateMargin + 0.01f);
            }
            // Within 7 m of a corner, all rock: no water joins two cubes that share only a corner.
            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                var corner = t.Coord.Corner(dx, dy).Point;
                for (int y = -7; y <= 7; y++)
                for (int x = -7; x <= 7; x++)
                {
                    if (x * x + y * y > 49) continue;
                    int gx = (int)corner.X + x, gy = (int)corner.Y + y;
                    if (t.Has(gx, gy)) Assert.Equal(CubeCell.Rock, t.CellAt(gx, gy));
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void The_rim_is_closed_and_every_layer_joins_up_through_its_open_edges(ulong seed)
    {
        var space = new CubeSpace(seed, F, L);
        for (int z = 0; z < L; z++)
        {
            Assert.False(space.Edge(new CubeCoord(0, 0, z).West).Open);
            Assert.False(space.Edge(new CubeCoord(F - 1, 1, z).East).Open);
            Assert.False(space.Edge(new CubeCoord(1, 0, z).North).Open);
            Assert.False(space.Edge(new CubeCoord(2, F - 1, z).South).Open);
            var seen = new HashSet<CubeCoord> { new(0, 0, z) };
            var todo = new Queue<CubeCoord>(seen);
            while (todo.Count > 0)
            {
                var c = todo.Dequeue();
                foreach (var e in new[] { c.West, c.East, c.North, c.South })
                {
                    if (!space.Edge(e).Open) continue;
                    var next = e.Low == c ? e.High : e.Low;
                    if (seen.Add(next)) todo.Enqueue(next);
                }
            }
            Assert.Equal(F * F, seen.Count);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Inside_a_cube_every_gate_reaches_every_other_and_every_well(ulong seed)
    {
        foreach (var t in MakeAll(new CubeSpace(seed, F, L)).Values)
        {
            var label = Regions(t);
            var points = t.Gates.Select(g => g.Mouth).Concat(t.Down.Select(w => w.Center)).Concat(t.Arrivals.Select(w => w.Center)).ToList();
            var ids = points.Select(p => label[t.Index((int)MathF.Round(p.X), (int)MathF.Round(p.Y))]).ToList();
            Assert.All(ids, id => Assert.True(id > 0, $"{t.Coord}: a gate or well not in open water"));
            Assert.True(ids.Distinct().Count() == 1, $"{t.Coord}: its gates and wells are not all joined");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_floor_has_a_well_or_two_she_can_dive_through(ulong seed)
    {
        var space = new CubeSpace(seed, F, L);
        var all = MakeAll(space);
        foreach (var c in Block().Where(c => c.Z < L - 1))
        {
            var wells = space.Floor(c.Under).Wells;
            Assert.InRange(wells.Count, 1, 2);
            var lower = all[c with { Z = c.Z + 1 }];
            foreach (var w in wells) Assert.True(CubeTerrain.CanDive(all[c], lower, w.Center, w.Radius - 0.6f), $"{c}: well at {w.Center}");
            // The ways in from above and the ways on down are apart.
            foreach (var w in wells)
            foreach (var a in all[c].Arrivals)
                Assert.True(Vector2.Distance(w.Center, a.Center) >= 14f);
        }
        Assert.Empty(all[new CubeCoord(0, 0, L - 1)].Down);
        Assert.Empty(all[new CubeCoord(0, 0, 0)].Arrivals);
    }


    /// <summary>The cube's own open water (1 m clear of rock) labelled by region (0: none).</summary>
    static int[] Regions(CubeTerrain t)
    {
        int n = CubeTerrain.N;
        var label = new int[n * n];
        bool Clear(int i, int j) =>
            i > 0 && j > 0 && i < n - 1 && j < n - 1
            && t.Cells[j * n + i] != CubeCell.Rock && t.Cells[j * n + i - 1] != CubeCell.Rock && t.Cells[j * n + i + 1] != CubeCell.Rock
            && t.Cells[(j - 1) * n + i] != CubeCell.Rock && t.Cells[(j + 1) * n + i] != CubeCell.Rock;
        int next = 0;
        var todo = new Stack<int>();
        for (int s = 0; s < n * n; s++)
        {
            if (label[s] != 0 || !Clear(s % n, s / n)) continue;
            label[s] = ++next;
            todo.Push(s);
            while (todo.Count > 0)
            {
                int k = todo.Pop();
                int i = k % n, j = k / n;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, b = j + dj;
                    // (Only its own square: water beyond it is its neighbours'.)
                    if (a < CubeTuning.Apron || b < CubeTuning.Apron || a > n - 1 - CubeTuning.Apron || b > n - 1 - CubeTuning.Apron) continue;
                    if (label[b * n + a] != 0 || !Clear(a, b)) continue;
                    label[b * n + a] = next;
                    todo.Push(b * n + a);
                }
            }
        }
        return label;
    }

    /// <summary>
    /// Not a check: with CUBE_SHEET set to a folder, draws the 3 × 3 × 3 block there (CUBE-WORLD-PLAN §14.2 step 3),
    /// one sheet a layer, side by side, with and without the cube lines.
    /// </summary>
    [Fact]
    public void Draws_the_block_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("CUBE_SHEET");
        if (string.IsNullOrEmpty(dir)) return;
        foreach (var seed in SeedValues.Take(3))
        {
            var all = MakeAll(new CubeSpace(seed, F, L));
            CubeSheet.Write(System.IO.Path.Combine(dir, $"cubes-{seed:X}.png"), all, F, L, lines: false);
            CubeSheet.Write(System.IO.Path.Combine(dir, $"cubes-{seed:X}-lines.png"), all, F, L, lines: true);
        }
    }
}

/// <summary>Timing, run on its own (not beside the other tests, which would slow it).</summary>
[CollectionDefinition("Cube timing", DisableParallelization = true)]
public class CubeTimingCollection
{
}

[Collection("Cube timing")]
public class CubeTimingTests
{
    readonly ITestOutputHelper _out;
    public CubeTimingTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void A_cube_is_made_within_its_budget()
    {
        var space = new CubeSpace(CubeTerrainTests.SeedValues[0], CubeTuning.Footprint, CubeTuning.Layers);
        CubeTerrain.Make(space, new CubeCoord(4, 4, 4));
        double best = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            var watch = Stopwatch.StartNew();
            int n = 0;
            for (int z = round; z < round + 2; z++)
            for (int y = 2; y < 6; y++)
            for (int x = 2; x < 6; x++, n++)
                CubeTerrain.Make(space, new CubeCoord(x, y, z));
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds / n);
        }
        _out.WriteLine($"{best:F2} ms a cube");
        // (CUBE-WORLD-PLAN §13: 10 ms on a worker; the test machine is given room.)
        Assert.True(best < 20.0, $"{best:F2} ms a cube");
    }

    [Fact]
    public void The_stream_keeps_its_books_within_its_budget()
    {
        var space = new CubeSpace(CubeTerrainTests.SeedValues[0], 5, 3);
        var worker = new TaskCubeWorker(space);
        var stream = new ReefStream(space, worker);
        worker.Stream = stream;
        var focus = new StreamFocus { Position = new System.Numerics.Vector2(2.5f * Side, 2.5f * Side), Velocity = new System.Numerics.Vector2(6f, 0f) };
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 20 && (stream.Held.Count() < 18 || stream.InFlight.Count > 0))
        {
            stream.Update(focus, 1f / 60f);
            worker.Drain(stream);
            System.Threading.Thread.Sleep(1);
        }
        double best = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            watch.Restart();
            for (int k = 0; k < 600; k++) stream.Update(focus, 1f / 60f);
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds / 600);
        }
        _out.WriteLine($"{best * 1000:F1} µs a frame");
        // (CUBE-WORLD-PLAN §13: 0.1 ms a tick.)
        Assert.True(best < 0.1, $"{best * 1000:F1} µs a frame");
    }
}
