using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The dive as a graph of reefs (docs/LIGHT-AND-GRAPH.md §6; Run.DiveGraph) and its reefs' holes.</summary>
public class DiveGraphTests
{
    public static IEnumerable<object[]> Seeds => new[] { "KELP7Q2Z", "ABCD2345", "HAVENS23", "QQQQ2222", "REEF5TAR", "ZZZZ9999" }.Select(s => new object[] { s });

    /// <summary>Every reef of the first two loops, depth by depth (with the tutorial's).</summary>
    static IEnumerable<DiveNode> Reefs(DiveGraph g, int cycles = 2)
    {
        for (int row = 1; row <= DiveGraph.RowsOf(LevelPlan.Tutorial); row++)
            foreach (var n in g.Row(1, LevelPlan.Tutorial, row)) yield return n;
        for (int cycle = 1; cycle <= cycles; cycle++)
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        for (int row = 1; row <= DiveGraph.RowsOf(depth); row++)
            foreach (var n in g.Row(cycle, depth, row)) yield return n;
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EachDepthIsFourRowsOfTwoToFourReefsThenItsBossAlone(string seed)
    {
        var g = DiveGraph.Of(SeedCode.Parse(seed));
        Assert.Single(g.Row(1, LevelPlan.Tutorial, 1));
        Assert.Single(g.Row(1, LevelPlan.Tutorial, 2));
        // Where every dive begins: one reef.
        Assert.Single(g.Row(1, LevelPlan.Tutorial + 1, 1));
        for (int cycle = 1; cycle <= 2; cycle++)
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        {
            for (int row = cycle == 1 && depth == LevelPlan.Tutorial + 1 ? 2 : 1; row < DiveGraph.RowsOf(depth); row++)
                Assert.InRange(g.Row(cycle, depth, row).Count, DiveTuning.MinLanes, DiveTuning.MaxLanes);
            var boss = Assert.Single(g.Row(cycle, depth, DiveGraph.RowsOf(depth)));
            Assert.Equal(ReefKind.Boss, boss.Kind);
            Assert.True(LevelPlan.For(SeedCode.Parse(seed), boss.Id).HasBoss);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryReefIsReachedAndLeadsOnToItsBoss_ItsHolesEachToAnotherReef(string seed)
    {
        var g = DiveGraph.Of(SeedCode.Parse(seed));
        foreach (var n in Reefs(g))
        {
            if (n.Id != LevelId.First) Assert.NotEmpty(n.From);
            var exits = g.ExitsOf(n.Id);
            Assert.NotEmpty(exits);
            Assert.Equal(exits.Count, exits.Distinct().Count());
            int below = 0;
            foreach (var e in exits)
            {
                var to = g.Node(e);
                Assert.Equal(e, to.Id);
                Assert.Contains(n.Id, to.From);
                // Down a row, or past one (a shortcut, never past the boss), or into the next depth.
                if (to.Id.Depth == n.Id.Depth && to.Id.Cycle == n.Id.Cycle)
                {
                    Assert.InRange(to.Row - n.Row, 1, 2);
                    if (to.Row - n.Row == 2) Assert.NotEqual(ReefKind.Boss, to.Kind);
                }
                else Assert.Equal(1, to.Row);
                if (to.Row == n.Row + 1 || to.Row == 1) below++;
            }
            int most = DiveTuning.MaxExits;
            Assert.InRange(exits.Count, 1, most);
            // Two holes at least, wherever the row below is two wide.
            var next = g.Node(exits[0]);
            int width = next.Row == 1 ? g.Row(next.Id.Cycle, next.Id.Depth, 1).Count : g.Row(n.Id.Cycle, n.Id.Depth, n.Row + 1).Count;
            if (n.Kind != ReefKind.Haven) Assert.True(exits.Count >= Math.Min(DiveTuning.MinExits, width), $"{n.Id}: {exits.Count} holes over a row {width} wide");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void No_reef_of_the_graph_is_a_haven_and_each_depths_second_row_opens_one(string seed)
    {
        var code = SeedCode.Parse(seed);
        var g = DiveGraph.Of(code);
        for (int cycle = 1; cycle <= 2; cycle++)
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        {
            var rows = Enumerable.Range(1, DiveGraph.RowsOf(depth)).Select(r => g.Row(cycle, depth, r)).ToList();
            Assert.DoesNotContain(rows.SelectMany(r => r), n => n.Kind == ReefKind.Haven || n.Id.Haven);
            // (A haven is a reef's way into one of its own: LevelPlan.Havens; the depth's second row's first always has one.)
            Assert.Equal(1, LevelPlan.For(code, rows[1][0].Id).Havens);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void EveryDepthHasATreasureReef_AndAReefsPlanHoldsWhatItsKindSays(string seed)
    {
        var code = SeedCode.Parse(seed);
        var g = DiveGraph.Of(code);
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Depths; depth++)
        {
            var reefs = Enumerable.Range(1, 4).SelectMany(r => g.Row(1, depth, r)).ToList();
            Assert.Contains(reefs, n => n.Kind == ReefKind.Treasure);
            Assert.DoesNotContain(g.Row(1, depth, 1), n => n.Kind == ReefKind.Den);
            foreach (var n in reefs.Where(n => n.Kind != ReefKind.Haven))
            {
                var plan = LevelPlan.For(code, n.Id);
                Assert.Equal(n.Kind, plan.Kind);
                Assert.Equal(n.Kind == ReefKind.Treasure ? 1 : 0, plan.Treasures);
                // (A treasure reef may hold a curse den its template asks for too.)
                if (n.Kind != ReefKind.Treasure) Assert.Equal(n.Kind == ReefKind.Den ? 1 : 0, plan.Curses);
                Assert.Equal(g.ExitsOf(n.Id).Count, plan.Exits.Count);
                Assert.Equal(plan.Exits.Select(g.KindOf), plan.ExitKinds);
                Assert.True(plan.Places <= LevelStocking.MaxPlaces, $"{n.Id}: {plan.Places} places");
            }
        }
    }

    [Fact]
    public void MenaceComesFromTheDepthAlone()
    {
        var d1 = LevelPlan.Tutorial + 1;
        Assert.Equal(0f, LevelPlan.MenaceOf(new LevelId(1, d1, 1)));
        Assert.Equal(LevelPlan.MenaceOf(new LevelId(1, d1, 2)), LevelPlan.MenaceOf(new LevelId(1, d1, 5)));
        Assert.True(LevelPlan.MenaceOf(new LevelId(1, d1 + 1, 1)) > LevelPlan.MenaceOf(new LevelId(1, d1, 5)));
        Assert.Equal(1f, LevelPlan.MenaceOf(new LevelId(1, d1 + 1, 1)) * (LevelPlan.Depths - d1), 3);
        // Lanes share their row's.
        Assert.Equal(LevelPlan.MenaceOf(new LevelId(1, d1, 3)), LevelPlan.MenaceOf(new LevelId(1, d1, 3) { Lane = 2 }));
    }

    [Fact]
    public void TheGraphIsTheSameFromTheSameSeed()
    {
        var a = DiveGraph.Of(SeedCode.Parse("KELP7Q2Z"));
        var b = DiveGraph.Of(SeedCode.Parse("KELP7Q2Z"));
        Assert.Same(a, b);
        var other = DiveGraph.Of(SeedCode.Parse("ABCD2345"));
        string Shape(DiveGraph g) => string.Join("|", Reefs(g, 1).Select(n => $"{n.Id.Level}.{n.Id.Lane}:{n.Kind}>{string.Join(",", n.Exits.Select(e => $"{e.Level}.{e.Lane}"))}"));
        Assert.NotEqual(Shape(a), Shape(other));
    }

    [Theory]
    [InlineData("KELP7Q2Z")]
    [InlineData("ABCD2345")]
    public void AReefHasAHoleForEachExit_EachLeadingToItsReefLeftToRight(string seed)
    {
        var code = SeedCode.Parse(seed);
        var g = DiveGraph.Of(code);
        int checkedReefs = 0;
        for (int depth = LevelPlan.Tutorial + 1; depth <= LevelPlan.Tutorial + 2; depth++)
        for (int row = 1; row <= DiveGraph.RowsOf(depth); row++)
            foreach (var n in g.Row(1, depth, row))
            {
                if (n.Kind == ReefKind.Haven) continue;
                var map = TopDownGenerator.Generate(new RunStreams(code), n.Id);
                var exits = g.ExitsOf(n.Id);
                Assert.Equal(exits.Count, map.Holes.Count);
                Assert.Equal(exits.ToHashSet(), map.Holes.Select(h => h.To).ToHashSet());
                // Left to right across the map as across the rows below.
                for (int i = 1; i < map.Holes.Count; i++)
                {
                    Assert.True(map.Holes[i].Shaft.Center.X >= map.Holes[i - 1].Shaft.Center.X);
                    Assert.True(g.Node(map.Holes[i].To).X >= g.Node(map.Holes[i - 1].To).X);
                }
                foreach (var hole in map.Holes)
                {
                    Assert.Equal(g.KindOf(hole.To), hole.Kind);
                    Assert.True(map.IsOpen(hole.Shaft.Center), $"{n.Id}: a hole in the rock");
                    Assert.Equal(map.Holes.IndexOf(hole), map.HoleAt(hole.Shaft.Center));
                    Assert.True(Vector2Dist(hole.Shaft.Center, map.Start.Position) > 18f, $"{n.Id}: a hole by the start");
                }
                checkedReefs++;
            }
        Assert.True(checkedReefs >= 6);
    }

    static float Vector2Dist(System.Numerics.Vector2 a, System.Numerics.Vector2 b) => System.Numerics.Vector2.Distance(a, b);
}
