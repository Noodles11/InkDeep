using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>One rule for each kind of place in a reef (docs/LIGHT-AND-GRAPH-PROPOSAL.md §7; PlaceRules).</summary>
public class PlaceRulesTests
{
    [Fact]
    public void EveryKindOfPlaceHasOneRule_TheLeakCaveAndTheSecretTheWaysIntoSubLevels()
    {
        Assert.Equal(PlaceRules.All.Count, PlaceRules.All.Select(r => r.Kind).Distinct().Count());
        Assert.All(PlaceRules.All, r => Assert.Equal(r, PlaceRules.Of(r.Kind)));
        Assert.Equal(new[] { PoiKind.Secret, PoiKind.LeakCave }, PlaceRules.All.Where(r => r.Sort == PlaceSort.SubLevel).Select(r => r.Kind).OrderBy(k => k));
        // What is not rolled says how it comes.
        Assert.All(PlaceRules.All.Where(r => r.Chance == 0f), r => Assert.NotEmpty(r.Comes));
    }

    [Fact]
    public void PlacesKeepApart_CavesAndHolesFurther()
    {
        Assert.Equal(PlaceRules.Apart, PlaceRules.Between(PoiKind.Secret, PoiKind.ShellCache));
        Assert.Equal(PlaceRules.CaveApart, PlaceRules.Between(PoiKind.Shop, PoiKind.TreasureCave));
        Assert.Equal(PlaceRules.HoleApart, PlaceRules.Between(PoiKind.Hole, PoiKind.Hole));
        Assert.Equal(PlaceRules.Apart, PlaceRules.Between(PoiKind.Hole, PoiKind.Shop));
    }

    [Theory]
    [InlineData("KELP7Q2Z")]
    [InlineData("ABCD2345")]
    public void AReefsFirstRowHasNoShopNorShellCache_AndEveryPlanKeepsItsRules(string seed)
    {
        var code = SeedCode.Parse(seed);
        var g = DiveGraph.Of(code);
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        for (int row = 1; row <= DiveGraph.RowsOf(depth); row++)
            foreach (var n in g.Row(1, depth, row).Where(n => n.Kind != ReefKind.Haven))
            {
                var plan = LevelPlan.For(code, n.Id);
                if (row < PlaceRules.Shop.FromRow) Assert.Equal(0, plan.Shops);
                if (row < PlaceRules.ShellCache.FromRow) Assert.Equal(0, plan.Caches);
                if (row < PlaceRules.LeakCave.FromRow) Assert.Equal(0, plan.Curses);
            }
    }
}
