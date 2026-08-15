using Godot;

public static class UIScreenKinds
{
    public static readonly StringName Pause = "pause";
    public static readonly StringName Settings = "settings";
    public static readonly StringName Inventory = "inventory";
    public static readonly StringName SaveLoad = "save_load";
    public static readonly StringName Dialogue = "dialogue";
    public static readonly StringName Shop = "shop";
    public static readonly StringName Heal = "heal";
    public static readonly StringName PuzzleRiddle = "puzzle_riddle";
    public static readonly StringName Battle = "battle";
    public static readonly StringName RewardToast = "reward_toast";
    public static readonly StringName RewardAcknowledgement = "reward_acknowledgement";
    public static readonly StringName Transition = "transition";
    public static readonly StringName Prompt = "prompt";
}

public static class UIScreenExclusiveGroups
{
    public static readonly StringName None = "";
    public static readonly StringName BlockingPrompt = "blocking_prompt";
}
