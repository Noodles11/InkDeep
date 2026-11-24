using System;
using System.IO;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The level's danger (PlaneDanger.cs): murklings out of the dark, more the deeper and the longer she stays.</summary>
public class DangerTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(int shownDepth)
    {
        var id = new LevelId(1, shownDepth + LevelPlan.Tutorial, 2);
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        return new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
    }

    [Fact]
    public void The_clock_climbs_in_five_steps_to_its_most()
    {
        Assert.Equal(0, DangerTuning.StepAt(0f));
        Assert.Equal(1, DangerTuning.StepAt(DangerTuning.Seconds / DangerTuning.Steps + 0.1f));
        Assert.Equal(DangerTuning.Steps - 1, DangerTuning.StepAt(DangerTuning.Seconds * 3f));
    }

    [Fact]
    public void A_crossover_level_starts_with_murklings_in_its_dark_and_more_the_deeper()
    {
        var shallow = World(1);
        var deep = World(3);
        Assert.NotEmpty(shallow.Murklings);
        Assert.True(deep.Murklings.Count > shallow.Murklings.Count, $"{deep.Murklings.Count} deep, {shallow.Murklings.Count} shallow");
        Assert.All(shallow.Murklings, m => Assert.True(System.Numerics.Vector2.Distance(m.Position, shallow.Map.Start.Position) >= MurkRosterTuning.FromStart - 0.01f));
    }

    [Fact]
    public void The_longer_she_stays_the_higher_the_danger_and_the_more_murklings_come()
    {
        var w = World(1);
        int before = w.Murklings.Count;
        Assert.Equal(0, w.Danger);
        var at = w.Map.Start.Position;
        int steps = (int)(DangerTuning.Seconds * 0.8f / PlaneWorld.Dt);
        for (int i = 0; i < steps; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
        Assert.True(w.Danger >= 3, $"danger {w.Danger}");
        Assert.True(w.Murklings.Count > before, $"{w.Murklings.Count} after, {before} before");
    }

    [Fact]
    public void Without_her_glow_dark_and_quiet_she_is_unseen_but_her_beam_draws_them()
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, 1 + LevelPlan.Tutorial, 2), crossover: true);
        float Closed(bool beam)
        {
            var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, Glow = false });
            w.Murklings.Clear();
            // (No fish to hunt: only she could draw them.)
            w.Fish.Clear();
            w.ReefFish.Clear();
            var at = map.Start.Position;
            var near = new System.Collections.Generic.List<Murkling>();
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathF.Tau / 8f;
                var spot = at + new System.Numerics.Vector2(MathF.Cos(a), MathF.Sin(a)) * 7f;
                if (!w.Clear(spot, 0.6f)) continue;
                var m = w.DebugBud(spot, prime: false);
                m.Bud = 0f;
                // (The light-seekers: a thrower or a hunter keeps its own ways.)
                m.Kind = MurkKind.Basic;
                near.Add(m);
            }
            Assert.NotEmpty(near);
            var before = near.Select(m => System.Numerics.Vector2.Distance(m.Position, at)).ToList();
            for (int i = 0; i < 60 * 4; i++)
            {
                w.Player.Position = w.Player.PrevPosition = at;
                w.Player.Hp = w.Run.MaxHp;
                // (Her beam swept round her, so every murkling sees it.)
                float turn = i * 0.05f;
                w.Step(beam ? new PlaneInput { Beam = true, Aim = new System.Numerics.Vector2(MathF.Cos(turn), MathF.Sin(turn)) } : default);
            }
            return near.Select((m, i) => before[i] - System.Numerics.Vector2.Distance(m.Position, at)).Max();
        }
        float dark = Closed(beam: false), lit = Closed(beam: true);
        Assert.True(dark < 1.5f, $"dark and quiet, the nearest still closed {dark} m");
        Assert.True(lit > 2f, $"her beam on, the nearest closed only {lit} m");
    }
}
