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

    static PlaneWorld World(int shownDepth, int branch)
    {
        var id = new LevelId(1, shownDepth + LevelPlan.Tutorial, 2) { Branch = branch };
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
}
