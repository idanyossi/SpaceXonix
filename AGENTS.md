# SpaceXonix Development Instructions

This repository contains SpaceXonix, a Unity 6.3 LTS game.

Before making changes:
1. Read the Game Design Document completely.
2. Inspect the existing Unity project and existing implementation.
3. Never assume a system is missing until you search for it.
4. Preserve existing working systems unless the requested task explicitly replaces them.

## Unity

Unity version:
6000.3.20f1

Render pipeline:
URP

Targets:
- Windows PC
- Android

Reference resolution:
1080x1920 portrait

Use the Unity MCP tools when available for Unity-specific operations such as:
- inspecting scenes
- creating or configuring GameObjects
- assigning components
- inspecting prefabs
- checking serialized references
- entering Play Mode
- reading the Unity Console

Prefer Unity MCP/editor operations over manually editing complex .unity or .prefab YAML.

## Architecture

Follow the architecture defined by the GDD.

Major responsibilities:
- GameManager: campaign/stage/lives/state
- BoardManager: territory/trail/capture
- PlayerController: movement
- InputRouter: PC/mobile input abstraction
- EnemyManager: enemy lifecycle
- EnemyController: shared enemy state
- Hazard/LaserManager: hazards
- PowerMeter: power charging and shots
- PowerUpManager: temporary abilities
- UpgradeManager: campaign upgrades
- StageModifierManager: modifiers
- BossController: boss
- UIManager: UI
- AudioManager: sound/music
- PoolService: reusable runtime objects

Avoid giant god classes.

Use events where systems need to react without tight coupling.

Use ScriptableObjects for configurable gameplay data where specified in the GDD.

## Coding rules

- Use clear C# naming and structure.
- Do not add unnecessary abstractions.
- Do not introduce third-party packages unless necessary.
- Avoid FindObjectOfType and repeated scene searches during gameplay.
- Avoid per-frame allocations where practical.
- Use object pooling for frequently created runtime objects.
- Expose tuning parameters through serialized fields or ScriptableObjects.
- Add comments only where logic is genuinely non-obvious.

## Gameplay correctness

Territory capture is the core feature and correctness has priority over presentation.

Never compromise capture correctness merely to make a visual effect work.

Handle:
- exposed trail creation
- trail self-interaction
- reconnection
- flood-fill / region determination
- enemy-containing regions
- life loss
- trail cleanup
- territory destruction

## Verification

After each implementation task:

1. Compile the project.
2. Inspect Unity Console errors.
3. Run relevant automated tests if present.
4. Use Play Mode when available.
5. Test the feature's requested acceptance criteria.
6. Check git diff for unintended modifications.

Do not declare the task complete while compilation errors remain.

## Assets

Do not download or use third-party assets unless their license is clearly compatible.

For every external asset record:
- asset name
- creator
- original URL
- license
- required attribution

Store this in Assets/ThirdParty/ATTRIBUTION.md.

Prefer:
- CC0
- public-domain
- permissive commercial-use licenses

Never assume that "free download" means free to redistribute.

## Scope

Implement only features defined by the GDD or explicitly requested by the user.

Do not add speculative systems.

Stretch goals are not part of the required implementation unless explicitly requested.

## Git

Before large changes:
- inspect git status
- avoid overwriting unrelated user work

At completion:
- report changed files
- report tests performed
- report known limitations
- do not hide failed tests or warnings