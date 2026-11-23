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

/// <summary>Plants that answer light, fluorescence and light rooms (PlaneFlora.cs, LIGHT-WORLD.md phase 3).</summary>
public class FloraTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(Biome biome = Biome.SunlitReef)
    {
        var (depth, branch) = biome switch
        {
            Biome.KelpForest => (2, 0), Biome.Shipwreck => (2, 1), Biome.SunkenRuins => (3, 0),
            Biome.AlienWreckage => (4, 0), Biome.DrownedTemple => (4, 1), Biome.Atlantis => (5, 0), _ => (1, 0),
        };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, depth + LevelPlan.Tutorial, 2) with { Branch = branch }, crossover: true);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Flora = true });
        w.Murklings.Clear();
        return w;
    }

    /// <summary>A spot in the open, a plant 3.5 m along the open way from it, and the way to it.</summary>
    static (Vector2 Her, Vector2 At, Vector2 Aim) Bed(PlaneWorld w)
    {
        var her = w.Map.Start.Position;
        for (int k = 0; k < 16; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
            if (w.LineReach(her, d, 8f, out _) >= 7.5f) return (her, her + d * 3.5f, d);
        }
        throw new Xunit.Sdk.XunitException("no open way");
    }

    static void Hold(PlaneWorld w, Vector2 her, PlaneInput input, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(input);
        }
    }

    [Fact]
    public void A_level_has_light_rooms_of_its_biome_s_plants_and_loose_plants()
    {
        foreach (var biome in new[] { Biome.SunlitReef, Biome.KelpForest, Biome.AlienWreckage })
        {
            var w = World(biome);
            var (kind, count, _) = FloraTuning.For(biome);
            Assert.NotEmpty(w.LightRooms);
            Assert.All(w.LightRooms, r => Assert.True(r.Plants.Count == count && r.Plants.All(p => p.Kind == kind)));
            Assert.Contains(w.Flora, f => f.Room < 0);
        }
    }

    [Fact]
    public void A_sun_anemone_opens_in_her_light_gives_what_it_hid_and_closes_after()
    {
        var w = World();
        w.Flora.Clear();
        w.LightRooms.Clear();
        var (her, at, aim) = Bed(w);
        var a = w.AddFlora(FloraKind.SunAnemone, at);
        int shells = w.Shells.Count;
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 60);
        Assert.True(a.Open >= 0.9f, $"open {a.Open}");
        Assert.True(w.Shells.Count > shells, "it hid nothing");
        Hold(w, her, default, (int)((FloraTuning.SunHold + 4f) / PlaneWorld.Dt));
        Assert.True(a.Open < 0.1f, $"still open {a.Open}");
    }

    [Fact]
    public void A_shy_anemone_shuts_in_her_light_and_opens_in_the_dark()
    {
        var w = World();
        w.Flora.Clear();
        w.LightRooms.Clear();
        var (her, at, aim) = Bed(w);
        var a = w.AddFlora(FloraKind.ShyAnemone, at);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 30);
        Assert.True(a.Open < 0.1f, $"open {a.Open}");
        Hold(w, her, default, (int)(4f / PlaneWorld.Dt));
        Assert.True(a.Open > 0.9f, $"open {a.Open}");
    }

    [Fact]
    public void A_kelp_grows_in_held_light_faster_in_green_withers_in_the_dark_and_hides_what_lies_behind()
    {
        float Grown(params string[] pearls)
        {
            var w = World(Biome.KelpForest);
            foreach (var p in pearls) w.Run.Add(p);
            w.Flora.Clear();
            w.LightRooms.Clear();
            var (her, at, aim) = Bed(w);
            var k = w.AddFlora(FloraKind.GiantKelp, at);
            k.Way = new Vector2(-aim.Y, aim.X);
            Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 90);
            return k.Length;
        }
        float plain = Grown(), green = Grown("glowing_moss", "glowing_moss");
        Assert.True(plain > 0.5f, $"plain grew {plain}");
        Assert.True(green > plain * 1.2f, $"green {green}, plain {plain}");

        var w = World(Biome.KelpForest);
        w.Flora.Clear();
        w.LightRooms.Clear();
        var (her, at, aim) = Bed(w);
        var kelp = w.AddFlora(FloraKind.GiantKelp, at - new Vector2(-aim.Y, aim.X) * 1.5f);
        kelp.Way = new Vector2(-aim.Y, aim.X);
        kelp.Length = 3f;
        // (Stood across her line of sight: what lies past it is hidden.)
        Assert.False(w.LineOfSight(her, at + aim * 2f));
        Hold(w, her, default, (int)(25f / PlaneWorld.Dt));
        Assert.True(kelp.Length < 0.5f, $"still {kelp.Length} m");
    }

    [Fact]
    public void A_sunflower_sponge_stores_held_light_and_then_glows_a_light_of_its_own()
    {
        var w = World(Biome.SunkenRuins);
        w.Flora.Clear();
        w.LightRooms.Clear();
        var (her, at, aim) = Bed(w);
        var s = w.AddFlora(FloraKind.SunflowerSponge, at);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, (int)(6f / PlaneWorld.Dt));
        Assert.True(s.Glow > 0f, "it never filled");
        Hold(w, her, default, 30);
        Assert.True(s.Light!.Shining && w.LightAt(at + aim * 1f) > 0.2f, "it gives no light");
    }

    [Fact]
    public void A_night_lily_opens_only_in_the_dark()
    {
        var w = World(Biome.DrownedTemple);
        w.Flora.Clear();
        w.LightRooms.Clear();
        var (her, at, aim) = Bed(w);
        var l = w.AddFlora(FloraKind.NightLily, at);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, (int)(6f / PlaneWorld.Dt));
        Assert.True(l.Open < 0.1f, $"open in light {l.Open}");
        Hold(w, her, default, (int)(6f / PlaneWorld.Dt));
        Assert.True(l.Open > 0.9f, $"shut in the dark {l.Open}");
    }

    [Fact]
    public void A_fluorescent_coral_glows_green_from_blue_light_more_than_from_red()
    {
        float Glow(Spectrum light)
        {
            var w = World(Biome.AlienWreckage);
            w.Flora.Clear();
            w.LightRooms.Clear();
            w.ManualLight = new LightSettings { Spectrum = light };
            var (her, at, aim) = Bed(w);
            var c = w.AddFlora(FloraKind.FluorCoral, at);
            Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 60);
            return c.Glow;
        }
        float blue = Glow(new Spectrum(0.1f, 0.2f, 2f)), red = Glow(new Spectrum(2f, 0.2f, 0.1f));
        Assert.True(blue > 0.5f, $"blue {blue}");
        Assert.True(red < blue * 0.5f, $"red {red}, blue {blue}");
    }

    [Fact]
    public void A_light_room_opens_once_all_its_plants_are_awake_at_once()
    {
        var w = World();
        var room = w.LightRooms[0];
        int shells = w.Shells.Count;
        // (All lit at once: each held open.)
        for (int i = 0; i < 40 && !room.Opened; i++)
        {
            foreach (var p in room.Plants) p.Hold = FloraTuning.SunHold;
            w.Step(default);
        }
        Assert.True(room.Opened);
        Assert.True(w.Shells.Count >= shells + FloraTuning.RoomShells);
    }
}
