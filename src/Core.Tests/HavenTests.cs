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
