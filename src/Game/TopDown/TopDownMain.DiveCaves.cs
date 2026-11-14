using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A dive's leak caves (docs/DIVE.md §6; docs/LIGHT-AND-GRAPH.md §7): a level's cave mouth leads down into a
/// sub-level half a level's size, where the Leak's water harms her till its leak is sealed. Each is made with its reef
/// (LevelShape.Caves), so the ride in and out is short: the reef is kept, made, while she is below. She may leave by its
/// way out at any time, or, once it is sealed, up its upwelling. The level waits as she left it: its state is kept as
/// she goes in and put back as she comes out, out of the mouth she went in by, while all she gained in the cave (and
/// its harm) stays hers. A cave she leaves unsealed waits as she left it too, should she go back in.
///
/// A secret room's mouth leads the same way into its labyrinth (docs/DIVE.md §6.1; TopDownGenerator.GenerateLabyrinth),
/// made with the reef too: the reef's own look, no map, no creatures, its reward at its heart. She leaves by its way out,
/// or gives up at any time with the key that uses her active pearl (it carries her straight back out of the mouth). Its
/// reward taken, the mouth closes behind her; left with it still there, it waits as she left it.
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
    /// <summary>The reef as it was made, kept while she is in one of its caves (the way back needs nothing made).</summary>
    LevelShape? _reefShape;
    /// <summary>The caves she has left unsealed on this reef, as she left them (by mouth).</summary>
    readonly Dictionary<int, LevelState> _caveStates = new();
    /// <summary>The level's anchors that leave the run as it is (PlaneWorld.SaveLevelAnchors): the cave's gains stay hers.</summary>
    OctoShoots.Core.Saves.Snapshot.Anchors? _levelAnchors;

    bool InDiveCave => _diveCave >= 0;
    /// <summary>In a secret room's labyrinth (a dive cave behind a secret mouth).</summary>
    bool InLabyrinth => InDiveCave && _map.Labyrinth;
    /// <summary>Verification (--dbg-mazein=seconds): that long onto a reef with a secret mouth, its wall opens and she is taken into its labyrinth.</summary>
    float _dbgMazeIn;
    /// <summary>Verification (--dbg-havenin=seconds): that long onto a reef with a haven, she is taken into it.</summary>
    float _dbgHavenIn;
    /// <summary>Verification (--dbg-mazeout=seconds): that long into a labyrinth, she gives it up.</summary>
    float _dbgMazeOut;
    /// <summary>A labyrinth's reward is all taken (its shells and its pearl).</summary>
    bool LabyrinthEmptied => _world.Shells.Count == 0 && _world.Pearls.Count == 0;

    /// <summary>Verification (--dbg-caveback=seconds): that long into a cave, she is taken back out of it.</summary>
    float _dbgCaveBack;
    /// <summary>
    /// Verification (--dbg-cavein=seconds, --dbg-caverounds=n): that long onto a reef with a leak cave, she is taken into
    /// its first one; so many times (with --dbg-caveback, out and in again).
    /// </summary>
    float _dbgCaveIn;
    int _dbgCaveRounds = 1;

    /// <summary>A dive cave's world: the Leak's water harming her till its leak is sealed (a labyrinth's harms nothing).</summary>
    static PlaneOptions DiveCaveOptions(LevelMap map)
    {
        var d = PlaneOptions.Default;
        bool calm = map.Labyrinth || map.Haven is not null;
        return new PlaneOptions { Corruption = d.Corruption, Pufferlings = d.Pufferlings && !calm, Vases = d.Vases && !calm, Adventure = d.Adventure, Shots = d.Shots, DiveCave = map.LeakCave };
    }

    /// <summary>In a haven (docs/DIVE.md §4): a reef's haven behind its mouth (or, for an old save, Lantern Bay).</summary>
    bool InHavenPlace => _id.Haven || InDiveCave && _map.Haven is not null;

    /// <summary>
    /// The haven behind a reef's mouth (docs/DIVE.md §4): its own id (beyond any level's, leak cave's and labyrinth's), its
    /// way in going the way she swam.
    /// </summary>
    static LevelShape PrepareHaven(SeedCode seed, LevelMap reef, int mouth)
    {
        var id = new LevelId(reef.Id.Cycle + 700, reef.Id.Depth, reef.Id.Level * 10 + mouth + 1) { Lane = reef.Id.Lane };
        return LevelShape.Prepare(TopDownGenerator.GenerateHaven(seed, id, reef.Mouths[mouth].Into));
    }

    /// <summary>
    /// What a haven offers besides its shop (docs/DIVE.md §4): a pick of the services she has unlocked (the folk she has
    /// freed), from one to all of them, its own for each haven (from its seed). Null: not in a haven made so (all open).
    /// </summary>
    HashSet<BayService>? _havenOffers;

    void PickHavenOffers()
    {
        _havenOffers = null;
        if (!InDiveCave || _map.Haven is null) return;
        var open = Enum.GetValues<BayService>().Where(s => _jelly is not { } jelly || Folk.Opens(jelly, s)).ToList();
        var rng = new OctoShoots.Core.Rng(_map.Seed ^ 0x0FFE25UL);
        for (int i = open.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (open[i], open[j]) = (open[j], open[i]);
        }
        int keep = open.Count == 0 ? 0 : 1 + rng.Int(open.Count);
        _havenOffers = open.Take(keep).ToHashSet();
    }

    /// <summary>
    /// The cave behind a reef's mouth: its own id (beyond any level's, its reef's lane in it, so each reef's caves are
    /// their own), half a level's water, a little meaner.
    /// </summary>
    static LevelShape PrepareCave(SeedCode seed, LevelMap reef, int mouth)
    {
        var id = new LevelId(reef.Id.Cycle + 500, reef.Id.Depth, reef.Id.Level * 10 + mouth + 1) { Lane = reef.Id.Lane };
        return LevelShape.Prepare(TopDownGenerator.GenerateCave(seed, id, reef.Menace + DiveCaveTuning.Menace, 1, reef.Mouths[mouth].Into, areaScale: DiveCaveTuning.Area));
    }

    /// <summary>
    /// The labyrinth behind a reef's secret mouth: its own id (beyond any level's and any leak cave's), the reef's look,
    /// its way out going the way she swam in.
    /// </summary>
    static LevelShape PrepareLabyrinth(SeedCode seed, LevelMap reef, int mouth)
    {
        var id = new LevelId(reef.Id.Cycle + 600, reef.Id.Depth, reef.Id.Level * 10 + mouth + 1) { Lane = reef.Id.Lane };
        return LevelShape.Prepare(TopDownGenerator.GenerateLabyrinth(seed, id, reef.LookDepth ?? reef.Id.Depth, reef.Mouths[mouth].Into));
    }

    static LevelShape PrepareBehind(SeedCode seed, LevelMap reef, int mouth) =>
        reef.HavenMouth(mouth) ? PrepareHaven(seed, reef, mouth) : reef.LeakMouth(mouth) ? PrepareCave(seed, reef, mouth) : PrepareLabyrinth(seed, reef, mouth);

    /// <summary>A reef's caves, made with it (on the same thread, after it).</summary>
    static void PrepareCaves(SeedCode seed, LevelShape reef)
    {
        if (reef.Map.LeakCave || reef.Map.Labyrinth || reef.Map.Mouths.Count == 0) return;
        var caves = new List<LevelShape>();
        for (int i = 0; i < reef.Map.Mouths.Count; i++) caves.Add(PrepareBehind(seed, reef.Map, i));
        reef.Caves = caves;
    }

    /// <summary>The cave behind a mouth of the reef: made with it (else made now).</summary>
    Task<LevelShape> ShapeDiveCave(int mouth)
    {
        if (_levelShape is { } reef && mouth < reef.Caves.Count) return Task.FromResult(reef.Caves[mouth]);
        var seed = _seed;
        var map = _map;
        return Task.Run(() => PrepareBehind(seed, map, mouth));
    }

    /// <summary>
    /// Where a dive's cave takes her, if she is in a way through: into the cave behind an open mouth of the level; out of
    /// the cave by its way out (at any time); or, its leak sealed, up its upwelling. None until she has swum clear of the
    /// opening she came out of.
    /// </summary>
    (bool Into, int Mouth, Func<LevelMap, Landing?> Arrive, Doorway? By, RideWay Way)? DiveCaveWay()
    {
        if (_id.Haven || _world.Defeated) return null;
        var her = _world.Player.Position;
        bool atWay = _map.Way is { } w && w.Holds(her, ReefTuning.MouthIn);
        if (!InDiveCave && _dbgCaveIn > 0f && _dbgCaveRounds > 0 && _levelTime > _dbgCaveIn && _map.Mouths.Count > 0 && !_world.ShutMouths.Contains(0))
        {
            _dbgCaveRounds--;
            return (true, 0, map => map.Way is { } way ? Through(way) : null, _map.Mouths[0], RideWay.Door);
        }
        // Verification: into the first secret mouth's labyrinth, its wall opened.
        if (!InDiveCave && _dbgHavenIn > 0f && _levelTime > _dbgHavenIn && _map.MouthKinds.IndexOf(PoiKind.Haven) is var haven and >= 0)
        {
            _dbgHavenIn = 0f;
            return (true, haven, map => map.Way is { } way ? Through(way) : null, _map.Mouths[haven], RideWay.Door);
        }
        if (!InDiveCave && _dbgMazeIn > 0f && _levelTime > _dbgMazeIn && _map.MouthKinds.IndexOf(PoiKind.Secret) is var maze and >= 0)
        {
            _dbgMazeIn = 0f;
            var door = _map.Mouths[maze];
            foreach (var seal in _world.Seals)
                if (!seal.Open && seal.Rock.Contains(door.At - door.Into * 0.5f)) _world.DebugOpenSeal(seal);
            return (true, maze, map => map.Way is { } way ? Through(way) : null, door, RideWay.Door);
        }
        // Verification (--dbg-mazeout): giving a labyrinth up, straight back out of the mouth she came in by.
        if (InLabyrinth && _dbgMazeOut > 0f && _levelTime > _dbgMazeOut)
        {
            _dbgMazeOut = 0f;
            int mouth = _diveCave;
            return (false, mouth, map => mouth < map.Mouths.Count ? Through(map.Mouths[mouth]) : null, null, RideWay.Up);
        }
        if (InDiveCave && _dbgCaveBack > 0f && _levelTime > _dbgCaveBack)
        {
            if (_dbgCaveRounds <= 0) _dbgCaveBack = 0f;
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
            _reefShape = _levelShape;
            _caveHp = _world.Player.Hp;
            _caveMouth = way.Mouth;
            _diveCave = way.Mouth;
            // A cave she left unsealed: as she left it.
            _returnState = _caveStates.TryGetValue(way.Mouth, out var kept) ? kept : null;
            shape = ShapeDiveCave(way.Mouth);
        }
        else
        {
            AddToRun();
            _caveHp = _world.Player.Hp;
            // (A labyrinth: done with once its reward is taken; a haven never is.)
            _caveSealed = InHavenPlace ? false : InLabyrinth ? LabyrinthEmptied : !_world.CaveLeakOpen;
            // Unsealed, it waits as she leaves it (sealed, its mouth closes behind her).
            if (!_caveSealed && Moment(levelOnly: true) is { } cave) _caveStates[_diveCave] = cave;
            else _caveStates.Remove(_diveCave);
            _returnState = _levelState;
            _levelState = null;
            _diveCave = -1;
            shape = _reefShape is { } reef ? Task.FromResult(reef) : Shape(_seed, _id, _levelLuck);
            _reefShape = null;
        }
        BeginRide(way.Arrive, way.By, way.Way, shape);
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
        if (_captureDir is not null) GD.Print($"Put back as she left it: the {(InDiveCave ? "cave" : "reef")} at tick {_world.Tick}, {_world.Murklings.Count} murklings");
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
        if (InLabyrinth) Say("A secret labyrinth: find its heart, and its current will carry you home");
        else if (InHavenPlace) Say("A haven: safe water, healed whole, all you carry banked");
        else if (InDiveCave) Say("A leak cave: its water stings till its leak is sealed · seal it for a pearl and a full heal");
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
        _reefShape = null;
        _caveStates.Clear();
    }
}
