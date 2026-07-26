# Sirius UI Visual Language and Screen Design

**Version:** 1.0  
**Status:** Review candidate — design approved section by section; written artifact review pending  
**Linear:** HPA-373  
**Design decisions approved:** 2026-07-25

## 1. Purpose

This specification defines the visual language, responsive layouts, interaction rules, component states, platform policy, and asset requirements for the Sirius UI revamp.

The approved direction is **Constellation Grimoire**: a mystical anime-fantasy interface built from deep indigo surfaces, cyan magical focus, gold primary emphasis, and restrained constellation and sigil ornament. Menus and rewards express celestial hope; exploration and battle deepen toward arcane violet and magenta while retaining the same component system.

This document is the implementation baseline for HPA-374, HPA-354, HPA-355, HPA-356, HPA-357, and HPA-358. Downstream work must not invent new colours, typography, spacing, control states, or screen rules without updating this versioned specification.

## 2. Scope and non-goals

This design covers:

- Main menu and deterministic Continue presentation
- Exploration HUD
- Battle encounter, preparation, automatic combat, and results
- Inventory, equipment, accessories, consumables, and active-skill configuration
- Pause, settings, save, and load
- Dialogue, shop, and healing
- Puzzle, reward, confirmation, warning, and error presentation
- Mouse, keyboard, and gamepad interaction
- Desktop landscape layouts from 640×360 through ultrawide

This design does not:

- Change combat, inventory, save, settings, dialogue, shop, healing, puzzle, or reward domain rules
- Add manual combat, battle speed, combat pause, or battle skill editing
- Add inventory comparison, filters, sorting, Drop, Favourite, Lock, or bulk actions
- Add minimap, objective, quick-item, cooldown, or empty future-feature widgets
- Design touch-first, portrait, or mobile-specific layouts
- Replace world, character, enemy, or floor artwork

## 3. Current-state baseline

The captured baseline is indexed in [Baseline evidence](../../ui/hpa-373/baseline/README.md). Runtime screenshots use the current scenes and controllers. Isolated dialogue, shop, healing, puzzle, reward, confirmation, and error fixtures use representative content rendered through the current controller or default Godot dialog presentation without changing domain data.

| Flow | Principal current problem |
|---|---|
| Main menu | Generic centred control stack obscures the background focal area and has no Continue hierarchy or save summary. |
| Exploration | Oversized draggable debug panel, visible Lock control, raw statistics, and permanent instructions dominate the playfield. |
| Battle | Desktop `AcceptDialog` framing, weak information hierarchy, and no distinct visual composition for preparation, combat, and results. |
| Inventory | Fixed 1240×760 workbench layout, inconsistent slot sizes, emoji headings, and no minimum-resolution navigation model. |
| Pause | Generic desktop dialog layered over an already dense debug HUD. |
| Settings | Most presentation is constructed at runtime in C# and appears as an unframed utility panel. |
| Save/load | Generic button rows do not communicate slot identity, metadata state, or autosave distinction as cards. |
| Dialogue/shop/healing | Separate desktop dialogs share no coherent NPC identity, action, currency, or feedback language. |
| Puzzle/reward/error | Generic dialogs make puzzles, important rewards, warnings, and errors visually indistinguishable. |

## 4. Platform and viewport policy

### 4.1 Supported environment

- Primary target: desktop landscape
- Input: mouse, keyboard, and gamepad
- Window modes: resizable window and fullscreen
- Supported shapes: 4:3, 16:10, 16:9, and ultrawide landscape
- Touch-first, portrait, and mobile-specific presentation: outside this revamp

### 4.2 Reference sizes

- Reference design canvas: 1280×720
- Minimum supported logical resolution: 640×360
- Compact reflow: usable width below 800 logical pixels or height below 450
- Standard safe margin: 24 px
- Compact safe margin: 12 px
- Ultrawide content frame: centred, maximum width 1600 px

Validation is required at:

- 640×360
- 1024×768
- 1280×720
- 1440×900
- 1920×1080
- 2560×1080
- 2560×1440

Background artwork uses aspect-preserving cover crop. Non-uniform scaling is prohibited. Art-directed alignment preserves the main-menu castle and moon and the battle arena’s opposing flame regions. Primary controls and HUD elements stay inside the safe frame instead of moving to distant ultrawide edges.

## 5. Visual foundation

### 5.1 Core palette

| Role | Token | Hex |
|---|---|---|
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
| Danger and destructive | `danger-400` | `#F16D83` |

Selection uses gold. Keyboard and gamepad focus use a separate 2 px cyan outer ring. Selection and focus may coexist, and no state is communicated by colour alone.

### 5.2 Typography

- Wordmark and major fantasy heading: bundled **Cinzel SemiBold**
- Controls, body, data, and localization: bundled **Noto Sans**
- Decorative text falls back to Noto Sans when its glyph coverage is incomplete.
- Body text and numeric data never use the decorative face.

| Role | Standard | Compact |
|---|---:|---:|
| Display | 44 px | 30 px |
| Screen title | 28 px | 22 px |
| Section title | 20 px | 17 px |
| Body and control | 16 px | 14 px |
| Metadata | 14 px | 12 px |

Nothing renders below 12 logical pixels.

### 5.3 Geometry and surfaces

- Spacing scale: `4 / 8 / 12 / 16 / 24 / 32 / 48`
- Slot radius: 4 px
- Control radius: 8 px
- Panel radius: 12 px
- Feature-panel radius: 16 px
- Normal border: 1 px
- Selected and focus border: 2 px
- Content panel opacity: 90%
- HUD plate opacity: 82%
- Modal opacity: 96%
- Full-screen scrim: 58% black-navy
- Child-modal scrim: 72% black-navy

Shadows are soft navy-black and appear only beneath raised panels and modals.

### 5.4 Ornament

Approved ornament consists of fine constellation lines, four-point stars, small orbit marks, and partial circular sigils. Ornament belongs around major titles, selected feature cards, battle and result frames, or intentionally empty corners.

Routine controls, settings rows, lists, and dense slot grids remain quiet. Circuit grids, chrome bevels, dense runic wallpaper, and science-fiction dashboard motifs are prohibited.

## 6. Component and state matrix

### 6.1 Universal interactive states

| State | Required treatment |
|---|---|
| Normal | Indigo surface with 1 px muted border |
| Hover | Brighter surface and restrained cyan edge light |
| Pressed | Darker fill and 1 px visual depression |
| Focus | 2 px cyan outer ring and explicit focus marker |
| Selected | 2 px gold border, gold marker/check, and persistent tint |
| Disabled | 45% opacity, no glow, and readable reason in tooltip or details |
| Warning | Amber icon, border, and explicit warning text |
| Destructive | Rose-red icon and border; filled red reserved for the final confirmation |

Minimum target size is 44×44 standard and 40×40 compact.

### 6.2 Buttons

- Primary: gold emphasis; one dominant primary action per decision area
- Secondary: indigo surface with cyan interaction feedback
- Tertiary: text or ghost treatment
- Destructive: outlined danger treatment until final confirmation
- Buttons include a text label even when an icon is present.

### 6.3 Panels

Four panel variants are approved:

- Base content panel
- Raised feature panel
- Compact HUD plate
- Blocking modal

No screen creates an additional panel variant solely to achieve a one-off colour.

### 6.4 Bars

- HP: rose-red
- MP: cyan
- EXP: gold
- Automatic-action progress: magenta

Every bar retains a visible track and a label or numeric value. Low-resource presentation adds icon or text feedback instead of flashing colour alone.

### 6.5 Item and equipment slots

- Standard size: 56 px
- Compact size: 48 px
- Shared geometry for inventory, equipment, and battle-item use
- Equipment adds slot-type glyph and label.
- Items show icon, quantity, supported state, and rarity accent when real rarity data exists.
- Rarity never replaces focus or selection treatment.
- Locked accessory slots combine a lock glyph, muted treatment, and reason.

### 6.6 Icons and input hints

- Default icon: 24 px
- Metadata icon: 16 px
- Feature icon: 32 px
- Consistent 2 px dark outline and simplified internal shapes
- Production emoji are prohibited.

Input hints pair a device glyph with text and update when the active input device or binding changes. An unknown binding falls back to a readable text label.

### 6.7 Tabs and tooltips

Tabs form one focus group. Gold indicates selection; cyan independently indicates focus.

Tooltips are capped at 360 px standard and 280 px compact. Keyboard and gamepad focus expose the same information available to mouse hover. Tooltip-only information must remain reachable without a mouse.

### 6.8 Notifications

- Brief acknowledgement: non-blocking toast
- Warning that does not require action: longer-lived toast
- Recoverable choice: child modal
- Flow-blocking failure: blocking modal

Toasts stay inside the safe frame, avoid critical HUD data, queue deterministically, and never capture gameplay input.

## 7. Modal, focus, and lifecycle rules

### 7.1 Modal geometry

- Small: 420 px
- Medium: 640 px
- Large: 960 px
- Maximum: 90% of viewport
- Compact: viewport minus 12 px margins

Titles and actions remain fixed while long modal bodies scroll internally.

### 7.2 Stack and dismissal

- The parent remains visible but inert beneath a child modal.
- Only one child modal may appear over a screen.
- A third nested actionable layer is prohibited.
- Outside clicks never dismiss modals.
- Cancel closes the topmost ordinary layer.
- Overwrite, delete, quit-with-risk, and destructive confirmations require an explicit button.
- Dropdown popups and key capture consume Cancel before their parent screen.

### 7.3 Screen lifecycle contract

Every screen declares:

- Parent context
- Pause behavior
- HUD visibility
- Cursor policy
- Initial focus
- Cancel behavior
- Restoration target

The topmost screen alone receives UI input. On dismissal, focus returns to the invoking control when it still exists; otherwise it returns to the screen’s safe default.

Domain controllers remain responsible for combat, saves, inventory, dialogue, rewards, and settings. Presentation binds state and invokes existing operations. UI code does not grant rewards, repair saves, or fabricate domain data.

## 8. Motion and sound

| Motion class | Duration |
|---|---:|
| Control feedback | 100 ms |
| Panel enter/exit | 180 ms |
| Screen transition | 240 ms |
| Encounter or result emphasis | Up to 320 ms |

Movement is limited to 8–16 logical pixels with soft ease-out. Looping ornament must not compete with content or gameplay.

Reduced-motion mode:

- Removes translation, scaling, parallax, flashes, and looping effects
- Retains opacity transitions of at most 100 ms
- Preserves all state and timing information without animation

UI sound follows the same hierarchy: soft focus, clear activation, restrained warning, and distinctive reward cues.

## 9. Screen specifications

The paired desktop and compact wireframes are in [Screen wireframes](../../ui/hpa-373/wireframes/screen-wireframes.svg).

### 9.1 Main menu

Desktop uses the left third of the safe frame:

1. Sirius title
2. Continue
3. Selected Continue save summary
4. New Game
5. Load
6. Settings
7. Quit
8. Input hints lower-left
9. Version lower-right

The castle and moon remain unobstructed on the right.

Continue policy:

- Inspect manual slots 0–2 and autosave slot 3 through save metadata.
- Ignore missing and metadata-corrupted saves.
- Choose the newest valid timestamp.
- Timestamp ties resolve in this order: autosave, manual slot 0, slot 1, slot 2.
- Show the exact selected save’s player name, level, floor/location, and timestamp when available.
- When no valid metadata exists, Continue remains visible but disabled, explains why, and is skipped by focus.
- Activation performs full load validation.
- A load failure shows the themed error and opens Load; it never starts a fresh game.
- Continue does not mutate, repair, or delete invalid saves.
- Double activation is blocked during transition.

At 640×360, the same order remains. The save summary becomes two lines and uses compact spacing.

### 9.2 Exploration HUD

Top-left HUD plate:

- Portrait or graceful identity fallback
- Name and level
- HP
- MP when supported
- Thin EXP line

Gold remains in inventory and shop screens. Raw ATK, DEF, SPD, the `Player HUD` heading, drag affordance, Lock control, permanent instructions, and area-colour documentation are removed from normal play.

A floor or area title appears briefly at top centre. A bottom-centre interaction prompt appears only for a valid target, reflects active input bindings, and disappears during incompatible states.

At compact size, the portrait becomes 40 px and bars shorten. Missing optional data collapses without leaving empty frames.

### 9.3 Battle flow

The retained battle background fills the screen. Player and enemy plates remain in stable opposing positions through all states.

Encounter:

- Brief transition from exploration
- Gameplay input behind the battle screen is disabled.

Preparation:

- Player and enemy identity, level, HP/MP where applicable, core preview, and active-skill summary
- Lower-third consumable tray with quantity, effect, target, disabled reason, selection, clear selection, and error
- Explicit gold **Begin Battle** action
- No automatic start

Automatic combat:

- Preparation tray collapses to current action, automatic-action progress, status, damage feedback, compact event feed, supported cure-item access, and Escape
- No manual Attack, Defend, battle-speed, general pause, or skill-editing controls

Results:

- Lower strip becomes victory, defeat, escape, loot, reward, and continuation presentation
- Exploration does not resume before required information is acknowledged.

At 640×360, preparation uses `Overview` and `Items` pages. Automatic combat abbreviates the feed to the latest entries and puts cure selection in the standard modal.

The polished quality reference is [Battle preparation reference](../../ui/hpa-373/reference/battle-preparation-reference.png); its editable vector source is stored beside it.

### 9.4 Inventory and equipment

Desktop:

- Stable character identity header
- Left region: portrait, level, supported statistics, five primary equipment slots, accessory slots, and locked states
- Right region: deterministic alphabetical inventory grid
- Active-skill configuration belongs with character configuration.
- Existing tooltip detail remains available, supplemented only by a lightweight focus summary.

Compact:

- Persistent character identity strip
- `Equipment`, `Items`, and `Skills` pages
- Shoulder-button, keyboard, and clickable tab navigation
- Focus and last page preserved across responsive transitions where possible

Current equip, unequip, consume, capacity rollback, locked accessory, quantity, and explicit no-active-skill behavior remain unchanged. Persistent comparison, filters, user sorting, Drop, Sell, Favourite, Lock, and bulk actions are not added.

### 9.5 Pause

The frozen game remains visible beneath the full-screen scrim. A 420 px centred panel contains:

1. Resume
2. Save
3. Load
4. Settings
5. Quit to Main Menu

Resume is initially focused. Cancel resumes. Quit opens the destructive child confirmation. Compact mode uses a 320 px panel.

### 9.6 Settings

Standard:

- Left tab rail: Audio, Display, Gameplay, Controls
- Active settings page in the main pane
- Fixed Apply and Cancel actions

Compact:

- Short top page selector
- Only the active page scrolls

Staged edits, Apply/Cancel, resolution, fullscreen, auto-save, difficulty, audio, key capture, duplicate/reserved handling, dropdown cancellation, mouse, keyboard, and gamepad behavior remain unchanged.

### 9.7 Save and load

Four cards represent three manual slots and one visually distinct autosave.

Each card shows only reliably available metadata:

- Slot or Autosave identity
- Player name and level
- Floor/location
- Timestamp
- Empty, valid, corrupted, incompatible, selected, loading, or failure state

Standard uses a 2×2 grid. Compact uses a single scrolling list. Save, Overwrite, Delete, Load, and disabled reasons appear only where supported. Autosave is read-only in Save mode. Overwrite and Delete use child confirmations. Cancel closes the child before returning to Main Menu or Pause.

### 9.8 Dialogue

A wide bottom panel is centred inside the safe frame:

- NPC portrait and name in the identity area
- Dialogue text in the flexible body
- Choice list and continuation action
- Close and input hints

Long text scrolls independently. Compact mode reduces the portrait and permits the panel to grow upward without losing choices.

### 9.9 Shop

A large interaction panel contains:

- NPC identity
- Player gold
- Buy and Sell tabs
- Item rows with name, quantity, price, stock where supported, and action
- Disabled reason and feedback
- Close action

Compact mode presents one list page at a time. Existing affordability, stock, purchase, sale, failure, and double-activation behavior remain unchanged.

### 9.10 Healing

A small modal contains:

- NPC identity
- Current and maximum HP
- Cost
- Available gold
- Feedback
- Heal
- No Thanks

Unavailable healing includes a readable reason.

### 9.11 Puzzle

A medium sigil-framed panel contains title, prompt, answer choices or input, validation feedback, and Cancel. Compact mode fills the safe area and scrolls long content.

### 9.12 Rewards

- Brief single reward: queued non-blocking toast
- Important reward, multiple-item award, battle result, or required acknowledgement: result card
- Show only supported item icon/name, quantity, currency, experience, or level change.
- UI presents but never grants rewards.
- Duplicate domain emissions must not produce duplicate presentation.

### 9.13 Confirmations, warnings, and errors

Approved variants:

- Informational confirmation
- Destructive confirmation
- Warning
- Recoverable error
- Blocking error

Each includes icon, title, actionable message, safe default focus, double-activation guard, and deterministic parent restoration.

## 10. Error escalation

- Recoverable validation or unavailable action: inline feedback
- Non-blocking acknowledgement: toast
- Recoverable choice or confirmation: child modal
- Unsafe-to-continue state: blocking modal

Failures preserve the user’s context and domain state. Save corruption, load failure, missing NPC data, missing optional art, and unavailable actions are never silently converted into success.

## 11. Asset inventory

### 11.1 Retain

| Asset class | Current asset | Decision |
|---|---|---|
| Main-menu background | `assets/sprites/ui/ui_main_menu_background.png` | Retain as production scenic anchor |
| Battle background | `assets/sprites/ui/ui_battle_background.png` | Retain as production scenic anchor |
| Player, enemy, and NPC sprites | Existing sprite sheets and frames | Reuse as content imagery |
| Item icons | Existing item PNGs | Reuse in slots, rewards, shops, and battle preparation |

### 11.2 Replace or retire

- Emoji headings and emoji-as-icon presentation
- Default Godot and operating-system-style dialog framing
- Duplicated local style overrides
- Temporary text-only icon substitutes
- Current draggable HUD and visible Lock control
- Permanent exploration instructions and area-colour legend
- Deprecated UI button prompt art that encodes unsupported manual battle actions

### 11.3 Produce under HPA-374

- Bundled Cinzel SemiBold
- Bundled Noto Sans
- Cohesive 16/24/32 px UI icon set
- Mouse, keyboard, and gamepad input glyphs
- Equipment-slot and status symbols
- Reusable panel edge, constellation corner, and partial-sigil ornament
- Minimum encounter, impact, status, and reward effects

No new world, character, enemy, or floor artwork is required by this specification.

## 12. Validation requirements

Every downstream screen migration must validate:

- All seven target viewport sizes
- 12 px minimum logical text
- 24 px standard and 12 px compact safe margins
- Ultrawide 1600 px content frame
- Representative long and localized strings
- Mouse, keyboard, and gamepad focus
- Selected plus focused state coexistence
- Reduced-motion behavior
- Missing portrait/icon fallback
- Empty, corrupted, incompatible, disabled, warning, and failure states
- Topmost Cancel and focus restoration
- Double-activation prevention
- Existing domain behavior and rollback semantics

The HPA-373 artifact package itself is complete when:

- The baseline set is present and indexed.
- Every required screen has paired 1280×720 and 640×360 wireframes.
- The battle preparation reference demonstrates the final visual language and normal, focused, selected, and disabled states.
- This specification contains no unresolved placeholder or contradictory rule.
- The product owner approves this written repository version before production Theme and screen layout work begins.
