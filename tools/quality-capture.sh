#!/usr/bin/env bash
# Orbital Ante: take the quality gate's evidence in Unity without opening the editor window (macOS, Linux, Steam Deck).
#
#   bash tools/quality-capture.sh                 # level 2: all tests, full capture of every target
#   bash tools/quality-capture.sh 1               # level 1: rule tests and a few pictures of each target
#   bash tools/quality-capture.sh 2 ship,planets  # only these targets (ids from Verification/Config/targets.json)
#
# Evidence goes to Verification/Reports/<build id>/. This script gathers evidence only: it never scores
# anything and never says PASS. The verdict comes from the independent Critic review afterwards.
# Unity must be closed first. A screen is needed (pictures cannot be taken with -nographics).
set -u
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(dirname "$here")"
project="$repo/unity"
level="${1:-2}"
targets="${2:-}"
case "$level" in 1|2) ;; *) echo "The level must be 1 or 2." >&2; exit 2;; esac

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
  exit 3
fi
echo "Using $unity"

commit="$(git -C "$repo" rev-parse HEAD 2>/dev/null || echo unknown)"
id="$(date +%Y%m%d-%H%M)-$(echo "$commit" | cut -c1-7)"
reports="$repo/Verification/Reports"
run="$reports/$id"
if [ -e "$run/test-results.json" ]; then
  echo "A run called $id already exists. Evidence is never overwritten: wait a minute and run again." >&2
  exit 3
fi
mkdir -p "$run/logs"
if [ -n "$(git -C "$repo" status --porcelain 2>/dev/null)" ]; then
  echo "Note: there are changes that are not committed. The evidence will be labelled with commit $(echo "$commit" | cut -c1-7), which is not exactly what is being photographed." | tee "$run/logs/uncommitted-changes.txt"
fi

# The run file switches the capture suites on. Without it they skip themselves.
list=""
if [ -n "$targets" ]; then list="\"$(echo "$targets" | sed 's/,/","/g')\""; fi
printf '{"buildId":"%s","commit":"%s","level":%s,"targets":[%s],"startedUtc":"%s"}\n' \
  "$id" "$commit" "$level" "$list" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$reports/.current-run.json"
trap 'rm -f "$reports/.current-run.json"' EXIT

run_unity() { local name="$1"; shift; echo "== $name (log: $run/logs/$name.log)"; "$unity" -batchmode -projectPath "$project" -logFile "$run/logs/$name.log" "$@"; return $?; }

run_unity 1-setup -quit -executeMethod OrbitalAnte.EditorTools.ProjectSetup.SetUpFromCommandLine
echo "   setup exit code $?"
run_unity 2-editmode -runTests -testPlatform EditMode -testResults "$run/editmode-results.xml"
echo "   rule tests exit code $?"
if [ "$level" = 1 ]; then
  run_unity 3-playmode-capture -runTests -testPlatform PlayMode -testCategory OACapture -testResults "$run/playmode-results.xml"
else
  run_unity 3-playmode-capture -runTests -testPlatform PlayMode -testResults "$run/playmode-results.xml"
fi
echo "   Play mode exit code $?"
run_unity 4-summary -quit -executeMethod OrbitalAnte.Verification.EditorTools.VerificationRunner.SummariseFromCommandLine -oaBuildId "$id" -oaLevel "$level"
code=$?

pictures="$(find "$run" -name '*.jpg' 2>/dev/null | wc -l | tr -d ' ')"
echo
echo "Build id: $id"
echo "Evidence: Verification/Reports/$id  ($pictures pictures)"
grep -h "Orbital Ante verification" "$run/logs/4-summary.log" 2>/dev/null | tail -3
case $code in
  0) echo "Every test ran and passed. Next: the independent Critic review. This is not a verdict.";;
  2) echo "Tests ran and some FAILED. The Critic review must record them. This is not a verdict.";;
  *) echo "The run could not finish (exit $code). Read the logs in Verification/Reports/$id/logs. Any review of this run is BLOCKED.";;
esac
[ "$pictures" -gt 0 ] || echo "No pictures were taken, so every target is BLOCKED. Common causes: no screen (do not use -nographics), a compile error (read logs/2-editmode.log)."
exit $code
