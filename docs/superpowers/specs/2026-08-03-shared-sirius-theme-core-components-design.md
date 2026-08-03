# HPA-377 Shared Sirius Theme, Core Components, and UI Showcase Design

**Status:** Approved architecture; written artifact pending user review  
**Date:** 2026-08-03  
**Issue:** HPA-377  
**Repository:** `cwchanap/Sirius`  
**Runtime:** Godot 4.6, C#/.NET 8, GdUnit4  
**Depends on:** HPA-373 and HPA-374  
**Integrates with:** HPA-378  
**Blocks:** HPA-379

## 1. Summary

HPA-377 implements the approved Sirius visual language as one canonical Godot `Theme` resource, a deliberately small set of presentation-only components, and an isolated UI showcase. It creates the visual foundation required by later screen migrations without restyling or restructuring existing production screens.

The design uses three boundaries:

1. **The Theme owns visual values.** Fonts, colours, style boxes, font sizes, control constants, and native control states live in one authored resource.
2. **Thin components own presentation behaviour.** Loading labels, semantic variants, stat formatting, input hints, modal composition, and focus ornament behaviour live in reusable controls that expose small APIs.
3. **`UIScreenHost` owns presentation lifecycle.** Screen stacking, pause, input blocking, cursor policy, HUD policy, cancellation, and focus restoration remain outside HPA-377.

The shared theme is opt-in during HPA-377. It is assigned to the showcase root and documented for later root integration, but it is not configured as the project-wide default yet. HPA-379 will opt production roots into the theme while proving legacy parity.

## 2. Context

HPA-373 approved the Constellation Orrery visual language:

- deep indigo surfaces;
- cyan focus;
- gold selection and commitment;
- magenta arcane or automatic-action accents;
- Noto Sans for body and controls;
- Noto Sans Mono for compact numeric and telemetry content;
- Cinzel SemiBold for the wordmark and major fantasy headings;
- a `4 / 8 / 12 / 16 / 24 / 32 / 48` spacing scale;
- explicit normal, hover, pressed, focused, selected, disabled, warning, and destructive states;
- minimum target sizes;
- responsive behaviour from 640×360 through ultrawide;
- restrained motion with reduced-motion alternatives.

HPA-374 subsequently shipped the approved font files, semantic icons, input glyphs, ornaments, and effects behind `UiArtCatalog`, `UiIconPresenter`, and `InputHintPresenter`.

Current production scenes still contain repeated local `StyleBoxFlat` resources and runtime style duplication. That code is evidence for the shared foundation, but replacing those definitions belongs to each screen migration. HPA-377 must not partially migrate Inventory, Main Menu, Settings, battle, or other existing screens.

HPA-378 has already introduced the reusable `UIScreenHost`. HPA-377 components may be placed inside host layers later, but they must not call, locate, or depend on the host.

## 3. Goals

1. Represent the approved palette, typography, geometry, opacity, focus, semantic states, bars, and motion rules in reusable Godot resources.
2. Provide one canonical `Theme` with stable type-variation names.
3. Eliminate the need for new screen-local visual style definitions in downstream migrations.
4. Provide only the reusable components proven necessary by the first migrations.
5. Keep component APIs presentation-oriented and independently instantiable without gameplay autoloads.
6. Demonstrate all supported states, typography roles, long-text behaviour, focus treatment, surface layering, and stat edge cases in one isolated scene.
7. Validate the foundation at every approved viewport and aspect ratio.
8. Document how later screens opt into the Theme and consume type variations and HPA-374 assets.
9. Provide deterministic tests for resource loading, variation coverage, component behaviour, and responsive showcase structure.

## 4. Non-goals

HPA-377 does not:

- set the shared Theme as `ProjectSettings.gui/theme/custom`;
- restyle or migrate existing production screens;
- modify `MainMenu.tscn`, `Game.tscn`, or production root ownership;
- change `UIScreenHost` stack, pause, input, cursor, HUD, cancel, or focus-restoration policy;
- add a global UI manager, Theme autoload, input-device autoload, or event bus;
- create inventory-slot, equipment-slot, battle-card, save-card, dialogue-choice, shop-row, or puzzle-specific abstractions;
- implement a toast queue, modal stack, notification lifetime service, or navigation service;
- own asynchronous operations from buttons;
- read gameplay, save, inventory, battle, settings, NPC, or reward singletons;
- change domain rules or runtime flow behaviour;
- produce additional final art beyond the existing HPA-374 contract;
- add touch-first, portrait, or mobile layouts;
- introduce visual-regression screenshot baselines as the primary correctness mechanism.

## 5. Architectural decision

Use a resource-first Theme with thin scene/controller components.

```text
resources/ui/theme/
└── SiriusTheme.tres

scripts/ui/theme/
├── SiriusThemeTypes.cs
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

Simple controls remain C# subclasses or themed stock controls. Composite controls receive `.tscn` scenes so designers can instantiate and inspect their structure in the editor.

### 5.1 Why one authored Theme resource

Godot Theme resources apply to a control and its descendant control branch, and type variations allow semantic roles to inherit from built-in control types. This matches the requirement to share style definitions while keeping each production root responsible for opting in.

`SiriusTheme.tres` is authored and reviewed as a resource. HPA-377 does not generate it at runtime and does not add an editor-time code generator. Contract tests protect the resource from missing variations and accidental drift.

### 5.2 Opt-in integration boundary

HPA-377 assigns `SiriusTheme.tres` only to:

- the showcase root;
- component fixture roots used by tests.

The integration guide documents two supported future uses:

1. assign the Theme to a screen root when migrating one isolated screen;
2. assign the Theme to a `UIScreenHost`-owned control branch when HPA-379 integrates a root.

The Theme must not be configured globally in HPA-377. Global activation would immediately alter legacy controls and expand this task into several migration tickets.

### 5.3 Ownership of values

`SiriusTheme.tres` owns:

- fonts and font sizes;
- control colours;
- style boxes;
- content margins;
- border widths and radii;
- opacity encoded in visual resources;
- native hover, pressed, focus, disabled, selected/toggled, and semantic control states;
- base container and control separation constants.

`SiriusUiMetrics` owns values that Theme resources cannot reliably enforce or that layouts need as arithmetic:

- standard and compact safe margins;
- compact breakpoint;
- ultrawide content maximum;
- minimum target sizes;
- standard and compact slot sizes;
- modal width classes;
- tooltip maximum widths;
- approved verification viewport sizes.

`SiriusMotion` owns:

- named duration classes;
- easing choices;
- reduced-motion resolution;
- entry/exit duration relationships.

No parallel C# colour-token catalogue is introduced. Components query their Theme or select a stable type variation instead of reproducing palette values in code.

## 6. Theme contract

### 6.1 Palette

The resource encodes the approved roles:

| Role | Value |
| --- | --- |
| Deep backdrop | `#050714` |
| Base surface | `#0D1530` |
| Raised surface | `#18234A` |
| Interactive indigo | `#27366C` |
| Primary text | `#F7F5FF` |
| Secondary text | `#C7CEE8` |
| Muted text | `#8F9AB8` |
| Magic and focus | `#62DCFF` |
| Primary and reward | `#F5D784` |
| Strong gold | `#DFAE43` |
| Arcane action | `#D96CC2` |
| Success | `#68D6A3` |
| Warning | `#F1B85B` |
| Danger and destructive | `#F16D83` |

Selection remains gold. Keyboard and gamepad focus remain cyan. Controls that are both selected and focused show both treatments, and semantic state is never communicated by colour alone.

### 6.2 Typography variations

`SiriusThemeTypes` exposes stable `StringName` constants for these `Label` variations:

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
SiriusMetadata
SiriusMetadataCompact
SiriusTelemetry
SiriusNumeric
SiriusNumericCompact
```

Font assignments:

- wordmark and major fantasy headings: Cinzel at weight 600;
- body, controls, metadata, and localized text: Noto Sans;
- numeric values and short telemetry: Noto Sans Mono Medium.

Sizes:

| Role | Standard | Compact |
| --- | ---: | ---: |
| Wordmark | 44 | 30 |
| Screen title | 32 | 24 |
| Entity name | 24 | 18 |
| Section title | 20 | 17 |
| Body and essential control text | 16 | 14 |
| Metadata and input hint | 14 | 12 |
| Telemetry | 12 | 12 |

No role renders below 12 logical pixels. Essential state and actions never use the telemetry role. Long localized text wraps or expands its container instead of shrinking below the role minimum.

### 6.3 Button variations

`Button` variations:

```text
SiriusPrimaryButton
SiriusSecondaryButton
SiriusTertiaryButton
SiriusWarningButton
SiriusDestructiveButton
SiriusIgnitionButton
```

Each applicable variation defines:

- `normal`;
- `hover`;
- `pressed`;
- `focus`;
- `disabled`;
- `hover_pressed`.

Selection uses Godot's toggled/pressed state rather than a separate selected-button class. The selected treatment is gold; the focus style remains independently visible in cyan.

The destructive button remains outlined during ordinary use. A filled danger treatment is reserved for an explicit final destructive confirmation and is exposed by `SiriusActionButton` state rather than by inventing a screen-specific style.

### 6.4 Surface variations

`PanelContainer` variations:

```text
SiriusContentPanel
SiriusFeaturePanel
SiriusHudPlate
SiriusTelemetryCallout
SiriusCatalogueRail
SiriusModalPanel
SiriusWarningPanel
SiriusErrorPanel
```

The first six correspond to the approved surface families. Warning and error variants are semantic treatments of the modal/content surfaces, not new layout concepts.

Shared geometry:

- slot radius: 4;
- control radius: 8;
- panel radius: 12;
- feature-panel radius: 16;
- normal border: 1;
- focus or selected border: 2;
- content-panel opacity: 90%;
- HUD-plate opacity: 82%;
- modal opacity: 96%.

Shadows appear only on raised surfaces. Glow represents focus, selection, hostility, or commitment and is not applied to every border.

### 6.5 Stat-bar variations

`ProgressBar` variations:

```text
SiriusHpBar
SiriusMpBar
SiriusExpBar
SiriusAutomaticActionBar
SiriusInvalidBar
```

Semantic fill colours:

- HP: danger/rose;
- MP: cyan;
- EXP: gold;
- automatic action: magenta.

Every bar keeps a visible track and a text or numeric value. Low-resource state adds explicit text/icon feedback.

### 6.6 Inputs, tabs, and tooltips

The Theme provides focused Sirius styling for:

```text
SiriusLineEdit
SiriusOptionButton
SiriusTabBar
```

Built-in tooltip panel and label theme types are configured directly. Tooltip content is capped by component/layout metrics at:

- 360 px standard;
- 280 px compact.

Tabs use gold for selection and cyan for focus. Tooltip-only information must remain available through focus or a visible detail surface, not mouse hover alone.

### 6.7 Focus

Conventional controls use a 2 px cyan Theme focus style. It must not change layout size or move neighbouring controls.

Geometric controls that cannot express the approved focus treatment with a normal `StyleBox` use `SiriusFocusHalo`. Selection and focus remain independently renderable.

## 7. Responsive and motion policy

### 7.1 Metrics

`SiriusUiMetrics` defines:

```text
Reference viewport: 1280×720
Compact threshold: width < 800 or height < 450
Standard safe margin: 24
Compact safe margin: 12
Ultrawide content maximum: 1600
Standard minimum target: 44×44
Compact minimum target: 40×40
Standard slot: 56×56
Compact slot: 48×48
Small modal: 420
Medium modal: 640
Large modal: 960
Modal maximum: 90% of viewport
```

It also exposes the approved validation sizes:

- 640×360;
- 1024×768;
- 1280×720;
- 1440×900;
- 1920×1080;
- 2560×1080;
- 2560×1440.

`IsCompact(Vector2 viewportSize)` is a pure helper. HPA-377 does not add a global responsive service. Screen roots decide when to reflow and pass compact presentation state to their components.

### 7.2 Motion

`SiriusMotion` defines named profiles:

| Profile | Duration | Easing |
| --- | ---: | --- |
| Control feedback | 120 ms | quadratic out |
| Callout/catalogue entry | 220 ms | cubic out |
| Callout/catalogue exit | 160 ms | quadratic in |
| Screen transition | 280 ms | cubic in/out |
| Orrery transformation | 400 ms maximum | cubic in/out |
| Reduced-motion opacity | 100 ms maximum | linear |

Reduced motion:

- replaces rotation, translation, scaling, unfolding, parallax, flashes, and travelling pulses;
- uses a static final state or an opacity transition no longer than 100 ms;
- preserves all state and timing information;
- does not alter input availability or completion signals.

Components receive reduced-motion state through an explicit property or method. They do not read `SettingsManager` directly. A later screen or root binds the user setting to the component tree.

The loading state is static and semantic: a readable `Loading…` label, disabled activation, and optional static icon. It does not add a continuously looping spinner.

## 8. Core component design

### 8.1 `SiriusActionButton`

Base: `Button`.

Public presentation API:

```text
Variant
IconId / ShowIcon
Selected
Loading
LoadingText
DisabledReason
UseFinalDestructiveTreatment
Compact
ReducedMotion
```

Behaviour:

- maps `Variant` to a stable Theme type variation;
- uses `ToggleMode` and `ButtonPressed` for selection;
- preserves the original label and icon while loading;
- shows `Loading…` or caller-provided loading text;
- disables activation while loading without owning the asynchronous operation;
- maintains readable disabled reason text through tooltip/detail integration;
- enforces the approved minimum target size;
- keeps focus visible while selected;
- applies HPA-374 icons through `UiIconPresenter`.

The component emits normal `Pressed` behaviour. It does not accept `Task`, invoke domain commands, or manage navigation.

### 8.2 `SiriusPanel`

Base: `PanelContainer`.

Public API:

```text
Surface
Compact
```

`Surface` selects one of the approved panel type variations. The component contains no gameplay data, does not load child content, and does not add one-off colour overrides.

### 8.3 `SiriusModalShell`

Composite scene:

```text
SiriusModalShell
└── SiriusPanel
    └── RootLayout
        ├── Header
        │   ├── SeverityIcon
        │   └── Title
        ├── BodyScroll
        │   └── BodyHost
        └── Actions
```

Public API:

```text
Title
Severity
SizeClass
Compact
BodyHost
ActionsHost
ShowCloseAffordance
ReducedMotion
CloseRequested signal
```

Behaviour:

- applies small, medium, or large width rules and the viewport maximum;
- uses viewport-minus-compact-margin sizing in compact mode;
- keeps the title and actions fixed while the body scrolls;
- maps severity to semantic icon and surface treatment;
- exposes composition hosts instead of knowing body/action domain types;
- when the optional close affordance is activated, emits `CloseRequested` and leaves dismissal to the caller or host.

It does not:

- add itself to `UIScreenHost`;
- decide initial focus;
- intercept Cancel;
- dismiss itself;
- pause the tree;
- choose a safe/destructive domain action.

### 8.4 `SiriusStatBar`

Composite control using a `ProgressBar`, icon, label, numeric value, and state marker.

Public API:

```text
Kind
Current
Maximum
Label
ShowNumericValue
LowThreshold
Compact
```

Rules:

- visual fill is clamped to `[0, 1]`;
- the displayed numeric value preserves the caller's real values;
- `Maximum <= 0` produces the invalid state, zero fill, and explicit invalid text/marker;
- `Current < 0` clamps to zero and exposes an error marker;
- `Current > Maximum` fills to 100% but preserves the overflow value and exposes an overflow marker;
- low state defaults to `Current / Maximum <= 0.25`;
- low, overflow, and invalid states are not communicated through colour alone;
- HP, MP, EXP, and automatic-action kinds select the appropriate icon and Theme variation.

The component performs presentation validation only. It does not change or normalize domain statistics.

### 8.5 `SiriusInputHint`

Composite control built around the existing `InputHintPresenter`.

Public API:

```text
Prompt
Actions
Compact
ActiveDevice
Refresh
```

Behaviour:

- pairs a HPA-374 device glyph with readable binding text;
- supports one or more fallback actions;
- updates after relevant keyboard, mouse, or gamepad input;
- displays `Unbound` when no action can be represented;
- switches between standard and compact layouts without changing input bindings.

The component may observe input only while visible. It does not add a new input-device singleton.

### 8.6 `SiriusContextPrompt`

Composite scene:

```text
SiriusContextPrompt
├── SemanticIcon
├── PromptText
└── SiriusInputHint
```

Public API:

```text
IconId
Prompt
Actions
Compact
```

It presents an available contextual action. It does not discover nearby interactables, decide whether an action is valid, or invoke world interaction.

### 8.7 `SiriusToastShell`

Composite scene:

```text
SiriusToastShell
└── SiriusPanel
    └── Row
        ├── SeverityIcon
        └── TextColumn
            ├── OptionalTitle
            └── Message
```

Public API:

```text
Severity
Title
Message
Compact
ReducedMotion
```

It owns visual entry/exit states and semantic presentation only. It does not own:

- queueing;
- deduplication;
- timeout selection;
- stacking position;
- input capture;
- host registration;
- domain acknowledgement.

### 8.8 `SiriusFocusHalo`

A non-layout overlay for geometric controls.

Public API:

```text
Target
VisibleWhenFocused
VisibleWhenSelected
Selected
ReducedMotion
```

Behaviour:

- loads the existing HPA-374 focus-halo and selection-halo ornaments through `UiArtCatalog`;
- follows the target's global bounds;
- renders outside normal layout measurement;
- cannot move siblings or change the target's minimum size;
- allows a gold selection treatment and cyan focus treatment to coexist;
- disconnects signals safely when the target exits the tree.

It is used only where Theme focus style boxes are insufficient.

## 9. Showcase design

`SiriusUiShowcase.tscn` is an isolated development scene and is not linked from production navigation.

### 9.1 Composition

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

The Theme is assigned to `ThemedPreviewRoot`.

The preview viewport can switch among all approved logical sizes without changing the actual test runner or desktop window.

### 9.2 Required sections

1. Palette and surface layering over representative light and dark backgrounds.
2. Standard and compact typography roles.
3. Short, long, wrapped, and localization-stress text.
4. Primary, secondary, tertiary, warning, destructive, ignition, selected, disabled, and loading buttons.
5. Interactive normal, hover, pressed, and focused button examples.
6. Selected-plus-focused state.
7. Line edit, option button, tab, and tooltip treatment.
8. HP, MP, EXP, and automatic-action bars.
9. Stat values at negative, low, medium, full, overflow, and invalid maximum.
10. Keyboard, mouse, gamepad, fallback, and unbound input hints.
11. Context prompt examples.
12. Info, success, warning, error, and destructive toast shells.
13. Small, medium, and large modal shells with long scrolling body content.
14. Native focus ring and ornament focus-halo examples.
15. Normal motion and reduced-motion state transitions.

Hover and pressed are demonstrated through live interactive controls. Deterministic tests validate the required Theme state resources rather than relying on synthetic mouse screenshots.

### 9.3 Responsive behaviour

At standard sizes, sections use a multi-column grid where space permits. At compact sizes, they reflow to one column inside a `ScrollContainer`.

The preview must:

- stay inside the approved safe margin;
- avoid horizontal scrolling for primary examples;
- preserve minimum target sizes;
- keep long text readable;
- keep essential content reachable through vertical scrolling;
- center content inside the 1600 px ultrawide maximum rather than stretching controls to distant edges.

## 10. Testing strategy

### 10.1 Theme resource contract

`SiriusThemeResourceTest` loads `SiriusTheme.tres` and verifies:

- the Theme loads as a non-null `Theme`;
- all committed font files load as `FontFile`;
- every required type variation exists;
- every variation has the expected built-in base type;
- required style boxes, colours, fonts, font sizes, and constants exist;
- button variations include normal, hover, pressed, focus, and disabled resources;
- progress-bar variations include track and fill resources;
- selection and focus resources are distinct;
- minimum typography sizes do not fall below the approved values;
- `SiriusThemeTypes` contains no duplicate values;
- no expected type-variation name exists only as an untested string literal.

### 10.2 Metrics and motion tests

Pure tests verify:

- compact breakpoint boundaries;
- approved viewport list;
- modal width resolution;
- safe-margin resolution;
- target and slot sizes;
- named motion durations;
- exits are shorter than entries;
- reduced-motion output never exceeds 100 ms;
- reduced motion selects opacity/static presentation rather than transforms.

### 10.3 Component tests

Each component is instantiated without `GameManager`, `SaveManager`, `SettingsManager`, or `UIScreenHost`.

Tests cover:

- action-button variant mapping;
- selected-plus-focused behaviour;
- loading state restoration;
- disabled reason;
- final destructive treatment;
- panel surface mapping;
- modal severity and size classes;
- stat-bar clamping, low, overflow, negative, and invalid states;
- input-device and binding changes;
- context-prompt composition;
- toast severity;
- focus-halo target lifecycle and no-layout-shift behaviour;
- reduced-motion presentation.

### 10.4 Showcase runtime tests

GdUnit4 runtime tests instantiate the showcase in a `SubViewport` at:

- 640×360;
- 1024×768;
- 1280×720;
- 1440×900;
- 1920×1080;
- 2560×1080;
- 2560×1440.

Assertions are structural and deterministic:

- the scene and all resources load;
- every required section and state fixture exists;
- compact state resolves at the approved threshold;
- the content frame respects safe margins and ultrawide maximum;
- critical controls remain reachable;
- minimum target sizes are preserved;
- long text wraps;
- required focus neighbours and focus targets are valid;
- selected and focused state can coexist;
- component roots instantiate with no gameplay autoloads;
- no missing HPA-374 resource warnings are emitted;
- headless runs verify viewport sizing and node/layout contracts without requiring pixel screenshots.

A small manual verification checklist remains for subjective glow, balance, animation feel, and readability over representative backgrounds.

## 11. Documentation contract

`docs/ui/hpa-377/README.md` documents:

1. the canonical Theme path;
2. how a screen root or `UIScreenHost` branch opts in;
3. the stable type-variation names and intended built-in control types;
4. the difference between Theme values and `SiriusUiMetrics`;
5. compact-mode selection;
6. reduced-motion binding;
7. component APIs and ownership boundaries;
8. HPA-374 asset access through `UiArtCatalog` and `UiIconPresenter`;
9. which visual details remain asset-owned;
10. prohibited patterns:
   - new scene-local palette values;
   - repeated `StyleBoxFlat` definitions;
   - component access to gameplay singletons;
   - setting the Theme globally before HPA-379;
   - screen-specific abstractions in the shared component folder.

The document also records the HPA-373 source version used by the implementation. The stale HPA-373 header that still says written review is pending must be corrected to match its completed Linear state before final HPA-377 validation.

## 12. Implementation order

1. Correct the HPA-373 approval-status inconsistency.
2. Add `SiriusThemeTypes`, metrics, motion contracts, and failing tests.
3. Author `SiriusTheme.tres` with fonts, base controls, and type variations.
4. Implement action button, panel, and focus treatment.
5. Implement modal shell and stat bar.
6. Implement input hint and context prompt by composing the existing presenter.
7. Implement toast shell.
8. Build the showcase and viewport selector.
9. Add component, resource, and all-size runtime tests.
10. Add integration documentation and run the complete validation suite.

The implementation plan will turn these steps into file-by-file test-driven tasks after this design artifact is reviewed.

## 13. Rejected alternatives

### 13.1 Project-global Theme activation in HPA-377

Rejected because it would restyle legacy screens immediately and combine theme implementation with several migration tickets.

### 13.2 Runtime Theme builder or Theme autoload

Rejected because it duplicates Godot's authored resource model, adds initialization order and lifecycle concerns, and makes editor inspection harder.

### 13.3 Parallel C# design-token catalogue

Rejected because colour and style values would drift between code and `SiriusTheme.tres`. Only non-Theme arithmetic metrics and motion policy belong in code.

### 13.4 Multiple Themes for compact, dark, or individual screens

Rejected because it creates resource drift and makes downstream composition unpredictable. Compact presentation uses explicit compact variations and component/layout state within one canonical Theme. Light and dark backgrounds are showcase test surfaces, not separate product themes.

### 13.5 Comprehensive UI component library before migration

Rejected because inventory, battle, save/load, dialogue, and other screen-specific APIs are not yet proven. Those abstractions must be extracted during their migration tickets.

### 13.6 Pixel-golden tests as the main test strategy

Rejected because font rendering, GPU/backend differences, and headless execution would make them fragile. Resource and layout contracts provide deterministic coverage; manual visual review covers subjective polish.

## 14. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Type-variation spelling drift | One `SiriusThemeTypes` catalogue plus resource-contract tests |
| Theme values duplicated in code | Theme owns colours/styles; code owns only metrics and motion |
| Accidental legacy restyle | Do not configure a project-global Theme in HPA-377 |
| Components become a second navigation framework | Presentation-only APIs; no host, pause, cancel, or focus-restoration ownership |
| Input hints create another global device service | Reuse `InputHintPresenter`; observe only while visible |
| Loading state violates restrained-motion rules | Static readable state; no looping spinner |
| Compact handling forks the Theme | One Theme with compact typography variations and explicit layout state |
| Showcase passes while components depend on gameplay | Instantiate every component without gameplay autoloads |
| Headless rendering creates flaky tests | Structural assertions rather than pixel equality |
| HPA-373 status remains contradictory | Correct the header and record the source version before final validation |

## 15. Acceptance mapping

| HPA-377 acceptance criterion | Design coverage |
| --- | --- |
| Approved palette, typography, spacing, state, and motion rules are represented | Sections 6 and 7 |
| Common controls no longer require repeated per-scene styles | One canonical Theme and documented opt-in contract |
| Showcase demonstrates every supported state and long-text behaviour | Section 9 |
| Focus is clear for keyboard and gamepad users | Theme focus styles, selected-plus-focused fixture, and focus halo |
| APIs are small and avoid gameplay singletons | Section 8 and component isolation tests |
| Components work at approved viewport sizes | Section 10.4 |
| No premature domain-specific abstractions | Non-goals and rejected comprehensive library |
| Tests cover Theme loading and required variations | Sections 10.1–10.4 |

## 16. Completion definition

HPA-377 is complete when:

- `SiriusTheme.tres` is the single canonical Sirius visual resource;
- every required variation and font is covered by deterministic tests;
- the approved core components exist with presentation-only APIs;
- the showcase contains every required state and edge-case fixture;
- the showcase passes structural validation at every approved viewport;
- focus, selected, disabled, warning, destructive, loading, and reduced-motion treatment are demonstrably distinct;
- integration and asset-ownership documentation is complete;
- no production screen is silently migrated;
- no component depends on gameplay singletons;
- the full repository test suite passes.
