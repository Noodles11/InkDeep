using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her searchlight burns and dazzles the corrupted in its beam (docs/ADVENTURE.md §8; PlaneHerLight.cs).</summary>
public class HerLightTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>An adventure level with no fish, no anemones and every leak sealed: only what a test brings.</summary>
    static PlaneWorld Bare()
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        var w = new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        w.ReefFish.Clear();
        w.Anemones.Clear();
        w.Schools.Clear();
        w.Flounders.Clear();
        foreach (var v in w.Leaks) v.Sealed = true;
        return w;
    }

    /// <summary>An open spot this far from her start, in plain sight of it.</summary>
    static Vector2? Beside(PlaneWorld w, float distance)
    {
        var her = w.Map.Start.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var p = her + new Vector2(MathF.Cos(a), MathF.Sin(a)) * distance;
            if (w.Clear(p, 0.6f) && w.LineOfSight(her, p)) return p;
        }
        return null;
    }

    static Murkling Murkling(PlaneWorld w, Vector2 at)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f };
        w.Murklings.Add(m);
        return m;
    }

    /// <summary>
    /// A step with her held at the level's start (against its current), full of light, swimming on the spot this way at
    /// her cruise (zero: still); the murklings held where they are (pinned) when asked.
    /// </summary>
    static void Step(PlaneWorld w, Vector2 swim, params (Murkling M, Vector2 At)[] pinned)
    {
        foreach (var (m, at) in pinned)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = swim * w.CruiseSpeed;
        w.Player.Hp = 100f;
        w.Step(default);
    }

    /// <summary>She swims on the spot this way for a second: her bell tips, and her beam streams out behind her.</summary>
    static void Swim(PlaneWorld w, Vector2 swim)
    {
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, swim);
    }

    [Fact]
    public void At_rest_her_beam_shines_straight_down_and_burns_nothing_beside_her()
    {
        var w = Bare();
        if (Beside(w, 3f) is not { } at) return;
        var m = Murkling(w, at);
        float full = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, Vector2.Zero, (m, at));
        Assert.True(w.BeamDirection.Y < -0.99f, $"{w.BeamDirection}");
        Assert.Equal(full, m.Corruption.Value);
        Assert.False(w.InHerLight(m));
    }

    [Fact]
    public void Swimming_her_beam_streams_out_behind_her_and_slowly_burns_what_is_in_it()
    {
        var w = Bare();
        if (Beside(w, 3f) is not { } behind) return;
        var her = w.Map.Start.Position;
        var swim = Vector2.Normalize(her - behind);
        Swim(w, swim);
        // Out behind her and down a little: tipped about 70° at her cruise.
        Assert.True(Vector2.Dot(Vector2.Normalize(new Vector2(w.BeamDirection.X, w.BeamDirection.Z)), -swim) > 0.95f, $"{w.BeamDirection}");
        Assert.InRange(MathF.Acos(-w.BeamDirection.Y), 1.1f, 1.3f);
        var lit = Murkling(w, behind);
        // One beside her, square to her swim, is outside the beam.
        var side = her + new Vector2(-swim.Y, swim.X) * 3f;
        var dark = w.Clear(side, 0.6f) ? Murkling(w, side) : null;
        float full = lit.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            if (dark is null) Step(w, swim, (lit, behind));
            else Step(w, swim, (lit, behind), (dark, side));
        }
        Assert.Equal(full - HerLightTuning.Burn, lit.Corruption.Value, 1);
        Assert.True(w.InHerLight(lit));
        if (dark is not null)
        {
            Assert.Equal(full, dark.Corruption.Value);
            Assert.False(w.InHerLight(dark));
        }
        // Steady, not hits: no flash, no numbers.
        Assert.Equal(0f, lit.Corruption.LastHit);

        // Kept in it, she frees it.
        for (int i = 0; i < (full / HerLightTuning.Burn + 1f) * PlaneWorld.TickRate && lit.Alive; i++) Step(w, swim, (lit, behind));
        Assert.False(lit.Alive);
    }

    [Fact]
    public void Coming_into_her_beam_dazzles_a_murkling_still_for_a_moment_then_it_carries_on()
    {
        var w = Bare();
        if (Beside(w, 4f) is not { } at) return;
        var swim = Vector2.Normalize(w.Map.Start.Position - at);
        Swim(w, swim);
        var m = Murkling(w, at);
        Step(w, swim);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dazzled);
        Assert.True(w.Dazzled(m));
        // Dazzled, it holds still...
        for (int i = 0; i < (int)(HerLightTuning.Dazzle * PlaneWorld.TickRate) - 3; i++) Step(w, swim);
        Assert.Equal(at, m.Position);
        // ...then carries on, never dazzled again while it stays in her beam.
        int again = 0;
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            Step(w, swim);
            again += w.Events.Count(e => e.Type == PlaneEventType.Dazzled);
        }
        Assert.False(w.Dazzled(m));
        Assert.True(Vector2.Distance(m.Position, at) > 0.3f, "it moves on once the dazzle has passed");
        Assert.Equal(0, again);
    }

    [Fact]
    public void A_murkling_out_of_her_beam_long_enough_is_dazzled_again()
    {
        var w = Bare();
        if (Beside(w, 3f) is not { } near) return;
        var swim = Vector2.Normalize(w.Map.Start.Position - near);
        Swim(w, swim);
        // Out of the beam: ahead of her.
        var ahead = w.Map.Start.Position + swim * 3f;
        var m = Murkling(w, near);
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, swim, (m, near));
        // Out of it, but not long enough: no dazzle on coming back.
        for (int i = 0; i < (int)(HerLightTuning.Rearm * PlaneWorld.TickRate) / 2; i++) Step(w, swim, (m, ahead));
        Step(w, swim, (m, near));
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.Dazzled);
        // Out of it long enough: dazzled again.
        for (int i = 0; i < (int)(HerLightTuning.Rearm * PlaneWorld.TickRate) + 2; i++) Step(w, swim, (m, ahead));
        Step(w, swim, (m, near));
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dazzled);
    }

    [Fact]
    public void Rock_between_her_and_a_murkling_keeps_her_beam_off_it()
    {
        var w = Bare();
        var her = w.Map.Start.Position;
        Vector2? hidden = null;
        for (float r = 1.5f; r < 6f && hidden is null; r += 0.5f)
            for (int i = 0; i < 64; i++)
            {
                var p = her + new Vector2(MathF.Cos(i * MathF.Tau / 64f), MathF.Sin(i * MathF.Tau / 64f)) * r;
                if (!w.Clear(p, 0.5f) || w.LineOfSight(her, p)) continue;
                hidden = p;
                break;
            }
        if (hidden is not { } at) return;
        var swim = Vector2.Normalize(her - at);
        Swim(w, swim);
        var m = Murkling(w, at);
        float full = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, swim, (m, at));
        Assert.True(w.InBeam(at, 0f, m.Radius));
        Assert.Equal(full, m.Corruption.Value);
        Assert.False(w.InHerLight(m));
    }

    [Fact]
    public void Lantern_pearl_lengthens_her_beam()
    {
        var w = Bare();
        float reach = w.BeamReach;
        Assert.Equal(HerLightTuning.BeamReach, reach, 3);
        w.Run.Add("lantern_pearl");
        Assert.True(w.BeamReach > reach);
    }
}
