using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her stats on the beam (docs/BEAM-PEARLS-PLAN.md, step 2): tick rate, carry and light pressure.</summary>
public class BeamStatTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1)), new Tuning(), run,
            new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.ReefFish.Clear();
        w.Fish.Clear();
        return w;
    }

    static Vector2 Open(PlaneWorld w, float far)
    {
        var her = w.Map.Start.Position;
        for (int i = 0; i < 72; i++)
        {
            float a = i * MathF.Tau / 72f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            bool ok = true;
            for (float s = 0.5f; s <= far && ok; s += 0.5f) ok = w.Clear(w.Map.Start.Position + d * s, 1f);
            if (ok && w.LineOfSight(her, her + d * far)) return d;
        }
        throw new InvalidOperationException("no open way");
    }

    /// <summary>How much a pinned murkling this far along her beam is burned in half a second.</summary>
    static float Burned(PlaneWorld w, float distance)
    {
        var her = w.Map.Start.Position;
        var way = Open(w, distance + 1f);
        var at = her + way * distance;
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = 10000f;
        w.Murklings.Add(m);
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            m.Position = at;
            m.Velocity = m.Knock = Vector2.Zero;
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Beam = true, Aim = way });
        }
        return 10000f - m.Corruption.Value;
    }

    [Fact]
    public void Her_fire_rate_is_the_beams_tick_rate_and_burns_faster()
    {
        var plain = World();
        var quick = World("squid_ink_espresso");
        Assert.Equal(1f, plain.BeamTick, 3);
        Assert.True(quick.BeamTick > 1.1f);
        plain.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        quick.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.Equal(plain.HerBeams.First().Shots * quick.BeamTick, quick.HerBeams.First().Shots, 3);
        float a = Burned(World(), 4f), b = Burned(World("squid_ink_espresso"), 4f);
        Assert.Equal(a * quick.BeamTick, b, 0);
    }

    [Fact]
    public void Her_shot_speed_is_its_carry_more_light_far_out()
    {
        var plain = World();
        var far = World("cuttlebone");
        var slow = World("pufferfish_pout");
        Assert.Equal(LightTuning.FarLight, plain.BeamCarry, 3);
        Assert.True(far.BeamCarry > plain.BeamCarry);
        Assert.True(slow.BeamCarry < plain.BeamCarry);
        // Near her the same, far out more.
        Assert.Equal(plain.BeamFalloff(plain.Beam, 0f), far.BeamFalloff(far.Beam, 0f), 3);
        Assert.True(far.BeamFalloff(far.Beam, far.Beam.Range) > plain.BeamFalloff(plain.Beam, plain.Beam.Range));
    }

    [Fact]
    public void Her_knockback_is_light_pressure_pushing_what_is_in_her_beam_away()
    {
        foreach (bool hook in new[] { false, true })
        {
            var w = hook ? World("captains_hook") : World();
            var her = w.Map.Start.Position;
            var way = Open(w, 9f);
            var start = her + way * 4f;
            var m = new Murkling { Position = start, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
            m.Corruption.Max = m.Corruption.Value = 10000f;
            w.Murklings.Add(m);
            for (int i = 0; i < PlaneWorld.TickRate; i++)
            {
                w.Player.Position = w.Player.PrevPosition = her;
                w.Player.Velocity = Vector2.Zero;
                w.Player.Hp = w.Run.MaxHp;
                w.Step(new PlaneInput { Beam = true, Aim = way });
            }
            float pushed = Vector2.Dot(m.Position - start, way);
            if (hook) Assert.True(pushed > 0.5f, $"pushed {pushed} m");
            else Assert.Equal(Vector2.Zero, w.LightPressure(m, m.Position, m.Radius));
        }
    }
}
