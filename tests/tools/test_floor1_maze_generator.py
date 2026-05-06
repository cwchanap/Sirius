import json
import io
import tempfile
import unittest
from collections import deque
from contextlib import redirect_stdout
from pathlib import Path
import sys
from unittest.mock import patch


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
    FLOOR2_HEIGHT,
    FLOOR2_PLAYER_START,
    FLOOR2_WIDTH,
    GRID_HEIGHT,
    GRID_WIDTH,
    build_floor1_model,
    build_floor2_model,
    main,
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


def count_dead_end_cells(walkable):
    dead_ends = 0
    for x, y in walkable:
        if x >= FLOOR1_WIDTH or y >= FLOOR1_HEIGHT:
            continue

        neighbor_count = sum(
            (nx, ny) in walkable
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
        )
        if neighbor_count == 1:
            dead_ends += 1

    return dead_ends


def floor_definition_source():
    return "\n".join(
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

    def test_maze_has_multiple_dead_end_branches(self):
        self.assertGreaterEqual(count_dead_end_cells(self.walkable), 8)

    def test_enemy_gates_block_routes_until_clearable(self):
        enemy_positions = {
            (enemy["position"]["x"], enemy["position"]["y"])
            for enemy in self.model["entities"]["enemy_spawns"]
        }
        uncleared_walkable = self.walkable - enemy_positions
        gated_goals = [FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B]
        gated_goals.extend(FLOOR1_HIDDEN_PLACEHOLDERS.values())

        for goal in gated_goals:
            with self.subTest(goal=goal):
                self.assertFalse(has_path(uncleared_walkable, FLOOR1_PLAYER_START, goal))

    def test_south_stair_gate_does_not_open_north_stair(self):
        enemy_positions = {
            enemy["id"]: (enemy["position"]["x"], enemy["position"]["y"])
            for enemy in self.model["entities"]["enemy_spawns"]
        }
        south_gate_only_walkable = self.walkable - {
            position
            for enemy_id, position in enemy_positions.items()
            if enemy_id != "EnemySpawn_ForestSpirit_StairB"
        }

        self.assertFalse(has_path(south_gate_only_walkable, FLOOR1_PLAYER_START, FLOOR1_UP_STAIR_A))

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
        with tempfile.TemporaryDirectory() as tmpdir:
            floor_def = Path(tmpdir) / "Floor1F.tres"
            floor_def.write_text(floor_definition_source(), encoding="utf-8")

            update_floor_definition(floor_def, self.model)

            updated = floor_def.read_text(encoding="utf-8")
            self.assertIn("PlayerStartPosition = Vector2i(8, 30)", updated)
            self.assertIn("StairsUp = Array[Vector2i]([Vector2i(49, 12), Vector2i(48, 48)])", updated)
            self.assertIn("StairsDown = Array[Vector2i]([Vector2i(8, 30)])", updated)
            self.assertIn("StairsUpDestinations = Array[Vector2i]([Vector2i(49, 12), Vector2i(48, 48)])", updated)
            self.assertIn("StairsDownDestinations = Array[Vector2i]([Vector2i(8, 30)])", updated)

    def test_main_skips_missing_floor2_definition_and_updates_floor1(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            tmp = Path(tmpdir)
            floor1_output = tmp / "Floor1F.json"
            floor2_output = tmp / "Floor2F.json"
            floor1_def = tmp / "Floor1F.tres"
            missing_floor2_def = tmp / "Floor2F.tres"
            floor1_def.write_text(floor_definition_source(), encoding="utf-8")

            argv = [
                "floor1_maze_generator.py",
                "--floor1-output",
                str(floor1_output),
                "--floor1-def",
                str(floor1_def),
                "--floor2-output",
                str(floor2_output),
                "--floor2-def",
                str(missing_floor2_def),
            ]

            stdout = io.StringIO()
            with patch.object(sys, "argv", argv), redirect_stdout(stdout):
                result = main()

            self.assertEqual(result, 0)
            self.assertTrue(floor1_output.exists())
            self.assertTrue(floor2_output.exists())
            self.assertIn("Warning: floor definition not found", stdout.getvalue())

            updated = floor1_def.read_text(encoding="utf-8")
            self.assertIn("PlayerStartPosition = Vector2i(8, 30)", updated)
            self.assertIn("StairsUp = Array[Vector2i]([Vector2i(49, 12), Vector2i(48, 48)])", updated)

    def test_main_fails_when_floor1_definition_is_missing(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            tmp = Path(tmpdir)
            floor1_output = tmp / "Floor1F.json"
            floor2_output = tmp / "Floor2F.json"
            missing_floor1_def = tmp / "Floor1F.tres"
            floor2_def = tmp / "Floor2F.tres"
            floor2_def.write_text(floor_definition_source(), encoding="utf-8")

            argv = [
                "floor1_maze_generator.py",
                "--floor1-output",
                str(floor1_output),
                "--floor1-def",
                str(missing_floor1_def),
                "--floor2-output",
                str(floor2_output),
                "--floor2-def",
                str(floor2_def),
            ]

            stdout = io.StringIO()
            with patch.object(sys, "argv", argv), redirect_stdout(stdout):
                result = main()

            self.assertNotEqual(result, 0)
            self.assertIn("Error: required Floor 1 definition not found", stdout.getvalue())


class Floor2PlaceholderGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor2_model()
        self.walkable = walkable_set(self.model)

    def test_generates_placeholder_with_two_return_stairs(self):
        ground = self.model["tile_layers"]["ground"]
        walls = {(tile["x"], tile["y"]) for tile in self.model["tile_layers"]["wall"]}
        stairs = self.model["tile_layers"]["stair"]
        stair_positions = {(tile["x"], tile["y"]) for tile in stairs}
        metadata = self.model["floor_metadata"]

        self.assertEqual(FLOOR2_WIDTH, 36)
        self.assertEqual(FLOOR2_HEIGHT, 22)
        self.assertEqual(metadata["player_start"], {"x": 10, "y": 10})
        self.assertEqual(FLOOR2_PLAYER_START, (10, 10))
        self.assertEqual(len(ground), 792)
        self.assertEqual(ground[0], {"x": 0, "y": 0, "tile": "starting_area"})
        self.assertEqual(ground[-1], {"x": 35, "y": 21, "tile": "starting_area"})

        for y in range(FLOOR2_HEIGHT, GRID_HEIGHT):
            for x in range(GRID_WIDTH):
                self.assertIn((x, y), walls)

        for y in range(FLOOR2_HEIGHT):
            for x in range(FLOOR2_WIDTH, GRID_WIDTH):
                self.assertIn((x, y), walls)

        self.assertEqual(stair_positions, {FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B})
        self.assertEqual(
            {stair["id"] for stair in self.model["entities"]["stair_connections"]},
            {"2F_1F_A", "2F_1F_B"},
        )
        self.assertEqual(
            {stair["id"]: stair for stair in self.model["entities"]["stair_connections"]},
            {
                "2F_1F_A": {
                    "id": "2F_1F_A",
                    "position": {"x": 10, "y": 10},
                    "direction": "down",
                    "target_floor": 1,
                    "destination_stair_id": "1F_2F_A",
                },
                "2F_1F_B": {
                    "id": "2F_1F_B",
                    "position": {"x": 26, "y": 10},
                    "direction": "down",
                    "target_floor": 1,
                    "destination_stair_id": "1F_2F_B",
                },
            },
        )
        self.assertEqual(self.model["entities"]["enemy_spawns"], [])
        self.assertEqual(self.model["entities"]["npc_spawns"], [])

    def test_placeholder_stairs_are_connected(self):
        self.assertTrue(has_path(self.walkable, FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B))


if __name__ == "__main__":
    unittest.main()
