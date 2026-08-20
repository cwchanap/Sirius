# HPA-625 Dialogue NPC Portraits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit optional NPC portrait data and render current shopkeeper/healer portraits in the existing Dialogue screen with clean absent, missing-path, and wrong-type fallbacks.

**Architecture:** Add one nullable `NpcData.PortraitPath`, keep `SpriteType` world-only, reuse `UiIconPresenter.ApplyItem` for `TextureRect` presentation, and extend `UiArtCatalog` with one raw-texture entry point backed by a type-safe `LoadOnce<T>`. `DialogueScreenController` refreshes portrait only from its existing `ShowNode(...)` render path. The scene reuses the shipped portrait + expanding-copy row pattern; no portrait service, registry, cache, crop metadata, new art pipeline, or theme metric.

**Tech Stack:** Godot 4.6, C#, GdUnit4, existing Sirius UI art helpers and `SiriusModalShell`.

**Spec:** `docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md`

## Global Constraints

- One branch / one PR for HPA-625. Continue implementation on `jack65786656/hpa-625-add-sirius-npc-portrait-data-and-dialogue-identity` / PR #43.
- `NpcData.PortraitPath : string?` is the only new NPC portrait contract.
- `PortraitPath` points at a tracked, ready-to-display `Texture2D`; Dialogue does not infer sprite-sheet crop semantics.
- `NpcData.SpriteType` remains world-sprite metadata and must not become a Dialogue portrait dependency.
- Shopkeeper portrait: `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`.
- Healer portrait: `res://assets/sprites/npcs/healer/frames/frame1.png`.
- Missing `PortraitPath` is valid and silent.
- Missing and wrong-type configured paths warn once through `UiArtCatalog` and render the same hidden-portrait fallback.
- Portrait `TextureRect` presentation uses existing `UiIconPresenter.ApplyItem(...)`; do not duplicate expand/stretch configuration in the scene.
- `RefreshPortrait()` is called only from `ShowNode(...)`, immediately beside shell-title identity rendering.
- Standard portrait size is 64 logical pixels; compact portrait size is 40. Keep both local to Dialogue and do not test by merely reading the assigned `CustomMinimumSize` back.
- No portrait registry/service/cache/resource database, generic portrait crop model, new art, host lifecycle change, or dialogue-domain change.
- `.gitignore` remains unchanged. HPA-625 adds no image binary and defines no future portrait storage convention.
- Keep existing shell title, speaker, text, choices, focus, terminal latch, Shop/Heal handoff, and NPC interaction behavior.

---

## File Structure

### Task 1 — NPC data contract

- Modify: `scripts/data/npc/NpcData.cs`
- Modify: `scripts/data/npc/NpcCatalog.cs`
- Modify: `tests/data/npc/NpcCatalogTest.cs`

### Task 2 — Type-safe optional UI texture loading

- Modify: `scripts/ui/art/UiArtCatalog.cs`
- Modify: `tests/ui/art/UiArtCatalogTest.cs`

### Task 3 — Dialogue scene and portrait presentation

- Modify: `scenes/ui/DialogueScreen.tscn`
- Modify: `scripts/ui/DialogueScreenController.cs`
- Modify: `tests/ui/DialogueScreenControllerTest.cs`
- Reuse unchanged: `scripts/ui/art/UiIconPresenter.cs`

### Task 4 — Final regression and scope gates

- Verify all files above plus the two planning documents.

---

# Task 1: Add the optional NPC portrait data contract

**Files:**
- Modify: `scripts/data/npc/NpcData.cs`
- Modify: `scripts/data/npc/NpcCatalog.cs`
- Modify: `tests/data/npc/NpcCatalogTest.cs`

**Produces:**

```csharp
public string? PortraitPath { get; init; }
```

plus explicit shopkeeper/healer mappings.

- [ ] **Step 1: Write failing catalog integrity and optionality tests**

Add `using Godot;` to `tests/data/npc/NpcCatalogTest.cs`.

Add:

```csharp
[TestCase]
public void NpcCatalog_AllAuthoredPortraits_AreLoadableTextures()
{
    foreach (var npc in NpcCatalog.AllNpcs)
    {
        if (npc.PortraitPath == null)
            continue;

        AssertThat(npc.PortraitPath).IsNotEmpty();
        AssertThat(ResourceLoader.Exists(npc.PortraitPath)).IsTrue();
        AssertThat(ResourceLoader.Load<Resource>(npc.PortraitPath) is Texture2D).IsTrue();
    }

    AssertThat(NpcCatalog.GetById("village_shopkeeper")!.PortraitPath).IsNotNull();
    AssertThat(NpcCatalog.GetById("village_healer")!.PortraitPath).IsNotNull();
}

[TestCase]
public void NpcData_PortraitPath_IsOptional()
{
    var npc = new NpcData
    {
        NpcId = "test_optional_portrait",
        DisplayName = "Test NPC",
        NpcType = NpcType.Villager,
        DialogueTreeId = "villager_01",
        SpriteType = "villager"
    };

    AssertThat(npc.PortraitPath).IsNull();
}
```

Do not assert `NpcCatalog.AllNpcs.Any(npc => npc.PortraitPath == null)`: optionality belongs to the model contract, not the current catalog census.

Do not derive expected portrait paths from `SpriteType`.

- [ ] **Step 2: Run focused tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter FullyQualifiedName~NpcCatalogTest
```

Expected: compile failure because `NpcData.PortraitPath` does not exist.

- [ ] **Step 3: Add `PortraitPath` to `NpcData`**

Add near the existing Dialogue/presentation identity data:

```csharp
/// <summary>
/// Optional player-facing portrait resource used by Dialogue identity presentation.
/// Independent from SpriteType, which remains world-sprite metadata.
/// </summary>
public string? PortraitPath { get; init; }
```

Leave `SpriteType` and its comment unchanged.

- [ ] **Step 4: Add only the two shipped catalog mappings**

In `CreateVillageShopkeeper()`:

```csharp
PortraitPath = "res://assets/sprites/npcs/shopkeeper/frames/frame1.png",
```

In `CreateVillageHealer()`:

```csharp
PortraitPath = "res://assets/sprites/npcs/healer/frames/frame1.png",
```

Do not map farmer/blacksmith merely to remove nulls. Do not replace these direct portrait textures with `sprite_sheet.png` plus a Dialogue-local crop convention.

- [ ] **Step 5: Run focused tests and confirm GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter FullyQualifiedName~NpcCatalogTest
```

Expected: PASS.

- [ ] **Step 6: Commit Task 1**

```bash
git add -- \
  scripts/data/npc/NpcData.cs \
  scripts/data/npc/NpcCatalog.cs \
  tests/data/npc/NpcCatalogTest.cs
git commit -m "feat: add explicit NPC portrait data"
```

---

# Task 2: Make optional UI texture loading safe for authored raw paths

**Files:**
- Modify: `scripts/ui/art/UiArtCatalog.cs`
- Modify: `tests/ui/art/UiArtCatalogTest.cs`

**Consumes:** existing private `LoadOnce<T>`, `MissingPaths`, and `ResourceExists` test seam.

**Produces:**

```csharp
public static Texture2D? LoadContentTexture(string path);
```

and a type-safe `LoadOnce<T>` that returns `null` rather than throwing when an existing resource has the wrong type.

- [ ] **Step 1: Write the failing missing-path test for the new raw texture entry point**

Using the existing reflection helpers in `UiArtCatalogTest.cs`:

```csharp
[TestCase]
public void Catalog_LoadContentTexture_DeduplicatesMissingPath()
{
    const string path = "res://test/missing-npc-portrait.png";
    var missingPaths = GetMissingPaths();
    var originalResourceExists = GetResourceExists();
    missingPaths.Clear();
    SetResourceExists(_ => false);

    try
    {
        AssertThat(UiArtCatalog.LoadContentTexture(path)).IsNull();
        AssertThat(UiArtCatalog.LoadContentTexture(path)).IsNull();
        AssertThat(missingPaths.SetEquals(new[] { path })).IsTrue();
    }
    finally
    {
        SetResourceExists(originalResourceExists);
        missingPaths.Clear();
    }
}
```

- [ ] **Step 2: Write the wrong-type regression that the old helper cannot satisfy safely**

Use a real existing non-texture Godot resource:

```csharp
[TestCase]
public void Catalog_LoadContentTexture_ExistingWrongTypeReturnsNullAndDeduplicates()
{
    const string path = "res://scenes/ui/DialogueScreen.tscn";
    var missingPaths = GetMissingPaths();
    missingPaths.Clear();

    try
    {
        AssertThat(ResourceLoader.Exists(path)).IsTrue();
        AssertThat(UiArtCatalog.LoadContentTexture(path)).IsNull();
        AssertThat(UiArtCatalog.LoadContentTexture(path)).IsNull();
        AssertThat(missingPaths.SetEquals(new[] { path })).IsTrue();
    }
    finally
    {
        missingPaths.Clear();
    }
}
```

This is the load-bearing invalid-data case. Do not replace it with another nonexistent path.

- [ ] **Step 3: Run focused art tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter FullyQualifiedName~UiArtCatalogTest
```

Expected: compile failure because `LoadContentTexture` does not exist.

- [ ] **Step 4: Add the narrow raw texture delegation**

In `UiArtCatalog.cs` near the other load methods:

```csharp
public static Texture2D? LoadContentTexture(string path) =>
    LoadOnce<Texture2D>(path);
```

No portrait-specific path derivation, registry, cache object, or new loader class.

- [ ] **Step 5: Make `LoadOnce<T>` wrong-type-safe while preserving warning dedupe**

Replace the private helper with:

```csharp
private static T? LoadOnce<T>(string path) where T : Resource
{
    if (ResourceExists(path))
    {
        var resource = ResourceLoader.Load<Resource>(path);
        if (resource is T typed)
            return typed;

        WarnOnce(
            path,
            $"[UiArtCatalog] Optional UI art resource has unexpected type: {path}");
        return null;
    }

    WarnOnce(path, $"[UiArtCatalog] Missing optional UI art resource: {path}");
    return null;
}

private static void WarnOnce(string path, string message)
{
    if (MissingPaths.Add(path))
        GD.PushWarning(message);
}
```

`MissingPaths` remains the existing dedupe set even though it now also records wrong-type optional art. Renaming it is not required for this ticket.

Do not catch arbitrary loader exceptions or create a generalized resource-validation framework.

- [ ] **Step 6: Run the complete art suite and confirm GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter FullyQualifiedName~UiArtCatalogTest
```

Expected: PASS, including existing icon/ornament/effect behavior and the two new raw-path cases.

- [ ] **Step 7: Commit Task 2**

```bash
git add -- scripts/ui/art/UiArtCatalog.cs tests/ui/art/UiArtCatalogTest.cs
git commit -m "fix: make optional UI texture loading type safe"
```

---

# Task 3: Add the Dialogue identity row and portrait rendering

**Files:**
- Modify: `scenes/ui/DialogueScreen.tscn`
- Modify: `scripts/ui/DialogueScreenController.cs`
- Modify: `tests/ui/DialogueScreenControllerTest.cs`
- Reuse unchanged: `scripts/ui/art/UiIconPresenter.cs`

**Consumes:** `NpcData.PortraitPath`, `UiArtCatalog.LoadContentTexture(...)`, and the existing `ShowNode(...)` render path.

**Produces:** `%NpcPortrait`, portrait/copy row layout, and clean fallbacks while preserving existing dialogue lifecycle/focus behavior.

## 3A. Write portrait regressions before production edits

- [ ] **Step 1: Add the authored production-order portrait test**

Existing `TryStartDialogue_BeforeReady_RendersAfterAttach` already proves general speaker/body/choices/focus ordering. Add only portrait-specific assertions:

```csharp
[TestCase]
public async Task AuthoredPortrait_BeforeReadyStart_RendersPortraitAfterAttach()
{
    var screen = CreateUnparentedCandidate();
    var npc = NpcCatalog.GetById("village_shopkeeper")!;
    var tree = DialogueCatalog.GetById("shopkeeper_greeting")!;

    AssertThat(screen.TryStartDialogue(
        npc,
        tree,
        TestHelpers.CreateTestCharacter(),
        new HashSet<string>())).IsTrue();

    var fixture = Mount(screen, new Vector2I(1280, 720));
    try
    {
        await AwaitFrames(2);

        var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
        AssertThat(portrait.Visible).IsTrue();
        AssertThat(portrait.Texture).IsNotNull();
        AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
            .IsEqual(npc.DisplayName);
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

This must fail if portrait rendering is wired only to the already-ready start branch and not the shared `ShowNode(...)` path.

- [ ] **Step 2: Add deterministic synthetic missing-portrait coverage**

Do not select `First(npc => npc.PortraitPath == null)` from the catalog.

```csharp
[TestCase]
public async Task MissingPortrait_HidesPortraitAndKeepsFocusUsable()
{
    var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
    try
    {
        var screen = fixture.Screen;
        var tree = DialogueCatalog.GetById("villager_01")!;
        var npc = new NpcData
        {
            NpcId = "test_no_portrait",
            DisplayName = "Test Villager",
            NpcType = NpcType.Villager,
            DialogueTreeId = tree.TreeId,
            SpriteType = "villager",
            PortraitPath = null
        };

        AssertThat(screen.TryStartDialogue(
            npc,
            tree,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>())).IsTrue();
        await AwaitFrames(2);

        var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
        AssertThat(portrait.Visible).IsFalse();
        AssertThat(portrait.Texture).IsNull();
        AssertThat(screen.InitialFocusTarget).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

- [ ] **Step 3: Add missing-path fallback coverage**

```csharp
[TestCase]
public async Task MissingPortraitPath_HidesPortraitAndKeepsFocusUsable()
{
    var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
    try
    {
        var screen = fixture.Screen;
        var tree = DialogueCatalog.GetById("shopkeeper_greeting")!;
        var npc = new NpcData
        {
            NpcId = "test_missing_portrait_path",
            DisplayName = "Test Merchant",
            NpcType = NpcType.Shopkeeper,
            DialogueTreeId = tree.TreeId,
            SpriteType = "shopkeeper",
            PortraitPath = "res://assets/sprites/npcs/does-not-exist/portrait.png"
        };

        AssertThat(screen.TryStartDialogue(
            npc,
            tree,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>())).IsTrue();
        await AwaitFrames(2);

        var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
        AssertThat(portrait.Visible).IsFalse();
        AssertThat(portrait.Texture).IsNull();
        AssertThat(screen.InitialFocusTarget).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

- [ ] **Step 4: Add existing-but-wrong-type fallback coverage**

```csharp
[TestCase]
public async Task WrongTypePortraitPath_HidesPortraitAndKeepsFocusUsable()
{
    var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
    try
    {
        var screen = fixture.Screen;
        var tree = DialogueCatalog.GetById("shopkeeper_greeting")!;
        var npc = new NpcData
        {
            NpcId = "test_wrong_type_portrait",
            DisplayName = "Test Merchant",
            NpcType = NpcType.Shopkeeper,
            DialogueTreeId = tree.TreeId,
            SpriteType = "shopkeeper",
            PortraitPath = "res://scenes/ui/DialogueScreen.tscn"
        };

        AssertThat(ResourceLoader.Exists(npc.PortraitPath)).IsTrue();
        AssertThat(screen.TryStartDialogue(
            npc,
            tree,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>())).IsTrue();
        await AwaitFrames(2);

        var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
        AssertThat(portrait.Visible).IsFalse();
        AssertThat(portrait.Texture).IsNull();
        AssertThat(screen.InitialFocusTarget).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

This pins the authored-string failure mode that `ResourceLoader.Exists(...)` alone cannot catch.

- [ ] **Step 5: Make the existing standard geometry test portrait-sensitive**

In `StandardDialogue_StaysWithinLowerBand`, use the portrait-bearing shopkeeper/tree, retain all existing lower-band/safe-frame assertions, and add horizontal composition checks:

```csharp
var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
var copy = screen.GetNode<VBoxContainer>(
    "SafeFrame/ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/IdentityRow/DialogueCopy");
var dialogueText = screen.GetNode<RichTextLabel>("%DialogueText");

AssertThat(portrait.Visible).IsTrue();
AssertThat(portrait.GetGlobalRect().End.X)
    .IsLessEqual(copy.GetGlobalRect().Position.X + 1f);
AssertThat(dialogueText.Size.X).IsGreater(200f);
```

Do not add `portrait.CustomMinimumSize == new Vector2(64f, 64f)`; that only echoes the value assigned by `RefreshLayout()`.

- [ ] **Step 6: Make the existing compact overflow/focus test portrait-sensitive**

In `CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice`:

- keep its existing synthetic overflow tree;
- pass `village_shopkeeper` as the NPC so portrait data is present;
- retain every existing full-safe-height, `VerticalScrollBar.MaxValue > Page`, focused-choice visibility/scroll, and action assertion;
- add:

```csharp
var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
var copy = screen.GetNode<VBoxContainer>(
    "SafeFrame/ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/IdentityRow/DialogueCopy");
var dialogueText = screen.GetNode<RichTextLabel>("%DialogueText");

AssertThat(portrait.Visible).IsTrue();
AssertThat(portrait.GetGlobalRect().End.X)
    .IsLessEqual(copy.GetGlobalRect().Position.X + 1f);
AssertThat(dialogueText.Size.X).IsGreater(200f);
```

The existing overflow assertion is portrait-sensitive because the 40-pixel portrait plus row spacing reduces text width and changes wrapping/body height.

- [ ] **Step 7: Run Dialogue tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter FullyQualifiedName~DialogueScreenControllerTest
```

Expected: FAIL on missing `%NpcPortrait` / portrait behavior.

## 3B. Edit and verify the scene before touching controller code

- [ ] **Step 8: Add `IdentityRow` and reparent copy in `DialogueScreen.tscn`**

The declaration order must be:

```text
BodyHost
├── IdentityRow
│   ├── NpcPortrait
│   └── DialogueCopy
│       ├── SpeakerLabel
│       └── DialogueText
└── ChoicesContainer
```

Use:

```ini
[node name="IdentityRow" type="HBoxContainer" parent="SafeFrame/ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
theme_override_constants/separation = 12

[node name="NpcPortrait" type="TextureRect" parent="SafeFrame/ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/IdentityRow"]
unique_name_in_owner = true
visible = false
custom_minimum_size = Vector2(64, 64)
layout_mode = 2
mouse_filter = 2

[node name="DialogueCopy" type="VBoxContainer" parent="SafeFrame/ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/IdentityRow"]
layout_mode = 2
size_flags_horizontal = 3
theme_override_constants/separation = 4
```

Move the existing `%SpeakerLabel` and `%DialogueText` declarations immediately after `DialogueCopy` and update only their `parent` paths to include `/IdentityRow/DialogueCopy`.

Keep `%ChoicesContainer` as the **last node block in the file and the final direct child of `BodyHost`**. Do not append `IdentityRow` after the existing choices block.

Do not set `expand_mode` / `stretch_mode` on `%NpcPortrait`; `UiIconPresenter.ApplyItem(...)` owns them.

- [ ] **Step 9: Verify the scene edit in isolation with the unmodified controller**

Run only existing tests that depend on the moved unique-name nodes and authored scene:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter 'FullyQualifiedName~DialogueScreenControllerTest.Scene_UsesSafeFrameModalShellAndContainsNoAcceptDialog|FullyQualifiedName~DialogueScreenControllerTest.SpeakerName_BlankHidesSpeakerLabel'
```

Expected: PASS **before** any `DialogueScreenController.cs` portrait edits.

If this fails, fix the `.tscn` parent path/declaration order first. Do not continue into controller changes with a broken scene tree.

## 3C. Bind and render portrait through the one existing render path

- [ ] **Step 10: Bind portrait before any layout/render call in `_Ready()`**

Add:

```csharp
private const float StandardPortraitSize = 64f;
private const float CompactPortraitSize = 40f;
private TextureRect _portrait = null!;
```

In the existing `_Ready()` `GetNode` block, bind `%NpcPortrait` **with `_speakerLabel`, `_textLabel`, and `_choicesContainer`, before `Resized += OnResized;`, `RefreshLayout()`, or the `_currentNode` `ShowNode(...)` call**:

```csharp
_portrait = GetNode<TextureRect>("%NpcPortrait");
```

This ordering is mandatory because `RefreshLayout()` will dereference `_portrait`.

- [ ] **Step 11: Apply local responsive portrait size in `RefreshLayout()`**

After `var insets = SiriusUiMetrics.SafeFrameInsets(size);`:

```csharp
var portraitSize = insets.Compact
    ? CompactPortraitSize
    : StandardPortraitSize;
_portrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
```

Do not change the existing 45% standard lower band, compact full-safe-height behavior, shell size class, text theme variations, or action target sizing.

- [ ] **Step 12: Add `RefreshPortrait()` using existing helpers**

```csharp
private void RefreshPortrait()
{
    var path = _npc?.PortraitPath;
    var texture = string.IsNullOrWhiteSpace(path)
        ? null
        : UiArtCatalog.LoadContentTexture(path);

    UiIconPresenter.ApplyItem(_portrait, texture);
    _portrait.Visible = texture != null;
}
```

No `IsNodeReady()` guard and no sprite-sheet crop logic.

- [ ] **Step 13: Wire exactly one portrait refresh call in `ShowNode(...)`**

Keep current rendering order and add:

```csharp
_currentNode = node;
_shell.Title = _npc?.DisplayName ?? string.Empty;
RefreshPortrait();
_speakerLabel.Text = node.SpeakerName ?? string.Empty;
```

Do not call `RefreshPortrait()` directly from `_Ready()` or `TryStartDialogue(...)`.

- [ ] **Step 14: Run the complete Dialogue suite and confirm GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter FullyQualifiedName~DialogueScreenControllerTest
```

Expected: PASS, including:

- existing before-ready lifecycle coverage;
- authored portrait before attach;
- synthetic missing portrait;
- missing path;
- existing wrong-type path;
- standard no-overlap/text width;
- compact overflow/focus/no-overlap/text width.

- [ ] **Step 15: Run data + art + Dialogue focused suites together**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter 'FullyQualifiedName~NpcCatalogTest|FullyQualifiedName~UiArtCatalogTest|FullyQualifiedName~DialogueScreenControllerTest'
```

Expected: PASS.

- [ ] **Step 16: Commit Task 3**

```bash
git add -- \
  scenes/ui/DialogueScreen.tscn \
  scripts/ui/DialogueScreenController.cs \
  tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat: render Dialogue NPC portraits"
```

---

# Task 4: Run final regression and scope gates

**Files:** verification only unless a gate exposes a real HPA-625 defect.

- [ ] **Step 1: Run all focused HPA-625 suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter 'FullyQualifiedName~NpcCatalogTest|FullyQualifiedName~UiArtCatalogTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest'
```

Expected: PASS. `NpcInteractionControllerTest` is regression-only; production interaction code remains unchanged.

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: all tests pass.

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: exit 0.

- [ ] **Step 4: Verify portrait/world-sprite separation**

```bash
if rg -n 'SpriteType' scripts/ui/DialogueScreenController.cs scenes/ui/DialogueScreen.tscn; then
  echo 'Dialogue portrait presentation must not depend on SpriteType'
  exit 1
fi
```

Expected: no matches.

- [ ] **Step 5: Verify the single portrait refresh call site**

```bash
calls=$(rg -n 'RefreshPortrait\(\);' scripts/ui/DialogueScreenController.cs | wc -l | tr -d ' ')
test "$calls" = "1"
```

Expected: one call, in `ShowNode(...)`.

- [ ] **Step 6: Verify raw texture loading no longer uses the throwing generic cast path**

```bash
rg -n 'ResourceLoader\.Load<Resource>\(path\)|resource is T typed|WarnOnce' \
  scripts/ui/art/UiArtCatalog.cs
```

Expected: the type-safe base-resource load, pattern match, and deduplicated warning helper are present.

Then:

```bash
if rg -n 'ResourceLoader\.Load<T>\(path\)' scripts/ui/art/UiArtCatalog.cs; then
  echo 'LoadOnce<T> must not cast authored raw paths through ResourceLoader.Load<T>'
  exit 1
fi
```

Expected: no matches.

- [ ] **Step 7: Verify reused portrait resources are tracked and no new binary was added**

```bash
git ls-files --error-unmatch assets/sprites/npcs/shopkeeper/frames/frame1.png
git ls-files --error-unmatch assets/sprites/npcs/healer/frames/frame1.png

if git diff --name-only main...HEAD | grep -E '\.(png|jpg|jpeg|webp)$'; then
  echo 'HPA-625 should reuse tracked art and add no image binary'
  exit 1
fi
```

Expected: both existing portrait resources resolve; no image files are in the branch diff.

No `.gitignore` content assertion is needed beyond normal diff scope: HPA-625 simply does not modify it.

- [ ] **Step 8: Verify exact implementation surface**

```bash
cat > /tmp/hpa625-expected-files <<'EOF'
docs/superpowers/plans/2026-08-19-hpa-625-dialogue-npc-portraits.md
docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md
scenes/ui/DialogueScreen.tscn
scripts/data/npc/NpcCatalog.cs
scripts/data/npc/NpcData.cs
scripts/ui/DialogueScreenController.cs
scripts/ui/art/UiArtCatalog.cs
tests/data/npc/NpcCatalogTest.cs
tests/ui/DialogueScreenControllerTest.cs
tests/ui/art/UiArtCatalogTest.cs
EOF

git diff --name-only main...HEAD | sort > /tmp/hpa625-actual-files
diff -u /tmp/hpa625-expected-files /tmp/hpa625-actual-files
```

Expected: no diff.

`UiIconPresenter.cs`, `NpcInteractionController.cs`, `NpcSpawn.cs`, `.gitignore`, and all image resources are intentionally absent because HPA-625 reuses them unchanged.

- [ ] **Step 9: Inspect the final runtime diff**

Reject any accidental addition of:

- portrait registry/service/cache/presenter class;
- `SpriteType` portrait inference;
- generic sprite-sheet crop metadata or AtlasTexture convention in `NpcData`/Dialogue;
- host/domain/dialogue-tree changes;
- new image files or `.gitignore` policy changes;
- second NPC-name label;
- duplicate `_Ready()` / `TryStartDialogue(...)` portrait refresh call sites;
- hand-rolled Dialogue resource loading instead of `UiArtCatalog.LoadContentTexture(...)`;
- tautological geometry tests that only assert the local 64/40 values assigned by the controller.

- [ ] **Step 10: Check patch integrity**

```bash
git diff --check main...HEAD
```

Expected: no output.

- [ ] **Step 11: Mark the design implemented only after all gates are fresh and green**

Add directly below the design title:

```markdown
**Status:** Implemented in HPA-625.
```

Do not claim HPA-358, HPA-359, or HPA-541 completion.

- [ ] **Step 12: Commit closeout documentation**

```bash
git add -- docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md
git commit -m "docs: close HPA-625 dialogue portraits"
```

- [ ] **Step 13: Update the existing PR #43**

Keep this branch/PR as the only HPA-625 PR. Update the PR body with actual runtime files and fresh test/build results after implementation.

---

## Review disposition

Accepted:

- harden `UiArtCatalog.LoadOnce<T>` for existing wrong-type resources and test an existing `.tscn` path;
- replace the catalog-null census and nondeterministic missing-portrait selection with synthetic fixtures;
- replace tautological 64/40 geometry assertions with portrait/copy non-overlap, text-width, and existing compact overflow/focus checks;
- verify the `.tscn` reparent before controller edits, keep `ChoicesContainer` last, and bind `_portrait` before layout/render;
- remove the speculative future `portrait.png` convention.

Partially accepted:

- Inventory/HUD prove the row/presenter shape and use scene-authored `AtlasTexture` crops for one fixed hero sheet. HPA-625 reuses that composition but keeps `PortraitPath` as a ready-to-display texture resource; it does not make every NPC portrait consumer understand four-frame sheet layout.

## Post-merge Linear closeout

After PR #43 merges:

1. re-fetch HPA-625;
2. add one concise comment with merged PR and shipped scope;
3. mark HPA-625 Done;
4. evaluate HPA-358’s workstream checklist separately;
5. do not create a portrait-framework follow-up merely for generalized reuse.