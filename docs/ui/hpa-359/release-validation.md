# HPA-359 Sirius UI Release Validation

## Final disposition

The final HPA-359 verification is green for the build, focused release suites,
and full suite. The Task 2 production walkthrough was completed with the
shipped Godot MCP runtime bridge `0.1.4`; Tasks 3 and 4 changed only test and
documentation files, so Task 5 reuses that final walkthrough and its existing
captures rather than repeating the production journey or recapturing images.

The compact Inventory remains usable at `640×360`, although its section-heading
chrome is tight/clipped at the minimum viewport. The corrected compact Prompt
has readable text and reachable actions. These are recorded as follow-up
visual concerns, not as unresolved HPA-359 blockers.

The earlier bridge `0.1.3` transport limitation is historical and superseded by
the completed `0.1.4` walkthrough below; it is not an outstanding release
status.

## Automated baseline

- Build command: `dotnet build Sirius.sln` — exit 0, 0 errors, summary 269 warnings. Diagnostic command `dotnet build Sirius.sln --no-restore --no-incremental` also exited 0 with 0 errors and exactly 268 compiler/analyzer warnings, matching the Task 1 pre-existing baseline; its warning log contained no `GameplayPauseHostTest`, `SiriusPromptTest`, or other HPA-changed-file warning. The extra restore-time count is `NU1900` vulnerability-feed unavailability displayed at solution/project contexts.
- Focused release command:

  ```text
  dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt|FullyQualifiedName~Hpa374RuntimeSmokeTest"
  ```

  Final result: 607 passed, 0 failed, 0 skipped (`607/607/0`); restore-time `NU1900` vulnerability-feed warnings and known GdUnit orphan-node warning noise were present; no test failures. The earlier 605-test baseline predates the Prompt regression and hosted joypad characterization now included in this branch.
- Full-suite command: `dotnet test Sirius.sln --settings test.runsettings.local` — final result: 1498 passed, 0 failed, 0 skipped (`1498/1498/0`); restore-time `NU1900` vulnerability-feed warnings and known GdUnit orphan-node warning noise were present; no test failures.
- The first non-escalated focused invocation stopped before discovery because the VSTest/Godot adapter could not obtain its local-socket permission (`0 passed, 1 failed, 0 skipped`); the escalated rerun above is the authoritative final result and is an environment startup limitation, not a product failure.

## Existing runtime-backed coverage reused

- Dialogue → Shop / Heal: `GameplayPauseHostTest.NpcShopOutcome_HostsAsBlockingScreenWithoutPausingTree` and `GameplayPauseHostTest.NpcHealOutcome_HostsAsBlockingScreenWithoutPausingTree` cover the real `Game.tscn` composition, HUD visibility policy, gameplay blocking, no tree pause, and restoration on close.
- Pause children / focus restoration: `GameplayPauseHostTest.HostedSaveLoad_CloseReturnsFocusToSamePause`, `HostedSaveLoad_SaveAndLoadHostLogicalPauseChildrenAndRestoreExistingPause`, `HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause`, and `PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause` cover shared Pause-child ownership and focus restoration.
- Save overwrite Prompt retention / topmost Cancel: `GameplayPauseHostTest.HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad` and `GameplayPauseHostTest.HostedOverwrite_ActiveChildConsumesCancelBeforeSaveLoad` cover nested Prompt retention and topmost Cancel.
- Return-to-title teardown/navigation: `GameplayPauseHostTest.GameSceneHostPrepareForTeardown_ClosesEntryAndRestoresIncomingState` covers closing the hosted entry and restoring incoming state before teardown; `GameplayPauseHostTest.PauseReturnToTitle_PrimaryRequestsNavigationOnce` covers the confirmation's one-shot navigation request.
- Long Dialogue / Puzzle / corrupt-save: `DialogueScreenControllerTest.CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice`, `GameTest.CorruptedSave_*`, `PuzzleRiddleScreenControllerTest`, and `GameInputLifecycleTest` remain the automated owners. These contracts were not duplicated as manual acceptance steps.

## Hosted joypad characterization

`dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.HostedPause_JoypadNavigationAndCancelRestoreGameplay"` — passed: 1, failed: 0, skipped: 0. The real hosted Game scene opens Pause through configured `pause_menu`, moves focus with injected D-pad Down, closes with injected joypad B/`ui_cancel`, and restores an unpaused, unblocked gameplay state. The test restores only its temporary `ui_down`/`ui_cancel` events in `finally`, preserving pre-existing mappings. No gameplay joypad binding or production change was added.

## Final production walkthrough reused from Task 2

- Runtime: Godot `4.6.2.stable.mono.official.71f334935`, shipped runtime bridge `0.1.4`, actual production game process/root viewport. Native process-targeted macOS `CGEvent` input was used only where the bridge has no keyboard action; no debug movement or test seam was used.
- Main Menu → Game: the production `NewGameButton` was activated from `root/MainMenu/MainMenuContent/SafeFrame/MenuRail/NewGameButton`. The running scene became `Game.tscn`; output logged `Game scene loaded`, authored FloorGF start `(8, 50)`, `Floor 0 loaded: Ground Floor`, and `Floor 'Ground Floor' ready for gameplay`.
- Movement → goblin → Battle: from `(8, 50)`, six Right inputs reached `(14, 50)`; the authored treasure box at `(15, 50)` correctly rejected movement. Five Up inputs reached `(14, 45)`, and ten Right inputs reached the authored first Goblin at `(24, 45)`. Output logged `Enemy encountered at position: (24, 45)`, `Battle started against Goblin! IsInBattle: True`, and `Starting battle with Goblin`.
- Battle/result: the production `BeginBattleButton` was activated at `root/Game/UI/UIScreenHost/ScreenLayer/BattleScreen/SafeFrame/BattleContent/ActorField/CenterFlow/PreparationPanel/PreparationContent/BeginBattleButton`. Auto-combat ended with `BattleManager.EndBattle called: playerWon = True`, `VICTORY`, and `Battle ended. Player won: True. IsInBattle: False`. The result Continue action returned to exploration and logged `Enemy removed from position: (24, 45)`.
- Game → Main Menu: the production Pause → Return to Title confirmation flow was completed in a fresh `1280×720` run. Output logged a second `Main Menu loaded`, and the final root capture showed the actual Main Menu scene replacement.
- Save/overwrite path: in the compact run, the real nested Save flow populated disposable manual slot 0, reopened it, and opened the overwrite Prompt. The generated disposable slot was removed afterward; original saves were restored byte-for-byte.

## Real-window visual checks

- Inventory `1280×720`: passed in the actual game process; content, item art, tabs, and Close control were visible and usable.
- Inventory `640×360`: passed visible-usability criteria in the actual game process; content, item art, tabs, and Close control remained visible/reachable. Section-heading chrome is tight/clipped at the minimum viewport and is retained as a follow-up concern.
- Battle/result `1280×720`: passed; the Victory/result composition was captured from the actual production root viewport.
- Save/overwrite Prompt `640×360`: passed after the Task 2 RED→GREEN fix; the body text is readable on one line and both Cancel and Overwrite actions are reachable.
- Main Menu return `1280×720`: passed; the final capture shows the actual scene replacement back to Main Menu.

## Prompt defect, regression, and fix

The first corrected compact Prompt capture exposed a real layout defect: the
message body measured one pixel wide and rendered one character per line.
`SiriusPromptTest.CompactMessageRetainsReadableLineWidth` reproduced the issue
RED with `message.Size.X == 1` (expected greater than `300`) before the
production edit. The minimal GREEN fix was the authored shared-shell change
`size_flags_horizontal = 3` on `BodyHost` in
`scenes/ui/components/SiriusModalShell.tscn`.

Focused GREEN verification recorded by Task 2: `SiriusPromptTest` passed 11,
and `SiriusModalShellTest` plus `SaveLoadScreenSceneTest` passed 26, with no
failures. This was the only HPA-359 production edit; it was test-first and
limited to the shared shell layout defect.

## Runtime observations and cleanup

- Final production output contained 43 deterministic warning headers: existing invalid-UID loader fallbacks and dynamic `Music`/`SFX` audio-bus creation warnings. No `ERROR:` headers, managed exceptions, or new product runtime errors were observed.
- No duplicate battle activation or stuck result presentation was observed. Victory cleared `IsInBattle`, Continue returned to exploration, and Return to Title completed the scene replacement. The compact Inventory heading concern is visual only; required content and controls remained usable.
- The original user saves were restored byte-for-byte after removing the disposable slot: `save_slot_0.json` SHA-256 `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`; `save_slot_1.json` SHA-256 `b291752bbee497b753534f83b9865a7062cc586d1e2b95dbf38fa553d35a654e`.
- After the walkthrough, the shipped runtime bridge was uninstalled, `.godot-mcp` was absent, bridge autoload entries were absent, and `project.godot` matched base SHA-256 `43c075b83c8f07e82b2e217f1aa6671378115e497bec5947bd459f61bd848f6e`. Task 2 removed its session-generated `.import` copies; later Godot/test access regenerated the five current `.png.import` files as ignored, untracked local cache, not PR content.

## Legacy-path and documentation audit

- The exact scoped audit found no executable `DialogueDialog`, `SaveLoadDialog`, `SaveOverwriteConfirmationController`, `DraggablePanel`, `Player HUD`, or Settings-stub leftovers under `scripts/`, `scenes/`, or `project.godot`; no executable path was removed and no replacement-protection gate was triggered.
- Intentional `AcceptDialog` matches remain only as current compatibility/test references and historical HPA-376 evidence/comments; no `ConfirmationDialog` production match was found. Dynamic Dialogue/Shop/Inventory rows and current input-compatibility code were retained.
- `git ls-files -- '*.uid'` reported 0 tracked UID paths; ignored/generated sidecars were left untouched.
- `CLAUDE.md` now describes the scene-authored hosted Battle/UI path, current controller names, hosted Battle-result state, and shared `SiriusPrompt` ownership. `docs/PRD.md` now records current Settings completion/access and hosted NPC controller names while preserving the April `~65%` snapshot and historical v1.2 row. The HPA-376 introduction now describes hosted `ui_cancel`/`UIScreenHost` ownership while retaining compatibility context; its lifecycle matrix and historical evidence were not rewritten.
- No HPA-375 or HPA-541 work was added. No global UI architecture, second host, E2E/screenshot framework, or gameplay/controller binding was added; the only production change was the test-first shared-shell Prompt fix above.

## Evidence screenshots

Exactly the five existing real-window captures are retained:

| Path | Dimensions | SHA-256 |
| --- | --- | --- |
| `docs/ui/hpa-359/evidence/battle-result-1280x720.png` | `1280×720` | `28270e3a8839cc0fcf70f60f43581c062bbcff8ff9962f4bd57a66218b551d42` |
| `docs/ui/hpa-359/evidence/inventory-1280x720.png` | `1280×720` | `624d96b08fdfde8432d31ea8a03ffbb5d7414a376553c824a0251f3812b4dc0c` |
| `docs/ui/hpa-359/evidence/inventory-640x360.png` | `640×360` | `3a4a374b9d135dcdfc27b057e93155947c3c85d2e207bad65c83153c3f6e58b0` |
| `docs/ui/hpa-359/evidence/save-overwrite-prompt-640x360.png` | `640×360` | `c697661dfb5d00006d62f94bf1c50a4f49b072f850f33d92f34803063093c5d5` |
| `docs/ui/hpa-359/evidence/main-menu-return-1280x720.png` | `1280×720` | `c8b2345644416949032cc36d0cb925a4e755ca19f9eab2b2e8e5c8e48f654dde` |
