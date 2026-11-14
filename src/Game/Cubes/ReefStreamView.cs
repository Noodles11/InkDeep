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

    ShaderMaterial _here = null!, _below = null!;
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

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/topdown_ground.gdshader");
        _here = new ShaderMaterial { Shader = shader };
        _below = new ShaderMaterial { Shader = shader };
        Place();
    }

    /// <summary>The layer she is on (the one drawn at the swim plane), and the dive's lift.</summary>
    public void SetLayer(int layer, float lift = 0f)
    {
        if (layer == _layer && Mathf.IsEqualApprox(lift, _lift)) return;
        _layer = layer;
        _lift = lift;
        Place();
    }

    /// <summary>What stands between the camera and her (an arch) cut open round her (level_frame's see_through).</summary>
    public void SeeThrough(Vector3 her, float radius, float open)
    {
        _here.SetShaderParameter(SN.Of("see_through"), new Vector4(her.X, her.Z, radius, open));
        _here.SetShaderParameter(SN.Of("see_through_y"), her.Y);
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
        _here.SetShaderParameter(SN.Of("level_offset"), new Vector3(0f, _lift, 0f));
        _below.SetShaderParameter(SN.Of("level_offset"), new Vector3(0f, _lift - LayerDrop, 0f));
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
        bool fullShown = full is { Count: > 0 } && rel is 0 or 1;
        var material = rel == 0 ? _here : _below;
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
