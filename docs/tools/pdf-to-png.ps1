<#
.SYNOPSIS
    Renders the pages of a PDF to PNG files with the Windows built-in PDF renderer (Windows.Data.Pdf).
    Used to review the generated Word documents page by page.
.EXAMPLE
    .\pdf-to-png.ps1 -Pdf ..\01_System_Analysis.pdf -OutFolder .\preview -Pages 1,2,3
#>
param(
    [Parameter(Mandatory)][string]$Pdf,
    [Parameter(Mandatory)][string]$OutFolder,
    [int[]]$Pages,
    [int]$Width = 1100
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[void][Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]
[void][Windows.Storage.Streams.InMemoryRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asTaskAction = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' })[0]
function Await($op, [Type]$type) { $t = $asTaskGeneric.MakeGenericMethod($type).Invoke($null, @($op)); $t.Wait(-1) | Out-Null; $t.Result }
function AwaitAction($op) { $t = $asTaskAction.Invoke($null, @($op)); $t.Wait(-1) | Out-Null }

New-Item -ItemType Directory -Force -Path $OutFolder | Out-Null
$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path -LiteralPath $Pdf).Path)) ([Windows.Storage.StorageFile])
$document = Await ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)) ([Windows.Data.Pdf.PdfDocument])
$count = [int]$document.PageCount
if (-not $Pages) { $Pages = 1..$count }
$name = [IO.Path]::GetFileNameWithoutExtension($Pdf)
foreach ($n in $Pages) {
    if ($n -lt 1 -or $n -gt $count) { continue }
    $page = $document.GetPage([uint32]($n - 1))
    $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
    $options.DestinationWidth = [uint32]$Width
    $stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream
    AwaitAction ($page.RenderToStreamAsync($stream, $options))
    $netStream = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($stream.GetInputStreamAt(0))
    $out = Join-Path $OutFolder ('{0}-{1:00}.png' -f $name, $n)
    $fs = [IO.File]::Create($out); $netStream.CopyTo($fs); $fs.Close(); $netStream.Dispose(); $page.Dispose()
    Write-Output $out
}
Write-Output "pages=$count"
