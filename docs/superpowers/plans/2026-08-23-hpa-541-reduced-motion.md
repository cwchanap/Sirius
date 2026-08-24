# HPA-541 Reduced Motion Preference Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a Reduced Motion preference and apply it to Sirius’s current exploration and Battle motion without introducing a global motion framework or changing gameplay timing.

**Architecture:** Extend the existing Settings model/UI. `Game` remains the composition root: it applies the current scalar to camera/`GridMap` world ownership and passes the same scalar explicitly into each new `BattleManager`. `PlayerDisplay` and `EnemySpawn` read only their existing parent `GridMap` scalar; Battle keeps its local combat timing and does not use `SiriusMotion`.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, System.Text.Json, GdUnit4.

**Spec:** `docs/superpowers/specs/2026-08-23-hpa-541-reduced-motion-design.md`

## Global Constraints

- Keep all planning and implementation in this one HPA-541 PR.
- `ReducedMotionEnabled` defaults to `false`; keep `SettingsData.CurrentVersion = 1`.
- No `MotionPolicy` object/service/singleton, settings event bus, observer, or shared-component lifecycle API.
- No production child reads `SettingsManager` directly.
- `scripts/ui/theme/SiriusMotion.cs` is **not** a Battle dependency. Its 100 ms reduced chrome timing must not replace Battle’s local 1-second damage fade.
- Reduced exploration disables camera smoothing and continuous player/enemy idle frame cycling; movement/input timing stays unchanged.
- Reduced Battle removes damage translation, attack scale/flash, and looping actor idle playback while keeping the 1-second opacity fade.
- Treasure Box opening remains a finite state-change animation; editor preview motion remains unchanged.
- Every `BattleManager.StartBattle(...)` caller passes the reduced-motion bool explicitly; no default parameter or compatibility overload.

---

## File map

### Settings

- `scripts/settings/SettingsData.cs`
- `scripts/settings/SettingsManager.cs`
- `scenes/ui/SettingsMenu.tscn`
- `scripts/ui/SettingsMenuController.cs`
- `tests/settings/SettingsDataTest.cs`
- `tests/settings/SettingsManagerTest.cs`
- `tests/ui/SettingsMenuControllerTest.cs`
- `tests/ui/SettingsMenuSceneTest.cs`

### Exploration/world

- `scripts/game/Game.cs`
- `scripts/game/GridMap.cs`
- `scripts/game/PlayerDisplay.cs`
- `scripts/game/EnemySpawn.cs`
- `tests/game/GameTest.cs`
- `tests/game/GridMapTest.cs` (new)
- `tests/game/PlayerDisplayTest.cs` (new)
- `tests/game/EnemySpawnTest.cs`

### Battle

- `scripts/ui/BattleManager.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/ui/BattleSceneTest.cs`
- `tests/game/GameTest.cs`

---

### Task 1: Persist and stage Reduced Motion safely

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
- Preserves: safe `SettingsData.Clone()` even when unsanitized JSON supplied null keybindings.
- Preserves: `SettingsManager.Sanitize(SettingsData)` validation while removing the plain-field whitelist rebuild.
- Produces: `%ReducedMotionLabel` and `%ReducedMotionCheck` on the existing Display page.

- [ ] **Step 1: Write failing SettingsData tests**

Extend defaults:

```csharp
AssertThat(SettingsData.CreateDefaults().ReducedMotionEnabled).IsFalse();
```

Extend clone preservation:

```csharp
var original = SettingsData.CreateDefaults();
original.ReducedMotionEnabled = true;
var cloned = original.Clone();

AssertThat(cloned.ReducedMotionEnabled).IsTrue();
cloned.ReducedMotionEnabled = false;
AssertThat(original.ReducedMotionEnabled).IsTrue();
```

Add a regression required by the `Sanitize -> data.Clone()` refactor:

```csharp
[TestCase]
public void SettingsData_Clone_NullKeybindingsFallsBackToDefaults()
{
    var data = SettingsData.CreateDefaults();
    data.PrimaryKeybindings = null!;

    var clone = data.Clone();

    AssertThat(clone.PrimaryKeybindings).IsNotNull();
    AssertThat(clone.PrimaryKeybindings["toggle_inventory"])
        .IsEqual((long)Key.I);
}
```

- [ ] **Step 2: Write failing SettingsManager persistence/sanitize tests**

Extend `SettingsManager_SaveAndReload_PreservesSettings`:

```csharp
candidate.ReducedMotionEnabled = true;
```

and after reboot:

```csharp
AssertThat(reloadedManager.GetSnapshot().ReducedMotionEnabled).IsTrue();
```

Add valid older JSON with no new field:

```csharp
[TestCase]
public async Task SettingsManager_Ready_SettingsWithoutReducedMotion_DefaultsFalse()
{
    File.WriteAllText(ProjectSettings.GlobalizePath("user://settings.json"), """
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

Pin null-keybinding sanitization through the load path so moving `Sanitize` to `Clone()` cannot regress it:

```csharp
[TestCase]
public async Task SettingsManager_Ready_NullKeybindingsStillNormalizesDefaults()
{
    File.WriteAllText(ProjectSettings.GlobalizePath("user://settings.json"), """
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
          "ReducedMotionEnabled": true,
          "PrimaryKeybindings": null
        }
        """);

    var manager = await BootstrapSettingsManager();
    var snapshot = manager.GetSnapshot();

    AssertThat(snapshot.ReducedMotionEnabled).IsTrue();
    AssertThat(snapshot.PrimaryKeybindings["pause_menu"])
        .IsEqual((long)Key.Escape);
}
```

- [ ] **Step 3: Run Settings data/persistence tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest"
```

Expected: compile failure because `ReducedMotionEnabled` does not exist.

- [ ] **Step 4: Add the field and make Clone safe for unsanitized JSON**

In `SettingsData` add:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

In `Clone()` add the bool and replace the keybinding copy with:

```csharp
ReducedMotionEnabled = ReducedMotionEnabled,
PrimaryKeybindings = PrimaryKeybindings is null
    ? CreateDefaultKeybindings()
    : new Dictionary<string, long>(PrimaryKeybindings)
```

Keep `CurrentVersion = 1`.

- [ ] **Step 5: Refactor Sanitize from whitelist rebuild to clone-then-normalize**

Keep the existing defaults/resolution checks, but use:

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
return sanitized;
```

Do not add a mapper or reflection copier. Plain bools such as Fullscreen, Auto Save, and Reduced Motion now carry through `Clone()`; validated fields are overwritten explicitly.

- [ ] **Step 6: Run Settings data/persistence tests and confirm GREEN**

Run the Step 3 command.

Expected: PASS, including null-keybindings and missing-Reduced-Motion JSON.

- [ ] **Step 7: Write failing Settings UI tests**

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

Extend the existing staged-Cancel test:

```csharp
snapshot.ReducedMotionEnabled = false;
_ctrl.OpenSettings(snapshot);
GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed = true;
InvokePrivate(_ctrl, "OnCancelPressed");
AssertThat(snapshot.ReducedMotionEnabled).IsFalse();
```

Add a **dedicated** Apply test; do not hide this assertion in the unrelated unset-selection bounds test:

```csharp
[TestCase]
public async Task OnApplyPressed_PersistsReducedMotionCheckbox()
{
    // Reuse the same window/file overrides and local SettingsManager setup
    // already used by the existing successful Apply test.
    var settingsManager = await BootstrapLocalSettingsManagerForApply();
    try
    {
        _ctrl.OpenSettings(settingsManager.GetSnapshot());
        GetField<CheckBox>(_ctrl, "_reducedMotionCheck").ButtonPressed = true;

        InvokePrivate(_ctrl, "OnApplyPressed");

        AssertThat(settingsManager.GetSnapshot().ReducedMotionEnabled).IsTrue();
    }
    finally
    {
        await FreeLocalSettingsManagerForApply(settingsManager);
    }
}
```

If the existing test does not yet expose those helper names, extract only its existing setup/cleanup into file-local test helpers; do not add production seams.

- [ ] **Step 8: Run Settings UI tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: authored node/controller field failures.

- [ ] **Step 9: Author and bind the Display row**

Append after Resolution under `%DisplayRows`:

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

In `SettingsMenuController`:

```csharp
private CheckBox _reducedMotionCheck = null!;
```

Bind:

```csharp
_reducedMotionCheck = GetNode<CheckBox>("%ReducedMotionCheck");
```

Populate:

```csharp
_reducedMotionCheck.ButtonPressed = _editedSettings.ReducedMotionEnabled;
```

Add to the Apply candidate:

```csharp
ReducedMotionEnabled = _reducedMotionCheck.ButtonPressed,
```

Do not edit `ApplyToRuntime(...)` and do not persist from `Toggled`.

- [ ] **Step 10: Run focused Settings suites and commit**

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

### Task 2: Bind Reduced Motion to the exploration world

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/game/GridMap.cs`
- Modify: `scripts/game/PlayerDisplay.cs`
- Modify: `scripts/game/EnemySpawn.cs`
- Create: `tests/game/GridMapTest.cs`
- Create: `tests/game/PlayerDisplayTest.cs`
- Modify: `tests/game/EnemySpawnTest.cs`
- Test: `tests/game/GameTest.cs`

**Interfaces:**
- Produces: `GridMap.ReducedMotionEnabled : bool`.
- `PlayerDisplay` and `EnemySpawn` consume only their existing `_gridMap.ReducedMotionEnabled`.
- `Game` produces `CurrentReducedMotionEnabled()` and `ApplyCurrentReducedMotionToWorld()` as private composition helpers.
- No new singleton/event interface.

- [ ] **Step 1: Write a failing GridMap runtime-loop test**

Create `tests/game/GridMapTest.cs` with the existing GdUnit runtime pattern and reflection helpers:

```csharp
[TestSuite]
[RequireGodotRuntime]
public partial class GridMapTest : Node
{
    [TestCase]
    public void ReducedMotion_RuntimeProcessResetsFrameAndStopsCycling()
    {
        var grid = new GridMap { ReducedMotionEnabled = true };
        AddChild(grid);
        try
        {
            SetPrivateField(grid, "_currentFrame", 2);
            SetPrivateField(grid, "_animationTime", 0.19f);

            grid._Process(0.2);

            AssertThat(GetPrivateField<int>(grid, "_currentFrame")).IsEqual(0);
            AssertThat(GetPrivateField<float>(grid, "_animationTime")).IsEqual(0f);
        }
        finally
        {
            grid.Free();
        }
    }
}
```

Use file-local reflection helpers; do not expose test-only production state.

- [ ] **Step 2: Write failing PlayerDisplay frame tests**

Create `tests/game/PlayerDisplayTest.cs`. Build a synthetic 4-frame texture so the test does not depend on asset loading:

```csharp
private static Texture2D CreateFourFrameTexture()
{
    var image = Image.CreateEmpty(384, 96, false, Image.Format.Rgba8);
    image.Fill(Colors.White);
    return ImageTexture.CreateFromImage(image);
}
```

Add reduced and normal cases by setting the existing private `_gridMap`, `Texture`, `RegionEnabled`, and initial `RegionRect`:

```csharp
[TestCase]
public void Process_ReducedMotionKeepsFrameZero()
{
    var grid = new GridMap { ReducedMotionEnabled = true };
    var display = new PlayerDisplay
    {
        Texture = CreateFourFrameTexture(),
        RegionEnabled = true,
        RegionRect = new Rect2(0, 0, 96, 96)
    };
    grid.AddChild(display);
    SetPrivateField(display, "_gridMap", grid);

    display._Process(0.2);

    AssertThat(display.RegionRect.Position.X).IsEqual(0f);
}
```

```csharp
[TestCase]
public void Process_DefaultMotionAdvancesOneFrame()
{
    var grid = new GridMap { ReducedMotionEnabled = false };
    var display = new PlayerDisplay
    {
        Texture = CreateFourFrameTexture(),
        RegionEnabled = true,
        RegionRect = new Rect2(0, 0, 96, 96)
    };
    grid.AddChild(display);
    SetPrivateField(display, "_gridMap", grid);

    display._Process(0.2);

    AssertThat(display.RegionRect.Position.X).IsEqual(96f);
}
```

Clean up both nodes per test.

- [ ] **Step 3: Write failing EnemySpawn frame tests**

Extend `EnemySpawnTest` with the same synthetic 384×96 texture and a small reflection helper. Configure the existing private `_gridMap`, `FrameWidth = 96`, `FrameHeight = 96`, `Texture`, `RegionEnabled`, and `RegionRect`.

Reduced:

```csharp
spawn._Process(0.2);
AssertThat(spawn.RegionRect.Position.X).IsEqual(0f);
```

Normal:

```csharp
spawn._Process(0.2);
AssertThat(spawn.RegionRect.Position.X).IsEqual(96f);
```

Do not change editor snap behavior.

- [ ] **Step 4: Run focused world-owner tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GridMapTest|FullyQualifiedName~PlayerDisplayTest|FullyQualifiedName~EnemySpawnTest"
```

Expected: compile failure because `GridMap.ReducedMotionEnabled` does not exist.

- [ ] **Step 5: Add the world scalar and stop GridMap runtime cycling**

In `GridMap` add:

```csharp
public bool ReducedMotionEnabled { get; set; }
```

Leave the editor-preview branch unchanged. Before the existing runtime `_animationTime += ...` block add:

```csharp
if (ReducedMotionEnabled)
{
    _animationTime = 0f;
    if (_currentFrame != 0)
    {
        _currentFrame = 0;
        QueueRedraw();
    }
    return;
}
```

Do not remove the legacy animation path; normal/non-baked behavior stays unchanged.

- [ ] **Step 6: Freeze PlayerDisplay and EnemySpawn frame cycling via the GridMap scalar**

At the top of `PlayerDisplay._Process(...)`, after the texture/region guard:

```csharp
if (_gridMap?.ReducedMotionEnabled == true)
{
    _animTimer = 0f;
    if (_currentFrame != 0)
    {
        _currentFrame = 0;
        RegionRect = new Rect2(0, 0, FrameWidth, FrameHeight);
    }
    return;
}
```

In the **runtime** branch of `EnemySpawn._Process(...)`, before `_animTimer += ...`:

```csharp
if (_gridMap?.ReducedMotionEnabled == true)
{
    _animTimer = 0f;
    if (_currentFrame != 0)
    {
        _currentFrame = 0;
        RegionRect = new Rect2(0, 0, FrameWidth, FrameHeight);
    }
    return;
}
```

Do not affect `Engine.IsEditorHint()` behavior.

- [ ] **Step 7: Run world-owner tests and confirm GREEN**

Run the Step 4 command.

Expected: PASS.

- [ ] **Step 8: Write failing real-Game world propagation tests**

In `GameTest`, reuse the out-of-tree `SettingsManager.Instance` swap pattern from the current HPA-541 plan. Add a test that installs a snapshot with `ReducedMotionEnabled = true`, then instantiates the real Game:

```csharp
[TestCase]
public async Task GameReady_ReducedMotionDisablesCameraAndMarksCurrentGrid()
{
    var previousSettingsManager = SettingsManager.Instance;
    var settingsManager = CreateOutOfTreeSettingsManager(reducedMotionEnabled: true);
    SetSettingsManagerInstance(settingsManager);
    Game? game = null;
    try
    {
        game = await InstantiateRealGameScene();
        var camera = game.GetNode<Camera2D>("Camera2D");
        var grid = game.GetNode<FloorManager>("FloorManager").CurrentGridMap;

        AssertThat(camera.PositionSmoothingEnabled).IsFalse();
        AssertThat(grid.ReducedMotionEnabled).IsTrue();
    }
    finally
    {
        if (game != null && IsInstanceValid(game)) game.Free();
        SetSettingsManagerInstance(previousSettingsManager);
        settingsManager.Free();
        await AwaitFrames(1);
    }
}
```

Add a focused test for gameplay Settings reapplication. Open Pause -> Settings using the existing hosted fixture/real Game helpers, change the persisted snapshot to true before emitting Settings `Closed`, then assert the current camera/GridMap are reduced after close. The test must use the actual `OnHostedSettingsClosed` path; do not call the future helper directly.

- [ ] **Step 9: Run GameTest and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest"
```

Expected: current Game leaves camera smoothing enabled and does not set the GridMap scalar.

- [ ] **Step 10: Add Game composition helpers and lifecycle calls**

Add:

```csharp
private bool CurrentReducedMotionEnabled() =>
    SettingsManager.Instance?.GetSnapshot().ReducedMotionEnabled ?? false;

private void ApplyCurrentReducedMotionToWorld()
{
    var reduced = CurrentReducedMotionEnabled();

    if (_camera != null)
        _camera.PositionSmoothingEnabled = EnableCameraSmoothing && !reduced;

    if (_gridMap != null)
        _gridMap.ReducedMotionEnabled = reduced;
}
```

After current camera smoothing/zoom setup in `_Ready()`:

```csharp
ApplyCurrentReducedMotionToWorld();
```

After `_gridMap = gridMap;` in `OnFloorLoaded(...)`:

```csharp
ApplyCurrentReducedMotionToWorld();
```

Replace the expression-bodied Settings close handler with:

```csharp
private void OnHostedSettingsClosed()
{
    TryCloseHostedSettings(UIScreenCloseReason.ExplicitAction);
    ApplyCurrentReducedMotionToWorld();
}
```

This safely reapplies the same value on Cancel and the new value after successful Apply. Do not add a settings event or cache.

- [ ] **Step 11: Run focused world + Game suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GridMapTest|FullyQualifiedName~PlayerDisplayTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~GameTest"
```

Expected: PASS.

```bash
git add scripts/game/Game.cs scripts/game/GridMap.cs scripts/game/PlayerDisplay.cs \
  scripts/game/EnemySpawn.cs tests/game/GameTest.cs tests/game/GridMapTest.cs \
  tests/game/PlayerDisplayTest.cs tests/game/EnemySpawnTest.cs
git commit -m "feat(game): reduce exploration motion"
```

---

### Task 3: Apply Reduced Motion to Battle and pass the required value

**Files:**
- Modify: `scripts/ui/BattleManager.cs`
- Modify: `scripts/game/Game.cs`
- Test: `tests/ui/BattleManagerTest.cs`
- Test: `tests/ui/BattleSceneTest.cs`
- Test: `tests/game/GameTest.cs`

**Interfaces:**
- Produces: `BattleManager.StartBattle(Character player, Enemy enemy, bool reducedMotionEnabled)` with **no default**.
- Consumes: `Game.CurrentReducedMotionEnabled()`.
- Internal: private `_reducedMotionEnabled` captured before `SetupCharacterAnimations()`.
- Prohibits: `SiriusMotion` usage in `BattleManager`.

- [ ] **Step 1: Update existing Battle test call sites to make intent explicit**

Before changing the signature, inventory current callers:

```bash
rg -n '\.StartBattle\(' tests/ui scripts/game/Game.cs
```

For each existing BattleManager call in `BattleManagerTest` and `BattleSceneTest`, change two-argument startup to:

```csharp
battle.StartBattle(player, enemy, reducedMotionEnabled: false);
```

Do not add an overload to avoid these edits.

- [ ] **Step 2: Write failing deterministic reduced Battle tween tests**

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

        var tweens = GetPrivateField<HashSet<Tween>>(manager, "_visualTweens");
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

For damage, drive the tracked tween instead of wall-clock time:

```csharp
[TestCase]
public async Task ShowDamageNumber_ReducedMotionKeepsPositionAndUsesOneSecondFade()
{
    var manager = await CreateReadyBattleManager();
    try
    {
        SetPrivateField(manager, "_reducedMotionEnabled", true);
        var label = new Label { Position = new Vector2(12f, 34f) };
        manager.AddChild(label);
        var resting = label.Position;

        InvokePrivateMethod(manager, "ShowDamageNumber", label, 25, false);

        var tweens = GetPrivateField<HashSet<Tween>>(manager, "_visualTweens");
        AssertThat(tweens.Count).IsEqual(1);
        var tween = tweens.Single();
        tween.Pause();
        tween.CustomStep(0.5d);

        AssertThat(label.Position).IsEqual(resting);
        AssertThat(label.Modulate.A).IsEqualApprox(0.5f, 0.05f);

        tween.CustomStep(0.5d);
        AssertThat(label.Position).IsEqual(resting);
        AssertThat(label.Modulate.A).IsEqualApprox(0f, 0.05f);
    }
    finally
    {
        await FreeManager(manager);
    }
}
```

If the delayed reset callback requires one additional zero/small `CustomStep` after the second half, step it deterministically and assert the final hidden/resting state; do not use a `SceneTreeTimer`.

- [ ] **Step 3: Write failing real-scene visibility/stillness tests**

In `BattleSceneTest` add:

```csharp
[TestCase]
public async Task ReducedMotion_StartBattleShowsStaticIdleFrame()
{
    _battle.StartBattle(
        TestHelpers.CreateTestCharacter(),
        Enemy.CreateGoblin(),
        reducedMotionEnabled: true);
    await AwaitFrames(2);

    foreach (var path in new[]
             {
                 "%PlayerSpriteContainer/PlayerSprite",
                 "%EnemySpriteContainer/EnemySprite"
             })
    {
        var sprite = _battle.GetNode<AnimatedSprite2D>(path);
        AssertThat(sprite.Animation.ToString()).IsEqual("idle");
        AssertThat(sprite.Frame).IsEqual(0);
        AssertThat(sprite.IsPlaying()).IsFalse();
        AssertThat(sprite.SpriteFrames.GetFrameTexture("idle", 0)).IsNotNull();
    }
}
```

Keep/add normal startup coverage:

```csharp
_battle.StartBattle(player, enemy, reducedMotionEnabled: false);
AssertThat(playerSprite.IsPlaying()).IsTrue();
AssertThat(enemySprite.IsPlaying()).IsTrue();
```

The texture assertion is load-bearing: `IsPlaying == false` plus `Frame == 0` alone also passes on an invisible empty `default` animation.

- [ ] **Step 4: Change StartBattle to a required bool and capture it before actor setup**

Add:

```csharp
private bool _reducedMotionEnabled;
```

Change:

```csharp
public void StartBattle(
    Character player,
    Enemy enemy,
    bool reducedMotionEnabled)
```

After the existing null guard, before assigning `_player` / calling `SetupCharacterAnimations()`:

```csharp
_reducedMotionEnabled = reducedMotionEnabled;
```

No default and no two-argument overload.

- [ ] **Step 5: Keep reduced idle actors visible**

Replace the player unconditional `Play("idle")` with:

```csharp
if (_reducedMotionEnabled)
{
    _playerSprite.Animation = "idle";
    _playerSprite.Frame = 0;
    _playerSprite.Stop();
}
else
{
    _playerSprite.Play("idle");
}
```

Do the same for enemy:

```csharp
if (_reducedMotionEnabled)
{
    _enemySprite.Animation = "idle";
    _enemySprite.Frame = 0;
    _enemySprite.Stop();
}
else
{
    _enemySprite.Play("idle");
}
```

Do not remove the `idle` animation or loop metadata from `SpriteFrames`; reduced mode simply does not play it.

- [ ] **Step 6: Remove Battle translation/scale/flash only**

In `PlayAttackAnimation(...)`, after its null guard:

```csharp
if (_reducedMotionEnabled)
    return;
```

In `ShowDamageNumber(...)`, keep the tracked parallel tween, opacity tween, and 1-second reset. Guard only the position tween:

```csharp
var startPos = damageLabel.Position;
if (!_reducedMotionEnabled)
{
    var endPos = startPos + new Vector2(0, -30);
    tween.TweenProperty(damageLabel, "position", endPos, 1.0);
}

tween.TweenProperty(damageLabel, "modulate:a", 0.0f, 1.0);
```

Do not use `SiriusMotion.Duration(...)`, `SiriusMotion.UseTransform(...)`, or change `_battleTimer.WaitTime`.

- [ ] **Step 7: Pass the current bool from Game**

Change the one production call in `Game.OnBattleStarted(...)` to:

```csharp
battle.StartBattle(
    _gameManager.Player,
    enemy,
    CurrentReducedMotionEnabled());
```

No extra snapshot cache is needed.

- [ ] **Step 8: Extend the real-Game propagation test**

Using the same out-of-tree SettingsManager setup from Task 2, start a real Battle and assert:

```csharp
var manager = game.GetNode<GameManager>("GameManager");
manager.StartBattle(Enemy.CreateGoblin());
await AwaitFrames(2);

var battle = GetPrivateField<BattleManager>(game, "_battleManager");
AssertThat(GetPrivateField<bool>(battle, "_reducedMotionEnabled")).IsTrue();
```

Cleanly cancel/close the Battle before freeing the Game so no hosted state leaks to later tests.

- [ ] **Step 9: Run focused Battle/Game suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameTest"
```

Expected: PASS.

- [ ] **Step 10: Enforce explicit callers and the SiriusMotion boundary**

```bash
rg -n '\.StartBattle\([^,]+,[^,]+\)' scripts tests
```

Expected: no two-argument `BattleManager.StartBattle` call remains. Ignore `GameManager.StartBattle(Enemy)` domain calls; if the regex catches them, inspect rather than mechanically editing them.

```bash
rg -n 'SiriusMotion' scripts/ui/BattleManager.cs
```

Expected: no matches.

- [ ] **Step 11: Commit Battle/root propagation**

```bash
git add scripts/ui/BattleManager.cs scripts/game/Game.cs \
  tests/ui/BattleManagerTest.cs tests/ui/BattleSceneTest.cs tests/game/GameTest.cs
git commit -m "feat(ui): apply reduced motion to battle"
```

---

## Final verification and scope audit

- [ ] **Step 1: Run every focused HPA-541 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~GridMapTest|FullyQualifiedName~PlayerDisplayTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: PASS.

- [ ] **Step 2: Run the broad production-motion inventory**

```bash
rg -n 'CreateTween|TweenProperty|QueueRedraw|_currentFrame|_animTimer|RegionRect|\.Play\(|PositionSmoothing|AnimationPlayer|AnimatedSprite2D' \
  scripts --glob '!scripts/ui/showcase/**'
```

Classify every player-facing hit before closeout. Expected known results include:

- `BattleManager` — handled;
- `Game` camera smoothing — handled;
- `GridMap` runtime frame/redraw loop — handled through the world scalar; editor preview retained;
- `PlayerDisplay` — handled;
- `EnemySpawn` — handled;
- `NpcSpawn` — editor-only process/static runtime, retained;
- `TreasureBoxSpawn` — finite state-change animation, retained deliberately.

If another player-facing continuous/decorative motion owner appears, do not declare the audit complete; either bind the same scalar locally or document why it is semantically required.

- [ ] **Step 3: Re-check boundaries**

```bash
rg -n 'SiriusMotion|SettingsManager' \
  scripts/ui/BattleManager.cs scripts/game/GridMap.cs \
  scripts/game/PlayerDisplay.cs scripts/game/EnemySpawn.cs
```

Expected:

- no `SiriusMotion` in Battle;
- no `SettingsManager` reads in Battle/GridMap/PlayerDisplay/EnemySpawn.

- [ ] **Step 4: Run full verification**

```bash
dotnet build Sirius.sln
dotnet test Sirius.sln --settings test.runsettings.local
git diff --check
```

Expected: build succeeds, all tests pass, `git diff --check` is clean.

- [ ] **Step 5: Audit unrelated docs/code**

```bash
git diff main...HEAD -- \
  scripts/ui/showcase/SiriusUiShowcase.cs \
  scripts/ui/theme/SiriusMotion.cs \
  scripts/game/NpcSpawn.cs scripts/game/TreasureBoxSpawn.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md docs/PRD.md CLAUDE.md
```

Expected: empty unless an existing factual statement became stale. Do not edit these just to mention HPA-541.

- [ ] **Step 6: Record closeout evidence on this same PR**

```text
Verification:
- focused HPA-541 suites: PASS
- dotnet build Sirius.sln: PASS
- dotnet test Sirius.sln --settings test.runsettings.local: PASS
- git diff --check: PASS
- broad motion inventory: every player-facing result classified
- BattleManager SiriusMotion dependency: none
- production children reading SettingsManager: none
```

Mark this same PR ready only after implementation and review. Do not open a second HPA-541 PR.