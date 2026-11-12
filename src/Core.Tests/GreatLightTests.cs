using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her great lights (docs/LIGHT-AND-GRAPH.md §16, PlaneGreatLights.cs).</summary>
public class GreatLightTests
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
        w.Shells.Clear();
        return w;
    }

    /// <summary>Three pearls of a tag (a lantern form's), none of them shaping her beam.</summary>
    static string[] Three(string tag) =>
        Catalog.Value.Items.Where(i => i.Tags.Contains(tag) && i.Shot is null && i.Kind != ItemKind.Active).Take(3).Select(i => i.Id).ToArray();

    static Vector2 Line(PlaneWorld w, float far)
    {
        var her = w.Player.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            bool open = true;
            for (float s = 0.5f; s <= far && open; s += 0.5f) open = w.Clear(her + d * s, 0.8f);
            if (open && w.LineOfSight(her, her + d * far)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    static Murkling Pinned(PlaneWorld w, Vector2 at, float meter = 100000f)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f, Kind = MurkKind.Thrower };
        m.Corruption.Max = m.Corruption.Value = meter;
        w.Murklings.Add(m);
        return m;
    }

    static void Step(PlaneWorld w, Vector2 aim, bool beam, params (Murkling M, Vector2 At)[] pinned)
    {
        foreach (var (m, at) in pinned)
        {
            m.Position = at;
            m.Velocity = Vector2.Zero;
            m.ThrowTimer = 100f;
            m.Feeding = null;
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Step(new PlaneInput { Aim = aim, Beam = beam });
    }

    [Fact]
    public void Sunbeam_is_a_lance_long_narrow_and_carrying_its_light_to_its_end()
    {
        var plain = World();
        Step(plain, Vector2.UnitX, true);
        var lance = World("sunbeam");
        Step(lance, Vector2.UnitX, true);
        Assert.Equal(plain.Beam.Range * GreatLightTuning.LanceReach, lance.Beam.Range, 2);
        Assert.Equal(plain.Beam.HalfAngle * GreatLightTuning.LanceWidth, lance.Beam.HalfAngle, 3);
        Assert.Equal(LightTuning.MostCarry, lance.BeamFalloff(lance.Beam, lance.Beam.Range), 3);
        Assert.Equal(plain.BeamCarry, plain.BeamFalloff(plain.Beam, plain.Beam.Range), 3);
    }

    [Fact]
    public void Nautilus_Spiral_turns_a_faint_beam_round_her_and_Lighthouse_makes_it_a_full_lance()
    {
        var orbit = World("nautilus_spiral");
        Step(orbit, Vector2.UnitX, false);
        Assert.True(orbit.SweepBeam.Shining);
        Assert.False(orbit.Run.Loadout.Has("lighthouse"));
        Assert.Equal(orbit.Beam.Range * GreatLightTuning.OrbitReach, orbit.SweepBeam.Range, 2);
        Assert.Equal(GreatLightTuning.OrbitShots, orbit.HerBeams.First(b => b.Beam == orbit.SweepBeam).Shots, 3);

        var house = World("nautilus_spiral", "sunbeam");
        Step(house, Vector2.UnitX, false);
        Assert.True(house.Run.Loadout.Has("lighthouse"));
        Assert.True(house.SweepBeam.Shining);
        Assert.Equal(house.Beam.Range, house.SweepBeam.Range, 2);
        Assert.Equal(house.Beam.HalfAngle, house.SweepBeam.HalfAngle, 4);
        Assert.Equal(LightTuning.BeamIntensity, house.SweepBeam.Intensity, 3);
        Assert.Equal((float)HerLightTuning.BeamShots, house.HerBeams.First(b => b.Beam == house.SweepBeam).Shots, 3);
        Assert.Equal(LightTuning.MostCarry, house.BeamFalloff(house.SweepBeam, house.SweepBeam.Range), 3);
    }

    [Fact]
    public void Neon_Rave_turns_her_light_through_all_seven_colours()
    {
        var w = World(Three("glow"));
        Assert.True(w.Run.Loadout.Has("neon_rave"));
        var seen = new System.Collections.Generic.HashSet<LightHue>();
        for (int i = 0; i < (int)(GreatLightTuning.NeonEvery * 7 * PlaneWorld.TickRate) + 2; i++)
        {
            Step(w, Vector2.UnitX, true);
            seen.Add(w.HerHue);
            Assert.Equal(w.HerHue, w.RayHue(w.Beam));
        }
        Assert.Equal(7, seen.Count);
        Assert.DoesNotContain(LightHue.None, seen);
    }

    [Fact]
    public void Coral_Reef_lingers_and_standing_in_her_own_afterlight_mends_her()
    {
        var w = World(Three("coral"));
        Assert.True(w.Run.Loadout.Has("coral_reef"));
        Assert.Contains("linger", w.Run.Loadout.Flags);
        w.Player.Hp = 50f;
        Step(w, Vector2.UnitX, false);
        float before = w.Player.Hp;
        w.Pools.Add(new LingerPool { At = w.Map.Start.Position, Life = 5f, Worth = 0.1f });
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, Vector2.UnitX, false);
        Assert.True(w.Mending);
        Assert.Equal(before + GreatLightTuning.ReefMend, w.Player.Hp, 0);
    }

    [Fact]
    public void Shark_Mode_narrows_its_beam_and_what_it_frees_sends_her_into_a_frenzy()
    {
        var plain = World();
        Step(plain, Vector2.UnitX, true);
        var w = World(Three("predator"));
        Assert.True(w.Run.Loadout.Has("shark_mode"));
        var d = Line(w, 5f);
        Step(w, d, true);
        Assert.True(w.Beam.HalfAngle < plain.Beam.HalfAngle);
        Assert.Equal(0f, w.FrenzyLeft);
        float calm = w.HerBeams.First().Shots;
        var at = w.Map.Start.Position + d * 3f;
        var m = Pinned(w, at, 1f);
        for (int i = 0; i < PlaneWorld.TickRate && m.Alive; i++) Step(w, d, true, (m, at));
        Assert.False(m.Alive);
        Assert.True(w.FrenzyLeft > GreatLightTuning.FrenzyTime - 1f);
        Assert.Equal(calm * GreatLightTuning.FrenzyBurn, w.HerBeams.First().Shots, 2);
    }

    [Fact]
    public void Pirate_gilds_her_light_shells_in_it_fly_to_her_and_what_it_frees_drops_more()
    {
        var w = World(Three("galleon"));
        Assert.True(w.Run.Loadout.Has("pirate"));
        var d = Line(w, 9f);
        var her = w.Map.Start.Position;
        // A shell far beyond her magnet, in her beam: it flies to her.
        var shell = new PlaneShell { Position = her + d * 7f };
        w.Shells.Add(shell);
        Step(w, d, true);
        Assert.True(Vector2.Dot(shell.Velocity, -d) > 1f);
        // Without Pirate it lies there.
        var plain = World();
        var lying = new PlaneShell { Position = her + d * 7f };
        plain.Shells.Add(lying);
        Step(plain, d, true);
        Assert.Equal(Vector2.Zero, lying.Velocity);

        // What it frees drops more.
        int Freed(PlaneWorld world)
        {
            world.Shells.Clear();
            var at = her + d * 3f;
            var m = Pinned(world, at, 1f);
            for (int i = 0; i < PlaneWorld.TickRate && m.Alive; i++) Step(world, d, true, (m, at));
            Assert.False(m.Alive);
            return world.Shells.Count;
        }
        Assert.True(Freed(World(Three("galleon"))) >= Freed(World()) + GreatLightTuning.GildShells);
    }

    [Fact]
    public void Black_Sun_burns_what_stands_in_a_creatures_shadow_down_her_beam()
    {
        foreach (bool black in new[] { false, true })
        {
            var w = black ? World("black_sun") : World();
            var d = Line(w, 7f);
            var her = w.Map.Start.Position;
            var front = Pinned(w, her + d * 2.5f);
            var back = Pinned(w, her + d * 5f);
            for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, d, true, (front, her + d * 2.5f), (back, her + d * 5f));
            Assert.True(front.Corruption.Value < 100000f);
            if (black) Assert.True(back.Corruption.Value < 100000f, "the shadow burns it");
            else Assert.Equal(100000f, back.Corruption.Value, 1);
        }
    }

    [Fact]
    public void The_pause_menu_reads_her_light_back_in_words_and_her_beam_as_it_plays()
    {
        var w = World("sunbeam", "nautilus_spiral", "mirror_scale", "fire_coral", "frost_kelp", "pearl_diver", "whale_lung");
        Step(w, Vector2.UnitX, true);
        var words = PlaneStatSheet.Words(w.Run).ToDictionary(x => x.Kind, x => x.Words);
        Assert.Contains("lance", words["Shape"]);
        Assert.Contains("a second lance turning round her", words["Shape"]);
        Assert.Equal("bounce", words["Paths"]);
        Assert.Equal("ember + frost (shatter)", words["Colour"]);
        Assert.Equal("focus", words["Rhythm"]);
        Assert.Contains("Lighthouse", words["Named"]);
        Assert.Contains("Shatter", words["Named"]);
        Assert.False(words.ContainsKey("Echo"));
        Assert.Equal(w.Beam.Range, PlaneStatSheet.BeamReach(w.Run), 2);
        Assert.Equal(w.Beam.HalfAngle / w.FocusWidth * 2f * 180f / MathF.PI, PlaneStatSheet.BeamWidthDeg(w.Run), 1);
        Assert.Equal("one beam", PlaneStatSheet.Words(World().Run).Single().Words);
    }

    [Fact]
    public void Molten_Carapace_under_half_health_burns_hotter_and_runs_on_through_creatures()
    {
        foreach (bool low in new[] { false, true })
        {
            var w = World("molten_carapace");
            w.Player.Hp = low ? w.Run.MaxHp * 0.4f : w.Run.MaxHp;
            var d = Line(w, 7f);
            var her = w.Map.Start.Position;
            var front = Pinned(w, her + d * 2.5f);
            var back = Pinned(w, her + d * 5f);
            for (int i = 0; i < PlaneWorld.TickRate / 2; i++) Step(w, d, true, (front, her + d * 2.5f), (back, her + d * 5f));
            if (low) Assert.True(back.Corruption.Value < 100000f, "her light runs on through the front one");
            else Assert.Equal(100000f, back.Corruption.Value, 1);
        }
    }
}
