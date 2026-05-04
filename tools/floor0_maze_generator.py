#!/usr/bin/env python3
"""Generate the Floor 0 District Loop maze JSON for Sirius."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import deque
from pathlib import Path


FLOOR_WIDTH = 100
FLOOR_HEIGHT = 100

# The GridMap defaults to 160x160. We emit wall tiles for the full grid
# so the player cannot walk into void cells beyond the maze content.
GRID_WIDTH = 160
GRID_HEIGHT = 160

PLAYER_START = (8, 50)
SHOPKEEPER_POS = (12, 46)
HEALER_POS = (12, 54)
FIRST_GOBLIN_POS = (24, 45)
STAIR_POS = (82, 68)
# Spawn position on this floor (GF) when player returns from floor 1
RETURN_SPAWN_FROM_FLOOR_1 = (17, 13)

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


def perimeter_walls(grid_width: int, grid_height: int) -> set[tuple[int, int]]:
    """Return wall cells that fill the region beyond the maze (100x100) to the full grid."""
    walls: set[tuple[int, int]] = set()
    # Fill rows beyond FLOOR_HEIGHT
    for y in range(FLOOR_HEIGHT, grid_height):
        for x in range(grid_width):
            walls.add((x, y))
    # Fill columns beyond FLOOR_WIDTH (within maze-height rows)
    for y in range(FLOOR_HEIGHT):
        for x in range(FLOOR_WIDTH, grid_width):
            walls.add((x, y))
    return walls


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
                for y in range(GRID_HEIGHT)
                for x in range(GRID_WIDTH)
            ],
            "wall": [
                {"x": x, "y": y, "tile": "generic"}
                for x, y in sorted(
                    walls | perimeter_walls(GRID_WIDTH, GRID_HEIGHT),
                    key=lambda p: (p[1], p[0]),
                )
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
        for y in range(GRID_HEIGHT)
        for x in range(GRID_WIDTH)
        if (x, y) not in walls
    }


def has_path(walkable: set[tuple[int, int]], start: tuple[int, int], goal: tuple[int, int]) -> bool:
    return connected_walkable_cells(walkable, start).__contains__(goal)


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


def validate_model(model: dict) -> None:
    walkable = walkable_cells(model)
    start = (model["floor_metadata"]["player_start"]["x"], model["floor_metadata"]["player_start"]["y"])
    if start not in walkable:
        raise ValueError(f"Player start {start} is not walkable")

    connected = connected_walkable_cells(walkable, start)
    disconnected = walkable - connected
    if disconnected:
        sample = sorted(disconnected)[:5]
        raise ValueError(f"Disconnected walkable cells: {sample}")

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


def update_floor_definition(path: Path, model: dict, stair_dest: tuple[int, int] | None = None) -> None:
    text = path.read_text(encoding="utf-8")
    start = model["floor_metadata"]["player_start"]
    stair = model["entities"]["stair_connections"][0]["position"]

    # Determine stair destination: explicit param > existing value in file > default constant
    dest_x, dest_y = stair_dest if stair_dest is not None else RETURN_SPAWN_FROM_FLOOR_1
    # If no explicit override, try to preserve existing StairsUpDestinations from the file
    if stair_dest is None:
        existing_match = re.search(
            r"StairsUpDestinations\s*=\s*Array\[Vector2i\]\(\[Vector2i\((\d+),\s*(\d+)\)\]\)",
            text,
        )
        if existing_match:
            dest_x, dest_y = int(existing_match.group(1)), int(existing_match.group(2))

    text, start_count = re.subn(
        r"PlayerStartPosition = Vector2i\([^)]+\)",
        f"PlayerStartPosition = Vector2i({start['x']}, {start['y']})",
        text,
    )
    text, stairs_count = re.subn(
        r"StairsUp = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsUp = Array[Vector2i]([Vector2i({stair['x']}, {stair['y']})])",
        text,
    )
    text, destinations_count = re.subn(
        r"StairsUpDestinations = Array\[Vector2i\]\(\[[^\]]*\]\)",
        f"StairsUpDestinations = Array[Vector2i]([Vector2i({dest_x}, {dest_y})])",
        text,
    )
    missing = [
        name
        for name, count in (
            ("PlayerStartPosition", start_count),
            ("StairsUp", stairs_count),
            ("StairsUpDestinations", destinations_count),
        )
        if count != 1
    ]
    if missing:
        raise ValueError(f"Could not update required FloorDefinition fields: {', '.join(missing)}")

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
        "--stair-dest",
        type=str,
        default=None,
        help="Override StairsUpDestinations as 'x,y' (default: use existing value or 17,13).",
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
        stair_dest = None
        if args.stair_dest:
            parts = args.stair_dest.split(",")
            if len(parts) != 2:
                print(f"Error: --stair-dest must be 'x,y', got '{args.stair_dest}'", file=sys.stderr)
                return 1
            stair_dest = (int(parts[0].strip()), int(parts[1].strip()))
        update_floor_definition(Path(args.floor_def), model, stair_dest)
    print(
        f"Generated Floor 0 maze: {FLOOR_WIDTH}x{FLOOR_HEIGHT}, "
        f"{len(model['tile_layers']['wall'])} walls, "
        f"{len(model['entities']['enemy_spawns'])} enemies"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
