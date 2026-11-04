using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's folk in the game (docs/NPC-BOSSES-PROPOSAL.md): a great cave's boss is a neighbour, named for its world
/// while she is still corrupted; freed, she goes home to Lantern Bay (the caption as she swims off), lives at her home
/// there, talks when Clementine swims up, and opens her service in the bay (a drawer at her home, for the services
/// that have one: Fennel's Brood Bed).
/// </summary>
public partial class TopDownMain
{
    FolkView _folkView = null!;
    FolkTalk _folkTalk = null!;
    FolkMenu _folkMenu = null!;
    /// <summary>The neighbour whose drawer is open, and whether hers opens again (once Clementine has swum away from her home).</summary>
    Neighbour? _serving;
    bool _serveArmed = true;

    /// <summary>How near a neighbour's home Clementine swims for her service's drawer to open (metres).</summary>
    const float ServeReach = 2.2f;

    void BuildFolk(CanvasLayer pauseLayer)
    {
        _folkView = new FolkView();
        AddChild(_folkView);
        _folkTalk = new FolkTalk();
        _hudRoot.AddChild(_folkTalk);
        _folkMenu = new FolkMenu();
        pauseLayer.AddChild(_folkMenu);
        _folkMenu.Closed += CloseFolkMenu;
    }

    /// <summary>The neighbour whose great cave she is in, or null (a region, or a minor cave).</summary>
    Neighbour? GreatHere => _place == ReefPlace.Cave ? OpenReef.GreatOf(LoadMap(_region), _mouth) : null;

    /// <summary>The open reef's world options: in a great cave's arena, its neighbour's boss, till she is freed.</summary>
    PlaneOptions ReefOptions()
    {
        var options = PlaneOptions.OpenReef;
        // The bay's shell shop stands empty till Ottilie, its collector, is home.
        options.ShopShut = !BayOpen(BayService.PearlCollector);
        if (GreatHere is { } n && _map.HasBoss && _jelly is { } jelly && !jelly.Freed.Contains(n.Id))
            options.Boss = FolkBosses.For(n.Id, n.Level, _map.Menace);
        return options;
    }

    /// <summary>A boss freed: a neighbour goes home to the bay (her jelly keeps her freed), with its caption.</summary>
    void FolkFreed()
    {
        if (_world.Boss?.Spec.Folk is not { } id || Folk.Get(id) is not { } n || _jelly is not { } jelly) return;
        jelly.Freed.Add(n.Id);
        SaveJelly();
        Say(Folk.HomeCaption(n));
    }

    /// <summary>A frame of the bay's folk: each at her home, the one she is near talking, and her service's drawer at her home.</summary>
    void StepFolk(float dt)
    {
        _folkView.Sync(_world, dt);
        bool drawer = _pause.IsOpen || _mirror.IsOpen || _fanMenu.IsOpen || _folkMenu.IsOpen;
        _folkTalk.Sync(drawer ? null : _folkView.Near, dt);
        var home = _folkView.AtHome(_world.Player.Position, ServeReach);
        if (home is null) _serveArmed = true;
        else if (_serveArmed && !drawer && !_debug.Visible && HasDrawer(home)) OpenFolkMenu(home);
    }

    /// <summary>A bay service is open: its neighbour is home (or not yet in the game).</summary>
    bool BayOpen(BayService service) => _jelly is not { } jelly || Folk.Opens(jelly, service);

    /// <summary>The services that are a drawer at their neighbour's home (the fan and the mirror are places of their own).</summary>
    static bool HasDrawer(Neighbour n) => n.Opens is BayService.BroodBed or BayService.DuskDance;

    void OpenFolkMenu(Neighbour n)
    {
        _serveArmed = false;
        _serving = n;
        _sfx.PlayPitched("bell_ring", 0.85f, -10f);
        var (title, summary, choices) = Service(n);
        _folkMenu.Open(title, summary, choices, $"Leave {n.Name}");
    }

    void RefillFolkMenu()
    {
        if (_serving is not { } n) return;
        var (title, summary, choices) = Service(n);
        _folkMenu.Fill(title, summary, choices);
    }

    void CloseFolkMenu()
    {
        _serving = null;
        _folkMenu.Close();
        // The click that closed it must not fire a shot.
        _fireBlocked = true;
    }

    /// <summary>A neighbour's service as her drawer shows it: its name, how things stand, what it does.</summary>
    (string Title, string Summary, List<FolkMenu.Choice> Choices) Service(Neighbour n)
    {
        var jelly = _jelly!;
        var run = _world.Run;
        switch (n.Opens)
        {
            case BayService.DuskDance:
            {
                // Bubbles's Dusk Dance: every skill point she has spent comes back to lay again.
                int spent = run.Upgrades;
                return ("The Dusk Dance",
                    $"At the soft end of the day Bubbles dances, rising in the water. Dance with her, and every skill point you have spent comes back to you to lay again. You have spent {spent}.",
                    new List<FolkMenu.Choice>
                    {
                        new($"Dance with her  ({spent} {(spent == 1 ? "point" : "points")} back)", spent > 0, () =>
                        {
                            run.Relearn();
                            Kept("Your points are yours to lay again");
                        }),
                    });
            }
            default:
                // Fennel's Brood Bed: the shells she leaves with him are never lost when she dims.
                return ("The Brood Bed",
                    $"Fennel keeps what you leave with him the way he keeps his brood: it is never lost when you dim. You carry {run.Shells} silver shells; he keeps {jelly.Banked}.",
                    new List<FolkMenu.Choice>
                    {
                        new($"Leave your shells with him  ({run.Shells})", run.Shells > 0, () =>
                        {
                            BroodBed.Leave(jelly, run);
                            Kept("Fennel keeps your shells");
                        }),
                        new($"Take them back  ({jelly.Banked})", jelly.Banked > 0, () =>
                        {
                            BroodBed.TakeBack(jelly, run);
                            Kept("Your shells are with you again");
                        }),
                    });
        }
    }

    /// <summary>A service done: saved, said, and the drawer afresh.</summary>
    void Kept(string said)
    {
        SaveJelly();
        _sfx.PlayPitched("bell_ring", 1.15f, -8f);
        Say(said);
        RefillFolkMenu();
    }
}
