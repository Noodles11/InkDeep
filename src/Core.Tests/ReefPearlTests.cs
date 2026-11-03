using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Pearls on the open reef (docs/RPG-PROPOSAL.md §5): behaviour only, three active slots, selling, lost light.</summary>
public class ReefPearlTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneRun Reef() => new(Catalog.Value, new Tuning()) { Reef = true };

    static readonly Lazy<LevelMap> Bay = new(() => TopDownGenerator.FromHand(
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "lantern_bay.json")))));

    [Fact]
    public void OnlyLevelsChangeHerStats_APearlStillChangesWhatSheDoes()
    {
        var run = Reef();
        var bare = run.Loadout.Stats;
        float hp = bare.MaxHp, damage = bare.Damage, speed = bare.Speed;
        run.Add("coral_crown");
        run.Add("dolphin_fin");
        run.Add("triple_tentacle");
        Assert.Equal(hp, run.Loadout.Stats.MaxHp);
        Assert.Equal(damage, run.Loadout.Stats.Damage);
        Assert.Equal(speed, run.Loadout.Stats.Speed);
        Assert.Equal(3, run.Loadout.Shot.Multishot);
        run.Points = 1;
        Assert.True(run.Spend(Stat.Damage));
        Assert.True(run.Loadout.Stats.Damage > damage);
        // The same pearls on a run do change her stats.
        var old = new PlaneRun(Catalog.Value, new Tuning());
        old.Add("coral_crown");
        Assert.True(old.Loadout.Stats.MaxHp > hp);
    }

    [Fact]
    public void TheReefOffersNoStatOnlyPearlsAndNoActives()
    {
        var run = Reef();
        Assert.Equal(0f, run.OfferWeight("coral_crown"));
        Assert.Equal(0f, run.OfferWeight("dolphin_fin"));
        Assert.Equal(0f, run.OfferWeight("bubble_shield"));
        Assert.True(run.OfferWeight("triple_tentacle") > 0f);
        Assert.True(run.OfferWeight("remora_sucker") > 0f);
        var old = new PlaneRun(Catalog.Value, new Tuning());
        Assert.True(old.OfferWeight("coral_crown") > 0f);
        // The shop in the bay sells only pearls that do something.
        var w = new PlaneWorld(Bay.Value, new Tuning(), Reef());
        foreach (var stand in w.Stands.Where(s => s.Kind == StandKind.Pearl))
            Assert.False(PlaneRun.OnlyStats(Catalog.Value[stand.ItemId]) || Catalog.Value[stand.ItemId].Kind == ItemKind.Active, stand.ItemId);
    }

    [Fact]
    public void ActivesAreOwnedInThreeSlots_EachOnItsOwnButtonAndCharge()
    {
        var run = Reef();
        run.Add("tide_pulse");
        run.Add("bubble_shield");
        run.Add("whale_song");
        run.Add("conch_horn");
        Assert.DoesNotContain("tide_pulse", run.Items);
        Assert.Equal(new[] { "tide_pulse", "bubble_shield", "whale_song" }, run.Slots);
        Assert.Equal(4, run.OwnedActives.Count);
        var w = new PlaneWorld(Bay.Value, new Tuning(), run);
        w.Step(new PlaneInput { UseActive2 = true });
        Assert.Equal(0f, run.Charge(1));
        Assert.Equal(1f, run.Charge(0));
        Assert.True(w.Player.ShieldTimer > 0f);
        w.Step(new PlaneInput { UseActive2 = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ActiveNotReady && e.Direction.X == 1f);
    }

    [Fact]
    public void ASlotCyclesThroughTheActivesSheOwnsThatSitInNoOtherSlot()
    {
        var run = Reef();
        foreach (string id in new[] { "tide_pulse", "bubble_shield", "whale_song", "conch_horn" }) run.Add(id);
        run.CycleSlot(2);
        Assert.Equal("conch_horn", run.Slots[2]);
        run.CycleSlot(2);
        Assert.Null(run.Slots[2]);
        run.CycleSlot(2);
        Assert.Equal("whale_song", run.Slots[2]);
        Assert.Equal(run.Slots.Where(s => s is not null).Count(), run.Slots.Where(s => s is not null).Distinct().Count());
    }

    [Fact]
    public void SellingPaysSilverShells_AnActiveItsWorth_APassiveHalf()
    {
        var run = Reef();
        run.Add("bubble_shield");
        run.Add("triple_tentacle");
        var shield = Catalog.Value["bubble_shield"];
        var triple = Catalog.Value["triple_tentacle"];
        Assert.True(run.Sell("bubble_shield"));
        Assert.Equal(ReefTuning.PearlWorth(shield.Quality), run.Shells);
        Assert.DoesNotContain("bubble_shield", run.OwnedActives);
        Assert.DoesNotContain("bubble_shield", run.Slots);
        Assert.True(run.Sell("triple_tentacle"));
        Assert.Equal(ReefTuning.PearlWorth(shield.Quality) + ReefTuning.PearlWorth(triple.Quality) / 2, run.Shells);
        Assert.Equal(1, run.Loadout.Shot.Multishot);
        Assert.False(run.Sell("triple_tentacle"));
    }

    [Fact]
    public void HerLostLightGivesBackHerPearlsAndShellsWhenSheSwimsIntoIt()
    {
        var run = Reef();
        var w = new PlaneWorld(Bay.Value, new Tuning(), run);
        var at = w.Map.Start.Position + new Vector2(0f, -4f);
        w.Lost = new PlaneLostLight { Position = at, Pearls = { "triple_tentacle", "mitosis" }, Shells = 30 };
        w.Step(default);
        Assert.False(w.Lost.Taken);
        w.Player.Position = w.Player.PrevPosition = at;
        w.Step(default);
        Assert.True(w.Lost.Taken);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.LostLightTaken);
        Assert.Contains("triple_tentacle", run.Items);
        Assert.Contains("mitosis", run.Items);
        Assert.Equal(30, run.Shells);
        w.Step(default);
        Assert.Equal(30, run.Shells);
    }
}
