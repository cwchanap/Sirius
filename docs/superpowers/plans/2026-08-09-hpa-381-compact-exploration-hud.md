# HPA-381 Compact Exploration HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's draggable debug exploration overlay with a scene-authored compact HUD that shows supported player state, a binding-aware contextual interaction prompt, temporary floor/hint feedback, and no future-feature placeholders.

**Architecture:** Add one feature-local `ExplorationHud.tscn` plus `ExplorationHudController`. `Game` remains the gameplay/world-context owner and adapts existing `Character`, floor, target, scene-transition, and host-block state into the HUD's narrow presentation API. Reuse the existing Sirius Theme, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, art catalogue, and gameplay `UIScreenHost`; do not add another host, Theme, generic presenter framework, or domain state layer. The isolated HUD component lands first; production scene/code/prompt/lifecycle migration lands as one atomic second task so no committed bridge state references deleted prototype nodes.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme/components/art catalogue, and `UIScreenHost`.

## Global Constraints

- Primary target is desktop landscape with mouse, keyboard, and gamepad.
- Minimum supported logical resolution is 640×360.
- Use `SiriusUiMetrics.IsCompact(viewportSize)`; do not invent another breakpoint.
- Use `SiriusUiMetrics.SafeMargin(compact)` and a centred maximum content width of 1600 px.
- Validate every viewport in `SiriusUiMetrics.VerificationViewports`; use deeper layout assertions at 640×360 and 1280×720.
- Preserve current `GameManager.PlayerStatsChanged`, floor, battle, NPC, world-interaction, scene-transition, and `UIScreenHost` lifecycle semantics.
- `Game` keeps world-target validity and suppression decisions; the HUD controller must not query `GameManager`, `GridMap`, spawn groups, save state, or inventory.
- Show name, level, HP, MP, and thin EXP progress.
- Do not show Gold in exploration; the approved HPA-373 layout keeps it in inventory/shop.
- Do not show ATK, DEF, SPD, equipment bonuses, developer headings, drag controls, Lock, permanent instructions, area-colour documentation, or empty future-feature regions.
- Reuse `SiriusStatBar` for HP/MP and `SiriusContextPrompt`/`SiriusInputHint` for the interaction prompt.
- Reuse the existing `interact` InputMap action; do not add or rename input actions.
- Keep movement hinting session-scoped and fixed to the current direct WASD/arrow behavior; do not add movement InputMap actions or tutorial persistence.
- The exploration HUD is passive: all Controls in its subtree must ignore mouse input and own no focus.
- Explicitly assign `res://resources/ui/theme/SiriusTheme.tres` to the `ExplorationHud` root so free labels, panels, and the EXP bar inherit the Sirius Theme.
- Do not modify `GameManager`, `Character`, `PlayerController`, `GridMap`, `SiriusTheme.tres`, `SiriusUiMetrics`, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, or `scripts/ui/hosting/*` unless implementation proves the design impossible; reassess scope before doing so.
- Do not delete `scripts/ui/DraggablePanel.cs` merely because `Game.tscn` stops using it.
- Do not commit a production state where old prompt/HUD nodes are deleted but `UpdateInteractionPrompt()` or existing lifecycle tests still reference them.

---

## File map

**Production**
- Create `scenes/ui/ExplorationHud.tscn` — canonical passive HUD hierarchy, explicit Sirius Theme root, existing ornaments, and authored timers.
- Create `scripts/ui/ExplorationHudController.cs` — feature-local player display contract, responsive layout, passive-input policy, prompt, floor-title, and temporary-hint presentation.
- Modify `scenes/game/Game.tscn` — remove the prototype HUD/instructions and instance `ExplorationHud.tscn` under `UI/GameUI`.
- Modify `scripts/game/Game.cs` — atomically replace concrete HUD labels/runtime prompt construction with the HUD API while preserving gameplay/domain ownership.

**Tests**
- Create `tests/ui/ExplorationHudControllerTest.cs` — scene authorship, Theme inheritance, state binding, passive input, temporary surfaces, compact behavior, and viewport-fit coverage.
- Modify `tests/game/GameTest.cs` — production scene cutover, player-state binding, treasure/puzzle prompt semantics, prompt restoration, and floor-title integration.
- Modify `tests/game/GameInputLifecycleTest.cs` — migrate all old `UI/GameUI/InteractionPrompt` lifecycle contracts to the authored HUD and real resolver state.
- Modify `tests/game/GameplayPauseHostTest.cs` — prove the existing host gameplay-block callback hides stale prompt presentation.

---

### Task 1: Build the passive, responsive Exploration HUD component

**Files:**
- Create: `scenes/ui/ExplorationHud.tscn`
- Create: `scripts/ui/ExplorationHudController.cs`
- Create: `tests/ui/ExplorationHudControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusUiMetrics`, `SiriusThemeTypes`, `SiriusStatBar`, `SiriusContextPrompt`, `UiIconId`, the current hero sprite texture, existing orbit/callout ornaments, and `SiriusTheme.tres`.
- Produces: `ExplorationHudPlayerState`, `ExplorationHudController.ApplyPlayerState`, `ShowInteractionPrompt`, `HideInteractionPrompt`, `ShowAreaTitle`, and `ShowSessionHint`.

- [ ] **Step 1: Write the failing scene-authorship and Theme-root contract**

Create `tests/ui/ExplorationHudControllerTest.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ExplorationHudControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/ExplorationHud.tscn";

    private static readonly string[] RequiredNodes =
    {
        "%SafeFrame",
        "%HeroOrbitArc",
        "%HeroPlate",
        "%Portrait",
        "%PlayerName",
        "%PlayerLevel",
        "%HealthBar",
        "%ManaBar",
        "%ExperienceRow",
        "%ExperienceLabel",
        "%ExperienceBar",
        "%AreaTitle",
        "%PromptPlate",
        "%ContextPrompt",
        "%PromptConnector",
        "%HintPlate",
        "%HintLabel",
        "%AreaTitleTimer",
        "%HintTimer"
    };

    private static readonly string[] ProhibitedPrototypeNodes =
    {
        "HeaderTitle",
        "LockDrag",
        "Instructions",
        "PlayerAttackHUD",
        "PlayerDefenseHUD",
        "PlayerSpeedHUD",
        "PlayerGold"
    };

    [TestCase]
    public void SceneOwnsCompleteHudAndSiriusThemeBeforeReady()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        AssertThat(packed).IsNotNull();

        var hud = packed!.Instantiate<ExplorationHudController>();
        try
        {
            foreach (var path in RequiredNodes)
                AssertThat(hud.GetNodeOrNull(path)).IsNotNull();

            foreach (var nodeName in ProhibitedPrototypeNodes)
                AssertThat(hud.FindChild(nodeName, recursive: true, owned: false)).IsNull();

            AssertThat(hud.Theme).IsNotNull();
            AssertThat(hud.Theme.ResourcePath).IsEqual(SiriusThemeTypes.ResourcePath);
            AssertThat(hud.GetNode<PanelContainer>("%HeroPlate").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.HudPlate);
            AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.ExpBar);
        }
        finally
        {
            hud.Free();
        }
    }
}
```

- [ ] **Step 2: Run the component test and confirm the expected red state**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest"
```

Expected: FAIL loading `res://scenes/ui/ExplorationHud.tscn`.

- [ ] **Step 3: Author `ExplorationHud.tscn` with the Sirius Theme on its root**

Create the scene with these external resources:

```text
Script: res://scripts/ui/ExplorationHudController.cs
Theme: res://resources/ui/theme/SiriusTheme.tres
PackedScene: res://scenes/ui/components/SiriusStatBar.tscn
PackedScene: res://scenes/ui/components/SiriusContextPrompt.tscn
Texture2D: current player hero sprite sheet
Texture2D: res://assets/sprites/ui/ornaments/orbit_arc.png
Texture2D: res://assets/sprites/ui/ornaments/callout_connector.png
```

The root must explicitly set:

```text
[node name="ExplorationHud" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
theme = ExtResource("<sirius_theme_id>")
script = ExtResource("<controller_id>")
```

Author this hierarchy and mark the named nodes as unique in owner:

```text
ExplorationHud
├── SafeFrame (Control, full rect anchors; offsets controlled by controller)
│   ├── HeroOrbitArc (TextureRect, orbit_arc.png)
│   ├── HeroPlate (PanelContainer, SiriusHudPlate, top-left)
│   │   └── HeroContent (HBoxContainer)
│   │       ├── Portrait (TextureRect)
│   │       └── PlayerData (VBoxContainer)
│   │           ├── IdentityRow (HBoxContainer)
│   │           │   ├── PlayerName (Label, SiriusBody)
│   │           │   └── PlayerLevel (Label, SiriusMetadata)
│   │           ├── HealthBar (SiriusStatBar, Health)
│   │           ├── ManaBar (SiriusStatBar, Mana)
│   │           └── ExperienceRow (VBoxContainer)
│   │               ├── ExperienceLabel (Label, SiriusNumeric)
│   │               └── ExperienceBar (ProgressBar, SiriusExpBar)
│   ├── AreaTitle (Label, SiriusTitle, top-centre, hidden)
│   ├── PromptPlate (PanelContainer, SiriusHudPlate, bottom-centre, hidden)
│   │   └── PromptContent (VBoxContainer)
│   │       ├── ContextPrompt (SiriusContextPrompt)
│   │       └── PromptConnector (TextureRect, callout_connector.png)
│   └── HintPlate (PanelContainer, SiriusHudPlate, top-right, hidden)
│       └── HintLabel (Label, SiriusMetadata)
├── AreaTitleTimer (Timer, one_shot=true, wait_time=2.0)
└── HintTimer (Timer, one_shot=true, wait_time=4.0)
```

Each `PanelContainer` gets exactly one direct layout child. Keep positioning relative to `%SafeFrame`; do not author separate aspect-ratio layouts.

- [ ] **Step 4: Implement the feature-local display state and controller node binding**

Create `scripts/ui/ExplorationHudController.cs`:

```csharp
using Godot;
using System;

public readonly record struct ExplorationHudPlayerState(
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    int Experience,
    int ExperienceToNext);

public partial class ExplorationHudController : Control
{
    private static readonly StringName InteractAction = new("interact");
    private const float MaximumContentWidth = 1600f;

    private Control _safeFrame = null!;
    private TextureRect _portrait = null!;
    private Label _playerName = null!;
    private Label _playerLevel = null!;
    private SiriusStatBar _healthBar = null!;
    private SiriusStatBar _manaBar = null!;
    private VBoxContainer _experienceRow = null!;
    private Label _experienceLabel = null!;
    private ProgressBar _experienceBar = null!;
    private Label _areaTitle = null!;
    private PanelContainer _promptPlate = null!;
    private SiriusContextPrompt _contextPrompt = null!;
    private PanelContainer _hintPlate = null!;
    private Label _hintLabel = null!;
    private Timer _areaTitleTimer = null!;
    private Timer _hintTimer = null!;

    public override void _Ready()
    {
        BindNodes();
        MakePassive(this);
        _areaTitleTimer.Timeout += HideAreaTitle;
        _hintTimer.Timeout += HideSessionHint;
        GetViewport().SizeChanged += RefreshLayout;
        RefreshLayout();
    }

    private void BindNodes()
    {
        _safeFrame = GetNode<Control>("%SafeFrame");
        _portrait = GetNode<TextureRect>("%Portrait");
        _playerName = GetNode<Label>("%PlayerName");
        _playerLevel = GetNode<Label>("%PlayerLevel");
        _healthBar = GetNode<SiriusStatBar>("%HealthBar");
        _manaBar = GetNode<SiriusStatBar>("%ManaBar");
        _experienceRow = GetNode<VBoxContainer>("%ExperienceRow");
        _experienceLabel = GetNode<Label>("%ExperienceLabel");
        _experienceBar = GetNode<ProgressBar>("%ExperienceBar");
        _areaTitle = GetNode<Label>("%AreaTitle");
        _promptPlate = GetNode<PanelContainer>("%PromptPlate");
        _contextPrompt = GetNode<SiriusContextPrompt>("%ContextPrompt");
        _hintPlate = GetNode<PanelContainer>("%HintPlate");
        _hintLabel = GetNode<Label>("%HintLabel");
        _areaTitleTimer = GetNode<Timer>("%AreaTitleTimer");
        _hintTimer = GetNode<Timer>("%HintTimer");
    }

    public override void _ExitTree()
    {
        if (_areaTitleTimer != null)
            _areaTitleTimer.Timeout -= HideAreaTitle;
        if (_hintTimer != null)
            _hintTimer.Timeout -= HideSessionHint;

        var viewport = GetViewport();
        if (viewport != null)
            viewport.SizeChanged -= RefreshLayout;
    }
}
```

Do not create presentation nodes in C#.

- [ ] **Step 5: Implement player, prompt, title, and hint presentation**

Add:

```csharp
public void ApplyPlayerState(ExplorationHudPlayerState state)
{
    _playerName.Text = string.IsNullOrWhiteSpace(state.Name) ? "Adventurer" : state.Name;
    _playerLevel.Text = $"Lv {state.Level}";

    _healthBar.Label = "HP";
    _healthBar.Current = state.CurrentHealth;
    _healthBar.Maximum = state.MaxHealth;

    _manaBar.Visible = state.MaxMana > 0;
    if (_manaBar.Visible)
    {
        _manaBar.Label = "MP";
        _manaBar.Current = state.CurrentMana;
        _manaBar.Maximum = state.MaxMana;
    }

    _experienceRow.Visible = state.ExperienceToNext > 0;
    if (_experienceRow.Visible)
    {
        _experienceLabel.Text = $"EXP {state.Experience} / {state.ExperienceToNext}";
        _experienceBar.MaxValue = state.ExperienceToNext;
        _experienceBar.Value = Math.Clamp(state.Experience, 0, state.ExperienceToNext);
    }

    _portrait.Visible = _portrait.Texture != null;
}

public void ShowInteractionPrompt(string text, UiIconId icon)
{
    _contextPrompt.Prompt = text;
    _contextPrompt.ShowIcon = true;
    _contextPrompt.IconId = icon;
    _contextPrompt.Actions = new[] { InteractAction };
    _contextPrompt.Refresh();
    _promptPlate.Visible = true;
}

public void HideInteractionPrompt() => _promptPlate.Visible = false;

public void ShowAreaTitle(string title)
{
    if (string.IsNullOrWhiteSpace(title))
    {
        HideAreaTitle();
        return;
    }

    _areaTitle.Text = title;
    _areaTitle.Visible = true;
    _areaTitleTimer.Start();
}

public void ShowSessionHint(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        HideSessionHint();
        return;
    }

    _hintLabel.Text = text;
    _hintPlate.Visible = true;
    _hintTimer.Start();
}

private void HideAreaTitle() => _areaTitle.Visible = false;
private void HideSessionHint() => _hintPlate.Visible = false;
```

- [ ] **Step 6: Implement passive input and one responsive layout policy**

Add:

```csharp
private static void MakePassive(Node node)
{
    if (node is Control control)
    {
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        control.FocusMode = Control.FocusModeEnum.None;
    }

    foreach (var child in node.GetChildren())
        MakePassive(child);
}

private void RefreshLayout()
{
    var viewportSize = GetViewportRect().Size;
    var compact = SiriusUiMetrics.IsCompact(viewportSize);
    var margin = SiriusUiMetrics.SafeMargin(compact);
    var availableWidth = MathF.Max(0, viewportSize.X - 2 * margin);
    var contentWidth = MathF.Min(availableWidth, MaximumContentWidth);
    var sideInset = MathF.Max(margin, (viewportSize.X - contentWidth) / 2f);

    _safeFrame.OffsetLeft = sideInset;
    _safeFrame.OffsetRight = -sideInset;
    _safeFrame.OffsetTop = margin;
    _safeFrame.OffsetBottom = -margin;

    _portrait.CustomMinimumSize = compact ? new Vector2(40, 40) : new Vector2(56, 56);
    _healthBar.Compact = compact;
    _manaBar.Compact = compact;
    _contextPrompt.Compact = compact;
    _playerName.ThemeTypeVariation = compact ? SiriusThemeTypes.BodyCompact : SiriusThemeTypes.Body;
    _playerLevel.ThemeTypeVariation = compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
    _experienceLabel.ThemeTypeVariation = compact ? SiriusThemeTypes.NumericCompact : SiriusThemeTypes.Numeric;
    _areaTitle.ThemeTypeVariation = compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title;
    _hintLabel.ThemeTypeVariation = compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
}
```

`%SafeFrame` must use full-rect anchors in the scene so these offsets define the real safe frame.

- [ ] **Step 7: Add focused state, temporary-surface, passive-input, and viewport tests**

Use a `SubViewport` fixture that instantiates the production HUD scene and awaits one process frame.

Player state:

```csharp
[TestCase]
public async Task ApplyPlayerStateBindsSupportedStatsAndCollapsesMissingMana()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));

    hud.ApplyPlayerState(new ExplorationHudPlayerState(
        "Aster", 7, 73, 120, 21, 50, 340, 500));

    AssertThat(hud.GetNode<Label>("%PlayerName").Text).IsEqual("Aster");
    AssertThat(hud.GetNode<Label>("%PlayerLevel").Text).IsEqual("Lv 7");
    AssertThat(hud.GetNode<SiriusStatBar>("%HealthBar").Current).IsEqual(73);
    AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Current).IsEqual(21);
    AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").Value).IsEqual(340);

    hud.ApplyPlayerState(new ExplorationHudPlayerState(
        "Aster", 7, 73, 120, 0, 0, 340, 500));

    AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Visible).IsFalse();
}
```

Prompt/title/hint:

```csharp
[TestCase]
public async Task PromptAndTemporarySurfacesUseAuthoredPresentation()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));

    hud.ShowInteractionPrompt("Open", UiIconId.Reward);
    var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsTrue();
    AssertThat(prompt.Prompt).IsEqual("Open");
    AssertThat(prompt.IconId).IsEqual(UiIconId.Reward);
    AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));

    hud.ShowAreaTitle("Ground Floor");
    hud.GetNode<Timer>("%AreaTitleTimer").EmitSignal(Timer.SignalName.Timeout);
    AssertThat(hud.GetNode<Label>("%AreaTitle").Visible).IsFalse();

    hud.ShowSessionHint("Move with WASD or Arrow Keys");
    hud.GetNode<Timer>("%HintTimer").EmitSignal(Timer.SignalName.Timeout);
    AssertThat(hud.GetNode<PanelContainer>("%HintPlate").Visible).IsFalse();
}
```

Passive subtree:

```csharp
[TestCase]
public async Task HudSubtreeIsPassive()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));
    var controls = hud.FindChildren("*", "Control", recursive: true, owned: false)
        .OfType<Control>()
        .Prepend(hud);

    foreach (var control in controls)
    {
        AssertThat(control.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(control.FocusMode).IsEqual(Control.FocusModeEnum.None);
    }
}
```

Also:

- clear `%Portrait.Texture`, apply state, and prove name/level remain visible;
- loop through every `SiriusUiMetrics.VerificationViewports` size;
- show all temporary surfaces before the fit assertion;
- assert HeroPlate, AreaTitle, PromptPlate, and HintPlate have non-zero rectangles contained by `%SafeFrame`;
- at 640×360 assert portrait minimum 40×40;
- at 1280×720 assert portrait minimum 56×56; and
- at those two focus viewports assert the four visible surfaces do not overlap.

- [ ] **Step 8: Run Task 1 verification and commit the isolated component**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest"
dotnet build Sirius.sln --no-restore
```

Expected: component suite PASS and build exits 0.

Commit:

```bash
git add scenes/ui/ExplorationHud.tscn \
  scripts/ui/ExplorationHudController.cs \
  tests/ui/ExplorationHudControllerTest.cs
git commit -m "feat(ui): add compact exploration HUD component"
```

---

### Task 2: Atomically cut production gameplay over to the HUD

**Files:**
- Modify: `scenes/game/Game.tscn`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Consumes: `ExplorationHudController`, `ExplorationHudPlayerState`, existing `GameManager.PlayerStatsChanged`, `Game.UpdateInteractionPrompt()` target resolver, `IsGameplayInputSuppressed()`, `UIScreenHostOptions.GameplayInputBlockChanged`, `FloorManager.FloorLoaded`, and `Game.RequestSceneChange()`.
- Produces: one production `ExplorationHud` under `UI/GameUI`; player-state binding; binding-aware `Open`/`Use`/`Solve`; host/domain suppression and valid-target restoration; temporary floor title; one session-scoped movement hint; zero runtime references to the removed prototype prompt/HUD path.

- [ ] **Step 1: Add failing production scene and player-state tests in `GameTest`**

Add:

```csharp
[TestCase]
public async Task GameSceneUsesCompactExplorationHudWithoutPrototypeDebugControls()
{
    var gameScene = await InstantiateRealGameScene();
    try
    {
        AssertThat(gameScene.GetNodeOrNull<ExplorationHudController>(
            "UI/GameUI/ExplorationHud")).IsNotNull();
        AssertThat(gameScene.GetNodeOrNull("UI/GameUI/TopPanel")).IsNull();
        AssertThat(gameScene.GetNodeOrNull("UI/GameUI/Instructions")).IsNull();
    }
    finally
    {
        gameScene.Free();
        await AwaitFrames(1);
    }
}

[TestCase]
public async Task PlayerStatsChangedRefreshesExplorationHudIncludingMana()
{
    var gameScene = await InstantiateRealGameScene();
    try
    {
        var gameManager = gameScene.GetNode<GameManager>("GameManager");
        var hud = gameScene.GetNode<ExplorationHudController>(
            "UI/GameUI/ExplorationHud");

        gameManager.Player.CurrentHealth = 61;
        gameManager.Player.CurrentMana = 17;
        gameManager.Player.Experience = 42;
        gameManager.NotifyPlayerStatsChanged();
        await AwaitFrames(1);

        AssertThat(hud.GetNode<SiriusStatBar>("%HealthBar").Current).IsEqual(61);
        AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Current).IsEqual(17);
        AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").Value).IsEqual(42);
    }
    finally
    {
        gameScene.Free();
        await AwaitFrames(1);
    }
}
```

If `GameTest` lacks `InstantiateRealGameScene()`, factor its repeated production-scene loading pattern into a private test helper. Do not add a production-only test seam.

- [ ] **Step 2: Rewrite the existing treasure prompt test against the authored HUD before deleting the old label**

In `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt`, use:

```csharp
var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
```

Preserve the existing treasure arrangement/reward-once assertions and replace old `Label` assertions with:

```csharp
AssertThat(promptPlate.Visible).IsTrue();
AssertThat(prompt.Prompt).IsEqual("Open");
AssertThat(prompt.IconId).IsEqual(UiIconId.Reward);
AssertThat(prompt.Actions.Length).IsEqual(1);
AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));
```

During treasure opening, assert `promptPlate.Visible` becomes false. After the box is opened, assert it remains false.

Add focused runtime cases for:

- adjacent unsolved `PuzzleSwitchSpawn` → `Use`, `UiIconId.Puzzle`;
- adjacent unsolved `PuzzleRiddleSpawn` → `Solve`, `UiIconId.Puzzle`.

Use the existing runtime spawn registration helpers/patterns. Do not add prompt mappings for target types the current gameplay path does not expose.

- [ ] **Step 3: Migrate the three hard-coded prompt lifecycle contracts in `GameInputLifecycleTest`**

For `ConfiguredKeyboardCancel_DeclinesToTopmostRiddleWithoutOpeningHostedPause`, replace:

```csharp
var prompt = _realGame.GetNode<Label>("UI/GameUI/InteractionPrompt");
```

with:

```csharp
var hud = _realGame.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
```

Then assert `promptPlate.Visible` and `prompt.Prompt == "Solve"` before the riddle; hidden during world interaction; visible again after closing.

For `FloorReplacement_RebindsGridAndRefreshesPrompt`, remove the artificial:

```csharp
prompt.Visible = true;
```

Arrange a real unopened treasure next to the current player before the floor replacement, register it, set the facing direction, call `UpdateInteractionPrompt`, and assert `%PromptPlate` is visible with `Open`. After `floorManager.LoadFloor(1)` and the existing rebind wait, assert the new grid is bound and `%PromptPlate` is hidden because the previous-floor target no longer exists.

For `InteractionPrompt_HidesDuringBattleAndRestoresAfterEscape`, keep the existing adjacent treasure fixture, but assert `%PromptPlate` visible before battle, hidden after `StartBattle`, and visible again after `ForceCloseAsEscape()`.

Do not directly call `hud.ShowInteractionPrompt()` in these lifecycle tests; they protect the real resolver/lifecycle path.

- [ ] **Step 4: Add the host-block stale-prompt test and real-target Pause restoration test**

In `GameplayPauseHostTest` add the narrow host-boundary test:

```csharp
[TestCase]
public async Task HostGameplayBlockSuppressesStaleExplorationPrompt()
{
    var hud = _game!.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
    hud.ShowInteractionPrompt("Open", UiIconId.Reward);
    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsTrue();

    AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
    await AwaitFrames(2);

    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsFalse();
}
```

In `GameTest`, extend a real adjacent-treasure fixture to prove restoration:

1. arrange an unopened adjacent treasure;
2. call/trigger the real `UpdateInteractionPrompt()` path;
3. assert `Open` is visible;
4. invoke the real Pause path;
5. assert `%PromptPlate` is hidden;
6. emit Pause `%ResumeButton.Pressed`;
7. await host restoration; and
8. assert `Open` is visible again because the treasure is still valid.

The restoration assertion is acceptance-critical. Do not restore presentation directly through `ExplorationHudController`.

- [ ] **Step 5: Run the pre-cutover focused tests and confirm red state**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: new HUD-path tests fail because production `Game.tscn`/`Game.cs` still use the prototype HUD/prompt.

- [ ] **Step 6: Replace the prototype hierarchy in `Game.tscn`**

Remove:

```text
DraggablePanel ext_resource used by TopPanel
TopPanel and every child beneath it
Instructions
TopPanel-only StyleBoxFlat resources
TopPanel-only portrait AtlasTexture if ExplorationHud now owns it
```

Add one packed scene and one instance:

```text
UI
└── GameUI
    └── ExplorationHud (res://scenes/ui/ExplorationHud.tscn)
```

Keep `UI/UIScreenHost` unchanged. Keep `GameUI` as the `HudRoot` configured by `Game._EnterTree()`.

- [ ] **Step 7: Replace all old HUD fields and bind `_explorationHud` in `Game.cs`**

Remove:

```text
_playerNameLabel
_playerLevelLabel
_playerHealthLabel
_playerExperienceLabel
_playerGoldLabel
_interactionPromptLabel
EnsureInteractionPromptLabel()
```

Add:

```csharp
private ExplorationHudController _explorationHud = null!;
```

Bind in `_Ready()` before subscribing to `PlayerStatsChanged`:

```csharp
_explorationHud = GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
```

Delete all fallback lookups for old `TopPanel` paths. This prototype layout has no compatibility requirement.

- [ ] **Step 8: Collapse `UpdatePlayerUI()` into the one display-state adapter**

Replace its old label/bar/build-stat mutation with:

```csharp
private void UpdatePlayerUI()
{
    if (_isAbortInitialization || _gameManager?.Player == null || _explorationHud == null)
        return;

    var player = _gameManager.Player;
    _explorationHud.ApplyPlayerState(new ExplorationHudPlayerState(
        player.Name,
        player.Level,
        player.CurrentHealth,
        player.GetEffectiveMaxHealth(),
        player.CurrentMana,
        player.MaxMana,
        player.Experience,
        player.ExperienceToNext));
}
```

Keep all existing `UpdatePlayerUI()` call sites.

- [ ] **Step 9: Convert `UpdateInteractionPrompt()` completely in the same cutover**

Keep all current finders and target rules; replace only presentation mutation:

```csharp
private void UpdateInteractionPrompt()
{
    if (_explorationHud == null)
        return;

    if (_gridMap == null || _playerController == null || _gameManager == null ||
        _sceneChangeCommitted || IsGameplayInputSuppressed())
    {
        _explorationHud.HideInteractionPrompt();
        return;
    }

    Vector2I target = _gridMap.GetPlayerPosition() + _playerController.FacingDirection;

    var box = FindTreasureBoxAt(target);
    if (box != null &&
        !box.IsOpened &&
        !box.IsOpening &&
        !_gameManager.IsTreasureBoxOpened(box.TreasureBoxId))
    {
        _explorationHud.ShowInteractionPrompt("Open", UiIconId.Reward);
        return;
    }

    var puzzleSwitch = FindPuzzleSwitchAt(target);
    if (puzzleSwitch != null &&
        !string.IsNullOrWhiteSpace(puzzleSwitch.PuzzleId) &&
        !_gameManager.IsPuzzleSolved(puzzleSwitch.PuzzleId))
    {
        _explorationHud.ShowInteractionPrompt("Use", UiIconId.Puzzle);
        return;
    }

    var riddle = FindPuzzleRiddleAt(target);
    if (riddle != null &&
        !string.IsNullOrWhiteSpace(riddle.PuzzleId) &&
        !_gameManager.IsPuzzleSolved(riddle.PuzzleId))
    {
        _explorationHud.ShowInteractionPrompt("Solve", UiIconId.Puzzle);
        return;
    }

    _explorationHud.HideInteractionPrompt();
}
```

There must be no runtime-created `InteractionPrompt` label after this step.

- [ ] **Step 10: Refresh prompt state from the existing host-block callback**

Change the current host option callback from a single assignment to:

```csharp
GameplayInputBlockChanged = blocked =>
{
    _presentationGameplayBlocked = blocked;
    if (IsNodeReady())
        UpdateInteractionPrompt();
}
```

Do not add a second modal observer, host event, or presentation-state singleton.

- [ ] **Step 11: Wire floor title, session hint, and committed-navigation hiding**

After initial player UI setup in `_Ready()` show the one session hint:

```csharp
_explorationHud.ShowSessionHint("Move with WASD or Arrow Keys");
```

In `OnFloorLoaded(FloorDefinition floorDef, GridMap gridMap)`, after the HUD/grid references are valid:

```csharp
_explorationHud?.ShowAreaTitle(floorDef.FloorName);
```

In `RequestSceneChange(string path)`, immediately after:

```csharp
_sceneChangeCommitted = true;
```

call:

```csharp
UpdateInteractionPrompt();
```

so a committed navigation hides the prompt without waiting for another movement/domain event.

Do not add movement binding actions. The fixed movement hint documents the current direct WASD/arrow input behavior only.

- [ ] **Step 12: Run the entire focused cutover suite and build before committing**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet build Sirius.sln --no-restore
```

Expected: all focused tests PASS and build exits 0.

- [ ] **Step 13: Audit every stale prototype path before committing**

Run:

```bash
rg -n 'Player HUD|Lock position|PlayerAttackHUD|PlayerDefenseHUD|PlayerSpeedHUD|UI/GameUI/TopPanel|UI/GameUI/Instructions|UI/GameUI/InteractionPrompt|_interactionPromptLabel|EnsureInteractionPromptLabel' \
  scenes/game/Game.tscn \
  scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  tests/game/GameplayPauseHostTest.cs
```

Expected: no matches.

Also verify no production debug Gold path remains:

```bash
rg -n 'PlayerGold|Gold:' scenes/game/Game.tscn scripts/game/Game.cs
```

Expected: no matches from exploration HUD presentation.

- [ ] **Step 14: Commit the complete production cutover as one green slice**

Commit only after Steps 12–13 are green:

```bash
git add scenes/game/Game.tscn \
  scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): replace exploration debug HUD"
```

Do not split scene deletion, prompt conversion, or lifecycle-test migration into separate commits.

- [ ] **Step 15: Run full regression and scope verification**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local
dotnet build Sirius.sln --no-restore
```

Expected: full suite PASS; build exits 0.

Confirm the implementation diff is restricted to:

```text
scenes/ui/ExplorationHud.tscn
scripts/ui/ExplorationHudController.cs
scenes/game/Game.tscn
scripts/game/Game.cs
tests/ui/ExplorationHudControllerTest.cs
tests/game/GameTest.cs
tests/game/GameInputLifecycleTest.cs
tests/game/GameplayPauseHostTest.cs
```

Run:

```bash
git diff --name-only main...HEAD
```

The two approved HPA-381 design/plan documents may also be present if implementation starts from the planning branch. Any additional runtime file requires an explicit scope explanation before merge.

---

## Review-specific decisions

### Adopted

- Production cutover is atomic; the former Task 2/Task 3 bridge is removed.
- `GameInputLifecycleTest.cs` is explicitly part of the file map and cutover verification.
- `ExplorationHud` root explicitly receives `SiriusTheme.tres`, with a scene test asserting the relationship.
- Host-block restoration remains acceptance-critical and uses a real adjacent target after Resume.
- No fake mid-cutover green checkpoint remains.

### Intentionally not expanded

The optional suggestion to make the movement hint device/binding-aware is not adopted in HPA-381. Current movement is handled directly from WASD/arrow key events rather than a movement InputMap action. Introducing movement actions or remapping belongs to gameplay/input work, not this HUD presentation migration. The contextual `interact` prompt remains binding/device-aware through the existing `SiriusInputHint` path.