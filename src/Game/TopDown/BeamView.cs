using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her beams (docs/LIGHT-AND-GRAPH-PROPOSAL.md §2.2, §5.1; PlaneWorld.HerBeams): each a fan of her light over her plane
/// easing in and out rather than snapping. Lines of her light out from her bell, about one a degree, are each cast on their
/// own (PlaneWorld.LineReach) and stopped by what they meet, rock or a creature's body; they are not drawn, but shape it.
/// It is a flashlight's beam (her_beam_volume.gdshader): a round cone, up and down from her plane as well as either side,
/// each way out to where its line is stopped; a faint haze with plankton motes drifting in it, its far end a disc of
/// light as bright where it lands on something as anywhere, soft at every edge (it fades out before the rock it goes
/// into). (Its mesh is laid out afresh each frame from the lines' ends; the lines' own glow along what stops them,
/// her_beam.gdshader, is not drawn.) Her own beam also has a
/// spotlight from her bell tilted down along it, lighting the reef it falls on (its shadows keep it off what lies behind
/// the walls). A split beam's other rays (docs/BEAM-PEARLS-PLAN.md) are fans of their own; Rear Fin's and Lighthouse's
/// are fainter fans. The corrupted show where any of them falls (her_beam_lit: the whole of her beam's fan, every ray of
/// it within it; her_beam2_lit, her_beam3_lit Rear Fin's and Lighthouse's, with their _dir) and sear where her own burns
/// (her_beam_at). Electric Eel Tail's arcs flicker between what her beam burns and what it arcs on to. Where a line goes
/// into a prism stone (PlanePrismStones.cs) her light lands on it, and out of its far side goes on in the stone's colour
/// (mixed as light mixes through more than one): a band of that colour laid between the lines that carry it, to where
/// they end, and a light of that colour where her own beam's axis lands.
/// </summary>
public partial class BeamView : Node3D
{
    /// <summary>How bright her beam's fan is, and the spotlight's strength and how far down it tilts.</summary>
    const float FanStrength = 0.32f, SpotEnergy = 3.2f, SpotTilt = 0.35f;

    sealed class Fan
    {
        public MeshInstance3D Node = null!;
        public ShaderMaterial Material = null!;
        public ImmediateMesh Mesh = null!;
        /// <summary>The flashlight's cone (round, up and down from her plane), its material and mesh.</summary>
        public MeshInstance3D Volume = null!;
        public ShaderMaterial VolumeMaterial = null!;
        public ImmediateMesh VolumeMesh = null!;
        /// <summary>Its lines this frame: each one's reach (a share of the fan's), and whether something stopped it.</summary>
        public float[] Reach = new float[MostLines + 1];
        public bool[] Stopped = new bool[MostLines + 1];
        public int Lines;
        public float On;
        public StringName Lit = null!, Dir = null!;
    }

    /// <summary>How many degrees of a fan each line of it covers, and the fewest and most lines a fan has.</summary>
    const float LineDegrees = 1f;
    const int LeastLines = 6, MostLines = 72;

    readonly List<Fan> _fans = new();
    Shader _shader = null!, _volumeShader = null!;
    /// <summary>The cone's rings round its axis and along it.</summary>
    const int Around = 28, Along = 14;
    /// <summary>How far up and down from her plane the cone reaches, as a share of how far it reaches either side.</summary>
    const float VerticalShare = 1f;
    SpotLight3D _spot = null!;
    MeshInstance3D _arcs = null!;
    ImmediateMesh _arcMesh = null!;
    /// <summary>Her light come out of prism stones in their colours, laid out afresh each frame (world), and a light where her beam's coloured axis lands.</summary>
    MeshInstance3D _tint = null!;
    ImmediateMesh _tintMesh = null!;
    bool _tintBegun;
    OmniLight3D _tintLight = null!;
    readonly List<(float In, float Out, LightHue Hue, float Across)> _hues = new(), _prevHues = new();
    /// <summary>How strong the coloured light is near the stone it comes out of, and at the end of its way.</summary>
    const float TintNear = 0.3f, TintFar = 0.1f;
    float _time;

    static readonly StringName BeamAt = "her_beam_at";

    public override void _Ready()
    {
        _shader = GD.Load<Shader>("res://assets/shaders/her_beam.gdshader");
        _volumeShader = GD.Load<Shader>("res://assets/shaders/her_beam_volume.gdshader");
        _spot = new SpotLight3D
        {
            SpotAngle = LightTuning.BeamHalfAngleDeg, SpotAngleAttenuation = 0.35f, ShadowBlur = 3f, SpotAttenuation = 1.1f, LightSpecular = 0.5f,
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
        _tintMesh = new ImmediateMesh();
        _tint = new MeshInstance3D
        {
            Mesh = _tintMesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 40f,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, NoDepthTest = false,
            },
        };
        AddChild(_tint);
        _tintLight = new OmniLight3D { OmniRange = 3.5f, OmniAttenuation = 1.2f, ShadowEnabled = false, Visible = false, LightSpecular = 0.4f };
        AddChild(_tintLight);
    }

    /// <summary>
    /// A fan's lines cast and laid out (in its own frame: along +X, one unit its full reach): a triangle from her to each
    /// two lines' ends. UV.x: how far along its own line (1 at its end); UV.y: 1 where something stopped the line (its
    /// end glows there). Returns how far its longest line gets (metres).
    /// </summary>
    float LayFan(PlaneWorld world, Fan fan, LightEmitter beam, float strength)
    {
        float half = beam.HalfAngle, range = Mathf.Max(beam.Range, 0.1f);
        int n = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.RadToDeg(half) / LineDegrees), LeastLines, MostLines);
        var mesh = fan.Mesh;
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        _prevHues.Clear();
        float longest = 0f;
        Vector3 prev = default;
        float prevHit = 0f;
        for (int k = 0; k <= n; k++)
        {
            float a = -half + 2f * half * k / n;
            var way = new System.Numerics.Vector2(
                beam.Direction.X * Mathf.Cos(a) - beam.Direction.Y * Mathf.Sin(a),
                beam.Direction.X * Mathf.Sin(a) + beam.Direction.Y * Mathf.Cos(a));
            float len = world.LineReach(beam.Position, way, range, out bool hit);
            longest = Mathf.Max(longest, len);
            // Into a prism stone her own light lands on it; out of it, it goes on in its colour.
            world.HuesAlong(beam.Position, way, len, _hues);
            if (_hues.Count > 0) TintBetween(beam.Position, way, len, k > 0 ? _prevWay : way, strength);
            _prevHues.Clear();
            _prevHues.AddRange(_hues);
            _prevWay = way;
            _prevLen = len;
            if (_hues.Count > 0)
            {
                len = _hues[0].In;
                hit = true;
            }
            float f = len / range;
            fan.Reach[k] = f;
            fan.Stopped[k] = hit;
            var end = new Vector3(Mathf.Cos(a) * f, 0f, Mathf.Sin(a) * f);
            float h = hit ? 1f : 0f;
            if (k > 0)
            {
                mesh.SurfaceSetUV(new Vector2(0f, (h + prevHit) * 0.5f));
                mesh.SurfaceAddVertex(Vector3.Zero);
                mesh.SurfaceSetUV(new Vector2(1f, prevHit));
                mesh.SurfaceAddVertex(prev);
                mesh.SurfaceSetUV(new Vector2(1f, h));
                mesh.SurfaceAddVertex(end);
            }
            prev = end;
            prevHit = h;
        }
        mesh.SurfaceEnd();
        fan.Lines = n;
        LayVolume(fan, half);
        return longest;
    }

    System.Numerics.Vector2 _prevWay;
    float _prevLen;

    /// <summary>
    /// The coloured light between this line and the one before it: each stretch of this line out of a stone, to the next
    /// stone or its end, joined to the stretch of the same colour on the line before (if it has one).
    /// </summary>
    void TintBetween(System.Numerics.Vector2 from, System.Numerics.Vector2 way, float len, System.Numerics.Vector2 prevWay, float strength)
    {
        if (_prevHues.Count == 0 || strength <= 0.001f) return;
        float y = LevelMap.SwimBand + 0.3f;
        Vector3 P(System.Numerics.Vector2 w, float t) => new(from.X + w.X * t, y, from.Y + w.Y * t);
        for (int i = 0; i < _hues.Count; i++)
        {
            var hue = _hues[i].Hue;
            float a0 = _hues[i].Out, a1 = i + 1 < _hues.Count ? _hues[i + 1].In : len;
            int j = _prevHues.FindIndex(h => h.Hue == hue);
            if (j < 0 || a1 <= a0) continue;
            float b0 = _prevHues[j].Out, b1 = j + 1 < _prevHues.Count ? _prevHues[j + 1].In : _prevLen;
            if (b1 <= b0) continue;
            if (!_tintBegun)
            {
                _tintMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
                _tintBegun = true;
            }
            var c = OctoShoots.Game.Fx.HueColor.Of(hue);
            // Bright as it comes out of the stone, fainter along its way, and soft at its sides (where it went through
            // the stone's rim).
            float edgeA = 1f - Mathf.SmoothStep(0.45f, 1f, _hues[i].Across), edgeB = 1f - Mathf.SmoothStep(0.45f, 1f, _prevHues[j].Across);
            Color nearA = new(c, TintNear * strength * edgeA), farA = new(c, TintFar * strength * edgeA);
            Color nearB = new(c, TintNear * strength * edgeB), farB = new(c, TintFar * strength * edgeB);
            void V(Vector3 p, Color col) { _tintMesh.SurfaceSetColor(col); _tintMesh.SurfaceAddVertex(p); }
            V(P(way, a0), nearA); V(P(prevWay, b0), nearB); V(P(prevWay, b1), farB);
            V(P(way, a0), nearA); V(P(prevWay, b1), farB); V(P(way, a1), farA);
        }
    }

    /// <summary>A line's reach (a share of the fan's) at an angle off its axis, between the two cast either side of it.</summary>
    static float ReachAt(Fan fan, float half, float a, out bool stopped)
    {
        float x = Mathf.Clamp((a + half) / Mathf.Max(2f * half, 1e-4f), 0f, 1f) * fan.Lines;
        int k = Mathf.Min((int)x, fan.Lines - 1);
        float t = x - k;
        stopped = t < 0.5f ? fan.Stopped[k] : fan.Stopped[k + 1];
        // (The shorter of the two at an edge, so the light never runs on past the edge of what stopped it.)
        return fan.Stopped[k] != fan.Stopped[k + 1] ? Mathf.Min(fan.Reach[k], fan.Reach[k + 1]) : Mathf.Lerp(fan.Reach[k], fan.Reach[k + 1], t);
    }

    /// <summary>
    /// The flashlight's cone, in the fan's own frame (along +X, one unit its full reach, Y up): a round cone of its
    /// half-angle, each way round its axis out to as far as the line of light at that side gets (a light going up or
    /// down meets what the line at its side meets), open at its far end, which glows where it was stopped.
    /// </summary>
    void LayVolume(Fan fan, float half)
    {
        var mesh = fan.VolumeMesh;
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        float tan = Mathf.Tan(half);
        var endHit = new float[Around + 1];
        var reach = new float[Around + 1];
        for (int m = 0; m <= Around; m++)
        {
            float phi = m * Mathf.Tau / Around;
            // The side its way leans to, as an angle off the axis across her plane.
            float a = Mathf.Atan(tan * Mathf.Cos(phi));
            float r = ReachAt(fan, half, a, out bool stopped);
            reach[m] = r * Mathf.Cos(a);
            endHit[m] = stopped ? 1f : 0f;
        }
        // (As far up and down as either side, VerticalShare of it: a round cone.)
        Vector3 Point(float s, float phi, float t) => new(s, s * t * Mathf.Sin(phi) * VerticalShare, s * t * Mathf.Cos(phi));
        Vector3 Normal(float phi) => new Vector3(-tan, Mathf.Sin(phi) / VerticalShare, Mathf.Cos(phi)).Normalized();
        // Its sides: each way round out to its own end, in rings. UV.y: how far along its own way (1 at its end);
        // COLOR.g: 1 where that way was stopped (its end glows there, however far it is).
        for (int m = 0; m < Around; m++)
        {
            float p0 = m * Mathf.Tau / Around, p1 = (m + 1) * Mathf.Tau / Around;
            for (int j = 0; j < Along; j++)
            {
                float u0 = j / (float)Along, u1 = (j + 1) / (float)Along;
                void V(float u, int at, float phi)
                {
                    var v = Point(reach[at] * u, phi, tan);
                    mesh.SurfaceSetColor(new Color(0f, endHit[at], 0f, 1f));
                    mesh.SurfaceSetUV(new Vector2(v.X, u));
                    mesh.SurfaceSetNormal(Normal(phi));
                    mesh.SurfaceAddVertex(v);
                }
                V(u0, m, p0); V(u1, m, p0); V(u1, m + 1, p1);
                V(u0, m, p0); V(u1, m + 1, p1); V(u0, m + 1, p1);
            }
        }
        mesh.SurfaceEnd();
    }

    /// <summary>A fan for the next of her beams' rays, made as it is first needed.</summary>
    Fan FanAt(int i)
    {
        while (_fans.Count <= i)
        {
            var material = new ShaderMaterial { Shader = _shader };
            var mesh = new ImmediateMesh();
            // (Its lines reach out round her: never culled while she is on screen.)
            var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false, ExtraCullMargin = 40f };
            AddChild(node);
            var volumeMaterial = new ShaderMaterial { Shader = _volumeShader };
            var volumeMesh = new ImmediateMesh();
            var volume = new MeshInstance3D { Mesh = volumeMesh, MaterialOverride = volumeMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false, ExtraCullMargin = 40f };
            AddChild(volume);
            _fans.Add(new Fan { Node = node, Material = material, Mesh = mesh, Volume = volume, VolumeMaterial = volumeMaterial, VolumeMesh = volumeMesh });
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
        _tintMesh.ClearSurfaces();
        _tintBegun = false;
        _tintLight.Visible = false;
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
            fan.Volume.Visible = fan.On > 0.001f;
            // (Its lines shape the cone; their own glow along what stops them is not drawn.)
            fan.Node.Visible = false;
            if (main) _spot.Visible = fan.Volume.Visible;
            if (!fan.Volume.Visible)
            {
                if (slot > 0) RenderingServer.GlobalShaderParameterSet(fan.Lit, new Vector4(0f, -1000f, 0f, 0f));
                if (main) RenderingServer.GlobalShaderParameterSet(BeamAt, new Vector4(0f, -1000f, 0f, 0f));
                continue;
            }
            var dir = new Vector3(beam.Direction.X, 0f, beam.Direction.Y);
            float range = beam.Range;
            // Its lines, each to what stops it.
            float longest = LayFan(world, fan, beam, fan.On * dim * beam.Intensity / LightTuning.BeamIntensity);
            float stop = Mathf.Clamp(longest / Mathf.Max(range, 0.1f), 0.02f, 1f);
            var origin = new Vector3(beam.Position.X, LevelMap.SwimBand + 0.3f, beam.Position.Y);
            fan.Node.GlobalTransform = fan.Volume.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(-dir.Z, dir.X)).Scaled(new Vector3(range, range, range)), origin);
            fan.VolumeMaterial.SetShaderParameter("tint", TopDownMain.ShowcaseOn ? color.Lerp(new Color(0.9f, 0.96f, 1f), 0.65f) : color);
            fan.VolumeMaterial.SetShaderParameter("strength", fan.On * dim * beam.Intensity / LightTuning.BeamIntensity);
            fan.Material.SetShaderParameter("tint", TopDownMain.ShowcaseOn ? color.Lerp(new Color(0.9f, 0.96f, 1f), 0.65f) : color);
            fan.Material.SetShaderParameter("strength", FanStrength * fan.On * dim * beam.Intensity / LightTuning.BeamIntensity);
            fan.Material.SetShaderParameter("half_angle", beam.HalfAngle);
            fan.Material.SetShaderParameter("reach", range);
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
            // Where her beam's axis lands, in the colour the stones on its way give it.
            var hue = world.HueAlong(beam.Position, world.BeamEnd);
            if (hue != LightHue.None)
            {
                _tintLight.Visible = true;
                _tintLight.LightColor = OctoShoots.Game.Fx.HueColor.Of(hue);
                _tintLight.LightEnergy = 2.5f * fan.On * dim;
                var back = world.BeamEnd - beam.Direction * 0.6f;
                _tintLight.GlobalPosition = new Vector3(back.X, LevelMap.SwimBand + 0.4f, back.Y);
            }
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
        for (int k = i; k < _fans.Count; k++) _fans[k].Node.Visible = _fans[k].Volume.Visible = false;
        if (_tintBegun) _tintMesh.SurfaceEnd();
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
