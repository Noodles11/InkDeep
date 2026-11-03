using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her shots' light burns and dazzles the corrupted it reaches; her own light burns nothing (docs/ADVENTURE.md §8; PlaneHerLight.cs).</summary>
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

    /// <summary>A plain shot of hers hanging at her start (held there by Step), its light out to ShotReach.</summary>
    static PlaneShot Shot(PlaneWorld w)
    {
        var s = new PlaneShot { FromPlayer = true, Damage = PlaneCombatTuning.ShotDamage, Life = 100f, Range = 100f, Hover = 100f };
        w.Shots.Add(s);
        return s;
    }

    /// <summary>
    /// A step with her held still at the level's start, full of light, her shots held there too (alive), and the
    /// murklings held where they are (pinned) when asked.
    /// </summary>
    static void Step(PlaneWorld w, params (Murkling M, Vector2 At)[] pinned)
    {
        foreach (var (m, at) in pinned)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
        }
        var her = w.Map.Start.Position;
        foreach (var s in w.Shots)
        {
            s.Position = s.Line = her;
            s.From = new Vector2(float.NaN);
            s.Velocity = Vector2.UnitX * 0.001f;
            s.Life = 100f;
        }
        w.Player.Position = w.Player.PrevPosition = her;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(default);
    }

    [Fact]
    public void Her_own_light_burns_nothing_beside_her()
    {
        var w = Bare();
        if (Beside(w, 2.5f) is not { } at) return;
        var m = Murkling(w, at);
        float full = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, (m, at));
        Assert.Equal(full, m.Corruption.Value);
        Assert.False(w.InHerLight(m));
    }

    [Fact]
    public void A_shots_light_slowly_burns_what_it_reaches_and_nothing_beyond()
    {
        var w = Bare();
        if (Beside(w, 2.5f) is not { } near || Beside(w, HerLightTuning.ShotReach + 1.5f) is not { } far) return;
        var shot = Shot(w);
        var lit = Murkling(w, near);
        var dark = Murkling(w, far);
        float full = lit.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, (lit, near), (dark, far));
        Assert.Equal(full - HerLightTuning.Burn, lit.Corruption.Value, 1);
        Assert.True(w.InHerLight(lit));
        Assert.True(w.LitFrom(lit) is { } from && Vector2.Distance(from, w.Map.Start.Position) < 0.1f);
        Assert.Contains(shot, w.Burners);
        Assert.Equal(full, dark.Corruption.Value);
        Assert.False(w.InHerLight(dark));
        // Steady, not hits: no flash, no numbers.
        Assert.Equal(0f, lit.Corruption.LastHit);

        // Kept in it, it is freed.
        for (int i = 0; i < (full / HerLightTuning.Burn + 1f) * PlaneWorld.TickRate && lit.Alive; i++) Step(w, (lit, near), (dark, far));
        Assert.False(lit.Alive);
    }

    [Fact]
    public void Two_shots_lighting_it_burn_it_twice_as_fast()
    {
        var w = Bare();
        if (Beside(w, 2.5f) is not { } near) return;
        Shot(w);
        Shot(w);
        var m = Murkling(w, near);
        float full = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, (m, near));
        Assert.Equal(full - 2f * HerLightTuning.Burn, m.Corruption.Value, 1);
    }

    [Fact]
    public void Coming_into_a_shots_light_dazzles_a_murkling_still_for_a_moment_then_it_carries_on()
    {
        var w = Bare();
        if (Beside(w, 3f) is not { } at) return;
        var m = Murkling(w, at);
        Step(w, (m, at));
        Shot(w);
        Step(w, (m, at));
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dazzled);
        Assert.True(w.Dazzled(m));
        // Dazzled, it holds still...
        for (int i = 0; i < (int)(HerLightTuning.Dazzle * PlaneWorld.TickRate) - 3; i++) Step(w);
        Assert.Equal(at, m.Position);
        // ...then carries on, never dazzled again while it stays in the light.
        int again = 0;
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            Step(w);
            again += w.Events.Count(e => e.Type == PlaneEventType.Dazzled);
        }
        Assert.False(w.Dazzled(m));
        Assert.True(Vector2.Distance(m.Position, at) > 0.3f, "it moves on once the dazzle has passed");
        Assert.Equal(0, again);
    }

    [Fact]
    public void A_murkling_out_of_the_light_long_enough_is_dazzled_again()
    {
        var w = Bare();
        if (Beside(w, 2.5f) is not { } near || Beside(w, HerLightTuning.ShotReach + 2f) is not { } away) return;
        Shot(w);
        var m = Murkling(w, near);
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, (m, near));
        // Out of it, but not long enough: no dazzle on coming back.
        for (int i = 0; i < (int)(HerLightTuning.Rearm * PlaneWorld.TickRate) / 2; i++) Step(w, (m, away));
        Step(w, (m, near));
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.Dazzled);
        // Out of it long enough: dazzled again.
        for (int i = 0; i < (int)(HerLightTuning.Rearm * PlaneWorld.TickRate) + 2; i++) Step(w, (m, away));
        Step(w, (m, near));
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dazzled);
    }

    [Fact]
    public void Rock_between_a_shot_and_a_murkling_keeps_its_light_off_it()
    {
        var w = Bare();
        var her = w.Map.Start.Position;
        Vector2? hidden = null;
        for (float r = 1.5f; r < HerLightTuning.ShotReach && hidden is null; r += 0.5f)
            for (int i = 0; i < 64; i++)
            {
                var p = her + new Vector2(MathF.Cos(i * MathF.Tau / 64f), MathF.Sin(i * MathF.Tau / 64f)) * r;
                if (!w.Clear(p, 0.5f) || w.LineOfSight(her, p)) continue;
                hidden = p;
                break;
            }
        if (hidden is not { } at) return;
        Shot(w);
        var m = Murkling(w, at);
        float full = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, (m, at));
        Assert.Equal(full, m.Corruption.Value);
        Assert.False(w.InHerLight(m));
    }

    [Fact]
    public void A_harder_shot_lights_farther_and_Lantern_pearl_widens_her_own_light()
    {
        var plain = new PlaneShot { Damage = PlaneCombatTuning.ShotDamage };
        var hard = new PlaneShot { Damage = PlaneCombatTuning.ShotDamage * 4f };
        Assert.Equal(HerLightTuning.ShotReach, PlaneWorld.ShotLightReach(plain), 3);
        Assert.Equal(HerLightTuning.ShotReach * 2f, PlaneWorld.ShotLightReach(hard), 3);
        var w = Bare();
        float reach = w.LightReach;
        Assert.Equal(HerLightTuning.Reach, reach, 3);
        w.Run.Add("lantern_pearl");
        Assert.True(w.LightReach > reach);
    }
}
