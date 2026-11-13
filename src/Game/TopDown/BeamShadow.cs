using System;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Where her beam's light gets to, in three dimensions (docs/VISUAL-SHOWCASE.md): a grid of rays out from her bell over
/// the whole of the cone, across it (Size of them, from one side to the other) and up and down it (Size), each going on
/// until something stops it: the seabed under it or rock rising into it (from the height map; rock that stands across
/// her plane at least WallTop above it, so the light is stopped there as in the sim), or a creature's body, a round
/// body of its own size at her plane. Each ray's reach (a share of the beam's) is a texel of a texture (red: stopped by
/// anything; green: by the creatures alone), so the shaders light only what the light gets to: the haze in the water
/// (her_beam_volume, red) and the reef (reef_show's rs_beam, green: it has its own test for the rock, her_sight). Every
/// creature casts its own round shadow down the beam, as wide as it is, growing behind it.
/// </summary>
public sealed class BeamShadow
{
    /// <summary>Rays across and up and down the cone (texels), how far apart the samples along the floor are (m).</summary>
    public const int Size = 48;
    const float Step = 0.25f;
    /// <summary>Rock across her plane stands at least this far above it (m) for her light.</summary>
    const float WallTop = 1.4f;
    /// <summary>How much wider than the cone the grid reaches (so its edge is never cut).</summary>
    public const float Margin = 1.08f;

    /// <summary>Each ray's reach (a share of the beam's): stopped by anything, and by the creatures alone.</summary>
    readonly float[] _reach = new float[Size * Size * 2];
    readonly byte[] _bytes = new byte[Size * Size * 8];
    float[] _tops = Array.Empty<float>();
    readonly System.Collections.Generic.List<(System.Numerics.Vector2 At, float R)> _near = new();
    readonly Image _image;
    public ImageTexture Texture { get; }

    public BeamShadow()
    {
        _image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgf);
        _image.Fill(Colors.White);
        Texture = ImageTexture.CreateFromImage(_image);
    }

    /// <summary>
    /// Casts the grid for a beam from <paramref name="origin"/> (world: her plane's point, at <paramref name="height"/>)
    /// along <paramref name="dir"/>, its half-angle and reach; the grid spans ±half×Margin each way.
    /// </summary>
    public void Cast(PlaneWorld world, System.Numerics.Vector2 origin, float height, System.Numerics.Vector2 dir, float half, float range)
    {
        var map = world.Map;
        float span = half * Margin;
        int steps = Mathf.CeilToInt(range / Step) + 1;
        if (_tops.Length < steps) _tops = new float[steps];
        // Only the bodies that could stand in it: within its reach and near its cone (a plain array: this loop runs for
        // every ray of the grid, and an interface's enumerator would make garbage each time).
        var all = world.LightBodies;
        _near.Clear();
        float wide = MathF.Sin(MathF.Min(span + 0.15f, 1.5f));
        for (int b = 0; b < all.Count; b++)
        {
            var (_, at, r) = all[b];
            var to = at - origin;
            float along = System.Numerics.Vector2.Dot(to, dir);
            if (along + r < 0f || along - r > range) continue;
            float across = MathF.Abs(to.X * dir.Y - to.Y * dir.X);
            if (across - r > MathF.Max(along, 0f) * wide / MathF.Max(MathF.Cos(MathF.Min(span, 1.4f)), 0.2f) + 0.5f) continue;
            _near.Add((at, r));
        }
        for (int i = 0; i < Size; i++)
        {
            float az = -span + 2f * span * (i + 0.5f) / Size;
            float ca = MathF.Cos(az), sa = MathF.Sin(az);
            var way = new System.Numerics.Vector2(dir.X * ca - dir.Y * sa, dir.X * sa + dir.Y * ca);
            // What stands under this way, out along the floor: the seabed, or rock (as tall as the light needs it).
            for (int k = 0; k < steps; k++)
            {
                var q = origin + way * (k * Step);
                float h = map.HeightAt(q);
                _tops[k] = h > LevelMap.BlockHeight || world.StopsLight(q) ? MathF.Max(h, LevelMap.SwimBand + WallTop) : h;
            }
            for (int j = 0; j < Size; j++)
            {
                float el = -span + 2f * span * (j + 0.5f) / Size;
                float ce = MathF.Cos(el), te = MathF.Tan(el);
                // Along the floor: the first sample the ray is under what stands there (between it and the one before).
                float reach = range;
                for (int k = 1; k < steps; k++)
                {
                    float t = k * Step;
                    float gap = height + t * te - _tops[k];
                    if (gap >= 0f) continue;
                    float before = height + (t - Step) * te - _tops[k - 1];
                    float f = before > 0f ? before / (before - gap) : 0f;
                    reach = MathF.Min(range, (t - Step + f * Step) / ce);
                    break;
                }
                // The creatures' bodies: round, at her plane.
                float byBodies = range;
                var d3 = new Vector3(way.X * ce, MathF.Sin(el), way.Y * ce);
                for (int b = 0; b < _near.Count; b++)
                {
                    var (at, r) = _near[b];
                    var to = new Vector3(at.X - origin.X, LevelMap.SwimBand - height, at.Y - origin.Y);
                    float along = to.Dot(d3);
                    if (along <= 0f || along - r > byBodies) continue;
                    float across2 = to.LengthSquared() - along * along;
                    float rr = r * 0.9f;
                    if (across2 >= rr * rr) continue;
                    float meet = along - MathF.Sqrt(rr * rr - across2);
                    if (meet > 0.2f) byBodies = MathF.Min(byBodies, meet);
                }
                _reach[(j * Size + i) * 2] = MathF.Min(reach, byBodies) / range;
                _reach[(j * Size + i) * 2 + 1] = byBodies / range;
            }
        }
        Buffer.BlockCopy(_reach, 0, _bytes, 0, _bytes.Length);
        _image.SetData(Size, Size, false, Image.Format.Rgf, _bytes);
        Texture.Update(_image);
    }
}
