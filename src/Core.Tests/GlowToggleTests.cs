using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her glow put out (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.4): dark, and her beam dark, she is hidden.</summary>
public class GlowToggleTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => p.Id.Depth == 2 && !p.HasBoss)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()),
            new PlaneOptions { Adventure = true });
        w.Murklings.Clear();
        w.Fish.Clear();
        return w;
    }

    static void Hold(PlaneWorld w, Vector2 at, PlaneInput input)
    {
        w.Player.Position = w.Player.PrevPosition = at;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = w.Run.MaxHp;
        w.Step(input);
    }

    [Fact]
    public void A_press_puts_her_glow_out_and_another_lights_it_again()
    {
        var w = World();
        var at = w.Map.Start.Position;
        Hold(w, at, default);
        Assert.True(w.Aura.Shining);
        Hold(w, at, new PlaneInput { ToggleGlow = true });
        Assert.True(w.GlowOff);
        Assert.Contains(w.Events, e => e.Type == PlaneEventType.GlowToggled && e.Size == 0f);
        Assert.False(w.Aura.Shining);
        Assert.True(w.Hidden);
        Assert.True(w.Unseen);
        // Her beam shining gives her away.
        Hold(w, at, new PlaneInput { Beam = true, Aim = Vector2.UnitX });
        Assert.False(w.Hidden);
        Hold(w, at, default);
        Assert.True(w.Hidden);
        Hold(w, at, new PlaneInput { ToggleGlow = true });
        Assert.False(w.GlowOff);
        Assert.True(w.Aura.Shining);
        Assert.False(w.Hidden);
    }

    [Fact]
    public void Hidden_a_murkling_beside_her_never_finds_her()
    {
        var w = World();
        var at = w.Map.Start.Position;
        Hold(w, at, new PlaneInput { ToggleGlow = true });
        var near = Enumerable.Range(0, 16).Select(i => at + new Vector2(MathF.Cos(i * 0.4f), MathF.Sin(i * 0.4f)) * 2.5f).First(q => w.Clear(q, 0.6f));
        var m = new Murkling { Position = near, Bud = 0f };
        w.Murklings.Add(m);
        for (int i = 0; i < 3 * PlaneWorld.TickRate; i++) Hold(w, at, default);
        Assert.False(m.Clinging);
        Assert.True(m.Light is null || m.Light.Owner != w.Her);
        // Lit again, it finds her.
        Hold(w, at, new PlaneInput { ToggleGlow = true });
        for (int i = 0; i < 3 * PlaneWorld.TickRate && !m.Clinging; i++) Hold(w, at, default);
        Assert.True(m.Clinging || m.Light?.Owner == w.Her);
    }

    [Fact]
    public void Dark_her_inner_light_seals_no_leak()
    {
        var w = World();
        var v = w.Leaks[0];
        var at = new[] { 1f, -1f }.Select(k => v.Position + new Vector2(0f, k * (AdventureTuning.LeakSealReach - 1f)))
            .Concat(new[] { 1f, -1f }.Select(k => v.Position + new Vector2(k * (AdventureTuning.LeakSealReach - 1f), 0f))).First(q => w.Map.IsOpen(q));
        w.Inkwardens.Clear();
        Hold(w, at, new PlaneInput { ToggleGlow = true });
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++) Hold(w, at, default);
        Assert.Equal(0f, v.Light);
        Hold(w, at, new PlaneInput { ToggleGlow = true });
        for (int i = 0; i < 2 * PlaneWorld.TickRate; i++) Hold(w, at, default);
        Assert.True(v.Light > 0f);
    }
}
