# HPA-381 Compact Exploration HUD Design

**Date:** 2026-08-09  
**Status:** Revised after implementation-plan review  
**Linear:** HPA-381 — Replace the Sirius debug exploration HUD with a compact contextual HUD

## 1. Purpose

Replace the current debug-oriented exploration overlay with the approved compact Sirius exploration HUD without changing gameplay, character progression, interaction rules, or the shared UI architecture.

This is a presentation migration. `Game` remains the owner of gameplay/world context, `GameManager` remains the owner of player and interaction lifecycle state, and `UIScreenHost` remains the owner of gameplay presentation blocking. The new HUD gets a small scene/controller boundary so `Game` no longer owns concrete labels, bars, prompt construction, or responsive HUD layout.

## 2. Why HPA-381 is the next slice

The Sirius project delivery order starts with the compact exploration HUD. HPA-381 is High priority and Todo, and its listed blocker HPA-354 is complete. The shared Sirius Theme, art catalogue, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, and gameplay `UIScreenHost` are already merged, so this ticket can be implemented as a player-visible vertical slice without adding foundation work.

The current runtime still carries the prototype presentation in `Game.tscn`:

- a draggable `TopPanel`;
- the `Player HUD` developer title;
- the visible Lock toggle;
- a 96 px portrait plus oversized labels;
- raw ATK, DEF, and SPD diagnostics;
- permanent themed-area/control instructions; and
- a plain `InteractionPrompt` label created at runtime by `Game`.

`Game.cs` also binds each old HUD node directly and updates it from `PlayerStatsChanged`.

## 3. Approaches considered

### A. Dedicated scene-authored HUD with a small presentation controller — selected

Create one `ExplorationHud.tscn` and one `ExplorationHudController`. `Game` feeds player state, floor title, and resolved interaction prompts into that controller.

Benefits:

- removes presentation paths and layout logic from `Game`;
- allows the HUD to be tested independently at every supported viewport;
- reuses the existing Sirius Theme/components directly;
- keeps world-target validity decisions in `Game`, where they already live; and
- establishes a narrow production boundary without a new framework.

Cost: one scene and one controller are added.

### B. Restructure `Game.tscn` in place and keep all bindings in `Game.cs`

This has the fewest new files, but it leaves `Game` responsible for presentation details and responsive layout. It also keeps future HUD changes coupled to the already-large gameplay root controller.

Rejected because the small file-count saving is not worth preserving the current presentation coupling.

### C. Introduce a generic HUD presenter/view-model framework

A generic presentation model could support future battle, inventory, or other HUDs, but there is only one concrete consumer now and those later screens have materially different contracts.

Rejected under YAGNI. Generalize only if a second real consumer proves a shared API.

## 4. Goals

HPA-381 will:

1. Replace the debug `TopPanel` and permanent `Instructions` nodes with one scene-authored compact exploration HUD.
2. Reuse the existing Sirius Theme, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, UI ornaments, and current hero portrait asset.
3. Show player identity, level, HP, MP, and thin EXP progress.
4. Keep gold out of the exploration HUD, following the approved HPA-373 composition that reserves gold for inventory/shop surfaces.
5. Move the interaction prompt into the scene-authored HUD while preserving current target-validity rules.
6. Let the existing `interact` InputMap action drive the prompt glyph/label so remaps and active-device changes are reflected by the existing input-hint component while the prompt is actionable.
7. Show the current floor/area name briefly at the top centre when a floor loads.
8. Replace the permanent instruction block with one short, session-scoped movement hint; do not add tutorial persistence.
9. Keep the HUD safely positioned at all approved desktop landscape viewports, including the 1600 px centred ultrawide content frame.
10. Guarantee that the non-interactive HUD does not capture gameplay mouse/focus input.
11. Cut production over atomically: no committed intermediate state may remove the old prompt/HUD nodes while code or lifecycle tests still depend on them.

## 5. Non-goals

HPA-381 will not:

- add a minimap, objective system, quick-item bar, cooldown panel, or active-skill HUD;
- add tutorial completion or cross-session tutorial persistence;
- add new input actions or change movement/input semantics;
- change character stat formulas, experience progression, mana rules, or inventory rules;
- expose ATK, DEF, SPD, equipment bonuses, or detailed build data in exploration;
- display gold in exploration solely because HPA-381 originally allowed it conditionally; HPA-373's approved screen composition keeps it in inventory/shop;
- create another Theme, stat-bar component, prompt component, input-hint component, or host API;
- add motion infrastructure or the HPA-541 Reduced Motion setting;
- migrate battle, dialogue, puzzle, inventory, save/load, or error presentation;
- refactor unrelated `Game` behavior; or
- delete `DraggablePanel.cs` unless a separate repository-wide usage audit proves it is unused.

## 6. Ownership and interfaces

### 6.1 `Game` keeps gameplay/world decisions

`Game` continues to decide:

- which adjacent treasure/puzzle target is valid;
- when battle, NPC interaction, world interaction, or host presentation blocks exploration input;
- when a floor has loaded;
- when player state changed; and
- when scene navigation has committed.

`Game` does not create or style HUD controls after this migration.

### 6.2 `ExplorationHudController` owns presentation

The new controller owns only:

- binding authored HUD nodes;
- applying the supplied player display state;
- showing/hiding the interaction prompt;
- showing the temporary area title and movement hint;
- compact/standard responsive sizing; and
- making the entire HUD subtree non-interactive.

It does not query `GameManager`, `GridMap`, floor spawn groups, inventory, or save state.

### 6.3 Player display contract

Use one feature-local immutable value rather than eight positional parameters or a generic view-model layer:

```csharp
public readonly record struct ExplorationHudPlayerState(
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    int Experience,
    int ExperienceToNext);
```

`Game.UpdatePlayerUI()` remains as the existing call-site seam, but its implementation becomes a small adapter from `GameManager.Player` to `ExplorationHudPlayerState`. `Game` calculates the effective maximum health before passing the state. The HUD simply renders the supplied values.

MP is visible when `MaxMana > 0`; otherwise that row collapses. EXP collapses if there is no meaningful positive next-level maximum. If the portrait control has no texture, it collapses while name/level remain as the identity fallback.

### 6.4 HUD methods

The controller exposes only the operations that the current gameplay root needs:

```csharp
public void ApplyPlayerState(ExplorationHudPlayerState state);
public void ShowInteractionPrompt(string text, UiIconId icon);
public void HideInteractionPrompt();
public void ShowAreaTitle(string title);
public void ShowSessionHint(string text);
```

`ShowInteractionPrompt` always binds `SiriusContextPrompt.Actions` to the existing `interact` action. No new prompt abstraction or target model is introduced.

## 7. Scene structure and Theme ownership

Add `res://scenes/ui/ExplorationHud.tscn` and instance it once under `UI/GameUI` in `Game.tscn`.

The **root explicitly owns `theme = res://resources/ui/theme/SiriusTheme.tres`**. Shared components already carry the Theme in their own scenes, but free `Label`, `PanelContainer`, and `ProgressBar` nodes in this HUD must inherit the Sirius Theme from the feature root rather than falling back to Godot defaults.

```text
ExplorationHud                       # full-rect Control, SiriusTheme, controller attached
├── SafeFrame                        # centred max-width 1600 content frame
│   ├── HeroOrbitArc                 # existing quarter-orbit ornament
│   ├── HeroPlate                    # PanelContainer, SiriusHudPlate, top-left
│   │   └── HeroContent              # HBoxContainer
│   │       ├── Portrait             # existing hero portrait; collapses if absent
│   │       └── PlayerData           # VBoxContainer
│   │           ├── IdentityRow
│   │           │   ├── PlayerName
│   │           │   └── PlayerLevel
│   │           ├── HealthBar        # SiriusStatBar
│   │           ├── ManaBar          # SiriusStatBar
│   │           └── ExperienceRow
│   │               ├── ExperienceLabel
│   │               └── ExperienceBar # thin ProgressBar, SiriusExpBar
│   ├── AreaTitle                    # top-centre, hidden by default
│   ├── PromptPlate                  # bottom-centre, SiriusHudPlate, hidden by default
│   │   └── PromptContent            # VBoxContainer
│   │       ├── ContextPrompt        # SiriusContextPrompt
│   │       └── PromptConnector      # existing callout-connector ornament
│   └── HintPlate                    # small temporary top-right hint, hidden by default
│       └── HintLabel
├── AreaTitleTimer                   # one-shot
└── HintTimer                        # one-shot
```

The `PanelContainer` nodes each have one layout child. The orbit/connector are existing committed ornaments; no new art or fallback loader is added.

The timers are authored children rather than ad hoc async tasks. They only control temporary presentation lifetime.

No Theme token or new Theme resource is added.

## 8. Visual composition

### 8.1 Hero anchor

The top-left hero anchor uses the existing orbit ornament plus a compact HUD plate rather than another large rectangular workbench.

- Standard portrait target: 56 px.
- Compact portrait target: 40 px, matching HPA-373.
- Name and level sit together in the identity row.
- HP and MP reuse `SiriusStatBar` with Health/Mana kinds.
- EXP uses a short label plus a thin stock `ProgressBar` with `SiriusExpBar` so the HUD does not inherit the taller full `SiriusStatBar` header/state treatment for EXP.
- ATK, DEF, SPD, Gold, and equipment bonuses are absent.

The plate remains small enough that the world is the primary canvas.

### 8.2 Interaction prompt

The prompt is bottom-centre inside a compact HUD plate and uses `SiriusContextPrompt`.

Current prompt mapping remains intentionally narrow:

| Existing target | Text | Icon | Action hint |
|---|---|---|---|
| unopened treasure | `Open` | `UiIconId.Reward` | `interact` |
| unsolved puzzle switch | `Use` | `UiIconId.Puzzle` | `interact` |
| unsolved riddle | `Solve` | `UiIconId.Puzzle` | `interact` |

HPA-381 does not add NPC prompt behavior that the current interaction path does not already expose.

`SiriusInputHint` resolves the active keyboard/mouse/gamepad binding while the prompt is visible. `ShowInteractionPrompt` explicitly refreshes the component, so returning from Settings picks up a remapped `interact` binding before the player takes another action.

### 8.3 Area title

`OnFloorLoaded(FloorDefinition floorDef, ...)` calls `ShowAreaTitle(floorDef.FloorName)`. The label is top-centre, non-interactive, and hides after a short one-shot timer. No tween or new motion policy is introduced.

### 8.4 Session hint

The permanent instruction block is removed. On gameplay startup, `Game` asks the HUD to show one short session-scoped movement hint:

`Move with WASD or Arrow Keys`

This remains a fixed hint intentionally. Current movement is handled directly from WASD/arrow key events rather than through a movement InputMap action, so making this surface binding-aware would require a gameplay-input change outside HPA-381. The interaction prompt remains binding/device-aware through the existing `interact` action.

The hint automatically hides after a few seconds. It does not mention remappable Inventory/Pause bindings and therefore cannot become stale when Settings changes those controls.

No save flag, tutorial progression object, persistence change, or new movement action is added.

## 9. Prompt visibility and host integration

The interaction prompt must disappear whenever exploration input is incompatible.

`Game.UpdateInteractionPrompt()` keeps the existing world-target resolution but begins with this visibility gate:

- no valid HUD/grid/player/game manager;
- `_sceneChangeCommitted` is true; or
- `IsGameplayInputSuppressed()` is true.

`IsGameplayInputSuppressed()` already composes the gameplay `UIScreenHost` presentation block with battle, NPC interaction, and world interaction state.

The existing `GameplayInputBlockChanged` host callback is expanded only enough to refresh the interaction prompt after `_presentationGameplayBlocked` changes:

```csharp
GameplayInputBlockChanged = blocked =>
{
    _presentationGameplayBlocked = blocked;
    if (IsNodeReady())
        UpdateInteractionPrompt();
};
```

This ensures:

- opening Pause or direct Inventory hides a currently visible prompt;
- closing the blocking screen re-resolves and restores the prompt if the target is still valid;
- battle/NPC/world-interaction transitions continue to hide it through existing calls; and
- a committed scene transition cannot leave a stale prompt visible.

`RequestSceneChange()` hides/re-resolves the prompt immediately after setting `_sceneChangeCommitted = true` so navigation does not depend on a later incidental refresh.

The host continues to treat `UI/GameUI` as its HUD root. HPA-381 does not register the passive HUD as another host screen.

## 10. Responsive layout

Use the existing `SiriusUiMetrics` contract:

- compact when width < 800 or height < 450;
- safe margin 24 standard / 12 compact;
- supported viewports are `SiriusUiMetrics.VerificationViewports`;
- ultrawide content is centred with a maximum width of 1600 px.

`ExplorationHudController.RefreshLayout()` computes one `SafeFrame` rectangle from the viewport:

```text
contentWidth = min(viewportWidth - 2 * safeMargin, 1600)
sideInset = max(safeMargin, (viewportWidth - contentWidth) / 2)
verticalInset = safeMargin
```

The authored child anchors then position the hero anchor, area title, prompt, and hint relative to that frame. No per-aspect-ratio branches or duplicate layouts are created.

At compact size, the controller switches the shared components to compact typography, reduces the portrait to 40 px, shortens the hero/prompt footprint, and keeps the temporary hint narrow enough to avoid the hero and bottom-centre prompt. Missing optional MP/portrait data collapses without reserved empty regions.

## 11. Input policy

The exploration HUD has no interactive controls. During `_Ready()`, the controller recursively sets every `Control` in its subtree to:

- `MouseFilter = Ignore`; and
- `FocusMode = None`.

This includes internals of the instanced prompt and stat-bar components. `SiriusInputHint` may observe `_Input` to update its glyph, but it does not mark the event handled.

The HUD therefore never becomes a gameplay focus target and never blocks clicks or movement input.

## 12. Production cutover

The isolated HUD component may land as its own green commit. The production migration is one atomic second slice because `Game.tscn`, `Game.cs`, and existing lifecycle tests share the old prompt/HUD path.

The cutover changes together:

### `scenes/game/Game.tscn`

- remove the `DraggablePanel` ext-resource from this scene;
- remove the old `TopPanel` subtree;
- remove the permanent `Instructions` label;
- remove old local stylebox resources used only by that debug panel;
- instance `ExplorationHud.tscn` under `UI/GameUI`.

### `scripts/game/Game.cs`

- replace individual HUD label fields and runtime prompt label with `_explorationHud`;
- bind the authored HUD in `_Ready()`;
- simplify `UpdatePlayerUI()` into the `ExplorationHudPlayerState` adapter;
- convert `UpdateInteractionPrompt()` to HUD show/hide calls while retaining all existing finders and target-validity rules;
- refresh the prompt from `GameplayInputBlockChanged`;
- show the floor title in `OnFloorLoaded`;
- show the one-shot movement hint at gameplay startup;
- hide the prompt when scene navigation commits; and
- remove `EnsureInteractionPromptLabel()` and all old `TopPanel`/`InteractionPrompt` node paths in the same commit.

### Tests changed in the same slice

- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameplayPauseHostTest.cs`

No intermediate commit may delete `_interactionPromptLabel`, `EnsureInteractionPromptLabel()`, or `UI/GameUI/InteractionPrompt` while `UpdateInteractionPrompt()` or lifecycle tests still use them.

No `GameManager`, `Character`, `GridMap`, `PlayerController`, Theme, or host API change is required.

## 13. Testing strategy

### 13.1 HUD scene/controller tests

Add focused tests that prove:

- all required scene-authored HUD nodes exist before `_Ready()`;
- the root scene explicitly references `SiriusTheme.tres`;
- free labels/plates resolve Sirius Theme variations through that root;
- no debug title, Lock control, permanent Instructions, ATK, DEF, SPD, or Gold node is present;
- applying player state updates name, level, HP, MP, and EXP;
- `MaxMana <= 0` collapses the MP row;
- clearing the portrait texture still leaves a readable identity treatment;
- interaction prompt uses the correct text/icon/action and hides deterministically;
- area title and session hint become visible and their authored timers hide them;
- every HUD control is mouse-ignore/non-focusable after `_Ready()`; and
- every `SiriusUiMetrics.VerificationViewports` case keeps visible HUD surfaces inside the safe frame with non-zero sizes.

Use deeper assertions at 640×360 and 1280×720, and light fit assertions for the remaining verification viewports.

### 13.2 Gameplay integration tests

Update/add focused `GameTest` coverage for:

- `PlayerStatsChanged` updating the authored HUD, including mana;
- an adjacent treasure producing `Open` through `SiriusContextPrompt`;
- puzzle targets preserving `Use`/`Solve` semantics;
- opening a host-blocking screen hiding the prompt and closing it re-resolving the prompt for a still-valid target;
- floor load showing the current floor name; and
- production `Game.tscn` containing the new HUD and no prototype nodes.

Update existing `GameInputLifecycleTest` contracts that directly use `UI/GameUI/InteractionPrompt`:

- configured Cancel while a riddle owns the interaction;
- floor replacement/rebinding; and
- battle hide/escape restoration.

Those tests must assert `%PromptPlate`/`%ContextPrompt` state from the real resolver. They must not force a presentation node visible to simulate a valid target.

Keep HPA-382 host/Pause tests intact; add the host-block suppression assertion in `GameplayPauseHostTest` and keep real-target restoration acceptance-critical in `GameTest`/lifecycle coverage.

### 13.3 Final verification

Run:

- `ExplorationHudControllerTest`;
- `GameTest`;
- `GameInputLifecycleTest`;
- `GameplayPauseHostTest`;
- the full `Sirius.sln` test suite;
- `dotnet build Sirius.sln --no-restore`; and
- a diff/scope audit proving no Theme, `UIScreenHost`, domain-model, inventory, battle, save, or settings files changed unintentionally.

## 14. Risks and mitigations

### Prompt stays visible under Pause or another host screen

Mitigation: make prompt eligibility use `IsGameplayInputSuppressed()` and refresh on the host's `GameplayInputBlockChanged` callback, rather than relying only on battle/NPC/world state. Test both hide and real-target restoration.

### Production cutover creates a broken bridge state

Mitigation: keep component creation separate, then migrate scene, `Game`, and all old prompt-path tests atomically. There is no mid-cutover green checkpoint.

### Free HUD controls render with default Godot styling

Mitigation: explicitly assign `SiriusTheme.tres` to the HUD root and assert that authored Theme relationship in the component test.

### Reusable component internals consume mouse events

Mitigation: the HUD controller recursively forces its entire passive subtree to `MouseFilter.Ignore` and verifies this in a real scene test.

### Compact HUD becomes another oversized panel

Mitigation: lock the approved content list, use only name/level + HP/MP + thin EXP, omit Gold/build stats, and assert non-zero/contained layout at 640×360.

### Responsive logic proliferates per viewport

Mitigation: use one shared `IsCompact` branch plus the existing safe-margin/max-content-frame policy. The seven reference viewports are verification inputs, not seven implementations.

### HPA-381 expands into tutorial persistence or movement rebinding

Mitigation: keep the movement hint timer-scoped and fixed to the current direct WASD/arrow behavior. Persistence or movement-action refactoring requires a separate gameplay issue.

## 15. Acceptance mapping

| HPA-381 acceptance requirement | Design coverage |
|---|---|
| Remove draggable/debug HUD and permanent instructions | `Game.tscn` replaces `TopPanel`/`Instructions` entirely |
| Compact approved HUD, supported data only | Hero anchor shows identity, level, HP, MP, EXP; no Gold/future placeholders |
| HP, MP, EXP, level update | `ExplorationHudPlayerState` fed from existing `PlayerStatsChanged` path |
| Prompt appears/disappears deterministically | Existing target resolver + suppression/host gate + HUD show/hide methods |
| Prompt reflects bindings/device | Reuse `SiriusContextPrompt`/`SiriusInputHint` with the existing `interact` action |
| Hints temporary/non-obstructive | one-shot scene-authored movement hint timer; no persistence |
| Safe at approved aspect ratios | shared compact/safe-frame policy + seven viewport tests |
| HUD does not intercept gameplay input | recursive mouse-ignore/focus-none policy + test |
| Existing behavior/tests preserved | atomic cutover updates `GameTest`, `GameInputLifecycleTest`, and host coverage together |

## 16. Final scope decision

Implement the HUD as a small production screen component, not as a new UI subsystem. The only new runtime concepts are `ExplorationHudController` and its feature-local player display value. Everything else reuses already-merged Sirius infrastructure or existing `Game` lifecycle seams.