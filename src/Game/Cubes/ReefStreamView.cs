using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using OctoShoots.Core.Gen.Cubes;
using OctoShoots.Game.Fx;
using OctoShoots.Game.TopDown;
using static OctoShoots.Core.Gen.Cubes.CubeTuning;

namespace OctoShoots.Game.Cubes;

/// <summary>
/// The stream's cubes on screen (CUBE-WORLD-PLAN §5.2, §5.5): it follows <see cref="ReefStream.Changes"/>, showing each
/// cube's meshes (made on the worker) a few a frame, on nodes taken from and returned to a pool. Her layer is drawn at the swim plane in the reef's own skin; the layer under it LayerDrop below,
/// its simplified ground (or its full one, once a dive is coming).
/// </summary>
public partial class ReefStreamView : Node3D
{
    /// <summary>Meshes shown a frame at most (each made already, on the worker), and the time they may take (ms; one at least).</summary>
    public const int MostUploads = 8;
    public const double UploadBudget = 1.5;

    ShaderMaterial _here = null!, _below = null!, _above = null!;

    /// <summary>Her layer's ground material (what stands as her layer's rock, a cracked seal's slab, is drawn with it: risen, cut and shaded alike).</summary>
    public ShaderMaterial Here => _here;
    bool _showAbove;
    readonly Dictionary<(CubeId Cube, CubeWork Work), List<MeshInstance3D>> _shown = new();
    readonly LinkedList<(CubeId Cube, CubeWork Work, ArrayMesh Mesh)> _pending = new();
    readonly Stack<MeshInstance3D> _pool = new();
    int _layer;
    /// <summary>How far the layers are drawn off their own heights (a dive under way: the world sinking past her).</summary>
    float _lift;

    /// <summary>Meshes waiting to be uploaded; nodes in use and in the pool; the last frame's upload cost (ms) and the worst.</summary>
    public int Pending => _pending.Count;
    public int InUse { get; private set; }
    public int Pooled => _pool.Count;
    public double LastMs { get; private set; }
    public double WorstMs { get; set; }

    /// <summary>
    /// The murk under the layer below (CUBE-WORLD-PLAN §5.5): one flat dark floor at that layer's own void, following her,
    /// so nothing under it is ever drawn or needed.
    /// </summary>
    MeshInstance3D _murk = null!;
    public const float MurkSize = 400f;

    /// <summary>
    /// The pall (DIVE §1): every square round her that shows none of her layer's cubes (one not made yet, one behind a
    /// closed edge, anything past the window) filled as the cut rock's inside is (the cap, LevelView.Cap: near-black,
    /// deep past its hatching) at the cap's own height, so the reef never ends in a cut edge with the water or the layer
    /// below showing past it. Laid again as her square changes or her layer's cubes come and go.
    /// </summary>
    MeshInstance3D _pall = null!;
    bool _pallStale = true;
    CellCoord _pallAt = new(int.MinValue, int.MinValue, 0);
    /// <summary>The squares either side of hers the pall is laid square by square over; past them, out to PallReach metres, it is whole.</summary>
    public const int PallSquares = 6;
    public const float PallReach = 2000f, PallHeight = LevelView.CapHeight;

    /// <summary>The pall laid round her square: each square of the block round it with no cube of her layer shown, and all past the block.</summary>
    void LayPall(CellCoord at)
    {
        _pallAt = at;
        _pallStale = false;
        var shown = new HashSet<CellCoord>();
        foreach (var ((cube, work), list) in _shown)
            if (work == CubeWork.Mesh && cube.Z == _layer && list.Count > 0)
            {
                shown.Add(cube.First with { Z = 0 });
                shown.Add(cube.Second with { Z = 0 });
            }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        // (The cap's, its whole depth into the rock: no ink.)
        st.SetColor(new Color(1f, 0f, 0f, 0.5f));
        void Quad(float x0, float z0, float x1, float z1)
        {
            var a = new Vector3(x0, PallHeight, z0);
            var b = new Vector3(x1, PallHeight, z0);
            var c = new Vector3(x1, PallHeight, z1);
            var d = new Vector3(x0, PallHeight, z1);
            st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
            st.AddVertex(a); st.AddVertex(c); st.AddVertex(d);
        }
        const float side = CubeTuning.Side;
        float x0 = (at.X - PallSquares) * side, y0 = (at.Y - PallSquares) * side;
        float x1 = (at.X + PallSquares + 1) * side, y1 = (at.Y + PallSquares + 1) * side;
        Quad(x0 - PallReach, y0 - PallReach, x1 + PallReach, y0);
        Quad(x0 - PallReach, y1, x1 + PallReach, y1 + PallReach);
        Quad(x0 - PallReach, y0, x0, y1);
        Quad(x1, y0, x1 + PallReach, y1);
        for (int y = at.Y - PallSquares; y <= at.Y + PallSquares; y++)
        for (int x = at.X - PallSquares; x <= at.X + PallSquares; x++)
            if (!shown.Contains(new CellCoord(x, y, 0))) Quad(x * side, y * side, (x + 1) * side, (y + 1) * side);
        _pall.Mesh = st.Commit();
    }

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/topdown_ground.gdshader");
        _here = new ShaderMaterial { Shader = shader };
        _below = new ShaderMaterial { Shader = shader };
        _above = new ShaderMaterial { Shader = shader };
        foreach (var m in new[] { _here, _below, _above }) InkLook.ApplyGround(m);
        SetRise(1f, 0f, 0f);
        _murk = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(MurkSize, MurkSize) },
            MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.02f, 0.035f, 0.06f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_murk);
        _pall = new MeshInstance3D { MaterialOverride = _here, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_pall);
        Place();
    }

    /// <summary>
    /// Where her layer's map lies in the reef (LevelMap.ReefOrigin): the cubes, made in the reef's own metres, are drawn
    /// that much back, in the game's metres; their patterns read where they truly lie (level_origin), so nothing in them
    /// moves when the frame does (a change of layer onto a map centred elsewhere).
    /// </summary>
    public void SetFrame(System.Numerics.Vector2 frame)
    {
        if (frame == _frame) return;
        _frame = frame;
        Position = new Vector3(-frame.X, 0f, -frame.Y);
        foreach (var m in new[] { _here, _below, _above }) m.SetShaderParameter(SN.Of("level_origin"), new Vector2(frame.X, frame.Y));
        _pallStale = true;
    }

    System.Numerics.Vector2 _frame;

    /// <summary>The murk kept under her (her point in the game's metres).</summary>
    public void Follow(Vector3 her)
    {
        her += new Vector3(_frame.X, 0f, _frame.Y);
        _murk.Position = new Vector3(her.X, _lift - LayerDrop + VoidHeight + 0.5f, her.Z);
        var at = CellCoord.Of(new System.Numerics.Vector2(her.X, her.Z), 0);
        if (_pallStale || at != _pallAt) LayPall(at);
    }

    /// <summary>The layer she is on (the one drawn at the swim plane), and the dive's lift.</summary>
    /// <summary>
    /// How far her layer's walls rise above their own rock, metres: past the camera's own height (CameraRig.Distance at
    /// its tilt, about 48 m), so no wall's top is ever seen: every wall runs on out past the screen's edge, one tall
    /// structure, until the cutout takes it.
    /// </summary>
    public const float Rise = 60f;

    /// <summary>
    /// The walls' rise on each layer drawn (0–1 of Rise): hers in full; the one she goes to growing as she nears it, the
    /// one she leaves as it was. None on a layer seen through the void.
    /// </summary>
    public void SetRise(float here, float below, float above)
    {
        HereRise = Rise * here;
        _here.SetShaderParameter(SN.Of("rise"), Rise * here);
        _below.SetShaderParameter(SN.Of("rise"), Rise * below);
        _above.SetShaderParameter(SN.Of("rise"), Rise * above);
    }

    /// <summary>How far her layer's walls rise (m).</summary>
    public float HereRise { get; private set; }

    /// <summary>The dive's lift of her layer (its ground drawn that much up).</summary>
    public float Lift => _lift;

    /// <summary>Sealed sockets' rock (CubeShapes.Plug), drawn on her layer as its ground is, by who laid them.</summary>
    readonly Dictionary<object, List<MeshInstance3D>> _plugs = new();

    public void ShowPlug(object key, CubeShape shape)
    {
        HidePlug(key);
        var list = new List<MeshInstance3D>();
        foreach (var mesh in shape.Meshes)
        {
            var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = _here, Position = new Vector3(0f, _lift, 0f) };
            AddChild(m);
            list.Add(m);
        }
        _plugs[key] = list;
    }

    public void HidePlug(object key)
    {
        if (!_plugs.Remove(key, out var list)) return;
        foreach (var m in list) m.QueueFree();
    }

    public void SetLayer(int layer, float lift = 0f)
    {
        if (layer == _layer && Mathf.IsEqualApprox(lift, _lift)) return;
        _layer = layer;
        _lift = lift;
        Place();
    }

    /// <summary>The layer above hers drawn (only while she climbs to it), or not.</summary>
    public void ShowAbove(bool on)
    {
        if (on == _showAbove) return;
        _showAbove = on;
        Place();
    }

    /// <summary>
    /// The iris of a change of layer (level_frame's portal): her layer cut away round <paramref name="at"/> out to
    /// <paramref name="radius"/> as she dives through it; the layer above drawn only inside it as she climbs into it.
    /// Radius below 0: none.
    /// </summary>
    public void SetPortal(Vector2 at, float radius, bool climbing)
    {
        var off = new Vector4(0f, 0f, 0f, 0f);
        _here.SetShaderParameter(SN.Of("portal"), radius < 0f || climbing ? off : new Vector4(at.X, at.Y, radius, 1f));
        _above.SetShaderParameter(SN.Of("portal"), radius < 0f || !climbing ? off : new Vector4(at.X, at.Y, radius, 2f));
    }

    /// <summary>What stands on the camera's side of her cut away (level_frame's see_through).</summary>
    public void SeeThrough(Vector3 her, float radius, float open, (Texture2D Cut, Vector3 Box)? mask = null)
    {
        // (The rock behind her line cut with the wall in front: LevelView.SeeCut.)
        if (mask is { } m)
        {
            _here.SetShaderParameter(SN.Of("see_cut"), m.Cut);
            _here.SetShaderParameter(SN.Of("see_cut_box"), m.Box);
        }
        _here.SetShaderParameter(SN.Of("see_through"), new Vector4(her.X, her.Z, radius, open));
        // (Cut at her layer's swim plane, where its caps lie, never her bobbing.)
        _here.SetShaderParameter(SN.Of("see_through_y"), _lift + OctoShoots.Core.Gen.TopDown.LevelMap.SwimBand);
    }

    /// <summary>The stream's changes taken: shapes queued for upload, what was let go returned to the pool.</summary>
    public void Take(ReefStream stream)
    {
        foreach (var change in stream.Changes)
        {
            if (change.Work is not (CubeWork.Mesh or CubeWork.PreviewMesh)) continue;
            var key = (change.Cube, change.Work);
            if (change.Added)
            {
                if (change.Result is not CubeShape shape) continue;
                foreach (var mesh in shape.Meshes) _pending.AddLast((change.Cube, change.Work, mesh));
                continue;
            }
            for (var node = _pending.First; node is not null;)
            {
                var next = node.Next;
                if (node.Value.Cube == change.Cube && node.Value.Work == change.Work) _pending.Remove(node);
                node = next;
            }
            if (!_shown.Remove(key, out var meshes)) continue;
            foreach (var m in meshes)
            {
                m.Visible = false;
                m.Mesh = null;
                _pool.Push(m);
                InUse--;
            }
            // (Its simplified ground shows again if its full one went first.)
            Show(change.Cube);
        }
        stream.Changes.Clear();
    }

    /// <summary>A few of the waiting meshes uploaded and shown: her layer's first.</summary>
    public void Apply()
    {
        long start = Stopwatch.GetTimestamp();
        for (int n = 0; n < MostUploads && _pending.Count > 0; n++)
        {
            if (n > 0 && (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency > UploadBudget) break;
            var pick = _pending.First!;
            for (var node = _pending.First; node is not null; node = node.Next)
                if (node.Value.Cube.Z == _layer)
                {
                    pick = node;
                    break;
                }
            _pending.Remove(pick);
            var (cube, work, mesh) = pick.Value;
            var instance = _pool.Count > 0 ? _pool.Pop() : NewNode();
            instance.Mesh = mesh;
            InUse++;
            var key = (cube, work);
            if (!_shown.TryGetValue(key, out var list)) _shown[key] = list = new List<MeshInstance3D>();
            list.Add(instance);
            Show(cube);
        }
        LastMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        if (LastMs > WorstMs) WorstMs = LastMs;
    }

    MeshInstance3D NewNode()
    {
        var m = new MeshInstance3D { Visible = false };
        AddChild(m);
        return m;
    }

    void Place()
    {
        foreach (var list in _plugs.Values)
            foreach (var m in list) m.Position = new Vector3(0f, _lift, 0f);
        _pall.Position = new Vector3(0f, _lift, 0f);
        _pallStale = true;
        _here.SetShaderParameter(SN.Of("level_offset"), new Vector3(0f, _lift, 0f));
        _below.SetShaderParameter(SN.Of("level_offset"), new Vector3(0f, _lift - LayerDrop, 0f));
        _above.SetShaderParameter(SN.Of("level_offset"), new Vector3(0f, _lift + LayerDrop, 0f));
        var seen = new HashSet<CubeId>();
        foreach (var key in _shown.Keys)
            if (seen.Add(key.Cube)) Show(key.Cube);
    }

    /// <summary>A cube's meshes shown as its layer stands to hers: its full ground on her layer; under it, its full ground if it has one, else its simplified one.</summary>
    void Show(CubeId cube)
    {
        _shown.TryGetValue((cube, CubeWork.Mesh), out var full);
        _shown.TryGetValue((cube, CubeWork.PreviewMesh), out var simple);
        int rel = cube.Z - _layer;
        if (rel == 0) _pallStale = true;
        bool fullShown = full is { Count: > 0 } && (rel is 0 or 1 || rel == -1 && _showAbove);
        var material = rel == 0 ? _here : rel < 0 ? _above : _below;
        float y = _lift - rel * LayerDrop;
        if (full is not null)
            foreach (var m in full)
            {
                m.Visible = fullShown;
                m.MaterialOverride = material;
                m.Position = new Vector3(0f, y, 0f);
                m.CastShadow = rel == 0 ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            }
        if (simple is not null)
            foreach (var m in simple)
            {
                m.Visible = rel == 1 && !fullShown;
                m.MaterialOverride = _below;
                m.Position = new Vector3(0f, y, 0f);
                m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
    }
}
