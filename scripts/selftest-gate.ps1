# Self-test for the P1-H gate: proves the gate can actually fail.
# Injects three faults against scratch copies so the real install is never touched.
param(
    [string]$GameManaged = 'C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Managed'
)

# Child scripts write diagnostics to stderr on purpose; assert on exit codes, not on errors.
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent
$base = $env:KCODE_SCRATCH_DIR
if (-not $base) { $base = $env:TEMP }
$scratch = Join-Path $base 'wsii-gate-selftest'
if (Test-Path $scratch) { Remove-Item $scratch -Recurse -Force }
New-Item -ItemType Directory -Path $scratch | Out-Null

$failures = 0
function Expect([string]$name, [int]$expected, [int]$actual) {
    if ($expected -eq $actual) {
        Write-Output ('ok   ' + $name + ' exit=' + $actual)
    } else {
        Write-Output ('FAIL ' + $name + ' expected-exit=' + $expected + ' actual=' + $actual)
        $script:failures++
    }
}

# Fault 1: stale install. Copy the good deployment, then alter one shipped file only.
$modsRoot = Join-Path $scratch 'Mods'
$dest = Join-Path $modsRoot 'WsiiP1H'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $dest 'fixtures') -Force | Out-Null
Copy-Item (Join-Path $root 'artifacts\Release\WsiiP1H.dll') $dest -Force
Copy-Item (Join-Path $root 'artifacts\Release\mod.json') $dest -Force
Copy-Item (Join-Path $root 'artifacts\Release\fixtures\commands.txt') (Join-Path $dest 'fixtures') -Force
Add-Content -Path (Join-Path $dest 'fixtures\commands.txt') -Value '# local drift'

$out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check.ps1') -ModsRoot $modsRoot -GameManaged $GameManaged 2>&1
$stale = @($out | Where-Object { "$_" -like 'STALE-DEPLOY*' }).Count
if ($stale -eq 1) { Write-Output 'ok   stale-install-detected' } else { Write-Output 'FAIL stale-install-detected'; $script:failures++ }

# Fault 2: no game assemblies to bind against.
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check.ps1') -ModsRoot $modsRoot -GameManaged (Join-Path $scratch 'no-such-dir') 2>&1
$gateFail = @($out | Where-Object { "$_" -like 'GATE-FAIL*' }).Count
if ($gateFail -ge 1) { Write-Output 'ok   missing-game-abi-fails-gate' } else { Write-Output 'FAIL missing-game-abi-fails-gate'; $script:failures++ }

# Fault 3: verification must reject an assembly that is not our mod.
$notOurMod = Join-Path $GameManaged 'Colossal.Core.dll'
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-assembly.ps1') -Assembly $notOurMod -GameManaged $GameManaged 2>&1
$last = $LASTEXITCODE
Expect 'verify-rejects-foreign-assembly' 1 $last

# Fault 4: a corrupted binary must not be accepted as loadable.
$broken = Join-Path $scratch 'broken.dll'
Copy-Item (Join-Path $root 'artifacts\Release\WsiiP1H.dll') $broken -Force
$bytes = [System.IO.File]::ReadAllBytes($broken)
for ($i = 0; $i -lt 64; $i++) { $bytes[$i] = 0 }
[System.IO.File]::WriteAllBytes($broken, $bytes)
$out = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-assembly.ps1') -Assembly $broken -GameManaged $GameManaged 2>&1
$last = $LASTEXITCODE
if ($last -ne 0) { Write-Output ('ok   corrupt-binary-rejected exit=' + $last) } else { Write-Output 'FAIL corrupt-binary-rejected'; $script:failures++ }

Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue

if ($failures -eq 0) { Write-Output 'SELFTEST-PASS gate rejects stale, missing-abi, foreign and corrupt inputs'; exit 0 }
Write-Output ('SELFTEST-FAIL ' + $failures + ' check(s)')
exit 1
