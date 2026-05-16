using Godot;

[Tool]
public partial class TrapTileSpawn : PuzzleSpawnBase
{
    [Export] public int Damage { get; set; } = 12;
    [Export] public string StatusEffectId { get; set; } = "";
    [Export] public int StatusMagnitude { get; set; }
    [Export] public int StatusTurns { get; set; }

    /// <summary>
    /// Stable identifier for JSON round-trip. Falls back to node name when empty.
    /// </summary>
    [Export] public string TrapId { get; set; } = "";

    protected override string GroupName => "TrapTileSpawn";
    protected override Color FallbackColor => Colors.OrangeRed;
}
