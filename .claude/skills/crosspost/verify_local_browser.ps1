# Prove the Claude-in-Chrome browser just selected is the Edge on THIS PC, not Luka's other PC.
# The extension's own "isLocal" flag is NOT proof: on 2026-09-15 the other PC's browser reported
# isLocal:true too, and display names drift ("Browser 1" / "Browser 2" / "Automation").
#
# How: first set a random marker as the page title THROUGH THE EXTENSION
#   (javascript_tool: document.title = 'LOCAL-<random>'), then run this. It looks for the
#   marker in this PC's Edge window titles; if the tab isn't the active one it Ctrl+Tab-cycles
#   via activate_edge_tab.ps1 (beside this file). A window on another machine can never show up.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/crosspost/verify_local_browser.ps1 -Marker LOCAL-7Q4K2
#
# Exit 0 + "LOCAL" = the selected browser drives this PC's Edge.
# Exit 1 + "NOT_LOCAL" = wrong browser, OR a tab that never became the window's active tab (a
# window title shows only its active tab): settle it with the localhost check in the skill.
param([Parameter(Mandatory)][string]$Marker)

function Titles { Get-Process msedge -ErrorAction SilentlyContinue | Where-Object MainWindowTitle | ForEach-Object MainWindowTitle }
function Has { param($t) @($t | Where-Object { $_ -like "*$Marker*" }).Count -gt 0 }

# The extension's tab can take a few seconds to become the window's active tab: poll ~10 s
# before cycling, and print what the cycle said instead of swallowing it.
$titles = Titles
for ($i = 0; $i -lt 10 -and -not (Has $titles); $i++) { Start-Sleep -Seconds 1; $titles = Titles }
if (-not $titles) { Write-Output "NOT_LOCAL: no Edge window on this PC"; exit 1 }
if (Has $titles) { Write-Output "LOCAL: $($titles -join ' | ')"; exit 0 }

# The marked tab may exist but not be the active one: cycle this PC's Edge tabs to find it.
$cycle = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'activate_edge_tab.ps1') -TitleMatch ([regex]::Escape($Marker))
if ($LASTEXITCODE -eq 0) { Write-Output "LOCAL (after tab cycle): $cycle"; exit 0 }
Write-Output "NOT_LOCAL: marker '$Marker' is in no Edge window on this PC (titles: $($titles -join ' | '); cycle: $cycle)"
exit 1
