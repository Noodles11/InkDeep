using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A treasure in the water (docs/proposals/CROSSOVER.md §5.2): one lying hidden on its level (a relic place) until she
/// takes it, or one she has found, standing in the start cave (<see cref="InStartCave"/>: the Pearl Altar, the Spare Bell,
/// the Shell Urn; what each gives is given as the run starts or lies beside it).
/// </summary>
public sealed class PlaneRelic
{
    public string Id = "";
    public Vector2 Position;
    public bool Taken;
    public bool InStartCave;
}

/// <summary>What a found treasure's place in the start cave asks, and how near she must come to take a hidden one.</summary>
public static class RelicTuning
{
    /// <summary>How far from where she starts the start cave's treasures stand, and their pearls round the altar.</summary>
    public const float StartRing = 4.2f, AltarRing = 1.6f;
    /// <summary>How near she must come to take a hidden treasure.</summary>
    public const float Reach = 0.9f;
}

public sealed partial class PlaneWorld
{
    /// <summary>The level's treasures: a hidden one to find, and in the start cave the ones she has.</summary>
    public List<PlaneRelic> Relics { get; } = new();

    /// <summary>The treasure she last found (with TreasureFound).</summary>
    public string? LastTreasure { get; private set; }

    /// <summary>
    /// Each relic place (a POI mark far from the walk) holds its depth's treasure while it is still to be found; once she
    /// has it, the place rolls an ordinary reward instead: a pearl from the secret pool.
    /// </summary>
    void PlaceRelics()
    {
        foreach (var poi in Map.Pois.Where(p => p.Kind == PoiKind.Relic))
            RelicAt(poi, new Rng(Map.Seed ^ 0x2E11C5UL ^ ((ulong)Map.Level << 24) ^ ((ulong)Map.Depth << 32) ^ ((ulong)Map.Attempt << 48)));
    }

    /// <summary>A relic place: its depth's treasure, or (found already, or on its level once) a pearl.</summary>
    void RelicAt(Poi poi, Rng rng)
    {
        int depth = Map.Depth - LevelPlan.Tutorial;
        string? id = (Map.Templated ? Run.TreasureCatalog?.InBiome(Map.Biome) : Run.TreasureCatalog?.AtDepth(depth))?.Id;
        if (id is not null && !Run.Treasures.Contains(id) && !Relics.Exists(r => r.Id == id))
        {
            Relics.Add(new PlaneRelic { Id = id, Position = poi.Position });
            return;
        }
        var offer = Run.Offer("secret", Pearls.Select(q => q.ItemId));
        if (offer.Count > 0) Pearls.Add(new PlanePearl { ItemId = Run.Pick(offer, rng), Position = poi.Position });
    }

    /// <summary>
    /// The start cave (CROSSOVER.md §5.2): on a run's first level (PlaneRun.StartCave, taken once) the treasures she has
    /// found stand round where she starts. The Pearl Altar offers its pearls (she takes one; the others fade); the Shell
    /// Urn's shells lie spilled before it; the Spare Bell's pip is hers already (PlaneRun.BeginWithTreasures).
    /// </summary>
    void PlaceStartCave()
    {
        if (!Run.StartCave || !Map.Templated) return;
        Run.StartCave = false;
        var have = Treasures.Known.Where(Run.Treasures.Contains).ToList();
        if (have.Count == 0) return;
        var rng = new Rng(Map.Seed ^ 0x57A27CUL ^ ((ulong)Map.Depth << 32));
        var start = Map.Start.Position;
        var used = new List<Vector2>();
        float turn = rng.NextFloat() * MathF.Tau;
        foreach (string id in have)
        {
            if (SpotNear(start, RelicTuning.StartRing, ref turn, used, id == Treasures.PearlAltar ? RelicTuning.AltarRing + 0.6f : 0.9f) is not { } at) continue;
            used.Add(at);
            Relics.Add(new PlaneRelic { Id = id, Position = at, Taken = true, InStartCave = true });
            if (id == Treasures.PearlAltar)
            {
                var offer = Run.Offer("altar", Pearls.Select(q => q.ItemId));
                for (int k = 0; k < Treasures.AltarPearls && offer.Count > 0; k++)
                {
                    string pearl = Run.Pick(offer, rng);
                    offer.Remove(pearl);
                    float a = MathF.Tau * k / Treasures.AltarPearls + turn;
                    var on = at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * RelicTuning.AltarRing;
                    Pearls.Add(new PlanePearl { ItemId = pearl, Position = Clear(on, 0.2f) ? on : at, Placed = true, Choice = 1 });
                }
            }
            else if (id == Treasures.ShellUrn)
            {
                // Spilled before the urn: a conch, two scallops and a cowrie, fifteen in all.
                var spill = new[] { ShellKind.Conch, ShellKind.Scallop, ShellKind.Scallop, ShellKind.Cowrie };
                var toward = Vector2.Normalize(start - at);
                for (int k = 0; k < spill.Length; k++)
                {
                    var side = new Vector2(-toward.Y, toward.X);
                    var on = at + toward * (1f + 0.35f * k) + side * (k % 2 == 0 ? 0.45f : -0.45f);
                    Shells.Add(new PlaneShell { Position = Clear(on, 0.2f) ? on : at, Kind = spill[k], Value = ShellWorth.Of(spill[k]) });
                }
            }
        }
    }

    /// <summary>A spot about this far from a centre in open water, clear of the others, turning round from an angle.</summary>
    Vector2? SpotNear(Vector2 centre, float far, ref float turn, List<Vector2> others, float clear)
    {
        for (int k = 0; k < 24; k++)
        {
            // Alternately either side of the angle, further each time.
            float a = turn + (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * MathF.Tau / 24f;
            foreach (float d in new[] { far, far * 0.75f, far * 1.3f })
            {
                var at = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
                if (!Clear(at, clear) || others.Any(o => Vector2.Distance(o, at) < 3f)) continue;
                turn = a + MathF.Tau / 3f;
                return at;
            }
        }
        return null;
    }

    /// <summary>She takes a hidden treasure by touching it: it is hers for good (the game keeps it on the jelly).</summary>
    void StepRelics()
    {
        var p = Player;
        foreach (var relic in Relics)
        {
            if (relic.Taken || Vector2.Distance(relic.Position, p.Position) > Radius + RelicTuning.Reach) continue;
            relic.Taken = true;
            Run.Treasures.Add(relic.Id);
            LastTreasure = relic.Id;
            Events.Add(new PlaneEvent(PlaneEventType.TreasureFound, relic.Position, Vector2.Zero));
        }
    }

    /// <summary>A pearl of a choice is touched: the others of its choice fade.</summary>
    void ChoiceMade(PlanePearl chosen)
    {
        if (chosen.Choice == 0) return;
        foreach (var other in Pearls)
        {
            if (other == chosen || other.Choice != chosen.Choice || other.Taken || other.Absorbing) continue;
            other.Taken = true;
            Events.Add(new PlaneEvent(PlaneEventType.PearlFaded, other.Position, Vector2.Zero));
        }
    }
}
