using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Plane;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Murklings as liquid (murkling.gdshader): each one's body a soft drop of ink, like mercury in no gravity.
/// <para>
/// Twelve points round it (the icosahedron's corners) each stand out of round or in, on a spring back to round (its
/// surface tension) lightly damped, so it rings; each is pulled toward its neighbours (so a dent spreads and smooths);
/// and the whole is kept at its volume (what one lobe takes in, the rest push out). Its inertia shapes it: when it
/// speeds up the ink lags, its trailing side bulging and its leading side flattening; when it stops, it sloshes on;
/// moving fast, it draws out along its way. Her hit dents it where the light struck (the deeper, the more of its meter
/// the hit took), and it rebounds past round.
/// </para>
/// <para>
/// Its throw: drawing the slingshot back, its body sloshes back with the strain; at the snap the front is flung out and
/// the body recoils, ringing as the piece leaves it.
/// </para>
/// </summary>
public partial class BlightView
{
    sealed class MurkBody
    {
        public readonly float[] R = new float[Lobes], V = new float[Lobes];
        public readonly ShaderMaterial Material;
        public System.Numerics.Vector2 LastVelocity;
        public bool Seen, WasWinding;
        public float LastThrowAge = 10f;
        /// <summary>How much light on its core shows the whole of it (eased).</summary>
        public float Core;
        /// <summary>How much of its budding shows (0..1): only while light falls on where it is born.</summary>
        public float BudSeen;
        /// <summary>How much a sealed leak's wave of light, passing over it, still shows it (1 as it touches it, fading).</summary>
        public float Waved;
        /// <summary>Whether it showed at all this frame (her light, a charge stone's, the wave): its health bar is drawn only then.</summary>
        public bool Shown;
        /// <summary>Her light's colour on it as drawn (eased), and the colour it was last.</summary>
        public float Colour;
        public LightHue ColourHue;
        /// <summary>How hard her light's current presses it now (eased, 0..1), and which way (world, level).</summary>
        public float Pressed;
        public Vector3 PressWay;
        /// <summary>Unseen but near her: till its next ripple on the water (s), and whether its coming has been heard.</summary>
        public float Ripple;
        public bool Heard;
        /// <summary>How long it has been out of her sensing (re-arms its sound).</summary>
        public float Unsensed;
        /// <summary>A second material drawing it over her (a Blightroot's head: murkling_over), kept in step with Material.</summary>
        public ShaderMaterial? Over;
        public MurkBody(ShaderMaterial material) { Material = material; }
        /// <summary>A small black hole: how hard her light is burning it now (eased), how much the water round it shows (eased), its meter last frame, and till its next ring drawn in as it regrows (s).</summary>
        public float Burn, Lens, LastMeter = -1f, Inhale;
    }

    // ───────────────────────── a small black hole ─────────────────────────

    /// <summary>The water round each murkling (murk_hole.gdshader), its disc this many times its radius across (a radius's worth: 2.6).</summary>
    readonly List<MeshInstance3D> _lenses = new();
    ShaderMaterial? _lensMat;
    readonly PlaneMesh _lensDisc = new() { Size = new Vector2(2f, 2f) };
    const float LensReach = 2.6f;

    /// <summary>Flecks of ink a second it sheds into her light at the least, and more per share of its meter she takes a second; how fast they drift to her, and how soon they burn away.</summary>
    const float ShedLeast = 10f, ShedPerShare = 120f, ShedMost = 60f, ShedDrift = 1f, ShedLife = 0.5f;
    /// <summary>Regrowing: a ring of the water drawn in to it every InhaleEvery seconds, from InhaleReach metres, over InhaleLife.</summary>
    const float InhaleEvery = 0.6f, InhaleReach = 3f, InhaleLife = 0.9f;

    /// <summary>
    /// A murkling as a small black hole (docs/CORRUPTION.md): its body black, its photon ring and the water bent and whirling
    /// round it while her light is on it; its core in her light's colour, as bright as her beam is drawn, while her light
    /// burns it, flecks of its ink peeling off toward her and burning away; regrowing, the water drawn in to it.
    /// </summary>
    void Hole(PlaneWorld world, Murkling m, MurkBody body, MeshInstance3D view, MeshInstance3D lens, float scale, LightHue colourOn, float dt)
    {
        var mat = body.Material;
        mat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("hole"), 1f);
        var lightCol = OctoShoots.Game.Fx.LightColour.Beam(colourOn, false);
        var from = world.LitFrom(m);
        bool burning = !m.Budding && from is not null;
        body.Burn = Mathf.MoveToward(body.Burn, burning ? 1f : 0f, dt * (burning ? 8f : 2.5f));
        body.Lens = Mathf.MoveToward(body.Lens, body.Shown && !m.Budding ? 1f : 0f, dt * 4f);
        var her = world.Player.Position;
        var light = from ?? her;
        mat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ring_from"), new Vector3(light.X, view.Position.Y, light.Y));
        mat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ring_color"), lightCol);
        mat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("ring_on"), body.Lens);
        mat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("core_lit"), new Color(lightCol.R, lightCol.G, lightCol.B, body.Burn * BeamView.BrightOf(world.LightPower)));
        // The water round it, only while her light is on it.
        lens.Visible = body.Lens > 0.01f && scale > 0.01f;
        if (lens.Visible)
        {
            lens.Position = view.Position;
            lens.Scale = Vector3.One * scale * LensReach;
            lens.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("shown"), body.Lens);
            lens.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), m.Seed);
        }
        float meter = m.Corruption.Value;
        float was = body.LastMeter < 0f ? meter : body.LastMeter;
        body.LastMeter = meter;
        if (m.Budding || dt <= 0f) return;
        // Burnt: its ink peels off the side her light comes from, drifts to her and burns away in her light's colour.
        if (burning)
        {
            float share = m.Corruption.Max > 0f ? Mathf.Max(0f, was - meter) / m.Corruption.Max / dt : 0f;
            float rate = Mathf.Min(ShedLeast + ShedPerShare * share, ShedMost);
            int n = (int)(rate * dt) + (_droplets.RandRange(0f, 1f) < rate * dt % 1f ? 1 : 0);
            var toward = new Vector3(light.X - m.Position.X, 0f, light.Y - m.Position.Y);
            toward = toward.LengthSquared() > 1e-4f ? toward.Normalized() : Vector3.Forward;
            var across = new Vector3(-toward.Z, 0f, toward.X);
            for (int k = 0; k < n; k++)
            {
                float a = _droplets.RandRange(-1f, 1f) * 1.05f;
                var normal = (toward * Mathf.Cos(a) + across * Mathf.Sin(a) + Vector3.Up * _droplets.RandRange(-0.3f, 0.5f)).Normalized();
                var at = view.Position + normal * scale * _droplets.RandRange(0.92f, 1.05f);
                var v = toward * ShedDrift * _droplets.RandRange(0.7f, 1.3f) + normal * _droplets.RandRange(0.2f, 0.5f) + across * _droplets.RandRange(-0.25f, 0.25f);
                float life = ShedLife * _droplets.RandRange(0.7f, 1.2f), size = _droplets.RandRange(0.035f, 0.075f);
                _droplets.Emit(at, v, life, size, size * 0.4f, new Color(0.008f, 0.004f, 0.014f, 0.95f), new Color(lightCol.R, lightCol.G, lightCol.B, 0f), drag: 0.6f);
                _motes.Emit(at, v, life, size * 0.5f, size * 1.2f, new Color(lightCol.R, lightCol.G, lightCol.B, 0f), new Color(lightCol.R * 1.6f, lightCol.G * 1.6f, lightCol.B * 1.6f, 0.9f), drag: 0.6f);
            }
        }
        // Regrowing: the water drawn in to where it is, lit or not, for as long as it regrows.
        if (meter > was + 1e-5f)
        {
            body.Inhale -= dt;
            if (body.Inhale <= 0f)
            {
                body.Inhale = InhaleEvery;
                Swells?.AddInward(new Vector2(m.Position.X, m.Position.Y), 0.9f, InhaleReach / 2.6f, InhaleLife);
            }
        }
        else body.Inhale = 0f;
    }

    /// <summary>How big a freed murkling is as it implodes (m: its core's size, all it has left), and how long it takes.</summary>
    const float ImplodeFrom = 0.3f, ImplodeTime = 0.25f;

    sealed class Implosion
    {
        public MeshInstance3D Node = null!;
        public float Age, Size;
    }

    readonly List<Implosion> _implosions = new();
    StandardMaterial3D? _voidMat;

    /// <summary>
    /// Freed, it implodes: its black collapses to a point over ImplodeTime, the water drawn in with it; then one bright
    /// flash of her light's colour and a quick ring of the water outward.
    /// </summary>
    void Implode(Vector3 at, float size)
    {
        _voidMat ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0f, 0f, 0f) };
        var node = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _voidMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = at, Scale = Vector3.One * size };
        AddChild(node);
        _implosions.Add(new Implosion { Node = node, Size = size });
        Swells?.AddInward(new Vector2(at.X, at.Z), 1.3f, Mathf.Max(size * 5f, 1.5f) / 2.6f, ImplodeTime);
    }

    void StepImplosions(float dt)
    {
        for (int k = _implosions.Count - 1; k >= 0; k--)
        {
            var im = _implosions[k];
            im.Age += dt;
            float t = Mathf.Clamp(im.Age / ImplodeTime, 0f, 1f);
            im.Node.Scale = Vector3.One * Mathf.Max(im.Size * (1f - t * t * t), 0.001f);
            if (t < 1f) continue;
            // The flash: her light's colour, bright, gone at once; and one ring of the water out from it.
            var at = im.Node.Position;
            var c = OctoShoots.Game.Fx.LightColour.Beam(LightHue.None, false);
            _motes.Emit(at, Vector3.Zero, 0.18f, 0.9f, 0.1f, new Color(c.R * 2f, c.G * 2f, c.B * 2f, 1f), new Color(c.R, c.G, c.B, 0f), drag: 0f);
            for (int s = 0; s < 14; s++)
            {
                var dir = new Vector3(Mathf.Cos(s * Mathf.Tau / 14f), _motes.RandRange(-0.2f, 0.3f), Mathf.Sin(s * Mathf.Tau / 14f));
                _motes.Emit(at, dir * _motes.RandRange(2f, 3.4f), _motes.RandRange(0.2f, 0.32f), 0.07f, 0.01f, new Color(c.R * 1.5f, c.G * 1.5f, c.B * 1.5f, 0.9f), new Color(c.R, c.G, c.B, 0f), drag: 5f);
            }
            Swells?.AddRing(new Vector2(at.X, at.Z), 1.1f, 0.8f, 0.55f, snap: 3f);
            im.Node.QueueFree();
            _implosions.RemoveAt(k);
        }
    }

    internal const int Lobes = 12;
    /// <summary>Its surface tension (spring), its damping (low: it rings), its neighbours' pull, how hard its inertia shapes it, how far it may stand out.</summary>
    const float Tension = 170f, Damping = 5f, Coupling = 40f, Inertia = 0.03f, LobeMax = 0.45f;
    /// <summary>A hit: how deep it dents (per its share of the meter).</summary>
    const float HitDent = 8f;

    internal static readonly Vector3[] LobeDirs =
    {
        new(-0.5257f, 0.8507f, 0f), new(0.5257f, 0.8507f, 0f), new(-0.5257f, -0.8507f, 0f), new(0.5257f, -0.8507f, 0f),
        new(0f, -0.5257f, 0.8507f), new(0f, 0.5257f, 0.8507f), new(0f, -0.5257f, -0.8507f), new(0f, 0.5257f, -0.8507f),
        new(0.8507f, 0f, -0.5257f), new(0.8507f, 0f, 0.5257f), new(-0.8507f, 0f, -0.5257f), new(-0.8507f, 0f, 0.5257f),
    };

    /// <summary>Each lobe's five neighbours (the icosahedron's edges).</summary>
    internal static readonly int[][] LobeNear = BuildNear();

    static int[][] BuildNear()
    {
        var near = new int[Lobes][];
        for (int i = 0; i < Lobes; i++)
        {
            var list = new List<int>();
            for (int j = 0; j < Lobes; j++)
                if (j != i && LobeDirs[i].Dot(LobeDirs[j]) > 0.4f) list.Add(j);
            near[i] = list.ToArray();
        }
        return near;
    }

    /// <summary>Liquid bodies by what they belong to: a murkling, or a Blightroot's head.</summary>
    readonly Dictionary<object, MurkBody> _bodies = new();

    /// <summary>Whether a murkling showed this frame (her light, a charge stone's, a sealed leak's wave): its health bar is drawn only then.</summary>
    public bool Shown(Murkling m) => _bodies.TryGetValue(m, out var b) && b.Shown;
    Shader? _murkShader;

    MurkBody BodyOf(object m)
    {
        if (_bodies.TryGetValue(m, out var b)) return b;
        _murkShader ??= GD.Load<Shader>("res://assets/shaders/murkling.gdshader");
        b = new MurkBody(new ShaderMaterial { Shader = _murkShader });
        _bodies[m] = b;
        return b;
    }

    /// <summary>One frame of a murkling's liquid body: its inertia, its throw's strain and snap, its springs.</summary>
    void StepBody(Murkling m, MurkBody b, float dt)
    {
        // The throw's drawing back: it sloshes away from its aim, then is flung forward at the snap and recoils.
        var aim = new Vector3(m.Aim.X, 0f, m.Aim.Y);
        bool winding = m.Windup > BlightTuning.SnapSeconds;
        bool snapped = m.ThrowAge < b.LastThrowAge && m.ThrowAge < 0.1f;
        b.LastThrowAge = m.ThrowAge;
        SoftStep(b, m.Velocity, aim, winding, snapped, m.Seed, dt);
    }

    /// <summary>
    /// One frame of a liquid body (a murkling's, a Blightroot's head): its inertia, any strain (drawn back from aim) and
    /// snap, its springs and volume; sent to its material. stir: how restless the water round it keeps it.
    /// </summary>
    void SoftStep(MurkBody b, System.Numerics.Vector2 velocity, Vector3 aim, bool winding, bool snapped, float seed, float dt, float stirAmount = 0.06f)
    {
        b.Seen = true;
        dt = Mathf.Min(dt, 1f / 30f);
        // Its acceleration (map axes: x east, y south = world z): the ink lags it.
        var acc = (velocity - b.LastVelocity) / Mathf.Max(dt, 1e-4f);
        b.LastVelocity = velocity;
        var a = new Vector3(acc.X, 0f, acc.Y);
        var v = new Vector3(velocity.X, 0f, velocity.Y);
        float speed = v.Length();
        var along = speed > 1e-3f ? v / speed : Vector3.Zero;
        const int Steps = 3;
        float h = dt / Steps;
        for (int step = 0; step < Steps; step++)
        {
            float mean = 0f;
            for (int i = 0; i < Lobes; i++)
            {
                var d = LobeDirs[i];
                float near = 0f;
                foreach (int j in LobeNear[i]) near += b.R[j];
                near /= LobeNear[i].Length;
                // Moving fast it draws out along its way; speeding up, the ink lags behind.
                float stretch = speed * 0.035f * (d.Dot(along) * d.Dot(along) - 0.33f);
                float lag = Mathf.Clamp(-d.Dot(a) * Inertia, -0.3f, 0.3f);
                float strain = winding ? -d.Dot(aim) * 0.12f : 0f;
                // The water round it never quite still: a slow stir on each lobe, out of step.
                float stir = Mathf.Sin(_time * (2.1f + 0.37f * i) + seed * 3f + i * 1.7f) * stirAmount;
                float f = -Tension * (b.R[i] - stretch - strain - stir - lag) - Damping * b.V[i] + Coupling * (near - b.R[i]);
                b.V[i] += f * h;
            }
            for (int i = 0; i < Lobes; i++)
            {
                b.R[i] = Mathf.Clamp(b.R[i] + b.V[i] * h, -LobeMax, LobeMax);
                mean += b.R[i];
            }
            // Its volume kept: what one lobe takes in, the rest push out.
            mean /= Lobes;
            for (int i = 0; i < Lobes; i++) b.R[i] -= mean;
        }
        if (snapped)
            // The snap: the front flung out, the back pulled in (it rings as the piece leaves it).
            for (int i = 0; i < Lobes; i++) b.V[i] += LobeDirs[i].Dot(aim) * 3.2f;
        b.WasWinding = winding;
        var lobes = new float[Lobes];
        for (int i = 0; i < Lobes; i++) lobes[i] = b.R[i];
        b.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lobes"), lobes);
        b.Over?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lobes"), lobes);
    }

    /// <summary>A liquid body struck (a murkling, a Blightroot's head; from Struck): dented where it struck, ringing back.</summary>
    void Dent(MurkBody b, Vector3 back, float hitShare)
    {
        // Where it struck, on the body (the side facing the shot, a little up toward the camera).
        var dir = (back + Vector3.Up * 0.35f).Normalized();
        for (int i = 0; i < Lobes; i++)
        {
            float k = Mathf.Max(0f, LobeDirs[i].Dot(dir));
            b.V[i] -= HitDent * (0.5f + hitShare * 2f) * k * k;
        }
    }

    // ───────────────────────── ripped chunks ─────────────────────────

    sealed class Chunk
    {
        public MeshInstance3D View = null!;
        public Vector3 Position, Velocity;
        public float Size, Age, Life, Seed;
    }

    readonly List<Chunk> _chunks = new();
    const int MaxChunks = 40;

    void AddChunk(Vector3 at, Vector3 velocity, float size, float seed)
    {
        if (_chunks.Count >= MaxChunks)
        {
            _chunks[0].View.QueueFree();
            _chunks.RemoveAt(0);
        }
        var view = new MeshInstance3D { Mesh = _sphere, MaterialOverride = _orb, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
        view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("ember"), 0.15f);
        view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("wobble"), 1.6f);
        view.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("seed"), seed);
        AddChild(view);
        _chunks.Add(new Chunk { View = view, Position = at, Velocity = velocity, Size = size, Life = _droplets.RandRange(0.55f, 0.9f), Seed = seed });
    }

    /// <summary>
    /// The chunks fly on, dragged by the water, sinking (ink is heavier); each wobbles as a drop does (stretched along
    /// its flight, ringing back), shrinks, sheds a drop now and then, and at its end breaks into a few.
    /// </summary>
    void StepChunks(float dt)
    {
        for (int i = _chunks.Count - 1; i >= 0; i--)
        {
            var c = _chunks[i];
            c.Age += dt;
            if (c.Age >= c.Life)
            {
                for (int k = 0; k < 3; k++)
                    _droplets.Emit(c.Position, c.Velocity * 0.4f + _droplets.RandDir() * 0.5f, _droplets.RandRange(0.3f, 0.5f), c.Size * 0.7f, c.Size * 0.2f, InkDrop, InkDropFade, drag: 3f, buoyancy: -2f);
                c.View.QueueFree();
                _chunks.RemoveAt(i);
                continue;
            }
            c.Velocity *= Mathf.Exp(-3.2f * dt);
            c.Velocity += Vector3.Down * 2.2f * dt;
            c.Position += c.Velocity * dt;
            float k01 = c.Age / c.Life;
            float speed = c.Velocity.Length();
            float ring = 1f + 0.35f * Mathf.Exp(-6f * c.Age) * Mathf.Sin(28f * c.Age + c.Seed);
            float stretch = (1f + Mathf.Min(speed * 0.12f, 0.6f)) * ring;
            float s = c.Size * (1f - 0.6f * k01);
            c.View.Position = c.Position;
            c.View.Basis = speed > 1e-3f ? Basis.LookingAt(c.Velocity / speed, Vector3.Up).Scaled(new Vector3(s / Mathf.Sqrt(stretch), s / Mathf.Sqrt(stretch), s * stretch)) : Basis.Identity.Scaled(Vector3.One * s);
            c.View.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 1f - Mathf.SmoothStep(0.7f, 1f, k01));
            if (_droplets.RandRange(0f, 1f) < dt * 6f)
                _droplets.Emit(c.Position, c.Velocity * 0.3f, 0.35f, s * 0.5f, s * 0.15f, InkDrop, InkDropFade, drag: 3f, buoyancy: -2f);
        }
    }

    /// <summary>Bodies of murklings no longer here are let go.</summary>
    void ForgetBodies()
    {
        var gone = new List<object>();
        foreach (var (m, b) in _bodies)
        {
            if (!b.Seen) gone.Add(m);
            b.Seen = false;
        }
        foreach (var m in gone) _bodies.Remove(m);
    }

    void ClearMurkBodies()
    {
        _bodies.Clear();
        foreach (var c in _chunks) c.View.QueueFree();
        _chunks.Clear();
    }

    // ───────────────────────── every creature: a flinch when struck ─────────────────────────

    /// <summary>Views scaled for a flinch this frame (put back before the next frame's views are drawn).</summary>
    readonly List<(Node3D Node, Vector3 Scale)> _flinched = new();

    /// <summary>Puts back what last frame's flinches scaled (before this frame's views set their own).</summary>
    void UnFlinch()
    {
        foreach (var (node, scale) in _flinched)
            if (GodotObject.IsInstanceValid(node)) node.Scale = scale;
        _flinched.Clear();
    }

    /// <summary>
    /// A creature her light has just struck contracts for a moment, then rebounds past its size and settles — a sharp
    /// flinch, harder for a harder hit (CorruptionMeter.LastHit, Since).
    /// </summary>
    void Flinch(Node3D? node, CorruptionMeter meter)
    {
        if (node is null || meter.LastHit <= 0f || meter.Since > 0.3f || meter.Empty) return;
        float share = Mathf.Clamp(meter.LastHit / Mathf.Max(meter.Max, 1f), 0.05f, 1f);
        float t = meter.Since;
        float k = 1f - (0.1f + 0.2f * share) * Mathf.Exp(-14f * t) * Mathf.Cos(26f * t);
        _flinched.Add((node, node.Scale));
        node.Scale *= k;
    }

    void FlinchAll(PlaneWorld world)
    {
        for (int i = 0; i < _murks.Count && i < world.Murklings.Count; i++)
            if (_murks[i].Visible) Flinch(_murks[i], world.Murklings[i].Corruption);
        for (int i = 0; i < _roots.Count && i < world.Blightroots.Count; i++) Flinch(_roots[i].Node as Node3D, world.Blightroots[i].Corruption);
        foreach (var (b, h) in _hermits) Flinch(h.Node as Node3D, b.Corruption);
        foreach (var (t, tube) in _tubes) Flinch(tube.Node as Node3D, t.Corruption);
        foreach (var (w, view) in _wardens) Flinch(view.Node as Node3D, w.Corruption);
        foreach (var (l, jaw) in _jaws) Flinch(jaw.Node as Node3D, l.Corruption);
    }
}
