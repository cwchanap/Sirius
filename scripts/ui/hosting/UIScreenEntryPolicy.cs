using System.Collections.Generic;
using Godot;

public sealed record UIScreenEntryPolicy
{
    public required StringName Kind { get; init; }
    public required UIScreenLayer Layer { get; init; }
    public required UIInputPriority InputPriority { get; init; }
    public required UIProcessPolicy ProcessPolicy { get; init; }
    public UIScreenHandle? Parent { get; init; }
    public required StringName ExclusiveGroup { get; init; }
    public required IReadOnlySet<StringName> IncompatibleKinds { get; init; }
    public bool PauseTree { get; init; }
    public bool BlockGameplayInput { get; init; }
    public UICursorPolicy Cursor { get; init; }
    public UIHudPolicy Hud { get; init; }
    public UILowerLayerPolicy LowerLayers { get; init; }
    public UICancelPolicy Cancel { get; init; }
    public required IReadOnlySet<StringName> EntryCancelActions { get; init; }
}
