# HPA-541 Reduced Motion Preference Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a Reduced Motion preference and apply it to Sirius’s proven production Battle motion without introducing a global motion framework or changing gameplay timing.

**Architecture:** Extend the existing `SettingsData` / `SettingsManager` persistence path and scene-authored Settings Display page. `Game` reads the current snapshot whenever it opens a Battle and passes one boolean into `BattleManager.StartBattle(...)`; Battle locally suppresses decorative translation, scale/flash, and idle loops while retaining opacity feedback and semantic/timing behavior.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, System.Text.Json, GdUnit4.

**Spec:** `docs/superpowers/specs/2026-08-23-hpa-541-reduced-motion-design.md`

## Global Constraints

- Keep all planning and implementation in this one HPA-541 PR.
- `ReducedMotionEnabled` defaults to `false`; keep `SettingsData.CurrentVersion = 1`.
- No motion service, global policy object, settings event bus, observer, or shared-component lifecycle API.
- Shared/static UI components do not read `SettingsManager`.
- Battle is the only proven player-facing UI motion owner; `SiriusUiShowcase` stays out of scope.
- Reduced mode removes Battle damage translation, attack scale/flash, and looping actor idle animation.
- Keep the 1-second damage opacity fade and existing Battle timing.
- Do not change action-point simulation, automatic-action progress, stats, focus/input, rewards, or Battle completion.
- Main Menu gets no no-op motion state; Settings persistence already reaches later Game/Battle composition.

---

### Task 1: Persist and stage the preference

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
- Produces: `SettingsData.ReducedMotionEnabled : bool`.
- Produces: scene-authored `%ReducedMotionLabel` and `%ReducedMotionCheck` on Display.

- [ ] **Step 1: Write failing data/persistence tests**

Extend the existing defaults test:

```csharp
AssertThat(defaults.ReducedMotionEnabled).IsFalse();
```

Extend the clone test:

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
```

and after reload:

```csharp
AssertThat(reloadedManager.GetSnapshot().ReducedMotionEnabled).IsTrue();
```

Add:

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

- [ ] **Step 2: Run the settings-data tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest"
```

Expected: compile/test failure because the new property does not exist.

- [ ] **Step 3: Implement settings persistence**

Add to `SettingsData`:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

Add to `Clone()`:

```csharp
ReducedMotionEnabled = this.ReducedMotionEnabled,
```

Add to `SettingsManager.Sanitize(...)`:

```csharp
ReducedMotionEnabled = data.ReducedMotionEnabled,
```

Do not edit `ApplyToRuntime(...)` for this setting.

- [ ] **Step 4: Run the Step 2 command and confirm GREEN**

Expected: PASS, including a valid old JSON that omits the field.

- [ ] **Step 5: Write failing Settings scene/controller tests**

Add to `SettingsMenuSceneTest.RequiredUniqueNodes`:

```csharp
"%ReducedMotionLabel",
"%ReducedMotionCheck",
```

Add:

```csharp
[TestCase]
public void OpenSettings_SetsReducedMotionCheckbox()
{
    var data = SettingsData.CreateDefaults();
    data.ReducedMotionEnabled = true;
    _ctrl.OpenSettings(data);

    AssertThat(GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed)
        .IsTrue();
}
```

In `CancelAfterStagedEdit_DoesNotMutateSnapshot_AndEmitsClosedOnce`, add:

```csharp
snapshot.ReducedMotionEnabled = false;
```

then after `OpenSettings(snapshot)`:

```csharp
GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed = true;
```

and after Cancel:

```csharp
AssertThat(snapshot.ReducedMotionEnabled).IsFalse();
```

In the existing real-manager `OnApplyPressed_WhenSelectionsAreUnset_DoesNotIndexPastOptions` test, before Apply add:

```csharp
GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed = true;
```

and after Apply add:

```csharp
AssertThat(settingsManager.GetSnapshot().ReducedMotionEnabled).IsTrue();
```

- [ ] **Step 6: Run the Settings UI tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: FAIL because the nodes/controller field do not exist.

- [ ] **Step 7: Author the Display row and staged binding**

Append under `%DisplayRows` after Resolution:

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

In `SettingsMenuController` add:

```csharp
private CheckBox _reducedMotionCheck = null!;
```

Bind it in `BindSceneNodes()`:

```csharp
_reducedMotionCheck = GetNode<CheckBox>("%ReducedMotionCheck");
```

Populate it with the other Display controls:

```csharp
_reducedMotionCheck.ButtonPressed = _editedSettings.ReducedMotionEnabled;
```

Add to the `SettingsData` candidate in `OnApplyPressed()`:

```csharp
ReducedMotionEnabled = _reducedMotionCheck.ButtonPressed,
```

Do not bind `Toggled` to persistence; Apply remains the write boundary.

- [ ] **Step 8: Run focused Settings suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: PASS.

```bash
git add scripts/settings/SettingsData.cs scripts/settings/SettingsManager.cs \
  scenes/ui/SettingsMenu.tscn scripts/ui/SettingsMenuController.cs \
  tests/settings/SettingsDataTest.cs tests/settings/SettingsManagerTest.cs \
  tests/ui/SettingsMenuControllerTest.cs tests/ui/SettingsMenuSceneTest.cs
git commit -m "feat(settings): add reduced motion preference"
```

---

### Task 2: Reduce Battle-local decorative motion

**Files:**
- Modify: `scripts/ui/BattleManager.cs`
- Test: `tests/ui/BattleManagerTest.cs`
- Test: `tests/ui/BattleSceneTest.cs`

**Interfaces:**
- Produces: `StartBattle(Character player, Enemy enemy, bool reducedMotionEnabled = false)`.
- Stores: private `_reducedMotionEnabled` for the Battle instance.
- Does not read `SettingsManager`.

- [ ] **Step 1: Write failing reduced tween tests**

Add to `BattleManagerTest`:

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

Keep the existing `StopBattleRuntime_KillsTrackedVisualTweens` as the normal attack-tween regression.

- [ ] **Step 2: Write failing real-scene idle tests**

Add to `BattleSceneTest`:

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

- [ ] **Step 3: Run Battle suites and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: reduced-state/parameter failures; current Battle always animates.

- [ ] **Step 4: Capture the startup flag before animation setup**

Add:

```csharp
private bool _reducedMotionEnabled;
```

Change the method declaration to:

```csharp
public void StartBattle(
    Character player,
    Enemy enemy,
    bool reducedMotionEnabled = false)
```

Immediately after the existing null guard and before `_player = player;`, add:

```csharp
_reducedMotionEnabled = reducedMotionEnabled;
```

`SetupCharacterAnimations()` already runs later in this method, so it sees the captured value.

- [ ] **Step 5: Make the two idle sprites static only in reduced mode**

Replace `_playerSprite.Play("idle")` with:

```csharp
if (_reducedMotionEnabled)
{
    _playerSprite.Stop();
    _playerSprite.Frame = 0;
}
else
{
    _playerSprite.Play("idle");
}
```

Replace `_enemySprite.Play("idle")` with:

```csharp
if (_reducedMotionEnabled)
{
    _enemySprite.Stop();
    _enemySprite.Frame = 0;
}
else
{
    _enemySprite.Play("idle");
}
```

Leave frame construction, loop metadata, scaling, modulation, materials, textures, and fallbacks unchanged.

- [ ] **Step 6: Remove attack scale/flash and damage translation in reduced mode**

In `PlayAttackAnimation(...)`, after its existing `sprite == null` guard add:

```csharp
if (_reducedMotionEnabled)
    return;
```

In `ShowDamageNumber(...)`, keep `CreateTrackedTween()`, `SetParallel(true)`, the opacity tween, and the 1-second delayed reset. Replace only the unconditional position tween with:

```csharp
var startPos = damageLabel.Position;
if (!_reducedMotionEnabled)
{
    var endPos = startPos + new Vector2(0, -30);
    tween.TweenProperty(damageLabel, "position", endPos, 1.0);
}
```

Keep:

```csharp
tween.TweenProperty(damageLabel, "modulate:a", 0.0f, 1.0);
```

Do not edit `_battleTimer.WaitTime = 1.5` or turn/progress logic.

- [ ] **Step 7: Run Battle suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: PASS.

```bash
git add scripts/ui/BattleManager.cs tests/ui/BattleManagerTest.cs tests/ui/BattleSceneTest.cs
git commit -m "feat(ui): reduce battle motion"
```

---

### Task 3: Pass the current setting from Game and verify the slice

**Files:**
- Modify: `scripts/game/Game.cs`
- Test: `tests/game/GameTest.cs`
- Audit only: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Audit only: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit only: `docs/PRD.md`
- Audit only: `CLAUDE.md`

**Interfaces:**
- Consumes: `SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled`.
- Calls: `BattleManager.StartBattle(Character, Enemy, bool)`.
- Fallback: unavailable `SettingsManager.Instance` -> `false`.

- [ ] **Step 1: Write a failing real-Game propagation test**

Add to `GameTest`:

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

Add:

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

The temporary manager stays out of the tree, so the test neither applies runtime settings nor writes files.

- [ ] **Step 2: Run `GameTest` and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest"
```

Expected: the hosted Battle starts with `_reducedMotionEnabled == false`.

- [ ] **Step 3: Pass the snapshot value at the existing Battle composition seam**

Immediately before `battle.StartBattle(...)` in `Game.OnBattleStarted(...)` add:

```csharp
var reducedMotionEnabled =
    SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled ?? false;
```

Change the call to:

```csharp
battle.StartBattle(
    _gameManager.Player,
    enemy,
    reducedMotionEnabled);
```

Do not cache the setting on `Game`, subscribe to changes, or edit Main Menu.

- [ ] **Step 4: Run all focused HPA-541 suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: PASS.

- [ ] **Step 5: Re-run the production-motion audit**

```bash
rg -n 'CreateTween|TweenProperty|SetAnimationLoop|\.Play\("idle"\)' scripts/ui \
  --glob '!scripts/ui/showcase/**'
```

Expected: player-facing motion ownership remains in `scripts/ui/BattleManager.cs`.

```bash
git diff main...HEAD -- scripts/ui/showcase/SiriusUiShowcase.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md docs/PRD.md CLAUDE.md
```

Expected: empty unless an existing statement became factually stale.

- [ ] **Step 6: Run full verification**

```bash
dotnet build Sirius.sln
dotnet test Sirius.sln --settings test.runsettings.local
git diff --check
```

Expected: build succeeds, all tests pass, and `git diff --check` prints no errors.

- [ ] **Step 7: Commit root propagation**

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "feat(game): pass reduced motion to battle"
```

- [ ] **Step 8: Record closeout evidence on this same PR**

```text
Verification:
- dotnet build Sirius.sln
- dotnet test Sirius.sln --settings test.runsettings.local
- git diff --check
- production motion survey: BattleManager remains the only player-facing UI motion owner
```

Mark this same PR ready only after implementation/review; do not create another PR for HPA-541.
