using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// An octopus (Ottilie, docs/NPC-BOSSES-PROPOSAL.md §7): a soft bulbous mantle behind a broad web, and eight arms
/// radiating from it, each a chain of tapering segments that curls and sways, pale suckers along its underside (no eyes
/// drawn: the arms tell it). Her pearl is held behind her; on the hoard throw's warning three arms curl back, on the
/// reach's two arms turn to their lit lines and shoot out along them, and after each act she lifts the pearl up high to
/// admire it. The Leak's mutation: arms too many, thinner and inked, crowding in between her own, and ink sacs
/// swelling between them at her back.
/// </summary>
public partial class OctopusRig : BossRig
{
    const int Segments = 5;

    sealed class Arm
    {
        public Node3D Root = null!;
        public readonly List<Node3D> Joints = new();
        public float Angle, Phase, Extend, Curl;
    }

    readonly List<Arm> _arms = new();
    Node3D _body = null!, _mantle = null!;
    float _lift, _swim;

    float Web => R * 0.34f;
    float Reach => R * 1.05f;

    public override Node3D CrustParent => _mantle;

    public override Vector3 Surface(float angle, float out_)
    {
        float r = out_ * R;
        float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
        float h = r < Web * 1.6f ? 0.55f + 0.5f * (1f - r / (Web * 1.6f)) : 0.25f;
        return new Vector3(x, h, z);
    }

    protected override void BuildBody()
    {
        _body = new Node3D();
        AddChild(_body);
        // The web: a low soft dome where the arms meet.
        Part(BossMeshes.Lathe(t => new Vector2(Web * 1.5f * Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f), 0.45f * Mathf.Cos(t * Mathf.Pi * 0.5f) + 0.15f), 6, 32,
            (p, u, v) => new Color(Stain(p), 0.1f, 0.2f * v)), _body);
        // The mantle: a soft bulb behind the web, lifted a little, mottled.
        _mantle = new Node3D { Position = new Vector3(-Web * 0.9f, 0.55f, 0f), Rotation = new Vector3(0f, 0f, 0.35f) };
        _body.AddChild(_mantle);
        var mantle = BossMeshes.Lathe(t => new Vector2(Web * 1.15f * Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Clamp(t, 0.01f, 0.99f)), 0.7f), Web * 1.6f * (1f - t)), 12, 28,
            (p, u, v) => new Color(Stain(_mantle.Transform * p), 0.18f * Mathf.Pow(Mathf.Abs(Mathf.Sin(p.X * 7f) * Mathf.Sin(p.Z * 6f + p.Y * 5f)), 3f), 0.25f));
        Part(mantle, _mantle).Rotation = new Vector3(0f, 0f, -Mathf.Pi * 0.5f);
        for (int i = 0; i < Spec.Count; i++) AddArm(Mathf.Tau * (i + 0.5f) / Spec.Count, Reach, 0.2f * R, false, i * 1.3f);
        PearlHeight = 0.6f;
    }

    /// <summary>One arm out from the web at its angle: a chain of segments, tapering, its suckers pale on its underside.</summary>
    Arm AddArm(float angle, float length, float root, bool inked, float phase)
    {
        var arm = new Arm { Angle = angle, Phase = phase };
        arm.Root = new Node3D { Position = new Vector3(Mathf.Cos(angle) * Web, 0.25f, Mathf.Sin(angle) * Web), Rotation = new Vector3(0f, -angle, 0f) };
        _body.AddChild(arm.Root);
        Node3D parent = arm.Root;
        float seg = length / Segments;
        for (int k = 0; k < Segments; k++)
        {
            var joint = new Node3D { Position = k == 0 ? Vector3.Zero : new Vector3(seg, 0f, 0f) };
            parent.AddChild(joint);
            float r0 = root * (1f - 0.8f * k / Segments) + 0.02f, r1 = root * (1f - 0.8f * (k + 1) / Segments) + 0.02f;
            var path = BossMeshes.Path(t => new Vector3(t * seg * 1.05f, 0f, 0f), 4);
            var mesh = BossMeshes.Tube(path, t => Mathf.Lerp(r0, r1, t), 8,
                inked ? Inked(0.4f) : (p, t, a) =>
                {
                    // Pale suckers on its underside (around 0.75), in a row.
                    float under = Mathf.Max(0f, -Mathf.Sin(a * Mathf.Tau));
                    float sucker = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.Pi * 3f)), 4f) * under;
                    return new Color(Stain(arm.Root.Transform * p), sucker, 0.15f);
                }, _ => 0.75f);
            Part(mesh, joint);
            arm.Joints.Add(joint);
            parent = joint;
        }
        _arms.Add(arm);
        return arm;
    }

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        if (theme != MutationTheme.ExtraArms) return null;
        // An arm too many, thinner, inked through, crowding in between her own.
        var arm = AddArm(site.Angle, Reach * (0.65f + 0.3f * site.Size), 0.13f * R, true, site.Angle * 2.3f);
        return arm.Root;
    }

    public override void Pose(in RigPose pose)
    {
        var boss = pose.Boss;
        bool fighting = pose.Stage == BossStage.Fight;
        var coming = fighting && boss is not null ? PlaneWorld.Coming(boss) : null;
        float warn = pose.Act == BossAct.Telegraph ? pose.Warn
            : pose.Act == BossAct.Act && boss is not null ? Mathf.Clamp(pose.ActTime / Mathf.Max(boss.Spec.VolleyInterval, 0.01f), 0f, 1f) : 0f;
        // After each act she lifts her pearl up high to admire it.
        float lift = fighting && pose.Act == BossAct.Window ? 1f : 0f;
        _lift = Mathf.MoveToward(_lift, lift, pose.Dt * 3f);
        PearlHeight = 0.6f + 1.8f * Mathf.SmoothStep(0f, 1f, _lift);
        _swim += pose.Dt * (pose.Stage == BossStage.Freed ? 3f : 1f);
        // The reach: the two arms nearest its lit lines turn to them; on it they shoot out along them.
        var reach = new List<float>();
        if (boss is not null && fighting)
        {
            float facing = Mathf.Atan2(boss.Facing.Y, boss.Facing.X);
            foreach (var dir in boss.ReachDirs) reach.Add(Mathf.Wrap(Mathf.Atan2(dir.Y, dir.X) - facing, -Mathf.Pi, Mathf.Pi));
        }
        foreach (var arm in _arms)
        {
            // The arm nearest each lit line takes it.
            float? line = null;
            foreach (float a in reach)
                if (Nearest(a) == arm) line = a;
            float target = line is { } l ? Mathf.LerpAngle(arm.Angle, l, Mathf.SmoothStep(0f, 1f, warn)) : arm.Angle;
            arm.Root.Rotation = new Vector3(0f, -target, 0f);
            // Shot out on the reach, drawn back after.
            arm.Extend = line is not null && warn > 0.97f ? 1f : Mathf.MoveToward(arm.Extend, 0f, pose.Dt * 2.5f);
            arm.Root.Scale = new Vector3(1f + 3.6f * arm.Extend, 1f, 1f);
            // The hoard throw's warning: three arms at her back curl up and over.
            bool throwing = coming == BossAttack.ArcBombs && Mathf.Abs(Mathf.Wrap(arm.Angle - Mathf.Pi, -Mathf.Pi, Mathf.Pi)) < 1.2f;
            arm.Curl = Mathf.MoveToward(arm.Curl, throwing ? warn : 0f, pose.Dt * 3f);
            for (int k = 0; k < arm.Joints.Count; k++)
            {
                float sway = 0.18f * Mathf.Sin(_swim * 1.6f + arm.Phase + k * 0.7f) * (line is not null ? 0.2f : 1f);
                float curl = 0.45f * arm.Curl + (pose.Stage == BossStage.Freed ? 0.2f * Mathf.Sin(_swim * 3f + k) : 0f);
                arm.Joints[k].Rotation = new Vector3(0f, sway, curl + (k == 0 ? 0.08f : 0.03f) * Mathf.Sin(_swim + arm.Phase));
            }
        }
        // The mantle breathes.
        _mantle.Scale = Vector3.One * (1f + 0.04f * Mathf.Sin(pose.Time * 1.8f));
    }

    Arm? Nearest(float angle)
    {
        Arm? best = null;
        float d = float.MaxValue;
        // Only her own arms reach (the extra ones are thinner, the Leak's).
        for (int i = 0; i < Spec.Count && i < _arms.Count; i++)
        {
            float e = Mathf.Abs(Mathf.Wrap(_arms[i].Angle - angle, -Mathf.Pi, Mathf.Pi));
            if (e < d)
            {
                d = e;
                best = _arms[i];
            }
        }
        return best;
    }
}
