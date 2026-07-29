"""Filesystem coverage gates for generated HPA-374 UI-art families."""

from pathlib import Path

from PIL import Image

from tools.ui_art_spec import ICON_FAMILIES, ICON_GROUPS


PROJECT_ROOT = Path(__file__).resolve().parents[2]


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
