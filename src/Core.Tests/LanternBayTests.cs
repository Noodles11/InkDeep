using System;
using System.IO;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Lantern Bay (docs/RPG-PROPOSAL.md §2.1): a map drawn by hand, a haven, and the jellies the player makes.</summary>
public class LanternBayTests
{
    static readonly Lazy<HandLayout> Bay = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "lantern_bay.json"))));

    static readonly Lazy<LevelMap> BayMap = new(() => TopDownGenerator.FromHand(Bay.Value));

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    [Fact]
    public void TheBayIsBuiltAsDrawn_AHavenWithItsShopInACave()
    {
        var map = BayMap.Value;
        Assert.True(map.Haven);
        Assert.Equal("Lantern Bay", map.Title);
        var hand = Bay.Value;
        var post = hand.Chambers.Single(c => c.Role == CaveLayout.Role.Start);
        var station = hand.Chambers.Single(c => c.Role == CaveLayout.Role.Exit);
        Assert.Equal(new System.Numerics.Vector2(post.X, post.Y), map.Start.Position);
        Assert.Equal(new System.Numerics.Vector2(station.X, station.Y), map.Exit.Position);
        var shop = map.Pois.Single(p => p.Kind == PoiKind.Shop);
        Assert.True(shop.Cave >= 0);
        Assert.Empty(map.Spawns);
        Assert.Empty(map.WeakRocks);
        // Every chamber is open water, and the station is reached from the post.
        foreach (var c in hand.Chambers) Assert.True(map.IsOpen(new System.Numerics.Vector2(c.X, c.Y)), c.Id);
        Assert.NotEmpty(LevelValidator.ShortestPath(map, map.Start.Position, map.Exit.Position, clearance: 1f));
        // No shaft: nothing on the map is over it.
        Assert.False(map.Shaft.Contains(map.Exit.Position, 4f));
    }

    [Fact]
    public void TheBayIsTheSameEveryTime() =>
        Assert.Equal(BayMap.Value.ToBytes(), TopDownGenerator.FromHand(Bay.Value).ToBytes());

    [Fact]
    public void ItsWaterIsClean_NoLeaksNoInkNothingCorrupted_AndTheStationTakesHerOut()
    {
        var map = BayMap.Value;
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Corruption = true, Pufferlings = true });
        Assert.True(w.Options.Reef);
        Assert.False(w.Options.Pufferlings);
        Assert.Empty(w.Leaks);
        Assert.Empty(w.Dots!.Live);
        Assert.Empty(w.Currents);
        Assert.Null(w.Boss);
        Assert.Contains(w.Stands, s => s.Kind == StandKind.Pearl);
        for (int i = 0; i < 600; i++) w.Step(default);
        Assert.All(w.ReefFish, f => Assert.False(f.Corrupted));
        Assert.Empty(w.Murklings);
        Assert.False(w.Defeated);
        Assert.False(w.AtStation);
        Assert.False(w.CanDive);
        w.Player.Position = w.Player.PrevPosition = map.Exit.Position;
        Assert.True(w.AtStation);
    }

    [Fact]
    public void AJellyNeedsAFreshNameOfHerOwn()
    {
        var others = new[] { new Jelly { Name = "Mira" } };
        Assert.Null(JellyNames.Problem("Kip", others));
        Assert.NotNull(JellyNames.Problem("  ", others));
        Assert.NotNull(JellyNames.Problem("mira", others));
        Assert.NotNull(JellyNames.Problem(new string('a', JellyNames.MaxLength + 1), others));
        Assert.NotNull(JellyNames.Problem("Kip<3", others));
        Assert.True(JellyNames.IsColour("#FF8C42"));
        Assert.False(JellyNames.IsColour("FF8C42"));
    }

    [Fact]
    public void JelliesAreSavedAndReadBack()
    {
        var save = new SaveFile();
        save.Jellies.Add(new Jelly { Name = "Mira", Look = new JellyLook { Glow = "#7FE7FF", Gonads = "clover" }, Level = 3, Shells = 40 });
        save.Jellies[0].Actives.Add("tide_pulse");
        save.Jellies[0].Slots[0] = "tide_pulse";
        save.Chosen = 0;
        var back = SaveCodec.Deserialize(SaveCodec.Serialize(save));
        var mira = Assert.IsType<Jelly>(back.Current);
        Assert.Equal("Mira", mira.Name);
        Assert.Equal("#7FE7FF", mira.Look.Glow);
        Assert.Equal("clover", mira.Look.Gonads);
        Assert.Equal(3, mira.Level);
        Assert.Equal("tide_pulse", mira.Slots[0]);
        Assert.Null(mira.Slots[1]);
        Assert.Equal(3, mira.Slots.Count);
    }
}
