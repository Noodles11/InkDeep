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

/// <summary>Havens (docs/DIVE.md §4): a reef's way into a small safe sub-level, with its shop.</summary>
public class HavenTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static LevelMap Haven(int level, int depth = 2, Vector2? into = null) =>
        TopDownGenerator.GenerateHaven(TestLevels.Seed, new LevelId(701, depth, level), into);

    [Fact]
    public void A_haven_is_small_all_safe_water_with_its_shop_fan_mirror_and_homes_and_a_way_in()
    {
        for (int k = 0; k < 6; k++)
        {
            var map = Haven(k + 1, 1 + k % 5, k % 2 == 0 ? Vector2.UnitX : -Vector2.UnitY);
            Assert.Equal(HavenLayout.Size, map.Size);
            Assert.NotNull(map.Haven);
            Assert.True(map.InHaven(map.Start.Position));
            Assert.NotNull(map.Way);
            Assert.Single(map.Pois, p => p.Kind == PoiKind.Shop);
            Assert.NotNull(map.Fan);
            Assert.NotNull(map.Mirror);
            Assert.True(map.IsOpen(map.Fan!.Value) && map.IsOpen(map.Mirror!.Value));
            var reach = LevelValidator.Distances(map, map.Way!.Value.Inside);
            Assert.False(float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, map.Pois.Single(p => p.Kind == PoiKind.Shop).Position)));
            Assert.False(float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, map.Fan.Value)));
            // Nothing lives in it, nothing leaks: it is bright and safe.
            var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, Shots = false });
            Assert.Empty(w.Leaks);
            Assert.Empty(w.Murklings);
            Assert.True(w.InHaven);
        }
    }

    [Fact]
    public void About_a_third_of_the_reefs_have_a_haven_and_none_a_shop_of_its_own_and_the_graph_has_no_haven_reefs()
    {
        int reefs = 0, havens = 0;
        foreach (var id in TestLevels.All(p => !p.HasBoss && !p.Id.IsTutorial))
        {
            var plan = LevelPlan.For(TestLevels.Seed, id);
            reefs++;
            havens += plan.Havens;
            Assert.Equal(0, plan.Shops);
            var map = TestLevels.Get(id);
            Assert.Equal(plan.Havens, map.MouthKinds.Count(k => k == PoiKind.Haven));
            Assert.DoesNotContain(map.Pois, p => p.Kind == PoiKind.Shop);
        }
        Assert.InRange(havens / (float)reefs, 0.2f, 0.6f);
        var graph = DiveGraph.Of(TestLevels.Seed);
        for (int d = 2; d <= 6; d++)
        {
            Assert.DoesNotContain(graph.Row(1, d, 2), n => n.Kind == ReefKind.Haven);
            Assert.Equal(1, LevelPlan.For(TestLevels.Seed, graph.Row(1, d, 2)[0].Id).Havens);
        }
    }
}
