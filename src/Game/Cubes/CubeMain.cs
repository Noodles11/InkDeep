using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Settings;
using OctoShoots.Game.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Game.Cubes;

/// <summary>
/// The cube world's prototype (docs/proposals/CUBE-WORLD-PLAN.md §14), opened by `--cubes`: Clementine, her light and
/// the camera in one continuous reef streamed in cubes (ReefStream, ReefStreamView). No creatures, pearls or places.
/// She is a reef world's own (CubeShell: a blank map with nothing in it but her) given the cubes' rock to swim and shine
/// against, so every view of her is the game's.
/// Flags: --seed=, --cube-footprint=N (3), --cube-layers=N (3), --cube-route (she swims a ring of cubes by herself),
/// --cube-perf=seconds (then the frame costs are printed and it quits), --capture= and --frames= as the game's.
/// </summary>
public partial class CubeMain : Node3D
{
    SeedCode _seed = SeedCode.Parse("KELP7Q2Z");
    int _footprint = 5, _layers = 3;
    bool _route;
    float _perfFor;
    string? _captureDir;
    readonly Queue<int> _captureFrames = new();

    CubeSpace _space = null!;
    ReefStream _stream = null!;
    TaskCubeWorker _worker = null!;
    CubeField _field = null!;
    PlaneWorld _world = null!;
    Tuning _tuning = null!;
    int _layer;

    ReefStreamView _view = null!;
    BellView _bell = null!;
    BeamView _beam = null!;
    CameraRig _camera = null!;
    SunLight _sun = null!;
    MarineSnow _snow = null!;
    WorldEnvironment _environment = null!;
    readonly HorizonMap _lantern = new();
    Label _overlay = null!;

    double _accumulator;
    int _frame;
    bool _ready;
    float _beamShown, _seeThrough;
    bool _dashLatched, _fieldChanged = true;

    // The frame costs, once she is swimming (the first frames make the first cubes).
    int _perfFrames, _perfOver;
    double _perfWorst, _perfTotal, _perfClock, _cpuMs, _cpuTotal, _gpuTotal, _renderCpuTotal, _allProcessTotal;
    readonly List<Vector2> _path = new();
    int _pathAt;

    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            string Value(string prefix) => arg[prefix.Length..];
            if (arg.StartsWith("--seed=") && SeedCode.TryParse(Value("--seed="), out var seed)) _seed = seed;
            else if (arg.StartsWith("--cube-footprint=") && int.TryParse(Value("--cube-footprint="), out int f)) _footprint = Math.Clamp(f, 1, Footprint);
            else if (arg.StartsWith("--cube-layers=") && int.TryParse(Value("--cube-layers="), out int l)) _layers = Math.Clamp(l, 1, Layers);
            else if (arg == "--cube-route") _route = true;
            else if (arg.StartsWith("--cube-perf=") && float.TryParse(Value("--cube-perf="), NumberStyles.Float, CultureInfo.InvariantCulture, out float p)) _perfFor = p;
            else if (arg.StartsWith("--capture=")) _captureDir = Value("--capture=");
            else if (arg.StartsWith("--frames="))
                foreach (string f2 in Value("--frames=").Split(','))
                    if (int.TryParse(f2, out int n)) _captureFrames.Enqueue(n);
        }
        InputSetup.Register();
        _tuning = SettingsStore.LoadTuning();
        var options = SettingsStore.LoadView();
        Engine.MaxFps = _captureDir is null && _perfFor <= 0f ? options.MaxFps : 0;
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        // (Measuring: unpaced by the screen, so a window behind another, which Windows paces down, still shows its own cost.)
        if (_perfFor > 0f) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        ViewOptions.Shadows = options.LightShadows;
        ReefLook.Brightness = Mathf.Clamp(options.Brightness, 0.6f, 1.8f);

        // The reef, and where she starts: a gate's mouth of its first cube.
        _space = new CubeSpace(_seed.Value, _footprint, _layers);
        // (The reef's cube nearest its north-west corner.)
        var first = _space.CubesOf(0).OrderBy(c => c.Middle.Length()).First();
        var start = CubeTerrain.Make(_space, first).Gates[0].Mouth;
        _worker = new TaskCubeWorker(_space, Build);
        _stream = new ReefStream(_space, _worker);
        _worker.Stream = _stream;
        _field = new CubeField(_stream.Terrain, CellCoord.Of(start, 0));
        _world = CubeShell.Make(_footprint, start, _tuning);
        _world.Field = _field;

        var look = LookOf(0);
        _environment = TopDownMain.MakeEnvironment(look);
        AddChild(_environment);
        _view = new ReefStreamView();
        AddChild(_view);
        _bell = new BellView();
        AddChild(_bell);
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_horizon"), _lantern.Texture);
        _beam = new BeamView();
        AddChild(_beam);
        _camera = new CameraRig();
        AddChild(_camera);
        _snow = new MarineSnow(900, new Vector3(22f, 7f, 17f), 0.11f);
        AddChild(_snow);
        _sun = new SunLight();
        AddChild(_sun);
        _sun.SetStrength(options.SunLight);
        if (!Without("fisheye")) AddChild(new Fisheye());
        var layer = new CanvasLayer { Layer = 5 };
        AddChild(layer);
        _overlay = new Label { Position = new Vector2(12f, 12f), Visible = _captureDir is null || OS.GetCmdlineUserArgs().Contains("--f3") };
        _overlay.AddThemeFontSizeOverride("font_size", 13);
        layer.AddChild(_overlay);
        ApplyLook(look);
        if (_route) PlanRoute(start);
        if (OS.GetCmdlineUserArgs().Contains("--cube-nodes")) GetTree().CreateTimer(2.0).Timeout += ListProcessing;
        if (OS.GetCmdlineUserArgs().Contains("--cube-nomusic") && GetTree().Root.GetNodeOrNull("MusicManager") is { } music) music.SetProcess(false);
    }

    /// <summary>Verification (--cube-without=bell,beam,…): parts of the scene left out, to find what a frame costs.</summary>
    static bool Without(string part) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--cube-without=")) is { } w && w["--cube-without=".Length..].Split(',').Contains(part);

    /// <summary>Verification (--cube-nodes): every node that runs a step of its own each frame.</summary>
    void ListProcessing()
    {
        void Walk(Node n)
        {
            if (n.IsProcessing() || n.IsPhysicsProcessing() || n.IsProcessingInternal() || n.IsPhysicsProcessingInternal()) GD.Print($"Processing: {n.GetPath()} ({n.GetType().Name}{(n.IsProcessingInternal() ? ", internal" : "")})");
            foreach (var c in n.GetChildren()) Walk(c);
        }
        Walk(GetTree().Root);
    }

    /// <summary>The stream's work off the main thread that is the game's: a cube's meshes (nothing is stocked yet).</summary>
    static object? Build(CubeJob job, CubeTerrain terrain) => job.Work switch
    {
        CubeWork.Mesh => CubeShapes.Ground(terrain),
        CubeWork.PreviewMesh => CubeShapes.Preview(terrain),
        _ => "nothing",
    };

    /// <summary>A layer's look: the shallows' for the warm-up layer, then a depth's look every few layers.</summary>
    static ReefLook LookOf(int layer) => ReefLook.For(1 + Math.Min(layer, 3));

    void ApplyLook(ReefLook look)
    {
        ArtLook.Set(ArtLook.Painted);
        look.Apply();
        look.ApplyTo(_environment.Environment);
        ReefLook.SetExtent(_footprint * CubeTuning.Side);
        RenderingServer.GlobalShaderParameterSet(SN.Of("sun_has_shadow"), 0f);
        _sun.Configure(look.Sun, look.SunEnergy);
        _bell.Lantern = look.Lantern;
        _camera.SetShowcase(true);
        TopDownMain.ShowcaseOn = true;
        _camera.SetFloorFog(true);
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_showcase"), 1f);
        // The warm-up layer is awake (bright); below it the reef is dark until her light shows it.
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_wake"), _layer == 0 ? 1f : 0f);
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_wave"), new Vector4(0f, 0f, 0f, 0f));
        var white = HerColor.Light.Lerp(new Color(0.92f, 0.96f, 1f), 0.7f);
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_show_light"), new Vector3(white.R, white.G, white.B));
    }

    public override void _Process(double delta)
    {
        long processStart = System.Diagnostics.Stopwatch.GetTimestamp();
        float dt = (float)delta;
        _frame++;
        var p = _world.Player;

        // The stream: what is wanted round her, finished work taken in, a few meshes uploaded.
        var focus = new StreamFocus { Position = p.Position, Velocity = p.Velocity, Layer = _layer };
        _stream.Update(focus, dt);
        _worker.Drain(_stream);
        while (_worker.Errors.TryDequeue(out var error))
            if (_errorsShown++ < 5) GD.PushError($"Cube work failed: {error}");
        _view.Take(_stream);
        _view.Apply();
        _field.Recenter(CellCoord.Of(p.Position, _layer));
        if (_field.Refresh()) _fieldChanged = true;

        // She waits for her own cube and what she can swim into (the first frames only).
        // (Every cube wanted at its state: her cube, what she sees, the layer below.)
        if (!_ready) _ready = _stream.InFlight.Count == 0 && _view.Pending == 0 && _stream.Wanted.All(w => _stream.State(w.Key) == w.Value);
        _accumulator += delta;
        if (!_ready) _accumulator = 0;
        int steps = 0;
        while (_accumulator >= PlaneWorld.Dt && steps < 8)
        {
            _world.Step(ReadInput());
            _field.Recenter(CellCoord.Of(p.Position, _layer));
            _accumulator -= PlaneWorld.Dt;
            steps++;
        }
        float alpha = (float)(_accumulator / PlaneWorld.Dt);

        var at = System.Numerics.Vector2.Lerp(p.PrevPosition, p.Position, alpha);
        var eye = new Vector3(at.X, LevelMap.SwimBand, at.Y);
        if (!Without("bell")) _bell.Sync(_world, alpha, dt);
        if (!Without("light")) SyncLight(dt);
        if (!Without("beam")) _beam.Sync(_world, _bell.LightPosition, dt);
        _camera.Track(eye, dt, snap: _frame <= 2);
        // Under an arch she is seen through it.
        bool hidden = _stream.Terrain(CubeId.Of(p.Position, _layer))?.CanopyAt(Mathf.RoundToInt(p.Position.X), Mathf.RoundToInt(p.Position.Y)) == true;
        _seeThrough = Mathf.MoveToward(_seeThrough, hidden ? 1f : 0f, dt * (hidden ? 4.5f : 2.2f));
        _view.SeeThrough(_bell.GlobalPosition, 4.5f, _seeThrough);
        _view.Follow(eye);
        if (!Without("snow"))
        {
            _snow.Her(_bell.GlobalPosition, new Vector3(p.Velocity.X, 0f, p.Velocity.Y));
            _snow.Tick(dt, eye + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        }

        _cpuMs = (System.Diagnostics.Stopwatch.GetTimestamp() - processStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Measure(delta);
        if (_overlay.Visible && _frame % 6 == 0) _overlay.Text = Overlay();
        Capture();
        _tailMs = (System.Diagnostics.Stopwatch.GetTimestamp() - processStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency - _cpuMs;
    }

    double _tailMs, _lateFor, _lateMost;
    string _worstAt = "";
    double _sinceReady;
    int _lateFrames;
    int _errorsShown;

    /// <summary>Her light in the water and on the reef (light_field, reef_show), as the game sets it.</summary>
    void SyncLight(float dt)
    {
        var at = _bell.LightPosition;
        float range = _bell.LightRange;
        _lantern.Update(_field, at, range, _fieldChanged);
        _fieldChanged = false;
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_light_at"), new Vector4(at.X, at.Y, at.Z, range));
        var aura = _world.Aura;
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_aura"), aura.Shining ? new Vector4(aura.Position.X, LevelMap.SwimBand, aura.Position.Y, aura.Range) : new Vector4(0f, -1000f, 0f, 0f));
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_show_aura"), aura.Shining ? new Vector4(aura.Position.X, LevelMap.SwimBand, aura.Position.Y, aura.Range * 1.6f) : new Vector4(0f, -1000f, 0f, 0f));
        var beam = _world.Beam;
        _beamShown = Mathf.MoveToward(_beamShown, beam.Shining ? 1f : 0f, dt * (beam.Shining ? 8f : 4f));
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_beam_show"), _beamShown > 0f
            ? new Vector4(beam.Position.X, LevelMap.SwimBand, beam.Position.Y, System.Numerics.Vector2.Distance(beam.Position, _world.BeamEnd) + 1.5f) : new Vector4(0f, -1000f, 0f, 0f));
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_show_dir"), new Vector4(_world.BeamAim.X, _beamShown, _world.BeamAim.Y, Mathf.Max(_world.BeamSpread, beam.HalfAngle)));
        float murk = 0.5f + LookOf(_layer).Fog;
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_scatter"), 0.016f * _bell.LightEnergy / Mathf.Pow(_bell.Lantern, 1.3f) * murk);
    }

    PlaneInput ReadInput()
    {
        var input = new PlaneInput();
        var pos = _world.Player.Position;
        if (_route && _path.Count > 0)
        {
            while (_pathAt + 1 < _path.Count && System.Numerics.Vector2.Distance(pos, ToCore(_path[_pathAt])) < 2.5f) _pathAt++;
            var to = ToCore(_path[_pathAt]) - pos;
            if (to.Length() > 0.3f) input.Move = System.Numerics.Vector2.Normalize(to);
            input.Aim = input.Move;
            return input;
        }
        Vector2 stick = Input.GetVector(InputSetup.Left, InputSetup.Right, InputSetup.Forward, InputSetup.Back);
        input.Move = new System.Numerics.Vector2(stick.X, stick.Y);
        bool dash = Input.IsActionPressed(InputSetup.Dash);
        input.Dash = dash && !_dashLatched;
        input.DashHeld = dash;
        _dashLatched = dash;
        bool shine = Input.IsActionPressed(InputSetup.Beam);
        Vector2 aim = Input.GetVector(InputSetup.AimLeft, InputSetup.AimRight, InputSetup.AimUp, InputSetup.AimDown);
        if (aim.LengthSquared() > 0.04f)
        {
            input.Aim = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(aim.X, aim.Y));
            input.Beam = !InputSetup.UsingPad || shine;
        }
        else if (InputSetup.UsingPad)
        {
            input.Aim = stick.LengthSquared() > 0.04f ? System.Numerics.Vector2.Normalize(input.Move) : _world.Player.Aim;
            input.Beam = shine;
        }
        else
        {
            var to = MouseOnBand() - pos;
            input.Aim = to.LengthSquared() > 1e-4f ? System.Numerics.Vector2.Normalize(to) : default;
            input.Beam = shine;
        }
        return input;
    }

    static System.Numerics.Vector2 ToCore(Vector2 v) => new(v.X, v.Y);

    public override void _Input(InputEvent e)
    {
        bool pad = e switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion m => Mathf.Abs(m.AxisValue) > 0.4f || InputSetup.UsingPad,
            InputEventKey or InputEventMouseButton => false,
            _ => InputSetup.UsingPad,
        };
        InputSetup.UsingPad = pad;
        if (e is InputEventKey { Pressed: true, Keycode: Key.F3 }) _overlay.Visible = !_overlay.Visible;
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) GetTree().Quit();
    }

    System.Numerics.Vector2 MouseOnBand()
    {
        var cam = _camera.Camera;
        Vector2 mouse = Fisheye.Source(GetViewport().GetMousePosition(), GetViewport().GetVisibleRect().Size);
        Vector3 origin = cam.ProjectRayOrigin(mouse), dir = cam.ProjectRayNormal(mouse);
        if (Mathf.Abs(dir.Y) < 1e-4f) return _world.Player.Position;
        Vector3 hit = origin + dir * ((LevelMap.SwimBand - origin.Y) / dir.Y);
        return new System.Numerics.Vector2(hit.X, hit.Z);
    }

    /// <summary>
    /// Verification (--cube-route): a way round a ring of four cubes and back to where she began, over the water (worked
    /// out here from the whole layer's terrain, which the game itself never holds at once).
    /// </summary>
    void PlanRoute(System.Numerics.Vector2 start)
    {
        var made = _space.CubesOf(0).ToDictionary(c => c, c => CubeTerrain.Make(_space, c));
        var all = new CubeField(c => made.GetValueOrDefault(c), new CellCoord(_footprint / 2, _footprint / 2, 0), _footprint);
        var stops = new List<System.Numerics.Vector2> { start };
        // A ring: round the cubes nearest the middle of the reef, in turn about it, and back.
        var middle = new System.Numerics.Vector2(_footprint * CubeTuning.Side * 0.5f);
        foreach (var t in made.Values.OrderBy(t => System.Numerics.Vector2.Distance(t.Cube.Middle, middle)).Take(4)
                     .OrderBy(t => MathF.Atan2(t.Cube.Middle.Y - middle.Y, t.Cube.Middle.X - middle.X)))
            stops.Add(t.Gates[0].Mouth);
        stops.Add(start);
        int n = _footprint * CubeTuning.Side + 1;
        float clearance = _tuning.PlayerRadius + 0.3f;
        for (int s = 0; s + 1 < stops.Count; s++)
        {
            var a = (X: Mathf.RoundToInt(stops[s].X), Y: Mathf.RoundToInt(stops[s].Y));
            var b = (X: Mathf.RoundToInt(stops[s + 1].X), Y: Mathf.RoundToInt(stops[s + 1].Y));
            var came = new Dictionary<(int, int), (int, int)> { [a] = a };
            var todo = new Queue<(int X, int Y)>();
            todo.Enqueue(a);
            bool Free(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && ReefCollision.Clear(all, new System.Numerics.Vector2(x, y), clearance);
            while (todo.Count > 0)
            {
                var c = todo.Dequeue();
                if (c == b) break;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var next = (X: c.X + dx, Y: c.Y + dy);
                    if ((dx == 0 && dy == 0) || came.ContainsKey(next) || !Free(next.X, next.Y)) continue;
                    if (dx != 0 && dy != 0 && (!Free(c.X + dx, c.Y) || !Free(c.X, c.Y + dy))) continue;
                    came[next] = c;
                    todo.Enqueue(next);
                }
            }
            if (!came.ContainsKey(b)) continue;
            var leg = new List<Vector2>();
            for (var c = b; c != a; c = came[c]) leg.Add(new Vector2(c.Item1, c.Item2));
            leg.Reverse();
            _path.AddRange(leg);
        }
        GD.Print($"Cube route: {_path.Count} m over {stops.Count - 1} legs");
    }

    void Measure(double delta)
    {
        // (From half a second after the first whole view: its first frame compiles the shaders.)
        if (!_ready) return;
        _sinceReady += delta;
        if (_sinceReady < 0.5) return;
        double ms = delta * 1000.0;
        _perfFrames++;
        _perfTotal += ms;
        _perfClock += delta;
        if (ms > _perfWorst)
        {
            _perfWorst = ms;
            _worstAt = $"{_perfClock:0.0} s in, at {_world.Player.Position.X:0},{_world.Player.Position.Y:0}";
        }
        if (ms > 20.0) _perfOver++;
        // A cube in her view on her layer not drawn yet: late.
        var cell = CellCoord.Of(_world.Player.Position, _layer);
        bool late = false;
        for (int dy = -1; dy <= 1 && !late; dy++)
        for (int dx = -1; dx <= 1 && !late; dx++)
        {
            var c = CubeId.Of(new CellCoord(cell.X + dx, cell.Y + dy, _layer));
            // (Only what the camera can see: a cube within 40 m of her.)
            var her = _world.Player.Position;
            var nearest = System.Numerics.Vector2.Clamp(her, c.Origin, c.Origin + c.Size);
            if (System.Numerics.Vector2.Distance(her, nearest) > 40f) continue;
            if (_space.Inside(c) && !_stream.Has(c, CubeWork.Mesh)) late = true;
            // (And under it: the layer below, seen through the void.)
            var under = c.OnLayer(_layer + 1);
            if (_space.Inside(under) && !_stream.Has(under, CubeWork.PreviewMesh) && !_stream.Has(under, CubeWork.Mesh)) late = true;
        }
        _lateFor = late ? _lateFor + delta : 0;
        if (late) _lateFrames++;
        _lateMost = Math.Max(_lateMost, _lateFor);
        _cpuTotal += _cpuMs;
        _gpuTotal += RenderingServer.ViewportGetMeasuredRenderTimeGpu(GetViewport().GetViewportRid());
        _renderCpuTotal += RenderingServer.ViewportGetMeasuredRenderTimeCpu(GetViewport().GetViewportRid());
        _allProcessTotal += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        if (_perfFor <= 0f || _perfClock < _perfFor) return;
        var p = _world.Player.Position;
        GD.Print(string.Format(CultureInfo.InvariantCulture,
            "Cube perf: {0} frames in {1:0.0} s, mean {2:0.00} ms (our frame code {12:0.00} ms + {16:0.00} ms after it, every node's process {14:0.00} ms, render CPU {15:0.00} ms, GPU {13:0.00} ms), worst {3:0.0} ms, over 20 ms: {4}; worst upload {5:0.00} ms; window moves {6}; at {7:0},{8:0} cube {9}; nodes {10} in use, {11} pooled",
            _perfFrames, _perfClock, _perfTotal / _perfFrames, _perfWorst, _perfOver, _view.WorstMs, _field.Recentres, p.X, p.Y, CubeId.Of(p, _layer), _view.InUse, _view.Pooled,
            _cpuTotal / _perfFrames, _gpuTotal / _perfFrames, _allProcessTotal / _perfFrames, _renderCpuTotal / _perfFrames, _tailMs));
        GD.Print(string.Format(CultureInfo.InvariantCulture, "Cube perf: the worst frame {3}; a cube in view undrawn on {0} frames, {1:0.00} s at most; jobs: {2}", _lateFrames, _lateMost,
            string.Join(", ", _worker.Timing.Select(t => $"{t.Key} {t.Value.Count} × {t.Value.Total / t.Value.Count:0.0} ms (most {t.Value.Most:0.0})")), _worstAt));
        GetTree().Quit();
    }

    string Overlay()
    {
        var p = _world.Player.Position;
        var here = CellCoord.Of(p, _layer);
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{Engine.GetFramesPerSecond():0} fps   worst {_perfWorst:0.0} ms   over 20 ms: {_perfOver}\n");
        sb.Append(CultureInfo.InvariantCulture, $"cube {here}   at {p.X:0.0}, {p.Y:0.0}   window moves {_field.Recentres}\n");
        sb.Append(CultureInfo.InvariantCulture, $"jobs out {_stream.InFlight.Count}   uploads waiting {_view.Pending}   last {_view.LastMs:0.00} ms   worst {_view.WorstMs:0.00} ms\n");
        sb.Append(CultureInfo.InvariantCulture, $"nodes {_view.InUse} in use, {_view.Pooled} pooled   cubes held {_stream.Held.Count()}\n");
        foreach (int z in new[] { _layer, _layer + 1 })
        {
            sb.Append(z == _layer ? "her layer:\n" : "below:\n");
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var c = CubeId.Of(new CellCoord(here.X + dx, here.Y + dy, z));
                    string s = !_space.Inside(c) ? "·" : _stream.State(c) switch
                    {
                        CubeState.Active => "A", CubeState.Preloaded => "P", CubeState.Preview => "v", CubeState.Below => "b", CubeState.Mask => "m", _ => "-",
                    };
                    sb.Append(s).Append(_stream.PinnedBy(c).Count > 0 ? "* " : "  ");
                }
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    void Capture()
    {
        if (_captureDir is null || _captureFrames.Count == 0 || _frame < _captureFrames.Peek()) return;
        int frame = _captureFrames.Dequeue();
        string path = $"{_captureDir}/cube_{frame:0000}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        var p = _world.Player.Position;
        GD.Print(string.Format(CultureInfo.InvariantCulture, "Captured {0}  pos {1:0.0},{2:0.0}  cube {3}  {4:0} FPS", path, p.X, p.Y, CubeId.Of(p, _layer), Engine.GetFramesPerSecond()));
        if (_captureFrames.Count == 0) GetTree().Quit();
    }
}
