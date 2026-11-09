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

/// <summary>A dive's leak caves (docs/DIVE.md §6).</summary>
public class DiveCaveTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly SeedCode Seed = SeedCode.Parse("KELP7Q2Z");

    static LevelMap Cave(int k) => TopDownGenerator.GenerateCave(Seed, new LevelId(501, 2, 10 + k), 0.1f, 1, new Vector2(0f, -1f), areaScale: DiveCaveTuning.Area);

    [Fact]
    public void ACaveIsHalfALevel_WithItsWayOutAndItsLeak()
    {
        for (int k = 0; k < 4; k++)
        {
            var map = Cave(k);
            Assert.True(map.LeakCave);
            Assert.NotNull(map.Way);
            int area = LevelValidator.WaterArea(map);
            Assert.InRange(area, LevelValidator.AreaMin * 0.5f, LevelValidator.AreaMax * 0.5f);
        }
    }

    [Fact]
    public void ItsWaterHarmsHerTillItsLeakIsSealed_ThenSheIsHealedWholeAndAPearlRises()
    {
        var map = Cave(0);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, DiveCave = true });
        Assert.True(w.CaveLeakOpen);
        float full = w.Player.Hp;
        for (int i = 0; i < 5 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = map.Start.Position;
            w.Step(default);
        }
        // (Its creatures may harm her besides.)
        Assert.True(full - w.Player.Hp >= DiveCaveTuning.Sting * 4.5f, $"{full - w.Player.Hp} HP in 5 s");
        int pearls = w.Pearls.Count;
        foreach (var v in w.Leaks.ToList()) w.DebugSealLeak(v);
        Assert.False(w.CaveLeakOpen);
        Assert.Equal(w.Run.MaxHp, w.Player.Hp);
        Assert.True(w.Pearls.Count > pearls, "no pearl for sealing it");
        // No current holds its way out: she may leave whenever she likes.
        Assert.DoesNotContain(w.Currents, c => c.Holds);
        // Sealed, its water harms her no more (its creatures gone, to tell).
        w.Murklings.Clear();
        w.Shots.Clear();
        float hp = w.Player.Hp;
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++) w.Step(default);
        Assert.True(w.Player.Hp >= hp - 0.01f);
    }

    [Fact]
    public void TheLevelIsPutBackAsSheLeftItForTheCave_WhatSheGainedThereStaysHers()
    {
        var id = new LevelId(1, 2, 1);
        var map = TopDownGenerator.Generate(new RunStreams(Seed), id);
        var run = new PlaneRun(Catalog.Value, new Tuning());
        var options = new PlaneOptions { Adventure = true, Corruption = true };
        var w = new PlaneWorld(map, new Tuning(), run, options);
        var anchors = w.SaveLevelAnchors();
        // On the level: a leak sealed, then she goes into the cave.
        var sealedOne = w.Leaks[0].Position;
        w.DebugSealLeak(w.Leaks[0]);
        for (int i = 0; i < 30; i++) w.Step(default);
        var state = w.SaveState(anchors);
        // In the cave: shells and a pearl gained.
        run.Shells += 25;
        run.Add("mitosis");
        // Back out: the level made again from its seed, with the same run, and put back as she left it.
        var again = new PlaneWorld(TopDownGenerator.Generate(new RunStreams(Seed), id), new Tuning(), run, options);
        var anchorsAgain = again.SaveLevelAnchors();
        again.LoadState(anchorsAgain, state);
        Assert.True(again.Leaks.Find(v => v.Position == sealedOne)!.Sealed);
        Assert.Same(run, again.Run);
        Assert.Equal(25, run.Shells);
        Assert.Contains("mitosis", run.Items);
    }
}
