using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The secrets' cracked seals (docs/DIVE.md §6.1; PlaneCrackSeals.cs): a continuous slab of the reef's rock across a
/// secret's way in, from the floor up past the arch, a jagged vertical crack down its middle glowing faintly from inside
/// (its light breathing slowly). Her dash into the crack crumbles it: the slab breaks into chunks that tumble out and
/// sink into the sand, fading, and the way is open.
/// </summary>
public partial class CrackSealView : Node3D
{
    static readonly Color Stone = new(0.52f, 0.5f, 0.46f), StoneDark = new(0.4f, 0.38f, 0.35f);
    static readonly Color Glow = new(0.75f, 0.92f, 1f);
    /// <summary>How high the slab stands over the floor (m), and how long its chunks take to sink away (s).</summary>
    const float Height = 4.6f, Crumble = 1.6f;
    /// <summary>How high its crack runs up it from the floor (m: to just over her plane, where the walls are cut).</summary>
    const float CrackHeight = 1.5f;

    sealed class Look
    {
        public required Node3D Root;
        public required StandardMaterial3D Crack;
        public float Seed;
    }

    sealed class Chunk
    {
        public required MeshInstance3D Node;
        public Vector3 Velocity, Spin;
        public float Age;
    }

    readonly Dictionary<CrackSeal, Look> _shown = new();
    /// <summary>Her layer's ground material: the slab is drawn as her layer's rock (risen to a wall, cut as the walls are).</summary>
    public ShaderMaterial? Ground { get; set; }
    readonly List<Chunk> _chunks = new();
    StandardMaterial3D? _stone;

    public void Clear()
    {
        foreach (var look in _shown.Values) look.Root.QueueFree();
        _shown.Clear();
        foreach (var c in _chunks) c.Node.QueueFree();
        _chunks.Clear();
    }

    public void Sync(PlaneWorld world, float time, float dt)
    {
        _stone ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var seen = new HashSet<CrackSeal>();
        foreach (var seal in world.CrackSeals)
        {
            seen.Add(seal);
            if (!_shown.TryGetValue(seal, out var look))
            {
                if (seal.Open) continue;
                _shown[seal] = look = Make(seal, world.Map.HeightAt(seal.Mouth.At - System.Numerics.Vector2.Normalize(seal.Mouth.Into) * 0.5f));
            }
            if (seal.Open)
            {
                Break(look);
                _shown.Remove(seal);
                continue;
            }
            // Its crack breathes faintly, as a light behind rock does.
            look.Crack.EmissionEnergyMultiplier = 0.55f + 0.25f * Mathf.Sin(time * 1.3f + look.Seed) + 0.08f * Mathf.Sin(time * 4.1f + look.Seed * 2f);
        }
        var gone = new List<CrackSeal>();
        foreach (var s in _shown.Keys) if (!seen.Contains(s)) gone.Add(s);
        foreach (var s in gone)
        {
            _shown[s].Root.QueueFree();
            _shown.Remove(s);
        }
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            var c = _chunks[i];
            c.Age += dt;
            c.Velocity += Vector3.Down * 6f * dt;
            c.Velocity *= Mathf.Exp(-1.8f * dt);
            c.Node.Position += c.Velocity * dt;
            c.Node.Rotation += c.Spin * dt;
            float k = c.Age / Crumble;
            c.Node.Scale = Vector3.One * Mathf.Max(0.01f, 1f - k * k);
            if (c.Age < Crumble) continue;
            c.Node.QueueFree();
            _chunks.RemoveAt(i);
        }
    }

    Look Make(CrackSeal seal, float floor)
    {
        var rock = seal.Rock;
        var face = rock.Facing;
        var root = new Node3D
        {
            Position = new Vector3(rock.Center.X, floor - 0.3f, rock.Center.Y),
            // (Its +Z out toward the water it is met from.)
            Rotation = new Vector3(0f, Mathf.Atan2(face.X, face.Y), 0f),
        };
        float width = rock.Radius * 2f, thick = rock.Thick * 2f;
        var b = new OctoShoots.Game.Fx.MeshBuilder();
        // The slab, and behind it its passage filled as rock (so while it stands nothing behind it shows, even where the
        // walls are cut away round her): rough stone, a little uneven in its colour.
        Slab(b, width, Height, thick * 0.5f, -(thick * 0.5f + OctoShoots.Core.Gen.TopDown.Doorway.Depth + 0.6f), seal.Mouth.At.X * 0.37f + seal.Mouth.At.Y * 0.71f);
        // Its cap at the cut's height, as the reef's rock has (LevelView.Cap): where the walls are cut away round her, it
        // shows cut, hatched, as solid as they do.
        float capY = LevelView.CapHeight - (floor - 0.3f);
        float zb = -(thick * 0.5f + OctoShoots.Core.Gen.TopDown.Doorway.Depth + 0.6f), xc = width * 0.5f;
        var cap = new Color(0.08f, 0.5f, 0.5f, 0.5f);
        b.Quad(b.Add(new Vector3(-xc, capY, thick * 0.5f), Vector3.Up, cap), b.Add(new Vector3(xc, capY, thick * 0.5f), Vector3.Up, cap),
            b.Add(new Vector3(xc, capY, zb), Vector3.Up, cap), b.Add(new Vector3(-xc, capY, zb), Vector3.Up, cap));
        root.AddChild(new MeshInstance3D { Mesh = b.Build(), MaterialOverride = (Material?)Ground ?? _stone, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // The crack: a jagged line down its middle on its outer face, lit faintly from inside.
        var crack = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Glow,
            EmissionEnabled = true,
            Emission = Glow,
            EmissionEnergyMultiplier = 0.6f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var cb = new OctoShoots.Game.Fx.MeshBuilder();
        var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.Abs(seal.Mouth.At.X * 131f + seal.Mouth.At.Y * 17f) + 1f) };
        float x = 0f, y = 0.05f;
        int n = 6;
        for (int k = 0; k < n; k++)
        {
            float y1 = 0.05f + (CrackHeight - 0.1f) * (k + 1) / n;
            float x1 = Mathf.Clamp(x + rng.RandfRange(-0.14f, 0.14f), -0.18f, 0.18f);
            float w0 = 0.035f + 0.025f * rng.Randf(), w1 = 0.035f + 0.025f * rng.Randf();
            float z = thick * 0.5f + 0.012f;
            var c = Glow;
            cb.Quad(cb.Add(new Vector3(x - w0, y, z), Vector3.Back, c), cb.Add(new Vector3(x + w0, y, z), Vector3.Back, c),
                cb.Add(new Vector3(x1 + w1, y1, z), Vector3.Back, c), cb.Add(new Vector3(x1 - w1, y1, z), Vector3.Back, c));
            x = x1;
            y = y1;
        }
        root.AddChild(new MeshInstance3D { Mesh = cb.Build(), MaterialOverride = crack, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(root);
        return new Look { Root = root, Crack = crack, Seed = rng.Randf() * 6f };
    }

    /// <summary>A box of stone on its foot, w across, h high, from its face at zFront back to zBack, its colour broken by a little noise.</summary>
    static void Slab(OctoShoots.Game.Fx.MeshBuilder b, float w, float h, float zFront, float zBack, float seed)
    {
        // (Alpha 1: it rises with the reef's walls when drawn with her layer's ground.)
        Color Tone(Vector3 p) => StoneDark.Lerp(Stone, 0.5f + 0.5f * Mathf.Sin(p.Y * 2.3f + seed) * Mathf.Cos(p.X * 1.7f + seed * 0.5f)) with { A = 1f };
        void Face(Vector3 n, Vector3 a, Vector3 bb, Vector3 c, Vector3 d) => b.Quad(b.Add(a, n, Tone(a)), b.Add(bb, n, Tone(bb)), b.Add(c, n, Tone(c)), b.Add(d, n, Tone(d)));
        float x = w * 0.5f, z = zFront, zb = zBack;
        Face(Vector3.Back, new(-x, 0, z), new(x, 0, z), new(x, h, z), new(-x, h, z));
        Face(Vector3.Forward, new(x, 0, zb), new(-x, 0, zb), new(-x, h, zb), new(x, h, zb));
        Face(Vector3.Up, new(-x, h, z), new(x, h, z), new(x, h, zb), new(-x, h, zb));
        Face(Vector3.Left, new(-x, 0, zb), new(-x, 0, z), new(-x, h, z), new(-x, h, zb));
        Face(Vector3.Right, new(x, 0, z), new(x, 0, zb), new(x, h, zb), new(x, h, z));
    }

    /// <summary>It crumbles: the slab gone, chunks of it tumbling out toward the water and sinking into the sand.</summary>
    void Break(Look look)
    {
        var root = look.Root;
        var basis = root.GlobalTransform.Basis;
        var rng = new RandomNumberGenerator { Seed = (ulong)(look.Seed * 1000f) + 7 };
        for (int i = 0; i < 16; i++)
        {
            var local = new Vector3(rng.RandfRange(-1.6f, 1.6f), rng.RandfRange(0.2f, Height * 0.8f), rng.RandfRange(-0.2f, 0.2f));
            float size = rng.RandfRange(0.25f, 0.6f);
            var node = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(size, size * rng.RandfRange(0.6f, 1.2f), size * rng.RandfRange(0.5f, 1f)) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = StoneDark.Lerp(Stone, rng.Randf()), Roughness = 1f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = root.GlobalPosition + basis * local,
            };
            AddChild(node);
            _chunks.Add(new Chunk
            {
                Node = node,
                Velocity = basis * new Vector3(rng.RandfRange(-1.2f, 1.2f), rng.RandfRange(0f, 1.5f), rng.RandfRange(0.8f, 3f)),
                Spin = new Vector3(rng.RandfRange(-4f, 4f), rng.RandfRange(-4f, 4f), rng.RandfRange(-4f, 4f)),
            });
        }
        root.QueueFree();
    }
}
