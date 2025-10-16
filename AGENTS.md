# Repository Guidelines

These notes keep contributors aligned with how Sirius is structured, built, and reviewed.

## Project Structure & Module Organization
- `scenes/` holds Godot scenes (`game/Game.tscn`, `ui/MainMenu.tscn`, `ui/BattleScene.tscn`).
- `scripts/` contains C# code grouped by domain: `game/` controllers (e.g., `GameManager.cs`, `GridMap.cs`), `data/` models (`Character.cs`, `Enemy.cs`, inventory), and `ui/` for interface logic.
- `assets/` stores sprites and audio; use `assets/sprites/items/…` for equipment art referenced via `res://` paths.
- `tools/` includes helper scripts such as `sprite_sheet_merger.py` and `resize_item_icons.py` for asset preparation.

## Build, Test, and Development Commands
```bash
dotnet build Sirius.sln          # Compile all C# scripts against .NET 8 / Godot SDK
godot                            # Open the Godot editor and run the project (F5)
godot --headless --run           # Launch headless builds/exports when scripting automation
python3 tools/sprite_sheet_merger.py   # Merge 32x32 frames into 128x32 sprite sheets
python3 tools/resize_item_icons.py     # Normalize item icon sizes before importing
```
Always run `dotnet build` prior to committing to catch compiler regressions.

## Coding Style & Naming Conventions
- C# files use 4-space indentation, one public type per file, and `public partial class` declarations for Godot nodes.
- Follow PascalCase for classes/methods, camelCase for locals, SCREAMING_SNAKE_CASE for constants, and underscore-separated `Id` strings (e.g., `wooden_sword`).
- Keep node paths and resources stable; reference assets with `res://` URIs and store new art under the matching subfolder.
- Prefer signal-based communication (`[Signal]`, `EmitSignal`) to keep systems loosely coupled.

## Testing Guidelines
- There is no automated test suite yet; rely on `dotnet build` plus in-editor playtesting.
- Verify gameplay changes by loading `Game.tscn`, walking the maze, triggering battles, and checking console logs for `GameManager` state transitions.
- When adjusting assets, re-run the sprite merge/resize scripts and confirm imports in Godot (Filter Off, Fix Alpha Border On).

## Commit & Pull Request Guidelines
- Match existing commit prefixes (`feat:`, `ui:`, `refactor:`) followed by imperative summaries, e.g., `feat: add battle reward screen`.
- Before opening a PR, describe the change, list validation steps (`dotnet build`, in-game scenarios), link related issues, and include screenshots or GIFs for UI/asset updates.
- Highlight any new assets or tool outputs so reviewers can update imports.
