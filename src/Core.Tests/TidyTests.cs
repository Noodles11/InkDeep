using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;
using Xunit.Abstractions;

namespace OctoShoots.Core.Tests;

/// <summary>A tidy level (PlaneTidy.cs): a few light features, quiet water, fewer specks; springs found by swimming over them.</summary>
public class TidyTests
{
    readonly ITestOutputHelper _out;
    public TidyTests(ITestOutputHelper output) => _out = output;

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(Biome biome, bool tidy, int level = 1)
    {
        var (depth, branch) = biome switch { Biome.Shipwreck => (2, 1), Biome.DrownedTemple => (4, 1), Biome.Atlantis => (5, 0), _ => (1, 0) };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, depth + LevelPlan.Tutorial, level) with { Branch = branch }, crossover: true);
        return new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Specks = true, Flora = true, Pockets = true, Fauna = true, Tidy = tidy });
    }

    static int Kinds(PlaneWorld w) =>
        (w.OpticWalls.Any(o => o.Kind == OpticKind.Mirror && !w.LightRooms.Any(r => r.Mirrors.Contains(o))) ? 1 : 0)
        + (w.OpticWalls.Any(o => o.Kind == OpticKind.Lightglass) ? 1 : 0)
        + (w.OpticWalls.Any(o => o.Kind is OpticKind.SunStone or OpticKind.ShadowStone) ? 1 : 0)
        + (w.OpticWalls.Any(o => o.Kind == OpticKind.Halocline) ? 1 : 0)
        + (w.Pockets.Count > 0 ? 1 : 0) + (w.LightRooms.Count > 0 ? 1 : 0)
        + (w.Flora.Any(f => f.Room < 0) ? 1 : 0) + (w.Fauna.Count > 0 ? 1 : 0);

    [Theory]
    [InlineData(Biome.Atlantis)]
    [InlineData(Biome.DrownedTemple)]
    [InlineData(Biome.SunlitReef)]
    public void A_tidy_level_holds_a_few_of_its_biome_s_features_and_none_in_its_quiet_water(Biome biome)
    {
        for (int level = 1; level <= 3; level++)
        {
            var w = World(biome, tidy: true, level);
            var full = World(biome, tidy: false, level);
            int bits = Enumerable.Range(0, 8).Count(b => ((int)w.Features & (1 << b)) != 0);
            _out.WriteLine($"{biome} {level}: {w.Features} placed kinds {Kinds(w)} (untidy {Kinds(full)}), specks {w.Specks.Count} (untidy {full.Specks.Count})");
            Assert.InRange(bits, TidyTuning.FeaturesMin, TidyTuning.FeaturesMax);
            Assert.True(Kinds(w) <= bits, $"{Kinds(w)} kinds placed of {w.Features}");
            Assert.True(Kinds(w) >= 1, "nothing placed");
            Assert.DoesNotContain(w.OpticWalls, o => w.InQuietWater(o.Centre) && !w.LightRooms.Any(r => r.Mirrors.Contains(o)));
            Assert.DoesNotContain(w.Flora, f => w.InQuietWater(f.Position) && f.Room < 0);
            Assert.DoesNotContain(w.LightRooms, r => w.InQuietWater(r.Centre));
            Assert.DoesNotContain(w.Pockets, p => w.InQuietWater(p.Centre));
            Assert.DoesNotContain(w.Fauna, f => w.InQuietWater(f.Home));
            Assert.DoesNotContain(w.Specks, s => w.InQuietWater(s.Position));
            // (Fewer specks, the same light in them.)
            Assert.True(w.Specks.Count < full.Specks.Count);
            Assert.InRange(w.Specks.Sum(s => s.Value), full.Specks.Sum(s => s.Value) * 0.7f, full.Specks.Sum(s => s.Value) * 1.5f);
        }
        Assert.Equal(LightFeature.All, World(biome, tidy: false).Features);
    }

    [Fact]
    public void A_spring_lies_unfound_until_she_swims_over_it()
    {
        var w = World(Biome.SunlitReef, tidy: true);
        var spring = Assert.Single(w.Springs);
        Assert.False(spring.Found);
        w.Step(default);
        Assert.False(spring.Found);
        w.Player.Position = w.Player.PrevPosition = spring.Position;
        w.Step(default);
        Assert.True(spring.Found);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.SpringFound);
    }
}
