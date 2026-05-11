# Treasure Box System Design

## Summary

Add a persistent treasure box system for the Ground Floor and Floor 1. Treasure boxes are authored static floor entities, opened deliberately by the player, play polished open animations, and award fixed curated rewards. Opened boxes persist in save data and cannot be looted again.

## Goals

- Place dense authored treasure boxes on both Ground Floor and Floor 1.
- Let the player open a box by pressing the existing input binding while adjacent and facing it.
- Show player-facing action text as `Open`, not `Interact`.
- Play a treasure box opening animation when the box is opened.
- Award fixed curated rewards per box: gold, consumables, and a small number of equipment upgrades.
- Persist opened treasure box IDs in save files so each box is one-time per save.
- Validate placement so boxes do not overlap other entities or break required floor routes.

## Non-Goals

- Random treasure loot tables.
- Respawning or resettable treasure.
- Trap, mimic, locked, or key-based treasure variants.
- A broad world-object interaction framework beyond what treasure boxes need.

## Architecture

Treasure boxes should follow the same static floor-content pattern as `EnemySpawn` and `NpcSpawn`.

New runtime pieces:

- `TreasureBoxSpawn`: a scene node under a floor's `GridMap`.
  - Exports `TreasureBoxId`, `GridPosition`, and curated reward fields.
  - Belongs to the `TreasureBoxSpawn` group.
  - Renders a closed, opening, or open box state.
  - Falls back to a simple visible drawing if sprite art is unavailable.
- `TreasureReward`: a small data model for fixed rewards.
  - Supports gold.
  - Supports item rewards by item ID and quantity.
- `GameManager`: owns the opened treasure ID set for the current game session.
- `SaveData`: stores `OpenedTreasureBoxIds`.
- `GridMap`: registers treasure boxes as blocking/interactable cells.
- `Game`: routes adjacent treasure opening, grants rewards, updates UI, and records opened state.
- `GameUI`: shows a minimal contextual `Open` prompt when the player is facing a closed treasure box. If an existing prompt surface can be reused, use it; otherwise add a focused treasure prompt label.

Opened boxes remain visible in their open state. They remain blocking after opening so floor navigation does not change after looting.

## Interaction

The player opens a treasure box by pressing the existing `interact` input action while adjacent and facing the box. This reuses the current keybinding infrastructure, but treasure UI must label the action as `Open`.

Runtime flow:

1. Player presses the input action.
2. Stair handling remains first priority.
3. If the facing tile contains a closed treasure box, the treasure open flow starts.
4. `GameManager` enters a generic world-interaction state so player movement is blocked during the short open interaction.
5. The box plays its open animation.
6. Rewards are granted when the lid visibly opens.
7. The box ID is marked opened and the HUD is refreshed.
8. Reopening an already-opened box does not grant rewards again.

If the player is facing an unopened treasure box, any prompt or action text should say `Open`. Already-opened boxes should show no actionable prompt.

## Animation And Art

The first implementation should use a polished treasure box sprite sheet rather than a code-only animation.

Expected art contract:

- Asset path: `res://assets/sprites/objects/treasure_box/sprite_sheet.png`.
- Four horizontal frames: closed, opening early, opening late, open.
- Source size should be `128x32` for four `32x32` frames unless the generated art pipeline produces a higher resolution sheet; the node must scale the selected frame to `GridMap.CellSize`.
- The node should support a horizontal sprite sheet similar to other animated entities.
- The open state must settle on the final open frame.
- Missing art must not break gameplay; the fallback draw path remains interactable and visibly distinct.

## Placement

Add dense authored treasure placement:

- Ground Floor: at least 6 boxes.
- Floor 1: at least 6 boxes.

Placement rules:

- Place boxes only on walkable tiles.
- Do not overlap player starts, stairs, NPC spawns, enemy spawns, or other treasure boxes.
- Do not block critical paths to required stairs or Ground Floor services.
- Prefer side rooms, dead ends, branch ends, shortcut branches, and enemy-gated routes.
- Ground Floor rewards should mostly support early progression.
- Floor 1 rewards can be stronger, especially behind route pressure or deeper branches.

Floor generators should emit treasure boxes in the JSON entity block so generated JSON, imported scenes, and layout tests stay in sync.

## Rewards

Rewards are fixed per treasure box. No random rolls are used in this first version.

Reward mix:

- Common boxes: small or medium gold amounts.
- Utility boxes: consumables such as health potions, mana potions, antidotes, and buff items.
- Deeper or gated boxes: a limited number of equipment upgrades, biased toward early iron or steel-tier gear.

Reward validation:

- Item IDs must exist in `ItemCatalog`.
- Quantities must be positive.
- Duplicate treasure IDs are invalid.
- If inventory cannot accept a full item reward, existing inventory overflow behavior should be reused rather than losing items silently.

## Floor JSON And Import

Extend the floor JSON entity model with `treasure_boxes`.

Example shape:

```json
{
  "id": "TreasureBox_GF_EntranceCache",
  "position": { "x": 15, "y": 50 },
  "gold": 40,
  "items": [
    { "item_id": "health_potion", "quantity": 2 }
  ]
}
```

Importer behavior:

- Create new `TreasureBoxSpawn` nodes when IDs are new.
- Update existing nodes when IDs already exist.
- Remove stale treasure nodes when the JSON contains an explicit `treasure_boxes` list without those IDs.
- Assign ownership to the scene root so imported nodes persist into `.tscn` files.
- Preserve existing scene behavior for enemy, NPC, and stair imports.

Exporter behavior:

- Include treasure boxes in floor JSON so the round-trip model remains complete.

## Save Behavior

`SaveData` gains `OpenedTreasureBoxIds`.

Save rules:

- Opening a box adds its ID to the set immediately.
- Manual saves include the opened set.
- Loading a save restores the opened set before or during floor load.
- Treasure nodes on loaded floors render open when their ID is already opened.
- Already-opened boxes cannot award rewards again even if interacted with after load.

Older saves without `OpenedTreasureBoxIds` should load as having no opened treasure boxes.

## Error Handling

- Missing treasure art falls back to visible generated drawing.
- Unknown reward item IDs are skipped with warnings.
- Invalid reward quantities are ignored with warnings.
- Duplicate treasure IDs should be caught by tests.
- Missing treasure IDs should be treated as invalid authored content.
- Already-opened boxes should be a no-op, not an error.

## Testing

Add focused tests at the same layers used by existing floor content.

Coverage targets:

- `FloorJsonModel` parses and serializes `treasure_boxes`.
- `TilemapJsonImporter` creates, updates, removes, and assigns owners to treasure nodes.
- `TilemapJsonExporter` includes treasure nodes in exported JSON.
- `SaveData` serializes and deserializes opened treasure IDs.
- `GameManager` collects and restores opened treasure IDs.
- `TreasureBoxSpawn` resolves open state and validates rewards.
- `FloorGFMazeLayoutTest` and `Floor1FMazeLayoutTest` assert treasure count, unique IDs, walkable placement, no overlap, and critical path safety.
- A runtime interaction test opens an adjacent/facing treasure box if practical in GdUnit.

Recommended verification loop:

- `python3 -m unittest tests.tools.test_floor0_maze_generator tests.tools.test_floor1_maze_generator -v`
- `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~FloorGFMazeLayoutTest|FullyQualifiedName~Floor1FMazeLayoutTest|FullyQualifiedName~GameManagerTest|FullyQualifiedName~SaveDataTest"`
- `dotnet build Sirius.sln`

## Implementation Planning Details

The implementation plan should choose exact coordinates and fixed rewards using the placement and reward rules above. It should keep the first pass to Ground Floor and Floor 1, with at least 6 boxes on each floor, and should include the sprite asset generation/import step before runtime verification.
