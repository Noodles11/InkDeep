using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her stats as the pause menu shows them: what she actually has now (in the plane's own units: damage per shot,
/// shots a second, metres, seconds), and how far each is from her bare base (no pearls). Read from the same loadout
/// and tuning the sim uses, so the sheet is exactly what plays.
/// </summary>
public static class PlaneStatSheet
{
    /// <summary>
    /// One stat: its name, its value (formatted), its change from her base (0: none), whether lower is better, and
    /// whether it is neither better nor worse (her beam's width: a lance is narrow, a floodlight wide).
    /// </summary>
    public readonly record struct Line(string Name, string Value, float Change, bool LowerIsBetter = false, bool Neutral = false);

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static List<Line> Of(PlaneRun run)
    {
        var t = run.Tuning;
        var stats = run.Loadout.Stats;
        var shot = run.Loadout.Shot;
        var bare = StatBlock.Base(t);
        bare.Apply(Array.Empty<StatMod>());
        float Share(float now, float was) => was > 0f ? now / was - 1f : 0f;

        float damage = stats.Damage / MathF.Max(t.Damage, 0.01f);
        float rate = stats.FireRate / MathF.Max(t.FireRate, 0.01f);
        float carry = stats.ShotSpeed / MathF.Max(t.ShotSpeed, 0.01f);
        float reach = BeamReach(run), width = BeamWidthDeg(run);
        float swim = t.PlaneCruiseSpeed * stats.Speed;
        float dash = t.DashCooldown * stats[Stat.DashCooldown];
        bool minigun = run.Loadout.Flags.Contains("minigun");

        var lines = new List<Line>
        {
            new("Max HP", F(run.MaxHp, "0"), Share(run.MaxHp, bare.MaxHp)),
            new("Damage", "×" + F(damage, "0.##"), damage - 1f),
            // (Her fire rate is her beam's tick rate, her shot speed its carry: docs/proposals/BEAM-PEARLS-PLAN.md.)
            new("Beam tick rate", "×" + F(rate, "0.##") + (minigun ? $" (burn ×{F(RhythmTuning.SpinBurn, "0.#")} spun up)" : ""), rate - 1f),
            new("Beam carry", "×" + F(carry, "0.##"), carry - 1f),
            new("Beam reach", F(reach, "0.#") + " m", Share(reach, LightTuning.BeamReach)),
            new("Beam width", F(width, "0") + "°", Share(width, LightTuning.BeamHalfAngleDeg * 2f), Neutral: true),
            new("Swim speed", F(swim, "0.#") + " m/s", Share(swim, t.PlaneCruiseSpeed)),
            new("Dash cooldown", F(dash, "0.##") + " s", Share(dash, t.DashCooldown), LowerIsBetter: true),
            new("Light pressure", "×" + F(stats[Stat.Knockback], "0.##"), stats[Stat.Knockback] - 1f),
            new("Glow", "×" + F(stats[Stat.Glow], "0.##"), stats[Stat.Glow] - 1f),
            new("Luck", F(stats.Luck, "0.#"), stats.Luck - bare.Luck),
        };
        if (stats[Stat.Regen] > 0f) lines.Add(new("Mending", F(stats[Stat.Regen], "0.##") + " HP/s", stats[Stat.Regen]));
        if (shot.CritChance > 0f) lines.Add(new("Critical hits", $"{F(shot.CritChance * 100f, "0")}% for ×{F(shot.CritMult, "0.#")}", shot.CritChance));
        if (run.Active is not null) lines.Add(new("Active recharge", "×" + F(stats[Stat.ActiveRecharge], "0.##"), stats[Stat.ActiveRecharge] - 1f));
        return lines;
    }

    /// <summary>Her beam's reach as SyncLights shapes it (m): her glow and range, a Lance's length.</summary>
    public static float BeamReach(PlaneRun run)
    {
        var stats = run.Loadout.Stats;
        return LightTuning.BeamReach * MathF.Sqrt(MathF.Max(stats[Stat.Glow], 0.1f)) * MathF.Sqrt(MathF.Max(stats.Range / MathF.Max(run.Tuning.Range, 0.01f), 0.1f))
            * (run.Loadout.Shot.Laser ? GreatLightTuning.LanceReach : 1f);
    }

    /// <summary>Her beam's whole width as SyncLights shapes it, unfocused (degrees): her shot size, a Lance, Shark Mode.</summary>
    public static float BeamWidthDeg(PlaneRun run)
    {
        var shot = run.Loadout.Shot;
        float half = MathF.Min(LightTuning.BeamHalfAngleDeg * MathF.Sqrt(MathF.Max(run.Loadout.Stats[Stat.ShotSize], 0.1f)), LightTuning.MaxHalfAngleDeg)
            * MathF.Sqrt(MathF.Max(shot.SizeMult, 0.1f)) * (shot.Laser ? GreatLightTuning.LanceWidth : 1f)
            * (run.Loadout.Has("shark_mode") ? GreatLightTuning.SharkNarrow : 1f);
        return half * 2f;
    }

    /// <summary>
    /// Her light read back in words (docs/proposals/LIGHT-GRAMMAR.md §11, "the sentence"): for each kind of word she
    /// has, the words — its shape, its paths, its colours, its rhythms, its echoes, her glow's voice, and the great
    /// lights and constellations she has named. Kinds she has nothing of are left out.
    /// </summary>
    public static List<(string Kind, string Words)> Words(PlaneRun run)
    {
        var l = run.Loadout;
        var s = l.Shot;
        var flags = l.Flags;
        var words = new List<(string, string)>();
        void Say(string kind, List<string> said)
        {
            if (said.Count > 0) words.Add((kind, string.Join(", ", said)));
        }

        var shape = new List<string>();
        if (s.Laser) shape.Add("lance");
        if (s.Pattern == ShotPattern.Kraken8) shape.Add("crown of 8");
        else if (s.Multishot > 1)
            shape.Add(s.Pattern == ShotPattern.Cone ? $"floodlight of {s.Multishot}" : s.Wave ? $"twin weave of {s.Multishot}" : $"{s.Multishot} rays");
        if (s.Rear) shape.Add("wake behind her");
        if (s.Spiral) shape.Add(l.Has("lighthouse") ? "a second lance turning round her" : "orbit");
        Say("Shape", shape.Count > 0 ? shape : new List<string> { "one beam" });

        var path = new List<string>();
        if (s.Bounces > 0) path.Add(s.Bounces == 1 ? "bounce" : $"bounce ×{s.Bounces}");
        if (s.Split) path.Add("fork");
        if (s.Boomerang) path.Add("return");
        if (s.Spectral) path.Add("ghost");
        if (s.Pierce) path.Add("pierce");
        if (s.Homing) path.Add("lean");
        if (s.Grow) path.Add("flare");
        if (flags.Contains("linger")) path.Add("linger");
        if (s.ChainTargets > 0) path.Add($"arc to {s.ChainTargets}");
        Say("Paths", path);

        var colour = new List<string>();
        if (l.Has("neon_rave")) colour.Add("all seven in turn");
        else
        {
            if (s.Ember > 0) colour.Add("ember");
            if (s.Bloom > 0) colour.Add("bloom");
            if (s.Frost > 0) colour.Add("frost");
            int n = colour.Count;
            if (n > 1)
            {
                string mix = n == 3 ? "daybreak" : s.Ember > 0 && s.Bloom > 0 ? "sunfire" : s.Ember > 0 ? "shatter" : "lull";
                colour = new List<string> { string.Join(" + ", colour) + $" ({mix})" };
            }
        }
        Say("Colour", colour);

        var rhythm = new List<string>();
        if (s.Charge) rhythm.Add("focus");
        if (flags.Contains("minigun")) rhythm.Add("spin-up");
        if (flags.Contains("cavitation")) rhythm.Add("snap");
        if (System.Linq.Enumerable.Range(0, PlaneRun.SlotCount).Any(i => run.SlotActive(i)?.Active?.Action == ActiveAction.GlowBurst)) rhythm.Add("pulse (Glow Burst)");
        Say("Rhythm", rhythm);

        var echo = new List<string>();
        if (s.Explosive) echo.Add("flare");
        if (l.Count("fire_urchin_spine") > 0) echo.Add("spines");
        if (l.Count("lamprey_mouth") > 0) echo.Add("gather");
        if (l.Has("shark_mode")) echo.Add("frenzy");
        if (l.Has("pirate")) echo.Add("gilded drops");
        Say("Echo", echo);

        var glow = new List<string>();
        int flies = (int)l.Stats[Stat.Fireflies];
        if (flies > 0) glow.Add(flies == 1 ? "1 firefly" : $"{flies} fireflies");
        if (l.Has("coral_reef")) glow.Add("mending afterlight");
        if (flags.Contains("blackSun")) glow.Add("black sun");
        Say("Glow", glow);

        var named = new List<string>();
        if (run.Catalog is { } catalog)
        {
            foreach (var t in catalog.Transformations) if (l.Transformations.Contains(t.Id)) named.Add(t.Name);
            foreach (var y in catalog.Synergies) if (l.Synergies.Contains(y.Id)) named.Add(y.Name);
        }
        Say("Named", named);
        return words;
    }

    static string F(float v, string format) => v.ToString(format, Inv);
}
