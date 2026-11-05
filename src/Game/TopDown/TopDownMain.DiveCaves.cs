using System;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A dive's leak caves (docs/DIVE.md §6): a level's cave mouth leads down into a sub-level half a level's size, where the
/// Leak's water harms her till its leak is sealed. She may leave by its way out at any time, or, once it is sealed, up its
/// upwelling. The level waits as she left it: its state is kept as she goes in and put back as she comes out, while all
/// she gained in the cave (and its harm) stays hers.
/// </summary>
public partial class TopDownMain
{
    /// <summary>The leak cave she is in (its mouth on the level's map), or −1 on the level itself.</summary>
    int _diveCave = -1;
    /// <summary>The cave she went into last on this level (its mouth), and whether she left it sealed.</summary>
    int _caveMouth = -1;
    bool _caveSealed;
    /// <summary>Her HP as she left the cave: the level put back keeps it.</summary>
    float _caveHp;
    /// <summary>The level as she left it for the cave, with the luck it was made with; and, on her way out, the state to put back.</summary>
    LevelState? _levelState, _returnState;
    float _levelLuck;
    /// <summary>The level's anchors that leave the run as it is (PlaneWorld.SaveLevelAnchors): the cave's gains stay hers.</summary>
    OctoShoots.Core.Saves.Snapshot.Anchors? _levelAnchors;

    bool InDiveCave => _diveCave >= 0;

    /// <summary>Verification (--dbg-caveback=seconds): that long into a cave, she is taken back out of it.</summary>
    float _dbgCaveBack;

    /// <summary>A dive cave's world: the Leak's water harming her till its leak is sealed.</summary>
    static PlaneOptions DiveCaveOptions()
    {
        var d = PlaneOptions.Default;
        return new PlaneOptions { Corruption = d.Corruption, Pufferlings = d.Pufferlings, Vases = d.Vases, Adventure = d.Adventure, DiveCave = true };
    }

    /// <summary>The cave behind a level's mouth: its own id (beyond any level's), half a level's water, a little meaner.</summary>
    Task<LevelShape> ShapeDiveCave(int mouth)
    {
        var seed = _seed;
        var id = new LevelId(_id.Cycle + 500, _id.Depth, _id.Level * 10 + mouth + 1);
        float menace = _map.Menace + DiveCaveTuning.Menace;
        var into = _map.Mouths[mouth].Into;
        return Task.Run(() => LevelShape.Prepare(TopDownGenerator.GenerateCave(seed, id, menace, 1, into, areaScale: DiveCaveTuning.Area)));
    }

    /// <summary>
    /// Where a dive's cave takes her, if she is in a way through: into the cave behind an open mouth of the level; out of
    /// the cave by its way out (at any time); or, its leak sealed, up its upwelling. None until she has swum clear of the
    /// opening she came out of.
    /// </summary>
    (bool Into, int Mouth, Func<LevelMap, Landing?> Arrive, Doorway? By, RideWay Way)? DiveCaveWay()
    {
        if (Reef || _id.Haven || _world.Defeated) return null;
        var her = _world.Player.Position;
        bool atWay = _map.Way is { } w && w.Holds(her, ReefTuning.MouthIn);
        if (InDiveCave && _dbgCaveBack > 0f && _levelTime > _dbgCaveBack)
        {
            _dbgCaveBack = 0f;
            int mouth = _diveCave;
            return (false, mouth, map => mouth < map.Mouths.Count ? Through(map.Mouths[mouth]) : null, _map.Way, RideWay.Door);
        }
        if (!_portalsArmed)
        {
            _portalsArmed = _world.AtMouth() < 0 && !atWay;
            if (!_portalsArmed) return null;
        }
        if (!InDiveCave)
            return _world.AtMouth() is var m and >= 0
                ? (true, m, map => map.Way is { } way ? Through(way) : null, _map.Mouths[m], RideWay.Door)
                : null;
        int back = _diveCave;
        Landing? OutOfMouth(LevelMap map) => back < map.Mouths.Count ? Through(map.Mouths[back]) : null;
        if (atWay) return (false, back, OutOfMouth, _map.Way, RideWay.Door);
        if (_world.Leaks.Count > 0 && _world.LeaksOpen == 0 && System.Numerics.Vector2.Distance(her, _map.Exit.Position) < UpwellReach)
            return (false, back, OutOfMouth, null, RideWay.Up);
        return null;
    }

    /// <summary>Into a cave (the level kept as it is) or back out of it to the level as she left it.</summary>
    void BeginDiveRide((bool Into, int Mouth, Func<LevelMap, Landing?> Arrive, Doorway? By, RideWay Way) way)
    {
        Task<LevelShape> shape;
        if (way.Into)
        {
            _levelState = Moment(levelOnly: true);
            _levelLuck = _map.Luck;
            _caveMouth = way.Mouth;
            _diveCave = way.Mouth;
            shape = ShapeDiveCave(way.Mouth);
        }
        else
        {
            AddToRun();
            _caveHp = _world.Player.Hp;
            _caveSealed = !_world.CaveLeakOpen;
            _returnState = _levelState;
            _levelState = null;
            _diveCave = -1;
            shape = Shape(_seed, _id, _levelLuck);
        }
        BeginRide(ReefPlace.Run, _region, _mouth, way.Arrive, way.By, _floor, way.Way, shape);
    }

    /// <summary>
    /// Back on the level from a cave: put back as she left it, her HP and her place out of the mouth as they are now.
    /// False (and the level as it starts) if it cannot be.
    /// </summary>
    bool RestoreLevel(LevelState state, System.Numerics.Vector2? at)
    {
        if (_levelAnchors is null) return false;
        try
        {
            _world.LoadState(_levelAnchors, Convert.FromBase64String(state.World));
        }
        catch (Exception e)
        {
            GD.PushWarning($"The level could not be put back as she left it ({e.Message}); it starts again.");
            return false;
        }
        _world.Player.Hp = Mathf.Min(_caveHp, _run!.MaxHp);
        if (at is { } p)
        {
            _world.Player.Position = _world.Player.PrevPosition = p;
            _world.Player.Velocity = System.Numerics.Vector2.Zero;
        }
        return true;
    }

    /// <summary>She has come out of the ride: into a cave, word of it; out of one she sealed, its mouth closes behind her.</summary>
    void DiveCaveArrived()
    {
        if (Reef) return;
        if (InDiveCave) Say("A leak cave: its water stings till its leak is sealed · seal it for a pearl and a full heal");
        else if (_caveMouth >= 0 && _caveSealed)
        {
            _closeBehind = _caveMouth;
            _caveSealed = false;
        }
    }

    /// <summary>A new level, or a dive ended: no cave, nothing kept for one.</summary>
    void ForgetDiveCaves()
    {
        _diveCave = _caveMouth = -1;
        _caveSealed = false;
        _levelState = _returnState = null;
    }
}
