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

- `map/` contains one C# file per map tile/item type defining its texture/path and node/layer factory (e.g. `GrassFloor`, `TownTree2`, `TownPlant1..3`) plus the grid script/scene.
- `map/map_grid.cs` is attached as an external script to the `MapGrid` node (`map/map_grid.tscn`, instanced/used in `level/map.tscn`). It owns the logical grid (`Width`×`Height`, `CellSize`), an occupancy map, and composes layers by delegating to `map/*` factories: `Floor` (grass), `Objects` (collidable tree at center, occupying its cell), and `Items` (random plants placed only on empty cells).
- The grid exposes helpers for future systems: `CellToWorldCenter`, `WorldToCell`, `IsInBounds`, `GetOccupant`, `TryPlaceOccupant`.

This is a pragmatic, node-based/grid-occupancy approach. As content grows, static/dense layers (floor, walls/fences, bridges, static trees) can migrate to `TileMapLayer`+`TileSet` for batching while interactive objects remain grid-placed nodes tracked in occupancy.
