#!/usr/bin/env python3
"""Generate Floor 1, the playable Floor 2 maze, and a Floor 3 landing JSON for Sirius."""
# DEPRECATED: superseded by the Sirius Floor Tools addon + headless CLI.
# Regenerate floors with: godot --headless --path . --script tools/generate_floor.gd -- --floor N
# This file is retained only as a parity reference until C# generation is confirmed stable.

from __future__ import annotations

import argparse
import json
import re
from collections import deque
from pathlib import Path


FLOOR1_WIDTH = 60
FLOOR1_HEIGHT = 60
FLOOR2_WIDTH = 60
FLOOR2_HEIGHT = 60
FLOOR3_WIDTH = 24
FLOOR3_HEIGHT = 18
GRID_WIDTH = 160
GRID_HEIGHT = 160
ENEMY_DENSITY_MULTIPLIER = 3

FLOOR1_PLAYER_START = (8, 30)
FLOOR1_DOWN_STAIR = (8, 30)
FLOOR1_UP_STAIR_A = (49, 12)
FLOOR1_UP_STAIR_B = (48, 48)

FLOOR2_PLAYER_START = (10, 10)
FLOOR2_DOWN_STAIR_A = (10, 10)
FLOOR2_DOWN_STAIR_B = (26, 10)
FLOOR2_UP_STAIR = (52, 50)

FLOOR3_PLAYER_START = (10, 10)
FLOOR3_DOWN_STAIR = (10, 10)

FLOOR1_HIDDEN_PLACEHOLDERS = {
    "hidden_room_north": (16, 8),
    "hidden_shortcut_east": (56, 30),
}

FLOOR1_SOUTH_SHORTCUT_ENTRY = (19, 54)

FLOOR1_ENEMY_GATES = {
    "EnemySpawn_Goblin_Branch": {"position": (16, 23), "enemy_type": "goblin"},
    "EnemySpawn_Orc_Central": {"position": (22, 30), "enemy_type": "orc"},
    "EnemySpawn_Skeleton_StairA": {"position": (43, 12), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_ForestSpirit_StairB": {"position": (42, 48), "enemy_type": "forest_spirit"},
    "EnemySpawn_Orc_HiddenBranch": {"position": (19, 51), "enemy_type": "orc"},
    "EnemySpawn_Skeleton_NorthShortcut": {"position": (36, 6), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_ForestSpirit_EastShortcut": {"position": (54, 56), "enemy_type": "forest_spirit"},
    "EnemySpawn_Orc_SouthShortcut": {"position": (32, 58), "enemy_type": "orc"},
}

FLOOR1_EXTRA_ENEMY_PATROLS = {
    "EnemySpawn_Goblin_WestDeadEnd": {"position": (5, 22), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_SideRoom": {"position": (18, 22), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_SouthwestSpur": {"position": (5, 54), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_WestLoop": {"position": (7, 42), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_NorthRoom": {"position": (8, 4), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_NorthBranch": {"position": (27, 8), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_CentralSouth": {"position": (28, 40), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_SouthLoop": {"position": (23, 58), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_EastSwitchback": {"position": (58, 50), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_EastCorridor": {"position": (56, 34), "enemy_type": "goblin"},
    "EnemySpawn_Goblin_CentralHall": {"position": (12, 28), "enemy_type": "goblin"},
    "EnemySpawn_Orc_WestCrossing": {"position": (13, 37), "enemy_type": "orc"},
    "EnemySpawn_Orc_NorthConnector": {"position": (30, 17), "enemy_type": "orc"},
    "EnemySpawn_Orc_NortheastBend": {"position": (34, 22), "enemy_type": "orc"},
    "EnemySpawn_Orc_EastHall": {"position": (44, 24), "enemy_type": "orc"},
    "EnemySpawn_Orc_EastLoop": {"position": (52, 34), "enemy_type": "orc"},
    "EnemySpawn_Orc_SoutheastSwitchback": {"position": (56, 46), "enemy_type": "orc"},
    "EnemySpawn_Orc_SouthBend": {"position": (35, 54), "enemy_type": "orc"},
    "EnemySpawn_Orc_SouthLoopEast": {"position": (42, 58), "enemy_type": "orc"},
    "EnemySpawn_Orc_CentralLower": {"position": (32, 34), "enemy_type": "orc"},
    "EnemySpawn_Skeleton_NorthDeadEnd": {"position": (49, 5), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_Skeleton_NorthShortcutBend": {"position": (38, 7), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_Skeleton_UpperConnector": {"position": (27, 11), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_Skeleton_EastSpur": {"position": (47, 35), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_Skeleton_CentralSpur": {"position": (38, 39), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_Skeleton_SouthSpur": {"position": (12, 49), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_ForestSpirit_EastSwitchback": {"position": (54, 58), "enemy_type": "forest_spirit"},
    "EnemySpawn_ForestSpirit_SouthGallery": {"position": (39, 44), "enemy_type": "forest_spirit"},
}

FLOOR1_SUPPLEMENTAL_ENEMY_PREFIX = "EnemySpawn_1F_DensityPatrol"
FLOOR1_SUPPLEMENTAL_ENEMY_TYPES = (
    "goblin",
    "orc",
    "skeleton_warrior",
    "forest_spirit",
)

FLOOR1_TREASURE_BOXES = {
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

FLOOR2_ENEMY_GATES = {
    "EnemySpawn_2F_ArchiveGate": {"position": (34, 14), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_2F_GalleryGate": {"position": (52, 34), "enemy_type": "grave_hexer"},
    "EnemySpawn_2F_UpStairGuard": {"position": (49, 50), "enemy_type": "crypt_sentinel"},
    "EnemySpawn_2F_PuzzleApproach": {"position": (29, 34), "enemy_type": "cave_spider"},
}

FLOOR2_EXTRA_ENEMY_PATROLS = {
    "EnemySpawn_2F_WestSupply": {"position": (8, 16), "enemy_type": "cave_spider"},
    "EnemySpawn_2F_WestLoop": {"position": (27, 18), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_2F_NorthStudy": {"position": (44, 12), "enemy_type": "grave_hexer"},
    "EnemySpawn_2F_NorthStacks": {"position": (18, 28), "enemy_type": "bone_archer"},
    "EnemySpawn_2F_PuzzleSide": {"position": (24, 38), "enemy_type": "cave_spider"},
    "EnemySpawn_2F_WestReadingRoom": {"position": (30, 24), "enemy_type": "skeleton_warrior"},
    "EnemySpawn_2F_CentralArchive": {"position": (36, 31), "enemy_type": "bone_archer"},
    "EnemySpawn_2F_EastStacks": {"position": (40, 40), "enemy_type": "iron_revenant"},
    "EnemySpawn_2F_SouthShortcut": {"position": (41, 44), "enemy_type": "grave_hexer"},
    "EnemySpawn_2F_EastDeadEnd": {"position": (44, 34), "enemy_type": "cursed_gargoyle"},
    "EnemySpawn_2F_UpperAlcove": {"position": (48, 24), "enemy_type": "bone_archer"},
    "EnemySpawn_2F_EastGallery": {"position": (55, 34), "enemy_type": "iron_revenant"},
    "EnemySpawn_2F_LowerWatch": {"position": (55, 46), "enemy_type": "iron_revenant"},
    "EnemySpawn_2F_SouthApproach": {"position": (24, 46), "enemy_type": "cave_spider"},
    "EnemySpawn_2F_SouthArmory": {"position": (42, 53), "enemy_type": "iron_revenant"},
    "EnemySpawn_2F_StairWatch": {"position": (52, 48), "enemy_type": "cursed_gargoyle"},
}

FLOOR2_SUPPLEMENTAL_ENEMY_PREFIX = "EnemySpawn_2F_DensityPatrol"
FLOOR2_SUPPLEMENTAL_ENEMY_TYPES = (
    "cave_spider",
    "skeleton_warrior",
    "grave_hexer",
    "bone_archer",
    "iron_revenant",
    "cursed_gargoyle",
    "crypt_sentinel",
)

FLOOR2_TREASURE_BOXES = {
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

FLOOR2_PUZZLE_ID = "Puzzle_2F_EastArchiveTrial"

FLOOR2_PUZZLE_TRAPS = {
    "TrapTile_2F_ArchiveTrial_01": {"position": (29, 35), "damage": 14},
    "TrapTile_2F_ArchiveTrial_02": {"position": (30, 36), "damage": 14},
    "TrapTile_2F_ArchiveTrial_03": {"position": (31, 39), "damage": 14},
}

FLOOR2_PUZZLE_SWITCHES = {
    "PuzzleSwitch_2F_ArchiveTrial_Lever": {
        "position": (27, 34),
        "prompt_text": "Use",
        "activated_text": "The archive lock starts listening.",
    }
}

FLOOR2_PUZZLE_GATES = {
    "PuzzleGate_2F_ArchiveTrial_Vault": {"position": (33, 38), "starts_closed": True},
    "PuzzleGate_2F_ArchiveTrial_Shortcut": {"position": (38, 44), "starts_closed": True},
}

FLOOR2_PUZZLE_RIDDLES = {
    "PuzzleRiddle_2F_ArchiveTrial_Seal": {
        "position": (32, 36),
        "prompt_text": "The archive seal asks: what opens the vault without moving the stones?",
        "choices": [
            {"id": "lever_memory", "label": "The remembered lever"},
            {"id": "broken_key", "label": "The broken key"},
            {"id": "silent_step", "label": "The silent step"},
        ],
        "correct_choice_id": "lever_memory",
        "wrong_answer_damage": 14,
    }
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


def add_gate_barrier(walls: set[tuple[int, int]], gate: tuple[int, int], blocked_cells: list[tuple[int, int]]) -> None:
    for cell in blocked_cells:
        if cell != gate:
            walls.add(cell)


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


def wall_tiles(
    walls: set[tuple[int, int]],
    width: int,
    height: int,
    include_outside_footprint: bool = True,
) -> list[dict[str, int | str]]:
    all_walls = set(walls)
    if include_outside_footprint:
        all_walls |= outside_footprint_walls(width, height)
    return [{"x": x, "y": y, "tile": "generic"} for x, y in sorted(all_walls, key=lambda p: (p[1], p[0]))]


def treasure_box_entities(boxes: dict[str, tuple[tuple[int, int], int, dict[str, int]]]) -> list[dict]:
    return [
        {
            "id": box_id,
            "position": vector(*position),
            "gold": gold,
            "items": [
                {"item_id": item_id, "quantity": quantity}
                for item_id, quantity in items.items()
            ],
        }
        for box_id, (position, gold, items) in boxes.items()
    ]


def trap_tile_entities(traps: dict[str, dict], puzzle_id: str) -> list[dict]:
    return [
        {
            "id": trap_id,
            "puzzle_id": puzzle_id,
            "position": vector(*data["position"]),
            "damage": data["damage"],
            "status_effect": data.get("status_effect", ""),
            "status_magnitude": data.get("status_magnitude", 0),
            "status_turns": data.get("status_turns", 0),
        }
        for trap_id, data in traps.items()
    ]


def puzzle_switch_entities(switches: dict[str, dict], puzzle_id: str) -> list[dict]:
    return [
        {
            "id": switch_id,
            "puzzle_id": puzzle_id,
            "position": vector(*data["position"]),
            "prompt_text": data["prompt_text"],
            "activated_text": data["activated_text"],
        }
        for switch_id, data in switches.items()
    ]


def puzzle_gate_entities(gates: dict[str, dict], puzzle_id: str) -> list[dict]:
    return [
        {
            "id": gate_id,
            "puzzle_id": puzzle_id,
            "position": vector(*data["position"]),
            "starts_closed": data["starts_closed"],
        }
        for gate_id, data in gates.items()
    ]


def puzzle_riddle_entities(riddles: dict[str, dict], puzzle_id: str) -> list[dict]:
    return [
        {
            "id": riddle_id,
            "puzzle_id": puzzle_id,
            "position": vector(*data["position"]),
            "prompt_text": data["prompt_text"],
            "choices": data["choices"],
            "correct_choice_id": data["correct_choice_id"],
            "wrong_answer_damage": data["wrong_answer_damage"],
        }
        for riddle_id, data in riddles.items()
    ]


def walkable_from_walls(walls: set[tuple[int, int]], width: int, height: int) -> set[tuple[int, int]]:
    return {
        (x, y)
        for y in range(height)
        for x in range(width)
        if (x, y) not in walls
    }


def enemy_data_positions(enemies: dict[str, dict]) -> set[tuple[int, int]]:
    return {data["position"] for data in enemies.values()}


def authored_position_values(entities: dict[str, dict]) -> set[tuple[int, int]]:
    return {data["position"] for data in entities.values()}


def treasure_positions(boxes: dict[str, tuple[tuple[int, int], int, dict[str, int]]]) -> set[tuple[int, int]]:
    return {position for position, _, _ in boxes.values()}


def build_supplemental_enemy_patrols(
    prefix: str,
    base_enemies: dict[str, dict],
    walkable: set[tuple[int, int]],
    occupied: set[tuple[int, int]],
    enemy_types: tuple[str, ...],
) -> dict[str, dict]:
    target_count = len(base_enemies) * (ENEMY_DENSITY_MULTIPLIER - 1)
    occupied = set(occupied)
    supplemental: dict[str, dict] = {}
    selected_positions: list[tuple[int, int]] = []
    candidates = [
        position
        for position in sorted(
            walkable,
            key=lambda pos: ((pos[0] * 73 + pos[1] * 37) % 997, pos[1], pos[0]),
        )
        if position not in occupied and walkable_neighbor_count(walkable, position) >= 2
    ]

    for min_distance in (4, 3, 2, 1):
        for position in candidates:
            if len(supplemental) == target_count:
                break
            if position in occupied:
                continue
            if any(
                abs(position[0] - selected[0]) + abs(position[1] - selected[1]) < min_distance
                for selected in selected_positions
            ):
                continue

            index = len(supplemental) + 1
            supplemental[f"{prefix}_{index:03d}"] = {
                "position": position,
                "enemy_type": enemy_types[(index - 1) % len(enemy_types)],
            }
            occupied.add(position)
            selected_positions.append(position)

        if len(supplemental) == target_count:
            break

    if len(supplemental) != target_count:
        raise ValueError(
            f"Could only place {len(supplemental)} supplemental enemies for {prefix}; needed {target_count}"
        )

    return supplemental


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
    builder.carve_path((28, 50), FLOOR1_SOUTH_SHORTCUT_ENTRY, half_width=1)

    builder.carve_rect(13, 6, 19, 10)
    builder.carve_rect(53, 28, 58, 32)
    builder.carve_rect(16, 52, 22, 56)

    dead_end_branches = [
        ((11, 22), (5, 22)),
        ((28, 26), (28, 20)),
        ((32, 34), (38, 39)),
        ((49, 9), (49, 5)),
        ((53, 35), (47, 35)),
        ((56, 30), (56, 36)),
        ((28, 50), (35, 55)),
        ((7, 42), (2, 42)),
        ((12, 49), (5, 54)),
        ((38, 12), (38, 7)),
    ]
    for start, end in dead_end_branches:
        builder.carve_path(start, end, half_width=0)

    decision_connectors = [
        ("h", 5, 14, 37),
        ("v", 31, 41, 12),
        ("h", 11, 15, 28),
        ("h", 19, 38, 8),
        ("h", 17, 33, 11),
        ("v", 8, 15, 28),
        ("h", 49, 56, 34),
        ("v", 31, 45, 52),
        ("h", 49, 53, 32),
        ("v", 31, 35, 50),
    ]
    for direction, start, end, fixed in decision_connectors:
        if direction == "h":
            builder.carve_h_corridor(start, end, fixed, half_width=0)
        else:
            builder.carve_v_corridor(start, end, fixed, half_width=0)

    shortcut_branches = [
        [
            FLOOR1_HIDDEN_PLACEHOLDERS["hidden_room_north"],
            (8, 8),
            (8, 4),
            (36, 4),
            (36, 8),
            (38, 8),
        ],
        [
            FLOOR1_HIDDEN_PLACEHOLDERS["hidden_shortcut_east"],
            (58, 46),
            (56, 46),
            (56, 48),
            (58, 48),
            (58, 50),
            (56, 50),
            (56, 52),
            (58, 52),
            (58, 54),
            (56, 54),
            (56, 56),
            (58, 56),
            (58, 58),
            (54, 58),
            (54, 46),
            (58, 46),
        ],
        [
            FLOOR1_SOUTH_SHORTCUT_ENTRY,
            (23, 58),
            (23, 56),
            (58, 56),
            (58, 58),
            (42, 58),
            (23, 58),
        ],
    ]
    for branch in shortcut_branches:
        for start, end in zip(branch, branch[1:]):
            builder.carve_path(start, end, half_width=0)

    wall_relief_paths = [
        ((5, 22), (4, 22)),
        ((30, 17), (30, 19)),
        ((34, 26), (34, 22)),
        ((52, 24), (44, 24)),
        ((39, 34), (43, 34)),
        ((48, 35), (44, 35)),
        ((12, 40), (28, 40)),
        ((12, 41), (28, 41)),
        ((13, 42), (28, 42)),
        ((13, 43), (28, 43)),
        ((13, 44), (28, 44)),
        ((47, 42), (39, 42)),
        ((47, 43), (39, 43)),
        ((47, 44), (39, 44)),
        ((47, 45), (39, 45)),
        ((13, 46), (28, 46)),
        ((38, 56), (38, 55)),
    ]
    for start, end in wall_relief_paths:
        builder.carve_path(start, end, half_width=0)

    builder.walls.update((x, 16) for x in range(48, 55))
    builder.walls.add((19, 8))
    builder.walls.add((35, 55))
    builder.walls.add((25, 56))

    add_gate_barrier(
        builder.walls,
        FLOOR1_ENEMY_GATES["EnemySpawn_Goblin_Branch"]["position"],
        [(x, 23) for x in range(11, 19)],
    )
    add_gate_barrier(builder.walls, FLOOR1_ENEMY_GATES["EnemySpawn_Orc_Central"]["position"], [(22, 29), (22, 30), (22, 31)])
    add_gate_barrier(builder.walls, FLOOR1_ENEMY_GATES["EnemySpawn_Skeleton_StairA"]["position"], [(43, 11), (43, 12), (43, 13)])
    add_gate_barrier(builder.walls, FLOOR1_ENEMY_GATES["EnemySpawn_ForestSpirit_StairB"]["position"], [(42, 47), (42, 48), (42, 49)])
    add_gate_barrier(
        builder.walls,
        FLOOR1_ENEMY_GATES["EnemySpawn_Orc_HiddenBranch"]["position"],
        [(x, 51) for x in range(16, 23)],
    )

    builder.reinforce_perimeter()
    return builder.walls


def build_floor2_walls() -> set[tuple[int, int]]:
    builder = MazeBuilder(FLOOR2_WIDTH, FLOOR2_HEIGHT)

    main_loop = [
        FLOOR2_DOWN_STAIR_A,
        (18, 14),
        (34, 14),
        (48, 20),
        (52, 34),
        FLOOR2_UP_STAIR,
        (38, 52),
        (24, 44),
        (16, 32),
        FLOOR2_DOWN_STAIR_A,
    ]
    builder.carve_loop(main_loop, half_width=1)

    assert FLOOR2_DOWN_STAIR_A[1] == FLOOR2_DOWN_STAIR_B[1], (
        f"carve_h_corridor requires shared y: A={FLOOR2_DOWN_STAIR_A}, B={FLOOR2_DOWN_STAIR_B}"
    )
    builder.carve_h_corridor(FLOOR2_DOWN_STAIR_A[0], FLOOR2_DOWN_STAIR_B[0], FLOOR2_DOWN_STAIR_A[1], half_width=1)
    builder.carve_path(FLOOR2_DOWN_STAIR_B, (34, 14), half_width=1)

    builder.carve_rect(7, 7, 13, 13)
    builder.carve_rect(23, 7, 29, 13)
    builder.carve_rect(3, 14, 9, 18)
    builder.carve_rect(26, 27, 37, 36)
    builder.carve_rect(41, 7, 49, 15)
    builder.carve_rect(50, 29, 57, 37)
    builder.carve_rect(38, 49, 55, 56)

    builder.carve_path((10, 13), (6, 16), half_width=0)
    builder.carve_path((34, 14), (44, 8), half_width=0)
    builder.carve_path((34, 14), (36, 31), half_width=1)
    builder.carve_path((36, 31), (29, 34), half_width=1)
    builder.carve_path((52, 34), (56, 36), half_width=0)
    builder.carve_path((38, 52), (42, 55), half_width=0)
    builder.carve_path(FLOOR2_UP_STAIR, (53, 48), half_width=0)

    # Optional puzzle branch: left chamber, locked vault, and a second gate for the shortcut exit.
    builder.carve_rect(27, 34, 32, 40)
    builder.carve_rect(34, 37, 36, 39)
    builder.carve_cell(*FLOOR2_PUZZLE_GATES["PuzzleGate_2F_ArchiveTrial_Vault"]["position"])
    builder.carve_path((36, 38), (38, 44), half_width=0)
    builder.carve_path((38, 44), (42, 52), half_width=0)

    side_branches = [
        ((18, 14), (18, 6)),
        ((24, 44), (16, 52)),
        ((52, 34), (56, 28)),
        ((42, 52), (34, 56)),
        ((16, 32), (7, 32)),
        ((26, 10), (26, 5)),
        ((44, 12), (50, 18)),
    ]
    for start, end in side_branches:
        builder.carve_path(start, end, half_width=0)

    decision_connectors = [
        ("h", 12, 22, 18),
        ("v", 14, 28, 18),
        ("h", 18, 34, 18),
        ("h", 30, 44, 24),
        ("v", 24, 34, 44),
        ("h", 40, 52, 40),
        ("v", 36, 46, 50),
        ("h", 30, 42, 52),
        ("v", 38, 52, 24),
        ("h", 24, 36, 44),
    ]
    for direction, start, end, fixed in decision_connectors:
        if direction == "h":
            builder.carve_h_corridor(start, end, fixed, half_width=0)
        else:
            builder.carve_v_corridor(start, end, fixed, half_width=0)

    wall_relief_paths = [
        ((6, 16), (3, 16)),
        ((18, 6), (18, 4)),
        ((44, 8), (47, 8)),
        ((44, 24), (48, 24)),
        ((56, 28), (56, 24)),
        ((7, 32), (4, 32)),
        ((16, 52), (13, 55)),
        ((34, 56), (30, 56)),
        ((50, 46), (55, 46)),
    ]
    for start, end in wall_relief_paths:
        builder.carve_path(start, end, half_width=0)

    shortcut_loop_cuts = [
        ((13, 55), (30, 56)),
        ((18, 28), (24, 38)),
        ((35, 44), (41, 44)),
        ((44, 34), (48, 24)),
        ((26, 5), (18, 4)),
    ]
    for start, end in shortcut_loop_cuts:
        builder.carve_path(start, end, half_width=0)

    add_gate_barrier(
        builder.walls,
        FLOOR2_ENEMY_GATES["EnemySpawn_2F_ArchiveGate"]["position"],
        [(x, y) for x in range(30, 38) for y in (13, 15)],
    )
    add_gate_barrier(
        builder.walls,
        FLOOR2_EXTRA_ENEMY_PATROLS["EnemySpawn_2F_WestLoop"]["position"],
        [(x, y) for x in range(23, 32) for y in (17, 19)],
    )
    add_gate_barrier(
        builder.walls,
        FLOOR2_ENEMY_GATES["EnemySpawn_2F_GalleryGate"]["position"],
        [(52, y) for y in range(30, 38)],
    )
    add_gate_barrier(
        builder.walls,
        FLOOR2_ENEMY_GATES["EnemySpawn_2F_UpStairGuard"]["position"],
        [(x, 50) for x in range(47, 52)],
    )
    add_gate_barrier(
        builder.walls,
        FLOOR2_EXTRA_ENEMY_PATROLS["EnemySpawn_2F_SouthApproach"]["position"],
        [
            *[(x, 45) for x in range(24, 29)],
            *[(x, 47) for x in range(23, 29)],
        ],
    )
    add_gate_barrier(
        builder.walls,
        FLOOR2_EXTRA_ENEMY_PATROLS["EnemySpawn_2F_SouthArmory"]["position"],
        [
            *[(x, y) for x in range(37, 42) for y in (51, 52)],
            (37, 50),
            (41, 54),
        ],
    )
    add_gate_barrier(
        builder.walls,
        FLOOR2_ENEMY_GATES["EnemySpawn_2F_PuzzleApproach"]["position"],
        [(x, 34) for x in range(28, 33)],
    )

    add_gate_barrier(
        builder.walls,
        FLOOR2_PUZZLE_GATES["PuzzleGate_2F_ArchiveTrial_Vault"]["position"],
        [(33, y) for y in range(35, 41)],
    )
    builder.walls.update((x, 33) for x in range(33, 37))
    builder.walls.update((35, y) for y in range(34, 38))
    builder.walls.update((x, 37) for x in range(34, 37))
    builder.walls.update({(33, 34), (34, 34), (34, 35), (34, 36)})
    add_gate_barrier(
        builder.walls,
        FLOOR2_PUZZLE_GATES["PuzzleGate_2F_ArchiveTrial_Shortcut"]["position"],
        [FLOOR2_PUZZLE_GATES["PuzzleGate_2F_ArchiveTrial_Shortcut"]["position"]],
    )

    builder.reinforce_perimeter()
    return builder.walls


def build_floor3_walls() -> set[tuple[int, int]]:
    builder = MazeBuilder(FLOOR3_WIDTH, FLOOR3_HEIGHT)
    builder.carve_rect(6, 6, 16, 13)
    builder.carve_h_corridor(8, 14, FLOOR3_DOWN_STAIR[1], half_width=1)
    builder.carve_v_corridor(8, 12, FLOOR3_DOWN_STAIR[0], half_width=1)
    builder.reinforce_perimeter()
    return builder.walls


def build_floor1_model() -> dict:
    floor1_walls = build_floor1_walls()
    floor1_walkable = walkable_from_walls(floor1_walls, FLOOR1_WIDTH, FLOOR1_HEIGHT)
    base_enemies = FLOOR1_ENEMY_GATES | FLOOR1_EXTRA_ENEMY_PATROLS
    occupied = (
        {FLOOR1_PLAYER_START, FLOOR1_DOWN_STAIR, FLOOR1_UP_STAIR_A, FLOOR1_UP_STAIR_B}
        | set(FLOOR1_HIDDEN_PLACEHOLDERS.values())
        | enemy_data_positions(base_enemies)
        | treasure_positions(FLOOR1_TREASURE_BOXES)
        | authored_position_values(FLOOR1_PUZZLE_TRAPS)
        | authored_position_values(FLOOR1_PUZZLE_SWITCHES)
        | authored_position_values(FLOOR1_PUZZLE_GATES)
        | authored_position_values(FLOOR1_PUZZLE_RIDDLES)
    )
    enemy_spawns = base_enemies | build_supplemental_enemy_patrols(
        FLOOR1_SUPPLEMENTAL_ENEMY_PREFIX,
        base_enemies,
        floor1_walkable,
        occupied,
        FLOOR1_SUPPLEMENTAL_ENEMY_TYPES,
    )

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
            "wall": wall_tiles(
                floor1_walls,
                FLOOR1_WIDTH,
                FLOOR1_HEIGHT,
                include_outside_footprint=False,
            ),
            "stair": [
                {"x": FLOOR1_DOWN_STAIR[0], "y": FLOOR1_DOWN_STAIR[1], "tile": "down"},
                {"x": FLOOR1_UP_STAIR_A[0], "y": FLOOR1_UP_STAIR_A[1], "tile": "up"},
                {"x": FLOOR1_UP_STAIR_B[0], "y": FLOOR1_UP_STAIR_B[1], "tile": "up"},
            ],
        },
        "entities": {
            "enemy_spawns": [
                {
                    "id": enemy_id,
                    "position": vector(*data["position"]),
                    "enemy_type": data["enemy_type"],
                }
                for enemy_id, data in enemy_spawns.items()
            ],
            "npc_spawns": [],
            "stair_connections": [
                {
                    "id": "1F_001",
                    "position": vector(*FLOOR1_DOWN_STAIR),
                    "direction": "down",
                    "target_floor": 0,
                    "destination_stair_id": "GF_000",
                },
                {
                    "id": "1F_2F_A",
                    "position": vector(*FLOOR1_UP_STAIR_A),
                    "direction": "up",
                    "target_floor": 2,
                    "destination_stair_id": "2F_1F_A",
                },
                {
                    "id": "1F_2F_B",
                    "position": vector(*FLOOR1_UP_STAIR_B),
                    "direction": "up",
                    "target_floor": 2,
                    "destination_stair_id": "2F_1F_B",
                },
            ],
            "hidden_placeholders": [
                {"id": key, "position": vector(*pos)}
                for key, pos in FLOOR1_HIDDEN_PLACEHOLDERS.items()
            ],
            "treasure_boxes": treasure_box_entities(FLOOR1_TREASURE_BOXES),
            "trap_tiles": trap_tile_entities(FLOOR1_PUZZLE_TRAPS, FLOOR1_PUZZLE_ID),
            "puzzle_switches": puzzle_switch_entities(FLOOR1_PUZZLE_SWITCHES, FLOOR1_PUZZLE_ID),
            "puzzle_gates": puzzle_gate_entities(FLOOR1_PUZZLE_GATES, FLOOR1_PUZZLE_ID),
            "puzzle_riddles": puzzle_riddle_entities(FLOOR1_PUZZLE_RIDDLES, FLOOR1_PUZZLE_ID),
        },
    }
    validate_model(model, FLOOR1_WIDTH, FLOOR1_HEIGHT)
    return model


def build_floor2_model() -> dict:
    floor2_walls = build_floor2_walls()
    floor2_walkable = walkable_from_walls(floor2_walls, FLOOR2_WIDTH, FLOOR2_HEIGHT)
    base_enemies = FLOOR2_ENEMY_GATES | FLOOR2_EXTRA_ENEMY_PATROLS
    occupied = (
        {FLOOR2_PLAYER_START, FLOOR2_DOWN_STAIR_A, FLOOR2_DOWN_STAIR_B, FLOOR2_UP_STAIR}
        | enemy_data_positions(base_enemies)
        | treasure_positions(FLOOR2_TREASURE_BOXES)
        | authored_position_values(FLOOR2_PUZZLE_TRAPS)
        | authored_position_values(FLOOR2_PUZZLE_SWITCHES)
        | authored_position_values(FLOOR2_PUZZLE_GATES)
        | authored_position_values(FLOOR2_PUZZLE_RIDDLES)
    )
    enemy_spawns = base_enemies | build_supplemental_enemy_patrols(
        FLOOR2_SUPPLEMENTAL_ENEMY_PREFIX,
        base_enemies,
        floor2_walkable,
        occupied,
        FLOOR2_SUPPLEMENTAL_ENEMY_TYPES,
    )

    model = {
        "schema_version": "1.0",
        "floor_metadata": {
            "floor_name": "Second Floor",
            "floor_number": 2,
            "description": "A moderate archive maze with two 1/F return stairs, one 3/F stair, treasure, and a puzzle-gated side chamber.",
            "player_start": vector(*FLOOR2_PLAYER_START),
        },
        "tile_layers": {
            "ground": ground_tiles(FLOOR2_WIDTH, FLOOR2_HEIGHT),
            "wall": wall_tiles(
                floor2_walls,
                FLOOR2_WIDTH,
                FLOOR2_HEIGHT,
                include_outside_footprint=False,
            ),
            "stair": [
                {"x": FLOOR2_DOWN_STAIR_A[0], "y": FLOOR2_DOWN_STAIR_A[1], "tile": "down"},
                {"x": FLOOR2_DOWN_STAIR_B[0], "y": FLOOR2_DOWN_STAIR_B[1], "tile": "down"},
                {"x": FLOOR2_UP_STAIR[0], "y": FLOOR2_UP_STAIR[1], "tile": "up"},
            ],
        },
        "entities": {
            "enemy_spawns": [
                {
                    "id": enemy_id,
                    "position": vector(*data["position"]),
                    "enemy_type": data["enemy_type"],
                }
                for enemy_id, data in enemy_spawns.items()
            ],
            "npc_spawns": [],
            "stair_connections": [
                {
                    "id": "2F_1F_A",
                    "position": vector(*FLOOR2_DOWN_STAIR_A),
                    "direction": "down",
                    "target_floor": 1,
                    "destination_stair_id": "1F_2F_A",
                },
                {
                    "id": "2F_1F_B",
                    "position": vector(*FLOOR2_DOWN_STAIR_B),
                    "direction": "down",
                    "target_floor": 1,
                    "destination_stair_id": "1F_2F_B",
                },
                {
                    "id": "2F_3F_A",
                    "position": vector(*FLOOR2_UP_STAIR),
                    "direction": "up",
                    "target_floor": 3,
                    "destination_stair_id": "3F_2F_A",
                },
            ],
            "hidden_placeholders": [],
            "treasure_boxes": treasure_box_entities(FLOOR2_TREASURE_BOXES),
            "trap_tiles": trap_tile_entities(FLOOR2_PUZZLE_TRAPS, FLOOR2_PUZZLE_ID),
            "puzzle_switches": puzzle_switch_entities(FLOOR2_PUZZLE_SWITCHES, FLOOR2_PUZZLE_ID),
            "puzzle_gates": puzzle_gate_entities(FLOOR2_PUZZLE_GATES, FLOOR2_PUZZLE_ID),
            "puzzle_riddles": puzzle_riddle_entities(FLOOR2_PUZZLE_RIDDLES, FLOOR2_PUZZLE_ID),
        },
    }
    validate_model(model, FLOOR2_WIDTH, FLOOR2_HEIGHT)
    return model


def build_floor3_model() -> dict:
    model = {
        "schema_version": "1.0",
        "floor_metadata": {
            "floor_name": "Third Floor",
            "floor_number": 3,
            "description": "A safe future landing for the second-floor up stair.",
            "player_start": vector(*FLOOR3_PLAYER_START),
        },
        "tile_layers": {
            "ground": ground_tiles(FLOOR3_WIDTH, FLOOR3_HEIGHT),
            "wall": wall_tiles(
                build_floor3_walls(),
                FLOOR3_WIDTH,
                FLOOR3_HEIGHT,
                include_outside_footprint=False,
            ),
            "stair": [
                {"x": FLOOR3_DOWN_STAIR[0], "y": FLOOR3_DOWN_STAIR[1], "tile": "down"},
            ],
        },
        "entities": {
            "enemy_spawns": [],
            "npc_spawns": [],
            "stair_connections": [
                {
                    "id": "3F_2F_A",
                    "position": vector(*FLOOR3_DOWN_STAIR),
                    "direction": "down",
                    "target_floor": 2,
                    "destination_stair_id": "2F_3F_A",
                }
            ],
            "hidden_placeholders": [],
            "treasure_boxes": [],
            "trap_tiles": [],
            "puzzle_switches": [],
            "puzzle_gates": [],
            "puzzle_riddles": [],
        },
    }
    validate_model(model, FLOOR3_WIDTH, FLOOR3_HEIGHT)
    return model


def walkable_cells(model: dict, width: int, height: int) -> set[tuple[int, int]]:
    walls = {(tile["x"], tile["y"]) for tile in model["tile_layers"]["wall"]}
    return {
        (x, y)
        for y in range(height)
        for x in range(width)
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


def walkable_neighbors(walkable: set[tuple[int, int]], position: tuple[int, int]) -> list[tuple[int, int]]:
    x, y = position
    return [
        (nx, ny)
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
        if (nx, ny) in walkable
    ]


def walkable_neighbor_count(walkable: set[tuple[int, int]], position: tuple[int, int]) -> int:
    return len(walkable_neighbors(walkable, position))


def dead_end_branches(
    walkable: set[tuple[int, int]],
    width: int,
    height: int,
) -> list[list[tuple[int, int]]]:
    branches: list[list[tuple[int, int]]] = []
    for leaf in sorted(walkable):
        x, y = leaf
        if x >= width or y >= height:
            continue

        if walkable_neighbor_count(walkable, leaf) != 1:
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
            if walkable_neighbor_count(walkable, next_cell) != 2:
                break

            branch.append(next_cell)
            previous = current
            current = next_cell

        branches.append(branch)

    return branches


ENTITY_POSITION_KEYS = (
    "enemy_spawns",
    "npc_spawns",
    "stair_connections",
    "hidden_placeholders",
    "treasure_boxes",
    "trap_tiles",
    "puzzle_switches",
    "puzzle_gates",
    "puzzle_riddles",
)


def entity_position(entity: dict) -> tuple[int, int]:
    pos = entity["position"]
    return pos["x"], pos["y"]


BRANCH_PAYOFF_POSITION_KEYS = (
    "enemy_spawns",
    "treasure_boxes",
    "stair_connections",
    "hidden_placeholders",
    "trap_tiles",
    "puzzle_switches",
    "puzzle_gates",
    "puzzle_riddles",
)


def branch_payoff_positions(model: dict) -> set[tuple[int, int]]:
    positions: set[tuple[int, int]] = set()
    for key in BRANCH_PAYOFF_POSITION_KEYS:
        positions.update(entity_position(entity) for entity in model["entities"].get(key, []))

    return positions


def unrewarded_dead_end_branches(
    model: dict,
    walkable: set[tuple[int, int]],
    width: int,
    height: int,
) -> list[list[tuple[int, int]]]:
    payoff_positions = branch_payoff_positions(model)
    unrewarded: list[list[tuple[int, int]]] = []
    for branch in dead_end_branches(walkable, width, height):
        adjacent: set[tuple[int, int]] = set()
        for cell in branch:
            adjacent.update(walkable_neighbors(walkable, cell))

        if payoff_positions.isdisjoint(set(branch) | adjacent):
            unrewarded.append(branch)

    return unrewarded


def validate_model(model: dict, width: int, height: int) -> None:
    walkable = walkable_cells(model, width, height)
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
    seen_ids: dict[str, str] = {}
    occupied_positions: dict[tuple[int, int], str] = {}
    for key in ENTITY_POSITION_KEYS:
        for entity in model["entities"].get(key, []):
            entity_id = entity.get("id", "")
            if not entity_id:
                raise ValueError(f"Entity in {key} has empty id")
            if entity_id in seen_ids:
                raise ValueError(f"Duplicate entity id {entity_id!r} in {key} and {seen_ids[entity_id]}")
            seen_ids[entity_id] = key

            goal = entity_position(entity)
            if goal in occupied_positions:
                raise ValueError(f"Entity position {goal} overlaps {key} and {occupied_positions[goal]}")
            occupied_positions[goal] = key
            goals.append(goal)

    for goal in goals:
        if goal not in walkable:
            raise ValueError(f"Entity position {goal} is not walkable")
        if goal not in connected:
            raise ValueError(f"No path from {start} to {goal}")

    closed_gate_positions = {
        entity_position(gate)
        for gate in model["entities"].get("puzzle_gates", [])
        if gate.get("starts_closed", True)
    }
    if closed_gate_positions:
        closed_gate_walkable = walkable - closed_gate_positions
        if start not in closed_gate_walkable:
            raise ValueError(f"Player start {start} is blocked by a closed puzzle gate")

        closed_gate_connected = connected_walkable_cells(closed_gate_walkable, start)
        required_entities = [
            ("stair", stair)
            for stair in model["entities"].get("stair_connections", [])
        ]
        required_entities.extend(
            ("hidden placeholder", placeholder)
            for placeholder in model["entities"].get("hidden_placeholders", [])
        )

        for entity_type, entity in required_entities:
            entity_pos = entity_position(entity)
            if entity_pos not in closed_gate_connected:
                raise ValueError(
                    f"Required {entity_type} {entity['id']} is blocked by a closed puzzle gate"
                )

    if model["floor_metadata"].get("floor_number") in (1, 2):
        unrewarded = unrewarded_dead_end_branches(model, walkable, width, height)
        if unrewarded:
            raise ValueError(f"Unrewarded dead-end branches: {unrewarded}")


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


def update_floor_definition_if_exists(path: Path, model: dict) -> None:
    if not path.exists():
        print(f"Warning: floor definition not found, skipping update: {path}")
        return

    update_floor_definition(path, model)


def update_required_floor_definition(path: Path, model: dict, label: str) -> bool:
    if not path.exists():
        print(f"Error: required {label} definition not found: {path}")
        return False

    update_floor_definition(path, model)
    return True


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Floor 1, Floor 2, and Floor 3 JSON.")
    parser.add_argument("--floor1-output", default="scenes/game/floors/Floor1F.json")
    parser.add_argument("--floor1-def", default="resources/floors/Floor1F.tres")
    parser.add_argument("--floor2-output", default="scenes/game/floors/Floor2F.json")
    parser.add_argument("--floor2-def", default="resources/floors/Floor2F.tres")
    parser.add_argument("--floor3-output", default="scenes/game/floors/Floor3F.json")
    parser.add_argument("--floor3-def", default="resources/floors/Floor3F.tres")
    parser.add_argument("--skip-floor-defs", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    floor1 = build_floor1_model()
    floor2 = build_floor2_model()
    floor3 = build_floor3_model()

    write_json(floor1, Path(args.floor1_output))
    write_json(floor2, Path(args.floor2_output))
    write_json(floor3, Path(args.floor3_output))

    if not args.skip_floor_defs:
        if not update_required_floor_definition(Path(args.floor1_def), floor1, "Floor 1"):
            return 1
        update_floor_definition_if_exists(Path(args.floor2_def), floor2)
        update_floor_definition_if_exists(Path(args.floor3_def), floor3)

    print(
        "Generated Floor 1 maze, Floor 2 maze, and Floor 3 landing: "
        f"{len(floor1['tile_layers']['wall'])} floor1 walls, "
        f"{len(floor1['entities']['enemy_spawns'])} floor1 enemies, "
        f"{len(floor1['entities']['treasure_boxes'])} floor1 treasure boxes, "
        f"{len(floor2['tile_layers']['wall'])} floor2 walls, "
        f"{len(floor2['entities']['enemy_spawns'])} floor2 enemies, "
        f"{len(floor2['entities']['treasure_boxes'])} floor2 treasure boxes, "
        f"{len(floor3['tile_layers']['wall'])} floor3 walls"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
