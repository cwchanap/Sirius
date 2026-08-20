# HPA-625 Sirius NPC Portrait and Dialogue Identity Design

## Goal

Complete the deferred Dialogue identity treatment with one explicit optional NPC portrait path and render it in the existing hosted `DialogueScreen`.

This remains a small data/presentation slice. It does not change dialogue trees, NPC interaction sequencing, `UIScreenHost`, world-sprite loading, or character-art infrastructure.

HPA-625 stays on one branch and one PR: this design/plan and the later runtime implementation all remain in PR #43.

## Current state

- `NpcData` owns NPC identity metadata (`DisplayName`, `DialogueTreeId`, `SpriteType`) but no portrait path.
- `SpriteType` is world-sprite metadata consumed by `NpcSpawn`; Dialogue must not derive portrait identity from it.
- `DialogueScreenController.TryStartDialogue(...)` already receives the complete `NpcData`; no new handoff is needed.
- Production configures Dialogue before attachment: `NpcInteractionController.Begin()` calls `TryStartDialogue(...)` on the unparented screen, then `UIScreenHost` attaches it.
- `ShowNode(...)` is the one existing render path reached by the already-ready start branch, `_Ready()` for a stored node, and later dialogue-node transitions.
- Inventory and Exploration HUD already use the desired portrait/text row shape: a `TextureRect` beside an expanding text column.
- Inventory and HUD also crop a fixed hero sprite sheet through scene-authored `AtlasTexture` resources. That is useful layout precedent, but it is not a generic data contract for NPC portrait paths.
- Ground Floor currently ships `village_shopkeeper` and `village_healer`; both already have tracked ready-to-display `frames/frame1.png` textures.
- `UiIconPresenter.ApplyItem(TextureRect, Texture2D?)` already owns `IgnoreSize` + `KeepAspectCentered` presentation.
- `UiArtCatalog.LoadOnce<T>` already owns optional-resource existence checks and deduplicated warnings, but its generic `ResourceLoader.Load<T>` assumes the existing path resolves to the requested type. HPA-625 introduces authored raw paths, so that helper must become wrong-type-safe.
- Missing portrait data must remain a valid silent fallback.

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

`PortraitPath` means a directly displayable portrait `Texture2D` resource. Absence means “no authored portrait.” It is not an error and must not trigger inference from `NpcId`, `SpriteType`, folder naming, or sheet layout.

No `PortraitId`, portrait registry, service, cache, crop metadata, or new resource type.

### 2. Author only the two shipped portrait mappings

`NpcCatalog` explicitly maps:

- `village_shopkeeper` -> `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`
- `village_healer` -> `res://assets/sprites/npcs/healer/frames/frame1.png`

Those files are already tracked and are complete `Texture2D` resources. Using them keeps `PortraitPath` independent of sprite-sheet dimensions and means a later dedicated portrait remains a catalog-path change.

Do **not** change these mappings to the world `sprite_sheet.png` and reconstruct frame-zero semantics in Dialogue. Inventory/HUD can use scene-authored `AtlasTexture` because they each have one fixed hero sheet. A data-driven NPC portrait contract should not silently mean “first quarter of a four-frame horizontal sheet.”

`old_farmer` and `village_blacksmith` remain unauthored for now. Tests must not require the catalog to stay incomplete forever.

Catalog integrity is content-oriented:

- for every non-null `PortraitPath`, assert the resource exists and loads as `Texture2D`;
- assert the two currently shipped NPCs have non-null portrait paths;
- prove optionality on a synthetic `NpcData` with no `PortraitPath`, not by counting nulls in the current catalog.

Adding a future portrait should normally require only a catalog edit plus a tracked ready-to-display texture resource.

### 3. Reuse the existing Dialogue scene and identity-row pattern

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

This mirrors the shipped Inventory/HUD portrait + expanding text-column composition without extracting a component.

`NpcPortrait` starts hidden, ignores mouse input, and has an initial 64x64 minimum size. Controller presentation calls:

```csharp
UiIconPresenter.ApplyItem(_portrait, texture);
```

That existing helper owns:

- `Texture = texture`;
- `ExpandMode = IgnoreSize`;
- `StretchMode = KeepAspectCentered`.

The shell title remains the NPC name. Do not add a second NPC-name label. Choices remain the final direct child of `BodyHost`, below `IdentityRow`, so portrait width cannot compress action buttons.

### 4. Reuse and harden `UiArtCatalog` optional-resource loading

Expose one narrow raw-texture delegation:

```csharp
public static Texture2D? LoadContentTexture(string path) =>
    LoadOnce<Texture2D>(path);
```

Because this method accepts an authored string, `LoadOnce<T>` must no longer rely on `ResourceLoader.Load<T>(path)` after existence alone. An existing non-texture resource is valid as a Godot resource path but invalid portrait data.

Harden the shared helper:

```csharp
private static T? LoadOnce<T>(string path) where T : Resource
{
    if (ResourceExists(path))
    {
        var resource = ResourceLoader.Load<Resource>(path);
        if (resource is T typed)
            return typed;

        WarnOnce(path, $"[UiArtCatalog] Optional UI art resource has unexpected type: {path}");
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

`WarnOnce` is private and only removes duplication inside `UiArtCatalog`; do not create a general resource loader.

This preserves current icon/ornament/effect behavior and adds one new guarantee: an authored path that resolves to the wrong resource type warns once and returns `null` instead of throwing from the middle of Dialogue rendering.

Focused `UiArtCatalogTest` cases must cover both:

- nonexistent path -> `null`, deduplicated `MissingPaths` entry;
- existing non-texture path such as `res://scenes/ui/DialogueScreen.tscn` -> `null`, deduplicated `MissingPaths` entry.

### 5. Refresh portrait from the single render path

Bind `_portrait` in `_Ready()` before the first `RefreshLayout()` / `ShowNode(...)` call, then call `RefreshPortrait()` only from `ShowNode(...)` immediately beside NPC title assignment:

```csharp
private void ShowNode(DialogueNode node)
{
    _currentNode = node;
    _shell.Title = _npc?.DisplayName ?? string.Empty;
    RefreshPortrait();
    // existing speaker/text/choice rendering continues
}
```

`ShowNode(...)` is not reached until `_Ready()` has bound authored nodes, so `RefreshPortrait()` needs no `IsNodeReady()` guard and no duplicate `_Ready()` / ready-branch call sites.

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
- valid texture path -> visible portrait;
- missing path -> one deduplicated warning, hidden portrait;
- existing wrong-type path -> one deduplicated warning, hidden portrait;
- dialogue-node transitions reuse the same identity render path.

No placeholder or `SpriteType` fallback.

### 6. Keep sizes local to Dialogue

Add local constants:

```csharp
private const float StandardPortraitSize = 64f;
private const float CompactPortraitSize = 40f;
```

`RefreshLayout()` sets the portrait minimum size from the existing compact decision. Do not add a `SiriusUiMetrics` portrait token: existing portrait surfaces already use different local sizes.

Tests should verify layout outcomes rather than echo these assignments back as `CustomMinimumSize == 64/40`.

### 7. Do not define a new asset-storage convention in this ticket

`.gitignore` contains a bare `frames/` rule. The two reused `frame1.png` files are already tracked, so HPA-625 needs no ignore change and adds no image binary.

Do not turn that historical fact into a new durable `portrait.png` convention, and do not redesign `frames/` ignore policy here. Future portrait work only needs to satisfy the `PortraitPath` contract: point at a tracked ready-to-display texture resource. Where that resource lives is owned by the future content change that introduces it.

## Data and presentation flow

1. `NpcSpawn` resolves `NpcData`; `SpriteType` continues to drive only world-sprite loading.
2. `NpcInteractionController` configures an unparented `DialogueScreenController`.
3. `TryStartDialogue(...)` stores NPC/tree/current node and returns because the screen is not ready.
4. Attachment runs `_Ready()`, binds `%NpcPortrait` with the other authored nodes, then renders the stored node through `ShowNode(...)`.
5. `ShowNode(...)` applies NPC title + portrait through one render path.
6. Later dialogue transitions call `ShowNode(...)` again.
7. Missing/wrong portrait data leaves portrait hidden while existing speaker/body/choices/focus continue normally.

## Testing strategy

### Data contract

Catalog tests:

- every non-null `PortraitPath` in `NpcCatalog.AllNpcs` exists and loads as `Texture2D`;
- shopkeeper and healer currently have portrait data;
- `new NpcData { ... }.PortraitPath` is null when omitted;
- no assertion requires any current catalog entry to remain portrait-less;
- no test derives portrait identity from `SpriteType`.

### Optional loader reuse

`UiArtCatalogTest` adds:

1. missing path called twice -> `null`, one `MissingPaths` entry;
2. existing wrong-type resource called twice -> `null`, one `MissingPaths` entry.

The second case is load-bearing: authored portrait paths are not enum-derived and therefore do not have the type guarantee the existing callers had.

### Dialogue presentation

Add focused cases:

1. **Authored portrait + production start order:** configure shopkeeper before attachment, mount, assert portrait visible/non-null and shell title correct.
2. **Missing optional portrait:** synthetic `NpcData` with `PortraitPath = null`, explicit known dialogue tree, hidden/null portrait, usable initial focus.
3. **Missing path:** synthetic NPC with nonexistent path, hidden/null portrait, usable initial focus.
4. **Wrong-type path:** synthetic NPC with `PortraitPath = "res://scenes/ui/DialogueScreen.tscn"`, hidden/null portrait, usable initial focus.

### Geometry

Do not claim the shell lower-band clamp proves the new row is healthy; `SiriusModalShell` intentionally caps body height.

Keep existing lower-band / compact-safe-height assertions because HPA-625 must not regress them, but add portrait-sensitive checks:

- portrait is visible;
- portrait global right edge is at or before the `DialogueCopy` global left edge (no overlap);
- `%DialogueText` retains useful width (`> 200f`) at standard and compact sizes;
- compact overflow test retains its existing `VerticalScrollBar.MaxValue > Page` and focused-choice scroll assertions, because the portrait reduces text width and can change wrapping/height.

The exact 64/40 constants remain local implementation choices, not geometry-test assertions.

### Scene edit isolation

The `.tscn` reparent is verified before touching controller code:

- declare `IdentityRow`, `NpcPortrait`, `DialogueCopy`, moved `SpeakerLabel`, moved `DialogueText`, then keep `ChoicesContainer` as the final direct-child node block under `BodyHost`;
- run existing scene/modal and blank-speaker tests with the old controller immediately after the scene edit;
- only after those pass bind `_portrait` and modify controller behavior.

This separates a broken node path or declaration order from later controller/test RED.

## Review disposition

Accepted from the latest review:

- harden `UiArtCatalog.LoadOnce<T>` for existing wrong-type resources and test the real cast-failure case;
- replace catalog-null census and nondeterministic missing-portrait selection with synthetic optionality/fallback fixtures;
- make geometry checks assert horizontal non-overlap/text width while retaining compact scroll overflow;
- verify the `.tscn` reparent independently before controller edits, keep `ChoicesContainer` last, and bind `_portrait` before layout/render;
- remove the speculative future `portrait.png` storage convention.

Partially accepted:

- the existing Inventory/HUD portrait row and `AtlasTexture` use are valid reuse evidence. Reuse the row/presenter pattern, but do not redefine `PortraitPath` as a sprite-sheet path that Dialogue must crop. The data contract stays a ready-to-display texture resource so future portrait art remains a catalog-path change.

## Risks and mitigations

### Start-order drift

Mitigation: `ShowNode(...)` is the single identity render point reached from both start orders, and the authored-portrait test configures before attachment.

### Wrong-type authored resource breaks Dialogue mid-render

Mitigation: `LoadOnce<T>` loads as base `Resource`, safe-casts, warns once, and returns `null`; a real existing `.tscn` wrong-type test pins this path.

### Portrait crowds or overlaps copy

Mitigation: standard/compact tests assert portrait/copy non-overlap and useful text width; compact retains its existing overflow/focus-scroll assertions.

### Catalog optionality test fossilizes current content

Mitigation: optionality is tested on synthetic `NpcData`, not by requiring a null in `NpcCatalog.AllNpcs`.

### Accidental world-sprite coupling

Mitigation: Dialogue reads only `PortraitPath`; final grep rejects `SpriteType` references in Dialogue portrait code.

### Future asset convention becomes accidental scope

Mitigation: HPA-625 defines only a tracked ready-to-display texture contract. It does not prescribe future storage or modify `.gitignore`.

## Out of scope

- New portrait art generation or art pipeline
- Portrait animation / expressions / lip sync / voice
- Portraits for every catalog NPC solely to remove nulls
- Portrait registry/service/cache/resource database
- Generic sprite-sheet portrait crop metadata
- New theme metrics
- Reinterpreting `SpriteType`
- Dialogue-domain, Shop/Heal, host, focus-policy, or NPC-interaction lifecycle changes
- `frames/` ignore-policy redesign
- Future portrait asset-directory convention
- HPA-541 Reduced Motion or HPA-359 hardening

## Acceptance mapping

- Explicit authored portrait independent from `SpriteType`: `NpcData.PortraitPath` + catalog mappings.
- Both Dialogue startup orders: `ShowNode(...)` is the single portrait call site; before-attach authored test exercises production order.
- Missing/wrong portrait data: hidden/null clean fallback with usable focus.
- Existing optional-resource policy reused and made type-safe: `UiArtCatalog.LoadContentTexture(...)` + hardened `LoadOnce<T>`.
- Existing portrait presentation reused: `IdentityRow` shape + `UiIconPresenter.ApplyItem(...)`.
- Standard/compact layout: no portrait/copy overlap, useful dialogue width, compact scroll/focus behavior retained.
- Current shipped content: shopkeeper/healer reuse existing tracked ready-to-display `frame1.png`; no new binary or ignore-policy change.