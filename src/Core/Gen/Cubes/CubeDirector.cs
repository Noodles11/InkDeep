using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// What a claimed cube holds besides its stock (CUBE-WORLD-PLAN §9): the places behind mouths in its sockets, rolled as
/// it is claimed from the seed, the layer and its claim number alone. Per layer: a clam room once, on its 1st, 2nd or
/// 3rd claim (one chance in three, then one in two, then certain); a secret room one claim in three; a curse room (the
/// game's leak cave, docs/DIVE.md §6: the curse dens are leak caves) one in five, never on the warm-up layer; a haven
/// one in four until the layer has two. At most two a cube (kept in the
/// order haven, clam room, curse room, secret); one with no free socket passes on to the next claim.
/// </summary>
public sealed class CubeDirector
{
    public const float ClamFirst = 1f / 3f, ClamSecond = 0.5f, Secret = 1f / 3f, Curse = 0.2f, Haven = 0.25f;
    public const int Havens = 2, MostPerCube = 2;

    /// <summary>The order places are kept in when more are rolled than a cube holds.</summary>
    static readonly PoiKind[] Order = { PoiKind.Haven, PoiKind.TreasureCave, PoiKind.LeakCave, PoiKind.Secret };

    sealed class LayerState
    {
        public bool Clam;
        public int Havens;
        public readonly List<PoiKind> Carried = new();
    }

    readonly ulong _seed;
    readonly Dictionary<int, LayerState> _layers = new();

    public CubeDirector(ulong seed) => _seed = seed;

    /// <summary>
    /// A cube claimed: the places it opens (as many as it has <paramref name="sockets"/>, at most two), the rest of those
    /// rolled passed on to the layer's next claim.
    /// </summary>
    public List<PoiKind> Claim(int layer, int claim, int sockets)
    {
        if (!_layers.TryGetValue(layer, out var state)) _layers[layer] = state = new LayerState();
        var rng = ReefHash.Rng(_seed, ReefHash.Layer, layer, claim, 0x91ACE);
        var rolled = new List<PoiKind>(state.Carried);
        state.Carried.Clear();
        bool clamPending = rolled.Contains(PoiKind.TreasureCave);
        bool clam = !state.Clam && !clamPending && (claim >= 3 || rng.NextFloat() < (claim == 1 ? ClamFirst : ClamSecond));
        float secret = rng.NextFloat(), curse = rng.NextFloat(), haven = rng.NextFloat();
        if (clam) rolled.Add(PoiKind.TreasureCave);
        if (secret < Secret) rolled.Add(PoiKind.Secret);
        if (layer > 0 && curse < Curse) rolled.Add(PoiKind.LeakCave);
        int havensSoFar = state.Havens + rolled.Count(k => k == PoiKind.Haven);
        if (havensSoFar < Havens && haven < Haven) rolled.Add(PoiKind.Haven);
        rolled = rolled.OrderBy(k => Array.IndexOf(Order, k)).ToList();
        int keep = Math.Min(Math.Min(sockets, MostPerCube), rolled.Count);
        var granted = rolled.Take(keep).ToList();
        // (No room here: passed on; past what a cube holds at most: dropped, but a clam room or a haven, which the layer owes.)
        foreach (var k in rolled.Skip(keep))
            if (keep < MostPerCube || k is PoiKind.TreasureCave or PoiKind.Haven) state.Carried.Add(k);
        state.Clam |= granted.Contains(PoiKind.TreasureCave);
        state.Havens += granted.Count(k => k == PoiKind.Haven);
        return granted;
    }

    /// <summary>A layer she can never come back to: its counts let go.</summary>
    public void Forget(int layer) => _layers.Remove(layer);
}
