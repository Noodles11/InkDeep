using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The adventure's murklings (PlaneMurkRoster.cs, docs/ADVENTURE.md §4): budded from the ink, on every depth.</summary>
public class MurkRosterTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions Game = new() { Adventure = true, Shots = false };

    static PlaneWorld World(int depth)
    {
        var plan = TestLevels.Find(p => p.Id.Depth == depth && !p.HasBoss);
        return new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Game);
    }

    [Fact]
    public void A_reef_starts_with_murklings_of_the_ink_and_no_other_creature()
    {
        var w = World(2);
        Assert.NotEmpty(w.Murklings);
        Assert.All(w.Murklings, m => Assert.Null(m.Fish));
        Assert.Empty(w.Inkwardens);
        Assert.Empty(w.Blightroots);
        Assert.Empty(w.Blotters);
        Assert.Empty(w.Lashjaws);
        Assert.Empty(w.Thumpsquirts);
        Assert.DoesNotContain(w.Murklings, m => m.Kind == MurkKind.Hunter);
        // Each kept away from where she comes in.
        Assert.All(w.Murklings, m => Assert.True(Vector2.Distance(m.Position, w.Map.Start.Position) >= MurkRosterTuning.FromStart - 1.5f));
    }

    [Fact]
    public void Every_kind_buds_and_a_shoal_buds_its_whole_school()
    {
        // (Many reefs' worth, so each kind shows.)
        var kinds = new System.Collections.Generic.HashSet<MurkKind>();
        foreach (var id in TestLevels.All(p => !p.HasBoss).Take(10))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Game);
            foreach (var m in w.Murklings) kinds.Add(m.Kind);
            foreach (var school in w.Murklings.Where(m => m.Kind == MurkKind.Shoal).GroupBy(m => m.Swarm))
                Assert.Equal(MurkRosterTuning.ShoalLeast + (int)MathF.Round(MurkRosterTuning.ShoalPerMenace * w.Menace), school.Count());
        }
        Assert.Contains(MurkKind.Basic, kinds);
        Assert.Contains(MurkKind.Thrower, kinds);
        Assert.Contains(MurkKind.Shoal, kinds);
    }

    [Fact]
    public void The_menace_makes_them_more_and_bigger()
    {
        var shallow = World(1);
        var deep = World(TestLevels.All(p => !p.HasBoss).Max(i => i.Depth));
        Assert.True(deep.Menace > shallow.Menace);
        float Mean(PlaneWorld w) => w.Murklings.Where(m => m.Sized).Average(m => m.Full);
        Assert.True(Mean(deep) > Mean(shallow) * 1.2f, $"{Mean(deep)} vs {Mean(shallow)}");
        int Count(PlaneWorld w) => w.Murklings.Count(m => !m.IsShoal) + w.Murklings.Where(m => m.IsShoal).Select(m => m.Swarm).Distinct().Count();
        Assert.True(Count(deep) > Count(shallow), $"{Count(deep)} vs {Count(shallow)}");
    }

    [Fact]
    public void The_fish_never_turn_and_murklings_bud_out_of_the_ink_near_her_while_a_leak_is_open()
    {
        var w = World(2);
        // (Those already there taken away: so many near her would be the cap on more budding.)
        w.Murklings.Clear();
        int at = 0;
        // She waits in the ink (they bud 9–20 m from her).
        var spot = w.Dots!.Live.Select(InkDots.Centre).Where(c => w.Clear(c, 1f)).OrderByDescending(c => w.Dots.Live.Count(d => Vector2.Distance(InkDots.Centre(d), c) is > 9f and < 20f)).First();
        for (int i = 0; i < 40 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = spot;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
        Assert.DoesNotContain(w.ReefFish, f => f.Corrupted || f.Turning > 0f);
        Assert.Empty(w.Inkwardens);
        Assert.DoesNotContain(w.Flounders, f => f.Corrupted);
        Assert.True(w.Murklings.Count > at, "none budded");
    }
}
