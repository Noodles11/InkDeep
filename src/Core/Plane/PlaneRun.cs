using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// What Clementine carries from room to room through the rift: her HP and the pearls she has collected (and the
/// loadout they add up to). A new run starts empty; going through the gateway keeps it.
/// </summary>
public sealed class PlaneRun
{
    /// <summary>
    /// The pearls ported to the plane so far (docs/PEARLS.md marks them): treasure rooms, shops and Queen Clam offer
    /// only these, and only once unlocked (a pearl with an `unlock` waits for its achievement). Passive ones first,
    /// then the active ones.
    /// </summary>
    public static readonly string[] PortedPearls =
    {
        "triple_tentacle", "hammerhead", "anglerfish_lure", "swordfish_bill", "mirror_scale", "boomerang_shrimp", "double_helix",
        "coral_crown", "moon_jelly_heart", "shark_tooth", "starfish_arm", "ink_sac", "remora_sucker",
        "captains_hook", "lantern_pearl", "bubble_coral", "plankton_swarm", "mitosis", "giant_squid_eye",
        "lucky_sea_glass", "pirates_doubloon",
        "squid_ink_espresso", "whale_lung", "pufferfish_pout", "cuttlebone", "cannonball", "dolphin_fin", "coral_polyp",
        "abyssal_glow", "glass_squid", "sea_cucumber", "sardine_run", "pistol_shrimp", "brass_compass", "sea_chart_nautilus",
        "bubble_shield", "whale_song", "firefly_squid", "prism_pearl", "ink_cloud", "conch_horn", StartingPearl,
    };

    /// <summary>The pearls found only in a Crab Deal (never offered anywhere else).</summary>
    public static readonly string[] CrabPearls = { "severed_nerve", "molten_carapace", "last_light", "claw_tithe" };

    /// <summary>The active pearl every run starts with (Tide Pulse).</summary>
    public const string StartingPearl = "tide_pulse";

    /// <summary>What every run starts with; never offered, since she always has it.</summary>
    public static readonly string[] StartingPearls = { StartingPearl, "sea_chart_nautilus" };

    /// <summary>The open reef's active slots (docs/archive/RPG-PROPOSAL.md §5.2): three, each its own button.</summary>
    public const int SlotCount = 3;

    public PlaneRun(ItemCatalog? catalog, Tuning tuning)
    {
        Catalog = catalog;
        Tuning = tuning;
        Rebuild();
        Hp = MaxHp;
    }

    public ItemCatalog? Catalog { get; }
    public Tuning Tuning { get; }
    public List<string> Items { get; } = new();
    public Loadout Loadout { get; private set; } = null!;
    public float Hp { get; set; }
    /// <summary>Small shells, the currency: picked up in places and from every mob, spent in shops.</summary>
    public int Shells { get; set; }
    public LevelId Level { get; set; } = LevelId.First;

    public float MaxHp => Loadout.Stats.MaxHp * System.MathF.Pow(1f - CrabTuning.Cut, ArmCuts);

    /// <summary>The deals she has struck: each cut a share of her max HP off for the rest of the run.</summary>
    public int ArmCuts { get; set; }

    /// <summary>Whether a deal would leave her enough.</summary>
    public bool CanBeCut => MaxHp * (1f - CrabTuning.Cut) >= CrabTuning.LeastMaxHp;

    /// <summary>A deal's price, taken.</summary>
    public void TakeCut() => ArmCuts++;

    readonly float[] _charges = { 1f, 1f, 1f };

    /// <summary>The active pearl in a slot: on the open reef, the slot's; on a run, the one she holds is the first slot's.</summary>
    public ItemDef? SlotActive(int slot)
    {
        return slot == 0 ? Loadout.Active : null;
    }

    /// <summary>How charged a slot's pearl is, 0 → 1 (ready).</summary>
    public float Charge(int slot) => _charges[slot];
    public void SetCharge(int slot, float charge) => _charges[slot] = charge;

    /// <summary>The active pearl she holds in her first slot (on a run, the last one taken; one at a time), used with F.</summary>
    public ItemDef? Active => SlotActive(0);

    /// <summary>How charged the first slot's pearl is, 0 → 1 (ready). A new one comes charged.</summary>
    public float ActiveCharge
    {
        get => _charges[0];
        set => _charges[0] = value;
    }

    /// <summary>What a pearl fetches at the shell shop: an active its worth, an absorbed passive half (ReefTuning.PearlWorth).</summary>
    public int SellPrice(string itemId) =>
        Catalog is not null && Catalog.TryGet(itemId, out var item)
            ? item.Kind == ItemKind.Active ? ReefTuning.PearlWorth(item.Quality) : ReefTuning.PearlWorth(item.Quality) / 2
            : 0;

    /// <summary>
    /// Sells a pearl at the shell shop for silver shells: an active she owns (out of its slot too), or one copy of a
    /// passive drawn back out of her bell. False if she has no such pearl.
    /// </summary>
    public bool Sell(string itemId)
    {
        int price = SellPrice(itemId);
        if (Items.Remove(itemId))
        {
            Rebuild();
            Hp = System.MathF.Min(Hp, MaxHp);
        }
        else return false;
        Shells += price;
        return true;
    }

    /// <summary>Experience (DESIGN-TOPDOWN §2.6): toward the next level, the level she is, and the skill points she has not spent yet.</summary>
    public int Xp { get; set; }
    public int XpLevel { get; set; } = 1;
    public int Points { get; set; }
    /// <summary>Her skill points, spent: how many on each modifier.</summary>
    public Dictionary<OctoShoots.Core.Items.Stat, int> Skills { get; } = new();

    /// <summary>How many skill points she has spent in all (each adds a little max HP, whatever it went on).</summary>
    public int Upgrades => Skills.Values.Sum();

    /// <summary>The experience it takes to reach the next level.</summary>
    public int XpNeeded => ExperienceTuning.Need(XpLevel);

    /// <summary>
    /// Gains experience; every level it reaches is a skill point. Returns how many levels. Held, her first level-up cannot
    /// come yet: it stops just short of it.
    /// </summary>
    public int Gain(int xp, bool hold = false)
    {
        int levels = 0;
        Xp += xp;
        if (hold && XpLevel == 1) Xp = System.Math.Min(Xp, XpNeeded - 1);
        while (Xp >= XpNeeded)
        {
            Xp -= XpNeeded;
            XpLevel++;
            Points++;
            levels++;
        }
        return levels;
    }

    /// <summary>Spends a skill point on a modifier (more max HP comes filled).</summary>
    public bool Spend(OctoShoots.Core.Items.Stat stat)
    {
        if (Points <= 0 || !OctoShoots.Core.Plane.Skills.All(Tuning).Any(s => s.Stat == stat)) return false;
        Points--;
        Skills[stat] = Skills.GetValueOrDefault(stat) + 1;
        float max = MaxHp;
        Rebuild();
        Hp = System.MathF.Min(Hp + System.MathF.Max(MaxHp - max, 0f), MaxHp);
        return true;
    }

    /// <summary>
    /// The Dusk Dance (Bubbles's, docs/FOLK.md §8.3): every skill point she has spent comes back to lay
    /// again (and the max HP they gave goes with them). Returns how many.
    /// </summary>
    public int Relearn()
    {
        int back = Upgrades;
        Points += back;
        Skills.Clear();
        Rebuild();
        Hp = System.MathF.Min(Hp, MaxHp);
        return back;
    }

    public void Add(string itemId)
    {
        bool active = Catalog is not null && Catalog.TryGet(itemId, out var item) && item.Kind == ItemKind.Active;
        // She carries one active pearl at a time: a new one takes the old one's place.
        if (active)
            Items.RemoveAll(id => Catalog!.TryGet(id, out var held) && held.Kind == ItemKind.Active);
        Items.Add(itemId);
        Rebuild();
        if (Active?.Id == itemId) ActiveCharge = 1f;
    }

    /// <summary>Debug: takes a pearl away again (every copy). Her HP stays within the new maximum.</summary>
    public bool Remove(string itemId)
    {
        if (Items.RemoveAll(id => id == itemId) == 0) return false;
        Rebuild();
        Hp = System.MathF.Min(Hp, MaxHp);
        return true;
    }

    /// <summary>Rebuilds the loadout after her skill points were set from outside (a resumed save).</summary>
    public void Refresh() => Rebuild();

    void Rebuild() => Loadout = Loadout.Build(Catalog, Items, Tuning, OctoShoots.Core.Plane.Skills.Mods(Skills, Tuning)
        .Append(new StatMod { Stat = Stat.MaxHp, Op = StatOp.Add, Value = ExperienceTuning.HpPerPoint * Upgrades }), pearlStats: true);

    /// <summary>
    /// The achievements earned (the profile's, shared: one earned mid-run unlocks its pearl from then on). Locked pearls
    /// are never offered.
    /// </summary>
    public ISet<string> Unlocked { get; set; } = new HashSet<string>();

    /// <summary>
    /// A pearl a room may hold: a ported one the catalog knows, unlocked, and either not held yet or not unique
    /// (docs/PEARLS.md).
    /// </summary>
    public bool CanOffer(string itemId) => OfferWeight(itemId) > 0f;

    /// <summary>
    /// Picks one pearl to offer from a list, by offer weight: a pearl she holds a copy of is less likely. (With every
    /// weight equal it is a plain uniform pick, as before.)
    /// </summary>
    public string Pick(IReadOnlyList<string> offer, Rng rng)
    {
        var weights = offer.Select(OfferWeight).ToList();
        if (weights.All(w => w == weights[0])) return offer[rng.Int(offer.Count)];
        float roll = rng.NextFloat() * weights.Sum();
        for (int i = 0; i < offer.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0f) return offer[i];
        }
        return offer[^1];
    }

    /// <summary>How likely a pearl is to be offered: its weight, lower for each copy of a non-unique one she holds, 0 if it may not be.</summary>
    public float OfferWeight(string itemId) =>
        !StartingPearls.Contains(itemId) && Catalog is not null && Catalog.TryGet(itemId, out var item) && ItemPools.IsUnlocked(item, Unlocked)
            ? ItemPools.WeightFor(item, Items.Count(id => id == itemId)) : 0f;

    /// <summary>
    /// A passive pearl that does nothing but change her stats (the open reef counts none of it, so offers none of them):
    /// no shot, no flag, nothing but what it does as it is taken.
    /// </summary>
    public static bool OnlyStats(ItemDef item) =>
        item.Kind == ItemKind.Passive && item.Shot is null && item.Flags.Count == 0 && item.Effects.All(e => e.Trigger == Trigger.OnPickup);
}
