# AGENTS.md — Grid Quest Engineering Guide

Guidance and mandatory protocols for AI coding agents working on **Grid Quest**, a Godot 4.7 C# 2D game.

## 1. Project Overview

- **Engine:** Godot 4.7, C# (.NET 8 / .NET 10), Forward+ renderer, Jolt 3D physics.
- **Root Namespace:** `GridQuest`
- **Solution & Project:** `Grid Quest.sln`, `Grid Quest.csproj`
- **Architecture & Layout:** Full specifications maintained in [docs/project-structure.md](docs/project-structure.md).

---

## 2. Repository Layout

| Path | Purpose |
| --- | --- |
| `project.godot` | Engine configuration (edit via the editor when possible) |
| `level/` | Gameplay scenes (`map.tscn`, level architecture, `MapGrid`) |
| `map/` | One C# file per map tile/item type (floor, objects, plants) |
| `menus/` | UI scenes (`main_menu.tscn`, HUD, screens) |
| `player/` | `Player.cs` script and `player.tscn` scene |
| `assets/` | Art assets and map tile textures |
| `docs/` | Project documentation, design docs, and architectural specs |

---

## 3. Coding Conventions

- **Style:** Match existing scene (`.tscn`) and script formatting. One script per node where it makes sense.
- **C# Standards:**
  - Indentation: Tabs.
  - Types & Methods: `PascalCase`.
  - Private Fields: `_camelCase`.
  - Overrides: Use `override` for engine callbacks (`_Ready`, `_Process`, `_PhysicsProcess`).
- **Comments:** Keep comments sparse and purposeful. Do not add comments unless they clarify non-obvious logic or complex algorithms.
- **APIs:** Always prefer built-in Godot engine APIs over custom boilerplate for input, math, and physics where an engine feature exists.

---

## 4. Mandatory Agent Operational Protocol

> **CRITICAL:** The entire purpose of this file is to enforce engineering discipline, system cohesion, and synchronized documentation. Agents must strictly adhere to the following three workflow laws on every task.

### Rule 1: Read the Docs First (Pre-Flight)
- **MANDATORY:** Before writing any code, modifying scenes, or introducing new systems, you **must** read the relevant documentation files under `docs/`. 
- Understand how existing systems (movement, state machines, saving, UI routing) operate so your changes integrate seamlessly without breaking architectural patterns.

### Rule 2: Holistic Implementation & Human Readability
- **Code Clarity:** Write clean, modular, and transparent C# code that human maintainers can easily read, audit, and extend.
- **No Isolated Silos:** Never drop a new feature or property into a single script or scene in isolation. Ensure end-to-end integration: hook up inputs, wire signals, update configuration files, and ensure dependent systems are fully aware of the new feature.

### Rule 3: Post-Change Documentation Sync
- **Never Leave Docs Outdated:** If your changes modify system architecture, add new mechanics, alter data schemas, or change file paths, you **must** update or expand the corresponding markdown files in `docs/` within the exact same task execution.
- Keeping documentation synchronized with the codebase is a core requirement, not an optional afterthought.