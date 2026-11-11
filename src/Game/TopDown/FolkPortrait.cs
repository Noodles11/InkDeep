using Godot;
using OctoShoots.Core.Bosses;

namespace OctoShoots.Game.TopDown;

/// <summary>A neighbour's portrait in her speech bubble (FolkTalk): her drawing (BossIntroArt), freed, at ease.</summary>
public partial class FolkPortrait : Control
{
    public BossSpec? Spec;
    float _t;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Spec is null) return;
        DrawSetTransform(Size * 0.5f + new Vector2(-6f, 6f), 0f, Vector2.One);
        // At ease, in her true colours.
        BossIntroArt.Boss(this, Spec, Size.Y * 0.32f, 5f, 0f, BossIntroArt.Mood.Calm, _t, freed: true);
    }
}
