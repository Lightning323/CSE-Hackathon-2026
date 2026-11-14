# AGENTS.md

Guidance for AI coding agents working on **Grid Quest**, a Godot 4.7 C# 2D game.

## Project

- Engine: Godot 4.7, C# (.NET 8), Forward+ renderer, Jolt 3D physics.
- Root namespace: `GridQuest`. Solution: `Grid Quest.sln`, project: `Grid Quest.csproj`.
- Full layout: see [docs/project-structure.md](docs/project-structure.md).

## Layout (short)

| Path | Purpose |
| --- | --- |
| `project.godot` | Engine configuration (edit via the editor when possible) |
| `level/` | Gameplay scenes (`map.tscn`) |
| `menus/` | UI scenes (`main_menu.tscn`) |
| `player/` | `Player.cs` script and `player.tscn` scene |
| `assets/` | Art assets (Cute Fantasy free pack) |
| `docs/` | Project documentation |

## Conventions

- Match existing scene/script style: Godot 4.7 `.tscn` format, one script per node where it makes sense.
- C# files use tabs for indentation, `PascalCase` types/methods, `_camelCase` private fields, and `override` for engine callbacks (`_Ready`, `_Process`, `_PhysicsProcess`).
- Keep `.import` / `.uid` files committed; never edit `.godot/` (generated, gitignored).
- Do not add comments unless they clarify non-obvious logic.
- Prefer Godot APIs over raw input/physics where an engine feature exists.

## Workflow

- Build: `dotnet build` from the project root (or let the Godot editor build).
- Run: open the project in Godot, or use the Godot MCP server registered by `setup_mcp.sh`.
- There is no test suite yet; verify changes by running the project and checking the Godot output/debugger.
- Only commit when explicitly asked. Do not modify `assets/` licensing or redistribute the asset pack.

## Player controls

- Arrow keys: move in eight directions.
- Space / J: attack in the facing direction.
- Input actions are defined in `project.godot`; the player sprite sheet is `assets/Cute_Fantasy_Free/Player/Player.png`.

## Assets

- The Cute Fantasy free pack is **non-commercial only**; do not resell or redistribute. Details: `assets/Cute_Fantasy_Free/read_me.txt`.
