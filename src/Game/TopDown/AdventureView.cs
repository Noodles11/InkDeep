using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The adventure on screen (docs/ADVENTURE.md), presentation only: the ink as tiny purple specks through the water's layers,
/// thicker the nearer its leak (ink_dot.gdshader, one MultiMesh); the leaks, brittle stars of ink floating at her plane —
/// a dark disc with hot air over it in the post pass, five long arms drawn as tapering ribbons along the sim's joints
/// (leak_arm.gdshader), a purple shimmer running through them on a beat, gold light-staples snapping in across them one
/// after another as her inner light works, the stapled stretch's edges searing, and all dissolving once sealed; dots her
/// light clears go up in magic particles.
/// </summary>
public partial class AdventureView : Node3D
{
    /// <summary>How high over her plane a leak floats, how much wider its arms are drawn than they sting, its disc's size.</summary>
    const float LeakHeight = 0.12f, ArmDrawn = 1.15f, DiscSize = 1.56f;
    /// <summary>A shimmer every this long; twice as often with her within Close.</summary>
    const float ShimmerSeconds = 3f, Close = 10f;
    /// <summary>A staple snaps in or out over this long; once sealed, the leak dissolves over this long.</summary>
    const float StapleSeconds = 0.18f, SealSeconds = 1.4f;
    /// <summary>Each stretch of an arm between joints is drawn as this many pieces, smoothed through the joints.</summary>
    const int ArmPieces = 3;

    sealed class LeakNode
    {
        /// <summary>How much of it is seen in light now (eased): its glow and the water it bends go with it.</summary>
        public float Seen;
        public MeshInstance3D Arms = null!, Disc = null!;
        public ImmediateMesh Mesh = null!;
        public OmniLight3D Light = null!;
        /// <summary>The staples across its arms (in the order they go in), and how far each is in now.</summary>
        public MeshInstance3D[] Staples = System.Array.Empty<MeshInstance3D>();
        public float[] In = System.Array.Empty<float>();
        /// <summary>The shimmer's beat; how hard she is sealing it now; seconds since it shut (negative: open).</summary>
        public float Beat, Sealing, Shut = -1f;
    }

    /// <summary>A dot's specks at most (the nearest its leak), how big, and the layers of water they lie on (metres off her plane).</summary>
    const int Marks = 4;
    const float MarkSize = 0.075f, Dense = 10f;
    static readonly float[] Layers = { 0f, 1.5f, -1.7f, 2.8f };
    /// <summary>Floats per speck in the buffer: its transform and its seed.</summary>
    const int MarkFloats = 16;

    MultiMesh _dots = null!;
    float[] _buffer = System.Array.Empty<float>();
    long _version = -1;
    MagicParticles _magic = null!;
    Shader _armShader = null!, _discShader = null!;
    StandardMaterial3D _staple = null!;
    readonly QuadMesh _stapleQuad = new() { Size = new Vector2(1f, 1f), Orientation = PlaneMesh.OrientationEnum.Y };
    readonly List<LeakNode> _leaks = new();
    readonly Vector3[] _airs = new Vector3[8];
    readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _dots = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ink_dot.gdshader") } },
            InstanceCount = AdventureTuning.MaxDots * Marks,
            VisibleInstanceCount = 0,
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _dots,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-10f, -10f, -10f), new Vector3(LevelMap.Seeded + 20f, 30f, LevelMap.Seeded + 20f)),
        });
        _buffer = new float[AdventureTuning.MaxDots * Marks * MarkFloats];
        _magic = new MagicParticles(2400);
        AddChild(_magic);
        _armShader = GD.Load<Shader>("res://assets/shaders/leak_arm.gdshader");
        _discShader = GD.Load<Shader>("res://assets/shaders/leak.gdshader");
        // Her light-staples: gold bars of light, glowing on their own.
        _staple = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.78f, 0.3f),
            EmissionEnabled = true, Emission = new Color(1f, 0.7f, 0.25f), EmissionEnergyMultiplier = 2.2f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _rng.Seed = 7117;
    }

    /// <summary>A new level: its leaks.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var n in _leaks)
        {
            n.Arms.QueueFree();
            n.Disc.QueueFree();
            n.Light.QueueFree();
            foreach (var s in n.Staples) s.QueueFree();
        }
        _leaks.Clear();
        _magic.Clear();
        _dots.VisibleInstanceCount = 0;
        _version = -1;
        Visible = world.Dots is not null;
        foreach (var v in world.Leaks) _leaks.Add(MakeLeak(v));
    }

    LeakNode MakeLeak(PlaneLeak v)
    {
        var at = new Vector3(v.Position.X, LevelMap.SwimBand + LeakHeight, v.Position.Y);
        var mesh = new ImmediateMesh();
        float seed = _rng.RandfRange(0f, 50f);
        var arms = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new ShaderMaterial { Shader = _armShader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Its arms reach far round it and move: never culled while near.
            ExtraCullMargin = 16f,
        };
        arms.SetInstanceShaderParameter(SN.Of("seed"), seed);
        AddChild(arms);
        var disc = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = Vector2.One * DiscSize, Orientation = PlaneMesh.OrientationEnum.Y },
            MaterialOverride = new ShaderMaterial { Shader = _discShader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = at + Vector3.Up * 0.01f,
        };
        disc.SetInstanceShaderParameter(SN.Of("seed"), seed);
        AddChild(disc);
        var light = new OmniLight3D { LightColor = new Color(0.6f, 0.25f, 0.95f), LightEnergy = 0f, OmniRange = 5f, ShadowEnabled = false, Position = at + Vector3.Up * 0.6f };
        AddChild(light);
        var node = new LeakNode { Arms = arms, Disc = disc, Mesh = mesh, Light = light, Beat = _rng.RandfRange(0f, 1f) };
        node.Staples = new MeshInstance3D[v.StapleCount];
        node.In = new float[v.StapleCount];
        for (int i = 0; i < node.Staples.Length; i++)
        {
            node.Staples[i] = new MeshInstance3D { Mesh = _stapleQuad, MaterialOverride = _staple, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            AddChild(node.Staples[i]);
        }
        if (v.Sealed) node.Shut = SealSeconds;
        return node;
    }

    /// <summary>
    /// Its arms as ribbons on her plane: through each arm's joints, smoothed, tapering to the tip. Per vertex, UV is along
    /// the arm (0 at the disc, 1 at the tip) and across it (0..1); COLOR.r says how far along it is stapled.
    /// </summary>
    void DrawArms(LeakNode node, PlaneLeak v, float time)
    {
        var m = node.Mesh;
        m.ClearSurfaces();
        m.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < v.Arms.Length; i++)
        {
            var arm = v.Arms[i];
            var pts = arm.Points;
            int segs = pts.Length - 1;
            if (segs < 1) continue;
            float stapled = v.StapledOn(i) / (float)segs;
            int samples = segs * ArmPieces;
            // A slow bob as it floats, each arm its own.
            float bob = Mathf.Sin(time * 0.8f + arm.Seed) * 0.06f;
            Vector3 prevL = default, prevR = default;
            float prevAlong = 0f;
            for (int s = 0; s <= samples; s++)
            {
                float u = s / (float)ArmPieces;
                int j = Mathf.Min((int)u, segs - 1);
                float t = u - j;
                var p0 = pts[Mathf.Max(j - 1, 0)];
                var p1 = pts[j];
                var p2 = pts[j + 1];
                var p3 = pts[Mathf.Min(j + 2, segs)];
                var c = CatmullRom(p0, p1, p2, p3, t);
                var ahead = CatmullRom(p0, p1, p2, p3, Mathf.Min(t + 0.05f, 1f)) - CatmullRom(p0, p1, p2, p3, Mathf.Max(t - 0.05f, 0f));
                if (ahead.LengthSquared() < 1e-8f) ahead = p2 - p1;
                ahead = System.Numerics.Vector2.Normalize(ahead);
                var side = new System.Numerics.Vector2(-ahead.Y, ahead.X);
                float along = s / (float)samples;
                float half = LeakArm.HalfWidth(along) * ArmDrawn * (along > 0.92f ? (1f - along) / 0.08f * 0.7f + 0.3f : 1f);
                float y = LevelMap.SwimBand + LeakHeight + bob * along;
                var l = new Vector3(c.X + side.X * half, y, c.Y + side.Y * half);
                var r = new Vector3(c.X - side.X * half, y, c.Y - side.Y * half);
                if (s > 0)
                {
                    var col = new Color(stapled, 0f, 0f);
                    Vertex(m, prevL, new Vector2(prevAlong, 0f), col);
                    Vertex(m, prevR, new Vector2(prevAlong, 1f), col);
                    Vertex(m, l, new Vector2(along, 0f), col);
                    Vertex(m, prevR, new Vector2(prevAlong, 1f), col);
                    Vertex(m, r, new Vector2(along, 1f), col);
                    Vertex(m, l, new Vector2(along, 0f), col);
                }
                prevL = l;
                prevR = r;
                prevAlong = along;
            }
        }
        m.SurfaceEnd();
    }

    static void Vertex(ImmediateMesh m, Vector3 at, Vector2 uv, Color col)
    {
        m.SurfaceSetNormal(Vector3.Up);
        m.SurfaceSetUV(uv);
        m.SurfaceSetColor(col);
        m.SurfaceAddVertex(at);
    }

    static System.Numerics.Vector2 CatmullRom(System.Numerics.Vector2 p0, System.Numerics.Vector2 p1, System.Numerics.Vector2 p2, System.Numerics.Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    public void Sync(PlaneWorld world, float dt, CameraRig camera)
    {
        if (world.Dots is not { } dots) return;
        if (dots.Version != _version) SyncDots(dots);
        SyncLeaks(world, dt, camera);
        if (camera.Camera is { } eye) _magic.Tick(dt, eye.GlobalBasis);
    }

    /// <summary>Lays the specks out afresh (only when the ink has changed): the nearer its leak a dot is, the more of them, up through the layers.</summary>
    void SyncDots(InkDots dots)
    {
        _version = dots.Version;
        int count = 0;
        foreach (int i in dots.Live)
        {
            if (count >= AdventureTuning.MaxDots * Marks) break;
            var c = InkDots.Centre(i);
            float near = 1f - Mathf.Clamp(dots.Far[i] / Dense, 0f, 1f);
            int marks = 1 + Mathf.RoundToInt((Marks - 1) * near);
            for (int m = 0; m < marks; m++)
            {
                float seed = ((i * Marks + m) * 0.7548777f) % 1f, seed2 = ((i * Marks + m) * 0.5698403f) % 1f;
                int o = count++ * MarkFloats;
                _buffer[o] = MarkSize; _buffer[o + 1] = 0f; _buffer[o + 2] = 0f; _buffer[o + 3] = c.X + (seed - 0.5f) * AdventureTuning.Spacing;
                _buffer[o + 4] = 0f; _buffer[o + 5] = MarkSize; _buffer[o + 6] = 0f; _buffer[o + 7] = LevelMap.SwimBand + Layers[m] + seed * 0.8f;
                _buffer[o + 8] = 0f; _buffer[o + 9] = 0f; _buffer[o + 10] = MarkSize; _buffer[o + 11] = c.Y + (seed2 - 0.5f) * AdventureTuning.Spacing;
                _buffer[o + 12] = seed;
            }
        }
        _dots.Buffer = _buffer;
        _dots.VisibleInstanceCount = count;
    }

    void SyncLeaks(PlaneWorld world, float dt, CameraRig camera)
    {
        var her = world.Player.Position;
        int shown = 0;
        float step = dt / StapleSeconds;
        float time = Time.GetTicksMsec() / 1000f;
        for (int k = 0; k < _leaks.Count && k < world.Leaks.Count; k++)
        {
            var v = world.Leaks[k];
            var node = _leaks[k];
            // A great cave's leak is its boss: nothing of it is drawn.
            if (v.Hidden)
            {
                node.Arms.Visible = node.Disc.Visible = node.Light.Visible = false;
                foreach (var s in node.Staples) s.Visible = false;
                continue;
            }
            float d = System.Numerics.Vector2.Distance(v.Position, her);
            if (v.Sealed && node.Shut < 0f) node.Shut = 0f;
            float shut = node.Shut < 0f ? 0f : Mathf.Clamp(node.Shut / SealSeconds, 0f, 1f);
            if (node.Shut >= 0f) node.Shut += dt;
            bool gone = shut >= 1f;
            node.Arms.Visible = node.Disc.Visible = node.Light.Visible = !gone;
            if (gone)
            {
                foreach (var s in node.Staples) s.Visible = false;
                continue;
            }
            node.Beat = Mathf.PosMod(node.Beat + dt / ShimmerSeconds * (d < Close ? 2f : 1f), 1f);
            // A short swell, then quiet.
            float shimmer = Mathf.Pow(Mathf.Max(Mathf.Sin(node.Beat * Mathf.Tau), 0f), 6f);
            // She is sealing it (her light on it now): its stapled edges sear brighter.
            bool sealing = !v.Sealed && v.Light > 0f && v.Since <= 0f;
            node.Sealing = Mathf.MoveToward(node.Sealing, sealing ? 1f : 0f, dt * 3f);
            // A staple about to snap trembles first.
            float fray = !v.Sealed && v.Light > 0f && v.Since > AdventureTuning.LeakHold * 0.5f ? 1f : 0f;
            // Seen only in light (leak.gdshader's leak_unseen): a lit crystal shows it too; its own glow and the water it
            // bends show only as much as it is seen, so nothing gives it away in the dark.
            float crystal = world.CrystalSees(v.Position, AdventureTuning.LeakArmLength(world.Menace));
            node.Arms.SetInstanceShaderParameter(SN.Of("crystal_seen"), crystal);
            node.Disc.SetInstanceShaderParameter(SN.Of("crystal_seen"), crystal);
            float seen = Mathf.Max(crystal, world.Aura.Shining ? 1f - Mathf.SmoothStep(world.Aura.Range * 0.8f, world.Aura.Range + 1.5f, d) : 0f);
            foreach (var (beam, _) in world.HerBeams)
                if (world.BeamLight(beam, v.Position, 0f, AdventureTuning.LeakDisc * 2f, v) > 0f) seen = 1f;
            node.Seen = Mathf.MoveToward(node.Seen, seen, dt * 3f);
            DrawArms(node, v, time);
            node.Arms.SetInstanceShaderParameter(SN.Of("state"), new Vector4(shimmer, node.Sealing, shut, v.Drunk ? 1f : 0f));
            node.Disc.SetInstanceShaderParameter(SN.Of("state"), new Vector4(shimmer, v.Glow, shut, v.Drunk ? 1f : 0f));
            node.Light.LightEnergy = (0.15f + 1.1f * shimmer) * (1f - shut) * node.Seen;
            SyncStaples(node, v, step, fray, shut, time);
            if (!v.Sealed && node.Seen > 0.02f && shown < _airs.Length) _airs[shown++] = new Vector3(v.Position.X, v.Position.Y, (1f - 0.7f * v.Glow) * node.Seen);
        }
        camera.SetLeaks(_airs, shown);
    }

    /// <summary>
    /// Her light-staples across its arms: each snaps in (a quick swell past its size) as the sim puts it in and out as it
    /// is pulled, trembling first; a bar across the arm at the middle of its stretch, as wide as the arm there and a little
    /// more. Sealed, they go with the arms.
    /// </summary>
    void SyncStaples(LeakNode node, PlaneLeak v, float step, float fray, float shut, float time)
    {
        int placed = v.Staples, per = Mathf.Max(v.StaplesPerArm, 1);
        for (int i = 0; i < node.Staples.Length; i++)
        {
            node.In[i] = Mathf.MoveToward(node.In[i], i < placed ? 1f : 0f, step);
            var staple = node.Staples[i];
            float k = node.In[i] * (1f - shut);
            staple.Visible = k > 0.01f;
            if (!staple.Visible) continue;
            var arm = v.Arms[i / per];
            int j = i % per;
            var a = arm.Points[j];
            var b = arm.Points[j + 1];
            var mid = (a + b) * 0.5f;
            var dir = b - a;
            float len = dir.Length();
            if (len < 1e-4f) continue;
            dir /= len;
            float along = (j + 0.5f) / per;
            float across = LeakArm.HalfWidth(along) * ArmDrawn * 2f + 0.14f;
            // In with a swell past its size; a last one in place trembles before it snaps.
            float swell = 1f + 0.35f * Mathf.Sin(Mathf.Clamp(node.In[i], 0f, 1f) * Mathf.Pi);
            float tremble = i == placed - 1 ? fray * 0.04f * Mathf.Sin(time * 60f + i) : 0f;
            var basis = new Basis(new Vector3(-dir.Y, 0f, dir.X) * across * swell, Vector3.Up, new Vector3(dir.X, 0f, dir.Y) * 0.09f * swell);
            float bob = Mathf.Sin(time * 0.8f + arm.Seed) * 0.06f * along;
            staple.Transform = new Transform3D(basis, new Vector3(mid.X + tremble, LevelMap.SwimBand + LeakHeight + bob + 0.02f, mid.Y));
            staple.Transparency = 1f - k;
        }
    }

    /// <summary>Her light's steam where it struck (CombatView.Steam): the place, the way the light went, how hard.</summary>
    public System.Action<Vector3, Vector3, float>? Steam;
    /// <summary>How hard a leak's sealing steams.</summary>
    const float LeakSteamSealed = 1f;

    /// <summary>The adventure's events: dots cleared go up in magic particles, a dot drawn to the boss flies to it, a leak struck steams.</summary>
    public void OnEvent(PlaneWorld world, in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        switch (e.Type)
        {
            case PlaneEventType.DotCleared:
                for (int i = 0; i < 3; i++) Rise(at, 0.5f, 0.9f);
                break;
            case PlaneEventType.DotDecayed:
                Rise(at, 0.5f, 0.9f);
                break;
            case PlaneEventType.DotGathered:
            {
                // Sucked in: straight for the boss, a dot from beyond the room coming in over its rim.
                var into = new Vector3(e.Direction.X, LevelMap.SwimBand, e.Direction.Y);
                var from = at;
                float rim = world.ArenaRadius + 2f;
                if (from.DistanceTo(into) > rim) from = into + (from - into).Normalized() * rim;
                float life = _rng.RandfRange(0.55f, 0.95f);
                _magic.Emit(from, (into - from) / life, life, _rng.RandfRange(0.14f, 0.22f), drag: 0f);
                break;
            }
            case PlaneEventType.DotEaten:
                for (int i = 0; i < 2; i++) Rise(at, 0.4f, 0.7f);
                break;
            case PlaneEventType.LeakUntied:
                Burst(at, 6);
                break;
            case PlaneEventType.LeakTied:
                Burst(at, 9);
                break;
            case PlaneEventType.LeakArmStruck:
                // Her light splashes off the arm.
                Steam?.Invoke(at + Vector3.Up * LeakHeight, new Vector3(e.Direction.X, 0f, e.Direction.Y), 0.5f);
                break;
            case PlaneEventType.LeakSealed:
            case PlaneEventType.LeakDrunk:
                Steam?.Invoke(at + Vector3.Up * LeakHeight, Vector3.Up, LeakSteamSealed);
                Burst(at, 40);
                break;
        }
    }

    void Rise(Vector3 at, float lifeMin, float lifeMax) =>
        _magic.Emit(at + new Vector3(_rng.RandfRange(-0.2f, 0.2f), 0f, _rng.RandfRange(-0.2f, 0.2f)),
            new Vector3(_rng.RandfRange(-0.6f, 0.6f), _rng.RandfRange(0.4f, 1.2f), _rng.RandfRange(-0.6f, 0.6f)), _rng.RandfRange(lifeMin, lifeMax), _rng.RandfRange(0.14f, 0.22f));

    /// <summary>A puff of magic particles flung out all round, stopping short in the water.</summary>
    void Burst(Vector3 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = _rng.RandfRange(0f, Mathf.Tau);
            var way = new Vector3(Mathf.Cos(a), _rng.RandfRange(-0.2f, 0.8f), Mathf.Sin(a)).Normalized();
            _magic.Emit(at, way * _rng.RandfRange(3f, 7f) + Vector3.Up * 0.4f, _rng.RandfRange(0.6f, 1.1f), _rng.RandfRange(0.14f, 0.26f), drag: 6f);
        }
    }
}
