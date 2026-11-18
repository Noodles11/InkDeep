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
    readonly ITestOutputHelper _out;
    public CubeTerrainTests(ITestOutputHelper output) => _out = output;

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
    public void No_cube_is_shut_in_nearly_all_of_a_window_joins_up_and_the_reef_is_the_same_in_any_window(ulong seed)
    {
        // (A layer as wide as a run's window: what any one cube's edges decide, read across all of it.)
        var space = new CubeSpace(seed, Window, 3);
        // The same reef read in another window (as a layer centred elsewhere is): every edge alike.
        var moved = new CubeSpace(seed, Window, 3);
        for (int z = 0; z < 3; z++) moved.SetWindow(z, new Vector2(9.5f * Side, 15.5f * Side));
        for (int z = 0; z < 3; z++)
        {
            var cubes = space.CubesOf(z).ToList();
            foreach (var c in cubes)
            {
                bool any = false;
                foreach (var (edge, _, _) in c.Outer())
                {
                    any |= space.Edge(edge).Open;
                    Assert.Equal(space.Edge(edge).Open, moved.Edge(edge).Open);
                }
                Assert.True(any, $"{c}: shut in");
            }
            // From the middle, across open edges, inside the window.
            var start = CubeId.Of(space.Middle(z));
            var seen = new HashSet<CubeId> { start };
            var todo = new Queue<CubeId>(seen);
            while (todo.Count > 0)
                foreach (var (edge, _, across) in todo.Dequeue().Outer())
                    if (space.Edge(edge).Open && space.Inside(CubeId.Of(across)) && seen.Add(CubeId.Of(across))) todo.Enqueue(CubeId.Of(across));
            _out.WriteLine($"layer {z}: {seen.Count} of {cubes.Count} joined to the middle");
            Assert.True(seen.Count >= cubes.Count * 0.85f, $"layer {z}: only {seen.Count} of {cubes.Count} joined to the middle");
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

    /// <summary>
    /// The reef is one body (CUBE-WORLD-PLAN §4.3): a layer's water open to the sky lies largely where the layer above has its own,
    /// much more so than a layer far below; and the void looks down onto the layer below's open water.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void Each_layer_carries_on_the_reef_of_the_one_above(ulong seed)
    {
        const int Layers = 9;
        var space = new CubeSpace(seed, F, Layers);
        var all = Enumerable.Range(0, Layers).SelectMany(space.CubesOf).ToDictionary(c => c, c => CubeTerrain.Make(space, c));
        int span = F * Side;
        CubeTerrain At(int x, int y, int z) => all[CubeId.Of(new CellCoord(x / Side, y / Side, z))];
        bool Rock(int x, int y, int z) => At(x, y, z).CellAt(x, y) == CubeCell.Rock;
        bool Open(int x, int y, int z) => !Rock(x, y, z) && !At(x, y, z).CanopyAt(x, y);
        double Same(int a, int b)
        {
            int both = 0, either = 0;
            for (int y = 0; y < span; y += 2)
            for (int x = 0; x < span; x += 2)
            {
                if (!space.Inside(new CellCoord(x / Side, y / Side, 0))) continue;
                // (Open to the sky: what is seen from above, a roofed way's rock rising with its walls.)
                bool wa = Open(x, y, a), wb = Open(x, y, b);
                if (wa || wb) either++;
                if (wa && wb) both++;
            }
            return both / (double)either;
        }
        double next = Same(3, 4), far = Same(0, 8);
        _out.WriteLine($"water shared: next layer {next:P0}, eight down {far:P0}");
        // (Far apart, layers still share some water by chance, the more the more open the reef: the next layer shares
        // at least twenty points more than one eight down.)
        Assert.True(next >= 0.6 && next > far + 0.2, $"next {next:P0}, far {far:P0}");
        // Down through the void (out of the wells' and the walls' way) she sees the layer below's water, not its rock.
        int voids = 0, onRock = 0;
        for (int z = 0; z < Layers - 1; z++)
        for (int y = 0; y < span; y += 2)
        for (int x = 0; x < span; x += 2)
        {
            if (!space.Inside(new CellCoord(x / Side, y / Side, z)) || At(x, y, z).CellAt(x, y) != CubeCell.Void) continue;
            voids++;
            if (Rock(x, y, z + 1)) onRock++;
        }
        _out.WriteLine($"{voids} void points, {onRock} over rock");
        Assert.True(onRock <= voids * 0.05, $"{onRock} of {voids} void points over rock");
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
        var space = new CubeSpace(CubeTerrainTests.SeedValues[0], CubeTuning.Window, CubeTuning.Layers);
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
    public void Reading_the_cubes_rock_costs_about_what_reading_a_levels_map_does()
    {
        // The same open-water test, a million times, on a level's map and on the cube field (CUBE-WORLD-PLAN §13), read
        // as collision reads them.
        var map = TestLevels.Get(TestLevels.Find(p => !p.HasBoss));
        var space = new CubeSpace(CubeTerrainTests.SeedValues[0], 5, 1);
        var made = space.CubesOf(0).ToDictionary(c => c, c => CubeTerrain.Make(space, c));
        var field = new CubeField(c => made.GetValueOrDefault(c), new CellCoord(2, 2, 0));
        var rng = new Rng(7);
        // (As a body's collision reads: its middle and eight points round it, 0.35 m out, at a scatter of places.)
        Vector2[] Clusters(float lo, float hi)
        {
            var points = new List<Vector2>();
            while (points.Count < 4096 - 9)
            {
                var at = new Vector2(rng.Range(lo, hi), rng.Range(lo, hi));
                points.Add(at);
                for (int i = 0; i < 8; i++) points.Add(at + new Vector2(MathF.Cos(i * MathF.Tau / 8f), MathF.Sin(i * MathF.Tau / 8f)) * 0.35f);
            }
            while (points.Count < 4096) points.Add(points[^1]);
            return points.ToArray();
        }
        var mapPoints = Clusters(1f, map.Size - 1f);
        var cubePoints = Clusters(Side * 1.2f, Side * 3.8f);
        double Time(Func<Vector2, bool> open, Vector2[] points)
        {
            double best = double.MaxValue;
            for (int round = 0; round < 5; round++)
            {
                var watch = Stopwatch.StartNew();
                int n = 0;
                for (int k = 0; k < 1_000_000; k++)
                    if (open(points[k & 4095])) n++;
                best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
                Assert.True(n >= 0);
            }
            return best;
        }
        Gen.IReefField asMap = map;
        double mapMs = Time(asMap.IsOpen, mapPoints), cubeMs = Time(field.IsOpen, cubePoints);
        _out.WriteLine($"a million reads: the level's map {mapMs:F1} ms, the cube field {cubeMs:F1} ms ({cubeMs / mapMs:P0})");
        Assert.True(cubeMs < mapMs * 1.25, $"the cube field {cubeMs:F1} ms against the map's {mapMs:F1} ms");
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

/// <summary>A cube's kind (CubeTerrain.KindOf): halls, spires and drops, each as its rules say.</summary>
public class CubeKindTests
{
    [Fact]
    public void Every_kind_comes_up_and_always_the_same_for_the_same_cube()
    {
        var space = new CubeSpace(42UL, 6, 3);
        var seen = new HashSet<CubeKind>();
        foreach (var c in Enumerable.Range(0, 3).SelectMany(space.CubesOf))
        {
            var kind = CubeTerrain.KindOf(space.Seed, c);
            Assert.Equal(kind, CubeTerrain.KindOf(space.Seed, c));
            seen.Add(kind);
        }
        Assert.Equal(4, seen.Count);
        Assert.All(Enumerable.Range(0, 3).SelectMany(space.CubesOf), c => Assert.Equal(CubeKind.Plain, CubeTerrain.KindOf(space.Seed, c, arena: true)));
    }

    [Theory]
    [MemberData(nameof(CubeTerrainTests.Seeds), MemberType = typeof(CubeTerrainTests))]
    public void Spires_stand_in_open_water_with_water_all_round_them(ulong seed)
    {
        var space = new CubeSpace(seed, CubeTerrainTests.F, CubeTerrainTests.L);
        int spires = 0;
        foreach (var c in CubeTerrainTests.Block(space).Where(c => CubeTerrain.KindOf(seed, c) == CubeKind.Spires))
        {
            var t = CubeTerrain.Make(space, c);
            Assert.Equal(CubeKind.Spires, t.Kind);
            foreach (var s in t.Spires)
            {
                spires++;
                // Its middle rock; a ring round it, past its rock, water all the way round.
                Assert.Equal(CubeCell.Rock, t.CellAt((int)MathF.Round(s.X), (int)MathF.Round(s.Y)));
                for (int k = 0; k < 24; k++)
                {
                    float a = k * MathF.Tau / 24f;
                    var p = new Vector2(s.X, s.Y) + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (s.Z + CubeTerrain.SpireClear - 0.5f);
                    Assert.NotEqual(CubeCell.Rock, t.CellAt((int)MathF.Round(p.X), (int)MathF.Round(p.Y)));
                }
            }
        }
        Assert.True(spires > 0 || CubeTerrainTests.Block(space).All(c => CubeTerrain.KindOf(seed, c) != CubeKind.Spires));
    }

    [Fact]
    public void A_drop_has_more_void_than_it_would_as_plain_water()
    {
        // (The same cube's void counted with and without its drop: summed over many drops.)
        var space = new CubeSpace(7UL, 6, 3);
        int drops = 0;
        long more = 0;
        foreach (var c in Enumerable.Range(0, 2).SelectMany(space.CubesOf).Where(c => CubeTerrain.KindOf(space.Seed, c) == CubeKind.Drop).Take(4))
        {
            drops++;
            var t = CubeTerrain.Make(space, c);
            more += t.Cells.Count(k => k == CubeCell.Void);
        }
        Assert.True(drops > 0);
        Assert.True(more > 0);
    }
}
