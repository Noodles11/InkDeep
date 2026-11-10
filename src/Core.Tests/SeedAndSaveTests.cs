using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

public class SeedTests
{
    [Theory]
    [InlineData("KELP 7Q2Z")]
    [InlineData("AAAA AAAA")]
    [InlineData("9999 9999")]
    public void SeedCodesRoundTrip(string code)
    {
        var seed = SeedCode.Parse(code);
        Assert.Equal(code, seed.ToString());
        Assert.Equal(seed, SeedCode.Parse(code.ToLowerInvariant().Replace(" ", "")));
    }

    [Theory]
    [InlineData("KELP 7Q2")]
    [InlineData("KELP 7Q2ZZ")]
    [InlineData("KELP 0Q2Z")]
    [InlineData("KELO 7Q2Z")]
    [InlineData("KEL1 7Q2Z")]
    [InlineData("")]
    public void BadSeedsAreRejected(string code)
    {
        Assert.False(SeedCode.TryParse(code, out _));
    }

    [Fact]
    public void SpecialSeedsAreRecognised()
    {
        Assert.Equal(SpecialSeed.Debug, SeedCode.Parse("debug").Special);
        Assert.Equal("DEBUG", SeedCode.Parse("DEBUG").ToString());
        Assert.Equal(SpecialSeed.BigJelly, SeedCode.Parse("HUGE JELL").Special);
        Assert.Equal(SpecialSeed.None, SeedCode.Parse("KELP 7Q2Z").Special);
    }

    [Fact]
    public void ValuesAreFortyBits()
    {
        var seed = SeedCode.FromValue(ulong.MaxValue);
        Assert.Equal((1UL << 40) - 1, seed.Value);
        Assert.Equal("9999 9999", seed.ToString());
    }

    [Fact]
    public void StreamsDoNotDisturbEachOther()
    {
        var a = new RunStreams(SeedCode.Parse("KELP 7Q2Z"));
        var b = new RunStreams(SeedCode.Parse("KELP 7Q2Z"));
        // Burn lots of numbers on one stream of b; the item stream must not care.
        var ai = b.EnemyAi();
        for (int i = 0; i < 1000; i++) ai.NextU64();
        Assert.Equal(a.Items(2, 1, 5).NextU64(), b.Items(2, 1, 5).NextU64());
        Assert.NotEqual(a.Items(2, 1, 5).NextU64(), a.Items(2, 1, 6).NextU64());
    }
}

public class SaveTests
{
    static SaveFile Sample() => new()
    {
        Profile = new Profile
        {
            Achievements = new HashSet<string> { "untouchable" },
            SeenItems = new HashSet<string> { "triple_tentacle", "hammerhead" },
            Stats = new ProfileStats { Runs = 3, Deaths = 2, DeathsByCause = new() { ["mob_shot"] = 2 } },
            Pearls = new() { ["triple_tentacle"] = new PearlRecord { Absorbed = 4, RunsLost = 1 } },
            Creatures = new() { ["queen_clam"] = new CreatureRecord { Defeated = 1, BestSeconds = 21.5 } },
            RecentSeeds = new() { "KELP 7Q2Z" },
        },
        Run = new SuspendedRun
        {
            Seed = "KELP 7Q2Z",
            Level = 3,
            Items = new List<string> { "triple_tentacle", "hammerhead" },
            Hp = 87.5f,
            Shells = 12,
            Elapsed = 431.5,
        },
    };

    [Fact]
    public void JsonRoundTrip()
    {
        var back = SaveCodec.Deserialize(SaveCodec.Serialize(Sample()));
        Assert.Equal(SaveCodec.CurrentVersion, back.Version);
        Assert.Contains("untouchable", back.Profile.Achievements);
        Assert.Equal(2, back.Profile.Stats.DeathsByCause["mob_shot"]);
        Assert.Equal(4, back.Profile.Pearls["triple_tentacle"].Absorbed);
        Assert.Equal(21.5, back.Profile.Creatures["queen_clam"].BestSeconds);
        Assert.Equal(87.5f, back.Run!.Hp);
        Assert.Equal(3, back.Run.Level);
        Assert.Equal(new[] { "triple_tentacle", "hammerhead" }, back.Run.Items);
    }

    [Fact]
    public void ExportCodeRoundTrip()
    {
        string code = SaveCodec.Export(Sample());
        Assert.StartsWith(SaveCodec.ExportPrefix, code);
        Assert.DoesNotContain("+", code);
        Assert.DoesNotContain("/", code);
        var back = SaveCodec.Import("  " + code + "\n");
        Assert.Equal(12, back.Run!.Shells);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("CQ3D1:aGVsbG8")]
    [InlineData("INKD2:not*base64")]
    [InlineData("INKD2:aGVsbG8")]
    public void DamagedCodesAreRejected(string code)
    {
        Assert.Throws<SaveException>(() => SaveCodec.Import(code));
    }

    [Fact]
    public void NewerSavesAreRejected()
    {
        var e = Assert.Throws<SaveException>(() => SaveCodec.Deserialize("""{ "version": 99, "profile": {} }"""));
        Assert.Contains("newer", e.Message);
    }

    [Fact]
    public void AFirstPersonSaveKeepsItsProfileAndDropsItsRun()
    {
        string v1 = """{ "version": 1, "profile": { "achievements": ["untouchable"], "stats": { "runs": 4, "itemPickups": { "x": 1 } } }, "run": { "seed": "AAAA AAAA", "coins": 7, "position": [1, 2, 3] } }""";
        var save = SaveCodec.Deserialize(v1);
        Assert.Equal(SaveCodec.CurrentVersion, save.Version);
        Assert.Contains("untouchable", save.Profile.Achievements);
        Assert.Equal(4, save.Profile.Stats.Runs);
        Assert.Null(save.Run);
    }

    [Fact]
    public void OldSavesMigrateStepByStep()
    {
        // Pretend the format is at version 3: v1 kept shells as "coins", v2 had no elapsed time.
        var migrations = new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            [1] = root =>
            {
                var run = root["run"]!.AsObject();
                run["shells"] = run["coins"]!.GetValue<int>();
                run.Remove("coins");
                return root;
            },
            [2] = root =>
            {
                root["run"]!["elapsed"] = 5.0;
                return root;
            },
        };
        string v1 = """{ "version": 1, "profile": {}, "run": { "seed": "AAAA AAAA", "coins": 7 } }""";
        var save = SaveCodec.Deserialize(v1, migrations, currentVersion: 3);
        Assert.Equal(7, save.Run!.Shells);
        Assert.Equal(5.0, save.Run.Elapsed);
    }

    [Fact]
    public void ASaveFromBeforeTheGraphTakesUpAtItsReefsStart_OnLaneZero_KeepingAllElse()
    {
        // A dive in a haven before Depth 1 · Level 3, with the moment it was left and what it carried; and a run.
        string v2 = """
            { "version": 2, "profile": { "achievements": ["untouchable"] },
              "run": { "seed": "KELP7Q2Z", "cycle": 1, "depth": 2, "level": 4, "items": ["sea_chart_nautilus"], "hp": 40,
                       "state": { "world": "AAAA", "fog": "", "visited": [1, 2] } },
              "jellies": [ { "name": "Clementine", "level": 3, "shells": 12, "freed": ["tull"],
                             "dive": { "seed": "KELP7Q2Z", "cycle": 1, "depth": 2, "level": 3, "haven": true, "shells": 30, "hp": 55,
                                       "state": { "world": "AAAA" } } } ] }
            """;
        var save = SaveCodec.Deserialize(v2);
        Assert.Equal(SaveCodec.CurrentVersion, save.Version);
        Assert.Contains("untouchable", save.Profile.Achievements);
        var run = save.Run!;
        Assert.Null(run.State);
        Assert.Equal(0, run.Lane);
        Assert.Empty(run.Reefs);
        Assert.Equal(40f, run.Hp);
        Assert.Equal(new[] { "sea_chart_nautilus" }, run.Items);
        var jelly = Assert.Single(save.Jellies);
        Assert.Equal(3, jelly.Level);
        Assert.Contains("tull", jelly.Freed);
        var dive = jelly.Dive!;
        Assert.Null(dive.State);
        Assert.Equal(30, dive.Shells);
        // It takes up on the graph's reef at that row's lane 0 (a haven only where the graph has one).
        var reef = dive.Reef();
        var graph = DiveGraph.Of(SeedCode.Parse("KELP7Q2Z"));
        Assert.Equal(graph.Row(1, 2, 3)[0].Id, reef);
        Assert.Equal(graph.KindOf(reef) == ReefKind.Haven, reef.Haven);
    }

    [Fact]
    public void ADiveSavedOnTheGraphComesBackOnItsReef_WithTheReefsItHasBeenOn()
    {
        var seed = SeedCode.Parse("KELP7Q2Z");
        var graph = DiveGraph.Of(seed);
        var at = graph.Row(1, 2, 3).Last().Id;
        var dive = new SuspendedRun { Seed = seed.ToString(), Cycle = at.Cycle, Depth = at.Depth, Level = at.Level, Lane = at.Lane, Haven = at.Haven, LeaksSealed = 4, CoresTaken = 9 };
        dive.Reefs.Add(SuspendedRun.ReefKey(LevelId.FirstRun));
        dive.Reefs.Add(SuspendedRun.ReefKey(at));
        var save = new SaveFile();
        save.Jellies.Add(new Jelly { Name = "Clementine", Dive = dive });
        var back = SaveCodec.Deserialize(SaveCodec.Serialize(save)).Jellies[0].Dive!;
        Assert.Equal(at, back.Reef());
        Assert.Equal(new[] { LevelId.FirstRun, at with { Haven = false } }, back.Reefs.Select(k => SuspendedRun.FromReefKey(k)!.Value));
        Assert.Equal(4, back.LeaksSealed);
        Assert.Equal(9, back.CoresTaken);
    }

    [Fact]
    public void CustomSeedRunsDontEarnAchievements()
    {
        var profile = new Profile();
        Assert.False(profile.Award("untouchable", customSeed: true));
        Assert.True(profile.Award("untouchable", customSeed: false));
        Assert.False(profile.Award("untouchable", customSeed: false));
    }
}
