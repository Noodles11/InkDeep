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

/// <summary>The main current, asleep until enough leaks are sealed, and each leak's corrupted one.</summary>
public class PlaneCurrentTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(bool boss = false)
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 2 && p.HasBoss == boss);
        var w = new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        Quiet(w);
        return w;
    }

    static void Quiet(PlaneWorld w)
    {
        w.Mobs.Clear();
        w.Fish.Clear();
        w.Murklings.Clear();
    }

    static void Run(PlaneWorld w, float seconds)
    {
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
    }

    static PlaneCurrent Main(PlaneWorld w) => w.Currents.Single();

    [Fact]
    public void TheMainCurrentRunsFromHerStartToTheWayDownThroughWater()
    {
        var w = World();
        var main = Main(w);
        Assert.True(Vector2.Distance(main.Course[0], w.Map.Start.Position) < 2f);
        Assert.True(Vector2.Distance(main.Course[^1], w.Map.Exit.Position) < 2f);
        Assert.All(main.Course, q => Assert.True(w.Map.IsOpen(q), $"{q} in rock"));
        for (int i = 1; i < main.Course.Count; i++) Assert.True(Vector2.Distance(main.Course[i - 1], main.Course[i]) < 4f);
    }

    [Fact]
    public void ItSleepsUntilTheFirstLeakIsSealed_ThenWakesAndTellsSo()
    {
        var w = World();
        var mid = Main(w).Course[Main(w).Course.Count / 2];
        Assert.Equal(0f, Main(w).Strength);
        Assert.Equal(Vector2.Zero, Main(w).At(mid));
        Run(w, 1f);
        Assert.Equal(0f, Main(w).Strength);

        // (The first leak sealed opens a hole: the way on is open, and the water remembers it.)
        w.DebugSealLeak();
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.MainCurrentWoke);
        Run(w, CurrentTuning.MainRise + 0.1f);
        Assert.Equal(1f, Main(w).Strength);
        Assert.InRange(Main(w).At(mid).Length(), CurrentTuning.MainSpeed * 0.9f, CurrentTuning.MainSpeed * 1.2f);
    }

    [Fact]
    public void RidingItCarriesHerToTheWayDownWithoutASwimStroke()
    {
        var w = World();
        for (int i = 0; i < AdventureTuning.LeaksToClear; i++) w.DebugSealLeak();
        Run(w, CurrentTuning.MainRise);
        foreach (var c in w.Currents) c.Strength = 1f;
        w.Clouds.Clear();
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        float seconds = Main(w).Length / CurrentTuning.MainSpeed * 1.5f + 5f;
        Run(w, seconds);
        Assert.True(Vector2.Distance(w.Player.Position, w.Map.Exit.Position) < CurrentTuning.EndFade + 2f, $"{Vector2.Distance(w.Player.Position, w.Map.Exit.Position)} m short");
    }

    [Fact]
    public void OnABossLevelItStopsAtTheArenasEdge()
    {
        var w = World(boss: true);
        var main = Main(w);
        Assert.InRange(Vector2.Distance(main.Course[^1], w.Map.Exit.Position), w.Map.Exit.Radius, w.Map.Exit.Radius + 4f);
    }

    [Fact]
    public void ALeakDoesNotPush()
    {
        var w = World();
        var v = w.Leaks[0];
        foreach (float d in new[] { 0.5f, 3f, 8f })
            Assert.Equal(Vector2.Zero, w.CurrentAt(v.Position + new Vector2(d, 0f), main: false));
    }

    [Fact]
    public void NoCreatureGetsUnderALeak()
    {
        var w = World();
        var v = w.Leaks[0];
        var fish = w.ReefFish[0];
        fish.Position = v.Position;
        var at = fish.Position;
        Run(w, 0.1f);
        Assert.True(Vector2.Distance(fish.Position, v.Position) >= AdventureTuning.LeakClear - 0.5f);
    }

    [Fact]
    public void WhatLivesInTheWaterAndWhatLiesInItStayPut()
    {
        var w = World();
        foreach (var c in w.Currents) c.Strength = 1f;
        var mid = Main(w).Course[Main(w).Course.Count / 2];
        var shell = new PlaneShell { Position = mid };
        w.Shells.Clear();
        w.Shells.Add(shell);
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        Run(w, 0.5f);
        Assert.True(Vector2.Distance(shell.Position, mid) < 0.5f, "only what it carries moves");
    }

    [Fact]
    public void NoFountainLiesOnTheMainCurrent()
    {
        foreach (var id in TestLevels.All(p => true).Take(20))
        {
            var map = AbyssReef.Apply(TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id));
            var springs = LightSprings.Of(map);
            Assert.NotEmpty(springs);
            Assert.All(springs, at => Assert.False(MainCurrent.Touches(map, at, 0f), $"{id}: a fountain on the current at {at}"));
        }
    }

    static (PlaneWorld World, PlaneBoss Boss) AtTheBoss(float share)
    {
        var w = World(boss: true);
        var dots = w.Dots!;
        int want = (int)(share * dots.Open.Count(o => o));
        foreach (int i in dots.Live.ToArray()) dots.Remove(i);
        foreach (int i in Enumerable.Range(0, InkDots.N * InkDots.N).Where(i => dots.Open[i]).Take(want)) dots.Add(i);
        w.Player.Position = w.Player.PrevPosition = w.Map.Exit.Position + new Vector2(6f, 0f);
        Quiet(w);
        w.Step(default);
        return (w, w.Boss!);
    }

    [Fact]
    public void TheBossDrawsInEveryDotLeftAndDrinksTheLeaksDry()
    {
        var (w, boss) = AtTheBoss(0.5f);
        Assert.Equal(BossStage.Drop, boss.Stage);
        Assert.Equal(0, w.LeaksOpen);
        // (Its reef's other holes' leaks, if it has any, drunk dry as it rises.)
        if (w.Leaks.Count > 0) Assert.Contains(w.Events, e => e.Type == PlaneEventType.LeakDrunk);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.LeakSealed);
        int total = w.Dots!.Count, gathered = 0;
        while (boss.Stage == BossStage.Drop)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            gathered += w.Events.Count(e => e.Type == PlaneEventType.DotGathered);
        }
        Assert.Equal(BossStage.Fight, boss.Stage);
        Assert.Equal(0, w.Dots.Count);
        Assert.InRange(gathered, total * 0.7f, total + 1);
    }

    [Fact]
    public void TheMoreInkSheLeftTheBiggerAndHarderItComes()
    {
        var (_, little) = AtTheBoss(0.1f);
        var (_, much) = AtTheBoss(0.8f);
        Assert.True(much.Ink > little.Ink);
        Assert.True(much.Radius > little.Radius);
        Assert.True(much.MaxHp > little.MaxHp);
        Assert.True(much.Damage > little.Damage);
        var (_, none) = AtTheBoss(0f);
        Assert.Equal(none.Spec.Hp, none.MaxHp);
        Assert.Equal(none.Spec.Damage, none.Damage);
    }

    [Fact]
    public void ThereAreNoCurrentsOutsideTheAdventure()
    {
        var w = new PlaneWorld(TestLevels.OneTreasure(), new Tuning());
        Assert.Empty(w.Currents);
    }
}
