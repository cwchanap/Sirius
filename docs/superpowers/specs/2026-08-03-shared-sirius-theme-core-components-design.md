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
2. seven thin presentation components with named first consumers;
3. an isolated UI showcase;
4. deterministic resource, component, and responsive-layout tests.

Ownership remains split three ways:

- **Theme:** fonts, colours, style boxes, native control states, scrims, spacing, and type variations;
- **components:** small presentation APIs and scene composition;
- **`UIScreenHost` and consuming flows:** stacking, queueing, pause, input, focus restoration, navigation, and lifetime.

The Theme is not configured as `ProjectSettings.gui/theme/custom`. HPA-377 assigns it only to the showcase and test fixtures. Production roots and migrated screens opt in later.

## 2. Complexity constraint

HPA-378 demonstrated the cost of over-generalized foundation work through a concentrated hardening tail around re-entrant mutation, focus restoration, publication ordering, and deferred teardown. HPA-377 therefore follows these guardrails:

- no autoload, registry, coordinator, pure state model, lifecycle service, or generic focus helper;
- no reusable state machine without a current or named first consumer;
- no component-owned domain operation, navigation, queue, or asynchronous task;
- no runtime fallback for required committed Theme assets when a resource-contract test can fail the build;
- no Godot control type is styled merely because the engine provides it;
- no exhaustive lifecycle matrix for controls that own no lifecycle;
- each HPA-377 test file stays below 500 lines;
- re-entrant or teardown combinatorics trigger a design revisit instead of a larger test matrix.

## 3. Approved source baseline

Implementation is grounded in HPA-373:

- specification version: **1.7**;
- design decisions approved: **2026-07-25**;
- source blob: `9e1d1edb366a67a3fa6d0dd02f3641aa0bb42a7d`;
- merged PR: **#17**;
- merge commit: `bc82eadcab27e2321c69fcf56cc3c43e6917b5f5`.

The stale “review candidate” header in that historical file is metadata debt. Linear completion and merged PR #17 are authoritative; the stale header does not block HPA-377.

HPA-374 supplies:

- fonts under `res://assets/fonts/`;
- icons, ornaments, and effects through `UiArtCatalog`;
- `UiIconPresenter` for Button and TextureRect icon application;
- `InputHintPresenter` for device and binding presentation.

HPA-378 supplies the scene-local `UIScreenHost`. HPA-377 components do not depend on it.

## 4. Demand ledger

| Capability | First consumer | HPA-377 treatment |
| --- | --- | --- |
| Labelled actions | Existing menus; HPA-380; HPA-382 | Theme variations plus `SiriusActionButton` |
| Content/HUD/modal surfaces | HPA-380; HPA-381; HPA-382 | Theme variations plus `SiriusPanel` and `SiriusModalShell` |
| HP, MP, EXP bars | HPA-381; HPA-356 | `SiriusStatBar` with three public kinds |
| Input hint | Existing inventory integration; HPA-380; HPA-381 | `SiriusInputHint` around `InputHintPresenter` |
| Context prompt | HPA-381 and explicit HPA-377 scope | `SiriusContextPrompt` |
| Toast visual shell | HPA-386 | `SiriusToastShell`; queueing remains HPA-386 work |
| Ignition seal | HPA-356; HPA-386 | Stock square Button Theme variation |
| Generic focus halo | No proven consumer | Deferred |
| Automatic-action bar | HPA-356 only | Deferred to HPA-356 |
| Telemetry callout/catalogue rail APIs | HPA-356/HPA-357 | Deferred to those tickets |
| Persisted reduced motion | HPA-541 | Explicit component flags only in HPA-377 |

A deferred consumer extends the same central Theme or extracts a component in its ticket. It must not add a screen-local palette or duplicate a shared style.

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

### 5.2 Components

```text
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
```

Direct subclasses with one visual region remain code-first. Multi-node composites receive `.tscn` scenes.

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

`SiriusUiMetrics` exposes only HPA-377 needs:

```text
Space4 / Space8 / Space12 / Space16 / Space24 / Space32 / Space48
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
```

Slot dimensions are not included; no slot component exists in HPA-377. HPA-357 owns slot metrics when it consumes them.

### 6.4 Interactive states

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

### 6.5 Buttons

`SiriusActionButtonVariant` contains:

```csharp
Primary,
Secondary,
Tertiary,
Warning,
Destructive
```

Each maps to one Theme variation with `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, and `disabled` resources.

`SiriusActionButton` inspector API:

```text
Variant : SiriusActionButtonVariant
ShowIcon : bool
IconId : UiIconId
IconSize : UiIconSize = UiIconSize.Default
DisabledReason : string
```

Runtime convenience API:

```csharp
void SetIcon(UiIconId? icon)
```

Godot exports regular enums but exported members must be Variant-compatible; nullable enums are not an inspector-safe contract. `ShowIcon` is therefore the authoritative presence flag, and `IconId` is inert when `ShowIcon == false`. `SetIcon()` changes both atomically for runtime callers.

`DisabledReason` behavior:

- `TooltipText` is populated only while `Disabled == true`;
- enabled buttons do not advertise a disabled reason;
- the component leaves `MouseFilter` non-Ignore so disabled mouse hover can resolve the tooltip;
- tests assert `GetTooltip()` on a disabled button;
- keyboard/gamepad flows that skip disabled controls must also expose the reason in their caller-owned visible detail surface.

The component preserves stock Button selection, focus, disabled, and activation behavior. It owns no loading or task lifecycle.

#### Ignition

Ignition is a stock square `Button` using `SiriusIgnitionButton`:

- required `ignition_seal.png` reused by native state `StyleBoxTexture` resources;
- preferred size 96×96 standard and 80×80 compact;
- label centered, at most two lines, 16 px inset;
- focus uses required cyan `focus_halo.png`;
- resource tests fail if either required asset is absent;
- there is no runtime fallback path.

Localized text that does not fit uses a conventional Primary action.

### 6.6 Surfaces and scrims

`SiriusPanelSurface` contains:

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

Warning and Error panels are selected internally by modal/toast severity, not public panel-surface enum values.

Scrims use `night-1000` at 58% and 72%. A host or caller creates them. `SiriusModalShell` and `SiriusToastShell` do not own a scrim.

### 6.7 Bars

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

`SiriusInvalidBar` is an internal presentation variation for `Maximum <= 0`; it is not a public stat kind.

- HP uses danger/rose;
- MP uses cyan;
- EXP uses gold;
- low, overflow, negative, and invalid states include text or markers.

Automatic-action progress remains HPA-356 work.

### 6.8 Other native controls

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

`SiriusMotion` contains the approved classes:

| Profile | Duration | First consumer |
| --- | ---: | --- |
| Control feedback | 120 ms | Current buttons; HPA-380/HPA-382 |
| Callout entry | 220 ms | Modal/toast in HPA-377; later callouts |
| Callout exit | 180 ms | Modal/toast in HPA-377; later callouts |
| Screen transition | 280 ms | HPA-380/HPA-382 |
| Orrery transformation maximum | 400 ms | HPA-356/HPA-357 |
| Reduced-motion opacity maximum | 100 ms | HPA-377, persisted by HPA-541 |

The constants are retained because each has a current or named first consumer and prevents downstream tickets from redefining timing. HPA-377 components animate only modal/toast entry and exit.

Reduced motion replaces transforms and pulses with static state or opacity no longer than 100 ms. Components receive it explicitly and never read `SettingsManager`.

## 9. Components

### 9.1 `SiriusActionButton`

Base: `Button`.

Responsibilities:

- map five variants;
- apply optional icons through the inspector encoding or `SetIcon()`;
- expose `DisabledReason` only when disabled;
- preserve stock Button behavior.

### 9.2 `SiriusPanel`

Base: `PanelContainer`.

```text
Surface : SiriusPanelSurface
```

It maps four values and owns no layout or domain state.

### 9.3 `SiriusModalShell`

Scene-authored rectangular observatory plate:

```text
Title : string
Severity : SiriusUiSeverity
SizeClass : SiriusModalSizeClass
Compact : bool
ReducedMotion : bool
ShowCloseAffordance : bool
BodyHost : Control
ActionsHost : Control
CloseRequested signal
```

It owns composition, responsive width, and visual-only entry/exit motion. It does not create a scrim, register with a host, choose focus, intercept Cancel, dismiss itself, or choose a domain action.

### 9.4 `SiriusStatBar`

```text
Kind : SiriusStatBarKind
Current : double
Maximum : double
Label : string
ShowNumericValue : bool
LowThreshold : double = 0.25
Compact : bool
```

Visual fill clamps to range; displayed values preserve caller data. Invalid maximum uses `SiriusInvalidBar`.

### 9.5 `SiriusInputHint`

```text
Prompt : string
Actions : StringName[]
Compact : bool
ActiveDevice : UiInputDevice
Observe(InputEvent)
Refresh()
```

It wraps `InputHintPresenter`, observes input only while visible, and introduces no global service.

### 9.6 `SiriusContextPrompt`

```text
ShowIcon : bool
IconId : UiIconId
Prompt : string
Actions : StringName[]
Compact : bool
Refresh()
```

It composes an optional icon, readable prompt, and `SiriusInputHint`. It does not discover targets or invoke interactions. It remains in scope because HPA-377 explicitly names it and HPA-381 is its first consumer.

### 9.7 `SiriusToastShell`

```text
Severity : SiriusUiSeverity
Title : string
Message : string
Compact : bool
ReducedMotion : bool
```

It owns semantic visual presentation and entry/exit motion only. HPA-386 owns queueing, timeout, stacking, host registration, and lifecycle.

## 10. Showcase

`SiriusUiShowcase.tscn` remains outside production navigation.

Deterministic backgrounds:

1. `night-1000` solid;
2. `moon-50` solid;
3. retained main-menu scenic background;
4. retained battle scenic background.

Stress fixtures:

- an action label approximately twice normal English length;
- a 240-character body paragraph;
- a 48-character unbroken metadata token with full tooltip text.

Required sections:

1. palette, surfaces, and both scrims;
2. standard/compact typography and long text;
3. all five action variants and native states;
4. standard/compact stock Ignition;
5. selected-plus-focused stock toggle;
6. disabled Primary labelled `Loading…`;
7. tabs and tooltips;
8. HP/MP/EXP edge cases;
9. keyboard, mouse, gamepad, fallback, and unbound hints;
10. context prompts;
11. Info/Success/Warning/Error toasts;
12. Small/Medium/Large modal shells;
13. normal and reduced-motion modal/toast transitions.

Loading is a static fixture, not a component API.

## 11. Testing

### 11.1 Runtime requirement

Tests that construct `StringName`, Godot vectors, Resources, Nodes, Themes, or scenes use:

```csharp
[TestSuite]
[RequireGodotRuntime]
public partial class ExampleTest : Node
```

Only genuinely pure C# suites omit the runtime attribute and Node base.

### 11.2 Resource tests

Verify:

- Theme loads and required variations exist;
- fonts load at direct paths;
- required ornament and icon files exist directly through `FileAccess.FileExists` and `ResourceLoader.Exists`;
- icon existence is not inferred through `UiArtCatalog.LoadIcon` or `UiIconPresenter.Apply`, because the catalog may substitute the Info icon;
- native Button states, Ignition textures, panels, bars including `SiriusInvalidBar`, tabs, tooltips, scrollbars, and scrims match the contract;
- enum mappings are exhaustive.

### 11.3 Component tests

Focused files cover:

- ActionButton mapping, icon presence encoding, `SetIcon()`, and disabled-reason gating;
- disabled Button `GetTooltip()` while `MouseFilter` is non-Ignore;
- panel mapping;
- modal composition, size/severity, no-scrim ownership, and reduced motion;
- stat edge cases and InvalidBar;
- input hint device/binding changes;
- context composition;
- toast semantics without queue ownership.

Do not duplicate native Button lifecycle tests.

### 11.4 Showcase tests

Split into three files to preserve the 500-line guard:

```text
SiriusUiShowcaseStructureTest.cs
SiriusUiShowcaseResponsiveTest.cs
SiriusUiShowcaseFocusTest.cs
```

- **Structure:** named sections, backgrounds, stress fixtures, component types, static Loading fixture, and required resources.
- **Responsive:** one fixture resized sequentially through all seven approved viewports; safe margins, compact authority, targets, reachability, and wrapping.
- **Focus:** full keyboard/gamepad traversal at 640×360, 1280×720, 1024×768, and 2560×1080.

No pixel equality is required.

## 12. Documentation

`docs/ui/hpa-377/README.md` is a concise integration guide containing only:

- Theme path and opt-in example;
- public variations and component APIs;
- compact authority;
- font/art paths;
- HPA-541 and HPA-386 handoffs;
- prohibition on repeated shared local styles.

It links here for rationale.

## 13. Non-goals and handoffs

HPA-377 does not:

- restyle production screens or globally activate the Theme;
- add inventory, battle, save, dialogue, shop, or puzzle domain components;
- add a focus-tracking component;
- implement asynchronous button ownership or loading restoration;
- implement toast/reward queueing or short confirmation seals;
- add settings persistence;
- style unused native controls;
- make screenshots the primary correctness gate.

HPA-541 owns persisted reduced motion. HPA-386 owns toast/reward queueing and short confirmations. HPA-356 owns automatic-action presentation. HPA-357 owns slot/component details.

## 14. Alternatives considered

- **Project-global Theme:** cheaper initially but causes an immediate legacy restyle.
- **Runtime Theme builder/autoload:** adds initialization and lifecycle state to an authored-resource problem.
- **Local styles per migration:** cheapest for one screen but recreates the duplication HPA-377 exists to remove.
- **One generic dark Theme:** insufficient for semantic states, focus/selection, bars, and approved visual language.
- **Single compact scale:** cannot reproduce non-uniform role reductions.
- **Full native-control coverage:** speculative; extend centrally when demand appears.
- **Reusable loading/focus helpers:** deferred until a real consumer proves the API.
- **Four Settings presets only:** insufficient for resizable 4:3, 16:10, and ultrawide shapes.
- **Pixel-golden tests:** fragile across fonts, backends, and headless execution.

## 15. Completion definition

HPA-377 is complete when:

- the canonical opt-in Theme loads with every required resource;
- seven components exist with only the APIs above;
- `SiriusInvalidBar` is present as an internal invalid-state variation;
- Ignition is a tested stock Button variation;
- the showcase contains every required state including static Loading;
- one responsive fixture covers all seven sizes;
- focus traversal passes at four representative shapes;
- all Godot-dependent tests declare runtime requirements;
- no component depends on application singletons, `UIScreenHost`, or a new lifecycle service;
- the concise guide is complete;
- focused tests, build, and full repository tests pass.