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
        'dotnetcli.blob.core.windows.net'
    )
    return $trustedHosts -contains $Uri.Host.ToLowerInvariant()
}

function Test-ExpectedDownloadArchitecture {
    param(
        [Uri]$Uri,
        [string]$Architecture
    )

    if (-not $Uri) { return $false }
    if ($Uri.AbsolutePath -match 'win-(x64|arm64|x86)') {
        return $Matches[1].ToLowerInvariant() -eq $Architecture.ToLowerInvariant()
    }
    return $true
}

function Get-RuntimeInstallerMetadata {
    param([string]$LiteralPath)
    return [Diagnostics.FileVersionInfo]::GetVersionInfo($LiteralPath)
}

function Assert-TrustedRuntimeInstaller {
    param(
        [string]$LiteralPath,
        [string]$Architecture
    )

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

    $hasMajorProperty = $metadata.PSObject.Properties['ProductMajorPart'] -and $null -ne $metadata.ProductMajorPart
    $matchesMajor = if ($hasMajorProperty) { $metadata.ProductMajorPart -eq 11 } else { $true }
    $matchesVersionText = ($metadata.ProductName -match '\b11\.0\.\d+') -or ($metadata.FileDescription -match '\b11\.0\.\d+')
    if (-not $matchesMajor -or (-not $hasMajorProperty -and -not $matchesVersionText) -or
        ($metadata.ProductName -match '\b\d+\.\d+\.\d+' -and $metadata.ProductName -notmatch '\b11\.0\.\d+') -or
        ($metadata.FileDescription -match '\b\d+\.\d+\.\d+' -and $metadata.FileDescription -notmatch '\b11\.0\.\d+')) {
        throw 'The downloaded runtime installer is not for .NET 11.0.'
    }

    if ($Architecture) {
        if ($metadata.ProductName -match '\((x64|arm64|x86)\)' -and $Matches[1].ToLowerInvariant() -ne $Architecture.ToLowerInvariant()) {
            throw 'The downloaded runtime installer architecture does not match the target architecture.'
        }
        if ($metadata.FileDescription -match '\((x64|arm64|x86)\)' -and $Matches[1].ToLowerInvariant() -ne $Architecture.ToLowerInvariant()) {
            throw 'The downloaded runtime installer architecture does not match the target architecture.'
        }
    }
}

function Set-DownloadDirectoryAcl {
    param(
        [string]$LiteralPath,
        [System.Security.AccessControl.FileSystemRights]$UserRights
    )

    $directoryInfo = New-Object System.IO.DirectoryInfo($LiteralPath)
    $acl = $directoryInfo.GetAccessControl([System.Security.AccessControl.AccessControlSections]::Access)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($existingRule in @($acl.GetAccessRules($true, $true, [System.Security.Principal.SecurityIdentifier]))) {
        $null = $acl.RemoveAccessRule($existingRule)
    }

    $inheritance = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit
    $propagation = [System.Security.AccessControl.PropagationFlags]::None
    $allow = [System.Security.AccessControl.AccessControlType]::Allow

    $systemSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
    $adminsSid = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
    $currentUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User

    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($systemSid, [System.Security.AccessControl.FileSystemRights]::FullControl, $inheritance, $propagation, $allow)))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($adminsSid, [System.Security.AccessControl.FileSystemRights]::FullControl, $inheritance, $propagation, $allow)))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($currentUserSid, $UserRights, $inheritance, $propagation, $allow)))

    $directoryInfo.SetAccessControl($acl)
}

function Assert-SingleDirectoryEntry {
    param(
        [string]$DirectoryPath,
        [string]$ExpectedFileName
    )

    $entries = @(Get-ChildItem -LiteralPath $DirectoryPath -Force)
    if ($entries.Count -ne 1 -or $entries[0].PSIsContainer -or $entries[0].Name -ne $ExpectedFileName) {
        throw 'Unexpected files were detected in the runtime download directory.'
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

        Set-DownloadDirectoryAcl -LiteralPath $downloadDirectory -UserRights ([System.Security.AccessControl.FileSystemRights]::FullControl)

        $downloadUrl = "https://aka.ms/dotnet/11.0/windowsdesktop-runtime-win-$Architecture.exe"
        if (-not (Test-TrustedDownloadUri ([Uri]$downloadUrl))) {
            throw 'The runtime download URL is not trusted.'
        }

        Write-Host "Downloading .NET 11 Desktop Runtime ($Architecture)..."
        $response = Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $runtimeInstaller -PassThru -TimeoutSec 300
        $finalUri = $null
        if ($response -and $response.BaseResponse -and $response.BaseResponse.PSObject.Properties['ResponseUri']) {
            $finalUri = $response.BaseResponse.ResponseUri
        }
        elseif ($response -and $response.BaseResponse -and $response.BaseResponse.PSObject.Properties['RequestMessage'] -and $response.BaseResponse.RequestMessage) {
            $finalUri = $response.BaseResponse.RequestMessage.RequestUri
        }
        if (-not $finalUri -or -not (Test-TrustedDownloadUri ([Uri]$finalUri)) -or -not (Test-ExpectedDownloadArchitecture -Uri ([Uri]$finalUri) -Architecture $Architecture)) {
            throw 'The runtime download redirected to an untrusted location.'
        }

        $fileInfo = Get-Item -LiteralPath $runtimeInstaller -Force
        if (($fileInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The downloaded runtime installer cannot be a reparse point.'
        }

        $lockStream = [IO.File]::Open($runtimeInstaller, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        Set-DownloadDirectoryAcl -LiteralPath $downloadDirectory -UserRights ([System.Security.AccessControl.FileSystemRights]::ReadAndExecute)
        Assert-SingleDirectoryEntry -DirectoryPath $downloadDirectory -ExpectedFileName 'windowsdesktop-runtime.exe'
        Assert-TrustedRuntimeInstaller -LiteralPath $runtimeInstaller -Architecture $Architecture

        Write-Host 'Installing the runtime. Approve the Windows administrator prompt to continue.'
        $process = Start-Process -FilePath $runtimeInstaller -ArgumentList '/install', '/quiet', '/norestart' -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw "Runtime installation failed with exit code $($process.ExitCode)." }
        if (-not (Test-DesktopRuntimeInstalled $Architecture)) { throw 'The required runtime was not detected after installation.' }
        return $process.ExitCode
    }
    finally {
        if ($lockStream) { $lockStream.Dispose() }
        if (Test-Path -LiteralPath $downloadDirectory) {
            $dirCheck = Get-Item -LiteralPath $downloadDirectory -Force
            if (($dirCheck.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) {
                Set-DownloadDirectoryAcl -LiteralPath $downloadDirectory -UserRights ([System.Security.AccessControl.FileSystemRights]::FullControl)
                Remove-Item -LiteralPath $downloadDirectory -Recurse -Force
            }
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    try { exit (Install-DesktopRuntime $Architecture) }
    catch {
        Write-Error $_ -ErrorAction Continue
        exit 1
    }
}
