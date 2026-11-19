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
            Assert.Equal(dive.Exits, cross.Exits);
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
    public void Only_ordinary_crossover_levels_are_templated()
    {
        foreach (var id in Ids)
        {
            var plan = LevelPlan.For(TestLevels.Seed, id, crossover: true);
            Assert.Equal(!plan.HasBoss && !id.IsTutorial, plan.Templated);
            Assert.False(LevelPlan.For(TestLevels.Seed, id).Templated);
        }
        Assert.False(LevelPlan.For(TestLevels.Seed, LevelId.First, crossover: true).Templated);
    }

    [Fact]
    public void A_template_level_is_one_cave_with_no_shaft_and_its_marks_in_the_water()
    {
        foreach (var map in Templated.Value)
        {
            Assert.True(map.Templated);
            Assert.Null(LevelValidator.Validate(map));
            Assert.Empty(map.Holes);
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
                Assert.DoesNotContain(poi.Kind, new[] { PoiKind.Secret, PoiKind.Hole, PoiKind.LeakCave, PoiKind.Haven, PoiKind.CurseDen });
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
    public void On_a_template_level_she_dives_anywhere_once_the_clock_has_run()
    {
        var map = Templated.Value[0];
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));
        int ticks = (int)MathF.Ceiling(DiveClock.Seconds / PlaneWorld.Dt);
        bool opened = false, earlyDive = false;
        for (int i = 0; i < ticks - 2; i++)
        {
            w.Step(new PlaneInput { Dive = true });
            earlyDive |= w.Events.Any(e => e.Type == PlaneEventType.Dived);
            opened |= w.Events.Any(e => e.Type == PlaneEventType.DiveOpened);
            Assert.False(w.Defeated, "overrun before the clock ran");
        }
        Assert.False(earlyDive, "dived before the clock ran");
        Assert.False(opened);
        Assert.False(w.CanDive);
        for (int i = 0; i < 4; i++) w.Step(default);
        Assert.True(w.CanDive);
        Assert.False(w.OverShaft);
        w.Step(new PlaneInput { Dive = true });
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Dived);
    }

    [Fact]
    public void The_dive_clock_runs_two_minutes()
    {
        Assert.Equal(120f, DiveClock.Seconds);
        Assert.False(DiveClock.Open(119.9f));
        Assert.True(DiveClock.Open(120f));
        Assert.Equal(0.5f, DiveClock.Progress(60f));
        Assert.Equal(1f, DiveClock.Progress(500f));
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
}
