using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's folk in the game (docs/NPC-BOSSES-PROPOSAL.md): a great cave's boss is a neighbour, named for its world
/// while she is still corrupted; freed, she goes home to Lantern Bay (the caption as she swims off), lives at her home
/// there, talks when Clementine swims up, and opens her service in the bay.
/// </summary>
public partial class TopDownMain
{
    FolkView _folkView = null!;
    FolkTalk _folkTalk = null!;

    void BuildFolk()
    {
        _folkView = new FolkView();
        AddChild(_folkView);
        _folkTalk = new FolkTalk();
        _hudRoot.AddChild(_folkTalk);
    }

    /// <summary>The neighbour whose great cave she is in, or null (a region, or a minor cave).</summary>
    Neighbour? GreatHere => _place == ReefPlace.Cave ? OpenReef.GreatOf(LoadMap(_region), _mouth) : null;

    /// <summary>The open reef's world options: in a great cave's arena, its neighbour's boss, till she is freed.</summary>
    PlaneOptions ReefOptions()
    {
        var options = PlaneOptions.OpenReef;
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

    /// <summary>A frame of the bay's folk: each at her home, and the one she is near talking.</summary>
    void StepFolk(float dt)
    {
        _folkView.Sync(_world, dt);
        _folkTalk.Sync(_pause.IsOpen || _mirror.IsOpen ? null : _folkView.Near, dt);
    }

    /// <summary>A bay service is open: its neighbour is home (or not yet in the game).</summary>
    bool BayOpen(BayService service) => _jelly is not { } jelly || Folk.Opens(jelly, service);
}
