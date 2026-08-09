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
- Modify `tests/game/GameTest.cs` — production scene cutover, player-state binding, interaction prompt semantics, and floor-title integration.
- Modify `tests/game/GameplayPauseHostTest.cs` — prove a visible interaction prompt is suppressed/restored by the existing host gameplay-block boundary.

---

### Task 1: Build the passive, responsive Exploration HUD component

**Files:**
- Create: `scenes/ui/ExplorationHud.tscn`
- Create: `scripts/ui/ExplorationHudController.cs`
- Create: `tests/ui/ExplorationHudControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusUiMetrics`, `SiriusThemeTypes`, `SiriusStatBar`, `SiriusContextPrompt`, `UiIconId`, the existing hero sprite texture, and the existing callout connector ornament.
- Produces: `ExplorationHudPlayerState`, `ExplorationHudController.ApplyPlayerState`, `ShowInteractionPrompt`, `HideInteractionPrompt`, `ShowAreaTitle`, and `ShowSessionHint`.

- [ ] **Step 1: Write the failing scene-authorship test**

Create `tests/ui/ExplorationHudControllerTest.cs` with a pre-`_Ready()` contract so runtime C# cannot silently rebuild the HUD:

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
        "%HintPlate",
        "%HintLabel",
        "%AreaTitleTimer",
        "%HintTimer"
    };

    [TestCase]
    public void SceneOwnsTheCompleteHudBeforeReady()
    {
        var packed = GD.Load<PackedScene>("res://scenes/ui/ExplorationHud.tscn");
        AssertThat(packed).IsNotNull();

        var hud = packed!.Instantiate<ExplorationHudController>();
        try
        {
            foreach (var path in RequiredNodes)
                AssertThat(hud.GetNodeOrNull(path)).IsNotNull();

            AssertThat(hud.FindChild("Player HUD", recursive: true, owned: false)).IsNull();
            AssertThat(hud.FindChild("LockDrag", recursive: true, owned: false)).IsNull();
            AssertThat(hud.FindChild("Instructions", recursive: true, owned: false)).IsNull();
            AssertThat(hud.FindChild("PlayerAttackHUD", recursive: true, owned: false)).IsNull();
            AssertThat(hud.FindChild("PlayerDefenseHUD", recursive: true, owned: false)).IsNull();
            AssertThat(hud.FindChild("PlayerSpeedHUD", recursive: true, owned: false)).IsNull();
            AssertThat(hud.FindChild("PlayerGold", recursive: true, owned: false)).IsNull();
        }
        finally
        {
            hud.Free();
        }
    }
}
```

- [ ] **Step 2: Run the new test and verify it fails because the scene does not exist**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest"
```

Expected: FAIL loading `res://scenes/ui/ExplorationHud.tscn`.

- [ ] **Step 3: Author `ExplorationHud.tscn` from existing components**

Create this hierarchy and mark the named nodes above as unique in owner:

```text
ExplorationHud (Control, full rect)
├── SafeFrame (Control)
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
│   ├── AreaTitle (Label, SiriusTitle, hidden)
│   ├── PromptPlate (PanelContainer, SiriusHudPlate, hidden)
│   │   ├── ContextPrompt (SiriusContextPrompt)
│   │   └── PromptConnector (TextureRect)
│   └── HintPlate (PanelContainer, SiriusHudPlate, hidden)
│       └── HintLabel (Label, SiriusMetadata)
├── AreaTitleTimer (Timer, one_shot=true, wait_time=2.0)
└── HintTimer (Timer, one_shot=true, wait_time=4.0)
```

Use the current player atlas texture already referenced by `Game.tscn` for `Portrait`. Use the existing `UiOrnamentId.CalloutConnector` asset path or direct texture resource for `PromptConnector`; if the texture cannot be loaded, hide only the connector and keep the prompt readable.

Set the scene root and authored passive surfaces to full/anchored layout; do not hard-code seven viewport-specific rectangles.

- [ ] **Step 4: Implement the feature-local controller contract**

Create `scripts/ui/ExplorationHudController.cs` with the display state and the only public operations this ticket needs:

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
    private PanelContainer _heroPlate = null!;
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

        _healthBar.Current = state.CurrentHealth;
        _healthBar.Maximum = state.MaxHealth;
        _healthBar.Label = "HP";

        _manaBar.Visible = state.MaxMana > 0;
        if (_manaBar.Visible)
        {
            _manaBar.Current = state.CurrentMana;
            _manaBar.Maximum = state.MaxMana;
            _manaBar.Label = "MP";
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

Implement `BindNodes()` using the unique names from Step 1.

Implement `MakePassive(Node node)` recursively and keep it feature-local:

```csharp
private static void MakePassive(Node node)
{
    if (node is Control control)
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        control.FocusMode = FocusModeEnum.None;
    }

    foreach (var child in node.GetChildren())
        MakePassive(child);
}
```

Implement `RefreshLayout()` using only shared metrics and one max-width rule:

```csharp
private void RefreshLayout()
{
    var viewportSize = GetViewportRect().Size;
    var compact = SiriusUiMetrics.IsCompact(viewportSize);
    var margin = SiriusUiMetrics.SafeMargin(compact);
    var contentWidth = MathF.Min(
        MathF.Max(0, viewportSize.X - 2 * margin),
        MaximumContentWidth);
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

Keep positional anchoring for HeroPlate/AreaTitle/PromptPlate/HintPlate in the scene, relative to `SafeFrame`; do not calculate separate coordinates in code for each supported aspect ratio.

- [ ] **Step 5: Add focused controller behavior and passive-input tests**

Extend `ExplorationHudControllerTest` with a SubViewport fixture and tests equivalent to:

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

[TestCase]
public async Task PromptUsesInteractBindingAndTemporarySurfacesHideOnTheirTimers()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));

    hud.ShowInteractionPrompt("Open", UiIconId.Reward);
    var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsTrue();
    AssertThat(prompt.Prompt).IsEqual("Open");
    AssertThat(prompt.Actions.Length).IsEqual(1);
    AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));

    hud.ShowAreaTitle("Ground Floor");
    hud.GetNode<Timer>("%AreaTitleTimer").EmitSignal(Timer.SignalName.Timeout);
    AssertThat(hud.GetNode<Label>("%AreaTitle").Visible).IsFalse();

    hud.ShowSessionHint("Move with WASD or Arrow Keys");
    hud.GetNode<Timer>("%HintTimer").EmitSignal(Timer.SignalName.Timeout);
    AssertThat(hud.GetNode<PanelContainer>("%HintPlate").Visible).IsFalse();
}

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

Add a viewport loop over `SiriusUiMetrics.VerificationViewports`. For each size, resize the SubViewport, await a frame, and assert every currently visible plate has a non-zero rect fully contained by `%SafeFrame`. At 640×360 also assert portrait minimum size is 40×40; at 1280×720 assert 56×56.

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

- [ ] **Step 1: Add failing production cutover and player-binding tests**

Add a `GameTest` that instantiates the real `Game.tscn` and checks the new boundary rather than old label paths:

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

Add a state-update test that mutates the existing player and emits through the current production signal seam:

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

If `GameTest` does not already expose `InstantiateRealGameScene()`, add one local test helper that loads `res://scenes/game/Game.tscn`, adds it to the test root, and awaits the same initialization frame count used by the existing real-scene tests. Do not create production-only seams for test setup.

- [ ] **Step 2: Run the focused Game tests and confirm they fail on the current debug HUD**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest"
```

Expected: the new ExplorationHud path is absent and the old TopPanel still exists.

- [ ] **Step 3: Replace the prototype hierarchy in `Game.tscn`**

Remove from `Game.tscn`:

```text
DraggablePanel ext_resource used by TopPanel
TopPanel and every child beneath it
Instructions
TopPanel-only StyleBoxFlat resources
TopPanel-only portrait AtlasTexture if the new HUD scene owns that atlas resource
```

Add one packed-scene resource and instance:

```text
UI
└── GameUI
    └── ExplorationHud (instance of res://scenes/ui/ExplorationHud.tscn)
```

Keep `UI/UIScreenHost` unchanged and keep `GameUI` as the existing `HudRoot` configured in `Game._EnterTree()`.

- [ ] **Step 4: Replace old label fields with the HUD controller**

In `Game.cs`, remove:

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

Bind it in `_Ready()` before subscribing to `PlayerStatsChanged`:

```csharp
_explorationHud = GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
```

Do not preserve fallback lookups to the old `TopPanel` paths; there are no production users that require backward compatibility for the removed prototype layout.

- [ ] **Step 5: Collapse `UpdatePlayerUI()` into the domain-to-presentation adapter**

Replace all old label/bar/build-stat updates with:

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

Keep every existing `UpdatePlayerUI()` call site. This minimizes lifecycle churn while removing presentation coupling.

- [ ] **Step 6: Run focused HUD/Game tests and the build**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameTest"
dotnet build Sirius.sln --no-restore
```

Expected: PASS and 0 build errors.

- [ ] **Step 7: Run stale-prototype searches and commit**

Run:

```bash
rg -n 'Player HUD|Lock position|PlayerAttackHUD|PlayerDefenseHUD|PlayerSpeedHUD|UI/GameUI/TopPanel|UI/GameUI/Instructions' \
  scenes/game/Game.tscn scripts/game/Game.cs tests/game/GameTest.cs
```

Expected: no production matches for the removed prototype paths/copy. Test names/comments may be rewritten to describe the new contract instead of preserving stale strings.

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
- Consumes: existing `Game.UpdateInteractionPrompt()` world-target resolver, `IsGameplayInputSuppressed()`, `UIScreenHostOptions.GameplayInputBlockChanged`, `FloorManager.FloorLoaded`, and `ExplorationHudController` prompt/title/hint methods.
- Produces: binding-aware `Open`/`Use`/`Solve` prompt presentation, host/domain suppression and restoration, temporary floor title, and one session-scoped movement hint.

- [ ] **Step 1: Rewrite the existing treasure prompt test against `SiriusContextPrompt` and add suppression coverage**

Update the current `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt` test so it locates:

```csharp
var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
```

Replace plain-label assertions with:

```csharp
AssertThat(promptPlate.Visible).IsTrue();
AssertThat(prompt.Prompt).IsEqual("Open");
AssertThat(prompt.IconId).IsEqual(UiIconId.Reward);
AssertThat(prompt.Actions.Length).IsEqual(1);
AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));
```

Preserve the existing reward-once assertions and assert `promptPlate.Visible` becomes false while treasure opening owns world interaction and remains false after the box is opened.

Add focused puzzle assertions using the existing test floor/entity helpers: an unsolved `PuzzleSwitchSpawn` resolves `Use`/`UiIconId.Puzzle`, and an unsolved `PuzzleRiddleSpawn` resolves `Solve`/`UiIconId.Puzzle`. Do not add new target types.

- [ ] **Step 2: Add a failing host-block visibility test**

In `GameplayPauseHostTest`, create a prompt directly through the production HUD, then open/close the existing hosted Pause and force the production resolver refresh:

```csharp
[TestCase]
public async Task HostGameplayBlockSuppressesAndRestoresExplorationPrompt()
{
    var hud = _game!.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
    hud.ShowInteractionPrompt("Open", UiIconId.Reward);
    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsTrue();

    AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
    await AwaitFrames(2);
    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsFalse();

    var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
    pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(2);

    // With no valid adjacent target in this fixture, the resolver keeps it hidden.
    // The important contract is that host unblock re-runs the resolver rather than
    // resurrecting stale presentation state.
    AssertThat(hud.GetNode<PanelContainer>("%PromptPlate").Visible).IsFalse();
}
```

Also add a `GameTest` real-target variant if needed to prove a valid adjacent target reappears after Resume. Prefer extending the existing treasure fixture rather than adding a new production seam.

- [ ] **Step 3: Run the focused interaction/host tests and confirm failure on the runtime-created label path**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: new prompt-component and host-suppression expectations fail before the implementation below.

- [ ] **Step 4: Convert `UpdateInteractionPrompt()` to HUD presentation calls**

Keep all existing treasure/puzzle lookup logic in `Game`. Replace label mutation with this shape:

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

Do not move `FindTreasureBoxAt`, `FindPuzzleSwitchAt`, or `FindPuzzleRiddleAt` into the HUD controller.

- [ ] **Step 5: Refresh prompt state when `UIScreenHost` gameplay blocking changes**

Change only the callback body in `_EnterTree()`:

```csharp
GameplayInputBlockChanged = blocked =>
{
    _presentationGameplayBlocked = blocked;
    if (IsNodeReady())
        UpdateInteractionPrompt();
}
```

This reuses the existing presentation-block signal rather than adding a second modal-state observer.

Before committing a scene change, explicitly clear the passive prompt once:

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

- [ ] **Step 6: Hook floor-title and session-scoped hint presentation**

At the end of successful gameplay initialization in `_Ready()`, after `_explorationHud` exists:

```csharp
_explorationHud.ShowSessionHint("Move with WASD or Arrow Keys");
```

In `OnFloorLoaded(FloorDefinition floorDef, GridMap gridMap)`, after the floor/grid references are valid:

```csharp
_explorationHud?.ShowAreaTitle(floorDef.FloorName);
```

Do not add save data, tutorial flags, or animation state.

- [ ] **Step 7: Add floor-title and binding-refresh assertions**

In `GameTest`, assert the real initial floor load leaves `%AreaTitle` populated with `FloorManager.CurrentFloorDefinition.FloorName` while visible before its timer expires. Trigger the timer signal directly rather than waiting real seconds.

For input binding, use a scoped InputMap mutation around the existing `interact` action:

```csharp
var originalEvents = InputMap.ActionGetEvents("interact");
try
{
    InputMap.ActionEraseEvents("interact");
    InputMap.ActionAddEvent("interact", new InputEventKey { PhysicalKeycode = Key.E });

    hud.HideInteractionPrompt();
    hud.ShowInteractionPrompt("Open", UiIconId.Reward);
    await AwaitFrames(1);

    var bindingLabel = hud.GetNode<Label>("%ContextPrompt/%InputHint/%BindingLabel");
    AssertThat(bindingLabel.Text).IsEqual("E");
}
finally
{
    InputMap.ActionEraseEvents("interact");
    foreach (var inputEvent in originalEvents)
        InputMap.ActionAddEvent("interact", inputEvent);
}
```

If the nested unique-name lookup differs in the instantiated component, resolve `%BindingLabel` from the `SiriusInputHint` child; do not expose a production property solely for the test.

- [ ] **Step 8: Run focused and full validation**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest"

dotnet test Sirius.sln --settings test.runsettings.local
dotnet build Sirius.sln --no-restore
```

Expected: all focused tests pass, full suite passes, build has 0 errors.

Run scope/staleness checks:

```bash
rg -n 'new Label.*InteractionPrompt|EnsureInteractionPromptLabel|_interactionPromptLabel|Player HUD|Lock position|PlayerAttackHUD|PlayerDefenseHUD|PlayerSpeedHUD' \
  scenes scripts tests

git diff --name-only main...HEAD
```

Expected:

- no production runtime-created interaction prompt or old debug HUD bindings remain;
- no HPA-381 change touches Theme resources, shared UI component implementations, hosting infrastructure, domain model/rules, inventory, battle, save/load, or settings implementation;
- changed runtime/test files are limited to the HPA-381 file map above.

- [ ] **Step 9: Commit the prompt/lifecycle slice**

```bash
git add scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): contextualize exploration HUD feedback"
```

---

## Implementation completion checklist

Before marking HPA-381 complete, verify all of the following together:

- `Game.tscn` has one `ExplorationHud` and no prototype TopPanel/Instructions.
- `Game.cs` contains no direct concrete HUD Label/ProgressBar binding paths.
- HP, MP, EXP, name, and level update through the existing `PlayerStatsChanged` path.
- Gold, ATK, DEF, and SPD are absent from exploration.
- Treasure/puzzle prompt behavior is unchanged except for themed presentation and binding/device hints.
- Prompt visibility follows existing domain suppression plus `UIScreenHost` gameplay blocking.
- Floor title and movement hint are temporary and timer-owned.
- HUD subtree is passive for mouse and focus.
- Every `SiriusUiMetrics.VerificationViewports` case fits inside the shared safe frame.
- Focused tests, full suite, build, stale-path search, and scope audit pass.
