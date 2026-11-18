using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Inkseeds (docs/CORRUPTION.md; PlaneInkseeds.cs): set off by her near or by her light, defused by her light kept on them, or going off into a stain no light shows through.</summary>
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
    public void Her_near_sets_it_off_and_it_goes_off_a_moment_later_on_her()
    {
        var w = World();
        var (at, way) = Open(w, 10f);
        w.PlaceInkseed(default, at + way * 2f);
        float hp = w.Player.Hp = w.Run.MaxHp;
        int shells = w.Shells.Count;
        float setOff = -1f, wentOff = -1f;
        for (int i = 0; i < (int)(2f / PlaneWorld.Dt) && wentOff < 0f; i++)
        {
            Hold(w, at, default);
            if (setOff < 0f && w.Events.Any(e => e.Type == PlaneEventType.InkseedWoke)) setOff = i * PlaneWorld.Dt;
            if (w.Events.Any(e => e.Type == PlaneEventType.InkseedBurst)) wentOff = i * PlaneWorld.Dt;
        }
        Assert.True(setOff >= 0f);
        Assert.InRange(wentOff - setOff, InkseedTuning.Fuse - 2f * PlaneWorld.Dt, InkseedTuning.Fuse + 2f * PlaneWorld.Dt);
        Assert.Empty(w.Inkseeds);
        Assert.Single(w.Stains);
        Assert.Equal(shells, w.Shells.Count);
        Assert.Equal(hp - InkseedTuning.Damage * (1f + w.Menace), w.Player.Hp, 3);
        // Thrown straight back from it.
        Assert.True(Vector2.Dot(w.Player.Velocity, -way) > 1f);
    }

    [Fact]
    public void Her_light_kept_on_it_defuses_it_and_it_drops_a_shell()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        w.PlaceInkseed(default, at + way * 9f);
        var seed = w.Inkseeds[0];
        int shells = w.Shells.Count;
        bool defused = false;
        for (int i = 0; i < (int)((InkseedTuning.Defuse + 0.5f) / PlaneWorld.Dt) && !defused; i++)
        {
            Hold(w, at, new PlaneInput { Beam = true, Aim = way });
            defused |= w.Events.Any(e => e.Type == PlaneEventType.InkseedFreed);
            Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.InkseedBurst);
        }
        Assert.True(defused);
        Assert.Empty(w.Inkseeds);
        Assert.Empty(w.Stains);
        Assert.Equal(shells + InkseedTuning.Shells, w.Shells.Count);
    }

    [Fact]
    public void Set_off_by_her_light_it_goes_off_once_dark_again_and_drops_nothing()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        w.PlaceInkseed(default, at + way * 9f);
        var seed = w.Inkseeds[0];
        int shells = w.Shells.Count;
        for (int i = 0; i < (int)(1.5f / PlaneWorld.Dt); i++) Hold(w, at, new PlaneInput { Beam = true, Aim = way });
        Assert.True(seed.Armed);
        Assert.True(seed.Held > 1f);
        Assert.Single(w.Inkseeds);
        // Her beam turned away: dark again, it goes off at once.
        Hold(w, at, new PlaneInput { Beam = true, Aim = -way });
        Hold(w, at, new PlaneInput { Beam = true, Aim = -way });
        Assert.Empty(w.Inkseeds);
        Assert.Single(w.Stains);
        Assert.Equal(shells, w.Shells.Count);
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
