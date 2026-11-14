using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Controls;
using OctoShoots.Game.TopDown;

namespace OctoShoots.Game.Cubes;

/// <summary>
/// Diving and climbing between the cube world's layers (CUBE-WORLD-PLAN §7), the scene's side: the prompt, the keys
/// (down: Shift or the d-pad's down; up: Q or the d-pad's up), the layer under or over her readied as she lingers by a
/// way through or as the cooldown runs out, and the transition: the game's own dive (she gathers, turns head-first and
/// sinks a layer's drop in pulses; climbing, she rises), the camera drawing in to 60% of its distance and back out, her
/// layer opening like an iris round her (or the layer above opening into view as she rises into it). Should the cube
/// she arrives in not be ready, the middle of it loops (she keeps sinking through murk, her bell beating) until it is.
/// </summary>
public partial class CubeMain
{
    enum Move { None, Down, Up }

    /// <summary>The transition's length (the game's dive), the point it holds at while the cube she arrives in is not ready, and how close the camera draws in.</summary>
    const float MoveSeconds = 1.6f, MoveHold = 0.6f, MoveCloser = 0.4f;

    CubeDiving _diving = null!;
    Move _move;
    float _moveTime, _holdPulse, _hoverDown, _hoverUp, _debugTick;
    System.Numerics.Vector2 _moveFrom;
    bool _downLatch, _upLatch;
    Label _prompt = null!;
    LayerBlock _down = LayerBlock.NotReady, _up = LayerBlock.NotReady;
    /// <summary>For the readout: each change of layer (when, where to, how long it held for its cube), and the worst frame during them.</summary>
    readonly List<string> _moves = new();
    double _heldFor, _moveWorst;

    int MoveTarget => _layer + (_move == Move.Down ? 1 : -1);

    void BuildLayers(float cooldown)
    {
        _diving = new CubeDiving(_layers, cooldown);
        var layer = new CanvasLayer { Layer = 5 };
        AddChild(layer);
        _prompt = new Label { HorizontalAlignment = HorizontalAlignment.Center, AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f, OffsetLeft = -300f, OffsetRight = 300f, OffsetTop = -64f, OffsetBottom = -32f };
        _prompt.AddThemeFontSizeOverride("font_size", 20);
        _prompt.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _prompt.AddThemeConstantOverride("outline_size", 6);
        layer.AddChild(_prompt);
    }

    /// <summary>What the stream should want this frame: her layer, or during a transition the one she is going to.</summary>
    StreamFocus LayerFocus(PlaneBody p)
    {
        if (_move != Move.None)
            return new StreamFocus { Position = _moveFrom, Layer = MoveTarget, Climbable = _move == Move.Down };
        bool soon = _diving.CooldownLeft < 5f;
        return new StreamFocus
        {
            Position = p.Position, Velocity = p.Velocity, Layer = _layer, Climbable = _diving.Climbable, Shut = Here.IsShut,
            ReadyBelow = _hoverDown >= 0.5f || soon && _layer + 1 < _layers,
            ReadyAbove = _diving.Climbable && (_hoverUp >= 0.5f || soon),
        };
    }

    /// <summary>Each frame she swims: where she may go through, the prompt, and a key pressed there starts the transition.</summary>
    void StepLayers(float dt)
    {
        _diving.Tick(dt);
        var p = _world.Player.Position;
        _down = _diving.CanDive(p, _world.Radius, _stream.Terrain);
        _up = _diving.CanClimb(p, _world.Radius, _stream.Terrain);
        if (_autoDives && (_debugTick += dt) > 1f)
        {
            _debugTick = 0f;
            GD.Print($"Cube layers: layer {_layer}, deepest {_diving.Deepest}, down {_down}, up {_up}, cooldown {_diving.CooldownLeft:0.0}, plan {string.Join(",", _plan)}, goal {_goal}, at {p}");
        }
        _hoverDown = _down is LayerBlock.None or LayerBlock.Cooldown ? _hoverDown + dt : 0f;
        _hoverUp = _up is LayerBlock.None or LayerBlock.Cooldown ? _hoverUp + dt : 0f;
        _prompt.Text = _heldShown > 0f ? "A current holds the way back" : _down switch
        {
            LayerBlock.None => "Dive  [Shift / D-pad ↓]",
            LayerBlock.Cooldown => string.Format(CultureInfo.InvariantCulture, "Dive in {0:0} s", Mathf.Ceil(_diving.CooldownLeft)),
            LayerBlock.TooNear => "Too near where you came in: swim on to dive again",
            _ => _up switch
            {
                LayerBlock.None => "Climb  [Q / D-pad ↑]",
                LayerBlock.Cooldown => string.Format(CultureInfo.InvariantCulture, "Climb in {0:0} s", Mathf.Ceil(_diving.CooldownLeft)),
                _ => "",
            },
        };
        bool downKey = Input.IsActionPressed(InputSetup.Dive) || Input.IsJoyButtonPressed(0, JoyButton.DpadDown) || _down == LayerBlock.None && AutoPress(Move.Down);
        bool upKey = Input.IsKeyPressed(Key.Q) || Input.IsJoyButtonPressed(0, JoyButton.DpadUp) || _up == LayerBlock.None && AutoPress(Move.Up);
        if (downKey && !_downLatch && _down == LayerBlock.None) Begin(Move.Down);
        else if (upKey && !_upLatch && _up == LayerBlock.None) Begin(Move.Up);
        _downLatch = downKey;
        _upLatch = upKey;
    }

    void Begin(Move move)
    {
        _move = move;
        _moveTime = 0f;
        _holdPulse = 0f;
        _moveFrom = _world.Player.Position;
        _world.Player.Velocity = System.Numerics.Vector2.Zero;
        _world.Player.PrevPosition = _world.Player.Position;
        _bell.Kick();
        _prompt.Text = "";
        _view.ShowAbove(move == Move.Up);
        GD.Print(string.Format(CultureInfo.InvariantCulture, "Cube {0} begins at {1:0.0},{2:0.0} from layer {3}, {4:0.0} s in", move == Move.Down ? "dive" : "climb", _moveFrom.X, _moveFrom.Y, _layer, _sinceReady));
    }

    static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t, 0f, 1f));

    /// <summary>One frame of a dive or a climb.</summary>
    void StepMove(float dt)
    {
        var arrival = CubeId.Of(_moveFrom, MoveTarget);
        bool ready = _stream.State(arrival) == CubeState.Active && _view.Pending == 0;
        _moveTime += dt;
        if (!ready && _moveTime > MoveHold)
        {
            // Not ready: the middle of it loops, her bell beating as she sinks (or rises) on through the murk.
            _moveTime = MoveHold;
            _heldFor += dt;
            _holdPulse += dt;
            if (_holdPulse > 0.7f)
            {
                _holdPulse = 0f;
                _bell.Kick();
            }
        }
        float t = _moveTime, k = Mathf.Clamp(t / MoveSeconds, 0f, 1f);
        float sink = Ease((t - 0.25f) / 0.95f);
        sink = Mathf.Clamp(sink + 0.04f * Mathf.Sin(Mathf.Clamp((t - 0.25f) / 0.95f, 0f, 1f) * Mathf.Pi * 3f), 0f, 1f);
        float sign = _move == Move.Down ? 1f : -1f;
        float drop = sign * sink * CubeTuning.LayerDrop;
        float gather = Mathf.Sin(Mathf.Clamp(t / 0.25f, 0f, 1f) * Mathf.Pi) * 0.35f;
        _bell.DiveDepth = drop - sign * gather;
        _bell.DiveTurn = _move == Move.Down ? Ease((t - 0.2f) / 0.3f) * (1f - Ease((t - 1.1f) / 0.4f)) : 0f;
        _bell.Sync(_world, 1f, dt);
        SyncLight(dt);
        var focus = new Vector3(_moveFrom.X, LevelMap.SwimBand - drop, _moveFrom.Y);
        float pull = Mathf.Sin(Mathf.Pi * k);
        _camera.Zoom = 1f - MoveCloser * pull * pull;
        _camera.Track(focus, dt);
        _camera.SetSwimLevel(-drop);
        LookOf(_layer).SetDrop(drop);
        _view.Follow(focus);
        // The iris: out from where she goes through, from nothing to past the edge of the view.
        float radius = t < 0.35f ? Mathf.Lerp(-3f, 6f, Ease(t / 0.35f)) : Mathf.Lerp(6f, 150f, Mathf.Pow(Ease((t - 0.35f) / 0.9f), 2f));
        _view.SetPortal(new Vector2(_moveFrom.X, _moveFrom.Y), radius, _move == Move.Up);
        if (!Without("snow")) _snow.Tick(dt, focus + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        // Verification (--cube-shots with --capture=): pictures a third, half and two-thirds through each change of layer.
        if (_captureDir is not null && OS.GetCmdlineUserArgs().Contains("--cube-shots"))
            foreach (float at in new[] { 0.5f, 0.85f, 1.2f })
                if (_moveTime >= at && _moveTime - dt < at)
                {
                    string path = $"{_captureDir}/move{_moves.Count + 1}_{(int)(at * 100):000}.png";
                    GetViewport().GetTexture().GetImage().SavePng(path);
                    GD.Print($"Captured {path}");
                }
        if (_moveTime >= MoveSeconds && ready) Finish(arrival);
    }

    /// <summary>The transition over: she is on the layer she went to, drawn at the swim plane, the view just as it was.</summary>
    void Finish(CubeId arrival)
    {
        var terrain = _stream.Terrain(arrival)!;
        bool down = _move == Move.Down;
        int from = _layer;
        if (down) _diving.Dived(_moveFrom, terrain);
        else _diving.Climbed(_moveFrom, terrain);
        _layer = _diving.Layer;
        _records.Changed(from, _layer, _diving.Deepest);
        Here.Arrive(_moveFrom);
        _move = Move.None;
        _view.SetLayer(_layer);
        _view.ShowAbove(false);
        _view.SetPortal(Vector2.Zero, -1f, false);
        _field.Recenter(CellCoord.Of(_moveFrom, _layer));
        _fieldChanged = true;
        _bell.DiveDepth = 0f;
        _bell.DiveTurn = 0f;
        _camera.Zoom = 1f;
        _camera.SetSwimLevel(0f);
        _camera.Track(new Vector3(_moveFrom.X, LevelMap.SwimBand, _moveFrom.Y), 0f, snap: true);
        ApplyLook(LookOf(_layer));
        LookOf(_layer).SetDrop(0f);
        _hoverDown = _hoverUp = 0f;
        _moves.Add(string.Format(CultureInfo.InvariantCulture, "{0} to layer {1} at {2:0.0} s", down ? "down" : "up", _layer, _sinceReady));
        GD.Print($"Cube {(down ? "dive" : "climb")} arrives on layer {_layer}");
        if (_captureDir is not null && OS.GetCmdlineUserArgs().Contains("--cube-shots") && _plan.Count == 0) GetTree().Quit();
    }

    // ───────────────────────── verification ─────────────────────────

    /// <summary>
    /// Verification (--cube-dives): she dives twice and climbs once by herself (CUBE-WORLD-PLAN §14.2 step 8): to the
    /// nearest well she may dive through, waiting out the cooldown there; after the second dive, back up where she
    /// arrived. Her route over the water is worked out from the whole layer's terrain, made here for the purpose.
    /// </summary>
    bool _autoDives;
    readonly Queue<Move> _plan = new();
    System.Numerics.Vector2? _goal;

    void PlanDives()
    {
        _plan.Enqueue(Move.Down);
        _plan.Enqueue(Move.Down);
        _plan.Enqueue(Move.Up);
    }

    bool AutoPress(Move move) => _autoDives && _plan.Count > 0 && _plan.Peek() == move && _goal is { } g
        && System.Numerics.Vector2.Distance(_world.Player.Position, g) < 1.2f && _plan.Dequeue() == move && (_goal = null) is null;

    /// <summary>Her next goal (a well beyond the reach of where she arrived, or for a climb where she stands), and the way to it.</summary>
    /// <summary>The route being worked out (off the main thread: it makes the whole layer's terrain to find it).</summary>
    System.Threading.Tasks.Task<(List<Vector2>? Path, System.Numerics.Vector2? Goal)>? _planning;

    void AutoSteer()
    {
        if (!_autoDives || _move != Move.None || _plan.Count == 0) return;
        var her = _world.Player.Position;
        if (_plan.Peek() == Move.Up)
        {
            // Back up where she came down (the drift will have carried her off it while she waited).
            _goal ??= _diving.Arrived ?? her;
            _path.Clear();
            _path.Add(new Vector2(_goal.Value.X, _goal.Value.Y));
            _pathAt = 0;
            return;
        }
        if (_goal is not null) return;
        if (_planning is null)
        {
            int layer = _layer;
            var arrived = _diving.Arrived;
            float limit = _diving.ChainLimit, radius = _world.Radius;
            _planning = System.Threading.Tasks.Task.Run(() =>
            {
                var made = _space.CubesOf(layer).ToDictionary(c => c, c => CubeTerrain.Make(_space, c));
                var below = _space.CubesOf(layer + 1).ToDictionary(c => c, c => CubeTerrain.Make(_space, c));
                var wells = made.Values.SelectMany(t => t.Down)
                    .Where(w => arrived is not { } at || System.Numerics.Vector2.Distance(w.Center, at) >= limit)
                    .Where(w => below.GetValueOrDefault(CubeId.Of(w.Center, layer + 1)) is { } lower && CubeTerrain.CanDive(made[CubeId.Of(w.Center, layer)], lower, w.Center, radius))
                    .OrderBy(w => System.Numerics.Vector2.Distance(w.Center, her)).ToList();
                var all = new CubeField(c => made.GetValueOrDefault(c), new CellCoord(_footprint / 2, _footprint / 2, layer), _footprint);
                foreach (var w in wells)
                    if (PathOver(all, her, w.Center) is { } way) return ((List<Vector2>?)way, (System.Numerics.Vector2?)w.Center);
                return (null, null);
            });
            return;
        }
        if (!_planning.IsCompleted) return;
        var (path, goal) = _planning.Result;
        _planning = null;
        if (path is null) return;
        _path.Clear();
        _path.AddRange(path);
        _pathAt = 0;
        _goal = goal;
    }
}
