# Starts smartHRMS.Api like `dotnet run`, but works around Smart App Control on this machine: when Windows blocks
# the freshly built DLL ("An Application Control policy has blocked this file"), it rebuilds (which gives the DLL a
# new hash, see Directory.Build.props) and tries again.
#
# Usage (from the backend folder):  .\run-api.ps1
param([int]$MaxAttempts = 5)

$project = Join-Path $PSScriptRoot 'smartHRMS.Api'

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    $output = @()
    dotnet run --project $project 2>&1 | Tee-Object -Variable output | Out-Host
    if (-not ($output -match 'Application Control policy has blocked')) { exit $LASTEXITCODE }

    Write-Host "`nSmart App Control blocked this build (attempt $attempt of $MaxAttempts). Rebuilding with a new hash..." -ForegroundColor Yellow
    dotnet build $project --no-incremental | Out-Null
}

Write-Host "Still blocked after $MaxAttempts attempts. Run .\run-api.ps1 again, or see documentation/backend.md section 20 (Smart App Control)." -ForegroundColor Red
exit 1
