using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// The crossover's shelving (docs/proposals/CROSSOVER.md §11, Run.Crossover): its levels have no havens and no leak
/// caves or curse dens; everything else is planned exactly as the dive plans it.
/// </summary>
public class CrossoverTests
{
    static readonly LevelId[] Ids = Enumerable.Range(1, LevelPlan.Depths - 1)
        .SelectMany(depth => Enumerable.Range(1, 5).Select(level => new LevelId(1, depth, level)))
        .ToArray();

    [Fact]
    public void A_crossover_plan_has_no_haven_and_no_leak_cave()
    {
        foreach (var id in Ids)
        {
            var plan = LevelPlan.For(TestLevels.Seed, id, crossover: true);
            Assert.Equal(0, plan.Havens);
            Assert.Equal(0, plan.Curses);
        }
    }

    [Fact]
    public void The_tutorials_plans_have_none_either()
    {
        for (int level = 1; level <= 2; level++)
        {
            var plan = LevelPlan.For(TestLevels.Seed, new LevelId(1, LevelPlan.Tutorial, level), crossover: true);
            Assert.Equal(0, plan.Havens);
            Assert.Equal(0, plan.Curses);
        }
    }

    [Fact]
    public void The_dive_still_plans_havens_and_leak_caves()
    {
        var plans = Ids.Select(id => LevelPlan.For(TestLevels.Seed, id)).ToList();
        Assert.Contains(plans, p => p.Havens > 0);
        Assert.Contains(plans, p => p.Curses > 0);
    }

    [Fact]
    public void Everything_else_is_planned_as_the_dive_plans_it()
    {
        foreach (var id in Ids)
        {
            var dive = LevelPlan.For(TestLevels.Seed, id);
            var cross = LevelPlan.For(TestLevels.Seed, id, crossover: true);
            Assert.Equal(dive.Id, cross.Id);
            Assert.Equal(dive.HasBoss, cross.HasBoss);
            Assert.Equal(dive.Treasures, cross.Treasures);
            Assert.Equal(dive.Secrets, cross.Secrets);
            Assert.Equal(dive.Crabs, cross.Crabs);
            Assert.Equal(dive.Shops, cross.Shops);
            Assert.Equal(dive.Caches, cross.Caches);
            Assert.Equal(dive.Exits, cross.Exits);
            Assert.Equal(dive.Menace, cross.Menace);
        }
    }

    [Fact]
    public void A_crossover_level_is_made_without_a_haven_or_leak_cave_mouth()
    {
        // (A level whose dive plan holds both, so the crossover really takes them away.)
        var id = Ids.FirstOrDefault(i => LevelPlan.For(TestLevels.Seed, i) is { Havens: > 0 } or { Curses: > 0 });
        Assert.NotEqual(default, id);
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        Assert.DoesNotContain(map.Pois, p => p.Kind is PoiKind.Haven or PoiKind.LeakCave);
        Assert.DoesNotContain(map.MouthKinds, k => k is PoiKind.Haven or PoiKind.LeakCave);
    }
}
