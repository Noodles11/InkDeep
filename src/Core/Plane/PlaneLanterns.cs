using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>Stone lanterns (docs/LIGHT-AND-GRAPH.md §2.7; PlaneLanterns.cs). Numbers are first guesses, tuned in play.</summary>
public static class LanternTuning
{
    /// <summary>How long her beam must be held on one to light it (s; taken off, it cools back at half that pace).</summary>
    public const float Kindle = 1.5f;
    /// <summary>Its body's radius (m), for her beam finding it.</summary>
    public const float Radius = 0.45f;
    /// <summary>How far its pool of light shows the reef and the creatures in it (m).</summary>
    public const float Reach = 6f;
    /// <summary>She heals within this of it (m), this share of her whole health a second, out of a store of this share of it.</summary>
    public const float HealReach = 3f, HealPerSecond = 0.06f, Store = 0.5f;
    /// <summary>How likely a cube is to hold one, and how far it stands from rock, from the cube's ways and from its edges (m).</summary>
    public const float Chance = 0.45f, Clear = 2.5f, FromWays = 9f, Inside = 6f;
}

/// <summary>
/// A stone lantern (tōrō) standing on a cube's floor: grey stone in the dark. Her beam held on it lights it for good: a
/// small fire in its window, a pool of light round it that shows the reef in its colours and the creatures in it, and a
/// gentle healing for her by it, out of a store.
/// </summary>
public sealed class StoneLantern
{
    public Vector2 Position;
    public OctoShoots.Core.Gen.Cubes.CubeId Cube;
    public bool Lit;
    /// <summary>Her beam on it now, and how long it has been (s, toward LanternTuning.Kindle).</summary>
    public bool Beamed;
    public float Held;
    /// <summary>What it has left to heal her with (hp).</summary>
    public float Store;
    /// <summary>She is healing by it now.</summary>
    public bool Healing;
}

public sealed partial class PlaneWorld
{
    public List<StoneLantern> Lanterns { get; } = new();

    /// <summary>A lantern stood in a cube (as it is laid), lit already with what is left of its store, or not (store null).</summary>
    public StoneLantern PlaceLantern(OctoShoots.Core.Gen.Cubes.CubeId cube, Vector2 at, float? store)
    {
        var lantern = new StoneLantern { Position = at, Cube = cube, Lit = store is not null, Store = store ?? 0f };
        Lanterns.Add(lantern);
        return lantern;
    }

    /// <summary>A cube's lantern as it is now: null if it has none or it is not lit, else what is left of its store.</summary>
    public float? LanternLeft(OctoShoots.Core.Gen.Cubes.CubeId cube) =>
        Lanterns.FirstOrDefault(l => l.Cube == cube && l.Lit) is { } lit ? lit.Store : null;

    /// <summary>How much a lit lantern's pool shows at a spot (0..1): full within most of its reach, thinning at its edge; not round rock.</summary>
    public float LanternSees(Vector2 at, float radius)
    {
        float seen = 0f;
        foreach (var l in Lanterns)
        {
            if (!l.Lit) continue;
            float d = Vector2.Distance(at, l.Position) - radius;
            float inner = LanternTuning.Reach * 0.7f, outer = LanternTuning.Reach;
            if (d >= outer) continue;
            float t = Math.Clamp((d - inner) / (outer - inner), 0f, 1f);
            float s = 1f - t * t * (3f - 2f * t);
            if (s > seen && LineOfSight(l.Position, at)) seen = s;
        }
        return seen;
    }

    /// <summary>The lanterns: kindled by her beam held on them, and the lit ones healing her by them.</summary>
    void StepLanterns()
    {
        if (Lanterns.Count == 0) return;
        var p = Player;
        foreach (var l in Lanterns)
        {
            l.Healing = false;
            if (!l.Lit)
            {
                // (Far off, her beam does not reach it.)
                l.Beamed = false;
                if (Vector2.DistanceSquared(l.Position, p.Position) > 30f * 30f) continue;
                if (!Defeated && !Stained(l.Position))
                    foreach (var (beam, _) in HerBeams)
                        if (BeamLight(beam, l.Position, 0f, LanternTuning.Radius, l) > 0f) l.Beamed = true;
                l.Held = l.Beamed ? l.Held + Dt : MathF.Max(0f, l.Held - Dt * 0.5f);
                if (l.Held < LanternTuning.Kindle) continue;
                l.Lit = true;
                l.Beamed = false;
                l.Store = Run.MaxHp * LanternTuning.Store;
                Events.Add(new PlaneEvent(PlaneEventType.LanternLit, l.Position, Vector2.Zero, LanternTuning.Reach));
                continue;
            }
            if (Defeated || l.Store <= 0f || Vector2.Distance(p.Position, l.Position) > LanternTuning.HealReach) continue;
            float max = Run.MaxHp;
            if (p.Hp >= max) continue;
            float heal = MathF.Min(MathF.Min(max * LanternTuning.HealPerSecond * Dt, max - p.Hp), l.Store);
            p.Hp += heal;
            Run.Hp = p.Hp;
            l.Store -= heal;
            l.Healing = true;
        }
    }
}
