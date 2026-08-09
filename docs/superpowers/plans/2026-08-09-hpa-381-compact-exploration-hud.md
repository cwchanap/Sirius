# HPA-381 Compact Exploration HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's draggable debug exploration overlay with a scene-authored compact HUD that shows supported player state, a binding-aware contextual interaction prompt, temporary floor/hint feedback, and no future-feature placeholders.

**Architecture:** Add one feature-local `ExplorationHud.tscn` plus `ExplorationHudController`. `Game` remains the gameplay/world-context owner and adapts existing `Character`, floor, target, and host-block state into the HUD's narrow presentation API. Reuse the existing Sirius Theme, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, art catalogue, and gameplay `UIScreenHost`; do not add another host, theme, generic presenter framework, or domain state layer.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme/components/art catalogue, and `UIScreenHost`.

## Global Constraints

- Primary target is desktop landscape with mouse, keyboard, and gamepad.
- Minimum supported logical resolution is 640×360.
- Use `SiriusUiMetrics.IsCompact(viewportSize)`; do not invent another breakpoint.
- Use `SiriusUiMetrics.SafeMargin(compact)` and a centred maximum content width of 1600 px.
- Validate every viewport in `SiriusUiMetrics.VerificationViewports`; use deeper layout assertions at 640×360 and 1280×720.
- Preserve current `GameManager.PlayerStatsChanged`, floor, battle, NPC, world-interaction, and `UIScreenHost` lifecycle semantics.
- `Game` keeps world-target validity and suppression decisions; the HUD controller must not query `GameManager`, `GridMap`, spawn groups, save state, or inventory.
- Show name, level, HP, MP, and thin EXP progress.
- Do not show Gold in exploration; the approved HPA-373 layout keeps it in inventory/shop.
- Do not show ATK, DEF, SPD, equipment bonuses, developer headings, drag controls, Lock, permanent instructions, area-colour documentation, or empty future-feature regions.
- Reuse `SiriusStatBar` for HP/MP and `SiriusContextPrompt`/`SiriusInputHint` for the interaction prompt.
- Reuse the existing `interact` InputMap action; do not add or rename input actions.
- Keep movement hinting session-scoped; do not add persistence/tutorial state.
- The exploration HUD is passive: all Controls in its subtree must ignore mouse input and own no focus.
- Do not modify `GameManager`, `Character`, `PlayerController`, `GridMap`, `SiriusTheme.tres`, `SiriusUiMetrics`, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, or `scripts/ui/hosting/*` unless implementation proves the design impossible; reassess scope before doing so.
- Do not delete `scripts/ui/DraggablePanel.cs` merely because `Game.tscn` stops using it.

---

## File map

**Production**
- Create `scenes/ui/ExplorationHud.tscn` — canonical passive HUD hierarchy and authored timers.
- Create `scripts/ui/ExplorationHudController.cs` — feature-local player display contract, responsive layout, passive-input policy, prompt, floor-title, and temporary-hint presentation.
- Modify `scenes/game/Game.tscn` — remove the prototype HUD/instructions and instance `ExplorationHud.tscn` under `UI/GameUI`.
- Modify `scripts/game/Game.cs` — replace concrete HUD labels/runtime prompt construction with the new HUD API while preserving gameplay/domain ownership.

**Tests**
- Create `tests/ui/ExplorationHudControllerTest.cs` — scene authorship, state binding, passive input, temporary surfaces, compact behavior, and viewport-fit coverage.
- Modify `tests/game/GameTest.cs` — production scene cutover, player-state binding, interaction prompt semantics, prompt restoration, and floor-title integration.
- Modify `tests/game/GameplayPauseHostTest.cs` — prove a stale visible prompt is suppressed by the existing host gameplay-block boundary.

---

### Task 1: Build the passive, responsive Exploration HUD component

**Files:**
- Create: `scenes/ui/ExplorationHud.tscn`
- Create: `scripts/ui/ExplorationHudController.cs`
- Create: `tests/ui/ExplorationHudControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusUiMetrics`, `SiriusThemeTypes`, `SiriusStatBar`, `SiriusContextPrompt`, `UiIconId`, the current hero sprite texture, and existing orbit/callout ornaments.
- Produces: `ExplorationHudPlayerState`, `ExplorationHudController.ApplyPlayerState`, `ShowInteractionPrompt`, `HideInteractionPrompt`, `ShowAreaTitle`, and `ShowSessionHint`.

- [ ] **Step 1: Write the failing scene-authorship contract**

Create `tests/ui/ExplorationHudControllerTest.cs` with a pre-`_Ready()` test so static HUD structure cannot drift back into runtime construction:

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
    public void SceneOwnsCompleteHudWithoutPrototypeNodes()
    {
        var packed = GD.Load<PackedScene>("res://scenes/ui/ExplorationHud.tscn");
        AssertThat(packed).IsNotNull();

        var hud = packed!.Instantiate<ExplorationHudController>();
        try
        {
            foreach (var path in RequiredNodes)
                AssertThat(hud.GetNodeOrNull(path)).IsNotNull();

            foreach (var nodeName in ProhibitedPrototypeNodes)
                AssertThat(hud.FindChild(nodeName, recursive: true, owned: false)).IsNull();
        }
        finally
        {
            hud.Free();
        }
    }
}
```

- [ ] **Step 2: Run the new test and confirm the expected red state**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest"
```

Expected: FAIL loading `res://scenes/ui/ExplorationHud.tscn`.

- [ ] **Step 3: Author `ExplorationHud.tscn` entirely from existing UI building blocks**

Create this hierarchy and mark the required nodes above as unique in owner:

```text
ExplorationHud (Control, full rect)
├── SafeFrame (Control, full rect anchors; offsets controlled by controller)
│   ├── HeroOrbitArc (TextureRect, existing orbit_arc.png)
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
│   │       └── PromptConnector (TextureRect, existing callout_connector.png)
│   └── HintPlate (PanelContainer, SiriusHudPlate, top-right, hidden)
│       └── HintLabel (Label, SiriusMetadata)
├── AreaTitleTimer (Timer, one_shot=true, wait_time=2.0)
└── HintTimer (Timer, one_shot=true, wait_time=4.0)
```

Use the current player atlas texture already referenced by `Game.tscn` for `Portrait`. Each `PanelContainer` has one layout child. No new texture or Theme resource is required.

Anchor HeroPlate, AreaTitle, PromptPlate, and HintPlate relative to SafeFrame. Keep the portrait, bars, and hint width compact in the scene; runtime code should only switch shared compact sizing and safe-frame insets, not rebuild hierarchy.

- [ ] **Step 4: Implement the feature-local HUD controller**

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

    public override void _ExitTree()
    {
        if (_areaTitleTimer != null)
            _areaTitleTimer.Timeout -= HideAreaTitle;
        if (_hintTimer != null)
            _hintTimer.Timeout -= HideSessionHint;
        if (GetViewport() != null)
            GetViewport().SizeChanged -= RefreshLayout;
    }

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
        _promptPlate.Visible = true;
        _contextPrompt.Refresh();
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
}
```

Implement `BindNodes()` using the authored unique names. Do not call `new Label`, `new ProgressBar`, or build presentation nodes in this controller.

Make the whole feature subtree passive after every child is ready:

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
```

Implement one responsive calculation:

```csharp
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

SafeFrame must use full-rect anchors in the scene so these offsets form the actual safe rectangle.

- [ ] **Step 5: Add focused controller tests**

Add a SubViewport fixture that instantiates the production HUD scene and awaits one process frame.

Cover player state:

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

Cover portrait fallback by clearing `%Portrait.Texture`, applying state, and asserting the portrait is hidden while `%PlayerName` and `%PlayerLevel` remain visible.

Cover prompt and timer-owned surfaces:

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
    AssertThat(prompt.Actions.Length).IsEqual(1);
    AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));

    hud.ShowAreaTitle("Ground Floor");
    hud.GetNode<Timer>("%AreaTitleTimer").EmitSignal(Timer.SignalName.Timeout);
    AssertThat(hud.GetNode<Label>("%AreaTitle").Visible).IsFalse();

    hud.ShowSessionHint("Move with WASD or Arrow Keys");
    hud.GetNode<Timer>("%HintTimer").EmitSignal(Timer.SignalName.Timeout);
    AssertThat(hud.GetNode<PanelContainer>("%HintPlate").Visible).IsFalse();
}
```

Cover passive input recursively:

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

Loop through `SiriusUiMetrics.VerificationViewports`, resize the SubViewport, await one process frame, and assert `%HeroPlate`, `%AreaTitle` when shown, `%PromptPlate` when shown, and `%HintPlate` when shown have non-zero rectangles contained by `%SafeFrame`. At 640×360 assert the portrait minimum is 40×40; at 1280×720 assert 56×56. At those two sizes also assert the four surfaces do not overlap each other after all are shown.

- [ ] **Step 6: Run Task 1 tests and commit**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest"
```

Expected: PASS.

Commit:

```bash
git add scenes/ui/ExplorationHud.tscn \
  scripts/ui/ExplorationHudController.cs \
  tests/ui/ExplorationHudControllerTest.cs
git commit -m "feat(ui): add compact exploration HUD component"
```

---

### Task 2: Replace the debug Game HUD and bind supported player state

**Files:**
- Modify: `scenes/game/Game.tscn`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`

**Interfaces:**
- Consumes: `ExplorationHudController`, `ExplorationHudPlayerState`, existing `GameManager.PlayerStatsChanged`, and `Character` values.
- Produces: one production `ExplorationHud` instance under `UI/GameUI`; `Game.UpdatePlayerUI()` as the sole adapter from player domain state to HUD state.

- [ ] **Step 1: Add failing production-cutover and player-binding tests**

Add a real-scene test using the same fixture style already present in `GameTest`:

```csharp
[TestCase]
public async Task GameSceneUsesCompactExplorationHudWithoutPrototypeDebugControls()
{
    var gameScene = await InstantiateRealGameScene();
    try
    {
        AssertThat(gameScene.GetNodeOrNull<ExplorationHudController>("UI/GameUI/ExplorationHud"))
            .IsNotNull();
        AssertThat(gameScene.GetNodeOrNull("UI/GameUI/TopPanel")).IsNull();
        AssertThat(gameScene.GetNodeOrNull("UI/GameUI/Instructions")).IsNull();
    }
    finally
    {
        gameScene.Free();
        await AwaitFrames(1);
    }
}
```

If `GameTest` does not yet have `InstantiateRealGameScene()`, factor its existing repeated `GD.Load<PackedScene>("res://scenes/game/Game.tscn")` + `AddChild` + `AwaitFrames` setup into a private test helper. Do not add a production seam solely for this test.

Add player-state binding through the current signal:

```csharp
[TestCase]
public async Task PlayerStatsChangedRefreshesExplorationHudIncludingMana()
{
    var gameScene = await InstantiateRealGameScene();
    try
    {
        var gameManager = gameScene.GetNode<GameManager>("GameManager");
        var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");

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

- [ ] **Step 2: Run focused Game tests and confirm the expected red state**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest"
```

Expected: FAIL because the production scene still owns `TopPanel`/`Instructions` and has no `ExplorationHud`.

- [ ] **Step 3: Replace the prototype hierarchy in `Game.tscn`**

Remove from `Game.tscn`:

```text
DraggablePanel ext_resource used by TopPanel
TopPanel and every child beneath it
Instructions
TopPanel-only StyleBoxFlat resources
TopPanel-only portrait AtlasTexture if the new HUD scene now owns it
```

Add one packed-scene resource and one instance:

```text
UI
└── GameUI
    └── ExplorationHud (instance of res://scenes/ui/ExplorationHud.tscn)
```

Keep `UI/UIScreenHost` unchanged and keep `GameUI` as the `HudRoot` configured by `Game._EnterTree()`.

- [ ] **Step 4: Replace old HUD fields and paths in `Game.cs`**

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

Bind it in `_Ready()` before `PlayerStatsChanged` subscription:

```csharp
_explorationHud = GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
```

Delete all fallback lookups for old `TopPanel` paths. This prototype layout has no compatibility requirement.

- [ ] **Step 5: Collapse `UpdatePlayerUI()` into one domain-to-presentation adapter**

Replace old label/bar/build-stat mutation with:

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

Keep all current `UpdatePlayerUI()` call sites so battle, load, NPC, floor-change, and other existing update triggers retain their behavior.

- [ ] **Step 6: Run focused HUD/Game tests and build**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameTest"
dotnet build Sirius.sln --no-restore
```

Expected: PASS and 0 build errors.

- [ ] **Step 7: Prove the prototype paths are gone and commit**

Run:

```bash
rg -n 'Player HUD|Lock position|PlayerAttackHUD|PlayerDefenseHUD|PlayerSpeedHUD|UI/GameUI/TopPanel|UI/GameUI/Instructions' \
  scenes/game/Game.tscn scripts/game/Game.cs
```

Expected: no matches.

Commit:

```bash
git add scenes/game/Game.tscn scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "feat(ui): replace exploration debug HUD"
```

---

### Task 3: Migrate contextual prompt, floor title, and host suppression to the HUD

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Consumes: existing `Game.UpdateInteractionPrompt()` target resolver, `IsGameplayInputSuppressed()`, `UIScreenHostOptions.GameplayInputBlockChanged`, `FloorManager.FloorLoaded`, and the HUD prompt/title/hint API.
- Produces: binding-aware `Open`/`Use`/`Solve` prompt presentation, host/domain suppression and valid-target restoration, temporary floor title, and one session-scoped movement hint.

- [ ] **Step 1: Rewrite the existing treasure prompt test against the new component**

In `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt`, replace the runtime-created plain Label lookup with:

```csharp
var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
```

After arranging the same adjacent unopened treasure:

```csharp
AssertThat(promptPlate.Visible).IsTrue();
AssertThat(prompt.Prompt).IsEqual("Open");
AssertThat(prompt.IconId).IsEqual(UiIconId.Reward);
AssertThat(prompt.Actions.Length).IsEqual(1);
AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));
```

Preserve every existing reward-once assertion. During treasure opening, assert `promptPlate.Visible` becomes false; after the treasure is opened, assert it stays false.

Add two focused cases using the existing runtime spawn setup patterns:

- adjacent unsolved `PuzzleSwitchSpawn` → `Use`, `UiIconId.Puzzle`;
- adjacent unsolved `PuzzleRiddleSpawn` → `Solve`, `UiIconId.Puzzle`.

Do not add prompt mappings for target types that the current gameplay path does not expose.

- [ ] **Step 2: Add failing host-block and valid-target restoration tests**

In `GameplayPauseHostTest`, prove the host cannot leave stale passive prompt presentation behind:

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

Extend the existing real treasure fixture in `GameTest` to prove restoration from actual gameplay state:

1. arrange the adjacent unopened treasure and verify `Open` is visible;
2. invoke the real Pause path;
3. verify the prompt is hidden while Pause owns gameplay blocking;
4. activate Resume;
5. await host restoration; and
6. verify `Open` is visible again because the adjacent target is still valid.

This test must use the real resolver; do not restore the prompt directly through the HUD API.

- [ ] **Step 3: Run the focused Game/host tests and confirm the expected red state**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: FAIL until `Game.UpdateInteractionPrompt()` and the host-block callback target the new HUD.

- [ ] **Step 4: Convert `UpdateInteractionPrompt()` to HUD presentation calls**

Keep existing world lookup logic in `Game` and replace only presentation mutation:

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

    var target = _gridMap.GetPlayerPosition() + _playerController.FacingDirection;

    var box = FindTreasureBoxAt(target);
    if (box != null && !box.IsOpened && !box.IsOpening &&
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

Do not move `FindTreasureBoxAt`, `FindPuzzleSwitchAt`, or `FindPuzzleRiddleAt` into `ExplorationHudController`.

- [ ] **Step 5: Refresh prompt state from the existing host gameplay-block boundary**

Change only the existing callback body in `Game._EnterTree()`:

```csharp
GameplayInputBlockChanged = blocked =>
{
    _presentationGameplayBlocked = blocked;
    if (IsNodeReady())
        UpdateInteractionPrompt();
}
```

Before a committed scene change proceeds, clear the current passive prompt once:

```csharp
private void RequestSceneChange(string path)
{
    if (_sceneChangeCommitted)
        return;

    _sceneChangeCommitted = true;
    _explorationHud?.HideInteractionPrompt();
    _pendingScenePath = path;
    ContinueSceneChangeAfterUiTeardown();
}
```

The existing battle/NPC/world-interaction calls to `UpdateInteractionPrompt()` remain unchanged and now use the same suppression gate.

- [ ] **Step 6: Hook floor-title and session-scoped hint presentation**

At the end of normal gameplay `_Ready()` initialization, after the HUD is bound:

```csharp
_explorationHud.ShowSessionHint("Move with WASD or Arrow Keys");
```

In `OnFloorLoaded(FloorDefinition floorDef, GridMap gridMap)`, after the loaded floor/grid is accepted:

```csharp
_explorationHud?.ShowAreaTitle(floorDef.FloorName);
```

Do not add save data, tutorial flags, or animation state.

- [ ] **Step 7: Prove input-binding refresh and floor-title behavior**

For a scoped `interact` remap test, preserve the original action events by duplication, remap to `Key.E`, show the prompt, and inspect the existing nested component:

```csharp
var originalEvents = InputMap.ActionGetEvents("interact")
    .Select(inputEvent => (InputEvent)inputEvent.Duplicate())
    .ToArray();

try
{
    InputMap.ActionEraseEvents("interact");
    InputMap.ActionAddEvent("interact", new InputEventKey { PhysicalKeycode = Key.E });

    hud.HideInteractionPrompt();
    hud.ShowInteractionPrompt("Open", UiIconId.Reward);
    await AwaitFrames(1);

    var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
    var inputHint = prompt.GetNode<SiriusInputHint>("%InputHint");
    var bindingLabel = inputHint.GetNode<Label>("%BindingLabel");
    AssertThat(bindingLabel.Text).IsEqual("E");
}
finally
{
    InputMap.ActionEraseEvents("interact");
    foreach (var inputEvent in originalEvents)
        InputMap.ActionAddEvent("interact", inputEvent);
}
```

For floor title, instantiate the real Game scene, await initial floor load, and assert `%AreaTitle.Text` equals `FloorManager.CurrentFloorDefinition.FloorName`. Emit `%AreaTitleTimer.Timeout` directly and assert the title hides; do not make the test sleep for two real seconds.

Existing `SiriusInputHintTest` already covers keyboard/mouse/gamepad device observation and fallback behavior, so do not duplicate that component matrix here.

- [ ] **Step 8: Run focused, full, build, and scope verification**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest"

dotnet test Sirius.sln --settings test.runsettings.local
dotnet build Sirius.sln --no-restore
```

Expected: focused tests pass, full suite passes, build has 0 errors.

Run stale-path and scope checks:

```bash
rg -n 'new Label.*InteractionPrompt|EnsureInteractionPromptLabel|_interactionPromptLabel|Player HUD|Lock position|PlayerAttackHUD|PlayerDefenseHUD|PlayerSpeedHUD' \
  scenes scripts tests

git diff --name-only main...HEAD
```

Expected:

- no production runtime-created interaction prompt or old debug HUD binding remains;
- no Theme resource, shared component implementation, hosting infrastructure, domain model/rule, inventory, battle, save/load, or settings implementation file changed;
- runtime/test changes are limited to the HPA-381 file map above.

- [ ] **Step 9: Commit the contextual lifecycle slice**

```bash
git add scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): contextualize exploration HUD feedback"
```

---

## Completion checklist

Before marking HPA-381 complete, verify all of the following together:

- `Game.tscn` has one `ExplorationHud` and no prototype TopPanel/Instructions.
- `Game.cs` contains no direct concrete HUD Label/ProgressBar binding paths.
- HP, MP, EXP, name, and level update through the existing `PlayerStatsChanged` path.
- Gold, ATK, DEF, and SPD are absent from exploration.
- Treasure/puzzle target semantics are unchanged except for themed prompt presentation and binding/device hints.
- Prompt visibility follows existing domain suppression plus `UIScreenHost` gameplay blocking, and a still-valid prompt reappears after Resume.
- Floor title and movement hint are temporary and Timer-owned.
- HUD subtree is passive for mouse and focus.
- Every `SiriusUiMetrics.VerificationViewports` case fits inside the shared safe frame.
- Focused tests, full suite, build, stale-path search, and scope audit pass.
