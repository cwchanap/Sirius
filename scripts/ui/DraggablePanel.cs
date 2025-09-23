using Godot;

public partial class DraggablePanel : PanelContainer
{
    private bool _dragging = false;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        // Show a move cursor when hovering the panel
        MouseDefaultCursorShape = CursorShape.Move;
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Start/stop drag with left mouse button
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _dragging = true;
                _dragOffset = GetGlobalMousePosition() - GlobalPosition;
                AcceptEvent();
            }
            else
            {
                _dragging = false;
                AcceptEvent();
            }
        }

        // While dragging, move the panel and clamp within the viewport
        if (_dragging && @event is InputEventMouseMotion)
        {
            Vector2 target = GetGlobalMousePosition() - _dragOffset;
            GlobalPosition = ClampToViewport(target, Size);
            AcceptEvent();
        }
    }

    private Vector2 ClampToViewport(Vector2 target, Vector2 size)
    {
        var vp = GetViewportRect().Size;
        float x = Mathf.Clamp(target.X, 0, Mathf.Max(0, vp.X - size.X));
        float y = Mathf.Clamp(target.Y, 0, Mathf.Max(0, vp.Y - size.Y));
        return new Vector2(x, y);
    }
}
