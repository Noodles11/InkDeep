using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Tests;

/// <summary>A layer of the reef as the game's map (CUBE-WORLD-PLAN §15.1): the cubes' own rock, read as a level's.</summary>
public class CubeReefTests
{
    [Theory]
    [MemberData(nameof(CubeTerrainTests.Seeds), MemberType = typeof(CubeTerrainTests))]
    public void A_layers_map_is_its_cubes_rock(ulong seed)
    {
        var reef = new CubeReef(seed, 5, 3);
        for (int z = 0; z < 2; z++)
        {
            var map = reef.Layer(z, reef.Start);
            Assert.Equal(5 * Side, map.Size);
            var field = new CubeField(reef.Made, CellCoord.Of(reef.Start, z), 5);
            var rng = new System.Random((int)seed + z);
            int inside = 0;
            for (int i = 0; i < 4000; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * map.Size, (float)rng.NextDouble() * map.Size);
                if (!reef.Space.Inside(CellCoord.Of(p, z)) || reef.Made(CubeId.Of(p, z)) is null) continue;
                field.Recenter(CellCoord.Of(p, z));
                inside++;
                Assert.Equal(field.HeightAt(p), map.HeightAt(p), 3);
                Assert.Equal(field.IsOpen(p), map.IsOpen(p));
            }
            Assert.True(inside > 2000, $"{inside} points inside the reef");
        }
    }

    [Theory]
    [MemberData(nameof(CubeTerrainTests.Seeds), MemberType = typeof(CubeTerrainTests))]
    public void She_starts_in_open_water_on_a_world_of_the_layer(ulong seed)
    {
        var reef = new CubeReef(seed, 5, 3);
        var map = reef.Layer(0, reef.Start);
        var world = new PlaneWorld(map, new Tuning(), options: new PlaneOptions { Corruption = false, InkMurklings = false });
        Assert.Equal(reef.Start, world.Player.Position);
        Assert.True(world.Clear(world.Player.Position, world.Radius));
        // A layer's rim squares outside the reef are rock.
        var outside = Enumerable.Range(0, 5).SelectMany(y => Enumerable.Range(0, 5).Select(x => new CellCoord(x, y, 0))).Where(c => !reef.Space.Inside(c)).ToList();
        foreach (var c in outside) Assert.False(map.IsOpen(c.Origin + new Vector2(Side / 2f)));
    }
}

/// <summary>A layer's cubes stocked as she claims them (CUBE-WORLD-PLAN §15.2), and put back as she left them.</summary>
public class CubeStockTests
{
    static PlaneWorld LayerWorld(CubeReef reef, int z) =>
        new(reef.Layer(z, reef.Start), new Tuning(), options: new PlaneOptions { Corruption = false, InkMurklings = false, Reef = true });

    [Theory]
    [MemberData(nameof(CubeTerrainTests.Seeds), MemberType = typeof(CubeTerrainTests))]
    public void A_claimed_cube_is_stocked_alike_each_time_and_never_on_the_warm_up_layer(ulong seed)
    {
        var reef = new CubeReef(seed, 5, 4);
        var world = LayerWorld(reef, 2);
        foreach (var cube in reef.Space.CubesOf(2))
        {
            var entry = cube.Origin + new Vector2(2f, 2f);
            var a = CubeStock.Spots(seed, cube, 3, world.Map.Menace, entry, world.Clear);
            var b = CubeStock.Spots(seed, cube, 3, world.Map.Menace, entry, world.Clear);
            Assert.Equal(a, b);
            Assert.True(a.Count >= CubeStockTuning.Least, $"{cube}: {a.Count} stocked");
            Assert.All(a, s => Assert.True(Vector2.Distance(s.At, entry) >= CubeStockTuning.FromEntry));
            Assert.True(a.Count(s => s.Prime) <= 1);
            Assert.Empty(CubeStock.Spots(seed, cube.OnLayer(0), 3, 0f, entry, (_, _) => true));
        }
    }

    [Theory]
    [MemberData(nameof(CubeTerrainTests.Seeds), MemberType = typeof(CubeTerrainTests))]
    public void Its_stock_lives_wakes_near_her_and_is_put_back_as_left(ulong seed)
    {
        var reef = new CubeReef(seed, 5, 4);
        var world = LayerWorld(reef, 2);
        Assert.NotNull(world.Corruption);
        var cubes = reef.Space.CubesOf(2).ToList();
        foreach (var cube in cubes)
            foreach (var s in CubeStock.Spots(seed, cube, 1, world.Map.Menace, reef.Start, world.Clear))
                world.PlaceStock(cube, s.Spawn, s.Seed, s.Prime, new[] { s.At }, left: false);
        int made = world.Murklings.Count;
        Assert.True(made >= cubes.Count * CubeStockTuning.Least);
        var before = world.Murklings.ToDictionary(m => m, m => m.Position);
        for (int i = 0; i < 240; i++) world.Step(new PlaneInput());
        // Asleep far from her: not moved; near her: some have.
        var her = world.Player.Position;
        foreach (var m in world.Murklings.Where(m => Vector2.Distance(before[m], her) > CubeStockTuning.SleepReach + 10f))
            Assert.Equal(before[m], m.Position);
        // What is left of each cube's stock, put back on a fresh world of the layer, is the same murklings where they were.
        var again = LayerWorld(reef, 2);
        foreach (var cube in cubes)
            foreach (var left in world.StockLeft(cube))
                again.PlaceStock(cube, left.Spawn, left.Seed, left.Prime, left.At, left: true, shoal: left.Shoal);
        Assert.Equal(world.Murklings.Count(m => m.Alive && m.Stocked is not null), again.Murklings.Count);
        var was = world.Murklings.Where(m => m.Alive && m.Stocked is not null).Select(m => (m.Position, m.Kind, m.Prime)).OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y);
        var now = again.Murklings.Select(m => (m.Position, m.Kind, m.Prime)).OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y);
        Assert.Equal(was, now);
    }
}

/// <summary>A cube's sockets (CUBE-WORLD-PLAN §9): alcoves cut into its walls, where a granted place opens its mouth.</summary>
public class CubeSocketTests
{
    readonly Xunit.Abstractions.ITestOutputHelper _out;
    public CubeSocketTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

    [Theory]
    [MemberData(nameof(CubeTerrainTests.Seeds), MemberType = typeof(CubeTerrainTests))]
    public void Most_cubes_have_a_socket_whose_passage_she_can_swim_into(ulong seed)
    {
        var reef = new CubeReef(seed, 5, 2);
        var map = reef.Layer(1, reef.Start);
        int cubes = 0, with = 0, sockets = 0;
        foreach (var cube in reef.Space.CubesOf(1))
        {
            var t = reef.Terrain(cube);
            cubes++;
            if (t.Sockets.Count > 0) with++;
            foreach (var door in t.Sockets)
            {
                sockets++;
                Assert.Equal(cube, CubeId.Of(door.At, 1));
                Assert.All(t.Gates, g => Assert.True(Vector2.Distance(g.Mouth, door.At) >= CubeTerrain.SocketFromGate));
                // Its passage open on the layer's map, its rock roofed over.
                Assert.True(map.IsOpen(door.Inside), $"{cube}: socket at {door.At} closed inside");
                Assert.True(map.IsOpen(door.At - door.Into * 1.5f), $"{cube}: socket at {door.At} closed before it");
                Assert.False(float.IsNaN(map.RoofAt((int)System.MathF.Round(door.Inside.X), (int)System.MathF.Round(door.Inside.Y))));
            }
        }
        _out.WriteLine($"{with} of {cubes} cubes with sockets, {sockets} in all");
        Assert.True(with * 3 >= cubes * 2, $"only {with} of {cubes} cubes have a socket");
    }
}
