using Godot;

[Tool]
public partial class PuzzleGateSpawn : PuzzleSpawnBase
{
    [Export] public string GateId { get; set; } = "";
    [Export] public bool StartsClosed { get; set; } = true;

    public bool IsOpen { get; private set; }
    public bool BlocksMovement => StartsClosed && !IsOpen;

    protected override string GroupName => "PuzzleGateSpawn";
    protected override Color FallbackColor => IsOpen ? Colors.SeaGreen : Colors.DarkSlateGray;

    public override void _Ready()
    {
        base._Ready();
        ApplySolvedState(!StartsClosed);
    }

    public void ApplySolvedState(bool solved)
    {
        IsOpen = solved || !StartsClosed;
        QueueRedraw();
    }
}
