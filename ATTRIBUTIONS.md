# Asset Attributions and Licences

Every third-party asset shipped in SpaceXonix must be listed here before it is committed, with a
licence that permits use in this project. An asset with no entry here is not cleared to ship.

The GDD requires this record (`SpaceXonixProposal.md`, "Asset Licensing"): *"Third-party assets will
only be used when their licence permits use in the project."*

---

## Currently shipped

### Art — third-party

| Asset | Author | Source | Licence | Used for |
|---|---|---|---|---|
| `Assets/SpaceXonix/Art/ThirdParty/Ansimuz/*.png` (7 sprite sheets) and the frames cut from them | Luis Zuno ([@ansimuz](https://ansimuz.com)) | [Space Ship Shooter Pixel Art Assets](https://opengameart.org/content/space-ship-shooter-pixel-art-assets), OpenGameArt | **CC0 1.0** | Player ship, all four alien types, the Alien Core, pickups, boss projectile, the ship's death explosion |

| `Assets/SpaceXonix/Art/ThirdParty/Ansimuz/Background/*.png` (5 layers) | Luis Zuno ([@ansimuz](https://ansimuz.com)) | [Space Background](https://opengameart.org/content/space-background-3), OpenGameArt | **CC0 1.0** | The drifting space backdrop behind the arena and menus |

| `Assets/SpaceXonix/Art/ThirdParty/Master484/Skins/*.png` (6 ships, 2 frames each) | Master484 (M484 Games) | [16x16 Ship Collection](https://opengameart.org/content/1616-ship-collection), OpenGameArt | **CC0 1.0** ("These graphics are in the Public Domain. Attribution is not needed.") | Ship skins: Cobalt Delta, Viper, Ember Talon, Solar Hornet, Nebula Dart, Phantom Rail. Cut from the sheet, turned nose-up and given thruster frames by **SpaceXonix > Build Ship Skins**; the sheet itself stays in the git-ignored `AssetSources/` |

The licence text ships beside the art in `Art/ThirdParty/Ansimuz/LICENSE.txt` and `Art/ThirdParty/Ansimuz/Background/LICENSE.txt`. CC0 needs no
attribution; it is recorded here anyway, and ansimuz asks that you *"spread the word"*.

The art is used modified, which CC0 permits: the Unstable and Volatile aliens and the Alien Core are
recoloured copies of the pack's enemy designs, and three power-ups share two orb designs by tint.

### Audio — third-party

| Asset | Author | Source | Licence | Used for |
|---|---|---|---|---|
| `Assets/SpaceXonix/Audio/ThirdParty/Junkala/Sfx/*.wav` (shortlisted candidates) | Juhani Junkala | [The Essential Retro Video Game Sound Effects Collection (512 sounds)](https://opengameart.org/content/512-sound-effects-8-bit-style) | **CC0 1.0** | All 20 game sounds, chosen by ear in **SpaceXonix > Sound Audition** |
| `Assets/SpaceXonix/Audio/ThirdParty/Junkala/Music/*.wav` (4 tracks) | Juhani Junkala | [5 Chiptunes (Action)](https://opengameart.org/content/5-chiptunes-action) | **CC0 1.0** | Menu (Title Screen), gameplay (Level 1), boss (Level 3), victory (Ending) |

The licence text ships in `Audio/ThirdParty/Junkala/LICENSE.txt`. Level 2 of the music pack is not
included, to keep 13 MB of WAV out of the public repository.

**Replaced:** the Kenney Sci-Fi Sounds and the generated placeholder clips were removed. The Kenney
sounds were chosen by file name rather than by ear and did not suit the game; smooth modern sci-fi
effects also clashed with the 8-bit pixel art in a way chiptune effects do not.

### Fonts

| Asset | Source | Licence | Notes |
|---|---|---|---|
| `Assets/SpaceXonix/Art/ThirdParty/Kenney/Fonts/Kenney Mini Square.ttf` and `Kenney Pixel Square.ttf` | Kenney ([kenney.nl](https://kenney.nl)), [Kenney Fonts](https://kenney.nl/assets/kenney-fonts) | **CC0 1.0** | Body text and titles in every menu and the HUD. Licence text in the same folder |

### Packages

Unity packages (URP, Cinemachine, Input System, uGUI, Test Framework) are covered by the Unity
Companion Licence and do not need individual entries.

---

### Generated in the project

The board, trail and laser textures (`Art/Generated/*.png`, from **SpaceXonix > Generate Board Art**)
and the menu widgets (`Art/Generated/UI/*.png`, from **SpaceXonix > Apply UI Skin**) are drawn in code
by this project, so they carry no third-party licence.

## Still to source

The game runs without these, using the stand-ins listed.

| Need | Used by | Current stand-in |
|---|---|---|
| Power Shot | `Prefabs/Power/PowerShot.prefab` | A stretched cube. The pack's bolt sprites could replace it, but its launch code stretches the visual, so it needs code changes first |
| UI icons — upgrades, abilities, pause | `Prefabs/UI` | Text labels |
| Title/logo | `MainMenu.unity` | The title in Kenney Pixel Square |

Section 12 of `IMPLEMENTATION_PLAN.md` settled the visual direction: **pixel-art sprites** on a
diagonal-down perspective camera, with gameplay staying on the XY plane. New art should match
ansimuz's style: a 16-pixel grid, a limited palette, and a top-down view.

### Considered for ship skins and not used

- **Kenney Pixel Shmup** (CC0): licence-compatible, but its heavy dark outline and larger 32-pixel ships clash with ansimuz's outline-free 16-pixel art. Master484's collection matches it far more closely.
- **Kenney Space Shooter Redux / Remastered ships** (CC0): smooth vector art, the same mismatch that ruled out Kenney's effects.

### Why most itch.io packs were ruled out

This repository is **public** on GitHub, so committing an asset publishes its raw files. Several
otherwise good packs forbid exactly that and were rejected on those grounds:

- **[8x8] Space Shooter Asset Pack** (gvituri): *"you are not allowed to redistribute this asset"*
- **CraftPix / Free Game Assets** packs: the licence forbids reselling the source files
- **Space Shooter Asset Pack** (dani567): paid, and *"You may not redistribute it"*
- **Space Shooter GB** (chasersgaming): CC-BY-SA 4.0, usable, but the four-colour Game Boy palette does not fit
- **FREE pixel art space shooter kit** (Helianthus Games): silent on redistribution, and the ships are side-view, which does not fit our top-down camera

Only CC0, CC-BY or CC-BY-SA assets are safe to add while the repository is public.

### Licence-compatible sources

The GDD lists these as acceptable starting points. Anything used from them still needs a row in the
table above, recording the specific asset, author, source URL and licence.

- **Kenney** (kenney.nl) — CC0, no attribution required, though it is still recorded here
- **OpenGameArt** — licence varies *per asset*; check each one, many are CC-BY and need attribution
- **freesound.org** — licence varies per sound; CC0 and CC-BY are both common
- **Unity Asset Store** — free assets are usually covered by the Asset Store EULA

CC-BY assets require crediting the author in the shipped game, not only in this file. When the first
CC-BY asset is added, an in-game credits screen has to go on the Phase 20 list.

---

## Adding an asset

1. Confirm the licence permits use in a distributed game, including commercial use if that is ever intended.
2. Add a row above with the asset path, author, source URL and licence.
3. Keep the licence text itself in `Assets/SpaceXonix/Licences/` when the licence requires it to be distributed.
4. If it needs visible credit, add it to the credits screen as well as here.
