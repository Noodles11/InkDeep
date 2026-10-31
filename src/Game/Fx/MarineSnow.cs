using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The water's plankton, in a box that wraps around a point (what the camera looks at): tiny plain dots with no glow, that
/// give the water parallax so movement reads (THEME-BIBLE §6.9: marine snow between camera and level). They sink slowly and
/// sway, move aside as she passes (<see cref="Her"/>), and are shoved by her dash and her waves (<see cref="Stir"/>).
/// Cosmetic only.
/// </summary>
public partial class MarineSnow : Node3D
{
    readonly int Count;
    readonly Vector3 Half;
    readonly float _size;

    /// <summary>How fast a dot sinks (m/s), and the water's drag on anything pushed, a second.</summary>
    const float Sink = 0.05f, Drag = 2.5f;
    /// <summary>She moves plankton this near her, this far off her plane.</summary>
    const float Wake = 1.7f, WakeHeight = 3f;

    readonly Vector3[] _positions;
    readonly Vector3[] _push;
    readonly float[] _phase;
    readonly float[] _scale;
    readonly Random _random = new(77);

    public MarineSnow() : this(700, new Vector3(9f, 9f, 9f), 0.035f) { }

    /// <param name="half">Half the box's size on each axis.</param>
    /// <param name="size">A dot's size (metres).</param>
    public MarineSnow(int count, Vector3 half, float size)
    {
        Count = count;
        Half = half;
        _size = size;
        _positions = new Vector3[count];
        _push = new Vector3[count];
        _phase = new float[count];
        _scale = new float[count];
    }
    MultiMesh _multimesh = null!;
    float _time;

    float Roll(float a, float b) => a + (b - a) * (float)_random.NextDouble();

    public override void _Ready()
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/marine_snow.gdshader") };
        material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade_far"), Mathf.Max(Half.X, Mathf.Max(Half.Y, Half.Z)) * 6f);
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new QuadMesh { Size = new Vector2(_size, _size), Material = material },
            InstanceCount = Count,
        };
        for (int i = 0; i < Count; i++)
        {
            _positions[i] = new Vector3(Roll(-1f, 1f) * Half.X, Roll(-1f, 1f) * Half.Y, Roll(-1f, 1f) * Half.Z);
            _phase[i] = Roll(0f, 10f);
            _scale[i] = Roll(0.4f, 0.75f);
        }
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-500, -500, -500), new Vector3(1000, 1000, 1000)),
        });
    }

    /// <summary>Where she is now and how she is moving (world, m/s): what is near her moves aside.</summary>
    public void Her(Vector3 at, Vector3 velocity)
    {
        _her = at;
        _herVelocity = velocity;
        _hasHer = true;
    }

    Vector3 _her, _herVelocity;
    bool _hasHer;

    /// <summary>A shove through the water (her dash, her waves): everything within reach of this point is thrown outward, the nearer the harder.</summary>
    public void Stir(Vector3 at, float strength, float reach)
    {
        for (int i = 0; i < Count; i++)
        {
            var off = _positions[i] - at;
            off.Y = 0f;
            float d = off.Length();
            if (d >= reach || d < 1e-3f) continue;
            _push[i] += off / d * strength * (1f - d / reach);
        }
    }

    public void Tick(float dt, Transform3D camera) => Tick(dt, camera.Origin, camera.Basis);

    /// <summary>Drifts the plankton and wraps it around <paramref name="centre"/>, facing the camera.</summary>
    public void Tick(float dt, Vector3 centre, Basis facing)
    {
        _time += dt;
        Vector3 c = centre;
        float drag = Mathf.Exp(-Drag * dt);
        float swim = _herVelocity.Length();
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = _positions[i];
            if (_hasHer)
            {
                var off = p - _her;
                float dy = off.Y;
                off.Y = 0f;
                float d = off.Length();
                if (Mathf.Abs(dy) < WakeHeight && d < Wake && d > 1e-3f) _push[i] += off / d * (1f - d / Wake) * (2f + swim) * 6f * dt;
            }
            _push[i] *= drag;
            p += _push[i] * dt;
            p.Y -= Sink * dt;
            p.X += Mathf.Sin(_time * 0.3f + _phase[i]) * 0.03f * dt;
            p = c + Wrap(p - c);
            _positions[i] = p;
            _multimesh.SetInstanceTransform(i, new Transform3D(facing.Scaled(Vector3.One * _scale[i]), p));
        }
    }

    /// <summary>Moves every dot with the world (the dive moves the new level back to the origin).</summary>
    public void Shift(Vector3 by)
    {
        for (int i = 0; i < Count; i++)
        {
            _positions[i] += by;
            // Drawn there at once: the frame that moves the world must not show them at the old place.
            var t = _multimesh.GetInstanceTransform(i);
            t.Origin += by;
            _multimesh.SetInstanceTransform(i, t);
        }
    }

    Vector3 Wrap(Vector3 d) => new(WrapAxis(d.X, Half.X), WrapAxis(d.Y, Half.Y), WrapAxis(d.Z, Half.Z));

    static float WrapAxis(float v, float half) => Mathf.PosMod(v + half, 2f * half) - half;
}
