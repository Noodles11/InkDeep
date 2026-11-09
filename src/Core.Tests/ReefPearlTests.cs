using System;
using System.IO;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Selling pearls at the shell shop (a haven's, once Ottilie is home: docs/DIVE.md §4).</summary>
public class ReefPearlTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    [Fact]
    public void SellingPaysSilverShells_AnActiveItsWorth_APassiveHalf()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("bubble_shield");
        run.Add("triple_tentacle");
        var shield = Catalog.Value["bubble_shield"];
        var triple = Catalog.Value["triple_tentacle"];
        Assert.True(run.Sell("bubble_shield"));
        Assert.Equal(ReefTuning.PearlWorth(shield.Quality), run.Shells);
        Assert.DoesNotContain("bubble_shield", run.Items);
        Assert.True(run.Sell("triple_tentacle"));
        Assert.Equal(ReefTuning.PearlWorth(shield.Quality) + ReefTuning.PearlWorth(triple.Quality) / 2, run.Shells);
        Assert.Equal(1, run.Loadout.Shot.Multishot);
        Assert.False(run.Sell("triple_tentacle"));
    }
}
