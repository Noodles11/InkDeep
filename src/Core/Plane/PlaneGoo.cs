using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The reef's soft edge: the rock is not a hard wall to her. Swimming into it she goes on past the line a little, the
/// water thickening round her like goo: it slows her (Drag × how deep) and pushes her back out (Spring × how deep), so
/// she stops within about a metre and can go no further than Most; let go, it eases her back out to the water. The view
/// lifts her the deeper she is, as a jellyfish swims up against the reef. Only she has it: the creatures keep the hard
/// line, and her light and her shots start from the water's edge she came in from (PlaneWorld.HerOrigin).
/// </summary>
public static class GooTuning
{
    /// <summary>How deep past the line she may go (metres), its push back (m/s² a metre deep) and its drag (/s a metre deep).</summary>
    public const float Most = 1.1f, Spring = 45f, Drag = 7f;
    /// <summary>And it eases her back out by this share of how deep she is each second (her own stopping never holds her in it).</summary>
    public const float Return = 3f;
}

public sealed partial class PlaneWorld
{
    /// <summary>
    /// How far a point lies inside the reef's soft edge for her (0: open water her whole body fits in), and the way back
    /// out: the nearest point she fits, looked for in rings out to past Most.
    /// </summary>
    public (float Depth, Vector2 Out) GooAt(Vector2 p)
    {
        if (Clear(p, Radius)) return (0f, Vector2.Zero);
        const int Ways = 16;
        for (float r = 0.05f; r <= GooTuning.Most + 0.45f; r += 0.05f)
            for (int k = 0; k < Ways; k++)
            {
                float a = k * MathF.Tau / Ways;
                var way = new Vector2(MathF.Cos(a), MathF.Sin(a));
                if (Clear(p + way * r, Radius)) return (r, way);
            }
        return (GooTuning.Most + 0.5f, -WallNormal(p, Radius));
    }

    /// <summary>Where her light and her shots start from: her body, or, sunk in the reef's soft edge, the water's edge she came in from.</summary>
    public Vector2 HerOrigin => Player.Position + Player.GooOut * Player.Goo;
}
