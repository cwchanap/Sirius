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
