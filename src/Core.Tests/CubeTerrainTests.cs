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

/// <summary>The cube world's terrain (docs/proposals/CUBE-WORLD-PLAN.md §2–§4, §12): herringbone cubes, three layers.</summary>
public class CubeTerrainTests
{
    public static readonly ulong[] SeedValues = { 0x4B454C50UL, 7UL, 0xC0FFEEUL, 123456789UL, 0xDEADBEEFUL, 42UL };
    public static IEnumerable<object[]> Seeds => SeedValues.Select(s => new object[] { s });

    /// <summary>The prototype's reef: four squares a side, three layers.</summary>
    public const int F = 4, L = 3;

    public static IEnumerable<CubeId> Block(CubeSpace space) => Enumerable.Range(0, L).SelectMany(space.CubesOf);

    public static Dictionary<CubeId, CubeTerrain> MakeAll(CubeSpace space) => Block(space).ToDictionary(c => c, c => CubeTerrain.Make(space, c));

    [Fact]
    public void The_squares_are_laid_in_a_herringbone_of_two_square_cubes_half_of_them_on_end()
    {
        var cubes = new HashSet<CubeId>();
        for (int y = -8; y < 8; y++)
        for (int x = -8; x < 8; x++)
        {
            var cell = new CellCoord(x, y, 0);
            var cube = CubeId.Of(cell);
            Assert.True(cube.Holds(cell));
            Assert.Equal(cube, CubeId.Of(cube.First));
            Assert.Equal(cube, CubeId.Of(cube.Second));
            cubes.Add(cube);
        }
        int lying = cubes.Count(c => !c.OnEnd), standing = cubes.Count(c => c.OnEnd);
        Assert.InRange(lying, standing - 8, standing + 8);
        foreach (var c in cubes)
        {
            Assert.Equal(6, c.Outer().Length);
            Assert.Equal(6, c.Corners().Distinct().Count());
            Assert.All(c.Outer(), o => Assert.NotEqual(c, CubeId.Of(o.Across)));
        }
        // The herringbone's L: a lying cube with a standing one under its west half, and the next lying cube a square down
        // the diagonal.
        var a = new CubeId(0, 0, 0, false);
        Assert.Equal(new CubeId(0, 1, 0, true), CubeId.Of(new CellCoord(0, 1, 0)));
        Assert.Equal(new CubeId(1, 1, 0, false), CubeId.Of(new CellCoord(1, 1, 0)));
        Assert.Equal(a, CubeId.Of(new CellCoord(1, 0, 0)));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_cube_is_the_same_made_twice_in_any_order_on_any_thread(ulong seed)
    {
        var first = MakeAll(new CubeSpace(seed, F, L));
        var space = new CubeSpace(seed, F, L);
        var order = Block(space).Reverse().ToArray();
        var again = new CubeTerrain[order.Length];
        Parallel.For(0, order.Length, k => again[k] = CubeTerrain.Make(space, order[k]));
        foreach (var b in again)
        {
            var a = first[b.Cube];
            Assert.Equal(a.Cells, b.Cells);
            Assert.Equal(a.Heights, b.Heights);
            Assert.Equal(a.Canopy, b.Canopy);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Neighbours_agree_on_every_sample_they_both_hold(ulong seed)
    {
        var all = MakeAll(new CubeSpace(seed, F, L)).Values.ToList();
        int pairs = 0;
        foreach (var a in all)
        foreach (var b in all)
        {
            if (a.Cube.Z != b.Cube.Z || !(a.Cube.Y < b.Cube.Y || a.Cube.Y == b.Cube.Y && a.Cube.X < b.Cube.X)) continue;
            int x0 = Math.Max(a.X0, b.X0), x1 = Math.Min(a.X0 + a.Stride, b.X0 + b.Stride);
            int y0 = Math.Max(a.Y0, b.Y0), y1 = Math.Min(a.Y0 + a.Rows, b.Y0 + b.Rows);
            if (x0 >= x1 || y0 >= y1) continue;
            pairs++;
            for (int gy = y0; gy < y1; gy++)
            for (int gx = x0; gx < x1; gx++)
            {
                Assert.True(a.CellAt(gx, gy) == b.CellAt(gx, gy), $"{a.Cube}/{b.Cube} at ({gx}, {gy}): {a.CellAt(gx, gy)} vs {b.CellAt(gx, gy)}");
                Assert.Equal(a.HeightAt(gx, gy), b.HeightAt(gx, gy));
                Assert.Equal(a.CanopyAt(gx, gy), b.CanopyAt(gx, gy));
            }
        }
        Assert.True(pairs > 0);
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
            // Within 7 m of each of its six corners, all rock: no water joins two cubes that share only a corner.
            foreach (var corner in t.Cube.Corners())
            {
                var at = corner.Point;
                for (int y = -7; y <= 7; y++)
                for (int x = -7; x <= 7; x++)
                {
                    if (x * x + y * y > 49) continue;
                    int gx = (int)at.X + x, gy = (int)at.Y + y;
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
            var cubes = space.CubesOf(z).ToList();
            Assert.True(cubes.Count >= 4, $"{cubes.Count} cubes");
            foreach (var c in cubes)
            foreach (var (edge, _, across) in c.Outer())
                if (!space.Inside(CubeId.Of(across))) Assert.False(space.Edge(edge).Open);
            var seen = new HashSet<CubeId> { cubes[0] };
            var todo = new Queue<CubeId>(seen);
            while (todo.Count > 0)
                foreach (var (edge, _, across) in todo.Dequeue().Outer())
                    if (space.Edge(edge).Open && seen.Add(CubeId.Of(across))) todo.Enqueue(CubeId.Of(across));
            Assert.Equal(cubes.Count, seen.Count);
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
            Assert.All(ids, id => Assert.True(id > 0, $"{t.Cube}: a gate or well not in open water"));
            Assert.True(ids.Distinct().Count() == 1, $"{t.Cube}: its gates and wells are not all joined");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_square_has_a_well_or_two_she_can_dive_through(ulong seed)
    {
        var space = new CubeSpace(seed, F, L);
        var all = MakeAll(space);
        foreach (var cube in Block(space).Where(c => c.Z < L - 1))
        foreach (var cell in new[] { cube.First, cube.Second })
        {
            var wells = space.Floor(cell.Under).Wells;
            Assert.InRange(wells.Count, 1, 2);
            var lower = all[CubeId.Of(cell with { Z = cell.Z + 1 })];
            foreach (var w in wells) Assert.True(CubeTerrain.CanDive(all[cube], lower, w.Center, w.Radius - 0.6f), $"{cell}: well at {w.Center}");
            // The ways in from above and the ways on down are apart.
            foreach (var w in wells)
            foreach (var a in space.Floor(cell.Over).Wells)
                Assert.True(Vector2.Distance(w.Center, a.Center) >= 14f);
        }
        Assert.All(space.CubesOf(L - 1), c => Assert.Empty(all[c].Down));
        Assert.All(space.CubesOf(0), c => Assert.Empty(all[c].Arrivals));
    }

    /// <summary>The cube's own open water (1 m clear of rock) labelled by region (0: none).</summary>
    static int[] Regions(CubeTerrain t)
    {
        int w = t.Stride, h = t.Rows;
        var label = new int[w * h];
        bool Clear(int i, int j) =>
            i > 0 && j > 0 && i < w - 1 && j < h - 1
            && t.Cells[j * w + i] != CubeCell.Rock && t.Cells[j * w + i - 1] != CubeCell.Rock && t.Cells[j * w + i + 1] != CubeCell.Rock
            && t.Cells[(j - 1) * w + i] != CubeCell.Rock && t.Cells[(j + 1) * w + i] != CubeCell.Rock;
        int next = 0;
        var todo = new Stack<int>();
        for (int s = 0; s < w * h; s++)
        {
            if (label[s] != 0 || !Clear(s % w, s / w)) continue;
            label[s] = ++next;
            todo.Push(s);
            while (todo.Count > 0)
            {
                int k = todo.Pop();
                int i = k % w, j = k / w;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int a = i + di, b = j + dj;
                    // (Only its own squares: water beyond them is its neighbours'.)
                    if (a < Apron || b < Apron || a > w - 1 - Apron || b > h - 1 - Apron) continue;
                    if (label[b * w + a] != 0 || !Clear(a, b)) continue;
                    label[b * w + a] = next;
                    todo.Push(b * w + a);
                }
            }
        }
        return label;
    }

    /// <summary>
    /// Not a check: with CUBE_SHEET set to a folder, draws a reef six squares a side there (CUBE-WORLD-PLAN §14.2
    /// step 3), one sheet a layer, side by side, with and without the cubes' outlines.
    /// </summary>
    [Fact]
    public void Draws_the_block_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("CUBE_SHEET");
        if (string.IsNullOrEmpty(dir)) return;
        foreach (var seed in SeedValues.Take(3))
        {
            var space = new CubeSpace(seed, F + 2, L);
            var all = Enumerable.Range(0, L).SelectMany(space.CubesOf).ToDictionary(c => c, c => CubeTerrain.Make(space, c));
            CubeSheet.Write(System.IO.Path.Combine(dir, $"cubes-{seed:X}.png"), all, F + 2, L, lines: false);
            CubeSheet.Write(System.IO.Path.Combine(dir, $"cubes-{seed:X}-lines.png"), all, F + 2, L, lines: true);
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
        var cubes = space.CubesOf(4).Take(12).ToList();
        CubeTerrain.Make(space, cubes[0]);
        double best = double.MaxValue;
        for (int round = 0; round < 3; round++)
        {
            var watch = Stopwatch.StartNew();
            foreach (var c in cubes) CubeTerrain.Make(space, c.OnLayer(round + 1));
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds / cubes.Count);
        }
        _out.WriteLine($"{best:F2} ms a cube");
        // (CUBE-WORLD-PLAN §13: 20 ms a cube of two squares on a worker; the test machine is given room.)
        Assert.True(best < 30.0, $"{best:F2} ms a cube");
    }

    [Fact]
    public void The_stream_keeps_its_books_within_its_budget()
    {
        var space = new CubeSpace(CubeTerrainTests.SeedValues[0], 6, 3);
        var worker = new TaskCubeWorker(space);
        var stream = new ReefStream(space, worker);
        worker.Stream = stream;
        var at = space.CubesOf(0).OrderBy(c => Vector2.Distance(c.Middle, new Vector2(3f * Side))).First().First.Origin + new Vector2(Side * 0.5f);
        var focus = new StreamFocus { Position = at, Velocity = new Vector2(6f, 0f) };
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < 20 && (stream.Held.Count() < stream.Wanted.Count || stream.InFlight.Count > 0))
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
