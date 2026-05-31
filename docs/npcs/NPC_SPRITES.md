# NPC Sprite Guide

NPC runtime sprite sheets use:

`assets/sprites/npcs/{sprite_type}/sprite_sheet.png`

`NpcSpawn.cs` checks that canonical path first, then falls back to:

`assets/sprites/characters/npc_{sprite_type}/sprite_sheet.png`

The `npcs/` location is canonical. Legacy `characters/npc_*` sheets are only fallback references for older assets.

## Runtime Format

- Sheet size: `384x96` px
- Frame layout: four horizontal `96x96` frames
- Format: PNG with RGBA alpha
- Source frames: `assets/sprites/npcs/{sprite_type}/frames/frame1.png` through `frame4.png`
- Build command: `python3 tools/sprite_sheet_merger.py`

## Current Coverage

| Status | NPC | Runtime Sheet |
|--------|-----|---------------|
| ✅ exists | Shopkeeper | `assets/sprites/npcs/shopkeeper/sprite_sheet.png` |
| ✅ exists | Healer | `assets/sprites/npcs/healer/sprite_sheet.png` |
| ❌ missing | Villager | `assets/sprites/npcs/villager/sprite_sheet.png` |
| ❌ missing | Blacksmith | `assets/sprites/npcs/blacksmith/sprite_sheet.png` |
