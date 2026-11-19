using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The biomes' own things (docs/proposals/CROSSOVER.md §8), for the look only (nothing in play depends on them): in the
/// Shipwreck Graveyard the wreck's planks, barrels, crates and a rusted cannon now and then, on its floor and on top of
/// its walls; in the Sunken Ruins standing column drums, fallen columns, cut blocks and amphorae. A deterministic
/// scatter from the level's seed, kept off the start, the ways down and the places. Drawn in the scene (so the ink look
/// takes them as it takes the reef: grey out of her light, their colour in it).
/// </summary>
public partial class BiomePropView : Node3D
{
    readonly Dictionary<string, (Mesh Mesh, Material Material, List<Transform3D> At)> _sets = new();

    public void Show(LevelMap map, ReefLook look)
    {
        foreach (Node n in GetChildren()) n.QueueFree();
        _sets.Clear();
        if (!map.Templated || map.Biome is not (Biome.Shipwreck or Biome.SunkenRuins)) return;
        uint seed = unchecked((uint)(map.Seed ^ (map.Seed >> 32)) ^ (uint)(map.Level * 4241 + map.Depth * 52711 + map.Id.Branch * 977) ^ 0x7B1D9Fu);
        float Hash(float x, float y, int k)
        {
            uint h = unchecked((uint)((int)(x * 10f) * 73856093) ^ (uint)((int)(y * 10f) * 19349663) ^ (uint)(k * 83492791) ^ seed);
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
        bool wreck = map.Biome == Biome.Shipwreck;
        var wood = Mat(new Color(0.4f, 0.28f, 0.17f), 0.9f);
        var oldWood = Mat(new Color(0.3f, 0.24f, 0.18f), 0.95f);
        var iron = Mat(new Color(0.24f, 0.22f, 0.21f), 0.6f, 0.6f);
        var marble = Mat(look.Rock.Lerp(new Color(0.9f, 0.88f, 0.82f), 0.6f), 0.85f);
        var terracotta = Mat(look.Coral.Lerp(new Color(0.72f, 0.42f, 0.28f), 0.6f), 0.9f);
        const float step = 2.3f;
        for (float y = LevelMap.RimWidth + 1f; y < map.Size - LevelMap.RimWidth - 1f; y += step)
        for (float x = LevelMap.RimWidth + 1f; x < map.Size - LevelMap.RimWidth - 1f; x += step)
        {
            var p = new System.Numerics.Vector2(x + Hash(x, y, 1) * step, y + Hash(x, y, 2) * step);
            if (map.InShaft(p, 3f) || System.Numerics.Vector2.Distance(p, map.Start.Position) < map.Start.Radius + 2f) continue;
            if (map.Pois.Exists(q => System.Numerics.Vector2.Distance(q.Position, p) < q.Radius + 2.5f)) continue;
            float h = map.HeightAt(p), slope = map.Gradient(p).Length();
            float r = Hash(x, y, 3), pick = Hash(x, y, 4), yaw = Hash(x, y, 5) * Mathf.Tau, s = (0.8f + 0.5f * Hash(x, y, 6)) * Big;
            bool top = h > 0.6f && slope < 1.1f, floor = h < -0.6f && slope < 0.7f;
            if (!top && !floor || r > (top ? 0.16f : 0.11f)) continue;
            var at = new Vector3(p.X, h, p.Y);
            var turn = new Basis(Vector3.Up, yaw);
            if (wreck)
            {
                if (top || pick < 0.4f) Add("plank", Box(2.4f, 0.12f, 0.34f), pick < 0.2f ? oldWood : wood, new Transform3D(turn.Scaled(Vector3.One * s), at + Vector3.Up * 0.06f));
                else if (pick < 0.65f) Add("barrel", Cyl(0.34f, 0.3f, 0.85f), oldWood, new Transform3D((turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big), at + Vector3.Up * 0.32f * Big));
                else if (pick < 0.9f) Add("crate", Box(0.85f, 0.7f, 0.85f), wood, new Transform3D(turn.Scaled(Vector3.One * s), at + Vector3.Up * 0.32f * s));
                else Add("cannon", Cyl(0.16f, 0.22f, 1.7f), iron, new Transform3D((turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big), at + Vector3.Up * 0.22f * Big));
            }
            else
            {
                if (top ? pick < 0.6f : pick < 0.2f) Add("drum", Cyl(0.5f, 0.52f, 0.9f), marble, new Transform3D(turn.Scaled(new Vector3(s, 0.6f + 0.8f * Hash(x, y, 7), s)), at + Vector3.Up * 0.3f));
                else if (pick < 0.45f) Add("column", Cyl(0.42f, 0.42f, 2.8f), marble, new Transform3D((turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big), at + Vector3.Up * 0.4f * Big));
                else if (pick < 0.75f) Add("block", Box(1.1f, 0.6f, 0.75f), marble, new Transform3D(turn.Scaled(Vector3.One * s), at + Vector3.Up * 0.28f * s));
                else
                {
                    // An amphora lying on its side: its belly and its neck.
                    var lie = (turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big);
                    Add("amphora", new SphereMesh { Radius = 0.32f, Height = 0.95f, RadialSegments = 14, Rings = 8 }, terracotta, new Transform3D(lie, at + Vector3.Up * 0.3f * Big));
                    Add("neck", Cyl(0.1f, 0.13f, 0.4f), terracotta, new Transform3D(lie, at + Vector3.Up * 0.3f * Big + lie * new Vector3(0f, 0.6f, 0f)));
                }
            }
        }
        foreach (var (mesh, material, list) in _sets.Values)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = list.Count };
            for (int i = 0; i < list.Count; i++) mm.SetInstanceTransform(i, list[i]);
            AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material });
        }
    }

    /// <summary>The things' size against a real one's: a little larger, so they read from her camera's height.</summary>
    const float Big = 1.5f;

    void Add(string key, Mesh mesh, Material material, Transform3D at)
    {
        string k = key + material.GetInstanceId();
        if (!_sets.TryGetValue(k, out var set)) _sets[k] = set = (mesh, material, new List<Transform3D>());
        set.At.Add(at);
    }

    static StandardMaterial3D Mat(Color c, float rough, float metal = 0f) => new() { AlbedoColor = c, Roughness = rough, Metallic = metal };
    static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };
    static CylinderMesh Cyl(float top, float bottom, float h) => new() { TopRadius = top, BottomRadius = bottom, Height = h, RadialSegments = 14, Rings = 1 };
}
