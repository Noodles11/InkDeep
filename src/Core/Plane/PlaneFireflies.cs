using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her glow's second voice (docs/LIGHT-AND-GRAPH.md §15, the light grammar's sixth words). Her glow burns nothing by
/// itself; these change that.
/// <list type="bullet">
/// <item>Fireflies (Firefly Squid 4, Bubble Gland 3, Anemone Pump 1: Stat.Fireflies): motes of her light orbiting her
///   at Orbit of her glow's reach, turning Turn radians a second. Each is a tiny lantern (an emitter of hers): it
///   reveals and burns what is within Reach of it (Burn shots each), in her colours, and a murkling may drink it, as it
///   drinks any light: drunk, it goes out for Return seconds (× Stat.FireflyReturn: Anemone Pump halves it). Dark,
///   with her beam off, they are all the light she has.</item>
/// <item>Her glow's colour: what is within ColourReach of her glow's reach takes her colours, as her beam gives them
///   (an ember glow leaves what brushes it burning; frost chills it).</item>
/// </list>
/// </summary>
public static class FireflyTuning
{
    public const float Orbit = 0.85f, Turn = 1.3f, Reach = 1.6f, Burn = 0.3f, Return = 4f, Intensity = 0.7f, Bob = 0.25f;
    public const float ColourReach = 0.45f;
}

/// <summary>A firefly of hers: its place in the ring, its light (registered), out (drunk) for so long still.</summary>
public sealed class Firefly
{
    public required LightEmitter Emitter { get; init; }
    public float Out;
    public bool Lit => Out <= 0f;
}

public sealed partial class PlaneWorld
{
    sealed class FireflyOwner : ILightOwner
    {
        readonly PlaneWorld _w;
        public Firefly? Of;
        public FireflyOwner(PlaneWorld w) => _w = w;
        public float Energy => Of is { Lit: true } && !_w.Defeated ? 1f : 0f;

        public float Absorb(float amount)
        {
            if (Of is not { Lit: true } f) return 0f;
            // Drunk: it goes out, and comes back in its time.
            f.Out = FireflyTuning.Return * _w.Run.Loadout.Stats[Stat.FireflyReturn];
            _w.Events.Add(new PlaneEvent(PlaneEventType.FireflyDrunk, f.Emitter.Position, Vector2.Zero));
            return MathF.Min(amount, 1f);
        }
    }

    /// <summary>Her fireflies (as many as Stat.Fireflies), in their ring.</summary>
    public List<Firefly> Fireflies { get; } = new();
    float _fireflyTurn;

    /// <summary>Her fireflies placed round her (SyncLights): made as she gains them, each lit unless drunk.</summary>
    void SyncFireflies()
    {
        int want = (int)Run.Loadout.Stats[Stat.Fireflies];
        while (Fireflies.Count < want)
        {
            var owner = new FireflyOwner(this);
            var f = new Firefly { Emitter = Lights.Add(new LightEmitter { Owner = owner, Intensity = FireflyTuning.Intensity, Range = FireflyTuning.Reach, Colour = new Vector3(1f, 0.86f, 0.5f) }) };
            owner.Of = f;
            Fireflies.Add(f);
        }
        while (Fireflies.Count > want)
        {
            Lights.Remove(Fireflies[^1].Emitter);
            Fireflies.RemoveAt(Fireflies.Count - 1);
        }
        if (Fireflies.Count == 0) return;
        _fireflyTurn = (_fireflyTurn + FireflyTuning.Turn * Dt) % MathF.Tau;
        float orbit = MathF.Max(Aura.Range * FireflyTuning.Orbit, 1.2f);
        for (int i = 0; i < Fireflies.Count; i++)
        {
            var f = Fireflies[i];
            if (f.Out > 0f) f.Out = MathF.Max(0f, f.Out - Dt);
            float a = _fireflyTurn + i * MathF.Tau / Fireflies.Count;
            float r = orbit * (1f + FireflyTuning.Bob * 0.3f * MathF.Sin(Time * 2.1f + i * 1.7f));
            var at = Player.Position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            // (Not inside rock: it hangs at the rock's face.)
            var stop = FirstBlocked(Player.Position, at, 0.05f);
            f.Emitter.Position = stop == at ? at : Vector2.Lerp(Player.Position, stop, 0.85f);
            f.Emitter.Active = f.Lit && !Defeated;
        }
    }

    /// <summary>What her lit fireflies burn on a creature here (shots' worth), and her colours they carry.</summary>
    float FirefliesOn(Vector2 at, float radius, ref LightHue hue)
    {
        float n = 0f;
        foreach (var f in Fireflies)
        {
            if (!f.Lit || Vector2.Distance(f.Emitter.Position, at) > FireflyTuning.Reach + radius || !LineOfSight(f.Emitter.Position, at)) continue;
            n += FireflyTuning.Burn * BeamTick;
            hue |= HerHue;
        }
        return n;
    }

    /// <summary>Whether one of her lit fireflies lights a creature here (it shows in it).</summary>
    public bool FireflySees(Vector2 at, float radius)
    {
        foreach (var f in Fireflies)
            if (f.Lit && Vector2.Distance(f.Emitter.Position, at) <= FireflyTuning.Reach + radius && LineOfSight(f.Emitter.Position, at)) return true;
        return false;
    }

    /// <summary>Her glow's colours on a creature here: within ColourReach of her glow's reach, her colours (none, colourless).</summary>
    LightHue GlowHueAt(Vector2 at, float radius)
    {
        if (HerHue == LightHue.None || Aura is not { Shining: true } aura) return LightHue.None;
        return Vector2.Distance(aura.Position, at) - radius <= aura.Range * FireflyTuning.ColourReach && LineOfSight(aura.Position, at) ? HerHue : LightHue.None;
    }
}
