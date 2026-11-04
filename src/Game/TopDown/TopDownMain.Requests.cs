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
/// The Tide Fan's requests in the game (docs/QUESTS-PROPOSAL.md): the fan in the bay and its drawer, its ribbons tied
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
            SaveJelly();
            _sfx.PlayPitched("pop", 0.8f, -10f);
            FillPauseRequests();
        };
        _pause.RibbonsToggled += on =>
        {
            _view.RequestRibbons = on;
            SettingsStore.Save(_tuning, _view);
        };
    }

    /// <summary>A region's mouths by what the bay calls them ("west", "south", "east").</summary>
    static IReadOnlyList<string> MouthNames(string region) => LoadMap(region).Mouths.Select(m => m.Name).ToList();

    /// <summary>Home in the bay: new ribbons tied in each region's empty places on the fan.</summary>
    void FillFan()
    {
        if (_jelly is not { } jelly || _world is null) return;
        if (!SeedCode.TryParse(jelly.Seed, out var seed)) seed = _seed;
        foreach (string region in OpenReef.Regions)
        {
            var hand = LoadMap(region);
            bool Den(int mouth) => LevelPlan.ForCave(seed, OpenReef.CaveId(region, hand.Depth, OpenReef.CaveBehind(jelly, region, mouth)), OpenReef.CaveMenace(hand, mouth)).Curses > 0;
            Requests.Fill(jelly, region, m => OpenReef.CaveLevel(hand, m), hand.Mouths.Count, Den, _world.Run.CanOffer, _catalog);
        }
    }

    /// <summary>A request as a drawer or the pause menu lists it (Enabled: for a carried one, done).</summary>
    FanMenu.Entry EntryOf(Request r, string action, bool enabled)
    {
        var mouths = MouthNames(r.Region);
        var (who, line) = Requests.PosterOf(r, mouths);
        var pearl = r.Pearl is { } id && _catalog is not null && _catalog.TryGet(id, out var item) ? item : null;
        return new FanMenu.Entry(who, line, Requests.Caption(r, LoadMap(r.Region).Title, mouths), pearl, r.Shells, r.Xp, RequestRibbons.KindColour(r.Kind), action, enabled);
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
        foreach (string region in OpenReef.Regions)
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
        SaveJelly();
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
        SaveJelly();
        RefillFan();
    }

    /// <summary>The sim's events that answer a request she carries (in a region's leak cave).</summary>
    void NoteRequest(in PlaneEvent e)
    {
        if (_jelly is not { } jelly || _place != ReefPlace.Cave) return;
        var done = e.Type switch
        {
            PlaneEventType.LeakSealed => Requests.Note(jelly, Requests.Seal, _region, _mouth),
            PlaneEventType.PrimeFreed => Requests.Note(jelly, Requests.Prime, _region),
            PlaneEventType.CurseCleared => Requests.Note(jelly, Requests.Den, _region, _mouth),
            _ => null,
        };
        if (e.Type == PlaneEventType.PrimeFreed && e.Size > 0f) Say("The swallowed school swims home");
        if (done is { Count: > 0 }) RequestDone();
    }

    void RequestDone()
    {
        _sfx.PlayPitched("bell_ring", 1.4f, -5f);
        Say("Request done · back to the Tide Fan");
        SaveJelly();
    }

    /// <summary>A frame of the requests: the fan's drawer at its foot, ash washed in a cave, the ribbons on the HUD and the fan, the maps' marks.</summary>
    void StepRequests()
    {
        var jelly = _jelly;
        if (jelly is not null && _map.Fan is not null)
        {
            if (!_world.AtFan) _fanArmed = true;
            else if (_fanArmed && !_pause.IsOpen && !_mirror.IsOpen && !_debug.Visible) OpenFan();
        }
        if (jelly is not null && _place == ReefPlace.Cave && _world.AshWashed > _ashSeen)
        {
            if (Requests.Note(jelly, Requests.Ash, _region, amount: _world.AshWashed - _ashSeen).Count > 0) RequestDone();
            _ashSeen = _world.AshWashed;
        }
        var shown = new List<RequestRibbons.Ribbon>();
        if (jelly is not null && Reef && _view.RequestRibbons)
            foreach (var r in jelly.Carried)
                shown.Add(new RequestRibbons.Ribbon(Requests.Short(r, MouthNames(r.Region)), RequestRibbons.KindColour(r.Kind), TokenColour(r), r.Done));
        _ribbons.Show(shown);
        var marks = new List<(System.Numerics.Vector2 At, Color Colour)>();
        if (jelly is not null)
        {
            if (_map.Fan is { } fan)
            {
                var ribbons = new List<FanView.Ribbon>();
                foreach (string region in OpenReef.Regions)
                    foreach (var r in Requests.Places(jelly, region))
                        if (r is not null) ribbons.Add(new FanView.Ribbon(RequestRibbons.KindColour(r.Kind), TokenColour(r), false));
                foreach (var r in jelly.Carried.Where(r => r.Done)) ribbons.Add(new FanView.Ribbon(RequestRibbons.KindColour(r.Kind), TokenColour(r), true));
                _fanView.SetRibbons(ribbons);
                // The fan on the maps, while a request waits there to be untied.
                if (jelly.Carried.Any(r => r.Done)) marks.Add((fan, new Color(1f, 0.82f, 0.4f)));
            }
            // A region's named mouths, ringed in their request's colour.
            if (_place == ReefPlace.Region)
                foreach (var r in jelly.Carried)
                    if (!r.Done && r.Region == _region && r.Mouth >= 0 && r.Mouth < _map.Mouths.Count) marks.Add((_map.Mouths[r.Mouth].At, RequestRibbons.KindColour(r.Kind)));
        }
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
        _pause.FillRequests(carried, _view.RequestRibbons, Reef && _jelly is not null);
    }
}
