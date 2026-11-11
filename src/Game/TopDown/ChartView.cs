using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The chart (docs/LIGHT-AND-GRAPH.md §6.3): the pause menu's map of the dive's graph through the depth she is
/// in, and the first row of the one below. The reefs she has been on, filled; the one she is on, ringed in gold; the
/// reefs she can see from here (down to Sight rows below her, and every hole's of a reef she has been on) in the colour
/// of their kind; the rest unknown, a faint ring. The holes between them as lines, brighter where both ends are known.
/// </summary>
public partial class ChartView : Control
{
    const float RowGap = 64f, Top = 34f, Side = 34f, Node = 11f;
    static readonly Color Ink = new(0.05f, 0.08f, 0.12f, 0.9f), Faint = new(0.6f, 0.7f, 0.8f, 0.35f), Gold = OctoShoots.Game.Ui.DrawerStyle.Gold;

    DiveGraph? _graph;
    LevelId _at;
    readonly HashSet<LevelId> _visited = new(), _known = new();
    readonly List<(DiveNode Node, Vector2 At)> _shown = new();
    string _title = "";

    public ChartView()
    {
        CustomMinimumSize = new Vector2(0f, Top + RowGap * (LevelStocking.LevelsPerDepth + 1) + 70f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    /// <param name="sight">How many rows below her she can see (her cartographer pearls: two more each).</param>
    public void Show(DiveGraph graph, LevelId at, IEnumerable<LevelId> visited, int sight)
    {
        _graph = graph;
        _at = at with { Haven = false };
        _visited.Clear();
        foreach (var v in visited) _visited.Add(v with { Haven = false });
        _visited.Add(_at);
        // What she can see: every hole's reef of a reef she has been on, and down from her as far as her sight.
        _known.Clear();
        foreach (var v in _visited)
        {
            _known.Add(v);
            foreach (var e in graph.ExitsOf(v)) _known.Add(e with { Haven = false });
        }
        var front = new List<LevelId> { _at };
        for (int step = 0; step < sight; step++)
        {
            var next = new List<LevelId>();
            foreach (var id in front)
                foreach (var e in graph.ExitsOf(id))
                {
                    var reef = e with { Haven = false };
                    _known.Add(reef);
                    next.Add(reef);
                }
            front = next.Distinct().ToList();
        }
        _title = at.IsTutorial ? "The tutorial" : $"Depth {at.ShownDepth}{(at.Cycle > 1 ? $" · cycle {at.Cycle}" : "")}";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_graph is null) return;
        var font = ThemeDB.FallbackFont;
        float width = Size.X - 2f * Side;
        _shown.Clear();
        int depth = _at.Depth, cycle = _at.Cycle;
        int rows = DiveGraph.RowsOf(depth);
        Vector2 Place(DiveNode n, int line) => new(Side + n.X * width, Top + line * RowGap);
        for (int row = 1; row <= rows; row++)
            foreach (var n in _graph.Row(cycle, depth, row)) _shown.Add((n, Place(n, row - 1)));
        // The depth below's first row, under its boss's reef.
        var below = _graph.ExitsOf(_graph.Row(cycle, depth, rows)[0].Id).Select(_graph.Node).ToList();
        foreach (var n in below) _shown.Add((n, Place(n, rows)));
        var at = _shown.ToDictionary(s => s.Node.Id with { Haven = false }, s => s.At);
        DrawString(font, new Vector2(4f, 18f), _title, HorizontalAlignment.Left, -1, 18, Gold);
        if (below.Count > 0)
            DrawString(font, new Vector2(4f, Top + rows * RowGap - 18f), $"Depth {below[0].Id.ShownDepth}", HorizontalAlignment.Left, -1, 14, Faint);
        bool Known(LevelId id) => _known.Contains(id with { Haven = false });
        // The holes.
        foreach (var (n, p) in _shown)
            foreach (var e in n.Exits)
            {
                if (!at.TryGetValue(e with { Haven = false }, out var q)) continue;
                bool seen = Known(n.Id) && Known(e);
                bool walked = _visited.Contains(n.Id with { Haven = false }) && _visited.Contains(e with { Haven = false });
                var colour = walked ? new Color(Gold, 0.85f) : seen ? new Color(0.75f, 0.85f, 0.95f, 0.55f) : new Color(0.6f, 0.7f, 0.8f, 0.14f);
                DrawLine(p, q, colour, walked ? 3f : 1.6f, true);
            }
        // The reefs.
        foreach (var (n, p) in _shown)
        {
            var id = n.Id with { Haven = false };
            bool here = id == _at, been = _visited.Contains(id), known = Known(id);
            if (!known)
            {
                DrawArc(p, Node * 0.6f, 0f, Mathf.Tau, 24, Faint, 1.5f, true);
                continue;
            }
            var colour = ReefKinds.Colour(n.Kind);
            DrawCircle(p, Node + 1.5f, Ink);
            DrawCircle(p, Node, been ? colour : new Color(colour, 0.35f));
            DrawArc(p, Node, 0f, Mathf.Tau, 32, colour, 2f, true);
            if (here)
            {
                DrawArc(p, Node + 5f, 0f, Mathf.Tau, 40, Gold, 3f, true);
                DrawArc(p, Node + 8.5f, 0f, Mathf.Tau, 40, new Color(Gold, 0.35f), 1.5f, true);
            }
        }
        // The key.
        float y = Top + (rows + 1) * RowGap - 10f, x = 4f;
        foreach (var kind in new[] { ReefKind.Reef, ReefKind.Treasure, ReefKind.Den, ReefKind.Haven, ReefKind.Boss })
        {
            DrawCircle(new Vector2(x + 7f, y), 6f, ReefKinds.Colour(kind));
            string name = ReefKinds.Name(kind);
            DrawString(font, new Vector2(x + 17f, y + 5f), name, HorizontalAlignment.Left, -1, 14, Faint.Lightened(0.6f));
            x += 30f + font.GetStringSize(name, HorizontalAlignment.Left, -1, 14).X;
        }
        DrawString(font, new Vector2(4f, y + 26f), "Gold ring: here · filled: been there · faint ring: not yet seen", HorizontalAlignment.Left, -1, 13, Faint.Lightened(0.4f));
    }
}
