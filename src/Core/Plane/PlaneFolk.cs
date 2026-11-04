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
public sealed record Neighbour(
    string Id, string Name, string Species, string Boss, string Cave, int Level, int Floors, BayService Opens, string[] Lines);

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
        }),
        new("kip", "Kip", "cleaner wrasse", "Nine-Fin Kip", "The Cleaning Station", 3, 3, BayService.Mirror, new[]
        {
            "Hold still. You've got Leak in your frills. …There. Family colour, all the way through.",
            "Your sister glowed just like that. I did her rim twice, she was so proud of it.",
            "The mirror's clean. Mostly. Don't look at the corners, I'm getting to them.",
        }),
    };

    /// <summary>The neighbours whose boss waits in a great cave now (the rest come with theirs).</summary>
    public static IEnumerable<Neighbour> Built => All;

    public static Neighbour? Get(string? id) => id is null ? null : All.FirstOrDefault(n => n.Id == id);

    /// <summary>The neighbour who opens a service, if her boss is in the game.</summary>
    public static Neighbour? Opener(BayService service) => Built.FirstOrDefault(n => n.Opens == service);

    /// <summary>The service is open in her bay: its neighbour is home (or has no boss yet, so it is open as it always was).</summary>
    public static bool Opens(Jelly jelly, BayService service) => Opener(service) is not { } n || jelly.Freed.Contains(n.Id);

    /// <summary>The caption as a neighbour swims off home.</summary>
    public static string HomeCaption(Neighbour n) => $"{n.Name} has gone home to Lantern Bay";

    /// <summary>What she says when Clementine swims close: her lines in turn, one more each visit.</summary>
    public static string Line(Neighbour n, int visit) => n.Lines[Math.Abs(visit) % n.Lines.Length];
}
