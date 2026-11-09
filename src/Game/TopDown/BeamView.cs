using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her beam (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.2; PlaneWorld.Beam): a fan of her light over her plane along her aim
/// (her_beam.gdshader), stopped where rock stops it, and a spotlight from her bell tilted down along it, lighting the reef
/// it falls on (its shadows keep it off what lies behind the walls). It eases in and out rather than snapping. The shaders
/// of the corrupted sear where it falls (her_beam_at, her_beam_dir) while it burns something.
/// </summary>
public partial class BeamView : Node3D
{
    /// <summary>How bright the fan is, and the spotlight's strength and how far down it tilts.</summary>
    const float FanStrength = 0.32f, SpotEnergy = 3.2f, SpotTilt = 0.35f;
    const int Segments = 24;

    MeshInstance3D _fan = null!;
    ShaderMaterial _material = null!;
    SpotLight3D _spot = null!;
    float _on;

    static readonly StringName BeamAt = "her_beam_at", BeamDir = "her_beam_dir", BeamLit = "her_beam_lit";

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/her_beam.gdshader") };
        _fan = new MeshInstance3D { Mesh = Fan(LightTuning.BeamHalfAngleDeg * Mathf.Pi / 180f), MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(_fan);
        _spot = new SpotLight3D
        {
            SpotAngle = LightTuning.BeamHalfAngleDeg, SpotAngleAttenuation = 0.6f, SpotAttenuation = 1.1f, LightSpecular = 0.5f,
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows, ShadowBias = 0.06f, ShadowNormalBias = 1.2f, Visible = false,
        };
        AddChild(_spot);
    }

    /// <summary>A flat fan along +X, one unit long, its half-angle as the beam's (scaled to the beam's reach as it shines).</summary>
    static ArrayMesh Fan(float half)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < Segments; i++)
        {
            float a0 = -half + 2f * half * i / Segments, a1 = -half + 2f * half * (i + 1) / Segments;
            st.AddVertex(Vector3.Zero);
            st.AddVertex(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)));
            st.AddVertex(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)));
        }
        return st.Commit();
    }

    /// <summary>Follows the sim's beam: from <paramref name="heart"/> (her light's heart, world), along its direction, to where rock stops it.</summary>
    public void Sync(PlaneWorld world, Vector3 heart, float dt)
    {
        var beam = world.Beam;
        _on = Mathf.MoveToward(_on, world.BeamOn ? 1f : 0f, dt * (world.BeamOn ? 10f : 5f));
        bool shown = _on > 0.001f;
        _fan.Visible = _spot.Visible = shown;
        if (!shown)
        {
            RenderingServer.GlobalShaderParameterSet(BeamAt, new Vector4(0f, -1000f, 0f, 0f));
            RenderingServer.GlobalShaderParameterSet(BeamLit, new Vector4(0f, -1000f, 0f, 0f));
            return;
        }
        var dir = new Vector3(beam.Direction.X, 0f, beam.Direction.Y);
        float range = beam.Range;
        float stop = Mathf.Clamp(System.Numerics.Vector2.Distance(beam.Position, world.BeamEnd) / range, 0.02f, 1f);
        var color = OctoShoots.Game.Fx.HerColor.Light;
        float life = Mathf.Clamp(world.Player.Hp / Mathf.Max(world.Run.MaxHp, 1f), 0f, 1f);
        float dim = 0.45f + 0.55f * Mathf.Sqrt(life);

        var origin = new Vector3(beam.Position.X, LevelMap.SwimBand + 0.3f, beam.Position.Y);
        _fan.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(-dir.Z, dir.X)).Scaled(new Vector3(range, 1f, range)), origin);
        _material.SetShaderParameter("tint", color);
        _material.SetShaderParameter("strength", FanStrength * _on * dim);
        _material.SetShaderParameter("half_angle", beam.HalfAngle);
        _material.SetShaderParameter("stop", stop);

        // The spotlight: from her heart, along the beam and a little down onto the reef.
        var look = (dir + Vector3.Down * SpotTilt).Normalized();
        _spot.GlobalTransform = new Transform3D(Basis.LookingAt(look, Vector3.Up), heart);
        _spot.LightColor = color;
        _spot.LightEnergy = SpotEnergy * _on * dim;
        _spot.SpotRange = range * 1.25f;

        // The sear on the corrupted (her_burn.gdshaderinc): only while it burns something, as her shots'.
        RenderingServer.GlobalShaderParameterSet(BeamAt, world.BeamBurns
            ? new Vector4(origin.X, origin.Y + 0.05f, origin.Z, range * stop) : new Vector4(0f, -1000f, 0f, 0f));
        RenderingServer.GlobalShaderParameterSet(BeamDir, new Vector4(dir.X, 0f, dir.Z, beam.HalfAngle));
        // What it shows (her_reveal): the sim's beam while it shines, out to the rock.
        RenderingServer.GlobalShaderParameterSet(BeamLit, world.BeamOn ? new Vector4(origin.X, origin.Y, origin.Z, range * stop) : new Vector4(0f, -1000f, 0f, 0f));
    }
}
