<#
.SYNOPSIS
    Concurrency acceptance test for ticket booking on a real SQL Server.

.DESCRIPTION
    Creates a throwaway database, starts the API against it and fires simultaneous requests:
      1. Many single-ticket bookings on an offer with little stock  -> exactly Capacity succeed, the rest 409 sold_out.
      2. The same booking request (same idempotency key) sent at once -> one booking, stock taken once.
      3. The same booking cancelled at once                          -> stock returned exactly once.
    No request may end in HTTP 500. The database is dropped at the end.
    SQLite tests cannot prove this because SQLite has no row locks or deadlock detection.

.EXAMPLE
    .\scripts\test-concurrency.ps1
.EXAMPLE
    .\scripts\test-concurrency.ps1 -Server '.' -Requests 50 -Capacity 10
#>
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [int]$Capacity = 5,
    [int]$Requests = 20,
    [int]$Port = 5199
)
$ErrorActionPreference = 'Stop'
if ($Requests -le $Capacity) { throw 'Requests must be larger than Capacity so some requests have to be refused.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\Arena.Api\Arena.Api.csproj'
$infraProject = Join-Path $projectRoot 'src\Arena.Infrastructure\Arena.Infrastructure.csproj'
$apiBin = Join-Path $projectRoot 'src\Arena.Api\bin\Debug\net10.0'
$database = 'ArenaConcurrency_' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$base = "http://127.0.0.1:$Port"
Add-Type -AssemblyName System.Net.Http

function New-Secret([int]$bytes) {
    $b = New-Object byte[] $bytes
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    [Convert]::ToBase64String($b)
}
function Invoke-Api([string]$method, [string]$path, $body, [string]$token) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $request = @{ Method = $method; Uri = "$base$path"; Headers = $headers; UseBasicParsing = $true }
    if ($null -ne $body) { $request.Body = ($body | ConvertTo-Json -Compress); $request.ContentType = 'application/json' }
    Invoke-RestMethod @request
}
function New-Client([string]$token) {
    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(90)
    $client.DefaultRequestHeaders.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $token)
    $client
}
# Starts every request before awaiting any, then returns status code, error code and body for each.
function Invoke-AtOnce([object[]]$calls) {
    $tasks = foreach ($c in $calls) {
        if ($null -ne $c.Body) {
            $content = New-Object System.Net.Http.StringContent(($c.Body | ConvertTo-Json -Compress), [Text.Encoding]::UTF8, 'application/json')
        } else { $content = New-Object System.Net.Http.StringContent('', [Text.Encoding]::UTF8, 'application/json') }
        $c.Client.PostAsync("$base$($c.Path)", $content)
    }
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
    foreach ($t in $tasks) {
        $text = $t.Result.Content.ReadAsStringAsync().Result
        $json = $null; try { $json = $text | ConvertFrom-Json } catch { }
        [pscustomobject]@{ Status = [int]$t.Result.StatusCode; Code = $(if ($json) { $json.title } else { '' }); Body = $json }
    }
}
function Get-Sold([int]$eventId, [int]$offerId) {
    ((Invoke-Api GET "/api/v1/events/$eventId/ticket-offers?pageSize=100").items | Where-Object { $_.id -eq $offerId }).soldQuantity
}
$results = New-Object System.Collections.Generic.List[object]
function Check([string]$name, [bool]$ok, [string]$detail) {
    $results.Add([pscustomobject]@{ Check = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Detail = $detail })
}

$names = @('ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','Database__Provider','ConnectionStrings__Arena',
    'Bootstrap__AdminEmail','Bootstrap__AdminPassword','Jwt__SigningKey')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$adminPassword = New-Secret 24
$api = $null
Push-Location $projectRoot
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $base
    $env:Database__Provider = 'SqlServer'
    $env:ConnectionStrings__Arena = "Server=$Server;Database=$database;Trusted_Connection=True;TrustServerCertificate=True"
    $env:Bootstrap__AdminEmail = 'concurrency@arena.local'
    $env:Bootstrap__AdminPassword = $adminPassword
    $env:Jwt__SigningKey = New-Secret 48

    Write-Host "Building and creating throwaway database $database on $Server ..."
    dotnet build $apiProject -nologo -v:q -m:1 -p:UseSharedCompilation=false | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet tool restore | Out-Null
    & dotnet run --project $apiProject --no-build --no-launch-profile -- --init-db --seed-demo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed. Check that SQL Server is running and reachable.' }

    $api = Start-Process dotnet -ArgumentList 'Arena.Api.dll' -WorkingDirectory $apiBin -PassThru -WindowStyle Hidden
    $ready = $false
    foreach ($i in 1..60) {
        try { if ((Invoke-WebRequest "$base/health/ready" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $ready = $true; break } }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "The API did not become ready on $base." }

    $admin = (Invoke-Api POST '/api/v1/auth/login' @{ email = 'concurrency@arena.local'; password = $adminPassword }).accessToken
    $fans = foreach ($n in 1..4) {
        (Invoke-Api POST '/api/v1/auth/register' @{ fullName = "Concurrency Fan $n"; email = "fan$n.concurrency@arena.local"; password = (New-Secret 24) }).accessToken
    }
    $clients = $fans | ForEach-Object { New-Client $_ }
    $event = (Invoke-Api GET '/api/v1/events?pageSize=100').items | Where-Object { $_.type -eq 'Community' } | Select-Object -First 1
    $now = [DateTime]::UtcNow; $fmt = 'yyyy-MM-ddTHH:mm:ssZ'
    function New-Offer([string]$name, [int]$capacity) {
        Invoke-Api POST "/api/v1/events/$($event.id)/ticket-offers" @{ name = $name; price = 50; capacity = $capacity
            salesStartUtc = $now.AddHours(-1).ToString($fmt); salesEndUtc = $now.AddDays(9).ToString($fmt) } $admin
    }

    # 1. More buyers than tickets, all at the same moment.
    $offer = New-Offer 'Concurrency stock' $Capacity
    $calls = foreach ($i in 1..$Requests) {
        @{ Client = $clients[$i % $clients.Count]; Path = '/api/v1/bookings'
           Body = @{ ticketOfferId = $offer.id; quantity = 1; idempotencyKey = ('stock-{0:0000}' -f $i) } }
    }
    $r = @(Invoke-AtOnce $calls)
    $created = @($r | Where-Object Status -eq 201).Count
    $soldOut = @($r | Where-Object { $_.Status -eq 409 -and $_.Code -eq 'sold_out' }).Count
    $errors = @($r | Where-Object Status -ge 500).Count
    $other = $Requests - $created - $soldOut
    $sold = Get-Sold $event.id $offer.id
    Check "Stock: $Requests buyers, $Capacity tickets" ($created -eq $Capacity -and $soldOut -eq ($Requests - $Capacity) -and $other -eq 0) "201=$created, 409 sold_out=$soldOut, other=$other"
    Check 'Stock: sold quantity matches bookings' ($sold -eq $Capacity) "SoldQuantity=$sold of $Capacity"
    Check 'Stock: no server errors' ($errors -eq 0) "HTTP 5xx=$errors"

    # 2. The same request with the same idempotency key sent several times at once.
    $offer2 = New-Offer 'Concurrency idempotency' 10
    $same = @{ ticketOfferId = $offer2.id; quantity = 2; idempotencyKey = 'same-key-0001' }
    $r2 = @(Invoke-AtOnce (1..8 | ForEach-Object { @{ Client = $clients[0]; Path = '/api/v1/bookings'; Body = $same } }))
    $ids = @($r2 | Where-Object Status -eq 201 | ForEach-Object { $_.Body.id } | Sort-Object -Unique)
    $bad2 = @($r2 | Where-Object { $_.Status -ne 201 -and $_.Status -ne 409 }).Count
    $sold2 = Get-Sold $event.id $offer2.id
    Check 'Same key: one booking' ($ids.Count -eq 1 -and $bad2 -eq 0) ("distinct booking ids=$($ids.Count), codes=" + (($r2 | Group-Object Status | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ' '))
    Check 'Same key: stock taken once' ($sold2 -eq 2) "SoldQuantity=$sold2 (expected 2)"

    # 3. The same booking cancelled several times at once.
    if ($ids.Count -ge 1) {
        $bookingId = $ids[0]
        $r3 = @(Invoke-AtOnce (1..8 | ForEach-Object { @{ Client = $clients[0]; Path = "/api/v1/bookings/$bookingId/cancel"; Body = $null } }))
        $bad3 = @($r3 | Where-Object { $_.Status -ne 200 -and $_.Status -ne 409 }).Count
        $sold3 = Get-Sold $event.id $offer2.id
        $status = (Invoke-Api GET "/api/v1/bookings/$bookingId" $null $fans[0]).status
        Check 'Cancel at once: stock returned once' ($sold3 -eq 0 -and $status -eq 'Cancelled' -and $bad3 -eq 0) ("SoldQuantity=$sold3, status=$status, codes=" + (($r3 | Group-Object Status | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ' '))
    }
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force }
    Write-Host "Dropping $database ..."
    & dotnet ef database drop --force --project $infraProject --startup-project $apiProject --no-build 2>&1 | Out-Null
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    Pop-Location
}

$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
$failed = @($results | Where-Object Result -eq 'FAIL').Count
if ($results.Count -eq 0 -or $failed -gt 0) { Write-Host "Concurrency test FAILED ($failed check(s))." -ForegroundColor Red; exit 1 }
Write-Host 'Concurrency test passed.' -ForegroundColor Green
