using System.Numerics;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Gen.TopDown;

public static partial class TopDownGenerator
{
    /// <summary>A room's water: this share of a level's area.</summary>
    public const float RoomArea = 0.45f;

    /// <summary>
    /// A cube reef's room behind a mouth (docs/proposals/CUBE-WORLD-PLAN.md §9): a small level holding the one place it is
    /// for (a clam room, PoiKind.TreasureCave), its way in cut as a cave's is, going the way she swam.
    /// </summary>
    public static LevelMap GenerateRoom(SeedCode seed, LevelId id, PoiKind kind, float menace, int lookDepth, Vector2? into = null)
    {
        var plan = new LevelPlan
        {
            Id = id,
            Treasures = kind == PoiKind.TreasureCave ? 1 : 0,
            Menace = menace,
            Template = LevelTemplates.Get("default"),
            AreaScale = RoomArea,
        };
        var map = Generate(new RunStreams(seed), plan, null, "", 0f);
        map.Room = kind;
        map.LookDepth = lookDepth;
        CutWayIn(map, into);
        return map;
    }
}
