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

/// <summary>The reef's water: no current of its own (a labyrinth's way home is LabyrinthTests'), and a leak does not push.</summary>
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

    [Fact]
    public void A_reef_has_no_current_of_its_own_even_with_its_leaks_sealed()
    {
        foreach (bool boss in new[] { false, true })
        {
            var w = World(boss);
            Assert.Empty(w.Currents);
            for (int i = 0; i < AdventureTuning.LeaksToClear; i++) w.DebugSealLeak();
            Run(w, 2f);
            Assert.Empty(w.Currents);
            Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.MainCurrentWoke);
            Assert.Equal(Vector2.Zero, w.CurrentAt(w.Map.Start.Position));
        }
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
