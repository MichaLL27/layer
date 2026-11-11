# Runs a Unity batch method of WeMadeIt_Unity and waits. Close any open Unity editor on the project first.
# Shared rules: BelowNormal + spare threads (cpu_courtesy), one GPU render at a time across all channels
# (gpu_lock), Unity on its own Windows desktop (desktop_isolate).
# Usage:
#   ./tools/unity_render.ps1 -UnityArgs @('-wellScene','tunnel','-wellOut','output/tunnel/video.mp4')
#   ./tools/unity_render.ps1 -UnityArgs @('-wellScene','tunnel','-wellOut','output/tunnel/stills/x.png','-wellStills','6.5,12.1','-wellScale','0.5','-wellSS','1')
param(
    [string]$Method = 'WellRender.Run',
    [string[]]$UnityArgs = @(),
    [string]$Log = 'output/unity_render.log'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cpu_courtesy.ps1')
. (Join-Path $PSScriptRoot 'gpu_lock.ps1')
. (Join-Path $PSScriptRoot 'desktop_isolate.ps1')
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe'
$proj = Join-Path $root 'WeMadeIt_Unity'
$logPath = Join-Path $root $Log
New-Item -ItemType Directory -Force (Split-Path $logPath) | Out-Null

# relative output paths -> absolute (Unity's working directory is not ours)
$fixed = @()
for ($i = 0; $i -lt $UnityArgs.Count; $i++) {
    $a = $UnityArgs[$i]
    if ($i -gt 0 -and $UnityArgs[$i - 1] -eq '-wellOut' -and -not [IO.Path]::IsPathRooted($a)) { $a = Join-Path $root $a }
    $fixed += $a
}
$quoted = @($fixed | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } })
$argv = @('-batchmode', '-quit', '-projectPath', "`"$proj`"", '-executeMethod', $Method, '-logFile', "`"$logPath`"") + $quoted
$exitCode = Invoke-BpsGpuLock -Tag $Method {
    $script:sw = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-BpsOnIsolatedDesktop -FilePath $unity -ArgumentList $argv -WorkingDirectory $root
}
Write-Host ("Unity exited {0} after {1:n0}s. Log: {2}" -f $exitCode, $sw.Elapsed.TotalSeconds, $logPath)
Select-String -Path $logPath -Pattern 'error CS|WellRender failed|RENDER DONE|STILL |BUILD |Tw:|Exception|missing:' | Select-Object -First 30 | ForEach-Object { $_.Line }
exit $exitCode
