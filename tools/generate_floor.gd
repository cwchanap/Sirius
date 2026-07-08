#!/usr/bin/env -S godot --headless --script
## Sirius floor generator (headless). Replaces tools/floor*_maze_generator.py.
##
## Usage:
##   godot --headless --path . --script tools/generate_floor.gd -- --floor <0|1|2|3> [options]
##
## Options:
##   --json-only          Write Floor*.json without touching scene/.tres
##   --skip-floor-def     Skip the FloorDefinition .tres sync
##   --stair-dest x,y     GF StairsUpDestinations override (parity with Python)

extends SceneTree

func _init():
	var cli = load("res://scripts/game/floors/FloorCli.cs")
	if cli == null:
		printerr("Failed to load FloorCli")
		quit(1)
		return
	var instance = cli.new()
	if instance == null:
		printerr("Failed to instantiate FloorCli")
		quit(1)
		return
	var code = instance.Run()
	quit(code)
