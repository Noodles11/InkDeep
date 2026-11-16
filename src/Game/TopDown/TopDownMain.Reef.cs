using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Cubes;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A dive in the cube reef (docs/proposals/CUBE-WORLD-PLAN.md §15.1): one continuous reef of layers, each a level as the
/// game's systems see it (CubeReef stitches its cubes into one map), its rock drawn as it streams in round her
/// (ReefStream, ReefStreamView). She claims the cubes she swims into, the wake current shuts the edge behind the one she
/// came by, the frontier drift leads her on to new water; over the void she dives to the layer below (Shift, the
/// d-pad's down), under a well of the layer above she climbs back (Q, the d-pad's up), the game's dive played either way.
/// The cube reef is the one game: every dive is in it.
/// </summary>
public partial class TopDownMain
{
    enum LayerMove { None, Down, Up }

    /// <summary>The change of layer's length (the game's dive), the point it holds at while what she arrives in is not ready, and how close the camera draws in.</summary>
    const float LayerSeconds = 1.6f, LayerHold = 0.6f, LayerCloser = 0.4f;

    CubeReef? _reef;
    ReefStream _stream = null!;
    TaskCubeWorker _cubeWorker = null!;
    ReefStreamView _reefView = null!;
    CubeDiving _cubeDiving = null!;
    ReefRecords _records = new();
    CubeCurrents _cubeCurrents = new();
    CubeDirector _director = null!;
    /// <summary>The layers whose boss has been rolled (on her first arrival).</summary>
    readonly HashSet<int> _bossRolled = new();
    /// <summary>Her layer's arena while its boss is unfreed: its wells sealed; while the battle runs, its edges too.</summary>
    bool InArena(System.Numerics.Vector2 p, int layer) => _reef!.IsArena(CubeId.Of(p, layer));
    int _layer;
    int _cubeWindow = CubeTuning.Window, _cubeLayers = CubeTuning.Layers;
    /// <summary>Verification (--cube-boss): every layer below the warm-up holds a boss (and --autopilot swims to its arena).</summary>
    bool _cubeBoss;
    /// <summary>
    /// Verification (--fight=id or --fight=id@layer): the dive opens inside a boss's arena on the top layer, the fight
    /// begun at once. The id is one of the Drowned Court's (BossCourt.All; --fight=list prints them) or a neighbour's
    /// (tull, kip, fennel, ottilie, bubbles); the layer is the one whose strength it has (its band's first, unnamed).
    /// </summary>
    string? _fight;
    int _fightLayer;

    /// <summary>The boss --fight names, as its layer makes it (null: none named, or no such boss).</summary>
    OctoShoots.Core.Bosses.BossSpec? FightBoss()
    {
        if (_fight is null) return null;
        if (OctoShoots.Core.Bosses.BossCourt.Get(_fight) is { } who)
            return OctoShoots.Core.Bosses.BossCourt.Make(who.Id, _seed.Value, _fightLayer > 0 ? _fightLayer : who.From);
        if (Folk.Get(_fight) is { } folk)
            return OctoShoots.Core.Bosses.FolkBosses.For(folk.Id, folk.Level, OctoShoots.Core.Bosses.BossCourt.MenaceOf(Math.Max(1, _fightLayer)));
        return null;
    }

    /// <summary>
    /// The top layer's map; with --fight, an arena opened on it a few cubes from her start (before any of it is drawn)
    /// and its boss waiting there.
    /// </summary>
    LevelMap FirstLayer(CubeReef reef)
    {
        CubeId? arena = null;
        if (_fight is not null)
        {
            if (FightBoss() is null)
            {
                GD.Print($"--fight={_fight}: no such boss. The court: {string.Join(", ", OctoShoots.Core.Bosses.BossCourt.All.Select(b => $"{b.Id} ({b.Name}, layers {b.From}+)"))}; the neighbours: tull, kip, fennel, ottilie, bubbles.");
                _fight = null;
            }
            else if ((arena = reef.ArenaOf(0) ?? _director.ArenaFor(reef.Space, CubeId.Of(reef.Start, 0))) is { } cube) reef.RestoreArena(cube);
        }
        var map = reef.Layer(0, reef.Start);
        if (arena is { } at)
        {
            map.HasBoss = true;
            map.Exit.Position = at.Middle;
            map.Exit.Radius = CubeTerrain.ArenaRadius;
            _startAt ??= "arena";
        }
        return map;
    }

    /// <summary>Verification (--cube-chart=seconds): that long into the run, the pause menu opens on its chart.</summary>
    float _cubeChartAt;
    /// <summary>Verification: a haven's checkpoint also written to this file (--cube-save=), and a dive taken up from one (--cube-resume=).</summary>
    string? _cubeSaveTo, _cubeResumeFrom;

    /// <summary>Verification (--cube-resume=): the verification jelly's dive is the one in that file; true if it was read.</summary>
    bool ResumeFromFile()
    {
        if (_cubeResumeFrom is null || _jelly is null || !System.IO.File.Exists(_cubeResumeFrom)) return false;
        var dive = System.Text.Json.JsonSerializer.Deserialize<OctoShoots.Core.Saves.SuspendedRun>(System.IO.File.ReadAllText(_cubeResumeFrom));
        if (dive?.Cubes is null || !OctoShoots.Core.Run.SeedCode.TryParse(dive.Seed, out var seed)) return false;
        _jelly.Dive = dive;
        _seed = seed;
        _resume = dive;
        _reefResume = dive.Cubes;
        return true;
    }
    /// <summary>Her layer's map and the one above's (kept to climb back to), and the next one down being made.</summary>
    readonly Dictionary<int, LevelMap> _layerMaps = new();
    Task<LevelMap>? _making;
    int _makingLayer = -1;

    LayerMove _layerMove;
    float _layerTime, _layerPulse, _hoverDown, _hoverUp, _heldShown, _reefSeeThrough;
    System.Numerics.Vector2 _layerFrom;
    bool _downLatch, _upLatch;
    LayerBlock _down = LayerBlock.NotReady, _up = LayerBlock.NotReady;
    ReefLook? _layerLookFrom, _layerLookTo;
    int _revealTick;

    LayerRecords Here => _records.Of(_layer);

    /// <summary>
    /// How dark a layer is, 0 (the top, bright) to 1 (the bottom, wholly dark): its depth's share squared, so the layers
    /// near the top dim only a little and the dark gathers toward the bottom.
    /// </summary>
    float LayerShare(int z)
    {
        float share = _reef is null || _reef.Space.Layers <= 1 ? 0f : z / (float)(_reef.Space.Layers - 1);
        return share * share;
    }

    /// <summary>A layer's look: a little darker each layer down (ReefLook.Layer), named as the depth it stands for.</summary>
    ReefLook ReefLayerLook(int z) => ReefLook.Layer(LayerShare(z), CubeReef.LookDepthOf(z));
    int LayerTarget => _layer + (_layerMove == LayerMove.Down ? 1 : -1);
    /// <summary>In the cube reef's open water (not in a place behind a mouth).</summary>
    bool InReef => _reef is not null && !InDiveCave;

    /// <summary>The flags of the cube reef (read with the others).</summary>
    void ParseReefArg(string arg)
    {
        if (arg == "--cube-boss") _cubeBoss = true;
        else if (arg.StartsWith("--fight="))
        {
            // (--fight=id, or --fight=id@layer for it as that layer makes it.)
            var parts = arg["--fight=".Length..].Split('@');
            _fight = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out int at)) _fightLayer = Math.Clamp(at, 1, CubeTuning.Layers);
        }
        else if (arg.StartsWith("--cube-chart=") && float.TryParse(arg["--cube-chart=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float chartAt)) _cubeChartAt = chartAt;
        else if (arg.StartsWith("--cube-save=")) _cubeSaveTo = arg["--cube-save=".Length..];
        else if (arg.StartsWith("--cube-resume=")) _cubeResumeFrom = arg["--cube-resume=".Length..];
        else if (arg.StartsWith("--cube-window=") && int.TryParse(arg["--cube-window=".Length..], out int f)) _cubeWindow = Math.Clamp(f, 4, 64);
        else if (arg.StartsWith("--cube-layers=") && int.TryParse(arg["--cube-layers=".Length..], out int l)) _cubeLayers = Math.Clamp(l, 2, CubeTuning.Layers);
    }

    /// <summary>A cube dive saved in a haven, being taken up (null: a new dive).</summary>
    CubeDiveSave? _reefResume;
    /// <summary>The layers whose boss she has freed.</summary>
    readonly HashSet<int> _bossFreed = new();

    /// <summary>
    /// Each layer's danger clock (DangerTuning, docs/DIVE.md): seconds she has spent on it, started unseen the first time
    /// she comes onto it, stopped for good once its boss is freed (its danger then back to none).
    /// </summary>
    readonly Dictionary<int, float> _dangerClock = new();

    /// <summary>Her layer's clock run on (not while the game is held: the pause menu or any other; nor out of the reef), and the danger it is at, to the world and the HUD.</summary>
    void StepDanger(float dt)
    {
        int danger = 0;
        if (!_bossFreed.Contains(_layer))
        {
            _dangerClock.TryGetValue(_layer, out float seconds);
            if (_layerMove == LayerMove.None && !_world.Defeated && !Paused) seconds += dt * _dangerSpeed;
            _dangerClock[_layer] = seconds;
            danger = DangerTuning.StepAt(seconds);
        }
        _world.Danger = danger;
        _hud.Danger = danger;
    }

    /// <summary>Verification (--danger-speed=N): the danger clock run N times as fast.</summary>
    readonly float _dangerSpeed = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--danger-speed="))
        is { } arg && float.TryParse(arg["--danger-speed=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float k) ? k : 1f;

    /// <summary>The reef from the run's seed, and its views; she begins on the warm-up layer, or in the haven a save left her in.</summary>
    void BuildReef()
    {
        if (_reefResume is { } saved)
        {
            _cubeWindow = saved.Window > 0 ? saved.Window : CubeTuning.Window;
            _cubeLayers = saved.Layers;
        }
        _reef = new CubeReef(_seed.Value, _cubeWindow, _cubeLayers);
        _id = CubeReef.IdOf(0);
        _layer = 0;
        _cubeWorker = new TaskCubeWorker(_reef.Space, BuildCube, _reef.Terrain);
        _stream = new ReefStream(_reef.Space, _cubeWorker);
        _cubeWorker.Stream = _stream;
        _cubeDiving = new CubeDiving(_reef.Space.Layers);
        _director = new CubeDirector(_seed.Value);
        _reefView = new ReefStreamView();
        AddChild(_reefView);
        if (_reefResume is not null) ResumeReef(_reefResume);
        else if (Here.Arrive(_reef.Start) == Crossing.Claimed) Grant(CubeId.Of(_reef.Start, 0));
        GD.Print($"Cube reef {_seed}: read in a window of {_reef.Space.Window} × {_reef.Space.Window} squares, {_reef.Space.Layers} layers, made as she swims");
    }

    /// <summary>
    /// A new dive after the last ended (CUBE-WORLD-PLAN §10: dying ends the run): the old reef and all it kept let go, a
    /// new one from the run's seed, she on its warm-up layer.
    /// </summary>
    void NewReef()
    {
        _reefView.QueueFree();
        _records = new ReefRecords();
        _cubeCurrents = new CubeCurrents();
        _layerMaps.Clear();
        _making = null;
        _makingLayer = -1;
        _bossRolled.Clear();
        _bossFreed.Clear();
        _dangerClock.Clear();
        _reefResume = null;
        _layerMove = LayerMove.None;
        _hud.LayerPrompt = null;
        ForgetDiveCaves();
        BuildReef();
    }

    /// <summary>The stream's work off the main thread that is the game's: a cube's meshes.</summary>
    static object? BuildCube(CubeJob job, CubeTerrain terrain) => job.Work switch
    {
        CubeWork.Mesh => CubeShapes.Ground(terrain),
        CubeWork.PreviewMesh => CubeShapes.Preview(terrain),
        _ => "nothing",
    };

    /// <summary>A layer as the level she is on (made off the main thread; kept while she may come back to it).</summary>
    LevelMap LayerMap(int z) => _layerMaps.TryGetValue(z, out var map) ? map : _layerMaps[z] = z == 0 ? FirstLayer(_reef!) : _reef!.Layer(z, _reef.Start);

    /// <summary>The warm-up layer's map and its (bare) shape: the first level of a dive in the reef.</summary>
    Task<LevelShape> ReefFirstShape()
    {
        var reef = _reef!;
        if (_reefResume is not null && InDiveCave)
        {
            var seed = _seed;
            var map = _layerMaps[_layer];
            int mouth = _diveCave;
            return Task.Run(() => PrepareBehind(seed, map, mouth));
        }
        return Task.Run(() => LevelShape.Bare(FirstLayer(reef)));
    }

    /// <summary>
    /// A cube dive taken up in the haven it was saved in (CUBE-WORLD-PLAN §10): the reef's records, the director, the
    /// arenas and her layer as they were; her layer made (with its mouths) and she in the haven behind its mouth.
    /// </summary>
    void ResumeReef(CubeDiveSave saved)
    {
        _layer = saved.Layer;
        _id = CubeReef.IdOf(_layer);
        _cubeDiving.Restore(saved.Layer, saved.Deepest);
        _director.Load(saved.Director);
        foreach (int z in saved.BossRolled) _bossRolled.Add(z);
        foreach (int z in saved.BossFreed) _bossFreed.Add(z);
        foreach (var (z, seconds) in saved.DangerClock) _dangerClock[z] = seconds;
        foreach (var (z, key) in saved.Arenas)
            if (CubeDiveSave.FromCubeKey(key) is { } arena) _reef!.RestoreArena(arena);
        foreach (var layer in saved.Records) CubeRecordSaving.Load(layer, _records.Of(layer.Layer));
        var map = _reef!.Layer(_layer, _reef.Start);
        MarkBoss(map, _layer);
        OpenMouths(map);
        _layerMaps[_layer] = map;
        _reefView.SetLayer(_layer);
        if (saved.HavenMouth >= 0 && saved.HavenMouth < map.Mouths.Count)
        {
            _diveCave = _caveMouth = saved.HavenMouth;
            _reefShape = LevelShape.Bare(map);
        }
        GD.Print($"Cube reef: taken up on layer {_layer} (deepest {saved.Deepest}), {Here.Claims} cubes claimed, in the haven behind mouth {saved.HavenMouth}");
    }

    /// <summary>A layer's boss on its map: its arena's middle the boss's place, unless she has freed it.</summary>
    void MarkBoss(LevelMap map, int layer)
    {
        if (_reef!.ArenaOf(layer) is not { } arena || _bossFreed.Contains(layer)) return;
        map.HasBoss = true;
        map.Exit.Position = arena.Middle;
        map.Exit.Radius = CubeTerrain.ArenaRadius;
    }

    /// <summary>
    /// A haven reached (§10): the dive saved there, its records, the director and the arenas with it, so continuing puts
    /// her back in it. What is left of her layer's stock was kept as she went in.
    /// </summary>
    void SaveReefCheckpoint()
    {
        if (_jelly is not { } jelly || _run is null) return;
        var record = RunRecord();
        record.Cubes = new CubeDiveSave
        {
            Terrain = CubeDiveSave.TerrainNow,
            Window = _reef!.Space.Window, Layers = _reef.Space.Layers, Layer = _layer, Deepest = _cubeDiving.Deepest,
            HavenMouth = _diveCave, Director = _director.Save(),
            Arenas = Enumerable.Range(0, _reef.Space.Layers).Where(z => _reef.ArenaOf(z) is not null).ToDictionary(z => z, z => CubeDiveSave.CubeKey(_reef.ArenaOf(z)!.Value)),
            BossRolled = _bossRolled.ToList(), BossFreed = _bossFreed.ToList(), DangerClock = new Dictionary<int, float>(_dangerClock),
            Records = _records.Layers.Select(z => CubeRecordSaving.Save(_records.Of(z))).ToList(),
        };
        jelly.Level = _run.XpLevel;
        jelly.Xp = _run.Xp;
        jelly.Points = _run.Points;
        jelly.Skills = _run.Skills.ToDictionary(k => k.Key.ToString(), k => k.Value);
        jelly.Dive = record;
        if (_cubeSaveTo is not null) System.IO.File.WriteAllText(_cubeSaveTo, System.Text.Json.JsonSerializer.Serialize(record));
        jelly.Played = DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
        if (_jellySaved && !_debugRun) OctoShoots.Game.Settings.GameSave.Write();
        GD.Print($"Cube reef: saved in the haven behind mouth {_diveCave} on layer {_layer}");
    }

    /// <summary>Into a place behind a mouth from her layer: what is left of each claimed cube's stock kept (for a save made there).</summary>
    void LeavingForMouth()
    {
        foreach (var record in Here.Claimed.Values) record.Left = _world.StockLeft(record.Cube);
    }

    /// <summary>
    /// The world's options on a layer: the reef's (its murklings stocked as she claims cubes), no leaks, no corruption on
    /// the ground. A layer's boss (§9.1): `--boss=`'s, else the neighbour the Leak made the boss of the depth it stands
    /// for (layer 1 the first's: the warm-up layer has none), as on the dives, unless she has freed her; else the depth's own.
    /// </summary>
    PlaneOptions ReefOptions()
    {
        var options = new PlaneOptions { Corruption = false, Pufferlings = false, Adventure = false, InkMurklings = false, Shots = true, Reef = true };
        if (!_map.HasBoss) return options;
        // Its boss: the neighbour the Leak made the boss of the depth the layer stands for, until she is freed; else one
        // of the Drowned Court, from the layer's pool (BossCourt; BOSS-GENERATOR §15).
        if (_layer == 0 && FightBoss() is { } fight) options.Boss = fight;
        else if (DebugBoss() is { Boss: { } forced }) options.Boss = forced;
        else if (_jelly is { } jelly && Dives.FolkBossOf(CubeReef.IdOf(_layer - 1), jelly) is { } folk && Folk.Get(folk) is { } n
            && OctoShoots.Core.Bosses.FolkBosses.For(n.Id, n.Level, _map.Menace) is { } spec) options.Boss = spec;
        else options.Boss = OctoShoots.Core.Bosses.BossCourt.For(_seed.Value, _layer);
        if (options.Boss is { } met) GD.Print($"Cube reef: layer {_layer}'s boss is {met.Name} ({met.Form}, {met.Concept})");
        return options;
    }

    /// <summary>A layer entered (the first, or after a change of layer): its map kept, the next one down begun.</summary>
    /// <param name="restored">She is back from a place behind a mouth, the layer's world put back as she left it (its stock with it).</param>
    void EnteredLayer(bool restored)
    {
        _layerMaps[_layer] = _map;
        // Its claimed cubes' places, their mouths on its map (those granted before it was made).
        if (_map.Mouths.Count is var mouthsWere && OpenMouths(_map)) ShowReefMouths(mouthsWere);
        // Back on a layer: its claimed cubes' stock put back as she left it.
        foreach (var record in restored ? Enumerable.Empty<CubeRecord>() : Here.Claimed.Values)
            foreach (var left in record.Left ?? Enumerable.Empty<StockLeft>())
                _world.PlaceStock(record.Cube, left.Spawn, left.Seed, left.Prime, left.At, left: true, shoal: left.Shoal);
        // (Only her layer and the one above are ever needed again: §10.)
        foreach (int z in _layerMaps.Keys.Where(z => z < _layer - 1).ToList())
        {
            _layerMaps.Remove(z);
            _reef!.Forget(z);
        }
        StartMaking(_layer + 1);
    }

    void StartMaking(int z)
    {
        if (_reef is not { } reef || z >= reef.Space.Layers || _layerMaps.ContainsKey(z) || _makingLayer == z) return;
        _makingLayer = z;
        // (Its reef life worked out with it, off the main thread.)
        _making = Task.Run(() =>
        {
            var map = reef.Layer(z, reef.Start);
            LevelShape.Bare(map);
            return map;
        });
    }

    /// <summary>The layer being made, once made, kept.</summary>
    void TakeMade()
    {
        if (_making is not { IsCompleted: true } made) return;
        if (made.IsFaulted) GD.PushError($"Cube layer {_makingLayer} failed: {made.Exception}");
        else _layerMaps[_makingLayer] = made.Result;
        _making = null;
        _makingLayer = -1;
    }

    /// <summary>Every frame on the reef: what the stream wants round her (or round where she is going), work taken in, meshes shown.</summary>
    void StepReef(float dt)
    {
        using (Timed("reef take made")) TakeMade();
        if (_cubeChartAt > 0f && _elapsed > _cubeChartAt)
        {
            _cubeChartAt = 0f;
            _pauseTab = PauseMenu.ChartTab;
            CallDeferred(MethodName.TogglePause);
        }
        if (_world.Boss?.Freed == true) _bossFreed.Add(_layer);
        StepDanger(dt);
        var p = _world.Player;
        using (Timed("reef stream")) _stream.Update(ReefFocus(p), dt);
        using (Timed("reef drain")) _cubeWorker.Drain(_stream);
        while (_cubeWorker.Errors.TryDequeue(out var error)) GD.PushError($"Cube work failed: {error}");
        using (Timed("reef settle")) SettleReef();
        using (Timed("reef view take")) _reefView.Take(_stream);
        using (Timed("reef view apply")) _reefView.Apply();
    }

    /// <summary>Charge stones set in each cube's walls (PlaneWorld.PlaceChargeStonesIn).</summary>
    const int CubeChargeStones = 2;

    /// <summary>When her layer's sun map was last cast, and whether a cube laid since wants it cast again.</summary>
    float _sunCastAt;
    bool _sunStale;

    /// <summary>
    /// The cubes made since last frame laid onto their layers' maps (CubeReef.Settle); those of her layer bring their reef
    /// life with them, the cutout's map of her surroundings made again, and in a while the sun's.
    /// </summary>
    void SettleReef()
    {
        bool any = false;
        TakeLife();
        foreach (var cube in _reef!.Settle(SettleMost))
            if (cube.Z == _layer && InReef && _layerMaps.TryGetValue(_layer, out var map) && map == _map)
            {
                LaidHere(cube);
                any = true;
            }
        // (Verification, --autopilot: a way on looked for again once more of the layer is laid.)
        if (any && _autopilot && _route.Count == 0 && _layerMove == LayerMove.None)
            using (Timed("autopilot route"))
                Route();
        if (_sunStale && _layerMove == LayerMove.None && InReef && _elapsed - _sunCastAt > 1.5f)
        {
            _sunStale = false;
            _sunCastAt = _elapsed;
            using (Timed("sun map")) OctoShoots.Game.Fx.SunMap.Build(_map, OctoShoots.Game.Fx.SunLight.Direction);
        }
    }

    /// <summary>Cubes laid onto their maps a frame at most (CubeReef.Settle): the rest wait for the next.</summary>
    const int SettleMost = 2;

    /// <summary>Cubes' reef life being worked out on the pool (LevelShape.ReefLife: it only reads the map), for the map it was laid on.</summary>
    readonly List<(LevelMap Map, Rect2 Area, System.Threading.Tasks.Task<(List<OctoShoots.Game.Fx.FloraItem>, List<Transform3D>, List<Transform3D>)> Work)> _life = new();

    /// <summary>The reef life worked out so far put in, if her layer's map is still the one it was for (worked out here should the pool have failed).</summary>
    void TakeLife()
    {
        for (int i = 0; i < _life.Count; i++)
        {
            var (map, area, work) = _life[i];
            if (!work.IsCompleted) continue;
            _life.RemoveAt(i--);
            if (map != _map) continue;
            var (flora, rocks, glows) = work.IsCompletedSuccessfully ? work.Result : LevelShape.ReefLife(map, area);
            using (Timed("laid: add life")) _level.AddLife(flora, rocks, glows);
        }
    }

    /// <summary>A cube of her layer laid on its map: its reef life and charge stones put in, the cutout's map and the sun's made again.</summary>
    void LaidHere(CubeId cube)
    {
        int had = _world.ChargeStones.Count;
        using var _ = Timed("laid: all");
        using (Timed("laid: stones")) _world.PlaceChargeStonesIn(cube.Origin, cube.Origin + cube.Size, ReefHash.Of(_reef!.Space.Seed, ReefHash.Cube, cube.X, cube.Y, cube.Z, cube.OnEnd ? 1 : 0), CubeChargeStones);
        // (Verification, --dbg-charge: lit as they come.)
        if (_dbgChargeStones)
            for (int i = had; i < _world.ChargeStones.Count; i++) _world.ChargeStones[i].Charge = ChargeStoneTuning.GlowSeconds;
        // (Its life worked out on the pool, put in as it is ready: TakeLife.)
        var map = _map;
        var area = new Rect2(cube.Origin.X, cube.Origin.Y, cube.Size.X, cube.Size.Y);
        _life.Add((map, area, System.Threading.Tasks.Task.Run(() => LevelShape.ReefLife(map, area))));
        _level.SeeMaskStale();
        // (The way through the water found again over it: PlaneWorld.NavStale.)
        _world.NavStale(cube.Origin, cube.Origin + cube.Size);
        _sunStale = true;
    }

    /// <summary>Her layer just shown: the reef life of every cube laid on it so far (the rest as each is laid).</summary>
    void ShowLaidLife()
    {
        if (_reef is null || !InReef || !_layerMaps.TryGetValue(_layer, out var map) || map != _map) return;
        _reef.Settle();
        foreach (var cube in _reef.LaidOn(_layer)) LaidHere(cube);
        _sunStale = false;
    }

    StreamFocus ReefFocus(PlaneBody p)
    {
        if (_layerMove != LayerMove.None)
            return new StreamFocus { Position = _layerFrom, Layer = LayerTarget, Climbable = _layerMove == LayerMove.Down };
        bool soon = _cubeDiving.CooldownLeft < 5f;
        return new StreamFocus
        {
            Position = p.Position, Velocity = p.Velocity, Layer = _layer, Climbable = _cubeDiving.Climbable, Shut = Here.IsShut,
            ReadyBelow = _hoverDown >= 0.5f || soon && _layer + 1 < _reef!.Space.Layers,
            ReadyAbove = _cubeDiving.Climbable && (_hoverUp >= 0.5f || soon),
        };
    }

    /// <summary>After each of her ticks: the currents move her; a cube line crossed claims a cube or holds her at a shut edge.</summary>
    void StepReefCurrents()
    {
        var p = _world.Player;
        var cube = CubeId.Of(p.Position, _layer);
        // The battle on: the arena's ways out are shut until it ends.
        if (_world.BossBattle && InArena(p.PrevPosition, _layer) && !InArena(p.Position, _layer))
        {
            p.Position = p.PrevPosition;
            p.Velocity = System.Numerics.Vector2.Zero;
            _heldShown = 2f;
            return;
        }
        if (_reef!.Made(cube) is { } here && !InArena(p.Position, _layer))
        {
            _cubeCurrents.Prepare(here, _reef.Space, Here);
            var flow = _cubeCurrents.DriftAt(p.Position) + CubeCurrents.WakeAt(p.Position, cube, _reef.Space, Here);
            if (flow != System.Numerics.Vector2.Zero) _world.Move(ref p.Position, ref flow, _world.Radius, report: false);
        }
        var to = p.Position;
        var crossed = Here.Move(p.PrevPosition, ref to);
        if (crossed == Crossing.Held)
        {
            p.Position = to;
            p.Velocity = System.Numerics.Vector2.Zero;
            _heldShown = 2f;
        }
        // A cube claimed: its stock buds (never the warm-up layer's, nor the cube she arrived in).
        else if (crossed == Crossing.Claimed)
        {
            var claimed = CubeId.Of(p.Position, _layer);
            // (The arena holds its boss alone: no stock, no places.)
            using var _ = Timed("reef claim");
            var sp = Timed("claim: spots");
            var spots = _reef.IsArena(claimed) ? new List<StockSpot>() : CubeStock.Spots(_reef.Space.Seed, claimed, Here.Claimed[claimed].Claim, _map.Menace, p.Position, _world.Clear);
            sp.Dispose();
            using (Timed("claim: stock")) foreach (var s in spots) _world.PlaceStock(claimed, s.Spawn, s.Seed, s.Prime, new[] { s.At }, left: false);
            using (Timed("claim: grant")) if (!_reef.IsArena(claimed)) Grant(claimed);
            using (Timed("claim: mouths")) if (_map.Mouths.Count is var mouthsWere && OpenMouths(_map)) ShowReefMouths(mouthsWere);
            GD.Print($"Cube reef: {claimed} claimed ({Here.Claims} on layer {_layer}), {spots.Count} of its stock budded");
            using (Timed("claim: route")) if (_autopilot) Route();
        }
        if (++_revealTick % 6 == 0) Here.Reveal(p.Position, 12f);
    }

    /// <summary>
    /// Each frame she swims: where she may go through, the HUD's prompt, and the key pressed there begins the change of
    /// layer (true: it has begun).
    /// </summary>
    bool StepLayers(float dt)
    {
        _cubeDiving.Tick(dt);
        _heldShown = Mathf.Max(0f, _heldShown - dt);
        var p = _world.Player.Position;
        _down = _cubeDiving.CanDive(p, _world.Radius, _reef!.Made);
        _up = _cubeDiving.CanClimb(p, _world.Radius, _reef.Made);
        // (The layer she would arrive on must be made too.)
        if (_down == LayerBlock.None && !_layerMaps.ContainsKey(_layer + 1)) _down = LayerBlock.NotReady;
        // The arena's wells are sealed over until its boss is freed; nothing comes down into an arena from above.
        if (_down is LayerBlock.None or LayerBlock.Cooldown && (InArena(p, _layer) && _world.Boss is { Freed: false } || InArena(p, _layer + 1))) _down = LayerBlock.NoWay;
        if (_up == LayerBlock.None && !_layerMaps.ContainsKey(_layer - 1)) _up = LayerBlock.NotReady;
        if (_up is LayerBlock.None or LayerBlock.Cooldown && InArena(p, _layer) && _world.Boss is { Freed: false }) _up = LayerBlock.NoWay;
        _hoverDown = _down is LayerBlock.None or LayerBlock.Cooldown ? _hoverDown + dt : 0f;
        _hoverUp = _up is LayerBlock.None or LayerBlock.Cooldown ? _hoverUp + dt : 0f;
        // No hint to dive or climb (nor why not yet): only, as she tries to go back, what holds her.
        _hud.LayerPrompt = _heldShown > 0f ? (_world.BossBattle ? "The arena is sealed till the fight is over" : "A current holds the way back", false, InputSetup.Dive) : null;
        bool downKey = Input.IsActionPressed(InputSetup.Dive) || _autoDive && _down == LayerBlock.None && !(_cubeBoss && _world.Boss is { Freed: false });
        bool upKey = Input.IsActionPressed(InputSetup.Climb);
        bool begun = false;
        if (downKey && !_downLatch && _down == LayerBlock.None) begun = BeginLayerMove(LayerMove.Down);
        else if (upKey && !_upLatch && _up == LayerBlock.None) begun = BeginLayerMove(LayerMove.Up);
        _downLatch = downKey;
        _upLatch = upKey;
        return begun;
    }

    bool BeginLayerMove(LayerMove move)
    {
        _accumulator = 0;
        _recorder.LevelCleared(_world, _levelTime);
        Earned(_achievements.LevelCleared(_world));
        AddToRun();
        _layerMove = move;
        _layerTime = 0f;
        _layerPulse = 0f;
        _layerFrom = _world.Player.Position;
        _world.Player.Velocity = System.Numerics.Vector2.Zero;
        _world.Player.PrevPosition = _world.Player.Position;
        OctoShoots.Game.Fx.SunMap.Enable(false);
        _layerLookFrom = ReefLayerLook(_layer);
        _layerLookTo = ReefLayerLook(LayerTarget);
        _bell.Kick();
        _hud.Diving = true;
        _hud.LayerPrompt = null;
        _reefView.ShowAbove(move == LayerMove.Up);
        GD.Print($"Cube reef: {(move == LayerMove.Down ? "dive" : "climb")} from layer {_layer} at {_layerFrom}");
        return true;
    }

    /// <summary>
    /// One frame of a dive or a climb: the game's dive (she gathers, turns head-first and sinks a layer's drop in pulses;
    /// climbing she rises), the camera drawing in and back out, her layer opening like an iris round her (the layer above
    /// opening into view as she rises into it). Should the cube she arrives in not be ready, the middle of it loops.
    /// </summary>
    void StepLayerMove(float dt)
    {
        var arrival = CubeId.Of(_layerFrom, LayerTarget);
        bool ready = _stream.State(arrival) == CubeState.Active && _reefView.Pending == 0;
        _layerTime += dt;
        if (!ready && _layerTime > LayerHold)
        {
            _layerTime = LayerHold;
            if ((_layerPulse += dt) > 0.7f)
            {
                _layerPulse = 0f;
                _bell.Kick();
            }
        }
        float t = _layerTime, k = Mathf.Clamp(t / LayerSeconds, 0f, 1f);
        float sink = Ease((t - 0.25f) / 0.95f);
        sink = Mathf.Clamp(sink + 0.04f * Mathf.Sin(Mathf.Clamp((t - 0.25f) / 0.95f, 0f, 1f) * Mathf.Pi * 3f), 0f, 1f);
        float sign = _layerMove == LayerMove.Down ? 1f : -1f;
        float drop = sign * sink * CubeTuning.LayerDrop;
        float gather = Mathf.Sin(Mathf.Clamp(t / 0.25f, 0f, 1f) * Mathf.Pi) * 0.35f;
        _bell.DiveDepth = drop - sign * gather;
        _bell.DiveTurn = _layerMove == LayerMove.Down ? Ease((t - 0.2f) / 0.3f) * (1f - Ease((t - 1.1f) / 0.4f)) : 0f;
        _bell.Sync(_world, 1f, dt);
        SyncLantern();
        var focus = new Vector3(_layerFrom.X, LevelMap.SwimBand - drop, _layerFrom.Y);
        float pull = Mathf.Sin(Mathf.Pi * k);
        _camera.Zoom = 1f - LayerCloser * pull * pull;
        _camera.Track(focus, dt);
        _camera.SetSwimLevel(-drop);
        var look = _layerLookFrom == _layerLookTo ? _layerLookFrom! : ReefLook.Blend(_layerLookFrom!, _layerLookTo!, sink);
        if (_layerLookFrom != _layerLookTo) ApplyLook(look);
        look.SetDrop(drop);
        _reefView.Follow(focus);
        float radius = t < 0.35f ? Mathf.Lerp(-3f, 6f, Ease(t / 0.35f)) : Mathf.Lerp(6f, 150f, Mathf.Pow(Ease((t - 0.35f) / 0.9f), 2f));
        var at = new Vector2(_layerFrom.X, _layerFrom.Y);
        _reefView.SetPortal(at, radius, _layerMove == LayerMove.Up);
        // The walls of the layer she goes to rise up round her as she reaches it.
        if (_layerMove == LayerMove.Down) _reefView.SetRise(1f, sink, 0f);
        else _reefView.SetRise(1f, 0f, sink);
        // (Her layer's reef life goes with its rock, through the same iris; climbing, it stays till she is up.)
        if (_layerMove == LayerMove.Down) _level.SetPortal(at, radius, 1);
        // What lived on the layer she leaves goes with it, as on the game's dive.
        float stay = 1f - Ease((t - 0.3f) / 1.0f);
        FadeLevelLife(at, Mathf.Lerp(-5f, radius, Ease(t / 0.45f)), stay);
        _arrows.DiveOut(Ease(t / 0.6f));
        _arrows.Sync(_camera.Camera, HudScale, dt, _bell.GlobalPosition);
        _snow.Tick(dt, focus + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        _plankton.Tick(dt, focus + Vector3.Up * 2f, _camera.Camera.GlobalBasis);
        _damage.Tick(dt);
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        if (_layerTime >= LayerSeconds && ready) FinishLayerMove(arrival);
    }

    /// <summary>The change over: she is on the layer she went to, its world made, the view just as it was.</summary>
    void FinishLayerMove(CubeId arrival)
    {
        var terrain = _reef!.Terrain(arrival);
        bool down = _layerMove == LayerMove.Down;
        int from = _layer;
        // A boss freed stays freed: the layer, should she climb back to it, has none.
        if (_world.Boss?.Freed == true) _map.HasBoss = false;
        // The layer left: what is left of each claimed cube's stock, kept for her return.
        foreach (var record in Here.Claimed.Values) record.Left = _world.StockLeft(record.Cube);
        if (down) _cubeDiving.Dived(_layerFrom, terrain);
        else _cubeDiving.Climbed(_layerFrom, terrain);
        _layer = _cubeDiving.Layer;
        // A new deepest layer: the one left counts toward the boss streak, and this one rolls its boss (§9.1).
        if (down && _layer == _cubeDiving.Deepest && _bossRolled.Add(_layer))
        {
            _director.LeftLayer(from, _world.Boss?.Freed == true);
            if ((_director.BossOn(_layer) || _cubeBoss) && _director.ArenaFor(_reef.Space, arrival) is { } arena)
            {
                var layerMap = _layerMaps[_layer];
                _reef.SetArena(arena, layerMap);
                layerMap.HasBoss = true;
                layerMap.Exit.Position = arena.Middle;
                layerMap.Exit.Radius = CubeTerrain.ArenaRadius;
                GD.Print($"Cube reef: layer {_layer} holds a boss, its arena {arena} (streak {_director.Streak})");
            }
        }
        _records.Changed(from, _layer, _cubeDiving.Deepest);
        if (Here.Arrive(_layerFrom) == Crossing.Claimed) Grant(CubeId.Of(_layerFrom, _layer));
        _layerMove = LayerMove.None;
        _reefView.SetLayer(_layer);
        _reefView.ShowAbove(false);
        _reefView.SetPortal(Vector2.Zero, -1f, false);
        _reefView.SetRise(1f, 0f, 0f);
        _level.SetPortal(Vector2.Zero, -1f, 0);
        _bell.DiveDepth = 0f;
        _bell.DiveTurn = 0f;
        _camera.Zoom = 1f;
        _camera.SetSwimLevel(0f);
        _hud.Diving = false;
        _hoverDown = _hoverUp = 0f;
        _id = CubeReef.IdOf(_layer);
        // (The cube she arrives in laid on its map before the layer is shown.)
        _reef.Settle();
        ShowLevel(LevelShape.Bare(_layerMaps[_layer]), keepRun: true, 0, _layerFrom);
        _arrows.Arrive();
        foreach (var view in LevelLife) ResetFade(view);
        GD.Print($"Cube reef: {(down ? "dived" : "climbed")} to layer {_layer} (deepest {_cubeDiving.Deepest})");
    }

    /// <summary>
    /// Verification (--autopilot): her way through a gate of her cube into one she has not claimed (across a shut edge
    /// never), 8 m past its line.
    /// </summary>
    List<System.Numerics.Vector2> ReefRoute()
    {
        var her = _world.Player.Position;
        var cube = CubeId.Of(her, _layer);
        if (_cubeBoss && _world.Boss is { Freed: false } && _reef!.ArenaOf(_layer) is { } arena)
            return LevelValidator.ShortestPath(_map, her, arena.Middle, clearance: 1f);
        foreach (var (edge, gate, _) in _reef!.Terrain(cube).Gates.OrderBy(g => System.Numerics.Vector2.Distance(g.Mouth, her)))
        {
            if (Here.IsShut(edge)) continue;
            var inside = CubeId.Of(edge.High) == cube ? edge.High : new CellCoord(edge.High.X - (edge.Axis == EdgeAxis.X ? 1 : 0), edge.High.Y - (edge.Axis == EdgeAxis.Y ? 1 : 0), _layer);
            float into = edge.High == inside ? 1f : -1f;
            var across = edge.Point(gate.Along, -into * 8f);
            if (Here.IsClaimed(CubeId.Of(across, _layer))) continue;
            var path = LevelValidator.ShortestPath(_map, her, across, clearance: 1f);
            if (path.Count > 0) return path;
        }
        return new();
    }

    /// <summary>The reef's own views shown on its layers, hidden in a place behind a mouth (a level of its own, LevelView's).</summary>
    void ShowReef()
    {
        if (_reefView.Visible == InReef) return;
        _reefView.Visible = InReef;
        if (!InReef) _hud.LayerPrompt = null;
    }

    /// <summary>
    /// The pause menu's chart of her layer: its claimed cubes and what they hold, the cubes beside them (seen from them),
    /// the arena once she has seen it, the shut edges, and where she is (in a place behind a mouth: at its mouth).
    /// </summary>
    ChartView.ReefChart ReefChart()
    {
        var space = _reef!.Space;
        var claimed = Here.Claimed;
        var beside = new HashSet<CubeId>();
        foreach (var cube in claimed.Keys)
            foreach (var (edge, _, across) in cube.Outer())
                if (space.Edge(edge).Open && space.Inside(CubeId.Of(across)) && !claimed.ContainsKey(CubeId.Of(across))) beside.Add(CubeId.Of(across));
        var cubes = new List<(CubeId, bool, bool, IReadOnlyList<PoiKind>)>();
        foreach (var (cube, record) in claimed) cubes.Add((cube, true, _reef.IsArena(cube), record.Places.Select(p => p.Kind).ToList()));
        foreach (var cube in beside) cubes.Add((cube, false, false, Array.Empty<PoiKind>()));
        var her = InReef ? _world.Player.Position : _caveMouth >= 0 && _layerMaps.TryGetValue(_layer, out var layer) && _caveMouth < layer.Mouths.Count ? layer.Mouths[_caveMouth].At : _reef.Start;
        // (Framed on what she knows of the layer, a square's margin round it.)
        var lo = her;
        var hi = her;
        foreach (var (cube, _, _, _) in cubes)
        {
            lo = System.Numerics.Vector2.Min(lo, cube.Origin);
            hi = System.Numerics.Vector2.Max(hi, cube.Origin + cube.Size);
        }
        var area = new Rect2(lo.X - CubeTuning.Side, lo.Y - CubeTuning.Side, hi.X - lo.X + 2f * CubeTuning.Side, hi.Y - lo.Y + 2f * CubeTuning.Side);
        return new ChartView.ReefChart(area, _layer, _cubeDiving.Deepest, cubes, Here.Shut.ToList(), her);
    }

    /// <summary>A cube claimed: the places the director grants it, each in one of its sockets (CUBE-WORLD-PLAN §9).</summary>
    void Grant(CubeId cube)
    {
        var record = Here.Claimed[cube];
        var sockets = _reef!.Terrain(cube).Sockets;
        var granted = _director.Claim(cube.Z, record.Claim, sockets.Count);
        for (int i = 0; i < granted.Count; i++) record.Places.Add((granted[i], sockets[i]));
        if (granted.Count > 0) GD.Print($"Cube reef: {cube} (claim {record.Claim}) opens {string.Join(", ", granted)}");
    }

    /// <summary>Her layer's granted places not on its map yet, their mouths put on it; whether any were.</summary>
    bool OpenMouths(LevelMap map)
    {
        bool any = false;
        foreach (var record in Here.Claimed.Values)
            foreach (var (kind, mouth) in record.Places)
            {
                if (map.Mouths.Contains(mouth)) continue;
                // (Its kinds kept beside its mouths, in step.)
                while (map.MouthKinds.Count < map.Mouths.Count) map.MouthKinds.Add(PoiKind.LeakCave);
                map.Mouths.Add(mouth);
                map.MouthKinds.Add(kind);
                any = true;
            }
        return any;
    }

    /// <summary>The layer's mouths shown afresh (its view's rock round each, the portals' ink).</summary>
    void ShowReefMouths(int from)
    {
        _level.AddMouths(_map, from);
        ShowMouths();
    }

    /// <summary>How wide the window round her is that the reef's currents are shown in (metres).</summary>
    const float CurrentWindow = 96f;
    readonly List<CurrentView.Flow> _reefFlows = new();

    /// <summary>
    /// The reef's currents seen as the game's moving water is (CurrentView's shimmer, like the air over a hot road): the
    /// wake current pouring out of each shut throat of her cube, and the frontier drift, a gentle stream from her along
    /// its way toward new water.
    /// </summary>
    void SyncReefCurrents(float dt)
    {
        _reefFlows.Clear();
        var her = _world.Player.Position;
        var cube = CubeId.Of(her, _layer);
        foreach (var (edge, inside, _) in cube.Outer())
        {
            if (!Here.IsShut(edge)) continue;
            float into = edge.High == inside ? 1f : -1f;
            foreach (var gate in _reef!.Space.Edge(edge).Gates)
                _reefFlows.Add(new CurrentView.Flow(new[] { edge.Point(gate.Along, 0f), edge.Point(gate.Along, into * CubeCurrents.WakeReach) },
                    gate.Width * 0.5f, 4f, 1f));
        }
        var drift = new List<System.Numerics.Vector2> { her };
        var at = her;
        for (int i = 0; i < 16; i++)
        {
            var d = _cubeCurrents.DriftAt(at);
            if (d.LengthSquared() < 1e-6f) break;
            at += System.Numerics.Vector2.Normalize(d) * 2.5f;
            drift.Add(at);
        }
        if (drift.Count >= 3) _reefFlows.Add(new CurrentView.Flow(drift, 2.5f, 1.2f, 0.45f));
        var origin = her - new System.Numerics.Vector2(CurrentWindow * 0.5f);
        _currents.SyncFlows(_reefFlows, origin, CurrentWindow, false, dt, _camera);
    }

    /// <summary>Each frame she swims: the reef drawn round her, an arch over her seen through, the currents' streaks.</summary>
    void SyncReefView(float dt, Vector3 eye)
    {
        _reefView.Follow(eye);
        var p = _world.Player.Position;
        bool hidden = _reef!.Made(CubeId.Of(p, _layer))?.CanopyAt(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y)) == true
            || WallHides(_bell.GlobalPosition, _camera.Camera.GlobalPosition) || _level.Hides(_bell.GlobalPosition, _camera.Camera.GlobalPosition);
        _reefSeeThrough = SeeStep(_reefSeeThrough, hidden, dt);
        // (Her layer's reef life and mouths are cut with its rock; its map tells which rock the cut may take.)
        _level.SeeThrough(_bell.GlobalPosition, SeeThroughRadius, SeeScale(_reefSeeThrough));
        _reefView.SeeThrough(_bell.GlobalPosition, SeeThroughRadius, SeeScale(_reefSeeThrough), _level.SeeMask);
        _portals.SeeThrough(_bell.GlobalPosition, SeeThroughRadius, SeeScale(_reefSeeThrough), _level.Position.Y + LevelMap.SwimBand, _level.SeeMask);
    }

    /// <summary>How far round her (metres, on the camera's side) the water is kept in view: rock between the camera and any of it is cut.</summary>
    const float SeeClear = 5f;

    /// <summary>
    /// Whether rock of her layer, risen as it is drawn (ReefStreamView.Rise, topdown_ground's rise; a roof with its
    /// wall), stands between the camera and her or the water SeeClear round her on the camera's side: each such line
    /// stepped along, against each point's rock as it is drawn. So walls crowding in beside her are cut too, however
    /// close the camera has drawn (CameraRig.Close).
    /// </summary>
    bool WallHides(Vector3 her, Vector3 camera)
    {
        var toward = new Vector3(camera.X - her.X, 0f, camera.Z - her.Z);
        toward = toward.LengthSquared() < 1e-6f ? new Vector3(0f, 0f, 1f) : toward.Normalized();
        var side = new Vector3(-toward.Z, 0f, toward.X);
        if (LineHidden(her, camera)) return true;
        for (int k = -2; k <= 2; k++)
        {
            float a = k * Mathf.Pi / 4f;
            if (LineHidden(her + (toward * Mathf.Cos(a) + side * Mathf.Sin(a)) * SeeClear, camera)) return true;
        }
        return false;
    }

    bool LineHidden(Vector3 from, Vector3 camera)
    {
        var way = camera - from;
        for (int i = 0; i <= 40; i++)
        {
            var at = from + way * (i / 40f);
            if (at.Y > ReefStreamView.Rise + CubeTuning.RockLow + CubeTuning.RockRise) break;
            if (DrawnHeight(at.X, at.Z) > at.Y) return true;
        }
        return false;
    }

    /// <summary>Her layer's rock at a point as it is drawn: its wall risen, a roof risen whole over its water.</summary>
    float DrawnHeight(float x, float z)
    {
        int gx = Mathf.RoundToInt(x), gy = Mathf.RoundToInt(z);
        if (gx >= 0 && gy >= 0 && gx <= _map.Size && gy <= _map.Size && !float.IsNaN(_map.RoofAt(gx, gy))) return ReefStreamView.Rise + CubeTuning.RoofHeight;
        float h = _map.HeightAt(new System.Numerics.Vector2(x, z));
        float t = Mathf.Clamp((h - 1.4f) / (3.2f - 1.4f), 0f, 1f);
        return h + ReefStreamView.Rise * t * t * (3f - 2f * t);
    }

}
