# HPA-541 Reduced Motion Preference Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a Reduced Motion preference and apply it to Sirius’s proven production Battle motion without introducing a global motion framework or changing gameplay timing.

**Architecture:** Extend the existing `SettingsData` / `SettingsManager` persistence path and the scene-authored Settings Display page. `Game` reads the current settings snapshot whenever it opens a Battle and passes one boolean into `BattleManager.StartBattle(...)`; Battle locally suppresses decorative translation, scale/flash, and idle loops while retaining damage opacity feedback and all semantic/timing behavior.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, System.Text.Json, GdUnit4.

**Spec:** `docs/superpowers/specs/2026-08-23-hpa-541-reduced-motion-design.md`

## Global Constraints

- This remains one HPA-541 PR; do not open a second implementation PR.
- `ReducedMotionEnabled` defaults to `false`.
- Do not bump `SettingsData.CurrentVersion` or add migration infrastructure.
- Do not add a motion service, global policy object, settings event bus, observer, or shared-component lifecycle API.
- Shared/static UI components must not read `SettingsManager`.
- The production motion survey currently identifies Battle as the only player-facing `CreateTween` / `TweenProperty` owner; the UI showcase is excluded.
- Reduced motion removes Battle damage translation, attack scale/flash, and looping actor idle animation.
- Keep the existing 1-second damage opacity fade and its completion timing.
- Do not change `_battleTimer.WaitTime`, action-point simulation, automatic-action progress, stat values, focus/input, reward application, or Battle completion.
- Main Menu receives no no-op motion state: applying Settings there persists through `SettingsManager`, and later Battle creation reads the current snapshot.

---

## File map

### Settings ownership

- `scripts/settings/SettingsData.cs` — persisted boolean, defaults, cloning.
- `scripts/settings/SettingsManager.cs` — sanitization/load-save preservation only; no motion dispatch.
- `scenes/ui/SettingsMenu.tscn` — authored Reduced Motion row on Display page.
- `scripts/ui/SettingsMenuController.cs` — staged populate/apply behavior.

### Battle ownership

- `scripts/game/Game.cs` — composition root reads the latest snapshot and passes the scalar preference when Battle starts.
- `scripts/ui/BattleManager.cs` — Battle-instance reduced-motion state and local presentation decisions.

### Tests

- `tests/settings/SettingsDataTest.cs`
- `tests/settings/SettingsManagerTest.cs`
- `tests/ui/SettingsMenuControllerTest.cs`
- `tests/ui/SettingsMenuSceneTest.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/ui/BattleSceneTest.cs`
- `tests/game/GameTest.cs`

---

### Task 1: Persist and stage the Reduced Motion preference

**Files:**
- Modify: `scripts/settings/SettingsData.cs`
- Modify: `scripts/settings/SettingsManager.cs`
- Modify: `scenes/ui/SettingsMenu.tscn`
- Modify: `scripts/ui/SettingsMenuController.cs`
- Test: `tests/settings/SettingsDataTest.cs`
- Test: `tests/settings/SettingsManagerTest.cs`
- Test: `tests/ui/SettingsMenuControllerTest.cs`
- Test: `tests/ui/SettingsMenuSceneTest.cs`

**Interfaces:**
- Produces: `SettingsData.ReducedMotionEnabled : bool`, default `false`.
- Produces: `%ReducedMotionCheck : CheckBox` on the existing Display page.
- Consumes: existing `SettingsManager.ApplyAndSave(SettingsData)` and Settings staged Apply/Cancel flow.

- [ ] **Step 1: Add failing settings-data and persistence assertions**

Extend the existing default/clone tests with the new contract:

```csharp
AssertThat(data.ReducedMotionEnabled).IsFalse();
```

In the clone test, set and verify the value independently of the source:

```csharp
var original = SettingsData.CreateDefaults();
original.ReducedMotionEnabled = true;

var cloned = original.Clone();

AssertThat(cloned.ReducedMotionEnabled).IsTrue();
cloned.ReducedMotionEnabled = false;
AssertThat(original.ReducedMotionEnabled).IsTrue();
```

Extend `SettingsManager_SaveAndReload_PreservesSettings`:

```csharp
candidate.ReducedMotionEnabled = true;
AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

var reloadedManager = await RebootSettingsManager();
AssertThat(reloadedManager.GetSnapshot().ReducedMotionEnabled).IsTrue();
```

Add a focused missing-field regression using the existing settings test setup. Write a valid current-shape JSON without the new field, bootstrap the manager, and assert `false`:

```csharp
[TestCase]
public async Task SettingsManager_Ready_SettingsWithoutReducedMotion_DefaultsFalse()
{
    var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
    File.WriteAllText(settingsPath, """
        {
          "Version": 1,
          "MasterVolumePercent": 100,
          "MusicVolumePercent": 100,
          "SfxVolumePercent": 100,
          "Difficulty": "Normal",
          "FullscreenEnabled": false,
          "ResolutionWidth": 1280,
          "ResolutionHeight": 720,
          "AutoSaveEnabled": true,
          "PrimaryKeybindings": {
            "toggle_inventory": 73,
            "interact": 69,
            "pause_menu": 4194305
          }
        }
        """);

    var manager = await BootstrapSettingsManager();

    AssertThat(manager.GetSnapshot().ReducedMotionEnabled).IsFalse();
}
```

- [ ] **Step 2: Run the settings-data tests and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest"
```

Expected: compile/test failure because `SettingsData.ReducedMotionEnabled` does not exist yet.

- [ ] **Step 3: Implement the minimal persisted field**

In `SettingsData` add:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

Keep `CurrentVersion = 1`.

Copy the value from `Clone()`:

```csharp
ReducedMotionEnabled = ReducedMotionEnabled,
```

Copy it in `SettingsManager.Sanitize(...)`:

```csharp
ReducedMotionEnabled = data.ReducedMotionEnabled,
```

Do not add anything to `ApplyToRuntime(...)`; persistence is sufficient until a concrete motion owner is composed by `Game`.

- [ ] **Step 4: Run the settings-data tests and verify GREEN**

Run the Step 2 command again.

Expected: PASS, including the old-JSON-without-field regression.

- [ ] **Step 5: Add failing scene/controller coverage for the authored checkbox**

Add these paths to `SettingsMenuSceneTest.RequiredUniqueNodes`:

```csharp
"%ReducedMotionLabel",
"%ReducedMotionCheck",
```

Add controller coverage:

```csharp
[TestCase]
public void OpenSettings_SetsReducedMotionCheckbox()
{
    var data = SettingsData.CreateDefaults();
    data.ReducedMotionEnabled = true;

    _ctrl.OpenSettings(data);

    AssertThat(GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed).IsTrue();
}
```

Extend the staged-cancel test so the checkbox is toggled before Cancel and the source snapshot remains unchanged:

```csharp
snapshot.ReducedMotionEnabled = false;
_ctrl.OpenSettings(snapshot);
GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed = true;
InvokePrivate(_ctrl, "OnCancelPressed");
AssertThat(snapshot.ReducedMotionEnabled).IsFalse();
```

Add an Apply test using the same local `SettingsManager` + runtime/file override pattern already used by `OnApplyPressed_WhenSelectionsAreUnset_DoesNotIndexPastOptions`. Set `%ReducedMotionCheck` true, invoke Apply, then assert:

```csharp
AssertThat(settingsManager.GetSnapshot().ReducedMotionEnabled).IsTrue();
AssertThat(closed).IsTrue();
```

- [ ] **Step 6: Run the Settings UI tests and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: FAIL because the authored nodes/controller field do not exist yet.

- [ ] **Step 7: Author the Display row and bind staged behavior**

In `SettingsMenu.tscn`, append this row to `%DisplayRows` after Resolution:

```text
[node name="ReducedMotionLabel" type="Label" parent="ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/SettingsFrame/PageDeck/DisplayPage/DisplayScroll/DisplayRows"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
text = "Reduced Motion"
autowrap_mode = 3
theme_type_variation = &"SiriusBody"

[node name="ReducedMotionCheck" type="CheckBox" parent="ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/SettingsFrame/PageDeck/DisplayPage/DisplayScroll/DisplayRows"]
unique_name_in_owner = true
custom_minimum_size = Vector2(0, 44)
layout_mode = 2
size_flags_horizontal = 3
```

In `SettingsMenuController`, bind:

```csharp
private CheckBox _reducedMotionCheck = null!;
```

```csharp
_reducedMotionCheck = GetNode<CheckBox>("%ReducedMotionCheck");
```

Populate it:

```csharp
_reducedMotionCheck.ButtonPressed = _editedSettings.ReducedMotionEnabled;
```

Include it in the Apply candidate:

```csharp
ReducedMotionEnabled = _reducedMotionCheck.ButtonPressed,
```

Do not add a Changed signal or mutate `SettingsManager` when the checkbox is clicked.

- [ ] **Step 8: Run the focused Settings suites and verify GREEN**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: PASS at standard and compact scene test viewports.

- [ ] **Step 9: Commit Task 1**

```bash
git add scripts/settings/SettingsData.cs \
  scripts/settings/SettingsManager.cs \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/settings/SettingsDataTest.cs \
  tests/settings/SettingsManagerTest.cs \
  tests/ui/SettingsMenuControllerTest.cs \
  tests/ui/SettingsMenuSceneTest.cs
git commit -m "feat(settings): add reduced motion preference"
```

---

### Task 2: Apply reduced motion inside Battle without changing timing

**Files:**
- Modify: `scripts/ui/BattleManager.cs`
- Test: `tests/ui/BattleManagerTest.cs`
- Test: `tests/ui/BattleSceneTest.cs`

**Interfaces:**
- Consumes: `bool reducedMotionEnabled` supplied at Battle startup.
- Produces: `BattleManager.StartBattle(Character player, Enemy enemy, bool reducedMotionEnabled = false)`.
- Internal state: private `_reducedMotionEnabled` for the lifetime of the Battle instance.
- Does not consume `SettingsManager`.

- [ ] **Step 1: Add failing reduced attack/damage tests**

In `BattleManagerTest`, add a reduced attack test using the existing ready-manager/reflection helpers:

```csharp
[TestCase]
public async Task PlayAttackAnimation_ReducedMotionSkipsTweenAndVisualMutation()
{
    var manager = await CreateReadyBattleManager();
    try
    {
        SetPrivateField(manager, "_reducedMotionEnabled", true);
        var sprite = new AnimatedSprite2D
        {
            Scale = new Vector2(3f, 3f),
            Modulate = Colors.White
        };
        manager.AddChild(sprite);

        InvokePrivateMethod(manager, "PlayAttackAnimation", sprite);

        var tweens = GetPrivateField<System.Collections.Generic.HashSet<Tween>>(
            manager, "_visualTweens");
        AssertThat(tweens.Count).IsEqual(0);
        AssertThat(sprite.Scale).IsEqual(new Vector2(3f, 3f));
        AssertThat(sprite.Modulate).IsEqual(Colors.White);
    }
    finally
    {
        await FreeManager(manager);
    }
}
```

Add a reduced damage test. The label must remain fixed while opacity starts fading:

```csharp
[TestCase]
public async Task ShowDamageNumber_ReducedMotionFadesWithoutTranslation()
{
    var manager = await CreateReadyBattleManager();
    try
    {
        SetPrivateField(manager, "_reducedMotionEnabled", true);
        var label = new Label { Position = new Vector2(12f, 34f) };
        manager.AddChild(label);
        var resting = label.Position;

        InvokePrivateMethod(manager, "ShowDamageNumber", label, 25, false);
        await ToSignal(
            ((SceneTree)Engine.GetMainLoop()).CreateTimer(0.1),
            Timer.SignalName.Timeout);

        AssertThat(label.Position).IsEqual(resting);
        AssertThat(label.Modulate.A).IsLess(1f);
        AssertThat(label.Modulate.A).IsGreater(0f);
    }
    finally
    {
        await FreeManager(manager);
    }
}
```

Keep the existing `StopBattleRuntime_KillsTrackedVisualTweens` test as the normal-mode attack regression.

- [ ] **Step 2: Add failing real-scene idle-loop tests**

In `BattleSceneTest` add:

```csharp
[TestCase]
public async Task ReducedMotion_StartBattleKeepsActorIdleSpritesStatic()
{
    _battle.StartBattle(
        TestHelpers.CreateTestCharacter(),
        Enemy.CreateGoblin(),
        reducedMotionEnabled: true);
    await AwaitFrames(2);

    var player = _battle.GetNode<AnimatedSprite2D>(
        "%PlayerSpriteContainer/PlayerSprite");
    var enemy = _battle.GetNode<AnimatedSprite2D>(
        "%EnemySpriteContainer/EnemySprite");

    AssertThat(player.IsPlaying()).IsFalse();
    AssertThat(enemy.IsPlaying()).IsFalse();
    AssertThat(player.Frame).IsEqual(0);
    AssertThat(enemy.Frame).IsEqual(0);
}
```

Add the normal counterpart:

```csharp
[TestCase]
public async Task DefaultMotion_StartBattleKeepsActorIdleSpritesPlaying()
{
    _battle.StartBattle(TestHelpers.CreateTestCharacter(), Enemy.CreateGoblin());
    await AwaitFrames(2);

    AssertThat(_battle.GetNode<AnimatedSprite2D>(
        "%PlayerSpriteContainer/PlayerSprite").IsPlaying()).IsTrue();
    AssertThat(_battle.GetNode<AnimatedSprite2D>(
        "%EnemySpriteContainer/EnemySprite").IsPlaying()).IsTrue();
}
```

- [ ] **Step 3: Run the Battle tests and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: compile/reflection failures because reduced-motion startup state/parameter does not exist and current Battle always animates.

- [ ] **Step 4: Capture reduced-motion state at Battle startup**

Add:

```csharp
private bool _reducedMotionEnabled;
```

Change startup to:

```csharp
public void StartBattle(
    Character player,
    Enemy enemy,
    bool reducedMotionEnabled = false)
{
    if (player == null || enemy == null)
    {
        // keep the existing null guard body unchanged
    }

    _reducedMotionEnabled = reducedMotionEnabled;

    // existing initialization continues unchanged
}
```

Assign `_reducedMotionEnabled` before `SetupCharacterAnimations()` is called.

- [ ] **Step 5: Make actor idle animation static in reduced mode**

For both player and enemy, keep frame construction, texture assignment, scale, material, and fallback behavior unchanged. Replace unconditional idle `Play(...)` with:

```csharp
if (_reducedMotionEnabled)
{
    sprite.Stop();
    sprite.Frame = 0;
}
else
{
    sprite.Play("idle");
}
```

Use the actual `_playerSprite` / `_enemySprite` variables at their existing setup sites; do not extract a generic animation-policy class.

- [ ] **Step 6: Suppress only nonessential attack and damage motion**

At the start of `PlayAttackAnimation(...)`, after the existing null guard, return in reduced mode:

```csharp
if (_reducedMotionEnabled)
    return;
```

In `ShowDamageNumber(...)`, keep the existing parallel tween and opacity tween, but conditionally add only the position tweener:

```csharp
var tween = CreateTrackedTween();
tween.SetParallel(true);

var startPos = damageLabel.Position;
if (!_reducedMotionEnabled)
{
    var endPos = startPos + new Vector2(0, -30);
    tween.TweenProperty(damageLabel, "position", endPos, 1.0);
}

tween.TweenProperty(damageLabel, "modulate:a", 0.0f, 1.0);

tween.TweenCallback(Callable.From(() =>
{
    damageLabel.Position = startPos;
    damageLabel.Modulate = new Color(1, 0, 0, 0);
})).SetDelay(1.0);
```

Do not change `_battleTimer.WaitTime = 1.5`, `_Process(...)` action-progress calculation, or any turn/domain code.

- [ ] **Step 7: Run the Battle suites and verify GREEN**

Run the Step 3 command again.

Expected: PASS for reduced and default motion, including existing visual-tween teardown coverage.

- [ ] **Step 8: Commit Task 2**

```bash
git add scripts/ui/BattleManager.cs \
  tests/ui/BattleManagerTest.cs \
  tests/ui/BattleSceneTest.cs
git commit -m "feat(ui): reduce battle motion"
```

---

### Task 3: Inject the current preference from Game and close verification

**Files:**
- Modify: `scripts/game/Game.cs`
- Test: `tests/game/GameTest.cs`
- Audit: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Audit: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: `docs/PRD.md`
- Audit: `CLAUDE.md`

**Interfaces:**
- Consumes: `SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled`.
- Calls: `BattleManager.StartBattle(Character, Enemy, bool)`.
- Fallback: missing/invalid Settings singleton means `false` and Battle still opens.

- [ ] **Step 1: Add a failing real-Game propagation test without a production test seam**

In `GameTest`, create a temporary `SettingsManager` object without adding it to the tree. Its `_settings` field already starts with defaults, so set a cloned snapshot to Reduced Motion and temporarily install the object as the singleton via the same private-setter reflection pattern already used in `SettingsManagerTest.ResetSingleton()`:

```csharp
[TestCase]
public async Task BattleStart_UsesCurrentReducedMotionSetting()
{
    var previousSettingsManager = SettingsManager.Instance;
    var settingsManager = new SettingsManager();
    Game? game = null;

    try
    {
        var snapshot = settingsManager.GetSnapshot();
        snapshot.ReducedMotionEnabled = true;
        SetPrivateField(settingsManager, "_settings", snapshot);
        SetSettingsManagerInstance(settingsManager);

        game = await InstantiateRealGameScene();
        var manager = game.GetNode<GameManager>("GameManager");
        manager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);

        var battle = GetPrivateField<BattleManager>(game, "_battleManager");
        AssertThat(GetPrivateField<bool>(battle, "_reducedMotionEnabled")).IsTrue();
    }
    finally
    {
        if (game != null && IsInstanceValid(game))
            game.Free();
        SetSettingsManagerInstance(previousSettingsManager);
        if (IsInstanceValid(settingsManager))
            settingsManager.Free();
        await AwaitFrames(1);
    }
}
```

Add the local helper:

```csharp
private static void SetSettingsManagerInstance(SettingsManager? manager)
{
    var property = typeof(SettingsManager).GetProperty(
        "Instance",
        BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("SettingsManager.Instance not found.");
    var setter = property.GetSetMethod(true)
        ?? throw new InvalidOperationException("SettingsManager.Instance setter not found.");
    setter.Invoke(null, new object?[] { manager });
}
```

This test does not touch disk, window/audio state, or add a test-only production provider.

- [ ] **Step 2: Run the Game test and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest"
```

Expected: FAIL because `Game` still calls Battle startup without the current settings value, leaving `_reducedMotionEnabled == false`.

- [ ] **Step 3: Pass the current setting at the existing Battle composition seam**

In `Game.OnBattleStarted(...)`, immediately before Battle startup, read the current snapshot with a false fallback and pass it to the existing Battle instance:

```csharp
var reducedMotionEnabled =
    SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled ?? false;

_battleManager = battle;
_battleHandle = result.Handle.Value;
battle.StartBattle(
    _gameManager.Player,
    enemy,
    reducedMotionEnabled);
```

Do not cache the value on `Game`, subscribe to Settings changes, or edit Main Menu. Every Battle opening performs this one current-value read.

- [ ] **Step 4: Run the Game test and focused feature suites**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: PASS.

- [ ] **Step 5: Re-run the production motion survey**

Run:

```bash
rg -n 'CreateTween|TweenProperty|SetAnimationLoop|\.Play\("idle"\)' scripts/ui \
  --glob '!scripts/ui/showcase/**'
```

Expected: player-facing motion ownership remains in `scripts/ui/BattleManager.cs`; do not add speculative wiring for files that only render semantic state changes.

Also inspect:

```bash
git diff main...HEAD -- scripts/ui/showcase/SiriusUiShowcase.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md docs/PRD.md CLAUDE.md
```

Expected: no edits unless implementation made an existing statement factually stale. Do not update docs just to create churn.

- [ ] **Step 6: Run full verification**

Run:

```bash
dotnet build Sirius.sln
dotnet test Sirius.sln --settings test.runsettings.local
git diff --check
```

Expected: build succeeds, all GdUnit4 tests pass, and `git diff --check` prints no errors.

- [ ] **Step 7: Commit Task 3**

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "feat(game): pass reduced motion to battle"
```

- [ ] **Step 8: Update PR/Linear closeout evidence on the same HPA-541 PR**

Record in the existing draft PR description/comment:

```text
Verification:
- dotnet build Sirius.sln
- dotnet test Sirius.sln --settings test.runsettings.local
- git diff --check
- production motion survey: BattleManager remains the only player-facing UI motion owner
```

After implementation review passes, mark this same PR ready for review. Do not create another PR for implementation.
