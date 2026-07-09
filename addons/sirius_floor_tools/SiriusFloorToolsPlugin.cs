using Godot;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsPlugin : EditorPlugin
{
    private SiriusFloorToolsDock _dock;

    public override void _EnterTree()
    {
        var dockScene = GD.Load<PackedScene>("res://addons/sirius_floor_tools/SiriusFloorToolsDock.tscn");
        if (dockScene == null)
        {
            GD.PrintErr("[SiriusFloorTools] Dock scene not found");
            return;
        }
        _dock = dockScene.Instantiate<SiriusFloorToolsDock>();
        if (_dock == null)
        {
            GD.PrintErr("[SiriusFloorTools] Failed to instantiate dock; plugin C# scripts may not be compiled");
            return;
        }
        AddControlToDock(DockSlot.LeftUl, _dock);
    }

    public override void _ExitTree()
    {
        if (_dock != null)
        {
            RemoveControlFromDocks(_dock);
            _dock.Free();
            _dock = null;
        }
    }
}
