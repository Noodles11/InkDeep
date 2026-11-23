using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Game.TopDown;

namespace OctoShoots.Game.Title;

/// <summary>
/// The title's phases (docs/TITLE-MENU.md): the opening out of the loader, her beam on the items, an item chosen (its
/// text dissolving, her push wave throwing the rest off), the dive below to her jellies, and the climb back.
/// </summary>
public partial class TitleMain
{
    /// <summary>An item in a murkling's colour (its ink's deep violet), its dark edge; lit, white with her light's outline.</summary>
    static readonly Color Murk = new(0.36f, 0.16f, 0.56f), MurkEdge = new(0.05f, 0.02f, 0.09f, 0.55f);
    static readonly Color LitText = new(1f, 0.98f, 0.93f), LitEdge = new(1f, 0.82f, 0.45f, 0.95f);
    const int LitOutline = 12;

    static Shader? _dissolve;
    static Shader DissolveShader => _dissolve ??= GD.Load<Shader>("res://assets/shaders/menu_dissolve.gdshader");

    /// <summary>The item her beam lights: the one the pointer is over, or (with the controller or the keys) the one in focus.</summary>
    Button? Lit()
    {
        if (_phase != Phase.Menu || _card is not null || _launching || _world is null) return null;
        if (!_byPad) return _hovered;
        return GetViewport().GuiGetFocusOwner() is Button b && _items.Any(it => it.Button == b) ? b : null;
    }

    Button? _shownLit;

    /// <summary>An item chosen, its text still dissolving in her beam.</summary>
    bool Dissolving => _phase == Phase.Choosing && _phaseTime < DissolveSeconds;

    void SetPhase(Phase phase)
    {
        _phase = phase;
        _phaseTime = 0f;
    }

    void StepPhase(float dt)
    {
        switch (_phase)
        {
            case Phase.Revealing: StepReveal(); break;
            case Phase.Menu: StepMenu(dt); break;
            case Phase.Choosing: StepChoose(); break;
            case Phase.Diving: StepDive(down: true); break;
            case Phase.Climbing: StepDive(down: false); break;
            case Phase.Returning: StepReturn(); break;
        }
        ShowLit(_phase == Phase.Menu ? Lit() : null);
    }

    static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t, 0f, 1f));

    // ───────────────────────── the opening ─────────────────────────

    /// <summary>
    /// The opening (a second): the loader whitens and starts to glow; then it is the opening, in its own shape at first,
    /// growing, with a round one spreading from it in a white glow until the whole screen is uncovered.
    /// </summary>
    void StepReveal()
    {
        float t = Reduced ? RevealSeconds : _phaseTime;
        var size = GetViewport().GetVisibleRect().Size;
        var centre = size * 0.5f;
        float half = (_bootLoader?.Texture?.GetSize().X ?? 64f) * 0.5f;
        var m = (ShaderMaterial)_reveal.Material;
        m.SetShaderParameter("centre", centre);
        float w = Ease(t / Whiten), e = Mathf.Clamp((t - Whiten) / (RevealSeconds - Whiten), 0f, 1f);
        if (_bootLoader is { } loader)
        {
            _loaderLook.SetShaderParameter("white", w);
            loader.Scale = Vector2.One * (1f + 0.2f * w);
            loader.Visible = t < Whiten;
        }
        float diagonal = centre.Length();
        // Its glow: starting round the whitening loader, spreading with the opening, gone as it is done.
        m.SetShaderParameter("glow", w * (1f - Ease((t - 0.8f) / 0.2f)));
        m.SetShaderParameter("glow_px", 18f + 260f * e * e);
        m.SetShaderParameter("gonad_px", t < Whiten ? 0f : half * 1.2f * Mathf.Pow(40f, e));
        m.SetShaderParameter("open_px", diagonal * 1.15f * Mathf.Pow(e, 1.6f));
        m.SetShaderParameter("fill", t < Whiten ? 0f : 1f - Ease(e / 0.45f));
        if (t < RevealSeconds) return;
        _reveal.Visible = false;
        _bootLoader?.QueueFree();
        _bootLoader = null;
        SetPhase(Phase.Menu);
    }

    // ───────────────────────── the menu ─────────────────────────

    /// <summary>Her beam on an item: it sprays her light and the corruption's brackets as a mob in her beam does.</summary>
    void StepMenu(float dt)
    {
        if (Lit() is { } lit) _spray.Lit(PageRect(lit), HerOnPage(), dt);
        // (Review: --title-pick chooses its item after a moment in her beam.)
        if (_pickPending && _phaseTime > 1.2f && _autoPick is { } pick && _items.Any(it => it.Id == pick))
        {
            _pickPending = false;
            Item(pick).Button.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    /// <summary>The lit item white, outlined in her light; the others back in a murkling's colour.</summary>
    void ShowLit(Button? lit)
    {
        if (lit == _shownLit) return;
        if (_shownLit is { } was && IsInstanceValid(was))
        {
            foreach (string state in new[] { "font_color", "font_pressed_color", "font_hover_color", "font_focus_color", "font_hover_pressed_color" }) was.AddThemeColorOverride(state, Murk);
            was.AddThemeColorOverride("font_outline_color", MurkEdge);
            was.AddThemeConstantOverride("outline_size", 4);
        }
        _shownLit = lit;
        if (lit is null) return;
        foreach (string state in new[] { "font_color", "font_pressed_color", "font_hover_color", "font_focus_color", "font_hover_pressed_color" }) lit.AddThemeColorOverride(state, LitText);
        lit.AddThemeColorOverride("font_outline_color", LitEdge);
        lit.AddThemeConstantOverride("outline_size", LitOutline);
    }

    /// <summary>A control's rect on the menu's page.</summary>
    Rect2 PageRect(Control c)
    {
        var r = c.GetGlobalRect();
        return new Rect2(r.Position / UiScale, r.Size / UiScale);
    }

    // ───────────────────────── choosing ─────────────────────────

    /// <summary>
    /// An item chosen: its text starts to dissolve outward along her beam (from the side it strikes), and her push wave
    /// runs out from her, throwing the other items, the logo and the footer off the screen.
    /// </summary>
    void Choose(string id, Action pressed)
    {
        if (_phase != Phase.Menu || _launching || _world is null) return;
        var item = _items.First(it => it.Id == id);
        _chosen = item;
        _chosenAction = pressed;
        _shownLit = null;
        var her = HerOnPage() * UiScale;
        var centre = item.Button.GetGlobalRect().GetCenter();
        _strikeFrom = her;
        _strikeDir = (centre - her).LengthSquared() > 1f ? (centre - her).Normalized() : Vector2.Up;
        foreach (var c in new Control[] { item.Button, item.Detail })
        {
            var m = (ShaderMaterial)c.Material;
            m.SetShaderParameter("origin", _strikeFrom);
            m.SetShaderParameter("dir", _strikeDir);
        }
        _spray.Wave(HerOnPage());
        SetPhase(Phase.Choosing);
    }

    /// <summary>Choosing, a frame on: the dissolve's front across the item, the push wave's throw; then what it does.</summary>
    void StepChoose()
    {
        if (_chosen is not { } item) return;
        float t = Reduced ? 10f : _phaseTime;
        // The front runs along her beam across the item's rect, past both ends.
        var rect = item.Button.GetGlobalRect().Merge(item.Detail.GetGlobalRect());
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var corner in new[] { rect.Position, rect.Position + new Vector2(rect.Size.X, 0f), rect.End, rect.Position + new Vector2(0f, rect.Size.Y) })
        {
            float along = (corner - _strikeFrom).Dot(_strikeDir);
            lo = Mathf.Min(lo, along);
            hi = Mathf.Max(hi, along);
        }
        float p = Mathf.Clamp(t / DissolveSeconds, 0f, 1f);
        float front = Mathf.Lerp(lo - 30f, hi + 30f, p);
        SetFront(item, front);
        // Its bits off the front, where it runs through the text.
        if (p < 1f)
        {
            var across = new Vector2(-_strikeDir.Y, _strikeDir.X);
            for (int k = 0; k < 6; k++)
            {
                var at = _strikeFrom + _strikeDir * front + across * (float)GD.RandRange(-rect.Size.Length() * 0.6f, rect.Size.Length() * 0.6f);
                if (rect.Grow(-2f).HasPoint(at)) _spray.Dissolve(at / UiScale, _strikeDir, 2);
            }
        }
        float q = Mathf.Clamp(t / PushSeconds, 0f, 1f);
        _push = q * q;
        if (t < Mathf.Max(DissolveSeconds, PushSeconds) + 0.05f) return;
        if (item.Id == "reef")
        {
            // Play: she dives to the water below, where her jellies float.
            _fish.Visible = false;
            _lookFrom = _look;
            SetPhase(Phase.Diving);
            return;
        }
        SetPhase(Phase.Below);
        _chosenAction?.Invoke();
    }

    void SetFront(( Button Button, Label Detail, string Id) item, float front)
    {
        ((ShaderMaterial)item.Button.Material).SetShaderParameter("front", front);
        ((ShaderMaterial)item.Detail.Material).SetShaderParameter("front", front);
    }

    /// <summary>Where a part pushed off by her wave stands off its place (page units): away from her, as far as the wave threw it.</summary>
    Vector2 Pushed(Control part, Vector2 at)
    {
        bool chosen = _chosen is { } c && c.Button.GetParent<Control>() == part;
        float push = chosen ? 0f : _push;
        part.Modulate = new Color(1f, 1f, 1f, 1f - push);
        if (push <= 0f) return Vector2.Zero;
        var her = HerOnPage();
        var away = (at - her).LengthSquared() > 1f ? (at - her).Normalized() : Vector2.Up;
        return away * push * 1400f;
    }

    readonly Dictionary<Control, Vector2> _partAt = new();

    /// <summary>The logo (and the footer) pushed off as the items are, from where they stand.</summary>
    void PushPart(Control part, Vector2 home)
    {
        if (!_partAt.ContainsKey(part)) _partAt[part] = home;
        var at = _partAt[part];
        part.Position = at + Pushed(part, at + part.Size * 0.5f);
        if (_footer is { } footer)
        {
            if (!_partAt.ContainsKey(footer)) _partAt[footer] = footer.Position;
            var fat = _partAt[footer];
            footer.Position = fat + Pushed(footer, fat + footer.Size * 0.5f);
        }
    }

    /// <summary>The menu comes back: the pushed-off parts return to their places, the chosen item whole again.</summary>
    void StepReturn()
    {
        float t = Reduced ? 10f : _phaseTime;
        float q = 1f - Mathf.Clamp(t / ReturnSeconds, 0f, 1f);
        _push = q * q;
        if (_chosen is { } item)
        {
            SetFront(item, -100000f);
            item.Button.GetParent<Control>().Modulate = new Color(1f, 1f, 1f, 1f - q);
        }
        if (t < ReturnSeconds) return;
        _push = 0f;
        if (_chosen is { } was) was.Button.GetParent<Control>().Modulate = Colors.White;
        _chosen = null;
        _fish.Visible = true;
        SetPhase(Phase.Menu);
        _items.FirstOrDefault(it => it.Button.GetParent<Control>().Visible).Button?.GrabFocus();
    }

    // ───────────────────────── the dive ─────────────────────────

    ReefLook? _lookFrom;

    /// <summary>The camera drawing in through the dive (as the game's between layers), and back out.</summary>
    float DiveZoom()
    {
        if (_phase is not (Phase.Diving or Phase.Climbing)) return 1f;
        float pull = Mathf.Sin(Mathf.Pi * Mathf.Clamp(_phaseTime / DiveSeconds, 0f, 1f));
        return 1f - DiveCloser * pull * pull;
    }

    /// <summary>
    /// The game's dive between layers (TopDownMain.StepLayerMove): she gathers, turns head-down and sinks a layer's drop,
    /// the camera after her drawing in and back out, the water darkening a little to the layer below's. Arrived, she is
    /// set back on her plane with the water moved up with her (no change to see), and her jellies' slots open. Climbing
    /// back is the same run the other way.
    /// </summary>
    void StepDive(bool down)
    {
        float t = Reduced ? DiveSeconds : _phaseTime;
        if (!down && _phaseTime == 0f) { }
        float sink = Ease((t - 0.25f) / 0.95f);
        sink = Mathf.Clamp(sink + 0.04f * Mathf.Sin(Mathf.Clamp((t - 0.25f) / 0.95f, 0f, 1f) * Mathf.Pi * 3f), 0f, 1f);
        float gather = Mathf.Sin(Mathf.Clamp(t / 0.25f, 0f, 1f) * Mathf.Pi) * 0.35f;
        _drop = (down ? sink : 1f - sink) * LevelMap.LevelDrop;
        _bell.DiveDepth = _drop - (down ? 1f : -1f) * gather;
        _bell.DiveTurn = down ? Ease((t - 0.2f) / 0.3f) * (1f - Ease((t - 1.1f) / 0.4f)) : 0f;
        _camera.SetSwimLevel(-_drop);
        var look = down ? ReefLook.Blend(_look, _lookBelow, sink) : ReefLook.Blend(_lookBelow, _look, sink);
        ApplyLook(look);
        look.SetDrop(_drop);
        if (t < DiveSeconds) return;
        // Arrived: back on her plane, the water (its floor) moved up with her.
        _bell.DiveDepth = 0f;
        _bell.DiveTurn = 0f;
        _camera.SetSwimLevel(0f);
        _level.Position = new Vector3(0f, down ? LevelMap.LevelDrop : 0f, 0f);
        _drop = 0f;
        var arrived = down ? _lookBelow : _look;
        ApplyLook(arrived);
        arrived.SetDrop(0f);
        if (down)
        {
            SetPhase(Phase.Below);
            _chosenAction?.Invoke();
        }
        else SetPhase(Phase.Returning);
    }

    void ApplyLook(ReefLook look)
    {
        look.ApplyTo(_env.Environment);
        look.Apply();
        _sun.Configure(look.Sun, look.SunEnergy);
    }
}
