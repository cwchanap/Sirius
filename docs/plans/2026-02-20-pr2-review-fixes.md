# PR #2 Review Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Address all 27 issues from the PR #2 code review across 5 independent file groups.

**Architecture:** 5 parallel task groups own non-overlapping files. Each group makes all its changes then commits. A final build+test pass validates the combined result.

**Tech Stack:** C# / .NET 8, Godot 4.5.1, GdUnit4 test framework. Test command: `dotnet test Sirius.sln --settings test.runsettings.local`

---

## Execution Order

Run Groups 1–5 in **parallel** (they own non-overlapping files). Then run Task 6 (final verification).

| Group | Files | Issues |
|-------|-------|--------|
| 1 | LootManager.cs, LootResult.cs, LootTable.cs | #5,6,8,9,20,21,22,23,27 |
| 2 | BattleManager.cs | Critical #1,#2, #10,16,24,25,26 |
| 3 | EnemyTypeId.cs (NEW), Enemy.cs, EnemyBlueprint.cs, EnemySpawn.cs | Critical #3, #11,17,18,19 |
| 4 | LootTableCatalog.cs, Item.cs | #15, catalog rename, comment fixes |
| 5 | LootDropSystemTest.cs, EnemySpawnTest.cs | Critical #4, #7,12,13,14 + catalog rename update |

---

## Group 1: Loot Core Data Types

**Files:**
- Modify: `scripts/data/LootTable.cs`
- Modify: `scripts/data/LootResult.cs`
- Modify: `scripts/data/LootManager.cs`

### Task 1.1: LootTable — MaxDrops validation + Entries null-coalescing + remove dead method

**Step 1: Apply all LootTable.cs changes**

Replace the entire file `scripts/data/LootTable.cs` with:

```csharp
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines the possible item drops for an enemy encounter.
/// Constructed in code via LootTableCatalog or from EnemyBlueprint exports.
/// DropChance is clamped to [0.0, 1.0] on assignment.
/// MaxDrops is clamped to >= 0 on assignment; it caps only weighted draws,
/// not guaranteed drops.
/// </summary>
[System.Serializable]
public class LootTable
{
    private int _maxDrops = 3;
    public int MaxDrops
    {
        get => _maxDrops;
        set
        {
            if (value < 0)
                GD.PushWarning($"[LootTable] MaxDrops cannot be negative (got {value}); clamping to 0.");
            _maxDrops = Math.Max(0, value);
        }
    }

    private float _dropChance = 1.0f;
    public float DropChance
    {
        get => _dropChance;
        set => _dropChance = Math.Clamp(value, 0f, 1f);
    }

    private List<LootEntry> _entries = new();
    public List<LootEntry> Entries
    {
        get => _entries;
        set => _entries = value ?? new List<LootEntry>();
    }

    /// <summary>Returns non-null guaranteed entries (GuaranteedDrop = true).</summary>
    public List<LootEntry> GetGuaranteedEntries()
    {
        var result = new List<LootEntry>();
        foreach (var entry in _entries)
        {
            if (entry != null && entry.GuaranteedDrop)
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// Returns weighted (non-guaranteed) entries eligible for random selection.
    /// Entries with Weight = 0 are excluded.
    /// </summary>
    public List<LootEntry> GetWeightedEntries()
    {
        var result = new List<LootEntry>();
        foreach (var entry in _entries)
        {
            if (entry != null && !entry.GuaranteedDrop && entry.Weight > 0)
                result.Add(entry);
        }
        return result;
    }
}

/// <summary>
/// A single item drop entry in a LootTable.
/// Note: when GuaranteedDrop is true, Weight is ignored — set it to 0 by convention
/// to avoid the entry appearing in weighted draws.
/// </summary>
[System.Serializable]
public class LootEntry
{
    public string ItemId { get; set; } = string.Empty;

    private int _weight = 100;
    public int Weight
    {
        get => _weight;
        set => _weight = Math.Max(0, value);
    }

    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
    public bool GuaranteedDrop { get; set; } = false;
}
```

Note: `ValidateAndNormalizeQuantityRange()` is removed — it was dead code (`LootManager.ResolveQuantity` normalizes locally without calling it). The test for it will be removed in Group 5.

**Step 2: Build to verify no errors**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/data/LootTable.cs
git commit -m "fix: add MaxDrops validation, Entries null-coalescing; remove dead ValidateAndNormalizeQuantityRange"
```

---

### Task 1.2: LootResult — consistent null-item enforcement + field comment

**Step 1: Apply LootResult.cs changes**

In `scripts/data/LootResult.cs`, make these two edits:

**Edit 1** — Add comment to `_droppedItemsView` field (line 15):
```csharp
// Not readonly: deserialization bypasses the constructor; recreated lazily on first access.
private ReadOnlyCollection<LootResultEntry> _droppedItemsView;
```

**Edit 2** — In `Add()`, replace the null-item warning+skip with a throw (lines 31-35):
```csharp
if (item == null)
    throw new ArgumentNullException(nameof(item), "[LootResult] Add called with null item.");
```

The existing `GD.PushWarning` line and `return;` are removed. The method now throws, consistent with `LootResultEntry`'s constructor.

> **Important:** The existing test `LootResult_Add_IgnoresNullItem` at line 416 in `LootDropSystemTest.cs` asserts the old warn+skip behavior. Group 5 will update that test to assert a throw instead. Do NOT touch the test file in this group.

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/data/LootResult.cs
git commit -m "fix: LootResult.Add throws on null item (consistent with LootResultEntry ctor); add field comment"
```

---

### Task 1.3: LootManager — slot accounting fix + severity fix + `in` modifier + comment fix

**Step 1: Apply all LootManager.cs changes**

Replace the entire file `scripts/data/LootManager.cs` with:

```csharp
using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Static service for loot generation and award.
/// RollLoot is pure logic (fully testable).
/// AwardLootToCharacter has side effects: it writes to player.Inventory and may
/// route overflow to RecoveryChest.Instance. If RecoveryChest.Instance is null,
/// overflow items are permanently discarded and an error is logged.
/// </summary>
public static class LootManager
{
    public static LootResult RollLoot(LootTable? table, Random rng)
    {
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        if (table == null)
            return LootResult.Empty;

        if (rng.NextDouble() >= table.DropChance)
            return LootResult.Empty;

        var result = new LootResult();

        // Guaranteed drops are included when the DropChance roll succeeds.
        // They are not capped by MaxDrops — MaxDrops applies only to weighted draws.
        foreach (var entry in table.GetGuaranteedEntries())
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item == null)
            {
                GD.PushWarning($"[LootManager] Unknown itemId '{entry.ItemId}' in LootTable - skipping");
                continue;
            }
            int qty = ResolveQuantity(entry, rng);
            result.Add(item, qty);
        }

        // Weighted random draws up to MaxDrops slots.
        // A null/unknown itemId skips that entry but does NOT consume a slot.
        // maxAttempts guards against an infinite loop if all weighted entries have unknown itemIds.
        int remainingSlots = Math.Max(0, table.MaxDrops - result.DroppedItems.Count);
        if (remainingSlots > 0)
        {
            var weighted = table.GetWeightedEntries();
            if (weighted.Count > 0)
            {
                int added = 0;
                int attempts = 0;
                int maxAttempts = remainingSlots * 2;
                while (added < remainingSlots && attempts++ < maxAttempts)
                {
                    var entry = PickWeightedEntry(weighted, rng);
                    if (entry == null) break;
                    var item = ItemCatalog.CreateItemById(entry.ItemId);
                    if (item == null)
                    {
                        GD.PushWarning($"[LootManager] Unknown itemId '{entry.ItemId}' in LootTable - skipping");
                        continue;
                    }
                    int qty = ResolveQuantity(entry, rng);
                    result.Add(item, qty);
                    added++;
                }
            }
        }

        return result;
    }

    public static void AwardLootToCharacter(LootResult result, Character player)
    {
        if (result == null)
        {
            GD.PrintErr("[LootManager] AwardLootToCharacter called with null result.");
            return;
        }
        if (player == null)
        {
            GD.PrintErr("[LootManager] AwardLootToCharacter called with null player; loot will not be awarded.");
            return;
        }
        if (!result.HasDrops)
            return;

        foreach (var entry in result.DroppedItems)
        {
            player.TryAddItem(entry.Item, entry.Quantity, out int added);

            if (added > 0)
            {
                GD.Print($"[LootManager] Awarded {added}x '{entry.Item.DisplayName}' to {player.Name}");
            }

            int overflow = entry.Quantity - added;
            if (overflow > 0)
            {
                if (RecoveryChest.Instance != null)
                {
                    RecoveryChest.Instance.AddOverflow(entry.Item.Id, overflow);
                    GD.Print($"[LootManager] {overflow}x '{entry.Item.DisplayName}' sent to RecoveryChest");
                }
                else
                {
                    GD.PrintErr($"[LootManager] RecoveryChest.Instance is null; {overflow}x " +
                                $"'{entry.Item.DisplayName}' (id='{entry.Item.Id}') permanently lost " +
                                $"for player '{player.Name}'");
                }
            }
        }
    }

    private static int ResolveQuantity(in LootEntry entry, Random rng)
    {
        int rawMin = entry.MinQuantity;
        int rawMax = entry.MaxQuantity;

        // Normalize locally without mutating the entry
        int min = Math.Max(1, Math.Min(rawMin, rawMax));
        int max = Math.Max(min, Math.Max(rawMin, rawMax));

        if (rawMin > rawMax)
        {
            GD.PushWarning($"[LootManager] LootEntry quantity range invalid for itemId '{entry.ItemId}' " +
                           $"(MinQuantity={rawMin}, MaxQuantity={rawMax}); normalizing locally.");
        }

        return rng.Next(min, max + 1);
    }

    private static LootEntry? PickWeightedEntry(List<LootEntry> entries, Random rng)
    {
        if (entries.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in entries) totalWeight += e.Weight;
        if (totalWeight <= 0) return null;

        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;
        foreach (var e in entries)
        {
            cumulative += e.Weight;
            if (roll < cumulative) return e;
        }
        // Fallback: floating-point/integer boundary guard; should not be reached with valid weights.
        return entries[entries.Count - 1];
    }
}
```

Key changes:
- `added == 0` `GD.PushWarning` removed — the `overflow` path below emits `GD.PrintErr` if needed (single error for same event)
- `for (int i …)` loop → `while (added < remainingSlots && attempts++ < maxAttempts)` — unknown itemIds no longer consume slots
- `ResolveQuantity` parameter gets `in` modifier
- Class summary updated to describe overflow behavior accurately
- Comment "Guaranteed drops always included" → accurate description

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/data/LootManager.cs
git commit -m "fix: unknown itemId no longer wastes slot; remove severity mismatch; add in modifier; fix comment"
```

---

## Group 2: BattleManager

**Files:**
- Modify: `scripts/ui/BattleManager.cs`

### Task 2.1: Critical — null guards in StartBattle

**Step 1: Edit `StartBattle` method (around line 214)**

Insert null guards at the very top of `StartBattle`, before any field access:

Replace:
```csharp
public void StartBattle(Character player, Enemy enemy)
{
    GD.Print($"BattleManager.StartBattle called: {player.Name} vs {enemy.Name}");

    _player = player;
    _enemy = enemy;
    _playerTurn = _player.Speed >= _enemy.Speed; // Faster character goes first
    _battleInProgress = false; // Wait for Start button
    _pendingLootDisplay = null; // Clear any stale loot from a previous battle
```

With:
```csharp
/// <summary>
/// Initializes the battle with the given combatants and sets up UI.
/// Player goes first if Speed >= enemy Speed (ties favor the player).
/// </summary>
public void StartBattle(Character player, Enemy enemy)
{
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

    GD.Print($"BattleManager.StartBattle called: {player.Name} vs {enemy.Name}");

    _player = player;
    _enemy = enemy;
    _playerTurn = _player.Speed >= _enemy.Speed;
    _battleInProgress = false;
    _pendingLootDisplay = null;
```

**Step 2: Add `_lootLabel` cleanup after `_pendingLootDisplay = null`**

After `_pendingLootDisplay = null;`, add:
```csharp
    // Clean up any loot label left from a previous battle
    if (_lootLabel != null && IsInstanceValid(_lootLabel))
    {
        _lootLabel.QueueFree();
        _lootLabel = null;
    }
```

**Step 3: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 4: Commit**

```bash
git add scripts/ui/BattleManager.cs
git commit -m "fix: null guards in StartBattle prevent NRE game-state lock; clean stale _lootLabel"
```

---

### Task 2.2: Debug print cleanup in SetupCharacterAnimations

`SetupCharacterAnimations` (lines 292–435) contains large blocks of emoji `GD.Print` debug statements from a transparency investigation. Remove them and fix the fallback `GD.Print("Warning:")` calls.

**Step 1: Replace the player texture loaded block**

The block from `GD.Print("🎮 Player texture loaded:")` through `GD.Print($"   📏 Sprite scale: {_playerSprite.Scale}");` (lines 301–352) — remove all those `GD.Print` calls. Keep all the functional code (`playerSpriteFrames.AddAnimation`, `AtlasTexture` construction, `_playerSprite.SpriteFrames = ...`, etc.).

Similarly remove the enemy texture `GD.Print` blocks (lines 377–427).

Also remove: `GD.Print("✅ Battle background loaded successfully")` at line 151, and `GD.Print($"✅ Battle background loaded successfully")` — keep the functional texture/rect code.

**Step 2: Fix `GD.Print("Warning: ...")` → `GD.PushWarning`**

Replace (line 357):
```csharp
GD.Print("Warning: Player sprite sheet not found, using fallback");
```
With:
```csharp
GD.PushWarning("[BattleManager] Player sprite sheet not found; using fallback rendering.");
```

Replace (line 431):
```csharp
GD.Print("Warning: Enemy goblin sprite sheet not found, using fallback");
```
With:
```csharp
// TODO: use _enemy.EnemyType to select the correct sprite path (currently always loads goblin)
GD.PushWarning("[BattleManager] Enemy sprite sheet not found; using fallback rendering.");
```

**Step 3: Fix magenta fallback color (line 167)**

Replace:
```csharp
colorRect.Color = new Color(1.0f, 0.0f, 1.0f, 1.0f); // Bright magenta for testing transparency
AddChild(colorRect);
MoveChild(colorRect, 0);
GD.Print("⚠️ Battle background not found, using fallback color (bright magenta for transparency testing)");
```
With:
```csharp
colorRect.Color = new Color(0.1f, 0.1f, 0.1f, 1.0f); // Dark fallback — replace with proper asset before shipping
AddChild(colorRect);
MoveChild(colorRect, 0);
GD.PushWarning("[BattleManager] Battle background asset not found; using dark color fallback.");
```

**Step 4: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 5: Commit**

```bash
git add scripts/ui/BattleManager.cs
git commit -m "fix: remove debug emoji prints; GD.PushWarning for missing sprites; neutral fallback color"
```

---

### Task 2.3: Comment fixes in BattleManager

**Step 1: Fix `_resultEmitted` field comment (line 39)**

Replace:
```csharp
private bool _resultEmitted = false; // Ensure we only emit BattleFinished once
```
With:
```csharp
private bool _resultEmitted = false; // Guards against double-emission in the common case; timer stop and signal emit must always be called together
```

**Step 2: Fix `PlayerAutoAction` doc comment (line 545)**

Replace:
```csharp
// Player automatically chooses the best action based on situation
```
With:
```csharp
// Player auto-AI: defends with 30% probability when health drops below 40%, otherwise attacks.
```

**Step 3: Fix `EnemyTurn` health percentage note (line 598)**

Replace:
```csharp
float playerHealthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;
```
With:
```csharp
// Note: uses base MaxHealth (not GetEffectiveMaxHealth()); equipment bonuses are not reflected in this threshold.
float playerHealthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;
```

**Step 4: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 5: Commit**

```bash
git add scripts/ui/BattleManager.cs
git commit -m "fix: update stale BattleManager comments for accuracy"
```

---

## Group 3: Enemy Architecture

**Files:**
- Create: `scripts/data/EnemyTypeId.cs`
- Modify: `scripts/data/EnemyBlueprint.cs`
- Modify: `scripts/data/Enemy.cs`
- Modify: `scripts/game/EnemySpawn.cs`

### Task 3.1: Create EnemyTypeId.cs

**Step 1: Create `scripts/data/EnemyTypeId.cs`**

```csharp
/// <summary>
/// String constants for all 14 enemy types.
/// Single source of truth referenced by Enemy.Create*(), EnemyBlueprint.SpriteType,
/// and LootTableCatalog.GetByEnemyType().
/// Adding a new enemy type: add a constant here, then add a blueprint factory in
/// EnemyBlueprint, a factory method in Enemy, and a table method in LootTableCatalog.
/// </summary>
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

**Step 2: Build to verify it compiles**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/data/EnemyTypeId.cs
git commit -m "feat: add EnemyTypeId constants class — single source of truth for enemy type strings"
```

---

### Task 3.2: EnemyBlueprint — add 10 missing factory methods + update docs + use EnemyTypeId

**Step 1: Update existing factory methods to use EnemyTypeId constants**

In `scripts/data/EnemyBlueprint.cs`, replace all `SpriteType = "goblin"` etc. with `SpriteType = EnemyTypeId.Goblin` etc. in the 4 existing factories.

**Step 2: Update class summary and CreateEnemy doc**

Replace class summary (lines 3-5):
```csharp
/// <summary>
/// Blueprint Resource for enemy spawning. Create instances via Godot editor and assign to EnemySpawn nodes.
/// Each blueprint can be cloned and customized with unique stats for level design.
/// To give a spawn node unique stats, use 'Make Unique' in the Godot Inspector or
/// set EnemySpawn.AutoMakeBlueprintUnique = true.
/// </summary>
```

Replace `CreateEnemy()` doc (lines 36-38):
```csharp
/// <summary>
/// Create an Enemy instance from this blueprint with fresh CurrentHealth equal to MaxHealth.
/// EnemyType is set from SpriteType, used by LootTableCatalog.GetByEnemyType() to look up
/// loot tables after combat.
/// </summary>
```

**Step 3: Add 10 missing factory methods**

Append after `CreateBossBlueprint()` (before the closing `}`):

```csharp
/// <summary>Factory method: Create a Skeleton Warrior blueprint with default stats.</summary>
public static EnemyBlueprint CreateSkeletonWarriorBlueprint() => new EnemyBlueprint
{
    EnemyName = "Skeleton Warrior", SpriteType = EnemyTypeId.SkeletonWarrior,
    Level = 3, MaxHealth = 120, Attack = 28, Defense = 12, Speed = 9,
    ExperienceReward = 70, GoldReward = 30
};

/// <summary>Factory method: Create a Troll blueprint with default stats.</summary>
public static EnemyBlueprint CreateTrollBlueprint() => new EnemyBlueprint
{
    EnemyName = "Troll", SpriteType = EnemyTypeId.Troll,
    Level = 4, MaxHealth = 150, Attack = 35, Defense = 15, Speed = 6,
    ExperienceReward = 120, GoldReward = 50
};

/// <summary>Factory method: Create a Dark Mage blueprint with default stats.</summary>
public static EnemyBlueprint CreateDarkMageBlueprint() => new EnemyBlueprint
{
    EnemyName = "Dark Mage", SpriteType = EnemyTypeId.DarkMage,
    Level = 6, MaxHealth = 180, Attack = 50, Defense = 18, Speed = 14,
    ExperienceReward = 220, GoldReward = 120
};

/// <summary>Factory method: Create a Demon Lord blueprint with default stats.</summary>
public static EnemyBlueprint CreateDemonLordBlueprint() => new EnemyBlueprint
{
    EnemyName = "Demon Lord", SpriteType = EnemyTypeId.DemonLord,
    Level = 8, MaxHealth = 300, Attack = 65, Defense = 25, Speed = 15,
    ExperienceReward = 400, GoldReward = 200
};

/// <summary>Factory method: Create a Forest Spirit blueprint with default stats.</summary>
public static EnemyBlueprint CreateForestSpiritBlueprint() => new EnemyBlueprint
{
    EnemyName = "Forest Spirit", SpriteType = EnemyTypeId.ForestSpirit,
    Level = 2, MaxHealth = 90, Attack = 20, Defense = 10, Speed = 15,
    ExperienceReward = 50, GoldReward = 22
};

/// <summary>Factory method: Create a Giant Cave Spider blueprint with default stats.</summary>
public static EnemyBlueprint CreateCaveSpiderBlueprint() => new EnemyBlueprint
{
    EnemyName = "Giant Cave Spider", SpriteType = EnemyTypeId.CaveSpider,
    Level = 3, MaxHealth = 110, Attack = 25, Defense = 8, Speed = 18,
    ExperienceReward = 65, GoldReward = 28
};

/// <summary>Factory method: Create a Desert Scorpion blueprint with default stats.</summary>
public static EnemyBlueprint CreateDesertScorpionBlueprint() => new EnemyBlueprint
{
    EnemyName = "Desert Scorpion", SpriteType = EnemyTypeId.DesertScorpion,
    Level = 4, MaxHealth = 130, Attack = 32, Defense = 14, Speed = 11,
    ExperienceReward = 95, GoldReward = 45
};

/// <summary>Factory method: Create a Swamp Wretch blueprint with default stats.</summary>
public static EnemyBlueprint CreateSwampWretchBlueprint() => new EnemyBlueprint
{
    EnemyName = "Swamp Wretch", SpriteType = EnemyTypeId.SwampWretch,
    Level = 5, MaxHealth = 160, Attack = 38, Defense = 16, Speed = 7,
    ExperienceReward = 140, GoldReward = 70
};

/// <summary>Factory method: Create a Mountain Wyvern blueprint with default stats.</summary>
public static EnemyBlueprint CreateMountainWyvernBlueprint() => new EnemyBlueprint
{
    EnemyName = "Mountain Wyvern", SpriteType = EnemyTypeId.MountainWyvern,
    Level = 6, MaxHealth = 220, Attack = 48, Defense = 22, Speed = 16,
    ExperienceReward = 200, GoldReward = 110
};

/// <summary>Factory method: Create a Dungeon Guardian blueprint with default stats.</summary>
public static EnemyBlueprint CreateDungeonGuardianBlueprint() => new EnemyBlueprint
{
    EnemyName = "Dungeon Guardian", SpriteType = EnemyTypeId.DungeonGuardian,
    Level = 7, MaxHealth = 280, Attack = 55, Defense = 28, Speed = 10,
    ExperienceReward = 300, GoldReward = 150
};
```

**Step 4: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 5: Commit**

```bash
git add scripts/data/EnemyBlueprint.cs
git commit -m "feat: add 10 missing EnemyBlueprint factories; use EnemyTypeId constants; update docs"
```

---

### Task 3.3: Enemy.cs — consolidate all 14 factory methods to delegate to blueprints

**Step 1: Replace all 14 factory methods in `scripts/data/Enemy.cs`**

Replace the entire body of each `Create*` method with a one-liner delegation. The result for all 14:

```csharp
public static Enemy CreateGoblin()          => EnemyBlueprint.CreateGoblinBlueprint().CreateEnemy();
public static Enemy CreateOrc()             => EnemyBlueprint.CreateOrcBlueprint().CreateEnemy();
public static Enemy CreateDragon()          => EnemyBlueprint.CreateDragonBlueprint().CreateEnemy();
public static Enemy CreateSkeletonWarrior() => EnemyBlueprint.CreateSkeletonWarriorBlueprint().CreateEnemy();
public static Enemy CreateTroll()           => EnemyBlueprint.CreateTrollBlueprint().CreateEnemy();
public static Enemy CreateDarkMage()        => EnemyBlueprint.CreateDarkMageBlueprint().CreateEnemy();
public static Enemy CreateDemonLord()       => EnemyBlueprint.CreateDemonLordBlueprint().CreateEnemy();
public static Enemy CreateBoss()            => EnemyBlueprint.CreateBossBlueprint().CreateEnemy();
public static Enemy CreateForestSpirit()    => EnemyBlueprint.CreateForestSpiritBlueprint().CreateEnemy();
public static Enemy CreateCaveSpider()      => EnemyBlueprint.CreateCaveSpiderBlueprint().CreateEnemy();
public static Enemy CreateDesertScorpion()  => EnemyBlueprint.CreateDesertScorpionBlueprint().CreateEnemy();
public static Enemy CreateSwampWretch()     => EnemyBlueprint.CreateSwampWretchBlueprint().CreateEnemy();
public static Enemy CreateMountainWyvern()  => EnemyBlueprint.CreateMountainWyvernBlueprint().CreateEnemy();
public static Enemy CreateDungeonGuardian() => EnemyBlueprint.CreateDungeonGuardianBlueprint().CreateEnemy();
```

The non-factory members (`Name`, `EnemyType`, `Level`, etc. properties, `IsAlive`, `TakeDamage`) remain unchanged.

Remove the `// Additional enemy types for the larger world` and `// Additional enemy types for specific areas` comments as the methods are now one-liners that don't need sectioning.

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Verify behavior is preserved (existing tests should still pass)**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "Enemy_AllFactoryMethodsSetEnemyType" 2>&1 | tail -10
```
Expected: `Passed!`

**Step 4: Commit**

```bash
git add scripts/data/Enemy.cs
git commit -m "refactor: Enemy.Create*() delegates to EnemyBlueprint factories — single source of truth for stats"
```

---

### Task 3.4: EnemySpawn.cs — error logging for fallback paths + blueprint validation

**Step 1: Add error logging to fallback paths in `CreateEnemyInstance`**

In `scripts/game/EnemySpawn.cs`, in `CreateEnemyInstance()`:

**Edit 1** — Replace the silent switch default (line 378):
```csharp
_ => Enemy.CreateGoblin() // default fallback
```
With:
```csharp
_ => {
    GD.PrintErr($"[EnemySpawn] Unknown EnemyType '{type}' at GridPosition {GridPosition}; defaulting to Goblin. Check the EnemyType property.");
    return Enemy.CreateGoblin();
}
```

**Edit 2** — Replace the silent ultimate fallback (lines 382-383):
```csharp
// Ultimate fallback
return Enemy.CreateGoblin();
```
With:
```csharp
// Ultimate fallback: no Blueprint and no EnemyType set
GD.PrintErr($"[EnemySpawn] No Blueprint and no EnemyType set at GridPosition {GridPosition}; defaulting to Goblin.");
return Enemy.CreateGoblin();
```

**Edit 3** — Add blueprint property validation after the `Get()` calls (after line 341, before the `return new Enemy`):

```csharp
if (string.IsNullOrEmpty(spriteType))
    GD.PrintErr($"[EnemySpawn] Blueprint at GridPosition {GridPosition} has empty SpriteType; loot table lookup will fail.");
if (string.IsNullOrEmpty(name))
    GD.PushWarning($"[EnemySpawn] Blueprint at GridPosition {GridPosition} has empty EnemyName.");
```

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/game/EnemySpawn.cs
git commit -m "fix: log errors for silent goblin fallbacks in EnemySpawn; validate blueprint properties"
```

---

## Group 4: Catalog & Item

**Files:**
- Modify: `scripts/data/LootTableCatalog.cs`
- Modify: `scripts/data/Item.cs`

### Task 4.1: LootTableCatalog — rename factory methods + fix comments + use EnemyTypeId

**Step 1: Apply all changes to `scripts/data/LootTableCatalog.cs`**

Replace the entire file with the updated version:

```csharp
/// <summary>
/// Centralizes default drop tables for all 14 enemy types.
/// Used by BattleManager.EndBattle() via GetByEnemyType() after combat resolves.
/// </summary>
public static class LootTableCatalog
{
    /// <summary>
    /// Looks up a drop table by enemy type string.
    /// The lookup is case-insensitive; input is lowercased before matching.
    /// Returns null if no drop table is defined for the given type.
    /// </summary>
    public static LootTable? GetByEnemyType(string enemyType)
    {
        return enemyType?.ToLower() switch
        {
            EnemyTypeId.Goblin          => CreateGoblinTable(),
            EnemyTypeId.Orc             => CreateOrcTable(),
            EnemyTypeId.SkeletonWarrior => CreateSkeletonWarriorTable(),
            EnemyTypeId.Troll           => CreateTrollTable(),
            EnemyTypeId.Dragon          => CreateDragonTable(),
            EnemyTypeId.ForestSpirit    => CreateForestSpiritTable(),
            EnemyTypeId.CaveSpider      => CreateCaveSpiderTable(),
            EnemyTypeId.DesertScorpion  => CreateDesertScorpionTable(),
            EnemyTypeId.SwampWretch     => CreateSwampWretchTable(),
            EnemyTypeId.MountainWyvern  => CreateMountainWyvernTable(),
            EnemyTypeId.DarkMage        => CreateDarkMageTable(),
            EnemyTypeId.DungeonGuardian => CreateDungeonGuardianTable(),
            EnemyTypeId.DemonLord       => CreateDemonLordTable(),
            EnemyTypeId.Boss            => CreateBossTable(),
            _ => null
        };
    }

    public static LootTable CreateGoblinTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.85f,
        Entries = new()
        {
            new LootEntry { ItemId = "goblin_ear", Weight = 200, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "wooden_sword", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateOrcTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 180, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_sword", Weight = 30, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateSkeletonWarriorTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 180, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "iron_armor", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateTrollTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 120, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_shield", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDragonTable() => new LootTable
    {
        MaxDrops = 3, DropChance = 1.0f,
        Entries = new()
        {
            // GuaranteedDrop = true; Weight = 0 by convention (guaranteed entries are excluded from weighted draws)
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 2, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 50, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 50, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateForestSpiritTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.80f,
        Entries = new()
        {
            new LootEntry { ItemId = "spider_silk", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "goblin_ear", Weight = 80, MinQuantity = 1, MaxQuantity = 2 }
        }
    };

    public static LootTable CreateCaveSpiderTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.85f,
        Entries = new()
        {
            new LootEntry { ItemId = "spider_silk", Weight = 200, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "iron_boots", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDesertScorpionTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.88f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_helmet", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateSwampWretchTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.88f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_armor", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateMountainWyvernTable() => new LootTable
    {
        MaxDrops = 3, DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_sword", Weight = 40, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_boots", Weight = 40, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDarkMageTable() => new LootTable
    {
        MaxDrops = 2, DropChance = 0.95f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 100, MinQuantity = 2, MaxQuantity = 4 },
            new LootEntry { ItemId = "iron_helmet", Weight = 30, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDungeonGuardianTable() => new LootTable
    {
        MaxDrops = 3, DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "iron_sword", Weight = 80, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 80, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_shield", Weight = 60, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDemonLordTable() => new LootTable
    {
        MaxDrops = 3, DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 2, MaxQuantity = 3, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 100, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateBossTable() => new LootTable
    {
        MaxDrops = 5, DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 3, MaxQuantity = 5, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_shield", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_helmet", Weight = 100, MinQuantity = 1, MaxQuantity = 1 }
        }
    };
}
```

Key changes:
- Class summary updated to accurately name the caller (`BattleManager.EndBattle`)
- `GetByEnemyType` doc notes case-insensitive behavior
- All `GoblinDrops()` → `CreateGoblinTable()` etc.
- Switch arms use `EnemyTypeId.*` constants instead of string literals
- Dragon/DemonLord/Boss guaranteed entries have convention comment

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/data/LootTableCatalog.cs
git commit -m "refactor: rename catalog methods to Create*Table(); use EnemyTypeId constants; fix class summary"
```

---

### Task 4.2: Item.cs — add Rarity XML doc explaining intentional public setter

**Step 1: Edit `scripts/data/Item.cs`**

Find the `Rarity` property (around line 60):
```csharp
public ItemRarity Rarity { get; set; } = ItemRarity.Common;
```

Replace with:
```csharp
/// <summary>
/// Item rarity tier. Defaults to Common.
/// Intentionally uses a public setter (unlike Category which uses protected SetCategory())
/// because rarity is set by external catalog factory classes via object initializers.
/// Making it protected would require a refactor cascade through all catalog factories.
/// </summary>
public ItemRarity Rarity { get; set; } = ItemRarity.Common;
```

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add scripts/data/Item.cs
git commit -m "docs: explain why Item.Rarity uses public setter vs protected Category pattern"
```

---

## Group 5: Tests

**Files:**
- Modify: `tests/data/LootDropSystemTest.cs`
- Modify: `tests/game/EnemySpawnTest.cs`

### Task 5.1: Fix obsolete API + update renamed catalog calls + update changed behavior test

**Step 1: Fix `FormatterServices.GetUninitializedObject` (line 402)**

In `LootResult_DroppedItems_SafeAfterDeserialization`, replace:
```csharp
var uninitializedResult = (LootResult)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LootResult));
```
With:
```csharp
var uninitializedResult = (LootResult)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(LootResult));
```

**Step 2: Update the two renamed catalog calls**

- Line 192: `LootTableCatalog.GoblinDrops()` → `LootTableCatalog.CreateGoblinTable()`
- Line 286: `LootTableCatalog.GoblinDrops()` → `LootTableCatalog.CreateGoblinTable()`

**Step 3: Update `LootResult_Add_IgnoresNullItem` to match new throw behavior**

The existing test at line 416 asserts old warn+skip behavior. Replace it:

```csharp
[TestCase]
public void LootResult_Add_ThrowsOnNullItem()
{
    var result = new LootResult();
    AssertThrown(() => result.Add(null!, 1))
        .IsInstanceOf<ArgumentNullException>();
}
```

**Step 4: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 5: Run existing tests to verify nothing broken**

```bash
dotnet test Sirius.sln --settings test.runsettings.local 2>&1 | tail -15
```
Expected: All existing tests pass.

**Step 6: Commit**

```bash
git add tests/data/LootDropSystemTest.cs
git commit -m "test: fix obsolete API; update renamed catalog calls; update null-item test to expect throw"
```

---

### Task 5.2: Add new test cases — Critical #4 catalog integrity + important gaps

**Step 1: Add 7 new test methods to `LootDropSystemTest.cs`**

Append before the closing `}` of the class:

```csharp
[TestCase]
public void LootTableCatalog_AllEntries_ItemIdsExistInCatalog()
{
    // Critical regression guard: a typo in any catalog ItemId silently produces zero drops.
    // This test catches any mismatch between LootTableCatalog and ItemCatalog at build time.
    string[] allTypes = new[]
    {
        EnemyTypeId.Goblin, EnemyTypeId.Orc, EnemyTypeId.Dragon,
        EnemyTypeId.SkeletonWarrior, EnemyTypeId.Troll, EnemyTypeId.DarkMage,
        EnemyTypeId.DemonLord, EnemyTypeId.Boss, EnemyTypeId.ForestSpirit,
        EnemyTypeId.CaveSpider, EnemyTypeId.DesertScorpion, EnemyTypeId.SwampWretch,
        EnemyTypeId.MountainWyvern, EnemyTypeId.DungeonGuardian
    };

    foreach (var type in allTypes)
    {
        var table = LootTableCatalog.GetByEnemyType(type);
        AssertThat(table).IsNotNull();
        foreach (var entry in table!.Entries)
        {
            AssertThat(ItemCatalog.ItemExists(entry.ItemId))
                .OverrideFailureMessage($"ItemId '{entry.ItemId}' in {type} table not found in ItemCatalog")
                .IsTrue();
        }
    }
}

[TestCase]
public void LootManager_AwardLootToCharacter_NullResult_DoesNotThrow()
{
    var player = new Character();
    // Should log an error and return without throwing
    AssertThrown(() => LootManager.AwardLootToCharacter(null!, player)).IsNull();
}

[TestCase]
public void LootManager_AwardLootToCharacter_NullPlayer_DoesNotThrow()
{
    var result = LootResult.Empty;
    AssertThrown(() => LootManager.AwardLootToCharacter(result, null!)).IsNull();
}

[TestCase]
public void LootTableCatalog_GetByEnemyType_IsCaseInsensitive()
{
    AssertThat(LootTableCatalog.GetByEnemyType("GOBLIN")).IsNotNull();
    AssertThat(LootTableCatalog.GetByEnemyType("Dragon")).IsNotNull();
    AssertThat(LootTableCatalog.GetByEnemyType("SKELETON_WARRIOR")).IsNotNull();
}

[TestCase]
public void LootManager_RollLoot_GuaranteedDropsExceedMaxDrops_AllGuaranteedIncluded()
{
    // Documents the contract: guaranteed drops are NOT capped by MaxDrops.
    // MaxDrops applies only to weighted draws.
    var table = new LootTable
    {
        DropChance = 1.0f,
        MaxDrops = 1,
        Entries = new()
        {
            new LootEntry { ItemId = "goblin_ear", GuaranteedDrop = true, Weight = 0, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "orc_tusk",   GuaranteedDrop = true, Weight = 0, MinQuantity = 1, MaxQuantity = 1 }
        }
    };
    var result = LootManager.RollLoot(table, new Random(42));
    // Both guaranteed entries are included even though MaxDrops = 1
    AssertThat(result.DroppedItems.Count).IsEqual(2);
}

[TestCase]
public void LootEntry_NegativeWeight_IsClampedToZero()
{
    var entry = new LootEntry { Weight = -50 };
    AssertThat(entry.Weight).IsEqual(0);
}

[TestCase]
public void LootTable_NegativeMaxDrops_IsClampedToZero()
{
    var table = new LootTable { MaxDrops = -5 };
    AssertThat(table.MaxDrops).IsEqual(0);
}
```

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Run the new tests to verify they pass**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "LootTableCatalog_AllEntries|LootManager_AwardLootToCharacter_Null|IsCaseInsensitive|GuaranteedDropsExceedMaxDrops|NegativeWeight|NegativeMaxDrops" 2>&1 | tail -15
```
Expected: All new tests pass.

**Step 4: Commit**

```bash
git add tests/data/LootDropSystemTest.cs
git commit -m "test: add catalog integrity test, null param guards, case-insensitivity, MaxDrops boundary"
```

---

### Task 5.3: Add EnemySpawnTest — unknown EnemyType fallback documents silent behavior

**Step 1: Add one test to `tests/game/EnemySpawnTest.cs`**

Add before the closing `}` of the test class:

```csharp
[TestCase]
[RequireGodotRuntime]
public void CreateEnemyInstance_UnknownEnemyType_FallsBackToGoblin()
{
    // Documents the silent fallback behavior: an unknown EnemyType string produces a Goblin.
    // After the fix, this also logs a GD.PrintErr (observable in Godot output, not assertable here).
    var spawn = new EnemySpawn();
    spawn.EnemyType = "zombie"; // unknown non-empty type
    // Blueprint = null is the default, so the type-switch path is taken
    var enemy = spawn.CreateEnemyInstance();
    AssertThat(enemy.EnemyType).IsEqual(EnemyTypeId.Goblin);
}
```

**Step 2: Build**

```bash
dotnet build Sirius.sln 2>&1 | tail -5
```
Expected: `Build succeeded.`

**Step 3: Commit**

```bash
git add tests/game/EnemySpawnTest.cs
git commit -m "test: document unknown EnemyType silent goblin fallback (now logs error)"
```

---

## Task 6: Final Verification

Run after all 5 groups are complete and committed.

**Step 1: Build clean**

```bash
dotnet build Sirius.sln 2>&1 | grep -E "error|warning|succeeded|failed"
```
Expected: `Build succeeded.` with 0 errors. Check for any new warnings.

**Step 2: Run full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local 2>&1 | tail -20
```
Expected: All tests pass. Count should be original count + 8 new tests (7 in LootDropSystemTest + 1 in EnemySpawnTest, offset by 1 renamed test).

**Step 3: Verify all 27 issues addressed**

Run a quick sanity check:
- `grep -n "GoblinDrops\|OrcDrops\|SkeletonWarriorDrops" scripts/ tests/ -r` — should return 0 results (all renamed)
- `grep -n '"goblin"\|"orc"\|"dragon"' scripts/data/Enemy.cs scripts/data/EnemyBlueprint.cs scripts/data/LootTableCatalog.cs` — should return 0 results (all replaced with EnemyTypeId constants)
- `grep -n "FormatterServices" tests/` — should return 0 results (obsolete API removed)
- `grep -n "1.0f, 0.0f, 1.0f" scripts/` — should return 0 results (magenta color removed)

**Step 4: Commit final verification note**

```bash
git commit --allow-empty -m "chore: all 27 PR #2 review issues addressed — see docs/plans/2026-02-20-pr2-review-fixes-design.md"
```
