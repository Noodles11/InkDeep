using System;
using System.Collections.Generic;
using Godot;
using OctoShoots.Core;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Small fish schooling through a woven reef's under-reef (docs/REEF-REDO-PROPOSAL.md §3, the look prototype), a
/// metre or several under her plane: each school wanders, turning off the coral that stands across its way; near her
/// it bolts and scatters, then draws together again. Cosmetic only (the sim's reef fish swim at her plane).
/// </summary>
public partial class WovenFish : Node3D
{
    const int Schools = 14;
    const float Bolt = 5.5f;

    sealed class School
    {
        public Vector3 At;
        public float Heading, Speed, Turn, Fright;
        public readonly List<Vector3> Offsets = new();
        public readonly List<Vector3> Fish = new();
        public readonly List<Vector3> Ways = new();
        public float Colour;
        /// <summary>How big its fish are (the mesh is 0.3 m long).</summary>
        public float Scale;
    }

    readonly List<School> _schools = new();
    MultiMesh _mm = null!;
    LevelMap _map = null!;
    Rng _rng = null!;
    public ShaderMaterial Material { get; private set; } = null!;

    public void Build(LevelMap map)
    {
        _map = map;
        _rng = new Rng(map.Seed ^ 0xF15C0UL);
        int total = 0;
        for (int s = 0; s < Schools; s++)
        {
            var school = new School
            {
                At = new Vector3(_rng.Range(20f, map.Size - 20f), -_rng.Range(1.6f, 6f), _rng.Range(20f, map.Size - 20f)),
                Heading = _rng.Range(0f, MathF.Tau),
                Speed = _rng.Range(1.1f, 2f),
                Colour = _rng.NextFloat(),
                Scale = _rng.Range(1.6f, 2.4f),
            };
            int count = 10 + _rng.Int(16);
            for (int i = 0; i < count; i++)
            {
                var o = new Vector3(_rng.Range(-1f, 1f), _rng.Range(-0.35f, 0.35f), _rng.Range(-1f, 1f)) * _rng.Range(0.6f, 2.2f);
                school.Offsets.Add(o);
                school.Fish.Add(school.At + o);
                school.Ways.Add(Vector3.Forward);
            }
            total += count;
            _schools.Add(school);
        }
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/woven_fish.gdshader"), RenderPriority = -49 };
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = FishMesh(), InstanceCount = total };
        AddChild(new MultiMeshInstance3D { Multimesh = _mm, MaterialOverride = Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(new Vector3(-20f, -20f, -20f), new Vector3(map.Size + 40f, 40f, map.Size + 40f)) });
        Tick(Vector3.Zero, 0f);
    }

    /// <summary>Wanders, turns off the coral, bolts from her (her world position), and lays the fish down.</summary>
    public void Tick(Vector3 her, float dt)
    {
        if (_mm is null) return;
        int k = 0;
        foreach (var s in _schools)
        {
            // Its wandering: a slow turn that drifts this way and that.
            s.Turn += (_rng.Range(-1f, 1f) * 1.4f - s.Turn * 0.6f) * dt;
            var ahead = new Vector2(MathF.Cos(s.Heading), MathF.Sin(s.Heading));
            var at = new System.Numerics.Vector2(s.At.X, s.At.Z);
            // Off the coral across its way, and back from the rim.
            float steer = s.Turn;
            for (float look = 1.5f; look <= 4.5f; look += 1.5f)
            {
                var q = at + new System.Numerics.Vector2(ahead.X, ahead.Y) * look;
                bool blocked = _map.Blocked(q) || q.X < 14f || q.Y < 14f || q.X > _map.Size - 14f || q.Y > _map.Size - 14f || _map.HeightAt(q) > s.At.Y - 0.6f;
                if (blocked)
                {
                    steer += 2.6f / look;
                    break;
                }
            }
            // From her: near enough and near her depth, it bolts straight away.
            var away = new Vector2(s.At.X - her.X, s.At.Z - her.Z);
            float d = away.Length();
            if (d < Bolt && her != Vector3.Zero && MathF.Abs(s.At.Y - her.Y) < 6f)
            {
                s.Fright = MathF.Min(1f, s.Fright + dt * 4f);
                float want = MathF.Atan2(away.Y, away.X);
                float diff = Mathf.Wrap(want - s.Heading, -MathF.PI, MathF.PI);
                steer += diff * 3f;
            }
            else s.Fright = MathF.Max(0f, s.Fright - dt * 0.5f);
            s.Heading += steer * dt;
            float speed = s.Speed * (1f + 2.2f * s.Fright);
            s.At += new Vector3(MathF.Cos(s.Heading), 0f, MathF.Sin(s.Heading)) * speed * dt;
            // Keep under her plane, over the floor.
            float floor = _map.HeightAt(new System.Numerics.Vector2(s.At.X, s.At.Z));
            s.At.Y = MathF.Min(MathF.Max(s.At.Y, floor + 0.8f), -1.4f);

            var basis = new Basis(Vector3.Up, -s.Heading + MathF.PI / 2f);
            for (int i = 0; i < s.Fish.Count; i++)
            {
                // Each fish keeps its place in the school, spread wider while it bolts, catching up with a little lag.
                var target = s.At + basis * (s.Offsets[i] * (1f + 1.6f * s.Fright));
                var p = s.Fish[i];
                var step = (target - p) * Mathf.Min(1f, dt * (2.2f + i % 5 * 0.3f));
                s.Fish[i] = p + step;
                var way = step.LengthSquared() > 1e-8f ? step.Normalized() : s.Ways[i];
                s.Ways[i] = s.Ways[i].Lerp(way, Mathf.Min(1f, dt * 6f)).Normalized();
                var fwd = new Vector3(s.Ways[i].X, s.Ways[i].Y * 0.3f, s.Ways[i].Z).Normalized();
                var side = Vector3.Up.Cross(fwd).Normalized();
                var up = fwd.Cross(side);
                _mm.SetInstanceTransform(k, new Transform3D(new Basis(side, up, fwd).Scaled(Vector3.One * s.Scale), s.Fish[i]));
                _mm.SetInstanceCustomData(k, new Color(10f / 16f, s.Colour, s.Fright, (i * 0.137f) % 1f));
                k++;
            }
        }
    }

    /// <summary>A small fish, about 0.3 m long along z (nose forward), flattened side to side, with a forked tail.</summary>
    static ArrayMesh FishMesh()
    {
        var v = new List<Vector3>();
        var c = new List<Color>();
        var idx = new List<int>();
        // Body: a spindle of rings along z.
        const int seg = 8;
        float[] z = { 0.16f, 0.1f, 0.02f, -0.06f, -0.12f };
        float[] r = { 0.0f, 0.045f, 0.06f, 0.04f, 0.012f };
        for (int j = 0; j < z.Length; j++)
            for (int i = 0; i < seg; i++)
            {
                float a = i * MathF.Tau / seg;
                v.Add(new Vector3(MathF.Cos(a) * r[j] * 0.45f, MathF.Sin(a) * r[j], z[j]));
                c.Add(new Color(MathF.Sin(a) > 0f ? 0.55f : 1f, (0.16f - z[j]) / 0.3f, 0.5f + 0.5f * MathF.Sin(a)));
            }
        for (int j = 0; j < z.Length - 1; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * seg + i, b = j * seg + (i + 1) % seg, cc = (j + 1) * seg + (i + 1) % seg, d = (j + 1) * seg + i;
                idx.AddRange(new[] { a, cc, b, a, d, cc });
            }
        // Tail: a forked fin.
        int t = v.Count;
        v.Add(new Vector3(0f, 0f, -0.11f)); c.Add(new Color(0.8f, 0.9f, 0.5f));
        v.Add(new Vector3(0f, 0.07f, -0.19f)); c.Add(new Color(0.8f, 1f, 0.9f));
        v.Add(new Vector3(0f, 0f, -0.15f)); c.Add(new Color(0.8f, 1f, 0.5f));
        v.Add(new Vector3(0f, -0.07f, -0.19f)); c.Add(new Color(0.8f, 1f, 0.1f));
        idx.AddRange(new[] { t, t + 1, t + 2, t, t + 2, t + 3 });
        var n = new Vector3[v.Count];
        for (int k = 0; k < idx.Count; k += 3)
        {
            var f = (v[idx[k + 2]] - v[idx[k]]).Cross(v[idx[k + 1]] - v[idx[k]]);
            n[idx[k]] += f; n[idx[k + 1]] += f; n[idx[k + 2]] += f;
        }
        for (int k = 0; k < n.Length; k++) n[k] = n[k].LengthSquared() > 1e-12f ? n[k].Normalized() : Vector3.Up;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = v.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = n;
        arrays[(int)Mesh.ArrayType.Color] = c.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
