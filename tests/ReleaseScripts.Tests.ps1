$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('MiniTranslator release script tests-' + [Guid]::NewGuid())
$originalPath = $env:PATH

New-Item -ItemType Directory -Path (Join-Path $testRoot 'repo/scripts') -Force | Out-Null
try {
    $utf8 = New-Object Text.UTF8Encoding $false
    $fixture = Join-Path $testRoot 'repo'
    foreach ($name in @('release.ps1', 'release.sh', 'installer.nsi')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot "scripts/$name") -Destination (Join-Path $fixture "scripts/$name")
    }
    [IO.File]::WriteAllText((Join-Path $fixture 'MiniTranslator.slnx'), '', $utf8)
    [IO.File]::WriteAllText((Join-Path $testRoot 'record.ps1'), @'
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath 'MiniTranslator.slnx')) { exit 91 }
$nativeArgs = [Environment]::GetCommandLineArgs()[4..([Environment]::GetCommandLineArgs().Length - 1)]
Add-Content -LiteralPath $env:MINITRANSLATOR_RELEASE_TEST_LOG -Value ($nativeArgs -join '|')
$stage = $nativeArgs[0]
if ($stage -eq '--version') { Write-Output '11.0.100'; exit 0 }
if ($stage -eq 'format' -and $nativeArgs -contains '--verify-no-changes') { $stage = 'lint' }
if ($stage -eq $env:MINITRANSLATOR_RELEASE_TEST_FAIL) { exit 23 }
if ($stage -eq 'msbuild') {
    if ($env:MINITRANSLATOR_RELEASE_TEST_FAIL -eq 'invalid-version') { Write-Output '../invalid' }
    else { Write-Output '0.2.0' }
}
if ($stage -eq 'publish' -and $env:MINITRANSLATOR_RELEASE_TEST_FAIL -ne 'missing-bundle') {
    $directory = $nativeArgs[[Array]::IndexOf($nativeArgs, '--output') + 1]
    [IO.File]::WriteAllText((Join-Path $directory 'MiniTranslator.exe'), '')
}
if ($stage -eq 'nsis' -and $env:MINITRANSLATOR_RELEASE_TEST_FAIL -ne 'missing-installer') {
    $output = ($nativeArgs | Where-Object { $_.StartsWith('/DOUTPUT_FILE=') }).Substring(14)
    [IO.File]::WriteAllText($output, '')
}
exit 0
'@, $utf8)
    [IO.File]::WriteAllText((Join-Path $testRoot 'dotnet.cmd'), '@powershell.exe -NoProfile -File "%~dp0record.ps1" %*', $utf8)
    [IO.File]::WriteAllText((Join-Path $testRoot 'makensis.cmd'), '@powershell.exe -NoProfile -File "%~dp0record.ps1" nsis %*', $utf8)
    $env:PATH = "$testRoot;$originalPath"
    $env:MINITRANSLATOR_RELEASE_TEST_LOG = Join-Path $testRoot 'commands.log'

    $publishDirectory = Join-Path $fixture 'artifacts/publish/win-x64'
    $appExecutable = Join-Path $publishDirectory 'MiniTranslator.exe'
    $installer = Join-Path $fixture 'artifacts/MiniTranslator-0.2.0-win-x64-Setup.exe'
    $expected = @(
        '--version'
        'restore|MiniTranslator.slnx'
        'format|MiniTranslator.slnx|--no-restore'
        'format|MiniTranslator.slnx|--no-restore|--verify-no-changes'
        'build|MiniTranslator.slnx|--configuration|Release|--no-restore'
        'test|MiniTranslator.slnx|--configuration|Release|--no-build|--no-restore'
        'msbuild|apps/MiniTranslator.Wpf/MiniTranslator.Wpf.csproj|-nologo|-getProperty:Version|-property:Configuration=Release'
        "publish|apps/MiniTranslator.Wpf/MiniTranslator.Wpf.csproj|--configuration|Release|--runtime|win-x64|--self-contained|true|--output|$publishDirectory|-p:PublishSingleFile=true|-p:IncludeNativeLibrariesForSelfExtract=true|-p:DebugType=embedded"
        "nsis|/V2|/DAPP_VERSION=0.2.0|/DAPP_EXE=$appExecutable|/DOUTPUT_FILE=$installer|scripts/installer.nsi"
    )
    $stages = @('', 'restore', 'format', 'lint', 'build', 'test', 'msbuild', 'publish', 'nsis')
    $cases = $stages + @('invalid-version', 'missing-bundle', 'missing-installer')
    Push-Location -LiteralPath $testRoot
    try {
        foreach ($shell in @('powershell', 'bash')) {
            foreach ($stage in $cases) {
                $env:MINITRANSLATOR_RELEASE_TEST_FAIL = $stage
                [IO.File]::WriteAllText($env:MINITRANSLATOR_RELEASE_TEST_LOG, '', $utf8)
                foreach ($output in @($appExecutable, $installer)) {
                    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output }
                }
                $ErrorActionPreference = 'Continue'
                try {
                    if ($shell -eq 'powershell') {
                        & powershell.exe -NoProfile -File (Join-Path $fixture 'scripts/release.ps1') 2>$null | Out-Null
                    }
                    else {
                        & bash.exe (Join-Path $fixture 'scripts/release.sh').Replace('\', '/') 2>$null | Out-Null
                    }
                }
                finally { $ErrorActionPreference = 'Stop' }
                $expectedExit = if (-not $stage) { 0 } elseif ($stages -contains $stage) { 23 } else { 1 }
                if ($LASTEXITCODE -ne $expectedExit) { throw "$shell/$stage returned $LASTEXITCODE, expected $expectedExit." }
                $count = switch ($stage) {
                    'invalid-version' { 7 }
                    'missing-bundle' { 8 }
                    'missing-installer' { 9 }
                    '' { 9 }
                    default { [Array]::IndexOf($stages, $stage) + 1 }
                }
                $actual = @(Get-Content -LiteralPath $env:MINITRANSLATOR_RELEASE_TEST_LOG)
                if (($actual -join "`n") -cne ($expected[0..($count - 1)] -join "`n")) {
                    throw "$shell/$stage ran unexpected commands: $($actual -join ', ')."
                }
                if (-not $stage -and -not (Test-Path -LiteralPath $installer)) { throw 'Installer missing after success.' }
            }
        }
    }
    finally { Pop-Location }
    Write-Host 'Release scripts: both entry points, command order, failure exits, version and output checks passed.'
}
finally {
    $env:PATH = $originalPath
    Remove-Item Env:MINITRANSLATOR_RELEASE_TEST_LOG, Env:MINITRANSLATOR_RELEASE_TEST_FAIL -ErrorAction SilentlyContinue
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
