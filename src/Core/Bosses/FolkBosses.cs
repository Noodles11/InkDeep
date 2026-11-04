using System;
using System.Numerics;

namespace OctoShoots.Core.Bosses;

/// <summary>
/// The bay's folk as the Leak made them (docs/NPC-BOSSES-PROPOSAL.md §4–8): authored bosses, each the same for every
/// jelly, waiting at the bottom of her great cave. Their meters grow with the cave's level as every creature's does on
/// the open reef (ReefTuning.MeterPerLevel); her harm there grows with it too.
/// </summary>
public static class FolkBosses
{
    /// <summary>A folk boss's meter at level 1, and the share each level past it adds.</summary>
    public const float Meter = 700f, MeterPerLevel = 0.25f;

    /// <summary>The boss a neighbour is, met at her cave's level and Menace (null: her boss is not in the game yet).</summary>
    public static BossSpec? For(string folk, int level, float menace) => folk switch
    {
        "tull" => Tull(level, menace),
        _ => null,
    };

    static float MeterAt(int level) => Meter * (1f + MeterPerLevel * Math.Max(level - 1, 0));

    static ulong SeedOf(string folk)
    {
        ulong h = 1469598103934665603UL;
        foreach (char c in "folk/" + folk) h = (h ^ c) * 1099511628211UL;
        return h;
    }

    /// <summary>
    /// Sleepy Old Tull (§4.2): a green turtle under a shell grown past her, plate on plate. Slam and Sigh: she sweeps a
    /// flipper at where she last felt Clementine, lifts the shell and drops it in a ring of ink, then sighs, her head out
    /// under the lip and the pearl at her throat open. Half asleep, she feels the water (vibration); she creeps toward
    /// her between acts and plants herself to strike. At half her meter a seam cracks open and the slams come in pairs.
    /// </summary>
    static BossSpec Tull(int level, float menace)
    {
        var spec = new BossSpec
        {
            Seed = SeedOf("tull"), Cycle = 1, Depth = 1, Menace = menace, Tier = BossTier.Common,
            Plan = BodyPlan.Reptile, Form = BodyForm.Turtle, Concept = BossConcept.SlamAndSigh, Blockade = true,
            Name = "Sleepy Old Tull", Folk = "tull",
            // 5 m long mutated (the shell's disc 4.8 m, stretched along her), 3.2 m freed.
            Radius = 2.4f, Stretch = 1.15f, Count = 4, Bulk = 0.6f, TrueScale = 0.64f,
            // An olive-brown shell; grey-green skin with pale scale edges.
            TrueColor = new Vector3(0.36f, 0.3f, 0.17f), Accent = new Vector3(0.48f, 0.55f, 0.4f),
            Theme = MutationTheme.OvergrownShell, Mutation = 0.35f,
            // At her throat, under the shell's front lip: open only in the sigh.
            Exposure = Exposure.Stagger, Pearl = new BodySite(0f, 1.02f, 0.6f, false), PearlRadius = 0.75f,
            Hp = MeterAt(level), Damage = 12f, Telegraph = 1f, Rest = 2.2f, Volleys = 2, VolleyInterval = 1f, Window = 2f,
            PearlSpeed = 6f, RingCount = 16, WallCount = 0, HelixArms = 3,
            Signature = BossAttack.Ring, Move = BossMove.Creep, MoveRate = 1.2f, Tempo = BossTempo.SurgeAndRest,
            Sense = BossSense.Vibration, ActsStill = true, Spice = BossSpice.FastLongWindows,
        };
        spec.Fillers.Add(BossAttack.Fan);
        // The flipper sweep, then the slam; from her second phase, the sweep and two slams.
        spec.Acts.Add(new[] { BossAttack.Fan, BossAttack.Ring });
        spec.Acts.Add(new[] { BossAttack.Fan, BossAttack.Ring, BossAttack.Ring });
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.MutationAdvances, null));
        // The overgrowth: plates piled along her shell's seams, thickest over her back.
        spec.Sites.Add(new BodySite(MathF.PI, 0.55f, 0.5f, false));
        spec.Sites.Add(new BodySite(MathF.PI * 0.7f, 0.65f, 0.4f, false));
        spec.Sites.Add(new BodySite(-MathF.PI * 0.7f, 0.65f, 0.4f, false));
        spec.Sites.Add(new BodySite(MathF.PI * 0.35f, 0.6f, 0.35f, false));
        spec.Sites.Add(new BodySite(-MathF.PI * 0.35f, 0.6f, 0.35f, false));
        return spec;
    }
}
