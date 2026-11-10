using System;
using System.IO;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her beam as a fan of lines, each stopped by what it meets (PlaneWorld.LineReach); and light on a core.</summary>
public class LaserTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1)), new Tuning(),
            new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.ReefFish.Clear();
        w.Fish.Clear();
        return w;
    }

    /// <summary>A way from her start with open water 12 m along it and a little either side.</summary>
    static Vector2 Open(PlaneWorld w)
    {
        var her = w.Map.Start.Position;
        for (int i = 0; i < 72; i++)
        {
            float a = i * MathF.Tau / 72f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var side = new Vector2(-d.Y, d.X);
            bool ok = true;
            for (float s = 0.5f; s <= 12f && ok; s += 0.5f) ok = w.Clear(her + d * s, 0.5f) && w.Clear(her + d * s + side * 1.5f, 0.5f) && w.Clear(her + d * s - side * 1.5f, 0.5f);
            if (ok) return d;
        }
        throw new InvalidOperationException("no open way");
    }

    [Fact]
    public void A_line_stops_at_rock_found_close_and_runs_its_full_reach_through_water()
    {
        var w = World();
        var her = w.Map.Start.Position;
        var way = Open(w);
        Assert.Equal(12f, w.LineReach(her, way, 12f, out bool none), 3);
        Assert.False(none);
        // Every way round her: where a line is stopped, the rock is just past its end.
        for (int i = 0; i < 36; i++)
        {
            float a = i * MathF.Tau / 36f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            float len = w.LineReach(her, d, 30f, out bool hit);
            if (!hit) continue;
            Assert.True(w.Map.IsOpen(her + d * (len - 0.05f)));
            Assert.False(w.Map.IsOpen(her + d * (len + 0.06f)) && w.WeakRocks.Count == 0 && w.Map.IsOpen(her + d * (len + 0.12f)));
        }
    }

    [Fact]
    public void A_body_stops_the_lines_that_meet_it_and_the_others_go_on_past()
    {
        var w = World();
        var her = w.Map.Start.Position;
        var way = Open(w);
        var m = new Murkling { Position = her + way * 6f, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        w.Murklings.Add(m);
        w.Player.Position = w.Player.PrevPosition = her;
        w.Step(new PlaneInput { Aim = way });
        float through = w.LineReach(her, way, 12f, out bool hit);
        Assert.True(hit);
        Assert.InRange(through, 6f - m.Body * 0.85f - 0.05f, 6f);
        // A line just past its side is not in its way.
        var beside = Vector2.Normalize(m.Position + new Vector2(-way.Y, way.X) * (m.Body + 0.6f) - her);
        Assert.Equal(12f, w.LineReach(her, beside, 12f, out bool clear), 3);
        Assert.False(clear);
    }

    [Fact]
    public void Light_on_a_murklings_core_shows_it_in_her_glow_or_beam_and_not_behind_rock()
    {
        var w = World();
        var her = w.Map.Start.Position;
        var way = Open(w);
        w.Player.Position = w.Player.PrevPosition = her;
        w.Step(default);
        var near = new Murkling { Position = her + way * 2f, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        var far = new Murkling { Position = her + way * 10f, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        Assert.True(w.CoreLit(near, near.Position));
        Assert.False(w.CoreLit(far, far.Position));
        w.Step(new PlaneInput { Beam = true, Aim = way });
        Assert.True(w.CoreLit(far, far.Position), "her beam on its core");
        w.Step(new PlaneInput { Beam = true, Aim = -way });
        Assert.False(w.CoreLit(far, far.Position));
    }
}
