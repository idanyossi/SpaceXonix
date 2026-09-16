# SpaceXonix Development Progress

## Project Snapshot

- Unity: 6000.3.20f1 (Unity 6.3 LTS), URP
- Targets: Windows PC and Android; 1080x1920 portrait reference resolution
- Active branch: `main`
- Main gameplay scene: `Assets/SpaceXonix/Scenes/Game.unity`
- Default logical board: configurable 54 x 96 cells
- Current phase: Phase 7 complete; Phase 8 not started
- Latest completed feature: laser hazards

## Phase Progress

1. **COMPLETE** — Project audit and implementation plan (`2a916d0`, `373442d`)
2. **COMPLETE** — Foundation, input, and cardinal player movement (`148beb9`)
3. **COMPLETE** — Board, trail, deterministic territory capture, and occupancy (`668a177`)
4. **COMPLETE** — Lives, authoritative failure pipeline, and last-safe-cell respawn (`172df88`)
5. **COMPLETE** — Pooled Basic, Linear, and Unstable standard enemy framework (`aa9fd91`)
6. **COMPLETE** — Pooled Volatile Alien, protected detonation, enemy/player blast effects, and territory destruction (`84391c1`)
7. **COMPLETE** — Pooled horizontal/vertical Laser Hazards with warning, firing, and cooldown states
8. **NOT STARTED** — Scoring + Large-Capture Multipliers
9. **NOT STARTED** — Power Meter + Power Shot
10. **NOT STARTED** — Shield + Freeze + Arena Tilt
11. **NOT STARTED** — Campaign Stages + Progression
12. **NOT STARTED** — Roguelite Upgrades
13. **NOT STARTED** — Stage Modifiers
14. **NOT STARTED** — Alien Core Boss
15. **NOT STARTED** — UI + Menus + HUD
16. **NOT STARTED** — Android Controls
17. **NOT STARTED** — Audio
18. **NOT STARTED** — Asset Acquisition/Integration
19. **NOT STARTED** — Visual Polish + VFX + Cinemachine
20. **NOT STARTED** — Final QA + Profiling + Submission Cleanup

## Current Architecture

- `BoardModel` is the gameplay authority for a configurable logical grid (default 54 x 96).
- The safe structural perimeter is excluded from captured-playable-area percentage.
- Capture uses iterative deterministic flood fill and selects the smallest valid alien-free region.
- `BoardManager` receives logical active-enemy occupancy without depending on concrete enemy subclasses.
- `GameManager.ReportPlayerFailure(...)` is the sole atomic life-loss entry point; respawn revalidates a current safe cell before restoration.
- `PlayerController` owns one explicit control state (`SafeIdle`, `SafeMoving`, `ExposedMoving`, `Respawning`, or `GameOver`) and one authoritative cardinal movement direction/logical position.
- Player and enemy movement remains cardinal/grid-compatible; standard enemies are pooled.
- Enemy registration, occupancy, movement pause/resume, and pooled reset are centralized through `EnemyManager` and `EnemyController`.
- Volatile collision/explosion orchestration stays in `EnemyManager`; territory removal stays authoritative in `BoardManager`/`BoardModel` and never directly changes an active trail.
- `LaserManager` advances independent axis-aligned emitters; `LaserEmitter` routes firing contact through the authoritative `GameManager` failure pipeline.
- Laser warning and beam presentations are pooled, and lasers intentionally do not affect enemies, Volatile behavior, territory, or unfinished trails.

## Current Test State

- EditMode discovered: 119
- Passed: 119
- Failed: 0
- Coverage includes the explicit player control-state lifecycle, held safe movement, persistent exposed movement, capture exit, input reversal rules, board/trail/capture/destruction, the complete atomic death/respawn lifecycle, safe-cell restoration, captured-territory preservation, duplicate failure rejection for every failure reason, repeated deaths, Game Over, all enemy behavior, manager occupancy, pooling/reset, Volatile protection/detonation, laser timing/geometry/presentation reuse, hazard isolation, and authoritative player damage.

## Respawn Reliability Fix

- True root cause: respawn always restored the configured `Right` direction without checking the destination. At a latest-safe cell on the right edge, every movement update advanced outside the board and was clamped back to the same position. State and input reported enabled, but the Transform could not visibly move. The earlier `runInBackground` diagnosis explained MCP timing while unfocused but was not the gameplay movement defect; that setting has been restored to its original value.
- Fix: GameManager resolves the safe respawn cell once and selects the configured direction when it stays in bounds, otherwise a deterministic in-bounds cardinal direction. PlayerController has one respawn reset path that synchronizes Transform, logical position, direction, and BoardManager tracking before controls are restored.
- Safe-cell fallback now validates the stored cell, uses the known safe `(0,1)` spawn when valid, and otherwise deterministically finds the first captured board cell.
- Regression coverage now advances the real PlayerController after respawn, verifies world/logical/grid synchronization, reproduces the right-edge blocked-direction case, and verifies invalid-last-safe-cell fallback.
- Play Mode QA in the real `Game.unity` verified a laser death at right-edge safe cell `(53,48)` respawned with `Up` and visibly moved to `(53,95)`. The player then completed another 2.97% capture, suffered an enemy-contact death, respawned at `(42,0)`, and visibly moved to `(53,0)`. Trail cleanup, territory persistence, repeated death, input, and movement restoration all remained valid.
- Unity Console: 0 SpaceXonix errors after verification.

## Phase 1-7 Core Gameplay QA

- Classification: **PASS WITH MINOR ISSUES**. Core gameplay is stable enough to proceed to Phase 8.
- Startup/serialization: `Game.unity` validates with no missing scripts or broken prefabs. It contains one GameManager, InputRouter, PlayerController, BoardManager, PoolService, EnemyManager, LaserManager, one camera, four configured enemy types, and two cardinal laser emitters. The 54 x 96 board, three lives, and 1.25-second respawn delay initialize correctly.
- Movement/capture: all four corners and edges accept an in-bounds direction; controller, Transform, and BoardManager coordinates remain synchronized. Multiple captures, enemy-aware occupancy, percentage updates, self-intersection failure, trail cleanup, and continued captures after respawn/territory destruction were verified in Play Mode.
- Earlier partial repair: checking the intended endpoint before velocity reflection fixed one blocked-entry case, but did not verify or cover an accepted move crossing an intermediate trail cell. The later trail-hit reliability repair below supersedes the earlier claim that this path was fully verified.
- Lives/respawn/Game Over: Basic, Linear, and Unstable contact each removed exactly one life; duplicate failures during Respawning and Game Over were rejected; final life reached zero without another respawn. Laser and enemy-trail deaths both completed safe-cell respawn and real post-respawn movement.
- Volatile: protection/arming, one-shot detonation, nearby enemy removal, distant/player-outside safety, pooling, captured-territory removal, perimeter preservation, percentage reduction, and consistent occupancy passed. New capture remained possible afterward.
- Lasers: both axes cycle through Cooldown, Warning, Firing, and Cooldown. Warning presentation was visible and non-damaging; firing damaged once; large-delta coverage passed; territory, enemies, Volatile behavior, and trails remained isolated. Repeated cycles did not grow the GameObject count.
- Continuous Play Mode session: completed two captures, encountered a non-damaging warning and damaging beam, respawned and moved, completed another capture, detonated a Volatile against captured territory/enemies, continued capturing, died from an enemy trail hit, respawned again, and continued moving. Console remained free of SpaceXonix errors.
- Minor issue: current placeholder enemy visuals are visible but not strongly distinguishable from one another. This does not block mechanics or Phase 8 and belongs to later asset/polish work.

## Enemy Trail-Hit Reliability Repair

- Actual root cause: `EnemyController` checked trail contact only when `EnemyMovementModel.Advance` rejected the move and left the enemy stationary, and it checked only the intended endpoint. An accepted move from cell A to cell D never examined intermediate cells B/C, so a visually and logically crossed active trail could silently pass without reaching `GameManager.ReportPlayerFailure`.
- Shared fix: `BoardManager.GetTraversedCells` now deterministically enumerates every logical cell crossed by an enemy movement segment. The shared `EnemyController` checks that sequence against authoritative `BoardModel` trail state before movement/reflection, then routes the first contact through `ReportPlayerFailure(PlayerFailureReason.TrailHit)`. Basic, Linear, Unstable, and normal Volatile movement inherit the same behavior; lasers remain separate.
- Regression coverage includes single-cell entry, intermediate multi-cell crossing, adjacent-path rejection, duplicate failure protection, Basic/Linear/Unstable shared behavior, controller reuse, and an actual PoolService release/reacquire cycle.
- Real `Game.unity` Play Mode proof ran 10 deliberate intersections across Basic, Linear, and Unstable enemies; short, long, horizontal, vertical, multi-cell, and edge-adjacent trails were covered. Every recorded traversal contained a trail cell, reported `TrailHit`, accepted exactly one failure, changed lives by exactly one (20→19 through 11→10), cleared the trail, entered `Respawning`, completed respawn, and allowed immediate movement afterward.
- Continuous `Game.unity` proof captured territory (0%→0.1022913%), then completed two successive trail deaths: Basic 3→2 and Linear 2→1. Both cleared the trail, entered Respawning, returned to Playing, and moved afterward.
- Final EditMode suite: 83 passed, 0 failed. Unity Console: 0 SpaceXonix errors.

## Player Startup, Input, and Laser-Respawn Stabilization

- Startup root cause: `InputRouter` and `PlayerController` both defaulted to enabled with `Right` already selected. As soon as the first gameplay update ran, the controller moved without any player command; the real scene reached cell `(1,1)` with one active trail cell and `IsPlayerExposed == true`. Startup now initializes input and movement disabled, synchronizes the configured direction, and keeps the controller stationary until the first legal direction command. Five fresh `Game.unity` entries consistently remained at `(0,1)` with zero trail cells, zero capture, and no exposure.
- Rapid-input root cause: every raw direction immediately replaced the movement model direction without applying board-step legality. At an edge, a rapid final input toward the outside overwrote a valid direction, after which every update clamped to the same position indefinitely. PlayerController now owns the actual movement direction plus one optional pending direction. InputRouter records raw input, the controller retains only the latest legal request, consumes it once per movement tick, rejects illegal requests without replacing valid movement, and selects a deterministic legal fallback only if the current direction itself becomes illegal.
- Laser-respawn root cause: a firing emitter checked the player every update. Respawn could return the player to the same beam while that firing phase was still active, immediately consuming another life and re-entering Respawning before the first movement tick. Each `LaserEmitter` now accepts at most one player failure per firing phase and resets that guard at the next phase; life loss remains authoritative through `GameManager.ReportPlayerFailure(Laser)`.
- Regression coverage adds deterministic startup gating, multiple inputs before a movement tick, invalid-edge then valid input, rapid post-respawn input, rapid exposed movement, and a real laser failure → respawn → same-firing-phase check → successful movement tick.
- Play Mode stress: 400 rapid-input sequences across all corners produced zero stuck states, zero logical/world/board synchronization failures, and zero diagonal movement. Ten real laser deaths changed lives exactly once each (20→19 through 11→10), rejected repeat damage from the same firing phase, respawned, and moved successfully.
- Continuous `Game.unity` session passed startup, movement, rapid turns, capture (0%→0.08183306%), laser death (5→4), respawn/movement, another capture (0.1841244%), real Basic enemy contact (4→3), respawn, and continued movement in `Playing` with synchronized positions.
- Final EditMode suite: 89 passed, 0 failed. Prompt 9 remains not started.

## Held Safe Movement / Persistent Capture Movement

- Final rule: safe/captured movement continues only while a direction is held and stops on release. Once exposed and drawing a trail, the accepted direction persists after release until reconnection. Capture ends exposed persistence, while a direction that remains physically held continues as ordinary safe movement.
- Root cause: `InputRouter` emitted only direction-press events, so `PlayerController` could stop stale exposed persistence after capture but could not distinguish a held safe-terrain command from a released key.
- Fix: `InputRouter` now reports held/released direction state while retaining latest-press behavior. `PlayerController` re-arms each accepted safe step only while that direction remains held, ignores releases while exposed, and stops/recenters on the authoritative safe cell when released. Reconnection checks current held state instead of leaking exposed persistence.
- Regression coverage verifies continuous held safe movement, release-to-stop, release-independent exposed movement, capture stop with controls released, held movement immediately after capture, two consecutive captures, and existing rapid-input synchronization.
- Targeted movement tests passed 7/7; the final EditMode suite passed 94/94 with no failures. Unity compiled without errors. Prompt 9 remains not started.

## Player Trail Self-Intersection

- Player movement may now enter an in-bounds active trail cell so the existing authoritative `BoardModel.MoveTo` self-intersection result can reach `GameManager.ReportPlayerFailure(PlayerFailureReason.TrailSelfIntersection)` instead of being blocked by direction validation.
- Existing traversal stops immediately on `TrailFailed`; the shared failure pipeline removes exactly one life, clears the unfinished trail, enters Respawning, rejects duplicate failures, and restores the player on a safe cell.
- Added an end-to-end regression covering draw → turn → cross an earlier trail cell → one life lost → trail cleared → duplicate rejected → successful safe respawn/movement. Normal safe-territory reconnection coverage now also asserts no life loss and successful capture.
- Unity compiled without code errors. The later AirXonix control/respawn stabilization pass ran and superseded the previously deferred verification. Prompt 9 remains not started.

## AirXonix Control and Hazard Respawn Stabilization

- Movement follows one authoritative rule: safe terrain moves only while input is held; exposed movement persists with a non-zero cardinal direction until reconnection. The later opposite-input stabilization below supersedes the earlier exposed-reversal behavior.
- Direct enemy/player contact is resolved before trail traversal contact at the same trail-head position, so Volatile ship contact uses the shared `EnemyContact` failure path. Enemy trail crossings still use `TrailHit`, and Volatile explosions retain `VolatileExplosion`.
- `PlayerController.PrepareForRespawn` now clears pending/persistent movement at failure acceptance. `RespawnAt` atomically restores the safe Transform/logical/board position, a valid cardinal direction, manual waiting state, and synchronized `InputRouter` state before `GameManager` returns to Playing.
- Focused regressions passed 11/11, covering safe hold/release, exposed release, reversal/self-intersection, capture exit, Volatile contact, same-firing-phase laser protection, and safe/manual respawn for EnemyContact, TrailHit, VolatileExplosion, Laser, and TrailSelfIntersection.
- Full EditMode suite: 102 passed, 0 failed. `Game.unity` Play Mode QA passed three normal captures, safe hold/release, exposed release and multiple turns, deliberate reversal death, three Volatile-contact deaths, and three active-beam laser deaths. Every respawn moved successfully; stale beam ticks caused no extra life loss; zero stuck states and zero Console errors occurred. Prompt 9 remains not started.

## Atomic Player Failure Handling

- `GameManager.ReportPlayerFailure` now closes an explicit synchronous failure gate before life loss, callbacks, trail cleanup, or coroutine startup. Every overlapping callback is rejected until the single authoritative respawn has completely restored the player.
- Only one respawn coroutine may exist. Manual/early completion cancels its pending coroutine, and delayed completion relinquishes coroutine ownership before restoring state, preventing a stale second completion.
- Accepted enemy-contact and enemy-trail callbacks return immediately without advancing the rest of that enemy movement update. Input disable clears held state without emitting a reentrant safe-release callback during failed player traversal.
- Respawn revalidates historical safe cells against the current board, selects a deterministic current safe cell with an outgoing move when needed, synchronizes board/logical/world/input state while still Respawning, and enters Playing only after restoration completes.
- Focused atomic regressions passed 7/7. Full EditMode suite: 108 passed, 0 failed. `Game.unity` overlap QA passed two simultaneous enemy contacts plus enemy+laser, enemy+Volatile, and trail+enemy pairs; each lost exactly one life, cleared the trail, performed one valid respawn, and moved afterward. Unity Console: 0 errors. Prompt 9 remains not started.

## Respawn Movement Stress Verification

- Intermittent root cause: a multi-cell player movement update could continue iterating after `BoardModel.MoveTo` reported `TrailFailed`. GameManager synchronously cleared the failed trail and entered `Respawning`, but the remaining cells in the same tracking loop could immediately start a second trail. The tracking method then returned `TrailStarted` with the player exposed during respawn, leaving stale board state that could interfere with movement after control returned.
- Fix: BoardManager stops cell traversal immediately on `TrailFailed`; PlayerController aborts that failed movement tick and restores its movement-model/board tracking synchronization; authoritative respawn completion clears any residual unfinished trail before resolving the safe cell and direction. Direction validation now uses BoardManager's gameplay legal-step rule rather than bounds alone.
- Pre-fix Play Mode reproduction: one multi-cell self-intersection changed lives from 3 to 2 and state to `Respawning`, but returned `TrailStarted` with one active trail cell and `IsPlayerExposed == true`.
- Post-fix Play Mode stress: the targeted reproduction plus 20 varied death/respawn cycles covered all edges, corners, exposed/safe states, repeated deaths, and EnemyContact, TrailHit, Laser, and VolatileExplosion reasons. All 21 respawns returned to `Playing` with enabled input/movement, a legal next cell, a successful movement tick, and synchronized logical/world positions. Zero stuck, illegal-direction, stale-exposure, or position-mismatch cases occurred.
- Final continuous session: capture, enemy death, respawn/move, capture, enemy-trail death, respawn/move, Volatile territory destruction, continued movement, laser warning/firing avoidance, and continued play all passed in one `Game.unity` run.

## Opposite-Direction Input Stabilization

- Safe/captured terrain accepts immediate 180-degree direction changes. Exposed movement rejects 180-degree input against the effective direction (the accepted pending turn when present, otherwise the movement-model direction), so a rapid Up/Down or Left/Right pair cannot overwrite motion, walk backward into the trail, or create a failure.
- `PlayerController` remains the sole authority for current movement direction and retains at most one legal pending direction. Ignored exposed reversals leave the current/pending movement state unchanged; perpendicular turns and safe-terrain reversals remain valid.
- Regression coverage now exercises all requested safe reversals, 100 rapid reversals on each safe axis with board/logical/world synchronization assertions, exposed opposite suppression on both axes, no life/state changes, persistent non-zero exposed movement, and legal perpendicular self-trail paths.
- Focused movement/respawn suite: 48 passed, 0 failed. Full EditMode suite: 114 passed, 0 failed. Real `Game.unity` stress passed 200 A/D and 200 W/S safe reversals plus 50 ignored exposed reversals on each axis; the player remained synchronized, moving, and in `Playing`, with zero Console errors. Prompt 9 remains not started.

## Trail-Hit Respawn Stabilization

- Root cause: `LifeStateModel.TryFail` synchronously entered `Respawning`, but `GameManager` left gameplay input and player movement enabled until after trail cleanup and failure callbacks. A re-entrant callback could therefore accept a direction and advance the player after the old trail was cleared, recreating exposed trail state inside the accepted failure frame.
- `GameManager.ReportPlayerFailure` now applies the accepted `Respawning` or `GameOver` state immediately after the atomic life-state transition, before trail cleanup or any callbacks. This closes input/movement synchronously while retaining the existing single failure gate and authoritative respawn restoration.
- The focused regression uses a multi-cell enemy traversal across an earlier active-trail cell, attempts re-entrant input/movement during `PlayerFailed`, and verifies one `TrailHit`, aborted enemy/player traversal, empty trail, no exposure, duplicate rejection, valid synchronized safe respawn, manual movement mode, and successful next input movement.
- Focused movement/respawn suite: 49 passed, 0 failed. Full EditMode suite: 115 passed, 0 failed. Unity compiled with 0 Console errors. Additional manual/client Play Mode verification was intentionally not required. Prompt 9 remains not started.

## Simultaneous Failure Completion-Frame Guard

- Remaining race: respawn restoration cleared `failureInProgress` before `PlayerRespawned` callbacks ran. A stale enemy or hazard callback queued for that completion frame could therefore be accepted as a new failure immediately after the first position restore, starting a competing death/respawn cycle.
- `GameManager` now keeps its single atomic failure gate closed throughout the respawn-completion frame. Position, board, movement-model, input, and safe/manual state are restored once; completion callbacks and all other same-frame failures remain no-ops. The gate releases only after Unity advances beyond that frame.
- Focused coverage verifies enemy+enemy, enemy+laser, enemy+Volatile, and TrailHit+enemy combinations, including a re-entrant hit from `PlayerRespawned`: exactly one life, failure event, respawn start, and respawn completion; empty trail; valid safe state; stale callback rejection; and successful movement after release.
- Focused movement/respawn suite: 53 passed, 0 failed. Full EditMode suite: 119 passed, 0 failed. Unity compiled with 0 SpaceXonix Console errors. Prompt 9 remains not started.

## Self-Trail Abort and Capture Candidate Repair

- `PlayerController` now exits the movement update immediately when synchronous board traversal accepts a failure and disables movement. Self-intersection can no longer return into post-failure movement-model or Transform processing; the authoritative respawn path remains responsible for clearing the trail and restoring board/logical/world/input state.
- `BoardModel` now discovers capture candidates only from uncaptured cells directly adjacent to the active closing trail. Each seed flood-fills its entire connected component, structural/captured boundaries remain excluded, enemy-containing components remain invalid, and the existing smallest eligible-region rule is applied only to regions actually created by that closure.
- This prevents small, disconnected uncaptured fragments elsewhere on the board from being selected as an arbitrary partial capture instead of the full valid enclosed connected territory.
- Unity compilation completed with 0 SpaceXonix Console errors. Automated and manual tests were not run for this task per request; the previously verified baseline remains 119/119. Prompt 9 remains not started.

## Authoritative Player Respawn State

- Root cause addressed: runtime respawn previously called a `void` world-position reset and then transitioned to `Playing` without confirmation that the destination remained safe or that board, movement model, Transform, trail mode, pending direction, and input direction all agreed.
- `PlayerController.RestoreSafeManualState` is now the single runtime restoration operation. It accepts a validated logical safe cell, clears any residual trail, synchronizes BoardManager tracking, movement-model position/direction, Transform, pending/manual state, and InputRouter direction, then reports whether the restored state is coherent.
- Failure preparation now clears pending movement and resets controller/InputRouter direction state immediately. `GameManager` revalidates the current respawn cell, requires successful restoration, enables safe/manual controls, and only then completes the state transition to `Playing`; duplicate failures and completion-frame callbacks remain behind the existing atomic gate.
- The shared player and enemy traversal aborts remain in place for every accepted failure source; enemy, laser, Volatile, trail-hit, and self-intersection callbacks continue to route exclusively through `ReportPlayerFailure`.
- Unity compilation completed with 0 SpaceXonix Console errors. Automated tests and Play Mode QA were not run per request; the previously verified baseline remains 119/119. Prompt 9 remains not started.

## Unified AirXonix Player Control State

- Replaced the independent movement-enabled and awaiting-input flags with one explicit `PlayerControlState`. Safe input transitions between `SafeIdle` and `SafeMoving`; leaving safe terrain enters `ExposedMoving`; accepted failure and final death enter `Respawning` or `GameOver` synchronously through GameManager.
- Safe terrain now advances only from a held legal direction and stops on release. Exposed movement keeps its single non-zero direction after release, accepts perpendicular turns, and ignores 180-degree input without cancelling motion. Reconnection clears the persistent capture direction immediately and requires ordinary safe-terrain input before movement continues.
- The existing atomic failure gate and single restoration operation remain authoritative. Failure disables player/input state before callbacks, traversal aborts on the accepted failure, and restoration revalidates a current safe cell before synchronizing BoardManager, movement model, Transform, trail/exposure, direction, and InputRouter; `Playing` is applied only after restoration succeeds.
- Regression assertions now verify every control-state transition in addition to existing rapid reversal, self-intersection, TrailHit, simultaneous hazard, stale-safe-cell, capture, and post-respawn movement coverage.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 119 passed, 0 failed, 0 skipped. Unity Console after the run: 0 errors. `git diff --check`: passed. Prompt 9 remains not started.

## Repository Cleanup

- Removed unused Unity template Readme/tutorial content, SampleScene, default template Input Actions, local `.vscode` settings, and generated `.slnx` metadata.
- Removed unused AI Assistant/Inference/Navigation, Version Control, Multiplayer Center, and Visual Scripting packages.
- Retained Unity MCP, URP and its referenced profiles/renderers, Input System, Test Framework, uGUI, Timeline, and Rider/Visual Studio integrations.
- The Console is clean; the former Unity AI Assistant `NoSubscription` noise is resolved.

## Known Issues / Tooling Noise

- Unity MCP's editor-state resource can briefly continue reporting `is_changing` after Play Mode has begun. Runtime event instrumentation and advancing frame/time values conclusively verified respawn completion despite that stale status field.

## Important Files

- `AGENTS.md` — repository development rules
- `PROG.md` — current development status; update after meaningful phase/test changes
- `SpaceXonixProposal.md` — game design document
- `IMPLEMENTATION_PLAN.md` — implementation architecture and phase plan
- `Assets/SpaceXonix/Scripts/Board` — board, trail, and capture authority
- `Assets/SpaceXonix/Scripts/Core` — gameplay state, lives, and failure routing
- `Assets/SpaceXonix/Scripts/Enemies` — standard enemy framework and configurations
- `Assets/SpaceXonix/Scripts/Hazards` — laser cycle, emitter, manager, and presentation behavior
- `Assets/SpaceXonix/Scripts/Pooling` — reusable runtime object service
- `Assets/SpaceXonix/Tests/EditMode` — deterministic regression suite
- `Assets/SpaceXonix/Scenes/Game.unity` — representative gameplay scene

## Next Recommended Action

**Phase 8 — Scoring + Large-Capture Multipliers**

Before modifying anything, read `AGENTS.md`, `PROG.md`, `SpaceXonixProposal.md`, and `IMPLEMENTATION_PLAN.md`, then inspect `git status` and the existing implementation.
