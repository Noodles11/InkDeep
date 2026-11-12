using System.Linq;

namespace OctoShoots.Core.Items;

/// <summary>
/// The kinds of pearl in the light grammar (docs/proposals/LIGHT-GRAMMAR.md §2, docs/PEARLS.md): what part of her
/// light a pearl speaks to. Shapes say how it leaves her, paths what it does where it lands, colours what it does to
/// what it lights, rhythms how it changes while held, echoes what freed light does, glow her aura's own voice; stat
/// pearls are the plain adverbs, actives are used, and the crab's pearls are found only in a Crab Deal.
/// </summary>
public enum PearlKind { Shape, Path, Colour, Rhythm, Echo, Glow, Stat, Active, Crab }

public static class PearlWords
{
    /// <summary>The kind of a pearl: the first that fits, in the order of the enum's own reading (a colour before a shape).</summary>
    public static PearlKind Kind(ItemDef item)
    {
        if (item.Pools.Contains("crab")) return PearlKind.Crab;
        if (item.Kind == ItemKind.Active) return PearlKind.Active;
        var s = item.Shot;
        var flags = item.Flags;
        if (s is { } c && (c.Ember > 0 || c.Bloom > 0 || c.Frost > 0)) return PearlKind.Colour;
        if (s is { } sh && (sh.Multishot > 1 || sh.Wave || sh.Rear || sh.Laser || sh.Spiral || sh.Pattern == ShotPattern.Kraken8)) return PearlKind.Shape;
        if ((s?.Charge ?? false) || flags.Contains("minigun") || flags.Contains("cavitation")) return PearlKind.Rhythm;
        if ((s?.Explosive ?? false) || item.Effects.Any(e => e.Action == EffectAction.Shards || (e.Trigger == Trigger.OnKill && e.Action == EffectAction.Heal)))
            return PearlKind.Echo;
        if (s is { } p && (p.Bounces > 0 || p.Split || p.Spectral || p.Pierce || p.Boomerang || p.Homing || p.Grow || p.ChainTargets > 0)
            || flags.Contains("linger"))
            return PearlKind.Path;
        if (item.Stats.Any(m => m.Stat is Stat.Glow or Stat.Fireflies)) return PearlKind.Glow;
        return PearlKind.Stat;
    }

    /// <summary>A kind's name, as a heading and a chip read it.</summary>
    public static string Name(PearlKind k) => k switch
    {
        PearlKind.Shape => "Shape",
        PearlKind.Path => "Path",
        PearlKind.Colour => "Colour",
        PearlKind.Rhythm => "Rhythm",
        PearlKind.Echo => "Echo",
        PearlKind.Glow => "Glow",
        PearlKind.Stat => "Stat",
        PearlKind.Active => "Active",
        _ => "Crab's",
    };

    /// <summary>Where a pool is, in words.</summary>
    public static string Pool(string pool) => pool switch
    {
        "treasure" => "clam rooms",
        "shop" => "shops",
        "boss" => "bosses",
        "secret" => "secret rooms",
        "whale" => "the whale",
        "curse" => "curse rooms",
        "goldenClam" => "golden clams",
        "crab" => "Crab Deals",
        "start" => "every run's start",
        _ => pool,
    };
}
