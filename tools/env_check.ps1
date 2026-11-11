# Checks the tools this channel needs. Exit 1 if something required is missing.
$ErrorActionPreference = 'Continue'
$ok = $true
function Report($name, $found, $detail, $required = $true) {
    if ($found) { Write-Host ("OK    {0,-10} {1}" -f $name, $detail) }
    else {
        $tag = if ($required) { 'FIX  ' } else { 'WARN ' }
        Write-Host ("{0} {1,-10} {2}" -f $tag, $name, $detail)
        if ($required) { $script:ok = $false }
    }
}

$unityVer = '6000.4.1f1'
$unity = "C:\Program Files\Unity\Hub\Editor\$unityVer\Editor\Unity.exe"
Report 'Unity' (Test-Path $unity) $(if (Test-Path $unity) { "$unityVer at $unity" } else { "Unity $unityVer not found: install it from Unity Hub" })

$blender = Get-ChildItem 'C:\Program Files\Blender Foundation\*\blender.exe' -ErrorAction SilentlyContinue | Select-Object -Last 1
Report 'Blender' ($null -ne $blender) $(if ($blender) { $blender.FullName } else { 'Blender not found under C:\Program Files\Blender Foundation' })

foreach ($t in 'ffmpeg', 'ffprobe', 'yt-dlp') {
    $c = Get-Command $t -ErrorAction SilentlyContinue
    Report $t ($null -ne $c) $(if ($c) { $c.Source } else { "$t not on PATH" })
}
$py = Get-Command python -ErrorAction SilentlyContinue
Report 'python' ($null -ne $py) $(if ($py) { $py.Source } else { 'python not on PATH' }) $false

$proj = Join-Path $PSScriptRoot '..\WeMadeIt_Unity\ProjectSettings\ProjectVersion.txt'
Report 'UnityProj' (Test-Path $proj) $(if (Test-Path $proj) { (Get-Content $proj -First 1) } else { 'WeMadeIt_Unity not created yet: run tools/unity_new_project.ps1' }) $false

if ($ok) { Write-Host 'All required tools present.'; exit 0 } else { exit 1 }
