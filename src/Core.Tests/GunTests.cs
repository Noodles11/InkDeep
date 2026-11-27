using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her shots as a gun (PlaneGun.cs): fast and straight, a hard shove, a kick back; her pearls shape how it fires.</summary>
public class GunTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(bool gun, params string[] pearls)
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, 1 + LevelPlan.Tutorial, 2), crossover: true);
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (var p in pearls) run.Add(p);
        var w = new PlaneWorld(map, new Tuning(), run, new PlaneOptions { Adventure = true, Glow = false, Gun = gun });
        w.Murklings.Clear();
        return w;
    }

    /// <summary>The way from her start with the most open water ahead.</summary>
    static Vector2 OpenWay(PlaneWorld w)
    {
        Vector2 best = Vector2.UnitX;
        float far = 0f;
        for (int k = 0; k < 32; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 32f), MathF.Sin(k * MathF.Tau / 32f));
            float r = w.LineReach(w.Map.Start.Position, d, 40f, out _);
            if (r > far) (far, best) = (r, d);
        }
        return best;
    }

    static PlaneShot FireOne(PlaneWorld w, Vector2 aim)
    {
        for (int i = 0; i < 30 && w.Shots.Count(s => s.FromPlayer) == 0; i++)
        {
            w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Fire = true, Aim = aim });
        }
        return w.Shots.First(s => s.FromPlayer);
    }

    [Fact]
    public void A_gun_s_shot_flies_straight_and_fast_and_goes_out_at_its_range()
    {
        var w = World(gun: true);
        var aim = OpenWay(w);
        var shot = FireOne(w, aim);
        Assert.True(shot.Gun);
        float speed = shot.Velocity.Length();
        Assert.True(speed > PlaneCombatTuning.ShotSpeed * 2f, $"speed {speed}");
        for (int i = 0; i < 5 && w.Shots.Contains(shot); i++) w.Step(default);
        if (w.Shots.Contains(shot)) Assert.Equal(speed, shot.Velocity.Length(), 2);
        for (int i = 0; i < 120 && w.Shots.Contains(shot); i++) w.Step(default);
        Assert.DoesNotContain(shot, w.Shots);
    }

    [Fact]
    public void Each_shot_kicks_her_back_against_her_aim()
    {
        var w = World(gun: true);
        var aim = OpenWay(w);
        FireOne(w, aim);
        // (Barely: a touch back, never more than RecoilMost however strong the volley.)
        float back = -Vector2.Dot(w.Player.Velocity, aim);
        Assert.True(back > 0.05f && back <= GunTuning.RecoilMost + 1e-3f, $"her velocity {w.Player.Velocity} after a shot along {aim}");
        var still = World(gun: false);
        FireOne(still, aim);
        Assert.True(Vector2.Dot(still.Player.Velocity, aim) > -0.5f, "a light bubble kicks her back too");
    }

    [Fact]
    public void A_gun_s_shot_shoves_a_murkling_far_harder_than_a_bubble()
    {
        float Shoved(bool gun)
        {
            var w = World(gun);
            var aim = OpenWay(w);
            var at = w.Map.Start.Position + aim * 4f;
            var m = w.DebugBud(at, prime: false);
            m.Bud = 0f;
            m.Corruption.Max = m.Corruption.Value = 1000f;
            float most = 0f;
            for (int i = 0; i < 40; i++)
            {
                w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
                m.Position = at;
                w.Step(new PlaneInput { Fire = i < 3, Aim = aim });
                most = MathF.Max(most, Vector2.Dot(m.Knock, aim));
            }
            return most;
        }
        float bubble = Shoved(gun: false), gun = Shoved(gun: true);
        Assert.True(gun > bubble * 2.5f, $"gun {gun}, bubble {bubble}");
    }

    [Fact]
    public void Her_pearls_make_it_a_shotgun_short_and_scattered_with_a_bigger_kick()
    {
        var pistol = World(gun: true);
        var aim = OpenWay(pistol);
        FireOne(pistol, aim);
        float pistolKick = -Vector2.Dot(pistol.Player.Velocity, aim);
        float pistolRange = pistol.Shots.First(s => s.FromPlayer).Range;

        var shotgun = World(gun: true, "triple_tentacle");
        FireOne(shotgun, aim);
        var pellets = shotgun.Shots.Where(s => s.FromPlayer).ToList();
        Assert.True(pellets.Count >= 3);
        Assert.All(pellets, s => Assert.True(s.Range < pistolRange * 0.7f));
        Assert.True(-Vector2.Dot(shotgun.Player.Velocity, aim) > pistolKick * 1.5f);
    }
}
