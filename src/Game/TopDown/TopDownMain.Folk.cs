using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's folk in the game (docs/FOLK.md): a great cave's boss is a neighbour, named for its world
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

    /// <summary>A boss freed: a neighbour goes home to the bay (her jelly keeps her freed), with its caption.</summary>
    void FolkFreed()
    {
        if (_world.Boss?.Spec.Folk is not { } id || Folk.Get(id) is not { } n || _jelly is not { } jelly) return;
        jelly.Freed.Add(n.Id);
        // Hers for good at once, dim as she may later in the dive.
        KeepJelly();
        Say(Folk.HomeCaption(n));
    }

    /// <summary>A frame of the bay's folk: each at her home, the one she is near talking, and her service's drawer at her home.</summary>
    void StepFolk(float dt)
    {
        _folkView.Sync(_world, dt);
        bool drawer = _pause.IsOpen || _mirror.IsOpen || _fanMenu.IsOpen || _folkMenu.IsOpen;
        _folkTalk.Sync(drawer ? null : _folkView.Near, _jelly is { } jelly && Folk.Late(jelly), dt);
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

    // ───────────────────────── the key quest chains ─────────────────────────

    /// <summary>The chains' gold, on the HUD's ribbons and the maps.</summary>
    static readonly Color ChainGold = new(1f, 0.8f, 0.36f);

    /// <summary>Something she did answers a chain's step: the chains it finishes pay their pearl, with their neighbour's line.</summary>
    void NoteChain(StepKind kind, string? zone = null, string? folk = null, System.Numerics.Vector2? at = null)
    {
        if (_jelly is not { } jelly) return;
        var before = Chains.Active(jelly).Select(a => (a.Chain.Folk, a.Step)).ToList();
        var finished = Chains.Note(jelly, kind, zone, folk, at);
        bool moved = !before.SequenceEqual(Chains.Active(jelly).Select(a => (a.Chain.Folk, a.Step)));
        foreach (var chain in finished)
        {
            // Her active pearl is hers (owned, into a free slot or her stash), with her line for it.
            _world.Run.Add(chain.Pearl);
            var who = Folk.Get(chain.Folk)?.Name ?? chain.Folk;
            string pearl = _catalog is not null && _catalog.TryGet(chain.Pearl, out var item) ? item.Name : chain.Pearl;
            Say($"{chain.Title} · done. {who}: \u201c{chain.Line}\u201d · {pearl} is yours");
            _sfx.PlayPitched("bell_ring", 1.05f, -3f);
        }
        if (finished.Count == 0 && moved) _sfx.PlayPitched("bell_ring", 1.3f, -9f);
        if (moved || finished.Count > 0) KeepJelly();
    }

    /// <summary>The sim's events that answer a chain's step (docs/DIVE.md §7): a prime freed or a leak sealed, in the depth she is diving.</summary>
    void NoteChainEvent(in PlaneEvent e)
    {
        if (_jelly is null) return;
        string depth = Chains.DepthZone(_id.ShownDepth);
        if (e.Type == PlaneEventType.PrimeFreed) NoteChain(StepKind.Prime, zone: depth);
        else if (e.Type == PlaneEventType.LeakSealed) NoteChain(StepKind.Seal, zone: depth);
    }

    /// <summary>
    /// A dive's level entered (docs/DIVE.md §7): its depth and cycle reached, a leak cave gone into (a find there), a haven
    /// reached (home).
    /// </summary>
    void NoteDiveChains()
    {
        if (_jelly is null) return;
        if (_id.Haven) NoteChain(StepKind.Home);
        // (A labyrinth is no find.)
        else if (InDiveCave)
        {
            if (!InLabyrinth) NoteChain(StepKind.Find, zone: Chains.DepthZone(_id.ShownDepth));
        }
        else if (!_id.IsTutorial)
        {
            NoteChain(StepKind.Enter, zone: Chains.DepthZone(_id.ShownDepth));
            NoteChain(StepKind.Enter, zone: Chains.CycleZone(_id.Cycle));
        }
    }

    /// <summary>The chains' steps as the HUD's kelp ribbons show them (gold).</summary>
    IEnumerable<RequestRibbons.Ribbon> ChainRibbons() =>
        _jelly is { } jelly ? Chains.Active(jelly).Select(a => new RequestRibbons.Ribbon(Chains.Short(a.Step, a.Count), ChainGold, ChainGold, false))
            : Enumerable.Empty<RequestRibbons.Ribbon>();

    /// <summary>The chains' places on the maps: a find in this depth's leak caves rings the level's open mouths.</summary>
    IEnumerable<(System.Numerics.Vector2 At, Color Colour)> ChainMarks()
    {
        if (_jelly is not { } jelly || _id.Haven || InDiveCave) yield break;
        string depth = Chains.DepthZone(_id.ShownDepth);
        if (!Chains.Active(jelly).Any(a => a.Step.Kind == StepKind.Find && a.Step.Zone == depth)) yield break;
        for (int i = 0; i < _map.Mouths.Count; i++)
            if (!_world.ShutMouths.Contains(i) && _map.LeakMouth(i)) yield return (_map.Mouths[i].At, ChainGold);
    }

    /// <summary>A service done: saved, said, and the drawer afresh.</summary>
    void Kept(string said)
    {
        KeepJelly();
        _sfx.PlayPitched("bell_ring", 1.15f, -8f);
        Say(said);
        RefillFolkMenu();
    }
}
