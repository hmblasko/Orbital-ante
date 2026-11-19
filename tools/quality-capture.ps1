# Orbital Ante: take the quality gate's evidence in Unity without opening the editor window (Windows).
#
#   powershell -ExecutionPolicy Bypass -File tools\quality-capture.ps1                       # level 2, every target
#   powershell -ExecutionPolicy Bypass -File tools\quality-capture.ps1 -Level 1              # quick check
#   powershell -ExecutionPolicy Bypass -File tools\quality-capture.ps1 -Targets ship,planets # only these targets
#
# Evidence goes to Verification\Reports\<build id>\. This script gathers evidence only: it never scores anything
# and never says PASS. Unity must be closed first.

param([ValidateSet(1, 2)][int]$Level = 2, [string[]]$Targets = @(), [string]$Unity = $env:UNITY_EDITOR)

$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "unity"

if (-not $Unity) {
    $candidates = @()
    foreach ($root in @("C:\Program Files\Unity\Hub\Editor", "$env:ProgramFiles\Unity\Hub\Editor", "D:\Program Files\Unity\Hub\Editor") | Select-Object -Unique) {
        if (Test-Path $root) { $candidates += Get-ChildItem $root -Directory | Where-Object { $_.Name -like "6000.*" } }
    }
    $pick = $candidates | Sort-Object { [version]($_.Name -replace '[a-z].*$', '') } -Descending | Select-Object -First 1
    if ($pick) { $Unity = Join-Path $pick.FullName "Editor\Unity.exe" }
}
if (-not $Unity -or -not (Test-Path $Unity)) {
    Write-Host "Could not find Unity 6. Set the UNITY_EDITOR environment variable to the full path of Unity.exe and run again." -ForegroundColor Red
    exit 3
}
Write-Host "Using $Unity"

$commit = (& git -C $repo rev-parse HEAD 2>$null); if (-not $commit) { $commit = "unknown" }
$short = if ($commit.Length -ge 7) { $commit.Substring(0, 7) } else { $commit }
$id = (Get-Date -Format "yyyyMMdd-HHmm") + "-" + $short
$reports = Join-Path $repo "Verification\Reports"
$run = Join-Path $reports $id
if (Test-Path (Join-Path $run "test-results.json")) {
    Write-Host "A run called $id already exists. Evidence is never overwritten: wait a minute and run again." -ForegroundColor Red
    exit 3
}
New-Item -ItemType Directory -Force -Path (Join-Path $run "logs") | Out-Null
if (& git -C $repo status --porcelain 2>$null) {
    "There were changes that were not committed. The evidence is labelled with commit $short, which is not exactly what was photographed." |
        Tee-Object -FilePath (Join-Path $run "logs\uncommitted-changes.txt")
}

$context = Join-Path $reports ".current-run.json"
$list = ($Targets | ForEach-Object { '"' + $_ + '"' }) -join ","
$json = '{"buildId":"' + $id + '","commit":"' + $commit + '","level":' + $Level + ',"targets":[' + $list + '],"startedUtc":"' + (Get-Date).ToUniversalTime().ToString("o") + '"}'
[System.IO.File]::WriteAllText($context, $json)

function Run-Unity([string]$name, [string[]]$extra) {
    $log = Join-Path $run "logs\$name.log"
    Write-Host "== $name (log: $log)"
    $all = @("-batchmode", "-projectPath", "`"$project`"", "-logFile", "`"$log`"") + $extra
    $p = Start-Process -FilePath $Unity -ArgumentList $all -Wait -PassThru -NoNewWindow
    return $p.ExitCode
}

try {
    $s = Run-Unity "1-setup" @("-quit", "-executeMethod", "OrbitalAnte.EditorTools.ProjectSetup.SetUpFromCommandLine")
    Write-Host "   setup exit code $s"
    $e = Run-Unity "2-editmode" @("-runTests", "-testPlatform", "EditMode", "-testResults", "`"$(Join-Path $run 'editmode-results.xml')`"")
    Write-Host "   rule tests exit code $e"
    $play = @("-runTests", "-testPlatform", "PlayMode", "-testResults", "`"$(Join-Path $run 'playmode-results.xml')`"")
    if ($Level -eq 1) { $play += @("-testCategory", "OACapture") }
    $p = Run-Unity "3-playmode-capture" $play
    Write-Host "   Play mode exit code $p"
    $code = Run-Unity "4-summary" @("-quit", "-executeMethod", "OrbitalAnte.Verification.EditorTools.VerificationRunner.SummariseFromCommandLine", "-oaBuildId", $id, "-oaLevel", "$Level")
}
finally {
    if (Test-Path $context) { Remove-Item $context -Force }
}

$pictures = @(Get-ChildItem $run -Recurse -Filter *.jpg -ErrorAction SilentlyContinue).Count
Write-Host ""
Write-Host "Build id: $id"
Write-Host "Evidence: Verification\Reports\$id  ($pictures pictures)"
switch ($code) {
    0 { Write-Host "Every test ran and passed. Next: the independent Critic review. This is not a verdict." }
    2 { Write-Host "Tests ran and some FAILED. The Critic review must record them. This is not a verdict." }
    default { Write-Host "The run could not finish (exit $code). Read the logs in Verification\Reports\$id\logs. Any review of this run is BLOCKED." }
}
if ($pictures -eq 0) { Write-Host "No pictures were taken, so every target is BLOCKED. Common causes: a compile error (read logs\2-editmode.log)." }
exit $code
