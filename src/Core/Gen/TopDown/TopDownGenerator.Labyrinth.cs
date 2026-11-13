using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>Labyrinths (docs/DIVE.md §6.1): a secret room's sub-level, a maze of reef walls with its reward at its heart.</summary>
public static class LabyrinthTuning
{
    /// <summary>Metres from one cell's middle to the next; a passage's open half-width, and how much its walls waver from it.</summary>
    public const float Pitch = 8f, HalfWidth = 2f, Waver = 0.35f;
    /// <summary>The open round its heart (the reward) and round its entrance cell.</summary>
    public const float HeartRadius = 3.2f, EntranceRadius = 2.6f;
    /// <summary>Its cells a side: Base + its depth, never fewer than Least nor more than Most.</summary>
    public const int Base = 8, Least = 9, Most = 11;
    /// <summary>Attempts at one before it gives up (each is checked whole: the heart reached from the way in).</summary>
    public const int Attempts = 8;

    public static int CellsFor(int depth) => Math.Clamp(Base + depth, Least, Most);
}

public static partial class TopDownGenerator
{
    /// <summary>
    /// A secret room's labyrinth (docs/DIVE.md §6.1): a maze (Maze) of passages LabyrinthTuning.HalfWidth either side of their
    /// line between cells Pitch apart, their walls the reef's own rock (raised, terraced and eased as any level's), in the
    /// middle of the square with solid rock round it. Its way in and out (Way) is cut in its entrance cell's outer wall,
    /// going the way she swam into the mouth (as a leak cave's is); its heart, the dead end farthest from it, is the
    /// secret's place (its shells, or now and then a pearl: PlaneWorld.PlaceShells). No leak, no shaft, no creatures; the
    /// look of the reef it lies under (LookDepth). Every attempt is checked whole, the heart swum to from the way in;
    /// the maze itself is always whole, so the first attempt holds unless a wall's wavering pinches a passage shut.
    /// </summary>
    /// <param name="into">The way she swam into the mouth she comes in by (null: a wall facing the camera).</param>
    public static LevelMap GenerateLabyrinth(SeedCode seed, LevelId id, int lookDepth, Vector2? into = null)
    {
        var streams = new RunStreams(seed);
        var toward = into is { } went && went.LengthSquared() > 1e-6f ? -Vector2.Normalize(went) : CameraWard;
        // (Its entrance on the side of the square the way in faces.)
        var side = MathF.Abs(toward.X) > MathF.Abs(toward.Y) ? new Vector2(MathF.Sign(toward.X), 0f) : new Vector2(0f, MathF.Sign(toward.Y));
        var reasons = new List<string>();
        for (int attempt = 0; attempt < LabyrinthTuning.Attempts; attempt++)
        {
            var map = BuildLabyrinth(streams, id, lookDepth, side, attempt, out string? why);
            if (map is not null) return map;
            reasons.Add(why ?? "?");
        }
        throw new InvalidOperationException($"No labyrinth for {id} after {LabyrinthTuning.Attempts} attempts: {string.Join("; ", reasons)}");
    }

    static LevelMap? BuildLabyrinth(RunStreams streams, LevelId id, int lookDepth, Vector2 side, int attempt, out string? why)
    {
        Rng Stage(string name) => streams.TopDown(id, attempt, "labyrinth/" + name);
        var map = new LevelMap { Seed = streams.Seed.Value, Id = id, Attempt = attempt, Labyrinth = true, LookDepth = lookDepth, Shaft = NoShaft };
        Array.Fill(map.Heights, -PathDepth);
        int cells = LabyrinthTuning.CellsFor(id.Depth);
        const float P = LabyrinthTuning.Pitch;
        float left = (map.Size - cells * P) * 0.5f;
        Vector2 At(int x, int y) => new(left + (x + 0.5f) * P, left + (y + 0.5f) * P);
        // Its entrance: a cell along the side the way in faces, in its middle third.
        var pick = Stage("entrance");
        int along = cells / 3 + pick.Int(Math.Max(1, cells - 2 * (cells / 3)));
        (int X, int Y) entrance = side.X > 0 ? (cells - 1, along) : side.X < 0 ? (0, along) : side.Y > 0 ? (along, cells - 1) : (along, 0);
        var maze = Maze.Build(cells, cells, entrance, Stage("maze"));

        // The open water: every passage, every cell's middle, the heart and the entrance a little wider. The walls waver.
        int N = map.Samples;
        var open = new bool[N * N];
        uint waver = (uint)(Stage("waver").NextU64() >> 32);
        float Half(int x, int y) => LabyrinthTuning.HalfWidth + LabyrinthTuning.Waver * TerrainNoise.Fbm(x * 0.21f, y * 0.21f, waver, 2);
        void Capsule(Vector2 a, Vector2 b, float extra)
        {
            float reach = LabyrinthTuning.HalfWidth + LabyrinthTuning.Waver + extra + 1f;
            int x0 = Math.Max(0, (int)(MathF.Min(a.X, b.X) - reach)), x1 = Math.Min(map.Size, (int)(MathF.Max(a.X, b.X) + reach) + 1);
            int y0 = Math.Max(0, (int)(MathF.Min(a.Y, b.Y) - reach)), y1 = Math.Min(map.Size, (int)(MathF.Max(a.Y, b.Y) + reach) + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (Geo.SegmentDistance(new Vector2(x, y), a, b) <= Half(x, y) + extra) open[y * N + x] = true;
        }
        foreach (var (x, y, dx, dy) in maze.Joins()) Capsule(At(x, y), At(x + dx, y + dy), 0f);
        for (int y = 0; y < cells; y++)
        for (int x = 0; x < cells; x++)
            Capsule(At(x, y), At(x, y), 0f);
        var heart = At(maze.Heart.X, maze.Heart.Y);
        var start = At(entrance.X, entrance.Y);
        Capsule(heart, heart, LabyrinthTuning.HeartRadius - LabyrinthTuning.HalfWidth);
        Capsule(start, start, LabyrinthTuning.EntranceRadius - LabyrinthTuning.HalfWidth);

        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = start, Radius = LabyrinthTuning.EntranceRadius });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = heart, Radius = LabyrinthTuning.HeartRadius });
        map.Pois.Add(new Poi { Kind = PoiKind.Secret, Position = heart, Radius = LabyrinthTuning.HeartRadius });

        uint noiseSeed = (uint)(Stage("noise").NextU64() >> 32);
        var strata = new WallStrata(Stage("strata"));
        var tops = RaiseCaveWalls(map, open, $"{streams.Seed} maze {id.Cycle}-{id.Depth}-{id.Level}-L{id.Lane}-{attempt}", Stage("topo"), strata);
        FlattenSeabed(map);
        SoftenSlopes(map, Stage("slopes"), strata);
        ClampHeights(map);
        // Its way in and out, in the entrance cell's outer wall (out toward the side it faces).
        map.Way = CutDoorway(map, start, P, side, 0.85f);
        if (map.Way is null)
        {
            why = "no room for its way in";
            return null;
        }
        Decorate(map, Stage("cosmetic"), noiseSeed, strata);
        ClearAround(map, heart, LabyrinthTuning.HeartRadius);
        Mountains.Fill(map);
        // Checked whole: the heart is swum to from the way in.
        if (LevelValidator.ShortestPath(map, map.Way.Value.Inside, heart, clearance: 0.6f).Count == 0)
        {
            why = "its heart cannot be reached";
            return null;
        }
        why = null;
        _ = tops;
        return map;
    }
}
