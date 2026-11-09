using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>A freed murkling's core: it hangs a moment, and heals her when she takes it (docs/LIGHT-AND-GRAPH-PROPOSAL.md §4.2).</summary>
public class CoreTests
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
        w.Shells.Clear();
        return w;
    }

    /// <summary>An open spot this far from her, in plain sight.</summary>
    static Vector2 Beside(PlaneWorld w, float distance)
    {
        var her = w.Player.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var p = her + new Vector2(MathF.Cos(a), MathF.Sin(a)) * distance;
            if (w.Clear(p, 0.8f) && w.LineOfSight(her, p)) return p;
        }
        throw new InvalidOperationException("no open spot");
    }

    static Murkling Freed(PlaneWorld w, Vector2 at, bool prime = false)
    {
        var m = new Murkling { Position = at, Bud = 0f, Prime = prime };
        w.Murklings.Add(m);
        w.DebugFreeMurkling(m);
        return m;
    }

    static void Hold(PlaneWorld w, float seconds, float hp)
    {
        var her = w.Player.Position;
        for (int i = 0; i < seconds * PlaneWorld.TickRate; i++)
        {
            w.Player.Position = w.Player.PrevPosition = her;
            w.Player.Velocity = Vector2.Zero;
            w.Player.Hp = hp;
            w.Step(default);
        }
    }

    [Fact]
    public void A_freed_murkling_leaves_its_core_which_fades_in_a_second()
    {
        var w = World();
        var at = Beside(w, 6f);
        Freed(w, at);
        var core = Assert.Single(w.Cores);
        Assert.Equal(at, core.Position);
        Assert.Equal(CoreTuning.Light, core.Light);
        Hold(w, CoreTuning.Life * 0.5f, 50f);
        Assert.Single(w.Cores);
        Hold(w, CoreTuning.Life * 0.6f, 50f);
        Assert.Empty(w.Cores);
        Assert.Equal(50f, w.Player.Hp);
    }

    [Fact]
    public void Taking_a_core_heals_her_but_never_past_her_max()
    {
        var w = World();
        Freed(w, w.Player.Position + new Vector2(0.3f, 0f));
        w.Player.Hp = 50f;
        w.Step(default);
        Assert.Empty(w.Cores);
        Assert.Equal(50f + CoreTuning.Light, w.Player.Hp, 3);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.CoreTaken && Math.Abs(e.Size - CoreTuning.Light) < 1e-3f);

        Freed(w, w.Player.Position + new Vector2(0.3f, 0f), prime: true);
        w.Player.Hp = w.Run.MaxHp - 5f;
        w.Step(default);
        Assert.Equal(w.Run.MaxHp, w.Player.Hp, 3);
    }

    [Fact]
    public void A_primes_core_holds_more_light()
    {
        var w = World();
        Freed(w, Beside(w, 6f), prime: true);
        Assert.Contains(w.Cores, c => c.Light == CoreTuning.PrimeLight);
    }

    [Fact]
    public void Only_the_magnet_draws_a_core_to_her()
    {
        var plain = World();
        Freed(plain, Beside(plain, 3f));
        Hold(plain, 0.5f, 50f);
        Assert.Equal(50f, plain.Player.Hp);

        var drawn = World("remora_sucker");
        Freed(drawn, Beside(drawn, 3f));
        float hp = 0f;
        var her = drawn.Player.Position;
        for (int i = 0; i < 0.9f * PlaneWorld.TickRate && drawn.Cores.Count > 0; i++)
        {
            drawn.Player.Position = drawn.Player.PrevPosition = her;
            drawn.Player.Hp = 50f;
            drawn.Step(default);
            hp = drawn.Player.Hp;
        }
        Assert.Empty(drawn.Cores);
        Assert.Equal(50f + CoreTuning.Light, hp, 3);
    }
}
