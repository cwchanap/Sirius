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
    FLOOR1_ENEMY_GATES,
    FLOOR1_EXTRA_ENEMY_PATROLS,
    FLOOR1_HEIGHT,
    FLOOR1_HIDDEN_PLACEHOLDERS,
    FLOOR1_PLAYER_START,
    FLOOR1_PUZZLE_GATES,
    FLOOR1_PUZZLE_ID,
    FLOOR1_PUZZLE_RIDDLES,
    FLOOR1_PUZZLE_SWITCHES,
    FLOOR1_PUZZLE_TRAPS,
    FLOOR1_UP_STAIR_A,
    FLOOR1_UP_STAIR_B,
    FLOOR1_WIDTH,
    FLOOR2_DOWN_STAIR_A,
    FLOOR2_DOWN_STAIR_B,
    FLOOR2_ENEMY_GATES,
    FLOOR2_EXTRA_ENEMY_PATROLS,
    FLOOR2_HEIGHT,
    FLOOR2_PLAYER_START,
    FLOOR2_PUZZLE_GATES,
    FLOOR2_PUZZLE_ID,
    FLOOR2_PUZZLE_RIDDLES,
    FLOOR2_PUZZLE_SWITCHES,
    FLOOR2_PUZZLE_TRAPS,
    FLOOR2_TREASURE_BOXES,
    FLOOR2_UP_STAIR,
    FLOOR2_WIDTH,
    FLOOR3_DOWN_STAIR,
    FLOOR3_HEIGHT,
    FLOOR3_PLAYER_START,
    FLOOR3_WIDTH,
    GRID_HEIGHT,
    GRID_WIDTH,
    build_floor1_model,
    build_floor2_model,
    build_floor3_model,
    main,
    update_floor_definition,
    validate_model,
)

ENEMY_DENSITY_MULTIPLIER = 3

EXPECTED_FLOOR1_TREASURE = {
    "TreasureBox_1F_WestDeadEndCache": ((4, 22), 85, {"health_potion": 2}),
    "TreasureBox_1F_WestCrossingCache": ((5, 37), 55, {"health_potion": 1}),
    "TreasureBox_1F_WestLoopCache": ((2, 42), 70, {"swiftness_draught": 1}),
    "TreasureBox_1F_NorthSpurCache": ((28, 20), 0, {"mana_potion": 1}),
    "TreasureBox_1F_NorthConnectorCache": ((30, 19), 0, {"mana_potion": 2}),
    "TreasureBox_1F_CentralSpurCache": ((43, 34), 95, {"iron_skin": 1}),
    "TreasureBox_1F_EastHallCache": ((52, 24), 120, {"greater_health_potion": 1}),
    "TreasureBox_1F_NorthStairCache": ((49, 14), 0, {"iron_boots": 1}),
    "TreasureBox_1F_EastShortcutCache": ((58, 46), 0, {"steel_longsword": 1}),
    "TreasureBox_1F_SouthGalleryCache": ((38, 55), 130, {"flash_powder": 1}),
    "TreasureBox_1F_SouthHiddenCache": ((24, 56), 0, {"chain_mail": 1}),
    "TreasureBox_1F_SouthShortcutPocket": ((26, 56), 0, {"antidote": 1}),
}

EXPECTED_FLOOR2_TREASURE = {
    "TreasureBox_2F_WestSupplyCache": ((6, 16), 100, {"greater_health_potion": 1}),
    "TreasureBox_2F_WestArchiveCache": ((4, 32), 120, {"major_health_potion": 1}),
    "TreasureBox_2F_NorthLandingCache": ((18, 4), 0, {"major_mana_potion": 1}),
    "TreasureBox_2F_NorthStudyCache": ((44, 8), 0, {"major_mana_potion": 1}),
    "TreasureBox_2F_SouthStacksCache": ((13, 55), 130, {"warding_charm": 1}),
    "TreasureBox_2F_EastGalleryCache": ((56, 36), 140, {"smoke_bomb": 1}),
    "TreasureBox_2F_EastStudyCache": ((56, 24), 150, {"smoke_bomb": 1}),
    "TreasureBox_2F_SouthArmoryCache": ((42, 55), 0, {"steel_tower_shield": 1}),
    "TreasureBox_2F_SouthShortcutCache": ((30, 56), 0, {"swift_boots": 1}),
    "TreasureBox_2F_StairWatchCache": ((53, 48), 160, {"swift_boots": 1}),
    "TreasureBox_2F_PuzzleVaultCache": ((35, 38), 0, {"warding_charm": 1}),
}


def walkable_set(model):
    walls = {(tile["x"], tile["y"]) for tile in model["tile_layers"]["wall"]}
    ground = model["tile_layers"]["ground"]
    width = max(tile["x"] for tile in ground) + 1
    height = max(tile["y"] for tile in ground) + 1
    return {
        (x, y)
        for y in range(height)
        for x in range(width)
        if (x, y) not in walls
    }


def assert_tiles_inside(test_case, tiles, width, height):
    for tile in tiles:
        test_case.assertGreaterEqual(tile["x"], 0)
        test_case.assertLess(tile["x"], width)
        test_case.assertGreaterEqual(tile["y"], 0)
        test_case.assertLess(tile["y"], height)


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


def shortest_path_length(walkable, start, goal):
    queue = deque([(start, 0)])
    seen = {start}

    while queue:
        current, distance = queue.popleft()
        if current == goal:
            return distance

        x, y = current
        for nxt in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if nxt in walkable and nxt not in seen:
                seen.add(nxt)
                queue.append((nxt, distance + 1))

    return None


def entity_positions(entities, key):
    return {
        entity["id"]: (entity["position"]["x"], entity["position"]["y"])
        for entity in entities.get(key, [])
    }


def count_dead_end_cells(walkable, width=FLOOR1_WIDTH, height=FLOOR1_HEIGHT):
    dead_ends = 0
    for x, y in walkable:
        if x >= width or y >= height:
            continue

        neighbor_count = sum(
            (nx, ny) in walkable
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
        )
        if neighbor_count == 1:
            dead_ends += 1

    return dead_ends


def branch_payoff_positions(model):
    positions = set()
    entities = model["entities"]
    for key in (
        "enemy_spawns",
        "treasure_boxes",
        "stair_connections",
        "hidden_placeholders",
        "trap_tiles",
        "puzzle_switches",
        "puzzle_gates",
        "puzzle_riddles",
    ):
        positions.update(entity_positions(entities, key).values())

    return positions


def walkable_neighbors(walkable, position):
    x, y = position
    return [
        (nx, ny)
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
        if (nx, ny) in walkable
    ]


def dead_end_branches(walkable, width, height):
    branches = []
    for leaf in sorted(walkable):
        x, y = leaf
        if x >= width or y >= height:
            continue

        if neighbor_count(walkable, leaf) != 1:
            continue

        branch = [leaf]
        previous = None
        current = leaf
        while True:
            next_cells = [
                neighbor
                for neighbor in walkable_neighbors(walkable, current)
                if neighbor != previous
            ]
            if not next_cells:
                break

            next_cell = next_cells[0]
            if neighbor_count(walkable, next_cell) != 2:
                break

            branch.append(next_cell)
            previous = current
            current = next_cell

        branches.append(branch)

    return branches


def unrewarded_dead_end_branches(model, walkable, width, height):
    payoff_positions = branch_payoff_positions(model)
    unrewarded = []
    for branch in dead_end_branches(walkable, width, height):
        adjacent = set()
        for cell in branch:
            adjacent.update(walkable_neighbors(walkable, cell))

        if payoff_positions.isdisjoint(set(branch) | adjacent):
            unrewarded.append(branch)

    return unrewarded


def neighbor_count(walkable, position):
    x, y = position
    return sum(
        (nx, ny) in walkable
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
    )


SHORTCUT_ROUTES = [
    {
        "enemy_id": "EnemySpawn_Skeleton_NorthShortcut",
        "entry": (16, 8),
        "source": (36, 4),
        "target": (38, 8),
        "min_depth": 24,
        "min_savings": 10,
    },
    {
        "enemy_id": "EnemySpawn_ForestSpirit_EastShortcut",
        "entry": (56, 30),
        "source": (54, 58),
        "target": (58, 46),
        "min_depth": 18,
        "min_savings": 10,
    },
    {
        "enemy_id": "EnemySpawn_Orc_SouthShortcut",
        "entry": (19, 54),
        "source": (42, 58),
        "target": (23, 58),
        "min_depth": 18,
        "min_savings": 10,
    },
]

INTERIOR_WALL_RUN_MARGIN = 4
MAX_INTERIOR_WALL_RUN = 28


def max_consecutive_wall_run(walls, width, height, margin):
    max_run = 0

    for y in range(margin, height - margin):
        run = 0
        for x in range(margin, width - margin):
            if (x, y) in walls:
                run += 1
                max_run = max(max_run, run)
            else:
                run = 0

    for x in range(margin, width - margin):
        run = 0
        for y in range(margin, height - margin):
            if (x, y) in walls:
                run += 1
                max_run = max(max_run, run)
            else:
                run = 0

    return max_run


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

    def test_generates_60_by_60_floor_without_outside_padding(self):
        ground = self.model["tile_layers"]["ground"]
        walls = self.model["tile_layers"]["wall"]
        stairs = self.model["tile_layers"]["stair"]

        self.assertEqual(FLOOR1_WIDTH, 60)
        self.assertEqual(FLOOR1_HEIGHT, 60)
        self.assertEqual(GRID_WIDTH, 160)
        self.assertEqual(GRID_HEIGHT, 160)
        self.assertEqual(len(ground), 3600)
        self.assertEqual(ground[0], {"x": 0, "y": 0, "tile": "starting_area"})
        self.assertEqual(ground[-1], {"x": 59, "y": 59, "tile": "starting_area"})
        assert_tiles_inside(self, ground, FLOOR1_WIDTH, FLOOR1_HEIGHT)
        assert_tiles_inside(self, walls, FLOOR1_WIDTH, FLOOR1_HEIGHT)
        assert_tiles_inside(self, stairs, FLOOR1_WIDTH, FLOOR1_HEIGHT)

    def test_places_visible_stairs_and_no_hidden_stair_tiles(self):
        stairs = self.model["tile_layers"]["stair"]
        stair_positions = {(tile["x"], tile["y"]) for tile in stairs}

        self.assertEqual(
            stair_positions,
            {FLOOR1_DOWN_STAIR, FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B},
        )

        hidden_positions = set(FLOOR1_HIDDEN_PLACEHOLDERS.values())
        self.assertNotIn("hidden_room_south", FLOOR1_HIDDEN_PLACEHOLDERS)
        self.assertTrue(hidden_positions.isdisjoint(stair_positions))

        connections = self.model["entities"]["stair_connections"]
        self.assertEqual(
            {stair["id"] for stair in connections},
            {"1F_001", "1F_2F_A", "1F_2F_B"},
        )

    def test_places_enemy_gates_and_no_npcs(self):
        entities = self.model["entities"]
        baseline_enemies = FLOOR1_ENEMY_GATES | FLOOR1_EXTRA_ENEMY_PATROLS
        enemy_types_by_id = {
            enemy["id"]: enemy["enemy_type"]
            for enemy in entities["enemy_spawns"]
        }
        required_enemy_types = {
            enemy_id: data["enemy_type"]
            for enemy_id, data in baseline_enemies.items()
        }
        supplemental_enemy_types = {
            enemy_id: enemy_type
            for enemy_id, enemy_type in enemy_types_by_id.items()
            if enemy_id.startswith("EnemySpawn_1F_DensityPatrol_")
        }

        self.assertEqual(entities["npc_spawns"], [])
        self.assertEqual(
            {enemy_id: enemy_types_by_id[enemy_id] for enemy_id in required_enemy_types},
            required_enemy_types,
        )
        self.assertEqual(len(entities["enemy_spawns"]), len(baseline_enemies) * ENEMY_DENSITY_MULTIPLIER)
        self.assertEqual(len(supplemental_enemy_types), len(baseline_enemies) * (ENEMY_DENSITY_MULTIPLIER - 1))
        self.assertEqual(
            set(supplemental_enemy_types),
            {
                f"EnemySpawn_1F_DensityPatrol_{index:03d}"
                for index in range(1, len(supplemental_enemy_types) + 1)
            },
        )
        self.assertTrue(
            set(supplemental_enemy_types.values()).issubset(
                {"goblin", "orc", "skeleton_warrior", "forest_spirit"}
            )
        )
        self.assertEqual(
            set(enemy_types_by_id) - set(required_enemy_types) - set(supplemental_enemy_types),
            set(),
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

    def test_floor1_treasure_boxes_are_authored_and_walkable(self):
        entities = self.model["entities"]
        treasure_boxes = {
            box["id"]: box
            for box in entities["treasure_boxes"]
        }
        occupied = set()
        for key in (
            "npc_spawns",
            "enemy_spawns",
            "stair_connections",
            "hidden_placeholders",
            "trap_tiles",
            "puzzle_switches",
            "puzzle_gates",
            "puzzle_riddles",
        ):
            occupied.update(
                (entity["position"]["x"], entity["position"]["y"])
                for entity in entities.get(key, [])
            )

        self.assertEqual(set(treasure_boxes), set(EXPECTED_FLOOR1_TREASURE))

        for box_id, (position, gold, items) in EXPECTED_FLOOR1_TREASURE.items():
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

    def test_floor1_puzzle_trap_entities_are_authored_and_walkable(self):
        entities = self.model["entities"]
        trap_tiles = entity_positions(entities, "trap_tiles")
        puzzle_switches = entity_positions(entities, "puzzle_switches")
        puzzle_gates = entity_positions(entities, "puzzle_gates")
        puzzle_riddles = entity_positions(entities, "puzzle_riddles")

        expected_traps = {
            trap_id: data["position"]
            for trap_id, data in FLOOR1_PUZZLE_TRAPS.items()
        }
        expected_switches = {
            switch_id: data["position"]
            for switch_id, data in FLOOR1_PUZZLE_SWITCHES.items()
        }
        expected_gates = {
            gate_id: data["position"]
            for gate_id, data in FLOOR1_PUZZLE_GATES.items()
        }
        expected_riddles = {
            riddle_id: data["position"]
            for riddle_id, data in FLOOR1_PUZZLE_RIDDLES.items()
        }

        self.assertEqual(trap_tiles, expected_traps)
        self.assertEqual(puzzle_switches, expected_switches)
        self.assertEqual(puzzle_gates, expected_gates)
        self.assertEqual(puzzle_riddles, expected_riddles)

        occupied = set()
        for key in ("enemy_spawns", "npc_spawns", "stair_connections", "hidden_placeholders", "treasure_boxes"):
            occupied.update(entity_positions(entities, key).values())

        all_puzzle_positions = [
            *trap_tiles.values(),
            *puzzle_switches.values(),
            *puzzle_gates.values(),
            *puzzle_riddles.values(),
        ]
        self.assertEqual(len(all_puzzle_positions), len(set(all_puzzle_positions)))

        for position in all_puzzle_positions:
            with self.subTest(position=position):
                self.assertIn(position, self.walkable)
                self.assertNotIn(position, occupied)

        for trap in entities["trap_tiles"]:
            self.assertEqual(trap["puzzle_id"], FLOOR1_PUZZLE_ID)
            self.assertEqual(trap["damage"], 12)
            self.assertEqual(trap.get("status_effect", ""), "")

        puzzle_switch = entities["puzzle_switches"][0]
        self.assertEqual(puzzle_switch["puzzle_id"], FLOOR1_PUZZLE_ID)
        self.assertEqual(puzzle_switch["prompt_text"], "Use")
        self.assertEqual(puzzle_switch["activated_text"], "The lever wakes the old shortcut seal.")

        puzzle_gate = entities["puzzle_gates"][0]
        self.assertEqual(puzzle_gate["puzzle_id"], FLOOR1_PUZZLE_ID)
        self.assertTrue(puzzle_gate["starts_closed"])

        riddle = entities["puzzle_riddles"][0]
        self.assertEqual(riddle["puzzle_id"], FLOOR1_PUZZLE_ID)
        self.assertEqual(riddle["correct_choice_id"], "east_stone")
        self.assertEqual(riddle["wrong_answer_damage"], 12)
        self.assertEqual(
            riddle["prompt_text"],
            "Four stones face the old shortcut. Which stone sleeps until the lever wakes it?",
        )
        self.assertEqual(
            riddle["choices"],
            [
                {"id": "north_stone", "label": "North stone"},
                {"id": "east_stone", "label": "East stone"},
                {"id": "south_stone", "label": "South stone"},
            ],
        )

    def test_required_routes_remain_reachable_with_puzzle_gate_closed(self):
        gate_positions = set(entity_positions(self.model["entities"], "puzzle_gates").values())
        closed_gate_walkable = self.walkable - gate_positions

        for goal in [FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B, *FLOOR1_HIDDEN_PLACEHOLDERS.values()]:
            with self.subTest(goal=goal):
                self.assertTrue(has_path(closed_gate_walkable, FLOOR1_PLAYER_START, goal))

    def test_south_puzzle_gate_opens_reward_and_shortcut_route(self):
        gate_positions = set(entity_positions(self.model["entities"], "puzzle_gates").values())
        closed_gate_walkable = self.walkable - gate_positions
        puzzle_room_side = (22, 56)
        reward = EXPECTED_FLOOR1_TREASURE["TreasureBox_1F_SouthHiddenCache"][0]
        shortcut_payoff = (42, 58)

        self.assertFalse(has_path(closed_gate_walkable, puzzle_room_side, reward))
        self.assertTrue(has_path(self.walkable, puzzle_room_side, reward))

        closed_length = shortest_path_length(closed_gate_walkable, puzzle_room_side, shortcut_payoff)
        open_length = shortest_path_length(self.walkable, puzzle_room_side, shortcut_payoff)

        self.assertIsNotNone(closed_length)
        self.assertIsNotNone(open_length)
        self.assertGreaterEqual(closed_length - open_length, 50)

    def test_maze_has_multiple_dead_end_branches(self):
        self.assertGreaterEqual(count_dead_end_cells(self.walkable), 8)

    def test_floor1_dead_end_branches_have_payoff(self):
        self.assertEqual(
            unrewarded_dead_end_branches(self.model, self.walkable, FLOOR1_WIDTH, FLOOR1_HEIGHT),
            [],
        )

    def test_maze_has_named_decision_intersections(self):
        decision_intersections = [
            (12, 37),
            (13, 28),
            (28, 8),
            (28, 11),
            (52, 34),
            (50, 32),
        ]

        for position in decision_intersections:
            with self.subTest(position=position):
                self.assertIn(position, self.walkable)
                self.assertGreaterEqual(neighbor_count(self.walkable, position), 3)

    def test_maze_breaks_up_long_interior_wall_runs(self):
        walls = {(tile["x"], tile["y"]) for tile in self.model["tile_layers"]["wall"]}

        self.assertLessEqual(
            max_consecutive_wall_run(walls, FLOOR1_WIDTH, FLOOR1_HEIGHT, INTERIOR_WALL_RUN_MARGIN),
            MAX_INTERIOR_WALL_RUN,
        )

    def test_maze_has_deep_shortcut_branches(self):
        enemy_positions = {
            enemy["id"]: (enemy["position"]["x"], enemy["position"]["y"])
            for enemy in self.model["entities"]["enemy_spawns"]
        }

        for route in SHORTCUT_ROUTES:
            with self.subTest(route=route["enemy_id"]):
                self.assertIn(route["source"], self.walkable)
                locked_walkable = self.walkable - {enemy_positions[route["enemy_id"]]}
                depth = shortest_path_length(locked_walkable, route["entry"], route["source"])
                self.assertIsNotNone(depth)
                self.assertGreaterEqual(depth, route["min_depth"])

    def test_shortcut_enemies_unlock_shorter_routes(self):
        enemy_positions = {
            enemy["id"]: (enemy["position"]["x"], enemy["position"]["y"])
            for enemy in self.model["entities"]["enemy_spawns"]
        }

        for route in SHORTCUT_ROUTES:
            with self.subTest(route=route["enemy_id"]):
                self.assertIn(route["enemy_id"], enemy_positions)
                locked_walkable = self.walkable - {enemy_positions[route["enemy_id"]]}
                locked_length = shortest_path_length(locked_walkable, route["source"], route["target"])
                unlocked_length = shortest_path_length(self.walkable, route["source"], route["target"])

                self.assertIsNotNone(unlocked_length)
                if locked_length is not None:
                    self.assertGreaterEqual(locked_length - unlocked_length, route["min_savings"])

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
        isolated = {"x": 2, "y": 2, "tile": "generic"}
        self.assertIn(isolated, self.model["tile_layers"]["wall"])
        self.model["tile_layers"]["wall"].remove(isolated)

        with self.assertRaisesRegex(ValueError, "Disconnected walkable cells"):
            validate_model(self.model, FLOOR1_WIDTH, FLOOR1_HEIGHT)

    def test_validate_model_rejects_closed_gate_blocking_hidden_placeholder(self):
        model = {
            "floor_metadata": {
                "floor_index": 1,
                "floor_id": "TestFloor",
                "display_name": "Test Floor",
                "grid_width": 3,
                "grid_height": 1,
                "player_start": {"x": 0, "y": 0},
            },
            "tile_layers": {
                "ground": [
                    {"x": 0, "y": 0, "tile": "ground_starting_area"},
                    {"x": 1, "y": 0, "tile": "ground_starting_area"},
                    {"x": 2, "y": 0, "tile": "ground_starting_area"},
                ],
                "wall": [],
                "stair": [],
            },
            "entities": {
                "hidden_placeholders": [
                    {"id": "Hidden_Test", "position": {"x": 2, "y": 0}, "target": "Future"}
                ],
                "puzzle_gates": [
                    {
                        "id": "Gate_Test",
                        "puzzle_id": "Puzzle_Test",
                        "position": {"x": 1, "y": 0},
                        "starts_closed": True,
                    }
                ],
            },
        }

        with self.assertRaisesRegex(
            ValueError,
            "Required hidden placeholder Hidden_Test is blocked by a closed puzzle gate",
        ):
            validate_model(model, 3, 1)

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
            floor3_output = tmp / "Floor3F.json"
            floor1_def = tmp / "Floor1F.tres"
            missing_floor2_def = tmp / "Floor2F.tres"
            missing_floor3_def = tmp / "Floor3F.tres"
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
                "--floor3-output",
                str(floor3_output),
                "--floor3-def",
                str(missing_floor3_def),
            ]

            stdout = io.StringIO()
            with patch.object(sys, "argv", argv), redirect_stdout(stdout):
                result = main()

            self.assertEqual(result, 0)
            self.assertTrue(floor1_output.exists())
            self.assertTrue(floor2_output.exists())
            self.assertTrue(floor3_output.exists())
            self.assertIn("Warning: floor definition not found", stdout.getvalue())

            updated = floor1_def.read_text(encoding="utf-8")
            self.assertIn("PlayerStartPosition = Vector2i(8, 30)", updated)
            self.assertIn("StairsUp = Array[Vector2i]([Vector2i(49, 12), Vector2i(48, 48)])", updated)

    def test_main_fails_when_floor1_definition_is_missing(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            tmp = Path(tmpdir)
            floor1_output = tmp / "Floor1F.json"
            floor2_output = tmp / "Floor2F.json"
            floor3_output = tmp / "Floor3F.json"
            missing_floor1_def = tmp / "Floor1F.tres"
            floor2_def = tmp / "Floor2F.tres"
            floor3_def = tmp / "Floor3F.tres"
            floor2_def.write_text(floor_definition_source(), encoding="utf-8")
            floor3_def.write_text(floor_definition_source(), encoding="utf-8")

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
                "--floor3-output",
                str(floor3_output),
                "--floor3-def",
                str(floor3_def),
            ]

            stdout = io.StringIO()
            with patch.object(sys, "argv", argv), redirect_stdout(stdout):
                result = main()

            self.assertNotEqual(result, 0)
            self.assertIn("Error: required Floor 1 definition not found", stdout.getvalue())


class Floor2MazeGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor2_model()
        self.walkable = walkable_set(self.model)

    def test_generates_60_by_60_floor_without_outside_padding(self):
        ground = self.model["tile_layers"]["ground"]
        walls = self.model["tile_layers"]["wall"]
        stairs = self.model["tile_layers"]["stair"]
        stair_positions = {(tile["x"], tile["y"]) for tile in stairs}
        metadata = self.model["floor_metadata"]

        self.assertEqual(FLOOR2_WIDTH, 60)
        self.assertEqual(FLOOR2_HEIGHT, 60)
        self.assertEqual(metadata["player_start"], {"x": 10, "y": 10})
        self.assertEqual(FLOOR2_PLAYER_START, (10, 10))
        self.assertEqual(len(ground), 3600)
        self.assertEqual(ground[0], {"x": 0, "y": 0, "tile": "starting_area"})
        self.assertEqual(ground[-1], {"x": 59, "y": 59, "tile": "starting_area"})
        assert_tiles_inside(self, ground, FLOOR2_WIDTH, FLOOR2_HEIGHT)
        assert_tiles_inside(self, walls, FLOOR2_WIDTH, FLOOR2_HEIGHT)
        assert_tiles_inside(self, stairs, FLOOR2_WIDTH, FLOOR2_HEIGHT)

        self.assertEqual(stair_positions, {FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B, FLOOR2_UP_STAIR})
        self.assertEqual(
            {stair["id"] for stair in self.model["entities"]["stair_connections"]},
            {"2F_1F_A", "2F_1F_B", "2F_3F_A"},
        )

    def test_places_three_visible_stairs_with_destinations(self):
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
                "2F_3F_A": {
                    "id": "2F_3F_A",
                    "position": {"x": 52, "y": 50},
                    "direction": "up",
                    "target_floor": 3,
                    "destination_stair_id": "3F_2F_A",
                },
            },
        )

    def test_places_moderate_enemy_set_and_no_npcs(self):
        enemies = self.model["entities"]["enemy_spawns"]
        baseline_enemies = FLOOR2_ENEMY_GATES | FLOOR2_EXTRA_ENEMY_PATROLS
        enemy_types_by_id = {
            enemy["id"]: enemy["enemy_type"]
            for enemy in enemies
        }
        required_enemy_types = {
            enemy_id: data["enemy_type"]
            for enemy_id, data in baseline_enemies.items()
        }
        supplemental_enemy_types = {
            enemy_id: enemy_type
            for enemy_id, enemy_type in enemy_types_by_id.items()
            if enemy_id.startswith("EnemySpawn_2F_DensityPatrol_")
        }

        self.assertEqual(self.model["entities"]["npc_spawns"], [])
        self.assertEqual(len(enemies), len(baseline_enemies) * ENEMY_DENSITY_MULTIPLIER)
        self.assertEqual(
            {enemy_id: enemy_types_by_id[enemy_id] for enemy_id in required_enemy_types},
            required_enemy_types,
        )
        self.assertEqual(len(supplemental_enemy_types), len(baseline_enemies) * (ENEMY_DENSITY_MULTIPLIER - 1))
        self.assertEqual(
            set(supplemental_enemy_types),
            {
                f"EnemySpawn_2F_DensityPatrol_{index:03d}"
                for index in range(1, len(supplemental_enemy_types) + 1)
            },
        )
        self.assertTrue(
            set(supplemental_enemy_types.values()).issubset(
                {
                    "cave_spider",
                    "skeleton_warrior",
                    "grave_hexer",
                    "bone_archer",
                    "iron_revenant",
                    "cursed_gargoyle",
                    "crypt_sentinel",
                }
            )
        )
        self.assertEqual(
            set(enemy_types_by_id) - set(required_enemy_types) - set(supplemental_enemy_types),
            set(),
        )

        stair_positions = {FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B, FLOOR2_UP_STAIR}
        for enemy in enemies:
            pos = (enemy["position"]["x"], enemy["position"]["y"])
            self.assertIn(pos, self.walkable)
            self.assertNotIn(pos, stair_positions)

    def test_treasure_boxes_are_authored_and_walkable(self):
        entities = self.model["entities"]
        treasure_boxes = {
            box["id"]: box
            for box in entities["treasure_boxes"]
        }
        occupied = set()
        for key in (
            "npc_spawns",
            "enemy_spawns",
            "stair_connections",
            "trap_tiles",
            "puzzle_switches",
            "puzzle_gates",
            "puzzle_riddles",
        ):
            occupied.update(entity_positions(entities, key).values())

        self.assertEqual(set(treasure_boxes), set(EXPECTED_FLOOR2_TREASURE))

        for box_id, (position, gold, items) in EXPECTED_FLOOR2_TREASURE.items():
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

    def test_puzzle_trap_chamber_entities_are_authored_and_walkable(self):
        entities = self.model["entities"]
        trap_tiles = entity_positions(entities, "trap_tiles")
        puzzle_switches = entity_positions(entities, "puzzle_switches")
        puzzle_gates = entity_positions(entities, "puzzle_gates")
        puzzle_riddles = entity_positions(entities, "puzzle_riddles")

        self.assertEqual(trap_tiles, {trap_id: data["position"] for trap_id, data in FLOOR2_PUZZLE_TRAPS.items()})
        self.assertEqual(
            puzzle_switches,
            {switch_id: data["position"] for switch_id, data in FLOOR2_PUZZLE_SWITCHES.items()},
        )
        self.assertEqual(puzzle_gates, {gate_id: data["position"] for gate_id, data in FLOOR2_PUZZLE_GATES.items()})
        self.assertEqual(
            puzzle_riddles,
            {riddle_id: data["position"] for riddle_id, data in FLOOR2_PUZZLE_RIDDLES.items()},
        )

        occupied = set()
        for key in ("enemy_spawns", "npc_spawns", "stair_connections", "treasure_boxes"):
            occupied.update(entity_positions(entities, key).values())

        all_puzzle_positions = [
            *trap_tiles.values(),
            *puzzle_switches.values(),
            *puzzle_gates.values(),
            *puzzle_riddles.values(),
        ]
        self.assertEqual(len(all_puzzle_positions), len(set(all_puzzle_positions)))

        for position in all_puzzle_positions:
            with self.subTest(position=position):
                self.assertIn(position, self.walkable)
                self.assertNotIn(position, occupied)

        for trap in entities["trap_tiles"]:
            self.assertEqual(trap["puzzle_id"], FLOOR2_PUZZLE_ID)
            self.assertEqual(trap["damage"], 14)
            self.assertEqual(trap.get("status_effect", ""), "")

        puzzle_switch = entities["puzzle_switches"][0]
        self.assertEqual(puzzle_switch["puzzle_id"], FLOOR2_PUZZLE_ID)
        self.assertEqual(puzzle_switch["prompt_text"], "Use")
        self.assertEqual(puzzle_switch["activated_text"], "The archive lock starts listening.")

        for gate in entities["puzzle_gates"]:
            self.assertEqual(gate["puzzle_id"], FLOOR2_PUZZLE_ID)
            self.assertTrue(gate["starts_closed"])

        riddle = entities["puzzle_riddles"][0]
        self.assertEqual(riddle["puzzle_id"], FLOOR2_PUZZLE_ID)
        self.assertEqual(riddle["correct_choice_id"], "lever_memory")
        self.assertEqual(riddle["wrong_answer_damage"], 14)
        self.assertEqual(
            riddle["prompt_text"],
            "The archive seal asks: what opens the vault without moving the stones?",
        )
        self.assertEqual(
            riddle["choices"],
            [
                {"id": "lever_memory", "label": "The remembered lever"},
                {"id": "broken_key", "label": "The broken key"},
                {"id": "silent_step", "label": "The silent step"},
            ],
        )

    def test_required_routes_remain_reachable_with_puzzle_gates_closed(self):
        gate_positions = set(entity_positions(self.model["entities"], "puzzle_gates").values())
        closed_gate_walkable = self.walkable - gate_positions

        for goal in [FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B, FLOOR2_UP_STAIR]:
            with self.subTest(goal=goal):
                self.assertTrue(has_path(closed_gate_walkable, FLOOR2_PLAYER_START, goal))

    def test_puzzle_gates_open_reward_and_shortcut_route(self):
        gate_positions = set(entity_positions(self.model["entities"], "puzzle_gates").values())
        closed_gate_walkable = self.walkable - gate_positions
        puzzle_room_side = (32, 38)
        reward = EXPECTED_FLOOR2_TREASURE["TreasureBox_2F_PuzzleVaultCache"][0]
        shortcut_payoff = (42, 52)

        self.assertFalse(has_path(closed_gate_walkable, puzzle_room_side, reward))
        self.assertTrue(has_path(self.walkable, puzzle_room_side, reward))

        closed_length = shortest_path_length(closed_gate_walkable, puzzle_room_side, shortcut_payoff)
        open_length = shortest_path_length(self.walkable, puzzle_room_side, shortcut_payoff)

        self.assertIsNotNone(closed_length)
        self.assertIsNotNone(open_length)
        self.assertGreaterEqual(closed_length - open_length, 12)

    def test_enemy_gates_block_main_up_stair_route_until_clearable(self):
        enemy_positions = set(entity_positions(self.model["entities"], "enemy_spawns").values())
        treasure_positions = set(entity_positions(self.model["entities"], "treasure_boxes").values())
        uncleared_walkable = self.walkable - enemy_positions - treasure_positions

        self.assertFalse(has_path(uncleared_walkable, FLOOR2_PLAYER_START, FLOOR2_UP_STAIR))
        self.assertTrue(has_path(self.walkable, FLOOR2_PLAYER_START, FLOOR2_UP_STAIR))

    def test_floor2_has_moderate_maze_structure(self):
        self.assertGreaterEqual(count_dead_end_cells(self.walkable, FLOOR2_WIDTH, FLOOR2_HEIGHT), 6)
        for position in [(18, 14), (36, 31), (24, 44), (51, 34), (42, 52)]:
            with self.subTest(position=position):
                self.assertIn(position, self.walkable)
                self.assertGreaterEqual(neighbor_count(self.walkable, position), 3)

    def test_floor2_dead_end_branches_have_payoff(self):
        self.assertEqual(
            unrewarded_dead_end_branches(self.model, self.walkable, FLOOR2_WIDTH, FLOOR2_HEIGHT),
            [],
        )

    def test_floor2_definition_arrays_include_return_and_up_stairs(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            floor_def = Path(tmpdir) / "Floor2F.tres"
            floor_def.write_text(floor_definition_source(), encoding="utf-8")

            update_floor_definition(floor_def, self.model)

            updated = floor_def.read_text(encoding="utf-8")
            self.assertIn("PlayerStartPosition = Vector2i(10, 10)", updated)
            self.assertIn("StairsUp = Array[Vector2i]([Vector2i(52, 50)])", updated)
            self.assertIn("StairsDown = Array[Vector2i]([Vector2i(10, 10), Vector2i(26, 10)])", updated)


class Floor3PlaceholderGeneratorTest(unittest.TestCase):
    def setUp(self):
        self.model = build_floor3_model()
        self.walkable = walkable_set(self.model)

    def test_generates_registered_future_landing_for_floor2_up_stair(self):
        ground = self.model["tile_layers"]["ground"]
        walls = self.model["tile_layers"]["wall"]
        stairs = self.model["tile_layers"]["stair"]

        self.assertEqual(FLOOR3_WIDTH, 24)
        self.assertEqual(FLOOR3_HEIGHT, 18)
        self.assertEqual(FLOOR3_PLAYER_START, FLOOR3_DOWN_STAIR)
        self.assertEqual(self.model["floor_metadata"]["player_start"], {"x": 10, "y": 10})
        self.assertEqual(len(ground), 432)
        assert_tiles_inside(self, ground, FLOOR3_WIDTH, FLOOR3_HEIGHT)
        assert_tiles_inside(self, walls, FLOOR3_WIDTH, FLOOR3_HEIGHT)
        assert_tiles_inside(self, stairs, FLOOR3_WIDTH, FLOOR3_HEIGHT)
        self.assertEqual({(tile["x"], tile["y"]) for tile in stairs}, {FLOOR3_DOWN_STAIR})
        self.assertEqual(
            self.model["entities"]["stair_connections"],
            [
                {
                    "id": "3F_2F_A",
                    "position": {"x": 10, "y": 10},
                    "direction": "down",
                    "target_floor": 2,
                    "destination_stair_id": "2F_3F_A",
                }
            ],
        )

        for key in (
            "enemy_spawns",
            "npc_spawns",
            "treasure_boxes",
            "trap_tiles",
            "puzzle_switches",
            "puzzle_gates",
            "puzzle_riddles",
        ):
            self.assertEqual(self.model["entities"][key], [])

        self.assertTrue(has_path(self.walkable, FLOOR3_PLAYER_START, FLOOR3_DOWN_STAIR))


if __name__ == "__main__":
    unittest.main()
