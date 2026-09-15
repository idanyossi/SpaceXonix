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
- `GameManager.ReportPlayerFailure(...)` is the sole life-loss entry point; respawn uses the last safe cell.
- Player and enemy movement remains cardinal/grid-compatible; standard enemies are pooled.
- Enemy registration, occupancy, movement pause/resume, and pooled reset are centralized through `EnemyManager` and `EnemyController`.
- Volatile collision/explosion orchestration stays in `EnemyManager`; territory removal stays authoritative in `BoardManager`/`BoardModel` and never directly changes an active trail.
- `LaserManager` advances independent axis-aligned emitters; `LaserEmitter` routes firing contact through the authoritative `GameManager` failure pipeline.
- Laser warning and beam presentations are pooled, and lasers intentionally do not affect enemies, Volatile behavior, territory, or unfinished trails.

## Current Test State

- EditMode discovered: 70
- Passed: 70
- Failed: 0
- Coverage includes input/movement, board/trail/capture/destruction, the complete death/respawn lifecycle, safe-cell restoration, captured-territory preservation, duplicate failure rejection for every failure reason, repeated deaths, Game Over, all enemy behavior, manager occupancy, pooling/reset, Volatile protection/detonation, laser timing/geometry/presentation reuse, hazard isolation, and authoritative player damage.

## Respawn Reliability Fix

- Root cause: `ProjectSettings.runInBackground` was disabled. When the Unity Game view lost focus, scaled game time and frame progression stopped, leaving the live `WaitForSeconds(1.25)` coroutine pending and the player apparently trapped in `Respawning` even though the GameManager remained enabled and `Time.timeScale` was 1.
- Fix: background progression is enabled while the respawn continues to use the configured scaled 1.25-second delay. Respawn completion is isolated as a deterministic state transition for direct lifecycle testing.
- Player reset now keeps the PlayerController and InputRouter direction state synchronized to the configured initial direction (`Right` in `Game.unity`) while restoring transform/logical position, board tracking, input, and movement.
- Play Mode QA in the real `Game.unity` verified complete laser, enemy-contact, and Volatile-explosion death cycles. Each lost exactly one life, cleared an active trail where applicable, returned to the recorded safe cell, restored control, returned to `Playing`, and allowed a later death to complete normally.
- Unity Console: 0 SpaceXonix errors after verification.

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
