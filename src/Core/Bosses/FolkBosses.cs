using System;
using System.Numerics;

namespace OctoShoots.Core.Bosses;

/// <summary>
/// The bay's folk as the Leak made them (docs/FOLK.md §4–8): authored bosses, each the same for every
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
        "fennel" => Fennel(level, menace),
        "ottilie" => Ottilie(level, menace),
        "bubbles" => Bubbles(level, menace),
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

    /// <summary>
    /// Papa Fennel (§6.2): a leafy seadragon, his fronds hardened into thorns curling into him, his clutch of eggs
    /// untouched on his tail. The hidden one: he drifts between five stands of kelp round the arena and hides in one, as
    /// still as the weed, his drip rising out of it. She finds him by swimming near or by her light reaching him; then
    /// he unfurls and lashes an arc of nine drops across 90° at where he last felt her (vibration), and his chest is open
    /// for 1.6 s before he drifts off to another stand. Left alone, he strikes from hiding: the tangle, a wall of drops
    /// with a gap of two, and no opening after. From half his meter two other stands drip too.
    /// </summary>
    static BossSpec Fennel(int level, float menace)
    {
        var spec = new BossSpec
        {
            Seed = SeedOf("fennel"), Cycle = 1, Depth = 2, Menace = menace, Tier = BossTier.Common,
            Plan = BodyPlan.Fish, Form = BodyForm.Seadragon, Concept = BossConcept.TheHiddenOne, Blockade = true,
            Name = "Papa Fennel", Folk = "fennel",
            // 5 m long mutated, 3 m freed.
            Radius = 1.4f, Stretch = 1.75f, Count = 12, Bulk = 0.5f, TrueScale = 0.6f,
            // Ochre-yellow, his leaves green.
            TrueColor = new Vector3(0.78f, 0.6f, 0.25f), Accent = new Vector3(0.42f, 0.6f, 0.26f),
            Theme = MutationTheme.InvertedSpines, Mutation = 0.5f,
            // At his chest, behind the thorns: open only after his lash.
            Exposure = Exposure.Stagger, Pearl = new BodySite(0f, 0.45f, 0.5f, false), PearlRadius = 0.7f,
            Hp = MeterAt(level), Damage = 12f, Telegraph = 1f, Rest = 6f, Volleys = 1, VolleyInterval = 1f, Window = 1.6f,
            PearlSpeed = 6.5f, RingCount = 0, WallCount = 14, HelixArms = 3, SweepCount = 9, SweepArc = 90f,
            Signature = BossAttack.Fan, Move = BossMove.Hide, MoveRate = 2.2f, Tempo = BossTempo.SurgeAndRest,
            Sense = BossSense.Vibration, Stands = 5, Decoys = 2, Spice = BossSpice.FastLongWindows,
        };
        spec.Fillers.Add(BossAttack.Wall);
        spec.Acts.Add(new[] { BossAttack.Fan });
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.MutationAdvances, null));
        // His fronds grown into thorns, along both sides from his neck to his tail.
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i < 4; i++)
                spec.Sites.Add(new BodySite(side * (0.45f + 0.65f * i), 0.8f, 0.4f, false));
        return spec;
    }

    /// <summary>
    /// Many-Armed Ottilie (§7.2): an octopus with fourteen arms crowding over each other and ink sacs swelling between
    /// them, her pearl held behind her, the best of her hoard. The treasure: she turns slowly to keep her body between it
    /// and Clementine (her light reaches it only from behind her, so circling round her finds it), and after each act
    /// she lifts it up to admire it for 1.2 s, when it can be struck from anywhere. She hurls three glints of the Leak's
    /// ink at her (the hoard throw), then shoots two arms out along lines lit on the floor a moment before (the reach).
    /// From half her meter an ink bloom joins: a cloud of the Leak's ink toward her, hiding what is in it.
    /// </summary>
    static BossSpec Ottilie(int level, float menace)
    {
        var spec = new BossSpec
        {
            Seed = SeedOf("ottilie"), Cycle = 1, Depth = 3, Menace = menace, Tier = BossTier.Common,
            Plan = BodyPlan.Cephalopod, Form = BodyForm.Octopus, Concept = BossConcept.TheTreasure, Blockade = true,
            Name = "Many-Armed Ottilie", Folk = "ottilie",
            // 5.5 m across her arms mutated, 3.3 m freed.
            Radius = 2.2f, Stretch = 1f, Count = 8, Bulk = 0.55f, TrueScale = 0.6f,
            // Warm red-brown, her suckers pale.
            TrueColor = new Vector3(0.62f, 0.3f, 0.2f), Accent = new Vector3(0.92f, 0.68f, 0.52f),
            Theme = MutationTheme.ExtraArms, Minor = MutationTheme.InkSacs, Mutation = 0.6f,
            // Held behind her (her back to the pearl's side), shielded by her body.
            Exposure = Exposure.MovingWindow, Pearl = new BodySite(MathF.PI, 0.75f, 0.6f, false), PearlRadius = 0.7f, PearlOrbit = 0f,
            Shielded = true,
            Hp = MeterAt(level), Damage = 12f, Telegraph = 0.8f, Rest = 2.2f, Volleys = 2, VolleyInterval = 1f, Window = 1.2f,
            PearlSpeed = 7f, RingCount = 0, WallCount = 0, HelixArms = 3,
            Signature = BossAttack.ArcBombs, Move = BossMove.Turn, MoveRate = 0.5f, Tempo = BossTempo.Duelist,
            Sense = BossSense.Water, Spice = BossSpice.FastLongWindows,
        };
        spec.Fillers.Add(BossAttack.Reach);
        spec.Acts.Add(new[] { BossAttack.ArcBombs, BossAttack.Reach });
        spec.Acts.Add(new[] { BossAttack.ArcBombs, BossAttack.Bloom, BossAttack.Reach });
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.MutationAdvances, BossAttack.Bloom));
        // The extra arms, crowding in between her own all round; two ink sacs swelling between them at her back.
        for (int i = 0; i < 6; i++) spec.Sites.Add(new BodySite(MathF.Tau * (i + 0.5f) / 6f, 0.55f, 0.4f, false));
        spec.Sites.Add(new BodySite(MathF.PI * 0.8f, 0.35f, 0.35f, true));
        spec.Sites.Add(new BodySite(-MathF.PI * 0.8f, 0.35f, 0.35f, true));
        return spec;
    }

    /// <summary>
    /// The Pale Dragonet (§8.2), who will call herself Bubbles: a mandarin dragonet whose every swirl of colour has
    /// swollen into a glossy ink sac, the rest of her drained to ash. Boil Heart: five sacs, each a small meter; each one
    /// burst gives back a swirl of her colour, and with all burst her pearl surfaces from her heart. Blind to Clementine,
    /// she dances on her own cycle for the light above: she rises off the plane and comes down where her shadow is, in a
    /// ring of drops (the dusk rise), then her sacs fire their rings. A spiral joins at a third of her meter; at each
    /// third her sacs grow back, one fewer each time, her restored swirls staying where they are.
    /// </summary>
    static BossSpec Bubbles(int level, float menace)
    {
        var spec = new BossSpec
        {
            Seed = SeedOf("bubbles"), Cycle = 1, Depth = 4, Menace = menace, Tier = BossTier.Legendary,
            Plan = BodyPlan.Fish, Form = BodyForm.Dragonet, Concept = BossConcept.BoilHeart, Blockade = true,
            Name = "The Pale Dragonet", Folk = "bubbles",
            // 4.5 m long mutated, 2.7 m freed.
            Radius = 1.8f, Stretch = 1.25f, Count = 5, Bulk = 0.6f, TrueScale = 0.6f,
            // Orange swirled with electric blue.
            TrueColor = new Vector3(0.95f, 0.45f, 0.15f), Accent = new Vector3(0.1f, 0.45f, 0.95f),
            Theme = MutationTheme.InkSacs, Mutation = 0.7f,
            // Buried at her heart, under her sacs.
            Exposure = Exposure.CoaxOut, Pearl = new BodySite(0f, 0f, 0.6f, false), PearlRadius = 0.8f, SacHp = MeterAt(level) * 0.08f,
            Hp = MeterAt(level) * 0.75f, Damage = 12f, Telegraph = 1f, Rest = 2.4f, Volleys = 2, VolleyInterval = 1f, Window = 2.6f,
            PearlSpeed = 7.5f, RingCount = 18, WallCount = 0, HelixArms = 4,
            Signature = BossAttack.Rise, Move = BossMove.Circle, MoveRate = 0.15f, Tempo = BossTempo.Territorial,
            Sense = BossSense.Blind, Spice = BossSpice.FastLongWindows,
        };
        spec.Fillers.Add(BossAttack.SacBurst);
        spec.Fillers.Add(BossAttack.Helix);
        spec.Acts.Add(new[] { BossAttack.Rise, BossAttack.SacBurst });
        spec.Acts.Add(new[] { BossAttack.Rise, BossAttack.Helix, BossAttack.SacBurst });
        spec.Acts.Add(new[] { BossAttack.Rise, BossAttack.Helix, BossAttack.Rise, BossAttack.SacBurst });
        spec.Phases.Add(new BossPhase(0.34f, PhaseChange.MutationAdvances, BossAttack.Helix));
        spec.Phases.Add(new BossPhase(0.67f, PhaseChange.MutationAdvances, null));
        // Her swirls, swollen into sacs: over her back and flanks, round her heart.
        for (int i = 0; i < 5; i++) spec.Sites.Add(new BodySite(MathF.Tau * i / 5f + 0.3f, 0.5f + 0.08f * (i % 2), 0.45f, true));
        return spec;
    }
}
