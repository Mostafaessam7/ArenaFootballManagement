<#
.SYNOPSIS
    Renders docs/diagrams/*.svg to PNG at 2x scale with headless Microsoft Edge (Word needs PNG).
.EXAMPLE
    .\render-diagrams.ps1                      # every SVG that has no PNG or is newer than its PNG
    .\render-diagrams.ps1 -Name 06_EERD_Overview -Force
#>
param([string[]]$Name, [switch]$Force)
$ErrorActionPreference = 'Stop'
$edge = @('C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe', 'C:\Program Files\Microsoft\Edge\Application\msedge.exe') |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge was not found.' }
$folder = Join-Path (Split-Path -Parent $PSScriptRoot) 'diagrams'
$profileDir = Join-Path $env:TEMP 'arena-edge-render'
# `-File` passes "a,b" as one string, so split names on commas.
$wanted = @($Name | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$files = Get-ChildItem $folder -Filter *.svg | Where-Object { $wanted.Count -eq 0 -or $wanted -contains $_.BaseName }
foreach ($svg in $files) {
    $png = [IO.Path]::ChangeExtension($svg.FullName, '.png')
    if (-not $Force -and (Test-Path $png) -and (Get-Item $png).LastWriteTime -ge $svg.LastWriteTime) { continue }
    [xml]$doc = Get-Content -LiteralPath $svg.FullName -Raw
    $w = [int]$doc.svg.width; $h = [int]$doc.svg.height
    $uri = ([Uri]$svg.FullName).AbsoluteUri
    $edgeArgs = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', "--user-data-dir=$profileDir",
        '--force-device-scale-factor=2', "--window-size=$w,$h", "--screenshot=$png", $uri)
    $log = Join-Path $env:TEMP 'arena-edge-render.log'
    $p = Start-Process $edge -ArgumentList $edgeArgs -PassThru -Wait -WindowStyle Hidden -RedirectStandardError $log
    if (-not (Test-Path $png)) { throw "Edge did not render $($svg.Name) (exit $($p.ExitCode))." }
    Write-Output ("{0} -> {1}x{2} ({3} KB)" -f $svg.Name, ($w * 2), ($h * 2), [math]::Round((Get-Item $png).Length / 1KB))
}

# Wide diagrams are placed on portrait pages turned a quarter: keep a rotated copy next to each.
# (Word lost Arabic shaping after switching page orientation, so the documents avoid landscape sections.)
Add-Type -AssemblyName System.Drawing
foreach ($wide in '06_EERD_Overview', '01_Relational_Overview') {
    $src = Join-Path $folder "$wide.png"
    $dst = Join-Path $folder "$wide-rotated.png"
    if (-not (Test-Path $src)) { continue }
    if (-not $Force -and (Test-Path $dst) -and (Get-Item $dst).LastWriteTime -ge (Get-Item $src).LastWriteTime) { continue }
    $img = [Drawing.Image]::FromFile($src)
    try { $img.RotateFlip([Drawing.RotateFlipType]::Rotate270FlipNone); $img.Save($dst, [Drawing.Imaging.ImageFormat]::Png) } finally { $img.Dispose() }
    Write-Output "$wide-rotated.png"
}
