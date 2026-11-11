using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Saves;
using OctoShoots.Game.TopDown.Bosses;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// The bay's folk at home (docs/FOLK.md §1): each neighbour she has freed lives at her home in Lantern
/// Bay, her true body (the boss's rig with its mutations melted away, at its freed size) drifting there, turning to
/// Clementine as she comes near. Who is near enough to talk to is the game's to show (FolkTalk).
/// </summary>
public partial class FolkView : Node3D
{
    /// <summary>How near her home Clementine must swim for a neighbour to talk (metres), and how far she turns to watch her.</summary>
    public const float TalkReach = 5f, WatchReach = 12f;

    sealed class Home
    {
        public Neighbour Folk = null!;
        public BossSpec Spec = null!;
        public BossRig Rig = null!;
        public System.Numerics.Vector2 At;
        public float Facing, Time;
    }

    readonly List<Home> _homes = new();

    /// <summary>The neighbour Clementine is near enough to talk to, and her spec (for the portrait), or null.</summary>
    public (Neighbour Folk, BossSpec Spec)? Near { get; private set; }

    /// <summary>A place's homes: those of the folk she has freed (none away from the bay).</summary>
    public void Show(LevelMap map, HandLayout? hand, Jelly? jelly)
    {
        foreach (var h in _homes) h.Rig.QueueFree();
        _homes.Clear();
        Near = null;
        if (map.Haven is null || hand is null || jelly is null) return;
        foreach (var point in hand.Homes)
        {
            if (Folk.Get(point.Folk) is not { } n || !jelly.Freed.Contains(n.Id) || FolkBosses.For(n.Id, n.Level, 0f) is not { } spec) continue;
            var rig = Rigs.For(spec);
            AddChild(rig);
            rig.Build(spec);
            // Her true body: no mutation, true colours, her freed size.
            foreach (var (node, _, _) in rig.Mutations) node.Visible = false;
            rig.SetSkin(0f, 0f, 0f, 1f);
            rig.Scale = Vector3.One * rig.TrueScale;
            var at = new System.Numerics.Vector2(point.X, point.Y);
            _homes.Add(new Home { Folk = n, Spec = spec, Rig = rig, At = at, Facing = Mathf.Pi * 0.5f });
        }
    }

    /// <summary>The neighbour whose home Clementine is at (within reach of it), or null.</summary>
    public Neighbour? AtHome(System.Numerics.Vector2 her, float reach)
    {
        foreach (var h in _homes)
            if (System.Numerics.Vector2.Distance(her, h.At) < reach) return h.Folk;
        return null;
    }

    public void Sync(PlaneWorld world, float dt)
    {
        Near = null;
        var her = world.Player.Position;
        foreach (var h in _homes)
        {
            h.Time += dt;
            float d = System.Numerics.Vector2.Distance(her, h.At);
            // She turns slowly to watch Clementine when she is near; otherwise she faces out over the lagoon.
            if (d < WatchReach && d > 0.5f) h.Facing = Mathf.LerpAngle(h.Facing, Mathf.Atan2(her.Y - h.At.Y, her.X - h.At.X), 1f - Mathf.Exp(-1.5f * dt));
            float bob = 0.12f * Mathf.Sin(h.Time * 0.8f);
            h.Rig.Position = new Vector3(h.At.X, LevelMap.SwimBand - 0.55f + bob, h.At.Y);
            h.Rig.Rotation = new Vector3(0f, -h.Facing, 0f);
            // At rest at home: the body's own idle (not the freeing's swim away).
            h.Rig.Pose(new RigPose { Stage = BossStage.Waiting, Act = BossAct.Rest, Melt = 1f, Speed = 0f, Time = h.Time, Dt = dt });
            if (d < TalkReach && Near is null) Near = (h.Folk, h.Spec);
        }
    }
}
