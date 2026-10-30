#!/usr/bin/env bash
# Downloads Godot and its export templates on a CI machine and sets $GODOT.
# Usage: tools/ci_install_godot.sh linux|windows
set -euo pipefail
V=${GODOT_VERSION:-4.7.2}
BASE="https://github.com/godotengine/godot/releases/download/${V}-stable"
case "$1" in
  linux)
    curl -sSL -o godot.zip "$BASE/Godot_v${V}-stable_linux.x86_64.zip"
    unzip -q godot.zip -d "$HOME/godot"
    GODOT_BIN="$HOME/godot/Godot_v${V}-stable_linux.x86_64"
    TEMPLATES="$HOME/.local/share/godot/export_templates/${V}.stable"
    ;;
  windows)
    curl -sSL -o godot.zip "$BASE/Godot_v${V}-stable_win64.exe.zip"
    unzip -q godot.zip -d "$HOME/godot"
    GODOT_BIN="$HOME/godot/Godot_v${V}-stable_win64_console.exe"
    TEMPLATES="$APPDATA/Godot/export_templates/${V}.stable"
    ;;
  *) echo "usage: $0 linux|windows"; exit 2 ;;
esac
curl -sSL -o templates.tpz "$BASE/Godot_v${V}-stable_export_templates.tpz"
mkdir -p "$TEMPLATES"
unzip -q -j templates.tpz -d "$TEMPLATES"
echo "GODOT=$GODOT_BIN" >> "$GITHUB_ENV"
echo "Installed Godot $V and templates in $TEMPLATES"
