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
import subprocess
import sys
from pathlib import Path

# Find project root (where project.godot is)
SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent

# Godot executable path (adjust for your system)
GODOT_PATH = "/Applications/Godot_mono.app/Contents/MacOS/Godot"


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

    print(f"Exporting {scene_path} -> {output_path}")
    print("Note: Export requires running Godot editor or using the EditorExportToJson button in GridMap inspector")
    print("Headless export is not yet supported - please use the Godot editor UI")
    return 0


def cmd_import(args):
    """Import JSON to scene."""
    json_path = args.json_path
    scene_path = args.scene_path

    print(f"Importing {json_path} -> {scene_path}")
    return run_godot_headless([json_path, scene_path])


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
