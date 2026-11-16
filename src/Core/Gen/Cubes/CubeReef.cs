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
/// A dive's cube reef in the game (CUBE-WORLD-PLAN §15.1): its space, each cube's terrain made the first time it is asked
/// for (on any thread: the stream asks as she swims) and kept while its layer is, and each layer one <see cref="LevelMap"/>
/// as wide as the window, so the game's own systems (collision, her light, the maps, the creatures) read a layer as they
/// read a level. A layer's map begins as rock; each cube is laid onto it once made (<see cref="Settle"/>), so it holds
/// only the cubes she has come near.
/// </summary>
public sealed class CubeReef
{
    /// <summary>The height a layer's map stands at where no cube is laid (the rock's own top).</summary>
    public const float OutsideHeight = RockLow + RockRim;

    public CubeReef(ulong seed, int window = Window, int layers = Layers)
    {
        Space = new CubeSpace(seed, window, layers);
        // Where a dive begins: a gate's mouth of the top layer's cube in the window's middle.
        Start = Terrain(CubeId.Of(Space.Middle(0))).Gates[0].Mouth;
    }

    public CubeSpace Space { get; }
    public Vector2 Start { get; }
    public int Size => Space.Window * Side;

    readonly ConcurrentDictionary<CubeId, Lazy<CubeTerrain>> _made = new();
    /// <summary>Cubes made and not yet laid onto their layer's map.</summary>
    readonly ConcurrentQueue<CubeId> _fresh = new();
    /// <summary>The layers' maps, each with the cubes laid on it (guarded by itself).</summary>
    readonly Dictionary<int, (LevelMap Map, HashSet<CubeId> Laid)> _maps = new();

    /// <summary>A cube's terrain, made the first time it is asked for (once, whichever thread asks); a layer's arena with its basin.</summary>
    public CubeTerrain Terrain(CubeId c) =>
        _made.GetOrAdd(c, id => new Lazy<CubeTerrain>(() =>
        {
            var t = CubeTerrain.Make(Space, id, IsArena(id));
            _fresh.Enqueue(id);
            return t;
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>
    /// The cubes made since the last call laid onto their layers' maps (on the thread that runs the game: its systems read
    /// the maps), at most <paramref name="most"/> of them (the rest left for the next call, so no one frame lays many);
    /// those laid.
    /// </summary>
    public List<CubeId> Settle(int most = int.MaxValue)
    {
        var laid = new List<CubeId>();
        lock (_maps)
            while (laid.Count < most && _fresh.TryDequeue(out var c))
                // (Its terrain read whole: queued as it was made, it may be a moment from being handed out.)
                if (_maps.TryGetValue(c.Z, out var layer) && _made.TryGetValue(c, out var made) && layer.Laid.Add(c))
                {
                    Lay(layer.Map, c, made.Value);
                    laid.Add(c);
                }
        return laid;
    }

    /// <summary>The cubes laid on a layer's map so far.</summary>
    public IReadOnlyList<CubeId> LaidOn(int z)
    {
        lock (_maps) return _maps.TryGetValue(z, out var layer) ? layer.Laid.ToList() : new List<CubeId>();
    }

    readonly ConcurrentDictionary<int, CubeId> _arenas = new();

    /// <summary>A layer's boss arena (CUBE-WORLD-PLAN §9.1), if it has one.</summary>
    public CubeId? ArenaOf(int z) => _arenas.TryGetValue(z, out var c) ? c : null;
    public bool IsArena(CubeId c) => _arenas.TryGetValue(c.Z, out var a) && a == c;

    /// <summary>
    /// A layer's arena fixed (as she arrives on it, before its cube is ever drawn): its terrain made again with its basin,
    /// and laid over the layer's map.
    /// </summary>
    public void SetArena(CubeId cube, LevelMap layer)
    {
        _arenas[cube.Z] = cube;
        _made.TryRemove(cube, out _);
        var t = Terrain(cube);
        lock (_maps)
        {
            Lay(layer, cube, t);
            if (_maps.TryGetValue(cube.Z, out var kept) && kept.Map == layer) kept.Laid.Add(cube);
        }
    }

    /// <summary>A layer's arena as a save kept it (before the layer is made: its map is made with the basin).</summary>
    public void RestoreArena(CubeId cube)
    {
        _arenas[cube.Z] = cube;
        _made.TryRemove(cube, out _);
    }

    /// <summary>A cube's heights (and the rock over its sockets, gates and tunnels) laid onto its layer's map.</summary>
    void Lay(LevelMap map, CubeId c, CubeTerrain t)
    {
        int last = Size;
        int x0 = (int)c.Origin.X, y0 = (int)c.Origin.Y, x1 = Math.Min(x0 + (int)c.Size.X, last), y1 = Math.Min(y0 + (int)c.Size.Y, last);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            map[x, y] = t.HeightAt(x, y);
            // (The rock over its sockets' passages, drawn by the mouths' views as a level's.)
            if (t.Roof is not null && t.RoofAt(x, y) is var roof && !float.IsNaN(roof)) map.Roof[y * map.Samples + x] = roof;
            // (The roofs over its gates and tunnels, as rock over the water to the cutout: LevelView.SeeKeep.)
            else if (t.CanopyAt(x, y) && t.CellAt(x, y) != CubeCell.Rock) map.Roof[y * map.Samples + x] = RoofHeight;
        }
    }

    /// <summary>A cube's terrain if it is made already (never makes it).</summary>
    public CubeTerrain? Made(CubeId c) => _made.TryGetValue(c, out var l) && l.IsValueCreated ? l.Value : null;

    /// <summary>A layer's terrains let go (a layer she can never return to).</summary>
    public void Forget(int z)
    {
        foreach (var c in _made.Keys.Where(c => c.Z == z).ToList()) _made.TryRemove(c, out _);
        lock (_maps) _maps.Remove(z);
    }

    /// <summary>The level a layer counts as: its depth (the warm-up layer the first after the tutorial's), level 1.</summary>
    public static LevelId IdOf(int z) => new(1, LevelPlan.Tutorial + 1 + z, 1);

    /// <summary>The layer a level of the reef is.</summary>
    public static int LayerOf(LevelId id) => id.Depth - LevelPlan.Tutorial - 1;

    /// <summary>The depth whose look a layer wears: the shallows' for the warm-up layer, then the next every layer to the fourth.</summary>
    public static int LookDepthOf(int z) => LevelPlan.Tutorial + 1 + Math.Min(z, 3);

    /// <summary>
    /// A layer as one map as wide as the window: rock, with the cubes of it made so far laid on it (the rest as they are
    /// made: <see cref="Settle"/>), her start on it at <paramref name="start"/>; it is the layer's map from then on. Safe
    /// on any thread.
    /// </summary>
    public LevelMap Layer(int z, Vector2 start)
    {
        var map = new LevelMap(Size)
        {
            Seed = Space.Seed, Id = IdOf(z), LookDepth = LookDepthOf(z),
            // The warm-up layer is bright and its menace low (§9); below, the menace grows a layer at a time.
            Menace = Math.Min(1f, z / 8f),
            Title = z == 0 ? "The warm-up reef" : $"Layer {z}",
        };
        Array.Fill(map.Heights, OutsideHeight);
        lock (_maps)
        {
            var laid = new HashSet<CubeId>();
            foreach (var (c, made) in _made)
                if (c.Z == z && made.IsValueCreated)
                {
                    Lay(map, c, made.Value);
                    laid.Add(c);
                }
            _maps[z] = (map, laid);
        }
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = start, Radius = 3f });
        // (No exit: a layer is left by diving or climbing. The game's code asks for one, far off the reef.)
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(-1000f, -1000f), Radius = 1f });
        // (Nor a shaft: one far off it, so nothing on the layer counts as standing in it.)
        map.Shaft = new Shaft(new Vector2(-1000f, -1000f), Vector2.UnitX, 1f, 1f);
        return map;
    }
}
