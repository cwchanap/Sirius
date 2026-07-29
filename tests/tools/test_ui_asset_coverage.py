"""Filesystem coverage gates for generated HPA-374 UI-art families."""

import json
from pathlib import Path

from PIL import Image

from tools.ui_art_pipeline import MAP_RELATIVE_PATH, runtime_path, sha256_file
from tools.ui_art_spec import ICON_FAMILIES, ICON_GROUPS


PROJECT_ROOT = Path(__file__).resolve().parents[2]


def _records() -> list[dict]:
    return [record for record in json.loads((PROJECT_ROOT / MAP_RELATIVE_PATH).read_text())
            if record["family"] == "inventory-actions"]


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
            assert not path.with_suffix(".png.import").exists()
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
