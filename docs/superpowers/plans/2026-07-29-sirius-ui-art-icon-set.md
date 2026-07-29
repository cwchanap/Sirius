# Sirius Minimum UI Art and Icon Set Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate, validate, and integrate the complete HPA-374 Sirius UI art inventory: 62 typed icons at 16/24/32 px, 13 ornaments, 4 effects, 5 font binaries, and the minimum current Inventory/input-hint consumers.

**Architecture:** A deterministic Pillow pipeline converts one built-in image-generation output per logical asset into canonical straight-alpha runtime PNGs and review contact sheets. A typed C# catalog is the only runtime path owner; focused presenters consume it without introducing a Theme, navigation, or layout framework. Python owns file/image/provenance validation, while GdUnit4 owns typed mappings, Godot resource loading, binding refresh, and current-scene integration.

**Tech Stack:** Godot 4.6.2 Mono, C# 12/.NET 8.0, GdUnit4, Python 3.12, Pillow 12.0.0, pytest 8.4.2, built-in `image_gen`, and the installed chroma-key removal helper.

## Global Constraints

- The normative design is `docs/superpowers/specs/2026-07-28-sirius-ui-art-icon-set-design.md`.
- The live Linear baseline is HPA-374, High priority and awaiting work, with completed HPA-373 as its dependency; HPA-375 remains optional and is not promoted by this plan.
- Use the built-in `image_gen` tool, not an SVG substitute or a custom API runner. Make one built-in generation call per logical asset.
- Every generation call references `docs/ui/hpa-373/reference/battle-preparation-reference.png`; background-sensitive review also references the retained main-menu or battle background as appropriate.
- Generate on flat `#00FF00` chroma key and run the installed imagegen `remove_chroma_key.py` helper with `--auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill`.
- If an effect still fails alpha/fringe validation after one `--edge-contract 1` retry, stop and ask before using the CLI/native-transparency fallback. Do not silently change models or require `OPENAI_API_KEY`.
- Keep selected raw outputs under ignored `art_source/ui/hpa-374/boards/`; commit prompts, source filenames, SHA-256 values, crops, and post-processing metadata under `docs/ui/hpa-374/sources/`.
- Never overwrite an existing canonical asset unless the source manifest records the replacement and the user has approved that replacement. HPA-374's new canonical paths are expected to be absent.
- Preserve aspect ratio. All exports are downsample-only and use premultiplied-alpha Lanczos resampling followed by straight-alpha RGBA output.
- Use the exact palette from HPA-373: `#050714`, `#0D1530`, `#18234A`, `#27366C`, `#F7F5FF`, `#C7CEE8`, `#8F9AB8`, `#62DCFF`, `#F5D784`, `#DFAE43`, `#D96CC2`, `#68D6A3`, `#F1B85B`, and `#F16D83`.
- Icons use simplified silhouettes, a final 2 px dark outline, no baked text, no uncontrolled particles/glow, and one-pixel transparent safety inset at 16 px.
- `calibration_ticks` is the only horizontal-edge inset exception; its final left and right RGBA columns must be byte-identical and visibly nonempty.
- Keep `callout_frame`'s 32 px nine-patch border and transparent center.
- Keep `focus_halo` and `selection_halo` for circular/square nodes only. Rectangular controls continue to use Theme-owned 2 px rings.
- Icons and ornaments import losslessly without mipmaps. The four effects alone commit `.png.import` sidecars with `mipmaps/generate=true`.
- Retain `assets/sprites/ui/ui_main_menu_background.png` and `assets/sprites/ui/ui_battle_background.png` unchanged.
- Preserve the hidden Attack, Defend, and Run scene nodes unskinned; do not add `ui_button_attack.png`, `ui_button_defend.png`, or `ui_button_run.png`.
- Do not add filter, comparison, user-sort, passive-skill, or other HPA-375-only art.
- Do not change gameplay, inventory capacity, equipment rules, accessory progression, save behavior, combat flow, input bindings, or HPA-376 lifecycle semantics.
- Keep actual item PNGs on populated slots. Empty equipment/accessory slots use slot glyphs; only inactive accessory placeholders use `locked`.
- Theme-wide font wiring, battle-row restructuring, modal-header restructuring, and full ornament composition remain downstream HPA-373 screen-migration work.
- Prefix shell commands with `rtk`, per repository instructions.
- Run Python and GdUnit4 suites separately; neither substitutes for the other.

### Canonical Image-Generation Prompt

Every generated icon prompt file contains this block verbatim before its per-asset subject table:

```text
Use case: stylized-concept
Asset type: Sirius RPG UI icon master
Primary request: Generate exactly one isolated icon described below.
Input image: HPA-373 battle-preparation reference, used only for Sirius palette, celestial line language, and anime-fantasy rendering.
Scene/backdrop: perfectly flat solid #00FF00 chroma-key background for removal.
Style/medium: crisp mystical anime-fantasy UI glyph, celestial-navigation motif, simplified silhouette, controlled cel-shaded highlight, strong dark indigo outline.
Composition/framing: one centered subject on a square 1024x1024 canvas with at least 20% clear padding.
Color palette: #050714, #0D1530, #18234A, #27366C, #F7F5FF, #C7CEE8, #8F9AB8, #62DCFF, #F5D784, #DFAE43, #D96CC2, #68D6A3, #F1B85B, #F16D83.
Constraints: no text, no letters, no numbers, no watermark, no border frame, no cast shadow, no reflection, no extra object, no particles or outer glow, and do not use #00FF00 in the subject.
Avoid: photorealism, generic mobile emoji, glossy app-store icon tiles, micro-detail, chrome bevels, circuitry, and baked UI labels.
```

---
## File Structure

### Create

- `requirements-dev.txt`: pinned Python validation dependencies.
- `art_source/.gdignore`: prevents ignored generated sources from entering Godot's import graph.
- `tools/ui_art_spec.py`: exact Python inventory, target dimensions, family membership, and canonical runtime paths.
- `tools/ui_art_pipeline.py`: source registration, hash/crop validation, alpha-safe extraction, contact sheets, and family verification CLI.
- `tests/tools/test_ui_art_pipeline.py`: synthetic unit tests for crop, aspect, alpha, overwrite, hash, and seam behavior.
- `tests/tools/test_ui_asset_coverage.py`: final filesystem, image, font, provenance, negative-path, and scoped emoji acceptance tests.
- `scripts/ui/art/UiArtCatalog.cs`: typed IDs, stable paths, enum mappings, guarded Godot resource loading, and one-warning fallback behavior.
- `scripts/ui/art/UiIconPresenter.cs`: applies catalog textures to `TextureRect`, `TextureButton`, and `Button` consumers while keeping readable labels.
- `scripts/ui/art/InputHintPresenter.cs`: current-device observation plus binding resolution that re-reads `InputMap` on every refresh.
- `tests/ui/art/UiArtCatalogTest.cs`: exact-ID, enum, resource, font, background, and mipmap tests.
- `tests/ui/art/InputHintPresenterTest.cs`: keyboard, mouse, joypad-button, joypad-axis, remap-refresh, and teardown coverage.
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`: compact presenter and current Inventory integration smoke at the two HPA-374 verification sizes.
- `docs/ui/hpa-374/README.md`: shipped scope, current consumers, explicit deferrals, and verification commands.
- `docs/ui/hpa-374/ASSET_MANIFEST.md`: runtime paths, provenance, prompts, post-processing, font sources, hashes, and intended usage.
- `docs/ui/hpa-374/CONTACT_SHEETS.md`: links to committed size/state/family review sheets.
- `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`: ignored local-source names and hashes.
- `docs/ui/hpa-374/sources/extraction-map.json`: per-source actual dimensions, hashes, crop rectangles, aspect, and runtime derivatives.
- `docs/ui/hpa-374/sources/prompts/inventory-actions.md`
- `docs/ui/hpa-374/sources/prompts/stats-status.md`
- `docs/ui/hpa-374/sources/prompts/flow-semantic.md`
- `docs/ui/hpa-374/sources/prompts/input-glyphs.md`
- `docs/ui/hpa-374/sources/prompts/ornaments.md`
- `docs/ui/hpa-374/sources/prompts/effects.md`
- `docs/ui/hpa-374/contact-sheets/icons-16.png`
- `docs/ui/hpa-374/contact-sheets/icons-24.png`
- `docs/ui/hpa-374/contact-sheets/icons-32.png`
- `docs/ui/hpa-374/contact-sheets/icon-states.png`
- `docs/ui/hpa-374/contact-sheets/ornaments.png`
- `docs/ui/hpa-374/contact-sheets/effects.png`
- `assets/fonts/cinzel/Cinzel-Variable.ttf`
- `assets/fonts/cinzel/OFL.txt`
- `assets/fonts/noto_sans/NotoSans-Regular.ttf`
- `assets/fonts/noto_sans/NotoSans-Medium.ttf`
- `assets/fonts/noto_sans/NotoSans-SemiBold.ttf`
- `assets/fonts/noto_sans/OFL.txt`
- `assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf`
- `assets/fonts/noto_sans_mono/OFL.txt`
- `assets/sprites/ui/icons/<category>/<16|24|32>/<id>.png`: 186 generated icon derivatives.
- `assets/sprites/ui/ornaments/<id>.png`: 13 generated ornaments.
- `assets/sprites/effects/ui/<id>.png`: 4 generated effects.
- `assets/sprites/effects/ui/<id>.png.import`: 4 committed mipmapped effect imports.

### Modify

- `.gitignore`: ignore raw HPA-374 sources and re-include only the four effect import sidecars.
- `scenes/ui/InventoryMenu.tscn:204-419`: replace emoji headings with icon-and-label rows and expose icon nodes.
- `scripts/ui/InventoryMenuController.cs:29-47,99-159,295-353,609-650`: initialize title art, empty/locked slot glyphs, and refreshed input hints.
- `tests/ui/InventoryMenuControllerTest.cs`: protect heading, empty-slot, locked-placeholder, populated-item, and input-hint behavior.
- `docs/ui/UI_SPRITES.md`: replace stale button/icon/effect plans with the shipped HPA-374 catalog.
- `docs/items/ASSET_STATUS.md:246-281`: retire stale root-level UI/effect entries and link canonical paths.

### Deliberately Unchanged

- `scripts/ui/BattleManager.cs` and `scenes/ui/BattleScene.tscn`: hidden legacy manual buttons remain present and unskinned; current stat/status rows and effect placement remain deferred because changing them requires screen-layout work.
- `scripts/ui/MainMenu.cs` and the two retained background PNGs: only tests verify their stable paths.
- `scripts/settings/SettingsManager.cs` and `project.godot` input actions: the presenter observes and resolves current events but never mutates persisted/domain bindings.

---

### Task 1: Build the Deterministic Art Pipeline and Source-Control Boundary

**Files:**
- Create: `requirements-dev.txt`
- Create: `art_source/.gdignore`
- Create: `tools/ui_art_spec.py`
- Create: `tools/ui_art_pipeline.py`
- Create: `tests/tools/test_ui_art_pipeline.py`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: ignored `art_source/ui/hpa-374/boards/<family>/<id>-source.png` and `<id>-alpha.png`.
- Produces: `register_family(family, source_root, map_path)`, `extract_family(family, map_path, project_root)`, `verify_family(family, project_root)`, and `build_contact_sheets(project_root)`.

- [ ] **Step 1: Add the failing Python inventory and pipeline tests**

Create `tests/tools/test_ui_art_pipeline.py` with these exact test cases:

```python
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
```

- [ ] **Step 2: Run the focused tests and observe the missing modules**

Run:

```bash
rtk uv run --with pytest==8.4.2 --with Pillow==12.0.0 python3 -m pytest tests/tools/test_ui_art_pipeline.py -q
```

Expected: collection fails because `tools.ui_art_pipeline` and `tools.ui_art_spec` do not exist.

- [ ] **Step 3: Implement the exact Python inventory**

Create `tools/ui_art_spec.py` with:

```python
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
    "celestial_anchor": (192, 192),
    "orbit_arc": (512, 256),
    "trajectory_line": (512, 64),
    "calibration_ticks": (256, 64),
    "callout_frame": (512, 256),
    "callout_connector": (256, 64),
    "catalogue_rail_endcap": (128, 256),
    "ignition_seal": (192, 192),
    "constellation_corner": (128, 128),
    "constellation_divider": (512, 64),
    "partial_sigil": (256, 256),
    "focus_halo": (96, 96),
    "selection_halo": (96, 96),
}

EFFECT_SIZES = {
    "encounter_burst": (256, 256),
    "hit_impact": (256, 256),
    "status_pulse": (256, 256),
    "reward_level_up": (256, 256),
}

ICON_FAMILIES = {
    "inventory-actions": ("inventory", "actions"),
    "stats-status": ("stats", "status"),
    "flow-semantic": ("flow", "interaction", "semantic"),
    "input-glyphs": ("input",),
}
```

- [ ] **Step 4: Implement alpha-safe export and failure behavior**

Create `tools/ui_art_pipeline.py` with these public types and the matching CLI:

```python
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


def runtime_path(
    record: dict, project_root: Path, target_width: int, target_height: int
) -> Path:
    asset_id = record["id"]
    match record["kind"]:
        case "icon":
            if target_width != target_height:
                raise ValueError(f"Icon target must be square: {asset_id}")
            return (
                project_root / "assets/sprites/ui/icons" / record["category"]
                / str(target_width) / f"{asset_id}.png"
            )
        case "ornament":
            return project_root / "assets/sprites/ui/ornaments" / f"{asset_id}.png"
        case "effect":
            return project_root / "assets/sprites/effects/ui" / f"{asset_id}.png"
        case _:
            raise ValueError(f"Unknown art kind: {record['kind']}")


def centered_aspect_crop(
    source_size: tuple[int, int], target_size: tuple[int, int]
) -> tuple[int, int, int, int]:
    source_width, source_height = source_size
    target_width, target_height = target_size
    if source_width * target_height > source_height * target_width:
        crop_height = source_height
        crop_width = source_height * target_width // target_height
    else:
        crop_width = source_width
        crop_height = source_width * target_height // target_width
    return (
        (source_width - crop_width) // 2,
        (source_height - crop_height) // 2,
        crop_width,
        crop_height,
    )


def export_record(record: dict, project_root: Path, allow_replace: bool = False) -> list[Path]:
    source = Path(record.get("alpha_source", record["source"]))
    expected_hash = record.get("alpha_sha256", record["source_sha256"])
    if sha256_file(source) != expected_hash:
        raise SourceHashMismatch(source)
    x, y, width, height = record["crop"]
    if width <= 0 or height <= 0:
        raise ValueError(f"Invalid crop for {record['id']}")
    image = Image.open(source).convert("RGBA").crop((x, y, x + width, y + height))
    outputs = [
        runtime_path(record, project_root, target_width, target_height)
        for target_width, target_height in record["target_sizes"]
    ]
    existing = next((path for path in outputs if path.exists()), None)
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
```

The CLI must expose these exact commands:

```text
register --family <inventory-actions|stats-status|flow-semantic|input-glyphs|ornaments|effects>
extract  --family <family>
verify   --family <family>
contact-sheets
manifest
```

`register` reads `*-source.png` and `*-alpha.png`, hashes both, computes alpha bounds, derives `crop` with `centered_aspect_crop` from the family's declared target aspect, verifies at least 2× target width and height inside that crop, and writes sorted actual records to `docs/ui/hpa-374/sources/extraction-map.json`. Every record contains `id`, `family`, `kind`, optional `category`, repo-relative `source` and `alpha_source`, both SHA-256 values and actual dimensions, `crop`, `target_sizes`, `generator: "OpenAI image_gen"`, the registration date in `generated_on`, and the exact chroma-removal arguments in `postprocess`. Registration preserves `generated_on` when the hashes are unchanged. `extract` refuses null crops, mismatched hashes, undersized sources, nonuniform scaling, or existing targets. It exports a whole family into a temporary sibling tree, verifies every staged derivative, and promotes the tree only after all records pass so a failed family never leaves a partial canonical set. `verify` checks the family-specific output set without requiring later families. `manifest` writes source/runtime rows from the completed map to `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md` instead of copying those records by hand.

Each record has these exact keys; `category` is present only for icons:

| Key | Stored value |
|---|---|
| `id`, `family`, `kind`, `category` | Values from `ui_art_spec.py` |
| `source`, `alpha_source` | Repo-relative selected and cleaned source paths |
| `source_sha256`, `alpha_sha256` | Lowercase SHA-256 computed from each file |
| `source_size`, `alpha_size` | Actual `[width, height]` read by Pillow |
| `crop` | Reviewed `[x, y, width, height]` in the alpha source |
| `target_sizes` | Exact dimensions from `ui_art_spec.py` |
| `generator` | Literal `OpenAI image_gen` |
| `generated_on` | ISO date captured by `date.today().isoformat()` on first registration of that hash |
| `postprocess` | `auto_key: border`, `soft_matte: true`, `transparent_threshold: 12`, `opaque_threshold: 220`, `despill: true`, and actual `edge_contract` of `0` or `1` |

`build_contact_sheets` renders registered-and-extracted records, so the early Inventory gate does not require later families; the final coverage test requires every cell before release. It writes the three true-size icon sheets plus `icon-states.png`, `ornaments.png`, and `effects.png`, replacing only these review derivatives on regeneration. `icon-states.png` shows every available icon in normal, focused, selected, and 45%-opacity disabled treatment; category labels sit outside the art. The ornament sheet includes a three-repeat calibration strip and callout-frame nine-patch guides.

- [ ] **Step 5: Add the repository boundaries and pinned test dependencies**

Create `requirements-dev.txt`:

```text
Pillow==12.0.0
pytest==8.4.2
```

Create an empty `art_source/.gdignore`. Append to `.gitignore`:

```gitignore
# HPA-374 generated-image masters are local provenance inputs, not runtime assets.
art_source/ui/hpa-374/boards/

# HPA-374 effects intentionally preserve per-file mipmap import settings.
!assets/sprites/effects/ui/*.png.import
```

- [ ] **Step 6: Make the pipeline tests pass**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 -m pytest tests/tools/test_ui_art_pipeline.py -q
rtk git diff --check
rtk git status --short --ignored
```

Expected: all pipeline tests pass; only `art_source/ui/hpa-374/boards/` is ignored; no runtime asset has been created.

- [ ] **Step 7: Record the unchanged C# baseline**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: the existing GdUnit4 suite passes before runtime catalog work begins.

- [ ] **Step 8: Commit the pipeline**

```bash
rtk git add .gitignore requirements-dev.txt art_source/.gdignore tools/ui_art_spec.py tools/ui_art_pipeline.py tests/tools/test_ui_art_pipeline.py
rtk git commit -m "chore: add deterministic UI art pipeline"
```

---

### Task 2: Add the Typed Runtime Art Catalog

**Files:**
- Create: `scripts/ui/art/UiArtCatalog.cs`
- Create: `tests/ui/art/UiArtCatalogTest.cs`

**Interfaces:**
- Produces: `UiIconId`, `UiIconSize`, `UiOrnamentId`, `UiEffectId`, `UiArtCatalog.GetIconPath`, `GetOrnamentPath`, `GetEffectPath`, `LoadIcon`, `LoadOrnament`, `LoadEffect`, `ForStatusEffect`, `ForItemCategory`, and `ForEquipmentSlot`.
- Consumes later: every UI presenter and integration task.

- [ ] **Step 1: Write failing path and enum-mapping tests**

Create `tests/ui/art/UiArtCatalogTest.cs` with:

```csharp
using GdUnit4;
using Godot;
using System;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UiArtCatalogTest : Node
{
    [TestCase]
    public void Catalog_ContainsExactReleaseInventory()
    {
        AssertThat(Enum.GetValues<UiIconId>().Length).IsEqual(62);
        AssertThat(Enum.GetValues<UiOrnamentId>().Length).IsEqual(13);
        AssertThat(Enum.GetValues<UiEffectId>().Length).IsEqual(4);
        AssertThat(Enum.GetValues<UiIconSize>().Select(value => (int)value).ToArray())
            .ContainsExactly(16, 24, 32);
    }

    [TestCase]
    public void Catalog_BuildsStableSnakeCasePaths()
    {
        AssertThat(UiArtCatalog.GetIconPath(UiIconId.Health, UiIconSize.Metadata))
            .IsEqual("res://assets/sprites/ui/icons/stats/16/health.png");
        AssertThat(UiArtCatalog.GetIconPath(UiIconId.ActiveSkill, UiIconSize.Default))
            .IsEqual("res://assets/sprites/ui/icons/inventory/24/active_skill.png");
        AssertThat(UiArtCatalog.GetIconPath(UiIconId.GamepadFaceBlank, UiIconSize.Feature))
            .IsEqual("res://assets/sprites/ui/icons/input/32/gamepad_face_blank.png");
        AssertThat(UiArtCatalog.GetOrnamentPath(UiOrnamentId.CalloutFrame))
            .IsEqual("res://assets/sprites/ui/ornaments/callout_frame.png");
        AssertThat(UiArtCatalog.GetEffectPath(UiEffectId.RewardLevelUp))
            .IsEqual("res://assets/sprites/effects/ui/reward_level_up.png");
    }

    [TestCase]
    public void Catalog_MapsRuntimeEnumsAndRejectsReservedStatusValue()
    {
        AssertThat(UiArtCatalog.ForStatusEffect(StatusEffectType.Poison)).IsEqual(UiIconId.Poison);
        AssertThat(UiArtCatalog.ForItemCategory(ItemCategory.Quest)).IsEqual(UiIconId.Quest);
        AssertThat(UiArtCatalog.ForEquipmentSlot(EquipmentSlotType.Shoe)).IsEqual(UiIconId.Shoe);
        AssertThat(UiArtCatalog.TryForStatusEffect((StatusEffectType)11, out _)).IsFalse();
    }
}
```

- [ ] **Step 2: Run the test and observe missing catalog types**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~UiArtCatalogTest"
```

Expected: build fails because `UiIconId`, `UiOrnamentId`, `UiEffectId`, and `UiArtCatalog` do not exist.

- [ ] **Step 3: Implement the exact typed IDs**

Create `scripts/ui/art/UiArtCatalog.cs` with these enums:

```csharp
public enum UiIconSize
{
    Metadata = 16,
    Default = 24,
    Feature = 32
}

public enum UiIconId
{
    Health, Mana, Experience, Level, Gold, Attack, Defense, Speed,
    Poison, Burn, Stun, Weaken, Slow, Blind, Regen, Haste, Strength, Fortify,
    General, Equipment, Consumable, Quest,
    Weapon, Shield, Armor, Helmet, Shoe, Accessory, ActiveSkill, Locked,
    Equip, Unequip, Use, Assign, Buy, Sell,
    Pause, Resume, Settings, Save, Load,
    Dialogue, Shop, Heal, Puzzle, Reward,
    Info, Warning, Error, Confirm, CancelClose,
    Keyboard, KeycapBlank, Mouse, MousePrimary, MouseSecondary, MouseWheel,
    Gamepad, GamepadFaceBlank, GamepadDpad, GamepadStick, GamepadShoulder
}

public enum UiOrnamentId
{
    CelestialAnchor, OrbitArc, TrajectoryLine, CalibrationTicks, CalloutFrame,
    CalloutConnector, CatalogueRailEndcap, IgnitionSeal, ConstellationCorner,
    ConstellationDivider, PartialSigil, FocusHalo, SelectionHalo
}

public enum UiEffectId
{
    EncounterBurst, HitImpact, StatusPulse, RewardLevelUp
}
```

- [ ] **Step 4: Implement path ownership, enum mappings, and guarded loading**

Use category switches containing every `UiIconId`, and one snake-case converter for filenames:

```csharp
public static string GetIconPath(UiIconId id, UiIconSize size)
{
    if (!Enum.IsDefined(id) || !Enum.IsDefined(size))
        throw new ArgumentOutOfRangeException();
    return $"res://assets/sprites/ui/icons/{CategoryFor(id)}/{(int)size}/{ToSnakeCase(id.ToString())}.png";
}

public static Texture2D? LoadIcon(UiIconId id, UiIconSize size)
{
    if (!Enum.IsDefined(id))
        id = UiIconId.Info;
    var texture = LoadOnce<Texture2D>(GetIconPath(id, size));
    return texture ?? (id == UiIconId.Info
        ? null
        : LoadOnce<Texture2D>(GetIconPath(UiIconId.Info, size)));
}

public static bool TryForStatusEffect(StatusEffectType type, out UiIconId id)
{
    id = type switch
    {
        StatusEffectType.Poison => UiIconId.Poison,
        StatusEffectType.Burn => UiIconId.Burn,
        StatusEffectType.Stun => UiIconId.Stun,
        StatusEffectType.Weaken => UiIconId.Weaken,
        StatusEffectType.Slow => UiIconId.Slow,
        StatusEffectType.Blind => UiIconId.Blind,
        StatusEffectType.Regen => UiIconId.Regen,
        StatusEffectType.Haste => UiIconId.Haste,
        StatusEffectType.Strength => UiIconId.Strength,
        StatusEffectType.Fortify => UiIconId.Fortify,
        _ => default
    };
    return type is StatusEffectType.Poison or StatusEffectType.Burn or
        StatusEffectType.Stun or StatusEffectType.Weaken or StatusEffectType.Slow or
        StatusEffectType.Blind or StatusEffectType.Regen or StatusEffectType.Haste or
        StatusEffectType.Strength or StatusEffectType.Fortify;
}

private static string CategoryFor(UiIconId id) => id switch
{
    UiIconId.Health or UiIconId.Mana or UiIconId.Experience or UiIconId.Level or
        UiIconId.Gold or UiIconId.Attack or UiIconId.Defense or UiIconId.Speed
        => "stats",
    UiIconId.Poison or UiIconId.Burn or UiIconId.Stun or UiIconId.Weaken or
        UiIconId.Slow or UiIconId.Blind or UiIconId.Regen or UiIconId.Haste or
        UiIconId.Strength or UiIconId.Fortify => "status",
    UiIconId.General or UiIconId.Equipment or UiIconId.Consumable or UiIconId.Quest or
        UiIconId.Weapon or UiIconId.Shield or UiIconId.Armor or UiIconId.Helmet or
        UiIconId.Shoe or UiIconId.Accessory or UiIconId.ActiveSkill or UiIconId.Locked
        => "inventory",
    UiIconId.Equip or UiIconId.Unequip or UiIconId.Use or UiIconId.Assign or
        UiIconId.Buy or UiIconId.Sell => "actions",
    UiIconId.Pause or UiIconId.Resume or UiIconId.Settings or UiIconId.Save or
        UiIconId.Load => "flow",
    UiIconId.Dialogue or UiIconId.Shop or UiIconId.Heal or UiIconId.Puzzle or
        UiIconId.Reward => "interaction",
    UiIconId.Info or UiIconId.Warning or UiIconId.Error or UiIconId.Confirm or
        UiIconId.CancelClose => "semantic",
    UiIconId.Keyboard or UiIconId.KeycapBlank or UiIconId.Mouse or
        UiIconId.MousePrimary or UiIconId.MouseSecondary or UiIconId.MouseWheel or
        UiIconId.Gamepad or UiIconId.GamepadFaceBlank or UiIconId.GamepadDpad or
        UiIconId.GamepadStick or UiIconId.GamepadShoulder => "input",
    _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
};

public static string GetOrnamentPath(UiOrnamentId id)
{
    if (!Enum.IsDefined(id))
        throw new ArgumentOutOfRangeException(nameof(id), id, null);
    return $"res://assets/sprites/ui/ornaments/{ToSnakeCase(id.ToString())}.png";
}

public static string GetEffectPath(UiEffectId id)
{
    if (!Enum.IsDefined(id))
        throw new ArgumentOutOfRangeException(nameof(id), id, null);
    return $"res://assets/sprites/effects/ui/{ToSnakeCase(id.ToString())}.png";
}

public static UiIconId ForStatusEffect(StatusEffectType type) =>
    TryForStatusEffect(type, out var id)
        ? id
        : throw new ArgumentOutOfRangeException(nameof(type), type, null);

public static UiIconId ForItemCategory(ItemCategory category) => category switch
{
    ItemCategory.General => UiIconId.General,
    ItemCategory.Equipment => UiIconId.Equipment,
    ItemCategory.Consumable => UiIconId.Consumable,
    ItemCategory.Quest => UiIconId.Quest,
    _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
};

public static UiIconId ForEquipmentSlot(EquipmentSlotType slot) => slot switch
{
    EquipmentSlotType.Weapon => UiIconId.Weapon,
    EquipmentSlotType.Shield => UiIconId.Shield,
    EquipmentSlotType.Armor => UiIconId.Armor,
    EquipmentSlotType.Helmet => UiIconId.Helmet,
    EquipmentSlotType.Shoe => UiIconId.Shoe,
    EquipmentSlotType.Accessory => UiIconId.Accessory,
    _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
};

public static Texture2D? LoadOrnament(UiOrnamentId id) =>
    LoadOnce<Texture2D>(GetOrnamentPath(id));

public static Texture2D? LoadEffect(UiEffectId id) =>
    LoadOnce<Texture2D>(GetEffectPath(id));

private static string ToSnakeCase(string value) =>
    Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();

private static readonly HashSet<string> MissingPaths = new();

private static T? LoadOnce<T>(string path) where T : Resource
{
    if (ResourceLoader.Exists(path))
        return ResourceLoader.Load<T>(path);
    if (MissingPaths.Add(path))
        GD.PushWarning($"[UiArtCatalog] Missing optional UI art resource: {path}");
    return null;
}
```

Add `using Godot;`, `using System;`, `using System.Collections.Generic;`, and `using System.Text.RegularExpressions;`. `LoadIcon` uses the same one-warning loader; when a valid icon is missing it makes one second attempt at the `Info` path unless the requested ID is already `Info`. Readable labels remain present even when both textures are unavailable.

- [ ] **Step 5: Run catalog tests and the build**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~UiArtCatalogTest"
rtk dotnet build Sirius.sln
rtk git diff --check
```

Expected: the three path/mapping tests pass; no resource-load test exists until assets are present.

- [ ] **Step 6: Commit the catalog contract**

```bash
rtk git add scripts/ui/art/UiArtCatalog.cs tests/ui/art/UiArtCatalogTest.cs
rtk git commit -m "feat: add typed UI art catalog"
```

---

### Task 3: Generate and Extract the Inventory and Action Family First

**Files:**
- Create: `docs/ui/hpa-374/sources/prompts/inventory-actions.md`
- Create: `docs/ui/hpa-374/sources/extraction-map.json`
- Create: `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`
- Create: `assets/sprites/ui/icons/inventory/<16|24|32>/*.png`
- Create: `assets/sprites/ui/icons/actions/<16|24|32>/*.png`
- Local only: `art_source/ui/hpa-374/boards/inventory-actions/*`

**Interfaces:**
- Produces: 18 logical assets and 54 true-size runtime PNGs used by Task 4.
- Consumes: Task 1 pipeline and HPA-373 battle-preparation reference.

- [ ] **Step 1: Write the exact family prompt**

Create `docs/ui/hpa-374/sources/prompts/inventory-actions.md` with this shared prompt:

```text
Use case: stylized-concept
Asset type: Sirius RPG UI icon master
Primary request: Generate exactly one isolated icon described below.
Input image: HPA-373 battle-preparation reference, used only for Sirius palette, celestial line language, and anime-fantasy rendering.
Scene/backdrop: perfectly flat solid #00FF00 chroma-key background for removal.
Style/medium: crisp mystical anime-fantasy UI glyph, celestial-navigation motif, simplified silhouette, controlled cel-shaded highlight, strong dark indigo outline.
Composition/framing: one centered subject on a square 1024x1024 canvas with at least 20% clear padding.
Color palette: #050714, #0D1530, #18234A, #27366C, #F7F5FF, #C7CEE8, #8F9AB8, #62DCFF, #F5D784, #DFAE43, #D96CC2, #68D6A3, #F1B85B, #F16D83.
Constraints: no text, no letters, no numbers, no watermark, no border frame, no cast shadow, no reflection, no extra object, no particles or outer glow, and do not use #00FF00 in the subject.
Avoid: photorealism, generic mobile emoji, glossy app-store icon tiles, micro-detail, chrome bevels, circuitry, and baked UI labels.
```

Append this exact per-asset matrix:

| ID | Subject |
|---|---|
| `general` | Compact celestial satchel with a four-point-star clasp |
| `equipment` | Symmetrical breastplate carrying a small orbit crest |
| `consumable` | Round potion vial with a star-shaped stopper |
| `quest` | Rolled parchment with a compass-star seal |
| `weapon` | Upright straight fantasy sword with one restrained cyan edge highlight |
| `shield` | Kite shield with a crescent-and-star boss |
| `armor` | Front-facing fantasy cuirass with a simple constellation notch |
| `helmet` | Closed fantasy helm with a narrow star-shaped visor slit |
| `shoe` | Armored boot with one compact comet-wing accent |
| `accessory` | Faceted celestial ring with a tiny orbit crossing its gemstone |
| `active_skill` | Radiant sigil orb encircled by one broken orbit |
| `locked` | Compact padlock with a four-point-star keyhole |
| `equip` | Bold inward arrow entering a circular equipment slot |
| `unequip` | Bold outward arrow leaving a circular equipment slot |
| `use` | Potion vial with one decisive activation spark |
| `assign` | Small active-skill sigil linked to an empty slot by one short orbit |
| `buy` | Gold celestial coin paired with one inward transaction arrow |
| `sell` | Gold celestial coin paired with one outward transaction arrow |

- [ ] **Step 2: Generate one source per logical asset**

For every row below, make one built-in `image_gen` call using the shared prompt plus that row's subject and:

```json
{
  "referenced_image_paths": [
    "/Users/chanwaichan/workspace/sirius/docs/ui/hpa-373/reference/battle-preparation-reference.png"
  ]
}
```

After inspecting each result, copy the selected output from the returned generated-image path to the listed ignored local path. Do not overwrite an existing source.

| Done | ID | Local source |
|---|---|---|
| [ ] | `general` | `art_source/ui/hpa-374/boards/inventory-actions/general-source.png` |
| [ ] | `equipment` | `art_source/ui/hpa-374/boards/inventory-actions/equipment-source.png` |
| [ ] | `consumable` | `art_source/ui/hpa-374/boards/inventory-actions/consumable-source.png` |
| [ ] | `quest` | `art_source/ui/hpa-374/boards/inventory-actions/quest-source.png` |
| [ ] | `weapon` | `art_source/ui/hpa-374/boards/inventory-actions/weapon-source.png` |
| [ ] | `shield` | `art_source/ui/hpa-374/boards/inventory-actions/shield-source.png` |
| [ ] | `armor` | `art_source/ui/hpa-374/boards/inventory-actions/armor-source.png` |
| [ ] | `helmet` | `art_source/ui/hpa-374/boards/inventory-actions/helmet-source.png` |
| [ ] | `shoe` | `art_source/ui/hpa-374/boards/inventory-actions/shoe-source.png` |
| [ ] | `accessory` | `art_source/ui/hpa-374/boards/inventory-actions/accessory-source.png` |
| [ ] | `active_skill` | `art_source/ui/hpa-374/boards/inventory-actions/active_skill-source.png` |
| [ ] | `locked` | `art_source/ui/hpa-374/boards/inventory-actions/locked-source.png` |
| [ ] | `equip` | `art_source/ui/hpa-374/boards/inventory-actions/equip-source.png` |
| [ ] | `unequip` | `art_source/ui/hpa-374/boards/inventory-actions/unequip-source.png` |
| [ ] | `use` | `art_source/ui/hpa-374/boards/inventory-actions/use-source.png` |
| [ ] | `assign` | `art_source/ui/hpa-374/boards/inventory-actions/assign-source.png` |
| [ ] | `buy` | `art_source/ui/hpa-374/boards/inventory-actions/buy-source.png` |
| [ ] | `sell` | `art_source/ui/hpa-374/boards/inventory-actions/sell-source.png` |

- [ ] **Step 3: Remove chroma key from every selected source**

Run the installed helper once for each exact ID:

```bash
for hpa374_id in general equipment consumable quest weapon shield armor helmet shoe accessory active_skill locked equip unequip use assign buy sell
do
  rtk python3.12 /Users/chanwaichan/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py --input "art_source/ui/hpa-374/boards/inventory-actions/${hpa374_id}-source.png" --out "art_source/ui/hpa-374/boards/inventory-actions/${hpa374_id}-alpha.png" --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
done
```

Inspect each alpha output; retry only the affected ID once with the command above plus `--edge-contract 1` when a thin green fringe remains.

- [ ] **Step 4: Register, extract, and verify the family**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py register --family inventory-actions
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py extract --family inventory-actions
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py verify --family inventory-actions
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
```

Expected: 54 new PNGs; every 16 px icon passes opaque-core and one-pixel inset checks; the extraction map contains real dimensions, hashes, and crop rectangles for all 18 sources.

- [ ] **Step 5: Generate and inspect the first family contact sheet**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

Inspect `docs/ui/hpa-374/contact-sheets/icons-16.png`, `icons-24.png`, and `icons-32.png` with the image viewer. Reject and regenerate ambiguous silhouettes, inconsistent outline weight, clipping, matte spill, or unreadable 16 px results before committing.

- [ ] **Step 6: Confirm raw sources remain ignored**

Run:

```bash
rtk git status --short --ignored
rtk git diff --check
```

Expected: runtime derivatives and committed source records are visible; every file under `art_source/ui/hpa-374/boards/` is ignored.

- [ ] **Step 7: Commit the first generated family**

```bash
rtk git add assets/sprites/ui/icons/inventory assets/sprites/ui/icons/actions docs/ui/hpa-374/sources docs/ui/hpa-374/contact-sheets
rtk git commit -m "feat: add inventory UI icon family"
```

---

### Task 4: Integrate Inventory Headings, Empty Slots, and Locks

**Files:**
- Create: `scripts/ui/art/UiIconPresenter.cs`
- Modify: `scenes/ui/InventoryMenu.tscn:204-419`
- Modify: `scripts/ui/InventoryMenuController.cs:29-47,99-159,295-353,609-650`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: `UiArtCatalog` and the 18 inventory/action assets.
- Produces: `UiIconPresenter.Apply(TextureRect, ...)`, `Apply(TextureButton, ...)`, and `Apply(Button, ...)`; current inventory icon-and-label rows and slot fallback rules.

- [ ] **Step 1: Add failing current-scene integration tests**

Append these tests to `InventoryMenuControllerTest`:

```csharp
[TestCase]
public void InventoryHeadings_UseReadableLabelsAndGeneratedIcons()
{
    var equipmentLabel = _inventoryMenu.GetNode<Label>("%EquipmentTitleLabel");
    var itemsLabel = _inventoryMenu.GetNode<Label>("%InventoryTitleLabel");
    var equipmentIcon = _inventoryMenu.GetNode<TextureRect>("%EquipmentTitleIcon");
    var itemsIcon = _inventoryMenu.GetNode<TextureRect>("%InventoryTitleIcon");

    AssertThat(equipmentLabel.Text).IsEqual("Equipment");
    AssertThat(itemsLabel.Text).IsEqual("Items");
    AssertThat(equipmentIcon.Texture.ResourcePath)
        .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Equipment, UiIconSize.Default));
    AssertThat(itemsIcon.Texture.ResourcePath)
        .IsEqual(UiArtCatalog.GetIconPath(UiIconId.General, UiIconSize.Default));
}

[TestCase]
public void EmptyEquipmentAndAccessorySlots_ShowTypeGlyphs()
{
    var weapon = _inventoryMenu.GetNode<PanelContainer>("%WeaponSlot")
        .GetNode<TextureButton>("Button");
    var accessory = _inventoryMenu.GetNode<PanelContainer>("%AccessorySlot0")
        .GetNode<TextureButton>("Button");

    AssertThat(weapon.TextureNormal.ResourcePath)
        .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
    AssertThat(accessory.TextureNormal.ResourcePath)
        .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
}

[TestCase]
public void InactiveAccessoryPlaceholders_ShowLockWithoutUnlockRule()
{
    for (var index = EquipmentSet.AccessorySlotCount; index < 6; index++)
    {
        var button = _inventoryMenu.GetNode<PanelContainer>($"%AccessorySlot{index}")
            .GetNode<TextureButton>("Button");
        AssertThat(button.Disabled).IsTrue();
        AssertThat(button.TextureDisabled.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));
        AssertThat(button.TooltipText).IsEqual("Accessory Slot Locked");
    }
}
```

Add the populated-slot regression:

```csharp
[TestCase]
public void PopulatedEquipmentSlot_ItemArtOverridesTypeGlyph()
{
    var sword = EquipmentCatalog.CreateWoodenSword();
    AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();

    _inventoryMenu.OpenMenu();

    var weapon = _inventoryMenu.GetNode<PanelContainer>("%WeaponSlot")
        .GetNode<TextureButton>("Button");
    AssertThat(weapon.TextureNormal.ResourcePath).IsEqual(sword.AssetPath);
    AssertThat(weapon.TextureNormal.ResourcePath)
        .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
}
```

- [ ] **Step 2: Run the tests and observe missing nodes/presenter behavior**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: the new tests fail because the title icon nodes do not exist and empty slots still clear textures.

- [ ] **Step 3: Replace emoji labels with icon-and-label rows**

In `InventoryMenu.tscn`, replace each standalone title `Label` with:

```text
EquipmentTitleRow (HBoxContainer, separation 8)
  EquipmentTitleIcon (TextureRect, unique, 24x24, KeepAspectCentered)
  EquipmentTitleLabel (Label, unique, text "Equipment", font size 22)

InventoryTitleRow (HBoxContainer, separation 8)
  InventoryTitleIcon (TextureRect, unique, 24x24, KeepAspectCentered)
  InventoryTitleLabel (Label, unique, text "Items", font size 22)
```

Change `CloseButton.text` from `Close [I]` to `Close`; Task 7 will make its binding suffix dynamic without changing the button action.

- [ ] **Step 4: Implement the narrow icon presenter**

Create `UiIconPresenter.cs`:

```csharp
public static class UiIconPresenter
{
    public static bool Apply(TextureRect target, UiIconId id, UiIconSize size)
    {
        var texture = UiArtCatalog.LoadIcon(id, size);
        target.Texture = texture;
        target.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        target.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        return texture != null;
    }

    public static bool Apply(TextureButton target, UiIconId id, UiIconSize size)
    {
        var texture = UiArtCatalog.LoadIcon(id, size);
        target.TextureNormal = texture;
        target.TextureHover = texture;
        target.TexturePressed = texture;
        target.TextureDisabled = texture;
        target.TextureFocused = texture;
        target.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        target.IgnoreTextureSize = true;
        return texture != null;
    }

    public static bool Apply(Button target, UiIconId id, UiIconSize size)
    {
        target.Icon = UiArtCatalog.LoadIcon(id, size);
        target.ExpandIcon = false;
        target.AddThemeConstantOverride("icon_max_width", (int)size);
        return target.Icon != null;
    }
}
```

- [ ] **Step 5: Apply heading and slot glyph rules**

In `_Ready`, apply `Equipment` and `General` to the two title `TextureRect`s. In `RefreshEquipmentSlots`, replace `ClearButtonIcon` for empty slots with `UiIconPresenter.Apply(slot.Button, UiArtCatalog.ForEquipmentSlot(slotType), UiIconSize.Feature)`. In `RefreshAccessorySlots`, apply `Locked` to inactive placeholders and `Accessory` to active empty slots. Preserve `SetButtonIcon` for populated item slots exactly as the higher-priority branch.

- [ ] **Step 6: Run focused tests and inspect the scene**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~UiArtCatalogTest"
rtk dotnet build Sirius.sln
rtk git diff --check
```

Expected: labels contain no emoji; title icons are 24 px assets; empty/locked glyphs are 32 px assets; populated item art still wins.

- [ ] **Step 7: Commit current-screen integration**

```bash
rtk git add scripts/ui/art/UiIconPresenter.cs scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
rtk git commit -m "feat: integrate inventory UI artwork"
```

---

### Task 5: Generate and Validate Core Stat and Status Icons

**Files:**
- Create: `docs/ui/hpa-374/sources/prompts/stats-status.md`
- Modify: `docs/ui/hpa-374/sources/extraction-map.json`
- Modify: `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`
- Create: `assets/sprites/ui/icons/stats/<16|24|32>/*.png`
- Create: `assets/sprites/ui/icons/status/<16|24|32>/*.png`
- Local only: `art_source/ui/hpa-374/boards/stats-status/*`

**Interfaces:**
- Produces: 18 logical assets and 54 true-size runtime PNGs.
- Deliberately does not restructure the current `BattleScene` stat/status labels; those assets become typed, validated HPA-373 migration inputs.

- [ ] **Step 1: Define the exact family prompt and subjects**

Create `docs/ui/hpa-374/sources/prompts/stats-status.md`. Begin with the canonical image-generation prompt in Global Constraints, then add:

| Done | ID | Subject |
|---|---|---|
| [ ] | `health` | Compact heart-shaped crystal with one cyan pulse notch |
| [ ] | `mana` | Upright indigo teardrop containing a four-point cyan star |
| [ ] | `experience` | Small ascending constellation of three connected stars |
| [ ] | `level` | Single upward chevron crossing a restrained orbit |
| [ ] | `gold` | Faceted gold celestial coin with a compass-star imprint |
| [ ] | `attack` | Crossed short sword and comet slash |
| [ ] | `defense` | Compact shield under one protective orbit arc |
| [ ] | `speed` | Forward comet with two clean motion fins |
| [ ] | `poison` | Violet venom droplet with one sharp inner bubble |
| [ ] | `burn` | Amber-and-rose three-lobed flame |
| [ ] | `stun` | Broken orbit circling a four-point impact star |
| [ ] | `weaken` | Downward fractured sword silhouette |
| [ ] | `slow` | Crescent clock with one trailing weight arc |
| [ ] | `blind` | Closed eye crossed by a dark celestial veil |
| [ ] | `regen` | Green sprout emerging from a circular healing pulse |
| [ ] | `haste` | Cyan double-comet arrow |
| [ ] | `strength` | Gold clenched gauntlet with one compact star flare |
| [ ] | `fortify` | Layered green-and-gold shield with a reinforced center |

For debuffs, use violet/amber/rose semantic accents without relying on colour alone. For buffs, use green/cyan/gold accents and distinct silhouettes.

- [ ] **Step 2: Generate one isolated source per ID**

Make exactly 18 built-in `image_gen` calls, one for each table row. Every call uses the shared prompt, the row subject, and:

```json
{
  "referenced_image_paths": [
    "/Users/chanwaichan/workspace/sirius/docs/ui/hpa-373/reference/battle-preparation-reference.png"
  ]
}
```

Inspect each returned image, then copy it to:

```text
art_source/ui/hpa-374/boards/stats-status/<id>-source.png
```

The exact IDs are `health`, `mana`, `experience`, `level`, `gold`, `attack`, `defense`, `speed`, `poison`, `burn`, `stun`, `weaken`, `slow`, `blind`, `regen`, `haste`, `strength`, and `fortify`. Do not batch multiple subjects into one generated image and do not overwrite a selected source.

- [ ] **Step 3: Remove chroma key and inspect alpha edges**

Run:

```bash
for hpa374_id in health mana experience level gold attack defense speed poison burn stun weaken slow blind regen haste strength fortify
do
  rtk python3.12 /Users/chanwaichan/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py --input "art_source/ui/hpa-374/boards/stats-status/${hpa374_id}-source.png" --out "art_source/ui/hpa-374/boards/stats-status/${hpa374_id}-alpha.png" --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
done
```

Inspect all 18 alpha outputs. A thin green fringe gets one retry with `--edge-contract 1`; a still-contaminated result is regenerated.

- [ ] **Step 4: Register, extract, and verify**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py register --family stats-status
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py extract --family stats-status
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py verify --family stats-status
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

Expected: 54 new PNGs, exact 16/24/32 dimensions, ten status IDs matching runtime enum names, and no source or derivative for reserved `StatusEffectType` value 11.

- [ ] **Step 5: Review minimum-size and semantic distinction**

Open the three icon contact sheets. At 16 px, require the health/mana, attack/strength, defense/fortify, burn/haste, and poison/regen pairs to remain distinguishable in silhouette without their labels. Reject clipping, reliance on hue alone, or opaque cores below the pipeline threshold.

- [ ] **Step 6: Commit the stat/status family**

```bash
rtk git add assets/sprites/ui/icons/stats assets/sprites/ui/icons/status docs/ui/hpa-374/sources docs/ui/hpa-374/contact-sheets
rtk git commit -m "feat: add stat and status UI icons"
```

---

### Task 6: Generate Flow, Interaction, and Semantic Icons

**Files:**
- Create: `docs/ui/hpa-374/sources/prompts/flow-semantic.md`
- Modify: `docs/ui/hpa-374/sources/extraction-map.json`
- Modify: `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`
- Create: `assets/sprites/ui/icons/flow/<16|24|32>/*.png`
- Create: `assets/sprites/ui/icons/interaction/<16|24|32>/*.png`
- Create: `assets/sprites/ui/icons/semantic/<16|24|32>/*.png`
- Local only: `art_source/ui/hpa-374/boards/flow-semantic/*`

**Interfaces:**
- Produces: 15 logical assets and 45 true-size runtime PNGs.
- Semantic symbols remain paired with readable text; this task does not rewrite modal headers.

- [ ] **Step 1: Define the exact family prompt and subjects**

Create `docs/ui/hpa-374/sources/prompts/flow-semantic.md` from the canonical image-generation prompt in Global Constraints and add:

| Done | ID | Subject |
|---|---|---|
| [ ] | `pause` | Two upright celestial bars joined by one subtle orbit |
| [ ] | `resume` | Right-pointing triangular star-sail |
| [ ] | `settings` | Six-tooth indigo-and-cyan astrolabe gear |
| [ ] | `save` | Compact archive crystal receiving a downward light ray |
| [ ] | `load` | Compact archive crystal releasing an upward light ray |
| [ ] | `dialogue` | Two overlapping speech crescents with one star point |
| [ ] | `shop` | Small market canopy over a gold celestial coin |
| [ ] | `heal` | Open hand supporting a green pulse star |
| [ ] | `puzzle` | Interlocking celestial maze pieces with a central compass point |
| [ ] | `reward` | Open treasure crest releasing one gold star |
| [ ] | `info` | Round information medallion with a simple lowercase information stem |
| [ ] | `warning` | Amber triangular beacon with an explicit central exclamation mark |
| [ ] | `error` | Rose octagonal seal with a bold central cross |
| [ ] | `confirm` | Green circular seal with a bold check mark |
| [ ] | `cancel_close` | Indigo circular seal with a bold diagonal cross |

The standardized information, exclamation, check, and cross markings describe semantic meaning and are the only glyph-like markings in this family. They remain paired with UI text.

- [ ] **Step 2: Generate and select one source per ID**

Make exactly 15 built-in `image_gen` calls. Every call references `/Users/chanwaichan/workspace/sirius/docs/ui/hpa-373/reference/battle-preparation-reference.png`. Copy inspected results to:

```text
art_source/ui/hpa-374/boards/flow-semantic/<id>-source.png
```

Generate the exact IDs `pause`, `resume`, `settings`, `save`, `load`, `dialogue`, `shop`, `heal`, `puzzle`, `reward`, `info`, `warning`, `error`, `confirm`, and `cancel_close`.

- [ ] **Step 3: Remove chroma key, register, extract, and verify**

Run the chroma-removal helper:

```bash
for hpa374_id in pause resume settings save load dialogue shop heal puzzle reward info warning error confirm cancel_close
do
  rtk python3.12 /Users/chanwaichan/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py --input "art_source/ui/hpa-374/boards/flow-semantic/${hpa374_id}-source.png" --out "art_source/ui/hpa-374/boards/flow-semantic/${hpa374_id}-alpha.png" --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
done
```

Inspect each output, then run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py register --family flow-semantic
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py extract --family flow-semantic
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py verify --family flow-semantic
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

Expected: 45 new PNGs and updated source records for all 15 logical assets.

- [ ] **Step 4: Review text pairing and destructive-state clarity**

Inspect all three icon sheets on the approved dark surface. Confirm that `warning`, `error`, `confirm`, and `cancel_close` are distinct without colour, contain no action-specific words, and retain room for adjacent readable labels. Do not add them to legacy modal layouts in this task.

- [ ] **Step 5: Commit the family**

```bash
rtk git add assets/sprites/ui/icons/flow assets/sprites/ui/icons/interaction assets/sprites/ui/icons/semantic docs/ui/hpa-374/sources docs/ui/hpa-374/contact-sheets
rtk git commit -m "feat: add flow and semantic UI icons"
```

---

### Task 7: Generate Input Glyphs and Add Binding-Aware Compact Hints

**Files:**
- Create: `docs/ui/hpa-374/sources/prompts/input-glyphs.md`
- Modify: `docs/ui/hpa-374/sources/extraction-map.json`
- Modify: `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`
- Create: `assets/sprites/ui/icons/input/<16|24|32>/*.png`
- Create: `scripts/ui/art/InputHintPresenter.cs`
- Create: `tests/ui/art/InputHintPresenterTest.cs`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Local only: `art_source/ui/hpa-374/boards/input-glyphs/*`

**Interfaces:**
- Produces: 11 logical assets, 33 runtime PNGs, `UiInputDevice`, `UiInputHint`, and a binding-aware `InputHintPresenter`.
- Consumes current `InputMap` events without mutating actions, saved settings, or HPA-376 lifecycle behavior.

- [ ] **Step 1: Define and generate the exact input family**

Create `docs/ui/hpa-374/sources/prompts/input-glyphs.md` from the canonical image-generation prompt in Global Constraints with:

| Done | ID | Subject |
|---|---|---|
| [ ] | `keyboard` | Simplified front-facing keyboard silhouette with three key rows |
| [ ] | `keycap_blank` | Empty rounded-square keycap frame with a dark transparent-safe center |
| [ ] | `mouse` | Front-facing two-button mouse silhouette |
| [ ] | `mouse_primary` | Mouse silhouette with left button highlighted by shape separation |
| [ ] | `mouse_secondary` | Mouse silhouette with right button highlighted by shape separation |
| [ ] | `mouse_wheel` | Mouse silhouette with central wheel highlighted |
| [ ] | `gamepad` | Symmetrical generic gamepad silhouette with no platform branding |
| [ ] | `gamepad_face_blank` | Empty circular face-button frame with a dark transparent-safe center |
| [ ] | `gamepad_dpad` | Four-direction D-pad cross |
| [ ] | `gamepad_stick` | Analog-stick cap inside a directional orbit |
| [ ] | `gamepad_shoulder` | Generic shoulder-button silhouette viewed from above |

Make exactly 11 built-in `image_gen` calls, reference the HPA-373 image in each, inspect each result, and copy it to:

```text
art_source/ui/hpa-374/boards/input-glyphs/<id>-source.png
```

Do not bake keyboard letters, controller brands, controller-layout colours, or game actions into the generated artwork.

- [ ] **Step 2: Remove chroma key and export all input derivatives**

Run:

```bash
for hpa374_id in keyboard keycap_blank mouse mouse_primary mouse_secondary mouse_wheel gamepad gamepad_face_blank gamepad_dpad gamepad_stick gamepad_shoulder
do
  rtk python3.12 /Users/chanwaichan/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py --input "art_source/ui/hpa-374/boards/input-glyphs/${hpa374_id}-source.png" --out "art_source/ui/hpa-374/boards/input-glyphs/${hpa374_id}-alpha.png" --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
done
```

Inspect every `<id>-alpha.png`, then run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py register --family input-glyphs
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py extract --family input-glyphs
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py verify --family input-glyphs
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

Expected: 33 new PNGs. Blank keycap and face-button centers remain visually suitable for an adjacent localized label.

- [ ] **Step 3: Write failing presenter tests with exact InputMap restoration**

Create `tests/ui/art/InputHintPresenterTest.cs` with a temporary action named `hpa374_test_hint`. In `[BeforeTest]`, remember whether the action existed, duplicate every prior event, create the action when absent, and erase its events. In `[AfterTest]`, erase the test events, restore every duplicate, and erase the action only if the test created it.

```csharp
private static readonly StringName TestAction = "hpa374_test_hint";
private readonly List<InputEvent> _originalEvents = new();
private bool _actionExisted;

[BeforeTest]
public void SetupAction()
{
    _actionExisted = InputMap.HasAction(TestAction);
    if (!_actionExisted)
        InputMap.AddAction(TestAction, 0.5f);
    foreach (var inputEvent in InputMap.ActionGetEvents(TestAction))
        _originalEvents.Add((InputEvent)inputEvent.Duplicate());
    InputMap.ActionEraseEvents(TestAction);
}

[AfterTest]
public void RestoreAction()
{
    InputMap.ActionEraseEvents(TestAction);
    if (_actionExisted)
    {
        foreach (var inputEvent in _originalEvents)
            InputMap.ActionAddEvent(TestAction, inputEvent);
    }
    else
    {
        InputMap.EraseAction(TestAction);
    }
    _originalEvents.Clear();
}
```

Add these tests:

```csharp
[TestCase]
public void Resolve_ReReadsKeyboardBindingOnEveryCall()
{
    InputMap.ActionAddEvent(TestAction, new InputEventKey
    {
        PhysicalKeycode = Key.I
    });
    var presenter = new InputHintPresenter(UiInputDevice.Keyboard);

    AssertThat(presenter.Resolve(TestAction).BindingLabel).IsEqual("I");

    InputMap.ActionEraseEvents(TestAction);
    InputMap.ActionAddEvent(TestAction, new InputEventKey
    {
        PhysicalKeycode = Key.K
    });
    AssertThat(presenter.Resolve(TestAction).BindingLabel).IsEqual("K");
}

[TestCase]
public void Observe_SwitchesBetweenMouseJoypadButtonAndJoypadAxis()
{
    var presenter = new InputHintPresenter(UiInputDevice.Keyboard);

    presenter.Observe(new InputEventMouseButton
    {
        ButtonIndex = MouseButton.Left,
        Pressed = true
    });
    AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Mouse);

    presenter.Observe(new InputEventJoypadButton
    {
        ButtonIndex = JoyButton.A,
        Pressed = true
    });
    AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Gamepad);

    presenter.Observe(new InputEventJoypadMotion
    {
        Axis = JoyAxis.LeftX,
        AxisValue = 0.75f
    });
    AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Gamepad);
}

[TestCase]
public void Resolve_UnboundActionReturnsReadableFallback()
{
    var hint = new InputHintPresenter(UiInputDevice.Keyboard).Resolve(TestAction);
    AssertThat(hint.IsRepresentable).IsFalse();
    AssertThat(hint.BindingLabel).IsEqual("Unbound");
    AssertThat(hint.IconId).IsEqual(UiIconId.Info);
}
```

Add:

```csharp
[TestCase]
public void Resolve_MapsMousePrimaryComponent()
{
    InputMap.ActionAddEvent(TestAction, new InputEventMouseButton
    {
        ButtonIndex = MouseButton.Left
    });
    var hint = new InputHintPresenter(UiInputDevice.Mouse).Resolve(TestAction);
    AssertThat(hint.IconId).IsEqual(UiIconId.MousePrimary);
    AssertThat(hint.BindingLabel).IsEqual("Mouse 1");
}

[TestCase]
public void Resolve_MapsFaceButtonAndStickAxisComponents()
{
    InputMap.ActionAddEvent(TestAction, new InputEventJoypadButton
    {
        ButtonIndex = JoyButton.A
    });
    var presenter = new InputHintPresenter(UiInputDevice.Gamepad);
    var face = presenter.Resolve(TestAction);
    AssertThat(face.IconId).IsEqual(UiIconId.GamepadFaceBlank);
    AssertThat(face.BindingLabel).IsEqual("A");

    InputMap.ActionEraseEvents(TestAction);
    InputMap.ActionAddEvent(TestAction, new InputEventJoypadMotion
    {
        Axis = JoyAxis.LeftX,
        AxisValue = 1.0f
    });
    var stick = presenter.Resolve(TestAction);
    AssertThat(stick.IconId).IsEqual(UiIconId.GamepadStick);
    AssertThat(stick.BindingLabel).IsEqual("Left Stick Right");
}

[TestCase]
public void Observe_IgnoresJoypadMotionBelowDeadzone()
{
    var presenter = new InputHintPresenter(UiInputDevice.Keyboard);
    var changed = presenter.Observe(new InputEventJoypadMotion
    {
        Axis = JoyAxis.LeftX,
        AxisValue = 0.49f
    });
    AssertThat(changed).IsFalse();
    AssertThat(presenter.ActiveDevice).IsEqual(UiInputDevice.Keyboard);
}
```

- [ ] **Step 4: Run the presenter tests and observe the missing service**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InputHintPresenterTest"
```

Expected: build fails because `InputHintPresenter`, `UiInputDevice`, and `UiInputHint` do not exist.

- [ ] **Step 5: Implement the narrow presenter contract**

Create `scripts/ui/art/InputHintPresenter.cs` with:

```csharp
using Godot;
using System;
using System.Linq;

public enum UiInputDevice
{
    Keyboard,
    Mouse,
    Gamepad
}

public readonly record struct UiInputHint(
    UiInputDevice Device,
    UiIconId IconId,
    string BindingLabel,
    bool IsRepresentable);

public sealed class InputHintPresenter
{
    public UiInputDevice ActiveDevice { get; private set; }

    public InputHintPresenter(UiInputDevice initialDevice = UiInputDevice.Keyboard)
    {
        ActiveDevice = initialDevice;
    }

    public bool Observe(InputEvent inputEvent)
    {
        var next = inputEvent switch
        {
            InputEventKey key when key.Pressed && !key.Echo => UiInputDevice.Keyboard,
            InputEventMouseButton mouse when mouse.Pressed => UiInputDevice.Mouse,
            InputEventJoypadButton button when button.Pressed => UiInputDevice.Gamepad,
            InputEventJoypadMotion motion when Math.Abs(motion.AxisValue) >= 0.5f
                => UiInputDevice.Gamepad,
            _ => ActiveDevice
        };
        var changed = next != ActiveDevice;
        ActiveDevice = next;
        return changed;
    }

    public UiInputHint Resolve(StringName action)
    {
        if (!InputMap.HasAction(action))
            return UnboundHint(ActiveDevice);
        var events = InputMap.ActionGetEvents(action);
        var currentDeviceEvent = events.FirstOrDefault(MatchesActiveDevice);
        var selected = currentDeviceEvent ?? events.FirstOrDefault();
        return selected == null ? UnboundHint(ActiveDevice) : HintFor(selected);
    }

    public void ApplyCompactButton(Button button, string baseText, StringName action)
    {
        var hint = Resolve(action);
        UiIconPresenter.Apply(button, hint.IconId, UiIconSize.Metadata);
        button.Text = $"{baseText} [{hint.BindingLabel}]";
        button.TooltipText = $"{baseText}: {hint.BindingLabel}";
    }

    private bool MatchesActiveDevice(InputEvent inputEvent) => ActiveDevice switch
    {
        UiInputDevice.Keyboard => inputEvent is InputEventKey,
        UiInputDevice.Mouse => inputEvent is InputEventMouseButton,
        UiInputDevice.Gamepad => inputEvent is InputEventJoypadButton
            or InputEventJoypadMotion,
        _ => false
    };

    private static UiInputHint HintFor(InputEvent inputEvent) => inputEvent switch
    {
        InputEventKey key => new(
            UiInputDevice.Keyboard,
            UiIconId.KeycapBlank,
            OS.GetKeycodeString(
                key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode),
            true),
        InputEventMouseButton mouse => MouseHint(mouse.ButtonIndex),
        InputEventJoypadButton button => JoyButtonHint(button.ButtonIndex),
        InputEventJoypadMotion motion => JoyAxisHint(motion.Axis, motion.AxisValue),
        _ => UnboundHint(UiInputDevice.Keyboard)
    };

    private static UiInputHint MouseHint(MouseButton button) => button switch
    {
        MouseButton.Left => new(UiInputDevice.Mouse, UiIconId.MousePrimary, "Mouse 1", true),
        MouseButton.Right => new(UiInputDevice.Mouse, UiIconId.MouseSecondary, "Mouse 2", true),
        MouseButton.Middle => new(UiInputDevice.Mouse, UiIconId.MouseWheel, "Mouse Wheel", true),
        MouseButton.WheelUp => new(UiInputDevice.Mouse, UiIconId.MouseWheel, "Wheel Up", true),
        MouseButton.WheelDown => new(UiInputDevice.Mouse, UiIconId.MouseWheel, "Wheel Down", true),
        _ => new(UiInputDevice.Mouse, UiIconId.Mouse, button.ToString(), true)
    };

    private static UiInputHint JoyButtonHint(JoyButton button) => button switch
    {
        JoyButton.A or JoyButton.B or JoyButton.X or JoyButton.Y
            => new(UiInputDevice.Gamepad, UiIconId.GamepadFaceBlank,
                button.ToString().ToUpperInvariant(), true),
        JoyButton.DpadUp => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Up", true),
        JoyButton.DpadDown => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Down", true),
        JoyButton.DpadLeft => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Left", true),
        JoyButton.DpadRight => new(UiInputDevice.Gamepad, UiIconId.GamepadDpad, "D-pad Right", true),
        JoyButton.LeftShoulder => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Left Shoulder", true),
        JoyButton.RightShoulder => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Right Shoulder", true),
        JoyButton.LeftStick => new(UiInputDevice.Gamepad, UiIconId.GamepadStick, "Left Stick", true),
        JoyButton.RightStick => new(UiInputDevice.Gamepad, UiIconId.GamepadStick, "Right Stick", true),
        _ => new(UiInputDevice.Gamepad, UiIconId.Gamepad, button.ToString(), true)
    };

    private static UiInputHint JoyAxisHint(JoyAxis axis, float value) => axis switch
    {
        JoyAxis.LeftX => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Left Stick Right" : "Left Stick Left", true),
        JoyAxis.LeftY => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Left Stick Down" : "Left Stick Up", true),
        JoyAxis.RightX => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Right Stick Right" : "Right Stick Left", true),
        JoyAxis.RightY => new(UiInputDevice.Gamepad, UiIconId.GamepadStick,
            value >= 0 ? "Right Stick Down" : "Right Stick Up", true),
        JoyAxis.TriggerLeft => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Left Trigger", true),
        JoyAxis.TriggerRight => new(UiInputDevice.Gamepad, UiIconId.GamepadShoulder, "Right Trigger", true),
        _ => new(UiInputDevice.Gamepad, UiIconId.Gamepad, axis.ToString(), true)
    };

    private static UiInputHint UnboundHint(UiInputDevice device) =>
        new(device, UiIconId.Info, "Unbound", false);
}
```

The service never caches the array returned by `InputMap.ActionGetEvents`.

- [ ] **Step 6: Wire the current Inventory close hint without changing bindings**

In `InventoryMenuController`, create one `InputHintPresenter`, cache `%CloseButton`, and add:

```csharp
private static readonly StringName ToggleInventoryAction = "toggle_inventory";

private void RefreshCloseHint()
{
    _inputHintPresenter.ApplyCompactButton(_closeButton, "Close", ToggleInventoryAction);
}
```

Call `RefreshCloseHint()` from `_Ready` and `OpenMenu`. At the beginning of `_Input`, call `Observe`; if it returns `true` while the menu is visible, refresh the close hint before existing close handling. Do not add, erase, or remap any action.

Add `using System.Linq;` to `InventoryMenuControllerTest.cs`. Append Inventory tests proving that `OpenMenu` displays `Close [I]` from the current default mapping and that reopening after a temporary mapping change displays the new label. Snapshot and restore `toggle_inventory`:

```csharp
[TestCase]
public void OpenMenu_UsesCurrentToggleInventoryBindingInCloseLabel()
{
    _inventoryMenu.OpenMenu();
    AssertThat(_inventoryMenu.GetNode<Button>("%CloseButton").Text).IsEqual("Close [I]");
}

[TestCase]
public void ReopenMenu_ReReadsChangedToggleInventoryBinding()
{
    var original = InputMap.ActionGetEvents("toggle_inventory")
        .Select(inputEvent => (InputEvent)inputEvent.Duplicate())
        .ToArray();
    try
    {
        InputMap.ActionEraseEvents("toggle_inventory");
        InputMap.ActionAddEvent("toggle_inventory", new InputEventKey
        {
            PhysicalKeycode = Key.K
        });
        _inventoryMenu.OpenMenu();
        AssertThat(_inventoryMenu.GetNode<Button>("%CloseButton").Text).IsEqual("Close [K]");
    }
    finally
    {
        if (_inventoryMenu.Visible)
            _inventoryMenu.CloseMenu();
        InputMap.ActionEraseEvents("toggle_inventory");
        foreach (var inputEvent in original)
            InputMap.ActionAddEvent("toggle_inventory", inputEvent);
    }
}
```

- [ ] **Step 7: Run focused behavior tests and commit**

Run:

```bash
rtk dotnet test Sirius.sln --settings task2.local.runsettings --no-build --filter "Name~Resolve_ReReadsKeyboardBindingOnEveryCall|Name~Observe_SwitchesBetweenMouseJoypadButtonAndJoypadAxis|Name~Resolve_UnboundActionReturnsReadableFallback|Name~Resolve_MapsMousePrimaryComponent|Name~Resolve_MapsFaceButtonAndStickAxisComponents|Name~Observe_IgnoresJoypadMotionBelowDeadzone|Name~OpenMenu_UsesCurrentToggleInventoryBindingInCloseLabel|Name~ReopenMenu_ReReadsChangedToggleInventoryBinding"
rtk uv run --with-requirements requirements-dev.txt python3 -m pytest tests/tools/test_ui_art_pipeline.py tests/tools/test_ui_asset_coverage.py -q
rtk dotnet build Sirius.sln
rtk git diff --check
```

Expected: exactly eight named real-Godot tests pass (the six presenter cases and two compact-label Inventory cases), then Python coverage remains green even if Godot has generated ignored local icon `.png.import` caches. Coverage rejects tracked icon sidecars; effects remain the only permitted tracked sidecar family.

Commit:

```bash
rtk git add assets/sprites/ui/icons/input scripts/ui/art/InputHintPresenter.cs scripts/ui/InventoryMenuController.cs tests/ui/art/InputHintPresenterTest.cs tests/ui/InventoryMenuControllerTest.cs docs/ui/hpa-374/sources docs/ui/hpa-374/contact-sheets
rtk git commit -m "feat: add binding-aware input artwork"
```

---

### Task 8: Generate the Celestial Ornament Set

**Files:**
- Create: `docs/ui/hpa-374/sources/prompts/ornaments.md`
- Modify: `docs/ui/hpa-374/sources/extraction-map.json`
- Modify: `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`
- Create: `assets/sprites/ui/ornaments/*.png`
- Local only: `art_source/ui/hpa-374/boards/ornaments/*`

**Interfaces:**
- Produces: all 13 `UiOrnamentId` resources at their documented nonuniform canvas sizes.
- Full screen composition remains downstream; this task proves safe reusable overlays, repeat, and nine-patch geometry.

- [ ] **Step 1: Define exact ornament prompts and crop geometry**

Create `docs/ui/hpa-374/sources/prompts/ornaments.md` with the HPA-373 reference, approved palette, flat `#00FF00` background, no text, no watermark, no panel fill, no detached particles, and these exact rows:

| Done | ID | Source composition | Runtime size | Required geometry |
|---|---|---|---:|---|
| [ ] | `celestial_anchor` | Symmetrical compass-star anchor inside one broken orbit | 192×192 | Square, uniform-scale safe |
| [ ] | `orbit_arc` | Wide thin elliptical orbit with two restrained star nodes | 512×256 | 2:1, crop-safe ends |
| [ ] | `trajectory_line` | Long horizontal comet trajectory with a quiet middle span | 512×64 | 8:1, stretch-safe center |
| [ ] | `calibration_ticks` | Horizontal cyan calibration baseline with sparse gold ticks | 256×64 | 4:1, artwork reaches both horizontal crop edges |
| [ ] | `callout_frame` | Angular celestial callout border and corner notches | 512×256 | 2:1, transparent center, final 32 px border |
| [ ] | `callout_connector` | Thin horizontal angular connector with quiet center span | 256×64 | 4:1, stretch-safe center |
| [ ] | `catalogue_rail_endcap` | Tall celestial rail cap with compass finial | 128×256 | 1:2, uniform-scale safe |
| [ ] | `ignition_seal` | Circular ignition sigil with an open center | 192×192 | Square, uniform-scale safe |
| [ ] | `constellation_corner` | One right-angle constellation corner flourish | 128×128 | Square, outer edges inset |
| [ ] | `constellation_divider` | Long sparse constellation divider with a central star | 512×64 | 8:1, stretch-safe center |
| [ ] | `partial_sigil` | Deliberately incomplete circular sigil fragment | 256×256 | Square, uniform-scale safe |
| [ ] | `focus_halo` | Thin cyan circular halo with four small cardinal points | 96×96 | Square, clearly cyan |
| [ ] | `selection_halo` | Thin gold circular halo with four offset star points | 96×96 | Square, clearly different geometry from focus |

Each call uses a square 1024×1024 source canvas. The prompt tells image generation to place the subject inside the centered target-aspect crop at no less than twice final resolution. For `callout_frame`, the source border is 64 px inside a 1024×512 crop so downsampling produces the exact 32 px preservation margin.

- [ ] **Step 2: Generate one source per logical ornament**

Make exactly 13 built-in `image_gen` calls. Every call references:

```json
{
  "referenced_image_paths": [
    "/Users/chanwaichan/workspace/sirius/docs/ui/hpa-373/reference/battle-preparation-reference.png"
  ]
}
```

Inspect and copy each selected result to:

```text
art_source/ui/hpa-374/boards/ornaments/<id>-source.png
```

Generate `celestial_anchor`, `orbit_arc`, `trajectory_line`, `calibration_ticks`, `callout_frame`, `callout_connector`, `catalogue_rail_endcap`, `ignition_seal`, `constellation_corner`, `constellation_divider`, `partial_sigil`, `focus_halo`, and `selection_halo`.

- [ ] **Step 3: Remove chroma key and record explicit aspect crops**

Run:

```bash
for hpa374_id in celestial_anchor orbit_arc trajectory_line calibration_ticks callout_frame callout_connector catalogue_rail_endcap ignition_seal constellation_corner constellation_divider partial_sigil focus_halo selection_halo
do
  rtk python3.12 /Users/chanwaichan/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py --input "art_source/ui/hpa-374/boards/ornaments/${hpa374_id}-source.png" --out "art_source/ui/hpa-374/boards/ornaments/${hpa374_id}-alpha.png" --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
done
```

Inspect each `<id>-alpha.png`. In `extraction-map.json`, record centered crops with the exact target aspect ratios; never resize a square crop into a non-square target.

Before extraction, require:

- `calibration_ticks`: nontransparent pixels intersect both horizontal crop edges and remain inset from top/bottom.
- `callout_frame`: transparent center begins at least 64 source pixels from every crop edge.
- `focus_halo` and `selection_halo`: no nonuniform crop or resize.

- [ ] **Step 4: Register, extract, and verify ornament invariants**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py register --family ornaments
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py extract --family ornaments
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py verify --family ornaments
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

`verify --family ornaments` must assert exact dimensions, RGBA mode, transparency, safety insets, byte-identical nonempty left/right columns for `calibration_ticks`, a transparent `callout_frame` center, and a 32 px nontransparent preservation band on every frame side.

- [ ] **Step 5: Review the ornament contact sheet**

Open `docs/ui/hpa-374/contact-sheets/ornaments.png`. Review every asset at true size and at 50% uniform scale on `#0D1530`. Also inspect a three-repeat `calibration_ticks` strip and nine-patch guide lines over `callout_frame`. Reject seams, stretched circular geometry, clipped glow, filled content surfaces, or focus/selection halos that differ only by hue.

- [ ] **Step 6: Commit the ornament set**

```bash
rtk git add assets/sprites/ui/ornaments docs/ui/hpa-374/sources docs/ui/hpa-374/contact-sheets
rtk git commit -m "feat: add celestial UI ornaments"
```

---

### Task 9: Generate Effects and Commit the Mipmap Import Exception

**Files:**
- Create: `docs/ui/hpa-374/sources/prompts/effects.md`
- Modify: `docs/ui/hpa-374/sources/extraction-map.json`
- Modify: `docs/ui/hpa-374/sources/SOURCE_MANIFEST.md`
- Create: `assets/sprites/effects/ui/encounter_burst.png`
- Create: `assets/sprites/effects/ui/hit_impact.png`
- Create: `assets/sprites/effects/ui/status_pulse.png`
- Create: `assets/sprites/effects/ui/reward_level_up.png`
- Create: `assets/sprites/effects/ui/*.png.import`
- Modify: `tests/ui/art/UiArtCatalogTest.cs`
- Local only: `art_source/ui/hpa-374/boards/effects/*`

**Interfaces:**
- Produces: four static 256×256 transparent effects; Godot handles scale, opacity, rotation, or duplication later.
- The four effect textures alone have mipmaps. No frame animation or battle-flow change is introduced.

- [ ] **Step 1: Define exact effect prompts**

Create `docs/ui/hpa-374/sources/prompts/effects.md` with a square 1024×1024 canvas, flat `#00FF00` background, at least 18% clear padding, a separable luminous subject, no text, no watermark, no scene background, and:

| Done | ID | Subject |
|---|---|---|
| [ ] | `encounter_burst` | Radial cyan-and-gold celestial gate burst with eight uneven rays and a transparent center |
| [ ] | `hit_impact` | Sharp rose-and-gold crossed impact slash with a compact white core |
| [ ] | `status_pulse` | Expanding cyan/violet circular status wave with four orbit nodes and a transparent center |
| [ ] | `reward_level_up` | Upward gold constellation bloom with one rising central star and two restrained arcs |

Each prompt references both the HPA-373 battle-preparation artwork and retained battle background:

```json
{
  "referenced_image_paths": [
    "/Users/chanwaichan/workspace/sirius/docs/ui/hpa-373/reference/battle-preparation-reference.png",
    "/Users/chanwaichan/workspace/sirius/assets/sprites/ui/ui_battle_background.png"
  ]
}
```

- [ ] **Step 2: Generate, clean, and inspect four isolated sources**

Make exactly four built-in `image_gen` calls, inspect each returned image, and copy selected outputs to:

```text
art_source/ui/hpa-374/boards/effects/<id>-source.png
```

Run:

```bash
for hpa374_id in encounter_burst hit_impact status_pulse reward_level_up
do
  rtk python3.12 /Users/chanwaichan/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py --input "art_source/ui/hpa-374/boards/effects/${hpa374_id}-source.png" --out "art_source/ui/hpa-374/boards/effects/${hpa374_id}-alpha.png" --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
done
```

Because these assets contain controlled glow, inspect on green, white, and `#0D1530`. Retry an affected ID once with the command above plus `--edge-contract 1` if a green fringe remains. If that retry still fails, stop and ask before using a native-transparency or CLI fallback.

- [ ] **Step 3: Write the failing imported-texture test**

Append to `UiArtCatalogTest.cs`:

```csharp
[TestCase]
public void Effects_LoadAtDocumentedSizeWithMipmaps()
{
    foreach (var id in Enum.GetValues<UiEffectId>())
    {
        var texture = UiArtCatalog.LoadEffect(id);
        AssertThat(texture).IsNotNull();
        AssertThat(texture!.GetSize()).IsEqual(new Vector2(256, 256));
        AssertThat(texture.HasMipmaps()).IsTrue();
    }
}
```

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~UiArtCatalogTest.Effects_LoadAtDocumentedSizeWithMipmaps"
```

Expected: the test fails because the effect resources do not exist.

- [ ] **Step 4: Export and verify source PNGs**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py register --family effects
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py extract --family effects
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py verify --family effects
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

Expected: four 256×256 RGBA PNGs with real transparency and nonempty alpha cores.

- [ ] **Step 5: Generate and pin the four Godot import sidecars**

Run Godot's editor import once:

```bash
rtk /Applications/Godot_mono.app/Contents/MacOS/Godot --headless --editor --path . --quit
```

For each generated `assets/sprites/effects/ui/<id>.png.import`, preserve the generated `uid`, `source_file`, and imported cache path while setting:

```ini
compress/mode=0
mipmaps/generate=true
mipmaps/limit=-1
process/fix_alpha_border=true
process/premult_alpha=false
```

Run the same Godot import command again so the cache matches the committed settings. Do not commit any icon or ornament `.import` sidecar.

- [ ] **Step 6: Prove the mipmap split**

Run:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~UiArtCatalogTest.Effects_LoadAtDocumentedSizeWithMipmaps"
rtk rg -n '^mipmaps/generate=true$' assets/sprites/effects/ui -g '*.png.import'
rtk git status --short --ignored
rtk git diff --check
```

Expected: the Godot test passes; `rg` returns exactly four sidecars; no icon or ornament import sidecar is staged.

- [ ] **Step 7: Commit effects and reproducible imports**

```bash
rtk git add assets/sprites/effects/ui docs/ui/hpa-374/sources docs/ui/hpa-374/contact-sheets tests/ui/art/UiArtCatalogTest.cs
rtk git commit -m "feat: add mipmapped UI effects"
```

---

### Task 10: Bundle the Approved Fonts with Reproducible Licenses

**Files:**
- Create: `assets/fonts/cinzel/Cinzel-Variable.ttf`
- Create: `assets/fonts/cinzel/OFL.txt`
- Create: `assets/fonts/noto_sans/NotoSans-Regular.ttf`
- Create: `assets/fonts/noto_sans/NotoSans-Medium.ttf`
- Create: `assets/fonts/noto_sans/NotoSans-SemiBold.ttf`
- Create: `assets/fonts/noto_sans/OFL.txt`
- Create: `assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf`
- Create: `assets/fonts/noto_sans_mono/OFL.txt`
- Create: `docs/ui/hpa-374/ASSET_MANIFEST.md`
- Modify: `tests/ui/art/UiArtCatalogTest.cs`

**Interfaces:**
- Produces: five Godot-loadable font binaries and one OFL copy per committed font-family directory.
- Font role wiring into a shared Theme remains HPA-373 work.

- [ ] **Step 1: Add the failing Godot font-load test**

Append:

```csharp
[TestCase]
public void ApprovedFonts_LoadAsFontFiles()
{
    string[] paths =
    [
        "res://assets/fonts/cinzel/Cinzel-Variable.ttf",
        "res://assets/fonts/noto_sans/NotoSans-Regular.ttf",
        "res://assets/fonts/noto_sans/NotoSans-Medium.ttf",
        "res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf",
        "res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf"
    ];

    foreach (var path in paths)
    {
        AssertThat(ResourceLoader.Exists(path)).IsTrue();
        AssertThat(ResourceLoader.Load<FontFile>(path)).IsNotNull();
    }
}
```

Run the focused test and confirm it fails because the font files are absent:

```bash
rtk dotnet vstest .godot/mono/temp/bin/Debug/Sirius.dll --Tests:ApprovedFonts_LoadAsFontFiles --Settings:test.runsettings.local
```

- [ ] **Step 2: Download exact official upstream revisions**

Create the directories:

```bash
rtk mkdir -p assets/fonts/cinzel assets/fonts/noto_sans assets/fonts/noto_sans_mono
```

Download Cinzel from Google Fonts commit `7ff85c87f93ea6cca5f41c69f2e4edcb90240f26`:

```bash
rtk curl -fL 'https://raw.githubusercontent.com/google/fonts/7ff85c87f93ea6cca5f41c69f2e4edcb90240f26/ofl/cinzel/Cinzel%5Bwght%5D.ttf' -o assets/fonts/cinzel/Cinzel-Variable.ttf
rtk curl -fL 'https://raw.githubusercontent.com/google/fonts/7ff85c87f93ea6cca5f41c69f2e4edcb90240f26/ofl/cinzel/OFL.txt' -o assets/fonts/cinzel/OFL.txt
```

Download static Noto files from archived official `notofonts/noto-fonts` commit `ffebf8c1ee449e544955a7e813c54f9b73848eac`:

```bash
rtk curl -fL 'https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-Regular.ttf' -o assets/fonts/noto_sans/NotoSans-Regular.ttf
rtk curl -fL 'https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-Medium.ttf' -o assets/fonts/noto_sans/NotoSans-Medium.ttf
rtk curl -fL 'https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSans/NotoSans-SemiBold.ttf' -o assets/fonts/noto_sans/NotoSans-SemiBold.ttf
rtk curl -fL 'https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/hinted/ttf/NotoSansMono/NotoSansMono-Medium.ttf' -o assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf
rtk curl -fL 'https://raw.githubusercontent.com/notofonts/noto-fonts/ffebf8c1ee449e544955a7e813c54f9b73848eac/LICENSE' -o assets/fonts/noto_sans/OFL.txt
rtk cp assets/fonts/noto_sans/OFL.txt assets/fonts/noto_sans_mono/OFL.txt
```

- [ ] **Step 3: Verify every binary and license hash**

Run:

```bash
rtk shasum -a 256 assets/fonts/cinzel/Cinzel-Variable.ttf assets/fonts/cinzel/OFL.txt assets/fonts/noto_sans/NotoSans-Regular.ttf assets/fonts/noto_sans/NotoSans-Medium.ttf assets/fonts/noto_sans/NotoSans-SemiBold.ttf assets/fonts/noto_sans/OFL.txt assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf assets/fonts/noto_sans_mono/OFL.txt
```

Require these exact SHA-256 values:

| File | SHA-256 |
|---|---|
| `Cinzel-Variable.ttf` | `f4d83d34d1f6c741193e4acf4b3dff9531e5a67b6aa65228d00a7db72a4e0f34` |
| Cinzel `OFL.txt` | `f2b3029aba64c378bf0963b62945eee15e564fe4330b934c8f2eb058282b5e83` |
| `NotoSans-Regular.ttf` | `b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5` |
| `NotoSans-Medium.ttf` | `7bbe267354704c6ad18bde24b1dbc756c8e4380ca1c3f3c25c45ec5c4471510b` |
| `NotoSans-SemiBold.ttf` | `87a8b90ece1e89746b544e4e086f85a3710e41485a8078f9be874837dfad45d5` |
| Noto Sans `OFL.txt` | `0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb` |
| `NotoSansMono-Medium.ttf` | `b1e09ba9f3607d81aedc9e4e1cbe225a0df85c77bde267931a1ab28577840edd` |
| Noto Sans Mono `OFL.txt` | `0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb` |

Delete and re-download any mismatched file; never update the manifest to bless an unexpected hash.

- [ ] **Step 4: Record exact roles and provenance**

Create `docs/ui/hpa-374/ASSET_MANIFEST.md` with the two upstream commits, every direct URL, the hash table above, and these roles:

| Runtime file | Sirius role |
|---|---|
| `Cinzel-Variable.ttf` | Cinzel SemiBold display role using the variable font's weight axis at 600 |
| `NotoSans-Regular.ttf` | Body text |
| `NotoSans-Medium.ttf` | Controls and compact labels |
| `NotoSans-SemiBold.ttf` | Emphasis and headings |
| `NotoSansMono-Medium.ttf` | Numeric/stat readouts and input-overlay labels |

State that each font is distributed under its committed SIL Open Font License and that Theme wiring is outside HPA-374. At this commit, the manifest contains complete `Fonts and licensing` content and no empty headings; the verified generated-art and runtime sections are appended with their actual records during final documentation.

- [ ] **Step 5: Import and load all fonts through Godot**

Run:

```bash
rtk /Applications/Godot_mono.app/Contents/MacOS/Godot --headless --editor --path . --quit
rtk dotnet vstest .godot/mono/temp/bin/Debug/Sirius.dll --Tests:ApprovedFonts_LoadAsFontFiles --Settings:test.runsettings.local
rtk git diff --check
```

Expected: all five paths load as `FontFile`. No font `.import` sidecar is committed.

- [ ] **Step 6: Commit fonts, licenses, and provenance**

```bash
rtk git add assets/fonts docs/ui/hpa-374/ASSET_MANIFEST.md tests/ui/art/UiArtCatalogTest.cs
rtk git commit -m "feat: bundle approved Sirius UI fonts"
```

---

### Task 11: Enforce Complete Asset Coverage and Replace Stale Documentation

**Files:**
- Create: `tests/tools/test_ui_asset_coverage.py`
- Create: `docs/ui/hpa-374/README.md`
- Create: `docs/ui/hpa-374/CONTACT_SHEETS.md`
- Modify: `docs/ui/hpa-374/ASSET_MANIFEST.md`
- Modify: `docs/ui/UI_SPRITES.md`
- Modify: `docs/items/ASSET_STATUS.md`
- Modify when validation exposes a defect: `tools/ui_art_spec.py`
- Modify when validation exposes a defect: `tools/ui_art_pipeline.py`
- Regenerate: `docs/ui/hpa-374/contact-sheets/*.png`

**Interfaces:**
- Produces: the release gate for all 203 runtime PNGs, five font binaries, three OFL copies, 79 ignored-source records, negative-path rules, and scoped production-emoji policy.
- Documentation becomes filesystem-derived and explicitly records current consumers and deferrals.

- [ ] **Step 1: Add the exact filesystem and image release tests**

Create `tests/tools/test_ui_asset_coverage.py`. Build expected paths only from `ICON_GROUPS`, `ORNAMENT_SIZES`, and `EFFECT_SIZES`; do not discover expected IDs from the filesystem.

Use these core checks:

```python
from hashlib import sha256
import json
from pathlib import Path
import re

from PIL import Image

from tools.ui_art_spec import EFFECT_SIZES, ICON_GROUPS, ORNAMENT_SIZES


PROJECT_ROOT = Path(__file__).resolve().parents[2]
ICON_SIZES = (16, 24, 32)


def expected_icon_paths() -> list[Path]:
    return [
        PROJECT_ROOT / "assets/sprites/ui/icons" / category / str(size) / f"{icon_id}.png"
        for category, ids in ICON_GROUPS.items()
        for size in ICON_SIZES
        for icon_id in ids
    ]


def test_runtime_inventory_has_exact_counts_and_paths():
    icon_paths = expected_icon_paths()
    ornament_paths = [
        PROJECT_ROOT / "assets/sprites/ui/ornaments" / f"{asset_id}.png"
        for asset_id in ORNAMENT_SIZES
    ]
    effect_paths = [
        PROJECT_ROOT / "assets/sprites/effects/ui" / f"{asset_id}.png"
        for asset_id in EFFECT_SIZES
    ]
    assert len(icon_paths) == 186
    assert len(ornament_paths) == 13
    assert len(effect_paths) == 4
    assert all(path.is_file() for path in icon_paths + ornament_paths + effect_paths)


def test_every_icon_has_true_size_alpha_and_readable_opaque_core():
    for path in expected_icon_paths():
        size = int(path.parent.name)
        with Image.open(path) as image:
            assert image.mode == "RGBA", path
            assert image.size == (size, size), path
            assert "icc_profile" not in image.info, path
            assert "srgb" not in image.info, path
            alpha = image.getchannel("A")
            visible = alpha.getbbox()
            assert visible is not None, path
            left, top, right, bottom = visible
            assert left >= 1 and top >= 1 and right <= size - 1 and bottom <= size - 1, path
            assert alpha.getextrema()[0] == 0 and alpha.getextrema()[1] == 255, path
            if size == 16:
                core = alpha.point(lambda value: 255 if value >= 128 else 0).getbbox()
                assert core is not None, path
                width, height = core[2] - core[0], core[3] - core[1]
                assert width >= 0.30 * size and height >= 0.30 * size, path
                assert width >= 0.50 * size or height >= 0.50 * size, path
```

Add separate tests that:

- compare every ornament/effect to its exact documented dimensions;
- require RGBA and real transparency;
- require nonempty visible bounds within forbidden edges;
- permit only `calibration_ticks` to touch horizontal edges;
- require `calibration_ticks` top/bottom inset and byte-identical nonempty first/last RGBA columns;
- require `callout_frame`'s center rectangle `(32, 32, 480, 224)` to contain transparent pixels and every 32 px border band to contain visible pixels;
- require every icon/ornament PNG to lack a tracked `.import` sidecar;
- require exactly four effect `.png.import` files and `mipmaps/generate=true` in each.

```python
def test_ornament_and_effect_dimensions_alpha_and_edges():
    for asset_id, expected_size in ORNAMENT_SIZES.items():
        path = PROJECT_ROOT / "assets/sprites/ui/ornaments" / f"{asset_id}.png"
        with Image.open(path) as image:
            assert image.mode == "RGBA" and image.size == expected_size, path
            alpha = image.getchannel("A")
            bounds = alpha.getbbox()
            assert bounds is not None and alpha.getextrema()[0] == 0, path
            left, top, right, bottom = bounds
            assert top >= 1 and bottom <= image.height - 1, path
            if asset_id != "calibration_ticks":
                assert left >= 1 and right <= image.width - 1, path
    for asset_id, expected_size in EFFECT_SIZES.items():
        path = PROJECT_ROOT / "assets/sprites/effects/ui" / f"{asset_id}.png"
        with Image.open(path) as image:
            assert image.mode == "RGBA" and image.size == expected_size, path
            alpha = image.getchannel("A")
            bounds = alpha.getbbox()
            assert bounds is not None and alpha.getextrema()[0] == 0, path
            assert bounds[0] >= 1 and bounds[1] >= 1, path
            assert bounds[2] <= image.width - 1 and bounds[3] <= image.height - 1, path


def test_calibration_ticks_and_callout_frame_geometry():
    ticks_path = PROJECT_ROOT / "assets/sprites/ui/ornaments/calibration_ticks.png"
    with Image.open(ticks_path).convert("RGBA") as ticks:
        left = ticks.crop((0, 0, 1, ticks.height))
        right = ticks.crop((ticks.width - 1, 0, ticks.width, ticks.height))
        assert left.tobytes() == right.tobytes()
        assert left.getbbox() is not None
    frame_path = PROJECT_ROOT / "assets/sprites/ui/ornaments/callout_frame.png"
    with Image.open(frame_path).convert("RGBA") as frame:
        alpha = frame.getchannel("A")
        assert alpha.crop((32, 32, 480, 224)).getextrema()[1] == 0
        assert alpha.crop((0, 0, 512, 32)).getbbox() is not None
        assert alpha.crop((0, 224, 512, 256)).getbbox() is not None
        assert alpha.crop((0, 0, 32, 256)).getbbox() is not None
        assert alpha.crop((480, 0, 512, 256)).getbbox() is not None


def test_only_effect_import_sidecars_are_tracked():
    import subprocess
    tracked = {
        line
        for line in subprocess.check_output(
            ["git", "ls-files", "*.png.import"],
            cwd=PROJECT_ROOT,
            text=True,
        ).splitlines()
        if "/icons/" in line or "/ornaments/" in line or "/effects/ui/" in line
    }
    expected = {
        f"assets/sprites/effects/ui/{asset_id}.png.import"
        for asset_id in EFFECT_SIZES
    }
    assert tracked == expected
    for relative in expected:
        assert "mipmaps/generate=true" in (PROJECT_ROOT / relative).read_text()
```

- [ ] **Step 2: Add provenance, font, and negative-path tests**

Add:

```python
FONT_HASHES = {
    "assets/fonts/cinzel/Cinzel-Variable.ttf":
        "f4d83d34d1f6c741193e4acf4b3dff9531e5a67b6aa65228d00a7db72a4e0f34",
    "assets/fonts/noto_sans/NotoSans-Regular.ttf":
        "b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5",
    "assets/fonts/noto_sans/NotoSans-Medium.ttf":
        "7bbe267354704c6ad18bde24b1dbc756c8e4380ca1c3f3c25c45ec5c4471510b",
    "assets/fonts/noto_sans/NotoSans-SemiBold.ttf":
        "87a8b90ece1e89746b544e4e086f85a3710e41485a8078f9be874837dfad45d5",
    "assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf":
        "b1e09ba9f3607d81aedc9e4e1cbe225a0df85c77bde267931a1ab28577840edd",
}

OFL_HASHES = {
    "assets/fonts/cinzel/OFL.txt":
        "f2b3029aba64c378bf0963b62945eee15e564fe4330b934c8f2eb058282b5e83",
    "assets/fonts/noto_sans/OFL.txt":
        "0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb",
    "assets/fonts/noto_sans_mono/OFL.txt":
        "0dab92d0544f7b233403f14b84a663bdbfa746982eda629e7f4f9ffe1b036feb",
}


def digest(path: Path) -> str:
    return sha256(path.read_bytes()).hexdigest()


def test_fonts_and_licenses_match_pinned_hashes():
    for relative, expected in (FONT_HASHES | OFL_HASHES).items():
        path = PROJECT_ROOT / relative
        assert path.is_file(), path
        assert digest(path) == expected, path


def test_extraction_map_and_source_manifest_agree():
    records = json.loads(
        (PROJECT_ROOT / "docs/ui/hpa-374/sources/extraction-map.json").read_text()
    )
    manifest = (
        PROJECT_ROOT / "docs/ui/hpa-374/sources/SOURCE_MANIFEST.md"
    ).read_text()
    assert len(records) == 79
    assert len({record["id"] for record in records}) == 79
    for record in records:
        assert record["source"] in manifest
        assert record["source_sha256"] in manifest
        assert record["alpha_source"] in manifest
        assert record["alpha_sha256"] in manifest
        assert record["generator"] == "OpenAI image_gen"
        assert re.fullmatch(r"\d{4}-\d{2}-\d{2}", record["generated_on"])
        assert record["postprocess"]["auto_key"] == "border"
        assert record["postprocess"]["soft_matte"] is True
```

Add one negative-path test requiring these paths to remain absent:

```text
assets/sprites/ui/ui_button_attack.png
assets/sprites/ui/ui_button_defend.png
assets/sprites/ui/ui_button_run.png
assets/sprites/ui/icon_health.png
assets/sprites/ui/icon_experience.png
assets/sprites/ui/icon_level.png
assets/sprites/effects/effect_hit_impact.png
assets/sprites/effects/effect_magic_sparkles.png
assets/sprites/effects/effect_level_up.png
```

Also assert that no canonical icon filename is `filter.png`, `sort.png`, `comparison.png`, `compare.png`, or `passive_skill.png`.

```python
PROHIBITED_PATHS = (
    "assets/sprites/ui/ui_button_attack.png",
    "assets/sprites/ui/ui_button_defend.png",
    "assets/sprites/ui/ui_button_run.png",
    "assets/sprites/ui/icon_health.png",
    "assets/sprites/ui/icon_experience.png",
    "assets/sprites/ui/icon_level.png",
    "assets/sprites/effects/effect_hit_impact.png",
    "assets/sprites/effects/effect_magic_sparkles.png",
    "assets/sprites/effects/effect_level_up.png",
)


def test_retired_and_hpa375_only_assets_are_absent():
    assert all(not (PROJECT_ROOT / relative).exists() for relative in PROHIBITED_PATHS)
    prohibited_names = {
        "filter.png", "sort.png", "comparison.png", "compare.png", "passive_skill.png"
    }
    actual_names = {
        path.name
        for path in (PROJECT_ROOT / "assets/sprites/ui/icons").rglob("*.png")
    }
    assert actual_names.isdisjoint(prohibited_names)
```

- [ ] **Step 3: Add the scoped production-emoji scanner**

Implement a Unicode-symbol detector covering `U+2600–U+27BF` and `U+1F300–U+1FAFF`. Scan only:

- `.tscn` values assigned to `text`, `tooltip_text`, `placeholder_text`, `dialog_text`, or `title`;
- `scripts/ui/**/*.cs` string literals assigned to `.Text`, `.TooltipText`, `.DialogText`, or `.Title`.

The C# scanner strips line and block comments before matching assignments. Calls to `GD.Print`, `GD.PrintErr`, `GD.PushWarning`, and `GD.PushError` are outside the accepted assignment shapes and are therefore excluded structurally. The failing assertion prints `path:line` and the user-facing field value.

```python
EMOJI = re.compile("[\u2600-\u27bf\U0001f300-\U0001faff]")
TSCN_USER_TEXT = re.compile(
    r'^\s*(text|tooltip_text|placeholder_text|dialog_text|title)\s*=\s*"(?P<value>.*)"\s*$'
)
C_SHARP_TOKEN = re.compile(
    r'(?P<string>(?:\\$@|@\\$|\\$|@)?"(?:\\\\.|""|[^"\\\\])*")'
    r'|(?P<comment>//[^\n]*|/\\*.*?\\*/)',
    re.DOTALL,
)
C_SHARP_USER_TEXT = re.compile(
    r'\\.(Text|TooltipText|DialogText|Title)\s*=\s*'
    r'(?P<value>(?:\\$@|@\\$|\\$|@)?"(?:\\\\.|""|[^"\\\\])*")'
)


def strip_csharp_comments(source: str) -> str:
    def replace(match: re.Match[str]) -> str:
        if match.group("string") is not None:
            return match.group("string")
        return "\n" * match.group(0).count("\n")
    return C_SHARP_TOKEN.sub(replace, source)


def test_structural_user_facing_strings_contain_no_emoji():
    offenders: list[str] = []
    for path in (PROJECT_ROOT / "scenes").rglob("*.tscn"):
        for line_number, line in enumerate(path.read_text().splitlines(), 1):
            match = TSCN_USER_TEXT.match(line)
            if match and EMOJI.search(match.group("value")):
                offenders.append(
                    f"{path.relative_to(PROJECT_ROOT)}:{line_number}: {match.group('value')}"
                )
    for path in (PROJECT_ROOT / "scripts/ui").rglob("*.cs"):
        source = strip_csharp_comments(path.read_text())
        for match in C_SHARP_USER_TEXT.finditer(source):
            if EMOJI.search(match.group("value")):
                line_number = source.count("\n", 0, match.start()) + 1
                offenders.append(
                    f"{path.relative_to(PROJECT_ROOT)}:{line_number}: {match.group('value')}"
                )
    assert offenders == []
```

- [ ] **Step 4: Run the release test and fix only concrete failures**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 -m pytest tests/tools/test_ui_asset_coverage.py -q
```

Expected initial failures: the high-level HPA-374 documentation and final contact-sheet index are incomplete. Asset failures are fixed in the relevant source record/pipeline and regenerated from that record; tests are not weakened to accept a bad derivative.

- [ ] **Step 5: Regenerate and visually approve all six contact sheets**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py manifest
rtk uv run --with-requirements requirements-dev.txt python3 tools/ui_art_pipeline.py contact-sheets
```

Inspect:

```text
docs/ui/hpa-374/contact-sheets/icons-16.png
docs/ui/hpa-374/contact-sheets/icons-24.png
docs/ui/hpa-374/contact-sheets/icons-32.png
docs/ui/hpa-374/contact-sheets/icon-states.png
docs/ui/hpa-374/contact-sheets/ornaments.png
docs/ui/hpa-374/contact-sheets/effects.png
```

Reject ambiguous 16 px silhouettes, inconsistent outline weight, alpha fringe, clipped glow, colour-only state distinctions, unreadable disabled treatment, a calibration seam, or a distorted halo/frame.

- [ ] **Step 6: Complete the HPA-374 documentation from verified files**

Create `docs/ui/hpa-374/README.md` with:

- shipped counts: 62 logical icons, 186 icon PNGs, 13 ornaments, 4 effects, 203 runtime PNGs, and 5 font binaries;
- stable path conventions and `UiArtCatalog` as runtime owner;
- current consumers: Inventory headings, empty equipment/accessory slots, inactive accessory locks, and binding-aware Inventory close hint;
- retained main-menu and battle backgrounds;
- explicit deferrals: battle stat/status row placement, battle effect placement, modal semantic headers, Theme font wiring, and full ornament composition;
- explicit exclusions: HPA-375 filter/sort/comparison/passive-skill art and manual Attack/Defend/Run button art;
- the Python and GdUnit4 verification commands.

Complete `docs/ui/hpa-374/ASSET_MANIFEST.md` with one generated-art row per logical ID containing runtime path(s), family prompt, source and alpha filenames/hashes, generation tool/date, crop, post-processing, and intended usage. Include this source statement:

```text
The UI artwork listed in this manifest was generated specifically for Sirius
with OpenAI image_gen and was not sourced from a third-party art pack.
```

Do not make claims of exclusive ownership or copyrightability.

Create `docs/ui/hpa-374/CONTACT_SHEETS.md` with links to all six sheets, review criteria, review date, and accepted/rejected status. Mark a sheet accepted only after the visual inspection in Step 5.

- [ ] **Step 7: Replace stale global asset documentation**

Update `docs/ui/UI_SPRITES.md` and `docs/items/ASSET_STATUS.md` so they:

- link to the HPA-374 manifest and categorized paths;
- remove planned root `icon_health.png`, `icon_experience.png`, and `icon_level.png`;
- remove planned `ui_button_attack.png`, `ui_button_defend.png`, and `ui_button_run.png`;
- replace the old root 96×96 effect paths with the four `assets/sprites/effects/ui/` 256×256 files;
- state that `status_pulse` is a new status-specific effect, not a rename of `effect_magic_sparkles.png`;
- report only files that exist and pass validation.

- [ ] **Step 8: Run the complete Python gate and commit**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 -m pytest tests/tools -q
rtk git diff --check
rtk rg -n 'ui_button_(attack|defend|run)|assets/sprites/ui/icon_(health|experience|level)\\.png|effect_(hit_impact|magic_sparkles|level_up)\\.png' docs/ui/UI_SPRITES.md docs/items/ASSET_STATUS.md
```

Expected: all Python tests pass; the final `rg` finds no stale path presented as current or planned. Historical explanation may name a retired file only when it is explicitly labeled absent.

Commit:

```bash
rtk git add tests/tools/test_ui_asset_coverage.py docs/ui/hpa-374 docs/ui/UI_SPRITES.md docs/items/ASSET_STATUS.md tools/ui_art_spec.py tools/ui_art_pipeline.py
rtk git commit -m "docs: finalize HPA-374 UI art inventory"
```

---

### Task 12: Prove Godot Loading, Current Runtime Integration, and Whole-Branch Completion

**Files:**
- Create: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- Modify: `tests/ui/art/UiArtCatalogTest.cs`
- Modify only for verified defects: HPA-374 implementation files from Tasks 1–11

**Interfaces:**
- Produces: exhaustive Godot resource/import tests and two-viewport smoke coverage for the mandatory current consumers.
- Existing scene suites remain the behavior regression gate for main menu, exploration, battle, pause/settings, save/load, dialogue/shop/heal, puzzle, reward, confirmation, warning, and error surfaces.

- [ ] **Step 1: Add exhaustive catalog resource tests**

Extend `UiArtCatalogTest.cs` with:

```csharp
[TestCase]
public void EveryTypedIconLoadsAtItsDeclaredTrueSizeWithoutMipmaps()
{
    foreach (var id in Enum.GetValues<UiIconId>())
    {
        foreach (var size in Enum.GetValues<UiIconSize>())
        {
            var path = UiArtCatalog.GetIconPath(id, size);
            AssertThat(ResourceLoader.Exists(path)).IsTrue();
            var texture = UiArtCatalog.LoadIcon(id, size);
            AssertThat(texture).IsNotNull();
            AssertThat(texture!.GetSize()).IsEqual(new Vector2((int)size, (int)size));
            AssertThat(texture.HasMipmaps()).IsFalse();
        }
    }
}

[TestCase]
public void EveryTypedOrnamentLoadsAtItsDeclaredSizeWithoutMipmaps()
{
    var sizes = new Dictionary<UiOrnamentId, Vector2>
    {
        [UiOrnamentId.CelestialAnchor] = new(192, 192),
        [UiOrnamentId.OrbitArc] = new(512, 256),
        [UiOrnamentId.TrajectoryLine] = new(512, 64),
        [UiOrnamentId.CalibrationTicks] = new(256, 64),
        [UiOrnamentId.CalloutFrame] = new(512, 256),
        [UiOrnamentId.CalloutConnector] = new(256, 64),
        [UiOrnamentId.CatalogueRailEndcap] = new(128, 256),
        [UiOrnamentId.IgnitionSeal] = new(192, 192),
        [UiOrnamentId.ConstellationCorner] = new(128, 128),
        [UiOrnamentId.ConstellationDivider] = new(512, 64),
        [UiOrnamentId.PartialSigil] = new(256, 256),
        [UiOrnamentId.FocusHalo] = new(96, 96),
        [UiOrnamentId.SelectionHalo] = new(96, 96)
    };

    foreach (var (id, expectedSize) in sizes)
    {
        var texture = UiArtCatalog.LoadOrnament(id);
        AssertThat(texture).IsNotNull();
        AssertThat(texture!.GetSize()).IsEqual(expectedSize);
        AssertThat(texture.HasMipmaps()).IsFalse();
    }
}
```

Add:

```csharp
[TestCase]
public void RuntimeEnumsHaveExhaustiveMappingsExceptReservedStatusValue()
{
    foreach (var type in Enum.GetValues<StatusEffectType>())
        AssertThat(UiArtCatalog.TryForStatusEffect(type, out _)).IsTrue();
    AssertThat(UiArtCatalog.TryForStatusEffect((StatusEffectType)11, out _)).IsFalse();
    foreach (var category in Enum.GetValues<ItemCategory>())
        AssertThat(Enum.IsDefined(UiArtCatalog.ForItemCategory(category))).IsTrue();
    foreach (var slot in Enum.GetValues<EquipmentSlotType>())
        AssertThat(Enum.IsDefined(UiArtCatalog.ForEquipmentSlot(slot))).IsTrue();
}

[TestCase]
public void RetainedScenicBackgrounds_LoadFromStablePaths()
{
    var mainMenu = ResourceLoader.Load<Texture2D>(
        "res://assets/sprites/ui/ui_main_menu_background.png");
    var battle = ResourceLoader.Load<Texture2D>(
        "res://assets/sprites/ui/ui_battle_background.png");

    AssertThat(mainMenu).IsNotNull();
    AssertThat(mainMenu!.GetSize()).IsEqual(new Vector2(1920, 1080));
    AssertThat(battle).IsNotNull();
    AssertThat(battle!.GetSize()).IsEqual(new Vector2(1280, 720));
}
```

Keep the effect mipmap test and five-font `FontFile` test added in their owning tasks; together these tests complete the resource matrix.

- [ ] **Step 2: Add the two-viewport current-consumer smoke test**

Create `tests/ui/art/Hpa374RuntimeSmokeTest.cs` with `[RequireGodotRuntime]` and:

```csharp
private GameManager? _gameManager;
private InventoryMenuController? _inventoryMenu;
private SubViewport? _viewport;
private bool _treeWasPaused;

[BeforeTest]
public async Task Setup()
{
    var sceneTree = (SceneTree)Engine.GetMainLoop();
    _treeWasPaused = sceneTree.Paused;
    sceneTree.Paused = false;
    ResetGameManagerSingleton();

    _gameManager = new GameManager { AutoSaveEnabled = false };
    sceneTree.Root.AddChild(_gameManager);
    await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

    _viewport = new SubViewport
    {
        Disable3D = true,
        HandleInputLocally = true,
        Size = new Vector2I(640, 360)
    };
    sceneTree.Root.AddChild(_viewport);
    var packed = ResourceLoader.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
    _inventoryMenu = packed.Instantiate<InventoryMenuController>();
    _viewport.AddChild(_inventoryMenu);
    await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
}

[AfterTest]
public async Task Cleanup()
{
    var sceneTree = (SceneTree)Engine.GetMainLoop();
    sceneTree.Paused = false;
    if (_inventoryMenu is { } menu && IsInstanceValid(menu))
    {
        if (menu.Visible)
            menu.CloseMenu();
        menu.Free();
    }
    if (_viewport is { } viewport && IsInstanceValid(viewport))
        viewport.Free();
    if (_gameManager is { } gameManager && IsInstanceValid(gameManager))
        gameManager.Free();
    await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    ResetGameManagerSingleton();
    sceneTree.Paused = _treeWasPaused;
}

private static void ResetGameManagerSingleton()
{
    var property = typeof(GameManager).GetProperty(
        "Instance",
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Static);
    property!.GetSetMethod(true)!.Invoke(null, [null]);
}

[TestCase(640, 360)]
[TestCase(1280, 720)]
public async Task InventoryArtRendersAtVerificationSize(int width, int height)
{
    _viewport!.Size = new Vector2I(width, height);
    _inventoryMenu!.OpenMenu();
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var equipmentHeading = _inventoryMenu.GetNode<TextureRect>("%EquipmentTitleIcon");
    var itemHeading = _inventoryMenu.GetNode<TextureRect>("%InventoryTitleIcon");
    var weapon = _inventoryMenu.GetNode<PanelContainer>("%WeaponSlot")
        .GetNode<TextureButton>("Button");
    var locked = _inventoryMenu.GetNode<PanelContainer>("%AccessorySlot4")
        .GetNode<TextureButton>("Button");
    var close = _inventoryMenu.GetNode<Button>("%CloseButton");

    AssertThat(_inventoryMenu.Visible).IsTrue();
    AssertThat(equipmentHeading.Texture.GetSize()).IsEqual(new Vector2(24, 24));
    AssertThat(itemHeading.Texture.GetSize()).IsEqual(new Vector2(24, 24));
    AssertThat(weapon.TextureNormal.GetSize()).IsEqual(new Vector2(32, 32));
    AssertThat(locked.Disabled).IsTrue();
    AssertThat(locked.TextureDisabled.GetSize()).IsEqual(new Vector2(32, 32));
    AssertThat(close.Text).StartsWith("Close [");

    var rendered = _viewport.GetTexture().GetImage();
    AssertThat(rendered).IsNotNull();
    AssertThat(rendered!.IsEmpty()).IsFalse();
}
```

Add `using GdUnit4;`, `using Godot;`, `using System;`, `using System.Linq;`, `using System.Threading.Tasks;`, and `using static GdUnit4.Assertions;`. Close/free the menu before freeing the viewport and singleton, as shown.

Add:

```csharp
[TestCase]
public void CompactHintSmoke_ChangesArtworkAndReadableLabelByDevice()
{
    const string action = "hpa374_smoke_hint";
    var existed = InputMap.HasAction(action);
    var original = existed
        ? InputMap.ActionGetEvents(action)
            .Select(inputEvent => (InputEvent)inputEvent.Duplicate()).ToArray()
        : Array.Empty<InputEvent>();
    if (!existed)
        InputMap.AddAction(action, 0.5f);

    var presenter = new InputHintPresenter();
    var button = new Button();
    try
    {
        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Key.K });
        presenter.Observe(new InputEventKey { PhysicalKeycode = Key.K, Pressed = true });
        presenter.ApplyCompactButton(button, "Close", action);
        AssertThat(button.Icon.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.KeycapBlank, UiIconSize.Metadata));
        AssertThat(button.Text).IsEqual("Close [K]");

        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left
        });
        presenter.Observe(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true
        });
        presenter.ApplyCompactButton(button, "Close", action);
        AssertThat(button.Icon.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.MousePrimary, UiIconSize.Metadata));
        AssertThat(button.Text).IsEqual("Close [Mouse 1]");

        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.A
        });
        presenter.Observe(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.A,
            Pressed = true
        });
        presenter.ApplyCompactButton(button, "Close", action);
        AssertThat(button.Icon.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(
                UiIconId.GamepadFaceBlank, UiIconSize.Metadata));
        AssertThat(button.Text).IsEqual("Close [A]");

        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion
        {
            Axis = JoyAxis.LeftX,
            AxisValue = 1.0f
        });
        presenter.ApplyCompactButton(button, "Close", action);
        AssertThat(button.Icon.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.GamepadStick, UiIconSize.Metadata));
        AssertThat(button.Text).IsEqual("Close [Left Stick Right]");
    }
    finally
    {
        button.Free();
        InputMap.ActionEraseEvents(action);
        if (existed)
        {
            foreach (var inputEvent in original)
                InputMap.ActionAddEvent(action, inputEvent);
        }
        else
        {
            InputMap.EraseAction(action);
        }
    }
}
```

Add `using System.Linq;` to the smoke test for event snapshots.

- [ ] **Step 3: Run focused runtime tests and fix concrete integration failures**

Run Godot import, then:

```bash
rtk /Applications/Godot_mono.app/Contents/MacOS/Godot --headless --editor --path . --quit
rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~UiArtCatalogTest|FullyQualifiedName~InputHintPresenterTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: every typed resource loads, only effects report mipmaps, both scenic backgrounds retain their stable paths/sizes, dynamic input hints refresh, and the mandatory Inventory integrations render at 640×360 and 1280×720.

- [ ] **Step 4: Run all existing representative scene regressions**

Run the complete C# suite:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local
```

This includes the existing main-menu, game/exploration, battle, pause/settings, save/load, dialogue, shop, heal, puzzle, and related UI suites. A regression is fixed at the narrow integration boundary; it is not dismissed as unrelated when caused by HPA-374.

- [ ] **Step 5: Run a headless project startup and inspect the runtime log**

Run:

```bash
rtk /Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . --quit-after 3 --log-file /tmp/hpa-374-runtime.log
rtk rg -n 'Failed loading resource|does not exist|UiArtCatalog|ERROR|WARNING' /tmp/hpa-374-runtime.log
```

Expected: project startup reaches the retained main menu and quits without a missing-art, catalog, import, or font warning. The second command should return no HPA-374-related match; inspect any unrelated pre-existing message before accepting it.

- [ ] **Step 6: Run the full release matrix**

Run:

```bash
rtk uv run --with-requirements requirements-dev.txt python3 -m pytest tests/tools -q
rtk dotnet build Sirius.sln
rtk dotnet test Sirius.sln --settings test.runsettings.local
rtk rg --files assets/sprites/ui/icons -g '*.png' | rtk wc -l
rtk rg --files assets/sprites/ui/ornaments -g '*.png' | rtk wc -l
rtk rg --files assets/sprites/effects/ui -g '*.png' | rtk wc -l
rtk rg --files assets/fonts -g '*.ttf' | rtk wc -l
rtk git diff --check
```

Expected counts are `186`, `13`, `4`, and `5`. Both test suites and the build pass.

- [ ] **Step 7: Review the whole branch against the approved spec**

Run:

```bash
rtk git diff --stat main...HEAD
rtk git diff --name-status main...HEAD
rtk rg -n -i '\b(T[B]D|T[O]DO|F[I]XME|later dec[i]de|to be dec[i]ded|sim[i]lar to)\b' docs/ui/hpa-374 tools/ui_art_spec.py tools/ui_art_pipeline.py scripts/ui/art tests/ui/art tests/tools/test_ui_asset_coverage.py
rtk git status --short --ignored
```

Review every HPA-374 §14 acceptance row against source, tests, contact sheets, and runtime evidence. Confirm ignored raw sources remain under `art_source/ui/hpa-374/boards/`, the four effect sidecars are the only newly tracked `.import` files, the retained backgrounds are byte-unchanged, and no HPA-375-only or manual-combat asset entered the branch.

- [ ] **Step 8: Commit runtime proof and verify a clean result**

```bash
rtk git add tests/ui/art/UiArtCatalogTest.cs tests/ui/art/Hpa374RuntimeSmokeTest.cs
rtk git commit -m "test: verify HPA-374 runtime artwork"
rtk git status --short
rtk git log --oneline --decorate -12
```

Expected: the worktree is clean and the task history contains one narrow commit per plan task. If the whole-branch review required a fix outside the smoke files, stage that verified fix with its owning files and include it in this final test commit only when it is inseparable from the runtime proof; otherwise make a separate narrowly named fix commit.
