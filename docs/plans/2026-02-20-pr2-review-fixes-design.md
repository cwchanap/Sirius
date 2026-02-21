# PR #2 Review Fixes — Design Document

**Date:** 2026-02-20
**Branch:** feature/loot-drop-system
**Scope:** Address all 27 issues identified in PR #2 review (4 critical, 13 important, 10 suggestions)

---

## Approach

5 parallel agents, each owning a distinct set of files, executed simultaneously. Results combined in a single build+test pass.

| Group | Files Owned | Key Issues |
|-------|-------------|------------|
| 1 — Data types | LootManager.cs, LootResult.cs, LootTable.cs | #5,6,8,9,20,21,22,23,27 |
| 2 — BattleManager | BattleManager.cs | Critical #1,#2, #10,16,24,25,26 |
| 3 — Enemy arch | EnemyTypeId.cs (new), Enemy.cs, EnemyBlueprint.cs, EnemySpawn.cs | Critical #3, #11,17,18,19 |
| 4 — Catalog | LootTableCatalog.cs, Item.cs | #15, comment fixes |
| 5 — Tests | LootDropSystemTest.cs, EnemySpawnTest.cs | Critical #4, #7,12,13,14 |

---

## Section 1: Critical Bug Fixes

### BattleManager.cs

**Fix 1: Null guards in `StartBattle`**
Add at the top of `StartBattle`, before any field access:
```csharp
if (player == null)
{
    GD.PrintErr("[BattleManager] StartBattle called with null player; aborting battle.");
    _resultEmitted = true;
    EmitSignal(SignalName.BattleFinished, false, true);
    return;
}
if (enemy == null)
{
    GD.PrintErr("[BattleManager] StartBattle called with null enemy; aborting battle.");
    _resultEmitted = true;
    EmitSignal(SignalName.BattleFinished, false, true);
    return;
}
```

**Fix 2: Stale `_lootLabel` cleanup in `StartBattle`**
After `_pendingLootDisplay = null;`:
```csharp
if (_lootLabel != null && IsInstanceValid(_lootLabel))
{
    _lootLabel.QueueFree();
    _lootLabel = null;
}
```

### EnemySpawn.cs

**Fix 3: Log errors in `CreateEnemyInstance` fallback paths**

Switch default:
```csharp
_ => {
    GD.PrintErr($"[EnemySpawn] Unknown EnemyType '{type}' at GridPosition {GridPosition}; defaulting to Goblin. Check the EnemyType property.");
    return Enemy.CreateGoblin();
}
```

Ultimate fallback:
```csharp
GD.PrintErr($"[EnemySpawn] No Blueprint and no EnemyType set at GridPosition {GridPosition}; defaulting to Goblin.");
return Enemy.CreateGoblin();
```

**Fix 4: Validate Blueprint property values after `Get()`**
After reading `spriteType` and `name`:
```csharp
if (string.IsNullOrEmpty(spriteType))
    GD.PrintErr($"[EnemySpawn] Blueprint at GridPosition {GridPosition} has empty SpriteType; loot table lookup will fail.");
if (string.IsNullOrEmpty(name))
    GD.PushWarning($"[EnemySpawn] Blueprint at GridPosition {GridPosition} has empty EnemyName.");
```

---

## Section 2: Loot Core Logic Fixes

### LootTable.cs

- **`MaxDrops` validated setter:** backing field `_maxDrops = 3`, setter clamps to `Math.Max(0, value)`, emits `GD.PushWarning` on negative input.
- **`Entries` null-coalescing setter:** `set => _entries = value ?? new List<LootEntry>();`
- **Remove `LootEntry.ValidateAndNormalizeQuantityRange()`** — dead code. `LootManager.ResolveQuantity` normalizes locally without calling it.

### LootResult.cs

- **`Add()` null-item path:** change from warn+skip to `throw new ArgumentNullException(nameof(item))` — consistent with `LootResultEntry` constructor.
- **`_droppedItemsView` field comment:** `// Not readonly: deserialization bypasses the constructor; recreated lazily on first access.`

### LootManager.cs

- **Slot accounting fix:** replace `for (int i = 0; i < remainingSlots; i++)` with:
  ```csharp
  int added = 0;
  int attempts = 0;
  int maxAttempts = remainingSlots * 2;
  while (added < remainingSlots && attempts++ < maxAttempts)
  {
      var entry = PickWeightedEntry(weighted, rng);
      if (entry == null) break;
      var item = ItemCatalog.CreateItemById(entry.ItemId);
      if (item == null) { GD.PushWarning(...); continue; }
      int qty = ResolveQuantity(entry, rng);
      result.Add(item, qty);
      added++;
  }
  ```
- **`AwardLootToCharacter` severity fix:** when `added == 0`, remove the `GD.PushWarning`; let the `GD.PrintErr` in the overflow branch handle it as a single error message.
- **`ResolveQuantity` `in` modifier:** `private static int ResolveQuantity(in LootEntry entry, Random rng)`
- **Comment fix:** "Guaranteed drops always included" → "Guaranteed drops are included when the DropChance roll succeeds."

---

## Section 3: Architecture

### New file: `scripts/data/EnemyTypeId.cs`

```csharp
/// <summary>String constants for all 14 enemy types. Single source of truth referenced by
/// Enemy.Create*(), EnemyBlueprint.SpriteType, and LootTableCatalog.GetByEnemyType().</summary>
public static class EnemyTypeId
{
    public const string Goblin          = "goblin";
    public const string Orc             = "orc";
    public const string Dragon          = "dragon";
    public const string SkeletonWarrior = "skeleton_warrior";
    public const string Troll           = "troll";
    public const string DarkMage        = "dark_mage";
    public const string DemonLord       = "demon_lord";
    public const string Boss            = "boss";
    public const string ForestSpirit    = "forest_spirit";
    public const string CaveSpider      = "cave_spider";
    public const string DesertScorpion  = "desert_scorpion";
    public const string SwampWretch     = "swamp_wretch";
    public const string MountainWyvern  = "mountain_wyvern";
    public const string DungeonGuardian = "dungeon_guardian";
}
```

All `EnemyType = "goblin"` string literals in `Enemy.cs`, `EnemyBlueprint.cs`, and `LootTableCatalog.cs` switch arms replaced with `EnemyTypeId.Goblin` etc.

### EnemyBlueprint.cs — add 10 missing factory methods

Add `CreateSkeletonWarriorBlueprint`, `CreateTrollBlueprint`, `CreateDarkMageBlueprint`, `CreateDemonLordBlueprint`, `CreateForestSpiritBlueprint`, `CreateCaveSpiderBlueprint`, `CreateDesertScorpionBlueprint`, `CreateSwampWretchBlueprint`, `CreateMountainWyvernBlueprint`, `CreateDungeonGuardianBlueprint`.

Stats copied verbatim from the corresponding `Enemy.Create*()` method. `SpriteType` set to matching `EnemyTypeId` constant.

### Enemy.cs — consolidate all 14 factory methods

All 14 become one-liners:
```csharp
public static Enemy CreateGoblin() => EnemyBlueprint.CreateGoblinBlueprint().CreateEnemy();
```

### LootTableCatalog.cs — rename factory methods

`GoblinDrops()` → `CreateGoblinTable()` (and all 14 equivalents). `BattleManager` is unaffected (only calls `GetByEnemyType()`). Test file updates happen in Group 5.

### Item.cs — `Rarity` doc comment

Keep public setter (external catalog factories use object initializers — making it protected would require a refactor cascade through all 5 catalog classes). Add XML doc comment explaining the intentional difference from `Category`.

---

## Section 4: Test Coverage

### LootDropSystemTest.cs

| Test | Description |
|------|-------------|
| `LootTableCatalog_AllEntries_ItemIdsExistInCatalog` | For all 14 enemy types, every `entry.ItemId` asserts `ItemCatalog.ItemExists(entry.ItemId)`. Critical regression guard. |
| `LootManager_AwardLootToCharacter_NullResult_DoesNotThrow` | Pass `null` result; assert no exception. |
| `LootManager_AwardLootToCharacter_NullPlayer_DoesNotThrow` | Pass `null` player; assert no exception. |
| `LootTableCatalog_GetByEnemyType_IsCaseInsensitive` | "GOBLIN" and "Dragon" return non-null tables. |
| `LootManager_RollLoot_GuaranteedDropsExceedMaxDrops_AllGuaranteedIncluded` | 2 guaranteed entries + `MaxDrops=1` → result has 2 drops (documents contract). |
| `LootEntry_NegativeWeight_IsClampedToZero` | `new LootEntry { Weight = -50 }` → `Weight == 0`. |
| Fix obsolete API | `FormatterServices.GetUninitializedObject` → `RuntimeHelpers.GetUninitializedObject`. |
| Rename catalog calls | `LootTableCatalog.GoblinDrops()` → `LootTableCatalog.CreateGoblinTable()` (2 locations). |

### EnemySpawnTest.cs

Add: `CreateEnemyInstance_UnknownEnemyType_FallsBackToGoblin` — sets `EnemyType = "zombie"`, `Blueprint = null`; asserts enemy is a goblin (documents silent fallback behavior that now logs an error).

---

## Section 5: Comments & Minor Cleanup

### LootTableCatalog.cs
- Class summary: replace "Used by Enemy.CreateX() static factory methods" → "Used by BattleManager.EndBattle() via GetByEnemyType() after combat resolves."
- `GetByEnemyType` doc: append "The lookup is case-insensitive; input is lowercased before matching."

### EnemyBlueprint.cs
- `CreateEnemy()` doc: append "EnemyType is set from SpriteType, used by LootTableCatalog.GetByEnemyType() to look up loot tables after combat."
- Class summary: append "To give a spawn node unique stats, use 'Make Unique' in the Godot Inspector or set EnemySpawn.AutoMakeBlueprintUnique = true."

### BattleManager.cs
- Remove debug `GD.Print` emoji blocks in `SetupCharacterAnimations`.
- Change missing-sprite-sheet `GD.Print("Warning: ...")` → `GD.PushWarning(...)` (2 occurrences).
- Add `// TODO: use _enemy.EnemyType to select sprite path` to hardcoded goblin path.
- Change magenta fallback `Color(1,0,1,1)` → `Color(0.1f, 0.1f, 0.1f, 1f)` with updated comment.
- Fix `EnemyTurn` comment: note `_player.MaxHealth` used, not `GetEffectiveMaxHealth()`.
- Fix `PlayerAutoAction` doc: "defends with 30% probability when health drops below 40%, otherwise attacks."
- Fix `StartBattle` turn-order doc: "Player goes first if Speed ≥ enemy Speed (ties favor the player)."

### LootManager.cs
- Comment: "Guaranteed drops always included" → "Guaranteed drops are included when the DropChance roll succeeds."

---

## Success Criteria

1. `dotnet build Sirius.sln` — 0 errors, 0 new warnings
2. `dotnet test Sirius.sln --settings test.runsettings.local` — all tests pass including 6+ new tests
3. All 27 review issues addressed (verified by checklist)
