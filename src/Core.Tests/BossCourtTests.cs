using System;
using System.Linq;
using OctoShoots.Core.Bosses;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The Drowned Court (docs/BOSS-GENERATOR.md §15): the cube reef's bosses, a pool per band of layers.</summary>
public class BossCourtTests
{
    [Fact]
    public void Every_band_has_a_pool_of_three_or_more_and_every_boss_is_met_somewhere()
    {
        for (int layer = 1; layer <= 36; layer++) Assert.True(BossCourt.Pool(layer).Count >= 3, $"layer {layer}");
        foreach (var boss in BossCourt.All) Assert.Contains(Enumerable.Range(1, 36), z => BossCourt.Pool(z).Contains(boss));
        Assert.Equal(BossCourt.All.Length, BossCourt.All.Select(b => b.Id).Distinct().Count());
        Assert.Equal(BossCourt.All.Length, BossCourt.All.Select(b => b.Name).Distinct().Count());
    }

    [Fact]
    public void Each_of_the_court_is_a_fair_fight_on_every_layer_it_is_met_on()
    {
        foreach (var boss in BossCourt.All)
        foreach (int layer in Enumerable.Range(1, 36).Where(z => BossCourt.Pool(z).Contains(boss)))
        {
            var spec = BossCourt.Make(boss.Id, 0xC0FFEEUL, layer)!;
            Assert.Equal(boss.Name, spec.Name);
            Assert.True(spec.Blockade);
            Assert.True(spec.Hp > 0f);
            // (Drippus Prime is all ink, held to its own rules: DrippusTests.)
            if (boss.Id == "drippus") continue;
            // Judged at the menace's own top: deeper, the court only grows hardier.
            if (layer > 18) continue;
            Assert.True(BossValidator.Validate(spec) is null, $"{boss.Name} on layer {layer}: {BossValidator.Validate(spec)}");
        }
    }

    [Fact]
    public void A_layers_boss_comes_from_its_pool_by_the_seed_and_never_twice_running()
    {
        foreach (ulong seed in new[] { 1UL, 7UL, 0xC0FFEEUL, 123456789UL })
        {
            for (int layer = 1; layer <= 36; layer++)
            {
                var pick = BossCourt.Pick(seed, layer);
                Assert.Contains(pick, BossCourt.Pool(layer));
                Assert.Equal(pick, BossCourt.Pick(seed, layer));
                if (layer > 1) Assert.NotEqual(BossCourt.Pick(seed, layer - 1).Id, pick.Id);
                Assert.Equal(pick.Name, BossCourt.For(seed, layer).Name);
            }
        }
        // Over many reefs every one of a band's pool turns up.
        foreach (int layer in BossCourt.Bands)
        {
            var met = Enumerable.Range(0, 200).Select(s => BossCourt.Pick((ulong)s * 7919UL + 3UL, layer).Id).Distinct().Count();
            Assert.Equal(BossCourt.Pool(layer).Count, met);
        }
    }

    [Fact]
    public void The_court_grows_hardier_with_depth_and_an_unknown_id_is_no_one()
    {
        Assert.Null(BossCourt.Make("nobody", 1UL, 3));
        Assert.True(BossCourt.Make("herald", 1UL, 30)!.Hp > BossCourt.Make("herald", 1UL, 13)!.Hp);
        Assert.True(BossCourt.Make("bell", 1UL, 5)!.Hp > BossCourt.Make("bell", 1UL, 1)!.Hp);
        Assert.Equal(0, BossCourt.Band(0));
        Assert.Equal(3, BossCourt.Band(36));
    }
}
