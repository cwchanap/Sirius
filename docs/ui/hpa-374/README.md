# HPA-374 UI art inventory

HPA-374 ships 62 logical icons (186 icon derivatives at 16, 24, and 32 px),
13 ornaments, four effects, and five font binaries: 203 total runtime PNGs.
The two existing backgrounds are retained unchanged.

`UiArtCatalog` is the runtime owner. It maps typed IDs to the stable paths
below and optional-loads textures with an `info`-icon fallback for a missing
non-info icon:

- `assets/sprites/ui/icons/{category}/{16|24|32}/{id}.png`
- `assets/sprites/ui/ornaments/{id}.png`
- `assets/sprites/effects/ui/{id}.png`
- `assets/fonts/{cinzel,noto_sans,noto_sans_mono}/`

The generated-art provenance and exact hashes are in
[ASSET_MANIFEST.md](ASSET_MANIFEST.md), with the detailed generated-source
record in [sources/SOURCE_MANIFEST.md](sources/SOURCE_MANIFEST.md). The six
visual review boards are indexed in [CONTACT_SHEETS.md](CONTACT_SHEETS.md).

## Current integration

The current consumer is the Inventory screen only. It uses generated icons for
the Equipment and Inventory headings, empty equipment/accessory slots, inactive
accessory locks, and the binding-aware Inventory close hint. The main-menu
(`assets/sprites/ui/ui_main_menu_background.png`) and battle
(`assets/sprites/ui/ui_battle_background.png`) backgrounds remain their
pre-existing assets.

## Intentional deferrals and exclusions

HPA-374 supplies resources; it does not compose every screen. Battle stat and
status-row placement, battle-effect placement, modal semantic headers, Theme
font wiring, and full ornament composition are deferred. Downstream screen
composition and theme wiring remain deferred.

HPA-375-only filter, sort, comparison, and passive-skill artwork is excluded.
Manual Attack, Defend, and Run button art is also excluded; no
`ui_button_attack.png`, `ui_button_defend.png`, or `ui_button_run.png` is part
of this inventory.

## Verification

```bash
rtk uv run --with-requirements requirements-dev.txt python3 -m pytest tests/tools -q
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --filter 'Class=UiArtCatalogTest' --no-build
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --filter 'Name=Effects_LoadAtDocumentedSizeWithMipmaps' --no-build
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --filter 'Name=InventoryHeadings_UseReadableLabelsAndGeneratedIcons|Name=OpenMenu_UsesCurrentToggleInventoryBindingInCloseLabel' --no-build
```

The focused Godot commands deliberately use VSTest's verified `Class=` and
exact `Name=` selectors, rather than stale `FullyQualifiedName~` examples.
