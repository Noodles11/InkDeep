using System;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// The Reef Survey prototype's reef field (maps/reef_field.gd), ported as it is: one seeded scalar density over all
/// signed coordinates (cells of Cell metres; a slice's y in steps), coarse shelf noise for the reef's footprint, 3D masses,
/// lobes and detail, all bent by a slowly varying domain warp. Positive past the cutoff is rock; the cutoff is a
/// percentile of the seed's own sample (Calibration, Cutoff), so a rock fraction holds over many slices.
/// </summary>
public static class ReefField
{
    public const float Cell = 2f;
    public const int Steps = 3;
    public const double MinRock = 0.10, MaxRock = 0.85;

    // (shift: the lattice's spacing as a power of two in cells; y shift, −1 for 2D; seed offset)
    static readonly (int Shift, int YShift, long Seed) WarpX = (5, 4, 13), WarpZ = (5, 4, 29), Shelf = (5, -1, 41),
        Masses = (4, 3, 61), Lobes = (3, 3, 79), Detail = (2, 2, 103);

    public static long Hash(long x, long z, long y, long seed)
    {
        unchecked
        {
            long h = (x * 3935559000370003845L) ^ (z * 2691343689449507681L) ^ (y * 4768777513237032717L) ^ (seed * 1442695040888963407L);
            h = (h ^ ((h >> 33) & 0x7fffffff)) * -49064778989728563L;
            h = (h ^ ((h >> 33) & 0x7fffffff)) * -4265267296055464877L;
            return (h ^ ((h >> 33) & 0x7fffffff)) & 0x7fffffff;
        }
    }

    static double Value(long x, long z, long y, long seed) => Hash(x, z, y, seed) * (2.0 / 2147483647.0) - 1.0;

    static double Fade(double t) => t * t * t * (t * (6.0 * t - 15.0) + 10.0);

    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Value noise at a point in cells (x, z, with the warp's offsets ox, oz), y in steps; 2D when the y shift is −1.</summary>
    static double Noise(double x, double z, long y, (int Shift, int YShift, long Seed) config, long seed, double ox = 0, double oz = 0)
    {
        double scale = 1 << config.Shift;
        double u = (x + ox) / scale, v = (z + oz) / scale;
        long ix = (long)Math.Floor(u), iz = (long)Math.Floor(v);
        double tx = Fade(u - ix), tz = Fade(v - iz);
        long s = seed + config.Seed;
        double Plane(long iy) => Lerp(Lerp(Value(ix, iz, iy, s), Value(ix + 1, iz, iy, s), tx), Lerp(Value(ix, iz + 1, iy, s), Value(ix + 1, iz + 1, iy, s), tx), tz);
        if (config.YShift < 0) return Plane(0);
        long iy0 = y >> config.YShift, ys = 1L << config.YShift;
        return Lerp(Plane(iy0), Plane(iy0 + 1), Fade((double)(y & (ys - 1)) / ys));
    }

    /// <summary>The density at a point (x, z in cells, y in steps): the prototype's blend, warped.</summary>
    public static double Density(double x, double z, long y, long seed)
    {
        double ox = Noise(x, z, y, WarpX, seed) * 8.0, oz = Noise(x, z, y, WarpZ, seed) * 8.0;
        return 0.40 * Noise(x, z, y, Shelf, seed, ox, oz) + 0.36 * Noise(x, z, y, Masses, seed, ox, oz)
            + 0.18 * Noise(x, z, y, Lobes, seed, ox, oz) + 0.06 * Noise(x, z, y, Detail, seed, ox, oz);
    }

    /// <summary>The seed's own sample of the field, sorted (for Cutoff).</summary>
    public static float[] Calibration(long seed)
    {
        // (Single precision, as the prototype's PackedFloat32Array: the same cutoff to the last digit.)
        var values = new float[4096];
        for (int i = 0; i < values.Length; i++)
            values[i] = (float)Density((Hash(i, 19, 0, seed) & 8191) - 4096, (Hash(i, 43, 0, seed) & 8191) - 4096, ((Hash(i, 91, 0, seed) & 255) - 128) * Steps, seed);
        Array.Sort(values);
        return values;
    }

    /// <summary>The density past which a point is rock, for a share of rock over many slices.</summary>
    public static double Cutoff(float[] values, double rockFraction)
    {
        double rank = (1.0 - Math.Clamp(rockFraction, MinRock, MaxRock)) * (values.Length - 1);
        int lower = (int)Math.Floor(rank);
        return Lerp(values[lower], values[Math.Min(lower + 1, values.Length - 1)], rank - lower);
    }
}
