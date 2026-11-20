$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/install-desktop-runtime.ps1') -Architecture x64

function Test-Path {
    param($LiteralPath)
    if ($LiteralPath -like 'HKLM:*') {
        $parts = $LiteralPath.Split('\')
        return $script:installedVersions.ContainsKey("$($parts[-3])/$($parts[-1])")
    }
    Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath
}

function Get-ItemProperty {
    param($LiteralPath)
    $parts = $LiteralPath.Split('\')
    $properties = @{}
    foreach ($version in $script:installedVersions["$($parts[-3])/$($parts[-1])"]) { $properties[$version] = 1 }
    [pscustomobject]$properties
}

function Invoke-WebRequest {
    param($Uri, $OutFile, [switch]$UseBasicParsing, [switch]$PassThru, $TimeoutSec)
    $script:downloadUri = $Uri
    $script:downloadPath = $OutFile
    if ($script:scenario -eq 'download-failure') { throw 'Network unavailable.' }
    [IO.File]::WriteAllText($OutFile, 'runtime installer fixture')
    $finalUri = switch ($script:scenario) {
        'downgrade-redirect' { 'http://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/11.0.1/windowsdesktop-runtime-win-x64.exe' }
        'untrusted-redirect' { 'https://example.org/windowsdesktop-runtime.exe' }
        default { 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/11.0.1/windowsdesktop-runtime-win-x64.exe' }
    }
    if ($PassThru) {
        [pscustomobject]@{
            BaseResponse = [pscustomobject]@{
                ResponseUri = [Uri]$finalUri
            }
        }
    }
}

function Get-AuthenticodeSignature {
    param($LiteralPath)
    if ($LiteralPath -ne $script:downloadPath) { throw 'Signature checked on the wrong file.' }
    [pscustomobject]@{
        Status = $(if ($script:scenario -eq 'invalid-signature') { 'HashMismatch' } else { 'Valid' })
        SignerCertificate = [pscustomobject]@{
            Subject = $(if ($script:scenario -eq 'wrong-publisher') { 'CN=Other, O=Other' } else { 'CN=Microsoft Corporation, O=Microsoft Corporation, C=US' })
        }
        TimeStamperCertificate = $(if ($script:scenario -eq 'missing-timestamp') { $null } else { [pscustomobject]@{ Subject = 'CN=Microsoft Time-Stamp Service' } })
    }
}

function Get-RuntimeInstallerMetadata {
    param($LiteralPath)
    if ($LiteralPath -ne $script:downloadPath) { throw 'Metadata checked on the wrong file.' }
    if ($script:scenario -eq 'wrong-product') {
        return [pscustomobject]@{
            CompanyName = 'Microsoft Corporation'
            ProductName = 'Unrelated Microsoft Tool'
            FileDescription = 'Unrelated Microsoft Tool'
        }
    }
    [pscustomobject]@{
        CompanyName = 'Microsoft Corporation'
        ProductName = 'Microsoft Windows Desktop Runtime - 11.0.1'
        FileDescription = 'Microsoft Windows Desktop Runtime - 11.0.1'
    }
}

function Start-Process {
    param($FilePath, $ArgumentList, $Verb, $WindowStyle, [switch]$Wait, [switch]$PassThru)
    $script:installationStarted = $true
    if ($FilePath -ne $script:downloadPath -or ($ArgumentList -join ' ') -ne '/install /quiet /norestart' -or
        $Verb -ne 'RunAs' -or $WindowStyle -ne 'Hidden' -or -not $Wait -or -not $PassThru) {
        throw 'Incorrect runtime installation command.'
    }
    $lockedAgainstWrite = $false
    try {
        $probe = [IO.File]::Open($FilePath, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $probe.Dispose()
    }
    catch [IO.IOException] {
        $lockedAgainstWrite = $true
    }
    if (-not $lockedAgainstWrite) {
        throw 'Runtime installer was not write-locked across verification and execution.'
    }
    if ($script:scenario -eq 'elevation-cancelled') { throw 'Elevation cancelled.' }
    if ($script:scenario -eq 'installer-failure') { return [pscustomobject]@{ ExitCode = 1603 } }
    if ($script:scenario -ne 'runtime-still-missing') {
        $script:installedVersions["$script:architecture/Microsoft.WindowsDesktop.App"] = @('11.0.1')
        $script:installedVersions["$script:architecture/Microsoft.NETCore.App"] = @('11.0.1')
    }
    [pscustomobject]@{ ExitCode = $(if ($script:scenario -eq 'restart-required') { 3010 } else { 0 }) }
}

foreach ($script:architecture in @('x64', 'arm64')) {
    foreach ($version in @('11.0.0', '11.0.12', '10.0.12', '12.0.0', '11.0.0-preview.1')) {
        $script:installedVersions = @{
            "$script:architecture/Microsoft.WindowsDesktop.App" = @($version)
            "$script:architecture/Microsoft.NETCore.App" = @($version)
        }
        $expected = $version -in @('11.0.0', '11.0.12')
        if ((Test-DesktopRuntimeInstalled $script:architecture) -ne $expected) { throw "Incorrect detection for $script:architecture/$version." }
        $otherArchitecture = if ($script:architecture -eq 'x64') { 'arm64' } else { 'x64' }
        if (Test-DesktopRuntimeInstalled $otherArchitecture) { throw 'Detected the wrong architecture.' }
    }
    $script:installedVersions = @{ "$script:architecture/Microsoft.WindowsDesktop.App" = @('11.0.0') }
    if (Test-DesktopRuntimeInstalled $script:architecture) { throw 'Accepted a missing core runtime.' }

    foreach ($script:scenario in @('already-installed', 'install', 'restart-required', 'download-failure', 'downgrade-redirect', 'untrusted-redirect', 'invalid-signature', 'wrong-publisher', 'missing-timestamp', 'wrong-product', 'elevation-cancelled', 'installer-failure', 'runtime-still-missing')) {
        $script:installedVersions = @{}
        $script:downloadPath = $null
        $script:downloadUri = $null
        $script:installationStarted = $false
        if ($script:scenario -eq 'already-installed') {
            $script:installedVersions["$script:architecture/Microsoft.WindowsDesktop.App"] = @('11.0.0')
            $script:installedVersions["$script:architecture/Microsoft.NETCore.App"] = @('11.0.0')
        }
        $failure = $null
        try { $exitCode = Install-DesktopRuntime $script:architecture }
        catch { $failure = $_ }
        $shouldFail = $script:scenario -notin @('already-installed', 'install', 'restart-required')
        if ([bool]$failure -ne $shouldFail) { throw "Unexpected result for $script:architecture/$script:scenario : $failure" }
        if (-not $shouldFail) {
            $expectedExit = if ($script:scenario -eq 'restart-required') { 3010 } else { 0 }
            if ($exitCode -ne $expectedExit) { throw "Incorrect exit code for $script:scenario." }
        }
        if ($script:scenario -eq 'already-installed') {
            if ($script:downloadPath -or $script:installationStarted) { throw 'Reinstalled an existing runtime.' }
        }
        else {
            if ($script:downloadUri -ne "https://aka.ms/dotnet/11.0/windowsdesktop-runtime-win-$script:architecture.exe") { throw 'Incorrect runtime download URL.' }
            if (Test-Path -LiteralPath (Split-Path -Parent $script:downloadPath)) { throw 'Runtime download directory was not removed.' }
        }
        if ($script:scenario -in @('download-failure', 'downgrade-redirect', 'untrusted-redirect', 'invalid-signature', 'wrong-publisher', 'missing-timestamp', 'wrong-product') -and $script:installationStarted) {
            throw 'Executed an untrusted or incomplete download.'
        }
    }
}
Write-Host 'Desktop runtime installer: architecture, version, existing runtime, download, signature, elevation, installation, restart and cleanup checks passed.'
