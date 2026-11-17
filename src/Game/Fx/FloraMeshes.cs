using System;
using System.Collections.Generic;
using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>The reef's plants and sessile animals, as in a Caribbean fore-reef. Each species has one look.</summary>
public enum FloraKind
{
    /// <summary>Turtle grass (Thalassia testudinum): strap-like blades in meadows on sandy flats.</summary>
    TurtleGrass,
    /// <summary>Calcareous green algae (Halimeda): chains of small green discs, on sand at the foot of rock.</summary>
    Halimeda,
    /// <summary>Staghorn coral (Acropora cervicornis): branching golden-brown antlers with pale tips, in the sunlit shallows.</summary>
    Staghorn,
    /// <summary>Elkhorn coral (Acropora palmata): broad flattened ochre branches, on the shallowest tops.</summary>
    Elkhorn,
    /// <summary>Brain coral (Diploria): rounded boulders grooved with meandering valleys.</summary>
    BrainCoral,
    /// <summary>Common sea fan (Gorgonia ventalina): a purple lattice fan, broadside to the current.</summary>
    SeaFan,
    /// <summary>Sea rods (Plexaura): bushy soft-coral candelabras that sway in the surge.</summary>
    SeaRod,
    /// <summary>Tube sponges (Aplysina, Callyspongia): clusters of hollow tubes, yellow, purple or lavender.</summary>
    TubeSponge,
    /// <summary>Giant barrel sponge (Xestospongia muta): a ridged red-brown barrel on the deeper slope.</summary>
    BarrelSponge,
    /// <summary>Giant kelp (Macrocystis): an amber stipe hung with blades and their floats, at the walls' feet (the Kelp Jungle).</summary>
    Kelp,
    /// <summary>Giant kelp grown up out of the void from the layer below's floor: the same, drawn to be stretched tall.</summary>
    KelpTall,
    /// <summary>A wreck's hull in the sand: a keel, pairs of curved ribs (some broken), a strake of planking, a fallen mast (the Sunken Galleon).</summary>
    Wreck,
    /// <summary>A broken plank lying on the sand.</summary>
    Plank,
    /// <summary>A vent's chimneys: rough dark stacks, their lips rimmed with an ember of mineral (the Twilight Trench).</summary>
    Vent,
    /// <summary>A whale fall: a spine, its ribs arching over the sand, its skull (the Abyss).</summary>
    WhaleFall,
    /// <summary>A lone vertebra in the sand.</summary>
    Vertebra,
}

/// <summary>
/// Procedural meshes for <see cref="FloraKind"/>, unit-ish real sizes in metres, growing up +Y from the origin
/// (the sea fan's lattice spans the XY plane). Vertex alpha = how freely a part sways (see flora.gdshader).
/// </summary>
public static class FloraMeshes
{
    public static ArrayMesh Build(FloraKind kind) => kind switch
    {
        FloraKind.TurtleGrass => TurtleGrass(),
        FloraKind.Halimeda => Halimeda(),
        FloraKind.Staghorn => Staghorn(),
        FloraKind.Elkhorn => Elkhorn(),
        FloraKind.BrainCoral => BrainCoral(),
        FloraKind.SeaFan => SeaFan(),
        FloraKind.SeaRod => SeaRod(),
        FloraKind.TubeSponge => TubeSponge(),
        FloraKind.Kelp => Kelp(1f),
        FloraKind.KelpTall => Kelp(4f),
        FloraKind.Wreck => Wreck(),
        FloraKind.Plank => Plank(),
        FloraKind.Vent => Vent(),
        FloraKind.WhaleFall => WhaleFall(),
        FloraKind.Vertebra => Vertebra(),
        _ => BarrelSponge(),
    };

    /// <summary>
    /// Giant kelp, a unit tall: a wavering stipe, blades on alternate sides with a float at each blade's foot, a tuft at
    /// its top. <paramref name="stretch"/>: how much taller than wide it is drawn (its blades kept their shape under it).
    /// </summary>
    static ArrayMesh Kelp(float stretch)
    {
        var b = new MeshBuilder();
        var rng = new Random(stretch > 1f ? 41 : 29);
        var stipe = new Color(0.36f, 0.3f, 0.1f);
        var blade = new Color(0.66f, 0.52f, 0.16f);
        const int n = 10;
        var path = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            path[i] = new Vector3(0.03f * Mathf.Sin(t * 7f), t, 0.03f * Mathf.Cos(t * 5f));
        }
        b.Tube(path, s => 0.012f, 5, s => C(stipe.R, stipe.G, stipe.B, s / (float)n));
        int blades = stretch > 1f ? 16 : 9;
        for (int k = 0; k < blades; k++)
        {
            float t = 0.2f + 0.8f * k / (blades - 1f);
            float a = k * 2.4f + (float)rng.NextDouble() * 0.6f;
            var out1 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var at = new Vector3(0.03f * Mathf.Sin(t * 7f), t, 0.03f * Mathf.Cos(t * 5f));
            float length = 0.22f + 0.12f * (float)rng.NextDouble();
            var bp = new Vector3[5];
            for (int s = 0; s < bp.Length; s++)
            {
                float u = s / 4f;
                bp[s] = at + out1 * length * u + Vector3.Up * (0.06f * u - 0.04f * u * u) / stretch;
            }
            Blade(b, bp, s => 0.07f * Mathf.Sin(Mathf.Pi * (0.15f + 0.85f * s / 4f)), Vector3.Up, s => C(blade.R, blade.G, blade.B, t));
            b.Ellipsoid(at + out1 * 0.02f, new Vector3(0.018f, 0.018f / stretch, 0.018f), C(0.78f, 0.62f, 0.2f, t), 5);
        }
        return b.Build();
    }

    /// <summary>A box from its middle, half-sizes along its own axes (x along, y up, z across), turned by <paramref name="basis"/>.</summary>
    static void Box(MeshBuilder b, Vector3 middle, Vector3 half, Basis basis, Color color)
    {
        Vector3 P(float x, float y, float z) => middle + basis * new Vector3(x * half.X, y * half.Y, z * half.Z);
        void Face(Vector3 n, Vector3 a, Vector3 bb, Vector3 c, Vector3 d)
        {
            var nn = (basis * n).Normalized();
            b.Quad(b.Add(a, nn, color), b.Add(bb, nn, color), b.Add(c, nn, color), b.Add(d, nn, color));
        }
        Face(Vector3.Up, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1));
        Face(Vector3.Down, P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1));
        Face(Vector3.Right, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1));
        Face(Vector3.Left, P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1));
        Face(Vector3.Back, P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1));
        Face(Vector3.Forward, P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1));
    }

    static readonly Color Wood = new(0.34f, 0.24f, 0.15f), WoodPale = new(0.5f, 0.42f, 0.3f), Verdigris = new(0.3f, 0.42f, 0.36f);

    /// <summary>A wreck's hull half sunk in the sand, about 4 m long and a metre high.</summary>
    static ArrayMesh Wreck()
    {
        var b = new MeshBuilder();
        var rng = new Random(57);
        b.Tube(new[] { new Vector3(-2.1f, 0.06f, 0f), new Vector3(0f, 0.1f, 0f), new Vector3(2.2f, 0.12f, 0f), new Vector3(2.5f, 0.45f, 0f) }, s => 0.09f, 4, s => Wood);
        for (int r = 0; r < 7; r++)
        {
            float x = -1.7f + r * 0.55f;
            for (int side = -1; side <= 1; side += 2)
            {
                // (Some broken short.)
                float reach = rng.NextDouble() < 0.3 ? 0.4f + 0.4f * (float)rng.NextDouble() : 1f;
                var path = new Vector3[6];
                for (int s = 0; s < path.Length; s++)
                {
                    float t = s / 5f * reach;
                    float narrow = 1f - 0.35f * Mathf.Abs(x) / 2f;
                    path[s] = new Vector3(x, 0.08f + 0.95f * t, side * (0.15f + 0.8f * Mathf.Sqrt(t)) * narrow);
                }
                var c = Wood.Lerp(Verdigris, (float)rng.NextDouble() * 0.35f);
                b.Tube(path, s => 0.05f - 0.004f * s, 4, s => c);
            }
        }
        // A strake of planking still on the ribs along one side, and a few loose.
        for (int k = 0; k < 3; k++)
        {
            float y = 0.3f + 0.22f * k;
            float z = 0.15f + 0.8f * Mathf.Sqrt((y - 0.08f) / 0.95f);
            float len = 1.4f + 0.5f * (float)rng.NextDouble();
            var basis = new Basis(Vector3.Right, -0.5f - 0.3f * k);
            Box(b, new Vector3(-0.6f + 0.3f * k, y, z * 0.9f), new Vector3(len * 0.5f, 0.02f, 0.09f), basis, WoodPale.Lerp(Wood, 0.4f * k));
        }
        // The mast, fallen across the sand.
        b.Tube(new[] { new Vector3(0.4f, 0.12f, -0.3f), new Vector3(2.2f, 0.1f, -1.3f), new Vector3(3.8f, 0.08f, -2.2f) }, s => 0.08f - 0.01f * s, 6, s => Wood.Lerp(WoodPale, 0.3f));
        return b.Build();
    }

    /// <summary>A broken plank, a metre and a little long.</summary>
    static ArrayMesh Plank()
    {
        var b = new MeshBuilder();
        Box(b, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.025f, 0.09f), new Basis(Vector3.Forward, 0.05f), WoodPale);
        Box(b, new Vector3(0.75f, 0.03f, 0.04f), new Vector3(0.18f, 0.02f, 0.05f), new Basis(Vector3.Up, 0.5f), Wood);
        return b.Build();
    }

    /// <summary>A vent's chimneys: three rough dark stacks, each rimmed at its lip with an ember of mineral.</summary>
    static ArrayMesh Vent()
    {
        var b = new MeshBuilder();
        var rng = new Random(71);
        var rock = new Color(0.17f, 0.15f, 0.14f);
        var ochre = new Color(0.55f, 0.38f, 0.16f);
        var ember = new Color(1f, 0.42f, 0.08f);
        for (int k = 0; k < 3; k++)
        {
            float a = k * 2.1f;
            var foot = k == 0 ? Vector3.Zero : new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.35f + 0.1f * k);
            float height = k == 0 ? 1f : 0.45f + 0.2f * (float)rng.NextDouble();
            float r0 = k == 0 ? 0.24f : 0.14f;
            const int n = 7;
            var path = new Vector3[n + 1];
            for (int s = 0; s <= n; s++)
            {
                float t = s / (float)n;
                path[s] = foot + new Vector3(0.04f * Mathf.Sin(t * 5f + k), height * t, 0.04f * Mathf.Cos(t * 4f + k));
            }
            b.Tube(path, s => r0 * (1f - 0.45f * s / n) * (1f + 0.15f * Mathf.Sin(s * 2.7f + k)), 7, s => s % 3 == 1 ? ochre : rock);
            // (Its lip, a ring of ember, and the dark of its throat.)
            b.Disc(path[n] + Vector3.Up * 0.005f, Vector3.Up, r0 * 0.55f, ember, 10);
            b.Disc(path[n] + Vector3.Up * 0.01f, Vector3.Up, r0 * 0.3f, new Color(0.05f, 0.03f, 0.02f), 8);
        }
        return b.Build();
    }

    static readonly Color Bone = new(0.86f, 0.83f, 0.74f), BoneDark = new(0.62f, 0.58f, 0.5f);

    /// <summary>A whale fall, about 5 m long: a spine of vertebrae, ribs arching over the sand, a skull at its head.</summary>
    static ArrayMesh WhaleFall()
    {
        var b = new MeshBuilder();
        var rng = new Random(83);
        for (int v = 0; v < 12; v++)
        {
            float x = -2.4f + v * 0.36f;
            b.Ellipsoid(new Vector3(x, 0.12f, 0f), new Vector3(0.12f, 0.12f, 0.16f), Bone.Lerp(BoneDark, (float)rng.NextDouble() * 0.4f), 6);
            b.Cone(new Vector3(x, 0.2f, 0f), new Vector3(x - 0.05f, 0.42f, 0f), 0.04f, Bone, BoneDark, 4);
        }
        for (int r = 0; r < 6; r++)
        {
            float x = -1.6f + r * 0.42f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() < 0.2) continue;
                var path = new Vector3[7];
                for (int s = 0; s < path.Length; s++)
                {
                    float t = s / 6f;
                    path[s] = new Vector3(x + 0.1f * t, 0.14f + 0.65f * Mathf.Sin(t * Mathf.Pi * 0.85f), side * (0.1f + 1.15f * t));
                }
                b.Tube(path, s => 0.045f - 0.004f * s, 5, s => Bone.Lerp(BoneDark, s / 8f));
            }
        }
        b.Ellipsoid(new Vector3(2.35f, 0.2f, 0f), new Vector3(0.7f, 0.24f, 0.42f), Bone, 10);
        b.Ellipsoid(new Vector3(3.15f, 0.12f, 0f), new Vector3(0.5f, 0.1f, 0.22f), BoneDark, 8);
        return b.Build();
    }

    /// <summary>A lone vertebra, its spines up.</summary>
    static ArrayMesh Vertebra()
    {
        var b = new MeshBuilder();
        b.Ellipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.09f, 0.14f), Bone, 8);
        b.Cone(new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.4f, 0.04f), 0.04f, Bone, BoneDark, 4);
        b.Cone(new Vector3(0.1f, 0.08f, 0f), new Vector3(0.35f, 0.1f, 0f), 0.035f, Bone, BoneDark, 4);
        b.Cone(new Vector3(-0.1f, 0.08f, 0f), new Vector3(-0.35f, 0.1f, 0f), 0.035f, Bone, BoneDark, 4);
        return b.Build();
    }

    static Color C(float r, float g, float b, float a = 0f) => new(r, g, b, a);

    /// <summary>A flat strip (blade) along a path, facing <paramref name="facing"/>, both sides visible.</summary>
    static void Blade(MeshBuilder b, IReadOnlyList<Vector3> path, Func<int, float> width, Vector3 facing, Func<int, Color> color)
    {
        var left = new int[path.Count];
        var right = new int[path.Count];
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tangent = (path[Math.Min(i + 1, path.Count - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            Vector3 side = tangent.Cross(facing).Normalized();
            Vector3 n = side.Cross(tangent).Normalized();
            left[i] = b.Add(path[i] - side * width(i) * 0.5f, n, color(i));
            right[i] = b.Add(path[i] + side * width(i) * 0.5f, n, color(i));
        }
        for (int i = 0; i + 1 < path.Count; i++) b.Quad(left[i], right[i], right[i + 1], left[i + 1]);
    }

    static ArrayMesh TurtleGrass()
    {
        var b = new MeshBuilder();
        var rng = new Random(3);
        for (int k = 0; k < 9; k++)
        {
            float a = (float)rng.NextDouble() * Mathf.Tau;
            var root = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.07f * (float)rng.NextDouble();
            float length = 0.25f + 0.32f * (float)rng.NextDouble();
            var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.15f + 0.25f * (float)rng.NextDouble());
            var path = new Vector3[5];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 4f;
                path[s] = root + (Vector3.Up + lean * t) * length * t;
            }
            bool epiphytes = rng.NextDouble() < 0.5;
            Blade(b, path, s => 0.011f * (s == 4 ? 0.6f : 1f), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), s =>
            {
                float t = s / 4f;
                Color green = new Color(0.2f, 0.45f, 0.13f).Lerp(new Color(0.42f, 0.56f, 0.18f), t);
                if (epiphytes && t > 0.7f) green = green.Lerp(new Color(0.55f, 0.48f, 0.3f), (t - 0.7f) / 0.3f * 0.8f);
                green.A = t * t;
                return green;
            });
        }
        return b.Build();
    }

    static ArrayMesh Halimeda()
    {
        var b = new MeshBuilder();
        var rng = new Random(5);
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * Mathf.Tau + (float)rng.NextDouble() * 0.5f;
            var dir = (Vector3.Up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f).Normalized();
            var at = Vector3.Zero;
            for (int s = 0; s < 5; s++)
            {
                at += dir * 0.035f;
                float t = s / 4f;
                b.Ellipsoid(at, new Vector3(0.022f, 0.008f, 0.02f) * (1f - 0.2f * t), C(0.36f + 0.1f * t, 0.62f, 0.22f, t * 0.5f), 6);
                dir = (dir + new Vector3((float)rng.NextDouble() - 0.5f, 0.2f, (float)rng.NextDouble() - 0.5f) * 0.6f).Normalized();
            }
        }
        return b.Build();
    }

    /// <summary>Branching antlers: each branch forks into two or three, thinner and paler toward the tips.</summary>
    static ArrayMesh Staghorn()
    {
        var b = new MeshBuilder();
        var rng = new Random(7);
        void Branch(Vector3 from, Vector3 dir, float length, float radius, int depth)
        {
            // (Each branch a straight tube: one length of it, tapering, which is all the eye sees of it from the camera.)
            var path = new[] { from, from + dir * length };
            b.Tube(path, s => radius * (1f - 0.25f * s), 5, s =>
            {
                float pale = depth == 0 ? s : 0f;
                return new Color(0.72f, 0.54f, 0.3f).Lerp(new Color(0.96f, 0.92f, 0.82f), pale * 0.85f);
            });
            if (depth == 0) return;
            int forks = 2 + (rng.NextDouble() < 0.15 ? 1 : 0);
            for (int f = 0; f < forks; f++)
            {
                var spread = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.4f, (float)rng.NextDouble() - 0.5f) * 1.2f;
                var nd = (dir + spread).Normalized();
                if (nd.Y < 0.2f) nd = (nd + Vector3.Up * 0.5f).Normalized();
                Branch(path[1], nd, length * 0.85f, radius * 0.75f, depth - 1);
            }
        }
        for (int i = 0; i < 4; i++)
        {
            float a = i / 4f * Mathf.Tau + 0.3f;
            var dir = (Vector3.Up + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.6f).Normalized();
            Branch(Vector3.Zero, dir, 0.2f, 0.035f, 3);
        }
        return b.Build();
    }

    static ArrayMesh Elkhorn()
    {
        var b = new MeshBuilder();
        var ochre = new Color(0.78f, 0.52f, 0.25f);
        b.Tube(new[] { Vector3.Zero, new Vector3(0f, 0.15f, 0f), new Vector3(0f, 0.3f, 0f) }, s => 0.1f - 0.02f * s, 8, _ => ochre);
        var rng = new Random(9);
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.Tau + (float)rng.NextDouble() * 0.4f;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float rise = 0.25f + 0.35f * (float)rng.NextDouble();
            // A broad flattened lobe: a chain of flat ellipsoids, widening then forking into fingers.
            for (int s = 0; s < 5; s++)
            {
                float t = s / 4f;
                var at = new Vector3(0f, 0.28f, 0f) + outward * (0.12f + 0.42f * t) + Vector3.Up * rise * t;
                var basis = Basis.LookingAt(outward + Vector3.Up * rise, Vector3.Up);
                float w = 0.09f + 0.1f * Mathf.Sin(t * Mathf.Pi);
                var color = ochre.Lerp(new Color(0.95f, 0.85f, 0.65f), t * t * 0.7f);
                AddEllipsoid(b, at, basis, new Vector3(w, 0.03f, 0.12f), color);
            }
        }
        return b.Build();
    }

    /// <summary>An ellipsoid with a rotation (MeshBuilder's is axis-aligned).</summary>
    static void AddEllipsoid(MeshBuilder b, Vector3 center, Basis basis, Vector3 radii, Color color)
    {
        // (Few faces: the coral's flat lobes are small on the screen, and there are hundreds of them on a reef.)
        const int seg = 6, rings = 4;
        var ids = new int[rings + 1, seg];
        for (int r = 0; r <= rings; r++)
        {
            float v = r / (float)rings * Mathf.Pi;
            for (int s = 0; s < seg; s++)
            {
                float u = s / (float)seg * Mathf.Tau;
                var n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                var local = n * radii;
                var normal = new Vector3(n.X / radii.X, n.Y / radii.Y, n.Z / radii.Z).Normalized();
                ids[r, s] = b.Add(center + basis * local, (basis * normal).Normalized(), color);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < seg; s++)
            b.Quad(ids[r, s], ids[r, (s + 1) % seg], ids[r + 1, (s + 1) % seg], ids[r + 1, s]);
    }

    static ArrayMesh BrainCoral()
    {
        var b = new MeshBuilder();
        // A dome slightly wider than tall, sunk a little into the rock; the grooves are drawn by the shader.
        b.Ellipsoid(new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.42f, 0.5f), new Color(0.74f, 0.66f, 0.4f, 0f), 18,
            p => new Color(0.72f + 0.06f * Mathf.Sin(p.X * 7f), 0.64f, 0.38f + 0.05f * Mathf.Sin(p.Z * 5f), 0f));
        return b.Build();
    }

    static ArrayMesh SeaFan()
    {
        var b = new MeshBuilder();
        var purple = new Color(0.58f, 0.24f, 0.6f);
        // Stalk.
        b.Tube(new[] { Vector3.Zero, new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0.12f, 0f) }, s => 0.025f, 5, s => C(0.45f, 0.2f, 0.45f, 0f));
        // The fan: a rounded blade in the XY plane, both sides drawn; the shader cuts the lattice.
        const int spokes = 18, rings = 6;
        var ids = new int[rings + 1, spokes + 1];
        for (int r = 0; r <= rings; r++)
        for (int s = 0; s <= spokes; s++)
        {
            float t = r / (float)rings;
            float a = (s / (float)spokes - 0.5f) * Mathf.Pi * 0.95f;
            float radius = 0.08f + 0.62f * t;
            var p = new Vector3(Mathf.Sin(a) * radius * 0.95f, 0.1f + Mathf.Cos(a) * radius, 0.02f * Mathf.Sin(a * 3f + t * 4f));
            var color = purple.Lerp(new Color(0.75f, 0.42f, 0.78f), t * 0.4f);
            color.A = t;
            ids[r, s] = b.Add(p, Vector3.Back, color);
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < spokes; s++)
            b.Quad(ids[r, s], ids[r, s + 1], ids[r + 1, s + 1], ids[r + 1, s]);
        return b.Build();
    }

    static ArrayMesh SeaRod()
    {
        var b = new MeshBuilder();
        var rng = new Random(13);
        var stem = new Color(0.42f, 0.28f, 0.3f);
        b.Tube(new[] { Vector3.Zero, new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.2f, 0f) }, s => 0.03f, 6, s => C(stem.R, stem.G, stem.B, 0f));
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.Tau + (float)rng.NextDouble() * 0.5f;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float height = 0.45f + 0.5f * (float)rng.NextDouble();
            var path = new Vector3[7];
            for (int s = 0; s < path.Length; s++)
            {
                float t = s / 6f;
                path[s] = new Vector3(0f, 0.18f, 0f) + outward * (0.18f * Mathf.Sqrt(t)) + Vector3.Up * height * t;
            }
            // Fuzzy with extended polyps: paler, purplish-tan toward the tips.
            b.Tube(path, s => 0.018f - 0.002f * s, 5, s =>
            {
                float t = s / 6f;
                var c = stem.Lerp(new Color(0.72f, 0.58f, 0.62f), t);
                c.A = t * t;
                return c;
            });
        }
        return b.Build();
    }

    /// <summary>A hollow tube: outside wall, dark inside, a rim; clusters of three to five.</summary>
    static void HollowTube(MeshBuilder b, Vector3 baseAt, Vector3 dir, float radius, float height, Color color)
    {
        var outer = new[] { baseAt, baseAt + dir * height * 0.5f, baseAt + dir * height };
        b.Tube(outer, s => radius * (0.9f + 0.1f * s), 8, s => color);
        b.Tube(outer, s => radius * 0.78f * (0.9f + 0.1f * s), 8, s => color * 0.3f);
        // Rim ring joining outside and inside.
        var top = baseAt + dir * height;
        Vector3 u = dir.Cross(Mathf.Abs(dir.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        Vector3 v = dir.Cross(u);
        var o = new int[10];
        var i2 = new int[10];
        for (int k = 0; k < 10; k++)
        {
            float a = k / 10f * Mathf.Tau;
            var r = u * Mathf.Cos(a) + v * Mathf.Sin(a);
            o[k] = b.Add(top + r * radius * 1.1f, dir, color * 1.1f);
            i2[k] = b.Add(top + r * radius * 0.86f, dir, color * 1.1f);
        }
        for (int k = 0; k < 10; k++) b.Quad(o[k], o[(k + 1) % 10], i2[(k + 1) % 10], i2[k]);
    }

    static ArrayMesh TubeSponge()
    {
        var b = new MeshBuilder();
        var rng = new Random(17);
        int tubes = 4;
        for (int i = 0; i < tubes; i++)
        {
            float a = i / (float)tubes * Mathf.Tau + (float)rng.NextDouble();
            var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f;
            var dir = (Vector3.Up + off * 2.5f).Normalized();
            HollowTube(b, off, dir, 0.055f + 0.025f * (float)rng.NextDouble(), 0.35f + 0.4f * (float)rng.NextDouble(), new Color(1f, 1f, 1f, 0f));
        }
        return b.Build();
    }

    static ArrayMesh BarrelSponge()
    {
        var b = new MeshBuilder();
        const int rings = 8, sides = 20;
        var outer = new int[rings + 1, sides];
        var inner = new int[rings + 1, sides];
        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;
            float radius = 0.26f + 0.12f * Mathf.Sin(t * Mathf.Pi * 0.8f) + 0.08f * t;
            float y = t * 1.1f;
            for (int s = 0; s < sides; s++)
            {
                float a = s / (float)sides * Mathf.Tau;
                // Vertical ridges and stair-step bands.
                float ridge = 1f + 0.06f * Mathf.Abs(Mathf.Sin(a * 7f)) + 0.03f * Mathf.Sin(t * 30f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var col = new Color(0.8f, 0.4f, 0.3f).Lerp(new Color(0.95f, 0.62f, 0.48f), Mathf.Abs(Mathf.Sin(a * 7f)) * 0.6f);
                outer[r, s] = b.Add(dir * radius * ridge + Vector3.Up * y, (dir + Vector3.Down * 0.1f).Normalized(), col);
                inner[r, s] = b.Add(dir * radius * 0.8f + Vector3.Up * Mathf.Max(y, 0.15f), -dir, new Color(0.35f, 0.15f, 0.12f));
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < sides; s++)
        {
            int s1 = (s + 1) % sides;
            b.Quad(outer[r, s], outer[r, s1], outer[r + 1, s1], outer[r + 1, s]);
            b.Quad(inner[r, s], inner[r, s1], inner[r + 1, s1], inner[r + 1, s]);
        }
        for (int s = 0; s < sides; s++)
        {
            int s1 = (s + 1) % sides;
            b.Quad(outer[rings, s], outer[rings, s1], inner[rings, s1], inner[rings, s]);
        }
        return b.Build();
    }
}
