# Orbital Ante: check the game in Unity without opening the editor window (Windows).
#
#   Right-click this file > Run with PowerShell, or in a terminal:  powershell -ExecutionPolicy Bypass -File tools\check-in-unity.ps1
#   Add -SkipPlayMode to run only the quick rule tests.
#
# It (1) sets the project up, (2) runs the EditMode rule tests, (3) runs the PlayMode tests on real game objects,
# and writes everything to unity\Logs\oa-check\. The last lines say PASS or FAIL. Unity must be closed first.

param([switch]$SkipPlayMode, [string]$Unity = $env:UNITY_EDITOR)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "unity"
$out = Join-Path $project "Logs\oa-check"
New-Item -ItemType Directory -Force -Path $out | Out-Null

if (-not $Unity) {
    $candidates = @()
    foreach ($root in @("C:\Program Files\Unity\Hub\Editor", "$env:ProgramFiles\Unity\Hub\Editor", "D:\Program Files\Unity\Hub\Editor")) {
        if (Test-Path $root) { $candidates += Get-ChildItem $root -Directory | Where-Object { $_.Name -like "6000.*" } }
    }
    $pick = $candidates | Sort-Object { [version]($_.Name -replace '[a-z].*$', '') } -Descending | Select-Object -First 1
    if ($pick) { $Unity = Join-Path $pick.FullName "Editor\Unity.exe" }
}
if (-not $Unity -or -not (Test-Path $Unity)) {
    Write-Host "Could not find Unity 6. Set the UNITY_EDITOR environment variable to the full path of Unity.exe and run again." -ForegroundColor Red
    exit 2
}
Write-Host "Using $Unity"

function Run-Unity([string]$name, [string[]]$extra) {
    $log = Join-Path $out "$name.log"
    Write-Host "== $name (log: $log)"
    $args = @("-batchmode", "-projectPath", "`"$project`"", "-logFile", "`"$log`"") + $extra
    $p = Start-Process -FilePath $Unity -ArgumentList $args -Wait -PassThru -NoNewWindow
    return $p.ExitCode
}

function Summarise([string]$xml) {
    if (-not (Test-Path $xml)) { return "no results file (Unity probably failed to compile: read the log)" }
    [xml]$doc = Get-Content $xml
    $run = $doc."test-run"
    $line = "total $($run.total), passed $($run.passed), failed $($run.failed), skipped $($run.skipped)"
    $failed = $doc.SelectNodes("//test-case[@result='Failed']")
    foreach ($f in $failed) { $line += "`n   FAILED " + $f.fullname + "`n      " + ($f.failure.message.InnerText -replace "`r?`n", " ").Trim() }
    return $line
}

$results = @()
$setup = Run-Unity "1-setup" @("-quit", "-executeMethod", "OrbitalAnte.EditorTools.ProjectSetup.SetUpFromCommandLine")
$results += "Setup: " + $(if ($setup -eq 0) { "OK" } else { "PROBLEMS (exit $setup) - read 1-setup.log" })

$edit = Join-Path $out "editmode-results.xml"
Remove-Item $edit -ErrorAction SilentlyContinue
$e = Run-Unity "2-editmode" @("-runTests", "-testPlatform", "EditMode", "-testResults", "`"$edit`"")
$results += "EditMode tests: " + (Summarise $edit)

$p = 0
if (-not $SkipPlayMode) {
    $play = Join-Path $out "playmode-results.xml"
    Remove-Item $play -ErrorAction SilentlyContinue
    $p = Run-Unity "3-playmode" @("-runTests", "-testPlatform", "PlayMode", "-testResults", "`"$play`"")
    $results += "PlayMode tests: " + (Summarise $play)
}

# Compile errors and script errors from all three runs, so nothing is missed.
$errors = Get-ChildItem $out -Filter "*.log" | Select-String -Pattern "error CS\d+|Exception:|Orbital Ante: .*problem" | Select-Object -First 40
if ($errors) { $results += "Errors in the logs:"; $results += ($errors | ForEach-Object { "   " + $_.Line.Trim() }) }

$verdict = if ($setup -eq 0 -and $e -eq 0 -and $p -eq 0 -and -not $errors) { "PASS" } else { "FAIL" }
$results += "Overall: $verdict"
$results | Set-Content (Join-Path $out "summary.txt")
$results | ForEach-Object { Write-Host $_ }
if ($verdict -eq "PASS") { exit 0 } else { exit 1 }
