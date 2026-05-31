# Floor Exploration, Stairs, and Asset Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve Sirius 1F/2F exploration payoff, make stair travel immediate on step-on, and fill GF/early-floor sprite coverage gaps.

**Architecture:** Keep `tools/floor1_maze_generator.py` as the source of truth for 1F, 2F, and the 3F landing. Add generator graph invariants for branch payoff, update deterministic content, regenerate/import scenes, then change `PlayerController` so stair movement transitions immediately. Asset work follows the existing runtime path convention and updates docs after files exist.

**Tech Stack:** Godot 4.6.2 C#/.NET 8, GdUnit4, Python `unittest`, Godot tilemap JSON sync, PNG sprite sheets.

---

## File Structure

- Modify `tests/tools/test_floor1_maze_generator.py`: add branch-payoff graph helpers and failing tests for 1F/2F.
- Modify `tools/floor1_maze_generator.py`: add branch-payoff validation, add 1F/2F enemies/treasure/loop cuts, and keep generated models deterministic.
- Modify `tests/game/Floor1FMazeLayoutTest.cs`: update expected enemy/treasure counts and add scene-level branch-payoff assertions.
- Modify `tests/game/Floor2FMazeLayoutTest.cs`: update expected enemy/treasure counts, add scene-level branch-payoff assertions, and add 2F/3F runtime stair transition tests.
- Modify `tests/game/FloorGFMazeLayoutTest.cs`: replace interact-required stair test with immediate GF to 1F coverage.
- Modify `tests/game/PlayerControllerTest.cs`: remove pending-stair interaction expectations and keep focused coverage for non-stair `interact` actions.
- Modify `scripts/game/PlayerController.cs`: transition floors immediately after successful stair movement and stop treating stairs as `interact` targets.
- Regenerate `scenes/game/floors/Floor1F.json`, `Floor2F.json`, `Floor3F.json`.
- Import regenerated JSON into `scenes/game/floors/Floor1F.tscn`, `Floor2F.tscn`, `Floor3F.tscn`.
- Update `resources/floors/Floor1F.tres`, `Floor2F.tres`, `Floor3F.tres` if generator output changes stair arrays.
- Add sprite sheets under `assets/sprites/enemies/{orc,skeleton_warrior,cave_spider,forest_spirit}/sprite_sheet.png` and `assets/sprites/npcs/{shopkeeper,healer}/sprite_sheet.png`.
- Modify `tools/sprite_sheet_merger.py`: include `assets/sprites/npcs/*` in frame discovery.
- Modify `docs/enemies/ENEMY_SPRITES.md`: mark migrated/generated enemy sheets as existing.
- Create `docs/npcs/NPC_SPRITES.md`: document NPC runtime sprite conventions and status.
- Create `tests/tools/test_sprite_sheet_merger.py`: cover NPC frame discovery and sheet generation.

## Task 1: Add Branch-Payoff Generator Tests

**Files:**
- Modify: `tests/tools/test_floor1_maze_generator.py`

- [ ] **Step 1: Add branch-payoff helpers near `count_dead_end_cells`**

```python
def branch_payoff_positions(model):
    positions = {}
    entities = model["entities"]
    payoff_keys = (
        "enemy_spawns",
        "treasure_boxes",
        "stair_connections",
        "hidden_placeholders",
        "trap_tiles",
        "puzzle_switches",
        "puzzle_gates",
        "puzzle_riddles",
    )
    for key in payoff_keys:
        for entity in entities.get(key, []) or []:
            position = (entity["position"]["x"], entity["position"]["y"])
            positions.setdefault(position, []).append(entity["id"])
    return positions


def dead_end_branches(walkable, width, height):
    branches = []
    for leaf in sorted(position for position in walkable if neighbor_count(walkable, position) == 1):
        path = [leaf]
        previous = None
        current = leaf

        while True:
            next_cells = [
                cell for cell in (
                    (current[0] + 1, current[1]),
                    (current[0] - 1, current[1]),
                    (current[0], current[1] + 1),
                    (current[0], current[1] - 1),
                )
                if cell in walkable and cell != previous
            ]
            if not next_cells:
                break

            next_cell = next_cells[0]
            if neighbor_count(walkable, next_cell) != 2:
                break

            previous = current
            current = next_cell
            path.append(current)

        branches.append(path)
    return branches


def unrewarded_dead_end_branches(model, walkable, width, height):
    payoff_positions = branch_payoff_positions(model)
    empty_branches = []

    for branch in dead_end_branches(walkable, width, height):
        branch_cells = set(branch)
        adjacent_cells = set()
        for x, y in branch:
            adjacent_cells.update(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

        if branch_cells & payoff_positions.keys():
            continue
        if adjacent_cells & payoff_positions.keys():
            continue

        empty_branches.append(branch)

    return empty_branches
```

- [ ] **Step 2: Add a failing 1F test after `test_maze_has_multiple_dead_end_branches`**

```python
    def test_floor1_dead_end_branches_have_payoff(self):
        empty_branches = unrewarded_dead_end_branches(
            self.model,
            self.walkable,
            FLOOR1_WIDTH,
            FLOOR1_HEIGHT,
        )

        self.assertEqual(empty_branches, [])
```

- [ ] **Step 3: Add a failing 2F test after `test_floor2_has_moderate_maze_structure`**

```python
    def test_floor2_dead_end_branches_have_payoff(self):
        empty_branches = unrewarded_dead_end_branches(
            self.model,
            self.walkable,
            FLOOR2_WIDTH,
            FLOOR2_HEIGHT,
        )

        self.assertEqual(empty_branches, [])
```

- [ ] **Step 4: Run tests and verify RED**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator.Floor1MazeGeneratorTest.test_floor1_dead_end_branches_have_payoff tests.tools.test_floor1_maze_generator.Floor2MazeGeneratorTest.test_floor2_dead_end_branches_have_payoff -v
```

Expected: FAIL. Current failures should list unrewarded 1F and 2F branch paths instead of `[]`.

- [ ] **Step 5: Commit failing tests**

```bash
git add tests/tools/test_floor1_maze_generator.py
git commit -m "test: require floor branch payoffs"
```

## Task 2: Add 1F and 2F Payoff Content

**Files:**
- Modify: `tools/floor1_maze_generator.py`
- Modify: `tests/tools/test_floor1_maze_generator.py`

- [ ] **Step 1: Update 1F expected treasure in tests**

Add these rows to `EXPECTED_FLOOR1_TREASURE`:

```python
    "TreasureBox_1F_WestCrossingCache": ((5, 37), 55, {"health_potion": 1}),
    "TreasureBox_1F_NorthSpurCache": ((28, 20), 0, {"mana_potion": 1}),
    "TreasureBox_1F_CentralSpurCache": ((43, 34), 95, {"iron_skin": 1}),
    "TreasureBox_1F_SouthShortcutPocket": ((26, 56), 0, {"antidote": 1}),
```

Update `test_places_enemy_gates_and_no_npcs` expected enemy map with:

```python
                "EnemySpawn_Goblin_SideRoom": "goblin",
                "EnemySpawn_Orc_SouthBend": "orc",
                "EnemySpawn_Skeleton_CentralSpur": "skeleton_warrior",
```

- [ ] **Step 2: Update 2F expected treasure and enemies in tests**

Add these rows to `EXPECTED_FLOOR2_TREASURE`:

```python
    "TreasureBox_2F_WestArchiveCache": ((4, 32), 120, {"major_health_potion": 1}),
    "TreasureBox_2F_NorthLandingCache": ((18, 4), 0, {"major_mana_potion": 1}),
    "TreasureBox_2F_SouthStacksCache": ((13, 55), 130, {"warding_charm": 1}),
    "TreasureBox_2F_EastStudyCache": ((56, 24), 150, {"smoke_bomb": 1}),
    "TreasureBox_2F_SouthShortcutCache": ((30, 56), 0, {"swift_boots": 1}),
```

Update `FLOOR2_EXTRA_ENEMY_PATROLS` expectations through the existing dict-comparison pattern by adding constants in the generator in Step 4.

- [ ] **Step 3: Run tests and verify RED**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator.Floor1MazeGeneratorTest.test_floor1_treasure_boxes_are_authored_and_walkable tests.tools.test_floor1_maze_generator.Floor1MazeGeneratorTest.test_places_enemy_gates_and_no_npcs tests.tools.test_floor1_maze_generator.Floor2MazeGeneratorTest.test_treasure_boxes_are_authored_and_walkable tests.tools.test_floor1_maze_generator.Floor2MazeGeneratorTest.test_places_moderate_enemy_set_and_no_npcs -v
```

Expected: FAIL because the generator does not yet emit the new treasure boxes and enemies.

- [ ] **Step 4: Add 1F generated content**

In `tools/floor1_maze_generator.py`, extend `FLOOR1_EXTRA_ENEMY_PATROLS`:

```python
    "EnemySpawn_Goblin_SideRoom": {"position": (18, 22), "enemy_type": "goblin"},
    "EnemySpawn_Orc_SouthBend": {"position": (35, 54), "enemy_type": "orc"},
    "EnemySpawn_Skeleton_CentralSpur": {"position": (38, 39), "enemy_type": "skeleton_warrior"},
```

Extend `FLOOR1_TREASURE_BOXES`:

```python
    "TreasureBox_1F_WestCrossingCache": ((5, 37), 55, {"health_potion": 1}),
    "TreasureBox_1F_NorthSpurCache": ((28, 20), 0, {"mana_potion": 1}),
    "TreasureBox_1F_CentralSpurCache": ((43, 34), 95, {"iron_skin": 1}),
    "TreasureBox_1F_SouthShortcutPocket": ((26, 56), 0, {"antidote": 1}),
```

- [ ] **Step 5: Add 2F generated content**

In `tools/floor1_maze_generator.py`, extend `FLOOR2_EXTRA_ENEMY_PATROLS`:

```python
    "EnemySpawn_2F_NorthStacks": {"position": (18, 28), "enemy_type": "bone_archer"},
    "EnemySpawn_2F_PuzzleSide": {"position": (24, 38), "enemy_type": "cave_spider"},
    "EnemySpawn_2F_WestReadingRoom": {"position": (30, 24), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_2F_EastStacks": {"position": (40, 40), "enemy_type": "iron_revenant"},
    "EnemySpawn_2F_SouthShortcut": {"position": (41, 44), "enemy_type": "grave_hexer"},
    "EnemySpawn_2F_EastDeadEnd": {"position": (44, 34), "enemy_type": "cursed_gargoyle"},
    "EnemySpawn_2F_UpperAlcove": {"position": (48, 24), "enemy_type": "bone_archer"},
    "EnemySpawn_2F_LowerWatch": {"position": (55, 46), "enemy_type": "iron_revenant"},
```

Extend `FLOOR2_TREASURE_BOXES`:

```python
    "TreasureBox_2F_WestArchiveCache": ((4, 32), 120, {"major_health_potion": 1}),
    "TreasureBox_2F_NorthLandingCache": ((18, 4), 0, {"major_mana_potion": 1}),
    "TreasureBox_2F_SouthStacksCache": ((13, 55), 130, {"warding_charm": 1}),
    "TreasureBox_2F_EastStudyCache": ((56, 24), 150, {"smoke_bomb": 1}),
    "TreasureBox_2F_SouthShortcutCache": ((30, 56), 0, {"swift_boots": 1}),
```

- [ ] **Step 6: Add selected 2F shortcut cuts**

In `build_floor2_walls()`, after `wall_relief_paths`, add:

```python
    shortcut_loop_paths = [
        ((13, 55), (30, 56)),
        ((18, 28), (24, 38)),
        ((35, 44), (41, 44)),
        ((44, 34), (48, 24)),
    ]
    for start, end in shortcut_loop_paths:
        builder.carve_path(start, end, half_width=0)
```

These cuts convert several long empty tails into return routes while the newly placed content rewards the remaining dead ends.

- [ ] **Step 7: Run generator tests and iterate until GREEN**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: PASS. If `validate_model()` reports overlap or unreachable entities, move the conflicting entity to the nearest walkable cell on the same branch and update the matching expected dict in `tests/tools/test_floor1_maze_generator.py`.

- [ ] **Step 8: Commit generator content**

```bash
git add tools/floor1_maze_generator.py tests/tools/test_floor1_maze_generator.py
git commit -m "feat: reward floor branch exploration"
```

## Task 3: Regenerate and Import Floor Scenes

**Files:**
- Modify: `scenes/game/floors/Floor1F.json`
- Modify: `scenes/game/floors/Floor1F.tscn`
- Modify: `scenes/game/floors/Floor2F.json`
- Modify: `scenes/game/floors/Floor2F.tscn`
- Modify: `scenes/game/floors/Floor3F.json`
- Modify: `scenes/game/floors/Floor3F.tscn`
- Modify if changed: `resources/floors/Floor1F.tres`
- Modify if changed: `resources/floors/Floor2F.tres`
- Modify if changed: `resources/floors/Floor3F.tres`
- Modify: `tests/game/Floor1FMazeLayoutTest.cs`
- Modify: `tests/game/Floor2FMazeLayoutTest.cs`

- [ ] **Step 1: Generate JSON and resources**

Run:

```bash
python3 tools/floor1_maze_generator.py
```

Expected: exit 0 and output mentioning generated Floor 1, Floor 2, and Floor 3.

- [ ] **Step 2: Import generated JSON into scenes**

Run:

```bash
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor2F.json scenes/game/floors/Floor2F.tscn
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor3F.json scenes/game/floors/Floor3F.tscn
```

Expected: all three imports exit 0.

- [ ] **Step 3: Check scene/resource UIDs**

Run:

```bash
rg -n '^\[gd_scene|^\[gd_resource|uid=' scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor2F.tscn scenes/game/floors/Floor3F.tscn resources/floors/Floor1F.tres resources/floors/Floor2F.tres resources/floors/Floor3F.tres
```

Expected: `Floor1F.tscn`, `Floor2F.tscn`, `Floor3F.tscn`, and all three `.tres` resources still include their `uid=` fields.

- [ ] **Step 4: Update GdUnit expected 1F content**

In `tests/game/Floor1FMazeLayoutTest.cs`, update `ExpectedEnemyTypes` with:

```csharp
        ["EnemySpawn_Goblin_SideRoom"] = "goblin",
        ["EnemySpawn_Orc_SouthBend"] = "orc",
        ["EnemySpawn_Skeleton_CentralSpur"] = "skeleton_warrior",
```

Update `ExpectedTreasureBoxes` with:

```csharp
        ["TreasureBox_1F_WestCrossingCache"] = (new Vector2I(5, 37), 55, new Dictionary<string, int> { ["health_potion"] = 1 }),
        ["TreasureBox_1F_NorthSpurCache"] = (new Vector2I(28, 20), 0, new Dictionary<string, int> { ["mana_potion"] = 1 }),
        ["TreasureBox_1F_CentralSpurCache"] = (new Vector2I(43, 34), 95, new Dictionary<string, int> { ["iron_skin"] = 1 }),
        ["TreasureBox_1F_SouthShortcutPocket"] = (new Vector2I(26, 56), 0, new Dictionary<string, int> { ["antidote"] = 1 }),
```

- [ ] **Step 5: Update GdUnit expected 2F content**

In `tests/game/Floor2FMazeLayoutTest.cs`, update `ExpectedEnemyTypes` with:

```csharp
        ["EnemySpawn_2F_NorthStacks"] = "bone_archer",
        ["EnemySpawn_2F_PuzzleSide"] = "cave_spider",
        ["EnemySpawn_2F_WestReadingRoom"] = "skeleton_warrior",
        ["EnemySpawn_2F_EastStacks"] = "iron_revenant",
        ["EnemySpawn_2F_SouthShortcut"] = "grave_hexer",
        ["EnemySpawn_2F_EastDeadEnd"] = "cursed_gargoyle",
        ["EnemySpawn_2F_UpperAlcove"] = "bone_archer",
        ["EnemySpawn_2F_LowerWatch"] = "iron_revenant",
```

Update `ExpectedTreasureBoxes` with:

```csharp
        ["TreasureBox_2F_WestArchiveCache"] = (new Vector2I(4, 32), 120, new Dictionary<string, int> { ["major_health_potion"] = 1 }),
        ["TreasureBox_2F_NorthLandingCache"] = (new Vector2I(18, 4), 0, new Dictionary<string, int> { ["major_mana_potion"] = 1 }),
        ["TreasureBox_2F_SouthStacksCache"] = (new Vector2I(13, 55), 130, new Dictionary<string, int> { ["warding_charm"] = 1 }),
        ["TreasureBox_2F_EastStudyCache"] = (new Vector2I(56, 24), 150, new Dictionary<string, int> { ["smoke_bomb"] = 1 }),
        ["TreasureBox_2F_SouthShortcutCache"] = (new Vector2I(30, 56), 0, new Dictionary<string, int> { ["swift_boots"] = 1 }),
```

- [ ] **Step 6: Run focused scene tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor1FMazeLayoutTest|FullyQualifiedName~Floor2FMazeLayoutTest"
```

Expected: PASS. If a scene-level position differs from generator expectations, inspect the imported `.tscn` node and fix the generator/import path rather than hand-editing the scene.

- [ ] **Step 7: Commit regenerated floor artifacts**

```bash
git add tools/floor1_maze_generator.py tests/tools/test_floor1_maze_generator.py tests/game/Floor1FMazeLayoutTest.cs tests/game/Floor2FMazeLayoutTest.cs scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor2F.json scenes/game/floors/Floor2F.tscn scenes/game/floors/Floor3F.json scenes/game/floors/Floor3F.tscn resources/floors/Floor1F.tres resources/floors/Floor2F.tres resources/floors/Floor3F.tres
git commit -m "feat: regenerate rewarded floor layouts"
```

## Task 4: Make Stairs Immediate

**Files:**
- Modify: `tests/game/PlayerControllerTest.cs`
- Modify: `tests/game/FloorGFMazeLayoutTest.cs`
- Modify: `scripts/game/PlayerController.cs`

- [ ] **Step 1: Replace stale pending-stair unit tests**

In `tests/game/PlayerControllerTest.cs`, delete these tests because the pending stair interaction state is no longer the stair contract:

```text
UnhandledInput_PendingStairTransitionWithInvalidState_ClearsPendingTransition
QueueStairTransition_SetsPendingStateAndTargetMetadata
ClearPendingStairTransition_ResetsAllStairState
UnhandledInput_PendingStairTransitionWithValidState_ClearsPendingAndCallsTransition
Interact_ValidPendingStairTransitionDoesNotAlsoRequestTreasureOpen
Interact_PrioritizesStairsThenTreasureThenPuzzleInteraction
QueueStairTransition_OnlyQueuesDoesNotAutoTransition
Interact_WhenNotPendingTriggersStairReCheck
Interact_ReCheckQueuesThenTransitionsInOnePress
UnhandledInput_StairTransitionRequiresInteractReleaseBeforeNextTransition
```

Keep `FacingDirection_DefaultsDownAndUpdatesOnMovementInput`, `CreateInteractEvent`, `CreateInteractReleaseEvent`, and reflection helpers if other remaining tests use them.

- [ ] **Step 2: Add an interact regression test without stair gating**

Add this test to `tests/game/PlayerControllerTest.cs`:

```csharp
    [TestCase]
    public void InteractWithoutPendingStairGateStillRequestsTreasureOpen()
    {
        var controller = new PlayerController();
        var gameManager = new GameManager();
        var gridMap = new GridMap();
        var grid = new int[gridMap.GridWidth, gridMap.GridHeight];
        int treasureOpenRequests = 0;

        grid[0, 1] = (int)GridMap.CellType.TreasureBox;
        gridMap.TreasureBoxOpenRequested += _ => treasureOpenRequests++;

        SetPrivateField(controller, "_gameManager", gameManager);
        SetPrivateField(controller, "_gridMap", gridMap);
        SetPrivateField(gridMap, "_grid", grid);
        SetPrivateField(gridMap, "_playerPosition", Vector2I.Zero);
        SetPrivateField(controller, "_lastFacingDirection", Vector2I.Down);

        controller._UnhandledInput(CreateInteractEvent());

        AssertThat(treasureOpenRequests).IsEqual(1);

        gridMap.Free();
        gameManager.Free();
        controller.Free();
    }
```

This test is intentionally narrow: it proves removing stair `interact` state does not break existing interact-driven treasure behavior. Full floor transitions are covered by scene tests where real `GridMap`/`FloorManager` instances exist.

- [ ] **Step 3: Replace GF runtime test with step-on behavior**

In `tests/game/FloorGFMazeLayoutTest.cs`, rename `Game_InteractImmediatelyAfterMovingOntoFloorGFStairLoadsFloor1` to:

```csharp
    public async Task Game_MovingOntoFloorGFStairImmediatelyLoadsFloor1()
```

Remove this line:

```csharp
            PressInteract(playerController);
```

Remove the `PressInteract` helper if no longer used in that file.

- [ ] **Step 4: Run stair tests and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~FloorGFMazeLayoutTest"
```

Expected: FAIL. GF step-on test should still be on floor 0 because current code waits for `interact`.

- [ ] **Step 5: Implement immediate stair transition**

In `scripts/game/PlayerController.cs`, remove these fields:

```csharp
    private bool _pendingStairTransition = false;
    private int _targetFloor = -1;
    private bool _isGoingUp = false;
    private int _targetStairIndex = -1;
    private bool _awaitingStairInteractRelease = false;
```

Replace the `interact` branch with treasure/puzzle only:

```csharp
        if (@event.IsActionPressed("interact"))
        {
            if (_gridMap != null && _gridMap.TryRequestTreasureBoxOpen(_lastFacingDirection))
            {
                return;
            }

            if (_gridMap != null && _gridMap.TryRequestPuzzleInteraction(_lastFacingDirection))
            {
                return;
            }
            return;
        }
```

After successful movement, replace `CheckForStairs();` with:

```csharp
                    TransitionIfOnStairs();
```

Replace `CheckForStairs`, `QueueStairTransition`, and `ClearPendingStairTransition` with:

```csharp
    private bool TransitionIfOnStairs()
    {
        if (_gridMap == null || _floorManager == null)
        {
            GD.Print("⚠️ TransitionIfOnStairs: GridMap or FloorManager is null");
            return false;
        }

        Vector2I playerPos = _gridMap.GetPlayerPosition();
        GD.Print($"🔍 TransitionIfOnStairs: Player at grid position {playerPos}");

        if (!_gridMap.IsOnStairs(playerPos))
        {
            return false;
        }

        bool hasStair = _floorManager.IsOnStairs(playerPos, out bool isUp, out int targetFloor, out int stairIndex);
        GD.Print($"🔍 FloorManager.IsOnStairs({playerPos}): {hasStair}, isUp: {isUp}, targetFloor: {targetFloor}, stairIndex: {stairIndex}");

        if (!hasStair)
        {
            return false;
        }

        GD.Print($"Taking stairs {(isUp ? "up" : "down")} to floor {targetFloor}");
        _floorManager.TransitionToFloor(targetFloor, isUp, stairIndex);
        return true;
    }
```

Remove release handling for `_awaitingStairInteractRelease`.

- [ ] **Step 6: Run stair tests and verify GREEN**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~FloorGFMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 7: Commit immediate stair behavior**

```bash
git add scripts/game/PlayerController.cs tests/game/PlayerControllerTest.cs tests/game/FloorGFMazeLayoutTest.cs
git commit -m "feat: transition stairs on step"
```

## Task 5: Add Runtime Stair Pair Coverage

**Files:**
- Modify: `tests/game/Floor2FMazeLayoutTest.cs`

- [ ] **Step 1: Add shared runtime helpers to `Floor2FMazeLayoutTest`**

Add these methods inside `Floor2FMazeLayoutTest` before the existing static helper block:

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

    private static Key DirectionToKey(Vector2I direction)
    {
        if (direction == Vector2I.Right) return Key.Right;
        if (direction == Vector2I.Left) return Key.Left;
        if (direction == Vector2I.Up) return Key.Up;
        if (direction == Vector2I.Down) return Key.Down;
        throw new System.ArgumentException($"Unsupported movement direction {direction}");
    }

    private static Vector2I FindWalkableNeighbor(GridMap gridMap, Vector2I target, out Vector2I moveDirection)
    {
        var walls = GetWalls(gridMap);
        var candidates = new[]
        {
            (Position: target + Vector2I.Left, Direction: Vector2I.Right),
            (Position: target + Vector2I.Right, Direction: Vector2I.Left),
            (Position: target + Vector2I.Up, Direction: Vector2I.Down),
            (Position: target + Vector2I.Down, Direction: Vector2I.Up)
        };

        foreach (var candidate in candidates)
        {
            if (IsWalkable(candidate.Position, walls))
            {
                moveDirection = candidate.Direction;
                return candidate.Position;
            }
        }

        throw new System.InvalidOperationException($"No walkable neighbor found for {target}");
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }
```

- [ ] **Step 2: Add 1F to 2F stair tests**

Add two async tests to `Floor2FMazeLayoutTest`:

```csharp
    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor1NorthStairImmediatelyLoadsFloor2A()
    {
        await AssertStepOnStairTransition(1, new Vector2I(49, 12), 2, DownStairA);
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor1SouthStairImmediatelyLoadsFloor2B()
    {
        await AssertStepOnStairTransition(1, new Vector2I(48, 48), 2, DownStairB);
    }
```

- [ ] **Step 3: Add 2F/3F stair tests**

Add:

```csharp
    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor2DownStairAImmediatelyLoadsFloor1A()
    {
        await AssertStepOnStairTransition(2, DownStairA, 1, new Vector2I(49, 12));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor2DownStairBImmediatelyLoadsFloor1B()
    {
        await AssertStepOnStairTransition(2, DownStairB, 1, new Vector2I(48, 48));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor2UpStairImmediatelyLoadsFloor3()
    {
        await AssertStepOnStairTransition(2, UpStair, 3, new Vector2I(10, 10));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor3DownStairImmediatelyLoadsFloor2()
    {
        await AssertStepOnStairTransition(3, new Vector2I(10, 10), 2, UpStair);
    }
```

- [ ] **Step 4: Add the shared assertion helper**

Add this helper below the tests:

```csharp
    private static async System.Threading.Tasks.Task AssertStepOnStairTransition(
        int sourceFloor,
        Vector2I sourceStair,
        int expectedFloor,
        Vector2I expectedSpawn)
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var packed = GD.Load<PackedScene>("res://scenes/game/Game.tscn");
        AssertThat(packed).IsNotNull();

        SaveData? previousPendingLoad = SaveManager.Instance?.PendingLoadData;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.PendingLoadData = null;
        }

        var game = packed!.Instantiate<Game>();

        try
        {
            sceneTree.Root.AddChild(game);
            await AwaitFrames(sceneTree, 4);

            var floorManager = game.GetNode<FloorManager>("FloorManager");
            floorManager.LoadFloor(sourceFloor);
            await AwaitFrames(sceneTree, 8);

            var playerController = game.GetNode<PlayerController>("PlayerController");
            var gridMap = floorManager.CurrentGridMap;
            var start = FindWalkableNeighbor(gridMap, sourceStair, out Vector2I moveDirection);

            SetPrivateField(gridMap, "_playerPosition", start);
            PressMovement(playerController, moveDirection);

            await AwaitFrames(sceneTree, 12);

            AssertThat(floorManager.CurrentFloorIndex).IsEqual(expectedFloor);
            AssertThat(floorManager.CurrentGridMap.GetPlayerPosition()).IsEqual(expectedSpawn);
        }
        finally
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.PendingLoadData = previousPendingLoad;
            }

            if (IsInstanceValid(game))
            {
                game.QueueFree();
                await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
            }
        }
    }
```

- [ ] **Step 5: Run runtime stair tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor2FMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 6: Commit stair pair coverage**

```bash
git add tests/game/Floor2FMazeLayoutTest.cs
git commit -m "test: cover immediate stair pairs"
```

## Task 6: Add Missing Sprite Assets and Asset Tests

**Files:**
- Create/modify: `assets/sprites/enemies/forest_spirit/sprite_sheet.png`
- Create: `assets/sprites/enemies/orc/sprite_sheet.png`
- Create: `assets/sprites/enemies/skeleton_warrior/sprite_sheet.png`
- Create: `assets/sprites/enemies/cave_spider/sprite_sheet.png`
- Create: `assets/sprites/npcs/shopkeeper/sprite_sheet.png`
- Create: `assets/sprites/npcs/healer/sprite_sheet.png`
- Modify: `tools/sprite_sheet_merger.py`
- Modify: `docs/enemies/ENEMY_SPRITES.md`
- Create: `docs/npcs/NPC_SPRITES.md`
- Create: `tests/tools/test_sprite_asset_coverage.py`
- Create: `tests/tools/test_sprite_sheet_merger.py`

- [ ] **Step 1: Add failing sprite coverage tests**

Create `tests/tools/test_sprite_asset_coverage.py`:

```python
import unittest
from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]


EXPECTED_SHEETS = [
    ROOT / "assets/sprites/enemies/forest_spirit/sprite_sheet.png",
    ROOT / "assets/sprites/enemies/orc/sprite_sheet.png",
    ROOT / "assets/sprites/enemies/skeleton_warrior/sprite_sheet.png",
    ROOT / "assets/sprites/enemies/cave_spider/sprite_sheet.png",
    ROOT / "assets/sprites/npcs/shopkeeper/sprite_sheet.png",
    ROOT / "assets/sprites/npcs/healer/sprite_sheet.png",
]


class SpriteAssetCoverageTest(unittest.TestCase):
    def test_required_runtime_sprite_sheets_exist_with_expected_dimensions(self):
        missing = [str(path.relative_to(ROOT)) for path in EXPECTED_SHEETS if not path.exists()]
        self.assertEqual(missing, [])

        for path in EXPECTED_SHEETS:
            with self.subTest(path=path.relative_to(ROOT)):
                with Image.open(path) as image:
                    self.assertEqual(image.size, (384, 96))
                    self.assertEqual(image.mode, "RGBA")


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run asset test and verify RED**

Run:

```bash
python3 -m unittest tests.tools.test_sprite_asset_coverage -v
```

Expected: FAIL listing the missing runtime sheets.

- [ ] **Step 3: Migrate forest spirit**

Create the canonical enemy directory and copy the existing runtime sheet:

```bash
mkdir -p assets/sprites/enemies/forest_spirit
cp assets/sprites/characters/forest_spirit/sprite_sheet.png assets/sprites/enemies/forest_spirit/sprite_sheet.png
```

Do not remove the legacy file in this task.

- [ ] **Step 4: Generate missing enemy sprite frames**

Use `manage-asset-generation` and the prompts already in `docs/enemies/ENEMY_SPRITES.md` for:

```text
orc
skeleton_warrior
cave_spider
```

Save frame outputs as:

```text
assets/sprites/enemies/orc/frames/frame1.png
assets/sprites/enemies/orc/frames/frame2.png
assets/sprites/enemies/orc/frames/frame3.png
assets/sprites/enemies/orc/frames/frame4.png
assets/sprites/enemies/skeleton_warrior/frames/frame1.png
assets/sprites/enemies/skeleton_warrior/frames/frame2.png
assets/sprites/enemies/skeleton_warrior/frames/frame3.png
assets/sprites/enemies/skeleton_warrior/frames/frame4.png
assets/sprites/enemies/cave_spider/frames/frame1.png
assets/sprites/enemies/cave_spider/frames/frame2.png
assets/sprites/enemies/cave_spider/frames/frame3.png
assets/sprites/enemies/cave_spider/frames/frame4.png
```

Run:

```bash
python3 tools/sprite_sheet_merger.py
```

Expected: creates `384x96` sheets for the three enemy types.

- [ ] **Step 5: Add failing NPC support test for the sheet merger**

Create `tests/tools/test_sprite_sheet_merger.py`:

```python
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.sprite_sheet_merger import SpriteSheetMerger


class SpriteSheetMergerTest(unittest.TestCase):
    def test_merge_all_discovers_npc_frame_directories(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            frame_dir = root / "assets/sprites/npcs/shopkeeper/frames"
            frame_dir.mkdir(parents=True)

            colors = (
                (255, 0, 0, 255),
                (0, 255, 0, 255),
                (0, 0, 255, 255),
                (255, 255, 0, 255),
            )
            for index, color in enumerate(colors, start=1):
                Image.new("RGBA", (96, 96), color).save(frame_dir / f"frame{index}.png")

            merger = SpriteSheetMerger(root)
            merger.merge_all()

            output_path = root / "assets/sprites/npcs/shopkeeper/sprite_sheet.png"
            self.assertTrue(output_path.exists())
            with Image.open(output_path) as image:
                self.assertEqual(image.size, (384, 96))
                self.assertEqual(image.mode, "RGBA")


if __name__ == "__main__":
    unittest.main()
```

Run:

```bash
python3 -m unittest tests.tools.test_sprite_sheet_merger -v
```

Expected: FAIL because `tools/sprite_sheet_merger.py` does not scan `assets/sprites/npcs`.

- [ ] **Step 6: Implement NPC support in the sheet merger**

In `tools/sprite_sheet_merger.py`, update the docstring discovery line from:

```text
2. Look for frame files in assets/sprites/characters/*/, assets/sprites/enemies/*/, and assets/sprites/terrain/*/
```

to:

```text
2. Look for frame files in assets/sprites/characters/*/, assets/sprites/enemies/*/, assets/sprites/npcs/*/, and assets/sprites/terrain/*/
```

Add the NPC entry to `self.sprite_dirs`:

```python
        self.sprite_dirs = {
            "characters": self.project_root / "assets" / "sprites" / "characters",
            "enemies": self.project_root / "assets" / "sprites" / "enemies",
            "npcs": self.project_root / "assets" / "sprites" / "npcs",
            "terrain": self.project_root / "assets" / "sprites" / "terrain",
        }
```

Run:

```bash
python3 -m unittest tests.tools.test_sprite_sheet_merger -v
```

Expected: PASS.

- [ ] **Step 7: Generate missing NPC sprite frames**

Use the same chibi top-down 4-frame convention with these prompts:

```text
Shopkeeper frame 1: Create a 96x96 anime-style chibi sprite of a friendly village shopkeeper in top-down view, facing downward toward camera. Warm brown hair, green vest, cream shirt, small satchel, merchant charm, bright cel-shaded colors, bold black outline, transparent background.
Shopkeeper frame 2: Same shopkeeper, left foot stepping forward, satchel and sleeves moving with a small walking bounce, transparent background.
Shopkeeper frame 3: Same shopkeeper, idle return pose with slight arm and satchel variation, transparent background.
Shopkeeper frame 4: Same shopkeeper, right foot stepping forward, mirrored walking bounce, transparent background.

Healer frame 1: Create a 96x96 anime-style chibi sprite of a gentle village healer in top-down view, facing downward toward camera. White and pale-blue robe, small gold trim, short staff or charm, calm expression, bright cel-shaded colors, bold black outline, transparent background.
Healer frame 2: Same healer, left foot stepping forward, robe and charm swaying softly, transparent background.
Healer frame 3: Same healer, idle return pose with subtle robe variation, transparent background.
Healer frame 4: Same healer, right foot stepping forward, mirrored gentle walking motion, transparent background.
```

Save frames under:

```text
assets/sprites/npcs/shopkeeper/frames/frame1.png
assets/sprites/npcs/shopkeeper/frames/frame2.png
assets/sprites/npcs/shopkeeper/frames/frame3.png
assets/sprites/npcs/shopkeeper/frames/frame4.png
assets/sprites/npcs/healer/frames/frame1.png
assets/sprites/npcs/healer/frames/frame2.png
assets/sprites/npcs/healer/frames/frame3.png
assets/sprites/npcs/healer/frames/frame4.png
```

Run:

```bash
python3 tools/sprite_sheet_merger.py
```

Expected: creates `384x96` NPC sheets at `assets/sprites/npcs/shopkeeper/sprite_sheet.png` and `assets/sprites/npcs/healer/sprite_sheet.png`.

- [ ] **Step 8: Update enemy docs**

In `docs/enemies/ENEMY_SPRITES.md`, change these checklist rows:

```markdown
| ✅ exists | Forest Spirit | `assets/sprites/enemies/forest_spirit/sprite_sheet.png` |
| ✅ exists | Orc | `assets/sprites/enemies/orc/sprite_sheet.png` |
| ✅ exists | Skeleton Warrior | `assets/sprites/enemies/skeleton_warrior/sprite_sheet.png` |
| ✅ exists | Cave Spider | `assets/sprites/enemies/cave_spider/sprite_sheet.png` |
```

Also update the Forest Spirit note to say the legacy sheet was copied to the canonical enemy path and remains as a legacy reference.

- [ ] **Step 9: Add NPC sprite docs**

Create `docs/npcs/NPC_SPRITES.md`:

```markdown
# NPC Sprite Guide

NPC runtime sheets use `assets/sprites/npcs/{sprite_type}/sprite_sheet.png`.
`NpcSpawn.cs` falls back to `assets/sprites/characters/npc_{sprite_type}/sprite_sheet.png`, but the canonical path is the `npcs/` folder.

Runtime sheet size: `384x96` (4 frames, each `96x96`).

| Status | NPC | Runtime Sheet |
|--------|-----|---------------|
| ✅ exists | Shopkeeper | `assets/sprites/npcs/shopkeeper/sprite_sheet.png` |
| ✅ exists | Healer | `assets/sprites/npcs/healer/sprite_sheet.png` |
| ❌ missing | Villager | `assets/sprites/npcs/villager/sprite_sheet.png` |
| ❌ missing | Blacksmith | `assets/sprites/npcs/blacksmith/sprite_sheet.png` |
```

- [ ] **Step 10: Run asset tests and verify GREEN**

Run:

```bash
python3 -m unittest tests.tools.test_sprite_asset_coverage -v
python3 -m unittest tests.tools.test_sprite_sheet_merger -v
```

Expected: PASS.

- [ ] **Step 11: Commit assets and docs**

```bash
git add assets/sprites/enemies/forest_spirit assets/sprites/enemies/orc assets/sprites/enemies/skeleton_warrior assets/sprites/enemies/cave_spider assets/sprites/npcs/shopkeeper assets/sprites/npcs/healer tools/sprite_sheet_merger.py docs/enemies/ENEMY_SPRITES.md docs/npcs/NPC_SPRITES.md tests/tools/test_sprite_asset_coverage.py tests/tools/test_sprite_sheet_merger.py
git commit -m "feat: add early floor sprite coverage"
```

## Task 7: Final Verification

**Files:**
- No planned edits unless verification exposes an issue.

- [ ] **Step 1: Run generator tests**

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 2: Run asset tests**

```bash
python3 -m unittest tests.tools.test_sprite_asset_coverage -v
python3 -m unittest tests.tools.test_sprite_sheet_merger -v
```

Expected: PASS.

- [ ] **Step 3: Run focused GdUnit tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGFMazeLayoutTest|FullyQualifiedName~Floor1FMazeLayoutTest|FullyQualifiedName~Floor2FMazeLayoutTest|FullyQualifiedName~Floor3FPlaceholderLayoutTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~NpcSpawnTest"
```

Expected: PASS.

- [ ] **Step 4: Run build**

```bash
dotnet build Sirius.sln
```

Expected: PASS with no compilation errors.

- [ ] **Step 5: Inspect final diff**

```bash
git status --short
git diff --stat
git diff --check
```

Expected: no whitespace errors; changed files match this plan's scope.

- [ ] **Step 6: Handle final verification failures**

If final verification exposes an issue, return to the task that owns that file or behavior, add or adjust the focused test first, make the fix, rerun that task's verification, and commit through that task's commit step. Do not create a catch-all final commit or an empty commit.

## Self-Review

- Spec coverage: branch payoff, 1F density, 2F shortcuts, immediate stairs, GF/early enemy/NPC assets, docs, and verification all have tasks.
- No placeholders: every task names concrete files, code snippets, commands, and expected outcomes.
- Type consistency: plan uses existing `Vector2I`, `FloorManager`, `GridMap`, `PlayerController`, `EnemySpawn`, `TreasureBoxSpawn`, and Python model structures already present in the repo.
