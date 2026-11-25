using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

public class OpenSeaTests
{
    static readonly OpenSea Sea = new(0xC0FFEEUL, Biome.SunlitReef);

    static IEnumerable<(int X, int Y)> ChunksWithin(int reach)
    {
        var seen = new HashSet<(int X, int Y)>();
        for (int y = -reach; y <= reach; y++)
        for (int x = -reach; x <= reach; x++)
            if (Math.Abs(x) + Math.Abs(y) <= reach && seen.Add(Sea.ChunkOfCell((x, y)))) yield return Sea.ChunkOfCell((x, y));
    }

    [Fact]
    public void Every_cell_is_in_one_herringbone_chunk_lying_or_standing()
    {
        for (int y = -9; y <= 9; y++)
        for (int x = -9; x <= 9; x++)
        {
            var key = Sea.ChunkOfCell((x, y));
            Assert.Contains((x, y), Sea.CellsOf(key));
            Assert.All(Sea.CellsOf(key), c => Assert.Equal(key, Sea.ChunkOfCell(c)));
        }
        var keys = ChunksWithin(8).ToList();
        Assert.Contains(keys, Sea.Lying);
        Assert.Contains(keys, k => !Sea.Lying(k));
    }

    [Fact]
    public void Every_chunk_has_a_way_out_and_all_are_joined()
    {
        var keys = ChunksWithin(14).ToList();
        foreach (var k in keys)
        {
            var (a0, a1) = Sea.RectOf(k);
            foreach (var n in Sea.Neighbours(k))
            {
                var (b0, b1) = Sea.RectOf(n);
                // (They share a stretch of side: they meet along one axis and overlap along the other.)
                bool alongX = (MathF.Abs(a1.X - b0.X) < 0.5f || MathF.Abs(b1.X - a0.X) < 0.5f) && a0.Y < b1.Y && b0.Y < a1.Y;
                bool alongY = (MathF.Abs(a1.Y - b0.Y) < 0.5f || MathF.Abs(b1.Y - a0.Y) < 0.5f) && a0.X < b1.X && b0.X < a1.X;
                Assert.True(alongX || alongY, $"{k} and its neighbour {n} do not touch");
            }
            Assert.Contains(Sea.Neighbours(k), n => Sea.Linked(k, n));
            foreach (var n in Sea.Neighbours(k)) Assert.Equal(Sea.Linked(k, n), Sea.Linked(n, k));
        }
        // From the chunk holding (0, 0), through joined neighbours, every chunk near it is reached.
        var start = Sea.ChunkOfCell((0, 0));
        var reached = new HashSet<(int X, int Y)> { start };
        var queue = new Queue<(int X, int Y)>(new[] { start });
        while (queue.Count > 0)
        {
            var k = queue.Dequeue();
            foreach (var n in Sea.Neighbours(k))
                if (Sea.Linked(k, n) && Sea.CellsOf(n).Min(c => Math.Abs(c.X) + Math.Abs(c.Y)) <= 16 && reached.Add(n)) queue.Enqueue(n);
        }
        Assert.All(keys, k => Assert.Contains(k, reached));
    }

    [Fact]
    public void Neighbouring_chunks_meet_without_a_seam_open_where_joined_and_walled_where_not()
    {
        foreach (var a in ChunksWithin(3))
        {
            var ca = Sea.Build(a);
            foreach (var b in Sea.Neighbours(a))
            {
                var cb = Sea.Build(b);
                // Where both hold heights (their padding), the same heights.
                var lo = Vector2.Max(ca.Origin, cb.Origin);
                var hi = Vector2.Min(ca.Origin + new Vector2(ca.Stride - 1, ca.Rows - 1), cb.Origin + new Vector2(cb.Stride - 1, cb.Rows - 1));
                for (int y = (int)lo.Y; y <= (int)hi.Y; y++)
                for (int x = (int)lo.X; x <= (int)hi.X; x++)
                    Assert.Equal(ca.At(x, y), cb.At(x, y), 3);
                // Joined, there is water across the side they share; not joined, their walls shut it.
                bool crossing = false;
                for (int y = (int)ca.Min.Y; y < ca.Max.Y && !crossing; y++)
                for (int x = (int)ca.Min.X; x < ca.Max.X && !crossing; x++)
                    crossing = ca.At(x, y) <= 0f && new[] { (1, 0), (0, 1), (-1, 0), (0, -1) }
                        .Any(d => cb.Contains(new Vector2(x + d.Item1, y + d.Item2)) && cb.At(x + d.Item1, y + d.Item2) <= 0f);
                Assert.True(crossing == Sea.Linked(a, b), $"{a} and {b}: joined {Sea.Linked(a, b)}, water across {crossing}");
            }
        }
    }

    [Fact]
    public void Some_chunks_hold_a_hole_down_open_to_the_deep()
    {
        var holes = ChunksWithin(10).Select(Sea.Build).SelectMany(c => c.Holes.Select(h => (c, h))).ToList();
        Assert.NotEmpty(holes);
        foreach (var (c, h) in holes)
        {
            Assert.True(c.Contains(h));
            Assert.True(c.At((int)h.X, (int)h.Y) < LevelMap.ShaftBottom + 1.5f);
        }
    }

    [Fact]
    public void Each_kind_of_place_lies_as_often_as_its_rule_says_none_side_by_side_and_none_by_her_start()
    {
        var keys = ChunksWithin(40).ToList();
        var places = keys.ToDictionary(k => k, Sea.PlaceOf);
        foreach (var rule in OpenSea.PlaceRules)
        {
            var mine = keys.Where(k => places[k] == rule.Kind).ToList();
            float per = keys.Count / (float)mine.Count;
            Assert.True(per >= rule.AtMostOnceIn * 0.8f && per <= rule.AtLeastOnceIn * 1.25f, $"{rule.Kind}: one in {per:0.0} chunks (want {rule.AtMostOnceIn}–{rule.AtLeastOnceIn})");
            Assert.All(mine, k => Assert.DoesNotContain(Sea.Neighbours(k), n => places.GetValueOrDefault(n) == rule.Kind));
        }
        var home = Sea.ChunkOfCell((0, 0));
        Assert.Null(places[home]);
        Assert.All(Sea.Neighbours(home), n => Assert.Null(Sea.PlaceOf(n)));
        // Built, a chunk holds its place in its open water (a hole open to the deep).
        foreach (var k in keys.Where(k => places[k] is not null).Take(30))
        {
            var c = Sea.Build(k);
            if (c.Place is null) continue;
            Assert.True(c.Contains(c.PlaceAt) && c.At((int)c.PlaceAt.X, (int)c.PlaceAt.Y) < 0f, $"{k}: its {c.Place} not in its water");
        }
    }

    [Fact]
    public void A_chunk_is_the_same_every_time_and_quick_to_build()
    {
        var sea = new OpenSea(0xC0FFEEUL, Biome.SunlitReef);
        Assert.Equal(Sea.Build((4, 2)).Heights, sea.Build((4, 2)).Heights);
        var keys = ChunksWithin(6).ToList();
        foreach (var k in keys.Take(3)) sea.Build(k);
        var watch = Stopwatch.StartNew();
        foreach (var k in keys) sea.Build(k);
        double each = watch.Elapsed.TotalMilliseconds / keys.Count;
        Assert.True(each < 25, $"{each:0.0} ms a chunk");
    }

    [Fact]
    public void She_keeps_her_place_in_the_sea_as_its_window_moves_on()
    {
        var built = ChunksWithin(12).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap { Templated = true };
        var origin = new Vector2(-54f, -54f);
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = new Vector2(55f), Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(55f), Radius = 6f });
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var w = new PlaneWorld(map, new Tuning(), options: new PlaneOptions { OpenSea = true, Corruption = false, Adventure = false });
        w.Player.Position = w.Player.PrevPosition = new Vector2(70f, 60f);
        var inSea = w.Player.Position + origin;
        float under = map.HeightAt(w.Player.Position);
        var by = new Vector2(OpenSea.Cell, 0f);
        origin += by;
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        w.ShiftWindow(by);
        Assert.Equal(inSea, w.Player.Position + origin);
        Assert.Equal(under, map.HeightAt(w.Player.Position), 3);
        for (int i = 0; i < 30; i++) w.Step(default);
        Assert.False(w.Defeated);
    }

    [Fact]
    public void A_chunks_place_comes_into_the_sim_whole_and_keeps_its_place_in_the_sea_as_the_window_moves()
    {
        var catalog = OctoShoots.Core.Items.ItemCatalog.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "data", "items.json")));
        var built = ChunksWithin(12).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap { Templated = true, Seed = 7 };
        var origin = new Vector2(-54f, -54f);
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = new Vector2(55f), Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(55f), Radius = 6f });
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(catalog, new Tuning()), new PlaneOptions { OpenSea = true, Corruption = false, Adventure = false });
        var at = new Vector2(60f, 50f);
        foreach (var kind in new[] { PoiKind.TreasureCave, PoiKind.ShellCache, PoiKind.Shop, PoiKind.Shrine, PoiKind.CrabDeal, PoiKind.Relic })
            w.AddSeaPlace(kind, at, at + origin);
        Assert.Equal(8, map.Pois.Count);
        Assert.Equal(8, w.Visited.Length);
        Assert.Single(w.Clams);
        Assert.NotEmpty(w.Shells);
        Assert.Contains(w.Stands, s => s.Kind == StandKind.Health);
        Assert.Contains(w.Stands, s => s.Kind == StandKind.Shrine);
        Assert.Contains(w.Stands, s => s.Kind == StandKind.Deal);
        Assert.NotNull(w.CrabAt);
        Assert.True(w.Relics.Count + w.Pearls.Count >= 1);
        // Moved on a cell: every one of them where it was in the sea.
        var before = map.Pois.Select(p => p.Position + origin).Concat(w.Clams.Select(c => c.Position + origin)).Concat(w.Stands.Select(s => s.Position + origin)).Concat(w.Shells.Select(s => s.Position + origin)).ToList();
        var by = new Vector2(OpenSea.Cell, -OpenSea.Cell);
        origin += by;
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        w.ShiftWindow(by);
        var after = map.Pois.Select(p => p.Position + origin).Concat(w.Clams.Select(c => c.Position + origin)).Concat(w.Stands.Select(s => s.Position + origin)).Concat(w.Shells.Select(s => s.Position + origin)).ToList();
        Assert.Equal(before.Count, after.Count);
        Assert.All(before.Zip(after), pair => Assert.True(Vector2.Distance(pair.First, pair.Second) < 1e-3f, $"{pair.First} moved to {pair.Second}"));
    }

    [Fact]
    public void Every_sea_has_one_lair_a_hole_to_its_boss_away_from_her_start()
    {
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var sea = new OpenSea(seed, Biome.SunlitReef);
            var lair = sea.Build(sea.Lair);
            Assert.True(lair.Lair is { } at && lair.Holes.Contains(at) && lair.At((int)at.X, (int)at.Y) < LevelMap.ShaftBottom + 1.5f, $"seed {seed}");
            Assert.Null(lair.Place);
            Assert.True(lair.Min.Length() > 3 * OpenSea.Cell, $"seed {seed}: lair at {lair.Min}");
            // In the sim's window it is the hole marked as the boss's.
            var map = new LevelMap();
            var origin = lair.Lair!.Value - new Vector2(55f);
            sea.Fill(map, origin, k => k == sea.Lair ? lair : null);
            Assert.Single(map.Holes, h => h.Kind == ReefKind.Boss);
        }
    }

    [Fact]
    public void A_chunks_fish_come_with_it_and_go_once_the_window_leaves_them()
    {
        var catalog = OctoShoots.Core.Items.ItemCatalog.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "data", "items.json")));
        var built = ChunksWithin(12).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap { Templated = true, Seed = 7 };
        var origin = new Vector2(-54f, -54f);
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = new Vector2(55f), Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(55f), Radius = 6f });
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(catalog, new Tuning()), new PlaneOptions { OpenSea = true, Corruption = false, Adventure = false });
        foreach (var c in built.Values.Where(c => c.Min.X - origin.X >= 0f && c.Min.Y - origin.Y >= 0f && c.Max.X - origin.X <= 110f && c.Max.Y - origin.Y <= 110f))
            w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null);
        Assert.NotEmpty(w.ReefFish);
        // Far on: every fish still in the window keeps its host, and none lies beyond it.
        var by = new Vector2(3 * OpenSea.Cell, 0f);
        origin += by;
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        w.ShiftWindow(by);
        Assert.All(w.ReefFish, f =>
        {
            if (f.Kind == ReefFishKind.Clownfish) Assert.Equal(w.Anemones[f.Host].Position, f.Home);
            if (f.Kind == ReefFishKind.Chromis) Assert.Contains(f, w.Schools[f.Host].Fish);
        });
        Assert.All(w.Anemones, a => Assert.InRange(a.Position.X, 0f, map.Size));
        Assert.All(w.Fish, f => Assert.InRange(f.Position.X, 0f, map.Size));
    }

    [Fact]
    public void Its_murklings_and_specks_keep_to_the_window_and_gathered_specks_never_come_back()
    {
        var catalog = OctoShoots.Core.Items.ItemCatalog.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "data", "items.json")));
        var built = ChunksWithin(12).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap { Templated = true, Seed = 7, Menace = 0.5f };
        var origin = new Vector2(-54f, -54f);
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = new Vector2(55f), Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(55f), Radius = 6f });
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(catalog, new Tuning()), new PlaneOptions { OpenSea = true, Corruption = false, Adventure = true, Specks = true, Tidy = true });
        var inWindow = built.Values.Where(c => c.Min.X - origin.X >= 0f && c.Min.Y - origin.Y >= 0f && c.Max.X - origin.X <= 110f && c.Max.Y - origin.Y <= 110f).ToList();
        foreach (var c in inWindow) w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null);
        Assert.NotEmpty(w.Murklings);
        Assert.NotEmpty(w.Specks);
        int lights = w.Lights.All.Count;
        // Far on and back: what lay beyond the window is gone (its specks' lights with them), and a chunk come back brings no specks again.
        var by = new Vector2(3 * OpenSea.Cell, 0f);
        Sea.Fill(map, origin + by, k => built.GetValueOrDefault(k));
        w.ShiftWindow(by);
        Assert.All(w.Murklings, m => Assert.InRange(m.Position.X, 0f, map.Size));
        Assert.All(w.Specks, s => Assert.InRange(s.Position.X, 0f, map.Size));
        Assert.True(w.Lights.All.Count < lights);
        int specks = w.Specks.Count;
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        w.ShiftWindow(-by);
        foreach (var c in inWindow) w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null);
        Assert.Equal(specks, w.Specks.Count);
    }

    [Fact]
    public void Its_light_features_leave_with_their_chunk_and_come_back_as_she_left_them()
    {
        var catalog = OctoShoots.Core.Items.ItemCatalog.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "data", "items.json")));
        var built = ChunksWithin(12).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap { Templated = true, Seed = 7 };
        var origin = new Vector2(-54f, -54f);
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = new Vector2(55f), Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(55f), Radius = 6f });
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(catalog, new Tuning()),
            new PlaneOptions { OpenSea = true, Corruption = false, Adventure = false, Tidy = true, Optics = true, Flora = true, Pockets = true, Fauna = true });
        var inWindow = built.Values.Where(c => c.Min.X - origin.X >= 0f && c.Min.Y - origin.Y >= 0f && c.Max.X - origin.X <= 110f && c.Max.Y - origin.Y <= 110f).ToList();
        foreach (var c in inWindow) w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null);
        Assert.NotEmpty(w.Flora);
        Assert.NotEmpty(w.ChargeStones);
        var stone = w.ChargeStones.MinBy(c => c.Position.X)!;
        var stoneInSea = stone.Position + origin;
        int stones = w.ChargeStones.Count, lights = w.Lights.All.Count;
        // Far on: the chunks out of the window altogether take theirs with them, lights and all.
        var by = new Vector2(3 * OpenSea.Cell, 0f);
        Sea.Fill(map, origin + by, k => built.GetValueOrDefault(k));
        w.ShiftWindow(by);
        Assert.DoesNotContain(stone, w.ChargeStones);
        Assert.DoesNotContain(stone.Light, w.Lights.All);
        Assert.True(w.Lights.All.Count < lights);
        // And back: the same ones, where they were in the sea, no more of them.
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        w.ShiftWindow(-by);
        foreach (var c in inWindow) w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null);
        Assert.Contains(stone, w.ChargeStones);
        Assert.Contains(stone.Light, w.Lights.All);
        Assert.Equal(stoneInSea, stone.Position + origin);
        Assert.Equal(stones, w.ChargeStones.Count);
    }

    [Fact]
    public void Its_window_for_the_sim_takes_the_built_chunks_and_their_holes()
    {
        var built = ChunksWithin(10).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap();
        var origin = new Vector2(-54f, -54f);
        Assert.Equal(0, Sea.Fill(map, origin, k => built.GetValueOrDefault(k)));
        Assert.Equal(built.Values.Single(c => c.Contains(new Vector2(3f, 5f))).At(3, 5), map[57, 59]);
        Assert.All(map.Holes, h => Assert.True(map.HeightAt(h.Shaft.Center) < LevelMap.ShaftBottom + 1.5f));
    }
}
