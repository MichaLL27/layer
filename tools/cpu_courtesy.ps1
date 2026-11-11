<#
  tools/cpu_courtesy.ps1 -- keep the PC usable while a build runs.

  Luka, 2026-09-19: the PC froze during a Unity build (every thread busy on ffmpeg). "When I'm
  not telling you this is an overnight run, leave at least one or two threads free, in case I
  open a browser."

  DOT-SOURCE IT at the top of a build script:
      . (Join-Path $PSScriptRoot 'tools\cpu_courtesy.ps1')
  It pins the CURRENT PowerShell process to every logical CPU but the last few and drops it to
  BelowNormal. Windows hands both to every child the build starts -- Blender, Unity, ffmpeg,
  python -- so the whole build tree stays off those threads and yields to anything interactive
  on the rest.

  HOW MANY IT LEAVES FREE IS READ FROM THE PC, NEVER HARDCODED (Luka, 2026-09-21: "this project
  also runs on my brother's PC and maybe it does not have sixteen threads"). The count comes from
  [Environment]::ProcessorCount at dot-source time:
      >= 8 threads -> 2 free   (Luka's 16-thread Ryzen: the build gets 14)
      4-7 threads  -> 1 free   (2 of 6 would cost a third of a small machine)
      < 4 threads  -> 0 free   (nothing to spare; BelowNormal still applies)
  Reading it costs nothing, so every build does it. What a build does NOT do is VERIFY that a
  real child process inherited the mask and the priority: that is tools/check_cpu_courtesy.ps1,
  run ONCE per PC and remembered in a marker (rule 4b -- this file is the builder, and a builder
  cannot check itself; the line it prints below is an intention, not a measurement).

  OVERNIGHT, and only when Luka says the run is overnight:   $env:BPS_OVERNIGHT = '1'
  -> every thread. Priority stays BelowNormal: it costs nothing when nobody else wants the CPU.

  Idempotent, so nested scripts may each dot-source it. A script that does NOT is covered by
  launching it from a session that has:
      . ./tools/cpu_courtesy.ps1; ./series_old_thing.ps1
#>
$__bpsN = [Environment]::ProcessorCount
if ($env:BPS_OVERNIGHT -eq '1') { $__bpsFree = 0 }
elseif ($__bpsN -ge 8)          { $__bpsFree = 2 }
elseif ($__bpsN -ge 4)          { $__bpsFree = 1 }
else                            { $__bpsFree = 0 }
try {
    $__bpsProc = [System.Diagnostics.Process]::GetCurrentProcess()
    $__bpsProc.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
    # > 62 threads would overflow the shift below; such a PC keeps every thread (and BelowNormal).
    if ($__bpsFree -gt 0 -and $__bpsN -le 62 -and $__bpsN -gt $__bpsFree + 1) {
        $__bpsProc.ProcessorAffinity = [IntPtr](([int64]1 -shl ($__bpsN - $__bpsFree)) - 1)
    }
    if (-not $global:BpsCourtesyShown) {
        $__bpsNote = if ($__bpsFree -eq 0) {
            if ($env:BPS_OVERNIGHT -eq '1') { ' (OVERNIGHT: all threads)' } else { ' (too few threads to spare one)' }
        } else { " ($__bpsFree left free)" }
        Write-Host ("cpu courtesy: build uses {0} of {1} threads, BelowNormal{2}" -f ($__bpsN - $__bpsFree), $__bpsN, $__bpsNote) -ForegroundColor DarkGray
        $global:BpsCourtesyShown = $true
    }
}
catch {
    Write-Host "cpu courtesy: could not set affinity/priority ($_)" -ForegroundColor Yellow
}
