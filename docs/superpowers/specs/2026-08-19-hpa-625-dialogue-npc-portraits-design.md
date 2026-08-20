# HPA-625 Sirius NPC Portrait and Dialogue Identity Design

## Goal

Complete the deferred HPA-373 Dialogue identity treatment by giving `NpcData` one explicit optional portrait resource path and rendering that portrait in the existing hosted `DialogueScreen` when authored.

This is a small presentation/data slice. It does not change dialogue trees, NPC interaction sequencing, `UIScreenHost`, world-sprite loading, or character art infrastructure.

## Why this is the next actionable slice

HPA-573 is merged and complete. The ordered HPA-358 presentation migrations are otherwise complete, and HPA-625 is an unblocked concrete gap that HPA-569 deliberately deferred from HPA-373 §9.8.

HPA-541 remains an optional, nonblocking Reduced Motion enhancement. HPA-359 is the final hardening gate, but it is still formally blocked by the HPA-355/HPA-358 workstream checkpoints. Closing the known Dialogue portrait gap before closing HPA-358 keeps that checkpoint truthful without expanding into optional polish.

This branch and draft PR are the single PR for HPA-625: the design and implementation plan land first, and implementation continues on the same branch/PR.

## Current state

- `NpcData` is a plain catalog model. It has `DisplayName`, `DialogueTreeId`, and `SpriteType`, but no UI portrait field.
- `SpriteType` is consumed by `NpcSpawn` to find the world sprite sheet. HPA-569 explicitly says it must not be reused as portrait identity metadata.
- `DialogueScreenController.TryStartDialogue(...)` already receives the full `NpcData`, so no new data handoff is required.
- Production `NpcInteractionController.Begin()` instantiates `DialogueScreen`, calls `TryStartDialogue(...)` while the screen is still unparented, and only then presents it through `UIScreenHost`. Portrait rendering must therefore work when configuration happens before `_Ready()`.
- `DialogueScreen.tscn` already owns the HPA-373 lower-band composition through `SafeFrame` + `SiriusModalShell`.
- The shell title already displays `NpcData.DisplayName`; `DialogueNode.SpeakerName` is the per-node speaker line.
- The Ground Floor currently spawns `village_shopkeeper` and `village_healer`.
- Both spawned NPCs already have tracked authored frame PNGs under `assets/sprites/npcs/.../frames/`.
- `old_farmer` and `village_blacksmith` exist in the catalog but are not currently spawned by the shipped floor generation path. Their missing portrait must remain a valid, clean fallback.
- HPA-373 explicitly allows existing NPC sprite sheets/frames to be reused as content imagery and requires missing portrait art to degrade gracefully.
- Existing asset-loading code such as `Item.TryLoadAsset(...)` checks `ResourceLoader.Exists(...)` before loading so a missing configured resource does not also trigger a loader error.

## Options considered

### A. Add explicit `NpcData.PortraitPath` and reuse current frame art — selected

Add one nullable resource path to `NpcData`. The catalog explicitly maps the currently shipped shopkeeper and healer to their existing `frame1.png` resources. `DialogueScreenController` loads that resource for presentation and hides the portrait node when the field is absent or invalid.

This is the smallest durable contract: portrait intent is authored where NPC identity already lives, future dedicated portrait art is a catalog-path change, and the UI never has to infer semantics from unrelated world-sprite data.

### B. Derive portrait paths from `SpriteType`

Rejected. `SpriteType` is world-sprite folder metadata. Reusing it would make a portrait silently depend on the current directory convention and would undo the exact separation HPA-569 preserved for HPA-625.

### C. Add a portrait registry/service or generate dedicated portrait assets now

Rejected as YAGNI. There is one consumer and two currently shipped NPC portraits can reuse existing authored frames. A registry, presenter service, resource database, cache, or new art-generation pipeline adds ownership and failure modes without solving a current second-consumer problem.

## Architecture

### 1. Add one explicit optional portrait field to `NpcData`

Add:

```csharp
/// <summary>
/// Optional player-facing portrait resource used by Dialogue identity presentation.
/// Independent from SpriteType, which remains world-sprite metadata.
/// </summary>
public string? PortraitPath { get; init; }
```

`PortraitPath` is a Godot resource path such as `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`.

It is optional by design. Absence means “no authored portrait for this NPC,” not an error and not a request to infer one.

Do not add a portrait ID enum, portrait catalog, loading service, or fallback derived from `NpcId`/`SpriteType`.

### 2. Author only the portrait mappings supported by current shipped content

Update `NpcCatalog`:

- `village_shopkeeper` → `res://assets/sprites/npcs/shopkeeper/frames/frame1.png`
- `village_healer` → `res://assets/sprites/npcs/healer/frames/frame1.png`
- `old_farmer` → no `PortraitPath`
- `village_blacksmith` → no `PortraitPath`

The first two are the NPCs currently spawned on the Ground Floor. Reusing their existing complete frame art satisfies the ticket without manufacturing new binaries or encoding a crop of `sprite_sheet.png` into Dialogue.

The latter two intentionally exercise the supported missing-portrait contract until they become shipped content with authored art. Do not point them at unrelated character art or synthesize placeholders just to make every catalog row non-null.

### 3. Extend the existing Dialogue scene; do not add a portrait component

Keep the existing screen, shell, and lifecycle. Restructure only the shell body:

```text
DialogueScreen
└── SafeFrame
    └── ModalShell
        └── .../BodyHost
            ├── IdentityRow (%IdentityRow, HBoxContainer)
            │   ├── NpcPortrait (%NpcPortrait, TextureRect)
            │   └── DialogueCopy (VBoxContainer)
            │       ├── SpeakerLabel (%SpeakerLabel)
            │       └── DialogueText (%DialogueText)
            └── ChoicesContainer (%ChoicesContainer)
```

`%NpcPortrait` starts hidden and has ordinary `TextureRect` aspect-preserving presentation (`ExpandMode = IgnoreSize`, `StretchMode = KeepAspectCentered`). It does not accept input.

The existing shell title remains the NPC name. Do not add a second always-visible `NpcNameLabel`; that would duplicate the same identity text. The portrait supplies the missing visual half of the HPA-373 identity treatment while the existing per-node `SpeakerLabel` continues to support dialogue trees that name a speaker explicitly.

Choices remain below the identity row so portrait width never compresses action buttons.

### 4. Load the explicit portrait in `DialogueScreenController`

Bind `%NpcPortrait` during `_Ready()` and add one private refresh method. Clear the node first so the method is safe if the screen is ever refreshed with absent/bad data.

Use the repository’s existing Exists-then-Load pattern rather than calling `GD.Load(...)` on an unchecked path:

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

The `ResourceLoader.Exists(...)` guard is load-bearing. A nonexistent authored path should produce the single explicit Dialogue warning and clean fallback without invoking Godot’s loader on a missing resource.

Call `RefreshPortrait()` in both supported configuration orders:

- `_Ready()` after node binding, which is the production path because `NpcInteractionController.Begin()` calls `TryStartDialogue(...)` before hosting/attachment;
- `TryStartDialogue(...)` when the screen is already ready, alongside the existing immediate `ShowNode(root)` path used by mounted-screen tests and defensive direct consumers.

Missing portrait data is silent and collapses cleanly. An explicitly configured but nonexistent/unloadable path is an authoring/configuration problem: warn and render the same clean no-portrait layout. Do not substitute `SpriteType` or a placeholder.

Do not extract a shared asset loader for one consumer.

The controller still does not own host lifetime, NPC interaction sequencing, dialogue-tree traversal outside its current behavior, or any domain mutation beyond the already-shipped choice semantics.

### 5. Keep portrait sizing local to Dialogue

Use local constants:

```csharp
private const float StandardPortraitSize = 64f;
private const float CompactPortraitSize = 40f;
```

`RefreshLayout()` sets:

```csharp
var portraitSize = insets.Compact ? CompactPortraitSize : StandardPortraitSize;
_portrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
```

Why these values:

- 64 px gives the standard lower-band Dialogue a readable identity image without turning the sprite frame into a dominant panel.
- 40 px matches the already-approved compact portrait treatment used by the Exploration HUD and satisfies HPA-373’s requirement to reduce the portrait before sacrificing essential text/actions.

Do not add a new global `SiriusUiMetrics` token for one consumer.

The existing standard 45% lower-band and compact full-safe-height policies remain unchanged. The existing shell body scroll remains the only scroll owner.

### 6. Keep current tracked frame reuse separate from future asset authoring

The repository `.gitignore` contains a top-level `frames/` rule, so a newly created nested `assets/sprites/npcs/<npc>/frames/...` file is ignored by default. The two HPA-625 mappings are safe because their `frame1.png` files are already tracked; this PR adds no portrait binaries and therefore does not need a `.gitignore` change.

Do not widen HPA-625 solely to change ignore policy. When a future ticket adds a new NPC portrait, either:

- prefer a tracked dedicated file such as `assets/sprites/npcs/<npc>/portrait.png` beside the sheet; or
- add a narrowly scoped un-ignore for the exact authored portrait path/pattern in that same ticket.

This note prevents the current reuse convention from becoming an accidental staging trap without creating work that HPA-625 does not need.

## Data and presentation flow

1. `NpcSpawn` resolves `NpcData` exactly as today. `SpriteType` continues to drive only its world texture.
2. `NpcInteractionController` instantiates an unparented `DialogueScreenController` and calls `TryStartDialogue(...)` before hosting it.
3. `TryStartDialogue(...)` stores `NpcData`; because the screen is not ready, it does not touch portrait nodes yet.
4. Host attachment runs `_Ready()`, which binds `%NpcPortrait`, calls `RefreshPortrait()`, then renders the stored dialogue node.
5. `DialogueScreenController` reads only `PortraitPath` for portrait presentation.
6. If the path exists and loads, `%NpcPortrait` becomes visible with the texture.
7. If the path is absent or fails validation/load, the portrait remains hidden and the existing text/actions expand naturally into the available identity-row width.
8. Dialogue choices, flags, outcomes, host close behavior, focus, and Shop/Heal handoff remain untouched.

## Testing

### Data contract

`NpcCatalogTest` pins:

- shopkeeper and healer have explicit portrait paths;
- `ResourceLoader.Exists(...)` is true for both mapped paths;
- both paths load as `Texture2D` resources;
- at least one catalog NPC (`old_farmer`) has no portrait, preserving the optional/fallback contract.

The test must not assert that `PortraitPath` equals a value derived from `SpriteType`.

### Dialogue presentation

`DialogueScreenControllerTest` adds focused runtime coverage:

1. **Production start order:** configure an unparented shopkeeper `DialogueScreenController` with `TryStartDialogue(...)`, then mount it at 1280×720. After `_Ready()`, assert the portrait is visible/non-null at 64×64 and title/speaker/body/actions still render. This specifically proves portrait refresh happens in `_Ready()` rather than only in the already-ready start branch.
2. **Missing optional portrait:** mounted 1280×720 old-farmer dialogue hides the portrait and still renders existing text/actions without a placeholder.
3. **Invalid explicit path:** a synthetic `NpcData` with a nonexistent `PortraitPath` renders with portrait hidden/texture null while body/actions remain usable. The production loader checks `ResourceLoader.Exists(...)` before `ResourceLoader.Load(...)`, so this path does not invoke Godot’s loader for a missing resource.
4. **Compact reduction:** 640×360 shopkeeper dialogue keeps the portrait visible at 40×40 and retains readable dialogue/action content.

Run the existing Dialogue suite unchanged as the regression gate for safe-frame geometry, long content, focus, conditions, terminal latching, and gamepad behavior. Run `NpcCatalogTest` with it.

No new `NpcInteractionController`/host tests are required because the public interaction and screen-start interfaces do not change; the new before-ready Dialogue test directly pins the production ordering contract already established by the existing suite.

## Risks and mitigations

### Portrait works only when Dialogue is already mounted

Mitigation: the authored-portrait regression configures the screen before attachment exactly like `NpcInteractionController.Begin()` and asserts portrait presentation after `_Ready()`.

### Broken authored portrait path produces noisy loader errors

Mitigation: `RefreshPortrait()` checks `ResourceLoader.Exists(...)` first, emits one explicit warning for a nonexistent configured path, and returns without calling the loader. A synthetic bad-path Dialogue test pins the clean UI fallback.

### Accidental coupling back to world sprites

Mitigation: `DialogueScreenController` references only `PortraitPath`; final grep verifies no `SpriteType` dependency was introduced into Dialogue presentation.

### Compact portrait crowds text/actions

Mitigation: 40 px compact size, portrait located beside copy rather than choices, and the existing shell scroll remains the body overflow owner. A 640×360 regression test pins the layout.

### Future new NPC frame art is silently ignored by git

Mitigation: HPA-625 reuses already-tracked frame PNGs and changes no ignore rules. The design records that future new portrait assets should use a tracked `portrait.png` path or carry a targeted un-ignore in the ticket that adds them.

### Scope expands into new character art

Mitigation: reuse the existing shipped NPC frames. Dedicated portrait production remains unnecessary until current frame art is demonstrably inadequate or another ticket explicitly owns new art.

## Out of scope

- New portrait art generation or a character-art pipeline
- Portrait animation, expression switching, lip sync, voice, or typewriter effects
- Portraits for unshipped catalog entries solely to eliminate nulls
- A generic portrait service, registry, presenter, resource database, cache, or shared asset loader
- Reinterpreting `NpcData.SpriteType`
- Dialogue-tree/domain changes
- Shop/Heal presentation changes
- `UIScreenHost`, modal-shell, focus-policy, or NPC-interaction lifecycle changes
- `.gitignore` changes when no new portrait file is being authored
- HPA-541 Reduced Motion or HPA-359 release hardening

## Acceptance mapping

- Explicit authored portrait without `SpriteType` inference: `NpcData.PortraitPath` + catalog mappings.
- Production pre-`_Ready()` start order: before-attach authored-portrait regression.
- Missing portrait leaves a clean layout: hidden-by-default `%NpcPortrait` and old-farmer regression.
- Invalid explicit path leaves a clean layout without a missing-resource load attempt: `ResourceLoader.Exists(...)` guard + synthetic bad-path regression.
- Compact Dialogue reduces portrait first: 64 px standard → 40 px compact, with 640×360 coverage.
- HPA-373 §9.8 portrait requirement for current shipped NPC content: shopkeeper and healer reuse their existing authored frame imagery in the hosted Dialogue identity area.
