using System;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>What stops a body swimming: the rock, and whatever else a world puts in the water.</summary>
public interface IBodyBlocker
{
    /// <summary>True when a circle of the given radius at p sits wholly in open water.</summary>
    bool Clear(Vector2 p, float radius);
    /// <summary>The way into whatever blocks the circle at p (unit, or zero).</summary>
    Vector2 WallNormal(Vector2 p, float radius);
}

/// <summary>
/// Her swimming, shared by every world she swims in (the reef's levels, and the cube world: CUBE-WORLD-PLAN §5.3): how
/// her stroke speeds her up and lets her glide to a stop, and how a body moves along its velocity, sliding on walls.
/// </summary>
public static class BodyMotion
{
    /// <summary>
    /// One tick of her stroke, outside a dash (DESIGN-2D §20): from rest or on a sharp turn a jet start bursts ahead;
    /// held dash keeps her sprinting; with no input she glides to a stop. While a dash coasts, only her heading is kept.
    /// True when a jet start began (the caller reports it).
    /// </summary>
    public static bool Swim(PlaneBody p, Vector2 wish, float wishLen, Tuning t, float cruise, float dt)
    {
        bool hasInput = wishLen > 0.01f;
        Vector2 wishDir = hasInput ? wish / wishLen : Vector2.Zero;
        bool jet = false;
        if (p.DashTimer > 0f)
        {
            p.DashTimer -= dt;
            if (hasInput)
            {
                p.WasMoving = true;
                p.IdleTime = 0f;
                p.LastMoveDir = wishDir;
            }
        }
        else if (hasInput)
        {
            bool fromRest = !p.WasMoving && p.Velocity.Length() < t.JetRestFraction * cruise;
            bool sharpTurn = p.WasMoving && Angle(wishDir, p.LastMoveDir) > t.JetTurnAngle * MathUtil.Deg2Rad;
            if ((fromRest || sharpTurn) && t.JetDuration > 0f)
            {
                p.JetTimer = t.JetDuration;
                jet = true;
            }
            float boost = 1f;
            if (p.JetTimer > 0f)
            {
                float k = MathUtil.Clamp01(p.JetTimer / t.JetDuration);
                boost = 1f + (t.JetMultiplier - 1f) * k * k * (3f - 2f * k);
            }
            float sprint = p.Sprinting ? t.SprintMultiplier : 1f;
            float maxSpeed = cruise * MathF.Max(boost, sprint);
            float rate = cruise / t.AccelTime * MathF.Max(boost, sprint);
            if (p.Velocity.Length() > maxSpeed + 0.01f) rate = MathF.Max(rate, t.OverspeedDecel);
            p.Velocity = MoveToward(p.Velocity, wish * maxSpeed, rate * dt);
            p.IdleTime = 0f;
            p.WasMoving = true;
            p.LastMoveDir = wishDir;
        }
        else
        {
            // Idle drift: she glides to a stop (the visual bob is presentation only; there is no sinking).
            p.IdleTime += dt;
            p.WasMoving = false;
            p.JetTimer = 0f;
            float rate = cruise / t.StopTime;
            if (p.Velocity.Length() > cruise + 0.01f) rate = MathF.Max(rate, t.OverspeedDecel);
            p.Velocity = MoveToward(p.Velocity, Vector2.Zero, rate * dt);
        }
        p.JetTimer -= dt;
        return jet;
    }

    /// <summary>Moves a circle along its velocity, blocking against what blocks it and sliding along it. True when it hit.</summary>
    public static bool Move(IBodyBlocker blocker, ref Vector2 position, ref Vector2 velocity, float radius, float dt)
    {
        // Pushed out of a wall (a crater or a spawn) first.
        for (int i = 0; i < 20 && !blocker.Clear(position, radius); i++) position -= blocker.WallNormal(position, radius) * 0.05f;

        Vector2 delta = velocity * dt;
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / (radius * 0.5f)));
        Vector2 step = delta / steps;
        bool hit = false;
        for (int s = 0; s < steps; s++)
        {
            Vector2 next = position + step;
            if (blocker.Clear(next, radius))
            {
                position = next;
                continue;
            }
            hit = true;
            Vector2 n = blocker.WallNormal(next, radius);
            float into = Vector2.Dot(step, n);
            if (into > 0f) step -= n * into;
            float vInto = Vector2.Dot(velocity, n);
            if (vInto > 0f) velocity -= n * vInto;
            next = position + step;
            if (blocker.Clear(next, radius))
            {
                position = next;
                continue;
            }
            // In a bend the averaged wall normal can come out square to the push, leaving nothing to slide along: try
            // the step's two axis parts and its slide along the wall, and take what goes furthest her way.
            Vector2 wish = delta / steps;
            Vector2 tangent = new(-n.Y, n.X);
            Vector2 best = Vector2.Zero;
            float bestGain = 1e-5f;
            // (And, on a bump of rock that juts into the way along a wall, that slide eased off the wall, round it.)
            foreach (var t in new[] { new Vector2(wish.X, 0f), new Vector2(0f, wish.Y), tangent * Vector2.Dot(wish, tangent), tangent * Vector2.Dot(wish, tangent) - n * wish.Length() })
            {
                float gain = Vector2.Dot(t, wish);
                if (gain <= bestGain || !blocker.Clear(position + t, radius)) continue;
                best = t;
                bestGain = gain;
            }
            if (best != Vector2.Zero)
            {
                position += best;
                step = best;
                // Keep only the velocity along the way she could go.
                Vector2 dir = Vector2.Normalize(best);
                velocity = dir * MathF.Max(Vector2.Dot(velocity, dir), 0f);
            }
            else
            {
                velocity = Vector2.Zero;
                break;
            }
        }
        return hit;
    }

    static Vector2 MoveToward(Vector2 from, Vector2 to, float maxDelta)
    {
        Vector2 d = to - from;
        float len = d.Length();
        return len <= maxDelta || len < 1e-6f ? to : from + d / len * maxDelta;
    }

    static float Angle(Vector2 a, Vector2 b) => MathF.Acos(Math.Clamp(Vector2.Dot(Normalize(a), Normalize(b)), -1f, 1f));

    static Vector2 Normalize(Vector2 v)
    {
        float l = v.Length();
        return l > 1e-6f ? v / l : Vector2.Zero;
    }
}
