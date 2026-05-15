using Godot;

[Tool]
public partial class TrapTileSpawn : PuzzleSpawnBase
{
    [Export] public int Damage { get; set; } = 12;
    [Export] public string StatusEffectId { get; set; } = "";
    [Export] public int StatusMagnitude { get; set; }
    [Export] public int StatusTurns { get; set; }

    protected override string GroupName => "TrapTileSpawn";
    protected override Color FallbackColor => Colors.OrangeRed;
}
