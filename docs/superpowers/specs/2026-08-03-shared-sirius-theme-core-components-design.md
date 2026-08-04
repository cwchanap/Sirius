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

## 1. Decision

HPA-377 will deliver:

1. one authored, opt-in `SiriusTheme.tres`;
2. a small set of presentation-only components with named first consumers;
3. an isolated showcase scene;
4. deterministic resource and layout checks.

The design keeps three ownership boundaries:

- **Theme:** fonts, colours, style boxes, native control states, scrims, spacing, and type variations;
- **components:** small presentation APIs and scene composition;
- **`UIScreenHost` and consuming flows:** stacking, queueing, pause, input, focus restoration, navigation, and lifetime.

The Theme is not configured as `ProjectSettings.gui/theme/custom`. HPA-377 applies it only to the showcase and test fixtures. Production roots or individual migrated screens opt in later.

## 2. Complexity constraint

HPA-378 demonstrated the cost of over-generalizing foundation work: its initial host implementation was followed by a concentrated sequence of fixes for re-entrant mutation, focus restoration, publication ordering, and deferred teardown.

HPA-377 therefore uses the following guardrails:

- no new autoload, registry, coordinator, pure state model, or lifecycle service;
- no reusable state machine without a current or named downstream consumer;
- no component that owns domain operations or asynchronous work;
- no runtime fallback for required committed Theme assets when a resource-contract test can fail the build;
- no control type is styled merely because Godot provides it;
- no generic focus helper until a real control proves Theme focus is insufficient;
- no exhaustive lifecycle test matrix for controls that have no lifecycle ownership;
- no single HPA-377 test file should become a multi-thousand-line integration suite.

If implementation requires breaking one of these rules, the design must be revisited before adding the abstraction.

## 3. Approved source baseline

Implementation is grounded in the approved HPA-373 artifact:

- specification version: **1.7**;
- design decisions approved: **2026-07-25**;
- source blob: `9e1d1edb366a67a3fa6d0dd02f3641aa0bb42a7d`;
- merged PR: **#17**;
- merge commit: `bc82eadcab27e2321c69fcf56cc3c43e6917b5f5`.

The stale “review candidate” header in that file is metadata debt. Linear completion and merged PR #17 are authoritative; the stale header does not block HPA-377.

HPA-374 provides:

- direct font resources under `res://assets/fonts/`;
- icons, ornaments, and effects through `UiArtCatalog`;
- icon application through `UiIconPresenter`;
- input-device and binding presentation through `InputHintPresenter`.

HPA-378 provides the scene-local `UIScreenHost`. HPA-377 components do not depend on it.

## 4. Demand ledger

The shared surface is limited to controls with a current or named first consumer.

| Capability | First consumer | HPA-377 treatment |
| --- | --- | --- |
| Labelled action buttons | Existing menus; HPA-380; HPA-382 | Theme variations plus thin `SiriusActionButton` |
| Content/HUD/modal surfaces | HPA-380; HPA-381; HPA-382 | Theme variations plus thin `SiriusPanel` and `SiriusModalShell` |
| HP, MP, EXP bars | HPA-381; HPA-356 | `SiriusStatBar` with three kinds |
| Input hint | Existing inventory integration; HPA-380; HPA-381 | `SiriusInputHint`, reusing `InputHintPresenter` |
| Context prompt | HPA-381 | `SiriusContextPrompt` |
| Toast visual shell | HPA-386 | `SiriusToastShell`; queueing remains in HPA-386 |
| Ignition seal | HPA-356 and HPA-386 | Theme variation and showcase fixture, not a separate component state machine |
| Generic focus halo | No proven consumer | Deferred to the first migration that proves Theme focus is insufficient |
| Automatic-action bar | HPA-356 only | Deferred to HPA-356; HPA-377 provides HP/MP/EXP only |
| Telemetry callout/catalogue rail APIs | HPA-356/HPA-357 only | Deferred to their owning migrations |
| Persistent reduced motion | HPA-541 | Components accept explicit flags only where they animate |

A deferred consumer extends the same central Theme or extracts a component in its own ticket. It must not add a screen-local palette or duplicate an existing shared style.

## 5. Scope

### 5.1 Theme and tokens

Implement the HPA-373 values for:

- palette and semantic colours;
- typography roles;
- spacing, borders, radii, shadows, and opacity;
- buttons and common interaction states;
- content, feature, HUD, modal, warning, and error surfaces;
- tabs and tooltips;
- HP, MP, and EXP bars;
- full-screen and child scrims;
- motion duration/easing constants and reduced-motion alternatives.

### 5.2 Components

Create only:

- `SiriusActionButton`;
- `SiriusPanel`;
- `SiriusModalShell`;
- `SiriusStatBar`;
- `SiriusInputHint`;
- `SiriusContextPrompt`;
- `SiriusToastShell`.

### 5.3 Showcase

Demonstrate:

- normal, hover, pressed, selected, disabled, focused, warning, destructive, and loading presentation;
- typography and long-text behaviour;
- HP, MP, and EXP edge cases;
- keyboard, mouse, and gamepad focus/input hints;
- panel and scrim layering;
- standard and compact layout;
- every HPA-373 validation size.

“Loading” is a showcase presentation fixture: a disabled Primary button labelled `Loading…`. HPA-377 does not add a reusable loading state machine.

### 5.4 Non-goals

HPA-377 does not:

- restyle or migrate production screens;
- globally activate the Theme;
- add inventory, battle, save, dialogue, shop, or puzzle domain components;
- add a focus-tracking component;
- implement asynchronous button ownership or loading restoration;
- implement toast queueing, deduplication, timeout, stacking, or transition retention;
- add settings persistence or a player-facing reduced-motion setting;
- style unused Godot control types pre-emptively;
- implement the seal-shaped short-confirmation flow;
- make pixel screenshots the primary correctness gate.

## 6. File layout

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
├── SiriusModalShell.cs
├── SiriusStatBar.cs
├── SiriusInputHint.cs
├── SiriusContextPrompt.cs
└── SiriusToastShell.cs

scenes/ui/components/
├── SiriusModalShell.tscn
├── SiriusStatBar.tscn
├── SiriusInputHint.tscn
├── SiriusContextPrompt.tscn
└── SiriusToastShell.tscn

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

Direct subclasses with one visual region remain code-first. Multi-node composites receive `.tscn` scenes so their structure remains editor-inspectable.

## 7. Theme contract

### 7.1 Palette

| Role | HPA-373 token | Value |
| --- | --- | --- |
| Deep backdrop | `night-1000` | `#050714` |
| Base surface | `night-900` | `#0D1530` |
| Raised surface | `indigo-800` | `#18234A` |
| Interactive indigo | `indigo-700` | `#27366C` |
| Primary text | `moon-50` | `#F7F5FF` |
| Secondary text | `moon-200` | `#C7CEE8` |
| Muted text | `moon-400` | `#8F9AB8` |
| Magic/focus | `cyan-400` | `#62DCFF` |
| Primary/reward | `gold-300` | `#F5D784` |
| Strong gold | `gold-500` | `#DFAE43` |
| Arcane action | `magenta-400` | `#D96CC2` |
| Success | `success-400` | `#68D6A3` |
| Warning | `warning-400` | `#F1B85B` |
| Danger | `danger-400` | `#F16D83` |

The names remain documentation identifiers; `SiriusTheme.tres` is the runtime source of truth.

### 7.2 Typography

Use seven semantic roles:

```text
SiriusDisplay / SiriusDisplayCompact
SiriusTitle / SiriusTitleCompact
SiriusSection / SiriusSectionCompact
SiriusBody / SiriusBodyCompact
SiriusMetadata / SiriusMetadataCompact
SiriusNumeric / SiriusNumericCompact
SiriusTelemetry
```

| Role | Standard | Compact | Font |
| --- | ---: | ---: | --- |
| Display/wordmark | 44 | 30 | Cinzel 600 |
| Title | 32 | 24 | Noto Sans SemiBold |
| Section/entity | 20 | 17 | Noto Sans SemiBold |
| Body/essential control | 16 | 14 | Noto Sans Regular/Medium |
| Metadata/input hint | 14 | 12 | Noto Sans Regular |
| Numeric | 16 | 14 | Noto Sans Mono Medium |
| Telemetry | 12 | 12 | Noto Sans Mono Medium |

Rules:

- Cinzel is restricted to display headings and has Noto Sans glyph fallback;
- body/numeric roles never use Cinzel;
- telemetry is uppercase, short, and tracked;
- telemetry intentionally has no compact variation;
- multi-line body copy uses approximately 1.4 line height;
- short HUD labels use the Body role and approximately 1.25 line height only when multi-line;
- numeric presentation uses the mono font and tabular figures;
- essential text never drops below 14 px compact;
- long text wraps or scrolls rather than shrinking.

#### Why paired compact roles remain

A single branch-wide font scale was considered. It is rejected because HPA-373’s compact reductions are non-uniform by role: Display changes 44→30, Title 32→24, Section 20→17, Body 16→14, and Metadata 14→12. A single scale cannot preserve those decisions. A compact root Theme override would duplicate the same per-role size table while making variation ownership less explicit.

The paired set is limited to six pairs plus fixed Telemetry; EntityName and HUD-specific duplicate roles were removed.

### 7.3 Spacing and metrics

`SiriusUiMetrics` exposes:

```text
Space4 / Space8 / Space12 / Space16 / Space24 / Space32 / Space48
Compact threshold: width < 800 or height < 450
Standard safe margin: 24
Compact safe margin: 12
Ultrawide content maximum: 1600
Standard minimum target: 44×44
Compact minimum target: 40×40
Standard slot: 56×56
Compact slot: 48×48
Modal widths: 420 / 640 / 960
Modal maximum: 90% of viewport
Tooltip maximum: 360 standard / 280 compact
Ignition preferred size: 96×96 standard / 80×80 compact
```

Components use these constants instead of introducing local spacing numbers.

### 7.4 Interactive states

| State | Contract |
| --- | --- |
| Normal | Indigo surface with 1 px muted border |
| Hover | Brighter surface and restrained cyan edge light |
| Pressed | Darker fill and 1 px depression |
| Hover-pressed | Pressed geometry with hover emphasis |
| Focus | Independent cyan outer ring; no layout shift |
| Selected/toggled | Persistent gold treatment and non-colour marker |
| Disabled | 45% opacity, no glow, readable reason where applicable |
| Warning | Amber icon/border plus warning text |
| Destructive | Rose icon/border; filled danger only at final confirmation |

### 7.5 Buttons

`SiriusActionButtonVariant` is limited to:

```csharp
Primary,
Secondary,
Tertiary,
Warning,
Destructive
```

Each maps to one Theme variation and defines `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, and `disabled` resources.

`SiriusActionButton` provides only:

```text
Variant : SiriusActionButtonVariant
IconId : UiIconId?
IconSize : UiIconSize = UiIconSize.Default
DisabledReason : string
```

It maps the enum, applies an optional icon, and exposes a readable disabled reason. It does not own loading, selection state, tasks, or navigation. Callers use the stock `ToggleMode`/`ButtonPressed` API when they need a selected button.

Unknown enum values throw.

#### Ignition

Ignition is a Theme type variation applied to a stock square `Button`; it is not another `SiriusActionButton` state machine.

- type variation: `SiriusIgnitionButton`;
- required asset: `res://assets/sprites/ui/ornaments/ignition_seal.png`;
- preferred size: 96×96 standard, 80×80 compact;
- same required texture is reused by normal/hover/pressed/hover-pressed/disabled `StyleBoxTexture` resources with state-specific modulation;
- label is centred and limited to two lines inside a 16 px inset;
- focus uses the required cyan `focus_halo.png` as the Theme focus style;
- required assets are validated by resource tests; there is no runtime fallback path for a normal build.

When localized text does not fit the seal, the consumer uses a conventional Primary action instead of stretching or truncating the seal.

### 7.6 Surfaces and scrims

`SiriusPanelSurface` is limited to:

```csharp
Content,
Feature,
HudPlate,
Modal
```

Theme variations:

```text
SiriusContentPanel
SiriusFeaturePanel
SiriusHudPlate
SiriusModalPanel
SiriusWarningPanel
SiriusErrorPanel
SiriusScrim
SiriusChildScrim
```

`SiriusWarningPanel` and `SiriusErrorPanel` are selected internally by `SiriusModalShell`; they are not extra public panel-surface enum values.

Scrims use `night-1000` at 58% and 72%. A host or caller creates the full-rect scrim beneath modal content. `SiriusModalShell` does not own it. Toasts never use scrims.

Telemetry callouts and catalogue rails are deferred to the tickets that first build them.

### 7.7 Bars

`SiriusStatBarKind` is limited to:

```csharp
Health,
Mana,
Experience
```

Each maps to its approved icon and Theme fill:

- HP: danger/rose;
- MP: cyan;
- EXP: gold.

Low, overflow, negative, and invalid values include text or markers and are never colour-only. Automatic-action progress is deferred to HPA-356.

### 7.8 Tabs, tooltips, and native controls

HPA-377 styles only controls required by its components/showcase or explicitly named in the ticket:

- Label;
- Button;
- Panel and PanelContainer;
- ProgressBar;
- TabBar and TabContainer;
- TooltipPanel and TooltipLabel;
- ScrollContainer, HScrollBar, and VScrollBar as required by long modal/showcase content.

HPA-377 does **not** pre-style VSlider, SpinBox, CheckButton, MenuButton, split containers, dialog chrome, ItemList, Tree, or other unused/future types.

Current screens are not opted into the Theme by HPA-377. Their current use of HSlider, CheckBox, OptionButton, TextureButton, HSplitContainer, AcceptDialog, and other controls therefore does not require speculative coverage here. Their owning migration tickets extend the same central Theme before opting those screens in; they do not create local shared styles.

## 8. Compact propagation

Compact mode has a mechanical authority rule:

- only a node that owns a `Viewport` or `SubViewport` computes compact mode;
- Main Menu and Game roots compute from their root viewport safe frame;
- the showcase preview computes from its `SubViewport`;
- hosted screens in the same viewport inherit the root decision;
- a host layer, modal, panel, or ordinary nested Control never begins a new compact branch.

Algorithm:

1. viewport owner computes `compact = SiriusUiMetrics.IsCompact(safeFrameSize)` when the safe frame changes;
2. it passes the value to shared components;
3. it assigns standard/compact variations to free Labels it owns;
4. a component switches only the nested Label variations it owns;
5. components never infer compact mode from their own rectangle.

This keeps one Theme and prevents parent/child disagreement.

## 9. Motion

`SiriusMotion` defines:

| Profile | Duration | Easing |
| --- | ---: | --- |
| Control feedback | 120 ms | quadratic out |
| Callout entry | 220 ms | cubic out |
| Callout exit | 180 ms | quadratic in |
| Screen transition | 280 ms | cubic in/out |
| Orrery transformation | 400 ms maximum | cubic in/out |
| Reduced-motion opacity | 100 ms maximum | linear |

HPA-377 components animate only modal/toast entry and exit where demonstrated. Reduced motion replaces transforms and pulses with a static state or ≤100 ms opacity transition.

Components receive reduced-motion state explicitly and never read `SettingsManager`. HPA-541 owns persistence and root propagation.

## 10. Core components

### 10.1 `SiriusActionButton`

Base: `Button`.

Responsibilities:

- map five variants to Theme type variations;
- apply optional `UiIconId` through `UiIconPresenter`;
- expose `DisabledReason` through tooltip/detail presentation;
- preserve native Button focus, toggle, disabled, and activation behaviour.

No custom lifecycle or loading state.

### 10.2 `SiriusPanel`

Base: `PanelContainer`.

```text
Surface : SiriusPanelSurface
```

It maps four values to Theme variations and contains no compact, content-loading, or domain logic.

### 10.3 `SiriusModalShell`

Scene-authored rectangular observatory plate:

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

Closed public types:

```csharp
SiriusUiSeverity: Info, Success, Warning, Error
SiriusModalSizeClass: Small, Medium, Large
```

The shell owns composition and responsive sizing only. It does not create a scrim, register with the host, select focus, intercept Cancel, dismiss itself, or choose domain actions.

Short circular/octagonal confirmations remain HPA-386 work.

### 10.4 `SiriusStatBar`

Scene-authored icon + label + ProgressBar + numeric/state marker.

```text
Kind : SiriusStatBarKind
Current : double
Maximum : double
Label : string
ShowNumericValue : bool
LowThreshold : double = 0.25
Compact : bool
```

Visual fill clamps to `[0,1]`; displayed values preserve caller data. Negative, overflow, and invalid maximum states are explicit. Unknown kinds throw.

### 10.5 `SiriusInputHint`

Scene-authored wrapper around the existing `InputHintPresenter`.

```text
Prompt : string
Actions : StringName[]
Compact : bool
Refresh()
```

It observes input only while visible. It does not introduce a global input-device service.

### 10.6 `SiriusContextPrompt`

Composes:

- optional `UiIconId`;
- prompt text;
- `SiriusInputHint`.

It does not discover targets or invoke interactions.

### 10.7 `SiriusToastShell`

Scene-authored semantic icon/title/message surface.

```text
Severity : SiriusUiSeverity
Title : string
Message : string
Compact : bool
ReducedMotion : bool
```

It owns only visual presentation and entry/exit motion. HPA-386 owns queueing, deduplication, timeout, stacking, host registration, and lifecycle.

## 11. Showcase

`SiriusUiShowcase.tscn` is a development scene outside production navigation.

### 11.1 Deterministic fixtures

Backgrounds:

1. solid `night-1000`;
2. solid `moon-50`;
3. retained main-menu background;
4. retained battle background.

Long-text fixtures:

- an action label approximately twice the normal English length;
- a 240-character multi-line body paragraph;
- a 48-character unbroken metadata token.

### 11.2 Required sections

1. palette, surfaces, and both scrims;
2. standard/compact typography and long text;
3. all five action variants and their native states;
4. stock Ignition button at standard/compact sizes;
5. selected-plus-focused toggle button;
6. disabled Primary button labelled `Loading…`;
7. tabs and tooltips;
8. HP/MP/EXP bars at negative, low, medium, full, overflow, and invalid values;
9. keyboard, mouse, gamepad, fallback, and unbound input hints;
10. context prompts;
11. Info/Success/Warning/Error toasts;
12. Small/Medium/Large modal shells over both scrims;
13. normal and reduced-motion modal/toast transitions.

Loading is intentionally only a static presentation fixture. A reusable loading API may be added by HPA-382 if a real operation needs one.

## 12. Testing

### 12.1 Resource contract

Verify:

- `SiriusTheme.tres` loads;
- all required fonts and required ornament textures load;
- required typography, button, panel, bar, tab, tooltip, scrollbar, and scrim variations exist;
- button variations define normal/hover/pressed/hover-pressed/focus/disabled;
- Ignition states reference the committed seal texture and focus halo;
- scrim opacity is 58%/72%;
- enum-to-Theme mappings are exhaustive;
- typography sizes and font roles match the contract.

There is no missing-required-asset runtime fallback test. A missing required asset is a failing resource contract.

### 12.2 Component tests

Keep focused tests for:

- enum mapping;
- disabled reason;
- modal severity/size mapping and no-scrim ownership;
- stat edge cases;
- input-device/binding changes;
- context-prompt composition;
- toast semantics without queue ownership;
- compact nested-label switching;
- reduced-motion modal/toast presentation.

Do not duplicate native Godot Button lifecycle tests or add generic focus-lifecycle tests.

### 12.3 Viewport matrix

HPA-373 requires these seven sizes because Sirius supports resizable windows, 4:3, 16:10, 16:9, and ultrawide—not only the four Settings presets:

- 640×360;
- 1024×768;
- 1280×720;
- 1440×900;
- 1920×1080;
- 2560×1080;
- 2560×1440.

To limit test cost, one showcase instance is created in one `SubViewport` fixture and resized sequentially through all seven sizes.

At every size, assert only:

- content remains inside the safe frame;
- compact mode is consistent;
- primary examples remain reachable;
- minimum targets hold;
- long content wraps or scrolls;
- no required resource is missing.

Run full keyboard/gamepad focus traversal only at four shape representatives:

- 640×360 compact;
- 1280×720 16:9;
- 1024×768 4:3;
- 2560×1080 ultrawide.

This preserves the approved rendering matrix without multiplying the full interaction suite seven times.

### 12.4 Test-size guard

Tests are split by resource, component, and showcase responsibility. If any test file approaches 500 lines or requires re-entrant/lifecycle combinatorics, stop and reconsider the production abstraction rather than expanding the matrix.

## 13. Documentation

`docs/ui/hpa-377/README.md` is a concise integration guide, not a second design specification. It contains only:

- Theme path and opt-in example;
- public type variations and component APIs;
- compact propagation rule;
- font/art paths;
- HPA-541 and HPA-386 handoffs;
- prohibition on repeated shared local styles.

It links to this design for rationale and should remain short enough to scan during a screen migration.

## 14. Implementation order

1. Add Theme identifiers, five closed enums, metrics, motion constants, and failing resource tests.
2. Author fonts, typography, buttons, Ignition, panels, bars, tabs, tooltips, scrollbars, and scrims in `SiriusTheme.tres`.
3. Implement `SiriusActionButton` and `SiriusPanel`.
4. Implement scene-authored ModalShell, StatBar, and InputHint.
5. Implement ContextPrompt and ToastShell.
6. Build the showcase and compact propagation.
7. Add focused component tests and the reused-fixture viewport matrix.
8. Add the concise integration README.
9. Run focused suites, build, and full repository tests.

After explicit approval, change this document’s status to `Approved design` and write the file-by-file TDD plan.

## 15. Alternatives considered

### 15.1 Project-global Theme

Cheaper to wire initially, but rejected because it immediately restyles legacy screens and combines HPA-377 with multiple migrations.

### 15.2 Runtime Theme builder/autoload

Rejected because it adds initialization and lifecycle state where an authored Godot resource already exists.

### 15.3 Local styles per migrated screen

Cheapest for the first screen, but rejected because it recreates the exact duplication HPA-377 exists to remove and makes later visual corrections multi-file work.

### 15.4 Stock controls with one generic dark Theme

Smaller than this design, but insufficient for the approved semantic button states, separate focus/selection treatment, HP/MP/EXP roles, and HPA-373 visual direction.

### 15.5 One typography set plus compact root scaling

Smaller variation count, but cannot reproduce the non-uniform compact role sizes. A per-root Theme override would duplicate the role table or introduce local overrides. The reduced seven-role paired set is the smaller explicit option.

### 15.6 Full native-control coverage

Rejected. Only types used by HPA-377 are styled now; later migration tickets extend the central Theme when demand appears.

### 15.7 Reusable loading and generic focus helpers

Rejected until a real consumer proves the API. The showcase demonstrates loading presentation with stock state, and Theme focus covers current components.

### 15.8 Four viewport presets only

Rejected because Settings presets do not define supported window shapes. HPA-373 explicitly requires 4:3, 16:10, and ultrawide validation in addition to 16:9.

### 15.9 Pixel-golden primary tests

Rejected because font rendering, GPU/backend differences, and headless execution make them fragile. Structural checks plus manual visual review divide objective and subjective validation.

## 16. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Foundation grows another bug tail | No lifecycle services/state machines; test-size guard; demand ledger |
| Theme names drift | Central identifiers and resource mapping tests |
| First migration needs an unstylized control | Extend the central Theme in that migration; do not add local shared styles |
| Compact disagreement | Only Viewport/SubViewport owners compute compact mode |
| Ignition becomes a bespoke component | Keep it a stock Button Theme variation |
| Missing required art creates dead fallback code | Fail resource contract instead of runtime fallback |
| Loading becomes a speculative state machine | Static showcase fixture only |
| Focus helper gains lifecycle complexity | Defer until a proven consumer exists |
| Viewport tests become expensive | Reuse one fixture; full interaction at four representative shapes |
| Documentation duplicates itself | Keep README as a short usage guide linking to this design |

## 17. Acceptance mapping

| HPA-377 criterion | Coverage |
| --- | --- |
| Approved palette, typography, spacing, states, motion | Sections 7–9 |
| Common controls avoid repeated per-scene styles | One canonical opt-in Theme and central-extension rule |
| Showcase demonstrates all required states and long text | Section 11, including static Loading fixture |
| Focus clear for keyboard/gamepad | Theme focus plus four-shape interaction traversal |
| Small APIs without gameplay singletons | Section 10 and complexity guardrails |
| All approved viewports/aspects | Sequential seven-size fixture in section 12.3 |
| No premature domain abstractions | Demand ledger and explicit deferrals |
| Theme loading/type variations tested | Sections 12.1–12.2 |

## 18. Completion definition

HPA-377 is complete when:

- the canonical opt-in Theme loads with all required resources;
- the seven listed components exist with only the APIs in section 10;
- Ignition is a tested stock Button Theme variation;
- the showcase contains every required state, including static Loading presentation;
- one fixture renders the showcase at all seven approved sizes;
- full focus traversal passes at the four representative shapes;
- no component depends on application singletons, `UIScreenHost`, or a new lifecycle service;
- the concise integration guide is complete;
- focused tests, build, and full repository tests pass.

Persistent reduced motion remains HPA-541 work. Toast/reward queueing and short confirmation seals remain HPA-386 work.