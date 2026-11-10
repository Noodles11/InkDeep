using System;
using System.Collections.Generic;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// A labyrinth's plan (docs/DIVE.md §6.1): a grid of cells, each joined to some of its four neighbours. Laid as a growing
/// tree from its entrance (mostly the newest cell grown on, now and then any: long winding runs with side branches),
/// so every cell is reached by exactly one way; then a few dead ends are opened into a neighbour, so it has some loops
/// and is never only one long corridor. Every cell is reachable from every other, by construction. Its heart (where the
/// reward lies) is the dead end farthest from the entrance by the maze's own ways.
/// </summary>
public sealed class Maze
{
    public int Cols { get; }
    public int Rows { get; }
    /// <summary>Joined to the cell east of it (x+1), and to the cell south of it (y+1).</summary>
    readonly bool[] _east, _south;
    public (int X, int Y) Entrance { get; private set; }
    public (int X, int Y) Heart { get; private set; }

    Maze(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
        _east = new bool[cols * rows];
        _south = new bool[cols * rows];
    }

    static readonly (int Dx, int Dy)[] Ways = { (1, 0), (0, 1), (-1, 0), (0, -1) };

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Cols && y < Rows;

    /// <summary>Whether a cell is joined to its neighbour the way (dx, dy) (one of the four).</summary>
    public bool Joined(int x, int y, int dx, int dy)
    {
        int nx = x + dx, ny = y + dy;
        if (!InBounds(x, y) || !InBounds(nx, ny)) return false;
        if (dx == 1) return _east[y * Cols + x];
        if (dx == -1) return _east[ny * Cols + nx];
        if (dy == 1) return _south[y * Cols + x];
        return _south[ny * Cols + nx];
    }

    void Join(int x, int y, int dx, int dy)
    {
        int nx = x + dx, ny = y + dy;
        if (dx == 1) _east[y * Cols + x] = true;
        else if (dx == -1) _east[ny * Cols + nx] = true;
        else if (dy == 1) _south[y * Cols + x] = true;
        else _south[ny * Cols + nx] = true;
    }

    /// <summary>How many neighbours a cell is joined to.</summary>
    public int Degree(int x, int y)
    {
        int n = 0;
        foreach (var (dx, dy) in Ways)
            if (Joined(x, y, dx, dy)) n++;
        return n;
    }

    /// <summary>Every join once: (x, y) to (x + dx, y + dy), east or south.</summary>
    public IEnumerable<(int X, int Y, int Dx, int Dy)> Joins()
    {
        for (int y = 0; y < Rows; y++)
        for (int x = 0; x < Cols; x++)
        {
            if (_east[y * Cols + x]) yield return (x, y, 1, 0);
            if (_south[y * Cols + x]) yield return (x, y, 0, 1);
        }
    }

    /// <summary>Steps from a cell to every cell by the maze's ways (−1: unreachable, which never happens).</summary>
    public int[] Steps(int fromX, int fromY)
    {
        var steps = new int[Cols * Rows];
        Array.Fill(steps, -1);
        var queue = new Queue<(int, int)>();
        steps[fromY * Cols + fromX] = 0;
        queue.Enqueue((fromX, fromY));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in Ways)
            {
                if (!Joined(x, y, dx, dy)) continue;
                int i = (y + dy) * Cols + x + dx;
                if (steps[i] >= 0) continue;
                steps[i] = steps[y * Cols + x] + 1;
                queue.Enqueue((x + dx, y + dy));
            }
        }
        return steps;
    }

    /// <param name="newest">How often the newest open cell is grown on (else any open one): the higher, the longer and more winding its runs.</param>
    /// <param name="loops">The share of dead ends (not the entrance's) opened into another neighbour.</param>
    public static Maze Build(int cols, int rows, (int X, int Y) entrance, Rng rng, float newest = 0.75f, float loops = 0.12f)
    {
        if (cols < 2 || rows < 2) throw new ArgumentException("a maze needs at least 2 × 2 cells");
        var m = new Maze(cols, rows) { Entrance = entrance };
        var seen = new bool[cols * rows];
        var open = new List<(int X, int Y)> { entrance };
        seen[entrance.Y * cols + entrance.X] = true;
        var choices = new List<(int, int)>(4);
        while (open.Count > 0)
        {
            int k = rng.NextFloat() < newest ? open.Count - 1 : rng.Int(open.Count);
            var (x, y) = open[k];
            choices.Clear();
            foreach (var (dx, dy) in Ways)
                if (m.InBounds(x + dx, y + dy) && !seen[(y + dy) * cols + x + dx]) choices.Add((dx, dy));
            if (choices.Count == 0)
            {
                open.RemoveAt(k);
                continue;
            }
            var (cx, cy) = choices[rng.Int(choices.Count)];
            m.Join(x, y, cx, cy);
            seen[(y + cy) * cols + x + cx] = true;
            open.Add((x + cx, y + cy));
        }
        // A few loops: some dead ends opened into a neighbour they are not joined to.
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            if ((x, y) == entrance || m.Degree(x, y) != 1 || rng.NextFloat() >= loops) continue;
            choices.Clear();
            foreach (var (dx, dy) in Ways)
                if (m.InBounds(x + dx, y + dy) && !m.Joined(x, y, dx, dy)) choices.Add((dx, dy));
            if (choices.Count > 0)
            {
                var (dx, dy) = choices[rng.Int(choices.Count)];
                m.Join(x, y, dx, dy);
            }
        }
        // Its heart: the dead end farthest from the entrance (any cell, should none be left).
        var steps = m.Steps(entrance.X, entrance.Y);
        (int X, int Y) heart = entrance;
        int far = -1;
        for (int pass = 0; pass < 2 && far < 0; pass++)
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                if ((x, y) == entrance || pass == 0 && m.Degree(x, y) != 1) continue;
                if (steps[y * cols + x] > far)
                {
                    far = steps[y * cols + x];
                    heart = (x, y);
                }
            }
        m.Heart = heart;
        return m;
    }
}
