#!/usr/bin/env python3
"""
Sprite Sheet Merger for Sirius RPG
==================================

This script merges individual 96x96 animation frames into horizontal sprite sheets
for use with Godot's animation system.

Usage:
    python tools/sprite_sheet_merger.py
    or
    ./tools/sprite_sheet_merger.py

The script will:
1. Auto-install Pillow if not available
2. Look for frame files in assets/sprites/characters/*/ and assets/sprites/enemies/*/
3. Group frames by character folder (e.g., player_hero/frame1.png, player_hero/frame2.png, etc.)
4. Create horizontal sprite sheets (384x96) in the same character folders
5. Maintain proper frame order (frame1, frame2, frame3, frame4)

File structure:
    assets/sprites/characters/player_hero/frame1.png
    assets/sprites/characters/player_hero/frame2.png
    assets/sprites/characters/player_hero/frame3.png
    assets/sprites/characters/player_hero/frame4.png
    assets/sprites/enemies/goblin/frame1.png (etc.)
    
Output:
    assets/sprites/characters/player_hero/sprite_sheet.png (384x96)
    assets/sprites/enemies/goblin/sprite_sheet.png (384x96)
"""

import sys
import subprocess

def check_and_install_pillow():
    """Check if Pillow is installed, install it if not."""
    try:
        import PIL.Image
        return PIL.Image
    except ImportError:
        print("📦 Pillow not found. Installing...")
        try:
            subprocess.check_call([sys.executable, "-m", "pip", "install", "Pillow"])
            print("✅ Pillow installed successfully!")
            import PIL.Image
            return PIL.Image
        except subprocess.CalledProcessError:
            print("❌ Failed to install Pillow. Please install manually: pip install Pillow")
            sys.exit(1)
        except ImportError:
            print("❌ Failed to import Pillow after installation. Please restart and try again.")
            sys.exit(1)

# Import PIL.Image with auto-installation
Image = check_and_install_pillow()
from pathlib import Path

FRAME_COUNT = 4
FRAME_WIDTH = 96
FRAME_HEIGHT = 96


class SpriteSheetMerger:
    def __init__(self, project_root=None):
        if project_root is None:
            # Try to find project root by looking for project.godot
            current_dir = Path.cwd()
            while current_dir.parent != current_dir:
                if (current_dir / "project.godot").exists():
                    project_root = current_dir
                    break
                current_dir = current_dir.parent
            
            if project_root is None:
                project_root = Path.cwd()
        
        self.project_root = Path(project_root)
        self.sprite_dirs = [
            self.project_root / "assets" / "sprites" / "characters",
            self.project_root / "assets" / "sprites" / "enemies",
        ]

        # Create base directories if they don't exist yet
        for sprite_dir in self.sprite_dirs:
            sprite_dir.mkdir(parents=True, exist_ok=True)
        
        print(f"Project root: {self.project_root}")
        for sprite_dir in self.sprite_dirs:
            print(f"Sprites directory: {sprite_dir}")
    
    def find_character_folders(self):
        """Find all character/enemy folders with frame files."""
        character_folders = {}

        for sprite_dir in self.sprite_dirs:
            if not sprite_dir.exists():
                continue

            for character_folder in sprite_dir.iterdir():
                if not character_folder.is_dir():
                    continue

                frames = {}

                # First, try to find frames directly in the character folder
                for i in range(1, FRAME_COUNT + 1):
                    frame_file = character_folder / f"frame{i}.png"
                    if frame_file.exists():
                        frames[i] = frame_file

                # If no direct frames found, check in frames/ subdirectory
                if not frames:
                    frames_subdir = character_folder / "frames"
                    if frames_subdir.exists() and frames_subdir.is_dir():
                        for i in range(1, FRAME_COUNT + 1):
                            frame_file = frames_subdir / f"frame{i}.png"
                            if frame_file.exists():
                                frames[i] = frame_file

                if frames:
                    key = character_folder.relative_to(self.project_root)
                    character_folders[str(key)] = frames

        return character_folders
    
    def validate_frame_group(self, character_name, frames):
        """Check if all 4 frames are present for a character."""
        missing_frames = []
        for i in range(1, FRAME_COUNT + 1):
            if i not in frames:
                missing_frames.append(f"frame{i}")
        
        if missing_frames:
            print(f"⚠️  {character_name}: Missing frames {missing_frames}")
            return False
        
        return True
    
    def create_sprite_sheet(self, character_name, frames):
        """Merge 4 frames into a horizontal sprite sheet."""
        sprite_sheet = Image.new('RGBA', (FRAME_WIDTH * FRAME_COUNT, FRAME_HEIGHT), (0, 0, 0, 0))
        
        try:
            for frame_num in range(1, FRAME_COUNT + 1):
                frame_path = frames[frame_num]
                with Image.open(frame_path) as frame_source:
                    frame_image = frame_source.copy()

                # Verify frame is 96x96
                expected_size = (FRAME_WIDTH, FRAME_HEIGHT)
                if frame_image.size != expected_size:
                    print(f"⚠️  {frame_path.name} is {frame_image.size}, expected {expected_size}")
                    # Resize if needed using LANCZOS resampling
                    from PIL.Image import Resampling
                    frame_image = frame_image.resize(expected_size, Resampling.LANCZOS)

                # Paste frame into sprite sheet
                x_offset = (frame_num - 1) * FRAME_WIDTH
                sprite_sheet.paste(frame_image, (x_offset, 0))
            
            return sprite_sheet
            
        except Exception as exc:  # pylint: disable=broad-except
            print(f"❌ Error while composing sprite sheet for {character_name}: {exc}")
            return None
    
    def save_sprite_sheet(self, character_name, sprite_sheet):
        """Save sprite sheet to the character's folder."""
        output_path = self.project_root / character_name / "sprite_sheet.png"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        
        try:
            sprite_sheet.save(output_path, "PNG")
            print(f"✅ Created: {output_path.relative_to(self.project_root)}")
            return True
        except Exception as exc:  # pylint: disable=broad-except
            print(f"❌ Error saving {output_path}: {exc}")
            return False
    
    def merge_all(self):
        """Find all character folders and merge their frames into sprite sheets."""
        print("🎨 Sirius RPG Sprite Sheet Merger")
        print("=" * 40)
        
        character_folders = self.find_character_folders()
        
        if not character_folders:
            print("❌ No character or enemy folders with frame files found!")
            return
        
        print(f"📁 Found {len(character_folders)} character(s) with frames:")
        for name in character_folders.keys():
            print(f"   - {name}")
        print()
        
        success_count = 0
        total_count = len(character_folders)
        
        for character_name, frames in character_folders.items():
            print(f"🔄 Processing {character_name}...")
            
            if not self.validate_frame_group(character_name, frames):
                continue
            
            sprite_sheet = self.create_sprite_sheet(character_name, frames)
            if sprite_sheet is None:
                continue
            
            if self.save_sprite_sheet(character_name, sprite_sheet):
                success_count += 1
        
        print()
        print("=" * 40)
        print(f"✅ Successfully created {success_count}/{total_count} sprite sheets!")
        
        if success_count > 0:
            print("\n🎮 Ready for Godot integration:")
            print("1. Import the sprite sheets in Godot")
            print("2. Set Import settings: Filter: Off, Fix Alpha Border: On")
            print("3. Create AnimatedSprite2D nodes")
            print("4. Create SpriteFrames resources with 4 frames each")

def main():
    """Main function to run the sprite sheet merger."""
    print("🎨 Running Sirius RPG Sprite Sheet Merger...")
    print("=" * 50)
    
    merger = SpriteSheetMerger()
    merger.merge_all()
    
    print("\n✅ Sprite sheet merging complete!")

if __name__ == "__main__":
    main()
