using Godot;

namespace OctoShoots.Game.Fx;

/// <summary>
/// The loader on a loading screen: her crosshair (GonadCursor) turning at the bottom right of the screen (or in its middle,
/// <see cref="Centered"/>), at the crosshair's own size whatever the window. Its parent must not be scaled.
/// </summary>
public partial class GonadLoader : TextureRect
{
    const float Margin = 32f, TurnSeconds = 1.4f;

    public bool Centered { get; init; }

    public override void _Ready()
    {
        Texture = GonadCursor.Texture();
        MouseFilter = MouseFilterEnum.Ignore;
        var size = Texture.GetSize();
        if (Centered)
        {
            AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 0.5f;
            OffsetLeft = -size.X * 0.5f;
            OffsetTop = -size.Y * 0.5f;
            OffsetRight = size.X * 0.5f;
            OffsetBottom = size.Y * 0.5f;
        }
        else
        {
            AnchorLeft = AnchorRight = AnchorTop = AnchorBottom = 1f;
            OffsetLeft = -Margin - size.X;
            OffsetTop = -Margin - size.Y;
            OffsetRight = OffsetBottom = -Margin;
        }
        PivotOffset = size * 0.5f;
    }

    public override void _Process(double delta) =>
        Rotation = Mathf.Wrap(Rotation + Mathf.Tau * (float)delta / TurnSeconds, 0f, Mathf.Tau);
}
