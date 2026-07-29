"""Deterministically register, export, and review HPA-374 UI art."""

from __future__ import annotations

import argparse
from datetime import date
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
from typing import Iterable

from PIL import Image

from tools.ui_art_spec import EFFECT_SIZES, ICON_FAMILIES, ICON_GROUPS, ORNAMENT_SIZES

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


def export_record(record: dict, project_root: Path, allow_replace: bool = False) -> list[Path]:
    project_root = Path(project_root)
    source = _source_path(record, project_root)
    expected_hash = record.get("alpha_sha256", record["source_sha256"])
    if sha256_file(source) != expected_hash:
        raise SourceHashMismatch(source)
    x, y, width, height = record["crop"]
    if width <= 0 or height <= 0:
        raise ValueError(f"Invalid crop for {record['id']}")
    with Image.open(source) as opened:
        image = opened.convert("RGBA").crop((x, y, x + width, y + height))
    outputs = [runtime_path(record, project_root, width, height) for width, height in record["target_sizes"]]
    existing = next((output for output in outputs if output.exists()), None)
    if existing is not None and not allow_replace:
        raise TargetExistsError(existing)
    for (target_width, target_height), output in zip(record["target_sizes"], outputs):
        if width * target_height != height * target_width:
            raise ValueError(f"Nonuniform scaling rejected for {record['id']}")
        output.parent.mkdir(parents=True, exist_ok=True)
        resized = premultiplied_resize(image, (target_width, target_height))
        if record["id"] == "calibration_ticks":
            resized = enforce_horizontal_seam(resized)
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
                if record["id"] == "calibration_ticks" and image.crop((0, 0, 1, image.height)).tobytes() != image.crop((image.width - 1, 0, image.width, image.height)).tobytes():
                    raise ValueError(f"Calibration seam mismatch: {path}")
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
        promoted = []
        for staged_path in staged:
            destination = project_root / staged_path.relative_to(staged_project)
            destination.parent.mkdir(parents=True, exist_ok=True)
            os.replace(staged_path, destination)
            promoted.append(destination)
        return promoted
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


def build_contact_sheets(project_root: Path) -> list[Path]:
    """Build review sheets from registered-and-extracted records only."""
    project_root = Path(project_root)
    icons = {size: [] for size in (16, 24, 32)}
    ornaments, effects, states = [], [], []
    for record in _load_map(project_root / MAP_RELATIVE_PATH):
        if record["kind"] == "icon":
            copies = []
            for width, height in record["target_sizes"]:
                path = runtime_path(record, project_root, width, height)
                if not path.exists():
                    continue
                with Image.open(path) as image:
                    copy = image.convert("RGBA").copy()
                icons[width].append(copy)
                copies.append(copy)
            if not copies:
                continue
            copy = copies[-1]
            for opacity in (255, 255, 255, 115):
                state = copy.copy()
                if opacity != 255:
                    state.putalpha(state.getchannel("A").point(lambda alpha: alpha * opacity // 255))
                states.append(state)
        else:
            width, height = record["target_sizes"][-1]
            path = runtime_path(record, project_root, width, height)
            if not path.exists():
                continue
            with Image.open(path) as image:
                copy = image.convert("RGBA").copy()
        if record["kind"] == "ornament":
            ornaments.extend([copy] * 3 if record["id"] == "calibration_ticks" else [copy] * 9 if record["id"] == "callout_frame" else [copy])
        else:
            if record["kind"] == "effect":
                effects.append(copy)
    root = project_root / SHEETS_RELATIVE_PATH
    return ([_sheet(root / f"icons-{size}.png", images, cell=size) for size, images in icons.items()] +
            [_sheet(root / "icon-states.png", states), _sheet(root / "ornaments.png", ornaments), _sheet(root / "effects.png", effects)])


def write_manifest(map_path: Path, project_root: Path) -> Path:
    lines = ["# HPA-374 UI Art Source Manifest", "", "| ID | Family | Source | Runtime derivatives |", "| --- | --- | --- | --- |"]
    for record in _load_map(Path(map_path)):
        paths = [runtime_path(record, Path(project_root), width, height).relative_to(project_root).as_posix() for width, height in record["target_sizes"]]
        lines.append(f"| `{record['id']}` | `{record['family']}` | `{record['alpha_source']}` | " + "<br>".join(f"`{path}`" for path in paths) + " |")
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
    elif args.command == "contact-sheets":
        build_contact_sheets(root)
    else:
        write_manifest(map_path, root)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
