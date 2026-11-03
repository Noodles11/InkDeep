using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A cave's wall ash on screen (docs/ADVENTURE.md §8; the sim's PlaneAsh.cs): each patch a few scraps of washed-purple ash
/// stuck flat to the rock like torn wallpaper (ash_flake.gdshader, one MultiMesh), seen only where light falls on them. As
/// her searchlight wears a patch away its scraps go one by one, each peeling off in a twirl of magic particles (the
/// pearls' "+" and "*") and a flake of ash drifting down and fading.
/// </summary>
public partial class AshView : Node3D
{
    const int PerPatch = 10;
    /// <summary>Scraps peeled this frame at most (a seal's wave clears many at once).</summary>
    const int PeelBudget = 70;

    static readonly Color[] Washed =
    {
        new(0.62f, 0.46f, 0.86f), new(0.5f, 0.37f, 0.74f), new(0.74f, 0.6f, 0.92f), new(0.44f, 0.32f, 0.64f),
    };

    MultiMesh _multimesh = null!;
    MagicParticles _magic = null!;
    FxParticles _flakes = null!;
    readonly RandomNumberGenerator _rng = new();
    Transform3D[] _at = System.Array.Empty<Transform3D>();
    float[] _threshold = System.Array.Empty<float>(), _shown = System.Array.Empty<float>();
    bool[] _on = System.Array.Empty<bool>();

    public override void _Ready()
    {
        _multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/ash_flake.gdshader") } },
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = _multimesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-10, -40, -10), new Vector3(LevelMap.Size + 20, 80, LevelMap.Size + 20)),
        });
        _magic = new MagicParticles(1024);
        AddChild(_magic);
        _flakes = new FxParticles(512, additive: false);
        AddChild(_flakes);
    }

    /// <summary>A new level: its walls' ash laid out (none outside caves).</summary>
    public void Show(PlaneWorld world)
    {
        _magic.Clear();
        _flakes.Clear();
        int n = world.Ash.Count * PerPatch;
        _multimesh.InstanceCount = n;
        _at = new Transform3D[n];
        _threshold = new float[n];
        _shown = new float[world.Ash.Count];
        _on = new bool[n];
        _rng.Seed = world.Map.Seed ^ 0xA54UL;
        for (int p = 0; p < world.Ash.Count; p++)
        {
            var a = world.Ash[p];
            var normal = new Vector3(a.Normal.X, 0f, a.Normal.Y);
            var along = new Vector3(-a.Normal.Y, 0f, a.Normal.X);
            var wall = new Vector3(a.Wall.X, LevelMap.SwimBand + a.Height, a.Wall.Y);
            _shown[p] = a.Cover;
            for (int k = 0; k < PerPatch; k++)
            {
                int i = p * PerPatch + k;
                // Stuck flat to the rock, turned any way, tipped a little off it as a torn scrap lifts at its edges.
                float spin = _rng.RandfRange(0f, Mathf.Tau);
                var face = new Basis(normal, spin) * new Basis(along, Vector3.Up, normal);
                face = new Basis(along, _rng.RandfRange(-0.3f, 0.3f)) * new Basis(Vector3.Up, _rng.RandfRange(-0.25f, 0.25f)) * face;
                float size = _rng.RandfRange(0.22f, 0.55f);
                // A little out from the heightfield's wall: the rock drawn over it stands a little proud of it.
                var at = wall + along * _rng.RandfRange(-0.6f, 0.6f) + Vector3.Up * _rng.RandfRange(-0.5f, 0.5f) + normal * _rng.RandfRange(0.12f, 0.22f);
                _at[i] = new Transform3D(face.Scaled(Vector3.One * size), at);
                _threshold[i] = _rng.RandfRange(0.02f, 0.98f);
                _on[i] = a.Cover > _threshold[i];
                _multimesh.SetInstanceTransform(i, _on[i] ? _at[i] : Hidden);
                _multimesh.SetInstanceColor(i, Washed[_rng.RandiRange(0, Washed.Length - 1)]);
                _multimesh.SetInstanceCustomData(i, new Color(_rng.Randf(), 0f, 0f, 0f));
            }
        }
    }

    static readonly Transform3D Hidden = new(Basis.Identity.Scaled(Vector3.Zero), new Vector3(0f, -500f, 0f));

    public void Sync(PlaneWorld world, float dt, Camera3D? camera)
    {
        int budget = PeelBudget;
        for (int p = 0; p < _shown.Length && p < world.Ash.Count; p++)
        {
            float cover = world.Ash[p].Cover;
            if (cover == _shown[p]) continue;
            _shown[p] = cover;
            var normal = new Vector3(world.Ash[p].Normal.X, 0f, world.Ash[p].Normal.Y);
            for (int k = 0; k < PerPatch; k++)
            {
                int i = p * PerPatch + k;
                if (!_on[i] || cover > _threshold[i]) continue;
                _on[i] = false;
                _multimesh.SetInstanceTransform(i, Hidden);
                if (budget-- > 0) Peel(_at[i].Origin, normal);
            }
        }
        if (camera is null) return;
        _magic.Tick(dt, camera.GlobalBasis);
        _flakes.Tick(dt, camera.GlobalBasis);
    }

    /// <summary>A scrap peels off: a twirl of magic particles off the wall, and a flake of ash drifting down and fading.</summary>
    void Peel(Vector3 at, Vector3 normal)
    {
        for (int i = 0; i < 3; i++)
        {
            var v = normal * _rng.RandfRange(0.5f, 1.4f) + Vector3.Up * _rng.RandfRange(0.2f, 1f)
                    + new Vector3(_rng.RandfRange(-0.5f, 0.5f), _rng.RandfRange(-0.2f, 0.3f), _rng.RandfRange(-0.5f, 0.5f));
            _magic.Emit(at + normal * 0.05f, v, _rng.RandfRange(0.6f, 1f), _rng.RandfRange(0.07f, 0.13f), gravity: -0.4f, drag: 2.2f);
        }
        var ash = Washed[_rng.RandiRange(0, Washed.Length - 1)];
        _flakes.Emit(at + normal * 0.05f, normal * _rng.RandfRange(0.3f, 0.7f) + Vector3.Down * 0.2f, _rng.RandfRange(0.9f, 1.4f), _rng.RandfRange(0.06f, 0.1f), 0.02f,
            new Color(ash.R, ash.G, ash.B, 0.9f), new Color(ash.R, ash.G, ash.B, 0f), drag: 1.8f, buoyancy: -0.5f);
    }
}
