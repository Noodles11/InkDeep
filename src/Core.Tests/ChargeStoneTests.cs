using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>the charge stones in the walls (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.5; PlaneChargeStones.cs).</summary>
public class ChargeStoneTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.Fish.Clear();
        w.ReefFish.Clear();
        return w;
    }

    /// <summary>A charge stone with a spot in open water a few metres before it, in sight of it, for her to shine from.</summary>
    static (ChargeStone Crystal, Vector2 From) Target(PlaneWorld w)
    {
        foreach (var c in w.ChargeStones)
            for (float d = 3f; d <= 5f; d += 1f)
            {
                var at = c.Position + c.Out * d;
                if (w.Clear(at, 0.8f) && w.LineOfSight(at, c.Position)) return (c, at);
            }
        throw new InvalidOperationException("no charge stone to shine on");
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(input);
    }

    [Fact]
    public void Charge_stones_are_set_in_the_walls_apart_each_its_own_colour_dark_at_first()
    {
        var w = World();
        Assert.InRange(w.ChargeStones.Count, 6, ChargeStoneTuning.Count);
        foreach (var c in w.ChargeStones)
        {
            Assert.True(w.Map.IsOpen(c.Position));
            Assert.True(w.Map.HeightAt(c.Position - c.Out * 1.1f) > LevelMap.SwimBand + 0.4f, "a wall behind it");
            Assert.Equal(0f, c.Charge);
            Assert.False(c.Light.Shining);
            Assert.Contains(c.Light, w.Lights.All);
        }
        // (A trio's three stand together round their nook.)
        Assert.All(w.ChargeStones, c => Assert.DoesNotContain(w.ChargeStones, o => o != c && !(o.Trio >= 0 && c.Trio >= 0) && Vector2.Distance(o.Position, c.Position) < ChargeStoneTuning.Apart));
        Assert.True(w.ChargeStones.Select(c => c.Colour).Distinct().Count() > 2);
    }

    [Fact]
    public void Her_beam_charges_one_it_glows_on_full_then_fades_out()
    {
        var w = World();
        var (c, from) = Target(w);
        var way = Vector2.Normalize(c.Position - from);
        for (int i = 0; i < PlaneWorld.TickRate; i++) Hold(w, from, new PlaneInput { Beam = true, Aim = way });
        Assert.True(c.Charge > ChargeStoneTuning.FadeSeconds);
        Assert.Equal(1f, c.Glow);
        Assert.True(c.Light.Shining);
        Assert.Equal(ChargeStoneTuning.Bright, c.Light.Intensity, 3);
        Assert.True(c.Light.Intensity < w.Aura.Intensity, "fainter than her glow");
        // Left alone: full a while, then dimming, then out.
        float held = c.Charge;
        for (int i = 0; i < (int)((held - ChargeStoneTuning.FadeSeconds - 0.5f) * PlaneWorld.TickRate); i++) Hold(w, from, default);
        Assert.Equal(1f, c.Glow);
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++) Hold(w, from, default);
        Assert.InRange(c.Glow, 0.05f, 0.95f);
        for (int i = 0; i < (int)(ChargeStoneTuning.FadeSeconds * PlaneWorld.TickRate); i++) Hold(w, from, default);
        Assert.Equal(0f, c.Charge);
        Assert.False(c.Light.Shining);
    }

    [Fact]
    public void A_lit_charge_stone_draws_a_murkling_from_her_and_harms_nothing()
    {
        var w = World();
        var (c, from) = Target(w);
        c.Charge = ChargeStoneTuning.MaxGlow;
        // Her glow out: the charge stone is the light it sees.
        Hold(w, from, new PlaneInput { ToggleGlow = true });
        var near = c.Position + c.Out * 1.6f;
        var m = new Murkling { Position = near, Bud = 0f };
        float full = m.Corruption.Value;
        w.Murklings.Add(m);
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++) Hold(w, from, default);
        // (Gone to it, or already feeding on it.)
        Assert.Same(c.Light, m.Feeding ?? m.Light);
        Assert.Equal(full, m.Corruption.Value);
        // It drinks the charge stone's glow away.
        for (int i = 0; i < 3 * PlaneWorld.TickRate; i++) Hold(w, from, default);
        Assert.True(c.Charge < ChargeStoneTuning.MaxGlow - 3f);
    }

    [Fact]
    public void A_lit_charge_stone_shows_the_murklings_in_its_light_as_her_glow_does()
    {
        var w = World();
        var (c, _) = Target(w);
        var near = c.Position + c.Out * 1.5f;
        var far = c.Position + c.Out * (ChargeStoneTuning.ShownReach + 2f);
        Assert.Equal(0f, w.ChargeStoneSees(near, 0.4f));
        c.Charge = ChargeStoneTuning.MaxGlow;
        Assert.Equal(1f, w.ChargeStoneSees(near, 0.4f));
        Assert.Equal(0f, w.ChargeStoneSees(far, 0.4f));
        // Dimming, it shows less.
        c.Charge = ChargeStoneTuning.FadeSeconds * 0.5f;
        Assert.InRange(w.ChargeStoneSees(near, 0.4f), 0.4f, 0.6f);
    }
}
