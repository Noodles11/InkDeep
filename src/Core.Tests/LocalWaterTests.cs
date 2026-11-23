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

/// <summary>Phase 4 of the lit world: sun-stone and shadow-stone, haloclines, and local water (brine, silt, vents, plankton).</summary>
public class LocalWaterTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(Biome biome)
    {
        var (depth, branch) = biome switch
        {
            Biome.KelpForest => (2, 0), Biome.Shipwreck => (2, 1), Biome.SunkenRuins => (3, 0),
            Biome.AlienWreckage => (4, 0), Biome.DrownedTemple => (4, 1), Biome.Atlantis => (5, 0), _ => (1, 0),
        };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, depth + LevelPlan.Tutorial, 2) with { Branch = branch }, crossover: true);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Pockets = true });
        w.Murklings.Clear();
        return w;
    }

    static (Vector2 Her, Vector2 Aim) OpenWay(PlaneWorld w)
    {
        var her = w.Map.Start.Position;
        for (int k = 0; k < 16; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
            if (w.LineReach(her, d, 10f, out _) >= 9.5f) return (her, d);
        }
        throw new Xunit.Sdk.XunitException("no open way");
    }

    static void Hold(PlaneWorld w, Vector2 her, PlaneInput input, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(input);
        }
    }

    /// <summary>A stone of its kind standing across her open way, 4 m off (made for the test).</summary>
    static OpticWall Stone(PlaneWorld w, OpticKind kind, Vector2 her, Vector2 aim)
    {
        w.OpticWalls.Clear();
        var s = new OpticWall
        {
            Kind = kind, Centre = her + aim * 4f, Angle = MathF.Atan2(aim.X, -aim.Y), HalfLength = 1.5f, HalfThick = OpticWallTuning.StoneThick,
            Solid = kind == OpticKind.SunStone ? 0f : 1f,
        };
        w.OpticWalls.Add(s);
        return s;
    }

    [Fact]
    public void The_biomes_have_their_stones_haloclines_and_pockets()
    {
        var ruins = World(Biome.SunkenRuins);
        Assert.Contains(ruins.OpticWalls, o => o.Kind == OpticKind.ShadowStone);
        Assert.Contains(ruins.Pockets, p => p.Kind == PocketKind.Silt);
        var temple = World(Biome.DrownedTemple);
        Assert.Contains(temple.OpticWalls, o => o.Kind == OpticKind.Halocline);
        Assert.Contains(temple.Pockets, p => p.Kind == PocketKind.Brine);
        Assert.Contains(World(Biome.SunlitReef).Pockets, p => p.Kind == PocketKind.Plankton);
        Assert.Contains(World(Biome.AlienWreckage).Pockets, p => p.Kind == PocketKind.Vent);
    }

    [Fact]
    public void Shadow_stone_stands_in_the_dark_and_fades_while_lit()
    {
        var w = World(Biome.SunkenRuins);
        var (her, aim) = OpenWay(w);
        var s = Stone(w, OpticKind.ShadowStone, her, aim);
        Hold(w, her, default, 10);
        Assert.False(w.Clear(s.Centre, 0.3f), "shadow-stone gone in the dark");
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 30);
        Assert.True(w.Clear(s.Centre, 0.3f), "shadow-stone stands in her light");
        Hold(w, her, default, (int)(3f / PlaneWorld.Dt));
        Assert.False(w.Clear(s.Centre, 0.3f), "shadow-stone never stood again");
    }

    [Fact]
    public void Sun_stone_stands_only_while_lit()
    {
        var w = World(Biome.SunkenRuins);
        var (her, aim) = OpenWay(w);
        var s = Stone(w, OpticKind.SunStone, her, aim);
        Hold(w, her, default, 10);
        Assert.True(w.Clear(s.Centre, 0.3f), "sun-stone stands in the dark");
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 30);
        Assert.False(w.Clear(s.Centre, 0.3f), "sun-stone not there in her light");
        Hold(w, her, default, (int)(3f / PlaneWorld.Dt));
        Assert.True(w.Clear(s.Centre, 0.3f), "sun-stone stayed");
    }

    [Fact]
    public void Her_beam_bends_crossing_a_halocline_and_is_thrown_back_from_its_dense_side_at_a_grazing_angle()
    {
        var w = World(Biome.DrownedTemple);
        var (her, aim) = OpenWay(w);
        w.OpticWalls.Clear();
        // A halocline 4 m on, turned 40° off square to her aim, its dense side away from her.
        float turn = MathF.Atan2(aim.Y, aim.X) + MathF.PI / 2f + 0.7f;
        var halo = new OpticWall { Kind = OpticKind.Halocline, Centre = her + aim * 4f, Angle = turn, HalfLength = 4f, HalfThick = OpticWallTuning.HaloThick };
        if (Vector2.Dot(halo.Normal, aim) < 0f) halo.Angle += MathF.PI;
        w.OpticWalls.Add(halo);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 3);
        var bent = w.Born.FirstOrDefault(b => b.Word == PathWord.Refract);
        Assert.NotNull(bent);
        float off = MathF.Acos(Math.Clamp(Vector2.Dot(bent!.Emitter.Direction, aim), -1f, 1f));
        Assert.True(off > 0.03f, $"bent only {off} rad");
        Assert.True(w.Clear(halo.Centre, 0.3f), "a halocline stops her body");

        // From the dense side at a grazing angle: thrown back.
        halo.Angle = MathF.Atan2(aim.Y, aim.X) + 0.12f;
        if (Vector2.Dot(halo.Normal, aim) > 0f) halo.Angle += MathF.PI;
        halo.Centre = her + aim * 3f + new Vector2(-aim.Y, aim.X) * 0.3f;
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 3);
        Assert.Contains(w.Born, b => b.Word == PathWord.Mirror);
    }

    [Fact]
    public void Her_dash_kicks_up_silt_that_hides_a_light_and_then_settles()
    {
        var w = World(Biome.Shipwreck);
        var (her, aim) = OpenWay(w);
        w.Pockets.Clear();
        w.Player.Position = w.Player.PrevPosition = her;
        for (int i = 0; i < 20 && w.Pockets.Count == 0; i++) w.Step(new PlaneInput { Move = aim, Dash = i == 0 });
        var silt = Assert.Single(w.Pockets);
        Assert.Equal(PocketKind.Silt, silt.Kind);
        var c = silt.Centre;
        var side = new Vector2(-aim.Y, aim.X);
        Assert.False(w.LineOfSight(c - side * 5f, c + side * 5f), "silt hides nothing");
        Hold(w, w.Player.Position, default, (int)((PocketTuning.SiltLife + 1f) / PlaneWorld.Dt));
        Assert.Empty(w.Pockets);
    }

    [Fact]
    public void A_brine_pool_hides_what_lies_across_it()
    {
        var w = World(Biome.DrownedTemple);
        var (her, aim) = OpenWay(w);
        w.Pockets.Clear();
        Assert.True(w.LineOfSight(her, her + aim * 9f));
        w.AddPocket(PocketKind.Brine, her + aim * 4.5f, 2.5f);
        Assert.False(w.LineOfSight(her, her + aim * 9f));
    }

    [Fact]
    public void A_plankton_bloom_glows_in_her_light_a_light_of_its_own()
    {
        var w = World(Biome.SunlitReef);
        var (her, aim) = OpenWay(w);
        w.Pockets.Clear();
        var bloom = w.AddPocket(PocketKind.Plankton, her + aim * 4f, 2.5f);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 30);
        Assert.True(bloom.Glow > 0.5f && bloom.Light!.Shining, $"glow {bloom.Glow}");
        Hold(w, her, default, (int)(5f / PlaneWorld.Dt));
        Assert.True(bloom.Glow < 0.1f, $"still {bloom.Glow}");
    }

    [Fact]
    public void Vent_shimmer_wobbles_her_shots_off_their_way()
    {
        var w = World(Biome.AlienWreckage);
        var (her, aim) = OpenWay(w);
        w.Pockets.Clear();
        w.OpticWalls.Clear();
        w.AddPocket(PocketKind.Vent, her + aim * 3.5f, 3f);
        float most = 0f;
        for (int i = 0; i < 40; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Step(new PlaneInput { Fire = i % 12 == 0, Aim = aim });
            foreach (var s in w.Shots.Where(s => s.FromPlayer && s.Velocity.LengthSquared() > 0.01f))
                most = MathF.Max(most, MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(s.Velocity), aim), -1f, 1f)));
        }
        Assert.True(most > 0.02f, $"wobbled only {most} rad");
    }
}
