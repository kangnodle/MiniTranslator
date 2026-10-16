$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'MiniTranslator releases require Windows.'
}

$nsis = Get-Command makensis -CommandType Application -ErrorAction SilentlyContinue
$nsisPath = if ($nsis) { $nsis.Source } else { $null }
if (-not $nsisPath) {
    foreach ($directory in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($directory) {
            $candidate = Join-Path $directory 'NSIS/makensis.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $nsisPath = $candidate
                break
            }
        }
    }
}
if (-not $nsisPath) {
    throw 'NSIS 3 is required. Install it in Program Files or add makensis.exe to PATH.'
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

    Write-Host 'Building Release...'
    & dotnet build MiniTranslator.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host 'Testing Release...'
    & dotnet test MiniTranslator.slnx --configuration Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $version = & dotnet msbuild apps/MiniTranslator.Wpf/MiniTranslator.Wpf.csproj -nologo -getProperty:Version -property:Configuration=Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $version = ($version -join "`n").Trim()
    if ($version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
        throw "Invalid release version: $version"
    }

    $artifacts = Join-Path $PWD 'artifacts'
    $publishDirectory = Join-Path $artifacts 'publish/win-x64'
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    Write-Host 'Bundling self-contained win-x64 app...'
    & dotnet publish apps/MiniTranslator.Wpf/MiniTranslator.Wpf.csproj --configuration Release --runtime win-x64 --self-contained true --output $publishDirectory -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $appExecutable = Join-Path $publishDirectory 'MiniTranslator.exe'
    if (-not (Test-Path -LiteralPath $appExecutable -PathType Leaf)) {
        throw 'Publish did not produce MiniTranslator.exe.'
    }

    $installer = Join-Path $artifacts "MiniTranslator-$version-win-x64-Setup.exe"
    if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer }
    Write-Host 'Creating NSIS installer...'
    & $nsisPath /V2 "/DAPP_VERSION=$version" "/DAPP_EXE=$appExecutable" "/DOUTPUT_FILE=$installer" scripts/installer.nsi
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        throw 'NSIS did not produce the installer.'
    }
    Write-Host "Installer: $installer"
}
finally {
    Pop-Location
}
