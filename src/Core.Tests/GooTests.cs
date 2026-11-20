using System;
using System.IO;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The reef's soft edge (PlaneGoo.cs): she sinks a little way into the rock, slowed, and is pushed back out.</summary>
public class GooTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>A crossover level, and from her start the way to the nearest rock (and how far).</summary>
    static (PlaneWorld World, Vector2 Toward) AtAWall()
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, 1 + LevelPlan.Tutorial, 2), crossover: true);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, Glow = false });
        w.Murklings.Clear();
        Vector2 best = Vector2.UnitX;
        float near = float.MaxValue;
        for (int k = 0; k < 32; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 32f), MathF.Sin(k * MathF.Tau / 32f));
            float r = w.LineReach(map.Start.Position, d, 30f, out bool hit);
            if (hit && r < near) (near, best) = (r, d);
        }
        return (w, best);
    }

    static void Swim(PlaneWorld w, Vector2 move, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Move = move });
        }
    }

    [Fact]
    public void Swimming_into_the_rock_she_goes_past_its_line_a_little_and_stops()
    {
        var (w, toward) = AtAWall();
        Swim(w, toward, 60 * 5);
        float deep = w.Player.Goo;
        Assert.True(deep > 0.3f, $"only {deep} m into the rock's edge");
        Assert.True(deep <= GooTuning.Most + 0.05f, $"{deep} m into the rock, past its most");
        var held = w.Player.Position;
        Swim(w, toward, 60);
        Assert.True(Vector2.Distance(held, w.Player.Position) < 0.3f);
    }

    [Fact]
    public void Let_go_inside_it_she_is_pushed_back_out_to_the_water()
    {
        var (w, toward) = AtAWall();
        Swim(w, toward, 60 * 5);
        Assert.True(w.Player.Goo > 0.3f);
        Swim(w, Vector2.Zero, 60 * 3);
        Assert.True(w.Player.Goo < 0.1f, $"still {w.Player.Goo} m in");
    }

    [Fact]
    public void Sunk_in_the_edge_her_beam_shines_from_the_water_she_came_in_from()
    {
        var (w, toward) = AtAWall();
        Swim(w, toward, 60 * 5);
        Assert.True(w.Player.Goo > 0.3f);
        w.Step(new PlaneInput { Move = toward, Beam = true, Aim = -toward });
        Assert.True(w.Clear(w.Beam.Position, w.Radius * 0.9f), "her beam starts in the rock");
        Assert.True(Vector2.Distance(w.BeamEnd, w.Beam.Position) > 2f);
    }
}
