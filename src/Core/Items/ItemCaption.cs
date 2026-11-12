using System.Collections.Generic;
using System.Globalization;
using OctoShoots.Core.Plane;

namespace OctoShoots.Core.Items;

/// <summary>
/// One line per real effect, with its exact numbers (2D §29): what it does to her own base stats ("+8.6% damage",
/// "+50% damage", "−15% dash cooldown", "+20 max HP") and the real reach, share and timing of what her shots do
/// ("…60% of its damage to everything within 1.8 m"), "12% chance to freeze foes for 1.6s", "Recharges in 30s".
/// </summary>
public static class ItemCaption
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Her base stats with no pearls (the default tuning): what a pearl's additions are a share of.</summary>
    static readonly StatBlock Baseline = BaselineStats();

    static StatBlock BaselineStats()
    {
        var b = StatBlock.Base(new Tuning());
        b.Apply(System.Array.Empty<StatMod>());
        return b;
    }

    /// <summary>Stats whose additions read best as a share of her own (the rest are counts: HP, luck, regeneration).</summary>
    static bool AsShare(Stat s) => s is Stat.Damage or Stat.FireRate or Stat.ShotSpeed or Stat.Range or Stat.Speed;

    /// <summary>A signed percentage: one decimal under 10%, whole above.</summary>
    static string SignedPct(float share)
    {
        float v = share * 100f;
        string n = System.MathF.Abs(v) < 10f ? System.MathF.Abs(v).ToString("0.#", Inv) : System.MathF.Abs(v).ToString("0", Inv);
        return (v >= 0f ? "+" : "−") + n + "%";
    }

    public static List<string> Describe(EffectBundle bundle)
    {
        var lines = new List<string>();
        foreach (var m in bundle.Stats) lines.Add(StatLine(m));
        if (bundle.Shot is { } s) ShotLines(s, lines);
        foreach (var e in bundle.Effects) lines.Add(EffectLine(e));
        foreach (var f in bundle.Flags) lines.Add(FlagLine(f));
        if (bundle is ItemDef { Active: { } a }) lines.Add(ActiveLine(a));
        return lines;
    }

    static string N(float v) => v.ToString("0.##", Inv);

    static string Pct(float v) => (v * 100f).ToString("0", Inv) + "%";

    static string StatName(Stat s) => s switch
    {
        Stat.MaxHp => "max HP",
        Stat.Damage => "damage",
        Stat.FireRate => "beam tick rate",
        Stat.ShotSpeed => "beam carry",
        Stat.Range => "range",
        Stat.Speed => "swim speed",
        Stat.Luck => "luck",
        Stat.ShotSize => "shot size",
        Stat.Sink => "idle sink",
        Stat.DashCooldown => "dash cooldown",
        Stat.Glow => "glow radius",
        Stat.EnemyShotSpeed => "enemy shot speed",
        Stat.Knockback => "light pressure",
        Stat.Regen => "HP mended per second",
        Stat.BubbleCapacity => "bubbles on the tentacle",
        Stat.BubbleRegrow => "bubble regrowth speed",
        Stat.BubblesPerThrow => "bubbles per throw",
        Stat.ActiveRecharge => "active item recharge speed",
        Stat.BubbleHover => "bubble hover time",
        _ => s.ToString(),
    };

    static string StatLine(StatMod m)
    {
        if (m.Op == StatOp.Mult)
        {
            if (m.Stat == Stat.Sink && m.Value == 0f) return "No idle sinking";
            return $"{SignedPct(m.Value - 1f)} {StatName(m.Stat)}";
        }
        // An addition to one of her rates or sizes: as a share of her own (+0.3 damage on 3.5 is +8.6%).
        float b = Baseline[m.Stat];
        if (AsShare(m.Stat) && b > 0f) return $"{SignedPct(m.Value / b)} {StatName(m.Stat)}";
        return $"{(m.Value >= 0 ? "+" : "−")}{N(System.MathF.Abs(m.Value))} {StatName(m.Stat)}";
    }

    static void ShotLines(ShotSpec s, List<string> lines)
    {
        if (s.Multishot > 1)
        {
            lines.Add(s.Pattern switch
            {
                ShotPattern.Cone => $"Opens the beam into a {s.Multishot}-ray floodlight, each {Pct(PlaneCombatTuning.VolleyShot(s.Multishot))} of the beam",
                ShotPattern.Kraken8 => "Short beams shine all round you, 8 ways",
                _ => $"Splits the beam into {s.Multishot} rays, each {Pct(PlaneCombatTuning.VolleyShot(s.Multishot))} of the beam",
            });
        }
        else if (s.Pattern == ShotPattern.Kraken8)
        {
            lines.Add("Short beams shine all round you, 8 ways");
        }
        if (s.Homing) lines.Add($"Shots home onto foes within {N(PlaneCombatTuning.HomingReach)} m (turning {N(PlaneCombatTuning.HomingDegPerSec)}° a second)");
        if (s.Pierce) lines.Add("Shots pierce foes (each foe hit once)");
        // Her light's paths on rock (PlanePaths.cs).
        if (s.Spectral) lines.Add($"Her light passes through a wall up to {N(PathTuning.GhostThick)} m thick, {Pct(PathTuning.Ghost)} as strong beyond");
        if (s.Bounces > 0) lines.Add(s.Bounces == 1 ? $"Her light bounces off rock ({Pct(PathTuning.Bounce)} of it)" : $"Her light bounces off rock {s.Bounces}× ({Pct(PathTuning.Bounce)} of it each time)");
        if (s.Split) lines.Add($"Where her light lands on rock it forks in two ({N(PathTuning.ForkDeg * 2)}° apart, {Pct(PathTuning.Fork)} of it each)");
        if (s.Boomerang) lines.Add($"Her light that lands on rock comes back along its way ({Pct(PathTuning.Return)} of it): what stands in it is lit from both sides");
        if (s.Wave) lines.Add("The beam's rays weave across your aim in a double helix");
        if (s.Spiral) lines.Add("Shots spiral outward");
        if (s.Grow) lines.Add($"Shots grow with distance: up to ×{N(PlaneCombatTuning.GrowMax)} size and damage at full range");
        if (s.Explosive) lines.Add($"Shots burst into light where they pop: {Pct(PlaneCombatTuning.BurstDamage)} of their damage to every corrupted thing within {N(PlaneCombatTuning.BurstRadius)} m, cleansing the ground");
        if (s.Charge) lines.Add($"Held steady, her beam focuses: over {N(RhythmTuning.FocusTime)} s it narrows and burns up to ×{N(RhythmTuning.FocusBurn)}; swinging it loses the focus");
        if (s.Laser) lines.Add("Her beam reaches half as far again, narrowed to a searchlight");
        if (s.Rear) lines.Add("A fainter beam shines out of her back too");
        if (s.ChainTargets > 0) lines.Add($"What her beam burns arcs its burn to {s.ChainTargets} more within {N(s.ChainRange)} m, at half the rate");
        if (s.Ember > 0) lines.Add($"Her light turns ember: what it lights keeps burning {N(ColourTuning.Ember(s.Ember))} s after it leaves");
        if (s.Bloom > 0) lines.Add($"Her light turns to bloom: ink and ash clear ×{N(ColourTuning.Bloom(ColourTuning.BloomClear, s.Bloom))} as fast in it, and a murkling it frees leaves ×{N(ColourTuning.Bloom(ColourTuning.BloomCore, s.Bloom))} the light");
        if (s.Frost > 0) lines.Add($"Her light turns to frost: what it lights slows, and stands still after {N(ColourTuning.Frost(s.Frost))} s");
        if (s.FreezeChance > 0) lines.Add($"{Pct(s.FreezeChance)} chance to freeze foes for {N(StatusRules.FreezeTime)}s");
        if (s.BurnChance > 0) lines.Add($"{Pct(s.BurnChance)} chance to burn foes for {N(StatusRules.BurnTime)}s");
        if (s.PoisonChance > 0) lines.Add($"{Pct(s.PoisonChance)} chance to poison foes for {N(StatusRules.PoisonTime)}s");
        if (s.CharmChance > 0) lines.Add($"{Pct(s.CharmChance)} chance to charm foes for {N(StatusRules.CharmTime)}s");
        if (s.SlowChance > 0) lines.Add($"{Pct(s.SlowChance)} chance to slow foes for {N(StatusRules.SlowTime)}s");
        if (s.CritChance > 0) lines.Add($"{Pct(s.CritChance)} chance of a ×{N(s.CritMult)} critical hit");
        if (s.SizeMult != 1f) lines.Add($"{SignedPct(s.SizeMult - 1f)} shot size");
        if (s.DamageMult != 1f) lines.Add($"{SignedPct(s.DamageMult - 1f)} damage per shot");
    }

    static string EffectLine(TriggerEffect e)
    {
        string chance = e.Chance < 1f ? $"{Pct(e.Chance)} chance: " : "";
        string when = e.Trigger switch
        {
            Trigger.OnPickup => "",
            Trigger.OnKill => "Kills ",
            Trigger.OnHit => "Hits ",
            Trigger.OnShoot => "Shots ",
            Trigger.OnDamaged => "When hurt, ",

            Trigger.OnFloorStart => "Each new reef ",
            _ => "",
        };
        string what = e.Action switch
        {
            EffectAction.Heal => e.Trigger == Trigger.OnPickup ? $"Heals {N(e.Value)} HP" : $"heal {N(e.Value)} HP",
            EffectAction.Foam => e.Trigger == Trigger.OnPickup ? $"+{N(e.Value)} foam HP" : $"gives {N(e.Value)} foam HP",
            EffectAction.Coins => e.Trigger == Trigger.OnPickup ? $"+{N(e.Value)} sand dollars" : $"drop {N(e.Value)} sand dollars",
            EffectAction.Bombs => e.Trigger == Trigger.OnPickup ? $"+{N(e.Value)} ink bombs" : $"give {N(e.Value)} ink bombs",
            EffectAction.Frenzy => $"send you into a frenzy: ×{N(e.Value)} fire rate for {N(e.Duration)}s",
            EffectAction.Shards => $"burst into {N(e.Value)} spines",

            _ => e.Action.ToString(),
        };
        return chance + when + what;
    }

    static string FlagLine(string flag) => flag switch
    {
        "magnet" => $"Pulls pickups and freed murklings' light toward you from {N(PlaneEconomyTuning.RemoraMagnet)} m (not {N(PlaneEconomyTuning.ShellMagnet)} m)",
        "minigun" => $"Held without a break, her beam spins up: ×{N(RhythmTuning.SpinBurn)} burn after {N(RhythmTuning.SpinTime)} s",
        "cavitation" => $"Held on a creature, her beam snaps every {N(RhythmTuning.SnapEvery)} s: a burst of light {N(RhythmTuning.SnapReach)} m wide that pushes it back",
        "mergeBubbles" => $"Her light lingers at the end of its range; shots that touch merge into one, adding their damage (+{Pct(PlaneCombatTuning.BubbleGrowth)} size for each)",
        "severedNerve" => "A severed strand circles you, stinging as your arm does",
        "moltenCarapace" => "Under half health your shots pierce and do 60% more",
        "lastLight" => "Once a level, a killing blow leaves you at 1 HP in a flood of light",
        "clawTithe" => "Each cut the crab has taken adds 25% damage",
        "surge" => $"Fire pours out a continuous surge of light instead of shots: {Pct(SurgeTuning.Dps)} of your shots' damage per second to all it touches, clearing a little corruption where it ends. Your other pearls shape it",
        "landmarks" => "Every place shows on the maps before you reach it (secret rooms stay hidden)",
        "minimap" => "Shows the minimap: the reef round you, top right",
        "inkTrail" => "Leaves an ink trail",
        "colorCycle" => "Shots cycle through neon colours",
        "richDrops" => "Foes drop more sand dollars",
        "richCaches" => "Shell caches hold 50% more",
        _ => flag,
    };

    static string ActiveLine(ActiveSpec a) => a.Action switch
    {
        ActiveAction.PushWave => $"Three waves push everything loose {N(a.Radius)} m away from her; recharges in {N(a.Recharge)}s",
        ActiveAction.GlowBurst => $"For {N(a.Duration)} s her beam beats, burning ×{N(a.Value)} on each beat; recharges in {N(a.Recharge)}s",
        _ => $"Recharges in {N(a.Recharge)}s",
    };
}
