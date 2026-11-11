<#
  tools/gpu_lock.ps1 -- ONE GPU render at a time, across every project on this PC.

  WHY. Renders are GPU-bound and the card is the cap (docs/SCENES.md section 4, measured on the
  RX 6800 XT): two concurrent 4K lanes each run at 2.6 s/frame against 1.254 s/frame alone -- the
  same total throughput, just later for both -- and at four or five lanes the 4K buffers overflow
  the card's 16 GB into shared system RAM and aggregate throughput COLLAPSES to about 0.23
  frames/s, worse than a single lane. So renders queue; they never share.

  WHAT IT LOCKS: the GPU render ONLY. Bakes, PhysX simulation and the x264 derive are CPU and
  overlap freely (docs/SCENES.md: "bakes are CPU and overlap freely") -- holding the lock through
  a 3.5 min derive would idle the GPU for nothing. unity_render.ps1 and run.ps1 take it around the
  engine call; tools/series_driver.ps1 -DeriveOnly runs OUTSIDE it, so the next video's render
  overlaps the previous video's derive.

  USE IT:
      . (Join-Path $PSScriptRoot 'tools\gpu_lock.ps1')
      Invoke-BpsGpuLock -Tag 'my_scene' { ...the render... }

  A WAITING RUN COSTS NOTHING. It sleeps inside the OS wait at zero CPU and wakes the instant the
  holder releases -- no polling, no estimated timer. Windows does not promise strict FIFO among
  waiters; it is arrival order in practice, but nothing should depend on exact order.

  IF A RENDER DIES (crash, Ctrl-C, closed window) Windows releases the mutex itself and tells the
  next waiter it was abandoned. There is no stale lock to clear by hand -- which is the whole
  reason this is a mutex and not a lock FILE.

  THE NAME IS THE CONTRACT. Every project on the PC -- satisfying-3d-sims, kids-cars, bot-league,
  particle-factory -- must use the SAME string or the queue silently splits in two. Each repo
  keeps its own COPY of this file (they are separate git repos; bot-league/CLAUDE.md: copy what
  you need, do not import across repos). Change the name in one, change it in all.
#>
$BpsGpuLockName = 'Global\YouTubeGpuRender'

function Invoke-BpsGpuLock {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Body,
        [string]$Tag = 'render'
    )

    $name = $BpsGpuLockName
    try { $mutex = New-Object System.Threading.Mutex($false, $name) }
    catch {
        # Creating a Global object needs SeCreateGlobalPrivilege. Every process on the PC fails
        # this the same way, so they all land on the session-local name together and still queue.
        $name = $BpsGpuLockName -replace '^Global\\', ''
        Write-Host "gpu lock: no Global mutex on this account, using session-local '$name'." -ForegroundColor Yellow
        $mutex = New-Object System.Threading.Mutex($false, $name)
    }

    $held = $false
    try {
        try {
            if ($mutex.WaitOne(0)) {
                $held = $true
            }
            else {
                Write-Host ("gpu lock: another render holds the GPU -- waiting ({0})..." -f $Tag) -ForegroundColor DarkGray
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                $mutex.WaitOne() | Out-Null
                $held = $true
                Write-Host ("gpu lock: waited {0:N0} s, starting ({1})" -f $sw.Elapsed.TotalSeconds, $Tag) -ForegroundColor DarkGray
            }
        }
        catch [System.Threading.AbandonedMutexException] {
            $held = $true
            Write-Host "gpu lock: the previous render died without releasing -- taking the lock." -ForegroundColor Yellow
        }
        & $Body
    }
    finally {
        if ($held) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}
