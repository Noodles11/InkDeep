using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her light's paths in the open (docs/LIGHT-AND-GRAPH.md §13, PlaneOpenPaths.cs).</summary>
public class OpenPathTests
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
            for (float s = 0.5f; s <= far && open; s += 0.5f) open = w.Clear(her + d * s, 1.6f);
            if (open && w.LineOfSight(her, her + d * far)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    static Vector2 Turn(Vector2 v, float deg)
    {
        float a = deg * MathF.PI / 180f;
        return new Vector2(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));
    }

    static Murkling Held(PlaneWorld w, Vector2 at, float meter = 100000f)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = meter;
        w.Murklings.Add(m);
        return m;
    }

    static void Step(PlaneWorld w, Vector2 aim, bool beam, Murkling? m = null, Vector2 at = default)
    {
        if (m is not null)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
            m.ThrowTimer = 100f;
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(new PlaneInput { Aim = aim, Beam = beam });
    }

    static float Angle(Vector2 a, Vector2 b) => MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)), -1f, 1f)) * 180f / MathF.PI;

    [Fact]
    public void Lean_turns_her_beam_toward_the_nearest_creature()
    {
        foreach (bool lure in new[] { true, false })
        {
            var w = lure ? World("anglerfish_lure") : World();
            var d = Line(w, 6f);
            var at = w.Map.Start.Position + Turn(d, 14f) * 4f;
            var m = Held(w, at);
            for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, d, true, m, at);
            float off = Angle(w.Beam.Direction, d);
            if (lure) Assert.InRange(off, OpenPathTuning.LeanDeg - 0.5f, OpenPathTuning.LeanDeg + 0.5f);
            else Assert.Equal(0f, off, 1);
        }
    }

    [Fact]
    public void Flare_widens_her_beam_toward_its_tip()
    {
        var plain = World();
        var flared = World("starfish_arm");
        var d = Line(plain, 6f);
        Step(plain, d, true);
        Step(flared, d, true);
        Assert.Equal(plain.Beam.HalfAngle, flared.Beam.HalfAngle, 4);
        Assert.Equal(plain.Beam.HalfAngle * (1f + OpenPathTuning.FlareWide), flared.Beam.HalfAt(flared.Beam.Range), 4);
        // Just outside the plain cone, far along it.
        float far = MathF.Min(5.5f, plain.Beam.Range * 0.8f);
        var point = flared.Beam.Position + Turn(d, plain.Beam.HalfAngle * 180f / MathF.PI * 1.15f) * far;
        Assert.False(plain.BeamReaches(point, 0f, 0.01f));
        Assert.True(flared.BeamReaches(point, 0f, 0.01f));
    }

    [Fact]
    public void Linger_leaves_pools_that_burn_on_then_fade()
    {
        var w = World("bubble_coral");
        var d = Line(w, 6f);
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, Turn(d, i * 0.6f), true);
        Assert.NotEmpty(w.Pools);
        // A creature set in a pool, her beam off: it burns on.
        var pool = w.Pools[^1];
        var m = Held(w, pool.At);
        float before = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, -d, false, m, pool.At);
        Assert.True(m.Corruption.Value < before, "it burns on in the pool");
        Assert.True(w.BeamSees(m, m.Position, m.Radius));
        for (int i = 0; i < 3 * PlaneWorld.TickRate; i++) Step(w, -d, false);
        Assert.Empty(w.Pools);
    }

    [Fact]
    public void Aurora_is_linger_and_any_two_colours_and_its_pools_hang_longer()
    {
        Assert.DoesNotContain("aurora", World("bubble_coral", "fire_coral").Run.Loadout.Synergies);
        var w = World("bubble_coral", "fire_coral", "frost_kelp");
        Assert.Contains("aurora", w.Run.Loadout.Synergies);
        Step(w, Line(w, 6f), true);
        Assert.Equal(OpenPathTuning.PoolLife * OpenPathTuning.AuroraLife, w.Pools[^1].Life, 3);
        Assert.NotEqual(LightHue.None, w.Pools[^1].Hue);
    }

    [Fact]
    public void Will_o_Wisp_leans_twice_as_far()
    {
        var w = World("anglerfish_lure", "ghost_jelly");
        Assert.Contains("will_o_wisp", w.Run.Loadout.Synergies);
        var d = Line(w, 6f);
        var at = w.Map.Start.Position + Turn(d, 24f) * 4f;
        var m = Held(w, at);
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, d, true, m, at);
        Assert.InRange(Angle(w.Beam.Direction, d), 2 * OpenPathTuning.LeanDeg - 0.5f, 2 * OpenPathTuning.LeanDeg + 0.5f);
    }

    [Fact]
    public void Electric_Eel_Tail_is_offered_now()
    {
        Assert.Contains("electric_eel_tail", PlaneRun.PortedPearls);
        Assert.Contains("pinball_storm", World("electric_eel_tail", "mirror_scale").Run.Loadout.Synergies);
    }
}
