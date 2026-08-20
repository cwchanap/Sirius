# HPA-625 Sirius NPC Portrait and Dialogue Identity Design

## Goal

Complete the deferred Dialogue identity treatment with one explicit optional NPC portrait path and render it in the existing hosted `DialogueScreen`.

This remains a small data/presentation slice. It does not change dialogue trees, NPC interaction sequencing, `UIScreenHost`, world-sprite loading, or character-art infrastructure.

HPA-625 stays on one branch and one PR: this design/plan and the later runtime implementation all remain in PR #43.

## Current state

- `NpcData` owns NPC identity metadata (`DisplayName`, `DialogueTreeId`, `SpriteType`) but no portrait path.
- `SpriteType` is world-sprite metadata consumed by NPC spawning and must remain unrelated to Dialogue portrait identity.
- `DialogueScreenController.TryStartDialogue(...)` already receives the complete `NpcData`; no new handoff is needed.
- Production configures Dialogue before attachment: `NpcInteractionController.Begin()` calls `TryStartDialogue(...)` while the screen is unparented, then `UIScreenHost` attaches it.
- `ShowNode(...)` is already the single render path used by both start orders: the ready branch of `TryStartDialogue(...)`, `_Ready()` when a stored node exists, and later dialogue-node transitions. It already applies `_shell.Title = _npc?.DisplayName`.
- The Ground Floor currently ships `village_shopkeeper` and `village_healer`; both already have tracked `frames/frame1.png` assets.
- Missing portrait data must remain a valid silent fallback.
- `UiIconPresenter.ApplyItem(TextureRect, Texture2D?)` already applies `IgnoreSize` + `KeepAspectCentered` for externally sourced textures.
- `UiArtCatalog.LoadOnce<T>` already owns optional-resource `Exists -> Load -> warn-once` behavior. Adding one narrow public texture delegation reuses that policy without adding a portrait-specific loader/service.

## Selected design

### 1. Add one optional `NpcData.PortraitPath`

Add:

```csharp
/// <summary>
/// Optional player-facing portrait resource used by Dialogue identity presentation.
/// Independent from SpriteType, which remains world-sprite metadata.
/// </summary>
public string? PortraitPath { get; init; }
```

Absence means “no authored portrait.” It is not an error and must not trigger inference from `NpcId` or `SpriteType`.

No `PortraitId`, portrait registry, service, cache, or new resource type.

### 2. Author only the two shipped portrait mappings

`NpcCatalog` explicitly maps:

- `village_shopkeeper` -> `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`
- `village_healer` -> `res://assets/sprites/npcs/healer/frames/frame1.png`

`old_farmer` and `village_blacksmith` remain unauthored for now. Tests must prove optionality without pinning those two identities to permanent null values.

Catalog integrity is content-oriented rather than literal-oriented:

- enumerate `NpcCatalog.AllNpcs`;
- for every non-null `PortraitPath`, assert `ResourceLoader.Exists(...)` and `ResourceLoader.Load<Texture2D>(...)` succeed;
- assert the two currently shipped NPCs have non-null portrait paths;
- assert at least one NPC has no portrait, proving the field remains optional.

Adding a future portrait should normally require only a catalog edit, not a test edit.

### 3. Reuse the existing Dialogue scene and texture presenter

Restructure only the shell body:

```text
BodyHost
├── IdentityRow (HBoxContainer)
│   ├── NpcPortrait (%NpcPortrait, TextureRect)
│   └── DialogueCopy (VBoxContainer)
│       ├── SpeakerLabel
│       └── DialogueText
└── ChoicesContainer
```

`NpcPortrait` starts hidden, ignores mouse input, and has a 64x64 initial minimum size. Do not hardcode numeric `expand_mode` / `stretch_mode` values in the `.tscn`; controller presentation calls:

```csharp
UiIconPresenter.ApplyItem(_portrait, texture);
```

That existing helper owns:

- `Texture = texture`;
- `ExpandMode = IgnoreSize`;
- `StretchMode = KeepAspectCentered`.

The shell title remains the NPC name. Do not add a second NPC-name label.

Choices remain below the identity row so portrait width never compresses action buttons.

### 4. Reuse `UiArtCatalog` optional-resource loading

Expose one narrow delegation:

```csharp
public static Texture2D? LoadContentTexture(string path) =>
    LoadOnce<Texture2D>(path);
```

This deliberately broadens `UiArtCatalog` only enough for optional textures shown in UI. It avoids duplicating an existing `ResourceLoader.Exists(...)` / load / warn-once policy and gives invalid portrait paths the same missing-resource dedupe already used by UI art.

This is not a portrait registry or asset service: the caller still supplies the explicit `NpcData.PortraitPath`, and no path derivation or caching layer is added.

Add one focused `UiArtCatalogTest` case proving `LoadContentTexture(...)` uses the existing missing-path dedupe behavior.

### 5. Refresh portrait from the single render path

Bind `_portrait` in `_Ready()`, then call `RefreshPortrait()` only from `ShowNode(...)`, immediately beside NPC title assignment:

```csharp
private void ShowNode(DialogueNode node)
{
    _currentNode = node;
    _shell.Title = _npc?.DisplayName ?? string.Empty;
    RefreshPortrait();
    // existing speaker/text/choice rendering continues
}
```

`ShowNode(...)` is never reached before the scene is ready, so `RefreshPortrait()` needs no `IsNodeReady()` guard and no separate `_Ready()` / ready-branch call sites.

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

Effects:

- absent path -> silent hidden portrait;
- valid path -> visible portrait;
- bad path -> one deduplicated optional-art warning from `UiArtCatalog`, hidden portrait;
- node changes -> harmless Godot resource-cache lookup, with no call-site drift.

No placeholder or `SpriteType` fallback.

### 6. Keep sizes local to Dialogue

Add local constants:

```csharp
private const float StandardPortraitSize = 64f;
private const float CompactPortraitSize = 40f;
```

`RefreshLayout()` sets the portrait minimum size from the existing compact decision.

Do not add a `SiriusUiMetrics` portrait token. Existing portrait surfaces already use different local sizes; there is no stable shared metric to extract.

### 7. Keep `frames/` ignore policy unchanged

The review suggested:

```gitignore
!assets/sprites/npcs/*/frames/frame1.png
```

Do not add that line. `frames/` excludes the parent directory, so a file-only negation does not make a newly created nested `frame1.png` trackable. Correctly reopening the directory would require additional patterns, and the existing NPC frame folders contain `frame2.png`, `frame3.png`, and `frame4.png` as generated/derived frame content.

HPA-625 therefore keeps `.gitignore` unchanged:

- the two reused `frame1.png` files are already tracked and remain valid runtime dependencies;
- future dedicated portrait authoring should use a tracked `assets/sprites/npcs/<npc>/portrait.png` outside ignored `frames/` unless that future task intentionally redesigns the frame-output ignore policy.

This is an explicit convention, not a deferred unknown: current tracked frame reuse is a compatibility exception; new portrait content uses dedicated `portrait.png`.

## Data and presentation flow

1. `NpcSpawn` resolves `NpcData`; `SpriteType` continues to drive only world-sprite loading.
2. `NpcInteractionController` configures an unparented `DialogueScreenController`.
3. `TryStartDialogue(...)` stores the NPC/tree/current node and returns because the screen is not ready.
4. Attachment runs `_Ready()`, binds `%NpcPortrait`, then calls `ShowNode(_currentNode)`.
5. `ShowNode(...)` applies NPC title + portrait through the one render path.
6. Later dialogue-node transitions call `ShowNode(...)` again and therefore keep identity presentation coherent without a second lifecycle branch.
7. Missing or invalid portrait data leaves the row collapsed around the existing dialogue copy; choices, focus, outcomes, and host behavior remain unchanged.

## Testing strategy

### Data contract

One catalog integrity test:

- every authored portrait path across `NpcCatalog.AllNpcs` exists and loads as `Texture2D`;
- shopkeeper and healer are currently authored;
- at least one catalog NPC remains unauthored;
- no test derives portrait identity from `SpriteType`;
- no test permanently asserts farmer/blacksmith must remain null.

### Optional loader reuse

One `UiArtCatalogTest` case calls `LoadContentTexture(...)` twice with a synthetic missing path under the existing `ResourceExists` test seam and verifies `MissingPaths` records it once.

### Dialogue presentation

Add only three new focused Dialogue cases:

1. **Authored portrait + production start order:** configure shopkeeper before attachment, mount, then assert portrait visible/non-null and shell title is the NPC display name. Existing `TryStartDialogue_BeforeReady_RendersAfterAttach` already owns speaker/body/choice/focus ordering coverage; do not duplicate those assertions.
2. **Missing optional portrait:** a portrait-less catalog NPC renders hidden/null portrait and retains a usable initial focus target.
3. **Invalid explicit path:** synthetic NPC with nonexistent `PortraitPath` renders hidden/null portrait and retains a usable initial focus target.

Reuse existing geometry tests instead of adding separate portrait geometry tests:

- switch `StandardDialogue_StaysWithinLowerBand` to a portrait-bearing shopkeeper and assert visible 64x64 portrait while retaining all existing lower-band containment assertions;
- switch `CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice` to a portrait-bearing shopkeeper and assert visible 40x40 portrait while retaining the existing full-safe-height and scroll-to-focused-choice assertions.

This gives portrait-bearing coverage on the exact `BodyHost` geometry paths changed by the identity row without adding redundant tests.

## Review disposition

Accepted from the latest review:

- one `ShowNode(...)` portrait refresh call site rather than `_Ready()` + ready-branch duplication;
- enumeration-based catalog integrity rather than four content literals/null pins;
- reuse `UiIconPresenter.ApplyItem(...)`;
- reuse `UiArtCatalog.LoadOnce<T>` through one narrow `LoadContentTexture(...)` delegation;
- make the existing standard and compact geometry tests portrait-bearing;
- reduce the new before-attach test to portrait-specific assertions because general configure-before-attach behavior is already covered.

Declined:

- the proposed one-line `.gitignore` un-ignore. Git does not re-include a file while its parent `frames/` directory is still ignored, and reopening that directory would also expose generated sibling frames unless we add a more complex ignore block. HPA-625 keeps generated-frame policy stable instead.

## Risks and mitigations

### Start-order drift

Mitigation: `ShowNode(...)` is the single identity render point reached from both start orders, and the authored-portrait test configures before attachment.

### Broken portrait path spams warnings

Mitigation: `UiArtCatalog.LoadContentTexture(...)` delegates to existing `LoadOnce<T>`, which checks existence before load and deduplicates `MissingPaths` warnings.

### Portrait changes Dialogue geometry

Mitigation: the existing standard lower-band and compact scroll tests run with a portrait-bearing NPC and pin 64/40 sizes in the same tests.

### Accidental world-sprite coupling

Mitigation: Dialogue reads only `PortraitPath`; final grep rejects new `SpriteType` references in Dialogue portrait code.

### Future generated frame output becomes runtime-authoring convention

Mitigation: `.gitignore` remains unchanged and future new portraits use dedicated `portrait.png`; HPA-625 only reuses already-tracked historical frames.

## Out of scope

- New portrait art generation or art pipeline
- Portrait animation / expressions / lip sync / voice
- Portraits for every catalog NPC solely to remove nulls
- Portrait registry/service/cache/resource database
- New theme metrics
- Reinterpreting `SpriteType`
- Dialogue-domain, Shop/Heal, host, focus-policy, or NPC-interaction lifecycle changes
- Broad `frames/` ignore-policy redesign
- HPA-541 Reduced Motion or HPA-359 hardening

## Acceptance mapping

- Explicit authored portrait independent from `SpriteType`: `NpcData.PortraitPath` + catalog mappings.
- Both Dialogue startup orders: `ShowNode(...)` is the single portrait call site; before-attach authored test exercises production order.
- Missing/invalid portrait: hidden/null clean fallback.
- Existing optional-resource policy reused: `UiArtCatalog.LoadContentTexture(...)` + `UiIconPresenter.ApplyItem(...)`.
- Standard/compact layout: existing geometry tests become portrait-bearing and pin 64/40 sizes.
- Current shipped content: shopkeeper/healer reuse tracked `frame1.png`; no new binaries.
