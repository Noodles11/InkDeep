using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Plane;
using Xunit;
using Xunit.Abstractions;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Tests;

/// <summary>Swimming in the cube world (CUBE-WORLD-PLAN §14.2 step 4): across cube lines with no hitch.</summary>
public class CubeWorldTests
{
    readonly ITestOutputHelper _out;
    public CubeWorldTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Seeds => CubeTerrainTests.Seeds;

    const int F = 5;

    /// <summary>A layer's cubes, made, in the order the space lists them, and the look-up the world reads them by.</summary>
    static (Func<CubeId, CubeTerrain?> Terrain, List<CubeTerrain> Cubes) Layer(ulong seed, int z = 0)
    {
        var space = new CubeSpace(seed, F, 3);
        var made = space.CubesOf(z).OrderBy(c => c.Y).ThenBy(c => c.X).ToDictionary(c => c, c => CubeTerrain.Make(space, c));
        return (c => made.TryGetValue(c, out var t) ? t : null, made.Values.ToList());
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void She_swims_straight_through_a_gate_at_an_even_speed(ulong seed)
    {
        var (terrain, all) = Layer(seed);
        var tuning = new Tuning();
        float r = tuning.PlayerRadius;
        int crossed = 0;
        foreach (var cube in all)
        {
            foreach (var (edge, gate, _) in cube.Gates)
            {
                if (!cube.Cube.Holds(edge.Low)) continue;
                // A straight line through its throat that she fits along with room to spare.
                var probe = new CubeField(terrain, edge.Low);
                float? clear = null;
                for (float off = 0f; off <= 3f && clear is null; off += 0.25f)
                foreach (float o in new[] { off, -off })
                {
                    float along = gate.Along + o;
                    bool fits = true;
                    for (float t = -10f; t <= 10f && fits; t += 0.25f)
                        fits = ReefCollision.Clear(probe, edge.Point(along, t), r + 0.3f);
                    if (fits)
                    {
                        clear = along;
                        break;
                    }
                }
                if (clear is not { } line) continue;

                var start = edge.Point(line, -10f);
                var way = edge.Point(line, 1f) - edge.Point(line, 0f);
                var world = new CubeWorld(terrain, start, 0, tuning);
                var speeds = new List<float>();
                var steps = new List<float>();
                bool hit = false;
                for (int k = 0; k < 300 && edge.Across(world.Player.Position) < 9f; k++)
                {
                    var before = world.Player.Position;
                    world.Step(new PlaneInput { Move = way });
                    float across = edge.Across(world.Player.Position);
                    hit |= world.HitWall;
                    if (across > -2f && across < 6f)
                    {
                        speeds.Add(world.Player.Velocity.Length());
                        steps.Add(Vector2.Distance(before, world.Player.Position));
                    }
                }
                Assert.False(hit, $"{edge}: she met rock");
                Assert.True(edge.Across(world.Player.Position) >= 9f, $"{edge}: she did not get through");
                Assert.Equal(CubeId.Of(edge.High), world.Cube);
                Assert.NotEmpty(speeds);
                foreach (var s in speeds) Assert.InRange(s, tuning.PlaneCruiseSpeed * 0.999f, tuning.PlaneCruiseSpeed * 1.001f);
                foreach (var d in steps) Assert.InRange(d, steps[0] * 0.999f, steps[0] * 1.001f);
                Assert.True(world.Field.Recentres >= 1);
                crossed++;
            }
        }
        Assert.True(crossed >= 2, $"only {crossed} gates had a straight way through");
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_long_swim_across_the_layer_is_the_same_whatever_window_holds_the_cubes(ulong seed)
    {
        var (terrain, all) = Layer(seed);
        var start = all[0].Gates[0].Mouth;
        var goal = all.OrderByDescending(t => Vector2.Distance(t.Cube.Middle, all[0].Cube.Middle)).First().Gates[0].Mouth;
        var tuning = new Tuning();
        var path = Path(new CubeField(terrain, new CellCoord(F / 2, F / 2, 0), reach: F), start, goal, tuning.PlayerRadius + 0.25f);
        Assert.NotNull(path);

        // The window round her (moving as she goes), and one holding every cube from the start.
        var near = new CubeWorld(terrain, start, 0, tuning);
        var whole = new CubeWorld(terrain, start, 0, tuning, reach: F);
        var cubes = new HashSet<CubeId>();
        int at = 0, ticks = 0;
        var rng = new Rng(seed);
        bool sprint = false;
        while (Vector2.Distance(near.Player.Position, goal) > 1.5f && ticks < 60 * 120)
        {
            ticks++;
            while (at + 1 < path!.Count && Vector2.Distance(near.Player.Position, path[at]) < 2.5f) at++;
            if (ticks % 90 == 0) sprint = rng.NextFloat() < 0.4f;
            var wish = Vector2.Normalize(path[at] - near.Player.Position + new Vector2(1e-4f, 0f));
            var input = new PlaneInput { Move = wish, Dash = sprint && ticks % 90 == 1, DashHeld = sprint };
            near.Step(input);
            whole.Step(input);
            Assert.Equal(whole.Player.Position, near.Player.Position);
            Assert.Equal(whole.Player.Velocity, near.Player.Velocity);
            Assert.True(near.Clear(near.Player.Position, near.Radius - 0.01f), $"in the rock at {near.Player.Position}");
            cubes.Add(near.Cube);
        }
        _out.WriteLine($"{ticks} ticks, {cubes.Count} cubes, {near.Field.Recentres} window moves");
        Assert.True(Vector2.Distance(near.Player.Position, goal) <= 1.5f, $"stuck at {near.Player.Position}");
        Assert.True(cubes.Count >= 3);
        Assert.True(near.Field.Recentres >= 2);
    }

    [Fact]
    public void Outside_the_window_and_the_reef_is_rock()
    {
        var (terrain, all) = Layer(7UL);
        var first = all[0];
        var field = new CubeField(terrain, first.Cube.First);
        Assert.False(field.IsOpen(new Vector2(-5f, 30f)));
        Assert.False(field.IsOpen(new Vector2(30f, -5f)));
        // (A cube whose squares all lie beyond the window round the first: rock, though it is made.)
        var far = all.First(t => Math.Abs(t.Cube.First.X - first.Cube.First.X) > 2 && Math.Abs(t.Cube.Second.X - first.Cube.First.X) > 2
            || Math.Abs(t.Cube.First.Y - first.Cube.First.Y) > 2 && Math.Abs(t.Cube.Second.Y - first.Cube.First.Y) > 2);
        var mouth = far.Gates[0].Mouth;
        Assert.True(new CubeField(terrain, CellCoord.Of(mouth, 0)).IsOpen(mouth));
        Assert.False(field.IsOpen(mouth));
    }

    [Fact]
    public void The_reef_world_swims_and_shines_against_the_cubes_rock_when_given_their_field()
    {
        var (terrain, all) = Layer(7UL);
        var cube = all[all.Count / 2];
        var mouth = cube.Gates[0].Mouth;
        var world = CubeShell.Make(F, mouth, new Tuning());
        var field = new CubeField(terrain, CellCoord.Of(mouth, 0));
        world.Field = field;
        // Straight at the rock nearest her: she stops at it, as the slim world does.
        var slim = new CubeWorld(terrain, mouth, 0, world.Tuning);
        var way = new Vector2(0.6f, 0.8f);
        for (int k = 0; k < 600; k++)
        {
            var input = new PlaneInput { Move = way, Aim = way };
            field.Recenter(CellCoord.Of(world.Player.Position, 0));
            world.Step(input);
            slim.Step(input);
            Assert.True(ReefCollision.Clear(field, world.Player.Position, world.Radius - 0.01f));
        }
        Assert.Equal(slim.Player.Position, world.Player.Position);
        Assert.False(world.Defeated);
        // Her light stops at the cubes' rock.
        Assert.True(world.StopsLight(cube.Cube.Origin + new Vector2(1f, 1f)));
        Assert.False(world.StopsLight(mouth));
    }

    /// <summary>The shortest way over the water a body of <paramref name="clearance"/> fits (8-way steps, 1 m grid).</summary>
    static List<Vector2>? Path(IReefField field, Vector2 from, Vector2 to, float clearance)
    {
        int n = F * Side + 1;
        bool Free(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && ReefCollision.Clear(field, new Vector2(x, y), clearance);
        var a = (X: (int)MathF.Round(from.X), Y: (int)MathF.Round(from.Y));
        var b = (X: (int)MathF.Round(to.X), Y: (int)MathF.Round(to.Y));
        var came = new Dictionary<(int, int), (int, int)> { [a] = a };
        var todo = new Queue<(int X, int Y)>();
        todo.Enqueue(a);
        while (todo.Count > 0)
        {
            var c = todo.Dequeue();
            if (c == b) break;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var next = (X: c.X + dx, Y: c.Y + dy);
                if ((dx == 0 && dy == 0) || came.ContainsKey(next) || !Free(next.X, next.Y)) continue;
                if (dx != 0 && dy != 0 && (!Free(c.X + dx, c.Y) || !Free(c.X, c.Y + dy))) continue;
                came[next] = c;
                todo.Enqueue(next);
            }
        }
        if (!came.ContainsKey(b)) return null;
        var path = new List<Vector2>();
        for (var c = b; c != a; c = came[c]) path.Add(new Vector2(c.Item1, c.Item2));
        path.Reverse();
        return path;
    }
}
