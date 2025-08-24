#!/usr/bin/env python3
"""
Sprite Sheet Merger for Sirius RPG
==================================

This script merges individual 32x32 animation frames into horizontal sprite sheets
for use with Godot's animation system.

Usage:
    python tools/sprite_sheet_merger.py
    or
    ./tools/sprite_sheet_merger.py

The script will:
1. Auto-install Pillow if not available
2. Look for frame files in assets/sprites/frames/
3. Group frames by character name (e.g., player_hero_frame1.png, player_hero_frame2.png, etc.)
4. Create horizontal sprite sheets (128x32) in assets/sprites/characters/ and assets/sprites/enemies/
5. Maintain proper frame order (frame1, frame2, frame3, frame4)

File naming convention:
    {character_name}_frame{1-4}.png
    
Examples:
    player_hero_frame1.png -> assets/sprites/characters/player_hero.png
    enemy_goblin_frame1.png -> assets/sprites/enemies/enemy_goblin.png
"""

import os
import re
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
        self.frames_dir = self.project_root / "assets" / "sprites" / "frames"
        self.characters_dir = self.project_root / "assets" / "sprites" / "characters"
        self.enemies_dir = self.project_root / "assets" / "sprites" / "enemies"
        
        # Create output directories if they don't exist
        self.characters_dir.mkdir(parents=True, exist_ok=True)
        self.enemies_dir.mkdir(parents=True, exist_ok=True)
        
        print(f"Project root: {self.project_root}")
        print(f"Frames directory: {self.frames_dir}")
    
    def find_frame_groups(self):
        """Find all frame files and group them by character name."""
        if not self.frames_dir.exists():
            print(f"❌ Frames directory not found: {self.frames_dir}")
            return {}
        
        frame_pattern = re.compile(r'^(.+)_frame([1-4])\.png$')
        groups = {}
        
        for file_path in self.frames_dir.glob("*.png"):
            match = frame_pattern.match(file_path.name)
            if match:
                character_name = match.group(1)
                frame_number = int(match.group(2))
                
                if character_name not in groups:
                    groups[character_name] = {}
                
                groups[character_name][frame_number] = file_path
        
        return groups
    
    def validate_frame_group(self, character_name, frames):
        """Check if all 4 frames are present for a character."""
        missing_frames = []
        for i in range(1, 5):
            if i not in frames:
                missing_frames.append(f"frame{i}")
        
        if missing_frames:
            print(f"⚠️  {character_name}: Missing frames {missing_frames}")
            return False
        
        return True
    
    def create_sprite_sheet(self, character_name, frames):
        """Merge 4 frames into a horizontal sprite sheet."""
        # Create new image: 128x32 (4 frames of 32x32)
        sprite_sheet = Image.new('RGBA', (128, 32), (0, 0, 0, 0))
        
        try:
            for frame_num in range(1, 5):
                frame_path = frames[frame_num]
                frame_image = Image.open(frame_path)
                
                # Verify frame is 32x32
                if frame_image.size != (32, 32):
                    print(f"⚠️  {frame_path.name} is {frame_image.size}, expected (32, 32)")
                    # Resize if needed using LANCZOS resampling
                    from PIL.Image import Resampling
                    frame_image = frame_image.resize((32, 32), Resampling.LANCZOS)
                
                # Paste frame into sprite sheet
                x_offset = (frame_num - 1) * 32
                sprite_sheet.paste(frame_image, (x_offset, 0))
            
            return sprite_sheet
            
        except Exception as e:
            print(f"❌ Error creating sprite sheet for {character_name}: {e}")
            return None
    
    def save_sprite_sheet(self, character_name, sprite_sheet):
        """Save sprite sheet to appropriate directory."""
        if character_name.startswith('player_'):
            output_dir = self.characters_dir
            filename = f"{character_name}.png"
        elif character_name.startswith('enemy_'):
            output_dir = self.enemies_dir
            filename = f"{character_name}.png"
        else:
            print(f"⚠️  Unknown character type: {character_name}")
            return False
        
        output_path = output_dir / filename
        
        try:
            sprite_sheet.save(output_path, "PNG")
            print(f"✅ Created: {output_path.relative_to(self.project_root)}")
            return True
        except Exception as e:
            print(f"❌ Error saving {output_path}: {e}")
            return False
    
    def merge_all(self):
        """Find all frame groups and merge them into sprite sheets."""
        print("🎨 Sirius RPG Sprite Sheet Merger")
        print("=" * 40)
        
        frame_groups = self.find_frame_groups()
        
        if not frame_groups:
            print("❌ No frame files found!")
            print(f"Expected files like: {self.frames_dir}/player_hero_frame1.png")
            return
        
        print(f"📁 Found {len(frame_groups)} character(s) with frames:")
        for name in frame_groups.keys():
            print(f"   - {name}")
        print()
        
        success_count = 0
        total_count = len(frame_groups)
        
        for character_name, frames in frame_groups.items():
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
