# Floor 0 Maze Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace floor 0 with a generated 100x100 beginner-friendly District Loop maze while keeping the shipped floor as static `TileMapLayer` scene content.

**Architecture:** Add a deterministic Python floor generator that emits the existing floor JSON format, extend the Godot JSON model/importer/exporter to include NPC spawn nodes, import the generated JSON into `FloorGF.tscn`, and update `FloorGF.tres` metadata. Add focused tests around the generator and final floor connectivity.

**Tech Stack:** Godot 4.5.1, C#/.NET 8, GdUnit4, Python 3 standard library, existing `tools/refresh_tilemap.gd` JSON import pipeline.

---

## File Structure

- Create `tools/floor0_maze_generator.py`: deterministic 100x100 District Loop generator, JSON writer, floor resource metadata updater, and internal connectivity validation.
- Create `tests/tools/test_floor0_maze_generator.py`: Python unit tests for generator dimensions, walkability, entity placement, and path connectivity.
- Modify `scripts/tilemap_json/FloorJsonModel.cs`: add `NpcSpawnData` and `SceneEntities.NpcSpawns`.
- Modify `scripts/tilemap_json/TilemapJsonExporter.cs`: export `NpcSpawn` nodes into JSON.
- Modify `scripts/tilemap_json/TilemapJsonImporter.cs`: import/update/create `NpcSpawn` nodes from JSON and use centered tile positions for imported entity nodes.
- Create `tests/tilemap_json/FloorJsonModelTest.cs`: serialization coverage for `npc_spawns`.
- Modify `tools/tilemap_json_sync.py`: honor `GODOT_PATH` from the environment while preserving the current default path.
- Create `tests/game/FloorGFMazeLayoutTest.cs`: scene-level validation for the generated floor.
- Generate `scenes/game/floors/FloorGF.json`: source JSON for the generated floor.
- Modify `scenes/game/floors/FloorGF.tscn`: imported static tilemap and moved entity nodes.
- Modify `resources/floors/FloorGF.tres`: updated player start and stair metadata.

## Task 1: Add Deterministic Floor 0 Generator

**Files:**
- Create: `tools/floor0_maze_generator.py`
- Create: `tests/tools/test_floor0_maze_generator.py`

- [ ] **Step 1: Write the failing generator tests**

Create `tests/tools/test_floor0_maze_generator.py`:

```python
import json
import unittest
from collections import deque
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from tools.floor0_maze_generator import FLOOR_HEIGHT, FLOOR_WIDTH, build_floor_model


def walkable_set(model):
    walls = {(tile["x"], tile["y"]) for tile in model["tile_layers"]["wall"]}
    return {
        (x, y)
        for y in range(FLOOR_HEIGHT)
        for x in range(FLOOR_WIDTH)
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


class Floor0MazeGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor_model()
        self.walkable = walkable_set(self.model)

    def test_generates_100_by_100_ground_fill(self):
        ground = self.model["tile_layers"]["ground"]

        self.assertEqual(FLOOR_WIDTH, 100)
        self.assertEqual(FLOOR_HEIGHT, 100)
        self.assertEqual(len(ground), 10000)
        self.assertEqual(ground[0], {"x": 0, "y": 0, "tile": "starting_area"})
        self.assertEqual(ground[-1], {"x": 99, "y": 99, "tile": "starting_area"})

    def test_generates_perimeter_walls_and_internal_maze(self):
        walls = {(tile["x"], tile["y"]) for tile in self.model["tile_layers"]["wall"]}

        for x in range(FLOOR_WIDTH):
            self.assertIn((x, 0), walls)
            self.assertIn((x, FLOOR_HEIGHT - 1), walls)

        for y in range(FLOOR_HEIGHT):
            self.assertIn((0, y), walls)
            self.assertIn((FLOOR_WIDTH - 1, y), walls)

        self.assertGreater(len(walls), 6000)
        self.assertGreater(len(self.walkable), 2500)

    def test_places_required_entities_on_walkable_tiles(self):
        metadata = self.model["floor_metadata"]
        entities = self.model["entities"]
        start = (metadata["player_start"]["x"], metadata["player_start"]["y"])
        stair = entities["stair_connections"][0]["position"]
        stair_pos = (stair["x"], stair["y"])

        self.assertIn(start, self.walkable)
        self.assertIn(stair_pos, self.walkable)

        npc_ids = {npc["npc_id"] for npc in entities["npc_spawns"]}
        self.assertEqual(npc_ids, {"village_shopkeeper", "village_healer"})

        for npc in entities["npc_spawns"]:
            pos = (npc["position"]["x"], npc["position"]["y"])
            self.assertIn(pos, self.walkable)

        enemy_ids = {enemy["id"] for enemy in entities["enemy_spawns"]}
        self.assertIn("EnemySpawn_Goblin", enemy_ids)

        for enemy in entities["enemy_spawns"]:
            pos = (enemy["position"]["x"], enemy["position"]["y"])
            self.assertIn(pos, self.walkable)

    def test_paths_exist_to_critical_floor_beats(self):
        metadata = self.model["floor_metadata"]
        entities = self.model["entities"]
        start = (metadata["player_start"]["x"], metadata["player_start"]["y"])
        goals = []

        for npc in entities["npc_spawns"]:
            goals.append((npc["position"]["x"], npc["position"]["y"]))

        first_enemy = next(enemy for enemy in entities["enemy_spawns"] if enemy["id"] == "EnemySpawn_Goblin")
        goals.append((first_enemy["position"]["x"], first_enemy["position"]["y"]))

        stair = entities["stair_connections"][0]["position"]
        goals.append((stair["x"], stair["y"]))

        for goal in goals:
            with self.subTest(goal=goal):
                self.assertTrue(has_path(self.walkable, start, goal))

    def test_model_is_json_serializable(self):
        encoded = json.dumps(self.model, sort_keys=True)
        decoded = json.loads(encoded)

        self.assertEqual(decoded["schema_version"], "1.0")
        self.assertIn("npc_spawns", decoded["entities"])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
python3 -m unittest tests.tools.test_floor0_maze_generator -v
```

Expected: FAIL with `ModuleNotFoundError: No module named 'tools.floor0_maze_generator'`.

- [ ] **Step 3: Create the generator**

Create `tools/floor0_maze_generator.py`:

```python
#!/usr/bin/env python3
"""Generate the Floor 0 District Loop maze JSON for Sirius."""

from __future__ import annotations

import argparse
import json
import re
from collections import deque
from pathlib import Path


FLOOR_WIDTH = 100
FLOOR_HEIGHT = 100

PLAYER_START = (8, 50)
SHOPKEEPER_POS = (12, 46)
HEALER_POS = (12, 54)
FIRST_GOBLIN_POS = (24, 45)
STAIR_POS = (82, 68)

MAIN_LOOP_POINTS = [
    (8, 50),
    (18, 50),
    (18, 18),
    (56, 18),
    (76, 30),
    (82, 68),
    (52, 82),
    (18, 72),
    (8, 50),
]


class MazeBuilder:
    def __init__(self, width: int = FLOOR_WIDTH, height: int = FLOOR_HEIGHT) -> None:
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

    def build(self) -> set[tuple[int, int]]:
        self.carve_loop(MAIN_LOOP_POINTS, half_width=2)

        self.carve_rect(5, 42, 17, 58)    # entrance plaza
        self.carve_rect(9, 43, 15, 48)    # shop room
        self.carve_rect(9, 52, 15, 57)    # healer room
        self.carve_rect(20, 41, 29, 48)   # early combat pocket

        self.carve_rect(11, 11, 25, 24)   # north west landmark
        self.carve_rect(38, 10, 52, 24)   # north loop room
        self.carve_path((25, 18), (38, 18), half_width=1)
        self.carve_path((44, 24), (44, 36), half_width=1)
        self.carve_rect(39, 34, 50, 41)   # north shortcut room
        self.carve_path((39, 38), (20, 50), half_width=1)

        self.carve_rect(62, 24, 81, 36)   # east progression room
        self.carve_rect(70, 42, 88, 55)   # east branch room
        self.carve_path((76, 36), (79, 42), half_width=1)
        self.carve_path((70, 49), (56, 49), half_width=1)
        self.carve_path((56, 49), (56, 18), half_width=1)

        self.carve_rect(66, 63, 88, 74)   # stair district
        self.carve_rect(72, 76, 90, 88)   # south east optional room
        self.carve_path((80, 74), (80, 76), half_width=1)

        self.carve_rect(34, 74, 58, 90)   # south optional district
        self.carve_rect(14, 65, 25, 79)   # south west loop bend
        self.carve_path((34, 82), (25, 72), half_width=1)
        self.carve_path((52, 74), (52, 52), half_width=1)
        self.carve_path((52, 52), (70, 49), half_width=1)

        self.carve_dead_end_branches()
        self.reinforce_perimeter()
        return self.walls

    def carve_dead_end_branches(self) -> None:
        branches = [
            ((30, 18), (30, 8)),
            ((49, 18), (49, 8)),
            ((76, 30), (91, 30)),
            ((82, 68), (94, 68)),
            ((52, 82), (52, 94)),
            ((18, 72), (7, 72)),
            ((18, 50), (33, 50)),
        ]
        for start, end in branches:
            self.carve_path(start, end, half_width=1)

    def reinforce_perimeter(self) -> None:
        for x in range(self.width):
            self.walls.add((x, 0))
            self.walls.add((x, self.height - 1))
        for y in range(self.height):
            self.walls.add((0, y))
            self.walls.add((self.width - 1, y))


def vector(x: int, y: int) -> dict[str, int]:
    return {"x": x, "y": y}


def build_floor_model() -> dict:
    builder = MazeBuilder()
    walls = builder.build()

    model = {
        "schema_version": "1.0",
        "floor_metadata": {
            "floor_name": "Ground Floor",
            "floor_number": 0,
            "description": "A readable starter district loop with optional branches.",
            "player_start": vector(*PLAYER_START),
        },
        "tile_layers": {
            "ground": [
                {"x": x, "y": y, "tile": "starting_area"}
                for y in range(FLOOR_HEIGHT)
                for x in range(FLOOR_WIDTH)
            ],
            "wall": [
                {"x": x, "y": y, "tile": "generic"}
                for x, y in sorted(walls, key=lambda p: (p[1], p[0]))
            ],
            "stair": [{"x": STAIR_POS[0], "y": STAIR_POS[1], "tile": "up"}],
        },
        "entities": {
            "enemy_spawns": [
                {
                    "id": "EnemySpawn_Goblin",
                    "position": vector(*FIRST_GOBLIN_POS),
                    "enemy_type": "Goblin",
                },
                {
                    "id": "EnemySpawn_Goblin_North",
                    "position": vector(44, 36),
                    "enemy_type": "Goblin",
                },
                {
                    "id": "EnemySpawn_Orc_East",
                    "position": vector(74, 49),
                    "enemy_type": "Orc",
                },
                {
                    "id": "EnemySpawn_Goblin_South",
                    "position": vector(45, 82),
                    "enemy_type": "Goblin",
                },
            ],
            "npc_spawns": [
                {
                    "id": "NpcSpawn_Shopkeeper",
                    "position": vector(*SHOPKEEPER_POS),
                    "npc_id": "village_shopkeeper",
                },
                {
                    "id": "NpcSpawn_Healer",
                    "position": vector(*HEALER_POS),
                    "npc_id": "village_healer",
                },
            ],
            "stair_connections": [
                {
                    "id": "GF_000",
                    "position": vector(*STAIR_POS),
                    "direction": "up",
                    "target_floor": 1,
                    "destination_stair_id": "1F_001",
                }
            ],
        },
    }

    validate_model(model)
    return model


def walkable_cells(model: dict) -> set[tuple[int, int]]:
    walls = {(tile["x"], tile["y"]) for tile in model["tile_layers"]["wall"]}
    return {
        (x, y)
        for y in range(FLOOR_HEIGHT)
        for x in range(FLOOR_WIDTH)
        if (x, y) not in walls
    }


def has_path(walkable: set[tuple[int, int]], start: tuple[int, int], goal: tuple[int, int]) -> bool:
    queue = deque([start])
    seen = {start}
    while queue:
        x, y = queue.popleft()
        if (x, y) == goal:
            return True
        for nxt in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if nxt in walkable and nxt not in seen:
                seen.add(nxt)
                queue.append(nxt)
    return False


def validate_model(model: dict) -> None:
    walkable = walkable_cells(model)
    start = (model["floor_metadata"]["player_start"]["x"], model["floor_metadata"]["player_start"]["y"])
    if start not in walkable:
        raise ValueError(f"Player start {start} is not walkable")

    goals = []
    for npc in model["entities"]["npc_spawns"]:
        goals.append((npc["position"]["x"], npc["position"]["y"]))
    for enemy in model["entities"]["enemy_spawns"]:
        goals.append((enemy["position"]["x"], enemy["position"]["y"]))
    for stair in model["entities"]["stair_connections"]:
        goals.append((stair["position"]["x"], stair["position"]["y"]))

    for goal in goals:
        if goal not in walkable:
            raise ValueError(f"Entity position {goal} is not walkable")
        if not has_path(walkable, start, goal):
            raise ValueError(f"No path from {start} to {goal}")


def write_json(model: dict, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(model, indent=2) + "\n", encoding="utf-8")


def update_floor_definition(path: Path, model: dict) -> None:
    text = path.read_text(encoding="utf-8")
    start = model["floor_metadata"]["player_start"]
    stair = model["entities"]["stair_connections"][0]["position"]

    text = re.sub(
        r"PlayerStartPosition = Vector2i\([^)]+\)",
        f"PlayerStartPosition = Vector2i({start['x']}, {start['y']})",
        text,
    )
    text = re.sub(
        r"StairsUp = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsUp = Array[Vector2i]([Vector2i({stair['x']}, {stair['y']})])",
        text,
    )
    text = re.sub(
        r"StairsUpDestinations = Array\[Vector2i\]\(\[[^\]]*\]\)",
        "StairsUpDestinations = Array[Vector2i]([Vector2i(17, 13)])",
        text,
    )
    path.write_text(text, encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate the 100x100 Floor 0 maze JSON.")
    parser.add_argument(
        "--output",
        default="scenes/game/floors/FloorGF.json",
        help="Path for generated floor JSON.",
    )
    parser.add_argument(
        "--floor-def",
        default="resources/floors/FloorGF.tres",
        help="FloorDefinition resource to update.",
    )
    parser.add_argument(
        "--skip-floor-def",
        action="store_true",
        help="Only write JSON; do not update the FloorDefinition resource.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    model = build_floor_model()
    write_json(model, Path(args.output))
    if not args.skip_floor_def:
        update_floor_definition(Path(args.floor_def), model)
    print(
        f"Generated Floor 0 maze: {FLOOR_WIDTH}x{FLOOR_HEIGHT}, "
        f"{len(model['tile_layers']['wall'])} walls, "
        f"{len(model['entities']['enemy_spawns'])} enemies"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: Run the generator tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor0_maze_generator -v
```

Expected: PASS for all 5 tests.

- [ ] **Step 5: Commit**

```bash
git add tools/floor0_maze_generator.py tests/tools/test_floor0_maze_generator.py
git commit -m "test: cover floor 0 maze generator"
```

## Task 2: Extend Floor JSON Model For NPC Spawns

**Files:**
- Modify: `scripts/tilemap_json/FloorJsonModel.cs`
- Create: `tests/tilemap_json/FloorJsonModelTest.cs`

- [ ] **Step 1: Write failing JSON model test**

Create `tests/tilemap_json/FloorJsonModelTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorJsonModelTest : Node
{
    [TestCase]
    public void FromJson_ParsesNpcSpawns()
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
            "npc_spawns": [
              {
                "id": "NpcSpawn_Shopkeeper",
                "position": { "x": 12, "y": 46 },
                "npc_id": "village_shopkeeper"
              }
            ]
          }
        }
        """;

        var model = FloorJsonModel.FromJson(json);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.NpcSpawns.Count).IsEqual(1);
        AssertThat(model.Entities.NpcSpawns[0].Id).IsEqual("NpcSpawn_Shopkeeper");
        AssertThat(model.Entities.NpcSpawns[0].NpcId).IsEqual("village_shopkeeper");
        AssertThat(model.Entities.NpcSpawns[0].Position.ToVector2I().X).IsEqual(12);
        AssertThat(model.Entities.NpcSpawns[0].Position.ToVector2I().Y).IsEqual(46);
    }
}
```

- [ ] **Step 2: Run the model test to verify it fails**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest"
```

Expected: FAIL because `SceneEntities` has no `NpcSpawns` property.

- [ ] **Step 3: Add NPC data model**

In `scripts/tilemap_json/FloorJsonModel.cs`, update `SceneEntities` and add `NpcSpawnData` after `EnemySpawnData`:

```csharp
public class SceneEntities
{
    [JsonPropertyName("enemy_spawns")]
    public List<EnemySpawnData> EnemySpawns { get; set; } = new();

    [JsonPropertyName("npc_spawns")]
    public List<NpcSpawnData> NpcSpawns { get; set; } = new();

    [JsonPropertyName("stair_connections")]
    public List<StairConnectionData> StairConnections { get; set; } = new();
}
```

```csharp
public class NpcSpawnData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public Vector2IData Position { get; set; } = new();

    [JsonPropertyName("npc_id")]
    public string NpcId { get; set; } = "";
}
```

- [ ] **Step 4: Run the model test**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/tilemap_json/FloorJsonModel.cs tests/tilemap_json/FloorJsonModelTest.cs
git commit -m "feat: add npc spawns to floor json model"
```

## Task 3: Import And Export NPC Spawns

**Files:**
- Modify: `scripts/tilemap_json/TilemapJsonExporter.cs`
- Modify: `scripts/tilemap_json/TilemapJsonImporter.cs`

- [ ] **Step 1: Export NPC spawns**

In `scripts/tilemap_json/TilemapJsonExporter.cs`, update `ExportEntities`:

```csharp
private SceneEntities ExportEntities(Node2D gridMapNode)
{
    var entities = new SceneEntities();

    entities.EnemySpawns = ExportEnemySpawns(gridMapNode);
    entities.NpcSpawns = ExportNpcSpawns(gridMapNode);
    entities.StairConnections = ExportStairConnections(gridMapNode);

    return entities;
}
```

Add this method after `ExportEnemySpawns`:

```csharp
private List<NpcSpawnData> ExportNpcSpawns(Node2D gridMapNode)
{
    var spawns = new List<NpcSpawnData>();

    foreach (var child in gridMapNode.GetChildren())
    {
        if (child is NpcSpawn spawn)
        {
            spawns.Add(new NpcSpawnData
            {
                Id = child.Name.ToString(),
                Position = new Vector2IData(spawn.GridPosition),
                NpcId = spawn.NpcId
            });
        }
    }

    return spawns;
}
```

- [ ] **Step 2: Import NPC spawns**

In `scripts/tilemap_json/TilemapJsonImporter.cs`, update `ImportEntities`:

```csharp
private void ImportEntities(SceneEntities entities, Node2D gridMapNode)
{
    ImportEnemySpawns(entities.EnemySpawns, gridMapNode);
    ImportNpcSpawns(entities.NpcSpawns, gridMapNode);
    ImportStairConnections(entities.StairConnections, gridMapNode);
}
```

Add these methods after `CreateEnemySpawnNode`:

```csharp
private void ImportNpcSpawns(List<NpcSpawnData> spawns, Node2D gridMapNode)
{
    var existingSpawns = new Dictionary<string, Node>();
    foreach (var child in gridMapNode.GetChildren())
    {
        if (child is NpcSpawn || child.Name.ToString().Contains("NpcSpawn"))
        {
            existingSpawns[child.Name.ToString()] = child;
        }
    }

    var processedIds = new HashSet<string>();

    foreach (var spawnData in spawns)
    {
        processedIds.Add(spawnData.Id);

        if (existingSpawns.TryGetValue(spawnData.Id, out var existingNode))
        {
            UpdateNpcSpawnNode(existingNode, spawnData);
        }
        else
        {
            CreateNpcSpawnNode(spawnData, gridMapNode);
        }
    }

    foreach (var (id, node) in existingSpawns)
    {
        if (!processedIds.Contains(id))
        {
            GD.Print($"[TilemapJsonImporter] Removing NPC spawn: {id}");
            node.QueueFree();
        }
    }
}

private void UpdateNpcSpawnNode(Node node, NpcSpawnData data)
{
    node.Set("GridPosition", data.Position.ToVector2I());
    node.Set("NpcId", data.NpcId);

    if (node is Node2D node2d)
    {
        node2d.Position = ToCenteredCellPosition(data.Position);
    }

    GD.Print($"[TilemapJsonImporter] Updated NPC spawn: {data.Id}");
}

private void CreateNpcSpawnNode(NpcSpawnData data, Node2D parent)
{
    var script = GD.Load<Script>("res://scripts/game/NpcSpawn.cs");
    if (script == null)
    {
        GD.PrintErr("[TilemapJsonImporter] Failed to load NpcSpawn script");
        return;
    }

    var instance = new Sprite2D();
    instance.SetScript(script);
    instance.Name = data.Id;
    instance.Set("GridPosition", data.Position.ToVector2I());
    instance.Set("NpcId", data.NpcId);
    instance.Position = ToCenteredCellPosition(data.Position);
    instance.ZIndex = 2;

    parent.AddChild(instance);

    if (Engine.IsEditorHint())
    {
        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }
    }

    GD.Print($"[TilemapJsonImporter] Created NPC spawn: {data.Id}");
}

private static Vector2 ToCenteredCellPosition(Vector2IData position)
{
    const int cellSize = 32;
    return new Vector2(
        position.X * cellSize + cellSize / 2f,
        position.Y * cellSize + cellSize / 2f
    );
}
```

- [ ] **Step 3: Center imported enemy and stair nodes**

In `UpdateEnemySpawnNode`, replace the `node2d.Position = ...` assignment with:

```csharp
node2d.Position = ToCenteredCellPosition(data.Position);
```

In `CreateEnemySpawnNode`, replace the `node2d.Position = ...` assignment with:

```csharp
node2d.Position = ToCenteredCellPosition(data.Position);
```

In `UpdateStairConnectionNode`, replace the `node2d.Position = ...` assignment with:

```csharp
node2d.Position = ToCenteredCellPosition(data.Position);
```

- [ ] **Step 4: Build**

Run:

```bash
dotnet build Sirius.sln
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 5: Run tilemap JSON tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/tilemap_json/TilemapJsonExporter.cs scripts/tilemap_json/TilemapJsonImporter.cs
git commit -m "feat: import npc spawns from floor json"
```

## Task 4: Make Tilemap Sync Respect GODOT_PATH

**Files:**
- Modify: `tools/tilemap_json_sync.py`

- [ ] **Step 1: Update Godot path resolution**

In `tools/tilemap_json_sync.py`, add `import os` beside the existing imports:

```python
import os
```

Replace:

```python
GODOT_PATH = "/Applications/Godot_mono.app/Contents/MacOS/Godot"
```

with:

```python
DEFAULT_GODOT_PATH = "/Applications/Godot_mono.app/Contents/MacOS/Godot"
GODOT_PATH = os.environ.get("GODOT_PATH", DEFAULT_GODOT_PATH)
```

- [ ] **Step 2: Smoke test CLI help**

Run:

```bash
python3 tools/tilemap_json_sync.py --help
```

Expected: prints usage and exits 0.

- [ ] **Step 3: Commit**

```bash
git add tools/tilemap_json_sync.py
git commit -m "chore: allow custom godot path for tilemap sync"
```

## Task 5: Generate And Import The New Floor 0 Maze

**Files:**
- Create: `scenes/game/floors/FloorGF.json`
- Modify: `scenes/game/floors/FloorGF.tscn`
- Modify: `resources/floors/FloorGF.tres`

- [ ] **Step 1: Generate floor JSON and floor resource metadata**

Run:

```bash
python3 tools/floor0_maze_generator.py --output scenes/game/floors/FloorGF.json --floor-def resources/floors/FloorGF.tres
```

Expected output starts with:

```text
Generated Floor 0 maze: 100x100
```

- [ ] **Step 2: Import JSON into the Godot floor scene**

Run:

```bash
python3 tools/tilemap_json_sync.py import scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn
```

Expected output includes:

```text
Successfully imported JSON and saved scene
```

If the default Godot path is wrong, run the same command with the local path:

```bash
GODOT_PATH="/Applications/Godot_mono.app/Contents/MacOS/Godot" python3 tools/tilemap_json_sync.py import scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn
```

- [ ] **Step 3: Inspect scene/resource diff**

Run:

```bash
git diff --stat scenes/game/floors/FloorGF.tscn scenes/game/floors/FloorGF.json resources/floors/FloorGF.tres
```

Expected: `FloorGF.json` is new, `FloorGF.tscn` has large tilemap/entity changes, and `FloorGF.tres` updates `PlayerStartPosition` plus `StairsUp`.

- [ ] **Step 4: Build after import**

Run:

```bash
dotnet build Sirius.sln
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn resources/floors/FloorGF.tres
git commit -m "feat: generate floor 0 district loop maze"
```

## Task 6: Add Scene-Level Floor 0 Connectivity Test

**Files:**
- Create: `tests/game/FloorGFMazeLayoutTest.cs`

- [ ] **Step 1: Write scene validation test**

Create `tests/game/FloorGFMazeLayoutTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorGFMazeLayoutTest : Node
{
    private static readonly Vector2I PlayerStart = new(8, 50);
    private static readonly Vector2I StairPosition = new(82, 68);

    [TestCase]
    public void FloorGF_GeneratedMaze_HasExpectedStaticLayers()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var groundLayer = gridMap.GetNode<TileMapLayer>("GroundLayer");
            var wallLayer = gridMap.GetNode<TileMapLayer>("WallLayer");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");

            AssertThat(groundLayer.GetUsedCells().Count).IsEqual(10000);
            AssertThat(wallLayer.GetUsedCells().Count).IsGreater(6000);
            AssertThat(stairLayer.GetUsedCells().Count).IsEqual(1);
            AssertThat(stairLayer.GetUsedCells().Contains(StairPosition)).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void FloorGF_GeneratedMaze_EntitiesAreOnWalkableTiles()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            AssertThat(IsWalkable(PlayerStart, walls)).IsTrue();
            AssertThat(IsWalkable(StairPosition, walls)).IsTrue();

            foreach (var child in gridMap.GetChildren())
            {
                if (child is EnemySpawn enemySpawn)
                {
                    AssertThat(IsWalkable(enemySpawn.GridPosition, walls)).IsTrue();
                }
                else if (child is NpcSpawn npcSpawn)
                {
                    AssertThat(IsWalkable(npcSpawn.GridPosition, walls)).IsTrue();
                }
                else if (child is StairConnection stair)
                {
                    AssertThat(IsWalkable(stair.GridPosition, walls)).IsTrue();
                }
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void FloorGF_GeneratedMaze_CriticalBeatsAreReachable()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            var goals = new List<Vector2I> { StairPosition };
            goals.AddRange(gridMap.GetChildren().OfType<NpcSpawn>().Select(n => n.GridPosition));
            goals.Add(gridMap.GetChildren().OfType<EnemySpawn>().First(e => e.Name == "EnemySpawn_Goblin").GridPosition);

            foreach (var goal in goals)
            {
                AssertThat(HasPath(PlayerStart, goal, walls)).IsTrue();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/FloorGF.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static HashSet<Vector2I> GetWalls(GridMap gridMap)
    {
        var wallLayer = gridMap.GetNode<TileMapLayer>("WallLayer");
        return wallLayer.GetUsedCells().ToHashSet();
    }

    private static bool IsWalkable(Vector2I position, HashSet<Vector2I> walls)
    {
        return position.X >= 0
            && position.X < 100
            && position.Y >= 0
            && position.Y < 100
            && !walls.Contains(position);
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

- [ ] **Step 2: Run the scene validation test**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGFMazeLayoutTest"
```

Expected: PASS.

- [ ] **Step 3: Run NPC spawn regression test**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~NpcSpawnTest"
```

Expected: PASS, including `FloorGF_ContainsReachableNpcSpawns_WithRegisteredNpcIds`.

- [ ] **Step 4: Commit**

```bash
git add tests/game/FloorGFMazeLayoutTest.cs
git commit -m "test: validate generated floor 0 maze"
```

## Task 7: Final Verification

**Files:**
- No new files.

- [ ] **Step 1: Run Python tests**

Run:

```bash
python3 -m unittest tests.tools.test_floor0_maze_generator -v
```

Expected: PASS.

- [ ] **Step 2: Build project**

Run:

```bash
dotnet build Sirius.sln
```

Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Run focused Godot tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~FloorGFMazeLayoutTest|FullyQualifiedName~NpcSpawnTest"
```

Expected: PASS.

- [ ] **Step 4: Inspect final git diff**

Run:

```bash
git status --short
```

Expected: clean working tree after all commits, or only intentional uncommitted changes if the user requested no commits during execution.
