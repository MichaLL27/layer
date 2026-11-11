# Contact sheet of a video: one tile every -Every seconds, 5 columns.
# Usage: ./tools/contact_sheet.ps1 <video.mp4> [-Every 5] [-Cols 5] [-TileWidth 304] [-Out <png>]
param(
    [Parameter(Mandatory = $true, Position = 0)][string]$Video,
    [double]$Every = 5,
    [int]$Cols = 5,
    [int]$TileWidth = 304,
    [string]$Out
)
if (-not (Test-Path -LiteralPath $Video)) { Write-Error "Not found: $Video"; exit 1 }
$dur = [double](& ffprobe -v error -show_entries format=duration -of csv=p=0 -- $Video)
$tiles = [math]::Ceiling($dur / $Every)
$rows = [math]::Ceiling($tiles / $Cols)
if (-not $Out) {
    $outDir = Join-Path $PSScriptRoot '..\output'
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $Out = Join-Path $outDir ("sheet_" + [IO.Path]::GetFileNameWithoutExtension($Video) + ".png")
}
$vf = "fps=1/$Every,scale=${TileWidth}:-1,tile=${Cols}x${rows}"
& ffmpeg -v error -y -i $Video -vf $vf -frames:v 1 $Out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Wrote $((Resolve-Path $Out).Path)  ($tiles tiles, $([math]::Round($dur,1)) s)"
