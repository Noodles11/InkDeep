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

/// <summary>The open reef (docs/RPG-PROPOSAL.md §2–4): a region and its mouths, leak caves and their ways out, the current that holds one, experience from leaks.</summary>
public class OpenReefTests
{
    static readonly Lazy<HandLayout> Shallows = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "reef.json"))));

    static readonly Lazy<LevelMap> Region = new(() => TopDownGenerator.FromHand(Shallows.Value));

    static readonly SeedCode Seed = SeedCode.TryParse("KELP7Q2Z", out var s) ? s : default;

    static readonly Lazy<LevelMap> Cave = new(() =>
        TopDownGenerator.GenerateCave(Seed, OpenReef.CaveId("reef", Shallows.Value.Depth, 0), 0f, 2));

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
    public void TheReefHasItsMouths_NoLeaksNoMainCurrent_AndFishInItsWater()
    {
        var map = Region.Value;
        Assert.True(map.Station);
        Assert.NotNull(map.Haven);
        Assert.Equal(Shallows.Value.Mouths.Count, map.Mouths.Count);
        foreach (var m in map.Mouths) AssertAnOpeningInAWall(map, m);
        var w = World(map);
        Assert.Empty(w.Leaks);
        Assert.Empty(w.Currents);
        Assert.True(w.ReefFish.Count > 0);
        Assert.Equal(-1, w.AtMouth());
        // At its threshold, not yet; in its passage, she is taken through.
        w.Player.Position = w.Player.PrevPosition = map.Mouths[1].At;
        Assert.Equal(-1, w.AtMouth());
        w.Player.Position = w.Player.PrevPosition = map.Mouths[1].Inside;
        Assert.Equal(1, w.AtMouth());
    }

    /// <summary>
    /// A cave's opening: water from its threshold to its back, rock round it, behind it and over it (its roof, never
    /// under its arch), and the way back out of it to the water it opens on.
    /// </summary>
    static void AssertAnOpeningInAWall(LevelMap map, Doorway door)
    {
        Assert.InRange(door.Into.Length(), 0.99f, 1.01f);
        for (float s = 0f; s <= Doorway.Depth - 0.4f; s += 0.5f) Assert.True(map.IsOpen(door.At + door.Into * s), $"its passage is rock {s} m in");
        Assert.False(map.IsOpen(door.Back + door.Into * 1.5f), "no rock behind its back");
        foreach (float side in new[] { -1f, 1f })
        {
            var beside = door.Inside + door.Across * (side * (Doorway.HalfWidth + 1.2f));
            Assert.False(map.IsOpen(beside), $"no rock beside it: {map.HeightAt(beside)} m at {beside}, door {door}");
        }
        var over = door.Inside;
        float roof = map.RoofAt((int)MathF.Round(over.X), (int)MathF.Round(over.Y));
        Assert.True(roof >= Doorway.FaceHeight, $"its roof is {roof} m");
        Assert.True(map.HeightAt(door.At) >= -1.01f && map.IsOpen(door.At - door.Into * 1f), "its threshold opens on no water");
    }

    [Fact]
    public void EveryLeakCaveHasItsWayOutInItsStartChambersWall_AndSheComesInByIt()
    {
        for (int k = 0; k < 24; k++)
        {
            var map = TopDownGenerator.GenerateCave(Seed, OpenReef.CaveId("reef", Shallows.Value.Depth, k), k % 3, 1 + k % 4);
            var way = Assert.NotNull(map.Way);
            AssertAnOpeningInAWall(map, way);
            Assert.True(Vector2.Distance(way.At, map.Start.Position) < 32f, $"cave {k}: its way out is {Vector2.Distance(way.At, map.Start.Position)} m from its start");
            Assert.NotEmpty(LevelValidator.ShortestPath(map, map.Exit.Position, way.At - way.Into * 1.5f, clearance: 0.5f));
            Assert.Equal(way.Inside, World(map).Player.Position);
        }
    }

    [Fact]
    public void ALeakCavesWayInFacesTheMouthSheSwamInto_SoSheComesOutStillSwimmingTheWaySheWent()
    {
        var ways = new[] { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f) };
        int near = 0, all = 0;
        for (int k = 0; k < 12; k++)
            foreach (var went in ways)
            {
                var map = TopDownGenerator.GenerateCave(Seed, OpenReef.CaveId("reef", Shallows.Value.Depth, k), 0f, 2, went);
                var way = Assert.NotNull(map.Way);
                AssertAnOpeningInAWall(map, way);
                Assert.NotEmpty(LevelValidator.ShortestPath(map, map.Exit.Position, way.At - way.Into * 1.5f, clearance: 0.5f));
                // Out of it she swims -Into: the way she swam into the mouth.
                float along = Vector2.Dot(-way.Into, went);
                Assert.True(along > 0f, $"cave {k} in by {went}: out of its way in by {-way.Into}");
                if (along > 0.85f) near++;
                all++;
            }
        Assert.True(near >= all * 9 / 10, $"{near} of {all} within about 30°");
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
    public void ALeakCaveHoldsOnlyMurklings_AWaitingPrime_AndBudsMoreUntilItsLeakIsSealed()
    {
        var w = World(Cave.Value);
        var start = w.Map.Start.Position;
        // No fish in it, so nothing turns: only murklings and primes, none at her start.
        Assert.Empty(w.ReefFish);
        Assert.Empty(w.Fish);
        Assert.Empty(w.Flounders);
        Assert.All(w.Murklings, m => Assert.Null(m.Fish));
        var prime = Assert.Single(w.Murklings, m => m.Prime);
        Assert.True(Vector2.Distance(prime.Position, w.Map.Exit.Position) >= ReefTuning.CavePrimeBeforeLeak - 0.01f);
        int waiting = w.Murklings.Count(m => !m.Prime);
        Assert.InRange(waiting, 2, ReefTuning.CaveMurklings + (int)MathF.Round(ReefTuning.CaveMurklingsPerMenace * w.Menace));
        Assert.All(w.Murklings, m => Assert.True(Vector2.Distance(m.Position, start) >= ReefTuning.CaveMurkFromStart - 2.1f));

        // Swimming in, more bud out of the ink round her.
        var deep = LevelValidator.ShortestPath(w.Map, start, w.Map.Exit.Position, clearance: 1f).First(q => Vector2.Distance(q, start) > 25f);
        int before = w.Murklings.Count;
        Hold(w, deep, ReefTuning.CaveBudEvery * 2f);
        Assert.True(w.Murklings.Count > before);
        Assert.All(w.Murklings, m => Assert.Null(m.Fish));

        // Sealed, it buds no more (she never shoots here, so no prime breaks apart either).
        w.DebugSealLeak();
        int sealedWith = w.Murklings.Count;
        Hold(w, deep, ReefTuning.CaveBudEvery * 2f);
        Assert.Equal(sealedWith, w.Murklings.Count);
    }

    [Fact]
    public void SealingItIsTheOnlyExperience_ForItsCavesLevel_AndThenNoCurrentHoldsItsWayOut()
    {
        var w = World(Cave.Value);
        var way = w.Map.Way!.Value;
        var leak = w.Leaks[0];
        // While its leak is open the Leak's current pours in through its way out, faster than she swims.
        var hold = Assert.Single(w.Currents);
        Assert.True(hold.Holds);
        var flow = w.CurrentAt(way.At);
        Assert.True(Vector2.Dot(flow, -way.Into) > 9f, $"the flow at its threshold is {flow}");
        Assert.Equal(0, w.Run.Xp);
        var near = Enumerable.Range(0, 16).Select(k => leak.Position + new Vector2(MathF.Cos(k * 0.4f), MathF.Sin(k * 0.4f)) * 4f).First(q => w.Clear(q, 1f));
        Hold(w, near, leak.Needed + 0.5f);
        Assert.True(leak.Sealed);
        Assert.Equal(ReefTuning.LeakXp(2), w.Run.Xp);
        // Sealed, it dies away: no current carries her back out either, she swims out herself.
        Hold(w, near, CurrentTuning.HoldFade + 0.2f);
        Assert.Equal(Vector2.Zero, w.CurrentAt(way.At));
    }

    [Fact]
    public void TheLeaksCurrentPushesHerBackOutOfTheWayOut()
    {
        var w = World(Cave.Value);
        var way = w.Map.Way!.Value;
        // Set in its passage, she is carried out of it into the cave.
        w.Player.Position = w.Player.PrevPosition = way.At + way.Into * 2f;
        for (int i = 0; i < PlaneWorld.TickRate; i++) w.Step(default);
        Assert.True(way.Measure(w.Player.Position).In < 0f, $"she is still {way.Measure(w.Player.Position).In} m in");
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
    public void SheLeavesACaveByItsWayOutOnlyOnceSheHasBeenIn_AndItsLeakIsSealed()
    {
        var w = World(Cave.Value);
        var way = w.Map.Way!.Value;
        // She comes in by it: not out again at once.
        Assert.False(w.AtCaveMouth);
        Assert.False(w.LeavingCave);
        var deep = LevelValidator.ShortestPath(w.Map, way.At, w.Map.Exit.Position, clearance: 1f).First(q => Vector2.Distance(q, way.At) > ReefTuning.LeaveAfter + 1f);
        w.Player.Position = w.Player.PrevPosition = deep;
        Assert.False(w.LeavingCave);
        // Back at its threshold with the leak still open: its current holds it.
        w.Player.Position = w.Player.PrevPosition = way.At;
        Assert.True(w.AtCaveMouth);
        Assert.False(w.LeavingCave);
        w.Player.Position = w.Player.PrevPosition = way.Inside;
        Assert.False(w.LeavingCave);
        w.DebugSealLeak();
        Assert.Equal(0, w.LeaksOpen);
        // Sealed: at its threshold she is not yet through; in its passage she is.
        w.Player.Position = w.Player.PrevPosition = way.At;
        Assert.False(w.LeavingCave);
        w.Player.Position = w.Player.PrevPosition = way.Inside;
        Assert.True(w.LeavingCave);
    }

    [Fact]
    public void ASealedCavesMouthIsShut_UntilTheLeakSeepsBack_ThenItOpensOnAFreshCave()
    {
        var jelly = new Jelly { PlaySeconds = 100 };
        Assert.Empty(OpenReef.ShutMouths(jelly, "reef"));
        OpenReef.Sealed(jelly, "reef", 1, 3);
        Assert.Equal(new[] { 1 }, OpenReef.ShutMouths(jelly, "reef"));
        // In the region, it stands closed with rock: she cannot go in by it.
        var w = World(Region.Value);
        w.ShutMouths = OpenReef.ShutMouths(jelly, "reef");
        w.Player.Position = w.Player.PrevPosition = w.Map.Mouths[1].Inside;
        Assert.Equal(-1, w.AtMouth());
        Assert.False(w.Clear(w.Map.Mouths[1].Inside, 0.5f));
        Assert.False(w.Clear(w.Map.Mouths[1].At + w.Map.Mouths[1].Into * 0.3f, 0.5f));
        Assert.True(w.Clear(w.Map.Mouths[1].At - w.Map.Mouths[1].Into * 1f, 0.5f));
        w.Player.Position = w.Player.PrevPosition = w.Map.Mouths[0].Inside;
        Assert.True(w.Clear(w.Map.Mouths[0].Inside, 0.5f));
        Assert.Equal(0, w.AtMouth());
        // Not yet...
        jelly.PlaySeconds += ReefTuning.Recorrupt - 1;
        Assert.Contains(1, OpenReef.ShutMouths(jelly, "reef"));
        // ...then it opens, on the next cave behind it.
        jelly.PlaySeconds += 2;
        Assert.Empty(OpenReef.ShutMouths(jelly, "reef"));
        Assert.Equal(1 + 3, OpenReef.CaveBehind(jelly, "reef", 1));
    }

    [Fact]
    public void LeakCavesHoldOnlySecretsCurseDensAndLightSprings_AndTheBayNoSpring()
    {
        bool curse = false;
        for (int k = 0; k < 16; k++)
        {
            var map = TopDownGenerator.GenerateCave(Seed, OpenReef.CaveId("reef", Shallows.Value.Depth, k), 0f, 2);
            Assert.All(map.Pois, p => Assert.Contains(p.Kind, new[] { PoiKind.Start, PoiKind.Exit, PoiKind.Secret, PoiKind.CurseDen }));
            curse |= map.Pois.Exists(p => p.Kind == PoiKind.CurseDen);
        }
        Assert.True(curse, "no cave of sixteen had a curse den");
        Assert.All(World(Region.Value).Springs, s => Assert.False(Region.Value.InHaven(s.Position)));
    }

    [Fact]
    public void TheReefHoldsFourZones_TheirLevelsRisingAwayFromTheBay_AndEveryZoneHasCavesOfEveryDanger()
    {
        var hand = Shallows.Value;
        var map = Region.Value;
        Assert.Equal(new[] { "shallows", "kelp", "galleon", "carnival" }, map.Zones.Select(z => z.Name));
        Assert.Equal(0, map.ZoneAt(map.Start.Position));
        Assert.True(map.Zones.Zip(map.Zones.Skip(1)).All(p => p.First.Level < p.Second.Level), "area levels rise zone by zone");
        for (int z = 0; z < map.Zones.Count; z++)
        {
            var zone = map.Zones[z];
            var mouths = Enumerable.Range(0, hand.Mouths.Count).Where(m => map.ZoneAt(map.Mouths[m].At) == z).ToList();
            Assert.Equal(new[] { 0, 1, 2 }, mouths.Select(m => hand.Mouths[m].Danger).Distinct().OrderBy(d => d));
            Assert.All(mouths, m => Assert.Equal(zone.Level + hand.Mouths[m].Danger, OpenReef.CaveLevel(hand, m)));
            // She can swim to every one of its mouths from the bay.
            Assert.All(mouths, m => Assert.NotEmpty(LevelValidator.ShortestPath(map, map.Start.Position, map.Mouths[m].At - map.Mouths[m].Into * 2f, clearance: 1f)));
        }
        Assert.Equal(map.Zones.Min(z => z.Level), OpenReef.CaveLevel(hand, -1));
    }

    [Fact]
    public void ADangerousCaveGoesDownFloors_EachAboveTheDeepestEndingInAHole_TheDeepestInItsGuardedLeak()
    {
        var hand = Shallows.Value;
        int deep = hand.Mouths.FindIndex(m => m.Danger == 2);
        Assert.Equal(3, OpenReef.CaveFloors(hand, deep));
        Assert.Equal(1, OpenReef.CaveFloors(hand, hand.Mouths.FindIndex(m => m.Danger == 0)));
        var id = OpenReef.CaveId("reef", hand.Depth, 0);
        var floors = Enumerable.Range(1, 3).Select(f => TopDownGenerator.GenerateCave(Seed, id, 0.2f, 3, new Vector2(0f, -1f), f, 3)).ToList();
        Assert.Equal(3, floors.Select(m => m.Id).Distinct().Count());
        Assert.True(floors[0].Menace < floors[2].Menace, "each floor down is meaner");
        // The first floor has the way in; she comes down into the deeper ones from above.
        Assert.NotNull(floors[0].Way);
        Assert.Null(floors[1].Way);
        for (int f = 0; f < 2; f++)
        {
            var map = floors[f];
            Assert.True(map.HoleDown);
            Assert.True(map.Shaft.Contains(map.Exit.Position, 0f), $"floor {f + 1}: its hole lies where the leak would be");
            var w = World(map);
            Assert.Empty(w.Leaks);
            Assert.True(w.CaveLeakOpen, "its leak lies below, open");
            Assert.DoesNotContain(w.Murklings, m => m.Prime);
        }
        var bottom = floors[2];
        Assert.False(bottom.HoleDown);
        Assert.False(bottom.Shaft.Contains(bottom.Exit.Position, 4f));
        var wb = World(bottom);
        Assert.Single(wb.Leaks);
        Assert.Contains(wb.Murklings, m => m.Prime);
        // Its name is its zone's own, the same every time.
        string name = OpenReef.CaveName("galleon", "KELP7Q2Z", 4);
        Assert.StartsWith("The ", name);
        Assert.Equal(name, OpenReef.CaveName("galleon", "KELP7Q2Z", 4));
    }

    [Fact]
    public void AMouthAlwaysLeadsToACave_ASealedOneMakesWayForAFreshOne()
    {
        var jelly = new Jelly();
        Assert.Equal(0, OpenReef.CaveBehind(jelly, "reef", 0));
        Assert.Equal(2, OpenReef.CaveBehind(jelly, "reef", 2));
        for (int i = 0; i < 3; i++) OpenReef.Sealed(jelly, "reef", 0, 3);
        Assert.Equal(9, OpenReef.CaveBehind(jelly, "reef", 0));
        Assert.Equal(1, OpenReef.CaveBehind(jelly, "reef", 1));
        Assert.Equal(3, OpenReef.SealedIn(jelly, "reef"));
        // Every cave its own: never one level id twice.
        Assert.NotEqual(OpenReef.CaveId("reef", 1, 0), OpenReef.CaveId("reef", 1, 3));
        Assert.True(ReefTuning.LeakXp(5) > ReefTuning.LeakXp(1));
        Assert.Equal(25, ReefTuning.LeakXp(1));
    }
}
