using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Light specks (PlaneSpecks.cs), growing with their energy (PlaneGrowth.cs), and the pearls left in play (the shot pearls).</summary>
public class GrowthTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, 1 + LevelPlan.Tutorial, 2), crossover: true);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Specks = true });
        w.Murklings.Clear();
        return w;
    }

    [Fact]
    public void Only_the_shot_pearls_are_offered()
    {
        Assert.All(PlaneRun.InPlay, id => Assert.Contains(id, PlaneRun.ShotPearls));
        Assert.DoesNotContain("fire_coral", PlaneRun.InPlay);
        Assert.DoesNotContain("coral_crown", PlaneRun.InPlay);
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (var place in new[] { "treasure", "shop", "secret", "shrine", "boss" })
            Assert.All(run.Offer(place, Array.Empty<string>()), id => Assert.Contains(id, PlaneRun.ShotPearls));
    }

    [Fact]
    public void Specks_lie_on_a_level_and_she_gathers_their_energy()
    {
        var w = World();
        Assert.True(w.Specks.Count >= SpeckTuning.Scatter, $"{w.Specks.Count} specks");
        Assert.All(w.Specks, s => Assert.True(Vector2.Distance(s.Position, w.Map.Start.Position) >= SpeckTuning.FromStart - 0.01f));
        var speck = w.Specks[0];
        float worth = speck.Value;
        w.Player.Position = w.Player.PrevPosition = speck.Position + new Vector2(0.3f, 0f);
        w.Step(default);
        Assert.Equal(worth, w.Run.Energy, 3);
        Assert.DoesNotContain(speck, w.Specks);
    }

    [Fact]
    public void A_murkling_drinks_a_speck_grows_stronger_and_lets_it_go_when_freed()
    {
        var w = World();
        w.Specks.Clear();
        var her = w.Map.Start.Position;
        // (A murkling and a speck a little way off her, in the open; her own light off.)
        Vector2 at = Vector2.Zero;
        for (int k = 0; k < 16 && at == Vector2.Zero; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
            if (w.LineReach(her, d, 14f, out _) >= 12f) at = her + d * 10f;
        }
        Assert.NotEqual(Vector2.Zero, at);
        var m = w.DebugBud(at, prime: false);
        m.Bud = 0f;
        float hardBefore = m.Corruption.Max;
        var speck = w.AddSpeck(at + new Vector2(1.5f, 0f), 1f, Vector2.Zero);
        for (int i = 0; i < 60 * 6 && !speck.Gone; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
        Assert.True(m.Specks > 0.5f, $"it drank {m.Specks}");
        Assert.True(m.Corruption.Max > hardBefore * 1.15f, $"corruption {m.Corruption.Max} from {hardBefore}");
        // Freed, what it drank is thrown out again as specks.
        int before = w.Specks.Count;
        m.Corruption.Value = 0.01f;
        for (int i = 0; i < 60 * 3 && m.Alive; i++)
        {
            w.Player.Position = w.Player.PrevPosition = m.Position - new Vector2(3f, 0f);
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        }
        Assert.False(m.Alive);
        Assert.True(w.Specks.Count > before, "it let go of no specks");
    }

    [Fact]
    public void At_a_spring_she_lays_her_energy_on_her_light_and_body_and_it_holds()
    {
        var w = World();
        w.Run.Energy = 10f;
        Assert.False(w.CanGrow);
        var spring = w.Springs.FirstOrDefault();
        Assert.NotNull(spring);
        for (int i = 0; i < 3; i++)
        {
            w.Player.Position = w.Player.PrevPosition = spring!.Position;
            w.Step(default);
        }
        Assert.True(w.CanGrow);
        float hp = w.Run.MaxHp, speed = w.Run.Loadout.Stats[Stat.Speed];
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        float power = w.HerLightPhysics.Power;
        Assert.False(w.Grow(new Dictionary<GrowthStat, float> { [GrowthStat.Power] = 11f }));
        Assert.True(w.Grow(new Dictionary<GrowthStat, float> { [GrowthStat.Power] = 5f, [GrowthStat.MaxHealth] = 2.5f, [GrowthStat.Speed] = 2f, [GrowthStat.Blue] = 0.5f }));
        Assert.Equal(0f, w.Run.Energy, 3);
        Assert.Equal(hp + 2.5f * GrowthTuning.MaxHealth, w.Run.MaxHp, 2);
        Assert.True(w.Run.Loadout.Stats[Stat.Speed] > speed);
        w.Player.Position = w.Player.PrevPosition = spring!.Position;
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.Equal(power * (1f + 5f * GrowthTuning.Power), w.HerLightPhysics.Power, 2);
        Assert.False(w.CanGrow);
    }
}
