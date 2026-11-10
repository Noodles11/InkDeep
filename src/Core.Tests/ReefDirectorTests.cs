using System;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The reef director: a seeded schedule of world events, and the geysers it runs.</summary>
public class ReefDirectorTests
{
    static readonly Lazy<LevelMap> Level = new(TestLevels.OneTreasure);

    [Fact]
    public void TheReefActsWhetherSheIsThereOrNot()
    {
        // She idles at the start; the first spray still starts and ends on time.
        var w = new PlaneWorld(Level.Value, new Tuning(), null, new PlaneOptions { Geysers = true });
        w.Mobs.Clear();
        var first = w.Director.GeyserSchedule[0];
        bool started = false, ended = false;
        int ticks = (int)MathF.Ceiling((first.End + 0.5f) / PlaneWorld.Dt);
        for (int i = 0; i < ticks; i++)
        {
            w.Player.Hp = 100f;
            w.Step(default);
            started |= w.Events.Any(e => e.Type == PlaneEventType.GeyserFizz);
            ended |= w.Events.Any(e => e.Type == PlaneEventType.GeyserEnded);
        }
        Assert.True(started && ended);
    }

    [Fact]
    public void GeysersAreSeededOnOpenFloorAndSprayFiveToTenSecondsOnTheirOwnClocks()
    {
        var a = new ReefDirector(Level.Value);
        var b = new ReefDirector(Level.Value);
        Assert.InRange(a.Geysers.Count, 1, GeyserTuning.CountMax);
        Assert.Equal(a.Geysers, b.Geysers);
        foreach (var vent in a.Geysers)
        {
            Assert.True(Level.Value.IsOpen(vent));
            Assert.True(Vector2.Distance(vent, Level.Value.Start.Position) >= GeyserTuning.FromStart);
        }
        Assert.NotEmpty(a.GeyserSchedule);
        foreach (var e in a.GeyserSchedule)
        {
            Assert.Equal(ReefEventKind.GeyserSpray, e.Kind);
            Assert.InRange(e.Duration - GeyserTuning.Warn, GeyserTuning.SprayMin, GeyserTuning.SprayMax);
        }
        // Each vent's sprays come one after another, with a gap between.
        foreach (var g in a.GeyserSchedule.GroupBy(e => e.Geyser))
        {
            var list = g.OrderBy(e => e.Start).ToList();
            for (int i = 1; i < list.Count; i++) Assert.True(list[i].Start >= list[i - 1].End + GeyserTuning.GapMin - 1e-3f);
        }
    }

    [Fact]
    public void ASprayPushesEverythingNearItOutwardHardestAtItsBurst_TheFizzBeforeItDoesNot()
    {
        var e = new ReefEvent { Kind = ReefEventKind.GeyserSpray, Start = 10f, Duration = GeyserTuning.Warn + 6f };
        var vent = new Vector2(50f, 50f);
        var near = vent + new Vector2(1.5f, 0f);
        Assert.Equal(Vector2.Zero, Geysers.PushAt(vent, e, near, 10.5f));
        var burst = Geysers.PushAt(vent, e, near, 10f + GeyserTuning.Warn + 0.4f);
        var steady = Geysers.PushAt(vent, e, near, 10f + GeyserTuning.Warn + 4f);
        Assert.True(burst.X > steady.X && steady.X > 0f, $"{burst} then {steady}");
        Assert.Equal(0f, burst.Y, 3);
        Assert.Equal(Vector2.Zero, Geysers.PushAt(vent, e, vent + new Vector2(GeyserTuning.Reach + 0.5f, 0f), 10f + GeyserTuning.Warn + 1f));
    }

    [Fact]
    public void The_game_has_no_geysers()
    {
        Assert.False(PlaneOptions.Default.Geysers);
        var w = new PlaneWorld(Level.Value, new Tuning());
        Assert.Empty(w.Director.Geysers);
        Assert.Empty(w.Director.GeyserSchedule);
    }
}
