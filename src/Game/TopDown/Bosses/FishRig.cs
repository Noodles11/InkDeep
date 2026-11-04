using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// A fish (docs/NPC-BOSSES-PROPOSAL.md). A cleaner wrasse (Kip): slender, pale, a black band along each flank under an
/// electric-blue stripe, a long low dorsal and anal fin, small pectorals, a rounded tail. It hangs in the water over its
/// station and bobs there (the cleaner's dance); on the dart's warning its fins tuck in flat, it shoots off with its
/// tail thrashing, and waiting after it it rolls a little onto its side, the stripe toward the light. The Leak's mutation
/// is fins too many: ragged inked fins crowding out of its flanks.
/// A leafy seadragon (Fennel): a thin body in a gentle S, a long tube of a snout, a tapering tail, leaf-shaped fronds
/// all along it, and his clutch of pink eggs under his tail (the Leak never touched them). He drifts like loose weed;
/// on the lash's warning his fronds stiffen and rattle, and he whips round on it. The Leak's mutation: fronds hardened
/// into thorns driven back into him.
/// </summary>
public partial class FishRig : BossRig
{
    Node3D _body = null!, _tail = null!;
    readonly List<(Node3D Pivot, float Side)> _pectorals = new();
    readonly List<(Node3D Pivot, float Side, float Phase)> _leaves = new();
    float _beat, _tuck, _roll, _bob, _swim, _stiff, _whip = 1f;
    bool Dragon => Spec.Form == BodyForm.Seadragon;

    float Long => R * Spec.Stretch;
    float Thick => R * 0.36f;
    /// <summary>How high over its seat it hangs.</summary>
    float Hang => 0.9f;

    public override Node3D CrustParent => _body;

    public override Vector3 Surface(float angle, float out_)
    {
        float along = Mathf.Cos(angle) * out_, across = Mathf.Sin(angle) * out_;
        if (Dragon)
        {
            float t = (along + 1f) * 0.5f;
            return Spine(t) + new Vector3(0f, Hang + DragonGirth(t) * 0.5f, Mathf.Clamp(across, -1f, 1f) * DragonGirth(t));
        }
        float r = Girth((along + 1f) * 0.5f);
        return new Vector3(along * Long, Hang + r * 0.6f, Mathf.Clamp(across, -1f, 1f) * r);
    }

    /// <summary>Its half thickness along it (t 0 the tail's root, 1 its snout): fusiform, blunt at the snout.</summary>
    float Girth(float t) => Thick * Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Clamp(0.08f + 0.88f * t, 0f, 1f)), 0.75f) * (t > 0.92f ? 1f - (t - 0.92f) * 4f : 1f) + 0.03f;

    /// <summary>A seadragon's spine (t 0 the tail's tip, 1 the snout's), in a gentle S across its facing.</summary>
    Vector3 Spine(float t) => new(Mathf.Lerp(-Long, Long, t), 0f, R * 0.22f * Mathf.Sin(t * 5.5f + 0.3f) * (1f - 0.6f * t));

    /// <summary>A seadragon's half thickness along it: a thin tail, a deeper body, a thin neck, a small head, a long thin snout.</summary>
    float DragonGirth(float t)
    {
        float Bump(float c, float w) => Mathf.Exp(-Mathf.Pow((t - c) / w, 2f));
        float g = R * (0.03f + 0.15f * Bump(0.52f, 0.2f) + 0.1f * Bump(0.8f, 0.05f));
        return t > 0.85f ? R * 0.045f : g;
    }

    protected override void BuildBody()
    {
        _body = new Node3D { Position = new Vector3(0f, Hang, 0f) };
        AddChild(_body);
        if (Dragon)
        {
            BuildSeadragon();
            return;
        }
        // The body, tail root to snout along +X, a little taller than wide; its stripe and band painted along the flanks.
        var path = BossMeshes.Path(t => new Vector3(Mathf.Lerp(-Long, Long, t), 0f, 0f), 28);
        var body = BossMeshes.Tube(path, Girth, 16,
            (p, t, around) =>
            {
                // around: 0 and 0.5 the flanks, 0.25 the back, 0.75 the belly.
                float flank = Mathf.Abs(Mathf.Cos(around * Mathf.Tau));
                float up = Mathf.Sin(around * Mathf.Tau);
                // The black band along the middle of each flank, widening toward the tail; the blue stripe just over it.
                float band = Mathf.SmoothStep(0.55f - 0.3f * (1f - t), 0.9f - 0.15f * (1f - t), flank) * (t < 0.95f ? 1f : 0f) * (up < 0.25f && up > -0.35f ? 1f : 0f);
                float stripe = flank > 0.4f && up > 0.18f && up < 0.42f ? 1f : 0f;
                return new Color(Stain(new Vector3(p.X, 0f, p.Z)), stripe, band);
            }, _ => 1.2f);
        Part(body, _body);
        // The long low dorsal and anal fins.
        foreach (float up in new[] { 1f, -1f })
        {
            var fin = BossMeshes.Sheet((u, v) =>
            {
                float x = Mathf.Lerp(-Long * 0.85f, Long * 0.45f, u);
                float h = Girth((x / Long + 1f) * 0.5f) * 1.15f;
                return new Vector3(x, up * (h + v * R * 0.22f * Mathf.Sin(Mathf.Pi * Mathf.Clamp(u * 1.1f, 0f, 1f))), 0f);
            }, 14, 3, (_, u, v) => new Color(0f, 0.35f * v, 0.5f + 0.4f * v));
            Part(fin, _body);
        }
        // The tail, rounded, on a pivot that beats.
        _tail = new Node3D { Position = new Vector3(-Long * 0.95f, 0f, 0f) };
        _body.AddChild(_tail);
        var tail = BossMeshes.Sheet((u, v) =>
        {
            float a = Mathf.Lerp(-1.1f, 1.1f, v);
            float r = R * 0.62f * u;
            return new Vector3(-Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.9f, 0f);
        }, 6, 10, (_, u, v) => new Color(0f, 0.2f * u, 0.85f * u));
        Part(tail, _tail);
        // The pectorals: small fans behind the head, held out flat (they read from above), tucking in on the warning.
        for (int side = -1; side <= 1; side += 2)
        {
            var pivot = new Node3D { Position = new Vector3(Long * 0.5f, -Thick * 0.2f, side * Thick * 0.85f) };
            _body.AddChild(pivot);
            var pec = BossMeshes.Sheet((u, v) => new Vector3(-u * R * 0.4f, 0f, side * (0.1f + u * R * 0.3f) * (0.6f + 0.4f * Mathf.Sin(v * Mathf.Pi))), 6, 5,
                (_, u, _) => new Color(0f, 0.25f, 0.3f * u));
            Part(pec, pivot);
            _pectorals.Add((pivot, side));
        }
        PearlHeight = Hang + Thick * 0.5f;
    }

    void BuildSeadragon()
    {
        // The body along its spine, ochre, banded faintly in pale rings.
        var path = BossMeshes.Path(Spine, 36);
        Part(BossMeshes.Tube(path, DragonGirth, 12, (p, t, _) => new Color(Stain(p), 0.15f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 60f)), 6f), 0.2f), _ => 1.15f), _body);
        // The fronds: leaf-shaped, lobed, out of both sides from its neck to its tail, and two on its head.
        for (int i = 0; i < Spec.Count; i++)
        {
            float t = 0.12f + 0.72f * i / Mathf.Max(Spec.Count - 1, 1);
            float side = i % 2 == 0 ? 1f : -1f;
            AddLeaf(t, side, R * (0.38f + 0.22f * Mathf.Sin(t * Mathf.Pi)), i * 1.7f);
        }
        AddLeaf(0.8f, 1f, R * 0.28f, 9f);
        AddLeaf(0.8f, -1f, R * 0.28f, 11f);
        // His eggs: a clutch of pink beads under his tail, in their own colour (the Leak never touched them).
        var eggs = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.55f, 0.6f), Roughness = 0.35f, EmissionEnabled = true, Emission = new Color(0.95f, 0.45f, 0.55f), EmissionEnergyMultiplier = 0.25f };
        var bead = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 5 };
        for (int k = 0; k < 18; k++)
        {
            float t = 0.2f + 0.14f * (k / 17f);
            var at = Spine(t) + new Vector3(0f, -DragonGirth(t) * 0.7f, (k % 3 - 1) * DragonGirth(t) * 0.7f);
            var egg = Part(bead, _body, eggs);
            egg.Position = at;
            egg.Scale = Vector3.One * R * 0.045f;
        }
        PearlHeight = Hang;
    }

    /// <summary>A frond: a lobed leaf out of the body's side (side -1, 1) at t along it, on a pivot that sways it.</summary>
    void AddLeaf(float t, float side, float length, float phase)
    {
        var a = Spine(t);
        var pivot = new Node3D { Position = a + new Vector3(0f, DragonGirth(t) * 0.3f, side * DragonGirth(t)) };
        _body.AddChild(pivot);
        var leaf = BossMeshes.Sheet((u, v) =>
        {
            float w = length * 0.32f * Mathf.Sin(Mathf.Pi * u) * (1f + 0.35f * Mathf.Sin(3f * Mathf.Pi * u));
            return new Vector3(Mathf.Lerp(-w, w, v) - u * length * 0.25f, 0.12f * u * length, side * u * length);
        }, 8, 4, (_, u, v) => new Color(0f, 1f, 0.35f * Mathf.Pow(1f - Mathf.Abs(v * 2f - 1f), 6f)));
        Part(leaf, pivot);
        _leaves.Add((pivot, side, phase));
    }

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        if (Dragon && theme == MutationTheme.InvertedSpines)
        {
            // Fronds hardened into thorns, driven back into him: a cluster of dark spikes, their stubs standing out.
            var cluster = new Node3D { Position = Surface(site.Angle, site.Out) - new Vector3(0f, Hang, 0f) };
            _body.AddChild(cluster);
            var rng = new RandomNumberGenerator { Seed = (ulong)(site.Angle * 1000f + 11f) };
            int n = 4 + (int)(site.Size * 6f);
            for (int i = 0; i < n; i++)
            {
                var spike = Part(BossMeshes.Lathe(t => new Vector2(t, 1f - t), 2, 5, (_, _, _) => new Color(1f, 0f, 0.5f)), cluster);
                float tilt = rng.RandfRange(0.5f, 1.2f), turn = rng.RandfRange(0f, Mathf.Tau);
                spike.Rotation = new Vector3(tilt * Mathf.Cos(turn), 0f, tilt * Mathf.Sin(turn));
                spike.Scale = new Vector3(0.06f, (0.3f + site.Size) * (0.7f + 0.6f * Spec.Mutation), 0.06f);
            }
            return cluster;
        }
        if (theme != MutationTheme.ExtraFins) return null;
        // A fin too many: a ragged fan of spines and membrane out of the flank, splaying out where no fin belongs.
        var at = Surface(site.Angle, site.Out) - new Vector3(0f, Hang, 0f);
        var pivot = new Node3D { Position = at, Rotation = new Vector3(0f, -Mathf.Sign(Mathf.Sin(site.Angle)) * 0.4f, 0f) };
        _body.AddChild(pivot);
        float side = Mathf.Sign(Mathf.Sin(site.Angle));
        float len = R * (0.45f + 0.5f * site.Size) * (0.8f + 0.5f * Spec.Mutation);
        var fin = BossMeshes.Sheet((u, v) =>
        {
            float a = Mathf.Lerp(-0.7f, 0.7f, u);
            float r = len * v * (0.75f + 0.25f * Mathf.Abs(Mathf.Sin(u * 17f)));
            return new Vector3(Mathf.Sin(a) * r, 0.15f * r * Mathf.Cos(u * 5f), side * Mathf.Cos(a) * r);
        }, 10, 4, Inked(0.5f));
        Part(fin, pivot);
        return pivot;
    }

    public override void Pose(in RigPose pose)
    {
        if (Dragon)
        {
            PoseSeadragon(pose);
            return;
        }
        var boss = pose.Boss;
        bool fighting = pose.Stage == BossStage.Fight;
        bool darting = boss is { DartLeft: > 0f };
        // Its fins tuck in flat on the dart's warning (and stay in through it).
        var coming = fighting && boss is not null ? PlaneWorld.Coming(boss) : null;
        float warn = pose.Act == BossAct.Telegraph ? pose.Warn
            : pose.Act == BossAct.Act && boss is not null ? Mathf.Clamp(pose.ActTime / Mathf.Max(boss.Spec.VolleyInterval, 0.01f), 0f, 1f) : 0f;
        float tuck = darting ? 1f : coming == BossAttack.Dart ? Mathf.SmoothStep(0f, 1f, warn) : 0f;
        _tuck = Mathf.MoveToward(_tuck, tuck, pose.Dt * 4f);
        // Waiting after a dart (its window), it rolls onto its side a little, its stripe up to the light.
        float roll = fighting && pose.Act == BossAct.Window ? 0.55f : 0f;
        _roll = Mathf.MoveToward(_roll, roll, pose.Dt * 2.5f);
        // At its station it bobs up and down (the cleaner's dance); it holds level to dart.
        float bob = !darting && (pose.Act == BossAct.Rest || !fighting) ? 1f : 0f;
        _bob = Mathf.MoveToward(_bob, bob, pose.Dt * 2f);
        _swim += pose.Dt * (darting ? 16f : pose.Stage == BossStage.Freed ? 9f : 4f);
        float beat = Mathf.Sin(_swim) * (darting ? 0.6f : 0.25f);
        _body.Position = new Vector3(0f, Hang + 0.35f * _bob * Mathf.Sin(pose.Time * 3.2f), 0f);
        _body.Rotation = new Vector3(_roll * (boss is not null && boss.Spec.Pearl.Angle < 0f ? -1f : 1f), beat * 0.12f, 0.12f * _bob * Mathf.Cos(pose.Time * 3.2f));
        _tail.Rotation = new Vector3(0f, beat, 0f);
        foreach (var (pivot, side) in _pectorals)
            pivot.Rotation = new Vector3(0f, side * (0.4f * _tuck - 0.25f * Mathf.Sin(pose.Time * 5f + side)), 0f);
    }

    void PoseSeadragon(in RigPose pose)
    {
        var boss = pose.Boss;
        bool fighting = pose.Stage == BossStage.Fight;
        var coming = fighting && boss is not null ? PlaneWorld.Coming(boss) : null;
        float warn = pose.Act == BossAct.Telegraph ? pose.Warn
            : pose.Act == BossAct.Act && boss is not null ? Mathf.Clamp(pose.ActTime / Mathf.Max(boss.Spec.VolleyInterval, 0.01f), 0f, 1f) : 0f;
        // The lash's warning: his fronds stiffen out and rattle; on it he whips round.
        float stiff = coming == BossAttack.Fan ? Mathf.SmoothStep(0f, 1f, warn) : coming == BossAttack.Wall ? 0.5f * warn : 0f;
        _stiff = Mathf.MoveToward(_stiff, stiff, pose.Dt * 3f);
        if (pose.Fired && fighting) _whip = 0f;
        _whip = Mathf.MoveToward(_whip, 1f, pose.Dt / 0.4f);
        // Hidden, he hangs low and still in the weed; otherwise he drifts, rising and falling a little.
        bool hidden = boss is { Hidden: true };
        _bob = Mathf.MoveToward(_bob, hidden ? 0f : 1f, pose.Dt);
        _body.Position = new Vector3(0f, Hang * (0.75f + 0.25f * _bob) + 0.12f * _bob * Mathf.Sin(pose.Time * 0.9f), 0f);
        float whip = _whip < 1f ? 0.5f * Mathf.Sin(_whip * Mathf.Pi * 2f) * (1f - _whip) : 0f;
        _body.Rotation = new Vector3(0.04f * Mathf.Sin(pose.Time * 0.7f), whip + 0.05f * Mathf.Sin(pose.Time * 0.5f), 0f);
        foreach (var (pivot, side, phase) in _leaves)
        {
            float sway = 0.18f * Mathf.Sin(pose.Time * 1.1f + phase) * (1f - _stiff);
            float rattle = 0.08f * _stiff * Mathf.Sin(pose.Time * 38f + phase);
            pivot.Rotation = new Vector3(side * (sway + rattle - 0.25f * _stiff), 0.1f * Mathf.Sin(pose.Time * 0.8f + phase), 0f);
            pivot.Scale = Vector3.One * (1f + 0.2f * _stiff);
        }
    }
}
