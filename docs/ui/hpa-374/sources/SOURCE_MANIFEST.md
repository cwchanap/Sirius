# HPA-374 UI Art Source Manifest

| ID | Family | Source | Runtime derivatives |
| --- | --- | --- | --- |
| `assign` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/assign-alpha.png` | `assets/sprites/ui/icons/actions/16/assign.png`<br>`assets/sprites/ui/icons/actions/24/assign.png`<br>`assets/sprites/ui/icons/actions/32/assign.png` |
| `buy` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/buy-alpha.png` | `assets/sprites/ui/icons/actions/16/buy.png`<br>`assets/sprites/ui/icons/actions/24/buy.png`<br>`assets/sprites/ui/icons/actions/32/buy.png` |
| `equip` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/equip-alpha.png` | `assets/sprites/ui/icons/actions/16/equip.png`<br>`assets/sprites/ui/icons/actions/24/equip.png`<br>`assets/sprites/ui/icons/actions/32/equip.png` |
| `sell` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/sell-alpha.png` | `assets/sprites/ui/icons/actions/16/sell.png`<br>`assets/sprites/ui/icons/actions/24/sell.png`<br>`assets/sprites/ui/icons/actions/32/sell.png` |
| `unequip` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/unequip-alpha.png` | `assets/sprites/ui/icons/actions/16/unequip.png`<br>`assets/sprites/ui/icons/actions/24/unequip.png`<br>`assets/sprites/ui/icons/actions/32/unequip.png` |
| `use` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/use-alpha.png` | `assets/sprites/ui/icons/actions/16/use.png`<br>`assets/sprites/ui/icons/actions/24/use.png`<br>`assets/sprites/ui/icons/actions/32/use.png` |
| `accessory` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/accessory-alpha.png` | `assets/sprites/ui/icons/inventory/16/accessory.png`<br>`assets/sprites/ui/icons/inventory/24/accessory.png`<br>`assets/sprites/ui/icons/inventory/32/accessory.png` |
| `active_skill` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/active_skill-alpha.png` | `assets/sprites/ui/icons/inventory/16/active_skill.png`<br>`assets/sprites/ui/icons/inventory/24/active_skill.png`<br>`assets/sprites/ui/icons/inventory/32/active_skill.png` |
| `armor` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/armor-alpha.png` | `assets/sprites/ui/icons/inventory/16/armor.png`<br>`assets/sprites/ui/icons/inventory/24/armor.png`<br>`assets/sprites/ui/icons/inventory/32/armor.png` |
| `consumable` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/consumable-alpha.png` | `assets/sprites/ui/icons/inventory/16/consumable.png`<br>`assets/sprites/ui/icons/inventory/24/consumable.png`<br>`assets/sprites/ui/icons/inventory/32/consumable.png` |
| `equipment` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/equipment-alpha.png` | `assets/sprites/ui/icons/inventory/16/equipment.png`<br>`assets/sprites/ui/icons/inventory/24/equipment.png`<br>`assets/sprites/ui/icons/inventory/32/equipment.png` |
| `general` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/general-alpha.png` | `assets/sprites/ui/icons/inventory/16/general.png`<br>`assets/sprites/ui/icons/inventory/24/general.png`<br>`assets/sprites/ui/icons/inventory/32/general.png` |
| `helmet` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/helmet-alpha.png` | `assets/sprites/ui/icons/inventory/16/helmet.png`<br>`assets/sprites/ui/icons/inventory/24/helmet.png`<br>`assets/sprites/ui/icons/inventory/32/helmet.png` |
| `locked` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/locked-alpha.png` | `assets/sprites/ui/icons/inventory/16/locked.png`<br>`assets/sprites/ui/icons/inventory/24/locked.png`<br>`assets/sprites/ui/icons/inventory/32/locked.png` |
| `quest` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/quest-alpha.png` | `assets/sprites/ui/icons/inventory/16/quest.png`<br>`assets/sprites/ui/icons/inventory/24/quest.png`<br>`assets/sprites/ui/icons/inventory/32/quest.png` |
| `shield` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/shield-alpha.png` | `assets/sprites/ui/icons/inventory/16/shield.png`<br>`assets/sprites/ui/icons/inventory/24/shield.png`<br>`assets/sprites/ui/icons/inventory/32/shield.png` |
| `shoe` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/shoe-alpha.png` | `assets/sprites/ui/icons/inventory/16/shoe.png`<br>`assets/sprites/ui/icons/inventory/24/shoe.png`<br>`assets/sprites/ui/icons/inventory/32/shoe.png` |
| `weapon` | `inventory-actions` | `art_source/ui/hpa-374/boards/inventory-actions/weapon-alpha.png` | `assets/sprites/ui/icons/inventory/16/weapon.png`<br>`assets/sprites/ui/icons/inventory/24/weapon.png`<br>`assets/sprites/ui/icons/inventory/32/weapon.png` |

## Replacement history

The first `weapon` source was rejected by the unmodified 16px opaque-core
validation: its tall, narrow silhouette could not satisfy that gate while
retaining the required one-pixel transparent inset. The rejected ignored
masters remain at `weapon-rejected-source.png`
(`92ae4f56482ad06eff8fdf155033f291be1516cbe3c092fabaa252cb582f8955`)
and `weapon-rejected-alpha.png`
(`113698e0a114d479c210f523d16930912314323639bd5a1fa0c746eafa8a7dc8`).
One targeted regeneration produced the accepted ignored masters
`weapon-replacement-source.png`
(`d423dbe54083d82cbac606f0a75c17e288a48692857362c642444d7d41068287`)
and `weapon-replacement-alpha.png`
(`ef557c76520d1952586ea9b83f962093b98699c0c3962544a025adeaa4890662`),
which were copied into the registered `weapon` source names before extraction.
