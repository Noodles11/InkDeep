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

    /// <summary>One bubble thrown at a murkling 6 m off: what it took off it, and the push it gave (her beam lit or out).</summary>
    static (float Dealt, float Push, PlaneWorld W) StrikeOne(bool beam, float meterLeft = -1f)
    {
        var w = World();
        var (at, way) = Open(w, 12f);
        var spot = at + way * 6f;
        var m = w.DebugBud(spot, prime: false);
        m.Bud = 0f;
        // (Her beam out first, so the bubble alone harms it; lit again for a lit throw, its little burn taken off.)
        Hold(w, at, new PlaneInput { Aim = way, ToggleGlow = true });
        if (meterLeft > 0f) m.Corruption.Value = meterLeft;
        if (beam) Hold(w, at, new PlaneInput { Aim = way, ToggleGlow = true });
        float meter = m.Corruption.Value;
        float burned = 0f;
        Hold(w, at, new PlaneInput { Aim = way, Fire = true });
        float push = 0f;
        for (int i = 0; i < 40 && push == 0f; i++)
        {
            float was = m.Corruption.Value;
            Hold(w, at, new PlaneInput { Aim = way });
            var knock = w.Events.FirstOrDefault(e => e.Type == PlaneEventType.BubbleKnock);
            if (knock.Type == PlaneEventType.BubbleKnock) push = knock.Size;
            else burned += was - m.Corruption.Value;
            m.Position = spot;
        }
        Assert.True(push > 0f, "no bubble struck it");
        Assert.DoesNotContain(w.Shots, s => s.FromPlayer && s.Life > 0f);
        return (meter - burned - m.Corruption.Value, push, w);
    }

    [Fact]
    public void A_light_bubble_in_her_beam_strikes_for_its_damage_out_of_it_for_half_and_pops()
    {
        var (lit, _, _) = StrikeOne(beam: true);
        var (dark, _, _) = StrikeOne(beam: false);
        // (Her beam's own little burn on the frame it strikes rides along.)
        Assert.InRange(lit, LightBubbleTuning.Damage, LightBubbleTuning.Damage + 0.3f);
        Assert.Equal(LightBubbleTuning.Damage * LightBubbleTuning.DarkShare, dark, 2);
    }

    [Fact]
    public void A_light_bubble_pushes_back_the_harder_the_less_health_is_left()
    {
        var (_, fresh, _) = StrikeOne(beam: false);
        var (_, spent, _) = StrikeOne(beam: false, meterLeft: 4f);
        Assert.True(spent > fresh * 2f, $"spent {spent} vs fresh {fresh}");
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
