# SpaceXonix Implementation Plan

## 1. Current Project State

### Audit snapshot

This audit was performed against the open Unity editor project **SpaceXonix** on
2026-09-15. Unity MCP was connected to `SpaceXonix@a9f913b6fa3ae70c` and reported
Unity **6000.3.20f1**. The editor was idle, not compiling, and not in Play Mode.

The project is an early Unity 6 URP template foundation, not a partial gameplay
implementation. No SpaceXonix gameplay systems have been found.

### Repository and working tree at audit start

- Branch: `main`
- HEAD: `88224264dbc7c5876dceced8d3c4ae1d4f9260e8` (`Initialize SpaceXonix Unity project`)
- Remote: `origin` → `https://github.com/idanyossi/SpaceXonix.git`
- The working tree was **not clean before this task**. It contained an
  `AGENTS.md` repository-link addition, Unity MCP package manifest/lock updates,
  and this plan. The temporary untracked `NewEmptyCSharpScript` placeholder was
  subsequently verified unreferenced and removed during baseline cleanup.

### Assets, scenes, and runtime content

- `Assets/Scenes/SampleScene.unity` is the sole scene and only enabled Build
  Settings entry.
- Its hierarchy contains only `Main Camera`, `Directional Light`, and `Global
  Volume`; it has no game-specific GameObjects or components.
- There is no project gameplay C# code, prefab, ScriptableObject, test, or
  gameplay art/audio asset.
- The project currently has template URP settings assets: PC and Mobile render
  pipeline/renderer assets, global settings, and the SampleScene Volume profile.
- The active pipeline is **Universal Render Pipeline** using `PC_RPAsset` at
  quality level `PC`; Linear colour space, HDR enabled, 1x MSAA, and four shadow
  cascades are configured. The PC renderer has active Screen Space Ambient
  Occlusion. The scene has a global Volume with Tonemapping, Bloom, and
  Vignette active; Motion Blur is inactive.
- The camera is the template Main Camera (no Cinemachine Brain); Cinemachine is
  not installed. The GDD requires it for gameplay camera and shake, so adding it
  later is a deliberate dependency, not an assumption that it already exists.

### Packages and input

- Relevant installed packages are URP 17.3.0, Input System 1.19.0, uGUI 2.0.0,
  Test Framework 1.6.0, Timeline 1.8.12, and the IDE integrations. Unity MCP
  10.0.0 is present as a current user change. No packages will be changed in
  this audit.
- `Assets/InputSystem_Actions.inputactions` is Unity's template Player/UI asset:
  Player actions include Move, Look, Attack, Interact, Crouch, Jump, Previous,
  Next, and Sprint; UI has standard navigation and touch bindings. It does not
  yet define the GDD's shared directional-command, ability, power-shot, or pause
  actions.

### Platform and project settings

- The project name is SpaceXonix and Android is configured as a target, with
  minimum SDK 25 and IL2CPP scripting backend. The current Windows editor target
  is StandaloneWindows64.
- The project remains template-configured rather than product-configured:
  application identifiers use the Unity URP template values; screen orientation
  is Auto Rotation with all four orientations enabled; and Android's default
  window dimensions are landscape values. A later foundation task should set and
  verify portrait-only behaviour, safe-area behaviour, and the intended app IDs.
- Existing named tags are Unity defaults plus `Player` and `GameController`.
  Existing named layers contain only Unity defaults (Default, TransparentFX,
  Ignore Raycast, Water, UI). Gameplay collision layers should be added only
  alongside the systems that need them.

### Console baseline

The Console has **five** pre-existing exceptions, all the same Unity AI
Assistant `NoSubscription` message from
`com.unity.ai.assistant/.../ModelSelectorSuperProxyActions.cs`. The requester
has designated these package-originated messages as accepted baseline noise.
There were no observed SpaceXonix script errors. This task must not repair or
modify Unity AI Assistant.

## 2. GDD Requirements

SpaceXonix is a portrait, 2.5D, URP arcade territory-capture game for Android
and Windows. A four-direction spaceship moves continuously over safe territory
or creates a vulnerable trail in uncaptured space. Reconnecting the trail to
safe territory captures the enemy-free enclosed region; the stage target is
75% territory. An exposed player or trail struck by an alien loses a life;
stages start with three lives and a run ends after all lives are lost.

Normal stages use Basic Bouncer, Linear, Unstable, and Volatile enemies plus
horizontal/vertical lasers. Volatile explosions remove captured territory as
well as affecting enemies/player. Captures award score with large-capture
multipliers, charge a Power Meter, and may spawn a one-slot temporary ability:
Shield, Freeze, or Arena Tilt. Touching a pickup stores it immediately when the
slot is empty. When occupied, touching another pickup pauses gameplay for an
explicit Keep / Replace decision; Keep retains the stored ability and Replace
stores the new one. Power at 100 produces a pooled directional Power Shot that
destroys the first standard alien and only interrupts the boss.

The campaign has four data-driven normal stages in one Game scene, a fifth
Boss scene, random compatible stage modifiers, and one of three random
run-only upgrades after stages 1–4. Required screens are Boot, Main Menu,
briefing, gameplay HUD, upgrade selection, pause, stage complete, game over,
settings, and controls. The game requires keyboard and swipe input through one
abstraction, object pooling, ScriptableObject configuration, PlayerPrefs for
settings/high score, coroutines for defined timed flows, and a GameManager
singleton. GDD scope explicitly excludes multiplayer, online services, accounts,
cloud saves, procedural arena geometry, permanent progression, and extra modes.

## 3. Proposed Project Structure

Create folders only as their first real content is added; this is the intended
layout, not a request to create empty directories now.

```text
Assets/
  Art/
    Sprites/  Materials/  Shaders/  UI/
  Audio/
    Music/  SFX/  Mixers/
  Input/
    SpaceXonix.inputactions
  Prefabs/
    Gameplay/  Enemies/  Hazards/  UI/  VFX/  Pooling/
  Scenes/
    Boot.unity  MainMenu.unity  Game.unity  Boss.unity
  ScriptableObjects/
    Balance/  Stages/  Enemies/  Upgrades/  Modifiers/  PowerUps/
  Scripts/
    Core/  Board/  Player/  Input/  Enemies/  Hazards/  Power/
    PowerUps/  Campaign/  Boss/  UI/  Audio/  Pooling/  Presentation/
  Tests/
    EditMode/  PlayMode/
  VFX/
  ThirdParty/
    ATTRIBUTION.md
```

Use namespaces that follow the folder/domain boundary where helpful, rather
than a global collection of unrelated scripts. External assets, if later used,
must be license-recorded in `Assets/ThirdParty/ATTRIBUTION.md`.

## 4. Runtime Architecture

`GameManager` is the one global campaign singleton. It owns campaign state,
current stage, score/lives/run reset, scene transitions, and high-level state
gates; it does not implement board simulation or enemy movement.

| System | Concrete responsibility | Key collaborations/events |
|---|---|---|
| GameManager | Stage/run state machine, lives, score ownership, transitions | Subscribes to death/capture/boss events; starts stage data and gates input. |
| BoardManager | Board grid, safe cells, exposed trail, capture, territory removal | Publishes TrailStarted, TrailFailed, TerritoryCaptured, TerritoryDestroyed. |
| PlayerController | Continuous cardinal grid motion, facing, respawn presentation | Receives commands from InputRouter; asks BoardManager to advance a cell. |
| InputRouter | Converts keyboard/swipes/buttons to direction, ability, shot, pause commands | Disabled by GameManager outside active play; ignores UI-owned touch. |
| EnemyManager | Spawn/despawn and active-enemy registry | Provides BoardManager snapshot/occupancy for capture; uses PoolService. |
| EnemyController | Shared lifecycle, freeze state, damageable/pool reset contract | Base for all standard enemy variants. |
| BouncerEnemy | Continuous uncaptured-space motion and boundary bounce | Reports occupied cells/positions and contact. |
| LinearEnemy | Fixed horizontal or vertical motion, reversal when blocked | Same shared enemy contract. |
| UnstableEnemy | Bouncer-like movement with telegraphed configured speed changes | Modifier can adjust change cadence. |
| VolatileEnemy | Standard motion, delayed collision-safe arming, explosion | Requests BoardManager territory removal; never owns board data. |
| LaserManager | Emits warning/firing/cooldown cycles for cardinal lasers | Uses PoolService; reports player/trail hit without affecting enemies. |
| PowerMeter | Capture-to-power conversion and pooled directional Power Shot | Listens to TerritoryCaptured; targets standard enemies/boss interrupt. |
| PowerUpManager | Capture-triggered pickup chance, one stored ability, active effects | Stores immediately when empty; when occupied, requests a Keep/Replace UI decision while gameplay is paused. |
| UpgradeManager | Random three-option run upgrade selection/application | Provides effective stats to later stage initialization. |
| StageModifierManager | Selects compatible stage modifier and exposes its effects | Modifies stage spawn/hazard/balance setup, not ad-hoc scripts. |
| BossController | Boss-only attack loop, pooled projectiles, capture-damage feedback | Listens to board capture; completed at 75% capture. |
| PoolService | Typed pools and reset lifecycle for frequent objects | Owns acquire/release, never gameplay policy. |
| UIManager | Screens, HUD binding, pause, safe-area-aware controls | Subscribes to state events; sends UI commands to InputRouter/managers. |
| AudioManager | Persistent music/SFX playback and settings application | Subscribes to meaningful gameplay/UI events. |

Use narrow C# events or event channels for cross-domain facts: capture result,
trail failure, player death, enemy destroyed, power changed, ability changed,
stage state, and boss state. Direct serialized references are appropriate for
scene-local collaborators. Avoid polling scene searches and do not turn
GameManager into a service locator.

## 5. Territory Capture Technical Design

### Authoritative representation

Use a configurable, axis-aligned logical grid independent of sprites, physics
colliders, and camera tilt. `BoardManager` is its only mutator. The initial
default is **54 columns × 96 rows**. Store dimensions in board/balance data and
pass them into board initialization; do not scatter `54` or `96` constants
through gameplay code.

- `CellState`: `Uncaptured`, `Captured`, or `Trail`. Store it in a contiguous
  one-dimensional array indexed by `x + y * width` for predictable traversal
  and low allocation.
- Keep the current trail as an ordered `List<int>` plus a same-size trail-mark
  bitmap/array for O(1) self-interaction checks and deterministic cleanup.
- The player has a logical cell and cardinal direction. Rendering interpolates
  between accepted cell steps; logical movement is never derived from world
  float comparisons.
- Maintain captured and total playable-cell counts. Capture percentage is
  `capturedPlayableCells / totalPlayableCells * 100`; trail cells are not safe
  or captured until a successful reconnection resolves.
- EnemyManager exposes a stable per-resolution snapshot of alive standard enemy
  grid cells (or grid-overlap cells). Convert world positions once per simulation
  tick and use that snapshot for collision and capture decisions.

### Movement and failure

On a step from Captured to Uncaptured, start a trail and mark each entered
uncaptured cell Trail. Continue marking cells while exposed. Entering an
already-Trail cell is self-interaction and fails the trail. Enemy contact with
the exposed player or any Trail cell also fails it. A failure removes only the
current Trail cells back to Uncaptured, clears the ordered list/bitmap, emits
one failure event, and lets GameManager spend a life/drive respawn. Captured
cells survive normal failure.

On a valid step from Trail to Captured, first commit the path as a temporary
barrier. Reconnection must occur at a captured cell distinct from the opening
path boundary as validated by the step rules; do not run a capture for an empty
or malformed trail.

### Region selection algorithm

The committed trail plus existing Captured cells are barriers. Flood-fill each
connected component of cells that are still passable (`Uncaptured`) using
four-neighbour adjacency. During each fill gather: cell indices, area, and
whether any active enemy position/overlap lies in the component.

The capture rule is then deterministic:

1. Components containing an active standard alien are ineligible.
2. Among all alien-free components, select the smallest connected component;
   ties must use a stable deterministic order such as lowest cell index.
3. Capture only that selected component. If no alien-free component exists, no
   connected region is captured.
4. On a successful selection, convert the committed Trail cells and selected
   component to Captured, clear trail state, recompute percentage, and publish a
   result containing captured-cell count and percentage.

If no component is eligible, still commit the completed trail to Captured so it
becomes safe territory and the player cannot remain exposed. This is not a
region capture; only the newly safe playable trail cells contribute to progress.

This is the confirmed generalization of the GDD's smaller-valid-region rule.
Board bounds must begin as captured border cells, ensuring the first trail can
form a closed topology.

### Territory destruction

Volatile explosions operate on the same cells: BoardManager receives a logical
explosion centre/radius, selects currently Captured playable cells within the
chosen radius metric (recommend Euclidean distance in grid/world units), changes
them to Uncaptured, decrements the captured count, and publishes
`TerritoryDestroyed`. A Volatile explosion does **not** directly remove,
invalidate, or otherwise modify a live Trail. It only destroys appropriate
nearby standard enemies, kills the player when the ship is inside its blast
radius, and removes already Captured cells. Using a single mutable grid makes
capture loss, percentage, future flood fills, and rendering agree.

For performance, reuse flood-fill queues, visited stamps, component lists, and
enemy-occupancy buffers. Render cell state through a board-view layer that
receives changed-cell batches; animation must never delay or alter the logical
state transition.

## 6. ScriptableObject / Data Design

Use data assets for tunable data, while runtime state remains in managers.

- `GameBalanceConfig`: movement speed, starting lives, target capture percent,
  score thresholds/multipliers, power conversion, pickup probabilities,
  respawn/laser timing, and default board dimensions (initially 54 × 96).
- `StageDefinition`: stage number, configurable board dimensions/definition,
  standard enemy spawn requests, laser layout, modifier compatibility, and
  normal/boss designation.
- `EnemyDefinition`: prefab/pool key, movement/bounce/linear settings, unstable
  speed ranges/telegraph cadence, volatile arming/explosion radius/damage.
- `PowerUpDefinition`: ability type, duration, UI/audio/VFX references and
  tunable effects. Arena Tilt specifies force and player penalty.
- `UpgradeDefinition`: display data, max stack, eligibility, and data-driven
  stat effect.
- `StageModifierDefinition`: compatibility predicate, display/score multiplier,
  and typed effect values (rather than arbitrary string keys).
- `BossDefinition`: projectile pooling key, firing interval/speed, capture
  reaction and compatible boss modifier ranges.
- Optional presentation configuration can centralize palette/VFX/audio cue
  references without coupling board logic to effects.

ScriptableObjects are immutable configuration at runtime. Copy effective values
into a per-run stat model when upgrades/modifiers must stack, so assets are not
mutated across campaigns or in the editor.

## 7. Scenes

The current project has only `SampleScene`; it does not yet match the GDD.
Replace the template scene structure only during the foundation/scene setup
phase, after references and build settings are deliberately configured:

| Required scene | Purpose |
|---|---|
| `Boot` | Initialize persistent GameManager, AudioManager, PoolService, settings, then route to MainMenu. |
| `MainMenu` | Main menu, settings, controls, and campaign start. |
| `Game` | Reusable data-driven gameplay scene for stages 1–4; no duplicated stage scenes. |
| `Boss` | Alien Core encounter using the same relevant campaign/persistent services. |

Stage briefing and the remaining screen states are UI panels/flows, not extra
gameplay scenes. Scene references should be configured through Build Settings or
addressable-style identifiers only after the intended scene names exist.

## 8. Implementation Order

1. Establish project foundation: folders, namespaces, Boot/MainMenu/Game/Boss
   scene skeletons, portrait/mobile configuration, build settings, baseline UI,
   and persistent service lifecycle.
2. Define input actions and implement InputRouter plus grid-based cardinal
   PlayerController movement on a minimal board.
3. Implement and unit-test BoardManager's logical grid, trail lifecycle,
   enemy-aware flood fill, capture events, and board view.
4. Add GameManager lives, death, trail cleanup, respawn, stage target, and
   campaign reset.
5. Build pooling and the shared enemy framework; add Basic Bouncer.
6. Add Linear and Unstable enemies, then their stage data/spawning.
7. Add Volatile enemy, explosion presentation, logical territory destruction,
   and LaserManager.
8. Add score, capture multipliers, PowerMeter, and pooled Power Shot.
9. Add pickup spawning, one-slot storage, Shield, Freeze, and Arena Tilt.
10. Add stages 1–4 as StageDefinitions and stage-completion/briefing flows.
11. Add UpgradeManager and compatible StageModifierManager behaviour.
12. Implement Boss scene/controller and campaign-complete flow.
13. Complete UI, safe-area handling, Android swipe/button input, settings, and
    PlayerPrefs.
14. Add licensed art, audio, VFX, animation, Cinemachine presentation, and
    attribution records.
15. Polish readability/feedback and accessibility of hazards.
16. Run end-to-end QA, device profiling, allocation review, and builds.

## 9. Testing Strategy

Prioritize EditMode automated tests for pure board and campaign logic. Cover
grid indexing/bounds; trail start, self-hit, reconnect, and cleanup; flood-fill
components; enemy-containing region exclusion; no-enemy smaller-region fallback;
percentage; exact volatile territory removal; score/multiplier; power/pickup
probability bounds; modifier compatibility; upgrade stack limits; and campaign
state transitions. Use deterministic seeds for random selection tests.

PlayMode tests should verify input-to-grid motion, player death/respawn gating,
enemy movement contracts, pooled object reset/reuse, laser state sequence,
abilities' timed effects, immediate empty-slot pickup storage, occupied-slot
Keep/Replace pause flow, normal-stage progression, boss interruption/capture
completion, UI state binding, and scene persistence. Manual acceptance tests
should exercise portrait safe area, swipe threshold/UI-touch exclusion, PC
keyboard controls, Android build/device performance, and visual telegraphing.

Run a focused regression set after every board or pooling change. Use Unity
Profiler/GC Allocation checks during sustained enemy/laser/pickup loops and
the target Android device before declaring performance ready.

## 10. Technical Risks

1. **Territory topology and enemy-aware capture:** Incorrect barrier/flood-fill
   semantics can incorrectly capture enemies, leak regions, or produce invalid
   percentages. Mitigate with a headless deterministic grid model and extensive
   topology test cases before presentation.
2. **Grid/world synchronization:** Rendering interpolation and physics-style
   enemy movement can disagree with the authoritative cell model. Use a fixed
   logical simulation step, explicit conversion rules, and test boundary cases.
3. **Volatile territory destruction:** It mutates the same topology after
   capture. Keep all cell mutation in BoardManager and test subsequent capture
   after blast-created holes.
4. **Pool state leaks:** Timers, subscriptions, velocities, collider state,
   VFX and ownership can survive release/reacquire. Define/reset contracts and
   PlayMode reuse tests for every poolable type.
5. **Coroutines across state transitions:** Respawn, ability, laser, boss, and
   UI coroutines must be cancelled or version-guarded on reset, scene change,
   disable, and pool release.
6. **Campaign state and scene references:** Persistent run state needs clear
   ownership, reset points, and no stale scene references after changing scenes.
7. **Mobile input/UI:** Swipe gesture recognition, safe areas, and UI event
   consumption must not generate accidental player movement or leave controls
   active while paused/respawning.

## 11. GDD Ambiguities and Recommended Defaults

| Ambiguity | Recommended default to confirm |
|---|---|
| What counts as an enemy inside a region at a grid boundary | Treat every cell overlapped by an active enemy's configured logical footprint as occupied; boundary/captured cells are not passable. |
| Shield versus unfinished trail | Shield protects player/laser contact as specified but does not prevent an alien striking the unfinished trail from causing trail failure. |
| Boss modifier selection | Restrict to explicit boss-compatible definitions affecting projectile speed/interval/frequency; normal-enemy modifiers are not eligible. |
| Power Shot direction when facing changes at a grid turn | Fire in the PlayerController's most recently accepted cardinal direction at button press. |
| Reinforced Hull timing | Apply +1 life at the beginning of every following stage, not retroactively during the stage in which it is selected. |

No gameplay implementation is included in this task.
