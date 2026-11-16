param(
    [string]$ConnectionString = 'Server=.;Database=ArenaSports;Trusted_Connection=True;TrustServerCertificate=True',
    [ValidateRange(1024,65535)][int]$Port = 5080
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\Arena.Api\Arena.Api.csproj'
$names = @('ASPNETCORE_ENVIRONMENT','Database__Provider','ConnectionStrings__Arena','Jwt__SigningKey')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__Provider = 'SqlServer'
    $env:ConnectionStrings__Arena = $ConnectionString
    if (!$env:Jwt__SigningKey) {
        $bytes = [byte[]]::new(48)
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        $env:Jwt__SigningKey = [Convert]::ToBase64String($bytes)
    }
    dotnet build $apiProject -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host "API reference: http://localhost:$Port/scalar/v1. Initialize the database with setup-sqlserver.ps1 first."
    dotnet run --project $apiProject --no-build --no-launch-profile -- --urls "http://localhost:$Port"
    if ($LASTEXITCODE -ne 0) { throw 'API stopped with an error.' }
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
