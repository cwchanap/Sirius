# Treasure Box System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persistent, animated, one-time treasure boxes with fixed rewards on Ground Floor and Floor 1.

**Architecture:** Treasure boxes are static floor entities under `GridMap`, parallel to `EnemySpawn` and `NpcSpawn`. Floor JSON and generators author the boxes, the importer/exporter round-trips them, `GridMap` blocks/interacts with them, `Game` opens them through the existing input action with player-facing `Open` text, and `GameManager` persists opened IDs through `SaveData`.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, Python floor generators, existing `tools/tilemap_json_sync.py` import pipeline.

---

## File Structure

- Create `scripts/data/TreasureReward.cs`: reward item DTOs, reward validation, and grant-to-player behavior.
- Create `tests/data/TreasureRewardTest.cs`: unit tests for gold, item, invalid item, invalid quantity, and overflow behavior.
- Modify `scripts/save/SaveData.cs`: add `OpenedTreasureBoxIds`.
- Modify `scripts/game/GameManager.cs`: add opened treasure set and world-interaction state.
- Modify `tests/save/SaveDataTest.cs`: save serialization coverage for opened box IDs.
- Modify `tests/game/GameManagerTest.cs`: collect/restore opened box IDs and world-interaction state.
- Create `scripts/game/TreasureBoxSpawn.cs`: authored scene node, visual state, reward exports, open animation, floor ownership helpers.
- Create `tests/game/TreasureBoxSpawnTest.cs`: node-level tests for IDs, rewards, open state, and visual frame state.
- Modify `scripts/tilemap_json/FloorJsonModel.cs`: add `treasure_boxes` entity model.
- Modify `scripts/tilemap_json/TilemapJsonImporter.cs`: create/update/remove `TreasureBoxSpawn` nodes.
- Modify `scripts/tilemap_json/TilemapJsonExporter.cs`: export `TreasureBoxSpawn` nodes.
- Modify `tests/tilemap_json/FloorJsonModelTest.cs`: parse/serialize treasure JSON.
- Modify `tests/tilemap_json/TilemapJsonImporterTest.cs`: owner, update, stale removal, and centered-position tests.
- Add `tests/tilemap_json/TilemapJsonExporterTest.cs`: export coverage for treasure boxes.
- Modify `scripts/game/GridMap.cs`: treasure cell type, registration, lookup, and open-request signal.
- Modify `scripts/game/PlayerController.cs`: facing direction, facing-change signal, and interact-to-open request after stair priority.
- Modify `tests/game/PlayerControllerTest.cs`: facing direction and treasure request ordering tests.
- Modify `scripts/game/Game.cs`: connect treasure signal, run open flow, award rewards, update `Open` prompt.
- Modify `tests/game/GameTest.cs`: runtime open flow, no duplicate rewards, and prompt text.
- Modify `tools/floor0_maze_generator.py`: authored Ground Floor treasure boxes.
- Modify `tools/floor1_maze_generator.py`: authored Floor 1 treasure boxes.
- Modify `tests/tools/test_floor0_maze_generator.py`: generated treasure placement and rewards.
- Modify `tests/tools/test_floor1_maze_generator.py`: generated treasure placement and rewards.
- Modify `tests/game/FloorGFMazeLayoutTest.cs`: scene-level treasure count, uniqueness, walkability, no overlap, reachability.
- Modify `tests/game/Floor1FMazeLayoutTest.cs`: scene-level treasure count, uniqueness, walkability, no overlap, reachability.
- Create `assets/sprites/objects/treasure_box/sprite_sheet.png`: polished four-frame treasure box sheet.
- Generate Godot import metadata for the sprite by opening/importing through Godot or running the normal project import path.

## Treasure IDs And Rewards

Use these authored boxes unless implementation-time validation proves a coordinate is not walkable. The validation tests below will fail if a coordinate becomes invalid.

Ground Floor:

| ID | Position | Gold | Items |
| --- | --- | ---: | --- |
| `TreasureBox_GF_EntranceCache` | `(15, 50)` | 35 | `health_potion` x1 |
| `TreasureBox_GF_NorthwestCache` | `(30, 8)` | 60 | `mana_potion` x1 |
| `TreasureBox_GF_NorthLoopCache` | `(49, 8)` | 80 | `strength_tonic` x1 |
| `TreasureBox_GF_EastBranchCache` | `(91, 30)` | 110 | `greater_health_potion` x1 |
| `TreasureBox_GF_StairDistrictCache` | `(94, 68)` | 75 | `iron_skin` x1 |
| `TreasureBox_GF_SouthDeepCache` | `(52, 94)` | 0 | `iron_sword` x1 |
| `TreasureBox_GF_SouthwestCache` | `(7, 72)` | 50 | `antidote` x2 |
| `TreasureBox_GF_SoutheastCache` | `(80, 82)` | 0 | `iron_shield` x1 |

Floor 1:

| ID | Position | Gold | Items |
| --- | --- | ---: | --- |
| `TreasureBox_1F_WestDeadEndCache` | `(4, 22)` | 85 | `health_potion` x2 |
| `TreasureBox_1F_WestLoopCache` | `(2, 42)` | 70 | `swiftness_draught` x1 |
| `TreasureBox_1F_NorthConnectorCache` | `(30, 19)` | 0 | `mana_potion` x2 |
| `TreasureBox_1F_EastHallCache` | `(52, 24)` | 120 | `greater_health_potion` x1 |
| `TreasureBox_1F_NorthStairCache` | `(49, 14)` | 0 | `iron_boots` x1 |
| `TreasureBox_1F_EastShortcutCache` | `(58, 46)` | 0 | `steel_longsword` x1 |
| `TreasureBox_1F_SouthGalleryCache` | `(38, 55)` | 130 | `flash_powder` x1 |
| `TreasureBox_1F_SouthHiddenCache` | `(20, 56)` | 0 | `chain_mail` x1 |

---

### Task 1: Reward Model

**Files:**
- Create: `scripts/data/TreasureReward.cs`
- Create: `tests/data/TreasureRewardTest.cs`

- [ ] **Step 1: Write the failing reward tests**

Create `tests/data/TreasureRewardTest.cs`:

```csharp
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TreasureRewardTest : Godot.Node
{
    [TestCase]
    public void GrantTo_AddsGoldAndKnownItems()
    {
        var player = new Character { Name = "Tester", Gold = 10 };
        var reward = new TreasureReward
        {
            Gold = 40,
            Items =
            [
                new TreasureRewardItem("health_potion", 2),
                new TreasureRewardItem("mana_potion", 1)
            ]
        };

        var result = reward.GrantTo(player);

        AssertThat(result.GoldGranted).IsEqual(40);
        AssertThat(player.Gold).IsEqual(50);
        AssertThat(player.GetItemQuantity("health_potion")).IsEqual(2);
        AssertThat(player.GetItemQuantity("mana_potion")).IsEqual(1);
        AssertThat(result.ItemQuantitiesGranted["health_potion"]).IsEqual(2);
        AssertThat(result.ItemQuantitiesGranted["mana_potion"]).IsEqual(1);
    }

    [TestCase]
    public void GrantTo_SkipsUnknownItems()
    {
        var player = new Character { Name = "Tester" };
        var reward = new TreasureReward
        {
            Items = [new TreasureRewardItem("missing_item", 1)]
        };

        var result = reward.GrantTo(player);

        AssertThat(result.ItemQuantitiesGranted.Count).IsEqual(0);
        AssertThat(result.SkippedItemIds.Count).IsEqual(1);
        AssertThat(result.SkippedItemIds[0]).IsEqual("missing_item");
    }

    [TestCase]
    public void GrantTo_SkipsInvalidQuantities()
    {
        var player = new Character { Name = "Tester" };
        var reward = new TreasureReward
        {
            Items = [new TreasureRewardItem("health_potion", 0)]
        };

        var result = reward.GrantTo(player);

        AssertThat(player.GetItemQuantity("health_potion")).IsEqual(0);
        AssertThat(result.SkippedItemIds.Count).IsEqual(1);
        AssertThat(result.SkippedItemIds[0]).IsEqual("health_potion");
    }

    [TestCase]
    public void Validate_ReturnsAuthoredErrors()
    {
        var reward = new TreasureReward
        {
            Gold = -1,
            Items =
            [
                new TreasureRewardItem("", 1),
                new TreasureRewardItem("missing_item", 1),
                new TreasureRewardItem("health_potion", -2)
            ]
        };

        var errors = reward.ValidateAuthoredContent();

        AssertThat(errors.Count).IsEqual(4);
        AssertThat(errors[0]).Contains("Gold");
        AssertThat(errors[1]).Contains("empty");
        AssertThat(errors[2]).Contains("missing_item");
        AssertThat(errors[3]).Contains("health_potion");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureRewardTest"
```

Expected: FAIL because `TreasureReward` and `TreasureRewardItem` do not exist.

- [ ] **Step 3: Implement the reward model**

Create `scripts/data/TreasureReward.cs`:

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public sealed class TreasureRewardItem
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;

    public TreasureRewardItem() { }

    public TreasureRewardItem(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

[System.Serializable]
public sealed class TreasureReward
{
    public int Gold { get; set; }
    public List<TreasureRewardItem> Items { get; set; } = new();

    public bool HasAnyReward => Gold > 0 || Items.Any(item => item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.ItemId));

    public IReadOnlyList<string> ValidateAuthoredContent()
    {
        var errors = new List<string>();

        if (Gold < 0)
        {
            errors.Add($"Gold reward cannot be negative: {Gold}");
        }

        foreach (var item in Items)
        {
            if (item == null)
            {
                errors.Add("Treasure reward item entry cannot be null");
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.ItemId))
            {
                errors.Add("Treasure reward item id cannot be empty");
                continue;
            }

            if (!ItemCatalog.ItemExists(item.ItemId))
            {
                errors.Add($"Treasure reward item id '{item.ItemId}' does not exist in ItemCatalog");
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"Treasure reward item '{item.ItemId}' has invalid quantity {item.Quantity}");
            }
        }

        return errors;
    }

    public TreasureRewardGrantResult GrantTo(Character player)
    {
        var result = new TreasureRewardGrantResult();
        if (player == null)
        {
            result.Errors.Add("Cannot grant treasure reward to null player");
            return result;
        }

        if (Gold > 0)
        {
            player.GainGold(Gold);
            result.GoldGranted = Gold;
        }

        foreach (var rewardItem in Items)
        {
            if (rewardItem == null || string.IsNullOrWhiteSpace(rewardItem.ItemId) || rewardItem.Quantity <= 0)
            {
                if (rewardItem?.ItemId != null)
                {
                    result.SkippedItemIds.Add(rewardItem.ItemId);
                }
                continue;
            }

            var item = ItemCatalog.CreateItemById(rewardItem.ItemId);
            if (item == null)
            {
                GD.PushWarning($"Treasure reward skipped unknown item '{rewardItem.ItemId}'");
                result.SkippedItemIds.Add(rewardItem.ItemId);
                continue;
            }

            player.TryAddItem(item, rewardItem.Quantity, out int addedQuantity);
            if (addedQuantity > 0)
            {
                result.ItemQuantitiesGranted[item.Id] = result.ItemQuantitiesGranted.TryGetValue(item.Id, out int existing)
                    ? existing + addedQuantity
                    : addedQuantity;
            }

            int overflow = rewardItem.Quantity - addedQuantity;
            if (overflow > 0)
            {
                if (RecoveryChest.Instance != null && GodotObject.IsInstanceValid(RecoveryChest.Instance))
                {
                    RecoveryChest.Instance.AddOverflow(item.Id, overflow);
                    result.ItemQuantitiesRecovered[item.Id] = result.ItemQuantitiesRecovered.TryGetValue(item.Id, out int existing)
                        ? existing + overflow
                        : overflow;
                }
                else
                {
                    GD.PushWarning($"Treasure reward overflow for '{item.Id}' could not be recovered because RecoveryChest is unavailable");
                    result.UnrecoveredItemQuantities[item.Id] = result.UnrecoveredItemQuantities.TryGetValue(item.Id, out int existing)
                        ? existing + overflow
                        : overflow;
                }
            }
        }

        return result;
    }
}

public sealed class TreasureRewardGrantResult
{
    public int GoldGranted { get; set; }
    public Dictionary<string, int> ItemQuantitiesGranted { get; } = new();
    public Dictionary<string, int> ItemQuantitiesRecovered { get; } = new();
    public Dictionary<string, int> UnrecoveredItemQuantities { get; } = new();
    public List<string> SkippedItemIds { get; } = new();
    public List<string> Errors { get; } = new();
}
```

- [ ] **Step 4: Run reward tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureRewardTest"
```

Expected: PASS.

- [ ] **Step 5: Commit reward model**

```bash
git add scripts/data/TreasureReward.cs tests/data/TreasureRewardTest.cs
git commit -m "feat: add treasure reward model"
```

---

### Task 2: Save And GameManager State

**Files:**
- Modify: `scripts/save/SaveData.cs`
- Modify: `scripts/game/GameManager.cs`
- Modify: `tests/save/SaveDataTest.cs`
- Modify: `tests/game/GameManagerTest.cs`

- [ ] **Step 1: Write failing save and manager tests**

Append this test to `tests/save/SaveDataTest.cs`:

```csharp
[TestCase]
public void TestSaveData_OpenedTreasureBoxIds_SerializeAndDeserialize()
{
    var saveData = new SaveData
    {
        OpenedTreasureBoxIds = new System.Collections.Generic.List<string>
        {
            "TreasureBox_GF_EntranceCache",
            "TreasureBox_1F_WestDeadEndCache"
        }
    };

    string json = JsonSerializer.Serialize(saveData);
    var deserialized = JsonSerializer.Deserialize<SaveData>(json);

    AssertThat(deserialized).IsNotNull();
    AssertThat(deserialized!.OpenedTreasureBoxIds.Count).IsEqual(2);
    AssertThat(deserialized.OpenedTreasureBoxIds[0]).IsEqual("TreasureBox_GF_EntranceCache");
    AssertThat(deserialized.OpenedTreasureBoxIds[1]).IsEqual("TreasureBox_1F_WestDeadEndCache");
}
```

Append these tests to `tests/game/GameManagerTest.cs`:

```csharp
[TestCase]
public void MarkTreasureBoxOpened_DeduplicatesIds()
{
    _gameManager.MarkTreasureBoxOpened("TreasureBox_GF_EntranceCache");
    _gameManager.MarkTreasureBoxOpened("TreasureBox_GF_EntranceCache");

    AssertThat(_gameManager.IsTreasureBoxOpened("TreasureBox_GF_EntranceCache")).IsTrue();
    AssertThat(_gameManager.OpenedTreasureBoxIds.Count).IsEqual(1);
}

[TestCase]
public void LoadFromSaveData_RestoresOpenedTreasureBoxes()
{
    var saveData = new SaveData
    {
        Version = SaveData.CurrentVersion,
        Character = new CharacterSaveData
        {
            Name = "Hero",
            Level = 1,
            MaxHealth = 100,
            CurrentHealth = 100,
            Attack = 20,
            Defense = 10,
            Speed = 15,
            ExperienceToNext = 110,
            Inventory = new InventorySaveData(),
            Equipment = new EquipmentSaveData()
        },
        OpenedTreasureBoxIds = new System.Collections.Generic.List<string>
        {
            "TreasureBox_GF_EntranceCache",
            "",
            "TreasureBox_GF_EntranceCache",
            "TreasureBox_1F_WestDeadEndCache"
        }
    };

    _gameManager.LoadFromSaveData(saveData);

    AssertThat(_gameManager.OpenedTreasureBoxIds.Count).IsEqual(2);
    AssertThat(_gameManager.IsTreasureBoxOpened("TreasureBox_GF_EntranceCache")).IsTrue();
    AssertThat(_gameManager.IsTreasureBoxOpened("TreasureBox_1F_WestDeadEndCache")).IsTrue();
}

[TestCase]
public void WorldInteractionState_StartEndAndReset()
{
    _gameManager.StartWorldInteraction();

    AssertThat(_gameManager.IsInWorldInteraction).IsTrue();

    _gameManager.EndWorldInteraction();

    AssertThat(_gameManager.IsInWorldInteraction).IsFalse();

    _gameManager.StartWorldInteraction();
    _gameManager.ResetBattleState();

    AssertThat(_gameManager.IsInWorldInteraction).IsFalse();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveDataTest|FullyQualifiedName~GameManagerTest"
```

Expected: FAIL because `OpenedTreasureBoxIds`, `MarkTreasureBoxOpened`, `IsTreasureBoxOpened`, `OpenedTreasureBoxIds`, and `IsInWorldInteraction` do not exist.

- [ ] **Step 3: Add save data property**

Modify `scripts/save/SaveData.cs` by adding this property after `QuestFlags`:

```csharp
public List<string> OpenedTreasureBoxIds { get; set; } = new();
```

- [ ] **Step 4: Add GameManager state and persistence**

Modify `scripts/game/GameManager.cs`.

Add this field near the existing private fields:

```csharp
private readonly HashSet<string> _openedTreasureBoxIds = new(StringComparer.Ordinal);
```

Add this public property near `IsInNpcInteraction`:

```csharp
public bool IsInWorldInteraction { get; private set; } = false;
public IReadOnlyCollection<string> OpenedTreasureBoxIds => _openedTreasureBoxIds;
```

Add these methods near `StartNpcInteraction()` / `EndNpcInteraction()`:

```csharp
public void StartWorldInteraction()
{
    IsInWorldInteraction = true;
    GD.Print("World interaction started.");
}

public void EndWorldInteraction()
{
    IsInWorldInteraction = false;
    GD.Print("World interaction ended.");
}

public bool MarkTreasureBoxOpened(string treasureBoxId)
{
    if (string.IsNullOrWhiteSpace(treasureBoxId))
    {
        GD.PushWarning("Cannot mark treasure box opened with null or empty ID.");
        return false;
    }

    return _openedTreasureBoxIds.Add(treasureBoxId);
}

public bool IsTreasureBoxOpened(string treasureBoxId)
{
    return !string.IsNullOrWhiteSpace(treasureBoxId) && _openedTreasureBoxIds.Contains(treasureBoxId);
}

private void RestoreOpenedTreasureBoxIds(IEnumerable<string>? treasureBoxIds)
{
    _openedTreasureBoxIds.Clear();
    if (treasureBoxIds == null)
    {
        return;
    }

    foreach (string id in treasureBoxIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
    {
        _openedTreasureBoxIds.Add(id);
    }
}
```

In `ResetBattleState()`, add:

```csharp
IsInWorldInteraction = false;
```

In `CollectSaveData()`, add this property assignment:

```csharp
OpenedTreasureBoxIds = _openedTreasureBoxIds
    .OrderBy(id => id, StringComparer.Ordinal)
    .ToList(),
```

In `LoadFromSaveData()`, after quest flags are restored, add:

```csharp
RestoreOpenedTreasureBoxIds(data.OpenedTreasureBoxIds);
```

- [ ] **Step 5: Run tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveDataTest|FullyQualifiedName~GameManagerTest"
```

Expected: PASS.

- [ ] **Step 6: Commit save and state work**

```bash
git add scripts/save/SaveData.cs scripts/game/GameManager.cs tests/save/SaveDataTest.cs tests/game/GameManagerTest.cs
git commit -m "feat: persist opened treasure boxes"
```

---

### Task 3: Treasure Box Spawn Node

**Files:**
- Create: `scripts/game/TreasureBoxSpawn.cs`
- Create: `tests/game/TreasureBoxSpawnTest.cs`

- [ ] **Step 1: Write failing node tests**

Create `tests/game/TreasureBoxSpawnTest.cs`:

```csharp
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TreasureBoxSpawnTest : Node
{
    [TestCase]
    public void BuildReward_UsesExportedGoldAndItems()
    {
        var box = new TreasureBoxSpawn
        {
            TreasureBoxId = "TreasureBox_Test",
            RewardGold = 25,
            RewardItemIds = ["health_potion", "mana_potion"],
            RewardItemQuantities = [2, 1]
        };

        var reward = box.BuildReward();

        AssertThat(reward.Gold).IsEqual(25);
        AssertThat(reward.Items.Count).IsEqual(2);
        AssertThat(reward.Items[0].ItemId).IsEqual("health_potion");
        AssertThat(reward.Items[0].Quantity).IsEqual(2);
        AssertThat(reward.Items[1].ItemId).IsEqual("mana_potion");
        AssertThat(reward.Items[1].Quantity).IsEqual(1);
    }

    [TestCase]
    public void BuildReward_MissingQuantityDefaultsToOne()
    {
        var box = new TreasureBoxSpawn
        {
            TreasureBoxId = "TreasureBox_Test",
            RewardItemIds = ["health_potion"],
            RewardItemQuantities = []
        };

        var reward = box.BuildReward();

        AssertThat(reward.Items.Count).IsEqual(1);
        AssertThat(reward.Items[0].Quantity).IsEqual(1);
    }

    [TestCase]
    public void ApplyOpenedState_SetsOpenedAndFrame()
    {
        var box = new TreasureBoxSpawn();

        box.ApplyOpenedState(true);

        AssertThat(box.IsOpened).IsTrue();
        AssertThat(box.CurrentFrameIndex).IsEqual(3);
    }

    [TestCase]
    public void BelongsToFloor_ReturnsTrueForAncestor()
    {
        var floor = new Node2D { Name = "Floor" };
        var grid = new GridMap { Name = "GridMap" };
        var box = new TreasureBoxSpawn { TreasureBoxId = "TreasureBox_Test" };
        floor.AddChild(grid);
        grid.AddChild(box);

        AssertThat(box.BelongsToFloor(floor)).IsTrue();

        floor.Free();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureBoxSpawnTest"
```

Expected: FAIL because `TreasureBoxSpawn` does not exist.

- [ ] **Step 3: Implement the node**

Create `scripts/game/TreasureBoxSpawn.cs`:

```csharp
using Godot;
using System.Threading.Tasks;

[Tool]
public partial class TreasureBoxSpawn : Sprite2D
{
    private const string SpritePath = "res://assets/sprites/objects/treasure_box/sprite_sheet.png";
    private const int FrameCount = 4;
    private int _frameWidth = 32;
    private int _frameHeight = 32;
    private GridMap? _gridMap;
    private Vector2I? _lastOutOfBoundsEditorGrid;

    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    [Export] public string TreasureBoxId { get; set; } = "";
    [Export] public int RewardGold { get; set; }
    [Export] public Godot.Collections.Array<string> RewardItemIds { get; set; } = new();
    [Export] public Godot.Collections.Array<int> RewardItemQuantities { get; set; } = new();
    [Export] public bool EditorSnapEnabled { get; set; } = false;

    public bool IsOpened { get; private set; }
    public bool IsOpening { get; private set; }
    public int CurrentFrameIndex { get; private set; }

    public override void _Ready()
    {
        if (!IsInGroup("TreasureBoxSpawn"))
        {
            AddToGroup("TreasureBoxSpawn");
        }

        _gridMap = GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap");
        if (_gridMap == null)
        {
            _gridMap = GetTree().Root.FindChild("GridMap", recursive: true, owned: false) as GridMap;
        }

        TryLoadSpriteTexture();
        Centered = true;
        RegionEnabled = Texture != null;
        SetFrame(IsOpened ? FrameCount - 1 : 0);

        int cell = _gridMap != null ? _gridMap.CellSize : 32;
        if (_frameWidth > 0 && _frameHeight > 0)
        {
            Scale = new Vector2(cell / (float)_frameWidth, cell / (float)_frameHeight);
        }

        if (_gridMap != null)
        {
            UpdateVisual(_gridMap);
        }

        ZIndex = 2;
        SetProcess(Engine.IsEditorHint());
    }

    public TreasureReward BuildReward()
    {
        var reward = new TreasureReward { Gold = RewardGold };
        for (int i = 0; i < RewardItemIds.Count; i++)
        {
            string itemId = RewardItemIds[i];
            int quantity = i < RewardItemQuantities.Count ? RewardItemQuantities[i] : 1;
            reward.Items.Add(new TreasureRewardItem(itemId, quantity));
        }

        return reward;
    }

    public TreasureRewardGrantResult GrantRewardTo(Character player)
    {
        return BuildReward().GrantTo(player);
    }

    public void ApplyOpenedState(bool opened)
    {
        IsOpened = opened;
        IsOpening = false;
        SetFrame(opened ? FrameCount - 1 : 0);
    }

    public async Task OpenAsync()
    {
        if (IsOpened || IsOpening)
        {
            return;
        }

        IsOpening = true;
        for (int frame = 1; frame < FrameCount; frame++)
        {
            SetFrame(frame);
            if (IsInsideTree())
            {
                await ToSignal(GetTree().CreateTimer(0.12), Timer.SignalName.Timeout);
            }
        }

        IsOpened = true;
        IsOpening = false;
        SetFrame(FrameCount - 1);
    }

    public void UpdateVisual(GridMap grid)
    {
        if (grid == null)
        {
            return;
        }

        var ground = grid.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        int cell = grid.CellSize;
        Position = new Vector2(GridPosition.X * cell + cell / 2f, GridPosition.Y * cell + cell / 2f) + offset;
    }

    public bool BelongsToFloor(Node? floorRoot)
    {
        return floorRoot != null && (ReferenceEquals(GetParent(), floorRoot) || floorRoot.IsAncestorOf(this));
    }

    public override void _Draw()
    {
        if (Texture != null)
        {
            return;
        }

        var size = new Vector2(_frameWidth, _frameHeight);
        var body = IsOpened ? Colors.Goldenrod.Darkened(0.25f) : Colors.SaddleBrown;
        DrawRect(new Rect2(-size / 2f, size), body);
        DrawRect(new Rect2(-size / 2f, size), Colors.Gold, false, 2.0f);
        if (!IsOpened)
        {
            DrawLine(new Vector2(-size.X / 2f, -2), new Vector2(size.X / 2f, -2), Colors.Gold, 2.0f);
        }
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() || _gridMap == null)
        {
            return;
        }

        var ground = _gridMap.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        int cellSize = _gridMap.CellSize;
        int maxX = Mathf.Max(0, _gridMap.GridWidth - 1);
        int maxY = Mathf.Max(0, _gridMap.GridHeight - 1);

        Vector2 local = Position - offset;
        int rawTx = Mathf.FloorToInt(local.X / cellSize);
        int rawTy = Mathf.FloorToInt(local.Y / cellSize);
        var rawGrid = new Vector2I(rawTx, rawTy);

        if (rawTx < 0 || rawTx > maxX || rawTy < 0 || rawTy > maxY)
        {
            if (_lastOutOfBoundsEditorGrid != rawGrid)
            {
                GD.PrintErr($"TreasureBoxSpawn '{TreasureBoxId}' editor position {rawGrid} is outside grid bounds 0..{maxX},0..{maxY}; clamping to fit.");
                _lastOutOfBoundsEditorGrid = rawGrid;
            }
        }
        else
        {
            _lastOutOfBoundsEditorGrid = null;
        }

        int tx = Mathf.Clamp(rawTx, 0, maxX);
        int ty = Mathf.Clamp(rawTy, 0, maxY);
        var newGrid = new Vector2I(tx, ty);
        if (newGrid != GridPosition)
        {
            GridPosition = newGrid;
        }

        if (EditorSnapEnabled)
        {
            Vector2 snapped = new Vector2(tx * cellSize + cellSize / 2f, ty * cellSize + cellSize / 2f) + offset;
            if (!snapped.IsEqualApprox(Position))
            {
                Position = snapped;
            }
        }
    }

    private void TryLoadSpriteTexture()
    {
        if (!FileAccess.FileExists(SpritePath))
        {
            Texture = null;
            return;
        }

        Texture = GD.Load<Texture2D>(SpritePath);
        if (Texture == null)
        {
            return;
        }

        var size = Texture.GetSize();
        _frameWidth = Mathf.Max(1, Mathf.RoundToInt(size.X) / FrameCount);
        _frameHeight = Mathf.Max(1, Mathf.RoundToInt(size.Y));
    }

    private void SetFrame(int frameIndex)
    {
        CurrentFrameIndex = Mathf.Clamp(frameIndex, 0, FrameCount - 1);
        if (Texture != null)
        {
            RegionEnabled = true;
            RegionRect = new Rect2(CurrentFrameIndex * _frameWidth, 0, _frameWidth, _frameHeight);
        }

        QueueRedraw();
    }
}
```

- [ ] **Step 4: Run node tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureBoxSpawnTest"
```

Expected: PASS.

- [ ] **Step 5: Commit treasure box node**

```bash
git add scripts/game/TreasureBoxSpawn.cs tests/game/TreasureBoxSpawnTest.cs
git commit -m "feat: add treasure box spawn node"
```

---

### Task 4: Floor JSON Import And Export

**Files:**
- Modify: `scripts/tilemap_json/FloorJsonModel.cs`
- Modify: `scripts/tilemap_json/TilemapJsonImporter.cs`
- Modify: `scripts/tilemap_json/TilemapJsonExporter.cs`
- Modify: `tests/tilemap_json/FloorJsonModelTest.cs`
- Modify: `tests/tilemap_json/TilemapJsonImporterTest.cs`
- Create: `tests/tilemap_json/TilemapJsonExporterTest.cs`

- [ ] **Step 1: Write failing JSON model tests**

Append to `tests/tilemap_json/FloorJsonModelTest.cs`:

```csharp
[TestCase]
public void FromJson_ParsesTreasureBoxes()
{
    const string json = """
    {
      "schema_version": "1.0",
      "floor_metadata": {
        "floor_name": "Ground Floor",
        "floor_number": 0,
        "player_start": { "x": 8, "y": 50 }
      },
      "tile_layers": {},
      "entities": {
        "treasure_boxes": [
          {
            "id": "TreasureBox_GF_EntranceCache",
            "position": { "x": 15, "y": 50 },
            "gold": 35,
            "items": [
              { "item_id": "health_potion", "quantity": 1 }
            ]
          }
        ]
      }
    }
    """;

    var model = FloorJsonModel.FromJson(json);

    AssertThat(model).IsNotNull();
    AssertThat(model!.Entities.TreasureBoxes).IsNotNull();
    AssertThat(model.Entities.TreasureBoxes!.Count).IsEqual(1);
    AssertThat(model.Entities.TreasureBoxes[0].Id).IsEqual("TreasureBox_GF_EntranceCache");
    AssertThat(model.Entities.TreasureBoxes[0].Position.ToVector2I()).IsEqual(new Vector2I(15, 50));
    AssertThat(model.Entities.TreasureBoxes[0].Gold).IsEqual(35);
    AssertThat(model.Entities.TreasureBoxes[0].Items.Count).IsEqual(1);
    AssertThat(model.Entities.TreasureBoxes[0].Items[0].ItemId).IsEqual("health_potion");
    AssertThat(model.Entities.TreasureBoxes[0].Items[0].Quantity).IsEqual(1);
}
```

- [ ] **Step 2: Write failing importer tests**

Append to `tests/tilemap_json/TilemapJsonImporterTest.cs`:

```csharp
[TestCase]
public void ImportToScene_AssignsOwnerToCreatedTreasureBoxNodes()
{
    var sceneRoot = new Node2D { Name = "TestFloor" };
    var gridMap = new Node2D { Name = "GridMap" };
    sceneRoot.AddChild(gridMap);
    gridMap.Owner = sceneRoot;

    var model = new FloorJsonModel
    {
        Entities = new SceneEntities
        {
            TreasureBoxes =
            [
                new TreasureBoxData
                {
                    Id = "TreasureBox_Test",
                    Position = new Vector2IData(15, 50),
                    Gold = 35,
                    Items = [new TreasureBoxItemData { ItemId = "health_potion", Quantity = 1 }]
                }
            ]
        }
    };

    var importer = new TilemapJsonImporter();

    var err = importer.ImportToScene(model, gridMap);

    AssertThat(err).IsEqual(Godot.Error.Ok);
    var box = gridMap.GetNode<TreasureBoxSpawn>("TreasureBox_Test");
    AssertThat(box.Owner).IsEqual(sceneRoot);
    AssertThat(box.GridPosition).IsEqual(new Vector2I(15, 50));
    AssertThat(box.RewardGold).IsEqual(35);
    AssertThat(box.RewardItemIds[0]).IsEqual("health_potion");
    AssertThat(box.RewardItemQuantities[0]).IsEqual(1);
    sceneRoot.Free();
}

[TestCase]
public void ImportToScene_RemovesStaleTreasureBoxesSynchronously()
{
    var sceneRoot = new Node2D { Name = "TestFloor" };
    var gridMap = new Node2D { Name = "GridMap" };
    sceneRoot.AddChild(gridMap);
    gridMap.Owner = sceneRoot;

    var staleBox = new TreasureBoxSpawn { Name = "TreasureBox_Stale", TreasureBoxId = "TreasureBox_Stale", GridPosition = new Vector2I(5, 5) };
    gridMap.AddChild(staleBox);
    staleBox.Owner = sceneRoot;

    var model = new FloorJsonModel
    {
        Entities = new SceneEntities
        {
            TreasureBoxes = []
        }
    };

    var importer = new TilemapJsonImporter();
    var err = importer.ImportToScene(model, gridMap);

    AssertThat(err).IsEqual(Godot.Error.Ok);
    AssertThat(gridMap.HasNode("TreasureBox_Stale")).IsFalse();
    sceneRoot.Free();
}
```

- [ ] **Step 3: Write failing exporter test**

Create `tests/tilemap_json/TilemapJsonExporterTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TilemapJsonExporterTest : Node
{
    [TestCase]
    public void ExportScene_IncludesTreasureBoxes()
    {
        var gridMap = new Node2D { Name = "GridMap" };
        var box = new TreasureBoxSpawn
        {
            Name = "TreasureBox_Test",
            TreasureBoxId = "TreasureBox_Test",
            GridPosition = new Vector2I(15, 50),
            RewardGold = 35,
            RewardItemIds = ["health_potion"],
            RewardItemQuantities = [1]
        };
        gridMap.AddChild(box);

        var exporter = new TilemapJsonExporter();

        var model = exporter.ExportScene(gridMap);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.TreasureBoxes).IsNotNull();
        AssertThat(model.Entities.TreasureBoxes!.Count).IsEqual(1);
        AssertThat(model.Entities.TreasureBoxes[0].Id).IsEqual("TreasureBox_Test");
        AssertThat(model.Entities.TreasureBoxes[0].Gold).IsEqual(35);
        AssertThat(model.Entities.TreasureBoxes[0].Items[0].ItemId).IsEqual("health_potion");
        gridMap.Free();
    }
}
```

- [ ] **Step 4: Run JSON tests to verify they fail**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest"
```

Expected: FAIL because treasure JSON data and import/export support do not exist.

- [ ] **Step 5: Add JSON model classes**

Modify `scripts/tilemap_json/FloorJsonModel.cs`.

Add to `SceneEntities` after `NpcSpawns`:

```csharp
[JsonPropertyName("treasure_boxes")]
public List<TreasureBoxData>? TreasureBoxes { get; set; }
```

Add these classes after `NpcSpawnData`:

```csharp
public class TreasureBoxData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public Vector2IData Position { get; set; } = new();

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("items")]
    public List<TreasureBoxItemData> Items { get; set; } = new();
}

public class TreasureBoxItemData
{
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = "";

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; } = 1;
}
```

- [ ] **Step 6: Add importer support**

Modify `scripts/tilemap_json/TilemapJsonImporter.cs`.

In `ImportEntities`, add this block after NPC imports:

```csharp
if (entities.TreasureBoxes != null)
    ImportTreasureBoxes(entities.TreasureBoxes, gridMapNode);
else
    GD.PrintErr("[TilemapJsonImporter] treasure_boxes key missing from JSON — treasure boxes not imported");
```

Add these methods near `ImportNpcSpawns`:

```csharp
private void ImportTreasureBoxes(List<TreasureBoxData> boxes, Node2D gridMapNode)
{
    var existingBoxes = new Dictionary<string, Node>();
    foreach (var child in gridMapNode.GetChildren())
    {
        if (child is TreasureBoxSpawn)
        {
            existingBoxes[child.Name.ToString()] = child;
        }
    }

    var processedIds = new HashSet<string>();

    foreach (var boxData in boxes)
    {
        processedIds.Add(boxData.Id);

        if (existingBoxes.TryGetValue(boxData.Id, out var existingNode))
        {
            UpdateTreasureBoxNode(existingNode, boxData);
        }
        else
        {
            CreateTreasureBoxNode(boxData, gridMapNode);
        }
    }

    foreach (var (id, node) in existingBoxes)
    {
        if (!processedIds.Contains(id))
        {
            GD.Print($"[TilemapJsonImporter] Removing treasure box: {id}");
            node.Free();
        }
    }
}

private void UpdateTreasureBoxNode(Node node, TreasureBoxData data)
{
    ConfigureTreasureBoxNode(node, data);
    GD.Print($"[TilemapJsonImporter] Updated treasure box: {data.Id}");
}

private void CreateTreasureBoxNode(TreasureBoxData data, Node2D parent)
{
    var instance = new TreasureBoxSpawn
    {
        Name = data.Id
    };

    ConfigureTreasureBoxNode(instance, data);
    parent.AddChild(instance);

    var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
    if (sceneRoot != null)
    {
        instance.Owner = sceneRoot;
    }

    GD.Print($"[TilemapJsonImporter] Created treasure box: {data.Id}");
}

private void ConfigureTreasureBoxNode(Node node, TreasureBoxData data)
{
    node.Set("TreasureBoxId", data.Id);
    node.Set("GridPosition", data.Position.ToVector2I());
    node.Set("RewardGold", data.Gold);

    var itemIds = new Godot.Collections.Array<string>();
    var quantities = new Godot.Collections.Array<int>();
    foreach (var item in data.Items ?? new List<TreasureBoxItemData>())
    {
        itemIds.Add(item.ItemId);
        quantities.Add(item.Quantity);
    }

    node.Set("RewardItemIds", itemIds);
    node.Set("RewardItemQuantities", quantities);

    if (node is Node2D node2d)
    {
        node2d.Position = ToCenteredCellPosition(data.Position);
        node2d.ZIndex = 2;
    }
}
```

- [ ] **Step 7: Add exporter support**

Modify `scripts/tilemap_json/TilemapJsonExporter.cs`.

In `ExportEntities`, add:

```csharp
entities.TreasureBoxes = ExportTreasureBoxes(gridMapNode);
```

Add this method after `ExportNpcSpawns`:

```csharp
private List<TreasureBoxData> ExportTreasureBoxes(Node2D gridMapNode)
{
    var boxes = new List<TreasureBoxData>();

    foreach (var child in gridMapNode.GetChildren())
    {
        if (child is not TreasureBoxSpawn box)
        {
            continue;
        }

        var data = new TreasureBoxData
        {
            Id = string.IsNullOrWhiteSpace(box.TreasureBoxId) ? child.Name.ToString() : box.TreasureBoxId,
            Position = new Vector2IData(box.GridPosition),
            Gold = box.RewardGold
        };

        for (int i = 0; i < box.RewardItemIds.Count; i++)
        {
            data.Items.Add(new TreasureBoxItemData
            {
                ItemId = box.RewardItemIds[i],
                Quantity = i < box.RewardItemQuantities.Count ? box.RewardItemQuantities[i] : 1
            });
        }

        boxes.Add(data);
    }

    return boxes;
}
```

- [ ] **Step 8: Run JSON tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest"
```

Expected: PASS.

- [ ] **Step 9: Commit JSON import/export work**

```bash
git add scripts/tilemap_json/FloorJsonModel.cs scripts/tilemap_json/TilemapJsonImporter.cs scripts/tilemap_json/TilemapJsonExporter.cs tests/tilemap_json/FloorJsonModelTest.cs tests/tilemap_json/TilemapJsonImporterTest.cs tests/tilemap_json/TilemapJsonExporterTest.cs
git commit -m "feat: round trip treasure boxes in floor JSON"
```

---

### Task 5: GridMap Registration And Player Input

**Files:**
- Modify: `scripts/game/GridMap.cs`
- Modify: `scripts/game/PlayerController.cs`
- Modify: `tests/game/PlayerControllerTest.cs`

- [ ] **Step 1: Write failing player controller tests**

Append to `tests/game/PlayerControllerTest.cs`:

```csharp
[TestCase]
public void FacingDirection_DefaultsDownAndUpdatesOnMovementInput()
{
    var controller = new PlayerController();
    var gameManager = new GameManager();
    SetPrivateField(controller, "_gameManager", gameManager);

    AssertThat(controller.FacingDirection).IsEqual(Vector2I.Down);

    controller._UnhandledInput(new InputEventKey { Keycode = Key.Left, Pressed = true });

    AssertThat(controller.FacingDirection).IsEqual(Vector2I.Left);

    gameManager.Free();
    controller.Free();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest"
```

Expected: FAIL because `FacingDirection` does not exist.

- [ ] **Step 3: Add GridMap treasure signal and registration**

Modify `scripts/game/GridMap.cs`.

Add this signal after `NpcInteractedEventHandler`:

```csharp
[Signal] public delegate void TreasureBoxOpenRequestedEventHandler(Vector2I treasurePosition);
```

Add `TreasureBox = 5` to the `CellType` enum:

```csharp
TreasureBox = 5
```

In `TryMovePlayer`, after NPC handling and before moving the player, add:

```csharp
if (targetCell == CellType.TreasureBox)
{
    return false;
}
```

In `LoadFloor`, after deferred NPC registration, add:

```csharp
CallDeferred(nameof(RegisterStaticTreasureBoxes));
```

Add these methods near the static spawn registration methods:

```csharp
public bool TryRequestTreasureBoxOpen(Vector2I facingDirection)
{
    if (facingDirection == Vector2I.Zero)
    {
        return false;
    }

    Vector2I target = _playerPosition + facingDirection;
    if (!IsWithinGrid(target) || (CellType)_grid[target.X, target.Y] != CellType.TreasureBox)
    {
        return false;
    }

    EmitSignal(SignalName.TreasureBoxOpenRequested, target);
    return true;
}

public bool IsTreasureBoxAtGridPosition(Vector2I gridPosition)
{
    return IsWithinGrid(gridPosition) && (CellType)_grid[gridPosition.X, gridPosition.Y] == CellType.TreasureBox;
}

public void RegisterStaticTreasureBoxes()
{
    var currentFloorRoot = GetParent();
    if (currentFloorRoot == null)
    {
        GD.PrintErr("GridMap.RegisterStaticTreasureBoxes: GridMap has no floor root parent.");
        return;
    }

    var nodes = GetTree().GetNodesInGroup("TreasureBoxSpawn");
    GD.Print($"GridMap.RegisterStaticTreasureBoxes: Found {nodes.Count} total TreasureBoxSpawn nodes; filtering to floor '{currentFloorRoot.Name}'.");

    foreach (Node n in nodes)
    {
        if (n is not TreasureBoxSpawn box || !box.BelongsToFloor(currentFloorRoot))
        {
            continue;
        }

        Vector2I gp = box.GridPosition;
        Vector2I gg = new Vector2I(gp.X - _tilemapOrigin.X, gp.Y - _tilemapOrigin.Y);

        if (gg.X >= 0 && gg.X < GridWidth && gg.Y >= 0 && gg.Y < GridHeight)
        {
            _grid[gg.X, gg.Y] = (int)CellType.TreasureBox;
            box.UpdateVisual(this);
            box.ApplyOpenedState(GameManager.Instance?.IsTreasureBoxOpened(box.TreasureBoxId) == true);
            GD.Print($"  Treasure box '{box.TreasureBoxId}' registered at grid[{gg.X}, {gg.Y}]");
        }
        else
        {
            GD.PrintErr($"  Treasure box '{box.TreasureBoxId}' out of bounds! Grid size: {GridWidth}x{GridHeight}");
        }
    }
}
```

- [ ] **Step 4: Add facing direction, facing signal, and treasure request to PlayerController**

Modify `scripts/game/PlayerController.cs`.

Add this field near `_isProcessingMove`:

```csharp
private Vector2I _lastFacingDirection = Vector2I.Down;
```

Add this signal near the top of the class:

```csharp
[Signal] public delegate void FacingChangedEventHandler(Vector2I facingDirection);
```

Add this property near `SetGridMap`:

```csharp
public Vector2I FacingDirection => _lastFacingDirection;
```

Update the input block condition:

```csharp
if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
```

In the interact branch after the stair transition block and before `return;`, add:

```csharp
if (_gridMap != null && _gridMap.TryRequestTreasureBoxOpen(_lastFacingDirection))
{
    _awaitingStairInteractRelease = true;
}
```

In the movement key handling, after `direction` is resolved and before `_gridMap == null` is checked, add:

```csharp
if (_lastFacingDirection != direction)
{
    _lastFacingDirection = direction;
    EmitSignal(SignalName.FacingChanged, _lastFacingDirection);
}
```

- [ ] **Step 5: Run focused tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~FloorGFMazeLayoutTest.Game_InteractImmediatelyAfterMovingOntoFloorGFStairLoadsFloor1"
```

Expected: PASS. The stair regression must still pass because stair handling remains first priority.

- [ ] **Step 6: Commit GridMap and input work**

```bash
git add scripts/game/GridMap.cs scripts/game/PlayerController.cs tests/game/PlayerControllerTest.cs
git commit -m "feat: request treasure opening from grid input"
```

---

### Task 6: Runtime Open Flow And `Open` Prompt

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`

- [ ] **Step 1: Write failing runtime tests**

Append to `tests/game/GameTest.cs`:

```csharp
[TestCase]
public async System.Threading.Tasks.Task Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt()
{
    var sceneTree = (SceneTree)Engine.GetMainLoop();
    var packed = GD.Load<PackedScene>("res://scenes/game/Game.tscn");
    AssertThat(packed).IsNotNull();
    var game = packed!.Instantiate<Game>();

    try
    {
        sceneTree.Root.AddChild(game);
        await AwaitFrames(sceneTree, 8);

        var floorManager = game.GetNode<FloorManager>("FloorManager");
        var gridMap = floorManager.CurrentGridMap;
        var playerController = game.GetNode<PlayerController>("PlayerController");
        var gameManager = game.GetNode<GameManager>("GameManager");

        var box = new TreasureBoxSpawn
        {
            Name = "TreasureBox_RuntimeTest",
            TreasureBoxId = "TreasureBox_RuntimeTest",
            GridPosition = new Vector2I(9, 50),
            RewardGold = 25,
            RewardItemIds = ["health_potion"],
            RewardItemQuantities = [1]
        };
        gridMap.AddChild(box);
        box.AddToGroup("TreasureBoxSpawn");

        SetPrivateField(gridMap, "_grid", new int[gridMap.GridWidth, gridMap.GridHeight]);
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
        gridMap.CallDeferred("RegisterStaticTreasureBoxes");
        await AwaitFrames(sceneTree, 2);

        PressMovement(playerController, Vector2I.Right);
        await AwaitFrames(sceneTree, 1);

        var prompt = game.GetNodeOrNull<Label>("UI/GameUI/InteractionPrompt");
        AssertThat(prompt).IsNotNull();
        AssertThat(prompt!.Visible).IsTrue();
        AssertThat(prompt.Text).IsEqual("Open");

        int startingGold = gameManager.Player.Gold;
        PressInteract(playerController);
        await AwaitFrames(sceneTree, 12);

        AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
        AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsGreaterEqual(4);
        AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeTest")).IsTrue();
        AssertThat(box.IsOpened).IsTrue();

        PressInteractRelease(playerController);
        PressInteract(playerController);
        await AwaitFrames(sceneTree, 12);

        AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
    }
    finally
    {
        if (IsInstanceValid(game))
        {
            game.QueueFree();
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
```

If `GameTest.cs` does not already define these helpers, add them once at the bottom of the test class:

```csharp
private static async System.Threading.Tasks.Task AwaitFrames(SceneTree sceneTree, int frames)
{
    for (int i = 0; i < frames; i++)
    {
        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}

private static void PressMovement(PlayerController playerController, Vector2I direction)
{
    playerController._UnhandledInput(new InputEventKey
    {
        Keycode = DirectionToKey(direction),
        Pressed = true
    });
}

private static void PressInteract(PlayerController playerController)
{
    playerController._UnhandledInput(new InputEventAction
    {
        Action = "interact",
        Pressed = true
    });
}

private static void PressInteractRelease(PlayerController playerController)
{
    playerController._UnhandledInput(new InputEventAction
    {
        Action = "interact",
        Pressed = false
    });
}

private static Key DirectionToKey(Vector2I direction)
{
    if (direction == Vector2I.Right) return Key.Right;
    if (direction == Vector2I.Left) return Key.Left;
    if (direction == Vector2I.Up) return Key.Up;
    if (direction == Vector2I.Down) return Key.Down;
    throw new System.ArgumentException($"Unsupported movement direction {direction}");
}

private static void SetPrivateField(object instance, string fieldName, object? value)
{
    var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (field == null)
    {
        throw new System.MissingFieldException(instance.GetType().FullName, fieldName);
    }

    field.SetValue(instance, value);
}
```

- [ ] **Step 2: Run runtime test to verify it fails**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt"
```

Expected: FAIL because `Game` does not connect treasure requests, open boxes, or create the prompt label.

- [ ] **Step 3: Add prompt and signal wiring to Game**

Modify `scripts/game/Game.cs`.

Add this field near `_activeErrorPopup`:

```csharp
private Label? _interactionPromptLabel;
```

In `_Ready()`, after HUD labels are resolved, call:

```csharp
EnsureInteractionPromptLabel();
```

In `_Ready()`, after `_playerController` is assigned, connect:

```csharp
_playerController.FacingChanged += OnPlayerFacingChanged;
```

In `OnFloorLoaded`, disconnect old treasure signal:

```csharp
_gridMap.TreasureBoxOpenRequested -= OnTreasureBoxOpenRequested;
```

In `OnFloorLoaded`, connect new treasure signal:

```csharp
_gridMap.TreasureBoxOpenRequested += OnTreasureBoxOpenRequested;
```

In `_ExitTree`, disconnect:

```csharp
_gridMap.TreasureBoxOpenRequested -= OnTreasureBoxOpenRequested;
```

Also in `_ExitTree`, disconnect the player-controller facing signal:

```csharp
if (_playerController != null)
{
    _playerController.FacingChanged -= OnPlayerFacingChanged;
}
```

In `OnPlayerMoved`, after updating the player display, add:

```csharp
UpdateInteractionPrompt();
```

In `OnFloorLoaded`, after `CallDeferred(nameof(SetInitialCameraPosition));`, add:

```csharp
CallDeferred(nameof(UpdateInteractionPrompt));
```

Add these methods near `OnNpcInteracted`:

```csharp
private void OnPlayerFacingChanged(Vector2I facingDirection)
{
    UpdateInteractionPrompt();
}

private async void OnTreasureBoxOpenRequested(Vector2I treasurePosition)
{
    if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
    {
        return;
    }

    var box = FindTreasureBoxAt(treasurePosition);
    if (box == null)
    {
        GD.PushWarning($"[Game] Treasure box requested at {treasurePosition} but no TreasureBoxSpawn was found.");
        return;
    }

    if (_gameManager.IsTreasureBoxOpened(box.TreasureBoxId) || box.IsOpened)
    {
        box.ApplyOpenedState(true);
        UpdateInteractionPrompt();
        return;
    }

    _gameManager.StartWorldInteraction();
    try
    {
        await box.OpenAsync();
        box.GrantRewardTo(_gameManager.Player);
        _gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
        _gameManager.NotifyPlayerStatsChanged();
    }
    finally
    {
        _gameManager.EndWorldInteraction();
        UpdateInteractionPrompt();
    }
}

private TreasureBoxSpawn? FindTreasureBoxAt(Vector2I internalGridPosition)
{
    if (_gridMap == null)
    {
        return null;
    }

    Vector2I tilemapPos = _gridMap.InternalGridToTilemapCoords(internalGridPosition);
    Node? currentFloorRoot = _gridMap.GetParent();

    foreach (Node n in GetTree().GetNodesInGroup("TreasureBoxSpawn"))
    {
        if (n is TreasureBoxSpawn box &&
            box.BelongsToFloor(currentFloorRoot) &&
            box.GridPosition == tilemapPos)
        {
            return box;
        }
    }

    return null;
}

private void EnsureInteractionPromptLabel()
{
    if (_interactionPromptLabel != null && IsInstanceValid(_interactionPromptLabel))
    {
        return;
    }

    var gameUi = GetNodeOrNull<Control>("UI/GameUI");
    if (gameUi == null)
    {
        return;
    }

    _interactionPromptLabel = gameUi.GetNodeOrNull<Label>("InteractionPrompt");
    if (_interactionPromptLabel == null)
    {
        _interactionPromptLabel = new Label
        {
            Name = "InteractionPrompt",
            Text = "Open",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ZIndex = 20
        };
        _interactionPromptLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _interactionPromptLabel.OffsetLeft = 0;
        _interactionPromptLabel.OffsetRight = 0;
        _interactionPromptLabel.OffsetTop = -96;
        _interactionPromptLabel.OffsetBottom = -56;
        gameUi.AddChild(_interactionPromptLabel);
    }
}

private void UpdateInteractionPrompt()
{
    EnsureInteractionPromptLabel();
    if (_interactionPromptLabel == null || _gridMap == null || _playerController == null || _gameManager == null)
    {
        return;
    }

    Vector2I target = _gridMap.GetPlayerPosition() + _playerController.FacingDirection;
    var box = FindTreasureBoxAt(target);
    bool canOpen = box != null &&
                   !box.IsOpened &&
                   !box.IsOpening &&
                   !_gameManager.IsTreasureBoxOpened(box.TreasureBoxId);

    _interactionPromptLabel.Text = "Open";
    _interactionPromptLabel.Visible = canOpen;
}
```

- [ ] **Step 4: Block save/load during world interaction**

In `ShowSaveMenu()`, `OnSaveSlotSelected()`, and `ShowLoadMenu()`, add world-interaction checks next to NPC interaction checks:

```csharp
if (_gameManager.IsInWorldInteraction)
{
    GD.PrintErr("Save/load blocked: world interaction in progress.");
    ShowSaveError("Cannot save or load while opening treasure.");
    return;
}
```

Use `"Save Failed"` for save paths and `"Load Failed"` for load paths.

- [ ] **Step 5: Run runtime tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt|FullyQualifiedName~PlayerControllerTest"
```

Expected: PASS.

- [ ] **Step 6: Commit runtime open flow**

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "feat: open treasure boxes from gameplay"
```

---

### Task 7: Generated Floor Content

**Files:**
- Modify: `tools/floor0_maze_generator.py`
- Modify: `tools/floor1_maze_generator.py`
- Modify: `tests/tools/test_floor0_maze_generator.py`
- Modify: `tests/tools/test_floor1_maze_generator.py`
- Modify: `tests/game/FloorGFMazeLayoutTest.cs`
- Modify: `tests/game/Floor1FMazeLayoutTest.cs`

- [ ] **Step 1: Write failing Python generator tests**

In `tests/tools/test_floor0_maze_generator.py`, add expected treasure constants and a test:

```python
EXPECTED_GF_TREASURE = {
    "TreasureBox_GF_EntranceCache": ((15, 50), 35, {"health_potion": 1}),
    "TreasureBox_GF_NorthwestCache": ((30, 8), 60, {"mana_potion": 1}),
    "TreasureBox_GF_NorthLoopCache": ((49, 8), 80, {"strength_tonic": 1}),
    "TreasureBox_GF_EastBranchCache": ((91, 30), 110, {"greater_health_potion": 1}),
    "TreasureBox_GF_StairDistrictCache": ((94, 68), 75, {"iron_skin": 1}),
    "TreasureBox_GF_SouthDeepCache": ((52, 94), 0, {"iron_sword": 1}),
    "TreasureBox_GF_SouthwestCache": ((7, 72), 50, {"antidote": 2}),
    "TreasureBox_GF_SoutheastCache": ((80, 82), 0, {"iron_shield": 1}),
}

def test_ground_floor_treasure_boxes_are_authored_and_walkable(self):
    entities = self.model["entities"]
    boxes = {box["id"]: box for box in entities["treasure_boxes"]}
    walkable = walkable_set(self.model)

    self.assertEqual(set(boxes), set(EXPECTED_GF_TREASURE))

    occupied = {
        (npc["position"]["x"], npc["position"]["y"])
        for npc in entities["npc_spawns"]
    } | {
        (enemy["position"]["x"], enemy["position"]["y"])
        for enemy in entities["enemy_spawns"]
    } | {
        (stair["position"]["x"], stair["position"]["y"])
        for stair in entities["stair_connections"]
    }

    for box_id, (expected_pos, expected_gold, expected_items) in EXPECTED_GF_TREASURE.items():
        box = boxes[box_id]
        position = (box["position"]["x"], box["position"]["y"])
        self.assertEqual(position, expected_pos)
        self.assertIn(position, walkable)
        self.assertNotIn(position, occupied)
        self.assertEqual(box["gold"], expected_gold)
        self.assertEqual(
            {item["item_id"]: item["quantity"] for item in box["items"]},
            expected_items,
        )
```

In `tests/tools/test_floor1_maze_generator.py`, add:

```python
EXPECTED_FLOOR1_TREASURE = {
    "TreasureBox_1F_WestDeadEndCache": ((4, 22), 85, {"health_potion": 2}),
    "TreasureBox_1F_WestLoopCache": ((2, 42), 70, {"swiftness_draught": 1}),
    "TreasureBox_1F_NorthConnectorCache": ((30, 19), 0, {"mana_potion": 2}),
    "TreasureBox_1F_EastHallCache": ((52, 24), 120, {"greater_health_potion": 1}),
    "TreasureBox_1F_NorthStairCache": ((49, 14), 0, {"iron_boots": 1}),
    "TreasureBox_1F_EastShortcutCache": ((58, 46), 0, {"steel_longsword": 1}),
    "TreasureBox_1F_SouthGalleryCache": ((38, 55), 130, {"flash_powder": 1}),
    "TreasureBox_1F_SouthHiddenCache": ((20, 56), 0, {"chain_mail": 1}),
}

def test_floor1_treasure_boxes_are_authored_and_walkable(self):
    entities = self.model["entities"]
    boxes = {box["id"]: box for box in entities["treasure_boxes"]}
    walkable = walkable_set(self.model)

    self.assertEqual(set(boxes), set(EXPECTED_FLOOR1_TREASURE))

    occupied = {
        (enemy["position"]["x"], enemy["position"]["y"])
        for enemy in entities["enemy_spawns"]
    } | {
        (stair["position"]["x"], stair["position"]["y"])
        for stair in entities["stair_connections"]
    }

    for box_id, (expected_pos, expected_gold, expected_items) in EXPECTED_FLOOR1_TREASURE.items():
        box = boxes[box_id]
        position = (box["position"]["x"], box["position"]["y"])
        self.assertEqual(position, expected_pos)
        self.assertIn(position, walkable)
        self.assertNotIn(position, occupied)
        self.assertEqual(box["gold"], expected_gold)
        self.assertEqual(
            {item["item_id"]: item["quantity"] for item in box["items"]},
            expected_items,
        )
```

- [ ] **Step 2: Run Python tests to verify they fail**

Run:

```bash
python3 -m unittest tests.tools.test_floor0_maze_generator tests.tools.test_floor1_maze_generator -v
```

Expected: FAIL because generators do not emit `treasure_boxes`.

- [ ] **Step 3: Add treasure helpers and Ground Floor data**

Modify `tools/floor0_maze_generator.py`.

Add this constant after `STAIR_POS`:

```python
TREASURE_BOXES = {
    "TreasureBox_GF_EntranceCache": {"position": (15, 50), "gold": 35, "items": {"health_potion": 1}},
    "TreasureBox_GF_NorthwestCache": {"position": (30, 8), "gold": 60, "items": {"mana_potion": 1}},
    "TreasureBox_GF_NorthLoopCache": {"position": (49, 8), "gold": 80, "items": {"strength_tonic": 1}},
    "TreasureBox_GF_EastBranchCache": {"position": (91, 30), "gold": 110, "items": {"greater_health_potion": 1}},
    "TreasureBox_GF_StairDistrictCache": {"position": (94, 68), "gold": 75, "items": {"iron_skin": 1}},
    "TreasureBox_GF_SouthDeepCache": {"position": (52, 94), "gold": 0, "items": {"iron_sword": 1}},
    "TreasureBox_GF_SouthwestCache": {"position": (7, 72), "gold": 50, "items": {"antidote": 2}},
    "TreasureBox_GF_SoutheastCache": {"position": (80, 82), "gold": 0, "items": {"iron_shield": 1}},
}
```

Add this helper near `vector()`:

```python
def treasure_box_entities(boxes: dict[str, dict]) -> list[dict]:
    return [
        {
            "id": box_id,
            "position": vector(*data["position"]),
            "gold": data["gold"],
            "items": [
                {"item_id": item_id, "quantity": quantity}
                for item_id, quantity in data["items"].items()
            ],
        }
        for box_id, data in boxes.items()
    ]
```

In the model `entities` block, add:

```python
"treasure_boxes": treasure_box_entities(TREASURE_BOXES),
```

In `validate_model()`, add treasure boxes to goals:

```python
for box in model["entities"]["treasure_boxes"]:
    goals.append((box["position"]["x"], box["position"]["y"]))
```

In `main()` print output, include:

```python
f", {len(model['entities']['treasure_boxes'])} treasure boxes"
```

- [ ] **Step 4: Add Floor 1 data**

Modify `tools/floor1_maze_generator.py`.

Add this constant after `FLOOR1_EXTRA_ENEMY_PATROLS`:

```python
FLOOR1_TREASURE_BOXES = {
    "TreasureBox_1F_WestDeadEndCache": {"position": (4, 22), "gold": 85, "items": {"health_potion": 2}},
    "TreasureBox_1F_WestLoopCache": {"position": (2, 42), "gold": 70, "items": {"swiftness_draught": 1}},
    "TreasureBox_1F_NorthConnectorCache": {"position": (30, 19), "gold": 0, "items": {"mana_potion": 2}},
    "TreasureBox_1F_EastHallCache": {"position": (52, 24), "gold": 120, "items": {"greater_health_potion": 1}},
    "TreasureBox_1F_NorthStairCache": {"position": (49, 14), "gold": 0, "items": {"iron_boots": 1}},
    "TreasureBox_1F_EastShortcutCache": {"position": (58, 46), "gold": 0, "items": {"steel_longsword": 1}},
    "TreasureBox_1F_SouthGalleryCache": {"position": (38, 55), "gold": 130, "items": {"flash_powder": 1}},
    "TreasureBox_1F_SouthHiddenCache": {"position": (20, 56), "gold": 0, "items": {"chain_mail": 1}},
}
```

Add this helper near `vector()`:

```python
def treasure_box_entities(boxes: dict[str, dict]) -> list[dict]:
    return [
        {
            "id": box_id,
            "position": vector(*data["position"]),
            "gold": data["gold"],
            "items": [
                {"item_id": item_id, "quantity": quantity}
                for item_id, quantity in data["items"].items()
            ],
        }
        for box_id, data in boxes.items()
    ]
```

In the Floor 1 `entities` block, add:

```python
"treasure_boxes": treasure_box_entities(FLOOR1_TREASURE_BOXES),
```

In the Floor 2 `entities` block, add:

```python
"treasure_boxes": [],
```

In validation, include treasure boxes in goals and entity validation:

```python
for box in model["entities"].get("treasure_boxes", []):
    goals.append((box["position"]["x"], box["position"]["y"]))
```

In `main()` print output, include:

```python
f", {len(floor1['entities']['treasure_boxes'])} floor1 treasure boxes"
```

- [ ] **Step 5: Run Python generator tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor0_maze_generator tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 6: Regenerate JSON and import scenes**

Run:

```bash
python3 tools/floor0_maze_generator.py
python3 tools/floor1_maze_generator.py
python3 tools/tilemap_json_sync.py import scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn
```

Expected:

- Floor generators report treasure box counts.
- `tilemap_json_sync.py import` exits with code 0 for both floors.
- `scenes/game/floors/FloorGF.tscn` contains `TreasureBox_GF_EntranceCache`.
- `scenes/game/floors/Floor1F.tscn` contains `TreasureBox_1F_WestDeadEndCache`.

- [ ] **Step 7: Add scene-level layout assertions**

In `tests/game/FloorGFMazeLayoutTest.cs`, extend entity walkability checks:

```csharp
else if (child is TreasureBoxSpawn treasureBox)
{
    AssertThat(IsWalkable(treasureBox.GridPosition, walls)).IsTrue();
}
```

Add a new test:

```csharp
[TestCase]
public void FloorGF_GeneratedMaze_HasExpectedTreasureBoxes()
{
    var floorRoot = LoadFloor();
    try
    {
        var gridMap = floorRoot.GetNode<GridMap>("GridMap");
        var walls = GetWalls(gridMap);
        var boxes = gridMap.GetChildren().OfType<TreasureBoxSpawn>().ToDictionary(box => box.TreasureBoxId);

        AssertThat(boxes.Count).IsEqual(8);
        AssertThat(boxes["TreasureBox_GF_EntranceCache"].GridPosition).IsEqual(new Vector2I(15, 50));
        AssertThat(boxes["TreasureBox_GF_SoutheastCache"].RewardItemIds[0]).IsEqual("iron_shield");

        var occupied = gridMap.GetChildren().OfType<EnemySpawn>().Select(enemy => enemy.GridPosition)
            .Concat(gridMap.GetChildren().OfType<NpcSpawn>().Select(npc => npc.GridPosition))
            .Concat(gridMap.GetChildren().OfType<StairConnection>().Select(stair => stair.GridPosition))
            .ToHashSet();

        foreach (var box in boxes.Values)
        {
            AssertThat(IsWalkable(box.GridPosition, walls)).IsTrue();
            AssertThat(occupied.Contains(box.GridPosition)).IsFalse();
            AssertThat(HasPath(PlayerStart, box.GridPosition, walls)).IsTrue();
            AssertThat(ItemCatalog.ItemExists(box.RewardItemIds[0])).IsTrue();
        }
    }
    finally
    {
        floorRoot.Free();
    }
}
```

In `tests/game/Floor1FMazeLayoutTest.cs`, add an equivalent test:

```csharp
[TestCase]
public void Floor1F_GeneratedMaze_HasExpectedTreasureBoxes()
{
    var floorRoot = LoadFloor();
    try
    {
        var gridMap = floorRoot.GetNode<GridMap>("GridMap");
        var walls = GetWalls(gridMap);
        var boxes = gridMap.GetChildren().OfType<TreasureBoxSpawn>().ToDictionary(box => box.TreasureBoxId);

        AssertThat(boxes.Count).IsEqual(8);
        AssertThat(boxes["TreasureBox_1F_WestDeadEndCache"].GridPosition).IsEqual(new Vector2I(4, 22));
        AssertThat(boxes["TreasureBox_1F_EastShortcutCache"].RewardItemIds[0]).IsEqual("steel_longsword");

        var occupied = gridMap.GetChildren().OfType<EnemySpawn>().Select(enemy => enemy.GridPosition)
            .Concat(gridMap.GetChildren().OfType<StairConnection>().Select(stair => stair.GridPosition))
            .ToHashSet();

        foreach (var box in boxes.Values)
        {
            AssertThat(IsWalkable(box.GridPosition, walls)).IsTrue();
            AssertThat(occupied.Contains(box.GridPosition)).IsFalse();
            AssertThat(HasPath(PlayerStart, box.GridPosition, walls)).IsTrue();
            AssertThat(ItemCatalog.ItemExists(box.RewardItemIds[0])).IsTrue();
        }
    }
    finally
    {
        floorRoot.Free();
    }
}
```

- [ ] **Step 8: Run scene layout tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGFMazeLayoutTest|FullyQualifiedName~Floor1FMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 9: Commit generated floor content**

```bash
git add tools/floor0_maze_generator.py tools/floor1_maze_generator.py tests/tools/test_floor0_maze_generator.py tests/tools/test_floor1_maze_generator.py tests/game/FloorGFMazeLayoutTest.cs tests/game/Floor1FMazeLayoutTest.cs scenes/game/floors/FloorGF.json scenes/game/floors/Floor1F.json scenes/game/floors/Floor2F.json scenes/game/floors/FloorGF.tscn scenes/game/floors/Floor1F.tscn resources/floors/FloorGF.tres resources/floors/Floor1F.tres resources/floors/Floor2F.tres
git commit -m "feat: place treasure boxes on early floors"
```

---

### Task 8: Treasure Box Art Asset

**Files:**
- Create: `assets/sprites/objects/treasure_box/sprite_sheet.png`
- Create: `assets/sprites/objects/treasure_box/sprite_sheet.png.import`

- [ ] **Step 1: Generate the sprite sheet**

Use the image generation workflow with this prompt:

```text
Create a polished pixel-art treasure chest sprite sheet for a top-down 2D tactical RPG. Four horizontal frames on a transparent background, each frame exactly square: frame 1 closed wooden treasure chest with gold trim, frame 2 lid opening slightly, frame 3 lid mostly open with warm gold glow, frame 4 fully open chest with visible treasure. Clean readable silhouette at 32x32 game scale, no text, no shadows outside the frame, consistent camera angle, fantasy dungeon style.
```

Save the final transparent PNG to:

```text
assets/sprites/objects/treasure_box/sprite_sheet.png
```

The final image must be either `128x32` or a higher-resolution 4:1 sheet that Godot can scale cleanly.

- [ ] **Step 2: Import the asset**

Run a Godot import through the project path:

```bash
godot --headless --path . --quit
```

Expected:

- `assets/sprites/objects/treasure_box/sprite_sheet.png.import` exists.
- The import command exits 0.

- [ ] **Step 3: Verify the asset is discoverable by runtime code**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureBoxSpawnTest"
```

Expected: PASS. `TreasureBoxSpawn` still passes with the real sprite present.

- [ ] **Step 4: Commit treasure box art**

```bash
git add assets/sprites/objects/treasure_box/sprite_sheet.png assets/sprites/objects/treasure_box/sprite_sheet.png.import
git commit -m "feat: add treasure box sprite sheet"
```

---

### Task 9: Full Verification

**Files:**
- Read-only verification of all touched files.

- [ ] **Step 1: Run Python floor generator tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor0_maze_generator tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 2: Run focused C# tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureRewardTest|FullyQualifiedName~TreasureBoxSpawnTest|FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest|FullyQualifiedName~GameManagerTest|FullyQualifiedName~SaveDataTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~FloorGFMazeLayoutTest|FullyQualifiedName~Floor1FMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 3: Run full build**

Run:

```bash
dotnet build Sirius.sln
```

Expected: PASS with 0 errors.

- [ ] **Step 4: Inspect git status**

Run:

```bash
git status --short
```

Expected: only intentional uncommitted files remain. Commit any missed intentional changes with a concise message.
