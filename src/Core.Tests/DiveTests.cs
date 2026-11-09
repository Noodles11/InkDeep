using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

public class DiveTests
{
    [Fact]
    public void EachDiveOfAJellyIsANewReef_TheSameForTheSameDive()
    {
        var jelly = new Jelly { Name = "Clementine", Seed = "KELP7Q2Z" };
        var first = Dives.SeedOf(jelly);
        Assert.Equal(first, Dives.SeedOf(new Jelly { Name = "Other", Seed = "KELP7Q2Z" }));
        jelly.Dives++;
        Assert.NotEqual(first, Dives.SeedOf(jelly));
        Assert.NotEqual(first, Dives.SeedOf(new Jelly { Seed = "ABCD2345" }));
    }

    [Fact]
    public void ANewJellysFirstDiveStartsInTheTutorial_OnceTutoredInDepthOne()
    {
        var jelly = new Jelly { Seed = "KELP7Q2Z" };
        Assert.Equal(LevelId.First, Dives.StartOf(jelly));
        jelly.Tutored = true;
        Assert.Equal(LevelId.FirstRun, Dives.StartOf(jelly));
    }

    [Fact]
    public void EachDepthsBossIsItsNeighbour_TillSheIsFreed()
    {
        var jelly = new Jelly { Seed = "KELP7Q2Z" };
        string[] order = { "tull", "kip", "fennel", "ottilie", "bubbles" };
        for (int shown = 1; shown <= order.Length; shown++)
            Assert.Equal(order[shown - 1], Dives.FolkBossOf(new LevelId(1, LevelPlan.Tutorial + shown, 5), jelly));
        Assert.Null(Dives.FolkBossOf(new LevelId(1, LevelPlan.Tutorial, 2), jelly));
        Assert.Null(Dives.FolkBossOf(new LevelId(1, LevelPlan.Tutorial + 1, 5) { Haven = true }, jelly));
        jelly.Freed.Add("tull");
        Assert.Null(Dives.FolkBossOf(new LevelId(1, LevelPlan.Tutorial + 1, 5), jelly));
        Assert.Equal("kip", Dives.FolkBossOf(new LevelId(2, LevelPlan.Tutorial + 2, 5), jelly));
    }

    [Fact]
    public void ADivesFanAsksNamingNoCave_ProgressedAnywhereInTheDive()
    {
        var jelly = new Jelly { Seed = "KELP7Q2Z" };
        OctoShoots.Core.Plane.Requests.FillDive(jelly, 3, _ => true, null);
        var places = OctoShoots.Core.Plane.Requests.Places(jelly, OctoShoots.Core.Plane.Requests.Dive);
        Assert.Equal(OctoShoots.Core.Plane.RequestTuning.Places, places.Count);
        Assert.All(places, r => Assert.True(r is { Mouth: -1, Region: OctoShoots.Core.Plane.Requests.Dive }));
        int seal = places.FindIndex(r => r!.Kind == OctoShoots.Core.Plane.Requests.Seal);
        Assert.True(seal >= 0, "no seal ask on the fan");
        Assert.Equal(OctoShoots.Core.Plane.RequestTuning.DiveSeals, places[seal]!.Goal);
        Assert.True(OctoShoots.Core.Plane.Requests.Take(jelly, OctoShoots.Core.Plane.Requests.Dive, seal));
        for (int i = 0; i < OctoShoots.Core.Plane.RequestTuning.DiveSeals - 1; i++)
            Assert.Empty(OctoShoots.Core.Plane.Requests.Note(jelly, OctoShoots.Core.Plane.Requests.Seal, OctoShoots.Core.Plane.Requests.Dive));
        Assert.Single(OctoShoots.Core.Plane.Requests.Note(jelly, OctoShoots.Core.Plane.Requests.Seal, OctoShoots.Core.Plane.Requests.Dive));
        var (_, line) = OctoShoots.Core.Plane.Requests.PosterOf(jelly.Carried[0], System.Array.Empty<string>());
        Assert.DoesNotContain("{mouth}", line);
        Assert.DoesNotContain(" mouth", line);
    }
}
