using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Her light burning the corrupted (docs/ADVENTURE.md §8; the light on their skin is her_burn.gdshaderinc), the
/// particles: her light strikes a body as a strong current strikes a stone. Where it meets the side facing it, it splashes
/// in its own colours, sheeting round the body and shooting off past its sides; streaks of it run on past the body's
/// edges downstream; and flakes of the ink it wears away are torn off and carried with it. A murkling in it is pressed
/// back a little and shudders (BlightView.cs). A creature a shot's light dazzles gives one sharp glint and a ring of sparks.
/// </summary>
public partial class BlightView
{
    /// <summary>Splashes off a burning body a second, for a body of a murkling's size (bigger bodies more).</summary>
    const float SizzleRate = 34f;
    /// <summary>How far her light presses a murkling back (m, at its full) and how fast it shudders in it.</summary>
    public const float PressBack = 0.07f, PressShudder = 38f;

    void SyncBurns(PlaneWorld world, float dt)
    {
        for (int i = 0; i < _murks.Count && i < world.Murklings.Count; i++)
        {
            var m = world.Murklings[i];
            if (m.Alive && world.LitFrom(m) is { } from) Sizzle(_murks[i].GlobalPosition, _murks[i].Scale.X, from, world.ColourOn(m), dt);
        }
        foreach (var (w, view) in _wardens)
            if (w.Alive && world.LitFrom(w) is { } from) Sizzle(view.Node.GlobalPosition + (w.Puffer ? Vector3.Zero : Vector3.Up * 0.5f), WardenTuning.Radius, from, world.ColourOn(w), dt);
        foreach (var (f, view) in _flounders)
            if (f.Corrupted && world.LitFrom(f) is { } from) Sizzle(view.Node.GlobalPosition + Vector3.Up * 0.1f, FlounderTuning.Radius, from, world.ColourOn(f), dt);
    }

    /// <summary>A burning body (its middle and radius), lit from a light there (on her plane) in a colour of hers: what her light's current throws off it this frame.</summary>
    void Sizzle(Vector3 at, float radius, System.Numerics.Vector2 light, LightHue hue, float dt)
    {
        var c = LightColour.Beam(hue, TopDownMain.ShowcaseOn);
        float rate = SizzleRate * Mathf.Clamp(radius / BlightTuning.MurkRadius, 0.5f, 3f);
        int n = (int)(rate * dt) + (_motes.RandRange(0f, 1f) < rate * dt % 1f ? 1 : 0);
        var flow = new Vector3(at.X - light.X, 0f, at.Z - light.Y);
        if (flow.LengthSquared() < 0.04f)
        {
            // Lit from all round (a pool of her light, a firefly): only a soft glow of its colour welling off it.
            for (int k = 0; k < n / 3; k++)
                _motes.Emit(at + _motes.RandDir() * radius, Vector3.Up * _motes.RandRange(0.3f, 0.7f), _motes.RandRange(0.4f, 0.7f), 0.05f, 0.015f,
                    new Color(c.R, c.G, c.B, 0.7f), new Color(c.R, c.G, c.B, 0f), drag: 2f, buoyancy: 0.6f);
            return;
        }
        flow = flow.Normalized();
        var across = new Vector3(-flow.Z, 0f, flow.X);
        var bright = new Color(c.R * 1.7f, c.G * 1.7f, c.B * 1.7f, 0.9f);
        var gone = new Color(c.R, c.G, c.B, 0f);
        for (int k = 0; k < n; k++)
        {
            // The splash: from a point of the side facing the light, sheeting round the body (along its skin, away from
            // the light) and flung off it.
            var normal = (-flow + _motes.RandDir() * 0.75f).Normalized();
            if (normal.Dot(-flow) < 0.15f) normal = (normal - flow).Normalized();
            var from = at + normal * radius * 1.03f;
            var slide = flow - normal * flow.Dot(normal);
            slide = slide.LengthSquared() > 1e-4f ? slide.Normalized() : across * (_motes.RandRange(0f, 1f) < 0.5f ? -1f : 1f);
            _motes.Emit(from, slide * _motes.RandRange(1.8f, 3.4f) + normal * _motes.RandRange(0.2f, 0.6f) + flow * 0.4f,
                _motes.RandRange(0.22f, 0.4f), _motes.RandRange(0.06f, 0.09f), 0.018f, bright, gone, drag: 3.5f);
            // The current running on past its edges, downstream.
            if (_motes.RandRange(0f, 1f) < 0.55f)
            {
                float side = _motes.RandRange(0f, 1f) < 0.5f ? -1f : 1f;
                var edge = at + across * side * radius * _motes.RandRange(0.85f, 1.1f) + Vector3.Up * _motes.RandRange(-0.3f, 0.4f) * radius;
                _motes.Emit(edge, flow * _motes.RandRange(2.6f, 4.2f) + across * side * _motes.RandRange(0.2f, 0.6f),
                    _motes.RandRange(0.28f, 0.48f), _motes.RandRange(0.045f, 0.065f), 0.015f, new Color(c.R * 1.3f, c.G * 1.3f, c.B * 1.3f, 0.75f), gone, drag: 1.8f);
            }
            // A flake of the ink it wears away, torn off and carried with it.
            if (_motes.RandRange(0f, 1f) < 0.18f)
                _droplets.Emit(from, slide * _motes.RandRange(0.6f, 1.2f) + flow * _motes.RandRange(0.8f, 1.6f), _motes.RandRange(0.5f, 0.9f),
                    _motes.RandRange(0.035f, 0.07f), 0.015f, new Color(0.08f, 0.05f, 0.13f, 0.9f), new Color(0.25f, 0.2f, 0.35f, 0f), drag: 1.6f, buoyancy: -0.3f);
        }
    }

    /// <summary>A creature comes into a shot's light, dazzled: one sharp glint on the side facing it and a ring of sparks off it.</summary>
    void Dazzle(in PlaneEvent e)
    {
        var at = new Vector3(e.Position.X, LevelMap.SwimBand, e.Position.Y);
        var toLight = new Vector3(-e.Direction.X, 0.3f, -e.Direction.Y).Normalized();
        var glint = at + toLight * Mathf.Max(e.Size, 0.2f);
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
