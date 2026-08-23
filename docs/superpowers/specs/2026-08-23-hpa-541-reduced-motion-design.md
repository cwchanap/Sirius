# HPA-541 Reduced Motion Preference Design

**Status:** Planning candidate
**Linear:** HPA-541 — Add persisted Reduced Motion preference and bind Sirius UI motion policy
**Date:** 2026-08-23

## 1. Decision

Implement HPA-541 now that the deferred evidence gate is satisfied, but keep the solution deliberately local.

Add one persisted `ReducedMotionEnabled` boolean to the existing settings model, expose it as one checkbox on the existing Display settings page, and inject its current value into the one proven production UI motion owner: `BattleManager`.

Do not add a motion service, global policy object, event bus, shared-component lifecycle API, or a new Settings page. `SettingsManager` remains the persistence owner; `Game` remains the Battle presentation/composition owner; `BattleManager` remains the owner of Battle-specific animation choices.

This branch is the **single HPA-541 PR**. It starts with planning documents and should receive the implementation commits after plan review rather than opening a second PR.

## 2. Why HPA-541 is actionable now

HPA-541 was intentionally deferred until production UI contained real motion worth reducing. That condition is now true.

The current production-code survey finds `CreateTween()` / `TweenProperty(...)` only in:

- `scripts/ui/BattleManager.cs` — production Battle presentation;
- `scripts/ui/showcase/SiriusUiShowcase.cs` — development showcase only.

`BattleManager` currently owns four relevant motion effects:

1. damage labels translate upward 30 px over 1 second;
2. damage labels fade to transparent over the same 1 second;
3. attack feedback scales the acting sprite to 1.2× and back;
4. attack feedback flashes the sprite white and back.

It also constructs looping 4 FPS `AnimatedSprite2D` idle animations for the player and enemy during Battle.

This is enough real production motion to justify the preference. The showcase is not a player-facing production flow and is out of scope.

## 3. Persistence contract

Extend `SettingsData` with:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

The default is `false`.

`Clone()` and `SettingsManager.Sanitize(...)` must copy the value. JSON serialization then persists it through the existing atomic settings path.

### No settings-version migration

Do **not** bump `SettingsData.CurrentVersion` and do not add a migration table.

An older development `settings.json` simply lacks `ReducedMotionEnabled`; `System.Text.Json` leaves the boolean at its default `false`, and `Sanitize(...)` carries that value forward. That is the entire compatibility requirement for this development-only setting.

Corrupt-file, backup, window, audio, input, and autosave recovery behavior remains unchanged.

## 4. Settings UI

Put one authored row on the existing **Display** page:

```text
Reduced Motion    [ ]
```

Use:

- `%ReducedMotionLabel`
- `%ReducedMotionCheck`

The row follows the existing `DisplayRows` responsive grid and page-local scrolling. No Accessibility page, custom component, tooltip framework, or theme token is needed.

`SettingsMenuController` follows the existing staged-edit contract:

- `OpenSettings(...)` populates the checkbox from the cloned snapshot;
- toggling the checkbox mutates only control state until Apply;
- Cancel discards the staged value with the rest of the screen;
- Apply includes the checkbox value in the new `SettingsData` candidate passed to `SettingsManager.ApplyAndSave(...)`.

The existing Apply/Cancel and focus ownership stays unchanged.

## 5. Propagation ownership

`BattleManager` must not read `SettingsManager` directly.

`Game`, which already instantiates and starts Battle, reads the current snapshot when opening each Battle and passes the scalar preference into `BattleManager` before motion starts.

Prefer making the setting part of Battle startup rather than introducing mutable global state:

```csharp
public void StartBattle(
    Character player,
    Enemy enemy,
    bool reducedMotionEnabled)
```

`Game` supplies:

```csharp
var reducedMotionEnabled =
    SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled ?? false;

battle.StartBattle(
    _gameManager.Player,
    enemy,
    reducedMotionEnabled);
```

This keeps the dependency direction explicit:

```text
SettingsManager -> snapshot -> Game -> bool -> BattleManager
```

There is no shared `MotionPolicy` object and no subscriber lifecycle to manage.

### Why Main Menu needs no production edit

Main Menu contains no proven production motion owner. Its Settings screen already writes through the shared `SettingsManager`, so a preference applied at Main Menu is automatically visible to `Game` when a later Battle opens.

Adding a Main Menu cache, signal, or no-op motion property solely to satisfy the original broad wording would create dead state. If a future Main Menu animation is added, that concrete consumer can receive the preference then.

## 6. Battle reduced-motion behavior

`BattleManager` stores the startup value for the lifetime of that Battle instance.

Reduced motion changes presentation only.

### Damage numbers

Normal mode remains unchanged:

- show the damage number;
- translate it upward 30 px over 1 second;
- fade it to transparent over 1 second;
- reset it at the same 1-second completion point.

Reduced mode:

- show the damage number at its resting position;
- **do not translate it**;
- keep the existing 1-second opacity fade;
- reset/hide it at the same 1-second completion point.

Opacity feedback is intentionally retained because it communicates transient feedback without spatial motion and preserves existing completion timing.

### Attack feedback

Normal mode keeps the current scale + flash tween.

Reduced mode:

- do not scale the sprite;
- do not flash the sprite;
- leave scale and modulation at their resting values;
- do not create a replacement delay or tween just to simulate the removed animation.

Battle action cadence is already controlled independently by `_battleTimer`; removing this cosmetic tween must not change turn timing.

### Idle sprite loops

Normal mode keeps the current looping 4 FPS player/enemy idle animation.

Reduced mode:

- build/load the same sprite frames;
- present a deterministic static idle frame;
- do not start the looping idle animation.

This preserves actor identity and art while removing continuous motion.

### Motion that remains

Do not treat semantic state changes as decorative motion. Reduced Motion does **not** change:

- HP/MP/stat bar values;
- automatic-action progress values;
- Battle event/feed text;
- Battle timer wait time or action-point simulation;
- preparation, Cure, Results, focus, Cancel, host, or input behavior;
- reward application or Battle completion timing.

The setting is not a “freeze the UI” switch.

## 7. Current-value and lifecycle semantics

Every newly opened Battle reads the latest persisted in-memory settings snapshot through `Game`.

That covers both current Settings entry points:

- Apply Reduced Motion from Main Menu -> start Game -> later Battle gets the new value;
- Apply Reduced Motion from gameplay Pause -> resume -> later Battle gets the new value.

A normal production Battle and Settings screen cannot be active concurrently: Battle blocks the gameplay/root path that opens Pause/Settings. Therefore HPA-541 does **not** add a live settings-changed event for an already-active Battle.

If a future flow allows Settings to overlap a motion-owning screen, that concrete flow can decide whether live rebinding is required. Do not build that protocol now.

## 8. Failure/default behavior

If `SettingsManager.Instance` is unavailable while Game opens Battle, fall back to `false` and preserve current motion.

This feature must never prevent Battle from opening.

An older settings JSON without the field also resolves to `false`.

No warning, repair popup, or compatibility branch is required for either case.

## 9. Test strategy

Use existing suites and private/reflection helpers; do not widen production visibility for tests.

### Settings data/persistence

`SettingsDataTest` and `SettingsManagerTest` cover:

- default `false`;
- clone preservation;
- Apply/save/reload preserving `true`;
- a valid older JSON without `ReducedMotionEnabled` loading as `false`.

### Settings screen

`SettingsMenuControllerTest` and `SettingsMenuSceneTest` cover:

- authored reduced-motion nodes exist before `_Ready()`;
- `OpenSettings(...)` populates the checkbox;
- Cancel does not mutate the source snapshot;
- Apply persists the staged checkbox value;
- existing standard/compact responsive checks still pass with the extra row.

### Battle motion

`BattleManagerTest` covers representative normal/reduced behavior:

- reduced attack feedback leaves scale/modulation unchanged and creates no attack tween;
- reduced damage feedback does not change position but still fades/resets on the existing 1-second schedule;
- reduced Battle startup leaves player/enemy idle sprites static;
- normal mode keeps the existing tween/idle behavior;
- `StopBattleRuntime()` still kills any tracked visual tweens and resets visual state.

### Root propagation

Extend the existing real-Game Battle-start coverage so a Battle opened while the current settings snapshot has Reduced Motion enabled receives `true` before `StartBattle(...)` begins presentation.

Keep the test local to `GameTest`; do not introduce a production settings-provider seam solely for this assertion.

## 10. File ownership

### Modify

- `scripts/settings/SettingsData.cs`
- `scripts/settings/SettingsManager.cs`
- `scenes/ui/SettingsMenu.tscn`
- `scripts/ui/SettingsMenuController.cs`
- `scripts/game/Game.cs`
- `scripts/ui/BattleManager.cs`
- `tests/settings/SettingsDataTest.cs`
- `tests/settings/SettingsManagerTest.cs`
- `tests/ui/SettingsMenuControllerTest.cs`
- `tests/ui/SettingsMenuSceneTest.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/game/GameTest.cs`

### Audit only

- `scripts/ui/showcase/SiriusUiShowcase.cs` — development showcase, not a production consumer;
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — no lifecycle authority changes are expected;
- `docs/PRD.md` / `CLAUDE.md` — update only if implementation makes an existing settings/motion statement stale.

## 11. Explicit non-goals

- no `MotionPolicy`, service, singleton, event bus, observer, or settings-change signal;
- no shared component reading `SettingsManager`;
- no new Settings page or accessibility framework;
- no settings version bump or migration matrix;
- no camera-smoothing/world-animation changes;
- no reward-toast timing changes;
- no battle-speed or auto-combat timing setting;
- no changes to stat/progress value updates;
- no showcase work;
- no speculative support for future animated screens.

## 12. Definition of done

- `ReducedMotionEnabled` defaults safely to `false`, clones, saves, and reloads.
- An older valid settings JSON without the field resolves to `false`.
- The existing Display settings page stages, applies, and cancels the preference correctly at standard and compact layouts.
- Every newly opened production Battle receives the current value from `Game` without reading the singleton itself.
- Reduced Battle presentation removes damage translation, attack scale/flash, and looping idle animation while retaining damage opacity feedback and unchanged Battle timing/domain behavior.
- Normal mode retains current Battle motion.
- Focused settings, Battle, and Game tests pass; the full suite/build and `git diff --check` pass before merge.
- A final production motion search confirms no additional player-facing `CreateTween`/`TweenProperty` owner was missed.
