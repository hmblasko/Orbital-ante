#!/usr/bin/env bash
# Orbital Ante: check the game in Unity without opening the editor window (macOS and Linux).
#
#   bash tools/check-in-unity.sh            # setup, EditMode tests, PlayMode tests
#   bash tools/check-in-unity.sh --quick    # setup and EditMode tests only
#
# Writes everything to unity/Logs/oa-check/. The last lines say PASS or FAIL. Unity must be closed first.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(dirname "$here")"
project="$repo/unity"
out="$project/Logs/oa-check"
mkdir -p "$out"
quick=0; [ "${1:-}" = "--quick" ] && quick=1

unity="${UNITY_EDITOR:-}"
if [ -z "$unity" ]; then
  for root in "/Applications/Unity/Hub/Editor" "$HOME/Applications/Unity/Hub/Editor" "$HOME/Unity/Hub/Editor"; do
    [ -d "$root" ] || continue
    v="$(ls "$root" | grep '^6000\.' | sort -V | tail -1)"
    [ -n "$v" ] || continue
    if [ -x "$root/$v/Unity.app/Contents/MacOS/Unity" ]; then unity="$root/$v/Unity.app/Contents/MacOS/Unity"; fi
    if [ -x "$root/$v/Editor/Unity" ]; then unity="$root/$v/Editor/Unity"; fi
  done
fi
if [ -z "$unity" ] || [ ! -x "$unity" ]; then
  echo "Could not find Unity 6. Set UNITY_EDITOR to the full path of the Unity program and run again." >&2
  exit 2
fi
echo "Using $unity"

run() { local name="$1"; shift; echo "== $name (log: $out/$name.log)"; "$unity" -batchmode -projectPath "$project" -logFile "$out/$name.log" "$@"; return $?; }

summarise() {
  local xml="$1"
  if [ ! -f "$xml" ]; then echo "no results file (Unity probably failed to compile: read the log)"; return; fi
  python3 - "$xml" <<'PY' 2>/dev/null || grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$xml" | head -1
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print("total %s, passed %s, failed %s, skipped %s" % (r.get("total"), r.get("passed"), r.get("failed"), r.get("skipped")))
for c in r.iter("test-case"):
    if c.get("result") == "Failed":
        m = c.find("failure/message")
        print("   FAILED " + c.get("fullname") + "\n      " + (" ".join((m.text or "").split()) if m is not None else ""))
PY
}

summary=()
run 1-setup -quit -executeMethod OrbitalAnte.EditorTools.ProjectSetup.SetUpFromCommandLine; s=$?
summary+=("Setup: $([ $s -eq 0 ] && echo OK || echo "PROBLEMS (exit $s) - read 1-setup.log")")

rm -f "$out/editmode-results.xml"
run 2-editmode -runTests -testPlatform EditMode -testResults "$out/editmode-results.xml"; e=$?
summary+=("EditMode tests: $(summarise "$out/editmode-results.xml")")

p=0
if [ $quick -eq 0 ]; then
  rm -f "$out/playmode-results.xml"
  run 3-playmode -runTests -testPlatform PlayMode -testResults "$out/playmode-results.xml"; p=$?
  summary+=("PlayMode tests: $(summarise "$out/playmode-results.xml")")
fi

errors="$(grep -hE "error CS[0-9]+|Exception:|Orbital Ante: .*problem" "$out"/*.log | head -40)"
[ -n "$errors" ] && summary+=("Errors in the logs:" "$(echo "$errors" | sed 's/^/   /')")

verdict=FAIL
[ $s -eq 0 ] && [ $e -eq 0 ] && [ $p -eq 0 ] && [ -z "$errors" ] && verdict=PASS
summary+=("Overall: $verdict")
printf '%s\n' "${summary[@]}" | tee "$out/summary.txt"
[ "$verdict" = PASS ]
