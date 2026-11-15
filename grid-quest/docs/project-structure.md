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
├── entities/
│   ├── base/Entity.cs         # Shared identity, state, inventory, and tools
│   ├── EntityPopulationManager.cs # Spawns generic autonomous villagers
│   ├── LlmEntity.cs           # LLM decision loop and generic character chat
│   ├── SkinCatalog.cs         # Reusable entity skin definitions and sprites
│   ├── llm_entity.tscn        # Generic LLM-driven character scene
│   └── player/
│       ├── PlayerEntity.cs    # Human input and player combat controls
│       ├── PlayerUpgradeDefs.cs # Player upgrade definitions
│       └── player.tscn        # Player scene
├── level/
│   ├── character_dev/main_level.tscn # Character development gameplay scene
│   ├── map_dev/main_level.tscn # Map development gameplay scene
│   ├── DayNightCycle.cs       # World lighting and day progression
│   └── WorldResetManager.cs   # Map reset and entity repopulation
├── items/                     # Item/inventory data model
│   ├── Coin.cs                # World coin pickup
│   ├── Fireball.cs            # Fire staff projectile logic
│   ├── CrossbowBolt.cs        # Crossbow bolt projectile (piercing)
│   ├── ItemDefs.cs            # Item definitions, stacks, and catalog
│   └── InventoryContainer.cs  # Slot container with add/swap/stack helpers
├── map/                       # Map tile definitions, generation, and object factories
│   ├── Map.cs                 # Runtime map composition and building placement
│   ├── MapConstants.cs        # Shared map constants (e.g. cell size)
│   ├── RoomGenerator.cs       # Builds rooms with configurable edges and safe chest placement
│   ├── RoomGenerationContext.cs # Deduplicates shared boundaries and floor tiles
│   ├── BuildingGenerator.cs   # Seedable connected multi-room planning and generation
│   ├── map.tscn               # Reusable map scene
│   ├── map_floors/            # Floor TileMapLayer/TileSet helpers (grass/stone)
│   └── map_objects/
│       ├── MapObjectFactory.cs # Base class for placeable map objects
│       ├── chest/             # Chest object/scene (open state, stored loot)
│       ├── gate/              # Gate/door object and scene
│       ├── plant/             # Plant factory
│       ├── props/             # Decorative/static prop factory
│       ├── tree/              # Tree factory/scene
│       └── walls/             # Wall factory/scene
├── menus/
│   ├── CheatMenu.cs           # Debug/cheat menu
│   ├── InventoryUi.cs         # Inventory + chest UI (drag/drop, move, take-all)
│   ├── InventorySlotUi.cs     # Reusable inventory slot UI control
│   ├── MonumentIndicator.cs   # Directional indicator pointing to the monument
│   ├── SkillsUi.cs            # Monument upgrade menu/UI (purchasable player attributes)
│   ├── VillagerChatUi.cs      # NPC chat interface (ask questions, streamed LLM replies)
│   ├── cheat_menu.tscn        # Debug/cheat menu UI scene
│   ├── inventory_ui.tscn      # Inventory/chest UI scene root
│   ├── skills_ui.tscn         # Monument/skills upgrade UI scene
│   ├── villager_chat_ui.tscn  # Villager chat UI scene
│   ├── main_menu.tscn         # Legacy main menu scene
│   └── mainMenu/
│       ├── MainMenu.cs        # Save selection/creation UI and game boot flow
│       ├── MainMenu.tscn      # Main menu scene (startup scene, UI in scene)
│       ├── SettingsMenu.cs    # LLM settings menu behavior
│       ├── SettingsMenu.tscn  # LLM settings scene (UI in scene)
│       ├── SaveManager.cs     # Save discovery/creation/deletion in app data
│       ├── StoryGenerator.cs   # LLM/fallback campaign story generation and JSON serialization
│       ├── GameGenerationScene.cs # Game generation flow scene behavior
│       ├── GameGenerationScene.tscn # Generation progress UI scene
│       ├── LlmSettingsStore.cs # LLM provider/model/key persistence in app data
│       ├── LlmApi.cs          # Autoload LLM API for in-game requests/tests
│       └── GameSession.cs     # Autoload holding currently selected save

├── game_generator/            # New-game generation flow and UI
│   ├── GameGenerator.cs       # Generator scene behavior (save creation, story generation, progress UI)
│   ├── GameGenerator.tscn     # Generator UI scene (main node)
│   ├── StoryGenerator.cs      # LLM/fallback campaign story generation and JSON serialization
│   ├── AIUtils.cs             # Shared generation/JSON utilities
│   └── llm/
│       ├── LlmApi.cs          # Autoload LLM API (streaming, hooks, provider requests)
│       └── LlmSettingsStore.cs # LLM settings model/persistence in app data
```

## Entity Architecture (`entities/`)

`entities/base/Entity.cs` is the shared abstract root for characters. It owns skin/faction identity, health and combat stats, equipment and inventory, money, persistent memory, chat history, movement intent, world observations, and the common tool registry/executor.

- **`entities/player/PlayerEntity.cs`** extends `Entity` with player movement, human input, combat animation, and upgrade behavior. Setting `UseHumanInput` to `false` routes movement through the shared entity intent.
- **`entities/LlmEntity.cs`** extends `PlayerEntity` so autonomous characters can use the same inventory, combat, and upgrade capabilities. It disables human input and runs a one-action-at-a-time agent loop through `LlmApi`, reacting to startup, interaction, and damage events with a periodic fallback decision.
- **`entities/SkinCatalog.cs`** is the shared skin list and sprite-frame builder. Entity instances select a skin by `SkinId`; villager profiles select skin and system prompt independently of entity behavior.
- **`entities/EntityPopulationManager.cs`** creates generic LLM villagers for each generated village and schedules off-screen respawns.

The base tool list includes observation, movement, stop, attack, interaction, inventory inspection, weapon equip, upgrades, memory, and speech. `GetWorldObservation()` packages the entity state, nearby entities and interactables, time-of-day, and available tools for the LLM.

## Player

`entities/player/player.tscn` uses `entities/player/PlayerEntity.cs`, which builds player animations at runtime based on the equipped weapon:

| Row | Animation | Frames |
| --- | --- | --- |
| 0-2 | idle down / side / up | 6 |
| 3-5 | walk down / side / up | 6 |
| 6-8 | attack down / side / up | 4 |
| 9 | death | 4 |

Movement is 4-directional (`WASD`/arrow keys, controller left stick/D-pad) and is allowed during attacks. Attack: `Space`/`J` or controller **A** (uses the `attack` action). The player camera supports zoom in/out via mouse wheel and the `+` / `-` keys next to Backspace (no Shift), including keypad `+` / `-` (clamped).
Health is shown in the player health bar. Debug cheats (including invincibility toggle) are available via the in-game cheat menu (`menus/CheatMenu.cs`, `` ` `` key) instead of direct gameplay keybinds.
The player can equip sword, fire staff, **scythe** (superior melee: higher base damage/faster attacks, 360° damage around the player), and **crossbow** (superior ranged: higher base damage/faster projectile performance, hold attack to charge and release a heavy piercing bolt from `assets/images/fx/crossbow__bolt.png` that passes through multiple enemies). Equipping scythe/crossbow swaps to weapon-specific player animations.

The player has a slot-based backpack inventory and weapon slot (`items/ItemDefs.cs`, `InventoryContainer.cs`).
Coins collected in-world are tracked as gold in the player's wallet (used for monument upgrades). Coins are not stored as backpack items; coin transfers from chests are converted to wallet immediately, and any legacy coin stacks in the backpack are normalized into the wallet.
`entities/player/PlayerUpgradeDefs.cs` defines monument upgrade definitions/cost scaling, and `PlayerEntity.cs` stores upgrade levels and applies derived combat/movement stats (including acceleration, regeneration, armor, lifesteal, thorns, knockback, projectile/melee upgrades, temporary speed buffs, AOE aura damage/radius, and Midas Touch gold-drop scaling). Coin purchases spend gold and remove coin items from inventory to stay in sync.

## Inventory, Chests, and Items

- `menus/inventory_ui.tscn` + `InventoryUi.cs`/`InventorySlotUi.cs` provide the inventory UI and chest UI.
  - Toggle inventory: `E`/`Tab`/`I` or controller **Y** (`toggle_inventory`); close: same toggle, `Esc`, or controller **B/Start** (`ui_cancel`/`pause_menu`) depending on context.
  - Interact/open a nearby chest, villager, or monument: `F` or controller **X** (`interact`). While the chest menu is open, inventory/close actions (`E`/`Tab`/`I`, `Esc`/`B`, or `F`/`X` when interacting with the open chest) or **Close Chest** close the entire inventory + chest view.
  - Supports click-to-select move/swap and drag-and-drop between backpack, weapon slot, and chest; stacks merge where applicable, and chest loot can be moved with **Take All**. Controller UI navigation uses focus/directional input and **A** (`ui_accept`) to activate/select. The inventory panel also lists **purchased monument attribute upgrades** (name and level) below the backpack slots once acquired, so progression remains visible without spoiling unpurchased upgrades.
  - Coins taken from a chest into the player's inventory (including **Take All**) are consumed from the chest and added directly to the player's coin wallet instead of occupying backpack slots.
- `menus/skills_ui.tscn` + `menus/SkillsUi.cs` provide the monument upgrade UI (`skills_ui` group). It opens from the monument, presents a **roll of 3 random upgrade options** at a time, and requires picking/purchasing one option before showing the next random 3 (uncapped levels; cost increases per level). Closes with `F`/`Esc` or cancel actions.
- `map/map_objects/chest/` defines a stateful chest object (`Chest.cs`, `chest.tscn`) that opens on interaction, generates/stores loot in a 12-slot container for transfer in/out, and supports daily restocking. Chest loot is increased overall but falls off rapidly to nothing after the **third day of the current world spawn** (`level/WorldProgress.cs` loot multiplier by world day). Procedural building/dungeon chests configured for daily restocking refill at sunrise, but their rewards decay with world day progression until nothing after day 3.
- `RoomGenerator` can place room chests selectively (not every room) and add simple procedural room decor. `Map.GenerateBuildings()` places varied procedural buildings (different room counts/sizes) before wilderness decoration.

## Map and Tiles

- `map/Map.cs` (attached to `map/map.tscn`) procedurally generates the 300×300 map at runtime (`MapConstants.CELL_SIZE` = 16px) and composes it into a `Floor` layer and an `Objects` layer (`Walls`, `Plants`, `Decor`, `Houses`, `Trees`), and can place generated buildings/rooms. It also provides a random player spawn point on an unoccupied grass tile within the playable/camera-reachable area.
- **Ground:** the base terrain is a `Floor` `TileMapLayer` generated via `map/map_floors/FloorTileMapFactory.cs`/`FloorTiles.cs` (shared `TileSet` with grass/stone atlas sources, 16x16). Grass is placed everywhere by default; procedural villages and monument approach routes carve stone path tiles (`assets/images/map_tiles/floor/stone.png`).
- **Villages:** generation places a limited number of spaced village clumps fully within the camera-reachable/playable map area (including village bounds, paths, and surrounding town-tree rings), without overlap with each other, the monument, paths/reserved areas, or map borders. `Map` exposes their bounds/centers as `Map.Villages` (`VillageArea`). Each village includes a small path network, 3–4 houses, and naturally decorated interiors with plants and town bushes placed on non-path, non-occupied ground. Houses are scene-backed map objects (`map/map_objects/house/house.tscn`, `HouseFactory.cs`) using `assets/images/map_tiles/cute_House_1_Wood_Base_Blue.png` with collision at the base.
- **Village-only town trees:** colliding, y-sorted town trees (`assets/images/map_tiles/trees/town_tree-1.png`, `town_tree-2.png`) are placed only in a ring around village bounds as village-edge decoration (separate from forest trees), sparsely placed while avoiding paths/objects/reserved areas and overlap, and kept within the camera-reachable map area.
- **Monument:** the stone monument (`map/map_objects/monument/`) spawns at a random location within the camera-reachable map bounds, biased toward the center and kept away from edges, with reserved clearance from villages/objects and cardinal (N/S/E/W) path lines carved for discoverability. `Map` exposes `MonumentWorldPosition`.
- **Forest/wilderness decorations:** outside villages (and a small buffer around village bounds reserved for village decor) the generator scatters natural props including colliding, y-sorted forest trees (orange/pink/dried variants), rocks, and trunks, plus non-blocking forest bushes and ground plants for a natural but relatively sparse terrain. Blocking decor/trees use base-aligned sprites and collision shapes for correct depth and movement.
- **Buildings/rooms, gates, and chests:** `RoomGenerator.cs` builds rectangular rooms with configurable wall edges (`Solid`/`Opening`/`Gate`), optional `TileMapLayer` stone floors, and a chest. `Map.GenerateBuilding(...)`/`GenerateBuildings()` place rooms while avoiding overlap with reserved procedural areas (villages/monument/paths). `RoomGenerationContext` resolves shared walls/gates/openings and deduplicates room floor tile cells. `BuildingGenerator.Plan/Create` produces connected layouts with an exterior entrance, and `Map.GenerateDungeon` validates and reserves their footprint. Gates (`map/map_objects/gate/`) have configurable visuals/collision; chests (`map/map_objects/chest/`) support interaction and inventory transfer. See [map-architecture.md](./map-architecture.md) for generation APIs, ownership, and tests.
- **Map objects/layering:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/houses/props/gates/chests. `TreeFactory`, `WallFactory`, and `HouseFactory` are scene-backed factories; `PlantFactory` creates lightweight non-blocking plant nodes; `map/map_objects/props/PropFactory.cs` creates configurable decorative props (sprite offset, optional collision, y-sort). Border walls are placed in a non-y-sorted `Walls` layer behind y-sorted actors/objects so players, plants, and other objects render over the walls.
- **Map helpers/data:** `Map` provides `CellToWorldCenter`, `CellToWorldBase`, village metadata (`Villages`, `GetRandomPointInVillage`), monument position, and size helpers for other systems.
- `FloorTiles` / `FloorTileMapFactory` (`map/map_floors/`) build/configure floor `TileMapLayer`s and the shared grass/stone `TileSet`.
- See [map-architecture.md](./map-architecture.md) for tile-versus-node decisions.

## Autonomous Villagers

- `entities/EntityPopulationManager.cs` creates three generic `LlmEntity` villagers per generated village. Each profile supplies a name, skin, and system prompt; dead villagers respawn after a delay when their village is off screen.
- `entities/LlmEntity.cs` uses `LlmApi` for an event-driven, one-tool-per-decision loop with a periodic fallback. Observations include nearby entities and interactables, self state, memory, conversation history, and available tools.
- Pressing `F` or the `interact` action near a villager opens `menus/villager_chat_ui.tscn`. The villager's reply is generated from its profile and recent context, then it returns to autonomous decisions.

## Monument, Upgrades, and World Resets

- `map/map_objects/monument/Monument.cs` tracks discovery when the player comes within discovery/interact range and opens the monument UI (`skills_ui`) when the player presses `F` nearby (with basic priority checks to avoid conflicting with chest/villager interactions). `menus/MonumentIndicator.cs` (added to level UI layers) displays a `meta_pickaxe.png` directional arrow that always points toward the monument from the screen center (texture top treated as the pointing direction) and is layered behind gameplay/UI overlays so menus and interfaces appear over it.
- `menus/SkillsUi.cs` presents monument upgrades as a **3-option roll system**: 3 random upgrades are shown and the player must pick/purchase one before the next random 3 are rolled. The current roll persists when closing/reopening the monument until a purchase is made. Upgrades remain uncapped with increasing per-level coin costs. Purchases deduct coins/gold from the player.
- The monument also provides an **always-available "Randomize Map Again"** world-reset option. It rerandomizes/regenerates the map (new layout/villages/buildings/monument placement, respawned world entities, player moved to a valid spawn, time set to sunrise) while keeping player progression (stats, inventory, purchased attribute upgrades/levels, and money). World-reset progress is tracked and shown in the monument UI (resets completed **this run**, current world day, high score/world reached, next cost). The world-reset count/cost scaling is **per-run** (resets on a fresh start) while the high score is persistent. Reset cost starts at **50 coins** and increases by **50** each reset in the current run.
- Available upgrades include: Player Movement Speed, Player Acceleration, Attack Speed, Attack Damage, Maximum Health, Slow Health Regeneration, Health Regeneration Delay, Damage Reduction Armor, Lifesteal, Thorns Damage, Knockback Strength, Projectile Speed, Projectile Damage, Melee Range, Movement Speed After Taking Damage, Movement Speed After Defeating a Target, **AOE Aura** (small visible damaging circle around the player; upgrades slowly increase range and damage), and **Midas Touch** (increases gold drops).

## Day/Night Cycle and Lighting

- `level/DayNightCycle.cs` drives the configurable day-night cycle, updates world-spawn day progression at sunrise, and modulates ambient lighting.
- The player's `PointLight2D` illuminates building interiors and outdoor areas at night. Generated building interiors are darkened for exploration.
- Building chests can restock at sunrise; loot decreases with world-spawn day and reaches zero after day three.

## World Progress and Resets

- `level/WorldProgress.cs` tracks per-run resets and world days, and persists the highest world reached.
- `level/WorldResetManager.cs` regenerates the map while preserving player progression, then asks `EntityPopulationManager` to repopulate its villages.

## Notes
 
- `.godot/` is generated by the editor and ignored by git; do not edit it by hand.
- `.import` files are generated by Godot on asset import and should be committed.
- The Cute Fantasy asset pack is licensed for **non-commercial** use; see
  `assets/docs/Cute_Fantasy_Free_read_me.txt`.
- Scenes use Godot 4.7 scene format (`uid=` attributes are managed by the editor).
- Custom C# scripts target .NET 8.


## Main Menu and Saves

- `menus/mainMenu/MainMenu.tscn` is the startup scene (`project.godot` `run/main_scene`) containing the menu UI layout; `menus/mainMenu/MainMenu.cs` handles behavior/wiring.
- **Current simplified flow:** the main menu displays the title image (`assets/images/everVenture_titlesign.png`), provides **Start Game** and **Settings**, and shows the persistent **World Reset High Score** (record of how far the player got/world number reached). Save list/selection, New Save, Delete, and game/story generation are not exposed in the UI (generation/save code remains in `game_generator/`, `menus/mainMenu/SaveManager.cs`, `menus/mainMenu/GameSession.cs` but is kept out of the active flow for now).
- **Start Game** creates/reuses a temporary session save behind the scenes (`OS.GetUserDataDir()/saves/temp_session`) via `SaveManager.GetOrCreateTemporarySave()`, stores it in the `GameSession` autoload, and boots into `res://level/character_dev/main_level.tscn`. The world-reset high score is persisted separately (`level/WorldProgress.cs`), counted even if the player dies on that world/reset number, and updates only when a higher number is reached. World reset count/cost scaling is per-run.
- **In-game menu (Pause/Game Over):** `menus/game_over_menu.tscn` + `menus/GameOverMenu.cs` (added to the level UI) pauses gameplay and provides **Restart**, **Controls** (in-menu bindings reference), and **Exit to Main Menu**. It opens as "Paused" when `Esc` or controller **Start** (`pause_menu`) is pressed in-game (toggles closed the same way, unless other modal UI is open; if the Controls view is open, back/close returns to the pause menu first) and opens as "Game Over" after the player death animation completes. After clicking **Start Game** from the main menu, the level loads with the **Controls** screen shown by default (paused); closing/back from that initial controls view resumes gameplay.

## Main Menu Settings and LLM API

- `menus/mainMenu/SettingsMenu.tscn` (UI in scene) and `menus/mainMenu/SettingsMenu.cs` provide LLM configuration: provider selection (OpenAI, Anthropic, LM Studio, Ollama, Custom OpenAI-compatible), base URL, model, API key, and an **Enable thinking** option (disabled by default). Settings also includes **Clear Data** to reset the saved world-reset high score.
- **Test Model** pings the configured provider/model and reports connection/status in the settings UI.
- `game_generator/llm/LlmSettingsStore.cs` persists LLM settings (including `EnableThinking`) to app data (`OS.GetUserDataDir()/llm_settings.json`).
- `game_generator/llm/LlmApi.cs` is registered as an autoload (`LlmApi`) and exposes async methods (`ChatAsync`, `TestConnectionAsync`) for direct in-Godot LLM use (OpenAI-compatible chat completions, Anthropic messages, and Ollama `/api/generate`). `ChatAsync` supports streaming with intermediate chunk output: prints chunks to terminal as they arrive via `GD.PrintRaw`/`GD.Print` and exposes optional hooks (`onChunk` callback parameter and `StreamChunkReceived` event) so callers can consume streamed data incrementally. Story/game generation uses these hooks during LLM generation.

## UI Theme

- `assets/main_theme.tres` is the shared UI theme and uses `assets/fonts/pixel_operator/PixelOperator.ttf` as the default font.
- Menu/UI scenes (including `menus/mainMenu/MainMenu.tscn`, `menus/mainMenu/SettingsMenu.tscn`, inventory/skills menus) apply `res://assets/main_theme.tres` on their root controls.

## Audio

- `assets/audio/music/` and `assets/audio/sfx/` contain imported audio resources (`.ogg/.mp3/.wav`).
- **Main level BGM:** `level/character_dev/MainLevel.cs` starts `res://assets/audio/sfx/forest_ancient_path.ogg` on a looping `AudioStreamPlayer` in the main gameplay level.
- **Gameplay SFX:**
  - Player hurt: `entities/player/PlayerEntity.cs` plays `res://assets/audio/sfx/forest_hurt.ogg` on damage (non-overlapping while already playing).
  - Coin pickup: `items/Coin.cs` plays `res://assets/audio/sfx/forest_item.ogg` on collection (overlapping allowed).
  - Sword attack: `entities/player/PlayerEntity.cs` plays `res://assets/audio/sfx/forest_slash.ogg` when using a sword/melee attack (not fire staff).


## Tests

- `tests/generation_tests.tscn` + `tests/GenerationTests.cs`: procedural generation/integration checks (run headless with `godot --headless --path . res://tests/generation_tests.tscn`).
- `tests/llm_hello_test.tscn` + `tests/LlmHelloTest.cs`: loads saved LLM settings from `OS.GetUserDataDir()/llm_settings.json` and uses `LlmApi`/`StoryGenerator` to run an LLM generation test, printing output to the terminal then exiting (run headless with `godot --headless --path . res://tests/llm_hello_test.tscn`).
- `tests/llm_hello_world_test.tscn` + `tests/LlmHelloWorldTest.cs`: simple streaming "hello world" test using saved LLM settings; prints streamed chunks as they arrive, reports chunk count/final response, and exits nonzero if no chunks are received (run headless with `godot --headless --path . res://tests/llm_hello_world_test.tscn`).
