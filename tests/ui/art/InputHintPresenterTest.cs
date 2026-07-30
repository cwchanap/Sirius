using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class InputHintPresenterTest : Node
{
    private static readonly StringName TestAction = "hpa374_test_hint";
    private readonly List<InputEvent> _originalEvents = new();
    private bool _actionExisted;

    [BeforeTest]
    public void SetupAction()
    {
        _actionExisted = InputMap.HasAction(TestAction);
        if (!_actionExisted)
            InputMap.AddAction(TestAction, 0.5f);
        foreach (var inputEvent in InputMap.ActionGetEvents(TestAction))
            _originalEvents.Add((InputEvent)inputEvent.Duplicate());
        InputMap.ActionEraseEvents(TestAction);
    }

    [AfterTest]
    public void RestoreAction()
    {
        InputMap.ActionEraseEvents(TestAction);
        if (_actionExisted)
        {
            foreach (var inputEvent in _originalEvents)
                InputMap.ActionAddEvent(TestAction, inputEvent);
        }
        else
        {
            InputMap.EraseAction(TestAction);
        }
        _originalEvents.Clear();
    }

    [TestCase]
    public void Resolve_ReReadsKeyboardBindingOnEveryCall()
    {
        InputMap.ActionAddEvent(TestAction, new InputEventKey { PhysicalKeycode = Key.I });
        var presenter = new InputHintPresenter(UiInputDevice.Keyboard);

        AssertThat(presenter.Resolve(TestAction).BindingLabel).IsEqual("I");

        InputMap.ActionEraseEvents(TestAction);
        InputMap.ActionAddEvent(TestAction, new InputEventKey { PhysicalKeycode = Key.K });
        AssertThat(presenter.Resolve(TestAction).BindingLabel).IsEqual("K");
    }

    [TestCase]
    public void Observe_SwitchesBetweenMouseJoypadButtonAndJoypadAxis()
    {
        var presenter = new InputHintPresenter(UiInputDevice.Keyboard);

        presenter.Observe(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Mouse);

        presenter.Observe(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
        AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Gamepad);

        presenter.Observe(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 0.75f });
        AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Gamepad);
    }

    [TestCase]
    public void Resolve_UnboundActionReturnsReadableFallback()
    {
        var hint = new InputHintPresenter(UiInputDevice.Keyboard).Resolve(TestAction);
        AssertThat(hint.IsRepresentable).IsFalse();
        AssertThat(hint.BindingLabel).IsEqual("Unbound");
        AssertThat(hint.IconId).IsEqual(UiIconId.Info);
    }

    [TestCase]
    public void Resolve_MapsMousePrimaryComponent()
    {
        InputMap.ActionAddEvent(TestAction, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        var hint = new InputHintPresenter(UiInputDevice.Mouse).Resolve(TestAction);
        AssertThat(hint.IconId).IsEqual(UiIconId.MousePrimary);
        AssertThat(hint.BindingLabel).IsEqual("Mouse 1");
    }

    [TestCase]
    public void Resolve_MapsFaceButtonAndStickAxisComponents()
    {
        InputMap.ActionAddEvent(TestAction, new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        var presenter = new InputHintPresenter(UiInputDevice.Gamepad);
        var face = presenter.Resolve(TestAction);
        AssertThat(face.IconId).IsEqual(UiIconId.GamepadFaceBlank);
        AssertThat(face.BindingLabel).IsEqual("A");

        InputMap.ActionEraseEvents(TestAction);
        InputMap.ActionAddEvent(TestAction, new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1.0f });
        var stick = presenter.Resolve(TestAction);
        AssertThat(stick.IconId).IsEqual(UiIconId.GamepadStick);
        AssertThat(stick.BindingLabel).IsEqual("Left Stick Right");
    }

    [TestCase]
    public void Observe_IgnoresJoypadMotionBelowDeadzone()
    {
        var presenter = new InputHintPresenter(UiInputDevice.Keyboard);
        var changed = presenter.Observe(new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 0.49f });
        AssertThat(changed).IsFalse();
        AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Keyboard);
    }

    [TestCase]
    public void ResolveActions_PicksGamepadBindingFromSecondActionWhenFirstLacksIt()
    {
        // Real configuration: toggle_inventory has only a keyboard binding,
        // ui_cancel has both keyboard and gamepad bindings. A gamepad user
        // closing the menu via ui_cancel must see a gamepad hint, not the
        // keyboard fallback from toggle_inventory.
        var toggleOriginal = InputMap.ActionGetEvents("toggle_inventory")
            .Select(e => (InputEvent)e.Duplicate()).ToArray();
        var cancelExisted = InputMap.HasAction("ui_cancel");
        var cancelOriginal = cancelExisted
            ? InputMap.ActionGetEvents("ui_cancel")
                .Select(e => (InputEvent)e.Duplicate()).ToArray()
            : System.Array.Empty<InputEvent>();
        try
        {
            InputMap.ActionEraseEvents("toggle_inventory");
            InputMap.ActionAddEvent("toggle_inventory", new InputEventKey { PhysicalKeycode = Key.I });

            if (!cancelExisted)
                InputMap.AddAction("ui_cancel", 0.5f);
            InputMap.ActionEraseEvents("ui_cancel");
            InputMap.ActionAddEvent("ui_cancel", new InputEventKey { PhysicalKeycode = Key.Escape });
            InputMap.ActionAddEvent("ui_cancel", new InputEventJoypadButton { ButtonIndex = JoyButton.B });

            var gamepadHint = new InputHintPresenter(UiInputDevice.Gamepad)
                .ResolveActions("toggle_inventory", "ui_cancel");
            AssertThat(gamepadHint.Device).IsEqual(UiInputDevice.Gamepad);
            AssertThat(gamepadHint.BindingLabel).IsEqual("B");

            var keyboardHint = new InputHintPresenter(UiInputDevice.Keyboard)
                .ResolveActions("toggle_inventory", "ui_cancel");
            AssertThat(keyboardHint.Device).IsEqual(UiInputDevice.Keyboard);
            AssertThat(keyboardHint.BindingLabel).IsEqual("I");
        }
        finally
        {
            InputMap.ActionEraseEvents("toggle_inventory");
            foreach (var e in toggleOriginal)
                InputMap.ActionAddEvent("toggle_inventory", e);
            if (cancelExisted)
            {
                InputMap.ActionEraseEvents("ui_cancel");
                foreach (var e in cancelOriginal)
                    InputMap.ActionAddEvent("ui_cancel", e);
            }
        }
    }
}
