# HPA-384 Save/Load Responsive Cards Design

**Status:** Implementation-ready design
**Linear:** HPA-384 — Redesign Sirius save/load flows with responsive save cards
**Target:** `main` after HPA-380, HPA-381, HPA-382, and HPA-383

## 1. Goal

Replace the runtime-built `SaveLoadDialog : AcceptDialog` with a scene-authored Sirius Save/Load flow while preserving the save domain and lifecycle behavior already shipped.

This is a presentation migration. It does not redesign persistence, add save features, or introduce a save facade, view model, navigation service, host factory, or generic confirmation framework.

## 2. Verified current-state findings

The relevant seams on current `main` are small and already well separated:

- `SaveManager` owns three manual slots, autosave, metadata reads, version validation, atomic writes, backup restoration, and full loads.
- `MainMenu` owns its Load entry, `PendingLoadData`, load-failure recovery, and scene transition.
- `Game` owns gameplay save eligibility, `CollectSaveData`, persistence calls, load handoff, errors, and scene transitions.
- Main Menu and gameplay each already own one local `UIScreenHost`.
- `SiriusModalShell` already owns modal chrome and responsive width. Its scene also owns `BodyScroll`, but the shared component has no bounded-height policy yet.
- `UIScreenKinds.ConfirmOverwrite` already exists even though current Save/Load still uses a native `AcceptDialog` child.
- `PauseReturnToTitleConfirmation.tscn` + `PauseReturnToTitleConfirmationController` demonstrate the shipped scene-authored blocking-confirmation pattern: `SiriusModalShell`, explicit buttons, safe initial focus, and `UIScreenHost` parent/child lifecycle.
- `GameInputLifecycleTest` has direct `SaveLoadDialog` assumptions that must migrate before the legacy type is deleted.
- `SettingsManager.cs` and `SettingsManagerTest.cs` contain comments naming `SaveLoadDialog`; these are documentation blast-radius items rather than runtime dependencies.
- The HPA-373 Save/Load wireframe uses cards, but its Delete action is superseded by HPA-384, which explicitly excludes Delete.

One previous HPA-384 planning revision proposed `Control.CustomMaximumSize`. Godot 4.6 does not expose that property. The implementation must use supported `Control.Size`, minimum-size, and `ScrollContainer` behavior instead.

## 3. Chosen shape

HPA-384 remains a small vertical migration, with two narrow shared reuses that reduce duplicate work:

1. complete the existing `SiriusModalShell` height/scroll contract once;
2. author the repeated save-card subtree once as a script-less scene.

The production flow then consists of:

- `scenes/ui/components/SiriusSaveSlotCard.tscn` — script-less card structure only;
- `scenes/ui/SaveLoadScreen.tscn` + `scripts/ui/SaveLoadScreenController.cs` — four explicit card instances and presentation intent;
- `scenes/ui/SaveOverwriteConfirmation.tscn` + `scripts/ui/SaveOverwriteConfirmationController.cs` — feature-specific scene-authored overwrite confirmation;
- existing `MainMenu` and `Game` host call sites — concrete view cutovers only;
- existing `SaveManager` — one additive `SaveSlotState` classification.

This does **not** introduce a card controller, collection renderer, save repository, or shared confirmation framework.

## 4. Shared `SiriusModalShell` height contract

### 4.1 Why this belongs in the shell

`SiriusModalShell` owns all chrome that determines usable modal height:

- centred `Panel`;
- margins;
- header;
- `BodyScroll` / `BodyHost`;
- footer `ActionsHost`.

It already owns responsive width in `RefreshPresentation(Vector2 availableSize)`. Letting every future modal derive a separate panel-height formula would duplicate knowledge of the same shell hierarchy.

Settings remains the exception it already is: it disables the shell scroller and uses page-local scrollers. The shared height behavior must respect `BodyScroll.VerticalScrollMode == Disabled` and must not re-enable Settings' outer scroll.

### 4.2 Supported sizing primitive

Godot 4.6 provides `Control.Size`, `CustomMinimumSize`, `GetCombinedMinimumSize()`, and `ScrollContainer` overflow. It does not provide `CustomMaximumSize`.

The shell therefore uses a content-fit body minimum, bounded by the available safe height. The implementation target is:

```csharp
private ScrollContainer _bodyScroll = null!;

private void RefreshBodyHeight(Vector2 availableSize)
{
    if (_bodyScroll.VerticalScrollMode == ScrollContainer.ScrollMode.Disabled)
        return;

    var safeMargin = SiriusUiMetrics.SafeMargin(Compact);
    var maximumPanelHeight = Mathf.Max(0f, availableSize.Y - safeMargin * 2f);

    var currentBodyMinimum = _bodyScroll.GetCombinedMinimumSize().Y;
    var currentPanelMinimum = _panel.GetCombinedMinimumSize().Y;
    var chromeHeight = Mathf.Max(0f, currentPanelMinimum - currentBodyMinimum);
    var maximumBodyHeight = Mathf.Max(0f, maximumPanelHeight - chromeHeight);
    var contentHeight = BodyHost.GetCombinedMinimumSize().Y;
    var bodyHeight = Mathf.Min(contentHeight, maximumBodyHeight);

    _bodyScroll.CustomMinimumSize = new Vector2(
        _bodyScroll.CustomMinimumSize.X,
        bodyHeight);
    _bodyScroll.FollowFocus = true;
}
```

`RefreshPresentation` keeps its current width calculation and then applies this body-height policy.

This formula is an implementation target, not an unmeasured runtime claim. Task 1 begins with a 640×360 regression fixture that must prove the actual Godot layout behavior before HPA-384 screen work proceeds. If Godot minimum-size propagation makes this exact calculation fail the contract, Task 1 stays inside `SiriusModalShell` and adjusts the supported sizing primitive there; downstream Save/Load tasks do not invent a feature-local workaround.

### 4.3 Shell acceptance contract

At 640×360 with deliberately tall body content and a footer:

- the panel stays inside the viewport safe bounds;
- title and footer remain visible;
- `BodyScroll.GetVScrollBar().MaxValue > BodyScroll.GetVScrollBar().Page`;
- `BodyScroll.FollowFocus == true`;
- focusing a lower body control can bring it into view;
- a short-body confirmation remains content-fit rather than expanding to full safe height.

Existing width, Settings, Pause, return-to-title confirmation, and showcase tests remain regression gates.

No exact panel/scroll numbers are claimed in this planning PR because a Godot 4.6 runtime is not available in the planning environment; Task 1 records those measurements from the real test fixture before implementation continues.

## 5. Save metadata state

`SaveSlotInfo` currently collapses corrupted and newer-version files into `IsCorrupted`. Add one small presentation classification:

```csharp
public enum SaveSlotState
{
    Empty,
    Valid,
    Corrupted,
    Incompatible
}

public class SaveSlotInfo
{
    public SaveSlotState State { get; set; } = SaveSlotState.Empty;
    // Existing fields remain unchanged.
}
```

`SaveManager.GetSaveSlotInfo` / `ExtractMetadataFromFile` resolves:

- no primary or backup → `Empty`;
- supported readable metadata → `Valid`;
- invalid JSON, missing required metadata, unreadable format → `Corrupted`;
- `fileVersion > SaveData.CurrentVersion` → `Incompatible`.

Keep existing `Exists` and `IsCorrupted` semantics so HPA-380 Continue remains unchanged. An incompatible file remains `Exists = true` and `IsCorrupted = true`.

The old `PlayerName = $"Newer Version (v{fileVersion})"` workaround is removed. `PlayerName` remains `null` for `Incompatible`; the UI renders the compatibility reason from `State`. No status string is stored in a metadata field that purports to contain player data.

Backup recovery remains transparent. A usable recovered backup resolves to ordinary `Valid`; there is no `Recovered` UI state.

## 6. Script-less save card component

Create `scenes/ui/components/SiriusSaveSlotCard.tscn` with **no script**.

Structure:

```text
SiriusSaveSlotCard (Button)
└── Margin
    └── Content (VBoxContainer)
        ├── SlotNameLabel
        ├── DetailLabel
        ├── TimestampLabel
        ├── StateLabel
        └── ActionLabel
```

The screen instances this scene four times and gives the roots stable unique names:

- `Slot0Card`
- `Slot1Card`
- `Slot2Card`
- `Slot3Card`

The component centralizes only authored structure, spacing, wrapping, and theme roles. It has no C# API, no state object, and no behavior. `SaveLoadScreenController` still owns one `_cards` array and applies data through relative child paths.

This preserves the fixed four-slot model while avoiding four hand-maintained copies of the same subtree.

## 7. Save/Load screen contract

Create `SaveLoadScreen.tscn` using the existing Theme and `SiriusModalShell`.

```text
SaveLoadScreenController (Control, full rect)
├── Background (SiriusScrim)
└── ModalShell (Large)
    ├── BodyHost
    │   └── CardsGrid
    │       ├── Slot0Card (SiriusSaveSlotCard instance)
    │       ├── Slot1Card (instance)
    │       ├── Slot2Card (instance)
    │       └── Slot3Card (instance)
    └── ActionsHost
        ├── MainMenuButton
        └── CancelButton
```

Controller contract:

```csharp
public enum SaveLoadMode
{
    Save,
    Load
}

public partial class SaveLoadScreenController : Control
{
    [Signal] public delegate void SaveSlotSelectedEventHandler(int slot);
    [Signal] public delegate void LoadSlotSelectedEventHandler(int slot);
    [Signal] public delegate void OverwriteRequestedEventHandler(int slot);
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void MainMenuRequestedEventHandler();

    public SaveLoadMode Mode { get; set; }
    public Control InitialFocusTarget => FirstEnabledCard() ?? _cancelButton;
}
```

There is no `HasActiveChildDialog`, `DismissActiveChildDialog`, `AcceptDialog`, or screen-owned modal stack.

### 7.1 Action matrix

| Slot state | Save mode, manual 0–2 | Save mode, autosave 3 | Load mode |
| --- | --- | --- | --- |
| Empty | Enabled — `Save` | Disabled — `Autosave is created automatically` | Disabled — `No save data to load` |
| Valid | Enabled — `Overwrite` → `OverwriteRequested(slot)` | Disabled — `Autosave is created automatically` | Enabled — `Load` |
| Corrupted | Enabled — `Save` | Disabled — `Autosave is created automatically` | Disabled — `File cannot be read` |
| Incompatible | Enabled — `Save` | Disabled — `Autosave is created automatically` | Disabled — `Requires a newer game version` |

The card shows only real supported metadata: slot identity, valid player name, level, floor, timestamp, and explicit state/action text.

If `SaveManager.Instance` is unexpectedly unavailable, the controller defensively disables all cards with `Save system unavailable` and falls back to Cancel focus. There is no dedicated injection/reset seam or isolated no-manager test: `SaveManager` is an autoload in supported runtime, and adding a production dependency override solely to simulate broken project configuration is not justified.

### 7.2 Terminal guard

Keep one `_terminalEmitted` latch for terminal screen intents:

- direct Save;
- Load;
- Close;
- Main Menu.

`OverwriteRequested` is **not** terminal because Cancel returns to the same Save/Load screen. While the hosted confirmation is open, `UIScreenHost` makes the Save/Load parent inert.

## 8. Scene-authored overwrite confirmation

Create `SaveOverwriteConfirmation.tscn` patterned narrowly after the existing return-to-title confirmation.

```text
SaveOverwriteConfirmationController (Control)
└── ModalShell
    ├── BodyHost
    │   └── Message
    └── ActionsHost
        ├── CancelButton
        └── OverwriteButton
```

Controller:

```csharp
public partial class SaveOverwriteConfirmationController : Control
{
    [Signal] public delegate void OverwriteConfirmedEventHandler(int slot);
    [Signal] public delegate void CancelRequestedEventHandler();

    public int Slot { get; set; }
    public Control InitialFocusTarget => _cancel;
}
```

The message uses only slot identity, e.g. `Slot 1 already contains save data. Overwrite it?`; it does not fabricate player metadata.

`Game` presents this scene as a logical child of the active Save/Load entry using the already-existing `UIScreenKinds.ConfirmOverwrite`:

- `Layer = Modal`;
- `InputPriority = Blocking`;
- `ProcessPolicy = Always`;
- `Parent = _hostedSaveLoadHandle`;
- `ExclusiveGroup = BlockingPrompt`;
- `PauseTree = false`;
- `BlockGameplayInput = false`;
- `Cursor = Visible`;
- `Hud = Inherit`;
- `LowerLayers = VisibleInert`;
- `Cancel = Close`;
- `InitialFocus = confirmation.InitialFocusTarget`;
- `NodeLifetime = QueueFree`.

Cancel closes only the confirmation and host focus restoration returns to Save/Load. Confirm closes the confirmation and routes the confirmed slot into the existing `OnHostedSaveSlotSelected` domain handler. The confirmation controller is one-shot so repeated button/signal activation cannot save twice.

This is feature-specific reuse of the existing host/scene pattern, not implementation of HPA-572’s future generic confirmation framework.

## 9. Host integration

### 9.1 Main Menu

`MainMenu.TryOpenHostedLoad` swaps only the concrete hosted view:

- instantiate `SaveLoadScreen.tscn`;
- `Mode = Load`;
- wire `LoadSlotSelected` and `Closed`;
- keep `UIScreenKinds.SaveLoad`, current modal policy, restore-focus target, `PendingLoadData`, error behavior, and teardown-safe scene transition;
- add `InitialFocus = screen.InitialFocusTarget`.

There is no Save mode and therefore no overwrite child on Main Menu. Remove the old SaveLoad-specific `InterceptCancel` closure; normal host `Cancel = Close` is sufficient.

### 9.2 Gameplay Pause

`Game.TryOpenHostedSaveLoad` swaps to the same screen and keeps the Pause handle as its parent. It wires all existing Save/Load/Close/MainMenu intents plus `OverwriteRequested`.

The Save/Load entry itself needs no child-interception closure. When overwrite is requested, `Game` presents `SaveOverwriteConfirmation` with `Parent = _hostedSaveLoadHandle`; topmost child-first Cancel is handled by `UIScreenHost` directly.

`Game` continues to own save eligibility, collection, SaveManager calls, errors, load handoff, Return to Main Menu, and scene transitions.

## 10. Responsive layout and focus

Save/Load itself only owns feature reflow:

```csharp
private void RefreshLayout()
{
    var size = GetViewportRect().Size;
    var compact = SiriusUiMetrics.IsCompact(size);

    _shell.Compact = compact;
    _shell.RefreshPresentation(size);
    _cardsGrid.Columns = compact ? 1 : 2;
    ApplyCardTypography(compact);
    ApplyMinimumTargets(compact);
}
```

The shell owns content-fit/bounded body height. Save/Load does not reach into `%Panel` to size it.

Standard behavior:

- Large modal;
- 2×2 card grid;
- 44 px minimum targets.

Compact behavior:

- one-column cards;
- compact typography;
- 40 px minimum targets;
- optional timestamp may be hidden before essential state/action text;
- shell body scrolls while title/footer stay fixed.

Initial focus is the first enabled card, otherwise Cancel. There is no cross-opening remembered selection.

## 11. Test strategy

### Shared shell

Extend `SiriusModalShellTest` first with a real 640×360 `SubViewport` fixture containing tall body content and footer actions. Record the observed panel height, body page, and scrollbar max during implementation and require the shell contract from §4.3.

Run shared regressions for:

- `SiriusModalShellTest`;
- `SettingsMenuSceneTest` / `SettingsMenuControllerTest`;
- `PauseScreenControllerTest`;
- `PauseReturnToTitleConfirmationControllerTest`;
- `SiriusUiShowcaseResponsiveTest`.

### Save metadata

Extend `SaveManagerTest` for Empty/Valid/Corrupted/Incompatible and backup recovery. The incompatible test also asserts `PlayerName == null` while `IsCorrupted` remains true.

### Card + screen

Test the script-less card scene structure once, then test the screen for:

- four explicit component instances;
- Save/Load action matrix;
- valid Save emits `OverwriteRequested` without terminally closing;
- direct Save/Load terminal latch;
- autosave Save-disabled reason;
- corrupted/incompatible reasons;
- 2-column standard / 1-column compact;
- long reason wrapping;
- shell scroll range at 640×360;
- first-actionable/Cancel focus.

### Overwrite confirmation

Test its scene/controller independently for safe Cancel focus and one-shot confirm/cancel signals.

### Host integration

Update existing Main Menu, gameplay host, and input lifecycle fixtures. Configured keyboard/gamepad Cancel over overwrite must close the hosted confirmation first, then Save/Load, then Pause in normal stack order.

No native `AcceptDialog`/`ui_close_dialog` behavior remains part of Save/Load acceptance.

## 12. Cleanup blast radius

After both hosts migrate:

- delete `scripts/ui/SaveLoadDialog.cs`;
- delete `tests/ui/SaveLoadDialogTest.cs`;
- migrate `MainMenu`, `Game`, `GameplayPauseHostTest`, `GameInputLifecycleTest`, and Main Menu tests;
- update `SettingsManager.cs` and `SettingsManagerTest.cs` comments so they describe modal/UI action conflicts generically rather than naming the removed `SaveLoadDialog`;
- update `CLAUDE.md` architecture/file-list wording from `SaveLoadDialog` to `SaveLoadScreenController`;
- update only `MAIN-LOAD` and `PAUSE-SAVELOAD` lifecycle rows plus overwrite-child wording in `docs/ui/hpa-376/ui-lifecycle-contract.md`.

Final active-source expectation:

```bash
git grep -n "SaveLoadDialog\|ShowDialog" -- scripts scenes tests
```

returns no matches. Historical planning/baseline documents outside active source/tests may retain the old name when describing history.

## 13. Non-goals

HPA-384 does not add:

- Delete UI;
- cloud saves;
- extra slots;
- thumbnails;
- save renaming;
- playtime or fabricated metadata;
- automatic repair UI;
- save repository/facade;
- save view model;
- card controller or collection renderer;
- generic host factory;
- navigation/scene service;
- HPA-572 generic confirmation/error framework;
- Main Menu Continue selection changes.

## 14. Risks and gates

### Shared shell height behavior

Risk: Godot minimum-size propagation can make either an overgrown modal or a collapsed body if the wrong primitive is used.

Gate: Task 1 proves the 640×360 layout in `SiriusModalShellTest` before Save/Load authoring starts. Downstream tasks depend only on the tested shell contract.

### Confirmation ownership drift

Risk: keeping a native child dialog would duplicate host stack behavior and preserve desktop-dialog presentation.

Gate: overwrite is a real `UIScreenHost` child using existing `ConfirmOverwrite`; no Save/Load `InterceptCancel`, `HasActiveChildDialog`, or native subwindow remains.

### Legacy name drift

Risk: comments/guidance can keep stale `SaveLoadDialog` assumptions after code deletion.

Gate: explicit comment/guidance files are in Task 5, followed by active-source grep.

## 15. Acceptance mapping

| HPA-384 acceptance | Design response |
| --- | --- |
| No generic desktop-dialog presentation | Save/Load and overwrite confirmation are scene-authored `Control`s using `SiriusModalShell` |
| Manual/autosave distinct and readable | Four explicit instances of one script-less card structure |
| Preserve Save/Overwrite/Load/backup/transition | Existing Game/MainMenu/SaveManager ownership remains unchanged |
| Nested Cancel closes overwrite first | Overwrite is a host child of Save/Load; `UIScreenHost` owns topmost Cancel |
| Main Menu/Pause return and focus | Existing host parents/restoration retained; explicit initial focus added |
| No unsupported metadata or Delete | State-driven reasons; incompatible `PlayerName` is null; Delete omitted |
| Responsive minimum viewport | Shared shell height contract + Save/Load 1-column reflow and scroll assertions |
| Focused regressions | Shell, save domain, screen/card, confirmation, Main Menu, gameplay host/input lifecycle, full suite/build |

## 16. Decision summary

The revised HPA-384 implementation remains lean:

- one small shared fix to finish `SiriusModalShell`’s existing responsive contract;
- one additive metadata enum;
- one script-less repeated card scene;
- one Save/Load screen/controller;
- one tiny feature-specific hosted overwrite confirmation;
- two explicit host cutovers;
- removal of the legacy native dialog and stale active references.

The new files replace duplicated scene markup and native-dialog lifecycle seams; they do not add domain layers or speculative frameworks.
