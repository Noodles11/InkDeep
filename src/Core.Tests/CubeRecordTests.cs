using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Plane;
using Xunit;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Tests;

/// <summary>Claims, shut edges, fog of war and the two currents (CUBE-WORLD-PLAN §6, §8, §10).</summary>
public class CubeRecordTests
{
    static (CubeSpace Space, Func<CubeId, CubeTerrain?> Terrain, List<CubeTerrain> Cubes) Reef(ulong seed = 0x4B454C50UL)
    {
        var space = new CubeSpace(seed, 5, 2);
        var made = space.CubesOf(0).OrderBy(c => c.Y).ThenBy(c => c.X).ToDictionary(c => c, c => CubeTerrain.Make(space, c));
        return (space, c => made.GetValueOrDefault(c), made.Values.ToList());
    }

    /// <summary>A step across an edge, from its low square into its high one (or back).</summary>
    static Crossing Cross(LayerRecords records, EdgeId edge, bool forward, float along = Side * 0.5f)
    {
        var from = edge.Point(along, forward ? -0.4f : 0.4f);
        var to = edge.Point(along, forward ? 0.4f : -0.4f);
        return records.Move(from, ref to);
    }

    /// <summary>Three cubes in a row: A, B across one edge, C across another of B's (each pair joined by an edge).</summary>
    static (CubeId A, CubeId B, CubeId C, EdgeId AB, EdgeId BC) Row(CubeSpace space)
    {
        foreach (var a in space.CubesOf(0))
        foreach (var (ab, _, toB) in a.Outer())
        {
            var b = CubeId.Of(toB);
            if (!space.Inside(b)) continue;
            foreach (var (bc, _, toC) in b.Outer())
            {
                var c = CubeId.Of(toC);
                if (c != a && space.Inside(c)) return (a, b, c, ab, bc);
            }
        }
        throw new Exception("no row of three cubes");
    }

    [Fact]
    public void A_cube_is_claimed_the_first_time_she_enters_it_and_kept_as_she_left_it()
    {
        var (space, _, _) = Reef();
        var (a, b, c, ab, bc) = Row(space);
        var records = new LayerRecords(0);
        var start = a.Middle;
        Assert.Equal(Crossing.Claimed, records.Arrive(start));
        records.Reveal(start, 10f);
        int seen = records.Claimed[a].SeenCount;
        Assert.InRange(seen, 300, 320);
        Assert.Equal(Crossing.Claimed, Cross(records, ab, forward: ab.Low == a.First || ab.Low == a.Second));
        Assert.Equal(2, records.Claims);
        Assert.Equal(2, records.Claimed[b].Claim);
        // Nothing of a cube not claimed yet is seen, however near she is.
        records.Reveal(bc.Point(Side * 0.5f, 0f), 20f);
        Assert.False(records.IsClaimed(c));
        Assert.False(records.WasSeen(bc.Point(Side * 0.5f, b.Holds(bc.Low) ? 5f : -5f)));
        // Back into A: the same record, as she left it.
        Assert.Equal(Crossing.Back, Cross(records, ab, forward: !(ab.Low == a.First || ab.Low == a.Second)));
        Assert.Equal(1, records.Claimed[a].Claim);
        Assert.Equal(seen, records.Claimed[a].SeenCount);
        Assert.True(records.WasSeen(start));
    }

    [Fact]
    public void Two_cubes_on_the_edge_she_crossed_before_is_shut_and_she_is_held_at_it()
    {
        var (space, _, _) = Reef();
        var (a, b, c, ab, bc) = Row(space);
        var records = new LayerRecords(0);
        records.Arrive(a.Middle);
        bool abForward = a.Holds(ab.Low), bcForward = b.Holds(bc.Low);
        Cross(records, ab, abForward);
        // Back one cube and forth again: the way back stays open.
        Assert.Equal(Crossing.Back, Cross(records, ab, !abForward));
        Assert.Equal(Crossing.Back, Cross(records, ab, abForward));
        Assert.Empty(records.Shut);
        // On into C: the edge before it (A–B) is shut; B–C is not.
        Assert.Equal(Crossing.Claimed, Cross(records, bc, bcForward));
        Assert.Equal(new[] { ab }, records.Shut);
        Assert.Equal(Crossing.Back, Cross(records, bc, !bcForward));
        Assert.Equal(Crossing.Held, Cross(records, ab, !abForward));
        // She leaves the layer: it opens again.
        var reef = new ReefRecords();
        var layer = reef.Of(0);
        layer.Arrive(a.Middle);
        Cross(layer, ab, abForward);
        Cross(layer, bc, bcForward);
        Assert.NotEmpty(layer.Shut);
        reef.Changed(0, 1, 1);
        Assert.Empty(reef.Of(0).Shut);
        Assert.Equal(3, reef.Of(0).Claims);
        reef.Changed(1, 2, 2);
        reef.Changed(2, 3, 3);
        Assert.DoesNotContain(0, reef.Layers);
        Assert.Contains(2, reef.Layers);
    }

    [Fact]
    public void Round_a_ring_of_three_cubes_she_comes_back_into_the_first_as_she_left_it()
    {
        // Three cubes meeting at a point of the herringbone, each touching the other two.
        var space = new CubeSpace(0x4B454C50UL, 7, 1);
        var cubes = space.CubesOf(0).ToList();
        EdgeId Between(CubeId x, CubeId y) => x.Outer().First(o => CubeId.Of(o.Across) == y).Edge;
        bool Touch(CubeId x, CubeId y) => x.Outer().Any(o => CubeId.Of(o.Across) == y);
        var (a, b, c) = cubes.SelectMany(x => cubes.Where(y => Touch(x, y)).SelectMany(y => cubes.Where(z => z != x && Touch(y, z) && Touch(z, x)).Select(z => (x, y, z)))).First();
        var records = new LayerRecords(0);
        records.Arrive(a.Middle);
        records.Reveal(a.Middle, 15f);
        int seen = records.Claimed[a].SeenCount;
        Crossing Step(CubeId from, CubeId to)
        {
            var e = Between(from, to);
            bool forward = from.Holds(e.Low);
            return Cross(records, e, forward);
        }
        Assert.Equal(Crossing.Claimed, Step(a, b));
        Assert.Equal(Crossing.Claimed, Step(b, c));
        // Into the first again, through an edge she has not used: open, and the cube as she left it.
        Assert.Equal(Crossing.Back, Step(c, a));
        Assert.Equal(1, records.Claimed[a].Claim);
        Assert.Equal(seen, records.Claimed[a].SeenCount);
        Assert.Equal(3, records.Claims);
        // The ring's first two edges are now held behind her; the last, the way she came in, is not.
        Assert.True(records.IsShut(Between(a, b)) && records.IsShut(Between(b, c)));
        Assert.False(records.IsShut(Between(c, a)));
    }

    [Fact]
    public void Against_the_wake_current_no_dash_carries_her_through_a_shut_edge()
    {
        var (space, terrain, cubes) = Reef();
        var tuning = new Tuning();
        int tried = 0;
        foreach (var t in cubes)
        foreach (var (edge, gate, mouth) in t.Gates)
        {
            if (!t.Cube.Holds(edge.High)) continue;
            // She is in the high cube, the edge behind her shut.
            var records = new LayerRecords(0);
            records.Arrive(edge.Point(gate.Along, -2f));
            var into = edge.Point(gate.Along, 0.4f);
            records.Move(edge.Point(gate.Along, -0.4f), ref into);
            var other = t.Cube.Outer().First(o => o.Edge != edge && CubeId.Of(o.Across) != CubeId.Of(edge.Low) && space.Inside(CubeId.Of(o.Across))).Edge;
            var beyond = other.Point(Side * 0.5f, t.Cube.Holds(other.Low) ? 0.4f : -0.4f);
            records.Move(other.Point(Side * 0.5f, t.Cube.Holds(other.Low) ? -0.4f : 0.4f), ref beyond);
            Assert.True(records.IsShut(edge));
            // Back at the mouth on her side, she dashes for it again and again, sprinting.
            var world = new CubeWorld(terrain, mouth, 0, tuning) { Records = records, Space = space };
            var back = Vector2.Normalize(edge.Point(gate.Along, -5f) - edge.Point(gate.Along, 5f));
            float nearest = float.MaxValue;
            for (int k = 0; k < 600; k++)
            {
                world.Step(new PlaneInput { Move = back, Aim = back, Dash = k % 60 == 0, DashHeld = true });
                nearest = MathF.Min(nearest, edge.Across(world.Player.Position));
                Assert.True(t.Cube == world.Cube, $"{edge}: through it, at {world.Player.Position}");
            }
            tried++;
        }
        Assert.True(tried >= 2);
    }

    [Fact]
    public void Left_to_drift_she_is_led_toward_a_cube_she_has_not_claimed()
    {
        var (space, terrain, cubes) = Reef();
        int tried = 0;
        foreach (var t in cubes)
        {
            if (t.Arrivals.Count == 0 && t.Down.Count == 0) continue;
            var start = t.Down.Count > 0 ? t.Down[0].Center : t.Arrivals[0].Center;
            var records = new LayerRecords(0);
            records.Arrive(start);
            var world = new CubeWorld(terrain, start, 0) { Records = records, Space = space };
            world.Currents.Prepare(t, space, records);
            if (world.Currents.Targets.Count == 0 || !t.Gates.Any(g => !records.IsClaimed(CubeId.Of(t.Cube.Holds(g.Edge.Low) ? g.Edge.High : g.Edge.Low)))) continue;
            int before = world.Currents.StepsAt(start);
            if (before < 0) continue;
            for (int k = 0; k < 60 * 40 && world.Cube == t.Cube; k++) world.Step(default);
            int after = world.Cube == t.Cube ? world.Currents.StepsAt(world.Player.Position) : 0;
            // (Idle, at 0.6 m/s, for up to 40 s: into new water, at a gate's line, or 18 m nearer one over the water.)
            Assert.True(after >= 0 && (after <= 2 || before - after >= 18), $"{t.Cube}: from {before} m to {after} m over the water");
            tried++;
        }
        Assert.True(tried >= 3);
    }
}
