# HPA-384 Save/Load Responsive Cards Design

**Status:** Implementation-ready design
**Linear:** HPA-384 — Redesign Sirius save/load flows with responsive save cards
**Target:** `main` after HPA-380, HPA-381, HPA-382, and HPA-383

## 1. Goal

Replace the runtime-built `SaveLoadDialog : AcceptDialog` presentation with one scene-authored Sirius Save/Load screen while preserving the save domain and lifecycle behavior already shipped.

This is a presentation migration. It does not redesign persistence, add save features, or introduce a generic save-navigation layer.

## 2. Current-state findings

The current flow is concentrated in a small number of seams:

- `scripts/ui/SaveLoadDialog.cs` builds all four slot buttons, labels, footer actions, and overwrite confirmation at runtime.
- `SaveManager` already owns the complete persistence path: three manual slots, autosave, metadata reads, version checks, atomic temp-file replacement, `.bak` recovery, and full load.
- `MainMenu` already presents Load through its local `UIScreenHost` and owns the `PendingLoadData` handoff and scene transition.
- `Game` already presents Save/Load as a logical Pause child and owns gameplay save eligibility, `CollectSaveData`, persistence calls, load handoff, errors, and scene transitions.
- HPA-382 already established parent/child host policy and child-first Cancel behavior. HPA-380 already established the Main Menu host path.
- The HPA-373 wireframe uses cards, a 2×2 desktop composition, and a stacked compact composition. Its old Delete action is superseded by HPA-384, which explicitly excludes Delete.

The migration should therefore replace the concrete view, not move responsibilities between systems.

## 3. Approaches considered

### A. One scene-authored Save/Load screen with a local controller — chosen

Create `SaveLoadScreen.tscn` and `SaveLoadScreenController.cs`. Author the four slot cards explicitly in the scene, reuse `SiriusModalShell`, and keep the existing explicit Main Menu and Game host call sites.

Benefits:

- smallest production cutover;
- preserves current ownership boundaries;
- gives Godot editor visibility into the real layout;
- supports responsive card reflow and focused layout tests;
- avoids a reusable component whose only consumer would be Save/Load.

### B. Keep `SaveLoadDialog` and change its internals/base type

This would reduce renames but leave a misleading `Dialog` abstraction after the screen becomes a scene-authored `Control`. It also makes it easier to accidentally preserve `AcceptDialog` assumptions in host and tests.

Rejected because the migration is a clean breaking change with no external consumer requiring the old type.

### C. Add reusable save-card components, a save view model, or a SaveManager facade

This would create more interfaces than the feature needs. There is one Save/Load screen, four fixed slots, and an already-sufficient domain API.

Rejected under YAGNI. Extract only if another real screen later needs the same component or presentation model.

## 4. Architecture

### 4.1 New scene and controller

Create:

- `scenes/ui/SaveLoadScreen.tscn`
- `scripts/ui/SaveLoadScreenController.cs`

The root is a full-rect `Control` using the existing Sirius Theme. It contains:

1. a `SiriusScrim` background;
2. one `SiriusModalShell`, size class `Large`;
3. one `GridContainer` inside `ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost`;
4. four explicitly authored slot-card `Button`s with child labels;
5. footer actions in the shell `ActionsHost`.

Do not add `SaveSlotCard.tscn`, a card controller, or a generic collection renderer. Four explicit cards are easier to inspect and are the complete domain cardinality.

### 4.2 Public controller contract

Use a small presentation contract:

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
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void MainMenuRequestedEventHandler();

    public SaveLoadMode Mode { get; set; }
    public Control InitialFocusTarget { get; }
    public bool HasActiveChildDialog { get; }

    public void DismissActiveChildDialog();
}
```

Each host creates a fresh screen instance for one presentation, sets `Mode` before `UIScreenHost.TryPresent`, and lets `_Ready()` bind the scene, read metadata, configure the mode, and lay out the cards. There is no reusable `Open()`/reset lifecycle because the host already uses `NodeLifetime = QueueFree`.

### 4.3 Ownership boundary

`SaveLoadScreenController` owns only:

- reading current `SaveSlotInfo` for presentation;
- formatting slot identity, state, player/level/floor, and timestamp;
- deciding whether a card is actionable for the current mode;
- opening/canceling the local overwrite confirmation;
- terminal/double-activation guarding;
- emitting Save, Load, Close, or Main Menu intent.

It does not:

- call `SaveGame`, `LoadGame`, `LoadAutosave`, or `CollectSaveData`;
- write `PendingLoadData`;
- change scenes;
- decide whether gameplay is currently allowed to save;
- recover, migrate, delete, rename, or repair save files.

Those responsibilities remain in `SaveManager`, `MainMenu`, and `Game` exactly where they are today.

## 5. Save metadata state

HPA-384 must visually distinguish an unreadable/corrupted file from a newer-version/incompatible file. `SaveSlotInfo` currently collapses both into `IsCorrupted` and, for a newer file, stores a descriptive string in `PlayerName`.

Add one small presentation-facing classification to the existing metadata object:

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
    public SaveSlotState State { get; set; }
    // Existing fields remain the current source for Continue and save behavior.
}
```

`SaveManager.GetSaveSlotInfo` / `ExtractMetadataFromFile` sets:

- no primary or backup: `Empty`;
- valid supported metadata: `Valid`;
- invalid JSON, missing required metadata, or unreadable format: `Corrupted`;
- file version greater than `SaveData.CurrentVersion`: `Incompatible`.

Keep `Exists` and `IsCorrupted` semantics unchanged for the already-shipped Main Menu Continue policy. `State` is the explicit UI classification HPA-384 needs; it does not become a new persistence protocol.

Do not add a `RecoveredFromBackup` state. Backup restoration remains a transparent `SaveManager` behavior. If recovery succeeds, the card displays the recovered metadata as a normal valid save.

## 6. Card behavior

Cards are the primary actions. There is no separate selection model or bottom `Load`/`Save` command state.

Each card shows only supported metadata:

- slot identity (`Slot 1`…`Slot 3`, `Autosave`);
- player name for a valid save;
- level;
- floor/location through `GetFloorName()`;
- local display timestamp when present;
- explicit slot state/action text.

The complete action matrix is:

| Slot state | Save mode, manual 0–2 | Save mode, autosave 3 | Load mode |
| --- | --- | --- | --- |
| Empty | Enabled — `Save` | Disabled — `Autosave is created automatically` | Disabled — `No save data to load` |
| Valid | Enabled — `Overwrite` → child confirmation | Disabled — `Autosave is created automatically` | Enabled — `Load` |
| Corrupted | Enabled — `Save` | Disabled — `Autosave is created automatically` | Disabled — `File cannot be read` |
| Incompatible | Enabled — `Save` | Disabled — `Autosave is created automatically` | Disabled — `Requires a newer game version` |

This preserves the current behavior that a manual corrupted/incompatible slot may be replaced directly, while a valid manual slot requires overwrite confirmation.

If `SaveManager` is unavailable, all cards are disabled with `Save system unavailable`, and Cancel becomes the initial focus target.

### Why cards directly activate

The old wireframe included a separate selected record and footer Load/Delete actions. HPA-384 explicitly removes Delete, and the current shipped dialog directly activates a slot. Keeping each card as the action preserves behavior and avoids introducing a second selection state only to press another button.

Keyboard/gamepad focus supplies the approved focused-card visual treatment. No persistent “selected card” state is added.

## 7. Overwrite confirmation

A valid manual save in Save mode opens exactly one child overwrite confirmation.

For HPA-384, keep this confirmation local to the Save/Load screen and theme the existing `AcceptDialog` with `SiriusTheme.tres`. Do not build HPA-572’s shared confirmation/warning/error framework early.

Rules:

- while the child is active, parent card activation is ignored;
- Cancel closes the overwrite child first and leaves Save/Load open;
- confirmation emits exactly one `SaveSlotSelected(slot)` terminal intent;
- cancellation clears the pending slot and restores focus to the invoking card;
- there is never a third actionable layer.

`HasActiveChildDialog` and `DismissActiveChildDialog()` remain the narrow seam used by the host’s existing Cancel interception.

## 8. Terminal and double-activation policy

The screen keeps one `_terminalEmitted` latch.

Before emitting any terminal Save, Load, Close, or Main Menu intent, the controller sets the latch and disables all card/footer actions. Subsequent button presses, duplicate confirmation signals, or close signals are ignored.

This is enough to prevent double activation because the actual save/load/scene work is synchronous at the current caller seams and the host frees the screen after terminal handling. Do not add async operation IDs, cancellation tokens, queues, or a save transaction service.

## 9. Host integration

### 9.1 Main Menu

`MainMenu.TryOpenHostedLoad` continues to own the Main Menu Load entry.

Change only the concrete view:

- instantiate `res://scenes/ui/SaveLoadScreen.tscn`;
- set `Mode = SaveLoadMode.Load` before presentation;
- wire `LoadSlotSelected` and `Closed`;
- keep `UIScreenKinds.SaveLoad`, modal layer/priority, no tree pause, visible cursor, inherited HUD, `VisibleInert` lower layers, and `NodeLifetime.QueueFree`;
- keep child-first Cancel interception;
- add `InitialFocus = () => screen.InitialFocusTarget`;
- keep the existing `RestoreFocus` target, including Continue’s failed-load fallback;
- keep `OnHostedLoadSlotSelected`, `LoadSlot`, `PendingLoadData`, deferred load-failure message, and teardown-safe scene transition unchanged.

If the screen has no actionable Load card, `InitialFocusTarget` returns Cancel. This preserves the HPA-380 fallback where a failed Continue may open Load even if no row can complete a load.

### 9.2 Gameplay Pause

`Game.TryOpenHostedSaveLoad` continues to own Save/Load under Pause.

Change only the concrete view:

- instantiate the same scene;
- set Save or Load mode before presentation;
- wire the four screen intent signals;
- keep the active Pause handle as logical parent;
- keep `ProcessPolicy.Always`, `PauseTree = false`, no additional gameplay block, inherited HUD, visible cursor, child-first Cancel, and `NodeLifetime.QueueFree`;
- add `InitialFocus = () => screen.InitialFocusTarget`.

`Game` keeps all save eligibility checks, save-data collection, SaveManager calls, errors, load handoff, Return to Main Menu, and teardown-safe scene changes.

No generic host factory is introduced. The two explicit call sites differ in parent, focus restoration, available signals, and domain handling, so sharing them would obscure rather than remove meaningful behavior.

## 10. Responsive layout

Reuse `SiriusUiMetrics` rather than introducing Save/Load-specific breakpoint constants.

### Standard

At non-compact viewports:

- `SiriusModalShell.SizeClass = Large`;
- cards use a 2×2 grid;
- metadata uses standard Section/Body/Metadata theme roles;
- each card keeps at least the standard 44 px interaction target and enough vertical room to show state and metadata without overlap.

### Compact

When `SiriusUiMetrics.IsCompact(viewportSize)` is true:

- shell uses compact presentation;
- cards become one column;
- card metadata uses compact typography;
- optional timestamp moves to the least prominent line and may be hidden only when the card would otherwise clip essential state/action text;
- the shell body scrolls while title and footer remain fixed;
- actions retain the 40 px compact minimum target.

The screen locally bounds the modal panel height to the viewport/safe margin so `BodyScroll` becomes the overflow owner. Do not modify `SiriusModalShell` globally; HPA-384 is one concrete consumer and the shell’s shared width contract remains unchanged.

Validate all `SiriusUiMetrics.VerificationViewports`, with deep perceptual/layout assertions at 640×360 and 1280×720 plus one long disabled-reason case.

## 11. Focus and input

- Initial focus is the first enabled slot in slot order.
- If no slot is enabled, initial focus is Cancel.
- Main Menu host restores the invoking Load/Continue control.
- Pause child closure restores the existing Pause entry and its invoking action through `UIScreenHost`.
- Mouse, keyboard, and gamepad all activate the same card buttons.
- The screen does not process global Cancel itself; `UIScreenHost` remains the owner, reserving the active overwrite child first.

No cross-session or cross-opening “last selected slot” memory is added. The approved “last valid slot” behavior is satisfied within a live screen by normal Godot focus; every new host presentation starts from the first actionable card.

## 12. Tests

### Save domain metadata

Extend `SaveManagerTest` to lock:

- empty → `SaveSlotState.Empty`;
- valid → `Valid`;
- invalid JSON → `Corrupted`;
- future version → `Incompatible`;
- missing primary with usable backup still resolves to the recovered state/metadata.

Keep existing atomic write/load/backup tests as the domain regression gate.

### Screen controller

Replace `SaveLoadDialogTest` with scene-backed controller coverage for:

- Save vs Load mode text/action availability;
- manual empty Save;
- valid Save → one overwrite child → confirm;
- overwrite Cancel keeps parent active and restores focus;
- manual Load and autosave Load;
- corrupted and incompatible display/reasons;
- autosave disabled in Save mode;
- no-manager state;
- Close and Main Menu terminal signals;
- repeated card press/terminal signal emits once.

### Responsive scene

Add a focused Save/Load scene test that mounts the production `.tscn` in a `SubViewport`, resizes through the shared verification viewports, and verifies:

- standard 2-column vs compact 1-column card layout;
- modal remains inside viewport bounds;
- cards have non-zero size and minimum targets;
- title/footer remain visible at 640×360;
- body is scrollable rather than clipped when content exceeds compact height;
- long status/reason text wraps and remains inside its card.

### Host integration

Update existing Main Menu and gameplay-host tests instead of creating a parallel integration harness.

Lock:

- Main Menu Load uses the scene-authored Save/Load screen and restores Load focus;
- failed Continue can host the new Load screen with safe Cancel focus;
- Pause Save and Load remain logical children of the same Pause handle;
- overwrite Cancel closes only the child;
- Save/Load close returns to the same Pause;
- manual and autosave load still set `PendingLoadData` and use the current scene-transition path;
- save failures/load failures still close Save/Load before the current error path;
- double activation cannot produce duplicate host/domain actions.

## 13. Lifecycle documentation and cleanup

After both hosts are migrated:

- delete `scripts/ui/SaveLoadDialog.cs`;
- delete/replace `tests/ui/SaveLoadDialogTest.cs`;
- update the `MAIN-LOAD` and `PAUSE-SAVELOAD` rows in `docs/ui/hpa-376/ui-lifecycle-contract.md` so they describe the scene-authored `Control` hosted on the modal layer rather than the old `AcceptDialog`/Window presentation;
- search for stale `SaveLoadDialog`, `ShowDialog`, and legacy Window-specific Save/Load assumptions.

Do not update HPA-373’s historical wireframe to reintroduce Delete. HPA-384 is the later concrete product decision.

## 14. Non-goals

HPA-384 does not add:

- Delete UI;
- cloud saves;
- extra slots;
- thumbnails;
- save renaming;
- playtime or fabricated metadata;
- automatic repair UI;
- a save repository/facade;
- a generic save-card component;
- a generic host factory;
- a navigation/scene service;
- HPA-572’s shared confirmation/error framework;
- Main Menu Continue selection changes.

## 15. Acceptance mapping

| HPA-384 acceptance | Design response |
| --- | --- |
| No generic desktop-dialog presentation | Scene-authored themed `SaveLoadScreen.tscn` under `SiriusModalShell` |
| Manual/autosave readable | Four explicit cards with slot identity/state and autosave action rules |
| Preserve Save/Overwrite/Load/backup/transition | Existing Game/MainMenu/SaveManager ownership remains unchanged |
| Nested Cancel closes overwrite first | Existing host interception uses controller child seam |
| Main Menu/Pause return and focus | Existing host parents/restoration retained; explicit initial focus added |
| No unsupported metadata or Delete | Only current `SaveSlotInfo` metadata; Delete omitted despite old wireframe |
| Focused tests pass | Domain-state, scene/controller, existing host integration, full suite/build |

## 16. Decision summary

HPA-384 should be a narrow vertical UI migration:

- one new scene;
- one new controller;
- one small metadata-state classification;
- two explicit host cutovers;
- focused test migration;
- removal of the old runtime-built dialog.

That is enough to deliver the approved player-facing improvement without creating new persistence, navigation, component, or confirmation frameworks.