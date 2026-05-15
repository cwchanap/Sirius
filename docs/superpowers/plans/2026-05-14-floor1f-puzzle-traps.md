# Floor1F Puzzle Traps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first optional Floor1F puzzle-trap chamber with visible traps, a switch, a riddle, a gate, a treasure reward, a shortcut payoff, and solved-state persistence.

**Architecture:** Puzzle traps are authored static floor entities under `GridMap`, parallel to `EnemySpawn`, `NpcSpawn`, and `TreasureBoxSpawn`. Floor JSON and `tools/floor1_maze_generator.py` author the puzzle nodes, importer/exporter round-trip them, `GridMap` registers trap/gate/interactable cells, `Game` handles switch/riddle/trap flows, and `GameManager` persists only solved puzzle IDs through `SaveData`.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, Python floor generator tests, existing `tools/tilemap_json_sync.py` import pipeline.

---

## Design Reference

- Spec: `docs/superpowers/specs/2026-05-14-floor1f-puzzle-traps-design.md`
- Existing pattern to copy: treasure boxes in `scripts/game/TreasureBoxSpawn.cs`, `scripts/tilemap_json/FloorJsonModel.cs`, `scripts/tilemap_json/TilemapJsonImporter.cs`, `scripts/tilemap_json/TilemapJsonExporter.cs`, `scripts/game/GridMap.cs`, and `scripts/game/Game.cs`.

## Target Floor Content

Use the current south optional Floor1F branch because it already has a compact chamber, a reward pocket, and a route into the south shortcut. This replaces the old `hidden_room_south` placeholder at `(19, 54)` with the first real puzzle room.

Puzzle group:

| Field | Value |
| --- | --- |
| Puzzle ID | `Puzzle_1F_SouthShortcutTrial` |
| Switch | `PuzzleSwitch_1F_SouthTrial_Lever` at `(16, 52)` |
| Riddle | `PuzzleRiddle_1F_SouthTrial_Seal` at `(22, 54)` |
| Gate | `PuzzleGate_1F_SouthTrial_Shortcut` at `(23, 56)` |
| Trap tiles | `(18, 53)`, `(17, 54)`, `(20, 54)`, `(21, 55)` |
| Reward treasure | Move `TreasureBox_1F_SouthHiddenCache` from `(20, 56)` to `(24, 56)` so it is behind the gate |
| Penalty | 12 HP damage per trap trigger or wrong riddle answer; status fields are supported by the data model but left blank for this first authored room |
| Riddle prompt | "Four stones face the old shortcut. Which stone sleeps until the lever wakes it?" |
| Correct choice ID | `east_stone` |

The gate at `(23, 56)` must not block required routes to `1F_2F_A` or `1F_2F_B`. It only blocks the south shortcut/reward branch.

---

### Task 1: Persist Solved Puzzle IDs

**Files:**
- Modify: `scripts/save/SaveData.cs`
- Modify: `scripts/game/GameManager.cs`
- Modify: `tests/save/SaveDataTest.cs`
- Modify: `tests/game/GameManagerTest.cs`

- [ ] **Step 1: Add failing save DTO tests**

Add a test next to `TestSaveData_OpenedTreasureBoxIds_SerializeAndDeserialize` in `tests/save/SaveDataTest.cs`:

```csharp
[TestCase]
public void TestSaveData_SolvedPuzzleIds_SerializeAndDeserialize()
{
    var saveData = new SaveData
    {
        SolvedPuzzleIds = new System.Collections.Generic.List<string>
        {
            "Puzzle_1F_SouthShortcutTrial",
            "Puzzle_1F_Other"
        }
    };

    string json = JsonSerializer.Serialize(saveData);
    var deserialized = JsonSerializer.Deserialize<SaveData>(json);

    AssertThat(deserialized).IsNotNull();
    AssertThat(deserialized!.SolvedPuzzleIds.Count).IsEqual(2);
    AssertThat(deserialized.SolvedPuzzleIds[0]).IsEqual("Puzzle_1F_SouthShortcutTrial");
    AssertThat(deserialized.SolvedPuzzleIds[1]).IsEqual("Puzzle_1F_Other");
}
```

- [ ] **Step 2: Add failing GameManager solved-state tests**

Add tests near the treasure-box ID tests in `tests/game/GameManagerTest.cs`:

```csharp
[TestCase]
public void MarkPuzzleSolved_DeduplicatesIds()
{
    _gameManager.MarkPuzzleSolved("Puzzle_1F_SouthShortcutTrial");
    _gameManager.MarkPuzzleSolved("Puzzle_1F_SouthShortcutTrial");

    AssertThat(_gameManager.IsPuzzleSolved("Puzzle_1F_SouthShortcutTrial")).IsTrue();
    AssertThat(_gameManager.SolvedPuzzleIds.Count).IsEqual(1);
}

[TestCase]
public void MarkPuzzleSolved_RejectsEmptyId()
{
    int countBefore = _gameManager.SolvedPuzzleIds.Count;

    AssertThat(_gameManager.MarkPuzzleSolved("")).IsFalse();
    AssertThat(_gameManager.MarkPuzzleSolved("   ")).IsFalse();

    AssertThat(_gameManager.SolvedPuzzleIds.Count).IsEqual(countBefore);
}

[TestCase]
public void LoadFromSaveData_RestoresSolvedPuzzleIds()
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
        SolvedPuzzleIds = new System.Collections.Generic.List<string>
        {
            "Puzzle_1F_SouthShortcutTrial",
            "",
            "Puzzle_1F_SouthShortcutTrial",
            "Puzzle_1F_Other"
        }
    };

    _gameManager.LoadFromSaveData(saveData);

    AssertThat(_gameManager.SolvedPuzzleIds.Count).IsEqual(2);
    AssertThat(_gameManager.IsPuzzleSolved("Puzzle_1F_SouthShortcutTrial")).IsTrue();
    AssertThat(_gameManager.IsPuzzleSolved("Puzzle_1F_Other")).IsTrue();
}
```

- [ ] **Step 3: Run failing persistence tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveDataTest|FullyQualifiedName~GameManagerTest"
```

Expected: FAIL because `SolvedPuzzleIds`, `MarkPuzzleSolved`, `IsPuzzleSolved`, and `SolvedPuzzleIds` do not exist.

- [ ] **Step 4: Implement `SaveData.SolvedPuzzleIds`**

In `scripts/save/SaveData.cs`, add:

```csharp
public List<string> SolvedPuzzleIds { get; set; } = new();
```

Place it near `OpenedTreasureBoxIds`.

- [ ] **Step 5: Implement GameManager solved puzzle state**

In `scripts/game/GameManager.cs`:

- Add a private set:

```csharp
private readonly HashSet<string> _solvedPuzzleIds = new(StringComparer.Ordinal);
```

- Add public read-only access:

```csharp
public IReadOnlyCollection<string> SolvedPuzzleIds => _solvedPuzzleIds;
```

- Add methods mirroring treasure boxes:

```csharp
public bool MarkPuzzleSolved(string puzzleId)
{
    if (string.IsNullOrWhiteSpace(puzzleId))
    {
        GD.PushWarning("Cannot mark puzzle solved with null or empty ID.");
        return false;
    }

    return _solvedPuzzleIds.Add(puzzleId);
}

public bool IsPuzzleSolved(string puzzleId)
{
    return !string.IsNullOrWhiteSpace(puzzleId) && _solvedPuzzleIds.Contains(puzzleId);
}

private void RestoreSolvedPuzzleIds(IEnumerable<string>? puzzleIds)
{
    _solvedPuzzleIds.Clear();
    if (puzzleIds == null)
    {
        return;
    }

    foreach (string id in puzzleIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
    {
        _solvedPuzzleIds.Add(id);
    }
}
```

- Include solved IDs in `CollectSaveData()`:

```csharp
SolvedPuzzleIds = _solvedPuzzleIds
    .OrderBy(id => id, StringComparer.Ordinal)
    .ToList(),
```

- Call `RestoreSolvedPuzzleIds(data.SolvedPuzzleIds);` in `LoadFromSaveData()` next to `RestoreOpenedTreasureBoxIds(...)`.

- [ ] **Step 6: Run persistence tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveDataTest|FullyQualifiedName~GameManagerTest"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/save/SaveData.cs scripts/game/GameManager.cs tests/save/SaveDataTest.cs tests/game/GameManagerTest.cs
git commit -m "Add solved puzzle save state"
```

---

### Task 2: Add Puzzle Spawn Nodes And Controller

**Files:**
- Create: `scripts/game/PuzzleSpawnBase.cs`
- Create: `scripts/game/TrapTileSpawn.cs`
- Create: `scripts/game/PuzzleSwitchSpawn.cs`
- Create: `scripts/game/PuzzleGateSpawn.cs`
- Create: `scripts/game/PuzzleRiddleSpawn.cs`
- Create: `scripts/game/PuzzleTrapController.cs`
- Create: `tests/game/PuzzleTrapSpawnTest.cs`
- Create: `tests/game/PuzzleTrapControllerTest.cs`

- [ ] **Step 1: Write failing node tests**

Create `tests/game/PuzzleTrapSpawnTest.cs`:

```csharp
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PuzzleTrapSpawnTest : Node
{
    [TestCase]
    public void PuzzleNodes_AddExpectedGroupsAndFloorOwnershipWorks()
    {
        var floorRoot = new Node2D { Name = "FloorRoot" };
        var gridMap = new GridMap { Name = "GridMap" };
        floorRoot.AddChild(gridMap);

        var trap = new TrapTileSpawn { Name = "TrapTile_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(3, 4) };
        var gate = new PuzzleGateSpawn { Name = "PuzzleGate_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(5, 4) };
        var lever = new PuzzleSwitchSpawn { Name = "PuzzleSwitch_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(4, 4) };
        var riddle = new PuzzleRiddleSpawn { Name = "PuzzleRiddle_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(6, 4) };

        gridMap.AddChild(trap);
        gridMap.AddChild(gate);
        gridMap.AddChild(lever);
        gridMap.AddChild(riddle);

        AssertThat(trap.BelongsToFloor(floorRoot)).IsTrue();
        AssertThat(gate.BelongsToFloor(floorRoot)).IsTrue();
        AssertThat(lever.BelongsToFloor(floorRoot)).IsTrue();
        AssertThat(riddle.BelongsToFloor(floorRoot)).IsTrue();

        AssertThat(trap.IsInGroup("TrapTileSpawn")).IsTrue();
        AssertThat(gate.IsInGroup("PuzzleGateSpawn")).IsTrue();
        AssertThat(lever.IsInGroup("PuzzleSwitchSpawn")).IsTrue();
        AssertThat(riddle.IsInGroup("PuzzleRiddleSpawn")).IsTrue();

        floorRoot.Free();
    }

    [TestCase]
    public void RiddleSpawn_EvaluatesCorrectChoiceId()
    {
        var riddle = new PuzzleRiddleSpawn
        {
            CorrectChoiceId = "east_stone",
            ChoiceIds = ["north_stone", "east_stone"],
            ChoiceLabels = ["North stone", "East stone"]
        };

        AssertThat(riddle.IsCorrectChoice("east_stone")).IsTrue();
        AssertThat(riddle.IsCorrectChoice("north_stone")).IsFalse();
        AssertThat(riddle.GetChoices().Count).IsEqual(2);
    }

    [TestCase]
    public void GateSpawn_ApplyOpenStateTracksBlockingState()
    {
        var gate = new PuzzleGateSpawn { StartsClosed = true };

        gate.ApplySolvedState(false);
        AssertThat(gate.IsOpen).IsFalse();
        AssertThat(gate.BlocksMovement).IsTrue();

        gate.ApplySolvedState(true);
        AssertThat(gate.IsOpen).IsTrue();
        AssertThat(gate.BlocksMovement).IsFalse();
    }
}
```

- [ ] **Step 2: Write failing controller tests**

Create `tests/game/PuzzleTrapControllerTest.cs`:

```csharp
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PuzzleTrapControllerTest : Node
{
    [TestCase]
    public void SwitchArmsPuzzleAndCorrectRiddleSolvesIt()
    {
        var manager = new GameManager();
        var controller = new PuzzleTrapController(manager);
        var riddle = new PuzzleRiddleSpawn
        {
            PuzzleId = "Puzzle_Test",
            CorrectChoiceId = "east_stone"
        };

        AssertThat(controller.TrySolveRiddle(riddle, "east_stone").Solved).IsFalse();

        controller.ActivateSwitch("Puzzle_Test");
        var result = controller.TrySolveRiddle(riddle, "east_stone");

        AssertThat(result.Solved).IsTrue();
        AssertThat(manager.IsPuzzleSolved("Puzzle_Test")).IsTrue();

        manager.Free();
    }

    [TestCase]
    public void WrongRiddleChoiceDoesNotSolvePuzzle()
    {
        var manager = new GameManager();
        var controller = new PuzzleTrapController(manager);
        var riddle = new PuzzleRiddleSpawn
        {
            PuzzleId = "Puzzle_Test",
            CorrectChoiceId = "east_stone"
        };

        controller.ActivateSwitch("Puzzle_Test");
        var result = controller.TrySolveRiddle(riddle, "north_stone");

        AssertThat(result.Solved).IsFalse();
        AssertThat(result.ShouldApplyPenalty).IsTrue();
        AssertThat(manager.IsPuzzleSolved("Puzzle_Test")).IsFalse();

        manager.Free();
    }
}
```

- [ ] **Step 3: Run failing node/controller tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PuzzleTrapSpawnTest|FullyQualifiedName~PuzzleTrapControllerTest"
```

Expected: FAIL because the puzzle classes do not exist.

- [ ] **Step 4: Implement shared puzzle node base**

Create `scripts/game/PuzzleSpawnBase.cs`:

```csharp
using Godot;

[Tool]
public abstract partial class PuzzleSpawnBase : Sprite2D
{
    [Export] public string PuzzleId { get; set; } = "";
    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    [Export] public bool EditorSnapEnabled { get; set; }

    protected abstract string GroupName { get; }
    protected virtual Color FallbackColor => Colors.MediumPurple;

    public override void _Ready()
    {
        if (!IsInGroup(GroupName))
        {
            AddToGroup(GroupName);
        }

        Centered = true;
        ZIndex = 2;
        var grid = FindGridMap();
        if (grid != null)
        {
            UpdateVisual(grid);
        }
    }

    public bool BelongsToFloor(Node? floorRoot)
    {
        return floorRoot != null && (ReferenceEquals(GetParent(), floorRoot) || floorRoot.IsAncestorOf(this));
    }

    public void UpdateVisual(GridMap grid)
    {
        var ground = grid.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        int cell = grid.CellSize;
        Position = new Vector2(GridPosition.X * cell + cell / 2f, GridPosition.Y * cell + cell / 2f) + offset;
    }

    public override void _Draw()
    {
        if (Texture != null)
        {
            return;
        }

        var size = new Vector2(24, 24);
        DrawRect(new Rect2(-size / 2f, size), FallbackColor);
        DrawRect(new Rect2(-size / 2f, size), Colors.White, false, 2.0f);
    }

    private GridMap? FindGridMap()
    {
        return GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap")
            ?? GetTree()?.Root.FindChild("GridMap", recursive: true, owned: false) as GridMap;
    }
}
```

- [ ] **Step 5: Implement concrete puzzle spawn nodes**

Create `scripts/game/TrapTileSpawn.cs`:

```csharp
using Godot;

[Tool]
public partial class TrapTileSpawn : PuzzleSpawnBase
{
    protected override string GroupName => "TrapTileSpawn";
    protected override Color FallbackColor => Colors.OrangeRed;

    [Export] public int Damage { get; set; } = 12;
    [Export] public string StatusEffectId { get; set; } = "";
    [Export] public int StatusMagnitude { get; set; }
    [Export] public int StatusTurns { get; set; }
}
```

Create `scripts/game/PuzzleSwitchSpawn.cs`:

```csharp
using Godot;

[Tool]
public partial class PuzzleSwitchSpawn : PuzzleSpawnBase
{
    protected override string GroupName => "PuzzleSwitchSpawn";
    protected override Color FallbackColor => Colors.DeepSkyBlue;

    [Export] public string SwitchId { get; set; } = "";
    [Export] public string PromptText { get; set; } = "Use";
    [Export] public string ActivatedText { get; set; } = "The mechanism wakes.";
}
```

Create `scripts/game/PuzzleGateSpawn.cs`:

```csharp
using Godot;

[Tool]
public partial class PuzzleGateSpawn : PuzzleSpawnBase
{
    protected override string GroupName => "PuzzleGateSpawn";
    protected override Color FallbackColor => IsOpen ? Colors.SeaGreen : Colors.DarkSlateGray;

    [Export] public string GateId { get; set; } = "";
    [Export] public bool StartsClosed { get; set; } = true;

    public bool IsOpen { get; private set; }
    public bool BlocksMovement => StartsClosed && !IsOpen;

    public void ApplySolvedState(bool solved)
    {
        IsOpen = solved || !StartsClosed;
        QueueRedraw();
    }
}
```

Create `scripts/game/PuzzleRiddleSpawn.cs`:

```csharp
using Godot;
using System.Collections.Generic;

[Tool]
public partial class PuzzleRiddleSpawn : PuzzleSpawnBase
{
    protected override string GroupName => "PuzzleRiddleSpawn";
    protected override Color FallbackColor => Colors.Gold;

    [Export] public string RiddleId { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string PromptText { get; set; } = "";
    [Export] public Godot.Collections.Array<string> ChoiceIds { get; set; } = new();
    [Export] public Godot.Collections.Array<string> ChoiceLabels { get; set; } = new();
    [Export] public string CorrectChoiceId { get; set; } = "";
    [Export] public int WrongAnswerDamage { get; set; } = 12;

    public bool IsCorrectChoice(string choiceId) => !string.IsNullOrWhiteSpace(choiceId) && choiceId == CorrectChoiceId;

    public IReadOnlyList<(string Id, string Label)> GetChoices()
    {
        var choices = new List<(string Id, string Label)>();
        for (int i = 0; i < ChoiceIds.Count; i++)
        {
            string id = ChoiceIds[i];
            string label = i < ChoiceLabels.Count ? ChoiceLabels[i] : id;
            choices.Add((id, label));
        }

        return choices;
    }
}
```

- [ ] **Step 6: Implement `PuzzleTrapController`**

Create `scripts/game/PuzzleTrapController.cs`:

```csharp
using System.Collections.Generic;

public sealed class PuzzleTrapController
{
    private readonly GameManager _gameManager;
    private readonly HashSet<string> _armedPuzzleIds = new(System.StringComparer.Ordinal);

    public PuzzleTrapController(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public bool IsPuzzleArmed(string puzzleId)
    {
        return !string.IsNullOrWhiteSpace(puzzleId) && _armedPuzzleIds.Contains(puzzleId);
    }

    public bool ActivateSwitch(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId) || _gameManager.IsPuzzleSolved(puzzleId))
        {
            return false;
        }

        return _armedPuzzleIds.Add(puzzleId);
    }

    public PuzzleRiddleResult TrySolveRiddle(PuzzleRiddleSpawn riddle, string choiceId)
    {
        if (riddle == null || string.IsNullOrWhiteSpace(riddle.PuzzleId))
        {
            return new PuzzleRiddleResult(false, false, "Invalid puzzle.");
        }

        if (_gameManager.IsPuzzleSolved(riddle.PuzzleId))
        {
            return new PuzzleRiddleResult(true, false, "Already solved.");
        }

        if (!IsPuzzleArmed(riddle.PuzzleId))
        {
            return new PuzzleRiddleResult(false, false, "The mechanism is dormant.");
        }

        if (!riddle.IsCorrectChoice(choiceId))
        {
            return new PuzzleRiddleResult(false, true, "The seal rejects the answer.");
        }

        _gameManager.MarkPuzzleSolved(riddle.PuzzleId);
        return new PuzzleRiddleResult(true, false, "The gate opens.");
    }
}

public readonly record struct PuzzleRiddleResult(bool Solved, bool ShouldApplyPenalty, string Message);
```

- [ ] **Step 7: Run node/controller tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PuzzleTrapSpawnTest|FullyQualifiedName~PuzzleTrapControllerTest"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add scripts/game/PuzzleSpawnBase.cs scripts/game/TrapTileSpawn.cs scripts/game/PuzzleSwitchSpawn.cs scripts/game/PuzzleGateSpawn.cs scripts/game/PuzzleRiddleSpawn.cs scripts/game/PuzzleTrapController.cs tests/game/PuzzleTrapSpawnTest.cs tests/game/PuzzleTrapControllerTest.cs
git commit -m "Add puzzle trap spawn nodes"
```

---

### Task 3: Add Puzzle Entities To Floor JSON Import/Export

**Files:**
- Modify: `scripts/tilemap_json/FloorJsonModel.cs`
- Modify: `scripts/tilemap_json/TilemapJsonImporter.cs`
- Modify: `scripts/tilemap_json/TilemapJsonExporter.cs`
- Modify: `tests/tilemap_json/FloorJsonModelTest.cs`
- Modify: `tests/tilemap_json/TilemapJsonImporterTest.cs`
- Modify: `tests/tilemap_json/TilemapJsonExporterTest.cs`

- [ ] **Step 1: Add failing JSON model test**

In `tests/tilemap_json/FloorJsonModelTest.cs`, add a parse test for `trap_tiles`, `puzzle_switches`, `puzzle_gates`, and `puzzle_riddles`. Assert:

- one trap with `id`, `puzzle_id`, position, damage, `status_effect`, `status_magnitude`, `status_turns`
- one switch with `id`, `puzzle_id`, position, prompt text, activated text
- one gate with `id`, `puzzle_id`, position, `starts_closed`
- one riddle with `id`, `puzzle_id`, position, prompt, choices, correct choice, wrong-answer damage

- [ ] **Step 2: Add failing importer tests**

In `tests/tilemap_json/TilemapJsonImporterTest.cs`, add:

- `ImportToScene_AssignsOwnerToCreatedPuzzleNodes`
- `ImportToScene_UpdatesExistingPuzzleNodesById`
- `ImportToScene_RemovesStalePuzzleNodesSynchronously`
- `ImportToScene_SkipsPuzzleNodesWithEmptyIdOrPuzzleId`

Use the existing treasure-box importer tests as the exact shape.

- [ ] **Step 3: Add failing exporter test**

In `tests/tilemap_json/TilemapJsonExporterTest.cs`, add `ExportScene_IncludesPuzzleTrapEntities`. Create all four puzzle node types under a test `GridMap`, export, and assert every entity list contains one record with expected values.

- [ ] **Step 4: Run failing JSON/import/export tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest"
```

Expected: FAIL because JSON DTOs and importer/exporter support do not exist.

- [ ] **Step 5: Add JSON DTOs**

In `scripts/tilemap_json/FloorJsonModel.cs`, add nullable lists to `SceneEntities`:

```csharp
[JsonPropertyName("trap_tiles")]
public List<TrapTileData>? TrapTiles { get; set; }

[JsonPropertyName("puzzle_switches")]
public List<PuzzleSwitchData>? PuzzleSwitches { get; set; }

[JsonPropertyName("puzzle_gates")]
public List<PuzzleGateData>? PuzzleGates { get; set; }

[JsonPropertyName("puzzle_riddles")]
public List<PuzzleRiddleData>? PuzzleRiddles { get; set; }
```

Add DTO classes:

```csharp
public class TrapTileData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("puzzle_id")] public string PuzzleId { get; set; } = "";
    [JsonPropertyName("position")] public Vector2IData Position { get; set; } = new();
    [JsonPropertyName("damage")] public int Damage { get; set; } = 12;
    [JsonPropertyName("status_effect")] public string StatusEffect { get; set; } = "";
    [JsonPropertyName("status_magnitude")] public int StatusMagnitude { get; set; }
    [JsonPropertyName("status_turns")] public int StatusTurns { get; set; }
}

public class PuzzleSwitchData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("puzzle_id")] public string PuzzleId { get; set; } = "";
    [JsonPropertyName("position")] public Vector2IData Position { get; set; } = new();
    [JsonPropertyName("prompt_text")] public string PromptText { get; set; } = "Use";
    [JsonPropertyName("activated_text")] public string ActivatedText { get; set; } = "";
}

public class PuzzleGateData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("puzzle_id")] public string PuzzleId { get; set; } = "";
    [JsonPropertyName("position")] public Vector2IData Position { get; set; } = new();
    [JsonPropertyName("starts_closed")] public bool StartsClosed { get; set; } = true;
}

public class PuzzleRiddleData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("puzzle_id")] public string PuzzleId { get; set; } = "";
    [JsonPropertyName("position")] public Vector2IData Position { get; set; } = new();
    [JsonPropertyName("prompt_text")] public string PromptText { get; set; } = "";
    [JsonPropertyName("choices")] public List<PuzzleRiddleChoiceData> Choices { get; set; } = new();
    [JsonPropertyName("correct_choice_id")] public string CorrectChoiceId { get; set; } = "";
    [JsonPropertyName("wrong_answer_damage")] public int WrongAnswerDamage { get; set; } = 12;
}

public class PuzzleRiddleChoiceData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}
```

- [ ] **Step 6: Implement importer support**

In `TilemapJsonImporter.ImportEntities(...)`, call four new import methods only when lists are non-null. Follow the treasure-box behavior: absent key preserves existing nodes; explicit empty list removes stale nodes.

Add generic helper if useful:

```csharp
private static bool HasValidPuzzleIdentity(string id, string puzzleId, string entityType)
{
    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(puzzleId))
    {
        GD.PrintErr($"[TilemapJsonImporter] Skipping {entityType} with empty id or puzzle_id.");
        return false;
    }

    return true;
}
```

Configure nodes with `Set(...)` or typed properties, assign `Position = ToCenteredCellPosition(data.Position)`, `ZIndex = 2`, and `Owner = sceneRoot`.

- [ ] **Step 7: Implement exporter support**

In `TilemapJsonExporter.ExportEntities(...)`, set:

```csharp
entities.TrapTiles = ExportTrapTiles(gridMapNode);
entities.PuzzleSwitches = ExportPuzzleSwitches(gridMapNode);
entities.PuzzleGates = ExportPuzzleGates(gridMapNode);
entities.PuzzleRiddles = ExportPuzzleRiddles(gridMapNode);
```

Each exporter should iterate typed child nodes and copy exported properties into DTOs.

- [ ] **Step 8: Run JSON/import/export tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest"
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add scripts/tilemap_json/FloorJsonModel.cs scripts/tilemap_json/TilemapJsonImporter.cs scripts/tilemap_json/TilemapJsonExporter.cs tests/tilemap_json/FloorJsonModelTest.cs tests/tilemap_json/TilemapJsonImporterTest.cs tests/tilemap_json/TilemapJsonExporterTest.cs
git commit -m "Round trip puzzle trap floor entities"
```

---

### Task 4: Register Puzzle Cells In GridMap

**Files:**
- Modify: `scripts/game/GridMap.cs`
- Modify: `scripts/game/PlayerController.cs`
- Modify: `tests/game/TreasureBoxSpawnTest.cs`
- Modify: `tests/game/PlayerControllerTest.cs`

- [ ] **Step 1: Add failing GridMap tests**

Add tests to `tests/game/TreasureBoxSpawnTest.cs` or create `tests/game/GridMapPuzzleTrapTest.cs` if the file is getting crowded:

- `RegisterStaticPuzzleTraps_RegistersActiveTrapAsWalkableTrapCell`
- `RegisterStaticPuzzleTraps_RegistersClosedGateAsBlockingGateCell`
- `RegisterStaticPuzzleTraps_RegistersSolvedGateAsEmptyCell`
- `TryMovePlayer_ActiveTrapMovesPlayerAndEmitsTrapTriggered`
- `TryMovePlayer_ClosedPuzzleGateBlocksMovement`
- `TryRequestPuzzleInteraction_EmitsForSwitchOrRiddleFacingDirection`

- [ ] **Step 2: Add failing PlayerController ordering test**

In `tests/game/PlayerControllerTest.cs`, add a test proving interact priority remains:

1. pending stair transition wins
2. treasure open comes before puzzle interaction only if the target is a treasure
3. puzzle interaction fires for switch/riddle when no stair/treasure is active

Use the existing `Interact_ValidPendingStairTransitionDoesNotAlsoRequestTreasureOpen` shape.

- [ ] **Step 3: Run failing movement/registration tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GridMapPuzzleTrapTest|FullyQualifiedName~TreasureBoxSpawnTest|FullyQualifiedName~PlayerControllerTest"
```

Expected: FAIL because GridMap has no puzzle cell types or signals.

- [ ] **Step 4: Add GridMap cell types and signals**

In `scripts/game/GridMap.cs`, extend `CellType`:

```csharp
TrapTile = 6,
PuzzleGate = 7,
PuzzleInteractable = 8
```

Add signals near existing signals:

```csharp
[Signal] public delegate void TrapTileTriggeredEventHandler(Vector2I trapPosition);
[Signal] public delegate void PuzzleInteractionRequestedEventHandler(Vector2I puzzlePosition);
```

- [ ] **Step 5: Add registration and lookup APIs**

Add:

```csharp
public void RegisterStaticPuzzleEntities()
```

It should:

- filter each group to the current floor root with `BelongsToFloor(...)`
- skip empty IDs or puzzle IDs
- convert tilemap coords to internal grid coords using `_tilemapOrigin`
- set active traps to `CellType.TrapTile` unless puzzle is solved
- set gates to `CellType.PuzzleGate` if `BlocksMovement`, otherwise `CellType.Empty`
- set switch/riddle cells to `CellType.PuzzleInteractable`
- call `UpdateVisual(this)` and `ApplySolvedState(...)` where applicable

Add:

```csharp
public bool TryRequestPuzzleInteraction(Vector2I facingDirection)
```

It should mirror `TryRequestTreasureBoxOpen(...)`, but target `CellType.PuzzleInteractable` and emit `PuzzleInteractionRequested`.

- [ ] **Step 6: Wire movement behavior**

In `TryMovePlayer(...)`:

- return false for `CellType.PuzzleGate`
- return false for `CellType.PuzzleInteractable`
- allow `CellType.TrapTile` movement, then emit `TrapTileTriggered` after `PlayerMoved`

In `LoadFloor(...)`, call:

```csharp
CallDeferred(nameof(RegisterStaticPuzzleEntities));
```

after treasure boxes and before stairs or after stairs; either order is fine as long as gates do not overwrite stairs.

- [ ] **Step 7: Wire PlayerController interact fallback**

In `PlayerController._UnhandledInput(...)`, after treasure open:

```csharp
if (_gridMap != null && _gridMap.TryRequestPuzzleInteraction(_lastFacingDirection))
{
    _awaitingStairInteractRelease = true;
}
```

Keep stair handling first.

- [ ] **Step 8: Run movement/registration tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GridMapPuzzleTrapTest|FullyQualifiedName~TreasureBoxSpawnTest|FullyQualifiedName~PlayerControllerTest"
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add scripts/game/GridMap.cs scripts/game/PlayerController.cs tests/game/TreasureBoxSpawnTest.cs tests/game/PlayerControllerTest.cs tests/game/GridMapPuzzleTrapTest.cs
git commit -m "Register puzzle trap cells"
```

---

### Task 5: Add Runtime Puzzle Interaction Flow

**Files:**
- Create: `scripts/ui/PuzzleRiddleDialog.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`

- [ ] **Step 1: Add failing runtime tests**

In `tests/game/GameTest.cs`, add focused tests:

- `Game_TrapTileTriggerAppliesDamageAndKeepsPlayerOnTrap`
- `Game_SwitchThenCorrectRiddleSolvesPuzzleOpensGateAndDisablesTrap`
- `Game_WrongRiddleAnswerAppliesPenaltyAndAllowsRetry`
- `Game_PuzzlePromptUsesUseForSwitchAndSolveForRiddle`

Keep tests similar to `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt`: instantiate `Game.tscn`, inject puzzle nodes into the current `GridMap`, replace `_grid` with a small clean grid, call `RegisterStaticPuzzleEntities`, drive `PlayerController`, and assert manager/player/node state.

- [ ] **Step 2: Run failing runtime tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Game_TrapTileTrigger|FullyQualifiedName~Game_SwitchThenCorrectRiddle|FullyQualifiedName~Game_WrongRiddleAnswer|FullyQualifiedName~Game_PuzzlePrompt"
```

Expected: FAIL because `Game` does not connect puzzle signals or display riddle UI.

- [ ] **Step 3: Add `PuzzleRiddleDialog`**

Create `scripts/ui/PuzzleRiddleDialog.cs` as an `AcceptDialog` with:

- signal `ChoiceSelected(string choiceId)`
- signal `PuzzleRiddleClosed()`
- `OpenRiddle(PuzzleRiddleSpawn riddle, string? message = null)`
- one button per `riddle.GetChoices()`
- no OK button

Use the construction style from `DialogueDialog.cs`.

- [ ] **Step 4: Add Game fields and signal wiring**

In `scripts/game/Game.cs`:

- add field `private PuzzleTrapController? _puzzleTrapController;`
- add field `private PuzzleRiddleDialog? _puzzleRiddleDialog;`
- initialize controller after `_gameManager` is available
- subscribe to `_gridMap.TrapTileTriggered += OnTrapTileTriggered`
- subscribe to `_gridMap.PuzzleInteractionRequested += OnPuzzleInteractionRequested`
- unsubscribe in the same places treasure box signals are unsubscribed

- [ ] **Step 5: Implement node finders**

Add private helpers mirroring `FindTreasureBoxAt(...)`:

```csharp
private TrapTileSpawn? FindTrapTileAt(Vector2I internalGridPosition)
private PuzzleSwitchSpawn? FindPuzzleSwitchAt(Vector2I internalGridPosition)
private PuzzleRiddleSpawn? FindPuzzleRiddleAt(Vector2I internalGridPosition)
private PuzzleGateSpawn? FindPuzzleGateByPuzzleId(string puzzleId)
```

Each should convert internal grid coords with `_gridMap.InternalGridToTilemapCoords(...)` and filter by `BelongsToFloor(...)`.

- [ ] **Step 6: Implement trap damage flow**

`OnTrapTileTriggered(Vector2I trapPosition)` should:

- ignore if battle/NPC/world interaction is active
- find the trap
- ignore if puzzle already solved
- subtract `trap.Damage` from `_gameManager.Player.CurrentHealth`, clamped to at least 1 for this first slice
- optionally apply status if `trap.StatusEffectId` parses into `StatusEffectType`
- call `_gameManager.NotifyPlayerStatsChanged()`

Do not start a battle or force a floor reload.

- [ ] **Step 7: Implement switch and riddle flow**

`OnPuzzleInteractionRequested(Vector2I puzzlePosition)` should:

- ignore if battle/NPC/world interaction is active
- if a switch is at the target, call `_puzzleTrapController.ActivateSwitch(switch.PuzzleId)` inside a short world interaction and update prompt
- if a riddle is at the target, start world interaction, show `PuzzleRiddleDialog`, and resolve on choice

On correct riddle:

- mark puzzle solved through controller
- call a helper that re-applies solved state to gates/traps and clears blocking cells
- end world interaction
- update prompt and player stats

On wrong riddle:

- apply `riddle.WrongAnswerDamage`, clamped to at least 1 HP
- keep puzzle unsolved
- end world interaction
- allow retry after interact release

- [ ] **Step 8: Update prompt text**

Extend `UpdateInteractionPrompt()`:

- keep treasure prompt as `Open`
- switch prompt as `Use`
- riddle prompt as `Solve`
- hide prompt during battle, NPC interaction, world interaction, or solved riddle/gate state

- [ ] **Step 9: Run runtime tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Game_TrapTileTrigger|FullyQualifiedName~Game_SwitchThenCorrectRiddle|FullyQualifiedName~Game_WrongRiddleAnswer|FullyQualifiedName~Game_PuzzlePrompt"
```

Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add scripts/ui/PuzzleRiddleDialog.cs scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "Add puzzle trap runtime interactions"
```

---

### Task 6: Author Floor1F Puzzle Content In Generator

**Files:**
- Modify: `tools/floor1_maze_generator.py`
- Modify: `tests/tools/test_floor1_maze_generator.py`
- Modify: `tests/game/Floor1FMazeLayoutTest.cs`
- Generated/imported: `scenes/game/floors/Floor1F.json`
- Generated/imported: `scenes/game/floors/Floor1F.tscn`

- [ ] **Step 1: Add failing generator tests**

In `tests/tools/test_floor1_maze_generator.py`:

- import new constants `FLOOR1_PUZZLE_TRAPS`, `FLOOR1_PUZZLE_SWITCHES`, `FLOOR1_PUZZLE_GATES`, `FLOOR1_PUZZLE_RIDDLES`
- assert every puzzle coordinate is walkable
- assert puzzle nodes do not overlap enemies, stairs, treasure boxes, or each other
- assert `hidden_room_south` is removed from `FLOOR1_HIDDEN_PLACEHOLDERS`
- assert `TreasureBox_1F_SouthHiddenCache` is now at `(24, 56)`
- assert required route reachability still exists when gates are treated as closed
- assert shortcut/reward route opens when the gate cell is treated as walkable

- [ ] **Step 2: Add failing scene layout tests**

In `tests/game/Floor1FMazeLayoutTest.cs`:

- replace the `HiddenPlaceholders` entry `(19, 54)` with only remaining placeholders
- update expected `TreasureBox_1F_SouthHiddenCache` to `(24, 56)`
- add dictionaries/arrays for expected trap/switch/gate/riddle nodes
- add `Floor1F_GeneratedMaze_HasExpectedPuzzleTrapSet`
- assert trap/switch/gate/riddle nodes are on walkable tiles
- assert no overlap with enemies, treasure boxes, stairs, or remaining hidden placeholders
- assert required stairs are reachable when closed puzzle gates are treated as walls

- [ ] **Step 3: Run failing generator/layout tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor1FMazeLayoutTest"
```

Expected: FAIL because the generator and scene do not include puzzle entities yet.

- [ ] **Step 4: Update Floor1F generator constants**

In `tools/floor1_maze_generator.py`:

- remove `hidden_room_south` from `FLOOR1_HIDDEN_PLACEHOLDERS`
- move `TreasureBox_1F_SouthHiddenCache` to `(24, 56)`
- add:

```python
FLOOR1_PUZZLE_ID = "Puzzle_1F_SouthShortcutTrial"

FLOOR1_PUZZLE_TRAPS = {
    "TrapTile_1F_SouthTrial_01": {"position": (18, 53), "damage": 12},
    "TrapTile_1F_SouthTrial_02": {"position": (17, 54), "damage": 12},
    "TrapTile_1F_SouthTrial_03": {"position": (20, 54), "damage": 12},
    "TrapTile_1F_SouthTrial_04": {"position": (21, 55), "damage": 12},
}

FLOOR1_PUZZLE_SWITCHES = {
    "PuzzleSwitch_1F_SouthTrial_Lever": {
        "position": (16, 52),
        "prompt_text": "Use",
        "activated_text": "The lever wakes the old shortcut seal.",
    }
}

FLOOR1_PUZZLE_GATES = {
    "PuzzleGate_1F_SouthTrial_Shortcut": {"position": (23, 56), "starts_closed": True}
}

FLOOR1_PUZZLE_RIDDLES = {
    "PuzzleRiddle_1F_SouthTrial_Seal": {
        "position": (22, 54),
        "prompt_text": "Four stones face the old shortcut. Which stone sleeps until the lever wakes it?",
        "choices": [
            {"id": "north_stone", "label": "North stone"},
            {"id": "east_stone", "label": "East stone"},
            {"id": "south_stone", "label": "South stone"},
        ],
        "correct_choice_id": "east_stone",
        "wrong_answer_damage": 12,
    }
}
```

- [ ] **Step 5: Emit puzzle entities**

Add helpers similar to `treasure_box_entities(...)` and include these entity keys in `build_floor1_model()`:

```python
"trap_tiles": trap_tile_entities(FLOOR1_PUZZLE_TRAPS),
"puzzle_switches": puzzle_switch_entities(FLOOR1_PUZZLE_SWITCHES),
"puzzle_gates": puzzle_gate_entities(FLOOR1_PUZZLE_GATES),
"puzzle_riddles": puzzle_riddle_entities(FLOOR1_PUZZLE_RIDDLES),
```

Each entity must include `puzzle_id`.

- [ ] **Step 6: Extend generator validation**

In `validate_model(...)`, include the four puzzle entity lists in:

- walkable placement checks
- overlap checks
- duplicate ID checks
- route checks, treating `puzzle_gates` as blocked for required stair paths

- [ ] **Step 7: Run Python generator tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 8: Regenerate Floor1F JSON**

Run the generator with the repo's existing command shape:

```bash
python3 tools/floor1_maze_generator.py --floor1-output scenes/game/floors/Floor1F.json --floor2-output scenes/game/floors/Floor2F.json --floor1-def resources/floors/Floor1F.tres --floor2-def resources/floors/Floor2F.tres
```

- [ ] **Step 9: Import generated JSON into Floor1F scene**

Use the established import pipeline:

```bash
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor1F.json
```

If the local Godot path is required, set `GODOT_PATH` as in the existing floor-generation workflow.

- [ ] **Step 10: Run scene layout tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor1FMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add tools/floor1_maze_generator.py tests/tools/test_floor1_maze_generator.py tests/game/Floor1FMazeLayoutTest.cs scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn
git commit -m "Author Floor1F puzzle trap chamber"
```

---

### Task 7: Focused Full Verification

**Files:**
- No new files unless verification exposes fixes.

- [ ] **Step 1: Run focused Python tests**

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 2: Run focused GdUnit test set**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest|FullyQualifiedName~SaveDataTest|FullyQualifiedName~GameManagerTest|FullyQualifiedName~PuzzleTrapSpawnTest|FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GridMapPuzzleTrapTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~Floor1FMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 3: Run build**

```bash
dotnet build Sirius.sln
```

Expected: PASS.

- [ ] **Step 4: Optional runtime smoke**

Open the project in Godot, start the game, enter Floor1F, and inspect the south optional branch:

- trap nodes are visible
- closed gate blocks shortcut
- trap damage applies but does not kill the player instantly
- switch arms the puzzle
- wrong riddle answer hurts and allows retry
- correct riddle answer opens the gate
- moved treasure behind the gate opens once
- shortcut route is passable after solve
- save/load keeps the gate solved

- [ ] **Step 5: Final commit if verification fixes were needed**

If verification required changes:

```bash
git add docs/superpowers/plans/2026-05-14-floor1f-puzzle-traps.md scripts tests tools scenes
git commit -m "Verify Floor1F puzzle traps"
```

If no changes were needed, do not create an empty commit.
