using System.Linq;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The biomes' own things (docs/proposals/CROSSOVER.md §8), for the look only (nothing in play depends on them): in the
/// Shipwreck Graveyard the wreck's planks, barrels, crates and a rusted cannon now and then, on its floor and on top of
/// its walls; in the Sunken Ruins standing column drums, fallen columns, cut blocks and amphorae. A deterministic
/// scatter on Poisson disks (PoissonTile) from the ground's seed (<see cref="Scatter"/>, pure: a level's square, or an
/// open level's chunk on its worker), kept off the start, the ways down and the places. Drawn in the scene (so the ink
/// look takes them as it takes the reef: grey out of her light, their colour in it).
/// </summary>
public partial class BiomePropView : Node3D
{
    /// <summary>One part of a thing: its kind (<see cref="Kinds"/>) and where it lies.</summary>
    public readonly record struct Part(string Key, Transform3D At);

    enum Skin { Wood, OldWood, Iron, Marble, Terracotta }

    /// <summary>Every part's mesh and skin, by its key.</summary>
    static readonly Dictionary<string, (System.Func<Mesh> Mesh, Skin Skin)> Kinds = new()
    {
        ["plank"] = (() => Box(2.4f, 0.12f, 0.34f), Skin.Wood),
        ["oldplank"] = (() => Box(2.4f, 0.12f, 0.34f), Skin.OldWood),
        ["barrel"] = (() => Cyl(0.34f, 0.3f, 0.85f), Skin.OldWood),
        ["crate"] = (() => Box(0.85f, 0.7f, 0.85f), Skin.Wood),
        ["cannon"] = (() => Cyl(0.16f, 0.22f, 1.7f), Skin.Iron),
        ["drum"] = (() => Cyl(0.5f, 0.52f, 0.9f), Skin.Marble),
        ["column"] = (() => Cyl(0.42f, 0.42f, 2.8f), Skin.Marble),
        ["block"] = (() => Box(1.1f, 0.6f, 0.75f), Skin.Marble),
        ["amphora"] = (() => new SphereMesh { Radius = 0.32f, Height = 0.95f, RadialSegments = 14, Rings = 8 }, Skin.Terracotta),
        ["neck"] = (() => Cyl(0.1f, 0.13f, 0.4f), Skin.Terracotta),
        ["keel"] = (() => Box(6.4f, 0.32f, 0.42f), Skin.OldWood),
        ["rib"] = (() => Box(0.16f, 1f, 0.22f), Skin.Wood),
        ["hullplank"] = (() => Box(1f, 0.08f, 0.3f), Skin.OldWood),
        ["mast"] = (() => Cyl(0.2f, 0.26f, 7.5f), Skin.OldWood),
        ["yard"] = (() => Cyl(0.12f, 0.12f, 4.2f), Skin.Wood),
        ["band"] = (() => Cyl(0.29f, 0.29f, 0.18f), Skin.Iron),
        ["wallblock"] = (() => Box(1f, 0.55f, 0.7f), Skin.Marble),
        ["fallen"] = (() => Box(1f, 0.55f, 0.7f), Skin.Marble),
        ["stump"] = (() => Cyl(0.5f, 0.55f, 1f), Skin.Marble),
        ["lintel"] = (() => Box(4.4f, 0.6f, 0.8f), Skin.Marble),
    };

    readonly Dictionary<string, Mesh> _meshes = new();
    readonly Dictionary<Skin, Material> _skins = new();

    /// <summary>The level's skins (its look's marble and terracotta), and none of its things yet.</summary>
    public void SetLook(ReefLook look)
    {
        foreach (Node n in GetChildren()) n.QueueFree();
        _skins[Skin.Wood] = Mat(new Color(0.4f, 0.28f, 0.17f), 0.9f);
        _skins[Skin.OldWood] = Mat(new Color(0.3f, 0.24f, 0.18f), 0.95f);
        _skins[Skin.Iron] = Mat(new Color(0.24f, 0.22f, 0.21f), 0.6f, 0.6f);
        _skins[Skin.Marble] = Mat(look.Rock.Lerp(new Color(0.9f, 0.88f, 0.82f), 0.6f), 0.85f);
        _skins[Skin.Terracotta] = Mat(look.Coral.Lerp(new Color(0.72f, 0.42f, 0.28f), 0.6f), 0.9f);
    }

    /// <summary>A template level's things, over its whole square.</summary>
    public void Show(OctoShoots.Core.Gen.TopDown.LevelMap map, ReefLook look)
    {
        SetLook(look);
        var parts = Scatter(DecorGround.Of(map));
        if (OS.GetCmdlineUserArgs().Length > 0 && parts.Count > 0) GD.Print($"Biome props: {parts.Count} parts in all");
        AddChild(Build(parts));
    }

    /// <summary>Parts drawn (one MultiMesh to a kind), in this level's skins: a node to put where they belong.</summary>
    public Node3D Build(List<Part> parts)
    {
        var node = new Node3D();
        foreach (var group in parts.GroupBy(p => p.Key))
        {
            var (make, skin) = Kinds[group.Key];
            if (!_meshes.TryGetValue(group.Key, out var mesh)) _meshes[group.Key] = mesh = make();
            var list = group.ToList();
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = list.Count };
            for (int i = 0; i < list.Count; i++) mm.SetInstanceTransform(i, list[i].At);
            node.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = _skins[skin] });
        }
        return node;
    }

    /// <summary>The things' size against a real one's: a little larger, so they read from her camera's height.</summary>
    const float Big = 1.5f;
    /// <summary>The small things' Poisson spacing, and the larger pieces' (far apart).</summary>
    const float Spacing = 2f, PieceSpacing = 13f;

    /// <summary>A ground's things (none but in the Shipwreck, the Ruins, the Temple and Atlantis). Pure: any thread.</summary>
    internal static List<Part> Scatter(DecorGround g)
    {
        var parts = new List<Part>();
        // (The Drowned Temple's and Atlantis's stone is the Ruins' kind: their drums, blocks, amphorae, walls and arches.)
        if (g.Biome is not (Biome.Shipwreck or Biome.SunkenRuins or Biome.DrownedTemple or Biome.Atlantis)) return parts;
        uint seed = g.Salt ^ 0x7B1D9Fu;
        float Hash(float x, float y, int k) => LevelView.DecorHash(seed, x, y, k);
        void Add(string key, Transform3D at) => parts.Add(new Part(key, at));
        bool wreck = g.Biome == Biome.Shipwreck;
        // (As many to the square metre as the old grid, a step of 2.3 m, gave.)
        float more = 1f / (2.3f * 2.3f) / PoissonTile.Density(Spacing);
        foreach (var p in PoissonTile.In(Spacing, g.Lo, g.Hi))
        {
            if (g.Blocked(p, 3f) || g.NearPlace(p, 2.5f)) continue;
            float x = p.X, y = p.Y;
            float h = g.Height(p), slope = g.Gradient(p).Length();
            float r = Hash(x, y, 3), pick = Hash(x, y, 4), yaw = Hash(x, y, 5) * Mathf.Tau, s = (0.8f + 0.5f * Hash(x, y, 6)) * Big;
            bool top = h > 0.6f && slope < 1.1f, floor = h < -0.6f && slope < 0.7f;
            if (!top && !floor || r > (top ? 0.16f : 0.11f) * more) continue;
            var at = new Vector3(p.X, h, p.Y);
            var turn = new Basis(Vector3.Up, yaw);
            if (wreck)
            {
                if (top || pick < 0.4f) Add(pick < 0.2f ? "oldplank" : "plank", new Transform3D(turn.Scaled(Vector3.One * s), at + Vector3.Up * 0.06f));
                else if (pick < 0.65f) Add("barrel", new Transform3D((turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big), at + Vector3.Up * 0.32f * Big));
                else if (pick < 0.9f) Add("crate", new Transform3D(turn.Scaled(Vector3.One * s), at + Vector3.Up * 0.32f * s));
                else Add("cannon", new Transform3D((turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big), at + Vector3.Up * 0.22f * Big));
            }
            else
            {
                if (top ? pick < 0.6f : pick < 0.2f) Add("drum", new Transform3D(turn.Scaled(new Vector3(s, 0.6f + 0.8f * Hash(x, y, 7), s)), at + Vector3.Up * 0.3f));
                else if (pick < 0.45f) Add("column", new Transform3D((turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big), at + Vector3.Up * 0.4f * Big));
                else if (pick < 0.75f) Add("block", new Transform3D(turn.Scaled(Vector3.One * s), at + Vector3.Up * 0.28f * s));
                else
                {
                    // An amphora lying on its side: its belly and its neck.
                    var lie = (turn * new Basis(Vector3.Forward, Mathf.Pi / 2f)).Scaled(Vector3.One * Big);
                    Add("amphora", new Transform3D(lie, at + Vector3.Up * 0.3f * Big));
                    Add("neck", new Transform3D(lie, at + Vector3.Up * 0.3f * Big + lie * new Vector3(0f, 0.6f, 0f)));
                }
            }
        }
        Pieces(g, wreck, Hash, Add);
        return parts;
    }

    /// <summary>
    /// The larger pieces, a few a level on the open seabed under her plane (so she swims over them), far apart: in the
    /// Shipwreck a broken hull section (its keel, its ribs curving up on both sides, a few planks still on them) and a
    /// fallen mast with its yard; in the Ruins a crumbled building wall (a run of cut blocks, broken down unevenly, some
    /// fallen beside it) and a fallen arch (two column stumps and its lintel). Some walls stand on the rock tops too.
    /// </summary>
    static void Pieces(DecorGround g, bool wreck, System.Func<float, float, int, float> hash, System.Action<string, Transform3D> add)
    {
        foreach (var p in PoissonTile.In(PieceSpacing, g.Lo + new System.Numerics.Vector2(3f), g.Hi - new System.Numerics.Vector2(3f)))
        {
            float x = p.X, y = p.Y;
            if (g.Blocked(p, 5f) || g.NearPlace(p, 4f)) continue;
            float h = g.Height(p);
            bool top = h > 0.8f && g.Gradient(p).Length() < 0.6f;
            // (On the floor: under her plane, and the ground round it all floor.)
            bool floor = h < -1.3f;
            for (int i = 0; i < 8 && floor; i++)
            {
                float a = i * Mathf.Tau / 8f;
                floor = g.Height(p + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f) < -0.9f;
            }
            if (!floor && !(top && !wreck)) continue;
            var at = new Vector3(p.X, h, p.Y);
            // (A piece on a shallow floor is laid lower, so it never stands up through her plane.)
            float room = top ? 2f : Mathf.Clamp((OctoShoots.Core.Gen.TopDown.LevelMap.SwimBand - 0.3f - h) / 1.6f, 0.45f, 1f);
            var turn = new Basis(Vector3.Up, hash(x, y, 24) * Mathf.Tau).Scaled(new Vector3(1f, room, 1f));
            Vector3 On(float ax, float ay, float az) => at + turn * new Vector3(ax, ay, az);
            if (wreck && hash(x, y, 25) < 0.6f)
            {
                // A hull section: its keel, ribs up both sides, leaning out, and planks along them.
                float lean = 0.45f;
                add("keel", new Transform3D(turn, On(0f, 0.16f, 0f)));
                for (int k = 0; k < 7; k++)
                {
                    float rx = -2.7f + k * 0.9f;
                    float tall = 1.1f + 0.5f * hash(x + k, y, 26);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var rib = turn * new Basis(Vector3.Right, side * lean);
                        add("rib", new Transform3D(rib.Scaled(new Vector3(1f, tall, 1f)), On(rx, tall * 0.45f, side * (0.35f + tall * 0.2f))));
                    }
                }
                for (int k = 0; k < 3; k++)
                {
                    float side = k % 2 == 0 ? 1f : -1f, len = 2.4f + 2.6f * hash(x, y + k, 27);
                    var plank = turn * new Basis(Vector3.Right, side * lean);
                    add("hullplank", new Transform3D(plank * Basis.FromScale(new Vector3(len, 1f, 1f)), On(-2f + len * 0.5f, 0.35f + 0.3f * k, side * 0.6f)));
                }
            }
            else if (wreck)
            {
                // A fallen mast, its yard still across it near the top, an iron band round it.
                add("mast", new Transform3D(turn * new Basis(Vector3.Forward, Mathf.Pi / 2f), On(0f, 0.24f, 0f)));
                add("yard", new Transform3D(turn * new Basis(Vector3.Right, Mathf.Pi / 2f), On(2.6f, 0.45f, 0f)));
                add("band", new Transform3D(turn * new Basis(Vector3.Forward, Mathf.Pi / 2f), On(-1.2f, 0.24f, 0f)));
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
                        add("wallblock", new Transform3D(turn * new Basis(Vector3.Up, (hash(x + k, y + c, 30) - 0.5f) * 0.12f), On(bx + (c % 2) * 0.5f, 0.28f + c * 0.56f, 0f)));
                    if (hash(x + k, y, 31) < 0.3f)
                        add("fallen", new Transform3D(turn * new Basis(Vector3.Up, hash(x + k, y, 32) * 1.2f), On(bx + 0.3f, 0.28f, 1.2f + hash(x + k, y, 33))));
                }
            }
            else
            {
                // A fallen arch: two column stumps, and its lintel lying across where it fell.
                // (One stump mesh a metre tall, each scaled to its own height.)
                add("stump", new Transform3D(turn * Basis.FromScale(new Vector3(1f, 1.4f, 1f)), On(-1.8f, 0.7f, 0f)));
                add("stump", new Transform3D(turn * Basis.FromScale(new Vector3(1f, 0.8f, 1f)), On(1.8f, 0.4f, 0f)));
                add("lintel", new Transform3D(turn * new Basis(Vector3.Up, 0.35f), On(0.4f, 0.3f, 1.3f)));
            }
        }
    }

    static StandardMaterial3D Mat(Color c, float rough, float metal = 0f) => new() { AlbedoColor = c, Roughness = rough, Metallic = metal };
    static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };
    static CylinderMesh Cyl(float top, float bottom, float h) => new() { TopRadius = top, BottomRadius = bottom, Height = h, RadialSegments = 14, Rings = 1 };
}
