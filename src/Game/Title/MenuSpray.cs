using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Title;

/// <summary>
/// The title menu's particles, over its items (on the menu's page): her light striking an item as it strikes a mob —
/// sparks of her light splashing off the side facing her and the corruption's violet brackets peeling away from it — the
/// sparks an item's text throws off as it dissolves, and the push wave's ring running out from her.
/// </summary>
public partial class MenuSpray : Control
{
    struct Bit
    {
        public Vector2 At, Velocity;
        public float Life, Total, Size, Spin, Angle;
        public Color Colour;
        /// <summary>0 a spark, 1–4 a bracket ( ) [ ].</summary>
        public int Shape;
    }

    readonly List<Bit> _bits = new();
    readonly RandomNumberGenerator _rng = new();
    /// <summary>The push wave: where it runs from, and how far through it is (−1: none).</summary>
    Vector2 _waveFrom;
    float _wave = -1f;
    const float WaveSeconds = 0.6f, WaveReach = 1500f;

    static readonly Color Spark = new(1f, 0.93f, 0.72f), Violet = new(0.42f, 0.16f, 0.72f), DeepViolet = new(0.26f, 0.08f, 0.48f), BrightViolet = new(0.78f, 0.5f, 1f);
    static readonly string[] Brackets = { "(", ")", "[", "]" };

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>A frame of her light on an item (its rect on the page) from her (on the page): so many bits a second.</summary>
    public void Lit(Rect2 item, Vector2 her, float dt, float rate = 70f)
    {
        float n = rate * dt;
        int count = (int)n + (_rng.Randf() < n % 1f ? 1 : 0);
        var centre = item.GetCenter();
        var flow = (centre - her).Normalized();
        var across = new Vector2(-flow.Y, flow.X);
        for (int i = 0; i < count; i++)
        {
            // From a point of its side facing her, sheeting round it and flung off away from her.
            var on = centre + across * _rng.RandfRange(-0.5f, 0.5f) * item.Size.X * 0.9f - flow * item.Size.Y * _rng.RandfRange(0f, 0.5f)
                + new Vector2(0f, _rng.RandfRange(-0.5f, 0.5f) * item.Size.Y);
            bool bracket = _rng.Randf() < 0.4f;
            var v = (flow * _rng.RandfRange(60f, 170f) + across * _rng.RandfRange(-90f, 90f)) * (bracket ? 0.7f : 1f);
            Add(on, v, bracket ? _rng.RandfRange(0.6f, 1f) : _rng.RandfRange(0.3f, 0.55f), bracket ? 15f : _rng.RandfRange(1.6f, 3f),
                bracket ? PeelColour() : Spark, bracket ? 1 + _rng.RandiRange(0, 3) : 0);
        }
    }

    /// <summary>Its text dissolving: bits off the front where it eats the text (a point of it, and the way it goes).</summary>
    public void Dissolve(Vector2 at, Vector2 dir, int count)
    {
        var across = new Vector2(-dir.Y, dir.X);
        for (int i = 0; i < count; i++)
        {
            bool bracket = _rng.Randf() < 0.35f;
            var v = dir * _rng.RandfRange(90f, 260f) + across * _rng.RandfRange(-80f, 80f);
            Add(at + across * _rng.RandfRange(-4f, 4f), v, _rng.RandfRange(0.45f, 0.9f), bracket ? 16f : _rng.RandfRange(2f, 3.6f),
                bracket ? PeelColour() : Spark, bracket ? 1 + _rng.RandiRange(0, 3) : 0);
        }
    }

    /// <summary>The push wave: a ring running out from her past the screen's edges.</summary>
    public void Wave(Vector2 from)
    {
        _waveFrom = from;
        _wave = 0f;
    }

    Color PeelColour()
    {
        float r = _rng.Randf();
        return r < 0.45f ? Violet : r < 0.8f ? DeepViolet : BrightViolet;
    }

    void Add(Vector2 at, Vector2 v, float life, float size, Color colour, int shape)
    {
        if (_bits.Count > 1200) return;
        _bits.Add(new Bit { At = at, Velocity = v, Life = life, Total = life, Size = size, Colour = colour, Shape = shape, Angle = _rng.RandfRange(-0.5f, 0.5f), Spin = _rng.RandfRange(-3f, 3f) });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _bits.Count - 1; i >= 0; i--)
        {
            var b = _bits[i];
            b.Life -= dt;
            if (b.Life <= 0f)
            {
                _bits.RemoveAt(i);
                continue;
            }
            b.Velocity *= Mathf.Max(0f, 1f - 2.4f * dt);
            // (The brackets sink as they slow; the sparks rise.)
            b.Velocity.Y += (b.Shape > 0 ? 40f : -25f) * dt;
            b.At += b.Velocity * dt;
            b.Angle += b.Spin * dt;
            _bits[i] = b;
        }
        if (_wave >= 0f)
        {
            _wave += dt / WaveSeconds;
            if (_wave > 1f) _wave = -1f;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        if (_wave >= 0f)
        {
            float k = _wave, r = WaveReach * (1f - (1f - k) * (1f - k));
            DrawArc(_waveFrom, r, 0f, Mathf.Tau, 96, new Color(1f, 0.95f, 0.85f, 0.45f * (1f - k)), 10f * (1f - k) + 2f, true);
            DrawArc(_waveFrom, r * 0.93f, 0f, Mathf.Tau, 96, new Color(1f, 0.95f, 0.85f, 0.18f * (1f - k)), 18f * (1f - k) + 2f, true);
        }
        foreach (var b in _bits)
        {
            float t = b.Life / b.Total;
            // In with a little swell, out with a fade.
            float a = Mathf.Min(1f, (1f - t) * 8f) * Mathf.Min(1f, t * 2.5f);
            var c = b.Colour with { A = b.Colour.A * a };
            if (b.Shape == 0)
            {
                DrawCircle(b.At, b.Size * 1.8f, c with { A = c.A * 0.25f });
                DrawCircle(b.At, b.Size, c);
                continue;
            }
            DrawSetTransform(b.At, b.Angle, Vector2.One);
            int size = (int)b.Size;
            DrawString(font, new Vector2(-size * 0.3f, size * 0.35f), Brackets[b.Shape - 1], HorizontalAlignment.Left, -1, size, c);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
