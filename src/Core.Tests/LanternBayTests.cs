using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// Lantern Bay (docs/RPG-PROPOSAL.md §2.1): the haven in the Sunlit Shallows' south-west corner, drawn by hand on the
/// reef's one map, and the jellies the player makes.
/// </summary>
public class LanternBayTests
{
    static readonly Lazy<HandLayout> Reef = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "shallows.json"))));

    static readonly Lazy<LevelMap> ReefMap = new(() => TopDownGenerator.FromHand(Reef.Value));

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(LevelMap map) => new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), PlaneOptions.OpenReef);

    [Fact]
    public void TheReefIsBuiltAsDrawn_NineTimesTheBay_WithTheBayAsAHavenInItsCorner()
    {
        var map = ReefMap.Value;
        Assert.Equal(420, map.Size);
        Assert.Equal("Sunlit Shallows", map.Title);
        var haven = Assert.NotNull(map.Haven);
        Assert.Equal(new Vector2(0f, 280f), haven.Min);
        Assert.Equal(new Vector2(140f, 420f), haven.Max);
        Assert.Equal(9f, map.Size * map.Size / ((haven.Max.X - haven.Min.X) * (haven.Max.Y - haven.Min.Y)), 2);
        var hand = Reef.Value;
        var post = hand.Chambers.Single(c => c.Role == CaveLayout.Role.Start);
        Assert.Equal(new Vector2(post.X, post.Y), map.Start.Position);
        Assert.True(map.InHaven(map.Start.Position));
        // The shop is the bay's, in a cave; the mirror and the Tide Fan are the bay's too.
        var shop = map.Pois.Single(p => p.Kind == PoiKind.Shop);
        Assert.True(shop.Cave >= 0);
        Assert.True(map.InHaven(shop.Position));
        Assert.True(map.InHaven(Assert.NotNull(map.Mirror)));
        Assert.True(map.InHaven(Assert.NotNull(map.Fan)));
        Assert.Empty(map.Spawns);
        Assert.Empty(map.WeakRocks);
        // Every chamber is open water, and all of the reef is reached from the post.
        foreach (var c in hand.Chambers) Assert.True(map.IsOpen(new Vector2(c.X, c.Y)), c.Id);
        var fromPost = LevelValidator.Distances(map, map.Start.Position, clearance: 1f);
        foreach (var c in hand.Chambers) Assert.True(float.IsFinite(LevelValidator.DistanceAt(fromPost, new Vector2(c.X, c.Y))), $"{c.Id} cannot be reached from the post");
        // The cave mouths are out on the reef, none in the bay; there is no current station and no shaft.
        Assert.Equal(hand.Mouths.Count, map.Mouths.Count);
        Assert.All(map.Mouths, m => Assert.False(map.InHaven(m.At, 10f)));
        Assert.False(map.CurrentStation);
        Assert.False(map.Shaft.Contains(map.Exit.Position, 4f));
    }

    [Fact]
    public void TheReefIsTheSameEveryTime() =>
        Assert.Equal(ReefMap.Value.ToBytes(), TopDownGenerator.FromHand(Reef.Value).ToBytes());

    [Fact]
    public void ItsWaterIsClean_NoLeaksNoInkNothingCorrupted_AndNoStationTakesHerAnywhere()
    {
        var map = ReefMap.Value;
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), PlaneOptions.OpenReef);
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
        Assert.False(w.CanDive);
        w.Player.Position = w.Player.PrevPosition = map.Exit.Position;
        Assert.False(w.AtStation);
        // No light spring in the bay: coming home heals her whole.
        Assert.All(w.Springs, s => Assert.False(map.InHaven(s.Position)));
    }

    [Fact]
    public void InTheBaySheIsSafe_LeavingItSheIsOnTheOpenReef_AndComingHomeHealsHerWhole()
    {
        var map = ReefMap.Value;
        var w = World(map);
        Assert.True(w.InHaven);
        var landing = Reef.Value.Chambers.Single(c => c.Id == "landing");
        var outside = new Vector2(landing.X, landing.Y);
        Assert.False(map.InHaven(outside));
        Assert.True(map.OutOfHaven(outside) > 30f);
        Assert.Equal(0f, map.OutOfHaven(map.Start.Position));
        // Out on the reef: she has left the haven.
        w.Player.Position = w.Player.PrevPosition = outside;
        w.Step(default);
        Assert.False(w.InHaven);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.HavenLeft);
        // Hurt out there, then home: whole again.
        w.Player.Hp = w.Run.Hp = w.Run.MaxHp * 0.3f;
        w.Player.Position = w.Player.PrevPosition = map.Start.Position;
        w.Step(default);
        Assert.True(w.InHaven);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.HavenEntered);
        Assert.Equal(w.Run.MaxHp, w.Player.Hp);
    }

    [Fact]
    public void TheMirrorLiesOnBareOpenFloorAndShowsHerWhenSheSwimsOntoIt()
    {
        var map = ReefMap.Value;
        var mirror = Assert.NotNull(map.Mirror);
        var w = World(map);
        Assert.True(w.Clear(mirror, LevelMap.MirrorRadius));
        Assert.DoesNotContain(map.Decor, d => Vector2.Distance(d.Position, mirror) < LevelMap.MirrorRadius);
        Assert.DoesNotContain(map.Kelp.SelectMany(k => k.Roots), r => Vector2.Distance(r, mirror) < LevelMap.MirrorRadius);
        Assert.NotEmpty(LevelValidator.ShortestPath(map, map.Start.Position, mirror, clearance: 1f));
        Assert.False(w.AtMirror);
        w.Player.Position = w.Player.PrevPosition = mirror;
        Assert.True(w.AtMirror);
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
