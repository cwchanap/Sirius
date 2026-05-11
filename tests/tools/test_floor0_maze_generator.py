import json
import tempfile
import unittest
from collections import deque
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from tools.floor0_maze_generator import (
    FLOOR_HEIGHT,
    FLOOR_WIDTH,
    GRID_HEIGHT,
    GRID_WIDTH,
    build_floor_model,
    update_floor_definition,
    validate_model,
)

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


class Floor0MazeGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor_model()
        self.walkable = walkable_set(self.model)

    def test_generates_100_by_100_maze_within_160_grid(self):
        ground = self.model["tile_layers"]["ground"]

        self.assertEqual(FLOOR_WIDTH, 100)
        self.assertEqual(FLOOR_HEIGHT, 100)
        self.assertEqual(GRID_WIDTH, 160)
        self.assertEqual(GRID_HEIGHT, 160)
        # Ground covers the full 160x160 grid
        self.assertEqual(len(ground), GRID_WIDTH * GRID_HEIGHT)
        self.assertEqual(ground[0], {"x": 0, "y": 0, "tile": "starting_area"})
        self.assertEqual(ground[-1], {"x": 159, "y": 159, "tile": "starting_area"})

    def test_generates_perimeter_walls_and_internal_maze(self):
        walls = {(tile["x"], tile["y"]) for tile in self.model["tile_layers"]["wall"]}

        # Maze perimeter walls (within 100x100)
        for x in range(FLOOR_WIDTH):
            self.assertIn((x, 0), walls)
            self.assertIn((x, FLOOR_HEIGHT - 1), walls)

        for y in range(FLOOR_HEIGHT):
            self.assertIn((0, y), walls)
            self.assertIn((FLOOR_WIDTH - 1, y), walls)

        # All cells beyond the maze (100..159) must be walls
        for y in range(FLOOR_HEIGHT, GRID_HEIGHT):
            for x in range(GRID_WIDTH):
                self.assertIn((x, y), walls, f"Cell ({x},{y}) beyond maze should be wall")
        for y in range(FLOOR_HEIGHT):
            for x in range(FLOOR_WIDTH, GRID_WIDTH):
                self.assertIn((x, y), walls, f"Cell ({x},{y}) beyond maze should be wall")

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

    def test_ground_floor_treasure_boxes_are_authored_and_walkable(self):
        entities = self.model["entities"]
        treasure_boxes = {
            box["id"]: box
            for box in entities["treasure_boxes"]
        }
        occupied = set()
        for key in ("npc_spawns", "enemy_spawns", "stair_connections"):
            occupied.update(
                (entity["position"]["x"], entity["position"]["y"])
                for entity in entities[key]
            )

        self.assertEqual(set(treasure_boxes), set(EXPECTED_GF_TREASURE))

        for box_id, (position, gold, items) in EXPECTED_GF_TREASURE.items():
            with self.subTest(box_id=box_id):
                box = treasure_boxes[box_id]
                box_pos = (box["position"]["x"], box["position"]["y"])
                box_items = {
                    item["item_id"]: item["quantity"]
                    for item in box["items"]
                }

                self.assertEqual(box_pos, position)
                self.assertIn(box_pos, self.walkable)
                self.assertNotIn(box_pos, occupied)
                self.assertEqual(box["gold"], gold)
                self.assertEqual(box_items, items)

    def test_model_is_json_serializable(self):
        encoded = json.dumps(self.model, sort_keys=True)
        decoded = json.loads(encoded)

        self.assertEqual(decoded["schema_version"], "1.0")
        self.assertIn("npc_spawns", decoded["entities"])

    def test_validate_model_rejects_disconnected_walkable_island(self):
        isolated = {"x": 98, "y": 98, "tile": "generic"}
        self.model["tile_layers"]["wall"].remove(isolated)

        with self.assertRaisesRegex(ValueError, "Disconnected walkable cells"):
            validate_model(self.model)

    def test_update_floor_definition_rejects_missing_fields(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            floor_def = Path(tmpdir) / "FloorGF.tres"
            floor_def.write_text("[resource]\nFloorName = \"Ground Floor\"\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "PlayerStartPosition"):
                update_floor_definition(floor_def, self.model)

    def test_update_floor_definition_updates_expected_fields(self):
        source = "\n".join(
            [
                "[resource]",
                "PlayerStartPosition = Vector2i(20, 15)",
                "StairsUp = Array[Vector2i]([Vector2i(13, 3)])",
                "StairsUpDestinations = Array[Vector2i]([Vector2i(17, 13)])",
                "",
            ]
        )

        with tempfile.TemporaryDirectory() as tmpdir:
            floor_def = Path(tmpdir) / "FloorGF.tres"
            floor_def.write_text(source, encoding="utf-8")

            update_floor_definition(floor_def, self.model)

            updated = floor_def.read_text(encoding="utf-8")
            self.assertIn("PlayerStartPosition = Vector2i(8, 50)", updated)
            self.assertIn("StairsUp = Array[Vector2i]([Vector2i(82, 68)])", updated)
            self.assertIn("StairsUpDestinations = Array[Vector2i]([Vector2i(17, 13)])", updated)


if __name__ == "__main__":
    unittest.main()
