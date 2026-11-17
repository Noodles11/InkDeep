using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The inkseeds on screen (docs/CORRUPTION.md, PlaneInkseeds.cs). Each is a small black hole (inkseed.gdshader, and the
/// murklings' bent water round it, murk_hole), drawn only while her light is on it; its core a dull ember, glowing a
/// deepening red-violet as it swells and flickering faster toward its end. Unseen or not, within SeedSense of her it beats
/// like a heart: two quick rings of the water's shimmer, the nearer she is the quicker (BeatFar to BeatNear seconds apart),
/// quicker still as it swells. Freed, it implodes as a murkling does; bursting, the strongest ring the water shows and a
/// spray of black.
/// </summary>
public partial class BlightView
{
    /// <summary>Its heartbeat: heard within SeedSense of her, a beat every BeatFar seconds there and BeatNear at BeatClose metres; the second beat BeatGap after the first.</summary>
    public const float SeedSense = 14f, BeatFar = 1.6f, BeatNear = 0.35f, BeatClose = 2f, BeatGap = 0.16f;

    sealed class SeedLook
    {
        public MeshInstance3D Body = null!, Lens = null!;
        public float Shown, Beat, Second = -1f, Seed;
    }

    readonly Dictionary<Inkseed, SeedLook> _seeds = new();
    readonly List<Inkseed> _seedsGone = new();
    ShaderMaterial? _seedMat;

    void ClearInkseeds()
    {
        foreach (var look in _seeds.Values)
        {
            look.Body.QueueFree();
            look.Lens.QueueFree();
        }
        _seeds.Clear();
    }

    void SyncInkseeds(PlaneWorld world, float dt)
    {
        _seedsGone.Clear();
        foreach (var seed in _seeds.Keys)
            if (!world.Inkseeds.Contains(seed)) _seedsGone.Add(seed);
        foreach (var seed in _seedsGone)
        {
            _seeds[seed].Body.QueueFree();
            _seeds[seed].Lens.QueueFree();
            _seeds.Remove(seed);
        }
        var her = world.Player.Position;
        var lightCol = OctoShoots.Game.Fx.LightColour.Beam(LightHue.None, false);
        foreach (var seed in world.Inkseeds)
        {
            if (!_seeds.TryGetValue(seed, out var look))
            {
                _seedMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/inkseed.gdshader") };
                _lensMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/murk_hole.gdshader") };
                look = new SeedLook
                {
                    Body = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _seedMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false },
                    Lens = new MeshInstance3D { Mesh = _lensDisc, MaterialOverride = _lensMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false },
                    Seed = _seeds.Count * 7.31f % 100f,
                    Beat = (float)GD.RandRange(0.0, BeatFar),
                };
                AddChild(look.Lens);
                AddChild(look.Body);
                _seeds[seed] = look;
            }
            float d = System.Numerics.Vector2.Distance(seed.Position, her);
            // Seen only while her light is on it (none within a stain: the sim's Lit knows).
            look.Shown = Mathf.MoveToward(look.Shown, seed.Lit ? 1f : 0f, dt * (seed.Lit ? 6f : 2.5f));
            var at = new Vector3(seed.Position.X, LevelMap.SwimBand + 0.06f * Mathf.Sin(_time * 1.7f + look.Seed), seed.Position.Y);
            // (It shivers as it nears its end.)
            float shiver = seed.Armed ? 0.03f * seed.Swell * seed.Swell * Mathf.Sin(_time * (30f + 50f * seed.Swell)) : 0f;
            look.Body.Visible = look.Shown > 0.01f;
            look.Body.Position = at;
            look.Body.Scale = Vector3.One * seed.Radius * (1f + shiver);
            look.Body.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("shown"), look.Shown);
            look.Body.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("swell"), seed.Armed ? seed.Swell : -1f);
            look.Body.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ring_color"), new Vector3(lightCol.R, lightCol.G, lightCol.B));
            look.Body.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), look.Seed);
            look.Lens.Visible = look.Body.Visible;
            if (look.Lens.Visible)
            {
                look.Lens.Position = at;
                look.Lens.Scale = Vector3.One * seed.Radius * LensReach;
                look.Lens.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("shown"), look.Shown);
                look.Lens.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), look.Seed);
            }
            // Its heartbeat, lit or not, near her: two quick rings, the nearer the quicker, quicker still as it swells.
            if (world.Defeated || d > SeedSense)
            {
                look.Second = -1f;
                continue;
            }
            look.Beat -= dt;
            if (look.Beat <= 0f)
            {
                float near = 1f - Mathf.Clamp((d - BeatClose) / (SeedSense - BeatClose), 0f, 1f);
                float every = Mathf.Lerp(BeatFar, BeatNear, near * near) * (seed.Armed ? Mathf.Lerp(1f, 0.3f, seed.Swell) : 1f);
                look.Beat = every;
                look.Second = Mathf.Min(BeatGap, every * 0.4f);
                Beat(seed, 1f);
            }
            if (look.Second > 0f)
            {
                look.Second -= dt;
                if (look.Second <= 0f) Beat(seed, 0.7f);
            }
        }
    }

    /// <summary>One beat of a seed's heart: a ring of the water's shimmer out from it.</summary>
    void Beat(Inkseed seed, float strength) =>
        Swells?.AddRing(new Vector2(seed.Position.X, seed.Position.Y), strength * (seed.Armed ? 0.8f + 0.5f * seed.Swell : 0.65f),
            (seed.Radius * 3f + 0.9f) / 2.6f, 0.5f, snap: 3f);

    /// <summary>A seed's moments: woken (a small start of the water), freed (it implodes), bursting.</summary>
    void InkseedEvent(in PlaneEvent e, Vector3 at)
    {
        var flat = new Vector2(e.Position.X, e.Position.Y);
        switch (e.Type)
        {
            case PlaneEventType.InkseedWoke:
                Swells?.AddInward(flat, 0.9f, 1.4f / 2.6f, 0.35f);
                break;
            case PlaneEventType.InkseedFreed:
                Implode(at, e.Size);
                break;
            case PlaneEventType.InkseedBurst:
                // The strongest ring the water shows, and the ink thrown out with it (its darkness is the post pass's).
                Swells?.AddRing(flat, 2f, InkseedTuning.Reach / 2.6f, 0.9f, snap: 3f);
                Swells?.AddRing(flat, 1.2f, InkseedTuning.Reach * 0.6f / 2.6f, 1.4f, snap: 2f);
                for (int k = 0; k < 40; k++)
                {
                    float a = k * Mathf.Tau / 40f + _droplets.RandRange(-0.08f, 0.08f);
                    var dir = new Vector3(Mathf.Cos(a), _droplets.RandRange(-0.1f, 0.35f), Mathf.Sin(a));
                    float size = _droplets.RandRange(0.08f, 0.16f);
                    _droplets.Emit(at, dir * _droplets.RandRange(5f, 9f), _droplets.RandRange(0.35f, 0.6f), size, size * 0.3f,
                        new Color(0.006f, 0.003f, 0.012f, 0.95f), new Color(0.02f, 0.01f, 0.04f, 0f), drag: 4f);
                }
                break;
        }
    }
}
