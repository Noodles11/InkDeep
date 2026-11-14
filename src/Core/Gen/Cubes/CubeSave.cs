using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// A dive in the cube reef as a haven's checkpoint keeps it (CUBE-WORLD-PLAN §10): the reef's size, her layer and the
/// deepest she reached, the director's counts and the boss streak, each layer's arena and whether its boss is freed, the
/// records of her layer and the one above, and the mouth of the haven she is in. Terrain, meshes and creatures are never
/// kept: the reef is made again from the seed and this is laid over it.
/// </summary>
public sealed class CubeDiveSave
{
    public int Footprint { get; set; }
    public int Layers { get; set; }
    public int Layer { get; set; }
    public int Deepest { get; set; }
    /// <summary>The haven she is in: its mouth on her layer's map (as granted, in claim order).</summary>
    public int HavenMouth { get; set; } = -1;
    public CubeDirectorSave Director { get; set; } = new();
    /// <summary>Each layer's arena (its cube, CubeKey) whose boss has been rolled, and the layers whose boss is freed.</summary>
    public Dictionary<int, string> Arenas { get; set; } = new();
    public List<int> BossRolled { get; set; } = new();
    public List<int> BossFreed { get; set; } = new();
    public List<CubeLayerSave> Records { get; set; } = new();

    public static string CubeKey(CubeId c) => $"{c.X}/{c.Y}/{c.Z}/{(c.OnEnd ? 1 : 0)}";

    public static CubeId? FromCubeKey(string key)
    {
        var p = key.Split('/');
        return p.Length == 4 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y) && int.TryParse(p[2], out int z) && int.TryParse(p[3], out int e)
            ? new CubeId(x, y, z, e == 1) : null;
    }

    public static string EdgeKey(EdgeId e) => $"{e.X}/{e.Y}/{e.Z}/{(int)e.Axis}";

    public static EdgeId? FromEdgeKey(string key)
    {
        var p = key.Split('/');
        return p.Length == 4 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y) && int.TryParse(p[2], out int z) && int.TryParse(p[3], out int a)
            ? new EdgeId(x, y, z, (EdgeAxis)a) : null;
    }
}

/// <summary>The director's counts (CubeDirector), kept.</summary>
public sealed class CubeDirectorSave
{
    public int Streak { get; set; }
    public List<CubeDirectorLayerSave> Layers { get; set; } = new();
}

public sealed class CubeDirectorLayerSave
{
    public int Layer { get; set; }
    public bool Clam { get; set; }
    public int Havens { get; set; }
    public List<PoiKind> Carried { get; set; } = new();
}

/// <summary>One layer's records (LayerRecords), kept.</summary>
public sealed class CubeLayerSave
{
    public int Layer { get; set; }
    public List<CubeRecordSave> Cubes { get; set; } = new();
    public List<string> Shut { get; set; } = new();
    public string? Last { get; set; }
}

/// <summary>One claimed cube's record (CubeRecord), kept: its claim, what she saw, its places, what is left of its stock.</summary>
public sealed class CubeRecordSave
{
    public string Cube { get; set; } = "";
    public int Claim { get; set; }
    public string Seen { get; set; } = "";
    public List<CubePlaceSave> Places { get; set; } = new();
    public List<CubeStockSave>? Left { get; set; }
}

public sealed class CubePlaceSave
{
    public PoiKind Kind { get; set; }
    public float AtX { get; set; }
    public float AtY { get; set; }
    public float IntoX { get; set; }
    public float IntoY { get; set; }
}

public sealed class CubeStockSave
{
    public int Spawn { get; set; }
    public float Seed { get; set; }
    public bool Prime { get; set; }
    public bool Shoal { get; set; }
    public List<float> At { get; set; } = new();
}

/// <summary>Records to and from their saved form.</summary>
public static class CubeRecordSaving
{
    public static CubeLayerSave Save(LayerRecords records) => new()
    {
        Layer = records.Layer,
        Cubes = records.Claimed.Values.OrderBy(r => r.Claim).Select(r => new CubeRecordSave
        {
            Cube = CubeDiveSave.CubeKey(r.Cube),
            Claim = r.Claim,
            Seen = Convert.ToBase64String(r.Seen.SelectMany(BitConverter.GetBytes).ToArray()),
            Places = r.Places.Select(p => new CubePlaceSave { Kind = p.Kind, AtX = p.Mouth.At.X, AtY = p.Mouth.At.Y, IntoX = p.Mouth.Into.X, IntoY = p.Mouth.Into.Y }).ToList(),
            Left = r.Left?.Select(l => new CubeStockSave { Spawn = l.Spawn, Seed = l.Seed, Prime = l.Prime, Shoal = l.Shoal, At = l.At.SelectMany(a => new[] { a.X, a.Y }).ToList() }).ToList(),
        }).ToList(),
        Shut = records.Shut.Select(CubeDiveSave.EdgeKey).ToList(),
        Last = records.Last is { } last ? CubeDiveSave.EdgeKey(last) : null,
    };

    public static void Load(CubeLayerSave save, LayerRecords records)
    {
        var cubes = new List<CubeRecord>();
        foreach (var c in save.Cubes)
        {
            if (CubeDiveSave.FromCubeKey(c.Cube) is not { } id) continue;
            var r = new CubeRecord(id, c.Claim);
            var bytes = Convert.FromBase64String(c.Seen);
            for (int i = 0; i < r.Seen.Length && (i + 1) * 8 <= bytes.Length; i++) r.Seen[i] = BitConverter.ToUInt64(bytes, i * 8);
            foreach (var p in c.Places) r.Places.Add((p.Kind, new Doorway(new Vector2(p.AtX, p.AtY), new Vector2(p.IntoX, p.IntoY))));
            r.Left = c.Left?.Select(l => new StockLeft(l.Spawn, l.Seed, l.Prime, l.Shoal,
                Enumerable.Range(0, l.At.Count / 2).Select(k => new Vector2(l.At[2 * k], l.At[2 * k + 1])).ToArray())).ToList();
            cubes.Add(r);
        }
        records.Restore(cubes, save.Shut.Select(CubeDiveSave.FromEdgeKey).OfType<EdgeId>(), save.Last is { } last ? CubeDiveSave.FromEdgeKey(last) : null);
    }
}
