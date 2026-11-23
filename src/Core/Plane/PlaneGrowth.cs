using System;
using System.Collections.Generic;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// What she can grow with her light's energy (the specks she gathers, PlaneSpecks.cs): her light's characteristics (its
/// power, coherence, pulse, beam width, and the mix of its red, green and blue bands) and her body's (her health, her dash's
/// cooldown, her swimming speed). Each is a float: the energy laid on it, any amount, for good.
/// </summary>
public enum GrowthStat { Power, Coherence, Pulse, Width, Red, Green, Blue, MaxHealth, DashCooldown, Speed }

/// <summary>What a unit of energy laid on each characteristic does (docs/GAME.md). (tune)</summary>
public static class GrowthTuning
{
    /// <summary>Her light's power × (1 + Power a unit); its coherence + Coherence a unit (to 1); its pulse + Pulse Hz a unit; her beam's width × (1 + Width a unit).</summary>
    public const float Power = 0.08f, Coherence = 0.04f, Pulse = 0.3f, Width = 0.05f;
    /// <summary>A band's share of her light: + Band a unit (the mix only: her light's total is kept).</summary>
    public const float Band = 0.12f;
    /// <summary>Her health + MaxHealth a unit; her dash's cooldown − DashCooldown a unit (of its stat); her swim + Speed a unit.</summary>
    public const float MaxHealth = 4f, DashCooldown = 0.03f, Speed = 0.03f;
    /// <summary>The least her dash's cooldown stat comes to, however much is laid on it.</summary>
    public const float DashLeast = 0.35f;

    public static readonly GrowthStat[] Light = { GrowthStat.Power, GrowthStat.Coherence, GrowthStat.Pulse, GrowthStat.Width, GrowthStat.Red, GrowthStat.Green, GrowthStat.Blue };
    public static readonly GrowthStat[] Body = { GrowthStat.MaxHealth, GrowthStat.DashCooldown, GrowthStat.Speed };

    public static string Name(GrowthStat s) => s switch
    {
        GrowthStat.Power => "Power",
        GrowthStat.Coherence => "Coherence",
        GrowthStat.Pulse => "Pulse",
        GrowthStat.Width => "Beam width",
        GrowthStat.Red => "Red band",
        GrowthStat.Green => "Green band",
        GrowthStat.Blue => "Blue band",
        GrowthStat.MaxHealth => "Max health",
        GrowthStat.DashCooldown => "Dash cooldown",
        _ => "Swim speed",
    };

    /// <summary>What so much energy on a characteristic gives, as the drawer shows it.</summary>
    public static string Effect(GrowthStat s, float units) => s switch
    {
        GrowthStat.Power => $"+{units * Power * 100f:0.#}%",
        GrowthStat.Coherence => $"+{units * Coherence:0.00}",
        GrowthStat.Pulse => $"{units * Pulse:0.0} Hz",
        GrowthStat.Width => $"+{units * Width * 100f:0.#}%",
        GrowthStat.Red or GrowthStat.Green or GrowthStat.Blue => $"+{units * Band:0.00}",
        GrowthStat.MaxHealth => $"+{units * MaxHealth:0.#}",
        GrowthStat.DashCooldown => $"-{units * DashCooldown * 100f:0.#}%",
        _ => $"+{units * Speed * 100f:0.#}%",
    };

    /// <summary>Her body's growth as stat changes (PlaneRun.Rebuild): health, dash cooldown, swim speed.</summary>
    public static IEnumerable<StatMod> Mods(PlaneRun run)
    {
        float hp = run.Grown(GrowthStat.MaxHealth), dash = run.Grown(GrowthStat.DashCooldown), speed = run.Grown(GrowthStat.Speed);
        if (hp > 0f) yield return new StatMod { Stat = Stat.MaxHp, Op = StatOp.Add, Value = hp * MaxHealth };
        if (dash > 0f) yield return new StatMod { Stat = Stat.DashCooldown, Op = StatOp.Mult, Value = MathF.Max(DashLeast, 1f - dash * DashCooldown) };
        if (speed > 0f) yield return new StatMod { Stat = Stat.Speed, Op = StatOp.Mult, Value = 1f + speed * Speed };
    }

    /// <summary>Her light's growth laid over the light her pearls (or the F1 gauges) make of it.</summary>
    public static LightSettings Grow(LightSettings light, PlaneRun run)
    {
        float Band(GrowthStat s) => run.Grown(s) * GrowthTuning.Band;
        var sp = light.Spectrum;
        float total = sp.Total;
        var mixed = new Spectrum(sp.R + Band(GrowthStat.Red) * total, sp.G + Band(GrowthStat.Green) * total, sp.B + Band(GrowthStat.Blue) * total);
        return new LightSettings
        {
            Spectrum = mixed,
            Power = light.Power * (1f + run.Grown(GrowthStat.Power) * Power),
            Coherence = MathF.Min(1f, light.Coherence + run.Grown(GrowthStat.Coherence) * Coherence),
            Width = light.Width * (1f + run.Grown(GrowthStat.Width) * Width),
            PulseHz = MathF.Min(LightSettings.PulseMost, light.PulseHz + run.Grown(GrowthStat.Pulse) * Pulse),
        };
    }

    /// <summary>Whether she has grown her light at all.</summary>
    public static bool LightGrown(PlaneRun run)
    {
        foreach (var s in Light)
            if (run.Grown(s) > 0f) return true;
        return false;
    }
}

public sealed partial class PlaneWorld
{
    /// <summary>
    /// She may lay her energy on her characteristics now (the upgrade drawer): she has some, and she is at a place that
    /// allows it (for now a light spring, the healing pool, spent or not).
    /// </summary>
    public bool CanGrow => Run.Energy > 0f && !Defeated && AtGrowthPlace;

    /// <summary>She is at a place that allows growing (a light spring she is bathing in).</summary>
    public bool AtGrowthPlace => Springs.Exists(s => s.Bathing);

    /// <summary>Lays energy on her characteristics (PlaneRun.Grow), where she may; false if she may not, or it is more than she has.</summary>
    public bool Grow(IReadOnlyDictionary<GrowthStat, float> amounts) => AtGrowthPlace && Run.Grow(amounts);
}
