using System;
using System.Collections.Generic;
using System.Numerics;

namespace OctoShoots.Core.Plane;

/// <summary>
/// A freed murkling's core (docs/LIGHT-AND-GRAPH-PROPOSAL.md §4.2): when its meter runs out, its ink shell splashes apart
/// and the light it had drunk hangs where it was, fading over Life seconds. Touching it (within Reach of her) gives her
/// Light HP (PrimeLight for a prime's, LesserLight for a turned fish's), never past her max. Her Remora Sucker (the
/// magnet) draws cores to her from as far as it draws shells.
/// </summary>
public static class CoreTuning
{
    public const float Life = 1f, Reach = 0.9f;
    public const float Light = 4f, PrimeLight = 20f, LesserLight = 2f;
}

/// <summary>A freed murkling's light, hanging a moment where it was freed.</summary>
public sealed class PlaneCore
{
    public Vector2 Position, Velocity;
    public float Age, Light;
    public bool Taken;
    /// <summary>How much of its time is gone (0 new, 1 gone).</summary>
    public float Fade => Math.Clamp(Age / CoreTuning.Life, 0f, 1f);
}

public sealed partial class PlaneWorld
{
    /// <summary>Freed murklings' cores, hanging until she takes them or they fade.</summary>
    public List<PlaneCore> Cores { get; } = new();

    /// <summary>A freed murkling leaves its core (Drippus Prime's brood, born of its body, leaves none).</summary>
    void LeaveCore(Murkling m)
    {
        if (m.Brood) return;
        float light = m.Prime ? CoreTuning.PrimeLight : m.Lesser ? CoreTuning.LesserLight : CoreTuning.Light;
        Cores.Add(new PlaneCore { Position = m.Position, Light = light });
    }

    /// <summary>Her cores: drawn in by the magnet, taken on touch (healing her), gone once faded.</summary>
    void StepCores(float magnet)
    {
        if (Cores.Count == 0) return;
        var p = Player;
        foreach (var core in Cores)
        {
            core.Age += Dt;
            if (core.Age >= CoreTuning.Life || Defeated)
            {
                core.Taken = true;
                continue;
            }
            Vector2 to = p.Position - core.Position;
            float d = to.Length();
            if (magnet > 0f && d < Radius + magnet && d > 1e-4f) core.Velocity = to / d * PlaneEconomyTuning.ShellMagnetSpeed;
            else core.Velocity *= MathF.Exp(-4f * Dt);
            var next = core.Position + core.Velocity * Dt;
            if (Map.IsOpen(next)) core.Position = next;
            else core.Velocity = Vector2.Zero;
            if (Vector2.Distance(core.Position, p.Position) > Radius + CoreTuning.Reach) continue;
            core.Taken = true;
            float healed = MathF.Min(core.Light, MathF.Max(Run.MaxHp - p.Hp, 0f));
            p.Hp += healed;
            Events.Add(new PlaneEvent(PlaneEventType.CoreTaken, core.Position, Vector2.Zero, healed));
        }
        Cores.RemoveAll(c => c.Taken);
    }
}
