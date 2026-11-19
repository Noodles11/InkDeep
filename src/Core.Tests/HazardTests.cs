using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The biomes' hazards (PlaneHazards.cs): the Kelp Forest's tangles, the Shipwreck's debris, the Ruins' dart traps.</summary>
public class HazardTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(int shownDepth, int branch, int level = 2)
    {
        var id = new LevelId(1, shownDepth + LevelPlan.Tutorial, level) { Branch = branch };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.Fish.Clear();
        w.Mobs.Clear();
        return w;
    }

    [Fact]
    public void A_kelp_bed_s_tangle_slows_her_in_the_Kelp_Forest()
    {
        var w = World(2, 0);
        Assert.Equal(Biome.KelpForest, w.Map.Biome);
        var bed = w.Map.Kelp.OrderByDescending(b => b.Radius).First();
        float Pace(Vector2 at)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Velocity = Vector2.Zero;
            for (int i = 0; i < 40; i++)
            {
                w.Player.Position = w.Player.PrevPosition = at;
                w.Step(new PlaneInput { Move = Vector2.UnitX });
            }
            return w.Player.Velocity.Length();
        }
        float tangled = Pace(bed.Center);
        Assert.True(w.Tangled);
        float free = Pace(w.Map.Start.Position);
        Assert.False(w.Tangled);
        Assert.True(tangled < free * 0.8f, $"tangled {tangled} free {free}");
    }

    [Fact]
    public void Debris_falls_where_she_is_heading_in_the_Shipwreck_and_hurts_her_under_it()
    {
        var w = World(2, 1);
        Assert.Equal(Biome.Shipwreck, w.Map.Biome);
        var at = w.Map.Start.Position;
        PlaneDebris? fell = null;
        for (int i = 0; i < 60 * 10 && fell is null; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            fell = w.Debris.FirstOrDefault();
        }
        Assert.NotNull(fell);
        float before = w.Player.Hp;
        for (int i = 0; i < 120 && !fell!.Landed; i++)
        {
            w.Player.Position = w.Player.PrevPosition = fell.Position;
            w.Step(default);
        }
        Assert.True(fell!.Landed);
        Assert.True(w.Player.Hp < before);
    }

    [Fact]
    public void A_dart_trap_in_the_Ruins_fires_down_its_line_when_she_crosses_it()
    {
        var w = World(3, 0);
        Assert.Equal(Biome.SunkenRuins, w.Map.Biome);
        Assert.NotEmpty(w.DartTraps);
        Assert.Empty(World(2, 0).DartTraps);
        var trap = w.DartTraps[0];
        trap.Rest = 0f;
        var at = trap.Position + trap.Facing * 4f;
        float before = w.Run.MaxHp;
        bool fired = false;
        for (int i = 0; i < 90; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Step(default);
            fired |= w.Events.Exists(e => e.Type == PlaneEventType.DartFired);
        }
        Assert.True(fired);
        Assert.True(w.Player.Hp < before);
    }

    [Fact]
    public void The_Shipwreck_s_cannons_fire_at_a_creature_crossing_their_line_and_the_ball_strikes_it()
    {
        var w = World(2, 1);
        Assert.NotEmpty(w.DartTraps);
        Assert.All(w.DartTraps, t => Assert.True(t.Cannon));
        var gun = w.DartTraps[0];
        gun.Rest = 0f;
        // (She is far off: only the creature crosses its line.)
        var spot = gun.Position + gun.Facing * 6f;
        var mob = new PlaneMob { Position = spot, Home = spot, Rng = new Rng(1), FireTimer = 99f, Hp = 1000f };
        w.Mobs.Add(mob);
        float hp = mob.Hp;
        bool fired = false;
        for (int i = 0; i < 180 && mob.Hp >= hp; i++)
        {
            mob.Position = spot;
            mob.Velocity = Vector2.Zero;
            w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
            w.Step(default);
            fired |= w.Events.Exists(e => e.Type == PlaneEventType.DartFired);
        }
        Assert.True(fired);
        Assert.True(mob.Hp < hp);
    }

    [Fact]
    public void A_kelp_curtain_stops_her_till_her_beam_parts_it_and_it_closes_again()
    {
        PlaneWorld? w = null;
        for (int level = 1; level <= 4 && (w is null || w.Curtains.Count == 0); level++) w = World(2, 0, level);
        Assert.NotEmpty(w!.Curtains);
        var c = w.Curtains[0];
        Assert.False(w.Clear(c.Rock.Center, 0.3f));
        var at = c.Rock.Center - c.Rock.Facing * 3f;
        for (int i = 0; i < 90 && !c.Open; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Beam = true, Aim = c.Rock.Facing });
        }
        Assert.True(c.Open);
        Assert.True(w.Clear(c.Rock.Center, 0.3f));
        for (int i = 0; i < 60 * 5 && c.Open; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at - c.Rock.Facing * 3f;
            w.Step(default);
        }
        Assert.False(c.Open);
        Assert.False(w.Clear(c.Rock.Center, 0.3f));
    }

    [Fact]
    public void The_Ruins_have_amphorae_to_break_and_a_light_locked_door()
    {
        var w = World(3, 0);
        Assert.NotEmpty(w.Vases);
        Assert.Empty(World(2, 1).Vases);
        bool door = false;
        for (int level = 1; level <= 4 && !door; level++) door = World(3, 0, level).Seals.Exists(x => x.Kind == SealKind.Path);
        Assert.True(door);
    }
}
