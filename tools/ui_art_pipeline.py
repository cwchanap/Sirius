"""Deterministically register, export, and review HPA-374 UI art."""

from __future__ import annotations

import argparse
from datetime import date
import hashlib
from io import BytesIO
import json
import os
from pathlib import Path
import shutil
import tempfile
from typing import Iterable

from PIL import Image, ImageCms, ImageDraw, ImageFont

try:
    from tools.ui_art_spec import EFFECT_SIZES, ICON_FAMILIES, ICON_GROUPS, ORNAMENT_SIZES
except ModuleNotFoundError:  # Direct ``python tools/ui_art_pipeline.py`` invocation.
    from ui_art_spec import EFFECT_SIZES, ICON_FAMILIES, ICON_GROUPS, ORNAMENT_SIZES

MAP_RELATIVE_PATH = Path("docs/ui/hpa-374/sources/extraction-map.json")
SHEETS_RELATIVE_PATH = Path("docs/ui/hpa-374/contact-sheets")
ICON_TARGET_SIZES = ((16, 16), (24, 24), (32, 32))
POSTPROCESS = {"auto_key": "border", "soft_matte": True, "transparent_threshold": 12,
               "opaque_threshold": 220, "despill": True}


class SourceHashMismatch(RuntimeError):
    pass


class TargetExistsError(RuntimeError):
    pass


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def premultiplied_resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    # Pillow's RGBa mode stores premultiplied color channels. Convert back to
    # straight RGBA only after Lanczos has finished filtering the edge pixels.
    return image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")


def enforce_horizontal_seam(image: Image.Image) -> Image.Image:
    repaired = image.convert("RGBA").copy()
    left = repaired.crop((0, 0, 1, repaired.height))
    right = repaired.crop((repaired.width - 1, 0, repaired.width, repaired.height))
    seam = Image.blend(left, right, 0.5)
    repaired.paste(seam, (0, 0))
    repaired.paste(seam, (repaired.width - 1, 0))
    return repaired


def _is_chroma_contamination(red: int, green: int, blue: int, alpha: int) -> bool:
    return bool(alpha and green > 160 and green > red * 1.3 and green > blue * 1.3)


def remove_low_alpha_chroma_residue(image: Image.Image, *, opaque_threshold: int) -> Image.Image:
    """Discard green-key residue introduced by resampling without touching cyan or gold edges."""
    cleaned = image.convert("RGBA").copy()
    cleaned.putdata([
        (0, 0, 0, 0) if 0 < alpha < opaque_threshold and _is_chroma_contamination(red, green, blue, alpha)
        else (red, green, blue, alpha)
        for red, green, blue, alpha in cleaned.getdata()
    ])
    return cleaned


def remove_scoped_high_alpha_chroma_residue(image: Image.Image, postprocess: dict) -> Image.Image:
    """Despill opaque key residue only for the explicitly registered exceptional master."""
    if not postprocess.get("high_alpha_chroma_despill", False):
        return image
    opaque_threshold = postprocess.get("opaque_threshold", POSTPROCESS["opaque_threshold"])
    cleaned = image.convert("RGBA").copy()
    cleaned.putdata([
        (red, min(green, int(max(red, blue) * 1.3)), blue, alpha)
        if alpha >= opaque_threshold and _is_chroma_contamination(red, green, blue, alpha)
        else (red, green, blue, alpha)
        for red, green, blue, alpha in cleaned.getdata()
    ])
    return cleaned


def runtime_path(record: dict, project_root: Path, target_width: int, target_height: int) -> Path:
    asset_id = record["id"]
    match record["kind"]:
        case "icon":
            if target_width != target_height:
                raise ValueError(f"Icon target must be square: {asset_id}")
            return project_root / "assets/sprites/ui/icons" / record["category"] / str(target_width) / f"{asset_id}.png"
        case "ornament":
            return project_root / "assets/sprites/ui/ornaments" / f"{asset_id}.png"
        case "effect":
            return project_root / "assets/sprites/effects/ui" / f"{asset_id}.png"
        case _:
            raise ValueError(f"Unknown art kind: {record['kind']}")


def centered_aspect_crop(source_size: tuple[int, int], target_size: tuple[int, int]) -> tuple[int, int, int, int]:
    source_width, source_height = source_size
    target_width, target_height = target_size
    if source_width * target_height > source_height * target_width:
        crop_height = source_height
        crop_width = source_height * target_width // target_height
    else:
        crop_width = source_width
        crop_height = source_width * target_height // target_width
    return ((source_width - crop_width) // 2, (source_height - crop_height) // 2, crop_width, crop_height)


def _source_path(record: dict, project_root: Path) -> Path:
    source = Path(record.get("alpha_source", record["source"]))
    return source if source.is_absolute() else project_root / source


def _normalize_srgb(image: Image.Image) -> Image.Image:
    """Convert embedded profiles to sRGB and discard all profile metadata."""
    profile = image.info.get("icc_profile")
    if profile:
        try:
            source_profile = ImageCms.ImageCmsProfile(BytesIO(profile))
            image = ImageCms.profileToProfile(
                image, source_profile, ImageCms.createProfile("sRGB"), outputMode="RGBA"
            )
        except (ImageCms.PyCMSError, OSError, ValueError):
            image = image.convert("RGBA")
    else:
        image = image.convert("RGBA")
    image.info.pop("icc_profile", None)
    image.info.pop("srgb", None)
    return image


def _strict_record(record: dict) -> bool:
    # Small direct-export fixtures remain useful for core scaling behavior.
    # Registered records always carry a family and must satisfy release checks.
    return "family" in record


def _validate_alpha_source(record: dict, image: Image.Image) -> None:
    if not _strict_record(record):
        return
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    visible = alpha.getbbox()
    if visible is None or alpha.getextrema()[0] != 0:
        raise ValueError(f"Missing transparent alpha for {record['id']}")
    if any(_is_chroma_contamination(*pixel) for pixel in rgba.getdata()):
        raise ValueError(f"Chroma contamination for {record['id']}")


def _validate_final_asset(record: dict, image: Image.Image) -> None:
    if not _strict_record(record):
        return
    alpha = image.getchannel("A")
    visible = alpha.getbbox()
    if visible is None or alpha.getextrema()[0] != 0:
        raise ValueError(f"Missing transparent alpha for {record['id']}")
    left, top, right, bottom = visible
    is_callout_frame = record["id"] == "callout_frame"
    touches_horizontal = record["id"] == "calibration_ticks"
    if any(_is_chroma_contamination(*pixel) for pixel in image.convert("RGBA").getdata()):
        raise ValueError(f"Chroma contamination for {record['id']}")
    if (not is_callout_frame and
            (top < 1 or bottom > image.height - 1 or (not touches_horizontal and (left < 1 or right > image.width - 1)))):
        raise ValueError(f"Final safety inset violated for {record['id']}")
    if touches_horizontal:
        if left != 0 or right != image.width or top < 1 or bottom > image.height - 1:
            raise ValueError("Calibration ticks must touch both horizontal edges and retain vertical safety inset")
        if image.crop((0, 0, 1, image.height)).tobytes() != image.crop((image.width - 1, 0, image.width, image.height)).tobytes():
            raise ValueError("Calibration seam mismatch")
        if alpha.crop((0, 0, 1, image.height)).getbbox() is None:
            raise ValueError("Calibration ticks must have nonempty horizontal edges")
    if record["kind"] == "icon" and image.size == (16, 16):
        core = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
        if core is None:
            raise ValueError(f"Unreadable 16px silhouette for {record['id']}")
        core_width, core_height = core[2] - core[0], core[3] - core[1]
        if core_width < 0.30 * 16 or core_height < 0.30 * 16 or max(core_width, core_height) < 0.50 * 16:
            raise ValueError(f"Unreadable 16px silhouette for {record['id']}")
    if is_callout_frame:
        if alpha.crop((32, 32, image.width - 32, image.height - 32)).getextrema()[1] != 0:
            raise ValueError("Callout frame center must remain transparent")
        bands = ((0, 0, image.width, 32), (0, image.height - 32, image.width, image.height),
                 (0, 0, 32, image.height), (image.width - 32, 0, image.width, image.height))
        if any(alpha.crop(band).getbbox() is None for band in bands):
            raise ValueError("Callout frame border must remain visible")


def _apply_alpha_contract(record: dict, image: Image.Image) -> Image.Image:
    """Apply the recorded chroma-helper alpha thresholds after filtering."""
    postprocess = {**POSTPROCESS, **record.get("postprocess", {})}
    transparent = postprocess["transparent_threshold"]
    opaque = postprocess["opaque_threshold"]
    cleaned = image.convert("RGBA").copy()
    cleaned.putalpha(cleaned.getchannel("A").point(
        lambda value: 0 if value < transparent else 255 if value >= opaque else value
    ))
    return cleaned


def _validate_export_request(record: dict, source: Path, image: Image.Image) -> tuple[int, int, int, int]:
    crop = record.get("crop")
    if not isinstance(crop, (list, tuple)) or len(crop) != 4:
        raise ValueError(f"Invalid crop for {record['id']}")
    x, y, width, height = crop
    if not all(isinstance(value, int) for value in crop) or x < 0 or y < 0 or width <= 0 or height <= 0:
        raise ValueError(f"Invalid crop for {record['id']}")
    if x + width > image.width or y + height > image.height:
        raise ValueError(f"Crop exceeds source bounds for {record['id']}")
    targets = record.get("target_sizes")
    if not isinstance(targets, list) or not targets:
        raise ValueError(f"Invalid target sizes for {record['id']}")
    for target in targets:
        if not isinstance(target, (list, tuple)) or len(target) != 2:
            raise ValueError(f"Invalid target sizes for {record['id']}")
        target_width, target_height = target
        if not isinstance(target_width, int) or not isinstance(target_height, int) or target_width <= 0 or target_height <= 0:
            raise ValueError(f"Invalid target sizes for {record['id']}")
        if width < target_width * 2 or height < target_height * 2:
            raise ValueError(f"Source is smaller than 2x its target: {record['id']}")
        if width * target_height != height * target_width:
            raise ValueError(f"Nonuniform scaling rejected for {record['id']}")
    return x, y, width, height


def export_record(record: dict, project_root: Path) -> list[Path]:
    project_root = Path(project_root)
    source = _source_path(record, project_root)
    expected_hash = record.get("alpha_sha256", record["source_sha256"])
    if sha256_file(source) != expected_hash:
        raise SourceHashMismatch(source)
    with Image.open(source) as opened:
        normalized = _normalize_srgb(opened)
    x, y, width, height = _validate_export_request(record, source, normalized)
    image = normalized.crop((x, y, x + width, y + height))
    _validate_alpha_source(record, image)
    outputs = [runtime_path(record, project_root, width, height) for width, height in record["target_sizes"]]
    existing = next((output for output in outputs if output.exists()), None)
    if existing is not None:
        raise TargetExistsError(existing)
    derivatives = []
    for target_width, target_height in record["target_sizes"]:
        resized = _apply_alpha_contract(record, premultiplied_resize(image, (target_width, target_height)))
        if record["id"] == "calibration_ticks":
            resized = enforce_horizontal_seam(resized)
        resized = remove_low_alpha_chroma_residue(
            resized, opaque_threshold={**POSTPROCESS, **record.get("postprocess", {})}["opaque_threshold"]
        )
        resized = remove_scoped_high_alpha_chroma_residue(
            resized, {**POSTPROCESS, **record.get("postprocess", {})}
        )
        _validate_final_asset(record, resized)
        derivatives.append(resized)
    for output, resized in zip(outputs, derivatives):
        output.parent.mkdir(parents=True, exist_ok=True)
        resized.save(output, format="PNG", icc_profile=None, optimize=True)
    return outputs


def _family_assets(family: str) -> list[tuple[str, str, str | None, tuple[tuple[int, int], ...]]]:
    if family in ICON_FAMILIES:
        return [(asset_id, "icon", category, ICON_TARGET_SIZES)
                for category in ICON_FAMILIES[family] for asset_id in ICON_GROUPS[category]]
    if family == "ornaments":
        return [(asset_id, "ornament", None, (size,)) for asset_id, size in ORNAMENT_SIZES.items()]
    if family == "effects":
        return [(asset_id, "effect", None, (size,)) for asset_id, size in EFFECT_SIZES.items()]
    raise ValueError(f"Unknown UI art family: {family}")


def _load_map(map_path: Path) -> list[dict]:
    if not map_path.exists():
        return []
    data = json.loads(map_path.read_text(encoding="utf-8"))
    return data["records"] if isinstance(data, dict) else data


def _write_map(map_path: Path, records: Iterable[dict]) -> None:
    ordered = sorted(records, key=lambda item: (item["family"], item["kind"], item.get("category", ""), item["id"]))
    map_path.parent.mkdir(parents=True, exist_ok=True)
    map_path.write_text(json.dumps(ordered, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _project_root(source_root: Path, map_path: Path) -> Path:
    return Path(os.path.commonpath((source_root.resolve(), map_path.resolve()))).resolve()


def _relative(path: Path, project_root: Path) -> str:
    return path.resolve().relative_to(project_root).as_posix()


def _registered_record(family: str, asset_id: str, kind: str, category: str | None,
                       target_sizes: tuple[tuple[int, int], ...], source_dir: Path,
                       project_root: Path, old: dict | None) -> dict:
    source, alpha = source_dir / f"{asset_id}-source.png", source_dir / f"{asset_id}-alpha.png"
    if not source.exists() or not alpha.exists():
        raise FileNotFoundError(f"Expected source and alpha PNGs for {family}/{asset_id}")
    with Image.open(source) as image:
        source_size = image.size
    with Image.open(alpha) as image:
        alpha_image = image.convert("RGBA")
        alpha_size, bounds = alpha_image.size, alpha_image.getchannel("A").getbbox()
    if source_size != alpha_size:
        raise ValueError(f"Source and alpha dimensions differ for {family}/{asset_id}")
    if bounds is None:
        raise ValueError(f"Alpha source contains no opaque pixels: {alpha}")
    target_width, target_height = target_sizes[0]
    crop = centered_aspect_crop(alpha_size, (target_width, target_height))
    if crop[2] < target_width * 2 or crop[3] < target_height * 2:
        raise ValueError(f"Source is smaller than 2x its target: {family}/{asset_id}")
    source_hash, alpha_hash = sha256_file(source), sha256_file(alpha)
    record = {"id": asset_id, "family": family, "kind": kind, "source": _relative(source, project_root),
              "alpha_source": _relative(alpha, project_root), "source_sha256": source_hash,
              "alpha_sha256": alpha_hash, "source_size": list(source_size), "alpha_size": list(alpha_size),
              "crop": list(crop), "target_sizes": [list(size) for size in target_sizes],
              "generator": "OpenAI image_gen", "generated_on": date.today().isoformat(),
              "postprocess": {**POSTPROCESS, "edge_contract": int(asset_id == "calibration_ticks")}}
    if category is not None:
        record["category"] = category
    if old and old.get("intended_usage"):
        record["intended_usage"] = old["intended_usage"]
    if old and old.get("source_sha256") == source_hash and old.get("alpha_sha256") == alpha_hash:
        record["generated_on"] = old["generated_on"]
    return record


def register_family(family: str, source_root: Path, map_path: Path) -> list[dict]:
    """Hash and record exactly one board family, retaining the other families."""
    source_root, map_path = Path(source_root), Path(map_path)
    project_root = _project_root(source_root, map_path)
    existing = _load_map(map_path)
    old = {(record["family"], record["id"]): record for record in existing}
    records = [record for record in existing if record["family"] != family]
    registered = [_registered_record(family, asset_id, kind, category, sizes, source_root / family,
                                     project_root, old.get((family, asset_id)))
                  for asset_id, kind, category, sizes in _family_assets(family)]
    _write_map(map_path, [*records, *registered])
    return sorted(registered, key=lambda record: record["id"])


def _records_for_family(family: str, map_path: Path) -> list[dict]:
    records = sorted((record for record in _load_map(map_path) if record["family"] == family), key=lambda record: record["id"])
    if not records:
        raise ValueError(f"No registered records for family: {family}")
    return records


def _validate_record(record: dict, project_root: Path) -> None:
    if not record.get("crop"):
        raise ValueError(f"Null crop for {record['id']}")
    raw_source = Path(record["source"])
    raw_source = raw_source if raw_source.is_absolute() else project_root / raw_source
    if sha256_file(raw_source) != record["source_sha256"]:
        raise SourceHashMismatch(raw_source)
    source = _source_path(record, project_root)
    if sha256_file(source) != record.get("alpha_sha256", record["source_sha256"]):
        raise SourceHashMismatch(source)
    x, y, width, height = record["crop"]
    with Image.open(source) as image:
        if x < 0 or y < 0 or x + width > image.width or y + height > image.height:
            raise ValueError(f"Crop exceeds source bounds for {record['id']}")
    for target_width, target_height in record["target_sizes"]:
        if width < target_width * 2 or height < target_height * 2:
            raise ValueError(f"Source is smaller than 2x its target: {record['id']}")
        if width * target_height != height * target_width:
            raise ValueError(f"Nonuniform scaling rejected for {record['id']}")


def _verify_records(records: list[dict], project_root: Path) -> list[Path]:
    verified = []
    for record in records:
        for width, height in record["target_sizes"]:
            path = runtime_path(record, project_root, width, height)
            if not path.exists():
                raise FileNotFoundError(path)
            with Image.open(path) as image:
                if image.mode != "RGBA" or image.size != (width, height):
                    raise ValueError(f"Invalid runtime derivative: {path}")
                if "icc_profile" in image.info or "srgb" in image.info:
                    raise ValueError(f"Runtime derivative retains a color profile: {path}")
                _validate_final_asset(record, image.convert("RGBA"))
            verified.append(path)
    return verified


def extract_family(family: str, map_path: Path, project_root: Path) -> list[Path]:
    """Stage a complete family before promoting any canonical runtime art."""
    project_root, records = Path(project_root).resolve(), _records_for_family(family, Path(map_path))
    for record in records:
        _validate_record(record, project_root)
    targets = [runtime_path(record, project_root, width, height) for record in records for width, height in record["target_sizes"]]
    existing = next((path for path in targets if path.exists()), None)
    if existing:
        raise TargetExistsError(existing)
    temporary_root = Path(tempfile.mkdtemp(prefix=f".{family}-", dir=project_root.parent))
    staged_project = temporary_root / project_root.name
    try:
        staged = []
        for record in records:
            staged_record = dict(record)
            # Runtime paths are staged, while provenance inputs remain the
            # immutable masters registered under the real project root.
            staged_record["alpha_source"] = str(_source_path(record, project_root))
            staged.extend(export_record(staged_record, staged_project))
        _verify_records(records, staged_project)
        promoted: list[tuple[Path, Path]] = []
        try:
            for staged_path in staged:
                destination = project_root / staged_path.relative_to(staged_project)
                # The initial target scan prevents normal replacement. Check
                # again inside the transaction to preserve a concurrently
                # created canonical asset rather than silently replacing it.
                if destination.exists():
                    raise TargetExistsError(destination)
                destination.parent.mkdir(parents=True, exist_ok=True)
                os.replace(staged_path, destination)
                promoted.append((staged_path, destination))
        except Exception:
            for staged_path, destination in reversed(promoted):
                if not destination.exists():
                    continue
                try:
                    os.replace(destination, staged_path)
                except OSError:
                    # A staged derivative is disposable: its immutable master
                    # remains registered. Never leave partial canonical art.
                    destination.unlink(missing_ok=True)
            raise
        return [destination for _, destination in promoted]
    finally:
        shutil.rmtree(temporary_root, ignore_errors=True)


def repair_ornament_derivatives(asset_ids: tuple[str, ...], map_path: Path, project_root: Path) -> list[Path]:
    """Atomically replace named ornament derivatives from their hash-verified selected masters."""
    requested = tuple(dict.fromkeys(asset_ids))
    if not requested or any(asset_id not in ORNAMENT_SIZES for asset_id in requested):
        raise ValueError("Repair requires one or more known ornament IDs")
    project_root = Path(project_root).resolve()
    records_by_id = {record["id"]: record for record in _records_for_family("ornaments", Path(map_path))}
    if not set(requested) <= set(records_by_id):
        raise ValueError("Repair IDs must be registered ornament records")
    records = [records_by_id[asset_id] for asset_id in requested]
    for record in records:
        _validate_record(record, project_root)
    targets = [runtime_path(record, project_root, width, height)
               for record in records for width, height in record["target_sizes"]]
    missing = next((path for path in targets if not path.exists()), None)
    if missing is not None:
        raise FileNotFoundError(f"Scoped repair refuses to create a missing runtime target: {missing}")

    temporary_root = Path(tempfile.mkdtemp(prefix=".ornament-repair-", dir=project_root.parent))
    staged_project, backup_root = temporary_root / project_root.name, temporary_root / "backups"
    try:
        staged: list[Path] = []
        for record in records:
            staged_record = dict(record)
            staged_record["alpha_source"] = str(_source_path(record, project_root))
            staged.extend(export_record(staged_record, staged_project))
        _verify_records(records, staged_project)

        promoted: list[tuple[Path, Path]] = []
        try:
            for destination in targets:
                backup = backup_root / destination.relative_to(project_root)
                backup.parent.mkdir(parents=True, exist_ok=True)
                os.replace(destination, backup)
                promoted.append((backup, destination))
            for staged_path in staged:
                destination = project_root / staged_path.relative_to(staged_project)
                os.replace(staged_path, destination)
        except Exception:
            for _, destination in promoted:
                destination.unlink(missing_ok=True)
            for backup, destination in reversed(promoted):
                if backup.exists():
                    os.replace(backup, destination)
            raise
        return targets
    finally:
        shutil.rmtree(temporary_root, ignore_errors=True)


def verify_family(family: str, project_root: Path) -> list[Path]:
    project_root = Path(project_root)
    return _verify_records(_records_for_family(family, project_root / MAP_RELATIVE_PATH), project_root)


def _sheet(output: Path, images: list[Image.Image], cell: int = 64, columns: int = 8) -> Path:
    rows = max(1, (len(images) + columns - 1) // columns)
    sheet = Image.new("RGBA", (columns * cell, rows * cell), (9, 17, 30, 255))
    for index, image in enumerate(images):
        copy = image.convert("RGBA").copy()
        copy.thumbnail((cell, cell), Image.Resampling.LANCZOS)
        sheet.alpha_composite(copy, ((index % columns) * cell + (cell - copy.width) // 2, (index // columns) * cell + (cell - copy.height) // 2))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", icc_profile=None, optimize=True)
    return output


def icon_sheet_layout(entries: list[tuple[str, str, Image.Image]], columns: int = 6,
                      cell_width: int = 128, art_height: int = 96, label_height: int = 32) -> dict:
    """Return deterministic, non-overlapping artwork and caption boxes."""
    rows = max(1, (len(entries) + columns - 1) // columns)
    cell_height = art_height + label_height
    positioned = []
    for index, (category, asset_id, _) in enumerate(entries):
        left, top = (index % columns) * cell_width, (index // columns) * cell_height
        positioned.append({
            "category": category, "id": asset_id,
            "art_box": (left, top, left + cell_width, top + art_height),
            "label_box": (left, top + art_height, left + cell_width, top + cell_height),
        })
    return {"canvas": (columns * cell_width, rows * cell_height), "entries": positioned}


def state_sheet_layout(entries: list[tuple[str, str, Image.Image]], columns: int = 4,
                       cell_width: int = 192, art_height: int = 96, label_height: int = 32) -> dict:
    """Lay out every icon state with enough caption width for category, ID, and state."""
    expanded = [(category, asset_id, state)
                for category, asset_id, _ in entries
                for state in ("normal", "focused", "selected", "disabled")]
    rows = max(1, (len(expanded) + columns - 1) // columns)
    cell_height = art_height + label_height
    positioned = []
    for index, (category, asset_id, state) in enumerate(expanded):
        left, top = (index % columns) * cell_width, (index // columns) * cell_height
        positioned.append({
            "category": category, "id": asset_id, "state": state,
            "art_box": (left, top, left + cell_width, top + art_height),
            "label_box": (left, top + art_height, left + cell_width, top + cell_height),
        })
    return {"canvas": (columns * cell_width, rows * cell_height), "entries": positioned}


def _draw_caption(draw: ImageDraw.ImageDraw, label_box: tuple[int, int, int, int],
                  category: str, asset_id: str, state: str | None = None) -> None:
    left, top, _, _ = label_box
    font = ImageFont.load_default()
    draw.text((left + 4, top + 2), category, fill=(151, 213, 255, 255), font=font)
    suffix = f" / {state}" if state is not None else ""
    draw.text((left + 4, top + 16), f"{asset_id}{suffix}", fill=(237, 241, 255, 255), font=font)


def _labelled_icon_sheet(output: Path, entries: list[tuple[str, str, Image.Image]], size: int) -> Path:
    layout = icon_sheet_layout(entries)
    sheet = Image.new("RGBA", layout["canvas"], (9, 17, 30, 255))
    draw = ImageDraw.Draw(sheet)
    for entry, (_, _, image) in zip(layout["entries"], entries):
        left, top, right, bottom = entry["art_box"]
        art = image.convert("RGBA")
        sheet.alpha_composite(art, (left + (right - left - art.width) // 2, top + (bottom - top - art.height) // 2))
        _draw_caption(draw, entry["label_box"], entry["category"], entry["id"])
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", icc_profile=None, optimize=True)
    return output


def _icon_state_preview(image: Image.Image, state: str, cell_width: int, cell_height: int) -> Image.Image:
    preview = Image.new("RGBA", (cell_width, cell_height), (9, 17, 30, 255))
    art = image.convert("RGBA").copy()
    art.thumbnail((cell_width - 32, cell_height - 16), Image.Resampling.LANCZOS)
    if state == "disabled":
        art.putalpha(art.getchannel("A").point(lambda alpha: alpha * 115 // 255))
    preview.alpha_composite(art, ((cell_width - art.width) // 2, (cell_height - art.height) // 2))
    draw = ImageDraw.Draw(preview)
    if state == "focused":
        draw.rectangle((2, 2, cell_width - 3, cell_height - 3), outline=(0, 255, 255, 255), width=2)
    elif state == "selected":
        draw.rectangle((2, 2, cell_width - 3, cell_height - 3), outline=(255, 204, 64, 255), width=2)
    return preview


def _state_sheet(output: Path, entries: list[tuple[str, str, Image.Image]]) -> Path:
    layout = state_sheet_layout(entries)
    sheet = Image.new("RGBA", layout["canvas"], (9, 17, 30, 255))
    draw = ImageDraw.Draw(sheet)
    image_by_id = {(category, asset_id): image for category, asset_id, image in entries}
    for entry in layout["entries"]:
        left, top, right, bottom = entry["art_box"]
        image = image_by_id[(entry["category"], entry["id"])]
        sheet.alpha_composite(_icon_state_preview(image, entry["state"], right - left, bottom - top), (left, top))
        _draw_caption(draw, entry["label_box"], entry["category"], entry["id"], entry["state"])
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, format="PNG", icc_profile=None, optimize=True)
    return output


def _nine_patch_guide(image: Image.Image) -> Image.Image:
    guided = image.convert("RGBA").copy()
    draw = ImageDraw.Draw(guided)
    width, height = guided.size
    guide = (0, 255, 255, 255)
    draw.line((32, 0, 32, height - 1), fill=guide, width=16)
    draw.line((width - 33, 0, width - 33, height - 1), fill=guide, width=16)
    draw.line((0, 32, width - 1, 32), fill=guide, width=16)
    draw.line((0, height - 33, width - 1, height - 33), fill=guide, width=16)
    return guided


def build_contact_sheets(project_root: Path) -> list[Path]:
    """Build review sheets from registered-and-extracted records only."""
    project_root = Path(project_root)
    icons = {size: [] for size in (16, 24, 32)}
    ornaments, effects = [], []
    states: list[tuple[str, str, Image.Image]] = []
    for record in _load_map(project_root / MAP_RELATIVE_PATH):
        if record["kind"] == "icon":
            copies = []
            for width, height in record["target_sizes"]:
                path = runtime_path(record, project_root, width, height)
                if not path.exists():
                    continue
                with Image.open(path) as image:
                    copy = image.convert("RGBA").copy()
                icons[width].append((record["category"], record["id"], copy))
                copies.append(copy)
            if not copies:
                continue
            states.append((record["category"], record["id"], copies[-1]))
        else:
            width, height = record["target_sizes"][-1]
            path = runtime_path(record, project_root, width, height)
            if not path.exists():
                continue
            with Image.open(path) as image:
                copy = image.convert("RGBA").copy()
        if record["kind"] == "ornament":
            if record["id"] == "calibration_ticks":
                ornaments.extend([copy] * 3)
            elif record["id"] == "callout_frame":
                ornaments.append(_nine_patch_guide(copy))
            else:
                ornaments.append(copy)
        else:
            if record["kind"] == "effect":
                effects.append(copy)
    root = project_root / SHEETS_RELATIVE_PATH
    return ([_labelled_icon_sheet(root / f"icons-{size}.png", images, size) for size, images in icons.items()] +
            [_state_sheet(root / "icon-states.png", states), _sheet(root / "ornaments.png", ornaments), _sheet(root / "effects.png", effects)])


INTENDED_USAGE = {
    "general": "Inventory general category heading icon",
    "equipment": "Inventory equipment category heading icon",
    "consumable": "Inventory consumable category tab icon",
    "quest": "Inventory quest category tab icon",
    "weapon": "Empty weapon equipment slot glyph",
    "shield": "Empty shield equipment slot glyph",
    "armor": "Empty armor equipment slot glyph",
    "helmet": "Empty helmet equipment slot glyph",
    "shoe": "Empty shoe equipment slot glyph",
    "accessory": "Empty accessory equipment slot glyph",
    "locked": "Inactive accessory placeholder",
    "active_skill": "Active-skill selector/slot glyph",
    "equip": "Equip selected item action",
    "unequip": "Unequip selected item action",
    "use": "Use selected consumable action",
    "assign": "Assign active skill action",
    "buy": "Shop purchase action",
    "sell": "Shop sale action",
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
    "encounter_burst": "Battle encounter transition burst",
    "hit_impact": "Battle hit-impact overlay",
    "status_pulse": "Status-effect pulse overlay",
    "reward_level_up": "Level-up reward overlay",
}


def _intended_usage(record: dict) -> str:
    if "intended_usage" in record:
        return record["intended_usage"]
    if record["id"] in INTENDED_USAGE:
        return INTENDED_USAGE[record["id"]]
    return f"{record['family']} UI artwork"


def write_manifest(map_path: Path, project_root: Path) -> Path:
    lines = [
        "# HPA-374 UI Art Source Manifest", "",
        "The UI artwork listed in this manifest was generated specifically for Sirius",
        "with OpenAI image_gen and was not sourced from a third-party art pack.",
    ]
    for record in _load_map(Path(map_path)):
        paths = [runtime_path(record, Path(project_root), width, height).relative_to(project_root).as_posix() for width, height in record["target_sizes"]]
        prompt = f"prompts/{record['family']}.md"
        lines.extend([
            "", f"## `{record['id']}`", "",
            f"- Family: `{record['family']}`",
            f"- Selected source: `{record['source']}`",
            f"- Source SHA-256: `{record['source_sha256']}`",
            f"- Selected alpha source: `{record['alpha_source']}`",
            f"- Alpha SHA-256: `{record['alpha_sha256']}`",
            f"- Actual source size: `{record['source_size']}` ({record['source_size'][0]}x{record['source_size'][1]})",
            f"- Actual alpha size: `{record['alpha_size']}` ({record['alpha_size'][0]}x{record['alpha_size'][1]})",
            f"- Crop: `{record['crop']}`",
            f"- Target sizes: `{record['target_sizes']}`",
            f"- Post-process: `{json.dumps(record['postprocess'], sort_keys=True)}`",
            f"- Generator/date: `{record['generator']}` / `{record['generated_on']}`",
            f"- Prompt reference: [`{record['family']}.md`]({prompt})",
            f"- Intended usage: {_intended_usage(record)}",
            "- Runtime derivatives: " + ", ".join(f"`{path}`" for path in paths),
        ])
    if any(record["id"] == "weapon" and record["family"] == "inventory-actions"
           for record in _load_map(Path(map_path))):
        lines.extend([
            "", "## Weapon replacement history", "",
            "The first `weapon` source was rejected by the unmodified 16px opaque-core validation: its tall, narrow silhouette could not satisfy that gate while retaining the required one-pixel transparent inset.",
            "The rejected ignored masters remain at `weapon-rejected-source.png` (`92ae4f56482ad06eff8fdf155033f291be1516cbe3c092fabaa252cb582f8955`) and `weapon-rejected-alpha.png` (`113698e0a114d479c210f523d16930912314323639bd5a1fa0c746eafa8a7dc8`).",
            "One targeted regeneration produced the accepted ignored masters `weapon-replacement-source.png` (`d423dbe54083d82cbac606f0a75c17e288a48692857362c642444d7d41068287`) and `weapon-replacement-alpha.png` (`ef557c76520d1952586ea9b83f962093b98699c0c3962544a025adeaa4890662`), which were copied into the registered `weapon` source names before extraction.",
        ])
    if any(record["id"] == "weaken" and record["family"] == "stats-status"
           for record in _load_map(Path(map_path))):
        lines.extend([
            "", "## Weaken replacement history", "",
            "The initial `weaken` source was rejected by the unmodified 16px opaque-core validation: its separated blade fragments produced an unreadable narrow silhouette. The rejected ignored masters remain at `weaken-rejected-source.png` (`d9ee160e049d30762a0893ac1a8372798c29e2e97f202d2538980a74dbe80607`) and `weaken-rejected-alpha.png` (`c41f28c97978983b77616fdff30c3fd6f3b69daa6a9f9182cd0475db689e3654`).",
            "Three targeted built-in regenerations were then rejected before runtime promotion: `weaken-replacement-1-rejected-source.png` (`325a9c5b7b79b72691e37d6bbf82c6ad777ebc97dd2a2e3603eb7333af152380`) / `weaken-replacement-1-rejected-alpha.png` (`a23b46dade9730851c692edbf2a7bb4a19094344f1fb5abc5e783051ad52e161`) still had a four-pixel 16px core; `weaken-replacement-2-rejected-source.png` (`e37123d4503c46faeb92d0b9bf01d7170ff734f65c9228121840e2d16d5dc52a`) / `weaken-replacement-2-rejected-alpha.png` (`c812ffb3be29704c801b23a75cf00e3d2c723641255eeb15fe88d1e4460a499a`) violated the final one-pixel safety inset; and `weaken-replacement-3-rejected-source.png` (`b78fabfca01fe305bdcce7b8dc3a450dc365c54366a0162f00d62e3005f3e4ac`) / `weaken-replacement-3-rejected-alpha.png` (`4e7c00417a72a39b08e8107d120d352aebd38ef21ee0756d34b8558bc002cd3a`) retained a bottom-edge alpha pixel after the unmodified pipeline contract.",
            "The accepted fourth replacement remains at `weaken-replacement-4-source.png` (`ea3b087ea4bcc482b09bbbf154775a3469d7c9c41586a2baf9be93a5363b10a2`) and `weaken-replacement-4-alpha.png` (`9c736563e4aa44199242dcd0571b600cae422435ba0091e96fcc6a053d88aa62`). It produces an 8x12px opaque 16px core and preserves the one-pixel inset under the unmodified extractor.",
        ])
    if any(record["id"] == "save" and record["family"] == "flow-semantic"
           for record in _load_map(Path(map_path))):
        lines.extend([
            "", "## Save replacement history", "",
            "The initial `save` source was rejected by the unmodified 16px opaque-core validation before runtime promotion: its narrow archive-and-ray silhouette was unreadable after downscaling.",
            "The rejected ignored masters remain at `save-rejected-source.png` (`6a8191228b9c7aa4bfeafa440dfb3d6cf8e01818fa4c2cbc1e5a93e9c1b22c0d`) and `save-rejected-alpha.png` (`4ab5516e2095d3e772424af012408bd41f496f12e06355e6a512f3f128d43ec7`).",
            "One targeted built-in regeneration produced the accepted ignored masters `save-replacement-source.png` (`436e0ee4cefe5671c9dfa0c558847b6fcf44e1844445b281118657374561d6ae`) and `save-replacement-alpha.png` (`12f7ad549ac3ca9c000ece0616a47a616adc73ce4ca3e950de9f44305c8d964b`), which were copied into the registered `save` source names before extraction. The unmodified extractor then accepted the complete family.",
        ])
    output = Path(project_root) / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    families = (*ICON_FAMILIES, "ornaments", "effects")
    for command in ("register", "extract", "verify"):
        sub = commands.add_parser(command)
        sub.add_argument("--family", required=True, choices=families)
        sub.add_argument("--project-root", type=Path, default=Path.cwd())
        sub.add_argument("--map", type=Path)
        if command == "register":
            sub.add_argument("--source-root", type=Path)
    repair = commands.add_parser("repair-ornaments")
    repair.add_argument("--ids", nargs="+", required=True, choices=tuple(ORNAMENT_SIZES))
    repair.add_argument("--project-root", type=Path, default=Path.cwd())
    repair.add_argument("--map", type=Path)
    for command in ("contact-sheets", "manifest"):
        sub = commands.add_parser(command)
        sub.add_argument("--project-root", type=Path, default=Path.cwd())
        if command == "manifest":
            sub.add_argument("--map", type=Path)
    args = parser.parse_args()
    root = args.project_root.resolve()
    map_path = (getattr(args, "map", None) or root / MAP_RELATIVE_PATH).resolve()
    if args.command == "register":
        register_family(args.family, (args.source_root or root / "art_source/ui/hpa-374/boards").resolve(), map_path)
    elif args.command == "extract":
        extract_family(args.family, map_path, root)
    elif args.command == "verify":
        verify_family(args.family, root)
    elif args.command == "repair-ornaments":
        repair_ornament_derivatives(tuple(args.ids), map_path, root)
    elif args.command == "contact-sheets":
        build_contact_sheets(root)
    else:
        write_manifest(map_path, root)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
