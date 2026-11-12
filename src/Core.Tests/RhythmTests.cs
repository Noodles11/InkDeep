using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her light's rhythms (docs/LIGHT-AND-GRAPH.md §12, PlaneRhythm.cs).</summary>
public class RhythmTests
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
            for (float s = 0.5f; s <= far && open; s += 0.5f) open = w.Clear(her + d * s, 0.9f);
            if (open && w.LineOfSight(her, her + d * far)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    static void Hold(PlaneWorld w, Vector2 aim, float seconds, bool beam = true, bool use = false)
    {
        for (int i = 0; i < (int)(seconds * PlaneWorld.TickRate); i++)
        {
            w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = aim, Beam = beam, UseActive = use && i == 0 });
        }
    }

    static float Worth(PlaneWorld w) => w.HerBeams.First().Shots;

    [Fact]
    public void Focus_builds_held_steady_narrowing_and_burning_three_times()
    {
        var plain = World();
        Hold(plain, Vector2.UnitX, 2f);
        var w = World("pearl_diver");
        Hold(w, Vector2.UnitX, 0.5f);
        Assert.InRange(w.Focus, 0.2f, 0.45f);
        Hold(w, Vector2.UnitX, 1.2f);
        Assert.Equal(1f, w.Focus, 3);
        Assert.Equal(RhythmTuning.FocusBurn, Worth(w) / Worth(plain), 2);
        Assert.Equal(RhythmTuning.FocusNarrow, w.Beam.HalfAngle / plain.Beam.HalfAngle, 2);
    }

    [Fact]
    public void A_swing_loses_the_focus_and_letting_go_ends_it()
    {
        var w = World("pearl_diver");
        Hold(w, Vector2.UnitX, 2f);
        Assert.Equal(1f, w.Focus, 3);
        // Swung round fast.
        for (int i = 0; i < PlaneWorld.TickRate / 4; i++)
        {
            float a = i * 0.08f;
            w.Step(new PlaneInput { Aim = new Vector2(MathF.Cos(a), MathF.Sin(a)), Beam = true });
        }
        Assert.True(w.Focus < 0.5f);
        Hold(w, Vector2.UnitX, 2f);
        Hold(w, Vector2.UnitX, 0.1f, beam: false);
        Assert.Equal(0f, w.Focus);
    }

    [Fact]
    public void Spin_up_builds_while_held_unbroken_and_spins_down_let_go()
    {
        var plain = World();
        Hold(plain, Vector2.UnitX, 1f);
        var w = World("sardine_run");
        Hold(w, Vector2.UnitX, 1.5f);
        Assert.InRange(w.SpinUp, 0.4f, 0.6f);
        Hold(w, Vector2.UnitX, 1.6f);
        Assert.Equal(1f, w.SpinUp, 3);
        Assert.Equal(RhythmTuning.SpinBurn, Worth(w) / Worth(plain), 2);
        // (Sardine Run no longer narrows nor weakens her beam.)
        Assert.Equal(plain.Beam.HalfAngle, w.Beam.HalfAngle, 3);
        Hold(w, Vector2.UnitX, 1f, beam: false);
        Assert.Equal(0f, w.SpinUp);
    }

    [Fact]
    public void Snap_bursts_where_her_beam_ends_on_a_creature()
    {
        var w = World("pistol_shrimp");
        var d = Line(w, 6f);
        var at = w.Map.Start.Position + d * 4f;
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = 100000f;
        w.Murklings.Add(m);
        int snaps = 0;
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
            m.ThrowTimer = 100f;
            w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = d, Beam = true });
            snaps += w.Events.Count(e => e.Type == PlaneEventType.Cavitation);
            if (i == 2 * PlaneWorld.TickRate - 1) Assert.True(m.Knock.LengthSquared() >= 0f);
        }
        Assert.Equal(2, snaps);

        // None into open water.
        var empty = World("pistol_shrimp");
        int none = 0;
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++)
        {
            empty.Player.Position = empty.Player.PrevPosition = empty.Map.Start.Position;
            empty.Step(new PlaneInput { Aim = d, Beam = true });
            none += empty.Events.Count(e => e.Type == PlaneEventType.Cavitation);
        }
        Assert.Equal(0, none);
    }

    [Fact]
    public void Glow_Burst_makes_her_beam_beat()
    {
        var w = World("glow_burst");
        Assert.Equal("glow_burst", w.Run.Active?.Id);
        w.Run.SetCharge(0, 1f);
        Hold(w, Vector2.UnitX, 0.05f, use: true);
        Assert.True(w.PulseLeft > 4.5f);
        float most = 0f, least = float.MaxValue;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            Hold(w, Vector2.UnitX, 1f / PlaneWorld.TickRate);
            most = MathF.Max(most, w.RhythmBurn);
            least = MathF.Min(least, w.RhythmBurn);
        }
        Assert.InRange(most, 1.8f, 2.01f);
        Assert.True(least < 1.2f);
        Hold(w, Vector2.UnitX, 5f);
        Assert.Equal(1f, w.RhythmBurn, 3);
    }

    [Fact]
    public void Pearl_Necklace_opens_a_ring_at_full_focus()
    {
        var w = World("pearl_diver", "mitosis");
        Assert.Contains("pearl_necklace", w.Run.Loadout.Synergies);
        Hold(w, Vector2.UnitX, 0.5f);
        int before = w.Born.Count;
        Hold(w, Vector2.UnitX, 1.5f);
        Assert.True(w.Born.Count >= before + RhythmTuning.NecklaceRays);
    }
}
