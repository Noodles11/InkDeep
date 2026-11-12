using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her light's paths on rock (docs/LIGHT-AND-GRAPH.md §11, PlanePaths.cs).</summary>
public class PathTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    static PlaneWorld World(params string[] pearls)
    {
        var run = new PlaneRun(Catalog.Value, new Tuning());
        foreach (string id in pearls) run.Add(id);
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1)), new Tuning(), run, War);
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
        w.ChargeStones.Clear();
        return w;
    }

    /// <summary>
    /// A place on the level and a way from it to rock 3–7 m off, met nearly square on with open water behind her (so
    /// what comes off the rock comes back into the open), and no thin wall a ghost would pass through.
    /// </summary>
    static (Vector2 At, Vector2 D) ToWall(PlaneWorld w)
    {
        var map = w.Map;
        for (float x = 4f; x < map.Size - 4f; x += 2f)
            for (float y = 4f; y < map.Size - 4f; y += 2f)
            {
                var at = new Vector2(x, y);
                if (!map.IsOpen(at) || !w.Clear(at, 0.8f)) continue;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * MathF.Tau / 16f;
                    var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    float len = w.LineReach(at, d, 9f, out bool hit);
                    if (!hit || len < 3f || len > 7f) continue;
                    var n = map.Gradient(at + d * (len + 0.2f));
                    if (n.LengthSquared() < 1e-6f || MathF.Abs(Vector2.Dot(Vector2.Normalize(n), d)) < 0.97f) continue;
                    if (map.IsOpen(at + d * (len + 4f))) continue;
                    return (at, d);
                }
            }
        throw new InvalidOperationException("no wall to aim at");
    }

    static PlaneWorld Shine(bool wall, params string[] pearls)
    {
        var w = World(pearls);
        var (at, d) = wall ? ToWall(w) : (w.Map.Start.Position, Vector2.UnitX);
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Step(new PlaneInput { Aim = d, Beam = true });
        return w;
    }

    [Fact]
    public void No_path_pearl_no_rays_born()
    {
        Assert.Empty(Shine(true).Born);
    }

    [Fact]
    public void Bounce_reflects_off_the_rock_with_less_light()
    {
        var w = Shine(true, "mirror_scale");
        var b = Assert.Single(w.Born);
        Assert.Equal(PathWord.Bounce, b.Word);
        Assert.True(w.Map.IsOpen(b.Emitter.Position));
        Assert.True(Vector2.Dot(b.Emitter.Direction, w.BeamAim) < 0.5f, "it leaves the rock");
        Assert.True(b.Worth < HerLightTuning.BeamShots * PathTuning.Bounce + 1e-3f);
        Assert.True(b.Emitter.Shining);
        Assert.Contains(w.HerBeams, x => x.Beam == b.Emitter);
    }

    [Fact]
    public void Copies_of_Mirror_Scale_bounce_again()
    {
        var w = Shine(true, "mirror_scale", "mirror_scale", "mirror_scale");
        Assert.All(w.Born, b => Assert.Equal(PathWord.Bounce, b.Word));
        Assert.True(w.Born.Count >= 1 && w.Born.Count <= 3);
        Assert.True(w.Born.Max(b => b.Bounces) <= 3);
    }

    [Fact]
    public void Fork_makes_a_Y_off_the_rock()
    {
        var w = Shine(true, "mitosis");
        Assert.Equal(2, w.Born.Count);
        Assert.All(w.Born, b => Assert.Equal(PathWord.Fork, b.Word));
        float apart = MathF.Acos(Math.Clamp(Vector2.Dot(w.Born[0].Emitter.Direction, w.Born[1].Emitter.Direction), -1f, 1f)) * 180f / MathF.PI;
        Assert.InRange(apart, 2 * PathTuning.ForkDeg - 1f, 2 * PathTuning.ForkDeg + 1f);
    }

    [Fact]
    public void Return_comes_back_along_its_way_and_lights_what_stands_in_it_from_both_sides()
    {
        var w = Shine(true, "boomerang_shrimp");
        var r = Assert.Single(w.Born);
        Assert.Equal(PathWord.Return, r.Word);
        Assert.True(Vector2.Dot(r.Emitter.Direction, w.BeamAim) < -0.99f);
        // A point between her and the wall is in both.
        var mid = Vector2.Lerp(w.Beam.Position, r.At, 0.6f);
        Assert.True(w.BeamLight(w.Beam, mid, 0f, 0.3f) > 0f);
        Assert.True(w.BeamLight(r.Emitter, mid, 0f, 0.3f) > 0f);
    }

    [Fact]
    public void Cathedral_every_bounce_forks()
    {
        // Swept round from in front of a wall: somewhere a bounce lands on rock again, and forks there.
        var w = Shine(true, "mirror_scale", "mitosis");
        Assert.Contains("cathedral", w.Run.Loadout.Synergies);
        var at = w.Player.Position;
        bool forked = false;
        for (int i = 0; i < 48 && !forked; i++)
        {
            float a = i * MathF.Tau / 48f;
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = new Vector2(MathF.Cos(a), MathF.Sin(a)), Beam = true });
            forked = w.Born.Any(b => b.Word == PathWord.Fork && (b.Taken & PathWord.Bounce) != 0);
        }
        Assert.True(forked);
    }

    [Fact]
    public void A_born_ray_never_takes_its_own_word_again_and_there_are_never_too_many()
    {
        var w = World("hammerhead", "mirror_scale", "mirror_scale", "mitosis", "boomerang_shrimp", "ghost_jelly");
        for (int i = 0; i < 16; i++)
        {
            float a = i * MathF.Tau / 16f;
            w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
            w.Step(new PlaneInput { Aim = new Vector2(MathF.Cos(a), MathF.Sin(a)), Beam = true });
            Assert.True(w.Born.Count <= PathTuning.MostBorn);
            Assert.All(w.Born, b => Assert.True(b.Word != PathWord.Fork || b.Taken.HasFlag(PathWord.Fork)));
        }
    }

    [Fact]
    public void Ghost_passes_through_a_thin_wall()
    {
        // Somewhere on the level: a wall at most GhostThick thick with open water beyond it, in her beam's reach.
        var w = World("ghost_jelly");
        var map = w.Map;
        for (float x = 4f; x < map.Size - 4f; x += 3f)
            for (float y = 4f; y < map.Size - 4f; y += 3f)
            {
                var at = new Vector2(x, y);
                if (!map.IsOpen(at) || !w.Clear(at, 0.6f)) continue;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathF.Tau / 8f;
                    var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    float len = w.LineReach(at, d, 6f, out bool hit);
                    if (!hit || len < 2f || len > 5f) continue;
                    float through = -1f;
                    for (float t = 0.1f; t <= PathTuning.GhostThick; t += 0.1f)
                        if (map.IsOpen(at + d * (len + t)))
                        {
                            through = t;
                            break;
                        }
                    if (through < 0.4f || len + through > 7f) continue;
                    w.Player.Position = w.Player.PrevPosition = at;
                    w.Player.Velocity = Vector2.Zero;
                    w.Step(new PlaneInput { Aim = d, Beam = true });
                    if (w.Born.Count == 0) continue;
                    var g = w.Born[0];
                    Assert.Equal(PathWord.Ghost, g.Word);
                    Assert.True(map.IsOpen(g.Emitter.Position), "out in the open beyond the wall");
                    Assert.True(Vector2.Dot(g.Emitter.Position - at, d) > len, "past the wall");
                    return;
                }
            }
        Assert.Fail("no thin wall found");
    }
}
