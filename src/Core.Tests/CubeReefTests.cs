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
        var world = new PlaneWorld(map, new Tuning(), options: new PlaneOptions { Corruption = false });
        Assert.Equal(reef.Start, world.Player.Position);
        Assert.True(world.Clear(world.Player.Position, world.Radius));
        // A layer's rim squares outside the reef are rock.
        var outside = Enumerable.Range(0, 5).SelectMany(y => Enumerable.Range(0, 5).Select(x => new CellCoord(x, y, 0))).Where(c => !reef.Space.Inside(c)).ToList();
        foreach (var c in outside) Assert.False(map.IsOpen(c.Origin + new Vector2(Side / 2f)));
    }
}
