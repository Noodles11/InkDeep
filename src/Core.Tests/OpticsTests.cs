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

/// <summary>Her light as physics (PlaneOptics.cs, docs/proposals/LIGHT-WORLD.md): spectrum, water, rock.</summary>
public class OpticsTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(Biome biome, bool optics = true)
    {
        var (depth, branch) = biome switch
        {
            Biome.KelpForest => (2, 0), Biome.Shipwreck => (2, 1), Biome.SunkenRuins => (3, 0),
            Biome.AlienWreckage => (4, 0), Biome.DrownedTemple => (4, 1), Biome.Atlantis => (5, 0), _ => (1, 0),
        };
        var id = new LevelId(1, depth + LevelPlan.Tutorial, 2) with { Branch = branch };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), id, crossover: true);
        Assert.Equal(biome, map.Biome);
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = optics });
        w.Murklings.Clear();
        return w;
    }

    [Fact]
    public void A_spectrum_has_a_wavelength_a_frequency_and_a_photon_energy()
    {
        var red = new Spectrum(1f, 0f, 0f);
        var blue = new Spectrum(0f, 0f, 1f);
        Assert.Equal(Spectrum.RedNm, red.Wavelength, 1);
        Assert.Equal(468.4f, red.Frequency, 0);
        Assert.True(blue.PhotonEnergy > Spectrum.Hers.PhotonEnergy && Spectrum.Hers.PhotonEnergy > red.PhotonEnergy);
        Assert.True(Optics.Potency(blue) > 1.1f && Optics.Potency(red) < 0.9f);
    }

    [Fact]
    public void Her_colour_words_shift_her_light_along_the_spectrum_and_keep_its_power()
    {
        var plain = PlaneWorld.SpectrumOf(0, 0, 0);
        var ember = PlaneWorld.SpectrumOf(2, 0, 0);
        var frost = PlaneWorld.SpectrumOf(0, 0, 2);
        Assert.Equal(plain.Total, ember.Total, 3);
        Assert.Equal(plain.Total, frost.Total, 3);
        Assert.True(ember.Wavelength > plain.Wavelength && plain.Wavelength > frost.Wavelength);
    }

    [Fact]
    public void Each_water_drinks_its_own_colours()
    {
        float Reach(Spectrum s, Biome b, float coherence = OpticsTuning.Coherence) => Optics.ReachScale(s, BiomeOptics.Water(b), coherence);
        var red = PlaneWorld.SpectrumOf(3, 0, 0);
        var green = PlaneWorld.SpectrumOf(0, 3, 0);
        var blue = PlaneWorld.SpectrumOf(0, 0, 3);
        Assert.Equal(1f, Reach(Spectrum.Hers, Biome.SunlitReef), 2);
        // The Alien Wreckage's cold clear water carries blue, drinks red; the Ruins' yellow dust the other way round.
        Assert.True(Reach(blue, Biome.AlienWreckage) > Reach(red, Biome.AlienWreckage) * 1.3f);
        Assert.True(Reach(red, Biome.SunkenRuins) > Reach(blue, Biome.SunkenRuins) * 1.3f);
        // The kelp's green water carries green.
        Assert.True(Reach(green, Biome.KelpForest) > Reach(red, Biome.KelpForest));
        // The wreck's silt shortens everything, a coherent light (the Lance) least.
        Assert.True(Reach(Spectrum.Hers, Biome.Shipwreck) < 0.8f);
        Assert.True(Reach(Spectrum.Hers, Biome.Shipwreck, OpticsTuning.LanceCoherence) > Reach(Spectrum.Hers, Biome.Shipwreck) * 1.15f);
        Assert.True(Optics.Halo(BiomeOptics.Water(Biome.Shipwreck), OpticsTuning.Coherence) > 2.5f);
    }

    [Fact]
    public void Light_bends_by_snells_law_and_cannot_leave_a_dense_material_at_a_grazing_angle()
    {
        var face = new Vector2(0f, 1f);
        var d = Vector2.Normalize(new Vector2(1f, -1f));
        var bent = Optics.Refract(d, face, 1.333f, 1.9f)!.Value;
        float sin1 = MathF.Abs(d.X), sin2 = MathF.Abs(bent.X);
        Assert.Equal(1.333f * sin1, 1.9f * sin2, 3);
        Assert.True(bent.Y < 0f);
        var grazing = Vector2.Normalize(new Vector2(1f, -0.2f));
        Assert.Null(Optics.Refract(grazing, face, 1.9f, 1.333f));
        Assert.Equal(new Vector2(1f, 1f), Optics.Reflect(new Vector2(1f, -1f), face));
    }

    [Fact]
    public void A_wall_shares_the_light_by_its_material()
    {
        foreach (var b in Enum.GetValues<Biome>())
        {
            var m = Optics.Meet(Spectrum.Hers, BiomeOptics.Rock(b), 0.7f);
            Assert.Equal(1f, m.Mirrored + m.Scattered + m.Entered + m.Drunk, 3);
        }
        var iron = Optics.Meet(Spectrum.Hers, BiomeOptics.Rock(Biome.Shipwreck), 0.7f);
        var coral = Optics.Meet(Spectrum.Hers, BiomeOptics.Rock(Biome.SunlitReef), 0.7f);
        var crystal = Optics.Meet(Spectrum.Hers, BiomeOptics.Rock(Biome.AlienWreckage), 0.7f);
        Assert.True(iron.Mirrored > coral.Mirrored * 3f);
        Assert.True(crystal.Entered > 0.3f && coral.Entered == 0f);
        // Grazing, more is a mirror.
        Assert.True(Optics.Meet(Spectrum.Hers, BiomeOptics.Rock(Biome.SunlitReef), 0.05f).Mirrored > coral.Mirrored);
    }

    [Fact]
    public void Without_optics_her_light_is_as_it_was()
    {
        var w = World(Biome.Shipwreck, optics: false);
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.Equal(LightPhysics.Neutral, w.HerLightPhysics);
        Assert.Same(BiomeOptics.Reference, w.Water);
    }

    [Fact]
    public void Her_beam_is_shorter_in_the_wreck_s_silt_than_in_the_clear_reef()
    {
        float Beam(Biome b)
        {
            var w = World(b);
            w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
            return w.Beam.Range;
        }
        Assert.True(Beam(Biome.Shipwreck) < Beam(Biome.SunlitReef) * 0.85f);
    }

    /// <summary>Fires her gun at the rock all round her start, and counts what her shots did there.</summary>
    static (int Bounced, int Refracted, int Glows) Volleys(PlaneWorld w)
    {
        int bounced = 0, refracted = 0, glows = 0;
        // (Four metres off the nearest wall from her start: the chambers are wide.)
        var start = w.Map.Start.Position;
        var at = start;
        float nearest = float.MaxValue;
        for (int k = 0; k < 32; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 32f), MathF.Sin(k * MathF.Tau / 32f));
            float reach = w.LineReach(start, d, 40f, out bool hit);
            if (hit && reach > 5f && reach < nearest) (nearest, at) = (reach, start + d * (reach - 4f));
        }
        for (int k = 0; k < 24; k++)
        {
            var aim = new Vector2(MathF.Cos(k * MathF.Tau / 24f), MathF.Sin(k * MathF.Tau / 24f));
            for (int i = 0; i < 70; i++)
            {
                w.Player.Position = w.Player.PrevPosition = at;
                w.Player.Velocity = Vector2.Zero;
                w.Player.Hp = w.Run.MaxHp;
                w.Step(new PlaneInput { Fire = i % 10 == 0, Aim = aim });
                bounced += w.Events.Count(e => e.Type == PlaneEventType.ShotBounced);
                refracted += w.Events.Count(e => e.Type == PlaneEventType.ShotRefracted);
                glows = Math.Max(glows, w.WallGlows.Count);
            }
        }
        return (bounced, refracted, glows);
    }

    [Fact]
    public void Her_gun_rings_off_the_wreck_s_iron_and_the_ruins_walls_keep_its_light()
    {
        var wreck = Volleys(World(Biome.Shipwreck));
        var ruins = Volleys(World(Biome.SunkenRuins));
        Assert.True(wreck.Bounced > ruins.Bounced && wreck.Bounced >= 5, $"wreck {wreck.Bounced} bounces, ruins {ruins.Bounced}");
        Assert.True(ruins.Glows > 0, "no wall glowed in the Ruins");
        Assert.Equal(0, wreck.Glows);
    }

    [Fact]
    public void Her_light_set_by_hand_takes_its_colour_power_width_and_pulse_in_any_level()
    {
        var w = World(Biome.SunlitReef, optics: false);
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        float plainHalf = w.Beam.HalfAngle;
        w.ManualLight = new LightSettings { Spectrum = new Spectrum(0.1f, 0.3f, 2f), Power = 2f, Width = 2f, Coherence = 0.5f };
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        var l = w.HerLightPhysics;
        Assert.True(l.Wavelength < 490f, $"a blue light at {l.Wavelength} nm");
        Assert.Equal(Spectrum.Hers.Total, l.Spectrum.Total, 3);
        Assert.True(l.Punch > 1.9f, $"punch {l.Punch}");
        Assert.True(w.Beam.HalfAngle > plainHalf * 1.9f, $"half-angle {w.Beam.HalfAngle} against {plainHalf}");
        // Pulsed, her light swells and ebbs.
        w.ManualLight = new LightSettings { PulseHz = 2f };
        float least = float.MaxValue, most = 0f;
        for (int i = 0; i < 60; i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
            least = MathF.Min(least, w.HerLightPhysics.Pulse);
            most = MathF.Max(most, w.HerLightPhysics.Pulse);
        }
        Assert.True(most > 1.4f && least < 0.6f, $"pulse {least}..{most}");
        // Given back to her pearls, it is as it was.
        w.ManualLight = null;
        w.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.Equal(LightPhysics.Neutral, w.HerLightPhysics);
        Assert.Equal(plainHalf, w.Beam.HalfAngle, 4);
    }

    /// <summary>Her first shot thrown along the open way from her start.</summary>
    static PlaneShot Throw(PlaneWorld w)
    {
        var at = w.Map.Start.Position;
        Vector2 aim = Vector2.UnitX;
        float far = 0f;
        for (int k = 0; k < 16; k++)
        {
            var d = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
            float r = w.LineReach(at, d, 30f, out _);
            if (r > far) (far, aim) = (r, d);
        }
        for (int i = 0; i < 30 && !w.Shots.Any(s => s.FromPlayer); i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Step(new PlaneInput { Fire = true, Aim = aim });
        }
        return w.Shots.First(s => s.FromPlayer);
    }

    [Fact]
    public void Her_shots_take_her_light_as_her_beam_has_it_its_colour_power_width_and_reach()
    {
        var plain = Throw(World(Biome.SunlitReef));
        var w = World(Biome.SunlitReef);
        w.ManualLight = new LightSettings { Power = 2f, Width = 1.8f };
        var hand = Throw(w);
        Assert.True(hand.Damage > plain.Damage * 1.8f, $"damage {hand.Damage} against {plain.Damage}");
        Assert.True(hand.Radius > plain.Radius * 1.7f, $"size {hand.Radius} against {plain.Radius}");
        // Her colour words: her shots carry them, as her beam does.
        var map = World(Biome.SunlitReef).Map;
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("frost_kelp");
        var cold = new PlaneWorld(map, new Tuning(), run, new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true });
        cold.Murklings.Clear();
        Assert.Equal(LightHue.Blue, Throw(cold).Light);
        Assert.Equal(LightHue.None, plain.Light);
    }
}
