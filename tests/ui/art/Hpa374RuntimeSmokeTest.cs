using GdUnit4;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class Hpa374RuntimeSmokeTest : Node
{
    private GameManager? _gameManager;
    private InventoryMenuController? _inventoryMenu;
    private SubViewport? _viewport;
    private SubViewportContainer? _viewportContainer;
    private bool _treeWasPaused;

    [BeforeTest]
    public async Task Setup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        _treeWasPaused = sceneTree.Paused;
        sceneTree.Paused = false;
        TestHelpers.ResetGameManagerSingleton();

        _gameManager = new GameManager { AutoSaveEnabled = false };
        sceneTree.Root.AddChild(_gameManager);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        _viewportContainer = new SubViewportContainer
        {
            Size = new Vector2(640, 360),
            Stretch = true
        };
        sceneTree.Root.AddChild(_viewportContainer);

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(640, 360)
        };
        _viewportContainer.AddChild(_viewport);
        var packed = ResourceLoader.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        _inventoryMenu = packed!.Instantiate<InventoryMenuController>();
        _viewport.AddChild(_inventoryMenu);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Paused = false;
        if (_inventoryMenu is { } menu && IsInstanceValid(menu))
        {
            if (menu.Visible)
                menu.CloseMenu();
            menu.Free();
        }
        if (_viewport is { } viewport && IsInstanceValid(viewport))
            viewport.Free();
        if (_viewportContainer is { } viewportContainer && IsInstanceValid(viewportContainer))
            viewportContainer.Free();
        if (_gameManager is { } gameManager && IsInstanceValid(gameManager))
            gameManager.Free();
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        TestHelpers.ResetGameManagerSingleton();
        sceneTree.Paused = _treeWasPaused;
    }

    [TestCase(640, 360)]
    [TestCase(1280, 720)]
    public async Task InventoryArtRendersAtVerificationSize(int width, int height)
    {
        _viewport!.Size = new Vector2I(width, height);
        _viewportContainer!.Size = new Vector2(width, height);
        AssertThat(_gameManager!.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu!.OpenMenu();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var equipmentHeading = _inventoryMenu.GetNode<TextureRect>("%EquipmentTitleIcon");
        var itemHeading = _inventoryMenu.GetNode<TextureRect>("%InventoryTitleIcon");
        var weapon = _inventoryMenu.GetNode<PanelContainer>("%WeaponSlot")
            .GetNode<TextureButton>("Button");
        var locked = _inventoryMenu.GetNode<PanelContainer>("%AccessorySlot4")
            .GetNode<TextureButton>("Button");
        var close = _inventoryMenu.GetNode<Button>("%CloseButton");

        AssertThat(_inventoryMenu.Visible).IsTrue();
        AssertThat(equipmentHeading.Texture!.GetSize()).IsEqual(new Vector2(24, 24));
        AssertThat(itemHeading.Texture!.GetSize()).IsEqual(new Vector2(24, 24));
        AssertThat(weapon.TextureNormal!.GetSize()).IsEqual(new Vector2(32, 32));
        AssertThat(locked.Disabled).IsTrue();
        AssertThat(locked.TextureDisabled!.GetSize()).IsEqual(new Vector2(32, 32));
        AssertThat(close.Text).StartsWith("Close [");

        var sword = EquipmentCatalog.CreateWoodenSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        AssertThat(weapon.TextureNormal!.ResourcePath).IsEqual(sword.AssetPath);
        AssertThat(weapon.TextureNormal.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));

        var rendered = _viewport.GetTexture().GetImage();
        if (DisplayServer.GetName().ToString() == "headless")
        {
            AssertThat(_viewport.GetTexture().GetSize())
                .IsEqual(new Vector2(width, height));
        }
        else
        {
            AssertThat(rendered).IsNotNull();
            AssertThat(rendered!.IsEmpty()).IsFalse();
        }
    }

    [TestCase]
    public void CompactHintSmoke_ChangesArtworkAndReadableLabelByDevice()
    {
        const string action = "hpa374_smoke_hint";
        var existed = InputMap.HasAction(action);
        var original = existed
            ? InputMap.ActionGetEvents(action)
                .Select(inputEvent => (InputEvent)inputEvent.Duplicate()).ToArray()
            : Array.Empty<InputEvent>();
        if (!existed)
            InputMap.AddAction(action, 0.5f);

        var presenter = new InputHintPresenter();
        var button = new Button();
        try
        {
            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Key.K });
            presenter.Observe(new InputEventKey { PhysicalKeycode = Key.K, Pressed = true });
            presenter.ApplyCompactButton(button, "Close", action);
            AssertThat(button.Icon!.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.KeycapBlank, UiIconSize.Metadata));
            AssertThat(button.Text).IsEqual("Close [K]");

            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left
            });
            presenter.Observe(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true
            });
            presenter.ApplyCompactButton(button, "Close", action);
            AssertThat(button.Icon!.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.MousePrimary, UiIconSize.Metadata));
            AssertThat(button.Text).IsEqual("Close [Mouse 1]");

            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.A
            });
            presenter.Observe(new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.A,
                Pressed = true
            });
            presenter.ApplyCompactButton(button, "Close", action);
            AssertThat(button.Icon!.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(
                    UiIconId.GamepadFaceBlank, UiIconSize.Metadata));
            AssertThat(button.Text).IsEqual("Close [A]");

            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventJoypadMotion
            {
                Axis = JoyAxis.LeftX,
                AxisValue = 1.0f
            });
            presenter.ApplyCompactButton(button, "Close", action);
            AssertThat(button.Icon!.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.GamepadStick, UiIconSize.Metadata));
            AssertThat(button.Text).IsEqual("Close [Left Stick Right]");
        }
        finally
        {
            button.Free();
            InputMap.ActionEraseEvents(action);
            if (existed)
            {
                foreach (var inputEvent in original)
                    InputMap.ActionAddEvent(action, inputEvent);
            }
            else
            {
                InputMap.EraseAction(action);
            }
        }
    }
}
