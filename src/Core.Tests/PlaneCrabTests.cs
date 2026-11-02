using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Crab Deals: the fake wall, the held bargain, the cut, and the pearls only the crab has.</summary>
public class PlaneCrabTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Crabs > 0)), new Tuning(), run, new PlaneOptions { Adventure = true });
        w.ReefFish.Clear();
        w.Schools.Clear();
        return w;
    }

    static void Hold(PlaneWorld w, Vector2 at, float seconds)
    {
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
    }

    [Fact]
    public void ItIsRare_OutsideBossLevelsAndTheFirstDepth()
    {
        var crabs = TestLevels.All(_ => true).Select(id => LevelPlan.For(TestLevels.Seed, id)).Where(p => p.Crabs > 0).ToList();
        Assert.NotEmpty(crabs);
        Assert.All(crabs, p => Assert.False(p.HasBoss));
        Assert.All(crabs, p => Assert.True(p.Id.Depth > LevelPlan.Tutorial + 1));
        Assert.All(crabs.GroupBy(p => (p.Id.Cycle, p.Id.Depth)), g => Assert.Single(g));
    }

    [Fact]
    public void TheFakeWallCarriesNoCarvingShotsDoNotOpenItAndSheSwimsThrough()
    {
        var w = World();
        var seal = w.Seals.Single(s => s.Rock.Fake);
        w.DebugStrikeSeal(seal);
        Assert.Equal(0, seal.Hits);
        Assert.False(seal.Open);
        Hold(w, seal.Rock.Center + seal.Facing * (seal.Rock.Thick + 0.5f), 0.1f);
        Assert.True(seal.Open);
    }

    [Fact]
    public void TwoPearls_OneOnlyTheCrabHas_AndLeavingCancelsTheHold()
    {
        var w = World();
        var deals = w.Stands.Where(s => s.Kind == StandKind.Deal).ToList();
        Assert.Equal(2, deals.Count);
        Assert.Contains(deals[0].ItemId, PlaneRun.CrabPearls);
        Assert.DoesNotContain(deals[1].ItemId, PlaneRun.CrabPearls);
        Assert.Empty(PlaneRun.CrabPearls.Intersect(PlaneRun.PortedPearls));
        Hold(w, deals[0].Position, CrabTuning.HoldSeconds * 0.5f);
        Assert.True(deals[0].Hold > 0f);
        Hold(w, w.Map.Start.Position, 0.1f);
        Assert.Equal(0f, deals[0].Hold);
        Assert.False(deals[0].Sold);
    }

    [Fact]
    public void AStruckDealTakesAQuarterOfHerMaxHpWhenThePearlIsAbsorbed()
    {
        var w = World();
        var deal = w.Stands.First(s => s.Kind == StandKind.Deal);
        float max = w.Run.MaxHp, arm = w.ArmLength;
        Hold(w, deal.Position, CrabTuning.HoldSeconds + 0.1f);
        Assert.True(w.Stands.Where(s => s.Kind == StandKind.Deal).All(s => s.Sold));
        Assert.Equal(max, w.Run.MaxHp);
        Hold(w, w.Map.Start.Position, PearlRitual.Total + 0.2f);
        Assert.Equal(max * (1f - CrabTuning.Cut), w.Run.MaxHp, 2);
        Assert.Contains(deal.ItemId, w.Run.Items);
        Assert.True(w.ArmLength < arm);
        Assert.True(w.Player.Hp <= w.Run.MaxHp);
    }

    [Fact]
    public void TheDealIsRefusedWhenItWouldLeaveTooLittle()
    {
        var w = World();
        var deal = w.Stands.First(s => s.Kind == StandKind.Deal);
        while (w.Run.CanBeCut) w.Run.TakeCut();
        Hold(w, deal.Position, CrabTuning.HoldSeconds + 0.5f);
        Assert.False(deal.Sold);
        Assert.Equal(0f, deal.Hold);
    }

    [Fact]
    public void MaxOneFountainALevel()
    {
        foreach (var id in TestLevels.All(_ => true).Take(20))
            Assert.InRange(LightSprings.Of(AbyssReef.Apply(TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id, luck: 6f))).Count, 0, 1);
    }

    [Fact]
    public void PearlDiverIsNotOffered() => Assert.DoesNotContain("pearl_diver", PlaneRun.PortedPearls);
}
