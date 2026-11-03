using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The adventure (docs/ADVENTURE.md): leaks, ink dots, fish that turn, the three murklings, her inner light.</summary>
public class AdventureTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions Adventure = new() { Adventure = true };

    static PlaneWorld World(bool boss = false)
    {
        var plan = TestLevels.Find(p => p.Id.Depth == 2 && p.HasBoss == boss);
        return new PlaneWorld(TestLevels.Get(plan), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
    }

    static void Run(PlaneWorld w, int ticks, PlaneInput input = default)
    {
        for (int i = 0; i < ticks; i++)
        {
            w.Player.Hp = 100f;
            w.Step(input);
        }
    }

    /// <summary>A world with its fish, anemones, leaks and starting ink taken away (each test brings what it needs).</summary>
    static PlaneWorld Bare()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Anemones.Clear();
        w.Schools.Clear();
        foreach (var v in w.Leaks) v.Sealed = true;
        NoInk(w);
        return w;
    }

    /// <summary>She stays near a leak for so long, held there among its arms.</summary>
    static void Shine(PlaneWorld w, PlaneLeak v, float seconds)
    {
        var at = v.Position + new Vector2(0f, AdventureTuning.LeakSealReach - 1f);
        at = new[] { 1f, -1f }.Select(k => v.Position + new Vector2(0f, k * (AdventureTuning.LeakSealReach - 1f))).Concat(new[] { 1f, -1f }.Select(k => v.Position + new Vector2(k * (AdventureTuning.LeakSealReach - 1f), 0f))).First(q => w.Map.IsOpen(q));
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
        }
    }

    static void NoInk(PlaneWorld w)
    {
        foreach (int i in w.Dots!.Live.ToArray()) w.Dots.Remove(i);
    }

    /// <summary>Open water round a point: every cell of a ring of dots there may hold one.</summary>
    static Vector2 OpenSpot(PlaneWorld w, float awayFromHer = 15f)
    {
        var dots = w.Dots!;
        for (int i = 0; i < InkDots.N * InkDots.N; i += 37)
        {
            var c = InkDots.Centre(i);
            if (Vector2.Distance(c, w.Player.Position) < awayFromHer || !w.Clear(c, 2.5f)) continue;
            bool all = true;
            for (int dy = -2; dy <= 2 && all; dy++)
            for (int dx = -2; dx <= 2 && all; dx++)
                all = dots.Open[i + dx + dy * InkDots.N];
            if (all) return c;
        }
        throw new InvalidOperationException("no open water");
    }

    [Fact]
    public void A_level_starts_with_leaks_fish_and_no_creatures()
    {
        foreach (bool boss in new[] { false, true })
        {
            var w = World(boss);
            Assert.Equal(AdventureTuning.LevelLeaks, w.Leaks.Count);
            Assert.False(w.ExitOpen);
            Assert.Empty(w.Murklings);
            Assert.Empty(w.Blightroots);
            Assert.Empty(w.Blotters);
            Assert.Empty(w.Lashjaws);
            Assert.Empty(w.Thumpsquirts);
            Assert.Empty(w.Inkwardens);
            // A boss level holds its boss in its arena (Drippus Prime), and nothing else of the war.
            Assert.Equal(boss, w.Boss is not null);
            Assert.True(w.ReefFish.Count > 60, $"only {w.ReefFish.Count} fish");
            Assert.True(w.Dots!.Count > 0);
            // Every leak floats in open water, clear of the others.
            foreach (var v in w.Leaks)
            {
                Assert.True(w.Clear(v.Position, 0.3f));
                Assert.True(Vector2.Distance(v.Position, w.Map.Start.Position) >= AdventureTuning.LeakFromStart);
            }
        }
    }

    [Fact]
    public void An_abyss_reef_drops_its_floor_and_its_anemones_grow_on_shelves()
    {
        var id = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        // (Its own copy: the abyss changes the map.)
        var map = AbyssReef.Apply(OctoShoots.Core.Gen.TopDown.TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id));
        Assert.True(map.Abyss);
        Assert.NotEmpty(map.Shelves);
        Assert.All(map.Shelves, s => Assert.True(map.HeightAt(s) >= AbyssReef.ShelfDepth - 0.05f && map.IsOpen(s)));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.NotEmpty(w.Anemones);
        Assert.All(w.Anemones, a =>
        {
            Assert.True(map.HeightAt(a.Position) >= AbyssReef.ShelfDepth - 0.5f, $"an anemone over the abyss at {a.Position}");
            Assert.Contains(map.Shelves, s => Vector2.Distance(s, a.Position) < 1.5f);
        });
        // Away from the walls, shelves, caves and tunnels, the floor lies at the menace's depth (give or take its dunes).
        float depth = AbyssReef.DepthOf(map.Menace);
        Assert.Contains(Enumerable.Range(0, 140 * 140), i => map.IsOpen(new Vector2(i % 140, i / 140)) && MathF.Abs(map.HeightAt(new Vector2(i % 140, i / 140)) - depth) < AbyssReef.DuneHeight);
    }

    [Fact]
    public void The_dropped_floor_sinks_with_the_menace_and_its_shallows_have_dunes()
    {
        Assert.Equal(AbyssReef.ShallowDepth, AbyssReef.DepthOf(0f));
        Assert.Equal(AbyssReef.DeepDepth, AbyssReef.DepthOf(1f));
        Assert.Equal(AbyssReef.DeepDepth, AbyssReef.DepthOf(2.4f));
        Assert.True(AbyssReef.DepthOf(0.4f) < AbyssReef.DepthOf(0.2f));
        var id = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        var map = AbyssReef.Apply(OctoShoots.Core.Gen.TopDown.TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id));
        // The open floor rolls: its heights spread across most of the dunes' height, and never rise to her plane.
        var floor = Enumerable.Range(0, 140 * 140).Select(i => new Vector2(i % 140, i / 140))
            .Where(p => map.IsOpen(p) && map.HeightAt(p) < AbyssReef.ShallowDepth + AbyssReef.DuneHeight).Select(map.HeightAt).ToList();
        Assert.NotEmpty(floor);
        Assert.True(floor.Max() - floor.Min() > AbyssReef.DuneHeight * 0.6f, $"the dunes span {floor.Max() - floor.Min():0.00} m");
        Assert.True(floor.Max() < -2f);
    }

    [Fact]
    public void Ink_cleared_comes_back_from_its_leak_and_sealing_it_dissolves_its_ink()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Anemones.Clear();
        var dots = w.Dots!;
        var v = w.Leaks[0];
        bool Own(int i) => dots.Source[i] == 0;
        int before = dots.Live.Count(Own);
        Assert.True(before > 10);
        foreach (int i in dots.Live.Where(Own).ToArray()) dots.Remove(i);
        Assert.Equal(0, dots.Live.Count(Own));
        // Nothing for a moment, then it flows back, nearest the leak first.
        Run(w, 12);
        Assert.Equal(0, dots.Live.Count(Own));
        Run(w, 2 * PlaneWorld.TickRate);
        var back = dots.Live.Where(Own).ToList();
        Assert.NotEmpty(back);
        Assert.All(back, i => Assert.True(dots.Parent[i] < 0 || dots.Has(dots.Parent[i]), "out from the leak"));
        Run(w, 14 * PlaneWorld.TickRate);
        Assert.True(dots.Live.Count(Own) >= before * 0.95f, "it all comes back");
        int others = dots.Count - dots.Live.Count(Own);
        // Sealed, its ink goes for good; the other leaks' stays.
        w.DebugSealLeak(v);
        Run(w, 5 * PlaneWorld.TickRate);
        Assert.Equal(0, dots.Live.Count(Own));
        Assert.True(dots.Count >= others * 0.9f);
        Run(w, 3 * PlaneWorld.TickRate);
        Assert.Equal(0, dots.Live.Count(Own));
    }

    [Fact]
    public void Only_a_leak_heals()
    {
        var meter = new CorruptionMeter(36f);
        meter.Hit(10f);
        for (int i = 0; i < 600; i++) meter.Step(PlaneWorld.Dt);
        Assert.Equal(26f, meter.Value);
    }

    [Fact]
    public void Staying_near_staples_its_arms_one_after_another_and_left_alone_the_staples_snap()
    {
        var w = World();
        w.ReefFish.Clear();
        var v = w.Leaks[0];
        Assert.Equal(AdventureTuning.LeakArms, v.Arms.Length);
        Shine(w, v, v.Needed * 0.5f);
        int staples = v.Staples;
        Assert.InRange(staples, v.StapleCount / 2 - 2, v.StapleCount / 2 + 1);
        // Arm after arm, from the disc out: the first arms all stapled, the rest none (but the one under way).
        Assert.Equal(v.StaplesPerArm, v.StapledOn(0));
        Assert.Equal(0, v.StapledOn(v.Arms.Length - 1));
        Assert.False(v.Sealed);
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        // She stops: after a grace, a staple snaps out every StapleDrain seconds.
        int snapped = 0;
        for (int i = 0; i < (AdventureTuning.LeakHold + AdventureTuning.StapleDrain * 2.5f) * PlaneWorld.TickRate; i++)
        {
            Run(w, 1);
            snapped += w.Events.Count(e => e.Type == PlaneEventType.LeakUntied);
        }
        Assert.InRange(snapped, 2, 3);
        Assert.True(v.Staples < staples);
    }

    [Fact]
    public void Its_arms_grow_with_menace_and_longer_arms_take_more_staples_and_longer()
    {
        Assert.Equal(AdventureTuning.LeakArmBase, AdventureTuning.LeakArmLength(0f), 3);
        Assert.True(AdventureTuning.LeakArmLength(1f) > AdventureTuning.LeakArmLength(0.4f));
        Assert.Equal(AdventureTuning.LeakArmLength(1f), AdventureTuning.LeakArmLength(3f), 3);
        Assert.True(AdventureTuning.StaplesPerArm(AdventureTuning.LeakArmLength(1f)) > AdventureTuning.StaplesPerArm(AdventureTuning.LeakArmLength(0f)));
        var w = World();
        var v = w.Leaks[0];
        Assert.Equal(AdventureTuning.StaplesPerArm(AdventureTuning.LeakArmLength(w.Menace)), v.StaplesPerArm);
        Assert.Equal(AdventureTuning.StapleSeconds * v.StapleCount, v.Needed, 3);
        // Each arm reaches out its length from the disc's rim.
        foreach (var arm in v.Arms)
            Assert.InRange(Vector2.Distance(arm.Points[0], arm.Points[^1]), arm.Length * 0.7f, arm.Length + 0.01f);
    }

    [Fact]
    public void Its_arms_wiggle_until_stapled_and_sting_her_where_they_are_free()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Murklings.Clear();
        var v = w.Leaks[0];
        var arm = v.Arms[0];
        var tip = arm.Points[^1];
        Run(w, PlaneWorld.TickRate);
        Assert.True(Vector2.Distance(tip, arm.Points[^1]) > 0.01f, "a free arm drifts");
        // Touching a free stretch: she is stung and thrown off.
        var on = (arm.Points[^2] + arm.Points[^1]) * 0.5f;
        w.Player.Position = w.Player.PrevPosition = on;
        w.Player.Hp = 100f;
        w.Step(default);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.PlayerHit && e.Source == DamageSource.Leak);
        Assert.True(w.Player.Hp < 100f);

        // Its first arm stapled all along: it holds still, and touching it does nothing.
        v.Light = v.StaplesPerArm * AdventureTuning.StapleSeconds + 0.01f;
        v.Since = 0f;
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        Run(w, 1);
        var held = arm.Points.ToArray();
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            v.Since = 0f;
            Run(w, 1);
        }
        Assert.All(held.Zip(arm.Points), q => Assert.True(Vector2.Distance(q.First, q.Second) < 1e-4f));
        // (Past her grace after the sting, her light kept on it so it does not heal.)
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++)
        {
            v.Since = 0f;
            Run(w, 1);
        }
        w.Player.Position = w.Player.PrevPosition = (arm.Points[^2] + arm.Points[^1]) * 0.5f;
        w.Player.Hp = 100f;
        v.Since = 0f;
        w.Step(default);
        Assert.DoesNotContain(w.Events, e => e.Type == PlaneEventType.PlayerHit && e.Source == DamageSource.Leak);
    }

    [Fact]
    public void A_shot_that_meets_a_free_arm_pops_on_it_and_bends_it_away()
    {
        var w = World();
        w.ReefFish.Clear();
        w.Murklings.Clear();
        var v = w.Leaks[0];
        var arm = v.Arms[2];
        var mid = (arm.Points[^3] + arm.Points[^2]) * 0.5f;
        var across = Vector2.Normalize(new Vector2(-(arm.Points[^2] - arm.Points[^3]).Y, (arm.Points[^2] - arm.Points[^3]).X));
        var from = mid + across * 1.2f;
        var shot = new PlaneShot { Position = from, Line = from, Velocity = -across * 20f, Life = 1f, FromPlayer = true, Damage = 10f, Range = 10f, Hover = 1f };
        w.Shots.Add(shot);
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        for (int i = 0; i < 10 && shot.Life > 0f; i++) Run(w, 1);
        Assert.True(shot.Life <= 0f, "it popped on the arm");
        Assert.NotEqual(0f, arm.KickSpeed + arm.Kick);
    }

    [Fact]
    public void Leaks_float_in_the_middle_of_chambers_where_they_can()
    {
        var w = World();
        Assert.Contains(w.Leaks, v => w.Map.Plazas.Any(z => Vector2.Distance(z.Center, v.Position) < 0.01f));
    }

    [Fact]
    public void Every_level_starts_part_inked_out_of_its_leaks_the_more_the_deeper()
    {
        PlaneWorld At(int depth) => new(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == depth && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.Equal(0.15f, AdventureTuning.StartInk(0f), 3);
        Assert.Equal(0.95f, AdventureTuning.StartInk(1f), 3);
        Assert.Equal(0.95f, AdventureTuning.StartInk(3f), 3);
        float last = 0f;
        foreach (int depth in new[] { 2, 4, 6 })
        {
            var w = At(depth);
            float share = w.Dots!.Count / (float)w.Dots.Open.Count(o => o);
            float want = AdventureTuning.StartInk(w.Menace);
            Assert.True(share <= want + 0.01f && share >= want - 0.02f, $"depth {depth}: {w.Dots.Count} of {w.Dots.Open.Count(o => o)} water, {share:P0} for {want:P0}");
            Assert.True(share > last);
            last = share;
            // All of it belongs to a leak.
            Assert.All(w.Dots.Live, i => Assert.True(w.Dots.Source[i] >= 0));
        }
    }

    [Fact]
    public void A_boss_levels_way_down_opens_once_its_boss_is_freed_its_rise_shutting_any_vent_left()
    {
        foreach (bool ventsFirst in new[] { true, false })
        {
            var w = World(boss: true);
            var boss = w.Boss!;
            Assert.Equal(OctoShoots.Core.Bosses.BossConcept.TheBrood, boss.Spec.Concept);
            if (ventsFirst)
            {
                foreach (var v in w.Leaks) w.DebugSealLeak(v);
                Assert.False(w.ExitOpen);
            }
            // In she goes; it rises, drinking the leaks left dry, and fights; it is freed.
            w.Player.Position = w.Player.PrevPosition = w.ArenaCenter + new Vector2(0f, w.ArenaRadius * 0.55f);
            for (int i = 0; i < 6 * PlaneWorld.TickRate && boss.Stage != BossStage.Fight; i++) Run(w, 1);
            Assert.Equal(BossStage.Fight, boss.Stage);
            w.DebugFreeBoss();
            Run(w, PlaneWorld.TickRate);
            Assert.False(w.BossBattle);
            Assert.True(w.ExitOpen);
        }
    }

    [Fact]
    public void Every_level_has_five_leaks_and_three_sealed_open_the_way_on()
    {
        foreach (var id in TestLevels.All(_ => true))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
            Assert.True(w.Leaks.Count == AdventureTuning.LevelLeaks, $"{id}: {w.Leaks.Count} leaks");
        }
        var plain = World();
        for (int i = 0; i < AdventureTuning.LeaksToClear; i++)
        {
            Assert.False(plain.ExitOpen);
            plain.DebugSealLeak();
        }
        Assert.True(plain.ExitOpen);
        Assert.Equal(AdventureTuning.LevelLeaks - AdventureTuning.LeaksToClear, plain.LeaksOpen);
    }

    [Fact]
    public void A_volley_shares_her_light()
    {
        Assert.Equal(1f, PlaneCombatTuning.VolleyPower(1), 3);
        Assert.Equal(1.62f, PlaneCombatTuning.VolleyPower(5), 2);
        Assert.Equal(PlaneCombatTuning.VolleyPower(5), PlaneCombatTuning.VolleyShot(5) * 5f, 3);
    }

    [Fact]
    public void Each_vent_sealed_throws_shells_out_of_its_mouth()
    {
        var w = World();
        foreach (var v in w.Leaks.ToList())
        {
            int before = w.Shells.Count;
            w.DebugSealLeak(v);
            Assert.Equal(before + AdventureTuning.LeakShells, w.Shells.Count);
        }
    }

    [Fact]
    public void A_leak_seals_in_her_light_with_no_shots_a_staple_at_a_time()
    {
        var w = World();
        var v = w.Leaks[0];
        Assert.Equal(AdventureTuning.StapleSeconds * v.StapleCount, v.Needed, 3);
        Shine(w, v, v.Needed * 0.8f);
        Assert.False(v.Sealed);
        float lit = v.Light;
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        Run(w, 3 * PlaneWorld.TickRate);
        Assert.True(v.Light < lit, "it heals once she leaves");
        Shine(w, v, v.Needed + 0.5f);
        Assert.True(v.Sealed);
        Assert.False(w.ExitOpen);
        foreach (var o in w.Leaks.Skip(1)) w.DebugSealLeak(o);
        Assert.True(w.ExitOpen);
    }

    [Fact]
    public void Her_light_clears_the_dots_she_swims_through()
    {
        var w = Bare();
        var at = OpenSpot(w);
        foreach (var d in new[] { Vector2.Zero, Vector2.UnitX * AdventureTuning.Spacing, -Vector2.UnitX * AdventureTuning.Spacing, Vector2.UnitY * AdventureTuning.Spacing })
            Assert.True(w.DebugDot(at + d));
        w.Player.Position = w.Player.PrevPosition = at;
        int cleared = 0;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            w.Step(default);
            cleared += w.Events.Count(e => e.Type == PlaneEventType.DotCleared);
        }
        Assert.Equal(4, cleared);
        Assert.Equal(0, w.Dots!.Count);
    }

    [Fact]
    public void Her_shots_clear_every_dot_along_their_flight()
    {
        var w = Bare();
        var her = OpenSpot(w);
        w.Player.Position = w.Player.PrevPosition = her;
        // A line of dots ahead of her, out past her light's reach; she fires one shot along it.
        int laid = 0;
        for (float d = 3.2f; d < 7f; d += AdventureTuning.Spacing)
            if (w.Clear(her + Vector2.UnitX * d, 0.3f) && w.DebugDot(her + Vector2.UnitX * d)) laid++;
        Assert.True(laid >= 3, $"only {laid} dots laid");
        int cleared = 0;
        w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
        cleared += w.Events.Count(e => e.Type == PlaneEventType.DotCleared);
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
            cleared += w.Events.Count(e => e.Type == PlaneEventType.DotCleared);
        }
        Assert.Equal(laid, cleared);
        Assert.Equal(0, w.Dots!.Count);
    }

    [Fact]
    public void A_fish_in_the_ink_fills_and_turns_into_its_murkling()
    {
        foreach (var kind in new[] { ReefFishKind.BlueTang, ReefFishKind.Clownfish })
        {
            var w = World();
            var fish = w.ReefFish.First(f => f.Kind == kind);
            foreach (var o in w.ReefFish.Where(o => o != fish)) o.Freed = true;
            foreach (var v in w.Leaks) v.Sealed = true;
            // Ink all round it, kept up as it eats.
            for (int t = 0; t < 30 * PlaneWorld.TickRate && !fish.Corrupted; t++)
            {
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    w.DebugDot(fish.Position + new Vector2(dx, dy) * AdventureTuning.Spacing);
                w.Player.Hp = 100f;
                w.Step(default);
            }
            Assert.True(fish.Corrupted, $"the {kind} never turned");
            var m = w.Murklings.Single(x => x.Fish == fish);
            Assert.Equal(kind == ReefFishKind.BlueTang ? AdventureTuning.SpikedRadius : AdventureTuning.TadpoleRadius, m.Radius);
        }
    }

    [Fact]
    public void A_fish_eats_the_dots_it_touches()
    {
        var w = World();
        foreach (var v in w.Leaks) v.Sealed = true;
        NoInk(w);
        var fish = w.ReefFish.First(f => f.Kind == ReefFishKind.BlueTang);
        fish.Freed = true;
        w.Player.Position = w.Map.Start.Position;
        w.DebugDot(fish.Position);
        int eaten = 0;
        for (int i = 0; i < 4 && eaten == 0; i++)
        {
            w.Step(default);
            eaten += w.Events.Count(e => e.Type == PlaneEventType.DotEaten);
        }
        Assert.Equal(1, eaten);
    }

    [Fact]
    public void A_school_turns_together_and_its_shoal_circles_her_taking_her_shots()
    {
        var w = World();
        foreach (var v in w.Leaks) v.Sealed = true;
        var school = w.ReefFish.First(f => f.Kind == ReefFishKind.Chromis).Host;
        var members = w.ReefFish.Where(f => f.Kind == ReefFishKind.Chromis && f.Host == school).ToList();
        members[0].Ink = 1f;
        // (Full: the next step sets it turning, with all its school.)
        Run(w, 2);
        Assert.All(members, f => Assert.True(f.Turning > 0f || f.Corrupted));
        Run(w, PlaneWorld.TickRate);
        var shoal = w.Murklings.Where(m => m.Swarm == school).ToList();
        Assert.Equal(members.Count, shoal.Count);
        // (Her light's burn and dazzle off: the shoal on its own.)
        w.LightBurns = false;

        // She stops beside them; they ring her all round, and the ring circles her (whichever way she aims).
        w.Player.Position = w.Player.PrevPosition = shoal[0].Position;
        var her = w.Player.Position;
        float[] Angles() => shoal.Select(m => MathF.Atan2(m.Position.Y - her.Y, m.Position.X - her.X)).ToArray();
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
        }
        var before = Angles();
        Assert.All(shoal, m => Assert.InRange(Vector2.Distance(m.Position, her), 0.5f, 4f));
        Assert.All(new[] { 0, 1, 2, 3 }, q => Assert.Contains(before, ang => (int)MathF.Floor((ang + MathF.PI) / (MathF.PI / 2f)) % 4 == q));
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
        }
        var after = Angles();
        float moved = 0f;
        for (int i = 0; i < shoal.Count; i++) moved += MathF.Abs(MathF.IEEERemainder(after[i] - before[i], MathF.Tau));
        Assert.True(moved / shoal.Count > 0.3f, $"the ring barely turned ({moved / shoal.Count:0.00} rad each in half a second)");
        Assert.Equal(0, w.LesserClingers);

        // Her shot meets one of them.
        int struck = 0;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && struck == 0; i++)
        {
            w.Player.Position = her;
            w.Step(new PlaneInput { Aim = Vector2.UnitX, Fire = true });
            struck += w.Events.Count(e => e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated);
        }
        Assert.True(struck > 0, "the shoal should take her shots");

        // Freed one by one, the rest hold their places: a gap is left, and no one moves up.
        var left = shoal.Where(m => m.Alive).ToList();
        var pair = left.GroupBy(m => m.ShoalSlot % 3).First(g => g.Count() >= 2).Take(2).ToArray();
        float Apart() => MathF.IEEERemainder(MathF.Atan2(pair[1].Position.Y - her.Y, pair[1].Position.X - her.X) - MathF.Atan2(pair[0].Position.Y - her.Y, pair[0].Position.X - her.X), MathF.Tau);
        float was = Apart();
        var slots = left.ToDictionary(m => m, m => m.ShoalSlot);
        left.First(m => !pair.Contains(m)).Alive = false;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(new PlaneInput { Aim = Vector2.UnitX });
        }
        Assert.All(left, m => Assert.Equal(slots[m], m.ShoalSlot));
        Assert.InRange(MathF.Abs(MathF.IEEERemainder(Apart() - was, MathF.Tau)), 0f, 0.2f);
    }

    [Fact]
    public void The_tadpole_chases_her_faster_than_she_cruises_and_clings()
    {
        var w = World();
        foreach (var v in w.Leaks) v.Sealed = true;
        var fish = w.ReefFish.First(f => f.Kind == ReefFishKind.Clownfish);
        fish.Ink = 1f;
        Run(w, PlaneWorld.TickRate);
        var m = w.Murklings.Single(x => x.Fish == fish);
        // (Her light's burn and dazzle off: the chase on its own.)
        w.LightBurns = false;
        w.Player.Position = w.Player.PrevPosition = m.Position + Vector2.Normalize(new Vector2(1f, 0.3f)) * 6f;
        if (!w.Clear(w.Player.Position, w.Radius)) w.Player.Position = w.Player.PrevPosition = m.Position + new Vector2(0f, 0.01f) + Vector2.UnitY * 3f;
        for (int i = 0; i < 4 * PlaneWorld.TickRate && !m.Clinging; i++)
        {
            w.Player.Hp = 100f;
            w.Player.Velocity = Vector2.Zero;
            w.Step(default);
        }
        Assert.True(m.Clinging, "it should have caught her and clung");
    }

    [Fact]
    public void A_pufferfish_in_the_ink_becomes_a_spiky_inkwarden_that_fires_back_wards_and_is_freed_a_pufferfish()
    {
        var w = World();
        foreach (var v in w.Leaks) v.Sealed = true;
        w.ReefFish.Clear();
        Assert.InRange(w.Fish.Count, AdventureTuning.PufferMin / 2, AdventureTuning.PufferMax);
        var puffer = w.Fish[0];
        puffer.Ink = 1f;
        Run(w, 1);
        var warden = w.Inkwardens.Single();
        Assert.True(warden.Puffer);
        Assert.DoesNotContain(puffer, w.Fish);
        Assert.Equal(WardenStage.Calm, warden.Stage);

        // Anemones never turn now.
        foreach (var an in w.Anemones) an.Ink = 1f;
        Run(w, 2);
        Assert.Single(w.Inkwardens);

        // It still wards the corrupted creatures round it.
        var kin = w.DebugBud(warden.Position + Vector2.UnitX * 2f, prime: false);
        kin.Bud = 0f;

        // Struck: it rolls out, spins up, fires all its spikes one by one, then settles and grows them back.
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        warden.Corruption.Hit(1f);
        int spikes = 0;
        bool rolled = false, spun = false;
        for (int i = 0; i < 6 * PlaneWorld.TickRate; i++)
        {
            w.Player.Hp = 100f;
            kin.Position = warden.Position + Vector2.UnitX * 2f;
            w.Step(default);
            spikes += w.Events.Count(e => e.Type == PlaneEventType.WardenSpike);
            rolled |= warden.Stage == WardenStage.Rolling;
            spun |= warden.SpinRate > WardenTuning.SpinFast * 0.9f;
        }
        Assert.True(rolled && spun);
        Assert.Equal(WardenTuning.Spikes, spikes);
        Assert.Equal(WardenStage.Calm, warden.Stage);
        Assert.Equal(WardenTuning.Spikes, warden.SpikesLeft);

        var light = kin.Corruption.Value;
        w.DebugBurst(kin.Position, 0.5f, 10f);
        Assert.Equal(light, kin.Corruption.Value);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.Warded);

        // Freed: a pufferfish again, swimming off (she is near: a freed one is gone once out of her sight).
        w.Player.Position = w.Player.PrevPosition = warden.Position + Vector2.UnitY * 3f;
        warden.Corruption.Value = 0.5f;
        w.DebugBurst(warden.Position, 0.5f, 10f);
        Run(w, 1);
        Assert.False(warden.Alive);
        Assert.Contains(w.Fish, f => f.Freed && Vector2.Distance(f.Position, warden.Position) < 1f);
    }

    [Fact]
    public void Each_fish_lives_fully_at_its_own_depth_and_fewer_below()
    {
        Assert.Equal(0f, AdventureTuning.Rate(FlounderTuning.Home, 1));
        Assert.Equal(1f, AdventureTuning.Rate(FlounderTuning.Home, FlounderTuning.Home));
        Assert.Equal(AdventureTuning.EarlierRate, AdventureTuning.Rate(FlounderTuning.Home, 5));
        Assert.True(AdventureTuning.EarlierRate is > 0f and < 1f);
        // The Shallows' fish live fully in the Kelp Jungle too, and fewer deeper.
        var kelp = World();
        var deep = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 4 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.InRange(kelp.Fish.Count, AdventureTuning.PufferMin, AdventureTuning.PufferMax);
        Assert.InRange(deep.Fish.Count, 1, (int)MathF.Round(AdventureTuning.PufferMax * AdventureTuning.EarlierRate));
        Assert.InRange(kelp.Flounders.Count, 1, FlounderTuning.Max);
        Assert.InRange(deep.Flounders.Count, 0, (int)MathF.Round(FlounderTuning.Max * AdventureTuning.EarlierRate));
    }

    [Fact]
    public void Flounders_lie_on_their_own_shelves()
    {
        var id = TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss);
        var map = AbyssReef.Apply(OctoShoots.Core.Gen.TopDown.TopDownGenerator.Generate(new OctoShoots.Core.Run.RunStreams(TestLevels.Seed), id));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
        Assert.InRange(w.Flounders.Count, FlounderTuning.Min, FlounderTuning.Max);
        Assert.All(w.Flounders, f =>
        {
            Assert.False(f.Corrupted);
            Assert.Contains(map.Shelves, s => Vector2.Distance(s, f.Home) < 1.5f);
            Assert.DoesNotContain(w.Anemones, a => Vector2.Distance(a.Position, f.Home) < 6f);
        });
    }

    [Fact]
    public void The_boss_wall_takes_no_light_until_every_vent_is_sealed_and_no_vent_lies_behind_it()
    {
        int walls = 0;
        foreach (var id in TestLevels.All(p => p.HasBoss && p.Id.Depth <= 4))
        {
            var w = new PlaneWorld(TestLevels.Get(id), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), Adventure);
            var seal = w.Seals.FirstOrDefault(s => s.Kind == SealKind.Boss);
            if (seal is null || w.Leaks.Count == 0) continue;
            walls++;
            var rocks = w.Seals.Where(s => s.Kind == SealKind.Boss).Select(s => s.Rock).ToList();
            var reach = LevelValidator.Distances(w.Map, w.Map.Start.Position, blocked: q => rocks.Exists(r => r.Contains(q)));
            Assert.All(w.Leaks, v => Assert.False(float.IsPositiveInfinity(LevelValidator.DistanceAt(reach, v.Position))));
            for (int k = 0; k < 60; k++) w.DebugStrikeSeal(seal);
            Assert.False(seal.Open);
            Assert.Equal(0, seal.Hits);
            Assert.Contains(w.Events, e => e.Type == PlaneEventType.SealRefused);
            for (int k = 0; k < AdventureTuning.LeaksToClear; k++) w.DebugSealLeak();
            for (int k = 0; k < 60 && !seal.Open; k++) w.DebugStrikeSeal(seal);
            Assert.True(seal.Open);
        }
        Assert.True(walls > 0, "no boss level with a wall and leaks");
    }

    [Fact]
    public void A_shot_over_a_hearthguard_lying_flat_strikes_it_where_it_shows()
    {
        var w = Bare();
        var f = w.Flounders.OrderByDescending(o => w.FlounderSeen(o).Y - o.Position.Y).First();
        w.Flounders.RemoveAll(o => o != f);
        f.Corrupted = true;
        var seen = w.FlounderSeen(f);
        Assert.True(Vector2.Distance(seen, f.Position) > FlounderTuning.Radius + PlaneCombatTuning.ShotRadius, "it shows clear of its disc");
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        void Shoot(Vector2 at) => w.Shots.Add(new PlaneShot { FromPlayer = true, Position = at, Line = at, Velocity = Vector2.UnitX * 0.1f, Damage = 1f, Life = 1f });
        // South of where it shows: nothing.
        Shoot(seen + new Vector2(0f, 1.5f));
        Run(w, 2);
        Assert.Equal(f.Corruption.Max, f.Corruption.Value);
        w.Shots.Clear();
        // Over the fish on the screen: struck.
        Shoot(seen);
        Run(w, 2);
        Assert.True(f.Corruption.Value < f.Corruption.Max);
    }

    [Fact]
    public void A_flounder_in_the_ink_turns_into_a_hearthguard_that_holds_its_patch_flings_ink_and_stabs_her_close_by()
    {
        var w = Bare();
        var f = w.Flounders[0];
        w.Flounders.RemoveAll(o => o != f);
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        // Healthy, it lies still; it eats the dots it touches, fills and turns.
        for (int i = 0; i < 2 * PlaneWorld.TickRate && !f.Corrupted; i++)
        {
            w.DebugDot(f.Home);
            Run(w, 1);
        }
        Assert.True(f.Corrupted);
        Assert.Equal(f.Home, f.Position);

        // Left alone, it shimmies now and then, flinging ink drops off its flanks that fall close and leave dots.
        int drops = 0;
        float far = 0f;
        var head = new Vector2(MathF.Cos(f.Heading), MathF.Sin(f.Heading));
        for (int i = 0; i < 10 * PlaneWorld.TickRate; i++)
        {
            int before = w.MurkShots.Count;
            Run(w, 1);
            drops += Math.Max(0, w.MurkShots.Count - before);
            foreach (var s in w.MurkShots)
            {
                far = MathF.Max(far, Vector2.Distance(s.Position, f.Home));
                Assert.True(s.Lobbed);
                Assert.InRange(MathF.Abs(Vector2.Dot(s.From - f.Home, head)), 0f, 0.01f);
            }
        }
        Assert.True(drops >= 8, $"only {drops} drops");
        Assert.InRange(far, 1f, FlounderTuning.InkReach + 1.5f);
        Assert.Equal(f.Home, f.Position);

        // She comes close: its disc draws into a needle at her, and it stabs.
        var near = f.Home + Vector2.Normalize(w.Map.Start.Position - f.Home) * 2f;
        for (int k = 0; k < 16 && !(w.Clear(near, w.Radius) && w.LineOfSight(f.Home, near)); k++)
            near = f.Home + new Vector2(MathF.Cos(k * 0.4f), MathF.Sin(k * 0.4f)) * 2f;
        float hp = 100f;
        bool wound = false, bit = false;
        for (int i = 0; i < 3 * PlaneWorld.TickRate && !bit; i++)
        {
            w.Player.Position = w.Player.PrevPosition = near;
            w.Player.Velocity = Vector2.Zero;
            w.Player.DashInvulnerableTimer = 0f;
            w.MurkShots.Clear();
            w.Player.Hp = hp;
            w.Step(default);
            wound |= f.Stage == FlounderStage.Windup && f.Needle > 0f;
            bit |= w.Events.Any(e => e.Type == PlaneEventType.FlounderBite);
        }
        Assert.True(wound && bit);
        Assert.True(w.Player.Hp < hp);
        Assert.Equal(f.Home, f.Position);

        // Far off, it never comes after her.
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        Run(w, 3 * PlaneWorld.TickRate);
        Assert.NotEqual(FlounderStage.Windup, f.Stage);
        Assert.Equal(f.Home, f.Position);

        // Freed: a flounder again, swimming off and gone.
        f.Corruption.Value = 0.5f;
        w.DebugBurst(f.Position, 0.5f, 10f);
        Run(w, 1);
        Assert.True(f.Freed);
        Assert.False(f.Corrupted);
        Run(w, PlaneWorld.TickRate * (int)ReefFishTuning.LeaveMax + 60);
        Assert.DoesNotContain(f, w.Flounders);
    }

    [Fact]
    public void An_inkwarden_guards_the_leaks_in_its_reach()
    {
        var w = World();
        var v = w.Leaks[0];
        var puffer = w.Fish[0];
        puffer.Position = v.Position + new Vector2(3f, 0f);
        puffer.Ink = 1f;
        Run(w, 1);
        var warden = w.Inkwardens.Single();
        Assert.Contains(w.GuardedBy(warden), at => at == v.Position);
        bool warded = false;
        for (int i = 0; i < 30; i++)
        {
            Shine(w, v, 1f / PlaneWorld.TickRate);
            warded |= w.Events.Exists(e => e.Type == PlaneEventType.Warded);
        }
        Assert.False(v.Sealed);
        Assert.True(v.Light < 0.3f, "her light is turned aside");
        Assert.True(warded);
        // Freed, it guards no more.
        warden.Corruption.Value = 0.5f;
        w.DebugBurst(warden.Position, 0.5f, 10f);
        Run(w, 1);
        Shine(w, v, v.Needed + 1f);
        Assert.True(v.Sealed);
    }

    [Fact]
    public void Her_oral_arm_trails_her_stings_what_it_touches_and_grows_with_her_skills()
    {
        var w = Bare();
        var her = OpenSpot(w);
        w.Player.Position = w.Player.PrevPosition = her;
        var behind = her - Vector2.UnitX * 1.4f;
        var m = w.DebugBud(behind, prime: false);
        m.Bud = 0f;
        float full = m.Corruption.Value;
        int stung = 0;
        for (int i = 0; i < PlaneWorld.TickRate; i++)
        {
            // She has just swum in from the west; the murkling lies on her way, by her arm.
            w.DebugTrail(new[] { her - Vector2.UnitX * 3f, her - Vector2.UnitX * 1.5f, her });
            m.Position = behind;
            m.Velocity = Vector2.Zero;
            w.Player.Position = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = 100f;
            w.Step(default);
            stung += w.Events.Count(e => e.Type is PlaneEventType.MobHit or PlaneEventType.MobDefeated);
        }
        // A share of a shot each ArmEvery: about two or three stings in a second.
        Assert.InRange(stung, 2, 3);
        Assert.True(m.Corruption.Value < full);
        Assert.True(w.Arm.Count >= 2);

        // Its length is her max HP, and every skill point spent adds to that.
        Assert.Equal(AdventureTuning.ArmBase, w.ArmLength, 3);
        w.Run.Points = 3;
        for (int i = 0; i < 3; i++) Assert.True(w.SpendSkill(OctoShoots.Core.Items.Stat.Damage));
        Assert.Equal(AdventureTuning.ArmLength(100f + 3 * ExperienceTuning.HpPerPoint), w.ArmLength, 3);
        Assert.True(w.ArmLength > AdventureTuning.ArmBase);
        Assert.Equal(AdventureTuning.ArmMax, AdventureTuning.ArmLength(1000f), 3);
        Assert.Equal(AdventureTuning.ArmMin, AdventureTuning.ArmLength(10f), 3);
    }

    [Fact]
    public void The_adventure_is_deterministic()
    {
        ulong Hash()
        {
            var w = World();
            for (int i = 0; i < 20 * PlaneWorld.TickRate; i++)
                w.Step(new PlaneInput { Move = new Vector2(MathF.Sin(i * 0.01f), 1f), Aim = Vector2.UnitX, Fire = i % 90 < 40 });
            ulong h = w.StateHash();
            foreach (int c in w.Dots!.Live) h = h * 31 + (ulong)c;
            return h ^ (ulong)w.Murklings.Count;
        }
        Assert.Equal(Hash(), Hash());
    }
}
