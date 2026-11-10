using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her beam: a cone along her aim while held, burning, dazzling, striking seals, costing her nothing (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2).</summary>
public class BeamTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World(LevelMap map) =>
        new(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true });

    /// <summary>An adventure level with no fish, no anemones and every leak sealed: only what a test brings.</summary>
    static PlaneWorld Bare()
    {
        var w = World(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)));
        w.ReefFish.Clear();
        w.Anemones.Clear();
        w.Schools.Clear();
        w.Flounders.Clear();
        foreach (var v in w.Leaks) v.Sealed = true;
        return w;
    }

    /// <summary>A way from her start with open water this far along it and the other way, both in plain sight.</summary>
    static Vector2? Line(PlaneWorld w, float distance)
    {
        var her = w.Map.Start.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            if (w.Clear(her + d * distance, 0.6f) && w.LineOfSight(her, her + d * distance)
                && w.Clear(her - d * distance, 0.6f) && w.LineOfSight(her, her - d * distance)) return d;
        }
        return null;
    }

    static Murkling Murkling(PlaneWorld w, Vector2 at)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f };
        w.Murklings.Add(m);
        return m;
    }

    /// <summary>A step with her held at her start, full of light, the beam held (or not) along <paramref name="aim"/>, the murklings pinned.</summary>
    static void Step(PlaneWorld w, Vector2 aim, bool beam, params (Murkling M, Vector2 At)[] pinned)
    {
        foreach (var (m, at) in pinned)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(new PlaneInput { Aim = aim, Beam = beam });
    }

    [Fact]
    public void The_beam_shines_only_while_held_and_points_where_she_aims_not_where_she_swims()
    {
        var w = Bare();
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Move = Vector2.UnitY });
        Assert.False(w.BeamOn);
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Move = Vector2.UnitY, Beam = true });
        Assert.True(w.BeamOn);
        Assert.Equal(1f, w.Beam.Direction.X, 3);
        Assert.Equal(w.Player.Position, w.Beam.Position);
        Assert.Same(w.Her, w.Beam.Owner);
        Assert.Contains(w.Beam, w.Lights.All);
        w.Step(new PlaneInput { Aim = -Vector2.UnitY, Move = Vector2.UnitX, Beam = true });
        Assert.Equal(-1f, w.Beam.Direction.Y, 3);
        w.Step(new PlaneInput());
        Assert.False(w.BeamOn);
    }

    [Fact]
    public void Shining_costs_her_nothing_and_nothing_mends_her_on_its_own()
    {
        var w = Bare();
        w.Player.Hp = 60f;
        for (int i = 0; i < 3 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
            w.Step(new PlaneInput { Aim = Vector2.UnitX, Beam = true });
        }
        Assert.Equal(60f, w.Player.Hp, 3);
    }

    [Fact]
    public void The_beam_burns_what_is_in_its_cone_far_beyond_a_shots_reach_and_nothing_outside_it()
    {
        var w = Bare();
        if (Line(w, 8f) is not { } way) return;
        var her = w.Map.Start.Position;
        Vector2 litAt = her + way * 8f, darkAt = her - way * 8f;
        var lit = Murkling(w, litAt);
        var dark = Murkling(w, darkAt);
        float full = lit.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, way, true, (lit, litAt), (dark, darkAt));
        // (As much as its light reaches it: PlaneWorld.BeamLight.)
        Assert.Equal(full - HerLightTuning.BeamShots * HerLightTuning.Burn * w.BeamLight(w.Beam, litAt, 0f, lit.Radius, lit), lit.Corruption.Value, 1);
        Assert.True(w.InHerLight(lit));
        Assert.True(w.BeamBurns);
        Assert.Equal(full, dark.Corruption.Value);
        Assert.False(w.InHerLight(dark));
        // Off, it burns nothing.
        float now = lit.Corruption.Value;
        for (int i = 0; i < 10; i++) Step(w, way, false, (lit, litAt), (dark, darkAt));
        Assert.Equal(now, lit.Corruption.Value, 3);
        Assert.False(w.BeamBurns);
    }

    [Fact]
    public void The_beam_burns_harder_the_nearer_her_it_reaches()
    {
        float Burned(float distance)
        {
            var w = Bare();
            if (Line(w, 13f) is not { } way) return float.NaN;
            var at = w.Map.Start.Position + way * distance;
            var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
            m.Corruption.Max = m.Corruption.Value = 10000f;
            w.Murklings.Add(m);
            for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, way, true, (m, at));
            return 10000f - m.Corruption.Value;
        }
        float near = Burned(2.5f), far = Burned(12f);
        if (float.IsNaN(near)) return;
        
        Assert.True(far > 0f);
        Assert.True(near > far * 2.5f, $"near {near}, far {far}");
    }

    [Fact]
    public void A_body_in_the_way_stops_the_beam_as_rock_does_unless_her_light_pierces()
    {
        foreach (bool pierce in new[] { false, true })
        {
            var map = TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss));
            var run = new PlaneRun(Catalog.Value, new Tuning());
            if (pierce) run.Add("swordfish_bill");
            var w = new PlaneWorld(map, new Tuning(), run, new PlaneOptions { Adventure = true });
            w.ReefFish.Clear();
            w.Flounders.Clear();
            foreach (var v in w.Leaks) v.Sealed = true;
            if (Line(w, 9f) is not { } way) return;
            var her = w.Map.Start.Position;
            Vector2 frontAt = her + way * 4f, backAt = her + way * 8f;
            Murkling Pinned(Vector2 at)
            {
                var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
                m.Corruption.Max = m.Corruption.Value = 10000f;
                w.Murklings.Add(m);
                return m;
            }
            var front = Pinned(frontAt);
            var back = Pinned(backAt);
            for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, way, true, (front, frontAt), (back, backAt));
            Assert.True(front.Corruption.Value < 10000f);
            if (pierce) Assert.True(back.Corruption.Value < 10000f, "her light pierces");
            else Assert.Equal(10000f, back.Corruption.Value);
            // It shows in the beam only where the beam reaches it; the beam is drawn to the first body across it.
            Assert.True(w.BeamSees(front, frontAt, front.Radius));
            Assert.Equal(pierce, w.BeamSees(back, backAt, back.Radius));
            float stop = Vector2.Distance(her, w.BeamStop(w.Beam));
            if (pierce) Assert.True(stop > 8f);
            else Assert.InRange(stop, 3f, 4f);
        }
    }

    [Fact]
    public void The_beam_seals_a_leak_from_beyond_her_inner_lights_reach()
    {
        var w = Bare();
        w.Inkwardens.Clear();
        // A leak, and a spot 9 m off it in open water with a clear line to it: past her inner light's reach.
        Vector2 her = default;
        var v = w.Leaks.FirstOrDefault(l => Enumerable.Range(0, 24).Select(i => l.Position + new Vector2(MathF.Cos(i * 0.26f), MathF.Sin(i * 0.26f)) * 9f)
            .FirstOrDefault(q => w.Clear(q, 0.8f) && w.LineOfSight(q, l.Position)) is var q && q != default && (her = q) == q);
        Assert.NotNull(v);
        v!.Sealed = false;
        var way = Vector2.Normalize(v.Position - her);
        void Shine()
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = way, Beam = true });
        }
        for (int i = 0; i < PlaneWorld.TickRate; i++) Shine();
        Assert.True(v.Light > 0f);
        for (int i = 0; i < 60 * PlaneWorld.TickRate && !v.Sealed; i++) Shine();
        Assert.True(v.Sealed);
    }

    [Fact]
    public void Held_on_a_murkling_the_beam_dazzles_it_once_not_for_ever()
    {
        var w = Bare();
        if (Line(w, 6f) is not { } way) return;
        var at = w.Map.Start.Position + way * 6f;
        var m = Murkling(w, at);
        Step(w, way, false, (m, at));
        Step(w, way, true, (m, at));
        Assert.True(w.Dazzled(m));
        for (int i = 0; i < (HerLightTuning.Dazzle + 0.2f) * PlaneWorld.TickRate && m.Alive; i++) Step(w, way, true, (m, at));
        Assert.True(m.Alive);
        Assert.False(w.Dazzled(m));
        Assert.True(w.InHerLight(m));
    }

    [Fact]
    public void Rock_stops_the_beam()
    {
        var w = Bare();
        var her = w.Map.Start.Position;
        // A way whose first rock lies within the beam's reach, and an open spot past it along that way.
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            Vector2? past = null;
            bool rock = false;
            for (float s = 1f; s < LightTuning.BeamReach - 1f; s += 0.25f)
            {
                var q = her + d * s;
                if (!w.Map.IsOpen(q)) rock = true;
                else if (rock && w.Clear(q, 0.6f)) { past = q; break; }
            }
            if (past is not { } behind) continue;
            var m = Murkling(w, behind);
            for (int k = 0; k < 20; k++) Step(w, d, true, (m, behind));
            Assert.False(w.InHerLight(m));
            Assert.True(Vector2.Distance(her, w.BeamEnd) < Vector2.Distance(her, behind));
            return;
        }
    }

    [Fact]
    public void The_beam_held_on_a_sealed_walls_carving_fills_it_with_light()
    {
        foreach (var id in TestLevels.All(p => p.Secrets > 0 && !p.HasBoss).Take(12))
        {
            var w = World(TestLevels.Get(id));
            foreach (var seal in w.Seals.Where(s => s.Kind == SealKind.Secret && !s.Open && !s.Rock.Fake))
            {
                var her = seal.Emblem + seal.Facing * 3f;
                if (!w.Clear(her, 0.6f)) continue;
                for (int i = 0; i < 2 * PlaneWorld.TickRate && !seal.Open; i++)
                {
                    w.Player.Position = w.Player.PrevPosition = her;
                    w.Player.Velocity = Vector2.Zero;
                    w.Player.Hp = 100f;
                    w.Step(new PlaneInput { Aim = -seal.Facing, Beam = true });
                }
                if (seal.Hits == 0 && !seal.Open) continue;
                Assert.True(seal.Open || seal.Hits >= 2 * HerLightTuning.BeamShots - 1, $"{seal.Hits} hits");
                return;
            }
        }
        Assert.Fail("no sealed wall the beam could be held on");
    }
}
