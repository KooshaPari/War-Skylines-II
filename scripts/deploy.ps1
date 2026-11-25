# Deploys the built mod into the local Cities: Skylines II Mods folder.
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\artifacts\Release'),
    [string]$ModsRoot = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods'),
    [string]$Name = 'WsiiP1H'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $Source 'WsiiP1H.dll'))) { throw "missing build output: $Source\WsiiP1H.dll" }
if (-not (Test-Path $ModsRoot)) { throw "mods folder not found: $ModsRoot" }

$dest = Join-Path $ModsRoot $Name
New-Item -ItemType Directory -Force -Path $dest | Out-Null

Copy-Item (Join-Path $Source 'WsiiP1H.dll') $dest -Force
Copy-Item (Join-Path $Source 'mod.json') $dest -Force

$fixtures = Join-Path $Source 'fixtures'
$destFixtures = Join-Path $dest 'fixtures'
New-Item -ItemType Directory -Force -Path $destFixtures | Out-Null
Copy-Item (Join-Path $fixtures 'commands.txt') $destFixtures -Force

Remove-Item (Join-Path $dest 'p1h.log') -ErrorAction SilentlyContinue

Write-Output ("deployed " + $dest)
Get-ChildItem -Recurse $dest | ForEach-Object {
    Write-Output ("  " + $_.FullName.Substring($dest.Length + 1) + " " + $_.Length + " bytes")
}
