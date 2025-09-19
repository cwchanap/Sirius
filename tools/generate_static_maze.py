#!/usr/bin/env python3
# Generates a 50x50 static maze and injects tile_data into scenes/game/Game.tscn
# - Floors use TileSet source 0
# - Walls use TileSet source 7 (wall_generic)
# - Encoded cell key = x + y*65536 (Godot 4 TileMapLayer tile_data format)

from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / "scenes" / "game" / "Game.tscn"

# Placement: put the 50x50 area at origin so it's immediately visible in the editor
OX = 0
OY = 0
W = 50
H = 50

FLOOR_SOURCE = 0
WALL_SOURCE = 7
ALT = 0


def enc(x: int, y: int) -> int:
    return x + y * 65536


def build_floor_data():
    data = []
    for yy in range(OY, OY + H):
        base = yy * 65536
        for xx in range(OX, OX + W):
            data.extend([base + xx, FLOOR_SOURCE, ALT])
    return data


def add_border_walls(walls: set):
    # Top & bottom borders
    for x in range(OX, OX + W):
        walls.add((x, OY))
        walls.add((x, OY + H - 1))
    # Left & right borders
    for y in range(OY, OY + H):
        walls.add((OX, y))
        walls.add((OX + W - 1, y))


def add_internal_walls(walls: set):
    # Vertical walls every 4 tiles with staggered gaps
    for i, x in enumerate(range(OX + 4, OX + W - 1, 5)):
        for y in range(OY + 1, OY + H - 1):
            # create gaps every few tiles
            if (y - OY) % 7 in (2, 5) and (i % 2 == 0):
                continue
            if (y - OY) % 5 == 1 and (i % 3 == 1):
                continue
            walls.add((x, y))
    # Horizontal walls every 6 tiles with staggered gaps
    for j, y in enumerate(range(OY + 6, OY + H - 1, 6)):
        for x in range(OX + 1, OX + W - 1):
            if (x - OX + j) % 7 in (1, 4):
                continue
            walls.add((x, y))
    # Some diagonal connectors to add loops
    for k in range(0, min(W, H), 6):
        x = OX + 2 + k
        y = OY + 3 + k
        if x < OX + W - 1 and y < OY + H - 1:
            walls.add((x, y))
        x2 = OX + W - 3 - k
        y2 = OY + 2 + k
        if x2 > OX + 0 and y2 < OY + H - 1:
            walls.add((x2, y2))


def build_wall_data():
    walls = set()
    add_border_walls(walls)
    add_internal_walls(walls)
    data = []
    for (x, y) in sorted(walls, key=lambda p: (p[1], p[0])):
        data.extend([enc(x, y), WALL_SOURCE, ALT])
    return data


def format_packed_int_array(ints):
    # Compact formatting to limit huge diffs but remain readable
    return "PackedInt32Array(" + ", ".join(str(i) for i in ints) + ")"


def replace_tile_data(text: str, node_name: str, new_array: str) -> str:
    # Match the node block and replace or insert tile_data = PackedInt32Array(...)
    # We search for the specific node header and operate until the next node header
    pattern = rf"(\[node name=\"{re.escape(node_name)}\"[^\]]*\][\s\S]*?)(?=\n\[node |\Z)"
    m = re.search(pattern, text)
    if not m:
        raise SystemExit(f"Node '{node_name}' not found in Game.tscn")
    block = m.group(1)

    if "tile_data = PackedInt32Array(" in block:
        block_new = re.sub(r"tile_data = PackedInt32Array\([^\)]*\)", f"tile_data = {new_array}", block)
    else:
        # Insert tile_data line after tile_set line if present, else at end of block
        if "tile_set =" in block:
            block_new = re.sub(r"(tile_set = .*\n)", r"\1tile_data = " + new_array + "\n", block)
        else:
            block_new = block.rstrip() + "\n" + "tile_data = " + new_array + "\n"

    return text[:m.start(1)] + block_new + text[m.end(1):]


# Note: We intentionally do not create a TileMap node. We only update existing TileMapLayer nodes.


def main():
    text = SCENE.read_text()

    floor_data = build_floor_data()
    wall_data = build_wall_data()

    text = replace_tile_data(text, "GroundLayer", format_packed_int_array(floor_data))
    text = replace_tile_data(text, "WallLayer", format_packed_int_array(wall_data))

    SCENE.write_text(text)
    print(f"✅ Wrote 50x50 static maze to {SCENE}")


if __name__ == "__main__":
    main()
