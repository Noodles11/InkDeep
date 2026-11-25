using System.Collections.Generic;
using Godot;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Game.Fx;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// Treasures on screen (docs/proposals/CROSSOVER.md §5.2): real old things. The Pearl Altar is a worn block of pale
/// stone with three shallow hollows; the Spare Bell a ship's bronze bell gone dull green; the Shell Urn a sealed clay
/// urn. One lying hidden on its level glints now and then with a small warm light, so she can find it (never brighter
/// than her); taken, it goes in a burst of her light. In the start cave they simply stand where she begins.
/// </summary>
public partial class RelicView : Node3D
{
    sealed class Shown
    {
        public PlaneRelic Sim = null!;
        public Node3D Root = null!;
        public OmniLight3D? Glint;
        public float Phase;
        public bool Gone;
    }

    readonly List<Shown> _shown = new();
    float _time;

    static readonly StandardMaterial3D Stone = new() { AlbedoColor = new Color(0.66f, 0.64f, 0.6f), Roughness = 0.95f };
    static readonly StandardMaterial3D StoneDark = new() { AlbedoColor = new Color(0.32f, 0.31f, 0.3f), Roughness = 1f };
    static readonly StandardMaterial3D Bronze = new() { AlbedoColor = new Color(0.42f, 0.47f, 0.4f), Metallic = 0.6f, Roughness = 0.6f };
    static readonly StandardMaterial3D Clay = new() { AlbedoColor = new Color(0.55f, 0.4f, 0.3f), Roughness = 0.9f };

    /// <summary>A new level's treasures.</summary>
    public void Show(PlaneWorld world)
    {
        foreach (var s in _shown) s.Root.QueueFree();
        _shown.Clear();
        foreach (var relic in world.Relics) Add(relic);
    }

    void Add(PlaneRelic relic)
    {
        {
            var root = new Node3D { Position = new Vector3(relic.Position.X, 0f, relic.Position.Y), Rotation = new Vector3(0f, (float)GD.RandRange(0.0, Mathf.Tau), 0f) };
            // (The bell and the urn lie on their side, the urn spilled: from above that reads as what they are.)
            root.AddChild(Model(relic.Id, lying: true));
            OmniLight3D? glint = null;
            if (!relic.InStartCave)
            {
                glint = new OmniLight3D { LightColor = HerColor.Light, LightEnergy = 0f, OmniRange = 3.5f, Position = new Vector3(0f, LevelMap.SwimBand - 0.8f, 0f), ShadowEnabled = false };
                root.AddChild(glint);
            }
            AddChild(root);
            _shown.Add(new Shown { Sim = relic, Root = root, Glint = glint, Phase = (float)GD.RandRange(0.0, 5.0) });
        }
    }

    /// <summary>The treasure's object on the floor: the altar standing, the urn and the bell lying on their side.</summary>
    static Node3D Model(string id, bool lying)
    {
        var node = new Node3D();
        const float floor = -1f;
        float top = LevelMap.SwimBand - 0.6f;
        switch (id)
        {
            case Treasures.PearlAltar:
            {
                // A squat worn block, a little wider at its foot, three hollows in its top.
                float h = top - floor;
                node.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.8f, BottomRadius = 0.95f, Height = h, RadialSegments = 7, Rings = 2 }, MaterialOverride = Stone, Position = new Vector3(0f, floor + h * 0.5f, 0f) });
                for (int k = 0; k < 3; k++)
                {
                    float a = Mathf.Tau * k / 3f;
                    node.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.17f, Height = 0.04f, RadialSegments = 12, Rings = 1 }, MaterialOverride = StoneDark, Position = new Vector3(Mathf.Cos(a) * 0.42f, top + 0.005f, Mathf.Sin(a) * 0.42f) });
                }
                break;
            }
            case Treasures.SpareBell:
            {
                var bell = new Node3D { Position = new Vector3(0f, lying ? floor + 0.5f : floor + 0.15f, 0f), Rotation = lying ? new Vector3(0f, 0f, Mathf.DegToRad(100f)) : Vector3.Zero };
                bell.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.5f, Height = 0.8f, RadialSegments = 20, Rings = 3 }, MaterialOverride = Bronze, Position = new Vector3(0f, 0.4f, 0f) });
                bell.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.46f, OuterRadius = 0.56f, Rings = 20, RingSegments = 8 }, MaterialOverride = Bronze, Position = new Vector3(0f, 0.02f, 0f) });
                bell.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.3f, Height = 0.36f, RadialSegments = 16, Rings = 8 }, MaterialOverride = Bronze, Position = new Vector3(0f, 0.8f, 0f) });
                bell.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.06f, OuterRadius = 0.12f, Rings = 12, RingSegments = 6 }, MaterialOverride = Bronze, Position = new Vector3(0f, 1f, 0f), Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f) });
                node.AddChild(bell);
                break;
            }
            case Treasures.OilLamp:
            {
                // A bronze oil lamp lying on the floor: its round flat body, its spout out to one side, a ring of a handle
                // on the other, the hole for its oil in its top.
                var lamp = new Node3D { Position = new Vector3(0f, floor + 0.2f, 0f) };
                lamp.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.42f, Height = 0.36f, RadialSegments = 20, Rings = 8 }, MaterialOverride = Bronze });
                lamp.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.14f, Height = 0.55f, RadialSegments = 10, Rings = 1 }, MaterialOverride = Bronze, Position = new Vector3(0.5f, 0.02f, 0f), Rotation = new Vector3(0f, 0f, Mathf.Pi / 2f) });
                lamp.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.07f, OuterRadius = 0.13f, Rings = 12, RingSegments = 6 }, MaterialOverride = Bronze, Position = new Vector3(-0.48f, 0.05f, 0f), Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f) });
                lamp.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.03f, RadialSegments = 10, Rings = 1 }, MaterialOverride = StoneDark, Position = new Vector3(0f, 0.18f, 0f) });
                node.AddChild(lamp);
                break;
            }
            case Treasures.ShellUrn:
            {
                var urn = new Node3D { Position = new Vector3(0f, lying ? floor + 0.45f : floor, 0f), Rotation = lying ? new Vector3(0f, 0f, Mathf.DegToRad(80f)) : Vector3.Zero };
                urn.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 1.3f, RadialSegments = 18, Rings = 10 }, MaterialOverride = Clay, Position = new Vector3(0f, 0.65f, 0f) });
                urn.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.16f, Height = 0.35f, RadialSegments = 14, Rings = 1 }, MaterialOverride = Clay, Position = new Vector3(0f, 1.38f, 0f) });
                // Its seal: a plug of dark wax.
                urn.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.18f, Height = 0.06f, RadialSegments = 12, Rings = 1 }, MaterialOverride = StoneDark, Position = new Vector3(0f, 1.57f, 0f) });
                node.AddChild(urn);
                break;
            }
        }
        return node;
    }

    /// <summary>Each frame: the hidden ones glint; one she has taken goes.</summary>
    public void Sync(PlaneWorld world, float dt, CombatView combat)
    {
        _time += dt;
        // (An open level's relics come in with their chunks, and keep their place as its window moves on.)
        while (_shown.Count < world.Relics.Count) Add(world.Relics[_shown.Count]);
        foreach (var s in _shown)
        {
            s.Root.Position = new Vector3(s.Sim.Position.X, s.Root.Position.Y, s.Sim.Position.Y);
            if (s.Gone) continue;
            if (!s.Sim.InStartCave && s.Sim.Taken)
            {
                s.Gone = true;
                s.Root.Visible = false;
                combat.LightBurst(s.Sim.Position, 2.5f);
                continue;
            }
            if (s.Glint is { } glint)
            {
                // A glint every few seconds, quick to rise and slow to fade.
                float t = (_time + s.Phase) % 3.2f;
                float k = t < 0.25f ? t / 0.25f : Mathf.Max(0f, 1f - (t - 0.25f) / 1.2f);
                glint.LightEnergy = 0.25f + 1.1f * k * k;
            }
        }
    }
}
