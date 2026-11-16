$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet restore Arena.sln
    if($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build Arena.sln --no-restore -m:1 -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test Arena.sln --no-build --no-restore --logger 'trx;LogFileName=week1-tests.trx' --results-directory TestResults
    if($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    dotnet tool restore
    if($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet ef migrations has-pending-model-changes --project src/Arena.Infrastructure --startup-project src/Arena.Api --no-build
    if($LASTEXITCODE -ne 0) { throw 'The model and migration snapshot differ.' }
} finally { Pop-Location }
