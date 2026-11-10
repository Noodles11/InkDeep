using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Secret rooms as labyrinths (docs/DIVE.md §6.1): the maze, its level, and the sealed mouth on the reef.</summary>
public class LabyrinthTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    [Fact]
    public void Every_maze_reaches_every_cell_its_heart_a_far_dead_end()
    {
        for (ulong s = 1; s <= 400; s++)
        {
            var rng = new Rng(s * 0x9E3779B97F4A7C15UL);
            int cols = 3 + rng.Int(12), rows = 3 + rng.Int(12);
            var entrance = (rng.Int(cols), rng.Int(rows));
            var m = Maze.Build(cols, rows, entrance, rng);
            var steps = m.Steps(entrance.Item1, entrance.Item2);
            Assert.DoesNotContain(-1, steps);
            // A spanning tree and only a few loops on top.
            int joins = m.Joins().Count();
            Assert.InRange(joins, cols * rows - 1, cols * rows - 1 + cols * rows / 4);
            Assert.NotEqual(m.Entrance, m.Heart);
            int far = steps[m.Heart.Y * cols + m.Heart.X];
            if (m.Degree(m.Heart.X, m.Heart.Y) == 1)
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    if ((x, y) != m.Entrance && m.Degree(x, y) == 1) Assert.True(steps[y * cols + x] <= far);
        }
    }

    [Fact]
    public void A_maze_is_the_same_from_the_same_dice()
    {
        var a = Maze.Build(11, 11, (5, 0), new Rng(42));
        var b = Maze.Build(11, 11, (5, 0), new Rng(42));
        Assert.Equal(a.Joins(), b.Joins());
        Assert.Equal(a.Heart, b.Heart);
    }

    static LevelMap Labyrinth(int level, int depth = 2, Vector2? into = null) =>
        TopDownGenerator.GenerateLabyrinth(TestLevels.Seed, new LevelId(601, depth, level), depth, into);

    [Fact]
    public void A_labyrinth_is_always_made_its_heart_reached_from_its_way_in()
    {
        var ways = new[] { new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0.3f) };
        for (int i = 0; i < 24; i++)
        {
            var into = ways[i % ways.Length];
            var map = Labyrinth(10 + i, 1 + i % 6, into);
            Assert.True(map.Labyrinth);
            Assert.NotNull(map.Way);
            var way = map.Way!.Value;
            // Its way out goes the way she swam in.
            Assert.True(Vector2.Dot(-way.Into, Vector2.Normalize(into)) > 0.5f, $"way {way.Into} for {into}");
            var heart = map.Pois.Single(p => p.Kind == PoiKind.Secret).Position;
            var path = LevelValidator.ShortestPath(map, way.Inside, heart, 0.6f);
            Assert.NotEmpty(path);
            // A maze, not a room: the way to the heart is long.
            Assert.True(path.Count > 60, $"only {path.Count} m to its heart");
            Assert.Empty(map.Mouths);
        }
    }

    [Fact]
    public void A_labyrinth_holds_its_reward_and_nothing_else()
    {
        var map = Labyrinth(3);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        var heart = map.Pois.Single(p => p.Kind == PoiKind.Secret).Position;
        Assert.True(w.Shells.Count >= PlaneEconomyTuning.SecretMin || w.Pearls.Count == 1);
        Assert.All(w.Shells, s => Assert.True(Vector2.Distance(s.Position, heart) < LabyrinthTuning.HeartRadius));
        Assert.Equal(0f, Vector2.Distance(w.Player.Position, map.Way!.Value.Inside), 3);
        for (int i = 0; i < 30 * PlaneWorld.TickRate; i++) w.Step(default);
        Assert.Empty(w.Murklings);
        Assert.Empty(w.Leaks);
        Assert.Empty(w.Fish);
        Assert.Empty(w.ReefFish);
        Assert.Empty(w.Inkwardens);
        Assert.Empty(w.Lashjaws);
        Assert.Empty(w.Currents);
        Assert.Null(w.Boss);
        Assert.False(w.Defeated);
        Assert.Equal(w.Run.MaxHp, w.Player.Hp);
    }

    [Fact]
    public void A_secret_room_on_the_reef_is_a_mouth_sealed_by_an_X_wall_and_holds_nothing_itself()
    {
        int found = 0;
        foreach (var id in TestLevels.All(p => p.Secrets > 0).Take(4))
        {
            var map = TestLevels.Get(id);
            int k = map.MouthKinds.IndexOf(PoiKind.Secret);
            Assert.True(k >= 0, $"{id} has no secret mouth");
            Assert.Equal(map.Mouths.Count, map.MouthKinds.Count);
            Assert.False(map.LeakMouth(k));
            var door = map.Mouths[k];
            var seal = map.WeakRocks.Single(r => r.Kind == SealKind.Secret && !r.Fake);
            // In front of the threshold, across it.
            Assert.True(seal.Contains(door.At - door.Into * 0.5f));
            Assert.True(seal.Contains(door.At - door.Into * 0.5f + door.Across * Doorway.HalfWidth));
            var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
            var poi = map.Pois.Single(p => p.Kind == PoiKind.Secret).Position;
            Assert.DoesNotContain(w.Shells, s => Vector2.Distance(s.Position, poi) < 5f);
            // Shut: she cannot swim in; its wall down, the mouth takes her.
            var sealView = w.Seals.Single(x => x.Kind == SealKind.Secret && !x.Rock.Fake);
            Assert.False(w.Clear(door.At - door.Into * 0.4f, w.Radius));
            w.DebugOpenSeal(sealView);
            Assert.True(w.Clear(door.At - door.Into * 0.4f, w.Radius));
            w.Player.Position = w.Player.PrevPosition = door.At + door.Into * (ReefTuning.MouthIn + 0.5f);
            Assert.Equal(k, w.AtMouth());
            found++;
        }
        Assert.True(found > 0);
    }
}
