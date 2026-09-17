# SpaceXonix Development Progress

## Project Snapshot

- Unity: 6000.3.20f1 (Unity 6.3 LTS), URP
- Targets: Windows PC and Android; 1080x1920 portrait reference resolution
- Active branch: `main`
- Main gameplay scene: `Assets/SpaceXonix/Scenes/Game.unity`
- Default logical board: configurable 54 x 96 cells
- Current phase: Phase 12 (2.5D Presentation Foundation) in progress — step 1 (camera) complete
- Latest completed feature: Cinemachine perspective diagonal-down arena camera (35° tilt, FOV 30) with Dutch-based Arena Tilt roll

## Phase Progress

1. **COMPLETE** — Project audit and implementation plan (`2a916d0`, `373442d`)
2. **COMPLETE** — Foundation, input, and cardinal player movement (`148beb9`)
3. **COMPLETE** — Board, trail, deterministic territory capture, and occupancy (`668a177`)
4. **COMPLETE** — Lives, authoritative failure pipeline, and last-safe-cell respawn (`172df88`)
5. **COMPLETE** — Pooled Basic, Linear, and Unstable standard enemy framework (`aa9fd91`)
6. **COMPLETE** — Pooled Volatile Alien, protected detonation, enemy/player blast effects, and territory destruction (`84391c1`)
7. **COMPLETE** — Pooled horizontal/vertical Laser Hazards with warning, firing, and cooldown states
8. **COMPLETE** — Scoring + Large-Capture Multipliers
9. **COMPLETE** — Power Meter + Power Shot
10. **COMPLETE** — Shield + Freeze + Arena Tilt
11. **COMPLETE** — Campaign Stages + Progression
12. **IN PROGRESS** — 2.5D Presentation Foundation (perspective Cinemachine camera, raised territory, hovering pixel-art billboards)
13. **NOT STARTED** — Roguelite Upgrades
14. **NOT STARTED** — Stage Modifiers
15. **NOT STARTED** — Alien Core Boss
16. **NOT STARTED** — UI + Menus + HUD
17. **NOT STARTED** — Android Controls
18. **NOT STARTED** — Audio
19. **NOT STARTED** — Asset Acquisition/Integration
20. **NOT STARTED** — Visual Polish + VFX + Cinemachine
21. **NOT STARTED** — Final QA + Profiling + Submission Cleanup

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
- `BoardCaptureResult.PercentageGained` reports the playable area made safe by one reconnection (selected region plus committed trail). `ScoreManager` listens to `BoardManager.CaptureCompleted` and delegates to the pure `ScoreModel`; tuning lives in `ScoringDefinition` (`ScriptableObjects/Balance/Scoring.asset`).
- `PowerMeter` charges from `BoardManager.CaptureCompleted` through the pure `PowerMeterModel`, fires on `InputRouter.PowerShotRequested`, and advances pooled `PowerShotProjectile` instances that despawn the first standard enemy through `EnemyManager`. Tuning lives in `PowerDefinition` (`ScriptableObjects/Balance/Power.asset`).
- `PowerUpManager` owns capture-driven pickup spawning, the pure `PowerUpSlotModel` (one stored ability, instantly replaced by a newly touched pickup), and timed Shield/Freeze/Arena Tilt effects. `GameManager` exposes `SetPaused` (time-scale pause that preserves life/control state) and `SetShieldActive`; `EnemyManager` exposes movement suspension and drift.
- `CampaignManager` (on the GameManager object, execution order 100) drives stages 1–4 inside `Game.unity` from `CampaignDefinition`/`StageDefinition` assets, resetting board, lives, player, enemies, lasers, and per-stage statistics in place. `GameManager.BeginStage`/`StartStagePlay` own the `Briefing` → `Playing` transition; `CampaignRunModel` tracks stage progression and highest stage reached.
- Collision is body-sized: `PlayerController.collisionRadius` and `EnemyDefinition.collisionRadius` drive ship contact (radius sum), trail contact and capture occupancy (all cells overlapped by the alien body via `BoardManager.GetCellsOverlappingCircle`), bouncing (the whole body must stay in uncaptured space), lasers (beam half-width + ship radius), Power Shot reach, Volatile blasts/detonation, and pickup collection. Prefab visual sizes equal the collision diameters. A radius of 0 preserves the original point/0.6-cell behaviour.

## Current Test State

- EditMode discovered: 225
- Passed: 225
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

## Safe-Terrain Laser Respawn Restoration

- The shared `PlayerController.RestoreSafeManualState` operation now establishes `SafeIdle` itself only after the board cell, movement-model position, Transform, exposure state, and input direction have all synchronized successfully. GameManager still keeps gameplay input disabled and remains in `Respawning` until that restoration returns success, then transitions to `Playing` through the existing single atomic failure flow.
- This removes the restoration gap where a safe-terrain laser death depended on a later broad gameplay-state application to leave the controller's `Respawning` movement state. No laser-specific respawn behavior was added, and repeated beam callbacks remain rejected by the failure gate and firing-phase guard.
- Focused coverage now runs both `SafeIdle` and `SafeMoving` laser hits through life loss, `Respawning`, same-beam rejection, synchronized `SafeIdle` restoration, and successful movement from fresh held input.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 120 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Unified Player Death and Respawn Lifecycle

- `GameManager.ReportPlayerFailure(...)` remains the only death entry point and now combines the synchronous failure lock, life transition, lifecycle-generation increment, traversal invalidation, single respawn ownership, and post-respawn protection. Simultaneous enemy, trail, laser, and Volatile reports can accept only the first failure and cannot start competing restoration work.
- Player and enemy traversal capture the current player lifecycle generation. When an accepted death increments it, remaining work from the prior generation exits before it can write another player position, recreate a trail, or continue collision processing.
- Respawn continues to prefer BoardManager's most recent pre-danger safe cell, revalidates it against the current BoardModel, and deterministically falls back to another current captured cell with a legal outgoing step. The one restoration operation clears trail/exposure, synchronizes BoardManager, movement model, Transform, direction, and InputRouter, and establishes `SafeIdle` before the life model returns to `Playing` and input is enabled.
- Successful non-GameOver respawns now start configurable protection (`2.0` seconds by default). `GameManager.IsInvulnerable` is authoritative; ignored failures do not alter lives, movement, position, or active trail, while movement and capture remain available. Protection expires automatically, is cleared by accepted death/GameOver and explicit state resets, and is never started on the final life.
- Direct enemy contact now uses the same `EnemyContact` failure path on safe as well as exposed terrain. Lasers continue to test only the player's position: beam contact with trail alone remains harmless and cannot report `TrailHit` or clear the trail.
- Regression coverage includes safe/exposed laser deaths, laser/trail isolation, safe direct enemy contact, enemy TrailHit, self-intersection, Volatile damage, simultaneous mixed failures including laser + Volatile, last-safe-cell fallback, lifecycle aborts, post-respawn movement, two-second invulnerability behavior, renewed damage after expiry, and final-life Game Over.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 123 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Volatile Direct-Contact Stabilization

- Real `Game.unity` tracing confirmed direct player/Volatile contact routes through exactly one `EnemyContact`; it does not itself detonate the Volatile. The remaining ordering risk was `EnemyManager` continuing same-frame Volatile interaction work after the accepted contact had already advanced the player lifecycle into `Respawning`.
- Volatile interaction simulation now respects the authoritative gameplay state and captured lifecycle generation. It does not advance protection, detonate, despawn, update explosion occupancy, or evaluate player blast damage while the player is Respawning; intended enemy-triggered detonation resumes after gameplay restoration.
- Focused coverage overlaps the player, an armed Volatile, and a normal enemy to exercise the competing path. It verifies one failure event, one life, one respawn start, `EnemyContact` as the sole accepted reason, no same-frame detonation, valid safe restoration, `SafeIdle`, and successful next movement.
- Post-fix `Game.unity` reproduction produced one `EnemyContact`, one life loss, one respawn, no Volatile detonation, a valid safe cell, and successful movement after restoration. Full Unity EditMode suite: 124 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Atomic Damage Lifecycle Barrier

- `GameManager` now exposes one lifecycle-token predicate for player-contact work. The first accepted failure closes the synchronous failure gate and increments the lifecycle generation before life-state mutation; stale callers can no longer pass a partial `Playing` check and continue touching player state.
- Enemy direct contact and TrailHit now check the captured token immediately after reporting damage and return when the failure was accepted, the generation changed, or gameplay left `Playing`. Lasers use the same predicate before their terminal failure report.
- Volatile explosion ordering is terminal with respect to player damage: enemy removal, territory effects, source despawn, occupancy refresh, and explosion notification complete first. The final operation is the token-validated player failure report, so accepted blast damage has no remaining callback work capable of changing player state or its future respawn destination.
- The five simultaneous combinations—enemy + enemy, enemy + laser, enemy + Volatile, laser + Volatile, and TrailHit + enemy—now run through the complete shared lifecycle regression: one life, one respawn, valid safe restoration, and successful movement afterward. Completion-frame callbacks remain rejected and invulnerability remains owned by GameManager.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 125 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Valid Player Board-State Invariant

- `BoardManager` now validates the complete authoritative player state: safe means a captured current cell with no exposure/trail; exposed means a contiguous active trail anchored beside captured terrain and ending at the current player cell.
- Respawn completion now uses one shared safe restoration method and cannot return to `Playing` until the BoardManager cell, movement-model position, Transform, empty trail, non-exposed state, and `SafeIdle` control state all agree. Historical safe cells continue to be revalidated against the current board.
- `GameManager` checks the invariant at the end of each gameplay frame. An impossible `Playing` state creates an immediate lifecycle barrier, invalidates old traversal callbacks, and repairs the player through the same authoritative safe restoration path without deducting a life.
- Regression coverage explicitly corrupts the player into uncaptured territory without a trail and verifies deterministic synchronized recovery, and invalidates a selected respawn cell before completion to verify current-board fallback.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 127 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Invalid Player-State Root Transition

- Real `Game.unity` reproduction placed the player on newly captured interior terrain during post-respawn invulnerability, then detonated a real armed Volatile at that cell. Immediately before territory damage the player was valid (`Captured`, no trail, `SafeIdle`); immediately after `EnemyManager.ResolveVolatileExplosion` called `BoardManager.RemoveCapturedWithinRadius`, the same occupied cell was `Uncaptured` while the game remained `Playing`, exposure was false, and the trail was empty.
- The first invalid write was `BoardModel.RemoveCapturedWithinRadius`, which removed the currently occupied player cell. Damage gating correctly skipped the invulnerable player, but territory damage was independent. The previous end-of-frame invariant repair only reacted after this invalid state had already been created and therefore did not prevent the real transition.
- Territory destruction now passes the authoritative current player cell as a protected cell into `BoardModel`. Other captured cells in the blast are still removed normally, while the player's safe anchor cannot be erased beneath a surviving player.
- The exact real-scene sequence now remains `Playing + Captured + SafeIdle + no trail`, and fresh input moves successfully into a valid exposed trail. The focused regression recreates capture, occupied-cell Volatile detonation during invulnerability, and validates the complete player state afterward.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 128 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Post-Death Lifecycle Continuation

- A full real-scene death trace reproduced the apparent stuck-middle state on a non-final life: `ReportPlayerFailure` correctly accepted `EnemyContact`, incremented the lifecycle generation, cleared the trail, disabled input, and entered `Respawning` at the death position. After more than the configured 1.25-second delay, no respawn cell had been selected and no restoration write had occurred.
- The trace identified the cause before any corrupting writer: Unity had `Application.runInBackground == false` while the player window was unfocused. The player loop stopped, so `RespawnAfterDelay` never resumed. Enabling background execution immediately let the existing single coroutine select the current safe `(0,1)` cell, synchronize BoardManager/movement/Transform, restore `SafeIdle`, enable input, and return to `Playing`.
- `GameManager.Awake` now explicitly enables background execution so an accepted death lifecycle cannot be suspended indefinitely at the death position merely because window focus changes. No new respawn path, fallback, or hazard-specific behavior was added.
- Real `Game.unity` replay verified a middle-board death restored to current captured terrain after the delay and accepted fresh movement. Existing shared regressions continue to cover enemy, laser, TrailHit/self-intersection, Volatile, simultaneous hits, safe restoration, and movement after respawn.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 129 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Remaining Respawn Deadlock Root Cause

- Bounded transition tracing in a continuously focused `Game.unity` session covered repeated EnemyContact, Laser, TrailHit/self-intersection, VolatileExplosion, and overlapping failure reports. Ordinary respawns consistently restored the current safe cell and moved afterward. The corrupting sequence occurred only after a completed respawn while post-respawn invulnerability was still active.
- Exact reproduction: die, complete the authoritative safe respawn, immediately draw a multi-turn trail, then cross an earlier trail cell during invulnerability. This was category C: `BoardModel.MoveTo` called `CancelTrail` before GameManager evaluated `TrailSelfIntersection`; GameManager correctly rejected the protected damage, but the trail and traversed BoardManager cell had already been mutated under the still-`Playing` player.
- Trail self-intersection is now transactional. `BoardManager` asks the authoritative GameManager failure gate to accept the failure before BoardModel clears the trail. Accepted failures retain the normal one-life/one-respawn path; rejected protected failures preserve the existing trail, roll back the attempted BoardManager cell, and restore the movement model to the unchanged Transform before returning from that movement tick.
- Focused real-scene replay kept the Game window focused and verified the rejected intersection left lives unchanged, the player at the original trail head, all three trail cells intact, and exposed movement valid. The exact regression additionally makes a legal perpendicular next move and verifies trail extension, proving the player is not stuck.
- `runInBackground` did not solve this because the player loop was running normally; the corruption was an ordering bug inside a live movement step after respawn.
- `SpaceXonix.EditModeTests.csproj` compilation: 0 warnings, 0 errors. Full Unity EditMode suite: 130 passed, 0 failed, 0 skipped. Prompt 9 remains not started.

## Player Lifecycle Diagnostics

- Added lightweight `UNITY_EDITOR` / `DEVELOPMENT_BUILD` tracing at player state-write boundaries only. The in-memory ring retains the most recent 50 movement, direction, capture, failure, lifecycle, respawn, and invariant-repair transitions without logging normal activity.
- Each entry records frame, lifecycle and operation generations, event/reason, accepted failure reason, gameplay/control states, logical/board/Transform cells and positions, current cell type, exposure/trail state, tracked safe cell, current/pending direction, movement/input state, and invulnerability state/timer.
- The tracer detects invalid safe/exposed combinations, position disagreement, movement enabled during Respawning, and writes tagged with an obsolete lifecycle generation. The first detected violation emits one clearly delimited history dump identifying the transition that first became invalid; subsequent checks remain silent to prevent Console spam.
- In Editor Play Mode, F8 now emits the complete current player/lifecycle state and the retained 50-entry history even when no invariant detector has fired. The snapshot includes active-respawn status alongside gameplay, control, position, board, trail, direction, input, and invulnerability data.
- Movement diagnostics now retain raw key presses, InputRouter acceptance/release, safe control-state transitions, direction rejection reasons, logical-step attempts/outcomes, and explicit failure/respawn milestones including the selected safe cell. F8 also reports the live held state of W/A/S/D and all four arrow keys to distinguish raw keyboard state from routed input state.
- Logical movement history now distinguishes `LogicalStepStarted`, `LogicalStepCommitted`, and `LogicalStepAborted`. Position synchronization invariants run only at committed/stable boundaries, so expected movement-model interpolation before BoardManager/Transform commit remains visible in history without producing a false invalid-state dump.
- Diagnostics now retain independent 30-entry critical lifecycle and 50-entry movement/input histories. Failure, generation, gameplay/control-state, respawn, restoration, invulnerability, invariant-repair, and Game Over records can no longer be evicted by routine movement; F8 prints them first and permanently reports the last accepted failure reason/frame.
- Diagnostics do not repair state or alter gameplay behavior and compile out of non-development players. Temporary full-frame tracing was not retained. Compilation completed with 0 warnings and 0 errors; automated and Play Mode tests were intentionally not run for this diagnostics-only task. Prompt 9 remains not started.

## Atomic Failure Gate and Temporary Life State HUD

- `GameManager` now closes an explicit player-damageability latch before lifecycle generation, life-state, trail, callback, or coroutine work begins. The latch stays closed throughout Respawning and post-respawn invulnerability, so later same-frame enemy, trail, laser, and Volatile reports are strict no-ops even if invoked by already-running callbacks.
- Existing failure callers continue to use the returned acceptance result and lifecycle token before doing any remaining contact work. Game Over keeps damageability, movement, and gameplay input disabled, never starts respawn or invulnerability, and rejects further damage at zero lives.
- `Game.unity` now includes a temporary `LifeStateDebugHud` on GameManager. It shows `Lives: N` during gameplay and a large centered `GAME OVER` only in the terminal state; this is intentionally not the later full HUD phase.
- Real-scene gate verification produced `EnemyContact=True`, same-frame `TrailHit=False`, one life lost, and one lifecycle-generation increment. The same scene verified `3 -> 2 -> 1 -> 0`, Game Over, rejected post-terminal damage, disabled movement/input, and an active HUD component. Full EditMode suite: 130 passed, 0 failed, 0 skipped. Unity Console: 0 errors. Prompt 9 remains not started.

## Repository Cleanup

- Removed unused Unity template Readme/tutorial content, SampleScene, default template Input Actions, local `.vscode` settings, and generated `.slnx` metadata.
- Removed unused AI Assistant/Inference/Navigation, Version Control, Multiplayer Center, and Visual Scripting packages.
- Retained Unity MCP, URP and its referenced profiles/renderers, Input System, Test Framework, uGUI, Timeline, and Rider/Visual Studio integrations.
- The Console is clean; the former Unity AI Assistant `NoSubscription` noise is resolved.

## Phase 8 — Scoring + Large-Capture Multipliers

- Formula: `pointsPerCapturedPercent (100) × percentage gained in one capture × large-capture multiplier × stage bonus`, rounded to an integer per capture. Tiers are data-driven: under 5% ×1.0, 5–9.99% ×1.5, 10–14.99% ×2.0, 15%+ ×3.0.
- "Percentage gained" includes committed trail cells, so a trail-only reconnection (both sides contain aliens) still awards its small trail area at ×1. Captured territory later removed by Volatile explosions does not deduct score.
- `ScoreModel` also tracks `LargestCapturePercentage` (for the future Stage Complete screen) and exposes `SetBonusMultiplier` as the hook for Phase 13 stage-modifier score bonuses. `ScoreManager.ResetScore` is the hook for campaign reset in Phase 11.
- `ScoreManager` publishes `CaptureScored(CaptureScoreAward)` and `ScoreChanged(int)`. The temporary `LifeStateDebugHud` now also shows score, capture percentage, and a 2-second `+points xN` capture popup; it remains a placeholder until the UI phase.
- Tests: 20 new EditMode tests cover every tier boundary, formula examples, accumulation, largest-capture tracking, risk/reward ordering, bonus stacking, non-positive captures, reset, empty tier data, and `PercentageGained` for region and trail-only captures. Full EditMode suite: 150 passed, 0 failed.
- Real `Game.unity` Play Mode QA drove actual PlayerController/InputRouter captures: 6.38% → ×1.5 → 957; 12.77% → ×2 → 2553; 1.06% trail-only → ×1 → 106 (twice, one due to aliens on both sides); 25.53% → ×3 → 7660. Final score 11488 at 47.87% board capture, largest capture 25.53%, lives unchanged, HUD verified by screenshot. Unity Console: 0 errors/warnings.

## Stage Completion Target (75%)

- `GameManager` owns a serialized `captureTargetPercentage` (75 by default, per GDD). `TryCompleteStage()` runs in `LateUpdate` after the frame's movement and only succeeds while `Playing`, with no failure in progress, the player on safe terrain (not exposed), and captured percentage at or above the target.
- Completion enters the terminal `GameplayState.StageComplete` through `LifeStateModel.TryCompleteStage()`, disables input/damage, increments the lifecycle generation to abort any in-flight traversal, sets `PlayerControlState.StageComplete`, and raises `StageCompleted` once.
- `EnemyManager` suspends all active enemy movement and `LaserManager` shuts down every emitter (releasing warning/beam presentations) on `StageCompleted`. The temporary debug HUD shows a green `STAGE COMPLETE` with score and largest capture.
- This is the stage-end trigger only; Stage Complete screen, upgrade selection, and next-stage flow remain Phase 11 work.
- Tests: 7 new EditMode tests (below target, reaching target once with full player/input/damage lock, end-of-frame detection, no completion while Respawning/GameOver/exposed, enemy freeze, `LifeStateModel` gating). Full suite: 157 passed, 0 failed. The user verified the banner and freeze manually in `Game.unity` Play Mode.

## Phase 9 — Power Meter + Power Shot

- Gain: `powerPerCapturedPercent (5) × BoardCaptureResult.PercentageGained × gain multiplier`, capped at `maxPower (100)`; overflow is discarded. `PowerFull` fires once when a capture fills the meter. `SetGainMultiplier` is the hook for the Rapid Capacitor upgrade; `ResetMeter` is the campaign-reset hook.
- Firing: `Space` (and the public `InputRouter.RequestPowerShot()` for the future Android button) requests a shot only while gameplay input is enabled. A shot requires `Playing` and a full meter, consumes the entire meter, and launches from the player in `PlayerController.FacingDirection` (most recently accepted cardinal direction).
- Projectile: pooled `PowerShot.prefab` (unlit cyan cube). Each frame it sweeps its straight movement segment against active standard enemies (`shotHitRadius`) so it cannot tunnel, destroys the nearest one along the path via `EnemyManager.Despawn`, and is released when it hits or leaves the board. It ignores territory and trails. A Volatile hit by the shot is removed without detonating. Active shots are released on `StageCompleted`. Boss interruption is deferred to the boss phase.
- The temporary debug HUD shows a power bar under score/capture percentage with `POWER READY [SPACE]` when full.
- Tests: 16 new EditMode tests cover gain formula, capping, full-only consumption, gain multiplier/reset, non-positive captures, nearest-hit sweep selection, capture-driven charging and single `PowerFull`, no-fire below full, first-enemy destruction on the facing axis, all four directions, board exit plus pool reuse, and disabled input. Full suite: 173 passed, 0 failed.
- Real `Game.unity` Play Mode: captures of 2.45%, 3.70%, and 4.77% produced 12.3, 18.5, and 23.8 power; a large capture capped the meter at 100; the HUD bar rendered; the user fired a shot during live play, the meter emptied and recharged, the shot instance returned to the pool, and targeted enemies were removed. Unity Console: 0 errors/warnings.

## Phase 10 — Pickups, Shield, Freeze, Arena Tilt

- Spawning (`PowerUpSpawnDefinition`, `ScriptableObjects/Balance/PowerUpSpawning.asset`): a capture of at least 5% rolls `min(60%, 15% + 2% × captured percent)`. At most one pickup exists on the board; it is placed on a random uncaptured, non-player cell at least 3 Manhattan cells from every standard enemy, uses a uniformly random type, rotates for readability, and despawns after 12 seconds (tunable, 0 = never). Pickups are pooled (`Prefabs/PowerUps/PowerUpPickup.prefab`) and tinted per type (Shield green, Freeze ice blue, Arena Tilt orange).
- Collection: entering the pickup's cell stores it immediately when the slot is empty. (Originally an occupied slot paused for a Keep/Replace prompt; superseded by instant replacement — see "Playtest Fixes: Tilt, Pickup Replace, Shield Grace".)
- Pause: `GameManager.SetPaused` sets `Time.timeScale = 0`, disables gameplay input, and rejects contact/failure reports without changing the life state, so an exposed trail and its movement direction resume unchanged. Enemy Volatile simulation and pickup lifetime also halt while paused. This is reusable for the later pause menu.
- Use: `E` (or `InputRouter.RequestAbility()` for the future Android button) consumes the stored ability while `Playing` and not paused. Each duration runs in a coroutine; `GetEffectRemaining` feeds the HUD. Reusing a type restarts its duration without compounding.
- Shield (4 s): `EnemyContact` and `Laser` failures are rejected; per the GDD it does not protect against enemies hitting the unfinished trail, self-intersection, or Volatile explosions. A translucent green bubble follows the ship.
- Freeze (3 s): `EnemyManager.SetMovementSuspended(true)` stops every standard enemy (including Unstable timers and Volatile interactions) and swaps renderers to an ice-blue material, restored on expiry. Frozen enemies remain hazardous on contact; lasers keep operating. Freeze expiry does not resume enemies after Stage Complete.
- Arena Tilt (5 s): a random left/right side is chosen; all active enemies receive a 1.2 world-unit/s drift added in `EnemyMovementModel`, the player's move speed is reduced by 20% from its pre-tilt value, and the Main Camera rolls 6 degrees (blended). Everything is restored on expiry.
- Stage Complete and Game Over end all effects, release the board pickup, and dismiss any pending decision. Boss-stage restrictions (no Tilt pickups, Freeze immunity) are deferred to the boss phase; upgrade hooks use the definition durations/values.
- Tests: 21 new EditMode tests cover spawn-chance boundaries/cap, slot store/decision/keep/replace/consume, drift movement, small-capture no-spawn, placement validity and single pickup, lifetime expiry, collection by cell, pause-based Keep/Replace (time scale, input, damage rejection, state preservation), empty-slot use, Shield blocking/not blocking and expiry, Freeze suspension/tint/restore, Tilt drift/slow/camera roll/restore, non-compounding Tilt, and Game Over cleanup. Full suite: 194 passed, 0 failed.
- Real `Game.unity` Play Mode: pickups spawned on valid cells and rendered; collecting and using Arena Tilt slowed the ship 5 → 4, applied (-1.2, 0) enemy drift, rolled the camera, and showed a HUD timer, then restored speed/drift on expiry; Freeze stopped and tinted all 4 enemies; Shield rejected a real `EnemyContact` report without life loss and displayed the bubble around the ship. Unity Console: 0 errors/warnings.

## Phase 11 — Campaign Stages + Progression

- Data: `ScriptableObjects/Campaign/Campaign.asset` lists four `StageDefinition` assets in `ScriptableObjects/Stages`, each with enemy spawn requests (prefab, definition, cell, direction) and laser placements (definition plus row/column).
  - Stage 1 First Contact: 2 Basic Bouncers, no lasers.
  - Stage 2 Crossfire: Basic, horizontal Linear, vertical Linear (new `LinearAlienVertical.asset`); horizontal laser on row 48.
  - Stage 3 Unstable Sector: Basic, Linear, 2 Unstable; faster horizontal (row 32, 1.4 s cooldown) and vertical (column 27, 1.6 s) lasers (`HorizontalLaserFast`/`VerticalLaserFast`).
  - Stage 4 Volatile Zone: Basic, vertical Linear, Unstable, 2 Volatile; fast lasers on row 40 and column 20.
- Flow: stage load → `GameplayState.Briefing` (input, damage, enemy movement, and laser cycling held; player at the default safe spawn) → Start → `Playing` → 75% capture → `StageComplete` → Continue → next stage briefing. After stage 4, Continue enters a "Stages 1-4 cleared" placeholder until the boss phase. Game Over shows the highest stage reached and Retry Campaign restarts stage 1.
- Per stage: fresh board (`BoardManager.ResetBoard`), lives reset to `startingLives` (with a `bonusLives` hook for Reinforced Hull), enemies despawned and respawned from data, laser emitters reused/created/hidden per layout (`LaserManager.ConfigureStage`), stage largest-capture statistic reset. Carried across stages: score, Power Meter charge, and the stored ability; in-flight shots, active effects, board pickups, and pending pickup decisions are cleared. Retry resets score, power, and stored ability.
- Temporary debug HUD panels (scaled to the Game view): briefing (stage name, description, enemy types, laser count, modifier/upgrade placeholders, Start), stage complete (score, captured %, stage largest capture, modifier bonus x1.00, Continue), game over (score, highest stage, Retry), and stages-cleared placeholder. Buttons or Enter.
- `EnemyManager.initialSpawns` in `Game.unity` is now empty; stage data owns spawning. `GameplayState.Briefing` was appended to the enum to keep existing values stable.
- Deferred: upgrade choice between stages (Phase 13), modifiers and their score bonus (Phase 14), boss stage and campaign complete (Phase 15), Main Menu (Phase 16).
- Tests: 11 new EditMode tests cover run progression/reset, stage largest-capture statistics, briefing enemy-type listing, stage load state (briefing lock, fresh board, spawns, lasers, damage rejection, frozen enemies), start gating, stage completion and continue (lives/board/player reset, score carry-over, new enemies and laser placement, movement on the new stage), hidden unused emitters, final-stage clear, Game Over retry, and laser cycling held during briefing. Full suite: 205 passed, 0 failed.
- Real `Game.unity` Play Mode: played through all four stages with real player movement (Stage 1 to 95.7%, Stage 2 to 93.6%, Stage 3 to 78.7%), verified every briefing's enemies and laser rows/columns, the Stage Complete panel (score, captured %, largest capture) with frozen enemies, carried score/power, lives reset to 3 each stage, then Game Over on stage 4 (highest stage 4, score 77711) and Retry back to stage 1 with score 0. Screenshots confirmed the briefing and stage complete panels. Unity Console: 0 errors/warnings.
- Tooling note: the Unity editor Pause toggle was found enabled twice during Play Mode QA (not caused by project code; no `Debug.Break` exists); unpausing resumed normal stage completion.

## Shield Contact Fix (post-Phase 11 playtest)

- Report: the player died from an alien while Shield was active.
- Root cause (reproduced in an EditMode test): while drawing a trail, the cell under the ship is itself a trail cell. An alien reaching the ship was detected as `TrailHit` on that cell before any `EnemyContact`, and Shield only blocks `EnemyContact`/`Laser`.
- Rule confirmed by the user (GDD literal): Shield protects the ship, not the trail. Aliens touching trail cells away from the ship still cost a life.
- Fix: `EnemyController` ignores trail cells under a shielded ship's body (initially the ship's cell; widened to the ship's collision circle by the hitbox work below). Regressions: shielded exposed ship reached by an alien survives; the unshielded case still dies; an alien touching the trail behind a shielded ship still reports `TrailHit`. Commit `da801bc`.

## Collision Size Matching (post-Phase 11 playtest)

- Report: hitboxes felt much smaller than the models. Measured: ship visual 0.75 (~4 cells) and Basic/Linear/Unstable 1.0 (~5.5 cells) versus a 0.108 center-distance contact check, single center-cell trail/capture checks, and center-point bouncing. Deaths were therefore decoupled from what the player saw.
- Decision: first implemented as "match both" with shrunken visuals (ship 0.32, aliens 0.48). After review the user asked to keep the original model sizes and grow the hitboxes instead, so the final values restore the original visuals and size collision to them.
- Final values: ship radius 0.375 (visual 0.75); Basic/Linear/Unstable radius 0.50 (visual 1.0, about 5.5 cells wide); Volatile radius 0.14 (its original 0.28 visual); pickup radius 0.25 (visual 0.5); Power Shot hit radius 0.06 (matching the 0.12 bar); shield bubble back to 1.15. Pickup minimum enemy distance raised from 3 to 6 cells so pickups cannot spawn inside an alien body.
- Rules: ship–alien contact when centres are within the radius sum (minimum 0.6 cell); alien trail contact and capture occupancy use every cell overlapped by the alien body (strict overlap — touching an edge does not count); aliens bounce before any part of the body enters captured territory or trail (a body already overlapping blocked cells falls back to centre movement so it cannot get stuck); lasers hit when the beam overlaps the ship body; Volatile blasts reach bodies (blast radius + target radius) and Volatile detonation triggers when bodies touch; Power Shot reach adds the alien radius; pickups collect when the ship body touches them.
- Tests: 9 new EditMode tests (circle cell overlap, radius-sum contact, trail contact one vs two columns from the body, bounce keeps the body out of captured cells, body-wide capture occupancy, laser body contact, Power Shot edge reach, and prefab visual size equals collision diameter for ship, all enemies, and pickups). Existing radius-0 fixtures keep their original semantics. Full suite: 217 passed, 0 failed.
- Real `Game.unity` Play Mode (final sizes): ship 0.75/radius 0.375 and aliens 1.0/radius 0.5 confirmed at runtime; stage 1 captures reached 55.3% using the body-wide occupancy snapshot (74 cells for two aliens); aliens kept bouncing with 0 body cells overlapping captured territory, including inside an 8-row uncaptured strip; a pickup spawned clear of both bodies; screenshot confirmed sizes. Unity Console: 0 errors/warnings.

## Playtest Fixes: Tilt, Pickup Replace, Shield Grace

- Arena Tilt direction. Report: when the arena tilted one way, aliens slid the other way. Root cause: `PowerUpManager` rolled the camera by `-side × degrees`; a positive camera roll lowers the right side of the arena on screen, so the drift went uphill. Fix: roll by `side × degrees`, so the side aliens drift toward is the side that tilts down. The regression asserts both the roll sign and the on-screen direction of the world right axis. Play Mode: a left drift of (-1.2, 0) produced a -5.8° to -6° roll and the aliens gathered on the lowered left side.
- Arena Tilt strength. Report: tilted aliens looked like they were free-falling into the wall. Cause: drift 1.2 exceeded every alien's sideways speed (a 1.2-speed bouncer moves ~0.85 sideways), so no alien could move against the tilt. Fix: `tiltEnemyDrift` 1.2 → 0.4 (asset and default). Aliens moving against the tilt now progress at ~0.45/s and aliens moving with it at ~1.25/s; the slowest case (Unstable at minimum speed, ~0.57 sideways) still beats the drift. A new asset guard test keeps the configured drift below 80% of every alien's slowest sideways speed and verifies uphill progress. Play Mode: during a +0.4 tilt one alien moved uphill from x 6.93 to ~4.0 while another moved downhill from 2.79 to 8.36.
- Instant pickup replacement (user request, supersedes the GDD/plan Keep/Replace prompt). Touching a pickup always stores it; an occupied slot is replaced immediately with no pause or prompt. Removed the pending-offer state, `ResolvePickupDecision`, `DecisionRequested`, and the HUD Keep/Replace panel. `GameManager.SetPaused` remains for the future pause menu and now has its own direct regression.
- Shield pass-through grace (user-chosen rule). Report: with Shield, flying through an alien was safe, but the alien then touched the trail just behind the ship and killed the player. Rule: an alien that touches the shielded ship gains pass-through grace; its trail hits are ignored until its body is fully off the trail and no longer touching the ship, even if the shield expires meanwhile. Other aliens touching the trail still cost a life, and the ship-body trail cells remain protected while shielded. Grace is cleared on activation and deactivation, so pooled reuse never inherits it.
- Tests: replaced the Keep/Replace slot and pause-decision tests with instant-replace coverage (model and manager, no pause, stored-changed event, usable immediately), added a direct pause regression, the tilt roll-direction assertion, a full pass-through grace lifecycle (granted on shielded contact, protects the trail behind the ship, outlasts shield expiry while still on the trail, ends once the body leaves, later trail hit kills), and grace not granted without Shield plus cleared on deactivation. Full suite after the tilt-strength guard: 219 passed, 0 failed. Unity Console: 0 errors/warnings.

## Phase 12 — 2.5D Presentation Foundation

### Step 1 — Camera (complete)

- Installed Cinemachine 3.1.7 (`com.unity.cinemachine`, required by the GDD; `SpaceXonix.Runtime` now references `Unity.Cinemachine`).
- Prototype: rendered 1080 × 1920 portrait comparisons of the current orthographic top-down view and perspective diagonal-down candidates at 25°/FOV 30, 35°/FOV 30, 45°/FOV 25, and 55°/FOV 22 (screenshots in the untracked `Temp/Screenshots/CameraCandidates`). Steeper pitches strengthen the AirXonix look but shrink the board's on-screen height (≈76%, 68%, 59%, 48%); far rows render at 77–83% of near-row width. The user selected **35° tilt, FOV 30**.
- `ArenaFraming` (pure) solves a perspective camera pitched 35° from top-down that fits the whole 54 × 96 board plus a 0.6 hover height inside viewport margins (sides 3%, bottom 8%, top 86%) for any aspect ratio, centred between the HUD margins, as large as the margins allow.
- `ArenaCameraRig` (on the new `ArenaCamera` object with a `CinemachineCamera`) applies that framing, re-frames when the output aspect changes (portrait device vs landscape editor), sets lens FOV/clip planes, and owns presentation roll. The Main Camera is now perspective with a `CinemachineBrain`. Gameplay objects and the board remain on the XY plane; no gameplay code reads the camera.
- Arena Tilt roll now goes through `ArenaCameraRig.SetRoll` → Cinemachine `Lens.Dutch` (smoothly blended) instead of rotating the Main Camera transform, which the brain would overwrite. Positive roll still lowers the right side, preserving the earlier direction fix.
- Tests: 6 new EditMode tests — framing fits within margins and is maximal for portrait, landscape, and tall-phone aspects; 35° diagonal-down orientation looking toward the far rows with 0.7–0.9 far/near row width; custom projection matches Unity's `Camera.WorldToViewportPoint`; rig roll sign and reset. Power-up tilt tests now drive the rig. Full suite: 225 passed, 0 failed.
- Real `Game.unity` Play Mode: the brain drove the Main Camera to the rig pose (perspective, FOV 30, 35° pitch, framing fits); activating Arena Tilt with drift (+0.4, 0) produced a 6° Dutch roll on the Main Camera with the board's right edge lower on screen (viewport y 0.486 vs 0.540). Screenshot confirmed the tilted perspective view with the HUD intact. Unity Console: 0 errors/warnings.
- Next: step 2 (chunked raised-territory board view).

## 2.5D Presentation Decisions

- Researched AirXonix: its 3D is presentation over flat Xonix rules (diagonal-down camera, hovering craft, shadows, solid filled territory). Full notes and work breakdown: `IMPLEMENTATION_PLAN.md` section 12.
- Approved 2026-09-17: perspective diagonal-down Cinemachine camera (deliberately replacing the GDD's orthographic camera), pixel-art billboard sprites, raised captured territory, scheduled as Phase 12 right after Campaign Stages + Progression.
- Constraint for every later phase: gameplay objects and the board stay on the XY plane; the 2.5D look comes only from the camera, visual-only child objects, and the board view. Arena Tilt's current camera roll is a placeholder until Phase 12 replaces it with a Cinemachine Dutch blend.

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
- `Assets/SpaceXonix/Scripts/Scoring` — score model, capture multipliers, and score manager
- `Assets/SpaceXonix/Scripts/Power` — power meter, power shot projectile, and power tuning
- `Assets/SpaceXonix/Scripts/PowerUps` — pickups, ability slot, and Shield/Freeze/Arena Tilt effects
- `Assets/SpaceXonix/Scripts/Campaign` — campaign/stage definitions, run progression, and stage flow
- `Assets/SpaceXonix/Tests/EditMode` — deterministic regression suite
- `Assets/SpaceXonix/Scenes/Game.unity` — representative gameplay scene

## Next Recommended Action

**Phase 12 — 2.5D Presentation Foundation** (see `IMPLEMENTATION_PLAN.md` section 12)

Before modifying anything, read `AGENTS.md`, `PROG.md`, `SpaceXonixProposal.md`, and `IMPLEMENTATION_PLAN.md`, then inspect `git status` and the existing implementation.
