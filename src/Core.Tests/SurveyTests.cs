using System;
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

/// <summary>Phase 6 of the lit world: the spectral survey, the keystones, the orrery (Survey.cs, PlaneCodex.cs, PlaneFlora.cs).</summary>
public class SurveyTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(Biome biome, params string[] pearls)
    {
        var (depth, branch) = biome switch { Biome.Atlantis => (5, 0), _ => (1, 0) };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, depth + LevelPlan.Tutorial, 2) with { Branch = branch }, crossover: true);
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (var p in pearls) run.Add(p);
        var w = new PlaneWorld(map, new Tuning(), run,
            new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true, Flora = true, Pockets = true, Fauna = true });
        w.Murklings.Clear();
        return w;
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(input);
    }

    /// <summary>A clear spot near a plant with sight of it, and the way to it.</summary>
    static (Vector2 At, Vector2 Aim) Near(PlaneWorld w, Vector2 thing, float away)
    {
        for (int k = 0; k < 16; k++)
        {
            var way = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
            var at = thing + way * away;
            if (w.Clear(at, w.Radius + 0.2f) && w.LineOfSight(at, thing)) return (at, -way);
        }
        throw new Xunit.Sdk.XunitException("no clear spot near it");
    }

    static bool Surveys(PlaneWorld w, Vector2 at, PlaneInput input, string mark)
    {
        for (int i = 0; i < 60; i++)
        {
            Hold(w, at, input);
            if (w.SurveyFinds.Contains(mark)) return true;
        }
        return false;
    }

    [Fact]
    public void She_surveys_a_plant_in_the_dark_and_by_the_colour_of_her_light()
    {
        var w = World(Biome.SunlitReef);
        var plant = w.Flora.First();
        string id = "flora:" + plant.Kind;
        var (at, aim) = Near(w, plant.Position, 3f);
        Assert.True(Surveys(w, at, new PlaneInput { Aim = aim }, Survey.Mark(id, "dark")), "not seen in the dark");
        Assert.True(Surveys(w, at, new PlaneInput { Beam = true, Aim = aim }, Survey.Mark(id, "white")), "not lit white");

        var frost = World(Biome.SunlitReef, "frost_kelp", "frost_kelp");
        var p2 = frost.Flora.First();
        var (at2, aim2) = Near(frost, p2.Position, 3f);
        Assert.True(Surveys(frost, at2, new PlaneInput { Beam = true, Aim = aim2 }, Survey.Mark("flora:" + p2.Kind, "frost")), "not lit frost");
    }

    [Fact]
    public void A_biome_surveyed_whole_raises_its_keystone_and_each_entry_has_its_lore()
    {
        foreach (string s in Survey.Subjects) Assert.False(string.IsNullOrEmpty(Survey.Lore(s)), s);
        foreach (var (piece, _) in Survey.Keystones) Assert.True(Codex.Has(Survey.Keystone(piece)));
        var profile = new Profile();
        var subjects = Survey.Of(Biome.SunlitReef).ToList();
        foreach (string s in subjects)
            foreach (string f in Survey.Facets)
                profile.Surveyed.Add(Survey.Mark(s, f));
        string last = Survey.Mark(subjects[0], "pulse");
        profile.Surveyed.Remove(last);
        var rec = new PlaneProfileRecorder(profile);
        var w = World(Biome.SunlitReef);
        w.Step(default);
        rec.Observe(w);
        rec.TakeNewEntries();
        Assert.DoesNotContain("keystone:1", profile.Discovered);
        w.SurveyFinds.Add(last);
        rec.Observe(w);
        Assert.Contains("keystone:1", profile.Discovered);
        Assert.Contains("keystone:1", rec.TakeNewEntries());
        Assert.Contains(last, rec.TakeNewSurveys());
        Assert.DoesNotContain("keystone:2", profile.Discovered);
    }

    [Fact]
    public void Atlantis_s_orrery_opens_only_when_her_light_is_thrown_off_its_mirrors_onto_its_heart()
    {
        var w = World(Biome.Atlantis);
        var room = Assert.Single(w.LightRooms.Where(r => r.Orrery).Take(1));
        Assert.Equal(FloraTuning.OrreryMirrors, room.Mirrors.Count);
        Assert.All(room.Mirrors, m => Assert.Contains(m, w.OpticWalls));
        foreach (var r in w.LightRooms)
            foreach (var m in r.Mirrors)
                Assert.True(w.Map.IsOpen(m.Ends.A) && w.Map.IsOpen(m.Ends.B) && w.Map.IsOpen(m.Centre), "an orrery mirror in the rock");
        Assert.All(World(Biome.SunlitReef).LightRooms, r => Assert.False(r.Orrery));

        void Wake()
        {
            foreach (var p in room.Plants) p.Glow = 1f;
        }
        // The corals glowing is not enough.
        for (int i = 0; i < 120; i++)
        {
            Wake();
            w.Step(default);
        }
        Assert.False(room.Opened, "opened without the heart lit");

        // Turn a mirror so her beam, coming at it from the side, is thrown onto the heart.
        var mirror = room.Mirrors[0];
        var u = Vector2.Normalize(room.Centre - mirror.Centre);
        Vector2 at = default, d = default;
        bool found = false;
        foreach (float deg in new[] { 90f, -90f, 60f, -60f, 120f, -120f })
            foreach (float dist in new[] { 2.2f, 2.6f, 3f })
            {
                float rad = deg * MathF.PI / 180f;
                var v = new Vector2(u.X * MathF.Cos(rad) - u.Y * MathF.Sin(rad), u.X * MathF.Sin(rad) + u.Y * MathF.Cos(rad));
                var p = mirror.Centre + v * dist;
                if (found || !w.Clear(p, w.Radius + 0.2f) || !w.LineOfSight(p, mirror.Centre)) continue;
                (at, d, found) = (p, -v, true);
            }
        Assert.True(found, "no clear spot beside the mirror");
        var n = Vector2.Normalize(u - d);
        mirror.Angle = MathF.Atan2(-n.X, n.Y);
        mirror.Spin = 0f;
        for (int i = 0; i < 60 * 4 && !room.Opened; i++)
        {
            Wake();
            mirror.Spin = 0f;
            Hold(w, at, new PlaneInput { Beam = true, Aim = d });
        }
        Assert.True(room.Opened, $"heart {room.Heart:0.00}");
        Assert.True(room.Heart >= 1f);
    }
}
