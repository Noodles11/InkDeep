using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her light (docs/ADVENTURE.md §8): a lamp in her bell, shining all round her, that burns nothing. What burns is her
/// shots' light: each shot of hers in flight is a small lantern, reaching ShotReach for a plain shot (× the square root of
/// its share of one's damage, as its lamp is drawn), and every corrupted creature some of whose body it reaches, with
/// nothing solid between, slowly loses corruption, Burn a second for each shot lighting it (× her damage stat), and is
/// freed when it has none left; a guarding Inkwarden turns the burn aside as it turns her shots. Coming into a shot's
/// light dazzles a creature: it stops dead for Dazzle seconds (a throw it was winding up is let go), then carries on; it
/// can be dazzled again once it has been out of every shot's light Rearm seconds. Murklings, Inkwardens and Hearthguards
/// are dazzled; the older rooted and leaping creatures only burn, and a boss only burns where it lies open to the light.
/// </summary>
public static class HerLightTuning
{
    public const float ShotReach = 3.4f, Burn = 3f;
    /// <summary>
    /// Her beam (docs/LIGHT-AND-GRAPH.md §2.3) burns as BeamShots plain shots would, and strikes a seal it falls on
    /// with that many shots' light a second. Along its axis it clears the ink dots within BeamDots metres.
    /// </summary>
    public const int BeamShots = 2;
    public const float BeamDots = 0.5f;
    public const float Dazzle = 0.8f, Rearm = 2f;
    /// <summary>Her light's reach on a cave's walls (× √ her glow stat): the ash within it, in sight of her, clears (AshTuning).</summary>
    public const float Reach = 6f;
}

public sealed partial class PlaneWorld
{
    /// <summary>A creature's moments in her shots' light: in it now (and the nearest shot lighting it), dazzled for so long still, out of it for so long.</summary>
    sealed class Glare
    {
        public bool Lit;
        public Vector2 From;
        public float Frozen, Dark = HerLightTuning.Rearm;
        public long Seen;
        /// <summary>
        /// Her beam's pearls on it (§5.1): how long a beam has burned it without a break (each whole second rolls her
        /// chance effects), and what they left: an afterglow burning on (Fire Coral, Sea Nettle), slowed, charmed (it
        /// does not see her), a crit (her burn × CritMult).
        /// </summary>
        public float BeamTime, Afterglow, Slow, Charm, Crit;
        /// <summary>
        /// Her light's colours on it (PlaneColour.cs): the mix on it this step, frost's chill (1: stilled), how lately bloom
        /// and sunfire lit it, and whether its afterglow is the ember's.
        /// </summary>
        public LightHue Hue;
        public float Chill, Bloom, Sun;
        public bool Ember;
    }

    /// <summary>What her beam burned last step (for Electric Eel Tail's arcs), the arcs this step, and her beam's dice.</summary>
    readonly List<(object Kin, Vector2 At)> _beamLit = new(), _beamLitBefore = new();
    readonly HashSet<object> _arced = new();
    readonly List<(Vector2 From, Vector2 To)> _arcs = new();
    Rng? _beamRng;

    /// <summary>Electric Eel Tail's arcs this step: from what her beam burns to what it arcs on to.</summary>
    public IReadOnlyList<(Vector2 From, Vector2 To)> Arcs => _arcs;

    /// <summary>Whether a creature is slowed (Stingray Barb) or charmed (Siren Song: it does not see her) by her beam.</summary>
    public bool Slowed(object creature) => _glare.TryGetValue(creature, out var g) && g.Slow > 0f;
    public bool Charmed(object creature) => _glare.TryGetValue(creature, out var g) && g.Charm > 0f;

    readonly Dictionary<object, Glare> _glare = new();
    readonly List<PlaneShot> _burners = new();
    readonly List<PlaneShot> _lights = new();

    /// <summary>Tests: false turns her shots' burn and dazzle off, to watch a creature or a shot on its own.</summary>
    public bool LightBurns { get; set; } = true;

    /// <summary>How far her own light reaches on a cave's walls now (Lantern Pearl: farther).</summary>
    public float LightReach => HerLightTuning.Reach * MathF.Sqrt(MathF.Max(Run.Loadout.Stats[Stat.Glow], 0.1f));

    /// <summary>How far a shot of hers lights (and burns): a plain shot's ShotReach, × √ its share of one's damage.</summary>
    public static float ShotLightReach(PlaneShot shot) =>
        HerLightTuning.ShotReach * Math.Clamp(MathF.Sqrt(MathF.Max(shot.Damage, 0f) / PlaneCombatTuning.ShotDamage), 0.55f, 2.6f);

    /// <summary>Her shots burning something this step (the view sears the bodies they light).</summary>
    public IReadOnlyList<PlaneShot> Burners => _burners;

    /// <summary>Her beam is burning something this step (the view sears the bodies it lights).</summary>
    public bool BeamBurns { get; private set; }

    /// <summary>Whether this creature is in a shot's light now (burning, unless warded).</summary>
    public bool InHerLight(object creature) => _glare.TryGetValue(creature, out var g) && g.Lit;

    /// <summary>Where the nearest shot lighting this creature is (on her plane), if one does.</summary>
    public Vector2? LitFrom(object creature) => _glare.TryGetValue(creature, out var g) && g.Lit ? g.From : null;

    /// <summary>Whether this creature is dazzled now (it holds still).</summary>
    public bool Dazzled(object creature) => _glare.TryGetValue(creature, out var g) && g.Frozen > 0f;

    /// <summary>A creature's knock held at least at the light pressure on it (the knock eases off; the pressure is steady).</summary>
    static void Press(ref Vector2 knock, Vector2 pressure)
    {
        if (pressure == Vector2.Zero) return;
        float along = Vector2.Dot(knock, Vector2.Normalize(pressure));
        if (along < pressure.Length()) knock += Vector2.Normalize(pressure) * (pressure.Length() - MathF.Max(along, 0f));
    }

    /// <summary>Her shots' light on the corrupted in it: each one burns and, coming into it, is dazzled.</summary>
    void StepHerLight()
    {
        _burners.Clear();
        _lights.Clear();
        BeamBurns = false;
        GatherBodies();
        StepBeam();
        StepArcs();
        // Pinball Storm's arcs off her glints, and her pools of light (PlaneOpenPaths.cs).
        GlintArcs();
        StepPools();
        if (!LightBurns)
        {
            _glare.Clear();
            return;
        }
        foreach (var s in Shots)
            if (s.FromPlayer && !s.BossPearl && s.Life > 0f) _lights.Add(s);
        // (Molten Carapace: under half health her light burns the hotter, and runs on through bodies: GatherBodies.)
        float burn = HerLightTuning.Burn * DamageScale * (Molten ? CrabTuning.Molten : 1f) * Dt;
        for (int i = 0; i < Murklings.Count; i++)
        {
            var m = Murklings[i];
            if (!m.Alive || m.Budding || m.Clinging) continue;
            float n = Touch(m, m.Position, 0f, m.Radius);
            // Her knockback is her beam's light pressure: it is pushed away from her while it is in it (held steady
            // against the knock's easing off).
            Press(ref m.Knock, LightPressure(m, m.Position, m.Radius));
            if (n == 0 || Guarded(m.Position) || !m.Corruption.Sear(burn * n)) continue;
            Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, m.Position, Vector2.Zero, burn));
            BurstMurkling(m, ember: true);
            FreedInColour(m, m.Position);
        }
        for (int i = 0; i < Inkwardens.Count; i++)
        {
            var w = Inkwardens[i];
            if (!w.Alive) continue;
            float n = Touch(w, w.Position, 0f, WardenTuning.Radius);
            if (n == 0 || !w.Corruption.Sear(burn * n)) continue;
            Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, w.Position, Vector2.Zero, burn));
            FreeWarden(w);
            FreedInColour(w, w.Position);
        }
        foreach (var f in Flounders)
        {
            // A Hearthguard lies on its bed under her plane: a shot's light finds it as it passes over it.
            if (!f.Corrupted || f.Freed) continue;
            float n = Touch(f, f.Position, FlounderTuning.Bed(Map, f.Home) - LevelMap.SwimBand, FlounderTuning.Radius);
            if (n == 0 || Guarded(f.Position) || !f.Corruption.Sear(burn * n)) continue;
            Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, FlounderSeen(f), Vector2.Zero, burn));
            FreeFlounder(f);
            FreedInColour(f, f.Position);
        }
        if (Corruption is { } field)
        {
            for (int i = 0; i < Blotters.Count; i++)
            {
                var b = Blotters[i];
                if (!b.Alive || b.Rise > 0f) continue;
                float n = Touch(b, b.Position, 0f, BlotterTuning.Radius, dazzles: false);
                Press(ref b.Knock, LightPressure(b, b.Position, BlotterTuning.Radius));
                if (n == 0 || Guarded(b.Position) || !b.Corruption.Sear(burn * n)) continue;
                Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, b.Position, Vector2.Zero, burn));
                BurstBlotter(b, field);
                FreedInColour(b, b.Position);
            }
            for (int i = 0; i < Thumpsquirts.Count; i++)
            {
                var t = Thumpsquirts[i];
                if (!t.Alive || t.Airborne) continue;
                float n = Touch(t, t.Position, 0f, ThumpTuning.Radius, dazzles: false);
                if (n == 0 || Guarded(t.Position) || !t.Corruption.Sear(burn * n)) continue;
                Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, t.Position, Vector2.Zero, burn));
                BurstThump(t);
                FreedInColour(t, t.Position);
            }
            foreach (var l in Lashjaws)
            {
                if (!l.Alive) continue;
                float n = Touch(l, l.Position, 0f, LashTuning.Head, dazzles: false);
                if (n == 0 || Guarded(l.Position) || !l.Corruption.Sear(burn * n)) continue;
                if (l.Phase == 2) Events.Add(new PlaneEvent(PlaneEventType.MobDefeated, l.Position, Vector2.Zero, burn));
                BreakLashjaw(l);
            }
            // An emptied Blightroot bursts on its own next step (its sac stands high on its pole).
            foreach (var root in Blightroots)
                if (root.Alive && Touch(root, root.Position, 1.9f, BlightTuning.SacRadius, dazzles: false) is var n and > 0 && !Guarded(root.Position))
                    root.Corruption.Sear(burn * n);
        }
        // A boss burns only where it lies open to the light (its pearl, or Drippus Prime's whole body), by its own rule for
        // her beam (PlaneBossLight.cs), and is never dazzled.
        if (Boss is { Stage: BossStage.Fight } boss) StepBossLight(boss, burn);
        else if (Boss is { } after) StepLamps(after);
        // Her colours' bursts (sunfire, shatter), once every creature has had its turn.
        StepColourBursts();
        // The echoes of what her light freed (flares, spines).
        StepEchoes();
        // Creatures gone (freed, or the level left) are forgotten.
        if (Tick % 60 == 0)
        {
            var gone = new List<object>();
            foreach (var (kin, g) in _glare)
                if (Tick - g.Seen > 60) gone.Add(kin);
            foreach (var kin in gone) _glare.Remove(kin);
            // Kept in the order they came (a removal leaves a gap the next one would take): a saved level restored lists
            // them the same way.
            if (gone.Count > 0)
            {
                var kept = _glare.ToList();
                _glare.Clear();
                foreach (var (kin, g) in kept) _glare[kin] = g;
            }
        }
    }

    /// <summary>
    /// How many of her shots light this creature (on her plane at <paramref name="at"/>, <paramref name="height"/> metres
    /// above it, this radius): some of its body within a shot's light, in sight of the shot. Dazzles it as it comes in.
    /// </summary>
    float Touch(object kin, Vector2 at, float height, float radius, bool dazzles = true, bool beams = true)
    {
        float n = 0f;
        Vector2 from = default;
        float nearest = float.MaxValue;
        if (!Defeated)
            foreach (var s in _lights)
            {
                var to = new Vector3(at.X - s.Position.X, height, at.Y - s.Position.Y);
                float d = to.Length();
                if (d - radius > ShotLightReach(s) || !LineOfSight(s.Position, at)) continue;
                n++;
                if (!_burners.Contains(s)) _burners.Add(s);
                if (d < nearest)
                {
                    nearest = d;
                    from = s.Position;
                }
            }
        bool beamed = false;
        var hue = LightHue.None;
        if (!Defeated && beams)
            foreach (var (beam, worth) in HerBeams)
            {
                // As much as her beam's light reaches it: more near her, none past a body in the way.
                float light = BeamLight(beam, at, height, radius, kin);
                if (light <= 0f)
                {
                    // Black Sun: the shadow of a body in its way burns it as hard (unseen, colourless).
                    float shade = ShadeLight(beam, at, height, radius, kin);
                    if (shade > 0f)
                    {
                        n += worth * shade;
                        if (from == default) from = beam.Position;
                    }
                    continue;
                }
                n += worth * light;
                // In the colour her pearls give that ray, and the prism stones it passed through (mixed on it with the others').
                hue |= RayHue(beam) | HueAlong(beam.Position, at);
                beamed = true;
                BeamBurns = true;
                float d = Vector2.Distance(beam.Position, at);
                if (d < nearest)
                {
                    nearest = d;
                    from = beam.Position;
                }
            }
        // Her pools of light burn on what stands in them, in their colours (Linger).
        if (!Defeated && beams && Pools.Count > 0)
        {
            float pooled = PoolsOn(at, radius, ref hue);
            if (pooled > 0f)
            {
                n += pooled;
                if (from == default) from = at;
            }
        }
        // Her fireflies burn a little, in her colours; her glow lends her colours to what is near her (PlaneFireflies.cs).
        if (!Defeated && beams)
        {
            if (Fireflies.Count > 0)
            {
                float lit = FirefliesOn(at, radius, ref hue);
                if (lit > 0f)
                {
                    n += lit;
                    if (from == default) from = at;
                }
            }
            hue |= GlowHueAt(at, radius);
        }
        if (!_glare.TryGetValue(kin, out var g)) _glare[kin] = g = new Glare();
        g.Seen = Tick;
        if (beamed)
        {
            _beamLit.Add((kin, at));
            // Each whole second her beam keeps burning it, her chance pearls roll on it.
            int was = (int)g.BeamTime;
            g.BeamTime += Dt;
            if ((int)g.BeamTime > was) RollBeamPearls(g);
        }
        else g.BeamTime = 0f;
        // Electric Eel Tail: burned by an arc off what her beam burns, at half the beam's rate.
        if (!beamed && _arced.Contains(kin)) n += HerLightTuning.BeamShots / 2f * BeamTick;
        // Fire Coral, Sea Nettle: it burns on a while out of her light.
        if (n == 0 && g.Afterglow > 0f && !Defeated) n = 1;
        if (n > 0f && g.Crit > 0f) n *= MathF.Max(Run.Loadout.Shot.CritMult, 1f);
        g.Afterglow = MathF.Max(0f, g.Afterglow - Dt);
        g.Slow = MathF.Max(0f, g.Slow - Dt);
        g.Charm = MathF.Max(0f, g.Charm - Dt);
        g.Crit = MathF.Max(0f, g.Crit - Dt);
        // Her colours on it (a charge stone lit in colour lends its colour to what is in its light).
        if (!Defeated && ChargeStones.Count > 0) hue |= LentAt(at);
        ApplyColour(g, hue, at);
        if (hue == LightHue.White && n > 0f) n *= ColourTuning.WhiteBurn;
        if (from == default && n > 0) from = at;
        g.Lit = n > 0;
        g.From = from;
        g.Frozen = MathF.Max(0f, g.Frozen - Dt);
        if (n == 0)
        {
            g.Dark += Dt;
            return 0;
        }
        if (dazzles && g.Dark >= HerLightTuning.Rearm)
        {
            g.Frozen = HerLightTuning.Dazzle;
            Events.Add(new PlaneEvent(PlaneEventType.Dazzled, at, SafeNormalize(at - from), radius));
        }
        g.Dark = 0f;
        return n;
    }

    /// <summary>
    /// Her beam on what is not a creature: the seal its axis falls on gains BeamShots shots' light a second (a coloured
    /// seal only from light of its colour: her pearls' or the prism stones'), and the ink dots along its axis clear (bloom:
    /// a wider swath; the ash it lights clears in StepAsh).
    /// </summary>
    void StepBeam()
    {
        if (!BeamOn) return;
        // Along each ray of it (a split beam's each strikes and clears where it falls), at its worth.
        foreach (var (ray, worth) in HerBeams)
        {
            if (!ray.Shining || ray == RearBeam || ray == SweepBeam) continue;
            var from = ray.Position;
            var end = ray == Beam ? BeamEnd : StopOf(ray);
            // (In the colour the prism stones on its way give it.)
            // (In its pearls' colour and the colour the prism stones on its way give it.)
            var hue = RayHue(ray) | HueAlong(from, end);
            if (_rocks.Exists(r => r.Contains(end))) StrikeSeal(end, worth * Dt * BeamFalloff(ray, Vector2.Distance(from, end)), quiet: true, hue: hue);
            // (Bloom clears a wider swath of ink.)
            float bloom = (hue & LightHue.Green) != 0 ? ColourTuning.Bloom(ColourTuning.BloomClear, Run.Loadout.Shot.Bloom) : 1f;
            if (Dots is not null) ClearAlong(from, end, HerLightTuning.BeamDots * bloom);
        }
    }

    /// <summary>
    /// Her chance pearls on a creature her beam has burned another whole second (§5.1): Frost Kelp holds it still,
    /// Fire Coral and Sea Nettle leave it burning on, Stingray Barb slows it, Siren Song charms it (it does not see her),
    /// Giant Squid Eye doubles down (her burn × CritMult for a second). Rolled from the level's own dice.
    /// </summary>
    void RollBeamPearls(Glare g)
    {
        var s = Run.Loadout.Shot;
        if (s.FreezeChance + s.BurnChance + s.PoisonChance + s.SlowChance + s.CharmChance + s.CritChance <= 0f) return;
        var rng = _beamRng ??= new Rng(Map.Seed ^ 0xBEA3C0DEUL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 48));
        if (s.FreezeChance > 0f && rng.NextFloat() < s.FreezeChance) g.Frozen = MathF.Max(g.Frozen, StatusRules.FreezeTime);
        if (s.BurnChance > 0f && rng.NextFloat() < s.BurnChance) g.Afterglow = MathF.Max(g.Afterglow, StatusRules.BurnTime);
        if (s.PoisonChance > 0f && rng.NextFloat() < s.PoisonChance) g.Afterglow = MathF.Max(g.Afterglow, StatusRules.PoisonTime);
        if (s.SlowChance > 0f && rng.NextFloat() < s.SlowChance) g.Slow = MathF.Max(g.Slow, StatusRules.SlowTime);
        if (s.CharmChance > 0f && rng.NextFloat() < s.CharmChance) g.Charm = MathF.Max(g.Charm, StatusRules.CharmTime);
        if (s.CritChance > 0f && rng.NextFloat() < s.CritChance) g.Crit = 1f;
    }

    /// <summary>
    /// Electric Eel Tail (§5.1): from each creature her beam burned last step, an arc to the ChainTargets nearest other
    /// corrupted creatures within ChainRange (in sight of it), which burn at half the beam's rate this step.
    /// </summary>
    void StepArcs()
    {
        _arced.Clear();
        _arcs.Clear();
        _beamLitBefore.Clear();
        _beamLitBefore.AddRange(_beamLit);
        _beamLit.Clear();
        var s = Run.Loadout.Shot;
        if (s.ChainTargets <= 0 || _beamLitBefore.Count == 0) return;
        var near = new List<(object Kin, Vector2 At, float D)>();
        foreach (var (src, at) in _beamLitBefore)
        {
            near.Clear();
            foreach (var (kin, pos) in CorruptedNow())
            {
                if (ReferenceEquals(kin, src) || _beamLitBefore.Exists(x => ReferenceEquals(x.Kin, kin))) continue;
                float d = Vector2.Distance(pos, at);
                if (d <= s.ChainRange && LineOfSight(at, pos)) near.Add((kin, pos, d));
            }
            near.Sort((a, b) => a.D.CompareTo(b.D));
            for (int i = 0; i < near.Count && i < s.ChainTargets; i++)
            {
                _arced.Add(near[i].Kin);
                _arcs.Add((at, near[i].At));
            }
        }
    }

    /// <summary>The corrupted creatures her light can burn now, and where.</summary>
    IEnumerable<(object Kin, Vector2 At)> CorruptedNow()
    {
        foreach (var m in Murklings)
            if (m.Alive && !m.Budding && !m.Clinging) yield return (m, m.Position);
        foreach (var w in Inkwardens)
            if (w.Alive) yield return (w, w.Position);
        foreach (var b in Blotters)
            if (b.Alive && b.Rise <= 0f) yield return (b, b.Position);
        foreach (var t in Thumpsquirts)
            if (t.Alive && !t.Airborne) yield return (t, t.Position);
        foreach (var l in Lashjaws)
            if (l.Alive) yield return (l, l.Position);
    }

    /// <summary>An Inkwarden guards this spot: her light's burn is turned aside there (silently, as it is steady).</summary>
    bool Guarded(Vector2 at)
    {
        foreach (var w in Inkwardens)
            if (w.Alive && Vector2.Distance(w.Position, at) <= WardenTuning.Reach) return true;
        return false;
    }
}
