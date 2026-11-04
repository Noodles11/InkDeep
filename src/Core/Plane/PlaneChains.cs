using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>What a step of a neighbour's chain asks (docs/NPC-BOSSES-PROPOSAL.md §1).</summary>
public enum StepKind
{
    /// <summary>Swim to a place on the reef (within ChainTuning.FindReach of it).</summary>
    Find,
    /// <summary>Swim into a zone.</summary>
    Enter,
    /// <summary>Free murkling primes in a zone (on its reef or in its caves).</summary>
    Prime,
    /// <summary>Seal minor leaks in a zone's caves.</summary>
    Seal,
    /// <summary>Untie requests at the Tide Fan.</summary>
    Untie,
    /// <summary>Find a neighbour's great cave: swim up to its mouth.</summary>
    Mouth,
    /// <summary>Come home to the bay.</summary>
    Home,
}

/// <summary>One step: what it asks, as the HUD says it, in which zone (by name) or of which neighbour, how many, or where.</summary>
public sealed record ChainStep(StepKind Kind, string Text, string? Zone = null, int Count = 1, string? Folk = null, float X = 0f, float Y = 0f)
{
    public Vector2 At => new(X, Y);
}

/// <summary>
/// A neighbour's key quest chain (docs/NPC-BOSSES-PROPOSAL.md §4.3–8.3): given once she is home, a few small steps that
/// lead toward the next great cave, the last paying an active pearl, with her line for it.
/// </summary>
public sealed record Chain(string Folk, string Title, ChainStep[] Steps, string Pearl, string Line);

public static class ChainTuning
{
    /// <summary>A Find step is done within this of its place, a Mouth step within this of the great cave's mouth (metres).</summary>
    public const float FindReach = 10f, MouthReach = 12f;
}

/// <summary>The five chains, and her progress through them (Jelly.Chains: per neighbour, the step she is on and how far along it).</summary>
public static class Chains
{
    public static readonly Chain[] All =
    {
        new("tull", "Find Kip", new ChainStep[]
        {
            new(StepKind.Find, "Chart the Shallows' east", X: 370f, Y: 695f),
            new(StepKind.Prime, "Free two murkling primes in the Shallows", Zone: "shallows", Count: 2),
            new(StepKind.Mouth, "Find the Cleaning Station", Folk: "kip"),
        }, "bubble_shield", "Every turtle knows: carry your house with you."),
        new("kip", "My kelp clients stopped coming", new ChainStep[]
        {
            new(StepKind.Enter, "Swim into the Kelp Jungle", Zone: "kelp"),
            new(StepKind.Untie, "Untie a request at the Tide Fan"),
            new(StepKind.Prime, "Free a murkling prime in the Kelp Jungle", Zone: "kelp"),
            new(StepKind.Mouth, "Find the Still Bed", Folk: "fennel"),
        }, "whale_song", "Hold still. This is the good part."),
        new("fennel", "The wreck has gone quiet", new ChainStep[]
        {
            new(StepKind.Enter, "Swim into the Sunken Galleon", Zone: "galleon"),
            new(StepKind.Seal, "Seal a leak in the Sunken Galleon", Zone: "galleon"),
            new(StepKind.Prime, "Free a murkling prime in the Sunken Galleon", Zone: "galleon"),
            new(StepKind.Mouth, "Find the Captain's Hold", Folk: "ottilie"),
        }, "ink_cloud", "Be the weed. Nobody looks at weed."),
        new("ottilie", "My best glint", new ChainStep[]
        {
            new(StepKind.Find, "Find Ottilie's old hoard in the wreck's debris", X: 700f, Y: 700f),
            new(StepKind.Home, "Bring her glint home to Lantern Bay"),
        }, "prism_pearl", "One light, three colours. Your family's, I think."),
        new("bubbles", "Where the reef goes down", new ChainStep[]
        {
            new(StepKind.Find, "Find where the reef goes down, past the Carnival's mirrors", X: 770f, Y: 300f),
        }, "conch_horn", "The sea answers. It always did, down here."),
    };

    public static Chain? Of(string folk) => All.FirstOrDefault(c => c.Folk == folk);

    /// <summary>Where she is in a chain: the step, and how far along it (a chain past its last step is done).</summary>
    static List<int> Progress(Jelly jelly, Chain chain) =>
        jelly.Chains.TryGetValue(chain.Folk, out var at) ? at : jelly.Chains[chain.Folk] = new List<int> { 0, 0 };

    public static bool Done(Jelly jelly, Chain chain) => jelly.Chains.TryGetValue(chain.Folk, out var at) && at[0] >= chain.Steps.Length;

    /// <summary>The chains she is on: every freed neighbour's not yet done, with the step she is on and how far along it.</summary>
    public static IEnumerable<(Chain Chain, ChainStep Step, int Count)> Active(Jelly jelly)
    {
        foreach (var c in All)
        {
            if (!jelly.Freed.Contains(c.Folk) || Done(jelly, c)) continue;
            var at = Progress(jelly, c);
            yield return (c, c.Steps[at[0]], at[1]);
        }
    }

    /// <summary>
    /// Something she did answers a step: its kind, in which zone or of which neighbour, or where she is now. Steps
    /// done move each chain on; returns the chains finished by it (their pearl to give).
    /// </summary>
    public static List<Chain> Note(Jelly jelly, StepKind kind, string? zone = null, string? folk = null, Vector2? at = null, int amount = 1)
    {
        var finished = new List<Chain>();
        foreach (var (chain, step, _) in Active(jelly).ToList())
        {
            if (step.Kind != kind) continue;
            bool fits = kind switch
            {
                StepKind.Find => at is { } here && Vector2.Distance(here, step.At) < ChainTuning.FindReach,
                StepKind.Enter or StepKind.Prime or StepKind.Seal => step.Zone is null || step.Zone == zone,
                StepKind.Mouth => step.Folk == folk,
                _ => true,
            };
            if (!fits) continue;
            var p = Progress(jelly, chain);
            p[1] += amount;
            if (p[1] < step.Count) continue;
            p[0]++;
            p[1] = 0;
            if (p[0] >= chain.Steps.Length) finished.Add(chain);
        }
        return finished;
    }

    /// <summary>A step as the HUD's ribbon says it (with how far along it, if it asks for more than one).</summary>
    public static string Short(ChainStep step, int count) => step.Count > 1 ? $"{step.Text} ({count}/{step.Count})" : step.Text;
}
