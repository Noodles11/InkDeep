using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The open reef (docs/RPG-PROPOSAL.md §2–4): a region and its mouths, leak caves, their return current, experience from leaks only.</summary>
public class OpenReefTests
{
    static readonly Lazy<HandLayout> Shallows = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "shallows.json"))));

    static readonly Lazy<LevelMap> Region = new(() => TopDownGenerator.FromHand(Shallows.Value));

    static readonly SeedCode Seed = SeedCode.TryParse("KELP7Q2Z", out var s) ? s : default;

    static readonly Lazy<LevelMap> Cave = new(() =>
        TopDownGenerator.GenerateCave(Seed, OpenReef.CaveId("shallows", Shallows.Value.Depth, 0), 0f, 2));

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(LevelMap map) => new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), PlaneOptions.OpenReef);

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
    public void TheRegionHasItsMouthsAndStation_NoLeaksNoMainCurrent_AndFishInItsWater()
    {
        var map = Region.Value;
        Assert.True(map.Station);
        Assert.False(map.Haven);
        Assert.Equal(Shallows.Value.Mouths.Count, map.Mouths.Count);
        foreach (var m in map.Mouths) Assert.True(map.IsOpen(m), $"mouth {m} in rock");
        var w = World(map);
        Assert.Empty(w.Leaks);
        Assert.Empty(w.Currents);
        Assert.True(w.ReefFish.Count > 0);
        Assert.Equal(-1, w.AtMouth());
        w.Player.Position = w.Player.PrevPosition = map.Mouths[1];
        Assert.Equal(1, w.AtMouth());
        w.Player.Position = w.Player.PrevPosition = map.Exit.Position;
        Assert.True(w.AtStation);
    }

    [Fact]
    public void ALeakCaveHasOneLeakAtTheDeadEndOfItsPath_NoShaft_AndIsMostlyInked()
    {
        var map = Cave.Value;
        Assert.True(map.LeakCave);
        Assert.Equal(2, map.CaveLevel);
        Assert.False(map.Shaft.Contains(map.Exit.Position, 4f));
        var w = World(map);
        var leak = Assert.Single(w.Leaks);
        Assert.Equal(map.Exit.Position, leak.Position);
        Assert.False(w.ExitOpen);
        Assert.True(w.Dots!.Live.Count > 0);
        Assert.Equal(map.ToBytes(), TopDownGenerator.GenerateCave(Seed, map.Id, 0f, 2).ToBytes());
    }

    [Fact]
    public void SealingItIsTheOnlyExperience_ForItsCavesLevel_AndWakesTheCurrentBackOut()
    {
        var w = World(Cave.Value);
        var leak = w.Leaks[0];
        var current = Assert.Single(w.Currents);
        Assert.Equal(0f, current.Strength);
        // The return current runs from the leak back to the mouth.
        Assert.True(Vector2.Distance(current.Course[0], leak.Position) < Vector2.Distance(current.Course[^1], leak.Position));
        Assert.True(Vector2.Distance(current.Course[^1], w.Map.Start.Position) < 6f);
        Assert.Equal(0, w.Run.Xp);
        var near = Enumerable.Range(0, 16).Select(k => leak.Position + new Vector2(MathF.Cos(k * 0.4f), MathF.Sin(k * 0.4f)) * 4f).First(q => w.Clear(q, 1f));
        Hold(w, near, leak.Needed + 0.5f);
        Assert.True(leak.Sealed);
        Assert.Equal(ReefTuning.LeakXp(2), w.Run.Xp);
        Hold(w, w.Map.Start.Position + new Vector2(0f, 0.01f), CurrentTuning.MainRise + 0.5f);
        Assert.Equal(1f, current.Strength, 3);
    }

    [Fact]
    public void CleansingInkGivesNothingOnTheOpenReef_AndNoLevelUpIsHeldBack()
    {
        var w = World(Cave.Value);
        Assert.False(w.OpeningLevel);
        // Her light clears the ink round the start for a few seconds: on the old rules that was experience.
        var at = w.Dots!.Live.Select(InkDots.Centre).OrderBy(q => Vector2.Distance(q, w.Map.Start.Position)).First(q => w.Clear(q, 1f));
        Hold(w, at, 3f);
        Assert.Equal(0, w.Run.Xp);
    }

    [Fact]
    public void SheLeavesACaveByItsMouthOnlyOnceSheHasBeenIn()
    {
        var w = World(Cave.Value);
        var start = w.Map.Start.Position;
        Assert.False(w.LeavingCave);
        var deep = LevelValidator.ShortestPath(w.Map, start, w.Map.Exit.Position, clearance: 1f).First(q => Vector2.Distance(q, start) > ReefTuning.LeaveAfter + 1f);
        w.Player.Position = w.Player.PrevPosition = deep;
        Assert.False(w.LeavingCave);
        w.Player.Position = w.Player.PrevPosition = start;
        Assert.True(w.LeavingCave);
    }

    [Fact]
    public void AMouthAlwaysLeadsToACave_ASealedOneMakesWayForAFreshOne_AndLevelsRiseWithThem()
    {
        var jelly = new Jelly();
        Assert.Equal(0, OpenReef.CaveBehind(jelly, "shallows", 0));
        Assert.Equal(2, OpenReef.CaveBehind(jelly, "shallows", 2));
        Assert.Equal(1, OpenReef.CaveLevel(jelly, "shallows", 1));
        for (int i = 0; i < ReefTuning.CavesPerLevel; i++) OpenReef.Sealed(jelly, "shallows", 0, 3);
        Assert.Equal(3 * ReefTuning.CavesPerLevel, OpenReef.CaveBehind(jelly, "shallows", 0));
        Assert.Equal(1, OpenReef.CaveBehind(jelly, "shallows", 1));
        Assert.Equal(2, OpenReef.CaveLevel(jelly, "shallows", 1));
        // Every cave its own: never one level id twice.
        Assert.NotEqual(OpenReef.CaveId("shallows", 1, 0), OpenReef.CaveId("shallows", 1, 3));
        Assert.True(ReefTuning.LeakXp(5) > ReefTuning.LeakXp(1));
        Assert.Equal(25, ReefTuning.LeakXp(1));
    }
}
