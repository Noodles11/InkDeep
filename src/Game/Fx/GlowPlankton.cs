using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// A cave's fluorescent plankton (docs/ADVENTURE.md §8): a few tiny specks of living light, cyan, green, blue and now and
/// then a pink one, in a box that wraps around a point (what the camera looks at). Each wanders on its own slow, uneven
/// way, and glows up and fades again on its own clock, so the dark is never quite still. They are the only light in a
/// cave that is not hers. Cosmetic only.
/// </summary>
public partial class GlowPlankton : Node3D
{
    readonly int Count;
    readonly Vector3 Half;

    readonly Vector3[] _positions;
    readonly Vector3[] _drift;
    readonly float[] _phase, _rate, _size;
    readonly Color[] _colour;
    readonly Random _random = new(91);
    MultiMesh _multimesh = null!;
    float _time;

    static readonly Color[] Palette =
    {
        new(0.35f, 1f, 0.92f), new(0.45f, 1f, 0.55f), new(0.4f, 0.65f, 1f), new(0.55f, 0.95f, 1f), new(1f, 0.45f, 0.85f),
    };

    public GlowPlankton() : this(120, new Vector3(22f, 6f, 17f)) { }

    /// <param name="half">Half the box's size on each axis.</param>
    public GlowPlankton(int count, Vector3 half)
    {
        Count = count;
        Half = half;
        _positions = new Vector3[count];
        _drift = new Vector3[count];
        _phase = new float[count];
        _rate = new float[count];
        _size = new float[count];
        _colour = new Color[count];
    }

    float Roll(float a, float b) => a + (b - a) * (float)_random.NextDouble();

    public override void _Ready()
    {
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/glow_plankton.gdshader") } },
            InstanceCount = Count,
        };
        for (int i = 0; i < Count; i++)
        {
            _positions[i] = new Vector3(Roll(-1f, 1f) * Half.X, Roll(-1f, 1f) * Half.Y, Roll(-1f, 1f) * Half.Z);
            _drift[i] = new Vector3(Roll(-1f, 1f), Roll(-0.4f, 0.4f), Roll(-1f, 1f)) * 0.12f;
            _phase[i] = Roll(0f, Mathf.Tau);
            _rate[i] = Roll(0.25f, 0.9f);
            _size[i] = Roll(0.09f, 0.17f);
            // Mostly the cool greens and blues; a pink one is rare.
            _colour[i] = Palette[_random.NextDouble() < 0.06 ? 4 : _random.Next(4)];
        }
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-500, -500, -500), new Vector3(1000, 1000, 1000)),
        });
    }

    /// <summary>Drifts the specks and wraps them around <paramref name="centre"/>, facing the camera.</summary>
    public void Tick(float dt, Vector3 centre, Basis facing)
    {
        if (!Visible) return;
        _time += dt;
        for (int i = 0; i < Count; i++)
        {
            // A random walk: its way turns a little at a time, never quite still, never going anywhere fast.
            var d = _drift[i] + new Vector3(Roll(-1f, 1f), Roll(-0.5f, 0.5f), Roll(-1f, 1f)) * 0.25f * dt;
            float speed = d.Length();
            if (speed > 0.18f) d *= 0.18f / speed;
            _drift[i] = d;
            var p = _positions[i] + d * dt;
            p = centre + Wrap(p - centre);
            _positions[i] = p;
            // It glows up and fades on its own clock, now and then going almost dark.
            float glow = 0.5f + 0.5f * Mathf.Sin(_time * _rate[i] + _phase[i]);
            glow = 0.12f + 0.88f * glow * glow * glow;
            _multimesh.SetInstanceTransform(i, new Transform3D(facing.Scaled(Vector3.One * _size[i]), p));
            _multimesh.SetInstanceColor(i, _colour[i] with { A = glow });
        }
    }

    /// <summary>Moves every speck with the world (the dive moves the new level back to the origin).</summary>
    public void Shift(Vector3 by)
    {
        for (int i = 0; i < Count; i++) _positions[i] += by;
    }

    Vector3 Wrap(Vector3 d) => new(WrapAxis(d.X, Half.X), WrapAxis(d.Y, Half.Y), WrapAxis(d.Z, Half.Z));

    static float WrapAxis(float v, float half) => Mathf.PosMod(v + half, 2f * half) - half;
}
