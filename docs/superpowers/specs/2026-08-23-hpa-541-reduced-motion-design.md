# HPA-541 Reduced Motion Preference Design

**Status:** Planning candidate
**Linear:** HPA-541 — Add persisted Reduced Motion preference and bind Sirius UI motion policy
**Date:** 2026-08-23

## 1. Decision

Implement HPA-541 as one local preference slice. Add one persisted `ReducedMotionEnabled` boolean, expose it on the existing Display settings page, and pass that scalar only through existing composition ownership.

The current production survey shows two motion domains that belong in this ticket:

1. **Exploration/world presentation** — camera smoothing plus 5 FPS player/enemy idle frame cycling.
2. **Battle presentation** — looping actor idle frames, floating damage translation, and attack scale/flash feedback.

Do not add a motion service, global policy object, event bus, settings-changed signal, shared-component lifecycle API, or new Settings page.

Ownership stays concrete:

```text
SettingsManager -> snapshot -> Game
                         |-> GridMap scalar -> PlayerDisplay / EnemySpawn
                         |-> Camera2D smoothing choice
                         `-> BattleManager startup bool
```

`Game` remains the composition root. `GridMap`, `PlayerDisplay`, `EnemySpawn`, and `BattleManager` do not read `SettingsManager`.

This branch remains the **single HPA-541 PR**. Planning and implementation commits stay on this PR.

## 2. Production motion survey

The earlier Battle-only survey was incomplete because it searched only `CreateTween` / `TweenProperty` under `scripts/ui`. Reduced-motion ownership is not limited to tween APIs or the UI directory.

The corrected survey covers process-driven animation, redraw/frame cycling, sprite playback, and camera smoothing across `scripts/`.

### Player-facing owners to handle

#### `scripts/ui/BattleManager.cs`

Battle owns:

- damage-label translation upward 30 px over 1 second;
- damage-label opacity fade over the same 1 second;
- attack scale to 1.2× and back;
- attack white flash and restore;
- looping 4 FPS player/enemy `AnimatedSprite2D` idle playback.

#### `scripts/game/PlayerDisplay.cs`

The baked-TileMap production player sprite advances `RegionRect` through four frames every 0.2 seconds.

#### `scripts/game/EnemySpawn.cs`

Scene-authored runtime enemy sprites advance `RegionRect` through four frames every 0.2 seconds.

#### `scripts/game/Game.cs`

The production camera enables Godot position smoothing when `EnableCameraSmoothing` is true. Reduced Motion disables this nonessential camera interpolation while preserving grid movement and camera targeting.

#### `scripts/game/GridMap.cs`

`GridMap` still owns a legacy/procedural `_currentFrame` + `QueueRedraw()` animation loop. Production defaults `UseBakedTileMapsAtRuntime = true`, and `_Draw()` returns immediately in that mode, so this is **not** the visible production player/enemy animation source. The loop still belongs to the world owner and must honor Reduced Motion so the non-baked path cannot keep cycling and the baked path does not perform needless reduced-mode redraw work.

### Surveyed but not reduced here

- `NpcSpawn` has no runtime frame loop; its `_Process` is editor-only.
- `TreasureBoxSpawn.OpenAsync()` is a short finite state-change animation, not continuous ambient motion. It remains because each frame communicates the open transition and completion state.
- `SiriusUiShowcase` is development-only demonstration code.
- `SiriusMotion` is shared chrome demo policy, not a Battle timing source.

### Closeout audit

Use a broad inventory rather than claiming one API proves completeness:

```bash
rg -n 'CreateTween|TweenProperty|QueueRedraw|_currentFrame|_animTimer|RegionRect|\.Play\(|PositionSmoothing|AnimationPlayer|AnimatedSprite2D' \
  scripts --glob '!scripts/ui/showcase/**'
```

The closeout requirement is: **every player-facing result is classified as handled or explicitly retained with a reason.** Do not claim there are no other motion owners merely because a narrower grep is empty.

## 3. Persistence contract

Extend `SettingsData` with:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

Default is `false`.

Do **not** bump `SettingsData.CurrentVersion`. A development settings JSON that lacks the field naturally deserializes to `false`.

### Make `Sanitize` future-field-safe without a mapper

`SettingsManager.Sanitize(...)` currently reconstructs `SettingsData` as a whitelist. That makes every future plain settings property easy to silently drop.

Keep the current simple model, but start sanitization from `data.Clone()` and overwrite only fields that actually require normalization:

```csharp
var sanitized = data.Clone();
sanitized.Version = SettingsData.CurrentVersion;
sanitized.MasterVolumePercent = Mathf.Clamp(data.MasterVolumePercent, 0, 100);
sanitized.MusicVolumePercent = Mathf.Clamp(data.MusicVolumePercent, 0, 100);
sanitized.SfxVolumePercent = Mathf.Clamp(data.SfxVolumePercent, 0, 100);
sanitized.Difficulty = string.IsNullOrWhiteSpace(data.Difficulty)
    ? defaults.Difficulty
    : data.Difficulty;
sanitized.ResolutionWidth = isResolutionValid
    ? data.ResolutionWidth
    : defaults.ResolutionWidth;
sanitized.ResolutionHeight = isResolutionValid
    ? data.ResolutionHeight
    : defaults.ResolutionHeight;
sanitized.PrimaryKeybindings = NormalizeKeybindings(data.PrimaryKeybindings);
```

`Clone()` must remain safe for unsanitized JSON whose `PrimaryKeybindings` is null:

```csharp
PrimaryKeybindings = PrimaryKeybindings is null
    ? CreateDefaultKeybindings()
    : new Dictionary<string, long>(PrimaryKeybindings)
```

This is not a mapper or migration layer. It preserves the current validation behavior while making future plain fields copy automatically.

## 4. Settings UI

Add one authored row to the existing Display page after Resolution:

```text
Reduced Motion    [ ]
```

Use:

- `%ReducedMotionLabel`
- `%ReducedMotionCheck`

Reuse the existing `DisplayRows` responsive grid and page-local scrolling.

`SettingsMenuController` keeps the current staged contract:

- `OpenSettings(...)` populates the checkbox from the cloned snapshot;
- toggling changes only the control state;
- Cancel discards it;
- Apply places the checkbox value into the `SettingsData` candidate passed to `SettingsManager.ApplyAndSave(...)`.

Do not bind `Toggled` directly to persistence.

## 5. World propagation

`Game` owns one private snapshot read:

```csharp
private bool CurrentReducedMotionEnabled() =>
    SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled ?? false;
```

Use one private helper to apply the current value to the active world:

```csharp
private void ApplyCurrentReducedMotionToWorld()
{
    var reduced = CurrentReducedMotionEnabled();

    if (_camera != null)
        _camera.PositionSmoothingEnabled = EnableCameraSmoothing && !reduced;

    if (_gridMap != null)
        _gridMap.ReducedMotionEnabled = reduced;
}
```

Call it:

- after initial camera setup in `Game._Ready()`;
- after assigning a newly loaded `_gridMap` in `OnFloorLoaded(...)`;
- after gameplay Settings closes, so a successful Apply takes effect on the current exploration scene immediately. Calling it after Cancel is harmless because the persisted snapshot is unchanged.

Main Menu needs no production motion field. Settings applied there are already persisted before a later Game scene starts.

## 6. Exploration/world reduced-motion behavior

### Camera

Normal mode preserves `EnableCameraSmoothing`.

Reduced mode sets `Camera2D.PositionSmoothingEnabled = false`.

Do not change player movement, camera target coordinates, zoom, or input cadence.

### `GridMap`

Add one scalar:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

Editor preview behavior remains unchanged.

In the runtime animation branch, when reduced motion is enabled:

- clear `_animationTime`;
- reset `_currentFrame` to `0` once when needed;
- request at most the redraw required to show frame 0;
- do not continue the 5 FPS frame/redraw loop.

This covers the legacy non-baked renderer and avoids useless cycling in the baked production path.

### `PlayerDisplay`

`PlayerDisplay` reads only its existing `_gridMap.ReducedMotionEnabled` scalar.

Reduced mode:

- reset `_animTimer`;
- keep `_currentFrame = 0`;
- set the region to the first frame when a reset is needed;
- do not advance frames.

Normal mode keeps the existing 5 FPS loop.

### `EnemySpawn`

Runtime `EnemySpawn` uses the same parent-world scalar.

Reduced mode keeps region frame 0 and does not advance `_animTimer` / `_currentFrame`.

Editor placement/snap processing remains unchanged.

NPCs remain static because they have no runtime frame loop today.

## 7. Battle propagation

`BattleManager` must not read `SettingsManager` or `SiriusMotion`.

Make the preference an explicit required startup argument:

```csharp
public void StartBattle(
    Character player,
    Enemy enemy,
    bool reducedMotionEnabled)
```

There is one production caller. Existing Battle tests should pass `reducedMotionEnabled: false` explicitly. There is no compatibility overload/default.

`Game.OnBattleStarted(...)` calls:

```csharp
battle.StartBattle(
    _gameManager.Player,
    enemy,
    CurrentReducedMotionEnabled());
```

The flag is copied into private Battle-instance state before `SetupCharacterAnimations()`.

## 8. Battle reduced-motion behavior

### Damage numbers

Normal mode stays unchanged:

- translate upward 30 px over 1 second;
- fade opacity to 0 over 1 second;
- reset at the existing 1-second completion point.

Reduced mode:

- keep the label at its resting position;
- retain the same 1-second opacity fade;
- retain the same reset timing.

Do **not** route this through `SiriusMotion`. `SiriusMotion.ReducedOpacitySeconds` is 0.100 seconds for shared chrome entry/exit demonstration. Battle owns a deliberate 1.0-second combat-feedback duration.

### Attack feedback

Reduced mode skips the attack scale/flash tween entirely. It must not add a replacement delay.

`_battleTimer` remains the battle cadence owner.

### Idle sprites

Normal mode continues `Play("idle")`.

Reduced mode must explicitly select the authored animation before choosing a static frame:

```csharp
sprite.Animation = "idle";
sprite.Frame = 0;
sprite.Stop();
```

Setting only `Stop(); Frame = 0;` is incorrect because a new `SpriteFrames` resource also contains an empty `default` animation; without selecting `idle`, the actor can render nothing.

The real-scene regression must assert both stillness and a resolved texture for `idle` frame 0.

## 9. Current-value lifecycle

The preference must affect current and future production owners without a global event system.

- Main Menu Apply -> later Game `_Ready()` reads the persisted value.
- Gameplay Pause -> Settings Apply -> `OnHostedSettingsClosed()` reapplies the current value to camera/GridMap immediately.
- New floor -> `OnFloorLoaded(...)` applies the current value to its GridMap; `PlayerDisplay` and `EnemySpawn` follow that scalar.
- New Battle -> `OnBattleStarted(...)` reads the current value and passes it to the new Battle instance.

Battle and Settings cannot overlap in normal production flow, so an already-active Battle does not need live rebinding.

If `SettingsManager.Instance` is unavailable, use `false` and preserve current motion. The preference must never block gameplay or Battle startup.

## 10. Test strategy

### Settings

`SettingsDataTest` / `SettingsManagerTest`:

- default false;
- clone preserves the boolean;
- clone remains safe with null keybindings;
- save/reload preserves true;
- valid older JSON without the field loads false;
- sanitize still normalizes invalid/null keybindings and other validated fields.

`SettingsMenuControllerTest` / `SettingsMenuSceneTest`:

- authored row exists;
- Open populates true;
- Cancel does not mutate source snapshot;
- dedicated `OnApplyPressed_PersistsReducedMotionCheckbox` proves the Apply initializer copies the field;
- standard/compact layout stays valid.

### World

Use focused game tests to prove:

- reduced `GridMap` runtime processing does not advance beyond frame 0;
- reduced `PlayerDisplay` stays on region frame 0 while normal mode advances;
- reduced `EnemySpawn` stays on region frame 0 while normal mode advances;
- a real Game with reduced motion disables camera smoothing and marks the active GridMap reduced;
- closing gameplay Settings reapplies the latest snapshot to the current world;
- loading a later floor receives the current value.

### Battle

`BattleManagerTest`:

- reduced attack feedback creates no attack tween and leaves scale/modulation unchanged;
- reduced damage uses the tracked tween and `Tween.CustomStep(...)` for deterministic half-duration assertions;
- at 0.5 seconds, position is unchanged and alpha is ~0.5;
- at 1.0 seconds, the reset contract completes;
- normal attack/damage behavior remains covered;
- teardown still kills tracked tweens.

`BattleSceneTest`:

- reduced startup selects `idle`, frame 0, does not play, and `GetFrameTexture("idle", 0)` is non-null for both actors;
- normal startup plays idle.

`GameTest`:

- a real hosted Battle receives the current required boolean.

## 11. File ownership

### Modify

- `scripts/settings/SettingsData.cs`
- `scripts/settings/SettingsManager.cs`
- `scenes/ui/SettingsMenu.tscn`
- `scripts/ui/SettingsMenuController.cs`
- `scripts/game/Game.cs`
- `scripts/game/GridMap.cs`
- `scripts/game/PlayerDisplay.cs`
- `scripts/game/EnemySpawn.cs`
- `scripts/ui/BattleManager.cs`
- focused existing/new tests under `tests/settings`, `tests/ui`, and `tests/game`

### Audit only unless stale

- `scripts/game/NpcSpawn.cs`
- `scripts/game/TreasureBoxSpawn.cs`
- `scripts/ui/theme/SiriusMotion.cs`
- `scripts/ui/showcase/SiriusUiShowcase.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`
- `docs/PRD.md`
- `CLAUDE.md`

## 12. Explicit non-goals

- no `MotionPolicy` object/service/singleton/event bus;
- no settings-change broadcast;
- no component reading `SettingsManager` directly;
- no new Settings page/accessibility framework;
- no settings version bump/migration matrix;
- no `SiriusMotion` reuse inside Battle;
- no battle-speed or auto-combat timing changes;
- no player movement/input timing changes;
- no reward-toast timing changes;
- no Treasure Box state-transition removal;
- no editor-preview motion changes;
- no speculative support for future animated screens.

## 13. Risks and mitigations

### Risk: reduced Battle actors become invisible

Cause: stopping an `AnimatedSprite2D` before selecting `idle` leaves it on the empty default animation.

Mitigation: set `Animation = "idle"`, then frame 0, then stop; real-scene tests assert the selected animation and non-null frame texture.

### Risk: the motion survey misses non-tween animation

Cause: process-driven `RegionRect` cycling, `QueueRedraw`, and camera smoothing do not use `CreateTween`.

Mitigation: broad `scripts/` audit plus explicit classification of each player-facing hit. Do not infer completeness from a narrow API grep.

### Risk: a future settings field is silently lost by sanitization

Cause: whitelist reconstruction must be updated for every field.

Mitigation: sanitize from a safe clone and overwrite only fields that actually require validation; dedicated Reduced Motion round-trip/UI Apply tests remain as feature-level guards.

## 14. Definition of done

- `ReducedMotionEnabled` defaults false, clones, saves, reloads, and is safe with missing old JSON fields.
- `Sanitize` preserves future plain settings fields by starting from a safe clone while keeping existing validation behavior.
- Display Settings stages, applies, and cancels the checkbox correctly.
- Reduced exploration disables camera smoothing and freezes production player/enemy idle frame cycling without changing movement/input.
- The legacy `GridMap` runtime frame/redraw loop also honors the world scalar; editor preview remains unchanged.
- New floors and the current exploration scene use the latest applied value without a global settings event.
- New Battles receive an explicit required bool from `Game`.
- Reduced Battle keeps actors visible on static `idle` frame 0, removes attack scale/flash and damage translation, and retains the local 1-second opacity fade.
- `BattleManager` contains no `SiriusMotion` reference.
- Focused suites, full `dotnet test`, `dotnet build Sirius.sln`, and `git diff --check` pass.
- The final broad motion audit has no **unclassified** player-facing results.