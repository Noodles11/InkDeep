using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>A clam's numbers (tune).</summary>
public static class ClamTuning
{
    /// <summary>The light on it (any light) that opens it; the share of clams holding a murkling rather than a pearl.</summary>
    public const float Touch = 0.12f, MurkShare = 0.1f;
}

/// <summary>
/// A clam (the treasure place, PoiKind.TreasureCave): a giant clam lying shut on the path. Any light touching it opens
/// it, once and for good, and shows what it has been keeping: a pearl (Pearl), or now and then a murkling, which comes
/// out of it at her.
/// </summary>
public sealed class PlaneClam
{
    public Vector2 Position;
    public int Poi;
    public PlanePearl? Pearl;
    public bool Murk, Open;
    /// <summary>Seconds since it opened (the view's lid).</summary>
    public float Opened;
}

public sealed partial class PlaneWorld
{
    public List<PlaneClam> Clams { get; } = new();

    /// <summary>A pearl still shut in its clam: it cannot be taken yet.</summary>
    public bool Shut(PlanePearl pearl) => Clams.Exists(c => c.Pearl == pearl && !c.Open);

    void StepClams()
    {
        foreach (var c in Clams)
        {
            if (c.Open)
            {
                c.Opened += Dt;
                continue;
            }
            if (LightAt(c.Position) < ClamTuning.Touch) continue;
            c.Open = true;
            Events.Add(new PlaneEvent(PlaneEventType.ClamOpened, c.Position, Vector2.Zero));
            if (c.Murk) Bud(c.Position, -1, drink: false, born: true);
        }
    }

    /// <summary>A clam's place is done once it has opened and nothing is left in it.</summary>
    bool ClamDone(int poi) => Clams.Find(c => c.Poi == poi) is not { } c || c.Open && (c.Pearl is null || c.Pearl.Taken);
}
