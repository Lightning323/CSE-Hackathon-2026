# Project Structure

Grid Quest is a Godot 4.7 (C#, Forward+) 2D game built for CSE Hackathon 2026.

```
grid-quest/
├── project.godot              # Engine configuration
├── Grid Quest.csproj          # .NET 8 project (Godot.NET.Sdk 4.7.2)
├── Grid Quest.sln             # Solution file
├── icon.svg                   # Project icon
├── setup_mcp.sh               # Registers the Godot MCP server with opencode
├── AGENTS.md                  # Instructions for AI coding agents
├── docs/
│   ├── project-structure.md   # This file
│   └── map-architecture.md    # Map tile/item architecture decisions
├── assets/
│   ├── audio/                 # Music and sound effects
│   ├── docs/                  # Asset licenses/notes (e.g. Cute Fantasy readme)
│   └── images/
│       ├── map_tiles/         # Grass, plants, and trees used by the map
│       ├── characters/        # Character sprites (player, allies, animals)
│       └── enemies/           # Enemy sprites (e.g. skeleton, slime assets)
├── enemy/
│   ├── Enemy.cs               # Configurable enemy AI, combat, and animation
│   ├── enemy.tscn             # Base skeleton enemy
│   ├── skeleton_enemy.tscn    # Medium skeleton variant
│   └── elite_skeleton_enemy.tscn # Hard skeleton variant
├── level/
│   ├── EnemyTestSpawner.cs    # Temporary number-key enemy spawner
│   ├── KnightSpawnInput.cs    # Spawns an allied knight helper on G
│   └── main_level.tscn        # Main gameplay level (map, player, enemies, helpers)
├── items/                     # Item/inventory data model
│   ├── Coin.cs                # World coin pickup
│   ├── Fireball.cs            # Projectile item logic
│   ├── ItemDefs.cs            # Item definitions, stacks, and catalog
│   └── InventoryContainer.cs  # Slot container with add/swap/stack helpers
├── map/                       # Map tile definitions, generation, and object factories
│   ├── Map.cs                 # Runtime map composition and building placement
│   ├── MapConstants.cs        # Shared map constants (e.g. cell size)
│   ├── RoomGenerator.cs       # Builds rectangular rooms (configurable wall edges, chest spawn)
│   ├── map.tscn               # Reusable map scene
│   ├── map_floors/            # Floor tile factories (grass/stone)
│   └── map_objects/
│       ├── MapObjectFactory.cs # Base class for placeable map objects
│       ├── chest/             # Chest object/scene (open state, stored loot)
│       ├── gate/              # Gate/door object and scene
│       ├── plant/             # Plant factory
│       ├── tree/              # Tree factory/scene
│       └── walls/             # Wall factory/scene
├── menus/
│   ├── InventoryUi.cs         # Inventory + chest UI (drag/drop, move, take-all)
│   ├── InventorySlotUi.cs     # Reusable inventory slot UI control
│   ├── MonumentIndicator.cs   # Directional monument indicator UI
│   ├── SkillsUi.cs            # Skills UI overlay
│   ├── inventory_ui.tscn      # Inventory/chest UI scene root
│   ├── skills_ui.tscn         # Skills UI scene
│   ├── main_menu.tscn         # Legacy main menu scene
│   └── mainMenu/
│       ├── MainMenu.cs        # Save selection/creation UI and game boot flow
│       ├── MainMenu.tscn      # Main menu scene (startup scene)
│       ├── SaveManager.cs     # Save discovery/creation/deletion in app data
│       └── GameSession.cs     # Autoload holding currently selected save
├── npc/
│   └── knight/
│       ├── KnightNpc.cs       # Allied knight AI, combat, and regeneration
│       └── knight_npc.tscn    # Knight character scene
└── player/
	├── Player.cs              # Player movement, combat, health, and animations
	├── Player.cs.uid          # Godot-generated script UID
	└── player.tscn            # Player character scene
```

## Player

`Player.cs` builds its `SpriteFrames` at runtime from `assets/images/characters/cute_Player.png` (32x32 frames, six columns):

| Row | Animation | Frames |
| --- | --- | --- |
| 0-2 | idle down / side / up | 6 |
| 3-5 | walk down / side / up | 6 |
| 6-8 | attack down / side / up | 4 |
| 9 | death | 4 |

Movement is 4-directional with WASD/arrow keys and is allowed during attacks. Space attacks;
the player can damage enemies and knights. Health is shown in the player health bar. `H` toggles
temporary debug invulnerability (remove when no longer needed).

The player has a slot-based backpack inventory and weapon slot (`items/ItemDefs.cs`, `InventoryContainer.cs`).
Coins collected in-world are tracked as gold and added to inventory as stackable coin items.

## Inventory, Chests, and Items

- `menus/inventory_ui.tscn` + `InventoryUi.cs`/`InventorySlotUi.cs` provide the inventory UI and chest UI.
  - Toggle inventory: `E`/`Tab`/`I` (controller `Y`); close: `Esc` or toggle key.
  - Interact/open nearby chest: `F` (opens chest and shows chest panel alongside inventory).
  - Supports click-to-select move/swap and drag-and-drop between backpack, weapon slot, and chest; stacks merge where applicable, and chest loot can be moved with **Take All**.
- `map/map_objects/chest/` defines a stateful chest object (`Chest.cs`, `chest.tscn`) that opens on first interaction, generates a random dynamic loot pool once, and stores items in a 12-slot container for transfer in/out.
- `RoomGenerator` spawns one chest inside each generated room; `Map.GenerateBuildings()` places multiple rooms/buildings so chests appear in those rooms.

## Map and Tiles

- `map/Map.cs` (attached to `map/map.tscn`) procedurally generates the 200×200 map at runtime (`MapConstants.CELL_SIZE` = 16px) and composes it into a `Floor` layer and an `Objects` layer (`Walls`, `Plants`, `Decor`, `Houses`, `Trees`), and can place generated buildings/rooms.
- **Ground:** the base terrain is grass everywhere (`assets/images/map_tiles/floor/grass.png`). Procedural villages and monument approach routes carve stone path tiles (`assets/images/map_tiles/floor/stone.png`).
- **Villages:** generation places multiple spaced village clumps (without overlap with each other, the monument, paths/reserved areas, or map borders) and exposes their bounds/centers as `Map.Villages` (`VillageArea`). Each village includes a small path network, 3–4 houses, and naturally decorated interiors with plants and town bushes placed on non-path, non-occupied ground. Houses are scene-backed map objects (`map/map_objects/house/house.tscn`, `HouseFactory.cs`) using `assets/images/map_tiles/cute_House_1_Wood_Base_Blue.png` with collision at the base.
- **Village-only town trees:** colliding, y-sorted town trees (`assets/images/map_tiles/trees/town_tree-1.png`, `town_tree-2.png`) are placed only in a ring around village bounds as village-edge decoration (separate from forest trees), avoiding paths/objects/reserved areas and overlap.
- **Monument:** the stone monument (`map/map_objects/monument/`) spawns at a random location within the camera-reachable map bounds, biased toward the center and kept away from edges, with reserved clearance from villages/objects and cardinal (N/S/E/W) path lines carved for discoverability. `Map` exposes `MonumentWorldPosition`.
- **Forest/wilderness decorations:** outside villages (and a small buffer around village bounds reserved for village decor) the generator scatters natural props including colliding, y-sorted forest trees (orange/pink/dried variants), rocks, and trunks, plus non-blocking forest bushes and ground plants for denser, more natural terrain. Blocking decor/trees use base-aligned sprites and collision shapes for correct depth and movement.
- **Buildings/rooms, gates, and chests:** `RoomGenerator.cs` builds rectangular rooms with configurable wall edges (`Solid`/`Opening`/`Gate`), optional floors, and a chest. `Map.GenerateBuilding(...)`/`GenerateBuildings()` place rooms while avoiding overlap with reserved procedural areas (villages/monument/paths). Gates (`map/map_objects/gate/`) and chests (`map/map_objects/chest/`) are interactive/stateful map objects integrated with the inventory system.
- **Map objects/layering:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/houses/props/gates/chests. `TreeFactory`, `WallFactory`, and `HouseFactory` are scene-backed factories; `PlantFactory` creates lightweight non-blocking plant nodes; `map/map_objects/props/PropFactory.cs` creates configurable decorative props (sprite offset, optional collision, y-sort). Border walls are placed in a non-y-sorted `Walls` layer behind y-sorted actors/objects so players, plants, and other objects render over the walls.
- **Map helpers/data:** `Map` provides `CellToWorldCenter`, `CellToWorldBase`, village metadata (`Villages`, `GetRandomPointInVillage`), monument position, and size helpers for other systems.
- `FloorTiles` / `FloorTileFactory` (`map/map_floors/`) build floor sprites.
- See [map-architecture.md](./map-architecture.md) for tile-versus-node decisions.

## Villagers

- `npc/villager/Villager.cs` builds villager animations at runtime and runs simple idle/patrol/flee AI. Villagers target-threat flee from enemies, can take damage, and prefer to stay within/near their assigned home village (`SetHome`) by wandering locally and returning when displaced.
- `npc/villager/VillagePopulationManager.cs` spawns a configurable number of villagers per generated village at startup and respawns villagers slowly after death when the village area is not visible to the active camera. Manual spawn inputs (`level/character_dev/VillagerSpawnInput.cs`) can also place/assign villagers to generated villages.
- **Interaction & dialogue bubbles:** villagers display a dynamically sized speech bubble above their head (`npc/villager/villager.tscn`) with placeholder dialogue for `Idle`, `Chased`, and `Recovered` (done being chased/catching breath) states. Bubbles auto-show/update on state changes (including while chased and briefly after fleeing ends) and the player can interact with the nearest nearby villager using `F` to refresh/show context-appropriate dialogue.
- **AI-ready context:** `Villager` exposes dialogue/emotional state and runtime context for future external dialogue generation via `CurrentDialogueEmotion`, `CurrentDialogueText`, and `GetDialogueContext()` (state/emotion, fleeing/recovery/threat/home/player proximity, health, current dialogue, and placeholder options per state).

## Monument Discovery

- `map/map_objects/monument/Monument.cs` tracks discovery when the player comes within discovery range (or interacts). `menus/MonumentIndicator.cs` (added to level UI layers) displays a `meta_pickaxe.png` directional arrow that always points toward the monument from the screen center (texture top treated as the pointing direction).

## Enemies

`Enemy.cs` builds `SpriteFrames` at runtime from a configurable 32×32 sprite sheet. It exposes
movement, health, combat, texture, and animation settings. Enemies target the closest player, allied knight, or villager, move toward them, and attack with a timed windup that applies damage at the end of the attack animation if the target is still in range.

| Scene | Variant | Speed | Health | Attack | Appearance/notes |
| --- | --- | --- | --- | --- | --- |
| `enemy.tscn` | Base | 50 | 3 | 1 dmg, 24 range, 0.8s cooldown | Skeleton |
| `skeleton_enemy.tscn` | Medium | 50 | 3 | 1 dmg, 24 range, 0.8s cooldown | Standard skeleton |
| `elite_skeleton_enemy.tscn` | Hard | 70 | 6 | 2 dmg, 26 range, 0.7s cooldown | Larger red-tinted skeleton |

`level/EnemySpawnOnKill.cs` performs occasional off-camera monster spawns (with a population cap and refill when empty), favoring normal skeletons over elite skeletons. Spawn pacing/cap are tuned for more frequent ambient monsters around the map. `level/character_dev/EnemyTestSpawner.cs` remains a temporary manual test spawner (keys `1`/`2`). Remove temporary spawners when no longer needed.

## Allied Knight NPC

`npc/knight/KnightNpc.cs` builds `SpriteFrames` at runtime from the `forest_hero` character
sprites under `assets/images/characters/`. Knights are allied village protectors: they target and attack nearby monsters threatening their village, can take damage, regenerate slowly after a delay out of combat, and can be assigned a home village (`SetHome`) to patrol/return near and stay in the village area.
- `npc/knight/VillageKnightPopulationManager.cs` spawns 2 knights per generated village at startup (within village bounds) and respawns them quickly (10s) when the village area is not visible to the camera to maintain the garrison.
- Manual spawn input (`G`) can still spawn a knight near the player (dev/testing). The player can also damage knights.

## Notes

- `.godot/` is generated by the editor and ignored by git; do not edit it by hand.
- `.import` files are generated by Godot on asset import and should be committed.
- The Cute Fantasy asset pack is licensed for **non-commercial** use; see
  `assets/docs/Cute_Fantasy_Free_read_me.txt`.
- Scenes use Godot 4.7 scene format (`uid=` attributes are managed by the editor).
- Custom C# scripts target .NET 8.


## Main Menu and Saves

- `menus/mainMenu/MainMenu.tscn` is the startup scene (`project.godot` `run/main_scene`) containing the menu UI layout; `menus/mainMenu/MainMenu.cs` handles behavior/wiring.
- The main menu lists save folders from the app data saves directory (`OS.GetUserDataDir()/saves`), and supports **Play**, **Delete**, and **New Save**.
- **New Save** opens a dialog for title and description, creates a new per-save folder under the saves directory with a `save.json` metadata file, sets it as the active session save, and boots into `res://level/character_dev/main_level.tscn`.
- `menus/mainMenu/GameSession.cs` is registered as an autoload (`GameSession`) to store the currently selected/created save for the running session.
- A debug button in the main menu opens the saves/app-data folder via `OS.ShellOpen`.
