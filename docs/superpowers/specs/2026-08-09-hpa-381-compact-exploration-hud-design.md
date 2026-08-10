# HPA-381 Compact Exploration HUD Design

**Date:** 2026-08-09  
**Status:** Revised after implementation-plan review  
**Linear:** HPA-381 — Replace the Sirius debug exploration HUD with a compact contextual HUD

## 1. Purpose

Replace Sirius's debug-oriented exploration overlay with the approved compact exploration HUD without changing gameplay, character progression, interaction rules, or screen-host architecture.

This is a presentation migration. `Game` remains the owner of gameplay/world context and target validity, `GameManager` remains the owner of player and interaction lifecycle state, and `UIScreenHost` remains the owner of gameplay presentation blocking. The HUD gets one scene/controller boundary so `Game` no longer owns concrete HUD labels, bars, prompt construction, or responsive HUD layout.

## 2. Why HPA-381 is the next slice

The Sirius delivery order starts with the compact exploration HUD. HPA-381 is High/Todo and its foundation blocker HPA-354 is complete. The Sirius Theme, art catalogue, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, and gameplay `UIScreenHost` are already merged.

The production scene still contains:

- a draggable `TopPanel`;
- the `Player HUD` developer title;
- a visible Lock toggle;
- oversized player-stat presentation;
- raw ATK/DEF/SPD diagnostics;
- permanent controls/terrain instructions; and
- a plain `InteractionPrompt` label created by `Game` at runtime.

`Game.UpdatePlayerUI()` directly mutates all of those prototype nodes.

## 3. Chosen architecture

### 3.1 Dedicated scene-authored HUD

Add:

- `scenes/ui/ExplorationHud.tscn`;
- `scripts/ui/ExplorationHudController.cs`; and
- one feature-local `ExplorationHudPlayerState` value.

`Game` adapts existing domain state into that presentation API. The HUD does not query `GameManager`, `GridMap`, spawn groups, inventory, saves, or screen-host state.

This is preferred over keeping the HUD inline in `Game.tscn` because `Game` is already large and should not own responsive visual composition.

### 3.2 No generic HUD framework

Do not add a presenter/view-model framework, screen registry, or generic notification system. No second consumer currently proves those abstractions.

## 4. Product scope

HPA-381 will:

1. remove the debug `TopPanel`, Lock control, drag behavior, permanent instructions, ATK/DEF/SPD, and exploration Gold line;
2. show identity, level, HP, MP, and a thin EXP progress line;
3. preserve `Open` / `Use` / `Solve` interaction semantics;
4. make interaction hints binding/device-aware through the existing `interact` action;
5. show a brief floor/area title and one session-scoped movement hint through one transient message region;
6. keep the HUD passive and inside the approved safe frame at all verification viewports;
7. preserve player access to Gold by adding one minimal Gold readout to the existing Inventory screen before the exploration Gold line is removed; and
8. remove `DraggablePanel.cs` after the production cutover because the repository-wide search shows `Game.tscn` is its only consumer.

HPA-381 will not:

- add minimap, objectives, quick items, cooldowns, or active-skill HUD slots;
- redesign Inventory beyond the one Gold-preservation readout;
- add tutorial persistence;
- add movement InputMap actions;
- change stat formulas or progression;
- add Theme tokens;
- add a new stat-bar variant;
- add another host/modal observer; or
- migrate battle/dialogue/puzzle/save/error presentation.

## 5. Ownership and interfaces

### 5.1 `Game` keeps decisions

`Game` continues to decide:

- which adjacent treasure or puzzle target is valid;
- when battle/NPC/world interaction blocks exploration input;
- when `UIScreenHost` blocks gameplay input;
- when scene navigation has committed;
- when a floor loaded; and
- when player state changed.

### 5.2 `ExplorationHudController` owns presentation

The controller owns only:

- authored-node binding;
- player display state;
- responsive safe-frame layout;
- interaction prompt presentation;
- transient floor/hint presentation; and
- passive mouse/focus policy.

Its public presentation API is valid after `_Ready()`. This matches Godot's child-before-parent ready ordering: the authored HUD is ready before `Game._Ready()` binds and uses it.

### 5.3 Player display value

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

`Game.UpdatePlayerUI()` stays as the existing adapter seam. It supplies `GetEffectiveMaxHealth()` and raw `MaxMana`; Sirius currently has no effective-max-mana equivalent.

## 6. Scene structure

`ExplorationHud.tscn` is instanced once under `UI/GameUI`.

The root explicitly owns `res://resources/ui/theme/SiriusTheme.tres` so free labels, panels, and the stock EXP `ProgressBar` inherit the Sirius visual system.

```text
ExplorationHud                        # full-rect Control + SiriusTheme
├── SafeFrame                         # max-width + safe-margin frame
│   ├── HeroOrbitArc                  # existing orbit_arc.png
│   ├── HeroPlate                     # SiriusHudPlate, top-left
│   │   └── HeroContent
│   │       ├── Portrait              # AtlasTexture, first 96×96 hero frame
│   │       └── PlayerData
│   │           ├── IdentityRow
│   │           │   ├── PlayerName
│   │           │   └── PlayerLevel
│   │           ├── HealthBar         # SiriusStatBar, Health, label HP authored here
│   │           ├── ManaBar           # SiriusStatBar, Mana, label MP authored here
│   │           └── ExperienceBar     # thin stock ProgressBar, SiriusExpBar
│   ├── PromptPlate                   # SiriusHudPlate, bottom-centre
│   │   └── PromptContent
│   │       ├── ContextPrompt         # SiriusContextPrompt
│   │       └── PromptConnector       # static callout_connector.png
│   └── TransientPlate                # one temporary top-centre region
│       └── TransientLabel
└── TransientTimer                    # one-shot, ProcessMode.Always
```

There is one transient plate and one timer, not separate area-title and hint surfaces.

### 6.1 Portrait contract

The committed hero sprite sheet is 384×96 and contains four 96×96 frames. The HUD preserves the existing prototype's single-frame crop with an `AtlasTexture`:

```text
atlas = res://assets/sprites/characters/player_hero/sprite_sheet.png
region = Rect2(0, 0, 96, 96)
```

Do not assign the raw strip directly to `TextureRect`.

The portrait may still collapse if the texture is unavailable; identity remains readable through name/level. That fallback remains because HPA-381 explicitly requires graceful handling of missing optional portrait data.

## 7. Stat presentation

### 7.1 HP and MP

Reuse `SiriusStatBar` exactly as designed:

- `Kind = Health`, `Label = "HP"` authored in the scene;
- `Kind = Mana`, `Label = "MP"` authored in the scene;
- runtime only updates Current/Maximum and MP visibility.

Do not reassign static labels/kinds on every player-state refresh.

### 7.2 EXP deliberately stays a thin native bar

HPA-373 calls for a **thin EXP progress line**. `SiriusStatBar` is intentionally a richer fixed composite: it always renders its header/numeric value and state text. HPA-377 explicitly rejected configurable stat-value presentation until a proven need.

HPA-381 therefore uses the already-themed native `ProgressBar` variation `SiriusExpBar`, with no parallel EXP label/formatter/state abstraction:

- `MaxValue = ExperienceToNext` when positive;
- `Value = clamp(Experience, 0, ExperienceToNext)`; and
- hide the bar when `ExperienceToNext <= 0`.

This is a screen-specific thin progress indicator, not another reusable stat component. Do not add `ShowHeader`, `ShowState`, `Thin`, or similar API to `SiriusStatBar` in HPA-381.

## 8. Gold preservation

Removing `_playerGoldLabel` from exploration currently removes the only always-available Gold readout. Shop/Heal display Gold only when those NPC surfaces are open, and the current Inventory screen has no Gold readout.

To avoid that regression while still following HPA-373's exploration composition:

- add one unique `%GoldLabel` to the existing Inventory header;
- `InventoryMenuController.RefreshUI()` writes `Gold: {player.Gold}`;
- `OpenMenu()` already calls `RefreshUI()`, so no new signal/subscription is needed;
- add one focused inventory test proving a changed player Gold value appears on open.

This is preservation work only. HPA-357 still owns the real Inventory redesign.

## 9. Interaction prompt

Current mappings remain:

| Target | Text | Icon | Action |
| --- | --- | --- | --- |
| unopened treasure | `Open` | `UiIconId.Reward` | `interact` |
| unsolved puzzle switch | `Use` | `UiIconId.Puzzle` | `interact` |
| unsolved riddle | `Solve` | `UiIconId.Puzzle` | `interact` |

`Game.UpdateInteractionPrompt()` keeps all target lookup and begins with this gate:

- HUD/grid/player/game manager unavailable;
- `_sceneChangeCommitted`; or
- `IsGameplayInputSuppressed()`.

The existing `GameplayInputBlockChanged` callback updates `_presentationGameplayBlocked` and calls `UpdateInteractionPrompt()` after `Game` is ready. This is necessary because Pause deliberately keeps `UI/GameUI` visible while still blocking gameplay input.

Opening Pause/direct Inventory therefore hides the prompt; closing them re-resolves the target and restores it only when still valid.

### 9.1 Prompt refresh efficiency

`SiriusContextPrompt` property setters already refresh when values change. Avoid blindly assigning all properties on every player movement:

- set `Actions = [interact]` once in HUD `_Ready()`;
- in `ShowInteractionPrompt`, only assign `Prompt`, `ShowIcon`, or `IconId` when that value changed;
- call `Refresh()` once at the end even when semantic values are unchanged so a remapped binding from Settings is picked up immediately.

Do not early-return before that final refresh.

### 9.2 Connector scope

HPA-373 describes a short coordinate-lock line tying the prompt to its target. HPA-381 uses the existing static `callout_connector.png` as a visual anchor only. It does **not** add world-to-HUD target projection/tracking; there is no existing target-anchor contract to reuse, and creating one is outside this slice.

## 10. Transient floor title and session hint

Use one transient region with explicit precedence.

Public calls remain:

```csharp
ShowAreaTitle(string title);
ShowSessionHint(string text);
```

Behavior:

1. area title displays for 2 seconds;
2. session hint displays for 4 seconds;
3. if a session hint is requested while the area title is visible, keep one pending hint and show it immediately after the title expires;
4. if an area title arrives while the session hint is visible, the area title preempts it and the interrupted hint becomes the one pending hint;
5. only one transient message is visible at a time; and
6. the one timer uses `ProcessMode.Always`, so Pause does not freeze a half-expired transient message under its scrim.

At initial game load this deterministically produces: floor title first, movement hint second.

The movement hint remains the fixed current-control statement:

`Move with WASD or Arrow Keys`

Movement is currently handled directly from key events rather than an InputMap action; HPA-381 does not introduce movement actions merely to make this one hint remappable.

## 11. Shared safe-frame metric

HPA-377's approved design already defines the ultrawide content maximum as 1600 px, but the implementation currently duplicates it privately in `SiriusUiShowcase` and its responsive test.

HPA-381 is the second real consumer, so promote the existing policy to:

```csharp
public const float MaximumContentWidth = 1600f;
```

in `SiriusUiMetrics`.

Then:

- `SiriusUiShowcase` uses `SiriusUiMetrics.MaximumContentWidth`;
- `SiriusUiShowcaseResponsiveTest` asserts against the shared value; and
- `ExplorationHudController` uses the same value.

No other metrics move.

## 12. Responsive layout

Use only:

- `SiriusUiMetrics.IsCompact(viewportSize)`;
- `SiriusUiMetrics.SafeMargin(compact)`; and
- `SiriusUiMetrics.MaximumContentWidth`.

```text
availableWidth = max(0, viewportWidth - 2 * safeMargin)
contentWidth = min(availableWidth, MaximumContentWidth)
sideInset = max(safeMargin, (viewportWidth - contentWidth) / 2)
```

The authored child anchors position hero, prompt, and transient region relative to `SafeFrame`. There are no viewport-specific layouts.

At compact size:

- portrait minimum becomes 40×40;
- stat/prompt typography uses existing compact modes;
- the hero plate remains small enough to leave the world as the primary canvas; and
- visible hero/prompt/transient surfaces do not overlap at 640×360.

## 13. Passive input policy

The HUD has no interactive controls. `_Ready()` recursively sets each `Control` to:

- `MouseFilter = Ignore`;
- `FocusMode = None`.

`SiriusInputHint` may observe `_Input` for active-device changes but does not mark events handled.

## 14. Atomic production cutover

Task 1 may land the isolated HUD + shared metric update as a green commit. The production migration is one atomic second slice.

The second slice changes together:

- `Game.tscn`: remove prototype HUD/instructions, instance `ExplorationHud`;
- `Game.cs`: replace old HUD/prompt fields and paths, adapt player state, convert prompt calls, host refresh, floor transient, startup hint, scene-transition prompt gate;
- `InventoryMenu.tscn` / `InventoryMenuController.cs`: add the minimal Gold readout;
- `GameTest`, `GameInputLifecycleTest`, `GameplayPauseHostTest`, `InventoryMenuControllerTest`: migrate/extend affected behavior;
- delete `scripts/ui/DraggablePanel.cs` after `Game.tscn` stops referencing it.

No intermediate commit may delete the prototype prompt/HUD path while production code or lifecycle tests still reference it.

## 15. Testing strategy

### HUD/component

Prove:

- required authored nodes exist before `_Ready()`;
- root uses `SiriusTheme.tres`;
- portrait is an `AtlasTexture` backed by the hero sheet with `Region == Rect2(0, 0, 96, 96)`;
- name/level, HP, MP, and thin EXP bind correctly;
- missing MP collapses;
- missing portrait collapses while identity stays readable;
- prompt mapping/action is correct;
- one transient region honors floor-title-before-hint precedence;
- transient timer continues while paused;
- all HUD controls are passive; and
- all shared verification viewports fit the safe frame, with deep checks at 640×360 and 1280×720.

Do **not** assert that a brand-new HUD scene lacks legacy node names; the production `Game.tscn` cutover and final stale-path audit are the meaningful checks.

### Production/lifecycle

Prove:

- `Game.tscn` has `ExplorationHud` and lacks the full prototype subtree;
- player stats refresh through `PlayerStatsChanged`;
- `Open`/`Use`/`Solve` behavior is preserved;
- battle/NPC/world states suppress prompts;
- Pause hides a real valid prompt and Resume re-resolves it;
- floor replacement rebinds prompt state rather than forcing UI visibility;
- floor title queues the startup hint correctly;
- Inventory shows current Gold when opened; and
- `DraggablePanel` no longer exists after its only consumer is removed.

## 16. Risks and mitigations

- **Stale prompt under Pause:** reuse `GameplayInputBlockChanged` + real resolver restoration.
- **Default-Godot styling on free controls:** Theme explicitly assigned to HUD root.
- **Sprite strip shown as portrait:** scene/test require 96×96 `AtlasTexture` region.
- **Transient overlap:** one region + one timer + area-title precedence.
- **Gold becomes inaccessible:** minimal Inventory readout lands in the same cutover.
- **Responsive policy drifts:** one shared `MaximumContentWidth` in `SiriusUiMetrics`.
- **Dead prototype utility lingers:** delete `DraggablePanel.cs` after repository-wide usage audit proved no second consumer.

## 17. Final scope decision

Keep the architecture small: one HUD scene, one HUD controller, one display value, one shared metric correction, one transient surface, and one narrow Inventory Gold preservation seam. Do not turn HPA-381 into an Inventory redesign or a shared-component redesign.
