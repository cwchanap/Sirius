# HPA-625 Dialogue NPC Portraits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit optional NPC portrait data and render current shopkeeper/healer portraits in the existing Dialogue screen with clean absent/invalid fallbacks.

**Architecture:** Add one nullable `NpcData.PortraitPath`, keep `SpriteType` world-only, reuse `UiArtCatalog` for optional texture loading and `UiIconPresenter.ApplyItem` for `TextureRect` presentation, and refresh portrait from the existing `ShowNode(...)` render path. Reuse existing Dialogue geometry tests for portrait-bearing standard/compact coverage; no portrait service/registry/cache/new art pipeline.

**Tech Stack:** Godot 4.6, C#, GdUnit4, existing Sirius UI art helpers and `SiriusModalShell`.

**Spec:** `docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md`

## Global Constraints

- One branch / one PR for HPA-625. Continue implementation on `jack65786656/hpa-625-add-sirius-npc-portrait-data-and-dialogue-identity` / PR #43.
- `NpcData.PortraitPath : string?` is the only new NPC portrait contract.
- `NpcData.SpriteType` remains world-sprite metadata and must not become a Dialogue portrait dependency.
- Shopkeeper portrait: `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`.
- Healer portrait: `res://assets/sprites/npcs/healer/frames/frame1.png`.
- Missing `PortraitPath` is valid and silent.
- Invalid configured path uses existing optional-resource Exists-before-Load + warn-once behavior through `UiArtCatalog.LoadContentTexture(...)`.
- Portrait `TextureRect` presentation uses existing `UiIconPresenter.ApplyItem(...)`; do not hardcode numeric expand/stretch modes in the scene.
- `RefreshPortrait()` is called only from `ShowNode(...)`, immediately beside shell-title identity rendering.
- Standard portrait size: 64x64. Compact portrait size: 40x40. Keep both as local Dialogue constants.
- No portrait registry, service, cache, resource database, new art, theme metric, host lifecycle change, or dialogue-domain change.
- `.gitignore` remains unchanged. Existing tracked `frame1.png` files are reused; future new portrait content uses dedicated `portrait.png` outside ignored `frames/` unless a future task intentionally redesigns frame ignore policy.
- Keep existing shell title, speaker, text, choices, focus, terminal latch, Shop/Heal handoff, and NPC interaction behavior.

---

## Task 1: Add the optional NPC portrait data contract

**Files:**
- Modify: `scripts/data/npc/NpcData.cs`
- Modify: `scripts/data/npc/NpcCatalog.cs`
- Modify: `tests/data/npc/NpcCatalogTest.cs`

**Produces:**

```csharp
public string? PortraitPath { get; init; }
```

and explicit shopkeeper/healer mappings.

- [ ] **Step 1: Write one failing catalog-integrity test**

Add `using Godot;` and `using System.Linq;` to `NpcCatalogTest.cs`.

Add one test:

```csharp
[TestCase]
public void NpcCatalog_AllAuthoredPortraits_AreLoadableAndOptional()
{
    var npcs = NpcCatalog.AllNpcs.ToArray();

    foreach (var npc in npcs)
    {
        if (npc.PortraitPath == null)
            continue;

        AssertThat(npc.PortraitPath).IsNotEmpty();
        AssertThat(ResourceLoader.Exists(npc.PortraitPath)).IsTrue();
        AssertThat(ResourceLoader.Load<Texture2D>(npc.PortraitPath)).IsNotNull();
    }

    AssertThat(NpcCatalog.GetById("village_shopkeeper")!.PortraitPath).IsNotNull();
    AssertThat(NpcCatalog.GetById("village_healer")!.PortraitPath).IsNotNull();
    AssertThat(npcs.Any(npc => npc.PortraitPath == null)).IsTrue();
}
```

Do **not** assert farmer/blacksmith must remain null forever and do not derive expected paths from `SpriteType`.

- [ ] **Step 2: Run focused test and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~NpcCatalogTest
```

Expected: compile failure because `PortraitPath` does not exist.

- [ ] **Step 3: Add `PortraitPath` to `NpcData`**

```csharp
/// <summary>
/// Optional player-facing portrait resource used by Dialogue identity presentation.
/// Independent from SpriteType, which remains world-sprite metadata.
/// </summary>
public string? PortraitPath { get; init; }
```

Leave `SpriteType` unchanged.

- [ ] **Step 4: Add the two shipped catalog mappings**

In `CreateVillageShopkeeper()`:

```csharp
PortraitPath = "res://assets/sprites/npcs/shopkeeper/frames/frame1.png",
```

In `CreateVillageHealer()`:

```csharp
PortraitPath = "res://assets/sprites/npcs/healer/frames/frame1.png",
```

Do not add portrait data to the other factories.

- [ ] **Step 5: Run focused test and confirm GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~NpcCatalogTest
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

## Task 2: Reuse UI art helpers and render Dialogue portraits

**Files:**
- Modify: `scripts/ui/art/UiArtCatalog.cs`
- Modify: `tests/ui/art/UiArtCatalogTest.cs`
- Modify: `scenes/ui/DialogueScreen.tscn`
- Modify: `scripts/ui/DialogueScreenController.cs`
- Modify: `tests/ui/DialogueScreenControllerTest.cs`
- Reuse unchanged: `scripts/ui/art/UiIconPresenter.cs`

**Consumes:** `NpcData.PortraitPath` from Task 1.

**Produces:**

```csharp
public static Texture2D? UiArtCatalog.LoadContentTexture(string path);
```

and `%NpcPortrait` rendered from the single `ShowNode(...)` identity path.

### 2A. Pin the existing optional-resource policy for raw content textures

- [ ] **Step 1: Write the failing `UiArtCatalog` test**

Use the existing reflection helpers (`GetMissingPaths`, `GetResourceExists`, `SetResourceExists`) in `UiArtCatalogTest.cs`:

```csharp
[TestCase]
public void Catalog_LoadContentTexture_DeduplicatesMissingWarnings()
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

This pins Exists-before-Load and warn-once through the existing `LoadOnce<T>` behavior without adding a warning-capture production seam.

- [ ] **Step 2: Run the focused art test and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~UiArtCatalogTest
```

Expected: compile failure because `LoadContentTexture` does not exist.

- [ ] **Step 3: Add the narrow delegation**

In `UiArtCatalog.cs` near other load methods:

```csharp
public static Texture2D? LoadContentTexture(string path) =>
    LoadOnce<Texture2D>(path);
```

Do not add path derivation, cache objects, portrait IDs, or a new loader class.

- [ ] **Step 4: Run the focused art test and confirm GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~UiArtCatalogTest
```

Expected: PASS.

### 2B. Write focused Dialogue portrait regressions and extend existing geometry tests

- [ ] **Step 5: Add the authored before-attach portrait test**

Existing `TryStartDialogue_BeforeReady_RendersAfterAttach` already proves title/speaker/body/choices/focus ordering with `old_farmer`. Do not duplicate those assertions.

Add:

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

This test proves the portrait participates in the already-supported configure-before-attach flow; it is not a second generic start-order test.

- [ ] **Step 6: Add missing and invalid portrait tests**

Missing optional portrait:

```csharp
[TestCase]
public async Task MissingPortrait_HidesPortraitAndKeepsFocusUsable()
{
    var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
    try
    {
        var screen = fixture.Screen;
        AssertThat(screen.TryStartDialogue(
            NpcCatalog.AllNpcs.First(npc => npc.PortraitPath == null),
            DialogueCatalog.GetById("villager_01")!,
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

If the selected portrait-less NPC's catalog dialogue tree differs from `villager_01`, use that NPC's own `DialogueTreeId` to resolve the tree. Do not pin a particular NPC as permanently portrait-less.

Invalid configured portrait:

```csharp
[TestCase]
public async Task InvalidPortraitPath_HidesPortraitAndKeepsFocusUsable()
{
    var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
    try
    {
        var screen = fixture.Screen;
        var tree = DialogueCatalog.GetById("shopkeeper_greeting")!;
        var npc = new NpcData
        {
            NpcId = "test_missing_portrait",
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

- [ ] **Step 7: Make existing standard geometry test portrait-bearing**

In `StandardDialogue_StaysWithinLowerBand`, replace the portrait-less NPC/tree with:

```csharp
var npc = NpcCatalog.GetById("village_shopkeeper")!;
var tree = DialogueCatalog.GetById("shopkeeper_greeting")!;
screen.TryStartDialogue(
    npc,
    tree,
    TestHelpers.CreateTestCharacter(),
    new HashSet<string>());
```

Keep every existing lower-band containment assertion and add:

```csharp
var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
AssertThat(portrait.Visible).IsTrue();
AssertThat(portrait.CustomMinimumSize).IsEqual(new Vector2(64f, 64f));
```

- [ ] **Step 8: Make existing compact scroll test portrait-bearing**

In `CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice`, keep the synthetic overflow tree but pass `village_shopkeeper` instead of `old_farmer`.

Keep all current full-safe-height and scroll-to-focused-choice assertions and add:

```csharp
var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
AssertThat(portrait.Visible).IsTrue();
AssertThat(portrait.CustomMinimumSize).IsEqual(new Vector2(40f, 40f));
```

Do not add a separate compact portrait test.

- [ ] **Step 9: Run Dialogue tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~DialogueScreenControllerTest
```

Expected: FAIL because `%NpcPortrait` / portrait controller behavior do not exist.

### 2C. Implement the identity row and single portrait render path

- [ ] **Step 10: Add `IdentityRow` to `DialogueScreen.tscn`**

Restructure `BodyHost`:

```text
BodyHost
├── IdentityRow
│   ├── NpcPortrait
│   └── DialogueCopy
│       ├── SpeakerLabel
│       └── DialogueText
└── ChoicesContainer
```

Scene properties:

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

Reparent existing `%SpeakerLabel` and `%DialogueText` under `DialogueCopy` without changing their theme/text/fit/scroll settings. Leave `%ChoicesContainer` directly under `BodyHost`.

Do **not** set `expand_mode` / `stretch_mode` in the scene; `UiIconPresenter.ApplyItem(...)` owns them.

- [ ] **Step 11: Bind portrait and local sizes in `DialogueScreenController`**

Add:

```csharp
private const float StandardPortraitSize = 64f;
private const float CompactPortraitSize = 40f;
private TextureRect _portrait = null!;
```

Bind in `_Ready()`:

```csharp
_portrait = GetNode<TextureRect>("%NpcPortrait");
```

In `RefreshLayout()` after compact calculation:

```csharp
var portraitSize = insets.Compact ? CompactPortraitSize : StandardPortraitSize;
_portrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
```

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

No `IsNodeReady()` guard: `RefreshPortrait()` is only called from `ShowNode(...)`, which runs after node binding.

- [ ] **Step 13: Wire exactly one portrait refresh call**

In `ShowNode(...)`:

```csharp
_currentNode = node;
_shell.Title = _npc?.DisplayName ?? string.Empty;
RefreshPortrait();
_speakerLabel.Text = node.SpeakerName ?? string.Empty;
```

Do not call `RefreshPortrait()` directly from `_Ready()` or `TryStartDialogue(...)`.

- [ ] **Step 14: Run focused tests and confirm GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter 'FullyQualifiedName~UiArtCatalogTest|FullyQualifiedName~DialogueScreenControllerTest'
```

Expected: PASS.

- [ ] **Step 15: Commit Task 2**

```bash
git add -- \
  scripts/ui/art/UiArtCatalog.cs \
  tests/ui/art/UiArtCatalogTest.cs \
  scenes/ui/DialogueScreen.tscn \
  scripts/ui/DialogueScreenController.cs \
  tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat: render Dialogue NPC portraits"
```

---

## Task 3: Run final regression and scope gates

**Files:** verification only unless a test exposes a real HPA-625 defect.

- [ ] **Step 1: Run all focused HPA-625 suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter 'FullyQualifiedName~NpcCatalogTest|FullyQualifiedName~UiArtCatalogTest|FullyQualifiedName~DialogueScreenControllerTest'
```

Expected: PASS.

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: PASS.

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

- [ ] **Step 6: Verify existing frame assets are tracked and no new portrait binary was added**

```bash
git ls-files --error-unmatch assets/sprites/npcs/shopkeeper/frames/frame1.png
git ls-files --error-unmatch assets/sprites/npcs/healer/frames/frame1.png

if git diff --name-only main...HEAD | grep -E '\.(png|jpg|jpeg|webp)$'; then
  echo 'HPA-625 should reuse tracked art and add no image binary'
  exit 1
fi
```

Expected: both tracked files resolve; no image files are in the branch diff.

- [ ] **Step 7: Verify `.gitignore` is intentionally unchanged**

```bash
git diff --exit-code main...HEAD -- .gitignore
```

Expected: exit 0.

Do not add the proposed file-only negation `!assets/sprites/npcs/*/frames/frame1.png`; an ignored `frames/` parent prevents that one-line rule from making new files trackable, and reopening the directory would broaden generated-frame staging behavior.

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

`UiIconPresenter.cs` is intentionally absent because HPA-625 reuses `ApplyItem(...)` without modifying it.

- [ ] **Step 9: Inspect the runtime diff**

Review `git diff main...HEAD --` and reject:

- portrait registry/service/cache/presenter additions;
- `SpriteType` portrait inference;
- host/domain/dialogue-tree changes;
- new art files;
- second NPC-name label;
- duplicate `_Ready()` / `TryStartDialogue(...)` portrait refresh call sites;
- local hand-rolled `ResourceLoader.Exists(...)` portrait loading instead of the existing optional-art helper;
- numeric `TextureRect` expand/stretch configuration duplicated in the scene.

- [ ] **Step 10: Commit only if verification required a real fix**

If no fix was required, do not create an empty verification commit.

- [ ] **Step 11: Update the existing PR #43**

Keep this branch/PR as the only HPA-625 PR. Update the PR body with actual runtime files and fresh test/build results after implementation.

---

## Post-merge Linear closeout

After PR #43 merges:

1. re-fetch HPA-625;
2. add one concise comment with merged PR and shipped scope;
3. mark HPA-625 Done;
4. do not create a portrait-framework follow-up merely for generalized reuse.
