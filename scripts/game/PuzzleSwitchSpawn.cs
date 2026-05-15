using Godot;

[Tool]
public partial class PuzzleSwitchSpawn : PuzzleSpawnBase
{
    [Export] public string SwitchId { get; set; } = "";
    [Export] public string PromptText { get; set; } = "Use";
    [Export] public string ActivatedText { get; set; } = "The mechanism wakes.";

    protected override string GroupName => "PuzzleSwitchSpawn";
    protected override Color FallbackColor => Colors.DeepSkyBlue;
}
