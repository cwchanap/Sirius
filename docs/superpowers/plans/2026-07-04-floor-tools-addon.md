# Sirius Floor Tools Editor Addon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Python-first floor generation workflow with a Godot editor addon + headless CLI backed by reusable, parity-gated C# services.

**Architecture:** `FloorGenerationService` ports the Python maze builders verbatim into C#, producing the existing `FloorJsonModel` DTO. One in-memory model fans out to scene writes (existing `TilemapJsonImporter`), typed `FloorDefinition.tres` updates (`FloorResourceSyncService`), and JSON serialization. Core services are editor-API-free (run under `--headless`); the dock is thin glue.

**Tech Stack:** Godot 4.6.2, C# / .NET 8.0, GdUnit4 tests, single `Sirius.csproj` assembly.

## Global Constraints

- Exact parity is required: generated GF/1F/2F/3F output must reproduce the current Python output cell-for-cell and entity-for-entity. The gate is deserialize-and-deep-equal against the committed `scenes/game/floors/Floor*.json` (semantic, not byte-for-byte). Committed JSON is regenerated from C# after the gate passes.
- Core services under `scripts/floor_tools/` (namespace `Sirius.FloorTools`) must NOT reference `EditorInterface` — they run identically under `--headless`. Only dock code under `addons/sirius_floor_tools/` may use editor APIs.
- Test files mirror source: `scripts/floor_tools/...` → `tests/floor_tools/...`. Tests use GdUnit4 `[TestSuite]`/`[TestCase]` with `using static GdUnit4.Assertions;` and `AssertThat(...)`.
- Build: `dotnet build Sirius.sln`. Tests: `dotnet test Sirius.sln --settings test.runsettings.local`.
- Floor scene node path is fixed: root `Floor{N}F` → child `GridMap` (type `Node2D` with `GridMap.cs` script) → `GroundLayer`/`WallLayer`/`StairLayer` (`TileMapLayer`).
- `config/tile_mapping.json` maps tile names → source IDs; generation emits tile *names* (e.g. `"starting_area"`, `"generic"`, `"up"`, `"down"`), never source IDs.

---

## File Structure

```
scripts/floor_tools/
  MazeBuilder.cs              carve ops + perimeter (port of Python MazeBuilder)
  FloorGraph.cs               walkable/connected/neighbors/dead-ends (shared by gen + validation)
  FloorEntityBuilders.cs      treasure/trap/switch/gate/riddle list builders + position extractors
  SupplementalEnemyPlanner.cs deterministic supplemental-enemy placement (port of build_supplemental_enemy_patrols)
  layouts/Floor0Layout.cs     GF constants (verbatim from floor0_maze_generator.py)
  layouts/Floor1Layout.cs     1F constants (verbatim from floor1_maze_generator.py)
  layouts/Floor2Layout.cs     2F constants
  layouts/Floor3Layout.cs     3F constants
  FloorGenerationService.cs   Generate(n) + per-floor build methods
  FloorValidationService.cs   Validate -> ValidationResult
  ValidationResult.cs         issues list
  ValidationIssue.cs          Severity/Code/Message
  FloorResourceSyncService.cs typed FloorDefinition .tres updates
  FloorSceneWriter.cs         orchestrator: model -> importer + sync + pack
  FloorRegistry.cs            floor number -> .tscn/.tres/.json paths
  FloorCli.cs                 static entry called by generate_floor.gd

addons/sirius_floor_tools/
  plugin.cfg
  SiriusFloorToolsPlugin.cs   [Tool] EditorPlugin
  SiriusFloorToolsDock.tscn   dock UI
  SiriusFloorToolsDock.cs     [Tool] dock controller

tools/generate_floor.gd       headless entry

tests/floor_tools/
  FloorModelAsserter.cs       semantic deep-equal helper (shared by parity tests)
  MazeBuilderTest.cs
  FloorGraphTest.cs
  SupplementalEnemyPlannerTest.cs
  FloorGenerationParityTest.cs
  FloorValidationServiceTest.cs
  FloorResourceSyncServiceTest.cs
  FloorSceneWriterTest.cs
  FloorRegistryTest.cs
```

`FloorJsonModel.cs` (existing) gains a `HiddenPlaceholders` entity list (Task 1 — DTO gap fix).

---

## Task 1: Add `hidden_placeholders` to FloorJsonModel

The committed `Floor1F.json`/`Floor2F.json` contain `entities.hidden_placeholders`. `FloorJsonModel.cs` currently lacks this property, so deserialization drops it and generation can never reproduce it. This DTO gap must be closed first or every 1F/2F parity test will fail.

**Files:**
- Modify: `scripts/tilemap_json/FloorJsonModel.cs`
- Test: `tests/tilemap_json/FloorJsonModelTest.cs`

**Interfaces:**
- Produces: `HiddenPlaceholderData` type and `SceneEntities.HiddenPlaceholders` property (consumed by Tasks 7-10 generation/validation; ignored by the importer — hidden placeholders are JSON/validation data, not scene nodes, matching current behavior).

- [ ] **Step 1: Write the failing test**

Add to `tests/tilemap_json/FloorJsonModelTest.cs`:

```csharp
[TestCase]
public void TestHiddenPlaceholdersRoundTrip()
{
    var model = new FloorJsonModel
    {
        Metadata = new FloorMetadata { FloorName = "Test" },
        Entities = new SceneEntities
        {
            HiddenPlaceholders = new List<HiddenPlaceholderData>
            {
                new() { Id = "hidden_north", Position = new Vector2IData(16, 8) }
            }
        }
    };

    string json = model.ToJson();
    var parsed = FloorJsonModel.FromJson(json);

    AssertThat(parsed.Entities.HiddenPlaceholders).IsNotNull();
    AssertThat(parsed.Entities.HiddenPlaceholders.Count).IsEqual(1);
    AssertThat(parsed.Entities.HiddenPlaceholders[0].Id).IsEqual("hidden_north");
    AssertThat(parsed.Entities.HiddenPlaceholders[0].Position.X).IsEqual(16);
    AssertThat(parsed.Entities.HiddenPlaceholders[0].Position.Y).IsEqual(8);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest.TestHiddenPlaceholdersRoundTrip"`
Expected: FAIL — `HiddenPlaceholderData` not defined / `HiddenPlaceholders` not a member of `SceneEntities`.

- [ ] **Step 3: Add the DTO type and property**

In `scripts/tilemap_json/FloorJsonModel.cs`, add the class (place near `StairConnectionData`):

```csharp
public class HiddenPlaceholderData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public Vector2IData Position { get; set; } = new();
}
```

Add the property to `SceneEntities` (declaration order does not matter — parity is semantic):

```csharp
[JsonPropertyName("hidden_placeholders")]
public List<HiddenPlaceholderData>? HiddenPlaceholders { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest.TestHiddenPlaceholdersRoundTrip"`
Expected: PASS.

- [ ] **Step 5: Run the full FloorJsonModel suite to confirm no regressions**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest"`
Expected: all PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/tilemap_json/FloorJsonModel.cs tests/tilemap_json/FloorJsonModelTest.cs
git commit -m "feat: add hidden_placeholders to FloorJsonModel DTO"
```

---

## Task 2: FloorRegistry

A small static map from floor number to scene/def/JSON paths, shared by the dock, CLI, and tests.

**Files:**
- Create: `scripts/floor_tools/FloorRegistry.cs`
- Test: `tests/floor_tools/FloorRegistryTest.cs`

**Interfaces:**
- Produces: `FloorRegistry.Get(int floorNumber) -> FloorPaths`, `FloorRegistry.AllFloors -> IReadOnlyList<int>` (consumed by Tasks 12-15).

- [ ] **Step 1: Write the failing test**

`tests/floor_tools/FloorRegistryTest.cs`:

```csharp
using GdUnit4;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FloorRegistryTest
{
    [TestCase]
    public void TestGroundFloorPaths()
    {
        var p = FloorRegistry.Get(0);
        AssertThat(p.ScenePath).IsEqual("res://scenes/game/floors/FloorGF.tscn");
        AssertThat(p.DefPath).IsEqual("res://resources/floors/FloorGF.tres");
        AssertThat(p.JsonPath).IsEqual("res://scenes/game/floors/FloorGF.json");
    }

    [TestCase]
    public void TestFloor1Paths()
    {
        var p = FloorRegistry.Get(1);
        AssertThat(p.ScenePath).IsEqual("res://scenes/game/floors/Floor1F.tscn");
        AssertThat(p.DefPath).IsEqual("res://resources/floors/Floor1F.tres");
    }

    [TestCase]
    public void TestAllFloors()
    {
        AssertThat(FloorRegistry.AllFloors).IsEqual(new int[] { 0, 1, 2, 3 });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorRegistryTest"`
Expected: FAIL — namespace/type not found.

- [ ] **Step 3: Implement FloorRegistry**

`scripts/floor_tools/FloorRegistry.cs`:

```csharp
using System.Collections.Generic;

namespace Sirius.FloorTools;

public record FloorPaths(string ScenePath, string DefPath, string JsonPath);

public static class FloorRegistry
{
    private static readonly Dictionary<int, FloorPaths> _floors = new()
    {
        [0] = new FloorPaths(
            "res://scenes/game/floors/FloorGF.tscn",
            "res://resources/floors/FloorGF.tres",
            "res://scenes/game/floors/FloorGF.json"),
        [1] = new FloorPaths(
            "res://scenes/game/floors/Floor1F.tscn",
            "res://resources/floors/Floor1F.tres",
            "res://scenes/game/floors/Floor1F.json"),
        [2] = new FloorPaths(
            "res://scenes/game/floors/Floor2F.tscn",
            "res://resources/floors/Floor2F.tres",
            "res://scenes/game/floors/Floor2F.json"),
        [3] = new FloorPaths(
            "res://scenes/game/floors/Floor3F.tscn",
            "res://resources/floors/Floor3F.tres",
            "res://scenes/game/floors/Floor3F.json"),
    };

    public static IReadOnlyList<int> AllFloors { get; } = new List<int> { 0, 1, 2, 3 };

    public static FloorPaths Get(int floorNumber)
    {
        if (_floors.TryGetValue(floorNumber, out var paths))
            return paths;
        throw new System.ArgumentException($"Unknown floor number: {floorNumber}");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorRegistryTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/floor_tools/FloorRegistry.cs tests/floor_tools/FloorRegistryTest.cs
git commit -m "feat: add FloorRegistry path lookup"
```

---

## Task 3: Addon skeleton (plugin enableable)

Satisfies AC #1: a `Sirius Floor Tools` plugin appears in Project Settings > Plugins and can be enabled after building. Buttons are non-functional placeholders.

**Files:**
- Create: `addons/sirius_floor_tools/plugin.cfg`
- Create: `addons/sirius_floor_tools/SiriusFloorToolsPlugin.cs`
- Create: `addons/sirius_floor_tools/SiriusFloorToolsDock.tscn`
- Create: `addons/sirius_floor_tools/SiriusFloorToolsDock.cs`
- Modify: `project.godot` (enable plugin by setting it in Project Settings; the `[editor_plugins]` enabled list)

**Interfaces:**
- Produces: `SiriusFloorToolsPlugin` (EditorPlugin), `SiriusFloorToolsDock` (Control). Dock buttons named `GenerateButton`, `ValidateButton`, `ExportJsonButton`, `ImportJsonButton`, `BakeSaveButton`, `FloorOption` (OptionButton), `ResultsLabel` (RichTextLabel) — wired in Task 13.

- [ ] **Step 1: Create plugin.cfg**

`addons/sirius_floor_tools/plugin.cfg`:

```ini
[plugin]

name="Sirius Floor Tools"
description="Generate, validate, bake, and import/export Sirius floors in-editor"
author="Sirius"
version="0.1.0"
script="SiriusFloorToolsPlugin.cs"
```

- [ ] **Step 2: Create the dock controller (placeholder)**

`addons/sirius_floor_tools/SiriusFloorToolsDock.cs`:

```csharp
using Godot;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsDock : Control
{
    private OptionButton _floorOption;
    private RichTextLabel _resultsLabel;

    public override void _Ready()
    {
        _floorOption = GetNodeOrNull<OptionButton>("%FloorOption");
        _resultsLabel = GetNodeOrNull<RichTextLabel>("%ResultsLabel");
        Log("Sirius Floor Tools ready. Buttons wire up in a later task.");
    }

    public void Log(string message)
    {
        if (_resultsLabel != null)
            _resultsLabel.AddText(message + "\n");
    }
}
```

- [ ] **Step 3: Create the dock scene**

Build `SiriusFloorToolsDock.tscn` as a `Control` (script `SiriusFloorToolsDock.cs`) containing a `VBoxContainer` with:
- `OptionButton` named `FloorOption` with `unique_name_in_owner = true` (so `%FloorOption` resolves); items: GF, 1F, 2F, 3F.
- Four `Button`s with `unique_name_in_owner = true`: `GenerateButton` ("Generate"), `ValidateButton` ("Validate"), `ExportJsonButton` ("Export JSON"), `ImportJsonButton` ("Import JSON"), `BakeSaveButton` ("Bake / Save Scene").
- `RichTextLabel` named `ResultsLabel` with `unique_name_in_owner = true`, `bbcode_enabled = true`, `scroll_following = true`, min size `Vector2(320, 200)`.

Create this scene in the Godot editor (or hand-author the `.tscn`). Verify it opens without errors.

- [ ] **Step 4: Create the EditorPlugin**

`addons/sirius_floor_tools/SiriusFloorToolsPlugin.cs`:

```csharp
using Godot;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsPlugin : EditorPlugin
{
    private SiriusFloorToolsDock _dock;

    public override void _EnterTree()
    {
        var dockScene = GD.Load<PackedScene>("res://addons/sirius_floor_tools/SiriusFloorToolsDock.tscn");
        if (dockScene == null)
        {
            GD.PrintErr("[SiriusFloorTools] Dock scene not found");
            return;
        }
        _dock = dockScene.Instantiate<SiriusFloorToolsDock>();
        AddControlToDock(DockSlot.LeftUl, _dock);
    }

    public override void _ExitTree()
    {
        if (_dock != null)
        {
            RemoveControlFromDocks(_dock);
            _dock.Free();
            _dock = null;
        }
    }
}
```

- [ ] **Step 5: Build and enable**

Run: `dotnet build Sirius.sln`
Expected: build succeeds.

Open the Godot editor → Project → Project Settings → Plugins → enable "Sirius Floor Tools". Verify the dock appears on the left. Confirm `project.godot` now lists the plugin under `[editor_plugins]` (enabled=true).

- [ ] **Step 6: Commit**

```bash
git add addons/sirius_floor_tools project.godot
git commit -m "feat: add Sirius Floor Tools editor plugin skeleton"
```

---

## Task 4: MazeBuilder (carve + perimeter)

Port of the Python `MazeBuilder` class. The wall set starts as every cell in the footprint; carving removes cells; `ReinforcePerimeter` adds border walls.

**Files:**
- Create: `scripts/floor_tools/MazeBuilder.cs`
- Test: `tests/floor_tools/MazeBuilderTest.cs`

**Interfaces:**
- Produces: `MazeBuilder` with `CarveCell`, `CarveRect`, `CarveHCorridor`, `CarveVCorridor`, `CarvePath`, `CarveLoop`, `ReinforcePerimeter`, `Walls` (`HashSet<Vector2I>`), constructor `MazeBuilder(int width, int height)` (consumed by Tasks 6-9).

- [ ] **Step 1: Write the failing test**

`tests/floor_tools/MazeBuilderTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
public partial class MazeBuilderTest
{
    [TestCase]
    public void TestStartsFullOfWalls()
    {
        var builder = new MazeBuilder(10, 10);
        // Footprint interior cells are walls until carved (border always wall).
        AssertThat(builder.Walls.Contains(new Vector2I(5, 5))).IsTrue();
        AssertThat(builder.Walls.Count).IsEqual(100);
    }

    [TestCase]
    public void TestCarveCellRemovesFromWalls()
    {
        var builder = new MazeBuilder(10, 10);
        builder.CarveCell(5, 5);
        AssertThat(builder.Walls.Contains(new Vector2I(5, 5))).IsFalse();
    }

    [TestCase]
    public void TestCarveRect()
    {
        var builder = new MazeBuilder(10, 10);
        builder.CarveRect(3, 3, 5, 5);
        AssertThat(builder.Walls.Contains(new Vector2I(4, 4))).IsFalse();
        AssertThat(builder.Walls.Contains(new Vector2I(2, 4))).IsTrue(); // outside rect
    }

    [TestCase]
    public void TestReinforcePerimeter()
    {
        var builder = new MazeBuilder(10, 10);
        builder.CarveRect(0, 0, 9, 9); // carve everything including border
        builder.ReinforcePerimeter();
        AssertThat(builder.Walls.Contains(new Vector2I(0, 0))).IsTrue();
        AssertThat(builder.Walls.Contains(new Vector2I(9, 9))).IsTrue();
        AssertThat(builder.Walls.Contains(new Vector2I(5, 5))).IsFalse(); // interior stays carved
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MazeBuilderTest"`
Expected: FAIL — `MazeBuilder` not found.

- [ ] **Step 3: Implement MazeBuilder**

Port verbatim from `tools/floor0_maze_generator.py:54-147` (the Python `MazeBuilder`). `scripts/floor_tools/MazeBuilder.cs`:

```csharp
using Godot;
using System.Collections.Generic;

namespace Sirius.FloorTools;

public class MazeBuilder
{
    public int Width { get; }
    public int Height { get; }
    public HashSet<Vector2I> Walls { get; } = new();

    public MazeBuilder(int width, int height)
    {
        Width = width;
        Height = height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Walls.Add(new Vector2I(x, y));
    }

    public void CarveCell(int x, int y)
    {
        if (x >= 1 && x < Width - 1 && y >= 1 && y < Height - 1)
            Walls.Remove(new Vector2I(x, y));
    }

    public void CarveRect(int x1, int y1, int x2, int y2)
    {
        int left = System.Math.Min(x1, x2);
        int right = System.Math.Max(x1, x2);
        int top = System.Math.Min(y1, y2);
        int bottom = System.Math.Max(y1, y2);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                CarveCell(x, y);
    }

    public void CarveHCorridor(int x1, int x2, int y, int halfWidth = 1)
    {
        int left = System.Math.Min(x1, x2);
        int right = System.Math.Max(x1, x2);
        for (int x = left; x <= right; x++)
            for (int dy = -halfWidth; dy <= halfWidth; dy++)
                CarveCell(x, y + dy);
    }

    public void CarveVCorridor(int y1, int y2, int x, int halfWidth = 1)
    {
        int top = System.Math.Min(y1, y2);
        int bottom = System.Math.Max(y1, y2);
        for (int y = top; y <= bottom; y++)
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
                CarveCell(x + dx, y);
    }

    public void CarvePath(Vector2I start, Vector2I end, int halfWidth = 1)
    {
        CarveHCorridor(start.X, end.X, start.Y, halfWidth);
        CarveVCorridor(start.Y, end.Y, end.X, halfWidth);
    }

    public void CarveLoop(IReadOnlyList<Vector2I> points, int halfWidth = 1)
    {
        for (int i = 0; i < points.Count - 1; i++)
            CarvePath(points[i], points[i + 1], halfWidth);
    }

    public void ReinforcePerimeter()
    {
        for (int x = 0; x < Width; x++)
        {
            Walls.Add(new Vector2I(x, 0));
            Walls.Add(new Vector2I(x, Height - 1));
        }
        for (int y = 0; y < Height; y++)
        {
            Walls.Add(new Vector2I(0, y));
            Walls.Add(new Vector2I(Width - 1, y));
        }
    }
}
```

Note: `Vector2I` is `Godot.Vector2I`; the file already has `using Godot;` so use the unqualified name.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MazeBuilderTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/floor_tools/MazeBuilder.cs tests/floor_tools/MazeBuilderTest.cs
git commit -m "feat: port MazeBuilder carve operations"
```

---

## Task 5: FloorGraph (walkable/connected/neighbors/dead-ends)

Shared graph helpers used by both generation (supplemental-enemy planning) and validation. Pure functions over `HashSet<Vector2I>`.

**Files:**
- Create: `scripts/floor_tools/FloorGraph.cs`
- Test: `tests/floor_tools/FloorGraphTest.cs`

**Interfaces:**
- Produces: `FloorGraph.WalkableCellsFromWalls`, `ConnectedCells`, `WalkableNeighbors`, `WalkableNeighborCount`, `DeadEndBranches` (consumed by Tasks 6, 10).

- [ ] **Step 1: Write the failing test**

`tests/floor_tools/FloorGraphTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.FloorTools;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FloorGraphTest
{
    [TestCase]
    public void TestConnectedCellsBFS()
    {
        var walkable = new HashSet<Vector2I>
        {
            new(0, 0), new(1, 0), new(2, 0), new(5, 5) // (5,5) disconnected
        };
        var connected = FloorGraph.ConnectedCells(walkable, new Vector2I(0, 0));
        AssertThat(connected.Contains(new Vector2I(2, 0))).IsTrue();
        AssertThat(connected.Contains(new Vector2I(5, 5))).IsFalse();
    }

    [TestCase]
    public void TestWalkableNeighborCount()
    {
        var walkable = new HashSet<Vector2I> { new(0, 0), new(1, 0), new(0, 1) };
        AssertThat(FloorGraph.WalkableNeighborCount(walkable, new Vector2I(0, 0))).IsEqual(2);
    }

    [TestCase]
    public void TestDeadEndBranchesFindsLeaf()
    {
        // corridor: (0,0)-(1,0)-(2,0)-(3,0); (0,0) is a leaf with 1 neighbor
        var walkable = new HashSet<Vector2I>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0)
        };
        var branches = FloorGraph.DeadEndBranches(walkable, 4, 1);
        AssertThat(branches.Count >= 1).IsTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGraphTest"`
Expected: FAIL — `FloorGraph` not found.

- [ ] **Step 3: Implement FloorGraph**

Port verbatim from `tools/floor1_maze_generator.py:1007-1078` (`walkable_cells`, `connected_walkable_cells`, `walkable_neighbors`, `walkable_neighbor_count`, `dead_end_branches`). `scripts/floor_tools/FloorGraph.cs`:

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public static class FloorGraph
{
    private static readonly Vector2I[] Directions =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    public static HashSet<Vector2I> WalkableCellsFromWalls(HashSet<Vector2I> walls, int width, int height)
    {
        var walkable = new HashSet<Vector2I>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2I(x, y);
                if (!walls.Contains(cell))
                    walkable.Add(cell);
            }
        return walkable;
    }

    public static HashSet<Vector2I> ConnectedCells(HashSet<Vector2I> walkable, Vector2I start)
    {
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        var seen = new HashSet<Vector2I> { start };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dir in Directions)
            {
                var next = current + dir;
                if (walkable.Contains(next) && !seen.Contains(next))
                {
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return seen;
    }

    public static List<Vector2I> WalkableNeighbors(HashSet<Vector2I> walkable, Vector2I position)
    {
        var result = new List<Vector2I>();
        foreach (var dir in Directions)
        {
            var next = position + dir;
            if (walkable.Contains(next))
                result.Add(next);
        }
        return result;
    }

    public static int WalkableNeighborCount(HashSet<Vector2I> walkable, Vector2I position)
        => WalkableNeighbors(walkable, position).Count;

    public static List<List<Vector2I>> DeadEndBranches(HashSet<Vector2I> walkable, int width, int height)
    {
        var branches = new List<List<Vector2I>>();
        var orderedLeaves = walkable.OrderBy(c => c.X).ThenBy(c => c.Y);
        foreach (var leaf in orderedLeaves)
        {
            if (leaf.X >= width || leaf.Y >= height)
                continue;
            if (WalkableNeighborCount(walkable, leaf) != 1)
                continue;

            var branch = new List<Vector2I> { leaf };
            Vector2I? previous = null;
            var current = leaf;
            while (true)
            {
                var nextCells = WalkableNeighbors(walkable, current)
                    .Where(n => n != previous).ToList();
                if (nextCells.Count == 0)
                    break;
                var nextCell = nextCells[0];
                if (WalkableNeighborCount(walkable, nextCell) != 2)
                    break;
                branch.Add(nextCell);
                previous = current;
                current = nextCell;
            }
            branches.Add(branch);
        }
        return branches;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGraphTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/floor_tools/FloorGraph.cs tests/floor_tools/FloorGraphTest.cs
git commit -m "feat: port FloorGraph walkable/dead-end helpers"
```

---

## Task 6: SupplementalEnemyPlanner (deterministic density)

Port of `build_supplemental_enemy_patrols`. This is the trickiest parity logic: a deterministic pseudo-random ordering + distance spreading + cycling enemy types. Must match Python exactly.

**Files:**
- Create: `scripts/floor_tools/SupplementalEnemyPlanner.cs`
- Test: `tests/floor_tools/SupplementalEnemyPlannerTest.cs`

**Interfaces:**
- Produces: `SupplementalEnemyPlanner.Plan(string prefix, Dictionary<string, EnemySpec> baseEnemies, HashSet<Vector2I> walkable, HashSet<Vector2I> occupied, IReadOnlyList<string> enemyTypes) -> Dictionary<string, EnemySpec>` where `EnemySpec = record(Vector2I Position, string EnemyType)` (consumed by Tasks 7-9).

- [ ] **Step 1: Write the failing test**

`tests/floor_tools/SupplementalEnemyPlannerTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.FloorTools;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
public partial class SupplementalEnemyPlannerTest
{
    [TestCase]
    public void TestProducesTargetCount()
    {
        // 2 base enemies, multiplier 3 => target = 2*(3-1) = 4 supplemental
        var baseEnemies = new Dictionary<string, EnemySpec>
        {
            ["a"] = new(new Vector2I(0, 0), "goblin"),
            ["b"] = new(new Vector2I(9, 9), "orc"),
        };
        var walkable = new HashSet<Vector2I>();
        for (int y = 1; y < 9; y++)
            for (int x = 1; x < 9; x++)
                walkable.Add(new Vector2I(x, y));
        var occupied = new HashSet<Vector2I> { new(0, 0), new(9, 9) };
        var types = new List<string> { "goblin", "orc", "skeleton_warrior", "forest_spirit" };

        var result = SupplementalEnemyPlanner.Plan("Patrol", baseEnemies, walkable, occupied, types);

        AssertThat(result.Count).IsEqual(4);
        // IDs are 1-based zero-padded to 3 digits
        AssertThat(result.ContainsKey("Patrol_001")).IsTrue();
        AssertThat(result.ContainsKey("Patrol_004")).IsTrue();
        // types cycle by index
        AssertThat(result["Patrol_001"].EnemyType).IsEqual("goblin");
        AssertThat(result["Patrol_002"].EnemyType).IsEqual("orc");
    }

    [TestCase]
    public void TestDeterministicAcrossCalls()
    {
        var baseEnemies = new Dictionary<string, EnemySpec> { ["a"] = new(new Vector2I(1, 1), "goblin") };
        var walkable = new HashSet<Vector2I>();
        for (int y = 1; y < 12; y++)
            for (int x = 1; x < 12; x++)
                walkable.Add(new Vector2I(x, y));
        var occupied = new HashSet<Vector2I> { new(1, 1) };
        var types = new List<string> { "goblin", "orc" };

        var r1 = SupplementalEnemyPlanner.Plan("P", baseEnemies, walkable, occupied, types);
        var r2 = SupplementalEnemyPlanner.Plan("P", baseEnemies, walkable, occupied, types);
        AssertThat(r1.Count).IsEqual(r2.Count);
        foreach (var kv in r1)
            AssertThat(r2[kv.Key].Position).IsEqual(kv.Value.Position);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SupplementalEnemyPlannerTest"`
Expected: FAIL — type not found.

- [ ] **Step 3: Implement the planner**

Port verbatim from `tools/floor1_maze_generator.py:406-454`. Note the exact sort key `(x*73 + y*37) % 997` and the distance-spreading loop over `(4,3,2,1)`. `scripts/floor_tools/SupplementalEnemyPlanner.cs`:

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public record EnemySpec(Vector2I Position, string EnemyType);

public static class SupplementalEnemyPlanner
{
    public const int DensityMultiplier = 3;

    public static Dictionary<string, EnemySpec> Plan(
        string prefix,
        Dictionary<string, EnemySpec> baseEnemies,
        HashSet<Vector2I> walkable,
        HashSet<Vector2I> occupied,
        IReadOnlyList<string> enemyTypes)
    {
        int targetCount = baseEnemies.Count * (DensityMultiplier - 1);
        var localOccupied = new HashSet<Vector2I>(occupied);
        var supplemental = new Dictionary<string, EnemySpec>();
        var selectedPositions = new List<Vector2I>();

        // Deterministic candidate ordering: ((x*73 + y*37) % 997, y, x)
        var candidates = walkable
            .OrderBy(p => ((p.X * 73 + p.Y * 37) % 997, p.Y, p.X))
            .Where(p => !localOccupied.Contains(p) && FloorGraph.WalkableNeighborCount(walkable, p) >= 2)
            .ToList();

        foreach (int minDistance in new[] { 4, 3, 2, 1 })
        {
            foreach (var position in candidates)
            {
                if (supplemental.Count == targetCount)
                    break;
                if (localOccupied.Contains(position))
                    continue;
                bool tooClose = selectedPositions.Any(selected =>
                    System.Math.Abs(position.X - selected.X) + System.Math.Abs(position.Y - selected.Y) < minDistance);
                if (tooClose)
                    continue;

                int index = supplemental.Count + 1;
                string id = $"{prefix}_{index:D3}";
                supplemental[id] = new EnemySpec(position, enemyTypes[(index - 1) % enemyTypes.Count]);
                localOccupied.Add(position);
                selectedPositions.Add(position);
            }
            if (supplemental.Count == targetCount)
                break;
        }

        if (supplemental.Count != targetCount)
            throw new System.Exception(
                $"Could only place {supplemental.Count} supplemental enemies for {prefix}; needed {targetCount}");

        return supplemental;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SupplementalEnemyPlannerTest"`
Expected: PASS. (If target count fails, the test's base-enemy multiplier math is `2*(3-1)=4` — confirm the walkable area yields ≥4 candidates with neighbor count ≥2.)

- [ ] **Step 5: Commit**

```bash
git add scripts/floor_tools/SupplementalEnemyPlanner.cs tests/floor_tools/SupplementalEnemyPlannerTest.cs
git commit -m "feat: port deterministic supplemental enemy planner"
```

---

## Task 7: FloorEntityBuilders + FloorModelAsserter

Entity-list builders (port of the Python `*_entities` helpers) and the shared semantic deep-equal asserter used by all parity tests. Doing these together gives the parity tests their comparison machinery before the first floor is generated.

**Files:**
- Create: `scripts/floor_tools/FloorEntityBuilders.cs`
- Create: `tests/floor_tools/FloorModelAsserter.cs`

**Interfaces:**
- Produces: `FloorEntityBuilders` static methods, `FloorModelAsserter.AssertModelsEqual(FloorJsonModel, FloorJsonModel)` (consumed by Tasks 8-11).

- [ ] **Step 1: Implement FloorEntityBuilders**

Port verbatim from `tools/floor1_maze_generator.py:315-382` and the position-extractors at 394-403. `scripts/floor_tools/FloorEntityBuilders.cs`:

```csharp
using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;

namespace Sirius.FloorTools;

public static class FloorEntityBuilders
{
    public static List<TreasureBoxData> TreasureBoxes(
        IEnumerable<(string Id, Vector2I Position, int Gold, Dictionary<string, int> Items)> boxes)
    {
        var result = new List<TreasureBoxData>();
        foreach (var (id, position, gold, items) in boxes)
        {
            var box = new TreasureBoxData
            {
                Id = id,
                Position = new Vector2IData(position),
                Gold = gold,
            };
            foreach (var (itemId, qty) in items)
                box.Items.Add(new TreasureBoxItemData { ItemId = itemId, Quantity = qty });
            result.Add(box);
        }
        return result;
    }

    public static List<TrapTileData> TrapTiles(
        IEnumerable<(string Id, Vector2I Position, int Damage, string StatusEffect, int Magnitude, int Turns)> traps,
        string puzzleId)
    {
        var result = new List<TrapTileData>();
        foreach (var (id, position, damage, effect, magnitude, turns) in traps)
        {
            result.Add(new TrapTileData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                Damage = damage,
                StatusEffect = effect,
                StatusMagnitude = magnitude,
                StatusTurns = turns,
            });
        }
        return result;
    }

    public static List<PuzzleSwitchData> Switches(
        IEnumerable<(string Id, Vector2I Position, string Prompt, string Activated)> switches,
        string puzzleId)
    {
        var result = new List<PuzzleSwitchData>();
        foreach (var (id, position, prompt, activated) in switches)
        {
            result.Add(new PuzzleSwitchData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                PromptText = prompt,
                ActivatedText = activated,
            });
        }
        return result;
    }

    public static List<PuzzleGateData> Gates(
        IEnumerable<(string Id, Vector2I Position, bool StartsClosed)> gates,
        string puzzleId)
    {
        var result = new List<PuzzleGateData>();
        foreach (var (id, position, startsClosed) in gates)
        {
            result.Add(new PuzzleGateData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                StartsClosed = startsClosed,
            });
        }
        return result;
    }

    public static List<PuzzleRiddleData> Riddles(
        IEnumerable<(string Id, Vector2I Position, string Prompt, List<PuzzleRiddleChoiceData> Choices, string CorrectChoiceId, int WrongDamage)> riddles,
        string puzzleId)
    {
        var result = new List<PuzzleRiddleData>();
        foreach (var (id, position, prompt, choices, correct, wrongDamage) in riddles)
        {
            var riddle = new PuzzleRiddleData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                PromptText = prompt,
                CorrectChoiceId = correct,
                WrongAnswerDamage = wrongDamage,
            };
            foreach (var choice in choices)
                riddle.Choices.Add(choice);
            result.Add(riddle);
        }
        return result;
    }
}
```

- [ ] **Step 2: Implement FloorModelAsserter**

`tests/floor_tools/FloorModelAsserter.cs`. Compares two models semantically: metadata exact; tile layers as cell-multisets keyed by `(x,y,tile,alt)`; entities as `id → record` maps (records compared via re-serialized canonical JSON so nested fields are covered).

```csharp
using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static GdUnit4.Assertions;

public static class FloorModelAsserter
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void AssertModelsEqual(FloorJsonModel actual, FloorJsonModel expected)
    {
        // Metadata
        AssertThat(actual.Metadata.FloorName).IsEqual(expected.Metadata.FloorName);
        AssertThat(actual.Metadata.FloorNumber).IsEqual(expected.Metadata.FloorNumber);
        AssertThat(actual.Metadata.Description).IsEqual(expected.Metadata.Description);
        AssertThat(actual.Metadata.PlayerStart.X).IsEqual(expected.Metadata.PlayerStart.X);
        AssertThat(actual.Metadata.PlayerStart.Y).IsEqual(expected.Metadata.PlayerStart.Y);

        // Tile layers as multisets
        AssertTileLayer(actual, expected, "ground");
        AssertTileLayer(actual, expected, "wall");
        AssertTileLayer(actual, expected, "stair");

        // Entities as id-keyed maps (canonical JSON per record)
        AssertEntityList(actual.Entities.EnemySpawns, expected.Entities.EnemySpawns);
        AssertEntityList(actual.Entities.NpcSpawns, expected.Entities.NpcSpawns);
        AssertEntityList(actual.Entities.TreasureBoxes, expected.Entities.TreasureBoxes);
        AssertEntityList(actual.Entities.TrapTiles, expected.Entities.TrapTiles);
        AssertEntityList(actual.Entities.PuzzleSwitches, expected.Entities.PuzzleSwitches);
        AssertEntityList(actual.Entities.PuzzleGates, expected.Entities.PuzzleGates);
        AssertEntityList(actual.Entities.PuzzleRiddles, expected.Entities.PuzzleRiddles);
        AssertEntityList(actual.Entities.StairConnections, expected.Entities.StairConnections);
        AssertEntityList(actual.Entities.HiddenPlaceholders, expected.Entities.HiddenPlaceholders);
    }

    private static void AssertTileLayer(FloorJsonModel actual, FloorJsonModel expected, string layer)
    {
        var a = Multiset(actual.TileLayers.GetValueOrDefault(layer));
        var e = Multiset(expected.TileLayers.GetValueOrDefault(layer));
        AssertThat(a.Count).IsEqual(e.Count);
        foreach (var key in e.Keys)
            AssertThat(a.GetValueOrDefault(key)).IsEqual(e.GetValueOrDefault(key));
    }

    private static Dictionary<string, int> Multiset(List<TileData> tiles)
    {
        var dict = new Dictionary<string, int>();
        foreach (var t in tiles ?? new List<TileData>())
        {
            string key = $"{t.X},{t.Y},{t.Tile},{t.Alternative}";
            dict[key] = dict.GetValueOrDefault(key) + 1;
        }
        return dict;
    }

    private static void AssertEntityList<T>(List<T> actual, List<T> expected) where T : class
    {
        var aMap = (actual ?? new List<T>()).ToDictionary(GetId, Canonical);
        var eMap = (expected ?? new List<T>()).ToDictionary(GetId, Canonical);

        AssertThat(aMap.Count).IsEqual(eMap.Count);
        foreach (var id in eMap.Keys)
        {
            AssertThat(aMap.ContainsKey(id)).IsTrue();
            AssertThat(aMap[id]).IsEqual(eMap[id]);
        }
    }

    private static string GetId<T>(T entity) => entity switch
    {
        EnemySpawnData e => e.Id,
        NpcSpawnData n => n.Id,
        TreasureBoxData t => t.Id,
        TrapTileData t => t.Id,
        PuzzleSwitchData s => s.Id,
        PuzzleGateData g => g.Id,
        PuzzleRiddleData r => r.Id,
        StairConnectionData s => s.Id,
        HiddenPlaceholderData h => h.Id,
        _ => Canonical(entity),
    };

    private static string Canonical<T>(T entity) =>
        JsonSerializer.Serialize(entity, CanonicalJson);
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Sirius.sln`
Expected: build succeeds (no test yet; the asserter is exercised in Tasks 8-11).

- [ ] **Step 4: Commit**

```bash
git add scripts/floor_tools/FloorEntityBuilders.cs tests/floor_tools/FloorModelAsserter.cs
git commit -m "feat: add floor entity builders and semantic parity asserter"
```

---

## Task 8: FloorGenerationService — Ground Floor (first parity gate)

Port GF generation. This is the first end-to-end parity test and validates the whole generation approach against the committed `FloorGF.json`.

**Files:**
- Create: `scripts/floor_tools/layouts/Floor0Layout.cs`
- Create: `scripts/floor_tools/FloorGenerationService.cs`
- Test: `tests/floor_tools/FloorGenerationParityTest.cs`

**Interfaces:**
- Produces: `FloorGenerationService.Generate(int floorNumber) -> FloorJsonModel`, `GenerateGroundFloor()`.
- Consumes: `MazeBuilder` (Task 4), `FloorEntityBuilders` (Task 7).

- [ ] **Step 1: Verify golden freshness**

Confirm the committed `FloorGF.json` matches the current Python generator (so it is a valid golden):

Run: `python3 tools/floor0_maze_generator.py --output /tmp/gf_parity.json --skip-floor-def && diff <(python3 -c "import json;print(json.dumps(json.load(open('/tmp/gf_parity.json')),sort_keys=True))") <(python3 -c "import json;print(json.dumps(json.load(open('scenes/game/floors/FloorGF.json')),sort_keys=True))")`
Expected: no diff. If there IS a diff, the committed JSON is stale — run `python3 tools/floor0_maze_generator.py` (with default paths) and commit that refresh as a baseline before proceeding, so the golden equals current Python.

- [ ] **Step 2: Write the failing parity test**

`tests/floor_tools/FloorGenerationParityTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorGenerationParityTest
{
    private static FloorJsonModel LoadCommitted(int floor)
    {
        string path = FloorRegistry.Get(floor).JsonPath;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        AssertThat(file).IsNotNull();
        return FloorJsonModel.FromJson(file.GetAsText());
    }

    [TestCase]
    public void TestGroundFloorParity()
    {
        var generated = FloorGenerationService.GenerateGroundFloor();
        var committed = LoadCommitted(0);
        FloorModelAsserter.AssertModelsEqual(generated, committed);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestGroundFloorParity"`
Expected: FAIL — `FloorGenerationService` not found.

- [ ] **Step 4: Port GF layout constants**

Create `scripts/floor_tools/layouts/Floor0Layout.cs`. Port **verbatim** from `tools/floor0_maze_generator.py:14-51` (the constants `FLOOR_WIDTH`, `FLOOR_HEIGHT`, `GRID_WIDTH`, `GRID_HEIGHT`, `PLAYER_START`, `SHOPKEEPER_POS`, `HEALER_POS`, `FIRST_GOBLIN_POS`, `STAIR_POS`, `RETURN_SPAWN_FROM_FLOOR_1`, `MAIN_LOOP_POINTS`, `TREASURE_BOXES`) into C# static fields with identical names/values. Use `Vector2I` for points, `Vector2I[]` for `MAIN_LOOP_POINTS`, and `Dictionary<string, (Vector2I Position, int Gold, Dictionary<string,int> Items)>` for `TREASURE_BOXES`. Copy every numeric value exactly.

```csharp
using Godot;
using System.Collections.Generic;

namespace Sirius.FloorTools.Layouts;

public static class Floor0Layout
{
    public const int FloorWidth = 100;   // from floor0_maze_generator.py:14
    public const int FloorHeight = 100;  // :15
    public const int GridWidth = 160;    // :19
    public const int GridHeight = 160;   // :20

    public static readonly Vector2I PlayerStart = new(8, 50);       // :22
    public static readonly Vector2I ShopkeeperPos = new(12, 46);    // :23
    public static readonly Vector2I HealerPos = new(12, 54);        // :24
    public static readonly Vector2I FirstGoblinPos = new(24, 45);   // :25
    public static readonly Vector2I StairPos = new(82, 68);         // :26
    public static readonly Vector2I ReturnSpawnFromFloor1 = new(17, 13); // :28

    // MAIN_LOOP_POINTS — port all 9 points verbatim from :30-40
    public static readonly Vector2I[] MainLoopPoints =
    {
        new(8, 50), new(18, 50), new(18, 18), new(56, 18),
        new(76, 30), new(82, 68), new(52, 82), new(18, 72), new(8, 50),
    };

    // TREASURE_BOXES — port all 8 entries verbatim from :42-51
    public static readonly Dictionary<string, (Vector2I Position, int Gold, Dictionary<string, int> Items)> TreasureBoxes = new()
    {
        ["TreasureBox_GF_EntranceCache"] = (new Vector2I(15, 50), 35, new() { ["health_potion"] = 1 }),
        // ... copy the remaining 7 entries from floor0_maze_generator.py:44-51 exactly
    };
}
```

- [ ] **Step 5: Implement FloorGenerationService GF**

Port `build_floor_model`, `perimeter_walls`, `walkable_cells`, and the GF build logic from `tools/floor0_maze_generator.py:183-259`. Key parity points: GF ground = ALL `GridWidth × GridHeight` cells (160×160) with tile `"starting_area"`; walls = `builder.Walls ∪ perimeter_walls` sorted by `(y, x)`; stair layer = single `"up"` tile. `scripts/floor_tools/FloorGenerationService.cs`:

```csharp
using Godot;
using Sirius.FloorTools.Layouts;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public static class FloorGenerationService
{
    public static FloorJsonModel Generate(int floorNumber) => floorNumber switch
    {
        0 => GenerateGroundFloor(),
        1 => GenerateFloor1(),
        2 => GenerateFloor2(),
        3 => GenerateFloor3(),
        _ => throw new System.ArgumentException($"Unknown floor: {floorNumber}"),
    };

    public static FloorJsonModel GenerateGroundFloor()
    {
        var builder = new MazeBuilder(Floor0Layout.FloorWidth, Floor0Layout.FloorHeight);
        BuildGroundFloorWalls(builder);
        var walls = new HashSet<Vector2I>(builder.Walls);
        walls.UnionWith(PerimeterWalls(Floor0Layout.FloorWidth, Floor0Layout.FloorHeight, Floor0Layout.GridWidth, Floor0Layout.GridHeight));

        var model = new FloorJsonModel
        {
            SchemaVersion = "1.0",
            Metadata = new FloorMetadata
            {
                FloorName = "Ground Floor",
                FloorNumber = 0,
                Description = "A readable starter district loop with optional branches.",
                PlayerStart = new Vector2IData(Floor0Layout.PlayerStart),
            },
        };

        // Ground = full 160x160 grid, tile "starting_area"
        var ground = new List<TileData>();
        for (int y = 0; y < Floor0Layout.GridHeight; y++)
            for (int x = 0; x < Floor0Layout.GridWidth; x++)
                ground.Add(new TileData(x, y, "starting_area"));
        model.TileLayers["ground"] = ground;

        // Walls sorted by (y, x), tile "generic"
        model.TileLayers["wall"] = walls
            .OrderBy(p => p.Y).ThenBy(p => p.X)
            .Select(p => new TileData(p.X, p.Y, "generic")).ToList();

        // Stair layer
        model.TileLayers["stair"] = new List<TileData>
        {
            new(Floor0Layout.StairPos.X, Floor0Layout.StairPos.Y, "up"),
        };

        // Entities — port the exact enemy/npc/stair/treasure lists from
        // floor0_maze_generator.py:211-255 verbatim.
        model.Entities = new SceneEntities
        {
            EnemySpawns = new()
            {
                new() { Id = "EnemySpawn_Goblin", Position = new Vector2IData(Floor0Layout.FirstGoblinPos), EnemyType = "Goblin" },
                new() { Id = "EnemySpawn_Goblin_North", Position = new Vector2IData(44, 36), EnemyType = "Goblin" },
                new() { Id = "EnemySpawn_Orc_East", Position = new Vector2IData(74, 49), EnemyType = "Orc" },
                new() { Id = "EnemySpawn_Goblin_South", Position = new Vector2IData(45, 82), EnemyType = "Goblin" },
            },
            NpcSpawns = new()
            {
                new() { Id = "NpcSpawn_Shopkeeper", Position = new Vector2IData(Floor0Layout.ShopkeeperPos), NpcId = "village_shopkeeper" },
                new() { Id = "NpcSpawn_Healer", Position = new Vector2IData(Floor0Layout.HealerPos), NpcId = "village_healer" },
            },
            StairConnections = new()
            {
                new() { Id = "GF_000", Position = new Vector2IData(Floor0Layout.StairPos), Direction = "up", TargetFloor = 1, DestinationStairId = "1F_001" },
            },
            TreasureBoxes = FloorEntityBuilders.TreasureBoxes(
                Floor0Layout.TreasureBoxes.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Gold, kv.Value.Items))),
        };

        return model;
    }

    // Port of perimeter_walls (floor0_maze_generator.py:169-180).
    public static HashSet<Vector2I> PerimeterWalls(int floorWidth, int floorHeight, int gridWidth, int gridHeight)
    {
        var walls = new HashSet<Vector2I>();
        for (int y = floorHeight; y < gridHeight; y++)
            for (int x = 0; x < gridWidth; x++)
                walls.Add(new Vector2I(x, y));
        for (int y = 0; y < floorHeight; y++)
            for (int x = floorWidth; x < gridWidth; x++)
                walls.Add(new Vector2I(x, y));
        return walls;
    }

    // Port of MazeBuilder.build() for GF — floor0_maze_generator.py:93-126.
    // Carves the loop, plazas, rooms, and dead-end branches exactly as Python.
    private static void BuildGroundFloorWalls(MazeBuilder builder)
    {
        builder.CarveLoop(Floor0Layout.MainLoopPoints, halfWidth: 2);

        builder.CarveRect(5, 42, 17, 58);
        builder.CarveRect(9, 43, 15, 48);
        builder.CarveRect(9, 52, 15, 57);
        builder.CarveRect(20, 41, 29, 48);
        builder.CarveRect(11, 11, 25, 24);
        builder.CarveRect(38, 10, 52, 24);
        builder.CarvePath(new(25, 18), new(38, 18), 1);
        builder.CarvePath(new(44, 24), new(44, 36), 1);
        builder.CarveRect(39, 34, 50, 41);
        builder.CarvePath(new(39, 38), new(20, 50), 1);
        builder.CarveRect(62, 24, 81, 36);
        builder.CarveRect(70, 42, 88, 55);
        builder.CarvePath(new(76, 36), new(79, 42), 1);
        builder.CarvePath(new(70, 49), new(56, 49), 1);
        builder.CarvePath(new(56, 49), new(56, 18), 1);
        builder.CarveRect(66, 63, 88, 74);
        builder.CarveRect(72, 76, 90, 88);
        builder.CarvePath(new(80, 74), new(80, 76), 1);
        builder.CarveRect(34, 74, 58, 90);
        builder.CarveRect(14, 65, 25, 79);
        builder.CarvePath(new(34, 82), new(25, 72), 1);
        builder.CarvePath(new(52, 74), new(52, 52), 1);
        builder.CarvePath(new(52, 52), new(70, 49), 1);

        // Dead-end branches — port verbatim from floor0_maze_generator.py:129-137
        var branches = new (Vector2I, Vector2I)[]
        {
            (new(30, 18), new(30, 8)),
            (new(49, 18), new(49, 8)),
            (new(76, 30), new(91, 30)),
            (new(82, 68), new(94, 68)),
            (new(52, 82), new(52, 94)),
            (new(18, 72), new(7, 72)),
            (new(18, 50), new(33, 50)),
        };
        foreach (var (start, end) in branches)
            builder.CarvePath(start, end, 1);

        builder.ReinforcePerimeter();
    }

    // Placeholders — implemented in Tasks 9-11.
    public static FloorJsonModel GenerateFloor1() => throw new System.NotImplementedException();
    public static FloorJsonModel GenerateFloor2() => throw new System.NotImplementedException();
    public static FloorJsonModel GenerateFloor3() => throw new System.NotImplementedException();
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestGroundFloorParity"`
Expected: PASS. If it fails, the diff output identifies the first mismatched cell/entity — fix by comparing against `floor0_maze_generator.py` line-by-line.

- [ ] **Step 7: Commit**

```bash
git add scripts/floor_tools/layouts/Floor0Layout.cs scripts/floor_tools/FloorGenerationService.cs tests/floor_tools/FloorGenerationParityTest.cs
git commit -m "feat: port Ground Floor generation with parity test"
```

---

## Task 9: FloorGenerationService — Floor 1

Port 1F generation including supplemental-enemy planning and puzzle-trap entities. The supplemental planner makes this the highest-risk parity task.

**Files:**
- Create: `scripts/floor_tools/layouts/Floor1Layout.cs`
- Modify: `scripts/floor_tools/FloorGenerationService.cs` (implement `GenerateFloor1`)
- Test: add `TestFloor1Parity` to `tests/floor_tools/FloorGenerationParityTest.cs`

**Interfaces:**
- Produces: `FloorGenerationService.GenerateFloor1()`.
- Consumes: `MazeBuilder`, `FloorGraph`, `SupplementalEnemyPlanner`, `FloorEntityBuilders`, `HiddenPlaceholderData`.

- [ ] **Step 1: Verify golden freshness**

Run: `python3 tools/floor1_maze_generator.py --skip-floor-defs && git diff --stat scenes/game/floors/Floor1F.json`
Expected: no changes (committed JSON is current). If changed, commit the Python refresh as baseline first.

- [ ] **Step 2: Write the failing parity test**

Add to `FloorGenerationParityTest.cs`:

```csharp
[TestCase]
public void TestFloor1Parity()
{
    var generated = FloorGenerationService.GenerateFloor1();
    var committed = LoadCommitted(1);
    FloorModelAsserter.AssertModelsEqual(generated, committed);
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestFloor1Parity"`
Expected: FAIL — `GenerateFloor1` throws `NotImplementedException`.

- [ ] **Step 4: Port Floor 1 layout constants**

Create `scripts/floor_tools/layouts/Floor1Layout.cs`. Port **verbatim** from `tools/floor1_maze_generator.py:13-264`:
- Dimensions `FLOOR1_WIDTH=60`, `FLOOR1_HEIGHT=60` (:13-14), `ENEMY_DENSITY_MULTIPLIER=3` (:21).
- `FLOOR1_PLAYER_START`, `FLOOR1_DOWN_STAIR`, `FLOOR1_UP_STAIR_A/B` (:23-26).
- `FLOOR1_HIDDEN_PLACEHOLDERS` dict (:36-39) → `Dictionary<string, Vector2I>`.
- `FLOOR1_ENEMY_GATES` (:43-52) and `FLOOR1_EXTRA_ENEMY_PATROLS` (:54-83) → `Dictionary<string, EnemySpec>`.
- `FLOOR1_SUPPLEMENTAL_ENEMY_PREFIX` (:85), `FLOOR1_SUPPLEMENTAL_ENEMY_TYPES` (:86-91).
- `FLOOR1_TREASURE_BOXES` (:93-106).
- `FLOOR1_PUZZLE_ID` (:108), `FLOOR1_PUZZLE_TRAPS` (:110-115), `FLOOR1_PUZZLE_SWITCHES` (:117-123), `FLOOR1_PUZZLE_GATES` (:125-127), `FLOOR1_PUZZLE_RIDDLES` (:129-... read to end of dict).

Use C# types mirroring the Python dicts. Copy every coordinate/value exactly. Read the unread riddle block (lines 129-160ish) and copy its `choices`/`correct_choice_id`/`wrong_answer_damage`.

Also port the 1F wall-builder `build_floor1_walls()` from `floor1_maze_generator.py:457-609` verbatim into a private method `BuildFloor1Walls(MazeBuilder)` in `FloorGenerationService.cs` (every `carve_*`/`builder.walls.update(...)` call, same coordinates).

- [ ] **Step 5: Implement GenerateFloor1**

Port `build_floor1_model()` from `floor1_maze_generator.py:782-871`. In `FloorGenerationService.cs`, replace the `GenerateFloor1()` placeholder:

```csharp
public static FloorJsonModel GenerateFloor1()
{
    var builder = new MazeBuilder(Floor1Layout.Width, Floor1Layout.Height);
    BuildFloor1Walls(builder);
    var walls = builder.Walls;
    var walkable = FloorGraph.WalkableCellsFromWalls(walls, Floor1Layout.Width, Floor1Layout.Height);

    var baseEnemies = MergeDicts(Floor1Layout.EnemyGates, Floor1Layout.ExtraEnemyPatrols);
    var occupied = new HashSet<Vector2I>
    {
        Floor1Layout.PlayerStart, Floor1Layout.DownStair,
        Floor1Layout.UpStairA, Floor1Layout.UpStairB,
    };
    occupied.UnionWith(Floor1Layout.HiddenPlaceholders.Values);
    occupied.UnionWith(PositionSet(baseEnemies));
    occupied.UnionWith(Floor1Layout.TreasureBoxes.Values.Select(t => t.Position));
    occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleTraps));
    occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleSwitches));
    occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleGates));
    occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleRiddles));

    var enemySpawns = MergeDicts(baseEnemies, SupplementalEnemyPlanner.Plan(
        Floor1Layout.SupplementalPrefix, baseEnemies, walkable, occupied,
        Floor1Layout.SupplementalTypes));

    var model = new FloorJsonModel { SchemaVersion = "1.0" };
    model.Metadata = new FloorMetadata
    {
        FloorName = "First Floor",
        FloorNumber = 1,
        Description = "A compact combat-gated loop maze with two 2/F routes.",
        PlayerStart = new Vector2IData(Floor1Layout.PlayerStart),
    };

    model.TileLayers["ground"] = GroundTiles(Floor1Layout.Width, Floor1Layout.Height);
    model.TileLayers["wall"] = WallTiles(walls, Floor1Layout.Width, Floor1Layout.Height, includeOutsideFootprint: false);
    model.TileLayers["stair"] = new List<TileData>
    {
        new(Floor1Layout.DownStair.X, Floor1Layout.DownStair.Y, "down"),
        new(Floor1Layout.UpStairA.X, Floor1Layout.UpStairA.Y, "up"),
        new(Floor1Layout.UpStairB.X, Floor1Layout.UpStairB.Y, "up"),
    };

    model.Entities = new SceneEntities
    {
        EnemySpawns = enemySpawns.Select(kv => new EnemySpawnData
        {
            Id = kv.Key, Position = new Vector2IData(kv.Value.Position), EnemyType = kv.Value.EnemyType,
        }).ToList(),
        NpcSpawns = new(),
        StairConnections = new()
        {
            new() { Id = "1F_001", Position = new Vector2IData(Floor1Layout.DownStair), Direction = "down", TargetFloor = 0, DestinationStairId = "GF_000" },
            new() { Id = "1F_2F_A", Position = new Vector2IData(Floor1Layout.UpStairA), Direction = "up", TargetFloor = 2, DestinationStairId = "2F_1F_A" },
            new() { Id = "1F_2F_B", Position = new Vector2IData(Floor1Layout.UpStairB), Direction = "up", TargetFloor = 2, DestinationStairId = "2F_1F_B" },
        },
        HiddenPlaceholders = Floor1Layout.HiddenPlaceholders
            .Select(kv => new HiddenPlaceholderData { Id = kv.Key, Position = new Vector2IData(kv.Value) }).ToList(),
        TreasureBoxes = FloorEntityBuilders.TreasureBoxes(Floor1Layout.TreasureBoxes.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Gold, kv.Value.Items))),
        TrapTiles = FloorEntityBuilders.TrapTiles(Floor1Layout.PuzzleTraps.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Damage, kv.Value.StatusEffect, kv.Value.Magnitude, kv.Value.Turns)), Floor1Layout.PuzzleId),
        PuzzleSwitches = FloorEntityBuilders.Switches(Floor1Layout.PuzzleSwitches.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Prompt, kv.Value.Activated)), Floor1Layout.PuzzleId),
        PuzzleGates = FloorEntityBuilders.Gates(Floor1Layout.PuzzleGates.Select(kv => (kv.Key, kv.Value.Position, kv.Value.StartsClosed)), Floor1Layout.PuzzleId),
        PuzzleRiddles = FloorEntityBuilders.Riddles(Floor1Layout.PuzzleRiddles.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Prompt, kv.Value.Choices, kv.Value.CorrectChoiceId, kv.Value.WrongDamage)), Floor1Layout.PuzzleId),
    };

    return model;
}

// Shared helpers (also used by Floor 2/3):
private static List<TileData> GroundTiles(int width, int height)
{
    var tiles = new List<TileData>(width * height);
    for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            tiles.Add(new TileData(x, y, "starting_area"));
    return tiles;
}

private static List<TileData> WallTiles(HashSet<Vector2I> walls, int width, int height, bool includeOutsideFootprint)
{
    var all = new HashSet<Vector2I>(walls);
    if (includeOutsideFootprint)
        all.UnionWith(OutsideFootprintWalls(width, height));
    return all.OrderBy(p => p.Y).ThenBy(p => p.X)
        .Select(p => new TileData(p.X, p.Y, "generic")).ToList();
}

private static HashSet<Vector2I> OutsideFootprintWalls(int width, int height)
{
    var walls = new HashSet<Vector2I>();
    for (int y = height; y < 160; y++)
        for (int x = 0; x < 160; x++)
            walls.Add(new Vector2I(x, y));
    for (int y = 0; y < height; y++)
        for (int x = width; x < 160; x++)
            walls.Add(new Vector2I(x, y));
    return walls;
}

private static HashSet<Vector2I> PositionSet(Dictionary<string, EnemySpec> enemies)
    => enemies.Values.Select(e => e.Position).ToHashSet();
private static HashSet<Vector2I> AuthoredPositions<T>(Dictionary<string, T> entities) where T : IHasPosition
    => entities.Values.Select(e => e.Position).ToHashSet();
private static Dictionary<string, EnemySpec> MergeDicts(params Dictionary<string, EnemySpec>[] dicts)
{
    var merged = new Dictionary<string, EnemySpec>();
    foreach (var d in dicts) foreach (var kv in d) merged[kv.Key] = kv.Value;
    return merged;
}
```

Define an `IHasPosition` interface (or use a `Vector2I Position` field convention) for the authored-position extractor, implemented by the puzzle layout record types.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestFloor1Parity"`
Expected: PASS. The supplemental-enemy positions are the likely failure point — if they mismatch, re-check the sort key `(x*73+y*37)%997` and the `(4,3,2,1)` distance loop ordering against `floor1_maze_generator.py:417-447`.

- [ ] **Step 7: Commit**

```bash
git add scripts/floor_tools/layouts/Floor1Layout.cs scripts/floor_tools/FloorGenerationService.cs tests/floor_tools/FloorGenerationParityTest.cs
git commit -m "feat: port Floor 1 generation with supplemental enemies and puzzle traps"
```

---

## Task 10: FloorGenerationService — Floor 2

Same shape as Floor 1; no hidden placeholders (`hidden_placeholders: []`).

**Files:**
- Create: `scripts/floor_tools/layouts/Floor2Layout.cs`
- Modify: `scripts/floor_tools/FloorGenerationService.cs` (implement `GenerateFloor2`, port `build_floor2_walls`)
- Test: add `TestFloor2Parity`

- [ ] **Step 1: Verify golden freshness**

Run: `python3 tools/floor1_maze_generator.py --skip-floor-defs && git diff --stat scenes/game/floors/Floor2F.json`
Expected: no changes.

- [ ] **Step 2: Write the failing parity test**

```csharp
[TestCase]
public void TestFloor2Parity()
{
    var generated = FloorGenerationService.GenerateFloor2();
    var committed = LoadCommitted(2);
    FloorModelAsserter.AssertModelsEqual(generated, committed);
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestFloor2Parity"`
Expected: FAIL — `GenerateFloor2` throws NotImplementedException.

- [ ] **Step 4: Port Floor 2 constants + walls**

Create `Floor2Layout.cs` porting verbatim from `floor1_maze_generator.py` the `FLOOR2_*` constants (player start, stairs, enemy gates, extra patrols, supplemental prefix/types, treasure boxes, puzzle id/traps/switches/gates/riddles — locate the FLOOR2_ block, roughly lines 28-31 and a dedicated constants section; read and copy exactly). Port `build_floor2_walls()` (around :611-769) into `BuildFloor2Walls(MazeBuilder)` verbatim, including the special `builder.walls.update(...)` calls at :724,:757,:760.

- [ ] **Step 5: Implement GenerateFloor2**

Mirror `GenerateFloor1` but with Floor 2 constants, `hidden_placeholders: []`, and Floor 2's three stairs (two down `2F_1F_A`/`2F_1F_B`, one up `2F_3F`). Port from `build_floor2_model()` (:874-960). Reuse the shared `GroundTiles`/`WallTiles` helpers (wall uses `includeOutsideFootprint: false`, matching Python :904-918).

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestFloor2Parity"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/floor_tools/layouts/Floor2Layout.cs scripts/floor_tools/FloorGenerationService.cs tests/floor_tools/FloorGenerationParityTest.cs
git commit -m "feat: port Floor 2 generation with parity test"
```

---

## Task 11: FloorGenerationService — Floor 3

Smallest floor (24×18 landing); no enemies, no treasure, no puzzles, no hidden placeholders.

**Files:**
- Create: `scripts/floor_tools/layouts/Floor3Layout.cs`
- Modify: `scripts/floor_tools/FloorGenerationService.cs` (implement `GenerateFloor3`, port `build_floor3_walls`)
- Test: add `TestFloor3Parity`

- [ ] **Step 1: Verify golden freshness**

Run: `python3 tools/floor1_maze_generator.py --skip-floor-defs && git diff --stat scenes/game/floors/Floor3F.json`
Expected: no changes.

- [ ] **Step 2: Write the failing parity test**

```csharp
[TestCase]
public void TestFloor3Parity()
{
    var generated = FloorGenerationService.GenerateFloor3();
    var committed = LoadCommitted(3);
    FloorModelAsserter.AssertModelsEqual(generated, committed);
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestFloor3Parity"`
Expected: FAIL.

- [ ] **Step 4: Port Floor 3 constants + walls**

Create `Floor3Layout.cs` porting `FLOOR3_WIDTH=24`, `FLOOR3_HEIGHT=18` (:17-18), `FLOOR3_PLAYER_START`, `FLOOR3_DOWN_STAIR` (:33-34). Port `build_floor3_walls()` (:773-779) into `BuildFloor3Walls(MazeBuilder)`.

- [ ] **Step 5: Implement GenerateFloor3**

Port `build_floor3_model()` (:962-1004): ground 24×18, walls (includeOutsideFootprint: false), one `"down"` stair `3F_2F` → target floor 2 `2F_3F`, empty entity lists for enemies/npcs/treasure/traps/switches/gates/riddles/hidden_placeholders.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest.TestFloor3Parity"`
Expected: PASS.

- [ ] **Step 7: Run the full parity suite**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest"`
Expected: all four floors PASS — generation is feature-complete and parity-locked.

- [ ] **Step 8: Commit**

```bash
git add scripts/floor_tools/layouts/Floor3Layout.cs scripts/floor_tools/FloorGenerationService.cs tests/floor_tools/FloorGenerationParityTest.cs
git commit -m "feat: port Floor 3 generation; all floors parity-gated"
```

---

## Task 12: FloorValidationService

Port `validate_model` plus the extra acceptance-criteria checks. Returns a `ValidationResult` instead of throwing.

**Files:**
- Create: `scripts/floor_tools/ValidationIssue.cs`, `scripts/floor_tools/ValidationResult.cs`, `scripts/floor_tools/FloorValidationService.cs`
- Test: `tests/floor_tools/FloorValidationServiceTest.cs`

**Interfaces:**
- Produces: `FloorValidationService.Validate(FloorJsonModel model, int width, int height) -> ValidationResult`; `ValidationResult.HasErrors`, `ValidationResult.Issues`; `ValidationIssue(Severity, string Code, string Message)` (consumed by Task 14 FloorSceneWriter, Task 15 dock).
- Consumes: `FloorGraph` (Task 5), `ItemCatalog.ItemExists` (existing).

- [ ] **Step 1: Write the failing tests**

`tests/floor_tools/FloorValidationServiceTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FloorValidationServiceTest
{
    private static FloorJsonModel ValidMinimalModel()
    {
        var walls = new HashSet<Vector2I> { new(2, 0), new(0, 2), new(2, 2) };
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(0, 0) },
        };
        model.TileLayers["ground"] = new() { new(0, 0, "starting_area"), new(1, 0, "starting_area"), new(1, 1, "starting_area") };
        model.TileLayers["wall"] = WallsList(walls);
        model.Entities = new SceneEntities
        {
            EnemySpawns = new(), NpcSpawns = new(), TreasureBoxes = new(),
            TrapTiles = new(), PuzzleSwitches = new(), PuzzleGates = new(),
            PuzzleRiddles = new(), StairConnections = new(), HiddenPlaceholders = new(),
        };
        return model;
    }

    private static List<TileData> WallsList(HashSet<Vector2I> walls) =>
        walls.Select(w => new TileData(w.X, w.Y, "generic")).ToList();

    [TestCase]
    public void TestValidModelHasNoErrors()
    {
        var result = FloorValidationService.Validate(ValidMinimalModel(), 3, 3);
        AssertThat(result.HasErrors).IsFalse();
    }

    [TestCase]
    public void TestDisconnectedCellsReported()
    {
        var model = ValidMinimalModel();
        model.TileLayers["ground"].Add(new TileData(5, 5, "starting_area")); // disconnected walkable
        var result = FloorValidationService.Validate(model, 6, 6);
        AssertThat(result.HasErrors).IsTrue();
        AssertThat(result.Issues.Any(i => i.Code == "DisconnectedCells")).IsTrue();
    }

    [TestCase]
    public void TestEntityOverlapReported()
    {
        var model = ValidMinimalModel();
        model.Entities.EnemySpawns = new()
        {
            new() { Id = "e1", Position = new Vector2IData(1, 0), EnemyType = "goblin" },
            new() { Id = "e2", Position = new Vector2IData(1, 0), EnemyType = "orc" },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "EntityOverlap")).IsTrue();
    }

    [TestCase]
    public void TestInvalidTreasureRewardReported()
    {
        var model = ValidMinimalModel();
        model.Entities.TreasureBoxes = new()
        {
            new() { Id = "t1", Position = new Vector2IData(1, 0), Gold = 0,
                    Items = new() { new() { ItemId = "nonexistent_item", Quantity = 1 } } },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "InvalidTreasureReward")).IsTrue();
    }

    [TestCase]
    public void TestEmptyPuzzleIdReported()
    {
        var model = ValidMinimalModel();
        model.Entities.PuzzleSwitches = new()
        {
            new() { Id = "s1", PuzzleId = "", Position = new Vector2IData(1, 0) },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "InvalidPuzzleIdentity")).IsTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorValidationServiceTest"`
Expected: FAIL — types not found.

- [ ] **Step 3: Implement ValidationResult + ValidationIssue**

`scripts/floor_tools/ValidationIssue.cs`:

```csharp
namespace Sirius.FloorTools;

public enum Severity { Error, Warning }

public record ValidationIssue(Severity Severity, string Code, string Message);
```

`scripts/floor_tools/ValidationResult.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();
    public bool HasErrors => Issues.Any(i => i.Severity == Severity.Error);
    public void Error(string code, string message) => Issues.Add(new ValidationIssue(Severity.Error, code, message));
}
```

- [ ] **Step 4: Implement FloorValidationService**

Port `validate_model` from `floor1_maze_generator.py:1138-1211`. `scripts/floor_tools/FloorValidationService.cs`:

```csharp
using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public static class FloorValidationService
{
    public static ValidationResult Validate(FloorJsonModel model, int width, int height)
    {
        var result = new ValidationResult();
        var walls = (model.TileLayers.GetValueOrDefault("wall") ?? new List<TileData>())
            .Select(t => new Vector2I(t.X, t.Y)).ToHashSet();
        var walkable = FloorGraph.WalkableCellsFromWalls(walls, width, height);
        var start = model.Metadata.PlayerStart.ToVector2I();

        if (!walkable.Contains(start))
            result.Error("PlayerStartNotWalkable", $"Player start {start} is not walkable");

        var connected = FloorGraph.ConnectedCells(walkable, start);
        var disconnected = walkable.Except(connected).ToList();
        if (disconnected.Count > 0)
            result.Error("DisconnectedCells", $"Disconnected walkable cells: {Format(disconnected.Take(5))}");

        // Entity id/overlap/walkable/reachable checks
        var seenIds = new Dictionary<string, string>();
        var occupied = new Dictionary<Vector2I, string>();
        var goals = new List<Vector2I>();
        foreach (var (key, entities) in EntityGroups(model))
        {
            foreach (var e in entities)
            {
                string id = e.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    result.Error("EmptyEntityId", $"Entity in {key} has empty id");
                    continue;
                }
                if (seenIds.TryGetValue(id, out var prev))
                    result.Error("DuplicateEntityId", $"Duplicate entity id '{id}' in {key} and {prev}");
                seenIds[id] = key;

                var pos = e.Position.ToVector2I();
                if (occupied.TryGetValue(pos, out var occupier))
                    result.Error("EntityOverlap", $"Entity position {pos} overlaps {key} and {occupier}");
                occupied[pos] = key;
                goals.Add(pos);

                // Invalid puzzle identity (non-treasure, non-stair puzzle-bearing entities)
                if (IsPuzzleEntity(key) && string.IsNullOrWhiteSpace(e.PuzzleId))
                    result.Error("InvalidPuzzleIdentity", $"{key} '{id}' has empty puzzle_id");

                // Invalid treasure reward
                if (e is TreasureDataBoxed tb)
                    ValidateTreasure(tb, id, result);
            }
        }

        foreach (var goal in goals)
        {
            if (!walkable.Contains(goal))
                result.Error("EntityNotWalkable", $"Entity position {goal} is not walkable");
            else if (!connected.Contains(goal))
                result.Error("EntityUnreachable", $"No path from {start} to {goal}");
        }

        // Closed puzzle gate blocking required route (stairs + hidden placeholders)
        ValidateClosedGates(model, walkable, start, result);

        // Unrewarded dead-end branches (floor 1, 2)
        if (model.Metadata.FloorNumber is 1 or 2)
            ValidateDeadEnds(model, walkable, width, height, result);

        return result;
    }

    // Helpers: EntityGroups, IsPuzzleEntity, ValidateTreasure, ValidateClosedGates,
    // ValidateDeadEnds — port the corresponding Python logic. Full bodies below.

    private static IEnumerable<(string Key, List<EntityView> Entities)> EntityGroups(FloorJsonModel model)
    {
        yield return ("enemy_spawns", View(model.Entities.EnemySpawns));
        yield return ("npc_spawns", View(model.Entities.NpcSpawns));
        yield return ("stair_connections", View(model.Entities.StairConnections));
        yield return ("hidden_placeholders", View(model.Entities.HiddenPlaceholders));
        yield return ("treasure_boxes", View(model.Entities.TreasureBoxes));
        yield return ("trap_tiles", View(model.Entities.TrapTiles));
        yield return ("puzzle_switches", View(model.Entities.PuzzleSwitches));
        yield return ("puzzle_gates", View(model.Entities.PuzzleGates));
        yield return ("puzzle_riddles", View(model.Entities.PuzzleRiddles));
    }

    private static bool IsPuzzleEntity(string key)
        => key is "trap_tiles" or "puzzle_switches" or "puzzle_gates" or "puzzle_riddles";

    private static void ValidateTreasure(TreasureDataBoxed box, string id, ValidationResult result)
    {
        foreach (var item in box.Items ?? new())
        {
            if (item.Quantity <= 0 || !ItemCatalog.ItemExists(item.ItemId))
                result.Error("InvalidTreasureReward", $"Treasure '{id}' has invalid reward item '{item.ItemId}' x{item.Quantity}");
        }
    }

    private static void ValidateClosedGates(FloorJsonModel model, HashSet<Vector2I> walkable, Vector2I start, ValidationResult result)
    {
        var closedGates = (model.Entities.PuzzleGates ?? new())
            .Where(g => g.StartsClosed)
            .Select(g => g.Position.ToVector2I()).ToHashSet();
        if (closedGates.Count == 0) return;

        var gateWalkable = new HashSet<Vector2I>(walkable);
        foreach (var g in closedGates) gateWalkable.Remove(g);

        if (!gateWalkable.Contains(start))
        {
            result.Error("ClosedGateBlocksStart", $"Player start {start} is blocked by a closed puzzle gate");
            return;
        }
        var gateConnected = FloorGraph.ConnectedCells(gateWalkable, start);
        foreach (var stair in model.Entities.StairConnections ?? new())
        {
            var pos = stair.Position.ToVector2I();
            if (!gateConnected.Contains(pos))
                result.Error("ClosedGateBlocksRoute", $"Required stair {stair.Id} is blocked by a closed puzzle gate");
        }
        foreach (var ph in model.Entities.HiddenPlaceholders ?? new())
        {
            var pos = ph.Position.ToVector2I();
            if (!gateConnected.Contains(pos))
                result.Error("ClosedGateBlocksRoute", $"Required hidden placeholder {ph.Id} is blocked by a closed puzzle gate");
        }
    }

    private static void ValidateDeadEnds(FloorJsonModel model, HashSet<Vector2I> walkable, int width, int height, ValidationResult result)
    {
        var payoff = new HashSet<Vector2I>();
        foreach (var (key, entities) in EntityGroups(model))
            foreach (var e in entities)
                payoff.Add(e.Position.ToVector2I());

        foreach (var branch in FloorGraph.DeadEndBranches(walkable, width, height))
        {
            var adjacent = new HashSet<Vector2I>();
            foreach (var cell in branch)
                adjacent.UnionWith(FloorGraph.WalkableNeighbors(walkable, cell));
            var branchSet = branch.ToHashSet();
            adjacent.UnionWith(branchSet);
            if (payoff.Intersect(adjacent).Any() == false)
                result.Error("UnrewardedDeadEnd", $"Unrewarded dead-end branch at {branch[0]}");
        }
    }

    private static string Format(IEnumerable<Vector2I> cells)
        => string.Join(", ", cells.Select(c => $"({c.X},{c.Y})"));
}
```

To avoid coupling validation to each concrete DTO type, introduce a small internal projection `EntityView { string Id; string PuzzleId; Vector2I Position; object Source; }` with a `View<T>(List<T>)` helper that reads `Id`/`Position`/`PuzzleId` reflectively or via per-type switch, and a `TreasureDataBoxed` alias for the treasure items check. Implement `View` to map each entity type to `EntityView` (treasure boxes expose `Items` via the `Source` field cast).

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorValidationServiceTest"`
Expected: PASS.

- [ ] **Step 6: Sanity-check validation against all real generated floors**

Add a temporary check (or test) that `FloorValidationService.Validate(FloorGenerationService.Generate(n), w, h)` returns `HasErrors == false` for n=0..3 with the correct per-floor dimensions. This confirms the validator accepts real floors. (Can be a `[TestCase]` in the parity test file using `FloorRegistry`-derived dimensions; remove or keep as a regression test.)

- [ ] **Step 7: Commit**

```bash
git add scripts/floor_tools/ValidationIssue.cs scripts/floor_tools/ValidationResult.cs scripts/floor_tools/FloorValidationService.cs tests/floor_tools/FloorValidationServiceTest.cs
git commit -m "feat: port FloorValidationService with structured issue reporting"
```

---

## Task 13: FloorResourceSyncService (typed .tres updates)

Replaces the Python regex `.tres` mutation with typed `FloorDefinition` property updates via `ResourceSaver`.

**Files:**
- Create: `scripts/floor_tools/FloorResourceSyncService.cs`
- Test: `tests/floor_tools/FloorResourceSyncServiceTest.cs`

**Interfaces:**
- Produces: `FloorResourceSyncService.Apply(FloorDefinition def, FloorJsonModel model, FloorSyncOptions options)`, `FloorSyncOptions` record (consumed by Task 14).
- Consumes: `FloorDefinition` (existing).

- [ ] **Step 1: Write the failing test**

`tests/floor_tools/FloorResourceSyncServiceTest.cs`:

```csharp
using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorResourceSyncServiceTest
{
    [TestCase]
    public void TestAppliesPlayerStartAndStairs()
    {
        var def = new FloorDefinition();
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(8, 30) },
            Entities = new()
            {
                StairConnections = new()
                {
                    new() { Position = new Vector2IData(8, 30), Direction = "down" },
                    new() { Position = new Vector2IData(49, 12), Direction = "up" },
                },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        AssertThat(def.PlayerStartPosition).IsEqual(new Vector2I(8, 30));
        AssertThat(def.StairsDown.Count).IsEqual(1);
        AssertThat(def.StairsDown[0]).IsEqual(new Vector2I(8, 30));
        AssertThat(def.StairsUp.Count).IsEqual(1);
        AssertThat(def.StairsUp[0]).IsEqual(new Vector2I(49, 12));
    }

    [TestCase]
    public void TestGfPreservesExistingDestinationsWhenNoOverride()
    {
        var def = new FloorDefinition();
        def.StairsUpDestinations.Add(new Vector2I(17, 13));
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(8, 50) },
            Entities = new()
            {
                StairConnections = new() { new() { Position = new Vector2IData(82, 68), Direction = "up" } },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        // GF keeps the existing up-destination when no override is supplied (parity with Python default).
        AssertThat(def.StairsUpDestinations[0]).IsEqual(new Vector2I(17, 13));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorResourceSyncServiceTest"`
Expected: FAIL — types not found.

- [ ] **Step 3: Implement FloorSyncOptions + FloorResourceSyncService**

Mirror the Python destination logic: GF (`FloorNumber==0`) preserves existing `StairsUpDestinations` when no override is given (otherwise uses the override); floors 1/2/3 set destinations to the stair positions themselves (parity with `floor1_maze_generator.py:1245-1254`). `scripts/floor_tools/FloorResourceSyncService.cs`:

```csharp
using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public record FloorSyncOptions(Vector2I? StairDestOverride = null)
{
    public FloorSyncOptions() : this(null) { }
}

public static class FloorResourceSyncService
{
    public static void Apply(FloorDefinition def, FloorJsonModel model, FloorSyncOptions options)
    {
        def.PlayerStartPosition = model.Metadata.PlayerStart.ToVector2I();

        var stairs = model.Entities.StairConnections ?? new();
        var up = stairs.Where(s => s.Direction?.ToLower() == "up").Select(s => s.Position.ToVector2I()).ToList();
        var down = stairs.Where(s => s.Direction?.ToLower() == "down").Select(s => s.Position.ToVector2I()).ToList();

        def.StairsUp = ToArray(up);
        def.StairsDown = ToArray(down);

        if (model.Metadata.FloorNumber == 0)
        {
            // GF: override wins; else preserve existing destination (parity with Python default).
            def.StairsUpDestinations = options.StairDestOverride is { } o
                ? ToArray(new List<Vector2I> { o })
                : PreserveOrFallback(def.StairsUpDestinations, up);
        }
        else
        {
            // Floors 1/2/3: destinations mirror the stair positions themselves.
            def.StairsUpDestinations = ToArray(up);
            def.StairsDownDestinations = ToArray(down);
        }
    }

    private static Godot.Collections.Array<Vector2I> ToArray(List<Vector2I> values)
    {
        var arr = new Godot.Collections.Array<Vector2I>();
        foreach (var v in values) arr.Add(v);
        return arr;
    }

    private static Godot.Collections.Array<Vector2I> PreserveOrFallback(
        Godot.Collections.Array<Vector2I> existing, List<Vector2I> fallback)
    {
        if (existing != null && existing.Count > 0)
            return new Godot.Collections.Array<Vector2I>(existing);
        return ToArray(fallback);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorResourceSyncServiceTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/floor_tools/FloorResourceSyncService.cs tests/floor_tools/FloorResourceSyncServiceTest.cs
git commit -m "feat: add typed FloorResourceSyncService replacing regex .tres edits"
```

---

## Task 14: FloorSceneWriter (end-to-end orchestrator)

Ties generation → validation → import → resource sync → pack. Context-independent (works in-editor and headless).

**Files:**
- Create: `scripts/floor_tools/FloorSceneWriter.cs`
- Test: `tests/floor_tools/FloorSceneWriterTest.cs`

**Interfaces:**
- Produces: `FloorSceneWriter.Generate(int floorNumber, FloorSyncOptions options, bool writeJson = true, bool syncDef = true) -> FloorSceneResult`, `FloorSceneWriter.GenerateToJson(int floorNumber) -> FloorJsonModel`. `FloorSceneResult { bool Success; ValidationResult Validation; string Summary }` (consumed by Task 15 dock, Task 16 CLI).

- [ ] **Step 1: Write the failing test**

`tests/floor_tools/FloorSceneWriterTest.cs`:

```csharp
using GdUnit4;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorSceneWriterTest
{
    [TestCase]
    public void TestGenerateFloor3WritesSceneAndDefAndJson()
    {
        // Floor 3 is the smallest/safest to round-trip in a test.
        var paths = FloorRegistry.Get(3);
        var result = FloorSceneWriter.Generate(3, new FloorSyncOptions());

        AssertThat(result.Success).IsTrue();
        AssertThat(result.Validation.HasErrors).IsFalse();
        // Files were rewritten
        AssertThat(FileAccess.FileExists(paths.ScenePath)).IsTrue();
        AssertThat(FileAccess.FileExists(paths.JsonPath)).IsTrue();
    }
}
```

Note: this test rewrites real committed Floor 3 artifacts. After it passes, regenerate all floors in Task 17 so committed files are C#-authored. Run it on a clean working tree and commit the regenerated Floor 3 with the task.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorSceneWriterTest"`
Expected: FAIL — `FloorSceneWriter` not found.

- [ ] **Step 3: Implement FloorSceneWriter**

`scripts/floor_tools/FloorSceneWriter.cs`:

```csharp
using Godot;
using Sirius.TilemapJson;
using System.Linq;

namespace Sirius.FloorTools;

public record FloorSceneResult(bool Success, ValidationResult Validation, string Summary);

public static class FloorSceneWriter
{
    public static FloorSceneResult Generate(int floorNumber, FloorSyncOptions options, bool writeJson = true, bool syncDef = true)
    {
        var paths = FloorRegistry.Get(floorNumber);
        var model = FloorGenerationService.Generate(floorNumber);
        var (width, height) = DimensionsFor(floorNumber);

        var validation = FloorValidationService.Validate(model, width, height);
        if (validation.HasErrors)
            return new FloorSceneResult(false, validation, $"Validation failed: {validation.Issues.Count} issue(s)");

        // Write into the scene via the existing importer.
        var packed = GD.Load<PackedScene>(paths.ScenePath);
        var scene = packed.Instantiate();
        var gridMap = scene.GetNode<GridMap>("GridMap");
        var importer = new TilemapJsonImporter();
        importer.ImportToScene(model, gridMap);

        // Sync .tres via typed API (skippable for --skip-floor-def parity).
        if (syncDef)
        {
            var def = ResourceLoader.Load<FloorDefinition>(paths.DefPath);
            FloorResourceSyncService.Apply(def, model, options);
            ResourceSaver.Save(def, paths.DefPath);
        }

        // Pack + save scene.
        var newPacked = new PackedScene();
        newPacked.Pack(scene);
        ResourceSaver.Save(newPacked, paths.ScenePath);
        scene.QueueFree();

        if (writeJson)
            WriteJson(model, paths.JsonPath);

        int walls = model.TileLayers.GetValueOrDefault("wall")?.Count ?? 0;
        int enemies = model.Entities.EnemySpawns?.Count ?? 0;
        return new FloorSceneResult(true, validation,
            $"Floor {floorNumber}: {walls} walls, {enemies} enemies generated");
    }

    public static FloorJsonModel GenerateToJson(int floorNumber)
        => FloorGenerationService.Generate(floorNumber);

    private static void WriteJson(FloorJsonModel model, string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(model.ToJson(indented: true));
    }

    private static (int Width, int Height) DimensionsFor(int floorNumber) => floorNumber switch
    {
        0 => (160, 160),            // GF ground is the full grid
        1 => (Layouts.Floor1Layout.Width, Layouts.Floor1Layout.Height),
        2 => (Layouts.Floor2Layout.Width, Layouts.Floor2Layout.Height),
        3 => (Layouts.Floor3Layout.Width, Layouts.Floor3Layout.Height),
        _ => (160, 160),
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorSceneWriterTest"`
Expected: PASS. Then verify the regenerated `Floor3F.tscn`/`.tres`/`.json` still pass the existing scene-layout tests:

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor3"`
Expected: existing Floor 3 layout tests PASS.

- [ ] **Step 5: Commit (include regenerated Floor 3)**

```bash
git add scripts/floor_tools/FloorSceneWriter.cs tests/floor_tools/FloorSceneWriterTest.cs scenes/game/floors/Floor3F.tscn scenes/game/floors/Floor3F.json resources/floors/Floor3F.tres
git commit -m "feat: add FloorSceneWriter orchestrator; regenerate Floor 3"
```

---

## Task 15: Wire dock buttons

Make the dock functional: Generate, Validate, Export JSON, Import JSON, Bake/Save Scene. The dock delegates to services; the only editor-specific call is `EditorInterface.GetResourceFilesystem().Scan()` after writes.

**Files:**
- Modify: `addons/sirius_floor_tools/SiriusFloorToolsDock.cs`

**Interfaces:**
- Consumes: `FloorSceneWriter`, `FloorGenerationService`, `FloorValidationService`, `FloorRegistry`, existing `TilemapJsonImporter`/`TilemapJsonExporter`, `EditorInterface`.

- [ ] **Step 1: Implement dock button handlers**

`addons/sirius_floor_tools/SiriusFloorToolsDock.cs`:

```csharp
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsDock : Control
{
    private OptionButton _floorOption;
    private RichTextLabel _resultsLabel;

    public override void _Ready()
    {
        _floorOption = GetNodeOrNull<OptionButton>("%FloorOption");
        _resultsLabel = GetNodeOrNull<RichTextLabel>("%ResultsLabel");

        ConnectButton("GenerateButton", OnGenerate);
        ConnectButton("ValidateButton", OnValidate);
        ConnectButton("ExportJsonButton", OnExportJson);
        ConnectButton("ImportJsonButton", OnImportJson);
        ConnectButton("BakeSaveButton", OnBakeSave);
        Log("Sirius Floor Tools ready.");
    }

    private void ConnectButton(string ownerName, System.Action handler)
    {
        var btn = GetNodeOrNull<Button>("%" + ownerName);
        if (btn != null)
            btn.Pressed += handler;
    }

    private int SelectedFloor => _floorOption?.Selected ?? 0;

    private void OnGenerate()
    {
        var result = FloorSceneWriter.Generate(SelectedFloor, new FloorSyncOptions());
        Log(result.Summary);
        foreach (var issue in result.Validation.Issues)
            Log($"  {(issue.Severity == Severity.Error ? "[x]" : "[!]")} {issue.Code}: {issue.Message}");
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
    }

    private void OnValidate()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        if (scene == null) { Log("No scene open to validate."); return; }
        var gridMap = scene.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene (no GridMap found)."); return; }

        var exporter = new TilemapJsonExporter();
        var model = exporter.ExportScene(gridMap);
        var (w, h) = (gridMap.GridWidth, gridMap.GridHeight);
        var result = FloorValidationService.Validate(model, w, h);
        Log(result.HasErrors ? "Validation FAILED" : "Validation passed");
        foreach (var issue in result.Issues)
            Log($"  {(issue.Severity == Severity.Error ? "[x]" : "[!]")} {issue.Code}: {issue.Message}");
    }

    private void OnExportJson()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene to export."); return; }
        var exporter = new TilemapJsonExporter();
        exporter.ExportToFile(gridMap, paths.JsonPath);
        Log($"Exported JSON to {paths.JsonPath}");
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
    }

    private void OnImportJson()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene to import into."); return; }
        var importer = new TilemapJsonImporter();
        var err = importer.ImportFromFile(paths.JsonPath, gridMap);
        Log(err == Error.Ok ? $"Imported from {paths.JsonPath}" : $"Import failed: {err}");
    }

    private void OnBakeSave()
    {
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        if (scene == null) { Log("No scene open to save."); return; }
        var packed = new PackedScene();
        packed.Pack(scene);
        ResourceSaver.Save(packed, scene.SceneFilePath);
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
        Log($"Saved scene {scene.SceneFilePath}");
    }

    private void Log(string message)
    {
        if (_resultsLabel != null)
            _resultsLabel.AddText(message + "\n");
    }
}
```

- [ ] **Step 2: Build and manually verify in-editor**

Run: `dotnet build Sirius.sln`
Open Godot → ensure plugin enabled → dock visible. Select "1F" → click Generate → verify the Results panel shows the summary and `scenes/game/floors/Floor1F.tscn` updates. Do not commit regenerated artifacts yet (Task 17 does that comprehensively).

- [ ] **Step 3: Commit**

```bash
git add addons/sirius_floor_tools/SiriusFloorToolsDock.cs
git commit -m "feat: wire Sirius Floor Tools dock buttons to services"
```

---

## Task 16: Headless CLI (agent/CI surface)

`tools/generate_floor.gd` + `FloorCli` C# entry. Replaces `python3 tools/floor*_maze_generator.py` + `tilemap_json_sync.py import`.

**Files:**
- Create: `scripts/floor_tools/FloorCli.cs`
- Create: `tools/generate_floor.gd`

**Interfaces:**
- Produces: `FloorCli.Run(string[] args) -> int` (exit code); the GDScript `extends SceneTree` entry parsing `--floor`, `--json-only`, `--skip-floor-def`, `--stair-dest`.

- [ ] **Step 1: Implement FloorCli**

`scripts/floor_tools/FloorCli.cs`:

```csharp
using Godot;
using System.Text.RegularExpressions;

namespace Sirius.FloorTools;

public static class FloorCli
{
    public static int Run(string[] args)
    {
        int floor = -1;
        bool jsonOnly = false;
        bool skipFloorDef = false;
        Vector2I? stairDest = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--floor": floor = int.Parse(args[++i]); break;
                case "--json-only": jsonOnly = true; break;
                case "--skip-floor-def": skipFloorDef = true; break;
                case "--stair-dest":
                    var m = Regex.Match(args[++i], @"^(\d+),\s*(\d+)$");
                    stairDest = new Vector2I(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                    break;
            }
        }

        if (floor < 0)
        {
            GD.PrintErr("Usage: --floor <0|1|2|3> [--json-only] [--skip-floor-def] [--stair-dest x,y]");
            return 1;
        }

        if (jsonOnly)
        {
            var model = FloorGenerationService.Generate(floor);
            var paths = FloorRegistry.Get(floor);
            using var file = FileAccess.Open(paths.JsonPath, FileAccess.ModeFlags.Write);
            file.StoreString(model.ToJson(indented: true));
            GD.Print($"Wrote {paths.JsonPath}");
            return 0;
        }

        var result = FloorSceneWriter.Generate(floor, new FloorSyncOptions(stairDest), writeJson: true);
        GD.Print(result.Summary);
        foreach (var issue in result.Validation.Issues)
            GD.PrintErr($"  [{issue.Severity}] {issue.Code}: {issue.Message}");
        return result.Success ? 0 : 1;
    }
}
```

Note: `--skip-floor-def` is honored by passing `syncDef: false` to `FloorSceneWriter.Generate` (the parameter is already part of the signature defined in Task 14):

```csharp
var result = FloorSceneWriter.Generate(floor, new FloorSyncOptions(stairDest), writeJson: true, syncDef: !skipFloorDef);
```

- [ ] **Step 2: Implement the GDScript entry**

`tools/generate_floor.gd` (mirrors `refresh_tilemap.gd`):

```gdscript
#!/usr/bin/env -S godot --headless --script
## Sirius floor generator (headless). Replaces tools/floor*_maze_generator.py.
##
## Usage:
##   godot --headless --path . --script tools/generate_floor.gd -- --floor <0|1|2|3> [options]
##
## Options:
##   --json-only          Write Floor*.json without touching scene/.tres
##   --skip-floor-def     Skip the FloorDefinition .tres sync
##   --stair-dest x,y     GF StairsUpDestinations override (parity with Python)

extends SceneTree

func _init():
	var args = OS.get_cmdline_user_args()
	var cli = load("res://scripts/floor_tools/FloorCli.cs")
	if cli == null:
		printerr("Failed to load FloorCli")
		quit(1)
		return
	var instance = cli.new()
	var code = instance.Run(args)
	quit(code)
```

- [ ] **Step 3: Build and smoke-test each floor**

Run: `dotnet build Sirius.sln`

Then for each floor 0-3:

Run: `godot --headless --path . --script tools/generate_floor.gd -- --floor 0` (and 1, 2, 3)
Expected: each prints a summary like "Floor 0: NNN walls, N enemies generated" and exits 0.

- [ ] **Step 4: Commit**

```bash
git add scripts/floor_tools/FloorCli.cs tools/generate_floor.gd
git commit -m "feat: add headless floor generation CLI"
```

---

## Task 17: Regenerate committed artifacts + docs + deprecation

Final cutover: regenerate all committed JSON/scenes/.tres from C#, update agent docs, deprecate Python.

**Files:**
- Modify: `scenes/game/floors/FloorGF.{json,tscn}`, `Floor1F.{json,tscn}`, `Floor2F.{json,tscn}`, `Floor3F.{json,tscn}`, `resources/floors/Floor{GF,1F,2F,3F}.tres`
- Modify: `.codex/skills/generate-sirius-floor/SKILL.md`, `.codex/skills/generate-sirius-floor/references/sirius-floor-workflow.md`
- Modify: `tools/floor0_maze_generator.py`, `tools/floor1_maze_generator.py` (deprecation header)
- Modify: `README.md`, `CLAUDE.md` / `AGENTS.md` (note new tool)

- [ ] **Step 1: Regenerate all floors from C#**

Run each (the headless CLI from Task 16):
```bash
godot --headless --path . --script tools/generate_floor.gd -- --floor 0
godot --headless --path . --script tools/generate_floor.gd -- --floor 1
godot --headless --path . --script tools/generate_floor.gd -- --floor 2
godot --headless --path . --script tools/generate_floor.gd -- --floor 3
```

- [ ] **Step 2: Verify the full test suite + layout tests pass against regenerated artifacts**

Run: `dotnet build Sirius.sln && dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorTools|FullyQualifiedName~TilemapJson|FullyQualifiedName~FloorLayout|FullyQualifiedName~Floor1|FullyQualifiedName~Floor2|FullyQualifiedName~Floor3|FullyQualifiedName~FloorG"`
Expected: all PASS. The scene-level `Floor*LayoutTest` suites are the runtime-correctness gate on the regenerated scenes.

- [ ] **Step 3: Confirm parity gate is green one final time**

Run: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGenerationParityTest"`
Expected: all four floors PASS. (The committed JSON is now C#-authored, so this is a self-consistency check.)

- [ ] **Step 4: Update agent skill + workflow docs**

In `.codex/skills/generate-sirius-floor/SKILL.md`:
- Step 4 → "Edit `FloorGenerationService` / `scripts/floor_tools/layouts/*Layout.cs` (C#) and regenerate via the headless CLI; cover with C# parity tests under `tests/floor_tools/`."
- Step 5 → "Regenerate via `godot --headless --path . --script tools/generate_floor.gd -- --floor N`. No separate JSON import is needed for generation."

In `.codex/skills/generate-sirius-floor/references/sirius-floor-workflow.md`:
- "Current Pipeline" → note `scripts/floor_tools/` is the generation source, `tools/generate_floor.gd` is the regenerate command.
- "Implementation Checklist" steps 4-5 and "Verification Commands" → replace the `python3`/`unittest` commands with the headless CLI and `dotnet test --filter "~FloorGeneration"`.

- [ ] **Step 5: Add deprecation headers to the Python generators**

Prepend a deprecation notice to `tools/floor0_maze_generator.py` and `tools/floor1_maze_generator.py` (after the docstring), pointing at the new path:

```python
# DEPRECATED: superseded by the Sirius Floor Tools addon + headless CLI.
# Regenerate floors with: godot --headless --path . --script tools/generate_floor.gd -- --floor N
# This file is retained only as a parity reference until C# generation is confirmed stable.
```

Do NOT delete the files (per the issue: deprecate only after parity is proven).

- [ ] **Step 6: Update README + CLAUDE.md tooling notes**

In `CLAUDE.md`/`AGENTS.md` "Development Commands", add the floor-generation command and note `tools/generate_floor.gd` replaces the Python generators. In `README.md`, if the floor toolchain is mentioned, update accordingly.

- [ ] **Step 7: Commit the cutover**

```bash
git add scenes/game/floors resources/floors .codex/skills/generate-sirius-floor tools/floor0_maze_generator.py tools/floor1_maze_generator.py README.md CLAUDE.md
git commit -m "feat: cut over to C# floor generation; deprecate Python generators"
```

---

## Self-Review Notes

- **Spec coverage:** Every acceptance criterion (plugin #1, dock actions #2, generate-without-Python #3, layer/entity writes #4, typed .tres #5, validation #6, JSON import/export #7, headless #8, Python deprecation #9) maps to a task above (Tasks 3, 15, 8-11, 14, 13, 12, 16, 17 respectively).
- **Dependency order:** Validation (Task 12) precedes FloorSceneWriter (Task 14) because the writer calls `FloorValidationService`. Generation tasks (8-11) precede both.
- **Type consistency:** `FloorSyncOptions`, `EnemySpec`, `FloorPaths`, `FloorSceneResult`, `ValidationIssue/Result`, `EntityView` are defined in their producing tasks and reused with the same names/signatures downstream.
- **Parity risk:** The supplemental-enemy planner (Task 6) and Floor 1/2 wall builders (Tasks 9-10) are the highest-risk ports; the parity tests are the lock. Golden-freshness steps (re-running Python, confirming no diff) ensure the committed JSON is a valid reference before each parity test is trusted.
