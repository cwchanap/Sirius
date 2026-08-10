# HPA-381 Compact Exploration HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's draggable debug exploration overlay with a scene-authored compact HUD while preserving player access to Gold, existing interaction semantics, lifecycle behavior, and the approved responsive visual system.

**Architecture:** Add one feature-local `ExplorationHud.tscn` plus `ExplorationHudController`. `Game` remains the gameplay/world-context owner and adapts `Character`, floor, target, scene-transition, and host-block state into the HUD's narrow API. Reuse the existing Theme, `SiriusStatBar` for HP/MP, `SiriusContextPrompt`, `SiriusInputHint`, stock themed `ProgressBar` for the thin EXP line, and the gameplay `UIScreenHost`. Promote the already-approved 1600px ultrawide limit into `SiriusUiMetrics` because the showcase and production HUD are now two real consumers. The isolated HUD/shared-metric work lands first; the production cutover, Gold-preservation seam, old prompt migration, lifecycle tests, and dead `DraggablePanel` deletion land atomically second.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme/components/art catalogue, and `UIScreenHost`.

## Global Constraints

- Primary target is desktop landscape with mouse, keyboard, and gamepad.
- Minimum supported logical resolution is 640×360.
- Use `SiriusUiMetrics.IsCompact(viewportSize)` and `SiriusUiMetrics.SafeMargin(compact)`; do not invent another breakpoint.
- Add exactly one shared layout policy value: `SiriusUiMetrics.MaximumContentWidth = 1600f`; migrate the existing showcase duplicate to it.
- Validate every `SiriusUiMetrics.VerificationViewports` size, with deeper layout assertions at 640×360 and 1280×720.
- Preserve current `GameManager.PlayerStatsChanged`, floor, battle, NPC, world-interaction, scene-transition, and `UIScreenHost` semantics.
- `Game` keeps world-target validity and suppression decisions; the HUD must not query `GameManager`, `GridMap`, spawn groups, inventory, saves, or host state.
- Show name, level, HP, MP, and a thin EXP `ProgressBar` only.
- Do not show Gold, ATK, DEF, SPD, equipment bonuses, developer headings, drag controls, Lock, permanent instructions, area-colour documentation, or future placeholders in exploration.
- Preserve Gold discoverability by adding one read-only Gold label to the existing Inventory header in the production cutover; do not redesign Inventory.
- Reuse `SiriusStatBar` unchanged for HP/MP. Do not add `ShowHeader`, `ShowState`, `Thin`, or other new stat-bar configuration in this ticket.
- Use the existing `SiriusExpBar` Theme variation on a stock `ProgressBar` for EXP; do not add a parallel EXP formatter/value-label component.
- Reuse `SiriusContextPrompt`/`SiriusInputHint` for interaction prompts and the existing `interact` InputMap action.
- Keep movement hinting session-scoped and fixed to current WASD/arrow behavior; do not add movement InputMap actions or tutorial persistence.
- Use one transient message plate and one one-shot `ProcessMode.Always` timer; area title wins and the movement hint queues behind it.
- The HUD is passive: every `Control` in its subtree uses `MouseFilter.Ignore` and `FocusMode.None`.
- Assign `res://resources/ui/theme/SiriusTheme.tres` to the HUD root.
- Preserve the current hero portrait crop as an `AtlasTexture` region `Rect2(0, 0, 96, 96)` over the 384×96 four-frame hero sheet; never bind the raw strip directly.
- Do not modify `GameManager`, `Character`, `PlayerController`, `GridMap`, `SiriusTheme.tres`, `SiriusStatBar`, `SiriusContextPrompt`, `SiriusInputHint`, or `scripts/ui/hosting/*`.
- Delete `scripts/ui/DraggablePanel.cs` in the atomic cutover; repository search confirms `Game.tscn` is its only consumer. There is no tracked `DraggablePanel.cs.uid` file.
- Do not commit a state where old HUD/prompt nodes are deleted while code or lifecycle tests still reference them.

---

## File map

### Task 1 — shared metric + isolated HUD

- Modify `scripts/ui/theme/SiriusUiMetrics.cs` — canonical 1600px content maximum.
- Modify `scripts/ui/showcase/SiriusUiShowcase.cs` — consume shared maximum instead of private duplicate.
- Modify `tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs` — consume shared maximum.
- Create `scenes/ui/ExplorationHud.tscn` — themed passive HUD, 96×96 portrait crop, one transient region/timer.
- Create `scripts/ui/ExplorationHudController.cs` — state binding, layout, prompt, transient arbitration.
- Create `tests/ui/ExplorationHudControllerTest.cs` — authored structure, portrait, binding, passive policy, transient precedence, viewport fit.

### Task 2 — atomic production cutover

- Modify `scenes/game/Game.tscn` — replace prototype HUD with `ExplorationHud`.
- Modify `scripts/game/Game.cs` — HUD adapter, prompt, host block, floor/hint, scene-transition behavior.
- Modify `scenes/ui/InventoryMenu.tscn` — add minimal `%GoldLabel`.
- Modify `scripts/ui/InventoryMenuController.cs` — refresh current Gold on Inventory open/refresh.
- Modify `tests/ui/InventoryMenuControllerTest.cs` — Gold preservation regression.
- Modify `tests/game/GameTest.cs` — scene cutover, stat binding, prompt semantics/restoration, floor transient.
- Modify `tests/game/GameInputLifecycleTest.cs` — migrate all old prompt-path lifecycle contracts.
- Modify `tests/game/GameplayPauseHostTest.cs` — host-block prompt suppression.
- Delete `scripts/ui/DraggablePanel.cs` — dead after its only scene consumer is removed.

---

## Task 1: Add the shared layout metric and isolated Exploration HUD

**Files:**
- Modify: `scripts/ui/theme/SiriusUiMetrics.cs`
- Modify: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Modify: `tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs`
- Create: `scenes/ui/ExplorationHud.tscn`
- Create: `scripts/ui/ExplorationHudController.cs`
- Create: `tests/ui/ExplorationHudControllerTest.cs`

**Interfaces:**
- Consumes: existing Sirius Theme, `SiriusThemeTypes`, `SiriusStatBar`, `SiriusContextPrompt`, `UiIconId`, hero sprite sheet, orbit/callout ornaments.
- Produces: `SiriusUiMetrics.MaximumContentWidth`, `ExplorationHudPlayerState`, `ApplyPlayerState`, `ShowInteractionPrompt`, `HideInteractionPrompt`, `ShowAreaTitle`, `ShowSessionHint`.

### Step 1: Write the red metric + scene-authorship test

- [ ] Extend `tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs` so the expected content width references the shared constant:

```csharp
AssertThat(content.CustomMinimumSize.X)
    .IsEqual(Mathf.Min(
        SiriusUiMetrics.MaximumContentWidth,
        size.X - safeMargin * 2f));
```

This initially fails to compile because the constant does not exist.

- [ ] Create `tests/ui/ExplorationHudControllerTest.cs` with the static scene contract:

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
    private const string HeroSheetPath =
        "res://assets/sprites/characters/player_hero/sprite_sheet.png";

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
        "%ExperienceBar",
        "%PromptPlate",
        "%ContextPrompt",
        "%PromptConnector",
        "%TransientPlate",
        "%TransientLabel",
        "%TransientTimer"
    };

    [TestCase]
    public void SceneOwnsRequiredHudThemeAndSingleFramePortrait()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        AssertThat(packed).IsNotNull();

        var hud = packed!.Instantiate<ExplorationHudController>();
        try
        {
            foreach (var path in RequiredNodes)
                AssertThat(hud.GetNodeOrNull(path)).IsNotNull();

            AssertThat(hud.Theme).IsNotNull();
            AssertThat(hud.Theme.ResourcePath).IsEqual(SiriusThemeTypes.ResourcePath);
            AssertThat(SiriusUiMetrics.MaximumContentWidth).IsEqual(1600f);

            var portrait = hud.GetNode<TextureRect>("%Portrait");
            AssertThat(portrait.Texture is AtlasTexture).IsTrue();
            var atlas = (AtlasTexture)portrait.Texture;
            AssertThat(atlas.Atlas).IsNotNull();
            AssertThat(atlas.Atlas.ResourcePath).IsEqual(HeroSheetPath);
            AssertThat(atlas.Region).IsEqual(new Rect2(0, 0, 96, 96));

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

Do not add vacuous assertions that this brand-new scene lacks legacy `TopPanel` child names. Production cutover tests own that contract.

### Step 2: Run the expected red tests

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~SiriusUiShowcaseResponsiveTest"
```

Expected: compile/test failure because `MaximumContentWidth` and `ExplorationHud.tscn` do not exist.

### Step 3: Promote the already-approved 1600px policy

- [ ] Add to `SiriusUiMetrics`:

```csharp
public const float MaximumContentWidth = 1600f;
```

- [ ] Delete `private const float MaximumContentWidth = 1600f;` from `SiriusUiShowcase` and replace its use with:

```csharp
SiriusUiMetrics.MaximumContentWidth
```

- [ ] Keep the responsive test expression from Step 1. Do not move any other metric in this ticket.

### Step 4: Author `ExplorationHud.tscn`

- [ ] Create these resources:

```text
Script: res://scripts/ui/ExplorationHudController.cs
Theme: res://resources/ui/theme/SiriusTheme.tres
PackedScene: res://scenes/ui/components/SiriusStatBar.tscn
PackedScene: res://scenes/ui/components/SiriusContextPrompt.tscn
Texture2D: res://assets/sprites/characters/player_hero/sprite_sheet.png
Texture2D: res://assets/sprites/ui/ornaments/orbit_arc.png
Texture2D: res://assets/sprites/ui/ornaments/callout_connector.png
```

- [ ] Define the portrait as a scene subresource, preserving the current production crop:

```text
[sub_resource type="AtlasTexture" id="HeroPortrait"]
atlas = ExtResource("<hero_sheet_id>")
region = Rect2(0, 0, 96, 96)
filter_clip = true
```

- [ ] Author this hierarchy:

```text
ExplorationHud (full rect, theme=SiriusTheme)
├── SafeFrame (full rect anchors; controller owns offsets)
│   ├── HeroOrbitArc (TextureRect)
│   ├── HeroPlate (PanelContainer, SiriusHudPlate)
│   │   └── HeroContent (HBoxContainer)
│   │       ├── Portrait (TextureRect, HeroPortrait)
│   │       └── PlayerData (VBoxContainer)
│   │           ├── IdentityRow
│   │           │   ├── PlayerName (SiriusBody)
│   │           │   └── PlayerLevel (SiriusMetadata)
│   │           ├── HealthBar (SiriusStatBar: Kind=Health, Label="HP")
│   │           ├── ManaBar (SiriusStatBar: Kind=Mana, Label="MP")
│   │           └── ExperienceBar (ProgressBar, SiriusExpBar, thin)
│   ├── PromptPlate (PanelContainer, SiriusHudPlate, hidden)
│   │   └── PromptContent (VBoxContainer)
│   │       ├── ContextPrompt (SiriusContextPrompt)
│   │       └── PromptConnector (TextureRect)
│   └── TransientPlate (PanelContainer, SiriusHudPlate, hidden, top-centre)
│       └── TransientLabel (Label)
└── TransientTimer (Timer, one_shot=true, process_mode=Always)
```

Set `%ExperienceBar.show_percentage = false`. No EXP value label is added.

### Step 5: Implement the feature-local controller shell

- [ ] Create `scripts/ui/ExplorationHudController.cs`:

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
    private const double AreaTitleSeconds = 2.0;
    private const double SessionHintSeconds = 4.0;

    private Control _safeFrame = null!;
    private TextureRect _portrait = null!;
    private Label _playerName = null!;
    private Label _playerLevel = null!;
    private SiriusStatBar _healthBar = null!;
    private SiriusStatBar _manaBar = null!;
    private ProgressBar _experienceBar = null!;
    private PanelContainer _promptPlate = null!;
    private SiriusContextPrompt _contextPrompt = null!;
    private PanelContainer _transientPlate = null!;
    private Label _transientLabel = null!;
    private Timer _transientTimer = null!;

    private bool _compact;
    private bool _showingAreaTitle;
    private string? _pendingSessionHint;

    public override void _Ready()
    {
        BindNodes();
        MakePassive(this);
        _contextPrompt.Actions = new[] { InteractAction };
        _transientTimer.Timeout += OnTransientTimeout;
        GetViewport().SizeChanged += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        if (_transientTimer != null)
            _transientTimer.Timeout -= OnTransientTimeout;

        var viewport = GetViewport();
        if (viewport != null)
            viewport.SizeChanged -= RefreshLayout;
    }

    private void BindNodes()
    {
        _safeFrame = GetNode<Control>("%SafeFrame");
        _portrait = GetNode<TextureRect>("%Portrait");
        _playerName = GetNode<Label>("%PlayerName");
        _playerLevel = GetNode<Label>("%PlayerLevel");
        _healthBar = GetNode<SiriusStatBar>("%HealthBar");
        _manaBar = GetNode<SiriusStatBar>("%ManaBar");
        _experienceBar = GetNode<ProgressBar>("%ExperienceBar");
        _promptPlate = GetNode<PanelContainer>("%PromptPlate");
        _contextPrompt = GetNode<SiriusContextPrompt>("%ContextPrompt");
        _transientPlate = GetNode<PanelContainer>("%TransientPlate");
        _transientLabel = GetNode<Label>("%TransientLabel");
        _transientTimer = GetNode<Timer>("%TransientTimer");
    }
}
```

Public methods are used only after `_Ready()`; do not add another pre-ready buffering abstraction.

### Step 6: Implement player and prompt presentation

- [ ] Add:

```csharp
public void ApplyPlayerState(ExplorationHudPlayerState state)
{
    _playerName.Text = string.IsNullOrWhiteSpace(state.Name)
        ? "Adventurer"
        : state.Name;
    _playerLevel.Text = $"Lv {state.Level}";

    _healthBar.Current = state.CurrentHealth;
    _healthBar.Maximum = state.MaxHealth;

    _manaBar.Visible = state.MaxMana > 0;
    if (_manaBar.Visible)
    {
        _manaBar.Current = state.CurrentMana;
        _manaBar.Maximum = state.MaxMana;
    }

    _experienceBar.Visible = state.ExperienceToNext > 0;
    if (_experienceBar.Visible)
    {
        _experienceBar.MaxValue = state.ExperienceToNext;
        _experienceBar.Value = Math.Clamp(
            state.Experience,
            0,
            state.ExperienceToNext);
    }

    _portrait.Visible = _portrait.Texture != null;
}

public void ShowInteractionPrompt(string text, UiIconId icon)
{
    if (_contextPrompt.Prompt != text)
        _contextPrompt.Prompt = text;
    if (!_contextPrompt.ShowIcon)
        _contextPrompt.ShowIcon = true;
    if (_contextPrompt.IconId != icon)
        _contextPrompt.IconId = icon;

    // Required even when text/icon are unchanged: Settings may have remapped
    // the same interact action while this target remained valid.
    _contextPrompt.Refresh();
    _promptPlate.Visible = true;
}

public void HideInteractionPrompt() => _promptPlate.Visible = false;
```

Static HP/MP `Kind` and `Label` values stay scene-authored.

### Step 7: Implement one transient region with precedence

- [ ] Add:

```csharp
public void ShowAreaTitle(string title)
{
    if (string.IsNullOrWhiteSpace(title))
        return;

    if (_transientPlate.Visible && !_showingAreaTitle)
        _pendingSessionHint = _transientLabel.Text;

    _showingAreaTitle = true;
    ShowTransient(
        title,
        _compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title,
        AreaTitleSeconds);
}

public void ShowSessionHint(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return;

    if (_transientPlate.Visible && _showingAreaTitle)
    {
        _pendingSessionHint = text;
        return;
    }

    _showingAreaTitle = false;
    ShowTransient(
        text,
        _compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata,
        SessionHintSeconds);
}

private void ShowTransient(string text, StringName variation, double seconds)
{
    _transientLabel.Text = text;
    _transientLabel.ThemeTypeVariation = variation;
    _transientPlate.Visible = true;
    _transientTimer.WaitTime = seconds;
    _transientTimer.Start();
}

private void OnTransientTimeout()
{
    if (_showingAreaTitle && !string.IsNullOrWhiteSpace(_pendingSessionHint))
    {
        var hint = _pendingSessionHint;
        _pendingSessionHint = null;
        _showingAreaTitle = false;
        ShowTransient(
            hint!,
            _compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata,
            SessionHintSeconds);
        return;
    }

    _showingAreaTitle = false;
    _pendingSessionHint = null;
    _transientPlate.Visible = false;
}
```

The timer is authored `ProcessMode.Always`, so Pause does not freeze transient lifetime.

### Step 8: Implement passive input and responsive layout

- [ ] Add:

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
    _compact = SiriusUiMetrics.IsCompact(viewportSize);
    var margin = SiriusUiMetrics.SafeMargin(_compact);
    var availableWidth = MathF.Max(0, viewportSize.X - margin * 2f);
    var contentWidth = MathF.Min(
        availableWidth,
        SiriusUiMetrics.MaximumContentWidth);
    var sideInset = MathF.Max(
        margin,
        (viewportSize.X - contentWidth) / 2f);

    _safeFrame.OffsetLeft = sideInset;
    _safeFrame.OffsetRight = -sideInset;
    _safeFrame.OffsetTop = margin;
    _safeFrame.OffsetBottom = -margin;

    _portrait.CustomMinimumSize = _compact
        ? new Vector2(40, 40)
        : new Vector2(56, 56);
    _healthBar.Compact = _compact;
    _manaBar.Compact = _compact;
    _contextPrompt.Compact = _compact;
    _playerName.ThemeTypeVariation = _compact
        ? SiriusThemeTypes.BodyCompact
        : SiriusThemeTypes.Body;
    _playerLevel.ThemeTypeVariation = _compact
        ? SiriusThemeTypes.MetadataCompact
        : SiriusThemeTypes.Metadata;

    if (_transientPlate.Visible)
    {
        _transientLabel.ThemeTypeVariation = _showingAreaTitle
            ? (_compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title)
            : (_compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata);
    }
}
```

### Step 9: Add behavior/layout tests

- [ ] Extend `ExplorationHudControllerTest` with a SubViewport fixture and cover:

```csharp
[TestCase]
public async Task ApplyPlayerStateBindsStatsAndUsesThinExpBar()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));

    hud.ApplyPlayerState(new ExplorationHudPlayerState(
        "Aster", 7, 73, 120, 21, 50, 340, 500));

    AssertThat(hud.GetNode<Label>("%PlayerName").Text).IsEqual("Aster");
    AssertThat(hud.GetNode<Label>("%PlayerLevel").Text).IsEqual("Lv 7");
    AssertThat(hud.GetNode<SiriusStatBar>("%HealthBar").Current).IsEqual(73);
    AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Current).IsEqual(21);
    AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").Value).IsEqual(340);
    AssertThat(hud.GetNodeOrNull("%ExperienceLabel")).IsNull();
}
```

- [ ] Preserve optional portrait behavior:

```csharp
[TestCase]
public async Task MissingPortraitCollapsesButIdentityRemainsReadable()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));
    var portrait = hud.GetNode<TextureRect>("%Portrait");
    portrait.Texture = null;

    hud.ApplyPlayerState(new ExplorationHudPlayerState(
        "Aster", 7, 73, 120, 0, 0, 10, 100));

    AssertThat(portrait.Visible).IsFalse();
    AssertThat(hud.GetNode<Label>("%PlayerName").Visible).IsTrue();
    AssertThat(hud.GetNode<Label>("%PlayerLevel").Visible).IsTrue();
}
```

This is intentionally retained because HPA-381 explicitly requires graceful missing-portrait behavior.

- [ ] Test transient arbitration:

```csharp
[TestCase]
public async Task AreaTitlePrecedesQueuedSessionHintInOneRegion()
{
    var hud = await InstantiateHud(new Vector2I(640, 360));

    hud.ShowSessionHint("Move with WASD or Arrow Keys");
    hud.ShowAreaTitle("Ground Floor");

    var plate = hud.GetNode<PanelContainer>("%TransientPlate");
    var label = hud.GetNode<Label>("%TransientLabel");
    var timer = hud.GetNode<Timer>("%TransientTimer");

    AssertThat(plate.Visible).IsTrue();
    AssertThat(label.Text).IsEqual("Ground Floor");
    AssertThat(timer.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(plate.Visible).IsTrue();
    AssertThat(label.Text).IsEqual("Move with WASD or Arrow Keys");

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(plate.Visible).IsFalse();
}
```

- [ ] Test prompt action and recursive passive policy.
- [ ] Loop over `SiriusUiMetrics.VerificationViewports`; show hero/prompt/transient and assert each visible rectangle is non-zero and contained inside `%SafeFrame`.
- [ ] At 640×360 assert the portrait minimum is 40×40 and hero/prompt/transient do not overlap; at 1280×720 assert portrait minimum is 56×56.

### Step 10: Run Task 1 verification and commit

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~SiriusUiShowcaseResponsiveTest"
dotnet build Sirius.sln --no-restore
```

Expected: PASS; build 0 errors.

- [ ] Commit:

```bash
git add scripts/ui/theme/SiriusUiMetrics.cs \
  scripts/ui/showcase/SiriusUiShowcase.cs \
  tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs \
  scenes/ui/ExplorationHud.tscn \
  scripts/ui/ExplorationHudController.cs \
  tests/ui/ExplorationHudControllerTest.cs
git commit -m "feat(ui): add compact exploration HUD component"
```

---

## Task 2: Atomically cut production over and preserve Gold discoverability

**Files:**
- Modify: `scenes/game/Game.tscn`
- Modify: `scripts/game/Game.cs`
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Delete: `scripts/ui/DraggablePanel.cs`

**Interfaces:**
- Consumes: `ExplorationHudController`, `ExplorationHudPlayerState`, existing `Game.UpdateInteractionPrompt()` finders, `IsGameplayInputSuppressed()`, `GameplayInputBlockChanged`, `FloorLoaded`, `RequestSceneChange()`, and `InventoryMenuController.RefreshUI()`.
- Produces: production compact HUD, deterministic prompt suppression/restoration, floor-title→session-hint sequencing, current Gold in Inventory, and no prototype prompt/debug utility path.

### Step 1: Write all red cutover regressions before deleting the old nodes

- [ ] In `GameTest`, add a full production scene contract:

```csharp
[TestCase]
public async Task GameSceneUsesExplorationHudWithoutPrototypeHud()
{
    var game = await InstantiateRealGameScene();
    try
    {
        AssertThat(game.GetNodeOrNull<ExplorationHudController>(
            "UI/GameUI/ExplorationHud")).IsNotNull();

        string[] removedPaths =
        {
            "UI/GameUI/TopPanel",
            "UI/GameUI/Instructions",
            "UI/GameUI/InteractionPrompt"
        };

        foreach (var path in removedPaths)
            AssertThat(game.GetNodeOrNull(path)).IsNull();
    }
    finally
    {
        game.Free();
        await AwaitFrames(1);
    }
}
```

The final `rg` audit below covers nested legacy names and the deleted script.

- [ ] Add player binding:

```csharp
[TestCase]
public async Task PlayerStatsChangedRefreshesExplorationHud()
{
    var game = await InstantiateRealGameScene();
    try
    {
        var manager = game.GetNode<GameManager>("GameManager");
        var hud = game.GetNode<ExplorationHudController>(
            "UI/GameUI/ExplorationHud");

        manager.Player.CurrentHealth = 61;
        manager.Player.CurrentMana = 17;
        manager.Player.Experience = 42;
        manager.NotifyPlayerStatsChanged();
        await AwaitFrames(1);

        AssertThat(hud.GetNode<SiriusStatBar>("%HealthBar").Current).IsEqual(61);
        AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Current).IsEqual(17);
        AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").Value).IsEqual(42);
    }
    finally
    {
        game.Free();
        await AwaitFrames(1);
    }
}
```

- [ ] Rewrite `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt` against:

```csharp
var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
```

Preserve reward-once assertions and assert `Open` / `UiIconId.Reward` / `interact`.

- [ ] Add/retain `Use` for unsolved switch and `Solve` for unsolved riddle using existing runtime spawn fixtures.

- [ ] Add real target Pause→Resume restoration: arrange unopened adjacent treasure, resolve `Open`, open real Pause, assert hidden, press Resume, await restoration, assert `Open` returns from resolver state without calling the HUD directly.

### Step 2: Migrate the three `GameInputLifecycleTest` prompt contracts

- [ ] `ConfiguredKeyboardCancel_DeclinesToTopmostRiddleWithoutOpeningHostedPause`:
  - replace old Label lookup with `%PromptPlate` + `%ContextPrompt`;
  - assert `Solve` before riddle;
  - assert plate hidden during world interaction;
  - after Cancel closes riddle, assert `Solve` is re-resolved.

- [ ] `FloorReplacement_RebindsGridAndRefreshesPrompt`:
  - remove `prompt.Visible = true` artificial setup;
  - arrange a real valid target on the original floor or invoke the existing resolver after fixture setup;
  - load the replacement floor;
  - assert `_gridMap` rebinds and `%PromptPlate` reflects the replacement floor's actual target state (hidden when no valid adjacent target).

- [ ] `InteractionPrompt_HidesDuringBattleAndRestoresAfterEscape`:
  - use `%PromptPlate`/`%ContextPrompt`;
  - preserve real adjacent treasure;
  - assert hide during battle and `Open` restoration after escape.

### Step 3: Add Gold preservation regression

- [ ] In `InventoryMenuControllerTest`:

```csharp
[TestCase]
public void OpenMenuShowsCurrentPlayerGold()
{
    _gameManager.Player.Gold = 321;

    _inventoryMenu.OpenMenu();

    AssertThat(_inventoryMenu.GetNode<Label>("%GoldLabel").Text)
        .IsEqual("Gold: 321");
}
```

- [ ] Run the cutover filters now. Expected: red because the HUD scene is not yet in `Game.tscn`, old prompt paths remain, and `%GoldLabel` does not exist:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~InventoryMenuControllerTest"
```

### Step 4: Replace the production `Game.tscn` prototype HUD

- [ ] Remove:

```text
DraggablePanel ext_resource
TopPanel subtree
Instructions
TopPanel-only StyleBoxFlat resources
TopPanel-only hero AtlasTexture resource
```

- [ ] Add one `ExplorationHud.tscn` packed-scene resource and instance:

```text
UI
└── GameUI
    └── ExplorationHud
```

Keep `UI/UIScreenHost` unchanged and keep `GameUI` as host `HudRoot`.

- [ ] Delete `scripts/ui/DraggablePanel.cs` in this same atomic cutover. Do not create a replacement drag utility.

### Step 5: Replace old HUD bindings in `Game.cs`

- [ ] Remove:

```text
_playerNameLabel
_playerLevelLabel
_playerHealthLabel
_playerExperienceLabel
_playerGoldLabel
_interactionPromptLabel
EnsureInteractionPromptLabel()
all UI/GameUI/TopPanel fallback paths
```

- [ ] Add:

```csharp
private ExplorationHudController _explorationHud = null!;
```

- [ ] Bind before player-stat signal use:

```csharp
_explorationHud = GetNode<ExplorationHudController>(
    "UI/GameUI/ExplorationHud");
```

### Step 6: Collapse `UpdatePlayerUI()` into the adapter

- [ ] Replace prototype label/bar/build-stat mutation with:

```csharp
private void UpdatePlayerUI()
{
    if (_isAbortInitialization ||
        _gameManager?.Player == null ||
        _explorationHud == null)
    {
        return;
    }

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

Keep existing `UpdatePlayerUI()` call sites.

### Step 7: Convert `UpdateInteractionPrompt()` without moving target logic

- [ ] Start with:

```csharp
private void UpdateInteractionPrompt()
{
    if (_explorationHud == null)
        return;

    if (_gridMap == null ||
        _playerController == null ||
        _gameManager == null ||
        _sceneChangeCommitted ||
        IsGameplayInputSuppressed())
    {
        _explorationHud.HideInteractionPrompt();
        return;
    }

    var target = _gridMap.GetPlayerPosition()
        + _playerController.FacingDirection;
```

- [ ] Preserve existing finders and map presentation only:

```csharp
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

Do not add NPC prompt behavior in this ticket.

### Step 8: Wire host-block refresh and scene-transition hiding

- [ ] Expand the existing host callback only:

```csharp
GameplayInputBlockChanged = blocked =>
{
    _presentationGameplayBlocked = blocked;
    if (IsNodeReady())
        UpdateInteractionPrompt();
};
```

- [ ] In `RequestSceneChange()` after `_sceneChangeCommitted = true`:

```csharp
UpdateInteractionPrompt();
```

This hides the prompt immediately without another observer.

### Step 9: Wire floor title and startup hint through one transient surface

- [ ] After the HUD is bound and initial state is available in `Game._Ready()`:

```csharp
if (_floorManager.CurrentFloorDefinition != null)
{
    _explorationHud.ShowAreaTitle(
        _floorManager.CurrentFloorDefinition.FloorName);
}

_explorationHud.ShowSessionHint("Move with WASD or Arrow Keys");
```

Because area title has precedence, the hint queues automatically.

- [ ] In `OnFloorLoaded`:

```csharp
_explorationHud?.ShowAreaTitle(floorDef.FloorName);
```

Keep existing floor/grid setup unchanged.

### Step 10: Add the minimal Inventory Gold readout

- [ ] In `InventoryMenu.tscn`, add to the existing Header, after the expanding Title and before `ActiveSkillToolbar`:

```text
GoldLabel (Label)
unique_name_in_owner = true
text = "Gold: 0"
font size = existing 16px header metadata scale
```

Do not add a new panel, tab, or inventory layout behavior.

- [ ] In `InventoryMenuController` add:

```csharp
private Label _goldLabel = null!;
```

Bind in `_Ready()`:

```csharp
_goldLabel = GetNode<Label>("%GoldLabel");
```

At the beginning of valid `RefreshUI()`:

```csharp
var player = _gameManager.Player;
_goldLabel.Text = $"Gold: {player.Gold}";
```

Keep the existing slot/skill refresh calls. No new signal is needed because `OpenMenu()` already calls `RefreshUI()`.

### Step 11: Run focused regression + build before commit

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~SiriusUiShowcaseResponsiveTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~InventoryMenuControllerTest"
dotnet build Sirius.sln --no-restore
```

Expected: all focused tests pass; build exits 0.

### Step 12: Run the full suite

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: 0 failed tests.

### Step 13: Run stale-path/dead-code audit

- [ ] Run:

```bash
rg -n 'Player HUD|Lock position|PlayerAttackHUD|PlayerDefenseHUD|PlayerSpeedHUD|UI/GameUI/TopPanel|UI/GameUI/Instructions|UI/GameUI/InteractionPrompt|_interactionPromptLabel|EnsureInteractionPromptLabel|DraggablePanel' \
  scenes scripts tests
```

Expected: no matches.

### Step 14: Run scope audit

- [ ] Run:

```bash
git diff --name-only main...HEAD
```

Allowed runtime/test paths for HPA-381:

```text
scripts/ui/theme/SiriusUiMetrics.cs
scripts/ui/showcase/SiriusUiShowcase.cs
tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs
scenes/ui/ExplorationHud.tscn
scripts/ui/ExplorationHudController.cs
tests/ui/ExplorationHudControllerTest.cs
scenes/game/Game.tscn
scripts/game/Game.cs
scenes/ui/InventoryMenu.tscn
scripts/ui/InventoryMenuController.cs
tests/ui/InventoryMenuControllerTest.cs
tests/game/GameTest.cs
tests/game/GameInputLifecycleTest.cs
tests/game/GameplayPauseHostTest.cs
scripts/ui/DraggablePanel.cs   # deleted
```

Plus the approved HPA-381 design/plan documents when implementation starts from this planning branch.

No `SiriusStatBar`, Theme resource, host, domain-model, battle, save, settings, or other Inventory files should change.

### Step 15: Commit the complete production cutover

- [ ] Commit only after Steps 11–14 are green:

```bash
git add scenes/game/Game.tscn \
  scripts/game/Game.cs \
  scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  tests/game/GameplayPauseHostTest.cs
git add -u scripts/ui/DraggablePanel.cs
git commit -m "feat(ui): replace exploration debug HUD"
```

Do not split scene deletion, prompt conversion, Gold preservation, lifecycle-test migration, or dead-script deletion into separate commits.

---

## Review-specific decisions

### Adopted

- Preserve Gold discoverability by adding one minimal Inventory readout in the same cutover.
- Promote the already-approved 1600px ultrawide limit to `SiriusUiMetrics` and migrate the showcase duplicate.
- Use one transient region/timer with area-title precedence and queued session hint.
- Preserve the actual hero single-frame crop as `AtlasTexture.Region = Rect2(0, 0, 96, 96)`; the committed sheet is 384×96, so the review's proposed 32×32 crop is not correct for this asset.
- Remove the vacuous legacy-node assertions from the new HUD scene test; test legacy removal on production `Game.tscn` plus the final `rg` audit.
- Delete `DraggablePanel.cs` after repository-wide search proved `Game.tscn` is its only consumer.
- Author HP/MP static Kind/Label values in the scene instead of reassigning them per refresh.
- Keep transient timer running during Pause with `ProcessMode.Always`.
- Explicitly document that the prompt connector is decorative; no world-to-HUD anchor tracking is introduced.

### Partially adopted / intentionally constrained

- Do **not** add `ShowHeader`, `ShowState`, or a Thin mode to `SiriusStatBar`. HPA-377 defines it as a fixed richer composite with always-visible numeric/state presentation, while HPA-373 specifically asks for a thin EXP line. HPA-381 instead removes the redundant EXP label/formatter and uses only the existing themed native `ProgressBar` variation `SiriusExpBar`.
- Keep the missing-portrait fallback/test. HPA-381 explicitly requires graceful optional portrait handling even though the committed production scene normally supplies the texture.
- Do not early-return from `ShowInteractionPrompt` when text/icon are unchanged: the final `Refresh()` is required to reflect Settings remaps of the same `interact` action. Avoid redundant setter work instead.
