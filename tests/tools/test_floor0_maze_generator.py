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
