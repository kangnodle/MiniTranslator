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
    # Resolve global.json from the repository before running any build steps.
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($sdkVersion -notmatch '^11\.') {
        throw ".NET 11 SDK is required (selected SDK: $sdkVersion). See global.json."
    }

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
    $publishRoot = Join-Path $artifacts 'publish'
    foreach ($runtime in @('win-x64', 'win-arm64')) {
        $publishDirectory = Join-Path $publishRoot $runtime
        if (Test-Path -LiteralPath $publishDirectory) {
            foreach ($directory in @($artifacts, $publishRoot, $publishDirectory)) {
                $item = Get-Item -LiteralPath $directory -Force
                if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                    throw "Expected a regular build directory: $directory"
                }
            }
            Remove-Item -LiteralPath $publishDirectory -Recurse -Force
        }

        # .NET 11 WPF loads mixed-mode assemblies such as DirectWriteForwarder.dll from disk.
        Write-Host "Publishing self-contained $runtime app..."
        & dotnet publish apps/MiniTranslator.Wpf/MiniTranslator.Wpf.csproj --configuration Release --runtime $runtime --self-contained true --output $publishDirectory -p:PublishSingleFile=false -p:DebugType=embedded
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $appExecutable = Join-Path $publishDirectory 'MiniTranslator.exe'
        if (-not (Test-Path -LiteralPath $appExecutable -PathType Leaf)) {
            throw "Publish did not produce MiniTranslator.exe for $runtime."
        }

        # Remove only installed payload files and empty payload directories.
        $uninstallList = Join-Path $artifacts "$runtime-uninstall.nsh"
        $uninstallCommands = foreach ($file in Get-ChildItem -LiteralPath $publishDirectory -File -Recurse) {
            if ($file.FullName -ne $appExecutable) {
                $relativePath = $file.FullName.Substring($publishDirectory.Length + 1).Replace('$', '$$')
                'Delete "$INSTDIR\{0}"' -f $relativePath
            }
        }
        $uninstallCommands += foreach ($directory in Get-ChildItem -LiteralPath $publishDirectory -Directory -Recurse |
            Sort-Object { $_.FullName.Length } -Descending) {
            $relativePath = $directory.FullName.Substring($publishDirectory.Length + 1).Replace('$', '$$')
            'RMDir "$INSTDIR\{0}"' -f $relativePath
        }
        Set-Content -LiteralPath $uninstallList -Value $uninstallCommands -Encoding UTF8

        $installer = Join-Path $artifacts "MiniTranslator-$version-$runtime-Setup.exe"
        if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer }
        Write-Host "Creating NSIS installer for $runtime..."
        & $nsisPath /V2 "/DAPP_VERSION=$version" "/DAPP_RUNTIME=$runtime" "/DAPP_DIRECTORY=$publishDirectory" "/DUNINSTALL_LIST=$uninstallList" "/DOUTPUT_FILE=$installer" scripts/installer.nsi
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
            throw "NSIS did not produce the installer for $runtime."
        }
        Write-Host "Installer: $installer"
    }
}
finally {
    Pop-Location
}
