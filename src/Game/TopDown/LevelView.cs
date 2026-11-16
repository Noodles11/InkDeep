using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Game.Fx;
using OctoShoots.Game.Util;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Everything about a level's look that takes time to work out — the ground's meshes, the cave roofs, the flora —
/// prepared off the main thread (<see cref="Prepare"/>), so the level below can be readied while she plays.
/// </summary>
public sealed class LevelShape
{
    public sealed record Surface(Vector3[] Vertices, Vector3[] Normals, Color[] Colors, int[] Indices);

    public LevelMap Map { get; private init; } = null!;
    public List<Surface> Ground { get; } = new();
    /// <summary>Per cave, its roof (null where the roof is flush with the ground).</summary>
    public List<Surface?> Lids { get; } = new();
    /// <summary>Per secret cave, by its index, its roof once its wall is down: its way in open, as any cave's.</summary>
    public Dictionary<int, Surface> OpenLids { get; } = new();
    /// <summary>Per roofed corridor (a shortcut tunnel), by its index, its roof.</summary>
    public Dictionary<int, Surface> TunnelRoofs { get; } = new();
    /// <summary>Per cave opening (a region's mouths in order, then a leak cave's way out), its rock: roof, face and plug.</summary>
    public List<LevelView.DoorRock> Doors { get; } = new();
    public List<FloraItem> Flora { get; } = new();
    public List<Transform3D> Rocks { get; } = new();
    public List<Transform3D> Glows { get; } = new();
    /// <summary>
    /// Its sub-levels (docs/LIGHT-AND-GRAPH.md §7), made with it: the leak cave behind each of its mouths, in
    /// order (none of their own).
    /// </summary>
    public IReadOnlyList<LevelShape> Caves { get; set; } = Array.Empty<LevelShape>();

    /// <summary>A map whose rock other views draw (a cube reef's layer: ReefStreamView): the map and its mouths' rock alone.</summary>
    public static LevelShape Bare(LevelMap map)
    {
        var shape = new LevelShape { Map = map };
        foreach (var door in map.Mouths) shape.Doors.Add(LevelView.DoorSurfaces(map, door));
        // (Its reef life comes cube by cube as each is laid on it: ReefLife, LevelView.AddLife.)
        return shape;
    }

    /// <summary>
    /// A cube reef's life (the coral gardens at its walls' feet and on its floor, the scattered rocks) over one cube's
    /// <paramref name="area"/> of its layer's map (map metres): each point of it belongs to one cube, so cube by cube it
    /// comes out as the whole would. Pure work: safe on any thread.
    /// </summary>
    public static (List<FloraItem> Flora, List<Transform3D> Rocks, List<Transform3D> Glows) ReefLife(LevelMap map, Rect2 area)
    {
        var f = new List<FloraItem>();
        var r = new List<Transform3D>();
        var g = new List<Transform3D>();
        if (!OS.GetCmdlineUserArgs().Contains("--dbg-nodecor")) LevelView.DecorItems(map, f, r, g, area);
        // (A cube reef's walls rise out of view: only the life at their feet and on the floor shows.)
        f.RemoveAll(i => i.Transform.Origin.Y > 0.8f);
        r.RemoveAll(t => t.Origin.Y > 0.8f);
        return (f, r, g);
    }

    /// <summary>Pure work on the map: safe to run on any thread.</summary>
    public static LevelShape Prepare(LevelMap map)
    {
        var shape = new LevelShape { Map = map };
        const int chunk = 32;
        // Every chunk on its own (plain arrays from the map alone), so they are built at once, kept in this order.
        var pieces = new List<Func<LevelShape.Surface?>>();
        for (int cy = 0; cy < map.Size; cy += chunk)
        for (int cx = 0; cx < map.Size; cx += chunk)
        {
            int x0 = cx, y0 = cy;
            pieces.Add(() => LevelView.ChunkSurface(map, x0, y0, Math.Min(x0 + chunk, map.Size), Math.Min(y0 + chunk, map.Size)));
        }
        // The reef carries on past the rim, so swimming along the edge never shows the void beyond the square.
        var spans = new List<(int A, int B, bool Inside)> { (-LevelView.Skirt, 0, false) };
        for (int c = 0; c < map.Size; c += chunk) spans.Add((c, Math.Min(c + chunk, map.Size), true));
        spans.Add((map.Size, map.Size + LevelView.Skirt, false));
        foreach (var sy in spans)
        foreach (var sx in spans)
            if (!sx.Inside || !sy.Inside) pieces.Add(() => LevelView.SkirtSurface(map, sx.A, sy.A, sx.B, sy.B));
        var built = new LevelShape.Surface?[pieces.Count];
        System.Threading.Tasks.Parallel.For(0, pieces.Count, k => built[k] = pieces[k]());
        foreach (var surface in built)
            if (surface is not null) shape.Ground.Add(surface);
        foreach (var cave in map.Caves) shape.Lids.Add(LevelView.LidSurface(map, cave));
        foreach (var rock in map.WeakRocks)
            if (rock.Kind == SealKind.Secret && rock.Cave >= 0 && LevelView.LidSurface(map, map.Caves[rock.Cave], opened: true) is { } open) shape.OpenLids[rock.Cave] = open;
        for (int c = 0; c < map.Corridors.Count; c++)
            if (map.Corridors[c].Roofed && LevelView.TunnelRoofSurface(map, map.Corridors[c]) is { } roof) shape.TunnelRoofs[c] = roof;
        foreach (var door in map.Mouths) shape.Doors.Add(LevelView.DoorSurfaces(map, door));
        if (map.Way is { } way) shape.Doors.Add(LevelView.DoorSurfaces(map, way));
        if (!OS.GetCmdlineUserArgs().Contains("--dbg-nodecor")) LevelView.DecorItems(map, shape.Flora, shape.Rocks, shape.Glows);
        return shape;
    }
}

/// <summary>
/// Shows a <see cref="LevelMap"/>: the heightfield as static 32 m chunks coloured by height band, the canopy layer
/// (arches, cave roofs, overhangs) that fades while Clementine is underneath, the POIs in the landmark colour
/// language, weak rock, and the decoration the generator chose. Purely presentation: nothing here generates,
/// randomises or decides placement.
/// The level below (DESIGN-TOPDOWN §4.6) is shown by a second LevelView placed under the hole (<see cref="Place"/>), drawn
/// only through the shaft (<see cref="SetPortal"/>) until she dives; then it is promoted (<see cref="Promote"/>).
/// </summary>
public partial class LevelView : Node3D
{

    /// <summary>Landmark light language (DESIGN-TOPDOWN §4.2, THEME-BIBLE §6.3).</summary>
    public static Color PoiColor(PoiKind kind) => kind switch
    {
        PoiKind.TreasureCave => new Color(1f, 0.78f, 0.25f),   // gold
        PoiKind.Shop => new Color(0.35f, 0.95f, 0.45f),        // green
        PoiKind.CurseDen => new Color(1f, 0.25f, 0.25f),       // red
        PoiKind.Secret => new Color(0.7f, 0.4f, 1f),           // violet (no beacon)
        PoiKind.CrabDeal => new Color(0.95f, 0.4f, 0.3f),      // crab red (no beacon)
        PoiKind.Exit => new Color(1f, 0.55f, 0.15f),           // orange (the Crack, the way down)
        PoiKind.ShellCache => new Color(0.3f, 0.9f, 0.85f),    // teal (the treasure pool's colour)
        _ => new Color(0.95f, 0.95f, 0.95f),                   // the start: white
    };


    LevelMap _map = null!;
    ShaderMaterial _ground = null!;
    /// <summary>Every material of the level's ground, rock and flora: they all take its frame and the dive's portal.</summary>
    readonly List<ShaderMaterial> _materials = new();
    /// <summary>Drawn below the current level: no Crack of its own yet.</summary>
    bool _below;
    readonly List<(Canopy Canopy, ShaderMaterial Material, float Alpha)> _canopies = new();
    readonly List<ShaderMaterial> _lids = new();
    /// <summary>How far under a cave's roof its sun shadow is cast from.</summary>
    const float LidShadowDrop = 6f;
    /// <summary>
    /// The secret caves still sealed (by cave), and what of them is kept whole till their wall opens: the roof and the
    /// wall itself are never faded nor carved away round her, so nothing shows there is a chamber behind the rock.
    /// </summary>
    readonly HashSet<int> _sealedCaves = new();
    readonly Dictionary<int, List<ShaderMaterial>> _keptWhole = new();
    /// <summary>Per sealed secret cave, its roof over the way in (shown till the wall comes down) and the one that leaves it open.</summary>
    readonly Dictionary<int, (List<MeshInstance3D> Sealed, List<MeshInstance3D> Open)> _lidSwaps = new();
    readonly Dictionary<int, ShaderMaterial> _tunnelRoofs = new();
    Shader _rock = null!;

    /// <summary>Free-standing reef rock in the reef's skin, lumped by <paramref name="lumps"/> metres (reef_rock.gdshader).</summary>
    ShaderMaterial Rock(float lumps, Color? tint = null)
    {
        var m = new ShaderMaterial { Shader = _rock };
        Track(m);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("lumps"), lumps);
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("tint"), tint ?? Colors.White);
        return m;
    }
    FloraViews _flora = null!;
    WovenFish? _wovenFish;

    /// <summary>A woven reef's schools (the look prototype): they wander, and bolt from her (her world position).</summary>
    public void TickWoven(Vector3 her, float dt) => _wovenFish?.Tick(her - Position, dt);
    readonly List<(System.Numerics.Vector2 At, MeshInstance3D Wall, MeshInstance3D? Remains, float Height)> _seals = new();

    /// <summary>A sealed wall's slide down into the sand: how long it takes, and how far down it has gone at t (0..1 of its height).</summary>
    public const float SlideSeconds = 2.2f;

    /// <summary>
    /// The slide's curve: a jolt as it gives (a few centimetres), a moment's strain, then down — slow to start, as heavy rock
    /// is, steady, easing as it beds into the sand.
    /// </summary>
    public static float SlideAt(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        if (t < 0.08f) return 0.015f * Mathf.Sin(t / 0.08f * Mathf.Pi);
        float u = (t - 0.08f) / 0.92f;
        return u * u * (3f - 2f * u);
    }

    /// <summary>
    /// A sealed wall slides down into the sand: its stretch of wall shudders as it gives, then sinks out of sight. A long
    /// one opens only its gate: what stands either side of it (its remains) is there at once, and the wall sinks behind it,
    /// so only the gate is seen going down.
    /// </summary>
    public void Slide(System.Numerics.Vector2 at) => Move(at, up: false);

    /// <summary>The stretch of wall a seal stands as (its carvings are cut in it), if it has one.</summary>
    public MeshInstance3D? SealWall(System.Numerics.Vector2 at) => _seals.Find(s => s.At == at).Wall;

    /// <summary>A barred boss arena's way in rises back up out of the sand: the slide played backwards.</summary>
    public void Raise(System.Numerics.Vector2 at) => Move(at, up: true);

    void Move(System.Numerics.Vector2 at, bool up)
    {
        int i = _seals.FindIndex(s => s.At == at);
        if (i < 0) return;
        // A secret's wall coming down: its cave is no longer hidden (its roof fades over her and carves away again).
        if (!up && _map.WeakRocks.Any(r => r.Center == at && r.Kind == SealKind.Secret) && _map.WeakRocks.First(r => r.Center == at).Cave is int cave and >= 0)
        {
            _sealedCaves.Remove(cave);
            _keptWhole.Remove(cave);
            _seeThrough = default;
            // The rock over its way in goes with the wall: the passage to the chamber lies open.
            if (_lidSwaps.Remove(cave, out var swap))
            {
                foreach (var m in swap.Sealed) m.Visible = false;
                foreach (var m in swap.Open) m.Visible = true;
            }
        }
        var (_, wall, remains, height) = _seals[i];
        if (remains is not null) remains.Visible = true;
        wall.Visible = true;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            // A tremor all through it as it grinds.
            float shake = t < 0.95f ? 0.035f * Mathf.Sin(t * 170f) * (1f - t) : 0f;
            wall.Position = new Vector3(shake, -(height + 0.3f) * SlideAt(up ? 1f - t : t), shake * 0.6f);
        }), 0f, 1f, SlideSeconds);
        // Down, it is gone; up, it is whole again (its remains are part of it).
        tween.TweenCallback(Callable.From(() =>
        {
            wall.Visible = up;
            if (up && remains is not null) remains.Visible = false;
        }));
    }

    public override void _Ready()
    {
        _flora = new FloraViews { Unlimited = true };
        AddChild(_flora);
        _ground = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/topdown_ground.gdshader") };
        _rock = GD.Load<Shader>("res://assets/shaders/reef_rock.gdshader");
        if (OS.GetCmdlineUserArgs().Contains("--dbg-normals")) _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("debug_normals"), true);
    }

    /// <summary>Shows a level: the current one, or (<paramref name="below"/>) the one under the hole, without its Crack.</summary>
    public void Show(LevelShape shape, bool below = false)
    {
        foreach (var child in GetChildren())
            if (child != _flora) child.QueueFree();
        _canopies.Clear();
        _spans.Clear();
        _materials.Clear();
        _seeThrough = default;
        Track(_ground);
        foreach (var m in _flora.Materials) Track(m);
        var map = _map = shape.Map;
        _rockCeiling = null;
        _below = below;

        foreach (var surface in shape.Ground) AddChild(new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = _ground });
        // The caps: the cutout's dotted fill inside all the rock at the cut's height, there all the time (only seen where
        // the rock over it is cut away).
        if (shape.Ground.Count > 0)
            for (int cy = 0; cy < map.Size; cy += CapChunk)
            for (int cx = 0; cx < map.Size; cx += CapChunk)
                if (Cap(_map.Size, (x, y) => SmoothHeight(map, x, y), cx, cy, Math.Min(cx + CapChunk, map.Size), Math.Min(cy + CapChunk, map.Size)) is { } cap)
                    AddChild(new MeshInstance3D { Mesh = ToMesh(cap), MaterialOverride = _ground, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // A woven reef's coral (WovenReef, the look prototype), in its own paint.
        if (map.Pieces.Count > 0)
        {
            var woven = new WovenReefView();
            AddChild(woven);
            woven.Build(map);
            Track(woven.Rigid);
            Track(woven.Soft);
            Track(woven.Fans);
            _wovenFish = new WovenFish();
            AddChild(_wovenFish);
            _wovenFish.Build(map);
            Track(_wovenFish.Material);
        }
        else _wovenFish = null;

        _lids.Clear();
        _sealedCaves.Clear();
        _keptWhole.Clear();
        _lidSwaps.Clear();
        foreach (var rock in map.WeakRocks)
            if (rock.Kind == SealKind.Secret && rock.Cave >= 0) _sealedCaves.Add(rock.Cave);
        for (int i = 0; i < map.Caves.Count; i++)
        {
            var lid = Rock(0f);
            _lids.Add(lid);
            if (_sealedCaves.Contains(i)) KeepWhole(i, lid);
            if (shape.Lids[i] is { } surface)
            {
                // The roof casts no sunlight shadow of its own (a single sheet over the hollow, it shaded itself: a dark
                // pit over every cave, giving the chamber away); a copy a little under it, seen only by the sun, keeps
                // the chamber in shade.
                var sealedLid = AddLid(ToMesh(surface), lid, true);
                if (shape.OpenLids.TryGetValue(i, out var opened)) _lidSwaps[i] = (sealedLid, AddLid(ToMesh(opened), lid, false));
            }
        }
        _tunnelRoofs.Clear();
        foreach (var (corridor, surface) in shape.TunnelRoofs)
        {
            var roof = Rock(0f);
            _tunnelRoofs[corridor] = roof;
            AddChild(new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = roof });
        }
        foreach (var canopy in map.Canopies) AddCanopy(canopy);
        AddShopGates(map);
        AddDoors(map, shape);
        // Sealed walls: the reef wall carried on across their passage (LevelMap.Sealed), in the reef's own ground, so each is
        // one more stretch of wall. SealView carves its emblem; Slide brings it down.
        _seals.Clear();
        foreach (var rock in map.WeakRocks)
        {
            if (SealSurface(map, rock) is not { } surface) continue;
            // A secret's wall is the reef's ground, but its own copy of it, so it is not carved away round her either.
            var face = _ground;
            if (rock.Kind == SealKind.Secret && rock.Cave >= 0)
            {
                face = (ShaderMaterial)_ground.Duplicate();
                Track(face);
                KeepWhole(rock.Cave, face);
            }
            var wall = new MeshInstance3D { Mesh = ToMesh(surface), MaterialOverride = face };
            AddChild(wall);
            // A long one's remains (what stands either side of its gate once it opens), hidden till then.
            MeshInstance3D? remains = null;
            if (rock.Gate < rock.Radius && RemainsSurface(map, rock) is { } rest)
            {
                remains = new MeshInstance3D { Mesh = ToMesh(rest), MaterialOverride = _ground, Visible = false };
                AddChild(remains);
            }
            _seals.Add((rock.Center, wall, remains, SealDrop(map, rock)));
        }
        ShowDecor(shape);
        _cover = _rift = null;
        _riftMat = null;
        _sandFall = null;
        _crackOpen = 0f;
        _crackFade = 1f;
        // (A boss over the Crack; a cube reef's arena has no shaft, and no Crack.)
        if (map.HasBoss && map.Shaft.HalfLength > 0f) ShowCover(map);
        ShowHoleCovers(map);
        if (!below) ShowCrack(map);
        if (OS.GetCmdlineUserArgs().Contains("--dbg-nocanopy")) foreach (var c in _canopies) c.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), 0f);
    }

    /// <summary>A cave's roof, and its copy under it that only the sun sees.</summary>
    List<MeshInstance3D> AddLid(ArrayMesh mesh, ShaderMaterial lid, bool visible)
    {
        var top = new MeshInstance3D { Mesh = mesh, MaterialOverride = lid, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = visible };
        var shade = new MeshInstance3D { Mesh = mesh, MaterialOverride = lid, CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly, Position = new Vector3(0f, -LidShadowDrop, 0f), Visible = visible };
        AddChild(top);
        AddChild(shade);
        return new List<MeshInstance3D> { top, shade };
    }

    void Track(ShaderMaterial m)
    {
        if (!_materials.Contains(m)) _materials.Add(m);
    }

    void KeepWhole(int cave, ShaderMaterial m)
    {
        if (!_keptWhole.TryGetValue(cave, out var list)) _keptWhole[cave] = list = new List<ShaderMaterial>();
        list.Add(m);
    }

    /// <summary>Whether this material is kept whole (a sealed secret's roof or wall).</summary>
    bool KeptWhole(ShaderMaterial m) => _keptWhole.Values.Any(list => list.Contains(m));

    /// <summary>
    /// Where the level sits in the world (its offset) and its absolute origin, which its patterns are drawn from: a
    /// level looks the same wherever it is placed, so moving it back to the origin after the dive changes nothing.
    /// </summary>
    public void Place(Vector3 offset, Vector2 origin)
    {
        Position = offset;
        foreach (var m in _materials)
        {
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("level_offset"), offset);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("level_origin"), origin);
        }
    }

    /// <summary>The level's corruption on its floor (docs/CORRUPTION.md): the field's texture, or null for none.</summary>
    public void SetCorruption(Texture2D? texture)
    {
        foreach (var m in _materials)
        {
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("corruption_map"), texture is null ? default(Variant) : texture);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("corruption_on"), texture is null ? 0f : 1f);
        }
    }

    /// <summary>The dive's portal: world xz centre, radius, and mode (0 off, 1 cut away inside, 2 only the inside).</summary>
    /// <summary>
    /// Rock between the camera and her plane round her (world xz, this far round, at her plane's height) is carved away by
    /// this much (0 none), so no wall ever hides her (level_frame's lf_see_cut).
    /// </summary>
    public void SeeThrough(Vector3 her, float radius, float amount)
    {
        var v = new Vector4(her.X, her.Z, radius, amount);
        if (v == _seeThrough) return;
        _seeThrough = v;
        _seeReach = radius;
        if (amount > 0f) SeeHeights(her);
        foreach (var m in _materials)
        {
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_through"), KeptWhole(m) ? v with { W = 0f } : v);
            // (Cut at the swim plane's own height, never her bobbing: where the caps lie.)
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_through_y"), Position.Y + LevelMap.SwimBand);
        }
    }
    Vector4 _seeThrough;

    /// <summary>
    /// The height (local metres) the cutout cuts at, and the caps lie at: CapOver over the swim plane (level_frame's
    /// LF_SEE_OVER); the caps a hair under it, so the cut rock's rim stands just over them.
    /// </summary>
    public const float CapOver = 0.5f, CapHeight = LevelMap.SwimBand + CapOver - 0.02f;
    const int CapChunk = 32;

    /// <summary>The caps' vertex colour: its alpha marks them for topdown_ground (and keeps them from rising with the walls).</summary>
    static readonly Color CapColour = new(0.5f, 0.5f, 0.5f, 0.5f);

    /// <summary>The depth into the rock the caps' colour carries, at most (metres: topdown_ground's CAP_DEPTH).</summary>
    const float CapDepth = 6f;

    /// <summary>
    /// A cap over a square of ground (its height there): flat at CapHeight wherever the rock stands over it, its outline
    /// on the rock's own line at that height. Null where no rock does.
    /// </summary>
    public static LevelShape.Surface? Cap(int size, Func<float, float, float> height, int x0, int y0, int x1, int y1)
    {
        if (SurfaceMesh(size, (_, _) => CapHeight, x0, y0, x1, y1, null, (x, y) => height(x, y) - CapHeight) is not { } s) return null;
        // (Each vertex's depth into the rock in its red, over CapDepth metres: the field over its slope, so near the edge
        // it is the distance itself.)
        var colours = new Color[s.Colors.Length];
        const float e = 0.25f;
        for (int k = 0; k < colours.Length; k++)
        {
            float x = s.Vertices[k].X, y = s.Vertices[k].Z;
            float f = height(x, y) - CapHeight;
            float gx = (height(x + e, y) - height(x - e, y)) / (2f * e), gy = (height(x, y + e) - height(x, y - e)) / (2f * e);
            float slope = Mathf.Max(Mathf.Sqrt(gx * gx + gy * gy), 0.05f);
            colours[k] = CapColour with { R = Mathf.Clamp(Mathf.Max(f, 0f) / slope / CapDepth, 0f, 1f) };
        }
        var normals = new Vector3[s.Normals.Length];
        Array.Fill(normals, Vector3.Up);
        return new LevelShape.Surface(s.Vertices, normals, colours, s.Indices);
    }

    /// <summary>
    /// The map round her for the cutout, a metre a texel (level_frame's see_heights, see_keep, see_heights_box): its
    /// height (for the see floor: it caps only what lies under rock), and which rock the cut may take. Every material
    /// that is cut reads it (the cube reef's too: SeeMask).
    /// </summary>
    const int SeeHeightsSize = 192;
    ImageTexture? _seeHeights, _seeKeep;
    float[] _seeH = Array.Empty<float>();
    Vector2 _seeHeightsAt = new(float.MaxValue, float.MaxValue), _seeKeepAt = new(float.MaxValue, float.MaxValue);
    Vector2 _seeKeepToward;
    byte[]? _seeKeepWas;
    float _seeReach;

    /// <summary>How far past her line (metres, along the wall) a wall the cut takes is taken with it: so it goes whole.</summary>
    const float SeeWholeReach = 24f;
    Vector3 _seeBox;

    /// <summary>The cutout's map round her (null until it is first cut), for the cube reef's rock.</summary>
    public (Texture2D Heights, Texture2D Keep, Vector3 Box)? SeeMask =>
        _seeHeights is null || _seeKeep is null ? null : (_seeHeights, _seeKeep, _seeBox);

    /// <summary>
    /// How far to her side of the line from her to the camera rock hides her (metres): her body and a margin; and how far
    /// for a wall already cut, so a wall at the edge of it does not flick between whole and cut as she moves.
    /// </summary>
    const float SeeHideReach = 1.6f, SeeHideKeep = 2.8f;

    /// <summary>
    /// The height map round her made again once she has swum a quarter of its span from where it was made; which rock
    /// the cut may take made again as she moves (SeeKeep).
    /// </summary>
    void SeeHeights(Vector3 her)
    {
        var at = new Vector2(her.X - Position.X, her.Z - Position.Z);
        int n = SeeHeightsSize;
        if (_seeHeights is null || at.DistanceTo(_seeHeightsAt) >= n * 0.25f)
        {
            _seeHeightsAt = at;
            float x0 = Mathf.Floor(at.X) - n / 2, y0 = Mathf.Floor(at.Y) - n / 2;
            var data = new float[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int gx = (int)x0 + i, gy = (int)y0 + j;
                float h = -100f;
                if (gx >= 0 && gy >= 0 && gx <= _map.Size && gy <= _map.Size)
                {
                    h = _map[gx, gy];
                    // (A tunnel's roof or a cave's lid is rock over it too.)
                    float roof = _map.RoofAt(gx, gy), lid = _map.LidAt(gx, gy);
                    if (!float.IsNaN(roof)) h = Mathf.Max(h, roof);
                    if (!float.IsNaN(lid)) h = Mathf.Max(h, lid);
                }
                data[j * n + i] = h + Position.Y;
            }
            _seeH = data;
            var bytes = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            var image = Image.CreateFromData(n, n, false, Image.Format.Rf, bytes);
            if (_seeHeights is null) _seeHeights = ImageTexture.CreateFromImage(image);
            else _seeHeights.Update(image);
            // (Texel centres on the metres: its corner half a metre short of the first.)
            _seeBox = new Vector3(x0 - 0.5f + Position.X, y0 - 0.5f + Position.Z, n);
            _seeKeepAt = new(float.MaxValue, float.MaxValue);
            _seeKeepWas = null;
        }
        var cam = GetViewport()?.GetCamera3D();
        var toward = cam is null ? new Vector2(0f, 1f) : new Vector2(cam.GlobalPosition.X - her.X, cam.GlobalPosition.Z - her.Z);
        toward = toward.LengthSquared() < 1e-6f ? new Vector2(0f, 1f) : toward.Normalized();
        if (_seeKeep is null || at.DistanceTo(_seeKeepAt) >= 0.4f || toward.Dot(_seeKeepToward) < 0.999f)
        {
            _seeKeepAt = at;
            _seeKeepToward = toward;
            var keep = SeeKeep(_seeH, n, _seeBox, her, Position.Y + LevelMap.SwimBand, toward, _seeReach, _seeKeepWas);
            _seeKeepWas = keep;
            var image = Image.CreateFromData(n, n, false, Image.Format.R8, keep);
            if (_seeKeep is null) _seeKeep = ImageTexture.CreateFromImage(image);
            else _seeKeep.Update(image);
        }
        foreach (var m in _materials)
        {
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_heights"), _seeHeights);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_keep"), _seeKeep);
            m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_heights_box"), _seeBox);
        }
    }

    /// <summary>
    /// Where the cut takes the rock (255) and where it leaves it (0), over the height map. The rock standing over her plane
    /// is gathered into its walls (cells joined side to side). A wall with nothing on the camera's side of her is left;
    /// one reaching back past her line (more than reach behind her) is left whole unless it hides her (it crosses the
    /// line from her toward the camera within SeeHideReach of it, or SeeHideKeep for a wall it took before); every other
    /// wall in front of her is taken: all of it on the camera's side of her line, and on from there along the wall for
    /// SeeWholeReach behind it, so a wall goes whole, never leaving faces of it standing. The open floor round what is
    /// taken is marked taken for two metres, so the rock's smooth skin past its cells goes with it.
    /// </summary>
    static byte[] SeeKeep(float[] heights, int n, Vector3 box, Vector3 her, float plane, Vector2 toward, float reach, byte[]? before)
    {
        var keep = new byte[n * n];
        float over = plane + CapOver;
        var wall = new int[n * n];
        Array.Fill(wall, -1);
        var front = new float[n * n];
        var cells = new List<int>();
        var stack = new Stack<int>();
        var queue = new Queue<int>();
        var along = new float[n * n];
        int walls = 0;
        for (int start = 0; start < n * n; start++)
        {
            if (wall[start] >= 0 || heights[start] <= over) continue;
            cells.Clear();
            stack.Push(start);
            wall[start] = walls;
            bool back = false, ahead = false, hides = false, cut = false, near = false;
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                cells.Add(c);
                int cx = c % n, cy = c / n;
                float x = box.X + 0.5f + cx - her.X, z = box.Y + 0.5f + cy - her.Z;
                float f = x * toward.X + z * toward.Y + reach, side = Mathf.Abs(x * toward.Y - z * toward.X);
                front[c] = f;
                // (Past her line by a cell more than the cut reaches back.)
                if (f < -1f) back = true;
                if (f > 0f) { ahead = true; if (side < SeeHideReach) hides = true; else if (side < SeeHideKeep) near = true; }
                if (before is not null && before[c] != 0) cut = true;
                if (cx > 0) Visit(c - 1);
                if (cx < n - 1) Visit(c + 1);
                if (cy > 0) Visit(c - n);
                if (cy < n - 1) Visit(c + n);
            }
            walls++;
            if (!ahead || back && !hides && !(near && cut)) continue;
            // Taken: on the camera's side of her line, and on along the wall from there.
            queue.Clear();
            foreach (int c in cells)
            {
                along[c] = float.MaxValue;
                if (front[c] > 0f) { along[c] = 0f; keep[c] = 255; queue.Enqueue(c); }
            }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                if (along[c] >= SeeWholeReach) continue;
                int cx = c % n, cy = c / n;
                if (cx > 0) Spread(c, c - 1);
                if (cx < n - 1) Spread(c, c + 1);
                if (cy > 0) Spread(c, c - n);
                if (cy < n - 1) Spread(c, c + n);
            }
        }
        // The open floor: on the camera's side of her line all it has over it is taken (a roof, an arch over a passage in
        // front of her), and round what is taken (the rock's skin past its cells) too; but not within two metres of rock
        // left standing, so its skin stays with it.
        var rock = (byte[])keep.Clone();
        for (int pass = 0; pass < 2; pass++)
        {
            var was = (byte[])keep.Clone();
            for (int c = 0; c < n * n; c++)
            {
                if (heights[c] > over || was[c] != 0) continue;
                int cx = c % n, cy = c / n;
                if (cx > 0 && was[c - 1] != 0 || cx < n - 1 && was[c + 1] != 0 || cy > 0 && was[c - n] != 0 || cy < n - 1 && was[c + n] != 0)
                    keep[c] = 255;
            }
        }
        var nearLeft = new int[n * n];
        Array.Fill(nearLeft, int.MaxValue);
        queue.Clear();
        for (int c = 0; c < n * n; c++)
            if (heights[c] > over && rock[c] == 0) { nearLeft[c] = 0; queue.Enqueue(c); }
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            if (nearLeft[c] >= 2) continue;
            int cx = c % n, cy = c / n;
            foreach (int q in new[] { cx > 0 ? c - 1 : -1, cx < n - 1 ? c + 1 : -1, cy > 0 ? c - n : -1, cy < n - 1 ? c + n : -1 })
                if (q >= 0 && heights[q] <= over && nearLeft[q] > nearLeft[c] + 1) { nearLeft[q] = nearLeft[c] + 1; queue.Enqueue(q); }
        }
        for (int c = 0; c < n * n; c++)
        {
            if (heights[c] > over) continue;
            if (nearLeft[c] <= 2) { keep[c] = 0; continue; }
            float x = box.X + 0.5f + c % n - her.X, z = box.Y + 0.5f + c / n - her.Z;
            if (x * toward.X + z * toward.Y + reach > 0f) keep[c] = 255;
        }
        return keep;

        void Visit(int c)
        {
            if (wall[c] >= 0 || heights[c] <= over) return;
            wall[c] = walls;
            stack.Push(c);
        }

        void Spread(int from, int to)
        {
            if (heights[to] <= over || wall[to] != wall[from] || along[to] <= along[from] + 1f) return;
            along[to] = along[from] + 1f;
            keep[to] = 255;
            queue.Enqueue(to);
        }
    }

    /// <summary>How far round her middle (metres) rock counts as hiding her: her bell and the strands under it.</summary>
    const float HerReach = 0.8f;

    /// <summary>
    /// Whether rock (the reef, a tunnel's roof, a cave's lid) stands between the camera and her, over any part of her
    /// (her middle, and round it to HerReach): only then is the rock in front carved away (SeeThrough), so walls she
    /// merely swims beside stay whole.
    /// </summary>
    public bool Hides(Vector3 her, Vector3 camera)
    {
        her -= Position;
        camera -= Position;
        for (int k = 0; k <= 8; k++)
        {
            float a = k * Mathf.Tau / 8f;
            var at = k == 0 ? her : her + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * HerReach;
            var d = camera - at;
            if (d.Y <= 0.1f) continue;
            for (float h = at.Y + 0.3f; h < Mathf.Min(camera.Y, RockCeiling); h += 0.35f)
            {
                var p = at + d * ((h - at.Y) / d.Y);
                if (RockTop(p.X, p.Z) > h) return true;
            }
        }
        return false;
    }

    /// <summary>The level's highest rock (nothing above it can hide her).</summary>
    float RockCeiling => _rockCeiling ??= _map.Heights.Concat(_map.Roof).Concat(_map.Lid).Where(h => !float.IsNaN(h)).Max() + 0.5f;
    float? _rockCeiling;

    /// <summary>The highest rock at a point: the reef, or a tunnel's roof or a cave's lid over it.</summary>
    float RockTop(float x, float y) => Mathf.Max(SmoothHeight(_map, x, y), Mathf.Max(SmoothTunnelRoof(_map, x, y), SmoothRoof(_map, x, y)));

    public void SetPortal(Vector2 centre, float radius, int mode)
    {
        foreach (var m in _materials) m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("portal"), new Vector4(centre.X, centre.Y, radius, mode));
    }

    /// <summary>The level below becomes the current one, back at the origin: its Crack, its lights.</summary>
    public void Promote(Vector2 origin)
    {
        _below = false;
        Place(Vector3.Zero, origin);
        SetPortal(Vector2.Zero, 0f, 0);
        ShowCrack(_map);
    }

    internal static ArrayMesh ToMesh(LevelShape.Surface surface)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = surface.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = surface.Normals;
        arrays[(int)Mesh.ArrayType.Color] = surface.Colors;
        arrays[(int)Mesh.ArrayType.Index] = surface.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // ───────────────────────── ground ─────────────────────────

    /// <summary>Mesh vertices per metre: half-metre steps keep steep walls and summits smooth.</summary>
    const int Sub = 2;

    /// <summary>
    /// Colour by height: the seabed under the swim level by depth (sand shallows, sea-green mid water, blue-black deeps),
    /// the reef wall breaking the swim level, steep reef rock, coralline peaks, the dark rim.
    /// </summary>
    static Color Band(float h, float x, float y, int size)
    {
        Color c;
        if (h < 0f)
        {
            float depth = -h;
            c = depth <= 4f ? new Color(0.8f, 0.72f, 0.53f).Lerp(new Color(0.55f, 0.6f, 0.47f), depth / 4f)
                : depth <= 10f ? new Color(0.55f, 0.6f, 0.47f).Lerp(new Color(0.22f, 0.36f, 0.4f), (depth - 4f) / 6f)
                : new Color(0.22f, 0.36f, 0.4f).Lerp(new Color(0.04f, 0.1f, 0.17f), Mathf.Clamp((depth - 10f) / 8f, 0f, 1f));
        }
        else if (h <= 6f) c = new Color(0.5f, 0.54f, 0.36f).Lerp(new Color(0.43f, 0.4f, 0.33f), h / 6f);
        else if (h <= 10f) c = new Color(0.43f, 0.4f, 0.33f).Lerp(new Color(0.4f, 0.36f, 0.32f), (h - 6f) / 4f);
        else c = new Color(0.4f, 0.36f, 0.32f).Lerp(new Color(0.66f, 0.43f, 0.52f), Mathf.Clamp((h - 10f) / 4f, 0f, 1f));
        float e = Mathf.Max(Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - x, size - y)), 0f);
        if (e < LevelMap.RimWidth) c = c.Lerp(new Color(0.24f, 0.23f, 0.26f), 1f - e / LevelMap.RimWidth);
        // A little grain so flat colour reads as ground.
        uint hash = unchecked((uint)((int)(x * Sub) * 73856093 ^ (int)(y * Sub) * 19349663));
        float grain = 0.95f + 0.1f * ((hash & 1023) / 1023f);
        return new Color(c.R * grain, c.G * grain, c.B * grain);
    }

    /// <summary>
    /// The terrain as a smooth surface: bicubic (Catmull-Rom) through the height samples, so it still passes through
    /// every grid point the simulation collides against, but without creases between them. Around the Crack it is
    /// plain bilinear: its walls drop sheer, without the bicubic's ripples. (A blue hole's bowl slopes smoothly, and is
    /// smooth all through: no seam rings it.)
    /// </summary>
    static float SmoothHeight(LevelMap map, float x, float y) =>
        map.HasBoss && map.Shaft.Contains(new System.Numerics.Vector2(x, y), 3f) ? map.HeightAt(new System.Numerics.Vector2(x, y)) : SmoothSample((i, j) => map[i, j], x, y, map.Size);

    /// <summary>A tunnel's roof where there is one, the terrain elsewhere (so its edge meets the ground exactly).</summary>
    static float SmoothTunnelRoof(LevelMap map, float x, float y) => SmoothSample((i, j) => float.IsNaN(map.RoofAt(i, j)) ? map[i, j] : map.RoofAt(i, j), x, y, map.Size);

    /// <summary>The cave roof where there is one, the terrain elsewhere (so a roof's edge meets the ground exactly).</summary>
    static float SmoothRoof(LevelMap map, float x, float y) => SmoothSample((i, j) => float.IsNaN(map.LidAt(i, j)) ? map[i, j] : map.LidAt(i, j), x, y, map.Size);

    static float SmoothSample(Func<int, int, float> grid, float x, float y, int size)
    {
        x = Mathf.Clamp(x, 0f, size);
        y = Mathf.Clamp(y, 0f, size);
        int ix = Math.Min((int)x, size - 1), iy = Math.Min((int)y, size - 1);
        float fx = x - ix, fy = y - iy;
        float Row(int j)
        {
            int yy = Math.Clamp(j, 0, size);
            float H(int i) => grid(Math.Clamp(i, 0, size), yy);
            return CatmullRom(H(ix - 1), H(ix), H(ix + 1), H(ix + 2), fx);
        }
        float v = CatmullRom(Row(iy - 1), Row(iy), Row(iy + 1), Row(iy + 2), fy);
        // Never past the cell's own corners: over a big drop the spline would ring (ridges and pits along a cliff).
        float c00 = grid(ix, iy), c10 = grid(Math.Min(ix + 1, size), iy), c01 = grid(ix, Math.Min(iy + 1, size)), c11 = grid(Math.Min(ix + 1, size), Math.Min(iy + 1, size));
        return Mathf.Clamp(v, Mathf.Min(Mathf.Min(c00, c10), Mathf.Min(c01, c11)), Mathf.Max(Mathf.Max(c00, c10), Mathf.Max(c01, c11)));
    }

    internal static float CatmullRom(float p0, float p1, float p2, float p3, float t) =>
        0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);

    /// <summary>The way a sealed wall faces: toward the side it is met from.</summary>
    public static System.Numerics.Vector2 SealFacing(LevelMap map, WeakRock rock) =>
        rock.Thick > 0f ? rock.Facing : rock.Cave >= 0 && rock.Cave < map.Caves.Count ? map.Caves[rock.Cave].Facing : System.Numerics.Vector2.UnitY;

    /// <summary>Whether a grid point is part of this sealed wall (nearer it than any other).</summary>
    static bool OfSeal(LevelMap map, WeakRock rock, int x, int y)
    {
        var p = new System.Numerics.Vector2(x, y);
        float reach = rock.Radius + (rock.Thick > 0f ? rock.Thick : rock.Radius) + 10f;
        if (System.Numerics.Vector2.Distance(p, rock.Center) > reach) return false;
        foreach (var other in map.WeakRocks)
            if (other != rock && System.Numerics.Vector2.Distance(p, other.Center) < System.Numerics.Vector2.Distance(p, rock.Center)) return false;
        return true;
    }

    /// <summary>The terrain with this sealed wall standing in it (LevelMap.Sealed where it stands, the ground elsewhere), smooth.</summary>
    public static float SealHeight(LevelMap map, WeakRock rock, float x, float y) =>
        SmoothSample((i, j) => float.IsNaN(map.SealedAt(i, j)) || !OfSeal(map, rock, i, j) ? map[i, j] : map.SealedAt(i, j), x, y, map.Size);

    /// <summary>The terrain with what a long sealed wall leaves standing once its gate is open (LevelMap.SealedOpen), smooth.</summary>
    public static float RemainsHeight(LevelMap map, WeakRock rock, float x, float y) =>
        SmoothSample((i, j) => float.IsNaN(map.SealedOpenAt(i, j)) || !OfSeal(map, rock, i, j) ? map[i, j] : map.SealedOpenAt(i, j), x, y, map.Size);

    /// <summary>A long sealed wall's remains: only where they stand above the ground.</summary>
    internal static LevelShape.Surface? RemainsSurface(LevelMap map, WeakRock rock)
    {
        int reach = (int)(rock.Radius + rock.Thick) + 10;
        int x0 = Math.Max(0, (int)rock.Center.X - reach), x1 = Math.Min(map.Size, (int)rock.Center.X + reach);
        int y0 = Math.Max(0, (int)rock.Center.Y - reach), y1 = Math.Min(map.Size, (int)rock.Center.Y + reach);
        return SurfaceMesh(map.Size, (x, y) => RemainsHeight(map, rock, x, y) + 0.02f, x0, y0, x1, y1, (x, y) => RemainsHeight(map, rock, x, y) > SmoothHeight(map, x, y) + 0.04f);
    }

    /// <summary>How far the sealed wall stands over the ground at its tallest (how far it has to slide down, below its remains too).</summary>
    public static float SealDrop(LevelMap map, WeakRock rock)
    {
        float most = 0f;
        int r = (int)(rock.Radius + rock.Thick) + 10;
        for (int y = Math.Max(0, (int)rock.Center.Y - r); y <= Math.Min(map.Size, (int)rock.Center.Y + r); y++)
        for (int x = Math.Max(0, (int)rock.Center.X - r); x <= Math.Min(map.Size, (int)rock.Center.X + r); x++)
            if (!float.IsNaN(map.SealedAt(x, y)) && OfSeal(map, rock, x, y)) most = Mathf.Max(most, map.SealedAt(x, y) - map[x, y]);
        return most;
    }

    /// <summary>
    /// Where a sealed wall meets the seabed: points along its feet, on the passage floor at the foot of its faces (the
    /// silt is pushed up there as it slides down).
    /// </summary>
    public static List<Vector3> SealFoot(LevelMap map, WeakRock rock)
    {
        var feet = new List<Vector3>();
        int r = (int)(rock.Radius + rock.Thick) + 4;
        for (int y = Math.Max(1, (int)rock.Center.Y - r); y <= Math.Min(map.Size - 1, (int)rock.Center.Y + r); y++)
        for (int x = Math.Max(1, (int)rock.Center.X - r); x <= Math.Min(map.Size - 1, (int)rock.Center.X + r); x++)
        {
            float s0 = map.SealedAt(x, y);
            if (float.IsNaN(s0) || s0 - map[x, y] < 0.4f || map[x, y] > LevelMap.BlockHeight || !OfSeal(map, rock, x, y)) continue;
            // A long one goes down only at its gate.
            if (rock.Gate < rock.Radius && MathF.Abs(System.Numerics.Vector2.Dot(new System.Numerics.Vector2(x, y) - rock.Center, rock.Across)) > rock.Gate + 0.5f) continue;
            // At its edge: a neighbour on the open floor with no wall over it.
            bool edge = false;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                float sn = map.SealedAt(x + dx, y + dy);
                if ((float.IsNaN(sn) || sn - map[x + dx, y + dy] < 0.3f) && map[x + dx, y + dy] <= LevelMap.BlockHeight) edge = true;
            }
            if (edge) feet.Add(new Vector3(x, map[x, y] + 0.1f, y));
        }
        return feet;
    }

    /// <summary>A sealed wall's stretch of wall: only where it stands above the ground.</summary>
    internal static LevelShape.Surface? SealSurface(LevelMap map, WeakRock rock)
    {
        int reach = (int)(rock.Radius + rock.Thick) + 10;
        int x0 = Math.Max(0, (int)rock.Center.X - reach), x1 = Math.Min(map.Size, (int)rock.Center.X + reach);
        int y0 = Math.Max(0, (int)rock.Center.Y - reach), y1 = Math.Min(map.Size, (int)rock.Center.Y + reach);
        return SurfaceMesh(map.Size, (x, y) => SealHeight(map, rock, x, y) + 0.01f, x0, y0, x1, y1, (x, y) => SealHeight(map, rock, x, y) > SmoothHeight(map, x, y) + 0.04f);
    }

    /// <summary>
    /// A chunk of the ground. The shaft has no floor: through it, the level below shows. (An abyss reef's dropped floor is
    /// drawn all the way down, so the water's murk takes it smoothly rather than at a cut edge.)
    /// </summary>
    internal static LevelShape.Surface? ChunkSurface(LevelMap map, int x0, int y0, int x1, int y1) =>
        SurfaceMesh(map.Size, (x, y) => SmoothHeight(map, x, y), x0, y0, x1, y1,
            (x, y) => SmoothHeight(map, x, y) > LevelMap.ShaftBottom + 0.3f || map.Abyss && !map.InShaft(new System.Numerics.Vector2(x, y), 3f));


    /// <summary>How far (metres) the reef is drawn past the square on every side: wider than the view reaches.</summary>
    internal const int Skirt = 32;

    /// <summary>
    /// The reef past the square, never reached: the rim's own rock where it meets the edge, rising within a few metres
    /// into a ragged ridge at about the rim's top, so the edge reads as more reef rather than a cliff into nothing.
    /// </summary>
    internal static LevelShape.Surface? SkirtSurface(LevelMap map, int x0, int y0, int x1, int y1) =>
        SurfaceMesh(map.Size, (x, y) => SkirtHeight(map, x, y), x0, y0, x1, y1, null);

    static float SkirtHeight(LevelMap map, float x, float y)
    {
        float cx = Mathf.Clamp(x, 0f, map.Size), cy = Mathf.Clamp(y, 0f, map.Size);
        float edge = SmoothHeight(map, cx, cy);
        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        if (d <= 0f) return edge;
        // Ragged, but the same every time (it is drawn from the position alone).
        float ridge = ReefTerrain.HMax - 1f + 1.6f * Mathf.Sin(x * 0.37f + Mathf.Sin(y * 0.21f) * 2f) * Mathf.Sin(y * 0.29f + Mathf.Sin(x * 0.17f) * 2f);
        return Mathf.Lerp(edge, ridge, Mathf.SmoothStep(0f, 6f, d));
    }

    /// <summary>
    /// A cave's roof: the rock carried on over the chambers and a little past them, so the cave reads as solid reef. Its
    /// height is the mountain as it stood before the cave was hollowed out, raised wherever the rock round it stands
    /// higher (a smooth surface spanned between the heights at its edge), so it never sinks into a basin that gives the
    /// chamber away. A secret room's roof runs on over its way in to the middle of its sealed wall, meeting the wall's
    /// top, so nothing shows a passage behind it (<paramref name="opened"/>: its wall is down, and its way in lies open as
    /// any cave's). It fades like any canopy while Clementine is inside.
    /// </summary>
    internal static LevelShape.Surface? LidSurface(LevelMap map, CaveSite cave, bool opened = false)
    {
        int index = map.Caves.IndexOf(cave);
        WeakRock? seal = opened ? null : map.WeakRocks.Where(r => r.Kind == SealKind.Secret && r.Cave == index).Select(r => (WeakRock?)r).FirstOrDefault();
        var spur = map.Corridors.FirstOrDefault(c => c.Poi == cave.Poi);
        float spurHalf = (spur?.HalfWidth ?? 2f) + 4f;
        // How far along its way in the roof runs: a secret's to the middle of its wall, a shop's to its gateway's face.
        bool shop = map.Pois[cave.Poi].Kind == PoiKind.Shop;
        var gate = shop ? ShopGate(map, cave) : default;
        System.Numerics.Vector2? end = seal?.Center ?? (shop ? gate.At : null);
        float cut = seal is not null ? 0f : GateDepth * 0.5f;
        var facing = cave.Facing;
        // A shop's roof keeps the height of the walls beside its passage out to its face, well over the gateway.
        float face = 0f;
        if (shop)
        {
            var across = new System.Numerics.Vector2(-facing.Y, facing.X);
            var l = gate.At + across * spurHalf;
            var r = gate.At - across * spurHalf;
            face = MathF.Max(0.5f * (SmoothHeight(map, l.X, l.Y) + SmoothHeight(map, r.X, r.Y)), gate.Crown + 1.6f);
        }
        float Beyond(System.Numerics.Vector2 p) => end is { } e ? System.Numerics.Vector2.Dot(p - e, facing) - cut : 0f;
        var points = cave.Chambers.Select(c => c.Center).ToList();
        if (end is { } e0) points.Add(e0);
        float pad = cave.Chambers.Max(c => c.Radius) + 6f;
        int x0 = Math.Max(0, (int)(points.Min(p => p.X) - pad)), x1 = Math.Min(map.Size, (int)(points.Max(p => p.X) + pad));
        int y0 = Math.Max(0, (int)(points.Min(p => p.Y) - pad)), y1 = Math.Min(map.Size, (int)(points.Max(p => p.Y) + pad));
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        bool Under(int x, int y)
        {
            var p = new System.Numerics.Vector2(x, y);
            foreach (var (c, r) in cave.Chambers)
                if (System.Numerics.Vector2.Distance(p, c) < r + 2f) return true;
            for (int i = 1; i < cave.Chambers.Count; i++)
                if (Geo.SegmentDistance(p, cave.Chambers[i - 1].Center, cave.Chambers[i].Center) < 4f) return true;
            return end is { } e && Geo.SegmentDistance(p, cave.Chambers[0].Center, e) < spurHalf && Beyond(p) <= 0f;
        }
        // Just past a shop's face, over its passage: held at the face's height, so the roof runs level out to it.
        bool AtFace(int x, int y)
        {
            if (!shop || end is not { } e) return false;
            var p = new System.Numerics.Vector2(x, y);
            float b = Beyond(p);
            return b > 0f && b < 2.5f && Geo.SegmentDistance(p, cave.Chambers[0].Center, e + facing * 3f) < spurHalf;
        }
        // Its way in stays open (a secret's excepted): no roof over the passage past the chamber's rim.
        bool Passage(int x, int y)
        {
            if (end is not null || spur is null) return false;
            var p = new System.Numerics.Vector2(x, y);
            if (cave.Chambers.Any(c => System.Numerics.Vector2.Distance(p, c.Center) < c.Radius - 0.5f)) return false;
            for (int i = 1; i < spur.Points.Count; i++)
                if (Geo.SegmentDistance(p, spur.Points[i - 1], spur.Points[i]) < spur.HalfWidth + 1f) return true;
            return false;
        }
        // The rock round it: the terrain, and a secret's wall where it stands.
        float Around(int x, int y) => seal is { } sw && !float.IsNaN(map.SealedAt(x, y)) && OfSeal(map, sw, x, y) ? MathF.Max(map[x, y], map.SealedAt(x, y)) : map[x, y];
        var under = new bool[w * h];
        var roof = new float[w * h];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int x = x0 + i, y = y0 + j, k = j * w + i;
            under[k] = i > 0 && j > 0 && i < w - 1 && j < h - 1 && Under(x, y) && !Passage(x, y);
            roof[k] = under[k] ? (float.IsNaN(map.LidAt(x, y)) ? Around(x, y) : map.LidAt(x, y)) : AtFace(x, y) ? face : Around(x, y);
        }
        // Spanned from the heights at its edge (relaxed toward their average, a few hundred passes).
        var span = (float[])roof.Clone();
        for (int pass = 0; pass < 300; pass++)
            for (int j = 1; j < h - 1; j++)
            for (int i = 1; i < w - 1; i++)
            {
                int k = j * w + i;
                if (under[k]) span[k] = 0.25f * (span[k - 1] + span[k + 1] + span[k - w] + span[k + w]);
            }
        for (int k = 0; k < roof.Length; k++)
            if (under[k]) roof[k] = MathF.Max(MathF.Max(roof[k], span[k]), shop ? gate.Crown + 1.2f : LevelMap.SwimBand + 2.5f);
        float Grid(int x, int y)
        {
            int i = x - x0, j = y - y0;
            return i >= 0 && j >= 0 && i < w && j < h && under[j * w + i] ? roof[j * w + i] : Around(x, y);
        }
        float Height(float x, float y) => SmoothSample(Grid, x, y, map.Size);
        return SurfaceMesh(map.Size, (x, y) => Height(x, y) + 0.03f, x0, y0, x1, y1, (x, y) => Height(x, y) > SmoothHeight(map, x, y) + 0.05f);
    }

    /// <summary>
    /// A shortcut tunnel's roof: the rock carried over it (<see cref="LevelMap.Roof"/>), meeting the ground at its edges,
    /// lumped like the rest of the reef's rock by its shader. It fades like any canopy while Clementine is near.
    /// </summary>
    internal static LevelShape.Surface? TunnelRoofSurface(LevelMap map, Corridor corridor)
    {
        float reach = corridor.HalfWidth + 5f;
        int x0 = Math.Max(0, (int)(corridor.Points.Min(p => p.X) - reach)), x1 = Math.Min(map.Size, (int)(corridor.Points.Max(p => p.X) + reach) + 1);
        int y0 = Math.Max(0, (int)(corridor.Points.Min(p => p.Y) - reach)), y1 = Math.Min(map.Size, (int)(corridor.Points.Max(p => p.Y) + reach) + 1);
        bool Along(float x, float y)
        {
            var q = new System.Numerics.Vector2(x, y);
            for (int i = 1; i < corridor.Points.Count; i++)
                if (Geo.SegmentDistance(q, corridor.Points[i - 1], corridor.Points[i]) < reach) return true;
            return false;
        }
        return SurfaceMesh(map.Size, (x, y) => SmoothTunnelRoof(map, x, y) + 0.03f, x0, y0, x1, y1,
            (x, y) => Along(x, y) && SmoothTunnelRoof(map, x, y) > SmoothHeight(map, x, y) + 0.05f);
    }

    /// <summary>A triangle is kept when any of its corners passes <paramref name="keep"/>.</summary>
    /// <param name="field">
    /// Instead of <paramref name="keep"/>: a continuous field, kept where above 0. The surface's outline is then smooth: each
    /// vertex outside it that a kept square holds is slid onto the field's zero line (between it and its neighbours inside),
    /// so the edge runs between the grid's points instead of along them (the cube reef: its void, its arches).
    /// </param>
    internal static LevelShape.Surface? SurfaceMesh(int size, Func<float, float, float> height, int x0, int y0, int x1, int y1, Func<float, float, bool>? keep,
        Func<float, float, float>? field = null)
    {
        if (field is not null) keep = (x, y) => field(x, y) > 0f;
        int w = (x1 - x0) * Sub + 1, h = (y1 - y0) * Sub + 1;
        var verts = new Vector3[w * h];
        var normals = new Vector3[w * h];
        var colors = new Color[w * h];
        const float e = 0.35f;
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            float x = x0 + i / (float)Sub, y = y0 + j / (float)Sub;
            float hgt = height(x, y);
            verts[j * w + i] = new Vector3(x, hgt, y);
            float hl = height(x - e, y), hr = height(x + e, y);
            float hd = height(x, y - e), hu = height(x, y + e);
            normals[j * w + i] = new Vector3(hl - hr, 2f * e, hd - hu).Normalized();
            colors[j * w + i] = Band(hgt, x, y, size);
        }
        var indices = new List<int>((w - 1) * (h - 1) * 6);
        for (int j = 0; j + 1 < h; j++)
        for (int i = 0; i + 1 < w; i++)
        {
            int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
            if (keep is not null && !keep(verts[a].X, verts[a].Z) && !keep(verts[b].X, verts[b].Z) && !keep(verts[c].X, verts[c].Z) && !keep(verts[d].X, verts[d].Z)) continue;
            // Clockwise seen from above (Godot's front face), split along the flatter diagonal: on a steep edge (a wall's
            // top over the abyss) the fixed split leaves a row of teeth along it.
            if (Mathf.Abs(verts[a].Y - verts[d].Y) < Mathf.Abs(verts[b].Y - verts[c].Y)) indices.AddRange(new[] { a, b, d, a, d, c });
            else indices.AddRange(new[] { a, b, c, b, d, c });
        }
        if (indices.Count == 0) return null;
        if (field is not null) SnapOutline(verts, normals, colors, indices, w, h, height, field, size);
        return new LevelShape.Surface(verts, normals, colors, indices.ToArray());
    }

    /// <summary>
    /// A kept surface's outline slid onto its field's zero line: each vertex outside the field that a kept square uses
    /// moves to the mean of the crossings toward its neighbours inside (the nearer side ones, else the diagonal ones), its
    /// height, normal and colour worked out again there.
    /// </summary>
    static void SnapOutline(Vector3[] verts, Vector3[] normals, Color[] colors, List<int> indices, int w, int h,
        Func<float, float, float> height, Func<float, float, float> field, int size)
    {
        var value = new float[verts.Length];
        for (int k = 0; k < verts.Length; k++) value[k] = field(verts[k].X, verts[k].Z);
        var used = new bool[verts.Length];
        foreach (int k in indices) used[k] = true;
        var moved = new Vector3[verts.Length];
        var move = new bool[verts.Length];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
        {
            int k = j * w + i;
            if (!used[k] || value[k] > 0f) continue;
            Vector2 sum = Vector2.Zero;
            int n = 0;
            for (int pass = 0; pass < 2 && n == 0; pass++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (pass == 0 && dx != 0 && dy != 0) continue;
                    if (pass == 1 && (dx == 0 || dy == 0)) continue;
                    int x = i + dx, y = j + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int q = y * w + x;
                    if (value[q] <= 0f) continue;
                    float t = value[k] / (value[k] - value[q]);
                    sum += new Vector2(Mathf.Lerp(verts[k].X, verts[q].X, t), Mathf.Lerp(verts[k].Z, verts[q].Z, t));
                    n++;
                }
            if (n == 0) continue;
            var at = sum / n;
            moved[k] = new Vector3(at.X, height(at.X, at.Y), at.Y);
            move[k] = true;
        }
        const float e = 0.35f;
        for (int k = 0; k < verts.Length; k++)
        {
            if (!move[k]) continue;
            var p = verts[k] = moved[k];
            float hl = height(p.X - e, p.Z), hr = height(p.X + e, p.Z), hd = height(p.X, p.Z - e), hu = height(p.X, p.Z + e);
            normals[k] = new Vector3(hl - hr, 2f * e, hd - hu).Normalized();
            colors[k] = Band(p.Y, p.X, p.Z, size);
        }
    }

    // ───────────────────────── canopy ─────────────────────────

    /// <summary>A shop is never roofed over: its room shows from outside, so it is plain where it lies and how to get in.</summary>
    bool ShopRoof(Canopy c) => _map is { } map && c.Kind == CanopyKind.CaveRoof && CaveOf(c) is int cave and >= 0 && map.Pois[map.Caves[cave].Poi].Kind is PoiKind.Shop or PoiKind.CrabDeal;

    void AddCanopy(Canopy c)
    {
        // A cave's roof is the mountain itself (its lid mesh), not a separate piece; a tunnel's, the rock over it.
        if (c.Kind == CanopyKind.CaveRoof)
        {
            int cave = CaveOf(c);
            if (cave >= 0) _canopies.Add((c, _lids[cave], 1f));
            return;
        }
        if (c.Kind == CanopyKind.TunnelRoof)
        {
            if (_tunnelRoofs.TryGetValue(c.Corridor, out var roof)) _canopies.Add((c, roof, 1f));
            return;
        }
        var material = Rock(0.25f);
        if (c.Radius > 0f)
        {
            // An overhang: a lumpy shelf of rock.
            float thick = c.Top - c.Bottom;
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 28, Rings = 12 },
                Scale = new Vector3(c.Radius * 1.05f, thick * 0.6f, c.Radius * 1.05f),
                Position = new Vector3(c.Center.X, (c.Bottom + c.Top) * 0.5f, c.Center.Y),
                MaterialOverride = material,
            });
        }
        else AddChild(new MeshInstance3D { Mesh = ArchMesh(c), MaterialOverride = material });
        _canopies.Add((c, material, 1f));
    }

    // ───────────────────────── the shop's gateway ─────────────────────────

    /// <summary>Each shop's sign (its golden shells and glow) and the canopy it goes with (its cave's roof): gone while that fades.</summary>
    readonly List<(Canopy Canopy, Node3D Inlay)> _gates = new();
    static ShaderMaterial? _goldShell;
    /// <summary>The gateway: out from the chamber's rim along the passage, springing this high over her plane, this deep and thick.</summary>
    const float GateOut = 1.8f, GateSpring = 0.9f, GateDepth = 1.4f, GateThick = 0.75f;
    /// <summary>The sign's shells: this far apart, side by side.</summary>
    const float SignSpacing = 1.05f;

    /// <summary>A shop's gateway: its middle (map), its half-span (the passage's half-width and a little), its crown's height.</summary>
    static (System.Numerics.Vector2 At, float Half, float Crown) ShopGate(LevelMap map, CaveSite cave)
    {
        var spur = map.Corridors.FirstOrDefault(c => c.Poi == cave.Poi);
        float half = (spur?.HalfWidth ?? 2f) + 0.3f;
        return (cave.Mouth + cave.Facing * GateOut, half, LevelMap.SwimBand + GateSpring + half + GateThick * 0.5f);
    }

    /// <summary>
    /// A shop shows its way in (DESIGN-TOPDOWN §4.2): its roof runs on over its passage as solid rock out to a face, and
    /// in that face its doorway, a round arch of pale dressed stone on two posts from the passage floor; over the arch,
    /// on the face, its sign: three golden shells side by side, a warm glow about them.
    /// </summary>
    void AddShopGates(LevelMap map)
    {
        _gates.Clear();
        foreach (var cave in map.Caves)
        {
            if (map.Pois[cave.Poi].Kind != PoiKind.Shop) continue;
            var (at, half, crown) = ShopGate(map, cave);
            var f = cave.Facing;
            var side2 = new System.Numerics.Vector2(-f.Y, f.X);
            var footA = at - side2 * half;
            var footB = at + side2 * half;
            float floor = Mathf.Min(SmoothHeight(map, footA.X, footA.Y), SmoothHeight(map, footB.X, footB.Y)) - 0.4f;
            float spring = LevelMap.SwimBand + GateSpring;
            var centre = new Vector3(at.X, 0f, at.Y);
            var side = new Vector3(side2.X, 0f, side2.Y);
            var fwd = new Vector3(f.X, 0f, f.Y);
            var material = Rock(0.06f, new Color(1.08f, 0.98f, 0.82f));
            AddChild(new MeshInstance3D { Mesh = GateMesh(centre, side, fwd, half, floor, spring), MaterialOverride = material });

            // Its sign: three golden shells side by side on the face over the arch, the middle one a little higher and
            // larger, their coils turned out of the face and up toward the camera so they read from above.
            _goldShell ??= GoldShell();
            var inlay = new Node3D();
            AddChild(inlay);
            var toEye = new Vector3(0f, Mathf.Sin(Mathf.DegToRad(CameraRig.Tilt)), Mathf.Cos(Mathf.DegToRad(CameraRig.Tilt))).Normalized();
            var faceOut = (toEye + fwd * 0.45f).Normalized();
            for (int k = -1; k <= 1; k++)
            {
                bool middle = k == 0;
                var p = centre + Vector3.Up * (crown + (middle ? 1f : 0.8f)) + fwd * (GateDepth * 0.5f + 0.45f) + side * (k * SignSpacing);
                var shell = new MeshInstance3D { Mesh = OctoShoots.Game.Fx.NautilusShell.Mesh, MaterialOverride = _goldShell, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                var x = side.Rotated(faceOut, k * 0.5f);
                var look = new Basis(x, faceOut, x.Cross(faceOut).Normalized());
                shell.Transform = new Transform3D(look.Scaled(Vector3.One * (middle ? 1.15f : 0.95f)), p);
                // (A gentler shimmer than a loose shell's, so the gold is not washed white.)
                shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("shimmer"), 0.2f);
                shell.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("shimmer_seed"), 0.3f * (k + 1));
                inlay.AddChild(shell);
            }
            inlay.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.78f, 0.4f), LightEnergy = 0.9f, OmniRange = 4.5f, ShadowEnabled = false, Position = centre + Vector3.Up * (crown + 0.8f) + fwd * 1.2f });

            var gate = new Canopy
            {
                Kind = CanopyKind.Arch,
                Center = at,
                Axis = side2,
                HalfLength = half + GateThick,
                HalfWidth = GateDepth * 0.5f + 0.2f,
                Bottom = spring,
                Top = spring + half + GateThick,
            };
            _canopies.Add((gate, material, 1f));
            _gates.Add((gate, inlay));
        }
    }

    // ───────────────────────── cave openings ─────────────────────────

    /// <summary>A cave opening's rock: the roof over its passage, the face its arch is cut in, and the plug that closes it.</summary>
    public sealed record DoorRock(LevelShape.Surface? Roof, LevelShape.Surface? Face, LevelShape.Surface? Plug);

    /// <summary>The opening's arch: its half-span (its jambs stand on the passage's sides) and the height its round top springs from.</summary>
    public const float DoorArchHalf = Doorway.HalfWidth + 0.15f, DoorSpring = Doorway.ArchTop - DoorArchHalf;
    /// <summary>The roof's reach beside the passage, and past its back.</summary>
    const float DoorSide = Doorway.HalfWidth + 1.6f, DoorPast = 1f;

    /// <summary>The arch's underside over the swim level at this far off its middle (only within its half-span).</summary>
    public static float DoorArch(float off) => DoorSpring + Mathf.Sqrt(Mathf.Max(DoorArchHalf * DoorArchHalf - off * off, 0f));

    /// <summary>
    /// A cave opening's rock (docs/DIVE.md §6), worked out in the opening's own frame (in along Into, off
    /// across it): its roof, the rock that stood over the passage (LevelMap.Roof) from its face back; its face, the
    /// rock standing at its threshold from the ground (or the arch) up to the roof's edge, with the round-topped
    /// arch cut in it; and its plug, rock filling the arch, for a mouth shut once its cave is sealed.
    /// </summary>
    internal static DoorRock DoorSurfaces(LevelMap map, Doorway door)
    {
        var into = door.Into;
        var across = door.Across;
        System.Numerics.Vector2 At(float u, float v) => door.At + into * u + across * v;
        float Ground(float u, float v)
        {
            var p = At(u, v);
            return SmoothHeight(map, p.X, p.Y);
        }
        float Top(float u, float v)
        {
            var p = At(u, v);
            return TopAt(map, p.X, p.Y);
        }
        var outward = new Vector3(-into.X, 0f, -into.Y);
        const float step = 0.25f;

        // The roof: a sheet over the passage, kept where there is water under it.
        int nu = (int)((Doorway.Depth + DoorPast) / step) + 1, nv = (int)(2f * DoorSide / step) + 1;
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        const float e = 0.35f;
        for (int j = 0; j < nv; j++)
        for (int i = 0; i < nu; i++)
        {
            float u = i * step, v = -DoorSide + j * step;
            var p = At(u, v);
            float h = Top(u, v) + 0.03f;
            verts.Add(new Vector3(p.X, h, p.Y));
            float hx = TopAt(map, p.X - e, p.Y) - TopAt(map, p.X + e, p.Y), hz = TopAt(map, p.X, p.Y - e) - TopAt(map, p.X, p.Y + e);
            normals.Add(new Vector3(hx, 2f * e, hz).Normalized());
            colors.Add(Band(h, p.X, p.Y, map.Size));
        }
        bool Over(int k) => verts[k].Y > SmoothHeight(map, verts[k].X, verts[k].Z) + 0.08f;
        for (int j = 0; j + 1 < nv; j++)
        for (int i = 0; i + 1 < nu; i++)
        {
            int a = j * nu + i, b = a + 1, c = a + nu, d = c + 1;
            if (!Over(a) && !Over(b) && !Over(c) && !Over(d)) continue;
            Tri(verts, indices, a, b, d, Vector3.Up);
            Tri(verts, indices, a, d, c, Vector3.Up);
        }
        var roof = indices.Count > 0 ? new LevelShape.Surface(verts.ToArray(), normals.ToArray(), colors.ToArray(), indices.ToArray()) : null;

        // The face: columns across the threshold, from the arch (or the ground beside it) up to the roof's edge.
        LevelShape.Surface? Sheet(float u, float half, Func<float, float> bottom, Func<float, float> top)
        {
            var fv = new List<Vector3>();
            var fn = new List<Vector3>();
            var fc = new List<Color>();
            var fi = new List<int>();
            int columns = (int)(2f * half / 0.125f);
            for (int k = 0; k <= columns; k++)
            {
                float v = -half + k * (2f * half / columns);
                var p = At(u, v);
                float lo = bottom(v), hi = Mathf.Max(top(v), lo);
                fv.Add(new Vector3(p.X, lo, p.Y));
                fv.Add(new Vector3(p.X, hi, p.Y));
                fn.Add(outward);
                fn.Add(outward);
                fc.Add(Band(lo, p.X, p.Y, map.Size));
                fc.Add(Band(hi, p.X, p.Y, map.Size));
                if (k == 0) continue;
                int a0 = (k - 1) * 2, a1 = a0 + 1, b0 = k * 2, b1 = b0 + 1;
                if (fv[a1].Y - fv[a0].Y < 0.02f && fv[b1].Y - fv[b0].Y < 0.02f) continue;
                Tri(fv, fi, a0, a1, b1, outward);
                Tri(fv, fi, a0, b1, b0, outward);
            }
            return fi.Count > 0 ? new LevelShape.Surface(fv.ToArray(), fn.ToArray(), fc.ToArray(), fi.ToArray()) : null;
        }
        var face = Sheet(0f, DoorSide, v => Mathf.Abs(v) < DoorArchHalf ? DoorArch(v) : Ground(0f, v) - 0.4f, v => Top(0.01f, v));
        var plug = Sheet(0.3f, DoorArchHalf + 0.05f, v => Ground(0.3f, v) - 0.4f, v => (Mathf.Abs(v) < DoorArchHalf ? DoorArch(v) : DoorSpring) + 0.1f);
        return new DoorRock(roof, face, plug);
    }

    /// <summary>The rock's top at a point: a roof over it where there is one, else the ground.</summary>
    static float TopAt(LevelMap map, float x, float y) => Mathf.Max(SmoothTunnelRoof(map, x, y), SmoothHeight(map, x, y));

    /// <summary>A triangle wound to face <paramref name="toward"/> (Godot's front face: clockwise seen from that side).</summary>
    static void Tri(List<Vector3> verts, List<int> indices, int a, int b, int c, Vector3 toward)
    {
        var n = (verts[b] - verts[a]).Cross(verts[c] - verts[a]);
        if (n.Dot(toward) < 0f) indices.AddRange(new[] { a, b, c });
        else indices.AddRange(new[] { a, c, b });
    }

    /// <summary>Each region mouth's plug, by its index: shown while that mouth is shut.</summary>
    readonly List<MeshInstance3D> _doorPlugs = new();

    /// <summary>
    /// The cave openings' rock: each one's roof and face in the reef's own ground, a rough rim of rock round its arch,
    /// and (a region's mouths) its plug, hidden till it shuts.
    /// </summary>
    void AddDoors(LevelMap map, LevelShape shape)
    {
        _doorPlugs.Clear();
        var doors = map.Mouths.ToList();
        if (map.Way is { } way) doors.Add(way);
        // The ground darkens into each passage (topdown_ground's door_dark).
        var dark = new Vector4[8];
        for (int k = 0; k < doors.Count && k < dark.Length; k++) dark[k] = new Vector4(doors[k].At.X, doors[k].At.Y, doors[k].Into.X, doors[k].Into.Y);
        _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("doors"), dark);
        _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("door_count"), Math.Min(doors.Count, dark.Length));
        _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("door_size"), new Vector3(Doorway.Depth, Doorway.HalfWidth, Doorway.ArchTop));
        for (int k = 0; k < doors.Count && k < shape.Doors.Count; k++) AddDoor(map, doors[k], shape.Doors[k], k);
    }

    /// <summary>
    /// Mouths opened on the map since it was shown (the cube reef's, as she claims cubes), from the <paramref name="from"/>-th
    /// on: each one's rock added as a shown map's are, the rest of the view untouched.
    /// </summary>
    public void AddMouths(LevelMap map, int from)
    {
        var dark = new Vector4[8];
        for (int k = 0; k < map.Mouths.Count && k < dark.Length; k++) dark[k] = new Vector4(map.Mouths[k].At.X, map.Mouths[k].At.Y, map.Mouths[k].Into.X, map.Mouths[k].Into.Y);
        _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("doors"), dark);
        _ground.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("door_count"), Math.Min(map.Mouths.Count, dark.Length));
        for (int k = from; k < map.Mouths.Count; k++) AddDoor(map, map.Mouths[k], DoorSurfaces(map, map.Mouths[k]), k);
    }

    void AddDoor(LevelMap map, Doorway door, DoorRock rock, int k)
    {
        {
            // In the reef's own ground, so the rock over it is one with the wall it is cut in.
            var skin = _ground;
            if (rock.Roof is { } roof) AddChild(new MeshInstance3D { Mesh = ToMesh(roof), MaterialOverride = skin });
            if (rock.Face is { } face) AddChild(new MeshInstance3D { Mesh = ToMesh(face), MaterialOverride = skin });
            var at = door.At + door.Into * 0.1f;
            var side = new Vector3(door.Across.X, 0f, door.Across.Y);
            var fwd = new Vector3(door.Into.X, 0f, door.Into.Y);
            float floor = SmoothHeight(map, at.X, at.Y) - 0.4f;
            // (Its dark cut away whole with the roof over it: under a cut roof the passage is open.)
            var gate = Rock(0.25f);
            gate.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("see_whole"), true);
            AddChild(new MeshInstance3D { Mesh = GateMesh(new Vector3(at.X, 0f, at.Y), side, fwd, DoorArchHalf + 0.1f, floor, DoorSpring), MaterialOverride = gate });
            if (k >= map.Mouths.Count || rock.Plug is not { } plug) return;
            var shut = new MeshInstance3D { Mesh = ToMesh(plug), MaterialOverride = skin, Visible = false };
            AddChild(shut);
            while (_doorPlugs.Count < k) _doorPlugs.Add(null!);
            _doorPlugs.Add(shut);
        }
    }

    /// <summary>A region's shut mouths stand closed with rock (their plugs shown); the open ones stand open.</summary>
    public void ShutDoors(IReadOnlySet<int> shut)
    {
        for (int i = 0; i < _doorPlugs.Count; i++)
            if (_doorPlugs[i] is { } plug) plug.Visible = shut.Contains(i);
    }

    /// <summary>The nautilus coated in gold (its silver and its tarnish warmed): the shells set in a shop's gateway.</summary>
    static ShaderMaterial GoldShell()
    {
        var m = (ShaderMaterial)OctoShoots.Game.Fx.NautilusShell.Material.Duplicate();
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("silver"), new Color(1f, 0.6f, 0.08f));
        m.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("tarnish"), new Color(0.38f, 0.2f, 0.04f));
        return m;
    }

    /// <summary>
    /// The gateway's stone: a band of rounded-square section along two posts up from the floor and a half-circle over the
    /// passage (its centre <paramref name="spring"/> high, radius <paramref name="half"/>), across <paramref name="side"/>.
    /// </summary>
    static ArrayMesh GateMesh(Vector3 centre, Vector3 side, Vector3 fwd, float half, float floor, float spring)
    {
        const int arcSteps = 24, postSteps = 4, around = 16;
        var path = new List<(Vector3 P, Vector3 Out)>();
        for (int i = 0; i <= postSteps; i++)
            path.Add((centre - side * half + Vector3.Up * Mathf.Lerp(floor, spring, i / (float)postSteps), -side));
        for (int i = 1; i < arcSteps; i++)
        {
            float a = Mathf.Pi * (1f - i / (float)arcSteps);
            var radial = side * Mathf.Cos(a) + Vector3.Up * Mathf.Sin(a);
            path.Add((centre + Vector3.Up * spring + radial * half, radial));
        }
        for (int i = postSteps; i >= 0; i--)
            path.Add((centre + side * half + Vector3.Up * Mathf.Lerp(floor, spring, i / (float)postSteps), side));
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        foreach (var (p, outward) in path)
            for (int j = 0; j < around; j++)
            {
                float ang = j * Mathf.Tau / around;
                float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang);
                // A rounded square: dressed stone, its edges eased.
                float sx = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), 0.35f), sy = Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), 0.35f);
                verts.Add(p + outward * (sx * GateThick * 0.5f) + fwd * (sy * GateDepth * 0.5f));
                normals.Add((outward * cx * GateDepth + fwd * cy * GateThick).Normalized());
            }
        for (int i = 0; i + 1 < path.Count; i++)
        {
            // Wound to face out whichever way the path runs (the section turns from out to fwd: against the path, or with it).
            var along = path[i + 1].P - path[i].P;
            bool flip = path[i].Out.Cross(fwd).Dot(along) < 0f;
            for (int j = 0; j < around; j++)
            {
                int j1 = (j + 1) % around;
                int p0 = i * around + j, p1 = i * around + j1, p2 = (i + 1) * around + j, p3 = (i + 1) * around + j1;
                if (flip) indices.AddRange(new[] { p0, p1, p2, p1, p3, p2 });
                else indices.AddRange(new[] { p0, p2, p1, p1, p2, p3 });
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>
    /// An arch: a slab of the reef's own rock bridging the corridor from wall top to wall top, its top running on level
    /// with the rock it springs from (a gentle crown over the span) so the walls' tops carry straight across it in the
    /// same skin, its ends sunk into the walls, thicker at the feet; flat on top with rounded shoulders, rounded beneath.
    /// Its height span (the underside's lowest to its top) is kept for the occlusion rule.
    /// </summary>
    ArrayMesh ArchMesh(Canopy c)
    {
        const int along = 48, around = 20;
        var a = c.EndA;
        var b = c.EndB;
        float topA = SmoothHeight(_map, a.X, a.Y), topB = SmoothHeight(_map, b.X, b.Y);
        var side = new Vector3(-c.Axis.Y, 0f, c.Axis.X);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        float lo = float.MaxValue, hi = float.MinValue;
        // Its line runs on past each footing, diving into the wall.
        const float sink = 0.15f;
        float Top(float t)
        {
            float u = Mathf.Clamp(t, 0f, 1f);
            float y = Mathf.Lerp(topA, topB, Mathf.SmoothStep(0f, 1f, u)) + 0.3f * Mathf.Sin(u * Mathf.Pi);
            // Over the corridor never so low that its underside crowds her plane; at its feet, on the rock it springs from.
            float span = Mathf.SmoothStep(0f, 0.2f, Mathf.Min(u, 1f - u));
            y = Mathf.Max(y, Mathf.Lerp(y, LevelMap.SwimBand + ArchClear + ArchThick, span));
            return y - 20f * Mathf.Max(Mathf.Max(-t, t - 1f), 0f);
        }
        Vector3 Line(float t)
        {
            var p = System.Numerics.Vector2.Lerp(a, b, t);
            return new Vector3(p.X, Top(t), p.Y);
        }
        for (int i = 0; i <= along; i++)
        {
            float t = Mathf.Lerp(-sink, 1f + sink, i / (float)along);
            var p = Line(t);
            var tangent = (Line(t + 0.01f) - Line(t - 0.01f)).Normalized();
            // Its side taken so that tangent × side is up: the rings then wind facing out whichever way it runs.
            var across = tangent.Cross(side).Y < 0f ? -side : side;
            var up = tangent.Cross(across).Normalized();
            float feet = 1f - Mathf.SmoothStep(0f, 0.3f, Mathf.Min(Mathf.Abs(t), Mathf.Abs(1f - t)));
            // A little ragged along it, as rock is.
            float wobble = 1f + 0.1f * Mathf.Sin(t * 9f + c.Center.X) + 0.05f * Mathf.Sin(t * 23f + c.Center.Y);
            float w = c.HalfWidth * (1f + 0.5f * feet) * wobble, thick = ArchThick * (1f + 0.9f * feet);
            // The section hangs from the top: its middle half its thickness down.
            var mid = p - up * (thick * 0.5f);
            float half = thick * 0.5f;
            for (int j = 0; j < around; j++)
            {
                float ang = j * Mathf.Tau / around;
                float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang);
                // Flat on top with rounded shoulders (a squarer superellipse above), rounder beneath.
                float e = cy > 0f ? 0.3f : 0.75f;
                float sx = Mathf.Sign(cx) * Mathf.Pow(Mathf.Abs(cx), e), sy = Mathf.Sign(cy) * Mathf.Pow(Mathf.Abs(cy), e);
                var v = mid + across * (sx * w) + up * (sy * half);
                verts.Add(v);
                normals.Add((across * cx * half + up * cy * w).Normalized());
                if (t >= 0f && t <= 1f)
                {
                    lo = Mathf.Min(lo, v.Y);
                    hi = Mathf.Max(hi, v.Y);
                }
            }
        }
        for (int i = 0; i < along; i++)
        for (int j = 0; j < around; j++)
        {
            int j1 = (j + 1) % around;
            int p0 = i * around + j, p1 = i * around + j1, p2 = (i + 1) * around + j, p3 = (i + 1) * around + j1;
            indices.AddRange(new[] { p0, p2, p1, p1, p2, p3 });
        }
        // Its ends capped (buried in the wall as a rule, but closed where the wall falls away).
        foreach (int i in new[] { 0, along })
        {
            var centre = Vector3.Zero;
            for (int j = 0; j < around; j++) centre += verts[i * around + j];
            centre /= around;
            int ci = verts.Count;
            verts.Add(centre);
            normals.Add((Line(i == 0 ? -sink : 1f + sink) - Line(i == 0 ? 0f : 1f)).Normalized());
            for (int j = 0; j < around; j++)
            {
                int j1 = (j + 1) % around;
                if (i == 0) indices.AddRange(new[] { ci, j, j1 });
                else indices.AddRange(new[] { ci, i * around + j1, i * around + j });
            }
        }
        _spans[c] = (lo, hi);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>An arch's slab: its thickness over the span (more at its feet), and the least room under it above her plane.</summary>
    const float ArchThick = 1.5f, ArchClear = 1.6f;
    /// <summary>The heights a canopy's rock really spans, where its mesh is not the map's Bottom–Top (an arch).</summary>
    readonly Dictionary<Canopy, (float Lo, float Hi)> _spans = new();

    /// <summary>
    /// The occlusion rule (DESIGN-TOPDOWN §3): she is always seen. Any canopy (arch, cave roof, tunnel roof, overhang)
    /// that stands over Clementine, or between her and the camera, dissolves, and returns once it no longer does: her
    /// line of sight to the camera is followed up through each canopy's height, and a canopy within OcclusionMargin of
    /// it (her bell and the strands under it) fades. One she only swims beside stays whole. A cave's roof, and a
    /// tunnel's, fade whole.
    /// </summary>
    public void UpdateCanopy(System.Numerics.Vector2 player, float dt, Vector3 camera)
    {
        var her = new Vector3(player.X, LevelMap.SwimBand, player.Y);
        var sight = (camera - her).Normalized();
        var hiddenCaves = new HashSet<int>();
        var hiddenTunnels = new HashSet<int>();
        var hide = new bool[_canopies.Count];
        for (int i = 0; i < _canopies.Count; i++)
        {
            var c = _canopies[i].Canopy;
            // A sealed secret's roof stays: she cannot be in there, and it must not show there is a chamber under it.
            if (c.Kind == CanopyKind.CaveRoof && _sealedCaves.Contains(CaveOf(c))) continue;
            var (lo, hi) = _spans.TryGetValue(c, out var span) ? span : (c.Bottom, c.Top);
            if (!Blocks(c, lo, hi, her, sight) && !ShopRoof(c)) continue;
            hide[i] = true;
            if (c.Kind == CanopyKind.CaveRoof && CaveOf(c) is int cave and >= 0) hiddenCaves.Add(cave);
            if (c.Kind == CanopyKind.TunnelRoof) hiddenTunnels.Add(c.Corridor);
        }
        for (int i = 0; i < _canopies.Count; i++)
        {
            var (c, material, alpha) = _canopies[i];
            bool gone = hide[i] || (c.Kind == CanopyKind.CaveRoof && hiddenCaves.Contains(CaveOf(c)))
                || (c.Kind == CanopyKind.TunnelRoof && hiddenTunnels.Contains(c.Corridor));
            // Quick to clear the way, gentle to come back; cleared, gone whole (no dithered ghost of it left over her).
            alpha = Mathf.MoveToward(alpha, gone ? Cleared : 1f, dt * (gone ? 7f : 3f));
            // Verification: --dbg-nocanopy keeps every canopy gone.
            if (_noCanopy) alpha = 0f;
            // A dithered dissolve: the rock stays opaque, so the depth focus still blurs it like other high rock.
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), alpha);
            // A shop's gateway: its golden shells and glow go with it.
            foreach (var (gate, inlay) in _gates)
                if (gate == c) inlay.Visible = alpha > 0.5f;
            _canopies[i] = (c, material, alpha);
        }
    }

    /// <summary>How near (metres) her line of sight may pass a canopy's footprint before it fades, and how much of it stays then (none).</summary>
    const float OcclusionMargin = 1f, Cleared = 0f;
    static readonly bool _noCanopy = OS.GetCmdlineUserArgs().Contains("--dbg-nocanopy");

    /// <summary>Whether a canopy stands in (or within OcclusionMargin of) her line of sight to the camera, over its height (bottom–top).</summary>
    static bool Blocks(Canopy c, float bottom, float top, Vector3 her, Vector3 sight)
    {
        float lo = Mathf.Max(bottom - 0.5f, her.Y), hi = top + 0.5f;
        if (sight.Y <= 1e-3f) return Near(c, new System.Numerics.Vector2(her.X, her.Z));
        for (float h = lo; h <= hi + 1e-3f; h += 0.75f)
        {
            var at = her + sight * ((h - her.Y) / sight.Y);
            if (Near(c, new System.Numerics.Vector2(at.X, at.Z))) return true;
        }
        // And straight over her, too.
        return Near(c, new System.Numerics.Vector2(her.X, her.Z));
    }

    static bool Near(Canopy c, System.Numerics.Vector2 p)
    {
        var d = p - c.Center;
        if (c.Radius > 0f) return d.LengthSquared() <= (c.Radius + OcclusionMargin) * (c.Radius + OcclusionMargin);
        float along = System.Numerics.Vector2.Dot(d, c.Axis);
        float across = System.Numerics.Vector2.Dot(d, new System.Numerics.Vector2(-c.Axis.Y, c.Axis.X));
        return MathF.Abs(along) <= c.HalfLength + OcclusionMargin && MathF.Abs(across) <= c.HalfWidth + OcclusionMargin;
    }

    int CaveOf(Canopy roof)
    {
        for (int i = 0; i < _map.Caves.Count; i++)
            if (_map.Caves[i].Chambers.Any(ch => ch.Center == roof.Center)) return i;
        return -1;
    }

    // ───────────────────────── the Crack ─────────────────────────

    /// <summary>
    /// The Crack in the rift's arena: a flat rift flush with the floor, holding the next depth's water (crack_rift.gdshader),
    /// that water's light spilling over the arena floor, and bubbles rising out of it in a slow updraft — a doorway, not a
    /// wound.
    /// </summary>
    void ShowCrack(LevelMap map)
    {
        // A blue hole is plain water; only a boss level's exit is the Crack (a cube reef's arena has none).
        if (!map.HasBoss || map.Shaft.HalfLength <= 0f) return;
        var rift = map.Exit.Position;
        var floor = new Vector3(rift.X, -6f, rift.Y);
        AddChild(_crackLight = new OmniLight3D
        {
            Position = floor + Vector3.Up * 2.5f,
            LightColor = ReefLook.For(map.Depth + 1).WaterDeep,
            LightEnergy = CrackLight,
            OmniRange = 13f,
            OmniAttenuation = 1.6f,
            // Shadowed, so the fissure's lips and whatever stands at the Crack throw shadows up the arena.
            ShadowEnabled = OctoShoots.Game.Settings.ViewOptions.Shadows,
            OmniShadowMode = OmniLight3D.ShadowMode.DualParaboloid,
            ShadowBias = 0.08f,
            ShadowBlur = 2f,
        });
        var bubble = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/plane_bubble.gdshader") };
        AddChild(_crackBubbles = new CpuParticles3D
        {
            Position = floor,
            Amount = 36,
            Lifetime = 5f,
            Preprocess = 5f,
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4, Material = bubble },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(5f, 0.3f, 5f),
            Direction = Vector3.Up,
            Spread = 12f,
            Gravity = new Vector3(0f, 0.6f, 0f),
            InitialVelocityMin = 0.6f,
            InitialVelocityMax = 1.4f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.6f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        ApplyCrack();
    }

    OmniLight3D? _crackLight;
    CpuParticles3D? _crackBubbles;
    MeshInstance3D? _cover, _rift;
    /// <summary>The sand over each of the reef's holes (by index; null for a boss's Crack, which has its own), and its material.</summary>
    readonly List<(MeshInstance3D Sheet, ShaderMaterial Material)?> _holeCovers = new();

    /// <summary>
    /// The reef's holes shut under their leaks (docs/ADVENTURE.md §1): each lies under sand like the floor round it, as
    /// the Crack does, till its leak is sealed and the sand drains away into it (SetHoleOpen). A boss's Crack has its own.
    /// </summary>
    void ShowHoleCovers(LevelMap map)
    {
        _holeCovers.Clear();
        if (map.LeakCave || map.Station) return;
        foreach (var hole in map.Holes)
        {
            var sh = hole.Shaft;
            if (map.HasBoss && (sh.Center - map.Shaft.Center).Length() < 0.5f)
            {
                _holeCovers.Add(null);
                continue;
            }
            var sheet = CoverSheet(map, sh, out float mean);
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/crack_cover.gdshader") };
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("cover_center"), new Vector2(sh.Center.X, sh.Center.Y));
            material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("drop"), mean - LevelMap.ShaftBottom + 1f);
            Track(material);
            var cover = new MeshInstance3D { Mesh = sheet, MaterialOverride = material };
            AddChild(cover);
            _holeCovers.Add((cover, material));
        }
    }

    /// <summary>A hole of the reef opening (0 shut under its sand → 1 open): its sand sags, turns and drains down into it.</summary>
    public void SetHoleOpen(int hole, float t)
    {
        if (hole < 0 || hole >= _holeCovers.Count || _holeCovers[hole] is not { } cover) return;
        cover.Material.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("sink"), Mathf.Clamp(t, 0f, 1f));
        cover.Sheet.Visible = t < 1f;
    }
    ShaderMaterial? _coverMat, _riftMat;
    /// <summary>The light the open Crack's water spills.</summary>
    const float CrackLight = 1.4f;
    const float RiftLift = 0.25f;
    CpuParticles3D? _sandFall;
    /// <summary>How far the Crack has opened (0 sealed under sand → 1 open), and the dive's fade of it (1 → 0).</summary>
    float _crackOpen, _crackFade = 1f;

    /// <summary>The Crack's water, light and bubbles: they come up as the last of the sand drains away.</summary>
    float CrackGlow => Mathf.SmoothStep(0.45f, 1f, _crackOpen) * _crackFade;

    /// <summary>
    /// The dive through the Crack: its water, its light and its rising bubbles fade together (1 full, 0 gone), so
    /// nothing of it is switched off when this level is let go.
    /// </summary>
    public void FadeCrack(float k)
    {
        _crackFade = k;
        ApplyCrack();
    }

    /// <summary>
    /// The Crack opening (0 sealed → 1 open): the sand over it sags, turns and drains down into it, grains pouring
    /// off its rim, and then the Crack's water, light and bubbles come up from below.
    /// </summary>
    public void SetCrackOpen(float t)
    {
        _crackOpen = Mathf.Clamp(t, 0f, 1f);
        ApplyCrack();
    }

    void ApplyCrack()
    {
        if (_map is null || !_map.HasBoss) return;
        float k = CrackGlow;
        _riftMat?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("open"), k);
        if (_rift is not null) _rift.Visible = k > 0f;
        if (_crackLight is not null) _crackLight.LightEnergy = CrackLight * k;
        if (_crackBubbles is not null)
        {
            _crackBubbles.SetInstanceShaderParameter(OctoShoots.Game.Fx.SN.Of("fade"), k);
            _crackBubbles.Emitting = k > 0.01f;
        }
        _coverMat?.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("sink"), _crackOpen);
        if (_cover is not null) _cover.Visible = _crackOpen < 1f;
        if (_sandFall is not null) _sandFall.Emitting = _crackOpen > 0f && _crackOpen < 0.85f;
    }

    /// <summary>
    /// The sand sealing a boss level's Crack: a cap over the shaft (a little past its rim), each rim point at the
    /// ground's own height there and the middle at their average, so it meets the arena floor without a seam. The open
    /// rift is the same sheet: it lies flush with the floor over the shaft, hiding the drop.
    /// </summary>
    /// <summary>
    /// A cap of sand over a shaft (a little past its rim), each rim point at the ground's own height there and the middle
    /// at their average, so it meets the floor without a seam (crack_cover.gdshader drains it away); and that average.
    /// </summary>
    static ArrayMesh CoverSheet(LevelMap map, Shaft sh, out float mean)
    {
        var across = new System.Numerics.Vector2(-sh.Axis.Y, sh.Axis.X);
        const float grow = 0.8f;
        const int rings = 14, segments = 64;
        System.Numerics.Vector2 At(float r, float a, float extra) =>
            sh.Center + sh.Axis * (Mathf.Cos(a) * (sh.HalfLength + grow + extra) * r) + across * (Mathf.Sin(a) * (sh.HalfWidth + grow + extra) * r);
        var rim = new float[segments];
        mean = 0f;
        for (int j = 0; j < segments; j++)
        {
            rim[j] = map.HeightAt(At(1f, j * Mathf.Tau / segments, 0.6f));
            mean += rim[j] / segments;
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        st.SetUV(new Vector2(0f, 0f));
        st.AddVertex(new Vector3(sh.Center.X, mean + 0.03f, sh.Center.Y));
        for (int i = 1; i <= rings; i++)
        for (int j = 0; j < segments; j++)
        {
            float r = i / (float)rings, a = j * Mathf.Tau / segments;
            var p = At(r, a, 0f);
            st.SetNormal(Vector3.Up);
            st.SetUV(new Vector2(r, 0f));
            st.AddVertex(new Vector3(p.X, Mathf.Lerp(mean, rim[j], r * r) + 0.03f, p.Y));
        }
        int Index(int ring, int seg) => ring == 0 ? 0 : 1 + (ring - 1) * segments + (seg % segments);
        for (int j = 0; j < segments; j++)
        {
            // Clockwise seen from above (Godot's front face).
            st.AddIndex(0);
            st.AddIndex(Index(1, j));
            st.AddIndex(Index(1, j + 1));
        }
        for (int i = 1; i < rings; i++)
        for (int j = 0; j < segments; j++)
        {
            int a0 = Index(i, j), a1 = Index(i, j + 1), b0 = Index(i + 1, j), b1 = Index(i + 1, j + 1);
            st.AddIndex(a0);
            st.AddIndex(b0);
            st.AddIndex(a1);
            st.AddIndex(a1);
            st.AddIndex(b0);
            st.AddIndex(b1);
        }
        return st.Commit();
    }

    void ShowCover(LevelMap map)
    {
        var sh = map.Shaft;
        var sheet = CoverSheet(map, sh, out float mean);
        _coverMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/crack_cover.gdshader") };
        _coverMat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("cover_center"), new Vector2(sh.Center.X, sh.Center.Y));
        _coverMat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("drop"), mean - LevelMap.ShaftBottom + 1f);
        Track(_coverMat);
        AddChild(_cover = new MeshInstance3D { Mesh = sheet, MaterialOverride = _coverMat });
        _riftMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/crack_rift.gdshader") };
        _riftMat.SetShaderParameter(OctoShoots.Game.Fx.SN.Of("water"), ReefLook.For(map.Depth + 1).WaterDeep);
        Track(_riftMat);
        // A little over the floor, so its soft edge melts over the ground rather than being cut by it.
        AddChild(_rift = new MeshInstance3D { Mesh = sheet, MaterialOverride = _riftMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false, Position = Vector3.Up * RiftLift });

        // Grains pouring off the cover's rim into the dark as it drains.
        var grain = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.78f, 0.7f, 0.52f),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
        };
        AddChild(_sandFall = new CpuParticles3D
        {
            Position = new Vector3(sh.Center.X, mean + 0.2f, sh.Center.Y),
            Emitting = false,
            Amount = 220,
            Lifetime = 1.6f,
            Mesh = new QuadMesh { Size = new Vector2(0.07f, 0.07f), Material = grain },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = Mathf.Max(sh.HalfLength, sh.HalfWidth) * 0.8f,
            Direction = Vector3.Down,
            Spread = 25f,
            Gravity = new Vector3(0f, -7f, 0f),
            InitialVelocityMin = 0.2f,
            InitialVelocityMax = 1.2f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.6f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    // ───────────────────────── places ─────────────────────────

    /// <summary>The place's name, as the HUD shows it when Clementine enters.</summary>
    public static string PlaceName(PoiKind kind) => kind switch
    {
        PoiKind.ShellCache => "Shell cache",
        PoiKind.TreasureCave => "Clam room",
        PoiKind.CurseDen => "Curse den",
        PoiKind.CrabDeal => "Crab's Deal",
        PoiKind.Exit or PoiKind.Hole => "Blue hole",
        _ => kind.ToString(),
    };

    /// <summary>A place's name on this level: a boss level's exit is the Crack; the open reef's exits and starts have their own.</summary>
    public static string PlaceName(LevelMap map, PoiKind kind) => kind switch
    {
        PoiKind.Exit when map.Station => "Current station",
        PoiKind.Exit when map.LeakCave && map.HasBoss => "The arena",
        PoiKind.Exit when map.LeakCave => "The leak",
        PoiKind.Exit when map.HasBoss => "The Crack",
        PoiKind.Start when map.LeakCave => "Cave mouth",
        PoiKind.Start when map.Haven is not null => "Lantern post",
        _ => PlaceName(kind),
    };

    // ───────────────────────── decoration ─────────────────────────

    List<FloraItem> _floraItems = new();

    /// <summary>
    /// No corals or grass within this far of these spots (map coordinates): a flounder's patch is open sand, and the
    /// scatter (drawn here, unseen by the sim) may have put a coral head on it.
    /// </summary>
    public void KeepClear(IEnumerable<System.Numerics.Vector2> spots, float radius)
    {
        var clear = spots.ToList();
        bool Near(FloraItem f) => clear.Any(c => new Vector2(f.Transform.Origin.X - c.X, f.Transform.Origin.Z - c.Y).LengthSquared() < radius * radius);
        if (clear.Count == 0 || !_floraItems.Any(Near)) return;
        _floraItems = _floraItems.Where(f => !Near(f)).ToList();
        _flora.Show(_floraItems);
    }

    /// <summary>A cube reef's life added as a cube is laid on her layer's map (LevelView.ReefLife).</summary>
    public void AddLife(List<FloraItem> flora, List<Transform3D> rocks, List<Transform3D> glows)
    {
        if (flora.Count > 0)
        {
            _floraItems.AddRange(flora);
            _flora.Show(_floraItems);
        }
        if (rocks.Count > 0) AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 18, Rings = 9 }, Rock(0.22f), rocks));
        if (glows.Count > 0)
            AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 },
                new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.4f, 1f, 0.9f), EmissionEnabled = true, Emission = new Color(0.4f, 1f, 0.9f), EmissionEnergyMultiplier = 2f }, glows, shadow: false));
    }

    /// <summary>Her map changed round her (a cube laid on it): the cutout's map of it made again at once.</summary>
    public void SeeMaskStale()
    {
        _seeHeightsAt = new(float.MaxValue, float.MaxValue);
        _seeKeepAt = new(float.MaxValue, float.MaxValue);
    }

    void ShowDecor(LevelShape shape)
    {
        PaintFlora(ReefLook.For(shape.Map.Depth));
        _floraItems = shape.Flora.ToList();
        _flora.Show(shape.Flora);
        AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 18, Rings = 9 }, Rock(0.22f), shape.Rocks));
        if (shape.Glows.Count > 0)
            AddChild(Instances(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 8, Rings = 4 },
                new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.4f, 1f, 0.9f), EmissionEnabled = true, Emission = new Color(0.4f, 1f, 0.9f), EmissionEnergyMultiplier = 2f }, shape.Glows, shadow: false));
    }

    /// <summary>Maps the generator's decoration onto the existing flora art. Positions, yaw and scale all come from the map.</summary>
    internal static void DecorItems(LevelMap map, List<FloraItem> items, List<Transform3D> rocks, List<Transform3D> glows, Rect2? area = null)
    {
        foreach (var d in map.Decor)
        {
            if (area is { } a && !a.HasPoint(new Vector2(d.Position.X, d.Position.Y))) continue;
            var pos = new Vector3(d.Position.X, d.Height, d.Position.Y);
            var basis = new Basis(Vector3.Up, d.Yaw).Scaled(Vector3.One * d.Scale);
            // Only the seagrass meadows and the brain corals (the sponge-like rocks): the rest of the dressing drew the eye
            // away from what matters (DESIGN-TOPDOWN §4.1).
            FloraKind? kind = d.Kind switch
            {
                DecorKind.GrassMeadow => FloraKind.TurtleGrass,
                DecorKind.Bommie => FloraKind.BrainCoral,
                _ => null,
            };
            if (kind is { } k)
            {
                // Seen from 30 m up, reef life is drawn a little larger than life, so it reads.
                basis = basis.Scaled(Vector3.One * (d.Kind switch
                {
                    DecorKind.Bommie => 2.2f,
                    DecorKind.GrassMeadow => 1.6f,
                    DecorKind.SeaFan or DecorKind.CrownGarden => 1.45f,
                    DecorKind.EncrustingCoral => 1.35f,
                    _ => 1.3f,
                }));
                // Variant (palette a or b) and a little brightness jitter, from the spot's own yaw and scale.
                float variant = Mathf.PosMod(d.Yaw * 3.7f, 1f) < 0.42f ? 1f : 0f;
                float bright = 0.88f + 0.24f * Mathf.PosMod(d.Scale * 13.1f, 1f);
                items.Add(new FloraItem(k, new Transform3D(basis, pos), new Color(variant, bright, 1f, d.Yaw / Mathf.Tau)));
            }
        }
        Scatter(map, items, rocks, area);
        if (ReefLook.Painted) Gardens(map, items, area);
    }

    /// <summary>
    /// A scatter's lattice over the map (inside its rim), or over the part of it in <paramref name="area"/>: its first
    /// and last rows and columns, on the same lattice points either way.
    /// </summary>
    static (float Y0, float Y1, float X0, float X1) Span(LevelMap map, float step, Rect2? area)
    {
        float lo = LevelMap.RimWidth + 1f, hi = map.Size - LevelMap.RimWidth - 1f;
        if (area is not { } a) return (lo, hi, lo, hi);
        float From(float v) => lo + MathF.Max(0f, MathF.Ceiling((v - lo) / step)) * step;
        return (From(a.Position.Y), MathF.Min(hi, a.End.Y), From(a.Position.X), MathF.Min(hi, a.End.X));
    }

    /// <summary>
    /// The game's look (docs/VISUAL-SHOWCASE.md): coral gardens, as in a reef seen from above — clusters of sea rods, tube
    /// sponges, staghorn, sea fans and brain corals packed in patches on the walls' tops and ledges, and in a band along
    /// their feet at the edge of the open floor; the open sand and her plane's way left clear. For the look only.
    /// </summary>
    static void Gardens(LevelMap map, List<FloraItem> items, Rect2? area = null)
    {
        uint seed = unchecked((uint)(map.Seed ^ (map.Seed >> 32)) ^ (uint)(map.Level * 6151 + map.Depth * 98317 + map.Cycle * 3079) ^ 0x6A4D3Bu);
        float Hash(float x, float y, int k)
        {
            uint h = unchecked((uint)((int)(x * 10f) * 73856093) ^ (uint)((int)(y * 10f) * 19349663) ^ (uint)(k * 83492791) ^ seed);
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
        FloraKind[] top = { FloraKind.SeaRod, FloraKind.TubeSponge, FloraKind.Staghorn, FloraKind.SeaFan, FloraKind.BrainCoral, FloraKind.SeaRod, FloraKind.Elkhorn };
        FloraKind[] foot = { FloraKind.TubeSponge, FloraKind.BrainCoral, FloraKind.SeaRod, FloraKind.Staghorn, FloraKind.BarrelSponge };
        const float step = 1.05f;
        var (ys, ye, xs, xe) = Span(map, step, area);
        for (float y = ys; y < ye; y += step)
        for (float x = xs; x < xe; x += step)
        {
            var p = new System.Numerics.Vector2(x + Hash(x, y, 1) * step, y + Hash(x, y, 2) * step);
            if (map.InShaft(p, 3f)) continue;
            float h = map.HeightAt(p);
            float slope = map.Gradient(p).Length();
            // Patches: the gardens grow in drifts, thick in their middles, none between.
            float patch = TerrainNoise.Fbm(p.X * 0.06f, p.Y * 0.06f, seed, 3);
            float r = Hash(x, y, 3);
            FloraKind kind;
            float scale;
            if (h > 0.6f && slope < 1.3f)
            {
                // A wall's top or a ledge.
                if (r > 0.8f * Mathf.SmoothStep(-0.3f, 0.15f, patch)) continue;
                kind = top[(int)(Hash(x, y, 4) * top.Length) % top.Length];
                scale = 1.8f + 1.6f * Hash(x, y, 5);
            }
            else if (h < -0.4f && h > -6f && slope < 0.9f)
            {
                // The open floor: only a band along the walls' feet.
                bool foots = false;
                for (int i = 0; i < 6 && !foots; i++)
                {
                    float a = i * Mathf.Tau / 6f;
                    foots = map.HeightAt(p + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.8f) > 0.2f;
                }
                if (!foots || r > 0.55f * Mathf.SmoothStep(-0.2f, 0.25f, patch)) continue;
                kind = foot[(int)(Hash(x, y, 4) * foot.Length) % foot.Length];
                scale = 1.1f + 1.1f * Hash(x, y, 5);
            }
            else continue;
            var at = new Vector3(p.X, h - 0.08f, p.Y);
            var basis = new Basis(Vector3.Up, Hash(x, y, 6) * Mathf.Tau).Scaled(Vector3.One * scale);
            var tint = new Color(Hash(x, y, 7) < 0.5f ? 1f : 0f, 0.9f + 0.25f * Hash(x, y, 8), 1f, Hash(x, y, 9));
            items.Add(new FloraItem(kind, new Transform3D(basis, at), tint));
        }
    }

    /// <summary>
    /// Ground cover, for the look only (nothing in play depends on it): a deterministic scatter from the level's seed so
    /// the seabed reads as a living reef rather than a beach (THEME-BIBLE §7, Depth 1: healthy neighbours everywhere),
    /// kept sparse so it never draws the eye from play: seagrass meadows in drifting patches, a few small brain corals at
    /// the feet of the walls and a lone coral head now and then out on the open sand. The exit is left clear round the
    /// shaft.
    /// </summary>
    static void Scatter(LevelMap map, List<FloraItem> items, List<Transform3D> rocks, Rect2? area = null)
    {
        uint seed = unchecked((uint)(map.Seed ^ (map.Seed >> 32)) ^ (uint)(map.Level * 7919 + map.Depth * 104729 + map.Cycle * 15485863));
        float Hash(float x, float y, int k)
        {
            uint h = unchecked((uint)((int)(x * 10f) * 73856093) ^ (uint)((int)(y * 10f) * 19349663) ^ (uint)(k * 83492791) ^ seed);
            h ^= h >> 13;
            h = unchecked(h * 0x5bd1e995u);
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
        var rift = map.Exit;
        const float step = 1.1f;
        var (ys, ye, xs, xe) = Span(map, step, area);
        for (float y = ys; y < ye; y += step)
        for (float x = xs; x < xe; x += step)
        {
            var p = new System.Numerics.Vector2(x + Hash(x, y, 1) * step, y + Hash(x, y, 2) * step);
            float h = map.HeightAt(p);
            // The seabed of the shallows only: not the walls, not the deeps.
            if (h > -0.6f || h < -6f) continue;
            float slope = map.Gradient(p).Length();
            if (slope > 0.9f) continue;
            if (map.HasBoss ? System.Numerics.Vector2.Distance(p, rift.Position) < rift.Radius * 0.6f || map.InShaft(p, 1.5f) : map.InShaft(p, 1.5f)) continue;
            bool nearWall = false;
            for (int i = 0; i < 6 && !nearWall; i++)
            {
                float a = i * Mathf.Tau / 6f;
                nearWall = map.HeightAt(p + new System.Numerics.Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f) > 0.2f;
            }
            float meadow = TerrainNoise.Fbm(p.X * 0.045f, p.Y * 0.045f, seed, 3);
            float r = Hash(x, y, 3), yaw = Hash(x, y, 4) * Mathf.Tau, size = Hash(x, y, 5);
            var at = new Vector3(p.X, h - 0.05f, p.Y);
            var tint = new Color(Hash(x, y, 6) < 0.42f ? 1f : 0f, 0.86f + 0.26f * Hash(x, y, 7), 1f, Hash(x, y, 8));
            FloraItem Item(FloraKind kind, float scale) => new(kind, new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), at), tint);
            if (nearWall && r < 0.06f)
                // The reef's foot: a few small brain corals.
                items.Add(Item(FloraKind.BrainCoral, 0.55f + 0.4f * size));
            else if (meadow > 0.1f && slope < 0.35f && r < 0.85f * Mathf.SmoothStep(0.1f, 0.35f, meadow))
                items.Add(Item(FloraKind.TurtleGrass, 1.9f + 1.1f * size));
            else if (slope < 0.3f && r < 0.012f)
                items.Add(Item(FloraKind.BrainCoral, 1.1f + 0.9f * size));
        }
    }

    /// <summary>The depth's healthy reef palette on the flora (THEME-BIBLE §6.3); soft things sway like water.</summary>
    void PaintFlora(ReefLook look)
    {
        static Color Dark(Color c, float k = 0.42f) => new(c.R * k, c.G * k, c.B * k);
        _flora.SetPalette(FloraKind.TurtleGrass, Dark(look.Grass, 0.4f), look.Grass, Dark(look.Grass, 0.45f), look.Grass.Lerp(look.Butter, 0.35f), sway: 0.16f);
        _flora.SetPalette(FloraKind.SeaFan, Dark(look.FanA), look.FanA, Dark(look.FanB), look.FanB, sway: 0.09f);
        _flora.SetPalette(FloraKind.SeaRod, Dark(look.Coral2), look.Coral2.Lerp(look.FanB, 0.3f), Dark(look.SoftCoral), look.SoftCoral, sway: 0.1f);
        _flora.SetPalette(FloraKind.TubeSponge, Dark(look.Sponge, 0.35f), look.Sponge, Dark(look.Coral2, 0.35f), look.Coral2);
        _flora.SetPalette(FloraKind.Staghorn, Dark(look.SoftCoral, 0.5f), look.SoftCoral, Dark(look.HardCoral, 0.5f), look.HardCoral);
        _flora.SetPalette(FloraKind.Elkhorn, Dark(look.HardCoral, 0.5f), look.HardCoral, Dark(look.SoftCoral, 0.5f), look.SoftCoral);
        _flora.SetPalette(FloraKind.BrainCoral, Dark(look.HardCoral, 0.5f), look.HardCoral, Dark(look.SoftCoral, 0.5f), look.SoftCoral.Lerp(look.HardCoral, 0.45f));
        _flora.SetPalette(FloraKind.BarrelSponge, Dark(look.Coral, 0.35f), look.Coral.Lerp(look.Rock, 0.4f), Dark(look.Coral2, 0.35f), look.Coral2.Lerp(look.Rock, 0.4f));
    }

    /// <summary>One mesh many times; it casts shadows (the boulders) unless it is light itself (the glows).</summary>
    static MultiMeshInstance3D Instances(Mesh mesh, Material material, List<Transform3D> transforms, bool shadow = true)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) mm.SetInstanceTransform(i, transforms[i]);
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
    }
}
