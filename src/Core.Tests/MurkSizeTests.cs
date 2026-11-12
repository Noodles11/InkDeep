using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>A murkling's size is its corruption (docs/CORRUPTION.md, Murklings; PlaneMurkSize.cs).</summary>
public class MurkSizeTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    static readonly PlaneOptions War = new() { Corruption = true, Pufferlings = false, Boss = OctoShoots.Core.Bosses.BossGenerator.QueenClam(1UL, 1, 1) };

    static PlaneWorld World()
    {
        var w = new PlaneWorld(TestLevels.Get(TestLevels.Find(p => TestLevels.General(p) && !p.HasBoss && p.Id.Level > 1)), new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), War);
        foreach (var area in w.Areas) area.Spent = true;
        w.Murklings.Clear();
        w.Blotters.Clear();
        w.Bombs.Clear();
        w.ChargeStones.Clear();
        return w;
    }

    static Vector2 Line(PlaneWorld w, float far)
    {
        var her = w.Player.Position;
        for (int i = 0; i < 64; i++)
        {
            float a = i * MathF.Tau / 64f;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            bool open = true;
            for (float s = 0.5f; s <= far && open; s += 0.5f) open = w.Clear(her + d * s, 0.9f);
            if (open && w.LineOfSight(her, her + d * far)) return d;
        }
        throw new InvalidOperationException("no open line");
    }

    /// <summary>A murkling budded (out at once) on an open line from her, held there, and the way to it.</summary>
    static (PlaneWorld W, Murkling M, Vector2 D, Vector2 At) Budded(float? full = null)
    {
        var w = World();
        var d = Line(w, 6f);
        var at = w.Map.Start.Position + d * 4f;
        var m = w.DebugBud(at, prime: false);
        m.Bud = 0f;
        m.ThrowTimer = 100f;
        m.Kind = MurkKind.Thrower;
        if (full is { } f)
        {
            float meter = m.Corruption.Max / MurkSizeTuning.MeterScale(m.Full);
            m.Full = f;
            m.Grade = MurkSizeTuning.GradeOf(f);
            m.Corruption.Max = m.Corruption.Value = meter * MurkSizeTuning.MeterScale(f);
        }
        return (w, m, d, at);
    }

    static void Step(PlaneWorld w, Murkling m, Vector2 at, Vector2 aim, bool beam)
    {
        m.Position = at;
        m.Velocity = Vector2.Zero;
        m.ThrowTimer = 100f;
        w.Player.Position = w.Player.PrevPosition = w.Map.Start.Position;
        w.Player.Velocity = Vector2.Zero;
        w.Player.Hp = 100f;
        w.Step(new PlaneInput { Aim = aim, Beam = beam });
    }

    [Fact]
    public void Each_has_its_own_full_size_and_a_grade_by_it_skewed_small()
    {
        var sizes = Enumerable.Range(0, 1000).Select(i => MurkSizeTuning.FullOf(i * 0.1f)).ToList();
        Assert.All(sizes, s => Assert.InRange(s, MurkSizeTuning.Least, MurkSizeTuning.Most));
        var grades = sizes.Select(MurkSizeTuning.GradeOf).ToList();
        int lesser = grades.Count(g => g == MurkGrade.Lesser), regular = grades.Count(g => g == MurkGrade.Regular), elite = grades.Count(g => g == MurkGrade.Elite);
        Assert.True(regular > lesser && lesser > elite && elite > 50, $"{lesser} {regular} {elite}");

        var (w, m, _, _) = Budded();
        Assert.True(m.Sized);
        Assert.Equal(MurkSizeTuning.GradeOf(m.Full), m.Grade);
        Assert.Equal(m.Full, m.Radius, 3);
    }

    [Fact]
    public void A_bigger_one_holds_more_corruption()
    {
        var (_, small, _, _) = Budded(0.38f);
        var (_, big, _, _) = Budded(0.66f);
        Assert.True(big.Corruption.Max > 2f * small.Corruption.Max);
    }

    [Fact]
    public void It_shrinks_toward_its_core_as_it_loses_corruption()
    {
        var (w, m, d, at) = Budded(0.6f);
        m.Corruption.Value = m.Corruption.Max * 0.5f;
        Step(w, m, at, d, false);
        Assert.Equal(MurkSizeTuning.Core + (0.6f - MurkSizeTuning.Core) * 0.5f, m.Radius, 2);
        Assert.Equal(m.Radius, m.Body, 4);
    }

    [Fact]
    public void Freed_when_no_bigger_than_its_core()
    {
        var (w, m, d, at) = Budded(0.5f);
        float smallest = m.Radius;
        for (int i = 0; i < 20 * PlaneWorld.TickRate && m.Alive; i++)
        {
            Step(w, m, at, d, true);
            if (m.Alive) smallest = MathF.Min(smallest, m.Radius);
        }
        Assert.False(m.Alive);
        Assert.True(smallest < 0.5f && smallest >= MurkSizeTuning.Core);
    }

    [Fact]
    public void It_grows_back_in_the_dark_after_a_grace()
    {
        var (w, m, d, at) = Budded(0.6f);
        // Burned down a while, then left.
        for (int i = 0; i < 20 * PlaneWorld.TickRate && m.Corruption.Fraction > 0.5f; i++) Step(w, m, at, d, true);
        float burned = m.Corruption.Value;
        Assert.True(burned < m.Corruption.Max);
        for (int i = 0; i < (int)(0.8f * PlaneWorld.TickRate); i++) Step(w, m, at, d, false);
        Assert.Equal(burned, m.Corruption.Value, 2);
        for (int i = 0; i < (int)(1.2f * PlaneWorld.TickRate); i++) Step(w, m, at, d, false);
        Assert.True(m.Corruption.Value > burned + 0.1f * m.Corruption.Max && m.Corruption.Value < m.Corruption.Max, "growing back");
        for (int i = 0; i < 6 * PlaneWorld.TickRate; i++) Step(w, m, at, d, false);
        Assert.Equal(m.Corruption.Max, m.Corruption.Value, 2);
        Assert.Equal(0.6f, m.Radius, 2);
    }

    [Theory]
    [InlineData(0.38f, 0, 0)]
    [InlineData(0.5f, 1, 2)]
    [InlineData(0.66f, 5, 10)]
    public void Its_grade_is_its_loot_and_every_one_leaves_its_core(float full, int least, int most)
    {
        var (w, m, d, at) = Budded(full);
        int shells = w.Shells.Count, cores = w.Cores.Count;
        for (int i = 0; i < 30 * PlaneWorld.TickRate && m.Alive; i++) Step(w, m, at, d, true);
        Assert.False(m.Alive);
        Assert.InRange(w.Shells.Count - shells, least, most);
        Assert.Equal(cores + 1, w.Cores.Count);
    }
}
