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

- `map/Map.cs` (attached to `map/map.tscn`) procedurally generates the runtime map (`MapConstants.CELL_SIZE` = 16px). It builds a `Floor` `TileMapLayer` (grass/stone) and an `Objects` node layer (`Walls`, `Plants`, `Decor`, `Houses`, `Trees`) and also supports placing generated buildings/rooms.
- **Ground & paths (TileMapLayer):** the base floor is rendered via a runtime `TileMapLayer` + `TileSet` (`map/map_floors/FloorTileMapFactory.cs`, helpers in `FloorTiles.cs`) using atlas sources for grass and stone (`assets/images/map_tiles/floor/*.png`, 16x16). Base terrain is grass everywhere; village generation and monument approach routes carve path cells as stone tiles.
- **Procedural villages:** a limited number of spaced village clumps are generated fully within the camera-reachable/playable map area (including bounds, local paths, and town-tree rings), with spacing/margin checks to avoid overlap with each other, monument, borders, and reserved/occupied areas. Villages include house placements, connecting paths, and local decoration. Village interiors are selectively reservable so town bushes/plants can decorate non-path, non-occupied ground nicely without overlap; `Map` exposes generated village metadata (`Villages`, `VillageArea` bounds/centers) and helper methods (cell/world conversion, random in-village point, monument position) for gameplay systems.
- **Village-only town trees:** colliding, y-sorted town trees (`town_tree-1.png`, `town_tree-2.png`) spawn only in a ring around village bounds (not as forest trees), placed on valid non-path/non-occupied cells to frame villages without overlap and kept within the camera-reachable map area.
- **Monument:** the monument spawns within the camera-reachable map bounds, biased toward center and away from edges, with reserved clearance from villages/objects and cardinal N/S/E/W paths carved to aid discovery. `Monument.cs` marks discovery on proximity/interaction and opens the monument upgrade UI (`menus/SkillsUi.cs`, `skills_ui` group) for coin-purchased player attribute upgrades; `menus/MonumentIndicator.cs` provides an always-on directional arrow (`meta_pickaxe.png`) in level UI.
- **Forest/wilderness decorations:** outside villages (with a small buffer near village bounds for village-specific decor/trees) the generator scatters natural decorations, including blocking y-sorted forest trees, rocks, and trunks (with base collision) plus non-blocking bushes/plants.
- **Buildings/rooms:** `RoomGenerator.cs` builds rectangular rooms from a grid origin with per-edge `Solid`/`Opening`/`Gate` walls, optional `Floor` `TileMapLayer` (stone), and a generated chest inside. Generated rooms are darkened (interior visuals modulated) so building interiors remain dark while staying readable with player lighting. `BuildingGenerator` plans connected multi-room layouts and `Map.GenerateDungeon(...)` places them; `Map.GenerateBuilding(...)` retains single-room placement. Buildings are reserved before wilderness decoration; generation should avoid conflicting with reserved procedural areas when integrated with villages/monument. `Map` tracks placed building interior rects and provides indoor-position checks used by lighting/gameplay logic.
- **Map objects/layering:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/houses/props/gates/chests. Scene-backed factories/objects include `TreeFactory`, `WallFactory`, `HouseFactory`, `GateFactory`, and `Chest`; `PlantFactory` creates lightweight non-blocking plant nodes; `map/map_objects/props/PropFactory.cs` builds configurable decorative props. Border walls are rendered in a non-y-sorted behind layer so player/plants/objects appear over them. Wall objects are grouped as `walls` (used as projectile blockers for player projectiles) while trees remain pass-through for projectiles.
- **Interactive/stateful objects:** `map/map_objects/gate/` provides configurable open/closed gate visuals and collision, and `map/map_objects/chest/` provides interactive chest containers (open state, loot generation, inventory transfer via UI).
- **Knights/enemies integration:** `npc/Knight.cs` supports home-village patrol/protection behavior (prioritizing nearby village threats and staying in-area) with fast off-screen respawns via `npc/VillageKnightPopulationManager.cs` (2 knights per village, 10s respawn). `level/EnemySpawnOnKill.cs` seeds and maintains an off-camera monster population across the map for ambient encounters.
- `map/map_floors/FloorTileMapFactory.cs` and `FloorTiles.cs` provide the shared floor `TileSet`/`TileMapLayer` helpers (grass and stone/path).

Floor terrain is now `TileMapLayer`-based for dense static cells, while interactive/stateful and sparse decorative objects remain scene nodes. Other dense static layers (e.g. walls/fences/static trees) can migrate to `TileMapLayer`+`TileSet` as needed.


## Shared rooms and procedural buildings

Room origins are grid cells in the containing map's coordinate space. Dimensions **include**
the perimeter: a room at `(x, y)` of size `(w, h)` occupies cells through
`(x + w - 1, y + h - 1)`. Adjacent aligned rooms use a stride of `w - 1` or
`h - 1`, sharing one boundary row/column. Floor tiles use top-left pixel positions;
walls, gates, and chests use the existing cell-base factories with `CELL_SIZE`.
All room/building roots remain at zero position and retain Y-sorting through their ancestors.

`RoomGenerator.Create(...)` retains its existing arguments and adds an optional final
`RoomGenerationContext`. Pass the **same context** to every room sharing geometry:

```csharp
var context = new RoomGenerationContext();
var rooms = new Node2D { YSortEnabled = true };
rooms.AddChild(RoomGenerator.Create(10, 10, 8, 7, east: WallFeature.Gate,
    gatesOpen: true, includeFloor: true, context: context));
rooms.AddChild(RoomGenerator.Create(17, 10, 8, 7, west: WallFeature.Gate,
    gatesOpen: true, includeFloor: true, context: context));
AddChild(rooms);
```

The context records empty openings as well as nodes. One cell resolves to at most one
wall/gate, using **Gate > Opening > Solid** regardless of creation order. Two gates
must agree on axis and open state. Every corner stays solid; a doorway overlapping
another room's corner raises an exception instead of silently sealing the doorway.
Interior overlap is rejected. Validation precedes geometry changes. If a later room
replaces a solid wall with a doorway, the old node is detached immediately and freed
at frame end. Unchanged boundary nodes and deduplicated floor tiles remain owned by
the room that first created them; treat the entire building as one lifetime/transform
unit. Do not individually remove, translate, or reuse rooms/context after freeing them.
Separate contexts and unrelated pre-existing scene nodes are not deduplicated.

Standalone room dimensions must be at least 3×3. Rooms smaller than 5 cells on either
axis omit the chest because it could block their only path. Larger rooms place their
chest at interior offset `(1, 1)`, away from the centered doorway routes. North/south
gates use the horizontal axis; east/west gates use the vertical axis. Gate `_Ready`
reapplies configuration after the base factory initializes movement metadata.

`BuildingGenerator.Plan(roomCount, roomWidth: 8, roomHeight: 7, seed: 42)` returns
an immutable layout with exact room count, local cell rectangles, per-edge features,
and bounding `Size`, without instantiating scenes. A random frontier grows unique
lattice slots, connecting each new room to its parent. This guarantees a connected
spanning tree without a random retry limit; incidental neighboring rooms retain
solid partitions. Layouts normalize to nonnegative coordinates. Rooms have uniform
sizes per building (minimum 5×5); odd and even dimensions both work. Seeded plans
are reproducible for this generator and Godot version. One exposed north edge is the
exterior entrance. `connection` selects `Opening` (default) or `Gate`; solid connections
and nonpositive counts are rejected. No upper room-count cap is imposed; callers
must choose counts appropriate for node-based generation and available memory.

```csharp
// Standalone building in the parent's grid space:
AddChild(BuildingGenerator.Create(12, originX: 10, originY: 10, seed: 42));

// Runtime Map: plan once, validate/reserve its footprint, then instantiate that plan.
BuildingLayout layout = BuildingGenerator.Plan(5, seed: 42);
Node2D dungeon = map.GenerateDungeon(10, 10, layout); // null if the site is occupied
```

Building connections and entrance default to openings. Gates default to open when
building via `BuildingGenerator`; explicitly choosing `gatesOpen: false` creates
closed physical barriers until gameplay calls `GateFactory.Configure`. There is no
automatic gate interaction or unlocking system. Existing standalone room defaults
remain closed. The runtime map tries to place one five-room dungeon in the
camera-reachable region, then its existing single rooms. Placement may be skipped
when no clear site fits. It conservatively reserves the full bounding rectangle,
including unused slots and a one-cell exterior margin, before decorating wilderness.
It avoids villages, monument reservations, paths, and occupied cells. Reserved and
occupied building cells are placement exclusions, not a fine-grained navigation map.

### Verification

Run `dotnet build`, then run Godot with
`--headless --path . res://tests/generation_tests.tscn`.
The scene exits nonzero on failure. Tests exercise both shared-wall orientations and
creation orders, all solid/opening/gate combinations, duplicate floor suppression,
four-room corners, invalid overlaps/features/counts/dimensions, conflicting gate
state, deterministic seeds, exact room counts, open-gate collision after `_Ready`,
and cell-level reachability on instantiated geometry (including chest blockers)
across 120 seeded building layouts, plus runtime map placement and decoration exclusions. This does not substitute for visual playtesting
or a player-sized physics traversal test.
