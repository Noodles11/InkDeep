using System;
using System.IO;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// The crossover's light (PlaneOptions.Glow off, BeamBurn): no glow round her, her beam her light to see by (it shows
/// what a vault holds), burning creatures only a little; she is never hidden for having no glow; her shots still fly.
/// </summary>
public class NoGlowTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(bool glow, float beamBurn = 1f)
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Shots = true, Glow = glow, BeamBurn = beamBurn });
        w.Murklings.Clear();
        w.Fish.Clear();
        return w;
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(input);
    }

    [Fact]
    public void Without_her_glow_no_aura_shines_and_she_is_not_hidden_for_it()
    {
        var w = World(glow: false);
        Hold(w, w.Map.Start.Position, default);
        Assert.False(w.Aura.Shining);
        Assert.False(w.GlowOff);
        Assert.False(w.Hidden);
    }

    [Fact]
    public void Her_beam_shows_what_a_vault_holds_when_she_has_no_glow()
    {
        var w = World(glow: false);
        var at = w.Map.Start.Position;
        var vault = new PlaneVault { Position = at + new Vector2(3f, 0f), Wall = Vector2.UnitX, Radius = 0.8f };
        w.Vaults.Add(vault);
        Hold(w, at, default);
        Assert.False(w.Seen(vault));
        Hold(w, at, new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.True(w.Seen(vault));
    }

    [Fact]
    public void Her_bubble_shots_fly_when_shots_are_on()
    {
        var w = World(glow: false);
        var at = w.Map.Start.Position;
        for (int i = 0; i < 30 && w.Shots.Count == 0; i++) Hold(w, at, new PlaneInput { Fire = true, Aim = Vector2.UnitX });
        Assert.NotEmpty(w.Shots);
    }

    [Fact]
    public void A_light_to_see_by_burns_a_murkling_far_less_than_her_full_beam()
    {
        float Burned(float share)
        {
            var w = World(glow: false, beamBurn: share);
            var at = w.Map.Start.Position;
            var m = w.DebugBud(at + new Vector2(3f, 0f), prime: false);
            m.Bud = 0f;
            Hold(w, at, default);
            float before = m.Corruption.Value;
            for (int i = 0; i < 20; i++) Hold(w, at, new PlaneInput { Beam = true, Aim = Vector2.UnitX });
            return before - m.Corruption.Value;
        }
        float full = Burned(1f), light = Burned(HerLightTuning.LightOnlyBurn);
        Assert.True(full > 0f);
        Assert.True(light > 0f);
        Assert.True(light < full * 0.5f, $"light {light} vs full {full}");
    }
}
