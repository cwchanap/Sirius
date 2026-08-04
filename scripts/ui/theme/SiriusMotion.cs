using Godot;

public static class SiriusMotion
{
    public const double EntrySeconds = 0.220;
    public const double ExitSeconds = 0.180;
    public const double ReducedOpacitySeconds = 0.100;
    public const Tween.TransitionType EntryTransition = Tween.TransitionType.Cubic;
    public const Tween.EaseType EntryEase = Tween.EaseType.Out;
    public const Tween.TransitionType ExitTransition = Tween.TransitionType.Quad;
    public const Tween.EaseType ExitEase = Tween.EaseType.In;

    public static double Duration(bool reducedMotion, bool entering) =>
        reducedMotion ? ReducedOpacitySeconds : entering ? EntrySeconds : ExitSeconds;

    public static bool UseTransform(bool reducedMotion) => !reducedMotion;
}
