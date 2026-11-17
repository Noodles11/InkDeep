using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her beam her lamp and her light bubbles (PlaneWorld.BeamLamp, LightBubbleTuning).</summary>
public class LightBubbleTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true }) { BeamLamp = true };
        w.Murklings.Clear();
        w.Fish.Clear();
        return w;
    }

    static (Vector2 At, Vector2 Way) Open(PlaneWorld w, float far)
    {
        var at = w.Map.Start.Position;
        for (int k = 0; k < 72; k++)
        {
            var way = new Vector2(MathF.Cos(k * MathF.Tau / 72f), MathF.Sin(k * MathF.Tau / 72f));
            if (w.LineReach(at, way, far, out _) >= far && w.Clear(at + way * far, 1f)) return (at, way);
        }
        throw new InvalidOperationException("no open water by her start");
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Step(input);
    }

    [Fact]
    public void Her_glow_never_shines_and_the_glow_button_lights_her_beam_or_puts_it_out()
    {
        var w = World();
        var (at, way) = Open(w, 10f);
        Hold(w, at, new PlaneInput { Aim = way });
        Assert.False(w.Aura.Shining);
        Assert.True(w.Beam.Shining);
        Hold(w, at, new PlaneInput { Aim = way, ToggleGlow = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.GlowToggled && e.Size == 0f);
        Hold(w, at, new PlaneInput { Aim = way });
        Assert.False(w.Beam.Shining);
        Assert.True(w.Hidden);
        Hold(w, at, new PlaneInput { Aim = way, ToggleGlow = true });
        Hold(w, at, new PlaneInput { Aim = way });
        Assert.True(w.Beam.Shining);
        Assert.False(w.Aura.Shining);
    }

    [Fact]
    public void Fire_held_throws_four_light_bubbles_a_second_flying_straight_and_fast()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        int thrown = 0;
        for (int i = 0; i < (int)MathF.Round(1f / PlaneWorld.Dt); i++)
        {
            Hold(w, at, new PlaneInput { Aim = way, Fire = true });
            thrown += w.Events.Count(e => e.Type == PlaneEventType.Shot);
        }
        Assert.Equal((int)LightBubbleTuning.Rate, thrown);
        var shot = w.Shots.First(s => s.FromPlayer);
        Assert.True(shot.Straight);
        Assert.Equal(LightBubbleTuning.Radius, shot.Radius, 3);
        Assert.Equal(LightBubbleTuning.Speed, shot.Velocity.Length(), 2);
        Assert.True(Vector2.Dot(Vector2.Normalize(shot.Velocity), way) > 0.999f);
        Assert.False(shot.Pierce);
    }

    [Fact]
    public void A_light_bubble_strikes_a_murkling_for_its_damage_and_pops()
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        var spot = at + way * 6f;
        var m = w.DebugBud(spot, prime: false);
        m.Bud = 0f;
        // (Her beam out, so only the bubble harms it.)
        Hold(w, at, new PlaneInput { Aim = way, ToggleGlow = true });
        float meter = m.Corruption.Value;
        Hold(w, at, new PlaneInput { Aim = way, Fire = true });
        bool hit = false;
        for (int i = 0; i < 40 && !hit; i++)
        {
            Hold(w, at, new PlaneInput { Aim = way });
            m.Position = spot;
            hit = m.Corruption.Value < meter;
        }
        Assert.True(hit);
        Assert.Equal(meter - LightBubbleTuning.Damage, m.Corruption.Value, 2);
        Assert.DoesNotContain(w.Shots, s => s.FromPlayer && s.Life > 0f);
    }

    [Fact]
    public void Her_lamp_beam_burns_far_less_than_her_beam_did()
    {
        float Burned(bool lamp)
        {
            var w = World();
            w.BeamLamp = lamp;
            var (at, way) = Open(w, 12f);
            var spot = at + way * 6f;
            var m = w.DebugBud(spot, prime: false);
            m.Bud = 0f;
            float meter = m.Corruption.Value;
            for (int i = 0; i < 30; i++)
            {
                Hold(w, at, new PlaneInput { Aim = way, Beam = true });
                m.Position = spot;
            }
            return meter - m.Corruption.Value;
        }
        float old = Burned(false), now = Burned(true);
        Assert.True(now > 0f);
        Assert.True(now < old * 0.3f, $"lamp {now} vs beam {old}");
    }
}
