using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Plane;

/// <summary>
/// The reef's own animals that answer light (docs/proposals/LIGHT-WORLD.md §7, phase 5): not the Leak's, never her
/// target. Fleeing, following and striking are all driven by light, so she steers them with it.
/// <list type="bullet">
/// <item>glass shrimp (Reef, Kelp): a cluster that flees the brightest light on it and gathers home again in the dark;</item>
/// <item>a lantern-fish shoal (Kelp, Alien): a living blue light; lit by a steady or slow-pulsing light with blue in it, it
/// follows her a while (lighting the water as she goes), and a fast pulse scatters it;</item>
/// <item>a cuttlefish (Reef, Ruins): grey and hidden in the dark; lit, its skin takes the colour of the light on it and
/// glows it back, a light of its own the murklings go to;</item>
/// <item>a mantis shrimp (Reef, Temple): hidden in its burrow in the dark; lit, it comes out and strikes whatever comes
/// within reach, her (unless her Polariser Lens hides her light from it) or a murkling;</item>
/// <item>a firefly squid (Kelp, Temple): glows blue in the dark, flashing at its own beat; her light on it pulsing at that
/// beat (within FlashMatch), it leads her toward the level's secret (or a shut light room).</item>
/// </list>
/// </summary>
public enum FaunaKind { GlassShrimp, LanternShoal, Cuttlefish, MantisShrimp, FireflySquid }

public sealed class PlaneFauna : ILightOwner
{
    public FaunaKind Kind;
    public Vector2 Position, Velocity, Home;
    /// <summary>The light on it this step (every light but the animals' own), and hers alone.</summary>
    public float Lit, HerLit;
    /// <summary>A cuttlefish's colour (what it glows back), its glow (0..1); a mantis shrimp out of its burrow (0..1).</summary>
    public Vector3 Colour = new(0.55f, 0.55f, 0.55f);
    public float Glow, Out;
    /// <summary>Seconds left: a shoal following her, a shoal scattered, a squid leading, a mantis's strike again.</summary>
    public float Follow, Scatter, Lead, Cooldown;
    /// <summary>A squid's way it leads (where it is going); a mantis's last strike's mark.</summary>
    public Vector2 Toward;
    /// <summary>A mantis's last strike, seconds ago (the view's snap).</summary>
    public float Struck = 10f;
    public float Seed;
    public LightEmitter? Light;

    public float Energy => Light is { Active: true } ? 1f : 0f;
    public float Absorb(float amount) => MathF.Min(MathF.Max(amount, 0f), 0.05f);
}

/// <summary>The fauna's numbers (LIGHT-WORLD.md §7). (tune)</summary>
public static class FaunaTuning
{
    /// <summary>Glass shrimp: the light they flee, how fast they flee and drift home.</summary>
    public const float ShrimpSense = 0.2f, ShrimpFlee = 2.8f, ShrimpHome = 0.7f;
    /// <summary>A lantern shoal: the light of hers that draws it, its blue need (× her plain light's share), the slowest pulse that scatters it (Hz), how long it follows, how far behind her it keeps.</summary>
    public const float ShoalSense = 0.15f, ShoalBlue = 0.8f, ShoalScatterHz = 2.5f, ShoalFollow = 6f, ShoalBehind = 2.2f, ShoalSpeed = 3.2f;
    /// <summary>A shoal's own light (always on), its reach.</summary>
    public const float ShoalLight = 0.45f, ShoalReach = 4f;
    /// <summary>A cuttlefish: the light that colours it, how fast its glow comes and goes (/s), its light and reach.</summary>
    public const float CuttleSense = 0.2f, CuttleRise = 2f, CuttleFade = 0.5f, CuttleLight = 0.5f, CuttleReach = 4f;
    /// <summary>A mantis shrimp: the light that brings it out, its reach, its blow on her and on a murkling (a share of its meter), its rest between strikes.</summary>
    public const float MantisSense = 0.25f, MantisReach = 2.4f, MantisBlow = 10f, MantisMurk = 0.5f, MantisRest = 2f;
    /// <summary>A firefly squid: the dark it glows in, its flash's beat (Hz), how near her pulse must be to it, how long it leads, how far ahead it keeps, its light.</summary>
    public const float SquidDark = 0.1f, FlashHz = 1.5f, FlashMatch = 0.5f, SquidLead = 12f, SquidAhead = 3f, SquidSpeed = 2.4f, SquidLight = 0.5f, SquidReach = 3.5f;
    /// <summary>How far from her start they live, and from each other.</summary>
    public const float FromStart = 10f, Apart = 8f;

    /// <summary>A biome's animals: (kind, how many).</summary>
    public static (FaunaKind Kind, int Count)[] For(Biome biome) => biome switch
    {
        Biome.SunlitReef => new[] { (FaunaKind.GlassShrimp, 2), (FaunaKind.Cuttlefish, 1), (FaunaKind.MantisShrimp, 1) },
        Biome.KelpForest => new[] { (FaunaKind.GlassShrimp, 2), (FaunaKind.LanternShoal, 1), (FaunaKind.FireflySquid, 1) },
        Biome.SunkenRuins => new[] { (FaunaKind.Cuttlefish, 2) },
        Biome.AlienWreckage => new[] { (FaunaKind.LanternShoal, 2) },
        Biome.DrownedTemple => new[] { (FaunaKind.MantisShrimp, 2), (FaunaKind.FireflySquid, 1) },
        _ => Array.Empty<(FaunaKind, int)>(),
    };
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's animals that answer light.</summary>
    public List<PlaneFauna> Fauna { get; } = new();

    /// <summary>Her light's pulse this step (Hz; 0 steady): what a lantern shoal and a firefly squid read.</summary>
    public float HerPulseHz { get; private set; }

    /// <summary>Her light on a point (her beam's and her glow's): the view's too (a spring not yet found shows in it).</summary>
    public float HerLightOn(Vector2 at)
    {
        float sum = 0f;
        foreach (var e in Lights.All)
            if (e.Owner == Her) sum += LightQuery.Brightness(e, at, LineOfSight);
        return sum;
    }

    /// <summary>The level's animals (PlaneOptions.Fauna), in its open water by its own seed.</summary>
    void PlaceFauna()
    {
        var rng = _areaRng ?? new Rng(Map.Seed ^ 0xFA0AUL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var spots = new List<Vector2>();
        foreach (var p in GridSpots(4f, 3f))
            if (Clear(p, 1.5f) && !Map.InShaft(p) && Vector2.Distance(p, FeatureStart) >= FaunaTuning.FromStart) spots.Add(p);
        Shuffle(spots, rng);
        int next = 0;
        if (!HasFeature(LightFeature.Animals)) return;
        foreach (var (kind, count) in FaunaTuning.For(Map.Biome))
            for (int k = 0; k < AreaCount(count) && next < spots.Count; next++)
            {
                var p = spots[next];
                if (Fauna.Exists(o => Vector2.Distance(o.Home, p) < FaunaTuning.Apart) || !TidySpot(p)) continue;
                AddFauna(kind, p, rng.Range(0f, 100f));
                k++;
            }
    }

    /// <summary>An animal of a kind where it is asked (the level's own placing, and tests).</summary>
    public PlaneFauna AddFauna(FaunaKind kind, Vector2 at, float seed = 0f)
    {
        var f = new PlaneFauna { Kind = kind, Position = at, Home = at, Seed = seed };
        if (kind is FaunaKind.LanternShoal or FaunaKind.Cuttlefish or FaunaKind.FireflySquid)
            f.Light = Lights.Add(new LightEmitter
            {
                Owner = f, Position = at, Active = kind == FaunaKind.LanternShoal,
                Intensity = kind == FaunaKind.LanternShoal ? FaunaTuning.ShoalLight : 0f,
                Range = kind switch { FaunaKind.LanternShoal => FaunaTuning.ShoalReach, FaunaKind.Cuttlefish => FaunaTuning.CuttleReach, _ => FaunaTuning.SquidReach },
                Colour = kind == FaunaKind.Cuttlefish ? f.Colour : new Vector3(0.35f, 0.65f, 1f),
            });
        Fauna.Add(f);
        return f;
    }

    /// <summary>The animals' step: each reads the light on it and answers (LIGHT-WORLD.md §7).</summary>
    void StepFauna()
    {
        if (Fauna.Count == 0) return;
        var her = Player.Position;
        foreach (var f in Fauna)
        {
            // The light on it from every light but the animals' own, the brightest of them, and her share.
            float lit = 0f, most = 0f;
            var colour = Vector3.Zero;
            var brightest = f.Position;
            foreach (var e in Lights.All)
            {
                if (e.Owner is PlaneFauna) continue;
                float b = LightQuery.Brightness(e, f.Position, LineOfSight);
                if (b <= 0f) continue;
                lit += b;
                // (Her light in her spectrum's colour, as her beam is drawn.)
                colour += (e.Owner == Her ? HerSpectrum.Colour : e.Colour) * b;
                if (b > most)
                {
                    most = b;
                    brightest = e.Position;
                }
            }
            f.Lit = lit;
            f.HerLit = HerLightOn(f.Position);
            var want = Vector2.Zero;
            float speed = 0f;
            switch (f.Kind)
            {
                case FaunaKind.GlassShrimp:
                    if (lit >= FaunaTuning.ShrimpSense)
                    {
                        want = SafeNormalize(f.Position - brightest);
                        speed = FaunaTuning.ShrimpFlee;
                    }
                    else if (Vector2.Distance(f.Position, f.Home) > 0.5f)
                    {
                        want = SafeNormalize(f.Home - f.Position);
                        speed = FaunaTuning.ShrimpHome;
                    }
                    break;
                case FaunaKind.LanternShoal:
                    f.Follow = MathF.Max(0f, f.Follow - Dt);
                    f.Scatter = MathF.Max(0f, f.Scatter - Dt);
                    // (Tidy: its light shows only while it answers her: lit by her, or following.)
                    f.Light!.Active = !Options.Tidy || f.Follow > 0f || f.HerLit >= FaunaTuning.ShoalSense;
                    if (f.HerLit >= FaunaTuning.ShoalSense)
                    {
                        if (HerPulseHz >= FaunaTuning.ShoalScatterHz) (f.Scatter, f.Follow) = (2f, 0f);
                        else if (BandShare(LightHue.Blue) >= FaunaTuning.ShoalBlue && f.Scatter <= 0f) f.Follow = FaunaTuning.ShoalFollow;
                    }
                    if (f.Scatter > 0f)
                    {
                        want = SafeNormalize(f.Position - her);
                        speed = FaunaTuning.ShoalSpeed;
                    }
                    else if (f.Follow > 0f)
                    {
                        var spot = her + SafeNormalize(f.Position - her) * FaunaTuning.ShoalBehind;
                        want = SafeNormalize(spot - f.Position);
                        speed = FaunaTuning.ShoalSpeed * Math.Clamp(Vector2.Distance(spot, f.Position), 0f, 1f);
                    }
                    else
                    {
                        // Wandering round where it is (its home moves with it once it has followed her).
                        float a = Time * 0.4f + f.Seed;
                        want = SafeNormalize(f.Home + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 2f - f.Position);
                        speed = 0.6f;
                    }
                    break;
                case FaunaKind.Cuttlefish:
                    if (lit >= FaunaTuning.CuttleSense)
                    {
                        float most3 = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));
                        if (most3 > 1e-4f) f.Colour = Vector3.Lerp(f.Colour, colour / most3, MathF.Min(1f, 3f * Dt));
                        f.Glow = MathF.Min(1f, f.Glow + FaunaTuning.CuttleRise * Dt);
                    }
                    else f.Glow = MathF.Max(0f, f.Glow - FaunaTuning.CuttleFade * Dt);
                    f.Light!.Active = f.Glow > 0.05f;
                    f.Light.Intensity = FaunaTuning.CuttleLight * f.Glow;
                    f.Light.Colour = f.Colour;
                    {
                        float a = Time * 0.25f + f.Seed;
                        want = SafeNormalize(f.Home + new Vector2(MathF.Cos(a), MathF.Sin(a * 0.7f)) * 1.5f - f.Position);
                        speed = 0.4f;
                    }
                    break;
                case FaunaKind.MantisShrimp:
                    StepMantis(f, lit);
                    break;
                case FaunaKind.FireflySquid:
                    StepSquid(f, lit, her, out want, out speed);
                    break;
            }
            if (f.Kind == FaunaKind.MantisShrimp) continue;
            f.Velocity = Vector2.Lerp(f.Velocity, want * speed, MathF.Min(1f, 4f * Dt));
            var to = f.Position + f.Velocity * Dt;
            if (Clear(to, 0.3f)) f.Position = to;
            else f.Velocity = -f.Velocity * 0.3f;
            // (A shoal that followed her makes its home where it was left.)
            if (f.Kind == FaunaKind.LanternShoal && f.Follow > 0f) f.Home = f.Position;
            if (f.Light is { } l) l.Position = f.Position;
        }
    }

    /// <summary>A mantis shrimp: out of its burrow while lit, it strikes what comes within reach (her, or a murkling), resting after.</summary>
    void StepMantis(PlaneFauna f, float lit)
    {
        f.Out = lit >= FaunaTuning.MantisSense ? MathF.Min(1f, f.Out + 4f * Dt) : MathF.Max(0f, f.Out - 1f * Dt);
        f.Cooldown = MathF.Max(0f, f.Cooldown - Dt);
        f.Struck += Dt;
        if (f.Out < 0.9f || f.Cooldown > 0f) return;
        // Her, if her light is what brought it out and nothing hides it from her (the Polariser Lens).
        if (f.HerLit >= FaunaTuning.MantisSense * 0.5f && !Run.Loadout.Flags.Contains("polariser")
            && Vector2.Distance(f.Position, Player.Position) <= FaunaTuning.MantisReach + Radius)
        {
            HurtPlayer(FaunaTuning.MantisBlow, SafeNormalize(Player.Position - f.Position), DamageSource.Mantis);
            Strike(f, Player.Position);
            return;
        }
        foreach (var m in Murklings)
        {
            if (!m.Alive || m.Budding || m.Clinging || Vector2.Distance(f.Position, m.Position) > FaunaTuning.MantisReach + m.Radius) continue;
            bool emptied = m.Corruption.Hit(m.Corruption.Max * FaunaTuning.MantisMurk);
            Events.Add(new PlaneEvent(emptied ? PlaneEventType.MobDefeated : PlaneEventType.MobHit, m.Position, SafeNormalize(m.Position - f.Position), m.Corruption.LastHit, Health: m.Corruption.Max));
            if (emptied) BurstMurkling(m, ember: false);
            Strike(f, m.Position);
            return;
        }
    }

    void Strike(PlaneFauna f, Vector2 at)
    {
        f.Cooldown = FaunaTuning.MantisRest;
        f.Struck = 0f;
        f.Toward = at;
        Events.Add(new PlaneEvent(PlaneEventType.MantisStruck, f.Position, SafeNormalize(at - f.Position), FaunaTuning.MantisReach));
    }

    /// <summary>
    /// A firefly squid: in the dark it glows blue, flashing at its beat; her light on it pulsing at that beat sends it
    /// leading her toward the level's secret (else a shut light room), keeping a little ahead of her, waiting if she
    /// falls behind.
    /// </summary>
    void StepSquid(PlaneFauna f, float lit, Vector2 her, out Vector2 want, out float speed)
    {
        want = Vector2.Zero;
        speed = 0f;
        f.Lead = MathF.Max(0f, f.Lead - Dt);
        bool dark = lit <= FaunaTuning.SquidDark;
        f.Glow = dark || f.Lead > 0f ? MathF.Min(1f, f.Glow + Dt) : MathF.Max(0f, f.Glow - 2f * Dt);
        bool flash = MathF.Sin(Time * FaunaTuning.FlashHz * MathF.Tau + f.Seed) > 0f;
        f.Light!.Active = f.Glow > 0.05f;
        f.Light.Intensity = FaunaTuning.SquidLight * f.Glow * (flash ? 1f : 0.25f);
        if (f.Lead <= 0f && f.HerLit >= FaunaTuning.ShoalSense && MathF.Abs(HerPulseHz - FaunaTuning.FlashHz) <= FaunaTuning.FlashMatch)
        {
            var goal = LeadGoal(f.Position);
            if (goal is { } g)
            {
                f.Lead = FaunaTuning.SquidLead;
                f.Toward = g;
                Events.Add(new PlaneEvent(PlaneEventType.SquidLeads, f.Position, SafeNormalize(g - f.Position), FaunaTuning.SquidLead));
            }
        }
        if (f.Lead > 0f)
        {
            if (Vector2.Distance(f.Position, f.Toward) < 2f)
            {
                f.Lead = 0f;
                f.Home = f.Position;
                return;
            }
            // Ahead of her toward the goal; if she is far behind, it waits.
            if (Vector2.Distance(her, f.Position) > FaunaTuning.SquidAhead * 2.5f) return;
            var spot = her + SafeNormalize(f.Toward - her) * FaunaTuning.SquidAhead;
            if (Vector2.Distance(f.Toward, her) < FaunaTuning.SquidAhead) spot = f.Toward;
            want = SafeNormalize(spot - f.Position);
            speed = FaunaTuning.SquidSpeed;
            return;
        }
        float a = Time * 0.3f + f.Seed;
        want = SafeNormalize(f.Home + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 1.2f - f.Position);
        speed = 0.5f;
    }

    /// <summary>Where a squid leads: the secret she has not found, else the nearest shut light room.</summary>
    Vector2? LeadGoal(Vector2 from)
    {
        if (SecretHidden) return SecretWay;
        Vector2? best = null;
        foreach (var r in LightRooms)
            if (!r.Opened && (best is null || Vector2.Distance(from, r.Centre) < Vector2.Distance(from, best.Value))) best = r.Centre;
        return best;
    }
}
