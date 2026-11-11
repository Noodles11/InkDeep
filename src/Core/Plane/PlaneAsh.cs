using System;
using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A cave's corruption on its walls (docs/ADVENTURE.md §8): washed-purple flakes like ash, clinging to the rock like
/// scraps of torn wallpaper, in patches Spacing apart along every wall, at Layers heights from Low to High about her
/// plane. Her light clears a patch within its reach (HerLightTuning.Reach × √ her glow stat, in sight of her) in
/// LightClear seconds; a shot of hers clears what lies within ShotReach of where it pops (× √ its share of a plain
/// shot's damage). Sealing the cave's leak dissolves what is left in a wave out from it at SealWave m/s. It lies thickest over a secret's sealed wall (and a crab's fake one): patches SealSpacing apart all over its face,
/// hiding the carving under them, so clearing the walls is how she finds the ways in.
/// </summary>
public static class AshTuning
{
    public const float Spacing = 0.9f, Low = -1.6f, High = 1.6f, SealSpacing = 0.45f;
    public const int Layers = 3;
    public const float LightClear = 0.6f, ShotReach = 0.9f, SealWave = 8f;
}

/// <summary>A patch of ash on a cave wall.</summary>
public sealed class AshPatch
{
    /// <summary>The open water just in front of it (for sight), the wall under it, its height over her plane, and the way the wall faces.</summary>
    public Vector2 At, Wall, Normal;
    public float Height;
    /// <summary>The seal whose wall it covers (an index into Seals; -1: a plain wall).</summary>
    public int Seal = -1;
    /// <summary>How much of it is left (1 → 0: cleared), and (sealed) the seconds until the wave reaches it (negative: none coming).</summary>
    public float Cover = 1f, Wave = -1f;
}

public sealed partial class PlaneWorld
{
    /// <summary>The ash on the cave's walls (none outside caves).</summary>
    public List<AshPatch> Ash { get; } = new();

    /// <summary>How much of a seal's wall the ash still hides (0 bare → 1 all of it; 0 where it never lay).</summary>
    public float SealAsh(int seal)
    {
        float sum = 0f;
        int n = 0;
        foreach (var a in Ash)
            if (a.Seal == seal)
            {
                sum += a.Cover;
                n++;
            }
        return n == 0 ? 0f : sum / n;
    }

    /// <summary>A cave's walls take their ash: a patch wherever open water meets rock, at each layer the wall rises through.</summary>
    void PlaceAsh()
    {
        if (!Map.LeakCave) return;
        var rng = new Rng(Map.Seed ^ 0xA5A5A5UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 28));
        float step = AshTuning.Spacing;
        for (float y = step * 0.5f; y < Map.Size; y += step)
        for (float x = step * 0.5f; x < Map.Size; x += step)
        {
            var p = new Vector2(x + rng.Range(-0.3f, 0.3f), y + rng.Range(-0.3f, 0.3f));
            if (!Map.IsOpen(p)) continue;
            bool wall = false;
            for (int k = 0; k < 8 && !wall; k++)
                wall = !Map.IsOpen(p + new Vector2(MathF.Cos(k * MathF.Tau / 8f), MathF.Sin(k * MathF.Tau / 8f)));
            if (!wall) continue;
            var into = SafeNormalize(Map.Gradient(p));
            if (into == Vector2.Zero) continue;
            for (int layer = 0; layer < AshTuning.Layers; layer++)
            {
                float h = AshTuning.Low + (AshTuning.High - AshTuning.Low) * (layer + rng.Range(0.1f, 0.9f)) / AshTuning.Layers;
                float level = LevelMap.SwimBand + h;
                // Not on the floor: only where the wall rises through this height in front of it.
                if (Map.HeightAt(p) >= level) continue;
                for (float s = 0.05f; s <= 1.6f; s += 0.05f)
                {
                    var q = p + into * s;
                    if (Map.HeightAt(q) < level) continue;
                    Ash.Add(new AshPatch { At = p, Wall = q - into * 0.03f, Normal = -into, Height = h });
                    break;
                }
            }
        }
        // Thickest over the sealed walls: all over the face she would meet each from.
        for (int i = 0; i < Seals.Count; i++)
        {
            var seal = Seals[i];
            if (seal.Open || seal.Kind == SealKind.Boss) continue;
            var across = new Vector2(-seal.Facing.Y, seal.Facing.X);
            float half = MathF.Max(seal.Rock.Radius, 1.2f);
            for (float o = -half; o <= half; o += AshTuning.SealSpacing)
            {
                // The face: out from its middle along the way she meets it, to where the wall gives onto open water.
                var line = seal.Rock.Center + across * o;
                var face = line;
                bool found = false;
                for (float d = 0f; d <= seal.Rock.Thick + seal.Rock.Radius + 3f; d += 0.05f)
                {
                    var q = line + seal.Facing * d;
                    if (seal.Rock.Contains(q, 0f)) continue;
                    face = q;
                    found = d > 0f;
                    break;
                }
                if (!found || !Map.IsOpen(face + seal.Facing * 0.6f)) continue;
                for (float h = AshTuning.Low; h <= AshTuning.High; h += AshTuning.SealSpacing * 1.6f)
                    Ash.Add(new AshPatch
                    {
                        At = face + seal.Facing * 0.6f, Wall = face + seal.Facing * 0.02f, Normal = seal.Facing,
                        Height = h + rng.Range(-0.1f, 0.1f), Seal = i,
                    });
            }
        }
    }

    /// <summary>Her light clears the ash within its reach; the sealed leak's wave dissolves the rest.</summary>
    void StepAsh()
    {
        if (Ash.Count == 0) return;
        var her = Player.Position;
        float reach = LightReach;
        foreach (var a in Ash)
        {
            if (a.Cover <= 0f) continue;
            if (a.Wave >= 0f)
            {
                a.Wave -= Dt;
                if (a.Wave < 0f) ClearPatch(a);
                continue;
            }
            // Her own light round her, or her beam falling on it.
            bool lit = !Defeated && (Vector2.DistanceSquared(a.Wall, her) + a.Height * a.Height <= reach * reach && LineOfSight(her, a.At)
                || BeamReaches(a.At, a.Height, 0.3f));
            if (!lit) continue;
            // (Bloom-coloured light clears it the faster.)
            a.Cover -= Dt / AshTuning.LightClear * (BloomReaches(a.At, a.Height) ? ColourTuning.Bloom(ColourTuning.BloomClear, Run.Loadout.Shot.Bloom) : 1f);
            if (a.Cover > 0f) continue;
            ClearPatch(a);
            AshWashed++;
        }
    }

    /// <summary>A shot of hers pops: the ash within its reach (on its plane) is cleared.</summary>
    void ClearAsh(PlaneShot shot)
    {
        if (Ash.Count == 0 || !shot.FromPlayer) return;
        float reach = AshTuning.ShotReach * Math.Clamp(MathF.Sqrt(MathF.Max(shot.Damage, 0f) / PlaneCombatTuning.ShotDamage), 0.55f, 2.6f);
        foreach (var a in Ash)
            if (a.Cover > 0f && Vector2.DistanceSquared(a.Wall, shot.Position) + a.Height * a.Height <= reach * reach)
            {
                ClearPatch(a);
                AshWashed++;
            }
    }

    /// <summary>The cave's leak is sealed: its ash dissolves in a wave out from it.</summary>
    void DissolveAsh(Vector2 from)
    {
        foreach (var a in Ash)
            if (a.Cover > 0f) a.Wave = Vector2.Distance(a.Wall, from) / AshTuning.SealWave;
    }

    void ClearPatch(AshPatch a)
    {
        a.Cover = 0f;
        a.Wave = -1f;
        Events.Add(new PlaneEvent(PlaneEventType.AshCleared, a.Wall, a.Normal, a.Height));
    }
}
