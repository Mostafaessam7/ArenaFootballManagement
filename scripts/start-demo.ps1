param(
    [string]$AdminEmail = 'leader@arena.local',
    [Security.SecureString]$AdminPassword,
    [ValidateRange(1024,65535)][int]$Port = 5080
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\Arena.Api\Arena.Api.csproj'
$dataPath = Join-Path $projectRoot 'artifacts\arena-demo.db'
New-Item -ItemType Directory -Path (Split-Path -Parent $dataPath) -Force | Out-Null
if (!$AdminPassword) { $AdminPassword = Read-Host 'Admin password (12 to 128 characters; existing admin passwords are never reset)' -AsSecureString }
$credential = [Management.Automation.PSCredential]::new($AdminEmail,$AdminPassword)
$names = @('ASPNETCORE_ENVIRONMENT','Database__Provider','ConnectionStrings__Arena','Jwt__SigningKey','Bootstrap__AdminEmail','Bootstrap__AdminPassword')
$previous = @{}
foreach($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__Provider = 'Sqlite'
    $env:ConnectionStrings__Arena = "Data Source=$dataPath;Foreign Keys=True"
    if (!$env:Jwt__SigningKey) {
        $bytes = [byte[]]::new(48)
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $random.GetBytes($bytes) } finally { $random.Dispose() }
        $env:Jwt__SigningKey = [Convert]::ToBase64String($bytes)
    }
    $env:Bootstrap__AdminEmail = $AdminEmail
    $env:Bootstrap__AdminPassword = $credential.GetNetworkCredential().Password
    dotnet build $apiProject -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project $apiProject --no-build --no-launch-profile -- --init-db --seed-demo
    if ($LASTEXITCODE -ne 0) { throw 'Demo initialization failed.' }
    $env:Bootstrap__AdminPassword = $null
    Write-Host "Demo API: http://localhost:$Port"
    Write-Host "Interactive API reference: http://localhost:$Port/scalar/v1"
    Write-Host "Admin email: $AdminEmail. Use the password you supplied. Press Ctrl+C to stop."
    dotnet run --project $apiProject --no-build --no-launch-profile -- --urls "http://localhost:$Port"
} finally {
    foreach($name in $names) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
