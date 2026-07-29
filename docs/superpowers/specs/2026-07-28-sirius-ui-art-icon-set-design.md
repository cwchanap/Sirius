# Sirius Minimum UI Art and Icon Set Design

Date: 2026-07-28
Issue: HPA-374
Status: Review-ready after third-pass corrections

## 1. Purpose

Produce and integrate the minimum cohesive UI artwork required by the approved Sirius UI visual language. The work turns the HPA-373 asset inventory into stable runtime files without replacing Godot controls, Theme styling, readable labels, or responsive layouts with image-only interfaces.

The final art direction is mystical anime fantasy expressed through celestial navigation, constellation, orbit, and magical-sigil motifs. All new UI artwork is produced with the image-generation tool, then deterministically extracted, cleaned, resized, validated, and integrated.

## 2. Source of truth

The implementation follows these sources in descending priority:

1. The approved HPA-373 visual specification at `docs/superpowers/specs/2026-07-25-sirius-ui-visual-language-design.md`
2. The high-fidelity battle reference at `docs/ui/hpa-373/reference/battle-preparation-reference.png`
3. Runtime enums, catalogs, scenes, and controllers in the current checkout
4. This HPA-374 production and integration design
5. Existing UI asset documentation, corrected where it conflicts with the approved visual specification or runtime

The HPA-373 specification fixes the palette, icon sizes, outline treatment, focus and selection rules, state semantics, and minimum logical resolution. HPA-374 does not reopen those decisions.

## 3. Scope

### 3.1 Included

- Generated status, inventory, equipment, action, semantic, and input-device artwork
- Generated celestial ornaments and minimum battle/reward feedback artwork
- True 16, 24, and 32 px icon exports from larger generated masters
- Stable runtime paths and a typed runtime lookup catalog
- Initial integration into current screens where it does not change gameplay or screen architecture
- Bundled fonts required by HPA-373
- Prompt, source, generation, licensing, and intended-usage records
- Automated asset validation and representative runtime smoke coverage
- Explicit acceptance of the existing main-menu and battle backgrounds

### 3.2 Excluded

- New world, character, enemy, NPC, item, or floor artwork
- HPA-375 comparison, filter, or user-sort icons
- Manual Attack, Defend, or Run button art
- Gameplay changes made to justify an icon or control
- A new screen-navigation or modal architecture
- A new accessory-slot unlocking or progression rule
- Dedicated passive-skill artwork; passive-skill domain data exists, but the current inventory presenter configures only the active skill, so any future passive-skill presentation uses readable slot labels or a later icon expansion
- Baked text inside general-purpose artwork
- Independent state-specific copies of every icon

## 4. Existing asset decisions

| Asset | Decision | Reason |
|---|---|---|
| `assets/sprites/ui/ui_main_menu_background.png` | Retain | It provides the approved castle, moon, and open-left composition for the main-menu orbit. |
| `assets/sprites/ui/ui_battle_background.png` | Retain | It is the scenic foundation used by the approved high-fidelity battle reference. |
| Existing item PNGs | Retain | Inventory, equipment, shop, reward, and battle-item views display the actual item art. |
| Planned Attack, Defend, and Run button PNGs | Retire from the plan | The approved automatic-combat flow has no final manual Attack, Defend, or Run controls. |
| Existing hidden Attack, Defend, and Run scene nodes | Preserve unskinned | HPA-374 neither skins nor removes these legacy nodes; removal belongs to the battle-screen migration. |
| Existing emoji headings | Replace | Production emoji are prohibited by HPA-373. |
| Existing text-only icon substitutes | Replace incrementally | Generated art is paired with text rather than replacing readable labels. |

The retained backgrounds are not regenerated or modified under HPA-374.

## 5. Generation strategy

### 5.1 Category board families

Six cohesive category board families are generated:

1. Core stats and status effects
2. Inventory, equipment slots, and supported inventory/shop actions
3. Screen, flow, and semantic symbols
4. Keyboard, mouse, and gamepad glyph components
5. Celestial ornaments
6. Encounter, impact, status, and reward effects

Each family uses the same approved palette, dark outline language, controlled highlight treatment, and celestial anime-fantasy rendering. A family may span multiple generated sheets or isolated source generations when the required runtime dimensions would make a single grid undersized. Those sources remain visually tied through the same category prompt, palette, reference artwork, silhouette rules, and review contact sheet. Category-wide generation is preferred to unrelated one-off assets because a family makes silhouette weight, highlight direction, edge treatment, and ornament density directly comparable.

Final icon derivatives use the approved 2 px dark outline. Source artwork is simplified before export when downsampling alone would make that outline heavier than the enclosed symbol.

### 5.2 Board-family layout and source resolution

- Grid sheets use a fixed labelled production grid in the prompt, but the generated artwork itself contains no labels or text.
- Every cell contains one centred, isolated subject with a consistent safe margin.
- Icon boards use simplified silhouettes and avoid particles, outer glows, or fine lines that disappear at 16 px.
- Ornament and effect boards may use controlled glow, but the subject must remain separable from the background.
- Every extracted source cell or isolated crop has at least twice the final runtime width and twice the final runtime height. A 512 px ornament dimension therefore has at least 1024 px of source extent on that axis, while each 256×256 effect has at least a 512×512 source crop.
- The largest tool-supported output is used when it satisfies that per-asset budget. No category is forced onto one physical sheet when doing so would reduce an asset below the 2× requirement.
- Before generation, the committed `extraction-map.json` fixes each source file's logical ID order, grid rows and columns where applicable, cell-padding ratio, expected aspect ratio, and minimum source rectangle.
- After generation and before extraction, `extraction-map.json` records the returned source pixel dimensions, exact crop rectangle, local source filename, and matching SHA-256 for every cell or isolated source. Extraction does not begin until that actual-pixel map is complete.
- Original generated files remain in the image-generation output location. Workspace-local copies used for extraction live under `art_source/ui/hpa-374/boards/`, are excluded from Git, and are blocked from Godot import by the tracked `art_source/.gdignore`.
- Prompts, the extraction map, board hashes, generation tool and date, and source-to-runtime mapping are committed under `docs/ui/hpa-374/sources/`. Raw generated boards are not committed, and HPA-374 does not introduce Git LFS.

### 5.3 Extraction and alpha cleanup

Board cells are extracted by deterministic coordinates. Each crop is:

1. Checked for clipping and correct cell ownership
2. Trimmed to its visible bounds, except that the horizontally repeatable edges of `calibration_ticks` intentionally meet the left and right canvas boundaries
3. Re-centred on a transparent canvas matching its documented runtime aspect ratio: square for icons and effects, and the §6.5 width-to-height ratio for ornaments
4. Normalized to sRGB with conflicting ICC profiles removed
5. Processed for real PNG alpha
6. Converted to premultiplied alpha, downsampled with a high-quality antialiasing filter, unpremultiplied, and saved as straight-alpha RGBA PNG
7. Checked again at its final target sizes, including exact left/right RGBA seam equality for `calibration_ticks`

The generator's transparency request is not trusted by itself. Edge-connected matte pixels are removed before resizing. A crop is rejected when background contamination, colour spill, clipped glow, or an unreadable silhouette remains after cleanup.

All runtime derivatives are downsample-only. The pipeline never enlarges a generated crop and never applies non-uniform scaling to satisfy a target size. Regeneration happens at category-family granularity when style drift affects the category. A single cell may be repaired only when the repair preserves the family's established language.

### 5.4 Overwrite policy

The pipeline checks every canonical runtime path before writing. Existing files are not overwritten unless the implementation explicitly records that the asset is being replaced. Generated source files are copied, not moved, from the image-generation output location.

### 5.5 Production sequence

The inventory and equipment board family is produced, integrated, and validated first because it exercises both mandatory current-screen changes: replacing emoji headings and presenting empty or inactive slots. Core stats and status, flow and semantic, and input families follow. Ornaments and effects are produced after the icon pipeline, source manifest, and contact-sheet validation are proven.

## 6. Runtime asset inventory

Every ID enumerated in §6.1–§6.6 is required for HPA-374 completion and must pass release validation even when no current legacy screen consumes it. Runtime “optional” means only that a missing ornament or effect uses a non-crashing fallback during development; it does not make the catalog entry optional for the ticket.

### 6.1 Core stats and status effects

All entries export at 16, 24, and 32 px.

| Group | IDs |
|---|---|
| Core stats | `health`, `mana`, `experience`, `level`, `gold`, `attack`, `defense`, `speed` |
| Debuffs | `poison`, `burn`, `stun`, `weaken`, `slow`, `blind` |
| Buffs | `regen`, `haste`, `strength`, `fortify` |

The ten status IDs correspond exactly to `StatusEffectType`. Reserved enum value 11 produces no asset.

### 6.2 Inventory and supported actions

All entries export at 16, 24, and 32 px.

| Group | IDs |
|---|---|
| Item categories | `general`, `equipment`, `consumable`, `quest` |
| Equipment slots | `weapon`, `shield`, `armor`, `helmet`, `shoe`, `accessory` |
| Configuration and state | `active_skill`, `locked` |
| Supported actions | `equip`, `unequip`, `use`, `assign`, `buy`, `sell` |

Actual item icons remain the primary art for populated slots. Slot glyphs appear on empty slots. The lock symbol is used by the current inventory scene's inactive accessory placeholders beyond `EquipmentSet.AccessorySlotCount`; it does not imply that HPA-374 adds a rule for unlocking those placeholders. `active_skill` is the only skill-specific icon in this minimum set.

`assign` means assigning the active skill to its supported selector or slot. It never means inventory reordering.

### 6.3 Screen, flow, and semantic symbols

All entries export at 16, 24, and 32 px.

| Group | IDs |
|---|---|
| Flow | `pause`, `resume`, `settings`, `save`, `load` |
| Interaction | `dialogue`, `shop`, `heal`, `puzzle`, `reward` |
| Semantic | `info`, `warning`, `error`, `confirm`, `cancel_close` |

Warning and destructive meaning is never communicated by the symbol alone. A title or action label remains visible.

### 6.4 Input glyph components

All entries export at 16, 24, and 32 px.

| Device | Device-class glyph | Overlayable binding components |
|---|---|---|
| Keyboard | `keyboard` | `keycap_blank` |
| Mouse | `mouse` | `mouse_primary`, `mouse_secondary`, `mouse_wheel` |
| Gamepad | `gamepad` | `gamepad_face_blank`, `gamepad_dpad`, `gamepad_stick`, `gamepad_shoulder` |

Bindings remain dynamic. A presenter overlays the current localized key or button label on the blank keycap or button frame. Unknown bindings fall back to readable text. Standardized button markings are permitted only when they describe the physical input glyph, never a game action.

### 6.5 Celestial ornaments

This set deliberately extends the abbreviated HPA-373 §11.3 inventory. `focus_halo` and `selection_halo` implement the independent states required by HPA-373 §6.1, while the connector, endcap, and divider assets compose the callout, catalogue-rail, and trajectory geometry required by HPA-373 §5.3.

The catalogue-rail body is reusable Theme and container geometry. `catalogue_rail_endcap` supplies only the generated bitmap ornament at its ends; HPA-374 does not generate a full rail surface texture.

| ID | Runtime size | Stretch policy |
|---|---:|---|
| `celestial_anchor` | 192×192 | Uniform scale only |
| `orbit_arc` | 512×256 | Uniform scale or crop |
| `trajectory_line` | 512×64 | Horizontal stretch within safe centre |
| `calibration_ticks` | 256×64 | Seamless horizontal repeat at true height; left and right RGBA edge columns must match exactly |
| `callout_frame` | 512×256 | Nine-patch border with a fixed 32 px preservation margin on every side; transparent content surface |
| `callout_connector` | 256×64 | Horizontal stretch within safe centre |
| `catalogue_rail_endcap` | 128×256 | Uniform scale only |
| `ignition_seal` | 192×192 | Uniform scale only |
| `constellation_corner` | 128×128 | Uniform scale only |
| `constellation_divider` | 512×64 | Horizontal stretch within safe centre |
| `partial_sigil` | 256×256 | Uniform scale only |
| `focus_halo` | 96×96 | Uniform scale only |
| `selection_halo` | 96×96 | Uniform scale only |

Ornaments are transparent overlays exported on canvases matching the aspect ratios in this table. The standard safety inset applies to every non-repeatable edge. `calibration_ticks` is the sole trim/inset exception: its repeatable artwork intentionally reaches the left and right boundaries while retaining the top and bottom safety inset.

Panel fills, text surfaces, borders, spacing, and responsive geometry remain Godot Theme and control responsibilities. No ornament is allowed to become an image-only replacement for a reusable control.

### 6.6 Effects

| ID | Runtime size | Intended use |
|---|---:|---|
| `encounter_burst` | 256×256 | Short battle-entry emphasis |
| `hit_impact` | 256×256 | Damage feedback |
| `status_pulse` | 256×256 | Status application and cure feedback |
| `reward_level_up` | 256×256 | Reward reveal and level-up emphasis |

Effects are static transparent PNGs animated through Godot scale, opacity, rotation, or controlled duplication. They do not require baked text or frame-by-frame sprite sheets. Reduced-motion presentation uses a short opacity transition without rotation or large scale changes.

Each effect has at least a 512×512 generated source crop and a 256×256 runtime derivative, providing intentional downsampling and animation headroom before the effect is displayed at a smaller logical size. No HPA-374 code loads the retired root-level 96×96 effect paths.

## 7. Runtime paths

Runtime files use these stable layouts:

```text
assets/
├── fonts/
│   ├── cinzel/
│   ├── noto_sans/
│   └── noto_sans_mono/
└── sprites/
    ├── effects/ui/<effect>.png
    └── ui/
        ├── icons/<category>/<16|24|32>/<id>.png
        └── ornaments/<id>.png
```

The icon category path is fixed as follows:

| Path category | IDs |
|---|---|
| `stats` | `health`, `mana`, `experience`, `level`, `gold`, `attack`, `defense`, `speed` |
| `status` | `poison`, `burn`, `stun`, `weaken`, `slow`, `blind`, `regen`, `haste`, `strength`, `fortify` |
| `inventory` | `general`, `equipment`, `consumable`, `quest`, `weapon`, `shield`, `armor`, `helmet`, `shoe`, `accessory`, `active_skill`, `locked` |
| `actions` | `equip`, `unequip`, `use`, `assign`, `buy`, `sell` |
| `flow` | `pause`, `resume`, `settings`, `save`, `load` |
| `interaction` | `dialogue`, `shop`, `heal`, `puzzle`, `reward` |
| `semantic` | `info`, `warning`, `error`, `confirm`, `cancel_close` |
| `input` | `keyboard`, `keycap_blank`, `mouse`, `mouse_primary`, `mouse_secondary`, `mouse_wheel`, `gamepad`, `gamepad_face_blank`, `gamepad_dpad`, `gamepad_stick`, `gamepad_shoulder` |

Workspace-local generated production sources use:

```text
art_source/
├── .gdignore
└── ui/hpa-374/boards/
```

`art_source/.gdignore` is tracked. `.gitignore` excludes `art_source/ui/hpa-374/boards/`, so raw generated outputs remain local and are neither Godot resources nor Git/LFS objects.

Documentation and committed source records use:

```text
docs/ui/hpa-374/
├── ASSET_MANIFEST.md
├── CONTACT_SHEETS.md
├── README.md
└── sources/
    ├── SOURCE_MANIFEST.md
    ├── extraction-map.json
    └── prompts/
```

`SOURCE_MANIFEST.md` records each local board filename, SHA-256, generator and generation date. `extraction-map.json` repeats the board hash beside every crop so a derivative can be traced to the exact local source without committing that large source image.

### 7.1 Godot import and display policy

- Icons and ornaments use lossless texture import with mipmap generation disabled. Their true-size derivatives avoid scale-dependent icon blur.
- The four effect PNGs use lossless texture import with mipmap generation enabled because they are deliberately displayed at changing, usually smaller scales during animation.
- The repository's broad `*.import` ignore remains in place. HPA-374 adds only `!assets/sprites/effects/ui/*.png.import` and commits the four effect sidecars so their mipmap exception is reproducible. No other generated import metadata is added.
- Alpha-border correction remains enabled so filtered transparent edges do not acquire a matte fringe.
- Generated art uses linear CanvasItem filtering. True-size 16, 24, and 32 px derivatives are selected instead of scaling one icon across all metadata, default, and feature roles.
- Texture repeat is disabled except for an ornament explicitly documented as repeatable, such as calibration ticks.
- Aspect ratio is preserved. Only the safe centre of `trajectory_line`, `callout_connector`, and `constellation_divider` may stretch horizontally, and only the area inside `callout_frame`'s fixed 32 px border may nine-patch stretch.
- Import and display settings are exercised by resource-loader tests, the visual contact sheet, and runtime smoke checks rather than assumed from source PNG dimensions.

This exception follows Godot's stable [image-import guidance](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_images.html): mipmaps are useful when a CanvasItem texture is scaled down, while true-size 2D textures generally do not need their memory overhead.

## 8. Fonts and licensing

Image generation does not apply to fonts. HPA-374 bundles:

- Cinzel SemiBold
- Noto Sans Regular, Medium, and SemiBold
- Noto Sans Mono Medium

Font files come from official upstream sources and retain their SIL Open Font License files. The asset manifest records exact upstream URLs, retrieved versions or commit references, file hashes, and intended roles.

The Regular, Medium, and SemiBold Noto Sans files provide body, control, and emphasis weights within the already-approved Noto Sans family; they do not introduce a new typeface decision.

Release validation checks that every declared font file and OFL file exists, every font SHA-256 matches the asset manifest, and Godot's `ResourceLoader` loads each font as a `FontFile`. Wiring those roles into the shared Theme and migrated screens belongs to the HPA-373 screen/theme migrations; HPA-374 ships and validates the approved font resources without expanding into a Theme rewrite.

Generated assets record:

- Runtime path
- Category board and cell
- Complete generation prompt
- Generation tool and date
- Post-processing performed
- Intended runtime usage
- Source statement showing that the artwork was generated for Sirius and did not come from a third-party art pack

The manifest does not make unsupported legal claims about exclusive ownership or copyrightability.

## 9. State treatment

One base icon normally serves all interactive states:

| State | Presentation |
|---|---|
| Normal | Original approved palette on indigo surface |
| Hover | Brighter surface plus restrained cyan edge light |
| Focus | Independent 2 px cyan Theme ring; `focus_halo` may supplement circular or square nodes and slots |
| Selected | Gold Theme border or marker; `selection_halo` may supplement circular or square nodes and slots |
| Pressed | Darkened control surface and visual depression |
| Disabled | 45% opacity, no glow, readable reason |
| Warning | Amber semantic tint, warning symbol, and text |
| Destructive | Rose semantic tint, error or confirmation symbol, and explicit action text |

Focus and selection can coexist. Runtime modulation and Theme styling create state variants; HPA-374 does not generate separate normal, hover, focus, selected, pressed, and disabled PNG copies.

`focus_halo` and `selection_halo` are never stretched non-uniformly around rectangular buttons or list rows. Those controls use Theme-owned 2 px focus and selected rings so the state outline follows the actual responsive geometry.

At 16 px, every icon must retain a distinct outer silhouette. Fine interior details are removed before reducing outline weight or target size.

## 10. Runtime integration

### 10.1 Thin art catalog

`scripts/ui/art/UiArtCatalog.cs` maps approved IDs and sizes to stable resource paths. `scripts/ui/art/UiIconPresenter.cs` and `scripts/ui/art/InputHintPresenter.cs` provide the narrowly reusable presentation behavior needed by current consumers. They provide:

- Typed icon lookup
- Ornament and effect path lookup
- Safe texture loading
- A readable fallback for an unknown or unavailable ID

The catalog is not a new navigation, layout, modal, or theme framework. It centralizes paths so controllers do not embed string literals or infer filenames.

### 10.2 Initial consumers

The following HPA-374 integrations are non-negotiable:

- Replace the Inventory screen's Equipment and Items emoji headings with icon-and-label rows.
- Show equipment-slot symbols when a slot is empty and the lock symbol on the current scene's inactive accessory placeholders; do not add an unlock condition or progression rule.
- Keep actual item PNGs on populated inventory and equipment slots.
- Ship the complete typed catalog, visual contact sheets, and automated existence, dimension, alpha, enum, and exact-ID integrity checks.
- Bundle the approved fonts with their OFL files, upstream references, hashes, and role assignments in the asset manifest.
- Continue loading the retained main-menu and battle backgrounds from their existing stable paths.

The following integrations are attempted when they fit the legacy surface without layout or behavior changes; otherwise the validated catalog entry is the HPA-374 deliverable and placement is deferred:

- Add stat and status icons beside existing readable battle values where the current layout can accept them without changing battle flow.
- Use the hit, status, and reward artwork in current battle feedback through lightweight animation.
- Replace hard-coded input-hint text where a current binding-aware presenter can be introduced safely.
- Make semantic icons available to existing pause, settings, save/load, dialogue, shop, healing, puzzle, warning, confirmation, and error content headers.

Generated ornaments are registered and demonstrated through reusable presentation helpers, but full ornamental screen composition remains with the corresponding HPA-373 screen migrations. HPA-374 does not force new spatial layouts into legacy scenes.

If a legacy battle row cannot accept a stat or status icon without resizing or restructuring existing controls, HPA-374 registers and verifies the asset in the catalog and defers placement to that screen's HPA-373 migration. The same rule applies when binding-aware input artwork cannot replace hard-coded hint text without changing the legacy layout. Deferral leaves readable text in place and does not expand HPA-374 into layout work.

### 10.3 Input hints

The reusable input-hint presenter accepts a current binding description rather than a hard-coded game action label. It:

1. Derives the active device class from the most recent relevant `InputEvent`, or accepts an explicit class for deterministic presentation.
2. Re-reads `InputMap.ActionGetEvents` when its screen is shown and whenever the presenter is refreshed.
3. Chooses the matching device or blank-button artwork.
4. Overlays the resolved binding label.
5. Falls back to readable text when the binding cannot be represented.

The presenter does not cache persisted bindings, own input remapping, require a new `SettingsManager` signal, mutate `InputMap`, or change the HPA-376 input-lifecycle contract.

## 11. Error handling

### 11.1 Production pipeline

- Missing required source file: stop the category-family extraction.
- Unexpected board dimensions or cell count: stop before writing runtime files.
- Opaque or contaminated background: reject the affected crop.
- Clipped artwork or unreadable 16 px result: regenerate the category family or repair the cell consistently.
- Existing canonical target: preserve it unless replacement is explicitly recorded.
- Failed derivative export: do not leave a partial category marked complete.

### 11.2 Runtime

- Known catalog entries are verified before release and therefore must resolve without warnings.
- An unknown icon ID falls back to readable text or the generic information symbol.
- A missing ornament or effect never blocks gameplay at runtime, but its absence still fails HPA-374 release validation.
- Missing catalog artwork produces one actionable development warning and a readable or no-effect fallback, not a crash or silent interactive control.

## 12. Validation

### 12.1 Test ownership

Validation is split at the runtime boundary:

- `tests/tools/test_ui_asset_coverage.py` owns Pillow-based filesystem, PNG mode, dimensions, alpha bounds, sRGB metadata, board-hash provenance, seam, negative-path, font-file/hash/OFL, and scoped emoji checks.
- `tests/ui/art/UiArtCatalogTest.cs` owns exact typed-ID coverage, enum mappings, `ResourceLoader` resolution, font `FontFile` loading, and imported texture behavior.
- `tests/ui/art/InputHintPresenterTest.cs` owns binding refresh, readable fallback, and active-device presentation.

Python tests run explicitly with `python3 -m pytest tests/tools`; they are not implied by `dotnet test`. GdUnit4 tests run with `dotnet test Sirius.sln --settings test.runsettings.local`.

### 12.2 Automated asset checks

Automated tests verify:

- Every expected runtime file exists.
- The catalog contains exactly the icon, ornament, and effect IDs enumerated in §6.1–§6.6; every entry resolves through Godot's resource loader and no undocumented extra ID is accepted.
- Every icon has exact 16, 24, and 32 px derivatives.
- Every ornament and effect has its documented dimensions.
- PNGs use RGBA and contain real transparency where required.
- Visible bounds are non-empty and do not touch forbidden crop edges.
- At 16 px, the opaque-core bounds measured at alpha ≥128 occupy at least 50% of one axis and at least 30% of both axes. All non-transparent icon pixels remain inside a one-pixel safety inset.
- `calibration_ticks` retains its top and bottom safety inset and has byte-identical left and right RGBA edge columns after final export.
- Every `StatusEffectType`, `ItemCategory`, and `EquipmentSlotType` value has its required mapping.
- The reserved status value 11 has no mapping.
- Catalog paths resolve through Godot's resource loader.
- All five declared font files and the required OFL file for each font family exist, each font SHA-256 equals the manifest value, and every font loads through Godot as a `FontFile`.
- Icons and ornaments load without mipmaps; all four effect textures load with mipmaps and have committed per-file import metadata.
- The retired manual-combat files `assets/sprites/ui/ui_button_attack.png`, `assets/sprites/ui/ui_button_defend.png`, and `assets/sprites/ui/ui_button_run.png` do not exist.
- HPA-375-only icons are absent.
- The retired root-level files `assets/sprites/ui/icon_health.png`, `assets/sprites/ui/icon_experience.png`, `assets/sprites/ui/icon_level.png`, `assets/sprites/effects/effect_hit_impact.png`, `assets/sprites/effects/effect_magic_sparkles.png`, and `assets/sprites/effects/effect_level_up.png` do not exist.
- The committed extraction map and source manifest agree on every board filename and SHA-256. When local raw boards are present, the extraction command additionally hashes each file and refuses to run on a mismatch; CI does not pretend to re-hash ignored source files that are absent from the checkout.

The input-presenter tests temporarily install synthetic `InputEventJoypadButton` and `InputEventJoypadMotion` mappings in `InputMap`, exercise gamepad detection and labels, and restore the exact prior action events in teardown. They also change a temporary mapping between show/refresh calls to prove that the presenter re-reads current bindings. These tests do not mutate saved settings or gameplay-domain bindings.

### 12.3 Visual contact sheets

Contact sheets render every icon:

- At 16, 24, and 32 px
- On approved dark surfaces
- In normal, focused, selected, and disabled presentation
- With category labels outside the art itself

Review rejects ambiguous silhouettes, inconsistent outline weight, clipped glow, unreadable disabled treatment, or icons that rely on colour alone.

### 12.4 Runtime verification

Representative smoke passes cover:

- Main menu
- Exploration HUD
- Battle preparation, automatic combat, status feedback, and results
- Inventory and equipment
- Pause and settings
- Save/load
- Dialogue, shop, and healing
- Puzzle, reward, confirmation, warning, and error presentation

The minimum verification sizes are 640×360 and 1280×720. The smoke pass checks intended display size, focus visibility, selected and disabled differentiation, text pairing, input-device switching, and runtime logs for missing-resource warnings. Input-device switching uses a connected controller when available or injects a synthetic joypad event through the smoke harness; it does not require adding persistent gameplay bindings.

This two-viewport pass validates HPA-374 asset integration only. It does not replace the seven-viewport responsive matrix in HPA-373 §4.2, which remains a required acceptance gate for the corresponding screen migrations.

### 12.5 Production emoji scan

The automated scan rejects emoji only in structurally user-facing string contexts:

- In `.tscn` files, assignments to `text`, `tooltip_text`, `placeholder_text`, `dialog_text`, and `title`
- In `scripts/ui/**/*.cs`, assignments to `.Text`, `.TooltipText`, `.DialogText`, and `.Title`

The scanner tokenizes these assignment contexts and excludes comments and call arguments to `GD.Print`, `GD.PrintErr`, `GD.PushWarning`, and `GD.PushError`. Developer-only log strings are therefore excluded by structure rather than by subjective review or a broad allowlist.

## 13. Documentation changes

Implementation updates:

- `docs/ui/UI_SPRITES.md` and `docs/items/ASSET_STATUS.md` to retire the obsolete `ui_button_attack.png`, `ui_button_defend.png`, and `ui_button_run.png` manual-battle-button plan in favour of the approved automatic-combat inventory
- `docs/ui/UI_SPRITES.md` and `docs/items/ASSET_STATUS.md` to retire the stale root-level `assets/sprites/ui/icon_health.png`, `icon_experience.png`, and `icon_level.png` references in favour of the §7 categorized icon paths
- `docs/ui/UI_SPRITES.md` and `docs/items/ASSET_STATUS.md` to retire the old root-level 96×96 effect paths `effect_hit_impact.png`, `effect_magic_sparkles.png`, and `effect_level_up.png` in favour of the §6.6 `assets/sprites/effects/ui/` inventory. `status_pulse` is a new status-specific asset, not a rename or derivative of `effect_magic_sparkles`.
- `docs/ui/hpa-374/ASSET_MANIFEST.md` with exact shipped status and provenance
- `docs/ui/hpa-374/CONTACT_SHEETS.md` with the visual validation output
- `docs/ui/hpa-374/sources/` with committed prompts, extraction mapping, board SHA-256 values, and generator/date metadata while raw source boards remain ignored
- Any item or UI asset-status summary whose counts or notes become stale

Documentation reflects the filesystem after generation. A file is not marked shipped until it exists at the canonical runtime path and passes validation.

## 14. Acceptance mapping

| HPA-374 acceptance requirement | Design response |
|---|---|
| Required artwork exists at stable paths | Explicit inventory, dimensions, directories, catalog, and existence tests |
| Artwork is integrated rather than delivered as a detached dump | Mandatory inventory heading and slot integration plus catalog, contact-sheet, test, and font deliverables |
| HPA-375 remains optional | Filter, sort, and comparison assets are excluded |
| No production emoji | Current headings are replaced and a scan is added |
| Cohesive mystical anime-fantasy direction | Category-family generation follows the approved reference and palette |
| Minimum-size readability | True-size derivatives and contact-sheet review |
| States remain distinguishable | Independent focus/selection halos, semantic tint, text, and disabled treatment |
| Background decision is explicit | Both existing scenic backgrounds are retained |
| No obsolete manual-combat art | Attack, Defend, and Run assets are explicitly prohibited |
| Documentation matches runtime | Manifest, filesystem-first status, and doc updates |
| Source and licensing metadata recorded | Committed prompts, crop map, source hashes, generated provenance, and official font license records |
| Font package is reproducible | File, SHA-256, OFL, and Godot `FontFile` load checks |
| Dynamic keyboard, mouse, and gamepad hints remain truthful | Show/refresh re-resolves `InputMap`; synthetic device-event tests restore prior mappings |
| Effect scaling remains clean | Effects alone commit reproducible mipmapped imports; true-size icons and ornaments remain non-mipmapped |
| No missing-resource warnings | Catalog/resource tests and representative runtime log review |

## 15. Implementation boundary

HPA-374 delivers generated artwork, production derivatives, stable lookup paths, minimum current-screen integration, validation, and documentation. It does not redesign screen geometry or change combat, inventory, save, dialogue, shop, healing, puzzle, reward, or input-domain behavior.
