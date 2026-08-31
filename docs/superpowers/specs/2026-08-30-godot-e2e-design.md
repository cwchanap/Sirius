# Sirius Godot E2E Design

**Date:** 2026-08-30
**Status:** Approved

## Goal

Add a separately runnable Godot end-to-end test lane that launches the real
Sirius C# game in a child process and verifies menu-to-game navigation, a
battle interaction, and a shop purchase. Keep the existing GdUnit4 unit-test
workflow unchanged.

## Constraints

- Use [cwchanap/godot-e2e](https://github.com/cwchanap/godot-e2e) at commit
  `6f251e864e195aa54345475f21ca38b6dc4c0e6c`.
- Keep the test runner and client in a separate C# project; do not add it to
  `Sirius.sln`.
- Retain the current Godot .NET 4.6.2 and .NET 8 project setup.
- Do not add production-only E2E methods, nodes, autoloads, or input routes.
- Run the E2E workflow separately from `.github/workflows/tests.yml`.
- Run each scenario in a fresh child process. No shared state or retries.
- Treat the first hosted Godot .NET 4.6.2 run as a compatibility gate:
  upstream publishes its C# baseline for Godot .NET 4.5.1 and GdUnit4 6.x.

## Architecture

### Vendored addon

Vendor only the C#-path subset of the upstream addon at the pinned commit:
`addons/gdunit_e2e/{csharp,protocol,runtime,server}`, including
`runtime/bootstrap.gd.uid`. Add the upstream Apache-2.0 `LICENSE` and
`NOTICE` beside the vendored addon.

Do not vendor upstream `client/` or `gdunit/`: they support the upstream
GDScript test path, while `gdunit/gdunit_e2e_test_suite.gd` expects GdUnit4
6.x. Sirius uses GdUnit4 5.0.0 and the C# E2E job intentionally does not
install the GdUnit4 editor addon.

`.gitignore` must explicitly unignore the addon directory and its UID file:

~~~gitignore
!addons/gdunit_e2e/
!addons/gdunit_e2e/**/*.uid
test_output/
~~~

The Godot game project must exclude the addon client source so Godot.NET.Sdk
does not compile it into `Sirius.dll`:

~~~xml
<Compile Remove="addons/gdunit_e2e/csharp/**/*.cs" />
~~~

The E2E test source must also be excluded from `Sirius.csproj`, because the
root game project currently globs C# files recursively:

~~~xml
<Compile Remove="tests/e2e/**/*.cs" />
~~~

### Separate test project

Create `tests/e2e/Sirius.E2E.Tests.csproj` as a normal `Microsoft.NET.Sdk`
net8.0 test project. It references:

- `Microsoft.NET.Test.Sdk` 18.0.0, `gdUnit4.api` 5.0.0,
  `gdUnit4.test.adapter` 3.0.0, and `gdUnit4.analyzers` 1.0.0; and
- `../../addons/gdunit_e2e/csharp/GodotE2E.Client.csproj`.

It must not reference `Sirius.csproj`. The child process loads the compiled
game using Godot; the test process drives it only through the authenticated
loopback protocol. Its suite is a plain `[TestSuite]` class: it neither
inherits `Node` nor uses `[RequireGodotRuntime]`, so the adapter stays in
the testhost instead of launching a second Godot runtime. Preserve upstream's
`GdUnit0501` suppression for the intentionally BCL-only `E2EVector2`
client type.

### Test-only helper

Keep one small helper in the E2E test project for the protocol operations the
client intentionally leaves raw:

- find an in-tree node by visible text or stable scene-node name using
  `find_nodes`;
- wait for a node by path with `wait_for_node` and an explicit timeout;
- read `Vector2i` results from raw `get_property` or `call_method`
  responses and manually parse their `{ "_t": "v2i", "x", "y" }`
  form; and
- build that tagged JSON form for a `TryMovePlayer` argument.

The pinned client exposes only `E2EVector2` (`_t: "v2"`).
`GetPropertyAsync<T>` and `CallMethodAsync<T>` cannot deserialize a
`v2i` result, so these reads must remain raw `SendCommandAsync` calls.
Conversely, `CallMethodAsync<bool>` passes a prebuilt `JsonElement`
argument through unchanged and is used for `TryMovePlayer`.

Use existing wrappers for existing commands: `E2EGame.ClickNodeAsync` for
buttons and `CallMethodAsync<string>` for `get_meta("ItemId")`. Do not
invent an `emit_signal` command or a raw click wrapper.

This reuses existing UI semantics and game signals. It does not introduce
test-specific selectors or APIs into production scenes.

## Scenario contracts

Every scenario uses `E2EGame.RunAsync`, a non-headless child, and the
current project root. The upstream runner owns authenticated connection,
failure artifact capture, graceful shutdown, and forced cleanup if needed.
Each child launch sets `E2ELaunchOptions.Timeout` to 30 seconds for the
port-file/hello handshake only. Every raw node/property wait declares its own
bounded server and transport timeout; the launch timeout is not a scenario
budget.

The two movement routes begin at FloorGF's committed player start `(8, 50)`.
They are deliberately literal baseline routes: a false pre-target move is a
floor-layout regression, not a reason to add a route planner.

### Main menu navigation

1. Launch `res://scenes/ui/MainMenu.tscn`.
2. Assert that the `SIRIUS` wordmark and `New Game` button are present.
3. Click `New Game`.
4. Wait for `res://scenes/game/Game.tscn` to become the current scene.
5. Wait for `/root/Game/FloorGF/GridMap` to exist.

This proves that the normal scene transition reaches a fully initialized
exploration game, rather than only loading a menu scene.

### Battle lifecycle

1. Launch `res://scenes/game/Game.tscn` directly and wait for
   `/root/Game/FloorGF/GridMap`.
2. Wait for `EnemySpawn_Orc_East` and read its raw `GridPosition`.
   Move through the committed FloorGF corridor with
   `TryMovePlayer`: `R6, U1, R7, U10, R24, U15, R7, U4, R5, D28, R17`.
   The one-cell north detour avoids the authored unopened
   `TreasureBox_GF_EntranceCache` at `(15, 50)`. Every corridor move must
   return true.
3. Read the actual player position, calculate the cell immediately south of
   it, and call `InternalGridToTilemapCoords` through the raw `v2i`
   helper. It must equal `EnemySpawn_Orc_East.GridPosition` before the final
   movement attempt.
4. Call `TryMovePlayer(Down)` and assert it returns false, then wait for
   `IsInBattle` to become true. This reaches GridMap's real
   `CellType.Enemy → EnemyEncountered` gate without a synthetic signal.
5. Assert the visible `EnemyName` and `EnemyLevel` labels are `Orc` and
   `Lv 2`, respectively. The area fallback at this coordinate cannot
   produce an Orc, so this proves the placed spawn was selected.
6. Find and click the visible `Begin Battle` button.
7. Assert the named `AutomaticCombatPanel` is visible.
8. Click the visible `Escape` button; do not send the `pause_menu` input
   action, which is also bound to Escape.
9. Wait for `/root/Game/GameManager.IsInBattle` to become false and for the
   battle screen to leave the tree.

The scenario exercises the production GridMap movement/encounter gate,
`Game`, `GameManager`, `UIScreenHost`, and `BattleManager` while
avoiding random battle completion and its autosave behavior.

### Shop purchase

1. Launch `res://scenes/game/Game.tscn` directly and wait for the GridMap.
2. Wait for `NpcSpawn_Shopkeeper` and read its raw `GridPosition`.
   Move `R4, U3`; every move must return true.
3. Read the actual player position, calculate the cell immediately north of
   it, and verify its `InternalGridToTilemapCoords` value equals the named
   shopkeeper spawn before moving into it.
4. Call `TryMovePlayer(Up)`, assert it returns false, and wait for the
   interaction dialogue. This reaches the real `CellType.Npc → NpcInteracted`
   gate.
5. Find and click the dialogue choice `Browse your wares.`.
6. Assert the `Mira's General Store` surface and parse the visible
   `GoldLabel` balance.
7. Find the Buy control whose existing `ItemId` metadata equals
   `health_potion`. From that row, read its rendered `Ng` price label,
   then click the button.
8. Wait for the computed post-purchase `GoldLabel` text, then parse it and
   assert the balance decreased by the rendered row price rather than fixed
   starting gold or item values.
9. Click the visible shop `Close` button and assert
   `/root/Game/GameManager.IsInNpcInteraction` becomes false.

The purchase remains inside the short-lived child process. It neither writes
a save nor changes the developer's local player data.

## Failure behavior

There are no retries. A timeout, missing node, wrong visible state, or
unexpected protocol result fails the test directly.

The E2E runner writes its diagnostics under `test_output/csharp/**`:

- `screenshot.png`
- `scene_tree.json`
- `engine_logs.json`
- `stdout.log`
- `stderr.log`

Add `test_output/` to `.gitignore`. GitHub Actions uploads
`TestResults/` for every run and `test_output/` only when the E2E job
fails.

## GitHub Actions workflow

Create `.github/workflows/godot-e2e.yml` with the same trigger surface and
draft-PR guard as the existing unit-test workflow:

- pushes to `main` and `develop`;
- pull requests targeting those branches;
- manual `workflow_dispatch`; and
- no E2E job for a draft pull request.

The Linux job uses `ubuntu-latest`, Godot .NET 4.6.2, .NET 8, Git LFS
checkout, and Xvfb. It reuses the unit workflow's cached/manual Godot install
but does not create its `godot-headless` wrapper. Upstream's published C#
CI baseline is Godot .NET 4.5.1, so this hosted 4.6.2 run is the compatibility
signal for the integration. It follows this order:

1. Restore the game and E2E test projects.
2. Build `Sirius.sln` in **Debug** before launching a Godot child.
3. Import the project with the real Godot executable using
   `--headless --editor --quit` so a clean runner has `.godot/` assets.
   Do not download the GdUnit4 addon for this job.
4. Build `tests/e2e/Sirius.E2E.Tests.csproj`.
5. Run the E2E project through
   `xvfb-run --auto-servernum --server-args="-screen 0 1280x720x24"` with
   `GODOT_BIN` set to the installed non-headless Godot executable. Do not
   pass `test.runsettings`, which configures a headless wrapper.
6. Upload E2E test results and failure diagnostics.

The job deliberately does not run coverage, GdUnit4's unit suite, or a
Windows matrix. Existing `tests.yml` already owns the unit suite; add
Windows coverage only when its hosted run is requested and maintained.

## Developer workflow

Document this command in `README.md`:

~~~bash
dotnet build Sirius.sln --configuration Debug
GODOT_BIN=/path/to/Godot dotnet test tests/e2e/Sirius.E2E.Tests.csproj
~~~

The command is separate from `dotnet test Sirius.sln`. It launches visible
Godot children, so Linux needs an X display such as Xvfb and `GODOT_BIN`
must name the real executable rather than a headless wrapper.

## Delivery order

1. Make the main-menu navigation scenario runnable first.
2. Temporarily break one asserted UI value, confirm the run fails as an
   assertion rather than a transport error, and confirm all five runner
   diagnostics are written beneath
   `test_output/csharp/<suite>/<test>/`. Revert the intentional failure.
3. Add the movement-driven battle scenario, then the shop scenario.
4. After the workflow is published and a remote run is authorized, perform one
   intentional failing dispatch to verify the failure-only artifact upload,
   then restore the passing assertion.

## Files

- Add: `addons/gdunit_e2e/{csharp,protocol,runtime,server}/**`,
  `addons/gdunit_e2e/LICENSE`, `addons/gdunit_e2e/NOTICE`
- Add: `tests/e2e/Sirius.E2E.Tests.csproj`
- Add: `tests/e2e/SiriusGameplayE2ETest.cs`
- Add: `tests/e2e/E2EUi.cs`
- Add: `.github/workflows/godot-e2e.yml`
- Modify: `Sirius.csproj`
- Modify: `.gitignore`
- Modify: `README.md`
