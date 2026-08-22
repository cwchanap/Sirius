# HPA-359 Sirius UI Release Validation

## Automated baseline
- Build: `dotnet build Sirius.sln` — passed: 2 projects, 0 errors, 1 warning (`NU1900`, NuGet vulnerability feed unavailable).
- Focused release suites: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt|FullyQualifiedName~Hpa374RuntimeSmokeTest"` — passed: 605, failed: 0, skipped: 0; 2 `NU1900` warnings.
- Full suite: `dotnet test Sirius.sln --settings test.runsettings.local` — passed: 1496, failed: 0, skipped: 0; 2 `NU1900` warnings.

## Existing runtime-backed coverage reused
- Dialogue → Shop / Heal: `GameplayPauseHostTest.NpcShopOutcome_HostsAsBlockingScreenWithoutPausingTree` and `GameplayPauseHostTest.NpcHealOutcome_HostsAsBlockingScreenWithoutPausingTree` cover real `Game.tscn` Dialogue→Shop/Heal composition, HUD visible→hidden, gameplay block, no tree pause, and restore on close.
- Pause children / focus restoration: `GameplayPauseHostTest.HostedSaveLoad_CloseReturnsFocusToSamePause` and adjacent `HostedSaveLoad_SaveAndLoadHostLogicalPauseChildrenAndRestoreExistingPause`, `HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause`, and `PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause` cases cover child ownership and same-Pause focus restoration.
- Save overwrite Prompt retention / topmost Cancel: `GameplayPauseHostTest.HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad` and `GameplayPauseHostTest.HostedOverwrite_ActiveChildConsumesCancelBeforeSaveLoad` cover Prompt retention and topmost Cancel.
- Return-to-title teardown/navigation: `GameplayPauseHostTest.GameSceneHostPrepareForTeardown_ClosesEntryAndRestoresIncomingState` covers closing the hosted entry and restoring incoming HUD/cursor/gameplay state before teardown; `GameplayPauseHostTest.PauseReturnToTitle_PrimaryRequestsNavigationOnce` covers the return-to-title confirmation's one-shot navigation request.
- Long Dialogue / Puzzle / corrupt-save: `DialogueScreenControllerTest.CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice`; `GameTest.CorruptedSave_*`; `PuzzleRiddleScreenControllerTest` plus `GameInputLifecycleTest` cover compact/cancel Puzzle/Riddle behavior.

## Hosted joypad characterization
- Result:

## One production walkthrough
- Runtime: Godot `4.6.2.stable.mono.official.71f334935`, launched through the shipped Godot MCP runtime bridge `0.1.3` with `runtimeControl=true` at `1280×720`.
- Main Menu → Game: pressed `root/MainMenu/MainMenuContent/SafeFrame/MenuRail/NewGameButton` through the production runtime bridge. Fresh output logged `Game scene loaded`, `📍 Player spawn position: (8, 50)`, `✅ Floor 0 loaded: Ground Floor`, and `✅ Floor 'Ground Floor' ready for gameplay`.
- Movement → encounter: used native macOS `CGEvent.postToPid` only because MCP has no keyboard action. From `(8, 50)`, six Right inputs reached `(14, 50)` (the authored treasure box at `(15, 50)` correctly rejected movement), five Up inputs reached `(14, 45)`, and ten Right inputs reached the authored first Goblin at `(24, 45)`. Output logged `Enemy encountered at position: (24,45)`, `Battle started against Goblin! IsInBattle: True`, and `Starting battle with Goblin`.
- Battle: pressed `root/Game/UI/UIScreenHost/ScreenLayer/BattleScreen/SafeFrame/BattleContent/ActorField/CenterFlow/PreparationPanel/PreparationContent/BeginBattleButton` through MCP. Auto-combat logged player and Goblin attacks, `BattleManager.EndBattle called: playerWon = True`, `🎉 VICTORY! Hero wins the battle!`, and `Battle ended. Player won: True. IsInBattle: False`. Captured the result at `1280×720`, then pressed `root/Game/UI/UIScreenHost/ScreenLayer/BattleScreen/SafeFrame/BattleContent/ActorField/CenterFlow/ResultPanel/ResultContent/ContinueButton`; output logged `Enemy removed from position: (24, 45)`.
- Inventory / compact resize: native process-targeted I was attempted after a fresh Game load. The MCP root capture timed out after 10 seconds and subsequent runtime commands reported `Runtime bridge reconnect-required`; restarting and retrying produced the same bridge disconnect. Native Accessibility/System Events exposed no Godot window, and therefore no safe native resize path to `640×360` existed. No inventory or compact screenshot is claimed.
- Save / overwrite / return to title: Escape did not reach the headless process and no native Godot window was exposed, so Pause could not be opened safely. No disposable slot was populated. The pre-walkthrough save snapshot was restored byte-for-byte after the run; exact hashes are recorded in the Task 2 report. Game → Main Menu was not reached; no claim made.

## Real-window visual checks
- Inventory 1280×720: attempted through the real Game input path; capture timed out when the runtime bridge disconnected. No screenshot file is claimed.
- Inventory 640×360: not attempted. No native Godot window was exposed for resize, and no screenshot harness or bridge patch was used.
- Battle/result 1280×720: passed. The controller ruling treats the production root-viewport MCP capture as real-window evidence because it came from the actual game process, not a SubViewport fixture. Evidence: `docs/ui/hpa-359/evidence/battle-result-1280x720.png` (`1280×720`, RGBA PNG).
- Save/overwrite Prompt 640×360: not reached because native Escape could not open Pause and no window was available for resize. No user save was modified.

## Runtime observations
- Warnings: fresh runs repeated deterministic invalid-UID loader fallbacks and `Music`/`SFX` audio-bus creation warnings. These are classified as existing normal-flow warnings, not Task 2 regressions.
- Errors: no `ERROR:`, `GD.PushError`, or managed `Exception` appeared in the fresh Main Menu → Game → encounter → Battle → result → exploration path. The 10-second screenshot timeout and later `Runtime bridge reconnect-required` response are classified as MCP/runtime transport limitations; they did not produce a game-process error in debug output.
- Duplicate/stuck state: no duplicate battle activation was observed. Victory cleared `IsInBattle`, the result Continue action returned to exploration, and the defeated enemy was removed. Pause/save/focus/cursor/HUD compact behavior remains unverified because no native Godot window was exposed and Escape could not be delivered.

## Save and bridge cleanup
- Before launch, copied existing saves to `/private/tmp/hpa359-task2-save-backup.N1KR92/saves/`.
- Restored both files after stopping the game. `save_slot_0.json`: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; `save_slot_1.json`: `b291752bbee497b753534f83b9865a7062cc586d1e2b95dbf38fa553d35a654e`. Backup and restored hashes matched exactly.
- Stopped the Godot process, uninstalled runtime bridge `0.1.3`, removed generated capture directory from the worktree, and restored `project.godot` exact SHA-256 `43c075b83c8f07e82b2e217f1aa6671378115e497bec5947bd459f61bd848f6e`.

## Legacy-path and documentation audit
- Executable leftovers:
- CLAUDE.md:
- docs/PRD.md:
- HPA-376 Cancel intro:

## Evidence screenshots
- `docs/ui/hpa-359/evidence/battle-result-1280x720.png` — `1280×720`, RGBA PNG, SHA-256 `28270e3a8839cc0fcf70f60f43581c062bbcff8ff9962f4bd57a66218b551d42`.
- The inventory, compact inventory, save-overwrite prompt, and optional Main Menu return evidence paths remain absent because the exact runtime/native interaction blockers above prevented safe capture. No fabricated or resized substitute was added.
