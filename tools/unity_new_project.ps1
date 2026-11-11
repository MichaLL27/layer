# Creates the Unity project WeMadeIt_Unity (once). Close other Unity editors first.
# Usage: ./tools/unity_new_project.ps1
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe'
$proj = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path + '\WeMadeIt_Unity'
$log = (Resolve-Path (Join-Path $PSScriptRoot '..\output')).Path + '\unity_create.log'

if (-not (Test-Path $unity)) { Write-Error "Unity 6000.4.1f1 not found at $unity"; exit 1 }
if (Test-Path "$proj\ProjectSettings") { Write-Host "Already exists: $proj"; exit 0 }

Write-Host "Creating $proj (this takes a few minutes) ..."
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-createProject', "`"$proj`"", '-logFile', "`"$log`"") -PassThru -Wait
if ($p.ExitCode -ne 0 -or -not (Test-Path "$proj\ProjectSettings")) {
    Write-Error "Unity exited with code $($p.ExitCode). See $log"
    exit 1
}
Write-Host "Created. Next: add packages (Recorder, Timeline, Cinemachine, TextMeshPro) - see docs/PIPELINE.md"
