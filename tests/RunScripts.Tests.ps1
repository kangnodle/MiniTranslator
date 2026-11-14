$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('MiniTranslator script tests-' + [Guid]::NewGuid())
$originalPath = $env:PATH

New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    $utf8 = New-Object Text.UTF8Encoding $false
    [IO.File]::WriteAllText((Join-Path $testRoot 'record.ps1'), @'
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath 'MiniTranslator.slnx')) { exit 91 }
$nativeArgs = [Environment]::GetCommandLineArgs()[4..([Environment]::GetCommandLineArgs().Length - 1)]
Add-Content -LiteralPath $env:MINITRANSLATOR_SCRIPT_TEST_LOG -Value ($nativeArgs -join '|')
$stage = $nativeArgs[0]
if ($stage -eq '--version') { Write-Output '11.0.100'; exit 0 }
if ($stage -eq 'format' -and $nativeArgs -contains '--verify-no-changes') { $stage = 'lint' }
if ($stage -eq $env:MINITRANSLATOR_SCRIPT_TEST_FAIL) { exit 23 }
exit 0
'@, $utf8)
    [IO.File]::WriteAllText((Join-Path $testRoot 'dotnet.cmd'), '@powershell.exe -NoProfile -File "%~dp0record.ps1" %*', $utf8)
    [IO.File]::WriteAllText((Join-Path $testRoot 'dotnet'), @'
#!/usr/bin/env bash
exec powershell.exe -NoProfile -File "$(dirname "$0")/record.ps1" "$@"
'@.Replace("`r`n", "`n"), $utf8)

    $env:PATH = "$testRoot;$originalPath"
    $env:MINITRANSLATOR_SCRIPT_TEST_LOG = Join-Path $testRoot 'commands.log'
    $expected = @(
        '--version'
        'restore|MiniTranslator.slnx'
        'format|MiniTranslator.slnx|--no-restore'
        'format|MiniTranslator.slnx|--no-restore|--verify-no-changes'
        'build|MiniTranslator.slnx|--no-restore'
        'test|MiniTranslator.slnx|--no-build|--no-restore'
        'run|--project|apps/MiniTranslator.Wpf|--no-build|--no-restore|--|--show|argument with spaces'
    )
    $stages = @('', 'restore', 'format', 'lint', 'build', 'test', 'run')
    Push-Location -LiteralPath $testRoot
    try {
        foreach ($shell in @('powershell', 'bash')) {
            foreach ($stage in $stages) {
                $env:MINITRANSLATOR_SCRIPT_TEST_FAIL = $stage
                [IO.File]::WriteAllText($env:MINITRANSLATOR_SCRIPT_TEST_LOG, '', $utf8)
                if ($shell -eq 'powershell') {
                    & powershell.exe -NoProfile -File (Join-Path $repoRoot 'scripts/run.ps1') --show 'argument with spaces' | Out-Null
                }
                else {
                    & bash.exe (Join-Path $repoRoot 'scripts/run.sh').Replace('\', '/') --show 'argument with spaces' | Out-Null
                }
                $expectedExit = if ($stage) { 23 } else { 0 }
                if ($LASTEXITCODE -ne $expectedExit) { throw "$shell/$stage returned $LASTEXITCODE, expected $expectedExit." }
                $count = if ($stage) { [Array]::IndexOf($stages, $stage) + 1 } else { $expected.Count }
                $actual = @(Get-Content -LiteralPath $env:MINITRANSLATOR_SCRIPT_TEST_LOG)
                if (($actual -join "`n") -cne ($expected[0..($count - 1)] -join "`n")) {
                    throw "$shell/$stage ran unexpected commands: $($actual -join ', ')."
                }
            }
        }
    }
    finally { Pop-Location }
    Write-Host 'Run scripts: command order, working directory, arguments and all failure exits passed.'
}
finally {
    $env:PATH = $originalPath
    Remove-Item Env:MINITRANSLATOR_SCRIPT_TEST_LOG, Env:MINITRANSLATOR_SCRIPT_TEST_FAIL -ErrorAction SilentlyContinue
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
