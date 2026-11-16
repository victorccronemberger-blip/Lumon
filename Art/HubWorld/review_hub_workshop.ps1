$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$exe=Join-Path $project 'Builds/HubWorld-Win64/Lumon.exe'
$output=(New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'review/workshop') -Force).FullName
foreach($scenario in @(@{ Flag='write'; Report='play' },@{ Flag='reload'; Report='reload' })){
    $log=Join-Path $project ("Logs/workshop-"+$scenario.Report+".log")
    $args="-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -force-d3d11 -lumon-hub-review-$($scenario.Flag) `"$output`" -lumon-hub-review-workshop -logFile `"$log`""
    $p=Start-Process -FilePath $exe -WorkingDirectory $project -ArgumentList $args -PassThru
    if(-not $p.WaitForExit(42000)){ $p.Kill(); throw "Workshop review timed out. See $log" }
    Get-Content -LiteralPath (Join-Path $output ("workshop-"+$scenario.Report+".txt"))
    if($p.ExitCode -ne 0){ throw "Workshop review exited $($p.ExitCode). See $log" }
}
