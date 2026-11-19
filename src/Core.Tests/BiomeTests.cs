using System;
using System.IO;
using System.Linq;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// The crossover's route (docs/proposals/CROSSOVER.md §8, Run.Biomes): a biome a depth, the first depth's arena opening
/// two ways down (the Kelp Forest and the Shipwreck Graveyard), the branch kept for the depth, both leading on to the
/// Sunken Ruins; each biome's boss, creatures and treasure.
/// </summary>
public class BiomeTests
{
    /// <summary>A level of a depth as the player counts them (1 the Sunlit Reef).</summary>
    static LevelId At(int shownDepth, int level, int branch = 0) =>
        new LevelId(1, shownDepth + LevelPlan.Tutorial, level) { Branch = branch };

    [Fact]
    public void Each_depth_is_its_biome_and_the_second_branches()
    {
        Assert.Equal(Biome.SunlitReef, Biomes.Of(LevelId.First));
        Assert.Equal(Biome.SunlitReef, Biomes.Of(At(1, 3)));
        Assert.Equal(Biome.KelpForest, Biomes.Of(At(2, 1)));
        Assert.Equal(Biome.Shipwreck, Biomes.Of(At(2, 1, branch: 1)));
        Assert.Equal(Biome.SunkenRuins, Biomes.Of(At(3, 2)));
    }

    [Fact]
    public void Queen_Clams_arena_opens_one_way_to_each_biome_below_and_the_branch_is_kept_down_the_depth()
    {
        var seed = TestLevels.Seed;
        int boss = LevelStocking.LevelsPerDepth;
        var below = Biomes.Exits(seed, At(1, boss));
        Assert.Equal(2, below.Count);
        Assert.Equal(new[] { Biome.KelpForest, Biome.Shipwreck }, below.Select(Biomes.Of));
        Assert.All(below, e => Assert.Equal(1, e.Level));
        // Down the Shipwreck's levels its branch is kept, and its own boss leads one way on, to the Ruins.
        var at = below[1];
        for (int level = 1; level < boss; level++)
        {
            var next = Assert.Single(Biomes.Exits(seed, at));
            Assert.Equal(1, next.Branch);
            Assert.Equal(Biome.Shipwreck, Biomes.Of(next));
            at = next;
        }
        Assert.Equal(boss, at.Level);
        var ruins = Assert.Single(Biomes.Exits(seed, at));
        Assert.Equal(Biome.SunkenRuins, Biomes.Of(ruins));
        Assert.Equal(0, ruins.Branch);
    }

    [Fact]
    public void The_two_branches_are_different_levels()
    {
        var streams = new RunStreams(TestLevels.Seed);
        var kelp = TopDownGenerator.Generate(streams, At(2, 1), crossover: true);
        var wreck = TopDownGenerator.Generate(streams, At(2, 1, branch: 1), crossover: true);
        Assert.Equal(Biome.KelpForest, kelp.Biome);
        Assert.Equal(Biome.Shipwreck, wreck.Biome);
        Assert.False(kelp.Heights.SequenceEqual(wreck.Heights));
        // (The Kelp Forest is the kelp's own biome.)
        Assert.True(kelp.Kelp.Count >= TopDownGenerator.KelpDepthMin || kelp.Kelp.Count > wreck.Kelp.Count);
    }

    [Fact]
    public void A_branch_arena_s_Crack_is_two_vortices_each_to_its_biome()
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), At(1, LevelStocking.LevelsPerDepth), crossover: true);
        Assert.True(map.HasBoss);
        Assert.Equal(2, map.Holes.Count);
        Assert.All(map.Holes, h => Assert.True(map.Shaft.Contains(h.Shaft.Center)));
        Assert.Equal(new[] { Biome.KelpForest, Biome.Shipwreck }, map.Holes.Select(h => Biomes.Of(h.To)));
        // (Not a branch: the Kelp Forest's arena has its one Crack.)
        var kelp = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), At(2, LevelStocking.LevelsPerDepth), crossover: true);
        var only = Assert.Single(kelp.Holes);
        Assert.Equal(Biome.SunkenRuins, Biomes.Of(only.To));
    }

    [Theory]
    [InlineData(Biome.SunlitReef, 1, "Queen Clam")]
    [InlineData(Biome.KelpForest, 2, "Tull")]
    [InlineData(Biome.Shipwreck, 2, "Kip")]
    [InlineData(Biome.SunkenRuins, 3, "Ottilie")]
    public void Each_biome_s_boss_holds_a_corrupted_pearl(Biome biome, int depth, string name)
    {
        var spec = BossRoster.For(TestLevels.Seed.Value, 1, depth, crossover: true, biome: biome);
        Assert.Contains(name, spec.Name);
        Assert.True(spec.PearlRadius > 0f);
        // (Nobody's neighbour in the crossover: freeing her sends no one home.)
        Assert.Null(spec.Folk);
        Assert.Equal(LevelPlan.MenaceOf(At(depth, 1)), spec.Menace, 3);
    }

    [Fact]
    public void A_biome_s_creatures_are_its_roster_s()
    {
        var streams = new RunStreams(TestLevels.Seed);
        foreach (var (id, biome) in new[] { (At(2, 2), Biome.KelpForest), (At(2, 2, 1), Biome.Shipwreck), (At(3, 2), Biome.SunkenRuins) })
        {
            var map = TopDownGenerator.Generate(streams, id, crossover: true);
            var r = Biomes.Roster(biome);
            var allowed = r.Lone.Concat(r.Pack).Concat(r.Thrower).Concat(r.Ambusher).Concat(r.Guard).ToHashSet();
            Assert.NotEmpty(map.Spawns);
            Assert.All(map.Spawns, s => Assert.Contains(s.Kind, allowed));
        }
    }

    [Fact]
    public void Each_biome_hides_its_own_treasure()
    {
        var data = TreasureCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "treasures.json")));
        Assert.Equal(Treasures.PearlAltar, data.InBiome(Biome.SunlitReef)?.Id);
        Assert.Equal(Treasures.SpareBell, data.InBiome(Biome.KelpForest)?.Id);
        Assert.Equal(Treasures.ShellUrn, data.InBiome(Biome.Shipwreck)?.Id);
        Assert.Equal(Treasures.OilLamp, data.InBiome(Biome.SunkenRuins)?.Id);
        // (The Oil Lamp's reach is hers for every run once found.)
        var run = new OctoShoots.Core.Plane.PlaneRun(ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))), new Tuning());
        run.BeginWithTreasures(new[] { Treasures.OilLamp });
        Assert.Equal(Treasures.LampReach, run.BeamReach);
        Assert.Throws<InvalidOperationException>(() => TreasureCatalog.FromJson(
            "{ \"treasures\": [ { \"id\": \"pearl_altar\", \"title\": \"A\", \"biome\": \"The Moon\", \"depth\": 1, \"line\": \"a\", \"does\": \"b\" } ] }"));
    }

    [Theory]
    [InlineData(Biome.KelpForest)]
    [InlineData(Biome.Shipwreck)]
    [InlineData(Biome.SunkenRuins)]
    public void Each_biome_has_its_own_cell_for_every_way_a_cell_can_open(Biome biome)
    {
        var cells = CaveTemplates.VariantsOf(biome);
        Assert.NotSame(CaveTemplates.SunlitVariants, cells);
        for (int opens = 1; opens < 16; opens++)
            Assert.Contains(cells, v => v.Openings == opens);
        Assert.NotEmpty(CaveTemplates.ChunksOf(biome));
    }

    [Fact]
    public void Each_biome_s_levels_are_made_from_its_cells()
    {
        var streams = new RunStreams(TestLevels.Seed);
        foreach (var (depth, branch) in new[] { (2, 0), (2, 1), (3, 0) })
            for (int level = 1; level <= LevelStocking.LevelsPerDepth; level++)
            {
                var map = TopDownGenerator.Generate(streams, At(depth, level, branch), crossover: true);
                Assert.True(map.Templated);
                Assert.Equal(Biomes.Of(At(depth, level, branch)), map.Biome);
                Assert.Null(LevelValidator.Validate(map));
            }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    public void Every_level_of_a_biome_is_built_only_from_that_biome_s_own_cells(int shownDepth, int branch)
    {
        var biome = Biomes.Of(At(shownDepth, 1, branch));
        var own = CaveTemplates.VariantsOf(biome).Select(v => v.Name).ToHashSet();
        var rooms = CaveTemplates.RoomVariants.Select(v => v.Name).ToHashSet();
        foreach (var seed in new[] { TestLevels.Seed, SeedCode.FromValue(0xC0FFEEUL), SeedCode.FromValue(0xBEEF42UL) })
            for (int level = 1; level <= LevelStocking.LevelsPerDepth; level++)
            {
                var map = TopDownGenerator.Generate(new RunStreams(seed), At(shownDepth, level, branch), crossover: true);
                Assert.NotEmpty(map.Cells);
                Assert.All(map.Cells, name => Assert.True(own.Contains(name) || rooms.Contains(name), $"{biome} level {level} uses the cell '{name}'"));
            }
        // (And no two biomes share a cell.)
        foreach (var other in Enum.GetValues<Biome>().Where(b => b != biome))
            Assert.Empty(own.Intersect(CaveTemplates.VariantsOf(other).Select(v => v.Name)));
    }

    [Fact]
    public void A_saved_run_keeps_its_branch()
    {
        var id = At(2, 3, branch: 1);
        var saved = new SuspendedRun { Seed = TestLevels.Seed.ToString(), Cycle = id.Cycle, Depth = id.Depth, Level = id.Level, Lane = id.Lane, Branch = id.Branch };
        Assert.Equal(1, saved.Reef().Branch);
        Assert.Equal(Biome.Shipwreck, Biomes.Of(saved.Reef()));
    }
}
