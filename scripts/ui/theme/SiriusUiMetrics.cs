using Godot;
using System;

public static class SiriusUiMetrics
{
    public const float MaximumContentWidth = 1600f;

    public static readonly Vector2I[] VerificationViewports =
    {
        new(640, 360),
        new(1024, 768),
        new(1280, 720),
        new(1440, 900),
        new(1920, 1080),
        new(2560, 1080),
        new(2560, 1440)
    };

    public static readonly Vector2I[] FocusVerificationViewports =
    {
        new(640, 360),
        new(1280, 720)
    };

    public static bool IsCompact(Vector2 safeFrameSize) =>
        safeFrameSize.X < 800 || safeFrameSize.Y < 450;

    public static int SafeMargin(bool compact) => compact ? 12 : 24;

    public static (bool Compact, float Margin, float SideInset)
        SafeFrameInsets(Vector2 viewportSize)
    {
        var compact = IsCompact(viewportSize);
        var margin = SafeMargin(compact);
        var availableWidth = MathF.Max(0f, viewportSize.X - margin * 2f);
        var contentWidth = MathF.Min(availableWidth, MaximumContentWidth);
        var sideInset = MathF.Max(
            margin,
            (viewportSize.X - contentWidth) / 2f);
        return (compact, margin, sideInset);
    }

    public static Vector2 MinimumTarget(bool compact) =>
        compact ? new Vector2(40, 40) : new Vector2(44, 44);

    public static Vector2 IgnitionSize(bool compact) =>
        compact ? new Vector2(80, 80) : new Vector2(96, 96);

    public static int TooltipMaximum(bool compact) => compact ? 280 : 360;

    public static int ModalWidth(SiriusModalSizeClass sizeClass) => sizeClass switch
    {
        SiriusModalSizeClass.Small => 420,
        SiriusModalSizeClass.Medium => 640,
        SiriusModalSizeClass.Large => 960,
        _ => throw new ArgumentOutOfRangeException(nameof(sizeClass), sizeClass, null)
    };
}
