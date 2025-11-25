# Stair Connection Setup Guide

## Visual Stair Configuration (No Manual Coordinates!)

Instead of manually entering coordinates in `.tres` files, you can now use **StairConnection nodes** directly in your scene to visually configure floor transitions.

## How to Add Stairs

### Step 1: Open Your Floor Scene
Open `FloorGF.tscn` or `Floor1F.tscn` in Godot.

### Step 2: Add a StairConnection Node
1. Select the **GridMap** node in the scene tree
2. Click the **+** button to add a child node
3. Search for **StairConnection** and add it
4. Rename it to something descriptive like `StairToFloor1F`

### Step 3: Position the StairConnection
1. Select the StairConnection node
2. In the 2D viewport, **drag it to the stair tile position**
3. The node will automatically calculate its grid position from world coordinates
4. You'll see a visual indicator (circle with arrow) showing the stair direction

### Step 4: Configure the Stair Properties

In the Inspector panel, you'll see these properties:

```
StairConnection
├─ Grid Position: (13, 3)           ← Auto-calculated from world position
├─ Direction: Up                     ← Choose "Up" or "Down"
├─ Target Floor: 1                   ← Which floor to transition to (0=GF, 1=1F, etc.)
├─ Use Custom Destination: false     ← Enable to specify exact spawn position
├─ Custom Destination: (0, 0)        ← Only used if above is true
└─ Indicator Color: (0, 1, 1, 0.5)   ← Visual color in editor
```

**Simple Setup (Automatic Matching):**
- Set **Direction** to `Up` or `Down`
- Set **Target Floor** to the floor number you want to go to
- Leave **Use Custom Destination** unchecked
- The system will automatically find a matching stair on the target floor

**Advanced Setup (Custom Spawn Position):**
- Check **Use Custom Destination**
- Set **Custom Destination** to the exact grid coordinates where you want the player to spawn
- Useful when you want asymmetric stair placement

### Step 5: Repeat for Other Floors
Add corresponding StairConnection nodes on the target floor:
- If you have a "Stair Up" on Ground Floor → Add a "Stair Down" on First Floor
- Position them where you want the player to arrive

## Example Setup

### Ground Floor (FloorGF.tscn)
```
GridMap
├─ GroundLayer
├─ WallLayer
├─ StairLayer
└─ StairToFloor1F (StairConnection)
    ├─ Grid Position: (13, 3)
    ├─ Direction: Up
    ├─ Target Floor: 1
    └─ Use Custom Destination: false
```

### First Floor (Floor1F.tscn)
```
GridMap
├─ GroundLayer
├─ WallLayer
├─ StairLayer
└─ StairToGroundFloor (StairConnection)
    ├─ Grid Position: (17, 13)
    ├─ Direction: Down
    ├─ Target Floor: 0
    └─ Use Custom Destination: false
```

## How It Works at Runtime

1. When the floor loads, `GridMap.RegisterStairConnections()` scans for all StairConnection nodes
2. It automatically populates the FloorDefinition's stair arrays:
   - `StairsUp` / `StairsDown` - positions of stair tiles
   - `StairsUpDestinations` / `StairsDownDestinations` - where to spawn
3. When the player steps on a stair tile, automatic transition happens
4. Player spawns at the corresponding stair position on the target floor

## Visual Indicators

In the Godot editor, StairConnection nodes display:
- **Circle** - marks the stair location
- **Arrow** - shows direction (up/down)
- **Label** - shows target floor

Colors:
- **Cyan (default)** - normal stair
- **Custom colors** - set via Indicator Color property

## Benefits

✅ **No manual coordinate entry** - just drag and position  
✅ **Visual feedback** - see exactly where stairs are  
✅ **Automatic registration** - no need to edit `.tres` files  
✅ **Flexible** - supports both automatic matching and custom destinations  
✅ **Editor-friendly** - all configuration in Inspector panel  

## Troubleshooting

**Stair not working?**
- Check that StairConnection is a **direct child of GridMap**
- Verify **Target Floor** number is correct (0-based: 0=GF, 1=1F, etc.)
- Ensure **Direction** matches (Up on lower floor, Down on upper floor)
- Check console logs for `🪜 Found X StairConnection nodes` message

**Wrong spawn position?**
- Enable **Use Custom Destination** and set exact coordinates
- Or reposition the corresponding StairConnection on the target floor

**Visual indicator not showing?**
- Make sure you're in the Godot editor (not runtime)
- Check that **Indicator Color** has some alpha (not fully transparent)
