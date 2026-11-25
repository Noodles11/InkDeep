using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>The lit world's features a level may hold (its biome's: OpticWallTuning, PocketTuning, FloraTuning, FaunaTuning).</summary>
[Flags]
public enum LightFeature
{
    None = 0, Mirrors = 1, Panes = 2, Stones = 4, Haloclines = 8, Pockets = 16, Rooms = 32, Plants = 64, Animals = 128,
    All = Mirrors | Panes | Stones | Haloclines | Pockets | Rooms | Plants | Animals,
}

/// <summary>
/// A tidy level (PlaneOptions.Tidy, on in the crossover): it holds only a few of its biome's light features (FeaturesMin
/// to FeaturesMax of those its biome has, by its own seed), so one level is the mirrors' and the next the stones'; and a
/// share of its water is quiet (Quiet: a cell of a Grid × Grid split of the level), where none of them is placed, nor a
/// speck. Loose things (plants, animals, pockets, mirror plates) stand only in a chamber's open water (Chamber clear round
/// them), not in a passage. (tune)
/// </summary>
public static class TidyTuning
{
    public const int FeaturesMin = 2, FeaturesMax = 3, Grid = 5;
    public const float QuietShare = 0.35f, Chamber = 3.5f;
    /// <summary>Specks: this share of the untidy count, each worth the more for it (her energy on a level kept).</summary>
    public const float SpeckShare = 0.6f;
    /// <summary>A speck's light: this share as bright, and of its reach.</summary>
    public const float SpeckLight = 0.7f, SpeckReach = 0.75f;
    /// <summary>A wall's stored glow lasts this share as long.</summary>
    public const float GlowShare = 0.6f;
}

public sealed partial class PlaneWorld
{
    /// <summary>The light features this level holds (all of its biome's, unless tidy).</summary>
    public LightFeature Features { get; private set; } = LightFeature.All;

    bool[]? _quiet;

    public bool HasFeature(LightFeature f) => (Features & f) != 0;

    /// <summary>A point in the level's quiet water (tidy only): no light feature or speck is placed there.</summary>
    public bool InQuietWater(Vector2 p)
    {
        if (_quiet is null) return false;
        int n = TidyTuning.Grid;
        int x = Math.Clamp((int)(p.X / Map.Size * n), 0, n - 1), y = Math.Clamp((int)(p.Y / Map.Size * n), 0, n - 1);
        return _quiet[y * n + x];
    }

    /// <summary>Where a loose feature may stand on a tidy level: out of the quiet water, in a chamber.</summary>
    bool TidySpot(Vector2 p) => !Options.Tidy || !InQuietWater(p) && Clear(p, TidyTuning.Chamber);

    /// <summary>A tidy level's features and quiet water, by its own seed.</summary>
    void ChooseFeatures()
    {
        if (!Options.Tidy) return;
        var rng = new Rng(Map.Seed ^ 0x71D7UL ^ ((ulong)Map.Level << 20) ^ ((ulong)Map.Depth << 30) ^ ((ulong)Map.Attempt << 46));
        var has = BiomeFeatures();
        for (int i = has.Count - 1; i > 0; i--)
        {
            int j = rng.Int(i + 1);
            (has[i], has[j]) = (has[j], has[i]);
        }
        int take = Math.Min(has.Count, TidyTuning.FeaturesMin + rng.Int(TidyTuning.FeaturesMax - TidyTuning.FeaturesMin + 1));
        Features = LightFeature.None;
        for (int i = 0; i < take; i++) Features |= has[i];
        int n = TidyTuning.Grid;
        _quiet = new bool[n * n];
        for (int i = 0; i < _quiet.Length; i++) _quiet[i] = rng.NextFloat() < TidyTuning.QuietShare;
    }

    /// <summary>The light features its biome has.</summary>
    List<LightFeature> BiomeFeatures()
    {
        var (mirrors, panes) = OpticWallTuning.For(Map.Biome);
        var (sun, shadow, halo) = OpticWallTuning.Stones(Map.Biome);
        var has = new List<LightFeature> { LightFeature.Rooms, LightFeature.Plants };
        if (mirrors > 0) has.Add(LightFeature.Mirrors);
        if (panes > 0) has.Add(LightFeature.Panes);
        if (sun + shadow > 0) has.Add(LightFeature.Stones);
        if (halo > 0) has.Add(LightFeature.Haloclines);
        if (PocketTuning.For(Map.Biome).Length > 0) has.Add(LightFeature.Pockets);
        if (FaunaTuning.For(Map.Biome).Length > 0) has.Add(LightFeature.Animals);
        return has;
    }
}
