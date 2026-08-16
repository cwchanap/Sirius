using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class NpcInteractionControllerTest : Node
{
    private SceneTree _sceneTree = null!;
    private HostFixture _hostFixture = null!;
    private UIScreenHost _screenHost = null!;
    private Node _uiParent = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _hostFixture = await UIScreenHostTestSupport.CreateHost(this);
        _screenHost = _hostFixture.Host;

        _uiParent = new Node { Name = "LegacyNpcUiParent" };
        _sceneTree.Root.AddChild(_uiParent);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (_uiParent != null && GodotObject.IsInstanceValid(_uiParent))
            _uiParent.QueueFree();

        await UIScreenHostTestSupport.DisposeFixture(_hostFixture);

        _uiParent = null!;
        _hostFixture = null!;
        _screenHost = null!;
        _sceneTree = null!;
    }

    [TestCase]
    public void Begin_HostsOneDialogueEntry()
    {
        var controller = CreateController("old_farmer");
        controller.Begin();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(1);
        AssertThat(HostedDialogueCount()).IsEqual(1);
    }

    [TestCase]
    public async Task DialogueCancel_ClosesHostedEntryAndCompletesOnce()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        HostedDialogue().RequestCancel();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public async Task ShopOutcome_ClosesHostedDialogueBeforeNativeShopOpens()
    {
        int completed = 0;
        var controller = CreateController("village_shopkeeper");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        FindButton(HostedDialogue(), "Browse your wares.").EmitSignal(Button.SignalName.Pressed);
        await AwaitTwoFrames();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);

        var shop = _uiParent.GetChildren().OfType<ShopDialog>().Single();
        shop.EmitSignal(AcceptDialog.SignalName.Canceled);
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public async Task HealOutcome_ClosesHostedDialogueBeforeNativeHealOpens()
    {
        int completed = 0;
        var controller = CreateController("village_healer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        FindButton(HostedDialogue(), "Yes, heal me. (50 gold)").EmitSignal(Button.SignalName.Pressed);
        await AwaitTwoFrames();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);

        var heal = _uiParent.GetChildren().OfType<HealDialog>().Single();
        heal.EmitSignal(AcceptDialog.SignalName.Canceled);
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public async Task Finish_WhileDialogueActive_ClosesHostedEntryAndCompletesOnce()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        controller.Finish();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public void MissingTree_CreatesNoHostedEntryAndCompletesOnce()
    {
        var npc = new NpcData
        {
            NpcId = "missing_dialogue_test",
            DisplayName = "Missing Dialogue",
            NpcType = NpcType.Villager,
            DialogueTreeId = "missing_dialogue_tree",
            SpriteType = "villager"
        };
        int completed = 0;
        var controller = CreateController(npc);
        controller.InteractionComplete += () => completed++;
        controller.Begin();
        controller.Finish();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public void HostRejectsDialogue_CleansCandidateAndCompletesOnce()
    {
        var fixtureScreen = new Control();
        AssertThat(_screenHost.TryPresent(fixtureScreen, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Dialogue,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Consume,
            NodeLifetime = UINodeLifetime.QueueFree
        }).Status).IsEqual(UIScreenOpenStatus.Opened);

        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(1); // only the pre-existing fixture entry
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public async Task Begin_PublicationSubscriberThrows_CompletesOnceAndCleansOrphanedEntry()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;

        // The host publishes EffectiveStateChanged after the entry commits;
        // a throwing subscriber escapes TryPresent and leaves the entry
        // hosted. Throw only on the blocked publication; the NodeFreed close
        // republishes unblocked and must not throw again.
        _screenHost.EffectiveStateChanged += state =>
        {
            if (state.IsPresentationGameplayBlocked)
                throw new InvalidOperationException("publication boom");
        };

        AssertThrown(() => controller.Begin())
            .IsInstanceOf<InvalidOperationException>();
        AssertThat(completed).IsEqual(1);

        await AwaitTwoFrames();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public async Task Begin_PublicationSubscriberClosesDialogue_CompletesOnceAndLeavesNoStaleHandle()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;

        // The host publishes EffectiveStateChanged after the entry commits; a
        // subscriber that synchronously closes the Dialogue entry during that
        // publication is a post-commit mutation. UIScreenHost documents that
        // TryPresent() may return Opened with the entry already closed. Without
        // the IsActive re-check in Begin(), the controller would retain a stale
        // screen/handle, no terminal signal would fire (the close path already
        // unsubscribed the dialogue signals), and the interaction would
        // soft-lock with GameManager.IsInNpcInteraction stuck true. Close only
        // once so the close-path's own republish does not re-enter.
        var closed = false;
        _screenHost.EffectiveStateChanged += _ =>
        {
            if (closed)
                return;
            var dialogueEntry = _screenHost.ActiveEntries
                .FirstOrDefault(e => e.Policy.Kind == UIScreenKinds.Dialogue);
            if (dialogueEntry == null)
                return;
            closed = true;
            _screenHost.TryClose(dialogueEntry.Handle, UIScreenCloseReason.Programmatic);
        };

        controller.Begin();

        AssertThat(completed).IsEqual(1);

        await AwaitTwoFrames();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public async Task Close_PublicationSubscriberThrows_CompletesOnceAndCleansEntry()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;

        // Let the initial blocked publication succeed so Begin() completes
        // normally and the Dialogue entry is hosted. Throw only when the close
        // publishes the unblocked state — the publication that escapes TryClose
        // and, without catching it in OnDialogueClosed, prevents Finish() from
        // being reached. By the time Recompute publishes, the host's Cleanup
        // callback (ClearDialoguePresentation) has already unsubscribed the
        // dialogue signals and cleared _dialogueScreen/_dialogueHandle, so the
        // Dialogue is gone but InteractionComplete never fires — leaving
        // GameManager.IsInNpcInteraction latched. Throw only once so the
        // recovery path's republish (if any) does not re-enter.
        var thrown = false;
        _screenHost.EffectiveStateChanged += state =>
        {
            if (!state.IsPresentationGameplayBlocked && !thrown)
            {
                thrown = true;
                throw new InvalidOperationException("close publication boom");
            }
        };

        controller.Begin();

        // RequestCancel fires DialogueClosed synchronously, which calls
        // OnDialogueClosed → CloseDialoguePresentation → TryClose → Recompute
        // (publishes unblocked → throws). Without the fix the exception
        // escapes RequestCancel and Finish() is never called.
        HostedDialogue().RequestCancel();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public async Task Finish_PublicationSubscriberThrows_CompletesOnceAndCleansEntry()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;

        // Parallel to Close_PublicationSubscriberThrows but exercises the
        // Finish() trap directly: Finish() sets _finished = true BEFORE
        // calling CloseDialoguePresentation. If that close publication throws,
        // InteractionComplete is skipped and every later Finish() retry is a
        // no-op (_finished is already true) — a permanent soft-lock. Throw
        // only on the first unblocked publication so the recovery path does
        // not re-enter.
        var thrown = false;
        _screenHost.EffectiveStateChanged += state =>
        {
            if (!state.IsPresentationGameplayBlocked && !thrown)
            {
                thrown = true;
                throw new InvalidOperationException("finish publication boom");
            }
        };

        controller.Begin();

        // Finish() calls CloseDialoguePresentation, whose TryClose → Recompute
        // publishes the unblocked state and throws. Without the fix the
        // exception escapes Finish(), InteractionComplete never fires, and
        // _finished is left true so no retry can recover.
        controller.Finish();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    private NpcInteractionController CreateController(string npcId)
    {
        var npc = NpcCatalog.GetById(npcId)
            ?? throw new InvalidOperationException($"NPC '{npcId}' not found.");
        return CreateController(npc);
    }

    private NpcInteractionController CreateController(NpcData npc)
    {
        return new NpcInteractionController(
            null!,
            _screenHost,
            _uiParent,
            npc,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>());
    }

    private DialogueScreenController HostedDialogue()
    {
        var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
        return modalLayer.GetChildren().OfType<DialogueScreenController>().Single();
    }

    private int HostedDialogueCount()
    {
        var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
        return modalLayer.GetChildren().OfType<DialogueScreenController>().Count();
    }

    private async Task AwaitTwoFrames()
    {
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static Button FindButton(Node node, string text)
    {
        if (node is Button button && button.Text == text)
            return button;

        foreach (Node child in node.GetChildren())
        {
            var found = FindButtonOrNull(child, text);
            if (found != null)
                return found;
        }

        throw new InvalidOperationException($"Button '{text}' not found.");
    }

    private static Button? FindButtonOrNull(Node node, string text)
    {
        if (node is Button button && button.Text == text)
            return button;

        foreach (Node child in node.GetChildren())
        {
            var found = FindButtonOrNull(child, text);
            if (found != null)
                return found;
        }

        return null;
    }
}
