using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>A secret's cracked seal (PlaneCrackSeals.cs): only her dash into its crack gets through, and then for good.</summary>
public class CrackSealTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    /// <summary>A world with a seal 4 m along open water from her start, its passage running on that way.</summary>
    static (PlaneWorld W, Vector2 At, Vector2 Way, CrackSeal Seal) World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true }) { BeamLamp = true };
        w.Murklings.Clear();
        w.Fish.Clear();
        var at = w.Map.Start.Position;
        for (int k = 0; k < 72; k++)
        {
            var way = new Vector2(MathF.Cos(k * MathF.Tau / 72f), MathF.Sin(k * MathF.Tau / 72f));
            if (w.LineReach(at, way, 12f, out _) < 12f || !w.Clear(at + way * 12f, 2.5f)) continue;
            var across = new Vector2(-way.Y, way.X);
            bool wide = true;
            for (float s = -2.5f; s <= 2.5f && wide; s += 0.5f) wide = w.LineReach(at + across * s, way, 12f, out _) >= 12f;
            if (!wide) continue;
            return (w, at, way, w.PlaceCrackSeal(new Doorway(at + way * 4f, way)));
        }
        throw new InvalidOperationException("no wide open water by her start");
    }

    static void Swim(PlaneWorld w, Vector2 way, int steps, bool dash = false)
    {
        for (int i = 0; i < steps; i++) w.Step(new PlaneInput { Move = way, Aim = way, Dash = dash && i == 0, DashHeld = dash });
    }

    static float Along(PlaneWorld w, Vector2 at, Vector2 way) => Vector2.Dot(w.Player.Position - at, way);

    [Fact]
    public void Swimming_into_it_she_is_stopped_at_its_face()
    {
        var (w, at, way, seal) = World();
        Swim(w, way, 120);
        Assert.False(seal.Open);
        Assert.True(Along(w, at, way) < 4f + CrackSealTuning.Inset, $"she got to {Along(w, at, way)}");
    }

    [Fact]
    public void Her_dash_into_its_crack_crumbles_it_and_takes_her_through()
    {
        var (w, at, way, seal) = World();
        // (Up to it first, then the dash.)
        Swim(w, way, 60);
        bool crumbled = false;
        for (int i = 0; i < 40 && !crumbled; i++)
        {
            w.Step(new PlaneInput { Move = way, Aim = way, Dash = i == 0, DashHeld = true });
            crumbled |= w.Events.Any(e => e.Type == PlaneEventType.CrackSealCrumbled);
        }
        Assert.True(crumbled);
        Assert.True(seal.Open);
        Swim(w, way, 60);
        Assert.True(Along(w, at, way) > 4f + CrackSealTuning.Inset + CrackSealTuning.Thick * 2f);
    }

    [Fact]
    public void A_dash_beside_the_crack_is_stopped_as_any_swim()
    {
        var (w, at, way, seal) = World();
        var across = new Vector2(-way.Y, way.X);
        w.Player.Position = w.Player.PrevPosition = at + across * 1.4f;
        Swim(w, way, 60);
        for (int i = 0; i < 40; i++) w.Step(new PlaneInput { Move = way, Aim = way, Dash = i == 0, DashHeld = true });
        Assert.False(seal.Open);
        Assert.True(Along(w, at, way) < 4f + CrackSealTuning.Inset);
    }

    [Fact]
    public void It_stops_her_light_bubbles()
    {
        var (w, at, way, seal) = World();
        for (int i = 0; i < 60; i++) w.Step(new PlaneInput { Aim = way, Fire = true });
        Assert.All(w.Shots.Where(s => s.FromPlayer && s.Life > 0f), s => Assert.True(Vector2.Dot(s.Position - at, way) < 4f + CrackSealTuning.Inset + CrackSealTuning.Thick * 2f));
        Assert.False(seal.Open);
    }
}
