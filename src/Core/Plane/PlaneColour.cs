using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The colours of her light (docs/LIGHT-AND-GRAPH.md §10, the light grammar's first words): three pearls tint it,
/// Fire Coral ember (red), Glowing Moss bloom (green), Frost Kelp frost (blue), each copy deepening its tint. One ray
/// takes all her colours mixed; a split beam deals them across its rays, left to right in turn. Where coloured light
/// falls on a corrupted creature the colours of every ray (and of the prism stones it passed through, and of a charge
/// stone she lit in colour) mix on it, as light mixes, and act:
/// <list type="bullet">
/// <item>ember: it keeps burning EmberTime after the light has left it;</item>
/// <item>bloom: ink and ash clear BloomClear × as fast in it, and a murkling it frees leaves BloomCore × the light;</item>
/// <item>frost: it slows the longer it is lit, standing still after FrostTime (thawing over Thaw out of it);</item>
/// <item>sunfire (ember + bloom): what it frees bursts in a ring of her light SunfireReach wide;</item>
/// <item>shatter (ember + frost): a creature frost has stilled takes the ember all at once, a burst of steam ShatterReach wide;</item>
/// <item>lull (bloom + frost): it forgets her (it does not see her) while lit and LullTime after;</item>
/// <item>daybreak (all three, white): all of these, and her burn × WhiteBurn.</item>
/// </list>
/// A coloured ray opens a coloured seal of its colour, and a charge stone she charges in colour glows in it and lends it
/// to what passes through its light. Bosses take no colours.
/// </summary>
public static class ColourTuning
{
    public const float EmberTime = 2.5f, EmberPerCopy = 1.5f;
    public const float FrostTime = 2f, FrostSlow = 0.85f, Thaw = 1.5f;
    public const float BloomClear = 2f, BloomCore = 1.5f, BloomPerCopy = 0.5f;
    public const float SunfireReach = 3f, SunfireShots = 1f, ShatterReach = 2.5f, ShatterShots = 2f;
    public const float LullTime = 1.5f, WhiteBurn = 1.5f;
    /// <summary>How long a creature counts as lit in a colour after the light leaves it (for what it frees).</summary>
    public const float Mark = 0.35f;

    /// <summary>The ember's afterglow and the frost's time to standstill, for so many copies.</summary>
    public static float Ember(int copies) => EmberTime + EmberPerCopy * MathF.Max(copies - 1, 0);
    public static float Frost(int copies) => FrostTime / (1f + 0.5f * MathF.Max(copies - 1, 0));
    public static float Bloom(float baseline, int copies) => baseline + BloomPerCopy * MathF.Max(copies - 1, 0);
}

public sealed partial class PlaneWorld
{
    /// <summary>Her colours: all of them mixed (her glow, Rear Fin's and Lighthouse's beams, an unsplit beam).</summary>
    public LightHue HerHue { get; private set; }
    LightHue _beamHue;
    readonly List<LightHue> _rayHue = new();
    /// <summary>Colour bursts waiting for the light's step to end (freeing inside its loops must not change them).</summary>
    readonly List<(Vector2 At, bool Shatter)> _colourBursts = new();

    /// <summary>Her colours in the order they are dealt: ember, bloom, frost (the ones she has).</summary>
    List<LightHue> Primaries()
    {
        var s = Run.Loadout.Shot;
        var list = new List<LightHue>(3);
        if (s.Ember > 0) list.Add(LightHue.Red);
        if (s.Bloom > 0) list.Add(LightHue.Green);
        if (s.Frost > 0) list.Add(LightHue.Blue);
        return list;
    }

    /// <summary>
    /// Deals her colours across her beam's rays (SyncLights, after the layout): one ray takes them all; a fan takes them
    /// in turn from its left, the rays round her (Kraken Form) after it.
    /// </summary>
    void DealHues(List<(float Offset, float Width, float Reach, float Worth)> layout, int fan)
    {
        var prim = Primaries();
        var all = LightHue.None;
        foreach (var h in prim) all |= h;
        HerHue = all;
        while (_rayHue.Count < Math.Max(layout.Count - 1, 0)) _rayHue.Add(LightHue.None);
        if (layout.Count <= 1 || prim.Count <= 1)
        {
            _beamHue = all;
            for (int i = 0; i < _rayHue.Count; i++) _rayHue[i] = all;
            return;
        }
        // The fan's rays (the first `fan` of the layout) by their place left to right.
        var order = new List<int>();
        for (int i = 0; i < fan && i < layout.Count; i++) order.Add(i);
        order.Sort((a, b) => layout[a].Offset.CompareTo(layout[b].Offset));
        var hueOf = new LightHue[layout.Count];
        for (int k = 0; k < order.Count; k++) hueOf[order[k]] = prim[k % prim.Count];
        for (int i = fan; i < layout.Count; i++) hueOf[i] = prim[(i - fan + order.Count) % prim.Count];
        _beamHue = hueOf[0];
        for (int i = 1; i < layout.Count; i++) _rayHue[i - 1] = hueOf[i];
    }

    /// <summary>The colour her pearls give one of her beams (before any prism stone it passes through).</summary>
    public LightHue RayHue(LightEmitter beam)
    {
        if (beam == Beam) return _beamHue;
        int i = _rays.IndexOf(beam);
        return i >= 0 && i < _rayHue.Count ? _rayHue[i] : HerHue;
    }

    /// <summary>What colour of her light is on a creature now (ember while it still burns on), and how far frost has stilled it (0..1).</summary>
    public LightHue ColourOn(object creature) =>
        _glare.TryGetValue(creature, out var g) ? g.Hue | (g.Afterglow > 0f && g.Ember ? LightHue.Red : LightHue.None) : LightHue.None;

    public float ChillOf(object creature) => _glare.TryGetValue(creature, out var g) ? g.Chill : 0f;

    /// <summary>How fast a creature may go now: a Stingray Barb's slow, and frost's chill (none once stilled).</summary>
    public float PaceOf(object creature)
    {
        if (!_glare.TryGetValue(creature, out var g)) return 1f;
        return (g.Slow > 0f ? Items.StatusRules.SlowFactor : 1f) * (1f - ColourTuning.FrostSlow * g.Chill);
    }

    /// <summary>The colour a charge stone she lit in colour lends to a point in its light (none from a plain one).</summary>
    LightHue LentAt(Vector2 at)
    {
        var hue = LightHue.None;
        foreach (var c in ChargeStones)
        {
            if (c.Lent == LightHue.None || c.Glow <= 0f) continue;
            if (Vector2.Distance(c.Position, at) > ChargeStoneTuning.ShownReach * MathF.Sqrt(c.Glow) || !LineOfSight(c.Position, at)) continue;
            hue |= c.Lent;
        }
        return hue;
    }

    /// <summary>Whether a bloom-coloured beam of hers reaches a point (ash there clears the faster).</summary>
    bool BloomReaches(Vector2 at, float height)
    {
        if ((HerHue & LightHue.Green) == 0) return false;
        foreach (var (beam, _) in HerBeams)
            if ((RayHue(beam) & LightHue.Green) != 0 && BeamReaches(beam, at, height, 0.3f)) return true;
        return false;
    }

    /// <summary>
    /// The colours on a creature this step act on it (Touch): ember leaves it burning on, frost chills it to a standstill,
    /// lull makes it lose her, shatter bursts a stilled one; bloom and sunfire mark it for when it is freed.
    /// </summary>
    void ApplyColour(Glare g, LightHue hue, Vector2 at)
    {
        g.Hue = hue;
        var s = Run.Loadout.Shot;
        bool red = (hue & LightHue.Red) != 0, green = (hue & LightHue.Green) != 0, blue = (hue & LightHue.Blue) != 0;
        if (red)
        {
            g.Afterglow = MathF.Max(g.Afterglow, ColourTuning.Ember(s.Ember));
            g.Ember = true;
        }
        else if (g.Afterglow <= 0f) g.Ember = false;
        g.Chill = blue ? MathF.Min(1f, g.Chill + Dt / ColourTuning.Frost(s.Frost)) : MathF.Max(0f, g.Chill - Dt / ColourTuning.Thaw);
        // Stilled: held where it is (as a dazzle holds it) while the frost has it.
        if (g.Chill >= 1f) g.Frozen = MathF.Max(g.Frozen, Dt * 2f);
        g.Bloom = green ? ColourTuning.Mark : MathF.Max(0f, g.Bloom - Dt);
        g.Sun = red && green ? ColourTuning.Mark : MathF.Max(0f, g.Sun - Dt);
        if (green && blue) g.Charm = MathF.Max(g.Charm, ColourTuning.LullTime);
        if (red && blue && g.Chill >= 1f)
        {
            g.Chill = 0f;
            g.Frozen = 0f;
            _colourBursts.Add((at, true));
        }
    }

    /// <summary>A creature her light freed: bloom leaves its core the richer, sunfire bursts from it.</summary>
    void FreedInColour(object kin, Vector2 at)
    {
        if (!_glare.TryGetValue(kin, out var g)) return;
        if (g.Bloom > 0f && kin is Murkling && Cores.Count > 0 && Vector2.DistanceSquared(Cores[^1].Position, at) < 0.01f)
            Cores[^1].Light *= ColourTuning.Bloom(ColourTuning.BloomCore, Run.Loadout.Shot.Bloom);
        if (g.Sun > 0f) _colourBursts.Add((at, false));
    }

    /// <summary>The colour bursts of this step: sunfire's ring of light and shatter's steam, each a burst of her light.</summary>
    void StepColourBursts()
    {
        if (_colourBursts.Count == 0) return;
        var bursts = _colourBursts.ToArray();
        _colourBursts.Clear();
        float shot = PlaneCombatTuning.ShotDamage * DamageScale;
        foreach (var (at, shatter) in bursts)
        {
            Events.Add(new PlaneEvent(shatter ? PlaneEventType.ColourShatter : PlaneEventType.ColourSunfire, at, Vector2.Zero,
                shatter ? ColourTuning.ShatterReach : ColourTuning.SunfireReach));
            LightBurst(at, shatter ? ColourTuning.ShatterReach : ColourTuning.SunfireReach, shot * (shatter ? ColourTuning.ShatterShots : ColourTuning.SunfireShots));
        }
    }
}
