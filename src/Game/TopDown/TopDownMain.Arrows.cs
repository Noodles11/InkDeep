using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// What the arrows at the screen's edges point at (EdgeArrows, in place of a minimap): the open mouths round her in
/// the colour of the place behind each, a sub-level's way out, the boss's arena while its boss waits, what her requests
/// and chains name, and a level's own rooms once she has been in them (or, with Brass Compass, all of them).
/// </summary>
public partial class TopDownMain
{
    /// <summary>She reveals the fog round her this far (the Tab map's), its edge this soft.</summary>
    const float RevealSoft = 10f, RevealRadius = 27.5f;

    static readonly Color WayOut = new(0.95f, 0.97f, 1f), SecretColour = new(0.82f, 0.88f, 1f);

    static Color MouthColour(PoiKind kind) => kind switch
    {
        PoiKind.Haven => ReefKinds.Colour(ReefKind.Haven),
        PoiKind.TreasureCave => ReefKinds.Colour(ReefKind.Treasure),
        PoiKind.Secret => SecretColour,
        _ => ReefKinds.Colour(ReefKind.Den),
    };

    IEnumerable<(System.Numerics.Vector2 At, Color Colour)> ArrowTargets()
    {
        foreach (var mark in _marks) yield return mark;
        for (int i = 0; i < _map.Mouths.Count; i++)
            if (!_world.ShutMouths.Contains(i))
                yield return (_map.Mouths[i].At, MouthColour(i < _map.MouthKinds.Count ? _map.MouthKinds[i] : PoiKind.LeakCave));
        if (_map.Way is { } way) yield return (way.At, WayOut);
        bool all = _world.Run.Loadout.Flags.Contains("landmarks");
        for (int i = 0; i < _map.Pois.Count; i++)
        {
            var poi = _map.Pois[i];
            bool inside = poi.Position.X >= 0f && poi.Position.Y >= 0f && poi.Position.X <= _map.Size && poi.Position.Y <= _map.Size;
            if (!inside) continue;
            // A boss's arena while its boss waits.
            if (poi.Kind == PoiKind.Exit && _map.HasBoss && _world.Boss is { Freed: false })
                yield return (poi.Position, ReefKinds.Colour(ReefKind.Boss));
            // A level's own rooms (those behind mouths have their mouths' arrows).
            else if (poi.Kind is PoiKind.Shop or PoiKind.TreasureCave or PoiKind.ShellCache or PoiKind.CrabDeal && (all || _visited.Contains(i)))
                yield return (poi.Position, LevelView.PoiColor(poi.Kind));
        }
    }

    /// <summary>Verification (--dbg-vault=shells|orb|prank): a bubble vault of that prize 4 m from her, where she sees it.</summary>
    string? _dbgVault = System.Linq.Enumerable.FirstOrDefault(OS.GetCmdlineUserArgs(), a => a.StartsWith("--dbg-vault="))?["--dbg-vault=".Length..];
    System.Numerics.Vector2? _dbgVaultAt;

    void DebugVault()
    {
        if (_dbgVault is not { } kind || InDiveCave) return;
        var prize = kind switch { "orb" => VaultPrize.Orb, "prank" => VaultPrize.Prank, _ => VaultPrize.Shells };
        var her = _world.Player.Position;
        for (int k = 0; k < 32; k++)
        {
            float a = -Mathf.Pi / 2f + k * Mathf.Tau / 32f;
            var at = her + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f;
            if (!_world.Clear(at, VaultTuning.Radius) || !_world.LineOfSight(her, at)) continue;
            _world.Vaults.Add(new PlaneVault { Position = at, Prize = prize, Shells = 10, Seed = 3.3f });
            _dbgVaultAt = at;
            _dbgVault = null;
            return;
        }
    }

    /// <summary>Copies the world's light springs onto the Tab map (it shows no enemies and no corruption), and the main current once it runs.</summary>
    static void FillSprings(TopoMap map, PlaneWorld world)
    {
        map.Springs.Clear();
        foreach (var sp in world.Springs)
            if (!sp.Spent) map.Springs.Add((sp.Position, sp.Bathing));
        map.Current = world.Currents.Find(c => c.Strength > 0f && !c.Holds)?.Course;
    }
}
