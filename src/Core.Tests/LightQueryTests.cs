using System;
using System.Numerics;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>How bright an emitter is at a point, and which is brightest (PlaneLight.cs; docs/LIGHT-AND-GRAPH.md §1).</summary>
public class LightQueryTests
{
    static LightEmitter Point(Vector2 at, float intensity = 1f, float range = 10f, float energy = 100f) =>
        new() { Owner = new LightPool(energy), Position = at, Intensity = intensity, Range = range };

    static LightEmitter Cone(Vector2 at, Vector2 dir, float range = 16f) =>
        new() { Owner = new LightPool(100f), Shape = LightShape.Cone, Position = at, Direction = dir, Range = range, Intensity = 1f };

    [Fact]
    public void A_point_fades_with_distance_and_ends_at_its_reach()
    {
        var e = Point(Vector2.Zero, range: 10f);
        Assert.Equal(1f, LightQuery.Brightness(e, Vector2.Zero), 4);
        float near = LightQuery.Brightness(e, new Vector2(2f, 0f)), far = LightQuery.Brightness(e, new Vector2(0f, 6f));
        Assert.True(near > far && far > 0f);
        Assert.Equal(MathF.Pow(1f - 0.6f, LightTuning.Falloff), far, 4);
        Assert.Equal(0f, LightQuery.Brightness(e, new Vector2(10f, 0f)));
        Assert.Equal(0f, LightQuery.Brightness(e, new Vector2(30f, 0f)));
    }

    [Fact]
    public void A_cone_lights_along_its_axis_only()
    {
        var e = Cone(Vector2.Zero, Vector2.UnitX);
        float onAxis = LightQuery.Brightness(e, new Vector2(8f, 0f));
        Assert.True(onAxis > 0f);
        Assert.Equal(0f, LightQuery.Brightness(e, new Vector2(-8f, 0f)));
        Assert.Equal(0f, LightQuery.Brightness(e, new Vector2(0f, 8f)));
        // Inside the soft core of the cone: as bright as on its axis; past it, fading toward the edge; past the edge, dark.
        float half = e.HalfAngle;
        Vector2 At(float angle) => new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f;
        Assert.Equal(onAxis, LightQuery.Brightness(e, At(half * 0.5f)), 4);
        float edge = LightQuery.Brightness(e, At(half * 0.9f));
        Assert.True(edge > 0f && edge < onAxis);
        Assert.Equal(0f, LightQuery.Brightness(e, At(half * 1.05f)));
    }

    [Fact]
    public void An_inactive_or_spent_emitter_lights_nothing()
    {
        var off = Point(Vector2.Zero);
        off.Active = false;
        Assert.Equal(0f, LightQuery.Brightness(off, Vector2.One));
        var spent = Point(Vector2.Zero, energy: 0f);
        Assert.Equal(0f, LightQuery.Brightness(spent, Vector2.One));
    }

    [Fact]
    public void Rock_between_blocks_the_light()
    {
        var e = Point(Vector2.Zero);
        Func<Vector2, Vector2, bool> wall = (a, b) => MathF.Sign(a.X - 1f) == MathF.Sign(b.X - 1f);
        Assert.True(LightQuery.Brightness(e, new Vector2(0.5f, 0f), wall) > 0f);
        Assert.Equal(0f, LightQuery.Brightness(e, new Vector2(3f, 0f), wall));
    }

    [Fact]
    public void The_brightest_wins_over_a_nearer_dimmer_light()
    {
        var at = Vector2.Zero;
        var dim = Point(new Vector2(2f, 0f), intensity: 0.3f);
        var bright = Point(new Vector2(-4f, 0f), intensity: 1.5f);
        var reg = new LightRegistry();
        reg.Add(dim);
        reg.Add(bright);
        Assert.Same(bright, LightQuery.Brightest(reg.All, at, 20f, LightTuning.Threshold, null));
        // Out of the scan, or behind rock, it is not seen: the dimmer one is.
        Assert.Same(dim, LightQuery.Brightest(reg.All, at, 3f, LightTuning.Threshold, null));
        Assert.Same(dim, LightQuery.Brightest(reg.All, at, 20f, LightTuning.Threshold, (a, b) => a.X >= 0f && b.X >= 0f));
    }

    [Fact]
    public void Too_faint_or_undrinkable_lights_are_not_picked()
    {
        var reg = new LightRegistry();
        reg.Add(Point(new Vector2(9.9f, 0f), range: 10f));
        var shy = reg.Add(Point(new Vector2(1f, 0f)));
        shy.Drinkable = false;
        Assert.Null(LightQuery.Brightest(reg.All, Vector2.Zero, 20f, LightTuning.Threshold, null));
    }

    [Fact]
    public void Equal_lights_pick_the_older_one()
    {
        var reg = new LightRegistry();
        var a = reg.Add(Point(new Vector2(3f, 0f)));
        reg.Add(Point(new Vector2(-3f, 0f)));
        Assert.Same(a, LightQuery.Brightest(reg.All, Vector2.Zero, 20f, LightTuning.Threshold, null));
    }

    [Fact]
    public void Lights_add_up_where_they_overlap()
    {
        var reg = new LightRegistry();
        var a = reg.Add(Point(new Vector2(2f, 0f)));
        var b = reg.Add(Point(new Vector2(-2f, 0f)));
        float sum = LightQuery.Total(reg.All, Vector2.Zero, null);
        Assert.Equal(LightQuery.Brightness(a, Vector2.Zero) + LightQuery.Brightness(b, Vector2.Zero), sum, 4);
    }
}
