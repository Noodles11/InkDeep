using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A woven reef's coral (WovenReef, docs/REEF-REDO-PROPOSAL.md, the look prototype): every piece drawn from a small kit
/// of procedural meshes (three variants of each), instanced by 32 m tile, in the reef's paint (woven_reef.gdshaderinc).
/// Each mesh is built at unit size (its foot at 0, its top at 1, its radius 1) and stretched to the piece.
/// </summary>
public partial class WovenReefView : Node3D
{
    /// <summary>The meshes of the kit, and the kind each is painted as (the shader's code, woven_reef.gdshaderinc).</summary>
    enum Mesh { Pinnacle, Staghorn, Plate, Stalk, Arch, Ring, Head, Fan, Sponge, Soft }
    const int Variants = 3;
    const float Tile = 32f;

    static readonly Dictionary<(Mesh, int), ArrayMesh> Kit = new();
    static Shader? _rigidShader, _softShader;

    public ShaderMaterial Rigid { get; private set; } = null!;
    public ShaderMaterial Soft { get; private set; } = null!;
    public ShaderMaterial Fans { get; private set; } = null!;

    public void Build(LevelMap map)
    {
        _rigidShader ??= GD.Load<Shader>("res://assets/shaders/woven_reef.gdshader");
        _softShader ??= GD.Load<Shader>("res://assets/shaders/woven_reef_soft.gdshader");
        Rigid = new ShaderMaterial { Shader = _rigidShader, RenderPriority = -50 };
        Soft = new ShaderMaterial { Shader = _softShader, RenderPriority = -50 };
        Fans = new ShaderMaterial { Shader = _softShader, RenderPriority = -50 };
        Fans.SetShaderParameter("lattice", 1f);

        var groups = new Dictionary<(Mesh, int, int, int), List<(Transform3D, Color)>>();
        void Add(Mesh mesh, uint seed, Vector3 at, float yaw, Vector3 scale, float rnd)
        {
            int variant = (int)(seed % Variants);
            var basis = new Basis(Vector3.Up, yaw).Scaled(scale);
            var key = (mesh, variant, (int)(at.X / Tile), (int)(at.Z / Tile));
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<(Transform3D, Color)>();
            list.Add((new Transform3D(basis, at), new Color((float)mesh / 16f, rnd, 0f, (seed >> 8 & 255) / 255f)));
        }

        foreach (var p in map.Pieces)
        {
            var foot = new Vector3(p.At.X, p.Base, p.At.Y);
            float h = MathF.Max(p.Top - p.Base, 0.3f);
            float rnd = (p.Seed >> 16 & 255) / 255f;
            switch (p.Kind)
            {
                case PieceKind.Pinnacle:
                    Add(Mesh.Pinnacle, p.Seed, foot, p.Yaw, new Vector3(p.Size, h, p.Size), rnd);
                    break;
                case PieceKind.Staghorn:
                    Add(Mesh.Staghorn, p.Seed, foot, p.Yaw, new Vector3(p.Size, h, p.Size), rnd);
                    break;
                case PieceKind.Table:
                    Add(Mesh.Stalk, p.Seed, foot, p.Yaw, new Vector3(0.45f + 0.06f * p.Size, h, 0.45f + 0.06f * p.Size), rnd);
                    Add(Mesh.Plate, p.Seed, new Vector3(p.At.X, p.Top, p.At.Y), p.Yaw, new Vector3(p.Size, 1f, p.Size), rnd);
                    break;
                case PieceKind.Arch:
                    // Its span runs along the yaw (the mesh along x).
                    Add(Mesh.Arch, p.Seed, foot, -p.Yaw, new Vector3(p.Size, h, 1f), rnd);
                    break;
                case PieceKind.Ring:
                {
                    float r = p.Size, centre = p.Top - r;
                    Add(Mesh.Ring, p.Seed, new Vector3(p.At.X, centre, p.At.Y), -p.Yaw, new Vector3(r, r, r), rnd);
                    // Its foot: a short column from the bank up to the ring's bottom.
                    float bottom = centre - r * 0.9f;
                    if (bottom > p.Base + 0.2f) Add(Mesh.Pinnacle, p.Seed + 1, foot, p.Yaw, new Vector3(r * 0.45f, bottom - p.Base, r * 0.45f), rnd);
                    break;
                }
                case PieceKind.Head:
                    Add(Mesh.Head, p.Seed, foot, p.Yaw, new Vector3(p.Size, h, p.Size), rnd);
                    break;
                case PieceKind.Fan:
                    Add(Mesh.Fan, p.Seed, foot, p.Yaw, new Vector3(p.Size, h, p.Size), rnd);
                    break;
                case PieceKind.Sponge:
                    Add(Mesh.Sponge, p.Seed, foot, p.Yaw, new Vector3(p.Size, h, p.Size), rnd);
                    break;
                default:
                    Add(Mesh.Soft, p.Seed, foot, p.Yaw, new Vector3(p.Size, h, p.Size), rnd);
                    break;
            }
        }

        foreach (var ((mesh, variant, _, _), list) in groups)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = KitMesh(mesh, variant),
                InstanceCount = list.Count,
            };
            for (int i = 0; i < list.Count; i++)
            {
                mm.SetInstanceTransform(i, list[i].Item1);
                mm.SetInstanceCustomData(i, list[i].Item2);
            }
            var material = mesh switch { Mesh.Fan => Fans, Mesh.Soft => Soft, _ => Rigid };
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }

    static ArrayMesh KitMesh(Mesh mesh, int variant)
    {
        if (Kit.TryGetValue((mesh, variant), out var m)) return m;
        var rng = new Rng(0xC0A1UL ^ ((ulong)mesh << 8) ^ (ulong)variant);
        var b = new Builder();
        switch (mesh)
        {
            case Mesh.Pinnacle: Pinnacle(b, rng); break;
            case Mesh.Staghorn: Staghorn(b, rng); break;
            case Mesh.Plate: Plate(b, rng); break;
            case Mesh.Stalk: Stalk(b, rng); break;
            case Mesh.Arch: Arch(b, rng); break;
            case Mesh.Ring: Ring(b, rng); break;
            case Mesh.Head: Head(b, rng); break;
            case Mesh.Fan: Fan(b, rng); break;
            case Mesh.Sponge: Sponge(b, rng); break;
            default: SoftTuft(b, rng); break;
        }
        return Kit[(mesh, variant)] = b.Finish();
    }

    // ───────────────────────── the kit ─────────────────────────

    /// <summary>A coral pinnacle: a lumpy column, wider at its foot, its top rounded, knobs of coral on its flanks.</summary>
    static void Pinnacle(Builder b, Rng rng)
    {
        const int seg = 18, rings = 16;
        float[] ph = { rng.Range(0f, 6.3f), rng.Range(0f, 6.3f), rng.Range(0f, 6.3f) };
        b.Lathe(seg, rings, (t, a) =>
        {
            float r = 1.18f - 0.36f * t;
            if (t > 0.82f) r *= MathF.Sqrt(MathF.Max(1f - MathF.Pow((t - 0.82f) / 0.18f, 2f), 0f));
            r *= 1f + 0.11f * MathF.Sin(3f * a + ph[0] + t * 4f) + 0.07f * MathF.Sin(5f * a + ph[1] - t * 7f) + 0.05f * MathF.Sin(9f * a + ph[2] + t * 13f);
            return r;
        }, t => new Color(0.5f + 0.5f * t, 0f, t));
    }

    /// <summary>Staghorn: branches rising from the foot and forking twice, thinning to their tips.</summary>
    static void Staghorn(Builder b, Rng rng)
    {
        int mains = 5 + rng.Int(3);
        for (int k = 0; k < mains; k++)
        {
            float a = k * MathF.Tau / mains + rng.Range(-0.3f, 0.3f);
            var start = new Vector3(MathF.Cos(a) * rng.Range(0f, 0.25f), 0f, MathF.Sin(a) * rng.Range(0f, 0.25f));
            var dir = new Vector3(MathF.Cos(a) * rng.Range(0.35f, 0.7f), 1f, MathF.Sin(a) * rng.Range(0.35f, 0.7f)).Normalized();
            Branch(b, rng, start, dir, rng.Range(0.55f, 0.75f), 0.075f, 2, 0f);
        }
    }

    static void Branch(Builder b, Rng rng, Vector3 from, Vector3 dir, float length, float radius, int forks, float at)
    {
        var to = from + dir * length;
        float tipR = radius * (forks > 0 ? 0.72f : 0.35f);
        b.Tube(from, to, radius, tipR, 7, at, Mathf.Min(at + length, 1f));
        if (forks <= 0) return;
        int n = 2 + (rng.NextFloat() < 0.3f ? 1 : 0);
        for (int i = 0; i < n; i++)
        {
            var side = new Vector3(rng.Range(-1f, 1f), rng.Range(0.1f, 0.6f), rng.Range(-1f, 1f));
            var d = (dir + side * 0.75f).Normalized();
            if (d.Y < 0.25f) d = (d + Vector3.Up * 0.5f).Normalized();
            Branch(b, rng, to, d, length * rng.Range(0.5f, 0.68f), tipR, forks - 1, at + length);
        }
    }

    /// <summary>A table coral's plate: a broad, slightly domed disc with a wavering lip, its underside in shade.</summary>
    static void Plate(Builder b, Rng rng)
    {
        const int seg = 28, rings = 6;
        float ph = rng.Range(0f, 6.3f), ph2 = rng.Range(0f, 6.3f);
        float Edge(float a) => 1f + 0.08f * MathF.Sin(5f * a + ph) + 0.05f * MathF.Sin(11f * a + ph2);
        // Top (up to 0) and underside (0.3 m under), joined at the lip.
        for (int side = 0; side < 2; side++)
        {
            int start = b.Count;
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * MathF.Tau / seg;
                    float r = t * Edge(a);
                    float y = side == 0 ? -0.08f * t * t : -0.3f + 0.12f * t;
                    b.Vertex(new Vector3(MathF.Cos(a) * r, y, MathF.Sin(a) * r), new Vector2(a, t), side == 0 ? new Color(1f, 0f, 1f) : new Color(0.45f, 0f, 0.8f));
                }
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int v = start + j * (seg + 1) + i;
                if (side == 0) b.Quad(v, v + 1, v + seg + 2, v + seg + 1);
                else b.Quad(v, v + seg + 1, v + seg + 2, v + 1);
            }
        }
    }

    /// <summary>A table's stalk: a tapering column flaring at its foot.</summary>
    static void Stalk(Builder b, Rng rng) =>
        b.Lathe(10, 6, (t, a) => (1f + 0.8f * MathF.Pow(1f - t, 3f)) * (1f + 0.06f * MathF.Sin(4f * a)), t => new Color(0.5f + 0.4f * t, 0f, t), capTop: false);

    /// <summary>An arch: a lumpy tube rising from one foot (x = −1) over a span to the other (x = +1).</summary>
    static void Arch(Builder b, Rng rng)
    {
        const int along = 24, around = 10;
        float ph = rng.Range(0f, 6.3f);
        int start = b.Count;
        for (int j = 0; j <= along; j++)
        {
            float s = j / (float)along;
            float th = MathF.PI * (1f - s);
            var c = new Vector3(MathF.Cos(th), MathF.Pow(MathF.Sin(th), 0.8f), 0f);
            var tangent = new Vector3(MathF.Sin(th), -MathF.Cos(th) * 0.9f, 0f).Normalized();
            var nrm = new Vector3(-tangent.Y, tangent.X, 0f);
            // Thicker at its feet.
            float foot = 1f + 1.2f * MathF.Pow(MathF.Abs(s - 0.5f) * 2f, 4f);
            for (int i = 0; i <= around; i++)
            {
                float a = i * MathF.Tau / around;
                float lump = 1f + 0.15f * MathF.Sin(a * 3f + s * 17f + ph);
                var o = nrm * (MathF.Cos(a) * 0.13f * foot * lump) + new Vector3(0f, 0f, MathF.Sin(a) * 0.55f * foot * lump);
                b.Vertex(c + o, new Vector2(s, i / (float)around), new Color(0.55f + 0.45f * c.Y, 0f, c.Y));
            }
        }
        for (int j = 0; j < along; j++)
        for (int i = 0; i < around; i++)
        {
            int v = start + j * (around + 1) + i;
            b.Quad(v, v + 1, v + around + 2, v + around + 1);
        }
    }

    /// <summary>A standing ring: a lumpy torus upright in the x–y plane, its hole along z.</summary>
    static void Ring(Builder b, Rng rng)
    {
        const int along = 28, around = 10;
        float ph = rng.Range(0f, 6.3f);
        int start = b.Count;
        for (int j = 0; j <= along; j++)
        {
            float th = j * MathF.Tau / along;
            var c = new Vector3(MathF.Cos(th), MathF.Sin(th), 0f);
            float thick = 0.22f * (1f + 0.25f * MathF.Sin(th * 3f + ph));
            for (int i = 0; i <= around; i++)
            {
                float a = i * MathF.Tau / around;
                var o = c * (MathF.Cos(a) * thick) + new Vector3(0f, 0f, MathF.Sin(a) * thick * 1.3f);
                b.Vertex(c + o, new Vector2(j / (float)along, i / (float)around), new Color(0.6f + 0.4f * (c.Y * 0.5f + 0.5f), 0f, c.Y * 0.5f + 0.5f));
            }
        }
        for (int j = 0; j < along; j++)
        for (int i = 0; i < around; i++)
        {
            int v = start + j * (around + 1) + i;
            b.Quad(v, v + around + 1, v + around + 2, v + 1);
        }
    }

    /// <summary>A brain coral or boulder head: a dome, lobed.</summary>
    static void Head(Builder b, Rng rng)
    {
        float p1 = rng.Range(0f, 6.3f), p2 = rng.Range(0f, 6.3f);
        b.Lathe(20, 10, (t, a) =>
        {
            float r = MathF.Sqrt(MathF.Max(1f - t * t, 0f)) * 1.05f + 0.02f;
            return r * (1f + 0.08f * MathF.Sin(4f * a + p1) + 0.05f * MathF.Sin(7f * a + p2 + t * 5f));
        }, t => new Color(0.55f + 0.45f * t, 0f, t));
    }

    /// <summary>A sea fan: a broad, thin fan on a short stalk (its net cut in the shader), free to bend at its rim.</summary>
    static void Fan(Builder b, Rng rng)
    {
        const int cols = 16, rows = 10;
        float spread = rng.Range(0.75f, 1.05f), lean = rng.Range(-0.15f, 0.15f);
        int start = b.Count;
        for (int j = 0; j <= rows; j++)
        {
            float v = j / (float)rows;
            for (int i = 0; i <= cols; i++)
            {
                float u = i / (float)cols;
                float a = (u - 0.5f) * 2f * spread;
                float r = 0.12f + 0.88f * v;
                var p = new Vector3(MathF.Sin(a) * r * 0.95f, MathF.Cos(a) * r * 0.85f + 0.12f * (1f - v), MathF.Sin(a * 2f + v * 3f) * 0.04f + lean * v);
                b.Vertex(p, new Vector2(u, v), new Color(0.7f + 0.3f * v, v * v, v));
            }
        }
        for (int j = 0; j < rows; j++)
        for (int i = 0; i < cols; i++)
        {
            int v = start + j * (cols + 1) + i;
            b.Quad(v, v + 1, v + cols + 2, v + cols + 1);
        }
    }

    /// <summary>Sponge tubes: a few open cylinders, their insides dark.</summary>
    static void Sponge(Builder b, Rng rng)
    {
        int tubes = 3 + rng.Int(3);
        for (int k = 0; k < tubes; k++)
        {
            float a = k * MathF.Tau / tubes + rng.Range(-0.4f, 0.4f);
            var at = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (k == 0 ? 0f : rng.Range(0.35f, 0.7f));
            float r = rng.Range(0.22f, 0.38f), h = rng.Range(0.45f, 1f);
            b.OpenTube(at, r, h);
        }
    }

    /// <summary>A soft-coral tuft: thin blades rising and leaning out, free to bend along their length.</summary>
    static void SoftTuft(Builder b, Rng rng)
    {
        int blades = 9 + rng.Int(5);
        for (int k = 0; k < blades; k++)
        {
            float a = rng.Range(0f, MathF.Tau);
            var from = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * rng.Range(0f, 0.35f);
            var dir = new Vector3(MathF.Cos(a) * rng.Range(0.2f, 0.6f), 1f, MathF.Sin(a) * rng.Range(0.2f, 0.6f)).Normalized();
            float len = rng.Range(0.6f, 1f);
            b.Tube(from, from + dir * len, 0.07f, 0.015f, 5, 0f, len, soft: true);
        }
    }

    // ───────────────────────── a little mesh builder ─────────────────────────

    sealed class Builder
    {
        readonly List<Vector3> _v = new();
        readonly List<Vector2> _uv = new();
        readonly List<Color> _c = new();
        readonly List<int> _i = new();

        public int Count => _v.Count;

        public void Vertex(Vector3 p, Vector2 uv, Color c)
        {
            _v.Add(p);
            _uv.Add(uv);
            _c.Add(c);
        }

        public void Quad(int a, int b, int c, int d)
        {
            _i.Add(a); _i.Add(b); _i.Add(c);
            _i.Add(a); _i.Add(c); _i.Add(d);
        }

        /// <summary>A surface of revolution round y from 0 to 1, its radius by (height, angle); capped on top.</summary>
        public void Lathe(int seg, int rings, Func<float, float, float> radius, Func<float, Color> paint, bool capTop = true)
        {
            int start = Count;
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * MathF.Tau / seg;
                    float r = radius(t, a);
                    Vertex(new Vector3(MathF.Cos(a) * r, t, MathF.Sin(a) * r), new Vector2(i / (float)seg, t), paint(t));
                }
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int v = start + j * (seg + 1) + i;
                Quad(v, v + seg + 1, v + seg + 2, v + 1);
            }
            if (!capTop) return;
            int top = Count;
            Vertex(new Vector3(0f, 1f, 0f), new Vector2(0.5f, 1f), paint(1f));
            int last = start + rings * (seg + 1);
            for (int i = 0; i < seg; i++)
            {
                _i.Add(top); _i.Add(last + i + 1); _i.Add(last + i);
            }
        }

        /// <summary>A tapered tube between two points; b is how high up the piece each end lies (its paint), soft if it bends.</summary>
        public void Tube(Vector3 from, Vector3 to, float r0, float r1, int seg, float h0, float h1, bool soft = false)
        {
            var axis = (to - from).Normalized();
            var side = Mathf.Abs(axis.Y) < 0.95f ? axis.Cross(Vector3.Up).Normalized() : axis.Cross(Vector3.Right).Normalized();
            var up = side.Cross(axis).Normalized();
            int start = Count;
            for (int j = 0; j <= 1; j++)
            {
                var c = j == 0 ? from : to;
                float r = j == 0 ? r0 : r1, h = j == 0 ? h0 : h1;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * MathF.Tau / seg;
                    Vertex(c + (side * MathF.Cos(a) + up * MathF.Sin(a)) * r, new Vector2(i / (float)seg, h), new Color(0.45f + 0.55f * Mathf.Clamp(h, 0f, 1f), soft ? h * h : 0.04f * h, Mathf.Clamp(h, 0f, 1f)));
                }
            }
            for (int i = 0; i < seg; i++) Quad(start + i, start + seg + 1 + i, start + seg + 2 + i, start + i + 1);
            // A rounded tip.
            int tip = Count;
            Vertex(to + axis * r1, new Vector2(0.5f, h1), new Color(1f, soft ? h1 * h1 : 0.04f, Mathf.Clamp(h1, 0f, 1f)));
            for (int i = 0; i < seg; i++)
            {
                _i.Add(start + seg + 2 + i); _i.Add(start + seg + 1 + i); _i.Add(tip);
            }
        }

        /// <summary>An open cylinder (wall with an inside), its rim at height h.</summary>
        public void OpenTube(Vector3 at, float r, float h)
        {
            const int seg = 10;
            int outer = Count;
            for (int j = 0; j <= 1; j++)
            for (int i = 0; i <= seg; i++)
            {
                float a = i * MathF.Tau / seg;
                float rr = r * (j == 0 ? 1.1f : 1f);
                Vertex(at + new Vector3(MathF.Cos(a) * rr, j * h, MathF.Sin(a) * rr), new Vector2(i / (float)seg, j), new Color(0.55f + 0.45f * j, 0.06f * j, j * h));
            }
            for (int i = 0; i < seg; i++) Quad(outer + i, outer + seg + 1 + i, outer + seg + 2 + i, outer + i + 1);
            int inner = Count;
            for (int j = 0; j <= 1; j++)
            for (int i = 0; i <= seg; i++)
            {
                float a = i * MathF.Tau / seg;
                Vertex(at + new Vector3(MathF.Cos(a) * r * 0.78f, j == 0 ? h * 0.2f : h, MathF.Sin(a) * r * 0.78f), new Vector2(i / (float)seg, j), new Color(0.15f + 0.3f * j, 0.06f * j, j * h));
            }
            for (int i = 0; i < seg; i++) Quad(inner + i, inner + i + 1, inner + seg + 2 + i, inner + seg + 1 + i);
            // The lip between them.
            for (int i = 0; i < seg; i++) Quad(outer + seg + 1 + i, inner + seg + 1 + i, inner + seg + 2 + i, outer + seg + 2 + i);
        }

        public ArrayMesh Finish()
        {
            // Smooth normals from the faces round each vertex.
            var n = new Vector3[_v.Count];
            for (int k = 0; k < _i.Count; k += 3)
            {
                int a = _i[k], b = _i[k + 1], c = _i[k + 2];
                var f = (_v[b] - _v[a]).Cross(_v[c] - _v[a]);
                n[a] += f;
                n[b] += f;
                n[c] += f;
            }
            for (int k = 0; k < n.Length; k++) n[k] = n[k].LengthSquared() > 1e-12f ? n[k].Normalized() : Vector3.Up;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Godot.Mesh.ArrayType.Max);
            arrays[(int)Godot.Mesh.ArrayType.Vertex] = _v.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Normal] = n;
            arrays[(int)Godot.Mesh.ArrayType.TexUV] = _uv.ToArray();
            arrays[(int)Godot.Mesh.ArrayType.Color] = _c.ToArray();
            // Built counter-clockwise round each face's outward side; Godot's front faces wind clockwise.
            var index = _i.ToArray();
            for (int k = 0; k < index.Length; k += 3) (index[k + 1], index[k + 2]) = (index[k + 2], index[k + 1]);
            arrays[(int)Godot.Mesh.ArrayType.Index] = index;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }
}
