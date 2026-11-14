using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// Which reef of the dive (DESIGN-TOPDOWN §8, docs/LIGHT-AND-GRAPH.md §6): the loop through the depths, the
/// depth (biome), the row within it (Level) and the reef across that row (Lane, from 0 at the left; Run.DiveGraph). A
/// depth has 5 rows; its boss stands alone on the last. The first depth (Depth 1 here, the Sunlit
/// Shallows) is the tutorial, Depth 0 to the player: two levels, no boss; runs start below it (Depth 2 here, Depth 1 to
/// the player) once she has passed it. Depth is kept 1-based in code and saves; only its name is one less.
/// </summary>
public readonly record struct LevelId(int Cycle, int Depth, int Level)
{
    /// <summary>The tutorial's first level: where a new player's first run starts.</summary>
    public static readonly LevelId First = new(1, LevelPlan.Tutorial, 1);
    /// <summary>Where a run starts once she has passed the tutorial: the first real depth.</summary>
    public static readonly LevelId FirstRun = new(1, LevelPlan.Tutorial + 1, 1);

    /// <summary>The depth's number as the player sees it: the tutorial is Depth 0.</summary>
    public int ShownDepth => Depth - LevelPlan.Tutorial;

    public bool IsTutorial => Depth == LevelPlan.Tutorial;

    /// <summary>The reef across its row (Run.DiveGraph), from 0 at the left.</summary>
    public int Lane { get; init; }

    /// <summary>A haven (docs/DIVE.md §4): Lantern Bay's water, a reef of the graph of its own kind (Run.ReefKind.Haven).</summary>
    public bool Haven { get; init; }

    public override string ToString() => Haven ? $"A haven · Depth {ShownDepth}"
        : Cycle > 1 ? $"Cycle {Cycle} · Depth {ShownDepth} · Level {Level}" : $"Depth {ShownDepth} · Level {Level}";
}

/// <summary>
/// What a level holds (DESIGN-TOPDOWN §4.1 stage 1): rolled per level from the run seed, before the level is shaped.
/// What each kind of place asks (where, how often, how far apart) is its rule (PlaceRules). At most
/// <see cref="MaxPlaces"/> places besides the start, the exit and its holes; over the cap, places are dropped in the
/// order secret, shell cache, a haven by chance, the Crab Deal.
/// </summary>
public static class LevelStocking
{
    public const int MaxPlaces = 3;
    /// <summary>The tutorial's two levels, between them every kind of place: level 1 a clam room, a secret and a shell
    /// cache; level 2 a shop, a curse den and the open rift (the arena with no boss).</summary>
    public const int TutorialLevels = 2;
    /// <summary>A depth's levels (DESIGN-TOPDOWN §8): the boss stands on the last.</summary>
    public const int LevelsPerDepth = 5;
}

/// <summary>
/// A level's plan, from the run seed and its <see cref="LevelId"/> alone: what its reef is in the dive's graph (Run.DiveGraph)
/// and where its holes lead, whether its boss stands here, what places it holds, and its Menace. Any level can be
/// generated on its own; no level needs another one generated first.
/// </summary>
public sealed class LevelPlan
{
    public const int Depths = 6;
    /// <summary>The tutorial depth (Depth 0 to the player): never part of a loop after the first.</summary>
    public const int Tutorial = 1;

    public LevelId Id { get; init; }
    /// <summary>The exit is an arena round the Crack: a boss level, or the tutorial's open rift.</summary>
    public bool HasBoss { get; init; }
    /// <summary>Its share of a level's water (a dive's leak cave half, docs/DIVE.md §6).</summary>
    public float AreaScale { get; init; } = 1f;
    /// <summary>The tutorial's last level: its arena holds no boss, and the Crack in it stands open (with HasBoss).</summary>
    public bool Rift { get; init; }
    public int Treasures { get; init; }
    public int Secrets { get; init; }
    public int Crabs { get; init; }
    public int Curses { get; init; }
    public int Shops { get; init; }
    /// <summary>Ways into a haven (docs/DIVE.md §4), 0 or 1.</summary>
    public int Havens { get; init; }
    public int Caches { get; init; }
    /// <summary>The level above was a boss level: she arrives on its arena's floor.</summary>
    public bool EntryFromArena { get; init; }
    /// <summary>
    /// 0 at the first real depth (and in the tutorial), 1 at the last, and on up by the same step through every later
    /// loop (DESIGN-TOPDOWN §6.2).
    /// </summary>
    public float Menace { get; init; }
    /// <summary>Where each of its holes leads (Run.DiveGraph), in the graph's order, and what each of those reefs is.</summary>
    public IReadOnlyList<LevelId> Exits { get; init; } = Array.Empty<LevelId>();
    public IReadOnlyList<ReefKind> ExitKinds { get; init; } = Array.Empty<ReefKind>();
    /// <summary>Its own kind in the graph.</summary>
    public ReefKind Kind { get; init; }
    /// <summary>A leak cave (LevelMap.LeakCave): no way down, one leak where it would be.</summary>
    public bool LeakCave { get; init; }
    /// <summary>A leak cave's floor (from 1) and how many it has (LevelMap.Floor, Floors).</summary>
    public int Floor { get; init; } = 1;
    public int Floors { get; init; } = 1;

    public int Places => Treasures + Secrets + Curses + Shops + Caches + Crabs + Havens + (HasBoss ? 1 : 0);
    /// <summary>The level's template (docs/archive/LEVEL-TEMPLATES-PROPOSAL.md): null in the tutorial.</summary>
    public LevelTemplate? Template { get; init; }

    /// <summary>The level of this depth (in this loop) where its boss stands: the 5th (the tutorial's rift: its 2nd).</summary>
    public static int BossLevel(SeedCode seed, int cycle, int depth) =>
        depth == Tutorial ? LevelStocking.TutorialLevels : LevelStocking.LevelsPerDepth;

    /// <summary>
    /// The reef of this depth that hides its Crab Deal (its row 0: none): one reef of a row from 2 to 4 (none where that
    /// reef is a haven). Never in the first real depth, nor on a boss level.
    /// </summary>
    static (int Row, int Lane) CrabReef(RunStreams streams, LevelId id)
    {
        if (id.Depth <= Tutorial + 1) return (0, 0);
        var rng = streams.Plan(new LevelId(id.Cycle, id.Depth, 0));
        if (rng.NextFloat() >= PlaceRules.CrabDepth) return (0, 0);
        int boss = BossLevel(streams.Seed, id.Cycle, id.Depth);
        var levels = Enumerable.Range(2, 3).Where(l => l != boss).ToList();
        int row = levels[rng.Int(levels.Count)];
        return (row, rng.Int(DiveGraph.Of(streams.Seed).Row(id.Cycle, id.Depth, row).Count));
    }

    /// <summary>
    /// A reef's Menace: by its depth alone (docs/LIGHT-AND-GRAPH.md §6.1), never by how many reefs she has
    /// crossed: 0 at the first real depth (and in the tutorial), one step a depth, on through every later loop.
    /// </summary>
    public static float MenaceOf(LevelId id) =>
        id.Depth == Tutorial ? 0f : ((id.Cycle - 1) * (Depths - 1) + id.Depth - Tutorial - 1) / (float)(Depths - Tutorial - 1);

    /// <param name="luck">Her luck (LuckTuning): a chance of a clam room on a level without one.</param>
    public static LevelPlan For(SeedCode seed, LevelId id, float luck = 0f)
    {
        var streams = new RunStreams(seed);
        var rng = streams.Plan(id);
        if (id.Depth == Tutorial) return Tutorial_(seed, streams, id);
        var graph = DiveGraph.Of(seed);
        var node = graph.Node(id);
        bool boss = node.Kind == ReefKind.Boss;
        bool first = id.Level == 1;
        var (templates, _) = DepthTemplates(seed, id.Cycle, id.Depth, node.Id.Lane);
        var template = templates[id.Level - 1];
        int treasures = node.Kind == ReefKind.Treasure ? 1 : 0;
        int secrets = PlaceRules.Rolls(PoiKind.Secret, id.Level, rng) ? 1 : 0;
        int curses = node.Kind == ReefKind.Den ? 1 : 0;
        // (A reef's shop is in its haven now: PlaceRules.Shop rolls none.)
        int shops = 0;
        // A haven: about a third of the reefs, and always the first of each depth's second row (so every dive meets one).
        bool keepHaven = !boss && id.Level == 2 && node.Id.Lane == 0;
        int havens = keepHaven || !boss && PlaceRules.Rolls(PoiKind.Haven, id.Level, rng) ? 1 : 0;
        int caches = PlaceRules.Rolls(PoiKind.ShellCache, id.Level, rng) ? 1 : 0;

        // The template's places over the general roll: required, a chance, or none (treasure rooms keep the depth's rule).
        var required = new HashSet<PoiKind>();
        int Apply(PoiKind kind, int general)
        {
            if (template is null || !template.Places.TryGetValue(kind, out var rule)) return general;
            if (rule.None) return 0;
            if (rule.Required > 0)
            {
                required.Add(kind);
                return rule.Required;
            }
            return rule.Chance is { } p ? (rng.NextFloat() < p ? 1 : 0) : general;
        }
        secrets = Apply(PoiKind.Secret, secrets);
        // (A curse den comes with the reef's kind, which its hole's rim shows: the template only picks fitting kinds, and
        // a treasure reef's may ask for one besides.)
        if (template?.Requires(PoiKind.CurseDen) == true) curses = 1;
        shops = 0;
        caches = Apply(PoiKind.ShellCache, caches);
        // The general rule wins: a depth's first level has no shop and no shell cache.
        if (id.Level < PlaceRules.Shop.FromRow) shops = 0;
        if (id.Level < PlaceRules.ShellCache.FromRow) caches = 0;
        // Her luck: a clam room where there was none (unless the template forbids one).
        if (luck > 0f)
        {
            var lucky = streams.Luck(id);
            if (treasures == 0 && !(template?.Forbids(PoiKind.TreasureCave) ?? false) && lucky.NextFloat() < OctoShoots.Core.Items.LuckTuning.ClamChance(luck))
                treasures = 1;
        }

        // Over the cap: drop the secret, then the shell cache — what the template requires last. Treasure rooms and
        // curse dens stay (the reef's kind).
        int crabs = CrabReef(streams, id) == (id.Level, node.Id.Lane) ? 1 : 0;
        int Total() => treasures + secrets + curses + shops + caches + crabs + havens + (boss ? 1 : 0);
        for (int pass = 0; pass < 2 && Total() > LevelStocking.MaxPlaces; pass++)
            while (Total() > LevelStocking.MaxPlaces)
            {
                bool Free(PoiKind k) => pass == 1 || !required.Contains(k);
                if (secrets > 0 && Free(PoiKind.Secret)) secrets--;
                else if (caches > 0 && Free(PoiKind.ShellCache)) caches--;
                // (Then a haven by chance, never the depth's sure one; then the Crab Deal.)
                else if (havens > 0 && !keepHaven) havens--;
                else if (crabs > 0) crabs--;
                else break;
            }

        // (Left to right across the rows below, as its holes are across the map.)
        var exits = graph.ExitsOf(id).OrderBy(e => graph.Node(e).X).ToList();
        return new LevelPlan
        {
            Id = node.Id,
            Kind = node.Kind,
            Exits = exits.ToArray(),
            ExitKinds = exits.Select(graph.KindOf).ToArray(),
            HasBoss = boss,
            Treasures = treasures,
            Secrets = secrets,
            Crabs = crabs,
            Curses = curses,
            Shops = shops,
            Havens = havens,
            Caches = caches,
            // (Into a depth's first row from its boss's arena above, or the tutorial's rift.)
            EntryFromArena = first,
            Menace = MenaceOf(id),
            Template = template,
        };
    }

    /// <summary>
    /// The templates down one lane of a depth (from its own stream, so any reef's is known on its own): the reef of each
    /// row at that lane (or the row's last) takes the row's. Each draws from the templates that fit its depth and slot
    /// (the first row, a middle one, the one before the boss, the boss's), by weight: never the same as the row before,
    /// no more of one than its MaxPerDepth, at least three different ones down a lane of three or more, and only one
    /// that fits the reef's kind (none forbidding the treasure room or curse den the kind holds, none requiring one it
    /// does not, but for a treasure reef's curse den). Also the rows whose reef holds a treasure room.
    /// </summary>
    public static (LevelTemplate?[] Templates, HashSet<int> TreasureLevels) DepthTemplates(SeedCode seed, int cycle, int depth, int lane = 0)
    {
        int boss = BossLevel(seed, cycle, depth);
        var graph = DiveGraph.Of(seed);
        var kinds = Enumerable.Range(1, boss).Select(level => graph.Node(new LevelId(cycle, depth, level) { Lane = lane }).Kind).ToArray();
        var all = LevelTemplates.All;
        var fallback = LevelTemplates.Get("default");
        var streams = new RunStreams(seed);
        var rng = lane == 0 ? streams.TemplateRoll(cycle, depth) : streams.TemplateRoll(cycle, depth * 8 + lane);
        int shown = depth - Tutorial;
        var treasures = new HashSet<int>(Enumerable.Range(1, boss).Where(level => kinds[level - 1] == ReefKind.Treasure));
        bool FitsKind(LevelTemplate t, ReefKind kind, int level) =>
            (kind == ReefKind.Treasure ? !t.Forbids(PoiKind.TreasureCave) : !t.Requires(PoiKind.TreasureCave))
            // (A treasure reef may hold the curse den its template asks for too, The Hoard's, where a den may stand.)
            && (kind == ReefKind.Den ? !t.Forbids(PoiKind.CurseDen)
                : kind == ReefKind.Treasure && level >= PlaceRules.LeakCave.FromRow || !t.Requires(PoiKind.CurseDen));
        for (int attempt = 0; ; attempt++)
        {
            var picked = new LevelTemplate?[boss];
            for (int level = 1; level <= boss; level++)
            {
                var slot = level == boss ? TemplateSlot.Boss : level == 1 ? TemplateSlot.First : level == boss - 1 ? TemplateSlot.BeforeBoss : TemplateSlot.Middle;
                var previous = level > 1 ? picked[level - 2] : null;
                var fits = all.Where(t => t.Fits(shown, slot) && t != previous
                    && (t.MaxPerDepth <= 0 || picked.Count(x => x == t) < t.MaxPerDepth)
                    && FitsKind(t, kinds[level - 1], level)).ToList();
                LevelTemplate? choice = fallback;
                float sum = fits.Sum(t => t.WeightOn(slot));
                if (sum > 0f)
                {
                    float roll = rng.NextFloat() * sum;
                    foreach (var t in fits)
                    {
                        roll -= t.WeightOn(slot);
                        choice = t;
                        if (roll <= 0f) break;
                    }
                }
                picked[level - 1] = choice;
            }
            // A lane of three rows or more shows at least three different ones (a few rolls, then as it is).
            if (boss < 3 || picked.Distinct().Count() >= 3 || attempt >= 12) return (picked, treasures);
        }
    }

    /// <summary>
    /// A leak cave's plan (docs/DIVE.md §6): the general level's shape, no shop, no crab, no boss, no clam room
    /// and no shell cache; now and then a secret or a curse den off its main path (and, as on any level, maybe a light
    /// spring); the Menace its region gives it.
    /// </summary>
    /// <param name="floor">Which of its floors (from 1; each its own layout), and how many it has: a floor above the last
    /// ends in a hole down instead of the leak, and each floor down is meaner (FloorMenace more).</param>
    /// <param name="boss">A great cave (docs/FOLK.md): its deepest floor's dead end is a boss's arena,
    /// its leak in the middle.</param>
    public static LevelPlan ForCave(SeedCode seed, LevelId id, float menace, int floor = 1, int floors = 1, bool boss = false, float areaScale = 1f)
    {
        var streams = new RunStreams(seed);
        if (floor > 1) id = id with { Cycle = id.Cycle + FloorCycles * (floor - 1) };
        var rng = streams.Plan(id);
        return new LevelPlan
        {
            Id = id,
            HasBoss = boss && floor >= floors,
            Secrets = PlaceRules.Rolls(PoiKind.Secret, 1, rng) ? 1 : 0,
            // (No curse den, nor a cave within the cave.)
            Curses = 0,
            Menace = menace + FloorMenace * (floor - 1),
            Template = LevelTemplates.Get("default"),
            LeakCave = true,
            Floor = floor,
            Floors = Math.Max(floors, floor),
            AreaScale = areaScale,
        };
    }

    /// <summary>A deeper floor of a cave counts as a level of its own: its id's cycle this many on per floor.</summary>
    public const int FloorCycles = 100;
    /// <summary>Each floor down a cave adds this to its Menace.</summary>
    public const float FloorMenace = 0.1f;

    /// <summary>The tutorial's levels: every kind of place between its two, set rather than rolled.</summary>
    static LevelPlan Tutorial_(SeedCode seed, RunStreams streams, LevelId id)
    {
        bool rift = id.Level >= LevelStocking.TutorialLevels;
        var graph = DiveGraph.Of(seed);
        var exits = graph.ExitsOf(id);
        return new LevelPlan
        {
            Id = id,
            HasBoss = rift,
            Rift = rift,
            Treasures = rift ? 0 : 1,
            Secrets = rift ? 0 : 1,
            Caches = rift ? 0 : 1,
            // (Its shop in its haven, as every reef's.)
            Havens = rift ? 1 : 0,
            Curses = rift ? 1 : 0,
            EntryFromArena = false,
            Menace = 0f,
            Exits = exits.ToArray(),
            ExitKinds = exits.Select(graph.KindOf).ToArray(),
        };
    }
}
