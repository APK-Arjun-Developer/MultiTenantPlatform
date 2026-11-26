$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet restore MultiTenantPlatform.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet format whitespace MultiTenantPlatform.slnx --no-restore --verify-no-changes --include src tests --exclude src/Infrastructure/Persistence/Migrations
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test MultiTenantPlatform.slnx -c Release --no-restore --collect 'XPlat Code Coverage'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
