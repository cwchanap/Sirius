# HPA-625 Dialogue NPC Portraits Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit optional NPC portrait data and render the currently shipped shopkeeper/healer portraits in the hosted Dialogue screen with clean missing/invalid fallbacks.

**Architecture:** Extend the existing `NpcData` catalog contract with one nullable `PortraitPath`; do not derive portrait identity from `SpriteType`. `DialogueScreenController` consumes that path directly, validates it with `ResourceLoader.Exists(...)`, loads it into one authored `TextureRect`, and keeps the existing Dialogue host/domain/lifecycle untouched. Reuse existing tracked NPC frame assets; no portrait registry, service, shared loader, or new art pipeline.

**Tech Stack:** Godot 4.6, C#, GdUnit4, existing Sirius Theme / `SiriusModalShell` / `DialogueScreen`.

**Spec:** `docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md`

## Global Constraints

- HPA-625 uses one branch and one PR. Continue implementation on `jack65786656/hpa-625-add-sirius-npc-portrait-data-and-dialogue-identity`; do not open a second implementation PR.
- Portrait presentation reads only `NpcData.PortraitPath`. `NpcData.SpriteType` remains world-sprite metadata and must not become a Dialogue dependency.
- `village_shopkeeper` maps to `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`.
- `village_healer` maps to `res://assets/sprites/npcs/healer/frames/frame1.png`.
- `old_farmer` and `village_blacksmith` remain without `PortraitPath` until authored/shipped content needs them.
- Standard Dialogue portrait size is 64×64 logical pixels; compact size is 40×40.
- Production Dialogue start order is configure-before-attach: `NpcInteractionController.Begin()` calls `TryStartDialogue(...)` on an unparented screen before `UIScreenHost` attaches it. At least one authored-portrait test must exercise that exact order.
- Missing portrait data is a valid silent fallback.
- A configured nonexistent path must be checked with `ResourceLoader.Exists(...)` before `ResourceLoader.Load(...)`, emit one explicit Dialogue warning, and fall back to the no-portrait layout without a missing-resource load attempt.
- Reuse existing tracked frame PNGs. Do not add generated portrait binaries, an asset pipeline, portrait registry/service/presenter/cache, shared asset loader, or new theme/metrics tokens.
- The repository ignores directories named `frames/`; HPA-625 changes no ignore rules because its two mapped frame PNGs are already tracked. Future new portrait files must handle that in their own ticket.
- Keep the existing shell title, `DialogueNode.SpeakerName`, text, choices, focus, terminal latch, host policy, Shop/Heal handoff, and NPC interaction lifecycle unchanged.
- Do not modify `NpcInteractionController`, `UIScreenHost`, `SiriusModalShell`, `NpcSpawn`, dialogue trees, or dialogue-domain behavior for this ticket.

---

### Task 1: Add the explicit NPC portrait data contract

**Files:**
- Modify: `scripts/data/npc/NpcData.cs`
- Modify: `scripts/data/npc/NpcCatalog.cs`
- Test: `tests/data/npc/NpcCatalogTest.cs`

**Interfaces:**
- Consumes: existing `NpcData`, `NpcCatalog`, and Godot `ResourceLoader`.
- Produces: `NpcData.PortraitPath : string?`; explicit shopkeeper/healer portrait mappings for Task 2.

- [ ] **Step 1: Write the failing catalog tests**

Add `using Godot;` to `tests/data/npc/NpcCatalogTest.cs`, then add:

```csharp
[TestCase]
public void NpcCatalog_ShippedDialoguePortraits_AreExplicitAndLoadable()
{
    var shopkeeper = NpcCatalog.GetById("village_shopkeeper")!;
    var healer = NpcCatalog.GetById("village_healer")!;

    AssertThat(shopkeeper.PortraitPath)
        .IsEqual("res://assets/sprites/npcs/shopkeeper/frames/frame1.png");
    AssertThat(healer.PortraitPath)
        .IsEqual("res://assets/sprites/npcs/healer/frames/frame1.png");

    AssertThat(ResourceLoader.Exists(shopkeeper.PortraitPath!)).IsTrue();
    AssertThat(ResourceLoader.Exists(healer.PortraitPath!)).IsTrue();
    AssertThat(ResourceLoader.Load<Texture2D>(shopkeeper.PortraitPath!)).IsNotNull();
    AssertThat(ResourceLoader.Load<Texture2D>(healer.PortraitPath!)).IsNotNull();
}

[TestCase]
public void NpcCatalog_UnauthoredPortrait_RemainsOptional()
{
    var farmer = NpcCatalog.GetById("old_farmer")!;
    var blacksmith = NpcCatalog.GetById("village_blacksmith")!;

    AssertThat(farmer.PortraitPath).IsNull();
    AssertThat(blacksmith.PortraitPath).IsNull();
}
```

Do not derive the expected portrait path from `SpriteType`; the literal catalog mapping is the contract.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~NpcCatalogTest
```

Expected: FAIL to compile because `NpcData` does not yet expose `PortraitPath`.

- [ ] **Step 3: Add `PortraitPath` to `NpcData`**

Add next to the existing identity/dialogue fields:

```csharp
/// <summary>
/// Optional player-facing portrait resource used by Dialogue identity presentation.
/// Independent from SpriteType, which remains world-sprite metadata.
/// </summary>
public string? PortraitPath { get; init; }
```

Keep the existing `SpriteType` property and its world-sprite comment intact.

- [ ] **Step 4: Author only the two current shipped mappings**

Update `NpcCatalog`:

```csharp
private static NpcData CreateVillageShopkeeper() => new NpcData
{
    NpcId = "village_shopkeeper",
    DisplayName = "Mira the Merchant",
    NpcType = NpcType.Shopkeeper,
    ShopId = "village_general_store",
    DialogueTreeId = "shopkeeper_greeting",
    PortraitPath = "res://assets/sprites/npcs/shopkeeper/frames/frame1.png",
    SpriteType = "shopkeeper"
};

private static NpcData CreateVillageHealer() => new NpcData
{
    NpcId = "village_healer",
    DisplayName = "Brother Aldric",
    NpcType = NpcType.Healer,
    HealCost = 50,
    DialogueTreeId = "healer_greeting",
    PortraitPath = "res://assets/sprites/npcs/healer/frames/frame1.png",
    SpriteType = "healer"
};
```

Leave `CreateVillager()` and `CreateBlacksmith()` without `PortraitPath`.

- [ ] **Step 5: Run the focused data test and verify GREEN**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~NpcCatalogTest
```

Expected: PASS, including existence and texture-load assertions.

- [ ] **Step 6: Commit the data contract**

```bash
git add -- scripts/data/npc/NpcData.cs scripts/data/npc/NpcCatalog.cs tests/data/npc/NpcCatalogTest.cs
git commit -m "feat: add explicit NPC portrait data"
```

---

### Task 2: Render portraits through the existing Dialogue lifecycle

**Files:**
- Modify: `scenes/ui/DialogueScreen.tscn`
- Modify: `scripts/ui/DialogueScreenController.cs`
- Test: `tests/ui/DialogueScreenControllerTest.cs`

**Interfaces:**
- Consumes: `NpcData.PortraitPath : string?` from Task 1; existing `TryStartDialogue(NpcData, DialogueTree, Character, HashSet<string>)`; existing `SiriusUiMetrics.SafeFrameInsets(...)` compact decision.
- Produces: authored `%NpcPortrait : TextureRect`; private Exists-then-Load portrait fallback; 64 px standard / 40 px compact presentation.

- [ ] **Step 1: Write the failing production-order portrait test**

The authored-portrait regression must configure before attach, matching `NpcInteractionController.Begin()`:

```csharp
[TestCase]
public async Task AuthoredPortrait_BeforeReadyStart_RendersAfterAttach()
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
        AssertThat(portrait.CustomMinimumSize).IsEqual(new Vector2(64f, 64f));
        AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
            .IsEqual(npc.DisplayName);
        AssertThat(screen.GetNode<Label>("%SpeakerLabel").Text)
            .IsEqual(tree.Root!.SpeakerName);
        AssertThat(screen.GetNode<RichTextLabel>("%DialogueText").Text)
            .IsEqual(tree.Root.Text);
        AssertThat(TestHelpers.FindButtonOrNull(screen, "Browse your wares.")).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

This test must fail if `RefreshPortrait()` is wired only into the already-ready branch of `TryStartDialogue(...)` and omitted from `_Ready()`.

- [ ] **Step 2: Write the failing optional and invalid-path fallback tests**

Missing optional portrait:

```csharp
[TestCase]
public async Task MissingPortrait_HidesPortraitAndKeepsDialogueUsable()
{
    var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
    try
    {
        var screen = fixture.Screen;
        var tree = DialogueCatalog.GetById("villager_01")!;

        AssertThat(screen.TryStartDialogue(
            NpcCatalog.GetById("old_farmer")!,
            tree,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>())).IsTrue();
        await AwaitFrames(2);

        var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
        AssertThat(portrait.Visible).IsFalse();
        AssertThat(portrait.Texture).IsNull();
        AssertThat(screen.GetNode<RichTextLabel>("%DialogueText").Text)
            .IsEqual(tree.Root!.Text);
        AssertThat(TestHelpers.FindButtonOrNull(screen, "I'm sorry to hear that.")).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

Explicit bad path:

```csharp
[TestCase]
public async Task InvalidPortraitPath_HidesPortraitAndKeepsDialogueUsable()
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
        AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
            .IsEqual(npc.DisplayName);
        AssertThat(screen.GetNode<RichTextLabel>("%DialogueText").Text)
            .IsEqual(tree.Root!.Text);
        AssertThat(TestHelpers.FindButtonOrNull(screen, "Browse your wares.")).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

Do not add production seams solely to capture warning count. The implementation below guarantees the missing-path branch emits one explicit `GD.PushWarning(...)` and returns before `ResourceLoader.Load(...)`; this runtime test pins the visible fallback.

- [ ] **Step 3: Write the failing compact portrait regression**

```csharp
[TestCase]
public async Task CompactDialogue_ReducesPortraitBeforeEssentialContent()
{
    var fixture = await InstantiateDialogue(new Vector2I(640, 360));
    try
    {
        var screen = fixture.Screen;
        var tree = DialogueCatalog.GetById("shopkeeper_greeting")!;

        AssertThat(screen.TryStartDialogue(
            NpcCatalog.GetById("village_shopkeeper")!,
            tree,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>())).IsTrue();
        await AwaitFrames(2);

        var portrait = screen.GetNode<TextureRect>("%NpcPortrait");
        AssertThat(portrait.Visible).IsTrue();
        AssertThat(portrait.Texture).IsNotNull();
        AssertThat(portrait.CustomMinimumSize).IsEqual(new Vector2(40f, 40f));
        AssertThat(screen.GetNode<RichTextLabel>("%DialogueText").Text)
            .IsEqual(tree.Root!.Text);
        AssertThat(TestHelpers.FindButtonOrNull(screen, "Browse your wares.")).IsNotNull();
    }
    finally
    {
        await FreeAsync(fixture);
    }
}
```

- [ ] **Step 4: Run the Dialogue suite and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~DialogueScreenControllerTest
```

Expected: FAIL because `%NpcPortrait` and portrait loading do not exist yet.

- [ ] **Step 5: Author the portrait/copy identity row in `DialogueScreen.tscn`**

Replace the current direct `SpeakerLabel` + `DialogueText` children under `BodyHost` with:

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
expand_mode = 1
stretch_mode = 5
mouse_filter = 2

[node name="DialogueCopy" type="VBoxContainer" parent="SafeFrame/ModalShell/Panel/Margin/RootLayout/BodyScroll/BodyHost/IdentityRow"]
layout_mode = 2
size_flags_horizontal = 3
theme_override_constants/separation = 4
```

Reparent existing `%SpeakerLabel` and `%DialogueText` under `DialogueCopy` without changing their text/theme/scroll behavior. Leave `%ChoicesContainer` directly under `BodyHost` with its existing settings.

Do not add a second NPC-name label; `SiriusModalShell.Title` remains the NPC name.

- [ ] **Step 6: Bind and load the explicit portrait in `DialogueScreenController`**

Add:

```csharp
private const float StandardDialogueHeightFraction = 0.45f;
private const float StandardPortraitSize = 64f;
private const float CompactPortraitSize = 40f;

private TextureRect _portrait = null!;
```

Bind in `_Ready()`:

```csharp
_portrait = GetNode<TextureRect>("%NpcPortrait");
```

Add the private loader/fallback exactly at the Dialogue consumer; do not extract a helper:

```csharp
private void RefreshPortrait()
{
    if (!IsNodeReady())
        return;

    _portrait.Texture = null;
    _portrait.Visible = false;

    var portraitPath = _npc?.PortraitPath;
    if (string.IsNullOrWhiteSpace(portraitPath))
        return;

    if (!ResourceLoader.Exists(portraitPath))
    {
        GD.PushWarning(
            $"[DialogueScreen] NPC '{_npc?.NpcId}' portrait '{portraitPath}' was not found.");
        return;
    }

    var texture = ResourceLoader.Load<Texture2D>(portraitPath);
    if (texture == null)
    {
        GD.PushWarning(
            $"[DialogueScreen] NPC '{_npc?.NpcId}' portrait '{portraitPath}' could not be loaded.");
        return;
    }

    _portrait.Texture = texture;
    _portrait.Visible = true;
}
```

The `Exists` check is required before `Load`; do not replace it with a bare `GD.Load<Texture2D>(portraitPath)`.

Update both start orders. Keep the existing validation/latching order in `TryStartDialogue(...)` and use:

```csharp
if (IsNodeReady())
{
    RefreshPortrait();
    ShowNode(root);
}
```

In `_Ready()`, after node binding:

```csharp
Resized += OnResized;
RefreshPortrait();
RefreshLayout();

if (_currentNode != null)
    ShowNode(_currentNode);
```

`RefreshPortrait()` in `_Ready()` is load-bearing for production; do not remove it because mounted-screen tests also cover the already-ready branch.

Do not move `_started = true` earlier, emit new signals, add a post-host-start protocol, or touch `NpcInteractionController`.

- [ ] **Step 7: Apply the local responsive portrait size in `RefreshLayout()`**

After computing `insets`, set:

```csharp
var portraitSize = insets.Compact
    ? CompactPortraitSize
    : StandardPortraitSize;
_portrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
```

Do not change the existing 45% standard lower-band, compact full-safe-height, shell size class, text theme variations, or action target sizes.

- [ ] **Step 8: Run the Dialogue suite and verify GREEN**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter FullyQualifiedName~DialogueScreenControllerTest
```

Expected: PASS for the four new portrait regressions and all existing Dialogue lifecycle/layout/focus tests.

- [ ] **Step 9: Run the data + Dialogue regression set**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~NpcCatalogTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest"
```

Expected: PASS. `NpcInteractionControllerTest` remains regression-only; no production interaction code should change.

- [ ] **Step 10: Commit the Dialogue portrait presentation**

```bash
git add -- scenes/ui/DialogueScreen.tscn scripts/ui/DialogueScreenController.cs tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat: show NPC portraits in dialogue"
```

---

### Task 3: Close HPA-625 with scope and regression verification

**Files:**
- Modify after implementation passes: `docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md`
- Verify only: Task 1/2 production and test files

**Interfaces:**
- Consumes: completed `PortraitPath` contract and Dialogue portrait presentation from Tasks 1–2.
- Produces: implementation-status documentation and evidence that HPA-625 stayed within its single-PR scope.

- [ ] **Step 1: Mark the design implemented only after focused tests are green**

Immediately below the design title, add:

```markdown
**Status:** Implemented in HPA-625.
```

Do not claim HPA-359 or HPA-541 completion.

- [ ] **Step 2: Verify the Dialogue implementation did not couple to `SpriteType`**

Run:

```bash
rg -n 'SpriteType' scripts/ui/DialogueScreenController.cs scenes/ui/DialogueScreen.tscn
```

Expected: no matches.

Then verify the explicit portrait contract stays in intended paths:

```bash
rg -n 'PortraitPath|NpcPortrait' scripts/data/npc scripts/ui/DialogueScreenController.cs scenes/ui/DialogueScreen.tscn tests/data/npc tests/ui/DialogueScreenControllerTest.cs
```

Expected: matches only in the HPA-625 contract, mappings, Dialogue binding, and focused tests.

- [ ] **Step 3: Verify the missing-path loader guard is still present**

Run:

```bash
rg -n 'ResourceLoader\.Exists\(portraitPath\)|ResourceLoader\.Load<Texture2D>\(portraitPath\)' scripts/ui/DialogueScreenController.cs
```

Expected: both calls exist, with `Exists(...)` guarding the `Load(...)` path in `RefreshPortrait()`.

- [ ] **Step 4: Verify no unnecessary asset-ignore change entered the PR**

Run:

```bash
git diff --name-only main...HEAD -- .gitignore assets/sprites/npcs
```

Expected after implementation: no `.gitignore` change and no new portrait binary. Existing tracked `frame1.png` resources are reused as-is.

Do not add an ignore exception just to make HPA-625 look future-proof. A future ticket that adds a new portrait asset owns its exact tracked path/un-ignore decision.

- [ ] **Step 5: Verify no out-of-scope infrastructure changed**

Run:

```bash
git diff --name-only main...HEAD
```

Expected implementation scope after the planning documents:

```text
docs/superpowers/plans/2026-08-19-hpa-625-dialogue-npc-portraits.md
docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md
scenes/ui/DialogueScreen.tscn
scripts/data/npc/NpcCatalog.cs
scripts/data/npc/NpcData.cs
scripts/ui/DialogueScreenController.cs
tests/data/npc/NpcCatalogTest.cs
tests/ui/DialogueScreenControllerTest.cs
```

Do not “fix” unrelated files during this audit. If the diff contains unrelated paths, remove those changes rather than expanding the ticket.

- [ ] **Step 6: Build the solution**

Run:

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: 0 errors.

- [ ] **Step 7: Run the full test suite**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: all tests pass. Existing environment-only warning noise may remain, but no new normal-flow Dialogue missing-resource error/warning is acceptable for valid catalog mappings.

- [ ] **Step 8: Check whitespace and patch integrity**

Run:

```bash
git diff --check main...HEAD
```

Expected: no output.

- [ ] **Step 9: Commit the implementation closeout**

```bash
git add -- docs/superpowers/specs/2026-08-19-hpa-625-dialogue-npc-portraits-design.md
git commit -m "docs: close HPA-625 dialogue portraits"
```

After this commit, keep using the existing HPA-625 draft PR. When implementation is reviewed and merged, mark HPA-625 Done, then evaluate HPA-358’s workstream acceptance checklist. HPA-541 remains optional/nonblocking; do not pull it into this PR.
