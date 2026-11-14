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
│       ├── tree/              # Tree factory/scene
│       └── walls/             # Wall factory/scene
├── menus/
│   ├── CheatMenu.cs           # Debug/cheat menu (invincibility, add coins, add weapons)
│   ├── InventoryUi.cs         # Inventory + chest UI (drag/drop, move, take-all)
│   ├── InventorySlotUi.cs     # Reusable inventory slot UI control
│   ├── MonumentIndicator.cs   # Directional indicator pointing to the monument
│   ├── SkillsUi.cs            # Monument upgrade menu/UI (purchasable player attributes)
│   ├── cheat_menu.tscn        # Debug/cheat menu UI scene
│   ├── inventory_ui.tscn      # Inventory/chest UI scene root
│   ├── skills_ui.tscn         # Monument/skills upgrade UI scene
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
├── npc/
│   └── knight/
│       ├── KnightNpc.cs       # Allied knight AI, combat, and regeneration
│       └── knight_npc.tscn    # Knight character scene
└── player/
	├── Player.cs              # Player movement, combat, health, upgrades, and animations
	├── PlayerUpgradeDefs.cs   # Monument upgrade definitions and cost scaling
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
the player can damage enemies and knights. Health is shown in the player health bar. Debug cheats (including invincibility toggle) are available via the in-game cheat menu (`menus/CheatMenu.cs`, `` ` `` key) instead of direct gameplay keybinds.

The player has a slot-based backpack inventory and weapon slot (`items/ItemDefs.cs`, `InventoryContainer.cs`).
Coins collected in-world are tracked as gold in the player's wallet (used for monument upgrades). Coins are not stored as backpack items; coin transfers from chests are converted to wallet immediately, and any legacy coin stacks in the backpack are normalized into the wallet.
`player/PlayerUpgradeDefs.cs` defines monument upgrade definitions/cost scaling, and `Player.cs` stores upgrade levels and applies derived combat/movement stats (including acceleration, regen/delay, armor, lifesteal, thorns, knockback, projectile/melee upgrades, temporary speed buffs, and boss bonus damage hooks). Coin purchases spend gold and remove coin items from inventory to stay in sync.

## Inventory, Chests, and Items

- `menus/inventory_ui.tscn` + `InventoryUi.cs`/`InventorySlotUi.cs` provide the inventory UI and chest UI.
  - Toggle inventory: `E`/`Tab`/`I` (controller `Y`); close: `Esc` or toggle key.
  - Interact/open nearby chest: `F` (opens chest and shows chest panel alongside inventory). While the chest menu is open, `E`/`F`/`Esc` (or Close Chest) close the entire inventory + chest view.
  - Supports click-to-select move/swap and drag-and-drop between backpack, weapon slot, and chest; stacks merge where applicable, and chest loot can be moved with **Take All**.
  - Coins taken from a chest into the player's inventory (including **Take All**) are consumed from the chest and added directly to the player's coin wallet instead of occupying backpack slots.
- `menus/skills_ui.tscn` + `menus/SkillsUi.cs` provide the monument upgrade UI (`skills_ui` group), listing all player attribute upgrades with current level, coin cost, and purchase actions. It opens from the monument and closes with `F`/`Esc`.
- `map/map_objects/chest/` defines a stateful chest object (`Chest.cs`, `chest.tscn`) that opens on first interaction, generates a random dynamic loot pool once, and stores items in a 12-slot container for transfer in/out.
- `RoomGenerator` spawns one chest in rooms at least 5×5; smaller rooms omit it to keep paths clear. `Map.GenerateBuildings()` places a procedural dungeon and single rooms before wilderness decoration.

## Map and Tiles

- `map/Map.cs` (attached to `map/map.tscn`) procedurally generates the 200×200 map at runtime (`MapConstants.CELL_SIZE` = 16px) and composes it into a `Floor` layer and an `Objects` layer (`Walls`, `Plants`, `Decor`, `Houses`, `Trees`), and can place generated buildings/rooms. It also provides a random player spawn point on an unoccupied grass tile within the playable/camera-reachable area.
- **Ground:** the base terrain is a `Floor` `TileMapLayer` generated via `map/map_floors/FloorTileMapFactory.cs`/`FloorTiles.cs` (shared `TileSet` with grass/stone atlas sources, 16x16). Grass is placed everywhere by default; procedural villages and monument approach routes carve stone path tiles (`assets/images/map_tiles/floor/stone.png`).
- **Villages:** generation places a limited number of spaced village clumps fully within the camera-reachable/playable map area (including village bounds, paths, and surrounding town-tree rings), without overlap with each other, the monument, paths/reserved areas, or map borders. `Map` exposes their bounds/centers as `Map.Villages` (`VillageArea`). Each village includes a small path network, 3–4 houses, and naturally decorated interiors with plants and town bushes placed on non-path, non-occupied ground. Houses are scene-backed map objects (`map/map_objects/house/house.tscn`, `HouseFactory.cs`) using `assets/images/map_tiles/cute_House_1_Wood_Base_Blue.png` with collision at the base.
- **Village-only town trees:** colliding, y-sorted town trees (`assets/images/map_tiles/trees/town_tree-1.png`, `town_tree-2.png`) are placed only in a ring around village bounds as village-edge decoration (separate from forest trees), avoiding paths/objects/reserved areas and overlap, and kept within the camera-reachable map area.
- **Monument:** the stone monument (`map/map_objects/monument/`) spawns at a random location within the camera-reachable map bounds, biased toward the center and kept away from edges, with reserved clearance from villages/objects and cardinal (N/S/E/W) path lines carved for discoverability. `Map` exposes `MonumentWorldPosition`.
- **Forest/wilderness decorations:** outside villages (and a small buffer around village bounds reserved for village decor) the generator scatters natural props including colliding, y-sorted forest trees (orange/pink/dried variants), rocks, and trunks, plus non-blocking forest bushes and ground plants for denser, more natural terrain. Blocking decor/trees use base-aligned sprites and collision shapes for correct depth and movement.
- **Buildings/rooms, gates, and chests:** `RoomGenerator.cs` builds rectangular rooms with configurable wall edges (`Solid`/`Opening`/`Gate`), optional `TileMapLayer` stone floors, and a chest. `Map.GenerateBuilding(...)`/`GenerateBuildings()` place rooms while avoiding overlap with reserved procedural areas (villages/monument/paths). `RoomGenerationContext` resolves shared walls/gates/openings and deduplicates room floor tile cells. `BuildingGenerator.Plan/Create` produces connected layouts with an exterior entrance, and `Map.GenerateDungeon` validates and reserves their footprint. Gates (`map/map_objects/gate/`) have configurable visuals/collision; chests (`map/map_objects/chest/`) support interaction and inventory transfer. See [map-architecture.md](./map-architecture.md) for generation APIs, ownership, and tests.
- **Map objects/layering:** `map/map_objects/MapObjectFactory.cs` is the base for walls/trees/bushes/houses/props/gates/chests. `TreeFactory`, `WallFactory`, and `HouseFactory` are scene-backed factories; `PlantFactory` creates lightweight non-blocking plant nodes; `map/map_objects/props/PropFactory.cs` creates configurable decorative props (sprite offset, optional collision, y-sort). Border walls are placed in a non-y-sorted `Walls` layer behind y-sorted actors/objects so players, plants, and other objects render over the walls.
- **Map helpers/data:** `Map` provides `CellToWorldCenter`, `CellToWorldBase`, village metadata (`Villages`, `GetRandomPointInVillage`), monument position, and size helpers for other systems.
- `FloorTiles` / `FloorTileMapFactory` (`map/map_floors/`) build/configure floor `TileMapLayer`s and the shared grass/stone `TileSet`.
- See [map-architecture.md](./map-architecture.md) for tile-versus-node decisions.

## Villagers

- `npc/villager/Villager.cs` builds villager animations at runtime and runs simple idle/patrol/flee AI. Villagers target-threat flee from enemies, can take damage, and prefer to stay within/near their assigned home village (`SetHome`) by wandering locally and returning when displaced.
- `npc/villager/VillagePopulationManager.cs` spawns a configurable number of villagers per generated village at startup and respawns villagers slowly after death when the village area is not visible to the active camera. Manual spawn inputs (`level/character_dev/VillagerSpawnInput.cs`) can also place/assign villagers to generated villages.
- **Interaction & dialogue bubbles:** villagers display a dynamically sized speech bubble above their head (`npc/villager/villager.tscn`) with placeholder dialogue for `Idle`, `Chased`, and `Recovered` (done being chased/catching breath) states. Bubbles auto-show/update on state changes (including while chased and briefly after fleeing ends) and the player can interact with the nearest nearby villager using `F` to refresh/show context-appropriate dialogue.
- **AI-ready context:** `Villager` exposes dialogue/emotional state and runtime context for future external dialogue generation via `CurrentDialogueEmotion`, `CurrentDialogueText`, and `GetDialogueContext()` (state/emotion, fleeing/recovery/threat/home/player proximity, health, current dialogue, and placeholder options per state).

## Monument & Upgrades

- `map/map_objects/monument/Monument.cs` tracks discovery when the player comes within discovery/interact range and opens the monument upgrade UI (`skills_ui`) when the player presses `F` nearby (with basic priority checks to avoid conflicting with chest/villager interactions). `menus/MonumentIndicator.cs` (added to level UI layers) displays a `meta_pickaxe.png` directional arrow that always points toward the monument from the screen center (texture top treated as the pointing direction).
- `menus/SkillsUi.cs` presents purchasable, uncapped attribute upgrades priced in coins (cheap starting costs with increasing per-level costs). Available upgrades include: Player Movement Speed, Player Acceleration, Attack Speed, Attack Damage, Maximum Health, Slow Health Regeneration, Health Regeneration Delay, Damage Reduction Armor, Lifesteal, Thorns Damage, Knockback Strength, Projectile Speed, Projectile Damage, Melee Range, Movement Speed After Taking Damage, Movement Speed After Killing an Enemy, and Bonus Damage to Bosses. Purchases deduct coin items/gold from the player.

## Enemies

`Enemy.cs` builds `SpriteFrames` at runtime from a configurable 32×32 sprite sheet. It exposes
movement, health, combat, texture, and animation settings. When not aggroed, enemies idle/wander around locally; when a target (player, allied knight, or villager) is within agro range they chase and attack with a timed windup that applies damage at the end of the attack animation if the target is still in range. Horde-spawned enemies can be flagged to force-chase the player.

| Scene | Variant | Speed | Health | Attack | Appearance/notes |
| --- | --- | --- | --- | --- | --- |
| `enemy.tscn` | Base | 50 | 3 | 1 dmg, 24 range, 0.8s cooldown | Skeleton |
| `skeleton_enemy.tscn` | Medium | 50 | 3 | 1 dmg, 24 range, 0.8s cooldown | Standard skeleton |
| `elite_skeleton_enemy.tscn` | Hard | 70 | 6 | 2 dmg, 26 range, 0.7s cooldown | Larger red-tinted skeleton |

`level/EnemySpawnOnKill.cs` performs off-camera monster spawning with an initial population seed, a maintained minimum population, and a higher cap to keep many monsters roaming the map. Spawns favor normal skeletons over elite skeletons and run in gameplay levels (including the main character dev level). Base spawn pressure is tuned higher, with additional night-time scaling and survival-time difficulty scaling that increases enemy counts over time (capped, without increasing mob health). The spawner can also trigger random night-weighted hordes (groups of base skeletons) that target the player. `level/character_dev/EnemyTestSpawner.cs` remains a temporary manual test spawner if attached/scripted for dev use. Remove temporary spawners when no longer needed.

Enemy combat integrates with player upgrade effects: `Enemy.cs` supports knockback (`ApplyKnockback`) and passes attacker context on player damage so thorns can trigger. `items/Fireball.cs` uses upgraded projectile speed/damage/knockback and notifies the player on hits for on-hit effects (e.g. lifesteal / kill-speed hooks); boss bonus damage applies to targets in `boss`/`bosses` groups or with an `IsBoss` property. Player projectiles are stopped by wall objects (`walls` group, e.g. border/building stone walls) but pass through trees/other non-wall obstacles.

## Allied Knight NPC

`npc/knight/KnightNpc.cs` builds `SpriteFrames` at runtime from the `forest_hero` character
sprites under `assets/images/characters/`. Knights are allied village protectors: they target and attack nearby monsters threatening their village, can take damage, regenerate slowly after a delay out of combat, and can be assigned a home village (`SetHome`) to patrol/return near and stay in the village area.
- `npc/knight/VillageKnightPopulationManager.cs` spawns 2 knights per generated village at startup (within village bounds) and respawns them quickly (10s) when the village area is not visible to the camera to maintain the garrison.
- Manual spawn input (`G`) can still spawn a knight near the player (dev/testing). The player can also damage knights.

## Day/Night Cycle and Lighting

- `level/DayNightCycle.cs` (attached to level `CanvasModulate` nodes, group `day_night_cycle`) drives a continuous day-night cycle over a configurable duration (default 300s full cycle).
- The cycle tints the world via `CanvasModulate` with smooth gradient transitions: deep night, brighter-yellow sunrise, full day, orangish sunset, and back to night.
- `level/EnemySpawnOnKill.cs` queries the active day/night cycle to apply increased enemy pressure at night (higher effective min/max population and spawn pacing/chance scaling), in addition to time-survival difficulty scaling and night-weighted horde spawns.
- Generated building/room interiors are visually darkened (`RoomGenerator` room modulate) so buildings remain dark. `Map` tracks building interior rects and exposes `IsWorldPositionInsideBuilding(...)` for indoor checks.
- The player's `PointLight2D` is used for exploration lighting: it turns on inside buildings (with brighter/wider indoor light for visibility), turns on outdoors around sunset/night when ambient daylight is low, and turns off outdoors during the day (around sunrise/daylight).

## Survival Time and Difficulty

- `level/SurvivalStats.cs` (group `survival_stats`, added to gameplay levels) tracks elapsed survival time and provides a capped difficulty multiplier used to increase enemy counts over time (without changing mob health).
- `menus/SurvivalTimerUi.cs` displays a small unobtrusive survival timer in the top-left HUD (CanvasLayer), formatted dynamically as seconds, minutes, or hours as time progresses (no milliseconds).
- `level/EnemySpawnOnKill.cs` uses survival time and day/night state to scale ambient spawn pressure and to trigger random hordes (groups of base skeletons, primarily at night) that target the player.

## Notes
 
- `.godot/` is generated by the editor and ignored by git; do not edit it by hand.
- `.import` files are generated by Godot on asset import and should be committed.
- The Cute Fantasy asset pack is licensed for **non-commercial** use; see
  `assets/docs/Cute_Fantasy_Free_read_me.txt`.
- Scenes use Godot 4.7 scene format (`uid=` attributes are managed by the editor).
- Custom C# scripts target .NET 8.


## Main Menu and Saves

- `menus/mainMenu/MainMenu.tscn` is the startup scene (`project.godot` `run/main_scene`) containing the menu UI layout; `menus/mainMenu/MainMenu.cs` handles behavior/wiring.
- The main menu lists save folders from the app data saves directory (`OS.GetUserDataDir()/saves`), and supports **Play**, **Delete**, **New Save**, and **Settings**.
- **New Save** opens a dialog for title and description, then transitions to `game_generator/GameGenerator.tscn`, which shows a progress bar and status descriptions while creating the save folder and generating `story.json` via `StoryGenerator`/`LlmApi` (with fallback if unavailable), then returns to the main menu and selects the new save (it does not automatically boot into the game).
- `game_generator/StoryGenerator.cs` generates campaign/story JSON (campaign metadata + progression stages/objectives/rewards/modifiers) using `LlmApi` when available and writes the schema-compatible `story.json` for the new save. `GameSession` carries pending new-save inputs and the last created save ID across the generation scene transition.
- **Play** sets the selected save as the active session save and boots into `res://level/character_dev/main_level.tscn`.
- `menus/mainMenu/GameSession.cs` is registered as an autoload (`GameSession`) to store the currently selected/created save for the running session.
- A debug button in the main menu opens the saves/app-data folder via `OS.ShellOpen`.

## Main Menu Settings and LLM API

- `menus/mainMenu/SettingsMenu.tscn` (UI in scene) and `menus/mainMenu/SettingsMenu.cs` provide LLM configuration: provider selection (OpenAI, Anthropic, LM Studio, Custom OpenAI-compatible), base URL, model, API key, and an **Enable thinking** option (disabled by default).
- **Test Model** pings the configured provider/model and reports connection/status in the settings UI.
- `game_generator/llm/LlmSettingsStore.cs` persists LLM settings (including `EnableThinking`) to app data (`OS.GetUserDataDir()/llm_settings.json`).
- `game_generator/llm/LlmApi.cs` is registered as an autoload (`LlmApi`) and exposes async methods (`ChatAsync`, `TestConnectionAsync`) for direct in-Godot LLM use (OpenAI-compatible and Anthropic APIs). `ChatAsync` supports streaming with intermediate chunk output: prints chunks to terminal as they arrive via `GD.PrintRaw`/`GD.Print` and exposes optional hooks (`onChunk` callback parameter and `StreamChunkReceived` event) so callers can consume streamed data incrementally. Story/game generation uses these hooks during LLM generation.

## UI Theme

- `assets/main_theme.tres` is the shared UI theme and uses `assets/fonts/pixel_operator/PixelOperator.ttf` as the default font.
- Menu/UI scenes (including `menus/mainMenu/MainMenu.tscn`, `menus/mainMenu/SettingsMenu.tscn`, inventory/skills menus) apply `res://assets/main_theme.tres` on their root controls.


## Tests

- `tests/generation_tests.tscn` + `tests/GenerationTests.cs`: procedural generation/integration checks (run headless with `godot --headless --path . res://tests/generation_tests.tscn`).
- `tests/llm_hello_test.tscn` + `tests/LlmHelloTest.cs`: loads saved LLM settings from `OS.GetUserDataDir()/llm_settings.json` and uses `LlmApi`/`StoryGenerator` to run an LLM generation test, printing output to the terminal then exiting (run headless with `godot --headless --path . res://tests/llm_hello_test.tscn`).
- `tests/llm_hello_world_test.tscn` + `tests/LlmHelloWorldTest.cs`: simple streaming "hello world" test using saved LLM settings; prints streamed chunks as they arrive, reports chunk count/final response, and exits nonzero if no chunks are received (run headless with `godot --headless --path . res://tests/llm_hello_world_test.tscn`).
