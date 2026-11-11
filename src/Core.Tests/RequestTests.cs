using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The Tide Fan's requests (docs/archive/QUESTS-PROPOSAL.md): tied from her seed, taken, done, untied.</summary>
public class RequestTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static Jelly NewJelly(string seed = "KELP7Q2Z") => new() { Name = "Mira", Seed = seed };

    static void Fill(Jelly j, Func<int, bool>? den = null) =>
        Requests.Fill(j, "reef", _ => 1, 3, den ?? (_ => false), _ => true, Catalog.Value);

    [Fact]
    public void The_fan_ties_three_ribbons_from_her_seed_a_seal_among_them_and_no_pearl_twice()
    {
        var a = NewJelly();
        Fill(a, m => m == 2);
        var places = Requests.Places(a, "reef");
        Assert.Equal(RequestTuning.Places, places.Count);
        Assert.All(places, r => Assert.NotNull(r));
        Assert.Contains(places, r => r!.Kind == Requests.Seal);
        var pearls = places.Where(r => r!.Pearl is not null).Select(r => r!.Pearl).ToList();
        Assert.Equal(pearls.Count, pearls.Distinct().Count());
        Assert.All(places, r =>
        {
            Assert.Equal(RequestTuning.Xp(r!.Level), r.Xp);
            if (r.Kind is Requests.Seal or Requests.Den) Assert.InRange(r.Mouth, 0, 2);
            if (r.Kind == Requests.Den) Assert.Equal(2, r.Mouth);
            if (r.Pearl is { } id) Assert.True(Catalog.Value.TryGet(id, out var item) && item.Kind == ItemKind.Passive);
        });
        // The same jelly, the same ribbons; another jelly, her own.
        var again = NewJelly();
        Fill(again, m => m == 2);
        Assert.Equal(places.Select(r => (r!.Kind, r.Mouth, r.Pearl, r.Poster)), Requests.Places(again, "reef").Select(r => (r!.Kind, r.Mouth, r.Pearl, r.Poster)));
        Assert.Equal(3, a.Tied["reef"]);
    }

    [Fact]
    public void She_carries_two_at_most_and_one_let_go_goes_back()
    {
        var j = NewJelly();
        Fill(j);
        var places = Requests.Places(j, "reef");
        int seal = places.FindIndex(r => r!.Kind == Requests.Seal);
        Assert.True(Requests.Take(j, "reef", seal));
        Assert.Null(places[seal]);
        int other = places.FindIndex(r => r is not null);
        Assert.True(Requests.Take(j, "reef", other));
        int third = places.FindIndex(r => r is not null);
        Assert.False(Requests.Take(j, "reef", third));
        Assert.Equal(RequestTuning.Carry, j.Carried.Count);
        Requests.LetGo(j, 1);
        Assert.Single(j.Carried);
        Assert.Equal(2, places.Count(r => r is not null));
        // Coming home fills the empty place with a new one.
        Fill(j);
        Assert.All(places, r => Assert.NotNull(r));
    }

    [Fact]
    public void Each_kind_is_done_by_its_own_deed_in_its_own_region()
    {
        var j = NewJelly();
        j.Carried.Add(new Request { Kind = Requests.Seal, Region = "reef", Mouth = 1 });
        j.Carried.Add(new Request { Kind = Requests.Ash, Region = "reef", Goal = 150 });
        Assert.Empty(Requests.Note(j, Requests.Seal, "reef", mouth: 0));
        Assert.Empty(Requests.Note(j, Requests.Seal, "kelp", mouth: 1));
        Assert.Single(Requests.Note(j, Requests.Seal, "reef", mouth: 1));
        Assert.True(j.Carried[0].Done);
        Assert.Empty(Requests.Note(j, Requests.Ash, "reef", amount: 100));
        Assert.Equal(100, j.Carried[1].Progress);
        Assert.Single(Requests.Note(j, Requests.Ash, "reef", amount: 80));
        Assert.Equal(150, j.Carried[1].Progress);
        // Only a done one unties.
        j.Carried.Add(new Request { Kind = Requests.Prime, Region = "reef" });
        Assert.Null(Requests.Untie(j, 2));
        Assert.NotNull(Requests.Untie(j, 0));
        Assert.Equal(2, j.Carried.Count);
    }

    [Fact]
    public void Freeing_a_caves_prime_lets_its_swallowed_school_out_streaming_for_the_mouth()
    {
        var seed = SeedCode.TryParse("KELP7Q2Z", out var s) ? s : default;
        var map = TopDownGenerator.GenerateCave(seed, new LevelId(1001, 1, 1), 0f, 2);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        var prime = w.Murklings.First(m => m.Prime);
        Assert.Empty(w.ReefFish);
        w.DebugFreeMurkling(prime);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.PrimeFreed && (int)e.Size == PlaneWorld.SwallowedSchool);
        Assert.Equal(PlaneWorld.SwallowedSchool, w.ReefFish.Count);
        Assert.All(w.ReefFish, f => Assert.True(f.Freed && f.Kind == ReefFishKind.Chromis));
        // Toward the mouth.
        var home = Vector2.Normalize(map.Start.Position - prime.Position);
        Assert.All(w.ReefFish, f => Assert.True(Vector2.Dot(f.Leaving, home) > 0.8f));
    }

    [Fact]
    public void Given_experience_is_paid_with_the_step_like_a_leaks()
    {
        var seed = SeedCode.TryParse("KELP7Q2Z", out var s) ? s : default;
        var map = TopDownGenerator.GenerateCave(seed, new LevelId(1001, 1, 1), 0f, 2);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        int before = w.Run.Xp;
        w.GiveXp(RequestTuning.Xp(2));
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.XpGained && (int)e.Size == RequestTuning.Xp(2));
        Assert.True(w.Run.Xp > before || w.Run.XpLevel > 1);
    }
}
