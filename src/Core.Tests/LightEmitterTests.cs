using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The world's light registry: her aura, her shots, a plant's glow, and what drinking them costs (PlaneLight.cs).</summary>
public class LightEmitterTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        return new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions());
    }

    [Fact]
    public void The_registry_gives_steady_ids_and_forgets_what_is_removed()
    {
        var reg = new LightRegistry();
        var a = reg.Add(new LightEmitter { Owner = new LightPool(1f) });
        var b = reg.Add(new LightEmitter { Owner = new LightPool(1f) });
        Assert.NotEqual(a.Id, b.Id);
        Assert.Same(a, reg.Get(a.Id));
        int was = a.Id;
        Assert.True(reg.Remove(a));
        Assert.Null(reg.Get(was));
        Assert.Equal(0, a.Id);
        Assert.False(reg.Remove(a));
        var c = reg.Add(new LightEmitter { Owner = new LightPool(1f) });
        Assert.True(c.Id > b.Id);
        Assert.Equal(2, reg.All.Count);
    }

    [Fact]
    public void Her_aura_is_registered_and_follows_her()
    {
        var w = World();
        w.Step(new PlaneInput());
        Assert.Contains(w.Aura, w.Lights.All);
        Assert.Equal(w.Player.Position, w.Aura.Position);
        Assert.Equal(LightTuning.AuraReach, w.Aura.Range, 3);
        Assert.Same(w.Her, w.Aura.Owner);
        w.Player.Position += new Vector2(0.5f, 0f);
        w.Step(new PlaneInput());
        Assert.Equal(w.Player.Position, w.Aura.Position);
        Assert.True(w.LightAt(w.Player.Position) >= LightTuning.AuraIntensity * 0.9f);
    }

    [Fact]
    public void Her_shots_shine_while_they_fly_and_leave_the_registry_when_gone()
    {
        var w = World();
        var shot = new PlaneShot { FromPlayer = true, Damage = PlaneCombatTuning.ShotDamage, Life = 100f, Range = 100f, Hover = 100f, Position = w.Player.Position };
        w.Shots.Add(shot);
        w.Step(new PlaneInput());
        var e = w.LightOf(shot);
        Assert.NotNull(e);
        Assert.Contains(e, w.Lights.All);
        Assert.Equal(shot.Position, e!.Position);
        shot.Life = 0f;
        w.Step(new PlaneInput());
        Assert.Null(w.LightOf(shot));
        Assert.DoesNotContain(e, w.Lights.All);
    }

    [Fact]
    public void Two_emitters_of_hers_drain_one_pool()
    {
        var w = World();
        w.Step(new PlaneInput());
        var beam = w.Lights.Add(new LightEmitter { Owner = w.Her, Shape = LightShape.Cone, Position = w.Player.Position, Range = LightTuning.BeamReach });
        float hp = w.Player.Hp;
        w.DrinkLight(w.Aura, 3f);
        w.DrinkLight(beam, 2f);
        Assert.Equal(hp - 5f * w.HurtScale, w.Player.Hp, 3);
    }

    [Fact]
    public void A_plant_drunk_dry_goes_dark_and_is_passed_over()
    {
        var w = World();
        w.Step(new PlaneInput());
        var near = w.Player.Position + new Vector2(0.3f, 0f);
        var plant = w.Lights.Add(new LightEmitter { Owner = new LightPool(1f), Position = near, Intensity = 3f, Range = 6f });
        Assert.Same(plant, w.BrightestLight(near));
        Assert.Equal(1f, w.DrinkLight(plant, 2.5f), 4);
        Assert.False(plant.Shining);
        Assert.Equal(0f, w.DrinkLight(plant, 1f));
        // With the plant dark, her own aura is the brightest light there.
        Assert.Same(w.Aura, w.BrightestLight(near));
    }

    [Fact]
    public void A_drunk_shot_dims_and_goes_out()
    {
        var w = World();
        var shot = new PlaneShot { FromPlayer = true, Damage = PlaneCombatTuning.ShotDamage, Life = 100f, Range = 100f, Hover = 100f, Position = w.Player.Position };
        w.Shots.Add(shot);
        w.Step(new PlaneInput());
        var e = w.LightOf(shot)!;
        Assert.Equal(0.5f, w.DrinkLight(e, 0.5f), 4);
        Assert.Equal(0.5f, shot.Dim, 4);
        w.DrinkLight(e, 1f);
        Assert.Equal(0f, shot.Life);
        Assert.False(e.Shining);
    }

    [Fact]
    public void Many_kinds_of_light_live_together()
    {
        var w = World();
        for (int i = 0; i < 5; i++)
            w.Shots.Add(new PlaneShot { FromPlayer = true, Damage = PlaneCombatTuning.ShotDamage, Life = 100f, Range = 100f, Hover = 100f, Position = w.Player.Position });
        w.Lights.Add(new LightEmitter { Owner = new LightPool(5f), Position = w.Player.Position, Range = 4f });
        w.Step(new PlaneInput());
        // (Touching bubbles join, so count the shots that are left: one light each, besides her aura, her beam (registered, dark) and the plant.)
        int shots = w.Shots.Count(s => s.FromPlayer && s.Life > 0f);
        Assert.True(shots > 0);
        Assert.Equal(shots + 3, w.Lights.All.Count);
        Assert.All(w.Shots, s => Assert.NotNull(w.LightOf(s)));
        Assert.Equal(w.Lights.All.Count, w.Lights.All.Select(e => e.Id).Distinct().Count());
    }
}
