# One gate for the P1-H lane: build, contract checks, artifact checks, deploy freshness.
# Exits non-zero on the first failure. Pass -Deploy to also push the build into the game folder.
param(
    [switch]$Deploy,
    [string]$GameManaged = 'C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Managed',
    [string]$ModsRoot = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\Wsii.P1H\Wsii.P1H.csproj'
$tests = Join-Path $root 'tests\Wsii.P1H.Semantics'
$built = Join-Path $root 'artifacts\Release\WsiiP1H.dll'
$deployed = Join-Path $ModsRoot 'WsiiP1H\WsiiP1H.dll'

function Step([string]$name, [scriptblock]$body) {
    Write-Output ('== ' + $name)
    & $body
    if ($LASTEXITCODE -ne 0) {
        Write-Output ('GATE-FAIL ' + $name + ' exit=' + $LASTEXITCODE)
        exit 1
    }
}

Step 'build' { & dotnet build $project -c Release -v q --nologo }
Step 'semantics' { & dotnet run --project $tests -c Release --nologo }

Write-Output '== verify-build'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-assembly.ps1') -Assembly $built -GameManaged $GameManaged
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE-FAIL verify-build'; exit 1 }

if (Test-Path $deployed) {
    Write-Output '== verify-deployed'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-assembly.ps1') -Assembly $deployed -GameManaged $GameManaged
    if ($LASTEXITCODE -ne 0) { Write-Output 'GATE-FAIL verify-deployed'; exit 1 }

    function Get-Sha([string]$p) {
        $s = [System.Security.Cryptography.SHA256]::Create()
        $f = [System.IO.File]::OpenRead($p)
        try { return ([System.BitConverter]::ToString($s.ComputeHash($f)) -replace '-', '') } finally { $f.Close(); $s.Clear() }
    }
    $pairs = @(
        @((Join-Path $root 'artifacts\Release\WsiiP1H.dll'), (Join-Path $ModsRoot 'WsiiP1H\WsiiP1H.dll')),
        @((Join-Path $root 'artifacts\Release\mod.json'), (Join-Path $ModsRoot 'WsiiP1H\mod.json')),
        @((Join-Path $root 'artifacts\Release\fixtures\commands.txt'), (Join-Path $ModsRoot 'WsiiP1H\fixtures\commands.txt'))
    )
    $drift = @()
    foreach ($pair in $pairs) {
        if (-not (Test-Path $pair[1])) { $drift += (Split-Path $pair[1] -Leaf) + ':missing'; continue }
        if ((Get-Sha $pair[0]) -ne (Get-Sha $pair[1])) { $drift += (Split-Path $pair[1] -Leaf) + ':differs' }
    }
    if ($drift.Count -gt 0) {
        Write-Output ('STALE-DEPLOY ' + ($drift -join ', ') + '; run with -Deploy')
    } else {
        Write-Output 'deploy-in-sync (dll, mod.json, fixture)'
    }
} else {
    Write-Output 'deploy-absent (nothing installed to compare)'
}

if ($Deploy) {
    Write-Output '== deploy'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'deploy.ps1') -Source (Join-Path $root 'artifacts\Release') -ModsRoot $ModsRoot
    if ($LASTEXITCODE -ne 0) { Write-Output 'GATE-FAIL deploy'; exit 1 }
}

Write-Output 'GATE-PASS'
exit 0
