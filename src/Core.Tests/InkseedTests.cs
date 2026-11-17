using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Inkseeds (docs/CORRUPTION.md; PlaneInkseeds.cs): woken by her light, freed by her beam, or bursting into a stain no light shows through.</summary>
public class InkseedTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.Fish.Clear();
        return w;
    }

    /// <summary>Her start, and a way from it with open water at least this far along.</summary>
    static (Vector2 At, Vector2 Way) Open(PlaneWorld w, float far)
    {
        var at = w.Map.Start.Position;
        for (int k = 0; k < 72; k++)
        {
            var way = new Vector2(MathF.Cos(k * MathF.Tau / 72f), MathF.Sin(k * MathF.Tau / 72f));
            if (w.LineReach(at, way, far, out _) >= far && w.Clear(at + way * far, 1f)) return (at, way);
        }
        throw new InvalidOperationException("no open water by her start");
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Step(input);
    }

    [Fact]
    public void Her_glow_wakes_a_seed_and_unheeded_it_bursts_on_her()
    {
        var w = World();
        var (at, way) = Open(w, 10f);
        w.PlaceInkseed(default, at + way * 2.5f);
        var seed = w.Inkseeds[0];
        float hp = w.Player.Hp = w.Run.MaxHp;
        bool woke = false, burst = false;
        for (int i = 0; i < (int)(4f / PlaneWorld.Dt) && !burst; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
            woke |= w.Events.Any(e => e.Type == PlaneEventType.InkseedWoke);
            burst |= w.Events.Any(e => e.Type == PlaneEventType.InkseedBurst);
        }
        Assert.True(woke);
        Assert.True(burst);
        Assert.Empty(w.Inkseeds);
        Assert.Single(w.Stains);
        Assert.Equal(hp - InkseedTuning.Damage * (1f + w.Menace), w.Player.Hp, 3);
        // Thrown straight back from it.
        Assert.True(Vector2.Dot(w.Player.Velocity, -way) > 1f);
    }

    [Fact]
    public void Her_beam_held_on_it_frees_it_and_stops_it_swelling()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        w.PlaceInkseed(default, at + way * 9f);
        var seed = w.Inkseeds[0];
        bool freed = false;
        for (int i = 0; i < (int)(2.5f / PlaneWorld.Dt) && !freed; i++)
        {
            Hold(w, at, new PlaneInput { Beam = true, Aim = way });
            Assert.Equal(0f, seed.Swell);
            freed |= w.Events.Any(e => e.Type == PlaneEventType.InkseedFreed);
        }
        Assert.True(freed);
        Assert.Empty(w.Inkseeds);
        Assert.Empty(w.Stains);
    }

    [Fact]
    public void Her_beam_taken_off_sets_the_freeing_back_and_it_swells_meanwhile()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        w.PlaceInkseed(default, at + way * 9f);
        var seed = w.Inkseeds[0];
        for (int i = 0; i < (int)(1.5f / PlaneWorld.Dt); i++) Hold(w, at, new PlaneInput { Beam = true, Aim = way });
        Assert.True(seed.Armed);
        Assert.True(seed.Held > 1f);
        Hold(w, at, default);
        Assert.Equal(0f, seed.Held);
        Assert.True(seed.Swell > 0f);
    }

    [Fact]
    public void Within_a_stain_her_beam_neither_burns_nor_shows_a_murkling()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        var spot = at + way * 6f;
        w.Stains.Add(new InkStain { Position = spot, Age = 1f });
        var m = w.DebugBud(spot, prime: false);
        m.Bud = 0f;
        float meter = m.Corruption.Value;
        for (int i = 0; i < 20; i++)
        {
            Hold(w, at, new PlaneInput { Beam = true, Aim = way });
            m.Position = spot;
        }
        Assert.Equal(meter, m.Corruption.Value);
        Assert.False(w.InHerLight(m));
        // The stain gone, her beam burns it.
        w.Stains.Clear();
        for (int i = 0; i < 20; i++)
        {
            Hold(w, at, new PlaneInput { Beam = true, Aim = way });
            m.Position = spot;
        }
        Assert.True(m.Corruption.Value < meter);
    }

    [Fact]
    public void A_stain_spreads_to_its_reach_and_shrinks_away()
    {
        var s = new InkStain();
        Assert.Equal(0f, s.Radius);
        s.Age = InkseedTuning.Spread;
        Assert.Equal(InkseedTuning.Reach, s.Radius, 3);
        s.Age = InkseedTuning.Stain * 0.6f;
        Assert.InRange(s.Radius, 0.1f, InkseedTuning.Reach);
        s.Age = InkseedTuning.Stain;
        Assert.Equal(0f, s.Radius, 3);
    }
}
