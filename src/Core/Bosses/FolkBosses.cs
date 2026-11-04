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
        "kip" => Kip(level, menace),
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

    /// <summary>
    /// Nine-Fin Kip (§5.2): a cleaner wrasse with fins crowding along her flanks, bobbing over her station in the
    /// arena's middle. The cleaning dance: she flicks a ring of drops from her fins, then darts straight at Clementine
    /// and on past her, and turns side-on, the pearl on her stripe toward her, as if waiting for a client to hold still.
    /// She senses her through the water. At half her meter she darts twice in a row, and her queue comes: six of the
    /// fish she used to clean, turned, cling to Clementine.
    /// </summary>
    static BossSpec Kip(int level, float menace)
    {
        var spec = new BossSpec
        {
            Seed = SeedOf("kip"), Cycle = 1, Depth = 1, Menace = menace, Tier = BossTier.Common,
            Plan = BodyPlan.Fish, Form = BodyForm.Wrasse, Concept = BossConcept.CleaningDance, Blockade = true,
            Name = "Nine-Fin Kip", Folk = "kip",
            // 4 m long mutated (a slender body: its disc 2.6 m, stretched along her), 2.4 m freed.
            Radius = 1.3f, Stretch = 1.55f, Count = 9, Bulk = 0.5f, TrueScale = 0.6f,
            // Pale blue-white, the electric-blue stripe and the black band along her (the accent, the groove).
            TrueColor = new Vector3(0.78f, 0.84f, 0.92f), Accent = new Vector3(0.12f, 0.45f, 1f),
            Theme = MutationTheme.ExtraFins, Mutation = 0.4f,
            // On her stripe, on her flank: open only as she waits side-on after a dart.
            Exposure = Exposure.Stagger, Pearl = new BodySite(MathF.PI * 0.5f, 0.45f, 0.5f, false), PearlRadius = 0.7f,
            Hp = MeterAt(level), Damage = 12f, Telegraph = 0.9f, Rest = 2f, Volleys = 2, VolleyInterval = 1f, Window = 1.4f,
            PearlSpeed = 6.5f, RingCount = 14, WallCount = 0, HelixArms = 3,
            Signature = BossAttack.Dart, Move = BossMove.Station, MoveRate = 3f, Tempo = BossTempo.Duelist,
            Sense = BossSense.Water, Clients = 6, Spice = BossSpice.FastLongWindows,
        };
        spec.Fillers.Add(BossAttack.Ring);
        spec.Acts.Add(new[] { BossAttack.Ring, BossAttack.Dart });
        spec.Acts.Add(new[] { BossAttack.Ring, BossAttack.Dart, BossAttack.Dart });
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.Quicken, null));
        // The extra fins: crowding along both flanks, front to back.
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i < 4; i++)
                spec.Sites.Add(new BodySite(side * (0.55f + 0.6f * i), 0.85f, 0.35f + 0.05f * i, false));
        return spec;
    }
}
