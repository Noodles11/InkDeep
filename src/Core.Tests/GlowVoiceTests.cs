using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her glow's second voice (docs/LIGHT-AND-GRAPH.md §15, PlaneFireflies.cs).</summary>
public class GlowVoiceTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1)), new Tuning(), run, War);
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
        w.ChargeStones.Clear();
        return w;
    }

    static void Step(PlaneWorld w, Murkling? m = null, Vector2 at = default)
    {
        if (m is not null)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
            m.ThrowTimer = 100f;
            m.Feeding = null;
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Beam = false });
    }

    static Murkling Held(PlaneWorld w, Vector2 at)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = 100000f;
        w.Murklings.Add(m);
        return m;
    }

    [Fact]
    public void Firefly_pearls_add_up_to_her_fireflies_orbiting_at_her_glow()
    {
        Assert.Empty(World().Fireflies);
        var w = World("firefly_squid", "bubble_gland", "anemone_pump");
        Step(w);
        Assert.Equal(8, w.Fireflies.Count);
        float orbit = w.Aura.Range * FireflyTuning.Orbit;
        Assert.Contains(w.Fireflies, f => MathF.Abs(Vector2.Distance(f.Emitter.Position, w.Player.Position) - orbit) < orbit * 0.15f);
        Assert.All(w.Fireflies, f => Assert.True(f.Emitter.Shining));
    }

    [Fact]
    public void A_firefly_reveals_and_burns_what_is_near_it_with_her_beam_off()
    {
        var w = World("firefly_squid");
        Step(w);
        var f = w.Fireflies.First(x => w.Map.IsOpen(x.Emitter.Position) && w.Clear(x.Emitter.Position, 0.6f));
        var at = f.Emitter.Position;
        var m = Held(w, at);
        for (int i = 0; i < PlaneWorld.TickRate / 4; i++)
        {
            // (Held where the firefly is now.)
            Step(w, m, f.Emitter.Position);
        }
        Assert.True(m.Corruption.Value < 100000f, "the firefly burns it");
        Assert.True(w.BeamSees(m, m.Position, m.Radius));
    }

    [Fact]
    public void A_drunk_firefly_goes_out_and_comes_back_quicker_with_Anemone_Pump()
    {
        foreach (bool pump in new[] { false, true })
        {
            var w = pump ? World("firefly_squid", "anemone_pump") : World("firefly_squid");
            Step(w);
            var f = w.Fireflies[0];
            Assert.True(w.DrinkLight(f.Emitter, 0.1f) > 0f);
            Assert.False(f.Lit);
            Assert.False(f.Emitter.Shining);
            Assert.Equal(FireflyTuning.Return * (pump ? 0.5f : 1f), f.Out, 2);
            for (int i = 0; i < (int)(FireflyTuning.Return * (pump ? 0.5f : 1f) * PlaneWorld.TickRate) + 2; i++) Step(w);
            Assert.True(f.Lit);
        }
    }

    [Fact]
    public void Her_glow_takes_her_colours_an_ember_glow_leaves_what_brushes_it_burning()
    {
        foreach (bool ember in new[] { false, true })
        {
            var w = ember ? World("fire_coral") : World();
            var her = w.Map.Start.Position;
            Vector2 at = default;
            for (int i = 0; i < 16; i++)
            {
                var d = new Vector2(MathF.Cos(i * MathF.Tau / 16f), MathF.Sin(i * MathF.Tau / 16f));
                if (w.Clear(her + d * 1.6f, 0.6f) && w.LineOfSight(her, her + d * 1.6f)) { at = her + d * 1.6f; break; }
            }
            Assert.NotEqual(default, at);
            var m = Held(w, at);
            for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, m, at);
            if (ember)
            {
                Assert.Equal(LightHue.Red, w.ColourOn(m));
                Assert.True(m.Corruption.Value < 100000f);
            }
            else
            {
                Assert.Equal(LightHue.None, w.ColourOn(m));
                Assert.Equal(100000f, m.Corruption.Value, 1);
            }
        }
    }
}
