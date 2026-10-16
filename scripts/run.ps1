$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'MiniTranslator requires Windows.'
}

Push-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
try {
    Write-Host 'Restoring dependencies...'
    & dotnet restore MiniTranslator.slnx
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Formatting...'
    & dotnet format MiniTranslator.slnx --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Linting...'
    & dotnet format MiniTranslator.slnx --no-restore --verify-no-changes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Building...'
    & dotnet build MiniTranslator.slnx --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Testing...'
    & dotnet test MiniTranslator.slnx --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Running MiniTranslator (exit through the tray menu)...'
    & dotnet run --project apps/MiniTranslator.Wpf --no-build --no-restore -- @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
