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

/// <summary>A cave's wall ash (docs/ADVENTURE.md §8; PlaneAsh.cs): her light and her shots clear it.</summary>
public class CaveAshTests
{
    static readonly SeedCode Seed = SeedCode.TryParse("KELP7Q2Z", out var s) ? s : default;

    static readonly Lazy<LevelMap> Cave = new(() =>
        TopDownGenerator.GenerateCave(Seed, CaveId(0), 0f, 2));

    /// <summary>A leak cave of its own (a dive cave's kind of id: docs/DIVE.md §6).</summary>
    static LevelId CaveId(int k) => new(1001, 1, k + 1);

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(LevelMap map) => new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });

    [Fact]
    public void A_caves_walls_carry_ash_and_open_water_carries_none()
    {
        var w = World(Cave.Value);
        Assert.True(w.Ash.Count > 200, $"{w.Ash.Count} patches");
        Assert.All(w.Ash, a =>
        {
            Assert.True(w.Map.IsOpen(a.At));
            Assert.True(w.Map.HeightAt(a.Wall + a.Normal * 0.03f) <= LevelMap.SwimBand + a.Height + 0.05f);
            Assert.InRange(a.Height, AshTuning.Low, AshTuning.High);
            Assert.Equal(1f, a.Cover);
        });
        // Outside caves there is none.
        Assert.Empty(World(TopDownGenerator.Generate(new RunStreams(Seed), LevelId.FirstRun)).Ash);
    }

    [Fact]
    public void Her_light_clears_the_ash_within_its_reach_and_none_beyond()
    {
        var w = World(Cave.Value);
        w.Murklings.Clear();
        // A patch near her plane, and a spot in front of it, in sight of it, to float still at.
        foreach (var a in w.Ash.Where(a => MathF.Abs(a.Height) < 0.6f))
        {
            var her = a.At + a.Normal * 2.5f;
            if (!w.Clear(her, 0.6f) || !w.LineOfSight(her, a.At)) continue;
            for (int i = 0; i < 2 * PlaneWorld.TickRate && a.Cover > 0f; i++) Step(w, her, Vector2.Zero);
            Assert.Equal(0f, a.Cover);
            // What lies beyond her light's reach is untouched.
            float reach = w.LightReach + 0.1f;
            Assert.All(w.Ash.Where(x => Vector2.DistanceSquared(x.Wall, her) + x.Height * x.Height > reach * reach), x => Assert.Equal(1f, x.Cover));
            Assert.Contains(w.Ash, x => x.Cover == 1f);
            return;
        }
        Assert.Fail("no patch to shine on");
    }

    [Fact]
    public void A_shot_popping_by_a_wall_clears_a_little_ash()
    {
        var w = World(Cave.Value);
        var a = w.Ash.First(x => MathF.Abs(x.Height) < 0.5f);
        var at = a.Wall + a.Normal * 0.3f;
        w.Shots.Add(new PlaneShot { Position = at, Line = at, Velocity = Vector2.UnitX * 0.01f, Life = 1f, FromPlayer = true, Damage = 10f, Range = 1f, Hover = 0.05f, Rest = 0.05f });
        w.Step(default);
        Assert.Equal(0f, a.Cover);
        // Only a little: the patches more than its reach away are untouched (but for what her own light reaches round her).
        Assert.All(w.Ash.Where(x => Vector2.Distance(x.Wall, at) > AshTuning.ShotReach + 0.1f && Vector2.Distance(x.Wall, w.Player.Position) > w.LightReach + 1f),
            x => Assert.Equal(1f, x.Cover));
    }

    [Fact]
    public void Sealing_the_caves_leak_dissolves_its_ash_in_a_wave()
    {
        var w = World(Cave.Value);
        var leak = w.Leaks.Single();
        w.DebugSealLeak(leak);
        for (int i = 0; i < 2; i++) w.Step(default);
        var near = w.Ash.Where(x => Vector2.Distance(x.Wall, leak.Position) < 4f).ToList();
        // (Not what her own light has cleared round her start.)
        var far = w.Ash.Where(x => Vector2.Distance(x.Wall, leak.Position) > 40f && Vector2.Distance(x.Wall, w.Player.Position) > w.LightReach + 1f).ToList();
        Assert.All(near, x => Assert.Equal(0f, x.Cover));
        Assert.All(far, x => Assert.True(x.Cover > 0f));
        for (int i = 0; i < 40 * PlaneWorld.TickRate; i++) w.Step(default);
        Assert.All(w.Ash, x => Assert.Equal(0f, x.Cover));
    }

    [Fact]
    public void Ash_lies_thick_over_a_secrets_sealed_wall_and_hides_it_until_cleared()
    {
        // The first of the caves that has a secret behind a sealed wall.
        for (int k = 0; k < 24; k++)
        {
            var map = TopDownGenerator.GenerateCave(Seed, CaveId(k), 0f, 2);
            var w = World(map);
            int seal = w.Seals.FindIndex(x => x.Kind == SealKind.Secret && !x.Open);
            if (seal < 0) continue;
            var over = w.Ash.Where(a => a.Seal == seal).ToList();
            Assert.True(over.Count >= 12, $"{over.Count} patches over it");
            Assert.All(over, a => Assert.True(w.Map.IsOpen(a.At)));
            Assert.Equal(1f, w.SealAsh(seal));
            foreach (var a in over) a.Cover = 0f;
            Assert.Equal(0f, w.SealAsh(seal));
            return;
        }
        Assert.Fail("no cave with a sealed secret");
    }

    static void Step(PlaneWorld w, Vector2 her, Vector2 swim)
    {
        w.Player.Position = w.Player.PrevPosition = her;
        w.Player.Velocity = swim * w.CruiseSpeed;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(default);
    }
}
