<#
.SYNOPSIS
    Refreshes the Word deliverables in Microsoft Word (table of contents and page numbers) and
    optionally exports PDFs. Run after `npm run docs`.
.DESCRIPTION
    Word automation is unreliable on some machines (for example an unactivated Office copy can stall
    while closing a document it just saved). So each step runs in its own short-lived PowerShell
    process: the step logs its progress, and as soon as the work is confirmed in the log the process
    and its hidden Word instance are stopped. Visible Word windows are never touched.
.EXAMPLE
    .\word-export.ps1                                   # refresh both documents in docs/
    .\word-export.ps1 -Pdf -PdfFolder C:\temp\preview   # also export PDFs
#>
param(
    [string[]]$Path = @((Join-Path (Split-Path -Parent $PSScriptRoot) '01_System_Analysis.docx'),
                        (Join-Path (Split-Path -Parent $PSScriptRoot) '02_Database_Design_and_Mapping.docx')),
    [switch]$Pdf,
    [string]$PdfFolder,
    # Exporting a document with large diagrams to PDF can take several minutes in Word.
    [int]$TimeoutSeconds = 600,
    # internal: run one step in this process
    [ValidateSet('', 'update', 'pdf')][string]$Step = '',
    [string]$Log
)
$ErrorActionPreference = 'Stop'

if ($Step) {
    function Note([string]$m) { Add-Content -LiteralPath $Log -Value $m }
    $word = New-Object -ComObject Word.Application
    $word.DisplayAlerts = 0   # wdAlertsNone
    if ($Step -eq 'pdf') {
        # Warm-up: a tiny export first; the first export of a fresh Word process has stalled otherwise.
        $blank = $word.Documents.Add()
        [void]$blank.Content.InsertAfter('warm-up')
        $blank.ExportAsFixedFormat((Join-Path $env:TEMP 'arena-word-warmup.pdf'), 17)
        $blank.Close($false)
    }
    foreach ($item in $Path) {
        $full = (Resolve-Path -LiteralPath $item).Path
        if ($Step -eq 'update') {
            $doc = $word.Documents.Open($full, $false, $false)   # FileName, ConfirmConversions, ReadOnly
            foreach ($toc in $doc.TablesOfContents) { $toc.Update() }
            [void]$doc.Fields.Update()
            $doc.Save()
            Note "done $full"
            $doc.Close($false)
        } else {
            $doc = $word.Documents.Open($full, $false, $true)
            [void]$doc.ComputeStatistics(2)   # finish pagination before exporting
            $folder = if ($PdfFolder) { $PdfFolder } else { Split-Path -Parent $full }
            $pdfPath = Join-Path $folder ([IO.Path]::GetFileNameWithoutExtension($full) + '.pdf')
            $doc.ExportAsFixedFormat($pdfPath, 17)   # wdExportFormatPDF
            Note "done $full -> $pdfPath"
            $doc.Close($false)
        }
    }
    $word.Quit()
    return
}

function Invoke-Step([string]$name, [string]$file) {
    $log = Join-Path $env:TEMP ("arena-word-$name-" + [Guid]::NewGuid().ToString('N') + '.log')
    New-Item -ItemType File -Path $log | Out-Null
    $started = Get-Date
    # The update step runs in its own PowerShell process; the PDF step runs as a background job,
    # which is the only way the export has been reliable on an unactivated Office.
    if ($name -eq 'pdf') {
        $runner = Start-Job -ScriptBlock { param($script, $log, $file, $folder)
            if ($folder) { & $script -Step pdf -Log $log -Path $file -PdfFolder $folder } else { & $script -Step pdf -Log $log -Path $file }
        } -ArgumentList $PSCommandPath, $log, $file, $PdfFolder
        $finished = { $runner.State -ne 'Running' }
    } else {
        $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-Step', $name, '-Log', $log, '-Path', $file)
        $runner = Start-Process powershell -ArgumentList $args -PassThru -WindowStyle Hidden
        $finished = { $runner.HasExited }
    }
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $ok = $false
    while ((Get-Date) -lt $deadline) {
        if (Select-String -LiteralPath $log -Pattern '^done ' -Quiet) { $ok = $true; break }
        if (& $finished) { break }
        Start-Sleep -Milliseconds 500
    }
    Start-Sleep -Seconds 2   # give Word a moment to finish on its own
    # stop only hidden Word instances started by this step
    Get-Process WINWORD -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $started.AddSeconds(-2) -and [string]::IsNullOrEmpty($_.MainWindowTitle) } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    if ($runner -is [System.Diagnostics.Process]) {
        if (-not $runner.HasExited) { Stop-Process -Id $runner.Id -Force -ErrorAction SilentlyContinue }
    } else { Remove-Job $runner -Force -ErrorAction SilentlyContinue }
    $lock =Join-Path (Split-Path -Parent $file) ('~$' + (Split-Path -Leaf $file).Substring(2))
    if (Test-Path -LiteralPath $lock) { Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue }
    $result = Get-Content -LiteralPath $log -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue
    if (-not $ok) { throw "Word did not finish the '$name' step for $file within $TimeoutSeconds seconds." }
    Write-Output $result
}

foreach ($item in $Path) {
    $full = (Resolve-Path -LiteralPath $item).Path
    Invoke-Step 'update' $full | ForEach-Object { "updated: $($_ -replace '^done ', '')" }
    if ($Pdf) { Invoke-Step 'pdf' $full | ForEach-Object { "pdf: $($_ -replace '^done ', '')" } }
}
