# Cross-Scene Stair Linking Guide

## The Problem
**NodePath can only link to nodes in the same scene file.** You cannot use the NodePath picker to select a stair in a different floor scene.

## The Solution: StairId Matching

Use **Stair IDs** to link stairs across different scenes. The system automatically finds and links stairs with matching IDs.

## How It Works

### 1. Give Each Stair a Unique ID

**Ground Floor (FloorGF.tscn):**
```
StairConnection: "StairToFloor1F"
├─ Stair ID: "gf_to_1f"           ← Unique identifier
├─ Direction: Up
├─ Target Floor: 1
└─ Grid Position: (13, 3)
```

**First Floor (Floor1F.tscn):**
```
StairConnection: "StairToGroundFloor"
├─ Stair ID: "1f_to_gf"           ← Unique identifier
├─ Direction: Down
├─ Target Floor: 0
└─ Grid Position: (17, 13)
```

### 2. The System Auto-Links Them

At runtime:
1. All StairConnections register with FloorManager using their **Stair ID**
2. When transitioning floors, the system looks up stairs by **Target Floor + Direction**
3. Player spawns at the correct stair position automatically

## Three Ways to Configure Destinations

### Method 1: Auto-Matching (Easiest)
Just set **Target Floor** and **Direction**. The system finds the matching stair automatically.

```
Ground Floor Stair:
├─ Direction: Up
├─ Target Floor: 1
└─ (System finds Down stair on Floor 1)

First Floor Stair:  
├─ Direction: Down
├─ Target Floor: 0
└─ (System finds Up stair on Ground Floor)
```

✅ **No IDs needed for simple cases!**

### Method 2: Stair ID Reference (Cross-Scene)
Give stairs unique IDs and they'll find each other:

```
Ground Floor:
└─ Stair ID: "gf_main_entrance"

First Floor:
└─ Stair ID: "1f_main_entrance"
```

At runtime, stairs with complementary directions and target floors will auto-match.

### Method 3: Custom Destination (Manual Override)
For special cases, use custom coordinates:

```
StairConnection:
├─ Use Custom Destination: ✓
└─ Custom Destination: (20, 15)
```

## Quick Setup Checklist

For each stair pair:

**Stair 1 (Lower Floor):**
- ✅ Direction: **Up**
- ✅ Target Floor: **1** (or higher floor number)
- ✅ Optional: Stair ID (e.g., "gf_to_1f")

**Stair 2 (Upper Floor):**
- ✅ Direction: **Down**
- ✅ Target Floor: **0** (or lower floor number)
- ✅ Optional: Stair ID (e.g., "1f_to_gf")

**The system automatically links them based on Target Floor + Direction!**

## Example: Three Floors

**Ground Floor → First Floor:**
```
GF Stair: Up → Floor 1
1F Stair: Down → Floor 0
```

**First Floor → Second Floor:**
```
1F Stair: Up → Floor 2
2F Stair: Down → Floor 1
```

**Multiple Stairs Per Floor:**
```
Ground Floor:
├─ Stair A: Up → Floor 1, ID: "gf_stair_a"
└─ Stair B: Up → Floor 1, ID: "gf_stair_b"

First Floor:
├─ Stair A: Down → Floor 0, ID: "1f_stair_a"
└─ Stair B: Down → Floor 0, ID: "1f_stair_b"
```

Use different spawn positions or custom destinations to control where players arrive.

## Why Not NodePath?

**Godot Limitation:** NodePath only works within the same `.tscn` file. Since each floor is a separate scene file (`FloorGF.tscn`, `Floor1F.tscn`), they can't reference each other's nodes directly in the editor.

**Our Solution:** The StairId registry system acts as a "global phonebook" that stairs use to find each other at runtime, across different scenes.

## Troubleshooting

**Stairs not linking?**
- Check **Target Floor** numbers are correct (0 = Ground Floor, 1 = First Floor, etc.)
- Check **Direction** is opposite (Up on lower floor, Down on upper floor)
- Ensure both floors are loaded (stairs register when their floor loads)

**Landing in wrong position?**
- Check **Grid Position** of the destination stair
- Or enable **Use Custom Destination** and set exact coordinates

**Want to debug?**
- Enable **Enable Debug Logging** on GridMap nodes
- Look for "🪜 Found X StairConnection nodes" messages
- Look for "📝 Registered stair" messages
- Look for "🔄 Auto-matched stair" messages
