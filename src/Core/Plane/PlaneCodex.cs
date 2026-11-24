using System.Collections.Generic;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;

namespace OctoShoots.Core.Plane;

/// <summary>
/// What she finds for the codex (Codex.cs): a place she swims into, a creature she faces, a fish, a plant or a water she
/// sees, the biome she reaches, the first time on the level each is met (the profile keeps them across runs:
/// PlaneProfileRecorder). Seen: within SeeReach and in sight; a place: within its radius and Enter.
/// </summary>
public sealed partial class PlaneWorld
{
    /// <summary>Codex ids found this step (each once a level).</summary>
    public List<string> Discoveries { get; } = new();

    readonly HashSet<string> _codexFound = new();

    /// <summary>How near (in sight) a thing must be to be seen; how far inside a place's own radius she must be; how often (steps) the world is looked over.</summary>
    public const float SeeReach = 9f, EnterReach = 1.5f;
    const int LookEvery = 6;

    void Found(string id)
    {
        if (Codex.Has(id) && _codexFound.Add(id)) Discoveries.Add(id);
    }

    bool Sees(Vector2 at, float reach = SeeReach) =>
        Vector2.DistanceSquared(Player.Position, at) <= reach * reach && LineOfSight(Player.Position, at);

    /// <summary>The step's finds: what the events tell (a vault burst, a creature struck), and every few steps a look round.</summary>
    void StepCodex()
    {
        Discoveries.Clear();
        if (Map.Templated) Found(Codex.Biome(Map.Biome));
        foreach (var e in Events)
            if (e.Type == PlaneEventType.VaultBurst) Found("place:Vault");
        if (Tick % LookEvery != 0) return;
        var her = Player.Position;

        foreach (var poi in Map.Pois)
            if (poi.Kind is not (PoiKind.Start or PoiKind.Exit or PoiKind.Hole) && Vector2.Distance(her, poi.Position) <= poi.Radius + EnterReach)
                Found(Codex.Place(poi.Kind));
        foreach (var s in Springs)
            if (Vector2.Distance(her, s.Position) <= SpringTuning.Radius + EnterReach) Found("place:Spring");
        foreach (var r in LightRooms)
            if (Vector2.Distance(her, r.Centre) <= FloraTuning.Ring + EnterReach) Found("place:LightRoom");
        foreach (var v in Vaults)
            if (Sees(v.Position, 5f)) Found("place:Vault");

        foreach (var m in Murklings)
        {
            if (!m.Alive || m.Budding || !Sees(m.Position)) continue;
            Found(Codex.Murk(m));
            if (m.Trait != MurkTrait.None) Found("trait:" + m.Trait);
        }
        foreach (var b in Blotters) if (b.Alive && Sees(b.Position)) Found("mob:blotter");
        foreach (var r in Blightroots) if (Sees(r.Position)) Found("mob:blightroot");
        foreach (var t in Thumpsquirts) if (t.Alive && Sees(t.Position)) Found("mob:thumpsquirt");
        foreach (var w in Inkwardens) if (w.Alive && Sees(w.Position)) Found("mob:inkwarden");
        foreach (var l in Lashjaws) if (Sees(l.Position)) Found("mob:lashjaw");
        foreach (var mob in Mobs) if (mob.Alive && Sees(mob.Position)) Found("mob:pufferling");
        foreach (var f in Flounders) if (Sees(FlounderSeen(f))) Found(f.Corrupted ? "mob:hearthguard" : "fish:Flounder");
        if (Boss is { } boss && Sees(boss.Position, SeeReach * 2f)) Found(Codex.Boss(boss.Spec.Concept));

        foreach (var fish in ReefFish)
            if (!fish.Gone && Sees(fish.Position)) Found(fish.Corrupted ? "murk:lesser" : "fish:" + fish.Kind);
        foreach (var a in Fauna) if (Sees(a.Position)) Found("fauna:" + a.Kind);
        foreach (var f in Flora) if (Sees(f.Position)) Found("flora:" + f.Kind);
        foreach (var w in Pockets) if (Sees(w.Centre, SeeReach + w.Radius)) Found("water:" + w.Kind);
        foreach (var o in OpticWalls) if (Sees(o.Centre)) Found("wall:" + o.Kind);
    }
}
