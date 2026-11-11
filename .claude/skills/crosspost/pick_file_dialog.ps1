# Put a local file into a browser <input type=file> by driving the REAL Windows "Open" dialog.
# Needed because the Claude-in-Chrome extension's file_upload tool caps at 10 MB and its own
# synthetic click never opens the OS picker. Part of the shared crosspost skill, beside
# activate_edge_tab.ps1 and verify_local_browser.ps1.
#
# Usual use: start this WITHOUT -InvokeButton in the background (it polls for the dialog), then
# click the page's upload button with the extension's `computer` click, a trusted gesture that
# opens the real dialog:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/pick_file_dialog.ps1 `
#       -Path "C:\full\path\clip.mp4" -TimeoutS 30
# -InvokeButton "<accessible name>" -WindowTitleMatch "<title regex>" presses the button itself
# through UI Automation instead (the tab must be the ACTIVE tab of that window); Edge often keeps
# the page out of UIA, and then it prints NO_BUTTON.
# -CloseDialogs closes every open file dialog and error box of the browser (WM_CLOSE) and exits:
# run it after a failed attempt, before trying again.
#
# 1. (optional) UIA Invoke of the page button named -InvokeButton.
# 2. Wait for a visible #32770 dialog owned by msedge/chrome (any title except "Rename") and
#    bring it to the foreground; FOREGROUND_FAILED if Windows refuses (typing would go elsewhere).
# 3. Fill it with REAL mouse clicks located through UIA bounding rectangles: click the "File name"
#    box, HOME+SHIFT+END, type the path, click the "Open" button.
#    (Alt+N/Enter/UIA ValuePattern were all tried: keys can land on the file list and start an
#    inline rename; ValuePattern.SetValue times out on Edge's dialog.)
# Output lines: WINDOW / INVOKED / DIALOG_FOUND / FILENAME_CLICKED / TYPED / OPEN_CLICKED /
# DIALOG_CLOSED. Exit 1 on any failure. Set PICK_DEBUG_DIR to get desktop screenshots
# (dlg1_focused / dlg2_typed / dlg3_after_open .png).
param(
    [string]$Path = "",
    [int]$TimeoutS = 40,
    [string]$InvokeButton = "",
    [string]$WindowTitleMatch = "Microsoft.*Edge",
    [switch]$CloseDialogs
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
public static class W32 {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    // Plain SetForegroundWindow is REFUSED while another app owns the foreground lock
    // (Luka working in his other browser). Same AttachThreadInput bypass as
    // activate_edge_tab.ps1. Returns false if the dialog still is not focused --
    // the caller MUST abort then, or the typed path lands in someone else's window.
    public static bool ForceForeground(IntPtr h) {
        IntPtr fg = GetForegroundWindow();
        uint pid; uint ft = GetWindowThreadProcessId(fg, out pid);
        uint me = GetCurrentThreadId();
        AttachThreadInput(me, ft, true);
        ShowWindow(h, 9); BringWindowToTop(h); SetForegroundWindow(h);
        AttachThreadInput(me, ft, false);
        System.Threading.Thread.Sleep(400);
        return GetForegroundWindow() == h;
    }
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
    public static void Click(int x, int y) {
        SetCursorPos(x, y); System.Threading.Thread.Sleep(80);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); System.Threading.Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }
    // The top-most visible dialog (#32770) of the browser: the file dialog, or the error box
    // above it. EnumWindows walks top-level windows in Z-order, so an error box comes first.
    public static IntPtr FindBrowserDialog() {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, lp) => {
            if (!IsWindowVisible(h)) return true;
            var c = new StringBuilder(64); GetClassName(h, c, 64);
            if (c.ToString() != "#32770") return true;
            var t = new StringBuilder(256); GetWindowText(h, t, 256);
            if (t.ToString() == "Rename") return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            string pn = "";
            try { pn = Process.GetProcessById((int)pid).ProcessName.ToLower(); } catch {}
            if (pn.Contains("msedge") || pn.Contains("chrome")) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

# --- -CloseDialogs: clear error boxes and zombie dialogs left by a failed attempt -----------
if ($CloseDialogs) {
    $closed = 0
    for ($i = 0; $i -lt 10; $i++) {
        $d = [W32]::FindBrowserDialog()
        if ($d -eq [IntPtr]::Zero) { Write-Output "CLOSED $closed"; exit 0 }
        [void][W32]::PostMessage($d, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)   # WM_CLOSE
        $closed++
        Start-Sleep -Milliseconds 400
    }
    Write-Output "STILL_OPEN after $closed closes (check a desktop screenshot)"
    exit 1
}
if ($Path -eq "" -or -not (Test-Path -LiteralPath $Path)) { Write-Output "NO_SUCH_FILE $Path"; exit 1 }

function Snap($name) {
    if ($env:PICK_DEBUG_DIR) {
        $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
        $bmp.Save((Join-Path $env:PICK_DEBUG_DIR "$name.png"))
        $g.Dispose(); $bmp.Dispose()
    }
}
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
function Cond($prop, $val) { New-Object System.Windows.Automation.PropertyCondition($prop, $val) }

# Edge does NOT build its renderer accessibility tree until a client asks the render
# widget for one -- until then the whole PAGE is missing from UIA (no Document, no page
# buttons) and -InvokeButton reports NO_BUTTON on a perfectly good page. Asking each
# Chrome_RenderWidgetHostHWND child for its IAccessible turns it on; it stays on.
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class A11Y {
    [DllImport("oleacc.dll")] public static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumChildProc cb, IntPtr lp);
    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    public static int Enable(IntPtr parent) {
        int n = 0;
        EnumChildWindows(parent, (h, lp) => {
            var c = new StringBuilder(128); GetClassName(h, c, 128);
            if (c.ToString().Contains("Chrome_RenderWidgetHostHWND")) {
                Guid iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71"); // IAccessible
                object o; try { AccessibleObjectFromWindow(h, 0xFFFFFFFC, ref iid, out o); n++; } catch {}
            }
            return true;
        }, IntPtr.Zero);
        return n;
    }
}
"@

# --- 1. optional: invoke the page button for real -------------------------------
if ($InvokeButton -ne "") {
    $proc = Get-Process msedge, chrome -ErrorAction SilentlyContinue | Where-Object MainWindowTitle | Select-Object -First 1
    if ($proc) {
        $poked = [A11Y]::Enable($proc.MainWindowHandle)
        Write-Output "A11Y_ENABLED $poked"
        Start-Sleep -Milliseconds 1500
    }
    $root = $AE::RootElement
    $wins = $root.FindAll($TS::Children, (Cond $AE::ControlTypeProperty ([System.Windows.Automation.ControlType]::Window)))
    $win = $null
    foreach ($w in $wins) { if ($w.Current.Name -match $WindowTitleMatch) { $win = $w; break } }
    if (-not $win) { Write-Output "NO_BROWSER_WINDOW matching $WindowTitleMatch"; exit 1 }
    Write-Output "WINDOW $($win.Current.Name)"
    $cond = New-Object System.Windows.Automation.AndCondition((Cond $AE::NameProperty $InvokeButton), (Cond $AE::ControlTypeProperty ([System.Windows.Automation.ControlType]::Button)))
    $btn = $null
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline -and -not $btn) {
        $btn = $win.FindFirst($TS::Descendants, $cond)
        if (-not $btn) { Start-Sleep -Milliseconds 500 }
    }
    if (-not $btn) { Write-Output "NO_BUTTON '$InvokeButton' in window"; exit 1 }
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Write-Output "INVOKED '$InvokeButton'"
}

# --- 2. wait for the dialog ---------------------------------------------------------
$h = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds($TimeoutS)
while ((Get-Date) -lt $deadline) {
    $h = [W32]::FindBrowserDialog()
    if ($h -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 250
}
if ($h -eq [IntPtr]::Zero) { Write-Output "NO_DIALOG within ${TimeoutS}s"; exit 1 }
Write-Output "DIALOG_FOUND $h"
Start-Sleep -Milliseconds 600
if (-not [W32]::ForceForeground($h)) {
    Write-Output "FOREGROUND_FAILED (another window owns focus; typing would land there)"
    exit 1
}
Snap "dlg1_focused"

# --- 3. fill with real clicks -------------------------------------------------------
$dlg = $AE::FromHandle($h)
# The file-name box is a ComboBoxEx32 pane with AutomationId 1148 (the common
# dialog's classic control id); the Open/Cancel buttons are panes of class
# Button named "Open"/"Cancel". None of them are ControlType.Edit/Button.
$edit = $dlg.FindFirst($TS::Descendants, (Cond $AE::AutomationIdProperty '1148'))
if (-not $edit) { Write-Output "NO_FILENAME_EDIT (id 1148 missing)"; exit 1 }
$r = $edit.Current.BoundingRectangle
[W32]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
Write-Output "FILENAME_CLICKED at $([int]($r.X + $r.Width / 2)),$([int]($r.Y + $r.Height / 2))"
Start-Sleep -Milliseconds 300
# HOME+SHIFT+END, not ^a: Ctrl+A is layout-dependent (a Georgian layout typed a stray 'a').
[System.Windows.Forms.SendKeys]::SendWait('{HOME}+{END}')
$esc = [regex]::Replace($Path, '[+^%~(){}\[\]]', { param($m) '{' + $m.Value + '}' })
[System.Windows.Forms.SendKeys]::SendWait($esc)
Start-Sleep -Milliseconds 600
Write-Output "TYPED"
Snap "dlg2_typed"

$openBtn = $dlg.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.AndCondition((Cond $AE::ClassNameProperty 'Button'), (Cond $AE::NameProperty 'Open'))))
if ($openBtn) {
    $r2 = $openBtn.Current.BoundingRectangle
    [W32]::Click([int]($r2.X + $r2.Width / 2), [int]($r2.Y + $r2.Height / 2))
    Write-Output "OPEN_CLICKED"
} else {
    [System.Windows.Forms.SendKeys]::SendWait('%o')
    Write-Output "OPEN_BUTTON_MISSING sent ALT+O"
}
Start-Sleep -Milliseconds 1200
Snap "dlg3_after_open"

$deadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $deadline) {
    if (-not [W32]::IsWindow($h) -or -not [W32]::IsWindowVisible($h)) {
        Start-Sleep -Milliseconds 800
        $left = [W32]::FindBrowserDialog()
        if ($left -eq [IntPtr]::Zero) { Write-Output "DIALOG_CLOSED"; exit 0 }
        Write-Output "DIALOG_CLOSED_BUT_ANOTHER_OPEN $left"; exit 1
    }
    Start-Sleep -Milliseconds 250
}
Write-Output "DIALOG_STILL_OPEN (path rejected? run -CloseDialogs and start again)"
exit 1
