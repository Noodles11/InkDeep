using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light's echoes (docs/LIGHT-AND-GRAPH.md §14, the light grammar's fifth words): what the light a creature had
/// drunk does when her light frees it. Each echo carries her colours (the mix on the creature, and hers) to what it
/// catches, and its rays take her path words; one generation deep: what an echo frees does not echo again.
/// <list type="bullet">
/// <item>Flare (Starburst Gland): a ring of her light FlareReach wide racing out, FlareShots shots' burn, clearing ink.</item>
/// <item>Spines (Fire Urchin Spine): SpineCount short rays out from where it was (evenly round, turned by its seed),
///   SpineReach long, SpineShots of a shot's light each, shining SpineTime; they bounce, fork, return and ghost as her
///   light does.</item>
/// <item>Gather (Lamprey Mouth): its core flies to her at once (GatherSpeed), worth GatherLight more (+1 a copy).</item>
/// </list>
/// Named when they meet: Supernova (Starburst Gland + Fire Urchin Spine: each spine ends in a small flare, NovaReach
/// wide).
/// </summary>
public static class EchoTuning
{
    public const float FlareReach = 3f, FlareShots = 1f;
    public const int SpineCount = 6;
    public const float SpineReach = 3.2f, SpineShots = 0.5f, SpineTime = 0.5f, SpineHalf = 0.09f;
    public const float GatherSpeed = 16f, GatherLight = 3f, GatherPerCopy = 1f;
    public const float NovaReach = 1.4f, NovaShots = 0.5f;
}

/// <summary>A spine of an echo shining out from where a creature was freed: where, which way, how long still, its colours.</summary>
public sealed class EchoSpine
{
    public Vector2 At, Way;
    public float Left;
    public LightHue Hue;
}

public sealed partial class PlaneWorld
{
    /// <summary>Echoes waiting for her light's step to end (freeing inside its loops must not change them).</summary>
    readonly List<(Vector2 At, LightHue Hue, float Seed)> _echoes = new();
    /// <summary>The spines shining now (Fire Urchin Spine), borne as rays of hers (PlanePaths).</summary>
    public List<EchoSpine> Spines { get; } = new();

    /// <summary>A creature her light freed: its echo, queued (FreedInColour).</summary>
    void Echo(object kin, Vector2 at)
    {
        var s = Run.Loadout;
        if (!s.Shot.Explosive && s.Count("fire_urchin_spine") == 0 && s.Count("lamprey_mouth") == 0) return;
        var hue = ColourOn(kin) | HerHue;
        _echoes.Add((at, hue, (at.X * 7.31f + at.Y * 3.17f) % MathF.Tau));
        // Gather: its core flies to her at once, the richer (its core is the last one left, where it was).
        int lamprey = s.Count("lamprey_mouth");
        if (lamprey > 0 && kin is Murkling && Cores.Count > 0 && Vector2.DistanceSquared(Cores[^1].Position, at) < 0.01f)
        {
            Cores[^1].Light += EchoTuning.GatherLight + EchoTuning.GatherPerCopy * (lamprey - 1);
            Cores[^1].Rush = true;
        }
    }

    /// <summary>The echoes of this step (after every creature has had her light's turn): flares and spines.</summary>
    void StepEchoes()
    {
        for (int i = Spines.Count - 1; i >= 0; i--)
        {
            Spines[i].Left -= Dt;
            if (Spines[i].Left <= 0f) Spines.RemoveAt(i);
        }
        if (_echoes.Count == 0) return;
        var echoes = _echoes.ToArray();
        _echoes.Clear();
        var s = Run.Loadout;
        float shot = PlaneCombatTuning.ShotDamage * DamageScale;
        bool nova = s.Has("supernova");
        foreach (var (at, hue, seed) in echoes)
        {
            if (s.Shot.Explosive)
            {
                Events.Add(new PlaneEvent(PlaneEventType.EchoFlare, at, Vector2.Zero, EchoTuning.FlareReach));
                EchoBurst(at, EchoTuning.FlareReach, shot * EchoTuning.FlareShots, hue);
            }
            if (s.Count("fire_urchin_spine") > 0)
                for (int k = 0; k < EchoTuning.SpineCount; k++)
                {
                    float a = seed + k * MathF.Tau / EchoTuning.SpineCount;
                    var way = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    Spines.Add(new EchoSpine { At = at, Way = way, Left = EchoTuning.SpineTime, Hue = hue });
                    if (!nova) continue;
                    // Supernova: each spine ends in a small flare.
                    float len = RockReach(at, way, EchoTuning.SpineReach, out _);
                    var tip = at + way * MathF.Max(len - 0.2f, 0f);
                    Events.Add(new PlaneEvent(PlaneEventType.EchoFlare, tip, Vector2.Zero, EchoTuning.NovaReach));
                    EchoBurst(tip, EchoTuning.NovaReach, shot * EchoTuning.NovaShots, hue);
                }
        }
    }

    /// <summary>A burst of her light from an echo: it burns and clears as a light burst does, and lends its colours to what it catches.</summary>
    void EchoBurst(Vector2 at, float reach, float burn, LightHue hue)
    {
        if (hue != LightHue.None)
            foreach (var (kin, pos) in CorruptedNow())
                if (Vector2.Distance(pos, at) <= reach && LineOfSight(at, pos)) EchoColour(kin, hue, pos);
        LightBurst(at, reach, burn);
    }

    /// <summary>An echo's colours on a creature it caught: ember left burning, frost's chill half-way, bloom and sunfire marked, lull.</summary>
    void EchoColour(object kin, LightHue hue, Vector2 at)
    {
        if (!_glare.TryGetValue(kin, out var g)) _glare[kin] = g = new Glare();
        g.Seen = Tick;
        ApplyColour(g, hue, at);
        if ((hue & LightHue.Blue) != 0) g.Chill = MathF.Max(g.Chill, 0.5f);
    }
}
