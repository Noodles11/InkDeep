using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using OctoShoots.Core.Gen.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// A dive's cube reef in the game (CUBE-WORLD-PLAN §15.1): its space, every cube's terrain made once (on any thread) and
/// kept while its layer is, and each layer stitched into one <see cref="LevelMap"/> as wide as the footprint, so the game's
/// own systems (collision, her light, the maps, the creatures to come) read a layer as they read a level. Squares of the
/// footprint outside the reef are rock.
/// </summary>
public sealed class CubeReef
{
    /// <summary>The height a square outside the reef stands at (the rock's own top).</summary>
    public const float OutsideHeight = RockLow + RockRim;

    public CubeReef(ulong seed, int footprint = Footprint, int layers = Layers)
    {
        Space = new CubeSpace(seed, footprint, layers);
        // Where a dive begins: a gate's mouth of the reef's cube nearest its north-west corner.
        Start = Terrain(Space.CubesOf(0).OrderBy(c => c.Middle.Length()).First()).Gates[0].Mouth;
    }

    public CubeSpace Space { get; }
    public Vector2 Start { get; }
    public int Size => Space.Footprint * Side;

    readonly ConcurrentDictionary<CubeId, Lazy<CubeTerrain>> _made = new();

    /// <summary>A cube's terrain, made the first time it is asked for (once, whichever thread asks).</summary>
    public CubeTerrain Terrain(CubeId c) =>
        _made.GetOrAdd(c, id => new Lazy<CubeTerrain>(() => CubeTerrain.Make(Space, id), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>A cube's terrain if it is made already (never makes it).</summary>
    public CubeTerrain? Made(CubeId c) => _made.TryGetValue(c, out var l) && l.IsValueCreated ? l.Value : null;

    /// <summary>A layer's terrains let go (a layer she can never return to).</summary>
    public void Forget(int z)
    {
        foreach (var c in _made.Keys.Where(c => c.Z == z).ToList()) _made.TryRemove(c, out _);
    }

    /// <summary>The level a layer counts as: its depth (the warm-up layer the first after the tutorial's), level 1.</summary>
    public static LevelId IdOf(int z) => new(1, LevelPlan.Tutorial + 1 + z, 1);

    /// <summary>The layer a level of the reef is.</summary>
    public static int LayerOf(LevelId id) => id.Depth - LevelPlan.Tutorial - 1;

    /// <summary>The depth whose look a layer wears: the shallows' for the warm-up layer, then the next every layer to the fourth.</summary>
    public static int LookDepthOf(int z) => LevelPlan.Tutorial + 1 + Math.Min(z, 3);

    /// <summary>
    /// A layer as one map: every cube's terrain made (in parallel), its heights laid onto the footprint's grid, her start
    /// on it at <paramref name="start"/>. Pure work: safe on any thread.
    /// </summary>
    public LevelMap Layer(int z, Vector2 start)
    {
        var cubes = Space.CubesOf(z).ToList();
        Parallel.ForEach(cubes, c => Terrain(c));
        var map = new LevelMap(Size)
        {
            Seed = Space.Seed, Id = IdOf(z), LookDepth = LookDepthOf(z),
            // The warm-up layer is bright and its menace low (§9); below, the menace grows a layer at a time.
            Menace = Math.Min(1f, z / 8f),
            Title = z == 0 ? "The warm-up reef" : $"Layer {z}",
        };
        Array.Fill(map.Heights, OutsideHeight);
        int last = Size;
        foreach (var c in cubes)
        {
            var t = Terrain(c);
            int x0 = (int)c.Origin.X, y0 = (int)c.Origin.Y, x1 = Math.Min(x0 + (int)c.Size.X, last), y1 = Math.Min(y0 + (int)c.Size.Y, last);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                map[x, y] = t.HeightAt(x, y);
        }
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = start, Radius = 3f });
        // (No exit: a layer is left by diving or climbing. The game's code asks for one, far off the reef.)
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(-1000f, -1000f), Radius = 1f });
        return map;
    }
}
