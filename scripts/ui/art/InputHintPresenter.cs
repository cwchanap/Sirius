using Godot;
using System;
using System.Linq;

public enum UiInputDevice
{
    Keyboard,
    Mouse,
    Gamepad
}

public readonly record struct UiInputHint(
    UiInputDevice Device,
    UiIconId IconId,
    string BindingLabel,
    bool IsRepresentable);

public sealed class InputHintPresenter
{
    public UiInputDevice ActiveDevice { get; private set; }

    public InputHintPresenter(UiInputDevice initialDevice = UiInputDevice.Keyboard)
    {
        ActiveDevice = initialDevice;
    }

    public bool Observe(InputEvent inputEvent)
    {
        var next = inputEvent switch
        {
            InputEventKey key when key.Pressed && !key.Echo => UiInputDevice.Keyboard,
            InputEventMouseButton mouse when mouse.Pressed => UiInputDevice.Mouse,
            InputEventJoypadButton button when button.Pressed => UiInputDevice.Gamepad,
            InputEventJoypadMotion motion when Math.Abs(motion.AxisValue) >= 0.5f
                => UiInputDevice.Gamepad,
            _ => ActiveDevice
        };
        var changed = next != ActiveDevice;
        ActiveDevice = next;
        return changed;
    }

    public UiInputHint Resolve(StringName action) => ResolveActions(action);

    public UiInputHint ResolveActions(params StringName[] actions)
    {
        InputEvent? firstFallback = null;
        foreach (var action in actions)
        {
            if (!InputMap.HasAction(action))
                continue;
            var events = InputMap.ActionGetEvents(action);
            var deviceMatch = events.FirstOrDefault(MatchesActiveDevice);
            if (deviceMatch != null)
                return HintFor(deviceMatch);
            firstFallback ??= events.FirstOrDefault();
        }
        return firstFallback != null ? HintFor(firstFallback) : UnboundHint(ActiveDevice);
    }

    public void ApplyCompactButton(Button button, string baseText, params StringName[] actions)
    {
        var hint = ResolveActions(actions);
        UiIconPresenter.Apply(button, hint.IconId, UiIconSize.Metadata);
        button.Text = $"{baseText} [{hint.BindingLabel}]";
        button.TooltipText = $"{baseText}: {hint.BindingLabel}";
    }

    private bool MatchesActiveDevice(InputEvent inputEvent) => ActiveDevice switch
    {
        UiInputDevice.Keyboard => inputEvent is InputEventKey,
        UiInputDevice.Mouse => inputEvent is InputEventMouseButton,
        UiInputDevice.Gamepad => inputEvent is InputEventJoypadButton or InputEventJoypadMotion,
        _ => false
    };

    private static UiInputHint HintFor(InputEvent inputEvent) => inputEvent switch
    {
        InputEventKey key => new(
            UiInputDevice.Keyboard,
            UiIconId.KeycapBlank,
            OS.GetKeycodeString(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode),
            true),
        InputEventMouseButton mouse => MouseHint(mouse.ButtonIndex),
        InputEventJoypadButton button => JoyButtonHint(button.ButtonIndex),
        InputEventJoypadMotion motion => JoyAxisHint(motion.Axis, motion.AxisValue),
        _ => UnboundHint(UiInputDevice.Keyboard)
    };

    private static UiInputHint MouseHint(MouseButton button) => button switch
    {
        MouseButton.Left => new(UiInputDevice.Mouse, UiIconId.MousePrimary, "Mouse 1", true),
        MouseButton.Right => new(UiInputDevice.Mouse, UiIconId.MouseSecondary, "Mouse 2", true),
        MouseButton.Middle => new(UiInputDevice.Mouse, UiIconId.MouseWheel, "Mouse Wheel", true),
        MouseButton.WheelUp => new(UiInputDevice.Mouse, UiIconId.MouseWheel, "Wheel Up", true),
        MouseButton.WheelDown => new(UiInputDevice.Mouse, UiIconId.MouseWheel, "Wheel Down", true),
        _ => new(UiInputDevice.Mouse, UiIconId.Mouse, button.ToString(), true)
    };

    private static UiInputHint JoyButtonHint(JoyButton button) => button switch
    {
        JoyButton.A or JoyButton.B or JoyButton.X or JoyButton.Y => new(
            UiInputDevice.Gamepad, UiIconId.GamepadFaceBlank, button.ToString().ToUpperInvariant(), true),
        JoyButton.DpadUp => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Up", true),
        JoyButton.DpadDown => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Down", true),
        JoyButton.DpadLeft => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Left", true),
        JoyButton.DpadRight => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Right", true),
        JoyButton.LeftShoulder => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Left Shoulder", true),
        JoyButton.RightShoulder => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Right Shoulder", true),
        JoyButton.LeftStick => new(UiInputDevice.Gamepad, UiIconId.GamepadStick, "Left Stick", true),
        JoyButton.RightStick => new(UiInputDevice.Gamepad, UiIconId.GamepadStick, "Right Stick", true),
        _ => new(UiInputDevice.Gamepad, UiIconId.Gamepad, button.ToString(), true)
    };

    private static UiInputHint JoyAxisHint(JoyAxis axis, float value) => axis switch
    {
        JoyAxis.LeftX => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Left Stick Right" : "Left Stick Left", true),
        JoyAxis.LeftY => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Left Stick Down" : "Left Stick Up", true),
        JoyAxis.RightX => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Right Stick Right" : "Right Stick Left", true),
        JoyAxis.RightY => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Right Stick Down" : "Right Stick Up", true),
        JoyAxis.TriggerLeft => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Left Trigger", true),
        JoyAxis.TriggerRight => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Right Trigger", true),
        _ => new(UiInputDevice.Gamepad, UiIconId.Gamepad, axis.ToString(), true)
    };

    private static UiInputHint UnboundHint(UiInputDevice device) =>
        new(device, UiIconId.Info, "Unbound", false);
}
