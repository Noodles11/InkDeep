using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>Bubble vaults (docs/proposals/CROSSOVER.md §5.1): first guesses, tuned in play.</summary>
public static class VaultTuning
{
    /// <summary>How often a vault mark holds a vault, and how many a level holds at most.</summary>
    public const float Taken = 0.7f;
    public const int Most = 6;
    /// <summary>A vault's bubble on the swim plane, and the rock it keeps to (it hangs in the lee of a wall).</summary>
    public const float Radius = 0.85f, WallSearch = 3.5f + OctoShoots.Core.Gen.TopDown.CaveLayout.Widen, WallGap = 0.12f;
    /// <summary>Kept clear of the start.</summary>
    public const float StartClear = 8f;
    /// <summary>Her beam's light it takes to burst (about a second of her beam); it settles back without it.</summary>
    public const float BurstLight = 2.5f, Settle = 0.5f;
    /// <summary>What one holds: 2–4 shells (rolled as a vault's: ShellWorth.Vault), a healing orb this often, a pearl this often.</summary>
    public const int ShellsMin = 2, ShellsMax = 4;
    public const float OrbChance = 0.4f, PearlChance = 0.08f;
}

/// <summary>
/// A bubble vault: a big air bubble caught against the rock, holding shells, a healing orb, now and then a pearl. Her
/// light shows what is inside (the view, within her aura: Seen); her beam bursts it, and what it held tumbles out.
/// It is attached to the rock: nothing sweeps it away.
/// </summary>
public sealed class PlaneVault
{
    public Vector2 Position;
    public float Radius = VaultTuning.Radius;
    /// <summary>Toward the rock it hangs against.</summary>
    public Vector2 Wall;
    public readonly List<ShellKind> Shells = new();
    public int Orbs;
    /// <summary>The pearl it holds, if any.</summary>
    public string? PearlId;
    /// <summary>Her beam's light on it so far (it bursts at VaultTuning.BurstLight).</summary>
    public float Light;
    public bool Burst;
}

public sealed partial class PlaneWorld
{
    public List<PlaneVault> Vaults { get; } = new();
    Rng? _vaultRng;

    /// <summary>A template level's bubble vaults, on its vault marks (LevelMap.VaultMarks), each snug against its wall.</summary>
    void PlaceVaults()
    {
        _vaultRng ??= new Rng(Map.Seed ^ 0xB0BBA17UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var rng = _areaRng ?? _vaultRng;
        // (An open level's chunk has no marks: spots by its rock, as many as its share.)
        var marks = Map.VaultMarks;
        int most = VaultTuning.Most, had = Vaults.Count;
        if (_area is not null)
        {
            marks = new List<Vector2>();
            foreach (var q in GridSpots(2f, 2f))
                if (Clear(q, 1f) && !Clear(q, 2.2f) && !Map.InShaft(q, 2f)) marks.Add(q);
            Shuffle(marks, rng);
            most = AreaShare((int)(VaultTuning.Most / VaultTuning.Taken), rng);
        }
        foreach (var mark in marks)
        {
            if (Vaults.Count - had >= most) break;
            if (rng.NextFloat() >= VaultTuning.Taken) continue;
            if (Vector2.Distance(mark, FeatureStart) < VaultTuning.StartClear || _area is not null && Vaults.Exists(v => Vector2.Distance(v.Position, mark) < 6f)) continue;
            // The nearest rock, by sixteen ways: the vault hangs against it.
            Vector2 wall = Vector2.Zero;
            float near = float.MaxValue;
            for (int k = 0; k < 16; k++)
            {
                var dir = new Vector2(MathF.Cos(k * MathF.Tau / 16f), MathF.Sin(k * MathF.Tau / 16f));
                for (float d = 0.25f; d <= VaultTuning.WallSearch; d += 0.25f)
                {
                    if (Map.IsOpen(mark + dir * d)) continue;
                    if (d < near)
                    {
                        near = d;
                        wall = dir;
                    }
                    break;
                }
            }
            if (wall == Vector2.Zero) continue;
            // (Against the rock, or a little off it where a corner crowds it; never left hanging in open water.)
            var at = mark + wall * (near - VaultTuning.Radius - VaultTuning.WallGap);
            if (!Clear(at, VaultTuning.Radius * 0.8f)) at -= wall * 0.3f;
            if (!Clear(at, VaultTuning.Radius * 0.8f)) continue;
            var vault = new PlaneVault { Position = at, Wall = wall };
            int shells = VaultTuning.ShellsMin + rng.Int(VaultTuning.ShellsMax - VaultTuning.ShellsMin + 1);
            for (int i = 0; i < shells; i++) vault.Shells.Add(ShellWorth.Roll(rng, ShellWorth.Vault, Map.Menace));
            if (rng.NextFloat() < VaultTuning.OrbChance) vault.Orbs = 1;
            if (rng.NextFloat() < VaultTuning.PearlChance)
            {
                var offer = Run.Offer("secret", Pearls.Select(q => q.ItemId));
                if (offer.Count > 0) vault.PearlId = Run.Pick(offer, rng);
            }
            Vaults.Add(vault);
        }
    }

    /// <summary>Whether her light shows what a vault holds (within her aura's reach, or in her beam's light; nothing in the way).</summary>
    public bool Seen(PlaneVault vault) =>
        Aura.Shining && Vector2.Distance(vault.Position, Aura.Position) < Aura.Range && LineOfSight(Aura.Position, vault.Position)
        || BeamOn && BeamLight(Beam, vault.Position, 0f, vault.Radius, vault) > 0f;

    /// <summary>Her beams fill the vaults they reach with light till they burst; a vault out of the beam settles back. Her dash bursts one too.</summary>
    void StepVaults()
    {
        var p = Player;
        foreach (var vault in Vaults)
        {
            if (vault.Burst) continue;
            float light = 0f;
            if (!Defeated)
                foreach (var (beam, worth) in HerBeams)
                    light += worth * BeamLight(beam, vault.Position, 0f, vault.Radius, vault);
            vault.Light = light > 0f ? vault.Light + light * Dt : MathF.Max(0f, vault.Light - VaultTuning.Settle * Dt);
            bool rammed = p.IsDashing && Vector2.Distance(vault.Position, p.Position) < vault.Radius + Radius;
            // A shot of hers pops it at once (and goes out on it).
            PlaneShot? shot = null;
            foreach (var s in Shots)
                if (s.FromPlayer && !s.BossPearl && s.Life > 0f && Reaches(s, vault.Position, vault.Radius))
                {
                    shot = s;
                    break;
                }
            if (shot is not null) Pop(shot, vault.Position - shot.Position);
            if (vault.Light >= VaultTuning.BurstLight || rammed || shot is not null) BurstVault(vault);
        }
    }

    /// <summary>A vault bursts: what it held tumbles out, away from its wall.</summary>
    void BurstVault(PlaneVault vault)
    {
        vault.Burst = true;
        Events.Add(new PlaneEvent(PlaneEventType.VaultBurst, vault.Position, -vault.Wall, vault.Radius));
        Vector2 Out(float min, float max)
        {
            float a = MathF.Atan2(-vault.Wall.Y, -vault.Wall.X) + _vaultRng!.Range(-1.1f, 1.1f);
            return new Vector2(MathF.Cos(a), MathF.Sin(a)) * _vaultRng!.Range(min, max);
        }
        foreach (var kind in vault.Shells)
            Shells.Add(new PlaneShell { Position = vault.Position, Velocity = Out(2f, 4.5f), Kind = kind, Value = ShellWorth.Of(kind) });
        for (int i = 0; i < vault.Orbs; i++) Hearts.Add(new PlaneHeart { Position = vault.Position, Velocity = Out(1.5f, 2.5f) });
        if (vault.PearlId is { } pearl) Pearls.Add(new PlanePearl { ItemId = pearl, Position = vault.Position - vault.Wall * 0.4f });
    }
}
