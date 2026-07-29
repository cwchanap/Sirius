"""Filesystem coverage gates for generated HPA-374 UI-art families."""

from hashlib import sha256
import json
from pathlib import Path
import re
import subprocess

from PIL import Image
import pytest

from tools.ui_art_pipeline import MAP_RELATIVE_PATH, _intended_usage, runtime_path, sha256_file
from tools.ui_art_spec import EFFECT_SIZES, ICON_FAMILIES, ICON_GROUPS, ORNAMENT_SIZES


PROJECT_ROOT = Path(__file__).resolve().parents[2]
ICON_SIZES = (16, 24, 32)

FONT_HASHES = {
    "assets/fonts/cinzel/Cinzel-Variable.ttf": "f4d83d34d1f6c741193e4acf4b3dff9531e5a67b6aa65228d00a7db72a4e0f34",
    "assets/fonts/noto_sans/NotoSans-Regular.ttf": "b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5",
    "assets/fonts/noto_sans/NotoSans-Medium.ttf": "7bbe267354704c6ad18bde24b1dbc756c8e4380ca1c3f3c25c45ec5c4471510b",
    "assets/fonts/noto_sans/NotoSans-SemiBold.ttf": "87a8b90ece1e89746b544e4e086f85a3710e41485a8078f9be874837dfad45d5",
    "assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf": "b1e09ba9f3607d81aedc9e4e1cbe225a0df85c77bde267931a1ab28577840edd",
}
OFL_HASHES = {
    "assets/fonts/cinzel/OFL.txt": "f2b3029aba64c378bf0963b62945eee15e564fe4330b934c8f2eb058282b5e83",
    "assets/fonts/noto_sans/OFL.txt": "0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb",
    "assets/fonts/noto_sans_mono/OFL.txt": "0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb",
}
PROHIBITED_PATHS = (
    "assets/sprites/ui/ui_button_attack.png", "assets/sprites/ui/ui_button_defend.png",
    "assets/sprites/ui/ui_button_run.png", "assets/sprites/ui/icon_health.png",
    "assets/sprites/ui/icon_experience.png", "assets/sprites/ui/icon_level.png",
    "assets/sprites/effects/effect_hit_impact.png",
    "assets/sprites/effects/effect_magic_sparkles.png",
    "assets/sprites/effects/effect_level_up.png",
)
EMOJI = re.compile("[\u2600-\u27bf\U0001f300-\U0001faff]")
TSCN_USER_TEXT = re.compile(r'^\s*(text|tooltip_text|placeholder_text|dialog_text|title)\s*=\s*"(?P<value>.*)"\s*$')
C_SHARP_TOKEN = re.compile(r'(?P<string>(?:\\$@|@\\$|\\$|@)?"(?:\\\\.|""|[^"\\\\])*")|(?P<comment>//[^\n]*|/\\*.*?\\*/)', re.DOTALL)
C_SHARP_USER_TEXT = re.compile(r'\.(Text|TooltipText|DialogText|Title)\s*=\s*(?P<value>(?:\\$@|@\\$|\\$|@)?"(?:\\\\.|""|[^"\\\\])*")')


def _records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "inventory-actions"]


def _stats_status_records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "stats-status"]


def _flow_semantic_records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "flow-semantic"]


def _input_glyph_records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "input-glyphs"]


def _ornament_records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "ornaments"]


def _effect_records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "effects"]


def expected_runtime_png_sets() -> tuple[set[Path], set[Path], set[Path]]:
    icons = {
        PROJECT_ROOT / "assets/sprites/ui/icons" / category / str(size) / f"{asset_id}.png"
        for category, ids in ICON_GROUPS.items()
        for size in ICON_SIZES
        for asset_id in ids
    }
    ornaments = {
        PROJECT_ROOT / "assets/sprites/ui/ornaments" / f"{asset_id}.png"
        for asset_id in ORNAMENT_SIZES
    }
    effects = {
        PROJECT_ROOT / "assets/sprites/effects/ui" / f"{asset_id}.png"
        for asset_id in EFFECT_SIZES
    }
    return icons, ornaments, effects


def assert_exact_runtime_png_sets() -> None:
    expected_icons, expected_ornaments, expected_effects = expected_runtime_png_sets()
    actual_icons = set((PROJECT_ROOT / "assets/sprites/ui/icons").rglob("*.png"))
    actual_ornaments = set((PROJECT_ROOT / "assets/sprites/ui/ornaments").glob("*.png"))
    actual_effects = set((PROJECT_ROOT / "assets/sprites/effects/ui").glob("*.png"))
    assert actual_icons == expected_icons
    assert actual_ornaments == expected_ornaments
    assert actual_effects == expected_effects
    assert len(expected_icons) == 186
    assert len(expected_ornaments) == 13
    assert len(expected_effects) == 4
    assert len(actual_icons | actual_ornaments | actual_effects) == 203


def test_input_glyph_family_has_all_true_size_runtime_derivatives():
    """Binding-aware presenters only consume the complete generated input family."""
    missing: list[str] = []
    for asset_id in ICON_GROUPS["input"]:
        for size in (16, 24, 32):
            path = PROJECT_ROOT / "assets/sprites/ui/icons/input" / str(size) / f"{asset_id}.png"
            if not path.is_file():
                missing.append(path.relative_to(PROJECT_ROOT).as_posix())
                continue
            with Image.open(path) as image:
                assert image.mode == "RGBA"
                assert image.size == (size, size)
    assert missing == []


def test_input_glyph_records_and_runtime_pngs_preserve_alpha_safety():
    records = _input_glyph_records()
    assert {record["id"] for record in records} == set(ICON_GROUPS["input"])
    for record in records:
        for size, _ in record["target_sizes"]:
            path = runtime_path(record, PROJECT_ROOT, size, size)
            with Image.open(path) as image:
                assert image.mode == "RGBA"
                assert "icc_profile" not in image.info
                rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            visible = alpha.getbbox()
            assert alpha.getextrema()[0] == 0
            assert visible is not None
            left, top, right, bottom = visible
            assert left >= 1 and top >= 1 and right <= size - 1 and bottom <= size - 1
            assert not any(a and g > 160 and g > r * 1.3 and g > b * 1.3 for r, g, b, a in rgba.getdata())


def test_input_glyph_map_hashes_and_manifest_agree_when_local_masters_exist():
    records = _input_glyph_records()
    assert len(records) == 11
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    for record in records:
        source = PROJECT_ROOT / record["source"]
        alpha = PROJECT_ROOT / record["alpha_source"]
        if source.is_file():
            assert sha256_file(source) == record["source_sha256"]
        if alpha.is_file():
            assert sha256_file(alpha) == record["alpha_sha256"]
        for expected in (
            record["id"], record["family"], record["source"], record["alpha_source"],
            record["source_sha256"], record["alpha_sha256"], str(record["source_size"]),
            str(record["alpha_size"]), str(record["crop"]), str(record["target_sizes"]),
            json.dumps(record["postprocess"], sort_keys=True), record["generator"], record["generated_on"],
            record["intended_usage"],
        ):
            assert expected in manifest


def test_input_glyph_records_have_exact_non_generic_intended_usage():
    expected_roles = {
        "keyboard": "Keyboard device-context glyph",
        "keycap_blank": "Localized keyboard binding label frame",
        "mouse": "Mouse device-context glyph",
        "mouse_primary": "Primary mouse-button binding glyph",
        "mouse_secondary": "Secondary mouse-button binding glyph",
        "mouse_wheel": "Mouse-wheel binding glyph",
        "gamepad": "Gamepad device-context glyph",
        "gamepad_face_blank": "Localized gamepad face-button binding frame",
        "gamepad_dpad": "Gamepad D-pad direction binding glyph",
        "gamepad_stick": "Gamepad analog-stick direction binding glyph",
        "gamepad_shoulder": "Gamepad shoulder/trigger binding glyph",
    }
    records = _input_glyph_records()
    assert {record["id"] for record in records} == set(expected_roles)
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    assert "Intended usage: input-glyphs UI artwork" not in manifest
    for record in records:
        role = expected_roles[record["id"]]
        assert record["intended_usage"] == role
        assert _intended_usage(record) == role
        assert f"- Intended usage: {role}" in manifest


def test_icon_import_sidecars_are_never_tracked():
    """Godot may generate ignored local caches, but icon sources never commit them."""
    tracked = subprocess.check_output(
        ["git", "ls-files", "*.png.import"], cwd=PROJECT_ROOT, text=True
    ).splitlines()
    assert not [path for path in tracked if path.startswith((
        "assets/sprites/ui/icons/", "assets/sprites/ui/ornaments/",
    ))]


def test_effects_are_the_exact_tracked_mipmap_import_exception():
    tracked = subprocess.check_output(
        ["git", "ls-files", "*.png.import"], cwd=PROJECT_ROOT, text=True
    ).splitlines()
    expected = {
        f"assets/sprites/effects/ui/{asset_id}.png.import"
        for asset_id in EFFECT_SIZES
    }
    assert {path for path in tracked if path.startswith("assets/sprites/effects/ui/")} == expected
    for relative_path in sorted(expected):
        contents = (PROJECT_ROOT / relative_path).read_text()
        for setting in (
            "compress/mode=0", "mipmaps/generate=true", "mipmaps/limit=-1",
            "process/fix_alpha_border=true", "process/premult_alpha=false",
        ):
            assert setting in contents


def test_effect_runtime_pngs_preserve_alpha_and_chroma_contracts():
    records = _effect_records()
    assert {record["id"] for record in records} == set(EFFECT_SIZES)
    for record in records:
        width, height = EFFECT_SIZES[record["id"]]
        path = runtime_path(record, PROJECT_ROOT, width, height)
        with Image.open(path) as image:
            assert image.mode == "RGBA"
            assert image.size == (width, height)
            assert "icc_profile" not in image.info
            rgba = image.convert("RGBA")
        alpha = rgba.getchannel("A")
        visible = alpha.getbbox()
        assert alpha.getextrema()[0] == 0
        assert visible is not None
        left, top, right, bottom = visible
        assert left >= 1 and top >= 1 and right <= width - 1 and bottom <= height - 1
        assert not any(a and g > 160 and g > r * 1.3 and g > b * 1.3 for r, g, b, a in rgba.getdata())


def test_effect_map_hashes_and_manifest_agree_when_local_masters_exist():
    records = _effect_records()
    assert len(records) == len(EFFECT_SIZES)
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    for record in records:
        source = PROJECT_ROOT / record["source"]
        alpha = PROJECT_ROOT / record["alpha_source"]
        if source.is_file():
            assert sha256_file(source) == record["source_sha256"]
        if alpha.is_file():
            assert sha256_file(alpha) == record["alpha_sha256"]
        for expected in (
            record["id"], record["family"], record["source"], record["alpha_source"],
            record["source_sha256"], record["alpha_sha256"], str(record["source_size"]),
            str(record["alpha_size"]), str(record["crop"]), str(record["target_sizes"]),
            json.dumps(record["postprocess"], sort_keys=True), record["generator"], record["generated_on"],
            record["intended_usage"],
        ):
            assert expected in manifest


def test_ornament_runtime_pngs_preserve_live_release_contracts():
    """All committed ornaments, not only synthetic exports, retain their release contracts."""
    records = _ornament_records()
    assert {record["id"] for record in records} == set(ORNAMENT_SIZES)
    for record in records:
        width, height = ORNAMENT_SIZES[record["id"]]
        path = runtime_path(record, PROJECT_ROOT, width, height)
        with Image.open(path) as image:
            assert image.mode == "RGBA"
            assert image.size == (width, height)
            assert "icc_profile" not in image.info
            rgba = image.convert("RGBA")
        alpha = rgba.getchannel("A")
        visible = alpha.getbbox()
        assert alpha.getextrema()[0] == 0
        assert visible is not None
        left, top, right, bottom = visible
        if record["id"] == "calibration_ticks":
            assert left == 0 and right == width
            assert top >= 1 and bottom <= height - 1
            assert rgba.crop((0, 0, 1, height)).tobytes() == rgba.crop((width - 1, 0, width, height)).tobytes()
            assert alpha.crop((0, 0, 1, height)).getbbox() is not None
        elif record["id"] == "callout_frame":
            assert left >= 1 and top >= 1 and right <= width - 1 and bottom <= height - 1
            assert alpha.crop((32, 32, width - 32, height - 32)).getextrema()[1] == 0
            assert all(alpha.crop(band).getbbox() is not None for band in (
                (0, 0, width, 32), (0, height - 32, width, height),
                (0, 0, 32, height), (width - 32, 0, width, height),
            ))
        else:
            assert left >= 1 and top >= 1 and right <= width - 1 and bottom <= height - 1
        assert not any(a and g > 160 and g > r * 1.3 and g > b * 1.3 for r, g, b, a in rgba.getdata())


def test_inventory_action_family_has_all_true_size_runtime_derivatives():
    """The first shipped family must be complete before screen integration."""
    missing: list[str] = []
    for category in ICON_FAMILIES["inventory-actions"]:
        for asset_id in ICON_GROUPS[category]:
            for size in (16, 24, 32):
                path = PROJECT_ROOT / "assets/sprites/ui/icons" / category / str(size) / f"{asset_id}.png"
                if not path.is_file():
                    missing.append(path.relative_to(PROJECT_ROOT).as_posix())
                    continue
                with Image.open(path) as image:
                    assert image.mode == "RGBA"
                    assert image.size == (size, size)
    assert missing == []


def test_inventory_action_runtime_pngs_preserve_real_alpha_safety_and_srgb_contracts():
    records = _records()
    assert len(records) == 18
    for record in records:
        for size, _ in record["target_sizes"]:
            path = runtime_path(record, PROJECT_ROOT, size, size)
            with Image.open(path) as image:
                assert image.mode == "RGBA"
                assert "icc_profile" not in image.info
                rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            visible = alpha.getbbox()
            assert alpha.getextrema()[0] == 0
            assert visible is not None
            left, top, right, bottom = visible
            assert left >= 1 and top >= 1 and right <= size - 1 and bottom <= size - 1
            assert not any(a and g > 160 and g > r * 1.3 and g > b * 1.3 for r, g, b, a in rgba.getdata())
            if size == 16:
                core = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
                assert core is not None
                core_width, core_height = core[2] - core[0], core[3] - core[1]
                assert core_width >= 0.30 * 16 and core_height >= 0.30 * 16
                assert max(core_width, core_height) >= 0.50 * 16


def test_inventory_action_map_hashes_and_manifest_agree_when_local_masters_exist():
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    for record in _records():
        source = PROJECT_ROOT / record["source"]
        alpha = PROJECT_ROOT / record["alpha_source"]
        if source.is_file():
            assert sha256_file(source) == record["source_sha256"]
        if alpha.is_file():
            assert sha256_file(alpha) == record["alpha_sha256"]
        for expected in (
            record["id"], record["family"], record["source"], record["alpha_source"],
            record["source_sha256"], record["alpha_sha256"], str(record["source_size"]),
            str(record["alpha_size"]), str(record["crop"]), str(record["target_sizes"]),
            json.dumps(record["postprocess"], sort_keys=True), record["generator"], record["generated_on"],
        ):
            assert expected in manifest
    assert "generated specifically for Sirius" in manifest
    assert "not sourced from a third-party art pack" in manifest


def test_stats_status_family_has_all_true_size_runtime_derivatives():
    """Stats and current status effects ship as complete, typed art inputs."""
    missing: list[str] = []
    for category in ICON_FAMILIES["stats-status"]:
        for asset_id in ICON_GROUPS[category]:
            for size in (16, 24, 32):
                path = PROJECT_ROOT / "assets/sprites/ui/icons" / category / str(size) / f"{asset_id}.png"
                if not path.is_file():
                    missing.append(path.relative_to(PROJECT_ROOT).as_posix())
                    continue
                with Image.open(path) as image:
                    assert image.mode == "RGBA"
                    assert image.size == (size, size)
    assert missing == []


def test_stats_status_runtime_pngs_preserve_real_alpha_safety_and_srgb_contracts():
    records = _stats_status_records()
    assert len(records) == 18
    assert {record["id"] for record in records if record["category"] == "status"} == set(ICON_GROUPS["status"])
    for record in records:
        for size, _ in record["target_sizes"]:
            path = runtime_path(record, PROJECT_ROOT, size, size)
            with Image.open(path) as image:
                assert image.mode == "RGBA"
                assert "icc_profile" not in image.info
                rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            visible = alpha.getbbox()
            assert alpha.getextrema()[0] == 0
            assert visible is not None
            left, top, right, bottom = visible
            assert left >= 1 and top >= 1 and right <= size - 1 and bottom <= size - 1
            assert not any(a and g > 160 and g > r * 1.3 and g > b * 1.3 for r, g, b, a in rgba.getdata())
            if size == 16:
                core = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
                assert core is not None
                core_width, core_height = core[2] - core[0], core[3] - core[1]
                assert core_width >= 0.30 * 16 and core_height >= 0.30 * 16
                assert max(core_width, core_height) >= 0.50 * 16


def test_stats_status_map_hashes_and_manifest_agree_when_local_masters_exist():
    records = _stats_status_records()
    assert len(records) == 18
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    for record in records:
        source = PROJECT_ROOT / record["source"]
        alpha = PROJECT_ROOT / record["alpha_source"]
        if source.is_file():
            assert sha256_file(source) == record["source_sha256"]
        if alpha.is_file():
            assert sha256_file(alpha) == record["alpha_sha256"]
        for expected in (
            record["id"], record["family"], record["source"], record["alpha_source"],
            record["source_sha256"], record["alpha_sha256"], str(record["source_size"]),
            str(record["alpha_size"]), str(record["crop"]), str(record["target_sizes"]),
            json.dumps(record["postprocess"], sort_keys=True), record["generator"], record["generated_on"],
        ):
            assert expected in manifest


def test_stats_status_records_have_exact_non_generic_intended_usage():
    expected_roles = {
        "health": "Player health resource indicator",
        "mana": "Player mana resource indicator",
        "experience": "Player experience resource indicator",
        "level": "Player level indicator",
        "gold": "Player gold resource indicator",
        "attack": "Player/battle attack stat indicator",
        "defense": "Player/battle defense stat indicator",
        "speed": "Player/battle speed stat indicator",
        "poison": "Active Poison debuff indicator",
        "burn": "Active Burn debuff indicator",
        "stun": "Active Stun debuff indicator",
        "weaken": "Active Weaken debuff indicator",
        "slow": "Active Slow debuff indicator",
        "blind": "Active Blind debuff indicator",
        "regen": "Active Regen buff indicator",
        "haste": "Active Haste buff indicator",
        "strength": "Active Strength buff indicator",
        "fortify": "Active Fortify buff indicator",
    }
    records = _stats_status_records()
    assert {record["id"] for record in records} == set(expected_roles)
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    assert "Intended usage: stats-status UI artwork" not in manifest
    for record in records:
        role = expected_roles[record["id"]]
        assert _intended_usage(record) == role
        assert f"- Intended usage: {role}" in manifest


def test_regenerated_manifest_preserves_inventory_roles_and_weaken_history():
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    for role in (
        "Inventory general category heading icon",
        "Inventory equipment category heading icon",
        "Inventory consumable category tab icon",
        "Inventory quest category tab icon",
        "Empty weapon equipment slot glyph",
        "Empty shield equipment slot glyph",
        "Empty armor equipment slot glyph",
        "Empty helmet equipment slot glyph",
        "Empty shoe equipment slot glyph",
        "Empty accessory equipment slot glyph",
        "Inactive accessory placeholder",
        "Active-skill selector/slot glyph",
        "Equip selected item action",
        "Unequip selected item action",
        "Use selected consumable action",
        "Assign active skill action",
        "Shop purchase action",
        "Shop sale action",
    ):
        assert f"- Intended usage: {role}" in manifest
    for expected in (
        "## Weaken replacement history",
        "weaken-rejected-source.png",
        "weaken-replacement-1-rejected-source.png",
        "weaken-replacement-2-rejected-source.png",
        "weaken-replacement-3-rejected-source.png",
        "weaken-replacement-4-source.png",
        "8x12px opaque 16px core",
    ):
        assert expected in manifest


def test_flow_semantic_family_has_all_true_size_runtime_derivatives():
    """Flow, interaction, and semantic controls ship as complete typed inputs."""
    missing: list[str] = []
    for category in ICON_FAMILIES["flow-semantic"]:
        for asset_id in ICON_GROUPS[category]:
            for size in (16, 24, 32):
                path = PROJECT_ROOT / "assets/sprites/ui/icons" / category / str(size) / f"{asset_id}.png"
                if not path.is_file():
                    missing.append(path.relative_to(PROJECT_ROOT).as_posix())
                    continue
                with Image.open(path) as image:
                    assert image.mode == "RGBA"
                    assert image.size == (size, size)
    assert missing == []


def test_flow_semantic_runtime_pngs_preserve_real_alpha_safety_and_srgb_contracts():
    records = _flow_semantic_records()
    assert len(records) == 15
    assert {record["id"] for record in records if record["category"] == "flow"} == set(ICON_GROUPS["flow"])
    assert {record["id"] for record in records if record["category"] == "interaction"} == set(ICON_GROUPS["interaction"])
    assert {record["id"] for record in records if record["category"] == "semantic"} == set(ICON_GROUPS["semantic"])
    for record in records:
        for size, _ in record["target_sizes"]:
            path = runtime_path(record, PROJECT_ROOT, size, size)
            with Image.open(path) as image:
                assert image.mode == "RGBA"
                assert "icc_profile" not in image.info
                rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            visible = alpha.getbbox()
            assert alpha.getextrema()[0] == 0
            assert visible is not None
            left, top, right, bottom = visible
            assert left >= 1 and top >= 1 and right <= size - 1 and bottom <= size - 1
            assert not any(a and g > 160 and g > r * 1.3 and g > b * 1.3 for r, g, b, a in rgba.getdata())
            if size == 16:
                core = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
                assert core is not None
                core_width, core_height = core[2] - core[0], core[3] - core[1]
                assert core_width >= 0.30 * 16 and core_height >= 0.30 * 16
                assert max(core_width, core_height) >= 0.50 * 16


def test_flow_semantic_map_hashes_and_manifest_agree_when_local_masters_exist():
    records = _flow_semantic_records()
    assert len(records) == 15
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    for record in records:
        source = PROJECT_ROOT / record["source"]
        alpha = PROJECT_ROOT / record["alpha_source"]
        if source.is_file():
            assert sha256_file(source) == record["source_sha256"]
        if alpha.is_file():
            assert sha256_file(alpha) == record["alpha_sha256"]
        for expected in (
            record["id"], record["family"], record["source"], record["alpha_source"],
            record["source_sha256"], record["alpha_sha256"], str(record["source_size"]),
            str(record["alpha_size"]), str(record["crop"]), str(record["target_sizes"]),
            json.dumps(record["postprocess"], sort_keys=True), record["generator"], record["generated_on"],
        ):
            assert expected in manifest


def test_flow_semantic_records_have_exact_non_generic_intended_usage():
    expected_roles = {
        "pause": "Pause gameplay flow control",
        "resume": "Resume gameplay flow control",
        "settings": "Open settings flow control",
        "save": "Save-game flow control",
        "load": "Load-game flow control",
        "dialogue": "Dialogue interaction entry",
        "shop": "Shop interaction entry",
        "heal": "Healing interaction entry",
        "puzzle": "Puzzle interaction entry",
        "reward": "Reward interaction indicator",
        "info": "Informational semantic indicator",
        "warning": "Warning semantic indicator paired with readable text",
        "error": "Error semantic indicator paired with readable text",
        "confirm": "Confirm semantic control paired with readable text",
        "cancel_close": "Cancel or close semantic control paired with readable text",
    }
    records = _flow_semantic_records()
    assert {record["id"] for record in records} == set(expected_roles)
    manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    assert "Intended usage: flow-semantic UI artwork" not in manifest
    for record in records:
        role = expected_roles[record["id"]]
        assert _intended_usage(record) == role
        assert f"- Intended usage: {role}" in manifest


def test_final_runtime_inventory_and_font_hashes_are_exact():
    assert_exact_runtime_png_sets()
    icon_paths, _, _ = expected_runtime_png_sets()
    for path in icon_paths:
        size = int(path.parent.name)
        with Image.open(path) as image:
            assert image.mode == "RGBA" and image.size == (size, size), path
            assert "icc_profile" not in image.info and "srgb" not in image.info, path
            alpha = image.getchannel("A")
            visible = alpha.getbbox()
            assert visible is not None and alpha.getextrema() == (0, 255), path
            left, top, right, bottom = visible
            assert left >= 1 and top >= 1 and right <= size - 1 and bottom <= size - 1, path
            if size == 16:
                core = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
                assert core is not None, path
                width, height = core[2] - core[0], core[3] - core[1]
                assert width >= 0.30 * size and height >= 0.30 * size, path
                assert width >= 0.50 * size or height >= 0.50 * size, path
    for relative, expected_hash in (FONT_HASHES | OFL_HASHES).items():
        path = PROJECT_ROOT / relative
        assert path.is_file(), path
        assert sha256(path.read_bytes()).hexdigest() == expected_hash, path


def test_exact_runtime_inventory_rejects_an_unregistered_png():
    extra_path = PROJECT_ROOT / "assets/sprites/ui/icons/stats/16/_coverage_extra.png"
    try:
        extra_path.write_bytes(b"not a registered runtime PNG")
        with pytest.raises(AssertionError):
            assert_exact_runtime_png_sets()
    finally:
        extra_path.unlink(missing_ok=True)
    assert_exact_runtime_png_sets()


def test_final_provenance_negative_paths_and_docs_are_current():
    records = json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
    source_manifest = (PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md").read_text()
    asset_manifest = (PROJECT_ROOT / "docs/ui/hpa-374/ASSET_MANIFEST.md").read_text()
    readme = (PROJECT_ROOT / "docs/ui/hpa-374/README.md").read_text()
    sheets = (PROJECT_ROOT / "docs/ui/hpa-374/CONTACT_SHEETS.md").read_text()
    assert len(records) == len({record["id"] for record in records}) == 79
    for record in records:
        assert record["generator"] == "OpenAI image_gen"
        assert re.fullmatch(r"\d{4}-\d{2}-\d{2}", record["generated_on"])
        assert record["postprocess"]["auto_key"] == "border"
        assert record["postprocess"]["soft_matte"] is True
        assert record["source"] in source_manifest
        assert record["source_sha256"] in source_manifest
        assert record["alpha_source"] in source_manifest
        assert record["alpha_sha256"] in source_manifest
        assert record["source_sha256"] in asset_manifest
        assert record["alpha_sha256"] in asset_manifest
    callout = next(record for record in records if record["id"] == "callout_frame")
    assert callout["crop"] == [47, 23, 1680, 840]
    for heading in (
        "Weapon replacement history", "Weaken replacement history",
        "Save replacement history", "Encounter burst extraction history",
        "Callout frame crop repair history",
    ):
        assert heading in source_manifest
    assert asset_manifest.split("## Generated HPA-374 artwork", 1)[1].count("\n| `") == 79
    assert "was not sourced from a third-party art pack" in asset_manifest
    assert "186 icon derivatives" in readme
    assert "203 total runtime PNGs" in readme
    assert "UiArtCatalog" in readme and "HPA-375" in readme and "unchanged" in readme
    for filename in ("icons-16.png", "icons-24.png", "icons-32.png", "icon-states.png", "ornaments.png", "effects.png"):
        assert filename in sheets
        assert (PROJECT_ROOT / "docs/ui/hpa-374/contact-sheets" / filename).is_file()
    assert "Accepted" in sheets
    assert all(not (PROJECT_ROOT / path).exists() for path in PROHIBITED_PATHS)
    assert not {
        "filter.png", "sort.png", "comparison.png", "compare.png", "passive_skill.png"
    } & {path.name for path in (PROJECT_ROOT / "assets/sprites/ui/icons").rglob("*.png")}


def test_structural_user_facing_strings_contain_no_emoji():
    def strip_comments(source: str) -> str:
        return C_SHARP_TOKEN.sub(
            lambda match: match.group("string") if match.group("string") else "\n" * match.group(0).count("\n"),
            source,
        )

    offenders: list[str] = []
    for path in (PROJECT_ROOT / "scenes").rglob("*.tscn"):
        for line_number, line in enumerate(path.read_text().splitlines(), 1):
            match = TSCN_USER_TEXT.match(line)
            if match and EMOJI.search(match.group("value")):
                offenders.append(f"{path.relative_to(PROJECT_ROOT)}:{line_number}: {match.group('value')}")
    for path in (PROJECT_ROOT / "scripts/ui").rglob("*.cs"):
        source = strip_comments(path.read_text())
        for match in C_SHARP_USER_TEXT.finditer(source):
            if EMOJI.search(match.group("value")):
                line_number = source.count("\n", 0, match.start()) + 1
                offenders.append(f"{path.relative_to(PROJECT_ROOT)}:{line_number}: {match.group('value')}")
    assert offenders == []


def test_global_ui_docs_only_describe_canonical_runtime_paths():
    stale = re.compile(r"ui_button_(attack|defend|run)|assets/sprites/ui/icon_(health|experience|level)\.png|assets/sprites/effects/effect_(hit_impact|magic_sparkles|level_up)\.png")
    for relative in ("docs/ui/UI_SPRITES.md", "docs/items/ASSET_STATUS.md"):
        assert stale.search((PROJECT_ROOT / relative).read_text()) is None, relative
