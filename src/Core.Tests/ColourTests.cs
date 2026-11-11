using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her light's colours (docs/LIGHT-AND-GRAPH.md §10, PlaneColour.cs).</summary>
public class ColourTests
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

    static Vector2 Line(PlaneWorld w, float far)
    {
        var her = w.Player.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            bool open = true;
            for (float s = 0.5f; s <= far && open; s += 0.5f) open = w.Clear(her + d * s, 0.8f);
            if (open && w.LineOfSight(her, her + d * far)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    static Murkling Pinned(PlaneWorld w, Vector2 at, float meter)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = meter;
        w.Murklings.Add(m);
        return m;
    }

    static void Step(PlaneWorld w, Vector2 aim, bool beam, Murkling m, Vector2 at)
    {
        m.Position = at;
        m.Velocity = Vector2.Zero;
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(new PlaneInput { Aim = aim, Beam = beam });
    }

    static (PlaneWorld W, Vector2 D, Vector2 At, Murkling M) Lit(float meter, params string[] pearls)
    {
        var w = World(pearls);
        var d = Line(w, 6f);
        var at = w.Map.Start.Position + d * 4f;
        return (w, d, at, Pinned(w, at, meter));
    }

    [Fact]
    public void One_ray_mixes_her_colours_and_a_split_beam_deals_them_across_it()
    {
        var w = World("fire_coral", "frost_kelp");
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.Equal(LightHue.Magenta, w.RayHue(w.Beam));
        Assert.Equal(LightHue.Magenta, w.HerHue);

        var fan = World("triple_tentacle", "fire_coral", "frost_kelp");
        fan.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        // Left to right: ember, frost, ember.
        var rays = fan.BeamRays.OrderBy(r => MathF.Atan2(r.Direction.Y, r.Direction.X)).Select(fan.RayHue).ToList();
        Assert.Equal(new[] { LightHue.Red, LightHue.Blue, LightHue.Red }, rays);
    }

    [Fact]
    public void Ember_keeps_it_burning_after_the_light_has_left()
    {
        var (w, d, at, m) = Lit(10000f, "fire_coral");
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, d, true, m, at);
        Assert.Equal(LightHue.Red, w.ColourOn(m));
        float off = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, d, false, m, at);
        Assert.True(m.Corruption.Value < off - 1f, "it burns on in the dark");
        for (int i = 0; i < 3 * PlaneWorld.TickRate; i++) Step(w, d, false, m, at);
        float spent = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, d, false, m, at);
        Assert.Equal(spent, m.Corruption.Value, 3);
    }

    [Fact]
    public void Frost_slows_it_to_a_standstill()
    {
        var (w, d, at, m) = Lit(10000f, "frost_kelp");
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, d, true, m, at);
        Assert.InRange(w.PaceOf(m), 0.1f, 0.9f);
        for (int i = 0; i < (int)(1.2f * PlaneWorld.TickRate); i++) Step(w, d, true, m, at);
        Assert.Equal(1f, w.ChillOf(m), 3);
        Assert.True(w.Dazzled(m), "stilled");
    }

    [Fact]
    public void Ember_shatters_what_frost_has_stilled()
    {
        var (w, d, at, m) = Lit(10000f, "fire_coral", "frost_kelp");
        bool shattered = false;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && !shattered; i++)
        {
            int from = w.Events.Count;
            Step(w, d, true, m, at);
            shattered = w.Events.Any(e => e.Type == PlaneEventType.ColourShatter);
        }
        Assert.True(shattered);
        Assert.Equal(0f, w.ChillOf(m), 3);
    }

    [Fact]
    public void Bloom_and_frost_lull_it()
    {
        var (w, d, at, m) = Lit(10000f, "glowing_moss", "frost_kelp");
        Step(w, d, true, m, at);
        Assert.True(w.Charmed(m));
    }

    [Fact]
    public void Sunfire_bursts_from_what_it_frees_and_bloom_leaves_a_richer_core()
    {
        var (w, d, at, m) = Lit(5f, "fire_coral", "glowing_moss");
        bool burst = false;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && m.Alive; i++)
        {
            Step(w, d, true, m, at);
            burst |= w.Events.Any(e => e.Type == PlaneEventType.ColourSunfire);
        }
        Assert.False(m.Alive);
        Assert.True(burst);

        var (w2, d2, at2, m2) = Lit(5f, "glowing_moss");
        for (int i = 0; i < 3 * PlaneWorld.TickRate && m2.Alive; i++) Step(w2, d2, true, m2, at2);
        Assert.False(m2.Alive);
        Assert.Contains(w2.Cores, c => MathF.Abs(c.Light - CoreTuning.Light * ColourTuning.BloomCore) < 0.01f);
    }

    [Fact]
    public void White_light_burns_half_again_as_hard()
    {
        var (plain, d, at, m) = Lit(10000f);
        var (white, d2, at2, m2) = Lit(10000f, "fire_coral", "glowing_moss", "frost_kelp");
        for (int i = 0; i < PlaneWorld.TickRate / 4; i++)
        {
            Step(plain, d, true, m, at);
            Step(white, d2, true, m2, at2);
        }
        Assert.Equal(LightHue.White, white.ColourOn(m2));
        float a = 10000f - m.Corruption.Value, b = 10000f - m2.Corruption.Value;
        Assert.InRange(b / a, 1.4f, 1.6f);
    }
}
