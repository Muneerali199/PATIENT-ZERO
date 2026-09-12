#!/bin/bash
# PATIENT-ZERO Godot launcher — sets DOTNET_ROOT so mono never crashes (hostfxr fix)
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
GODOT="$HOME/Tools/Godot_mono.app/Contents/MacOS/Godot"
if [ ! -f "$GODOT" ]; then
  echo "ERROR: Godot not found at $GODOT"
  echo "Re-download: curl -L -o ~/Tools/godot.zip https://github.com/godotengine/godot/releases/download/4.4.1-stable/Godot_v4.4.1-stable_mono_macos.universal.zip && cd ~/Tools && unzip -qo godot.zip"
  exit 1
fi
cd "$(dirname "$0")/game" || exit 1
exec "$GODOT" --path . "$@"
