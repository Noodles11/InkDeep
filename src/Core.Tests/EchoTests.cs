using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her light's echoes (docs/LIGHT-AND-GRAPH.md §14, PlaneEcho.cs).</summary>
public class EchoTests
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

    /// <summary>A way from her start open 6 m out and wide to either side of its far end.</summary>
    static Vector2 Line(PlaneWorld w)
    {
        var her = w.Player.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var side = new Vector2(-d.Y, d.X);
            bool open = true;
            for (float s = 0.5f; s <= 6f && open; s += 0.5f) open = w.Clear(her + d * s, 0.8f);
            var at = her + d * 4f;
            if (open && w.Clear(at + side * 2.2f, 0.6f) && w.LineOfSight(at, at + side * 2.2f) && w.LineOfSight(her, at)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    static Murkling Held(PlaneWorld w, Vector2 at, float meter)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = meter;
        w.Murklings.Add(m);
        return m;
    }

    /// <summary>A murkling freed by her beam 4 m out, with a neighbour beside it out of the beam; the events seen.</summary>
    static (PlaneWorld W, Murkling Target, Murkling Neighbour, int Flares) Free(float neighbourMeter, params string[] pearls)
    {
        var w = World(pearls);
        var d = Line(w);
        var at = w.Map.Start.Position + d * 4f;
        var beside = at + new Vector2(-d.Y, d.X) * 2.2f;
        var target = Held(w, at, 5f);
        var neighbour = Held(w, beside, neighbourMeter);
        int flares = 0;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && target.Alive; i++) Step(w, d, target, at, neighbour, beside);
        // A few steps more, for what follows.
        for (int i = 0; i < 3; i++)
        {
            flares += w.Events.Count(e => e.Type == PlaneEventType.EchoFlare);
            Step(w, d, target, at, neighbour, beside);
        }
        flares += w.Events.Count(e => e.Type == PlaneEventType.EchoFlare);
        return (w, target, neighbour, flares);
    }

    static void Step(PlaneWorld w, Vector2 d, Murkling a, Vector2 at, Murkling b, Vector2 beside)
    {
        foreach (var (m, p) in new[] { (a, at), (b, beside) })
        {
            m.Position = p;
            m.Velocity = Vector2.Zero;
            m.ThrowTimer = 100f;
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 50f;
        w.Step(new PlaneInput { Aim = d, Beam = true });
    }

    [Fact]
    public void Without_an_echo_pearl_nothing_echoes()
    {
        var (w, target, neighbour, flares) = Free(10000f);
        Assert.False(target.Alive);
        Assert.Equal(0, flares);
        Assert.Empty(w.Spines);
        Assert.Equal(10000f, neighbour.Corruption.Value, 1);
    }

    [Fact]
    public void Flare_bursts_a_ring_of_her_light_from_what_she_frees_in_her_colours()
    {
        var (_, target, neighbour, _) = Free(10000f, "ink_sac", "fire_coral");
        Assert.False(target.Alive);
        Assert.True(neighbour.Corruption.Value < 10000f, "the flare burned it");
    }

    [Fact]
    public void Flare_echoes_one_generation_deep()
    {
        // The neighbour, freed by the flare, does not flare again.
        var (_, target, neighbour, flares) = Free(1f, "ink_sac");
        Assert.False(target.Alive);
        Assert.False(neighbour.Alive);
        Assert.Equal(1, flares);
    }

    [Fact]
    public void Spines_shine_out_from_what_she_frees_for_a_moment()
    {
        var w = World("fire_urchin_spine");
        var d = Line(w);
        var at = w.Map.Start.Position + d * 4f;
        var target = Held(w, at, 5f);
        var far = Held(w, at + new Vector2(-d.Y, d.X) * 20f, 10000f);
        bool seen = false;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && !seen; i++)
        {
            Step(w, d, target, at, far, far.Position);
            seen = w.Spines.Count == EchoTuning.SpineCount;
        }
        Assert.True(seen);
        w.Step(new PlaneInput { Aim = d, Beam = false });
        Assert.Equal(EchoTuning.SpineCount, w.Born.Count(b => b.Word == PathWord.Spine));
        for (int i = 0; i < PlaneWorld.TickRate; i++) w.Step(new PlaneInput { Aim = d, Beam = false });
        Assert.Empty(w.Spines);
        Assert.DoesNotContain(w.Born, b => b.Word == PathWord.Spine);
    }

    [Fact]
    public void Gather_sends_its_core_flying_to_her_richer()
    {
        var (w, target, _, _) = Free(10000f, "lamprey_mouth");
        Assert.False(target.Alive);
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) w.Step(new PlaneInput());
        // Taken by now, worth its light and Gather's.
        Assert.Empty(w.Cores);
        Assert.Equal(1, w.Stats.CoresTaken);
    }

    [Fact]
    public void Supernova_ends_each_spine_in_a_flare()
    {
        var (w, target, _, flares) = Free(10000f, "ink_sac", "fire_urchin_spine");
        Assert.Contains("supernova", w.Run.Loadout.Synergies);
        Assert.False(target.Alive);
        Assert.Equal(1 + EchoTuning.SpineCount, flares);
    }
}
