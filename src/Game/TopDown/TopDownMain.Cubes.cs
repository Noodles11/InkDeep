using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Cubes;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A dive in the cube reef (docs/proposals/CUBE-WORLD-PLAN.md §15.1): one continuous reef of layers, each a level as the
/// game's systems see it (CubeReef stitches its cubes into one map), its rock drawn as it streams in round her
/// (ReefStream, ReefStreamView). She claims the cubes she swims into, the wake current shuts the edge behind the one she
/// came by, the frontier drift leads her on to new water; over the void she dives to the layer below (Shift, the
/// d-pad's down), under a well of the layer above she climbs back (Q, the d-pad's up), the game's dive played either way.
/// A new dive is in the cube reef; `--classic` keeps the seeded levels, `--cube-reef` puts a verification run in it.
/// </summary>
public partial class TopDownMain
{
    enum LayerMove { None, Down, Up }

    /// <summary>The change of layer's length (the game's dive), the point it holds at while what she arrives in is not ready, and how close the camera draws in.</summary>
    const float LayerSeconds = 1.6f, LayerHold = 0.6f, LayerCloser = 0.4f;

    CubeReef? _cubes;
    ReefStream _stream = null!;
    TaskCubeWorker _cubeWorker = null!;
    ReefStreamView _cubeView = null!;
    CubeDiving _cubeDiving = null!;
    readonly ReefRecords _records = new();
    readonly CubeCurrents _cubeCurrents = new();
    int _layer;
    int _cubeFootprint = CubeTuning.Footprint, _cubeLayers = CubeTuning.Layers;
    bool _classic, _cubeAsked;
    /// <summary>Her layer's map and the one above's (kept to climb back to), and the next one down being made.</summary>
    readonly Dictionary<int, LevelMap> _layerMaps = new();
    Task<LevelMap>? _making;
    int _makingLayer = -1;

    LayerMove _layerMove;
    float _layerTime, _layerPulse, _hoverDown, _hoverUp, _heldShown, _cubeSeeThrough;
    System.Numerics.Vector2 _layerFrom;
    bool _downLatch, _upLatch;
    LayerBlock _down = LayerBlock.NotReady, _up = LayerBlock.NotReady;
    ReefLook? _layerLookFrom, _layerLookTo;
    int _revealTick;
    MeshInstance3D _driftStreaks = null!;
    readonly List<MeshInstance3D> _wakeStreaks = new();
    Shader _currentShader = null!;

    LayerRecords Here => _records.Of(_layer);
    int LayerTarget => _layer + (_layerMove == LayerMove.Down ? 1 : -1);
    /// <summary>In the cube reef's open water (not in a place behind a mouth).</summary>
    bool InCubes => _cubes is not null && !InDiveCave;

    /// <summary>The flags of the cube reef (read with the others).</summary>
    void ParseCubeArg(string arg)
    {
        if (arg == "--classic") _classic = true;
        else if (arg == "--cube-reef") _cubeAsked = true;
        else if (arg.StartsWith("--cube-footprint=") && int.TryParse(arg["--cube-footprint=".Length..], out int f)) _cubeFootprint = Math.Clamp(f, 2, CubeTuning.Footprint);
        else if (arg.StartsWith("--cube-layers=") && int.TryParse(arg["--cube-layers=".Length..], out int l)) _cubeLayers = Math.Clamp(l, 2, CubeTuning.Layers);
    }

    /// <summary>Whether this launch's dive is in the cube reef: a new dive (not one under way in the seeded levels), or asked for.</summary>
    bool WantsCubes(bool newDive) => !_classic && (_cubeAsked || newDive);

    /// <summary>The reef from the run's seed, and its views; she begins on the warm-up layer.</summary>
    void BuildCubes()
    {
        _cubes = new CubeReef(_seed.Value, _cubeFootprint, _cubeLayers);
        _id = CubeReef.IdOf(0);
        _layer = 0;
        _cubeWorker = new TaskCubeWorker(_cubes.Space, BuildCube, _cubes.Terrain);
        _stream = new ReefStream(_cubes.Space, _cubeWorker);
        _cubeWorker.Stream = _stream;
        _cubeDiving = new CubeDiving(_cubes.Space.Layers);
        _cubeView = new ReefStreamView();
        AddChild(_cubeView);
        _currentShader = GD.Load<Shader>("res://assets/shaders/cube_current.gdshader");
        _driftStreaks = Streaks(new Vector2(26f, 26f));
        Here.Arrive(_cubes.Start);
        GD.Print($"Cube reef {_seed}: {_cubes.Space.Footprint} × {_cubes.Space.Footprint} squares, {_cubes.Space.Layers} layers, {_cubes.Space.CubesOf(0).Count()} cubes a layer");
    }

    /// <summary>The stream's work off the main thread that is the game's: a cube's meshes.</summary>
    static object? BuildCube(CubeJob job, CubeTerrain terrain) => job.Work switch
    {
        CubeWork.Mesh => CubeShapes.Ground(terrain),
        CubeWork.PreviewMesh => CubeShapes.Preview(terrain),
        _ => "nothing",
    };

    /// <summary>A layer as the level she is on (made off the main thread; kept while she may come back to it).</summary>
    LevelMap LayerMap(int z) => _layerMaps.TryGetValue(z, out var map) ? map : _layerMaps[z] = _cubes!.Layer(z, _cubes.Start);

    /// <summary>The warm-up layer's map and its (bare) shape: the first level of a dive in the reef.</summary>
    Task<LevelShape> CubeFirstShape()
    {
        var reef = _cubes!;
        return Task.Run(() => LevelShape.Bare(reef.Layer(0, reef.Start)));
    }

    /// <summary>The world's options on a layer: the cube reef's (its murklings stocked as she claims cubes), no leaks, no corruption on the ground.</summary>
    static PlaneOptions CubeReefOptions() => new() { Corruption = false, Pufferlings = false, Vases = false, Adventure = false, Cubes = true };

    /// <summary>A layer entered (the first, or after a change of layer): its map kept, the next one down begun.</summary>
    void EnteredLayer()
    {
        _layerMaps[_layer] = _map;
        // Back on a layer: its claimed cubes' stock put back as she left it.
        foreach (var record in Here.Claimed.Values)
            foreach (var left in record.Left ?? Enumerable.Empty<StockLeft>())
                _world.PlaceStock(record.Cube, left.Spawn, left.Prime, left.At);
        // (Only her layer and the one above are ever needed again: §10.)
        foreach (int z in _layerMaps.Keys.Where(z => z < _layer - 1).ToList())
        {
            _layerMaps.Remove(z);
            _cubes!.Forget(z);
        }
        StartMaking(_layer + 1);
    }

    void StartMaking(int z)
    {
        if (_cubes is not { } reef || z >= reef.Space.Layers || _layerMaps.ContainsKey(z) || _makingLayer == z) return;
        _makingLayer = z;
        _making = Task.Run(() => reef.Layer(z, reef.Start));
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
    void StepCubes(float dt)
    {
        TakeMade();
        var p = _world.Player;
        _stream.Update(CubeFocus(p), dt);
        _cubeWorker.Drain(_stream);
        while (_cubeWorker.Errors.TryDequeue(out var error)) GD.PushError($"Cube work failed: {error}");
        _cubeView.Take(_stream);
        _cubeView.Apply();
    }

    StreamFocus CubeFocus(PlaneBody p)
    {
        if (_layerMove != LayerMove.None)
            return new StreamFocus { Position = _layerFrom, Layer = LayerTarget, Climbable = _layerMove == LayerMove.Down };
        bool soon = _cubeDiving.CooldownLeft < 5f;
        return new StreamFocus
        {
            Position = p.Position, Velocity = p.Velocity, Layer = _layer, Climbable = _cubeDiving.Climbable, Shut = Here.IsShut,
            ReadyBelow = _hoverDown >= 0.5f || soon && _layer + 1 < _cubes!.Space.Layers,
            ReadyAbove = _cubeDiving.Climbable && (_hoverUp >= 0.5f || soon),
        };
    }

    /// <summary>After each of her ticks: the currents move her; a cube line crossed claims a cube or holds her at a shut edge.</summary>
    void StepCubeCurrents()
    {
        var p = _world.Player;
        var cube = CubeId.Of(p.Position, _layer);
        if (_cubes!.Made(cube) is { } here)
        {
            _cubeCurrents.Prepare(here, _cubes.Space, Here);
            var flow = _cubeCurrents.DriftAt(p.Position) + CubeCurrents.WakeAt(p.Position, cube, _cubes.Space, Here);
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
            var spots = CubeStock.Spots(_cubes.Space.Seed, claimed, Here.Claimed[claimed].Claim, _map.Menace, p.Position, _world.Clear);
            foreach (var s in spots) _world.PlaceStock(claimed, s.Spawn, s.Prime, s.At);
            GD.Print($"Cube reef: {claimed} claimed ({Here.Claims} on layer {_layer}), {spots.Count} of its stock budded");
            if (_autopilot) Route();
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
        _down = _cubeDiving.CanDive(p, _world.Radius, _cubes!.Made);
        _up = _cubeDiving.CanClimb(p, _world.Radius, _cubes.Made);
        // (The layer she would arrive on must be made too.)
        if (_down == LayerBlock.None && !_layerMaps.ContainsKey(_layer + 1)) _down = LayerBlock.NotReady;
        if (_up == LayerBlock.None && !_layerMaps.ContainsKey(_layer - 1)) _up = LayerBlock.NotReady;
        _hoverDown = _down is LayerBlock.None or LayerBlock.Cooldown ? _hoverDown + dt : 0f;
        _hoverUp = _up is LayerBlock.None or LayerBlock.Cooldown ? _hoverUp + dt : 0f;
        string wait = $"{Mathf.Ceil(_cubeDiving.CooldownLeft):0} s";
        _hud.LayerPrompt = _heldShown > 0f ? ("A current holds the way back", false, InputSetup.Dive)
            : _down switch
            {
                LayerBlock.None => ("dive", true, InputSetup.Dive),
                LayerBlock.Cooldown => ($"Dive in {wait}", false, InputSetup.Dive),
                LayerBlock.TooNear => ("Too near where you came down: swim on to dive again", false, InputSetup.Dive),
                _ => _up switch
                {
                    LayerBlock.None => ("climb", true, InputSetup.Climb),
                    LayerBlock.Cooldown => ($"Climb in {wait}", false, InputSetup.Climb),
                    _ => null,
                },
            };
        bool downKey = Input.IsActionPressed(InputSetup.Dive) || _autoDive && _down == LayerBlock.None;
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
        _layerLookFrom = LookOf(_map);
        _layerLookTo = LookOf(_layerMaps[LayerTarget]);
        _bell.Kick();
        _hud.Diving = true;
        _hud.LayerPrompt = null;
        _cubeView.ShowAbove(move == LayerMove.Up);
        _driftStreaks.Visible = false;
        foreach (var s in _wakeStreaks) s.Visible = false;
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
        bool ready = _stream.State(arrival) == CubeState.Active && _cubeView.Pending == 0;
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
        _cubeView.Follow(focus);
        float radius = t < 0.35f ? Mathf.Lerp(-3f, 6f, Ease(t / 0.35f)) : Mathf.Lerp(6f, 150f, Mathf.Pow(Ease((t - 0.35f) / 0.9f), 2f));
        var at = new Vector2(_layerFrom.X, _layerFrom.Y);
        _cubeView.SetPortal(at, radius, _layerMove == LayerMove.Up);
        // What lived on the layer she leaves goes with it, as on the game's dive.
        float stay = 1f - Ease((t - 0.3f) / 1.0f);
        FadeLevelLife(at, Mathf.Lerp(-5f, radius, Ease(t / 0.45f)), stay);
        _minimap.DiveOut(Ease(t / 0.9f));
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
        var terrain = _cubes!.Terrain(arrival);
        bool down = _layerMove == LayerMove.Down;
        int from = _layer;
        // The layer left: what is left of each claimed cube's stock, kept for her return.
        foreach (var record in Here.Claimed.Values) record.Left = _world.StockLeft(record.Cube);
        if (down) _cubeDiving.Dived(_layerFrom, terrain);
        else _cubeDiving.Climbed(_layerFrom, terrain);
        _layer = _cubeDiving.Layer;
        _records.Changed(from, _layer, _cubeDiving.Deepest);
        Here.Arrive(_layerFrom);
        _layerMove = LayerMove.None;
        _cubeView.SetLayer(_layer);
        _cubeView.ShowAbove(false);
        _cubeView.SetPortal(Vector2.Zero, -1f, false);
        _bell.DiveDepth = 0f;
        _bell.DiveTurn = 0f;
        _camera.Zoom = 1f;
        _camera.SetSwimLevel(0f);
        _hud.Diving = false;
        _hoverDown = _hoverUp = 0f;
        _id = CubeReef.IdOf(_layer);
        ShowLevel(LevelShape.Bare(_layerMaps[_layer]), keepRun: true, 0, _layerFrom);
        _minimap.Arrive();
        foreach (var view in LevelLife) ResetFade(view);
        GD.Print($"Cube reef: {(down ? "dived" : "climbed")} to layer {_layer} (deepest {_cubeDiving.Deepest})");
    }

    /// <summary>
    /// Verification (--autopilot): her way through a gate of her cube into one she has not claimed (across a shut edge
    /// never), 8 m past its line.
    /// </summary>
    List<System.Numerics.Vector2> CubeRoute()
    {
        var her = _world.Player.Position;
        var cube = CubeId.Of(her, _layer);
        foreach (var (edge, gate, _) in _cubes!.Terrain(cube).Gates.OrderBy(g => System.Numerics.Vector2.Distance(g.Mouth, her)))
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

    /// <summary>Each frame she swims: the reef drawn round her, an arch over her seen through, the currents' streaks.</summary>
    void SyncCubeView(float dt, Vector3 eye)
    {
        _cubeView.Follow(eye);
        var p = _world.Player.Position;
        bool hidden = _cubes!.Made(CubeId.Of(p, _layer))?.CanopyAt(Mathf.RoundToInt(p.X), Mathf.RoundToInt(p.Y)) == true;
        _cubeSeeThrough = Mathf.MoveToward(_cubeSeeThrough, hidden ? 1f : 0f, dt * (hidden ? 4.5f : 2.2f));
        _cubeView.SeeThrough(_bell.GlobalPosition, SeeThroughRadius, _cubeSeeThrough);
        DrawCubeCurrents(eye);
    }

    MeshInstance3D Streaks(Vector2 size)
    {
        var m = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = size },
            MaterialOverride = new ShaderMaterial { Shader = _currentShader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(m);
        return m;
    }

    /// <summary>The streaks where the currents run: the drift faint round her, the wake current plain at each shut throat of her cube.</summary>
    void DrawCubeCurrents(Vector3 her)
    {
        var p = _world.Player.Position;
        var drift = _cubeCurrents.DriftAt(p);
        _driftStreaks.Visible = drift != System.Numerics.Vector2.Zero;
        _driftStreaks.Position = new Vector3(her.X, LevelMap.SwimBand + 0.25f, her.Z);
        _driftStreaks.SetInstanceShaderParameter("flow", new Vector2(drift.X, drift.Y));
        _driftStreaks.SetInstanceShaderParameter("strength", 0.6f);
        var cube = CubeId.Of(p, _layer);
        int n = 0;
        foreach (var (edge, inside, _) in cube.Outer())
        {
            if (!Here.IsShut(edge)) continue;
            float into = edge.High == inside ? 1f : -1f;
            foreach (var gate in _cubes!.Space.Edge(edge).Gates)
            {
                if (n == _wakeStreaks.Count) _wakeStreaks.Add(Streaks(Vector2.One));
                var s = _wakeStreaks[n++];
                var at = edge.Point(gate.Along, into * CubeCurrents.WakeReach * 0.5f);
                bool acrossX = edge.Axis == EdgeAxis.X;
                ((PlaneMesh)s.Mesh).Size = acrossX ? new Vector2(CubeCurrents.WakeReach, gate.Width + 4f) : new Vector2(gate.Width + 4f, CubeCurrents.WakeReach);
                s.Position = new Vector3(at.X, LevelMap.SwimBand + 0.25f, at.Y);
                s.SetInstanceShaderParameter("flow", acrossX ? new Vector2(into * CubeCurrents.Wake, 0f) : new Vector2(0f, into * CubeCurrents.Wake));
                s.SetInstanceShaderParameter("strength", 1f);
                s.Visible = true;
            }
        }
        for (int i = n; i < _wakeStreaks.Count; i++) _wakeStreaks[i].Visible = false;
    }
}
