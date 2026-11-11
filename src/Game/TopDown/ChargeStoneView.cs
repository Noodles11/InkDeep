using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// the charge stones in the walls (PlaneChargeStones.cs): each a little cluster of hexagonal shards set in the rock's face
/// at her plane, leaning out of it, in its own colour. Dark, a faint glint in it so it can be found; charged, its shards
/// burn in their colour and a soft round light spreads from it over the reef (ChargeStoneTuning.ShownReach), fainter than
/// hers, dimming as its charge runs out. A level's trio (ChargeTrio) has its sign on the sand of the nook between them:
/// three shards of its colour lying in a triangle, each pointing to its stone and burning while it glows; once the
/// cache has burst up there, they are spent and dark.
/// </summary>
public partial class ChargeStoneView : Node3D
{
    const float DarkGlint = 0.18f, LitEmission = 3.2f, LightEnergy = 1.3f;

    sealed class Shown
    {
        public StandardMaterial3D Material = null!;
        public OmniLight3D Light = null!;
        public float Glow;
    }

    readonly List<Shown> _shown = new();
    /// <summary>The trio's sign: each shard's material, with the stone it stands for.</summary>
    readonly List<(StandardMaterial3D Material, int Stone)> _sign = new();
    List<ChargeStone>? _for;
    static ArrayMesh? _shard;
    float _time;

    /// <summary>One shard: a six-sided prism with a pointed tip, along +Y, its foot at the origin.</summary>
    static ArrayMesh Shard()
    {
        if (_shard is not null) return _shard;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int Sides = 6;
        const float R = 0.11f, Body = 0.5f, Tip = 0.72f;
        Vector3 Ring(int i, float y) => new(Mathf.Cos(i * Mathf.Tau / Sides) * R, y, Mathf.Sin(i * Mathf.Tau / Sides) * R);
        for (int i = 0; i < Sides; i++)
        {
            Vector3 a0 = Ring(i, 0f), a1 = Ring(i + 1, 0f), b0 = Ring(i, Body), b1 = Ring(i + 1, Body), tip = new(0f, Tip, 0f);
            // A side face, then its facet to the tip (flat, as cut crystal is).
            var n = (a0 + a1).Normalized();
            st.SetNormal(n); st.AddVertex(a0); st.SetNormal(n); st.AddVertex(b1); st.SetNormal(n); st.AddVertex(a1);
            st.SetNormal(n); st.AddVertex(a0); st.SetNormal(n); st.AddVertex(b0); st.SetNormal(n); st.AddVertex(b1);
            var f = (b1 - b0).Cross(tip - b0).Normalized();
            if (f.Dot(n) < 0f) f = -f;
            st.SetNormal(f); st.AddVertex(b0); st.SetNormal(f); st.AddVertex(tip); st.SetNormal(f); st.AddVertex(b1);
        }
        return _shard = st.Commit();
    }

    void Build(PlaneWorld world)
    {
        var crystals = world.ChargeStones;
        foreach (var child in GetChildren()) child.QueueFree();
        _shown.Clear();
        _sign.Clear();
        _for = crystals;
        var shard = Shard();
        foreach (var c in crystals)
        {
            var colour = new Color(ChargeStoneTuning.Colours[c.Colour].X, ChargeStoneTuning.Colours[c.Colour].Y, ChargeStoneTuning.Colours[c.Colour].Z);
            var material = new StandardMaterial3D
            {
                AlbedoColor = colour.Darkened(0.55f), Roughness = 0.12f, Metallic = 0.25f, EmissionEnabled = true, Emission = colour,
                EmissionEnergyMultiplier = DarkGlint, RimEnabled = true, Rim = 0.7f, RimTint = 0.6f,
            };
            var face = new Vector3(c.Face.X, LevelMap.SwimBand - 0.1f, c.Face.Y);
            var outward = new Vector3(c.Out.X, 0f, c.Out.Y);
            var rng = new RandomNumberGenerator { Seed = (ulong)(c.Seed * 1e6f) + 7 };
            int shards = 3 + rng.RandiRange(0, 2);
            for (int k = 0; k < shards; k++)
            {
                // Leaning out of the wall and up, splayed a little, each its own size.
                var lean = (outward * rng.RandfRange(0.5f, 1f) + Vector3.Up * rng.RandfRange(0.4f, 1.1f)
                    + new Vector3(rng.RandfRange(-0.6f, 0.6f), 0f, rng.RandfRange(-0.6f, 0.6f))).Normalized();
                var basis = new Basis(new Quaternion(Vector3.Up, lean)).Scaled(Vector3.One * rng.RandfRange(1.0f, 1.9f));
                var at = face + new Vector3(rng.RandfRange(-0.18f, 0.18f), rng.RandfRange(-0.15f, 0.2f), rng.RandfRange(-0.18f, 0.18f));
                AddChild(new MeshInstance3D { Mesh = shard, MaterialOverride = material, Transform = new Transform3D(basis, at) });
            }
            var light = new OmniLight3D
            {
                LightColor = colour, LightEnergy = 0f, OmniRange = ChargeStoneTuning.ShownReach, OmniAttenuation = 1.6f, ShadowEnabled = false,
                LightSpecular = 0.3f, Position = new Vector3(c.Position.X, LevelMap.SwimBand + 0.2f, c.Position.Y), Visible = false,
            };
            AddChild(light);
            _shown.Add(new Shown { Material = material, Light = light });
        }
        if (world.Trio is { } trio)
            for (int i = 0; i < crystals.Count; i++)
            {
                var c = crystals[i];
                if (c.Trio < 0) continue;
                var colour = new Color(ChargeStoneTuning.Colours[c.Colour].X, ChargeStoneTuning.Colours[c.Colour].Y, ChargeStoneTuning.Colours[c.Colour].Z);
                var material = new StandardMaterial3D
                {
                    AlbedoColor = colour.Darkened(0.55f), Roughness = 0.12f, Metallic = 0.25f, EmissionEnabled = true, Emission = colour,
                    EmissionEnergyMultiplier = DarkGlint, RimEnabled = true, Rim = 0.7f, RimTint = 0.6f,
                };
                // Lying on the sand a little out from the nook's middle, pointing to its stone.
                var to = System.Numerics.Vector2.Normalize(c.Position - trio.Middle);
                var at = trio.Middle + to * 0.55f;
                var point = new Vector3(to.X, 0f, to.Y);
                var basis = new Basis(new Quaternion(Vector3.Up, point)).Scaled(Vector3.One * 1.4f);
                AddChild(new MeshInstance3D { Mesh = shard, MaterialOverride = material, Transform = new Transform3D(basis, new Vector3(at.X, world.Map.HeightAt(at) + 0.12f, at.Y)) });
                _sign.Add((material, i));
            }
    }

    public void Sync(PlaneWorld world, float dt)
    {
        if (!ReferenceEquals(world.ChargeStones, _for)) Build(world);
        _time += dt;
        for (int i = 0; i < _shown.Count && i < world.ChargeStones.Count; i++)
        {
            var c = world.ChargeStones[i];
            var s = _shown[i];
            // Easing to the sim's glow, so its light swells rather than snaps on.
            s.Glow = Mathf.MoveToward(s.Glow, c.Glow, dt * 3f);
            float flicker = 0.92f + 0.08f * Mathf.Sin(_time * 5.3f + c.Seed * 40f);
            float glint = DarkGlint * (0.7f + 0.3f * Mathf.Sin(_time * 1.7f + c.Seed * 20f));
            s.Material.EmissionEnergyMultiplier = Mathf.Lerp(glint, LitEmission * flicker, s.Glow);
            s.Light.Visible = s.Glow > 0.01f;
            s.Light.LightEnergy = LightEnergy * s.Glow * flicker;
            // Charged in a colour of hers, it glows in that colour (and lends it: docs/LIGHT-AND-GRAPH.md §10).
            var own = new Color(ChargeStoneTuning.Colours[c.Colour].X, ChargeStoneTuning.Colours[c.Colour].Y, ChargeStoneTuning.Colours[c.Colour].Z);
            var shown = c.Lent != LightHue.None ? OctoShoots.Game.Fx.LightColour.Of(c.Lent) : own;
            s.Material.Emission = shown;
            s.Light.LightColor = shown;
        }
        bool spent = world.Trio is { Opened: true };
        foreach (var (material, stone) in _sign)
        {
            if (stone >= _shown.Count) continue;
            float glint = DarkGlint * (0.7f + 0.3f * Mathf.Sin(_time * 1.7f + stone));
            material.EmissionEnergyMultiplier = spent ? 0.05f : Mathf.Lerp(glint, LitEmission, _shown[stone].Glow);
        }
    }
}
