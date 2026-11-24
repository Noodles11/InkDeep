using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The Sea-pedia's codex: entries found in play (PlaneCodex.cs), kept in the profile.</summary>
public class CodexTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, 1 + LevelPlan.Tutorial, 2), crossover: true);
        return new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Flora = true, Pockets = true, Fauna = true });
    }

    [Fact]
    public void Every_kind_the_world_can_report_has_an_entry()
    {
        foreach (var k in Enum.GetValues<PoiKind>().Where(k => k is not (PoiKind.Start or PoiKind.Exit or PoiKind.Hole)))
            Assert.True(Codex.Has(Codex.Place(k)), k.ToString());
        foreach (var b in Enum.GetValues<Biome>()) Assert.True(Codex.Has(Codex.Biome(b)), b.ToString());
        foreach (var c in Enum.GetValues<BossConcept>()) Assert.True(Codex.Has(Codex.Boss(c)), c.ToString());
        foreach (var t in Enum.GetValues<MurkTrait>().Where(t => t != MurkTrait.None)) Assert.True(Codex.Has("trait:" + t), t.ToString());
        foreach (var f in Enum.GetValues<FloraKind>()) Assert.True(Codex.Has("flora:" + f), f.ToString());
        foreach (var f in Enum.GetValues<FaunaKind>()) Assert.True(Codex.Has("fauna:" + f), f.ToString());
        foreach (var f in Enum.GetValues<ReefFishKind>()) Assert.True(Codex.Has("fish:" + f), f.ToString());
        foreach (var p in Enum.GetValues<PocketKind>()) Assert.True(Codex.Has("water:" + p), p.ToString());
        foreach (var o in Enum.GetValues<OpticKind>()) Assert.True(Codex.Has("wall:" + o), o.ToString());
        Assert.Equal(Codex.All.Count, Codex.All.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void She_finds_the_biome_a_place_she_enters_and_a_plant_she_sees_once_each()
    {
        var w = World();
        w.Step(default);
        Assert.Contains("biome:SunlitReef", w.Discoveries);
        var poi = w.Map.Pois.First(p => p.Kind is not (PoiKind.Start or PoiKind.Exit or PoiKind.Hole));
        var plant = w.AddFlora(FloraKind.NightLily, w.Player.Position + new Vector2(0f, 0f));
        bool place = false, lily = false, again = false;
        for (int i = 0; i < 12; i++)
        {
            w.Player.Position = w.Player.PrevPosition = i < 6 ? plant.Position : poi.Position;
            w.Step(default);
            place |= w.Discoveries.Contains(Codex.Place(poi.Kind));
            again |= lily && w.Discoveries.Contains("flora:NightLily");
            lily |= w.Discoveries.Contains("flora:NightLily");
        }
        Assert.True(lily, "never saw the lily");
        Assert.True(place, $"never entered the {poi.Kind}");
        Assert.False(again, "found twice");
    }

    [Fact]
    public void The_profile_keeps_each_find_once_and_the_old_records_count()
    {
        var profile = new Profile();
        profile.SeenCreatures.Add(PlaneProfileRecorder.MobId);
        var rec = new PlaneProfileRecorder(profile);
        var w = World();
        w.Step(default);
        rec.Observe(w);
        Assert.Contains("biome:SunlitReef", profile.Discovered);
        Assert.Contains("biome:SunlitReef", rec.TakeNewEntries());
        Assert.Empty(rec.TakeNewEntries());
        Assert.True(PlaneProfileRecorder.Knows(profile, "mob:pufferling"));
        Assert.False(PlaneProfileRecorder.Knows(profile, "boss:TheLid"));
        // (A new world of the same biome finds nothing new.)
        var again = World();
        again.Step(default);
        rec.Observe(again);
        Assert.Empty(rec.TakeNewEntries());
    }
}
