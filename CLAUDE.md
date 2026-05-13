# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sirius is a 2D turn-based tactical RPG built with Godot 4.6.2 and C# scripting (.NET 8.0). The game features a 160x160 grid-based maze world with 8 themed areas, 14+ enemy types, automated turn-based combat, and a sprite animation system with fallback to colored rectangles.

## Development Commands

```bash
# Build the project
dotnet build Sirius.sln

# Run tests (GdUnit4 framework)
# First time: Copy local test settings template
cp test.runsettings.local.template test.runsettings.local
# Edit test.runsettings.local with your Godot path, then:
dotnet test Sirius.sln --settings test.runsettings.local

# Run a single test class
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~CharacterTest"

# Run from Godot editor (F5) or launch editor
godot

# Merge individual 32x32 sprite frames into 128x32 sprite sheets
python3 tools/sprite_sheet_merger.py
```

**Shell issues**: If you encounter `zsh: command not found`, restart shell with `zsh -il`.

**Test setup**: See [TESTING.md](TESTING.md) for detailed test configuration including local Godot path setup.

**`AGENTS.md` is a symlink to `CLAUDE.md`** — edit one, both update.

## Architecture Overview

### Core Systems
- **GameManager (Singleton)**: Global state management, player data persistence across battles, battle state tracking (`IsInBattle` flag)
- **GridMap**: 160x160 grid with viewport culling for performance, themed area system, position-based enemy spawning
- **FloorManager**: Multi-floor system with stair connections between floors (FloorGF.tscn, Floor1F.tscn, etc.). Lives as a node under `Game.tscn` and is the **only** entry point for floor swaps — call `_floorManager.LoadFloor(index, spawnPos?)` rather than `GetTree().ChangeSceneToFile` for in-game floor transitions.
- **SettingsManager (Autoload)**: Persists user settings (`scripts/settings/SettingsManager.cs` + `SettingsData.cs`); registered in `project.godot` `[autoload]`.
- **Battle System**: Modal battle dialogs as popup overlays with automated AI combat decisions
- **Scene Flow**: MainMenu.tscn → Game.tscn → Battle dialogs → back to game

### Key Design Patterns
- **Signal-based communication** for loose coupling between systems
- **Singleton pattern** for GameManager global state
- **Factory pattern** for enemy creation (Enemy.CreateGoblin(), etc.)
- **Viewport culling** for large grid performance optimization
- **Position-based deterministic systems** (enemy types, area detection using seed-based pseudo-random)

### Project Structure
```
scripts/
├── data/
│   ├── items/         # Item catalogs: ItemCatalog.cs, EquipmentCatalog.cs, ConsumableCatalog.cs, MonsterPartsCatalog.cs
│   ├── consumables/   # Status effects: ConsumableItem.cs, StatusEffectSet.cs, ActiveStatusEffect.cs
│   ├── skills/        # Skill system: Skill.cs, SkillCatalog.cs, SkillEffect.cs
│   ├── npc/           # NPC system: NpcCatalog.cs, NpcData.cs, DialogueCatalog.cs, DialogueTree.cs, DialogueCondition.cs, ShopCatalog.cs, ShopInventory.cs
│   └── ...            # Core data: Character.cs, Enemy.cs, EnemyBlueprint.cs, LootManager.cs, LootTable.cs, RecoveryChest.cs
├── game/          # Core game logic (GameManager.cs, GridMap.cs, FloorManager.cs, PlayerController.cs, EnemySpawn.cs, NpcSpawn.cs)
├── save/          # Persistence: SaveManager.cs, SaveData.cs, CharacterSaveData.cs, EquipmentSaveData.cs
├── settings/      # SettingsManager.cs (autoload), SettingsData.cs
├── tilemap_json/  # Tilemap import/export (TilemapJsonImporter.cs, TilemapJsonExporter.cs)
└── ui/            # UI controllers (BattleManager.cs, MainMenu.cs, InventoryMenuController.cs, SaveLoadDialog.cs, ShopDialog.cs, HealDialog.cs, DialogueDialog.cs, NpcInteractionController.cs)

scenes/
├── game/          # Game.tscn, floors/ (FloorGF.tscn, Floor1F.tscn)
├── ui/            # MainMenu.tscn, BattleScene.tscn, InventoryMenu.tscn
└── spawns/        # Enemy/NPC spawn scenes

tests/             # GdUnit4 tests mirroring scripts/ structure
├── TestHelpers.cs # Shared test utilities
├── data/          # CharacterTest.cs, EnemyTest.cs, InventoryTest.cs, skills/, consumables/, npc/
├── game/          # GameManagerTest.cs, FloorManagerTest.cs, EnemySpawnTest.cs, NpcSpawnTest.cs
├── save/          # SaveDataTest.cs, SaveManagerTest.cs
├── settings/      # SettingsDataTest.cs, SettingsManagerTest.cs
├── tilemap_json/  # FloorJsonModelTest.cs, TileConfigManagerTest.cs, TilemapJsonImporterTest.cs
├── tools/         # Python tests for tools/ scripts (run with pytest, NOT dotnet test)
└── ui/            # BattleManagerTest.cs, ShopDialogTest.cs, InventoryMenuControllerTest.cs

assets/sprites/    # Sprite sheets and individual frames
tools/             # Python utilities (see below)
├── sprite_sheet_merger.py        # 32x32 frames → 128x32 sprite sheets
├── floor0_maze_generator.py      # Emit Floor 0 maze JSON
├── floor1_maze_generator.py      # Emit Floor 1 maze JSON
├── generate_static_maze.py       # Static maze emitter
├── tilemap_json_sync.py          # Round-trip .tscn ↔ .json (used by MCP/CLI: export|import|refresh)
└── resize_item_icons.py
```

Python tests under `tests/tools/` are not picked up by `dotnet test` — invoke them with `python3 -m pytest tests/tools` (or per-file).

## Critical Development Patterns

### Battle State Management
Two flags on `GameManager` block player movement — check both:
```csharp
if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction) return;
_battleManager.BattleFinished += OnBattleFinished;
```
- `IsInBattle`: set during modal battle dialog; cleared by `BattleEnded` signal
- `IsInNpcInteraction`: set during NPC dialogue/shop/heal; cleared via `NpcInteractionResetRequested` event
- Emergency reset: `GameManager.ResetBattleState()`

### Grid Coordinate System
- Grid coordinates: (0,0) top-left, (159,159) bottom-right
- World coordinates: multiply by CellSize (32 pixels)
- Player starts at (5, GridHeight/2)
- Always check bounds before accessing grid arrays

### Sprite System
- Asset pipeline: 32x32 individual frames → merge to 128x32 sprite sheets
- 4-frame animation cycles (idle → left step → idle variant → right step)
- Graceful fallback to colored rectangles when sprites missing
- Animation timing: 0.2 seconds per frame (5 FPS)

### Skills System
Skills are stored in `SkillCatalog` (static registry) and referenced on `Character` by string ID — never as Godot Resources. This avoids `.tres`-loaded skills losing their non-exported `Effect` field.
- **Active skills**: fire automatically every `ActivePeriod` player turns
- **Passive skills**: fire when trigger conditions are met (`OnPlayerTurn` chance, `OnLowPlayerHp`, `OnLowEnemyHp`)
- Both cost mana; skipped silently if insufficient mana
- `Character` tracks `KnownSkillIds`, `ActiveSkillId`, `PassiveSkillIds` (up to 3 slots); duplicate passives in different slots are rejected
- `ActiveSkillExplicitlyNone` flag prevents auto-equip on level-up from overriding a deliberate "no active skill" choice

### Save System
`SaveManager` (autoload singleton) handles 3 manual slots (0-2) + autosave (slot 3, `autosave.json`). Saves use atomic write (temp → rename) with `.bak` backup for crash recovery. `SaveData` is serialized as JSON via `System.Text.Json`. `SaveManager.PendingLoadData` is the handoff mechanism between MainMenu and Game scenes — set before scene change, consumed on load.

### NPC & Dialogue System
NPCs follow the same catalog-and-spawn pattern as enemies. `NpcCatalog` is a static registry of `NpcData` objects, referenced by string `NpcId`. `NpcSpawn` is a scene node (mirroring `EnemySpawn`) placed in floor `.tscn` files with `NpcId` and `GridPosition` exports. `GridMap.RegisterStaticNpcSpawns()` picks them up via the `"NpcSpawn"` group.

NPC types: `Villager`, `Shopkeeper`, `Blacksmith`, `QuestGiver`, `Healer`. Each type wires to a subsystem:
- **Shopkeeper/Blacksmith**: `ShopCatalog` → `ShopInventory` → `ShopDialog`
- **Healer**: `HealCost` field on `NpcData` → `HealDialog`
- **All**: `DialogueTreeId` → `DialogueCatalog` → `DialogueTree` (nodes + `DialogueChoice` options)

`DialogueCondition` (`IDialogueCondition`) gates choice visibility on player level, quest flags, etc. Quest flags are stored in `GameManager` and persisted in `SaveData`.

### Enemy & Loot Architecture
Enemies are defined via `EnemyBlueprint` (data) and identified by `EnemyTypeId` enum. `LootTable` / `LootTableCatalog` define per-enemy drop rates. `LootManager` resolves loot on kill. Item definitions live in static catalogs (`ItemCatalog`, `EquipmentCatalog`, `ConsumableCatalog`, `MonsterPartsCatalog`) rather than Godot Resources.

Enemy debuff abilities are declared as `EnemyDebuffAbility` records on `EnemyBlueprint`. `BattleManager` rolls `Chance` (0–1) on each enemy attack; on success, the `StatusEffectType` is applied to the player via `StatusEffectSet.Add()`.

### Status Effects
`StatusEffectSet` tracks active effects on each combatant (one entry per type, max-merge on re-apply). `BattleManager.Tick()` advances durations after each combatant action and applies DoT/HoT directly to HP.

Available `StatusEffectType` values:
- **Debuffs**: `Poison`/`Burn` (DoT, bypasses defense), `Stun` (skip action), `Weaken` (−Magnitude% ATK), `Slow` (−Magnitude% SPD), `Blind` (55% accuracy)
- **Buffs**: `Regen` (HoT), `Haste` (+flat SPD), `Strength` (+flat ATK), `Fortify` (+flat DEF)

`ActiveStatusEffect` is an immutable record: `(Type, Magnitude, TurnsRemaining)`. Value 11 is reserved — do not use.

### Equipment & Inventory Limits
- `EquipmentSet`: 5 main slots (Weapon, Shield, Armor, Helmet, Shoe) + 4 Accessory slots
- `Inventory`: max 100 distinct item types (`MaxItemTypes`); individual stacks are unbounded

### Performance Requirements
- **Viewport culling** essential for 160x160 grid (must maintain 60fps)
- **Sprite caching** in dictionaries, load once in _Ready()
- **Position-based** enemy determination (avoid GD.Randf() in drawing loops)
- **Scene reuse**: instantiate battle dialogs, don't reload scenes

## Godot/C# Requirements

- C# partial classes required for Godot nodes: `public partial class`
- Resource classes need `[System.Serializable]` for data persistence
- Signal connections should be made in _Ready() methods
- Use deferred calls when modifying scene tree during signal callbacks

## Testing

### Setup
1. **One-time setup**: Copy and configure local test settings:
   ```bash
   cp test.runsettings.local.template test.runsettings.local
   # Edit test.runsettings.local with your Godot path
   ```

2. **Run tests**:
   ```bash
   dotnet test Sirius.sln --settings test.runsettings.local
   ```

See [TESTING.md](TESTING.md) for platform-specific Godot paths and detailed configuration.

### Writing Tests
Tests use GdUnit4 framework with `[TestSuite]` and `[TestCase]` attributes:
```csharp
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class YourTest : Node
{
    [TestCase]
    public void TestYourFeature()
    {
        var obj = new YourClass();
        obj.DoSomething();
        AssertThat(obj.Result).IsEqual(expectedValue);
    }
}
```

Test files must mirror source structure: `scripts/data/Character.cs` → `tests/data/CharacterTest.cs`

## Debugging

- Walk into colored enemies on grid to trigger battles
- Auto-combat proceeds without input, ESC returns to menu
- Console provides detailed battle logs and state transitions
- **Grid bounds errors**: Always validate coordinates before accessing grid arrays

## Architecture Docs

Deep-dive docs live in `docs/`:
- `docs/PRD.md` — feature roadmap and implementation status
- `docs/architecture/MULTI_FLOOR_ARCHITECTURE.md` — multi-floor system design
- `docs/items/items-guide.md` — item catalog conventions

`README.md` mirrors the enemy stat table (HP/ATK/DEF/SPD/XP per type) and area→enemy mapping from the runtime data catalogs (`EnemyBlueprint` factory methods, enemy catalogs). The **authoritative source** is always the code — consult `EnemyBlueprint.cs` and related catalogs when tweaking balance. Update README to match after code changes so the human-readable reference stays in sync.