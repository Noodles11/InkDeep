using Godot;
using OctoShoots.Core.Items;

namespace OctoShoots.Game.TopDown;

/// <summary>
/// A pearl, told before she takes it, wherever it lies (in a clam room, on a shop's or the crab's stand, or loose on the
/// reef): as she nears it the absorbing banner's card (PearlBanner), smaller, floats up over it with what the pearl does,
/// bobbing a little, and fades as she leaves or begins to absorb it.
/// </summary>
public partial class PearlHint : Control
{
    /// <summary>How near the pearl she must be (metres).</summary>
    public const float Reach = 6f;
    const float Size_ = 0.72f, Width = 560f, Margin = 16f, Rise = 14f, InTime = 0.2f, OutTime = 0.3f, Bob = 3f;

    PanelContainer _card = null!;
    VBoxContainer _body = null!;
    string? _id;
    float _shown, _time;
    Vector2 _at;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _card = PearlBanner.Card(Width);
        _card.Scale = Vector2.One * Size_;
        AddChild(_card);
        _body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _card.AddChild(_body);
    }

    /// <summary>Each frame: the pearl near her (null: none) and where over its shell the card floats, on the screen.</summary>
    public void Step(float dt, ItemDef? item, Vector2 over)
    {
        _time += dt;
        if (item is not null)
        {
            _at = over;
            if (item.Id != _id) Show(item);
        }
        _shown = Mathf.MoveToward(_shown, item is not null ? 1f : 0f, dt / (item is not null ? InTime : OutTime));
        if (_shown <= 0f)
        {
            _id = null;
            _card.Visible = false;
            return;
        }
        float ease = 1f - (1f - _shown) * (1f - _shown);
        _card.Modulate = new Color(1f, 1f, 1f, ease);
        _card.Size = new Vector2(Width, 0f);
        _card.ResetSize();
        var size = _card.Size * Size_;
        var place = _at - new Vector2(size.X * 0.5f, size.Y) + Vector2.Up * (Rise * ease + Bob * Mathf.Sin(_time * 2f));
        _card.Position = new Vector2(Mathf.Clamp(place.X, Margin, Mathf.Max(Size.X - size.X - Margin, Margin)), Mathf.Clamp(place.Y, Margin, Mathf.Max(Size.Y - size.Y - Margin, Margin)));
    }

    void Show(ItemDef item)
    {
        _id = item.Id;
        _shown = 0f;
        foreach (var child in _body.GetChildren()) child.QueueFree();
        _body.AddChild(PauseMenu.Row(item));
        _card.Visible = true;
    }
}
