using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Bosses;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>
/// A green sea turtle (Grandmother Tull, docs/NPC-BOSSES-PROPOSAL.md §4): a high domed shell of scutes over a pale
/// plastron, a blunt head on a short neck (no eyes drawn: the body tells it), two long front flippers and two short rear
/// ones. Her tells come from the body: a front flipper draws back before it sweeps, the whole shell lifts before it
/// drops, and in the sigh her head stretches out from under the lip. Creeping, her flippers paddle in turn; freed, they
/// row slow, wide strokes. The Leak's mutation is the shell grown past her: plates piled on plates along its seams.
/// </summary>
public partial class TurtleRig : BossRig
{
    Node3D _body = null!, _shell = null!, _neck = null!;
    readonly List<(Node3D Pivot, float Side, bool Front)> _flippers = new();
    float _lift, _drop = 1f, _reach, _draw, _sweep = 1f, _paddle;
    int _sweepSide = 1;

    /// <summary>The shell's half length and half width (m), and its height.</summary>
    float Long => R * Spec.Stretch * 0.86f;
    float Wide => R * 0.82f;
    float High => R * 0.42f;

    public override Node3D CrustParent => _shell;

    public override Vector3 Surface(float angle, float out_)
    {
        float r = Mathf.Clamp(out_, 0f, 1f);
        float x = Mathf.Cos(angle) * r * Long, z = Mathf.Sin(angle) * r * Wide;
        return new Vector3(x, High * Mathf.Sqrt(Mathf.Max(0f, 1f - r * r)) + 0.12f, z);
    }

    protected override void BuildBody()
    {
        _body = new Node3D();
        AddChild(_body);
        _shell = new Node3D { Position = new Vector3(0f, 0.15f, 0f) };
        _body.AddChild(_shell);
        // The carapace: a domed lathe stretched to an oval, its scutes drawn in the accent along their seams.
        var carapace = BossMeshes.Lathe(t => new Vector2(Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f), High * Mathf.Cos(t * Mathf.Pi * 0.5f)), 12, 40,
            (p, u, v) =>
            {
                var at = new Vector3(p.X * Long, p.Y, p.Z * Wide);
                return new Color(Stain(at), Scutes(u, v) * 0.85f, 0.35f * Scutes(u, v));
            });
        Part(carapace, _shell).Scale = new Vector3(Long, 1f, Wide);
        // The plastron: a low pale dome under it, in her skin's colour.
        var plastron = BossMeshes.Lathe(t => new Vector2(Mathf.Sin(t * Mathf.Pi * 0.5f + 0.001f), -0.18f * Mathf.Cos(t * Mathf.Pi * 0.5f)), 4, 32,
            (_, _, _) => new Color(0f, 1f, 0.1f));
        Part(plastron, _shell).Scale = new Vector3(Long * 0.92f, 1f, Wide * 0.88f);

        // The head on its neck, out from under the shell's front lip.
        _neck = new Node3D { Position = new Vector3(Long * 0.86f, 0.3f, 0f) };
        _body.AddChild(_neck);
        float head = R * 0.17f;
        var neck = BossMeshes.Tube(BossMeshes.Path(t => new Vector3(t * R * 0.42f, 0.08f * Mathf.Sin(t * Mathf.Pi), 0f), 10),
            t => head * (1.15f - 0.2f * t + 0.35f * Mathf.Pow(Mathf.Max(0f, t - 0.6f) / 0.4f, 2f) * (1f - t) * 4f) * (t > 0.97f ? 0.3f : 1f), 12,
            (p, t, a) => new Color(0f, 0.75f + 0.25f * Scales(p), 0.25f * Scales(p)), _ => 0.85f);
        Part(neck, _neck);

        // Flippers: the long front pair, the short rear pair, each on a pivot at the shell's edge.
        for (int side = -1; side <= 1; side += 2)
        {
            AddFlipper(new Vector3(Long * 0.42f, 0.2f, side * Wide * 0.8f), side, true);
            AddFlipper(new Vector3(-Long * 0.72f, 0.15f, side * Wide * 0.62f), side, false);
        }
        PearlHeight = 0.45f;
    }

    void AddFlipper(Vector3 at, float side, bool front)
    {
        var pivot = new Node3D { Position = at };
        _body.AddChild(pivot);
        float length = front ? R * 1.05f : R * 0.45f, chord = front ? R * 0.2f : R * 0.16f;
        // Out to her side and swept back, a paddle flattened to a blade.
        var path = BossMeshes.Path(t => new Vector3(-t * length * (front ? 0.45f : 0.7f), -0.05f * t, side * t * length), 12);
        var mesh = BossMeshes.Tube(path, t => chord * (0.75f + 0.5f * Mathf.Sin(Mathf.Pi * Mathf.Min(t * 1.4f, 1f))) * (1f - 0.75f * t * t) + 0.02f, 10,
            (p, t, a) => new Color(0f, 0.7f + 0.3f * Scales(p), 0.3f * Scales(p)), _ => 0.28f);
        Part(mesh, pivot);
        _flippers.Add((pivot, side, front));
    }

    /// <summary>The scutes' seams over the shell (u round it, v from its crown to its rim): the accent in the seams.</summary>
    static float Scutes(float u, float v)
    {
        // The ring between the back's scutes and the costals, the marginals' ring at the rim, and the radial seams.
        float ring = Mathf.Max(Line(v, 0.45f, 0.035f), Line(v, 0.82f, 0.03f));
        float spokes = v > 0.45f ? Line(Mathf.PosMod(u * (v > 0.82f ? 22f : 8f), 1f), 0.5f, v > 0.82f ? 0.08f : 0.05f) : Line(Mathf.PosMod(u * 4f + 0.5f, 1f), 0.5f, 0.04f / Mathf.Max(v, 0.08f));
        return Mathf.Max(ring, spokes * Mathf.Clamp(v * 3f, 0f, 1f));
    }

    static float Line(float x, float at, float width) => Mathf.Clamp(1f - Mathf.Abs(x - at) / width, 0f, 1f);

    /// <summary>A scaly pattern over skin: the accent with pale scale edges.</summary>
    static float Scales(Vector3 p) => Mathf.Pow(Mathf.Abs(Mathf.Sin(p.X * 9f) * Mathf.Sin(p.Z * 9f + p.Y * 7f)), 3f);

    protected override Node3D? Mutation(MutationTheme theme, BodySite site)
    {
        if (theme != MutationTheme.OvergrownShell) return null;
        // Plates on plates, piled crooked past the shell along its seams, inked through.
        var pile = new Node3D { Position = Surface(site.Angle, site.Out), Rotation = new Vector3(0.25f * Mathf.Sin(site.Angle * 3f), site.Angle, 0.3f * Mathf.Cos(site.Angle * 2f)) };
        _shell.AddChild(pile);
        float s = R * (0.18f + 0.2f * site.Size) * (0.8f + 0.5f * Spec.Mutation);
        int plates = 3 + (int)(site.Size * 3f);
        for (int i = 0; i < plates; i++)
        {
            var plate = Part(BossMeshes.Lathe(t => new Vector2(Mathf.Min(1f, t * 3f), 0.22f * (1f - t)), 3, 6, Inked(0.6f)), pile);
            float k = 1f - 0.18f * i;
            plate.Position = new Vector3(0.12f * s * Mathf.Sin(i * 2.1f), 0.12f * s * i, 0.12f * s * Mathf.Cos(i * 1.7f));
            plate.Rotation = new Vector3(0.2f * Mathf.Sin(i * 1.3f), i * 0.6f, 0.2f * Mathf.Cos(i * 0.9f));
            plate.Scale = new Vector3(s * k, s * k, s * k * 0.9f);
        }
        return pile;
    }

    public override void Pose(in RigPose pose)
    {
        var boss = pose.Boss;
        bool fighting = pose.Stage == BossStage.Fight;
        // What is coming, and how near it is: the telegraph's own, or the pause before her act's next volley.
        var coming = fighting && boss is not null ? PlaneWorld.Coming(boss) : null;
        float warn = pose.Act == BossAct.Telegraph ? pose.Warn
            : pose.Act == BossAct.Act && boss is not null ? Mathf.Clamp(pose.ActTime / Mathf.Max(boss.Spec.VolleyInterval, 0.01f), 0f, 1f) : 0f;
        bool fired = pose.Fired && fighting;
        // The slam: the shell lifts through its warning and drops when it strikes.
        float lift = coming == BossAttack.Ring ? Mathf.SmoothStep(0f, 1f, warn) : 0f;
        if (fired && _lift > 0.3f) _drop = 0f;
        _lift = Mathf.MoveToward(_lift, lift, pose.Dt * (lift > _lift ? 2.5f : 12f));
        _drop = Mathf.MoveToward(_drop, 1f, pose.Dt / 0.35f);
        float squash = 1f - 0.12f * Mathf.Sin(_drop * Mathf.Pi) * (_drop < 1f ? 1f : 0f);
        _body.Position = new Vector3(0f, 0.9f * _lift, 0f);
        _body.Scale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));

        // The sweep: the flipper on the side she was last felt draws back, then rows forward hard.
        if (coming == BossAttack.Fan && boss is not null)
        {
            var to = boss.Heard - boss.Position;
            _sweepSide = boss.Facing.X * to.Y - boss.Facing.Y * to.X >= 0f ? 1 : -1;
        }
        float draw = coming == BossAttack.Fan ? Mathf.SmoothStep(0f, 1f, warn) : 0f;
        if (fired && _draw > 0.3f) _sweep = 0f;
        _draw = Mathf.MoveToward(_draw, draw, pose.Dt * (draw > _draw ? 2.5f : 6f));
        _sweep = Mathf.MoveToward(_sweep, 1f, pose.Dt / 0.3f);

        // The sigh: her head stretches out from under the lip and lifts; drawn in while she warns and strikes.
        float reach = !fighting ? 0.55f : pose.Act == BossAct.Window ? 1f : pose.Act is BossAct.Telegraph or BossAct.Act ? 0f : 0.45f;
        _reach = Mathf.MoveToward(_reach, reach, pose.Dt * 2f);
        float breathe = pose.Act == BossAct.Window ? 0.04f * Mathf.Sin(pose.Time * 3f) : 0f;
        _neck.Position = new Vector3(Long * (0.62f + 0.32f * _reach), 0.25f + 0.25f * _reach, 0f);
        _neck.Rotation = new Vector3(0f, 0f, 0.2f * _reach + breathe);
        _shell.Scale = new Vector3(1f, 1f + breathe, 1f);

        // Creeping her flippers paddle in turn; freed she rows slow, wide strokes; still, they stir with the water.
        bool rowing = pose.Stage == BossStage.Freed;
        _paddle += pose.Dt * (rowing ? 1.6f : pose.Speed > 0.05f && pose.Act == BossAct.Rest ? 2.6f : 0.6f);
        foreach (var (pivot, side, front) in _flippers)
        {
            float stroke = rowing ? 0.55f * Mathf.Sin(_paddle) : pose.Speed > 0.05f && pose.Act == BossAct.Rest ? 0.3f * Mathf.Sin(_paddle + (front ? 0f : Mathf.Pi) + side) : 0.05f * Mathf.Sin(_paddle + side);
            float yaw = (front ? 1f : 0.6f) * stroke * side;
            float flap = rowing && front ? 0.35f * Mathf.Cos(_paddle) : 0f;
            if (front && (int)side == _sweepSide)
            {
                // Drawn back before the sweep (the flipper turned to point behind her), rowed through forward on it.
                yaw += side * (0.9f * _draw - 1.3f * (1f - _sweep) * (_sweep < 1f ? 1f : 0f));
            }
            // Lifting the shell, the flippers brace out wide.
            pivot.Rotation = new Vector3(side * (0.25f * _lift + flap), yaw, 0f);
        }
    }
}
