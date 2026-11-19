using System.Linq;
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
        // (The Drowned Temple's and Atlantis's stone is the Ruins' kind: their drums, blocks, amphorae, walls and arches.)
        if (!map.Templated || map.Biome is not (Biome.Shipwreck or Biome.SunkenRuins or Biome.DrownedTemple or Biome.Atlantis)) return;
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
        int pieces = Pieces(map, wreck, wood, oldWood, iron, marble, Hash);
        if (OS.GetCmdlineUserArgs().Length > 0) GD.Print($"Biome props: {pieces} larger pieces, {_sets.Values.Sum(v => v.At.Count)} parts in all");
        foreach (var (mesh, material, list) in _sets.Values)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = list.Count };
            for (int i = 0; i < list.Count; i++) mm.SetInstanceTransform(i, list[i]);
            AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material });
        }
    }

    /// <summary>The things' size against a real one's: a little larger, so they read from her camera's height.</summary>
    const float Big = 1.5f;

    /// <summary>
    /// The larger pieces, a few a level on the open seabed under her plane (so she swims over them), far apart: in the
    /// Shipwreck a broken hull section (its keel, its ribs curving up on both sides, a few planks still on them) and a
    /// fallen mast with its yard; in the Ruins a crumbled building wall (a run of cut blocks, broken down unevenly, some
    /// fallen beside it) and a fallen arch (two column stumps and its lintel). Some walls stand on the rock tops too.
    /// </summary>
    int Pieces(LevelMap map, bool wreck, Material wood, Material oldWood, Material iron, Material marble, System.Func<float, float, int, float> hash)
    {
        var placed = new List<System.Numerics.Vector2>();
        const float step = 6f, apart = 13f;
        for (float y = LevelMap.RimWidth + 4f; y < map.Size - LevelMap.RimWidth - 4f; y += step)
        for (float x = LevelMap.RimWidth + 4f; x < map.Size - LevelMap.RimWidth - 4f; x += step)
        {
            var p = new System.Numerics.Vector2(x + hash(x, y, 21) * step, y + hash(x, y, 22) * step);
            if (hash(x, y, 23) > 0.35f || placed.Exists(q => System.Numerics.Vector2.Distance(q, p) < apart)) continue;
            if (map.InShaft(p, 5f) || System.Numerics.Vector2.Distance(p, map.Start.Position) < map.Start.Radius + 5f) continue;
            if (map.Pois.Exists(q => System.Numerics.Vector2.Distance(q.Position, p) < q.Radius + 4f)) continue;
            float h = map.HeightAt(p);
            bool top = h > 0.8f && map.Gradient(p).Length() < 0.6f;
            // (On the floor: under her plane, and the ground round it all floor.)
            bool floor = h < -1.3f;
            for (int i = 0; i < 8 && floor; i++)
            {
                float a = i * Mathf.Tau / 8f;
                floor = map.HeightAt(p + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f) < -0.9f;
            }
            if (!floor && !(top && !wreck)) continue;
            placed.Add(p);
            var at = new Vector3(p.X, h, p.Y);
            // (A piece on a shallow floor is laid lower, so it never stands up through her plane.)
            float room = top ? 2f : Mathf.Clamp((LevelMap.SwimBand - 0.3f - h) / 1.6f, 0.45f, 1f);
            var turn = new Basis(Vector3.Up, hash(x, y, 24) * Mathf.Tau).Scaled(new Vector3(1f, room, 1f));
            Vector3 On(float ax, float ay, float az) => at + turn * new Vector3(ax, ay, az);
            if (wreck && hash(x, y, 25) < 0.6f)
            {
                // A hull section: its keel, ribs up both sides, leaning out, and planks along them.
                float lean = 0.45f;
                Add("keel", Box(6.4f, 0.32f, 0.42f), oldWood, new Transform3D(turn, On(0f, 0.16f, 0f)));
                for (int k = 0; k < 7; k++)
                {
                    float rx = -2.7f + k * 0.9f;
                    float tall = 1.1f + 0.5f * hash(x + k, y, 26);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var rib = turn * new Basis(Vector3.Right, side * lean);
                        Add("rib", Box(0.16f, 1f, 0.22f), wood, new Transform3D(rib.Scaled(new Vector3(1f, tall, 1f)), On(rx, tall * 0.45f, side * (0.35f + tall * 0.2f))));
                    }
                }
                for (int k = 0; k < 3; k++)
                {
                    float side = k % 2 == 0 ? 1f : -1f, len = 2.4f + 2.6f * hash(x, y + k, 27);
                    var plank = turn * new Basis(Vector3.Right, side * lean);
                    Add("hullplank", Box(1f, 0.08f, 0.3f), oldWood, new Transform3D(plank * Basis.FromScale(new Vector3(len, 1f, 1f)), On(-2f + len * 0.5f, 0.35f + 0.3f * k, side * 0.6f)));
                }
            }
            else if (wreck)
            {
                // A fallen mast, its yard still across it near the top, an iron band round it.
                Add("mast", Cyl(0.2f, 0.26f, 7.5f), oldWood, new Transform3D(turn * new Basis(Vector3.Forward, Mathf.Pi / 2f), On(0f, 0.24f, 0f)));
                Add("yard", Cyl(0.12f, 0.12f, 4.2f), wood, new Transform3D(turn * new Basis(Vector3.Right, Mathf.Pi / 2f), On(2.6f, 0.45f, 0f)));
                Add("band", Cyl(0.29f, 0.29f, 0.18f), iron, new Transform3D(turn * new Basis(Vector3.Forward, Mathf.Pi / 2f), On(-1.2f, 0.24f, 0f)));
            }
            else if (hash(x, y, 25) < 0.65f || top)
            {
                // A crumbled wall: a run of cut blocks, two or three courses where it still stands, broken down to one at
                // its ends, a few blocks fallen beside it.
                int blocks = 5 + (int)(hash(x, y, 28) * 4f);
                for (int k = 0; k < blocks; k++)
                {
                    float bx = (k - blocks * 0.5f) * 1.05f;
                    float edge = Mathf.Min(k, blocks - 1 - k);
                    int courses = 1 + (int)Mathf.Min(edge, 2f * hash(x + k, y, 29) + 1f);
                    for (int c = 0; c < courses; c++)
                        Add("wallblock", Box(1f, 0.55f, 0.7f), marble, new Transform3D(turn * new Basis(Vector3.Up, (hash(x + k, y + c, 30) - 0.5f) * 0.12f), On(bx + (c % 2) * 0.5f, 0.28f + c * 0.56f, 0f)));
                    if (hash(x + k, y, 31) < 0.3f)
                        Add("fallen", Box(1f, 0.55f, 0.7f), marble, new Transform3D(turn * new Basis(Vector3.Up, hash(x + k, y, 32) * 1.2f), On(bx + 0.3f, 0.28f, 1.2f + hash(x + k, y, 33))));
                }
            }
            else
            {
                // A fallen arch: two column stumps, and its lintel lying across where it fell.
                // (One stump mesh a metre tall, each scaled to its own height.)
                Add("stump", Cyl(0.5f, 0.55f, 1f), marble, new Transform3D(turn * Basis.FromScale(new Vector3(1f, 1.4f, 1f)), On(-1.8f, 0.7f, 0f)));
                Add("stump", Cyl(0.5f, 0.55f, 1f), marble, new Transform3D(turn * Basis.FromScale(new Vector3(1f, 0.8f, 1f)), On(1.8f, 0.4f, 0f)));
                Add("lintel", Box(4.4f, 0.6f, 0.8f), marble, new Transform3D(turn * new Basis(Vector3.Up, 0.35f), On(0.4f, 0.3f, 1.3f)));
            }
        }
        return placed.Count;
    }

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
