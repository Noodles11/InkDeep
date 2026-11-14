using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Bubble vaults: her light fills one till it pops and frees its prize; the shop sells them.</summary>
public class VaultTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World() => new(TestLevels.WithShop(), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()));

    /// <summary>A vault 3 m from her in open water she sees, and the way to it.</summary>
    static (PlaneVault Vault, Vector2 Way) Near(PlaneWorld w, VaultPrize prize, int shells = 0)
    {
        var her = w.Player.Position;
        for (int k = 0; k < 32; k++)
        {
            float a = k * MathF.Tau / 32f;
            var way = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var at = her + way * 3f;
            if (!w.Clear(at, VaultTuning.Radius) || !w.LineOfSight(her, at)) continue;
            var v = new PlaneVault { Position = at, Prize = prize, Shells = shells };
            w.Vaults.Add(v);
            return (v, way);
        }
        throw new InvalidOperationException("no open water near her");
    }

    [Fact]
    public void Her_beam_fills_it_till_it_pops_and_frees_its_shells()
    {
        var w = World();
        var (v, way) = Near(w, VaultPrize.Shells, 9);
        int before = w.Shells.Count;
        float lastFill = 0f;
        bool popped = false;
        for (int i = 0; i < 4 * PlaneWorld.TickRate && !popped; i++)
        {
            w.Step(new PlaneInput { Aim = way, Beam = true });
            Assert.True(v.Fill >= lastFill || v.Popped);
            lastFill = v.Fill;
            popped = w.Events.Any(e => e.Type == PlaneEventType.VaultPopped);
        }
        Assert.True(v.Popped);
        Assert.Equal(before + 9, w.Shells.Count);
    }

    [Fact]
    public void Unlit_it_settles_back_and_never_pops()
    {
        var w = World();
        var (v, way) = Near(w, VaultPrize.Orb);
        for (int i = 0; i < PlaneWorld.TickRate / 2; i++) w.Step(new PlaneInput { Aim = way, Beam = true });
        float lit = v.Fill;
        Assert.True(lit > 0.1f, $"lit {lit}");
        // Her beam turned away, she far off: it settles back.
        w.Player.Position = w.Player.PrevPosition = w.Player.Position - way * 40f;
        for (int i = 0; i < PlaneWorld.TickRate; i++) w.Step(new PlaneInput { Aim = -way });
        Assert.True(v.Fill < lit, $"{v.Fill} vs {lit}");
        Assert.False(v.Popped);
    }

    [Fact]
    public void An_orb_heals_and_a_prank_knocks_her_back()
    {
        var w = World();
        var (orb, way) = Near(w, VaultPrize.Orb);
        while (!orb.Popped) w.Step(new PlaneInput { Aim = way, Beam = true });
        Assert.NotEmpty(w.Hearts);
        var w2 = World();
        var (prank, way2) = Near(w2, VaultPrize.Prank);
        while (!prank.Popped) w2.Step(new PlaneInput { Aim = way2, Beam = true });
        Assert.True(Vector2.Dot(w2.Player.Velocity, -way2) > VaultTuning.PrankPush * 0.8f);
        Assert.True(w2.Player.TumbleTimer > 0f);
        Assert.Equal(w2.Run.MaxHp, w2.Player.Hp, 3);
    }

    [Fact]
    public void The_shop_sells_one_for_twenty_shells()
    {
        var w = World();
        var stand = Assert.Single(w.Stands, s => s.Kind == StandKind.Vault);
        Assert.Equal(20, stand.Price);
        w.Run.Shells = 25;
        w.Player.Position = w.Player.PrevPosition = stand.Position;
        w.Step(default);
        Assert.True(stand.Sold);
        Assert.Equal(5, w.Run.Shells);
        var v = Assert.Single(w.Vaults);
        Assert.Equal(stand.Position, v.Position);
        Assert.Contains(v.Prize, new[] { VaultPrize.Shells, VaultPrize.Orb, VaultPrize.Prank });
    }
}
