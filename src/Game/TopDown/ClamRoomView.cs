using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The clams (PlaneClams.cs) and every pearl's light. A clam is a giant clam lying shut on the path, three times the
/// pearl's width, its mouth turned toward the camera; when light touches it its upper valve swings open (easing out past
/// open and settling back) and shows what it kept: a pearl on its lower valve, whose light (its colours, its pattern)
/// falls out of the clam's mouth over the floor (pearl_floor_light, a cone of 120°) with a spotlight of its colour and
/// magic particles floating up round it, all fading once the pearl is taken; or a murkling, which comes out at her. A
/// pearl lying loose anywhere (a boss's, a crab's, an altar's) lays the same light all round it, with the same particles.
/// </summary>
public partial class ClamRoomView : Node3D
{
    sealed class Clam
    {
        public PlaneClam Sim = null!;
        public Node3D Hinge = null!;
        /// <summary>Its root, and where its sim stood when it was built (an open level's window moves it since).</summary>
        public Node3D Root = null!;
        public System.Numerics.Vector2 BuiltAt;
        public ShaderMaterial? Floor;
        public SpotLight3D? Inner;
        public float Light, Magic;
        public Color Tint = Gold;
    }

    sealed class Loose
    {
        public Node3D Root = null!;
        public ShaderMaterial Floor = null!;
        public float Light, Magic;
        public System.Numerics.Vector2 BuiltAt;
    }

    /// <summary>A built thing's root kept over where its sim now stands (an open level's window moves on), its floor light with it.</summary>
    static void Follow(Node3D root, ShaderMaterial? floor, System.Numerics.Vector2 builtAt, System.Numerics.Vector2 now)
    {
        var d = now - builtAt;
        var at = new Vector3(d.X, 0f, d.Y);
        if (root.Position.IsEqualApprox(at)) return;
        root.Position = at;
        floor?.SetShaderParameter(SN.Of("glow_pos"), new Vector3(now.X, RestHeight, now.Y));
    }

    const float InnerEnergy = 1.2f;
    /// <summary>Where a clam's pearl lies: at the swim level, on its lower valve.</summary>
    public const float RestHeight = LevelMap.SwimBand + 0.02f;
    /// <summary>The clam: half its width (three times the pearl's), how far its upper valve stands open (degrees), how long it takes to open (s).</summary>
    const float ClamHalf = 1.26f, OpenDeg = 62f, OpenSeconds = 0.9f;
    /// <summary>Over the pearl, where its hint floats (metres).</summary>
    const float HintOver = 1.5f;
    /// <summary>The pearl's radius as drawn (CombatView), for where the lower valve's lip lies under it.</summary>
    const float PearlRadius = 0.42f;
    /// <summary>A pearl's light on the floor: how far it reaches out of a clam, and round a loose pearl (m).</summary>
    const float ClamReach = 12f, LooseReach = 6f;

    readonly List<Clam> _clams = new();
    readonly Dictionary<PlanePearl, Loose> _loose = new();
    MagicParticles _magic = null!;

    /// <summary>The open clams' glows for the water pass (xz, radius, strength), and their colours.</summary>
    public List<Vector4> Glows { get; } = new();
    public List<Color> GlowColors { get; } = new();
    static readonly Color Gold = new(1f, 0.8f, 0.45f);

    public override void _Ready()
    {
        _magic = new MagicParticles(384);
        AddChild(_magic);
    }

    /// <summary>A new level: its clams.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var c in _clams) c.Hinge.GetParent().GetParent().QueueFree();
        _clams.Clear();
        foreach (var l in _loose.Values) l.Root.QueueFree();
        _loose.Clear();
        _magic.Clear();
        foreach (var sim in world.Clams) _clams.Add(Build(world, sim));
    }

    /// <summary>A pearl's look, for its light: its colours and its seed.</summary>
    static void Colour(PlaneWorld world, PlanePearl pearl, ShaderMaterial m, out Color tint)
    {
        tint = Gold;
        if (world.Run.Catalog?.TryGet(pearl.ItemId, out var item) != true) return;
        tint = PearlLight.Mean(item);
        var look = item.Pearl ?? new OctoShoots.Core.Items.PearlLook { Colors = { "#ffffff" } };
        m.SetShaderParameter(SN.Of("glow_c0"), Color.FromHtml(look.Colors[0]));
        m.SetShaderParameter(SN.Of("glow_c1"), Color.FromHtml(look.Colors[System.Math.Min(1, look.Colors.Count - 1)]));
        m.SetShaderParameter(SN.Of("glow_c2"), Color.FromHtml(look.Colors[^1]));
        m.SetShaderParameter(SN.Of("glow_seed"), (float)(System.StringComparer.Ordinal.GetHashCode(item.Id) % 97));
    }

    /// <summary>A pearl's light laid over the floor round it (in its pattern), in a cone that way, or all round.</summary>
    ShaderMaterial FloorLight(Node3D root, LevelMap map, System.Numerics.Vector2 at, Vector3 facing, float halfAngle, float reach)
    {
        var light = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/pearl_floor_light.gdshader"), RenderPriority = 1 };
        light.SetShaderParameter(SN.Of("glow_pos"), new Vector3(at.X, RestHeight, at.Y));
        light.SetShaderParameter(SN.Of("facing"), new Vector2(facing.X, facing.Z));
        light.SetShaderParameter(SN.Of("half_angle"), halfAngle);
        light.SetShaderParameter(SN.Of("reach"), reach);
        light.SetShaderParameter(SN.Of("glow_strength"), 0f);
        root.AddChild(new MeshInstance3D { Mesh = FloorPatch(map, at, reach), MaterialOverride = light, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return light;
    }

    Clam Build(PlaneWorld world, PlaneClam sim)
    {
        var map = world.Map;
        var at = sim.Position;
        float floor = map.HeightAt(at);
        var root = new Node3D();
        AddChild(root);
        // Its mouth toward the camera (south), turned a little each its own way, the hinge behind.
        float turn = (Mathf.PosMod(at.X * 0.37f + at.Y * 0.61f, 1f) - 0.5f) * 0.9f;
        var facing = new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn));
        var clam = new Node3D { Position = new Vector3(at.X, 0f, at.Y), Rotation = new Vector3(0f, -Mathf.Atan2(Mathf.Cos(turn), Mathf.Sin(turn)), 0f) };
        root.AddChild(clam);
        var shell = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/clam_shell.gdshader") };
        shell.SetShaderParameter(SN.Of("ribs"), 8f);
        // A healthy giant clam: bone-cream, a little darker in its grooves.
        shell.SetShaderParameter(SN.Of("color_a"), new Color(0.9f, 0.86f, 0.78f));
        shell.SetShaderParameter(SN.Of("color_b"), new Color(0.7f, 0.63f, 0.56f));
        float lip = RestHeight - PearlRadius * 0.9f;
        float depth = Mathf.Clamp(lip - floor, 0.18f, 1.2f);
        float rz = ClamHalf * 0.88f;
        var dome = new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 40, Rings = 12 };
        var lower = new MeshInstance3D { Mesh = dome, MaterialOverride = shell, Scale = new Vector3(ClamHalf, depth, rz), Position = new Vector3(0f, lip, 0f), RotationDegrees = new Vector3(180f, 0f, 0f) };
        lower.SetInstanceShaderParameter(SN.Of("drained"), 0f);
        clam.AddChild(lower);
        // Its flesh inside, soft and clean, under the pearl.
        clam.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.04f, RadialSegments = 40 },
            Scale = new Vector3(ClamHalf * 0.92f, 1f, rz * 0.92f),
            Position = new Vector3(0f, lip - 0.03f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.55f, 0.6f), Roughness = 0.35f },
        });
        var hinge = new Node3D { Position = new Vector3(-ClamHalf, lip, 0f) };
        clam.AddChild(hinge);
        var upper = new MeshInstance3D { Mesh = dome, MaterialOverride = shell, Scale = new Vector3(ClamHalf, 0.55f, rz), Position = new Vector3(ClamHalf, 0f, 0f) };
        upper.SetInstanceShaderParameter(SN.Of("drained"), 0f);
        hinge.AddChild(upper);

        var c = new Clam { Sim = sim, Hinge = hinge, Root = root, BuiltAt = at };
        if (sim.Pearl is { } pearl)
        {
            // Its pearl's light, out of the clam's mouth only, and a shadowed spotlight of its colour the same way.
            c.Floor = FloorLight(root, map, at, facing, 1.0472f, ClamReach);
            Colour(world, pearl, c.Floor, out c.Tint);
            c.Inner = new SpotLight3D
            {
                LightColor = c.Tint, LightEnergy = 0f, SpotRange = ClamReach * 0.7f, SpotAngle = 60f, SpotAngleAttenuation = 1.4f, Visible = false,
                ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows, ShadowBias = 0.08f, ShadowBlur = 1.5f,
                Position = new Vector3(at.X, RestHeight + 0.2f, at.Y),
            };
            root.AddChild(c.Inner);
            c.Inner.LookAt(c.Inner.Position + facing + Vector3.Down * 0.35f, Vector3.Up);
        }
        return c;
    }

    /// <summary>Out of 0 → 1, a little past the end and back (an ease-out with overshoot).</summary>
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    /// <summary>A patch of the ground's own surface round a point (this far), lifted a hair off it, for light laid on it.</summary>
    static ArrayMesh FloorPatch(LevelMap map, System.Numerics.Vector2 at, float radius)
    {
        int n = radius > 8f ? 64 : 32;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                var q = at + new System.Numerics.Vector2((x / (float)n * 2f - 1f) * radius, (y / (float)n * 2f - 1f) * radius);
                st.AddVertex(new Vector3(q.X, map.HeightAt(q) + 0.04f, q.Y));
            }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int a = y * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                st.AddIndex(a); st.AddIndex(c); st.AddIndex(b);
                st.AddIndex(b); st.AddIndex(c); st.AddIndex(d);
            }
        return st.Commit();
    }

    static bool Waiting(PlanePearl p) => !p.Taken && !p.Absorbing;

    /// <summary>The pearl waiting in an open clam she is near, and the point over it its hint floats at (world), if any.</summary>
    public (string Id, Vector3 Over)? Near(PlaneWorld world, float reach)
    {
        foreach (var c in _clams)
        {
            if (!c.Sim.Open || c.Sim.Pearl is not { } pearl || !Waiting(pearl)) continue;
            if (System.Numerics.Vector2.Distance(world.Player.Position, c.Sim.Position) > reach) continue;
            return (pearl.ItemId, new Vector3(c.Sim.Position.X, RestHeight + HintOver, c.Sim.Position.Y));
        }
        return null;
    }

    /// <summary>Magic particles floating up round a waiting pearl, so many a second.</summary>
    void Sparkle(ref float magic, System.Numerics.Vector2 at, float rate, float dt)
    {
        magic += dt * rate;
        while (magic >= 1f)
        {
            magic -= 1f;
            float a = _magic.RandRange(0f, Mathf.Tau), d = _magic.RandRange(0.4f, 2.2f);
            var from = new Vector3(at.X + Mathf.Cos(a) * d, RestHeight - 0.3f + _magic.RandRange(0f, 0.4f), at.Y + Mathf.Sin(a) * d);
            var drift = new Vector3(_magic.RandRange(-0.12f, 0.12f), _magic.RandRange(0.35f, 0.75f), _magic.RandRange(-0.12f, 0.12f));
            _magic.Emit(from, drift, _magic.RandRange(1.6f, 2.6f), _magic.RandRange(0.1f, 0.18f), drag: 0.2f);
        }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        // Brighter the deeper she goes.
        float bright = Mathf.Clamp(0.6f + 0.3f * (world.Map.Depth - 1), 0.6f, 2.2f);
        Glows.Clear();
        GlowColors.Clear();
        // (An open level's clams come in with their chunks.)
        while (_clams.Count < world.Clams.Count) _clams.Add(Build(world, world.Clams[_clams.Count]));
        foreach (var c in _clams)
        {
            Follow(c.Root, c.Floor, c.BuiltAt, c.Sim.Position);
            float t = c.Sim.Open ? Mathf.Clamp(c.Sim.Opened / OpenSeconds, 0f, 1f) : 0f;
            c.Hinge.Rotation = new Vector3(0f, 0f, Mathf.DegToRad(OpenDeg) * EaseOutBack(t));
            if (c.Floor is null || c.Inner is null || c.Sim.Pearl is not { } pearl) continue;
            // Its light comes out with the opening lid, and fades once the pearl is taken.
            bool waiting = c.Sim.Open && Waiting(pearl);
            c.Light = Mathf.MoveToward(c.Light, waiting ? t : 0f, dt / (waiting ? 0.3f : 1.2f));
            c.Inner.LightEnergy = InnerEnergy * bright * c.Light;
            c.Inner.Visible = c.Light > 0.001f;
            c.Floor.SetShaderParameter(SN.Of("glow_strength"), 1.3f * Mathf.Min(bright, 1.2f) * c.Light);
            if (waiting) Sparkle(ref c.Magic, c.Sim.Position, 7f * t, dt);
            if (c.Light <= 0.001f) continue;
            Glows.Add(new Vector4(c.Sim.Position.X, c.Sim.Position.Y, 6f, 0.45f * bright * c.Light));
            GlowColors.Add(c.Tint);
        }

        // Every other pearl lying loose: the same light all round it, while it waits.
        foreach (var pearl in world.Pearls)
        {
            if (_loose.ContainsKey(pearl) || !Waiting(pearl) || world.Clams.Exists(c => c.Pearl == pearl)) continue;
            var root = new Node3D();
            AddChild(root);
            var l = new Loose { Root = root, Floor = FloorLight(root, world.Map, pearl.Position, Vector3.Back, Mathf.Pi + 0.5f, LooseReach), BuiltAt = pearl.Position };
            Colour(world, pearl, l.Floor, out _);
            _loose[pearl] = l;
        }
        List<PlanePearl>? gone = null;
        foreach (var (pearl, l) in _loose)
        {
            bool waiting = Waiting(pearl);
            Follow(l.Root, l.Floor, l.BuiltAt, pearl.Position);
            l.Light = Mathf.MoveToward(l.Light, waiting ? 1f : 0f, dt / (waiting ? 0.6f : 0.8f));
            l.Floor.SetShaderParameter(SN.Of("glow_strength"), 1.1f * Mathf.Min(bright, 1.2f) * l.Light);
            if (waiting) Sparkle(ref l.Magic, pearl.Position, 4f, dt);
            else if (l.Light <= 0f) (gone ??= new()).Add(pearl);
        }
        if (gone is not null)
            foreach (var p in gone)
            {
                _loose[p].Root.QueueFree();
                _loose.Remove(p);
            }
        if (GetViewport().GetCamera3D() is { } camera) _magic.Tick(dt, camera.GlobalBasis);
    }
}
