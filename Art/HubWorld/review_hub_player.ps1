$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$exe = Join-Path $project 'Builds/HubWorld-Win64/Lumon.exe'
$review = Join-Path $project 'Art/HubWorld/review'
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null

foreach ($mode in @('write', 'reload', 'wide')) {
    $width = if ($mode -eq 'write') { 1280 } elseif ($mode -eq 'wide') { 1440 } else { 1920 }
    $height = if ($mode -eq 'write') { 720 } elseif ($mode -eq 'wide') { 900 } else { 1080 }
    $runLog = Join-Path $logs "hubworld-player-$mode.log"
    $scenario = if ($mode -eq 'wide') { '-lumon-hub-review-reload' } else { "-lumon-hub-review-$mode" }
    $arguments = "-screen-fullscreen 0 -screen-width $width -screen-height $height -force-d3d11 $scenario `"$review`" -logFile `"$runLog`""
    if ($mode -eq 'wide') { $arguments += ' -lumon-hub-review-wide' }
    $player = Start-Process -FilePath $exe -WorkingDirectory $project -ArgumentList $arguments -PassThru
    if (-not $player.WaitForExit(28000)) {
        $player.Kill()
        throw "Hub player $mode timed out. See $runLog"
    }
    if ($player.ExitCode -ne 0) {
        throw "Hub player $mode returned $($player.ExitCode). See $runLog"
    }
}
foreach ($report in @('player-smoke.txt', 'player-reload.txt', 'player-wide.txt', 'player-audio.txt')) {
    Get-Content -LiteralPath (Join-Path $review $report)
}
