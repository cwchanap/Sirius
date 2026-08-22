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
- Actual Main Menu → Game scene replacement: Earlier direct-binary attempt reached `Game.tscn`; the required rescue LaunchServices launches reached `Main Menu loaded` but exposed no controllable real window, so this seam was not safely repeated.
- FloorGF new-game start `(8, 50)`: A live Godot MCP runtime-control session pressed the real New Game button and again logged the authored start `(8, 50)`.
- Movement → goblin `(24, 45)` → Battle: Earlier direct-binary attempt reached the authored goblin and Battle result; the supported Godot MCP surface has no input-injection action beyond allowlisted button presses, so this follow-up could not repeat movement safely.
- Actual Game → Main Menu scene replacement: not reached; no claim made.

## Real-window visual checks
- Inventory 1280×720: blocked; no live Godot framebuffer exposed.
- Inventory 640×360: not attempted because the real window was unusable.
- Battle/result 1280×720: earlier temporary diagnostic capture was visually inspected, but no allowed repository evidence screenshot was produced. The official Godot MCP capture timed out waiting for `RenderingServer.frame_post_draw`; a transient diagnostic bridge substitution returned a native `1280×720` PNG but was not retained as release evidence.
- Save/overwrite Prompt 640×360: not attempted; no disposable save created and no user save modified.

## Runtime observations
- Warnings/errors: deterministic invalid-UID loader fallbacks and dynamic `Music`/`SFX` audio-bus warnings; no `ERROR:`/`GD.PushError` observed in the reached path or rescue launches through Main Menu. The live Godot MCP session connected and controlled the scene, but its official screenshot command timed out at `RenderingServer.frame_post_draw`; remaining seams are unverified.
- Duplicate activation: no production duplicate activation was observed; the rescue desktop blocker prevented further flow.
- Stuck focus/pause/input/cursor/HUD state: unverified because public Accessibility exposed zero Godot windows/UI elements.

## Legacy-path and documentation audit
- Executable leftovers:
- CLAUDE.md:
- docs/PRD.md:
- HPA-376 Cancel intro:

## Evidence screenshots
- paths: none. The five allowed evidence paths remain absent; `/private/tmp/hpa359-rescue-desktop.png` was a desktop diagnostic showing Chrome, not release evidence. The transient MCP diagnostic PNG was not copied into the repository.
