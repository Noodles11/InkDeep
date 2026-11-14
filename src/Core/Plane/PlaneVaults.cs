using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>What a bubble vault holds.</summary>
public enum VaultPrize { Shells, Orb, Prank, Pearl }

/// <summary>
/// Bubble vaults (a surprise reward, the game's chest): a fragile shimmering bubble, a little bigger than a murkling,
/// floating where it was left. Her light fills it (her beam fully, her glow a little) and it brightens as it fills; full,
/// it pops and frees what it held: shells, a healing orb, or a prank (a blow that knocks her back), or a secret room's
/// reward packed inside it. Unlit, it settles back slowly. First guesses, tuned in play.
/// </summary>
public static class VaultTuning
{
    public const float Radius = 0.62f;
    /// <summary>Seconds of her whole beam on it to pop it; her glow, within its reach, fills it at this share.</summary>
    public const float FillSeconds = 1.6f, AuraShare = 0.3f;
    /// <summary>How fast an unlit vault settles back (its fill a second).</summary>
    public const float Settle = 0.35f;
    /// <summary>A vault's shells, at least and at most.</summary>
    public const int ShellsMin = 6, ShellsMax = 14;
    /// <summary>A shop's vault: its price, and its prize's odds (shells, an orb, else a prank).</summary>
    public const int Price = 20;
    public const float ShellsChance = 0.5f, OrbChance = 0.3f;
    /// <summary>The prank: the blow's speed and how long it tumbles her.</summary>
    public const float PrankPush = 15f, PrankTumble = 0.45f;
}

public sealed class PlaneVault
{
    public Vector2 Position;
    public VaultPrize Prize;
    public int Shells;
    /// <summary>A pearl packed inside (a secret room's).</summary>
    public string ItemId = "";
    /// <summary>How full of her light, 0..1 (popping at 1).</summary>
    public float Fill;
    public bool Popped;
    /// <summary>Its own phase (its bob and wobble).</summary>
    public float Seed;
}

public sealed partial class PlaneWorld
{
    public List<PlaneVault> Vaults { get; } = new();

    /// <summary>A shop's vault's prize, rolled as it is bought.</summary>
    PlaneVault ShopVault(Vector2 at)
    {
        float roll = _drops.NextFloat();
        var prize = roll < VaultTuning.ShellsChance ? VaultPrize.Shells : roll < VaultTuning.ShellsChance + VaultTuning.OrbChance ? VaultPrize.Orb : VaultPrize.Prank;
        return new PlaneVault
        {
            Position = at, Prize = prize, Seed = _drops.Range(0f, 100f),
            Shells = prize == VaultPrize.Shells ? VaultTuning.ShellsMin + _drops.Int(VaultTuning.ShellsMax - VaultTuning.ShellsMin + 1) : 0,
        };
    }

    void StepVaults()
    {
        var p = Player;
        foreach (var v in Vaults)
        {
            if (v.Popped) continue;
            float light = 0f;
            if (!Defeated)
            {
                foreach (var (beam, worth) in HerBeams) light += worth * BeamLight(beam, v.Position, 0f, VaultTuning.Radius, v);
                var aura = Aura;
                if (aura.Shining)
                {
                    float d = Vector2.Distance(aura.Position, v.Position);
                    if (d < aura.Range) light += VaultTuning.AuraShare * (1f - d / aura.Range);
                }
            }
            // (Her whole beam fills it at its pace, however many rays meet it.)
            light = MathF.Min(light, 1f);
            v.Fill = light > 0f ? MathF.Min(1f, v.Fill + light * Dt / VaultTuning.FillSeconds) : MathF.Max(0f, v.Fill - VaultTuning.Settle * Dt);
            if (v.Fill >= 1f) PopVault(v);
        }
    }

    /// <summary>Full: it pops, and frees what it held.</summary>
    void PopVault(PlaneVault v)
    {
        v.Popped = true;
        Events.Add(new PlaneEvent(PlaneEventType.VaultPopped, v.Position, Vector2.Zero, (float)v.Prize));
        switch (v.Prize)
        {
            case VaultPrize.Shells:
                for (int i = 0; i < v.Shells; i++) Shells.Add(new PlaneShell { Position = v.Position, Velocity = Fling(1.5f, 4.5f) });
                break;
            case VaultPrize.Orb:
                Hearts.Add(new PlaneHeart { Position = v.Position, Velocity = Fling(0.5f, 1.2f) });
                break;
            case VaultPrize.Pearl:
                Pearls.Add(new PlanePearl { ItemId = v.ItemId, Position = v.Position });
                break;
            case VaultPrize.Prank:
            {
                // A blow out of it that knocks her back (no harm: a prank).
                var p = Player;
                var away = p.Position - v.Position;
                away = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : Vector2.UnitY;
                p.Velocity = away * VaultTuning.PrankPush;
                p.TumbleTimer = VaultTuning.PrankTumble;
                p.TumbleDir = away;
                Events.Add(new PlaneEvent(PlaneEventType.VaultPrank, v.Position, away));
                break;
            }
        }
    }
}
