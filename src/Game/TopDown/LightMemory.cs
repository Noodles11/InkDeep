using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her light's afterimage (the ink look, docs/proposals/CROSSOVER.md §12): where her beam and her bubble shots' light
/// have fallen, the reef keeps its colour a moment after the light has moved on (<see cref="Hold"/> seconds), then
/// fades back to the muted ash over <see cref="Fade"/>. A grid of <see cref="Cell"/>-metre cells over the level, each
/// the light it was last given and how long ago; sent to the post pass as a texture over the level (her_after,
/// her_after_rect: its corner and size in world xz), which takes the larger of it and her light now. What the light
/// fell on is drawn as the post pass draws it (her beam's fan to where it stops, cut by rock; a shot's light round it).
/// </summary>
public sealed class LightMemory
{
    /// <summary>A cell's side (metres); how long a lit cell holds its colour, and how long it then takes to fade.</summary>
    public const float Cell = 0.25f, Hold = 0.3f, Fade = 1.3f;

    int _n;
    float[] _peak = Array.Empty<float>(), _age = Array.Empty<float>();
    byte[] _bytes = Array.Empty<byte>();
    Image? _image;
    ImageTexture? _texture;
    LevelMap? _map;
    ulong _lastUsec;

    /// <summary>How much of her light a point still shows (0..1), now or from a moment ago.</summary>
    public float At(System.Numerics.Vector2 p)
    {
        if (_n == 0) return 0f;
        int x = (int)(p.X / Cell), y = (int)(p.Y / Cell);
        if (x < 0 || y < 0 || x >= _n || y >= _n) return 0f;
        return Shown(x + y * _n);
    }

    float Shown(int i) => _peak[i] * (1f - Mathf.SmoothStep(Hold, Hold + Fade, _age[i]));

    void Reset(LevelMap map)
    {
        _map = map;
        _n = (int)MathF.Ceiling(map.Size / Cell) + 1;
        _peak = new float[_n * _n];
        _age = new float[_n * _n];
        Array.Fill(_age, Hold + Fade);
        _bytes = new byte[_n * _n];
        _image = Image.CreateFromData(_n, _n, false, Image.Format.R8, _bytes);
        _texture = ImageTexture.CreateFromImage(_image);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_after"), _texture);
        RenderingServer.GlobalShaderParameterSet(OctoShoots.Game.Fx.SN.Of("her_after_rect"), new Vector4(0f, 0f, _n * Cell, _n * Cell));
    }

    /// <summary>
    /// A frame: every cell ages; those her beam lights now, and those round her bubbles' lights (each a centre, a reach,
    /// a strength: CombatView.Lights), take their light; the texture is sent on.
    /// </summary>
    public void Step(PlaneWorld world, Vector4[] shotLights, int shotCount)
    {
        if (!ReferenceEquals(world.Map, _map)) Reset(world.Map);
        ulong now = Time.GetTicksUsec();
        float dt = _lastUsec == 0 ? 0f : Mathf.Min((now - _lastUsec) / 1e6f, 0.1f);
        _lastUsec = now;
        for (int i = 0; i < _age.Length; i++) _age[i] += dt;
        if (world.BeamOn)
        {
            // Her beam's light as the post pass draws it (reef_show.gdshaderinc's rs_beam): its fan along her aim to where
            // it stops, strongest down its middle and near her, cut off by the rock between.
            var from = world.Beam.Position;
            var aim = world.BeamAim;
            float reach = System.Numerics.Vector2.Distance(from, world.BeamEnd) + 1.5f;
            float half = MathF.Max(world.BeamSpread, world.Beam.HalfAngle);
            Visit(from, reach, (p, i) =>
            {
                var to = p - from;
                float d = to.Length();
                if (d >= reach) return 0f;
                float angle = d < 1e-3f ? 0f : MathF.Acos(Math.Clamp(System.Numerics.Vector2.Dot(to / d, aim), -1f, 1f));
                float cone = (1f - Mathf.SmoothStep(half * 0.6f, half * 1.1f, angle)) * Mathf.SmoothStep(0.15f, 0.9f, d);
                if (cone <= 0f) return 0f;
                float fall = MathF.Pow(1f - d / reach, 0.9f) * (1f - Mathf.SmoothStep(reach * 0.55f, reach, d));
                return fall <= 0.02f || !world.LineOfSight(from, p) ? 0f : cone * fall;
            });
        }
        for (int s = 0; s < shotCount; s++)
        {
            var l = shotLights[s];
            var at = new System.Numerics.Vector2(l.X, l.Y);
            float r = l.Z, k = Mathf.Clamp(l.W * ShotShown, 0f, 1f);
            Visit(at, r * 1.6f, (p, i) =>
            {
                float d2 = System.Numerics.Vector2.DistanceSquared(p, at);
                return k * Mathf.Exp(-d2 / (r * r * 1.6f));
            });
        }
        for (int i = 0; i < _bytes.Length; i++) _bytes[i] = (byte)(Mathf.Clamp(Shown(i), 0f, 1f) * 255f);
        _image!.SetData(_n, _n, false, Image.Format.R8, _bytes);
        _texture!.Update(_image);
    }

    /// <summary>How much of a bubble's halo strength (CombatView.Lights' w) counts as showing the reef: the post pass's own.</summary>
    public const float ShotShown = 2.4f;

    /// <summary>Each cell within reach of a point gives its light; a cell lit more than it still shows takes it afresh.</summary>
    void Visit(System.Numerics.Vector2 c, float reach, Func<System.Numerics.Vector2, int, float> light)
    {
        int x0 = Math.Max(0, (int)((c.X - reach) / Cell)), x1 = Math.Min(_n - 1, (int)((c.X + reach) / Cell));
        int y0 = Math.Max(0, (int)((c.Y - reach) / Cell)), y1 = Math.Min(_n - 1, (int)((c.Y + reach) / Cell));
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var p = new System.Numerics.Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell);
            if (System.Numerics.Vector2.DistanceSquared(p, c) > reach * reach) continue;
            int i = x + y * _n;
            float v = light(p, i);
            if (v > 0.02f && v >= Shown(i))
            {
                _peak[i] = v;
                _age[i] = 0f;
            }
        }
    }
}
