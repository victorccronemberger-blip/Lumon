$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$exe = Join-Path $project 'Builds/HubWorld-Win64/Lumon.exe'
$design = (New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'review/design') -Force).FullName
$wide = (New-Item -ItemType Directory -Path (Join-Path $project 'Logs/HubDesignWide') -Force).FullName
$fullHd = (New-Item -ItemType Directory -Path (Join-Path $project 'Logs/HubDesign1080') -Force).FullName
$motion = (New-Item -ItemType Directory -Path (Join-Path $project 'Logs/HubPlayerMotion') -Force).FullName

$scenarios = @(
    @{ Name = 'design'; Tag = 'design'; Size = '1280 720'; Output = $design },
    @{ Name = 'design'; Tag = 'design-wide'; Size = '1440 900'; Output = $wide },
    @{ Name = 'design'; Tag = 'design-1080'; Size = '1920 1080'; Output = $fullHd },
    @{ Name = 'motion'; Tag = 'motion'; Size = '1280 720'; Output = $motion }
)
foreach ($review in $scenarios) {
    $name = $review.Name
    $tag = $review.Tag
    $output = $review.Output
    $width, $height = $review.Size -split ' '
    $log = Join-Path $project "Logs/hub-player-$tag.log"
    $arguments = "-screen-fullscreen 0 -screen-width $width -screen-height $height -force-d3d11 -lumon-hub-review-write `"$output`" -lumon-hub-review-$name -logFile `"$log`""
    $player = Start-Process -FilePath $exe -WorkingDirectory $project -ArgumentList $arguments -PassThru
    if (-not $player.WaitForExit(48000)) {
        $player.Kill()
        throw "Hub $tag review timed out. See $log"
    }
    $report = Join-Path $output "player-$name.txt"
    if (Test-Path $report) { Get-Content -LiteralPath $report }
    if ($player.ExitCode -ne 0) { throw "Hub $tag review exited $($player.ExitCode)." }
    if ($name -eq 'design' -and $tag -ne $name) {
        Copy-Item -LiteralPath $report -Destination (Join-Path $design "player-$tag.txt") -Force
    }
}
python (Join-Path $PSScriptRoot 'review_player_motion.py') $motion
if ($LASTEXITCODE -ne 0) { throw 'The player motion review failed.' }
