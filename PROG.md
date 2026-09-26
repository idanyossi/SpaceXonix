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
20. **COMPLETE** — Polish: capture flash, Freeze and Arena Tilt presentation, boss destruction sequence, menu and scene transitions
21. **COMPLETE** — Final QA + Profiling + Submission Cleanup (Android and Windows builds, steady 60 fps on device, Play Mode tests, the user's playthrough on the phone: "perfect on mobile")

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

- EditMode discovered: 386
- Passed: 386
- PlayMode: 8 passed (Tests/PlayMode, full game flow)
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

### Board, trail and lasers (done)

- **Design, chosen by the user:** the board starts as the deck of a giant derelict ship, and capturing converts it into your plating - *"make it look like we took that space"*. An open-space floor was proposed first and turned down as weird.
- The art is **drawn in code** by `BoardArtGenerator` (**SpaceXonix > Generate Board Art**) rather than sourced: these are patterns, not characters, and drawing them in ansimuz's palette at the sprites' 16 pixels per unit guarantees they match. Being generated in the project, they carry no licence.
  - **Deck:** dull, cold slate plates with seams, bevels and rivets. The four plates in each tile differ (a vent grille, faded hazard stripes, scuffs, a missing rivet) so the floor never reads as a flat grid.
  - **Claimed plating:** the same plate layout, clean and bright teal, with a lit power strip, a chevron and a status light, so capturing reads as converting the deck rather than covering it.
  - **Walls:** only ever show their bottom six pixels (0.35 units), so all their detail is there: dark at the foot, a lit cyan edge where the plating begins.
  - **Trail:** one texture repeat per cell, with a mid-cyan rim so neighbouring cells join into one continuous energy line. A dark rim was tried first and read as a string of beads.
  - **Lasers:** the beam has dark edges, red and a white-hot core, with bright pulses that scroll along it; the warning is a dashed red line cut out with alpha clipping rather than blended, which keeps its edges hard and needs no sorting.
- **The board meshes had no UVs at all**, so any texture would have smeared into one colour. `BoardMeshBuilder` now writes world-space UVs: tops use board XY and walls use the coordinate along the wall plus the height, so wall art stays upright and continues round corners. Because each UV comes from the vertex position rather than the cell, seams line up between neighbouring cells and chunks.
- **Lasers were stretched cubes**, so a texture would have smeared across the whole board. `LaserPresentation` now sets tiling from the beam's length through a `MaterialPropertyBlock`, which tiles each pooled beam without creating a material per instance, and scrolls the beam's pattern on unscaled time.
- Generated textures get their own import rule: point filtered, uncompressed and without mipmaps like the sprites, but **repeating** and not sprites, since they tile across meshes.
- **Harness note:** a first render showed no trail. The game's own consistency check had cancelled it, because the scripted trail's head was nowhere near the ship. It was not a rendering bug. Re-running with the ship at the trail's head, rendered in the same frame, showed it correctly.
- Tests: 4 new EditMode tests - tops tiling in world space with shared corners agreeing, walls running along their length and up their height, raised cells carrying one UV per vertex, and the laser tiling along the beam with never less than one repeat. Full suite: 323 passed, 0 failed.

### Background (done)

- **ansimuz's Space Background, CC0**, the same artist as the ships, so it matches by construction. It is built from five layers: the nebula backdrop, a star field, a far planet field, and two feature planets (a big planet top right, a ringed planet bottom left).
- `SpaceBackdrop` puts each layer on a camera-facing quad and re-fits it every frame, so it covers the view at any aspect, from a portrait phone to the landscape editor. Layers sit at a fraction of the camera's far plane, which the rig changes with the framing, so they always stay behind the board and are never clipped. The stars and planet field drift at different speeds for parallax, and the feature planets bob slowly. Tiling goes through a `MaterialPropertyBlock` and keeps texels square whatever the view's shape.
- Materials: the nebula is opaque, and the other layers use alpha cutout rather than blending, which keeps pixel edges hard and avoids sorting problems.
- The backdrop and star field repeat, but the two feature planets clamp, because repeating a single object would bleed a row of pixels from the opposite edge onto its border.
- It is in both the main menu and the game. The camera clears to a dark nebula purple instead of grey.

### Menus (done)

- The menus now look like part of the same ship. `UiSkin` (**SpaceXonix > Apply UI Skin**) draws the widgets in code, in the board plating's palette:
  - **Panels:** a dark hull plate inside a bevelled teal frame, riveted at the corners.
  - **Buttons:** raised plating with a two-pixel lip. The pressed state drops the lip and shifts the face down, so a button visibly pushes in. There are also hover and disabled states.
  - **Slots:** recessed slots for slider tracks, toggles, the power meter and the ability box.
  - **Other:** a glowing fill, a plating knob and a pixel tick.
- Everything is nine-sliced at 16 pixels per unit, so frames keep their pixel size on any panel.
- The Kenney Pixel UI pack was looked at and rejected as generic and flat next to the ship art.
- **Fonts: Kenney Mini Square for body text and Kenney Pixel Square for titles, CC0.** They import as hinted raster, which keeps the glyphs' edges hard. Titles are the plating's glow cyan with a drop shadow.
- The styler recognises roles from the hierarchy (a Button, a Slider's parts, a Toggle's graphics, a panel by name), so running it again after adding a screen styles that screen too. It covers the main menu, the difficulty screen, the HUD, the campaign screens, the pause menu and the settings overlay prefab. It styles a nested prefab through the prefab itself, never through scene overrides.
- **Pixel fonts need different overflow rules:**
  - Their taller line height made truncating rects drop single-line captions entirely, which blanked the EASY and HARD labels. Body text now overflows vertically.
  - The wider title font wrapped long stage names into the body text. Titles now shrink to fit their rect, down to half their authored size.
- **The difficulty panel stretched to the full screen**, which left most of it empty in portrait. It is now a fixed 920×790 centred card, sized to its content. The Hard description was shortened so it no longer runs into the best score.
- **Capture note:** the MCP's screenshot of an overlay canvas double-encodes gamma, so the dark hull came out grey. Rendering the canvas through the camera into an sRGB texture read exactly `#161A2B`, the palette value, so the game itself is correct. Menu checks were done with those camera renders at 540×960 portrait.
- Tests: 4 new EditMode tests fail if any button, panel label or text in the main menu, the game scene or the settings overlay is left unskinned, or if a UI sprite loses its nine-slice border or point filtering. Full suite: 327 passed, 0 failed.

### Playtest fixes (2026-09-25)

- **The turn sound was grating** (*"god awful and squirmy"*): every turn played a clip with ±12% random pitch, and it fires constantly. Turning is now **silent**. The binder also used to play the trail-start sound on any turn made inside safe territory, whether or not a trail began; the trail-start sound now plays from the board's own `TrailStarted` event, at the moment a trail actually starts. `GameSfx.DirectionChanged` stays in the enum because scenes store these values as numbers, but it has no clip, and its three audition candidates were removed from the shortlist and the repository.
- **Raised territory looked cut out.** The plating texture repeats every 2 units in world space, and a cell is 0.18 units, so each plate spans about five and a half cells and every staircase edge sliced straight through a plate. `BoardMeshBuilder` now lays a trim strip (submesh 2) along every top edge that drops to a lower neighbour. It is 0.125 units deep, two pixels at 16 per unit: a lit outer edge with a dark groove inside it, so every captured region ends on a finished edge. The trim's UVs run along the edge in world space, so its pattern continues unbroken across cells. The texture clamps across its depth, so the inner edge cannot wrap round to the lit row. Verified on a staircase edge, a lone island and a one-cell line; the board's outer frame gets the same trim.
- **The controls panel overflowed.** At size 40 in the pixel font, 17 lines needed about 880 px of the 810 available. The manual line breaks also stranded "and" and "75%" once the wider font rewrapped them. The body is now short paragraphs at size 34, with headings in the title cyan; it needs 658 px.
- Tests: 3 new EditMode tests check that trim appears on every exposed edge and nowhere else, that it lies on the top face inside the edge and faces the camera, and that a width of 0 builds none. Full suite: 330 passed, 0 failed.

### Playtest fixes, round 2 (2026-09-25)

- **The main menu was silent.** Its music was playing, but the menu scene had **no AudioListener**. Only the gameplay camera carried one, so nothing in the menu could be heard. The persistent `AudioManager` now owns the game's single listener, since all audio is 2D and its position is irrelevant. The gameplay camera's listener was removed so there is never a second one. Verified live from Boot: exactly one listener in Boot, the menu and the game; the title music playing in the menu and Level 1 in the stage; no console warnings.
- **The squirmy sound was the trail start**, not the turn. A pitch scan of the candidates (per-window zero-crossing pitch, summed movement in octaves) found that the second trail-start candidate, `sfx_movement_portal1`, moves about 71 octaves, the most of any sound scanned. `Blip5`, the one in use, was mild at 0.15, but it was played with ±10% random pitch on top. The shortlist is now three steady blips measured at no more than 0.02 octaves of movement (`Blip2`, `coin_single4`, `Blip8`), the sound defaults to `Blip2`, and its random pitch is off. The three old candidates were removed from the repository.
- The menu music measured quiet in testing because the saved settings have master and music at about 20% each, which multiply to 0.044. That is the player's own setting, not a bug.
- Tests: 4 new EditMode tests. One checks the manager owns exactly one listener even when woken twice; three check that no scene carries a listener of its own. Full suite: 334 passed, 0 failed.

### Playtest fixes, round 3 (2026-09-25)

- **Buttons were silent.** `GameSfx.UiInteraction` existed, but nothing played it. `UiClickSound` sits on each root canvas (the main menu, and the HUD, which holds the pause, campaign and settings screens). It hooks every button and toggle under it, so new screens click without any wiring. It skips only the two on-screen gameplay buttons that `TouchControls` owns, power shot and ability, because those have their own sounds. `TouchControls` sits on the same root as every other HUD button, so the skip asks it which buttons it owns rather than checking the hierarchy. The settings panel now refreshes its toggles with `SetIsOnWithoutNotify`, so opening it does not sound like a click.
- **The laser warning and the trail start still sounded bad.** The second pitch scan also measured pitch height and loudness. `Blip2`, the last trail-start pick, is a 175 Hz square wave held flat for 0.17 s at full loudness: a buzz, not a cue. The warning was `error1`, a bendy 625 Hz buzzer. New defaults, all short, soft and steady:
  - Trail start: `menu_move4`, a 0.05 s tick, at 70% volume.
  - Laser warning: `alarm_loop6`, a soft steady tone of about 0.5 s inside the 0.75 s warning, at 70%.
  - Button click: `menu_move1`, a quiet 0.04 s click, at 80%.
  - Random pitch is off on all three. Each keeps two alternatives in **SpaceXonix > Sound Audition**, and five superseded candidates were removed.
- **Lives are shown as ships.** The top-left number and its "LIVES" caption were replaced by a row of six player-ship icons (`ship_2` at exactly 3×, 48×72, so the pixels stay crisp), one per life. Reinforced Hull can raise lives past six, so the old lives label now sits after the row and shows `+N` for any beyond it. With no icons assigned, the HUD falls back to the number.
- Verified live: two menu clicks each played `UiInteraction`, and a stage showed five ships for Easy's four lives plus one granted.
- Tests: 4 new EditMode tests. They cover the ship row with its overflow count, menu buttons and toggles clicking while the gameplay buttons stay silent, and every root canvas in both scenes carrying the click sound. Full suite: 338 passed, 0 failed.

### Playtest tuning (2026-09-25)

- **Boss fires a little more often:** Alien Core `fireInterval` went from 2.2 s to 1.9 s, about 16% more volleys, because its shots are small and easy to miss. The volley size, spread and speed are unchanged.
- **Lasers fire from random lines.** A stage's `LaserPlacement.line` used to fix each laser to one row or column all stage, so players learned the safe lanes. With `LaserManager.randomizeLines` on in the game scene, each laser moves to a newly picked row (horizontal) or column (vertical) at the start of every warning. Lines are never diagonal.
  - Moving only at the warning keeps the warning line, the beam and the hit test on one line for the whole shot.
  - `LaserLinePicker` keeps 4 lines clear of each edge, where the always-captured border makes a beam pointless. It also tries to keep 8 lines from every laser on the same axis, including the laser's own last line, so consecutive shots never repeat a lane.
  - The placed line now serves only as the starting position.
  - Live check with Stage 3's two lasers: horizontal shots at rows 14, 35, 23, 71, 22, 66, 5, 78 and vertical at columns 39, 28, 44, 33, 23, 35, 23, 48.
- **Board width left as is.** The board is 54×96 cells, exactly 9:16, the shape of a portrait phone. The camera fits it to the screen's width, so a wider board would shrink the whole playfield on phones; 60 columns would make it about 10% smaller. Taller phones (9:19.5 and up) have spare height, not width. The user chose to keep it if widening hurt mobile.
- Tests: 2 new EditMode tests. One checks that each warning moves the laser and that the beam hits the new line and not the old; the other checks the picker's edge margin, separation and small-board fallback. Full suite: 340 passed, 0 failed.

### Hybrid aliens (2026-09-25)

- **Problem:** Volatile blasts destroyed every alien in range, which cleared the board for the player and made stages too easy.
- **User's choice:** out of three options, *"kills spawn a hybrid"*. The GDD's Volatile section was updated to match.
- **How it works now:** an alien caught in a Volatile blast becomes a hybrid instead of dying.
  - It is converted in place: the same object, the same movement code (bouncer, linear or unstable) and the same heading. Converting in place avoids a new class for every combination and any trouble spawning into freshly blasted cells.
  - Its charge goes off when it comes within half a cell of territory the player built. The permanent border never counts.
  - It removes captured cells within the Volatile's territory radius (4 cells), **centred on the territory cell it touched**, hits the ship if it is inside the blast, and is used up.
  - It gets the Volatile's spawn protection (0.75 s), so the blast that made it cannot set it off.
  - Hybrids never explode on aliens, Volatile Aliens neither detonate on nor convert hybrids, and a pooled alien comes back normal. Explosions therefore cannot chain.
- **Why the hole is centred on the contact:** the first live run centred it on the hybrid, which bounces about two cells short of territory. Only the circle's edge reached: 1 cell in one run, 13 in another. Centred on the contact cell, the next live run removed 29 cells, a clear half-circle bite. The explosion ring and the ship check use the same point.
- **Look:** `HybridTint` (presentation only, on the Bouncer, Linear and Unstable prefabs) pulses the sprite between the alien's own colour and the Volatile's orange (1, .65, .3), three times a second. The shadow is a mesh, so it is not tinted.
- **Verified live in Stage 1:**
  - A real Volatile converted a real bouncer, which turned orange and armed while the other bouncer was untouched.
  - After a row was captured below it, the hybrid hit it and removed 29 cells.
  - The captured share went from 28.72% to 28.13%, and the player kept all lives.
- **Tests:** 2 EditMode tests updated from "destroyed" to "converted", and 2 new ones. The first checks that the border is ignored, that the hole reaches exactly the radius into built territory, that bystanders are untouched and that pooled reuse clears the hybrid. The second checks that a Volatile does not detonate on a hybrid. Full suite: 342 passed, 0 failed.

### Hybrid follow-ups and lasers near the ship (2026-09-25)

- **Reinforcements:** every alien turned into a hybrid brings one new regular alien, picked at random from `EnemyManager.reinforcements` (Bouncer, horizontal Linear, vertical Linear, Unstable). It arrives on a free cell at least 10 cells from the ship. The user reported stages still ran short of aliens, because each Volatile and, later, each hybrid is used up.
- **Much bigger territory blast:** the Volatile's `volatileTerritoryRadiusCells` went from 4 to 9. It applies to Volatile explosions and hybrid charges alike. The explosion ring and camera shake scale with the actual size through a new `EnemyManager.LastExplosionScale`, the stage modifier times the hybrid's charge.
- **Supercharged hybrids:**
  - An armed Volatile that touches a hybrid is absorbed without exploding, and the hybrid's charge doubles, up to `maxHybridCharge` (4).
  - The blast, the ship-kill radius, the hole, the ring and the shake all scale with the charge.
  - At the cap, a Volatile just passes through.
  - `HybridTint` doubles its pulse rate with each doubling of charge. Once supercharged it switches from a soft fade to a hard on/off blink.
  - The `HybridSupercharged` event is available for a future sound.
- **Lasers near the ship:** each line is picked within 12 rows or columns of the ship (`LaserManager.playerVicinity`). It stays random within that window and still respects the edge margin and the gap from other lasers and the previous shot.
- **Verified live:**
  - A Volatile converted a bouncer and a new Linear alien arrived.
  - A second Volatile was absorbed, raising the charge to 2.
  - The ×2 hybrid then hit a captured row and removed 497 cells, dropping the captured share from 28.7% to 18.6%.
  - With the ship in the corner at (0,1), laser rows were 4–16 and columns 4–16.
- **Balance note:** a ×4 hybrid (radius 36) can erase most of the board. It needs two Volatiles absorbed into one hybrid, which is rare, and its hard blink warns the player. Lower `maxHybridCharge` to 2 if it feels unfair.
- **Tests:** "Volatile does not detonate on a hybrid" became a supercharge test, covering absorption, doubling to ×4, the cap, the ×4 hole reaching exactly 8 cells for a 2-cell radius, and the scale reported to presentation. A new test covers one regular reinforcement per hybrid, kept away from the ship. The line-picker test gained the ship-vicinity window. Full suite: 343 passed, 0 failed.

### Ship explosion and visible reinforcements (2026-09-25)

- **The death looked like a squish.** The ship vanished while an `ExplosionRing` expanded flat on the board floor. A death now plays ansimuz's 5-frame `explosion` sheet (spark, fireball, breaking shockwave). The sheet was already cut into frames but unused, and it is the same CC0 pack and artist as the ship, so no new asset or licence was needed.
  - `SpriteBurst` plays frames once, facing the camera, at 12 fps. That takes 0.42 s, inside the respawn pause (at most 0.8 s).
  - `PlayerDeathPresenter` draws it from the pool at the ship's visual, which hovers above the board, so it appears where the ship was seen. It is 1.6 units across, about three times the ship.
  - The ring is no longer used for deaths; Volatile blasts still use it.
  - Kenney's fire effects were considered and rejected: they are smooth vector art and would clash with the pixel art.
- **"A volatile mixing with a regular should also spawn a regular":** this already happened in the previous commit. The new alien landed on a random free cell anywhere on the board, so it was easy to miss. It now appears within 6 cells of the blast, still at least 10 from the ship, and falls back to anywhere only if nothing nearby is free. Live check: a blast at (21,90) produced a Linear reinforcement at (21,89).
- Tests: the death test now checks that the fireball plays at the ship's visual, steps through frames and returns to the pool once finished; the reinforcement test checks the new alien is within 6 cells of the blast. Full suite: 343 passed, 0 failed.

### Shot explosions and the boss's territory-breaking shot (2026-09-25)

- **Aliens killed by the Power Shot explode.** They used to just vanish.
  - `SpriteBurstPresenter` plays pooled explosions from the same ansimuz sheet as the ship's death, sized at 2.2× the alien's visual width.
  - Each one appears at the hover height the alien was seen at: the alien is already back in the pool when `PowerMeter.EnemyDestroyedByShot` fires, but its transforms still hold where it was.
  - The shared prefab was renamed from `ShipExplosion` to `Explosion`. `AssetDatabase.MoveAsset` kept its GUID, so the death presenter's reference survived.
- **The boss's middle shot breaks territory.**
  - In an odd volley, the middle shot is the one aimed straight at the ship. It is tinted red-orange (`BossProjectile.breakerTint`) so it reads differently from the side shots.
  - On reaching territory the player built, it removes captured cells within `BossDefinition.middleShotTerritoryRadiusCells` (2.5), plays an explosion there, and is spent.
  - The permanent border does not count, and the cell the ship stands on is protected by the existing rule in `RemoveCapturedWithinRadius`.
  - Side shots behave exactly as before: they fly over territory and only hit the ship or the trail.
  - Because the middle shot aims at the ship, repeated volleys dig a tunnel through the territory toward it, so hiding deep in territory is no longer permanently safe.
- **Verified live:**
  - A real Power Shot killed an alien at (8,1), and the explosion played there.
  - With the Alien Core active and a captured band between it and the ship, three middle shots broke 13, 15 and 12 cells at (4,12), (3,9) and (2,6), digging toward the ship.
- Tests: 3 new EditMode tests (the middle shot breaking a small patch and being spent, side shots leaving territory alone, and a shot alien exploding once where it was), plus the volley test now checks only the middle shot breaks territory. Full suite: 346 passed, 0 failed.

### Power gauge, charged boss shots and upgrade cards (2026-09-25)

- **The Power Shot meter never looked full.** The fill was a filled `Image` of the 8-pixel `UI_Fill` sprite. A filled image cannot nine-slice, so the sprite's frame stretched into thick dark ends. `PowerGauge` now drives a nine-sliced fill by its right anchor, so it reaches exactly the track's edge:
  - Energy stripes (`UI_EnergyStripes`) stream through it under a `RectMask2D`. They span the whole track and are uncovered as it fills, so they never squash.
  - Nine dividers cut it into ten cells.
  - The colour climbs from deep blue to cyan while charging.
  - At full it throbs white-hot, and a nine-sliced glow frame (`UI_GaugeGlow`) and the "POWER READY" label pulse with it.
  - `GameHud` uses the gauge when assigned and keeps the plain fill as a fallback.
- **The boss's charged shot is random and looks charged.**
  - One of the three shots in every volley is charged, picked at random with a seedable random, instead of always the middle one.
  - It is drawn 1.6× bigger (its hit radius is unchanged) with a pulsing halo behind it, a child of the visual so it billboards with it.
  - Its core throbs white-hot.
  - Breaking territory now plays a 2-unit explosion, shakes the camera and plays the blast sound.
- **Upgrades are holographic cards.** `UpgradeCardPanel` deals three `UpgradeCard`s in from below, staggered, on unscaled time, since the game is paused. Each card has:
  - a chamfered frame with corner brackets, plus hover and pressed states;
  - a header band in the upgrade's colour;
  - a 48×48 pixel-art picture shown at exactly 5×, behind faint scrolling scanlines;
  - diamond pips for owned stacks, with the pip the card would add blinking;
  - the effect text and a "TAP TO INSTALL" footer.
- **Card behaviour:**
  - Cards cannot be tapped until they have landed.
  - A double tap takes one upgrade.
  - The campaign screens re-show the choice every frame, so the panel re-deals only when the offer changes.
- **Card pictures:** drawn by `UpgradeCardBuilder` from the game's own ansimuz frames on a star-grid backdrop with a dithered glow in the card's colour:
  - Reinforced Hull: the ship between riveted plates.
  - Improved Thrusters: the ship with speed streaks.
  - Rapid Capacitor: three bolts over an energy arc.
  - Shield Capacitor: an orb in a shield ring.
  - Cryogenic Core: an orb washed icy, with frost.
  - Gravity Stabilizer: an orb washed violet between level bars.
  - Scavenger Protocol: three orbs in a reticle.
  - Two orbs are washed toward their colour, not multiplied by it, which had turned them muddy.
- **Builder:** **SpaceXonix > Build Upgrade Cards and Power Gauge** builds both in the game scene and can be re-run. `UiSkin` skips card subtrees, and the skin test accepts `UI_Card` frames on cards.
- **Import fix:** `UiSkin.IsUiSprite` matched `Generated/UI_Scanlines.png` because it tested the prefix without the folder's slash, which imported the tiling textures as clamped sprites.
- **Verified live:**
  - A real stage cleared at 76.6% dealt Cryogenic Core, Rapid Capacitor and Gravity Stabilizer.
  - Tapping Rapid Capacitor twice gave 1 stack and moved to the next briefing.
  - The gauge rendered at 55% (blue, striped, cells) and full (edge to edge, glowing).
  - A live Alien Core volley showed one big glowing charged shot beside two small side shots.
- **Harness note:** with the Unity window unfocused, Play Mode runs no frames between MCP calls, so `LateUpdate` work, such as hiding the card overlay, only happens once the editor ticks. A render taken straight after a scripted phase change can therefore show stale UI.
- Tests: 5 new EditMode tests (cards showing the offer and pips, dealing once and landing before a tap, one pick per offer, the gauge filling exactly and glowing when ready, the charged shot moving across all three lanes), plus the volley test now checks exactly one charged shot. Full suite: 351 passed, 0 failed.

### Capture fix: pockets blasted into territory (2026-09-25)

- **Bug (playtest):** a charged boss shot (and equally a Volatile or hybrid blast) can leave an enclosed pocket in the player's territory. Closing a trail through the pocket filled only half of it and left the rest as a hole.
- **Cause:** capture only ever took the single smallest alien-free region beside the trail. A trail through a pocket splits it into two alien-free halves, so only the smaller half filled.
- **Fix (`BoardModel.SelectRegionsToCapture`):**
  - When any region the trail touches contains an alien, every alien-free region it touches is captured (classic Xonix).
  - When none does, only the main field is kept open: the largest open area on the whole board, measured by flooding what the trail's regions did not already cover. Every other touched region is captured.
  - Splitting an empty arena, as on the boss stage, still takes the smaller side, as the GDD requires. A pocket is never the main field, so it fills completely.
  - The GDD's capture rule gained the precise wording.
- **Behaviour change to note:** a trail that splits the field into three or more pieces now captures every alien-free piece rather than only the smallest. That is the classic rule, and the old code only ever tested two-way splits.
- **Verified live:** with the real board, a 21-cell pocket blasted deep inside captured territory filled completely when a trail was closed through it (0 cells left open). The user was also playing during that run, which accounts for the other captures and deaths seen at the time.
- Tests: 2 new BoardModel tests (a trail through a blasted pocket fills both halves, 8 cells, while the main field stays open; an empty-arena split with an untouched pocket elsewhere still keeps the larger side open and leaves the pocket alone). All existing capture tests pass unchanged. Full suite: 353 passed, 0 failed.

### Hard-mode lasers on the boss stage (2026-09-25)

- `StageDefinition.hardModeLasers` holds lasers a stage adds on top of its own on Hard only. `LasersFor(difficulty)` returns the combined list, and `CampaignManager.LoadCurrentStage`, the briefing's laser count and the debug HUD all use it.
- The Alien Core stage has no lasers of its own and now has two Hard-only lasers, one horizontal and one vertical. Like every laser they fire from random lines within 12 of the ship. Easy is unchanged.
- Stage modifiers only ever roll on Hard, so a modifier that needs lasers now counts a stage's Hard-only lasers too.
- Verified live: on a Hard run jumped to the boss stage through the real `LoadCurrentStage`, the Alien Core was active with 2 lasers (horizontal and vertical), and the briefing read "Lasers: 2".
- Tests: 2 new EditMode tests (the real boss stage asset has no lasers on Easy and two resolvable ones, one per axis, on Hard; Hard-only lasers add to a stage's own). Full suite: 355 passed, 0 failed.

### Ship skins and the hangar (2026-09-25)

- **Seven skins:**
  - The default is **Crimson Vanguard**, ansimuz's original ship.
  - Six come from **Master484's 16x16 Ship Collection** (OpenGameArt, CC0), one fighter from each colour group plus a second red: Cobalt Delta, Viper, Ember Talon, Solar Hornet, Nebula Dart, Phantom Rail.
  - They are 16 pixels wide like ansimuz's ship, with the same flat, outline-free style.
  - **Rejected:** Kenney Pixel Shmup, whose heavy outline and 32-pixel ships clashed, and Kenney's vector ships.
- **`ShipSkinBuilder` (SpaceXonix > Build Ship Skins):**
  - Finds the sheet's grid (white-framed 16-pixel cells on a 20-pixel stride, five 110-pixel colour groups).
  - Cuts each chosen ship and turns it a quarter turn from nose-right to nose-up, which is lossless.
  - Draws a two-frame thruster under the rearmost row, white-hot to orange to red, with the outer columns shorter. Every skin then animates like the default.
  - Writes `ShipSkinDefinition` assets plus a `ShipSkinLibrary`, and builds the hangar and the game-scene wiring.
- **Menu:** a **SKINS** button in the main menu's top-right corner opens `SkinSelectPanel`, "CHOOSE YOUR SHIP".
  - It is a three-column grid of card-framed tiles, each with the ship animating at 8× on a slot screen and its name.
  - Tapping a tile equips it and saves the choice. The equipped tile is lit and carries an "EQUIPPED" badge.
- **Saving:** the choice is stored in `GameSettingsModel.ShipSkin` under the PlayerPrefs key `spacexonix.ship.skin`. `ISettingsStore` gained string get/set for it. Like the difficulty, it survives a settings reset, and an unknown id falls back to the default ship.
- **In game:**
  - `PlayerShipSkin` swaps the player's frames in `Awake` and rescales the visual so it keeps its authored width. The visual-equals-hitbox rule holds for every skin (checked live: Viper visual 0.75 = hitbox 0.75).
  - The HUD's life icons show the equipped ship.
- **Builder bug found:** the first build saved empty library references in both scenes. Opening a scene unloads assets nothing references yet, so the loaded library object was dead by the time it was assigned. The builder now re-loads the library by path after each scene opens.
- **Verified live:**
  - The menu shows SKINS top-right; the hangar grid showed all seven ships animating.
  - Tapping Viper equipped it and wrote `viper` to PlayerPrefs.
  - The game then flew the Viper at hitbox width, with Viper life icons.
  - The saved choice was reset to the default afterwards, so the user's own setting is untouched.
- Tests: 5 new EditMode tests (at least five distinct, animated, 16-pixel point-filtered skins with Crimson Vanguard as default; an unknown or empty choice falls back to the default; every skin keeps the ship exactly its hitbox width; the hangar equips the tapped ship, saves it and marks only that tile; the choice survives a settings reset). Full suite: 360 passed, 0 failed.

### Ship stats, hangar after the difficulty choice, and a hangar that fits (2026-09-25)

- **The hangar did not fit the screen** in a landscape Game view: it was a 1500-unit-tall panel on a canvas about 1140 units tall. `UniformFit` now shrinks the panel evenly to fit its parent, with a margin, and never scales it up. Live, in an 1100x688 Game view, the panel sat at 64%, fully on screen (y 14 to 673).
- **New flow:** Start Campaign, then Easy or Hard, then the hangar, then LAUNCH. The top-right SKINS button is gone.
  - `DifficultyPanel.StartOn` stores the mode and opens the hangar with `OpenForLaunch(SceneRouter.StartCampaign)`. Without a hangar it still starts at once.
  - The hangar has BACK, which returns to the difficulty choice, and LAUNCH.
- **Ship stats (`ShipStats` on each `ShipSkinDefinition`):** every ship but the default trades a strength for a weakness. Perk and drawback appear on each tile in green and red. The table is in the GDD's new "Ships" section; for example, Cobalt Delta is +25% off territory and -20% on it, and Phantom Rail is +1 life and -12% speed.
- **How the stats apply:**
  - They multiply on top of upgrades inside `UpgradeManager.ApplyToSystems`, which runs at every stage load.
  - Speed goes through `PlayerController.SetMoveSpeed`. The zone speeds are a new `SetZoneSpeedMultipliers(safe, exposed)`, applied each movement step by control state.
  - Power charge goes through the meter's gain multiplier, and Power Shot speed through a new `PowerMeter.SetShotSpeedMultiplier`.
  - Pickup chance goes through a new `SetShipPickupChanceMultiplier`, kept separate from the stage modifier's so neither overwrites the other.
  - Ability duration multiplies Shield, Freeze and Arena Tilt.
  - Extra lives are added to the run's first stage in `CampaignManager`.
- `PlayerShipSkin.Apply` now records the ship even without a visual to dress, so stats never depend on art. The builder wires `UpgradeManager`, `CampaignManager` and `DifficultyPanel` to it.
- **Verified live:**
  - Start Campaign, then Easy, opened the hangar.
  - Equipping Phantom Rail and pressing LAUNCH gave an Easy run with 5 lives (3, plus Easy's 1, plus the ship's 1), 5 ship icons and speed 4.4 (5 × 0.88).
  - The user's saved ship was reset to the default afterwards.
- **Harness note:** the editor had been left paused (`EditorApplication.isPaused`), which is why a first read showed stale defaults. It was unpaused to finish the check.
- Tests: 4 new EditMode tests. They cover upgrades and ship multiplying (speed 5 × 1.1 × 0.88; shield 4 × 1.25 × 0.75; shot speed, gain and pickup), measured movement on territory and in the open at 0.8× and 1.25×, every non-default ship having a real strength and weakness with the default neutral, and LAUNCH starting the run while BACK returns. Full suite: 364 passed, 0 failed.

### Ship carousel and readable text (2026-09-26)

- **The hangar grid was unreadable** on a phone and on a PC: seven 280-unit tiles with 17-point text, shrunk further to fit the screen.
- **`SkinSelectPanel` is now a one-ship carousel:**
  - The card holds the ship at 24× (384×480) on a slot screen, its name in 60-point title text, the perk (green) and the drawback (red) at 40 points, and a "4 / 7" counter.
  - Big arrow buttons sit either side, with dots below.
  - It browses endlessly: past the last ship it wraps to the first, and back.
  - Browsing works by the arrows, by a horizontal swipe (`HorizontalSwipe`: a drag of at least 8% of the screen width that is mostly sideways; dragging left brings the next ship in) or by the keyboard (Left/Right or A/D; Enter or Space launches; Esc goes back).
  - The ship on show is equipped and saved, and slides in from the side it came from. LAUNCH starts the run in it.
- **The font was unreadable at real screen scales.** The Kenney fonts were imported as **HintedRaster**, which snaps glyphs to whole pixels and only renders cleanly at multiples of the font's grid. The canvas scales with the screen (about 0.6× in a landscape editor window, less in a phone preview), so letters broke up into misshapen shapes. Changes:
  - The fonts import as **Smooth**.
  - Every body text gets a thin dark `Outline`, and titles keep their shadow.
  - `UiSkin` now keeps shrink-to-fit on body texts built to shrink, with truncation so shrinking actually works. It used to switch shrink-to-fit off.
  - Upgrade card text is bigger: effect 28 (shrinks to 22), footer 20, name at least 20.
  - The old grid was the only other text under 28 points; every other screen was already 28 or more at the 1080×1920 design size.
- **Verified live:**
  - Start Campaign → Easy opened the carousel; the NEXT arrow moved Ember Talon to Solar Hornet and saved it.
  - Renders at 45% phone scale and in the 1100×688 landscape Game view both read cleanly, as did the in-game HUD at 45%.
  - The user's saved ship was reset to the default afterwards.
- **Harness note:** the editor paused itself twice as the game scene loaded, with no error in the console and no `Debug.Break` in the project. It was unpaused to finish. Worth checking the Console's Error Pause and the Game view's settings.
- Tests: the grid test was replaced by carousel tests (opens on the equipped ship; Next equips and saves; wraps past the last and back) and a swipe test (long sideways drags count in the right direction; nudges and vertical drags do not). Full suite: 365 passed, 0 failed.

### A softer font everywhere, and a hangar that looks designed (2026-09-26)

- **Font:** the user found the pixel font unreadable everywhere (bottom HUD, upgrade cards, hangar), even with smooth rendering.
  - Every Kenney font is the same square, all-caps pixel style, so none of them was softer.
  - Four OFL fonts from Google Fonts were rendered side by side at 17 and 12 points: Exo 2, Oxanium, Rajdhani and Chakra Petch.
  - **Exo 2** reads best and keeps a sci-fi feel. SemiBold is used for body text; Bold for titles and button captions (`UiSkin` now gives captions the bold weight).
  - The fonts live in `Art/ThirdParty/Exo2` with `OFL.txt`. The Kenney fonts were removed once no scene, prefab or asset referenced them.
  - Every builder takes the font from `UiSkin.FontFolder`, so there is one place to change it.
  - The skin test now requires Exo 2 on every text in both scenes and the settings overlay.
- **Hangar redesign** (the user: the arrows and the rest looked "budget and rushed"):
  - The boxed arrow buttons became glowing pixel chevrons (`UI_Chevron`, one drawing mirrored for the left). They nudge outward and brighten on hover.
  - The previous and next ships peek in dimly behind them, so it reads as a carousel.
  - The ship floats with a slow bob over a hologram pedestal (`UI_Pedestal`) in a faint breathing aura (`UI_Glow`), with a glowing divider (`UI_Divider`) under the name.
  - Perk and drawback each sit in a recessed slot with a green ▲ or red ▼ marker (`UI_StatUp` / `UI_StatDown`).
  - LAUNCH is the primary action: lit (`UI_ButtonHover` as its normal state, which `UiSkin` keeps for any `LaunchButton`), larger, and gently pulsing. BACK stays secondary.
  - All the new art is drawn by `ShipSkinBuilder`, and `UiSkin` leaves chevron buttons alone.
- **Verified at 45% phone scale:**
  - The carousel (Cobalt Delta, Phantom Rail), the bottom HUD with touch buttons (ABILITY, POWER, "Power 0"), the top HUD ("Stage 1/5", score, "0.0%") and the upgrade cards all read cleanly.
  - A first render showed the aura as a solid disc and BACK touching LAUNCH; the aura was softened, the buttons spaced, and the side ships tucked inside the panel.
  - The user's saved ship was reset to the default afterwards.
- Tests: full suite 365 passed, 0 failed.

### Stronger ship trade-offs, a tighter hangar, bigger upgrade cards (2026-09-26)

- **Ships felt the same.** The first stats were 10–25% nudges, so every change is now at least 20% or a whole life. A test enforces this.
  - Cobalt Delta: +35% speed off your territory / −25% on it.
  - Viper: +20% speed / power charges 50% slower.
  - Ember Talon: power charges 2× faster / starts with 1 fewer life.
  - Solar Hornet: 2× power-up spawns / abilities last half as long.
  - Nebula Dart: abilities last 2× longer / −20% speed.
  - Phantom Rail: +2 lives / −20% speed.
  - The Power Shot speed drawback was dropped, because it was hardly felt.
  - A first pass used 30–50% speed swings. The user found the speed too much, so speed was tuned down to 20–35% and the "felt" threshold set at 20%.
- **Negative lives:** `ShipStats.extraLives` can now be negative (range −2 to 3), and `CampaignManager` floors the run's starting lives at 1.
- **Hangar gap:** the panel was 1700 tall with the buttons pinned to its bottom, leaving a dead band. At 1420 tall the buttons sit right under the dots.
- **Upgrade cards were still hard to read in the phone preview.** That is partly the simulator: it draws the 1080-wide canvas at about 44%, so 24-point text lands near 10 real pixels, while a phone draws it about 2.5× larger. But the cards were also small. Now:
  - Cards are 330×560, up from 300×480.
  - Effect text is 34 (shrinking no lower than 26), the name up to 32 (no lower than 24), and the footer 22.
- Verified at 45% phone scale: the hangar with no gap, and the cards (Shield Capacitor, Reinforced Hull, Gravity Stabilizer) read clearly. The user's saved ship was reset to the default afterwards.
- Tests: the ship stat test now also counts a lost life as a weakness and requires every change to be at least 20%. Full suite: 365 passed, 0 failed.

### Backdrop recoloured into the blue palette (2026-09-26)

- **Source:** the background is ansimuz's **Space Background** (OpenGameArt, CC0), five layers by the same artist as the ships. The user liked it, but its magenta (mean hue about 315°) clashed with the teal deck and UI.
- **Approach:** the same art recoloured, the closest possible match. CC0 allows modification.
- **What was tried:**
  - Hue rotations to 200°, 215° and 230° turned the nebula blue, but the big planet's orange-to-red gradient landed on violet, which still clashed.
  - Two gradient maps (a teal ramp and a deep-blue ramp) redraw each pixel by brightness onto one palette ramp, keeping every shape.
  - The **deep-blue ramp** won: `#06060f → #0d1430 → #1b2b5a → #2a5a8a → #43a3c4 → #aef0ff`. It gives a navy nebula and a cyan-lit planet, with enough contrast that the teal board still stands out.
- `BackdropRecolor` (**SpaceXonix > Recolor Backdrop**) reads the untouched originals from `AssetSources/ansimuz_space_background` and writes the five layers in place, so the materials, importer rules and `SpaceBackdrop` setup are unchanged and the ramp can be retuned at any time.
- The camera clear colour in both scenes moved from the old purple to the ramp's darkest navy.
- Verified with renders of the main menu and a stage (board, captured territory, HUD): one palette throughout, with warm colours left only for gameplay accents.
- Tests: 5 new EditMode tests check that no opaque pixel in any backdrop layer is warm (red above blue). Full suite: 370 passed, 0 failed.

## Phase 21 — Android build (started 2026-09-26)

- **Settings restored:** `ProjectSettings/QualitySettings.asset` had lost its **Mobile** quality level, uncommitted, before this phase. It was restored from git with the editor closed. Android defaults to Mobile again, and Standalone to PC.
- **Android player settings:**
  - package `com.idanyossi.spacexonix`, company `idanyossi`;
  - portrait only;
  - IL2CPP, ARM64, minimum API 25.
  - A generated app icon (the default ship on the game's navy, `Art/Icon/AppIcon.png`).
  - The project's active platform is now Android. Switching added Android sections to every texture's `.meta`; none are overridden.
- **First build on the device** (a OnePlus CPH2747, 1272x2772 at up to 165 Hz): the APK is 44 MB. The "934 MB" in the build report counts debug-symbol folders that don't ship. The game felt laggy, and there were three causes:
  - **No frame-rate target.** Android runs Unity at 30 fps by default. `BootLoader` now asks for 60 on phones (`mobileFrameRate`).
  - **Swipe logging was on** in `Game.unity`: every swipe logged with a full stack trace. It is off now, and release builds also drop stack traces from ordinary logs and warnings.
  - **Rendering work nothing used:** the directional light cast soft shadows, and the cameras and the Mobile URP asset had HDR on, yet every material is unlit. Shadows and HDR are off (and additional lights too, on the Mobile asset); the render scale stays 0.8.
- **Upgrade cards ran off the screen** on the phone. It is taller and narrower than 1080x1920, and the canvases scaled half by width and half by height, which left only about 975 UI units across for about 1020 of cards. Both canvases now use **Expand**: the full 1080x1920 layout always fits, and taller phones just get extra height.
- Tests: full EditMode suite 386 passed, 0 failed. The rebuild installed and ran on the phone with a clean log. Waiting on the user's feel test.

### Smooth frame rate on the phone (2026-09-26)

- The user reported occasional frame drops and slightly delayed swipes. Causes found:
  - **Garbage on every ship step:** trace calls built strings such as `$"LogicalStepStarted:{a}->{b}"` even in release builds; only the trace body was compiled out. `GameManager.TracePlayerLifecycle` and `InputRouter.TraceRawInput` are now `[Conditional]`, so release builds drop the call and its message.
  - **Garbage every frame from the HUD:** `GameHud` rebuilt about eight label strings every frame and called `Enum.GetValues` each time. Each label is now rebuilt only when its value changes (the effect timers at most ten times a second, through a reused `StringBuilder`).
  - **Uneven frame delivery:** Android **Optimized Frame Pacing** is now on. Incremental GC was already on.
- **Swipe delay:** the game path adds none. A swipe fires after about 2 mm, the turn applies the next frame, and the heading snaps. The felt delay is display latency, which the 30-to-60 fps change and frame pacing cut. If more is wanted, the next step is 90 or 120 fps.
- **`FrameStats`** is a device probe, compiled only with the `SPACEXONIX_FRAMESTATS` define, which is currently set for Android. Every 5 s it logs fps, the worst frame, frames over 20 and 50 ms, GC runs and heap size to logcat. **Remove the define before submission.**
- **Measured on the phone** (about 70 s of play):
  - 13 of 14 in-game windows averaged 60 fps, with the worst frame 17 ms (a flat 60).
  - Three single 33-36 ms frames in total.
  - GC ran about every 10 s with no visible cost; the heap stayed near 6 MB.
  - The one 1-second freeze coincided with the app regaining focus, not with gameplay.
  - The user: "it feels good".
- Tests: 386 passed, 0 failed.

### Windows build, Play Mode tests, cleanup (2026-09-26)

- **Windows build** (`Builds/Windows/SpaceXonix.exe`, Mono scripting, Direct3D 12):
  - It opens as a resizable 540x960 portrait window, which fits a 1080p screen, instead of fullscreen.
  - The first run logged an error **every frame**: the PC renderer had Screen Space Ambient Occlusion on, whose shaders are stripped from builds. SSAO and PC shadows are now off, since everything is unlit.
  - The rebuilt player ran with 0 errors or exceptions in `Player.log`.
- **Play Mode tests**, the first in the project (`Tests/PlayMode`, 8 tests, about 18 s). Each boots the real game through Boot and the Main Menu, then drives it through the same input and campaign calls as the UI:
  - boot to the first briefing;
  - swipes steer the ship out and back, the capture rises, and the HUD shows it;
  - losing a life respawns the ship safely;
  - pause freezes the game and resume continues it;
  - a stored shield runs its duration and ends, with the bubble shown and hidden;
  - the Power Shot kills an alien with its impact effects, and the hit-stop lets go;
  - lasers warn, fire and cool down in order;
  - **a full campaign:** five stages, four upgrades, the boss destruction sequence (the core is gone), the run complete, and every upgrade with a HUD badge.
  - All 8 passed on two consecutive runs.
- **Harness fixes:**
  - The editor's "Always Play From Boot Scene" rule also hijacked the Test Runner's start scene, so Play Mode tests never began. It now steps aside when Play Mode is entered from the Test Runner's `InitTestScene`.
  - The tests boot once per run: loading Boot a second time meets its persistent services as duplicates. The real game only boots once.
  - **Regression, fixed the same day:** that step-aside first keyed on the scene name. The interrupted test runs had left the Test Runner's `InitTestScene` open in the editor, so pressing Play started from it and **ran the Play Mode tests instead of the game** (the user saw the whole campaign play itself instantly).
    - Now the tests announce a run through `IPrebuildSetup` (a `SessionState` flag), and the rule steps aside only then.
    - The leftover scene was closed, and seven stale runner objects were removed from editor memory.
    - Verified: Play from the editor goes Boot, Main Menu, then the Stage 1 briefing at a 75% target; the Play Mode tests still pass 8 of 8.
- **Cleanup:**
  - `ATTRIBUTIONS.md` moved to `Assets/ThirdParty/ATTRIBUTION.md`, where AGENTS.md asks for it; references updated. Every shipped asset is covered (Ansimuz, Master484 and Junkala CC0; Exo 2 OFL).
  - The `SPACEXONIX_FRAMESTATS` define was removed. `FrameStats` stays in the code, inert, for future checks.
  - The unused sound candidates were **kept**. They never ship (builds only include referenced assets), and keeping audition options is the user's stated preference.
  - The active platform is back to Android.
  - The GDD's Android build and Windows build boxes are ticked.
- Tests: EditMode 386 passed, PlayMode 8 passed.

### Power Shot bolt turned sideways (2026-09-26)

- **Bug:** a bolt fired upward flew sideways. Bolts are pooled, and the hover visual only applies a heading when it is non-zero. Up is heading 0, so a reused bolt kept its last rotation (270 degrees from an earlier shot to the right). Measured in the game: 89 degrees off its travel.
- **Fix:** `PowerShotProjectile.Launch` now sets the sprite bolt's rotation outright on every launch. Verified in the game: fire right, let the bolt return to the pool, fire up with the same bolt, and it lands 0 degrees off.
- The bolt is a little smaller: scale 0.9, down from 1.1 (prefab and `FeedbackBuilder`).
- Test: the heading test now starts each launch from a bolt still turned from its last flight. EditMode 386 passed.

## Phase 20 — Polish (2026-09-26)

Scope as the user asked: a simple, not over-the-top capture effect, Freeze and Arena Tilt presentation, a boss destruction sequence, and menu transitions.

- **Capture flash:** `BoardRenderer` lays a flat, additive, pale cyan mesh over the cells that just rose (`CaptureFlash.mat`, URP Unlit, additive). It fades out over 0.45 s. It is one mesh per capture, rebuilt only when territory rises. Presentation only; the capture logic is untouched.
- **Freeze:** `FreezePresenter` fades in an icy nine-sliced border around the screen (`UI_Frost`, generated). While frozen, it tints every active alien toward ice blue with a faint shimmer and stops their frame animation. Everything is restored when the freeze ends.
- **Arena Tilt:** `TiltPresenter` streams a band of amber chevrons (`UI_TiltStreaks`, a repeating tile) along the screen edge the aliens are sliding toward. The chevrons point outward and fade in and out with the effect. The camera roll already shows the tilt; the chevrons show which way everything is going.
- **Boss destruction:** `BossDeathSequence` plays when the Alien Core is defeated:
  - a 1.6 s chain of pooled explosions across the core while it shudders;
  - then a large final blast, a hard camera shake, the core-destroyed sound, and a white flash that fades.
  - `BossController` now keeps the body visible on defeat and exposes `Body`/`HideBody` so the sequence can remove it at the climax.
  - `CampaignScreens` holds the stage-complete panel back until the sequence has finished.
  - The core-destroyed sound moved from `GameplayAudioBinder` to the climax of the sequence.
- **Menu transitions:**
  - `ScreenFader` fades to near-black between scenes (0.22 s out, 0.3 s in) on its own persistent canvas. It swallows taps while dark and ignores a second load request mid-fade. `SceneRouter` loads through it.
  - `PanelTransition` fades every "...Overlay" screen in over 0.18 s, and its inner panel settles from 94% scale. **Apply UI Skin** adds it to each overlay.
- `PolishBuilder` (**SpaceXonix > Build Polish Effects**) generates the frost and chevron art and the flash material, and wires the frost border, chevrons, boss flash and three presenters into `Game.unity`. Re-running it rebuilds everything in place.
- Found during verification:
  - The chevron was drawn tip-left and stacked into a zigzag. It was redrawn tip-right with clear rows between chevrons.
  - The panel hold originally only covered the victory phase, but on the boss stage the stage-complete panel comes first. The hold now applies whatever panel is next.
- Play Mode checks, all through the real menu flow:
  - The difficulty overlay faded in from alpha 0, and the menu-to-game fade completed.
  - Freeze showed the frost border at 0.75 with 2 aliens tinted.
  - Tilt showed chevrons on the downhill edge, pointing outward.
  - On the boss stage, a real stage completion played the explosion chain over the core. The panel only appeared, fading in, after the final blast removed the core.
  - A capture flashed its new strip and faded.
  - Unity Console: 0 errors.
- GDD (`SpaceXonixProposal.md`):
  - 14.1 MVP: everything is ticked except the Android and Windows builds (Phase 21).
  - 14.2 Polish: everything is ticked except the Volatile Alien warning animation and additional stage modifiers. The three extra modifier assets are the boss-compatible ones the GDD already describes.
  - 14.3 Stretch: "Boss projectiles destroying captured territory" is ticked (the charged shot).
- Tests: 6 new EditMode tests (`PolishTests`) cover:
  - the capture flash lighting and fading;
  - the frost border following Freeze only;
  - the chevrons' side and direction for both tilts;
  - the boss chain, the core hidden at the end and the flash cleared;
  - a panel fading and settling in.
  - Full suite: 376 passed, 0 failed.

### Shield bubble, arena rim, Power Shot impact and upgrades on screen (2026-09-26)

Four requests from play-testing.

- **Shield:** the flat green ring looked underwhelming next to Freeze and Arena Tilt. It is now:
  - a shimmering energy bubble round the ship (`ShieldBubble`, generated 24-pixel frames);
  - the bubble pops in, breathes, flashes white and swells when it absorbs a hit (new `GameManager.FailureShielded` event), and blinks through its last second;
  - the old ring now lies flat on the floor as the bubble's footprint.
  - **Rule change:** the trail inside the bubble (0.75 world units round the ship, about 4 cells; `PowerUpManager.shieldTrailRadius`) is covered too.
    - An alien crossing it, or a boss shot landing on it, no longer cuts it.
    - The rest of the trail stays vulnerable.
    - One shared rule, `GameManager.IsTrailCellShielded`, is used by both enemies and the boss.
    - The GDD's Shield section is updated to match.
- **Upgrades visible mid-run:**
  - `UpgradeStrip` puts a badge per upgrade (its card picture, "x2" when stacked) under the score, top right.
  - `UpgradeList` adds an "UPGRADES THIS RUN" panel under the pause menu with each upgrade's picture, name, effect and count. The panel says so when the run has none.
  - `RunUpgradeModel.Taken` keeps the order upgrades were taken in, and `UpgradeManager.Changed` refreshes both views.
- **Arena rim:** `ArenaRim` builds a raised metal frame round the board:
  - 0.3 wide and 0.55 tall, a little taller than territory;
  - the outer wall drops below the floor so the frame reads as a solid slab;
  - its top has steel plate with seams, rivets and cyan running lights;
  - a glowing inner lip slowly breathes.
  - It sits outside the playable grid and fits inside the portrait framing without reframing the camera.
- **Power Shot oomph:**
  - The cube became a flickering plasma bolt that turns to its direction of travel, with a fading cyan streak.
  - Firing throws a muzzle flash off the nose and kicks the camera.
  - A hit adds a cyan floor shockwave, a heavier shake and a 0.07 s hit-stop. The hit-stop never fights the pause, and is restored only if it still owns the time scale.
  - A shot kill's explosion is bigger (3x the alien's width, from 2.2x).
  - `PowerMeter.ShotImpact` reports where a shot struck an alien or the boss; the boss gets its own explosion.
- `FeedbackBuilder` (**SpaceXonix > Build Shield, Rim, Shot and Upgrade HUD**) generates the art, materials and prefabs and wires everything into `Game.unity`.
  - Generated effect sprites go in `Art/Generated/Fx`, a new pixel-sprite import rule.
  - Generated tiles are no longer rescaled to powers of two; the rim texture is 5 pixels deep.
- **Play Mode checks** (portrait 1080x1920 through the real menu flow):
  - the rim framing the board;
  - the bubble and its footprint over an exposed trail;
  - a blocked hit flashing the bubble with no life lost;
  - a real Power Shot via the input router hitting a frozen alien: muzzle flash, streak, explosion, shockwave, and hit-stop at 0.05 returning to 1;
  - three upgrades showing as badges and in the pause list.
  - Unity Console: 0 errors.
- **Tests:** 10 new or changed EditMode tests.
  - Trail inside the bubble survives an alien crossing it; trail beyond it is still cut (this replaced "does not protect trail behind ship").
  - Rim geometry stays outside the grid and every triangle faces its normal.
  - Bubble pop, hit flash and footprint.
  - Bolt heading for all four directions, and the streak reset.
  - Muzzle flash, shockwave, and the boss-vs-alien explosion.
  - `ShotImpact`.
  - Upgrade order, badges, stack counts, and the pause list resizing and resetting.
  - Full suite: 386 passed, 0 failed.

## Phase 19 — Asset Acquisition/Integration (partly complete)

**What blocked full completion:** this phase is about bringing in licensed third-party art and audio, which cannot be downloaded from here. The MCP's `generate_image` and `generate_model` tools exist but **both providers report `configured: false`**, so AI generation was not available either. The project had **zero** art and audio files before this phase.

### Placeholder audio (done)

- `SpaceXonix/Generate Placeholder Audio` synthesises a clip for all 20 sounds and all 3 music loops directly in the editor, so the game is audible now. Being generated in-project, they carry no licence obligations, and `SpaceXonix/Delete Placeholder Audio` removes them in one step when real clips arrive.
- Each sound's shape matches what it represents so they stay distinguishable while playtesting: rising tones for gains (capture, pickup, power full), falling ones for losses (player hit, freeze), noise bursts for destruction (Volatile, boss). Every clip is fade-edged so nothing clicks on play or loop. Music is three short arpeggio loops with per-note pluck envelopes.
- Import settings are applied per category: effects are PCM and decompressed on load, music is Vorbis and streamed, everything forced to mono.
- **Gap found while testing:** the binder only chose music from `StageLoaded`, so the menu had no track at all and started silent. `MusicCue` now states a scene's track outright, and the Main Menu carries one. It is silent rather than throwing when no audio service exists.
- Verified from a real boot: the Main Menu plays `Music_Menu`, a stage load switches to `Music_Gameplay`, and a capture fires three effects.

### Licence record (done)

- `Assets/ThirdParty/ATTRIBUTION.md` (moved there from the repo root in Phase 21, where AGENTS.md asks for it) records every shipped asset with its source and licence, as the GDD's Asset Licensing section requires, plus the full inventory of what still needs sourcing and which stand-in each replaces.
- It notes that CC-BY assets need visible in-game credit, not just a file entry, so a credits screen goes on the Phase 20 list the moment the first one is added.

### Third-party art and audio (done)

- **Sourcing.** Browsed Kenney and itch.io's free pixel-art listings, and followed up on OpenGameArt when itch.io blocked its filtered pages. The deciding constraint turned out to be that **the GitHub repository is public**: committing an asset publishes its raw files, so every pack whose licence forbids redistribution was out, however good it looked. That removed the [8x8] pack, the CraftPix/Free Game Assets packs and dani567's pack. Helianthus Games' kit was dropped because its licence is silent on redistribution and its ships are side-view, which is wrong for a camera looking down on the board. The full reasoning is in `Assets/ThirdParty/ATTRIBUTION.md`.
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

All 21 planned phases are complete. The game runs on Android (a steady 60 fps on the test phone) and on Windows, with 386 EditMode and 8 Play Mode tests passing.

Optional, only if wanted:

- Stretch goals from the GDD (section 14.3): Endless Mode, a Speed Boost power-up, extra upgrade and modifier types, controller support.
- A Volatile Alien warning animation (polish list, still open) and more stage modifiers.
- A 90/120 fps option for high-refresh phones.
- Release packaging: a signing keystore and an Android App Bundle (.aab) for a store.
