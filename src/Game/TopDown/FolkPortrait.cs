using Godot;
using OctoShoots.Core.Bosses;

namespace OctoShoots.Game.TopDown;

/// <summary>A neighbour's portrait in her speech bubble (FolkTalk): the cut-in's drawing of her, freed, at ease.</summary>
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
        // Past its cut-in's gag: at ease, in her true colours.
        BossIntroArt.Boss(this, Spec, Size.Y * 0.32f, 5f, 0f, BossIntroArt.Mood.Calm, _t, freed: true);
    }
}
