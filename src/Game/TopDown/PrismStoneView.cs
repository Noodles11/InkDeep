using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The prism stones (PlanePrismStones.cs): each a great standing stone, a menhir, rough grey rock taller than her plane,
/// a little leaning, rounded at its top, with a crystal set in it at her plane (a band round it) and in its crown, both in
/// its colour: faint in the dark, so its colour can be read, burning bright while her light goes through it, a light of
/// its colour on what is round it. Its bed is ringed with pale pebbles on the sand, so she sees how far it can be pushed.
/// </summary>
public partial class PrismStoneView : Node3D
{
    const float DarkGlow = 0.35f, LitGlow = 3.5f, LitLight = 2.2f;

    sealed class Shown
    {
        public Node3D Root = null!;
        public StandardMaterial3D Crystal = null!;
        public OmniLight3D Light = null!;
        public float Lit;
    }

    readonly List<Shown> _shown = new();
    List<PrismStone>? _for;
    float _time;

    /// <summary>A menhir from its foot (y 0) up <paramref name="height"/>: rough rock, tapering a little, its top rounded.</summary>
    static ArrayMesh Menhir(float height, float seed)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int Around = 14, Rings = 12;
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 1e6f) + 3 };
        var lumps = new float[Around];
        for (int i = 0; i < Around; i++) lumps[i] = rng.RandfRange(0.88f, 1.12f);
        float squash = rng.RandfRange(0.72f, 0.85f);
        Vector3 At(int i, int j)
        {
            float v = j / (float)Rings;
            float a = i % Around * Mathf.Tau / Around;
            // A flattened round, broad at its foot, narrowing toward its rounded crown.
            float r = PrismStoneTuning.Radius * Mathf.Lerp(1.05f, 0.7f, v) * lumps[i % Around] * (v > 0.82f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((v - 0.82f) / 0.18f, 2f))) : 1f);
            float wobble = 1f + 0.05f * Mathf.Sin(v * 9f + i * 1.7f + seed * 10f);
            return new Vector3(Mathf.Cos(a) * r * wobble, v * height, Mathf.Sin(a) * r * wobble * squash);
        }
        for (int j = 0; j < Rings; j++)
            for (int i = 0; i < Around; i++)
            {
                Vector3 a = At(i, j), b = At(i + 1, j), c = At(i, j + 1), d = At(i + 1, j + 1);
                st.AddVertex(a); st.AddVertex(c); st.AddVertex(b);
                st.AddVertex(b); st.AddVertex(c); st.AddVertex(d);
            }
        st.GenerateNormals();
        return st.Commit();
    }

    void Build(PlaneWorld world)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        _shown.Clear();
        _for = world.PrismStones;
        var beds = new HashSet<System.Numerics.Vector2>();
        var pebble = new SphereMesh { Radius = 0.16f, Height = 0.14f, RadialSegments = 8, Rings = 4 };
        var pebbleMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.76f, 0.7f), Roughness = 0.9f };
        foreach (var s in world.PrismStones)
        {
            var colour = HueColor.Of(s.Hue);
            float floor = world.Map.HeightAt(s.Position);
            float height = Mathf.Max(LevelMap.SwimBand + 1.6f - floor, 2.4f);
            var root = new Node3D { Position = new Vector3(s.Position.X, floor, s.Position.Y) };
            var stoneMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.64f, 0.6f).Lerp(colour, 0.08f), Roughness = 0.9f };
            var lean = new Basis(new Vector3(1f, 0f, 0f), (s.Seed - 0.5f) * 0.14f).Rotated(Vector3.Up, s.Seed * Mathf.Tau);
            var body = new Node3D { Basis = lean };
            root.AddChild(body);
            body.AddChild(new MeshInstance3D { Mesh = Menhir(height, s.Seed), MaterialOverride = stoneMaterial });
            var crystal = new StandardMaterial3D
            {
                AlbedoColor = colour.Darkened(0.3f), Roughness = 0.1f, Metallic = 0.2f, EmissionEnabled = true, Emission = colour,
                EmissionEnergyMultiplier = DarkGlow, RimEnabled = true, Rim = 0.6f, RimTint = 0.7f,
            };
            // Its band at her plane, where her light goes through it, and its crown.
            float band = LevelMap.SwimBand + 0.3f - floor;
            body.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = PrismStoneTuning.Radius * 0.78f, OuterRadius = PrismStoneTuning.Radius * 1.02f, Rings = 18, RingSegments = 8 },
                MaterialOverride = crystal, Position = new Vector3(0f, band, 0f), Scale = new Vector3(1f, 0.9f, 0.82f),
            });
            body.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.32f, Height = 0.36f, RadialSegments = 6, Rings = 1 },
                MaterialOverride = crystal, Position = new Vector3(0f, height - 0.08f, 0f),
            });
            var light = new OmniLight3D
            {
                LightColor = colour, LightEnergy = 0f, OmniRange = 4.5f, OmniAttenuation = 1.4f, ShadowEnabled = false, Visible = false,
                Position = new Vector3(0f, band + 0.2f, 0f),
            };
            root.AddChild(light);
            AddChild(root);
            _shown.Add(new Shown { Root = root, Crystal = crystal, Light = light });
            // Its bed's ring of pebbles (once a bed).
            if (!beds.Add(s.Bed)) continue;
            int n = Mathf.CeilToInt(Mathf.Tau * PrismStoneTuning.Bed / 0.7f);
            var rng = new RandomNumberGenerator { Seed = (ulong)(s.Bed.X * 1000f + s.Bed.Y * 7f) };
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.Tau / n + rng.RandfRange(-0.05f, 0.05f);
                var p = s.Bed + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (PrismStoneTuning.Bed + rng.RandfRange(-0.1f, 0.15f));
                if (!world.Map.IsOpen(p)) continue;
                AddChild(new MeshInstance3D
                {
                    Mesh = pebble, MaterialOverride = pebbleMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(p.X, world.Map.HeightAt(p) + 0.03f, p.Y), Scale = Vector3.One * rng.RandfRange(0.7f, 1.3f),
                    Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
                });
            }
        }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        if (!ReferenceEquals(world.PrismStones, _for)) Build(world);
        _time += dt;
        for (int i = 0; i < _shown.Count && i < world.PrismStones.Count; i++)
        {
            var s = world.PrismStones[i];
            var v = _shown[i];
            // Pushed: it slides over the sand, its foot on it.
            v.Root.Position = new Vector3(s.Position.X, world.Map.HeightAt(s.Position), s.Position.Y);
            v.Lit = Mathf.MoveToward(v.Lit, s.Lit, dt * 4f);
            float breathe = 0.85f + 0.15f * Mathf.Sin(_time * 1.3f + s.Seed * 30f);
            v.Crystal.EmissionEnergyMultiplier = Mathf.Lerp(DarkGlow * breathe, LitGlow, v.Lit);
            v.Light.Visible = v.Lit > 0.01f;
            v.Light.LightEnergy = LitLight * v.Lit;
        }
    }
}
