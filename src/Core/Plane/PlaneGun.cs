using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// Her shots as a gun (PlaneOptions.Gun), with her light as its ammunition: each shot flies straight and fast to its
/// range and goes out there (no easing, no hovering, no merging), shoves what it hits hard (Knock × the creature's own
/// shove) and kicks her back against her aim (Recoil m/s for a plain shot). Her pearls set how it fires:
/// <list type="bullet">
/// <item>a plain shot: a pistol;</item>
/// <item>more shots a throw (Multishot, a cone): a <b>shotgun</b>, its pellets scattered a little (Scatter degrees, speeds
/// ± Jitter), short (ShotgunRange of the range), the kick ShotgunKick × a plain shot's;</item>
/// <item>a charged throw (Pearl Diver): a <b>rifle</b> round, faster and farther (× 1 + RifleBoost × the charge), shoving
/// harder (× 1 + RifleKnock × the charge), through what it hits once fully charged, the kick × 1 + RifleKick × the charge;</item>
/// <item>the stream (Sardine Run): a <b>machine gun</b>, each shot's kick MachineKick of a plain shot's.</item>
/// </list>
/// </summary>
public static class GunTuning
{
    /// <summary>A shot's speed and range against the light bubbles' (ShotSpeed, her throw reach).</summary>
    public const float Speed = 2.3f, Range = 1.6f;
    /// <summary>
    /// Its shove on a creature (× the bubbles' KnockFor), its kick back on her (m/s, a plain shot), and the most any
    /// volley kicks her (barely felt, however strong it is).
    /// </summary>
    public const float Knock = 4f, Recoil = 0.2f, RecoilMost = 0.5f;
    public const float Scatter = 4f, Jitter = 0.12f, ShotgunRange = 0.55f, ShotgunKick = 2.2f;
    public const float RifleBoost = 0.8f, RifleKnock = 2f, RifleKick = 2.5f;
    public const float MachineKick = 0.35f;
}

public sealed partial class PlaneWorld
{
    /// <summary>A gun's volley (PlaneOptions.Gun): each shot shaped by how it fires, and the kick back on her for the whole volley.</summary>
    void ShapeGunVolley(int first, Vector2 aim, float charge)
    {
        if (!Options.Gun || first >= Shots.Count) return;
        int count = Shots.Count - first;
        bool shotgun = count > 1 && !Run.Loadout.Shot.Wave, rifle = charge > 0.05f, machine = Run.Loadout.Flags.Contains("minigun");
        for (int i = first; i < Shots.Count; i++)
        {
            var s = Shots[i];
            float speed = s.Speed0 * GunTuning.Speed, range = s.Range * GunTuning.Range;
            var dir = s.Velocity.LengthSquared() > 1e-8f ? Vector2.Normalize(s.Velocity) : aim;
            if (shotgun)
            {
                float a = _bubbleRng.Range(-1f, 1f) * GunTuning.Scatter * MathF.PI / 180f;
                dir = new Vector2(dir.X * MathF.Cos(a) - dir.Y * MathF.Sin(a), dir.X * MathF.Sin(a) + dir.Y * MathF.Cos(a));
                speed *= 1f + _bubbleRng.Range(-1f, 1f) * GunTuning.Jitter;
                range *= GunTuning.ShotgunRange;
            }
            if (rifle)
            {
                speed *= 1f + GunTuning.RifleBoost * charge;
                range *= 1f + GunTuning.RifleBoost * charge;
                if (charge >= 0.999f && s.Hit is null)
                {
                    s.Pierce = true;
                    s.Hit = new System.Collections.Generic.HashSet<PlaneMob>();
                }
            }
            s.Gun = true;
            s.Speed0 = speed;
            s.Range = range;
            s.Velocity = dir * speed;
            s.Knock = GunTuning.Knock * (1f + GunTuning.RifleKnock * charge);
            s.Hover = 0f;
            s.Lingers = false;
        }
        float kick = GunTuning.Recoil * (shotgun ? GunTuning.ShotgunKick : 1f) * (1f + GunTuning.RifleKick * charge) * (machine ? GunTuning.MachineKick : 1f);
        Player.Velocity -= aim * MathF.Min(kick, GunTuning.RecoilMost);
    }
}
