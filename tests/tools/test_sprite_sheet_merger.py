"""Tests for the sprite sheet merger utility."""

import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.sprite_sheet_merger import SpriteSheetMerger


class SpriteSheetMergerTest(unittest.TestCase):
    def test_merge_all_discovers_npc_sprite_frames(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            project_root = Path(temp_dir)
            frames_dir = project_root / "assets/sprites/npcs/shopkeeper/frames"
            frames_dir.mkdir(parents=True)

            for index in range(1, 5):
                frame = Image.new("RGBA", (96, 96), (index * 40, 20, 120, 255))
                frame.save(frames_dir / f"frame{index}.png", "PNG")

            SpriteSheetMerger(project_root).merge_all()

            sheet_path = project_root / "assets/sprites/npcs/shopkeeper/sprite_sheet.png"
            self.assertTrue(sheet_path.exists())
            with Image.open(sheet_path) as sheet:
                self.assertEqual((384, 96), sheet.size)
                self.assertEqual("RGBA", sheet.mode)


if __name__ == "__main__":
    unittest.main()
