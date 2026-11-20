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

function Enable-Tls12 {
    $protocol = [Net.SecurityProtocolType]::Tls12
    if ([Enum]::IsDefined([Net.SecurityProtocolType], 'Tls13')) {
        $protocol = $protocol -bor [Net.SecurityProtocolType]::Tls13
    }
    [Net.ServicePointManager]::SecurityProtocol = $protocol
}

function Test-TrustedDownloadUri {
    param([Uri]$Uri)

    if (-not $Uri -or $Uri.Scheme -ne 'https' -or -not $Uri.IsDefaultPort -or $Uri.UserInfo) {
        return $false
    }

    $trustedHosts = @(
        'aka.ms'
        'builds.dotnet.microsoft.com'
        'download.visualstudio.microsoft.com'
        'dotnetcli.azureedge.net'
        'dotnetcli.blob.core.windows.net'
    )
    return $trustedHosts -contains $Uri.Host.ToLowerInvariant()
}

function Get-RuntimeInstallerMetadata {
    param([string]$LiteralPath)
    return [Diagnostics.FileVersionInfo]::GetVersionInfo($LiteralPath)
}

function Assert-TrustedRuntimeInstaller {
    param([string]$LiteralPath)

    $signature = Get-AuthenticodeSignature -LiteralPath $LiteralPath
    if ($signature.Status -ne 'Valid' -or
        $signature.SignerCertificate.Subject -notmatch '(^|,\s*)CN=Microsoft Corporation(,|$)' -or
        $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)' -or
        -not $signature.TimeStamperCertificate) {
        throw 'The runtime installer does not have a valid Microsoft signature.'
    }

    $metadata = Get-RuntimeInstallerMetadata -LiteralPath $LiteralPath
    if (-not $metadata -or
        $metadata.CompanyName -ne 'Microsoft Corporation' -or
        ($metadata.ProductName -notmatch 'Windows Desktop Runtime' -and $metadata.FileDescription -notmatch 'Windows Desktop Runtime')) {
        throw 'The downloaded file is not a Microsoft .NET Windows Desktop Runtime installer.'
    }
}

function Install-DesktopRuntime {
    param([string]$Architecture)

    if (Test-DesktopRuntimeInstalled $Architecture) {
        Write-Host ".NET 11 Desktop Runtime ($Architecture) is already installed."
        return 0
    }

    Enable-Tls12

    $downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) ('MiniTranslator-runtime-' + [Guid]::NewGuid())
    New-Item -ItemType Directory -Path $downloadDirectory | Out-Null
    $runtimeInstaller = Join-Path $downloadDirectory 'windowsdesktop-runtime.exe'
    $lockStream = $null
    try {
        $directoryInfo = Get-Item -LiteralPath $downloadDirectory -Force
        if (($directoryInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The temporary download directory cannot be a reparse point.'
        }

        $downloadUrl = "https://aka.ms/dotnet/11.0/windowsdesktop-runtime-win-$Architecture.exe"
        Write-Host "Downloading .NET 11 Desktop Runtime ($Architecture)..."
        $response = Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $runtimeInstaller -PassThru -TimeoutSec 300
        if ($response) {
            $finalUri = $null
            if ($response.BaseResponse -and $response.BaseResponse.PSObject.Properties['ResponseUri']) {
                $finalUri = $response.BaseResponse.ResponseUri
            }
            elseif ($response.BaseResponse -and $response.BaseResponse.PSObject.Properties['RequestMessage'] -and $response.BaseResponse.RequestMessage) {
                $finalUri = $response.BaseResponse.RequestMessage.RequestUri
            }
            if ($finalUri -and -not (Test-TrustedDownloadUri ([Uri]$finalUri))) {
                throw 'The runtime download redirected to an untrusted location.'
            }
        }

        $fileInfo = Get-Item -LiteralPath $runtimeInstaller -Force
        if (($fileInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The downloaded runtime installer cannot be a reparse point.'
        }

        $lockStream = [IO.File]::Open($runtimeInstaller, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        Assert-TrustedRuntimeInstaller -LiteralPath $runtimeInstaller

        Write-Host 'Installing the runtime. Approve the Windows administrator prompt to continue.'
        $process = Start-Process -FilePath $runtimeInstaller -ArgumentList '/install', '/quiet', '/norestart' -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw "Runtime installation failed with exit code $($process.ExitCode)." }
        if (-not (Test-DesktopRuntimeInstalled $Architecture)) { throw 'The required runtime was not detected after installation.' }
        return $process.ExitCode
    }
    finally {
        if ($lockStream) { $lockStream.Dispose() }
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
