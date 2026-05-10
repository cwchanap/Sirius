# Sirius Item Guide

<!-- PLACEHOLDER: item grid image will go here when assets are ready.
     Suggested path: docs/assets/item-guide-grid.png
     Layout: 5 columns × N rows, 64×64 icon tiles with item name below each.
     Replace this comment block with: ![Item Grid](assets/item-guide-grid.png) -->

## Consumables

| ID | Display Name | Effect | Value | Rarity |
|----|-------------|--------|-------|--------|
| health_potion | Health Potion | Restores 50 HP | 30g | Common |
| greater_health_potion | Greater Health Potion | Restores 150 HP | 80g | Uncommon |
| mana_potion | Mana Potion | Restores 25 MP | 35g | Common |
| strength_tonic | Strength Tonic | +15 ATK for 3 turns | 50g | Common |
| iron_skin | Iron Skin | +10 DEF for 4 turns | 50g | Common |
| swiftness_draught | Swiftness Draught | +8 SPD for 3 turns | 40g | Common |
| antidote | Antidote | Cures Poison & Burn | 35g | Common |
| regen_potion | Regen Potion | +15 HP/turn for 3 turns | 65g | Common |
| poison_vial | Poison Vial | Poisons enemy (8 dmg/turn, 4 turns) | 60g | Uncommon |
| flash_powder | Flash Powder | Blinds enemy for 2 turns (55% accuracy) | 55g | Uncommon |

## Dungeon Consumables

| ID | Display Name | Effect | Value | Rarity |
|----|-------------|--------|-------|--------|
| major_health_potion | Major Health Potion | Restores 300 HP | 150g | Rare |
| major_mana_potion | Major Mana Potion | Restores 75 MP | 130g | Rare |
| warding_charm | Warding Charm | +18 DEF for 4 turns | 120g | Uncommon |
| smoke_bomb | Smoke Bomb | Blinds enemy for 3 turns | 95g | Uncommon |

## Status Effects Reference

Status effect tags are shown in battle as `[TAG Nt]` (e.g. `[PSN 3t]`).

| Effect | Tag | Type | Mechanic |
|--------|-----|------|----------|
| Poison | PSN | Debuff DoT | Flat HP damage per turn, bypasses defense |
| Burn | BRN | Debuff DoT | Flat HP damage per turn, bypasses defense |
| Stun | STN | Debuff | Target skips their next action |
| Weaken | WKN | Debuff | Reduces Attack by Magnitude% |
| Slow | SLW | Debuff | Reduces Speed by Magnitude% |
| Blind | BLD | Debuff | Hit rate reduced to 55% |
| Regen | RGN | Buff | Heals Magnitude HP per turn |
| Haste | HST | Buff | Raises Speed by Magnitude (flat) |
| Strength | STR | Buff | Raises Attack by Magnitude (flat) |
| Fortify | FRT | Buff | Raises Defense by Magnitude (flat) |

Notes:
- DoT (Poison, Burn) and HoT (Regen) bypass/ignore defense.
- Buffs and debuffs of the same type do not stack; re-applying takes the higher magnitude and higher turns remaining.
- All status effects are cleared at battle end.

## Enemy Debuff Abilities

Enemies with debuff abilities attempt to apply them each time they attack.

| Enemy | Debuff | Magnitude | Duration | Chance |
|-------|--------|-----------|----------|--------|
| Goblin | Poison | 5 dmg/turn | 3 turns | 20% |
| Cave Spider | Poison | 8 dmg/turn | 4 turns | 35% |
| Cave Spider | Slow | −4% SPD | 2 turns | 20% |
| Skeleton Warrior | Weaken | −8% ATK | 3 turns | 25% |
| Swamp Wretch | Poison | 10 dmg/turn | 4 turns | 30% |
| Swamp Wretch | Blind | — | 2 turns | 20% |
| Dark Mage | Weaken | −12% ATK | 3 turns | 30% |
| Dark Mage | Stun | — | 1 turn | 15% |
| Grave Hexer | Weaken | −14% ATK | 3 turns | 32% |
| Grave Hexer | Blind | — | 2 turns | 20% |
| Bone Archer | Slow | −10% SPD | 2 turns | 28% |
| Bone Archer | Blind | — | 1 turn | 16% |
| Cursed Gargoyle | Burn | 10 dmg/turn | 3 turns | 25% |
| Abyss Acolyte | Weaken | −16% ATK | 3 turns | 34% |
| Abyss Acolyte | Stun | — | 1 turn | 16% |

## Equipment

See `EquipmentCatalog.cs` for full equipment stats. Equipment guide section TBD.

## Dungeon Equipment

| ID | Slot | Key Bonuses | Rarity |
|----|------|-------------|--------|
| steel_longsword | Weapon | +32 ATK | Uncommon |
| chain_mail | Armor | +26 DEF, +50 HP | Uncommon |
| steel_tower_shield | Shield | +18 DEF, +30 HP | Uncommon |
| knight_helm | Helmet | +8 DEF, +160 HP | Uncommon |
| swift_boots | Shoe | +4 DEF, +8 SPD | Uncommon |
| obsidian_blade | Weapon | +44 ATK, +4 SPD | Rare |
| obsidian_carapace | Armor | +36 DEF, +120 HP | Rare |
| obsidian_guard | Shield | +8 ATK, +26 DEF | Rare |
| obsidian_crown | Helmet | +240 HP, +8 SPD | Rare |
| obsidian_treads | Shoe | +8 DEF, +12 SPD | Rare |

## Monster Parts

| ID | Name | Value | Source |
|----|------|-------|--------|
| goblin_ear | Goblin Ear | 5g | Goblin |
| orc_tusk | Orc Tusk | 15g | Orc |
| skeleton_bone | Skeleton Bone | 12g | Skeleton Warrior |
| spider_silk | Spider Silk | 20g | Cave Spider |
| dragon_scale | Dragon Scale | 200g | Dragon, Demon Lord, Boss |
| sentinel_core | Sentinel Core | 45g | Crypt Sentinel, Dungeon Guardian |
| hexed_cloth | Hexed Cloth | 50g | Grave Hexer, Dark Mage |
| splintered_bone | Splintered Bone | 38g | Bone Archer |
| revenant_plate | Revenant Plate | 90g | Iron Revenant, Dungeon Guardian |
| gargoyle_shard | Gargoyle Shard | 100g | Cursed Gargoyle |
| abyssal_sigil | Abyssal Sigil | 140g | Abyss Acolyte, Demon Lord, Boss |
