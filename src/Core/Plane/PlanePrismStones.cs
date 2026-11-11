using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Prism stones (docs/LIGHT-AND-GRAPH.md §2.6): great standing stones, each in a bed of sand before a sealed
/// wall whose X is carved in a colour. She pushes one by swimming into it (heavy: it gives slowly, and never leaves its
/// bed). Her light passes through a stone and comes out of it in the stone's colour, red, green or blue; through more
/// than one it takes on each one's colour as light mixes (red and blue: magenta; all three: white). A coloured seal takes
/// light only of its own colour: her beam must come through the right stones to it. First guesses, tuned in play.
/// </summary>
public static class PrismStoneTuning
{
    /// <summary>A stone's body (she meets it this far from its middle) and its lens (light within this of its middle goes through it).</summary>
    public const float Radius = 0.85f, Lens = 0.8f;
    /// <summary>Its bed: a round of sand this wide (the stones never leave it), its middle between these far from the seal's carving.</summary>
    public const float Bed = 3.4f, BedNear = 4f, BedFar = 12f;
    /// <summary>How fast it gives at most (m/s) as she pushes it, and how much of her push it takes off her.</summary>
    public const float PushSpeed = 1.6f, Heavy = 0.6f;
    /// <summary>The most coloured seals a level has, and the chance each secret seal is one.</summary>
    public const int MostSeals = 2;
    public const float SealChance = 0.85f;
    /// <summary>The chance its bed holds one stone more than it needs, of a colour it does not (to choose among).</summary>
    public const float DecoyChance = 0.6f;
}

/// <summary>The colours of her light through prism stones, each a share of white (red, green, blue) mixed as light mixes.</summary>
[Flags]
public enum LightHue : byte
{
    None = 0, Red = 1, Green = 2, Blue = 4,
    Yellow = Red | Green, Magenta = Red | Blue, Cyan = Green | Blue, White = Red | Green | Blue,
}

/// <summary>A prism stone: where it stands and its bed (it never leaves it), its colour, and how much light goes through it now.</summary>
public sealed class PrismStone
{
    public Vector2 Position, Bed;
    public LightHue Hue;
    /// <summary>The seal it is laid for.</summary>
    public int Seal;
    /// <summary>How much of her light is going through it (0..1, eased; drawn).</summary>
    public float Lit;
    public float Seed;
}

public sealed partial class PlaneWorld
{
    public List<PrismStone> PrismStones { get; } = new();

    /// <summary>The colour a line of her light has at its end, from <paramref name="from"/> to <paramref name="to"/>: each stone it goes through mixed in.</summary>
    public LightHue HueAlong(Vector2 from, Vector2 to)
    {
        var hue = LightHue.None;
        if (PrismStones.Count == 0) return hue;
        var line = to - from;
        float len = line.Length();
        if (len < 1e-3f) return hue;
        var dir = line / len;
        foreach (var s in PrismStones)
        {
            var at = s.Position - from;
            float along = Vector2.Dot(at, dir);
            if (along <= 0f || along >= len) continue;
            if (MathF.Abs(at.X * dir.Y - at.Y * dir.X) < PrismStoneTuning.Lens) hue |= s.Hue;
        }
        return hue;
    }

    /// <summary>
    /// Where a line of her light goes into and comes out of each stone it goes through, in order, and its colour from
    /// there on (the stones before mixed in), and how far off the stone's middle it went through (0 its middle, 1 its
    /// rim): for drawing it.
    /// </summary>
    public void HuesAlong(Vector2 from, Vector2 dir, float len, List<(float In, float Out, LightHue Hue, float Across)> into)
    {
        into.Clear();
        if (PrismStones.Count == 0) return;
        foreach (var s in PrismStones)
        {
            var at = s.Position - from;
            float along = Vector2.Dot(at, dir);
            float across = MathF.Abs(at.X * dir.Y - at.Y * dir.X);
            if (along <= 0f || across >= PrismStoneTuning.Lens) continue;
            float half = MathF.Sqrt(PrismStoneTuning.Lens * PrismStoneTuning.Lens - across * across);
            if (along - half >= len) continue;
            into.Add((MathF.Max(along - half, 0f), MathF.Min(along + half, len), s.Hue, across / PrismStoneTuning.Lens));
        }
        into.Sort((a, b) => a.In.CompareTo(b.In));
        var hue = LightHue.None;
        for (int i = 0; i < into.Count; i++)
        {
            hue |= into[i].Hue;
            into[i] = (into[i].In, into[i].Out, hue, into[i].Across);
        }
    }

    /// <summary>
    /// Some of the secret rooms' seals take only coloured light (up to MostSeals a level): each gets a colour, and a bed of
    /// sand out before it with the stones that make it (and now and then one more, of another colour).
    /// </summary>
    void PlacePrismStones()
    {
        var rng = new Rng(Map.Seed ^ 0x9215A570UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Id.Lane << 40) ^ ((ulong)Map.Attempt << 50));
        int coloured = 0;
        var beds = new List<Vector2>();
        for (int i = 0; i < Seals.Count && coloured < PrismStoneTuning.MostSeals; i++)
        {
            var seal = Seals[i];
            if (seal.Kind != SealKind.Secret || seal.Rock.Fake || seal.Open) continue;
            if (rng.NextFloat() >= PrismStoneTuning.SealChance) continue;
            if (FindBed(seal, beds) is not { } bed) continue;
            // Mostly one colour; now and then two mixed; seldom white (all three).
            float roll = rng.NextFloat();
            var hue = roll < 0.6f ? (LightHue)(1 << rng.Int(3)) : roll < 0.92f ? (LightHue)(7 & ~(1 << rng.Int(3))) : LightHue.White;
            var stones = new List<LightHue>();
            foreach (var one in new[] { LightHue.Red, LightHue.Green, LightHue.Blue })
                if ((hue & one) != 0) stones.Add(one);
            if (hue != LightHue.White && rng.NextFloat() < PrismStoneTuning.DecoyChance)
            {
                var others = new List<LightHue>();
                foreach (var one in new[] { LightHue.Red, LightHue.Green, LightHue.Blue })
                    if ((hue & one) == 0) others.Add(one);
                stones.Add(others[rng.Int(others.Count)]);
            }
            var placed = new List<PrismStone>();
            foreach (var one in stones)
            {
                for (int tries = 0; tries < 60; tries++)
                {
                    float a = rng.Range(0f, MathF.Tau), r = MathF.Sqrt(rng.NextFloat()) * (PrismStoneTuning.Bed - PrismStoneTuning.Radius - 0.3f);
                    var p = bed + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                    if (!Clear(p, PrismStoneTuning.Radius) || placed.Exists(o => Vector2.Distance(o.Position, p) < PrismStoneTuning.Radius * 2f + 0.8f)) continue;
                    placed.Add(new PrismStone { Position = p, Bed = bed, Hue = one, Seal = i, Seed = rng.NextFloat() });
                    break;
                }
            }
            if (placed.Count < stones.Count) continue;
            seal.Hue = hue;
            PrismStones.AddRange(placed);
            beds.Add(bed);
            coloured++;
        }
    }

    /// <summary>
    /// A bed of open sand on the seal's side of it, BedNear to BedFar from its carving and in sight of it, away from the
    /// other beds, the shafts and her start: the most open such place (at least half of it open water a stone fits in),
    /// the farther the better (room to stand behind the stones).
    /// </summary>
    Vector2? FindBed(SecretSeal seal, List<Vector2> beds)
    {
        Vector2? best = null;
        float bestScore = 0f;
        for (float out_ = PrismStoneTuning.BedNear; out_ <= PrismStoneTuning.BedFar; out_ += 1f)
            for (int k = 0; k < 24; k++)
            {
                float a = k * MathF.Tau / 24f;
                var c = seal.Emblem + new Vector2(MathF.Cos(a), MathF.Sin(a)) * out_;
                if (Vector2.Dot(c - seal.Emblem, seal.Facing) < 1f || !Clear(c, PrismStoneTuning.Radius)) continue;
                if (Map.InShaft(c, PrismStoneTuning.Bed + 1f) || beds.Exists(b => Vector2.Distance(b, c) < 10f)) continue;
                if (Vector2.Distance(c, Map.Start.Position) < PrismStoneTuning.Bed + 2f) continue;
                if (!LineOfSight(c, seal.Emblem + seal.Facing * 0.4f)) continue;
                int open = 0, all = 0;
                for (float x = -1f; x <= 1f; x += 0.5f)
                    for (float y = -1f; y <= 1f; y += 0.5f)
                    {
                        if (x * x + y * y > 1f) continue;
                        all++;
                        if (Clear(c + new Vector2(x, y) * (PrismStoneTuning.Bed - PrismStoneTuning.Radius), PrismStoneTuning.Radius)) open++;
                    }
                float share = open / (float)all;
                if (share < 0.5f) continue;
                float score = share + out_ * 0.03f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = c;
            }
        return best;
    }

    /// <summary>
    /// She pushes the stones she swims into (each gives at most PushSpeed, taking Heavy of her push off her, and never
    /// leaves its bed or goes into rock or another stone); and how much of her light goes through each.
    /// </summary>
    void StepPrismStones()
    {
        if (PrismStones.Count == 0) return;
        var p = Player;
        float reach = Radius + PrismStoneTuning.Radius;
        foreach (var s in PrismStones)
        {
            var d = s.Position - p.Position;
            float dist = d.Length();
            if (dist < reach && dist > 1e-4f)
            {
                var n = d / dist;
                float overlap = reach - dist;
                float give = MathF.Min(overlap, PrismStoneTuning.PushSpeed * Dt);
                var next = s.Position + n * give;
                bool free = Vector2.Distance(next, s.Bed) <= PrismStoneTuning.Bed - PrismStoneTuning.Radius * 0.5f && Clear(next, PrismStoneTuning.Radius * 0.9f)
                    && !PrismStones.Exists(o => o != s && Vector2.Distance(o.Position, next) < PrismStoneTuning.Radius * 2f);
                if (free) s.Position = next;
                else give = 0f;
                // What it does not give pushes her back; and it is heavy: it takes a share of her way into it.
                p.Position -= n * (overlap - give);
                float into = Vector2.Dot(p.Velocity, n);
                if (into > 0f) p.Velocity -= n * into * (free ? PrismStoneTuning.Heavy : 1f);
            }
            bool lit = false;
            if (BeamOn && !Defeated)
                foreach (var (beam, _) in HerBeams)
                    if (beam.Shining && BeamLight(beam, s.Position, 0f, PrismStoneTuning.Lens) > 0f)
                    {
                        lit = true;
                        break;
                    }
            s.Lit = MathF.Max(0f, MathF.Min(1f, s.Lit + (lit ? 6f : -3f) * Dt));
        }
    }
}
