$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$frontendPath = Join-Path $projectRoot "frontend"
$pythonPath = Join-Path $frontendPath ".venv\Scripts\python.exe"

if (-not (Test-Path $pythonPath)) {
    Write-Host "Ambiente Python nao encontrado. A criar frontend/.venv..." -ForegroundColor Yellow
    Push-Location $frontendPath
    try {
        python -m venv .venv
        & $pythonPath -m pip install -r requirements.txt
    }
    finally {
        Pop-Location
    }
}

$apiListener = Get-NetTCPConnection -LocalPort 5030 -State Listen -ErrorAction SilentlyContinue
$apiProcess = $null
if ($apiListener) {
    Write-Host "A API ja esta em execucao em http://localhost:5030." -ForegroundColor Yellow
}
else {
    Write-Host "A iniciar a API .NET em http://localhost:5030..." -ForegroundColor Cyan
    $apiProcess = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList "run --launch-profile http --project `"$projectRoot\ApiNetDeputados.csproj`"" `
        -WorkingDirectory $projectRoot `
        -PassThru

    Start-Sleep -Seconds 2
}

Write-Host "A iniciar o frontend Flet..." -ForegroundColor Cyan
$fletProcess = Start-Process `
    -FilePath $pythonPath `
    -ArgumentList "main.py" `
    -WorkingDirectory $frontendPath `
    -PassThru

Write-Host "API e frontend iniciados." -ForegroundColor Green
Write-Host "API:      http://localhost:5030" -ForegroundColor White
Write-Host "Frontend: processo Flet em execucao" -ForegroundColor White
Write-Host "Feche as janelas dos processos para os terminar." -ForegroundColor DarkGray

try {
    $processIds = @($fletProcess.Id)
    if ($apiProcess) {
        $processIds += $apiProcess.Id
    }
    Wait-Process -Id $processIds
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
    }
    if (-not $fletProcess.HasExited) {
        Stop-Process -Id $fletProcess.Id -Force
    }
}
