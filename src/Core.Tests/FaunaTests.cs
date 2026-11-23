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

/// <summary>Phase 5 of the lit world: the reef's animals that answer light, the murklings' corrupted traits, the light pearls.</summary>
public class FaunaTests
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
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Fauna = true });
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

    static void Hold(PlaneWorld w, Vector2 her, PlaneInput input, int steps, Action? each = null)
    {
        for (int i = 0; i < steps; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = w.Run.MaxHp;
            each?.Invoke();
            w.Step(input);
        }
    }

    static PlaneWorld Bare(Biome biome, out Vector2 her, out Vector2 aim)
    {
        var w = World(biome);
        w.Fauna.Clear();
        (her, aim) = OpenWay(w);
        return w;
    }

    static Murkling Murk(PlaneWorld w, Vector2 at, MurkTrait trait)
    {
        var m = new Murkling { Position = at, Bud = 0f, Trait = trait, Seed = 1f };
        w.Murklings.Add(m);
        return m;
    }

    [Fact]
    public void The_biomes_have_their_animals()
    {
        var reef = World(Biome.SunlitReef);
        Assert.Contains(reef.Fauna, f => f.Kind == FaunaKind.GlassShrimp);
        Assert.Contains(reef.Fauna, f => f.Kind == FaunaKind.MantisShrimp);
        var kelp = World(Biome.KelpForest);
        Assert.Contains(kelp.Fauna, f => f.Kind == FaunaKind.LanternShoal);
        Assert.Contains(kelp.Fauna, f => f.Kind == FaunaKind.FireflySquid);
        Assert.Contains(World(Biome.SunkenRuins).Fauna, f => f.Kind == FaunaKind.Cuttlefish);
    }

    [Fact]
    public void Glass_shrimp_flee_her_light_and_gather_home_in_the_dark()
    {
        var w = Bare(Biome.SunlitReef, out var her, out var aim);
        var home = her + aim * 4f;
        var s = w.AddFauna(FaunaKind.GlassShrimp, home);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 40);
        float fled = Vector2.Distance(s.Position, home);
        Assert.True(fled > 0.8f, $"fled only {fled} m");
        Hold(w, her, default, (int)(15f / PlaneWorld.Dt));
        Assert.True(Vector2.Distance(s.Position, home) < fled, "never came home");
    }

    [Fact]
    public void A_cuttlefish_takes_the_colour_of_the_light_on_it_and_glows_it_back()
    {
        var w = Bare(Biome.SunlitReef, out var her, out var aim);
        var c = w.AddFauna(FaunaKind.Cuttlefish, her + aim * 3.5f);
        Hold(w, her, default, 10);
        Assert.False(c.Light!.Active, "glows in the dark");
        w.ManualLight = new LightSettings { Spectrum = new Spectrum(0.1f, 0.2f, 2f) };
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 60);
        Assert.True(c.Glow > 0.8f && c.Light.Active, $"glow {c.Glow}");
        Assert.True(c.Colour.Z > c.Colour.X, $"not blue: {c.Colour}");
    }

    [Fact]
    public void A_lit_mantis_shrimp_strikes_her_up_close_unless_her_light_is_polarised()
    {
        var w = Bare(Biome.SunlitReef, out var her, out var aim);
        var m = w.AddFauna(FaunaKind.MantisShrimp, her + aim * 1.8f);
        bool struck = false;
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 60, () => struck |= w.Events.Any(e => e.Type == PlaneEventType.MantisStruck));
        Assert.True(struck, $"out {m.Out}, her light on it {m.HerLit}");

        var p = Bare(Biome.SunlitReef, out her, out aim);
        p.Run.Loadout.Flags.Add("polariser");
        p.AddFauna(FaunaKind.MantisShrimp, her + aim * 1.8f);
        struck = false;
        Hold(p, her, new PlaneInput { Beam = true, Aim = aim }, 60, () => struck |= p.Events.Any(e => e.Type == PlaneEventType.MantisStruck));
        Assert.False(struck);
    }

    [Fact]
    public void A_lantern_shoal_follows_a_steady_light_and_scatters_from_a_fast_pulse()
    {
        var w = Bare(Biome.KelpForest, out var her, out var aim);
        var s = w.AddFauna(FaunaKind.LanternShoal, her + aim * 4f);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 20);
        Assert.True(s.Follow > 0f, $"her light on it {s.HerLit}");

        var q = Bare(Biome.KelpForest, out her, out aim);
        q.ManualLight = new LightSettings { PulseHz = 5f };
        var t = q.AddFauna(FaunaKind.LanternShoal, her + aim * 4f);
        Hold(q, her, new PlaneInput { Beam = true, Aim = aim }, 20);
        Assert.True(t.Scatter > 0f && t.Follow <= 0f);
    }

    [Fact]
    public void A_firefly_squid_leads_her_when_her_light_pulses_at_its_beat()
    {
        var w = Bare(Biome.KelpForest, out var her, out var aim);
        w.LightRooms.Clear();
        w.LightRooms.Add(new LightRoom { Centre = her + aim * 9f });
        var sq = w.AddFauna(FaunaKind.FireflySquid, her + aim * 3f);
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 30);
        Assert.Equal(0f, sq.Lead);
        w.ManualLight = new LightSettings { PulseHz = FaunaTuning.FlashHz };
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 30);
        Assert.True(sq.Lead > 0f, "never led");
    }

    [Fact]
    public void A_shot_glances_off_a_chitin_shell_and_bends_past_a_refractors_middle()
    {
        float Lost(MurkTrait trait)
        {
            var w = Bare(Biome.Shipwreck, out var her, out var aim);
            w.OpticWalls.Clear();
            var at = her + aim * 3.5f;
            var m = Murk(w, at, trait);
            float full = m.Corruption.Value;
            for (int i = 0; i < 30; i++)
            {
                w.Player.Position = w.Player.PrevPosition = her;
                m.Position = at;
                m.Velocity = Vector2.Zero;
                m.Shell = MathF.Atan2(-aim.Y, -aim.X);
                w.Step(new PlaneInput { Fire = i == 0, Aim = aim });
            }
            return full - m.Corruption.Value;
        }
        // (Its light still sears a little as it glances by: never the hit.)
        float plain = Lost(MurkTrait.None);
        Assert.True(Lost(MurkTrait.ChitinMirror) < plain * 0.2f, $"chitin {Lost(MurkTrait.ChitinMirror)} of {plain}");
        Assert.True(Lost(MurkTrait.Refractor) < plain * 0.2f, $"refractor {Lost(MurkTrait.Refractor)} of {plain}");
    }

    static float BeamBurnt(MurkTrait trait, LightHue ward, Spectrum light, float metres = 3f, float power = 1f, float width = 1f, float startShare = 1f)
    {
        var w = Bare(Biome.AlienWreckage, out var her, out var aim);
        w.ManualLight = new LightSettings { Spectrum = light, Power = power, Width = width };
        var at = her + aim * metres;
        var m = Murk(w, at, trait);
        m.Ward = ward;
        m.Corruption.Value = m.Corruption.Max * startShare;
        float before = m.Corruption.Value;
        Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 15, () =>
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
        });
        return before - m.Corruption.Value;
    }

    [Fact]
    public void A_band_ward_shrugs_off_its_own_band()
    {
        var red = new Spectrum(2f, 0.2f, 0.1f);
        float own = BeamBurnt(MurkTrait.BandWard, LightHue.Red, red), other = BeamBurnt(MurkTrait.BandWard, LightHue.Blue, red);
        Assert.True(other > own * 2f, $"red on a red ward {own}, on a blue ward {other}");
    }

    [Fact]
    public void A_light_eater_feeds_on_weak_light_and_burns_in_strong()
    {
        float weak = BeamBurnt(MurkTrait.LightEater, LightHue.Red, Spectrum.Hers, metres: 7f, power: 0.25f, width: 2.5f, startShare: 0.5f);
        Assert.True(weak <= 0f, $"a weak light burnt it {weak}");
        float strong = BeamBurnt(MurkTrait.LightEater, LightHue.Red, Spectrum.Hers, metres: 1.6f, power: 3f, width: 0.3f, startShare: 0.5f);
        Assert.True(strong > 0f, $"a strong light did not burn it {strong}");
    }

    [Fact]
    public void Murklings_cannot_see_her_infrared_light()
    {
        foreach (bool infrared in new[] { false, true })
        {
            var w = Bare(Biome.SunlitReef, out var her, out var aim);
            if (infrared) w.Run.Loadout.Flags.Add("infrared");
            bool sawHer = false;
            Hold(w, her, new PlaneInput { Beam = true, Aim = aim }, 5, () => sawHer |= w.DebugSeekLight(her + aim * 5f)?.Owner == w.Her);
            Assert.Equal(!infrared, sawHer);
        }
    }

    [Fact]
    public void Abyssal_blue_turns_her_light_blue_and_the_lens_of_the_deep_focuses_her_shots()
    {
        var w = Bare(Biome.SunlitReef, out var her, out var aim);
        Hold(w, her, default, 1);
        float plain = w.HerLightPhysics.Spectrum.B / w.HerLightPhysics.Spectrum.Total;
        w.Run.Loadout.Flags.Add("nearUv");
        Hold(w, her, default, 1);
        Assert.True(w.HerLightPhysics.Spectrum.B / w.HerLightPhysics.Spectrum.Total > plain * 1.5f);

        float Flown(bool lens)
        {
            var q = Bare(Biome.SunlitReef, out var h, out var a);
            if (lens) q.Run.Loadout.Flags.Add("deepLens");
            q.OpticWalls.Clear();
            PlaneShot? shot = null;
            for (int i = 0; i < 12; i++)
            {
                q.Player.Position = q.Player.PrevPosition = h;
                q.Step(new PlaneInput { Fire = i == 0, Aim = a });
                shot ??= q.Shots.FirstOrDefault(s => s.FromPlayer);
            }
            return shot!.Damage;
        }
        Assert.True(Flown(true) > Flown(false) * 1.1f);
    }
}
