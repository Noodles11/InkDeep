using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Stone lanterns (docs/LIGHT-AND-GRAPH.md §2.7; PlaneLanterns.cs) and the carved arrows to a secret's mouth (CubeStock.Cues).</summary>
public class LanternTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.Fish.Clear();
        return w;
    }

    static (Vector2 At, Vector2 Way) Open(PlaneWorld w, float far)
    {
        var at = w.Map.Start.Position;
        for (int k = 0; k < 72; k++)
        {
            var way = new Vector2(MathF.Cos(k * MathF.Tau / 72f), MathF.Sin(k * MathF.Tau / 72f));
            if (w.LineReach(at, way, far, out _) >= far && w.Clear(at + way * far, 1f)) return (at, way);
        }
        throw new InvalidOperationException("no open water by her start");
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Step(input);
    }

    [Fact]
    public void Her_beam_held_on_a_lantern_lights_it_for_good()
    {
        var w = World();
        var (at, way) = Open(w, 10f);
        var lantern = w.PlaceLantern(default, at + way * 6f, null);
        bool lit = false;
        for (int i = 0; i < (int)(LanternTuning.Kindle * 0.6f / PlaneWorld.Dt); i++) Hold(w, at, new PlaneInput { Beam = true, Aim = way });
        Assert.False(lantern.Lit);
        // (Taken off, it cools back.)
        float held = lantern.Held;
        Hold(w, at, default);
        Assert.True(lantern.Held < held);
        for (int i = 0; i < (int)(LanternTuning.Kindle * 1.5f / PlaneWorld.Dt) && !lit; i++)
        {
            Hold(w, at, new PlaneInput { Beam = true, Aim = way });
            lit |= w.Events.Any(e => e.Type == PlaneEventType.LanternLit);
        }
        Assert.True(lit);
        Assert.True(lantern.Lit);
        for (int i = 0; i < 30; i++) Hold(w, at, default);
        Assert.True(lantern.Lit);
        Assert.Equal(lantern.Store, w.LanternLeft(default));
    }

    [Fact]
    public void A_lit_lantern_heals_her_by_it_out_of_its_store_and_shows_what_is_near_it()
    {
        var w = World();
        var (at, way) = Open(w, 10f);
        var spot = at + way * 6f;
        var lantern = w.PlaceLantern(default, spot, 5f);
        Assert.Equal(1f, w.LanternSees(spot + way * 1f, 0.3f));
        Assert.Equal(0f, w.LanternSees(spot + way * (LanternTuning.Reach + 1f), 0f));
        w.Player.Hp = w.Run.MaxHp * 0.5f;
        float hp = w.Player.Hp;
        for (int i = 0; i < 400; i++) Hold(w, spot + way * 1f, default);
        Assert.Equal(hp + 5f, w.Player.Hp, 2);
        Assert.Equal(0f, lantern.Store, 3);
    }

    [Fact]
    public void A_cubes_lantern_stands_on_its_open_floor_always_the_same()
    {
        var space = new CubeSpace(42UL, 6, 2);
        int lanterns = 0;
        foreach (var c in space.CubesOf(1))
        {
            var t = CubeTerrain.Make(space, c);
            var at = CubeStock.Lantern(space.Seed, t);
            Assert.Equal(at, CubeStock.Lantern(space.Seed, CubeTerrain.Make(space, c)));
            if (at is not { } p) continue;
            lanterns++;
            Assert.Equal(c, CubeId.Of(p, 1));
            Assert.Equal(CubeCell.Shelf, t.CellAt((int)MathF.Round(p.X), (int)MathF.Round(p.Y)));
            Assert.All(t.Gates, g => Assert.True(Vector2.Distance(g.Mouth, p) >= LanternTuning.FromWays));
        }
        Assert.True(lanterns > 0);
    }

    [Fact]
    public void The_arrows_lie_at_a_walls_foot_and_point_to_the_secrets_mouth()
    {
        // A straight wall along x = 0 (rock west of it), the mouth cut into it facing east.
        float Height(Vector2 p) => p.X < 0f ? 3f : -1.1f;
        var mouth = new Doorway(new Vector2(0f, 0f), new Vector2(-1f, 0f));
        var cues = CubeStock.Cues(7UL, mouth, Height, (a, b) => true);
        Assert.NotEmpty(cues);
        Assert.True(cues.Count <= CubeStock.CueMost);
        var before = mouth.At - mouth.Into * 2f;
        foreach (var (at, way) in cues)
        {
            Assert.InRange(at.X, 0f, CubeStock.CueWall);
            Assert.InRange(Vector2.Distance(at, before), CubeStock.CueNear, CubeStock.CueFar);
            Assert.True(Vector2.Dot(way, Vector2.Normalize(before - at)) > 0.999f);
        }
        Assert.Equal(cues, CubeStock.Cues(7UL, mouth, Height, (a, b) => true));
        Assert.Empty(CubeStock.Cues(7UL, mouth, Height, (a, b) => false));
    }
}
