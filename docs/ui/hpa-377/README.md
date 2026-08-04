# Sirius Theme and Shared Components

## Design

HPA-377 provides one authored, opt-in `Theme` and five scene-authored
presentation components. The Theme is the source of truth for shared fonts,
palette, `StyleBoxFlat` resources, spacing, native-control states, and stable
type variations. Use stock Godot controls wherever the Theme is sufficient;
use a component only for its documented composite presentation behavior.

Components do not own navigation, queueing, dismissal, focus restoration,
asynchronous work, or animation lifetime. The consuming screen or its
presentation owner owns those policies.

## Opt in

Load the Theme at the root of the screen being migrated. HPA-377 is deliberately
opt-in: it does not globally restyle existing production screens.

```csharp
var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
screenRoot.Theme = theme;
button.ThemeTypeVariation = SiriusThemeTypes.PrimaryButton;
panel.ThemeTypeVariation = SiriusThemeTypes.ContentPanel;
```

Keep the Theme on the migrated screen root so children inherit it. Scene-local
fixtures and test roots follow the same pattern.

## Compact authority

Only a Viewport/SubViewport owner calls SiriusUiMetrics.IsCompact(). That
owner passes the resulting `Compact` value to its local labels, stock controls,
and HPA-377 components, then refreshes presentation when the viewport size
changes. Components receive compact mode; they do not inspect a global window
or decide the application-wide layout policy.

Use `SiriusUiMetrics` for the approved compact breakpoint, safe margin,
minimum target, modal width, tooltip maximum, and Ignition size. Keep
screen-specific layout decisions with the screen owner.

## Stock variations

Select variations directly with `SiriusThemeTypes`. Use the stock `Button`
variations (`PrimaryButton`, `SecondaryButton`, `TertiaryButton`,
`WarningButton`, `DestructiveButton`, and `IgnitionButton`) and stock
`Panel`/`PanelContainer` variations (`ContentPanel`, `FeaturePanel`,
`HudPlate`, modal/severity panels, and scrims). Labels and bars likewise use
the stable typography and stat-bar variations defined in `SiriusThemeTypes`.

Ignition is SiriusIgnitionButton, not a component. It is a stock square
`Button` with the Ignition Theme variation, required seal/focus assets, and a
size chosen by the viewport owner through `SiriusUiMetrics.IgnitionSize()`.

## Components

The approved components are presentation-only scene composites:

- `SiriusModalShell`: severity-styled modal surface with title, `BodyHost`, and
  `ActionsHost`.
- `SiriusStatBar`: fixed Health, Mana, or Experience value presentation with
  visible numeric values and a fixed low threshold.
- `SiriusInputHint`: prompt, active-device icon, and resolved action binding.
- `SiriusContextPrompt`: optional semantic icon, prompt, and embedded input
  hint.
- `SiriusToastShell`: severity-styled title and message shell.

Instantiate a component only when its composition is needed. The screen owner
still supplies content, calls refresh methods as its state changes, and owns
all lifecycle policy around it.

## Handoffs

HPA-541 owns persisted reduced motion. HPA-377 supplies only the shared motion
policy and the showcase-local demonstration.

HPA-386 owns toast/reward queueing and short confirmation seals. A
`SiriusToastShell` is only the visual shell; it does not queue, time, or dismiss
notifications.

HPA-382 owns production modal lifecycle and dismissal. A `SiriusModalShell`
does not create a production modal flow or decide how it closes.

## Prohibited patterns

Do not set ProjectSettings.gui/theme/custom during an isolated migration.
Do not create Button or Panel subclasses solely to assign a Theme variation.
Do not repeat shared StyleBoxFlat resources or palette values.

Extend the central Theme instead. Do not add a global registry, coordinator,
autoload, generic focus helper, or component-owned lifecycle state for this
presentation layer.
