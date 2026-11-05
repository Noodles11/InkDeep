using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

public class HavenTests
{
    static readonly Lazy<HandLayout> Hand = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "haven.json"))));

    [Fact]
    public void HavensComeEveryTwoOrThreeLevels_NeverTwiceRunning()
    {
        foreach (string code in new[] { "KELP7Q2Z", "ABCD2345", "HAVENS23", "QQQQ2222" })
        {
            var seed = SeedCode.Parse(code);
            int since = 0, havens = 0;
            var at = LevelId.FirstRun;
            while (at.Cycle == 1)
            {
                var next = Dives.NextOf(seed, at);
                // Leaving a level: one more since the last haven.
                if (!at.Haven) since++;
                if (next.Haven)
                {
                    Assert.False(at.Haven, "a haven straight after a haven");
                    Assert.InRange(since, HavenTuning.Maybe, HavenTuning.Surely);
                    // Its hole leads on to the level it stands before.
                    Assert.Equal(next with { Haven = false }, Dives.NextOf(seed, next));
                    havens++;
                    since = 0;
                }
                Assert.True(since <= HavenTuning.Surely, $"{since} levels without a haven at {next}");
                at = next;
            }
            Assert.False(Dives.NextOf(seed, LevelId.First).Haven);
            // Thirty levels a cycle, a haven every two or three of them.
            Assert.InRange(havens, 8, 15);
        }
    }

    [Fact]
    public void TheHavenIsBuiltWithItsHoleOnward_AllOfItHavenWater_ItsHomesMirrorAndFanInTheOpen()
    {
        var map = TopDownGenerator.FromHand(Hand.Value);
        Assert.True(map.Shaft.HalfLength > 1f, "no hole down");
        Assert.True(map.IsOpen(map.Exit.Position));
        Assert.True(map.InHaven(map.Start.Position) && map.InHaven(map.Exit.Position));
        Assert.Contains(map.Pois, p => p.Kind == PoiKind.Shop);
        Assert.True(map.Mirror is { } mirror && map.IsOpen(mirror));
        Assert.True(map.Fan is { } fan && map.IsOpen(fan));
        foreach (var home in Hand.Value.Homes)
            Assert.True(map.IsOpen(new Vector2(home.X, home.Y)), $"{home.Folk}'s home is in the rock");
    }
}
