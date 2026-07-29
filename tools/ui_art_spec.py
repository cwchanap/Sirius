ICON_GROUPS = {
    "stats": ("health", "mana", "experience", "level", "gold", "attack", "defense", "speed"),
    "status": ("poison", "burn", "stun", "weaken", "slow", "blind", "regen", "haste", "strength", "fortify"),
    "inventory": (
        "general", "equipment", "consumable", "quest", "weapon", "shield",
        "armor", "helmet", "shoe", "accessory", "active_skill", "locked",
    ),
    "actions": ("equip", "unequip", "use", "assign", "buy", "sell"),
    "flow": ("pause", "resume", "settings", "save", "load"),
    "interaction": ("dialogue", "shop", "heal", "puzzle", "reward"),
    "semantic": ("info", "warning", "error", "confirm", "cancel_close"),
    "input": (
        "keyboard", "keycap_blank", "mouse", "mouse_primary", "mouse_secondary",
        "mouse_wheel", "gamepad", "gamepad_face_blank", "gamepad_dpad",
        "gamepad_stick", "gamepad_shoulder",
    ),
}

ORNAMENT_SIZES = {
    "celestial_anchor": (192, 192), "orbit_arc": (512, 256),
    "trajectory_line": (512, 64), "calibration_ticks": (256, 64),
    "callout_frame": (512, 256), "callout_connector": (256, 64),
    "catalogue_rail_endcap": (128, 256), "ignition_seal": (192, 192),
    "constellation_corner": (128, 128), "constellation_divider": (512, 64),
    "partial_sigil": (256, 256), "focus_halo": (96, 96),
    "selection_halo": (96, 96),
}

EFFECT_SIZES = {
    "encounter_burst": (256, 256), "hit_impact": (256, 256),
    "status_pulse": (256, 256), "reward_level_up": (256, 256),
}

ICON_FAMILIES = {
    "inventory-actions": ("inventory", "actions"),
    "stats-status": ("stats", "status"),
    "flow-semantic": ("flow", "interaction", "semantic"),
    "input-glyphs": ("input",),
}
