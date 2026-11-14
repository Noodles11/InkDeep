using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.Cubes;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The cube reef's stock in a layer's world (docs/proposals/CUBE-WORLD-PLAN.md §15.2): each claimed cube's murklings,
/// marked as its own, so what is left of them when she leaves the layer is put back where it was when she returns.
/// </summary>
public sealed partial class PlaneWorld
{
    /// <summary>One of a cube's stock, born where it stands (a prime if so), marked as that cube's.</summary>
    public void PlaceStock(CubeId cube, int spawn, bool prime, Vector2 at)
    {
        if (Bud(at, -1, prime, drink: false, born: true) is { } m) m.Stocked = (cube, spawn);
    }

    /// <summary>What is left of a cube's stock: each of its murklings still living, where it is.</summary>
    public List<StockLeft> StockLeft(CubeId cube) =>
        Murklings.Where(m => m.Alive && m.Stocked?.Cube == cube).Select(m => new StockLeft(m.Stocked!.Value.Spawn, m.Prime, m.Position)).ToList();
}
