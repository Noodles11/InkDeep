using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The basic murkling: it seeks the brightest light, drinks it, and is shaken off (docs/LIGHT-AND-GRAPH.md §3.1).</summary>
public class SeekerTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    static PlaneWorld World(Func<LevelPlan, bool> match)
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(match)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), War);
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
        return w;
    }

    static PlaneWorld World() => World(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1);

    /// <summary>A way from her with open water all along it out to <paramref name="far"/> metres, in plain sight.</summary>
    static Vector2 Way(PlaneWorld w, float far)
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
        throw new InvalidOperationException("no open way");
    }

    static Murkling Seeker(PlaneWorld w, Vector2 at)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 0f };
        w.Murklings.Add(m);
        return m;
    }

    static LightEmitter Plant(PlaneWorld w, Vector2 at, float intensity = 3f, float energy = 100f) =>
        w.Lights.Add(new LightEmitter { Owner = new LightPool(energy), Position = at, Intensity = intensity, Range = 8f });

    /// <summary>Steps with her held where she is, full of light (her beam held along <paramref name="beam"/>, if given).</summary>
    static void Run(PlaneWorld w, float seconds, Vector2? beam = null, bool fullHp = true)
    {
        var her = w.Player.Position;
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            if (fullHp) w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = beam ?? Vector2.UnitX, Beam = beam is not null });
        }
    }

    [Fact]
    public void In_the_dark_it_holds_still_and_waits()
    {
        var w = World();
        var d = Way(w, 13f);
        var at = w.Player.Position + d * 12f;
        var m = Seeker(w, at);
        Run(w, 1.5f);
        Assert.Equal(MurkState.Searching, m.State);
        Assert.True(Vector2.Distance(m.Position, at) < 0.2f);
    }

    [Fact]
    public void Near_her_it_swims_to_her_aura_and_clings_draining_without_a_hit()
    {
        var w = World();
        var d = Way(w, 4f);
        var m = Seeker(w, w.Player.Position + d * 3f);
        Run(w, 0.3f);
        Assert.Equal(MurkState.Approaching, m.State);
        Assert.Same(w.Her, m.Light!.Owner);
        for (int i = 0; i < 5 * PlaneWorld.TickRate && !m.Clinging; i++) Run(w, 1f / PlaneWorld.TickRate);
        Assert.True(m.Clinging);
        Assert.Equal(MurkState.Attached, m.State);
        // No hit on touch: only the steady drain, ClingDrain a second.
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.PlayerHit);
        w.Player.Hp = 100f;
        var her = w.Player.Position;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Step(default);
        }
        Assert.Equal(100f - BlightTuning.ClingDrain * w.HurtScale, w.Player.Hp, 1);
        Assert.Empty(w.MurkShots);
    }

    [Fact]
    public void Her_beam_draws_it_from_afar()
    {
        var w = World();
        var d = Way(w, 13f);
        var m = Seeker(w, w.Player.Position + d * 11f);
        Run(w, 0.5f);
        Assert.Equal(MurkState.Searching, m.State);
        float was = Vector2.Distance(m.Position, w.Player.Position);
        // (Coming into the beam it is dazzled a moment first.)
        Run(w, HerLightTuning.Dazzle + 1.5f, beam: d);
        Assert.Same(w.Her, m.Light?.Owner);
        Assert.True(Vector2.Distance(m.Position, w.Player.Position) < was - 1f);
    }

    [Fact]
    public void A_brighter_light_wins_over_her_and_it_feeds_on_that_at_the_same_pace()
    {
        var w = World();
        var d = Way(w, 8f);
        var m = Seeker(w, w.Player.Position + d * 3.5f);
        var plant = Plant(w, w.Player.Position + d * 6.5f);
        Run(w, 0.3f);
        Assert.Same(plant, m.Light);
        for (int i = 0; i < 4 * PlaneWorld.TickRate && m.Feeding is null; i++) Run(w, 1f / PlaneWorld.TickRate);
        Assert.Same(plant, m.Feeding);
        Assert.Equal(MurkState.Attached, m.State);
        float before = plant.Owner.Energy;
        Run(w, 1f);
        Assert.Equal(before - BlightTuning.ClingDrain, plant.Owner.Energy, 1);
        Assert.Equal(100f, w.Player.Hp);
    }

    [Fact]
    public void Swimming_to_her_it_turns_to_a_brighter_light_that_comes_on()
    {
        var w = World();
        var d = Way(w, 8f);
        var m = Seeker(w, w.Player.Position + d * 4f);
        Run(w, 0.3f);
        Assert.Same(w.Her, m.Light!.Owner);
        var plant = Plant(w, m.Position + d * 2.5f);
        Run(w, LightTuning.ScanEvery + 0.05f);
        Assert.Same(plant, m.Light);
    }

    [Fact]
    public void Feeding_it_does_not_look_for_another_light_and_lets_go_when_its_light_is_spent()
    {
        var w = World();
        var d = Way(w, 9f);
        var m = Seeker(w, w.Player.Position + d * 7f);
        var weak = Plant(w, w.Player.Position + d * 7.2f, intensity: 1f, energy: 1.5f);
        for (int i = 0; i < PlaneWorld.TickRate && m.Feeding is null; i++) Run(w, 1f / PlaneWorld.TickRate);
        Assert.Same(weak, m.Feeding);
        // A far brighter light beside it: it stays on the one it holds.
        var bright = Plant(w, w.Player.Position + d * 8.5f, intensity: 5f);
        Run(w, 0.3f);
        Assert.Same(weak, m.Feeding);
        // Drunk dry, it lets go and goes at once for the brightest it sees.
        Run(w, 1f);
        Assert.False(weak.Shining);
        Assert.Null(m.Feeding is { } f && f == weak ? f : null);
        Assert.True(m.Light == bright || m.Feeding == bright);
    }

    [Fact]
    public void A_dash_shakes_it_off_where_it_held_and_it_looks_again_once_it_recovers()
    {
        var w = World();
        var d = Way(w, 6f);
        var m = Seeker(w, w.Player.Position + d * 0.5f);
        m.Clinging = true;
        m.ClingOffset = d * 0.5f;
        Run(w, 0.2f);
        w.Step(new PlaneInput { Dash = true, Move = -d });
        Assert.False(m.Clinging);
        Assert.Equal(MurkState.Released, m.State);
        var left = m.Position;
        for (int i = 0; i < (int)(BlightTuning.ShakeSeconds * PlaneWorld.TickRate) - 2; i++) w.Step(default);
        Assert.True(Vector2.Distance(m.Position, left) < 0.3f, $"moved {Vector2.Distance(m.Position, left)} m");
        for (int i = 0; i < 6; i++) w.Step(default);
        Assert.NotEqual(MurkState.Released, m.State);
        Assert.NotEqual(MurkState.Attached, m.State);
    }

    [Fact]
    public void Each_murkling_seeks_on_its_own()
    {
        var w = World();
        var d = Way(w, 13f);
        var near = Seeker(w, w.Player.Position + d * 3f);
        var far = Seeker(w, w.Player.Position + d * 12.5f);
        Run(w, 1f);
        Assert.Equal(MurkState.Approaching, near.State == MurkState.Attached ? MurkState.Approaching : near.State);
        Assert.Equal(MurkState.Searching, far.State);
    }

    static Murkling? BudAt(PlaneWorld w, Vector2 at) =>
        (Murkling?)typeof(PlaneWorld).GetMethod("Bud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(w, new object[] { at, -1, false, false, false });

    [Fact]
    public void Depth_1_buds_basic_murklings_only_and_deeper_some_are_variants()
    {
        var shallow = World(p => p.Id.ShownDepth == 1 && !p.HasBoss);
        var deep = World(p => p.Id.ShownDepth == 5 && !p.HasBoss);
        var kinds1 = Enumerable.Range(0, 60).Select(_ => BudAt(shallow, shallow.Player.Position)!.Kind).ToList();
        var kinds5 = Enumerable.Range(0, 60).Select(_ => BudAt(deep, deep.Player.Position)!.Kind).ToList();
        Assert.All(kinds1, k => Assert.Equal(MurkKind.Basic, k));
        Assert.Contains(MurkKind.Thrower, kinds5);
        Assert.Contains(MurkKind.Hunter, kinds5);
        Assert.Contains(MurkKind.Basic, kinds5);
    }
}
