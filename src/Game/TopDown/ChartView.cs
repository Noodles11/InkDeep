using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>The pause menu's chart (docs/proposals/CUBE-WORLD-PLAN.md §15.5): her layer of the cube reef.</summary>
public partial class ChartView : Control
{
    const float Top = 34f, Side = 34f;
    static readonly Color Ink = new(0.05f, 0.08f, 0.12f, 0.9f), Faint = new(0.6f, 0.7f, 0.8f, 0.35f), Gold = OctoShoots.Game.Ui.DrawerStyle.Gold;

    public ChartView()
    {
        CustomMinimumSize = new Vector2(0f, 420f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    /// <summary>
    /// The cube reef's chart (docs/proposals/CUBE-WORLD-PLAN.md §15.5): her layer's footprint, each cube a brick; those she
    /// has claimed filled, with a mark for each place behind a mouth in it; the arena in the boss's colour once found;
    /// the cubes next to her claimed ones a faint outline; the edges the wake current holds red; she a gold dot.
    /// </summary>
    public sealed record ReefChart(int Footprint, int Layer, int Deepest, IReadOnlyList<(OctoShoots.Core.Gen.Cubes.CubeId Cube, bool Claimed, bool Arena, IReadOnlyList<PoiKind> Places)> Cubes,
        IReadOnlyList<OctoShoots.Core.Gen.Cubes.EdgeId> Shut, System.Numerics.Vector2 Her);

    ReefChart? _reef;

    public void Show(ReefChart chart)
    {
        _reef = chart;
        QueueRedraw();
    }

    static ReefKind KindOf(PoiKind place) => place switch
    {
        PoiKind.Haven => ReefKind.Haven,
        PoiKind.TreasureCave => ReefKind.Treasure,
        PoiKind.LeakCave => ReefKind.Den,
        _ => ReefKind.Reef,
    };

    void DrawReef(ReefChart chart)
    {
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(4f, 18f), chart.Layer == 0 ? "The warm-up reef" : $"Layer {chart.Layer}  ·  deepest {chart.Deepest}", HorizontalAlignment.Left, -1, 18, Gold);
        float metres = chart.Footprint * OctoShoots.Core.Gen.Cubes.CubeTuning.Side;
        float scale = Mathf.Min(Size.X - 2f * Side, CustomMinimumSize.Y - Top - 70f) / metres;
        var origin = new Vector2((Size.X - metres * scale) * 0.5f, Top);
        Vector2 At(System.Numerics.Vector2 p) => origin + new Vector2(p.X, p.Y) * scale;
        DrawRect(new Rect2(origin, new Vector2(metres, metres) * scale), new Color(Ink, 0.5f));
        foreach (var (cube, claimed, arena, places) in chart.Cubes)
        {
            var rect = new Rect2(At(cube.Origin) + Vector2.One * 1.5f, new Vector2(cube.Size.X, cube.Size.Y) * scale - Vector2.One * 3f);
            var colour = arena ? ReefKinds.Colour(ReefKind.Boss) : new Color(0.55f, 0.75f, 0.9f);
            if (claimed) DrawRect(rect, new Color(colour, 0.45f));
            DrawRect(rect, claimed ? colour : Faint, false, claimed ? 2f : 1.2f);
            for (int i = 0; i < places.Count; i++)
                DrawCircle(rect.Position + new Vector2(9f + 14f * i, 9f), 5f, ReefKinds.Colour(KindOf(places[i])));
        }
        foreach (var e in chart.Shut)
            DrawLine(At(e.Point(0f, 0f)), At(e.Point(OctoShoots.Core.Gen.Cubes.CubeTuning.Side, 0f)), new Color(0.95f, 0.35f, 0.3f, 0.9f), 3f, true);
        DrawCircle(At(chart.Her), 6f, Gold);
        DrawArc(At(chart.Her), 9f, 0f, Mathf.Tau, 24, new Color(Gold, 0.4f), 1.5f, true);
        float y = CustomMinimumSize.Y - 40f, x = 4f;
        foreach (var (kind, name) in new[] { (ReefKind.Haven, "Haven"), (ReefKind.Treasure, "Clam room"), (ReefKind.Den, "Leak cave"), (ReefKind.Reef, "Secret"), (ReefKind.Boss, "Arena") })
        {
            DrawCircle(new Vector2(x + 7f, y), 6f, ReefKinds.Colour(kind));
            DrawString(font, new Vector2(x + 17f, y + 5f), name, HorizontalAlignment.Left, -1, 14, Faint.Lightened(0.6f));
            x += 30f + font.GetStringSize(name, HorizontalAlignment.Left, -1, 14).X;
        }
        DrawString(font, new Vector2(4f, y + 26f), "Filled: claimed · faint: not yet entered · red: held by the wake current", HorizontalAlignment.Left, -1, 13, Faint.Lightened(0.4f));
    }

    public override void _Draw()
    {
        if (_reef is { } chart) DrawReef(chart);
    }
}
