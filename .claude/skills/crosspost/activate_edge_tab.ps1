# Make the Edge tab whose title matches -TitleMatch the ACTIVE tab of the Edge window. Only the
# active tab's page is in the UIA tree, a screenshot of an inactive tab times out, and the
# extension's tabs_create_mcp never activates the tab it opens. Foregrounds Edge for ~1s via the
# AttachThreadInput bypass (plain SetForegroundWindow / AppActivate are refused while another
# app holds the foreground lock, e.g. Luka using his other browser), then cycles Ctrl+Tab until
# the window title matches. Edge's tab strip is NOT exposed to UIA, so keys are the only lever.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/activate_edge_tab.ps1 -TitleMatch "Create reel"
param(
    [Parameter(Mandatory)][string]$TitleMatch,
    [int]$MaxTabs = 12
)
Add-Type -Name FG -Namespace W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
[DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
[DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
'@
function Title { (Get-Process msedge -ErrorAction SilentlyContinue | Where-Object MainWindowTitle | Select-Object -First 1).MainWindowTitle }
$p = Get-Process msedge -ErrorAction SilentlyContinue | Where-Object MainWindowTitle | Select-Object -First 1
if (-not $p) { Write-Output "NO_EDGE_WINDOW"; exit 1 }
if ((Title) -match $TitleMatch) { Write-Output "ALREADY_ACTIVE: $(Title)"; exit 0 }
$h = $p.MainWindowHandle
$fg = [W.FG]::GetForegroundWindow(); $pid0 = 0
$ft = [W.FG]::GetWindowThreadProcessId($fg, [ref]$pid0)
$me = [W.FG]::GetCurrentThreadId()
[void][W.FG]::AttachThreadInput($me, $ft, $true)
[void][W.FG]::ShowWindow($h, 9); [void][W.FG]::BringWindowToTop($h); [void][W.FG]::SetForegroundWindow($h)
[void][W.FG]::AttachThreadInput($me, $ft, $false)
Start-Sleep -Milliseconds 500
if ([W.FG]::GetForegroundWindow() -ne $h) { Write-Output "FOREGROUND_FAILED"; exit 1 }
$ws = New-Object -ComObject WScript.Shell
for ($i = 0; $i -lt $MaxTabs; $i++) {
    if ((Title) -match $TitleMatch) { Write-Output "ACTIVE: $(Title)"; exit 0 }
    $ws.SendKeys('^{TAB}'); Start-Sleep -Milliseconds 600
}
Write-Output "NO_TAB_MATCHING '$TitleMatch' (last: $(Title))"; exit 1
