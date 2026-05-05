import tempfile
import unittest
from pathlib import Path

from tools.tilemap_json_sync import extract_uid_map, restore_uids


class TilemapJsonSyncTest(unittest.TestCase):
    def test_restore_uids_preserves_scene_and_ext_resource_uids(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            scene_path = Path(tmpdir) / "Floor1F.tscn"
            scene_path.write_text(
                "\n".join(
                    [
                        '[gd_scene load_steps=2 format=4 uid="uid://scene123"]',
                        '[ext_resource type="Script" uid="uid://script123" path="res://scripts/game/GridMap.cs" id="1"]',
                        "",
                    ]
                ),
                encoding="utf-8",
            )

            uid_map = extract_uid_map(scene_path)
            scene_path.write_text(
                "\n".join(
                    [
                        "[gd_scene load_steps=2 format=4]",
                        '[ext_resource type="Script" path="res://scripts/game/GridMap.cs" id="1"]',
                        "",
                    ]
                ),
                encoding="utf-8",
            )

            restore_uids(scene_path, uid_map)

            updated = scene_path.read_text(encoding="utf-8")
            self.assertIn('[gd_scene load_steps=2 format=4 uid="uid://scene123"]', updated)
            self.assertIn(
                '[ext_resource type="Script" uid="uid://script123" path="res://scripts/game/GridMap.cs" id="1"]',
                updated,
            )


if __name__ == "__main__":
    unittest.main()
