using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>
/// The bay's folk (docs/NPC-BOSSES-PROPOSAL.md): every neighbour is a boss first, waiting in her great cave; freed, she
/// goes home and opens a service in the bay.
/// </summary>
public class FolkTests
{
    static readonly Lazy<HandLayout> Haven = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "haven.json"))));

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly SeedCode Seed = SeedCode.Parse("KELP7Q2Z");

    /// <summary>A neighbour's depth's boss level (docs/DIVE.md §5), and the world on it with her waiting in its arena.</summary>
    static PlaneWorld GreatCave(string folk)
    {
        int depth = Array.FindIndex(Folk.All, n => n.Id == folk) + 1;
        var n = Folk.Get(folk)!;
        var map = TopDownGenerator.Generate(new RunStreams(Seed), new LevelId(1, LevelPlan.Tutorial + depth, LevelStocking.LevelsPerDepth));
        var options = new PlaneOptions { Adventure = true, Boss = FolkBosses.For(folk, n.Level, map.Menace) };
        return new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), options);
    }

    static void Hold(PlaneWorld w, Vector2 at, float seconds, Func<bool>? until = null)
    {
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = at;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            if (until?.Invoke() == true) return;
        }
    }

    [Fact]
    public void Tull_SweepsThenSlams_ThenSighs_HerThroatOpenOnlyInTheSigh_AndPairsHerSlamsPastHalf()
    {
        var w = GreatCave("tull");
        var boss = w.Boss!;
        Assert.Equal(BodyForm.Turtle, boss.Spec.Form);
        var her = w.ArenaCenter + new Vector2(0f, 9f);
        // Into the arena: it is barred, she lands, and the fight runs.
        Hold(w, her, 3f, () => boss.Stage == BossStage.Fight);
        Assert.Equal(BossStage.Fight, boss.Stage);
        Assert.True(w.BossBattle);
        Assert.All(w.Seals.Where(x => x.Kind == SealKind.Boss), x => Assert.False(x.Open));
        var volleys = new System.Collections.Generic.List<BossAttack>();
        bool openOutside = false, sighed = false;
        for (int i = 0; i < 12 * PlaneWorld.TickRate && !sighed; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.BossVolley)) volleys.Add((BossAttack)(int)e.Size);
            if (boss.Open && boss.Act != BossAct.Window) openOutside = true;
            if (boss.Act == BossAct.Window) sighed = true;
        }
        Assert.True(sighed, "she sighs after her act");
        Assert.False(openOutside, "her throat is shut but in the sigh");
        Assert.Equal(new[] { BossAttack.Fan, BossAttack.Ring }, volleys);
        Assert.True(boss.Open);
        // Past half her meter, the slams come in pairs.
        boss.Phase = 2;
        Assert.Equal(new[] { BossAttack.Fan, BossAttack.Ring, BossAttack.Ring }, PlaneWorld.ActNow(boss));
    }

    [Fact]
    public void Kip_FlicksARingThenDartsPastHer_ThenWaitsSideOnHerStripeToHer_AndHerQueueComesPastHalf()
    {
        var w = GreatCave("kip");
        var boss = w.Boss!;
        Assert.Equal(BodyForm.Wrasse, boss.Spec.Form);
        var her = w.ArenaCenter + new Vector2(0f, 8f);
        Hold(w, her, 3f, () => boss.Stage == BossStage.Fight);
        Assert.Equal(BossStage.Fight, boss.Stage);
        var volleys = new System.Collections.Generic.List<BossAttack>();
        float farthest = 0f;
        bool openOutside = false;
        for (int i = 0; i < 12 * PlaneWorld.TickRate && boss.Act != BossAct.Window; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.BossVolley)) volleys.Add((BossAttack)(int)e.Size);
            farthest = MathF.Max(farthest, Vector2.Distance(boss.Position, w.ArenaCenter));
            if (boss.Open && boss.Act != BossAct.Window) openOutside = true;
        }
        Assert.Equal(BossAct.Window, boss.Act);
        Assert.False(openOutside, "her stripe is shut but in her wait");
        Assert.Equal(new[] { BossAttack.Ring, BossAttack.Dart }, volleys);
        Assert.True(farthest > 6f, $"she darts out from her station ({farthest} m)");
        // Waiting, she turns her flank, the pearl on it, toward her.
        Hold(w, her, 0.8f);
        Assert.True(boss.Open);
        Assert.True(Vector2.Distance(boss.PearlAt, her) < Vector2.Distance(boss.Position, her) - 0.3f, "her pearl's flank is toward her");
        // Past half her meter, her clients come: six lesser murklings, the fish she used to clean.
        int before = w.Murklings.Count(m => m.Alive && m.Lesser);
        w.DebugHurtBoss(boss.MaxHp * 0.55f);
        Assert.Equal(2, boss.Phase);
        Assert.Equal(before + 6, w.Murklings.Count(m => m.Alive && m.Lesser));
        Assert.Equal(new[] { BossAttack.Ring, BossAttack.Dart, BossAttack.Dart }, PlaneWorld.ActNow(boss));
    }

    [Fact]
    public void AServiceOpensOnlyOnceItsNeighbourIsHome()
    {
        var jelly = new Jelly();
        Assert.False(Folk.Opens(jelly, BayService.TideFan));
        jelly.Freed.Add("tull");
        Assert.True(Folk.Opens(jelly, BayService.TideFan));
        // A service whose neighbour has no boss yet is open as it always was.
        foreach (var service in Enum.GetValues<BayService>().Where(s => Folk.Opener(s) is null))
            Assert.True(Folk.Opens(new Jelly(), service));
        Assert.Equal("Grandmother Tull has gone home to Lantern Bay", Folk.HomeCaption(Folk.Get("tull")!));
    }

    [Fact]
    public void Fennel_HidesInHisKelp_StrikesFromHidingUnfound_ThenOnceFoundLashesAndOpens_AndMovesOn()
    {
        var w = GreatCave("fennel");
        var boss = w.Boss!;
        Assert.Equal(BodyForm.Seadragon, boss.Spec.Form);
        Assert.Equal(5, boss.Stands.Count);
        Assert.Equal(boss.Stands[0], boss.Position);
        Assert.True(boss.Hidden);
        // Into the arena on the far side from him: he stays hidden, and strikes from hiding with no opening after.
        var away = w.ArenaCenter - Vector2.Normalize(boss.Position - w.ArenaCenter) * 4f;
        Assert.True(Vector2.Distance(away, boss.Position) > PlaneBossTuning.FoundReach + boss.Radius);
        Hold(w, away, 3f, () => boss.Stage == BossStage.Fight);
        var volleys = new System.Collections.Generic.List<BossAttack>();
        bool opened = false;
        for (int i = 0; i < 10 * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = away;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.BossVolley)) volleys.Add((BossAttack)(int)e.Size);
            opened |= boss.Open;
        }
        Assert.Contains(BossAttack.Wall, volleys);
        Assert.DoesNotContain(BossAttack.Fan, volleys);
        Assert.False(opened, "unfound, he never opens");
        Assert.True(boss.Hidden);
        // She swims up to him: found, he unfurls, lashes at her and opens.
        var near = boss.Position + Vector2.Normalize(w.ArenaCenter - boss.Position) * (boss.Radius + 2.5f);
        int stand = boss.Stand;
        volleys.Clear();
        for (int i = 0; i < 8 * PlaneWorld.TickRate && boss.Act != BossAct.Window; i++)
        {
            w.Player.Position = w.Player.PrevPosition = near;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.BossVolley)) volleys.Add((BossAttack)(int)e.Size);
        }
        Assert.Equal(BossAct.Window, boss.Act);
        Assert.True(boss.Open);
        Assert.False(boss.Hidden);
        Assert.Equal(BossAttack.Fan, volleys.Last());
        // After his opening he drifts off to another stand.
        Hold(w, near, boss.Spec.Window + 0.2f);
        Assert.NotEqual(stand, boss.Stand);
        // From half his meter, two other stands drip too.
        Hold(w, near, 2f, () => boss.Act is BossAct.Telegraph or BossAct.Window);
        w.DebugHurtBoss(boss.MaxHp * 0.55f);
        Assert.Equal(2, boss.Decoys.Count);
        Assert.DoesNotContain(boss.Stand, boss.Decoys);
    }

    [Fact]
    public void Ottilie_KeepsHerPearlBehindHer_ThrowsThenReachesAlongLitLines_ThenLiftsItToAdmire_AndBloomsPastHalf()
    {
        var w = GreatCave("ottilie");
        var boss = w.Boss!;
        Assert.Equal(BodyForm.Octopus, boss.Spec.Form);
        Assert.True(boss.Spec.Shielded);
        var her = w.ArenaCenter + new Vector2(0f, 9f);
        Hold(w, her, 3f, () => boss.Stage == BossStage.Fight);
        // She turns to face her, her body between Clementine and the pearl.
        Hold(w, her, 1.5f);
        Assert.True(Vector2.Distance(boss.PearlAt, her) > Vector2.Distance(boss.Position, her) + 0.5f, "her pearl is behind her");
        var volleys = new System.Collections.Generic.List<BossAttack>();
        bool lit = false;
        for (int i = 0; i < 10 * PlaneWorld.TickRate && boss.Act != BossAct.Window; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = w.Run.MaxHp;
            w.Step(default);
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.BossVolley)) volleys.Add((BossAttack)(int)e.Size);
            lit |= boss.ReachDirs.Count == 2;
        }
        Assert.Equal(new[] { BossAttack.ArcBombs, BossAttack.Reach }, volleys);
        Assert.True(lit, "the reach's lines are lit before it strikes");
        Assert.Equal(BossAct.Window, boss.Act);
        Assert.True(boss.Open);
        // Past half her meter, an ink bloom joins her act.
        w.DebugHurtBoss(boss.MaxHp * 0.55f);
        Assert.Equal(new[] { BossAttack.ArcBombs, BossAttack.Bloom, BossAttack.Reach }, PlaneWorld.ActNow(boss));
        Hold(w, her, 8f, () => boss.Blooms.Count > 0);
        Assert.NotEmpty(boss.Blooms);
    }

    [Fact]
    public void Bubbles_RisesAndComesDownInARing_ThenHerSacsFire_HerHeartOpenOnlyOnceTheyAreBurst()
    {
        var w = GreatCave("bubbles");
        var boss = w.Boss!;
        Assert.Equal(BodyForm.Dragonet, boss.Spec.Form);
        Assert.Equal(Exposure.CoaxOut, boss.Spec.Exposure);
        Assert.Equal(5, boss.Sacs.Count);
        var her = w.ArenaCenter + new Vector2(0f, 10f);
        Hold(w, her, 3f, () => boss.Stage == BossStage.Fight);
        var volleys = new System.Collections.Generic.List<BossAttack>();
        bool aloft = false, openEarly = false;
        Vector2? down = null;
        for (int i = 0; i < 12 * PlaneWorld.TickRate && boss.Act != BossAct.Window; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Hp = w.Run.MaxHp;
            if (boss.RiseTo is { } to) down = to;
            w.Step(default);
            aloft |= boss.Aloft;
            openEarly |= boss.Open;
            foreach (var e in w.Events.Where(e => e.Type == PlaneEventType.BossVolley))
            {
                volleys.Add((BossAttack)(int)e.Size);
                if ((BossAttack)(int)e.Size == BossAttack.Rise) Assert.True(Vector2.Distance(down!.Value, boss.Position) < 0.5f, $"she comes down at {boss.Position}, her shadow at {down}");
            }
        }
        Assert.True(aloft, "she rises off the plane");
        Assert.Equal(new[] { BossAttack.Rise, BossAttack.SacBurst }, volleys);
        Assert.False(openEarly, "her heart is buried under her sacs");
        // Every sac burst: her pearl surfaces.
        w.DebugBurstSacs();
        Assert.True(boss.Open);
        Assert.Equal("The dragonet has gone home to Lantern Bay", Folk.HomeCaption(Folk.Get("bubbles")!));
    }

    [Fact]
    public void TheDuskDanceGivesBackEverySkillPointSpent()
    {
        var run = new PlaneRun(Catalog.Value, new Tuning()) { Points = 3 };
        var stat = Skills.All(new Tuning()).First().Stat;
        Assert.True(run.Spend(stat));
        Assert.True(run.Spend(stat));
        float max = run.MaxHp;
        Assert.Equal(2, run.Relearn());
        Assert.Equal(3, run.Points);
        Assert.Empty(run.Skills);
        Assert.True(run.MaxHp < max);
        Assert.True(run.Hp <= run.MaxHp);
    }

    [Fact]
    public void FennelKeepsTheShellsSheLeavesWithHim()
    {
        var jelly = new Jelly();
        var run = new PlaneRun(Catalog.Value, new Tuning()) { Shells = 40 };
        BroodBed.Leave(jelly, run);
        Assert.Equal(0, run.Shells);
        Assert.Equal(40, jelly.Banked);
        run.Shells = 5;
        BroodBed.TakeBack(jelly, run);
        Assert.Equal(45, run.Shells);
        Assert.Equal(0, jelly.Banked);
    }

    [Fact]
    public void EachNeighboursChainBeginsOnceSheIsHome_ItsStepsInTurn_TheLastPayingHerActivePearl()
    {
        var jelly = new Jelly();
        Assert.Empty(Chains.Active(jelly));
        jelly.Freed.Add("tull");
        var (chain, step, _) = Assert.Single(Chains.Active(jelly));
        Assert.Equal("tull", chain.Folk);
        Assert.Equal(StepKind.Enter, step.Kind);
        // Out of order, nothing moves; in order, step by step.
        Assert.Empty(Chains.Note(jelly, StepKind.Find, zone: Chains.DepthZone(2)));
        Assert.Empty(Chains.Note(jelly, StepKind.Enter, zone: Chains.DepthZone(3)));
        Assert.Equal(StepKind.Enter, Chains.Active(jelly).Single().Step.Kind);
        Chains.Note(jelly, StepKind.Enter, zone: Chains.DepthZone(2));
        Assert.Equal(StepKind.Prime, Chains.Active(jelly).Single().Step.Kind);
        Chains.Note(jelly, StepKind.Prime, zone: Chains.DepthZone(1));
        Chains.Note(jelly, StepKind.Prime, zone: Chains.DepthZone(2));
        Assert.Equal(1, Chains.Active(jelly).Single().Count);
        Chains.Note(jelly, StepKind.Prime, zone: Chains.DepthZone(2));
        Assert.Empty(Chains.Note(jelly, StepKind.Find, zone: Chains.DepthZone(3)));
        var done = Chains.Note(jelly, StepKind.Find, zone: Chains.DepthZone(2));
        Assert.Equal("tull", Assert.Single(done).Folk);
        Assert.Empty(Chains.Active(jelly));
        Assert.True(Chains.Done(jelly, Chains.Of("tull")!));
        // Every chain pays an active pearl, and names no place on a map (a dive has none: docs/DIVE.md §7).
        foreach (var c in Chains.All)
        {
            Assert.NotNull(Folk.Get(c.Folk));
            Assert.Contains(c.Pearl, PlaneRun.PortedPearls);
            Assert.True(Catalog.Value.TryGet(c.Pearl, out var pearl) && pearl.Kind == ItemKind.Active, $"{c.Pearl} is an active pearl");
            Assert.DoesNotContain(c.Steps, s => s.Kind == StepKind.Mouth || s.Kind == StepKind.Find && s.Zone is null);
        }
        // Tull's late line is hers once the Carnival's neighbour is home.
        var tull = Folk.Get("tull")!;
        Assert.DoesNotContain(Enumerable.Range(0, 8), v => Folk.Line(tull, v) == tull.Late);
        Assert.Contains(Enumerable.Range(0, 8), v => Folk.Line(tull, v, late: true) == tull.Late);
    }

    [Fact]
    public void InkCloudLosesHerToTheLeaksCreatures_ConchHornShakesThemOffAndAway()
    {
        var map = TopDownGenerator.GenerateCave(Seed, new LevelId(1001, 1, 4), 0.2f, 2);
        var run = new PlaneRun(Catalog.Value, new Tuning());
        run.Add("ink_cloud");
        var w = new PlaneWorld(map, new Tuning(), run, new PlaneOptions { Adventure = true });
        Assert.Equal("ink_cloud", run.SlotActive(0)?.Id);
        run.SetCharge(0, 1f);
        Assert.False(w.Unseen);
        w.Step(new PlaneInput { UseActive = true });
        Assert.True(w.Unseen);
        Assert.Contains(w.Clouds, c => Vector2.Distance(c.Position, w.Player.Position) < 0.5f);
        Hold(w, w.Player.Position, 4.5f);
        Assert.False(w.Unseen);
        // Conch Horn (she carries one active pearl: it takes Ink Cloud's place): the murkling beside her is shaken and knocked away.
        run.Add("conch_horn");
        run.SetCharge(0, 1f);
        Assert.Equal("conch_horn", run.SlotActive(0)?.Id);
        var m = w.Murklings.First(x => x.Alive && !x.Prime);
        w.Player.Position = w.Player.PrevPosition = m.Position + new Vector2(2f, 0f);
        w.Step(new PlaneInput { UseActive = true });
        Assert.True(m.Shaken > 0f);
        Assert.True(Vector2.Dot(m.Velocity, m.Position - w.Player.Position) > 0f, "knocked away from her");
    }
}
