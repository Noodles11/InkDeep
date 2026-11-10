using Godot;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her light going out (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.1; the Theme Bible's freed, not killed): when her light is
/// spent, the reef holds still round her while her beams fold away and her aura draws in; her bell goes out like a
/// lamp; then what light she had leaves her, motes of it rising off her as her bell fades to nothing. Then the dive's
/// summary (BeginDeath).
/// </summary>
public partial class TopDownMain
{
    /// <summary>Her light going out (and how long since it began), and whether its motes have left her yet.</summary>
    bool _dying, _released;
    float _dyingTime;
    /// <summary>How much of her aura's reach she still shows (1, but while her light goes out).</summary>
    float _auraShown = 1f;
    LightMotes _motes = null!;
    /// <summary>Verification (--dbg-die=seconds): that long onto the first reef, her light is spent.</summary>
    float _dbgDie;

    /// <summary>Her bell goes out over DimSeconds; its light leaves her at ReleaseAt, the bell fading over BellFadeSeconds; the summary at DyingSeconds.</summary>
    const float DimSeconds = 1.4f, ReleaseAt = 1.1f, BellFadeSeconds = 0.9f, DyingSeconds = 2.8f;

    void BeginDying()
    {
        _dying = true;
        _released = false;
        _dyingTime = 0f;
        _accumulator = 0;
        _hud.Diving = true;
        // Her beams fold away (the sim holds still from here).
        foreach (var (beam, _) in _world.HerBeams) beam.Active = false;
        if (_captureDir is not null) GD.Print($"Her light goes out at frame {_frame}");
    }

    /// <summary>One frame of her light going out; the summary once it has.</summary>
    void StepDying(float dt)
    {
        _dyingTime += dt;
        float t = _dyingTime;
        // Out like a lamp: her glow, her light on the reef, her aura drawing in.
        float dim = Ease(t / DimSeconds);
        _bell.Lit = 1f - dim;
        _auraShown = 1f - dim;
        // What light she had leaves her: motes rising off her as her bell fades away.
        if (!_released && t >= ReleaseAt)
        {
            _released = true;
            _motes.Release(_bell.LightPosition, OctoShoots.Game.Fx.HerColor.Light);
            _sfx.PlayPitched("dash", 0.45f, -10f);
        }
        float fade = Ease((t - ReleaseAt) / BellFadeSeconds);
        _bell.Scale = Vector3.One * Mathf.Max(0.001f, 1f - 0.7f * fade);
        _bell.DiveDepth = -0.8f * fade;
        _bell.Visible = fade < 0.99f;
        _bell.Sync(_world, 1f, dt);
        SyncLantern();
        _beam.Sync(_world, _bell.LightPosition, dt);
        // The camera closes in a little on where she was.
        _camera.Zoom = 1f - 0.12f * Ease(t / DyingSeconds);
        _camera.Track(Focus(1f), dt);
        _snow.Tick(dt, Focus(1f) + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        _plankton.Tick(dt, Focus(1f) + Vector3.Up * 2f, _camera.Camera.GlobalBasis);
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        if (t < DyingSeconds) return;
        _dying = false;
        BeginDeath();
    }

    /// <summary>Her bell as it was before her light went out (a new run, under the splash).</summary>
    void RelightBell()
    {
        _bell.Lit = 1f;
        _bell.Scale = Vector3.One;
        _bell.DiveDepth = 0f;
        _bell.Visible = true;
        _auraShown = 1f;
        _camera.Zoom = 1f;
        _motes.Clear();
    }
}

/// <summary>Motes of her light leaving her (her light going out): a soft burst rising off her and fading.</summary>
public partial class LightMotes : Node3D
{
    CpuParticles3D? _motes;

    public void Release(Vector3 at, Color colour)
    {
        Clear();
        var quad = new QuadMesh { Size = new Vector2(0.22f, 0.22f) };
        quad.Material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true, AlbedoTexture = Soft(),
        };
        var fade = new Gradient();
        fade.SetColor(0, new Color(colour.Lightened(0.4f), 0f));
        fade.AddPoint(0.15f, new Color(colour.Lightened(0.3f), 1f));
        fade.SetColor(fade.GetPointCount() - 1, new Color(colour, 0f));
        _motes = new CpuParticles3D
        {
            Mesh = quad, Amount = 70, Lifetime = 2.2f, OneShot = true, Explosiveness = 0.85f, Emitting = true,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.6f,
            Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = 0.6f, InitialVelocityMax = 2.2f,
            Gravity = new Vector3(0f, 0.9f, 0f), DampingMin = 0.6f, DampingMax = 1.2f,
            ScaleAmountMin = 0.5f, ScaleAmountMax = 1.4f, ColorRamp = fade, Position = at,
        };
        AddChild(_motes);
        var glow = new OmniLight3D { LightColor = colour, LightEnergy = 1.6f, OmniRange = 6f, ShadowEnabled = false, Position = at };
        AddChild(glow);
        var tween = CreateTween();
        tween.TweenProperty(glow, "light_energy", 0f, 1.6f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
    }

    public void Clear()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _motes = null;
    }

    /// <summary>A round soft dot.</summary>
    static GradientTexture2D Soft()
    {
        var g = new Gradient();
        g.SetColor(0, Colors.White);
        g.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 32, Height = 32 };
    }
}
