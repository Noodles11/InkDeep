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
