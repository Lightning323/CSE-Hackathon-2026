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
├── map/                       # Map tile definitions, grid scene/script, and object classes
│   ├── map_grid.cs            # Grid/occupancy and map composition (attached script)
│   ├── map_grid.tscn          # Reusable MapGrid scene
│   ├── GrassFloor.cs
│   └── map_objects/
│       ├── MapObject.cs       # Base class for walls/trees/bushes/fences/etc.
│       ├── TownTree2.cs
│       └── TownPlant.cs
├── menus/
│   └── main_menu.tscn         # Main menu
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

Movement is 4-directional with WASD or arrow keys and is allowed during attacks. Space attacks;
the player can damage enemies and knights. Health is shown in the player health bar. `H` toggles
temporary debug invulnerability (remove when no longer needed).

## Map and Tiles

- `map/Map.cs` (attached to `map/map.tscn`) procedurally generates the 200×200 map at runtime (`MapConstants.CELL_SIZE` = 16px) and composes it into a `Floor` layer and an `Objects` layer (`Walls`, `Plants`, `Decor`, `Houses`, `Trees`).
- **Ground:** the base terrain is grass everywhere (`assets/images/map_tiles/floor/grass.png`). Procedural villages and monument approach routes carve stone path tiles (`assets/images/map_tiles/floor/stone.png`).
- **Villages:** generation places several spaced village clumps and exposes their bounds/centers as `Map.Villages` (`VillageArea`). Each village includes a small path network, 3–4 houses, reserved/clear areas, and local decorations (plants and town-bush props). Houses are scene-backed map objects (`map/map_objects/house/house.tscn`, `HouseFactory.cs`) using `assets/images/map_tiles/cute_House_1_Wood_Base_Blue.png` with collision at the base.
- **Monument:** the stone monument (`map/map_objects/monument/`) spawns at a random location within the camera-reachable map bounds, biased toward the center and kept away from edges, with reserved clearance from villages/objects and cardinal (N/S/E/W) path lines carved for discoverability. `Map` exposes `MonumentWorldPosition`.
- **Forest/wilderness decorations:** outside village/reserved areas the generator scatters natural props including colliding, y-sorted forest trees (orange/pink/dried variants), rocks, and trunks, plus non-blocking forest bushes and ground plants for denser, more natural terrain. Blocking decor/trees use base-aligned sprites and collision shapes for correct depth and movement.
- **Map objects/layering:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/houses/props. `TreeFactory`, `WallFactory`, and `HouseFactory` are scene-backed factories; `PlantFactory` creates lightweight non-blocking plant nodes; `map/map_objects/props/PropFactory.cs` creates configurable decorative props (sprite offset, optional collision, y-sort). Border walls are placed in a non-y-sorted `Walls` layer behind y-sorted actors/objects so players, plants, and other objects render over the walls.
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

`level/EnemySpawnOnKill.cs` performs occasional off-camera monster spawns (with a population cap and refill when empty), favoring normal skeletons over elite skeletons. `level/character_dev/EnemyTestSpawner.cs` remains a temporary manual test spawner (keys `1`/`2`). Remove temporary spawners when no longer needed.

## Allied Knight NPC

`npc/knight/KnightNpc.cs` builds `SpriteFrames` at runtime from the `forest_hero` character
sprites under `assets/images/characters/`. Knights are allied helpers: they target and attack
enemies, can take damage, and regenerate slowly after a delay out of combat. No knights spawn by
default; press `G` to spawn one near the player. The player can also damage knights.

## Notes

- `.godot/` is generated by the editor and ignored by git; do not edit it by hand.
- `.import` files are generated by Godot on asset import and should be committed.
- The Cute Fantasy asset pack is licensed for **non-commercial** use; see
  `assets/docs/Cute_Fantasy_Free_read_me.txt`.
- Scenes use Godot 4.7 scene format (`uid=` attributes are managed by the editor).
- Custom C# scripts target .NET 8.
