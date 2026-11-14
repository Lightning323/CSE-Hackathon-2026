#!/bin/bash

GODOT_BIN="${GODOT_PATH:-$(command -v godot || echo "$HOME/.Programs/Godot/Godot.x86_64")}"

opencode mcp add godot --env "GODOT_PATH=$GODOT_BIN" -- npx -y @coding-solo/godot-mcp
opencode mcp list
