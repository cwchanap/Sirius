#!/usr/bin/env python3
"""Resize item sprites from the original source directory to 96x96 PNGs.

Usage:
    python3 tools/resize_item_icons.py
    python3 tools/resize_item_icons.py --size 96 --source assets/sprites/items/original --dest assets/sprites/items
    python3 tools/resize_item_icons.py --skip-background-removal --source /tmp/item_icon_source --dest /tmp/item_icon_out

The script requires Pillow (`pip install pillow`).
"""

import argparse
from collections import Counter, deque
from pathlib import Path
from typing import Iterable

from PIL import Image


DEFAULT_SIZE = 96
DEFAULT_SOURCE = Path("assets/sprites/items/original")
DEFAULT_DEST = Path("assets/sprites/items")
SUPPORTED_EXTS = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}
DEFAULT_BACKGROUND_TOLERANCE = 24
DEFAULT_EDGE_PALETTE_SIZE = 8
DEFAULT_COLOR_QUANTIZATION = 8


def iter_source_files(source: Path) -> Iterable[Path]:
    if not source.exists():
        raise FileNotFoundError(f"Source directory not found: {source}")

    return sorted(
        file
        for file in source.rglob("*")
        if file.is_file() and file.suffix.lower() in SUPPORTED_EXTS
    )


def iter_edge_coords(width: int, height: int) -> Iterable[tuple[int, int]]:
    seen = set()

    for x in range(width):
        coord = (x, 0)
        if coord not in seen:
            seen.add(coord)
            yield coord

        coord = (x, height - 1)
        if coord not in seen:
            seen.add(coord)
            yield coord

    for y in range(height):
        coord = (0, y)
        if coord not in seen:
            seen.add(coord)
            yield coord

        coord = (width - 1, y)
        if coord not in seen:
            seen.add(coord)
            yield coord


def has_real_transparency(image: Image.Image) -> bool:
    min_alpha, _ = image.getchannel("A").getextrema()
    return min_alpha < 255


def quantize_color(rgb: tuple[int, int, int], step: int) -> tuple[int, int, int]:
    return tuple((channel // step) * step for channel in rgb)


def build_edge_palette(
    image: Image.Image,
    palette_size: int,
    quantization: int,
) -> list[tuple[int, int, int]]:
    counter: Counter[tuple[int, int, int]] = Counter()

    for coord in iter_edge_coords(*image.size):
        rgb = image.getpixel(coord)[:3]
        counter[quantize_color(rgb, quantization)] += 1

    return [color for color, _ in counter.most_common(palette_size)]


def matches_background(
    rgb: tuple[int, int, int],
    palette: list[tuple[int, int, int]],
    tolerance: int,
) -> bool:
    return any(
        max(abs(rgb[index] - color[index]) for index in range(3)) <= tolerance
        for color in palette
    )


def remove_edge_connected_background(
    image: Image.Image,
    tolerance: int,
    palette_size: int,
    quantization: int,
) -> tuple[Image.Image, int]:
    if has_real_transparency(image):
        return image, 0

    palette = build_edge_palette(image, palette_size=palette_size, quantization=quantization)
    if not palette:
        return image, 0

    width, height = image.size
    queue: deque[tuple[int, int]] = deque()
    visited: set[tuple[int, int]] = set()
    background_pixels: list[tuple[int, int]] = []

    for coord in iter_edge_coords(width, height):
        rgb = image.getpixel(coord)[:3]
        if matches_background(rgb, palette, tolerance):
            queue.append(coord)
            visited.add(coord)

    while queue:
        x, y = queue.popleft()
        background_pixels.append((x, y))

        for dx, dy in (
            (-1, -1),
            (0, -1),
            (1, -1),
            (-1, 0),
            (1, 0),
            (-1, 1),
            (0, 1),
            (1, 1),
        ):
            nx = x + dx
            ny = y + dy
            if nx < 0 or nx >= width or ny < 0 or ny >= height:
                continue

            coord = (nx, ny)
            if coord in visited:
                continue
            visited.add(coord)

            rgb = image.getpixel(coord)[:3]
            if matches_background(rgb, palette, tolerance):
                queue.append(coord)

    if not background_pixels:
        return image, 0

    cleaned = image.copy()
    pixels = cleaned.load()
    for x, y in background_pixels:
        pixels[x, y] = (0, 0, 0, 0)

    return cleaned, len(background_pixels)


def resize_image(
    source_path: Path,
    dest_path: Path,
    size: int,
    remove_background: bool,
    background_tolerance: int,
    edge_palette_size: int,
    color_quantization: int,
) -> tuple[int, bool]:
    dest_path.parent.mkdir(parents=True, exist_ok=True)

    with Image.open(source_path) as image:
        image = image.convert("RGBA")
        removed_pixels = 0
        if remove_background:
            image, removed_pixels = remove_edge_connected_background(
                image,
                tolerance=background_tolerance,
                palette_size=edge_palette_size,
                quantization=color_quantization,
            )
        resized = image.resize((size, size), Image.Resampling.NEAREST)
        resized.save(dest_path.with_suffix(".png"))
        return removed_pixels, has_real_transparency(resized)


def process_directory(
    source: Path,
    dest: Path,
    size: int,
    remove_background: bool,
    background_tolerance: int,
    edge_palette_size: int,
    color_quantization: int,
) -> None:
    files = list(iter_source_files(source))
    if not files:
        print(f"No image files found in {source} (supported: {', '.join(sorted(SUPPORTED_EXTS))}).")
        return

    for file in files:
        relative = file.relative_to(source)
        output_path = dest / relative
        output_path = output_path.with_suffix(".png")
        removed_pixels, has_transparency = resize_image(
            file,
            output_path,
            size,
            remove_background=remove_background,
            background_tolerance=background_tolerance,
            edge_palette_size=edge_palette_size,
            color_quantization=color_quantization,
        )
        note = "preserved alpha" if has_transparency else "opaque output"
        if removed_pixels:
            note = f"removed {removed_pixels} background pixels; {note}"
        print(f"Resized {file} -> {output_path} ({note})")


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
    parser.add_argument(
        "--skip-background-removal",
        action="store_true",
        help="Do not convert edge-connected opaque background pixels to alpha before resizing.",
    )
    parser.add_argument(
        "--background-tolerance",
        type=int,
        default=DEFAULT_BACKGROUND_TOLERANCE,
        help="Per-channel tolerance for matching edge-connected background colors (default: %(default)s)",
    )
    parser.add_argument(
        "--edge-palette-size",
        type=int,
        default=DEFAULT_EDGE_PALETTE_SIZE,
        help="Number of dominant edge colors to treat as possible background seeds (default: %(default)s)",
    )
    parser.add_argument(
        "--color-quantization",
        type=int,
        default=DEFAULT_COLOR_QUANTIZATION,
        help="Quantization step for edge palette detection (default: %(default)s)",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    process_directory(
        args.source,
        args.dest,
        args.size,
        remove_background=not args.skip_background_removal,
        background_tolerance=args.background_tolerance,
        edge_palette_size=args.edge_palette_size,
        color_quantization=args.color_quantization,
    )


if __name__ == "__main__":
    main()
