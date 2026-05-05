# Floor 1 Combat-Gated Maze Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Floor 1 with a `60x60` combat-gated loop maze, add two functional 2/F stairs, and create a minimal 2/F placeholder scene.

**Architecture:** Extend the existing static floor JSON pipeline instead of adding runtime generation. Add importer support for missing stair nodes and non-dedicated enemy spawn scenes, generate Floor 1 and placeholder Floor 2 JSON/resources deterministically, import both into `.tscn` scenes, then guard the layouts with Python and GdUnit tests.

**Tech Stack:** Godot 4.5.1, C#/.NET 8, GdUnit4, Python 3 standard library, existing `tools/tilemap_json_sync.py` and `tools/refresh_tilemap.gd` import pipeline.

---

## File Structure

- Modify `scripts/tilemap_json/TilemapJsonImporter.cs`: create missing `StairConnection` nodes during JSON import and create generic `EnemySpawn` nodes when no dedicated spawn scene exists.
- Modify `tests/tilemap_json/TilemapJsonImporterTest.cs`: cover new stair creation and generic enemy spawn fallback.
- Create `tools/floor1_maze_generator.py`: deterministic model builder for Floor 1 and placeholder Floor 2, JSON writer, and `FloorDefinition` updater.
- Create `tests/tools/test_floor1_maze_generator.py`: generator tests for dimensions, stairs, hidden placeholders, enemies, connectivity, and `.tres` updates.
- Modify `tests/game/NpcSpawnTest.cs`: replace the current Floor 1 NPC expectation with a no-NPC expectation.
- Create `tests/game/Floor1FMazeLayoutTest.cs`: scene-level guardrail for Floor 1 static layers, stairs, enemies, hidden placeholders, and clearable-gate reachability.
- Create `tests/game/Floor2FPlaceholderLayoutTest.cs`: scene-level guardrail for placeholder 2/F.
- Generate `scenes/game/floors/Floor1F.json`: source JSON for the redesigned Floor 1.
- Create `scenes/game/floors/Floor2F.json`: source JSON for placeholder 2/F.
- Modify `scenes/game/floors/Floor1F.tscn`: imported Floor 1 static tilemap, stairs, enemies, and no NPCs.
- Create `scenes/game/floors/Floor2F.tscn`: placeholder floor scene with two down stairs.
- Modify `resources/floors/Floor1F.tres`: player start, one down stair, two up stairs, and return destinations.
- Create `resources/floors/Floor2F.tres`: placeholder floor definition with two down stairs.
- Modify `scenes/game/Game.tscn`: add Floor2F resource to `FloorManager.Floors`.

## Layout Constants

Use these positions consistently in generator, tests, resources, and scene nodes:

```text
Floor 1 footprint: 60x60 inside existing 160x160 grid
Floor 1 player start: (8, 30)
Floor 1 down stair to G/F: 1F_001 at (8, 30), destination GF_000
Floor 1 up stair A to 2/F: 1F_2F_A at (49, 12), destination 2F_1F_A
Floor 1 up stair B to 2/F: 1F_2F_B at (48, 48), destination 2F_1F_B

Hidden placeholder branches:
- hidden_room_north at (16, 8)
- hidden_shortcut_east at (56, 30)
- hidden_room_south at (19, 54)

Enemy gates:
- EnemySpawn_Goblin_Branch at (14, 25), enemy_type goblin
- EnemySpawn_Orc_Central at (28, 30), enemy_type orc
- EnemySpawn_Skeleton_StairA at (43, 12), enemy_type skeleton_warrior
- EnemySpawn_ForestSpirit_StairB at (42, 48), enemy_type forest_spirit
- EnemySpawn_Orc_HiddenBranch at (19, 51), enemy_type orc

Floor 2 placeholder footprint: 36x22 inside existing 160x160 grid
Floor 2 player start: (10, 10)
Floor 2 down stair A: 2F_1F_A at (10, 10), destination 1F_2F_A
Floor 2 down stair B: 2F_1F_B at (26, 10), destination 1F_2F_B
```

## Task 1: Add Importer Support For New Stair And Enemy Nodes

**Files:**
- Modify: `tests/tilemap_json/TilemapJsonImporterTest.cs`
- Modify: `scripts/tilemap_json/TilemapJsonImporter.cs`

- [ ] **Step 1: Add failing importer tests**

Append these tests to `tests/tilemap_json/TilemapJsonImporterTest.cs` before the final closing brace:

```csharp
    [TestCase]
    public void ImportToScene_CreatesMissingStairConnectionNode()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                StairConnections =
                [
                    new StairConnectionData
                    {
                        Id = "1F_2F_A",
                        Position = new Vector2IData(49, 12),
                        Direction = "up",
                        TargetFloor = 2,
                        DestinationStairId = "2F_1F_A"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("1F_2F_A")).IsTrue();

        var stair = gridMap.GetNode<StairConnection>("1F_2F_A");
        AssertThat(stair.Owner).IsEqual(sceneRoot);
        AssertThat(stair.StairId).IsEqual("1F_2F_A");
        AssertThat(stair.GridPosition).IsEqual(new Vector2I(49, 12));
        AssertThat(stair.Direction).IsEqual(StairDirection.Up);
        AssertThat(stair.TargetFloor).IsEqual(2);
        AssertThat(stair.DestinationStairId).IsEqual("2F_1F_A");
        AssertThat(stair.Position).IsEqual(new Vector2(1584, 400));

        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_CreatesGenericEnemySpawn_WhenDedicatedSceneIsMissing()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                EnemySpawns =
                [
                    new EnemySpawnData
                    {
                        Id = "EnemySpawn_Skeleton_StairA",
                        Position = new Vector2IData(43, 12),
                        EnemyType = "skeleton_warrior"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("EnemySpawn_Skeleton_StairA")).IsTrue();

        var spawn = gridMap.GetNode<EnemySpawn>("EnemySpawn_Skeleton_StairA");
        AssertThat(spawn.Owner).IsEqual(sceneRoot);
        AssertThat(spawn.GridPosition).IsEqual(new Vector2I(43, 12));
        AssertThat(spawn.EnemyType).IsEqual("skeleton_warrior");
        AssertThat(spawn.Position).IsEqual(new Vector2(1392, 400));

        sceneRoot.Free();
    }
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TilemapJsonImporterTest"
```

Expected: `ImportToScene_CreatesMissingStairConnectionNode` fails because the importer only logs missing stairs. `ImportToScene_CreatesGenericEnemySpawn_WhenDedicatedSceneIsMissing` fails because no `EnemySpawn_Skeleton_warrior.tscn` scene exists.

- [ ] **Step 3: Add helper methods in importer**

In `scripts/tilemap_json/TilemapJsonImporter.cs`, replace `CreateEnemySpawnNode` with:

```csharp
    private void CreateEnemySpawnNode(EnemySpawnData data, Node2D parent)
    {
        string scenePath = $"res://scenes/spawns/EnemySpawn_{ToPascalCase(data.EnemyType)}.tscn";
        Node instance;

        if (ResourceLoader.Exists(scenePath))
        {
            var scene = GD.Load<PackedScene>(scenePath);
            instance = scene.Instantiate();
            if (instance == null)
            {
                GD.PrintErr($"[TilemapJsonImporter] Failed to instantiate: {scenePath}");
                return;
            }
        }
        else
        {
            instance = new EnemySpawn();
            GD.Print($"[TilemapJsonImporter] Spawn scene not found for '{data.EnemyType}', created generic EnemySpawn");
        }

        instance.Name = data.Id;
        instance.Set("GridPosition", data.Position.ToVector2I());

        if (!string.IsNullOrEmpty(data.EnemyType))
        {
            instance.Set("EnemyType", data.EnemyType);
        }

        if (instance is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
            node2d.Scale = new Vector2(0.333333f, 0.333333f);
            node2d.ZIndex = 2;
        }

        parent.AddChild(instance);

        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }

        GD.Print($"[TilemapJsonImporter] Created enemy spawn: {data.Id}");
    }
```

Then replace the existing `CapitalizeFirst` helper with:

```csharp
    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        return string.Concat(
            s.Split('_')
                .Where(part => !string.IsNullOrEmpty(part))
                .Select(part => char.ToUpper(part[0]) + part.Substring(1).ToLower())
        );
    }
```

- [ ] **Step 4: Create missing stair nodes in importer**

In `ImportStairConnections`, replace the missing-stair `else` body with:

```csharp
                CreateStairConnectionNode(stairData, gridMapNode);
```

Add these methods below `UpdateStairConnectionNode`:

```csharp
    private void CreateStairConnectionNode(StairConnectionData data, Node2D parent)
    {
        var instance = new StairConnection
        {
            Name = data.Id
        };

        ConfigureStairConnectionNode(instance, data);
        parent.AddChild(instance);

        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }

        GD.Print($"[TilemapJsonImporter] Created stair connection: {data.Id}");
    }

    private void ConfigureStairConnectionNode(Node node, StairConnectionData data)
    {
        node.Set("StairId", data.Id);
        node.Set("GridPosition", data.Position.ToVector2I());

        int direction = data.Direction?.ToLower() == "down" ? 1 : 0;
        node.Set("Direction", direction);
        node.Set("TargetFloor", data.TargetFloor);
        node.Set("DestinationStairId", data.DestinationStairId ?? "");

        if (node is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
        }
    }
```

Replace the body of `UpdateStairConnectionNode` with:

```csharp
        ConfigureStairConnectionNode(node, data);
        GD.Print($"[TilemapJsonImporter] Updated stair connection: {data.Id}");
```

- [ ] **Step 5: Run importer tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TilemapJsonImporterTest"
```

Expected: PASS.

- [ ] **Step 6: Commit importer support**

Run:

```bash
git add scripts/tilemap_json/TilemapJsonImporter.cs tests/tilemap_json/TilemapJsonImporterTest.cs
git commit -m "feat: support generated stair and enemy spawn imports"
```

## Task 2: Add Floor 1 And Floor 2 Generator

**Files:**
- Create: `tools/floor1_maze_generator.py`
- Create: `tests/tools/test_floor1_maze_generator.py`

- [ ] **Step 1: Write failing generator tests**

Create `tests/tools/test_floor1_maze_generator.py`:

```python
import json
import tempfile
import unittest
from collections import deque
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from tools.floor1_maze_generator import (
    FLOOR1_DOWN_STAIR,
    FLOOR1_HEIGHT,
    FLOOR1_HIDDEN_PLACEHOLDERS,
    FLOOR1_PLAYER_START,
    FLOOR1_UP_STAIR_A,
    FLOOR1_UP_STAIR_B,
    FLOOR1_WIDTH,
    FLOOR2_DOWN_STAIR_A,
    FLOOR2_DOWN_STAIR_B,
    GRID_HEIGHT,
    GRID_WIDTH,
    build_floor1_model,
    build_floor2_model,
    update_floor_definition,
    validate_model,
)


def walkable_set(model):
    walls = {(tile["x"], tile["y"]) for tile in model["tile_layers"]["wall"]}
    return {
        (x, y)
        for y in range(GRID_HEIGHT)
        for x in range(GRID_WIDTH)
        if (x, y) not in walls
    }


def has_path(walkable, start, goal):
    queue = deque([start])
    seen = {start}

    while queue:
        current = queue.popleft()
        if current == goal:
            return True

        x, y = current
        for nxt in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if nxt in walkable and nxt not in seen:
                seen.add(nxt)
                queue.append(nxt)

    return False


class Floor1MazeGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor1_model()
        self.walkable = walkable_set(self.model)

    def test_generates_60_by_60_floor_inside_160_grid(self):
        ground = self.model["tile_layers"]["ground"]
        walls = {(tile["x"], tile["y"]) for tile in self.model["tile_layers"]["wall"]}

        self.assertEqual(FLOOR1_WIDTH, 60)
        self.assertEqual(FLOOR1_HEIGHT, 60)
        self.assertEqual(GRID_WIDTH, 160)
        self.assertEqual(GRID_HEIGHT, 160)
        self.assertEqual(len(ground), 3600)
        self.assertEqual(ground[0], {"x": 0, "y": 0, "tile": "starting_area"})
        self.assertEqual(ground[-1], {"x": 59, "y": 59, "tile": "starting_area"})

        for y in range(FLOOR1_HEIGHT, GRID_HEIGHT):
            for x in range(GRID_WIDTH):
                self.assertIn((x, y), walls)

        for y in range(FLOOR1_HEIGHT):
            for x in range(FLOOR1_WIDTH, GRID_WIDTH):
                self.assertIn((x, y), walls)

    def test_places_visible_stairs_and_no_hidden_stair_tiles(self):
        stairs = self.model["tile_layers"]["stair"]
        stair_positions = {(tile["x"], tile["y"]) for tile in stairs}

        self.assertEqual(
            stair_positions,
            {FLOOR1_DOWN_STAIR, FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B},
        )

        hidden_positions = set(FLOOR1_HIDDEN_PLACEHOLDERS.values())
        self.assertTrue(hidden_positions.isdisjoint(stair_positions))

        connections = self.model["entities"]["stair_connections"]
        self.assertEqual(
            {stair["id"] for stair in connections},
            {"1F_001", "1F_2F_A", "1F_2F_B"},
        )

    def test_places_enemy_gates_and_no_npcs(self):
        entities = self.model["entities"]

        self.assertEqual(entities["npc_spawns"], [])
        self.assertEqual(
            {enemy["id"]: enemy["enemy_type"] for enemy in entities["enemy_spawns"]},
            {
                "EnemySpawn_Goblin_Branch": "goblin",
                "EnemySpawn_Orc_Central": "orc",
                "EnemySpawn_Skeleton_StairA": "skeleton_warrior",
                "EnemySpawn_ForestSpirit_StairB": "forest_spirit",
                "EnemySpawn_Orc_HiddenBranch": "orc",
            },
        )

        for enemy in entities["enemy_spawns"]:
            pos = (enemy["position"]["x"], enemy["position"]["y"])
            self.assertIn(pos, self.walkable)
            self.assertNotIn(pos, {FLOOR1_DOWN_STAIR, FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B})

    def test_paths_exist_after_enemy_gates_are_clearable(self):
        goals = [FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B]
        goals.extend(FLOOR1_HIDDEN_PLACEHOLDERS.values())

        for goal in goals:
            with self.subTest(goal=goal):
                self.assertTrue(has_path(self.walkable, FLOOR1_PLAYER_START, goal))

    def test_model_is_json_serializable(self):
        encoded = json.dumps(self.model, sort_keys=True)
        decoded = json.loads(encoded)

        self.assertEqual(decoded["schema_version"], "1.0")
        self.assertIn("enemy_spawns", decoded["entities"])

    def test_validate_model_rejects_disconnected_walkable_island(self):
        isolated = {"x": 58, "y": 58, "tile": "generic"}
        self.model["tile_layers"]["wall"].remove(isolated)

        with self.assertRaisesRegex(ValueError, "Disconnected walkable cells"):
            validate_model(self.model, FLOOR1_WIDTH, FLOOR1_HEIGHT)

    def test_update_floor_definition_updates_floor1_arrays(self):
        source = "\n".join(
            [
                "[resource]",
                "PlayerStartPosition = Vector2i(17, 13)",
                "StairsUp = Array[Vector2i]([])",
                "StairsDown = Array[Vector2i]([Vector2i(17, 13)])",
                "StairsUpDestinations = Array[Vector2i]([])",
                "StairsDownDestinations = Array[Vector2i]([Vector2i(13, 3)])",
                "",
            ]
        )

        with tempfile.TemporaryDirectory() as tmpdir:
            floor_def = Path(tmpdir) / "Floor1F.tres"
            floor_def.write_text(source, encoding="utf-8")

            update_floor_definition(floor_def, self.model)

            updated = floor_def.read_text(encoding="utf-8")
            self.assertIn("PlayerStartPosition = Vector2i(8, 30)", updated)
            self.assertIn("StairsUp = Array[Vector2i]([Vector2i(49, 12), Vector2i(48, 48)])", updated)
            self.assertIn("StairsDown = Array[Vector2i]([Vector2i(8, 30)])", updated)
            self.assertIn("StairsUpDestinations = Array[Vector2i]([Vector2i(49, 12), Vector2i(48, 48)])", updated)
            self.assertIn("StairsDownDestinations = Array[Vector2i]([Vector2i(8, 30)])", updated)


class Floor2PlaceholderGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor2_model()
        self.walkable = walkable_set(self.model)

    def test_generates_placeholder_with_two_return_stairs(self):
        stairs = self.model["tile_layers"]["stair"]
        stair_positions = {(tile["x"], tile["y"]) for tile in stairs}

        self.assertEqual(stair_positions, {FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B})
        self.assertEqual(
            {stair["id"] for stair in self.model["entities"]["stair_connections"]},
            {"2F_1F_A", "2F_1F_B"},
        )
        self.assertEqual(self.model["entities"]["enemy_spawns"], [])
        self.assertEqual(self.model["entities"]["npc_spawns"], [])

    def test_placeholder_stairs_are_connected(self):
        self.assertTrue(has_path(self.walkable, FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B))


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run generator tests and verify failure**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: FAIL with `ModuleNotFoundError: No module named 'tools.floor1_maze_generator'`.

- [ ] **Step 3: Add generator implementation**

Create `tools/floor1_maze_generator.py` with these responsibilities:

```python
#!/usr/bin/env python3
"""Generate Floor 1 combat-gated maze and placeholder Floor 2 JSON for Sirius."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import deque
from pathlib import Path


FLOOR1_WIDTH = 60
FLOOR1_HEIGHT = 60
FLOOR2_WIDTH = 36
FLOOR2_HEIGHT = 22
GRID_WIDTH = 160
GRID_HEIGHT = 160

FLOOR1_PLAYER_START = (8, 30)
FLOOR1_DOWN_STAIR = (8, 30)
FLOOR1_UP_STAIR_A = (49, 12)
FLOOR1_UP_STAIR_B = (48, 48)

FLOOR2_PLAYER_START = (10, 10)
FLOOR2_DOWN_STAIR_A = (10, 10)
FLOOR2_DOWN_STAIR_B = (26, 10)

FLOOR1_HIDDEN_PLACEHOLDERS = {
    "hidden_room_north": (16, 8),
    "hidden_shortcut_east": (56, 30),
    "hidden_room_south": (19, 54),
}


class MazeBuilder:
    def __init__(self, width: int, height: int) -> None:
        self.width = width
        self.height = height
        self.walls = {(x, y) for y in range(height) for x in range(width)}

    def carve_cell(self, x: int, y: int) -> None:
        if 1 <= x < self.width - 1 and 1 <= y < self.height - 1:
            self.walls.discard((x, y))

    def carve_rect(self, x1: int, y1: int, x2: int, y2: int) -> None:
        left, right = sorted((x1, x2))
        top, bottom = sorted((y1, y2))
        for y in range(top, bottom + 1):
            for x in range(left, right + 1):
                self.carve_cell(x, y)

    def carve_h_corridor(self, x1: int, x2: int, y: int, half_width: int = 1) -> None:
        left, right = sorted((x1, x2))
        for x in range(left, right + 1):
            for dy in range(-half_width, half_width + 1):
                self.carve_cell(x, y + dy)

    def carve_v_corridor(self, y1: int, y2: int, x: int, half_width: int = 1) -> None:
        top, bottom = sorted((y1, y2))
        for y in range(top, bottom + 1):
            for dx in range(-half_width, half_width + 1):
                self.carve_cell(x + dx, y)

    def carve_path(self, start: tuple[int, int], end: tuple[int, int], half_width: int = 1) -> None:
        sx, sy = start
        ex, ey = end
        self.carve_h_corridor(sx, ex, sy, half_width)
        self.carve_v_corridor(sy, ey, ex, half_width)

    def carve_loop(self, points: list[tuple[int, int]], half_width: int = 1) -> None:
        for start, end in zip(points, points[1:]):
            self.carve_path(start, end, half_width)

    def reinforce_perimeter(self) -> None:
        for x in range(self.width):
            self.walls.add((x, 0))
            self.walls.add((x, self.height - 1))
        for y in range(self.height):
            self.walls.add((0, y))
            self.walls.add((self.width - 1, y))


def vector(x: int, y: int) -> dict[str, int]:
    return {"x": x, "y": y}


def outside_footprint_walls(width: int, height: int) -> set[tuple[int, int]]:
    walls: set[tuple[int, int]] = set()
    for y in range(height, GRID_HEIGHT):
        for x in range(GRID_WIDTH):
            walls.add((x, y))
    for y in range(height):
        for x in range(width, GRID_WIDTH):
            walls.add((x, y))
    return walls


def ground_tiles(width: int, height: int) -> list[dict[str, int | str]]:
    return [{"x": x, "y": y, "tile": "starting_area"} for y in range(height) for x in range(width)]


def wall_tiles(walls: set[tuple[int, int]], width: int, height: int) -> list[dict[str, int | str]]:
    all_walls = walls | outside_footprint_walls(width, height)
    return [{"x": x, "y": y, "tile": "generic"} for x, y in sorted(all_walls, key=lambda p: (p[1], p[0]))]


def build_floor1_walls() -> set[tuple[int, int]]:
    builder = MazeBuilder(FLOOR1_WIDTH, FLOOR1_HEIGHT)

    main_loop = [
        (8, 30),
        (16, 16),
        (33, 12),
        (49, 12),
        (53, 30),
        (48, 48),
        (28, 50),
        (12, 42),
        (8, 30),
    ]
    builder.carve_loop(main_loop, half_width=1)

    builder.carve_rect(5, 27, 11, 33)
    builder.carve_rect(24, 26, 34, 34)
    builder.carve_path((16, 30), (28, 30), half_width=1)

    builder.carve_rect(46, 9, 53, 15)
    builder.carve_rect(44, 45, 52, 52)

    builder.carve_rect(11, 22, 18, 27)
    builder.carve_path((16, 22), (14, 25), half_width=1)

    builder.carve_path((16, 16), FLOOR1_HIDDEN_PLACEHOLDERS["hidden_room_north"], half_width=1)
    builder.carve_path((53, 30), FLOOR1_HIDDEN_PLACEHOLDERS["hidden_shortcut_east"], half_width=1)
    builder.carve_path((28, 50), FLOOR1_HIDDEN_PLACEHOLDERS["hidden_room_south"], half_width=1)

    builder.carve_rect(13, 6, 19, 10)
    builder.carve_rect(53, 28, 58, 32)
    builder.carve_rect(16, 52, 22, 56)

    builder.reinforce_perimeter()
    return builder.walls


def build_floor2_walls() -> set[tuple[int, int]]:
    builder = MazeBuilder(FLOOR2_WIDTH, FLOOR2_HEIGHT)
    builder.carve_rect(6, 6, 30, 14)
    builder.carve_h_corridor(FLOOR2_DOWN_STAIR_A[0], FLOOR2_DOWN_STAIR_B[0], 10, half_width=1)
    builder.reinforce_perimeter()
    return builder.walls


def build_floor1_model() -> dict:
    model = {
        "schema_version": "1.0",
        "floor_metadata": {
            "floor_name": "First Floor",
            "floor_number": 1,
            "description": "A compact combat-gated loop maze with two 2/F routes.",
            "player_start": vector(*FLOOR1_PLAYER_START),
        },
        "tile_layers": {
            "ground": ground_tiles(FLOOR1_WIDTH, FLOOR1_HEIGHT),
            "wall": wall_tiles(build_floor1_walls(), FLOOR1_WIDTH, FLOOR1_HEIGHT),
            "stair": [
                {"x": FLOOR1_DOWN_STAIR[0], "y": FLOOR1_DOWN_STAIR[1], "tile": "down"},
                {"x": FLOOR1_UP_STAIR_A[0], "y": FLOOR1_UP_STAIR_A[1], "tile": "up"},
                {"x": FLOOR1_UP_STAIR_B[0], "y": FLOOR1_UP_STAIR_B[1], "tile": "up"},
            ],
        },
        "entities": {
            "enemy_spawns": [
                {"id": "EnemySpawn_Goblin_Branch", "position": vector(14, 25), "enemy_type": "goblin"},
                {"id": "EnemySpawn_Orc_Central", "position": vector(28, 30), "enemy_type": "orc"},
                {"id": "EnemySpawn_Skeleton_StairA", "position": vector(43, 12), "enemy_type": "skeleton_warrior"},
                {"id": "EnemySpawn_ForestSpirit_StairB", "position": vector(42, 48), "enemy_type": "forest_spirit"},
                {"id": "EnemySpawn_Orc_HiddenBranch", "position": vector(19, 51), "enemy_type": "orc"},
            ],
            "npc_spawns": [],
            "stair_connections": [
                {"id": "1F_001", "position": vector(*FLOOR1_DOWN_STAIR), "direction": "down", "target_floor": 0, "destination_stair_id": "GF_000"},
                {"id": "1F_2F_A", "position": vector(*FLOOR1_UP_STAIR_A), "direction": "up", "target_floor": 2, "destination_stair_id": "2F_1F_A"},
                {"id": "1F_2F_B", "position": vector(*FLOOR1_UP_STAIR_B), "direction": "up", "target_floor": 2, "destination_stair_id": "2F_1F_B"},
            ],
            "hidden_placeholders": [
                {"id": key, "position": vector(*pos)}
                for key, pos in FLOOR1_HIDDEN_PLACEHOLDERS.items()
            ],
        },
    }
    validate_model(model, FLOOR1_WIDTH, FLOOR1_HEIGHT)
    return model


def build_floor2_model() -> dict:
    model = {
        "schema_version": "1.0",
        "floor_metadata": {
            "floor_name": "Second Floor",
            "floor_number": 2,
            "description": "A safe placeholder landing for the two first-floor stair routes.",
            "player_start": vector(*FLOOR2_PLAYER_START),
        },
        "tile_layers": {
            "ground": ground_tiles(FLOOR2_WIDTH, FLOOR2_HEIGHT),
            "wall": wall_tiles(build_floor2_walls(), FLOOR2_WIDTH, FLOOR2_HEIGHT),
            "stair": [
                {"x": FLOOR2_DOWN_STAIR_A[0], "y": FLOOR2_DOWN_STAIR_A[1], "tile": "down"},
                {"x": FLOOR2_DOWN_STAIR_B[0], "y": FLOOR2_DOWN_STAIR_B[1], "tile": "down"},
            ],
        },
        "entities": {
            "enemy_spawns": [],
            "npc_spawns": [],
            "stair_connections": [
                {"id": "2F_1F_A", "position": vector(*FLOOR2_DOWN_STAIR_A), "direction": "down", "target_floor": 1, "destination_stair_id": "1F_2F_A"},
                {"id": "2F_1F_B", "position": vector(*FLOOR2_DOWN_STAIR_B), "direction": "down", "target_floor": 1, "destination_stair_id": "1F_2F_B"},
            ],
        },
    }
    validate_model(model, FLOOR2_WIDTH, FLOOR2_HEIGHT)
    return model


def walkable_cells(model: dict) -> set[tuple[int, int]]:
    walls = {(tile["x"], tile["y"]) for tile in model["tile_layers"]["wall"]}
    return {
        (x, y)
        for y in range(GRID_HEIGHT)
        for x in range(GRID_WIDTH)
        if (x, y) not in walls
    }


def connected_walkable_cells(walkable: set[tuple[int, int]], start: tuple[int, int]) -> set[tuple[int, int]]:
    queue = deque([start])
    seen = {start}
    while queue:
        x, y = queue.popleft()
        for nxt in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if nxt in walkable and nxt not in seen:
                seen.add(nxt)
                queue.append(nxt)
    return seen


def validate_model(model: dict, width: int, height: int) -> None:
    walkable = walkable_cells(model)
    start_data = model["floor_metadata"]["player_start"]
    start = (start_data["x"], start_data["y"])
    if start not in walkable:
        raise ValueError(f"Player start {start} is not walkable")

    connected = connected_walkable_cells(walkable, start)
    footprint_walkable = {
        (x, y)
        for y in range(height)
        for x in range(width)
        if (x, y) in walkable
    }
    disconnected = footprint_walkable - connected
    if disconnected:
        sample = sorted(disconnected)[:5]
        raise ValueError(f"Disconnected walkable cells: {sample}")

    goals: list[tuple[int, int]] = []
    for key in ("enemy_spawns", "npc_spawns", "stair_connections", "hidden_placeholders"):
        for entity in model["entities"].get(key, []):
            pos = entity["position"]
            goals.append((pos["x"], pos["y"]))

    for goal in goals:
        if goal not in walkable:
            raise ValueError(f"Entity position {goal} is not walkable")
        if goal not in connected:
            raise ValueError(f"No path from {start} to {goal}")


def write_json(model: dict, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(model, indent=2) + "\n", encoding="utf-8")


def update_floor_definition(path: Path, model: dict) -> None:
    text = path.read_text(encoding="utf-8")
    start = model["floor_metadata"]["player_start"]
    stairs = model["entities"]["stair_connections"]

    up = [stair for stair in stairs if stair["direction"] == "up"]
    down = [stair for stair in stairs if stair["direction"] == "down"]

    def array(values: list[dict]) -> str:
        return ", ".join(f"Vector2i({value['position']['x']}, {value['position']['y']})" for value in values)

    text, start_count = re.subn(
        r"PlayerStartPosition = Vector2i\([^)]+\)",
        f"PlayerStartPosition = Vector2i({start['x']}, {start['y']})",
        text,
    )
    text, up_count = re.subn(
        r"StairsUp = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsUp = Array[Vector2i]([{array(up)}])",
        text,
    )
    text, down_count = re.subn(
        r"StairsDown = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsDown = Array[Vector2i]([{array(down)}])",
        text,
    )
    text, up_dest_count = re.subn(
        r"StairsUpDestinations = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsUpDestinations = Array[Vector2i]([{array(up)}])",
        text,
    )
    text, down_dest_count = re.subn(
        r"StairsDownDestinations = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsDownDestinations = Array[Vector2i]([{array(down)}])",
        text,
    )

    missing = [
        name
        for name, count in (
            ("PlayerStartPosition", start_count),
            ("StairsUp", up_count),
            ("StairsDown", down_count),
            ("StairsUpDestinations", up_dest_count),
            ("StairsDownDestinations", down_dest_count),
        )
        if count != 1
    ]
    if missing:
        raise ValueError(f"Could not update required FloorDefinition fields: {', '.join(missing)}")

    path.write_text(text, encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Floor 1 maze and Floor 2 placeholder JSON.")
    parser.add_argument("--floor1-output", default="scenes/game/floors/Floor1F.json")
    parser.add_argument("--floor1-def", default="resources/floors/Floor1F.tres")
    parser.add_argument("--floor2-output", default="scenes/game/floors/Floor2F.json")
    parser.add_argument("--floor2-def", default="resources/floors/Floor2F.tres")
    parser.add_argument("--skip-floor-defs", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    floor1 = build_floor1_model()
    floor2 = build_floor2_model()

    write_json(floor1, Path(args.floor1_output))
    write_json(floor2, Path(args.floor2_output))

    if not args.skip_floor_defs:
        update_floor_definition(Path(args.floor1_def), floor1)
        update_floor_definition(Path(args.floor2_def), floor2)

    print(
        "Generated Floor 1 maze and Floor 2 placeholder: "
        f"{len(floor1['tile_layers']['wall'])} floor1 walls, "
        f"{len(floor1['entities']['enemy_spawns'])} floor1 enemies"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run generator tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 5: Commit generator**

Run:

```bash
git add tools/floor1_maze_generator.py tests/tools/test_floor1_maze_generator.py
git commit -m "feat: add floor 1 maze generator"
```

## Task 3: Create Placeholder Floor 2 Resource And Scene Shell

**Files:**
- Create: `resources/floors/Floor2F.tres`
- Create: `scenes/game/floors/Floor2F.tscn`

- [ ] **Step 1: Create Floor2F resource**

Create `resources/floors/Floor2F.tres`:

```ini
[gd_resource type="Resource" script_class="FloorDefinition" load_steps=3 format=3]

[ext_resource type="Script" uid="uid://kd6rbo3sqjd7" path="res://scripts/game/FloorDefinition.cs" id="1_1"]
[ext_resource type="PackedScene" path="res://scenes/game/floors/Floor2F.tscn" id="1_2"]

[resource]
script = ExtResource("1_1")
FloorName = "Second Floor"
FloorNumber = 2
FloorScene = ExtResource("1_2")
PlayerStartPosition = Vector2i(10, 10)
StairsUp = Array[Vector2i]([])
StairsDown = Array[Vector2i]([Vector2i(10, 10), Vector2i(26, 10)])
StairsUpDestinations = Array[Vector2i]([])
StairsDownDestinations = Array[Vector2i]([Vector2i(10, 10), Vector2i(26, 10)])
AmbientTint = Color(1, 1, 1, 1)
FloorDescription = "A safe placeholder landing for the second floor"
```

- [ ] **Step 2: Create Floor2F scene shell**

Create `scenes/game/floors/Floor2F.tscn`:

```ini
[gd_scene load_steps=6 format=4]

[ext_resource type="Script" uid="uid://dxou423ri263w" path="res://scripts/game/GridMap.cs" id="1"]
[ext_resource type="TileSet" uid="uid://1oxxalqkpebn" path="res://assets/tiles/ground_tileset.tres" id="2"]
[ext_resource type="TileSet" uid="uid://dm0fdgl52g2u4" path="res://assets/tiles/wall_tileset.tres" id="3"]
[ext_resource type="Script" uid="uid://dog8eagtv7b3n" path="res://scripts/game/PlayerDisplay.cs" id="4"]
[ext_resource type="TileSet" uid="uid://d1w3h484c5fh0" path="res://assets/tiles/stair_tileset.tres" id="5"]

[node name="Floor2F" type="Node2D"]

[node name="GridMap" type="Node2D" parent="."]
process_mode = 3
texture_filter = 1
script = ExtResource("1")
EnableDebugLogging = true

[node name="GroundLayer" type="TileMapLayer" parent="GridMap"]
scale = Vector2(0.333333, 0.333333)
tile_set = ExtResource("2")

[node name="WallLayer" type="TileMapLayer" parent="GridMap"]
z_index = 1
scale = Vector2(0.333333, 0.333333)
tile_set = ExtResource("3")

[node name="StairLayer" type="TileMapLayer" parent="GridMap"]
z_index = 2
scale = Vector2(0.333333, 0.333333)
tile_set = ExtResource("5")
metadata/_edit_lock_ = true

[node name="PlayerDisplay" type="Sprite2D" parent="GridMap"]
z_index = 3
script = ExtResource("4")
```

- [ ] **Step 3: Generate JSON and update resources**

Run:

```bash
python3 tools/floor1_maze_generator.py
```

Expected output contains `Generated Floor 1 maze and Floor 2 placeholder`.

- [ ] **Step 4: Import JSON into Floor 1 and Floor 2 scenes**

Run:

```bash
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn
python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor2F.json scenes/game/floors/Floor2F.tscn
```

Expected: both commands print `Successfully imported JSON and saved scene`. If Godot is not found, rerun with the local path:

```bash
GODOT_PATH=/Applications/Godot_mono.app/Contents/MacOS/Godot python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn
GODOT_PATH=/Applications/Godot_mono.app/Contents/MacOS/Godot python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor2F.json scenes/game/floors/Floor2F.tscn
```

- [ ] **Step 5: Check generated files**

Run:

```bash
rg -n "NpcSpawn|old_farmer|1F_2F_A|1F_2F_B|2F_1F_A|2F_1F_B|EnemySpawn_Skeleton_StairA|EnemySpawn_ForestSpirit_StairB" scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor2F.tscn resources/floors/Floor1F.tres resources/floors/Floor2F.tres
```

Expected: no `NpcSpawn` or `old_farmer` in `Floor1F.tscn`; all four 1/F to 2/F stair IDs are present in the appropriate scenes/resources; skeleton and forest spirit enemy spawns are present in `Floor1F.tscn`.

- [ ] **Step 6: Commit generated floor files**

Run:

```bash
git add scenes/game/floors/Floor1F.json scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor2F.json scenes/game/floors/Floor2F.tscn resources/floors/Floor1F.tres resources/floors/Floor2F.tres
git commit -m "feat: generate floor 1 maze and floor 2 placeholder"
```

## Task 4: Register Floor 2 In Game Scene

**Files:**
- Modify: `scenes/game/Game.tscn`

- [ ] **Step 1: Add Floor2F resource to Game scene**

In `scenes/game/Game.tscn`, increment the scene `load_steps` by one, add this resource after the existing Floor1F resource:

```ini
[ext_resource type="Resource" path="res://resources/floors/Floor2F.tres" id="13_floor_2f"]
```

Then change the `FloorManager` array from:

```ini
Floors = Array[ExtResource("4_wnrko")]([ExtResource("11_floor_gf"), ExtResource("12_floor_1f")])
```

to:

```ini
Floors = Array[ExtResource("4_wnrko")]([ExtResource("11_floor_gf"), ExtResource("12_floor_1f"), ExtResource("13_floor_2f")])
```

- [ ] **Step 2: Verify scene references**

Run:

```bash
rg -n "Floor2F|13_floor_2f|FloorManager" scenes/game/Game.tscn
```

Expected: `Floor2F.tres` is listed as an `ext_resource`, and `FloorManager.Floors` contains three resources in G/F, 1/F, 2/F order.

- [ ] **Step 3: Commit Game scene registration**

Run:

```bash
git add scenes/game/Game.tscn
git commit -m "feat: register floor 2 placeholder"
```

## Task 5: Add Scene-Level Layout Tests

**Files:**
- Create: `tests/game/Floor1FMazeLayoutTest.cs`
- Create: `tests/game/Floor2FPlaceholderLayoutTest.cs`
- Modify: `tests/game/NpcSpawnTest.cs`

- [ ] **Step 1: Replace Floor 1 NPC expectation**

In `tests/game/NpcSpawnTest.cs`, replace `Floor1F_ContainsReachableNpcSpawn_WithRegisteredNpcId` with:

```csharp
    [TestCase]
    public void Floor1F_ContainsNoNpcSpawns()
    {
        var floorScene = GD.Load<PackedScene>("res://scenes/game/floors/Floor1F.tscn");
        AssertThat(floorScene).IsNotNull();

        var floorRoot = floorScene!.Instantiate<Node2D>();

        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var npcCount = 0;

            foreach (Node child in gridMap.GetChildren())
            {
                if (child is NpcSpawn)
                {
                    npcCount++;
                }
            }

            AssertThat(npcCount).IsEqual(0);
        }
        finally
        {
            floorRoot.Free();
        }
    }
```

- [ ] **Step 2: Create Floor 1 scene layout test**

Create `tests/game/Floor1FMazeLayoutTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class Floor1FMazeLayoutTest : Node
{
    private static readonly Vector2I PlayerStart = new(8, 30);
    private static readonly Vector2I DownStair = new(8, 30);
    private static readonly Vector2I UpStairA = new(49, 12);
    private static readonly Vector2I UpStairB = new(48, 48);
    private static readonly Vector2I[] HiddenPlaceholders =
    [
        new Vector2I(16, 8),
        new Vector2I(56, 30),
        new Vector2I(19, 54)
    ];

    [TestCase]
    public void Floor1F_GeneratedMaze_HasExpectedStaticLayers()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var groundLayer = gridMap.GetNode<TileMapLayer>("GroundLayer");
            var wallLayer = gridMap.GetNode<TileMapLayer>("WallLayer");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");

            AssertThat(groundLayer.GetUsedCells().Count).IsEqual(3600);
            AssertThat(wallLayer.GetUsedCells().Count).IsGreater(22000);
            AssertThat(stairLayer.GetUsedCells().Count).IsEqual(3);
            AssertThat(stairLayer.GetUsedCells().Contains(DownStair)).IsTrue();
            AssertThat(stairLayer.GetUsedCells().Contains(UpStairA)).IsTrue();
            AssertThat(stairLayer.GetUsedCells().Contains(UpStairB)).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HasNoNpcsAndExpectedEnemyGates()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");

            AssertThat(gridMap.GetChildren().OfType<NpcSpawn>().Count()).IsEqual(0);

            var enemies = gridMap.GetChildren()
                .OfType<EnemySpawn>()
                .ToDictionary(enemy => enemy.Name.ToString(), enemy => enemy);

            AssertThat(enemies.Keys).Contains("EnemySpawn_Goblin_Branch");
            AssertThat(enemies.Keys).Contains("EnemySpawn_Orc_Central");
            AssertThat(enemies.Keys).Contains("EnemySpawn_Skeleton_StairA");
            AssertThat(enemies.Keys).Contains("EnemySpawn_ForestSpirit_StairB");
            AssertThat(enemies.Keys).Contains("EnemySpawn_Orc_HiddenBranch");

            AssertThat(enemies["EnemySpawn_Goblin_Branch"].EnemyType).IsEqual("goblin");
            AssertThat(enemies["EnemySpawn_Orc_Central"].EnemyType).IsEqual("orc");
            AssertThat(enemies["EnemySpawn_Skeleton_StairA"].EnemyType).IsEqual("skeleton_warrior");
            AssertThat(enemies["EnemySpawn_ForestSpirit_StairB"].EnemyType).IsEqual("forest_spirit");
            AssertThat(enemies["EnemySpawn_Orc_HiddenBranch"].EnemyType).IsEqual("orc");
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_StairsHaveExpectedIdsAndDestinations()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairs = gridMap.GetChildren().OfType<StairConnection>().ToDictionary(stair => stair.StairId);

            AssertThat(stairs.Keys).Contains("1F_001");
            AssertThat(stairs.Keys).Contains("1F_2F_A");
            AssertThat(stairs.Keys).Contains("1F_2F_B");

            AssertThat(stairs["1F_001"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["1F_001"].TargetFloor).IsEqual(0);
            AssertThat(stairs["1F_001"].DestinationStairId).IsEqual("GF_000");

            AssertThat(stairs["1F_2F_A"].Direction).IsEqual(StairDirection.Up);
            AssertThat(stairs["1F_2F_A"].TargetFloor).IsEqual(2);
            AssertThat(stairs["1F_2F_A"].DestinationStairId).IsEqual("2F_1F_A");

            AssertThat(stairs["1F_2F_B"].Direction).IsEqual(StairDirection.Up);
            AssertThat(stairs["1F_2F_B"].TargetFloor).IsEqual(2);
            AssertThat(stairs["1F_2F_B"].DestinationStairId).IsEqual("2F_1F_B");
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_CriticalBeatsAreReachableWhenEnemiesAreClearable()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var goals = new List<Vector2I> { UpStairA, UpStairB };
            goals.AddRange(HiddenPlaceholders);
            goals.AddRange(gridMap.GetChildren().OfType<EnemySpawn>().Select(enemy => enemy.GridPosition));

            foreach (var goal in goals)
            {
                AssertThat(IsInsideFloor(goal)).IsTrue();
                AssertThat(IsWalkable(goal, walls)).IsTrue();
                AssertThat(HasPath(PlayerStart, goal, walls)).IsTrue();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HiddenPlaceholdersAreNotVisibleStairs()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");
            var stairCells = stairLayer.GetUsedCells().ToHashSet();
            var stairNodes = gridMap.GetChildren().OfType<StairConnection>().Select(stair => stair.GridPosition).ToHashSet();

            foreach (var hidden in HiddenPlaceholders)
            {
                AssertThat(stairCells.Contains(hidden)).IsFalse();
                AssertThat(stairNodes.Contains(hidden)).IsFalse();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/Floor1F.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static HashSet<Vector2I> GetWalls(GridMap gridMap)
    {
        return gridMap.GetNode<TileMapLayer>("WallLayer").GetUsedCells().ToHashSet();
    }

    private static bool IsInsideFloor(Vector2I position)
    {
        return position.X >= 0 && position.X < 60 && position.Y >= 0 && position.Y < 60;
    }

    private static bool IsWalkable(Vector2I position, HashSet<Vector2I> walls)
    {
        return IsInsideFloor(position) && !walls.Contains(position);
    }

    private static bool HasPath(Vector2I start, Vector2I goal, HashSet<Vector2I> walls)
    {
        var queue = new Queue<Vector2I>();
        var seen = new HashSet<Vector2I> { start };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                return true;
            }

            foreach (var next in Neighbors(current))
            {
                if (!IsWalkable(next, walls) || seen.Contains(next))
                {
                    continue;
                }

                seen.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static IEnumerable<Vector2I> Neighbors(Vector2I position)
    {
        yield return new Vector2I(position.X + 1, position.Y);
        yield return new Vector2I(position.X - 1, position.Y);
        yield return new Vector2I(position.X, position.Y + 1);
        yield return new Vector2I(position.X, position.Y - 1);
    }
}
```

- [ ] **Step 3: Create Floor 2 placeholder scene test**

Create `tests/game/Floor2FPlaceholderLayoutTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class Floor2FPlaceholderLayoutTest : Node
{
    private static readonly Vector2I DownStairA = new(10, 10);
    private static readonly Vector2I DownStairB = new(26, 10);

    [TestCase]
    public void Floor2F_Placeholder_HasTwoReturnStairsAndNoContent()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");
            var stairCells = stairLayer.GetUsedCells().ToHashSet();

            AssertThat(gridMap.GetNode<TileMapLayer>("GroundLayer").GetUsedCells().Count).IsEqual(792);
            AssertThat(gridMap.GetNode<TileMapLayer>("WallLayer").GetUsedCells().Count).IsGreater(24000);
            AssertThat(stairCells.Count).IsEqual(2);
            AssertThat(stairCells.Contains(DownStairA)).IsTrue();
            AssertThat(stairCells.Contains(DownStairB)).IsTrue();
            AssertThat(gridMap.GetChildren().OfType<EnemySpawn>().Count()).IsEqual(0);
            AssertThat(gridMap.GetChildren().OfType<NpcSpawn>().Count()).IsEqual(0);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_Placeholder_StairsLinkBackToFloor1()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairs = gridMap.GetChildren().OfType<StairConnection>().ToDictionary(stair => stair.StairId);

            AssertThat(stairs.Keys).Contains("2F_1F_A");
            AssertThat(stairs.Keys).Contains("2F_1F_B");
            AssertThat(stairs["2F_1F_A"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["2F_1F_A"].TargetFloor).IsEqual(1);
            AssertThat(stairs["2F_1F_A"].DestinationStairId).IsEqual("1F_2F_A");
            AssertThat(stairs["2F_1F_B"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["2F_1F_B"].TargetFloor).IsEqual(1);
            AssertThat(stairs["2F_1F_B"].DestinationStairId).IsEqual("1F_2F_B");
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_Placeholder_ReturnStairsAreConnected()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = gridMap.GetNode<TileMapLayer>("WallLayer").GetUsedCells().ToHashSet();
            AssertThat(HasPath(DownStairA, DownStairB, walls)).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/Floor2F.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static bool IsWalkable(Vector2I position, HashSet<Vector2I> walls)
    {
        return position.X >= 0 && position.X < 36 && position.Y >= 0 && position.Y < 22 && !walls.Contains(position);
    }

    private static bool HasPath(Vector2I start, Vector2I goal, HashSet<Vector2I> walls)
    {
        var queue = new Queue<Vector2I>();
        var seen = new HashSet<Vector2I> { start };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                return true;
            }

            foreach (var next in new[]
            {
                new Vector2I(current.X + 1, current.Y),
                new Vector2I(current.X - 1, current.Y),
                new Vector2I(current.X, current.Y + 1),
                new Vector2I(current.X, current.Y - 1)
            })
            {
                if (!IsWalkable(next, walls) || seen.Contains(next))
                {
                    continue;
                }

                seen.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
    }
}
```

- [ ] **Step 4: Run scene layout tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor1FMazeLayoutTest|FullyQualifiedName~Floor2FPlaceholderLayoutTest|FullyQualifiedName~NpcSpawnTest"
```

Expected: PASS.

- [ ] **Step 5: Commit layout tests**

Run:

```bash
git add tests/game/Floor1FMazeLayoutTest.cs tests/game/Floor2FPlaceholderLayoutTest.cs tests/game/NpcSpawnTest.cs
git commit -m "test: cover floor 1 maze and floor 2 placeholder"
```

## Task 6: Final Verification

**Files:**
- No new files. Verify all changed files from previous tasks.

- [ ] **Step 1: Run Python generator tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor1_maze_generator -v
```

Expected: PASS.

- [ ] **Step 2: Run focused GdUnit tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~Floor1FMazeLayoutTest|FullyQualifiedName~Floor2FPlaceholderLayoutTest|FullyQualifiedName~NpcSpawnTest"
```

Expected: PASS.

- [ ] **Step 3: Build the solution**

Run:

```bash
dotnet build Sirius.sln
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 4: Inspect final diff**

Run:

```bash
git status --short
git diff --stat HEAD
```

Expected: only intended Floor 1, Floor 2, importer, generator, and test files are modified.

- [ ] **Step 5: Confirm every task committed cleanly**

Run:

```bash
git status --short
```

Expected: no output.
