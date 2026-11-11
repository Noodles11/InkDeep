using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Each folk boss's rule for her beam (docs/LIGHT-AND-GRAPH.md §5.2; PlaneBossLight.cs).</summary>
public class BossLightTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly SeedCode Seed = SeedCode.Parse("KELP7Q2Z");

    /// <summary>A neighbour's depth's boss level, its fight begun with her at <paramref name="her"/> (from the arena's middle).</summary>
    static PlaneWorld Fight(string folk, Vector2 her)
    {
        int depth = Array.FindIndex(Folk.All, n => n.Id == folk) + 1;
        var n = Folk.Get(folk)!;
        var map = TopDownGenerator.Generate(new RunStreams(Seed), new LevelId(1, LevelPlan.Tutorial + depth, LevelStocking.LevelsPerDepth));
        var options = new PlaneOptions { Adventure = true, Boss = FolkBosses.For(folk, n.Level, map.Menace) };
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), options);
        for (int i = 0; i < 3 * PlaneWorld.TickRate && w.Boss!.Stage != BossStage.Fight; i++) Step(w, w.ArenaCenter + her, default, null);
        Assert.Equal(BossStage.Fight, w.Boss!.Stage);
        return w;
    }

    /// <summary>A step with her held at <paramref name="at"/>, full of light, the boss held in <paramref name="act"/> (null: let it be).</summary>
    static void Step(PlaneWorld w, Vector2 at, PlaneInput input, BossAct? act, Vector2? facing = null)
    {
        var boss = w.Boss!;
        if (act is { } a)
        {
            boss.Act = a;
            boss.ActTime = 0f;
            boss.RiseTo = null;
        }
        if (facing is { } f) boss.Facing = f;
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(input);
    }

    static PlaneInput BeamAt(PlaneWorld w, Vector2 target) =>
        new() { Beam = true, Aim = Vector2.Normalize(target - w.Player.Position) };

    [Fact]
    public void Tull_burns_only_once_the_beam_has_held_steady_on_her_pearl()
    {
        var w = Fight("tull", new Vector2(0f, 7f));
        var boss = w.Boss!;
        var her = w.ArenaCenter + new Vector2(0f, 7f);
        float hp = boss.Hp;
        for (int i = 0; i < (int)(BossLightTuning.HoldSeconds * PlaneWorld.TickRate) - 3; i++)
            Step(w, her, BeamAt(w, boss.PearlAt), BossAct.Window, Vector2.UnitY);
        Assert.Equal(hp, boss.Hp);
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, her, BeamAt(w, boss.PearlAt), BossAct.Window, Vector2.UnitY);
        Assert.True(boss.Hp < hp);
        // Taken off it a moment, the hold starts again.
        Step(w, her, default, BossAct.Window, Vector2.UnitY);
        hp = boss.Hp;
        for (int i = 0; i < (int)(BossLightTuning.HoldSeconds * PlaneWorld.TickRate) - 3; i++)
            Step(w, her, BeamAt(w, boss.PearlAt), BossAct.Window, Vector2.UnitY);
        Assert.Equal(hp, boss.Hp);
    }

    [Fact]
    public void Kip_burns_only_with_the_beam_meeting_her_flank()
    {
        var w = Fight("kip", new Vector2(0f, 7f));
        var boss = w.Boss!;
        var her = w.ArenaCenter + new Vector2(0f, 7f);
        // Head-on to her: nothing.
        float hp = boss.Hp;
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, her, BeamAt(w, boss.PearlAt), BossAct.Window, Vector2.UnitY);
        Assert.Equal(hp, boss.Hp);
        // Side-on: it burns.
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, her, BeamAt(w, boss.PearlAt), BossAct.Window, Vector2.UnitX);
        Assert.True(boss.Hp < hp);
    }

    [Fact]
    public void Fennel_is_found_by_the_beam_falling_on_him_in_his_kelp()
    {
        var w = Fight("fennel", Vector2.Zero);
        var boss = w.Boss!;
        for (int i = 0; i < 4 * PlaneWorld.TickRate && !boss.Hidden; i++) Step(w, w.ArenaCenter, default, null);
        Assert.True(boss.Hidden);
        // From well out of his finding reach, the beam on him.
        var way = Vector2.Normalize(w.ArenaCenter - boss.Position);
        var her = boss.Position + way * (PlaneBossTuning.FoundReach + boss.Radius + 3f);
        Step(w, her, BeamAt(w, boss.Position), null);
        Step(w, her, BeamAt(w, boss.Position), null);
        Assert.False(boss.Hidden);
        Assert.True(boss.Found);
    }

    [Fact]
    public void Ottilie_turns_to_admire_a_lamp_shell_the_beam_has_lit()
    {
        var w = Fight("ottilie", new Vector2(0f, 7f));
        var boss = w.Boss!;
        Assert.Equal(2, boss.Lamps.Count);
        Assert.All(boss.Lamps, l => Assert.Contains(l.Light, w.Lights.All));
        var lamp = boss.Lamps[0];
        var her = w.ArenaCenter + new Vector2(0f, 7f);
        for (int i = 0; i < 3; i++) Step(w, her, BeamAt(w, lamp.Position), BossAct.Rest);
        Assert.True(lamp.Lit);
        Assert.Same(lamp, boss.Admiring);
        Assert.True(lamp.Light.Intensity > w.Aura.Intensity);
        // She turns to it (and keeps it lit a while after the beam leaves it).
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++) Step(w, her, default, BossAct.Rest);
        Assert.True(Vector2.Dot(boss.Facing, Vector2.Normalize(lamp.Position - boss.Position)) > 0.9f);
        for (int i = 0; i < (int)(BossLightTuning.LampGlow * PlaneWorld.TickRate); i++) Step(w, her, default, BossAct.Rest);
        Assert.False(lamp.Lit);
        Assert.Null(boss.Admiring);
    }

    [Fact]
    public void The_Dragonets_sacs_burn_only_under_the_heart_of_the_beam()
    {
        var w = Fight("bubbles", new Vector2(0f, 8f));
        var boss = w.Boss!;
        var her = w.ArenaCenter + new Vector2(0f, 8f);
        var sac = boss.Sacs.First(s => s.Alive);
        float hp = sac.Hp;
        // Its edge on the sac: nothing.
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            var to = Vector2.Normalize(boss.SacAt(sac) - her);
            float off = w.Beam.HalfAngle * 0.8f;
            var aim = new Vector2(to.X * MathF.Cos(off) - to.Y * MathF.Sin(off), to.X * MathF.Sin(off) + to.Y * MathF.Cos(off));
            Step(w, her, new PlaneInput { Beam = true, Aim = aim }, BossAct.Rest);
        }
        Assert.Equal(hp, sac.Hp);
        // Its heart on it: it burns.
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, her, BeamAt(w, boss.SacAt(sac)), BossAct.Rest);
        Assert.True(sac.Hp < hp);
    }
}
