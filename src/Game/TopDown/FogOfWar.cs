using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// What Clementine has seen of the level, per metre of the grid: 0 unseen, 1 seen, soft in between. Revealed around
/// her as she swims (a disc with a soft rim) and kept for the rest of the level; the minimap draws it as fog.
/// </summary>
public sealed class FogOfWar
{
    /// <summary>The level's grid points a side (its map's Samples).</summary>
    int N = LevelMap.Seeded + 1;
    float[] _reveal;
    Image _image;
    System.Numerics.Vector2 _last = new(-1000f, -1000f);

    public FogOfWar()
    {
        _reveal = new float[N * N];
        _image = Image.CreateEmpty(N, N, false, Image.Format.Rf);
        Texture = ImageTexture.CreateFromImage(_image);
    }

    /// <summary>The revealed amount per grid point, for shaders (sample at (world + 0.5) / samples).</summary>
    public ImageTexture Texture { get; }

    /// <summary>Its grid points a side.</summary>
    public int Samples => N;

    /// <summary>How much of the point has been seen (0–1), from the nearest grid point.</summary>
    public float At(System.Numerics.Vector2 p)
    {
        int x = Math.Clamp((int)MathF.Round(p.X), 0, N - 1), y = Math.Clamp((int)MathF.Round(p.Y), 0, N - 1);
        return _reveal[y * N + x];
    }

    /// <summary>A new level, of this many grid points a side: nothing seen.</summary>
    public void Reset(int samples)
    {
        if (samples != N)
        {
            N = samples;
            _reveal = new float[N * N];
            _image = Image.CreateEmpty(N, N, false, Image.Format.Rf);
            Texture.SetImage(_image);
        }
        Array.Clear(_reveal);
        _image.Fill(new Color(0f, 0f, 0f));
        Texture.Update(_image);
        _last = new(-1000f, -1000f);
    }

    /// <summary>Everything seen at once.</summary>
    public void RevealAll()
    {
        Array.Fill(_reveal, 1f);
        _image.Fill(new Color(1f, 0f, 0f));
        Texture.Update(_image);
    }

    /// <summary>Everything in a box seen at once (a haven: she knows her home water), its edge fading over <paramref name="soft"/> metres.</summary>
    public void RevealBox(System.Numerics.Vector2 min, System.Numerics.Vector2 max, float soft)
    {
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float out_ = MathF.Max(MathF.Max(min.X - x, x - max.X), MathF.Max(min.Y - y, y - max.Y));
            float v = 1f - Mathf.SmoothStep(0f, soft, out_);
            if (v <= _reveal[y * N + x]) continue;
            _reveal[y * N + x] = v;
            _image.SetPixel(x, y, new Color(v, 0f, 0f));
        }
        Texture.Update(_image);
    }

    /// <summary>Reveals a disc of the given radius around p, its last <paramref name="soft"/> metres fading out.</summary>
    public void Reveal(System.Numerics.Vector2 p, float radius, float soft)
    {
        if (System.Numerics.Vector2.Distance(p, _last) < 0.4f) return;
        _last = p;
        bool changed = false;
        int x0 = Math.Max(0, (int)(p.X - radius)), x1 = Math.Min(N - 1, (int)(p.X + radius) + 1);
        int y0 = Math.Max(0, (int)(p.Y - radius)), y1 = Math.Min(N - 1, (int)(p.Y + radius) + 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = System.Numerics.Vector2.Distance(new System.Numerics.Vector2(x, y), p);
            float v = 1f - Mathf.SmoothStep(radius - soft, radius, d);
            if (v <= _reveal[y * N + x]) continue;
            _reveal[y * N + x] = v;
            _image.SetPixel(x, y, new Color(v, 0f, 0f));
            changed = true;
        }
        if (changed) Texture.Update(_image);
    }

    /// <summary>What she has seen, for the save (base64).</summary>
    public string Save()
    {
        var bytes = new byte[_reveal.Length * sizeof(float)];
        Buffer.BlockCopy(_reveal, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>What she has seen, compactly (a byte a grid point, base64): a region's chart, kept on her jelly.</summary>
    public string Chart()
    {
        var bytes = new byte[_reveal.Length];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)MathF.Round(Math.Clamp(_reveal[i], 0f, 1f) * 255f);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>What she had charted (as <see cref="Chart"/> wrote it); nothing changes if it does not fit.</summary>
    public void LoadChart(string chart)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(chart);
        }
        catch (FormatException)
        {
            return;
        }
        if (bytes.Length != _reveal.Length) return;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float v = bytes[y * N + x] / 255f;
            _reveal[y * N + x] = v;
            _image.SetPixel(x, y, new Color(v, 0f, 0f));
        }
        Texture.Update(_image);
        _last = new(-1000f, -1000f);
    }

    /// <summary>What she had seen, from the save (as <see cref="Save"/> wrote it); nothing changes if it does not fit.</summary>
    public void Load(string saved)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(saved);
        }
        catch (FormatException)
        {
            return;
        }
        if (bytes.Length != _reveal.Length * sizeof(float)) return;
        Buffer.BlockCopy(bytes, 0, _reveal, 0, bytes.Length);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
            _image.SetPixel(x, y, new Color(_reveal[y * N + x], 0f, 0f));
        Texture.Update(_image);
        _last = new(-1000f, -1000f);
    }
}
