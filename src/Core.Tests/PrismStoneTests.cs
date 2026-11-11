using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Prism stones and coloured seals, and the charge stones' trio (docs/LIGHT-AND-GRAPH.md §2.5, §2.6).</summary>
public class PrismStoneTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld Make(LevelId id)
    {
        var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.ReefFish.Clear();
        w.Fish.Clear();
        w.Inkwardens.Clear();
        w.Blotters.Clear();
        w.Thumpsquirts.Clear();
        w.Lashjaws.Clear();
        return w;
    }

    static IEnumerable<PlaneWorld> Worlds(Func<PlaneWorld, bool> match)
    {
        foreach (var id in TestLevels.All(p => TestLevels.General(p) && !p.HasBoss).Take(40))
        {
            var w = Make(id);
            if (match(w)) yield return w;
        }
    }

    [Fact]
    public void Light_through_stones_takes_their_colours_mixed_as_light_mixes()
    {
        var w = Worlds(_ => true).First();
        w.PrismStones.Clear();
        var from = new Vector2(0f, 0f);
        w.PrismStones.Add(new PrismStone { Position = new Vector2(3f, 0f), Hue = LightHue.Red });
        w.PrismStones.Add(new PrismStone { Position = new Vector2(6f, 0.5f), Hue = LightHue.Blue });
        w.PrismStones.Add(new PrismStone { Position = new Vector2(6f, 4f), Hue = LightHue.Green });
        Assert.Equal(LightHue.None, w.HueAlong(from, new Vector2(2f, 0f)));
        Assert.Equal(LightHue.Red, w.HueAlong(from, new Vector2(5f, 0f)));
        Assert.Equal(LightHue.Magenta, w.HueAlong(from, new Vector2(10f, 0f)));
        var parts = new List<(float In, float Out, LightHue Hue, float Across)>();
        w.HuesAlong(from, Vector2.UnitX, 10f, parts);
        Assert.Equal(2, parts.Count);
        Assert.Equal((LightHue.Red, LightHue.Magenta), (parts[0].Hue, parts[1].Hue));
        Assert.InRange(parts[0].In, 2f, 3f);
        Assert.InRange(parts[0].Out, 3f, 4f);
        // All three: white.
        w.PrismStones[2].Position = new Vector2(8f, 0f);
        Assert.Equal(LightHue.White, w.HueAlong(from, new Vector2(10f, 0f)));
    }

    [Fact]
    public void Some_secret_seals_are_coloured_with_the_stones_that_make_their_colour_in_a_bed_before_them()
    {
        int coloured = 0;
        foreach (var w in Worlds(w => w.Seals.Any(s => s.Hue != LightHue.None)))
        {
            foreach (var seal in w.Seals.Where(s => s.Hue != LightHue.None))
            {
                coloured++;
                Assert.Equal(SealKind.Secret, seal.Kind);
                int index = w.Seals.IndexOf(seal);
                var stones = w.PrismStones.Where(s => s.Seal == index).ToList();
                var made = stones.Aggregate(LightHue.None, (h, s) => h | s.Hue);
                Assert.Equal(seal.Hue, made & seal.Hue);
                foreach (var s in stones)
                {
                    Assert.True(Vector2.Distance(s.Position, s.Bed) <= PrismStoneTuning.Bed);
                    Assert.True(w.Clear(s.Position, PrismStoneTuning.Radius));
                }
                Assert.True(w.LineOfSight(stones[0].Bed, seal.Emblem + seal.Facing * 0.4f));
            }
            Assert.True(w.Seals.Count(s => s.Hue != LightHue.None) <= PrismStoneTuning.MostSeals);
        }
        Assert.True(coloured > 0, "no coloured seal in the seed's levels");
    }

    /// <summary>Her beam held on a coloured seal's carving from its bed for a while: whether it opened.</summary>
    static bool Shine(PlaneWorld w, SecretSeal seal, Vector2 her)
    {
        var aim = Vector2.Normalize(seal.Emblem - her);
        for (int i = 0; i < PlaneWorld.TickRate * 20 && !seal.Open; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Beam = true, Aim = aim });
        }
        return seal.Open;
    }

    [Fact]
    public void A_coloured_seal_opens_only_to_her_beam_through_the_stones_of_its_colour()
    {
        var w = Worlds(w => w.Seals.Any(s => s.Hue != LightHue.None)).First();
        var seal = w.Seals.First(s => s.Hue != LightHue.None);
        int index = w.Seals.IndexOf(seal);
        var stones = w.PrismStones.Where(s => s.Seal == index).ToList();
        var her = stones[0].Bed;
        var right = stones.Where(s => (s.Hue & seal.Hue) != 0).ToList();
        // Her own light, the stones off its way: nothing.
        Assert.False(Shine(w, seal, her));
        // Through a stone of another colour as well: the wrong colour, nothing.
        var wrong = new PrismStone { Hue = (LightHue)(7 & ~(int)seal.Hue), Bed = her };
        if (wrong.Hue != LightHue.None)
        {
            for (int i = 0; i < right.Count; i++) right[i].Position = Vector2.Lerp(her, seal.Emblem, 0.3f + 0.15f * i);
            wrong.Position = Vector2.Lerp(her, seal.Emblem, 0.75f);
            w.PrismStones.Add(wrong);
            Assert.False(Shine(w, seal, her));
            w.PrismStones.Remove(wrong);
        }
        // Through its own colour's stones: it opens.
        for (int i = 0; i < right.Count; i++) right[i].Position = Vector2.Lerp(her, seal.Emblem, 0.3f + 0.15f * i);
        Assert.Equal(seal.Hue, w.HueAlong(her, seal.Emblem));
        Assert.True(Shine(w, seal, her));
    }

    [Fact]
    public void She_pushes_a_stone_slowly_and_it_never_leaves_its_bed()
    {
        var w = Worlds(w => w.PrismStones.Count > 0).First();
        var stone = w.PrismStones[0];
        var start = stone.Position;
        var way = Vector2.Normalize(stone.Bed - stone.Position + new Vector2(0.01f, 0f));
        w.Player.Position = w.Player.PrevPosition = stone.Position - way * (PrismStoneTuning.Radius + 1.2f);
        for (int i = 0; i < PlaneWorld.TickRate * 6; i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Move = way });
            Assert.True(Vector2.Distance(stone.Position, stone.Bed) <= PrismStoneTuning.Bed);
            Assert.True(Vector2.Distance(w.Player.Position, stone.Position) > PrismStoneTuning.Radius + w.Radius - 0.2f, "she does not swim through it");
        }
        Assert.True(Vector2.Distance(stone.Position, start) > 1f, "it moved");
        Assert.True(Vector2.Distance(stone.Position, start) < PrismStoneTuning.PushSpeed * 6f + 0.1f, "slowly");
    }

    [Fact]
    public void A_trio_of_charge_stones_glowing_at_once_opens_its_cache_of_shells_once()
    {
        var w = Worlds(w => w.Trio is not null).First();
        var three = w.ChargeStones.Where(c => c.Trio >= 0).ToList();
        Assert.Equal(3, three.Count);
        Assert.Single(three.Select(c => c.Colour).Distinct());
        Assert.All(three, c => Assert.True(Vector2.Distance(c.Position, w.Trio!.Middle) <= ChargeStoneTuning.TrioReach));
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        // Two of them lit: nothing.
        three[0].Charge = three[1].Charge = ChargeStoneTuning.MaxGlow;
        w.Step(new PlaneInput());
        Assert.False(w.Trio!.Opened);
        int shells = w.Shells.Count;
        three[2].Charge = ChargeStoneTuning.MaxGlow;
        w.Step(new PlaneInput());
        Assert.True(w.Trio.Opened);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.TrioOpened);
        Assert.Equal(shells + ChargeStoneTuning.TrioShells, w.Shells.Count);
        // Once only.
        foreach (var c in three) c.Charge = ChargeStoneTuning.MaxGlow;
        w.Step(new PlaneInput());
        Assert.Equal(shells + ChargeStoneTuning.TrioShells, w.Shells.Count);
    }
}
