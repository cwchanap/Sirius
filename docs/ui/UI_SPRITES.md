# UI and effects sprite guide

The canonical HPA-374 inventory is documented in
[hpa-374/README.md](hpa-374/README.md) and its exact generated-source and
runtime-path record is [hpa-374/ASSET_MANIFEST.md](hpa-374/ASSET_MANIFEST.md).
`UiArtCatalog` owns optional loading for these categorized paths:

- `assets/sprites/ui/icons/{category}/{16|24|32}/{id}.png`
- `assets/sprites/ui/ornaments/{id}.png`
- `assets/sprites/effects/ui/{encounter_burst,hit_impact,status_pulse,reward_level_up}.png`

All catalog effects are 256x256 with mipmaps enabled. `status_pulse` is a new
status-specific effect, not a rename of an older sparkle placeholder. The
catalog remains resource-only outside the current Inventory integration; battle
placement/playback and full ornament composition are deferred.

## Retained backgrounds

| File | Size | Current loader |
| --- | --- | --- |
| `assets/sprites/ui/ui_main_menu_background.png` | 1920x1080 | `MainMenu.cs` |
| `assets/sprites/ui/ui_battle_background.png` | 1280x720 | `BattleManager.cs` |

These existing backgrounds are unchanged. Their reference copies remain in
`assets/sprites/ui/original/`.

## Scope boundary

HPA-374 intentionally does not supply manual Attack/Defend/Run buttons, nor
HPA-375 filter/sort/comparison/passive-skill art. Do not add root-level UI
icons or effects for those deferred features; add a typed catalog entry and its
validated categorized derivative when that downstream work is approved.
