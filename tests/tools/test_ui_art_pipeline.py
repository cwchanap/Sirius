import json
from pathlib import Path
import subprocess
import sys

from PIL import Image, ImageChops
import pytest

import tools.ui_art_pipeline as pipeline
from tools.ui_art_pipeline import (
    SourceHashMismatch,
    TargetExistsError,
    build_contact_sheets,
    extract_family,
    enforce_horizontal_seam,
    export_record,
    register_family,
    runtime_path,
    sha256_file,
    icon_sheet_layout,
    state_sheet_layout,
    write_manifest,
)
from tools.ui_art_spec import EFFECT_SIZES, ICON_FAMILIES, ICON_GROUPS, ORNAMENT_SIZES


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


def _record(source: Path, asset_id: str = "health", **updates) -> dict:
    record = {
        "id": asset_id,
        "family": "stats-status",
        "kind": "icon",
        "category": "stats",
        "source": str(source),
        "alpha_source": str(source),
        "source_sha256": sha256_file(source),
        "alpha_sha256": sha256_file(source),
        "crop": [0, 0, 64, 64],
        "target_sizes": [[16, 16]],
    }
    record.update(updates)
    return record


@pytest.mark.parametrize(
    ("crop", "target_sizes"),
    [([56, 56, 64, 64], [[16, 16]]), ([0, 0, 64, 64], [[128, 128]]), ([0, 0, 64, 64], [[0, 16]])],
)
def test_export_rejects_invalid_extent_before_creating_output(
    tmp_path: Path, crop: list[int], target_sizes: list[list[int]]
):
    source = tmp_path / "source.png"
    Image.new("RGBA", (64, 64), (0, 0, 0, 0)).save(source)
    record = _record(source, crop=crop, target_sizes=target_sizes)

    with pytest.raises(ValueError):
        export_record(record, tmp_path / "project")

    assert not (tmp_path / "project").exists()


def test_export_has_no_unrecorded_replacement_escape_hatch(tmp_path: Path):
    source = tmp_path / "source.png"
    Image.new("RGBA", (64, 64), (0, 0, 0, 0)).save(source)

    with pytest.raises(TypeError):
        export_record(_record(source), tmp_path / "project", allow_replace=True)


def test_export_rejects_registered_chroma_contamination(tmp_path: Path):
    source = tmp_path / "source.png"
    image = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    image.paste((0, 255, 0, 255), (16, 16, 48, 48))
    image.save(source)

    with pytest.raises(ValueError, match="contamination"):
        export_record(_record(source), tmp_path / "project")


def test_export_rejects_registered_icon_without_final_safety_inset(tmp_path: Path):
    source = tmp_path / "source.png"
    image = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    image.paste((98, 220, 255, 255), (0, 0, 56, 56))
    image.save(source)

    with pytest.raises(ValueError, match="safety inset"):
        export_record(_record(source), tmp_path / "project")


def test_inventory_has_exact_ids_and_runtime_paths(tmp_path: Path):
    assert ICON_FAMILIES["flow-semantic"] == ("flow", "interaction", "semantic")
    assert ICON_GROUPS["input"][-1] == "gamepad_shoulder"
    assert ORNAMENT_SIZES["orbit_arc"] == (512, 256)
    assert EFFECT_SIZES["reward_level_up"] == (256, 256)
    assert runtime_path(
        {"id": "health", "kind": "icon", "category": "stats"}, tmp_path, 16, 16
    ) == tmp_path / "assets/sprites/ui/icons/stats/16/health.png"
    assert runtime_path({"id": "orbit_arc", "kind": "ornament"}, tmp_path, 512, 256) == tmp_path / "assets/sprites/ui/ornaments/orbit_arc.png"


def test_registration_writes_complete_provenance_and_preserves_date(tmp_path: Path, monkeypatch):
    project = tmp_path / "project"
    source_root = project / "art_source/ui/hpa-374/boards"
    family = "inventory-actions"
    for asset_id, _, _, _ in pipeline._family_assets(family):
        for suffix in ("source", "alpha"):
            path = source_root / family / f"{asset_id}-{suffix}.png"
            path.parent.mkdir(parents=True, exist_ok=True)
            Image.new("RGBA", (64, 64), (0, 0, 0, 0)).save(path)
            Image.new("RGBA", (64, 64), (98, 220, 255, 255)).save(path)
    map_path = project / "docs/ui/hpa-374/sources/extraction-map.json"

    class FirstDate:
        @staticmethod
        def today():
            return type("Day", (), {"isoformat": lambda self: "2026-07-29"})()

    monkeypatch.setattr(pipeline, "date", FirstDate)
    records = register_family(family, source_root, map_path)
    assert len(records) == 18
    assert {record["category"] for record in records} == {"inventory", "actions"}
    assert all(set(record) == {
        "id", "family", "kind", "category", "source", "alpha_source", "source_sha256",
        "alpha_sha256", "source_size", "alpha_size", "crop", "target_sizes", "generator",
        "generated_on", "postprocess",
    } for record in records)
    assert all(record["generator"] == "OpenAI image_gen" for record in records)
    assert all(record["postprocess"] == {**pipeline.POSTPROCESS, "edge_contract": 0} for record in records)
    assert all(not Path(record["source"]).is_absolute() for record in records)

    class SecondDate:
        @staticmethod
        def today():
            return type("Day", (), {"isoformat": lambda self: "2026-07-30"})()

    monkeypatch.setattr(pipeline, "date", SecondDate)
    register_family(family, source_root, map_path)
    assert {record["generated_on"] for record in json.loads(map_path.read_text())} == {"2026-07-29"}


def test_extract_rolls_back_when_canonical_promotion_fails(tmp_path: Path, monkeypatch):
    project = tmp_path / "project"
    source = project / "art_source/source.png"
    source.parent.mkdir(parents=True)
    image = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    image.paste((98, 220, 255, 255), (8, 8, 56, 56))
    image.save(source)
    health, mana = _record(source), _record(source, asset_id="mana")
    map_path = project / "map.json"
    map_path.write_text(json.dumps([health, mana]))
    original_replace = pipeline.os.replace

    def fail_second_promotion(from_path, to_path):
        if Path(to_path).name == "mana.png":
            raise OSError("simulated promotion failure")
        return original_replace(from_path, to_path)

    monkeypatch.setattr(pipeline.os, "replace", fail_second_promotion)
    with pytest.raises(OSError, match="simulated promotion failure"):
        extract_family("stats-status", map_path, project)

    assert not runtime_path(health, project, 16, 16).exists()
    assert not runtime_path(mana, project, 16, 16).exists()


def test_contact_sheets_annotate_categories_states_and_nine_patch(tmp_path: Path):
    project = tmp_path / "project"
    icon_source = tmp_path / "health.png"
    frame_source = tmp_path / "frame.png"
    Image.new("RGBA", (64, 64), (0, 0, 0, 0)).save(icon_source)
    Image.new("RGBA", (1024, 512), (0, 0, 0, 0)).save(frame_source)
    icon = _record(icon_source)
    frame = _record(
        frame_source,
        asset_id="callout_frame",
        kind="ornament",
        category=None,
        crop=[0, 0, 1024, 512],
        target_sizes=[[512, 256]],
    )
    for record in (icon, frame):
        source = Path(record["source"])
        if record["id"] == "health":
            image = Image.open(source)
            image.paste((98, 220, 255, 255), (8, 8, 56, 56))
            image.save(source)
        else:
            image = Image.open(source)
            image.paste((98, 220, 255, 255), (0, 0, 1024, 60))
            image.paste((98, 220, 255, 255), (0, 452, 1024, 512))
            image.paste((98, 220, 255, 255), (0, 0, 60, 512))
            image.paste((98, 220, 255, 255), (964, 0, 1024, 512))
            image.save(source)
        record["source_sha256"] = record["alpha_sha256"] = sha256_file(source)
        export_record(record, project)
    map_path = project / pipeline.MAP_RELATIVE_PATH
    map_path.parent.mkdir(parents=True)
    map_path.write_text(json.dumps([icon, frame]))

    sheets = build_contact_sheets(project)

    states = Image.open(project / pipeline.SHEETS_RELATIVE_PATH / "icon-states.png").convert("RGBA")
    assert len(sheets) == 6
    assert states.getbbox() is not None
    assert ImageChops.difference(
        states.crop((0, 0, 64, 64)).convert("RGB"),
        states.crop((64, 0, 128, 64)).convert("RGB"),
    ).getbbox() is not None
    icon_layout = icon_sheet_layout([("stats", "health", Image.new("RGBA", (16, 16)))])
    icon_labels = Image.open(project / pipeline.SHEETS_RELATIVE_PATH / "icons-16.png").convert("RGBA")
    assert any(pixel != (9, 17, 30, 255)
               for pixel in icon_labels.crop(icon_layout["entries"][0]["label_box"]).getdata())
    state_layout = state_sheet_layout([("stats", "health", Image.new("RGBA", (16, 16)))])
    assert any(pixel != (9, 17, 30, 255)
               for pixel in states.crop(state_layout["entries"][0]["label_box"]).getdata())
    ornaments = Image.open(project / pipeline.SHEETS_RELATIVE_PATH / "ornaments.png").convert("RGBA")
    assert (0, 255, 255, 255) in ornaments.getdata()


def test_icon_and_state_sheet_layouts_reserve_non_overlapping_label_regions():
    entries = [
        ("inventory", "general", Image.new("RGBA", (16, 16), (98, 220, 255, 255))),
        ("actions", "equip", Image.new("RGBA", (16, 16), (98, 220, 255, 255))),
    ]

    icon_layout = icon_sheet_layout(entries)
    state_layout = state_sheet_layout(entries)

    for layout in (icon_layout, state_layout):
        assert layout["canvas"][0] > 0 and layout["canvas"][1] > 0
        boxes = [entry["label_box"] for entry in layout["entries"]]
        assert all(left >= 0 and top >= 0 and right <= layout["canvas"][0] and bottom <= layout["canvas"][1]
                   for left, top, right, bottom in boxes)
        assert all(first[2] <= second[0] or second[2] <= first[0] or first[3] <= second[1] or second[3] <= first[1]
                   for index, first in enumerate(boxes) for second in boxes[index + 1:])
        assert all(entry["category"] in {"inventory", "actions"} and entry["id"] in {"general", "equip"}
                   for entry in layout["entries"])
    assert {entry["state"] for entry in state_layout["entries"]} == {
        "normal", "focused", "selected", "disabled",
    }


def test_manifest_emits_full_record_provenance_and_sirius_statement(tmp_path: Path):
    project = tmp_path / "project"
    master = tmp_path / "master.png"
    Image.new("RGBA", (64, 64), (0, 0, 0, 0)).save(master)
    record = _record(master, asset_id="general", category="inventory")
    record.update({
        "source": "art_source/ui/hpa-374/boards/inventory-actions/general-source.png",
        "alpha_source": "art_source/ui/hpa-374/boards/inventory-actions/general-alpha.png",
        "source_sha256": "a" * 64, "alpha_sha256": "b" * 64,
        "source_size": [1254, 1254], "alpha_size": [1254, 1254],
        "crop": [0, 0, 1254, 1254], "target_sizes": [[16, 16], [24, 24], [32, 32]],
        "generator": "OpenAI image_gen", "generated_on": "2026-07-29",
        "family": "inventory-actions",
        "postprocess": {"auto_key": "border", "soft_matte": True, "transparent_threshold": 12,
                        "opaque_threshold": 220, "despill": True, "edge_contract": 0},
    })
    map_path = project / pipeline.MAP_RELATIVE_PATH
    map_path.parent.mkdir(parents=True)
    map_path.write_text(json.dumps([record]))

    manifest = write_manifest(map_path, project).read_text()

    for expected in (
        "generated specifically for Sirius", "not sourced from a third-party art pack",
        "general-source.png", "general-alpha.png", "a" * 64, "b" * 64,
        "1254x1254", "[0, 0, 1254, 1254]", "OpenAI image_gen", "2026-07-29",
        "inventory-actions", "Inventory heading icon", "inventory-actions.md",
    ):
        assert expected in manifest


def test_profiled_source_exports_straight_rgba_without_profile(tmp_path: Path):
    source = tmp_path / "profiled.png"
    image = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    image.paste((98, 220, 255, 255), (8, 8, 56, 56))
    from PIL import ImageCms

    profile = ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes()
    image.save(source, icc_profile=profile)
    [output] = export_record(_record(source), tmp_path / "project")
    with Image.open(output) as exported:
        assert exported.mode == "RGBA"
        assert "icc_profile" not in exported.info
        assert exported.getchannel("A").getextrema()[0] == 0


def test_cli_contact_sheets_and_source_boundary(tmp_path: Path, monkeypatch):
    project = tmp_path / "project"
    monkeypatch.setattr(sys, "argv", ["ui_art_pipeline.py", "contact-sheets", "--project-root", str(project)])
    assert pipeline.main() == 0
    repo_root = Path(__file__).resolve().parents[2]
    result = subprocess.run(
        ["git", "check-ignore", "-v", "--no-index", "art_source/ui/hpa-374/boards/example.png"],
        cwd=repo_root,
        check=True,
        text=True,
        capture_output=True,
    )
    assert "art_source/ui/hpa-374/boards/" in result.stdout


def test_direct_cli_script_exposes_commands():
    repo_root = Path(__file__).resolve().parents[2]
    result = subprocess.run(
        [sys.executable, "tools/ui_art_pipeline.py", "--help"],
        cwd=repo_root,
        text=True,
        capture_output=True,
    )
    assert result.returncode == 0, result.stderr
    assert "contact-sheets" in result.stdout


@pytest.mark.parametrize("command", ("register", "extract", "verify", "manifest"))
def test_cli_routes_each_manifest_command(tmp_path: Path, monkeypatch, command: str):
    calls = []
    monkeypatch.setattr(pipeline, "register_family", lambda *args: calls.append(("register", args)))
    monkeypatch.setattr(pipeline, "extract_family", lambda *args: calls.append(("extract", args)))
    monkeypatch.setattr(pipeline, "verify_family", lambda *args: calls.append(("verify", args)))
    monkeypatch.setattr(pipeline, "write_manifest", lambda *args: calls.append(("manifest", args)))
    args = ["ui_art_pipeline.py", command, "--project-root", str(tmp_path)]
    if command in {"register", "extract", "verify"}:
        args.extend(("--family", "stats-status"))
    monkeypatch.setattr(sys, "argv", args)

    assert pipeline.main() == 0
    assert calls[0][0] == command
