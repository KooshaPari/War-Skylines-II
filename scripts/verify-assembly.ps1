# Verifies the built mod assembly against the installed Cities: Skylines II assemblies.
# Read-only: loads metadata reflection-only, executes nothing.
param(
    [string]$Assembly = (Join-Path $PSScriptRoot '..\artifacts\Release\WsiiP1H.dll'),
    [string]$GameManaged = 'C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Managed'
)

$ErrorActionPreference = 'Stop'
$failures = 0

function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { Write-Output ("ok   " + $name + " " + $detail) }
    else { Write-Output ("FAIL " + $name + " " + $detail); $script:failures++ }
}

if (-not (Test-Path $Assembly)) { Write-Output ("FAIL assembly-missing " + $Assembly); exit 1 }
if (-not (Test-Path (Join-Path $GameManaged 'Game.dll'))) { Write-Output ("FAIL game-dll-missing " + $GameManaged); exit 1 }

$resolver = {
    param($s, $e)
    $name = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $GameManaged ($name + '.dll')
    if (Test-Path $p) { return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($p) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)

$mod = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path $Assembly))
$game = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $GameManaged 'Game.dll'))

$target = $mod.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' } | Select-Object -First 1
Check 'target-framework' ($target -ne $null -and $target.ConstructorArguments[0].Value -like '.NETFramework,Version=v4.8*') ([string]$target.ConstructorArguments[0].Value)

$imod = $game.GetType('Game.Modding.IMod', $false)
Check 'game-imod-resolved' ($imod -ne $null) 'Game.Modding.IMod'

$modType = $mod.GetType('Wsii.P1H.Mod', $false)
Check 'entry-type' ($modType -ne $null) 'Wsii.P1H.Mod'

$implements = $false
if ($modType) {
    $implements = @($modType.GetInterfaces() | Where-Object { $_.FullName -eq 'Game.Modding.IMod' }).Count -eq 1
}
Check 'implements-imod' $implements 'Game.Modding.IMod'

$sysType = $mod.GetType('Wsii.P1H.P1HProbeSystem', $false)
$derives = $false
if ($sysType) { $derives = $sysType.BaseType.FullName -eq 'Game.GameSystemBase' }
Check 'system-base' $derives ('GameSystemBase <- ' + $(if ($sysType) { $sysType.BaseType.FullName } else { 'type-missing' }))

$refsGame = @($mod.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'Game' }).Count -eq 1
Check 'references-game' $refsGame 'Game'

$resources = @($mod.GetManifestResourceNames())
$hasFixture = $resources -contains 'Wsii.P1H.commands.txt'
Check 'embedded-fixture' $hasFixture ($resources -join ',')

if ($failures -eq 0) { Write-Output 'PASS mod assembly is loadable and API-correct' ; exit 0 }
Write-Output ("FAIL " + $failures + " check(s)")
exit 1
