using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The debug view's map of an open level's chunks round her (F3, with the debug map): the one she is in (gold), those
/// she has been in (teal), those made but not yet visited (grey), those not made yet (outlined); a bar across every side where two are
/// joined; each made chunk's template (its cells' names) and its holes down (blue, ringed once she has found one).
/// </summary>
public partial class SeaChunkMap : Control
{
    const int Reach = 5;
    const float Across = 360f;
    OpenSea? _sea;
    System.Numerics.Vector2 _her;
    (int X, int Y) _here;
    IReadOnlySet<(int X, int Y)> _visited = new HashSet<(int X, int Y)>();
    IReadOnlyDictionary<(int X, int Y), OpenSea.Chunk> _built = new Dictionary<(int X, int Y), OpenSea.Chunk>();
    IReadOnlyList<System.Numerics.Vector2> _found = new List<System.Numerics.Vector2>();

    public void Sync(OpenSea sea, System.Numerics.Vector2 her, (int X, int Y) here, IReadOnlySet<(int X, int Y)> visited,
        IReadOnlyDictionary<(int X, int Y), OpenSea.Chunk> built, IReadOnlyList<System.Numerics.Vector2> found)
    {
        (_sea, _her, _here, _visited, _built, _found) = (sea, her, here, visited, built, found);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_sea is not { } sea) return;
        var font = ThemeDB.FallbackFont;
        float scale = Across / ((2 * Reach + 1) * OpenSea.Cell);
        var corner = new Vector2(GetViewportRect().Size.X - Across - 16f, 16f);
        var centre = corner + new Vector2(Across / 2f, Across / 2f);
        Vector2 P(System.Numerics.Vector2 p) => centre + new Vector2(p.X - _her.X, p.Y - _her.Y) * scale;
        DrawRect(new Rect2(corner, new Vector2(Across, Across)), new Color(0f, 0f, 0f, 0.6f));
        var cell = OpenSea.CellOf(_her);
        var keys = new HashSet<(int X, int Y)>();
        for (int y = cell.Y - Reach; y <= cell.Y + Reach; y++)
        for (int x = cell.X - Reach; x <= cell.X + Reach; x++)
            keys.Add(sea.ChunkOfCell((x, y)));
        foreach (var k in keys)
        {
            var (min, max) = sea.RectOf(k);
            var r = new Rect2(P(min), new Vector2(max.X - min.X, max.Y - min.Y) * scale);
            var fill = k == _here ? new Color(1f, 0.8f, 0.3f, 0.7f) : _visited.Contains(k) ? new Color(0.3f, 0.85f, 0.8f, 0.45f)
                : _built.ContainsKey(k) ? new Color(0.6f, 0.62f, 0.66f, 0.3f) : new Color(0f, 0f, 0f, 0f);
            DrawRect(r.Grow(-1.5f), fill);
            DrawRect(r.Grow(-1.5f), new Color(1f, 1f, 1f, 0.35f), false, 1f);
            if (_built.TryGetValue(k, out var chunk))
            {
                DrawString(font, r.Position + new Vector2(3f, 10f), chunk.Template, HorizontalAlignment.Left, r.Size.X - 4f, 8, new Color(1f, 1f, 1f, 0.6f));
                foreach (var h in chunk.Holes)
                {
                    DrawCircle(P(h), 4f, new Color(0.45f, 0.75f, 1f));
                    if (_found.Contains(h)) DrawArc(P(h), 7f, 0f, Mathf.Tau, 20, new Color(0.45f, 0.75f, 1f), 1.5f, true);
                }
            }
            foreach (var n in sea.Neighbours(k))
                if (keys.Contains(n) && k.CompareTo(n) < 0 && sea.Linked(k, n))
                {
                    // (A way between them: a bar across the middle of the side they share.)
                    var (a0, a1) = sea.RectOf(n);
                    var lo = System.Numerics.Vector2.Max(min, a0);
                    var hi = System.Numerics.Vector2.Min(max, a1);
                    var mid = (lo + hi) * 0.5f;
                    var along = hi.X - lo.X > hi.Y - lo.Y ? new System.Numerics.Vector2(3f, 0f) : new System.Numerics.Vector2(0f, 3f);
                    DrawLine(P(mid - along), P(mid + along), new Color(1f, 0.95f, 0.75f, 0.9f), 3f, true);
                }
        }
        DrawCircle(centre, 3.5f, new Color(1f, 0.95f, 0.8f));
        DrawString(font, corner + new Vector2(4f, Across - 6f), $"chunk {_here}  made {_built.Count}  visited {_visited.Count}  holes found {_found.Count}", HorizontalAlignment.Left, -1, 11, Colors.White);
    }
}
