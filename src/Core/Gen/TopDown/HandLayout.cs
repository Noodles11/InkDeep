using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// A static map drawn by hand (docs/RPG-PROPOSAL.md §2): Lantern Bay and the key areas. It names the cave's chambers,
/// the tunnels between them and its places in the generator's own terms; <see cref="TopDownGenerator.FromHand"/> builds the
/// level from it with the same stages as a seeded one (the rock, arches and passes, the decoration), so it is drawn
/// unchanged. Its seed shapes only the details (the chambers' craggy outlines, the tunnels' bends, the rock).
/// </summary>
public sealed class HandLayout
{
    /// <summary>The map's file name (data/maps/&lt;Name&gt;.json).</summary>
    public string Name { get; set; } = "";
    /// <summary>What the arrival card and the map call it.</summary>
    public string Title { get; set; } = "";
    /// <summary>The seed code for its details.</summary>
    public string Seed { get; set; } = "";
    /// <summary>Its biome, as a depth (ReefLook): 1 the Shallows.</summary>
    public int Depth { get; set; } = 1;
    /// <summary>The depth whose look (ReefLook) it takes, when not its own (a map without zones; the reef's zones each take their own).</summary>
    public int? Look { get; set; }
    public float Menace { get; set; }
    /// <summary>A haven's water (Lantern Bay, in a corner of the reef): the box it fills, where nothing harms her (LevelMap.Haven).</summary>
    public HandBox? Haven { get; set; }
    /// <summary>Its exit chamber is a hole down, as a seeded level's (a dive's haven, docs/DIVE.md §4).</summary>
    public bool Hole { get; set; }
    public List<HandChamber> Chambers { get; set; } = new();
    public List<HandTunnel> Tunnels { get; set; } = new();
    /// <summary>Places in caves (the shop): each off a chamber, its tunnel ending at the cave's mouth.</summary>
    public List<HandRoom> Rooms { get; set; } = new();
    /// <summary>Open places: clearings in the water.</summary>
    public List<HandPlace> Places { get; set; } = new();
    /// <summary>The bay's mirror (docs/RPG-PROPOSAL.md §8): a still pool of nacre where she changes her look.</summary>
    public HandPoint? Mirror { get; set; }
    /// <summary>The bay's Tide Fan (docs/QUESTS-PROPOSAL.md §2): the great sea fan where the bay ties its requests.</summary>
    public HandPoint? Fan { get; set; }
    /// <summary>The bay's folk at home (docs/NPC-BOSSES-PROPOSAL.md): where each lives once she is freed (Folk names her).</summary>
    public List<HandPoint> Homes { get; set; } = new();
    /// <summary>Its square, metres a side (a seeded level's unless it asks for more).</summary>
    public int Size { get; set; } = LevelMap.Seeded;

    /// <summary>Its number among the hand-drawn maps (the bay 0): with its depth, which level it counts as.</summary>
    public int Number { get; set; }

    /// <summary>Which level it counts as (its own, apart from every seeded level's).</summary>
    public LevelId Id => new(0, Depth, Number);

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static HandLayout FromJson(string json) => JsonSerializer.Deserialize<HandLayout>(json, Json) ?? throw new JsonException("an empty map");
}

/// <summary>A chamber at (X, Y) on the 140 m square, R metres across its middle. One is the start, one the exit.</summary>
public sealed class HandChamber
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float R { get; set; }
    public CaveLayout.Role Role { get; set; } = CaveLayout.Role.Main;
}

/// <summary>A tunnel from chamber A to chamber B.</summary>
public sealed class HandTunnel
{
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public float Width { get; set; } = 7f;
    public CorridorKind Kind { get; set; } = CorridorKind.Main;
}

/// <summary>A cave room off chamber From: Angle degrees round it (0 east, 90 south), Distance metres past its rim.</summary>
public sealed class HandRoom
{
    public PoiKind Kind { get; set; }
    public string From { get; set; } = "";
    public float Angle { get; set; }
    public float Distance { get; set; } = 16f;
    public float Width { get; set; } = 6f;
}

/// <summary>An open place at (X, Y).</summary>
public sealed class HandPlace
{
    public PoiKind Kind { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float R { get; set; } = 6f;
}

/// <summary>A box on the map, from (X0, Y0) to (X1, Y1).</summary>
public sealed class HandBox
{
    public float X0 { get; set; }
    public float Y0 { get; set; }
    public float X1 { get; set; }
    public float Y1 { get; set; }
}

/// <summary>A point on the map.</summary>
public sealed class HandPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>What the bay calls it (a region's cave mouth: "the east mouth"), if anything.</summary>
    public string Name { get; set; } = "";
    /// <summary>
    /// A great cave's mouth: the neighbour waiting at its bottom (Folk, docs/NPC-BOSSES-PROPOSAL.md), whose cave's level
    /// and floors are her own. A bay home: the neighbour who lives there.
    /// </summary>
    public string? Folk { get; set; }
}

