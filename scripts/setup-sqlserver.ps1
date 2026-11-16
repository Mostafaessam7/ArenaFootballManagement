param(
    [string]$ConnectionString = 'Server=.;Database=ArenaSports;Trusted_Connection=True;TrustServerCertificate=True',
    [string]$AdminEmail = 'leader@arena.local',
    [Security.SecureString]$AdminPassword,
    [switch]$SeedDemo
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\Arena.Api\Arena.Api.csproj'
if (!$AdminPassword) { $AdminPassword = Read-Host 'Admin password (12 to 128 characters)' -AsSecureString }
$credential = [Management.Automation.PSCredential]::new($AdminEmail,$AdminPassword)
$names = @('ASPNETCORE_ENVIRONMENT','Database__Provider','ConnectionStrings__Arena','Bootstrap__AdminEmail','Bootstrap__AdminPassword')
$previous = @{}
foreach($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__Provider = 'SqlServer'
    $env:ConnectionStrings__Arena = $ConnectionString
    $env:Bootstrap__AdminEmail = $AdminEmail
    $env:Bootstrap__AdminPassword = $credential.GetNetworkCredential().Password
    dotnet build $apiProject -m:1 -p:UseSharedCompilation=false
    if($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $runArgs = @('run','--project',$apiProject,'--no-build','--no-launch-profile','--','--init-db')
    if($SeedDemo) { $runArgs += '--seed-demo' }
    & dotnet @runArgs
    if($LASTEXITCODE -ne 0) { throw 'Database initialization failed. Check the SQL Server instance, service and permissions.' }
    Write-Host 'SQL Server migration applied. See README.md for starting the API with a JWT signing secret.'
} finally {
    foreach($name in $names) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
