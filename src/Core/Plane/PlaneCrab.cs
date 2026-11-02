using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>Crab Deals (docs/ADVENTURE.md §7): first guesses, tuned in play.</summary>
public static class CrabTuning
{
    /// <summary>What a deal cuts off her max HP (a share of what she has now), and what must be left after it.</summary>
    public const float Cut = 0.25f, LeastMaxHp = 40f;
    /// <summary>How long she must stay at a pearl to strike the deal.</summary>
    public const float HoldSeconds = 1.5f;
    /// <summary>How far the two pearls lie from the room's middle, and the crab behind them.</summary>
    public const float Slot = 1.6f, Back = 2.4f;
    /// <summary>How near she must swim for the fake wall to give way.</summary>
    public const float WallTouch = 0.4f;
    /// <summary>Each cut she has taken adds this much to her damage (Claw Tithe).</summary>
    public const float Tithe = 0.25f;
    /// <summary>Under this share of her health, Molten Carapace's shots pierce and do Molten more.</summary>
    public const float MoltenBelow = 0.5f, Molten = 1.6f;
    /// <summary>Severed Nerve's strand circles her this far out, this fast (radians a second).</summary>
    public const float NerveOut = 1.6f, NerveSpin = 2.4f;
    /// <summary>Last Light's flood of light, and how long she cannot be hurt after it.</summary>
    public const float LastLightReach = 12f, LastLightShield = 1.5f;
}

public sealed partial class PlaneWorld
{
    /// <summary>The crab's place in a Crab Deal room (null: this level has none).</summary>
    public Vector2? CrabAt { get; private set; }
    /// <summary>The way out of its room.</summary>
    public Vector2 CrabFacing { get; private set; } = Vector2.UnitY;

    /// <summary>A Crab Deal room: the crab, and two pearls to choose between, one of them only ever found here.</summary>
    void PlaceCrab()
    {
        var room = Map.Pois.FirstOrDefault(p => p.Kind == PoiKind.CrabDeal);
        if (room is null || room.Cave < 0) return;
        var cave = Map.Caves[room.Cave];
        CrabFacing = cave.Facing;
        CrabAt = room.Position - cave.Facing * CrabTuning.Back;
        if (Run.Catalog is not { } catalog) return;
        var side = new Vector2(-cave.Facing.Y, cave.Facing.X);
        var rng = new Rng(Map.Seed ^ 0xC2ABUL ^ ((ulong)Map.Level << 28) ^ ((ulong)Map.Depth << 36) ^ ((ulong)Map.Attempt << 50));
        var rare = PlaneRun.CrabPearls.Where(id => catalog.TryGet(id, out _) && !Run.Items.Contains(id)).ToList();
        var best = PlaneRun.PortedPearls.Where(id => Run.CanOffer(id) && catalog.TryGet(id, out var item) && item.Quality >= 3).ToList();
        var offers = new List<string>();
        if (rare.Count > 0) offers.Add(rare[rng.Int(rare.Count)]);
        if (best.Count > 0) offers.Add(Run.Pick(best, rng));
        for (int i = 0; i < offers.Count; i++)
            Stands.Add(new ShopStand { Kind = StandKind.Deal, ItemId = offers[i], Position = room.Position + side * (i == 0 ? -CrabTuning.Slot : CrabTuning.Slot) });
    }

    ShopStand? _refused;

    /// <summary>The deal: she stays at a pearl while the ring fills; struck, the other pearl crumbles and the ritual begins.</summary>
    void StepDeals()
    {
        var p = Player;
        foreach (var stand in Stands)
        {
            if (stand.Kind != StandKind.Deal || stand.Sold) continue;
            bool near = Vector2.Distance(stand.Position, p.Position) <= Radius + PlaneEconomyTuning.StandReach;
            if (!near || Pearls.Exists(q => q.Absorbing))
            {
                stand.Hold = 0f;
                if (_refused == stand) _refused = null;
                continue;
            }
            if (!Run.CanBeCut)
            {
                if (_refused != stand) Events.Add(new PlaneEvent(PlaneEventType.DealRefused, stand.Position, Vector2.Zero));
                _refused = stand;
                continue;
            }
            stand.Hold += Dt;
            if (stand.Hold < CrabTuning.HoldSeconds) continue;
            foreach (var other in Stands)
                if (other.Kind == StandKind.Deal) other.Sold = true;
            var bought = new PlanePearl { ItemId = stand.ItemId, Position = p.Position, Deal = true };
            Pearls.Add(bought);
            BeginRitual(bought);
            Events.Add(new PlaneEvent(PlaneEventType.DealStruck, stand.Position, Vector2.Zero));
            return;
        }
    }

    /// <summary>The pearl is hers, and the crab takes its price: its claw cuts her arms.</summary>
    void PayDeal()
    {
        Run.TakeCut();
        Player.Hp = MathF.Min(Player.Hp, Run.MaxHp);
        Events.Add(new PlaneEvent(PlaneEventType.ClawCut, Player.Position, Vector2.Zero));
    }

    bool Has(string flag) => Run.Loadout.Flags.Contains(flag);

    /// <summary>Severed Nerve's strand: where it is now (null: she has none).</summary>
    public Vector2? NerveAt => Has("severedNerve")
        ? Player.Position + new Vector2(MathF.Cos(Time * CrabTuning.NerveSpin), MathF.Sin(Time * CrabTuning.NerveSpin)) * CrabTuning.NerveOut
        : null;

    /// <summary>Claw Tithe: the damage her cuts have earned her.</summary>
    float TitheScale => Has("clawTithe") ? 1f + CrabTuning.Tithe * Run.ArmCuts : 1f;

    /// <summary>Molten Carapace is burning: she is low enough.</summary>
    bool Molten => Has("moltenCarapace") && Player.Hp < Run.MaxHp * CrabTuning.MoltenBelow;

    bool _lastLightSpent;

    /// <summary>Last Light: once a level, what would have ended her leaves her at 1 HP in a flood of light.</summary>
    void SpendLastLight()
    {
        var p = Player;
        if (p.Hp > 0f || _lastLightSpent || !Has("lastLight")) return;
        _lastLightSpent = true;
        p.Hp = 1f;
        p.ShieldTimer = CrabTuning.LastLightShield;
        LightDots(p.Position, CrabTuning.LastLightReach, 3f);
        Events.Add(new PlaneEvent(PlaneEventType.LastLight, p.Position, Vector2.Zero));
    }
}
