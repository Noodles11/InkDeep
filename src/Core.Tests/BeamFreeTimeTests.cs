using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>How long her plain beam takes to free a murkling (HerLightTuning.Burn).</summary>
public class BeamFreeTimeTests
{
    readonly ITestOutputHelper _out;
    public BeamFreeTimeTests(ITestOutputHelper output) => _out = output;

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>Seconds of her plain beam a murkling of this full size takes to free, held this far out on her beam's axis, at the first depth.</summary>
    float Seconds(float full, float far)
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 1 && !p.HasBoss);
        var w = new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, Shots = false, InkMurklings = false });
        w.Murklings.Clear();
        var her = w.Map.Start.Position;
        Vector2 d = Vector2.UnitX;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var c = new Vector2(MathF.Cos(a), MathF.Sin(a));
            bool open = true;
            for (float s = 0.5f; s <= far + 1f && open; s += 0.5f) open = w.Clear(her + c * s, 0.8f);
            if (open && w.LineOfSight(her, her + c * (far + 1f))) { d = c; break; }
        }
        var at = her + d * far;
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower, Full = full, Radius = full, Body = full };
        float meter = BlightTuning.MurkCorruption * (1f + BlightTuning.MeterPerMenace * w.Menace) * MurkSizeTuning.MeterScale(full);
        m.Corruption.Max = m.Corruption.Value = meter;
        w.Murklings.Add(m);
        int ticks = 0;
        while (m.Alive && ticks < 30 * PlaneWorld.TickRate)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Aim = d, Beam = true });
            ticks++;
        }
        return ticks / (float)PlaneWorld.TickRate;
    }

    [Fact]
    public void A_middling_murkling_is_freed_in_about_two_seconds_of_her_plain_beam()
    {
        float mid = (MurkSizeTuning.Least + MurkSizeTuning.Most) * 0.5f;
        float t = Seconds(mid, 5f);
        _out.WriteLine($"{t:0.00} s (small {Seconds(MurkSizeTuning.Least, 5f):0.00}, big {Seconds(MurkSizeTuning.Most, 5f):0.00}, far {Seconds(mid, 10f):0.00})");
        Assert.InRange(t, 1.5f, 2.5f);
    }
}
