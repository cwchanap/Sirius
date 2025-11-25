<!--
  Sync Impact Report
  ===================
  Version change: N/A → 1.0.0 (initial ratification)
  
  Modified principles: N/A (new constitution)
  
  Added sections:
    - Core Principles (5 principles)
    - Quality Standards
    - Development Workflow
    - Governance
  
  Removed sections: N/A
  
  Templates requiring updates:
    ✅ plan-template.md - Constitution Check section compatible
    ✅ spec-template.md - User scenarios and requirements align with testing principles
    ✅ tasks-template.md - Phase structure supports test-first and modular design
    ✅ checklist-template.md - Generic template, no updates needed
    ✅ agent-file-template.md - Generic template, no updates needed
  
  Follow-up TODOs: None
-->

# Sirius Constitution

## Core Principles

### I. Godot-Native Architecture
All game systems MUST leverage Godot's built-in capabilities. Scenes MUST use partial classes 
(`public partial class`) for node scripts. Signal-based communication MUST be preferred over 
direct method calls for loose coupling between systems. The GameManager singleton pattern MUST 
be maintained for global state management. Resource classes requiring persistence MUST include 
`[System.Serializable]` attribute.

**Rationale**: Godot-native patterns ensure compatibility with the engine's lifecycle, enable 
proper scene serialization, and maintain clean separation between game systems.

### II. Grid-First World Design
The 160x160 grid coordinate system (origin at top-left) MUST be the authoritative source for 
world positions. World coordinates MUST be derived by multiplying grid coordinates by CellSize 
(32 pixels). Position-based systems MUST use deterministic calculations (seed-based pseudo-random) 
rather than runtime randomness in rendering loops. Viewport culling MUST be applied for any 
grid-wide operations to maintain 60fps performance.

**Rationale**: Deterministic grid behavior prevents visual artifacts, ensures reproducible 
gameplay, and enables efficient rendering of large world spaces.

### III. Test-Driven Development
New features MUST have corresponding test cases written using GdUnit4 framework before 
implementation. Tests MUST follow Arrange-Act-Assert pattern with `[TestCase]` attributes. 
Test files MUST mirror the source structure (e.g., `scripts/data/Character.cs` → 
`tests/data/CharacterTest.cs`). Integration tests MUST verify signal emissions and 
cross-system interactions.

**Rationale**: TDD ensures battle mechanics, inventory operations, and game state transitions 
behave correctly and prevents regressions in complex RPG systems.

### IV. Factory Pattern for Game Entities
Enemy creation MUST use static factory methods (e.g., `Enemy.CreateGoblin()`, `Enemy.CreateBoss()`). 
New enemy types MUST be added through factory methods, not direct instantiation. Factory methods 
MUST set all required stats (HP, ATK, DEF, SPD, XP reward) with balanced progression values. 
Item creation SHOULD follow similar factory patterns for consistency.

**Rationale**: Factory patterns centralize entity configuration, ensure stat balance is 
maintained, and prevent incomplete entity initialization.

### V. Graceful Asset Degradation
Sprite loading MUST implement fallback rendering (colored rectangles) when asset files are 
missing. The asset pipeline (32x32 frames → 128x32 sprite sheets via `sprite_sheet_merger.py`) 
MUST be followed for all character animations. Sprite caching MUST occur in `_Ready()` methods, 
not in draw loops. Four-frame animation cycles (idle → left → idle variant → right) MUST be 
maintained for consistency.

**Rationale**: Fallback rendering allows gameplay testing without complete art assets and 
ensures the game remains playable during iterative asset development.

## Quality Standards

### Performance Requirements
- Grid rendering MUST maintain 60fps through viewport culling on 160x160 maps
- Sprite dictionaries MUST be populated once at scene ready, not per-frame
- Battle dialogs MUST be instantiated as modal overlays without scene reloads
- Memory allocations in `_Draw()` and `_Process()` MUST be minimized

### Code Organization
- Scripts MUST be organized by domain: `scripts/data/`, `scripts/game/`, `scripts/ui/`
- Scenes MUST be organized by purpose: `scenes/game/`, `scenes/ui/`
- Test files MUST mirror source structure under `tests/`
- Resource definitions (`.tres` files) MUST reside in `resources/` with descriptive subdirectories

### Boundary Safety
- All grid array access MUST validate coordinates against bounds before access
- Battle state checks (`GameManager.IsInBattle`) MUST precede movement operations
- Signal handlers MUST use deferred calls when modifying scene tree during callbacks

## Development Workflow

### Build and Test Process
1. Run `dotnet build Sirius.sln` to compile before testing
2. Execute tests via GdUnit4 panel in Godot Editor or `dotnet test`
3. Use Godot Editor (F5) or `godot` command for runtime testing
4. Walk into enemies to trigger battle system verification

### Asset Creation Pipeline
1. Generate individual 32x32 PNG frames with transparent backgrounds
2. Place frames in `assets/sprites/characters/{character_name}/`
3. Run `python3 tools/sprite_sheet_merger.py` to create 128x32 sprite sheets
4. Import in Godot with Filter: Off, Fix Alpha Border: On

### Scene Flow Validation
- MainMenu.tscn → Game.tscn → Battle dialogs (modal) → Return to Game.tscn
- ESC key MUST return to main menu when not in battle
- Auto-combat MUST proceed without manual input in battle dialogs

## Governance

### Amendment Process
1. Proposed changes MUST be documented with rationale
2. Changes affecting game balance MUST include test case updates
3. Architectural changes MUST update relevant template files in `.specify/templates/`
4. Version MUST be incremented according to semantic versioning:
   - MAJOR: Backward-incompatible principle changes or removals
   - MINOR: New principles or materially expanded guidance
   - PATCH: Clarifications, wording improvements, typo fixes

### Compliance Verification
- All PRs MUST verify adherence to Core Principles
- New game systems MUST include Constitution Check in their plan.md
- Test coverage MUST be maintained for data classes and game state management
- AGENTS.md and CLAUDE.md serve as runtime development guidance files

**Version**: 1.0.0 | **Ratified**: 2025-11-24 | **Last Amended**: 2025-11-24
