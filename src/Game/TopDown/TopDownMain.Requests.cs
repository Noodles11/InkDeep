using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using OctoShoots.Core.Saves;
using OctoShoots.Game.Settings;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The Tide Fan's requests in the game (docs/archive/QUESTS-PROPOSAL.md): the fan in the bay and its drawer, its ribbons tied
/// afresh each time she comes home, what she does out in a region's caves carrying them forward, untying one done (its
/// pearl absorbed at the fan, its shells spilled, its experience given), the pause menu's Requests tab and the HUD's
/// kelp ribbons, and the named mouths ringed on the maps.
/// </summary>
public partial class TopDownMain
{
    FanView _fanView = null!;
    FanMenu _fanMenu = null!;
    RequestRibbons _ribbons = null!;
    bool _fanArmed = true;
    int _ashSeen;
    /// <summary>The fan drawer's ribbons, in the order it lists them: their region and place.</summary>
    readonly List<(string Region, int Place)> _fanShown = new();

    void BuildRequests(CanvasLayer pauseLayer)
    {
        _fanMenu = new FanMenu();
        pauseLayer.AddChild(_fanMenu);
        _fanMenu.Acted += (carried, index) =>
        {
            if (carried) UntieRequest(index);
            else TakeRequest(index);
        };
        _fanMenu.Closed += CloseFan;
        _ribbons = new RequestRibbons();
        _hudRoot.AddChild(_ribbons);
        _pause.RequestLetGo += index =>
        {
            if (_jelly is not { } jelly) return;
            Requests.LetGo(jelly, index);
            KeepJelly();
            _sfx.PlayPitched("pop", 0.8f, -10f);
            FillPauseRequests();
        };
        _pause.RibbonsToggled += on =>
        {
            _view.RequestRibbons = on;
            SettingsStore.Save(_tuning, _view);
        };
    }

    /// <summary>A dive's requests name no mouth (docs/DIVE.md §7).</summary>
    static IReadOnlyList<string> MouthNames(string region) => System.Array.Empty<string>();

    /// <summary>What a request's trouble is called: the deep.</summary>
    static string RegionTitle(string region) => "the deep";

    /// <summary>The fan's one region: the dive's.</summary>
    static IEnumerable<string> FanRegions => new[] { Requests.Dive };

    /// <summary>
    /// Her jelly written to the save as it stands: what is hers already (her requests, her chains, her folk); what she
    /// carries is banked at havens.
    /// </summary>
    void KeepJelly()
    {
        if (_jellySaved && !_debugRun) GameSave.Write();
    }

    /// <summary>Home in the bay: new ribbons tied in each region's empty places on the fan.</summary>
    void FillFan()
    {
        // Grandmother Tull ties the bay's troubles to the fan: none till she is home.
        if (_jelly is not { } jelly || _world is null || !BayOpen(BayService.TideFan)) return;
        // Its asks at the depth she is diving.
        Requests.FillDive(jelly, System.Math.Max(1, 2 * _id.ShownDepth - 1), _world.Run.CanOffer, _catalog);
    }

    /// <summary>A request as a drawer or the pause menu lists it (Enabled: for a carried one, done).</summary>
    FanMenu.Entry EntryOf(Request r, string action, bool enabled)
    {
        var mouths = MouthNames(r.Region);
        var (who, line) = Requests.PosterOf(r, mouths);
        var pearl = r.Pearl is { } id && _catalog is not null && _catalog.TryGet(id, out var item) ? item : null;
        return new FanMenu.Entry(who, line, Requests.Caption(r, RegionTitle(r.Region), mouths), pearl, r.Shells, r.Xp, RequestRibbons.KindColour(r.Kind), action, enabled);
    }

    void OpenFan()
    {
        if (_jelly is null) return;
        _fanArmed = false;
        _sfx.PlayPitched("bell_ring", 0.8f, -10f);
        var (fan, carried) = FanEntries();
        _fanMenu.Open(fan, carried, RequestTuning.Carry);
    }

    (List<FanMenu.Entry> Fan, List<FanMenu.Entry> Carried) FanEntries()
    {
        var jelly = _jelly!;
        _fanShown.Clear();
        var fan = new List<FanMenu.Entry>();
        bool room = jelly.Carried.Count < RequestTuning.Carry;
        foreach (string region in FanRegions)
        {
            var places = Requests.Places(jelly, region);
            for (int i = 0; i < places.Count; i++)
            {
                if (places[i] is not { } r) continue;
                _fanShown.Add((region, i));
                fan.Add(EntryOf(r, room ? "Take" : "You carry two already", room));
            }
        }
        var carried = jelly.Carried.Select(r => EntryOf(r, r.Done ? "Untie" : "Not done yet", r.Done)).ToList();
        return (fan, carried);
    }

    void RefillFan()
    {
        var (fan, carried) = FanEntries();
        _fanMenu.Fill(fan, carried, RequestTuning.Carry);
    }

    void CloseFan()
    {
        _fanMenu.Close();
        _fireBlocked = true;
    }

    void TakeRequest(int index)
    {
        if (_jelly is not { } jelly || index < 0 || index >= _fanShown.Count) return;
        var (region, place) = _fanShown[index];
        if (!Requests.Take(jelly, region, place)) return;
        _sfx.PlayPitched("bell_ring", 1.1f, -8f);
        KeepJelly();
        RefillFan();
    }

    /// <summary>A request done, untied at the fan: its pearl floats to her bell and is absorbed, its shells spill, its experience is hers.</summary>
    void UntieRequest(int index)
    {
        if (_jelly is not { } jelly || Requests.Untie(jelly, index) is not { } r) return;
        var her = _world.Player.Position;
        if (r.Pearl is { } pearl) _world.Pearls.Add(new PlanePearl { ItemId = pearl, Position = her, Placed = true });
        var rng = new RandomNumberGenerator();
        for (int i = 0; i < r.Shells; i++)
        {
            float a = rng.RandfRange(0f, Mathf.Tau), speed = rng.RandfRange(2f, 5f);
            _world.Shells.Add(new PlaneShell { Position = her, Velocity = new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed });
        }
        _world.GiveXp(r.Xp);
        _sfx.PlayPitched("bell_ring", 1.25f, -4f);
        Say("Request untied");
        NoteChain(StepKind.Untie);
        KeepJelly();
        RefillFan();
    }

    /// <summary>The sim's events that answer a request she carries, anywhere in the dive.</summary>
    void NoteRequest(in PlaneEvent e)
    {
        if (_jelly is not { } jelly) return;
        // Every leak sealed counts (docs/DIVE.md §7), a leak cave's for its own ask too.
        var noted = e.Type switch
        {
            PlaneEventType.LeakSealed => Requests.Note(jelly, Requests.Seal, Requests.Dive).Concat(InDiveCave ? Requests.Note(jelly, Requests.Den, Requests.Dive) : new()).ToList(),
            PlaneEventType.PrimeFreed => Requests.Note(jelly, Requests.Prime, Requests.Dive),
            _ => null,
        };
        if (e.Type == PlaneEventType.PrimeFreed && e.Size > 0f) Say("The swallowed school swims home");
        if (noted is { Count: > 0 }) RequestDone();
        else if (noted is not null) KeepJelly();
    }

    void RequestDone()
    {
        _sfx.PlayPitched("bell_ring", 1.4f, -5f);
        Say("Request done · back to the Tide Fan");
        KeepJelly();
    }

    /// <summary>A frame of the requests: the fan's drawer at its foot, ash washed in a cave, the ribbons on the HUD and the fan, the maps' marks.</summary>
    void StepRequests()
    {
        var jelly = _jelly;
        if (jelly is not null && _map.Fan is not null)
        {
            if (!_world.AtFan) _fanArmed = true;
            else if (_fanArmed && !_pause.IsOpen && !_mirror.IsOpen && !_debug.Visible && BayOpen(BayService.TideFan)) OpenFan();
        }
        if (jelly is not null && InDiveCave && _world.AshWashed > _ashSeen)
        {
            if (Requests.Note(jelly, Requests.Ash, Requests.Dive, amount: _world.AshWashed - _ashSeen).Count > 0) RequestDone();
            _ashSeen = _world.AshWashed;
        }
        // A haven banks as she goes: what she buys, absorbs or is paid there is hers for good at once.
        if (jelly is not null && _id.Haven) KeepBanked(jelly);
        var shown = new List<RequestRibbons.Ribbon>();
        if (jelly is not null && _view.RequestRibbons)
            foreach (var r in jelly.Carried)
                shown.Add(new RequestRibbons.Ribbon(Requests.Short(r, MouthNames(r.Region)), RequestRibbons.KindColour(r.Kind), TokenColour(r), r.Done));
        // The bay's folk's chains, in gold, after the requests.
        if (jelly is not null && _view.RequestRibbons) shown.AddRange(ChainRibbons());
        _ribbons.Show(shown);
        var marks = new List<(System.Numerics.Vector2 At, Color Colour)>();
        if (jelly is not null)
        {
            if (_map.Fan is { } fan)
            {
                var ribbons = new List<FanView.Ribbon>();
                // (The fan hangs bare till its neighbour is home to tie its ribbons.)
                foreach (string region in BayOpen(BayService.TideFan) ? FanRegions : System.Array.Empty<string>())
                    foreach (var r in Requests.Places(jelly, region))
                        if (r is not null) ribbons.Add(new FanView.Ribbon(RequestRibbons.KindColour(r.Kind), TokenColour(r), false));
                foreach (var r in jelly.Carried.Where(r => r.Done)) ribbons.Add(new FanView.Ribbon(RequestRibbons.KindColour(r.Kind), TokenColour(r), true));
                _fanView.SetRibbons(ribbons);
                // The fan on the maps, while a request waits there to be untied.
                if (jelly.Carried.Any(r => r.Done)) marks.Add((fan, new Color(1f, 0.82f, 0.4f)));
            }
            // A dive's level (docs/DIVE.md §7): what her asks want here, ringed (the open leaks for a seal ask, the leak
            // caves' open mouths for a cave ask), and the hole once it is open.
            if (!_id.Haven && !InDiveCave)
            {
                foreach (var r in jelly.Carried.Where(r => !r.Done && r.Region == Requests.Dive))
                {
                    if (r.Kind == Requests.Seal)
                        foreach (var v in _world.Leaks)
                            if (!v.Sealed && !v.Hidden) marks.Add((v.Position, RequestRibbons.KindColour(r.Kind)));
                    if (r.Kind is Requests.Den or Requests.Ash)
                        for (int i = 0; i < _map.Mouths.Count; i++)
                            if (!_world.ShutMouths.Contains(i) && _map.LeakMouth(i)) marks.Add((_map.Mouths[i].At, RequestRibbons.KindColour(r.Kind)));
                }
                if (_world.ExitOpen) marks.Add((_map.Exit.Position, new Color(1f, 0.82f, 0.4f)));
            }
        }
        marks.AddRange(ChainMarks());
        _minimap.SetMarks(marks);
        _fullMap.Marks.Clear();
        _fullMap.Marks.AddRange(marks);
    }

    /// <summary>A request's token's colour: its pearl's own (its light), or gold for shells.</summary>
    Color TokenColour(Request r)
    {
        if (r.Pearl is { } id && _catalog is not null && _catalog.TryGet(id, out var item) && item.Pearl is { Colors.Count: > 0 } look)
            return Color.FromHtml(look.Colors[look.Colors.Count > 1 ? 1 : 0]);
        return new Color(1f, 0.8f, 0.35f);
    }

    void FillPauseRequests()
    {
        var carried = _jelly?.Carried.Select(r => EntryOf(r, "", r.Done)).ToList() ?? new List<FanMenu.Entry>();
        _pause.FillRequests(carried, _view.RequestRibbons, _jelly is not null);
    }
}
