using System.Collections.Generic;
using Godot;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The requests she carries off the Tide Fan (docs/QUESTS-PROPOSAL.md §6), on the HUD under the minimap: each a ribbon of
/// kelp drifting from a knot, the knot holding its token (the pearl in its colour, or gold for shells) and coloured by its
/// kind, what it asks written along it in a few words with how far along it is. A done one glints gold. The pause
/// menu's Requests tab hides them.
/// </summary>
public partial class RequestRibbons : Control
{
    /// <summary>One ribbon: its words, its kind's colour, its token's colour, and whether it is done.</summary>
    public readonly record struct Ribbon(string Text, Color Kind, Color Token, bool Done);

    readonly List<Ribbon> _ribbons = new();
    readonly List<Label> _labels = new();
    const float Width = MinimapView.Diameter + 20f, RowHeight = 40f, Knot = 11f;
    float _time;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -Width - MinimapView.Margin;
        OffsetRight = -MinimapView.Margin;
        OffsetTop = MinimapView.Margin + MinimapView.Diameter + 82f;
        OffsetBottom = OffsetTop + RowHeight * 2f + 8f;
    }

    /// <summary>The ribbons to show now (none hides them).</summary>
    public void Show(IReadOnlyList<Ribbon> ribbons)
    {
        _ribbons.Clear();
        _ribbons.AddRange(ribbons);
        while (_labels.Count < _ribbons.Count)
        {
            var l = new Label { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center, ClipText = true };
            l.AddThemeFontSizeOverride("font_size", 15);
            l.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.06f, 0.05f, 0.85f));
            l.AddThemeConstantOverride("outline_size", 4);
            AddChild(l);
            _labels.Add(l);
        }
        for (int i = 0; i < _labels.Count; i++)
        {
            var l = _labels[i];
            l.Visible = i < _ribbons.Count;
            if (!l.Visible) continue;
            l.Text = _ribbons[i].Text;
            l.AddThemeColorOverride("font_color", _ribbons[i].Done ? new Color(1f, 0.86f, 0.5f) : new Color(0.93f, 0.97f, 0.88f));
            l.Position = new Vector2(Knot * 2f + 10f, i * RowHeight + 4f);
            l.Size = new Vector2(Width - Knot * 2f - 18f, RowHeight - 8f);
        }
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_ribbons.Count > 0) QueueRedraw();
    }

    public override void _Draw()
    {
        for (int i = 0; i < _ribbons.Count; i++)
        {
            var r = _ribbons[i];
            float y0 = i * RowHeight + RowHeight * 0.5f;
            var knot = new Vector2(Knot + 2f, y0);
            // The kelp: a long blade drifting from the knot, waving gently, a midrib down it.
            const int steps = 24;
            var top = new Vector2[steps + 1];
            var bottom = new Vector2[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float x = knot.X + t * (Width - knot.X - 4f);
                float wave = Mathf.Sin(t * 6f - _time * 1.4f + i * 1.7f) * 3.5f * t;
                float half = Mathf.Lerp(13f, 5f, t) * (1f + 0.08f * Mathf.Sin(t * 19f + _time * 2f));
                top[s] = new Vector2(x, y0 + wave - half);
                bottom[s] = new Vector2(x, y0 + wave + half);
            }
            var blade = new Vector2[(steps + 1) * 2];
            for (int s = 0; s <= steps; s++)
            {
                blade[s] = top[s];
                blade[blade.Length - 1 - s] = bottom[s];
            }
            var kelp = r.Done ? new Color(0.42f, 0.36f, 0.12f, 0.82f) : new Color(0.16f, 0.3f, 0.15f, 0.78f);
            DrawColoredPolygon(blade, kelp);
            DrawPolyline(top, new Color(r.Kind, 0.7f), 1.4f, true);
            DrawPolyline(bottom, new Color(r.Kind, 0.7f), 1.4f, true);
            // The knot, in its kind's colour, holding its token.
            DrawCircle(knot, Knot + 2f, new Color(0.03f, 0.06f, 0.05f, 0.85f));
            DrawCircle(knot, Knot, r.Kind);
            DrawCircle(knot, Knot * 0.55f, r.Token);
            if (r.Done)
            {
                float glint = 0.5f + 0.5f * Mathf.Sin(_time * 4f + i);
                DrawArc(knot, Knot + 4f, 0f, Mathf.Tau, 24, new Color(1f, 0.85f, 0.4f, 0.5f + 0.5f * glint), 2f, true);
            }
        }
    }

    /// <summary>A request kind's ribbon colour (docs/QUESTS-PROPOSAL.md §3).</summary>
    public static Color KindColour(string kind) => kind switch
    {
        OctoShoots.Core.Plane.Requests.Seal => new Color(0.62f, 0.3f, 0.95f),
        OctoShoots.Core.Plane.Requests.Prime => new Color(0.8f, 0.85f, 0.92f),
        OctoShoots.Core.Plane.Requests.Den => new Color(0.4f, 0.16f, 0.55f),
        _ => new Color(0.66f, 0.64f, 0.7f),
    };
}
