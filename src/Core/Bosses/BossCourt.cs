using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Bosses;

/// <summary>One of the Drowned Court (docs/BOSS-GENERATOR.md §15): its id (the debug flag's), name, the tale it keeps, and the layers it is met on.</summary>
public sealed record CourtBoss(string Id, string Name, string Tale, int From, int To);

/// <summary>
/// The Drowned Court (docs/BOSS-GENERATOR.md §15): the cube reef's bosses. The Dragon Palace's old household, still at
/// the duties they held when it went dark, each rewritten by the Leak round the splinter of the tide jewels it holds.
/// Each is authored on one of the generator's fights and bodies (its plain roll at the layer's menace, then named and
/// tuned by hand), and each band of layers has its own pool, one drawn per boss layer from the reef's seed, never the
/// same on two boss layers running.
/// </summary>
public static class BossCourt
{
    /// <summary>The bands' first layers: the Gardens, the Sunk Stair, the Dark Halls, Under the Keystone.</summary>
    public static readonly int[] Bands = { 1, 6, 13, 25 };
    public static readonly string[] BandNames = { "The Gardens", "The Sunk Stair", "The Dark Halls", "Under the Keystone" };

    const int Deepest = int.MaxValue;

    public static readonly CourtBoss[] All =
    {
        new("queen", "Queen Clam", "She breathes out the Palace as she remembers it, and guards the picture.", 1, 5),
        new("bell", "The Tide Bell", "It rang the tides in and out. It rings them still, and no tide answers.", 1, 5),
        new("compass", "Old Compass", "The couriers steered by it. It turns and turns, and cannot find the Palace.", 1, 5),
        new("gate", "The Gatekeeper", "It opens and shuts the Palace gate for callers who never come.", 6, 12),
        new("drummer", "The Hour Drummer", "It beat the hours on the stair. It has lost count, and beats them all.", 6, 12),
        new("crown", "The Thorn Crown", "The King's crown, set down on the stair the day the light went out.", 6, 24),
        new("scribe", "The Court Scribe", "It ground the King's ink and wrote the summons that sent the jellyfish.", 6, 24),
        new("procession", "The Lantern Procession", "The lamp bearers, walking the halls in one long line, their lamps all ink.", 13, Deepest),
        new("herald", "The Conch Herald", "It blew the King's comings and goings, and hides now from its own call.", 13, Deepest),
        new("drippus", "Drippus Prime", "The Leak's own: a vast dark shape rising where the water was calm.", 13, Deepest),
        new("physician", "The Physician", "It named the cure the jellyfish was sent for. Its jars hold only ink now.", 25, Deepest),
    };

    public static CourtBoss? Get(string id) => All.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The band a layer is in (0–3).</summary>
    public static int Band(int layer)
    {
        int band = 0;
        for (int i = 0; i < Bands.Length; i++)
            if (layer >= Bands[i]) band = i;
        return band;
    }

    /// <summary>A layer's pool: the court met at its depth.</summary>
    public static IReadOnlyList<CourtBoss> Pool(int layer)
    {
        int at = Math.Max(1, layer);
        return All.Where(b => at >= b.From && at <= b.To).ToList();
    }

    /// <summary>How hard the court presses at a layer: 0 at the top, 1 by layer 12, 1.5 from layer 18 down.</summary>
    public static float MenaceOf(int layer) => Math.Clamp(layer / 12f, 0f, 1.5f);

    /// <summary>
    /// Which of its pool a boss layer holds: drawn from the reef's seed and the layer, and never the one the layer above
    /// would hold.
    /// </summary>
    public static CourtBoss Pick(ulong seed, int layer)
    {
        CourtBoss? last = null;
        for (int z = 1; z <= Math.Max(1, layer); z++)
        {
            var pool = Pool(z);
            int index = new Rng(seed).Stream($"boss-court/{z}").Int(pool.Count);
            if (last is not null && pool.Count > 1 && pool[index].Id == last.Id) index = (index + 1) % pool.Count;
            last = pool[index];
        }
        return last!;
    }

    /// <summary>The boss a layer of the reef holds.</summary>
    public static BossSpec For(ulong seed, int layer) => Make(Pick(seed, layer).Id, seed, layer)!;

    /// <summary>One of the court by its id, as it is met on a layer (null: no such id).</summary>
    public static BossSpec? Make(string id, ulong seed, int layer)
    {
        if (Get(id) is not { } who) return null;
        float menace = MenaceOf(layer);
        // (The depth the generator's rolls read: 1–6, by menace.)
        int depth = 1 + (int)MathF.Round(MathF.Min(menace, 1f) * 5f);
        var spec = who.Id switch
        {
            "queen" => Queen(seed, depth),
            "drippus" => Drippus(seed, depth),
            "bell" => Bell(seed, depth, menace),
            "compass" => Compass(seed, depth, menace),
            "gate" => Gate(seed, depth, menace),
            "drummer" => Drummer(seed, depth, menace),
            "crown" => Crown(seed, depth, menace),
            "scribe" => Scribe(seed, depth, menace),
            "procession" => Procession(seed, depth, menace),
            "herald" => Herald(seed, depth, menace),
            _ => Physician(seed, depth, menace),
        };
        spec.Name = who.Name;
        // (Every arena of the reef is barred while its fight runs: CUBE-WORLD-PLAN §9.1.)
        spec.Blockade = true;
        // (No body past what its kind may grow to.)
        if (!spec.Reference && who.Id != "drippus") spec.Radius = MathF.Min(spec.Radius, BossGenerator.SizeOf(spec.Form).Max * 1.4f);
        // Past the menace's own top the court only grows hardier.
        if (layer > 18) spec.Hp *= 1f + 0.02f * (layer - 18);
        return spec;
    }

    static Vector3 C(float r, float g, float b) => new(r, g, b);

    static BossSpec Base(ulong seed, int depth, float menace, BossConcept concept, BodyForm form, BossTier tier, string id)
    {
        var spec = BossGenerator.Plain(new Rng(seed).Stream($"boss-court/{id}"), seed, depth, menace, tier, form, concept);
        spec.Fillers.Clear();
        spec.Phases.Clear();
        return spec;
    }

    static BossSpec Queen(ulong seed, int depth)
    {
        var spec = BossGenerator.QueenClam(seed, 1, depth);
        spec.Hp *= 1f + 0.6f * spec.Menace;
        return spec;
    }

    static BossSpec Drippus(ulong seed, int depth)
    {
        var spec = BossGenerator.DrippusPrime(seed, 1, depth);
        spec.Hp *= 1f + BossRoster.HpGrowth * spec.Menace;
        return spec;
    }

    /// <summary>
    /// The Tide Bell: a moon jelly drifting in pulses, open only while it rings. Each ring of pearls is followed by the
    /// swell of its toll (a pulse ring); from half its meter the spiral joins.
    /// </summary>
    static BossSpec Bell(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.PulseBell, BodyForm.MoonJelly, BossTier.Common, "bell");
        spec.TrueColor = C(0.62f, 0.78f, 0.74f);
        spec.Accent = C(0.95f, 0.78f, 0.4f);
        spec.Radius *= 1.1f;
        spec.Bulk = 0.6f;
        spec.Volleys = 3;
        spec.Fillers.Add(BossAttack.PulseRing);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Helix));
        return spec;
    }

    /// <summary>
    /// Old Compass: a seven-armed sea star turning on the spot, its pearl travelling round under the skin, so she must
    /// keep moving round it to keep her light on it. Rings, bombs lobbed from its arms; it turns faster as it tires.
    /// </summary>
    static BossSpec Compass(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.TurningStar, BodyForm.SeaStar, BossTier.Common, "compass");
        spec.TrueColor = C(0.85f, 0.55f, 0.2f);
        spec.Accent = C(0.98f, 0.9f, 0.62f);
        spec.Count = 7;
        spec.Bulk = 0.8f;
        spec.MoveRate = 0.45f;
        spec.PearlOrbit = -0.6f;
        spec.Fillers.Add(BossAttack.ArcBombs);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.Quicken, null));
        return spec;
    }

    /// <summary>
    /// The Gatekeeper: a giant clam that turns to face her and opens to throw its spiral, snapping shut on her if she
    /// comes close while it is open. A wall of pearls between spirals; its openings shorten from half its meter.
    /// </summary>
    static BossSpec Gate(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.SnappingGate, BodyForm.GiantClam, BossTier.Common, "gate");
        spec.TrueColor = C(0.55f, 0.6f, 0.66f);
        spec.Accent = C(0.8f, 0.3f, 0.28f);
        spec.Radius *= 1.15f;
        spec.Count = 20;
        spec.Fillers.Add(BossAttack.Snap);
        spec.Fillers.Add(BossAttack.Wall);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.ShorterWindow, null));
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Homing));
        return spec;
    }

    /// <summary>
    /// The Hour Drummer: a sea star that feels where she last shook the floor and creeps there, then beats four swells
    /// out of the ground and sags, its pearl bare while it gets its breath. Bombs between; a ring joins at half.
    /// </summary>
    static BossSpec Drummer(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.SlamAndSigh, BodyForm.SeaStar, BossTier.Common, "drummer");
        spec.TrueColor = C(0.7f, 0.22f, 0.24f);
        spec.Accent = C(0.95f, 0.82f, 0.55f);
        spec.Count = 5;
        spec.Bulk = 0.6f;
        spec.Radius *= 1.1f;
        spec.Volleys = 4;
        spec.VolleyInterval *= 0.9f;
        spec.ActsStill = true;
        spec.Fillers.Add(BossAttack.ArcBombs);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Ring));
        return spec;
    }

    /// <summary>
    /// The Thorn Crown: an urchin spinning fast, its pearl riding round its rim. Rings through its spines and bombs; from
    /// a third it swells of its own, from two thirds its spines grow on.
    /// </summary>
    static BossSpec Crown(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.TurningStar, BodyForm.Urchin, BossTier.Rare, "crown");
        spec.TrueColor = C(0.78f, 0.62f, 0.2f);
        spec.Accent = C(0.6f, 0.2f, 0.5f);
        spec.Count = 14;
        spec.Bulk = 0.75f;
        spec.MoveRate = 0.7f;
        spec.PearlOrbit = 0.75f;
        spec.Pearl = spec.Pearl with { Out = 0.85f };
        spec.Fillers.Add(BossAttack.ArcBombs);
        spec.Phases.Add(new BossPhase(0.34f, PhaseChange.NewAttack, BossAttack.PulseRing));
        spec.Phases.Add(new BossPhase(0.67f, PhaseChange.MutationAdvances, null));
        return spec;
    }

    /// <summary>
    /// The Court Scribe: a sea hare creeping toward the last place she shook the floor, blotting the arena with lobbed
    /// ink and sending one pearl after her like a pointing brush; spent, its pearl shows. A spiral joins at half.
    /// </summary>
    static BossSpec Scribe(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.TheSpreader, BodyForm.SeaHare, BossTier.Common, "scribe");
        spec.TrueColor = C(0.2f, 0.2f, 0.3f);
        spec.Accent = C(0.9f, 0.86f, 0.74f);
        spec.Stretch = 1.15f;
        spec.Bulk = 0.75f;
        spec.Fillers.Add(BossAttack.Homing);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Helix));
        return spec;
    }

    /// <summary>
    /// The Lantern Procession: a siphonophore circling the arena without rest, laying walls of pearls across it, its
    /// pearl passed along the line of its bells. A homing pearl, and rings from half.
    /// </summary>
    static BossSpec Procession(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.TrailingCurtain, BodyForm.Siphonophore, BossTier.Rare, "procession");
        spec.TrueColor = C(0.8f, 0.86f, 0.95f);
        spec.Accent = C(1f, 0.62f, 0.25f);
        spec.Count = 6;
        spec.Stretch = 1.15f;
        spec.Fillers.Add(BossAttack.Homing);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.NewAttack, BossAttack.Ring));
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.Quicken, null));
        return spec;
    }

    /// <summary>
    /// The Conch Herald: a great snail shut in its shell, creeping after her; it comes out only to sound its call, pearls
    /// that follow her, and is bare while they fly. Bombs from its shell; it quickens at half.
    /// </summary>
    static BossSpec Herald(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.ShyShell, BodyForm.Snail, BossTier.Rare, "herald");
        spec.TrueColor = C(0.93f, 0.8f, 0.7f);
        spec.Accent = C(0.75f, 0.4f, 0.36f);
        spec.Count = 4;
        spec.Bulk = 0.75f;
        spec.Radius *= 1.1f;
        spec.Fillers.Add(BossAttack.ArcBombs);
        spec.Phases.Add(new BossPhase(0.5f, PhaseChange.Quicken, null));
        return spec;
    }

    /// <summary>
    /// The Physician: a sea hare whose medicine jars are ink sacs, its pearl buried under them. She bursts the sacs with
    /// her light (each bursts in a spray) to bring the pearl up; it holds its ground and bombs her. Three phases.
    /// </summary>
    static BossSpec Physician(ulong seed, int depth, float menace)
    {
        var spec = Base(seed, depth, menace, BossConcept.BoilHeart, BodyForm.SeaHare, BossTier.Legendary, "physician");
        spec.TrueColor = C(0.5f, 0.62f, 0.5f);
        spec.Accent = C(0.92f, 0.9f, 0.8f);
        spec.Radius *= 1.15f;
        spec.Bulk = 0.8f;
        spec.Fillers.Add(BossAttack.ArcBombs);
        spec.Phases.Add(new BossPhase(0.34f, PhaseChange.NewAttack, BossAttack.Helix));
        spec.Phases.Add(new BossPhase(0.67f, PhaseChange.MutationAdvances, null));
        return spec;
    }
}
