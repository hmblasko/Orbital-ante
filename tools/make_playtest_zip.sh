#!/usr/bin/env bash
# Builds OrbitalAnte-playtest.zip: one self-contained Godot project folder
# with the release-built game library in bin/, for opening straight in
# Godot 4.7.2 (Matthew's Steam Deck playtests).
#
# Usage: tools/make_playtest_zip.sh <out_dir> [windows_target_dir]
#   windows_target_dir: a CARGO_TARGET_DIR holding
#   x86_64-pc-windows-gnu/release/orbital_ante.dll (optional).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:?out dir}"
WIN="${2:-}"
(cd "$ROOT/rust" && cargo build --release -p orbital_ante)
if [[ -n "$WIN" ]]; then
  (cd "$ROOT/rust" && CARGO_TARGET_DIR="$WIN" cargo build --release -p orbital_ante --target x86_64-pc-windows-gnu)
fi
STAGE="$(mktemp -d)"
P="$STAGE/OrbitalAnte-playtest"
mkdir -p "$P/bin"
(cd "$ROOT/godot" && tar --exclude=.godot --exclude='tests/_tmp_*' -cf - .) | tar -xf - -C "$P"
cp "$ROOT/rust/target/release/liborbital_ante.so" "$P/bin/"
[[ -n "$WIN" ]] && cp "$WIN/x86_64-pc-windows-gnu/release/orbital_ante.dll" "$P/bin/"
cat > "$P/orbital_ante.gdextension" <<'GDE'
; Playtest build: the compiled Rust game code is inside this project, in bin/.
[configuration]
entry_symbol = "gdext_rust_init"
compatibility_minimum = 4.7
reloadable = false

[libraries]
linux.debug.x86_64 = "res://bin/liborbital_ante.so"
linux.release.x86_64 = "res://bin/liborbital_ante.so"
windows.debug.x86_64 = "res://bin/orbital_ante.dll"
windows.release.x86_64 = "res://bin/orbital_ante.dll"
GDE
cp "$ROOT/tools/playtest_HOW_TO_PLAY.txt" "$P/HOW_TO_PLAY.txt"
mkdir -p "$OUT"
rm -f "$OUT/OrbitalAnte-playtest.zip"
(cd "$STAGE" && zip -qr "$OUT/OrbitalAnte-playtest.zip" OrbitalAnte-playtest)
cp "$P/HOW_TO_PLAY.txt" "$OUT/"
rm -rf "$STAGE"
echo "Wrote $OUT/OrbitalAnte-playtest.zip"
