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

    internal static IEnumerable<(int X, int Y)> ChunksWithin(int reach)
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
        // (A chunk filled whole with rock is joined to none: nothing lies past it.)
        var keys = ChunksWithin(14).Where(k => Sea.FillOf(k) != OpenSea.Filler.Full).ToList();
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
                // (A secret room's ways are sealed till she opens them.)
                bool open = Sea.Linked(a, b) && !Sea.Secret(a) && !Sea.Secret(b);
                Assert.True(crossing == open, $"{a} and {b}: joined {Sea.Linked(a, b)}, sealed {Sea.Secret(a) || Sea.Secret(b)}, water across {crossing}");
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
    public void Each_kind_of_place_lies_as_often_and_as_far_apart_as_its_rule_says_and_none_by_her_start()
    {
        var keys = ChunksWithin(40).ToList();
        var places = keys.ToDictionary(k => k, Sea.PlaceOf);
        foreach (var rule in OpenSea.PlaceRules)
        {
            var mine = keys.Where(k => places[k] == rule.Kind).ToList();
            float per = keys.Count / (float)mine.Count;
            Assert.True(per >= rule.OnceIn * 0.6f && per <= rule.OnceIn * 1.6f, $"{rule.Kind}: one in {per:0.0} chunks (want about {rule.OnceIn})");
            Assert.All(mine, k => Assert.DoesNotContain(Sea.Neighbours(k), n => places.GetValueOrDefault(n) == rule.Kind));
            if (rule.Apart > 1)
                Assert.All(mine, k => Assert.DoesNotContain(mine, o => o != k && Math.Abs(o.X - k.X) <= rule.Apart && Math.Abs(o.Y - k.Y) <= rule.Apart));
            Assert.All(mine, k => Assert.True(Sea.CellsOf(k).Min(c => Math.Abs(c.X) + Math.Abs(c.Y)) >= rule.From));
        }
        // Some stand in a room drawn by hand (an area template of their kind's), their place in its water.
        var stamped = keys.Where(k => places[k] is { } kind && OpenSea.PlaceRules.First(r => r.Kind == kind).Templates is { Length: > 0 }).Take(40).Select(Sea.Build).Where(c => AreaTemplates.ByName.ContainsKey(c.Template)).ToList();
        Assert.NotEmpty(stamped);
        Assert.All(stamped, c => Assert.Contains(c.Template, OpenSea.PlaceRules.First(r => r.Kind == c.Place).Templates!));
        Assert.All(stamped, c => Assert.True(c.At((int)c.PlaceAt.X, (int)c.PlaceAt.Y) < 0f));
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
    public void A_chunk_is_the_same_every_time()
    {
        var sea = new OpenSea(0xC0FFEEUL, Biome.SunlitReef);
        Assert.Equal(Sea.Build((4, 2)).Heights, sea.Build((4, 2)).Heights);
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
    public void A_chunk_holding_a_treasure_may_be_sealed_by_its_own_wall_till_it_opens()
    {
        var all = ChunksWithin(14).Select(Sea.Build).ToList();
        var shut = all.Where(c => c.Sealed).ToList();
        Assert.NotEmpty(shut);
        foreach (var c in shut)
        {
            Assert.True(c.Place is PoiKind.TreasureCave or PoiKind.ShellCache or PoiKind.Relic && c.Key != Sea.Lair);
            Assert.NotEmpty(c.Sockets);
            foreach (var (at, way, _, _) in c.Sockets)
            {
                var inside = at - way * OpenSea.SealThick;
                Assert.True(c.At((int)inside.X, (int)inside.Y) >= OpenSea.SealTop);
            }
        }
        // Opened: its own heights again, the way through.
        var one = shut[0];
        var (wayAt, wayOut, _, _) = one.Sockets[0];
        one.Unseal();
        Assert.False(one.Sealed);
        var through = wayAt - wayOut * OpenSea.SealThick;
        Assert.True(one.At((int)through.X, (int)through.Y) < LevelMap.SwimBand);
        // And its wall gone from the padding of the chunk beyond the way (its ground drawn to meet it).
        var beyond = Sea.Build(Sea.ChunkAt(wayAt + wayOut * 0.5f));
        Assert.True(beyond.At((int)through.X, (int)through.Y) >= OpenSea.SealTop);
        Sea.UnsealAround(one, beyond);
        Assert.Equal(one.At((int)through.X, (int)through.Y), beyond.At((int)through.X, (int)through.Y));
        // Some chunks hold kelp beds, rooted on their floor, and the window takes those wholly in it.
        var kelp = all.Where(c => c.Kelp.Count > 0).ToList();
        Assert.NotEmpty(kelp);
        Assert.All(kelp.SelectMany(c => c.Kelp.SelectMany(b => b.Roots.Select(r => (c, r)))), x => Assert.True(x.c.Contains(x.r)));
        var map = new LevelMap();
        Sea.Fill(map, new Vector2(-54f), k => all.FirstOrDefault(c => c.Key == k));
        Assert.All(map.Kelp, b => Assert.InRange(b.Center.X, b.Radius, map.Size - b.Radius));
    }

    [Fact]
    public void A_secret_rooms_seal_leaves_and_comes_back_with_its_chunk_as_she_left_it()
    {
        var catalog = OctoShoots.Core.Items.ItemCatalog.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "data", "items.json")));
        var built = ChunksWithin(12).ToDictionary(k => k, Sea.Build);
        var map = new LevelMap { Templated = true, Seed = 7 };
        var origin = new Vector2(-54f, -54f);
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = new Vector2(55f), Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(55f), Radius = 6f });
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var w = new PlaneWorld(map, new Tuning(), new PlaneRun(catalog, new Tuning()), new PlaneOptions { OpenSea = true, Corruption = false, Adventure = false });
        // A chunk in the window, as if sealed (its ways sealed by the sim's walls).
        var c = built.Values.First(c => c.Min.X - origin.X >= 0f && c.Min.Y - origin.Y >= 0f && c.Max.X - origin.X <= 54f && c.Max.Y - origin.Y <= 110f && c.Sockets.Count > 0);
        w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null, c.Sockets.Select(s => (s.At - origin, s.Way, s.Half, s.Own)).ToList(), @sealed: true);
        Assert.Equal(c.Sockets.Count, w.Seals.Count);
        var seal = w.Seals[0];
        Assert.Contains(seal.Rock, w.WeakRocks);
        w.DebugOpenSeal(seal);
        // The room opens whole: its walls are one set of its chunk's heights.
        Assert.All(w.Seals, s => Assert.True(s.Open));
        Assert.All(w.Seals, s => Assert.DoesNotContain(s.Rock, w.WeakRocks));
        // Far on: it goes with its chunk, its wall too; and back, open still, where it was.
        var by = new Vector2(4 * OpenSea.Cell, 0f);
        var emblemInSea = seal.Emblem + origin;
        Sea.Fill(map, origin + by, k => built.GetValueOrDefault(k));
        w.ShiftWindow(by);
        Assert.Empty(w.Seals);
        Assert.Empty(w.WeakRocks);
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        w.ShiftWindow(-by);
        w.AddSeaLife(c.Min - origin, c.Max - origin, c.Min, null, c.Sockets.Select(s => (s.At - origin, s.Way, s.Half, s.Own)).ToList(), @sealed: true);
        Assert.Equal(c.Sockets.Count, w.Seals.Count);
        Assert.Same(seal, w.Seals[0]);
        Assert.True(seal.Open);
        Assert.DoesNotContain(seal.Rock, w.WeakRocks);
        Assert.Equal(emblemInSea, seal.Emblem + origin);
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

    [Fact]
    public void Some_chunks_hold_an_arch_over_a_passage_or_a_roof_over_their_room_and_the_window_takes_them()
    {
        var built = ChunksWithin(10).ToDictionary(k => k, Sea.Build);
        var arches = built.Values.SelectMany(c => c.Canopies.Where(a => a.Kind == CanopyKind.Arch).Select(a => (c, a))).ToList();
        var roofed = built.Values.Where(c => c.Lid is not null).ToList();
        Assert.NotEmpty(arches);
        Assert.NotEmpty(roofed);
        // An arch springs from rock either side, inside its chunk, over water.
        foreach (var (c, a) in arches)
        {
            Assert.True(c.Contains(a.EndA) && c.Contains(a.EndB));
            Assert.True(c.At((int)MathF.Round(a.EndA.X), (int)MathF.Round(a.EndA.Y)) >= OpenSea.ArchFooting - 0.01f);
            Assert.True(c.At((int)MathF.Round(a.EndB.X), (int)MathF.Round(a.EndB.Y)) >= OpenSea.ArchFooting - 0.01f);
            Assert.InRange(a.HalfLength * 2f, OpenSea.ArchSpanMin, OpenSea.ArchSpanMax + 1f);
            Assert.True(a.Bottom > LevelMap.SwimBand);
        }
        // A roof covers its place's room, over its own heights, inside its chunk, with its canopy over it.
        foreach (var c in roofed)
        {
            Assert.True(c.Place is PoiKind.TreasureCave or PoiKind.ShellCache or PoiKind.Relic);
            var roof = Assert.Single(c.Canopies, x => x.Kind == CanopyKind.CaveRoof);
            Assert.Equal(c.PlaceAt, roof.Center);
            Assert.True(c.Lid![(int)(c.PlaceAt.Y - c.Origin.Y) * c.Stride + (int)(c.PlaceAt.X - c.Origin.X)] > LevelMap.SwimBand + 1f);
            for (int k = 0; k < c.Lid.Length; k++)
                if (!float.IsNaN(c.Lid[k]))
                {
                    var p = c.Origin + new Vector2(k % c.Stride, k / c.Stride);
                    Assert.True(c.Contains(p));
                    Assert.True(c.Lid[k] >= c.Heights[k]);
                }
        }
        // The window takes them, in its own coordinates.
        var one = roofed[0];
        var origin = new Vector2(MathF.Floor((one.PlaceAt.X - 55f) / OpenSea.Cell) * OpenSea.Cell, MathF.Floor((one.PlaceAt.Y - 55f) / OpenSea.Cell) * OpenSea.Cell);
        var more = ChunksAround(one.Key, 6).Where(k => !built.ContainsKey(k)).ToList();
        foreach (var k in more) built[k] = Sea.Build(k);
        var map = new LevelMap();
        Sea.Fill(map, origin, k => built.GetValueOrDefault(k));
        var at = one.PlaceAt - origin;
        Assert.False(float.IsNaN(map.LidAt((int)at.X, (int)at.Y)));
        Assert.Contains(map.Canopies, x => x.Kind == CanopyKind.CaveRoof && Vector2.Distance(x.Center, at) < 0.01f);
    }

    [Fact]
    public void Some_chunks_are_filled_with_rock_whole_by_half_or_by_a_quarter_and_every_chunks_ways_meet_in_its_water()
    {
        var keys = ChunksWithin(12).ToList();
        var fills = keys.ToDictionary(k => k, Sea.FillOf);
        foreach (var f in new[] { OpenSea.Filler.Full, OpenSea.Filler.Half, OpenSea.Filler.Quarter })
            Assert.Contains(fills.Values, v => v == f);
        foreach (var k in keys)
        {
            var c = Sea.Build(k);
            if (fills[k] == OpenSea.Filler.Full)
            {
                Assert.DoesNotContain(Sea.Neighbours(k), n => Sea.Linked(k, n));
                Assert.Null(Sea.PlaceOf(k));
                for (int y = (int)c.Min.Y; y < c.Max.Y; y++)
                for (int x = (int)c.Min.X; x < c.Max.X; x++)
                    Assert.True(c.At(x, y) > 0f, $"{k}: water at ({x}, {y}) in a chunk filled whole");
                continue;
            }
            // Its water, flooded from its first way: every way (unsealed) is reached, and the most of its rock rises past its foot.
            var heights = c.Unsealed ?? c.Heights;
            bool Water(int x, int y) => c.Contains(new Vector2(x, y)) && heights[(y - (int)c.Origin.Y) * c.Stride + x - (int)c.Origin.X] < 0f;
            var ways = c.Sockets.Select(s => s.At - s.Way * 1.5f).Select(p => ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y))).ToList();
            Assert.NotEmpty(ways);
            var reached = new HashSet<(int, int)>();
            var queue = new Queue<(int X, int Y)>();
            foreach (var (wx, wy) in ways.Take(1))
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (Water(wx + dx, wy + dy) && reached.Add((wx + dx, wy + dy))) queue.Enqueue((wx + dx, wy + dy));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (Water(x + dx, y + dy) && reached.Add((x + dx, y + dy))) queue.Enqueue((x + dx, y + dy));
            }
            foreach (var (wx, wy) in ways)
            {
                bool hit = false;
                for (int dy = -2; dy <= 2 && !hit; dy++)
                for (int dx = -2; dx <= 2 && !hit; dx++)
                    hit = reached.Contains((wx + dx, wy + dy));
                Assert.True(hit, $"{k} ({fills[k]}): its way at ({wx}, {wy}) not reached from its first");
            }
        }
    }

    static IEnumerable<(int X, int Y)> ChunksAround((int X, int Y) key, int reach)
    {
        var cell = Sea.RectOf(key).Min;
        var seen = new HashSet<(int, int)>();
        for (int y = -reach; y <= reach; y++)
        for (int x = -reach; x <= reach; x++)
        {
            var k = Sea.ChunkAt(cell + new Vector2(x, y) * OpenSea.Cell);
            if (seen.Add(k)) yield return k;
        }
    }
}

/// <summary>Timings, run with nothing else beside them (what they measure is the build, not the other tests).</summary>
[CollectionDefinition("Alone", DisableParallelization = true)]
public class AloneCollection;

[Collection("Alone")]
public class OpenSeaTimingTests
{
    [Fact]
    public void A_chunk_is_quick_to_build()
    {
        var sea = new OpenSea(0xC0FFEEUL, Biome.SunlitReef);
        var keys = OpenSeaTests.ChunksWithin(6).ToList();
        foreach (var k in keys.Take(3)) sea.Build(k);
        var watch = Stopwatch.StartNew();
        foreach (var k in keys) sea.Build(k);
        double each = watch.Elapsed.TotalMilliseconds / keys.Count;
        Assert.True(each < 25, $"{each:0.0} ms a chunk");
    }
}
