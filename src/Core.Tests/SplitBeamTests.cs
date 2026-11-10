using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her beam split into rays by her shot pearls (docs/BEAM-PEARLS-PLAN.md, step 1).</summary>
public class SplitBeamTests
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

    static (LightEmitter Ray, float Worth)[] Shining(PlaneWorld w) =>
        w.HerBeams.Where(b => b.Beam.Shining && b.Beam != w.RearBeam && b.Beam != w.SweepBeam).ToArray();

    static float Off(PlaneWorld w, LightEmitter ray) =>
        MathF.Atan2(w.BeamAim.X * ray.Direction.Y - w.BeamAim.Y * ray.Direction.X, Vector2.Dot(w.BeamAim, ray.Direction)) * 180f / MathF.PI;

    [Fact]
    public void Without_a_split_pearl_her_beam_is_one_ray_worth_all_of_it()
    {
        var w = World();
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        var rays = Shining(w);
        Assert.Single(rays);
        Assert.Same(w.Beam, rays[0].Ray);
        Assert.Equal(HerLightTuning.BeamShots, rays[0].Worth, 3);
        Assert.Equal(0f, Off(w, w.Beam), 2);
    }

    [Fact]
    public void Triple_Tentacle_fans_it_into_three_narrower_rays_each_a_volleys_share()
    {
        var plain = World();
        plain.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        var w = World("triple_tentacle");
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        var rays = Shining(w);
        Assert.Equal(3, rays.Length);
        // Her own in the middle, one either side.
        Assert.Equal(0f, Off(w, w.Beam), 2);
        var sides = rays.Select(r => Off(w, r.Ray)).OrderBy(a => a).ToArray();
        Assert.Equal(-12f, sides[0], 1);
        Assert.Equal(12f, sides[2], 1);
        foreach (var (ray, worth) in rays)
        {
            Assert.Equal(HerLightTuning.BeamShots * PlaneCombatTuning.VolleyShot(3), worth, 3);
            Assert.True(ray.HalfAngle < plain.Beam.HalfAngle);
        }
        Assert.True(w.BeamSpread > plain.BeamSpread);
    }

    [Fact]
    public void Hammerhead_opens_a_wide_floodlight_of_five_shorter_rays()
    {
        var w = World("hammerhead");
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        var rays = Shining(w);
        Assert.Equal(5, rays.Length);
        var offs = rays.Select(r => Off(w, r.Ray)).OrderBy(a => a).ToArray();
        Assert.Equal(-30f, offs[0], 1);
        Assert.Equal(30f, offs[4], 1);
        var plain = World();
        plain.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.All(rays, r => Assert.True(r.Ray.Range < plain.Beam.Range));
    }

    [Fact]
    public void Double_Helix_weaves_two_strands_across_her_aim()
    {
        var w = World("double_helix");
        float lo = 0f, hi = 0f;
        for (int i = 0; i < PlaneWorld.TickRate * 3; i++)
        {
            w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
            var rays = Shining(w);
            Assert.Equal(2, rays.Length);
            float a = Off(w, rays[0].Ray), b = Off(w, rays[1].Ray);
            // Always mirrored about her aim.
            Assert.Equal(-a, b, 2);
            lo = MathF.Min(lo, a);
            hi = MathF.Max(hi, a);
        }
        // Each swings to both sides: they cross.
        Assert.True(lo < -LightTuning.WaveDeg * 0.9f && hi > LightTuning.WaveDeg * 0.9f, $"{lo}..{hi}");
    }

    [Fact]
    public void Kraken_Form_adds_short_rays_all_round_her()
    {
        var layout = PlaneWorld.RayLayout(new ShotSpec { Pattern = ShotPattern.Kraken8 }, 0f);
        Assert.Equal(LightTuning.KrakenRays, layout.Count);
        Assert.Equal(0f, layout[0].Offset);
        for (int k = 1; k < layout.Count; k++)
        {
            Assert.Equal(k * MathF.Tau / LightTuning.KrakenRays, layout[k].Offset, 3);
            Assert.Equal(LightTuning.KrakenReach, layout[k].Reach);
            Assert.Equal(LightTuning.KrakenWorth, layout[k].Worth);
        }
    }

    [Fact]
    public void A_side_ray_burns_what_her_plain_beam_would_miss()
    {
        foreach (bool split in new[] { false, true })
        {
            var w = split ? World("hammerhead") : World();
            var her = w.Map.Start.Position;
            // A murkling 28° off her aim, 5 m out, in open water.
            Vector2? spot = null;
            Vector2 aim = default;
            for (int i = 0; i < 72 && spot is null; i++)
            {
                float a = i * MathF.Tau / 72f;
                aim = new Vector2(MathF.Cos(a), MathF.Sin(a));
                var side = new Vector2(MathF.Cos(a + 28f * MathF.PI / 180f), MathF.Sin(a + 28f * MathF.PI / 180f));
                var at = her + side * 5f;
                if (w.Clear(at, 0.8f) && w.LineOfSight(her, at) && w.Clear(her + aim * 4f, 0.5f)) spot = at;
            }
            Assert.NotNull(spot);
            var m = new Murkling { Position = spot!.Value, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
            m.Corruption.Max = m.Corruption.Value = 10000f;
            w.Murklings.Add(m);
            for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
            {
                m.Position = spot.Value;
                m.Velocity = Vector2.Zero;
                w.Player.Position = w.Player.PrevPosition = her;
                w.Player.Velocity = Vector2.Zero;
                w.Player.Hp = w.Run.MaxHp;
                w.Step(new PlaneInput { Beam = true, Aim = aim });
            }
            if (split) Assert.True(m.Corruption.Value < 10000f, "a side ray reaches it");
            else Assert.Equal(10000f, m.Corruption.Value);
        }
    }
}
