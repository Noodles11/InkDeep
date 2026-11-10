using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her beams (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.2, §5.1; PlaneWorld.HerBeams): each a fan of her light over her plane
/// (her_beam.gdshader), stopped where rock stops it, easing in and out rather than snapping. Her own beam also has a
/// spotlight from her bell tilted down along it, lighting the reef it falls on (its shadows keep it off what lies behind
/// the walls). A split beam's other rays (docs/BEAM-PEARLS-PLAN.md) are fans of their own; Rear Fin's and Lighthouse's
/// are fainter fans. The corrupted show where any of them falls (her_beam_lit: the whole of her beam's fan, every ray of
/// it within it; her_beam2_lit, her_beam3_lit Rear Fin's and Lighthouse's, with their _dir) and sear where her own burns
/// (her_beam_at). Electric Eel Tail's arcs flicker between what her beam burns and what it arcs on to.
/// </summary>
public partial class BeamView : Node3D
{
    /// <summary>How bright her beam's fan is, and the spotlight's strength and how far down it tilts.</summary>
    const float FanStrength = 0.32f, SpotEnergy = 3.2f, SpotTilt = 0.35f;
    const int Segments = 32;

    sealed class Fan
    {
        public MeshInstance3D Node = null!;
        public ShaderMaterial Material = null!;
        public float On;
        public StringName Lit = null!, Dir = null!;
    }

    readonly List<Fan> _fans = new();
    Shader _shader = null!;
    ArrayMesh _fanMesh = null!;
    SpotLight3D _spot = null!;
    MeshInstance3D _arcs = null!;
    ImmediateMesh _arcMesh = null!;
    float _time;

    static readonly StringName BeamAt = "her_beam_at";

    public override void _Ready()
    {
        _shader = GD.Load<Shader>("res://assets/shaders/her_beam.gdshader");
        _fanMesh = FanMesh(LightTuning.MaxHalfAngleDeg * Mathf.Pi / 180f);
        _spot = new SpotLight3D
        {
            SpotAngle = LightTuning.BeamHalfAngleDeg, SpotAngleAttenuation = 0.6f, SpotAttenuation = 1.1f, LightSpecular = 0.5f,
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows, ShadowBias = 0.06f, ShadowNormalBias = 1.2f, Visible = false,
        };
        AddChild(_spot);
        _arcMesh = new ImmediateMesh();
        _arcs = new MeshInstance3D
        {
            Mesh = _arcMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            },
        };
        AddChild(_arcs);
    }

    /// <summary>A flat fan along +X, one unit long, as wide as the widest beam (the shader trims it to the beam's own).</summary>
    static ArrayMesh FanMesh(float half)
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

    /// <summary>A fan for the next of her beams' rays, made as it is first needed.</summary>
    Fan FanAt(int i)
    {
        while (_fans.Count <= i)
        {
            var material = new ShaderMaterial { Shader = _shader };
            var node = new MeshInstance3D { Mesh = _fanMesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            AddChild(node);
            _fans.Add(new Fan { Node = node, Material = material });
        }
        return _fans[i];
    }

    static readonly StringName[] SlotLit = { "her_beam_lit", "her_beam2_lit", "her_beam3_lit" }, SlotDir = { "her_beam_dir", "her_beam2_dir", "her_beam3_dir" };

    /// <summary>Follows the sim's beams: from <paramref name="heart"/> (her light's heart, world) for her own's spotlight.</summary>
    public void Sync(PlaneWorld world, Vector3 heart, float dt)
    {
        _time += dt;
        var color = OctoShoots.Game.Fx.HerColor.Light;
        float life = Mathf.Clamp(world.Player.Hp / Mathf.Max(world.Run.MaxHp, 1f), 0f, 1f);
        float dim = 0.45f + 0.55f * Mathf.Sqrt(life);
        int i = 0;
        // Her beam's fan as a whole, for what it shows: out to its farthest ray's stop, as wide as its rays reach.
        float reach = 0f;
        bool anyOn = false;
        foreach (var (beam, _) in world.HerBeams)
        {
            var fan = FanAt(i);
            bool main = beam == world.Beam;
            int slot = main ? 0 : beam == world.RearBeam ? 1 : beam == world.SweepBeam ? 2 : -1;
            bool ofHers = slot == 0 || slot < 0;
            fan.Lit = slot >= 0 ? SlotLit[slot] : null!;
            fan.Dir = slot >= 0 ? SlotDir[slot] : null!;
            i++;
            bool on = beam.Shining;
            fan.On = Mathf.MoveToward(fan.On, on ? 1f : 0f, dt * (on ? 10f : 5f));
            fan.Node.Visible = fan.On > 0.001f;
            if (main) _spot.Visible = fan.Node.Visible;
            if (!fan.Node.Visible)
            {
                if (slot > 0) RenderingServer.GlobalShaderParameterSet(fan.Lit, new Vector4(0f, -1000f, 0f, 0f));
                if (main) RenderingServer.GlobalShaderParameterSet(BeamAt, new Vector4(0f, -1000f, 0f, 0f));
                continue;
            }
            var dir = new Vector3(beam.Direction.X, 0f, beam.Direction.Y);
            float range = beam.Range;
            // (To rock, or short of the first body across it.)
            var end = world.BeamStop(beam);
            float stop = Mathf.Clamp(System.Numerics.Vector2.Distance(beam.Position, end) / range, 0.02f, 1f);
            var origin = new Vector3(beam.Position.X, LevelMap.SwimBand + 0.3f, beam.Position.Y);
            fan.Node.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(-dir.Z, dir.X)).Scaled(new Vector3(range, 1f, range)), origin);
            fan.Material.SetShaderParameter("tint", TopDownMain.ShowcaseOn ? color.Lerp(new Color(0.9f, 0.96f, 1f), 0.65f) : color);
            fan.Material.SetShaderParameter("strength", FanStrength * fan.On * dim * beam.Intensity / LightTuning.BeamIntensity);
            fan.Material.SetShaderParameter("half_angle", beam.HalfAngle);
            fan.Material.SetShaderParameter("stop", stop);
            if (ofHers && on)
            {
                anyOn = true;
                reach = Mathf.Max(reach, range * stop);
            }
            if (slot > 0)
            {
                RenderingServer.GlobalShaderParameterSet(fan.Dir, new Vector4(dir.X, 0f, dir.Z, beam.HalfAngle));
                // What it shows (her_reveal): the sim's beam while it shines, out to the rock.
                RenderingServer.GlobalShaderParameterSet(fan.Lit, on ? new Vector4(origin.X, origin.Y, origin.Z, range * stop) : new Vector4(0f, -1000f, 0f, 0f));
            }
            if (!main) continue;
            // Her own beam's spotlight: from her heart, along the beam and a little down onto the reef.
            var look = (dir + Vector3.Down * SpotTilt).Normalized();
            _spot.GlobalTransform = new Transform3D(Basis.LookingAt(look, Vector3.Up), heart);
            _spot.LightColor = TopDownMain.ShowcaseOn ? color.Lerp(new Color(0.9f, 0.96f, 1f), 0.65f) : color;
            _spot.LightEnergy = SpotEnergy * fan.On * dim;
            _spot.SpotRange = range * 1.25f;
            _spot.SpotAngle = Mathf.RadToDeg(beam.HalfAngle);
            // The sear on the corrupted (her_burn.gdshaderinc): only while it burns something, as her shots'.
            RenderingServer.GlobalShaderParameterSet(BeamAt, world.BeamBurns
                ? new Vector4(origin.X, origin.Y + 0.05f, origin.Z, range * stop) : new Vector4(0f, -1000f, 0f, 0f));
        }
        for (int k = i; k < _fans.Count; k++) _fans[k].Node.Visible = false;
        // What her beam shows (her_reveal), the whole of its fan: along her aim, as wide as its rays spread.
        var her = new Vector3(world.Beam.Position.X, LevelMap.SwimBand + 0.3f, world.Beam.Position.Y);
        var aim = new Vector3(world.BeamAim.X, 0f, world.BeamAim.Y);
        RenderingServer.GlobalShaderParameterSet(SlotDir[0], new Vector4(aim.X, 0f, aim.Z, Mathf.Max(world.BeamSpread, world.Beam.HalfAngle)));
        RenderingServer.GlobalShaderParameterSet(SlotLit[0], anyOn ? new Vector4(her.X, her.Y, her.Z, reach) : new Vector4(0f, -1000f, 0f, 0f));
        DrawArcs(world, color);
    }

    /// <summary>Electric Eel Tail's arcs: a jagged line of her light, flickering, from each burned creature to the next.</summary>
    void DrawArcs(PlaneWorld world, Color color)
    {
        _arcMesh.ClearSurfaces();
        if (world.Arcs.Count == 0) return;
        _arcMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        float y = LevelMap.SwimBand + 0.35f;
        int flick = (int)(_time * 24f);
        foreach (var (from, to) in world.Arcs)
        {
            var a = new Vector3(from.X, y, from.Y);
            var b = new Vector3(to.X, y, to.Y);
            var across = new Vector3(-(b.Z - a.Z), 0f, b.X - a.X).Normalized();
            const int bits = 7;
            var prev = a;
            for (int k = 1; k <= bits; k++)
            {
                float t = k / (float)bits;
                float jag = k == bits ? 0f : (Hash(flick * 31 + k * 7 + (int)(from.X * 13f)) - 0.5f) * 0.7f;
                var next = a.Lerp(b, t) + across * jag;
                _arcMesh.SurfaceSetColor(new Color(color, 0.9f));
                _arcMesh.SurfaceAddVertex(prev);
                _arcMesh.SurfaceSetColor(new Color(color.Lerp(Colors.White, 0.5f), 0.9f));
                _arcMesh.SurfaceAddVertex(next);
                prev = next;
            }
        }
        _arcMesh.SurfaceEnd();
    }

    static float Hash(int n)
    {
        n = (n << 13) ^ n;
        return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / (float)0x7fffffff;
    }
}
