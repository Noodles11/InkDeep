using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The biomes' hazards on screen (PlaneHazards.cs). The Shipwreck's debris: where a timber will land, a dark shadow
/// grows on the water, its edge a faint pale ring so it reads in the dark, and the timber falls into it from above, lands
/// across her plane and sinks to the floor. The Ruins' dart traps: a carved stone face set in the wall, a dark slot
/// at its mouth that glints as it winds up; its darts thin bronze shafts. (The tangles of the Kelp Forest are its kelp.)
/// </summary>
public partial class HazardView : Node3D
{
    sealed class Fall
    {
        public PlaneDebris Sim = null!;
        public MeshInstance3D Shadow = null!, Ring = null!;
        public Node3D Timber = null!;
        public StandardMaterial3D ShadowMat = null!, RingMat = null!;
    }

    sealed class Trap
    {
        public PlaneDartTrap Sim = null!;
        public Node3D Root = null!;
        public OmniLight3D Glint = null!;
        public StandardMaterial3D Mouth = null!;
    }

    readonly List<Fall> _falls = new();
    readonly List<Trap> _traps = new();
    readonly List<MeshInstance3D> _darts = new();
    /// <summary>The Kelp Forest's curtains: each its strands (and how far they stand, 1 shut, 0 parted) and its faint glow.</summary>
    readonly List<(PlaneKelpCurtain Sim, Node3D Strands, StandardMaterial3D Glow, float Up)> _curtains = new();
    static readonly SphereMesh BallMesh = new() { Radius = HazardTuning.BallRadius, Height = HazardTuning.BallRadius * 2f, RadialSegments = 12, Rings = 6 };
    static readonly CylinderMesh DartMesh = new() { TopRadius = 0.035f, BottomRadius = 0.05f, Height = 0.6f, RadialSegments = 6, Rings = 1 };
    static readonly StandardMaterial3D Iron = new() { AlbedoColor = new Color(0.22f, 0.2f, 0.19f), Metallic = 0.6f, Roughness = 0.55f };
    static readonly StandardMaterial3D Rust = new() { AlbedoColor = new Color(0.45f, 0.22f, 0.12f), Roughness = 0.9f };
    static readonly StandardMaterial3D KelpGreen = new() { AlbedoColor = new Color(0.32f, 0.42f, 0.18f), Roughness = 0.8f };
    static readonly StandardMaterial3D BallGlow = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.75f, 0.55f, 0.4f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
    LevelMap? _map;

    static readonly StandardMaterial3D Wood = new() { AlbedoColor = new Color(0.36f, 0.25f, 0.16f), Roughness = 0.9f };
    static readonly StandardMaterial3D Stone = new() { AlbedoColor = new Color(0.62f, 0.6f, 0.55f), Roughness = 0.95f };
    static readonly StandardMaterial3D Slot = new() { AlbedoColor = new Color(0.05f, 0.05f, 0.06f), Roughness = 1f };
    // (A dart, and a trap's mouth, are drawn after the ink pass with a faint light of their own: a hazard must show in
    // the dark, as a vault does.)
    static readonly StandardMaterial3D Bronze = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.95f, 0.78f, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
    static readonly Texture2D Soft = SoftDisc();

    static Texture2D SoftDisc()
    {
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 1f, 1f, 1f));
        g.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 64, Height = 64 };
    }

    public void Sync(PlaneWorld world)
    {
        if (!ReferenceEquals(world.Map, _map)) Reset(world);
        // Debris: each falling or lying timber.
        foreach (var d in world.Debris)
            if (_falls.Find(f => f.Sim == d) is null) _falls.Add(MakeFall(world.Map, d));
        for (int i = _falls.Count - 1; i >= 0; i--)
        {
            var f = _falls[i];
            if (!world.Debris.Contains(f.Sim))
            {
                f.Shadow.QueueFree();
                f.Ring.QueueFree();
                f.Timber.QueueFree();
                _falls.RemoveAt(i);
                continue;
            }
            float k = f.Sim.Landed ? 1f : 1f - f.Sim.Timer / HazardTuning.DebrisWarn;
            float r = HazardTuning.DebrisRadius * Mathf.Lerp(0.5f, 1.1f, k);
            f.Shadow.Scale = new Vector3(r * 2f, 1f, r * 2f);
            f.ShadowMat.AlbedoColor = new Color(0.02f, 0.02f, 0.03f, (f.Sim.Landed ? 0.45f * (1f - f.Sim.Lain / 4f) : 0.25f + 0.5f * k));
            f.RingMat.AlbedoColor = new Color(0.9f, 0.85f, 0.75f, f.Sim.Landed ? 0f : 0.3f + 0.45f * k);
            f.Ring.Scale = new Vector3(HazardTuning.DebrisRadius * 2.2f, 1f, HazardTuning.DebrisRadius * 2.2f);
            // The timber: out of sight above till its last half second, then down onto her plane, then sinking.
            float plane = LevelMap.SwimBand, floor = world.Map.HeightAt(f.Sim.Position);
            float y = f.Sim.Landed ? Mathf.Lerp(plane, floor + 0.15f, Mathf.Clamp(f.Sim.Lain / 1.5f, 0f, 1f))
                : plane + 9f * Mathf.Pow(Mathf.Clamp(f.Sim.Timer / 0.5f, 0f, 1f), 2f);
            f.Timber.Visible = f.Sim.Landed || f.Sim.Timer < 0.5f;
            f.Timber.Position = new Vector3(f.Sim.Position.X, y, f.Sim.Position.Y);
        }
        // Dart traps: a glint as one winds up. (An open level's come and go with their chunks, and keep their place as its window moves on.)
        for (int i = _traps.Count - 1; i >= 0; i--)
            if (!world.DartTraps.Contains(_traps[i].Sim))
            {
                _traps[i].Root.QueueFree();
                _traps.RemoveAt(i);
            }
        foreach (var d in world.DartTraps)
            if (!_traps.Exists(t => t.Sim == d)) _traps.Add(MakeTrap(d));
        foreach (var t in _traps)
        {
            t.Root.Position = new Vector3(t.Sim.Position.X, LevelMap.SwimBand, t.Sim.Position.Y);
            bool armed = t.Sim.Windup > 0f;
            t.Glint.LightEnergy = armed ? 2.2f : 0f;
            t.Mouth.AlbedoColor = armed ? new Color(1f, 0.85f, 0.5f, 0.95f) : new Color(0.8f, 0.77f, 0.7f, t.Sim.Rest > 0f ? 0.2f : 0.5f);
        }
        // Darts.
        while (_darts.Count < world.Darts.Count)
        {
            var m = new MeshInstance3D { Mesh = DartMesh, MaterialOverride = Bronze, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(m);
            _darts.Add(m);
        }
        // The curtains: their strands sink aside while parted, and stand again as they close.
        for (int i = 0; i < _curtains.Count; i++)
        {
            var c = _curtains[i];
            float up = Mathf.MoveToward(c.Up, c.Sim.Open ? 0f : 1f, (float)GetProcessDeltaTime() * (c.Sim.Open ? 4f : 1.5f));
            c.Strands.Scale = new Vector3(1f, Mathf.Max(up, 0.05f), 1f);
            float lit = Mathf.Clamp(c.Sim.Light / HazardTuning.CurtainBurn, 0f, 1f);
            c.Glow.AlbedoColor = new Color(0.55f, 0.85f, 0.45f, (0.12f + 0.3f * lit) * up);
            _curtains[i] = (c.Sim, c.Strands, c.Glow, up);
        }
        for (int i = 0; i < _darts.Count; i++)
        {
            bool on = i < world.Darts.Count;
            _darts[i].Visible = on;
            if (!on) continue;
            var d = world.Darts[i];
            _darts[i].Mesh = d.Ball ? BallMesh : DartMesh;
            _darts[i].MaterialOverride = d.Ball ? BallGlow : Bronze;
            var way = new Vector3(d.Velocity.X, 0f, d.Velocity.Y).Normalized();
            _darts[i].Position = new Vector3(d.Position.X, LevelMap.SwimBand, d.Position.Y);
            _darts[i].Basis = new Basis(new Quaternion(Vector3.Up, way));
        }
    }

    /// <summary>The curtains drawn again where they stand (an open level's come and go with their chunks).</summary>
    public void ShowCurtains(PlaneWorld world)
    {
        if (!ReferenceEquals(world.Map, _map)) return;
        foreach (var c in _curtains) c.Strands.GetParent().QueueFree();
        _curtains.Clear();
        foreach (var c in world.Curtains) _curtains.Add(MakeCurtain(c));
    }

    void Reset(PlaneWorld world)
    {
        _falls.Clear();
        foreach (Node n in GetChildren()) n.QueueFree();
        _traps.Clear();
        _darts.Clear();
        _map = world.Map;
        _curtains.Clear();
        foreach (var c in world.Curtains) _curtains.Add(MakeCurtain(c));
    }

    /// <summary>A trap: a ship's cannon (the Shipwreck) or a carved dart trap (the Ruins), against its wall.</summary>
    Trap MakeTrap(PlaneDartTrap t)
    {
        var root = new Node3D { Position = new Vector3(t.Position.X, LevelMap.SwimBand, t.Position.Y) };
        // (Its front, local −z, toward the water it faces.)
        root.Basis = new Basis(Vector3.Up, Mathf.Atan2(-t.Facing.X, -t.Facing.Y));
        if (t.Cannon)
        {
            // A ship's cannon fallen against the rock, its rusted barrel toward the water on a broken carriage.
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.35f, 1.1f) }, MaterialOverride = Rust, Position = new Vector3(0f, -0.25f, 0.3f) });
            root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.24f, Height = 1.8f, RadialSegments = 14, Rings = 1 }, MaterialOverride = Iron, Rotation = new Vector3(-Mathf.Pi / 2f, 0f, 0f), Position = new Vector3(0f, 0f, -0.1f) });
            var muzzle = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoTexture = Soft, AlbedoColor = new Color(1f, 1f, 1f, 0.3f) };
            root.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(1.5f, 1.5f) }, MaterialOverride = muzzle, Position = new Vector3(0f, 0.5f, -1f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            var flash = new OmniLight3D { LightColor = new Color(1f, 0.7f, 0.4f), LightEnergy = 0f, OmniRange = 3f, ShadowEnabled = false, Position = new Vector3(0f, 0.3f, -1.2f) };
            root.AddChild(flash);
            AddChild(root);
            return new Trap { Sim = t, Root = root, Glint = flash, Mouth = muzzle };
        }
        // A carved face of pale stone pressed into the wall, its slot toward the water.
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 1.1f, 0.5f) }, MaterialOverride = Stone, Position = new Vector3(0f, 0f, 0.15f) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.26f, 0.14f, 0.1f) }, MaterialOverride = Slot, Position = new Vector3(0f, 0.15f, -0.12f) });
        // Its mouth's faint pale outline, seen in the dark; it flares warm as the trap winds up.
        var mouth = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoTexture = Soft, AlbedoColor = new Color(1f, 1f, 1f, 0.3f) };
        root.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(1.4f, 1.4f) }, MaterialOverride = mouth, Position = new Vector3(0f, 0.6f, -0.2f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var glint = new OmniLight3D { LightColor = new Color(1f, 0.85f, 0.55f), LightEnergy = 0f, OmniRange = 2.2f, ShadowEnabled = false, Position = new Vector3(0f, 0.3f, -0.5f) };
        root.AddChild(glint);
        AddChild(root);
        return new Trap { Sim = t, Root = root, Glint = glint, Mouth = mouth };
    }

    /// <summary>A kelp curtain: a close row of tall strands across its passage, a faint green glow along it (seen in the dark).</summary>
    (PlaneKelpCurtain, Node3D, StandardMaterial3D, float) MakeCurtain(PlaneKelpCurtain c)
    {
        var r = c.Rock;
        var across = new Vector3(-r.Facing.Y, 0f, r.Facing.X);
        var mid = new Vector3(r.Center.X, 0f, r.Center.Y);
        float floor = _map!.HeightAt(r.Center);
        // (Its node on the seabed: the strands stand from it, and shrink back down to it as they part.)
        var strands = new Node3D { Position = mid + Vector3.Up * floor };
        var rng = new RandomNumberGenerator { Seed = (ulong)(r.Center.X * 1000f + r.Center.Y) };
        for (float t = -r.Radius; t <= r.Radius; t += 0.32f)
        {
            float h = LevelMap.SwimBand - floor + 1.2f + rng.Randf() * 0.8f;
            var at = across * t + new Vector3(r.Facing.X, 0f, r.Facing.Y) * rng.RandfRange(-0.3f, 0.3f);
            strands.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.06f, h, 0.22f) }, MaterialOverride = KelpGreen,
                Position = at + Vector3.Up * (h * 0.5f), Rotation = new Vector3(rng.RandfRange(-0.15f, 0.15f), rng.Randf() * Mathf.Tau, rng.RandfRange(-0.15f, 0.15f)),
            });
        }
        // (Scaled from the floor up: its strands stand on the seabed.)
        var holder = new Node3D { Position = new Vector3(0f, 0f, 0f) };
        AddChild(holder);
        holder.AddChild(strands);
        var glow = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoTexture = Soft, AlbedoColor = new Color(0.55f, 0.85f, 0.45f, 0.12f) };
        var band = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(r.Radius * 2.2f, 1.6f) }, MaterialOverride = glow, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = mid + Vector3.Up * (LevelMap.SwimBand + 0.1f), Basis = new Basis(Vector3.Up, Mathf.Atan2(-across.Z, across.X)),
        };
        holder.AddChild(band);
        return (c, strands, glow, 1f);
    }

    Fall MakeFall(LevelMap map, PlaneDebris d)
    {
        float y = LevelMap.SwimBand - 0.05f;
        var shadowMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = Soft, AlbedoColor = new Color(0f, 0f, 0f, 0f) };
        var ringMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(1f, 1f, 1f, 0f) };
        var shadow = new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = shadowMat, Position = new Vector3(d.Position.X, y, d.Position.Y), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.45f, OuterRadius = 0.5f, Rings = 40, RingSegments = 4 }, MaterialOverride = ringMat, Position = new Vector3(d.Position.X, y + 0.02f, d.Position.Y), Scale = new Vector3(1f, 0.05f, 1f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        // A broken timber from the wreck above: a long beam, a short splintered piece across its end.
        var timber = new Node3D { Visible = false, Rotation = new Vector3(0f, d.Yaw, 0f) };
        timber.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2.3f, 0.26f, 0.38f) }, MaterialOverride = Wood });
        timber.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.36f, 0.22f, 0.9f) }, MaterialOverride = Wood, Position = new Vector3(0.95f, 0.1f, 0.1f), Rotation = new Vector3(0f, 0.3f, 0.1f) });
        AddChild(shadow);
        AddChild(ring);
        AddChild(timber);
        return new Fall { Sim = d, Shadow = shadow, Ring = ring, Timber = timber, ShadowMat = shadowMat, RingMat = ringMat };
    }
}
