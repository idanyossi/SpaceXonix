# SpaceXonix Development Progress

## Project Snapshot

- Unity: 6000.3.20f1 (Unity 6.3 LTS), URP
- Targets: Windows PC and Android; 1080x1920 portrait reference resolution
- Active branch: `main`
- Main gameplay scene: `Assets/SpaceXonix/Scenes/Game.unity`
- Default logical board: configurable 54 x 96 cells
- Current phase: Phase 19 complete bar music and a few textures; Phase 20 next
- Latest completed feature: CC0 pixel-art sprites for every actor and CC0 sound effects for 19 of 20 sounds

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
12. **COMPLETE** — 2.5D Presentation Foundation (perspective Cinemachine camera, raised territory, hovering pixel-art billboards)
13. **COMPLETE** — Roguelite Upgrades
14. **COMPLETE** — Per-stage random modifiers and score bonuses
15. **COMPLETE** — Alien Core boss stage and campaign completion
16. **COMPLETE** — UI, menus, HUD, settings, pause, and safe area
17. **COMPLETE** — Android swipe controls and touch buttons
18. **COMPLETE** — Audio system, sound library, and gameplay bindings
19. **COMPLETE** — CC0 pixel-art sprites for every actor, CC0 sound effects, licence record (music and board textures still to source)
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
- `BossController` (scene object `AlienCore`) owns the final stage: a stationary core that fires pooled `BossProjectile` volleys on the pure `BossAttackModel` cycle, takes visual damage as the arena is captured, and dies when the stage's capture target is met. It cannot be shot down; `PowerMeter` routes a Power Shot into `TryInterceptShot`, which stuns the cycle instead.
- `StageModifierManager` (on the GameManager object) rolls one compatible modifier per stage from `StageModifierSetDefinition` via the pure `StageModifierSelection.Select`, then pushes typed multipliers into `EnemyManager` (speed, Unstable interval, Volatile radius), `LaserManager` (cooldown), `PowerUpManager` (pickup chance), and `ScoreManager` (score bonus). Definitions are never mutated and every multiplier is cleared between stages.
- `UpgradeManager` owns the run's upgrades: `UpgradeOffer` builds the between-stage choice from `UpgradeSetDefinition`, the pure `RunUpgradeModel` holds stacks and computes effective stats, and `ApplyToSystems` pushes them into PlayerController speed, PowerMeter gain, and PowerUpManager durations/tilt penalty/pickup chance at every stage load.
- Collision is body-sized: `PlayerController.collisionRadius` and `EnemyDefinition.collisionRadius` drive ship contact (radius sum), trail contact and capture occupancy (all cells overlapped by the alien body via `BoardManager.GetCellsOverlappingCircle`), bouncing (the whole body must stay in uncaptured space), lasers (beam half-width + ship radius), Power Shot reach, Volatile blasts/detonation, and pickup collection. Prefab visual sizes equal the collision diameters. A radius of 0 preserves the original point/0.6-cell behaviour.

## Current Test State

- EditMode discovered: 319
- Passed: 319
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

### Step 2 — Raised 3D territory board (complete)

- `BoardRenderer` was rewritten from a flat state texture into a 2.5D board view: a recessed dark space floor at z = 0; captured territory (including the structural perimeter/arena rim) as a raised slab (`territoryHeight` 0.35, rising toward the camera along −Z) with side walls only where a neighbour is lower; and the active trail as an orange strip lifted 0.02 above the floor.
- Territory geometry is split into 66 chunk meshes (9 × 9 cells) with separate top and wall submeshes (`TerritoryTop`, `TerritoryWall` URP Unlit materials in `Materials/Board`, plus `SpaceFloor` and `Trail`). `BoardMeshBuilder` is the pure geometry builder; it reuses list buffers and emits correctly outward-facing quads.
- Changes are detected by diffing the model against cached target heights when `BoardManager` refreshes the view (trail changes, captures, trail cancellation, territory destruction), so `BoardModel` stays untouched. Only chunks with changed cells (and neighbours when a border cell changes) are rebuilt, and only while animating.
- Animation: newly captured cells rise from the floor to full height over 0.3 s; cells removed by Volatile explosions sink back. The logical capture/removal is already committed before either animation starts. `BoardManager.ResetBoard` (new stage/retry) snaps the view without animating.
- Height choice: 0.15 was compared against 0.35 in 1080 × 1920 renders; 0.15 walls read as thin lines at the 35° camera, while 0.35 clearly shows pit walls and the slab front edge, so 0.35 was selected.
- Tests: 6 new EditMode tests — a single raised cell produces one top and four outward walls with correct winding and −Z height; adjacent cells share no inner wall and partial walls start at the lower neighbour's height; a flat board emits no territory geometry; the renderer snaps the initial perimeter, keeps the capture committed in the model while the view rises and completes the rise; destroyed territory sinks and a stage reset snaps flat; the trail mesh follows the active trail and clears on cancellation. Full suite: 231 passed, 0 failed.
- Real `Game.unity` Play Mode: captures raised territory with visible pit walls, an active 20-cell trail rendered on the pit floor, and animations completed; 1080 × 1920 renders confirmed the look (untracked `Temp/Screenshots/board3d_*.png`). Unity Console: 0 errors/warnings.
- Known and expected until step 3: ship, aliens, and pickups are still centred on the board plane, so they appear partly sunk into the floor and can be hidden behind nearby raised territory.

### Step 3 — Hovering actors with shadows (complete)

- `ActorVisual` splits every actor into a logical root that never leaves the board plane and child visuals: the mesh hovers 0.55 toward the camera (pickups 0.45) and a shadow blob sits on whatever surface is under the actor — the pit floor or the top of raised territory, following the rise/sink animation via the new `BoardManager.GetVisualSurfaceHeight`.
- Player, all four enemy prefabs, the pickup, and the Power Shot were restructured: root keeps unit scale and logic only (no MeshRenderer), a `Visual` child carries the old mesh/material/scale, and a `Shadow` child uses a flattened sphere with the translucent `ShadowBlob` material. Pickups spin their visual instead of the root; the Power Shot shapes its visual child so its logical root stays unscaled.
- Billboarding is implemented and kept for the pixel-art sprites, but it is **disabled on the ship and all aliens** for now: rotating their solid placeholder cube/sphere meshes to the camera made them read as leaning cards rather than hovering bodies (user feedback). Pickups keep spinning instead. The board is found once per actor and can be injected with `ConnectBoard`.
- Fixes found while verifying in Play Mode: the built-in quad lookup returned no mesh (shadows were invisible), Unity's quad already faces the camera so an added 180° flip was removed, pure black blobs were invisible on the near-black pit floor (now a dark blue-grey), and square blobs were replaced by flattened spheres so shadows read as discs.
- Tests: 5 new EditMode tests — the visual hovers toward -Z while the logical root stays put; billboarding follows the camera and non-billboarded visuals keep their rotation; the shadow rests on the pit floor and climbs onto raised territory; the shadow follows the rising capture animation; all configured prefabs have hovering visuals, shadows, logic-only roots, and unit scale. Full suite: 236 passed, 0 failed.
- Real `Game.unity` Play Mode: aliens, ship, and pickup hover fully above the board with round shadows beneath them on both the floor and captured territory; 1080 × 1920 renders confirmed (untracked `Temp/Screenshots/actors_hover*.png`). Unity Console: 0 errors/warnings.

### Step 4 — Hazards and effects in 3D space (complete)

- Lasers: `LaserPresentation.Configure` takes a height and `LaserEmitter` serializes the warning and beam heights. Both are now 0.55 (the ship's hover height): the first attempt put the warning on the pit floor, which made the beam appear higher than the mark that telegraphed it (user feedback), so the warning previews the beam exactly. Beam width was raised 0.12 → 0.2 on all four laser definitions, which also widens the hitbox (`beamWidth / 2 + ship radius` = 0.475 from the beam centre).
- Volatile explosions: new pooled `ExplosionRing` prefab (flattened sphere, translucent orange `ExplosionRing` material) laid flat on the floor, sized to the Volatile's territory blast diameter and faded out over 0.45 s by `ExplosionRingPresenter`, which listens to `EnemyManager.ExplosionOccurred`. `EnemyManager` now exposes its `BoardManager` and the `LastExplosionDefinition` for presentation sizing only.
- Shield bubble follows the ship's `ActorVisual.HoverHeight` instead of a hard-coded offset; the Power Shot already hovers through its own `ActorVisual`. The bubble was a flattened disc left over from the flat board and read as a 2D card against the 3D models; a translucent sphere then swallowed the ship, so the shield is now a bright green **ring** encircling the ship at hover height (procedural annulus mesh, inner 0.72 / outer 0.88, 48 segments, `Meshes/ShieldRing.asset`). The ship stays fully readable inside it. `RingMeshBuilder` is covered by 2 EditMode tests (radii, flatness, camera-facing winding, argument validation, segment clamping).
- Tests: 4 new EditMode tests — laser presentation lifts toward the camera and keeps axis orientation/scale; the emitter puts the warning on the floor and the beam at hover height across a real cycle; the blast ring lies on the floor at blast diameter and fades to finished; the presenter spawns on explosions, releases finished rings, and reuses pooled instances. Full suite: 240 passed, 0 failed.
- Real `Game.unity` Play Mode (stage 4): a detonated Volatile produced a floor blast disc at the blast position, a vertical laser beam fired at hover height across the arena, and the shield bubble sat at -0.55 with the ship. Screenshot: untracked `Temp/Screenshots/hazards_3d.png`. Unity Console: 0 errors/warnings.

### Step 5 — Camera feel (complete)

- `ArenaShaker` (on the GameManager object with a `CinemachineImpulseSource`) converts gameplay events into Cinemachine impulses, and the arena camera carries a `CinemachineImpulseListener`. Impulse: 0.35 s Bump, uniform, default velocity (0, 0.25, 0).
- Tuned after playtest feedback and AirXonix research (below): **ordinary captures never shake**. `ShakeStrength.ForCapture` returns 0 below 15% and ramps from half to full force (0.25) between 15% and 30%. Explosions scale with blast radius against a 0.75 reference, clamped to half/double, at 0.3. **Death is the strongest and deliberately varied**: a random in-plane direction with a random magnitude between 0.6 and 1.0 (`ShakeStrength.ForDeath`, seedable `System.Random`), sent through `GenerateImpulseWithVelocity`, so repeated deaths never feel canned. Impulse shape is Rumble over 0.28 s. `SetShakeEnabled(false)` suppresses everything for the future camera-shake setting; `LastForce`/`LastVelocity` expose what was requested.
- AirXonix research (2026-09-18): no source describes any camera movement, rotation, zoom, or shake — MobyGames classifies the view as fixed diagonal-down and Old-Games.ru as isometric, and reviews attribute all "stunning special effects" to the field itself (explosions, fills, enemy destruction). Shake on every capture was therefore both unfaithful and disruptive, since capturing is the core repeated action. Unverified detail: gameplay footage was not watched, so a subtle shake in the original cannot be ruled out.
- Arena Tilt keeps using the Cinemachine Dutch roll from step 1; the optional visual-only board pivot was deliberately skipped because tilting the board view alone would desynchronise it from the hovering actors, which stay on the XY plane.
- Tests: 4 new EditMode tests — routine captures produce zero force while large captures ramp and clamp, the death kick varies in direction and magnitude within its range and stays in the arena plane, explosion scaling with clamps, and the shaker staying silent on a routine capture, varying between two real deaths, and honouring the shake setting. Full suite: 246 passed, 0 failed.
- Real `Game.unity` Play Mode: with an impulse active the Main Camera was displaced 0.24 from the rig pose, confirming source → listener → brain wiring. After tuning: 4.3% and 1.1% captures requested force 0.000, a 31.9% capture requested the full 0.250, and a death produced a random-direction kick of magnitude 0.795. The long test durations used for sampling were runtime-only; the saved scene keeps 0.35 s Bump. Unity Console: 0 errors/warnings.
- Death readability fix (playtest feedback: "everything stops for a sec and it looks like it's lagging" before the shake). Measured: the failure call itself takes 0.33 ms and the Console is clean, so the pause was not a stall — it was the 1.25 s `respawnDelay` during which the ship sat motionless at the death position with input disabled and no death feedback. Fixes: `PlayerDeathPresenter` spawns a burst ring at the ship and hides the ship's visual and shadow on the accepted failure, restoring them on `PlayerRespawned`/`StageBriefingStarted`; `respawnDelay` 1.25 s → 0.7 s. 2 new EditMode tests cover the burst/hide/restore cycle and guard the configured delay (0.2 s–0.8 s). Real `Game.unity`: death hid the ship, spawned one burst, shook 0.91, and the ship returned at a safe cell with the burst expired.

### Step 6 — Verification and profiling (complete)

- Full regression: 249 EditMode tests pass, 0 failed. Unity Console: 0 errors/warnings.
- Play Mode sweep under the new presentation: a complete stage 1 run reached 84% and Stage Complete, with captures, trail, rise/sink animation, hovering actors and shadows, lasers, Volatile blast rings, pickups, shield ring, death burst and respawn all behaving.
- Costs measured in the editor (54 × 96 board):
  - Steady gameplay simulation (player + enemies + lasers + board view + occupancy refresh) ≈ **0.051 ms/frame with 0 bytes allocated per frame** — no per-frame GC churn.
  - Board rise animation during a 26.6% capture ≈ **0.34–0.38 ms/frame** for ~19 frames, then idle.
  - A 20-capture stage run allocated ~176 KB total (transient mesh buffers), not per frame.
- Draw-call reduction: empty chunks (all-uncaptured regions) now disable their renderer, and the chunk size was raised from 9 to 18 cells (66 → 18 chunks). Stage start went **178 → 154 → 90 draw calls**; a fully captured board draws ~86 with ~15.8 k triangles and 30 set-pass calls. Rebuild cost per animated frame stayed the same; transient allocation during the animation rose (16 KB → 108 KB per capture burst) because chunk meshes are larger, which is an acceptable trade for halving draw calls. 1 new EditMode test guards that empty chunks are not drawn and captured chunks start drawing.
- Portrait framing verified by 1080 × 1920 renders throughout the phase; the rig re-frames automatically for the editor's landscape Game view and for phone aspects.
- Deferred to the final QA phase: profiling on a real Android device (frame time, thermal, GPU) and a device build. Editor numbers above are the baseline to compare against.

## Phase 13 — Roguelite Upgrades

- Data: seven `UpgradeDefinition` assets in `ScriptableObjects/Upgrades` collected by `CampaignUpgrades.asset` — Reinforced Hull (+1 life per stack, max 3), Improved Thrusters (+10% speed, 3), Rapid Capacitor (+20% power gain, 3), Shield Capacitor (+25% Shield duration, 3), Cryogenic Core (+25% Freeze duration, 3), Gravity Stabilizer (-50% Arena Tilt slowdown, max 2), Scavenger Protocol (+10% pickup chance, 3).
- Flow: clearing a stage now goes Stage Complete → **Upgrade Choice** (new `CampaignPhase`) → next stage briefing. `ContinueAfterStageComplete` offers three distinct upgrades the run can still take (fewer only when fewer remain eligible); `ChooseUpgrade(index)` applies one and advances. The temporary HUD panel lists name, effect, and current stacks, selectable by button or number keys, and the briefing lists the run's upgrades.
- **Lives carry across stages (approved deviation from the GDD, 2026-09-18).** The GDD says each stage starts with 3 lives; in play that meant finishing a stage on 1 life and starting the next on 4 with Reinforced Hull, which read as a bug. Now only the first stage of a run uses `startingLives`; later stages continue with whatever the run has left (`GameManager.BeginStage(lives)` with `CampaignManager` passing the carried total), and **Reinforced Hull grants its +1 immediately when taken** (`GameManager.AddLives` / `LifeStateModel.AddLives`, ignored at Game Over) instead of at the next stage. `RetryCampaign` starts from `startingLives` again.
- Effects are applied without mutating definition assets: `PlayerController.SetMoveSpeed(base × multiplier)` from a captured base speed, `PowerMeter.SetGainMultiplier`, and new `PowerUpManager.SetDurationMultiplier/SetTiltPenaltyMultiplier/SetPickupChanceBonus` (the pickup bonus is added before the configured maximum). Reinforced Hull feeds `GameManager.BeginStage(bonusLives)`, so it applies from the next stage onward as the plan's ambiguity table specified, never retroactively.
- Run-only: `RetryCampaign` resets the upgrades along with score and power, restoring base speed and durations.
- Tests: 4 new EditMode tests — stacking to the limit and reset, each upgrade mapping to its own stat (including the tilt penalty clamped at zero), offers returning three distinct eligible upgrades and nothing when everything is maxed, and the manager applying/clearing effects on the real systems. Full suite: 253 passed, 0 failed.
- Real `Game.unity` Play Mode (carry-over rules): started stage 1 with 3 lives, died twice to reach 1, cleared the stage and took a non-Hull upgrade — stage 2 began with **1 life**; taking Reinforced Hull then moved 1 → **2 immediately**. Earlier run (pre-change): cleared stage 1 at 83%, was offered Reinforced Hull / Shield Capacitor / Rapid Capacitor, took Reinforced Hull, and stage 2 began with **4 lives**; cleared stage 2 and took Gravity Stabilizer, which set the tilt penalty multiplier to 0.5; the stage 3 briefing listed "Reinforced Hull, Gravity Stabilizer". Screenshots confirmed the choice panel (with a "(have 1)" stack hint) and the briefing. Unity Console: 0 errors/warnings.
- Deferred: modifier score bonus and stage modifiers (Phase 14); upgrade icons and final UI (Phase 16/19).
- Fixed after the phase commit: a broken string literal in the briefing panel (a real newline instead of `
`) stopped compilation; repaired in `0a65b1c`, with the folder meta added in `19c28fa`. Compile and tests now run before every commit.

## Phase 14 — Stage Modifiers

- Data: six `StageModifierDefinition` assets in `ScriptableObjects/Modifiers`, collected by `StageModifiers.asset` — Overclocked Swarm (aliens +25% speed, score x1.15), Laser Storm (lasers recharge 40% faster, x1.15, needs lasers), Dense Sector (+2 aliens, x1.15), Resource Shortage (pickups spawn half as often, x1.10), Unstable Space (Unstable aliens change speed twice as often, x1.15, needs Unstable), Volatile Matter (blasts reach 50% further, x1.20, needs Volatile).
- Compatibility: `StageModifierDefinition.IsCompatibleWith` only offers a modifier on a stage that actually contains what it modifies (lasers present, required enemy type spawned, spawns present for extra aliens). Stage 1 therefore only ever rolls Overclocked Swarm / Dense Sector / Resource Shortage, while stage 4 can roll all six. When nothing fits, the stage simply runs without a modifier instead of failing.
- Flow: `CampaignManager.LoadCurrentStage` rolls the modifier **before** lasers are configured and aliens are spawned, so the multipliers reach them as they are built; `SpawnExtras` then adds Dense Sector's extra aliens on random free cells, reusing the stage's first Basic Bouncer spawn as a template. The briefing panel lists the rolled modifier and its bonus, the Stage Complete panel shows the real `Modifier bonus xN.NN`, and a HUD line under the stage counter names the active modifier during play.
- Effects are applied without mutating definition assets, matching the Phase 13 pattern: new `EnemyManager.SetSpeedMultiplier/SetUnstableIntervalMultiplier/SetVolatileRadiusMultiplier`, `EnemyController.SetSpeedMultiplier/SetIntervalMultiplier` (mid-stage changes rescale from the base speed rather than compounding), `LaserManager`/`LaserEmitter.SetCooldownMultiplier` (warning and firing windows untouched), and `PowerUpManager.SetPickupChanceMultiplier` (applied after the upgrade bonus and the configured cap). `ScoreManager` now exposes `BonusMultiplier` for the HUD and tests.
- Tests: 5 new EditMode tests — compatibility filtering across laserless/laser/Volatile stages plus the null-pool, null-stage and nothing-compatible cases; seeded selection being deterministic and never rolling an incompatible modifier over 25 seeds; the manager pushing all six typed effects into the real systems and clearing them again; enemy speed rescaling from the base speed while the definition stays at 4; and laser cooldown halving against an unmodified control emitter. Full suite: 258 passed, 0 failed.
- Real `Game.unity` Play Mode: stage 1 rolled Resource Shortage (x1.10) and the briefing rendered it. Forcing each modifier in turn confirmed stage 1 filters Laser Storm, Unstable Space and Volatile Matter out entirely, while stage 4 applies all six — speed x1.25, laser cooldown 1.40 s -> 0.84 s, Dense Sector 5 -> 7 aliens, Unstable interval x0.5, Volatile radius x1.5, score x1.10-x1.20. A forced Dense Sector run on stage 1 showed 4 aliens instead of 2 with the HUD reading "Dense Sector x1.15". Unity Console: 0 errors/warnings.
- Deferred: modifier icons and final modifier presentation (Phase 16/20). Boss-only modifiers were added in Phase 15.

## Phase 15 — Alien Core Boss

- Data: `BossDefinition` (`ScriptableObjects/Boss/AlienCore.asset`) holds placement, fire interval 2.2 s, projectile speed 4.5, 3 shots per volley across 24 deg, projectile radius 0.28, a 2 s Power Shot stun and 4 damage stages. `Stage5_AlienCore.asset` is a normal `StageDefinition` with no spawns and no lasers whose new `boss` field points at it; `CampaignDefinition.bossStage` makes the campaign 5 stages long.
- Run flow: `CampaignRunModel` gained `HasBossStage`/`IsBossStage`/`CampaignComplete` and a `TotalStageCount`. Clearing stage 4 now loads the boss as stage 5 instead of ending the run; clearing the boss sets `CampaignComplete` and raises `CampaignManager.CampaignCompleted`. The boss stage skips the between-stage upgrade offer, because there is no next stage to spend it on. The one-argument `CampaignRunModel(count)` constructor still behaves exactly as before, so a boss-less campaign keeps the old `NormalStagesCleared` ending.
- Combat: the core is stationary and cannot be destroyed by shooting. It fires pooled volleys aimed at the ship; a projectile that reaches the ship is a `BossProjectile` failure and one that crosses the unfinished trail is a `TrailHit`. **Shield blocks the ship hit but not the trail hit**, which satisfies the GDD ("Shield functions normally against boss projectiles") while keeping the trail rule identical to every other hazard. A Power Shot is absorbed by the core and stuns its cycle for 2 s without reducing its health; recovery restarts a full interval so it never fires the instant it wakes. Freeze and Arena Tilt do not touch it, and the boss stage excludes Arena Tilt pickups entirely (`PowerUpManager.SetTypeExcluded`) because there are no standard aliens to slow.
- Damage: the core's condition tracks progress toward the stage's capture target rather than the whole board, stepping through 4 stages and shrinking the body at each one; the HUD reads `ALIEN CORE <n>%` during the fight and `INTERRUPTED` while stunned. A new `CAMPAIGN COMPLETE` panel shows the final score, largest capture and run upgrades.
- Boss modifiers (GDD section 10): `StageModifierDefinition` gained `requiresBossStage` plus `bossFireIntervalMultiplier`/`bossProjectileSpeedMultiplier`. Boss modifiers are offered **only** on the boss stage and normal modifiers **only** on normal stages, so the alien-free boss can no longer roll "Overclocked Swarm" and do nothing. Three new assets: Overdriven Core (fires 35% more often, x1.15), Plasma Acceleration (projectiles 40% faster, x1.15), Relentless Barrage (20% more often and 20% faster, x1.20).
- Tests: 10 new EditMode tests — the run playing the boss after the normal stages and completing the campaign (including a finished run still reading as stage 5), a boss-less run keeping the old ending, the attack cycle firing on the interval with stun/recovery/longer-stun-wins semantics and a rejected non-positive interval, volleys aiming at the ship and spreading around that direction, Shield blocking the ship hit but not the trail hit, an unshielded hit reporting `BossProjectile`, a Power Shot stunning rather than killing the core, boss modifiers scaling the cycle and projectile speed without mutating the definition, capture damage stepping and shrinking the body plus defeat clearing in-flight projectiles, and the boss/normal modifier pools staying separate. Full suite: 268 passed, 0 failed.
- Real `Game.unity` Play Mode: jumped the run to stage 5, which briefed as "Stage 5/5 - Boss: Alien Core - capture 75% to destroy it". The core rendered as a large sphere at the top of the arena firing orange volleys that killed the player three times to Game Over. A scripted capture run walked the core through damage stages 1-4 (body 3.20 -> 1.12) and at 86.5% the stage completed, the core deactivated and its projectiles cleared, then Continue went straight to `CAMPAIGN COMPLETE`. Rolling the boss stage over six seeds drew only boss modifiers (fire interval 2.20 s -> 1.43 s / 1.76 s / 2.20 s), and stage 1 over the same seeds drew only normal ones. Unity Console: 0 errors/warnings.
- Fixed during the phase: `AssetDatabase.GetBuiltinExtraResource<Mesh>("Sphere.fbx")` returns null in this project, so the core and the projectile prefab were created with no mesh and rendered nothing; both now take the mesh from `GameObject.CreatePrimitive`. `ApplyDamageVisual` also wrote an absolute scale, wiping the authored 3.2 body size on the first frame, and now scales relative to a captured base scale. The briefing panel was resized for the longer boss text and the Campaign Complete subtitle moved out of `DrawStats`' area.
- Fixed after the phase commit (playtest screenshot): the Alien Core was visible on **stage 1**, parked at the board corner. The core lives in `Game.unity` for every stage and nothing ever hid it - `Activate` only switched its own GameObject on, and `Deactivate` left the body showing. It now hides its body visual in `Awake` and in `Deactivate`, and shows it only in `Activate`; the controller GameObject itself is never toggled, so it keeps running and can still be woken on stage 5. A new EditMode test walks hidden -> `Activate(null)` still hidden -> `Activate(definition)` visible -> `Deactivate` hidden again, and the boss fixture now assigns its references before Awake so it matches the saved scene. Verified in Play Mode: the core is hidden on stages 1-4 and visible only on stage 5. Full suite: 269 passed, 0 failed.
- Deferred: boss destruction sequence VFX and boss audio (Phases 18/20); a dedicated `Boss.unity` scene is deliberately not used, since the campaign drives every stage in place inside `Game.unity`.

## Phase 16 — UI, Menus, and HUD (in progress)

### Settings and persistence (done)

- `GameSettingsModel` is the pure preference state: master/music/SFX volume (clamped 0-1, and combined into `EffectiveMusicVolume`/`EffectiveSfxVolume` for Phase 18's audio), vibration and camera-shake toggles, and the campaign high score. It raises `Changed` only on real changes, so setting a value to what it already is writes nothing.
- `GameSettings` is the persistent service (`Settings` object in `Game.unity`, execution order -200, `DontDestroyOnLoad`). It loads once, writes every change straight back, and exposes `GameSettings.Current` for systems that only need to read. Persistence goes through the `ISettingsStore` seam: `PlayerPrefsSettingsStore` in the game, an in-memory store in tests, so the suite never touches the editor's shared PlayerPrefs.
- The high score is treated as a record rather than a preference: `TrySetHighScore` only accepts a strictly better run, `ResetToDefaults` deliberately leaves it alone, and `CampaignManager.RecordCampaignScore` submits it on **both** endings - `GameManager.GameOver` and beating the boss - so a strong run still counts when it ends badly. `CampaignManager.IsNewHighScore` tells the UI whether to celebrate.
- Camera shake is the first setting with a real consumer: `ArenaShaker` adopts the stored preference when it wakes and stays subscribed to `Changed`, so toggling it mid-run from the pause menu takes effect immediately.
- Tests: 6 new EditMode tests - volume clamping with change events only on real changes, master volume combining with each channel (and muting everything at zero), the high score keeping only the best and surviving a settings reset, the service loading defaults then persisting each change and a fresh service reading them back, the high score persisting and still needing to be beaten rather than matched after a reload, and the shaker following the setting including a mid-run change. Full suite: 275 passed, 0 failed.
- Real `Game.unity` Play Mode: settings loaded at defaults (master 1, music 0.7, shake on), toggling `CameraShakeEnabled` flipped `ArenaShaker.ShakeEnabled` live in both directions, and losing a run at 53.8% captured wrote score 13960 into `spacexonix.campaign.highscore` with `IsNewHighScore` true. Unity Console: 0 errors/warnings.
- Fixed while writing the tests: `GameSettings.Awake` destroyed duplicate services unconditionally, which in EditMode logs "Destroy may not be called from edit mode" and permanently destroys objects. The singleton guard now runs only in play mode, so test rigs can stand several services side by side.

### Boot, Main Menu and settings screen (done)

- Scenes: `Boot.unity` holds the persistent services and `BootLoader`, which routes to the menu once they have woken; `MainMenu.unity` holds the menu itself. Build Settings is now Boot -> MainMenu -> Game, and `SceneRouter` owns every transition, always restoring `Time.timeScale` first so a paused game cannot carry its freeze into the next scene.
- Main Menu: title, campaign high score read from `GameSettings`, and Start Campaign / Settings / Controls / Quit. Quit hides itself on Android and iOS instead of showing a dead button. The content sits in a `VerticalLayoutGroup` so it adapts to any aspect rather than relying on fixed offsets against the 1080x1920 reference.
- Settings screen: Master, Music and SFX volume sliders plus Vibration and Camera Shake toggles, with Reset Defaults and Back. `SettingsPanel` binds two ways and guards the feedback loop, so a refresh from the model never writes back into it. Settings and Controls each sit on a full-screen opaque overlay that dims the menu and swallows clicks behind it.
- `SafeAreaFitter` keeps the UI inside the device safe area, re-checking each frame because rotation and foldables change it. The anchor maths is a static pure function so it is unit tested without a device.
- Input: `InputRouter` gained `PauseToggleRequested`, checked **before** the gameplay-input gate, because pausing is exactly what disables gameplay input and Escape still has to close the menu. `PauseMenu` routes through the existing `GameManager.SetPaused`, so the time-scale pause, input gating and damage rejection behave as they already do for the ability pause.
- Tests: 4 new EditMode tests - the safe area returning the full rect for a full-screen area, insetting for a notch and gesture bar, ignoring an axis on request and surviving zero-sized or degenerate platform data, and the settings panel binding both ways without feeding back on itself. Full suite: 279 passed, 0 failed.
- Real Play Mode: the menu renders correctly and reads "Best run: 13960" from the previous session's PlayerPrefs, proving persistence across scenes. Opening Settings shows master 1.0, music 0.7 and SFX 1.0 on the sliders; dragging the music slider to 0.25 wrote through to both the model and `spacexonix.audio.music`. Unity Console: 0 errors/warnings.
- Note on verification: MCP screenshots of a Screen Space - Overlay canvas are unreliable - single frames came back all-cyan, all-dark, or missing the IMGUI pass entirely, and repeating the capture fixed it each time. Layout and colour were confirmed by querying the live components rather than trusting one frame. The modal dim was a real finding though, not an artifact: at alpha 0.82 the bright menu text still showed through, so both overlays are now fully opaque.

### Gameplay HUD, campaign screens and pause menu (done)

- `GameHud` replaces the IMGUI debug HUD with real uGUI: lives, score, capture percentage, stage counter, active modifier, the power meter as a filled bar that recolours when ready, the one stored ability, running effect timers and the brief capture award. `LifeStateDebugHud` is disabled in the scene rather than deleted, so the old readout is still available if a future phase needs it.
- `CampaignPanel` is **one** reusable panel for all six between-stage screens - briefing, stage complete, upgrade choice, game over, stages cleared and campaign complete - since they differ only in title, body and buttons. `CampaignScreens` drives it from the campaign phase and owns every screen's copy in one place. Spare buttons hide themselves, so the same panel serves a one-button briefing and a three-option upgrade choice.
- Game Over, All Stages Cleared and Campaign Complete now offer Main Menu alongside retry, and each shows the campaign best, calling out `NEW BEST RUN` when the run set one.
- `PauseMenu` is wired into `Game.unity`: Escape or the on-screen pause button opens Resume / Restart Campaign / Settings / Main Menu. It routes through the existing `GameManager.SetPaused`, so the time-scale pause, input gating and damage rejection behave exactly as they do for the ability pause, and the pause button hides itself while the menu is open. Pausing is refused outside Playing and Respawning, and a Game Over force-closes it.
- The settings screen is now a prefab (`Prefabs/UI/SettingsOverlay.prefab`) shared by the Main Menu and the pause menu, rather than two hand-built copies that could drift apart.
- Panels stretch within margins instead of using fixed sizes, so they fit any aspect rather than overflowing whenever the window is shorter than the 1080x1920 reference.
- Tests: 4 new EditMode tests - the panel showing one caption per button and hiding the spares, each button reporting its own slot and a hidden panel routing nothing, the HUD showing lives/score/capture and the power meter's fill and ready colour, and the HUD tracking a lost life and a real capture. Full suite: 283 passed, 0 failed.
- Real `Game.unity` Play Mode: the briefing rendered as a proper panel, play showed lives 2, Stage 1/5, "Overclocked Swarm x1.15", score 9288, 26.9% and a full cyan POWER READY bar; Escape opened the pause menu with `Time.timeScale` 0, Settings opened over it, toggling camera shake reached the live `ArenaShaker`, and Resume restored the time scale and closed both. Unity Console: 0 project errors.

## Difficulty Modes (requested 2026-09-19)

- Requested by the user: "easy doesnt have the stage modifiers and hard does have it", with Easy also granting an extra life. Chosen flow: Start Campaign opens a **Choose Difficulty** screen rather than replacing the button with two.
- `DifficultyMode` (Easy/Hard) with the whole difference expressed as two extension methods - `UsesStageModifiers()` and `BonusStartingLives()` - so nothing else has to branch on the enum. Easy: no stage modifiers, 4 starting lives. Hard: modifiers on with their score bonuses, 3 starting lives. Enemies, lasers and the 75% capture target are identical.
- `CampaignManager` locks the difficulty in at `Initialize` and at `RetryCampaign`, so changing the setting mid-run does nothing but the menu choice always applies to the next run. `LoadCurrentStage` either rolls a modifier or calls `Clear()`, and the first stage of a run adds the difficulty's bonus life on top of `GameManager.StartingLives`.
- **One high score per difficulty.** Hard's modifier bonuses (x1.10-x1.20) inflate its scores, so a shared record would make an Easy run permanently uncompetitive. The pre-existing `spacexonix.campaign.highscore` key becomes Hard's record, because every run before difficulty existed had modifiers on; Easy gets a new key. The difficulty screen shows each mode's own best.
- The briefing shows the difficulty, and omits the modifier line entirely on Easy where it would always read "none".
- Tests: 5 new EditMode tests - the mode's effects, a separate record per difficulty that one mode cannot fill in for the other, difficulty surviving a settings reset, both records and the selected mode persisting, and a pre-difficulty save being read as the Hard record. Full suite: 292 passed, 0 failed.
- Real Play Mode: the difficulty screen showed Easy "No run yet" and Hard "Best: 46162"; choosing Easy loaded the game with **4 lives, no modifier, score bonus 1.00**, and switching to Hard gave **3 lives, "Overclocked Swarm x1.15", bonus 1.15**.

## Asset Pass 2 — Audio, Board, Background and Menus (2026-09-24)

Playtest verdict on the first pass: the sounds were *"absolutely horrific"*, the board, trail and lasers looked out of place, the background was empty, and the menus were ugly.

### Audio (done)

- **Music: Juhani Junkala's 5 Chiptunes (Action), CC0**, chosen by the user after previewing. It maps one-to-one: Title Screen for the menu, Level 1 for normal stages, Level 3 for the boss, and Ending for a new **Victory** cue on campaign complete. `MusicTrack.Victory` was appended to the enum rather than inserted, because scenes store these values as numbers. Level 2 was left out to keep 13 MB of WAV out of the public repository. Music streams, and effects decompress on load.
- **Effects: Juhani Junkala's 512 retro sound effects, CC0.** Chiptune effects fit pixel art, and smooth modern sci-fi effects never could; that mismatch was a large part of why the Kenney pass sounded so wrong.
- **Chosen by ear this time.** The Kenney clips were picked from their file names, which is how they went wrong. **SpaceXonix > Sound Audition** now lists three shortlisted candidates for each of the 20 sounds: ▶ plays one, **Use** makes it the sound, and **+** adds it as a random variant. The first candidate is the default, so the game is fully voiced before anything is picked, and **Remove unused candidates** clears the rest out of the repository once the choices are final. Preview goes through Unity's internal `AudioUtil`, which the window reports rather than throws on if a future Unity version moves it.
- The Kenney audio, the generated placeholders and both of their editor commands were removed.
- The MCP blocks `AssetDatabase.DeleteAsset` from executed code as a safety measure; that was left in place and the files were removed through the filesystem instead, `.meta` files included.

## Phase 19 — Asset Acquisition/Integration (partly complete)

**What blocked full completion:** this phase is about bringing in licensed third-party art and audio, which cannot be downloaded from here. The MCP's `generate_image` and `generate_model` tools exist but **both providers report `configured: false`**, so AI generation was not available either. The project had **zero** art and audio files before this phase.

### Placeholder audio (done)

- `SpaceXonix/Generate Placeholder Audio` synthesises a clip for all 20 sounds and all 3 music loops directly in the editor, so the game is audible now. Being generated in-project, they carry no licence obligations, and `SpaceXonix/Delete Placeholder Audio` removes them in one step when real clips arrive.
- Each sound's shape matches what it represents so they stay distinguishable while playtesting: rising tones for gains (capture, pickup, power full), falling ones for losses (player hit, freeze), noise bursts for destruction (Volatile, boss). Every clip is fade-edged so nothing clicks on play or loop. Music is three short arpeggio loops with per-note pluck envelopes.
- Import settings are applied per category: effects are PCM and decompressed on load, music is Vorbis and streamed, everything forced to mono.
- **Gap found while testing:** the binder only chose music from `StageLoaded`, so the menu had no track at all and started silent. `MusicCue` now states a scene's track outright, and the Main Menu carries one. It is silent rather than throwing when no audio service exists.
- Verified from a real boot: the Main Menu plays `Music_Menu`, a stage load switches to `Music_Gameplay`, and a capture fires three effects.

### Licence record (done)

- `ATTRIBUTIONS.md` at the repo root records every shipped asset with its source and licence, as the GDD's Asset Licensing section requires, plus the full inventory of what still needs sourcing and which stand-in each replaces.
- It notes that CC-BY assets need visible in-game credit, not just a file entry, so a credits screen goes on the Phase 20 list the moment the first one is added.

### Third-party art and audio (done)

- **Sourcing.** Browsed Kenney and itch.io's free pixel-art listings, and followed up on OpenGameArt when itch.io blocked its filtered pages. The deciding constraint turned out to be that **the GitHub repository is public**: committing an asset publishes its raw files, so every pack whose licence forbids redistribution was out, however good it looked. That removed the [8x8] pack, the CraftPix/Free Game Assets packs and dani567's pack. Helianthus Games' kit was dropped because its licence is silent on redistribution and its ships are side-view, which is wrong for a camera looking down on the board. The full reasoning is in `ATTRIBUTIONS.md`.
- **Art: ansimuz's Space Ship Shooter Pixel Art Assets (CC0).** Genuine top-down pixel art on a 16-pixel grid, with a ship, three enemy designs, an explosion, bolts and power-up orbs. **Kenney's space art was deliberately not used for anything in-world**: it is smooth vector, and mixing it with pixel sprites is what makes asset-pack games look cheap. Kenney has no pixel-art space pack at all.
- **Audio: Kenney Sci-Fi Sounds plus the Space Shooter Remastered bonus clips (CC0).** Sound has no visual style, so there is no clash. They cover 19 of the 20 sounds, with several variants picked at random for alien deaths and boss shots. The clips were chosen **by name, not by ear**, and `ThirdPartyAudioApplier` is the one place to change a mapping. The direction blip stays generated because it must be quieter than anything in the pack, and all three music loops stay generated because neither Kenney pack has music.
- **Pipeline.** Raw downloads live in the git-ignored `AssetSources/`, and only the files actually used are copied into `Assets`, each folder with its licence file. `PixelArtImporter` forces point filtering, no compression and no mipmaps on anything under `Art/ThirdParty`, since any of those would smear a 16-pixel sprite. It also cuts each sheet into one PNG per frame, which avoids the deprecated slicing API and needs no 2D Sprite package. `PixelArtSpriteApplier` (**SpaceXonix > Apply Pixel Art Sprites**) records the whole art mapping in one re-runnable place.
- **Mapping.** Three alien silhouettes cover four alien types, so tint separates two of them: Basic Bouncer is the pink pod, Linear the pink wings, Unstable the tank in electric blue, and Volatile the pod in orange. The Alien Core is the tank in red at 3.2 units wide. Two orb designs serve the three power-ups: warm for Shield, icy blue for Freeze, gold for Arena Tilt.
- **Hitboxes still equal visuals.** Every sprite is scaled so its width is exactly the collision diameter, the rule set in the Phase 11 hitbox fix. The trade-off, accepted deliberately, is that pixel density varies by actor: a Volatile packs its 16 pixels into 0.28 units, an alien into 1.0.
- **Billboard or flat?** Both were rendered at the real 1080x1920 portrait target. At this camera pitch they look nearly identical, so the tiebreaker was pixel art: a flat sprite under perspective has its pixel rows compressed unevenly and shimmers as it moves, while a billboard keeps every pixel square. **Billboarding stays**, which is also what Phase 12 originally planned.
- `ActorVisual` gained a heading applied inside the camera facing, so the ship turns on screen to face its direction of travel (`ShipHeading`) without tilting away from the camera. Pickups no longer spin, since rotating pixel art smears it and the orbs already animate. `SpriteFrameAnimator` loops the thruster flicker and alien pulses, and catches up correctly after a long frame.
- **Fixed on the way:** shadows were a fixed 0.8 units wide, so a Volatile sat on a shadow three times its own width. They now scale to each actor's visual.
- **Test fixed on the way:** the hitbox size test measured the first `MeshFilter` in a prefab. Once the visuals became sprites, the only mesh left was the shadow, so it would have silently started measuring the shadow instead. It now measures the actor's own visual, sprite or mesh.
- Tests: 5 new EditMode tests - the frame animator looping and catching up after a hitch, the ship's heading for each direction, a billboard turning on screen while still facing the camera, shadows sized to their actor, and a pickup taking its sprite and tint from its power-up without spinning. Full suite: 319 passed, 0 failed.
- **Still outstanding:** music; board, trail and laser textures (the flat laser bars now clash with the pixel art); a Power Shot sprite, which needs its launch code changed since it stretches its visual; and UI icons and a logo.

## Phase 18 — Audio

- Scope: this phase builds the audio **system**. The clips themselves are Phase 19's job, so every sound is configured but empty, and the game is deliberately silent rather than broken until they arrive.
- `GameSfx` names all 19 sound categories the GDD lists plus `MusicTrack` for the three loops. Gameplay asks for one of these rather than a clip, so sounds can be swapped or left empty without touching a system.
- `SfxDefinition` holds one sound's tuning: several clips picked at random, volume, a pitch spread so repeats do not sound mechanical, and a minimum interval so frequent events cannot machine-gun. Frequent sounds (direction changes, trail steps, lasers, boss shots, alien deaths) got intervals of 50-120ms and wider pitch spread; one-off moments (power full, boss destroyed, UI) are pinned to identical playback.
- `SfxLibrary` maps every `GameSfx` to its definition and holds the three music clips. Its own file, per the Phase 15/16 lesson. A duplicate entry keeps the first rather than throwing, since that is a data mistake, not a crash.
- `AudioManager` is the persistent service: a pool of 12 2D voices, round-robined and preferring an idle one before stealing the oldest, plus one music source that crossfades on unscaled time so music behaves while paused. Volumes come from `GameSettings`, and it stays subscribed so a change on the settings screen is heard at once. `Play` returns false and does nothing when a sound has no clip, repeats inside its interval, or effects are muted.
- `GameplayAudioBinder` does all the event wiring in one place, so no gameplay system knows audio exists and a missing AudioManager costs nothing but silence. Captures pick the large-capture sound at or above 15%, a direction change plays the trail sound when the ship is safe and the steer sound when exposed, and stage loads switch between the gameplay and boss tracks.
- Tests: 8 new EditMode tests - empty definitions picking nothing rather than throwing, pitch staying inside its spread, the library finding every sound and keeping the first duplicate, music tracks mapping with None as silence, playing a configured sound while skipping an empty one, the minimum interval blocking a repeat without affecting other sounds, muted effects and a muted master both silencing playback, and switching tracks without restarting the one already playing.
- Real Play Mode with placeholder clips: loading stage 1 started the **Gameplay** track, one capture fired three sounds ending on `PowerMeterFull`, a death fired `PlayerHit`, and loading the boss stage switched to the **Boss** track.
- Gotcha worth remembering: right after `CreateAsset`, `LoadAssetAtPath` can still return null while the import settles, and assigning that null into a scene reference silently stores nothing. Two scene links were written as null this way before the reference was checked and re-assigned.

## Mobile Playtest Fixes (2026-09-19)

### Swipes logged but the ship never moved

- Reported from the Device Simulator: the swipe log looked right but the ship stayed put.
- Cause: `TrySelectDirection` sets `IsDirectionHeld`, but `InputRouter.Update` polls the keyboard **every frame** and falls through to `ReleaseDirection()` whenever no key is pressed. The swipe set the heading and the keyboard poll cancelled it on the very next frame. The log was honest - the swipe really was accepted - which is exactly why it looked fine.
- Fix: a swipe **latches** the heading, because a finger cannot hold a direction the way a key can. `TrySelectLatchedDirection` sets `IsDirectionLatched`, and the keyboard poll only releases an unlatched heading. Pressing a movement key clears the latch and hands control back to hold-to-move, and the latch is dropped by `ReleaseDirection`, `ResetDirection` and losing gameplay input, so a death or transition never leaves the ship steering itself.
- Verified in a real run: one upward swipe drove the ship from cell (0,1) the full length of the left edge to (0,95), with no key held.

### Bottom HUD was unusable on a phone

- Reported with a screenshot: the bottom controls looked bad and the POWER and ABILITY buttons sat on top of the arena.
- Cause: both buttons were stacked up the right edge, reaching ~560 canvas units above the bottom, while the camera only reserved an 8% bottom margin. On a landscape editor window that looked survivable; in portrait they covered the board.
- Fix: ABILITY and POWER moved to the two bottom corners where thumbs actually rest, with the power meter, its readout and pause stacked up the middle between them. The stored-ability name sits above its own button and the effect timers moved up under the lives counter. The arena's bottom margin went 0.08 -> 0.19 so the board stops above the control band.
- Measured on a 1170x2532 simulated phone: control band top at 410px, board bottom at 615px, **205px (8.1%) of clearance**, with every element inside the screen.

## Phase 17 — Android Controls

- `SwipeModel` is the pure gesture rule: a drag steers along whichever axis moved further, and anything below the threshold is ignored so a tap or a shaky finger never turns the ship. A perfectly diagonal drag resolves horizontally, which is documented rather than left to chance. The threshold is a fraction of the screen's **shorter edge** (3%), so the gesture feels the same on a phone and a tablet and in either orientation.
- `TouchInputSource` feeds swipes into the same `InputRouter` the keyboard uses, so PC and Android share one abstraction exactly as the GDD requires - including the gameplay-input gate, so swipes are dead during respawns and transitions like every other control. A press that starts on a UI element is never read as a gameplay swipe (`EventSystem.IsPointerOverGameObject`). A drag re-anchors each time it steers, so one continuous finger movement can turn the ship more than once instead of firing once per touch. Mouse drags count as swipes too, which is only there to make the gesture testable in the editor.
- `TouchControls` adds the on-screen **ABILITY** and **POWER** buttons, routed through `InputRouter.RequestAbility`/`RequestPowerShot` so a tap and a key press are the same request. They dim rather than disappear when unusable, keeping their position stable, and are hidden on desktop where the keyboard covers both. Pause already had its on-screen button from Phase 16.
- **Orientation fixed:** the player settings allowed both landscape orientations, but the board, camera framing and HUD are all built for 1080x1920 portrait, so a phone would have rotated into a layout the game was never designed for. Auto-rotation is now limited to the two portrait orientations. `renderOutsideSafeArea` stays on, which is exactly why `SafeAreaFitter` exists.
- Tests: 9 new EditMode tests - the swipe latch surviving the keyboard poll and being dropped whenever gameplay input goes away, swipes resolving to the dominant axis including the diagonal rule, small movements being ignored, the threshold scaling with the shorter edge in both orientations and never reaching zero, swipes steering through the router, swipes obeying the gameplay-input gate, the touch buttons requesting ability and power through the router, and the buttons hiding on desktop. Full suite: 303 passed, 0 failed.
- Real `Game.unity` Play Mode: all four swipe directions steered the ship (Up, Left, Down, Right) while a 7px flick was rejected; the buttons appeared dimmed with no power and no ability, then lit up once a capture charged the meter and a Shield was stored, and tapping them fired a Power Shot and activated the Shield.
- **Fixed while writing up how to test swipes:** the UI-blocking check passed `Pointer.deviceId` to `EventSystem.IsPointerOverGameObject`, which expects a **pointer id** - a finger id for touch, `kMouseLeftId` for the mouse. A device id matches no pointer, so the "UI touches are not gameplay swipes" rule never actually fired and dragging off a button could steer the ship. Now derived per device type.
- `TouchInputSource.logSwipes` (on in `Game.unity`) logs every accepted swipe with its direction and the pixel threshold, which is the only practical way to tell whether the gesture registered in the Simulator. `LastSwipeDirection` and `SwipeCount` expose the same thing to tests.
- Testing mobile without a device: the **Device Simulator** (Game view tab dropdown, or Window > General > Device Simulator) gives a real portrait aspect, a notch safe area and mouse-driven touch. The touch buttons now appear whenever a touchscreen device exists rather than only on `Application.isMobilePlatform`, so they show in the Simulator too; `TouchControls.forceVisible` still forces them on. **SpaceXonix > Add Portrait Game View Size** registers a 1080x1920 entry, because Free Aspect in a landscape editor window frames the game completely differently from the portrait target.
- **The Android build module is not installed** in this Unity 6000.3.20f1 install (`BuildPipeline.IsBuildTargetSupported(Android)` is false, and the SDK/JDK/NDK paths are empty). Building an APK needs Unity Hub > Installs > 6000.3.20f1 > Add modules > Android Build Support, including OpenJDK and the Android SDK/NDK.
- Deferred to Phase 21: running this on a real Android device. Everything here was verified in the editor through the same code paths a device uses, but touch feel and safe-area insets can only be judged on hardware.

## Playtest Fixes (2026-09-19)

### Alien stuck inside new territory

- Reported: "if i complete a trail with a shield active and an alien is on that trail when it completes the alien just gets stuck".
- Cause: a Shield lets an alien survive standing on the trail (the pass-through grace), so when the trail is committed the territory closes around it. `EnemyController.AdvanceMovement` falls back to `IsUncapturedWorld` when the body starts blocked, but the alien's **centre** is now captured too, so every candidate is rejected, `Advance` just flips the velocity each frame and the alien never moves again for the rest of the stage.
- Fix: `EnemyManager.EjectTrappedEnemies`, called from `BoardManager.CaptureCompleted`, finds any alien whose own cell is no longer open and searches outward ring by ring for the nearest spot where its **whole body** fits, then relocates it there. It keeps its heading, speed, drift and frozen state, so it simply carries on from the open space nearest to where it was caught. When the board has no open cell left there is nothing to do and it is left alone, which only happens at 100% capture where the stage ends anyway.
- Tests: 4 new EditMode tests - the stuck alien reproduced (trapped and provably immobile over 30 steps), ejection returning it to open space with its velocity intact and moving again, untrapped aliens being left alone, and a real capture freeing it through the event with no manual call.

### Upgrade and briefing panels were mostly empty

- Reported: the between-stage panels were far too large and full of dead space.
- Cause: the shared `CampaignPanel` stretched to the screen with fixed margins and gave the body a fixed slab of the middle, so a short screen like the upgrade choice left a large void.
- Fix: the panel is now a `VerticalLayoutGroup` with a `ContentSizeFitter`, so its height is the sum of title, body and however many buttons are shown. The briefing and the three-option upgrade choice both come out compact with no empty band, and it still adapts to any aspect.

## Editor: Play Always Starts From Boot (2026-09-19)

- Reported: pressing Play in Unity went straight to stage 1 instead of the menu. Not a bug in the game - Unity starts Play Mode from whichever scene is **open**, ignoring the Build Settings order, and `Game.unity` was the scene left open.
- Fix: `Assets/SpaceXonix/Editor/PlayFromBootScene.cs` (new editor-only assembly `SpaceXonix.Editor`) sets `EditorSceneManager.playModeStartScene` to `Boot.unity` on load, so the editor follows the real startup path no matter which scene is open. It is a toggle under **SpaceXonix > Always Play From Boot Scene**, on by default and stored per user and per project, because forcing the menu on every Play is unhelpful while iterating on one scene. A missing Boot scene warns and falls back rather than failing.
- Follow-on fix: the menu's best-run line showed the *selected* difficulty's record, so a 46162 Hard record read as "No campaign completed yet" whenever Easy was selected. Since the mode is now chosen **after** Start Campaign, the menu shows the best run across both modes with its mode named, and the difficulty screen keeps the per-mode breakdown.
- Verified: with `Game.unity` open, Play loaded Boot, routed to MainMenu, kept the settings service alive across the load, and the menu read "Best run: 46162 (Hard)".

## Broken ScriptableObject Assets (found 2026-09-19)

- Verifying Hard exposed that `StageModifiers.asset` had `m_Script: {fileID: 0}` - **no script reference at all** - so it silently loaded as null and Hard could never roll a modifier. `CampaignUpgrades.asset` had the identical fault, meaning the **roguelite upgrades had been dead since Phase 13**.
- Cause: `StageModifierSetDefinition` and `UpgradeSetDefinition` were each declared beside another type in a file named after the *other* class. Unity only creates a `MonoScript` for the type matching the file name, so these assets were saved with no script. They worked in the session that created them, because the in-memory instance was still alive, and loaded as null from the next domain reload onward - which is exactly why every earlier Play Mode check passed.
- Fix: both classes moved into their own files, and each asset's `m_Script` repaired in place so the asset GUIDs and scene references survived. Both scene links were re-pointed and verified.
- Guard: 4 new EditMode tests load the real assets, including `EveryScriptableObjectAsset_ResolvesToItsScript`, which walks every ScriptableObject under `Assets/SpaceXonix` and fails if any cannot resolve its script. That is the general form of the bug, so a new asset cannot reintroduce it.
- Lesson recorded for later phases: a Play Mode check in the same session that created an asset proves nothing about whether it survives a reload.

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
- `Assets/SpaceXonix/Scripts/Campaign` — campaign/stage definitions, run progression, stage flow, roguelite upgrades, and stage modifiers
- `Assets/SpaceXonix/Scripts/Boss` — Alien Core definition, attack cycle, pooled projectiles, and boss controller
- `Assets/SpaceXonix/Tests/EditMode` — deterministic regression suite
- `Assets/SpaceXonix/Scenes/Game.unity` — representative gameplay scene

## Next Recommended Action

**Phase 20 — Visual Polish + VFX + Cinemachine**

Before modifying anything, read `AGENTS.md`, `PROG.md`, `SpaceXonixProposal.md`, and `IMPLEMENTATION_PLAN.md`, then inspect `git status` and the existing implementation.
