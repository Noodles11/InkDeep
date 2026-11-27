using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The reef slide (as the Reef Survey prototype dives): from an open level onto the next, the level below is the next
/// slice of the same reef, made round the spot she dives from; this level's reef morphs into its shape where it stands
/// (LevelView.MorphSea, the ground's slide), its life fading, and the level below takes over exactly there.
/// </summary>
public partial class TopDownMain
{
    /// <summary>This dive is a slide; the level below being made for it.</summary>
    bool _sliding;
    Task<LevelShape>? _slideShape;

    /// <summary>How long she gathers before the reef starts to slide, and how long it slides (s).</summary>
    const float SlideHold = 0.15f, SlideSeconds = 0.9f;

    /// <summary>The slide's level below, made round where she dives (and every chunk now on screen, so all of it morphs).</summary>
    void BeginSlide(LevelId next)
    {
        DropBelowView();
        var at = (_diveHole >= 0 ? HoleCenter(_diveHole) : _world.Player.Position) + _seaW;
        var also = _level.SeaShown.ToList();
        var seed = _seed;
        _slideShape = Task.Run(() => SeaShape(seed, next, at, also));
    }

    /// <summary>One frame of the slide: she holds till the level below is made, then the reef slides into it.</summary>
    void StepSlide(float dt)
    {
        _diveTime += dt;
        if (_below is null && _slideShape is { IsCompleted: true } task)
        {
            _slideShape = null;
            if (task.IsFaulted) GD.PushError(task.Exception?.ToString());
            else ShowBelow(task.Result, _diveHole, slide: true);
        }
        if (_below is not null && _belowMap is { } belowMap) OctoShoots.Game.Fx.SunMap.Prepare(belowMap, OctoShoots.Game.Fx.SunLight.Direction);
        bool ready = _below is not null && _belowCorruption is null && _belowMap is { } made && OctoShoots.Game.Fx.SunMap.Ready(made);
        if (!ready && _diveTime > SlideHold) _diveTime = SlideHold;
        float t = Mathf.Clamp((_diveTime - SlideHold) / SlideSeconds, 0f, 1f), k = Ease(t);
        if (ready) _level.SetSlide(k);
        if (ready && !_belowShaded && t > 0.5f) _belowShaded = OctoShoots.Game.Fx.SunMap.ShowAhead(_belowMap!, _belowOffset);
        // She dips and turns a little as the reef slides past her.
        float dip = Mathf.Sin(Mathf.Pi * t);
        _bell.DiveDepth = dip * 1.2f;
        _bell.DiveTurn = dip * 0.5f;
        _bell.Sync(_world, 1f, dt);
        SyncLantern();
        var focus = new Vector3(_diveFrom.X, LevelMap.SwimBand, _diveFrom.Y);
        _camera.Track(focus, dt);
        var look = _lookFrom == _lookTo ? _lookFrom : ReefLook.Blend(_lookFrom, _lookTo, k);
        if (_lookFrom != _lookTo) ApplyLook(look);
        _snow.Tick(dt, focus + Vector3.Up * 6f, _camera.Camera.GlobalBasis);
        _plankton.Tick(dt, focus + Vector3.Up * 2f, _camera.Camera.GlobalBasis);
        // This level's life goes as its reef becomes the next one's.
        float stay = 1f - k;
        FadeLevelLife(new Vector2(_diveFrom.X, _diveFrom.Y), 1000f, stay);
        _level.FadeCrack(stay);
        _corruption.Fade(stay);
        _minimap.DiveOut(k);
        _arrows.DiveOut(k);
        _damage.Tick(dt);
        _hud.Elapsed = _elapsed;
        _hud.Track(_world, _catalog, dt);
        if (t >= 1f && ready) FinishDive();
    }
}
