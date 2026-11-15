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
│   ├── docs/                  # Asset licenses/notes
│   └── images/
│       ├── map_tiles/         # Floor, plants, trees, buildings, props
│       ├── characters/        # Entity/character sprites
│       └── fx/                # Projectiles and effects
├── entities/
│   ├── base/Entity.cs         # Shared abstract base: identity, stats, inventory, tools, memory
│   ├── Entity.cs              # Unified entity (movement/combat/upgrades + optional LLM/AI control)
│   ├── entity.tscn            # Reusable entity scene placed directly in levels
│   ├── PlayerController.cs    # Human input, camera, and control/possession of a selected entity
│   ├── PlayerUpgradeDefs.cs   # Upgrade definitions and cost scaling
│   └── SkinCatalog.cs         # Skin definitions and sprite-frame helpers
├── level/
│   ├── MainLevel.cs           # Main gameplay level script
│   ├── main_level.tscn        # Main gameplay scene (map, entities, controller, UI)
│   ├── DayNightCycle.cs       # World lighting and day progression
│   ├── WorldProgress.cs       # World/reset/day progress and persistence
│   └── WorldResetManager.cs   # Map/world reset while preserving controlled entity progression
├── items/                     # Item and inventory systems
│   ├── Coin.cs                # World coin pickup
│   ├── Fireball.cs            # Fire staff projectile logic
│   ├── CrossbowBolt.cs        # Crossbow bolt projectile (piercing)
│   ├── ItemDefs.cs            # Item definitions and catalog
│   └── InventoryContainer.cs  # Slot container helpers
├── map/                       # Map generation and placeable objects
│   ├── Map.cs                 # Runtime map composition and building placement
│   ├── MapConstants.cs        # Shared map constants
│   ├── RoomGenerator.cs       # Room generation
│   ├── RoomGenerationContext.cs
│   ├── BuildingGenerator.cs   # Procedural building layouts
│   ├── map.tscn               # Reusable map scene
│   ├── map_floors/            # Floor TileMapLayer/TileSet helpers
│   └── map_objects/           # Walls, trees, plants, props, gates, chests, monument, houses
├── menus/
│   ├── CheatMenu.cs           # Debug/cheat menu
│   ├── GameOverMenu.cs        # Pause/game-over menu and entity control selector UI
│   ├── InventoryUi.cs         # Inventory + chest UI
│   ├── InventorySlotUi.cs     # Reusable inventory slot control
│   ├── MonumentIndicator.cs   # Directional indicator pointing to the monument
│   ├── SkillsUi.cs            # Monument upgrade menu/UI
│   ├── VillagerChatUi.cs      # Chat UI for interacting with AI-controlled entities
│   ├── *.tscn                 # UI scenes
│   └── mainMenu/              # Startup menu, settings, save/session helpers
└── game_generator/            # New-game/story generation and LLM helpers
    └── llm/
        ├── LlmApi.cs          # Autoload LLM API
        └── LlmSettingsStore.cs
```

## Entity Architecture (`entities/`)

There is no separate player-vs-AI entity type. All characters use the unified `entities/Entity.cs` scene/script (`entities/entity.tscn`), which extends `entities/base/Entity.cs`.

- **`entities/base/Entity.cs`**: shared abstract character foundation (identity/skin/faction, health and combat stats, inventory/equipment/money, memory/chat history, movement intent, world observation, and tool registry/execution).
- **`entities/Entity.cs`**: concrete unified character implementation. It includes movement/combat/animation, weapon handling (sword, fire staff, scythe, crossbow), upgrades/progression stats, interaction behavior, and an optional LLM decision loop. When `AiControlled` is enabled, the entity acts autonomously via `LlmApi` tools; when controlled by a human, it follows input provided by `PlayerController`.
- **`entities/PlayerController.cs`**: level/controller node that handles human movement and attack input, camera follow/zoom, and possession of a selected entity. Controlling an entity marks it as the active/controlled entity; releasing control returns it to AI control.
- **`entities/entity.tscn`**: reusable scene for any entity instance. Levels place entities directly in the scene (e.g. a single `Entity` node in `level/main_level.tscn`).
- **`entities/PlayerUpgradeDefs.cs`**: monument/character upgrade definitions and cost scaling used by entities and the skills UI.
- **`entities/SkinCatalog.cs`**: reusable entity skin definitions and sprite-frame building helpers.

The base/tooling model supports observation, movement, stop, attack, interaction, inventory inspection, weapon equip, upgrades, memory, and speech. `GetWorldObservation()` packages entity state, nearby entities/interactables, world/day context, and available tools for LLM-driven behavior.

## Control, Camera, and Entity Selection

- `level/main_level.tscn` includes a `PlayerController` node with a `Camera2D` and one placed `Entity`. On level start, `MainLevel.cs` positions the entity at a valid map spawn and the controller begins controlling it by default.
- Human controls (when an entity is controlled): 4-directional movement (`WASD`/arrow keys, controller left stick/D-pad), attack (`Space`/`J` or controller **A**), interact (`F` or controller **X**), inventory (`E`/`I` or controller **Y**), pause/menu (`Esc`/controller **Start**). Camera zoom is handled by `PlayerController` (mouse wheel and `+`/`-` keys/keypad).
- HUD displays the currently viewed entity as `Viewing ENTITY_NAME` (`menus/ObservationHud.cs`). `Tab` cycles the observed/viewed entity at any time (camera follows the viewed entity). `Ctrl` toggles control of the currently viewed entity (take control / release to AI), so you can switch view while controlling and then take control of the viewed entity.
- Entities are assigned unique runtime names via `entities/EntityNameGenerator.cs`.
- The in-game pause menu (`menus/GameOverMenu.cs`, shown as "Paused"/"Game Over") includes a simple **Entities** UI that lists entities in the current scene. Clicking an entity begins controlling it; **Deselect (AI controls all)** releases control so entities act autonomously via AI.
- Systems that operate on the active character (inventory, skills/monument upgrades, coin pickup magnet/collection, world reset, monument indicator, game over on controlled-entity death) target the currently controlled entity (`controlled_entity`/compatibility player grouping managed by `Entity`/`PlayerController`).

## Combat, Inventory, and Progression

- Unified entities share combat/health/inventory/upgrade behavior. Weapon-specific animations and attacks (including scythe and charged crossbow behavior) are implemented in `entities/Entity.cs`.
- Entities have slot-based backpack inventory, weapon equipment, and wallet/gold tracking (`items/ItemDefs.cs`, `items/InventoryContainer.cs`). Coins from world/chests are converted to gold/wallet for upgrades.
- `menus/inventory_ui.tscn` + `InventoryUi.cs`/`InventorySlotUi.cs` provide inventory and chest interactions for the controlled entity.
- `menus/skills_ui.tscn` + `menus/SkillsUi.cs` provide the monument upgrade UI (random rolls, purchases, and world-randomize action) for the controlled entity.

## Entity Interaction and Chat

- `menus/villager_chat_ui.tscn` + `menus/VillagerChatUi.cs` provide chat with an AI-controlled entity. Pressing interact near an AI entity opens chat (with interaction priority rules shared with chests/monument). Replies are generated through the entity's LLM-backed response flow and chat/memory state.

## Map and Tiles

- `map/Map.cs` procedurally generates the runtime map and composes floor/object layers, villages, paths, monument placement, buildings/rooms, and decorations. It provides random spawn helpers and world-position queries used by entities and systems.
- `RoomGenerator`, `BuildingGenerator`, and `RoomGenerationContext` handle room/building layout and placement rules. `map/map_objects/chest/`, `map/map_objects/gate/`, and `map/map_objects/monument/` provide interactive world objects.
- See [map-architecture.md](./map-architecture.md) for tile-vs-node decisions and generation details.

## Monument, Upgrades, and World Resets

- `map/map_objects/monument/Monument.cs` tracks discovery and opens the skills UI for the controlled entity when interacted with (respecting chest/entity interaction priority).
- `menus/SkillsUi.cs` presents purchasable upgrades and the always-available world-randomize option.
- `level/WorldProgress.cs` tracks per-run and persistent world/reset progress; `level/WorldResetManager.cs` regenerates the map/world state and moves the controlled entity to a valid spawn while preserving that entity's progression (stats, inventory, upgrades, money).

## Main Menu and In-Game Menu

- `menus/mainMenu/MainMenu.tscn` / `MainMenu.cs` is the startup flow (start game, settings, world-reset high score display).
- **Start Game** creates/reuses a temporary session save via `SaveManager`, sets session state in `GameSession`, and loads `res://level/main_level.tscn`.
- The in-game menu (`menus/game_over_menu.tscn` + `menus/GameOverMenu.cs`) pauses gameplay and provides restart/controls/exit actions, startup controls flow, game-over handling on controlled-entity death, and the entity selection UI for controlling/deselecting entities.

## LLM Settings/API and UI Theme

- `menus/mainMenu/SettingsMenu.tscn` / `SettingsMenu.cs` configure LLM provider/model/endpoint/key and related options.
- `game_generator/llm/LlmSettingsStore.cs` persists settings in app data; `game_generator/llm/LlmApi.cs` (autoload) provides LLM chat/test/streaming APIs used by entity AI/chat and generation flows.
- `assets/main_theme.tres` is the shared UI theme used by menu/UI scenes.

## Notes

- `.godot/` is generated by the editor and ignored by git; do not edit it by hand.
- `.import` files are generated by Godot on asset import and should be committed.
- Scenes use Godot 4.7 scene format; custom C# scripts target .NET 8.
- Audio SFX/music assets live under `assets/audio/`; level BGM and gameplay SFX hooks are wired in level/entity/item scripts.
