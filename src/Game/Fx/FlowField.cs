using System;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The water's motion round her (docs/REEF-REDO-PROPOSAL.md §5): a 128² grid, 0.5 m a cell, that follows the camera's
/// focus and holds how fast the water moves on her plane. Her swimming, her jets and her dashes stir it (Stir), and so
/// do the creatures; each frame the stir is carried along by itself, spreads a little and fades. It is handed to the
/// shaders as the `flow_tex` global (flow_field.gdshaderinc), so everything soft bends away from her wake and springs
/// back.
/// </summary>
public sealed class FlowField
{
    const int N = 128;
    const float Cell = 0.5f;
    /// <summary>How fast a stir fades (per second), and how much of it spreads to its neighbours each step.</summary>
    const float Fade = 1.5f, Spread = 0.18f;
    /// <summary>The fastest the water moves (m/s).</summary>
    const float MaxSpeed = 9f;

    float[] _vx = new float[N * N], _vz = new float[N * N];
    float[] _tx = new float[N * N], _tz = new float[N * N];
    readonly byte[] _bytes = new byte[N * N * 8];
    readonly float[] _packed = new float[N * N * 2];
    ImageTexture? _texture;
    /// <summary>The grid's corner in whole cells (world x, z / Cell).</summary>
    int _ox, _oz;
    bool _placed;

    public float Size => N * Cell;

    /// <summary>Keeps the grid centred on the focus (world x, z), moving its contents by whole cells.</summary>
    public void Follow(Vector3 focus)
    {
        int ox = Mathf.FloorToInt(focus.X / Cell) - N / 2, oz = Mathf.FloorToInt(focus.Z / Cell) - N / 2;
        if (!_placed)
        {
            _ox = ox;
            _oz = oz;
            _placed = true;
            return;
        }
        int dx = ox - _ox, dz = oz - _oz;
        if (dx == 0 && dz == 0) return;
        Array.Clear(_tx);
        Array.Clear(_tz);
        for (int z = 0; z < N; z++)
        {
            int sz = z + dz;
            if (sz < 0 || sz >= N) continue;
            for (int x = 0; x < N; x++)
            {
                int sx = x + dx;
                if (sx < 0 || sx >= N) continue;
                _tx[z * N + x] = _vx[sz * N + sx];
                _tz[z * N + x] = _vz[sz * N + sx];
            }
        }
        (_vx, _tx) = (_tx, _vx);
        (_vz, _tz) = (_tz, _vz);
        _ox = ox;
        _oz = oz;
    }

    /// <summary>Stirs the water round a point (world x, z) toward a velocity (m/s), within radius metres.</summary>
    public void Stir(System.Numerics.Vector2 at, System.Numerics.Vector2 velocity, float radius, float strength = 1f)
    {
        if (!_placed || velocity.LengthSquared() < 1e-4f) return;
        float gx = at.X / Cell - _ox, gz = at.Y / Cell - _oz;
        int r = Mathf.CeilToInt(radius / Cell * 1.5f);
        int x0 = Math.Max(0, (int)gx - r), x1 = Math.Min(N - 1, (int)gx + r);
        int z0 = Math.Max(0, (int)gz - r), z1 = Math.Min(N - 1, (int)gz + r);
        float inv = 1f / (radius / Cell * (radius / Cell));
        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            float ddx = x - gx, ddz = z - gz;
            float k = Mathf.Exp(-(ddx * ddx + ddz * ddz) * inv) * strength;
            if (k < 0.01f) continue;
            int i = z * N + x;
            // Toward the stir's velocity, never past it.
            _vx[i] += (velocity.X - _vx[i]) * Mathf.Min(k, 1f);
            _vz[i] += (velocity.Y - _vz[i]) * Mathf.Min(k, 1f);
        }
    }

    /// <summary>One frame: the stir carried along by itself, spread, faded; then handed to the shaders.</summary>
    public void Step(float dt)
    {
        if (!_placed) return;
        float fade = Mathf.Exp(-Fade * dt);
        float back = dt / Cell;
        for (int z = 0; z < N; z++)
        for (int x = 0; x < N; x++)
        {
            int i = z * N + x;
            // Where this water came from a moment ago (semi-Lagrangian), so a wake travels on after her.
            float sx = Math.Clamp(x - _vx[i] * back, 0f, N - 1.001f), sz = Math.Clamp(z - _vz[i] * back, 0f, N - 1.001f);
            int ix = (int)sx, iz = (int)sz;
            float fx = sx - ix, fz = sz - iz;
            int j = iz * N + ix;
            float ax = Mathf.Lerp(_vx[j], _vx[j + 1], fx), bx = Mathf.Lerp(_vx[j + N], _vx[j + N + 1], fx);
            float az = Mathf.Lerp(_vz[j], _vz[j + 1], fx), bz = Mathf.Lerp(_vz[j + N], _vz[j + N + 1], fx);
            _tx[i] = Mathf.Lerp(ax, bx, fz);
            _tz[i] = Mathf.Lerp(az, bz, fz);
        }
        for (int z = 1; z < N - 1; z++)
        for (int x = 1; x < N - 1; x++)
        {
            int i = z * N + x;
            float mx = (_tx[i - 1] + _tx[i + 1] + _tx[i - N] + _tx[i + N]) * 0.25f;
            float mz = (_tz[i - 1] + _tz[i + 1] + _tz[i - N] + _tz[i + N]) * 0.25f;
            float vx = Mathf.Lerp(_tx[i], mx, Spread) * fade, vz = Mathf.Lerp(_tz[i], mz, Spread) * fade;
            float l = Mathf.Sqrt(vx * vx + vz * vz);
            if (l > MaxSpeed)
            {
                vx *= MaxSpeed / l;
                vz *= MaxSpeed / l;
            }
            _vx[i] = vx;
            _vz[i] = vz;
        }
        for (int i = 0; i < N * N; i++)
        {
            _packed[i * 2] = _vx[i];
            _packed[i * 2 + 1] = _vz[i];
        }
        Buffer.BlockCopy(_packed, 0, _bytes, 0, _bytes.Length);
        var image = Image.CreateFromData(N, N, false, Image.Format.Rgf, _bytes);
        if (_texture is null) _texture = ImageTexture.CreateFromImage(image);
        else _texture.Update(image);
        RenderingServer.GlobalShaderParameterSet(SN.Of("flow_tex"), _texture);
        RenderingServer.GlobalShaderParameterSet(SN.Of("flow_rect"), new Vector4(_ox * Cell, _oz * Cell, Size, 1f));
    }

    /// <summary>Turns the field off for the shaders (nothing bends).</summary>
    public static void Off() => RenderingServer.GlobalShaderParameterSet(SN.Of("flow_rect"), new Vector4(0f, 0f, 1f, 0f));
}
