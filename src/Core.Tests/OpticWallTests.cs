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

/// <summary>Phase 2 of the lit world (PlaneOpticWalls.cs): her beam divided at walls, mirror plates, lightglass.</summary>
public class OpticWallTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(Biome biome, params string[] pearls)
    {
        var (depth, branch) = biome switch
        {
            Biome.Shipwreck => (2, 1), Biome.AlienWreckage => (4, 0), Biome.DrownedTemple => (4, 1), Biome.Atlantis => (5, 0), _ => (1, 0),
        };
        var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, depth + LevelPlan.Tutorial, 2) with { Branch = branch }, crossover: true);
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (var p in pearls) run.Add(p);
        var w = new PlaneWorld(map, new Tuning(), run, new PlaneOptions { Adventure = true, Glow = false, Gun = true, Optics = true });
        w.Murklings.Clear();
        return w;
    }

    /// <summary>A clear spot facing a wall's face from <paramref name="away"/> metres, and the way to it.</summary>
    static (Vector2 At, Vector2 Aim) Facing(PlaneWorld w, OpticWall wall, float away = 3.5f)
    {
        foreach (float side in new[] { 1f, -1f })
            for (float a = away; a >= 2f; a -= 0.5f)
            {
                var at = wall.Centre + wall.Normal * side * a;
                if (w.Clear(at, w.Radius + 0.1f) && w.LineOfSight(at, wall.Centre + wall.Normal * side * (wall.HalfThick + 0.2f))) return (at, -wall.Normal * side);
            }
        throw new Xunit.Sdk.XunitException("no clear spot facing the wall");
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(input);
    }

    [Fact]
    public void The_metal_biomes_have_mirror_plates_and_the_alien_wreck_lightglass_across_its_passages()
    {
        var wreck = World(Biome.Shipwreck);
        Assert.Contains(wreck.OpticWalls, o => o.Kind == OpticKind.Mirror);
        var alien = World(Biome.AlienWreckage);
        var panes = alien.OpticWalls.Where(o => o.Kind == OpticKind.Lightglass).ToList();
        Assert.NotEmpty(panes);
        foreach (var p in panes)
        {
            var (a, b) = p.Ends;
            Assert.False(alien.Map.IsOpen(a) && alien.Map.IsOpen(b), "a pane with both ends in the open");
            Assert.False(alien.Clear(p.Centre, 0.3f), "she can swim through a pane");
        }
        Assert.Empty(World(Biome.SunlitReef).OpticWalls);
    }

    [Fact]
    public void Her_beam_on_a_mirror_plate_goes_on_reflected_whole()
    {
        var w = World(Biome.Shipwreck);
        var plate = w.OpticWalls.First(o => o.Kind == OpticKind.Mirror);
        var (at, aim) = Facing(w, plate);
        Hold(w, at, new PlaneInput { Beam = true, Aim = aim });
        Hold(w, at, new PlaneInput { Beam = true, Aim = aim });
        var ray = w.Born.FirstOrDefault(b => b.Word == PathWord.Mirror && Vector2.Distance(b.At, plate.Centre) < plate.HalfLength + 0.5f);
        Assert.NotNull(ray);
        Assert.True(Vector2.Dot(ray!.Emitter.Direction, -aim) > 0.9f, $"reflected {ray.Emitter.Direction}, came along {aim}");
    }

    [Fact]
    public void Her_gun_rings_off_a_mirror_plate()
    {
        var w = World(Biome.Shipwreck);
        var plate = w.OpticWalls.First(o => o.Kind == OpticKind.Mirror);
        var (at, aim) = Facing(w, plate);
        bool bounced = false;
        for (int i = 0; i < 90 && !bounced; i++)
        {
            Hold(w, at, new PlaneInput { Fire = i % 15 == 0, Aim = aim });
            bounced = w.Events.Any(e => e.Type == PlaneEventType.ShotBounced && Vector2.Distance(e.Position, plate.Centre) < plate.HalfLength + 0.8f);
        }
        Assert.True(bounced);
    }

    [Fact]
    public void Lightglass_passes_only_its_colour_and_shatters_under_it_faster_for_a_light_of_its_colour()
    {
        int Steps(params string[] pearls)
        {
            var w = World(Biome.AlienWreckage, pearls);
            var pane = w.OpticWalls.First(o => o.Kind == OpticKind.Lightglass);
            pane.Band = LightHue.Blue;
            var (at, aim) = Facing(w, pane);
            int shells = w.Shells.Count;
            for (int i = 0; i < 60 * 30; i++)
            {
                Hold(w, at, new PlaneInput { Beam = true, Aim = aim });
                if (i == 3)
                {
                    var through = w.Born.FirstOrDefault(b => b.Word == PathWord.Refract);
                    Assert.NotNull(through);
                    Assert.Equal(LightHue.Blue, through!.Hue);
                }
                if (!pane.Shattered) continue;
                Assert.True(w.Shells.Count > shells, "a shattered pane leaves shells");
                Assert.True(w.Clear(pane.Centre, 0.3f), "a shattered pane still stands");
                return i;
            }
            return int.MaxValue;
        }
        int plain = Steps(), frost = Steps("frost_kelp", "frost_kelp");
        Assert.True(plain < int.MaxValue, "her plain light never shattered a blue pane");
        Assert.True(frost < plain * 0.8f, $"frost {frost} steps, plain {plain}");
    }

    [Fact]
    public void She_turns_a_mirror_plate_by_pushing_its_end()
    {
        var w = World(Biome.Shipwreck);
        var plate = w.OpticWalls.First(o => o.Kind == OpticKind.Mirror);
        float before = plate.Angle;
        var (at, aim) = Facing(w, plate, 2.2f);
        // (Off its centre, toward one end, swimming into it.)
        var start = at + plate.Axis * (plate.HalfLength * 0.8f);
        if (!w.Clear(start, w.Radius)) start = at - plate.Axis * (plate.HalfLength * 0.8f);
        w.Player.Position = w.Player.PrevPosition = start;
        for (int i = 0; i < 120; i++)
        {
            w.Player.Hp = w.Run.MaxHp;
            w.Step(new PlaneInput { Move = aim });
        }
        Assert.True(MathF.Abs(plate.Angle - before) > 0.05f, $"turned only {plate.Angle - before} rad");
    }

    [Fact]
    public void On_the_wreck_s_iron_her_beam_is_divided_a_mirror_ray_springing_off_it()
    {
        var w = World(Biome.Shipwreck);
        var at = w.Map.Start.Position;
        bool any = false;
        for (int k = 0; k < 32 && !any; k++)
        {
            var aim = new Vector2(MathF.Cos(k * MathF.Tau / 32f), MathF.Sin(k * MathF.Tau / 32f));
            if (w.LineReach(at, aim, 12f, out bool hit) > 11f || !hit) continue;
            Hold(w, at, new PlaneInput { Beam = true, Aim = aim });
            Hold(w, at, new PlaneInput { Beam = true, Aim = aim });
            any = w.Born.Any(b => b.Word == PathWord.Mirror);
        }
        Assert.True(any, "her beam never sprang off the iron");
    }
}
