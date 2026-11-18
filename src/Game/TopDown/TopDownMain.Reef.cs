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
/// came by, the frontier drift leads her on to new water; over the void she dives to the layer below (↓, the
/// d-pad's down), under a well of the layer above she climbs back (↑, the d-pad's up), the game's dive played either way.
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
    bool InArena(System.Numerics.Vector2 p, int layer) => _reef!.IsArena(CubeId.Of(G(p), layer));

    /// <summary>
    /// Where her layer's map lies in the reef (LevelMap.ReefOrigin): the game's metres on a layer are its map's, the
    /// reef's cubes the reef's own; <see cref="G"/> takes a point of hers into the reef, <see cref="L"/> one of the reef's
    /// onto her map. A layer's map is centred where she comes onto it (CubeReef.Layer), so the reef runs on round her.
    /// </summary>
    System.Numerics.Vector2 Frame => _layerMaps.TryGetValue(_layer, out var m) ? m.ReefOrigin : System.Numerics.Vector2.Zero;
    System.Numerics.Vector2 G(System.Numerics.Vector2 p) => p + Frame;
    System.Numerics.Vector2 L(System.Numerics.Vector2 g) => g - Frame;
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
            map.Exit.Position = at.Middle - map.ReefOrigin;
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
    float _layerTime, _layerPulse, _hoverDown, _hoverUp, _heldShown;
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
        _cubeDiving = new CubeDiving(_reef.Space.Layers, _diveCooldown);
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
    LevelMap LayerMap(int z) => _layerMaps.TryGetValue(z, out var map) ? map : _layerMaps[z] = z == 0 ? FirstLayer(_reef!) : _reef!.Layer(z, _reef.WindowCentre(z));

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
        // (Each kept layer read in the window it lay in.)
        foreach (var (z, corner) in saved.Windows)
            if (corner.Split('/') is [var x, var y] && int.TryParse(x, out int cx) && int.TryParse(y, out int cy))
                _reef!.Space.SetWindow(z, new CellCoord(cx + _reef.Space.Window / 2, cy + _reef.Space.Window / 2, z).Origin + new System.Numerics.Vector2(CubeTuning.Side * 0.5f));
        var map = _reef!.Layer(_layer, _reef.WindowCentre(_layer));
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
        map.Exit.Position = arena.Middle - map.ReefOrigin;
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
            Windows = _layerMaps.Keys.ToDictionary(z => z, z => $"{_reef.Space.WindowCorner(z).X}/{_reef.Space.WindowCorner(z).Y}"),
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
        foreach (var record in Here.Claimed.Values)
        {
            record.Left = _world.StockLeft(record.Cube);
            record.Seeds = _world.InkseedsLeft(record.Cube);
        }
        KeepLanterns();
    }

    /// <summary>Her layer's lit lanterns kept in their cubes' records (once lit, a lantern stays lit for the dive).</summary>
    void KeepLanterns()
    {
        foreach (var record in Here.Claimed.Values) record.Lantern = _world.LanternLeft(record.Cube) ?? record.Lantern;
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
        // (The reef's cubes drawn in this layer's frame.)
        _reefView.SetFrame(Frame);
        // Its claimed cubes' places, their mouths on its map (those granted before it was made).
        if (_map.Mouths.Count is var mouthsWere && OpenMouths(_map)) ShowReefMouths(mouthsWere);
        // Back on a layer: its claimed cubes' stock put back as she left it.
        foreach (var record in restored ? Enumerable.Empty<CubeRecord>() : Here.Claimed.Values)
        {
            foreach (var left in record.Left ?? Enumerable.Empty<StockLeft>())
                _world.PlaceStock(record.Cube, left.Spawn, left.Seed, left.Prime, left.At, left: true, shoal: left.Shoal);
            foreach (var at in record.Seeds ?? Enumerable.Empty<System.Numerics.Vector2>()) _world.PlaceInkseed(record.Cube, at);
        }
        // (Only her layer and the one above are ever needed again: §10.)
        foreach (int z in _layerMaps.Keys.Where(z => z < _layer - 1).ToList())
        {
            _layerMaps.Remove(z);
            _reef!.Forget(z);
        }
        // (Centred where she came onto this one: CubeReef.Layer.)
        StartMaking(_layer + 1, G(_world.Player.Position));
        ShowCues();
    }

    void StartMaking(int z, System.Numerics.Vector2 centre)
    {
        if (_reef is not { } reef || z >= reef.Space.Layers || _layerMaps.ContainsKey(z) || _makingLayer == z) return;
        _makingLayer = z;
        // (Its reef life worked out with it, off the main thread.)
        _making = Task.Run(() =>
        {
            var map = reef.Layer(z, centre);
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
        if (_layerMove == LayerMove.None) FollowBelow(G(p.Position), _layer + 1);
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
        using (Timed("laid: stones")) _world.PlaceChargeStonesIn(L(cube.Origin), L(cube.Origin + cube.Size), ReefHash.Of(_reef!.Space.Seed, ReefHash.Cube, cube.X, cube.Y, cube.Z, cube.OnEnd ? 1 : 0), CubeChargeStones);
        // Its stone lantern, if it has one: lit if she lit it before (its record), else plain stone.
        if (!_world.Lanterns.Any(l => l.Cube == cube) && CubeStock.Lantern(_reef.Space.Seed, _reef.Terrain(cube)) is { } lantern)
            _world.PlaceLantern(cube, L(lantern), Here.Claimed.TryGetValue(cube, out var kept) ? kept.Lantern : null);
        // (Verification, --dbg-charge: lit as they come.)
        if (_dbgChargeStones)
            for (int i = had; i < _world.ChargeStones.Count; i++) _world.ChargeStones[i].Charge = ChargeStoneTuning.GlowSeconds;
        // (Its life worked out on the pool, put in as it is ready: TakeLife.)
        var map = _map;
        var corner = L(cube.Origin);
        var area = new Rect2(corner.X, corner.Y, cube.Size.X, cube.Size.Y);
        _life.Add((map, area, _lifeAhead.Remove((map, cube), out var ahead) ? ahead : System.Threading.Tasks.Task.Run(() => LevelShape.ReefLife(map, area))));
        // (The way through the water found again over it: PlaneWorld.NavStale.)
        _world.NavStale(L(cube.Origin), L(cube.Origin + cube.Size));
        _sunStale = true;
    }

    /// <summary>Her layer just shown: the reef life of every cube laid on it so far (the rest as each is laid).</summary>
    void ShowLaidLife()
    {
        if (_reef is null || !InReef || !_layerMaps.TryGetValue(_layer, out var map) || map != _map) return;
        _reef.Settle();
        foreach (var cube in _reef.LaidOn(_layer)) LaidHere(cube);
        // (What was worked out ahead, put in at once: no reef life popping in after she arrives.)
        TakeLife();
        // (What was worked out ahead for any other map let go.)
        foreach (var key in _lifeAhead.Keys.Where(k => k.Item1 != map).ToList()) _lifeAhead.Remove(key);
        _sunStale = false;
    }

    StreamFocus ReefFocus(PlaneBody p)
    {
        if (_layerMove != LayerMove.None)
            return new StreamFocus { Position = G(_layerFrom), Layer = LayerTarget, Climbable = _layerMove == LayerMove.Down };
        bool soon = _cubeDiving.CooldownLeft < 5f;
        return new StreamFocus
        {
            Position = G(p.Position), Velocity = p.Velocity, Layer = _layer, Climbable = _cubeDiving.Climbable, Shut = Here.IsShut,
            ReadyBelow = _hoverDown >= 0.5f || soon && _layer + 1 < _reef!.Space.Layers,
            ReadyAbove = _cubeDiving.Climbable && (_hoverUp >= 0.5f || soon),
        };
    }

    /// <summary>After each of her ticks: the currents move her; a cube line crossed claims a cube or holds her at a shut edge.</summary>
    void StepReefCurrents()
    {
        var p = _world.Player;
        var cube = CubeId.Of(G(p.Position), _layer);
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
            var flow = _cubeCurrents.DriftAt(G(p.Position)) + CubeCurrents.WakeAt(G(p.Position), cube, _reef.Space, Here);
            if (flow != System.Numerics.Vector2.Zero) _world.Move(ref p.Position, ref flow, _world.Radius, report: false);
        }
        // The seeds near her ride the currents as she does (her cube's drift, and the wake current anywhere).
        if (_world.Inkseeds.Count > 0)
        {
            var mine = _reef.Made(cube) is not null && !InArena(p.Position, _layer) ? cube : (CubeId?)null;
            foreach (var seed in _world.Inkseeds)
            {
                if (System.Numerics.Vector2.DistanceSquared(seed.Position, p.Position) > InkseedTuning.Still * InkseedTuning.Still) continue;
                var at = G(seed.Position);
                var its = CubeId.Of(at, _layer);
                if (_reef.Made(its) is null || InArena(seed.Position, _layer)) continue;
                var flow = (its == mine ? _cubeCurrents.DriftAt(at) : System.Numerics.Vector2.Zero) + CubeCurrents.WakeAt(at, its, _reef.Space, Here);
                if (flow != System.Numerics.Vector2.Zero) _world.Move(ref seed.Position, ref flow, seed.Radius, report: false);
            }
        }
        var to = G(p.Position);
        var crossed = Here.Move(G(p.PrevPosition), ref to);
        if (crossed == Crossing.Held)
        {
            p.Position = L(to);
            p.Velocity = System.Numerics.Vector2.Zero;
            _heldShown = 2f;
        }
        // A cube claimed: its stock buds (never the warm-up layer's, nor the cube she arrived in).
        else if (crossed == Crossing.Claimed)
        {
            var claimed = CubeId.Of(G(p.Position), _layer);
            // (The arena holds its boss alone: no stock, no places.)
            using var _ = Timed("reef claim");
            var sp = Timed("claim: spots");
            var spots = _reef.IsArena(claimed) ? new List<StockSpot>() : CubeStock.Spots(_reef.Space.Seed, claimed, Here.Claimed[claimed].Claim, _map.Menace, G(p.Position), (at, r) => _world.Clear(L(at), r));
            sp.Dispose();
            using (Timed("claim: stock")) foreach (var s in spots) _world.PlaceStock(claimed, s.Spawn, s.Seed, s.Prime, new[] { L(s.At) }, left: false);
            // Its inkseeds (none in an arena).
            if (!_reef.IsArena(claimed))
                using (Timed("claim: seeds"))
                    foreach (var at in CubeStock.Inkseeds(_reef.Space.Seed, claimed, Here.Claimed[claimed].Claim, _map.Menace, G(p.Position), _reef.Terrain(claimed), (at, r) => _world.Clear(L(at), r)))
                        _world.PlaceInkseed(claimed, L(at));
            using (Timed("claim: grant")) if (!_reef.IsArena(claimed)) Grant(claimed);
            using (Timed("claim: mouths")) if (_map.Mouths.Count is var mouthsWere && OpenMouths(_map)) ShowReefMouths(mouthsWere);
            GD.Print($"Cube reef: {claimed} claimed ({Here.Claims} on layer {_layer}), {spots.Count} of its stock budded");
            using (Timed("claim: route")) if (_autopilot) Route();
        }
        if (++_revealTick % 6 == 0) Here.Reveal(G(p.Position), 12f);
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
        _down = _cubeDiving.CanDive(G(p), _world.Radius, _reef!.Made);
        _up = _cubeDiving.CanClimb(G(p), _world.Radius, _reef.Made);
        // (The layer she would arrive on must be made too; one she has been on, kept as it lay, must hold where she would
        // come onto it: one she has not is made again round her as she dives.)
        if (_down == LayerBlock.None && (!_layerMaps.ContainsKey(_layer + 1) || !Recentres(_layer + 1) && !Holds(_layer + 1, G(p)))) _down = LayerBlock.NotReady;
        if (_up == LayerBlock.None && _layerMaps.ContainsKey(_layer - 1) && !Holds(_layer - 1, G(p))) _up = LayerBlock.NoWay;
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
        Recentre(move);
        RollBoss(move);
        GD.Print($"Cube reef: {(move == LayerMove.Down ? "dive" : "climb")} from layer {_layer} at {_layerFrom}");
        return true;
    }

    /// <summary>
    /// A layer she has never been on is made round where she comes onto it (CubeReef.Layer): so on every layer the reef
    /// runs on as far every way from her arrival, however far she has come.
    /// </summary>
    bool Recentres(int z) => _records.Of(z).Claims == 0;

    /// <summary>Whether a layer's map, as it lies, holds a point of the reef (its cube inside the layer's window).</summary>
    bool Holds(int z, System.Numerics.Vector2 g) => _reef!.Space.Inside(CubeId.Of(g, z));

    /// <summary>The map of the layer she dives to, made again round her arrival on the pool as she dives (null: none).</summary>
    Task<LevelMap>? _targetMaking;

    /// <summary>
    /// Diving to a layer she has never been on, that lies off where she comes onto it (her arrival more than
    /// RecentreSlack squares off its window's middle): its window set round her arrival at once (the stream and its arena
    /// read it), its map made again on the pool while she sinks (LayerReady waits for it).
    /// </summary>
    void Recentre(LayerMove move)
    {
        int to = LayerTarget;
        _targetMaking = null;
        if (move != LayerMove.Down || !Recentres(to)) return;
        var g = G(_layerFrom);
        var at = CellCoord.Of(g, to);
        FollowBelow(g, to);
        // (Its map as it lies already in that window: kept.)
        if (_layerMaps.TryGetValue(to, out var kept) && kept.ReefOrigin == _reef!.Space.WindowCorner(to).Origin) return;
        if (_makingLayer == to)
        {
            _making = null;
            _makingLayer = -1;
        }
        _layerMaps.Remove(to);
        var reef = _reef;
        _targetMaking = Task.Run(() =>
        {
            var map = reef.Layer(to);
            LevelShape.Bare(map);
            return map;
        });
        GD.Print($"Cube reef: layer {to} made again round her arrival {at}");
    }

    /// <summary>How many squares off a layer's window's middle her arrival may lie before the layer is made again round it.</summary>
    const int RecentreSlack = 3;

    /// <summary>
    /// The window of a layer she has never been on (the one under hers) kept round her as she swims (more than
    /// RecentreSlack squares off its middle: set round her again), so the stream makes the cubes under her wherever she
    /// is, and she may dive anywhere. Only its window moves: its map is made again in it as she dives (Recentre).
    /// </summary>
    void FollowBelow(System.Numerics.Vector2 g, int z)
    {
        if (z >= _reef!.Space.Layers || !Recentres(z)) return;
        var at = CellCoord.Of(g, z);
        var middle = _reef.Space.Middle(z);
        if (Math.Max(Math.Abs(at.X - middle.X), Math.Abs(at.Y - middle.Y)) > RecentreSlack) _reef.Space.SetWindow(z, g);
    }

    /// <summary>The arena of the layer she dives to, made on the pool as she dives (null: none being made).</summary>
    System.Threading.Tasks.Task? _arenaMaking;

    /// <summary>
    /// Diving to a new deepest layer: the layer left counts toward the boss streak, and the one she goes to rolls its boss
    /// (§9.1), as the dive begins: its arena's terrain made again (with its basin) and laid on its map on the pool while
    /// she sinks, so nothing of it is worked out as she arrives.
    /// </summary>
    void RollBoss(LayerMove move)
    {
        int to = LayerTarget;
        if (move != LayerMove.Down || to <= _cubeDiving.Deepest || !_bossRolled.Add(to)) return;
        _director.LeftLayer(_layer, _world.Boss?.Freed == true);
        var arrival = CubeId.Of(G(_layerFrom), to);
        if ((!_director.BossOn(to) && !_cubeBoss) || _director.ArenaFor(_reef!.Space, arrival) is not { } arena) return;
        var reef = _reef;
        // (On the layer's map once it is made: made again round her, or as it was.)
        var making = _targetMaking ?? (_layerMaps.TryGetValue(to, out var kept) ? Task.FromResult(kept) : null);
        if (making is null) return;
        _arenaMaking = making.ContinueWith(made =>
        {
            var layerMap = made.Result;
            layerMap.HasBoss = true;
            layerMap.Exit.Position = arena.Middle - layerMap.ReefOrigin;
            layerMap.Exit.Radius = CubeTerrain.ArenaRadius;
            reef.SetArena(arena, layerMap);
        });
        GD.Print($"Cube reef: layer {to} holds a boss, its arena {arena} (streak {_director.Streak})");
    }

    /// <summary>
    /// One frame of a dive or a climb: the game's dive (she gathers, turns head-first and sinks a layer's drop in pulses;
    /// climbing she rises), the camera drawing in and back out, her layer opening like an iris round her (the layer above
    /// opening into view as she rises into it). Should the cube she arrives in not be ready, the middle of it loops.
    /// </summary>
    void StepLayerMove(float dt)
    {
        var arrival = CubeId.Of(G(_layerFrom), LayerTarget);
        bool ready = LayerReady(arrival);
        _layerTime += dt;
        if (!ready && _layerTime > LayerHold) _layerWaited += dt;
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
        if (_layerTime >= LayerSeconds && ready)
            using (Timed("layer finish"))
                FinishLayerMove(arrival);
    }

    /// <summary>How long the dive's middle may loop waiting for the layer she goes to, at most (s).</summary>
    const float LayerWaitMost = 6f;
    float _layerWaited;

    /// <summary>
    /// Whether the layer she goes to is all there, so nothing pops in as she arrives: the cube she lands in Active, every
    /// cube the stream wants round it ready and drawn, every cube made laid on its map, and the reef life of those laid
    /// on her new layer worked out ahead (taken up as it is shown: LaidHere).
    /// </summary>
    bool LayerReady(CubeId arrival)
    {
        // (The map of the layer she goes to, made again round her: kept once made.)
        if (_targetMaking is { } making)
        {
            if (!making.IsCompleted) return false;
            if (making.IsCompletedSuccessfully) _layerMaps[LayerTarget] = making.Result;
            else GD.PushError($"Cube layer {LayerTarget} failed: {making.Exception}");
            _targetMaking = null;
        }
        if (_layerWaited > LayerWaitMost) return _stream.State(arrival) == CubeState.Active && _layerMaps.ContainsKey(LayerTarget);
        if (_arenaMaking is { IsCompleted: false }) return false;
        if (_stream.State(arrival) != CubeState.Active || _reefView.Pending > 0 || !_reef!.Settled) return false;
        foreach (var (cube, want) in _stream.Wanted)
            if (want != CubeState.Mask && _stream.State(cube) != want) return false;
        if (!_layerMaps.TryGetValue(LayerTarget, out var map)) return false;
        bool all = true;
        foreach (var cube in _reef.LaidOn(LayerTarget))
        {
            if (!_lifeAhead.TryGetValue((map, cube), out var work))
            {
                var corner = cube.Origin - map.ReefOrigin;
                var area = new Rect2(corner.X, corner.Y, cube.Size.X, cube.Size.Y);
                _lifeAhead[(map, cube)] = work = System.Threading.Tasks.Task.Run(() => LevelShape.ReefLife(map, area));
            }
            all &= work.IsCompleted;
        }
        return all;
    }

    /// <summary>Reef life worked out ahead, for the layer she is diving or climbing to (LayerReady).</summary>
    readonly Dictionary<(LevelMap, CubeId), System.Threading.Tasks.Task<(List<OctoShoots.Game.Fx.FloraItem>, List<Transform3D>, List<Transform3D>)>> _lifeAhead = new();

    /// <summary>The change over: she is on the layer she went to, its world made, the view just as it was.</summary>
    void FinishLayerMove(CubeId arrival)
    {
        var terrain = _reef!.Terrain(arrival);
        bool down = _layerMove == LayerMove.Down;
        int from = _layer;
        // A boss freed stays freed: the layer, should she climb back to it, has none.
        if (_world.Boss?.Freed == true) _map.HasBoss = false;
        // The layer left: what is left of each claimed cube's stock and seeds, kept for her return.
        foreach (var record in Here.Claimed.Values)
        {
            record.Left = _world.StockLeft(record.Cube);
            record.Seeds = _world.InkseedsLeft(record.Cube);
        }
        KeepLanterns();
        // (Where she comes onto the layer, in the reef; and the frame she leaves.)
        var g = G(_layerFrom);
        var frameWas = Frame;
        if (down) _cubeDiving.Dived(g, terrain);
        else _cubeDiving.Climbed(g, terrain);
        _layer = _cubeDiving.Layer;
        // Onto the layer's own map: she, and all that is drawn, in its frame (the camera, her bell and the water's motes
        // moved with her, so the view is just as it was).
        _layerFrom = L(g);
        var by = frameWas - Frame;
        if (by != System.Numerics.Vector2.Zero)
        {
            var shift = new Vector3(by.X, 0f, by.Y);
            _camera.Shift(shift);
            _bell.Shift(shift);
            _snow.Shift(shift);
            _plankton.Shift(shift);
        }
        _reefView.SetFrame(Frame);
        // (Its boss rolled, and its arena made, as the dive began: RollBoss.)
        if (_arenaMaking is { IsFaulted: true } failed) GD.PushError(failed.Exception?.ToString());
        _arenaMaking = null;
        _records.Changed(from, _layer, _cubeDiving.Deepest);
        if (Here.Arrive(g) == Crossing.Claimed) Grant(CubeId.Of(g, _layer));
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
        _layerWaited = 0f;
        using (Timed("layer show level")) ShowLevel(LevelShape.Bare(_layerMaps[_layer]), keepRun: true, 0, _layerFrom);
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
        var cube = CubeId.Of(G(her), _layer);
        if (_cubeBoss && _world.Boss is { Freed: false } && _reef!.ArenaOf(_layer) is { } arena)
            return LevelValidator.ShortestPath(_map, her, L(arena.Middle), clearance: 1f);
        foreach (var (edge, gate, _) in _reef!.Terrain(cube).Gates.OrderBy(g => System.Numerics.Vector2.Distance(g.Mouth, G(her))))
        {
            if (Here.IsShut(edge)) continue;
            var inside = CubeId.Of(edge.High) == cube ? edge.High : new CellCoord(edge.High.X - (edge.Axis == EdgeAxis.X ? 1 : 0), edge.High.Y - (edge.Axis == EdgeAxis.Y ? 1 : 0), _layer);
            float into = edge.High == inside ? 1f : -1f;
            var across = edge.Point(gate.Along, -into * 8f);
            if (Here.IsClaimed(CubeId.Of(across, _layer)) || !_reef.Space.Inside(CubeId.Of(across, _layer))) continue;
            var path = LevelValidator.ShortestPath(_map, her, L(across), clearance: 1f);
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
        // (In the reef's metres, as the chart's cubes are.)
        var her = InReef ? G(_world.Player.Position) : _caveMouth >= 0 && _layerMaps.TryGetValue(_layer, out var layer) && _caveMouth < layer.Mouths.Count ? layer.Mouths[_caveMouth].At + layer.ReefOrigin : _reef.Start;
        // (Framed on what she knows of the layer, a square's margin round it.)
        var lo = her;
        var hi = her;
        foreach (var (cube, _, _, _) in cubes)
        {
            lo = System.Numerics.Vector2.Min(lo, cube.Origin);
            hi = System.Numerics.Vector2.Max(hi, cube.Origin + cube.Size);
        }
        var area = new Rect2(lo.X - CubeTuning.Side, lo.Y - CubeTuning.Side, hi.X - lo.X + 2f * CubeTuning.Side, hi.Y - lo.Y + 2f * CubeTuning.Side);
        // (Its lanterns: those in the cubes she has claimed, lit or not; from the records while she is behind a mouth.)
        var lanterns = new List<(System.Numerics.Vector2, bool)>();
        if (InReef)
        {
            foreach (var l in _world.Lanterns)
                if (claimed.ContainsKey(l.Cube)) lanterns.Add((G(l.Position), l.Lit));
        }
        else
            foreach (var (cube, record) in claimed)
                if (CubeStock.Lantern(space.Seed, _reef.Terrain(cube)) is { } at) lanterns.Add((at, record.Lantern is not null));
        return new ChartView.ReefChart(area, _layer, _cubeDiving.Deepest, cubes, Here.Shut.ToList(), her, lanterns);
    }

    /// <summary>A cube claimed: the places the director grants it, each in one of its sockets (CUBE-WORLD-PLAN §9).</summary>
    void Grant(CubeId cube)
    {
        var record = Here.Claimed[cube];
        var sockets = _reef!.Terrain(cube).Sockets;
        var granted = _director.Claim(cube.Z, record.Claim, sockets.Count);
        // A secret only behind a wall the camera faces (its passage running north into the rock): its cracked seal must
        // be seen. Secrets placed first; a secret with no such socket left is not placed.
        var free = sockets.ToList();
        foreach (var kind in granted.OrderBy(k => k == PoiKind.Secret ? 0 : 1))
        {
            int pick = kind == PoiKind.Secret ? free.FindIndex(FacesCamera) : free.FindIndex(s => !FacesCamera(s));
            if (pick < 0 && kind != PoiKind.Secret) pick = free.Count > 0 ? 0 : -1;
            if (pick < 0) continue;
            record.Places.Add((kind, free[pick]));
            free.RemoveAt(pick);
        }
        if (granted.Count > 0) GD.Print($"Cube reef: {cube} (claim {record.Claim}) opens {string.Join(", ", granted)}");
    }

    /// <summary>Her layer's granted places not on its map yet, their mouths put on it; whether any were.</summary>
    bool OpenMouths(LevelMap map)
    {
        bool any = false;
        foreach (var record in Here.Claimed.Values)
            foreach (var (kind, granted) in record.Places)
            {
                // (Granted in the reef's metres; on the map, in its own.)
                var mouth = granted with { At = granted.At - map.ReefOrigin };
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
        ShowCues();
    }

    /// <summary>Her layer's mouth (by its index on the map) and the claimed cube's record and place it is, if any.</summary>
    (CubeRecord Record, int Place)? PlaceOfMouth(int mouth)
    {
        if (_reef is null || !_layerMaps.TryGetValue(_layer, out var map) || mouth < 0 || mouth >= map.Mouths.Count) return null;
        var at = map.Mouths[mouth].At + map.ReefOrigin;
        foreach (var record in Here.Claimed.Values)
            for (int i = 0; i < record.Places.Count; i++)
                if (System.Numerics.Vector2.DistanceSquared(record.Places[i].Mouth.At, at) < 0.01f) return (record, i);
        return null;
    }

    /// <summary>Whether a mouth's wall faces the camera (which sits south, looking north): its passage runs north into the rock.</summary>
    static bool FacesCamera(Doorway mouth) => mouth.Into.Y <= -0.5f * mouth.Into.Length();

    /// <summary>Her layer's secrets' mouths sealed with a cracked wall, but those whose seal she has broken (or been through).</summary>
    void SealSecrets()
    {
        if (!InReef || _reef is null) return;
        for (int i = 0; i < _map.Mouths.Count; i++)
        {
            if (i >= _map.MouthKinds.Count || _map.MouthKinds[i] != PoiKind.Secret || _world.ShutMouths.Contains(i)) continue;
            var mouth = _map.Mouths[i];
            if (_world.CrackSeals.Any(s => s.Mouth == mouth)) continue;
            if (PlaceOfMouth(i) is { } p && (p.Record.Opened.Contains(p.Place) || p.Record.Visited.Contains(p.Place))) continue;
            SealMouth(mouth);
        }
    }

    /// <summary>The sealed passages' rock (CubeShapes.Plug), by their seal.</summary>
    readonly Dictionary<CrackSeal, CubeShapes.PlugShape> _plugs = new();

    /// <summary>
    /// A mouth of her layer sealed: its passage drawn as the rock it was (flush with the wall round it, one with the
    /// ground), the seal's face where that rock's is at her plane.
    /// </summary>
    CrackSeal SealMouth(Doorway mouth)
    {
        var door = mouth with { At = G(mouth.At) };
        var plug = _reef is null ? null : CubeShapes.Plug(_reef.Terrain(CubeId.Of(door.At, _layer)), door);
        if (plug is null) return _world.PlaceCrackSeal(mouth);
        var into = System.Numerics.Vector2.Normalize(door.Into);
        System.Numerics.Vector2? face = null;
        for (float s = -6f; s <= Doorway.Depth; s += 0.05f)
        {
            var q = door.At + into * s;
            if (plug.Height(q.X, q.Y) < LevelMap.SwimBand) continue;
            face = L(q);
            break;
        }
        var seal = _world.PlaceCrackSeal(mouth, face);
        _plugs[seal] = plug;
        _reefView.ShowPlug(seal, plug.Shape);
        return seal;
    }

    /// <summary>Each frame: a sealed passage's rock gone with its seal (broken, or her world left).</summary>
    void SyncPlugs()
    {
        if (_plugs.Count == 0) return;
        List<CrackSeal>? gone = null;
        foreach (var seal in _plugs.Keys)
            if (seal.Open || !_world.CrackSeals.Contains(seal)) (gone ??= new()).Add(seal);
        if (gone is null) return;
        foreach (var seal in gone)
        {
            _plugs.Remove(seal);
            _reefView.HidePlug(seal);
        }
    }

    /// <summary>A sealed passage's rock's own height under a point of her plane, if it is one's.</summary>
    float? PlugSurface(CrackSeal seal, System.Numerics.Vector2 p)
    {
        if (!_plugs.TryGetValue(seal, out var plug)) return null;
        var g = G(p);
        return plug.Height(g.X, g.Y);
    }

    /// <summary>A cracked seal crumbled: its place kept open for good.</summary>
    void SealCrumbled(System.Numerics.Vector2 at)
    {
        if (_world.CrackSeals.FirstOrDefault(s => s.Open && System.Numerics.Vector2.DistanceSquared(s.Rock.Center, at) < 0.01f) is not { } seal) return;
        int i = _map.Mouths.IndexOf(seal.Mouth);
        if (PlaceOfMouth(i) is { } p) p.Record.Opened.Add(p.Place);
    }

    /// <summary>Whether she has been in through this mouth of her layer.</summary>
    bool MouthVisited(int mouth) => PlaceOfMouth(mouth) is { } p && p.Record.Visited.Contains(p.Place);

    /// <summary>Into a place through this mouth: kept as visited (a secret's emblem shows from then on).</summary>
    void VisitMouth(int mouth)
    {
        if (PlaceOfMouth(mouth) is { } p) p.Record.Visited.Add(p.Place);
    }

    /// <summary>The arrows carved round her layer's secrets' mouths (CubeStock.Cues), shown in her beam (SecretCueView).</summary>
    void ShowCues()
    {
        SealSecrets();
        var cues = new List<(System.Numerics.Vector2, System.Numerics.Vector2)>();
        if (InReef && _reef is not null)
            for (int i = 0; i < _map.Mouths.Count; i++)
                if (i < _map.MouthKinds.Count && _map.MouthKinds[i] == PoiKind.Secret && !_world.ShutMouths.Contains(i))
                    cues.AddRange(CubeStock.Cues(_reef.Space.Seed, _map.Mouths[i], _map.HeightAt, _world.LineOfSight));
        _cueView.Show(cues, _map.HeightAt);
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
        var cube = CubeId.Of(G(her), _layer);
        foreach (var (edge, inside, _) in cube.Outer())
        {
            if (!Here.IsShut(edge)) continue;
            float into = edge.High == inside ? 1f : -1f;
            foreach (var gate in _reef!.Space.Edge(edge).Gates)
                _reefFlows.Add(new CurrentView.Flow(new[] { L(edge.Point(gate.Along, 0f)), L(edge.Point(gate.Along, into * CubeCurrents.WakeReach)) },
                    gate.Width * 0.5f, 4f, 1f));
        }
        var drift = new List<System.Numerics.Vector2> { her };
        var at = her;
        for (int i = 0; i < 16; i++)
        {
            var d = _cubeCurrents.DriftAt(G(at));
            if (d.LengthSquared() < 1e-6f) break;
            at += System.Numerics.Vector2.Normalize(d) * 2.5f;
            drift.Add(at);
        }
        if (drift.Count >= 3) _reefFlows.Add(new CurrentView.Flow(drift, 2.5f, 1.2f, 0.45f));
        var origin = her - new System.Numerics.Vector2(CurrentWindow * 0.5f);
        _currents.SyncFlows(_reefFlows, origin, CurrentWindow, false, dt, _camera);
    }

    /// <summary>
    /// Each frame she swims: the reef drawn round her, and the cutout always on: every wall on
    /// the camera's side of her (the screen's lower half, she being at its middle) cut down to its cap.
    /// </summary>
    void SyncReefView(float dt, Vector3 eye)
    {
        _reefView.Follow(eye);
        float open = _dbgNoCarve ? 0f : 1f;
        // (Her layer's reef life and mouths are cut with its rock.)
        _level.SeeThrough(_bell.GlobalPosition, ReefSeeBehind, open);
        _reefView.SeeThrough(_bell.GlobalPosition, ReefSeeBehind, open);
        _portals.SeeThrough(_bell.GlobalPosition, ReefSeeBehind, open, _level.Position.Y + LevelMap.SwimBand);
        _crackView.SeeThrough(_bell.GlobalPosition, ReefSeeBehind, open, _level.Position.Y + LevelMap.SwimBand);
    }

    /// <summary>How far behind her line (metres, away from the camera) the reef's cut starts: her bell's own reach.</summary>
    const float ReefSeeBehind = 0.5f;

}
