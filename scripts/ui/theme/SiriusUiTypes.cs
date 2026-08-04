using Godot;
using System;

public enum SiriusUiSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public enum SiriusModalSizeClass
{
    Small,
    Medium,
    Large
}

public enum SiriusStatBarKind
{
    Health,
    Mana,
    Experience
}

public static class SiriusUiTypeMappings
{
    public static UiIconId ToIconId(this SiriusUiSeverity severity) => severity switch
    {
        SiriusUiSeverity.Info => UiIconId.Info,
        SiriusUiSeverity.Success => UiIconId.Confirm,
        SiriusUiSeverity.Warning => UiIconId.Warning,
        SiriusUiSeverity.Error => UiIconId.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };

    public static StringName ToModalPanelThemeType(this SiriusUiSeverity severity) => severity switch
    {
        SiriusUiSeverity.Info or SiriusUiSeverity.Success => SiriusThemeTypes.ModalPanel,
        SiriusUiSeverity.Warning => SiriusThemeTypes.WarningPanel,
        SiriusUiSeverity.Error => SiriusThemeTypes.ErrorPanel,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };

    public static StringName ToToastPanelThemeType(this SiriusUiSeverity severity) => severity switch
    {
        SiriusUiSeverity.Info => SiriusThemeTypes.ContentPanel,
        SiriusUiSeverity.Success => SiriusThemeTypes.FeaturePanel,
        SiriusUiSeverity.Warning => SiriusThemeTypes.WarningPanel,
        SiriusUiSeverity.Error => SiriusThemeTypes.ErrorPanel,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };

    public static StringName ToThemeType(this SiriusStatBarKind kind) => kind switch
    {
        SiriusStatBarKind.Health => SiriusThemeTypes.HpBar,
        SiriusStatBarKind.Mana => SiriusThemeTypes.MpBar,
        SiriusStatBarKind.Experience => SiriusThemeTypes.ExpBar,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static UiIconId ToIconId(this SiriusStatBarKind kind) => kind switch
    {
        SiriusStatBarKind.Health => UiIconId.Health,
        SiriusStatBarKind.Mana => UiIconId.Mana,
        SiriusStatBarKind.Experience => UiIconId.Experience,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
