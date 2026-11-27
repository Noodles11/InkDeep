using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Controls;
using OctoShoots.Game.Ui;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The upgrade drawer (docs/GAME.md, PlaneGrowth.cs): at a light spring, her light's energy laid on what she would grow.
/// At its top a bubble of light, its specks swirling inside, as many as the energy she has left to lay (the number
/// beside it). Below, every characteristic she can grow, each with its bar: a click sends a speck's worth (one, or what
/// is left) flying out of the bubble into its bar, the bar filling and its number climbing as the specks arrive.
/// A row held (the mouse or A) takes her energy as long as it is held, and what is laid stays laid when it is let go.
/// Reset sends what is not yet kept back into the bubble; Mutate keeps it, for good (PlaneWorld.Grow), and the energy is
/// spent. Leaving it unkept is a reset.
/// </summary>
public partial class GrowthMenu : Control
{
    /// <summary>Mutate: the energy laid on each characteristic, to keep.</summary>
    public event Action<Dictionary<GrowthStat, float>>? Mutated;
    public event Action? Closed;

    const float Width = 600f, SlideSeconds = DrawerStyle.SlideSeconds, FlySeconds = 0.5f, ScrollSpeed = 900f;
    /// <summary>A bar is full at this much energy laid (it fills ever more slowly toward it: laid / (laid + BarHalf)).</summary>
    const float BarHalf = 8f;

    PanelContainer _panel = null!;
    ColorRect _shade = null!;
    ScrollContainer _scroll = null!;
    Bubble _bubble = null!;
    Label _count = null!;
    Fx _fx = null!;
    Button _mutate = null!, _reset = null!;
    float _open;
    bool _opening;

    sealed class Row
    {
        public GrowthStat Stat;
        public Button Button = null!;
        public Bar Bar = null!;
        public Label Value = null!;
        /// <summary>Kept already; laid now and arrived; laid now (some still flying).</summary>
        public float Kept, Arrived, Laid;
    }

    readonly List<Row> _rows = new();
    float _energy;

    public bool IsOpen => _opening;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _shade = new ColorRect { Color = new Color(0.01f, 0.03f, 0.05f, 0f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1f, AnchorBottom = 1f };
        AddChild(_shade);
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(Width, 0f) };
        _panel.AddThemeStyleboxOverride("panel", DrawerStyle.Panel());
        AddChild(_panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(box);
        box.AddChild(DrawerStyle.Title("Grow"));
        box.AddChild(DrawerStyle.Line("The spring takes the light you have gathered. Lay it on what you would grow: Mutate keeps it for good, Reset gives it back.", DrawerStyle.Soft, 15, wrap: true));

        // The bubble of her light's energy, and how much is left to lay.
        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        top.AddThemeConstantOverride("separation", 20);
        box.AddChild(top);
        _bubble = new Bubble { CustomMinimumSize = new Vector2(150f, 150f) };
        top.AddChild(_bubble);
        var counts = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        top.AddChild(counts);
        _count = DrawerStyle.Line("0", DrawerStyle.Gold, 40);
        counts.AddChild(_count);
        counts.AddChild(DrawerStyle.Line("specks of light", DrawerStyle.Soft, 15));

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        box.AddChild(_scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        _scroll.AddChild(list);
        list.AddChild(DrawerStyle.Line("HER LIGHT", DrawerStyle.Gold, 13));
        foreach (var s in GrowthTuning.Light) list.AddChild(MakeRow(s));
        list.AddChild(DrawerStyle.Line("HER BODY", DrawerStyle.Gold, 13));
        foreach (var s in GrowthTuning.Body) list.AddChild(MakeRow(s));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        box.AddChild(buttons);
        _reset = DrawerStyle.Button("Reset");
        _reset.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _reset.Pressed += Reset;
        buttons.AddChild(_reset);
        _mutate = DrawerStyle.Button("Mutate");
        _mutate.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _mutate.Pressed += Mutate;
        buttons.AddChild(_mutate);
        var leave = DrawerStyle.Button("Leave");
        leave.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leave.Pressed += () => Closed?.Invoke();
        buttons.AddChild(leave);

        // (The specks in flight are drawn over everything in the drawer.)
        _fx = new Fx { MouseFilter = MouseFilterEnum.Ignore };
        _fx.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_fx);
    }

    Control MakeRow(GrowthStat stat)
    {
        var row = new Row { Stat = stat };
        row.Button = new Button { CustomMinimumSize = new Vector2(0f, 54f), FocusMode = FocusModeEnum.All };
        var inner = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        inner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        inner.OffsetLeft = 14f;
        inner.OffsetRight = -14f;
        inner.AddThemeConstantOverride("separation", 12);
        row.Button.AddChild(inner);
        var name = DrawerStyle.Line(GrowthTuning.Name(stat), Colour(stat), 18);
        name.CustomMinimumSize = new Vector2(150f, 0f);
        name.VerticalAlignment = VerticalAlignment.Center;
        inner.AddChild(name);
        row.Bar = new Bar { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(0f, 14f), Tint = Colour(stat), MouseFilter = MouseFilterEnum.Ignore };
        inner.AddChild(row.Bar);
        row.Value = DrawerStyle.Line("", DrawerStyle.Text, 17);
        row.Value.CustomMinimumSize = new Vector2(96f, 0f);
        row.Value.HorizontalAlignment = HorizontalAlignment.Right;
        row.Value.VerticalAlignment = VerticalAlignment.Center;
        inner.AddChild(row.Value);
        // (Held, by the mouse or A, it lays her energy on it as long as it is held: _Process.)
        _rows.Add(row);
        return row.Button;
    }

    static Color Colour(GrowthStat s) => s switch
    {
        GrowthStat.Red => new Color(1f, 0.45f, 0.4f),
        GrowthStat.Green => new Color(0.5f, 1f, 0.55f),
        GrowthStat.Blue => new Color(0.5f, 0.7f, 1f),
        _ => DrawerStyle.Text,
    };

    /// <summary>What is left to lay: her energy less all laid now.</summary>
    float Left => MathF.Max(0f, _energy - _rows.Sum(r => r.Laid));

    /// <summary>Opens on her energy and what she has grown already.</summary>
    public void Open(float energy, IReadOnlyDictionary<GrowthStat, float> grown)
    {
        _energy = energy;
        foreach (var r in _rows)
        {
            r.Kept = grown.TryGetValue(r.Stat, out var v) ? v : 0f;
            r.Arrived = r.Laid = 0f;
        }
        _fx.Clear();
        Show_();
        _opening = true;
        Visible = true;
        if (_rows.Count > 0) _rows[0].Button.GrabFocus();
    }

    public void Close()
    {
        // (Unkept, what was laid goes back: nothing is spent.)
        foreach (var r in _rows) r.Arrived = r.Laid = 0f;
        _fx.Clear();
        _opening = false;
        GetViewport()?.GuiReleaseFocus();
    }

    /// <summary>Verification: as if its row were clicked.</summary>
    public void LayOn(GrowthStat stat)
    {
        foreach (var r in _rows)
            if (r.Stat == stat) Lay(r, 1f);
    }

    /// <summary>How fast her energy flows into a characteristic while its row is held (a speck a second), and in what steps.</summary>
    const float FlowRate = 2f, FlowStep = 0.15f;
    float _flow;

    /// <summary>Its row held: by the mouse, or (focused) by A or Enter.</summary>
    static bool Held(Row row) => !row.Button.Disabled && (row.Button.IsPressed()
        || row.Button.HasFocus() && (Input.IsActionPressed(InputSetup.MenuAccept) || Input.IsActionPressed("ui_accept")));

    /// <summary>Some of what is left (amount, or all there is) sent flying from the bubble into a characteristic's bar.</summary>
    void Lay(Row row, float amount)
    {
        float step = MathF.Min(amount, Left);
        if (step <= 1e-4f) return;
        row.Laid += step;
        _bubble.Amount = Left;
        var from = _bubble.GlobalPosition + _bubble.Size * 0.5f;
        var to = row.Bar.GlobalPosition + new Vector2(row.Bar.Size.X * Fill(row.Kept + row.Arrived + step), row.Bar.Size.Y * 0.5f);
        int motes = Math.Max(1, (int)(step * 6f));
        for (int i = 0; i < motes; i++)
        {
            float share = step / motes;
            _fx.Fly(from, to, FlySeconds + 0.06f * i, Colour(row.Stat).Lerp(DrawerStyle.Gold, 0.5f), () =>
            {
                row.Arrived = MathF.Min(row.Laid, row.Arrived + share);
                Show_();
            });
        }
        Show_();
    }

    /// <summary>All that is laid and not kept back into the bubble, the specks flying home.</summary>
    void Reset()
    {
        foreach (var r in _rows)
        {
            if (r.Laid <= 0f) continue;
            var from = r.Bar.GlobalPosition + new Vector2(r.Bar.Size.X * Fill(r.Kept + r.Arrived), r.Bar.Size.Y * 0.5f);
            var to = _bubble.GlobalPosition + _bubble.Size * 0.5f;
            int motes = Math.Max(3, (int)(r.Laid * 4f));
            for (int i = 0; i < motes; i++) _fx.Fly(from, to, FlySeconds + 0.05f * i, Colour(r.Stat).Lerp(DrawerStyle.Gold, 0.5f), null);
            r.Laid = r.Arrived = 0f;
        }
        _fx.Settle();
        Show_();
    }

    /// <summary>What is laid kept for good: her energy spent on it.</summary>
    void Mutate()
    {
        var laid = _rows.Where(r => r.Laid > 0f).ToDictionary(r => r.Stat, r => r.Laid);
        if (laid.Count == 0) return;
        float spent = laid.Values.Sum();
        Mutated?.Invoke(laid);
        _energy = MathF.Max(0f, _energy - spent);
        foreach (var r in _rows)
        {
            r.Kept += r.Laid;
            r.Laid = r.Arrived = 0f;
        }
        _fx.Settle();
        _bubble.Burst();
        Show_();
    }

    static float Fill(float units) => units / (units + BarHalf);

    void Show_()
    {
        float left = Left;
        _bubble.Amount = left;
        _count.Text = $"{left:0.#}";
        foreach (var r in _rows)
        {
            r.Bar.Kept = Fill(r.Kept);
            r.Bar.Now = Fill(r.Kept + r.Arrived);
            string now = GrowthTuning.Effect(r.Stat, r.Kept + r.Arrived);
            r.Value.Text = r.Kept + r.Arrived <= 0f ? "—" : now;
            r.Value.AddThemeColorOverride("font_color", r.Arrived > 0f ? DrawerStyle.Gold : DrawerStyle.Text);
            r.Button.Disabled = left <= 1e-4f;
        }
        bool laid = _rows.Any(r => r.Laid > 0f);
        _mutate.Disabled = !laid;
        _reset.Disabled = !laid;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _open = Mathf.MoveToward(_open, _opening ? 1f : 0f, dt / SlideSeconds);
        Visible = _opening || _open > 0f;
        var size = GetViewportRect().Size;
        float e = 1f - Mathf.Pow(1f - _open, 3f);
        _panel.Position = new Vector2(size.X - Width * e, 0f);
        _panel.Size = new Vector2(Width, size.Y);
        _shade.Color = new Color(0.01f, 0.03f, 0.05f, 0.45f * e);
        // Held, a row takes her energy as long as it is held, a little at a time; let go, what is laid stays laid.
        if (_opening && _rows.FirstOrDefault(Held) is { } held)
        {
            _flow += dt * FlowRate;
            if (_flow >= FlowStep)
            {
                Lay(held, _flow);
                _flow = 0f;
            }
        }
        else _flow = 0f;
        if (!_opening || !InputSetup.UsingPad) return;
        float scroll = Input.GetAxis(InputSetup.AimUp, InputSetup.AimDown);
        if (Mathf.Abs(scroll) > 0.2f) _scroll.ScrollVertical += Mathf.RoundToInt(scroll * ScrollSpeed * dt);
        if (GetViewport().GuiGetFocusOwner() is null && _rows.Count > 0) _rows[0].Button.GrabFocus();
    }

    /// <summary>The bubble of light: a glassy sphere, its specks of light swirling inside, as many as the energy left in it.</summary>
    partial class Bubble : Control
    {
        public float Amount;
        float _shown, _time, _burst;
        readonly RandomNumberGenerator _rng = new() { Seed = 7 };
        readonly List<(Vector2 P, Vector2 V, float Phase)> _motes = new();

        public void Burst() => _burst = 1f;

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            _burst = Mathf.Max(0f, _burst - dt * 1.5f);
            // (Its count eases to the energy left, a few motes a speck.)
            _shown = Mathf.MoveToward(_shown, Amount, dt * MathF.Max(4f, MathF.Abs(Amount - _shown) * 3f));
            int want = Math.Min(160, (int)MathF.Ceiling(_shown * 4f));
            float r = Size.X * 0.5f * 0.8f;
            while (_motes.Count < want)
            {
                float a = _rng.RandfRange(0f, Mathf.Tau), d = Mathf.Sqrt(_rng.Randf()) * r;
                _motes.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f)) * 18f, _rng.RandfRange(0f, 10f)));
            }
            if (_motes.Count > want) _motes.RemoveRange(want, _motes.Count - want);
            for (int i = 0; i < _motes.Count; i++)
            {
                var (p, v, ph) = _motes[i];
                // (A slow swirl round the middle, a drift, kept inside the glass.)
                v += new Vector2(-p.Y, p.X) * 0.4f * dt + new Vector2(Mathf.Sin(_time * 1.3f + ph), Mathf.Cos(_time * 1.1f + ph)) * 10f * dt;
                p += v * dt;
                if (p.Length() > r)
                {
                    p = p.Normalized() * r;
                    v = -v * 0.6f;
                }
                _motes[i] = (p, v.LimitLength(26f), ph);
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            var c = Size * 0.5f;
            float r = Size.X * 0.5f;
            float full = Mathf.Clamp(_shown / 20f, 0f, 1f);
            // Its inner glow, the more light in it the brighter.
            for (int k = 6; k >= 1; k--)
                DrawCircle(c, r * (0.2f + 0.13f * k), new Color(1f, 0.85f, 0.5f, (0.025f + 0.05f * full) * (1f + _burst)));
            foreach (var (p, _, ph) in _motes)
            {
                float tw = 0.6f + 0.4f * Mathf.Sin(_time * 4f + ph * 3f);
                DrawCircle(c + p, 4.2f, new Color(1f, 0.9f, 0.6f, 0.16f * tw));
                DrawCircle(c + p, 1.8f, new Color(1f, 0.97f, 0.88f, 0.95f * tw));
            }
            // The glass: a rim, and a highlight up on its left.
            DrawArc(c, r - 1.5f, 0f, Mathf.Tau, 64, new Color(0.85f, 0.95f, 1f, 0.55f), 2f, true);
            DrawArc(c, r - 7f, Mathf.Pi * 1.1f, Mathf.Pi * 1.45f, 16, new Color(1f, 1f, 1f, 0.45f), 3f, true);
        }
    }

    /// <summary>A characteristic's bar: what is kept, and what is laid now and arrived, easing as the specks come in.</summary>
    partial class Bar : Control
    {
        public float Kept, Now;
        public Color Tint = Colors.White;
        float _kept, _now;

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _kept = Mathf.MoveToward(_kept, Kept, dt * 1.5f);
            _now = Mathf.MoveToward(_now, Now, dt * 0.8f);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            DrawRect(r, new Color(0.05f, 0.1f, 0.12f, 0.95f));
            DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * _now, Size.Y)), new Color(1f, 0.85f, 0.45f, 0.9f));
            DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * Mathf.Min(_kept, _now), Size.Y)), Tint.Lerp(new Color(1f, 0.9f, 0.7f), 0.35f));
            DrawRect(r, new Color(1f, 0.82f, 0.45f, 0.35f), false, 1f);
        }
    }

    /// <summary>Specks of light in flight across the drawer, each arriving where it was sent (its arrival told).</summary>
    partial class Fx : Control
    {
        sealed class Mote
        {
            public Vector2 From, To, Bend;
            public float T, Seconds;
            public Color Colour;
            public Action? Arrive;
        }

        readonly List<Mote> _motes = new();
        readonly RandomNumberGenerator _rng = new() { Seed = 11 };

        public void Fly(Vector2 from, Vector2 to, float seconds, Color colour, Action? arrive)
        {
            var side = (to - from).Orthogonal().Normalized();
            _motes.Add(new Mote { From = from, To = to, Seconds = seconds, Colour = colour, Arrive = arrive, Bend = side * _rng.RandfRange(-90f, 90f) });
        }

        /// <summary>Every speck still flying arrives now (a mutate or reset settles the bars at once).</summary>
        public void Settle()
        {
            foreach (var m in _motes.ToList()) m.Arrive?.Invoke();
            _motes.RemoveAll(m => m.Arrive is not null);
        }

        public void Clear() => _motes.Clear();

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                var m = _motes[i];
                m.T += dt / m.Seconds;
                if (m.T < 1f) continue;
                m.Arrive?.Invoke();
                _motes.RemoveAt(i);
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            var origin = GetGlobalTransformWithCanvas().Origin;
            foreach (var m in _motes)
            {
                float t = Mathf.SmoothStep(0f, 1f, m.T);
                for (int k = 0; k < 5; k++)
                {
                    float tk = Mathf.Max(0f, t - k * 0.03f);
                    var p = m.From.Lerp(m.To, tk) + m.Bend * Mathf.Sin(tk * Mathf.Pi) - origin;
                    float a = 1f - k * 0.2f;
                    DrawCircle(p, 6f - k, new Color(m.Colour, 0.18f * a));
                    DrawCircle(p, 2.6f - k * 0.4f, new Color(1f, 0.97f, 0.88f, a));
                }
            }
        }
    }
}
