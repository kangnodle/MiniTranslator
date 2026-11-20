param(
    [Parameter(Mandatory)]
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture
)

$ErrorActionPreference = 'Stop'

function Test-DesktopRuntimeInstalled {
    param([string]$Architecture)

    foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        $registryPath = "HKLM:\SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\$Architecture\sharedfx\$framework"
        if (-not (Test-Path -LiteralPath $registryPath)) { return $false }
        $versions = (Get-ItemProperty -LiteralPath $registryPath).PSObject.Properties
        if (-not ($versions | Where-Object { $_.Name -match '^11\.0\.\d+$' -and $_.Value -eq 1 })) { return $false }
    }
    return $true
}

function Install-DesktopRuntime {
    param([string]$Architecture)

    if (Test-DesktopRuntimeInstalled $Architecture) {
        Write-Host ".NET 11 Desktop Runtime ($Architecture) is already installed."
        return 0
    }

    $downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) ('MiniTranslator-runtime-' + [Guid]::NewGuid())
    New-Item -ItemType Directory -Path $downloadDirectory | Out-Null
    $runtimeInstaller = Join-Path $downloadDirectory 'windowsdesktop-runtime.exe'
    try {
        Write-Host "Downloading .NET 11 Desktop Runtime ($Architecture)..."
        Invoke-WebRequest -UseBasicParsing -Uri "https://aka.ms/dotnet/11.0/windowsdesktop-runtime-win-$Architecture.exe" -OutFile $runtimeInstaller -TimeoutSec 300
        $signature = Get-AuthenticodeSignature -LiteralPath $runtimeInstaller
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
            throw 'The runtime installer does not have a valid Microsoft signature.'
        }

        Write-Host 'Installing the runtime. Approve the Windows administrator prompt to continue.'
        $process = Start-Process -FilePath $runtimeInstaller -ArgumentList '/install', '/quiet', '/norestart' -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw "Runtime installation failed with exit code $($process.ExitCode)." }
        if (-not (Test-DesktopRuntimeInstalled $Architecture)) { throw 'The required runtime was not detected after installation.' }
        return $process.ExitCode
    }
    finally {
        if (Test-Path -LiteralPath $runtimeInstaller) { Remove-Item -LiteralPath $runtimeInstaller -Force }
        Remove-Item -LiteralPath $downloadDirectory -Force
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    try { exit (Install-DesktopRuntime $Architecture) }
    catch {
        Write-Error $_ -ErrorAction Continue
        exit 1
    }
}
