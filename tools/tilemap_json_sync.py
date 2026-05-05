#!/usr/bin/env python3
"""
Tilemap JSON Sync Tool for LLM Co-Editing

This script provides CLI commands for syncing between .tscn scenes and .json files.
Designed to be called by MCP servers or directly from command line.

Usage:
    python3 tools/tilemap_json_sync.py export <scene_path> [--output <json_path>]
    python3 tools/tilemap_json_sync.py import <json_path> <scene_path>
    python3 tools/tilemap_json_sync.py refresh <json_path> <scene_path>

Examples:
    # Export FloorGF.tscn to FloorGF.json
    python3 tools/tilemap_json_sync.py export scenes/game/floors/FloorGF.tscn

    # Import changes from JSON back to scene
    python3 tools/tilemap_json_sync.py import scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn

    # Refresh (same as import, for MCP trigger)
    python3 tools/tilemap_json_sync.py refresh scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn
"""

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

# Find project root (where project.godot is)
SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent
SCENE_UID_KEY = "__scene_uid__"

# Godot executable path (adjust for your system)
DEFAULT_GODOT_PATH = "/Applications/Godot_mono.app/Contents/MacOS/Godot"
GODOT_PATH = (
    os.environ.get("GODOT_PATH")
    or os.environ.get("GODOT_BIN")
    or DEFAULT_GODOT_PATH
)


def run_godot_headless(script_args: list[str]) -> int:
    """Run Godot in headless mode with the refresh script."""
    cmd = [
        GODOT_PATH,
        "--headless",
        "--path", str(PROJECT_ROOT),
        "--script", "tools/refresh_tilemap.gd",
        "--",
    ] + script_args

    print(f"Running: {' '.join(cmd)}")

    try:
        result = subprocess.run(cmd, cwd=PROJECT_ROOT, capture_output=True, text=True)
        print(result.stdout)
        if result.stderr:
            print(result.stderr, file=sys.stderr)
        return result.returncode
    except FileNotFoundError:
        print(f"Error: Godot not found at {GODOT_PATH}", file=sys.stderr)
        print("Please update GODOT_PATH in this script or set GODOT_PATH environment variable", file=sys.stderr)
        return 1


def cmd_export(args):
    """Export a scene to JSON (calls Godot to do the work)."""
    scene_path = args.scene_path
    output_path = args.output or scene_path.replace(".tscn", ".json")

    print(f"Exporting {scene_path} -> {output_path}", file=sys.stderr)
    print("Error: Headless export is not yet supported - please use the Godot editor UI", file=sys.stderr)
    return 1


def extract_uid_map(tscn_path: Path) -> dict[str, str]:
    """Extract scene and ext_resource uid metadata from a .tscn file."""
    uid_map: dict[str, str] = {}
    if not tscn_path.exists():
        return uid_map
    for line in tscn_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("[gd_scene"):
            scene_uid_match = re.search(r'uid="([^"]+)"', line)
            if scene_uid_match:
                uid_map[SCENE_UID_KEY] = scene_uid_match.group(1)
            continue

        if not line.startswith("[ext_resource"):
            continue
        path_match = re.search(r'path="([^"]+)"', line)
        uid_match = re.search(r'uid="([^"]+)"', line)
        if path_match and uid_match:
            uid_map[path_match.group(1)] = uid_match.group(1)
    return uid_map


def restore_uids(tscn_path: Path, uid_map: dict[str, str]) -> None:
    """Patch a .tscn file to restore uid= attributes on scene and ext_resource lines."""
    if not uid_map or not tscn_path.exists():
        return
    lines = tscn_path.read_text(encoding="utf-8").splitlines()
    patched = []
    restored = 0
    for line in lines:
        if line.startswith("[gd_scene") and "uid=" not in line and SCENE_UID_KEY in uid_map:
            line = line[:-1] + f' uid="{uid_map[SCENE_UID_KEY]}"]'
            restored += 1
        elif line.startswith("[ext_resource"):
            path_match = re.search(r'path="([^"]+)"', line)
            has_uid = "uid=" in line
            if path_match and not has_uid and path_match.group(1) in uid_map:
                uid = uid_map[path_match.group(1)]
                # Insert uid= after the type attribute
                line = re.sub(
                    r'(type="[^"]*")',
                    rf'\1 uid="{uid}"',
                    line,
                    count=1,
                )
                restored += 1
        patched.append(line)
    if restored > 0:
        tscn_path.write_text("\n".join(patched) + "\n", encoding="utf-8")
        print(f"Restored {restored} uid references in {tscn_path}")


def cmd_import(args):
    """Import JSON to scene, preserving uid:// references."""
    json_path = args.json_path
    scene_path = Path(args.scene_path)

    # Snapshot uid map before Godot overwrites the .tscn
    uid_map = extract_uid_map(scene_path)

    print(f"Importing {json_path} -> {scene_path}")
    result = run_godot_headless([json_path, str(scene_path)])
    if result != 0:
        return result

    # Restore uid references that Godot headless import may have dropped
    restore_uids(scene_path, uid_map)
    return 0


def cmd_refresh(args):
    """Refresh scene from JSON (alias for import, designed for MCP triggers)."""
    return cmd_import(args)


def main():
    parser = argparse.ArgumentParser(
        description="Tilemap JSON Sync Tool for LLM Co-Editing",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    # Export command
    export_parser = subparsers.add_parser("export", help="Export scene to JSON")
    export_parser.add_argument("scene_path", help="Path to .tscn scene file")
    export_parser.add_argument("--output", "-o", help="Output JSON path (default: same as scene with .json extension)")
    export_parser.set_defaults(func=cmd_export)

    # Import command
    import_parser = subparsers.add_parser("import", help="Import JSON to scene")
    import_parser.add_argument("json_path", help="Path to .json file")
    import_parser.add_argument("scene_path", help="Path to .tscn scene file")
    import_parser.set_defaults(func=cmd_import)

    # Refresh command (alias for import, designed for MCP)
    refresh_parser = subparsers.add_parser("refresh", help="Refresh scene from JSON (MCP trigger)")
    refresh_parser.add_argument("json_path", help="Path to .json file")
    refresh_parser.add_argument("scene_path", help="Path to .tscn scene file")
    refresh_parser.set_defaults(func=cmd_refresh)

    args = parser.parse_args()
    sys.exit(args.func(args))


if __name__ == "__main__":
    main()
