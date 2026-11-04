using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Saves;

namespace OctoShoots.Core.Plane;

/// <summary>What a neighbour brings back to life in Lantern Bay once she is home (docs/NPC-BOSSES-PROPOSAL.md §3).</summary>
public enum BayService { TideFan, Mirror, BroodBed, PearlCollector, DuskDance }

/// <summary>
/// One of the bay's folk (docs/NPC-BOSSES-PROPOSAL.md): who she is, the boss the Leak made of her and the great cave she
/// waits at the bottom of (its level and floors fixed, the same for every jelly), and what she opens in the bay.
/// </summary>
/// <remarks>
/// Called: what the reef calls her till she names herself in the bay (Bubbles: "The dragonet"). Late: a line she has
/// only once the Coral Carnival's neighbour is home too (the foreshadowing the Bible allows from Depth 4, §4.4).
/// </remarks>
public sealed record Neighbour(
    string Id, string Name, string Species, string Boss, string Cave, int Level, int Floors, BayService Opens, string[] Lines, string? Called = null, string? Late = null);

/// <summary>
/// The bay's folk (docs/NPC-BOSSES-PROPOSAL.md): every NPC is a boss first. Each waits, corrupted, at the bottom of a great
/// cave; freed, she goes home to Lantern Bay and opens one of its services. A service whose neighbour has no boss yet is
/// open from the start, as before.
/// </summary>
public static class Folk
{
    public static readonly Neighbour[] All =
    {
        new("tull", "Grandmother Tull", "green sea turtle", "Sleepy Old Tull", "The Sleeping Ledge", 2, 1, BayService.TideFan, new[]
        {
            "I watched your whole bloom go down in one breath. I'm too wide for the Crack, little lamp. You aren't.",
            "You found me asleep under all that. Don't tell the others how long it took.",
            "The bay ties its troubles to the fan. I tie the knots. Take what you can carry.",
        }, Late: "At night the shore has lights that don't move like stars. I never liked them."),
        new("kip", "Kip", "cleaner wrasse", "Nine-Fin Kip", "The Cleaning Station", 3, 3, BayService.Mirror, new[]
        {
            "Hold still. You've got Leak in your frills. …There. Family colour, all the way through.",
            "Your sister glowed just like that. I did her rim twice, she was so proud of it.",
            "The mirror's clean. Mostly. Don't look at the corners, I'm getting to them.",
        }),
        new("fennel", "Fennel", "leafy seadragon", "Papa Fennel", "The Still Bed", 6, 3, BayService.BroodBed, new[]
        {
            "They passed through the kelp in a line, all facing down. None of them were pulsing. You don't drift like that. Something was drinking them.",
            "Leave your shells with me, if you like. I'll keep them the way I keep these.",
            "Shh. They're sleeping. All forty-one of them.",
        }),
        new("ottilie", "Ottilie", "common octopus", "Many-Armed Ottilie", "The Captain's Hold", 9, 3, BayService.PearlCollector, new[]
        {
            "Real ink is brown and honest, darling. That stuff out there is ink pretending.",
            "Buying, selling, admiring. Mostly admiring. Bring me something that glints.",
            "The brightest fish I ever saw swam through my wreck going north-east. All the wrong colours. Or all the right ones.",
        }),
        new("bubbles", "Bubbles", "mandarin dragonet", "The Pale Dragonet", "The Empty Stage", 12, 3, BayService.DuskDance, new[]
        {
            "Bubbles. That's the sound the sky made when it looked at me.",
            "Before the sea I lived somewhere small. The sun never moved. Food fell like snow. I think I dreamed it.",
            "There were jellies there too, before me. Pale ones. They never went anywhere.",
            "You don't have to keep swimming the way you learned.",
        }, "The dragonet"),
    };

    /// <summary>The neighbours whose boss waits in a great cave now (the rest come with theirs).</summary>
    public static IEnumerable<Neighbour> Built => All;

    public static Neighbour? Get(string? id) => id is null ? null : All.FirstOrDefault(n => n.Id == id);

    /// <summary>The neighbour who opens a service, if her boss is in the game.</summary>
    public static Neighbour? Opener(BayService service) => Built.FirstOrDefault(n => n.Opens == service);

    /// <summary>The service is open in her bay: its neighbour is home (or has no boss yet, so it is open as it always was).</summary>
    public static bool Opens(Jelly jelly, BayService service) => Opener(service) is not { } n || jelly.Freed.Contains(n.Id);

    /// <summary>The caption as a neighbour swims off home.</summary>
    public static string HomeCaption(Neighbour n) => $"{n.Called ?? n.Name} has gone home to Lantern Bay";

    /// <summary>
    /// What she says when Clementine swims close: her lines in turn, one more each visit (her late line among them once
    /// it is hers to say).
    /// </summary>
    public static string Line(Neighbour n, int visit, bool late = false)
    {
        var lines = late && n.Late is { } more ? n.Lines.Append(more).ToArray() : n.Lines;
        return lines[Math.Abs(visit) % lines.Length];
    }

    /// <summary>Her late lines are hers to say: the Coral Carnival's neighbour is home.</summary>
    public static bool Late(Jelly jelly) => jelly.Freed.Contains("bubbles");
}

/// <summary>
/// Fennel's Brood Bed (docs/NPC-BOSSES-PROPOSAL.md §6.3): shells she leaves with him are kept, never lost when she
/// dims (the shells she carries go with her lost light, as ever).
/// </summary>
public static class BroodBed
{
    /// <summary>She leaves every shell she carries with him.</summary>
    public static void Leave(Jelly jelly, PlaneRun run)
    {
        jelly.Banked += run.Shells;
        run.Shells = 0;
    }

    /// <summary>She takes back every shell he keeps for her.</summary>
    public static void TakeBack(Jelly jelly, PlaneRun run)
    {
        run.Shells += jelly.Banked;
        jelly.Banked = 0;
    }
}
