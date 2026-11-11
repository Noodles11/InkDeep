using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>What a step of a neighbour's chain asks (docs/FOLK.md §1).</summary>
public enum StepKind
{
    /// <summary>Find a thing: on a dive, at the bottom of a leak cave in its depth (going into one there); else a place on the reef (within ChainTuning.FindReach of it).</summary>
    Find,
    /// <summary>Reach a zone: on a dive, a depth or a cycle.</summary>
    Enter,
    /// <summary>Free murkling primes in a zone (a dive's depth).</summary>
    Prime,
    /// <summary>Seal leaks in a zone (a dive's depth).</summary>
    Seal,
    /// <summary>Untie requests at the Tide Fan.</summary>
    Untie,
    /// <summary>Find a neighbour's great cave: swim up to its mouth.</summary>
    Mouth,
    /// <summary>Come home: on a dive, reach a haven.</summary>
    Home,
}

/// <summary>One step: what it asks, as the HUD says it, in which zone (by name) or of which neighbour, how many, or where.</summary>
public sealed record ChainStep(StepKind Kind, string Text, string? Zone = null, int Count = 1, string? Folk = null, float X = 0f, float Y = 0f)
{
    public Vector2 At => new(X, Y);
}

/// <summary>
/// A neighbour's key quest chain (docs/FOLK.md §4.3–8.3): given once she is home, a few small steps that
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
    /// <summary>A dive's zone for a chain step (docs/DIVE.md §7): a depth as the player sees it, and a cycle.</summary>
    public static string DepthZone(int shownDepth) => $"depth{shownDepth}";
    public static string CycleZone(int cycle) => $"cycle{cycle}";

    /// <summary>
    /// The five chains of the dive (docs/DIVE.md §7): each neighbour's leads on down the depth below the one she was the
    /// boss of. A Find step's thing lies at the bottom of a leak cave in its depth: going into one there finds it.
    /// </summary>
    public static readonly Chain[] All =
    {
        new("tull", "Find Kip", new ChainStep[]
        {
            new(StepKind.Enter, "Dive down to Depth 2", Zone: "depth2"),
            new(StepKind.Prime, "Free two murkling primes in Depth 2", Zone: "depth2", Count: 2),
            new(StepKind.Find, "Find Kip's lost comb in a Depth 2 leak cave", Zone: "depth2"),
        }, "bubble_shield", "Every turtle knows: carry your house with you."),
        new("kip", "My kelp clients stopped coming", new ChainStep[]
        {
            new(StepKind.Untie, "Untie a request at the Tide Fan"),
            new(StepKind.Enter, "Dive down to Depth 3", Zone: "depth3"),
            new(StepKind.Prime, "Free a murkling prime in Depth 3", Zone: "depth3"),
        }, "whale_song", "Hold still. This is the good part."),
        new("fennel", "The wreck has gone quiet", new ChainStep[]
        {
            new(StepKind.Enter, "Dive down to Depth 4", Zone: "depth4"),
            new(StepKind.Seal, "Seal a leak in Depth 4", Zone: "depth4"),
            new(StepKind.Prime, "Free a murkling prime in Depth 4", Zone: "depth4"),
        }, "ink_cloud", "Be the weed. Nobody looks at weed."),
        new("ottilie", "My best glint", new ChainStep[]
        {
            new(StepKind.Find, "Find Ottilie's old hoard in a Depth 5 leak cave", Zone: "depth5"),
            new(StepKind.Home, "Bring her glint to a haven"),
        }, "prism_pearl", "One light, three colours. Your family's, I think."),
        new("bubbles", "Where the reef goes down", new ChainStep[]
        {
            new(StepKind.Enter, "Dive on past Depth 5, where the reef goes down again", Zone: "cycle2"),
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
                // (A dive's find is in a depth's leak cave, by zone; else a place on the reef, by where she is.)
                StepKind.Find => step.Zone is not null ? step.Zone == zone : at is { } here && Vector2.Distance(here, step.At) < ChainTuning.FindReach,
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
