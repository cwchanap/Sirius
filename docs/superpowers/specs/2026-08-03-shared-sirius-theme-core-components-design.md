# HPA-377 Shared Sirius Theme, Core Components, and UI Showcase Design

**Status:** Proposed design — pending review  
**Date:** 2026-08-03  
**Issue:** HPA-377  
**Repository:** `cwchanap/Sirius`  
**Runtime:** Godot 4.6, C#/.NET 8, GdUnit4  
**Depends on:** HPA-373 and HPA-374  
**Integrates with:** HPA-378  
**Blocks:** HPA-379  
**Reduced-motion persistence handoff:** HPA-541  
**Toast/reward queue handoff:** HPA-386

## 1. Summary

HPA-377 implements the approved Sirius visual language as one canonical Godot `Theme` resource, a deliberately small set of presentation-only components, and an isolated UI showcase. It creates the visual foundation required by later screen migrations without restyling or restructuring existing production screens.

The design uses three ownership boundaries:

1. **The Theme owns visual values.** Fonts, colours, style boxes, font sizes, control constants, control-state resources, scrims, and base native-control styling live in one authored resource.
2. **Thin components own presentation behaviour.** Semantic variants, loading-state restoration, stat formatting, input-hint composition, modal/toast composition, compact presentation, reduced-motion variants, and focus ornament behaviour live in reusable controls with closed APIs.
3. **`UIScreenHost` and consuming flows own lifecycle.** Screen stacking, queueing, pause, gameplay-input blocking, cursor policy, HUD policy, cancellation, focus restoration, and notification lifetime remain outside HPA-377.

The shared Theme is opt-in during HPA-377. It is assigned to the showcase preview root and component test fixtures, but it is not configured as `ProjectSettings.gui/theme/custom`. Production roots or individual migrated screens opt in later through HPA-379 and the screen-migration tickets.

HPA-377 defines and tests reduced-motion-capable presentation, but it does not add a persisted player preference. HPA-541 owns persistence, the player-facing Settings control, and production-root propagation.

HPA-377 supplies a visual toast shell only. HPA-386 owns deterministic toast/reward queueing, deduplication, timeout policy, stacking, and lifecycle integration.

## 2. Approved source baseline

Implementation is grounded in the approved HPA-373 artifact:

- specification version: **1.7**;
- design decisions approved: **2026-07-25**;
- source content blob: `9e1d1edb366a67a3fa6d0dd02f3641aa0bb42a7d`;
- HPA-373 PR: **#17**;
- merged as: `bc82eadcab27e2321c69fcf56cc3c43e6917b5f5`.

The HPA-373 file header still describes the artifact as a review candidate, while Linear marks HPA-373 complete and PR #17 is merged. That stale header is metadata debt, not a runtime dependency. It should be corrected in HPA-373 or a tiny documentation-only follow-up, but it does not block HPA-377 implementation after this design is approved.

HPA-373 approved:

- deep indigo surfaces;
- cyan focus;
- gold selection and commitment;
- magenta arcane or automatic-action accents;
- Noto Sans for body and controls;
- Noto Sans Mono for numeric and telemetry content;
- Cinzel SemiBold for the wordmark and major fantasy headings;
- spacing `4 / 8 / 12 / 16 / 24 / 32 / 48`;
- normal, hover, pressed, focused, selected, disabled, warning, and destructive states;
- responsive behaviour from 640×360 through ultrawide;
- modal and scrim rules;
- restrained motion and reduced-motion alternatives.

HPA-374 shipped two asset groups with different access contracts:

- **Fonts** are direct Godot resources under `res://assets/fonts/` and are referenced directly by `SiriusTheme.tres`.
- **Icons, ornaments, and effects** are exposed through `UiArtCatalog`; `UiIconPresenter` applies icons, and `InputHintPresenter` resolves input-device glyphs and readable binding labels.

`UiArtCatalog` does not provide a font API.

HPA-378 introduced the scene-local `UIScreenHost`. HPA-377 components may later be hosted in its layers, but they do not call, locate, or depend on it.

## 3. Goals

1. Represent the approved palette, typography, spacing, geometry, opacity, focus, semantic states, bars, scrims, and motion rules in reusable Godot resources.
2. Provide one canonical `Theme` with stable type-variation names and sufficient base native-control coverage for the first migrations.
3. Prevent downstream migrations from reintroducing screen-local palette values or repeated `StyleBoxFlat` definitions for covered controls.
4. Provide only the reusable components proven necessary by the first migrations.
5. Keep component APIs presentation-oriented and independently instantiable without application-owned singletons, autoloads, or `UIScreenHost`.
6. Define one deterministic compact-propagation path instead of allowing nested components to infer conflicting modes.
7. Demonstrate all supported states, typography roles, long-text behaviour, focus treatment, surface layering, stat edge cases, native controls, and reduced-motion variants in one isolated scene.
8. Validate the foundation at every approved viewport and aspect ratio.
9. Provide deterministic resource, mapping, behaviour, and responsive-layout tests.
10. Hand persistence and queue/lifecycle concerns to named downstream owners instead of silently expanding this foundation task.

## 4. Non-goals

HPA-377 does not:

- set the shared Theme as the project-global custom Theme;
- restyle or migrate existing production screens;
- modify `MainMenu.tscn`, `Game.tscn`, or production-root ownership;
- change `UIScreenHost` stack, pause, input, cursor, HUD, cancel, or focus-restoration policy;
- add a global UI manager, Theme autoload, input-device autoload, or event bus;
- create inventory-slot, equipment-slot, battle-card, save-card, dialogue-choice, shop-row, or puzzle-specific abstractions;
- implement toast/reward queueing, deduplication, timeout selection, stacking, or cross-transition retention;
- own asynchronous operations from buttons;
- read `GameManager`, `SaveManager`, `SettingsManager`, `RecoveryChest`, or other application/domain singletons;
- add or migrate settings serialization;
- add a persisted reduced-motion preference or player-facing Settings control;
- produce additional final art beyond HPA-374;
- add touch-first, portrait, or mobile layouts;
- make pixel-golden screenshots the primary correctness gate;
- implement the seal-shaped short-confirmation flow owned by HPA-386.

## 5. Handoffs

### 5.1 Reduced-motion persistence — HPA-541

HPA-377 supplies:

- named normal and reduced-motion profiles;
- explicit `ReducedMotion` component inputs;
- deterministic reduced-motion tests;
- a non-persisted showcase toggle.

HPA-541 owns:

- `SettingsData.ReducedMotionEnabled`;
- backward-compatible persistence and settings-version handling;
- the player-facing Settings control;
- Main Menu and Game root propagation;
- updating already-presented and newly-presented components after Apply.

Until HPA-541 lands, production callers default `ReducedMotion` to `false`. Tests and the showcase set it explicitly.

### 5.2 Toast and reward queue — HPA-386

HPA-377 supplies `SiriusToastShell`, its semantic visual states, motion inputs, and fixtures.

HPA-386 owns:

- deterministic queue order;
- duplicate/re-entrant enqueue protection;
- timeout and acknowledgement policy;
- stacking position and safe-frame avoidance;
- lifecycle across brief transitions and cleanup on new game/return to title;
- host registration in `ToastLayer`;
- reward-specific presentation payloads.

### 5.3 Short confirmation seals — HPA-386

`SiriusModalShell` v1 is a rectangular observatory plate for information-heavy content, settings-like content, warnings, and errors. HPA-386 owns the short octagonal/circular confirmation presentation described by HPA-373, composed from shared seal, focus, button, and host primitives. It is not a `SiriusModalShell` size variant.

## 6. Architecture and file layout

Use a resource-first Theme with thin scene/controller components.

```text
resources/ui/theme/
└── SiriusTheme.tres

scripts/ui/theme/
├── SiriusThemeTypes.cs
├── SiriusUiTypes.cs
├── SiriusUiMetrics.cs
└── SiriusMotion.cs

scripts/ui/components/
├── SiriusActionButton.cs
├── SiriusPanel.cs
├── SiriusStatBar.cs
├── SiriusInputHint.cs
├── SiriusContextPrompt.cs
├── SiriusToastShell.cs
├── SiriusModalShell.cs
└── SiriusFocusHalo.cs

scenes/ui/components/
├── SiriusStatBar.tscn
├── SiriusInputHint.tscn
├── SiriusContextPrompt.tscn
├── SiriusToastShell.tscn
├── SiriusModalShell.tscn
└── SiriusFocusHalo.tscn

scenes/ui/showcase/
└── SiriusUiShowcase.tscn

scripts/ui/showcase/
└── SiriusUiShowcase.cs

tests/ui/theme/
tests/ui/components/
tests/ui/showcase/

docs/ui/hpa-377/
└── README.md
```

Direct subclasses with one visual region may remain code-first. Every multi-node composite receives a `.tscn` scene so its structure is inspectable in the editor. `SiriusStatBar` and `SiriusInputHint` are therefore scene-authored composites, not hidden runtime node builders.

### 6.1 Theme integration boundary

HPA-377 assigns `SiriusTheme.tres` only to:

- `ThemedPreviewRoot` in the showcase;
- component fixture roots used by tests.

The integration guide supports two downstream paths:

1. assign the Theme to an isolated screen root when migrating that screen;
2. assign the Theme to a `UIScreenHost`-owned control branch when HPA-379 integrates a root.

Both paths use the same Theme. No screen creates a forked compact, light, dark, or domain-specific Theme.

### 6.2 Ownership of values

`SiriusTheme.tres` owns:

- approved font resources and font variations;
- colours and control-state colours;
- style boxes, including texture-backed styles;
- scrim styles;
- content and expand margins;
- border widths, radii, and shadows;
- opacity encoded in visual resources;
- native hover, pressed, hover-pressed, focus, disabled, selected/toggled, and semantic states;
- base native-control styles and separation constants.

`SiriusUiMetrics` owns arithmetic/layout values:

- `Space4`, `Space8`, `Space12`, `Space16`, `Space24`, `Space32`, `Space48`;
- safe margins and compact breakpoint;
- ultrawide content maximum;
- target, slot, and Ignition sizes;
- modal width classes;
- tooltip maximum widths;
- approved verification viewport sizes.

`SiriusMotion` owns:

- named duration classes;
- easing choices;
- normal versus reduced-motion resolution;
- transform/pulse/flash permissions;
- entry/exit relationships.

No parallel runtime colour-token catalogue is introduced. HPA-373 token names remain documentation identifiers for traceability.

## 7. Closed public types

`SiriusUiTypes.cs` defines the complete shared semantic sets:

```csharp
public enum SiriusActionButtonVariant
{
    Primary,
    Secondary,
    Tertiary,
    Warning,
    Destructive,
    Ignition
}

public enum SiriusPanelSurface
{
    Content,
    Feature,
    HudPlate,
    TelemetryCallout,
    CatalogueRail,
    Modal,
    Warning,
    Error
}

public enum SiriusUiSeverity
{
    Info,
    Success,
    Warning,
    Error,
    Destructive
}

public enum SiriusModalSizeClass
{
    Small,
    Medium,
    Large
}

public enum SiriusStatBarKind
{
    Health,
    Mana,
    Experience,
    AutomaticAction
}
```

Every enum value maps exhaustively to one Theme variation, icon, or layout rule. Unknown programmatic enum values throw `ArgumentOutOfRangeException` and fail tests; they do not silently default. Missing optional asset files are a separate runtime-fallback case and log once before using a readable fallback.

## 8. Theme contract

### 8.1 Palette

| Role | HPA-373 token | Value |
| --- | --- | --- |
| Deep backdrop | `night-1000` | `#050714` |
| Base surface | `night-900` | `#0D1530` |
| Raised surface | `indigo-800` | `#18234A` |
| Interactive indigo | `indigo-700` | `#27366C` |
| Primary text | `moon-50` | `#F7F5FF` |
| Secondary text | `moon-200` | `#C7CEE8` |
| Muted text | `moon-400` | `#8F9AB8` |
| Magic and focus | `cyan-400` | `#62DCFF` |
| Primary and reward | `gold-300` | `#F5D784` |
| Strong gold | `gold-500` | `#DFAE43` |
| Arcane action | `magenta-400` | `#D96CC2` |
| Success | `success-400` | `#68D6A3` |
| Warning | `warning-400` | `#F1B85B` |
| Danger/destructive | `danger-400` | `#F16D83` |

Selection remains gold. Keyboard/gamepad focus remains cyan. Selection and focus may coexist, and no semantic state is communicated by colour alone.

### 8.2 Typography variations and micro-rules

`SiriusThemeTypes` exposes these `Label` variations:

```text
SiriusWordmark
SiriusWordmarkCompact
SiriusScreenTitle
SiriusScreenTitleCompact
SiriusEntityName
SiriusEntityNameCompact
SiriusSectionTitle
SiriusSectionTitleCompact
SiriusBody
SiriusBodyCompact
SiriusHudBody
SiriusHudBodyCompact
SiriusMetadata
SiriusMetadataCompact
SiriusTelemetry
SiriusNumeric
SiriusNumericCompact
```

Font resources:

| Role | Resource |
| --- | --- |
| Wordmark/major fantasy heading | `res://assets/fonts/cinzel/Cinzel-Variable.ttf`, weight 600 |
| Body/localized text | `res://assets/fonts/noto_sans/NotoSans-Regular.ttf` |
| Controls/compact labels | `res://assets/fonts/noto_sans/NotoSans-Medium.ttf` |
| Emphasis/headings | `res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf` |
| Numeric/telemetry | `res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf` |

Sizes:

| Role | Standard | Compact |
| --- | ---: | ---: |
| Wordmark | 44 | 30 |
| Screen title | 32 | 24 |
| Entity name | 24 | 18 |
| Section title | 20 | 17 |
| Body/HUD/essential control | 16 | 14 |
| Metadata/input hint | 14 | 12 |
| Telemetry | 12 | 12 |

Rules:

- `SiriusBody*` uses approximately 1.4 line height.
- `SiriusHudBody*` uses approximately 1.25 line height.
- `SiriusTelemetry` is intentionally 12 px in both modes; it has no compact variation.
- Telemetry strings are uppercase content and use tracked `FontVariation` spacing; essential instructions never use telemetry styling.
- `SiriusNumeric*` uses Noto Sans Mono and tabular figures by design.
- Cinzel is restricted to the wordmark and major fantasy headings. Body and numeric roles never use it.
- The Cinzel `FontVariation` includes Noto Sans as its explicit glyph fallback. Representative fallback glyphs are covered by tests.
- Long localized text wraps or expands its container rather than shrinking below role minimums.

### 8.3 Universal interactive states

| State | Contract |
| --- | --- |
| Normal | Indigo surface with 1 px muted border |
| Hover | Brighter surface and restrained cyan edge light |
| Pressed | Darker fill and 1 px visual depression |
| Hover-pressed | Pressed geometry with hover emphasis |
| Focus | Independent 2 px cyan outer ring/halo; does not alter layout |
| Selected/toggled | Persistent 2 px gold treatment and non-colour marker |
| Disabled | 45% visual/icon/text opacity, no glow, readable reason via component/caller detail |
| Warning | Amber icon/border plus explicit warning text |
| Destructive | Rose icon/border; filled danger only for final confirmation |

Minimum targets are 44×44 standard and 40×40 compact. Disabled controls remain discoverable when the interaction pattern requires an explanation, but activation stays unavailable.

### 8.4 Button variations

| Enum | Theme variation | Use |
| --- | --- | --- |
| `Primary` | `SiriusPrimaryButton` | Conventional dominant form/decision action |
| `Secondary` | `SiriusSecondaryButton` | Ordinary supporting action |
| `Tertiary` | `SiriusTertiaryButton` | Quiet text/ghost action |
| `Warning` | `SiriusWarningButton` | Non-destructive caution action |
| `Destructive` | `SiriusDestructiveButton` | Destructive action, outlined until final confirmation |
| `Ignition` | `SiriusIgnitionButton` | Decisive spatial commitment only |

Each variation supplies `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, and `disabled` resources. Toggle buttons use `ButtonPressed` for selection; `hover_pressed` is the pointer-hover treatment while selected. Focus remains an independent overlay.

#### 8.4.1 Ignition authoring contract

The shipped `ignition_seal.png` is a 192×192, open-centre, uniform-scale ornament. It must not be stretched into an arbitrary rectangular nine-patch.

Geometry:

- preferred control size: 96×96 standard, 80×80 compact;
- 44×44/40×40 remains the absolute accessibility floor, not the normal Ignition size;
- the control remains square;
- standard content margins: 20 px on all sides, leaving a 56×56 safe centre;
- compact content margins: 16 px, leaving a 48×48 safe centre;
- standard expand margins: 4 px; compact: 3 px;
- pressed state shifts content downward by 1 px and reduces expansion by 1 px.

Style topology:

- `normal`, `hover`, `pressed`, `hover_pressed`, and `disabled` are separate `StyleBoxTexture` resources that reference the same `ignition_seal.png`;
- texture margins remain zero and horizontal/vertical stretch mode uses uniform whole-texture stretching inside the square control, avoiding sliced-ring distortion;
- visual states use per-style `modulate_color`, not generated derivative files;
- normal uses the approved gold at approximately 92% opacity;
- hover increases luminance and opacity;
- pressed/hover-pressed use strong gold plus the 1 px depression;
- disabled uses 45% opacity and no glow.

Content:

- the label is centred and may wrap to at most two lines;
- an optional semantic icon uses `UiIconSize.Metadata`; Feature-size icons are not placed inside the seal with a label;
- the control never shrinks text below 16 px standard or 14 px compact;
- when localized text cannot fit the safe centre, the caller must use a conventional Primary/plate action instead of truncating or stretching the seal.

Focus:

- Ignition always uses a separate cyan `StyleBoxTexture` focus overlay referencing `focus_halo.png`;
- focus expansion is 6 px standard and 5 px compact;
- the focus overlay is independent from gold commitment/selection and may coexist with it.

Fallback:

- missing seal or focus texture logs once;
- the button falls back to `SiriusPrimaryButton` while preserving its label, focusability, minimum hit target, disabled reason, and activation contract;
- resource tests make this fallback unreachable in a normal build.

### 8.5 Surfaces and scrims

`SiriusPanelSurface` maps to:

```text
Content          -> SiriusContentPanel
Feature          -> SiriusFeaturePanel
HudPlate         -> SiriusHudPlate
TelemetryCallout -> SiriusTelemetryCallout
CatalogueRail    -> SiriusCatalogueRail
Modal            -> SiriusModalPanel
Warning          -> SiriusWarningPanel
Error            -> SiriusErrorPanel
```

Shared geometry:

- slot radius 4;
- control radius 8;
- panel radius 12;
- feature-panel radius 16;
- normal border 1;
- focus/selected border 2;
- content panel opacity 90%;
- HUD plate opacity 82%;
- modal opacity 96%.

The Theme also defines `Panel` variations:

- `SiriusScrim`: `night-1000` at 58% opacity;
- `SiriusChildScrim`: `night-1000` at 72% opacity.

A host or caller creates a full-rect scrim sibling beneath the modal content. `SiriusModalShell` does not draw, size, or own the scrim. Toasts never use a scrim.

### 8.6 Stat bars

| Kind | Theme variation | Icon | Fill |
| --- | --- | --- | --- |
| `Health` | `SiriusHpBar` | `UiIconId.Health` | danger/rose |
| `Mana` | `SiriusMpBar` | `UiIconId.Mana` | cyan |
| `Experience` | `SiriusExpBar` | `UiIconId.Experience` | gold |
| `AutomaticAction` | `SiriusAutomaticActionBar` | appropriate action/status icon | magenta |

`SiriusInvalidBar` is a presentation state used for invalid maximums, not an additional `SiriusStatBarKind`.

Every bar retains a visible track and numeric/text value. Low, overflow, negative, and invalid states add explicit text or marker feedback rather than colour alone.

### 8.7 Native-control coverage tiers

The Theme is not limited to bespoke components. It provides the following base coverage so first migrations do not immediately reintroduce local style resources.

**Tier A — required and demonstrated in the showcase**

- Label roles;
- Button and `SiriusActionButton` variants;
- Panel/PanelContainer and scrims;
- ProgressBar;
- LineEdit;
- OptionButton and its PopupMenu;
- TabBar and TabContainer;
- CheckBox;
- HSlider;
- ScrollContainer, HScrollBar, and VScrollBar;
- TooltipPanel and TooltipLabel.

**Tier B — styled base types in the same Theme, no dedicated component**

- CheckButton;
- VSlider;
- SpinBox;
- TextureButton;
- MenuButton;
- HSplitContainer and VSplitContainer;
- embedded Window, AcceptDialog, and ConfirmationDialog chrome needed during migration compatibility.

Tier B must be visually coherent and focus-visible, but it does not require a dedicated showcase section for every subtype.

**Tier C — deferred until a consumer proves requirements**

- ItemList;
- Tree;
- RichTextLabel-specific content styling;
- FileDialog;
- ColorPicker;
- GraphEdit;
- inventory slots, save rows/cards, battle nodes, and other domain-specific composites.

A Tier C consumer extends the central Theme or extracts a proven component in its owning migration ticket. It does not add a screen-local palette or duplicate shared style boxes.

## 9. Compact propagation and metrics

### 9.1 Single-authority compact algorithm

Compact mode has one authority per screen or showcase preview branch:

1. The root computes `compact = SiriusUiMetrics.IsCompact(availableSafeFrameSize)` once whenever its safe-frame size changes.
2. The root sets `Compact` on every shared component in that branch.
3. The root assigns standard or compact Theme variations to free-standing Labels it owns.
4. A shared component with nested Labels assigns the matching variations to its own descendants whenever `Compact` changes.
5. Components do not independently recalculate compact mode from their own rectangle, parent, or viewport.
6. A nested host/screen begins a new branch only when it owns an independently sized safe frame; otherwise it inherits the parent decision.

This prevents parent/child disagreement and preserves one Theme. `Compact` changes typography variation, target/minimum size, spacing, content arrangement, and component-specific dimensions where documented. It does not select another Theme.

`SiriusPanel` does not expose `Compact`; surface styling is mode-independent. Its layout owner chooses standard/compact spacing and dimensions.

### 9.2 Metrics

`SiriusUiMetrics` defines:

```text
Space4 / Space8 / Space12 / Space16 / Space24 / Space32 / Space48
Reference viewport: 1280×720
Compact threshold: width < 800 or height < 450
Standard safe margin: 24
Compact safe margin: 12
Ultrawide content maximum: 1600
Standard minimum target: 44×44
Compact minimum target: 40×40
Standard slot: 56×56
Compact slot: 48×48
Ignition standard: 96×96
Ignition compact: 80×80
Small modal: 420
Medium modal: 640
Large modal: 960
Modal maximum: 90% of viewport
Tooltip standard maximum: 360
Tooltip compact maximum: 280
```

Approved validation sizes:

- 640×360;
- 1024×768;
- 1280×720;
- 1440×900;
- 1920×1080;
- 2560×1080;
- 2560×1440.

## 10. Motion policy

`SiriusMotion` defines:

| Profile | Duration | Easing |
| --- | ---: | --- |
| Control feedback | 120 ms | quadratic out |
| Callout/catalogue entry | 220 ms | cubic out |
| Callout/catalogue exit | 180 ms | quadratic in |
| Screen transition | 280 ms | cubic in/out |
| Orrery transformation | 400 ms maximum | cubic in/out |
| Reduced-motion opacity | 100 ms maximum | linear |

Callout/catalogue exit is shorter than entry while remaining inside HPA-373’s 180–240 ms deploy/retract range. No silent timing deviation is introduced.

Reduced motion:

- replaces rotation, translation, scaling, unfolding, parallax, flashes, and travelling pulses;
- uses a static final state or opacity transition no longer than 100 ms;
- preserves state, ordering, input availability, and completion signals;
- removes continuous loops except no state-carrying motion is introduced by HPA-377.

Components receive reduced-motion state explicitly and never read `SettingsManager`.

## 11. Core component design

### 11.1 `SiriusActionButton`

Base: `Button`.

Public API:

```text
Variant : SiriusActionButtonVariant
IconId : UiIconId?
IconSize : UiIconSize = UiIconSize.Default
ShowIcon : bool
Selected : bool
Loading : bool
LoadingText : string = "Loading…"
DisabledReason : string
UseFinalDestructiveTreatment : bool
Compact : bool
ReducedMotion : bool
```

Variant mapping is exhaustive. Unknown enum values throw.

Selection:

- `Selected` maps to `ToggleMode`/`ButtonPressed`;
- setting Loading never clears or toggles `ButtonPressed`;
- focus remains independently visible while selected.

Loading uses a replace-and-restore model:

1. On `Loading = true`, snapshot the current `Text`, icon, icon visibility, and `Disabled` value.
2. Replace `Text` with `LoadingText`; keep the existing static icon unless no icon was configured.
3. Force `Disabled = true`.
4. Suppress activation and prevent `ButtonPressed` from changing.
5. Do not show a spinner or looping animation.
6. On `Loading = false`, restore the snapshot exactly.

Changing `Text`, icon, or `Disabled` while Loading is active is unsupported and logs a diagnostic; callers apply new content after Loading ends.

`DisabledReason` is exposed through tooltip/detail integration and remains readable without relying on opacity. Ignition follows section 8.4.1. The component does not accept `Task`, invoke domain commands, manage navigation, or load ornaments procedurally.

### 11.2 `SiriusPanel`

Base: `PanelContainer`.

```text
Surface : SiriusPanelSurface
```

It maps one closed enum to one Theme variation. It contains no gameplay data, child-loading policy, compact inference, or one-off colour overrides.

### 11.3 `SiriusModalShell`

Scene-authored composite:

```text
SiriusModalShell
└── SiriusPanel
    └── RootLayout
        ├── Header
        │   ├── SeverityIcon
        │   └── Title
        ├── BodyScroll
        │   └── BodyHost
        └── ActionsHost
```

Public API:

```text
Title : string
Severity : SiriusUiSeverity
SizeClass : SiriusModalSizeClass
Compact : bool
BodyHost : Control
ActionsHost : Control
ShowCloseAffordance : bool
ReducedMotion : bool
CloseRequested signal
```

The shell:

- applies small/medium/large width rules and 90% viewport maximum;
- uses viewport-minus-compact-margin sizing in compact mode;
- keeps title/actions fixed while the body scrolls;
- maps severity to icon and semantic panel treatment;
- emits `CloseRequested` without dismissing itself.

It does not create a scrim, register with the host, choose initial focus, intercept Cancel, pause the tree, or select a domain action.

### 11.4 `SiriusStatBar`

Scene-authored composite containing icon, label, `ProgressBar`, numeric value, and state marker.

```text
Kind : SiriusStatBarKind
Current : double
Maximum : double
Label : string
ShowNumericValue : bool
LowThreshold : double = 0.25
Compact : bool
```

Rules:

- visual fill clamps to `[0, 1]`;
- displayed numeric values preserve caller values;
- `Maximum <= 0` produces invalid state and zero fill;
- `Current < 0` clamps visual fill to zero and exposes an error marker;
- `Current > Maximum` fills to 100%, preserves the overflow number, and exposes an overflow marker;
- low/overflow/negative/invalid states are not colour-only;
- unknown kinds throw.

The component performs presentation validation only and never normalizes domain state.

### 11.5 `SiriusInputHint`

Scene-authored composite around the existing `InputHintPresenter`.

```text
Prompt : string
Actions : StringName[]
Compact : bool
ActiveDevice : UiInputDevice
Refresh()
```

It pairs a device glyph with readable binding text, supports fallback actions, displays `Unbound`, and updates standard/compact nested label roles. It observes input only while visible and adds no global device singleton.

### 11.6 `SiriusContextPrompt`

```text
IconId : UiIconId?
IconSize : UiIconSize = UiIconSize.Default
Prompt : string
Actions : StringName[]
Compact : bool
```

It composes a semantic icon, prompt text, and `SiriusInputHint`. It does not discover interactables, decide validity, or invoke world interaction.

### 11.7 `SiriusToastShell`

```text
Severity : SiriusUiSeverity
Title : string
Message : string
Compact : bool
ReducedMotion : bool
```

It owns semantic visual presentation and entry/exit visuals only. HPA-386 owns queue, dedupe, timeout, stacking, host registration, and lifecycle.

### 11.8 `SiriusFocusHalo`

A non-layout overlay for geometric controls.

```text
Target : Control
VisibleWhenFocused : bool
VisibleWhenSelected : bool
Selected : bool
ReducedMotion : bool
```

It:

- loads focus/selection ornaments through `UiArtCatalog`;
- follows target bounds only while visible and while the target is valid/in-tree;
- updates from target focus, resize, visibility, and tree lifecycle signals;
- does not run an always-on `_Process` loop for inactive halos;
- cannot move siblings or change target minimum size;
- disconnects safely when the target exits the tree.

Ignition’s standard focus is Theme-owned; `SiriusFocusHalo` remains available for other geometric controls whose normal Theme focus box is insufficient.

## 12. Showcase design

`SiriusUiShowcase.tscn` is an isolated development scene and is not linked from production navigation.

### 12.1 Composition

```text
SiriusUiShowcase
├── ShowcaseToolbar
│   ├── ViewportSizeSelector
│   ├── BackgroundSelector
│   ├── InputDeviceInstructions
│   └── ReducedMotionToggle
└── PreviewFrame
    └── SubViewportContainer
        └── SubViewport
            └── ThemedPreviewRoot
                └── ResponsiveScroll
                    └── ShowcaseSections
```

The root computes compact mode once from the preview safe-frame size and propagates it using section 9.1.

Background options are deterministic:

1. solid `night-1000` dark background;
2. solid `moon-50` light background;
3. retained main-menu scenic background at `res://assets/sprites/ui/ui_main_menu_background.png`;
4. retained battle scenic background at `res://assets/sprites/ui/ui_battle_background.png`.

The showcase does not rely on external screenshots or environment-dependent colours.

Localization stress fixtures include:

- a German-style action label approximately twice the English reference length;
- a 240-character multi-line body paragraph;
- a 48-character unbroken metadata token to verify explicit overflow handling.

These fixtures test expansion, wrapping, scroll behaviour, and permitted metadata truncation without assuming unverified CJK glyph coverage.

### 12.2 Required sections

1. Palette, scrims, and surface layering over all background options.
2. Standard and compact typography, line-height, tracking, numeric, and fallback roles.
3. Short, long, wrapped, and localization-stress text.
4. Every action-button variant and all native button states, including `hover_pressed`.
5. Primary and Ignition side by side at standard/compact size.
6. Selected-plus-focused state.
7. Tier A native controls, including CheckBox, HSlider, OptionButton/PopupMenu, TabContainer, ScrollBars, and tooltips.
8. Tier B compatibility-control sample group.
9. HP, MP, EXP, and automatic-action bars.
10. Negative, low, medium, full, overflow, and invalid stat values.
11. Keyboard, mouse, gamepad, fallback, and unbound input hints.
12. Context prompt examples.
13. Info, success, warning, error, and destructive toast shells.
14. Small, medium, and large observatory-plate modal shells over both scrim variants.
15. Native focus ring and ornament focus-halo examples.
16. Loading snapshot/restore behaviour.
17. Normal and reduced-motion transitions.

At standard sizes sections use a multi-column grid where space permits. Compact mode reflows to one column inside a `ScrollContainer`. Primary examples must not require horizontal scrolling.

## 13. Testing strategy

### 13.1 Theme resource contract

`SiriusThemeResourceTest` verifies:

- the Theme loads;
- every required type variation exists with the expected built-in base type;
- Tier A and Tier B base types contain their required state resources;
- every action-button variation defines `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, and `disabled`;
- progress bars define track/fill resources;
- scrims resolve to 58%/72% approved opacity;
- selection and focus resources are distinct;
- minimum typography sizes, line spacing, telemetry tracking, numeric font, and decorative-font restrictions match the contract;
- Cinzel’s explicit Noto Sans fallback resolves representative glyphs;
- every closed enum value maps exactly once and no unknown values silently default;
- `SiriusThemeTypes` contains no duplicates or untested string-only names;
- each font referenced by the Theme resolves at the approved direct path.

The existing `UiArtCatalogTest.ApprovedFonts_LoadAsFontFiles` remains the complete font-inventory test. HPA-377 tests only the Theme’s actual assignments.

### 13.2 Ignition tests

Tests verify:

- every non-focus state references the approved 192×192 seal texture;
- state `modulate_color` values differ as documented;
- square standard/compact sizes and safe-centre margins resolve correctly;
- pressed geometry uses the 1 px depression;
- focus references the cyan focus halo and expands without layout shift;
- `hover_pressed` exists;
- missing texture falls back to a labelled/focusable Primary button and logs once.

### 13.3 Metrics, compact, and motion tests

Pure tests verify:

- all spacing constants;
- compact breakpoint boundaries;
- approved viewport list;
- safe margins and ultrawide maximum;
- modal, tooltip, target, slot, and Ignition sizes;
- one root compact decision propagates to free labels and nested component labels;
- components do not recalculate compact mode independently;
- entry 220 ms / exit 180 ms remain inside HPA-373 range and exit is shorter;
- reduced-motion output never exceeds 100 ms and permits only static/opacity presentation.

### 13.4 Component tests

Each component is instantiated without:

- `GameManager` static singleton;
- `SaveManager`, `SettingsManager`, or `RecoveryChest` autoloads;
- scene-local `UIScreenHost`.

Tests cover:

- closed variant/surface/severity/size/kind mapping;
- selected-plus-focused behaviour;
- loading snapshot, forced disabled state, activation suppression, selection preservation, and exact restoration;
- disabled reason;
- final destructive treatment;
- modal severity/size and no-scrim ownership;
- stat-bar edge cases;
- input-device/binding changes;
- context-prompt composition;
- toast severity without queue ownership;
- focus-halo lifecycle and no inactive `_Process` tracking;
- reduced-motion presentation.

### 13.5 Showcase runtime tests

Instantiate the showcase in a `SubViewport` at:

- 640×360;
- 1024×768;
- 1280×720;
- 1440×900;
- 1920×1080;
- 2560×1080;
- 2560×1440.

Structural assertions verify:

- all scenes/resources and required sections load;
- compact state resolves once and propagates consistently;
- safe margins, ultrawide maximum, and target sizes hold;
- long fixtures wrap/scroll according to policy;
- required focus neighbours/targets are valid;
- selected and focus coexist;
- Primary and Ignition remain structurally distinct;
- Tier A controls have coherent focus/disabled/interaction states;
- both scrim variants layer correctly;
- the reduced-motion toggle changes explicit component policy without settings persistence;
- component roots instantiate without application dependencies;
- no normal-flow HPA-374 missing-resource warning occurs;
- headless runs verify structure and layout without pixel equality.

Manual verification covers subjective glow, seal composition, scenic contrast, animation feel, and pointer/gamepad interaction.

## 14. Documentation contract

`docs/ui/hpa-377/README.md` documents:

1. canonical Theme path and HPA-373 source version/blob/date;
2. isolated-screen and `UIScreenHost` opt-in paths;
3. native-control coverage tiers;
4. stable type variations and closed enum mappings;
5. Theme values versus arithmetic metrics;
6. compact-propagation algorithm;
7. spacing constants and responsive dimensions;
8. reduced-motion capability and HPA-541 persistence handoff;
9. `SiriusToastShell` boundary and HPA-386 queue handoff;
10. scrim ownership;
11. direct font references and HPA-374 artwork APIs;
12. component APIs and fallback behaviour;
13. prohibited patterns: local palettes, repeated shared style boxes, singleton access, component-owned host/lifecycle, global Theme activation, and premature domain abstractions.

## 15. Implementation order

1. Add `SiriusThemeTypes`, closed `SiriusUiTypes`, metrics, motion contracts, and failing pure/resource tests.
2. Author `SiriusTheme.tres` with fonts, typography micro-rules, scrims, Tier A coverage, and button/panel/bar variations.
3. Add Tier B base native-control styling and its resource-contract tests.
4. Implement `SiriusActionButton`, Loading restoration, and Ignition texture/focus/fallback contract.
5. Implement `SiriusPanel`, `SiriusModalShell`, scrim fixtures, and closed surface/severity/size mappings.
6. Implement scene-authored `SiriusStatBar` and `SiriusInputHint`.
7. Implement `SiriusContextPrompt`, `SiriusToastShell`, and `SiriusFocusHalo`.
8. Build showcase backgrounds, native-control tiers, compact propagation, stress fixtures, and reduced-motion toggle.
9. Add all component and viewport-matrix runtime tests.
10. Add integration documentation and explicit HPA-541/HPA-386 handoffs.
11. Run focused suites, build, and full repository tests.

After this design is explicitly approved, its status changes to `Approved design` and the file-by-file TDD implementation plan may begin.

## 16. Rejected alternatives

### 16.1 Project-global Theme activation

Rejected because it would restyle legacy screens immediately and absorb HPA-379 and multiple migration tickets.

### 16.2 Runtime Theme builder or Theme autoload

Rejected because it duplicates Godot’s authored resource model, adds initialization/lifecycle concerns, and impairs editor inspection.

### 16.3 Parallel C# colour catalogue

Rejected because values would drift from `SiriusTheme.tres`. Code owns only arithmetic metrics, closed semantic types, and motion policy.

### 16.4 Multiple compact/light/dark Themes

Rejected because one root compact decision and one central Theme provide deterministic composition without resource forks.

### 16.5 Comprehensive domain component library

Rejected because inventory, battle, save/load, dialogue, and other APIs are not yet proven.

### 16.6 Pixel-golden primary tests

Rejected because fonts, render backends, and headless execution make them fragile. Structural contracts and manual visual review divide objective and subjective validation.

### 16.7 Persisted reduced motion in HPA-377

Rejected because it would add settings schema, serialization, Settings-screen, and root-binding concerns. HPA-541 owns the complete preference.

### 16.8 Queueing inside `SiriusToastShell`

Rejected because queue/lifecycle policy belongs to HPA-386 and `UIScreenHost` integration, not a visual shell.

### 16.9 Ignition as a stretched rectangular nine-patch

Rejected because the asset is a square open-centre seal. The control remains square and uses whole-texture state styles.

### 16.10 Silent enum defaults

Rejected because they hide contract drift. Programmatic unknown values fail fast; only missing optional assets use logged visual fallback.

## 17. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Type-variation spelling drift | One `SiriusThemeTypes` catalogue plus resource tests |
| Closed enums diverge from Theme | Exhaustive mapping tests for every enum |
| First migrations reintroduce local styles | Tier A/B native-control coverage in the canonical Theme |
| Compact parent/child disagreement | One root decision and explicit propagation algorithm |
| Ignition ring is distorted or unreadable | Square sizing, whole-texture topology, safe-centre margins, explicit fallback |
| Focus is lost on geometric controls | Independent focus overlay/halo with no layout shift |
| Scrim values drift | Theme-owned 58%/72% variations; lifecycle owner only instantiates them |
| Loading destroys caller state | Snapshot/restore contract and mutation diagnostics |
| Typography micro-rules disappear | Theme/resource tests for line spacing, tracking, fallback, and font roles |
| Reduced motion implies nonexistent persistence | Explicit HPA-541 boundary |
| Toast shell gains hidden lifecycle responsibility | Explicit HPA-386 boundary |
| Headless tests become flaky | Structural assertions, not pixel equality |
| Status metadata becomes a false blocker | Current doc uses Proposed/Approved states; stale HPA-373 header is non-authoritative debt |

## 18. Acceptance mapping

| HPA-377 acceptance criterion | Design coverage |
| --- | --- |
| Approved palette, typography, spacing, state, and motion rules represented | Sections 8–10 |
| Common controls avoid repeated per-scene styles | Tier A/B native coverage and canonical Theme |
| Showcase demonstrates every supported state and long-text behaviour | Section 12 |
| Focus is clear for keyboard/gamepad | Universal focus, Ignition focus, FocusHalo, native fixtures |
| APIs are small and avoid gameplay singletons | Sections 7 and 11; component-isolation tests |
| Components work at approved viewport sizes | Section 13.5 |
| No premature domain abstractions | Non-goals and rejected comprehensive library |
| Tests cover Theme loading and variations | Sections 13.1–13.5 |
| Reduced-motion variants exist where practical | Sections 10, 12, and 13; persistence in HPA-541 |

## 19. Completion definition

HPA-377 is complete when:

- `SiriusTheme.tres` is the single canonical Sirius visual resource;
- Tier A and Tier B native controls have tested central styles;
- every closed semantic enum maps exhaustively;
- every Theme font resolves at its approved path and typography micro-rules are tested;
- scrim values and ownership are explicit;
- all approved core components exist as presentation-only controls/scenes;
- Ignition is square, readable, focusable, state-complete, and structurally distinct from Primary;
- compact mode has one tested authority and propagation path;
- Loading preserves/restores caller state and suppresses activation;
- the showcase contains all required states, controls, backgrounds, stress fixtures, and motion modes;
- viewport-matrix structural validation passes;
- no production screen is silently migrated;
- no component depends on application/domain singletons, autoloads, or `UIScreenHost`;
- HPA-541 and HPA-386 handoffs are documented;
- focused tests, build, and full repository tests pass.

Persisted reduced-motion settings are HPA-541 outcomes. Toast/reward queueing and short confirmation seals are HPA-386 outcomes.
