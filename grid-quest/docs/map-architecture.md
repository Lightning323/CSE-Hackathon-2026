# Map Tile & Item Architecture

Decisions for how Grid Quest represents floor tiles, walls/fences, large props (trees/bridges), and interactive items on a 2D grid (Godot 4.7, C#).

## Core Principle

**Size does not decide representation—behavior does.** A 3x3 visual can be a tile or a node depending on whether it is static map data or a stateful/interactive object.

## Representation Options

### 1. TileMap-backed tiles (`TileMapLayer` + `TileSet`)

- **Use for:** dense, mostly static, grid-aligned content — floor (grass/stone), walls/fences, bridges as terrain, and static decorative/occluding trees.
- **Capabilities:** tiles can be larger than the cell and have collision. A tile texture may span multiple cells, with collision shapes authored in the `TileSet` physics layer (e.g. a 3x3 tree can render large but only its trunk/base footprint cells collide). Rendering/physics are batched for good performance.
- **Notes:** terrain/walkability changes (like a bridge over water) fit naturally as tiles. Large tiles can be split across footprint (collision) and canopy/decor layers for Y-sorting (player passing behind the top).

### 2. Grid-placed nodes/scenes (independent of the tilemap)

- **Use for:** sparse, stateful, or interactive/unique objects — chests, doors, switches, breakable/harvestable trees, anything needing a script, signals, HP, animation, per-instance state, or interaction.
- **Placement:** instantiated as scenes/nodes snapped to grid coordinates and tracked in a grid/occupancy map so systems can query what occupies a cell.
- **Notes:** higher per-instance overhead than tiles but appropriate when instances are sparse and behavior-rich. "Item-like" in behavior (interactive) even if not an inventory item.

## Category Guidance

| Category | Typical representation | Rationale |
| --- | --- | --- |
| Floor (grass, stone, water) | Tile | Static, dense terrain |
| Walls / fences | Tile | Static blockers/collision, dense |
| Bridge | Tile (usually) | Changes terrain/walkability; blends with floors. Node only if stateful (e.g. drawbridge open/close) |
| Tree (static/decor, 3x3) | Tile (or static node) | Static blocker; can be a large/multi-cell tile with trunk collision. Use node if choppable/interactive/has state |
| Chest / door / switch | Node/scene | Interactive, stateful (open/locked/triggered) |
| Small plants/decor on empty cells | Node placed per-cell (current) | Placed only on unoccupied cells and tracked as occupants |

## Current Implementation

- `map/Map.cs` (attached to `map/map.tscn`) procedurally generates the 200×200 runtime map (`MapConstants.CELL_SIZE` = 16px) as node-based content. It builds a `Floor` layer and an `Objects` layer (`Walls`, `Plants`, `Decor`, `Houses`, `Trees`) and also supports placing generated buildings/rooms.
- **Ground & paths:** the base floor is grass everywhere. Village generation and monument approach routes carve path cells rendered with stone path tiles.
- **Procedural villages:** multiple spaced village clumps are generated (with spacing/margin checks to avoid overlap with each other, monument, borders, and reserved/occupied areas) with house placements, connecting paths, and local decoration. Village interiors are selectively reservable so town bushes/plants can decorate non-path, non-occupied ground nicely without overlap; `Map` exposes generated village metadata (`Villages`, `VillageArea` bounds/centers) and helper methods (cell/world conversion, random in-village point, monument position) for gameplay systems.
- **Village-only town trees:** colliding, y-sorted town trees (`town_tree-1.png`, `town_tree-2.png`) spawn only in a ring around village bounds (not as forest trees), placed on valid non-path/non-occupied cells to frame villages without overlap.
- **Monument:** the monument spawns within the camera-reachable map bounds, biased toward center and away from edges, with reserved clearance from villages/objects and cardinal N/S/E/W paths carved to aid discovery. `Monument.cs` marks discovery on proximity/interaction and opens the monument upgrade UI (`menus/SkillsUi.cs`, `skills_ui` group) for coin-purchased player attribute upgrades; `menus/MonumentIndicator.cs` provides an always-on directional arrow (`meta_pickaxe.png`) in level UI.
- **Forest/wilderness decorations:** outside villages (with a small buffer near village bounds for village-specific decor/trees) the generator scatters natural decorations, including blocking y-sorted forest trees, rocks, and trunks (with base collision) plus non-blocking bushes/plants.
- **Buildings/rooms (pulled feature):** `RoomGenerator.cs` builds rectangular rooms from a grid origin with per-edge `Solid`/`Opening`/`Gate` walls, optional floor, and a generated chest inside. `Map.GenerateBuilding(...)`/`GenerateBuildings()` place room/building nodes; generation should avoid conflicting with reserved procedural areas when integrated with villages/monument.
- **Map objects/layering:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/houses/props/gates/chests. Scene-backed factories/objects include `TreeFactory`, `WallFactory`, `HouseFactory`, `GateFactory`, and `Chest`; `PlantFactory` creates lightweight non-blocking plant nodes; `map/map_objects/props/PropFactory.cs` builds configurable decorative props. Border walls are rendered in a non-y-sorted behind layer so player/plants/objects appear over them.
- **Interactive/stateful objects:** `map/map_objects/gate/` provides open/close gate visuals/collision behavior, and `map/map_objects/chest/` provides interactive chest containers (open state, loot generation, inventory transfer via UI).
- **Villagers/knights/enemies integration:** `npc/villager/Villager.cs` supports home-village bounds, return/wander behavior, interaction/speech bubbles with state/context for external dialogue generation, and delayed off-screen respawns via `VillagePopulationManager.cs`. `npc/knight/KnightNpc.cs` supports home-village patrol/protection behavior (prioritizing nearby village threats and staying in-area) with fast off-screen respawns via `VillageKnightPopulationManager.cs` (2 knights per village, 10s respawn). `level/EnemySpawnOnKill.cs` seeds and maintains a higher off-camera monster population across the map (with wander-capable enemies, lower elite-skeleton chance, and faster refill pacing) for denser ambient encounters.
- `map/map_floors/FloorTiles.cs` and `FloorTileFactory.cs` provide reusable floor tile sprites (grass and stone/path).

This remains a pragmatic, node-based generation approach. As content grows, dense static layers (floor, walls/fences, paths, static trees) can migrate to `TileMapLayer`+`TileSet` for batching while interactive/stateful objects remain scene nodes.
