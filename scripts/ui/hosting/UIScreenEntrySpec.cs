using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;

public sealed record UIScreenEntrySpec
{
    public required StringName Kind { get; init; }
    public required UIScreenLayer Layer { get; init; }
    public required UIInputPriority InputPriority { get; init; }
    public required UIProcessPolicy ProcessPolicy { get; init; }
    public UIScreenHandle? Parent { get; init; }
    public StringName? ExclusiveGroup { get; init; }
    public IReadOnlySet<StringName>? IncompatibleKinds { get; init; }
    public bool PauseTree { get; init; }
    public bool BlockGameplayInput { get; init; }
    public UICursorPolicy Cursor { get; init; }
    public UIHudPolicy Hud { get; init; }
    public UILowerLayerPolicy LowerLayers { get; init; }
    public UICancelPolicy Cancel { get; init; }
    public IReadOnlySet<StringName>? EntryCancelActions { get; init; }
    public Func<Control?>? InitialFocus { get; init; }
    public Func<Control?>? RestoreFocus { get; init; }
    public Func<UIInputContext, UIInputInterception>? InterceptCancel { get; init; }
    public Func<bool>? IsPresented { get; init; }
    public Action<bool>? SetPresented { get; init; }
    public Action<bool>? SetInteractive { get; init; }
    public Func<Viewport>? FocusViewport { get; init; }
    public Action<UIScreenCloseReason>? Cleanup { get; init; }
    public UINodeLifetime NodeLifetime { get; init; }

    internal UIScreenSpecNormalizationResult Normalize()
    {
        if (Kind == default || string.IsNullOrEmpty(Kind.ToString()))
            return new(UIScreenOpenStatus.InvalidSpecification, null);

        var incompatibleKinds = NormalizeSet(IncompatibleKinds);
        var entryCancelActions = NormalizeSet(EntryCancelActions);
        var exclusiveGroup = NormalizeGroup(ExclusiveGroup);

        if (InputPriority == UIInputPriority.Passive &&
            (PauseTree || BlockGameplayInput || Cancel != UICancelPolicy.None ||
             entryCancelActions.Count != 0 ||
             LowerLayers != UILowerLayerPolicy.VisibleInteractive ||
             InitialFocus != null || RestoreFocus != null || InterceptCancel != null))
        {
            return new(UIScreenOpenStatus.InvalidSpecification, null);
        }

        return new(
            UIScreenOpenStatus.Opened,
            new UIScreenEntryPolicy
            {
                Kind = Kind,
                Layer = Layer,
                InputPriority = InputPriority,
                ProcessPolicy = ProcessPolicy,
                Parent = Parent,
                ExclusiveGroup = exclusiveGroup,
                IncompatibleKinds = incompatibleKinds,
                PauseTree = PauseTree,
                BlockGameplayInput = BlockGameplayInput,
                Cursor = Cursor,
                Hud = Hud,
                LowerLayers = LowerLayers,
                Cancel = Cancel,
                EntryCancelActions = entryCancelActions
            });
    }

    private static StringName NormalizeGroup(StringName? value)
    {
        var text = value?.ToString();
        return string.IsNullOrEmpty(text)
            ? UIScreenExclusiveGroups.None
            : new StringName(text);
    }

    private static IReadOnlySet<StringName> NormalizeSet(IReadOnlySet<StringName>? values) =>
        values is null || values.Count == 0
            ? EmptyStringNameSet.Value
            : values.ToFrozenSet();
}

internal readonly record struct UIScreenSpecNormalizationResult(
    UIScreenOpenStatus Status,
    UIScreenEntryPolicy? Policy);
