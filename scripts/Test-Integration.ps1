param([switch]$SkipExternal, [switch]$Postman)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testDatabase = 'EvaluacionDotNet_Test_' + [Guid]::NewGuid().ToString('N')
$connection = "Server=.\SQLEXPRESS;Database=$testDatabase;Trusted_Connection=True;TrustServerCertificate=True"
$testPort = 5296
$baseUrl = "http://127.0.0.1:$testPort"
$testProcess = $null
$passed = 0
$headers = @{}
$previousConnection = $env:ConnectionStrings__DefaultConnection
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousUrls = $env:ASPNETCORE_URLS
$previousHttpsPort = $env:ASPNETCORE_HTTPS_PORT

function Check($condition, [string]$label) {
    if (-not $condition) { throw "FALLO: $label" }
    $script:passed++
    Write-Host "OK: $label"
}

function Api([string]$method, [string]$path, $body = $null, [int]$expected = 200, [switch]$Anonymous, [string]$Bearer) {
    $parameters = @{ Uri = "$baseUrl$path"; Method = $method; UseBasicParsing = $true; TimeoutSec = 40 }
    if (-not $Anonymous) { $parameters.Headers = $script:headers }
    if ($Bearer) { $parameters.Headers = @{ Authorization = "Bearer $Bearer" } }
    if ($null -ne $body) { $parameters.ContentType = 'application/json'; $parameters.Body = $body | ConvertTo-Json -Depth 10 }
    try {
        $response = Invoke-WebRequest @parameters
        $status = [int]$response.StatusCode
        $content = $response.Content
    } catch {
        if (-not $_.Exception.Response) { throw }
        $status = [int]$_.Exception.Response.StatusCode
        $content = $_.ErrorDetails.Message
    }
    Check ($status -eq $expected) "$method $path => $status (esperado $expected)"
    if ($content) {
        $parsed = $content | ConvertFrom-Json
        return $parsed
    }
}

Push-Location $projectRoot
try {
    if (Get-NetTCPConnection -LocalPort $testPort -State Listen -ErrorAction SilentlyContinue) {
        throw "Puerto de pruebas $testPort ocupado. No se detuvo el proceso existente."
    }
    New-Item -ItemType Directory -Path '.artifacts' -Force | Out-Null
    dotnet build src/EvaluacionDotNet.Api --configuration Verification | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion.' }
    dotnet ef database update --project src/EvaluacionDotNet.Api --configuration Verification --no-build --connection $connection -- --environment Development | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la creacion de la base aislada.' }
    $env:ConnectionStrings__DefaultConnection = $connection
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = $baseUrl
    $env:ASPNETCORE_HTTPS_PORT = $null
    $testProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList 'bin/Verification/net8.0/EvaluacionDotNet.Api.dll' `
        -WorkingDirectory (Join-Path $projectRoot 'src/EvaluacionDotNet.Api') -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $projectRoot '.artifacts/test-api.log') `
        -RedirectStandardError (Join-Path $projectRoot '.artifacts/test-api-error.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($testProcess.HasExited) { throw 'La API de prueba termino. Consulta .artifacts/test-api-error.log.' }
        try {
            $health = Invoke-RestMethod "$baseUrl/health" -TimeoutSec 2
            if ($health.database -eq 'connected') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 300 }
    }
    Check $ready 'API conectada a la base SQL aislada'
    Api GET /rates -expected 401 -Anonymous | Out-Null
    Api GET /currencies -expected 404 -Anonymous | Out-Null
    Api GET '/frankfurter/latest?base=USD&target=EUR' -expected 401 -Anonymous | Out-Null
    Api GET /rates -expected 401 -Bearer 'invalid.jwt.token' | Out-Null
    Api POST /auth/register -expected 404 -Anonymous | Out-Null
    Api POST /auth/login -expected 404 -Anonymous | Out-Null
    $token = Api POST /auth/token -Anonymous
    Check (-not [string]::IsNullOrEmpty($token.accessToken)) 'JWT emitido sin credenciales ni body'
    Check ($token.expiresIn -eq 7200) 'JWT valido durante dos horas'
    $payloadPart = $token.accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/')
    $payloadPart = $payloadPart.PadRight($payloadPart.Length + ((4 - $payloadPart.Length % 4) % 4), '=')
    $payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payloadPart)) | ConvertFrom-Json
    Check (($payload.exp - $payload.nbf) -eq 7200) 'La expiracion firmada del JWT corresponde a dos horas'
    $headers = @{ Authorization = "Bearer $($token.accessToken)" }
    sqlcmd -S '.\SQLEXPRESS' -d $testDatabase -E -b -Q "INSERT INTO Currency (Symbol, Name) VALUES ('XAA', 'Moneda de prueba A'), ('XBB', 'Moneda de prueba B'), ('XCC', 'Moneda de prueba C');"
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la preparacion de monedas de prueba.' }
    $rate = @{ baseCurrency = 'XAA'; targetCurrency = 'XBB'; rate = 2; date = '2024-01-02' }
    $first = Api POST /rates $rate -expected 201
    Api POST /rates $rate -expected 409 | Out-Null
    Api POST /rates @{ baseCurrency = 'XAA'; targetCurrency = 'XAA'; rate = 1; date = '2024-01-02' } -expected 400 | Out-Null
    Api POST /rates @{ baseCurrency = 'XAA'; targetCurrency = 'ZZZ'; rate = 1; date = '2024-01-02' } -expected 400 | Out-Null
    Api POST /rates @{ baseCurrency = 'XAA'; targetCurrency = 'XBB'; rate = 0; date = '2024-01-02' } -expected 400 | Out-Null
    Api POST /rates @{ baseCurrency = 'XAA'; targetCurrency = 'XBB'; rate = 1 } -expected 400 | Out-Null
    Api POST /rates @{ baseCurrency = 'XAA'; targetCurrency = 'XBB'; rate = 1; date = '2099-01-02' } -expected 400 | Out-Null
    $rate.date = '2024-01-03'; $rate.rate = 4
    $second = Api POST /rates $rate -expected 201
    $average = Api GET '/rates/average?base=XAA&target=XBB&start=2024-01-02&end=2024-01-03'
    Check ($average.average -eq 3 -and $average.count -eq 2) 'Promedio e inclusion de ambas fechas'
    $minmax = Api GET '/rates/minmax?base=XAA&target=XBB&start=2024-01-02&end=2024-01-03'
    Check ($minmax.min -eq 2 -and $minmax.max -eq 4) 'Minimo y maximo'
    Api GET '/rates/average?base=XAA&target=XBB&start=2024-02-01&end=2024-01-01' -expected 400 | Out-Null
    Api GET '/rates/average?base=XAA&target=XBB&start=2020-01-01&end=2020-01-02' -expected 404 | Out-Null
    $rate.rate = 6
    Api PUT "/rates/$($second.id)" $rate -expected 204 | Out-Null
    $saved = Api GET "/rates/$($second.id)"
    Check ($saved.rate -eq 6) 'Actualizacion individual persistida'
    Api POST /rates @{ baseCurrency = 'XAA'; targetCurrency = 'XCC'; rate = 8; date = '2024-01-03T13:45:00' } -expected 201 | Out-Null
    $bulk = Api PUT /rates/currency/XAA @{ rate = 10 }
    Check ($bulk.updated -eq 3) 'Actualizacion por base de todas las fechas y monedas destino'
    Api PUT /rates/currency/XAA @{ rate = 0 } -expected 400 | Out-Null
    $rows = @(Api GET /rates/currency/xaa)
    Check ($rows.Count -eq 3 -and ($rows | Where-Object { $_.rate -ne 10 }).Count -eq 0) 'Cambios masivos persistidos'
    Api DELETE "/rates/$($first.id)" -expected 204 | Out-Null
    Api GET "/rates/$($first.id)" -expected 404 | Out-Null
    $deleted = Api DELETE /rates/currency/XAA
    Check ($deleted.deleted -eq 2) 'Eliminacion por base'
    Api DELETE /rates/currency/XAA -expected 404 | Out-Null
    Api GET /rates | Out-Null
    Api GET '/frankfurter/historical?base=USD&target=EUR&date=2099-01-01' -expected 400 | Out-Null
    Api GET '/frankfurter/timeseries?base=USD&target=EUR&start=2024-02-01&end=2024-01-01' -expected 400 | Out-Null
    Api GET '/frankfurter/convert?from=USD&to=EUR&amount=0' -expected 400 | Out-Null
    Api POST /frankfurter/import/unknown @{ baseCurrency = 'USD'; targetCurrency = 'EUR' } -expected 400 | Out-Null
    if (-not $SkipExternal) {
        $latest = @(Api GET '/frankfurter/latest?base=USD&target=EUR')
        Check ($latest.Count -eq 1 -and $latest[0].rate -gt 0) 'Frankfurter: tasas recientes'
        $historical = @(Api GET '/frankfurter/historical?base=USD&target=EUR&date=2024-01-02')
        Check ($historical[0].date -eq '2024-01-02') 'Frankfurter: fecha historica'
        $series = @(Api GET '/frankfurter/timeseries?base=USD&target=EUR&start=2024-01-02&end=2024-01-05')
        Check ($series.Count -eq 4) 'Frankfurter: serie temporal'
        $conversion = Api GET '/frankfurter/convert?from=USD&to=EUR&amount=100&date=2024-01-02'
        Check ([Math]::Abs($conversion.convertedAmount - [Math]::Round(100 * $historical[0].rate, 4)) -lt 0.00001) 'Conversion calculada desde la tasa historica'
        $import = @{ baseCurrency = 'USD'; targetCurrency = 'EUR'; date = '2024-01-02' }
        $result = Api POST /frankfurter/import/historical $import
        Check ($result.created -eq 1) 'Importacion historica persistida'
        $again = Api POST /frankfurter/import/historical $import
        Check ($again.created -eq 0 -and $again.updated -eq 1) 'Reimportacion sin duplicados'
        $result = Api POST /frankfurter/import/timeseries @{ baseCurrency = 'USD'; targetCurrency = 'EUR'; start = '2024-01-02'; end = '2024-01-05' }
        Check ($result.created -eq 3 -and $result.updated -eq 1) 'Importacion atomica de serie'
        Api POST /frankfurter/import/latest @{ baseCurrency = 'USD'; targetCurrency = 'EUR' } | Out-Null
        $stored = @(Api GET /rates/currency/USD)
        Check ($stored.Count -ge 4) 'Tasas importadas recuperadas desde SQL Server'
        Api GET '/frankfurter/latest?base=ZZZ&target=EUR' -expected 404 | Out-Null
    }
    if ($Postman) {
        if (-not (Get-Command npx.cmd -ErrorAction SilentlyContinue)) { throw 'Instala Node.js para ejecutar la coleccion con Newman.' }
        npx.cmd --yes --package newman@6.2.3 newman run postman/EvaluacionDotNet.postman_collection.json --env-var "baseUrl=$baseUrl" --timeout-request 40000 --bail | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Fallo la coleccion de Postman.' }
        Check $true 'Coleccion Postman ejecutada con Newman'
    }
    Write-Host "RESULTADO: $passed comprobaciones correctas."
} finally {
    if ($testProcess -and -not $testProcess.HasExited) {
        Stop-Process -Id $testProcess.Id
        $testProcess.WaitForExit()
    }
    $env:ConnectionStrings__DefaultConnection = $previousConnection
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:ASPNETCORE_URLS = $previousUrls
    $env:ASPNETCORE_HTTPS_PORT = $previousHttpsPort
    # Solo se elimina la base temporal exacta creada por esta ejecucion.
    if ($testDatabase -match '^EvaluacionDotNet_Test_[a-f0-9]{32}$') {
        sqlcmd -S '.\SQLEXPRESS' -E -b -Q "IF DB_ID(N'$testDatabase') IS NOT NULL BEGIN ALTER DATABASE [$testDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$testDatabase]; END;" | Out-Host
    }
    Pop-Location
}
