from pathlib import Path

from PIL import Image
import pytest

from tools.ui_art_pipeline import (
    SourceHashMismatch,
    TargetExistsError,
    enforce_horizontal_seam,
    export_record,
    sha256_file,
)
from tools.ui_art_spec import EFFECT_SIZES, ICON_GROUPS, ORNAMENT_SIZES


def test_inventory_has_exact_release_counts():
    assert sum(len(ids) for ids in ICON_GROUPS.values()) == 62
    assert len(ORNAMENT_SIZES) == 13
    assert len(EFFECT_SIZES) == 4


def test_export_icon_writes_true_sized_straight_alpha_derivatives(tmp_path: Path):
    src = Image.new("RGBA", (128, 128), (0, 255, 0, 0))
    from PIL import ImageDraw

    ImageDraw.Draw(src).ellipse((24, 24, 103, 103), fill=(98, 220, 255, 255))
    source_path = tmp_path / "health-alpha.png"
    src.save(source_path)
    record = {
        "id": "health",
        "kind": "icon",
        "category": "stats",
        "source": str(source_path),
        "source_sha256": sha256_file(source_path),
        "crop": [0, 0, 128, 128],
        "target_sizes": [[16, 16], [24, 24], [32, 32]],
    }

    written = export_record(record, tmp_path / "project")

    assert [Image.open(path).size for path in written] == [(16, 16), (24, 24), (32, 32)]
    for path in written:
        with Image.open(path) as image:
            assert image.mode == "RGBA"
            assert image.getpixel((0, 0))[3] == 0
            assert "icc_profile" not in image.info


def test_export_preserves_documented_non_square_aspect(tmp_path: Path):
    # A 2:1 source becomes the exact 512x256 orbit target, never a square stretch.
    source = Image.new("RGBA", (1024, 512), (98, 220, 255, 255))
    source_path = tmp_path / "orbit_arc-alpha.png"
    source.save(source_path)
    record = {
        "id": "orbit_arc",
        "kind": "ornament",
        "source": str(source_path),
        "source_sha256": sha256_file(source_path),
        "crop": [0, 0, 1024, 512],
        "target_sizes": [[512, 256]],
    }
    [written] = export_record(record, tmp_path / "project")
    assert Image.open(written).size == (512, 256)


def test_calibration_ticks_get_byte_identical_nonempty_edges():
    image = Image.new("RGBA", (256, 64), (0, 0, 0, 0))
    from PIL import ImageDraw

    ImageDraw.Draw(image).line((0, 32, 255, 32), fill=(98, 220, 255, 255), width=2)
    repaired = enforce_horizontal_seam(image)
    assert repaired.crop((0, 0, 1, 64)).tobytes() == repaired.crop((255, 0, 256, 64)).tobytes()
    assert repaired.crop((0, 0, 1, 64)).getbbox() is not None


def test_hash_mismatch_stops_before_export(tmp_path: Path):
    source = tmp_path / "source.png"
    Image.new("RGBA", (64, 64), (255, 255, 255, 255)).save(source)
    with pytest.raises(SourceHashMismatch):
        export_record(
            {
                "id": "health",
                "kind": "icon",
                "category": "stats",
                "source": str(source),
                "source_sha256": "0" * 64,
                "crop": [0, 0, 64, 64],
                "target_sizes": [[16, 16]],
            },
            tmp_path / "project",
        )


def test_existing_target_requires_explicit_replacement(tmp_path: Path):
    source = tmp_path / "source.png"
    Image.new("RGBA", (64, 64), (255, 255, 255, 255)).save(source)
    project = tmp_path / "project"
    record = {
        "id": "health",
        "kind": "icon",
        "category": "stats",
        "source": str(source),
        "source_sha256": sha256_file(source),
        "crop": [0, 0, 64, 64],
        "target_sizes": [[16, 16]],
    }
    export_record(record, project)
    with pytest.raises(TargetExistsError):
        export_record(record, project)
