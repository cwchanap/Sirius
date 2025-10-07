#!/usr/bin/env python3
"""Resize item sprites from the original source directory to 96x96 PNGs.

Usage:
    python3 tools/resize_item_icons.py
    python3 tools/resize_item_icons.py --size 96 --source assets/sprites/items/original --dest assets/sprites/items

The script requires Pillow (`pip install pillow`).
"""

import argparse
from pathlib import Path
from typing import Iterable

from PIL import Image


DEFAULT_SIZE = 96
DEFAULT_SOURCE = Path("assets/sprites/items/original")
DEFAULT_DEST = Path("assets/sprites/items")
SUPPORTED_EXTS = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}


def iter_source_files(source: Path) -> Iterable[Path]:
    if not source.exists():
        raise FileNotFoundError(f"Source directory not found: {source}")

    return sorted(
        file
        for file in source.rglob("*")
        if file.is_file() and file.suffix.lower() in SUPPORTED_EXTS
    )


def resize_image(source_path: Path, dest_path: Path, size: int) -> None:
    dest_path.parent.mkdir(parents=True, exist_ok=True)

    with Image.open(source_path) as image:
        image = image.convert("RGBA")
        resized = image.resize((size, size), Image.Resampling.NEAREST)
        resized.save(dest_path.with_suffix(".png"))


def process_directory(source: Path, dest: Path, size: int) -> None:
    files = list(iter_source_files(source))
    if not files:
        print(f"No image files found in {source} (supported: {', '.join(sorted(SUPPORTED_EXTS))}).")
        return

    for file in files:
        relative = file.relative_to(source)
        output_path = dest / relative
        output_path = output_path.with_suffix(".png")
        resize_image(file, output_path, size)
        print(f"Resized {file} -> {output_path}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Resize item sprite assets to square PNGs.")
    parser.add_argument(
        "--size",
        type=int,
        default=DEFAULT_SIZE,
        help="Target width/height in pixels (default: %(default)s)",
    )
    parser.add_argument(
        "--source",
        type=Path,
        default=DEFAULT_SOURCE,
        help="Source directory containing original sprites (default: %(default)s)",
    )
    parser.add_argument(
        "--dest",
        type=Path,
        default=DEFAULT_DEST,
        help="Destination directory for resized sprites (default: %(default)s)",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    process_directory(args.source, args.dest, args.size)


if __name__ == "__main__":
    main()
