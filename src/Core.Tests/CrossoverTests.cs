using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// The crossover (docs/proposals/CROSSOVER.md, Run.Crossover): its levels have no havens, no leak caves or curse dens
/// and no secret rooms (§11, §3); every level but the tutorial's and a boss's is built from cell templates (§2); and
/// on those she may dive anywhere once the dive clock has run (§3).
/// </summary>
public class CrossoverTests
{
    static readonly LevelId[] Ids = Enumerable.Range(1, LevelPlan.Depths - 1)
        .SelectMany(depth => Enumerable.Range(1, 5).Select(level => new LevelId(1, depth, level)))
        .ToArray();

    [Fact]
    public void A_crossover_plan_has_no_haven_and_no_leak_cave()
    {
        foreach (var id in Ids)
        {
            var plan = LevelPlan.For(TestLevels.Seed, id, crossover: true);
            Assert.Equal(0, plan.Havens);
            Assert.Equal(0, plan.Curses);
        }
    }

    [Fact]
    public void The_tutorials_plans_have_none_either()
    {
        for (int level = 1; level <= 2; level++)
        {
            var plan = LevelPlan.For(TestLevels.Seed, new LevelId(1, LevelPlan.Tutorial, level), crossover: true);
            Assert.Equal(0, plan.Havens);
            Assert.Equal(0, plan.Curses);
        }
    }

    [Fact]
    public void The_dive_still_plans_havens_and_leak_caves()
    {
        var plans = Ids.Select(id => LevelPlan.For(TestLevels.Seed, id)).ToList();
        Assert.Contains(plans, p => p.Havens > 0);
        Assert.Contains(plans, p => p.Curses > 0);
    }

    [Fact]
    public void Everything_else_is_planned_as_the_dive_plans_it()
    {
        foreach (var id in Ids)
        {
            var dive = LevelPlan.For(TestLevels.Seed, id);
            var cross = LevelPlan.For(TestLevels.Seed, id, crossover: true);
            Assert.Equal(dive.Id, cross.Id);
            Assert.Equal(dive.HasBoss, cross.HasBoss);
            Assert.Equal(dive.Treasures, cross.Treasures);
            Assert.Equal(0, cross.Secrets);
            Assert.Equal(dive.Crabs, cross.Crabs);
            Assert.True(cross.Shops >= dive.Shops);
            Assert.Equal(dive.Caches, cross.Caches);
            // (Its way on is the crossover's route, Biomes: one of the dive's exits, her branch carried or chosen.)
            Assert.Equal(Biomes.Exits(TestLevels.Seed, id), cross.Exits);
            Assert.All(cross.Exits, e => Assert.Contains(e with { Branch = 0 }, dive.Exits));
            Assert.Equal(dive.Menace, cross.Menace);
        }
    }

    [Fact]
    public void A_crossover_level_is_made_without_a_haven_or_leak_cave_mouth()
    {
        // (A level whose dive plan holds both, so the crossover really takes them away.)
        var id = Ids.FirstOrDefault(i => LevelPlan.For(TestLevels.Seed, i) is { Havens: > 0 } or { Curses: > 0 });
        Assert.NotEqual(default, id);
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        Assert.DoesNotContain(map.Pois, p => p.Kind is PoiKind.Haven or PoiKind.LeakCave);
        Assert.DoesNotContain(map.MouthKinds, k => k is PoiKind.Haven or PoiKind.LeakCave);
    }

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>A few template levels, from two seeds and every depth below the tutorial.</summary>
    static readonly Lazy<LevelMap[]> Templated = new(() =>
        new[] { TestLevels.Seed, SeedCode.Parse("REEF22XY") }
            .SelectMany(seed => Enumerable.Range(LevelPlan.Tutorial + 1, LevelPlan.Depths - LevelPlan.Tutorial - 1)
                .Select(depth => new LevelId(1, depth, 1 + depth % 4))
                .Select(id => TopDownGenerator.Generate(new RunStreams(seed), id, crossover: true)))
            .ToArray());

    [Fact]
    public void Every_cell_template_keeps_the_socket_rule_and_every_opening_has_one()
    {
        // (The variants are checked as they are made: a template that breaks the rule throws.)
        var masks = CaveTemplates.SunlitVariants.Select(v => v.Openings).ToHashSet();
        for (int mask = 1; mask < 16; mask++) Assert.Contains(mask, masks);
        foreach (int side in new[] { CaveTemplates.N, CaveTemplates.E, CaveTemplates.S, CaveTemplates.W })
            Assert.Contains(CaveTemplates.RoomVariants, v => v.Openings == side);
        Assert.All(CaveTemplates.RoomVariants, v => Assert.Equal(1, Enumerable.Range(0, CaveTemplates.Cell * CaveTemplates.Cell).Count(i => v[i % CaveTemplates.Cell, i / CaveTemplates.Cell] == 'R')));
        Assert.All(CaveTemplates.ChunkVariants, c => Assert.Equal(CaveTemplates.Chunk, c.GetLength(0)));
    }

    [Fact]
    public void Every_crossover_level_but_the_rift_is_templated()
    {
        foreach (var id in Ids)
        {
            var plan = LevelPlan.For(TestLevels.Seed, id, crossover: true);
            Assert.Equal(!plan.Rift, plan.Templated);
            Assert.False(LevelPlan.For(TestLevels.Seed, id).Templated);
        }
        // (The tutorial's first level is a template level like the rest; its rift is not.)
        Assert.True(LevelPlan.For(TestLevels.Seed, LevelId.First, crossover: true).Templated);
        Assert.False(LevelPlan.For(TestLevels.Seed, LevelId.First with { Level = 2 }, crossover: true).Templated);
    }

    [Fact]
    public void A_template_level_is_one_cave_with_its_holes_down_and_its_marks_in_the_water()
    {
        foreach (var map in Templated.Value)
        {
            Assert.True(map.Templated);
            Assert.Null(LevelValidator.Validate(map));
            // One to three holes down, each a Hole place on the path: its middle open down to the shaft's floor, and
            // the floor round it sloping into it (no sheer cut at the rim).
            var holes = map.Pois.Where(p => p.Kind == PoiKind.Hole).ToList();
            Assert.InRange(holes.Count, 1, 3);
            Assert.Equal(holes.Count, map.Holes.Count);
            foreach (var hole in holes)
            {
                Assert.True(map.InShaft(hole.Position));
                Assert.True(map.HeightAt(hole.Position) < LevelMap.ShaftBottom + 1.2f, $"{map.Id}: the hole at {hole.Position} not open");
                var path = LevelValidator.Distances(map, map.Start.Position);
                Assert.True(float.IsFinite(LevelValidator.DistanceAt(path, hole.Position)), $"{map.Id}: a hole off the path");
                for (int k = 0; k < 8; k++)
                {
                    var way = new Vector2(MathF.Cos(k * MathF.Tau / 8f), MathF.Sin(k * MathF.Tau / 8f));
                    float near = map.HeightAt(hole.Position + way * 4.5f), far = map.HeightAt(hole.Position + way * 6.5f);
                    if (far > 0f || near > 0f) continue;
                    Assert.True(near <= far + 0.15f, $"{map.Id}: the floor round a hole does not slope into it ({near} at 4.5 m, {far} at 6.5 m)");
                    Assert.True(far - near < 3f, $"{map.Id}: a step at a hole's rim");
                }
            }
            var reach = LevelValidator.Distances(map, map.Start.Position);
            Assert.True(float.IsFinite(LevelValidator.DistanceAt(reach, map.Exit.Position)), $"{map.Id}: the far side cannot be reached");
            Assert.NotEmpty(map.MobMarks);
            Assert.All(map.MobMarks, m => Assert.True(map.IsOpen(m.At), $"{map.Id}: a mob mark in the rock at {m.At}"));
            Assert.All(map.PoiMarks, p => Assert.True(map.IsOpen(p), $"{map.Id}: a POI mark in the rock at {p}"));
            Assert.NotEmpty(map.Spawns);
            Assert.All(map.Spawns, s => Assert.True(map.IsOpen(s.Position), $"{map.Id}: a spawn in the rock at {s.Position}"));
            // Its places stand where the plan's rules keep them.
            foreach (var poi in map.Pois.Where(p => p.Kind is not (PoiKind.Start or PoiKind.Exit)))
            {
                Assert.True(Vector2.Distance(poi.Position, map.Start.Position) >= PlaceRules.FromStart, $"{map.Id}: the {poi.Kind} by the start");
                Assert.DoesNotContain(poi.Kind, new[] { PoiKind.Secret, PoiKind.LeakCave, PoiKind.Haven, PoiKind.CurseDen });
            }
        }
    }

    [Fact]
    public void A_template_level_is_the_same_from_the_same_seed()
    {
        var id = new LevelId(1, LevelPlan.Tutorial + 2, 2);
        var a = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        var b = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        Assert.Equal(a.Heights, b.Heights);
        Assert.Equal(a.MobMarks, b.MobMarks);
        Assert.Equal(a.Pois.Select(p => (p.Kind, p.Position)), b.Pois.Select(p => (p.Kind, p.Position)));
        var other = TopDownGenerator.Generate(new RunStreams(SeedCode.Parse("REEF22XY")), id, crossover: true);
        Assert.NotEqual(a.Heights, other.Heights);
    }

    [Fact]
    public void On_a_template_level_she_dives_down_a_hole_at_any_time_and_nowhere_else()
    {
        var map = Templated.Value[0];
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        // Away from its holes: no dive, however long she waits.
        for (int i = 0; i < 40 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = map.Start.Position;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Dive = true });
            Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.Dived);
        }
        Assert.False(w.CanDive);
        // Over one, at once (on a fresh level too).
        w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        w.Player.Position = w.Player.PrevPosition = map.Holes[^1].Shaft.Center;
        Assert.True(w.CanDive);
        w.Step(new PlaneInput { Dive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dived);
    }

    [Fact]
    public void A_template_level_has_one_to_three_holes_down_each_further_one_rarer()
    {
        var plans = Ids.Select(id => LevelPlan.For(TestLevels.Seed, id, crossover: true)).Where(p => p.Templated).ToList();
        Assert.All(plans.Where(p => p.HasBoss), p => Assert.Equal(0, p.DiveHoles));
        var free = plans.Where(p => !p.HasBoss).ToList();
        Assert.All(free, p => Assert.InRange(p.DiveHoles, 1, 3));
        Assert.All(new[] { 1, 2, 3 }, n => Assert.Contains(free, p => p.DiveHoles == n));
        Assert.True(LevelPlan.HoleSecond * LevelPlan.HoleThird < LevelPlan.HoleSecond - LevelPlan.HoleSecond * LevelPlan.HoleThird && LevelPlan.HoleSecond < 1f - LevelPlan.HoleSecond + 1e-6f);
    }

    [Fact]
    public void Shells_are_worth_more_the_rarer_and_rich_finds_hold_rarer_ones()
    {
        Assert.Equal(new[] { 1, 3, 8, 25 }, Enum.GetValues<ShellKind>().Select(ShellWorth.Of));
        foreach (var kind in Enum.GetValues<ShellKind>())
        {
            Assert.Equal(kind, ShellWorth.KindOf(ShellWorth.Of(kind)));
            Assert.Equal(kind, ShellWorth.KindOf(ShellWorth.Of(kind) + 1));
        }
        float Mean(int richness, float menace)
        {
            var rng = new Rng(7);
            return Enumerable.Range(0, 4000).Average(_ => (float)ShellWorth.Of(ShellWorth.Roll(rng, richness, menace)));
        }
        Assert.True(Mean(ShellWorth.Freed, 0f) < Mean(ShellWorth.Pot, 0f));
        Assert.True(Mean(ShellWorth.Pot, 0f) < Mean(ShellWorth.Cache, 0f));
        Assert.True(Mean(ShellWorth.Cache, 0f) < Mean(ShellWorth.Vault, 0f));
        Assert.True(Mean(ShellWorth.Vault, 0f) < Mean(ShellWorth.Vault, 1f));
    }

    [Fact]
    public void Some_template_levels_hold_a_shop_in_a_room_cell()
    {
        var plans = Ids.Select(id => LevelPlan.For(TestLevels.Seed, id, crossover: true)).Where(p => p.Templated).ToList();
        Assert.Contains(plans, p => p.Shops == 1);
        Assert.All(plans, p => Assert.True(p.Places <= LevelStocking.MaxPlaces));
        var id = plans.First(p => p.Shops == 1).Id;
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        var shop = Assert.Single(map.Pois, p => p.Kind == PoiKind.Shop);
        Assert.True(shop.Cave >= 0, "the shop has no cave");
    }

    [Fact]
    public void Bubble_vaults_hang_against_the_rock_and_hold_shells()
    {
        int vaults = 0;
        foreach (var map in Templated.Value)
        {
            Assert.NotEmpty(map.VaultMarks);
            var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
            vaults += w.Vaults.Count;
            foreach (var v in w.Vaults)
            {
                Assert.True(map.IsOpen(v.Position), $"{map.Id}: a vault in the rock");
                Assert.False(map.IsOpen(v.Position + v.Wall * (v.Radius + 0.6f)), $"{map.Id}: a vault at {v.Position} hangs against no rock");
                Assert.InRange(v.Shells.Count, VaultTuning.ShellsMin, VaultTuning.ShellsMax);
                Assert.True(Vector2.Distance(v.Position, map.Start.Position) >= VaultTuning.StartClear - VaultTuning.WallSearch);
            }
        }
        Assert.True(vaults > Templated.Value.Length, $"only {vaults} vaults on {Templated.Value.Length} levels");
    }

    [Fact]
    public void Her_beam_bursts_a_vault_and_what_it_held_tumbles_out()
    {
        var map = Templated.Value.First(m => new PlaneWorld(m, new Tuning()).Vaults.Count > 0);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        var vault = w.Vaults[0];
        // She floats a little way off it, out from its rock, and shines at it.
        var at = vault.Position - vault.Wall * 2.5f;
        Assert.True(map.IsOpen(at));
        int shells = w.Shells.Count;
        w.Run.Shells = 0;
        bool burst = false;
        for (int i = 0; i < 300 && !burst; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = vault.Wall, Fire = true, Beam = true });
            burst = w.Events.Any(e => e.Type == PlaneEventType.VaultBurst);
        }
        Assert.True(burst, "the beam never burst it");
        Assert.True(vault.Burst);
        // (Its shells lie out of it, or have already drifted to her.)
        int worth = vault.Shells.Sum(ShellWorth.Of);
        int lying = w.Shells.Skip(shells).Sum(sh => sh.Value);
        Assert.True(lying + w.Run.Shells >= worth, $"{lying} lying + {w.Run.Shells} taken, of {worth}");
    }

    [Fact]
    public void Light_pips_count_her_light_in_halves()
    {
        Assert.Equal(12, LightPips.MaxHalves(100f));
        Assert.Equal(12, LightPips.Halves(100f));
        Assert.Equal(12, LightPips.Halves(100f - 0.5f));
        Assert.Equal(11, LightPips.Halves(100f - LightPips.Half - 0.5f));
        Assert.Equal(1, LightPips.HitHalves(3f));
        Assert.Equal(2, LightPips.HitHalves(24f));
        Assert.Equal(LightPips.HitMost, LightPips.HitHalves(500f));
        Assert.Equal(10 * LightPips.Half, LightPips.Add(95f, -2, 100f), 3);
        Assert.Equal(100f, LightPips.Add(95f, 4, 100f));
    }

    static PlaneWorld PipWorld(LevelMap map)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning()) { Pips = true };
        return new PlaneWorld(map, new Tuning(), run);
    }

    [Fact]
    public void In_pips_a_hit_takes_whole_halves_and_an_orb_gives_back_a_pip()
    {
        var w = PipWorld(Templated.Value[0]);
        float full = w.Player.Hp;
        Assert.True(w.DebugHurt(5f));
        Assert.Equal(LightPips.MaxHalves(full) - 1, LightPips.Halves(w.Player.Hp));
        Assert.Equal(0f, w.Player.Hp % LightPips.Half, 2);
        int before = LightPips.Halves(w.Player.Hp);
        w.Player.HurtTimer = 0f;
        Assert.True(w.DebugHurt(1000f));
        Assert.Equal(before - LightPips.HitMost, LightPips.Halves(w.Player.Hp));
        // An orb where she floats: a whole pip back.
        before = LightPips.Halves(w.Player.Hp);
        w.Hearts.Add(new PlaneHeart { Position = w.Player.Position });
        w.Step(default);
        Assert.Equal(before + LightPips.OrbHalves, LightPips.Halves(w.Player.Hp));
    }

    [Fact]
    public void Pools_by_place_offer_the_place_s_pearls()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        var none = Array.Empty<string>();
        Assert.All(run.Offer("shrine", none), id => Assert.True(run.QualityOf(id) >= 3, id));
        Assert.All(run.Offer("shop", none), id => Assert.Contains("shop", Catalog.Value.Items.First(i => i.Id == id).Pools));
        Assert.All(run.Offer("secret", none), id => Assert.True(run.QualityOf(id) <= 2 || Catalog.Value.Items.First(i => i.Id == id).Pools.Contains("secret"), id));
        Assert.True(run.Offer("shop", none).Count < run.Offer("treasure", none).Count);
        Assert.Equal(50, PlaneEconomyTuning.PriceOf(0));
        Assert.Equal(150, PlaneEconomyTuning.PriceOf(4));
    }

    [Fact]
    public void A_light_shrine_gives_a_fine_pearl_for_two_pips_and_never_her_last_light()
    {
        var plan = Ids.Concat(Ids.Select(i => i with { Cycle = 2 })).Select(id => LevelPlan.For(TestLevels.Seed, id, crossover: true)).First(p => p.Shrines == 1);
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), plan.Id, crossover: true);
        var poi = Assert.Single(map.Pois, p => p.Kind == PoiKind.Shrine);
        Assert.True(LevelValidator.DistanceAt(LevelValidator.Distances(map, map.Start.Position), poi.Position) < float.PositiveInfinity);

        // Too little light: it is refused.
        var low = PipWorld(map);
        var stand = Assert.Single(low.Stands, s => s.Kind == StandKind.Shrine);
        Assert.True(low.Run.QualityOf(stand.ItemId) >= 3);
        low.Player.Hp = LightPips.ShrineHalves * LightPips.Half;
        low.Player.Position = low.Player.PrevPosition = stand.Position;
        low.Step(default);
        Assert.Contains(low.Events, e => e.Type == PlaneEventType.ShrineDenied);
        Assert.False(stand.Sold);

        // Enough: two pips given, the pearl taken.
        var w = PipWorld(map);
        stand = w.Stands.Single(s => s.Kind == StandKind.Shrine);
        int halves = LightPips.Halves(w.Player.Hp);
        w.Player.Position = w.Player.PrevPosition = stand.Position;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.ShrineGiven);
        Assert.True(stand.Sold);
        Assert.Equal(halves - LightPips.ShrineHalves, LightPips.Halves(w.Player.Hp));
        Assert.Contains(w.Pearls, q => q.ItemId == stand.ItemId);
    }

    static readonly Lazy<LevelMap> BossMap = new(() =>
        TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), Ids.First(id => !id.IsTutorial && LevelPlan.For(TestLevels.Seed, id, crossover: true).HasBoss), crossover: true));

    [Fact]
    public void A_template_boss_level_ends_in_a_sealed_round_arena_round_the_Crack()
    {
        var map = BossMap.Value;
        Assert.True(map.Templated && map.HasBoss);
        Assert.Null(LevelValidator.Validate(map));
        Assert.Equal(CaveLayout.ArenaRadius, map.Exit.Radius);
        Assert.Contains(map.WeakRocks, r => r.Kind == SealKind.Boss);
        Assert.NotEmpty(map.Holes);
        Assert.True(map.InShaft(map.Exit.Position, 0.5f), "no Crack in the arena's middle");
        // Its floor is open water all round, out to most of its radius.
        for (int k = 0; k < 16; k++)
        {
            var q = map.Exit.Position + new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f)) * (CaveLayout.ArenaRadius * 0.75f);
            Assert.True(map.IsOpen(q), $"rock in the arena at {q}");
        }
    }

    [Fact]
    public void The_first_depth_s_boss_is_Queen_Clam()
    {
        Assert.Equal("Queen Clam", OctoShoots.Core.Bosses.BossRoster.For(TestLevels.Seed.Value, 1, 1, crossover: true).Name);
        Assert.NotEqual("Queen Clam", OctoShoots.Core.Bosses.BossRoster.For(TestLevels.Seed.Value, 1, 1).Name);
        var w = new PlaneWorld(BossMap.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        if (BossMap.Value.Depth - LevelPlan.Tutorial == 1) Assert.Equal("Queen Clam", w.Boss!.Spec.Name);
        Assert.False(w.CanDive);
    }

    [Fact]
    public void On_a_boss_level_the_clock_raises_a_current_that_carries_her_to_the_arena()
    {
        var w = new PlaneWorld(BossMap.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        var reach = LevelValidator.Distances(w.Map, w.ArenaCenter);
        Assert.False(w.TideActive);
        Assert.Equal(Vector2.Zero, w.TideAt(w.Player.Position));
        w.DebugClockTo(TideTuning.After + 0.5f);
        w.Step(default);
        Assert.True(w.TideActive);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.TideRose);
        Assert.Equal(Vector2.Zero, w.TideAt(w.ArenaCenter));
        // Left to it, she is carried nearer the arena along the water, faster as it rises.
        float before = LevelValidator.DistanceAt(reach, w.Player.Position);
        for (int i = 0; i < 600 && !w.Defeated; i++) w.Step(default);
        float after = LevelValidator.DistanceAt(reach, w.Player.Position);
        Assert.True(after < before - 10f, $"{before:0.0} m from the arena, then {after:0.0} m");
        Assert.True(w.TideStrength > TideTuning.Start);
    }

    [Fact]
    public void A_creature_the_current_brings_to_the_arena_dissolves_and_feeds_the_boss()
    {
        var w = new PlaneWorld(BossMap.Value, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        w.DebugClockTo(TideTuning.After + 0.5f);
        float max = w.Boss!.MaxHp;
        // A free-swimming creature just outside the arena's mouth, on open water toward the start.
        var path = LevelValidator.ShortestPath(w.Map, w.ArenaCenter, w.Map.Start.Position, clearance: 1f);
        var at = path.First(q => Vector2.Distance(q, w.ArenaCenter) > CaveLayout.ArenaRadius + TideTuning.Mouth + 1.5f);
        var mob = new PlaneMob { Position = at, Home = at, Rng = new Rng(1) };
        w.Mobs.Add(mob);
        int dissolved = 0;
        for (int i = 0; i < 600 && dissolved == 0; i++)
        {
            w.Step(default);
            dissolved = w.Dissolved;
        }
        Assert.True(dissolved > 0, $"nothing dissolved: from {at} ({Vector2.Distance(at, w.ArenaCenter)} m) to {mob.Position} ({Vector2.Distance(mob.Position, w.ArenaCenter)} m), her {w.Player.Position}, flow there {w.TideAt(at)}");
        Assert.True(w.Boss.MaxHp > max);
        Assert.True(w.Boss.MaxHp <= max * (1f + TideTuning.FeedMost) + 0.01f);
    }

    static readonly Lazy<TreasureCatalog> TreasureData = new(() =>
        TreasureCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "treasures.json"))));

    [Fact]
    public void The_treasures_load_one_to_a_biome_and_each_is_known()
    {
        var all = TreasureData.Value.All;
        Assert.Equal(new[] { Treasures.PearlAltar, Treasures.SpareBell, Treasures.ShellUrn, Treasures.OilLamp }, all.Select(t => t.Id));
        Assert.Equal(Treasures.PearlAltar, TreasureData.Value.AtDepth(1)?.Id);
        Assert.Throws<InvalidOperationException>(() => TreasureCatalog.FromJson(
            "{ \"treasures\": [ { \"id\": \"golden_fork\", \"title\": \"Fork\", \"depth\": 1, \"line\": \"a\", \"does\": \"b\" } ] }"));
    }

    [Fact]
    public void Each_depth_hides_its_treasure_on_one_level_far_from_the_walk()
    {
        foreach (var seed in new[] { TestLevels.Seed, SeedCode.Parse("REEF22XY") })
        for (int depth = LevelPlan.Tutorial + 1; depth < LevelPlan.Depths; depth++)
        {
            var levels = Enumerable.Range(1, LevelStocking.LevelsPerDepth)
                .Where(level => LevelPlan.For(seed, new LevelId(1, depth, level), crossover: true).Relics == 1).ToList();
            int only = Assert.Single(levels);
            Assert.InRange(only, 2, LevelStocking.LevelsPerDepth - 1);
        }
        var id = new LevelId(1, LevelPlan.Tutorial + 1, LevelPlan.RelicLevel(TestLevels.Seed, new LevelId(1, LevelPlan.Tutorial + 1, 1)));
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        var relic = Assert.Single(map.Pois, p => p.Kind == PoiKind.Relic);
        Assert.True(float.IsFinite(LevelValidator.DistanceAt(LevelValidator.Distances(map, map.Start.Position), relic.Position)));
        Assert.True(Vector2.Distance(relic.Position, map.Start.Position) >= PlaceRules.FromStart);
    }

    static LevelMap RelicMap() => TopDownGenerator.Generate(new RunStreams(TestLevels.Seed),
        new LevelId(1, LevelPlan.Tutorial + 1, LevelPlan.RelicLevel(TestLevels.Seed, new LevelId(1, LevelPlan.Tutorial + 1, 1))), crossover: true);

    [Fact]
    public void She_finds_a_treasure_by_touching_it_and_once_found_its_place_holds_a_pearl()
    {
        var map = RelicMap();
        var run = new PlaneRun(Catalog.Value, new Tuning()) { TreasureCatalog = TreasureData.Value };
        var w = new PlaneWorld(map, new Tuning(), run);
        var relic = Assert.Single(w.Relics);
        Assert.Equal(Treasures.PearlAltar, relic.Id);
        w.Player.Position = w.Player.PrevPosition = relic.Position;
        w.Step(default);
        Assert.True(relic.Taken);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.TreasureFound);
        Assert.Equal(Treasures.PearlAltar, w.LastTreasure);
        Assert.Contains(Treasures.PearlAltar, run.Treasures);

        // Found already: the place rolls a pearl instead.
        var again = new PlaneRun(Catalog.Value, new Tuning()) { TreasureCatalog = TreasureData.Value };
        again.Treasures.Add(Treasures.PearlAltar);
        var w2 = new PlaneWorld(map, new Tuning(), again);
        Assert.Empty(w2.Relics);
        Assert.Contains(w2.Pearls, q => q.Position == relic.Position);
    }

    [Fact]
    public void The_start_cave_holds_her_treasures_the_altar_offers_three_pearls_and_she_takes_one()
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), LevelId.FirstRun, crossover: true);
        var bare = new PlaneRun(Catalog.Value, new Tuning()) { Pips = true };
        var run = new PlaneRun(Catalog.Value, new Tuning()) { Pips = true, TreasureCatalog = TreasureData.Value };
        run.BeginWithTreasures(Treasures.Known);
        // The Spare Bell: one more pip.
        Assert.Equal(LightPips.MaxHalves(bare.MaxHp) + Treasures.BellHalves, LightPips.MaxHalves(run.MaxHp));
        Assert.Equal(run.MaxHp, run.Hp);

        var w = new PlaneWorld(map, new Tuning(), run);
        Assert.False(run.StartCave);
        Assert.Equal(Treasures.Known.OrderBy(x => x), w.Relics.Where(r => r.InStartCave).Select(r => r.Id).OrderBy(x => x));
        // The Shell Urn's fifteen shells, spilled.
        Assert.Equal(Treasures.UrnShells, w.Shells.Where(s => Vector2.Distance(s.Position, map.Start.Position) < 10f).Sum(s => s.Value));
        var offer = w.Pearls.Where(q => q.Choice == 1).ToList();
        Assert.Equal(Treasures.AltarPearls, offer.Count);
        Assert.Equal(Treasures.AltarPearls, offer.Select(q => q.ItemId).Distinct().Count());
        Assert.All(offer, q => Assert.True(map.IsOpen(q.Position)));

        // She takes one: the others fade, and it is hers.
        w.Player.Position = w.Player.PrevPosition = offer[0].Position;
        for (int i = 0; i < 200; i++) w.Step(default);
        Assert.Contains(offer[0].ItemId, run.Items);
        Assert.All(offer.Skip(1), q => Assert.True(q.Taken));
        Assert.DoesNotContain(offer[1].ItemId, run.Items);
        Assert.DoesNotContain(offer[2].ItemId, run.Items);

        // Without treasures, or on a later level, the start is bare.
        var plain = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()) { TreasureCatalog = TreasureData.Value });
        Assert.Empty(plain.Relics);
        Assert.DoesNotContain(plain.Pearls, q => q.Choice != 0);
    }
}
