# HPA-373 Current UI Baseline

Captured from commit `ba5fb37` on 2026-07-25 at 1280×720 using Godot 4.6.2 Mono.

The main menu, exploration, inventory, pause, settings, save, and load images are rendered through the current runtime scenes and input flows. Battle is rendered from the current `BattleScene.tscn` before combatant initialization. Dialogue, shop, healing, and puzzle use their current C# controller layouts populated with representative fixture text. Reward, confirmation, and error images use the same default Godot dialog presentation employed by the current flows. Fixture content is illustrative; the captured structure and styling are current.

No screenshot fixture changed or persisted game-domain state.

## Screen index

| Flow | Evidence | Principal observation |
|---|---|---|
| Main menu | [main-menu-current.png](main-menu-current.png) | Centred default controls compete with the castle focal area; no Continue or save summary. |
| Exploration | [exploration-current.png](exploration-current.png) | Debug HUD and permanent area legend cover a large portion of the playfield. |
| Battle | [battle-current.png](battle-current.png) | Battle uses a desktop `AcceptDialog` with default styling and weak state separation. |
| Inventory | [inventory-current.png](inventory-current.png) | Fixed desktop split, inconsistent 108 px accessory and 40 px inventory slots, and emoji headings. |
| Pause | [pause-current.png](pause-current.png) | Generic modal appears over the already dense debug HUD. |
| Settings | [settings-current.png](settings-current.png) | Runtime-built utility layout lacks a clear modal shell and visual hierarchy. |
| Save | [save-current.png](save-current.png) | Slots are generic rows; autosave and metadata states lack card hierarchy. |
| Load | [load-current.png](load-current.png) | Load repeats the generic row treatment and provides little state explanation. |
| Dialogue | [dialogue-current.png](dialogue-current.png) | Speaker, body, and choices use default dialog presentation without NPC identity treatment. |
| Shop | [shop-current.png](shop-current.png) | Dense text rows and default tabs do not establish price, affordability, or item hierarchy. |
| Healing | [healing-current.png](healing-current.png) | Functional confirmation lacks status, cost, and NPC identity hierarchy. |
| Puzzle | [puzzle-current.png](puzzle-current.png) | Puzzle is visually indistinguishable from a generic utility prompt. |
| Reward | [reward-current.png](reward-current.png) | Important rewards appear as a tiny default acknowledgement dialog. |
| Confirmation | [confirmation-current.png](confirmation-current.png) | Destructive intent is not visually distinct from ordinary confirmation. |
| Error | [error-current.png](error-current.png) | Error severity and recovery action are not communicated by a shared semantic system. |

## Asset baseline

The only shipped UI-specific artwork is:

- `assets/sprites/ui/ui_main_menu_background.png` — 1920×1080
- `assets/sprites/ui/ui_battle_background.png` — 1280×720

Both are approved for retention. Existing character, enemy, NPC, and item art can be reused as content imagery. The repository currently has no bundled UI font family, cohesive UI icon set, input glyph set, reusable ornament, or production component sprites.

