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
│   ├── inventory_ui.tscn      # Inventory/chest UI scene root
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

- `map/map_grid.cs` (attached to `map/map_grid.tscn`, instanced in `level/main_level.tscn`) owns a logical 2D grid (`Width`×`Height`, `CellSize` default 16px) and an occupancy map of `MapObject`s. `main_level.tscn` configures a 200×200 map.
- **Floor:** `map/GrassFloor.cs` builds the floor layer from `assets/images/map_tiles/grass/grass.png` (48×48), tiled across the map.
- **Map objects:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/fences/etc. `TreeFactory` (`map/map_objects/tree/`, scene-backed `town_tree_2.tscn`), `PlantFactory` (`map/map_objects/plant/`, variants 1–3), and `WallFactory` (`map/map_objects/walls/`, scene-backed `stone_wall.tscn`) extend it and can be placed as children of the Y-sorted `MapGrid`. Trees/walls block movement (with collision at the base); plants do not. Sprites are offset so their base aligns to the cell for correct Y-sorting.
- Grid helpers include `CellToWorldCenter`, `CellToWorldBase`, `WorldToCell`, `IsInBounds`, `GetOccupant`, and `TryPlaceOccupant`.
- See [map-architecture.md](./map-architecture.md) for tile-versus-node decisions.

## Enemies

`Enemy.cs` builds `SpriteFrames` at runtime from a configurable 32×32 sprite sheet. It exposes
movement, health, combat, texture, and animation settings. Enemies target the closest player or
allied knight, move toward it, and attack with a timed windup that applies damage at the end of the
attack animation if the target is still in range.

| Scene | Variant | Speed | Health | Attack | Appearance/notes |
| --- | --- | --- | --- | --- | --- |
| `enemy.tscn` | Base | 50 | 3 | 1 dmg, 24 range, 0.8s cooldown | Skeleton |
| `skeleton_enemy.tscn` | Medium | 50 | 3 | 1 dmg, 24 range, 0.8s cooldown | Standard skeleton |
| `elite_skeleton_enemy.tscn` | Hard | 70 | 6 | 2 dmg, 26 range, 0.7s cooldown | Larger red-tinted skeleton |

The temporary slime enemy variant was removed because of broken sprite-sheet animation mapping.
`map.tscn` includes a temporary `EnemyTestSpawner`: press `1` for a skeleton or `2` for an elite
skeleton to spawn additional test enemies at its position. Remove the spawner when no longer
needed.

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
