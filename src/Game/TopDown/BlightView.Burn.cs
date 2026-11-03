using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her light burning the corrupted (docs/ADVENTURE.md §8; the searing itself is her_burn.gdshaderinc), the particles:
/// from the side of every burning body facing her, tiny bright specks boil off and rise like the fizz off a dissolving
/// tablet, flakes of bleached ink peel away and sink, and now and then a pale wisp of it rises and thins. A creature her
/// light dazzles gives one sharp glint and a ring of sparks.
/// </summary>
public partial class BlightView
{
    /// <summary>Her light's heart (world), where the burning bodies' lit sides face.</summary>
    public Vector3 HerLight { get; set; }

    /// <summary>Specks boiled off a burning body a second, for a body of a murkling's size (bigger bodies more).</summary>
    const float SizzleRate = 34f;

    void SyncBurns(PlaneWorld world, float dt)
    {
        for (int i = 0; i < _murks.Count && i < world.Murklings.Count; i++)
        {
            var m = world.Murklings[i];
            if (m.Alive && world.InHerLight(m)) Sizzle(_murks[i].GlobalPosition, _murks[i].Scale.X, dt);
        }
        foreach (var (w, view) in _wardens)
            if (w.Alive && world.InHerLight(w)) Sizzle(view.Node.GlobalPosition + (w.Puffer ? Vector3.Zero : Vector3.Up * 0.5f), WardenTuning.Radius, dt);
        foreach (var (f, view) in _flounders)
            if (f.Corrupted && world.InHerLight(f)) Sizzle(view.Node.GlobalPosition + Vector3.Up * 0.1f, FlounderTuning.Radius, dt);
    }

    /// <summary>A burning body (its middle and radius): what boils off its lit side this frame.</summary>
    void Sizzle(Vector3 at, float radius, float dt)
    {
        var toLight = HerLight - at;
        if (toLight.LengthSquared() < 1e-4f) return;
        toLight = toLight.Normalized();
        float rate = SizzleRate * Mathf.Clamp(radius / BlightTuning.MurkRadius, 0.5f, 3f);
        int n = (int)(rate * dt) + (_motes.RandRange(0f, 1f) < rate * dt % 1f ? 1 : 0);
        var light = HerColor.Light;
        var hot = new Color(Mathf.Lerp(light.R, 1f, 0.6f) * 1.8f, Mathf.Lerp(light.G, 1f, 0.6f) * 1.8f, Mathf.Lerp(light.B, 1f, 0.6f) * 1.8f, 1f);
        var cool = new Color(light.R, light.G * 0.8f, light.B * 0.6f, 0f);
        for (int k = 0; k < n; k++)
        {
            // A point on the lit side: toward her light, scattered over the half of it that faces her.
            var d = (toLight + _motes.RandDir() * 0.85f).Normalized();
            if (d.Dot(toLight) < 0.05f) d = (d + toLight).Normalized();
            var from = at + d * radius * 1.02f;
            // The fizz: a tiny white-hot speck boiling off, rising as it cools to her colour and goes.
            _motes.Emit(from, d * _motes.RandRange(0.3f, 0.9f) + Vector3.Up * _motes.RandRange(0.3f, 0.9f), _motes.RandRange(0.25f, 0.6f),
                _motes.RandRange(0.025f, 0.06f), 0.01f, hot, cool, drag: 2.5f, buoyancy: 1.3f);
            // A flake of the bleached ink peeling off and sinking.
            if (_motes.RandRange(0f, 1f) < 0.25f)
                _droplets.Emit(from, d * _motes.RandRange(0.4f, 1f) + Vector3.Up * 0.2f, _motes.RandRange(0.5f, 0.9f), _motes.RandRange(0.035f, 0.07f), 0.015f,
                    new Color(0.16f, 0.09f, 0.24f, 0.9f), new Color(0.4f, 0.3f, 0.5f, 0f), drag: 2f, buoyancy: -0.6f);
            // A pale wisp of it, rising and thinning.
            if (_motes.RandRange(0f, 1f) < 0.06f)
                _droplets.Emit(from, d * 0.2f + Vector3.Up * 0.5f, _motes.RandRange(0.7f, 1.1f), 0.08f, 0.3f,
                    new Color(light.R, light.G, light.B, 0.18f), new Color(0.6f, 0.55f, 0.7f, 0f), drag: 1.5f, buoyancy: 0.5f);
        }
    }

    /// <summary>A creature comes into her light, dazzled: one sharp glint on the side facing her and a ring of sparks off it.</summary>
    void Dazzle(in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        var toHer = new Vector3(-e.Direction.X, 0.3f, -e.Direction.Y).Normalized();
        var glint = at + toHer * Mathf.Max(e.Size, 0.2f);
        var light = HerColor.Light;
        var white = new Color(2.2f, 2.1f, 1.9f, 1f);
        _motes.Emit(glint, Vector3.Zero, 0.22f, 0.5f, 0.05f, white, new Color(light.R, light.G, light.B, 0f), drag: 0f);
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.Tau / 14f + _motes.RandRange(-0.15f, 0.15f);
            var dir = new Vector3(Mathf.Cos(a), _motes.RandRange(-0.2f, 0.4f), Mathf.Sin(a));
            _motes.Emit(glint, dir * _motes.RandRange(1.6f, 2.6f), _motes.RandRange(0.2f, 0.35f), _motes.RandRange(0.04f, 0.07f), 0.01f, white, new Color(light.R, light.G, light.B, 0f), drag: 6f);
        }
    }
}
