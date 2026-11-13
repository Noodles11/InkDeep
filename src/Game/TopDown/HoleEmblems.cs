using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The emblems over a reef's open holes (docs/LIGHT-AND-GRAPH.md §6.3): a faint see-through sign of what lies below,
/// drawn in light in the colour of its rim (ReefKinds), lying flat over the hole just above her plane:
/// <list type="bullet">
/// <item>a plain reef: a fish;</item>
/// <item>Lantern Bay (a haven): a lantern with its flame;</item>
/// <item>the depth's boss: the Leak's five-armed star;</item>
/// <item>a clam room: an open clam with its pearl;</item>
/// <item>a curse den: a spiral drawing in.</item>
/// </list>
/// Each is drawn once, here, as a soft white line drawing (its colour is its material's).
/// </summary>
public static class HoleEmblems
{
    const int Size = 160;
    static readonly Dictionary<ReefKind, Texture2D> Made = new();

    public static Texture2D Of(ReefKind kind)
    {
        if (Made.TryGetValue(kind, out var t)) return t;
        Func<Vector2, float> shape = kind switch
        {
            ReefKind.Haven => Lantern,
            ReefKind.Boss => Star,
            ReefKind.Treasure => Clam,
            ReefKind.Den => Spiral,
            _ => Fish,
        };
        var img = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        // (Each pixel the line drawing's coverage there, soft at its edges, with a faint fill inside it.)
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                float a = Mathf.Clamp(shape(p), 0f, 1f);
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        img.GenerateMipmaps();
        return Made[kind] = ImageTexture.CreateFromImage(img);
    }

    // A line of width w (as a share of the half-size) along a signed distance d: 1 on it, soft to 0 off it (in pixels).
    static float Line(float d, float w) => (w - Mathf.Abs(d)) * Size / 2f;
    static float Fill(float d, float soft = 0f) => -d * Size / 2f + soft;

    static float Circle(Vector2 p, Vector2 c, float r) => (p - c).Length() - r;

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return (p - (a + ab * t)).Length();
    }

    static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
    {
        var q = (p - c) / r;
        return (q.Length() - 1f) * Mathf.Min(r.X, r.Y);
    }

    /// <summary>The line's coverage, and a faint fill (0.18) inside the shape's body.</summary>
    static float Draw(float line, float inside) => Mathf.Max(Mathf.Clamp(line, 0f, 1f), 0.18f * Mathf.Clamp(inside, 0f, 1f));

    // (Image rows run down the texture, which lies flat with its top to the north: y < 0 is north, up the screen.)

    static float Fish(Vector2 p)
    {
        // A body facing east, its tail fanned to the west, an eye.
        float body = Ellipse(p, new Vector2(0.12f, 0f), new Vector2(0.55f, 0.32f));
        float tail = Mathf.Min(Segment(p, new Vector2(-0.43f, 0f), new Vector2(-0.8f, -0.32f)),
            Mathf.Min(Segment(p, new Vector2(-0.43f, 0f), new Vector2(-0.8f, 0.32f)), Segment(p, new Vector2(-0.8f, -0.32f), new Vector2(-0.8f, 0.32f))));
        float line = Mathf.Max(Line(body, 0.035f), Line(tail, 0.035f));
        line = Mathf.Max(line, Fill(Circle(p, new Vector2(0.4f, -0.08f), 0.06f)));
        return Draw(line, Fill(body));
    }

    static float Lantern(Vector2 p)
    {
        // Its glass a rounded box, a cap and a ring to hang it by, its flame a drop inside.
        var c = new Vector2(0f, 0.12f);
        var q = (p - c).Abs() - new Vector2(0.32f, 0.42f);
        float box = new Vector2(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f)).Length() + Mathf.Min(Mathf.Max(q.X, q.Y), 0f) - 0.08f;
        float cap = Segment(p, new Vector2(-0.3f, -0.42f), new Vector2(0.3f, -0.42f));
        float ring = Mathf.Abs(Circle(p, new Vector2(0f, -0.6f), 0.13f));
        float flame = Mathf.Min(Circle(p, new Vector2(0f, 0.22f), 0.13f), Segment(p, new Vector2(0f, 0.22f), new Vector2(0f, -0.06f)) - 0.05f);
        float line = Mathf.Max(Line(box, 0.035f), Mathf.Max(Line(cap, 0.045f), Line(ring, 0.03f)));
        line = Mathf.Max(line, Fill(flame) * 0.8f);
        return Draw(line, Fill(box));
    }

    static float Star(Vector2 p)
    {
        // The Leak: a dark disc where the ink comes through, five long arms curling as they reach out.
        float d = Circle(p, Vector2.Zero, 0.2f);
        float arms = 9f;
        for (int k = 0; k < 5; k++)
        {
            float a0 = -Mathf.Pi / 2f + k * Mathf.Tau / 5f;
            var prev = Vector2.FromAngle(a0) * 0.18f;
            for (int s = 1; s <= 6; s++)
            {
                float t = s / 6f;
                var next = Vector2.FromAngle(a0 + 0.55f * t * t) * (0.18f + 0.72f * t);
                arms = Mathf.Min(arms, Segment(p, prev, next) - 0.07f * (1f - 0.75f * t));
                prev = next;
            }
        }
        float line = Mathf.Max(Line(d, 0.035f), Fill(arms));
        return Draw(line, Fill(d));
    }

    static float Clam(Vector2 p)
    {
        // An open clam seen from above: its upper shell a fan of ribs, its pearl in the lower lip.
        var hinge = new Vector2(0f, 0.55f);
        var v = p - hinge;
        float r = v.Length();
        float ang = Mathf.Atan2(v.X, -v.Y);
        float fan = Mathf.Max(r - 0.95f, Mathf.Abs(ang) - 0.95f);
        float ribs = 9f;
        for (int k = -3; k <= 3; k++)
        {
            float a = k * 0.27f;
            ribs = Mathf.Min(ribs, Segment(p, hinge, hinge + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * 0.9f));
        }
        float edge = Mathf.Abs(fan);
        float lip = Mathf.Abs(Ellipse(p, new Vector2(0f, 0.5f), new Vector2(0.6f, 0.2f)));
        float pearl = Circle(p, new Vector2(0f, 0.42f), 0.15f);
        float line = Mathf.Max(Line(edge, 0.035f), Line(ribs, 0.016f) * (fan < 0f ? 1f : 0f));
        line = Mathf.Max(line, Line(lip, 0.03f));
        line = Mathf.Max(line, Fill(pearl));
        return Draw(line, Fill(fan));
    }

    static float Spiral(Vector2 p)
    {
        // A whirl drawing in: three turns closing on its middle, and a ring round it.
        float r = p.Length();
        float a = Mathf.Atan2(p.Y, p.X);
        const float turn = 0.26f;
        float best = 9f;
        for (int k = -1; k <= 3; k++)
        {
            float rr = (a / Mathf.Tau + k) * turn + 0.08f;
            if (rr < 0.05f || rr > 0.85f) continue;
            best = Mathf.Min(best, Mathf.Abs(r - rr));
        }
        float line = Line(best, 0.035f) * (r < 0.86f ? 1f : 0f);
        line = Mathf.Max(line, Line(Mathf.Abs(r - 0.92f), 0.025f));
        return Draw(line, Fill(r - 0.92f));
    }
}
