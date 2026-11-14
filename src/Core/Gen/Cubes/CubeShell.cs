using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// The prototype's stand-in for a level (CUBE-WORLD-PLAN §14.1): a reef world on a blank map of open water as wide as the
/// reef's footprint, with nothing in it but her. Given the cubes' field (PlaneWorld.Field), she swims and shines against
/// their rock, and every view of her (her bell, her beam, her light) works as on a reef.
/// </summary>
public static class CubeShell
{
    public static PlaneWorld Make(int footprint, Vector2 start, Tuning tuning, PlaneRun? run = null)
    {
        var map = new LevelMap(footprint * Side) { Seed = 1, Id = new LevelId(1, 1, 1) };
        for (int i = 0; i < map.Heights.Length; i++) map.Heights[i] = -2f;
        map.Pois.Add(new Poi { Kind = PoiKind.Start, Position = start, Radius = 3f });
        map.Pois.Add(new Poi { Kind = PoiKind.Exit, Position = new Vector2(-1000f, -1000f), Radius = 1f });
        var options = new PlaneOptions { Corruption = false, Pufferlings = false, Vases = false, Adventure = false };
        return new PlaneWorld(map, tuning, run, options);
    }
}
