# HPA-359 Sirius UI Release Validation

## Automated baseline
- Build: `dotnet build Sirius.sln` — passed: 2 projects, 0 errors, 1 warning (`NU1900`, NuGet vulnerability feed unavailable).
- Focused release suites: `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt|FullyQualifiedName~Hpa374RuntimeSmokeTest"` — passed: 605, failed: 0, skipped: 0; 2 `NU1900` warnings.
- Full suite: `dotnet test Sirius.sln --settings test.runsettings.local` — passed: 1496, failed: 0, skipped: 0; 2 `NU1900` warnings.

## Existing runtime-backed coverage reused
- Dialogue → Shop / Heal: `GameplayPauseHostTest.NpcShopOutcome_HostsAsBlockingScreenWithoutPausingTree` and `GameplayPauseHostTest.NpcHealOutcome_HostsAsBlockingScreenWithoutPausingTree` cover real `Game.tscn` Dialogue→Shop/Heal composition, HUD visible→hidden, gameplay block, no tree pause, and restore on close.
- Pause children / focus restoration: `GameplayPauseHostTest.HostedSaveLoad_CloseReturnsFocusToSamePause` and adjacent `HostedSaveLoad_SaveAndLoadHostLogicalPauseChildrenAndRestoreExistingPause`, `HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause`, and `PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause` cases cover child ownership and same-Pause focus restoration.
- Save overwrite Prompt retention / topmost Cancel: `GameplayPauseHostTest.HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad` and `GameplayPauseHostTest.HostedOverwrite_ActiveChildConsumesCancelBeforeSaveLoad` cover Prompt retention and topmost Cancel.
- Long Dialogue / Puzzle / corrupt-save: `DialogueScreenControllerTest.CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice`; `GameTest.CorruptedSave_*`; `PuzzleRiddleScreenControllerTest` plus `GameInputLifecycleTest` cover compact/cancel Puzzle/Riddle behavior.

## Hosted joypad characterization
- Result:

## One production walkthrough
- Actual Main Menu → Game scene replacement:
- FloorGF new-game start `(8, 50)`:
- Movement → goblin `(24, 45)` → Battle:
- Actual Game → Main Menu scene replacement:

## Real-window visual checks
- Inventory 1280×720:
- Inventory 640×360:
- Battle/result 1280×720:
- Save/overwrite Prompt 640×360:

## Runtime observations
- Warnings/errors:
- Duplicate activation:
- Stuck focus/pause/input/cursor/HUD state:

## Legacy-path and documentation audit
- Executable leftovers:
- CLAUDE.md:
- docs/PRD.md:
- HPA-376 Cancel intro:

## Evidence screenshots
- paths:
