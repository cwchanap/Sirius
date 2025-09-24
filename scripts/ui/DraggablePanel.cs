using Godot;

public partial class DraggablePanel : PanelContainer
{
    [Export] public bool DraggableEnabled { get; set; } = true;
    [Export] public bool OnlyDragFromHandle { get; set; } = false;
    [Export] public NodePath DragHandlePath { get; set; }
    [Export] public bool EnableEdgeSnap { get; set; } = true;
    [Export] public float SnapThreshold { get; set; } = 24.0f;

    private bool _dragging = false;
    private Vector2 _dragOffset;
    private const string ConfigPath = "user://hud.cfg";
    private const string ConfigSection = "HUD";
    private const string ConfigKeyX = "TopPanelPosX";
    private const string ConfigKeyY = "TopPanelPosY";

    public override void _Ready()
    {
        // Show a move cursor when hovering the panel
        MouseDefaultCursorShape = CursorShape.Move;

        // Optional: Wire up a CheckButton named "LockDrag" if present
        var lockToggle = GetNodeOrNull<BaseButton>("LockDrag");
        if (lockToggle == null)
        {
            // Try to search in descendants to be resilient to layout
            lockToggle = FindChild("LockDrag", true, false) as BaseButton;
        }
        if (lockToggle is CheckButton cb)
        {
            cb.ButtonPressed = !DraggableEnabled;
            cb.Toggled += pressed =>
            {
                DraggableEnabled = !pressed;
                MouseDefaultCursorShape = DraggableEnabled ? CursorShape.Move : CursorShape.Arrow;
            };
        }

        // Load persisted position
        LoadPosition();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!DraggableEnabled)
            return;

        // Start/stop drag with left mouse button
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                // If configured, only allow drag when starting in the handle area
                if (OnlyDragFromHandle && !IsPointerInHandle())
                {
                    return;
                }
                _dragging = true;
                _dragOffset = GetGlobalMousePosition() - GlobalPosition;
                AcceptEvent();
            }
            else
            {
                _dragging = false;
                if (EnableEdgeSnap)
                {
                    GlobalPosition = SnapToEdges(GlobalPosition, Size, SnapThreshold);
                }
                SavePosition();
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

    private Vector2 SnapToEdges(Vector2 pos, Vector2 size, float threshold)
    {
        var vp = GetViewportRect().Size;
        float x = pos.X;
        float y = pos.Y;

        if (x <= threshold)
            x = 0;
        else if (x >= vp.X - size.X - threshold)
            x = Mathf.Max(0, vp.X - size.X);

        if (y <= threshold)
            y = 0;
        else if (y >= vp.Y - size.Y - threshold)
            y = Mathf.Max(0, vp.Y - size.Y);

        return new Vector2(x, y);
    }

    private bool IsPointerInHandle()
    {
        if (DragHandlePath == null || DragHandlePath.IsEmpty)
            return true; // No handle specified => allow anywhere

        var handle = GetNodeOrNull<Control>(DragHandlePath);
        if (handle == null)
            return true;

        var rect = new Rect2(handle.GlobalPosition, handle.Size);
        return rect.HasPoint(GetGlobalMousePosition());
    }

    private void SavePosition()
    {
        var cfg = new ConfigFile();
        cfg.Load(ConfigPath);
        cfg.SetValue(ConfigSection, ConfigKeyX, GlobalPosition.X);
        cfg.SetValue(ConfigSection, ConfigKeyY, GlobalPosition.Y);
        cfg.Save(ConfigPath);
    }

    private void LoadPosition()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(ConfigPath) == Error.Ok)
        {
            if (cfg.HasSectionKey(ConfigSection, ConfigKeyX) && cfg.HasSectionKey(ConfigSection, ConfigKeyY))
            {
                float x = (float)(double)cfg.GetValue(ConfigSection, ConfigKeyX, 20.0);
                float y = (float)(double)cfg.GetValue(ConfigSection, ConfigKeyY, 80.0);
                var clamped = ClampToViewport(new Vector2(x, y), Size);
                GlobalPosition = clamped;
            }
        }
    }
}
