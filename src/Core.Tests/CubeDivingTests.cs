using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.Cubes;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Diving and climbing between layers (CUBE-WORLD-PLAN §7).</summary>
public class CubeDivingTests
{
    const float R = 0.35f;

    static (CubeSpace Space, System.Func<CubeId, CubeTerrain?> Terrain) Reef()
    {
        var space = new CubeSpace(CubeTerrainTests.SeedValues[0], 5, 3);
        var made = Enumerable.Range(0, 3).SelectMany(space.CubesOf).ToDictionary(c => c, c => CubeTerrain.Make(space, c));
        return (space, c => made.GetValueOrDefault(c));
    }

    [Fact]
    public void She_dives_through_a_well_not_through_a_shelf_nor_onto_rock()
    {
        var (space, terrain) = Reef();
        var diving = new CubeDiving(3);
        var cube = space.CubesOf(0).First();
        var well = terrain(cube)!.Down[0];
        Assert.Equal(LayerBlock.None, diving.CanDive(well.Center, R, terrain));
        // Over rock and over a shelf: no way.
        Assert.Equal(LayerBlock.NoWay, diving.CanDive(cube.Corners()[0].Point + new Vector2(1f, 1f), R, terrain));
        var t = terrain(cube)!;
        var shelf = Enumerable.Range(0, t.Stride * t.Rows).Select(k => new Vector2(t.X0 + k % t.Stride, t.Y0 + k / t.Stride))
            .First(p => t.CellAt((int)p.X, (int)p.Y) == CubeCell.Shelf && t.CellAt((int)p.X + 1, (int)p.Y) == CubeCell.Shelf && t.CellAt((int)p.X, (int)p.Y + 1) == CubeCell.Shelf
                && t.CellAt((int)p.X - 1, (int)p.Y) == CubeCell.Shelf && t.CellAt((int)p.X, (int)p.Y - 1) == CubeCell.Shelf);
        Assert.Equal(LayerBlock.NoWay, diving.CanDive(shelf, R, terrain));
    }

    [Fact]
    public void After_a_change_of_layer_she_waits_out_the_cooldown_and_dives_no_nearer_than_half_a_square()
    {
        var (space, terrain) = Reef();
        var diving = new CubeDiving(3);
        var first = space.CubesOf(0).First();
        var at = terrain(first)!.Down[0].Center;
        var landed = terrain(CubeId.Of(at, 1))!;
        diving.Dived(at, landed);
        Assert.Equal(1, diving.Layer);
        Assert.Equal(CubeDiving.Cooldown, diving.CooldownLeft);
        // A way on down near where she arrived (if this square has one), and one far enough off.
        var ways = space.CubesOf(1).Select(c => terrain(c)!).SelectMany(c => c.Down).ToList();
        var far = ways.First(w => Vector2.Distance(w.Center, at) >= CubeDiving.ChainReach);
        Assert.Equal(LayerBlock.Cooldown, diving.CanDive(far.Center, R, terrain));
        diving.Tick(CubeDiving.Cooldown - 0.1f);
        Assert.Equal(LayerBlock.Cooldown, diving.CanDive(far.Center, R, terrain));
        diving.Tick(0.2f);
        Assert.Equal(LayerBlock.None, diving.CanDive(far.Center, R, terrain));
        foreach (var near in ways.Where(w => Vector2.Distance(w.Center, at) < diving.ChainLimit))
            Assert.Equal(LayerBlock.TooNear, diving.CanDive(near.Center, R, terrain));
        Assert.InRange(diving.ChainLimit, 1f, CubeDiving.ChainReach);
        // A cube's own ways down always lie beyond the limit for at least one of them.
        Assert.Contains(landed.Down, w => Vector2.Distance(w.Center, at) >= diving.ChainLimit);
    }

    [Fact]
    public void She_climbs_back_one_layer_only_from_the_deepest_she_has_reached()
    {
        var (space, terrain) = Reef();
        var diving = new CubeDiving(3, cooldown: 0f);
        Assert.Equal(LayerBlock.Edge, diving.CanClimb(Vector2.Zero, R, terrain));
        var at = terrain(space.CubesOf(0).First())!.Down[0].Center;
        diving.Dived(at, terrain(CubeId.Of(at, 1))!);
        // Where she came down, the water above is void: she may go back up there.
        Assert.Equal(LayerBlock.None, diving.CanClimb(at, R, terrain));
        diving.Climbed(at, terrain(CubeId.Of(at, 0))!);
        Assert.Equal(0, diving.Layer);
        Assert.Equal(1, diving.Deepest);
        Assert.False(diving.Climbable);
        // Down again and on down: from layer 2 she may climb to 1, and from 1 no further.
        diving.Dived(at, terrain(CubeId.Of(at, 1))!);
        var on = space.CubesOf(1).Select(c => terrain(c)!).SelectMany(c => c.Down).First(w => Vector2.Distance(w.Center, at) >= diving.ChainLimit).Center;
        Assert.Equal(LayerBlock.None, diving.CanDive(on, R, terrain));
        diving.Dived(on, terrain(CubeId.Of(on, 2))!);
        Assert.Equal(LayerBlock.Edge, diving.CanDive(on, R, terrain));
        Assert.Equal(LayerBlock.None, diving.CanClimb(on, R, terrain));
        diving.Climbed(on, terrain(CubeId.Of(on, 1))!);
        Assert.Equal(LayerBlock.NotDeepest, diving.CanClimb(on, R, terrain));
    }

    [Fact]
    public void Where_the_cubes_are_not_in_hand_she_waits()
    {
        var (space, terrain) = Reef();
        var diving = new CubeDiving(3);
        var at = terrain(space.CubesOf(0).First())!.Down[0].Center;
        Assert.Equal(LayerBlock.NotReady, diving.CanDive(at, R, c => c.Z == 0 ? terrain(c) : null));
    }
}
