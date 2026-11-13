using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her beams (docs/LIGHT-AND-GRAPH.md §2.2, §5.1; docs/VISUAL-SHOWCASE.md; PlaneWorld.HerBeams), each easing in and
/// out rather than snapping. Each is a flashlight's beam in the water, lit as light is: a grid of rays over its round
/// cone is cast in three dimensions each frame (BeamShadow), each stopped by the seabed, rock, or a creature's round
/// body, and the water glows only where the light gets to (her_beam_volume.gdshader, marched along each pixel's line of
/// sight): every creature casts its own round shadow down the beam, as wide as its body. Lines of her light across her
/// plane (PlaneWorld.LineReach, about one a degree) are cast as well, for how far it reaches and for prism stones'
/// colours. Her own beam also has a spotlight from her bell level along it, lighting the reef it falls on (its
/// shadows keep it off what lies behind the walls and behind the creatures), and its shadow grid goes to the reef's own
/// light (her_beam_shadow; reef_show's rs_beam). A split beam's other rays (docs/proposals/BEAM-PEARLS-PLAN.md) are cones of their
/// own; Rear Fin's and Lighthouse's are fainter. The corrupted show where any of them falls (her_beam_lit: the whole of
/// her beam's fan, every ray of it within it; her_beam2_lit, her_beam3_lit Rear Fin's and Lighthouse's, with their _dir)
/// and sear where her own burns (her_beam_at). Electric Eel Tail's arcs flicker between what her beam burns and what it
/// arcs on to. Where a line goes into a prism stone (PlanePrismStones.cs) her light lands on it, and out of its far side
/// goes on in the stone's colour (mixed as light mixes through more than one): a band of that colour laid between the
/// lines that carry it, to where they end, and a light of that colour where her own beam's axis lands.
/// </summary>
public partial class BeamView : Node3D
{
    /// <summary>The spotlight's strength and how far down it tilts.</summary>
    const float SpotEnergy = 3.2f, SpotTilt = 0f;
    /// <summary>How long a beam takes to shoot out of her bell to its full reach as it comes on (s).</summary>
    const float ExtendTime = 0.14f;

    sealed class Fan
    {
        /// <summary>The flashlight's cone: its hull (marched through by its material), and where its light gets to.</summary>
        public MeshInstance3D Volume = null!;
        public ShaderMaterial VolumeMaterial = null!;
        public ImmediateMesh VolumeMesh = null!;
        public BeamShadow Shadow = null!;
        /// <summary>The half-angle its hull was laid out for.</summary>
        public float HullHalf = -1f;
        public float On;
        /// <summary>How far out of her bell it has shot since it came on (0..1, eased out: Extend).</summary>
        public float Out;
        public StringName Lit = null!, Dir = null!;
    }

    /// <summary>How many degrees of a fan each line of it covers, and the fewest and most lines a fan has.</summary>
    const float LineDegrees = 1f;
    const int LeastLines = 6, MostLines = 72;

    readonly List<Fan> _fans = new();
    Shader _volumeShader = null!;
    /// <summary>The hull's segments round its axis.</summary>
    const int Around = 32;
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
    /// <summary>How much thicker a coloured ray's haze is drawn (docs/LIGHT-AND-GRAPH.md §10).</summary>
    const float ColouredHaze = 1.7f;
    float _time;
    /// <summary>
    /// The rays born where her light lands on rock (PlanePaths.cs) are drawn as soft cones without the shadow grid (a
    /// white one: lit all through, to the rock their axis meets), and each has a glint of its light where it was born.
    /// </summary>
    ImageTexture _unshadowed = null!;
    readonly List<OmniLight3D> _glints = new();
    /// <summary>How much her rhythms brighten her own beam's haze: at full focus, spun right up, on a pulse's beat; and how white focus turns it.</summary>
    const float FocusGlow = 0.9f, SpinGlow = 0.6f, BeatGlow = 1.2f, FocusWhite = 0.7f;
    /// <summary>Shark Mode's Frenzy: her beam flushed bright and hot-white; Coral Reef's pools a living coral.</summary>
    const float FrenzyGlow = 0.8f, FrenzyWhite = 0.35f;
    static readonly Color ReefPool = new(1f, 0.5f, 0.45f);
    const float GlintEnergy = 5f, GlintRange = 2.4f, BornHaze = 2f, BornLeast = 0.55f;

    static readonly StringName BeamAt = "her_beam_at", BeamShadowTex = "her_beam_shadow", BeamFrame = "her_beam_frame", BeamAxis = "her_beam_axis";

    public override void _Ready()
    {
        _volumeShader = GD.Load<Shader>("res://assets/shaders/her_beam_volume.gdshader");
        _spot = new SpotLight3D
        {
            SpotAngle = LightTuning.BeamHalfAngleDeg, SpotAngleAttenuation = 0.35f, ShadowBlur = 3f, SpotAttenuation = 1.1f, LightSpecular = 0.5f,
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows, ShadowBias = 0.06f, ShadowNormalBias = 1.2f, Visible = false,
        };
        AddChild(_spot);
        ReadyMotes();
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
        var white = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        white.Fill(Colors.White);
        _unshadowed = ImageTexture.CreateFromImage(white);
    }

    /// <summary>
    /// A beam's lines across her plane cast (and the coloured light out of the prism stones they go through laid out).
    /// Returns how far its longest line gets (metres).
    /// </summary>
    float LayFan(PlaneWorld world, LightEmitter beam, float strength)
    {
        float half = beam.HalfAngle, range = Mathf.Max(beam.Range, 0.1f);
        int n = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.RadToDeg(half) / LineDegrees), LeastLines, MostLines);
        _prevHues.Clear();
        float longest = 0f;
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
        }
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

    /// <summary>
    /// The cone's hull, in the beam's own frame (along +X, one unit its full reach, Y up): its round side from her bell out
    /// to its reach, and a disc closing its far end, a little wider than the cone. Its material marches through it, so
    /// only its shape matters: each vertex carries its outward normal (the march is drawn from its far side).
    /// </summary>
    static void LayHull(ImmediateMesh mesh, float half)
    {
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        float tan = Mathf.Tan(half) * 1.04f;
        Vector3 Rim(float phi) => new(1f, tan * Mathf.Sin(phi), tan * Mathf.Cos(phi));
        Vector3 Side(float phi) => new Vector3(-tan, Mathf.Sin(phi), Mathf.Cos(phi)).Normalized();
        for (int m = 0; m < Around; m++)
        {
            float p0 = m * Mathf.Tau / Around, p1 = (m + 1) * Mathf.Tau / Around;
            mesh.SurfaceSetNormal(Side((p0 + p1) * 0.5f));
            mesh.SurfaceAddVertex(Vector3.Zero);
            mesh.SurfaceSetNormal(Side(p0));
            mesh.SurfaceAddVertex(Rim(p0));
            mesh.SurfaceSetNormal(Side(p1));
            mesh.SurfaceAddVertex(Rim(p1));
            mesh.SurfaceSetNormal(Vector3.Right);
            mesh.SurfaceAddVertex(new Vector3(1f, 0f, 0f));
            mesh.SurfaceSetNormal(Vector3.Right);
            mesh.SurfaceAddVertex(Rim(p1));
            mesh.SurfaceSetNormal(Vector3.Right);
            mesh.SurfaceAddVertex(Rim(p0));
        }
        mesh.SurfaceEnd();
    }

    /// <summary>A fan for the next of her beams' rays, made as it is first needed.</summary>
    Fan FanAt(int i)
    {
        while (_fans.Count <= i)
        {
            var volumeMaterial = new ShaderMaterial { Shader = _volumeShader };
            var volumeMesh = new ImmediateMesh();
            var volume = new MeshInstance3D { Mesh = volumeMesh, MaterialOverride = volumeMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false, ExtraCullMargin = 40f };
            AddChild(volume);
            var shadow = new BeamShadow();
            volumeMaterial.SetShaderParameter("shadow", shadow.Texture);
            _fans.Add(new Fan { Volume = volume, VolumeMaterial = volumeMaterial, VolumeMesh = volumeMesh, Shadow = shadow });
        }
        return _fans[i];
    }

    OmniLight3D GlintAt(int k)
    {
        while (_glints.Count <= k)
        {
            var g = new OmniLight3D { OmniRange = GlintRange, OmniAttenuation = 1.4f, ShadowEnabled = false, Visible = false, LightSpecular = 0.8f };
            AddChild(g);
            _glints.Add(g);
        }
        return _glints[k];
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
        int glints = 0;
        foreach (var (beam, _) in world.HerBeams)
        {
            var fan = FanAt(i);
            bool main = beam == world.Beam;
            int slot = main ? 0 : beam == world.RearBeam ? 1 : beam == world.SweepBeam ? 2 : -1;
            var born = slot < 0 ? world.BornOf(beam) : null;
            bool ofHers = slot == 0 || (slot < 0 && born is null);
            fan.Lit = slot >= 0 ? SlotLit[slot] : null!;
            fan.Dir = slot >= 0 ? SlotDir[slot] : null!;
            i++;
            bool on = beam.Shining;
            fan.On = Mathf.MoveToward(fan.On, on ? 1f : 0f, dt * (on ? 10f : 5f));
            // (Coming on, it shoots out of her bell to its reach; going off, it fades where it is.)
            if (on) fan.Out = Mathf.MoveToward(fan.Out, 1f, dt / ExtendTime);
            else if (fan.On <= 0.001f) fan.Out = 0f;
            fan.Volume.Visible = fan.On > 0.001f;
            if (main) _spot.Visible = fan.Volume.Visible;
            if (!fan.Volume.Visible)
            {
                if (slot > 0) RenderingServer.GlobalShaderParameterSet(fan.Lit, new Vector4(0f, -1000f, 0f, 0f));
                if (main)
                {
                    RenderingServer.GlobalShaderParameterSet(BeamAt, new Vector4(0f, -1000f, 0f, 0f));
                    RenderingServer.GlobalShaderParameterSet(BeamAxis, new Vector4(1f, 0f, 0f, 0f));
                }
                continue;
            }
            var dir = new Vector3(beam.Direction.X, 0f, beam.Direction.Y);
            float range = beam.Range;
            // Its lines, each to what stops it (a born ray: its axis, to the rock).
            float longest = born is null ? LayFan(world, beam, fan.On * dim * beam.Intensity / LightTuning.BeamIntensity)
                : world.LineReach(beam.Position, beam.Direction, Mathf.Max(range, 0.1f), out _);
            float stop = Mathf.Clamp(longest / Mathf.Max(range, 0.1f), 0.02f, 1f);
            var origin = new Vector3(beam.Position.X, LevelMap.SwimBand + 0.3f, beam.Position.Y);
            // Where its light gets to, in three dimensions; its hull to march through. A born ray ends at the rock.
            if (born is not null) range = Mathf.Min(range, longest + 0.4f);
            range = Mathf.Max(range, 0.1f);
            // Shooting out of her bell as it comes on: drawn out only as far as it has got yet.
            if (fan.Out < 1f)
            {
                float got = 1f - Mathf.Pow(1f - fan.Out, 3f);
                float stopAt = stop * Mathf.Max(beam.Range, 0.1f);
                range = Mathf.Max(range * got, 0.1f);
                stop = Mathf.Clamp(Mathf.Min(stopAt, range) / range, 0.02f, 1f);
            }
            // (Its widest, at its reach: a flaring beam's trumpet mouth. PlaneOpenPaths.cs.)
            float widest = beam.HalfAngle * (1f + beam.Flare);
            // (Cast every other frame, each fan on its own turn: a creature's shadow a frame late does not show; and at once
            // as a beam comes on.)
            if (born is null && (fan.On < 0.5f || (Engine.GetProcessFrames() + (ulong)i) % 2 == 0))
                fan.Shadow.Cast(world, beam.Position, origin.Y, beam.Direction, widest, range);
            fan.VolumeMaterial.SetShaderParameter("shadow", born is null ? fan.Shadow.Texture : _unshadowed);
            if (!Mathf.IsEqualApprox(fan.HullHalf, widest))
            {
                LayHull(fan.VolumeMesh, widest);
                fan.HullHalf = widest;
            }
            fan.Volume.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.Atan2(-dir.Z, dir.X)).Scaled(new Vector3(range, range, range)), origin);
            var m = fan.VolumeMaterial;
            // In the colour her pearls give this ray (PlaneColour.cs).
            var rayHue = world.RayHue(beam);
            // Her rhythms on her own beam's rays (PlaneRhythm.cs): focus a white-hot thread, spin-up a thicker haze, the
            // pulse a flash on each beat.
            bool rhythmic = born is null && slot <= 0;
            float frenzy = world.FrenzyLeft > 0f ? Mathf.Min(1f, world.FrenzyLeft / 0.5f) : 0f;
            float rhythmLook = rhythmic ? (1f + FocusGlow * world.Focus) * (1f + SpinGlow * world.SpinUp) * (1f + BeatGlow * world.PulseBeat) * (1f + FrenzyGlow * frenzy) : 1f;
            var tint = OctoShoots.Game.Fx.LightColour.Beam(rayHue, TopDownMain.ShowcaseOn);
            if (rhythmic) tint = tint.Lerp(Colors.White, FocusWhite * world.Focus + FrenzyWhite * frenzy);
            // Black Sun: the creatures' shadows down her own beam glow dark violet (her_beam_volume).
            m.SetShaderParameter("black_sun", born is null && world.Run.Loadout.Flags.Contains("blackSun") ? 1f : 0f);
            m.SetShaderParameter("tint", tint);
            // (A coloured ray's haze a little thicker, so its colour shows.)
            // (A born ray drawn by the square root of its worth, so even a faint one shows: BornHaze.)
            float bright = born is null ? beam.Intensity / LightTuning.BeamIntensity
                : Mathf.Clamp(Mathf.Sqrt(born.Worth / HerLightTuning.BeamShots) * BornHaze, BornLeast, 1.2f);
            m.SetShaderParameter("strength", fan.On * dim * bright * rhythmLook * (rayHue is LightHue.None or LightHue.White ? 1f : ColouredHaze));
            m.SetShaderParameter("origin", origin);
            m.SetShaderParameter("axis", dir);
            m.SetShaderParameter("half_angle", beam.HalfAngle);
            m.SetShaderParameter("flare", beam.Flare);
            m.SetShaderParameter("span", widest * BeamShadow.Margin);
            m.SetShaderParameter("reach", range);
            // Her colours' motes in the water it lights (BeamView.Motes.cs).
            if (on) EmitMotes(world, beam, rayHue, range, fan.On * Mathf.Min(bright, 1.2f) * (main ? 1f : 0.55f), origin.Y, dt);
            if (main)
            {
                // The reef's own light from her beam (reef_show's rs_beam) is shadowed alike.
                RenderingServer.GlobalShaderParameterSet(BeamShadowTex, fan.Shadow.Texture);
                RenderingServer.GlobalShaderParameterSet(BeamFrame, new Vector4(origin.X, origin.Y, origin.Z, range));
                RenderingServer.GlobalShaderParameterSet(BeamAxis, new Vector4(dir.X, dir.Z, widest * BeamShadow.Margin, 1f));
            }
            // A born ray's glint where her light landed (a ghost's: where it went into the rock).
            if (born is not null && born.Word != PathWord.Return)
            {
                var g = GlintAt(glints++);
                var at = born.Word == PathWord.Ghost ? born.Into : born.At;
                var off = born.Word == PathWord.Ghost ? -beam.Direction : (beam.Position - born.At);
                off = off.LengthSquared() > 1e-6f ? System.Numerics.Vector2.Normalize(off) : System.Numerics.Vector2.Zero;
                g.Visible = true;
                g.GlobalPosition = new Vector3(at.X + off.X * 0.35f, LevelMap.SwimBand + 0.35f, at.Y + off.Y * 0.35f);
                g.LightColor = OctoShoots.Game.Fx.LightColour.Beam(world.RayHue(beam), TopDownMain.ShowcaseOn);
                g.LightEnergy = GlintEnergy * fan.On * dim * Mathf.Clamp(Mathf.Sqrt(born.Worth / HerLightTuning.BeamShots) * 2f, 0.5f, 1.2f)
                    * (0.85f + 0.15f * Mathf.Sin(_time * 11f + i * 1.7f));
            }
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
            var hue = world.HueAlong(beam.Position, world.BeamEnd) | rayHue;
            if (hue != LightHue.None)
            {
                _tintLight.Visible = true;
                _tintLight.LightColor = OctoShoots.Game.Fx.HueColor.Of(hue);
                _tintLight.LightEnergy = 2.5f * fan.On * dim;
                var back = world.BeamEnd - beam.Direction * 0.6f;
                _tintLight.GlobalPosition = new Vector3(back.X, LevelMap.SwimBand + 0.4f, back.Y);
            }
            // Her own beam's spotlight: from her heart, level along the beam (tilted down, it lit the floor south of it on the screen).
            var look = (dir + Vector3.Down * SpotTilt).Normalized();
            _spot.GlobalTransform = new Transform3D(Basis.LookingAt(look, Vector3.Up), heart);
            _spot.LightColor = OctoShoots.Game.Fx.LightColour.Beam(rayHue, TopDownMain.ShowcaseOn);
            _spot.LightEnergy = SpotEnergy * fan.On * dim * rhythmLook;
            _spot.SpotRange = range * 1.25f;
            _spot.SpotAngle = Mathf.RadToDeg(beam.HalfAngle);
            // The sear on the corrupted (her_burn.gdshaderinc): only while it burns something, as her shots', in its colours.
            RenderingServer.GlobalShaderParameterSet(BurnTint, BurnTintOf(rayHue));
            RenderingServer.GlobalShaderParameterSet(BeamAt, world.BeamBurns
                ? new Vector4(origin.X, origin.Y + 0.05f, origin.Z, range * stop) : new Vector4(0f, -1000f, 0f, 0f));
        }
        for (int k = i; k < _fans.Count; k++) _fans[k].Volume.Visible = false;
        for (int k = glints; k < _glints.Count; k++) _glints[k].Visible = false;
        if (_tintBegun) _tintMesh.SurfaceEnd();
        // What her beam shows (her_reveal), the whole of its fan: along her aim, as wide as its rays spread.
        var her = new Vector3(world.Beam.Position.X, LevelMap.SwimBand + 0.3f, world.Beam.Position.Y);
        var aim = new Vector3(world.BeamAim.X, 0f, world.BeamAim.Y);
        RenderingServer.GlobalShaderParameterSet(SlotDir[0], new Vector4(aim.X, 0f, aim.Z, Mathf.Max(world.BeamSpread, world.Beam.HalfAngle)));
        RenderingServer.GlobalShaderParameterSet(SlotLit[0], anyOn ? new Vector4(her.X, her.Y, her.Z, reach) : new Vector4(0f, -1000f, 0f, 0f));
        DrawArcs(world, color);
        DrawPools(world, dim);
        DrawFireflies(world, dim);
        TickMotes(dt);
    }

    static readonly StringName BurnTint = "her_burn_tint";

    /// <summary>Her light's colour as it lights what it burns, and its colours' bits (her_burn.gdshaderinc's hb_tint).</summary>
    static Vector4 BurnTintOf(LightHue hue)
    {
        var c = OctoShoots.Game.Fx.LightColour.Beam(hue, TopDownMain.ShowcaseOn);
        return new Vector4(c.R, c.G, c.B, (float)(int)hue);
    }

    /// <summary>
    /// Her fireflies (PlaneFireflies.cs): each a bright mote of her light in her colours, with a little light of its own on
    /// the reef, twinkling; one a murkling drank fades out and back in when it returns.
    /// </summary>
    readonly List<(MeshInstance3D Mote, OmniLight3D Light, float Shown)> _flies = new();
    StandardMaterial3D? _flyMaterial;
    const float FlyEnergy = 2f, FlyRange = 2.4f, FlySize = 0.17f;

    void DrawFireflies(PlaneWorld world, float dim)
    {
        _flyMaterial ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = false,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        var flies = world.Fireflies;
        while (_flies.Count < flies.Count)
        {
            var mote = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 5 }, MaterialOverride = _flyMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            var light = new OmniLight3D { OmniRange = FlyRange, OmniAttenuation = 1.3f, ShadowEnabled = false, LightSpecular = 0.3f };
            AddChild(mote);
            AddChild(light);
            _flies.Add((mote, light, 0f));
        }
        var tint = world.HerHue == LightHue.None ? OctoShoots.Game.Fx.HerColor.Light : OctoShoots.Game.Fx.LightColour.Of(world.HerHue);
        _flyMaterial.AlbedoColor = tint.Lerp(Colors.White, 0.35f);
        for (int i = 0; i < _flies.Count; i++)
        {
            var (mote, light, shown) = _flies[i];
            bool on = i < flies.Count && flies[i].Lit && !world.Defeated;
            shown = Mathf.MoveToward(shown, on ? 1f : 0f, (float)GetProcessDeltaTime() * (on ? 2.5f : 6f));
            _flies[i] = (mote, light, shown);
            mote.Visible = light.Visible = shown > 0.01f && i < flies.Count;
            if (!mote.Visible) continue;
            var p = flies[i].Emitter.Position;
            float twinkle = 0.75f + 0.25f * Mathf.Sin(_time * 7.3f + i * 2.1f);
            var at = new Vector3(p.X, LevelMap.SwimBand + 0.35f + 0.12f * Mathf.Sin(_time * 2.4f + i), p.Y);
            mote.Position = at;
            mote.Scale = Vector3.One * FlySize * (0.6f + 0.4f * shown) * twinkle;
            light.Position = at;
            light.LightColor = tint;
            light.LightEnergy = FlyEnergy * dim * shown * twinkle;
        }
    }

    /// <summary>
    /// Her pools of light (Linger, PlaneOpenPaths.cs): soft discs of her light on the water where her beam ended, in their
    /// colours, fading as they go; under Aurora, taller and brighter.
    /// </summary>
    MultiMeshInstance3D? _pools;
    const float PoolGlow = 0.55f;

    void DrawPools(PlaneWorld world, float dim)
    {
        if (_pools is null)
        {
            var shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled;
varying vec4 v_col;
void vertex() { v_col = INSTANCE_CUSTOM; }
void fragment() {
	float r = length(UV - 0.5) * 2.0;
	float soft = 1.0 - smoothstep(0.0, 1.0, r);
	soft *= soft;
	float ring = smoothstep(0.55, 0.8, r) * (1.0 - smoothstep(0.8, 1.0, r));
	ALBEDO = v_col.rgb;
	ALPHA = clamp((soft * 0.85 + ring * 0.35) * v_col.a, 0.0, 1.0);
}",
            };
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true,
                Mesh = new PlaneMesh { Size = Vector2.One * 2f },
            };
            _pools = new MultiMeshInstance3D
            {
                Multimesh = mm, MaterialOverride = new ShaderMaterial { Shader = shader },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 60f,
            };
            AddChild(_pools);
        }
        var pools = world.Pools;
        var multi = _pools.Multimesh;
        if (multi.InstanceCount < pools.Count) multi.InstanceCount = Mathf.Max(pools.Count, OpenPathTuning.MostPools + 8);
        multi.VisibleInstanceCount = pools.Count;
        bool aurora = world.Run.Loadout.Has("aurora");
        bool reef = world.Run.Loadout.Has("coral_reef");
        for (int i = 0; i < pools.Count; i++)
        {
            var p = pools[i];
            float left = p.Left;
            // Swelling a little as it is laid down, then fading out.
            float grow = Mathf.Min(1f, p.Age / 0.15f);
            float r = OpenPathTuning.PoolReach * (0.6f + 0.4f * grow) * (aurora ? 1.25f : 1f);
            var basis = Basis.Identity.Scaled(new Vector3(r, 1f, r));
            multi.SetInstanceTransform(i, new Transform3D(basis, new Vector3(p.At.X, LevelMap.SwimBand + 0.05f + (aurora ? 0.1f : 0f), p.At.Y)));
            var c = OctoShoots.Game.Fx.LightColour.Beam(p.Hue, TopDownMain.ShowcaseOn);
            // (Coral Reef: her afterlight a living coral, brighter where she stands in it and mends.)
            if (reef) c = c.Lerp(ReefPool, 0.6f);
            multi.SetInstanceCustomData(i, new Color(c.R, c.G, c.B, PoolGlow * dim * left * left * (aurora ? 1.4f : 1f)));
        }
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
