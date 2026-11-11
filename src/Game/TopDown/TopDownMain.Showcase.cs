using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The game's look (docs/VISUAL-SHOWCASE.md), first built on the showcase reef: her light shows the reef as it is
/// (reef_show.gdshaderinc), and the reef wakes as its leaks are sealed, a wave of her light running out across it from
/// each. `--showcase=0` takes it off (the drawn look).
/// </summary>
public partial class TopDownMain
{
    /// <summary>A wave's front runs out at WaveSpeed m/s, and is done past WaveEnd m; the reef settles awake over WakeEase seconds.</summary>
    const float WaveSpeed = 30f, WaveEnd = 200f;

    /// <summary>Verification (--showcase, --showcase=0): the showcase on or off wherever she is.</summary>
    bool? _dbgShowcase;
    bool _showcase;
    /// <summary>The art look off the showcase (the game's, or --look=N's).</summary>
    int _artLook = OctoShoots.Game.Fx.ArtLook.Drawn;
    /// <summary>How far her beam has eased on (0..1), for the reef it shows.</summary>
    float _beamShown;

    /// <summary>Whether the place she is in is painted so (every place, unless `--showcase=0`).</summary>
    bool IsShowcase => _dbgShowcase ?? true;
    /// <summary>How awake the reef is (the share of its leaks sealed, as of the last wave to pass), and the wave running now.</summary>
    float _wake, _waveRadius, _waveTo;
    System.Numerics.Vector2 _waveAt;

    /// <summary>Whether the showcase is on (her beam's view asks).</summary>
    public static bool ShowcaseOn { get; private set; }

    float LeaksSealedShare => _world.Leaks.Count == 0 ? 0f : _world.Leaks.Count(v => v.Sealed) / (float)_world.Leaks.Count;

    /// <summary>A new place: the showcase on if it is one of its reefs, as awake as its leaks are sealed.</summary>
    void EnterShowcase()
    {
        _showcase = IsShowcase;
        _camera.SetShowcase(_showcase);
        ShowcaseOn = _showcase;
        _wake = _showcase ? LeaksSealedShare : 0f;
        _waveRadius = 0f;
        PushShowcase();
    }

    /// <summary>A leak sealed: a wave of her light runs out from it, the reef behind it as awake as its sealed leaks make it.</summary>
    void ShowcaseLeakSealed(System.Numerics.Vector2 at)
    {
        if (!_showcase) return;
        // (A wave still running: the reef is as awake as it was bringing, at once.)
        if (_waveRadius > 0f) _wake = Mathf.Max(_wake, _waveTo);
        _waveAt = at;
        _waveRadius = 0.5f;
        _waveTo = LeaksSealedShare;
    }

    void StepShowcase(float dt)
    {
        if (!_showcase) return;
        // Her aura and her beam, for the reef they show (reef_show.gdshaderinc).
        var aura = _world.Aura;
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_show_aura"), aura.Shining && !_dying
            ? new Vector4(aura.Position.X, LevelMap.SwimBand, aura.Position.Y, aura.Range * 1.6f) : new Vector4(0f, -1000f, 0f, 0f));
        var beam = _world.Beam;
        _beamShown = Mathf.MoveToward(_beamShown, beam.Shining ? 1f : 0f, dt * (beam.Shining ? 8f : 4f));
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_beam_show"), _beamShown > 0f
            ? new Vector4(beam.Position.X, LevelMap.SwimBand, beam.Position.Y, System.Numerics.Vector2.Distance(beam.Position, _world.BeamEnd) + 1.5f) : new Vector4(0f, -1000f, 0f, 0f));
        // (The whole of her beam's fan, a split beam's rays within it.)
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_show_dir"), new Vector4(_world.BeamAim.X, _beamShown, _world.BeamAim.Y, Mathf.Max(_world.BeamSpread, beam.HalfAngle)));
        if (_waveRadius > 0f)
        {
            _waveRadius += WaveSpeed * dt;
            if (_waveRadius > WaveEnd)
            {
                _wake = Mathf.Max(_wake, _waveTo);
                _waveRadius = 0f;
            }
        }
        // (It shows the hidden murklings it passes over.)
        _blight.Wave = _waveRadius > 0f ? (_waveAt, _waveRadius) : null;
        PushShowcase();
    }

    void PushShowcase()
    {
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_showcase"), _showcase ? 1f : 0f);
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_wake"), _wake);
        RenderingServer.GlobalShaderParameterSet(SN.Of("reef_wave"), new Vector4(_waveAt.X, _waveAt.Y, _waveRadius, _waveTo));
        // Her light on the reef nearly white (a little of her own colour): on blue rock her warm light would turn it magenta.
        var white = HerColor.Light.Lerp(new Color(0.92f, 0.96f, 1f), 0.7f);
        // (In her pearls' colours, mixed, when she has any: docs/LIGHT-AND-GRAPH.md §10.)
        if (_world is not null && _world.HerHue != OctoShoots.Core.Plane.LightHue.None) white = white.Lerp(LightColour.Of(_world.HerHue), 0.6f);
        RenderingServer.GlobalShaderParameterSet(SN.Of("her_show_light"), new Vector3(white.R, white.G, white.B));
    }
}
