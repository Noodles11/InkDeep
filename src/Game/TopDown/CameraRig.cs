using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The top-down camera (DESIGN-TOPDOWN §3): tilted ~60°, narrow perspective (~35° FOV), fixed yaw with north always up,
/// smoothed follow, no rotation. About 30×22 m of the level fit on screen (§4.5).
/// </summary>
public partial class CameraRig : Node3D
{
    /// <summary>Downward tilt from the horizontal.</summary>
    public const float Tilt = 60f;
    public const float Fov = 35f;
    /// <summary>
    /// Distance from the followed point: ~36 m of ground top to bottom and ~54 m across at 16:9; at the players' zoom
    /// about 44 × 67 m, so a screen shows about 15% of a 140 m level (docs/DIVE.md §3).
    /// </summary>
    public const float Distance = 55f;
    const float Follow = 6f;
    /// <summary>The zoom: fixed for players at 0.8 (69 m); the debug menu alone moves it, from 0.5 (farther) to 2 (closer).</summary>
    public const float DefaultZoom = 0.8f, MinZoom = 0.5f, MaxZoom = 2f;
    /// <summary>Verification: `--dbg-zoom=metres` brings the camera closer (to inspect Clementine); it overrides the setting.</summary>
    readonly float? _dbgDistance = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--dbg-zoom=")) is { } zoom
        && float.TryParse(zoom["--dbg-zoom=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : null;
    float _distance = Distance / DefaultZoom;

    /// <summary>The camera-zoom setting (1 is the 55 m framing). The dive's Zoom multiplies it.</summary>
    public float ZoomSetting
    {
        set => _distance = _dbgDistance ?? Distance / Mathf.Clamp(value, MinZoom, MaxZoom);
    }

    public Camera3D Camera { get; private set; } = null!;
    /// <summary>The dive pulls the camera in (1 is the usual distance).</summary>
    public float Zoom { get; set; } = 1f;
    Vector3 _focus;
    ShaderMaterial _post = null!;
    bool _snapped;
    float _shake, _shakeTime;

    /// <summary>A jolt (a boss landing, a slam): the view shakes, easing out over about half a second.</summary>
    public void Shake(float amount)
    {
        if (ShakeEnabled) _shake = Mathf.Max(_shake, amount);
    }

    /// <summary>The swim level the depth focus is sharp at (the dive slides it down to the level below).</summary>
    public void SetSwimLevel(float y) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swim_level"), y);

    /// <summary>The absolute position of the world's origin (the god rays follow it).</summary>
    public void SetWorldOrigin(Vector2 origin) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("world_origin"), origin);

    /// <summary>Moves the camera with the world, keeping the view exactly as it was (her halo in the water too).</summary>
    public void Shift(Vector3 by)
    {
        _focus += by;
        Camera.GlobalPosition += by;
        _player += by;
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("player_pos"), _player);
    }

    /// <summary>The point her halo in the water is centred on (the last followed point).</summary>
    Vector3 _player;

    /// <summary>The reduced-motion setting: no refraction wobble in the water.</summary>
    /// <summary>Debug: the floor's fog and depth blur on (the game's look) or off (the floor seen clear).</summary>
    public void SetFloorFog(bool on) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("floor_fog"), on);

    /// <summary>
    /// The showcase reef (docs/VISUAL-SHOWCASE.md): crisp, as the references are — the deeps barely blurred, a thin
    /// veil of water over them; else the painted look's own soft depths.
    /// </summary>
    /// <summary>
    /// The game's look: how much fog lies under her plane (thickening over 5 m down), and what is left outside her light
    /// of the reef's colour and of its brightness.
    /// </summary>
    public const float BelowFog = 0.8f, UnlitSaturation = 0.3f, UnlitDim = 0.88f;

    public void SetShowcase(bool on)
    {
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("below_blur"), on ? 1.4f : 5.2f);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("above_blur"), on ? 1.6f : 3.4f);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("deep_fog"), on ? 0.025f : 0.06f);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("reef_water"), on ? 1.2f : 3.0f);
        // The game's look: thick fog under her plane, and the colour hers to bring (topdown_post's below_fog, unlit_*).
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("below_fog"), on ? BelowFog : 0f);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("unlit_saturation"), on ? UnlitSaturation : 1f);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("unlit_dim"), on ? UnlitDim : 1f);
    }

    public void SetReducedMotion(bool reduced) => _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), reduced ? 0f : 1f);

    /// <summary>Light bubbles: the halos her bubbles and their pop blooms cast on the swim layer (x, z, radius, strength).</summary>
    /// <summary>The clam rooms' glows on the water (xz, radius, strength), at most six.</summary>
    public void SetClamGlows(IReadOnlyList<Vector4> glows, IReadOnlyList<Color> colors)
    {
        int n = Mathf.Min(glows.Count, 6);
        // Sent flattened, as the bubble lights are; each glow in its pearl's colour.
        var flat = new float[24];
        var tint = new float[18];
        for (int i = 0; i < n && i < colors.Count; i++)
        {
            var l = colors[i].SrgbToLinear();
            tint[i * 3] = l.R;
            tint[i * 3 + 1] = l.G;
            tint[i * 3 + 2] = l.B;
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("clam_colors"), tint);
        for (int i = 0; i < n; i++)
        {
            flat[i * 4] = glows[i].X;
            flat[i * 4 + 1] = glows[i].Y;
            flat[i * 4 + 2] = glows[i].Z;
            flat[i * 4 + 3] = glows[i].W;
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("clam_glows"), flat);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("clam_count"), n);
    }

    public void SetBubbleLights(Vector4[] lights, int count)
    {
        // A vec4 array uniform, sent flattened (the form every renderer accepts).
        for (int i = 0; i < count && i < lights.Length; i++)
        {
            _lights[i * 4] = lights[i].X;
            _lights[i * 4 + 1] = lights[i].Y;
            _lights[i * 4 + 2] = lights[i].Z;
            _lights[i * 4 + 3] = lights[i].W;
        }
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("bubble_lights"), _lights);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("bubble_count"), Mathf.Min(count, lights.Length));
    }

    readonly float[] _lights = new float[CombatView.MaxLights * 4];

    /// <summary>A way down's opening, where the ground gives way to water (strength 0: none).</summary>
    public void SetHole(System.Numerics.Vector2 at, float radius, float strength) =>
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("hole"), new Vector4(at.X, at.Y, radius, strength));

    /// <summary>The leaks' hot air (AdventureView): where each is and how open.</summary>
    public void SetLeaks(Vector3[] leaks, int count)
    {
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("leaks"), leaks);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("leak_count"), count);
    }

    /// <summary>The currents' shimmer (CurrentView): the map of them over her plane, and each one's roll and strength.</summary>
    public void SetCurrents(Texture2D? map, Vector2[] flows, int count)
    {
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("current_map"), map);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("current_ids"), map);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("current_flows"), flows);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("current_count"), count);
    }

    /// <summary>The swells in the water round her (WaterSwells): where, how old and strong, and the way she was going.</summary>
    public void SetSwells(OctoShoots.Game.Fx.WaterSwells swells)
    {
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swells"), swells.Rings);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swell_dirs"), swells.Dirs);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("swell_count"), swells.Count);
    }

    /// <summary>The fish far under her plane (DeepShoals): where they are and how they swim.</summary>
    public void SetDeepFish(OctoShoots.Game.Fx.DeepShoals shoals)
    {
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("deep_fish"), shoals.Fish);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("deep_fish_ways"), shoals.Ways);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("deep_fish_count"), shoals.Count);
    }

    /// <summary>
    /// A blast's mark (topdown_post's blurs): a small round smear of the picture on her plane at this point, this wide,
    /// fading over BlurSeconds. At most four at once (the oldest gives way).
    /// </summary>
    public void AddBlur(System.Numerics.Vector2 at, float radius)
    {
        int i = _blurCount < MaxBlurs ? _blurCount++ : OldestBlur();
        _blurSpots[i] = new Vector4(at.X, at.Y, 0f, radius);
    }

    int OldestBlur()
    {
        int o = 0;
        for (int i = 1; i < _blurCount; i++)
            if (_blurSpots[i].Z > _blurSpots[o].Z) o = i;
        return o;
    }

    void TickBlurs(float dt)
    {
        for (int i = _blurCount - 1; i >= 0; i--)
        {
            _blurSpots[i].Z += dt / BlurSeconds;
            if (_blurSpots[i].Z >= 1f) _blurSpots[i] = _blurSpots[--_blurCount];
        }
        for (int i = 0; i < _blurCount; i++)
            for (int c = 0; c < 4; c++) _blurFlat[i * 4 + c] = _blurSpots[i][c];
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("blurs"), _blurFlat);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("blur_count"), _blurCount);
    }

    const int MaxBlurs = 4;
    const float BlurSeconds = 0.9f;
    readonly Vector4[] _blurSpots = new Vector4[MaxBlurs];
    readonly float[] _blurFlat = new float[MaxBlurs * 4];
    int _blurCount;

    /// <summary>A big-merge kill: the screen's edges lift toward white for 60 ms.</summary>
    public void EdgeFlash() => _edgeFlash = 1f;
    float _edgeFlash;

    /// <summary>Prism Pearl: the picture's colours pulled apart for a moment (chromatic aberration), easing back over AberrationSeconds.</summary>
    public void Aberrate(float strength = 1f) => _aberration = Mathf.Max(_aberration, strength);
    float _aberration;
    const float AberrationSeconds = 0.6f;
    const float EdgeFlashSeconds = 0.06f;

    /// <summary>The camera-shake setting.</summary>
    public bool ShakeEnabled { get; set; } = true;

    public override void _Ready()
    {
        Camera = new Camera3D { Fov = Fov, Near = 0.5f, Far = 200f, Current = true };
        AddChild(Camera);
        // The depth focus: the swim level sharp, the deeps blurred into fog, high rock blurred into silhouette (Below's look).
        _post = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topdown_post.gdshader"), RenderPriority = -100 };
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-lightview") >= 0) _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("debug_view"), 1);
        // (--dbg-inkview: the felt tip's lines alone, white on black.)
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dbg-inkview") >= 0) _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("debug_view"), 7);
        Camera.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = _post,
            ExtraCullMargin = 16384f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Verification: `--no-focus` shows the plain render.
            Visible = !OS.GetCmdlineUserArgs().Contains("--no-focus"),
        });
    }

    /// <summary>
    /// Follow a point on the swim band; snap jumps straight there (level start, teleports). The camera centres look
    /// metres past it (the level-up frames her at the screen's left); her halo stays on the point itself.
    /// </summary>
    public void Track(Vector3 focus, float dt, bool snap = false, Vector3 look = default)
    {
        TickBlurs(dt);
        var aim = focus + look;
        if (snap || !_snapped)
        {
            _focus = aim;
            _snapped = true;
        }
        else
        {
            _focus = _focus.Lerp(aim, 1f - Mathf.Exp(-Follow * dt));
        }
        float tilt = Mathf.DegToRad(Tilt);
        // North is −Z: the camera sits south of the focus, looking north and down.
        Vector3 offset = new Vector3(0f, Mathf.Sin(tilt), Mathf.Cos(tilt)) * _distance * Zoom;
        _shakeTime += dt;
        _shake = Mathf.MoveToward(_shake, 0f, dt * 1.6f);
        Vector3 jolt = _shake > 0f ? new Vector3(Mathf.Sin(_shakeTime * 53f), Mathf.Sin(_shakeTime * 61f + 1f), Mathf.Sin(_shakeTime * 47f + 2f)) * _shake * _shake : Vector3.Zero;
        Camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(-tilt, 0f, 0f)), _focus + offset + jolt);
        _player = focus;
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("player_pos"), focus);
        _edgeFlash = Mathf.MoveToward(_edgeFlash, 0f, dt / EdgeFlashSeconds);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("edge_flash"), _edgeFlash);
        _aberration = Mathf.MoveToward(_aberration, 0f, dt / AberrationSeconds);
        _post.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("aberration"), _aberration * _aberration);
    }
}
