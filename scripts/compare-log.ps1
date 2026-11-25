# Cross-checks the in-game admission log against the offline contract run.
# The offline verdicts come from the same fixture the mod replays; the game verdicts come from p1h.log.
param(
    [string]$Log = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\WsiiP1H\p1h.log'),
    [string]$Project = (Join-Path $PSScriptRoot '..\tests\Wsii.P1H.Semantics')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Log)) { Write-Output ('FAIL log-missing ' + $Log); exit 1 }

$offline = @()
$emitOutput = & dotnet run --project $Project -c Release -- emit 2>&1
foreach ($line in $emitOutput) {
    if ("$line" -match '^VERDICT\|(.*)\|(.*)$') { $offline += ($Matches[1] + '|' + $Matches[2]) }
}
if ($offline.Count -eq 0) { Write-Output 'FAIL no-offline-verdicts'; exit 1 }

$ingame = @()
foreach ($line in (Get-Content $Log)) {
    if ($line -match '\[WsiiP1H\] admit id=(.*?) type=\S* result=(.*)$') {
        $ingame += ($Matches[1] + '|' + $Matches[2])
    }
}
if ($ingame.Count -eq 0) { Write-Output 'FAIL no-ingame-verdicts'; exit 1 }

Write-Output ('offline=' + $offline.Count + ' ingame=' + $ingame.Count)

$failures = 0
if ($offline.Count -ne $ingame.Count) {
    Write-Output ('FAIL count-mismatch offline=' + $offline.Count + ' ingame=' + $ingame.Count)
    $failures++
}

$limit = [Math]::Min($offline.Count, $ingame.Count)
for ($i = 0; $i -lt $limit; $i++) {
    if ($offline[$i] -ceq $ingame[$i]) {
        Write-Output ('ok   [' + $i + '] ' + $offline[$i])
    } else {
        Write-Output ('FAIL [' + $i + '] offline=' + $offline[$i] + ' ingame=' + $ingame[$i])
        $failures++
    }
}

if ($failures -eq 0) { Write-Output 'PASS in-game verdicts match the offline contract exactly'; exit 0 }
Write-Output ('FAIL ' + $failures + ' mismatch(es)')
exit 1
