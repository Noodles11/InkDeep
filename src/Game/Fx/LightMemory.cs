using System;
using Godot;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.Fx;

/// <summary>
/// Where her light has lately fallen (docs/VISUAL-SHOWCASE.md, "The reef answers her light"): a map round her (Span
/// metres, PerMetre cells a metre) of how much of her glow and her beam each point had at most in the last moments,
/// fading out over Fade seconds once her light has gone on. The post pass reads it (topdown_post's glow_memory) to let
/// the reef's fluorescent life glow on after her light has passed, the reef otherwise grey again. Her light is cut off
/// by the rock between (rays cast round her) and shows nothing within an inkseed's stain.
/// </summary>
public sealed class LightMemory
{
    /// <summary>How long what her light touched glows on after it (s).</summary>
    public const float Fade = 5f;
    /// <summary>The map round her: metres a side and cells a metre.</summary>
    public const float Span = 64f;
    const int PerMetre = 2, N = (int)(Span * PerMetre);
    /// <summary>The rays cast round her for where her light is cut off by rock.</summary>
    const int Rays = 72;

    float[] _glow = new float[N * N];
    float[] _swap = new float[N * N];
    readonly byte[] _bytes = new byte[N * N];
    readonly float[] _reach = new float[Rays];
    Image? _image;
    System.Numerics.Vector2 _origin;
    bool _any;

    public ImageTexture? Texture { get; private set; }
    /// <summary>The map's corner (world x, z).</summary>
    public Vector2 Origin => new(_origin.X, _origin.Y);

    public void Clear()
    {
        Array.Clear(_glow);
        _any = false;
    }

    /// <summary>
    /// One frame: what was lit fades, and her light now laid in (its strongest kept): her aura round
    /// <paramref name="aura"/> to <paramref name="auraReach"/> (0: none), her beam from <paramref name="beam"/> along
    /// <paramref name="aim"/> to <paramref name="beamReach"/> within <paramref name="half"/> radians each side, eased on by
    /// <paramref name="beamOn"/> (0..1).
    /// </summary>
    public void Step(PlaneWorld world, float dt, System.Numerics.Vector2 her, System.Numerics.Vector2 aura, float auraReach,
        System.Numerics.Vector2 beam, System.Numerics.Vector2 aim, float beamReach, float half, float beamOn)
    {
        // (The map's corner on its own cells; moved, what it holds moves with it.)
        var origin = new System.Numerics.Vector2(MathF.Floor((her.X - Span / 2f) * PerMetre) / PerMetre, MathF.Floor((her.Y - Span / 2f) * PerMetre) / PerMetre);
        int sx = (int)MathF.Round((origin.X - _origin.X) * PerMetre), sy = (int)MathF.Round((origin.Y - _origin.Y) * PerMetre);
        if (sx != 0 || sy != 0)
        {
            Array.Clear(_swap);
            if (Math.Abs(sx) < N && Math.Abs(sy) < N)
                for (int y = 0; y < N; y++)
                {
                    int oy = y + sy;
                    if (oy < 0 || oy >= N) continue;
                    for (int x = 0; x < N; x++)
                    {
                        int ox = x + sx;
                        if (ox >= 0 && ox < N) _swap[y * N + x] = _glow[oy * N + ox];
                    }
                }
            (_glow, _swap) = (_swap, _glow);
            _origin = origin;
        }
        float fall = dt / Fade;
        if (_any)
            for (int i = 0; i < _glow.Length; i++)
                if (_glow[i] > 0f) _glow[i] = MathF.Max(0f, _glow[i] - fall);

        bool auraOn = auraReach > 0f, beamLit = beamOn > 0f && beamReach > 0f;
        if (auraOn || beamLit)
        {
            // Where her light gets to round her: each ray's reach before rock.
            float most = MathF.Max(auraOn ? auraReach * 1.15f : 0f, beamLit ? beamReach : 0f);
            var from = auraOn ? aura : beam;
            for (int r = 0; r < Rays; r++)
            {
                float a = r * MathF.Tau / Rays;
                _reach[r] = world.LineReach(from, new System.Numerics.Vector2(MathF.Cos(a), MathF.Sin(a)), most, out _) + 0.8f;
            }
            float Lit(System.Numerics.Vector2 p)
            {
                var d = p - from;
                float dist = d.Length();
                int r = ((int)MathF.Round(MathF.Atan2(d.Y, d.X) / MathF.Tau * Rays) % Rays + Rays) % Rays;
                if (dist > _reach[r]) return 0f;
                float k = 0f;
                if (auraOn)
                {
                    float da = System.Numerics.Vector2.Distance(p, aura);
                    k = 1f - SmoothStep(auraReach * 0.4f, auraReach * 1.15f, da);
                }
                if (beamLit)
                {
                    var to = p - beam;
                    float db = to.Length();
                    if (db < beamReach && db > 1e-3f)
                    {
                        float angle = MathF.Acos(Math.Clamp(System.Numerics.Vector2.Dot(to / db, aim), -1f, 1f));
                        float cone = db < 0.8f ? 1f : 1f - SmoothStep(half * 0.6f, half * 1.1f, angle);
                        float fade = MathF.Pow(1f - db / beamReach, 0.9f) * (1f - SmoothStep(beamReach * 0.55f, beamReach, db));
                        k = MathF.Max(k, cone * fade * beamOn);
                    }
                }
                return k;
            }
            int x0 = Math.Max(0, (int)((from.X - most - _origin.X) * PerMetre)), x1 = Math.Min(N - 1, (int)((from.X + most - _origin.X) * PerMetre) + 1);
            int y0 = Math.Max(0, (int)((from.Y - most - _origin.Y) * PerMetre)), y1 = Math.Min(N - 1, (int)((from.Y + most - _origin.Y) * PerMetre) + 1);
            bool stains = world.Stains.Count > 0;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = _origin + new System.Numerics.Vector2((x + 0.5f) / PerMetre, (y + 0.5f) / PerMetre);
                float k = Lit(p);
                if (k <= 0.01f || stains && world.Stained(p)) continue;
                int i = y * N + x;
                if (k > _glow[i]) _glow[i] = k;
            }
            _any = true;
        }
        for (int i = 0; i < _glow.Length; i++) _bytes[i] = (byte)(Math.Clamp(_glow[i], 0f, 1f) * 255f);
        if (_image is null)
        {
            _image = Image.CreateFromData(N, N, false, Image.Format.R8, _bytes);
            Texture = ImageTexture.CreateFromImage(_image);
        }
        else
        {
            _image.SetData(N, N, false, Image.Format.R8, _bytes);
            Texture!.Update(_image);
        }
    }

    static float SmoothStep(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
