# HPA-377 Shared Sirius Theme, Core Components, and UI Showcase Design

**Status:** Approved design  
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

HPA-377 delivers:

1. one authored, opt-in `SiriusTheme.tres`;
2. five scene-authored presentation components with proven consumers;
3. an isolated UI showcase;
4. deterministic resource, component, and responsive-layout tests.

Ownership remains split three ways:

- **Theme:** fonts, colours, style boxes, native control states, scrims, spacing, and type variations;
- **components:** only presentation behavior that stock Godot controls cannot express declaratively;
- **`UIScreenHost` and consuming flows:** stacking, queueing, pause, input, focus restoration, navigation, dismissal, and lifetime.

The Theme is not configured as `ProjectSettings.gui/theme/custom`. HPA-377 assigns it only to the showcase and test fixtures. Production roots and migrated screens opt in later.

## 2. YAGNI and complexity constraint

HPA-378 demonstrated the cost of over-generalized foundation work through a concentrated hardening tail around re-entrant mutation, focus restoration, publication ordering, and deferred teardown. HPA-377 therefore follows these rules:

- no autoload, registry, coordinator, pure state model, lifecycle service, or generic focus helper;
- no subclass whose only behavior is assigning `ThemeTypeVariation`;
- no reusable state machine without a current consumer in HPA-377;
- no component-owned navigation, queue, dismissal, asynchronous task, or animation lifetime;
- no runtime fallback for required committed Theme assets when a resource test can fail the build;
- no Godot control type is styled merely because the engine provides it;
- no C# constant is added only for a downstream ticket;
- no configurable property is added when HPA-377 has one approved value;
- each HPA-377 test file stays below 500 lines;
- re-entrant or teardown combinatorics trigger a design revisit instead of a larger test matrix.

## 3. Approved source baseline

Implementation is grounded in HPA-373:

- specification version: **1.7**;
- design decisions approved: **2026-07-25**;
- source blob: `9e1d1edb366a67a3fa6d0dd02f3641aa0bb42a7d`;
- merged PR: **#17**;
- merge commit: `bc82eadcab27e2321c69fcf56cc3c43e6917b5f5`.

HPA-374 supplies:

- fonts under `res://assets/fonts/`;
- icons, ornaments, and effects through `UiArtCatalog`;
- `UiIconPresenter` for Button and TextureRect icon application;
- `InputHintPresenter` for device and binding presentation.

HPA-378 supplies the scene-local `UIScreenHost`. HPA-377 components do not depend on it.

## 4. Demand ledger

| Capability | First consumer | HPA-377 treatment |
| --- | --- | --- |
| Labelled actions | Existing menus; HPA-380; HPA-382 | Stock `Button` plus Theme variations; no subclass |
| Content/HUD surfaces | HPA-380; HPA-381 | Stock `PanelContainer` plus Theme variations; no subclass |
| Modal composition | HPA-382 | `SiriusModalShell` visual composition only |
| HP, MP, EXP bars | HPA-381; HPA-356 | `SiriusStatBar` with three public kinds |
| Input hint | Existing inventory integration; HPA-380; HPA-381 | `SiriusInputHint` around `InputHintPresenter` |
| Context prompt | Explicit HPA-377 scope; HPA-381 | `SiriusContextPrompt` |
| Toast visual shell | Explicit HPA-377 scope; HPA-386 | `SiriusToastShell`; queue/lifetime remains HPA-386 work |
| Ignition seal | HPA-356; HPA-386 | Stock square `Button` Theme variation |
| Focus/highlight helper | No control proves Theme focus insufficient | Deferred |
| Automatic-action bar | HPA-356 only | Deferred to HPA-356 |
| Telemetry callout/catalogue rail | HPA-356/HPA-357 | Deferred to those tickets |
| Persisted reduced motion | HPA-541 | Shared motion policy only; production binding deferred |

A deferred consumer extends the same central Theme or extracts a component in its own ticket. It must not add a screen-local palette or duplicate a shared style.

## 5. Scope and files

### 5.1 Theme and contracts

```text
resources/ui/theme/SiriusTheme.tres

scripts/ui/theme/
├── SiriusThemeTypes.cs
├── SiriusUiTypes.cs
├── SiriusUiMetrics.cs
└── SiriusMotion.cs
```

`SiriusUiTypes.cs` contains only:

- `SiriusUiSeverity`;
- `SiriusModalSizeClass`;
- `SiriusStatBarKind`;
- their exhaustive presentation mappings.

Button and panel roles use `SiriusThemeTypes` constants directly. There is no `SiriusActionButtonVariant`, `SiriusPanelSurface`, `SiriusActionButton`, or `SiriusPanel`.

### 5.2 Components

```text
scripts/ui/components/
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
```

Every component is a multi-node composite. Stock controls remain stock controls.

### 5.3 Showcase, tests, and guide

```text
scenes/ui/showcase/SiriusUiShowcase.tscn
scripts/ui/showcase/SiriusUiShowcase.cs

tests/ui/theme/
tests/ui/components/
tests/ui/showcase/

docs/ui/hpa-377/README.md
```

## 6. Theme contract

### 6.1 Palette

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

These names are documentation identifiers. `SiriusTheme.tres` is the runtime source of truth.

### 6.2 Typography

Use six standard/compact pairs plus fixed Telemetry:

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
| Body/control | 16 | 14 | Noto Sans Regular/Medium |
| Metadata/input hint | 14 | 12 | Noto Sans Regular |
| Numeric | 16 | 14 | Noto Sans Mono Medium |
| Telemetry | 12 | 12 | Noto Sans Mono Medium |

Rules:

- Cinzel is restricted to display headings and has Noto Sans glyph fallback;
- body and numeric roles never use Cinzel;
- telemetry is uppercase, short, tracked, and intentionally has no compact variation;
- multi-line body copy uses approximately 1.4 line height;
- short HUD copy uses Body and approximately 1.25 line height only when multi-line;
- numeric presentation uses the mono font and tabular figures;
- essential compact text never drops below 14 px;
- long text wraps or scrolls instead of shrinking.

A single compact scale is rejected because the approved reductions are non-uniform: 44→30, 32→24, 20→17, 16→14, and 14→12.

### 6.3 Metrics

`SiriusUiMetrics` exposes only values consumed by HPA-377 code:

```text
Compact threshold: width < 800 or height < 450
Standard safe margin: 24
Compact safe margin: 12
Ultrawide content maximum: 1600
Standard minimum target: 44×44
Compact minimum target: 40×40
Modal widths: 420 / 640 / 960
Modal maximum: 90% of viewport
Tooltip maximum: 360 standard / 280 compact
Ignition preferred size: 96×96 standard / 80×80 compact
Seven approved validation viewports
Two focus-validation viewports: 640×360 and 1280×720
```

Spacing values remain authored in the Theme and scenes. They are not duplicated as C# constants. Slot dimensions remain HPA-357 work.

### 6.4 Buttons

Stock `Button` variations:

```text
SiriusPrimaryButton
SiriusSecondaryButton
SiriusTertiaryButton
SiriusWarningButton
SiriusDestructiveButton
SiriusIgnitionButton
```

Each conventional variation defines `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, and `disabled` resources. Callers use:

```csharp
button.ThemeTypeVariation = SiriusThemeTypes.PrimaryButton;
UiIconPresenter.Apply(button, UiIconId.Confirm, UiIconSize.Default);
button.TooltipText = disabledReason;
```

No wrapper class is required. The caller owns the disabled explanation because it owns the reason.

Ignition remains a stock square Button:

- required `ignition_seal.png` reused by state `StyleBoxTexture` resources;
- preferred size 96×96 standard and 80×80 compact;
- label centered, at most two lines, 16 px inset;
- focus uses required cyan `focus_halo.png`;
- resource tests fail if either asset is absent;
- there is no runtime fallback path.

### 6.5 Surfaces and scrims

Stock `Panel`/`PanelContainer` variations:

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

Scrims use `night-1000` at 58% and 72%. A host or caller creates them. Modal and Toast shells do not own a scrim.

### 6.6 Bars

`SiriusStatBarKind` contains:

```csharp
Health,
Mana,
Experience
```

Theme variations:

```text
SiriusHpBar
SiriusMpBar
SiriusExpBar
SiriusInvalidBar
```

`SiriusInvalidBar` is internal presentation for `Maximum <= 0`; it is not a public stat kind.

- HP uses danger/rose;
- MP uses cyan;
- EXP uses gold;
- the low threshold is fixed at `0.25` for HPA-377;
- numeric values are always shown;
- low, overflow, negative, and invalid states include text or markers.

There is no `ShowNumericValue` or configurable `LowThreshold` until a consumer proves either need.

### 6.7 Other native controls

HPA-377 styles only:

- Label;
- Button;
- Panel and PanelContainer;
- ProgressBar;
- TabBar and TabContainer;
- TooltipPanel and TooltipLabel;
- ScrollContainer, HScrollBar, and VScrollBar.

Owning migration tickets extend the central Theme when a new control becomes a themed consumer.

## 7. Compact authority

Only a node that owns a `Viewport` or `SubViewport` computes compact mode.

- Main Menu and Game roots compute from their root viewport safe frame;
- the showcase preview computes from its `SubViewport`;
- hosted screens in the same viewport inherit the root decision;
- host layers, modals, panels, and nested Controls never begin another compact branch.

Algorithm:

1. viewport owner computes `compact = SiriusUiMetrics.IsCompact(safeFrameSize)` when size changes;
2. it passes the value to shared components;
3. it assigns standard/compact variations to free Labels it owns;
4. a component switches only nested Labels it owns;
5. no component infers compact mode from its own rectangle.

## 8. Motion

`SiriusMotion` contains only the motion exercised by HPA-377:

```text
Entry: 220 ms, cubic out
Exit: 180 ms, quadratic in
Reduced-motion opacity: at most 100 ms, linear
```

The showcase owns the demonstration Tween and applies it to wrapper Controls around the static Modal and Toast shells. The shells expose no `ReducedMotion`, `PlayEntry`, or `PlayExit` API and own no Tween lifetime.

HPA-373 remains the source for control-feedback, screen-transition, and orrery timings. HPA-380, HPA-382, HPA-356, and HPA-357 add code constants only when they implement those motions.

HPA-541 owns the persisted preference and production propagation.

## 9. Components

### 9.1 `SiriusModalShell`

Scene-authored rectangular observatory plate:

```text
Title : string
Severity : SiriusUiSeverity
SizeClass : SiriusModalSizeClass
Compact : bool
BodyHost : Control
ActionsHost : Control
RefreshPresentation(Vector2 availableSize)
```

It owns composition, severity presentation, and responsive width only. It does not create a scrim, animate, register with a host, choose focus, expose a close action, intercept Cancel, dismiss itself, or choose a domain action. HPA-382 owns production modal lifecycle and close behavior.

### 9.2 `SiriusStatBar`

```text
Kind : SiriusStatBarKind
Current : double
Maximum : double
Label : string
Compact : bool
RefreshPresentation()
```

Visual fill clamps to range; displayed values preserve caller data. Numeric text is always visible. The low threshold is fixed at 0.25. Invalid maximum uses `SiriusInvalidBar`.

### 9.3 `SiriusInputHint`

```text
Prompt : string
Actions : StringName[]
Compact : bool
ActiveDevice : UiInputDevice
Observe(InputEvent)
Refresh()
```

It wraps `InputHintPresenter`, observes input only while visible, and introduces no global service.

### 9.4 `SiriusContextPrompt`

```text
ShowIcon : bool
IconId : UiIconId
Prompt : string
Actions : StringName[]
Compact : bool
Refresh()
```

It composes an optional icon, readable prompt, and `SiriusInputHint`. It does not discover targets or invoke interactions.

### 9.5 `SiriusToastShell`

```text
Severity : SiriusUiSeverity
Title : string
Message : string
Compact : bool
RefreshPresentation()
```

It owns semantic visual presentation only. It has no Timer, Tween, queue, timeout, stacking, acknowledgement, host registration, or lifecycle behavior. HPA-386 owns those concerns.

## 10. Showcase

`SiriusUiShowcase.tscn` remains outside production navigation.

The toolbar contains only:

- approved viewport selector;
- reduced-motion toggle for the local motion demonstration.

There is no background selector or scenic-background loading. The palette section contains fixed dark and light fixtures, which satisfies HPA-377's representative-background requirement without another enum or controller path.

Stress fixtures:

- an action label approximately twice normal English length;
- a 240-character body paragraph;
- a 48-character unbroken metadata token with full tooltip text.

Required sections:

1. palette, surfaces, and both scrims over fixed dark/light fixtures;
2. standard/compact typography and long text;
3. all six Button variations and native states;
4. selected-plus-focused stock toggle;
5. disabled Primary labelled `Loading…`;
6. tabs and tooltips;
7. HP/MP/EXP edge cases;
8. keyboard, mouse, gamepad, fallback, and unbound hints;
9. context prompts;
10. Info/Success/Warning/Error toasts;
11. Small/Medium/Large modal shells;
12. normal and reduced-motion wrapper transitions.

Loading is a static fixture, not a component API.

## 11. Testing

### 11.1 Runtime requirement

Tests that construct `StringName`, Godot vectors, Resources, Nodes, Themes, or scenes use `[RequireGodotRuntime]` and inherit `Node`.

### 11.2 Resource tests

Verify:

- Theme loads and required variations exist;
- fonts load at direct paths;
- required ornament and icon files exist directly through `FileAccess.FileExists` and `ResourceLoader.Exists`;
- native Button states, Ignition textures, panels, bars including `SiriusInvalidBar`, tabs, tooltips, scrollbars, and scrims match the contract;
- enum mappings are exhaustive.

### 11.3 Component tests

Focused files cover:

- modal composition, size/severity, and no-scrim/no-lifecycle ownership;
- stat edge cases and InvalidBar;
- input hint device/binding changes;
- context composition;
- toast semantics without queue, Timer, Tween, or scrim ownership.

Stock Button and Panel behavior is covered by resource tests rather than wrapper-component tests.

### 11.4 Showcase tests

Split into three focused files:

```text
SiriusUiShowcaseStructureTest.cs
SiriusUiShowcaseResponsiveTest.cs
SiriusUiShowcaseFocusTest.cs
```

- **Structure:** named sections, fixed light/dark fixtures, stress fixtures, component types, static Loading, and local motion demo controls.
- **Responsive:** one fixture resized sequentially through all seven approved viewports; safe margins, compact authority, targets, reachability, and wrapping.
- **Focus:** full traversal once in compact mode at 640×360 and once in standard mode at 1280×720. Other aspect ratios reuse the same focus tree and are covered by responsive reachability checks.

No pixel equality is required.

## 12. Documentation

`docs/ui/hpa-377/README.md` is a concise integration guide containing only:

- Theme path and opt-in example;
- public type variations and five component APIs;
- compact authority;
- font/art paths;
- HPA-541 and HPA-386 handoffs;
- prohibition on repeated shared local styles.

It links here for rationale.

## 13. Non-goals and handoffs

HPA-377 does not:

- restyle production screens or globally activate the Theme;
- add Button or Panel subclasses;
- add inventory, battle, save, dialogue, shop, or puzzle domain components;
- add a focus-tracking component;
- add component-owned animation or dismissal behavior;
- implement asynchronous button ownership or loading restoration;
- implement toast/reward queueing or short confirmation seals;
- add settings persistence;
- style unused native controls;
- make screenshots the primary correctness gate.

HPA-541 owns persisted reduced motion. HPA-386 owns toast/reward queueing and short confirmations. HPA-382 owns production modal lifecycle. HPA-356 owns automatic-action presentation. HPA-357 owns slot/component details.

## 14. Alternatives considered

- **Button/Panel subclasses:** rejected because assigning a Theme variation is already native Godot behavior; the wrappers add files, APIs, and tests without new capability.
- **Component-owned Tweens:** rejected because they duplicate lifetime logic and overlap the production owners in HPA-382/HPA-386.
- **Future motion constants:** rejected until the downstream ticket implements that motion.
- **Configurable stat display/threshold:** rejected because HPA-377 has one approved numeric display and threshold.
- **Scenic background selector:** rejected because fixed light/dark fixtures satisfy the ticket with less code.
- **Project-global Theme:** cheaper initially but causes an immediate legacy restyle.
- **Runtime Theme builder/autoload:** adds initialization and lifecycle state to an authored-resource problem.
- **Local styles per migration:** cheapest for one screen but recreates the duplication HPA-377 exists to remove.
- **One generic dark Theme:** insufficient for semantic states, focus/selection, bars, and the approved visual language.
- **Single compact scale:** cannot reproduce non-uniform role reductions.
- **Full native-control coverage:** speculative; extend centrally when demand appears.
- **Four Settings presets only:** insufficient for resizable 4:3, 16:10, and ultrawide shapes.
- **Pixel-golden tests:** fragile across fonts, backends, and headless execution.

## 15. Completion definition

HPA-377 is complete when:

- the canonical opt-in Theme loads with every required resource;
- five components exist with only the APIs above;
- stock Button and Panel variations cover the approved roles without wrapper classes;
- `SiriusInvalidBar` is present as an internal invalid-state variation;
- Ignition is a tested stock Button variation;
- the showcase contains every required state including static Loading and fixed light/dark surfaces;
- one responsive fixture covers all seven sizes;
- focus traversal passes once for compact and once for standard mode;
- all Godot-dependent tests declare runtime requirements;
- no component owns application lifecycle, animation lifetime, or a gameplay singleton dependency;
- the concise guide is complete;
- focused tests, build, and full repository tests pass.