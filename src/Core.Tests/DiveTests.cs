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
}
