using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her pearls on the beam (docs/LIGHT-AND-GRAPH-PROPOSAL.md §5.1).</summary>
public class BeamPearlTests
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
        return w;
    }

    /// <summary>A way from her with open water all along it out to <paramref name="far"/> metres, and back the other way too.</summary>
    static Vector2 Line(PlaneWorld w, float far)
    {
        var her = w.Player.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            bool open = true;
            for (float s = 0.5f; s <= far && open; s += 0.5f) open = w.Clear(her + d * s, 0.8f) && w.Clear(her - d * s, 0.8f);
            if (open && w.LineOfSight(her, her + d * far) && w.LineOfSight(her, her - d * far)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    static Murkling Pinned(PlaneWorld w, Vector2 at, float meter = 36f)
    {
        var m = new Murkling { Position = at, Bud = 0f, ThrowTimer = 100f };
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
        }
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(new PlaneInput { Aim = aim, Beam = beam });
    }

    [Fact]
    public void Range_and_size_lengthen_and_widen_the_beam_and_Sunbeam_makes_it_a_searchlight()
    {
        var plain = World();
        plain.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        var wide = World("whale_lung");
        wide.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.True(wide.Beam.Range > plain.Beam.Range);
        Assert.True(wide.Beam.HalfAngle > plain.Beam.HalfAngle);
        Assert.True(wide.Beam.HalfAngle <= LightTuning.MaxHalfAngleDeg * MathF.PI / 180f + 1e-4f);

        var sun = World("sunbeam");
        sun.Step(new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.Equal(plain.Beam.Range * LightTuning.SunbeamReach, sun.Beam.Range, 2);
        Assert.Equal(LightTuning.SunbeamHalfAngleDeg * MathF.PI / 180f, sun.Beam.HalfAngle, 3);
    }

    [Fact]
    public void Rear_Fin_shines_a_fainter_beam_behind_her_that_burns_as_one_shot()
    {
        var w = World("rear_fin");
        var d = Line(w, 6f);
        var at = w.Map.Start.Position - d * 5f;
        var m = Pinned(w, at);
        float full = m.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, d, true, (m, at));
        Assert.True(w.RearBeam.Shining);
        Assert.Equal(-d.X, w.RearBeam.Direction.X, 3);
        Assert.Equal(full - HerLightTuning.Burn, m.Corruption.Value, 1);
        // Without the fin, nothing shines behind her.
        var bare = World();
        bare.Step(new PlaneInput { Beam = true, Aim = d });
        Assert.False(bare.RearBeam.Shining);
    }

    [Fact]
    public void Lighthouse_turns_a_beam_round_her_even_while_her_own_is_off()
    {
        var w = World("sunbeam", "nautilus_spiral");
        Assert.True(w.Run.Loadout.Has("lighthouse"));
        w.Step(new PlaneInput());
        Assert.True(w.SweepBeam.Shining);
        var was = w.SweepBeam.Direction;
        for (int i = 0; i < PlaneWorld.TickRate; i++) w.Step(new PlaneInput());
        float turned = MathF.Acos(Math.Clamp(Vector2.Dot(was, w.SweepBeam.Direction), -1f, 1f));
        Assert.Equal(LightTuning.SweepTurn, turned, 1);
    }

    [Fact]
    public void Electric_Eel_Tail_arcs_the_beams_burn_on_to_one_nearby_at_half_the_rate()
    {
        var w = World("electric_eel_tail");
        var d = Line(w, 8f);
        var her = w.Map.Start.Position;
        // One in her beam, one 3 m to its side, out of the beam's cone.
        var litAt = her + d * 5f;
        var across = new Vector2(-d.Y, d.X);
        var besideAt = litAt + across * 3f;
        if (!w.Clear(besideAt, 0.8f) || !w.LineOfSight(litAt, besideAt) || w.BeamReaches(besideAt, 0f, 0.45f)) besideAt = litAt - across * 3f;
        var lit = Pinned(w, litAt, 200f);
        var beside = Pinned(w, besideAt, 200f);
        for (int i = 0; i < 3; i++) Step(w, d, true, (lit, litAt), (beside, besideAt));
        Assert.False(w.BeamReaches(besideAt, 0f, 0.45f) || !w.Clear(besideAt, 0.8f), "no spot beside the lit one");
        float before = beside.Corruption.Value;
        for (int i = 0; i < PlaneWorld.TickRate; i++) Step(w, d, true, (lit, litAt), (beside, besideAt));
        Assert.Equal(before - HerLightTuning.Burn * HerLightTuning.BeamShots / 2, beside.Corruption.Value, 1);
        Assert.NotEmpty(w.Arcs);
    }

    [Fact]
    public void Frost_Kelp_now_and_then_holds_what_the_beam_keeps_burning()
    {
        var w = World("frost_kelp", "frost_kelp", "frost_kelp");
        var d = Line(w, 6f);
        var at = w.Map.Start.Position + d * 4f;
        var m = Pinned(w, at, 10000f);
        int frozenLate = 0;
        for (int i = 0; i < 20 * PlaneWorld.TickRate; i++)
        {
            Step(w, d, true, (m, at));
            if (i > 2 * PlaneWorld.TickRate && w.Dazzled(m)) frozenLate++;
        }
        Assert.True(frozenLate > 0);
    }

    [Fact]
    public void Siren_Song_charms_and_Stingray_Barb_slows()
    {
        var w = World("siren_song", "siren_song", "siren_song", "stingray_barb", "stingray_barb", "stingray_barb");
        var d = Line(w, 6f);
        var at = w.Map.Start.Position + d * 4f;
        var m = Pinned(w, at, 10000f);
        bool charmed = false, slowed = false;
        for (int i = 0; i < 20 * PlaneWorld.TickRate && !(charmed && slowed); i++)
        {
            Step(w, d, true, (m, at));
            charmed |= w.Charmed(m);
            slowed |= w.Slowed(m);
        }
        Assert.True(charmed);
        Assert.True(slowed);
    }

    [Fact]
    public void The_Anglerfish_Lure_makes_a_hanging_shot_a_decoy_brighter_than_her()
    {
        var w = World("anglerfish_lure");
        var her = w.Map.Start.Position;
        var shot = new PlaneShot { FromPlayer = true, Damage = PlaneCombatTuning.ShotDamage, Life = 100f, Range = 100f, Hover = 100f, Rest = 1f, Position = her };
        w.Shots.Add(shot);
        w.Step(default);
        var plain = World();
        var other = new PlaneShot { FromPlayer = true, Damage = PlaneCombatTuning.ShotDamage, Life = 100f, Range = 100f, Hover = 100f, Rest = 1f, Position = her };
        plain.Shots.Add(other);
        plain.Step(default);
        var lure = w.LightOf(shot)!;
        var bare = plain.LightOf(other)!;
        Assert.Equal(bare.Intensity * LightTuning.LureBright, lure.Intensity, 2);
        Assert.True(lure.Intensity > w.Aura.Intensity);
    }
}
