"""Coverage checks for required runtime sprite sheets."""

import unittest
from pathlib import Path

from PIL import Image


PROJECT_ROOT = Path(__file__).resolve().parents[2]
EXPECTED_SPRITE_SHEETS = (
    PROJECT_ROOT / "assets/sprites/enemies/forest_spirit/sprite_sheet.png",
    PROJECT_ROOT / "assets/sprites/enemies/orc/sprite_sheet.png",
    PROJECT_ROOT / "assets/sprites/enemies/skeleton_warrior/sprite_sheet.png",
    PROJECT_ROOT / "assets/sprites/enemies/cave_spider/sprite_sheet.png",
    PROJECT_ROOT / "assets/sprites/npcs/shopkeeper/sprite_sheet.png",
    PROJECT_ROOT / "assets/sprites/npcs/healer/sprite_sheet.png",
)


class SpriteAssetCoverageTest(unittest.TestCase):
    def test_required_runtime_sheets_exist_with_expected_format(self):
        for sheet_path in EXPECTED_SPRITE_SHEETS:
            with self.subTest(sheet=sheet_path.relative_to(PROJECT_ROOT)):
                self.assertTrue(sheet_path.exists(), f"{sheet_path} is missing")

                with Image.open(sheet_path) as sheet:
                    self.assertEqual((384, 96), sheet.size)
                    self.assertEqual("RGBA", sheet.mode)


if __name__ == "__main__":
    unittest.main()
