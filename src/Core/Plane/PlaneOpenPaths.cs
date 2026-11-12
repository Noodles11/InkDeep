using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light's paths in the open (docs/LIGHT-AND-GRAPH.md §13, the light grammar's fourth words): what her rays do on
/// their way, and where they end.
/// <list type="bullet">
/// <item>Lean (Anglerfish Lure): each ray of her own beam turns toward the nearest corrupted creature near its cone, up
///   to LeanDeg off her aim (easing, LeanTurn degrees a second).</item>
/// <item>Flare (Starfish Arm): each ray of her own widens along its length, FlareWide as wide again at its tip (a
///   trumpet).</item>
/// <item>Linger (Bubble Coral): where each ray of her own ends (rock, a body, its reach) a pool of her light stays
///   PoolLife seconds, PoolReach wide, burning on at PoolBurn of what her light burned there, in its colours, fading;
///   swept, they paint a trail.</item>
/// <item>Arc (Electric Eel Tail): arcs off what any light of hers burns, the rays born of it too (PlaneHerLight).</item>
/// </list>
/// Named when they meet: Will-o'-Wisp (Ghost Jelly + Anglerfish Lure: it leans twice as far, toward creatures behind
/// rock too), Pinball Storm (Mirror Scale + Electric Eel Tail: each glint where her light bounced arcs to the nearest
/// creature), Aurora (Bubble Coral and two colour pearls: her pools hang AuroraLife × as long, mixing their colours).
/// </summary>
public static class OpenPathTuning
{
    public const float LeanDeg = 10f, LeanTurn = 90f, WispLean = 2f;
    public const float FlareWide = 0.5f;
    public const float PoolLife = 2.5f, PoolReach = 0.8f, PoolSpacing = 0.55f, PoolBurn = 0.35f, AuroraLife = 2f;
    public const int MostPools = 90;
}

/// <summary>A pool of her light left where a ray of hers ended (Linger): where, how old, what it burns at first, its colours.</summary>
public sealed class LingerPool
{
    public Vector2 At;
    public float Age, Life, Worth;
    public LightHue Hue;
    /// <summary>0..1: how much of its light is left.</summary>
    public float Left => Life <= 0f ? 0f : Math.Clamp(1f - Age / Life, 0f, 1f);
}

public sealed partial class PlaneWorld
{
    /// <summary>Her pools of light (Linger), oldest first.</summary>
    public List<LingerPool> Pools { get; } = new();
    readonly List<float> _lean = new();

    /// <summary>
    /// Lean (SyncLights, her rays laid out): each ray of her own turned toward the nearest corrupted creature near it,
    /// easing; and Flare's widening set on them.
    /// </summary>
    void LeanRays(int count)
    {
        var s = Run.Loadout;
        float flare = s.Shot.Grow ? OpenPathTuning.FlareWide : 0f;
        bool wisp = s.Has("will_o_wisp");
        float most = OpenPathTuning.LeanDeg * (wisp ? OpenPathTuning.WispLean : 1f) * MathF.PI / 180f;
        while (_lean.Count < count) _lean.Add(0f);
        for (int i = 0; i < count; i++)
        {
            var ray = i == 0 ? Beam : _rays[i - 1];
            ray.Flare = flare;
            if (!s.Shot.Homing || !ray.Active)
            {
                _lean[i] = 0f;
                continue;
            }
            // The nearest creature within its reach and near its cone (in sight, unless it is a wisp).
            float want = 0f, nearest = float.MaxValue;
            foreach (var (_, at) in CorruptedNow())
            {
                var to = at - ray.Position;
                float d = to.Length();
                if (d < 0.5f || d > ray.Range || d >= nearest) continue;
                float off = MathF.Atan2(Cross(ray.Direction, to), Vector2.Dot(ray.Direction, to));
                if (MathF.Abs(off) > ray.HalfAngle * (1f + flare) + most) continue;
                if (!wisp && !LineOfSight(ray.Position, at)) continue;
                nearest = d;
                want = Math.Clamp(off, -most, most);
            }
            float step = OpenPathTuning.LeanTurn * MathF.PI / 180f * Dt;
            _lean[i] = want > _lean[i] ? MathF.Min(want, _lean[i] + step) : MathF.Max(want, _lean[i] - step);
            ray.Direction = Rotate(ray.Direction, _lean[i]);
        }
    }


    /// <summary>
    /// Linger (her light's step, the bodies gathered): a pool where each ray of her own ends, spaced along what she sweeps;
    /// the pools age and go.
    /// </summary>
    void StepPools()
    {
        for (int i = Pools.Count - 1; i >= 0; i--)
        {
            Pools[i].Age += Dt;
            if (Pools[i].Age >= Pools[i].Life) Pools.RemoveAt(i);
        }
        if (!Run.Loadout.Flags.Contains("linger") || !BeamOn || Defeated) return;
        float life = OpenPathTuning.PoolLife * (Run.Loadout.Has("aurora") ? OpenPathTuning.AuroraLife : 1f);
        foreach (var (ray, worth) in HerBeams)
        {
            if (!ray.Shining || ray == RearBeam || ray == SweepBeam || BornOf(ray) is not null) continue;
            var end = BeamStop(ray);
            // (Just short of the rock it lands on.)
            var at = end - ray.Direction * 0.2f;
            bool near = false;
            foreach (var p in Pools)
                if (p.Age < life * 0.5f && Vector2.DistanceSquared(p.At, at) < OpenPathTuning.PoolSpacing * OpenPathTuning.PoolSpacing)
                {
                    near = true;
                    break;
                }
            if (near) continue;
            float light = BeamFalloff(ray, Vector2.Distance(ray.Position, end));
            Pools.Add(new LingerPool
            {
                At = at, Life = life, Worth = worth * light * OpenPathTuning.PoolBurn, Hue = RayHue(ray) | HueAlong(ray.Position, end),
            });
            if (Pools.Count > OpenPathTuning.MostPools) Pools.RemoveAt(0);
        }
    }

    /// <summary>What her pools burn on a creature here (shots' worth), and the colours they lend it.</summary>
    float PoolsOn(Vector2 at, float radius, ref LightHue hue)
    {
        float n = 0f;
        foreach (var p in Pools)
        {
            if (Vector2.DistanceSquared(p.At, at) > (OpenPathTuning.PoolReach + radius) * (OpenPathTuning.PoolReach + radius)) continue;
            n += p.Worth * p.Left;
            hue |= p.Hue;
        }
        return n;
    }

    /// <summary>Whether one of her pools lights a creature here (it shows in it).</summary>
    public bool PoolSees(Vector2 at, float radius)
    {
        foreach (var p in Pools)
            if (Vector2.DistanceSquared(p.At, at) <= (OpenPathTuning.PoolReach + radius) * (OpenPathTuning.PoolReach + radius)) return true;
        return false;
    }

    /// <summary>Pinball Storm: from each glint where her light bounced, an arc to the nearest corrupted creature in reach.</summary>
    void GlintArcs()
    {
        if (!Run.Loadout.Has("pinball_storm")) return;
        float reach = Run.Loadout.Shot.ChainRange;
        for (int i = 0; i < _bornCount; i++)
        {
            var b = _born[i];
            if (b.Word != PathWord.Bounce || !b.Emitter.Shining) continue;
            object? best = null;
            Vector2 bestAt = default;
            float nearest = reach;
            foreach (var (kin, at) in CorruptedNow())
            {
                float d = Vector2.Distance(at, b.Emitter.Position);
                if (d >= nearest || !LineOfSight(b.Emitter.Position, at)) continue;
                nearest = d;
                best = kin;
                bestAt = at;
            }
            if (best is null) continue;
            _arced.Add(best);
            _arcs.Add((b.Emitter.Position, bestAt));
        }
    }
}
