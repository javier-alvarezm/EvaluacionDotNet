$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet restore.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet tool restore.' }
    $secretFile = Join-Path $env:APPDATA 'Microsoft\UserSecrets\evaluacion-dotnet-local-development\secrets.json'
    $hasKey = $false
    if (Test-Path -LiteralPath $secretFile) {
        $secrets = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
        $hasKey = -not [string]::IsNullOrWhiteSpace($secrets.'Jwt:Key')
    }
    if (-not $hasKey) {
        $keyBytes = New-Object byte[] 64
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($keyBytes) } finally { $rng.Dispose() }
        $jwtKey = [Convert]::ToBase64String($keyBytes)
        @{ 'Jwt:Key' = $jwtKey } | ConvertTo-Json -Compress |
            dotnet user-secrets set --project src/EvaluacionDotNet.Api | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar Jwt:Key.' }
    }
    sqlcmd -S '.\SQLEXPRESS' -E -b -l 10 -Q "IF DB_ID(N'EvaluacionDotNet_Dev') IS NULL CREATE DATABASE [EvaluacionDotNet_Dev];"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar la base de datos.' }
    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion.' }
    dotnet ef database update --project src/EvaluacionDotNet.Api --no-build -- --environment Development
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la aplicacion de migraciones.' }
    Write-Host 'Ambiente listo. Ejecuta: dotnet run --project src/EvaluacionDotNet.Api --launch-profile https'
} finally { Pop-Location }
