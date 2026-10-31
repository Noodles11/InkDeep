using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The adventure on screen (docs/ADVENTURE.md), presentation only: the ink as tiny purple specks through the water's layers,
/// thicker the nearer its leak (ink_dot.gdshader, one MultiMesh); the leaks, X-shaped tears hanging in the water facing the
/// camera, hot air in the post pass with a purple shimmer running through them on a beat, tied arm by arm with golden
/// thread as she strikes them (leak.gdshader), the ties snapping again if she stops, and dissolving, thread and tear
/// alike, once sealed; dots her light clears go up in magic particles.
/// </summary>
public partial class AdventureView : Node3D
{
    /// <summary>A leak's quad across (metres), and how high over her plane it hangs.</summary>
    const float LeakSize = 3.2f, LeakHeight = 0.5f;
    /// <summary>A shimmer every this long; twice as often with her within Close. She has found it within Found.</summary>
    const float ShimmerSeconds = 3f, Close = 10f, Found = 12f;
    /// <summary>A tie pulls tight or snaps over this long; once sealed, it closes and dissolves over this long.</summary>
    const float TieSeconds = 0.35f, SealSeconds = 1.4f;

    sealed class LeakNode
    {
        public MeshInstance3D Quad = null!;
        public OmniLight3D Light = null!;
        /// <summary>How far each arm is tied; the shimmer's beat; how plainly it shows once found; seconds since it shut (negative: open).</summary>
        public Vector4 Ties;
        public float Beat, Seen, Shut = -1f;
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
    Shader _leakShader = null!;
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
            CustomAabb = new Aabb(new Vector3(-10f, -10f, -10f), new Vector3(LevelMap.Size + 20f, 30f, LevelMap.Size + 20f)),
        });
        _buffer = new float[AdventureTuning.MaxDots * Marks * MarkFloats];
        _magic = new MagicParticles(2400);
        AddChild(_magic);
        _leakShader = GD.Load<Shader>("res://assets/shaders/leak.gdshader");
        _rng.Seed = 7117;
    }

    /// <summary>A new level: its leaks.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var n in _leaks)
        {
            n.Quad.QueueFree();
            n.Light.QueueFree();
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
        var quad = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = Vector2.One * LeakSize },
            MaterialOverride = new ShaderMaterial { Shader = _leakShader },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = at,
            ExtraCullMargin = 4f,
        };
        quad.SetInstanceShaderParameter(SN.Of("seed"), _rng.RandfRange(0f, 50f));
        AddChild(quad);
        var light = new OmniLight3D { LightColor = new Color(0.6f, 0.25f, 0.95f), LightEnergy = 0f, OmniRange = 5f, ShadowEnabled = false, Position = at };
        AddChild(light);
        var node = new LeakNode { Quad = quad, Light = light, Beat = _rng.RandfRange(0f, 1f) };
        if (v.Sealed) node.Shut = SealSeconds;
        return node;
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
        float step = dt / TieSeconds;
        for (int k = 0; k < _leaks.Count && k < world.Leaks.Count; k++)
        {
            var v = world.Leaks[k];
            var node = _leaks[k];
            float d = System.Numerics.Vector2.Distance(v.Position, her);
            if (v.Sealed && node.Shut < 0f)
            {
                node.Shut = 0f;
                if (v.Drunk) node.Quad.SetInstanceShaderParameter(SN.Of("thread"), new Vector3(0.05f, 0.02f, 0.09f));
            }
            for (int arm = 0; arm < 4; arm++) node.Ties[arm] = Mathf.MoveToward(node.Ties[arm], v.Sealed || arm < v.Ties ? 1f : 0f, step);
            node.Quad.SetInstanceShaderParameter(SN.Of("ties"), new Vector4(node.Ties[0], node.Ties[1], node.Ties[2], node.Ties[3]));
            float shut = node.Shut < 0f ? 0f : Mathf.Clamp(node.Shut / SealSeconds, 0f, 1f);
            if (node.Shut >= 0f) node.Shut += dt;
            bool gone = shut >= 1f;
            node.Quad.Visible = !gone;
            node.Light.Visible = !gone;
            if (gone) continue;
            if (d < Found) node.Seen = 1f;
            node.Beat = Mathf.PosMod(node.Beat + dt / ShimmerSeconds * (d < Close ? 2f : 1f), 1f);
            // A short swell, then quiet.
            float shimmer = Mathf.Pow(Mathf.Max(Mathf.Sin(node.Beat * Mathf.Tau), 0f), 6f);
            // A tie about to snap trembles first.
            float fray = !v.Sealed && v.Hits > 0 && v.Since > AdventureTuning.LeakHold * 0.5f ? 1f : 0f;
            node.Quad.SetInstanceShaderParameter(SN.Of("state"), new Vector4(shimmer, node.Seen, shut, fray));
            node.Light.LightEnergy = (0.15f + 1.1f * shimmer) * (1f - shut);
            if (!v.Sealed && shown < _airs.Length) _airs[shown++] = new Vector3(v.Position.X, v.Position.Y, 1f - 0.7f * v.Glow);
        }
        camera.SetLeaks(_airs, shown);
    }

    /// <summary>Her light's steam where it struck (CombatView.Steam): the place, the way the light went, the body's radius, how hard.</summary>
    public System.Action<Vector3, Vector3, float, float>? Steam;
    /// <summary>A leak's width for the steam, how hard a hit on it is, and how hard its sealing.</summary>
    const float LeakSteamRadius = 0.6f, LeakSteamHit = 0.3f, LeakSteamSealed = 1f;

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
            case PlaneEventType.LeakLit:
                Steam?.Invoke(at + Vector3.Up * LeakHeight, Vector3.Up, LeakSteamRadius, LeakSteamHit);
                break;
            case PlaneEventType.LeakUntied:
                Burst(at, 12);
                break;
            case PlaneEventType.LeakTied:
                Burst(at, 18);
                break;
            case PlaneEventType.LeakSealed:
            case PlaneEventType.LeakDrunk:
                Steam?.Invoke(at + Vector3.Up * LeakHeight, Vector3.Up, LeakSteamRadius, LeakSteamSealed);
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
