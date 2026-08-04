using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusInputHintTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusInputHint.tscn";
    private static readonly StringName TestAction = "hpa377_sirius_input_hint";

    private SceneTree _sceneTree = null!;
    private SiriusInputHint _inputHint = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _inputHint = scene.Instantiate<SiriusInputHint>();
        _sceneTree.Root.AddChild(_inputHint);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_inputHint))
            _inputHint.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void Refresh_UsesKeyboardKBindingAndMetadataPresentation()
    {
        WithTemporaryAction(action =>
        {
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Key.K });
            _inputHint.Prompt = "Open";
            _inputHint.Actions = new[] { action };
            _inputHint.Observe(new InputEventKey { PhysicalKeycode = Key.K, Pressed = true });
            _inputHint.Refresh();

            AssertThat(_inputHint.ActiveDevice).IsEqual(UiInputDevice.Keyboard);
            AssertThat(_inputHint.GetNode<Label>("%PromptLabel").Text).IsEqual("Open");
            AssertThat(_inputHint.GetNode<Label>("%PromptLabel").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.Metadata);
            AssertHint(UiIconId.KeycapBlank, "K");
        });
    }

    [TestCase]
    public void Refresh_UsesMousePrimaryBindingWhenMouseIsObserved()
    {
        WithTemporaryAction(action =>
        {
            InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
            _inputHint.Actions = new[] { action };
            _inputHint.Observe(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true
            });
            _inputHint.Refresh();

            AssertThat(_inputHint.ActiveDevice).IsEqual(UiInputDevice.Mouse);
            AssertHint(UiIconId.MousePrimary, "Mouse 1");
        });
    }

    [TestCase]
    public void Refresh_UsesGamepadFaceBindingWhenGamepadIsObserved()
    {
        WithTemporaryAction(action =>
        {
            InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = JoyButton.A });
            _inputHint.Actions = new[] { action };
            _inputHint.Observe(new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.A,
                Pressed = true
            });
            _inputHint.Refresh();

            AssertThat(_inputHint.ActiveDevice).IsEqual(UiInputDevice.Gamepad);
            AssertHint(UiIconId.GamepadFaceBlank, "A");
        });
    }

    [TestCase]
    public void Refresh_FallsBackToFirstAvailableBindingWhenActiveDeviceHasNoBinding()
    {
        WithTemporaryAction(action =>
        {
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Key.K });
            _inputHint.Actions = new[] { action };
            _inputHint.Observe(new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.A,
                Pressed = true
            });
            _inputHint.Refresh();

            AssertThat(_inputHint.ActiveDevice).IsEqual(UiInputDevice.Gamepad);
            AssertHint(UiIconId.KeycapBlank, "K");
        });
    }

    [TestCase]
    public void Refresh_UsesUnboundInfoFallbackWhenNoActionHasABinding()
    {
        WithTemporaryAction(action =>
        {
            _inputHint.Actions = new[] { action };
            _inputHint.Refresh();

            AssertHint(UiIconId.Info, "Unbound");
        });
    }

    [TestCase]
    public void Input_IsIgnoredWhileTheHintIsHidden()
    {
        _inputHint.Visible = false;
        _inputHint._Input(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true
        });

        AssertThat(_inputHint.ActiveDevice).IsEqual(UiInputDevice.Keyboard);
    }

    private void AssertHint(UiIconId expectedIcon, string expectedBinding)
    {
        var icon = _inputHint.GetNode<TextureRect>("%DeviceIcon");
        var binding = _inputHint.GetNode<Label>("%BindingLabel");

        AssertThat(icon.Texture).IsNotNull();
        if (icon.Texture is not null)
        {
            AssertThat(icon.Texture.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(expectedIcon, UiIconSize.Metadata));
        }
        AssertThat(binding.Text).IsEqual(expectedBinding);
        AssertThat(binding.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Metadata);
    }

    private static void WithTemporaryAction(Action<StringName> assertion)
    {
        var actionExisted = InputMap.HasAction(TestAction);
        var originalEvents = actionExisted
            ? InputMap.ActionGetEvents(TestAction)
                .Select(inputEvent => (InputEvent)inputEvent.Duplicate())
                .ToArray()
            : Array.Empty<InputEvent>();

        if (!actionExisted)
            InputMap.AddAction(TestAction, 0.5f);

        try
        {
            InputMap.ActionEraseEvents(TestAction);
            assertion(TestAction);
        }
        finally
        {
            InputMap.ActionEraseEvents(TestAction);
            if (actionExisted)
            {
                foreach (var inputEvent in originalEvents)
                    InputMap.ActionAddEvent(TestAction, inputEvent);
            }
            else
            {
                InputMap.EraseAction(TestAction);
            }
        }
    }
}
