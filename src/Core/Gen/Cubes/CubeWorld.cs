using System;
using System.Numerics;
using OctoShoots.Core.Plane;

namespace OctoShoots.Core.Gen.Cubes;

/// <summary>
/// The prototype's world (CUBE-WORLD-PLAN §14.1): Clementine alone in one layer of the cube world, swimming exactly as
/// on a reef (<see cref="BodyMotion"/>) against the rock of the cubes round her (<see cref="CubeField"/>). No creatures,
/// shots or pearls; her dash and its held sprint, but no ink cloud.
/// </summary>
public sealed class CubeWorld : IBodyBlocker
{
    public const float Dt = PlaneWorld.Dt;

    public CubeWorld(Func<CubeCoord, CubeTerrain?> terrain, Vector2 start, int layer, Tuning? tuning = null, int reach = 1)
    {
        Tuning = tuning ?? new Tuning();
        Layer = layer;
        Player = new PlaneBody { Position = start, PrevPosition = start };
        Field = new CubeField(terrain, CubeCoord.Of(start, layer), reach);
    }

    public Tuning Tuning { get; }
    public PlaneBody Player { get; }
    public CubeField Field { get; }
    public int Layer { get; }
    public long Tick { get; private set; }
    /// <summary>She met rock in the last tick.</summary>
    public bool HitWall { get; private set; }

    public float Radius => Tuning.PlayerRadius;
    public float CruiseSpeed => Tuning.PlaneCruiseSpeed;
    /// <summary>The cube she is in.</summary>
    public CubeCoord Cube => CubeCoord.Of(Player.Position, Layer);

    public void Step(in PlaneInput input)
    {
        Tick++;
        var p = Player;
        var t = Tuning;
        p.PrevPosition = p.Position;
        if (input.Aim.LengthSquared() > 1e-6f) p.Aim = Vector2.Normalize(input.Aim);
        Vector2 wish = input.Move;
        float wishLen = wish.Length();
        if (wishLen > 1f)
        {
            wish /= wishLen;
            wishLen = 1f;
        }
        float cruise = CruiseSpeed;
        p.DashCooldownTimer -= Dt;
        p.DashInvulnerableTimer -= Dt;
        if (input.Dash && p.DashCooldownTimer <= 0f)
        {
            Vector2 dir = wishLen > 0.01f ? wish / wishLen : p.Aim;
            p.DashTimer = t.DashCoast;
            p.DashInvulnerableTimer = t.DashInvulnerable;
            p.DashCooldownTimer = t.DashCooldown;
            p.Velocity = dir * cruise * t.DashMultiplier;
            p.JetTimer = 0f;
        }
        if (input.Dash) p.Sprinting = true;
        if (!input.DashHeld) p.Sprinting = false;
        BodyMotion.Swim(p, wish, wishLen, t, cruise, Dt);
        // (The window round the cube she starts the tick in: it reaches a whole cube past her every way.)
        Field.Recenter(Cube);
        HitWall = BodyMotion.Move(this, ref p.Position, ref p.Velocity, Radius, Dt);
    }

    public bool Clear(Vector2 p, float radius) => ReefCollision.Clear(Field, p, radius);

    public Vector2 WallNormal(Vector2 p, float radius) => ReefCollision.WallNormal(Field, p, radius);
}
