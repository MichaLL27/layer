<#
  tools/desktop_isolate.ps1 -- run a build process on a SEPARATE Windows desktop object, so it
  cannot touch Luka's screen, focus or fullscreen apps.

  WHY. Unity's editor steals the foreground on Windows -- a bug open since ~2016 and still present
  in the Unity 6 line (issuetracker: "Unity progress bar steals focus from other windows"). While a
  build runs, the text caret vanishes mid-sentence and a fullscreen video drops to show the
  taskbar. Unity will not fix it, and Windows will not stop it: a process LAUNCHED BY the current
  foreground process inherits the right to take the foreground, so ForegroundLockTimeout never
  bites (and Windows 11 ignores that value anyway).

  So we take the screen away from it instead. A window on another DESKTOP OBJECT cannot appear on,
  or take focus from, the interactive desktop -- it is structural, not a setting Unity can
  override. Same window station (WinSta0), so the GPU, the licence and the file system are
  unchanged; only windows and input are separated. This is NOT session-0 isolation.

  USE IT:
      . (Join-Path $PSScriptRoot 'desktop_isolate.ps1')
      $code = Invoke-BpsOnIsolatedDesktop -FilePath $exe -ArgumentList @('-a','-b')

  It blocks until the process exits and returns the exit code, like Start-Process -Wait -PassThru.
  The child writes no console output here -- give the engine a -logFile (unity_render.ps1 does).

  PROVE IT:  ./tools/desktop_isolate.ps1 -Selftest
  Launches a real window (notepad) on the isolated desktop and checks it is absent from the
  interactive desktop's window list, then checks the same window IS visible when launched normally
  -- a check that has been seen to fail is the only kind worth having (CLAUDE.md rule 4b).
#>
param([switch]$Selftest)

if (-not ('BpsDesktop' -as [type])) {
    Add-Type -Namespace '' -Name 'BpsDesktop' -MemberDefinition @'
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO {
        public int cb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpReserved;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDesktop;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateDesktop(string desktop, IntPtr device, IntPtr devmode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool CloseDesktop(IntPtr hDesktop);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(string app, System.Text.StringBuilder cmd, IntPtr pa, IntPtr ta,
        bool inherit, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr h);
'@ -UsingNamespace 'System.Text'
}

function Invoke-BpsOnIsolatedDesktop {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [string]$DesktopName = 'BpsRender',
        [string]$WorkingDirectory = $PWD.Path,
        [switch]$NoWait               # selftest only: return the PID without waiting
    )

    # The desktop object lives as long as a handle to it is open. Closing ours the moment a
    # -NoWait child was created DESTROYED the desktop under the still-starting process and it died
    # (measured 2026-09-21). So the handle is created once and kept for the session.
    if (-not $script:BpsDesktops) { $script:BpsDesktops = @{} }
    if (-not $script:BpsDesktops.ContainsKey($DesktopName)) {
        $GENERIC_ALL = 0x10000000
        $h = [BpsDesktop]::CreateDesktop($DesktopName, [IntPtr]::Zero, [IntPtr]::Zero, 0, $GENERIC_ALL, [IntPtr]::Zero)
        if ($h -eq [IntPtr]::Zero) {
            throw "CreateDesktop('$DesktopName') failed: $([ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()).Message)"
        }
        $script:BpsDesktops[$DesktopName] = $h
    }

    # CreateProcess may WRITE to lpCommandLine, so it must be a mutable buffer (StringBuilder).
    # argv[0] is the exe, quoted; every argument already carrying quotes is passed through as is.
    $parts = @("`"$FilePath`"") + ($ArgumentList | ForEach-Object { if ($_ -match '^".*"$' -or $_ -notmatch '\s') { $_ } else { "`"$_`"" } })
    $cmd = New-Object System.Text.StringBuilder (($parts -join ' '), 32768)

    $si = New-Object BpsDesktop+STARTUPINFO
    $si.cb = [Runtime.InteropServices.Marshal]::SizeOf([type][BpsDesktop+STARTUPINFO])
    $si.lpDesktop = $DesktopName          # <- the whole point
    $pi = New-Object BpsDesktop+PROCESS_INFORMATION

    $CREATE_NO_WINDOW = 0x08000000
    $ok = [BpsDesktop]::CreateProcess($FilePath, $cmd, [IntPtr]::Zero, [IntPtr]::Zero, $false,
                                      $CREATE_NO_WINDOW, [IntPtr]::Zero, $WorkingDirectory, [ref]$si, [ref]$pi)
    if (-not $ok) {
        throw "CreateProcess failed: $([ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error()).Message)"
    }

    try {
        if ($NoWait) { return $pi.dwProcessId }
        [void][BpsDesktop]::WaitForSingleObject($pi.hProcess, [uint32]::MaxValue)
        $code = 0
        [void][BpsDesktop]::GetExitCodeProcess($pi.hProcess, [ref]$code)
        return [int]$code
    }
    finally {
        [void][BpsDesktop]::CloseHandle($pi.hThread)
        if (-not $NoWait) { [void][BpsDesktop]::CloseHandle($pi.hProcess) }
    }
}

if ($Selftest) {
    $ErrorActionPreference = 'Stop'
    $fail = 0
    # Our OWN window, not a shipped app: on Windows 11 notepad/calc redirect to their Store
    # versions and the launched process exits at once, so the test measured a dead process.
    $ps1 = Join-Path $env:TEMP 'bps_desktop_selftest_window.ps1'
    Set-Content -Path $ps1 -Encoding utf8 -Value @'
Add-Type -AssemblyName System.Windows.Forms
$f = New-Object System.Windows.Forms.Form
$f.Text = 'BPS DESKTOP SELFTEST'
$f.ShowDialog()
'@
    $exe  = (Get-Command powershell.exe).Source
    $argv = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $ps1)

    function Get-WindowHandle([int]$procId) {
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if (-not $p) { return $null }          # dead: the test proves nothing
        $p.Refresh()
        return [int64]$p.MainWindowHandle      # 0 = no window on THIS desktop
    }

    try {
        Write-Host "1. a window on the ISOLATED desktop must NOT appear on this one"
        $id1 = Invoke-BpsOnIsolatedDesktop -FilePath $exe -ArgumentList $argv -NoWait
        Start-Sleep -Seconds 5
        $h1 = Get-WindowHandle $id1
        if ($null -eq $h1) { Write-Host "   FAIL: the process died, nothing was measured"; $fail++ }
        elseif ($h1 -ne 0) { Write-Host "   FAIL: its window IS on this desktop (handle $h1)"; $fail++ }
        else { Write-Host "   OK: alive (pid $id1) with no window on this desktop" }
        if ($h1 -ne $null) { Stop-Process -Id $id1 -Force -ErrorAction SilentlyContinue }

        Write-Host "2. the SAME window launched NORMALLY must be visible -- proves test 1 can fail"
        $p2 = Start-Process $exe -ArgumentList $argv -PassThru
        Start-Sleep -Seconds 5
        $h2 = Get-WindowHandle $p2.Id
        if ($null -eq $h2) { Write-Host "   FAIL: the process died, nothing was measured"; $fail++ }
        elseif ($h2 -eq 0) { Write-Host "   FAIL: invisible either way, so test 1 proves nothing"; $fail++ }
        else { Write-Host "   OK: window present (handle $h2)" }
        if ($h2 -ne $null) { Stop-Process -Id $p2.Id -Force -ErrorAction SilentlyContinue }
    }
    finally { Remove-Item $ps1 -ErrorAction SilentlyContinue }

    if ($fail) { Write-Host "SELFTEST FAILED ($fail)" -ForegroundColor Red; exit 1 }
    Write-Host "SELFTEST PASSED" -ForegroundColor Green
}
