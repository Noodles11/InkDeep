using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// An open level (docs/GAME.md §5, Open levels; OpenSea): an endless sea made chunk by chunk as she swims. Only the chunk
/// she starts or lands in and those round it are made with the level (off the main thread); as she comes into a chunk,
/// those round it are made on workers and each, when done, is shown in a frame of its own (its mesh, and its heights in
/// the sim's window). The sim's map is a window onto the sea that follows her: when she is a cell from its middle it
/// moves on a cell, and the whole world with it (as a dive's arrival moves it back to the origin), so no coordinate ever
/// grows large.
/// </summary>
public partial class TopDownMain
{
    OpenSea? Sea => _levelShape?.Sea;
    /// <summary>The window's corner in the sea (sea metres, whole cells).</summary>
    System.Numerics.Vector2 _seaW;
    readonly Dictionary<(int X, int Y), OpenSea.Chunk> _seaBuilt = new();
    readonly Dictionary<(int X, int Y), Task<(OpenSea.Chunk, LevelShape.Surface?, LevelView.SeaDecor)>> _seaPending = new();
    /// <summary>The chunks she has been in, the one she is in, and the holes she has found (sea metres).</summary>
    readonly HashSet<(int X, int Y)> _seaVisited = new();
    (int X, int Y)? _seaHere;
    readonly List<System.Numerics.Vector2> _seaFound = new();
    SeaChunkMap _seaMap = null!;
    /// <summary>Verification (--dbg-explore): she swims on from chunk to chunk through their ways, the unvisited first.</summary>
    bool _dbgExplore;

    /// <summary>Whether a level is an open one: a crossover template level but a boss's.</summary>
    static bool OpenLevel(SeedCode seed, LevelId id) =>
        !_dbgClosed && Crossover.On && !id.Haven && LevelPlan.For(seed, id, crossover: true) is var plan && (plan is { Templated: true, HasBoss: false } || plan.Rift);

    /// <summary>
    /// Which slice of the run's one reef a level is (ReefField's y): one step down for each level from the run's start,
    /// so a level's reef is the one above's, a step deeper.
    /// </summary>
    static int SliceOf(LevelId id) =>
        (id.Cycle - 1) * 1000 + (id.Depth <= LevelPlan.Tutorial ? 0 : LevelStocking.TutorialLevels + (id.Depth - LevelPlan.Tutorial - 1) * LevelStocking.LevelsPerDepth) + id.Level;

    /// <summary>
    /// An open level made: its sea (seeded by the run and the level), the chunk of her start and those round it, and
    /// the sim's window round her start. Pure: any thread.
    /// </summary>
    static LevelShape SeaShape(SeedCode seed, LevelId id, System.Numerics.Vector2? at = null, IEnumerable<(int X, int Y)>? also = null)
    {
        var plan = LevelPlan.For(seed, id, crossover: true);
        // (Its reef the run's one volume, this level's slice of it: dived into at a spot, the level below lies under it.)
        var sea = new OpenSea(new RunStreams(seed).TopDown(id, 0, "open sea").NextU64(), plan.Biome, reef: seed.Value, slice: SliceOf(id));
        var middle = at ?? new System.Numerics.Vector2(OpenSea.Cell / 2f);
        var home = sea.ChunkAt(middle);
        var chunks = Around(sea, home).Concat(also ?? Enumerable.Empty<(int X, int Y)>()).Distinct().AsParallel().Select(k => BuildSeaChunk(sea, k)).ToList();
        var built = chunks.ToDictionary(c => c.Item1.Key, c => c.Item1);
        // Her start: the open water nearest where she dived (or the middle of the first cell).
        var start = middle;
        float best = float.MaxValue;
        var first = built[home];
        for (int y = (int)first.Min.Y; y < first.Max.Y; y++)
        for (int x = (int)first.Min.X; x < first.Max.X; x++)
        {
            var p = new System.Numerics.Vector2(x, y);
            float d = System.Numerics.Vector2.Distance(p, middle);
            if (d < best && first.At(x, y) < -0.7f && first.At(x + 1, y) < -0.7f && first.At(x - 1, y) < -0.7f && first.At(x, y + 1) < -0.7f && first.At(x, y - 1) < -0.7f)
            {
                best = d;
                start = p;
            }
        }
        var w = WindowFor(start);
        var map = new LevelMap { Seed = seed.Value, Id = id, Templated = true, Biome = plan.Biome, Menace = plan.Menace, AreaScale = plan.AreaScale };
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = start - w, Radius = 6f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = start - w, Radius = 6f });
        sea.Fill(map, w, k => built.GetValueOrDefault(k));
        return LevelShape.OfSea(map, sea, w, chunks);
    }

    /// <summary>How far past the window an open level's sun map reaches on every side (SunMap.Wide).</summary>
    const int SunMargin = 2 * (int)OpenSea.Cell;

    /// <summary>The sea round the window, SunMargin past it on every side, from the chunks made there (SunMap.Wide).</summary>
    (LevelMap Map, float Margin) WideSea()
    {
        var wide = new LevelMap(LevelMap.Seeded + 2 * SunMargin);
        Sea!.Fill(wide, _seaW - new System.Numerics.Vector2(SunMargin), k => _seaBuilt.GetValueOrDefault(k));
        return (wide, SunMargin);
    }

    /// <summary>The window round a point: its corner a whole number of cells, the point within a cell of its middle.</summary>
    static System.Numerics.Vector2 WindowFor(System.Numerics.Vector2 at)
    {
        float half = LevelMap.Seeded / 2f;
        return new System.Numerics.Vector2(Mathf.Round((at.X - half) / OpenSea.Cell) * OpenSea.Cell, Mathf.Round((at.Y - half) / OpenSea.Cell) * OpenSea.Cell);
    }

    /// <summary>How many cells round her chunk are made ahead: enough that the screen (about ±35 m across, ±20 m up and down) never reaches past them.</summary>
    const int SeaReach = 2;

    /// <summary>A chunk and those round it (every chunk within SeaReach cells of it, corners too).</summary>
    static IEnumerable<(int X, int Y)> Around(OpenSea sea, (int X, int Y) key)
    {
        var cells = sea.CellsOf(key);
        var seen = new HashSet<(int X, int Y)>();
        int x0 = cells.Min(c => c.X) - SeaReach, x1 = cells.Max(c => c.X) + SeaReach, y0 = cells.Min(c => c.Y) - SeaReach, y1 = cells.Max(c => c.Y) + SeaReach;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            if (seen.Add(sea.ChunkOfCell((x, y)))) yield return sea.ChunkOfCell((x, y));
    }

    static (OpenSea.Chunk, LevelShape.Surface?, LevelView.SeaDecor) BuildSeaChunk(OpenSea sea, (int X, int Y) key)
    {
        var chunk = sea.Build(key);
        return (chunk, LevelView.SeaSurface(chunk), LevelView.GrowSeaDecor(sea, chunk));
    }

    /// <summary>A chunk's decoration drawn with it (its biome's things in the level's skins).</summary>
    void ShowSeaDecor(OpenSea.Chunk chunk, LevelView.SeaDecor decor) => _level.ShowSeaDecor(chunk, decor, _props.Build(decor.Props));

    /// <summary>An open level entered: what was made with it, and nothing found yet.</summary>
    void EnterSea()
    {
        _seaBuilt.Clear();
        _seaOpened.Clear();
        _seaPending.Clear();
        _seaVisited.Clear();
        _seaFound.Clear();
        _seaLife.Clear();
        _seaHere = null;
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("reef_rim"), Sea is null ? 1f : 0f);
        OctoShoots.Game.Fx.SunMap.Wide = Sea is null ? null : WideSea;
        // (F3's map of the window is hidden on an open level: its chunk map stands in for it.)
        _debugMap.SelfModulate = Sea is null ? Colors.White : Colors.Transparent;
        if (_levelShape is not { Sea: not null } shape) return;
        _seaW = shape.SeaOrigin;
        foreach (var (chunk, _, _) in shape.SeaChunks) _seaBuilt[chunk.Key] = chunk;
        // (Its sun map cast wide at once, past the window.)
        OctoShoots.Game.Fx.SunMap.Shifted(_map, OctoShoots.Game.Fx.SunLight.Direction, Vector3.Zero);
    }

    /// <summary>Once a frame on an open level: the chunks round hers asked for, one made chunk shown, the window moved on, the holes she has found.</summary>
    void StepSea()
    {
        if (Sea is not { } sea || _diving) return;
        var her = _world.Player.Position;
        var here = sea.ChunkAt(her + _seaW);
        if (here != _seaHere)
        {
            _seaHere = here;
            _seaVisited.Add(here);
            foreach (var k in Around(sea, here))
                if (!_seaBuilt.ContainsKey(k) && !_seaPending.ContainsKey(k))
                    _seaPending[k] = Task.Run(() => BuildSeaChunk(sea, k));
        }
        // One made chunk a frame: its mesh shown, its heights into the window.
        foreach (var (key, task) in _seaPending)
        {
            if (!task.IsCompleted) continue;
            _seaPending.Remove(key);
            if (task.IsFaulted)
            {
                GD.PushError(task.Exception?.ToString());
                break;
            }
            var (chunk, ground, decor) = task.Result;
            _seaBuilt[key] = chunk;
            // (A secret room opened before it came: the room's walls gone from its padding too.)
            bool touched = false;
            foreach (var room in _seaOpened)
                if (Touches(_seaBuilt[room], chunk))
                {
                    sea.UnsealAround(_seaBuilt[room], chunk);
                    touched = true;
                }
            if (touched) ground = LevelView.SeaSurface(chunk);
            _level.ShowSeaChunk(chunk, ground);
            ShowSeaDecor(chunk, decor);
            AddSeaPlace(chunk);
            var (min, max) = (chunk.Min - _seaW, chunk.Max - _seaW);
            if (max.X > 0f && max.Y > 0f && min.X < LevelMap.Seeded && min.Y < LevelMap.Seeded)
            {
                sea.Fill(_map, _seaW, k => _seaBuilt.GetValueOrDefault(k), key);
                OctoShoots.Game.Fx.SunMap.Shifted(_map, OctoShoots.Game.Fx.SunLight.Direction, Vector3.Zero);
                AddSeaLife(moved: true);
            }
            break;
        }
        if (_dbgSecret && DebugSecret()) { }
        else if (_dbgExplore && (_route.Count == 0 || _routeProgress >= _route.Count - 3)) Explore(sea, here);
        // The window moves on a cell once she is a cell from its middle.
        var off = her - new System.Numerics.Vector2(LevelMap.Seeded / 2f);
        int dx = off.X > OpenSea.Cell ? 1 : off.X < -OpenSea.Cell ? -1 : 0, dy = off.Y > OpenSea.Cell ? 1 : off.Y < -OpenSea.Cell ? -1 : 0;
        if (dx != 0 || dy != 0) ShiftSea(new System.Numerics.Vector2(dx, dy) * OpenSea.Cell);
        foreach (var hole in _map.Holes)
        {
            var at = hole.Shaft.Center + _seaW;
            if (hole.Kind != ReefKind.Boss && System.Numerics.Vector2.Distance(her, hole.Shaft.Center) < 7f && !_seaFound.Contains(at)) _seaFound.Add(at);
        }
        // (F3 shows the chunk map in place of the window's own, which moves.)
        _seaMap.Visible = _debugMap.Visible;
        if (_seaMap.Visible) _seaMap.Sync(sea, her + _seaW, here, _seaVisited, _seaBuilt, _seaFound);
    }

    /// <summary>A chunk's place (OpenSea.PlaceRules) into the sim, once: a hole is the map's own.</summary>
    void AddSeaPlace(OpenSea.Chunk chunk)
    {
        if (chunk.Place is { } kind && kind != PoiKind.Hole) _world.AddSeaPlace(kind, chunk.PlaceAt - _seaW, chunk.PlaceAt);
    }

    /// <summary>The places of the chunks made with the level, once its sim is made (its views are shown after).</summary>
    void AddSeaPlaces()
    {
        if (_levelShape is not { Sea: not null } shape) return;
        foreach (var (chunk, _, _) in shape.SeaChunks) AddSeaPlace(chunk);
        AddSeaLife();
    }

    /// <summary>The chunks whose life has come in (each once, when it first lies whole in the window).</summary>
    readonly HashSet<(int X, int Y)> _seaLife = new();

    /// <summary>Each made chunk lying whole in the window, its life not yet come in: its fish (PlaneWorld.AddSeaLife).</summary>
    void AddSeaLife(bool moved = false)
    {
        bool added = moved;
        foreach (var (key, chunk) in _seaBuilt)
        {
            var (min, max) = (chunk.Min - _seaW, chunk.Max - _seaW);
            // (Out of the window altogether, its fish are gone from the sim: they come back with it.)
            if (max.X <= 0f || max.Y <= 0f || min.X >= LevelMap.Seeded || min.Y >= LevelMap.Seeded) _seaLife.Remove(key);
            if (min.X < 0f || min.Y < 0f || max.X > LevelMap.Seeded || max.Y > LevelMap.Seeded || !_seaLife.Add(key)) continue;
            _world.AddSeaLife(min, max, chunk.Min, chunk.Place is null ? null : chunk.PlaceAt - _seaW,
                chunk.Sockets.Select(s => (s.At - _seaW, s.Way, s.Half, s.Own)).ToList(), chunk.Sealed);
            added = true;
        }
        // (The light features' views are drawn where they lie: shown again as they come, go and move.)
        if (!added || _opticWalls is null) return;
        _opticWalls.Show(_world);
        _flora.Show(_world);
        _pockets.Clear();
        _fauna.Clear();
        _chargeStones.Show(_world);
        _springs.Show(_world);
        _vases.Show(_world);
        _vaults.Show(_world);
        _prismStones.Show(_world);
        _seals.Show(_world, _level);
        _hazards.ShowCurtains(_world);
        _kelp.Build(_map, LookOf(_map));
    }

    /// <summary>A secret room's seal opened on an open level: its chunk's wall gone from its heights, the window filled again and its ground redrawn.</summary>
    void OpenSeaSeal(System.Numerics.Vector2 at)
    {
        if (Sea is not { } sea || !_seaBuilt.TryGetValue(sea.ChunkAt(at + _seaW), out var chunk) || !chunk.Sealed) return;
        chunk.Unseal();
        _level.OpenSeaRoof(chunk.Key);
        sea.Fill(_map, _seaW, k => _seaBuilt.GetValueOrDefault(k), chunk.Key);
        OctoShoots.Game.Fx.SunMap.Shifted(_map, OctoShoots.Game.Fx.SunLight.Direction, Vector3.Zero);
        _level.ShowSeaChunk(chunk, LevelView.SeaSurface(chunk), again: true);
        // Its walls stood in the padding of the chunks round it as well (their ground drawn to meet it).
        _seaOpened.Add(chunk.Key);
        foreach (var other in _seaBuilt.Values)
            if (other != chunk && Touches(chunk, other))
            {
                sea.UnsealAround(chunk, other);
                _level.ShowSeaChunk(other, LevelView.SeaSurface(other), again: true);
            }
        _seals.Show(_world, _level);
    }

    /// <summary>The window moved on by <paramref name="by"/> (sea metres): the sim's map refilled, she and the whole world moved back by as much.</summary>
    void ShiftSea(System.Numerics.Vector2 by)
    {
        _seaW += by;
        Sea!.Fill(_map, _seaW, k => _seaBuilt.GetValueOrDefault(k));
        _world.ShiftWindow(by);
        for (int i = 0; i < _route.Count; i++) _route[i] -= by;
        var shift = new Vector3(-by.X, 0f, -by.Y);
        _origin += new Vector2(by.X, by.Y);
        _camera.Shift(shift);
        _camera.SetWorldOrigin(_origin);
        _snow.Shift(shift);
        _plankton.Shift(shift);
        _bell.Shift(shift);
        _wakes.Clear();
        _memory.Shift(by);
        _level.Place(Vector3.Zero, _origin);
        _level.SetSeaOrigin(_seaW);
        OctoShoots.Game.Fx.SunMap.Shifted(_map, OctoShoots.Game.Fx.SunLight.Direction, new Vector3(by.X, 0f, by.Y));
        AddSeaLife(moved: true);
    }

    void Explore(OpenSea sea, (int X, int Y) here)
    {
        // (--dbg-lair: on towards the lair, the nearest way first, and down it once there.)
        var lair = LairAt(sea) + _seaW;
        if (_dbgLair && here == sea.Lair)
        {
            _route = LevelValidator.ShortestPath(_map, _world.Player.Position, lair - _seaW, clearance: 0.8f);
            _routeProgress = 0;
            _autoDive = true;
            return;
        }
        var ways = sea.Neighbours(here).Where(n => sea.Linked(here, n) && _seaBuilt.ContainsKey(n))
            .OrderBy(n => _dbgLair ? System.Numerics.Vector2.Distance((sea.RectOf(n).Min + sea.RectOf(n).Max) * 0.5f, lair) : 0f).ThenBy(n => _seaVisited.Contains(n)).ThenBy(n => (n.X * 7919 + n.Y * 104729 + _frame / 600) & 7).ToList();
        foreach (var n in ways)
        {
            var c = _seaBuilt[n];
            var mid = (c.Min + c.Max) * 0.5f;
            var target = Enumerable.Range(0, 400).Select(i => mid + new System.Numerics.Vector2(i % 20 - 10, i / 20 - 10)).Where(p => c.At((int)p.X, (int)p.Y) < -0.7f)
                .OrderBy(p => System.Numerics.Vector2.Distance(p, mid)).Cast<System.Numerics.Vector2?>().FirstOrDefault();
            if (target is not { } t) continue;
            var route = LevelValidator.ShortestPath(_map, _world.Player.Position, t - _seaW, clearance: 0.8f);
            if (route.Count == 0) continue;
            _route = route;
            _routeProgress = 0;
            return;
        }
    }

    /// <summary>The holes she has found on an open level, in the window (their emblems at the screen's edge).</summary>
    /// <summary>The lair (the way to the level's boss) in the window: its hole once its chunk is made, till then its chunk's middle.</summary>
    System.Numerics.Vector2 LairAt(OpenSea sea) =>
        (_seaBuilt.GetValueOrDefault(sea.Lair)?.Lair ?? (sea.RectOf(sea.Lair).Min + sea.RectOf(sea.Lair).Max) * 0.5f) - _seaW;

    /// <summary>The secret rooms opened (their walls gone from the padding of chunks made after).</summary>
    readonly HashSet<(int X, int Y)> _seaOpened = new();

    /// <summary>Whether a chunk's padding reaches into a room.</summary>
    static bool Touches(OpenSea.Chunk room, OpenSea.Chunk other) =>
        other.Min.X - OpenSea.Pad < room.Max.X && other.Max.X + OpenSea.Pad > room.Min.X && other.Min.Y - OpenSea.Pad < room.Max.Y && other.Max.Y + OpenSea.Pad > room.Min.Y;

    bool _dbgLair;
    /// <summary>Verification (--dbg-closed): every level a template level, as before open levels (to compare their look).</summary>
    static bool _dbgClosed;
    /// <summary>Verification (--dbg-secret): exploring, she makes for the nearest secret room's seal she can reach, holds two seconds before it, then it opens.</summary>
    bool _dbgSecret;
    int _dbgSecretHeld;

    bool DebugSecret()
    {
        var her = _world.Player.Position;
        foreach (var seal in _world.Seals.Where(s => !s.Open).OrderBy(s => System.Numerics.Vector2.Distance(s.Emblem, her)))
        {
            var stand = seal.Emblem + seal.Facing * 2.5f;
            if (System.Numerics.Vector2.Distance(stand, her) < 1.5f)
            {
                _route.Clear();
                if (++_dbgSecretHeld == 1) GD.Print($"secret seal reached at frame {_frame} (hue {seal.Hue})");
                if (_dbgSecretHeld < 120) return true;
                int from = _world.Events.Count;
                for (int i = 0; i < seal.Needed && !seal.Open; i++) _world.DebugStrikeSeal(seal);
                for (int i = from; i < _world.Events.Count; i++)
                {
                    _seals.OnEvent(_world, _world.Events[i]);
                    CorruptionEvent(_world.Events[i]);
                }
                GD.Print($"secret seal opened at frame {_frame}");
                _dbgSecret = false;
                return true;
            }
            if (_route.Count > 0 && System.Numerics.Vector2.Distance(_route[^1], stand) < 1f) return true;
            if (_frame % 30 != 0) return false;
            var route = LevelValidator.ShortestPath(_map, her, stand, clearance: 0.8f);
            if (route.Count == 0) continue;
            _route = route;
            _routeProgress = 0;
            return true;
        }
        return false;
    }

    IEnumerable<System.Numerics.Vector2> FoundSeaHoles() => Sea is null ? Enumerable.Empty<System.Numerics.Vector2>() : _seaFound.Select(h => h - _seaW);
}
