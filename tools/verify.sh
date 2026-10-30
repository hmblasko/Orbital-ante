#!/usr/bin/env bash
# One-command verification run (CLAUDE.md section 19.1, "every commit").
#
#   tools/verify.sh                 everything that can run on this machine
#   tools/verify.sh --no-render     skip screenshots (no display / GPU)
#
# Environment:
#   GODOT=/path/to/godot            Godot 4.7.2 editor binary (default: "godot" on PATH)
#   SEED=XXXX-XXXX                  seed for the bot run (default below)
#   OUT=verify_out/<name>           results folder (default: verify_out/latest)
#
# Every gate is recorded as pass, fail or not run; nothing is skipped silently.
# Exit code is 1 if any gate failed.

set -u
cd "$(dirname "$0")/.."
ROOT=$(pwd)
GODOT=${GODOT:-godot}
SEED=${SEED:-7K3M-Q9TD}
OUT=$(realpath -m "${OUT:-verify_out/latest}")
RENDER=1
[ "${1:-}" = "--no-render" ] && RENDER=0

rm -rf "$OUT"; mkdir -p "$OUT"
echo "== Building tools and game library"
(cd rust && cargo build --quiet && cargo build --quiet -p oa_verify) || { echo "build failed"; exit 1; }
V="$ROOT/rust/target/debug/oa_verify"
gate() { "$V" gate "$OUT" "$@"; }
IGNORE="$ROOT/godot/data/verify/log_ignore.ron"

echo "== Rust unit, determinism and data-file tests"
run_tests() { # name filter severity
  (cd rust && cargo test --quiet --workspace -- "$2" >"$OUT/test_$1.log" 2>&1)
  local ok=$?
  local passed=$(grep -ho '[0-9]* passed' "$OUT/test_$1.log" | awk '{s+=$1} END {print s+0}')
  local failed=$(grep -ho '[0-9]* failed' "$OUT/test_$1.log" | awk '{s+=$1} END {print s+0}')
  if [ $ok -eq 0 ] && [ "$passed" -gt 0 ]; then
    gate "$3" pass "$passed passed, $failed failed"
  else
    gate "$3" fail "$passed passed, $failed failed; see test_$1.log" S2
  fi
}
run_tests unit "" "Rust unit tests"
run_tests determinism "seed::" "Determinism tests"
run_tests data "data::" "Data-file validation"

if ! command -v "$GODOT" >/dev/null 2>&1 && [ ! -x "$GODOT" ]; then
  for g in "Smoke test" "Input replay determinism" "Screenshot regression"; do gate "$g" not_run "Godot not found; set GODOT=/path/to/godot"; done
else
  echo "== Importing project"
  "$GODOT" --headless --path godot --import >"$OUT/import.log" 2>&1

  echo "== Smoke test: boot, menu, slice, 60 s autoplay bot, quit"
  mkdir -p "$OUT/smoke"
  "$GODOT" --headless --path godot --fixed-fps 60 -- --smoke --seed="$SEED" --out="$OUT/smoke" >"$OUT/smoke/godot.log" 2>&1
  code=$?
  "$V" logcheck "$OUT/smoke/godot.log" "$OUT/smoke/logcheck.json" "$IGNORE" 0 >/dev/null; logs=$?
  if [ ! -f "$OUT/smoke/results.json" ]; then
    gate "Smoke test" fail "game exited with code $code before writing results (crash?)" S1 smoke
  elif [ $code -ne 0 ]; then
    gate "Smoke test" fail "$(grep -o '"failures": \[[^]]*' "$OUT/smoke/results.json" | head -c 300)" S2 smoke
  elif [ $logs -ne 0 ]; then
    gate "Smoke test" fail "errors in the log" S3 smoke
  else
    gate "Smoke test" pass "seed $SEED, 60 s wanderer bot, no detector hits, no log errors" "" smoke
  fi

  echo "== Input replay determinism"
  if [ -f "$OUT/smoke/replay.ron" ]; then
    mkdir -p "$OUT/replay"
    "$GODOT" --headless --path godot -- --replay="$OUT/smoke/replay.ron" --out="$OUT/replay" >"$OUT/replay/godot.log" 2>&1
    code=$?
    "$V" logcheck "$OUT/replay/godot.log" "$OUT/replay/logcheck.json" "$IGNORE" 0 >/dev/null
    drift=$(grep -o '"replay_drift_m": [0-9.e-]*' "$OUT/replay/results.json" 2>/dev/null | cut -d' ' -f2)
    if [ $code -eq 0 ]; then gate "Input replay determinism" pass "replayed in real time; ended ${drift} m from the recording" "" replay
    else gate "Input replay determinism" fail "drift ${drift:-unknown} m, exit $code" S2 replay; fi
  else
    gate "Input replay determinism" not_run "smoke test produced no replay"
  fi

  echo "== Screenshots"
  if [ $RENDER -eq 0 ]; then
    gate "Screenshot regression" not_run "--no-render given"
  else
    mkdir -p "$OUT/screenshots"
    WRAP=""
    if [ -z "${DISPLAY:-}" ] && command -v xvfb-run >/dev/null; then WRAP="xvfb-run -a -s '-screen 0 1280x800x24'"; fi
    eval $WRAP "\"$GODOT\"" --path godot --resolution 1280x800 --audio-driver Dummy -- --screenshots --seed="$SEED" --out="\"$OUT/screenshots\"" >"$OUT/screenshots/godot.log" 2>&1
    code=$?
    "$V" logcheck "$OUT/screenshots/godot.log" "$OUT/screenshots/logcheck.json" "$IGNORE" 0 >/dev/null
    if [ $code -ne 0 ]; then
      gate "Screenshot regression" fail "screenshot run failed (exit $code)" S3 screenshots
    elif [ -z "$(ls "$ROOT/tests/baselines/"*.png 2>/dev/null)" ]; then
      "$V" compare "$ROOT/tests/baselines" "$OUT/screenshots" "$OUT" >/dev/null
      gate "Screenshot regression" not_run "no approved baselines yet; $(ls "$OUT/screenshots"/*.png | wc -l) new screenshots await Matthew's approval" "" screenshots
    elif "$V" compare "$ROOT/tests/baselines" "$OUT/screenshots" "$OUT"; then
      gate "Screenshot regression" pass "all viewpoints match baselines (SSIM >= 0.98)" "" screenshots
    else
      gate "Screenshot regression" fail "one or more viewpoints flagged; see report" S3 screenshots
    fi
  fi
fi

for g in "Autoplay bots (50 seeds x 5 profiles)" "Balance simulator" "Fun proxies"; do
  gate "$g" not_run "the systems these need arrive in later phases"
done
gate "Steam Deck session and rubric" not_run "needs Matthew's Deck"

echo "== Report"
"$V" report "$OUT" "$OUT/report.html" "Verification report ($(git rev-parse --short HEAD 2>/dev/null || echo local), seed $SEED)"
OA_BUILD=$(git rev-parse --short HEAD 2>/dev/null || echo local) "$V" bugs "$OUT"
echo "== Gates"
python3 - "$OUT/gates.json" <<'PY' 2>/dev/null || cat "$OUT/gates.json"
import json, sys
for g in json.load(open(sys.argv[1])):
    print(f"{g['status'].upper():8} {g['name']}: {g['detail']}")
PY
echo "Report: $OUT/report.html"
grep -q '"status": "fail"' "$OUT/gates.json" && exit 1
exit 0
