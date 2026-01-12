#!/usr/bin/env -S godot --headless --script
## MCP Refresh Script for LLM Tilemap Editing
##
## Usage:
##   godot --headless --script tools/refresh_tilemap.gd -- <json_path> <scene_path>
##
## Example:
##   godot --headless --script tools/refresh_tilemap.gd -- scenes/game/floors/FloorGF.json scenes/game/floors/FloorGF.tscn
##
## This script:
## 1. Loads the target floor scene
## 2. Imports tile data from the JSON file
## 3. Saves the updated scene
## 4. Exits with status code 0 on success, 1 on failure

extends SceneTree

func _init():
	var args = OS.get_cmdline_user_args()

	if args.size() < 2:
		printerr("Usage: godot --headless --script tools/refresh_tilemap.gd -- <json_path> <scene_path>")
		printerr("  json_path:  Path to the LLM-edited JSON file (e.g., scenes/game/floors/FloorGF.json)")
		printerr("  scene_path: Path to the floor scene to update (e.g., scenes/game/floors/FloorGF.tscn)")
		quit(1)
		return

	var json_path = "res://" + args[0] if not args[0].begins_with("res://") else args[0]
	var scene_path = "res://" + args[1] if not args[1].begins_with("res://") else args[1]

	print("🔄 MCP Tilemap Refresh")
	print("  JSON: ", json_path)
	print("  Scene: ", scene_path)

	# Validate files exist
	if not FileAccess.file_exists(json_path):
		printerr("❌ JSON file not found: ", json_path)
		quit(1)
		return

	if not FileAccess.file_exists(scene_path):
		printerr("❌ Scene file not found: ", scene_path)
		quit(1)
		return

	# Load the scene
	var packed_scene = load(scene_path) as PackedScene
	if packed_scene == null:
		printerr("❌ Failed to load scene: ", scene_path)
		quit(1)
		return

	# Instantiate the scene
	var scene_instance = packed_scene.instantiate()
	if scene_instance == null:
		printerr("❌ Failed to instantiate scene")
		quit(1)
		return

	# Find the GridMap node
	var grid_map = scene_instance.get_node_or_null("GridMap")
	if grid_map == null:
		printerr("❌ GridMap node not found in scene")
		scene_instance.queue_free()
		quit(1)
		return

	# Import the JSON using our C# importer
	# Note: We need to call the C# importer via its class
	var importer = load("res://scripts/tilemap_json/TilemapJsonImporter.cs")
	if importer == null:
		printerr("❌ Failed to load TilemapJsonImporter script")
		# Fallback: Try calling via method if script is attached
		if grid_map.has_method("EditorImportFloorFromJson"):
			grid_map.set("JsonFilePath", json_path)
			grid_map.call("EditorImportFloorFromJson")
		else:
			printerr("❌ Cannot import: no importer available")
			scene_instance.queue_free()
			quit(1)
			return
	else:
		# Create importer instance and import
		var importer_instance = importer.new()
		var err = importer_instance.ImportFromFile(json_path, grid_map)
		if err != OK:
			printerr("❌ Import failed with error: ", err)
			scene_instance.queue_free()
			quit(1)
			return

	# Pack the modified scene
	var new_packed = PackedScene.new()
	var pack_err = new_packed.pack(scene_instance)
	if pack_err != OK:
		printerr("❌ Failed to pack scene: ", pack_err)
		scene_instance.queue_free()
		quit(1)
		return

	# Save the scene
	var save_err = ResourceSaver.save(new_packed, scene_path)
	if save_err != OK:
		printerr("❌ Failed to save scene: ", save_err)
		scene_instance.queue_free()
		quit(1)
		return

	print("✅ Successfully imported JSON and saved scene")
	print("  Tiles and entities updated from: ", json_path)
	print("  Scene saved to: ", scene_path)

	scene_instance.queue_free()
	quit(0)
