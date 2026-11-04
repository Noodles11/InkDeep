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
    static readonly Lazy<HandLayout> Reef = new(() =>
        HandLayout.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "maps", "reef.json"))));

    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly SeedCode Seed = SeedCode.TryParse(Reef.Value.Seed, out var s) ? s : default;

    static int MouthOf(string folk) => Reef.Value.Mouths.FindIndex(m => m.Folk == folk);

    /// <summary>A neighbour's great cave, its deepest floor, and the world in it with her boss waiting.</summary>
    static PlaneWorld GreatCave(string folk)
    {
        var hand = Reef.Value;
        int mouth = MouthOf(folk);
        var n = OpenReef.GreatOf(hand, mouth)!;
        int floors = OpenReef.CaveFloors(hand, mouth);
        float menace = OpenReef.CaveMenace(hand, mouth);
        var map = TopDownGenerator.GenerateCave(Seed, OpenReef.CaveId("reef", hand.Depth, OpenReef.GreatCave(n)), menace, n.Level, null, floors, floors, boss: true);
        var options = PlaneOptions.OpenReef;
        options.Boss = FolkBosses.For(folk, n.Level, map.Menace);
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
    public void EachNeighbourWaitsInHerOwnGreatCave_ItsLevelAndFloorsHers_TheGreatMouthsListedLast()
    {
        var hand = Reef.Value;
        foreach (var n in Folk.Built)
        {
            int mouth = MouthOf(n.Id);
            Assert.True(mouth >= 0, $"{n.Name} has no great cave");
            Assert.Same(n, OpenReef.GreatOf(hand, mouth));
            Assert.Equal(n.Level, OpenReef.CaveLevel(hand, mouth));
            Assert.Equal(n.Floors, OpenReef.CaveFloors(hand, mouth));
            Assert.NotNull(FolkBosses.For(n.Id, n.Level, 0f));
            Assert.Contains(hand.Homes, h => h.Folk == n.Id);
        }
        // The Tide Fan names only the minor mouths: every one before the great caves', which come last.
        int minor = OpenReef.MinorMouths(hand);
        Assert.All(hand.Mouths.Take(minor), m => Assert.Null(m.Folk));
        Assert.All(hand.Mouths.Skip(minor), m => Assert.NotNull(m.Folk));
        Assert.Equal("The Sleeping Ledge", Folk.Get("tull")!.Cave);
        Assert.Null(OpenReef.GreatOf(hand, 0));
    }

    [Fact]
    public void AGreatCavesDeepestFloorIsAnArenaRoundItsLeak_HerBossOnIt_NoPrimeWaiting()
    {
        var w = GreatCave("tull");
        Assert.True(w.Map.HasBoss);
        Assert.True(w.Map.LeakCave);
        var leak = Assert.Single(w.Leaks);
        Assert.Equal(w.Map.Exit.Position, leak.Position);
        Assert.NotNull(w.Boss);
        Assert.Equal("tull", w.Boss!.Spec.Folk);
        Assert.Equal(w.ArenaCenter, w.Boss.Position);
        Assert.DoesNotContain(w.Murklings, m => m.Prime);
        // Its arena's walls stand open (its only leak is inside), to rise behind her only while the boss fights.
        var walls = w.Seals.Where(x => x.Kind == SealKind.Boss).ToList();
        Assert.NotEmpty(walls);
        Assert.All(walls, x => Assert.True(x.Open));
        // Once she is freed, no boss waits there again.
        var freed = PlaneOptions.OpenReef;
        Assert.Null(new PlaneWorld(w.Map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), freed).Boss);
        // The same cave for every jelly, every time.
        var again = GreatCave("tull");
        Assert.Equal(w.Map.ToBytes(), again.Map.ToBytes());
    }

    [Fact]
    public void HerLeakCannotBeSealedWhileTheBossLiesOverIt_OnceSheIsFreedItCan()
    {
        var w = GreatCave("tull");
        var leak = w.Leaks[0];
        var near = leak.Position + new Vector2(0f, w.Boss!.Radius + w.Radius + 0.3f);
        Hold(w, near, 20f);
        Assert.False(leak.Sealed);
        Assert.Equal(0f, leak.Light);
        Assert.Equal(BossStage.Fight, w.Boss.Stage);
        w.DebugFreeBoss();
        Hold(w, near, 1f);
        Assert.True(w.Boss.Freed);
        Assert.All(w.Seals.Where(x => x.Kind == SealKind.Boss), x => Assert.True(x.Open));
        Hold(w, leak.Position, 40f, () => leak.Sealed);
        Assert.True(leak.Sealed);
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
        Assert.Equal(3, w.Map.Floors);
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
    public void AServiceOpensOnlyOnceItsNeighbourIsHome_AndHerGreatCaveClosesForGoodOnceSealed()
    {
        var jelly = new Jelly();
        Assert.False(Folk.Opens(jelly, BayService.TideFan));
        jelly.Freed.Add("tull");
        Assert.True(Folk.Opens(jelly, BayService.TideFan));
        // A service whose neighbour has no boss yet is open as it always was.
        foreach (var service in Enum.GetValues<BayService>().Where(s => Folk.Opener(s) is null))
            Assert.True(Folk.Opens(new Jelly(), service));
        var hand = Reef.Value;
        int mouth = MouthOf("tull");
        Assert.DoesNotContain(mouth, OpenReef.ShutMouths(jelly, "reef", hand));
        OpenReef.SealedGreat(jelly, Folk.Get("tull")!);
        Assert.Contains(mouth, OpenReef.ShutMouths(jelly, "reef", hand));
        jelly.PlaySeconds += ReefTuning.Recorrupt * 3;
        Assert.Contains(mouth, OpenReef.ShutMouths(jelly, "reef", hand));
        Assert.Equal("Grandmother Tull has gone home to Lantern Bay", Folk.HomeCaption(Folk.Get("tull")!));
    }
}
