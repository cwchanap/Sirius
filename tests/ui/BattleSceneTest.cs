using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class BattleSceneTest : Node
{
    private const string ScenePath = "res://scenes/ui/BattleScene.tscn";

    private SceneTree _sceneTree = null!;
    private SubViewportContainer _viewportContainer = null!;
    private SubViewport _viewport = null!;
    private BattleManager _battle = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _sceneTree.Paused = false;

        _viewportContainer = new SubViewportContainer
        {
            Size = new Vector2(1280, 720),
            Stretch = false
        };
        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(1280, 720)
        };
        _viewportContainer.AddChild(_viewport);
        _sceneTree.Root.AddChild(_viewportContainer);

        var packed = GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException("Failed to load BattleScene.tscn.");
        _battle = packed.Instantiate<BattleManager>();
        _viewport.AddChild(_battle);
        await AwaitFrames(2);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        _sceneTree.Paused = false;
        if (GodotObject.IsInstanceValid(_viewportContainer))
            _viewportContainer.Free();
        await AwaitFrames(1);
    }

    [TestCase]
    public void AuthorsFinalBattlePresentationStructure()
    {
        foreach (var path in new[]
                 {
                     "%SafeFrame", "%PreparationPanel", "%AutomaticCombatPanel",
                     "%ResultPanel", "%CureOverlay", "%PlayerSpriteContainer",
                     "%EnemySpriteContainer", "%PlayerDamageLabel", "%EnemyDamageLabel",
                     "%BeginBattleButton", "%CureButton", "%EscapeButton", "%ContinueButton"
                 })
        {
            AssertThat(_battle.GetNodeOrNull(path)).IsNotNull();
        }

        var background = _battle.GetNodeOrNull<TextureRect>("BattleBackground");
        AssertThat(background).IsNotNull();
        if (background is not null)
            AssertThat(background.Texture).IsNotNull();
    }

    [TestCase]
    public void RemovesLegacyManualCombatControls()
    {
        foreach (var path in new[]
                 {
                     "BattleContent/ActionButtons/AttackButton",
                     "BattleContent/ActionButtons/DefendButton",
                     "BattleContent/ActionButtons/RunButton",
                     "BattleContent/ActionButtons/BattleSpeed"
                 })
            AssertThat(_battle.GetNodeOrNull(path)).IsNull();
    }

    [TestCase]
    public async Task SafeFrameAndActionTargetsFollowSharedResponsiveContract()
    {
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(640, 360) })
        {
            _viewport.Size = size;
            _viewportContainer.Size = new Vector2(size.X, size.Y);
            await AwaitFrames(2);

            var safeFrame = _battle.GetNodeOrNull<Control>("%SafeFrame");
            AssertThat(safeFrame).IsNotNull();
            if (safeFrame is null)
                return;
            var insets = SiriusUiMetrics.SafeFrameInsets(size);
            AssertThat(safeFrame.OffsetLeft).IsEqual(insets.SideInset);
            AssertThat(safeFrame.OffsetTop).IsEqual(insets.Margin);
            AssertThat(safeFrame.OffsetRight).IsEqual(-insets.SideInset);
            AssertThat(safeFrame.OffsetBottom).IsEqual(-insets.Margin);
            AssertThat(new Rect2(Vector2.Zero, size).Encloses(safeFrame.GetGlobalRect())).IsTrue();

            foreach (var path in new[] { "%BeginBattleButton", "%CureButton", "%EscapeButton", "%ContinueButton" })
            {
                var button = _battle.GetNode<Button>(path);
                AssertThat(button.Size.Y).IsGreaterEqual(SiriusUiMetrics.MinimumTarget(insets.Compact).Y);
                AssertThat(new Rect2(Vector2.Zero, size).Encloses(button.GetGlobalRect()))
                    .OverrideFailureMessage($"{path} rect {button.GetGlobalRect()} parent {button.GetParent<Control>().GetGlobalRect()} is outside viewport {size}")
                    .IsTrue();
            }
        }
    }

    [TestCase]
    public async Task ActorDamageFeedbackRemainsInsideAuthoredActorRegionsAtRest()
    {
        await AwaitFrames(2);
        var size = new Vector2I(1280, 720);
        var playerContainer = _battle.GetNodeOrNull<Control>("%PlayerSpriteContainer");
        var enemyContainer = _battle.GetNodeOrNull<Control>("%EnemySpriteContainer");
        var playerDamageLabel = _battle.GetNodeOrNull<Control>("%PlayerDamageLabel");
        var enemyDamageLabel = _battle.GetNodeOrNull<Control>("%EnemyDamageLabel");
        AssertThat(playerContainer).IsNotNull();
        AssertThat(enemyContainer).IsNotNull();
        AssertThat(playerDamageLabel).IsNotNull();
        AssertThat(enemyDamageLabel).IsNotNull();
        if (playerContainer is null || enemyContainer is null ||
            playerDamageLabel is null || enemyDamageLabel is null)
            return;

        var playerRegion = playerContainer.GetGlobalRect();
        var enemyRegion = enemyContainer.GetGlobalRect();
        var playerDamage = playerDamageLabel.GetGlobalRect();
        var enemyDamage = enemyDamageLabel.GetGlobalRect();

        AssertThat(new Rect2(Vector2.Zero, size).Encloses(playerRegion)).IsTrue();
        AssertThat(new Rect2(Vector2.Zero, size).Encloses(enemyRegion)).IsTrue();
        AssertThat(playerRegion.Encloses(playerDamage)).IsTrue();
        AssertThat(enemyRegion.Encloses(enemyDamage)).IsTrue();
    }

    [TestCase]
    public void PreparationAndCureRailsUseDynamicItemSlotControllers()
    {
        foreach (var path in new[] { "%PreparationItemRail", "%CureItemList" })
            AssertThat(_battle.GetNodeOrNull<Container>(path)).IsNotNull();

        if (_battle.GetNodeOrNull<Container>("%PreparationItemRail") is null ||
            _battle.GetNodeOrNull<Container>("%CureItemList") is null)
            return;

        var slotScene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusItemSlot.tscn");
        AssertThat(slotScene).IsNotNull();
        if (slotScene is not null)
        {
            var slot = slotScene.Instantiate<SiriusItemSlotController>();
            AssertThat(slot).IsNotNull();
            slot.Free();
        }
    }

    [TestCase]
    public async Task PreparationPagingUsesFourStandardAndThreeCompactSlots()
    {
        var player = TestHelpers.CreateTestCharacter();
        foreach (var item in new ConsumableItem[]
                 {
                     ConsumableCatalog.CreateHealthPotion(),
                     ConsumableCatalog.CreateManaPotion(),
                     ConsumableCatalog.CreateStrengthTonic(),
                     ConsumableCatalog.CreateIronSkin(),
                     ConsumableCatalog.CreateSwiftnessDraught()
                 })
            player.TryAddItem(item, 1, out _);

        _battle.StartBattle(player, Enemy.CreateGoblin());
        await AwaitFrames(2);
        var rail = _battle.GetNode<Container>("%PreparationItemRail");
        AssertThat(rail.GetChildCount()).IsEqual(4);

        _viewport.Size = new Vector2I(640, 360);
        _viewportContainer.Size = new Vector2(640, 360);
        await AwaitFrames(2);
        AssertThat(rail.GetChildCount()).IsEqual(3);
    }

    [TestCase]
    public async Task CompactResultsKeepContinueInsideSafeFrame()
    {
        _viewport.Size = new Vector2I(640, 360);
        _viewportContainer.Size = new Vector2(640, 360);
        await AwaitFrames(2);

        _battle.StartBattle(TestHelpers.CreateTestCharacter(), Enemy.CreateGoblin());
        var endBattle = typeof(BattleManager).GetMethod(
            "EndBattle", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("EndBattle not found.");
        endBattle.Invoke(_battle, new object[] { true });
        await AwaitFrames(2);

        var continueButton = _battle.GetNode<Button>("%ContinueButton");
        AssertThat(continueButton.Visible).IsTrue();
        AssertThat(continueButton.Size.Y).IsGreaterEqual(SiriusUiMetrics.MinimumTarget(true).Y);
        AssertThat(new Rect2(Vector2.Zero, new Vector2(640, 360))
            .Encloses(continueButton.GetGlobalRect()))
            .OverrideFailureMessage($"Continue rect {continueButton.GetGlobalRect()} is outside compact viewport")
            .IsTrue();
    }

    private async Task AwaitFrames(int count)
    {
        for (var index = 0; index < count; index++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
