# Sirius - Product Requirements Document

**Version:** 1.0
**Last Updated:** January 2026
**Product Owner:** TBD
**Tech Lead:** TBD

---

## Executive Summary

Sirius is a 2D turn-based tactical RPG built with Godot 4.4.1 and C# (.NET 8.0). The game features a 160x160 grid-based maze world with 8 themed areas, 14+ enemy types, automated turn-based combat, and a sprite animation system.

This PRD outlines 16 features across 4 priority tiers that will transform Sirius from a functional prototype into a complete, engaging RPG experience. The features are designed to create a compelling gameplay loop with exploration, combat progression, itemization, and replayability.

### Strategic Vision

Transform Sirius into a polished tactical RPG that offers:
- **Deep Combat**: Meaningful decisions through skills, status effects, and boss mechanics
- **Progression Systems**: Satisfying character growth through loot, equipment, and abilities
- **Exploration**: Procedurally generated dungeons with varied environments
- **Replayability**: Roguelike elements and daily challenges for long-term engagement

### Success Metrics

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| Average Session Duration | > 30 minutes | Analytics |
| Day 7 Retention | > 25% | Analytics |
| Combat Encounters per Session | > 10 | Event tracking |
| Feature Completion Rate | > 80% | Milestone tracking |
| Critical Bug Count | 0 | QA testing |

---

## Table of Contents

1. [Current Architecture Context](#current-architecture-context)
2. [Feature Dependency Graph](#feature-dependency-graph)
3. [Phase 1: Core Gameplay Loop](#phase-1-core-gameplay-loop-high-priority)
4. [Phase 2: Combat Depth](#phase-2-combat-depth-medium-priority)
5. [Phase 3: World and Content](#phase-3-world-and-content-medium-priority)
6. [Phase 4: Polish](#phase-4-polish-lower-priority)
7. [Bonus Features](#bonus-features)
8. [Implementation Timeline](#implementation-timeline)
9. [Risks and Mitigations](#risks-and-mitigations)
10. [Appendix](#appendix)

---

## Current Architecture Context

### Tech Stack
- **Engine**: Godot 4.4.1
- **Language**: C# (.NET 8.0)
- **Testing**: GdUnit4 framework
- **Grid System**: 160x160 cells, 32px per cell

### Core Systems (Existing)

| System | File | Responsibility |
|--------|------|----------------|
| GameManager | `scripts/game/GameManager.cs` | Singleton for global state, player data, battle state tracking |
| Character | `scripts/data/Character.cs` | Player stats, inventory, equipment, leveling |
| Enemy | `scripts/data/Enemy.cs` | Enemy data with 14+ factory methods |
| BattleManager | `scripts/ui/BattleManager.cs` | Combat UI, auto-battle, damage calculations |
| GridMap | `scripts/game/GridMap.cs` | 160x160 grid rendering, viewport culling, theming |
| FloorManager | `scripts/game/FloorManager.cs` | Multi-floor transitions, stair connections |
| Inventory | `scripts/data/Inventory.cs` | Item storage with stacking, 100 item type limit |
| EquipmentSet | `scripts/data/EquipmentSet.cs` | 5 main slots + 4 accessory slots |
| Item | `scripts/data/Item.cs` | Base class with General, Equipment, Consumable, Quest categories |

### Key Design Patterns
- **Signal-based communication** for loose coupling
- **Singleton pattern** for GameManager
- **Factory pattern** for enemy creation
- **Viewport culling** for performance

### Current Item System

```csharp
public enum ItemCategory
{
    General = 0,
    Equipment = 1,
    Consumable = 2,  // Defined but not implemented
    Quest = 3        // Defined but not implemented
}
```

---

## Feature Dependency Graph

```
                    +-----------------+
                    |   Save/Load     |
                    |    System       |
                    +--------+--------+
                             |
              +--------------+--------------+
              |              |              |
     +--------v------+  +----v----+  +------v------+
     | Loot Drop     |  | Quest   |  | Settings    |
     | System        |  | System  |  | Menu        |
     +--------+------+  +----+----+  +-------------+
              |              |
     +--------v------+       |
     | Consumable    |       |
     | Item Effects  |       |
     +--------+------+       |
              |              |
     +--------v--------------v--------+
     |       Skills/Abilities         |
     |           System               |
     +--------+-----------------------+
              |
     +--------v------+   +------------+
     | Status Effects|   | NPC &      |
     | System        |   | Dialogue   |
     +--------+------+   +-----+------+
              |                |
     +--------v------+   +-----v------+
     | Boss Fight    |   | Crafting   |
     | Mechanics     |   | System     |
     +---------------+   +------------+
              |
     +--------v---------------------------+
     | Procedural Maze Generation         |
     +--------+---------------------------+
              |
     +--------v------+   +----------------+
     | Party System  |   | Roguelike Mode |
     +---------------+   +----------------+
```

---

## Phase 1: Core Gameplay Loop (HIGH PRIORITY)

### 1.1 Save/Load System

#### Executive Summary
- **Feature Name**: Save/Load System
- **One-Liner**: JSON-based persistence for player progress with multiple save slots
- **Strategic Rationale**: Essential for player retention; no one plays an RPG they cannot save
- **Success Metrics**: 100% data integrity after load, < 500ms save/load time

#### Purpose and Problem Statement

**Business Context**: Players expect to save progress in any RPG. Without saves, session length is artificially limited and player frustration increases.

**User Pain Points**:
- Cannot continue progress after closing the game
- Risk of losing hours of progress to crashes or interruptions
- No way to experiment with different builds or strategies

**Current State**: No persistence; all progress lost on game close
**Desired State**: Multiple save slots with automatic and manual save options

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| SL-1 | As a player, I want to save my game manually so I can stop playing anytime | Must Have | Save button in pause menu creates save file |
| SL-2 | As a player, I want the game to auto-save after battles so I don't lose progress | Must Have | Auto-save triggers after every victory |
| SL-3 | As a player, I want multiple save slots so I can try different strategies | Should Have | 3 save slots available |
| SL-4 | As a player, I want to see save slot metadata so I can choose which to load | Should Have | Display level, playtime, last save date |
| SL-5 | As a player, I want to delete save files so I can start fresh | Could Have | Delete confirmation dialog |

#### Technical Specifications

**Data Model - SaveData.cs**

```csharp
[System.Serializable]
public class SaveData
{
    public int Version { get; set; } = 1;
    public DateTime SavedAt { get; set; }
    public TimeSpan PlayTime { get; set; }

    // Character data
    public CharacterSaveData Player { get; set; }

    // World state
    public int CurrentFloorIndex { get; set; }
    public Vector2I PlayerPosition { get; set; }
    public List<string> DefeatedEnemyIds { get; set; }

    // Quest progress (for future)
    public Dictionary<string, QuestState> Quests { get; set; }
}

[System.Serializable]
public class CharacterSaveData
{
    public string Name { get; set; }
    public int Level { get; set; }
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public int Experience { get; set; }
    public int Gold { get; set; }

    public List<InventorySaveEntry> Inventory { get; set; }
    public EquipmentSaveData Equipment { get; set; }
}
```

**File Structure**
```
user://saves/
    slot_1.json
    slot_2.json
    slot_3.json
    autosave.json
    meta.json (slot metadata for quick display)
```

**SaveManager.cs Integration**

```csharp
public partial class SaveManager : Node
{
    [Signal] public delegate void SaveCompletedEventHandler(bool success);
    [Signal] public delegate void LoadCompletedEventHandler(bool success);

    private const string SAVE_DIR = "user://saves/";
    private const int MAX_SLOTS = 3;

    public Error SaveGame(int slot);
    public Error LoadGame(int slot);
    public Error AutoSave();
    public SaveMetadata[] GetSaveSlots();
    public Error DeleteSave(int slot);
}
```

**Integration Points**:
- GameManager: Add SaveManager reference, expose Player data for serialization
- BattleManager: Emit signal on victory for auto-save trigger
- MainMenu: Add Load Game button and slot selection UI
- Game.cs: Add Pause menu with Save button

**Migration Strategy**: N/A (new feature)

#### Non-Functional Requirements

| Requirement | Target | Notes |
|-------------|--------|-------|
| Save time | < 500ms | Async write to prevent frame drops |
| Load time | < 1000ms | Show loading indicator |
| File size | < 100KB | Compressed JSON |
| Data integrity | 100% | Version migration support |
| Encryption | Optional | Prevent casual save editing |

#### Implementation Phases

**Phase 1a (MVP)**: Manual save/load single slot (3 days)
- SaveData class and serialization
- SaveManager with Save/Load methods
- Basic UI buttons

**Phase 1b**: Multiple slots + metadata (2 days)
- Slot selection UI
- Metadata extraction and display
- Delete functionality

**Phase 1c**: Auto-save integration (1 day)
- Battle victory trigger
- Floor transition trigger
- Settings toggle for auto-save

---

### 1.2 Loot Drop System

#### Executive Summary
- **Feature Name**: Loot Drop System
- **One-Liner**: Enemy drop tables with weighted random loot and rare boss drops
- **Strategic Rationale**: Core progression loop - defeating enemies should feel rewarding
- **Success Metrics**: > 70% of players pick up loot, < 5% complaints about drop rates

#### Purpose and Problem Statement

**Business Context**: Loot is the primary reward mechanism in action RPGs. Without it, combat lacks tangible rewards beyond XP/gold.

**User Pain Points**:
- Enemies only give XP and gold (already implemented)
- No way to acquire new equipment through gameplay
- Combat feels repetitive without item anticipation

**Current State**: `Enemy.cs` has `ExperienceReward` and `GoldReward`, no item drops
**Desired State**: Every enemy has a chance to drop items based on configurable drop tables

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| LD-1 | As a player, I want enemies to drop items so combat feels rewarding | Must Have | Items appear after battle victory |
| LD-2 | As a player, I want rare enemies to drop better loot | Must Have | Boss drops have higher quality |
| LD-3 | As a player, I want to see what I received | Must Have | Loot popup shows all drops |
| LD-4 | As a player, I want drops to match enemy types | Should Have | Dragons drop fire items, etc. |
| LD-5 | As a player, I want legendary drops to feel special | Could Have | Visual/audio fanfare for rare drops |

#### Technical Specifications

**Data Model - LootTable.cs**

```csharp
[System.Serializable]
public class LootTable : Resource
{
    [Export] public string TableId { get; set; }
    [Export] public Godot.Collections.Array<LootEntry> Entries { get; set; }
    [Export] public int GuaranteedDrops { get; set; } = 0; // Min items to drop
    [Export] public int MaxDrops { get; set; } = 3; // Max items per kill
}

[System.Serializable]
public class LootEntry : Resource
{
    [Export] public Item ItemTemplate { get; set; }
    [Export] public float Weight { get; set; } = 1.0f; // Relative probability
    [Export] public int MinQuantity { get; set; } = 1;
    [Export] public int MaxQuantity { get; set; } = 1;
    [Export] public ItemRarity MinRarity { get; set; } = ItemRarity.Common;
}

public enum ItemRarity
{
    Common = 0,    // 60% base chance
    Uncommon = 1,  // 25% base chance
    Rare = 2,      // 12% base chance
    Epic = 3,      // 2.5% base chance
    Legendary = 4  // 0.5% base chance
}
```

**Enemy.cs Enhancement**

```csharp
public partial class Enemy : Resource
{
    // ... existing properties ...

    [Export] public LootTable LootTable { get; set; }
    [Export] public float LootBonusMultiplier { get; set; } = 1.0f;

    public List<Item> RollLoot()
    {
        if (LootTable == null) return new List<Item>();
        return LootManager.Instance.Roll(LootTable, LootBonusMultiplier);
    }
}
```

**LootManager.cs**

```csharp
public partial class LootManager : Node
{
    public static LootManager Instance { get; private set; }

    [Signal] public delegate void LootDroppedEventHandler(List<Item> items);

    public List<Item> Roll(LootTable table, float bonusMultiplier = 1.0f);
    public Item RollSingleEntry(LootEntry entry);
    private float CalculateEffectiveWeight(LootEntry entry, float bonus);
}
```

**BattleManager Integration**

```csharp
// In EndBattle(bool playerWon)
if (playerWon)
{
    // ... existing XP/Gold logic ...

    var loot = _enemy.RollLoot();
    if (loot.Count > 0)
    {
        foreach (var item in loot)
        {
            _player.TryAddItem(item, 1, out _);
        }
        ShowLootPopup(loot);
    }
}
```

**Drop Rate Configuration (examples)**

| Enemy Type | Common | Uncommon | Rare | Epic | Legendary |
|------------|--------|----------|------|------|-----------|
| Goblin | 60% | 25% | 12% | 2.5% | 0.5% |
| Orc | 50% | 30% | 15% | 4% | 1% |
| Dragon | 30% | 35% | 25% | 8% | 2% |
| Boss | 10% | 20% | 40% | 25% | 5% |

#### Implementation Phases

**Phase 2a (MVP)**: Basic drops (2 days)
- LootTable and LootEntry classes
- LootManager singleton
- Integration with BattleManager

**Phase 2b**: Loot UI (2 days)
- Victory screen loot display
- Rarity color coding
- Item comparison tooltips

**Phase 2c**: Content creation (3 days)
- Create loot tables for all 14 enemy types
- Balance drop rates
- Create 20+ new item definitions

---

### 1.3 Consumable Item Effects

#### Executive Summary
- **Feature Name**: Consumable Item Effects
- **One-Liner**: Health/mana potions, buff items, and battle item usage
- **Strategic Rationale**: Gives players tactical options and a reason to collect items
- **Success Metrics**: Average 3+ consumables used per boss fight

#### Purpose and Problem Statement

**Business Context**: Consumables add strategic depth and create a gold sink. They also provide recovery options that reduce frustration.

**User Pain Points**:
- No way to heal outside of leveling up
- Combat lacks tactical options beyond attack/defend
- Collected items have no use

**Current State**: `ItemCategory.Consumable` exists but has no implementation
**Desired State**: Full consumable system with battle and field usage

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| CI-1 | As a player, I want to use health potions to heal during battle | Must Have | "Use Item" button in battle consumes potion |
| CI-2 | As a player, I want to use items outside battle from inventory | Must Have | Right-click/long-press shows "Use" option |
| CI-3 | As a player, I want buff items that temporarily boost stats | Should Have | Attack/Defense potions last N turns |
| CI-4 | As a player, I want to see item effects before using | Should Have | Tooltip shows heal amount, duration |
| CI-5 | As a player, I want visual feedback when items are used | Could Have | Particle effects, sound |

#### Technical Specifications

**Data Model - ConsumableItem.cs**

```csharp
[System.Serializable]
public partial class ConsumableItem : Item
{
    [Export] public ConsumableEffect Effect { get; set; }
    [Export] public int EffectValue { get; set; } // Heal amount, buff strength
    [Export] public int Duration { get; set; } = 0; // 0 = instant, >0 = turns/seconds
    [Export] public bool UsableInBattle { get; set; } = true;
    [Export] public bool UsableOutOfBattle { get; set; } = true;
    [Export] public string UseAnimation { get; set; } = "consume_default";

    public ConsumableItem()
    {
        SetCategory(ItemCategory.Consumable);
    }

    public bool TryUse(Character target, bool inBattle)
    {
        if (inBattle && !UsableInBattle) return false;
        if (!inBattle && !UsableOutOfBattle) return false;

        ApplyEffect(target);
        return true;
    }

    private void ApplyEffect(Character target);
}

public enum ConsumableEffect
{
    HealHealth = 0,
    HealMana = 1,
    BuffAttack = 2,
    BuffDefense = 3,
    BuffSpeed = 4,
    CurePoison = 5,
    CureAll = 6,
    Revive = 7
}
```

**Starter Consumables**

| Item | Effect | Value | Duration | Gold Cost |
|------|--------|-------|----------|-----------|
| Minor Health Potion | HealHealth | 50 HP | Instant | 25g |
| Health Potion | HealHealth | 100 HP | Instant | 50g |
| Major Health Potion | HealHealth | 200 HP | Instant | 100g |
| Attack Tonic | BuffAttack | +20% | 5 turns | 75g |
| Defense Tonic | BuffDefense | +20% | 5 turns | 75g |
| Antidote | CurePoison | N/A | Instant | 30g |

**BattleManager Integration**

```csharp
// Add "Items" button to battle UI
private Button _itemsButton;
private List<ConsumableItem> _battleConsumables;

private void OnItemsButtonPressed()
{
    // Show consumable selection popup
    var consumables = GetBattleUsableConsumables();
    ShowItemSelectionPopup(consumables, OnBattleItemSelected);
}

private void OnBattleItemSelected(ConsumableItem item)
{
    if (item.TryUse(_player, inBattle: true))
    {
        _player.TryRemoveItem(item.Id, 1);
        // Using item takes player's turn
        _playerTurn = false;
        UpdateUI();
    }
}
```

**InventoryMenuController Integration**

```csharp
// Modify OnInventorySlotPressed
private void OnInventorySlotPressed(int slotIndex)
{
    var entry = _inventorySlotEntries[slotIndex];

    if (entry?.Item is EquipmentItem equipment)
    {
        EquipFromInventory(equipment);
    }
    else if (entry?.Item is ConsumableItem consumable)
    {
        ShowContextMenu(consumable, slotIndex);
    }
}

private void ShowContextMenu(ConsumableItem item, int slotIndex)
{
    // Options: Use, Drop, Cancel
    // "Use" only enabled if UsableOutOfBattle
}
```

#### Implementation Phases

**Phase 3a (MVP)**: Health potions (2 days)
- ConsumableItem class
- Basic heal effect
- Battle "Items" button

**Phase 3b**: Buff items (2 days)
- Buff effect implementation
- Turn/time duration tracking
- Visual buff indicators

**Phase 3c**: Field usage (1 day)
- Inventory context menu
- Out-of-battle consumption
- UI feedback

---

## Phase 2: Combat Depth (MEDIUM PRIORITY)

### 2.1 Skills/Abilities System

#### Executive Summary
- **Feature Name**: Skills/Abilities System
- **One-Liner**: Unlockable skills with mana costs and strategic skill selection
- **Strategic Rationale**: Transforms combat from auto-battle to tactical decision-making
- **Success Metrics**: Players use skills in > 50% of combat turns

#### Purpose and Problem Statement

**Business Context**: Skills differentiate RPGs from simple combat games. They create build diversity and mastery progression.

**User Pain Points**:
- Combat is automated with no player agency
- No sense of character customization
- All encounters feel similar regardless of enemy

**Current State**: Auto-battle only with attack/defend
**Desired State**: Player-selectable skills with resource management

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| SK-1 | As a player, I want to learn skills as I level up | Must Have | Skill unlock notification on level |
| SK-2 | As a player, I want to choose which skill to use in battle | Must Have | Skill menu in combat UI |
| SK-3 | As a player, I want skills to cost mana | Must Have | Mana bar visible, depletes on use |
| SK-4 | As a player, I want different skill types (attack, heal, buff) | Should Have | At least 3 skill categories |
| SK-5 | As a player, I want to see skill descriptions and costs | Should Have | Tooltip with details |
| SK-6 | As a player, I want to equip/unequip skills for battle | Could Have | Skill loadout system |

#### Technical Specifications

**Data Model - Skill.cs**

```csharp
[System.Serializable]
public partial class Skill : Resource
{
    [Export] public string SkillId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public SkillType Type { get; set; }
    [Export] public int ManaCost { get; set; }
    [Export] public int CooldownTurns { get; set; } = 0;
    [Export] public int BasePower { get; set; }
    [Export] public float ScalingFactor { get; set; } = 1.0f;
    [Export] public int UnlockLevel { get; set; } = 1;
    [Export] public SkillTarget Target { get; set; }
    [Export] public StatusEffect AppliedEffect { get; set; }
    [Export] public string AnimationName { get; set; }
}

public enum SkillType
{
    Physical = 0,
    Magical = 1,
    Healing = 2,
    Buff = 3,
    Debuff = 4
}

public enum SkillTarget
{
    Self = 0,
    SingleEnemy = 1,
    AllEnemies = 2,
    SingleAlly = 3,
    AllAllies = 4
}
```

**Character.cs Enhancement**

```csharp
public partial class Character : Resource
{
    // ... existing properties ...

    [Export] public int MaxMana { get; set; } = 50;
    [Export] public int CurrentMana { get; set; } = 50;
    [Export] public List<Skill> LearnedSkills { get; set; } = new();
    [Export] public List<Skill> EquippedSkills { get; set; } = new(); // Max 4-6

    public void RestoreMana(int amount);
    public bool TryUseSkill(Skill skill, Character target);
    public void LearnSkill(Skill skill);
}
```

**Starter Skills**

| Skill | Type | Mana | Power | Unlock Lv | Description |
|-------|------|------|-------|-----------|-------------|
| Power Strike | Physical | 10 | 150% ATK | 1 | Strong physical attack |
| Heal | Healing | 15 | 50 HP | 2 | Restore health |
| Fire Bolt | Magical | 20 | 120% ATK | 3 | Magical fire damage |
| Shield Bash | Physical | 15 | 100% ATK + Stun | 5 | Attack with stun chance |
| Battle Cry | Buff | 25 | +30% ATK | 7 | Buff attack for 3 turns |
| Cleave | Physical | 30 | 80% ATK AoE | 10 | Hit all enemies |

#### Implementation Phases

**Phase A (MVP)**: Core skill system (4 days)
- Skill class and data
- Mana system in Character
- Battle skill selection UI
- 5 starter skills

**Phase B**: Skill progression (3 days)
- Level-up skill unlocks
- Skill equip/loadout screen
- Cooldown system

**Phase C**: Skill effects (3 days)
- Skill animations
- Status effect integration
- Balance tuning

---

### 2.2 Boss Fight Mechanics

#### Executive Summary
- **Feature Name**: Boss Fight Mechanics
- **One-Liner**: Multi-phase boss battles with special attacks and minion summoning
- **Strategic Rationale**: Boss fights are memorable moments that test player skill and mark progression milestones
- **Success Metrics**: < 20% first-attempt boss clear rate, > 80% eventual clear rate

#### Purpose and Problem Statement

**Business Context**: Boss fights create peak experiences and serve as skill/gear checks. They are often the most shared and discussed content.

**User Pain Points**:
- "Ancient Dragon King" boss is just a stat-boosted enemy
- No unique mechanics to learn or overcome
- Boss victory feels unearned

**Current State**: `Enemy.CreateBoss()` creates a high-stat enemy with no special behavior
**Desired State**: Multi-phase boss with unique mechanics, attacks, and drama

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| BF-1 | As a player, I want bosses to have multiple phases | Must Have | HP thresholds trigger phase changes |
| BF-2 | As a player, I want bosses to use special attacks | Must Have | Unique attack patterns per boss |
| BF-3 | As a player, I want bosses to summon minions | Should Have | Adds spawn during fight |
| BF-4 | As a player, I want warning indicators for big attacks | Should Have | Charge-up visual before devastating attack |
| BF-5 | As a player, I want unique boss arenas | Could Have | Different battle backgrounds |
| BF-6 | As a player, I want boss defeat cutscenes | Could Have | Victory animation/dialogue |

#### Technical Specifications

**Data Model - BossEnemy.cs**

```csharp
public partial class BossEnemy : Enemy
{
    [Export] public int PhaseCount { get; set; } = 3;
    [Export] public Godot.Collections.Array<BossPhase> Phases { get; set; }
    [Export] public string BossTitle { get; set; } // "Ancient Dragon King"
    [Export] public string DefeatDialogue { get; set; }

    public int CurrentPhase { get; private set; } = 1;

    public BossPhase GetCurrentPhase()
    {
        return Phases[CurrentPhase - 1];
    }

    public bool CheckPhaseTransition()
    {
        float hpPercent = (float)CurrentHealth / MaxHealth;
        // Phase 2 at 66%, Phase 3 at 33%
        int newPhase = hpPercent > 0.66f ? 1 : (hpPercent > 0.33f ? 2 : 3);

        if (newPhase != CurrentPhase)
        {
            CurrentPhase = newPhase;
            return true;
        }
        return false;
    }
}

[System.Serializable]
public class BossPhase : Resource
{
    [Export] public string PhaseName { get; set; }
    [Export] public Godot.Collections.Array<BossAttack> Attacks { get; set; }
    [Export] public EnemyBlueprint SummonTemplate { get; set; }
    [Export] public int SummonCount { get; set; } = 0;
    [Export] public float AttackSpeedMultiplier { get; set; } = 1.0f;
    [Export] public string PhaseTransitionDialogue { get; set; }
}

[System.Serializable]
public class BossAttack : Resource
{
    [Export] public string AttackName { get; set; }
    [Export] public int Damage { get; set; }
    [Export] public float UseChance { get; set; } // 0-1
    [Export] public int ChargeUpTurns { get; set; } = 0; // 0 = instant
    [Export] public StatusEffect AppliedStatus { get; set; }
    [Export] public bool IsAoE { get; set; } = false;
}
```

**Ancient Dragon King Design**

| Phase | HP Range | Mechanics |
|-------|----------|-----------|
| Phase 1 | 100%-66% | Basic attacks, occasional Fire Breath (AoE) |
| Phase 2 | 66%-33% | Summons 2 Dragon Whelps, gains Wing Buffet attack |
| Phase 3 | 33%-0% | Enraged (+50% damage), uses Dragon's Fury (3-turn charge, massive AoE) |

#### Implementation Phases

**Phase A**: Boss framework (3 days)
- BossEnemy class
- Phase transition system
- BossBattleManager variant

**Phase B**: Special attacks (3 days)
- Attack pattern system
- Charge-up mechanics
- Warning indicators

**Phase C**: Minion summoning (2 days)
- Mid-battle enemy spawning
- Target selection with adds
- Add priority AI

---

### 2.3 Status Effects System

#### Executive Summary
- **Feature Name**: Status Effects System
- **One-Liner**: Poison, Stun, Weaken, and buffs like Shield/Haste/Regen
- **Strategic Rationale**: Adds tactical depth and makes combat more dynamic
- **Success Metrics**: > 30% of battles involve status effects

#### Purpose and Problem Statement

**Business Context**: Status effects create build diversity and counter-play. They make combat more interesting and strategic.

**User Pain Points**:
- Combat is purely damage-based
- No way to control or debilitate enemies
- Enemy attacks are predictable

**Current State**: No status effect system
**Desired State**: Comprehensive buff/debuff system affecting combat flow

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| SE-1 | As a player, I want to see active status effects on combatants | Must Have | Icons displayed near health bars |
| SE-2 | As a player, I want debuffs to expire after duration | Must Have | Turn counter visible |
| SE-3 | As a player, I want to cure negative effects | Should Have | Antidote items, cure skills |
| SE-4 | As a player, I want buffs to stack strategically | Should Have | Multiple buffs have synergy |
| SE-5 | As a player, I want enemies to have immunities | Could Have | Bosses immune to stun, etc. |

#### Technical Specifications

**Data Model - StatusEffect.cs**

```csharp
[System.Serializable]
public partial class StatusEffect : Resource
{
    [Export] public string EffectId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public StatusEffectType Type { get; set; }
    [Export] public int Duration { get; set; } // Turns
    [Export] public int ValuePerTurn { get; set; } // Damage/heal per turn
    [Export] public float StatModifier { get; set; } // Multiplier for stat effects
    [Export] public Texture2D Icon { get; set; }
    [Export] public bool IsDebuff { get; set; }
    [Export] public bool Stackable { get; set; } = false;
}

public enum StatusEffectType
{
    // Debuffs
    Poison = 0,      // Damage over time
    Burn = 1,        // Damage over time (fire)
    Stun = 2,        // Skip turn
    Weaken = 3,      // Reduce attack
    Slow = 4,        // Reduce speed
    Blind = 5,       // Reduce accuracy

    // Buffs
    Regen = 10,      // Heal over time
    Shield = 11,     // Damage absorption
    Haste = 12,      // Increase speed
    Strength = 13,   // Increase attack
    Fortify = 14     // Increase defense
}
```

**StatusEffectManager.cs**

```csharp
public class StatusEffectManager
{
    private List<ActiveStatusEffect> _activeEffects = new();

    public void ApplyEffect(StatusEffect effect, Character target);
    public void RemoveEffect(string effectId, Character target);
    public void ProcessTurnStart(Character character);
    public void ProcessTurnEnd(Character character);
    public bool HasEffect(Character character, StatusEffectType type);
    public int GetStackCount(Character character, StatusEffectType type);
}

public class ActiveStatusEffect
{
    public StatusEffect Effect { get; set; }
    public Character Target { get; set; }
    public int RemainingDuration { get; set; }
    public int StackCount { get; set; } = 1;
}
```

**Status Effect Definitions**

| Effect | Type | Duration | Value | Description |
|--------|------|----------|-------|-------------|
| Poison | Debuff | 3 turns | 5% max HP/turn | Take damage each turn |
| Stun | Debuff | 1 turn | N/A | Cannot act |
| Weaken | Debuff | 3 turns | -25% ATK | Reduced attack |
| Slow | Debuff | 3 turns | -50% SPD | Reduced speed |
| Regen | Buff | 5 turns | 3% max HP/turn | Heal each turn |
| Shield | Buff | 3 turns | 50 HP absorb | Absorb damage |
| Haste | Buff | 3 turns | +50% SPD | Increased speed |

#### Implementation Phases

**Phase A**: Core system (3 days)
- StatusEffect class
- StatusEffectManager
- Turn processing

**Phase B**: Combat integration (2 days)
- BattleManager integration
- UI effect icons
- Duration display

**Phase C**: Skills/Items integration (2 days)
- Skill status application
- Consumable cures
- Enemy status abilities

---

## Phase 3: World and Content (MEDIUM PRIORITY)

### 3.1 Procedural Maze Generation

#### Executive Summary
- **Feature Name**: Procedural Maze Generation
- **One-Liner**: Infinite dungeon depth with room templates and themed areas
- **Strategic Rationale**: Infinite content creation, replayability, and exploration excitement
- **Success Metrics**: > 5 procedural floors explored per session

#### Purpose and Problem Statement

**Business Context**: Hand-crafted content is expensive. Procedural generation provides infinite variety with bounded development effort.

**User Pain Points**:
- Limited to hand-designed floors (FloorGF, Floor1F)
- No sense of exploration into the unknown
- Content exhaustion after seeing all floors

**Current State**: Static floor scenes with manually placed tiles and enemies
**Desired State**: Procedural dungeon generator with themed biomes

#### User Stories

| ID | Story | Priority | Acceptance Criteria |
|----|-------|----------|---------------------|
| PM-1 | As a player, I want each floor to be different | Must Have | Unique layout on each visit |
| PM-2 | As a player, I want deeper floors to be harder | Must Have | Enemy scaling with depth |
| PM-3 | As a player, I want themed areas (forest, cave, etc.) | Should Have | Visual/enemy theming |
| PM-4 | As a player, I want special rooms (treasure, boss) | Should Have | Room type variety |
| PM-5 | As a player, I want a minimap to track exploration | Could Have | Fog of war minimap |

#### Technical Specifications

**Data Model - ProceduralFloorConfig.cs**

```csharp
[System.Serializable]
public class ProceduralFloorConfig : Resource
{
    [Export] public int MinRooms { get; set; } = 5;
    [Export] public int MaxRooms { get; set; } = 10;
    [Export] public Vector2I RoomMinSize { get; set; } = new(5, 5);
    [Export] public Vector2I RoomMaxSize { get; set; } = new(15, 15);
    [Export] public float CorridorWidth { get; set; } = 3;
    [Export] public string Biome { get; set; } = "dungeon";
    [Export] public int BaseEnemyLevel { get; set; } = 1;
    [Export] public float EnemyDensity { get; set; } = 0.05f; // Enemies per tile
    [Export] public float TreasureRoomChance { get; set; } = 0.2f;
    [Export] public float BossRoomChance { get; set; } = 0.1f;
}
```

**ProceduralFloorGenerator.cs**

```csharp
public partial class ProceduralFloorGenerator : Node
{
    public GeneratedFloor Generate(ProceduralFloorConfig config, int seed);

    private List<Room> GenerateRooms(ProceduralFloorConfig config);
    private void ConnectRooms(List<Room> rooms);
    private void PlaceEnemies(GeneratedFloor floor, ProceduralFloorConfig config);
    private void PlaceStairs(GeneratedFloor floor);
    private void ApplyBiomeTheming(GeneratedFloor floor, string biome);
}

public class GeneratedFloor
{
    public int[,] Tiles { get; set; }
    public List<EnemySpawnData> Enemies { get; set; }
    public Vector2I StairsUp { get; set; }
    public Vector2I StairsDown { get; set; }
    public Vector2I PlayerSpawn { get; set; }
}

public class Room
{
    public Rect2I Bounds { get; set; }
    public RoomType Type { get; set; }
    public bool IsConnected { get; set; }
}

public enum RoomType
{
    Normal,
    Treasure,
    Boss,
    Spawn,
    Secret
}
```

**Generation Algorithm**: Binary Space Partitioning (BSP) + Corridor Connection
1. Recursively divide floor area into cells
2. Place rooms within cells (randomized size/position)
3. Connect rooms via L-shaped or straight corridors
4. Place stairs (up in first room, down in last)
5. Populate with enemies based on density
6. Add special rooms (treasure, boss) based on chance

#### Implementation Phases

**Phase A**: Basic generation (5 days)
- BSP room generator
- Corridor connection
- Tile output to GridMap

**Phase B**: Theming and enemies (3 days)
- Biome tile sets
- Enemy placement
- Difficulty scaling

**Phase C**: Special rooms (3 days)
- Treasure rooms
- Boss rooms
- Minimap integration

---

### 3.2 NPC and Dialogue System

#### Executive Summary
- **Feature Name**: NPC and Dialogue System
- **One-Liner**: Town NPCs, shops, and dialogue choices
- **Strategic Rationale**: NPCs provide story context, services, and break up combat monotony
- **Success Metrics**: > 60% of players interact with NPCs

#### Purpose and Problem Statement

**Business Context**: NPCs are essential for worldbuilding, shops, and quest distribution. They make the world feel alive.

**User Pain Points**:
- No friendly characters in the world
- No way to buy/sell items
- No story or lore

**Current State**: No NPCs exist
**Desired State**: Town hub with shops, quest givers, and dialogue

#### Technical Specifications

**Data Model - NPC.cs**

```csharp
[System.Serializable]
public partial class NPC : Resource
{
    [Export] public string NpcId { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public NPCType Type { get; set; }
    [Export] public Texture2D Portrait { get; set; }
    [Export] public DialogueTree Dialogue { get; set; }
    [Export] public ShopInventory Shop { get; set; }
    [Export] public string[] AvailableQuestIds { get; set; }
}

public enum NPCType
{
    Villager,
    Shopkeeper,
    QuestGiver,
    Blacksmith,
    Healer
}
```

**DialogueSystem.cs**

```csharp
public partial class DialogueSystem : Control
{
    [Signal] public delegate void DialogueEndedEventHandler();
    [Signal] public delegate void ChoiceMadeEventHandler(string choiceId);

    public void StartDialogue(DialogueTree tree);
    public void AdvanceDialogue();
    public void SelectChoice(int choiceIndex);
}

public class DialogueTree
{
    public List<DialogueNode> Nodes { get; set; }
    public string StartNodeId { get; set; }
}

public class DialogueNode
{
    public string NodeId { get; set; }
    public string Speaker { get; set; }
    public string Text { get; set; }
    public List<DialogueChoice> Choices { get; set; }
    public string NextNodeId { get; set; }
    public DialogueCondition Condition { get; set; }
}
```

**ShopSystem.cs**

```csharp
public partial class ShopSystem : Control
{
    [Signal] public delegate void PurchaseMadeEventHandler(Item item, int quantity);
    [Signal] public delegate void SaleMadeEventHandler(Item item, int quantity, int gold);

    public void OpenShop(ShopInventory inventory);
    public bool TryPurchase(Item item, int quantity);
    public bool TrySell(Item item, int quantity);
}

public class ShopInventory
{
    public List<ShopItem> Items { get; set; }
    public float BuyPriceMultiplier { get; set; } = 1.0f;
    public float SellPriceMultiplier { get; set; } = 0.5f;
}
```

---

### 3.3 Quest System

#### Executive Summary
- **Feature Name**: Quest System
- **One-Liner**: Kill/collect/explore quests with tracking UI
- **Strategic Rationale**: Quests provide goals, rewards, and narrative structure
- **Success Metrics**: > 3 quests completed per session

#### Technical Specifications

**Data Model - Quest.cs**

```csharp
[System.Serializable]
public partial class Quest : Resource
{
    [Export] public string QuestId { get; set; }
    [Export] public string Title { get; set; }
    [Export] public string Description { get; set; }
    [Export] public QuestType Type { get; set; }
    [Export] public Godot.Collections.Array<QuestObjective> Objectives { get; set; }
    [Export] public Godot.Collections.Array<QuestReward> Rewards { get; set; }
    [Export] public string RequiredQuestId { get; set; } // Prerequisite
    [Export] public int RequiredLevel { get; set; } = 1;
}

public enum QuestType
{
    Kill,      // Defeat X enemies of type Y
    Collect,   // Gather X items
    Explore,   // Visit location
    Boss,      // Defeat specific boss
    Escort,    // Protect NPC
    Delivery   // Bring item to NPC
}

[System.Serializable]
public class QuestObjective : Resource
{
    [Export] public string ObjectiveId { get; set; }
    [Export] public string Description { get; set; }
    [Export] public string TargetId { get; set; } // Enemy type, item id, location id
    [Export] public int RequiredCount { get; set; } = 1;
    public int CurrentCount { get; set; } = 0;

    public bool IsComplete => CurrentCount >= RequiredCount;
}

[System.Serializable]
public class QuestReward : Resource
{
    [Export] public QuestRewardType Type { get; set; }
    [Export] public int Value { get; set; }
    [Export] public Item ItemReward { get; set; }
}
```

**QuestManager.cs**

```csharp
public partial class QuestManager : Node
{
    [Signal] public delegate void QuestAcceptedEventHandler(Quest quest);
    [Signal] public delegate void QuestProgressEventHandler(Quest quest, QuestObjective objective);
    [Signal] public delegate void QuestCompletedEventHandler(Quest quest);

    public List<Quest> ActiveQuests { get; private set; } = new();
    public List<Quest> CompletedQuests { get; private set; } = new();

    public bool AcceptQuest(Quest quest);
    public void UpdateProgress(string targetType, string targetId, int count = 1);
    public bool TurnInQuest(Quest quest);
    public List<Quest> GetAvailableQuests(int playerLevel);
}
```

---

## Phase 4: Polish (LOWER PRIORITY)

### 4.1 Settings Menu

#### Executive Summary
- **Feature Name**: Settings Menu
- **One-Liner**: Audio, difficulty, and control customization
- **Success Metrics**: > 20% of players adjust settings

#### Features
- Master/Music/SFX volume sliders
- Difficulty selection (Easy/Normal/Hard)
- Control rebinding
- Display options (fullscreen, resolution)
- Auto-save toggle

---

### 4.2 Combat Animations and VFX

#### Executive Summary
- **Feature Name**: Combat Animations and VFX
- **One-Liner**: Attack animations, particles, and screen effects
- **Success Metrics**: Improved player satisfaction ratings

#### Features
- Attack swing animations
- Hit impact particles
- Spell casting effects
- Screen shake on crits
- Damage number polish

---

### 4.3 Sound Effects Integration

#### Executive Summary
- **Feature Name**: Sound Effects Integration
- **One-Liner**: Footsteps, battle SFX, and ambient music
- **Success Metrics**: Audio present in > 90% of game moments

#### Reference: `docs/SoundEffects.md` contains detailed requirements

#### Features
- Movement: Footsteps by terrain type
- Combat: Hit, miss, block, critical sounds
- UI: Button clicks, menu opens, notifications
- Ambient: Area-specific background music
- Victory/defeat fanfares

---

### 4.4 Minimap

#### Executive Summary
- **Feature Name**: Minimap
- **One-Liner**: Explored areas display with fog of war
- **Success Metrics**: > 50% of players use minimap

#### Features
- Corner HUD minimap
- Fog of war (unexplored = hidden)
- Player position indicator
- Enemy/NPC markers
- Stair markers
- Full map toggle (M key)

---

## Bonus Features

### Party System
- Control up to 4 characters
- Class system (Warrior, Mage, Healer, Rogue)
- Formation and targeting
- Party-wide inventory

### Crafting System
- Material gathering from enemies/chests
- Recipe discovery
- Equipment crafting
- Equipment upgrading/enchanting

### Roguelike Mode
- Permadeath option
- Daily challenge seeds
- Leaderboards
- Unlockable starting bonuses

---

## Implementation Timeline

### Quarter 1: Foundation (Weeks 1-12)

| Week | Features | Milestone |
|------|----------|-----------|
| 1-2 | Save/Load System | Player can save and continue |
| 3-4 | Loot Drop System | Enemies drop items |
| 5-6 | Consumable Items | Potions usable in battle |
| 7-9 | Skills/Abilities | Player has skill choices |
| 10-11 | Status Effects | Buffs/debuffs functional |
| 12 | Integration & Testing | Phase 1+2 complete |

### Quarter 2: Content (Weeks 13-24)

| Week | Features | Milestone |
|------|----------|-----------|
| 13-15 | Boss Fight Mechanics | First real boss fight |
| 16-18 | Procedural Generation | Infinite dungeon |
| 19-20 | NPC/Dialogue System | Town hub functional |
| 21-23 | Quest System | Quests give direction |
| 24 | Integration & Testing | Phase 3 complete |

### Quarter 3: Polish (Weeks 25-36)

| Week | Features | Milestone |
|------|----------|-----------|
| 25-26 | Settings Menu | Full customization |
| 27-29 | Combat VFX & Audio | Polished feel |
| 30-31 | Minimap | Navigation aid |
| 32-34 | Bug fixes & Balance | Stable release |
| 35-36 | Launch preparation | V1.0 ready |

---

## Risks and Mitigations

### Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Save corruption | Medium | High | Version migration, backup saves |
| Procedural gen performance | Medium | Medium | Async generation, LOD |
| Skill balance issues | High | Medium | Data-driven tuning, playtesting |
| Memory leaks in long sessions | Medium | High | Resource pooling, profiling |

### Design Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Feature creep | High | High | Strict scope management, MVP focus |
| Unbalanced loot | High | Medium | Drop rate analytics, tuning |
| Combat becoming trivial | Medium | High | Difficulty scaling, boss checkpoints |
| Player confusion | Medium | Medium | Tutorials, tooltips, UX testing |

### Process Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Scope underestimation | High | Medium | Buffer time, phased delivery |
| Art asset delays | Medium | Medium | Placeholder art, outsourcing |
| Testing bottleneck | Medium | High | Automated tests, beta testers |

---

## Appendix

### A. Glossary

| Term | Definition |
|------|------------|
| BSP | Binary Space Partitioning - algorithm for procedural room generation |
| DoT | Damage over Time - status effects that deal damage each turn |
| HoT | Heal over Time - status effects that heal each turn |
| MVP | Minimum Viable Product - smallest feature set for release |
| NPC | Non-Player Character - friendly characters in the world |
| VFX | Visual Effects - particles, animations, screen effects |

### B. Referenced Files

| File | Purpose |
|------|---------|
| `/scripts/game/GameManager.cs` | Global state singleton |
| `/scripts/data/Character.cs` | Player data model |
| `/scripts/data/Enemy.cs` | Enemy data with factory methods |
| `/scripts/data/Item.cs` | Item base class and categories |
| `/scripts/ui/BattleManager.cs` | Combat system |
| `/scripts/game/GridMap.cs` | World rendering |
| `/scripts/game/FloorManager.cs` | Floor transitions |
| `/docs/SoundEffects.md` | Audio requirements |
| `/docs/ASSET_REQUIREMENTS.md` | Art asset specs |

### C. Open Questions

1. **Monetization**: Will there be in-app purchases? Affects save system encryption.
2. **Multiplayer**: Any future co-op plans? Affects architecture decisions.
3. **Platform targets**: PC only or mobile? Affects UI design.
4. **Content cadence**: How often will new content be added post-launch?
5. **Localization**: Which languages to support? Affects dialogue system design.

### D. Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Jan 2026 | Initial PRD creation |

---

*Document generated for Sirius tactical RPG development. For questions or clarifications, contact the product owner.*
